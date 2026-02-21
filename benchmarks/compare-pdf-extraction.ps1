<#
.SYNOPSIS
    Compare PDF extraction between Python docling-parse and .NET DoclingDotNet.

.DESCRIPTION
    Downloads a PDF, processes it through both pipelines in-memory, and writes
    per-pipeline text output and a comparison report to .artifacts/extraction/<basename>/.

.PARAMETER Url
    URL of the PDF to download.

.EXAMPLE
    .\benchmarks\compare-pdf-extraction.ps1 -Url "https://assets.publishing.service.gov.uk/media/67489b9d5ba46550018cebd8/Research_reports_guidance.pdf"
#>
param(
    [Parameter(Mandatory = $false)]
    [string]$Url,
    
    [Parameter(Mandatory = $false)]
    [string]$File
)

if (-not $Url -and -not $File) {
    $Url = "https://assets.publishing.service.gov.uk/media/67489b9d5ba46550018cebd8/Research_reports_guidance.pdf"
}

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

$utf8 = [System.Text.UTF8Encoding]::new($false)
if ($File) {
    if (-not (Test-Path $File)) { throw "File not found: $File" }
    $basename = [System.IO.Path]::GetFileNameWithoutExtension($File)
} else {
    $basename = [System.IO.Path]::GetFileNameWithoutExtension(([System.Uri]$Url).Segments[-1])
}
$outputDir = [System.IO.Path]::Combine($repoRoot, ".artifacts", "extraction", $basename)
$pdfPath = [System.IO.Path]::Combine($outputDir, "$basename.pdf")
$venvDir = Join-Path $repoRoot ".venv-docling"

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# ── Step 1: Download PDF ──────────────────────────────────────────────────────
if ($File) {
    Copy-Item $File $pdfPath -Force
    Write-Host "[info] Using local file: $File" -ForegroundColor Cyan
} else {
    if (Test-Path $pdfPath) {
        Write-Host "[download] PDF already exists: $pdfPath" -ForegroundColor DarkGray
    } else {
        Write-Host "[download] Fetching: $Url" -ForegroundColor Cyan
        Invoke-WebRequest -Uri $Url -OutFile $pdfPath -UseBasicParsing
        $sizeMb = [math]::Round((Get-Item $pdfPath).Length / 1MB, 2)
        Write-Host "[download] Saved ($sizeMb MB): $pdfPath" -ForegroundColor Green
    }
}

# ── Step 2: .NET Extraction via C ABI ─────────────────────────────────────────
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
Write-Host "  .NET Extraction (C ABI → in-memory)" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow

$harnessProject = [IO.Path]::Combine($repoRoot, "dotnet", "tools", "DoclingParityHarness", "DoclingParityHarness.csproj")

Write-Host "[dotnet] Building harness..." -ForegroundColor DarkGray
dotnet build $harnessProject --configuration Release --nologo -v q 2>&1 | Out-Null

# Resolve native library directories (docling_parse_c + dependencies like qpdf, jpeg)
$doclingParseDir = [IO.Path]::Combine($repoRoot, "upstream", "deps", "docling-parse")
$nativeBuildDir = Join-Path $doclingParseDir "build-cabi"
$nativeDll = Get-ChildItem -Path $nativeBuildDir -Recurse -File -Filter "docling_parse_c.dll" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if (-not $nativeDll) { throw "[dotnet] Cannot find docling_parse_c.dll under $nativeBuildDir" }

$runtimeDirs = @(
    $nativeDll.Directory.FullName,
    (Join-Path $nativeBuildDir "Release"),
    $nativeBuildDir,
    (Join-Path $doclingParseDir "externals/bin"),
    (Join-Path $doclingParseDir "externals/lib")
) | Where-Object { Test-Path $_ } | ForEach-Object { (Resolve-Path $_).Path } | Select-Object -Unique

$previousPath = $env:PATH
$env:PATH = ($runtimeDirs -join ";") + ";$env:PATH"

Write-Host "[dotnet] Extracting..." -ForegroundColor Cyan
$dotnetStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$ErrorActionPreference = "Continue"
$dotnetStdout = "$outputDir\dotnet-stdout.log"
$dotnetStderr = "$outputDir\dotnet-stderr.log"
$p = Start-Process -FilePath "dotnet" -ArgumentList "run --project `"$harnessProject`" --configuration Release --no-build -- --extract-pdf `"$pdfPath`" --extract-output-dir `"$outputDir`"" -NoNewWindow -Wait -PassThru -RedirectStandardOutput $dotnetStdout -RedirectStandardError $dotnetStderr
if ($p.ExitCode -ne 0) { throw "dotnet extract failed (exit $($p.ExitCode)). Check $dotnetStderr" }
$ErrorActionPreference = "Stop"
$env:PATH = $previousPath
$dotnetStopwatch.Stop()

