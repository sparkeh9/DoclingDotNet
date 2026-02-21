using DoclingDotNet.Pipeline;
using Xunit;

namespace DoclingDotNet.Tests;

public sealed class PipelineExecutionSemanticsTests
{
    [Fact]
    public async Task ExecuteAsync_WhenStageThrows_StopsPipelineAndReturnsFailed()
    {
        var executor = new PipelineExecutor();
        var executed = new List<string>();

        var stages = new[]
        {
            new PipelineStageDefinition
            {
                Name = "stage-1",
                ExecuteAsync = (_, _) =>
                {
                    executed.Add("stage-1");
                    return Task.CompletedTask;
                }
            },
            new PipelineStageDefinition
            {
                Name = "stage-2",
                ExecuteAsync = (_, _) =>
                {
                    executed.Add("stage-2");
                    throw new InvalidOperationException("boom");
                }
            },
            new PipelineStageDefinition
            {
                Name = "stage-3",
                ExecuteAsync = (_, _) =>
                {
                    executed.Add("stage-3");
                    return Task.CompletedTask;
                }
            }
        };

        var result = await executor.ExecuteAsync(
            runId: "run-fail",
            stages,
            timeout: TimeSpan.FromSeconds(2));

        Assert.Equal(PipelineRunStatus.Failed, result.Status);
        Assert.Equal("stage-2", result.FailureStage);
        Assert.Equal(new[] { "stage-1", "stage-2" }, executed);
        Assert.Contains(
            result.Events,
            e => e.StageName == "stage-3" && e.Kind == PipelineEventKind.StageSkipped);
        Assert.Equal(
            new[]
            {
                PipelineStageStatus.Succeeded,
                PipelineStageStatus.Failed,
                PipelineStageStatus.Skipped
            },
            result.StageResults.Select(s => s.Status).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTimeoutOccurs_StopsPipelineAndReturnsTimedOut()
    {
        var executor = new PipelineExecutor();
        var executed = new List<string>();

        var stages = new[]
        {
            new PipelineStageDefinition
            {
                Name = "slow-stage",
                ExecuteAsync = async (_, cancellationToken) =>
                {
                    executed.Add("slow-stage");
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            },
            new PipelineStageDefinition
            {
                Name = "never-stage",
                ExecuteAsync = (_, _) =>
                {
                    executed.Add("never-stage");
                    return Task.CompletedTask;
                }
            }
        };

        var result = await executor.ExecuteAsync(
            runId: "run-timeout",
            stages,
            timeout: TimeSpan.FromMilliseconds(100));

        Assert.Equal(PipelineRunStatus.TimedOut, result.Status);
        Assert.Equal("slow-stage", result.FailureStage);
        Assert.Equal(new[] { "slow-stage" }, executed);
        Assert.Contains(
            result.Events,
            e => e.StageName == "slow-stage" && e.Kind == PipelineEventKind.StageCancelled);
        Assert.Contains(
            result.Events,
            e => e.StageName == "never-stage" && e.Kind == PipelineEventKind.StageSkipped);
        Assert.Equal(
            new[]
            {
                PipelineStageStatus.Cancelled,
                PipelineStageStatus.Skipped
            },
            result.StageResults.Select(s => s.Status).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentRuns_AreIsolatedByRunId()
    {
        var executor = new PipelineExecutor();

        var stages = new[]
        {
            new PipelineStageDefinition
            {
                Name = "stage-a",
                ExecuteAsync = async (context, cancellationToken) =>
                {
                    await Task.Delay(25, cancellationToken);
                    Assert.StartsWith("run-", context.RunId);
                }
            },
            new PipelineStageDefinition
            {
                Name = "stage-b",
                ExecuteAsync = async (context, cancellationToken) =>
                {
                    await Task.Delay(25, cancellationToken);
                    Assert.StartsWith("run-", context.RunId);
                }
            }
        };

        var run1 = executor.ExecuteAsync("run-1", stages, TimeSpan.FromSeconds(2));
        var run2 = executor.ExecuteAsync("run-2", stages, TimeSpan.FromSeconds(2));

        var results = await Task.WhenAll(run1, run2);

        Assert.All(results, result => Assert.Equal(PipelineRunStatus.Succeeded, result.Status));
        Assert.All(results, result => Assert.All(result.Events, e => Assert.Equal(result.RunId, e.RunId)));
        Assert.All(results, result => Assert.All(result.StageResults, s => Assert.Equal(result.RunId, s.RunId)));
        Assert.Contains(results, r => r.RunId == "run-1");
        Assert.Contains(results, r => r.RunId == "run-2");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTerminal_StillExecutesCleanupStages()
    {
        var executor = new PipelineExecutor();
        var executed = new List<string>();
        var cleanupTokenWasCancelled = true;

        var stages = new[]
        {
            new PipelineStageDefinition
            {
                Name = "stage-1",
                ExecuteAsync = (_, _) =>
                {
                    executed.Add("stage-1");
                    return Task.CompletedTask;
                }
            },
            new PipelineStageDefinition
            {
                Name = "stage-2",
                ExecuteAsync = (_, _) =>
                {
                    executed.Add("stage-2");
                    throw new InvalidOperationException("boom");
                }
            },
            new PipelineStageDefinition
            {
                Name = "stage-3",
                ExecuteAsync = (_, _) =>
                {
                    executed.Add("stage-3");
                    return Task.CompletedTask;
                }
            },
            new PipelineStageDefinition
            {
                Name = "cleanup",
                Kind = PipelineStageKind.Cleanup,
                ExecuteAsync = (_, token) =>
                {
                    executed.Add("cleanup");
                    cleanupTokenWasCancelled = token.IsCancellationRequested;
                    return Task.CompletedTask;
                }
            }
        };

        var result = await executor.ExecuteAsync(
            runId: "run-cleanup",
            stages,
            timeout: TimeSpan.FromSeconds(2));

        Assert.Equal(PipelineRunStatus.Failed, result.Status);
        Assert.Equal(new[] { "stage-1", "stage-2", "cleanup" }, executed);
        Assert.False(cleanupTokenWasCancelled);
        Assert.Contains(
            result.Events,
            e => e.StageName == "stage-3" && e.Kind == PipelineEventKind.StageSkipped);
        Assert.Contains(
            result.Events,
            e => e.StageName == "cleanup" && e.Kind == PipelineEventKind.StageCompleted);
        Assert.Equal(
            new[]
            {
                PipelineStageStatus.Succeeded,
                PipelineStageStatus.Failed,
                PipelineStageStatus.Skipped,
                PipelineStageStatus.Succeeded
            },
            result.StageResults.Select(s => s.Status).ToArray());
        Assert.Equal(
            PipelineStageKind.Cleanup,
            Assert.Single(result.StageResults, s => s.StageName == "cleanup").Kind);
    }

    [Fact]
    public async Task ExecuteAsync_EmitsStageTelemetryTimestampsAndDurations()
    {
        var executor = new PipelineExecutor();

        var stages = new[]
        {
            new PipelineStageDefinition
            {
                Name = "stage-1",
                ExecuteAsync = async (_, token) => await Task.Delay(20, token)
            }
        };

        var result = await executor.ExecuteAsync(
            runId: "run-telemetry",
            stages,
            timeout: TimeSpan.FromSeconds(2));

        Assert.Equal(PipelineRunStatus.Succeeded, result.Status);
        Assert.All(result.Events, e => Assert.NotEqual(default, e.TimestampUtc));
        Assert.All(result.StageResults.Where(s => s.Status != PipelineStageStatus.Skipped), s => Assert.NotNull(s.StartedUtc));
        Assert.All(result.StageResults.Where(s => s.Status != PipelineStageStatus.Skipped), s => Assert.NotNull(s.CompletedUtc));

        var started = Assert.Single(result.Events, e => e.Kind == PipelineEventKind.StageStarted);
        Assert.Null(started.Duration);

        var completed = Assert.Single(result.Events, e => e.Kind == PipelineEventKind.StageCompleted);
        Assert.NotNull(completed.Duration);
        Assert.True(completed.Duration >= TimeSpan.Zero);

        var stage = Assert.Single(result.StageResults);
        Assert.Equal(PipelineStageStatus.Succeeded, stage.Status);
        Assert.NotNull(stage.Duration);
        Assert.True(stage.Duration >= TimeSpan.Zero);
    }
}
