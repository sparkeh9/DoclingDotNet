---
name: python-algorithm-porter
description: "Strict workflow for porting Python algorithms (numpy, pandas, rtree, dict manipulation) to high-performance, zero-allocation C# equivalents."
---

# `python-algorithm-porter` Skill

## Context
When porting algorithms from upstream Python repositories (e.g., `docling`, `docling-core`, `docling-ibm-models`) to the .NET port, direct translation of syntax often leads to poor performance or subtle bugs. Python relies heavily on dynamic typing, implicit broadcasting (numpy), and C-backed native libraries (rtree, bisect). .NET relies on static typing, explicit memory management (`Span<T>`, `Memory<T>`), and JIT inlining.

## Trigger
Use this skill whenever you are tasked with porting a specific algorithm, mathematical function, or complex data transformation from Python to C#.

## Workflow

### 1. Isolate and Analyze (The Python Baseline)
- Locate the target Python function.
- Identify the **input shapes** and **data types** (e.g., `np.ndarray` of `float32` with shape `[1, 3, 640, 640]`, or a `list` of `dict` objects).
- Identify the **algorithmic complexity** (e.g., $O(N^2)$ nested loops, $O(\log N)$ binary searches).
- Identify any **hidden native dependencies** (e.g., `bisect`, `rtree`, `shapely`, `scipy`).

### 2. Design the .NET Data Structures
- **Avoid Classes for Small Data:** Prefer `readonly struct` for geometric data (BoundingBox, Point) to avoid heap allocation and GC pressure.
- **Avoid Boxing:** Use strongly-typed generic collections (`List<T>`, `Dictionary<TKey, TValue>`). Never use `object` or `dynamic`.
- **Memory Efficiency:** If an array is created and discarded within a method (e.g., a temporary buffer for image pixels), consider `ArrayPool<T>.Shared.Rent` or `stackalloc` for small allocations.

### 3. Porting Strategies

#### A. Numpy/Broadcasting -> C# Loops/SIMD
Python:
```python
# Normalize image: (img - mean) / std
img = (img / 255.0 - [0.485, 0.456, 0.406]) / [0.229, 0.224, 0.225]
```
C# Port:
- **Do not** use `System.Drawing.Bitmap.GetPixel()`.
- **Do** use `unsafe` pointers, `Span<T>`, or `MemoryMarshal.Cast` to iterate over flat byte arrays of pixel data.
- **Do** manually unroll the broadcasting into explicit `R`, `G`, `B` scalar operations within a tight `for` loop.

#### B. `bisect` (Sorted Intervals) -> C# `List<T>.BinarySearch`
Python:
```python
import bisect
bisect.insort(my_list, new_item)
```
C# Port:
```csharp
int index = myList.BinarySearch(newItem);
if (index < 0) index = ~index;
myList.Insert(index, newItem);
```

#### C. `rtree` / `libspatialindex` -> C# Spatial Index
Python:
```python
from rtree import index
idx = index.Index()
idx.insert(id, (l, b, r, t))
candidates = idx.intersection((query_l, query_b, query_r, query_t))
```
C# Port:
- If $N < 5000$, a flat `List<T>` with a brute-force $O(N)$ linear scan is often faster in .NET due to cache locality and zero FFI overhead.
- For optimized lookup, use the custom `DoclingDotNet.Algorithms.Spatial.SpatialIndex<T>` (a flat array optimized for zero-allocation intersections).

#### D. Pandas DataFrames -> C# LINQ
Python:
```python
df.groupby('label').agg({'confidence': 'mean'})
```
C# Port:
```csharp
items.GroupBy(x => x.Label)
     .Select(g => new { Label = g.Key, MeanConfidence = g.Average(x => x.Confidence) });
```

### 4. Implementation Rules
1. **No `dynamic`**: Everything must be strictly typed.
2. **Zero Allocations in Loops**: Avoid instantiating new objects (e.g., `new List<int>()`) inside tight inner loops. Reuse buffers.
3. **Explicit Math**: Use `Math.Max`, `Math.Min`, `Math.Abs`, and `Math.Round(..., MidpointRounding.AwayFromZero)` strictly. Python's `round()` behaves differently (Banker's rounding) than standard expectations; verify the exact rounding semantics required.

### 5. Validation
- Always write a short, deterministic unit test in `DoclingDotNet.Tests` demonstrating that the ported C# algorithm produces the exact same numerical or structural output as the Python original for a given dummy input.