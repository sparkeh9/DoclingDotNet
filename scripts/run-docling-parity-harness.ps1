param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipConfigure,
    [string]$Output = ".artifacts/parity/docling-parse-parity-report.json",
    [int]$MaxOcrDrift = 0,
    [ValidateSet("Critical", "Major", "Minor")]
    [string]$TextMismatchSeverity = "Minor",
    [ValidateSet("Critical", "Major", "Minor")]
    [string]$GeometryMismatchSeverity = "Minor",
    [ValidateSet("Critical", "Major", "Minor")]
    [string]$ContextMismatchSeverity = "Minor",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$HarnessArgs
)

# We use Continue here because native commands often write warnings 
# to stderr, which PowerShell incorrectly treats as fatal errors when set to Stop.
$ErrorActionPreference = "Continue"

function Resolve-NativeRuntimeDirectories {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BuildDir,
        [Parameter(Mandatory = $true)]
        [string]$Configuration,
        [Parameter(Mandatory = $true)]
        [string]$DoclingParseDir
    )

    $nativeLibraryNames = @(
        "docling_parse_c.dll",
        "docling_parse_c.so",
        "docling_parse_c.dylib",
        "libdocling_parse_c.so",
        "libdocling_parse_c.dylib"
    )

    $nativeLibrary = Get-ChildItem -Path $BuildDir -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $nativeLibraryNames -contains $_.Name } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if (-not $nativeLibrary) {
        throw "[parity] Could not locate a built docling_parse_c native library under '$BuildDir'."
    }

    $runtimeDirs = New-Object System.Collections.Generic.List[string]
    $runtimeDirs.Add($nativeLibrary.Directory.FullName)

    foreach ($candidate in @(
        (Join-Path $BuildDir $Configuration),
        $BuildDir,
        (Join-Path $DoclingParseDir "externals/bin"),
        (Join-Path $DoclingParseDir "externals/lib")
    )) {
        if (-not (Test-Path $candidate)) {
            continue
        }

        $resolved = (Resolve-Path $candidate).Path
        if (-not $runtimeDirs.Contains($resolved)) {
            $runtimeDirs.Add($resolved)
        }
    }

    return $runtimeDirs.ToArray()
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$doclingParseDir = Join-Path $repoRoot "upstream/deps/docling-parse"
$buildDir = Join-Path $doclingParseDir "build-cabi"
$harnessProject = Join-Path $repoRoot "dotnet/tools/DoclingParityHarness/DoclingParityHarness.csproj"

Write-Host "[parity] repo root: $repoRoot"
Write-Host "[parity] docling-parse dir: $doclingParseDir"
Write-Host "[parity] build dir: $buildDir"
Write-Host "[parity] harness project: $harnessProject"

if (-not $SkipConfigure) {
    Write-Host "[parity] cmake configure"
    & cmake -S $doclingParseDir -B $buildDir -DDOCLING_PARSE_BUILD_C_API=ON -DDOCLING_PARSE_BUILD_PYTHON_BINDINGS=OFF
    if ($LASTEXITCODE -ne 0) { throw "cmake configure failed" }
}

Write-Host "[parity] cmake build docling_parse_c ($Configuration)"
& cmake --build $buildDir --config $Configuration --target docling_parse_c
if ($LASTEXITCODE -ne 0) { throw "cmake build failed" }

$runtimeDirs = Resolve-NativeRuntimeDirectories -BuildDir $buildDir -Configuration $Configuration -DoclingParseDir $doclingParseDir
$pathSeparator = [System.IO.Path]::PathSeparator

$previousPath = $env:PATH
$env:PATH = ($runtimeDirs + $previousPath) -join $pathSeparator

# On Linux/macOS, update LD_LIBRARY_PATH / DYLD_LIBRARY_PATH
$previousLdPath = $env:LD_LIBRARY_PATH
$env:LD_LIBRARY_PATH = ($runtimeDirs + $previousLdPath) -join $pathSeparator

$previousDyldPath = $env:DYLD_LIBRARY_PATH
$env:DYLD_LIBRARY_PATH = ($runtimeDirs + $previousDyldPath) -join $pathSeparator

Write-Host ("[parity] runtime dirs: " + ($runtimeDirs -join ", "))

try {
    Write-Host "[parity] running DoclingParityHarness"
    $dotnetArgs = @(
        "run",
        "--project", $harnessProject,
        "--configuration", $Configuration,
        "--",
        "--output", $Output,
        "--max-ocr-drift", $MaxOcrDrift,
        "--text-mismatch-severity", $TextMismatchSeverity,
        "--geometry-mismatch-severity", $GeometryMismatchSeverity,
        "--context-mismatch-severity", $ContextMismatchSeverity
    )

    if ($HarnessArgs) {
        $dotnetArgs += $HarnessArgs
    }

    & dotnet @dotnetArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet run DoclingParityHarness failed" }
}
finally {
    $env:PATH = $previousPath
    $env:LD_LIBRARY_PATH = $previousLdPath
    $env:DYLD_LIBRARY_PATH = $previousDyldPath
}

Write-Host "[parity] completed"
