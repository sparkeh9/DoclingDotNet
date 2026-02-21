param(
    [string]$TargetRef = "origin/main",
    [string]$PatchPath = "patches/docling-parse/0001-docling-parse-cabi-foundation-and-segmented-runtime.patch",
    [string]$BaselinePath = "patches/docling-parse/upstream-baseline.json",
    [switch]$SkipFetch,
    [switch]$SkipValidation,
    [switch]$ContinueOnValidationFailure,
    [switch]$DryRun,
    [switch]$AllowEmptyPatch,
    [switch]$KeepBackup,
    [switch]$KeepTempOnFailure
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

    Write-Host "[upgrade] $Name"
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
        [string[]]$Arguments
    )

    $result = & git -C $RepoPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git command failed: git -C $RepoPath $($Arguments -join ' ')"
    }

    return ($result | Select-Object -First 1).Trim()
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$upstreamRepo = Join-Path $repoRoot "upstream\deps\docling-parse"
$resolvedPatch = Join-Path $repoRoot $PatchPath
$baselineFile = Join-Path $repoRoot $BaselinePath
$exportScript = Join-Path $repoRoot "scripts\export-docling-parse-upstream-delta.ps1"
$baselineScript = Join-Path $repoRoot "scripts\update-docling-parse-upstream-baseline.ps1"
$applyScript = Join-Path $repoRoot "scripts\apply-docling-parse-upstream-delta.ps1"
$smokeScript = Join-Path $repoRoot "scripts\test-docling-parse-cabi-smoke.ps1"
$parityScript = Join-Path $repoRoot "scripts\run-docling-parity-harness.ps1"
$powerShellHost = (Get-Process -Id $PID).Path

if (-not (Test-Path $upstreamRepo)) {
    throw "Upstream repo not found: $upstreamRepo"
}

$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$tempRoot = Join-Path $repoRoot ".tmp-upgrade\docling-parse-$timestamp"
$tempRepo = Join-Path $tempRoot "docling-parse"
$upgradeApplied = $false

