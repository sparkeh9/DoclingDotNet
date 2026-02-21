using System.Runtime.InteropServices;
using System.Text.Json;
using Spike.DoclingParseCAbi;

Console.WriteLine("Spike.DoclingParseCAbi: validating C ABI contract for .NET P/Invoke...");

var repoRoot = FindRepositoryRoot();

var resourcesDir = Path.GetFullPath(Path.Combine(
    repoRoot,
    "upstream",
    "deps",
    "docling-parse",
    "docling_parse",
    "pdf_resources"));

var samplePdf = Path.GetFullPath(Path.Combine(
    repoRoot,
    "upstream",
    "deps",
    "docling-parse",
    "tests",
    "data",
    "regression",
    "font_01.pdf"));

var groundTruthPageJson = Path.GetFullPath(Path.Combine(
    repoRoot,
    "upstream",
    "deps",
    "docling-parse",
    "tests",
    "data",
    "groundtruth",
    "font_01.pdf.page_no_1.py.json"));

Console.WriteLine($"Resources dir: {resourcesDir}");
Console.WriteLine($"Sample PDF: {samplePdf}");
Console.WriteLine($"Ground truth JSON: {groundTruthPageJson}");

nint handle = nint.Zero;

try
{
    EnsureOkNoHandle(
        NativeDoclingParse.docling_parse_get_abi_version(out var abiMajor, out var abiMinor, out var abiPatch),
        "get_abi_version");
    Console.WriteLine($"C ABI version: {abiMajor}.{abiMinor}.{abiPatch}");
    if (abiMajor != NativeDoclingParse.ExpectedAbiMajor)
    {
        throw new InvalidOperationException(
            $"Unsupported C ABI major version {abiMajor}. Expected {NativeDoclingParse.ExpectedAbiMajor}.");
    }

    var nativeConfigSize = NativeDoclingParse.docling_parse_get_decode_page_config_size();
    var managedConfigSize = (nuint)Marshal.SizeOf<NativeDoclingParse.DecodePageConfig>();
    if (nativeConfigSize != managedConfigSize)
    {
        throw new InvalidOperationException(
            $"DecodePageConfig size mismatch. Native={nativeConfigSize}, Managed={managedConfigSize}.");
    }

    var createStatus = NativeDoclingParse.docling_parse_create("warning", out handle);
    if (createStatus != NativeDoclingParse.Ok)
    {
        Console.WriteLine($"create failed with status {createStatus}");
        Environment.ExitCode = 1;
        return;
    }

    EnsureOk(NativeDoclingParse.docling_parse_set_resources_dir(handle, resourcesDir), handle, "set_resources_dir");
    EnsureOk(NativeDoclingParse.docling_parse_load_document(handle, "sample", samplePdf, null), handle, "load_document");

    EnsureOk(NativeDoclingParse.docling_parse_number_of_pages(handle, "sample", out var pageCount), handle, "number_of_pages");
    Console.WriteLine($"Page count via C ABI: {pageCount}");

    EnsureOk(
        NativeDoclingParse.docling_parse_init_decode_page_config(out var config, nativeConfigSize),
        handle,
        "init_decode_page_config");

    EnsureOk(
        NativeDoclingParse.docling_parse_decode_page_json(handle, "sample", 0, ref config, out var jsonPtr),
        handle,
        "decode_page_json");

    try
    {
        var json = Marshal.PtrToStringUTF8(jsonPtr) ?? string.Empty;
        Console.WriteLine($"Decoded page JSON length: {json.Length}");
    }
    finally
    {
        NativeDoclingParse.docling_parse_free_string(jsonPtr);
    }

    config.DoSanitization = 0;

    EnsureOk(
        NativeDoclingParse.docling_parse_decode_segmented_page_json(handle, "sample", 0, ref config, out var segmentedPtr),
        handle,
        "decode_segmented_page_json");

    try
    {
        var segmentedJson = Marshal.PtrToStringUTF8(segmentedPtr) ?? string.Empty;
        Console.WriteLine($"Segmented page JSON length: {segmentedJson.Length}");

        AssertSegmentedParity(segmentedJson, File.ReadAllText(groundTruthPageJson));
        Console.WriteLine("Segmented parity check: OK");
    }
    finally
    {
        NativeDoclingParse.docling_parse_free_string(segmentedPtr);
    }

    Console.WriteLine("C ABI spike: OK");
}
catch (DllNotFoundException ex)
{
    Console.WriteLine("Native library 'docling_parse_c' not found.");
    Console.WriteLine("Build docling-parse C API first, then place the resulting DLL on PATH or beside this executable.");
    Console.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}
