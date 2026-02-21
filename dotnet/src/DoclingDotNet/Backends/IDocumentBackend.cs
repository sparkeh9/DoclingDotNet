using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DoclingDotNet.Models;

namespace DoclingDotNet.Backends;

public sealed class DocumentBackendCapabilities
{
    public bool SupportsPagination { get; init; }
}

public interface IDocumentBackend
{
    string Name { get; }
    IReadOnlyList<string> SupportedExtensions { get; }
    DocumentBackendCapabilities Capabilities { get; }
    
    Task<IReadOnlyList<SegmentedPdfPageDto>> ConvertAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}