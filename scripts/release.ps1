<#
.SYNOPSIS
    Release automation helper for PSTT.Dashboard.

.DESCRIPTION
    Performs a full patch-release workflow from local source:

      1.  preflight       Verify required tools (git, dotnet, gh)
      2.  clean           Auto-commit TODO.md if the only dirty file; else abort
      3.  test-pstt       Build + test PSTT submodule (libs/PSTT)
      4.  test-blazor-diagrams  Build + test Blazor.Diagrams submodule (libs/Blazor.Diagrams)
      5.  build-debug     Build + test (Debug configuration)
      6.  build-release   Build + test (Release configuration)
      7.  publish-check   dotnet publish (Release, linux-x64, self-contained)
      8.  docker-build    docker build (local only, no push; skipped if Docker unavailable)
      9.  sync            Fetch + pull --rebase from origin
      10. version         Compute next semver tag from latest git tag
      11. changelog       Insert versioned section into CHANGELOG.md
      12. push-changelog  Commit + push the changelog update
      13. pr              Create PR → main, wait for CI checks, merge
      14. tag             Create annotated tag and push it
      15. wait-workflows  Wait for release workflows triggered by the tag
      16. post-deploy     SSH deploy: docker compose pull + up -d (skipped if DEPLOY_HOST not set)

    Steps 1–8 are purely local; steps 9–16 require git remote and/or gh CLI.

    Use -Verify to run only steps 1–8 as a quick local CI mirror — no git state
    changes, no gh required. Suitable as a Copilot post-change verification gate.

    Runs on pwsh 7+ (Windows, Linux, WSL).
    When invoked under Windows PowerShell 5.1, automatically re-launches in pwsh
    if available on PATH.

.PARAMETER DryRun
    Skip all remote operations (push, merge, tag push). Changelog and version files
    are still updated locally. To skip all git state changes, use -Verify instead.

.PARAMETER NoGh
    Disable GitHub CLI automation (PR creation, workflow polling).

.PARAMETER Verify
    Local-only verification mode. Runs steps 1–6 (preflight through docker-build)
    only. No git state changes, no gh CLI required. Use -Skip docker-build or
    -SkipPublishCheck to tune which local checks run. Suitable as a fast pre-push
    gate or Copilot post-change verification step.

.PARAMETER SkipReleaseTests
    Skip running tests for the Release build.

.PARAMETER SkipPublishCheck
    Skip the publish-check step (self-contained dotnet publish for linux-x64).

.PARAMETER ReleaseTestFilter
    Test filter expression for Release config. Default: 'TestCategory!=Playwright'.

.PARAMETER Parallel
    Run Debug and Release build+test stages concurrently (output is written to
    separate temp files and replayed on completion).

.PARAMETER WorkflowTimeoutMinutes
    Maximum minutes to wait for GitHub Actions workflows (default: 30).

.PARAMETER BumpType
    Version component to increment: patch (default), minor, or major.

.PARAMETER From
    Start from a named step, skipping all earlier ones. Useful to resume after
    a failure without re-running build/test.
    Step names: preflight clean test-pstt test-blazor-diagrams
                build-debug build-release publish-check docker-build
                sync version changelog push-changelog pr tag wait-workflows post-deploy

.PARAMETER Only
    Run exactly one named step and exit.

.PARAMETER Skip
    Step name(s) to skip. Accepts an array or a comma-separated string.

.PARAMETER NonInteractive
    Suppress all interactive prompts; abort automatically on failure.
    Implied when stdin is redirected (e.g. CI pipelines).

.PARAMETER Help
    Show this help text. Alias: -h

.PARAMETER LightBackground
    Use darker colours suitable for white/light-theme terminal backgrounds.
    Auto-detection via $Host.UI.RawUI.BackgroundColor is attempted as a
    best-effort fallback; pass this flag (or set LIGHT_BACKGROUND=1) when your
    terminal has a light background and auto-detection does not fire correctly.
    You can also set it permanently in your profile:
        $env:LIGHT_BACKGROUND = '1'

.EXAMPLE
    pwsh ./scripts/release.ps1 -h
    pwsh ./scripts/release.ps1 -Help
    pwsh ./scripts/release.ps1
    pwsh ./scripts/release.ps1 -LightBackground        # white/light terminal theme
    pwsh ./scripts/release.ps1 -DryRun
    pwsh ./scripts/release.ps1 -Verify
    pwsh ./scripts/release.ps1 -Verify -Skip docker-build
    pwsh ./scripts/release.ps1 -From sync
    pwsh ./scripts/release.ps1 -Only build-debug
    pwsh ./scripts/release.ps1 -Skip build-debug,build-release -DryRun
    pwsh ./scripts/release.ps1 -BumpType minor -WorkflowTimeoutMinutes 45

.NOTES
    Environment variable fallbacks:
      DRYRUN=1               equivalent to -DryRun
      VERIFY=1               equivalent to -Verify
      LIGHT_BACKGROUND=1     equivalent to -LightBackground (add to profile for permanent use)
      NO_GH=1                equivalent to -NoGh
      SKIP_RELEASE_TESTS=1   equivalent to -SkipReleaseTests
      SKIP_PUBLISH_CHECK=1   equivalent to -SkipPublishCheck
      RELEASE_TEST_FILTER    equivalent to -ReleaseTestFilter

    SSH deploy configuration (post-deploy step):
      DEPLOY_HOST            Remote host (step auto-skipped if not set)
      DEPLOY_USER            SSH user (default: current user)
      DEPLOY_PATH            Remote working directory (default: /opt/psttdashboard)
      DEPLOY_COMPOSE_FILE    Compose file name (default: docker-compose.yml)