finally
{
    if (handle != nint.Zero)
    {
        NativeDoclingParse.docling_parse_destroy(handle);
    }
}

static void EnsureOkNoHandle(int status, string operation)
{
    if (status == NativeDoclingParse.Ok)
    {
        return;
    }

    throw new InvalidOperationException($"{operation} failed with status {status}");
}

static void EnsureOk(int status, nint handle, string operation)
{
    if (status == NativeDoclingParse.Ok)
    {
        return;
    }

    var errPtr = NativeDoclingParse.docling_parse_get_last_error(handle);
    var err = errPtr == nint.Zero ? string.Empty : Marshal.PtrToStringUTF8(errPtr);
    throw new InvalidOperationException($"{operation} failed with status {status}: {err}");
}

static void AssertSegmentedParity(string actualJson, string expectedJson)
{
    using var actualDoc = JsonDocument.Parse(actualJson);
    using var expectedDoc = JsonDocument.Parse(expectedJson);

    var requiredKeys = new[]
    {
        "dimension",
        "bitmap_resources",
        "char_cells",
        "word_cells",
        "textline_cells",
        "has_chars",
        "has_words",
        "has_lines",
        "widgets",
        "hyperlinks",
        "lines",
        "shapes"
    };

    foreach (var key in requiredKeys)
    {
        if (!actualDoc.RootElement.TryGetProperty(key, out _))
        {
            throw new InvalidOperationException($"Segmented payload missing key: {key}");
        }
    }

    AssertCount(actualDoc.RootElement, expectedDoc.RootElement, "char_cells");
    AssertCount(actualDoc.RootElement, expectedDoc.RootElement, "word_cells");
    AssertCount(actualDoc.RootElement, expectedDoc.RootElement, "textline_cells");
    AssertCount(actualDoc.RootElement, expectedDoc.RootElement, "shapes");
    AssertCount(actualDoc.RootElement, expectedDoc.RootElement, "widgets");
    AssertCount(actualDoc.RootElement, expectedDoc.RootElement, "hyperlinks");

    AssertBool(actualDoc.RootElement, expectedDoc.RootElement, "has_chars");
    AssertBool(actualDoc.RootElement, expectedDoc.RootElement, "has_words");
    AssertBool(actualDoc.RootElement, expectedDoc.RootElement, "has_lines");
}

static void AssertCount(JsonElement actualRoot, JsonElement expectedRoot, string property)
{
    var actualCount = actualRoot.GetProperty(property).GetArrayLength();
    var expectedCount = expectedRoot.GetProperty(property).GetArrayLength();
    if (actualCount != expectedCount)
    {
        throw new InvalidOperationException(
            $"Segmented parity mismatch for '{property}': actual={actualCount}, expected={expectedCount}.");
    }
}

static void AssertBool(JsonElement actualRoot, JsonElement expectedRoot, string property)
{
    var actual = actualRoot.GetProperty(property).GetBoolean();
    var expected = expectedRoot.GetProperty(property).GetBoolean();
    if (actual != expected)
    {
        throw new InvalidOperationException(
            $"Segmented parity mismatch for '{property}': actual={actual}, expected={expected}.");
    }
}

static string FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (Directory.Exists(Path.Combine(current.FullName, "upstream")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate repository root containing 'upstream' folder.");
}

