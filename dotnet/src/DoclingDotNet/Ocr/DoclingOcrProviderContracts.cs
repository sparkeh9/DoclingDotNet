using DoclingDotNet.Models;

namespace DoclingDotNet.Ocr;

public sealed class OcrProviderCapabilities
{
    public bool SupportsLanguageSelection { get; init; }

    public bool SupportsIncrementalPageProcessing { get; init; }

    public bool IsTrusted { get; init; } = true;

    public bool IsExternal { get; init; }
}

public sealed class OcrProcessRequest
{
    public required string RunId { get; init; }

    public required string DocumentKey { get; init; }

    public required string FilePath { get; init; }

    public string? Language { get; init; }

    public required IReadOnlyList<SegmentedPdfPageDto> Pages { get; init; }
}

public enum OcrProcessStatus
{
    Succeeded = 0,
    NoChanges = 1,
    RecoverableFailure = 2,
    FatalFailure = 3
}

public sealed class OcrProcessResult
{
    public required OcrProcessStatus Status { get; init; }

    public IReadOnlyList<SegmentedPdfPageDto>? Pages { get; init; }

    public string? ErrorType { get; init; }

    public string? Message { get; init; }

    public static OcrProcessResult Succeeded(
        IReadOnlyList<SegmentedPdfPageDto>? pages = null,
        string? message = null)
    {
        return new OcrProcessResult
        {
            Status = OcrProcessStatus.Succeeded,
            Pages = pages,
            Message = message
        };
    }

    public static OcrProcessResult NoChanges(string? message = null)
    {
        return new OcrProcessResult
        {
            Status = OcrProcessStatus.NoChanges,
            Message = message
        };
    }

    public static OcrProcessResult RecoverableFailure(
        string errorType,
        string message)
    {
        return new OcrProcessResult
        {
            Status = OcrProcessStatus.RecoverableFailure,
            ErrorType = errorType,
            Message = message
        };
    }

    public static OcrProcessResult FatalFailure(
        string errorType,
        string message)
    {
        return new OcrProcessResult
        {
            Status = OcrProcessStatus.FatalFailure,
            ErrorType = errorType,
            Message = message
        };
    }
}

public interface IDoclingOcrProvider
{
    string Name { get; }

    int Priority { get; }

    OcrProviderCapabilities Capabilities { get; }

    bool IsAvailable();

    Task<OcrProcessResult> ProcessAsync(
        OcrProcessRequest request,
        CancellationToken cancellationToken = default);
}