#>

[CmdletBinding()]
Param(
    [switch]$DryRun,
    [switch]$Verify,
    [switch]$NoGh,
    [switch]$SkipReleaseTests,
    [switch]$SkipPublishCheck,
    [string]$ReleaseTestFilter = '',
    [switch]$Parallel,
    [int]$WorkflowTimeoutMinutes = 30,
    [ValidateSet('patch','minor','major')]
    [string]$BumpType = 'patch',
    [string]$From = '',
    [string]$Only = '',
    [string[]]$Skip = @(),
    [switch]$NonInteractive,
    [Alias('h')]
    [switch]$Help,
    [switch]$LightBackground
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ─── -Help / -h ───────────────────────────────────────────────────────────────
if ($Help) { Get-Help $MyInvocation.MyCommand.Path -Detailed; exit 0 }

# ─── Auto-restart in pwsh 7+ when invoked from Windows PowerShell 5.1 ─────────
if ($PSVersionTable.PSVersion.Major -lt 7) {
    $pwshExe = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwshExe) {
        Write-Host "Windows PowerShell detected — re-launching in pwsh 7+..." -ForegroundColor Yellow
        $fwdArgs = [System.Collections.Generic.List[string]]::new()
        $fwdArgs.Add('-NoProfile'); $fwdArgs.Add('-File'); $fwdArgs.Add($MyInvocation.MyCommand.Path)
        foreach ($kv in $MyInvocation.BoundParameters.GetEnumerator()) {
            if ($kv.Value -is [switch]) {
                if ($kv.Value.IsPresent) { $fwdArgs.Add("-$($kv.Key)") }
            } elseif ($kv.Value -is [string[]]) {
                $fwdArgs.Add("-$($kv.Key)"); $fwdArgs.AddRange([string[]]$kv.Value)
            } else {
                $fwdArgs.Add("-$($kv.Key)"); $fwdArgs.Add("$($kv.Value)")
            }
        }
        & $pwshExe.Source @fwdArgs
        exit $LASTEXITCODE
    }
    Write-Warning "pwsh (PowerShell 7+) not found on PATH. Continuing on Windows PowerShell — some features may not work correctly."
}

# ─── Repo root ────────────────────────────────────────────────────────────────
$RepoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $RepoRoot

# ─── Console color scheme ─────────────────────────────────────────────────────
# -LightBackground (or LIGHT_BACKGROUND=1) is the authoritative override for
# light-theme terminals. As a best-effort fallback, BackgroundColor is probed;
# Windows Terminal and many other emulators may not report the actual theme, so
# the explicit flag is always preferred.
$_light = $LightBackground -or $env:LIGHT_BACKGROUND -eq '1'
if (-not $_light) {
    try {
        $_bg    = $Host.UI.RawUI.BackgroundColor
        $_light = $_bg -in @(
            [ConsoleColor]::White, [ConsoleColor]::Gray,
            [ConsoleColor]::Yellow, [ConsoleColor]::Cyan, [ConsoleColor]::Green
        )
    } catch { }
}
$C = @{
    Header  = if ($_light) { 'DarkCyan'    } else { 'Cyan'   }
    Step    = 'DarkGray'    # readable on both
    Ok      = if ($_light) { 'DarkGreen'   } else { 'Green'  }
    Warn    = if ($_light) { 'DarkMagenta' } else { 'Yellow' }
    Fail    = 'Red'         # readable on both
    Active  = if ($_light) { 'Black'       } else { 'White'  }
    Dim     = 'DarkGray'
    Cyan    = if ($_light) { 'DarkCyan'    } else { 'Cyan'   }
    Yellow  = if ($_light) { 'DarkMagenta' } else { 'Yellow' }
}

function Write-Header([string]$msg) { Write-Host "`n=== $msg ===" -ForegroundColor $C.Header }
function Write-Step([string]$msg)   { Write-Host "    $msg"       -ForegroundColor $C.Step   }
function Write-Ok([string]$msg)     { Write-Host "  ✓ $msg"       -ForegroundColor $C.Ok     }
function Write-Warn([string]$msg)   { Write-Host "  ⚠ $msg"       -ForegroundColor $C.Warn   }
function Write-Fail([string]$msg)   { Write-Host "  ✗ $msg"       -ForegroundColor $C.Fail   }

# ─── Effective flags ──────────────────────────────────────────────────────────
$IsDryRun      = $DryRun      -or $env:DRYRUN              -eq '1'
$IsVerify      = $Verify      -or $env:VERIFY              -eq '1'
$IsNoGh        = $NoGh        -or $env:NO_GH               -eq '1'
$IsSkipTests   = $SkipReleaseTests -or $env:SKIP_RELEASE_TESTS -eq '1'
$IsSkipPublish = $SkipPublishCheck -or $env:SKIP_PUBLISH_CHECK -eq '1'
$EffTestFilter = if ($ReleaseTestFilter) { $ReleaseTestFilter }
                 elseif ($env:RELEASE_TEST_FILTER) { $env:RELEASE_TEST_FILTER }
                 else { 'TestCategory!=Playwright' }

