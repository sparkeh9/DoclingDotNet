using System;
using System.Runtime.InteropServices;

namespace DoclingDotNet.Layout;

internal static partial class PdfiumNative
{
    private const string PdfiumDll = "pdfium";

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDF_InitLibrary();

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDF_DestroyLibrary();

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr FPDF_LoadDocument(string file_path, string? password);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDF_CloseDocument(IntPtr document);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDF_GetPageCount(IntPtr document);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr FPDF_LoadPage(IntPtr document, int page_index);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDF_ClosePage(IntPtr page);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern double FPDF_GetPageWidth(IntPtr page);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern double FPDF_GetPageHeight(IntPtr page);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr first_scan, int stride);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int start_x, int start_y, int size_x, int size_y, int rotate, int flags);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDFBitmap_Destroy(IntPtr bitmap);
}
