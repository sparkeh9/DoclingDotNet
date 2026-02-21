using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;

namespace DoclingDotNet.Parity;

public static class SegmentedParityComparer
{
    private static readonly HashSet<string> RequiredTopLevelKeys =
    [
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
    ];

    private static readonly string[] CountProperties =
    [
        "bitmap_resources",
        "char_cells",
        "word_cells",
        "textline_cells",
        "widgets",
        "hyperlinks",
        "lines",
        "shapes"
    ];

    private static readonly string[] BooleanProperties =
    [
        "has_chars",
        "has_words",
        "has_lines"
    ];

    private static readonly string[] RectProperties =
    [
        "r_x0",
        "r_y0",
        "r_x1",
        "r_y1",
        "r_x2",
        "r_y2",
        "r_x3",
        "r_y3"
    ];

    private static readonly string[] CellArrayProperties =
    [
        "char_cells",
        "word_cells",
        "textline_cells"
    ];

    public static SegmentedParityResult Compare(
        string expectedJson,
        string actualJson,
        SegmentedParityOptions? options = null)
    {
        var parityOptions = options ?? new SegmentedParityOptions();
        var diffs = new List<ParityDiff>();

        using var expectedDoc = JsonDocument.Parse(expectedJson);
        using var actualDoc = JsonDocument.Parse(actualJson);

        var expectedRoot = expectedDoc.RootElement;
        var actualRoot = actualDoc.RootElement;

        CompareTopLevelKeys(expectedRoot, actualRoot, diffs);
        CompareArrayCounts(expectedRoot, actualRoot, diffs);
        CompareBooleans(expectedRoot, actualRoot, diffs);
        CompareDimension(expectedRoot, actualRoot, diffs, parityOptions.NumericTolerance);

        if (parityOptions.CompareTextHashes)
        {
            CompareTextHashes(expectedRoot, actualRoot, "char_cells", diffs);
            CompareTextHashes(expectedRoot, actualRoot, "word_cells", diffs);
            CompareTextHashes(expectedRoot, actualRoot, "textline_cells", diffs);
        }

        if (parityOptions.CompareTextFieldSemantics)
        {
            CompareTextFieldSemantics(expectedRoot, actualRoot, diffs, parityOptions.TextMismatchSeverity);
        }

        if (parityOptions.CompareOcrSignals)
        {
            CompareOcrSignals(expectedRoot, actualRoot, diffs);
        }

        if (parityOptions.CompareGeometrySignatures)
        {
            CompareGeometrySignatures(
                expectedRoot,
                actualRoot,
                diffs,
                parityOptions.GeometryRoundingDecimals,
                parityOptions.GeometryMismatchSeverity);
        }

        if (parityOptions.CompareFontSignatures)
        {
            CompareFontSignatures(expectedRoot, actualRoot, diffs);
        }

        if (parityOptions.CompareConfidenceStats)
        {
            CompareConfidenceStats(
                expectedRoot,
                actualRoot,
                diffs,
                parityOptions.ConfidenceMeanTolerance);
        }

        if (parityOptions.CompareReadingOrderSignals)
        {
            CompareReadingOrderSignals(expectedRoot, actualRoot, diffs);
        }

        if (parityOptions.CompareBitmapResourceSignatures)
        {
            CompareBitmapResourceSignatures(
                expectedRoot,
                actualRoot,
                diffs,
                parityOptions.GeometryRoundingDecimals,
                parityOptions.ContextMismatchSeverity);
        }

        if (parityOptions.CompareWidgetSignatures)
        {
            CompareWidgetSignatures(
                expectedRoot,
                actualRoot,
                diffs,
                parityOptions.GeometryRoundingDecimals,
                parityOptions.ContextMismatchSeverity);
        }

        if (parityOptions.CompareHyperlinkSignatures)
        {
            CompareHyperlinkSignatures(
                expectedRoot,
                actualRoot,
                diffs,
                parityOptions.GeometryRoundingDecimals,
                parityOptions.ContextMismatchSeverity);
        }

        if (parityOptions.CompareShapeSignatures)
        {
            CompareShapeSignatures(
                expectedRoot,
                actualRoot,
                diffs,
                parityOptions.GeometryRoundingDecimals,
                parityOptions.ContextMismatchSeverity);
        }

        if (parityOptions.CompareContextFieldSemantics)
        {
            CompareBitmapResourceSemantics(expectedRoot, actualRoot, diffs, parityOptions.ContextMismatchSeverity);
            CompareWidgetSemantics(expectedRoot, actualRoot, diffs, parityOptions.ContextMismatchSeverity);
            CompareHyperlinkSemantics(expectedRoot, actualRoot, diffs, parityOptions.ContextMismatchSeverity);
            CompareShapeSemantics(
                expectedRoot,
                actualRoot,
                diffs,
                parityOptions.ContextMismatchSeverity,
                parityOptions.NumericTolerance);
        }

        return new SegmentedParityResult
        {
            Diffs = diffs
        };
    }

    private static void CompareTopLevelKeys(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs)
    {
        var expectedKeys = GetObjectKeys(expectedRoot);
        var actualKeys = GetObjectKeys(actualRoot);

        foreach (var requiredKey in RequiredTopLevelKeys)
        {
            if (!actualKeys.Contains(requiredKey))
            {
                diffs.Add(new ParityDiff(
                    ParitySeverity.Critical,
                    "missing_required_top_level_key",
                    $"Actual payload is missing required top-level key '{requiredKey}'."));
            }

            if (!expectedKeys.Contains(requiredKey))
            {
                diffs.Add(new ParityDiff(
                    ParitySeverity.Critical,
                    "missing_required_top_level_key_expected",
                    $"Expected payload is missing required top-level key '{requiredKey}'."));
            }
        }

        foreach (var expectedKey in expectedKeys)
        {
            if (!actualKeys.Contains(expectedKey))
            {
                diffs.Add(new ParityDiff(
                    ParitySeverity.Major,
                    "missing_expected_top_level_key",
                    $"Actual payload is missing expected top-level key '{expectedKey}'."));
            }
        }

        foreach (var actualKey in actualKeys)
        {
            if (!expectedKeys.Contains(actualKey))
            {
                diffs.Add(new ParityDiff(
                    ParitySeverity.Minor,
                    "unexpected_top_level_key",
                    $"Actual payload has unexpected top-level key '{actualKey}'."));
            }
        }
    }

    private static void CompareArrayCounts(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs)
    {
        foreach (var property in CountProperties)
        {
            if (!TryGetArrayLength(expectedRoot, property, out var expectedCount))
            {
                diffs.Add(new ParityDiff(
                    ParitySeverity.Critical,
                    "expected_array_missing",
                    $"Expected payload is missing array '{property}'."));
                continue;
            }

            if (!TryGetArrayLength(actualRoot, property, out var actualCount))
            {
                diffs.Add(new ParityDiff(
                    ParitySeverity.Critical,
                    "actual_array_missing",
                    $"Actual payload is missing array '{property}'."));
                continue;
            }

            if (expectedCount != actualCount)
            {
                diffs.Add(new ParityDiff(
                    ParitySeverity.Major,
                    "array_count_mismatch",
                    $"Array '{property}' count mismatch. Expected={expectedCount}, Actual={actualCount}."));
            }
        }
    }

