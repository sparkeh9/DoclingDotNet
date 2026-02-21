param(
    [string]$UpstreamRepoPath = "upstream/docling",
    [string]$UpstreamRemote = "https://github.com/docling-project/docling.git",
    [string]$BaselinePath = "patches/docling/upstream-baseline.json",
    [string]$TargetRef = "origin/main",
    [string]$OutputDir = ".artifacts/upstream-delta/docling",
    [int]$MaxCommitsInMarkdown = 200,
    [switch]$CloneIfMissing,
    [switch]$InitializeBaselineIfMissing,
    [switch]$SkipFetch
)

$ErrorActionPreference = "Stop"
if (Get-Variable PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

function Invoke-NativeStep {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [int[]]$AllowedExitCodes = @(0)
    )

    Write-Host "[delta] $Name"
    & $FilePath @Arguments
    $exitCode = $LASTEXITCODE
    if ($AllowedExitCodes -notcontains $exitCode) {
        throw "Step failed ($Name): $FilePath $($Arguments -join ' ') [exit=$exitCode]"
    }
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

function Assert-CommitExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoPath,
        [Parameter(Mandatory = $true)]
        [string]$Commit
    )

    & git -C $RepoPath cat-file -e "$Commit^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Commit not found in repo: $Commit"
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$upstreamRepo = Join-Path $repoRoot $UpstreamRepoPath
$baselineFile = Join-Path $repoRoot $BaselinePath
$outputRoot = Join-Path $repoRoot $OutputDir
$baselineScript = Join-Path $repoRoot "scripts\update-docling-upstream-baseline.ps1"
$powerShellHost = (Get-Process -Id $PID).Path

if (-not (Test-Path $upstreamRepo)) {
    if (-not $CloneIfMissing) {
        throw "Upstream repo not found: $upstreamRepo. Re-run with -CloneIfMissing to clone it."
    }

    $parentDir = Split-Path -Parent $upstreamRepo
    New-Item -ItemType Directory -Force -Path $parentDir | Out-Null
    Invoke-NativeStep -Name "Clone upstream docling repository" -FilePath "git" -Arguments @("clone", $UpstreamRemote, $upstreamRepo)
}

if (-not $SkipFetch) {
    Invoke-NativeStep -Name "Fetch upstream origin" -FilePath "git" -Arguments @("-C", $upstreamRepo, "fetch", "origin")
}
else {
    Write-Host "[delta] SkipFetch enabled; using currently available refs."
}

$targetCommit = Get-GitValue -RepoPath $upstreamRepo -Arguments @("rev-parse", $TargetRef)

