using System.Runtime.InteropServices;

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

var repoRoot = FindRepositoryRoot();

var pdfPath = Path.GetFullPath(Path.Combine(
    repoRoot,
    "upstream",
    "deps",
    "docling-parse",
    "tests",
    "data",
    "regression",
    "font_01.pdf"));

Console.WriteLine("Spike.Pdfium: validating native PDFium load...");
Console.WriteLine($"Runtime: {Environment.Version} | OS: {Environment.OSVersion} | Arch: {RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"Sample PDF: {pdfPath}");

if (!File.Exists(pdfPath))
{
    Console.WriteLine("Sample PDF was not found.");
    Environment.ExitCode = 1;
    return;
}

NativeMethods.FPDF_InitLibrary();
var doc = IntPtr.Zero;

try
{
    doc = NativeMethods.FPDF_LoadDocument(pdfPath, null);
    if (doc == IntPtr.Zero)
    {
        var error = NativeMethods.FPDF_GetLastError();
        Console.WriteLine($"PDFium open failed. Error code: {error}");
        Environment.ExitCode = 1;
        return;
    }

    var pageCount = NativeMethods.FPDF_GetPageCount(doc);
    Console.WriteLine("PDFium native load: OK");
    Console.WriteLine($"Page count: {pageCount}");
}
catch (DllNotFoundException ex)
{
    Console.WriteLine("PDFium native load: FAILED (dll not found)");
    Console.WriteLine(ex.ToString());
    Environment.ExitCode = 1;
}
finally
{
    if (doc != IntPtr.Zero)
    {
        NativeMethods.FPDF_CloseDocument(doc);
    }

    NativeMethods.FPDF_DestroyLibrary();
}

internal static partial class NativeMethods
{
    private const string PdfiumDll = "pdfium_x64.dll";

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDF_InitLibrary();

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDF_DestroyLibrary();

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr FPDF_LoadDocument(string file_path, string? password);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FPDF_GetPageCount(IntPtr document);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint FPDF_GetLastError();

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FPDF_CloseDocument(IntPtr document);
}