    private static void CompareBooleans(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs)
    {
        foreach (var property in BooleanProperties)
        {
            if (!TryGetBoolean(expectedRoot, property, out var expectedValue))
            {
                diffs.Add(new ParityDiff(
                    ParitySeverity.Critical,
                    "expected_boolean_missing",
                    $"Expected payload is missing boolean '{property}'."));
                continue;
            }

            if (!TryGetBoolean(actualRoot, property, out var actualValue))
            {
                diffs.Add(new ParityDiff(
                    ParitySeverity.Critical,
                    "actual_boolean_missing",
                    $"Actual payload is missing boolean '{property}'."));
                continue;
            }

            if (expectedValue != actualValue)
            {
                diffs.Add(new ParityDiff(
                    ParitySeverity.Major,
                    "boolean_mismatch",
                    $"Boolean '{property}' mismatch. Expected={expectedValue}, Actual={actualValue}."));
            }
        }
    }

    private static void CompareDimension(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs,
        double tolerance)
    {
        if (!TryGetProperty(expectedRoot, "dimension", out var expectedDimension))
        {
            diffs.Add(new ParityDiff(
                ParitySeverity.Critical,
                "expected_dimension_missing",
                "Expected payload is missing 'dimension'."));
            return;
        }

        if (!TryGetProperty(actualRoot, "dimension", out var actualDimension))
        {
            diffs.Add(new ParityDiff(
                ParitySeverity.Critical,
                "actual_dimension_missing",
                "Actual payload is missing 'dimension'."));
            return;
        }

        if (TryGetString(expectedDimension, "boundary_type", out var expectedBoundaryType) &&
            TryGetString(actualDimension, "boundary_type", out var actualBoundaryType) &&
            !string.Equals(expectedBoundaryType, actualBoundaryType, StringComparison.Ordinal))
        {
            diffs.Add(new ParityDiff(
                ParitySeverity.Minor,
                "dimension_boundary_type_mismatch",
                $"Dimension boundary_type mismatch. Expected='{expectedBoundaryType}', Actual='{actualBoundaryType}'."));
        }

        if (TryGetProperty(expectedDimension, "rect", out var expectedRect) &&
            TryGetProperty(actualDimension, "rect", out var actualRect))
        {
            foreach (var property in RectProperties)
            {
                if (TryGetDouble(expectedRect, property, out var expectedValue) &&
                    TryGetDouble(actualRect, property, out var actualValue) &&
                    Math.Abs(expectedValue - actualValue) > tolerance)
                {
                    diffs.Add(new ParityDiff(
                        ParitySeverity.Minor,
                        "dimension_rect_mismatch",
                        $"Dimension rect '{property}' mismatch. Expected={expectedValue}, Actual={actualValue}, Tolerance={tolerance}."));
                }
            }
        }
    }

    private static void CompareTextHashes(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        string property,
        ICollection<ParityDiff> diffs)
    {
        if (!TryGetArray(expectedRoot, property, out var expectedArray))
        {
            return;
        }

        if (!TryGetArray(actualRoot, property, out var actualArray))
        {
            return;
        }

        var expectedHash = ComputeTextHash(expectedArray);
        var actualHash = ComputeTextHash(actualArray);

        if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
        {
            diffs.Add(new ParityDiff(
                ParitySeverity.Major,
                "text_hash_mismatch",
                $"Text hash mismatch for '{property}'. Expected={expectedHash}, Actual={actualHash}."));
        }
    }

