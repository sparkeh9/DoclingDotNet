param(
    [switch]$IncludeNativeBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "[clean] repo root: $repoRoot"

$targets = @(
    (Join-Path $repoRoot "dotnet\examples\Spike.OnnxRuntime\bin"),
    (Join-Path $repoRoot "dotnet\examples\Spike.OnnxRuntime\obj"),
    (Join-Path $repoRoot "dotnet\examples\Spike.Pdfium\bin"),
    (Join-Path $repoRoot "dotnet\examples\Spike.Pdfium\obj"),
    (Join-Path $repoRoot "dotnet\examples\Spike.Tesseract\bin"),
    (Join-Path $repoRoot "dotnet\examples\Spike.Tesseract\obj"),
    (Join-Path $repoRoot "dotnet\examples\Spike.DoclingParseCAbi\bin"),
    (Join-Path $repoRoot "dotnet\examples\Spike.DoclingParseCAbi\obj")
)

if ($IncludeNativeBuild) {
    $targets += (Join-Path $repoRoot "upstream\deps\docling-parse\build-cabi")
}

foreach ($target in $targets) {
    if (Test-Path $target) {
        Write-Host "[clean] removing $target"
        Remove-Item -Recurse -Force $target
    }
}

Write-Host "[clean] completed"
