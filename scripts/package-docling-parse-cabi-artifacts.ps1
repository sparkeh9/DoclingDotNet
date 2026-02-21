param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$OutputDir = ".artifacts/slice0-win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$doclingParseRoot = Join-Path $repoRoot "upstream\deps\docling-parse"
$buildConfigDir = Join-Path $doclingParseRoot ("build-cabi\" + $Configuration)
$externalsBinDir = Join-Path $doclingParseRoot "externals\bin"

$dllPath = Join-Path $buildConfigDir "docling_parse_c.dll"
$libPath = Join-Path $buildConfigDir "docling_parse_c.lib"
$headerPath = Join-Path $doclingParseRoot "src\c_api\docling_parse_c_api.h"
$cAbiDocPath = Join-Path $doclingParseRoot "docs\c_abi.md"

if (-not (Test-Path $dllPath)) {
    throw "Missing native library: $dllPath. Build and smoke-test first."
}

if (-not (Test-Path $headerPath)) {
    throw "Missing header: $headerPath"
}

$artifactRoot = Join-Path $repoRoot $OutputDir
if (Test-Path $artifactRoot) {
    Remove-Item -Recurse -Force $artifactRoot
}

$nativeOut = Join-Path $artifactRoot "runtimes\win-x64\native"
$includeOut = Join-Path $artifactRoot "include"
$docsOut = Join-Path $artifactRoot "docs"

New-Item -ItemType Directory -Path $nativeOut -Force | Out-Null
New-Item -ItemType Directory -Path $includeOut -Force | Out-Null
New-Item -ItemType Directory -Path $docsOut -Force | Out-Null

Copy-Item $dllPath $nativeOut
if (Test-Path $libPath) {
    Copy-Item $libPath $nativeOut
}

if (Test-Path $externalsBinDir) {
    Get-ChildItem $externalsBinDir -Filter *.dll | ForEach-Object {
        Copy-Item $_.FullName $nativeOut
    }
}

Copy-Item $headerPath (Join-Path $includeOut "docling_parse_c_api.h")
if (Test-Path $cAbiDocPath) {
    Copy-Item $cAbiDocPath (Join-Path $docsOut "c_abi.md")
}

$headerContent = Get-Content $headerPath -Raw
function Get-MacroValue {
    param([string]$MacroName)
    $pattern = "#define\s+$([regex]::Escape($MacroName))\s+(\d+)"
    $match = [regex]::Match($headerContent, $pattern)
    if (-not $match.Success) {
        throw "Could not find macro '$MacroName' in $headerPath"
    }
    return [int]$match.Groups[1].Value
}

$abiMajor = Get-MacroValue "DOCLING_PARSE_C_ABI_VERSION_MAJOR"
$abiMinor = Get-MacroValue "DOCLING_PARSE_C_ABI_VERSION_MINOR"
$abiPatch = Get-MacroValue "DOCLING_PARSE_C_ABI_VERSION_PATCH"

$runtimeFiles = Get-ChildItem -Path $nativeOut -File | Sort-Object Name | ForEach-Object {
    "runtimes/win-x64/native/$($_.Name)"
}

$manifest = [ordered]@{
    schema_version = 1
    artifact_name = "docling-parse-cabi-win-x64"
    generated_utc = [DateTime]::UtcNow.ToString("o")
    platform = "windows-x64"
    configuration = $Configuration
    abi_version = [ordered]@{
        major = $abiMajor
        minor = $abiMinor
        patch = $abiPatch
        text = "$abiMajor.$abiMinor.$abiPatch"
    }
    files = [ordered]@{
        runtime = $runtimeFiles
        include = @("include/docling_parse_c_api.h")
        docs = @("docs/c_abi.md")
    }
    smoke_test_command = "powershell -ExecutionPolicy Bypass -File .\scripts\test-docling-parse-cabi-smoke.ps1 -SkipConfigure"
}

$manifestPath = Join-Path $artifactRoot "manifest.json"
$manifest | ConvertTo-Json -Depth 8 | Set-Content -Path $manifestPath -Encoding UTF8

Write-Host "[package] repo root: $repoRoot"
Write-Host "[package] artifact root: $artifactRoot"
Write-Host "[package] ABI version: $abiMajor.$abiMinor.$abiPatch"
Write-Host "[package] runtime files copied: $($runtimeFiles.Count)"
Write-Host "[package] manifest: $manifestPath"
