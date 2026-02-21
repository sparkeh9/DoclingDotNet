param(
    [string]$PatchPath = "patches/docling-parse/0001-docling-parse-cabi-foundation-and-segmented-runtime.patch",
    [switch]$CheckOnly
)

$ErrorActionPreference = "Stop"
if (Get-Variable PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$upstreamRepo = Join-Path $repoRoot "upstream\deps\docling-parse"
$resolvedPatch = Join-Path $repoRoot $PatchPath

if (-not (Test-Path $upstreamRepo)) {
    throw "Upstream repo not found: $upstreamRepo"
}

if (-not (Test-Path $resolvedPatch)) {
    throw "Patch file not found: $resolvedPatch"
}

Write-Host "[apply] upstream repo: $upstreamRepo"
Write-Host "[apply] patch file: $resolvedPatch"

# Check if already applied (reverse check)
$alreadyApplied = $false
try {
    & git -C $upstreamRepo apply --check --reverse --ignore-space-change --ignore-whitespace $resolvedPatch *>$null
    if ($LASTEXITCODE -eq 0) {
        $alreadyApplied = $true
    }
} catch { }

if ($alreadyApplied) {
    Write-Host "[apply] patch is already present."
    exit 0
}

if ($CheckOnly) {
    Write-Host "[apply] checking if patch applies cleanly..."
    & git -C $upstreamRepo apply --check $resolvedPatch
    if ($LASTEXITCODE -ne 0) {
        throw "Patch check failed."
    }

    Write-Host "[apply] patch check passed."
    exit 0
}

Write-Host "[apply] applying patch..."
& git -C $upstreamRepo apply --whitespace=nowarn $resolvedPatch
if ($LASTEXITCODE -ne 0) {
    throw "Patch apply failed."
}

Write-Host "[apply] patch applied successfully."
