param(
    [string]$UpstreamRepoPath = "upstream/docling",
    [string]$OutputPath = "patches/docling/upstream-baseline.json",
    [string]$PortedRef = "HEAD",
    [string]$TrackedRef = "origin/main",
    [ValidateSet("not_verified", "partial", "verified")]
    [string]$EquivalenceStatus = "not_verified",
    [string]$EquivalenceScope = "",
    [string[]]$EvidenceArtifacts = @(),
    [switch]$SkipFetch
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

if (-not (Test-Path $upstreamRepo)) {
    throw "Upstream repo not found: $upstreamRepo"
}

if (-not $SkipFetch) {
    Write-Host "[baseline] fetching upstream origin"
    & git -C $upstreamRepo fetch origin
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to fetch origin for $upstreamRepo"
    }
}
else {
    Write-Host "[baseline] SkipFetch enabled; using currently available refs."
}

$outputDir = Split-Path -Parent $outputFile
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$portedCommit = Get-GitValue -RepoPath $upstreamRepo -Arguments @("rev-parse", $PortedRef)
$trackedRefCommit = Get-GitValue -RepoPath $upstreamRepo -Arguments @("rev-parse", $TrackedRef) -AllowFailure
$upstreamRemote = Get-GitValue -RepoPath $upstreamRepo -Arguments @("remote", "get-url", "origin") -AllowFailure
$upstreamBranch = Get-GitValue -RepoPath $upstreamRepo -Arguments @("branch", "--show-current") -AllowFailure
$rootRepoCommit = Get-GitValue -RepoPath $repoRoot -Arguments @("rev-parse", "HEAD") -AllowFailure

$metadata = [ordered]@{
    schema_version = 1
    updated_at_utc = (Get-Date).ToUniversalTime().ToString("o")
    upstream_repository = $upstreamRemote
    upstream_repository_local_path = $upstreamRepo
    upstream_branch = $upstreamBranch
    baseline_ported_ref = $PortedRef
    baseline_ported_commit = $portedCommit
    tracked_ref = $TrackedRef
    tracked_ref_commit = $trackedRefCommit
    behavioral_equivalence_status = $EquivalenceStatus
    behavioral_equivalence_scope = $EquivalenceScope
    behavioral_evidence_artifacts = $EvidenceArtifacts
    root_repo_commit = $rootRepoCommit
}

$metadata | ConvertTo-Json -Depth 4 | Set-Content -Path $outputFile -Encoding UTF8

Write-Host "[baseline] metadata written: $outputFile"
Write-Host "[baseline] baseline ported commit: $portedCommit"
if ($trackedRefCommit) {
    Write-Host "[baseline] tracked ref ($TrackedRef): $trackedRefCommit"
}
Write-Host "[baseline] behavioral equivalence status: $EquivalenceStatus"