# Interactive only when attached to a real terminal and not suppressed
$IsInteractive = -not $NonInteractive -and
                 -not $IsDryRun -and
                 [Environment]::UserInteractive -and
                 -not [Console]::IsInputRedirected

# ─── Step catalogue ───────────────────────────────────────────────────────────
$StepOrder = @(
    'preflight', 'clean',
    'test-pstt', 'test-blazor-diagrams',
    'build-debug', 'build-release', 'publish-check', 'docker-build',
    'sync', 'version', 'changelog', 'push-changelog',
    'pr', 'tag', 'wait-workflows', 'post-deploy'
)
# Steps that are purely local (no git remote / gh required)
$LocalSteps = @('preflight', 'clean', 'test-pstt', 'test-blazor-diagrams', 'build-debug', 'build-release', 'publish-check', 'docker-build')

$StepDesc = @{
    'preflight'            = 'Preflight checks (tools, git remote)'
    'clean'                = 'Ensure clean working tree'
    'test-pstt'            = 'Build and test PSTT submodule (libs/PSTT)'
    'test-blazor-diagrams' = 'Build and test Blazor.Diagrams submodule (libs/Blazor.Diagrams)'
    'build-debug'          = 'Build and test (Debug)'
    'build-release'        = 'Build and test (Release)'
    'publish-check'        = 'dotnet publish (Release, linux-x64, self-contained)'
    'docker-build'         = 'docker build (local verify, no push)'
    'sync'                 = 'Pull and sync with remote'
    'version'              = 'Compute next version'
    'changelog'            = 'Update CHANGELOG.md'
    'push-changelog'       = 'Commit and push changelog'
    'pr'                   = 'Create PR → wait for CI → merge'
    'tag'                  = 'Create and push release tag'
    'wait-workflows'       = 'Wait for release workflows'
    'post-deploy'          = 'SSH deploy: docker compose pull + up -d'
}

# ─── Step groups (used by the interactive menu) ───────────────────────────────
$StepGroups = [ordered]@{
    'Preflight'      = @('preflight', 'clean')
    'Build & Test'   = @('test-pstt', 'test-blazor-diagrams', 'build-debug', 'build-release', 'publish-check', 'docker-build')
    'Version'        = @('sync', 'version', 'changelog', 'push-changelog')
    'GitHub Release' = @('pr', 'tag', 'wait-workflows')
    'Deploy'         = @('post-deploy')
}
# Keywords accepted in the menu to toggle an entire group
$GroupKeywords = @{
    'preflight' = 'Preflight'
    'build'     = 'Build & Test'
    'test'      = 'Build & Test'
    'version'   = 'Version'
    'release'   = 'GitHub Release'
    'github'    = 'GitHub Release'
    'deploy'    = 'Deploy'
}

# Resolve skip set (accepts array or comma-separated strings)
$SkipSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($s in $Skip) { foreach ($part in ($s -split ',')) { [void]$SkipSet.Add($part.Trim()) } }

# Build the ordered list of steps to run
function Get-StepsToRun {
    if ($Only) { return @($Only.Trim()) }
    if ($IsVerify) {
        # Local-only mode: restrict to the local steps
        return $LocalSteps | Where-Object { -not $SkipSet.Contains($_) }
    }
    $started = [string]::IsNullOrEmpty($From)
    return $StepOrder | Where-Object {
        if (-not $started -and ($_ -ieq $From.Trim())) { $started = $true }
        $started -and -not $SkipSet.Contains($_)
    }
}

# ─── Shared state populated by steps ─────────────────────────────────────────
$script:NextVersion    = $null
$script:CurrentBranch  = $null

# ─── Command helpers ──────────────────────────────────────────────────────────

# Run a command, streaming output to the terminal. Returns exit code.
function Invoke-Cmd([string]$Exe, [string[]]$ArgList) {
    Write-Step "→ $Exe $($ArgList -join ' ')"
    & $Exe @ArgList | Out-Host   # Out-Host bypasses pipeline so $code = Invoke-Cmd captures only the exit code
    $ec = $LASTEXITCODE          # capture immediately before anything else can change it
    return $ec
}

# Run a command and return its stdout as a trimmed string (stderr discarded).
function Get-CmdOutput([string]$Exe, [string[]]$ArgList) {
    $out = & $Exe @ArgList 2>$null
    return ($out -join "`n").Trim()
}

function Assert-Cmd([string]$Exe, [string[]]$ArgList, [string]$ErrorMsg = '') {
    $code = Invoke-Cmd $Exe $ArgList
    if ($code -ne 0) { throw $(if ($ErrorMsg) { $ErrorMsg } else { "$Exe exited with code $code" }) }
}

# ─── Step: preflight ─────────────────────────────────────────────────────────
function Step-Preflight {
    foreach ($tool in @('git', 'dotnet')) {
        if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
            throw "$tool not found on PATH. Install it and ensure it is on PATH."
        }
    }
    $ghAvail = [bool](Get-Command gh -ErrorAction SilentlyContinue)
    # gh is not required in -Verify mode (local-only steps)
    if (-not $IsNoGh -and -not $IsVerify -and -not $ghAvail) {
        throw "gh CLI required but not found. Install from https://cli.github.com/ or pass -NoGh to skip GH automation."
    }
    if (($IsNoGh -or $IsVerify) -and -not $ghAvail) {
        Write-Warn "gh CLI not available — PR automation and workflow polling disabled"
    }
    if (-not $IsVerify) {
        $remote = Get-CmdOutput git @('remote', 'get-url', 'origin')
        if (-not $remote) { throw "git remote 'origin' not configured. Add an origin remote and retry." }
        Write-Ok "git, dotnet$(if ($ghAvail) { ', gh' }) found"
        Write-Ok "Remote: $remote"
    } else {
        Write-Ok "git, dotnet found (verify mode — gh and remote not required)"
    }
}

