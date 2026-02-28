using System.Runtime.InteropServices;
using DoclingDotNet.Models;
using DoclingDotNet.Native;
using DoclingDotNet.Serialization;

namespace DoclingDotNet.Parsing;

public sealed class DoclingParseSession : IDoclingParseSession
{
    private IntPtr _handle;

    private DoclingParseSession(IntPtr handle)
    {
        _handle = handle;
    }

    public static DoclingParseSession Create(string logLevel = "error")
    {
        var status = DoclingParseNative.docling_parse_create(logLevel, out var handle);
        if (status != DoclingParseNative.Ok || handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"docling_parse_create failed with status {status}.");
        }

        var session = new DoclingParseSession(handle);

        // Automatically configure the resources directory if it exists near the executing assembly.
        var appDir = AppContext.BaseDirectory;
        var defaultResourcesPath = Path.Combine(appDir, "pdf_resources");
        if (Directory.Exists(defaultResourcesPath))
        {
            try
            {
                // Best-effort auto-configuration; failures are ignored so callers can still
                // explicitly configure resources via SetResourcesDir if needed.
                session.SetResourcesDir(defaultResourcesPath);
            }
            catch (InvalidOperationException)
            {
                // Ignore invalid/incomplete default resources; leave session usable.
            }
        }

