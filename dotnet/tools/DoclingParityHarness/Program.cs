using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DoclingDotNet;
using DoclingDotNet.Parsing;
using DoclingDotNet.Parity;

var options = HarnessOptions.Parse(args);

// ── Extract-only mode (no ground truth comparison) ──
if (!string.IsNullOrEmpty(options.ExtractPdfPath))
{
    DoclingParseAbi.EnsureCompatibleMajor();
    var extractRepoRoot = FindRepositoryRoot();
    options.ResolveDefaults(extractRepoRoot);

    using var extractSession = DoclingParseSession.Create(logLevel: "error");
    extractSession.SetResourcesDir(options.ResourcesDir);

    var extractBasename = Path.GetFileNameWithoutExtension(options.ExtractPdfPath);
    var docKey = $"extract_{extractBasename}";

    extractSession.LoadDocument(docKey, options.ExtractPdfPath);
    var pageCount = extractSession.GetPageCount(docKey);

    var textOutput = new System.Text.StringBuilder();
    var pageStats = new List<object>();
    var colorCounts = new Dictionary<string, int>();
    var sw = Stopwatch.StartNew();

    for (int p = 0; p < pageCount; p++)
    {
        var json = extractSession.DecodeSegmentedPageJson(docKey, p);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var nc = root.TryGetProperty("char_cells", out var cc) ? cc.GetArrayLength() : 0;
        var nw = root.TryGetProperty("word_cells", out var wc) ? wc.GetArrayLength() : 0;
        var nl = root.TryGetProperty("textline_cells", out var tc) ? tc.GetArrayLength() : 0;

        // Extract text from textline_cells
        textOutput.AppendLine($"--- Page {p + 1} ---");
        if (root.TryGetProperty("textline_cells", out var textlines))
        {
            foreach (var cell in textlines.EnumerateArray())
            {
                if (cell.TryGetProperty("text", out var textProp))
                    textOutput.AppendLine(textProp.GetString()?.TrimEnd());
            }
        }
        textOutput.AppendLine();

        // Accumulate colors from char_cells
        if (root.TryGetProperty("char_cells", out var charCells))
        {
            foreach (var cell in charCells.EnumerateArray())
            {
                if (cell.TryGetProperty("rgba", out var rgba))
                {
                    var r = rgba.GetProperty("r").GetInt32();
                    var g = rgba.GetProperty("g").GetInt32();
                    var b = rgba.GetProperty("b").GetInt32();
                    var a = rgba.GetProperty("a").GetInt32();
                    var key = $"rgba({r},{g},{b},{a})";
                    colorCounts[key] = colorCounts.GetValueOrDefault(key) + 1;
                }
            }
        }

        pageStats.Add(new { page = p + 1, chars = nc, words = nw, lines = nl });
    }

    sw.Stop();
    extractSession.UnloadDocument(docKey);

    // Write text file
    var textFilePath = options.ExtractOutputDir != null
        ? Path.Combine(options.ExtractOutputDir, $"{extractBasename}.dotnet.md")
        : $"{extractBasename}.dotnet.md";
    Directory.CreateDirectory(Path.GetDirectoryName(textFilePath) ?? ".");
    File.WriteAllText(textFilePath, textOutput.ToString(), new System.Text.UTF8Encoding(false));

    // Output JSON stats to stdout
    var stats = new
    {
        pages = pageCount,
        timeMs = sw.ElapsedMilliseconds,
        perPage = pageStats,
        colors = colorCounts,
        textFile = textFilePath
    };
    // Write stats JSON file
    var statsFilePath = Path.Combine(
        Path.GetDirectoryName(textFilePath) ?? ".",
        $"{extractBasename}.dotnet.stats.json");
    File.WriteAllText(statsFilePath, JsonSerializer.Serialize(stats), new System.Text.UTF8Encoding(false));
    return;
}

var repoRoot = FindRepositoryRoot();
options.ResolveDefaults(repoRoot);

