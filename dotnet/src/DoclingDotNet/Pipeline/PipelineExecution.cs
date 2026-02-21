using System.Diagnostics;

namespace DoclingDotNet.Pipeline;

public enum PipelineRunStatus
{
    Succeeded = 0,
    Failed = 1,
    TimedOut = 2,
    Cancelled = 3
}

public enum PipelineEventKind
{
    StageStarted = 0,
    StageCompleted = 1,
    StageFailed = 2,
    StageCancelled = 3,
    StageSkipped = 4
}

public enum PipelineStageKind
{
    Regular = 0,
    Cleanup = 1
}

public enum PipelineStageStatus
{
    Succeeded = 0,
    Failed = 1,
    Cancelled = 2,
    Skipped = 3
}

public sealed record PipelineEvent(
    string RunId,
    string StageName,
    PipelineEventKind Kind,
    DateTimeOffset TimestampUtc,
    TimeSpan? Duration = null,
    string? Message = null);

public sealed record PipelineStageResult(
    string RunId,
    string StageName,
    PipelineStageKind Kind,
    PipelineStageStatus Status,
    DateTimeOffset? StartedUtc = null,
    DateTimeOffset? CompletedUtc = null,
    TimeSpan? Duration = null,
    string? ErrorMessage = null);

public sealed class PipelineRunContext
{
    public required string RunId { get; init; }
}

public sealed class PipelineStageDefinition
{
    public required string Name { get; init; }

    public required Func<PipelineRunContext, CancellationToken, Task> ExecuteAsync { get; init; }

    public PipelineStageKind Kind { get; init; } = PipelineStageKind.Regular;
}

public sealed class PipelineRunResult
{
    public required string RunId { get; init; }

    public required PipelineRunStatus Status { get; init; }

    public string? FailureStage { get; init; }

    public string? ErrorMessage { get; init; }

    public required IReadOnlyList<PipelineEvent> Events { get; init; }

    public required IReadOnlyList<PipelineStageResult> StageResults { get; init; }
}

public sealed class PipelineExecutor
{
    public async Task<PipelineRunResult> ExecuteAsync(
        string runId,
        IReadOnlyList<PipelineStageDefinition> stages,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(stages);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");
        }

        var events = new List<PipelineEvent>();
        var stageResults = new List<PipelineStageResult>();
        var context = new PipelineRunContext { RunId = runId };

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        var status = PipelineRunStatus.Succeeded;
        string? failureStage = null;
        string? errorMessage = null;
        var isTerminal = false;

        foreach (var stage in stages)
        {
            if (isTerminal && stage.Kind != PipelineStageKind.Cleanup)
            {
                var skippedMessage = $"Skipped due to terminal status: {status}.";
                events.Add(new PipelineEvent(
                    runId,
                    stage.Name,
                    PipelineEventKind.StageSkipped,
                    DateTimeOffset.UtcNow,
                    Message: skippedMessage));
                stageResults.Add(new PipelineStageResult(
                    runId,
                    stage.Name,
                    stage.Kind,
                    PipelineStageStatus.Skipped,
                    ErrorMessage: skippedMessage));
                continue;
            }

            var startUtc = DateTimeOffset.UtcNow;
            var stageTimer = Stopwatch.StartNew();
            events.Add(new PipelineEvent(
                runId,
                stage.Name,
                PipelineEventKind.StageStarted,
                startUtc));

            try
            {
                var stageToken = isTerminal
                    ? CancellationToken.None
                    : linkedCts.Token;

                await stage.ExecuteAsync(context, stageToken).ConfigureAwait(false);
                stageTimer.Stop();
                var endUtc = DateTimeOffset.UtcNow;

                events.Add(new PipelineEvent(
                    runId,
                    stage.Name,
                    PipelineEventKind.StageCompleted,
                    endUtc,
                    stageTimer.Elapsed));
                stageResults.Add(new PipelineStageResult(
                    runId,
                    stage.Name,
                    stage.Kind,
                    PipelineStageStatus.Succeeded,
                    startUtc,
                    endUtc,
                    stageTimer.Elapsed));
            }
            catch (OperationCanceledException)
            {
                stageTimer.Stop();
                var endUtc = DateTimeOffset.UtcNow;
                var cancellationMessage = timeoutCts.IsCancellationRequested
                    ? "timeout"
                    : "cancelled";
                if (!isTerminal)
                {
                    status = timeoutCts.IsCancellationRequested
                        ? PipelineRunStatus.TimedOut
                        : PipelineRunStatus.Cancelled;

                    failureStage = stage.Name;
                    errorMessage = status == PipelineRunStatus.TimedOut
                        ? $"Pipeline timed out after {timeout}."
                        : "Pipeline execution cancelled.";
                    isTerminal = true;
                }

                events.Add(new PipelineEvent(
                    runId,
                    stage.Name,
                    PipelineEventKind.StageCancelled,
                    endUtc,
                    stageTimer.Elapsed,
                    cancellationMessage));
                stageResults.Add(new PipelineStageResult(
                    runId,
                    stage.Name,
                    stage.Kind,
                    PipelineStageStatus.Cancelled,
                    startUtc,
                    endUtc,
                    stageTimer.Elapsed,
                    cancellationMessage));
            }
            catch (Exception ex)
            {
                stageTimer.Stop();
                var endUtc = DateTimeOffset.UtcNow;
                if (!isTerminal)
                {
                    status = PipelineRunStatus.Failed;
                    failureStage = stage.Name;
                    errorMessage = ex.Message;
                    isTerminal = true;
                }

                events.Add(new PipelineEvent(
                    runId,
                    stage.Name,
                    PipelineEventKind.StageFailed,
                    endUtc,
                    stageTimer.Elapsed,
                    ex.Message));
                stageResults.Add(new PipelineStageResult(
                    runId,
                    stage.Name,
                    stage.Kind,
                    PipelineStageStatus.Failed,
                    startUtc,
                    endUtc,
                    stageTimer.Elapsed,
                    ex.Message));
            }
        }

        return new PipelineRunResult
        {
            RunId = runId,
            Status = status,
            FailureStage = failureStage,
            ErrorMessage = errorMessage,
            Events = events,
            StageResults = stageResults
        };
    }
}
