param(
    [string]$OutputPatch = "patches/docling-parse/0001-docling-parse-cabi-foundation-and-segmented-runtime.patch"
)

$ErrorActionPreference = "Stop"
if (Get-Variable PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

function Invoke-GitCapture {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [int[]]$AllowedExitCodes = @(0)
    )

    $stdout = New-TemporaryFile
    $stderr = New-TemporaryFile

    try {
        $proc = Start-Process `
            -FilePath "git" `
            -ArgumentList $Arguments `
            -NoNewWindow `
            -Wait `
            -PassThru `
            -RedirectStandardOutput $stdout `
            -RedirectStandardError $stderr

        $stdOutLines = @()
        $stdErrLines = @()

        if (Test-Path $stdout) {
            $stdOutLines = Get-Content $stdout
        }

        if (Test-Path $stderr) {
            $stdErrLines = Get-Content $stderr
        }

        foreach ($line in $stdErrLines) {
            if ($line -notmatch "LF will be replaced by CRLF") {
                Write-Host "[git] $line"
            }
        }

        if ($AllowedExitCodes -notcontains $proc.ExitCode) {
            throw "git command failed with exit code $($proc.ExitCode): git $($Arguments -join ' ')"
        }

        return $stdOutLines
    }
    finally {
        Remove-Item $stdout, $stderr -ErrorAction SilentlyContinue
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$upstreamRepo = Join-Path $repoRoot "upstream\deps\docling-parse"
$patchPath = Join-Path $repoRoot $OutputPatch
$patchDir = Split-Path -Parent $patchPath

if (-not (Test-Path $upstreamRepo)) {
    throw "Upstream repo not found: $upstreamRepo"
}

New-Item -ItemType Directory -Force -Path $patchDir | Out-Null

if (Test-Path $patchPath) {
    Remove-Item $patchPath -Force
}

Write-Host "[export] collecting tracked upstream diff..."
$trackedDiff = Invoke-GitCapture -Arguments @("-C", $upstreamRepo, "diff", "--binary")
$trackedDiff | Set-Content -Path $patchPath -Encoding UTF8

Write-Host "[export] collecting untracked upstream files..."
$untracked = Invoke-GitCapture -Arguments @("-C", $upstreamRepo, "ls-files", "--others", "--exclude-standard") |
    Where-Object { $_ -notlike "build-cabi/*" }

foreach ($path in $untracked) {
    Add-Content -Path $patchPath -Value ""
    $fileDiff = Invoke-GitCapture `
        -Arguments @("-C", $upstreamRepo, "diff", "--binary", "--no-index", "--", "NUL", $path) `
        -AllowedExitCodes @(0, 1)
    $fileDiff | Add-Content -Path $patchPath
}

Write-Host "[export] patch written: $patchPath"
if ($untracked.Count -gt 0) {
    Write-Host "[export] included untracked files:"
    $untracked | ForEach-Object { Write-Host " - $_" }
}