Console.WriteLine($"[parity] repo root: {repoRoot}");
Console.WriteLine($"[parity] resources dir: {options.ResourcesDir}");
Console.WriteLine($"[parity] regression dir: {options.RegressionDir}");
Console.WriteLine($"[parity] groundtruth dir: {options.GroundTruthDir}");
Console.WriteLine($"[parity] output: {options.OutputPath}");

if (!Directory.Exists(options.ResourcesDir))
{
    throw new DirectoryNotFoundException($"Resources directory not found: {options.ResourcesDir}");
}

if (!Directory.Exists(options.RegressionDir))
{
    throw new DirectoryNotFoundException($"Regression directory not found: {options.RegressionDir}");
}

if (!Directory.Exists(options.GroundTruthDir))
{
    throw new DirectoryNotFoundException($"Groundtruth directory not found: {options.GroundTruthDir}");
}

var groundTruthPages = DiscoverGroundTruthPages(options.GroundTruthDir, options.MaxDocuments, options.MaxPages);
if (groundTruthPages.Count == 0)
{
    throw new InvalidOperationException("No groundtruth files matched '*.pdf.page_no_*.py.json'.");
}

DoclingParseAbi.EnsureCompatibleMajor();
var abiVersion = DoclingParseAbi.GetAbiVersion();

var summary = new ParitySummary();
var documentResults = new List<ParityDocumentResult>();

using var session = DoclingParseSession.Create(logLevel: "error");
session.SetResourcesDir(options.ResourcesDir);

foreach (var documentGroup in groundTruthPages
             .GroupBy(static page => page.PdfFileName, StringComparer.OrdinalIgnoreCase)
             .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase))
{
    var pdfPath = Path.Combine(options.RegressionDir, documentGroup.Key);
    var docResult = new ParityDocumentResult
    {
        PdfFileName = documentGroup.Key,
        PdfPath = pdfPath
    };

    if (!File.Exists(pdfPath))
    {
        foreach (var page in documentGroup.OrderBy(static p => p.PageNumber))
        {
            var pageResult = CreateCriticalPageResult(
                page,
                "regression_pdf_missing",
                $"Regression PDF not found: {pdfPath}");
            docResult.Pages.Add(pageResult);
            summary.Accumulate(pageResult);
        }

        documentResults.Add(docResult);
        continue;
    }

    var documentKey = CreateDocumentKey(documentGroup.Key);

    try
    {
        session.LoadDocument(documentKey, pdfPath);
        docResult.NativePageCount = session.GetPageCount(documentKey);

        foreach (var page in documentGroup.OrderBy(static p => p.PageNumber))
        {
            var pageResult = ComparePage(session, documentKey, page, docResult.NativePageCount, options);
            docResult.Pages.Add(pageResult);
            summary.Accumulate(pageResult);
        }
    }
    catch (Exception ex)
    {
        foreach (var page in documentGroup.OrderBy(static p => p.PageNumber))
        {
            var pageResult = CreateCriticalPageResult(
                page,
                "document_processing_failed",
                $"Document processing failed: {ex.Message}");
            docResult.Pages.Add(pageResult);
            summary.Accumulate(pageResult);
        }
    }
    finally
    {
        TryUnloadDocument(session, documentKey);
    }

    documentResults.Add(docResult);
}

summary.DocumentsTotal = documentResults.Count;
summary.PagesTotal = documentResults.Sum(static doc => doc.Pages.Count);
summary.PagesPassed = documentResults.Sum(static doc => doc.Pages.Count(static page => page.IsPass));
summary.PagesFailed = summary.PagesTotal - summary.PagesPassed;

