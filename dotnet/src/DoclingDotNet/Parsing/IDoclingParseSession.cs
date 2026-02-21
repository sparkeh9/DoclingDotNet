using DoclingDotNet.Models;

namespace DoclingDotNet.Parsing;

public interface IDoclingParseSession : IDisposable
{
    void SetResourcesDir(string resourcesDir);

    void LoadDocument(string key, string filePath, string? password = null);

    void LoadDocumentFromBytes(string key, byte[] bytes, string? description = null, string? password = null);

    void UnloadDocument(string key);

    int GetPageCount(string key);

    SegmentedPdfPageDto DecodeSegmentedPage(string key, int pageNumber);

    string GetAnnotationsJson(string key);

    string GetTableOfContentsJson(string key);

    string GetMetaXmlJson(string key);
}
