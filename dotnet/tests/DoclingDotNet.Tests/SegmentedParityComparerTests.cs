using System.Globalization;
using System.Text.Json;
using DoclingDotNet.Parity;
using Xunit;

namespace DoclingDotNet.Tests;

public sealed class SegmentedParityComparerTests
{
    [Fact]
    public void Compare_IdenticalPayloads_ReturnsNoDiffs()
    {
        var json = CreateBaselineJson();

        var result = SegmentedParityComparer.Compare(json, json);

        Assert.True(result.IsExactMatch);
        Assert.Empty(result.Diffs);
    }

    [Fact]
    public void Compare_MissingKeyAndBooleanMismatch_ReportsCriticalAndMajorDiffs()
    {
        var expected = CreateBaselineJson();
        using var actualDoc = JsonDocument.Parse(CreateBaselineJson());
        var root = actualDoc.RootElement;

        var mutatedActual = $$"""
{
  "dimension": {{root.GetProperty("dimension").GetRawText()}},
  "bitmap_resources": [],
  "char_cells": [],
  "word_cells": [],
  "textline_cells": [],
  "has_chars": false,
  "has_words": false,
  "has_lines": false,
  "widgets": [],
  "hyperlinks": [],
  "lines": []
}
""";

        var result = SegmentedParityComparer.Compare(expected, mutatedActual);

        Assert.Contains(result.Diffs, diff => diff.Code == "missing_required_top_level_key" && diff.Severity == ParitySeverity.Critical);
        Assert.Contains(result.Diffs, diff => diff.Code == "boolean_mismatch" && diff.Severity == ParitySeverity.Major);
    }

    [Fact]
    public void Compare_TextHashMismatch_IsMajor()
    {
        var expected = CreateBaselineJson("alpha");
        var actual = CreateBaselineJson("beta");

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(result.Diffs, diff => diff.Code == "text_hash_mismatch" && diff.Severity == ParitySeverity.Major);
    }

