param(
    [switch]$SkipPatch
)

$ErrorActionPreference = "Stop"
if (Get-Variable PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$applyScript = Join-Path $repoRoot "scripts\apply-docling-parse-upstream-delta.ps1"

function Initialize-Repo {
    param(
        [string]$BaselinePath,
        [string]$LocalPath,
        [string]$RepoUrl = $null
    )

    $baselineFile = Join-Path $repoRoot $BaselinePath
    if (-not (Test-Path $baselineFile)) {
        Write-Warning "Baseline file not found: $baselineFile"
        return
    }

    $metadata = Get-Content $baselineFile | ConvertFrom-Json
    $targetCommit = $metadata.upstream_head_commit
    $url = if ($RepoUrl) { $RepoUrl } else { $metadata.upstream_repository }

    # Convert SSH URL to HTTPS for CI/Ease of use
    if ($url -match "git@github\.com:(.+)\.git") {
        $url = "https://github.com/$($Matches[1]).git"
    }

    $absoluteLocalPath = Join-Path $repoRoot $LocalPath
    if (-not (Test-Path $absoluteLocalPath)) {
        Write-Host "[init] cloning $url into $LocalPath..."
        New-Item -ItemType Directory -Force -Path (Split-Path $absoluteLocalPath -Parent) | Out-Null
        & git clone $url $absoluteLocalPath
        if ($LASTEXITCODE -ne 0) { throw "Failed to clone $url" }
    }

    Write-Host "[init] resetting $LocalPath to $targetCommit..."
    & git -C $absoluteLocalPath reset --hard $targetCommit
    if ($LASTEXITCODE -ne 0) { throw "Failed to reset $LocalPath" }

    & git -C $absoluteLocalPath clean -fd
}

# 1. Initialize docling-parse (Critical for C ABI)
Initialize-Repo `
    -BaselinePath "patches/docling-parse/upstream-baseline.json" `
    -LocalPath "upstream/deps/docling-parse"

# 2. Initialize docling (Reference/Parity)
Initialize-Repo `
    -BaselinePath "patches/docling/upstream-baseline.json" `
    -LocalPath "upstream/docling"

# 3. Initialize audio testing samples
Initialize-Repo `
    -BaselinePath "patches/audio-testing-samples/upstream-baseline.json" `
    -LocalPath "upstream/audio-testing-samples"

# 4. Apply patch to docling-parse
if (-not $SkipPatch) {
    Write-Host "[init] applying patches..."
    $powerShellHost = (Get-Process -Id $PID).Path
    & $powerShellHost -ExecutionPolicy Bypass -File $applyScript
    if ($LASTEXITCODE -ne 0) { throw "Failed to apply patches" }
}

Write-Host "[init] workspace initialization completed successfully."