# Read stats from file
$dnStats = [IO.File]::ReadAllText((Join-Path $outputDir "$basename.dotnet.stats.json"), $utf8) | ConvertFrom-Json
Write-Host "[dotnet] $($dnStats.pages) pages in $([math]::Round($dotnetStopwatch.Elapsed.TotalSeconds, 2))s" -ForegroundColor Green

# ── Step 3: Python Extraction via docling-parse ───────────────────────────────
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
Write-Host "  Python Extraction (typed API → in-memory)" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow

# Create venv if it doesn't exist
if (-not (Test-Path ([IO.Path]::Combine($venvDir, "Scripts", "python.exe")))) {
    Write-Host "[python] Creating virtual environment: $venvDir" -ForegroundColor DarkGray
    python -m venv $venvDir
}

$venvPython = [IO.Path]::Combine($venvDir, "Scripts", "python.exe")
$venvPip = [IO.Path]::Combine($venvDir, "Scripts", "pip.exe")

# Install docling-parse if needed
$ErrorActionPreference = "Continue"
$installed = & $venvPip show docling-parse 2>&1 | Select-String "^Name:" -Quiet
if (-not $installed) {
    Write-Host "[python] Installing docling-parse into venv..." -ForegroundColor DarkGray
    & $venvPip install docling-parse 2>&1 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
    Write-Host "[python] Installed." -ForegroundColor Green
}
$ErrorActionPreference = "Stop"

Write-Host "[python] Extracting..." -ForegroundColor Cyan
$pythonStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$pythonStdout = "$outputDir\python-stdout.log"
$pythonStderr = "$outputDir\python-stderr.log"
$scriptPath = [IO.Path]::Combine($repoRoot, "benchmarks", "extract-pdf-python.py")
$p = Start-Process -FilePath $venvPython -ArgumentList "`"$scriptPath`" `"$pdfPath`" `"$outputDir`"" -NoNewWindow -Wait -PassThru -RedirectStandardOutput $pythonStdout -RedirectStandardError $pythonStderr
if ($p.ExitCode -ne 0) { throw "python extract failed (exit $($p.ExitCode)). Check $pythonStderr" }
$pythonStopwatch.Stop()

# Read stats from file
$pyStats = [IO.File]::ReadAllText((Join-Path $outputDir "$basename.python.stats.json"), $utf8) | ConvertFrom-Json
Write-Host "[python] $($pyStats.pages) pages in $([math]::Round($pythonStopwatch.Elapsed.TotalSeconds, 2))s" -ForegroundColor Green

# ── Step 4: Build Markdown Report from stats ──────────────────────────────────
$dotnetTime = [math]::Round($dotnetStopwatch.Elapsed.TotalSeconds, 3)
$pythonTime = [math]::Round($pythonStopwatch.Elapsed.TotalSeconds, 3)
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

$md = [System.Text.StringBuilder]::new()
[void]$md.AppendLine("# PDF Extraction Comparison: ``$basename``")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Property | Value |")
[void]$md.AppendLine("|---|---|")
[void]$md.AppendLine("| **Document** | ``$basename.pdf`` |")
if ($Url) { [void]$md.AppendLine("| **URL** | $Url |") }
[void]$md.AppendLine("| **File size** | $([math]::Round((Get-Item $pdfPath).Length / 1MB, 2)) MB |")
[void]$md.AppendLine("| **Timestamp** | $timestamp |")
[void]$md.AppendLine("")
[void]$md.AppendLine("## Timing")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Pipeline | Pages | Time (s) | Per-page (ms) |")
[void]$md.AppendLine("|---|---|---|---|")
$dotnetPerPage = if ($dnStats.pages -gt 0) { [math]::Round(($dotnetTime / $dnStats.pages) * 1000, 1) } else { "N/A" }
$pythonPerPage = if ($pyStats.pages -gt 0) { [math]::Round(($pythonTime / $pyStats.pages) * 1000, 1) } else { "N/A" }
[void]$md.AppendLine("| **.NET** (C ABI) | $($dnStats.pages) | $dotnetTime | $dotnetPerPage |")
[void]$md.AppendLine("| **Python** (typed API) | $($pyStats.pages) | $pythonTime | $pythonPerPage |")
[void]$md.AppendLine("")

# Per-page field counts from stats
[void]$md.AppendLine("## Per-Page Field Counts")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Page | .NET chars | Py chars | .NET words | Py words | .NET lines | Py lines | Chars match | Words match |")
[void]$md.AppendLine("|---|---|---|---|---|---|---|---|---|")

$totalDnChars = 0; $totalPyChars = 0
$totalDnWords = 0; $totalPyWords = 0
$totalDnLines = 0; $totalPyLines = 0
$charMismatches = 0; $wordMismatches = 0
$totalPages = [Math]::Max($dnStats.pages, $pyStats.pages)

