using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

Console.WriteLine("Spike.OnnxRuntime: validating native runtime load...");
Console.WriteLine($"Runtime: {Environment.Version} | OS: {Environment.OSVersion} | Arch: {RuntimeInformation.ProcessArchitecture}");

try
{
    using var sessionOptions = new SessionOptions();
    var providers = OrtEnv.Instance().GetAvailableProviders();

    Console.WriteLine("ONNX Runtime native load: OK");
    Console.WriteLine("Available providers: " + string.Join(", ", providers));

    var modelPath = Path.Combine("models", "model.onnx");
    if (!File.Exists(modelPath))
    {
        Console.WriteLine($"Model not found at {modelPath}. Run download script first.");
        return;
    }

    Console.WriteLine($"\nLoading model {modelPath}...");
    using var session = new InferenceSession(modelPath, sessionOptions);

    Console.WriteLine("Inputs:");
    foreach (var input in session.InputMetadata)
    {
        Console.WriteLine($"- {input.Key}: {input.Value.ElementType} [{string.Join(", ", input.Value.Dimensions)}]");
    }

    Console.WriteLine("Outputs:");
    foreach (var output in session.OutputMetadata)
    {
        Console.WriteLine($"- {output.Key}: {output.Value.ElementType} [{string.Join(", ", output.Value.Dimensions)}]");
    }

    Console.WriteLine("\nRunning dummy inference benchmark...");
    
    // Create a dummy tensor based on typical RT-DETR input (pixel_values)
    var inputName = session.InputMetadata.Keys.First();
    var dimensions = session.InputMetadata[inputName].Dimensions;
    
    // If dimensions are symbolic (-1), replace with typical 1x3x640x640
    var shape = dimensions.Select(d => d <= 0 ? (d == -1 ? 1 : 640) : d).ToArray();
    if (shape.Length == 4 && shape[0] <= 0) shape[0] = 1;
    if (shape.Length == 4 && shape[1] <= 0) shape[1] = 3;
    if (shape.Length == 4 && shape[2] <= 0) shape[2] = 640;
    if (shape.Length == 4 && shape[3] <= 0) shape[3] = 640;
    
    Console.WriteLine($"Using dummy input shape: [{string.Join(", ", shape)}]");
    var tensor = new DenseTensor<float>(shape);

    var targetSizesTensor = new DenseTensor<long>(new[] { 1, 2 });
    targetSizesTensor[0, 0] = 640; // height
    targetSizesTensor[0, 1] = 640; // width

    // Warmup
    var inputs = new List<NamedOnnxValue> 
    { 
        NamedOnnxValue.CreateFromTensor("images", tensor),
        NamedOnnxValue.CreateFromTensor("orig_target_sizes", targetSizesTensor)
    };
    using var warmupResult = session.Run(inputs);

    // Benchmark
    int iterations = 10;
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < iterations; i++)
    {
        using var result = session.Run(inputs);
    }
    sw.Stop();

    Console.WriteLine($"\nInference benchmark completed.");
    Console.WriteLine($"Total time for {iterations} iterations: {sw.ElapsedMilliseconds} ms");
    Console.WriteLine($"Average time per iteration: {sw.ElapsedMilliseconds / (double)iterations:F2} ms");
    Console.WriteLine("Spike exit: OK");
}
catch (Exception ex)
{
    Console.WriteLine("ONNX Runtime load/execution FAILED");
    Console.WriteLine(ex.ToString());
    Environment.ExitCode = 1;
}
