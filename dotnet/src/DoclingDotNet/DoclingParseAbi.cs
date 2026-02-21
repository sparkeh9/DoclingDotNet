using System.Runtime.InteropServices;

namespace DoclingDotNet
{
    public static class DoclingParseAbi
    {
        public const int ExpectedAbiMajor = 1;

        public static (int Major, int Minor, int Patch) GetAbiVersion()
        {
            var status = Native.DoclingParseNative.docling_parse_get_abi_version(
                out var major,
                out var minor,
                out var patch);

            if (status != Native.DoclingParseNative.Ok)
            {
                throw new InvalidOperationException(
                    $"docling_parse_get_abi_version failed with status {status}.");
            }

            return (major, minor, patch);
        }

        public static void EnsureCompatibleMajor(int expectedMajor = ExpectedAbiMajor)
        {
            var (major, minor, patch) = GetAbiVersion();
            if (major != expectedMajor)
            {
                throw new InvalidOperationException(
                    $"Unsupported docling_parse_c ABI version {major}.{minor}.{patch}. Expected major {expectedMajor}.");
            }
        }

        public static nuint GetDecodePageConfigSize()
        {
            return Native.DoclingParseNative.docling_parse_get_decode_page_config_size();
        }
    }
}

namespace DoclingDotNet.Native
{
    internal static class DoclingParseNative
    {
        private const string LibraryName = "docling_parse_c";

        internal const int Ok = 0;
        internal const int InvalidArgument = 1;
        internal const int NotFound = 2;
        internal const int OperationFailed = 3;
        internal const int InternalError = 4;

        [StructLayout(LayoutKind.Sequential)]
        internal struct DecodePageConfig
        {
            public nint page_boundary;
            public int do_sanitization;
            public int keep_char_cells;
            public int keep_shapes;
            public int keep_bitmaps;
            public int max_num_lines;
            public int max_num_bitmaps;
            public int create_word_cells;
            public int create_line_cells;
            public int enforce_same_font;
            public double horizontal_cell_tolerance;
            public double word_space_width_factor_for_merge;
            public double line_space_width_factor_for_merge;
            public double line_space_width_factor_for_merge_with_space;
            public int populate_json_objects;
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_get_abi_version(
            out int major,
            out int minor,
            out int patch);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern nuint docling_parse_get_decode_page_config_size();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_init_decode_page_config(
            out DecodePageConfig out_config,
            nuint config_size);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void docling_parse_get_default_decode_page_config(
            out DecodePageConfig out_config);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_create(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string log_level,
            out IntPtr out_handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void docling_parse_destroy(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_set_loglevel(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string log_level);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_set_resources_dir(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string resources_dir);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_load_document(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string filename,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? password_or_null);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_load_document_from_bytes(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
            [In] byte[] bytes,
            nuint length,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? description_or_null,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? password_or_null);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_unload_document(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string key);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_unload_documents(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_number_of_pages(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
            out int out_page_count);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_get_annotations_json(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
            out IntPtr out_json_utf8);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_get_table_of_contents_json(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
            out IntPtr out_json_utf8);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_get_meta_xml_json(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
            out IntPtr out_json_utf8);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_decode_page_json(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
            int page_number,
            ref DecodePageConfig config_or_null,
            out IntPtr out_json_utf8);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int docling_parse_decode_segmented_page_json(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
            int page_number,
            ref DecodePageConfig config_or_null,
            out IntPtr out_json_utf8);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void docling_parse_free_string(IntPtr value);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr docling_parse_get_last_error(IntPtr handle);
    }
}