var report = new ParityReport
{
    GeneratedAtUtc = DateTime.UtcNow,
    AbiVersion = $"{abiVersion.Major}.{abiVersion.Minor}.{abiVersion.Patch}",
    Thresholds = new ParityThresholds
    {
        MaxCritical = options.MaxCritical,
        MaxMajor = options.MaxMajor,
        MaxMinor = options.MaxMinor,
        MaxOcrDrift = options.MaxOcrDrift,
        TextMismatchSeverity = options.TextMismatchSeverity.ToString(),
        GeometryMismatchSeverity = options.GeometryMismatchSeverity.ToString(),
        ContextMismatchSeverity = options.ContextMismatchSeverity.ToString()
    },
    Corpus = new ParityCorpus
    {
        GroundTruthDir = options.GroundTruthDir,
        RegressionDir = options.RegressionDir,
        ResourcesDir = options.ResourcesDir,
        GroundTruthFiles = groundTruthPages.Count
    },
    Summary = summary,
    Documents = documentResults
};

WriteReport(options.OutputPath, report);

var failedByThreshold =
    summary.CriticalCount > options.MaxCritical ||
    summary.MajorCount > options.MaxMajor ||
    summary.MinorCount > options.MaxMinor ||
    summary.OcrDriftCount > options.MaxOcrDrift;

Console.WriteLine(
    $"[parity] summary: docs={summary.DocumentsTotal}, pages={summary.PagesTotal}, " +
    $"passed={summary.PagesPassed}, failed={summary.PagesFailed}, " +
    $"critical={summary.CriticalCount}, major={summary.MajorCount}, minor={summary.MinorCount}, " +
    $"ocr_drift={summary.OcrDriftCount}");
Console.WriteLine($"[parity] report written: {options.OutputPath}");

if (failedByThreshold)
{
    Console.WriteLine("[parity] status: FAIL (threshold exceeded)");
    Environment.ExitCode = 2;
}
else
{
    Console.WriteLine("[parity] status: PASS");
}

static ParityPageResult ComparePage(
    DoclingParseSession session,
    string documentKey,
    GroundTruthPage page,
    int nativePageCount,
    HarnessOptions options)
{
    var result = new ParityPageResult
    {
        PageNumber = page.PageNumber,
        GroundTruthPath = page.GroundTruthPath
    };

    var stopwatch = Stopwatch.StartNew();

    if (page.PageNumber <= 0 || page.PageNumber > nativePageCount)
    {
        result.Diffs.Add(new ParityDiff(
            ParitySeverity.Critical,
            "page_number_out_of_range",
            $"Expected page {page.PageNumber} but native page count is {nativePageCount}."));
        stopwatch.Stop();
        result.DurationMs = stopwatch.ElapsedMilliseconds;
        return result;
    }

    try
    {
        var expectedJson = File.ReadAllText(page.GroundTruthPath);
        var actualJson = session.DecodeSegmentedPageJson(documentKey, page.PageNumber - 1);

        if (!string.IsNullOrEmpty(options.DumpActualJsonDir))
        {
            Directory.CreateDirectory(options.DumpActualJsonDir);
            var dumpFileName = Path.GetFileName(page.GroundTruthPath);
            var dumpPath = Path.Combine(options.DumpActualJsonDir, dumpFileName);
            File.WriteAllText(dumpPath, actualJson);
        }

        var parity = SegmentedParityComparer.Compare(
            expectedJson,
            actualJson,
            new SegmentedParityOptions
            {
                NumericTolerance = options.NumericTolerance,
                CompareTextHashes = !options.SkipTextHash,
                CompareOcrSignals = !options.SkipOcrSignals,
                TextMismatchSeverity = options.TextMismatchSeverity,
                GeometryMismatchSeverity = options.GeometryMismatchSeverity,
                ContextMismatchSeverity = options.ContextMismatchSeverity
            });
        result.Diffs.AddRange(parity.Diffs);
    }
    catch (Exception ex)
    {
        result.Diffs.Add(new ParityDiff(
            ParitySeverity.Critical,
            "decode_or_compare_failed",
            $"Decode/compare failed: {ex.Message}"));
    }
    finally
    {
        stopwatch.Stop();
        result.DurationMs = stopwatch.ElapsedMilliseconds;
    }

    return result;
}

