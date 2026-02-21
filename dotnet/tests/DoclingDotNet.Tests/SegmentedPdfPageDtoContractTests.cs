using System.Text.Json;
using DoclingDotNet.Models;
using DoclingDotNet.Serialization;
using Xunit;

namespace DoclingDotNet.Tests;

public sealed class SegmentedPdfPageDtoContractTests
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

    [Fact]
    public void GroundTruthCorpus_DeserializesAndPreservesTopLevelKeys()
    {
        var files = EnumerateGroundTruthFiles().ToList();
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var sourceJson = File.ReadAllText(file);
            var sourceKeys = GetTopLevelKeys(sourceJson);
            Assert.Equal(RequiredTopLevelKeys, sourceKeys);

            var dto = DoclingJson.DeserializeSegmentedPage(sourceJson);
            var roundTripJson = DoclingJson.SerializeSegmentedPage(dto);
            var roundTripKeys = GetTopLevelKeys(roundTripJson);
            Assert.Equal(sourceKeys, roundTripKeys);
        }
    }

    [Fact]
    public void Serialization_UsesExactSnakeCaseNames()
    {
        var dto = CreateMinimalSegmentedPage();
        var json = DoclingJson.SerializeSegmentedPage(dto);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("char_cells", out _));
        Assert.False(root.TryGetProperty("charCells", out _));
        Assert.True(root.GetProperty("dimension").TryGetProperty("boundary_type", out _));
        Assert.False(root.GetProperty("dimension").TryGetProperty("boundaryType", out _));
    }

    [Fact]
    public void ExtensionData_RoundTripsUnknownFields()
    {
        const string json = """
{
  "dimension": {
    "angle": 0.0,
    "rect": {
      "r_x0": 0.0, "r_y0": 0.0, "r_x1": 1.0, "r_y1": 0.0,
      "r_x2": 1.0, "r_y2": 1.0, "r_x3": 0.0, "r_y3": 1.0,
      "coord_origin": "BOTTOMLEFT"
    },
    "boundary_type": "crop_box",
    "art_bbox": { "l": 0.0, "t": 1.0, "r": 1.0, "b": 0.0, "coord_origin": "BOTTOMLEFT" },
    "bleed_bbox": { "l": 0.0, "t": 1.0, "r": 1.0, "b": 0.0, "coord_origin": "BOTTOMLEFT" },
    "crop_bbox": { "l": 0.0, "t": 1.0, "r": 1.0, "b": 0.0, "coord_origin": "BOTTOMLEFT" },
    "media_bbox": { "l": 0.0, "t": 1.0, "r": 1.0, "b": 0.0, "coord_origin": "BOTTOMLEFT" },
    "trim_bbox": { "l": 0.0, "t": 1.0, "r": 1.0, "b": 0.0, "coord_origin": "BOTTOMLEFT" },
    "unknown_dimension": "keep-me"
  },
  "bitmap_resources": [],
  "char_cells": [],
  "word_cells": [],
  "textline_cells": [],
  "has_chars": false,
  "has_words": false,
  "has_lines": false,
  "widgets": [],
  "hyperlinks": [],
  "lines": [],
  "shapes": [],
  "unknown_top_level": 123
}
""";

        var dto = DoclingJson.DeserializeSegmentedPage(json);
        var roundTrip = DoclingJson.SerializeSegmentedPage(dto);

        using var doc = JsonDocument.Parse(roundTrip);
        Assert.True(doc.RootElement.TryGetProperty("unknown_top_level", out _));
        Assert.True(doc.RootElement.GetProperty("dimension").TryGetProperty("unknown_dimension", out _));
    }

    [Fact]
    public void NativeEnvelopeProjection_MapsAvailableFieldsAndDefaultsMissingWordLineCells()
    {
        var native = new NativeDecodedPageDto
        {
            PageNumber = 0,
            Original = new NativeDecodedPagePayloadDto
            {
                Dimension = CreateMinimalSegmentedPage().Dimension,
                Images = [],
                Cells = [new PdfTextCellDto { Index = 7, Text = "x", Orig = "x", TextDirection = "left_to_right" }],
                Shapes = [],
                Widgets = [],
                Hyperlinks = []
            }
        };

        var projected = DoclingDotNet.Projection.NativeDecodedPageProjector.ToSegmentedPdfPage(native);

        Assert.Single(projected.CharCells);
        Assert.Equal(7, projected.CharCells[0].Index);
        Assert.Empty(projected.WordCells);
        Assert.Empty(projected.TextlineCells);
        Assert.True(projected.HasChars);
        Assert.False(projected.HasWords);
        Assert.False(projected.HasLines);
    }

    private static SegmentedPdfPageDto CreateMinimalSegmentedPage()
    {
        return new SegmentedPdfPageDto
        {
            Dimension = new PdfPageGeometryDto
            {
                Angle = 0.0,
                BoundaryType = "crop_box",
                Rect = new BoundingRectangleDto
                {
                    RX0 = 0.0,
                    RY0 = 0.0,
                    RX1 = 1.0,
                    RY1 = 0.0,
                    RX2 = 1.0,
                    RY2 = 1.0,
                    RX3 = 0.0,
                    RY3 = 1.0,
                    CoordOrigin = "BOTTOMLEFT"
                },
                ArtBbox = CreateUnitBoundingBox(),
                BleedBbox = CreateUnitBoundingBox(),
                CropBbox = CreateUnitBoundingBox(),
                MediaBbox = CreateUnitBoundingBox(),
                TrimBbox = CreateUnitBoundingBox()
            },
            BitmapResources = [],
            CharCells = [],
            WordCells = [],
            TextlineCells = [],
            HasChars = false,
            HasWords = false,
            HasLines = false,
            Widgets = [],
            Hyperlinks = [],
            Lines = [],
            Shapes = []
        };
    }

    private static BoundingBoxDto CreateUnitBoundingBox()
    {
        return new BoundingBoxDto
        {
            L = 0.0,
            T = 1.0,
            R = 1.0,
            B = 0.0,
            CoordOrigin = "BOTTOMLEFT"
        };
    }

    private static HashSet<string> GetTopLevelKeys(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            keys.Add(property.Name);
        }

        return keys;
    }

    private static IEnumerable<string> EnumerateGroundTruthFiles()
    {
        var root = FindRepositoryRoot();
        var groundTruthPath = Path.Combine(
            root,
            "upstream",
            "deps",
            "docling-parse",
            "tests",
            "data",
            "groundtruth");

        return Directory.EnumerateFiles(groundTruthPath, "*.py.json", SearchOption.TopDirectoryOnly);
    }

    private static string FindRepositoryRoot()
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
}