# ─── Step: clean ─────────────────────────────────────────────────────────────
function Step-CleanTree {
    $status = Get-CmdOutput git @('status', '--porcelain')
    if ([string]::IsNullOrWhiteSpace($status)) { Write-Ok "Working tree clean"; return }

    $lines = ($status -split "`n") | Where-Object { $_ -ne '' }
    $isTodoOnly = $lines.Count -eq 1 -and $lines[0] -match 'TODO\.md$'

    if ($isTodoOnly) {
        Write-Step "Only TODO.md modified — auto-committing"
        Assert-Cmd git @('add', 'TODO.md')
        Assert-Cmd git @('commit', '-m', 'chore: update TODO')
        Write-Ok "TODO.md committed"
    } else {
        throw "Working tree is dirty. Commit or stash changes before releasing.`n$status"
    }
}

# ─── Step: test-pstt ─────────────────────────────────────────────────────────
function Step-TestPstt {
    $slnx = Join-Path $RepoRoot 'libs' 'PSTT' 'PSTT.slnx'
    Write-Step "Building PSTT submodule..."
    Assert-Cmd dotnet @('build', $slnx, '-c', 'Debug') "PSTT submodule build failed"
    Write-Step "Testing PSTT submodule..."
    Assert-Cmd dotnet @('test', $slnx, '-c', 'Debug', '--no-build') "PSTT submodule tests failed"
    Write-Ok "PSTT submodule build + tests passed"
}

# ─── Step: test-blazor-diagrams ──────────────────────────────────────────────
function Step-TestBlazorDiagrams {
    $testRoot  = Join-Path $RepoRoot 'libs' 'Blazor.Diagrams' 'tests'
    $coreTests = Join-Path $testRoot 'Blazor.Diagrams.Core.Tests' 'Blazor.Diagrams.Core.Tests.csproj'
    $mainTests = Join-Path $testRoot 'Blazor.Diagrams.Tests'      'Blazor.Diagrams.Tests.csproj'
    foreach ($proj in @($coreTests, $mainTests)) {
        Write-Step "Testing $(Split-Path -Leaf $proj)..."
        Assert-Cmd dotnet @('test', $proj, '-c', 'Debug') "Tests failed: $(Split-Path -Leaf $proj)"
    }
    Write-Ok "Blazor.Diagrams submodule tests passed"
}

# ─── Step: build-debug ───────────────────────────────────────────────────────
function Step-BuildDebug {
    Write-Step "Building (Debug)..."
    Assert-Cmd dotnet @('build', 'PSTT.Dashboard.slnx', '-c', 'Debug') "Debug build failed"
    Write-Step "Testing (Debug) [filter: $EffTestFilter]..."
    Assert-Cmd dotnet @('test', 'PSTT.Dashboard.slnx', '-c', 'Debug', '--no-build', '--filter', $EffTestFilter) "Debug tests failed"
    Write-Ok "Debug build + tests passed"
}