static ParityPageResult CreateCriticalPageResult(GroundTruthPage page, string code, string message)
{
    return new ParityPageResult
    {
        PageNumber = page.PageNumber,
        GroundTruthPath = page.GroundTruthPath,
        Diffs =
        [
            new ParityDiff(ParitySeverity.Critical, code, message)
        ]
    };
}

static void TryUnloadDocument(DoclingParseSession session, string documentKey)
{
    try
    {
        session.UnloadDocument(documentKey);
    }
    catch
    {
        // unload failures should not hide primary parity findings
    }
}

static string CreateDocumentKey(string pdfFileName)
{
    var stem = Path.GetFileNameWithoutExtension(pdfFileName);
    if (string.IsNullOrWhiteSpace(stem))
    {
        return "doc";
    }

    return $"doc_{stem.Replace(' ', '_')}";
}

static List<GroundTruthPage> DiscoverGroundTruthPages(
    string groundTruthDir,
    int? maxDocuments,
    int? maxPages)
{
    var regex = new Regex(
        @"^(?<pdf>.+\.pdf)\.page_no_(?<page>\d+)\.py\.json$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    var pages = new List<GroundTruthPage>();

    foreach (var file in Directory.EnumerateFiles(groundTruthDir, "*.py.json", SearchOption.TopDirectoryOnly))
    {
        var name = Path.GetFileName(file);
        var match = regex.Match(name);
        if (!match.Success)
        {
            continue;
        }

        var pdf = match.Groups["pdf"].Value;
        if (!int.TryParse(match.Groups["page"].Value, out var pageNumber))
        {
            continue;
        }

        pages.Add(new GroundTruthPage(pdf, pageNumber, file));
    }

    var ordered = pages
        .OrderBy(static p => p.PdfFileName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(static p => p.PageNumber)
        .ToList();

    if (maxDocuments is > 0)
    {
        var allowedDocuments = ordered
            .Select(static p => p.PdfFileName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxDocuments.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        ordered = ordered
            .Where(page => allowedDocuments.Contains(page.PdfFileName))
            .ToList();
    }

    if (maxPages is > 0)
    {
        ordered = ordered.Take(maxPages.Value).ToList();
    }

    return ordered;
}

static string FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        var marker = Path.Combine(
            current.FullName,
            "upstream",
            "deps",
            "docling-parse",
            "tests",
            "data",
            "groundtruth");
        if (Directory.Exists(marker))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new DirectoryNotFoundException(
        "Could not locate repository root containing upstream/deps/docling-parse/tests/data/groundtruth.");
}

static void WriteReport(string outputPath, ParityReport report)
{
    var dir = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(dir))
    {
        Directory.CreateDirectory(dir);
    }

    var options = new JsonSerializerOptions
    {
        WriteIndented = true
    };
    options.Converters.Add(new JsonStringEnumConverter());

    File.WriteAllText(outputPath, JsonSerializer.Serialize(report, options));
}

internal sealed record GroundTruthPage(string PdfFileName, int PageNumber, string GroundTruthPath);

internal sealed class HarnessOptions
{
    public string? ResourcesDir { get; private set; }

    public string? RegressionDir { get; private set; }

    public string? GroundTruthDir { get; private set; }

    public string OutputPath { get; private set; } = ".artifacts/parity/docling-parse-parity-report.json";

    public int? MaxDocuments { get; private set; }

    public int? MaxPages { get; private set; }

    public int MaxCritical { get; private set; } = 0;

    public int MaxMajor { get; private set; } = 0;

    public int MaxMinor { get; private set; } = int.MaxValue;

    public int MaxOcrDrift { get; private set; } = 0;

    public ParitySeverity GeometryMismatchSeverity { get; private set; } = ParitySeverity.Minor;

    public ParitySeverity ContextMismatchSeverity { get; private set; } = ParitySeverity.Minor;

    public ParitySeverity TextMismatchSeverity { get; private set; } = ParitySeverity.Major;

    public double NumericTolerance { get; private set; } = 0.001;

    public bool SkipTextHash { get; private set; }

    public bool SkipOcrSignals { get; private set; }

    public string? DumpActualJsonDir { get; private set; }

    public string? ExtractPdfPath { get; private set; }

    public string? ExtractOutputDir { get; private set; }

    public static HarnessOptions Parse(string[] args)
    {
        var options = new HarnessOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--resources-dir":
                    options.ResourcesDir = ReadValue(args, ref i, arg);
                    break;
                case "--regression-dir":
                    options.RegressionDir = ReadValue(args, ref i, arg);
                    break;
                case "--groundtruth-dir":
                    options.GroundTruthDir = ReadValue(args, ref i, arg);
                    break;
                case "--output":
                    options.OutputPath = ReadValue(args, ref i, arg);
                    break;
                case "--max-docs":
                    options.MaxDocuments = int.Parse(ReadValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--max-pages":
                    options.MaxPages = int.Parse(ReadValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--max-critical":
                    options.MaxCritical = int.Parse(ReadValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--max-major":
                    options.MaxMajor = int.Parse(ReadValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--max-minor":
                    options.MaxMinor = int.Parse(ReadValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--max-ocr-drift":
                    options.MaxOcrDrift = int.Parse(ReadValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--numeric-tolerance":
                    options.NumericTolerance = double.Parse(ReadValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--geometry-mismatch-severity":
                    options.GeometryMismatchSeverity = ParseSeverity(ReadValue(args, ref i, arg), arg);
                    break;
                case "--context-mismatch-severity":
                    options.ContextMismatchSeverity = ParseSeverity(ReadValue(args, ref i, arg), arg);
                    break;
                case "--text-mismatch-severity":
                    options.TextMismatchSeverity = ParseSeverity(ReadValue(args, ref i, arg), arg);
                    break;
                case "--skip-text-hash":
                    options.SkipTextHash = true;
                    break;
                case "--skip-ocr-signals":
                    options.SkipOcrSignals = true;
                    break;
                case "--dump-actual-json":
                    options.DumpActualJsonDir = ReadValue(args, ref i, arg);
                    break;
                case "--extract-pdf":
                    options.ExtractPdfPath = ReadValue(args, ref i, arg);
                    break;
                case "--extract-output-dir":
                    options.ExtractOutputDir = ReadValue(args, ref i, arg);
                    break;
                case "--help":
                case "-h":
                    PrintHelpAndExit();
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        return options;
    }

    public void ResolveDefaults(string repoRoot)
    {
        ResourcesDir ??= Path.Combine(repoRoot, "upstream", "deps", "docling-parse", "docling_parse", "pdf_resources");
        RegressionDir ??= Path.Combine(repoRoot, "upstream", "deps", "docling-parse", "tests", "data", "regression");
        GroundTruthDir ??= Path.Combine(repoRoot, "upstream", "deps", "docling-parse", "tests", "data", "groundtruth");
        OutputPath = Path.IsPathRooted(OutputPath) ? OutputPath : Path.Combine(repoRoot, OutputPath);
    }

    private static string ReadValue(string[] args, ref int index, string arg)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for argument {arg}");
        }

        index++;
        return args[index];
    }

    private static ParitySeverity ParseSeverity(string value, string arg)
    {
        if (Enum.TryParse<ParitySeverity>(value, ignoreCase: true, out var severity) &&
            Enum.IsDefined(severity))
        {
            return severity;
        }

        throw new ArgumentException(
            $"Invalid value '{value}' for {arg}. Allowed values: {string.Join(", ", Enum.GetNames<ParitySeverity>())}.");
    }

    private static void PrintHelpAndExit()
    {
        Console.WriteLine("DoclingParityHarness");
        Console.WriteLine("Arguments:");
        Console.WriteLine("  --resources-dir <path>");
        Console.WriteLine("  --regression-dir <path>");
        Console.WriteLine("  --groundtruth-dir <path>");
        Console.WriteLine("  --output <path>");
        Console.WriteLine("  --max-docs <int>");
        Console.WriteLine("  --max-pages <int>");
        Console.WriteLine("  --max-critical <int> (default: 0)");
        Console.WriteLine("  --max-major <int> (default: 0)");
        Console.WriteLine("  --max-minor <int> (default: int.MaxValue)");
        Console.WriteLine("  --max-ocr-drift <int> (default: 0)");
        Console.WriteLine("  --numeric-tolerance <double> (default: 0.001)");
        Console.WriteLine("  --text-mismatch-severity <Critical|Major|Minor> (default: Major)");
        Console.WriteLine("  --geometry-mismatch-severity <Critical|Major|Minor> (default: Minor)");
        Console.WriteLine("  --context-mismatch-severity <Critical|Major|Minor> (default: Minor)");
        Console.WriteLine("  --skip-text-hash");
        Console.WriteLine("  --skip-ocr-signals");
        Console.WriteLine("  --dump-actual-json <dir>   Write each page's actual C ABI JSON to <dir>");
        Environment.Exit(0);
    }
}

internal sealed class ParityReport
{
    public required DateTime GeneratedAtUtc { get; init; }

    public required string AbiVersion { get; init; }

    public required ParityThresholds Thresholds { get; init; }

    public required ParityCorpus Corpus { get; init; }

    public required ParitySummary Summary { get; init; }

    public required List<ParityDocumentResult> Documents { get; init; }
}

internal sealed class ParityThresholds
{
    public required int MaxCritical { get; init; }

    public required int MaxMajor { get; init; }

    public required int MaxMinor { get; init; }

    public required int MaxOcrDrift { get; init; }

    public required string TextMismatchSeverity { get; init; }

    public required string GeometryMismatchSeverity { get; init; }

    public required string ContextMismatchSeverity { get; init; }
}

internal sealed class ParityCorpus
{
    public required string GroundTruthDir { get; init; }

    public required string RegressionDir { get; init; }

    public required string ResourcesDir { get; init; }

    public required int GroundTruthFiles { get; init; }
}

internal sealed class ParitySummary
{
    public int DocumentsTotal { get; set; }

    public int PagesTotal { get; set; }

    public int PagesPassed { get; set; }

    public int PagesFailed { get; set; }

    public int CriticalCount { get; private set; }

    public int MajorCount { get; private set; }

    public int MinorCount { get; private set; }

    public int OcrDriftCount { get; private set; }

    public void Accumulate(ParityPageResult page)
    {
        CriticalCount += page.CriticalCount;
        MajorCount += page.MajorCount;
        MinorCount += page.MinorCount;
        OcrDriftCount += page.Diffs.Count(static diff => diff.Code == "ocr_from_ocr_count_mismatch");
    }
}

internal sealed class ParityDocumentResult
{
    public required string PdfFileName { get; init; }

    public required string PdfPath { get; init; }

    public int NativePageCount { get; set; }

    public List<ParityPageResult> Pages { get; } = [];
}

internal sealed class ParityPageResult
{
    public int PageNumber { get; init; }

    public required string GroundTruthPath { get; init; }

    public long DurationMs { get; set; }

    public List<ParityDiff> Diffs { get; init; } = [];

    public int CriticalCount => Diffs.Count(static diff => diff.Severity == ParitySeverity.Critical);

    public int MajorCount => Diffs.Count(static diff => diff.Severity == ParitySeverity.Major);

    public int MinorCount => Diffs.Count(static diff => diff.Severity == ParitySeverity.Minor);

    public bool IsPass => Diffs.Count == 0;
}
