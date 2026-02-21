using System.Text.Json;
using System.Text.Json.Serialization;
using DoclingDotNet.Models;

namespace DoclingDotNet.Serialization;

public static class DoclingJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    public static SegmentedPdfPageDto DeserializeSegmentedPage(string json)
    {
        var value = JsonSerializer.Deserialize<SegmentedPdfPageDto>(json, SerializerOptions);
        return value ?? throw new InvalidOperationException("Failed to deserialize segmented PDF page JSON.");
    }

    public static NativeDecodedPageDto DeserializeNativeDecodedPage(string json)
    {
        var value = JsonSerializer.Deserialize<NativeDecodedPageDto>(json, SerializerOptions);
        return value ?? throw new InvalidOperationException("Failed to deserialize native decoded page JSON.");
    }

    public static string SerializeSegmentedPage(SegmentedPdfPageDto page)
    {
        return JsonSerializer.Serialize(page, SerializerOptions);
    }

    public static string SerializeNativeDecodedPage(NativeDecodedPageDto page)
    {
        return JsonSerializer.Serialize(page, SerializerOptions);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
    }
}
