using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoclingDotNet.Models;

public sealed class SegmentedPdfPageDto
{
    [JsonPropertyName("dimension")]
    public PdfPageGeometryDto Dimension { get; set; } = new();

    [JsonPropertyName("bitmap_resources")]
    public List<BitmapResourceDto> BitmapResources { get; set; } = [];

    [JsonPropertyName("char_cells")]
    public List<PdfTextCellDto> CharCells { get; set; } = [];

    [JsonPropertyName("word_cells")]
    public List<PdfTextCellDto> WordCells { get; set; } = [];

    [JsonPropertyName("textline_cells")]
    public List<PdfTextCellDto> TextlineCells { get; set; } = [];

    [JsonPropertyName("has_chars")]
    public bool HasChars { get; set; }

    [JsonPropertyName("has_words")]
    public bool HasWords { get; set; }

    [JsonPropertyName("has_lines")]
    public bool HasLines { get; set; }

    [JsonPropertyName("widgets")]
    public List<PdfWidgetDto> Widgets { get; set; } = [];

    [JsonPropertyName("hyperlinks")]
    public List<PdfHyperlinkDto> Hyperlinks { get; set; } = [];

    [JsonPropertyName("lines")]
    public List<JsonElement> Lines { get; set; } = [];

    [JsonPropertyName("shapes")]
    public List<PdfShapeDto> Shapes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class PdfPageGeometryDto
{
    [JsonPropertyName("angle")]
    public double Angle { get; set; }

    [JsonPropertyName("rect")]
    public BoundingRectangleDto Rect { get; set; } = new();

    [JsonPropertyName("boundary_type")]
    public string BoundaryType { get; set; } = string.Empty;

    [JsonPropertyName("art_bbox")]
    public BoundingBoxDto ArtBbox { get; set; } = new();

    [JsonPropertyName("bleed_bbox")]
    public BoundingBoxDto BleedBbox { get; set; } = new();

    [JsonPropertyName("crop_bbox")]
    public BoundingBoxDto CropBbox { get; set; } = new();

    [JsonPropertyName("media_bbox")]
    public BoundingBoxDto MediaBbox { get; set; } = new();

    [JsonPropertyName("trim_bbox")]
    public BoundingBoxDto TrimBbox { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class BoundingRectangleDto
{
    [JsonPropertyName("r_x0")]
    public double RX0 { get; set; }

    [JsonPropertyName("r_y0")]
    public double RY0 { get; set; }

    [JsonPropertyName("r_x1")]
    public double RX1 { get; set; }

    [JsonPropertyName("r_y1")]
    public double RY1 { get; set; }

    [JsonPropertyName("r_x2")]
    public double RX2 { get; set; }

    [JsonPropertyName("r_y2")]
    public double RY2 { get; set; }

    [JsonPropertyName("r_x3")]
    public double RX3 { get; set; }

    [JsonPropertyName("r_y3")]
    public double RY3 { get; set; }

    [JsonPropertyName("coord_origin")]
    public string CoordOrigin { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class BoundingBoxDto
{
    [JsonPropertyName("l")]
    public double L { get; set; }

    [JsonPropertyName("t")]
    public double T { get; set; }

    [JsonPropertyName("r")]
    public double R { get; set; }

    [JsonPropertyName("b")]
    public double B { get; set; }

    [JsonPropertyName("coord_origin")]
    public string CoordOrigin { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class BitmapResourceDto
{
    [JsonPropertyName("index")]
    public long Index { get; set; }

    [JsonPropertyName("rect")]
    public BoundingRectangleDto Rect { get; set; } = new();

    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("image")]
    public BitmapImageDto? Image { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class BitmapImageDto
{
    [JsonPropertyName("mimetype")]
    public string? Mimetype { get; set; }

    [JsonPropertyName("dpi")]
    public long? Dpi { get; set; }

    [JsonPropertyName("size")]
    public BitmapImageSizeDto? Size { get; set; }

    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class BitmapImageSizeDto
{
    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class TrackSourceDto
{
    [JsonPropertyName("start_time")]
    public double? StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public double? EndTime { get; set; }

    [JsonPropertyName("voice")]
    public string? Voice { get; set; }
}

public sealed class PdfTextCellDto
{
    [JsonPropertyName("index")]
    public long Index { get; set; }

    [JsonPropertyName("rgba")]
    public ColorRgbaDto Rgba { get; set; } = new();

    [JsonPropertyName("rect")]
    public BoundingRectangleDto Rect { get; set; } = new();

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("orig")]
    public string Orig { get; set; } = string.Empty;

    [JsonPropertyName("text_direction")]
    public string TextDirection { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("from_ocr")]
    public bool FromOcr { get; set; }

    [JsonPropertyName("rendering_mode")]
    public long RenderingMode { get; set; }

    [JsonPropertyName("widget")]
    public bool Widget { get; set; }

    [JsonPropertyName("font_key")]
    public string FontKey { get; set; } = string.Empty;

    [JsonPropertyName("font_name")]
    public string FontName { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public TrackSourceDto? Source { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class ColorRgbaDto
{
    [JsonPropertyName("r")]
    public int R { get; set; }

    [JsonPropertyName("g")]
    public int G { get; set; }

    [JsonPropertyName("b")]
    public int B { get; set; }

    [JsonPropertyName("a")]
    public int A { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class PdfWidgetDto
{
    [JsonPropertyName("index")]
    public long Index { get; set; }

    [JsonPropertyName("rect")]
    public BoundingRectangleDto Rect { get; set; } = new();

    [JsonPropertyName("widget_text")]
    public string? WidgetText { get; set; }

    [JsonPropertyName("widget_description")]
    public string? WidgetDescription { get; set; }

    [JsonPropertyName("widget_field_name")]
    public string? WidgetFieldName { get; set; }

    [JsonPropertyName("widget_field_type")]
    public string? WidgetFieldType { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class PdfHyperlinkDto
{
    [JsonPropertyName("index")]
    public long Index { get; set; }

    [JsonPropertyName("rect")]
    public BoundingRectangleDto Rect { get; set; } = new();

    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class PdfShapeDto
{
    [JsonPropertyName("index")]
    public long Index { get; set; }

    [JsonPropertyName("parent_id")]
    public long ParentId { get; set; }

    [JsonPropertyName("points")]
    public List<List<double>> Points { get; set; } = [];

    [JsonPropertyName("coord_origin")]
    public string CoordOrigin { get; set; } = string.Empty;

    [JsonPropertyName("has_graphics_state")]
    public bool HasGraphicsState { get; set; }

    [JsonPropertyName("line_width")]
    public double LineWidth { get; set; }

    [JsonPropertyName("miter_limit")]
    public double MiterLimit { get; set; }

    [JsonPropertyName("line_cap")]
    public long LineCap { get; set; }

    [JsonPropertyName("line_join")]
    public long LineJoin { get; set; }

    [JsonPropertyName("dash_phase")]
    public double DashPhase { get; set; }

    [JsonPropertyName("dash_array")]
    public List<double> DashArray { get; set; } = [];

    [JsonPropertyName("flatness")]
    public double Flatness { get; set; }

    [JsonPropertyName("rgb_stroking")]
    public ColorRgbaDto RgbStroking { get; set; } = new();

    [JsonPropertyName("rgb_filling")]
    public ColorRgbaDto RgbFilling { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