# ─── Step: build-release ─────────────────────────────────────────────────────
function Step-BuildRelease {
    if ($Parallel) {
        # Run both configs concurrently; replay captured output afterwards.
        Write-Step "Building Debug + Release in parallel..."
        $dOut  = [System.IO.Path]::GetTempFileName()
        $rOut  = [System.IO.Path]::GetTempFileName()
        $dCmd  = "dotnet build PSTT.Dashboard.slnx -c Debug && dotnet test PSTT.Dashboard.slnx -c Debug --no-build"
        $filter = $EffTestFilter
        $rTest = if ($IsSkipTests) { '' } else { " && dotnet test PSTT.Dashboard.slnx -c Release --no-build --filter `"$filter`"" }
        $rCmd  = "dotnet build PSTT.Dashboard.slnx -c Release$rTest"
        $p1 = Start-Process pwsh -ArgumentList @('-NoProfile','-Command',$dCmd) -PassThru -RedirectStandardOutput $dOut -RedirectStandardError "$dOut.err" -NoNewWindow
        $p2 = Start-Process pwsh -ArgumentList @('-NoProfile','-Command',$rCmd) -PassThru -RedirectStandardOutput $rOut -RedirectStandardError "$rOut.err" -NoNewWindow
        Wait-Process -Id $p1.Id, $p2.Id
        Write-Host (Get-Content $dOut -Raw) -ForegroundColor Gray
        Write-Host (Get-Content $rOut -Raw) -ForegroundColor Gray
        Remove-Item $dOut, "$dOut.err", $rOut, "$rOut.err" -ErrorAction SilentlyContinue
        if ($p1.ExitCode -ne 0) { throw "Debug build/test failed" }
        if ($p2.ExitCode -ne 0) { throw "Release build/test failed" }
        Write-Ok "Parallel Debug + Release build and tests passed"
        return   # skip the sequential Release-only logic below
    }

    Write-Step "Building (Release)..."
    Assert-Cmd dotnet @('build', 'PSTT.Dashboard.slnx', '-c', 'Release') "Release build failed"
    if ($IsSkipTests) { Write-Warn "Skipping Release tests (-SkipReleaseTests)"; return }
    Write-Step "Testing (Release) [filter: $EffTestFilter]..."
    Assert-Cmd dotnet @('test', 'PSTT.Dashboard.slnx', '-c', 'Release', '--no-build', '--filter', $EffTestFilter) "Release tests failed"
    Write-Ok "Release build + tests passed"
}

# ─── Step: publish-check ─────────────────────────────────────────────────────
function Step-PublishCheck {
    if ($IsSkipPublish) { Write-Warn "Skipping publish-check (-SkipPublishCheck)"; return }
    $proj   = Join-Path $RepoRoot 'src' 'PSTT.Dashboard.WebApp' 'PSTT.Dashboard.WebApp' 'PSTT.Dashboard.WebApp.csproj'
    $outDir = Join-Path $RepoRoot 'artifacts' 'publish-check'
    Write-Step "Publishing self-contained (Release, linux-x64)..."
    try {
        Assert-Cmd dotnet @('publish', $proj, '-c', 'Release', '-r', 'linux-x64',
                            '--self-contained', 'true', '-o', $outDir) "dotnet publish failed"
        Write-Ok "Publish succeeded (linux-x64)"
    } finally {
        if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

# ─── Step: docker-build ──────────────────────────────────────────────────────
function Step-DockerBuild {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-Warn "docker not found on PATH — skipping docker-build"
        return
    }
    & docker info 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warn "Docker daemon not running — skipping docker-build"
        return
    }
    $dockerfile = Join-Path 'src' 'PSTT.Dashboard.WebApp' 'PSTT.Dashboard.WebApp' 'Dockerfile'
    Write-Step "Building Docker image (local, no push)..."
    Assert-Cmd docker @('build', '-f', $dockerfile, '-t', 'psttdashboard:local', '.') "docker build failed"
    Write-Ok "Docker image built: psttdashboard:local"
}

# ─── Step: sync ──────────────────────────────────────────────────────────────
function Step-GitSync {
    Assert-Cmd git @('fetch', '--prune') "git fetch failed"
    $script:CurrentBranch = Get-CmdOutput git @('rev-parse', '--abbrev-ref', 'HEAD')
    Write-Step "Branch: $($script:CurrentBranch)"
    $code = Invoke-Cmd git @('pull', '--rebase', 'origin', $script:CurrentBranch)
    if ($code -ne 0) { throw "git pull --rebase failed. Resolve conflicts and retry." }
    Write-Ok "Synced with origin/$($script:CurrentBranch)"
}

# ─── Step: version ───────────────────────────────────────────────────────────
function Step-ComputeVersion {
    if (-not $script:CurrentBranch) {
        $script:CurrentBranch = Get-CmdOutput git @('rev-parse', '--abbrev-ref', 'HEAD')
    }
    $allTags = Get-CmdOutput git @('tag', '--list')
    $latest = $allTags -split "`n" |
        Where-Object { $_ -match '^v?(\d+)\.(\d+)\.(\d+)$' } |
        Sort-Object { [Version]"$($_ -replace '^v','')" } |
        Select-Object -Last 1

    if ($latest -and $latest -match '^v?(\d+)\.(\d+)\.(\d+)$') {
        $maj = [int]$Matches[1]; $min = [int]$Matches[2]; $pat = [int]$Matches[3]
        $next = switch ($BumpType) {
            'major' { "v$($maj+1).0.0" }
            'minor' { "v$maj.$($min+1).0" }
            default { "v$maj.$min.$($pat+1)" }
        }
    } else {
        $latest = '(none)'; $next = 'v0.1.0'
    }
    $script:NextVersion = $next
    Write-Ok "Latest: $latest  →  Next ($BumpType bump): $next"
}

# ─── Step: changelog ─────────────────────────────────────────────────────────
function Step-UpdateChangelog {
    if (-not $script:NextVersion) { throw "Version not computed — run 'version' step first" }
    $path = Join-Path $RepoRoot 'CHANGELOG.md'
    if (-not (Test-Path $path)) { throw "CHANGELOG.md not found at $path" }

    $content = Get-Content $path -Raw
    $today   = Get-Date -Format 'yyyy-MM-dd'
    $verLine = "## [$($script:NextVersion)] - $today"

    # Insert a new versioned section immediately after ## [Unreleased]
    if ($content -match '(?m)^## \[Unreleased\]') {
        # Add blank line between [Unreleased] and the new versioned section
        $content = $content -replace '(?m)^(## \[Unreleased\])', "`$1`n`n$verLine"
    } else {
        Write-Warn "No [Unreleased] section found — prepending versioned section"
        $content = "## [Unreleased]`n`n$verLine`n`n" + $content
    }
    [System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
    Write-Ok "CHANGELOG.md updated ($verLine)"
}

# ─── Step: push-changelog ────────────────────────────────────────────────────
function Step-CommitChangelog {
    if (-not $script:CurrentBranch) {
        $script:CurrentBranch = Get-CmdOutput git @('rev-parse', '--abbrev-ref', 'HEAD')
    }
    Assert-Cmd git @('add', 'CHANGELOG.md')
    Assert-Cmd git @('commit', '-m', "chore: prepare release $($script:NextVersion)")
    if ($IsDryRun) { Write-Warn "DRYRUN: skipping push"; return }
    Assert-Cmd git @('push', 'origin', $script:CurrentBranch) "git push failed"
    Write-Ok "Changelog committed and pushed"
}

# ─── Step: pr ────────────────────────────────────────────────────────────────
function Step-CreateMergePR {
    if ($IsNoGh -or -not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Warn "NoGh / gh missing: skipping PR creation. Create and merge the PR manually, then re-run with -From tag."
        return
    }
    $branch = if ($script:CurrentBranch) { $script:CurrentBranch } else { Get-CmdOutput git @('rev-parse', '--abbrev-ref', 'HEAD') }
    Write-Step "Creating PR: $branch → main for $($script:NextVersion)"
    if ($IsDryRun) { Write-Warn "DRYRUN: skipping PR create/merge"; return }

    Assert-Cmd gh @('pr', 'create', '--title', "Release $($script:NextVersion)", '--body', "Prepare release $($script:NextVersion)", '--base', 'main', '--head', $branch) "Failed to create PR"
    $prNum = Get-CmdOutput gh @('pr', 'view', '--json', 'number', '--jq', '.number')
    if (-not $prNum) { throw "Could not determine PR number" }
    Write-Ok "PR #$prNum created"

    # Wait for CI checks using gh pr checks (accurate per-check status)
    $timeoutSec = $WorkflowTimeoutMinutes * 60
    $interval = 15; $elapsed = 0
    Write-Step "Waiting for CI checks on PR #$prNum (timeout: $WorkflowTimeoutMinutes min)..."
    while ($true) {
        Start-Sleep -Seconds $interval; $elapsed += $interval
        if ($elapsed -gt $timeoutSec) { throw "Timeout ($WorkflowTimeoutMinutes min) waiting for PR CI checks" }

        $checksJson = Get-CmdOutput gh @('pr', 'checks', $prNum, '--json', 'state,name')
        if (-not $checksJson) { Write-Step "  No checks yet..."; continue }
        try   { $checks = $checksJson | ConvertFrom-Json }
        catch { Write-Step "  Waiting for checks to appear..."; continue }
        if (-not $checks -or $checks.Count -eq 0) { Write-Step "  No checks yet..."; continue }

        $failed  = @($checks | Where-Object { $_.state -in @('FAILURE','ERROR','CANCELLED','TIMED_OUT') })
        $pending = @($checks | Where-Object { $_.state -notin @('SUCCESS','SKIPPED','NEUTRAL','FAILURE','ERROR','CANCELLED','TIMED_OUT') })

        if ($failed.Count -gt 0) { throw "CI check(s) failed: $(($failed.name) -join ', ')" }
        if ($pending.Count -eq 0) { Write-Ok "All CI checks passed"; break }
        Write-Step "  Pending: $(($pending.name) -join ', ')..."
    }

    $slug = Get-RepoSlug
    Assert-Cmd gh @('pr', 'merge', $prNum, '--merge', '--delete-branch', '--repo', $slug) "Failed to merge PR #$prNum"
    Write-Ok "PR #$prNum merged into main"
}

# ─── Step: tag ───────────────────────────────────────────────────────────────
function Step-CreateTag {
    if (-not $script:NextVersion) { throw "Version not set — run 'version' step first" }
    Assert-Cmd git @('tag', '-a', $script:NextVersion, '-m', "Release $($script:NextVersion)") "Failed to create tag"
    if ($IsDryRun) { Write-Warn "DRYRUN: skipping tag push"; return }
    Assert-Cmd git @('push', 'origin', $script:NextVersion) "Failed to push tag"
    Write-Ok "Tag $($script:NextVersion) created and pushed"
}

# ─── Step: wait-workflows ────────────────────────────────────────────────────
function Step-WaitWorkflows {
    if ($IsNoGh -or -not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Warn "gh not available: skipping workflow wait"
        return
    }
    $tag = $script:NextVersion
    $timeoutSec = $WorkflowTimeoutMinutes * 60
    $interval = 20; $elapsed = 0
    $startTime = [DateTime]::UtcNow.AddSeconds(-30)
    Write-Step "Waiting for workflows triggered by tag $tag (timeout: $WorkflowTimeoutMinutes min)..."

    while ($true) {
        Start-Sleep -Seconds $interval; $elapsed += $interval
        if ($elapsed -gt $timeoutSec) { throw "Timeout ($WorkflowTimeoutMinutes min) waiting for release workflows" }

        $runsJson = Get-CmdOutput gh @('run', 'list', '--branch', $tag, '--limit', '50', '--json', 'status,conclusion,name,startedAt')
        if (-not $runsJson) { Write-Step "  No runs detected yet for $tag..."; continue }
        try   { $runs = @($runsJson | ConvertFrom-Json | Where-Object { $_.startedAt -ge $startTime }) }
        catch { continue }
        if ($runs.Count -eq 0) { Write-Step "  Waiting for workflow runs to appear..."; continue }

        $failed     = @($runs | Where-Object { $_.status -eq 'completed' -and $_.conclusion -notin @('success','skipped','neutral') })
        $inProgress = @($runs | Where-Object { $_.status -ne 'completed' })

        if ($failed.Count -gt 0) { throw "Release workflow(s) failed: $(($failed.name) -join ', ')" }
        if ($inProgress.Count -eq 0) { Write-Ok "All release workflows succeeded"; break }
        Write-Step "  In progress: $(($inProgress.name) -join ', ')..."
    }
}

# ─── Step: post-deploy ───────────────────────────────────────────────────────
function Step-PostDeploy {
    $deployHost = $env:DEPLOY_HOST
    if (-not $deployHost) {
        Write-Warn "DEPLOY_HOST not set — skipping post-deploy."
        Write-Step "  Set DEPLOY_HOST (and optionally DEPLOY_USER, DEPLOY_PATH, DEPLOY_COMPOSE_FILE) to enable."
        return
    }
    $deployUser  = if ($env:DEPLOY_USER)         { $env:DEPLOY_USER }         else { $env:USER ?? $env:USERNAME }
    $deployPath  = if ($env:DEPLOY_PATH)         { $env:DEPLOY_PATH }         else { '/opt/psttdashboard' }
    $composeFile = if ($env:DEPLOY_COMPOSE_FILE) { $env:DEPLOY_COMPOSE_FILE } else { 'docker-compose.yml' }
    if ($deployUser -and $deployUser -ne '-') {
        $sshTarget   = "$deployUser@$deployHost"
    } else {
        $sshTarget   = $deployHost
    }
    $remoteCmd   = "cd '$deployPath' && docker compose -f '$composeFile' pull && docker compose -f '$composeFile' up -d"

    Write-Step "Deploying to $sshTarget : $deployPath ($composeFile)"
    if ($IsDryRun) { Write-Warn "DRYRUN: skipping SSH deploy"; return }

    Assert-Cmd ssh @($sshTarget, $remoteCmd) "SSH deploy failed"
    Write-Ok "Deployed to $sshTarget"
}

# ─── Helpers ─────────────────────────────────────────────────────────────────
function Get-RepoSlug {
    $url = Get-CmdOutput git @('remote', 'get-url', 'origin')
    if ($url -match 'github\.com[:/](.+?)/(.+?)(?:\.git)?$') { return "$($Matches[1])/$($Matches[2])" }
    throw "Could not parse GitHub repo slug from remote URL: $url"
}

# ─── Interactive step menu ────────────────────────────────────────────────────
function Show-StepMenu([string[]]$planned) {
    # Validate groups cover exactly the steps in $StepOrder — fail fast on drift
    $allGroupedSteps = $StepGroups.Values | ForEach-Object { $_ }
    $inOrderNotGrouped = $StepOrder | Where-Object { $_ -notin $allGroupedSteps }
    $inGroupNotOrder   = $allGroupedSteps | Where-Object { $_ -notin $StepOrder }
    if ($inOrderNotGrouped) { throw "BUG: steps in `$StepOrder but not in any `$StepGroups group: $($inOrderNotGrouped -join ', ')" }
    if ($inGroupNotOrder)   { throw "BUG: steps in `$StepGroups but not in `$StepOrder: $($inGroupNotOrder -join ', ')" }

    $selected = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($s in $planned) { [void]$selected.Add($s) }

    # Build stable 1-based number lookup
    $stepNums = @{}
    $i = 1
    foreach ($s in $StepOrder) { $stepNums[$s] = $i++ }

    while ($true) {
        Write-Host "`nStep plan — [✓] will run  [-] partial  [ ] skipped:" -ForegroundColor $C.Cyan

        foreach ($groupLabel in $StepGroups.Keys) {
            $groupSteps = $StepGroups[$groupLabel]
            $onCount    = @($groupSteps | Where-Object { $selected.Contains($_) }).Count
            $allOn      = $onCount -eq $groupSteps.Count
            $someOn     = $onCount -gt 0 -and -not $allOn
            $groupMarker = if ($allOn) { '[✓]' } elseif ($someOn) { '[-]' } else { '[ ]' }
            $gcolor      = if ($allOn) { $C.Header } elseif ($someOn) { $C.Warn } else { $C.Dim }
            $fill        = '─' * [Math]::Max(0, 46 - $groupLabel.Length)
            Write-Host " $groupMarker ── $groupLabel $fill" -ForegroundColor $gcolor

            foreach ($s in $groupSteps) {
                $on     = $selected.Contains($s)
                $marker = if ($on) { '[✓]' } else { '[ ]' }
                $color  = if ($on) { $C.Active } else { $C.Dim }
                Write-Host ("  {0,2}. {1} {2,-26} {3}" -f $stepNums[$s], $marker, $s, $StepDesc[$s]) -ForegroundColor $color
            }
        }

        Write-Host ""
        Write-Host "  Commands (comma-separate multiple):" -ForegroundColor $C.Yellow
        Write-Host "    1,3,5      toggle steps by number  |  9-14     toggle a range" -ForegroundColor $C.Dim
        Write-Host "    build      toggle a group          |  all      select all" -ForegroundColor $C.Dim
        Write-Host "    none/clear deselect all            |  exit     quit" -ForegroundColor $C.Dim
        Write-Host "    <Enter>  run selected steps" -ForegroundColor $C.Dim
        $rawInput = Read-Host '  >'

        if ([string]::IsNullOrWhiteSpace($rawInput)) { break }

        foreach ($token in ($rawInput -split ',')) {
            $t = $token.Trim().ToLower()
            if ([string]::IsNullOrEmpty($t)) { continue }

            if ($t -eq 'exit' -or $t -eq 'quit') {
                exit 0
            } elseif ($t -eq 'all') {
                foreach ($s in $StepOrder) { [void]$selected.Add($s) }
            } elseif ($t -eq 'none' -or $t -eq 'clear') {
                $selected.Clear()
            } elseif ($gKey = $GroupKeywords.Keys |
                              Where-Object { $_.StartsWith($t, [System.StringComparison]::OrdinalIgnoreCase) } |
                              Select-Object -First 1) {
                $gLabel  = $GroupKeywords[$gKey]
                $gSteps  = $StepGroups[$gLabel]
                $allSel  = @($gSteps | Where-Object { $selected.Contains($_) }).Count -eq $gSteps.Count
                foreach ($s in $gSteps) {
                    if ($allSel) { [void]$selected.Remove($s) }
                    else         { [void]$selected.Add($s)    }
                }
            } elseif ($t -match '^(\d+)-(\d+)$') {
                $lo = [Math]::Max(1,                  [Math]::Min([int]$Matches[1], [int]$Matches[2]))
                $hi = [Math]::Min($StepOrder.Count,   [Math]::Max([int]$Matches[1], [int]$Matches[2]))
                for ($n = $lo; $n -le $hi; $n++) {
                    $s = $StepOrder[$n - 1]
                    if ($selected.Contains($s)) { [void]$selected.Remove($s) }
                    else                         { [void]$selected.Add($s)    }
                }
            } elseif ($t -match '^\d+$') {
                $n = [int]$t
                if ($n -ge 1 -and $n -le $StepOrder.Count) {
                    $s = $StepOrder[$n - 1]
                    if ($selected.Contains($s)) { [void]$selected.Remove($s) }
                    else                         { [void]$selected.Add($s)    }
                }
            }
        }
    }

    # Return in canonical step order
    return [string[]]($StepOrder | Where-Object { $selected.Contains($_) })
}

# Interactive prompt when a step fails: Retry / Skip / Abort
function Prompt-OnFailure([string]$stepName) {
    if (-not $IsInteractive) { return 'abort' }
    Write-Host "`n  Step '$stepName' failed." -ForegroundColor $C.Fail
    Write-Host "  [R]etry  [S]kip  [A]bort (default)" -ForegroundColor $C.Yellow
    $choice = (Read-Host '  Choice').Trim().ToLower()
    $action = switch ($choice) {
        'r' { 'retry' }
        's' { 'skip'  }
        default { 'abort' }
    }
    return $action
}

# ─── Step dispatch table ─────────────────────────────────────────────────────
$StepFns = @{
    'preflight'            = { Step-Preflight }
    'clean'                = { Step-CleanTree }
    'test-pstt'            = { Step-TestPstt }
    'test-blazor-diagrams' = { Step-TestBlazorDiagrams }
    'build-debug'          = { Step-BuildDebug }
    'build-release'        = { Step-BuildRelease }
    'publish-check'  = { Step-PublishCheck }
    'docker-build'   = { Step-DockerBuild }
    'sync'           = { Step-GitSync }
    'version'        = { Step-ComputeVersion }
    'changelog'      = { Step-UpdateChangelog }
    'push-changelog' = { Step-CommitChangelog }
    'pr'             = { Step-CreateMergePR }
    'tag'            = { Step-CreateTag }
    'wait-workflows' = { Step-WaitWorkflows }
    'post-deploy'    = { Step-PostDeploy }
}

# ─── Main ────────────────────────────────────────────────────────────────────
try {
    if ($IsVerify)  { Write-Warn "VERIFY MODE — local checks only, no git state changes" }
    if ($IsDryRun)  { Write-Warn "DRY RUN — no pushes, merges or tags will be made" }

    $stepsToRun = [string[]](Get-StepsToRun)

    # When running interactively with no explicit step selection, show the menu
    if ($IsInteractive -and -not $Only -and -not $From -and $SkipSet.Count -eq 0) {
        $stepsToRun = [string[]](Show-StepMenu $stepsToRun)
    } else {
        Write-Host "`nSteps to run: $($stepsToRun -join ' → ')" -ForegroundColor $C.Cyan
    }

    if ($stepsToRun.Count -eq 0) { Write-Warn "No steps selected. Exiting."; exit 0 }

    $total = $stepsToRun.Count; $num = 0
    foreach ($step in $stepsToRun) {
        $num++
        Write-Header "[$num/$total] $step — $($StepDesc[$step])"

        $succeeded = $false
        while (-not $succeeded) {
            try {
                & $StepFns[$step]
                $succeeded = $true
            } catch {
                Write-Fail "Step '$step' failed: $_"
                $action = Prompt-OnFailure $step
                switch ($action) {
                    'retry' { Write-Warn "Retrying '$step'..." }
                    'skip'  { Write-Warn "Skipping '$step'"; $succeeded = $true }
                    default { throw "Aborted at step '$step': $_" }
                }
            }
        }
    }

    if ($IsVerify) {
        Write-Host "`n✓ Local verification complete — all checks passed." -ForegroundColor $C.Ok
    } else {
        Write-Host "`n✓ Release $($script:NextVersion ?? '(version not computed)') complete." -ForegroundColor $C.Ok
    }
}
catch {
    Write-Fail "RELEASE FAILED: $_"
    exit 1
}
finally {
    Pop-Location
}