try {
    # 1) Always export current local delta first so the latest port state is captured.
    Invoke-NativeStep `
        -Name "Export current upstream delta patch" `
        -FilePath $powerShellHost `
        -Arguments @(
            "-ExecutionPolicy", "Bypass",
            "-File", $exportScript,
            "-OutputPatch", $PatchPath
        )

    if (-not (Test-Path $resolvedPatch)) {
        throw "Patch file not found after export: $resolvedPatch"
    }
    $patchSizeBytes = (Get-Item $resolvedPatch).Length
    if ($patchSizeBytes -eq 0 -and -not $AllowEmptyPatch) {
        throw "Exported patch is empty ($resolvedPatch). Use -AllowEmptyPatch if this is intentional."
    }

    # 2) Fetch upstream if requested (default on).
    if (-not $SkipFetch) {
        Invoke-NativeStep `
            -Name "Fetch upstream remote" `
            -FilePath "git" `
            -Arguments @("-C", $upstreamRepo, "fetch", "origin")
    }
    else {
        Write-Host "[upgrade] SkipFetch enabled; using currently available refs."
    }

    $targetCommit = Get-GitValue -RepoPath $upstreamRepo -Arguments @("rev-parse", $TargetRef)
    $originalCommit = Get-GitValue -RepoPath $upstreamRepo -Arguments @("rev-parse", "HEAD")
    $remoteUrl = Get-GitValue -RepoPath $upstreamRepo -Arguments @("remote", "get-url", "origin")

    Write-Host "[upgrade] target ref: $TargetRef"
    Write-Host "[upgrade] target commit: $targetCommit"
    Write-Host "[upgrade] remote url: $remoteUrl"

    # 3) Build isolated candidate repo from local upstream clone at target commit.
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    Invoke-NativeStep `
        -Name "Clone local upstream repo into isolated temp workspace" `
        -FilePath "git" `
        -Arguments @("clone", "--quiet", "--no-checkout", $upstreamRepo, $tempRepo)

    Invoke-NativeStep `
        -Name "Set temp repo origin url to canonical remote" `
        -FilePath "git" `
        -Arguments @("-C", $tempRepo, "remote", "set-url", "origin", $remoteUrl)

    Invoke-NativeStep `
        -Name "Checkout target commit in temp repo" `
        -FilePath "git" `
        -Arguments @("-C", $tempRepo, "checkout", "--quiet", $targetCommit)

    # 4) Reapply local delta onto latest upstream candidate.
    Invoke-NativeStep `
        -Name "Check patch applicability on target commit" `
        -FilePath "git" `
        -Arguments @("-C", $tempRepo, "apply", "--check", $resolvedPatch)

    Invoke-NativeStep `
        -Name "Apply patch onto target commit" `
        -FilePath "git" `
        -Arguments @("-C", $tempRepo, "apply", "--whitespace=nowarn", $resolvedPatch)

    if ($DryRun) {
        Write-Host "[upgrade] DryRun enabled: preflight succeeded; skipping in-place update, baseline metadata update, and validations."
        return
    }

    # 5) Apply validated target+patch state in-place to workspace upstream repo.
    # We validate in temp first, then perform deterministic in-place port.
    if ($KeepBackup) {
        $backupPatch = Join-Path $repoRoot "patches\docling-parse\backup-before-upgrade-$timestamp.patch"
        Copy-Item -Path $resolvedPatch -Destination $backupPatch -Force
        Write-Host "[upgrade] kept backup patch snapshot: $backupPatch"
    }

    try {
        Invoke-NativeStep `
            -Name "Reset workspace upstream repo to target commit" `
            -FilePath "git" `
            -Arguments @("-C", $upstreamRepo, "reset", "--hard", $targetCommit)

        Invoke-NativeStep `
            -Name "Clean workspace upstream repo before patch apply" `
            -FilePath "git" `
            -Arguments @("-C", $upstreamRepo, "clean", "-fd")

        Invoke-NativeStep `
            -Name "Apply port patch in workspace upstream repo" `
            -FilePath "git" `
            -Arguments @("-C", $upstreamRepo, "apply", "--whitespace=nowarn", $resolvedPatch)

        $upgradeApplied = $true
    }
    catch {
        Write-Warning "[upgrade] in-place apply failed; attempting rollback to original commit + patch."

        try {
            Invoke-NativeStep `
                -Name "Rollback: reset to original commit" `
                -FilePath "git" `
                -Arguments @("-C", $upstreamRepo, "reset", "--hard", $originalCommit)
            Invoke-NativeStep `
                -Name "Rollback: clean upstream repo" `
                -FilePath "git" `
                -Arguments @("-C", $upstreamRepo, "clean", "-fd")
            Invoke-NativeStep `
                -Name "Rollback: re-apply original patch" `
                -FilePath "git" `
                -Arguments @("-C", $upstreamRepo, "apply", "--whitespace=nowarn", $resolvedPatch)
        }
        catch {
            Write-Warning "[upgrade] rollback failed; manual intervention required."
        }

        throw
    }

    # 6) Refresh baseline metadata after swap.
    Invoke-NativeStep `
        -Name "Update upstream baseline metadata" `
        -FilePath $powerShellHost `
        -Arguments @(
            "-ExecutionPolicy", "Bypass",
            "-File", $baselineScript,
            "-OutputPath", $BaselinePath,
            "-PatchPath", $PatchPath,
            "-TrackedRef", $TargetRef
        )

    # 7) Validate upgraded port if not skipped.
    if (-not $SkipValidation) {
        $validationFailures = @()
        $validationSteps = @(
            @{ Name = ".NET tests"; File = "dotnet"; Args = @("test", (Join-Path $repoRoot "dotnet\DoclingDotNet.slnx"), "--configuration", "Release") },
            @{ Name = "Spike build"; File = "dotnet"; Args = @("build", (Join-Path $repoRoot "dotnet\DoclingDotNet.Examples.slnx"), "--configuration", "Release") },
            @{ Name = "C ABI smoke assertions"; File = $powerShellHost; Args = @("-ExecutionPolicy", "Bypass", "-File", $smokeScript, "-SkipConfigure") },
            @{ Name = "Parity harness"; File = $powerShellHost; Args = @("-ExecutionPolicy", "Bypass", "-File", $parityScript, "-SkipConfigure", "-Output", ".artifacts/parity/docling-parse-parity-report.json", "--max-pages", "20") },
            @{ Name = "Patch applicability check"; File = $powerShellHost; Args = @("-ExecutionPolicy", "Bypass", "-File", $applyScript, "-CheckOnly") }
        )

        foreach ($step in $validationSteps) {
            try {
                Invoke-NativeStep -Name $step.Name -FilePath $step.File -Arguments $step.Args
            }
            catch {
                $validationFailures += "$($step.Name): $($_.Exception.Message)"
                if (-not $ContinueOnValidationFailure) {
                    throw
                }
            }
        }

        if ($validationFailures.Count -gt 0) {
            Write-Warning "[upgrade] validation completed with failures:"
            $validationFailures | ForEach-Object { Write-Warning " - $_" }
        }
    }
    else {
        Write-Host "[upgrade] SkipValidation enabled."
    }

    $newHead = Get-GitValue -RepoPath $upstreamRepo -Arguments @("rev-parse", "HEAD")
    Write-Host "[upgrade] completed"
    Write-Host "[upgrade] upgraded upstream head: $newHead"
    Write-Host "[upgrade] baseline metadata: $baselineFile"
}
finally {
    if (Test-Path $tempRoot) {
        if ($upgradeApplied -or -not $KeepTempOnFailure) {
            Remove-Item -Path $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
        else {
            Write-Host "[upgrade] kept temp workspace for debugging: $tempRoot"
        }
    }
}
