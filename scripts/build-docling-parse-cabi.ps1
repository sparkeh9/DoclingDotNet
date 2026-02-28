param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$InstallDependencies
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$doclingParseDir = Join-Path $repoRoot "upstream/deps/docling-parse"

# Normalize path separators for Linux/macOS compatibility when calling native tools
$buildDirName = "build-cabi"
$buildDirPath = "$doclingParseDir/$buildDirName"

Write-Host "[build-native] OS: $([System.Environment]::OSVersion.Platform)"
Write-Host "[build-native] repo root: $repoRoot"

# If requested and on Linux, attempt to install build dependencies
if ($InstallDependencies -and $IsLinux) {
    Write-Host "[build-native] Installing Linux build dependencies..."
    & sudo apt-get update
    & sudo apt-get install -y build-essential cmake
}

$cmakeArgs = @(
    "-S", $doclingParseDir,
    "-B", $buildDirPath,
    "-DCMAKE_BUILD_TYPE=$Configuration",
    "-DDOCLING_PARSE_BUILD_C_API=ON",
    "-DDOCLING_PARSE_BUILD_PYTHON_BINDINGS=OFF"
)

if ($IsWindows) {
    Write-Host "[build-native] Suppressing MSVC C4530 warning by injecting /EHsc..."
    $cmakeArgs += "-DCMAKE_CXX_FLAGS=/EHsc"
}

Write-Host "[build-native] configuring CMake..."
& cmake @cmakeArgs
if ($LASTEXITCODE -ne 0) { throw "CMake configure failed" }

Write-Host "[build-native] building docling_parse_c..."
& cmake --build $buildDirPath --config $Configuration --target docling_parse_c
if ($LASTEXITCODE -ne 0) { throw "CMake build failed" }

Write-Host "[build-native] Build complete."
