param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipConfigure
)

# We use Continue here because native commands (like cmake) often write warnings 
# to stderr, which PowerShell incorrectly treats as fatal errors when set to Stop.
$ErrorActionPreference = "Continue"

$repoRoot = Split-Path -Parent $PSScriptRoot
$doclingParseDir = Join-Path $repoRoot "upstream/deps/docling-parse"
$buildDir = Join-Path $doclingParseDir "build-cabi"
$spikeProject = Join-Path $repoRoot "dotnet\examples\Spike.DoclingParseCAbi\Spike.DoclingParseCAbi.csproj"

Write-Host "[smoke] repo root: $repoRoot"
Write-Host "[smoke] docling-parse dir: $doclingParseDir"
Write-Host "[smoke] build dir: $buildDir"
Write-Host "[smoke] spike project: $spikeProject"

if (-not $SkipConfigure) {
    Write-Host "[smoke] cmake configure"
    & cmake -S $doclingParseDir -B $buildDir -DDOCLING_PARSE_BUILD_C_API=ON -DDOCLING_PARSE_BUILD_PYTHON_BINDINGS=OFF
    if ($LASTEXITCODE -ne 0) { throw "cmake configure failed" }
}

Write-Host "[smoke] cmake build docling_parse_c ($Configuration)"
& cmake --build $buildDir --config $Configuration --target docling_parse_c
if ($LASTEXITCODE -ne 0) { throw "cmake build failed" }

$nativeBinDir = Join-Path $buildDir $Configuration
$externalsBinDir = Join-Path $doclingParseDir "externals\bin"
$runtimeDirs = @($nativeBinDir, $externalsBinDir)

$pathSeparator = [System.IO.Path]::PathSeparator
$previousPath = $env:PATH
$env:PATH = ($runtimeDirs + $previousPath) -join $pathSeparator

# On Linux/macOS, update LD_LIBRARY_PATH / DYLD_LIBRARY_PATH for the dynamic linker
$previousLdPath = $env:LD_LIBRARY_PATH
$env:LD_LIBRARY_PATH = ($runtimeDirs + $previousLdPath) -join $pathSeparator

$previousDyldPath = $env:DYLD_LIBRARY_PATH
$env:DYLD_LIBRARY_PATH = ($runtimeDirs + $previousDyldPath) -join $pathSeparator

try {
    Write-Host "[smoke] dotnet run Spike.DoclingParseCAbi"
    & dotnet run --project $spikeProject --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw "dotnet run failed" }
}
finally {
    $env:PATH = $previousPath
    $env:LD_LIBRARY_PATH = $previousLdPath
    $env:DYLD_LIBRARY_PATH = $previousDyldPath
}

Write-Host "[smoke] completed"