    private static void CompareTextFieldSemantics(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs,
        ParitySeverity mismatchSeverity)
    {
        foreach (var property in CellArrayProperties)
        {
            if (!TryGetArray(expectedRoot, property, out var expectedArray) ||
                !TryGetArray(actualRoot, property, out var actualArray))
            {
                continue;
            }

            CompareDistributionMismatch(
                "text_direction_distribution_mismatch",
                $"{property} text-direction distribution",
                BuildDistribution(expectedArray, static item => GetStringKey(item, "text_direction")),
                BuildDistribution(actualArray, static item => GetStringKey(item, "text_direction")),
                mismatchSeverity,
                diffs);

            CompareDistributionMismatch(
                "rendering_mode_distribution_mismatch",
                $"{property} rendering-mode distribution",
                BuildDistribution(expectedArray, GetRenderingModeKey),
                BuildDistribution(actualArray, GetRenderingModeKey),
                mismatchSeverity,
                diffs);

            CompareDistributionMismatch(
                "text_length_distribution_mismatch",
                $"{property} text-length distribution",
                BuildDistribution(expectedArray, GetTextLengthKey),
                BuildDistribution(actualArray, GetTextLengthKey),
                mismatchSeverity,
                diffs);

            CompareDistributionMismatch(
                "text_orig_length_delta_distribution_mismatch",
                $"{property} text/orig length-delta distribution",
                BuildDistribution(expectedArray, GetTextOrigLengthDeltaKey),
                BuildDistribution(actualArray, GetTextOrigLengthDeltaKey),
                mismatchSeverity,
                diffs);

            CompareDistributionMismatch(
                "font_name_distribution_mismatch",
                $"{property} font-name distribution",
                BuildDistribution(expectedArray, static item => GetStringKey(item, "font_name")),
                BuildDistribution(actualArray, static item => GetStringKey(item, "font_name")),
                mismatchSeverity,
                diffs);

            CompareDistributionMismatch(
                "font_key_distribution_mismatch",
                $"{property} font-key distribution",
                BuildDistribution(expectedArray, static item => GetStringKey(item, "font_key")),
                BuildDistribution(actualArray, static item => GetStringKey(item, "font_key")),
                mismatchSeverity,
                diffs);

            CompareDistributionMismatch(
                "text_color_distribution_mismatch",
                $"{property} color distribution",
                BuildDistribution(expectedArray, static item => ComputeColorSignature(item, "rgba")),
                BuildDistribution(actualArray, static item => ComputeColorSignature(item, "rgba")),
                mismatchSeverity,
                diffs);

            var expectedWidgetCount = CountBooleanFlag(expectedArray, "widget", true);
            var actualWidgetCount = CountBooleanFlag(actualArray, "widget", true);
            if (expectedWidgetCount != actualWidgetCount)
            {
                diffs.Add(new ParityDiff(
                    mismatchSeverity,
                    "widget_flag_count_mismatch",
                    $"Widget-flag count mismatch for '{property}'. Expected={expectedWidgetCount}, Actual={actualWidgetCount}."));
            }

            var expectedTextPresentCount = CountNonEmptyString(expectedArray, "text");
            var actualTextPresentCount = CountNonEmptyString(actualArray, "text");
            if (expectedTextPresentCount != actualTextPresentCount)
            {
                diffs.Add(new ParityDiff(
                    mismatchSeverity,
                    "text_presence_count_mismatch",
                    $"Text-presence count mismatch for '{property}'. Expected={expectedTextPresentCount}, Actual={actualTextPresentCount}."));
            }

            var expectedOrigPresentCount = CountNonEmptyString(expectedArray, "orig");
            var actualOrigPresentCount = CountNonEmptyString(actualArray, "orig");
            if (expectedOrigPresentCount != actualOrigPresentCount)
            {
                diffs.Add(new ParityDiff(
                    mismatchSeverity,
                    "orig_presence_count_mismatch",
                    $"Orig-presence count mismatch for '{property}'. Expected={expectedOrigPresentCount}, Actual={actualOrigPresentCount}."));
            }

            var expectedTextOrigEqualityCount = CountEqualStringProperties(expectedArray, "text", "orig");
            var actualTextOrigEqualityCount = CountEqualStringProperties(actualArray, "text", "orig");
            if (expectedTextOrigEqualityCount != actualTextOrigEqualityCount)
            {
                diffs.Add(new ParityDiff(
                    mismatchSeverity,
                    "text_orig_equality_count_mismatch",
                    $"Text/orig equality count mismatch for '{property}'. Expected={expectedTextOrigEqualityCount}, Actual={actualTextOrigEqualityCount}."));
            }

            var expectedSemanticSequenceHash = ComputeTextSemanticSequenceHash(expectedArray);
            var actualSemanticSequenceHash = ComputeTextSemanticSequenceHash(actualArray);
            if (!string.Equals(expectedSemanticSequenceHash, actualSemanticSequenceHash, StringComparison.Ordinal))
            {
                diffs.Add(new ParityDiff(
                    mismatchSeverity,
                    "text_semantic_sequence_mismatch",
                    $"Text semantic sequence mismatch for '{property}'. Expected={expectedSemanticSequenceHash}, Actual={actualSemanticSequenceHash}."));
            }

            var expectedIndexMapValid = TryBuildTextIndexSemanticMap(
                expectedArray,
                out var expectedIndexMap,
                out var expectedDuplicateIndexCount,
                out var expectedMissingIndexCount);
            var actualIndexMapValid = TryBuildTextIndexSemanticMap(
                actualArray,
                out var actualIndexMap,
                out var actualDuplicateIndexCount,
                out var actualMissingIndexCount);

            if (!expectedIndexMapValid || !actualIndexMapValid)
            {
                diffs.Add(new ParityDiff(
                    mismatchSeverity,
                    "text_index_integrity_mismatch",
                    $"Text index integrity mismatch for '{property}'. Expected(duplicate={expectedDuplicateIndexCount}, missing={expectedMissingIndexCount}), Actual(duplicate={actualDuplicateIndexCount}, missing={actualMissingIndexCount})."));
                continue;
            }

            var expectedIndexMapHash = ComputeSequenceHash(
                expectedIndexMap.Select(static pair => $"{pair.Key.ToString(CultureInfo.InvariantCulture)}:{pair.Value}"));
            var actualIndexMapHash = ComputeSequenceHash(
                actualIndexMap.Select(static pair => $"{pair.Key.ToString(CultureInfo.InvariantCulture)}:{pair.Value}"));
            if (!string.Equals(expectedIndexMapHash, actualIndexMapHash, StringComparison.Ordinal))
            {
                diffs.Add(new ParityDiff(
                    mismatchSeverity,
                    "text_index_value_map_mismatch",
                    $"Text index-value map mismatch for '{property}'. Expected={expectedIndexMapHash}, Actual={actualIndexMapHash}."));
            }
        }
    }

    private static void CompareOcrSignals(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs)
    {
        CompareFromOcrFlagCount(expectedRoot, actualRoot, "char_cells", diffs);
        CompareFromOcrFlagCount(expectedRoot, actualRoot, "word_cells", diffs);
        CompareFromOcrFlagCount(expectedRoot, actualRoot, "textline_cells", diffs);
    }

    private static void CompareFromOcrFlagCount(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        string arrayProperty,
        ICollection<ParityDiff> diffs)
    {
        if (!TryGetArray(expectedRoot, arrayProperty, out var expectedArray))
        {
            return;
        }

        if (!TryGetArray(actualRoot, arrayProperty, out var actualArray))
        {
            return;
        }

        var expectedFromOcrCount = CountBooleanFlag(expectedArray, "from_ocr", true);
        var actualFromOcrCount = CountBooleanFlag(actualArray, "from_ocr", true);
        if (expectedFromOcrCount != actualFromOcrCount)
        {
            diffs.Add(new ParityDiff(
                ParitySeverity.Major,
                "ocr_from_ocr_count_mismatch",
                $"OCR flag count mismatch for '{arrayProperty}'. Expected={expectedFromOcrCount}, Actual={actualFromOcrCount}."));
        }
    }

    private static void CompareGeometrySignatures(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs,
        int roundingDecimals,
        ParitySeverity mismatchSeverity)
    {
        var decimals = Math.Clamp(roundingDecimals, 0, 8);
        foreach (var property in CellArrayProperties)
        {
            if (!TryGetArray(expectedRoot, property, out var expectedArray) ||
                !TryGetArray(actualRoot, property, out var actualArray))
            {
                continue;
            }

            var expectedHash = ComputeRectSignatureHash(expectedArray, decimals);
            var actualHash = ComputeRectSignatureHash(actualArray, decimals);
            if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
            {
                diffs.Add(new ParityDiff(
                    mismatchSeverity,
                    "geometry_signature_mismatch",
                    $"Geometry signature mismatch for '{property}'. Expected={expectedHash}, Actual={actualHash}, Decimals={decimals}."));
            }
        }
    }

    private static void CompareFontSignatures(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs)
    {
        foreach (var property in CellArrayProperties)
        {
            if (!TryGetArray(expectedRoot, property, out var expectedArray) ||
                !TryGetArray(actualRoot, property, out var actualArray))
            {
                continue;
            }

            var expectedHash = ComputeFontSignatureHash(expectedArray);
            var actualHash = ComputeFontSignatureHash(actualArray);
            if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
            {
                diffs.Add(new ParityDiff(
                    ParitySeverity.Major,
                    "font_signature_mismatch",
                    $"Font signature mismatch for '{property}'. Expected={expectedHash}, Actual={actualHash}."));
            }
        }
    }

