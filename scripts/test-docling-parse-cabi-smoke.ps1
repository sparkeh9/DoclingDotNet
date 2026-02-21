param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipConfigure
)

$ErrorActionPreference = "Stop"
if (Get-Variable PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$smokeScript = Join-Path $repoRoot "scripts\run-docling-parse-cabi-smoke.ps1"

if (-not (Test-Path $smokeScript)) {
    throw "Smoke script not found: $smokeScript"
}

$smokeArgs = @{
    Configuration = $Configuration
}

if ($SkipConfigure) {
    $smokeArgs.SkipConfigure = $true
}

Write-Host "[test] executing smoke script with assertions..."
$outputTextList = New-Object System.Collections.Generic.List[string]

# Execute smoke script and stream output to host while capturing for pattern matching
& $smokeScript @smokeArgs *>&1 | ForEach-Object {
    $line = $_.ToString()
    Write-Host $line
    $outputTextList.Add($line)
}

$exitCode = $LASTEXITCODE
$outputText = $outputTextList -join "`n"

if ($exitCode -ne 0) {
    throw "Smoke script exited with non-zero code: $exitCode"
}

$requiredPatterns = @(
    "C ABI version:",
    "Page count via C ABI:",
    "Segmented parity check: OK",
    "C ABI spike: OK"
)

foreach ($pattern in $requiredPatterns) {
    if (-not ($outputText -match [regex]::Escape($pattern))) {
        throw "Missing expected output pattern: '$pattern'"
    }
}

Write-Host "[test] PASS: C ABI smoke assertions succeeded."
