param(
    [string]$UpstreamRepoPath = "upstream/deps/docling-parse",
    [string]$OutputPath = "patches/docling-parse/upstream-baseline.json",
    [string]$PatchPath = "patches/docling-parse/0001-docling-parse-cabi-foundation-and-segmented-runtime.patch",
    [string]$TrackedRef = "origin/main"
)

$ErrorActionPreference = "Stop"
if (Get-Variable PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

function Get-GitValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoPath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [switch]$AllowFailure
    )

    $result = & git -C $RepoPath @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        if ($AllowFailure) {
            return $null
        }

        throw "git command failed: git -C $RepoPath $($Arguments -join ' ')"
    }

    if ($null -eq $result) {
        return $null
    }

    return ($result | Select-Object -First 1).Trim()
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$upstreamRepo = Join-Path $repoRoot $UpstreamRepoPath
$outputFile = Join-Path $repoRoot $OutputPath
$patchFile = Join-Path $repoRoot $PatchPath

if (-not (Test-Path $upstreamRepo)) {
    throw "Upstream repo not found: $upstreamRepo"
}

$outputDir = Split-Path -Parent $outputFile
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$upstreamHead = Get-GitValue -RepoPath $upstreamRepo -Arguments @("rev-parse", "HEAD")
$trackedRefCommit = Get-GitValue -RepoPath $upstreamRepo -Arguments @("rev-parse", $TrackedRef) -AllowFailure
$upstreamRemote = Get-GitValue -RepoPath $upstreamRepo -Arguments @("remote", "get-url", "origin") -AllowFailure
$upstreamBranch = Get-GitValue -RepoPath $upstreamRepo -Arguments @("branch", "--show-current") -AllowFailure
$rootRepoCommit = Get-GitValue -RepoPath $repoRoot -Arguments @("rev-parse", "HEAD") -AllowFailure

$patchSha256 = $null
if (Test-Path $patchFile) {
    $patchSha256 = (Get-FileHash -Path $patchFile -Algorithm SHA256).Hash.ToLowerInvariant()
}

$metadata = [ordered]@{
    schema_version                 = 1
    updated_at_utc                 = (Get-Date).ToUniversalTime().ToString("o")
    upstream_repository            = $upstreamRemote
    upstream_repository_local_path = $UpstreamRepoPath
    upstream_branch                = $upstreamBranch
    upstream_head_commit           = $upstreamHead
    tracked_ref                    = $TrackedRef
    tracked_ref_commit             = $trackedRefCommit
    patch_file                     = $PatchPath
    patch_sha256                   = $patchSha256
    root_repo_commit               = $rootRepoCommit
}

$metadata | ConvertTo-Json -Depth 4 | Set-Content -Path $outputFile -Encoding UTF8

Write-Host "[baseline] metadata written: $outputFile"
Write-Host "[baseline] upstream head: $upstreamHead"
if ($trackedRefCommit) {
    Write-Host "[baseline] tracked ref ($TrackedRef): $trackedRefCommit"
}
if ($patchSha256) {
    Write-Host "[baseline] patch sha256: $patchSha256"
}