    private static void CompareConfidenceStats(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs,
        double meanTolerance)
    {
        var tolerance = Math.Max(0.0, meanTolerance);

        foreach (var property in CellArrayProperties)
        {
            if (!TryGetArray(expectedRoot, property, out var expectedArray) ||
                !TryGetArray(actualRoot, property, out var actualArray))
            {
                continue;
            }

            var expectedValues = ExtractNumericPropertyValues(expectedArray, "confidence");
            var actualValues = ExtractNumericPropertyValues(actualArray, "confidence");
            if (expectedValues.Count != actualValues.Count)
            {
                diffs.Add(new ParityDiff(
                    ParitySeverity.Major,
                    "confidence_value_count_mismatch",
                    $"Confidence-value count mismatch for '{property}'. Expected={expectedValues.Count}, Actual={actualValues.Count}."));
                continue;
            }

            if (expectedValues.Count == 0)
            {
                continue;
            }

            var expectedMean = expectedValues.Average();
            var actualMean = actualValues.Average();
            if (Math.Abs(expectedMean - actualMean) > tolerance)
            {
                diffs.Add(new ParityDiff(
                    ParitySeverity.Minor,
                    "confidence_mean_mismatch",
                    $"Confidence mean mismatch for '{property}'. Expected={expectedMean.ToString("G17", CultureInfo.InvariantCulture)}, Actual={actualMean.ToString("G17", CultureInfo.InvariantCulture)}, Tolerance={tolerance.ToString("G17", CultureInfo.InvariantCulture)}."));
            }
        }
    }

    private static void CompareReadingOrderSignals(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs)
    {
        if (!TryGetArray(expectedRoot, "textline_cells", out var expectedArray) ||
            !TryGetArray(actualRoot, "textline_cells", out var actualArray))
        {
            return;
        }

        var expectedIndices = ExtractIntegerPropertyValues(expectedArray, "index");
        var actualIndices = ExtractIntegerPropertyValues(actualArray, "index");

        if (expectedIndices.Count != actualIndices.Count)
        {
            diffs.Add(new ParityDiff(
                ParitySeverity.Major,
                "reading_order_index_count_mismatch",
                $"Reading-order index count mismatch for 'textline_cells'. Expected={expectedIndices.Count}, Actual={actualIndices.Count}."));
            return;
        }

        if (expectedIndices.Count == 0)
        {
            return;
        }

        var expectedHash = ComputeSequenceHash(expectedIndices.Select(static value => value.ToString(CultureInfo.InvariantCulture)));
        var actualHash = ComputeSequenceHash(actualIndices.Select(static value => value.ToString(CultureInfo.InvariantCulture)));
        if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
        {
            diffs.Add(new ParityDiff(
                ParitySeverity.Major,
                "reading_order_index_mismatch",
                $"Reading-order index signature mismatch for 'textline_cells'. Expected={expectedHash}, Actual={actualHash}."));
        }
    }

    private static void CompareBitmapResourceSignatures(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs,
        int roundingDecimals,
        ParitySeverity mismatchSeverity)
    {
        if (!TryGetArray(expectedRoot, "bitmap_resources", out var expectedArray) ||
            !TryGetArray(actualRoot, "bitmap_resources", out var actualArray))
        {
            return;
        }

        var expectedHash = ComputeBitmapResourceSignatureHash(expectedArray, roundingDecimals);
        var actualHash = ComputeBitmapResourceSignatureHash(actualArray, roundingDecimals);
        if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
        {
            diffs.Add(new ParityDiff(
                mismatchSeverity,
                "bitmap_resource_signature_mismatch",
                $"Bitmap-resource signature mismatch. Expected={expectedHash}, Actual={actualHash}."));
        }
    }

    private static void CompareWidgetSignatures(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs,
        int roundingDecimals,
        ParitySeverity mismatchSeverity)
    {
        if (!TryGetArray(expectedRoot, "widgets", out var expectedArray) ||
            !TryGetArray(actualRoot, "widgets", out var actualArray))
        {
            return;
        }

        var expectedHash = ComputeWidgetSignatureHash(expectedArray, roundingDecimals);
        var actualHash = ComputeWidgetSignatureHash(actualArray, roundingDecimals);
        if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
        {
            diffs.Add(new ParityDiff(
                mismatchSeverity,
                "widget_signature_mismatch",
                $"Widget signature mismatch. Expected={expectedHash}, Actual={actualHash}."));
        }
    }

    private static void CompareHyperlinkSignatures(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs,
        int roundingDecimals,
        ParitySeverity mismatchSeverity)
    {
        if (!TryGetArray(expectedRoot, "hyperlinks", out var expectedArray) ||
            !TryGetArray(actualRoot, "hyperlinks", out var actualArray))
        {
            return;
        }

        var expectedHash = ComputeHyperlinkSignatureHash(expectedArray, roundingDecimals);
        var actualHash = ComputeHyperlinkSignatureHash(actualArray, roundingDecimals);
        if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
        {
            diffs.Add(new ParityDiff(
                mismatchSeverity,
                "hyperlink_signature_mismatch",
                $"Hyperlink signature mismatch. Expected={expectedHash}, Actual={actualHash}."));
        }
    }

    private static void CompareShapeSignatures(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs,
        int roundingDecimals,
        ParitySeverity mismatchSeverity)
    {
        if (!TryGetArray(expectedRoot, "shapes", out var expectedArray) ||
            !TryGetArray(actualRoot, "shapes", out var actualArray))
        {
            return;
        }

        var expectedHash = ComputeShapeSignatureHash(expectedArray, roundingDecimals);
        var actualHash = ComputeShapeSignatureHash(actualArray, roundingDecimals);
        if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
        {
            diffs.Add(new ParityDiff(
                mismatchSeverity,
                "shape_signature_mismatch",
                $"Shape signature mismatch. Expected={expectedHash}, Actual={actualHash}."));
        }
    }

    private static void CompareBitmapResourceSemantics(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs,
        ParitySeverity mismatchSeverity)
    {
        if (!TryGetArray(expectedRoot, "bitmap_resources", out var expectedArray) ||
            !TryGetArray(actualRoot, "bitmap_resources", out var actualArray))
        {
            return;
        }

        CompareDistributionMismatch(
            "bitmap_mode_distribution_mismatch",
            "bitmap resource mode distribution",
            BuildDistribution(expectedArray, GetBitmapModeKey),
            BuildDistribution(actualArray, GetBitmapModeKey),
            mismatchSeverity,
            diffs);

        CompareDistributionMismatch(
            "bitmap_mimetype_distribution_mismatch",
            "bitmap resource mimetype distribution",
            BuildDistribution(expectedArray, GetBitmapMimetypeKey),
            BuildDistribution(actualArray, GetBitmapMimetypeKey),
            mismatchSeverity,
            diffs);

        CompareDistributionMismatch(
            "bitmap_dpi_distribution_mismatch",
            "bitmap resource DPI distribution",
            BuildDistribution(expectedArray, GetBitmapDpiKey),
            BuildDistribution(actualArray, GetBitmapDpiKey),
            mismatchSeverity,
            diffs);
    }

