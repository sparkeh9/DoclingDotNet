using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoclingDotNet.Models;

public sealed class NativeDecodedPageDto
{
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    [JsonPropertyName("annotations")]
    public JsonElement? Annotations { get; set; }

    [JsonPropertyName("timings")]
    public Dictionary<string, double>? Timings { get; set; }

    [JsonPropertyName("original")]
    public NativeDecodedPagePayloadDto? Original { get; set; }

    [JsonPropertyName("sanitized")]
    public NativeDecodedPagePayloadDto? Sanitized { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class NativeDecodedPagePayloadDto
{
    [JsonPropertyName("dimension")]
    public PdfPageGeometryDto? Dimension { get; set; }

    [JsonPropertyName("images")]
    public List<BitmapResourceDto>? Images { get; set; }

    [JsonPropertyName("cells")]
    public List<PdfTextCellDto>? Cells { get; set; }

    [JsonPropertyName("shapes")]
    public List<PdfShapeDto>? Shapes { get; set; }

    [JsonPropertyName("widgets")]
    public List<PdfWidgetDto>? Widgets { get; set; }

    [JsonPropertyName("hyperlinks")]
    public List<PdfHyperlinkDto>? Hyperlinks { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