        return session;
    }

    public void SetLogLevel(string logLevel)
    {
        EnsureNotDisposed();
        EnsureStatus(
            DoclingParseNative.docling_parse_set_loglevel(_handle, logLevel),
            nameof(SetLogLevel));
    }

    public void SetResourcesDir(string resourcesDir)
    {
        EnsureNotDisposed();
        EnsureStatus(
            DoclingParseNative.docling_parse_set_resources_dir(_handle, resourcesDir),
            nameof(SetResourcesDir));
    }

    public void LoadDocument(string key, string filePath, string? password = null)
    {
        EnsureNotDisposed();
        EnsureStatus(
            DoclingParseNative.docling_parse_load_document(_handle, key, filePath, password),
            nameof(LoadDocument));
    }

    public void LoadDocumentFromBytes(
        string key,
        byte[] bytes,
        string? description = null,
        string? password = null)
    {
        EnsureNotDisposed();
        EnsureStatus(
            DoclingParseNative.docling_parse_load_document_from_bytes(
                _handle,
                key,
                bytes,
                (nuint)bytes.Length,
                description,
                password),
            nameof(LoadDocumentFromBytes));
    }

    public void UnloadDocument(string key)
    {
        EnsureNotDisposed();
        EnsureStatus(
            DoclingParseNative.docling_parse_unload_document(_handle, key),
            nameof(UnloadDocument));
    }

    public void UnloadDocuments()
    {
        EnsureNotDisposed();
        EnsureStatus(
            DoclingParseNative.docling_parse_unload_documents(_handle),
            nameof(UnloadDocuments));
    }

    public int GetPageCount(string key)
    {
        EnsureNotDisposed();

        var status = DoclingParseNative.docling_parse_number_of_pages(_handle, key, out var pages);
        EnsureStatus(status, nameof(GetPageCount));
        return pages;
    }

    public string GetAnnotationsJson(string key)
    {
        EnsureNotDisposed();

        var status = DoclingParseNative.docling_parse_get_annotations_json(
            _handle,
            key,
            out var jsonPtr);
        EnsureStatus(status, nameof(GetAnnotationsJson));
        return ReadAndFreeUtf8(jsonPtr);
    }

    public string DecodePageJson(string key, int pageNumber)
    {
        return DecodePageJsonInternal(key, pageNumber, config: null);
    }

    public NativeDecodedPageDto DecodePage(string key, int pageNumber)
    {
        return DoclingJson.DeserializeNativeDecodedPage(DecodePageJson(key, pageNumber));
    }

    public SegmentedPdfPageDto DecodePageAsSegmented(string key, int pageNumber)
    {
        return DecodeSegmentedPage(key, pageNumber);
    }

    public string DecodeSegmentedPageJson(string key, int pageNumber)
    {
        return DecodeSegmentedPageJsonInternal(key, pageNumber, config: null);
    }

    public SegmentedPdfPageDto DecodeSegmentedPage(string key, int pageNumber)
    {
        return DoclingJson.DeserializeSegmentedPage(DecodeSegmentedPageJson(key, pageNumber));
    }

    private string DecodeSegmentedPageJsonInternal(
        string key,
        int pageNumber,
        DoclingParseNative.DecodePageConfig? config)
    {
        EnsureNotDisposed();

        var configValue = config ?? CreateDefaultSegmentedDecodePageConfig();
        var status = DoclingParseNative.docling_parse_decode_segmented_page_json(
            _handle,
            key,
            pageNumber,
            ref configValue,
            out var jsonPtr);

        EnsureStatus(status, nameof(DecodeSegmentedPageJson));
        return ReadAndFreeUtf8(jsonPtr);
    }

    private string DecodePageJsonInternal(
        string key,
        int pageNumber,
        DoclingParseNative.DecodePageConfig? config)
    {
        EnsureNotDisposed();

        var configValue = config ?? CreateDefaultDecodePageConfig();

        var status = DoclingParseNative.docling_parse_decode_page_json(
            _handle,
            key,
            pageNumber,
            ref configValue,
            out var jsonPtr);
        EnsureStatus(status, nameof(DecodePageJson));
        return ReadAndFreeUtf8(jsonPtr);
    }

    public string GetTableOfContentsJson(string key)
    {
        EnsureNotDisposed();

        var status = DoclingParseNative.docling_parse_get_table_of_contents_json(
            _handle,
            key,
            out var jsonPtr);
        EnsureStatus(status, nameof(GetTableOfContentsJson));
        return ReadAndFreeUtf8(jsonPtr);
    }

    public string GetMetaXmlJson(string key)
    {
        EnsureNotDisposed();

        var status = DoclingParseNative.docling_parse_get_meta_xml_json(
            _handle,
            key,
            out var jsonPtr);
        EnsureStatus(status, nameof(GetMetaXmlJson));
        return ReadAndFreeUtf8(jsonPtr);
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            DoclingParseNative.docling_parse_destroy(_handle);
            _handle = IntPtr.Zero;
        }
    }

    private static DoclingParseNative.DecodePageConfig CreateDefaultDecodePageConfig()
    {
        var configSize = DoclingParseNative.docling_parse_get_decode_page_config_size();
        var status = DoclingParseNative.docling_parse_init_decode_page_config(
            out var config,
            configSize);

        if (status != DoclingParseNative.Ok)
        {
            throw new InvalidOperationException(
                $"docling_parse_init_decode_page_config failed with status {status}.");
        }

        return config;
    }

    private static DoclingParseNative.DecodePageConfig CreateDefaultSegmentedDecodePageConfig()
    {
        var config = CreateDefaultDecodePageConfig();
        config.do_sanitization = 0;
        config.create_word_cells = 1;
        config.create_line_cells = 1;
        return config;
    }

    private void EnsureStatus(int status, string operation)
    {
        if (status == DoclingParseNative.Ok)
        {
            return;
        }

        var messagePtr = DoclingParseNative.docling_parse_get_last_error(_handle);
        var message = messagePtr == IntPtr.Zero
            ? string.Empty
            : Marshal.PtrToStringUTF8(messagePtr);

        throw new InvalidOperationException(
            $"{operation} failed with status {status}: {message}");
    }

    private static string ReadAndFreeUtf8(IntPtr value)
    {
        try
        {
            return value == IntPtr.Zero
                ? string.Empty
                : Marshal.PtrToStringUTF8(value) ?? string.Empty;
        }
        finally
        {
            if (value != IntPtr.Zero)
            {
                DoclingParseNative.docling_parse_free_string(value);
            }
        }
    }

    private void EnsureNotDisposed()
    {
        if (_handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(DoclingParseSession));
        }
    }
}