    private static void CompareWidgetSemantics(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs,
        ParitySeverity mismatchSeverity)
    {
        if (!TryGetArray(expectedRoot, "widgets", out var expectedArray) ||
            !TryGetArray(actualRoot, "widgets", out var actualArray))
        {
            return;
        }

        CompareDistributionMismatch(
            "widget_field_type_distribution_mismatch",
            "widget field-type distribution",
            BuildDistribution(expectedArray, static item => GetStringKey(item, "widget_field_type")),
            BuildDistribution(actualArray, static item => GetStringKey(item, "widget_field_type")),
            mismatchSeverity,
            diffs);

        var expectedTextPresentCount = CountNonEmptyString(expectedArray, "widget_text");
        var actualTextPresentCount = CountNonEmptyString(actualArray, "widget_text");
        if (expectedTextPresentCount != actualTextPresentCount)
        {
            diffs.Add(new ParityDiff(
                mismatchSeverity,
                "widget_text_presence_count_mismatch",
                $"Widget text-presence count mismatch. Expected={expectedTextPresentCount}, Actual={actualTextPresentCount}."));
        }
    }

    private static void CompareHyperlinkSemantics(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs,
        ParitySeverity mismatchSeverity)
    {
        if (!TryGetArray(expectedRoot, "hyperlinks", out var expectedArray) ||
            !TryGetArray(actualRoot, "hyperlinks", out var actualArray))
        {
            return;
        }

        CompareDistributionMismatch(
            "hyperlink_scheme_distribution_mismatch",
            "hyperlink URI scheme distribution",
            BuildDistribution(expectedArray, GetHyperlinkSchemeKey),
            BuildDistribution(actualArray, GetHyperlinkSchemeKey),
            mismatchSeverity,
            diffs);

        CompareDistributionMismatch(
            "hyperlink_host_distribution_mismatch",
            "hyperlink URI host distribution",
            BuildDistribution(expectedArray, GetHyperlinkHostKey),
            BuildDistribution(actualArray, GetHyperlinkHostKey),
            mismatchSeverity,
            diffs);
    }

    private static void CompareShapeSemantics(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        ICollection<ParityDiff> diffs,
        ParitySeverity mismatchSeverity,
        double numericTolerance)
    {
        if (!TryGetArray(expectedRoot, "shapes", out var expectedArray) ||
            !TryGetArray(actualRoot, "shapes", out var actualArray))
        {
            return;
        }

        var expectedGraphicsStateCount = CountBooleanFlag(expectedArray, "has_graphics_state", true);
        var actualGraphicsStateCount = CountBooleanFlag(actualArray, "has_graphics_state", true);
        if (expectedGraphicsStateCount != actualGraphicsStateCount)
        {
            diffs.Add(new ParityDiff(
                mismatchSeverity,
                "shape_graphics_state_count_mismatch",
                $"Shape graphics-state count mismatch. Expected={expectedGraphicsStateCount}, Actual={actualGraphicsStateCount}."));
        }

        var expectedLineWidths = ExtractNumericPropertyValues(expectedArray, "line_width");
        var actualLineWidths = ExtractNumericPropertyValues(actualArray, "line_width");
        if (expectedLineWidths.Count == actualLineWidths.Count && expectedLineWidths.Count > 0)
        {
            var tolerance = Math.Max(0.0, numericTolerance);
            var expectedMean = expectedLineWidths.Average();
            var actualMean = actualLineWidths.Average();
            if (Math.Abs(expectedMean - actualMean) > tolerance)
            {
                diffs.Add(new ParityDiff(
                    mismatchSeverity,
                    "shape_line_width_mean_mismatch",
                    $"Shape line-width mean mismatch. Expected={expectedMean.ToString("G17", CultureInfo.InvariantCulture)}, Actual={actualMean.ToString("G17", CultureInfo.InvariantCulture)}, Tolerance={tolerance.ToString("G17", CultureInfo.InvariantCulture)}."));
            }
        }

        CompareDistributionMismatch(
            "shape_point_count_distribution_mismatch",
            "shape point-count distribution",
            BuildDistribution(expectedArray, GetShapePointCountKey),
            BuildDistribution(actualArray, GetShapePointCountKey),
            mismatchSeverity,
            diffs);

        CompareDistributionMismatch(
            "shape_line_cap_distribution_mismatch",
            "shape line-cap distribution",
            BuildDistribution(expectedArray, GetShapeLineCapKey),
            BuildDistribution(actualArray, GetShapeLineCapKey),
            mismatchSeverity,
            diffs);

        CompareDistributionMismatch(
            "shape_line_join_distribution_mismatch",
            "shape line-join distribution",
            BuildDistribution(expectedArray, GetShapeLineJoinKey),
            BuildDistribution(actualArray, GetShapeLineJoinKey),
            mismatchSeverity,
            diffs);

        CompareDistributionMismatch(
            "shape_fill_color_distribution_mismatch",
            "shape fill-color distribution",
            BuildDistribution(expectedArray, static item => ComputeColorSignature(item, "rgb_filling")),
            BuildDistribution(actualArray, static item => ComputeColorSignature(item, "rgb_filling")),
            mismatchSeverity,
            diffs);

        CompareDistributionMismatch(
            "shape_stroke_color_distribution_mismatch",
            "shape stroke-color distribution",
            BuildDistribution(expectedArray, static item => ComputeColorSignature(item, "rgb_stroking")),
            BuildDistribution(actualArray, static item => ComputeColorSignature(item, "rgb_stroking")),
            mismatchSeverity,
            diffs);
    }

