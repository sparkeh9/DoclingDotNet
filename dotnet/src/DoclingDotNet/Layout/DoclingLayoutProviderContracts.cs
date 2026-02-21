using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoclingDotNet.Algorithms.Layout;
using DoclingDotNet.Models;

namespace DoclingDotNet.Layout;

public sealed class LayoutProviderCapabilities
{
    public bool IsTrusted { get; init; } = true;
    public bool IsExternal { get; init; }
}

public sealed class LayoutProcessRequest
{
    public required string RunId { get; init; }
    public required string DocumentKey { get; init; }
    public required string FilePath { get; init; }
    public required IReadOnlyList<SegmentedPdfPageDto> Pages { get; init; }
}

public enum LayoutProcessStatus
{
    Succeeded = 0,
    NoChanges = 1,
    RecoverableFailure = 2,
    FatalFailure = 3
}

public sealed class LayoutProcessResult
{
    public required LayoutProcessStatus Status { get; init; }
    public IReadOnlyDictionary<int, IReadOnlyList<LayoutCluster>>? PageClusters { get; init; }
    public string? ErrorType { get; init; }
    public string? Message { get; init; }

    public static LayoutProcessResult Succeeded(
        IReadOnlyDictionary<int, IReadOnlyList<LayoutCluster>>? pageClusters = null,
        string? message = null)
    {
        return new LayoutProcessResult
        {
            Status = LayoutProcessStatus.Succeeded,
            PageClusters = pageClusters,
            Message = message
        };
    }

    public static LayoutProcessResult NoChanges(string? message = null)
    {
        return new LayoutProcessResult
        {
            Status = LayoutProcessStatus.NoChanges,
            Message = message
        };
    }

    public static LayoutProcessResult RecoverableFailure(string errorType, string message)
    {
        return new LayoutProcessResult
        {
            Status = LayoutProcessStatus.RecoverableFailure,
            ErrorType = errorType,
            Message = message
        };
    }

    public static LayoutProcessResult FatalFailure(string errorType, string message)
    {
        return new LayoutProcessResult
        {
            Status = LayoutProcessStatus.FatalFailure,
            ErrorType = errorType,
            Message = message
        };
    }
}

public interface IDoclingLayoutProvider
{
    string Name { get; }
    int Priority { get; }
    LayoutProviderCapabilities Capabilities { get; }
    bool IsAvailable();
    Task<LayoutProcessResult> ProcessAsync(
        LayoutProcessRequest request,
        CancellationToken cancellationToken = default);
}