    [Fact]
    public void Compare_TextFieldSemanticMismatch_DefaultsToMajor()
    {
        var expected = CreateBaselineJson(textDirection: "left_to_right");
        var actual = CreateBaselineJson(textDirection: "right_to_left");

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "text_direction_distribution_mismatch" && diff.Severity == ParitySeverity.Major);
    }

    [Fact]
    public void Compare_TextFieldSemanticMismatch_RespectsConfiguredSeverity()
    {
        var expected = CreateBaselineJson(origText: "alpha");
        var actual = CreateBaselineJson(origText: "beta");
        var options = new SegmentedParityOptions
        {
            TextMismatchSeverity = ParitySeverity.Minor
        };

        var result = SegmentedParityComparer.Compare(expected, actual, options);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "text_orig_equality_count_mismatch" && diff.Severity == ParitySeverity.Minor);
    }

    [Fact]
    public void Compare_TextSemanticSequenceMismatch_IsMajor()
    {
        var expected = CreateBaselineJson(
            charCellsJson: CreateCharCellsJson(
                (index: 0, text: "A", orig: "A"),
                (index: 1, text: "B", orig: "C")));
        var actual = CreateBaselineJson(
            charCellsJson: CreateCharCellsJson(
                (index: 0, text: "A", orig: "C"),
                (index: 1, text: "B", orig: "B")));

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "text_semantic_sequence_mismatch" && diff.Severity == ParitySeverity.Major);
    }

    [Fact]
    public void Compare_TextIndexValueMapMismatch_IsMajor()
    {
        var expected = CreateBaselineJson(
            charCellsJson: CreateCharCellsJson(
                (index: 0, text: "A", orig: "A"),
                (index: 1, text: "B", orig: "B")));
        var actual = CreateBaselineJson(
            charCellsJson: CreateCharCellsJson(
                (index: 1, text: "B", orig: "B"),
                (index: 0, text: "A", orig: "C")));

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "text_index_value_map_mismatch" && diff.Severity == ParitySeverity.Major);
    }

    [Fact]
    public void Compare_TextIndexIntegrityMismatch_IsMajor()
    {
        var expected = CreateBaselineJson(
            charCellsJson: CreateCharCellsJson(
                (index: 0, text: "A", orig: "A"),
                (index: 1, text: "B", orig: "B")));
        var actual = CreateBaselineJson(
            charCellsJson: CreateCharCellsJson(
                (index: 0, text: "A", orig: "A"),
                (index: 0, text: "B", orig: "B")));

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "text_index_integrity_mismatch" && diff.Severity == ParitySeverity.Major);
    }

    [Fact]
    public void Compare_TextLengthDistributionMismatch_IsMajor()
    {
        var expected = CreateBaselineJson(
            charCellsJson: CreateCharCellsJson(
                (index: 0, text: "AA", orig: "AA"),
                (index: 1, text: "B", orig: "B")));
        var actual = CreateBaselineJson(
            charCellsJson: CreateCharCellsJson(
                (index: 0, text: "A", orig: "AA"),
                (index: 1, text: "BBB", orig: "B")));

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "text_length_distribution_mismatch" && diff.Severity == ParitySeverity.Major);
    }

    [Fact]
    public void Compare_TextOrigLengthDeltaDistributionMismatch_IsMajor()
    {
        var expected = CreateBaselineJson(
            charCellsJson: CreateCharCellsJson(
                (index: 0, text: "AA", orig: "AA"),
                (index: 1, text: "B", orig: "B")));
        var actual = CreateBaselineJson(
            charCellsJson: CreateCharCellsJson(
                (index: 0, text: "AA", orig: "A"),
                (index: 1, text: "B", orig: "BBB")));

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "text_orig_length_delta_distribution_mismatch" && diff.Severity == ParitySeverity.Major);
    }

    [Fact]
    public void Compare_OcrFromFlagMismatch_IsMajor()
    {
        var expected = CreateBaselineJson(fromOcr: false);
        var actual = CreateBaselineJson(fromOcr: true);

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "ocr_from_ocr_count_mismatch" && diff.Severity == ParitySeverity.Major);
    }

    [Fact]
    public void Compare_GeometrySignatureMismatch_DefaultsToMinor()
    {
        var expected = CreateBaselineJson(charRectX1: 1.0);
        var actual = CreateBaselineJson(charRectX1: 1.5);

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "geometry_signature_mismatch" && diff.Severity == ParitySeverity.Minor);
    }

    [Fact]
    public void Compare_GeometrySignatureMismatch_RespectsConfiguredSeverity()
    {
        var expected = CreateBaselineJson(charRectX1: 1.0);
        var actual = CreateBaselineJson(charRectX1: 1.5);
        var options = new SegmentedParityOptions
        {
            GeometryMismatchSeverity = ParitySeverity.Major
        };

        var result = SegmentedParityComparer.Compare(expected, actual, options);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "geometry_signature_mismatch" && diff.Severity == ParitySeverity.Major);
    }

    [Fact]
    public void Compare_FontSignatureMismatch_IsMajor()
    {
        var expected = CreateBaselineJson(fontName: "Font-A");
        var actual = CreateBaselineJson(fontName: "Font-B");

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "font_signature_mismatch" && diff.Severity == ParitySeverity.Major);
    }

    [Fact]
    public void Compare_ConfidenceMeanMismatch_IsMinor()
    {
        var expected = CreateBaselineJson(confidence: 1.0);
        var actual = CreateBaselineJson(confidence: 0.75);

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "confidence_mean_mismatch" && diff.Severity == ParitySeverity.Minor);
    }

    [Fact]
    public void Compare_ReadingOrderIndexMismatch_IsMajor()
    {
        var expected = CreateBaselineJson(
            textlineCellsJson: CreateTextlineCellsJson(firstIndex: 0, secondIndex: 1));
        var actual = CreateBaselineJson(
            textlineCellsJson: CreateTextlineCellsJson(firstIndex: 1, secondIndex: 0));

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "reading_order_index_mismatch" && diff.Severity == ParitySeverity.Major);
    }

    [Fact]
    public void Compare_BitmapResourceSignatureMismatch_DefaultsToMinor()
    {
        var expected = CreateBaselineJson(bitmapResourcesJson: CreateBitmapResourcesJson(uri: "https://example.test/a.png"));
        var actual = CreateBaselineJson(bitmapResourcesJson: CreateBitmapResourcesJson(uri: "https://example.test/b.png"));

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "bitmap_resource_signature_mismatch" && diff.Severity == ParitySeverity.Minor);
    }

    [Fact]
    public void Compare_BitmapResourceSemanticMismatch_DefaultsToMinor()
    {
        var expected = CreateBaselineJson(bitmapResourcesJson: CreateBitmapResourcesJson(mode: "embedded"));
        var actual = CreateBaselineJson(bitmapResourcesJson: CreateBitmapResourcesJson(mode: "external"));

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "bitmap_mode_distribution_mismatch" && diff.Severity == ParitySeverity.Minor);
    }

    [Fact]
    public void Compare_WidgetSignatureMismatch_DefaultsToMinor()
    {
        var expected = CreateBaselineJson(widgetsJson: CreateWidgetsJson(fieldType: "Text"));
        var actual = CreateBaselineJson(widgetsJson: CreateWidgetsJson(fieldType: "Checkbox"));

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "widget_signature_mismatch" && diff.Severity == ParitySeverity.Minor);
    }

    [Fact]
    public void Compare_WidgetSemanticMismatch_DefaultsToMinor()
    {
        var expected = CreateBaselineJson(widgetsJson: CreateWidgetsJson(fieldType: "Text"));
        var actual = CreateBaselineJson(widgetsJson: CreateWidgetsJson(fieldType: "Checkbox"));

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "widget_field_type_distribution_mismatch" && diff.Severity == ParitySeverity.Minor);
    }

    [Fact]
    public void Compare_HyperlinkSignatureMismatch_DefaultsToMinor()
    {
        var expected = CreateBaselineJson(hyperlinksJson: CreateHyperlinksJson(uri: "https://example.test/a"));
        var actual = CreateBaselineJson(hyperlinksJson: CreateHyperlinksJson(uri: "https://example.test/b"));

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "hyperlink_signature_mismatch" && diff.Severity == ParitySeverity.Minor);
    }

    [Fact]
    public void Compare_HyperlinkSemanticMismatch_DefaultsToMinor()
    {
        var expected = CreateBaselineJson(hyperlinksJson: CreateHyperlinksJson(uri: "https://example.test/a"));
        var actual = CreateBaselineJson(hyperlinksJson: CreateHyperlinksJson(uri: "mailto:test@example.test"));

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "hyperlink_scheme_distribution_mismatch" && diff.Severity == ParitySeverity.Minor);
    }

    [Fact]
    public void Compare_ShapeSignatureMismatch_DefaultsToMinor()
    {
        var expected = CreateBaselineJson(shapesJson: CreateShapesJson(lineWidth: 1.0));
        var actual = CreateBaselineJson(shapesJson: CreateShapesJson(lineWidth: 2.0));

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "shape_signature_mismatch" && diff.Severity == ParitySeverity.Minor);
    }

    [Fact]
    public void Compare_ShapeSemanticMismatch_DefaultsToMinor()
    {
        var expected = CreateBaselineJson(shapesJson: CreateShapesJson(hasGraphicsState: true));
        var actual = CreateBaselineJson(shapesJson: CreateShapesJson(hasGraphicsState: false));

        var result = SegmentedParityComparer.Compare(expected, actual);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "shape_graphics_state_count_mismatch" && diff.Severity == ParitySeverity.Minor);
    }

    [Fact]
    public void Compare_ContextSignatureMismatch_RespectsConfiguredSeverity()
    {
        var expected = CreateBaselineJson(widgetsJson: CreateWidgetsJson(fieldType: "Text"));
        var actual = CreateBaselineJson(widgetsJson: CreateWidgetsJson(fieldType: "Checkbox"));
        var options = new SegmentedParityOptions
        {
            ContextMismatchSeverity = ParitySeverity.Major
        };

        var result = SegmentedParityComparer.Compare(expected, actual, options);

        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "widget_signature_mismatch" && diff.Severity == ParitySeverity.Major);
        Assert.Contains(
            result.Diffs,
            diff => diff.Code == "widget_field_type_distribution_mismatch" && diff.Severity == ParitySeverity.Major);
    }

    private static string CreateBaselineJson(
        string text = "alpha",
        bool fromOcr = false,
        double charRectX1 = 1.0,
        double confidence = 1.0,
        string fontName = "",
        string textDirection = "left_to_right",
        int renderingMode = 0,
        bool widget = false,
        string? origText = null,
        string? charCellsJson = null,
        string textlineCellsJson = "[]",
        string bitmapResourcesJson = "[]",
        string widgetsJson = "[]",
        string hyperlinksJson = "[]",
        string shapesJson = "[]")
    {
        var fromOcrLiteral = fromOcr ? "true" : "false";
        var charRectX1Literal = charRectX1.ToString("G17", CultureInfo.InvariantCulture);
        var confidenceLiteral = confidence.ToString("G17", CultureInfo.InvariantCulture);
        var escapedFontName = fontName.Replace("\"", "\\\"", StringComparison.Ordinal);
        var escapedTextDirection = textDirection.Replace("\"", "\\\"", StringComparison.Ordinal);
        var renderingModeLiteral = renderingMode.ToString(CultureInfo.InvariantCulture);
        var widgetLiteral = widget ? "true" : "false";
        var origValue = origText ?? text;
        var escapedOrig = origValue.Replace("\"", "\\\"", StringComparison.Ordinal);
        var defaultCharCellsJson = $$"""
[
  {
    "index": 0,
    "rgba": { "r": 0, "g": 0, "b": 0, "a": 255 },
    "rect": {
      "r_x0": 0.0,
      "r_y0": 0.0,
      "r_x1": {{charRectX1Literal}},
      "r_y1": 0.0,
      "r_x2": 1.0,
      "r_y2": 1.0,
      "r_x3": 0.0,
      "r_y3": 1.0,
      "coord_origin": "BOTTOMLEFT"
    },
    "text": "{{text}}",
    "orig": "{{escapedOrig}}",
    "text_direction": "{{escapedTextDirection}}",
    "confidence": {{confidenceLiteral}},
    "from_ocr": {{fromOcrLiteral}},
    "rendering_mode": {{renderingModeLiteral}},
    "widget": {{widgetLiteral}},
    "font_key": "",
    "font_name": "{{escapedFontName}}"
  }
]
""";
        var charCellsPayload = charCellsJson ?? defaultCharCellsJson;

        return $$"""
{
  "dimension": {
    "angle": 0.0,
    "rect": {
      "r_x0": 0.0,
      "r_y0": 0.0,
      "r_x1": 1.0,
      "r_y1": 0.0,
      "r_x2": 1.0,
      "r_y2": 1.0,
      "r_x3": 0.0,
      "r_y3": 1.0,
      "coord_origin": "BOTTOMLEFT"
    },
    "boundary_type": "crop_box",
    "art_bbox": { "l": 0.0, "t": 1.0, "r": 1.0, "b": 0.0, "coord_origin": "BOTTOMLEFT" },
    "bleed_bbox": { "l": 0.0, "t": 1.0, "r": 1.0, "b": 0.0, "coord_origin": "BOTTOMLEFT" },
    "crop_bbox": { "l": 0.0, "t": 1.0, "r": 1.0, "b": 0.0, "coord_origin": "BOTTOMLEFT" },
    "media_bbox": { "l": 0.0, "t": 1.0, "r": 1.0, "b": 0.0, "coord_origin": "BOTTOMLEFT" },
    "trim_bbox": { "l": 0.0, "t": 1.0, "r": 1.0, "b": 0.0, "coord_origin": "BOTTOMLEFT" }
  },
  "bitmap_resources": {{bitmapResourcesJson}},
  "char_cells": {{charCellsPayload}},
  "word_cells": [],
  "textline_cells": {{textlineCellsJson}},
  "has_chars": true,
  "has_words": false,
  "has_lines": false,
  "widgets": {{widgetsJson}},
  "hyperlinks": {{hyperlinksJson}},
  "lines": [],
  "shapes": {{shapesJson}}
}
""";
    }

    private static string CreateCharCellsJson(params (int index, string text, string orig)[] cells)
    {
        var entries = cells.Select(cell => $$"""
  {
    "index": {{cell.index}},
    "rgba": { "r": 0, "g": 0, "b": 0, "a": 255 },
    "rect": {
      "r_x0": 0.0,
      "r_y0": 0.0,
      "r_x1": 1.0,
      "r_y1": 0.0,
      "r_x2": 1.0,
      "r_y2": 1.0,
      "r_x3": 0.0,
      "r_y3": 1.0,
      "coord_origin": "BOTTOMLEFT"
    },
    "text": "{{cell.text}}",
    "orig": "{{cell.orig}}",
    "text_direction": "left_to_right",
    "confidence": 1.0,
    "from_ocr": false,
    "rendering_mode": 0,
    "widget": false,
    "font_key": "",
    "font_name": ""
  }
""");

        return "[\n" + string.Join(",\n", entries) + "\n]";
    }

    private static string CreateTextlineCellsJson(int firstIndex, int secondIndex)
    {
        return $$"""
[
  {
    "index": {{firstIndex}},
    "rgba": { "r": 0, "g": 0, "b": 0, "a": 255 },
    "rect": {
      "r_x0": 0.0,
      "r_y0": 0.0,
      "r_x1": 1.0,
      "r_y1": 0.0,
      "r_x2": 1.0,
      "r_y2": 1.0,
      "r_x3": 0.0,
      "r_y3": 1.0,
      "coord_origin": "BOTTOMLEFT"
    },
    "text": "line-a",
    "orig": "line-a",
    "text_direction": "left_to_right",
    "confidence": 0.9,
    "from_ocr": false,
    "rendering_mode": 0,
    "widget": false,
    "font_key": "font_a",
    "font_name": "FontA"
  },
  {
    "index": {{secondIndex}},
    "rgba": { "r": 0, "g": 0, "b": 0, "a": 255 },
    "rect": {
      "r_x0": 1.0,
      "r_y0": 0.0,
      "r_x1": 2.0,
      "r_y1": 0.0,
      "r_x2": 2.0,
      "r_y2": 1.0,
      "r_x3": 1.0,
      "r_y3": 1.0,
      "coord_origin": "BOTTOMLEFT"
    },
    "text": "line-b",
    "orig": "line-b",
    "text_direction": "left_to_right",
    "confidence": 0.8,
    "from_ocr": false,
    "rendering_mode": 0,
    "widget": false,
    "font_key": "font_b",
    "font_name": "FontB"
  }
]
""";
    }

    private static string CreateBitmapResourcesJson(string uri = "https://example.test/a.png", string mode = "embedded")
    {
        return $$"""
[
  {
    "index": 0,
    "rect": {
      "r_x0": 0.0,
      "r_y0": 0.0,
      "r_x1": 1.0,
      "r_y1": 0.0,
      "r_x2": 1.0,
      "r_y2": 1.0,
      "r_x3": 0.0,
      "r_y3": 1.0,
      "coord_origin": "BOTTOMLEFT"
    },
    "mode": "{{mode}}",
    "uri": "{{uri}}",
    "image": {
      "mimetype": "image/png",
      "dpi": 300,
      "size": { "width": 100.0, "height": 200.0 },
      "uri": "{{uri}}"
    }
  }
]
""";
    }

    private static string CreateWidgetsJson(string fieldType)
    {
        return $$"""
[
  {
    "index": 0,
    "rect": {
      "r_x0": 0.0,
      "r_y0": 0.0,
      "r_x1": 1.0,
      "r_y1": 0.0,
      "r_x2": 1.0,
      "r_y2": 1.0,
      "r_x3": 0.0,
      "r_y3": 1.0,
      "coord_origin": "BOTTOMLEFT"
    },
    "widget_text": "Name",
    "widget_description": "Name field",
    "widget_field_name": "name",
    "widget_field_type": "{{fieldType}}"
  }
]
""";
    }

    private static string CreateHyperlinksJson(string uri)
    {
        return $$"""
[
  {
    "index": 0,
    "rect": {
      "r_x0": 0.0,
      "r_y0": 0.0,
      "r_x1": 1.0,
      "r_y1": 0.0,
      "r_x2": 1.0,
      "r_y2": 1.0,
      "r_x3": 0.0,
      "r_y3": 1.0,
      "coord_origin": "BOTTOMLEFT"
    },
    "uri": "{{uri}}"
  }
]
""";
    }

    private static string CreateShapesJson(double lineWidth = 1.0, bool hasGraphicsState = true)
    {
        var lineWidthLiteral = lineWidth.ToString("G17", CultureInfo.InvariantCulture);
        var hasGraphicsStateLiteral = hasGraphicsState ? "true" : "false";
        return $$"""
[
  {
    "index": 0,
    "parent_id": 0,
    "points": [[0.0, 0.0], [1.0, 1.0]],
    "coord_origin": "BOTTOMLEFT",
    "has_graphics_state": {{hasGraphicsStateLiteral}},
    "line_width": {{lineWidthLiteral}},
    "miter_limit": 10.0,
    "line_cap": 0,
    "line_join": 1,
    "dash_phase": 0.0,
    "dash_array": [1.0, 2.0],
    "flatness": 0.0,
    "rgb_stroking": { "r": 0, "g": 0, "b": 0, "a": 255 },
    "rgb_filling": { "r": 255, "g": 255, "b": 255, "a": 255 }
  }
]
""";
    }
}