    private static HashSet<string> GetObjectKeys(JsonElement element)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return keys;
        }

        foreach (var property in element.EnumerateObject())
        {
            keys.Add(property.Name);
        }

        return keys;
    }

    private static bool TryGetArrayLength(JsonElement root, string property, out int length)
    {
        if (TryGetArray(root, property, out var array))
        {
            length = array.GetArrayLength();
            return true;
        }

        length = 0;
        return false;
    }

    private static bool TryGetArray(JsonElement root, string property, out JsonElement array)
    {
        if (TryGetProperty(root, property, out var value) && value.ValueKind == JsonValueKind.Array)
        {
            array = value;
            return true;
        }

        array = default;
        return false;
    }

    private static bool TryGetBoolean(JsonElement root, string property, out bool value)
    {
        if (TryGetProperty(root, property, out var element) &&
            (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
        {
            value = element.GetBoolean();
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryGetString(JsonElement root, string property, out string value)
    {
        if (TryGetProperty(root, property, out var element) && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetDouble(JsonElement root, string property, out double value)
    {
        if (TryGetProperty(root, property, out var element) && element.TryGetDouble(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetInt64(JsonElement root, string property, out long value)
    {
        if (TryGetProperty(root, property, out var element) && element.TryGetInt64(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetProperty(JsonElement root, string property, out JsonElement value)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(property, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string ComputeTextHash(JsonElement array)
    {
        return ComputeSequenceHash(
            array.EnumerateArray().Select(item =>
            {
                if (item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("text", out var textElement) &&
                    textElement.ValueKind == JsonValueKind.String)
                {
                    return textElement.GetString() ?? string.Empty;
                }

                return string.Empty;
            }));
    }

    private static string ComputeTextSemanticSequenceHash(JsonElement array)
    {
        return ComputeSequenceHash(
            array.EnumerateArray().Select(item =>
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return "_invalid_";
                }

                var indexKey = item.TryGetProperty("index", out var indexElement) && indexElement.TryGetInt64(out var indexValue)
                    ? indexValue.ToString(CultureInfo.InvariantCulture)
                    : "_missing_";
                var textKey = GetStringKey(item, "text");
                var origKey = GetStringKey(item, "orig");
                var directionKey = GetStringKey(item, "text_direction");
                var renderingKey = GetRenderingModeKey(item);
                var widgetKey = GetBooleanKey(item, "widget");
                var fontKey = GetStringKey(item, "font_key");
                var fontNameKey = GetStringKey(item, "font_name");
                var colorKey = ComputeColorSignature(item, "rgba");

                return $"{indexKey}|{textKey}|{origKey}|{directionKey}|{renderingKey}|{widgetKey}|{fontKey}|{fontNameKey}|{colorKey}";
            }));
    }

    private static bool TryBuildTextIndexSemanticMap(
        JsonElement array,
        out SortedDictionary<long, string> indexMap,
        out int duplicateIndexCount,
        out int missingIndexCount)
    {
        indexMap = new SortedDictionary<long, string>();
        duplicateIndexCount = 0;
        missingIndexCount = 0;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("index", out var indexElement) ||
                !indexElement.TryGetInt64(out var index))
            {
                missingIndexCount++;
                continue;
            }

            var signature = $"{GetStringKey(item, "text")}|{GetStringKey(item, "orig")}|{GetStringKey(item, "text_direction")}|{GetRenderingModeKey(item)}|{GetBooleanKey(item, "widget")}|{GetStringKey(item, "font_key")}|{GetStringKey(item, "font_name")}|{ComputeColorSignature(item, "rgba")}";
            if (!indexMap.TryAdd(index, signature))
            {
                duplicateIndexCount++;
            }
        }

        return duplicateIndexCount == 0 && missingIndexCount == 0;
    }

    private static int CountBooleanFlag(JsonElement array, string property, bool expectedValue)
    {
        var count = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!item.TryGetProperty(property, out var flagElement))
            {
                continue;
            }

            if ((flagElement.ValueKind == JsonValueKind.True || flagElement.ValueKind == JsonValueKind.False) &&
                flagElement.GetBoolean() == expectedValue)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountNonEmptyString(JsonElement array, string property)
    {
        var count = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty(property, out var textElement) ||
                textElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(textElement.GetString()))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountEqualStringProperties(JsonElement array, string propertyA, string propertyB)
    {
        var count = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty(propertyA, out var valueA) ||
                !item.TryGetProperty(propertyB, out var valueB) ||
                valueA.ValueKind != JsonValueKind.String ||
                valueB.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (string.Equals(valueA.GetString(), valueB.GetString(), StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static Dictionary<string, int> BuildDistribution(
        JsonElement array,
        Func<JsonElement, string> selector)
    {
        var distribution = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in array.EnumerateArray())
        {
            var key = selector(item);
            if (!distribution.TryAdd(key, 1))
            {
                distribution[key]++;
            }
        }

        return distribution;
    }

    private static void CompareDistributionMismatch(
        string code,
        string label,
        IReadOnlyDictionary<string, int> expectedDistribution,
        IReadOnlyDictionary<string, int> actualDistribution,
        ParitySeverity severity,
        ICollection<ParityDiff> diffs)
    {
        if (AreDistributionsEqual(expectedDistribution, actualDistribution))
        {
            return;
        }

        diffs.Add(new ParityDiff(
            severity,
            code,
            $"{label} mismatch. Expected={FormatDistribution(expectedDistribution)}, Actual={FormatDistribution(actualDistribution)}."));
    }

    private static bool AreDistributionsEqual(
        IReadOnlyDictionary<string, int> expected,
        IReadOnlyDictionary<string, int> actual)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        foreach (var (key, expectedCount) in expected)
        {
            if (!actual.TryGetValue(key, out var actualCount) || expectedCount != actualCount)
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatDistribution(IReadOnlyDictionary<string, int> distribution)
    {
        if (distribution.Count == 0)
        {
            return "{}";
        }

        return "{" + string.Join(
            ", ",
            distribution
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => $"{pair.Key}={pair.Value}")) + "}";
    }

    private static string GetStringKey(JsonElement item, string property)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return "_invalid_";
        }

        if (item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? "_empty_" : text!;
        }

        return "_missing_";
    }

    private static string GetBooleanKey(JsonElement item, string property)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty(property, out var value))
        {
            return "_missing_";
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => "_missing_"
        };
    }

    private static string GetBitmapModeKey(JsonElement item)
    {
        return GetStringKey(item, "mode");
    }

    private static string GetTextLengthKey(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("text", out var textElement) ||
            textElement.ValueKind != JsonValueKind.String)
        {
            return "_missing_";
        }

        var text = textElement.GetString() ?? string.Empty;
        return text.Length.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetTextOrigLengthDeltaKey(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("text", out var textElement) ||
            !item.TryGetProperty("orig", out var origElement) ||
            textElement.ValueKind != JsonValueKind.String ||
            origElement.ValueKind != JsonValueKind.String)
        {
            return "_missing_";
        }

        var textLength = (textElement.GetString() ?? string.Empty).Length;
        var origLength = (origElement.GetString() ?? string.Empty).Length;
        var delta = Math.Abs(textLength - origLength);
        return delta.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetRenderingModeKey(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("rendering_mode", out var renderingMode) ||
            !renderingMode.TryGetInt64(out var value))
        {
            return "_missing_";
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetBitmapMimetypeKey(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("image", out var image) ||
            image.ValueKind != JsonValueKind.Object)
        {
            return "_missing_";
        }

        return GetStringKey(image, "mimetype");
    }

    private static string GetBitmapDpiKey(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("image", out var image) ||
            image.ValueKind != JsonValueKind.Object)
        {
            return "_missing_";
        }

        if (image.TryGetProperty("dpi", out var dpi) && dpi.TryGetInt64(out var dpiValue))
        {
            return dpiValue.ToString(CultureInfo.InvariantCulture);
        }

        return "_missing_";
    }

    private static string GetHyperlinkSchemeKey(JsonElement item)
    {
        return GetUriComponentKey(item, component: "scheme");
    }

    private static string GetHyperlinkHostKey(JsonElement item)
    {
        return GetUriComponentKey(item, component: "host");
    }

    private static string GetUriComponentKey(JsonElement item, string component)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("uri", out var uriElement) ||
            uriElement.ValueKind != JsonValueKind.String)
        {
            return "_missing_";
        }

        var uriText = uriElement.GetString();
        if (string.IsNullOrWhiteSpace(uriText))
        {
            return "_empty_";
        }

        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
        {
            return "_invalid_uri_";
        }

        return component == "scheme"
            ? uri.Scheme
            : uri.Host;
    }

    private static string GetShapePointCountKey(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("points", out var points) ||
            points.ValueKind != JsonValueKind.Array)
        {
            return "_missing_";
        }

        return points.GetArrayLength().ToString(CultureInfo.InvariantCulture);
    }

    private static string GetShapeLineCapKey(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("line_cap", out var value) ||
            !value.TryGetInt64(out var number))
        {
            return "_missing_";
        }
        return number.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetShapeLineJoinKey(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("line_join", out var value) ||
            !value.TryGetInt64(out var number))
        {
            return "_missing_";
        }
        return number.ToString(CultureInfo.InvariantCulture);
    }

    private static string ComputeRectSignatureHash(JsonElement array, int roundingDecimals)
    {
        var signatures = array.EnumerateArray().Select(item =>
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("rect", out var rect) ||
                    rect.ValueKind != JsonValueKind.Object)
                {
                    return "_missing_rect_";
                }

                var parts = new List<string>(RectProperties.Length);
                foreach (var property in RectProperties)
                {
                    if (!rect.TryGetProperty(property, out var value) || !value.TryGetDouble(out var number))
                    {
                        parts.Add("_missing_");
                        continue;
                    }

                    var rounded = Math.Round(number, roundingDecimals, MidpointRounding.AwayFromZero);
                    parts.Add(rounded.ToString(CultureInfo.InvariantCulture));
                }

                return string.Join('|', parts);
            })
            .OrderBy(static signature => signature, StringComparer.Ordinal)
            .ToArray();

        return ComputeSequenceHash(signatures);
    }

    private static string ComputeFontSignatureHash(JsonElement array)
    {
        return ComputeSequenceHash(array.EnumerateArray().Select(item =>
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return "|";
            }

            var fontKey = item.TryGetProperty("font_key", out var fontKeyElement) && fontKeyElement.ValueKind == JsonValueKind.String
                ? fontKeyElement.GetString() ?? string.Empty
                : string.Empty;
            var fontName = item.TryGetProperty("font_name", out var fontNameElement) && fontNameElement.ValueKind == JsonValueKind.String
                ? fontNameElement.GetString() ?? string.Empty
                : string.Empty;

            return $"{fontKey}|{fontName}";
        }));
    }

    private static string ComputeBitmapResourceSignatureHash(JsonElement array, int roundingDecimals)
    {
        var decimals = Math.Clamp(roundingDecimals, 0, 8);
        var signatures = array.EnumerateArray()
            .Select(item =>
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return "_invalid_bitmap_resource_";
                }

                var index = TryGetInt64(item, "index", out var indexValue)
                    ? indexValue.ToString(CultureInfo.InvariantCulture)
                    : "_missing_";
                var mode = TryGetString(item, "mode", out var modeValue)
                    ? modeValue
                    : string.Empty;
                var uri = TryGetString(item, "uri", out var uriValue)
                    ? uriValue
                    : string.Empty;
                var rectSignature = ComputeEmbeddedRectSignature(item, decimals);

                var mimeType = string.Empty;
                var dpi = "_missing_";
                var sizeWidth = "_missing_";
                var sizeHeight = "_missing_";
                var imageUri = string.Empty;

                if (TryGetProperty(item, "image", out var image) && image.ValueKind == JsonValueKind.Object)
                {
                    if (TryGetString(image, "mimetype", out var mimeTypeValue))
                    {
                        mimeType = mimeTypeValue;
                    }

                    if (TryGetInt64(image, "dpi", out var dpiValue))
                    {
                        dpi = dpiValue.ToString(CultureInfo.InvariantCulture);
                    }

                    if (TryGetProperty(image, "size", out var size) && size.ValueKind == JsonValueKind.Object)
                    {
                        if (TryGetDouble(size, "width", out var widthValue))
                        {
                            sizeWidth = FormatRoundedNumber(widthValue, decimals);
                        }

                        if (TryGetDouble(size, "height", out var heightValue))
                        {
                            sizeHeight = FormatRoundedNumber(heightValue, decimals);
                        }
                    }

                    if (TryGetString(image, "uri", out var imageUriValue))
                    {
                        imageUri = imageUriValue;
                    }
                }

                return string.Join('|',
                    index,
                    mode,
                    ComputeStringHash(uri),
                    rectSignature,
                    mimeType,
                    dpi,
                    sizeWidth,
                    sizeHeight,
                    ComputeStringHash(imageUri));
            })
            .OrderBy(static signature => signature, StringComparer.Ordinal)
            .ToArray();

        return ComputeSequenceHash(signatures);
    }

    private static string ComputeWidgetSignatureHash(JsonElement array, int roundingDecimals)
    {
        var decimals = Math.Clamp(roundingDecimals, 0, 8);
        var signatures = array.EnumerateArray()
            .Select(item =>
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return "_invalid_widget_";
                }

                var index = TryGetInt64(item, "index", out var indexValue)
                    ? indexValue.ToString(CultureInfo.InvariantCulture)
                    : "_missing_";
                var widgetText = TryGetString(item, "widget_text", out var widgetTextValue)
                    ? widgetTextValue
                    : string.Empty;
                var widgetDescription = TryGetString(item, "widget_description", out var widgetDescriptionValue)
                    ? widgetDescriptionValue
                    : string.Empty;
                var widgetFieldName = TryGetString(item, "widget_field_name", out var widgetFieldNameValue)
                    ? widgetFieldNameValue
                    : string.Empty;
                var widgetFieldType = TryGetString(item, "widget_field_type", out var widgetFieldTypeValue)
                    ? widgetFieldTypeValue
                    : string.Empty;
                var rectSignature = ComputeEmbeddedRectSignature(item, decimals);

                return string.Join('|',
                    index,
                    rectSignature,
                    widgetText,
                    widgetDescription,
                    widgetFieldName,
                    widgetFieldType);
            })
            .OrderBy(static signature => signature, StringComparer.Ordinal)
            .ToArray();

        return ComputeSequenceHash(signatures);
    }

    private static string ComputeHyperlinkSignatureHash(JsonElement array, int roundingDecimals)
    {
        var decimals = Math.Clamp(roundingDecimals, 0, 8);
        var signatures = array.EnumerateArray()
            .Select(item =>
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return "_invalid_hyperlink_";
                }

                var index = TryGetInt64(item, "index", out var indexValue)
                    ? indexValue.ToString(CultureInfo.InvariantCulture)
                    : "_missing_";
                var uri = TryGetString(item, "uri", out var uriValue)
                    ? uriValue
                    : string.Empty;
                var rectSignature = ComputeEmbeddedRectSignature(item, decimals);

                return string.Join('|',
                    index,
                    rectSignature,
                    ComputeStringHash(uri));
            })
            .OrderBy(static signature => signature, StringComparer.Ordinal)
            .ToArray();

        return ComputeSequenceHash(signatures);
    }

    private static string ComputeShapeSignatureHash(JsonElement array, int roundingDecimals)
    {
        var decimals = Math.Clamp(roundingDecimals, 0, 8);
        var signatures = array.EnumerateArray()
            .Select(item =>
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return "_invalid_shape_";
                }

                var index = TryGetInt64(item, "index", out var indexValue)
                    ? indexValue.ToString(CultureInfo.InvariantCulture)
                    : "_missing_";
                var parentId = TryGetInt64(item, "parent_id", out var parentIdValue)
                    ? parentIdValue.ToString(CultureInfo.InvariantCulture)
                    : "_missing_";
                var coordOrigin = TryGetString(item, "coord_origin", out var coordOriginValue)
                    ? coordOriginValue
                    : string.Empty;
                var hasGraphicsState = TryGetBoolean(item, "has_graphics_state", out var hasGraphicsStateValue)
                    ? hasGraphicsStateValue.ToString(CultureInfo.InvariantCulture)
                    : "_missing_";
                var lineWidth = TryGetDouble(item, "line_width", out var lineWidthValue)
                    ? FormatRoundedNumber(lineWidthValue, decimals)
                    : "_missing_";
                var miterLimit = TryGetDouble(item, "miter_limit", out var miterLimitValue)
                    ? FormatRoundedNumber(miterLimitValue, decimals)
                    : "_missing_";
                var lineCap = TryGetInt64(item, "line_cap", out var lineCapValue)
                    ? lineCapValue.ToString(CultureInfo.InvariantCulture)
                    : "_missing_";
                var lineJoin = TryGetInt64(item, "line_join", out var lineJoinValue)
                    ? lineJoinValue.ToString(CultureInfo.InvariantCulture)
                    : "_missing_";
                var dashPhase = TryGetDouble(item, "dash_phase", out var dashPhaseValue)
                    ? FormatRoundedNumber(dashPhaseValue, decimals)
                    : "_missing_";
                var dashArraySignature = ComputeShapeDashArraySignature(item, decimals);
                var pointSignature = ComputeShapePointSignature(item, decimals);
                var strokingSignature = ComputeColorSignature(item, "rgb_stroking");
                var fillingSignature = ComputeColorSignature(item, "rgb_filling");

                return string.Join('|',
                    index,
                    parentId,
                    coordOrigin,
                    hasGraphicsState,
                    lineWidth,
                    miterLimit,
                    lineCap,
                    lineJoin,
                    dashPhase,
                    dashArraySignature,
                    pointSignature,
                    strokingSignature,
                    fillingSignature);
            })
            .OrderBy(static signature => signature, StringComparer.Ordinal)
            .ToArray();

        return ComputeSequenceHash(signatures);
    }

    private static List<double> ExtractNumericPropertyValues(JsonElement array, string property)
    {
        var values = new List<double>();

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty(property, out var value) ||
                !value.TryGetDouble(out var number))
            {
                continue;
            }

            values.Add(number);
        }

        return values;
    }

    private static string ComputeEmbeddedRectSignature(JsonElement root, int roundingDecimals)
    {
        if (!TryGetProperty(root, "rect", out var rect) || rect.ValueKind != JsonValueKind.Object)
        {
            return "_missing_rect_";
        }

        var parts = new List<string>(RectProperties.Length);
        foreach (var property in RectProperties)
        {
            if (!rect.TryGetProperty(property, out var value) || !value.TryGetDouble(out var number))
            {
                parts.Add("_missing_");
                continue;
            }

            parts.Add(FormatRoundedNumber(number, roundingDecimals));
        }

        return string.Join('|', parts);
    }

    private static string ComputeShapeDashArraySignature(JsonElement shape, int roundingDecimals)
    {
        if (!TryGetProperty(shape, "dash_array", out var dashArray) || dashArray.ValueKind != JsonValueKind.Array)
        {
            return "_missing_";
        }

        var values = dashArray.EnumerateArray()
            .Select(value => value.TryGetDouble(out var number)
                ? FormatRoundedNumber(number, roundingDecimals)
                : "_missing_");

        return ComputeSequenceHash(values);
    }

    private static string ComputeShapePointSignature(JsonElement shape, int roundingDecimals)
    {
        if (!TryGetProperty(shape, "points", out var points) || points.ValueKind != JsonValueKind.Array)
        {
            return "_missing_";
        }

        var pointPairs = points.EnumerateArray()
            .Select(point =>
            {
                if (point.ValueKind != JsonValueKind.Array)
                {
                    return "_invalid_point_";
                }

                var coordinates = point.EnumerateArray()
                    .Select(value => value.TryGetDouble(out var number)
                        ? FormatRoundedNumber(number, roundingDecimals)
                        : "_missing_");
                return string.Join(',', coordinates);
            });

        return ComputeSequenceHash(pointPairs);
    }

    private static string ComputeColorSignature(JsonElement root, string property)
    {
        if (!TryGetProperty(root, property, out var color) || color.ValueKind != JsonValueKind.Object)
        {
            return "_missing_color_";
        }

        var r = TryGetInt64(color, "r", out var rValue)
            ? rValue.ToString(CultureInfo.InvariantCulture)
            : "_missing_";
        var g = TryGetInt64(color, "g", out var gValue)
            ? gValue.ToString(CultureInfo.InvariantCulture)
            : "_missing_";
        var b = TryGetInt64(color, "b", out var bValue)
            ? bValue.ToString(CultureInfo.InvariantCulture)
            : "_missing_";
        var a = TryGetInt64(color, "a", out var aValue)
            ? aValue.ToString(CultureInfo.InvariantCulture)
            : "_missing_";

        return $"{r},{g},{b},{a}";
    }

    private static string FormatRoundedNumber(double value, int roundingDecimals)
    {
        var decimals = Math.Clamp(roundingDecimals, 0, 8);
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        return rounded.ToString(CultureInfo.InvariantCulture);
    }

    private static string ComputeStringHash(string value)
    {
        return ComputeSequenceHash([value]);
    }

    private static List<int> ExtractIntegerPropertyValues(JsonElement array, string property)
    {
        var values = new List<int>();

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty(property, out var value) ||
                !value.TryGetInt32(out var number))
            {
                continue;
            }

            values.Add(number);
        }

        return values;
    }

    private static string ComputeSequenceHash(IEnumerable<string> values)
    {
        using var sha = SHA256.Create();
        var separator = new byte[] { 0x1F };

        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            sha.TransformBlock(bytes, 0, bytes.Length, outputBuffer: null, outputOffset: 0);
            sha.TransformBlock(separator, 0, separator.Length, outputBuffer: null, outputOffset: 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!);
    }
}
