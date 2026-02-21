namespace DoclingDotNet.Parity;

public enum ParitySeverity
{
    Critical = 0,
    Major = 1,
    Minor = 2
}

public sealed record ParityDiff(
    ParitySeverity Severity,
    string Code,
    string Message);

public sealed class SegmentedParityOptions
{
    public double NumericTolerance { get; set; } = 0.001;

    public bool CompareTextHashes { get; set; } = true;

    public bool CompareTextFieldSemantics { get; set; } = true;

    public ParitySeverity TextMismatchSeverity { get; set; } = ParitySeverity.Major;

    public bool CompareOcrSignals { get; set; } = true;

    public bool CompareGeometrySignatures { get; set; } = true;

    public int GeometryRoundingDecimals { get; set; } = 2;

    public ParitySeverity GeometryMismatchSeverity { get; set; } = ParitySeverity.Minor;

    public bool CompareFontSignatures { get; set; } = true;

    public bool CompareConfidenceStats { get; set; } = true;

    public double ConfidenceMeanTolerance { get; set; } = 0.001;

    public bool CompareReadingOrderSignals { get; set; } = true;

    public bool CompareBitmapResourceSignatures { get; set; } = true;

    public bool CompareWidgetSignatures { get; set; } = true;

    public bool CompareHyperlinkSignatures { get; set; } = true;

    public bool CompareShapeSignatures { get; set; } = true;

    public bool CompareContextFieldSemantics { get; set; } = true;

    public ParitySeverity ContextMismatchSeverity { get; set; } = ParitySeverity.Minor;
}

public sealed class SegmentedParityResult
{
    public required IReadOnlyList<ParityDiff> Diffs { get; init; }

    public int CriticalCount => Diffs.Count(static diff => diff.Severity == ParitySeverity.Critical);

    public int MajorCount => Diffs.Count(static diff => diff.Severity == ParitySeverity.Major);

    public int MinorCount => Diffs.Count(static diff => diff.Severity == ParitySeverity.Minor);

    public bool IsExactMatch => Diffs.Count == 0;
}
