---
name: onnx-tensor-math
description: "Reference guide and workflow for highly optimized image-to-tensor preprocessing for ONNX models (specifically RT-DETR) in .NET."
---

# `onnx-tensor-math` Skill

## Context
Slice 7 requires extracting a PDF page as an image and converting it into a normalized `[1, 3, H, W]` `float32` tensor for ONNX inference (RT-DETR). Image processing in .NET can be a massive performance bottleneck if done naively (e.g., `GetPixel()`). This skill defines the strict, high-performance workflow for creating these tensors.

## Trigger
Use this skill when implementing the `apply_layout_inference` pipeline stage, specifically when building the image extraction and tensor normalization logic for `OnnxLayoutProvider`.

## The RT-DETR Preprocessing Specification
According to the upstream `preprocessor_config.json` for `docling-layout-heron-onnx`:
- **Target Size:** `H = 640`, `W = 640`
- **Format:** RGB (Channels First: `[Batch, Channel, Height, Width]`)
- **Rescale Factor:** `1/255.0` (Scale pixel values from `[0, 255]` to `[0.0, 1.0]`)
- **Mean (RGB):** `[0.485, 0.456, 0.406]`
- **Std (RGB):** `[0.229, 0.224, 0.225]`
- **Padding:** None (`do_pad = false`)
- **Formula:** `tensor_val = ((pixel_val / 255.0) - mean) / std`

## Workflow

### 1. Choose the Image Library
Do **NOT** use `System.Drawing.Common` (it is Windows-only and slow).
**DO** use `SkiaSharp`. It is cross-platform, fast, and provides direct memory access.

### 2. The Vectorized Extraction Pattern
To convert a `SKBitmap` to a `DenseTensor<float>` efficiently, you must use `unsafe` pointers to bypass bounds checking and object allocation overhead.

```csharp
// Example high-performance pattern
using SkiaSharp;
using Microsoft.ML.OnnxRuntime.Tensors;

public static DenseTensor<float> CreateNormalizedTensor(SKBitmap bitmap)
{
    // 1. Resize to target (640x640)
    using var resized = bitmap.Resize(new SKImageInfo(640, 640), SKFilterQuality.Medium);
    
    // Ensure format is exactly 8888 (RGBA or BGRA)
    // ...

    var tensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });
    
    // Pre-calculate constants
    const float scale = 1f / 255f;
    float meanR = 0.485f, meanG = 0.456f, meanB = 0.406f;
    float stdR = 0.229f, stdG = 0.224f, stdB = 0.225f;

    int width = 640;
    int height = 640;
    int channelStride = width * height;

    unsafe
    {
        // 2. Get direct pointer to pixel memory
        byte* srcPtr = (byte*)resized.GetPixels().ToPointer();
        
        // 3. Get direct pointer to tensor memory (using Span/MemoryMarshal or indexing)
        // ... (Implementation specific)
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Calculate source index (assuming 4 bytes per pixel: R, G, B, A)
                int srcIdx = (y * width + x) * 4;
                
                // Extract R, G, B (Handling BGRA vs RGBA depending on Skia platform defaults!)
                byte r = srcPtr[srcIdx]; // (Or srcIdx+2 if BGRA)
                byte g = srcPtr[srcIdx + 1];
                byte b = srcPtr[srcIdx + 2]; // (Or srcIdx if BGRA)

                // Normalize and assign to contiguous channel planes
                int destIdxR = 0 * channelStride + (y * width + x);
                int destIdxG = 1 * channelStride + (y * width + x);
                int destIdxB = 2 * channelStride + (y * width + x);

                // Assuming tensor is backed by a 1D array we can index into
                // tensorBuffer[destIdxR] = ((r * scale) - meanR) / stdR;
            }
        }
    }

    return tensor;
}
```

### 3. Critical Edge Cases to Validate
1. **Color Type (BGRA vs RGBA):** `SkiaSharp` defaults to `SKColorType.Bgra8888` on Windows but `Rgba8888` on Android/Linux. Always explicitly check `bitmap.ColorType` or convert it to a known type before blindly indexing `[srcIdx + 2]`.
2. **Memory Leaks:** Always `Dispose()` or use `using` statements for `SKBitmap`, `SKImage`, and `SKData`.
3. **Bounding Box Descaling:** The ONNX model will output bounding boxes relative to the `[640, 640]` space. You **must** multiply these coordinates by `(OriginalWidth / 640.0)` and `(OriginalHeight / 640.0)` to map them back to the PDF's native coordinate system before passing them to the `LayoutPostprocessor`.