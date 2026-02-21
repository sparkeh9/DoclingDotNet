using System.Runtime.InteropServices;

namespace Spike.DoclingParseCAbi;

internal static partial class NativeDoclingParse
{
    internal const int Ok = 0;
    internal const int ExpectedAbiMajor = 1;

    [StructLayout(LayoutKind.Sequential)]
    internal struct DecodePageConfig
    {
        public nint PageBoundary;
        public int DoSanitization;
        public int KeepCharCells;
        public int KeepShapes;
        public int KeepBitmaps;
        public int MaxNumLines;
        public int MaxNumBitmaps;
        public int CreateWordCells;
        public int CreateLineCells;
        public int EnforceSameFont;
        public double HorizontalCellTolerance;
        public double WordSpaceWidthFactorForMerge;
        public double LineSpaceWidthFactorForMerge;
        public double LineSpaceWidthFactorForMergeWithSpace;
        public int PopulateJsonObjects;
    }

    [LibraryImport("docling_parse_c", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int docling_parse_create(string? logLevel, out nint handle);

    [LibraryImport("docling_parse_c")]
    internal static partial int docling_parse_get_abi_version(
        out int major,
        out int minor,
        out int patch);

    [LibraryImport("docling_parse_c")]
    internal static partial nuint docling_parse_get_decode_page_config_size();

    [LibraryImport("docling_parse_c")]
    internal static partial int docling_parse_init_decode_page_config(
        out DecodePageConfig outConfig,
        nuint configSize);

    [LibraryImport("docling_parse_c")]
    internal static partial void docling_parse_destroy(nint handle);

    [LibraryImport("docling_parse_c")]
    internal static partial void docling_parse_get_default_decode_page_config(
        ref DecodePageConfig outConfig);

    [LibraryImport("docling_parse_c", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int docling_parse_set_resources_dir(nint handle, string resourcesDir);

    [LibraryImport("docling_parse_c", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int docling_parse_load_document(
        nint handle,
        string key,
        string filename,
        string? passwordOrNull);

    [LibraryImport("docling_parse_c", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int docling_parse_number_of_pages(
        nint handle,
        string key,
        out int pageCount);

    [LibraryImport("docling_parse_c", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int docling_parse_decode_page_json(
        nint handle,
        string key,
        int pageNumber,
        ref DecodePageConfig configOrNull,
        out nint outJsonUtf8);

    [LibraryImport("docling_parse_c", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int docling_parse_decode_segmented_page_json(
        nint handle,
        string key,
        int pageNumber,
        ref DecodePageConfig configOrNull,
        out nint outJsonUtf8);

    [LibraryImport("docling_parse_c")]
    internal static partial void docling_parse_free_string(nint value);

    [LibraryImport("docling_parse_c")]
    internal static partial nint docling_parse_get_last_error(nint handle);
}