if (-not (Test-Path $baselineFile)) {
    if (-not $InitializeBaselineIfMissing) {
        throw "Baseline file not found: $baselineFile. Run update-docling-upstream-baseline.ps1 or re-run with -InitializeBaselineIfMissing."
    }

    Invoke-NativeStep `
        -Name "Initialize missing baseline to target commit" `
        -FilePath $powerShellHost `
        -Arguments @(
            "-ExecutionPolicy", "Bypass",
            "-File", $baselineScript,
            "-UpstreamRepoPath", $UpstreamRepoPath,
            "-OutputPath", $BaselinePath,
            "-PortedRef", $targetCommit,
            "-TrackedRef", $TargetRef,
            "-SkipFetch"
        )
}

$baseline = Get-Content $baselineFile -Raw | ConvertFrom-Json
$baselineCommit = $baseline.baseline_ported_commit
if ([string]::IsNullOrWhiteSpace($baselineCommit)) {
    $baselineCommit = $baseline.upstream_head_commit
}

if ([string]::IsNullOrWhiteSpace($baselineCommit)) {
    throw "Baseline file does not contain baseline_ported_commit (or legacy upstream_head_commit): $baselineFile"
}

Assert-CommitExists -RepoPath $upstreamRepo -Commit $baselineCommit
Assert-CommitExists -RepoPath $upstreamRepo -Commit $targetCommit

$divergenceCounts = Get-GitValue -RepoPath $upstreamRepo -Arguments @("rev-list", "--left-right", "--count", "$baselineCommit...$targetCommit")
$divergenceParts = $divergenceCounts -split "\s+"
$behindCount = [int]$divergenceParts[0]
$aheadCount = [int]$divergenceParts[1]

$commitLines = @(& git -C $upstreamRepo log --reverse --date=iso-strict --pretty=format:"%H|%ad|%an|%s" "$baselineCommit..$targetCommit")
if ($LASTEXITCODE -ne 0) {
    throw "Failed to collect commit log delta."
}

$commitObjects = @()
foreach ($line in $commitLines) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    $parts = $line -split "\|", 4
    if ($parts.Count -lt 4) {
        continue
    }

    $commitObjects += [ordered]@{
        commit = $parts[0]
        date_utc = $parts[1]
        author = $parts[2]
        subject = $parts[3]
    }
}

$fileLines = @(& git -C $upstreamRepo diff --name-status "$baselineCommit" "$targetCommit")
if ($LASTEXITCODE -ne 0) {
    throw "Failed to collect changed file list."
}

$fileObjects = @()
foreach ($line in $fileLines) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    $parts = $line -split "\s+", 2
    if ($parts.Count -lt 2) {
        continue
    }

    $fileObjects += [ordered]@{
        status = $parts[0]
        path = $parts[1]
    }
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$fromShort = $baselineCommit.Substring(0, [Math]::Min(8, $baselineCommit.Length))
$toShort = $targetCommit.Substring(0, [Math]::Min(8, $targetCommit.Length))

$patchFileName = "docling-upstream-delta-$fromShort-to-$toShort.patch"
$summaryJsonName = "docling-upstream-delta-$fromShort-to-$toShort.json"
$summaryMarkdownName = "docling-upstream-delta-$fromShort-to-$toShort.md"
$llmPromptName = "docling-upstream-delta-$fromShort-to-$toShort-llm-prompt.md"

$patchPath = Join-Path $outputRoot $patchFileName
$summaryJsonPath = Join-Path $outputRoot $summaryJsonName
$summaryMarkdownPath = Join-Path $outputRoot $summaryMarkdownName
$llmPromptPath = Join-Path $outputRoot $llmPromptName

$patchLines = @(& git -C $upstreamRepo diff --binary "$baselineCommit" "$targetCommit")
if ($LASTEXITCODE -ne 0) {
    throw "Failed to generate patch file."
}
$patchLines | Set-Content -Path $patchPath -Encoding UTF8

$summary = [ordered]@{
    generated_at_utc = (Get-Date).ToUniversalTime().ToString("o")
    upstream_repository = (Get-GitValue -RepoPath $upstreamRepo -Arguments @("remote", "get-url", "origin") -AllowFailure)
    upstream_repo_path = $upstreamRepo
    baseline_file = $BaselinePath
    baseline_ported_commit = $baselineCommit
    baseline_behavioral_equivalence_status = $baseline.behavioral_equivalence_status
    baseline_behavioral_equivalence_scope = $baseline.behavioral_equivalence_scope
    baseline_behavioral_evidence_artifacts = $baseline.behavioral_evidence_artifacts
    target_ref = $TargetRef
    target_commit = $targetCommit
    commits_ahead = $aheadCount
    commits_behind = $behindCount
    changed_file_count = $fileObjects.Count
    artifacts = [ordered]@{
        patch_file = $patchPath
        summary_markdown = $summaryMarkdownPath
        llm_prompt_markdown = $llmPromptPath
    }
    commits = $commitObjects
    changed_files = $fileObjects
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryJsonPath -Encoding UTF8

$markdown = @()
$markdown += "# Docling Upstream Delta Report"
$markdown += ""
$markdown += "Generated: $($summary.generated_at_utc)"
$markdown += ""
$markdown += "## Range"
$markdown += "- Baseline (last ported): $baselineCommit"
$markdown += "- Baseline equivalence status: $($baseline.behavioral_equivalence_status)"
if (-not [string]::IsNullOrWhiteSpace($baseline.behavioral_equivalence_scope)) {
    $markdown += "- Baseline equivalence scope: $($baseline.behavioral_equivalence_scope)"
}
$markdown += "- Target ref: $TargetRef"
$markdown += "- Target commit: $targetCommit"
$markdown += "- Commits ahead: $aheadCount"
$markdown += "- Commits behind: $behindCount"
$markdown += "- Changed files: $($fileObjects.Count)"
$markdown += ""
$markdown += "## Artifacts"
$markdown += "- Patch: $patchPath"
$markdown += "- Summary JSON: $summaryJsonPath"
$markdown += "- LLM prompt: $llmPromptPath"
$markdown += ""
$markdown += "## Commit list"

if ($commitObjects.Count -eq 0) {
    $markdown += "- No upstream commits in range."
}
else {
    $commitLimit = [Math]::Min($MaxCommitsInMarkdown, $commitObjects.Count)
    for ($i = 0; $i -lt $commitLimit; $i++) {
        $commit = $commitObjects[$i]
        $short = $commit.commit.Substring(0, [Math]::Min(12, $commit.commit.Length))
        $markdown += "- $short $($commit.subject) ($($commit.author), $($commit.date_utc))"
    }

    if ($commitObjects.Count -gt $commitLimit) {
        $markdown += "- ... truncated in markdown ($commitLimit shown, $($commitObjects.Count) total)."
    }
}

$markdown += ""
$markdown += "## Changed files"
if ($fileObjects.Count -eq 0) {
    $markdown += "- No file changes in range."
}
else {
    foreach ($file in $fileObjects) {
        $markdown += "- $($file.status) $($file.path)"
    }
}

$markdown | Set-Content -Path $summaryMarkdownPath -Encoding UTF8

$llmPrompt = @()
$llmPrompt += "# LLM Porting Input: Docling Upstream Delta"
$llmPrompt += ""
$llmPrompt += "You are porting upstream Docling Python changes into the .NET implementation in this repository."
$llmPrompt += ""
$llmPrompt += "## Upstream range"
$llmPrompt += "- Baseline (already ported): $baselineCommit"
$llmPrompt += "- Target: $targetCommit ($TargetRef)"
$llmPrompt += "- Commits to port: $aheadCount"
$llmPrompt += ""
$llmPrompt += "## Input artifacts"
$llmPrompt += "- Patch file: $patchPath"
$llmPrompt += "- JSON summary: $summaryJsonPath"
$llmPrompt += "- Markdown summary: $summaryMarkdownPath"
$llmPrompt += ""
$llmPrompt += "## Required output"
$llmPrompt += "1. Analyze upstream changes and map them to impacted .NET areas (dotnet/src/DoclingDotNet/**, tests, scripts, docs)."
$llmPrompt += "2. Implement behavior changes so .NET contracts/parity remain aligned with upstream semantics."
$llmPrompt += "3. Add or update tests for each behavior change."
$llmPrompt += "4. Run validation commands and provide pass/fail evidence."
$llmPrompt += "5. Update docs (docs/**, docs/05_Operations/Progress/progress.md, docs/CHANGELOG.md) with what changed and why."
$llmPrompt += "6. If a change cannot be ported exactly, document gap + mitigation explicitly."
$llmPrompt += ""
$llmPrompt += "## Validation commands"
$llmPrompt += "- dotnet test dotnet/DoclingDotNet.slnx --configuration Release"
$llmPrompt += "- dotnet build dotnet/DoclingDotNet.Examples.slnx --configuration Release"
$llmPrompt += "- powershell -ExecutionPolicy Bypass -File .\scripts\test-docling-parse-cabi-smoke.ps1 -SkipConfigure"
$llmPrompt += "- powershell -ExecutionPolicy Bypass -File .\scripts\run-docling-parity-harness.ps1 -SkipConfigure -Output .artifacts/parity/docling-parse-parity-report.json -MaxOcrDrift 0 --max-pages 20"
$llmPrompt += ""
$llmPrompt += "After successful porting, update baseline so this target becomes the new last-ported commit:"
$llmPrompt += "- powershell -ExecutionPolicy Bypass -File .\scripts\update-docling-upstream-baseline.ps1 -PortedRef $targetCommit -TrackedRef $TargetRef -SkipFetch"

$llmPrompt | Set-Content -Path $llmPromptPath -Encoding UTF8

Write-Host "[delta] report generated"
Write-Host "[delta] baseline: $baselineCommit"
Write-Host "[delta] target: $targetCommit"
Write-Host "[delta] commits ahead: $aheadCount"
Write-Host "[delta] changed files: $($fileObjects.Count)"
Write-Host "[delta] patch: $patchPath"
Write-Host "[delta] summary: $summaryJsonPath"
Write-Host "[delta] markdown: $summaryMarkdownPath"
Write-Host "[delta] llm prompt: $llmPromptPath"