for ($i = 0; $i -lt $totalPages; $i++) {
    $dnp = if ($i -lt $dnStats.perPage.Count) { $dnStats.perPage[$i] } else { $null }
    $pyp = if ($i -lt $pyStats.perPage.Count) { $pyStats.perPage[$i] } else { $null }

    $dnc = if ($dnp) { $dnp.chars } else { 0 }
    $dnw = if ($dnp) { $dnp.words } else { 0 }
    $dnl = if ($dnp) { $dnp.lines } else { 0 }
    $pyc = if ($pyp) { $pyp.chars } else { 0 }
    $pyw = if ($pyp) { $pyp.words } else { 0 }
    $pyl = if ($pyp) { $pyp.lines } else { 0 }

    $totalDnChars += $dnc; $totalPyChars += $pyc
    $totalDnWords += $dnw; $totalPyWords += $pyw
    $totalDnLines += $dnl; $totalPyLines += $pyl

    $charsOk = if ($dnc -eq $pyc) { "✅" } else { $charMismatches++; "❌" }
    $wordsOk = if ($dnw -eq $pyw) { "✅" } else { $wordMismatches++; "❌" }

    [void]$md.AppendLine("| $($i + 1) | $dnc | $pyc | $dnw | $pyw | $dnl | $pyl | $charsOk | $wordsOk |")
}

[void]$md.AppendLine("| **Total** | **$totalDnChars** | **$totalPyChars** | **$totalDnWords** | **$totalPyWords** | **$totalDnLines** | **$totalPyLines** | | |")
[void]$md.AppendLine("")

# Color distribution from stats
[void]$md.AppendLine("## Color Distribution (all pages)")
[void]$md.AppendLine("")
[void]$md.AppendLine("### .NET (C ABI)")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Color | Count |")
[void]$md.AppendLine("|---|---|")
$dnStats.colors.PSObject.Properties | Sort-Object { [int]$_.Value } -Descending | Select-Object -First 10 | ForEach-Object {
    [void]$md.AppendLine("| ``$($_.Name)`` | $($_.Value) |")
}
[void]$md.AppendLine("")
[void]$md.AppendLine("### Python (typed API)")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Color | Count |")
[void]$md.AppendLine("|---|---|")
$pyStats.colors.PSObject.Properties | Sort-Object { [int]$_.Value } -Descending | Select-Object -First 10 | ForEach-Object {
    [void]$md.AppendLine("| ``$($_.Name)`` | $($_.Value) |")
}
[void]$md.AppendLine("")

# Parity summary
[void]$md.AppendLine("## Parity Summary")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Metric | Result |")
[void]$md.AppendLine("|---|---|")
[void]$md.AppendLine("| Page count match | $(if ($dnStats.pages -eq $pyStats.pages) { '✅ Yes' } else { '❌ No' }) |")
[void]$md.AppendLine("| Char count mismatches | $(if ($charMismatches -eq 0) { '✅ 0' } else { "❌ $charMismatches / $totalPages" }) |")
[void]$md.AppendLine("| Word count mismatches | $(if ($wordMismatches -eq 0) { '✅ 0' } else { "❌ $wordMismatches / $totalPages" }) |")
[void]$md.AppendLine("| Unique .NET colors | $($dnStats.colors.PSObject.Properties.Count) |")
[void]$md.AppendLine("| Unique Python colors | $($pyStats.colors.PSObject.Properties.Count) |")
[void]$md.AppendLine("| Speedup (.NET vs Python) | $([math]::Round($pythonTime / [Math]::Max($dotnetTime, 0.001), 1))x |")
[void]$md.AppendLine("")

# Text file references
[void]$md.AppendLine("## Extracted Text")
[void]$md.AppendLine("")
[void]$md.AppendLine("| File | Pipeline |")
[void]$md.AppendLine("|---|---|")
[void]$md.AppendLine("| ``$basename.dotnet.md`` | .NET (C ABI) |")
[void]$md.AppendLine("| ``$basename.python.md`` | Python (typed API) |")
[void]$md.AppendLine("")

# Write the report
$reportPath = Join-Path $outputDir "comparison-report.md"
[IO.File]::WriteAllText($reportPath, $md.ToString(), $utf8)
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Magenta
Write-Host "  Report written: $reportPath" -ForegroundColor Magenta
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Magenta
Write-Host ""
Write-Host "  $totalPages pages | .NET: ${dotnetTime}s | Python: ${pythonTime}s | Speedup: $([math]::Round($pythonTime / [Math]::Max($dotnetTime, 0.001), 1))x"
Write-Host "  Char mismatches: $charMismatches | Word mismatches: $wordMismatches" -ForegroundColor $(if ($charMismatches + $wordMismatches -eq 0) { "Green" } else { "Yellow" })
