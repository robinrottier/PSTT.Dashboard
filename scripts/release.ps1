<#
.SYNOPSIS
    Release automation helper for PSTT.Dashboard.

.DESCRIPTION
    Performs a full patch-release workflow from local source:

      1.  preflight       Verify required tools (git, dotnet, gh)
      2.  clean           Auto-commit TODO.md and/or submodule pointer changes if the only dirty items; else abort
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
      13. prep-submodules Merge PSTT develop→main; pin submodule to PSTT main
      14. pr              Create PR → main, wait for CI checks, merge
      15. tag             Create annotated tag on origin/main and push it
      16. restore-submodules Restore PSTT submodule back to develop tracking [skip ci]
      17. wait-workflows  Wait for release workflows triggered by the tag
      18. post-deploy     SSH deploy: docker compose pull + up -d (skipped if DEPLOY_HOST not set)

    Steps 1–8 are purely local; steps 9–18 require git remote and/or gh CLI.

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
                sync version changelog push-changelog prep-submodules
                pr tag restore-submodules wait-workflows post-deploy

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
    Auto-detection via OSC 11 terminal query is attempted first (supported by
    Windows Terminal, iTerm2, and most modern emulators); the ConsoleColor enum
    is tried as a secondary fallback. Pass this flag (or set LIGHT_BACKGROUND=1)
    only if auto-detection still does not fire correctly for your terminal.

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
      GHCR_TOKEN             GitHub PAT with read:packages scope — used to run
                             'docker login ghcr.io' on the remote host before
                             pulling. Required if the container image is private.
      GHCR_USER              GitHub username for the registry login
                             (default: parsed from git remote URL)
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
# -LightBackground (or LIGHT_BACKGROUND=1) is the authoritative override.
# Auto-detection: first try OSC 11 (works in Windows Terminal, iTerm2, etc.),
# then fall back to the ConsoleColor enum (works in some legacy hosts).
$_light = $LightBackground -or $env:LIGHT_BACKGROUND -eq '1'
if (-not $_light -and -not [Console]::IsInputRedirected -and -not [Console]::IsOutputRedirected) {
    try {
        # OSC 11 query — terminal replies with actual background RGB
        [Console]::Write("`e]11;?`a")
        $sb = [System.Text.StringBuilder]::new()
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        while ($sw.ElapsedMilliseconds -lt 200) {
            if ([Console]::KeyAvailable) {
                $null = $sb.Append([Console]::ReadKey($true).KeyChar)
                if ($sb.ToString() -match 'rgb:([0-9a-fA-F]+)/([0-9a-fA-F]+)/([0-9a-fA-F]+)') {
                    # Components are 4-digit hex (0000–ffff); first 2 digits = 0–255
                    $r = [Convert]::ToInt32($Matches[1].Substring(0, [Math]::Min(2, $Matches[1].Length)), 16)
                    $g = [Convert]::ToInt32($Matches[2].Substring(0, [Math]::Min(2, $Matches[2].Length)), 16)
                    $b = [Convert]::ToInt32($Matches[3].Substring(0, [Math]::Min(2, $Matches[3].Length)), 16)
                    $_light = (0.2126 * $r + 0.7152 * $g + 0.0722 * $b) -gt 127
                    break
                }
            } else { [System.Threading.Thread]::Sleep(5) }
        }
        # Drain any leftover response chars so they don't pollute the menu prompt
        while ([Console]::KeyAvailable) { $null = [Console]::ReadKey($true) }
    } catch { }
}
if (-not $_light -and (Get-Command Get-PSReadLineOption -ErrorAction Ignore)) {
    # PSReadLine heuristic: count dark (30–37, 90) vs bright (91–97) ANSI fg codes.
    # Dark foreground codes → colours chosen for a light background; bright → dark background.
    # DarkGray maps to code 90 (counted as dark); White/colours 91–97 counted as bright.
    # Note: .Colors hashtable may be null; iterate named *Color properties via reflection instead.
    try {
        $esc = [char]27
        $nDark = 0; $nBright = 0
        (Get-PSReadLineOption).PSObject.Properties |
            Where-Object { $_.Name -like '*Color' } |
            ForEach-Object {
                $s = "$($_.Value)"
                if ($s -match "$esc\[(?:\d+;)*(?:3[0-6]|90)m") { $nDark++   }
                if ($s -match "$esc\[(?:\d+;)*9[1-7]m")         { $nBright++ }
            }
        if (($nDark + $nBright) -gt 0) { $_light = $nDark -gt $nBright }
    } catch { }
}
if (-not $_light) {
    # Legacy fallback: ConsoleColor enum (unreliable in Windows Terminal but works in some hosts)
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
    'prep-submodules',
    'pr', 'tag', 'restore-submodules', 'wait-workflows', 'post-deploy'
)
# Steps that are purely local (no git remote / gh required)
$LocalSteps = @('preflight', 'clean', 'test-pstt', 'test-blazor-diagrams', 'build-debug', 'build-release', 'publish-check', 'docker-build')

$StepDesc = @{
    'preflight'            = 'Preflight checks (tools, git remote)'
    'clean'                = 'Ensure clean working tree (auto-commits TODO.md / submodule pointers)'
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
    'prep-submodules'      = 'Merge PSTT develop→main; pin submodule to PSTT main'
    'pr'                   = 'Create PR → wait for CI → merge'
    'restore-submodules'   = 'Restore PSTT submodule to develop tracking'
    'tag'                  = 'Create and push release tag'
    'wait-workflows'       = 'Wait for release workflows'
    'post-deploy'          = 'SSH deploy: docker compose pull + up -d'
}

# ─── Step groups (used by the interactive menu) ───────────────────────────────
$StepGroups = [ordered]@{
    'Preflight'      = @('preflight', 'clean')
    'Build & Test'   = @('test-pstt', 'test-blazor-diagrams', 'build-debug', 'build-release', 'publish-check', 'docker-build')
    'Version'        = @('sync', 'version', 'changelog', 'push-changelog', 'prep-submodules')
    'GitHub Release' = @('pr', 'tag', 'restore-submodules', 'wait-workflows')
    'Deploy'         = @('post-deploy')
}
# Hard step dependencies (a step will throw or produce wrong output if its dep hasn't run)
$StepDeps = @{
    'changelog'      = @('version')          # uses $script:NextVersion
    'push-changelog' = @('version')          # commit message uses $script:NextVersion
    'tag'            = @('version')          # throws if $script:NextVersion is null
    'wait-workflows' = @('tag')              # nothing to poll without a pushed tag
    # post-deploy has no hard dep — it can be rerun independently at any time
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
    # Resolve a value to one or more step names.
    # Accepts: step name, 1-based number, or group keyword (expands to all steps in group).
    function Resolve-Steps([string]$value) {
        # Numeric: single step by number
        if ($value -match '^\d+$') {
            $n = [int]$value
            if ($n -ge 1 -and $n -le $StepOrder.Count) { return @($StepOrder[$n - 1]) }
            return @($value)  # out-of-range: pass through so error is reported later
        }
        # Group keyword prefix match (e.g. "deploy", "bui", "ver")
        $gKey = $GroupKeywords.Keys |
                Where-Object { $_.StartsWith($value, [System.StringComparison]::OrdinalIgnoreCase) } |
                Select-Object -First 1
        if ($gKey) { return @($StepGroups[$GroupKeywords[$gKey]]) }
        # Exact step name
        return @($value)
    }
    if ($Only) { return Resolve-Steps $Only.Trim() | Where-Object { -not $SkipSet.Contains($_) } }
    if ($IsVerify) {
        # Local-only mode: restrict to the local steps
        return $LocalSteps | Where-Object { -not $SkipSet.Contains($_) }
    }
    # -From: if it's a group keyword, start from the first step of that group
    $resolvedFrom = if ($From) { @(Resolve-Steps $From.Trim())[0] } else { '' }
    $started = [string]::IsNullOrEmpty($resolvedFrom)
    return $StepOrder | Where-Object {
        if (-not $started -and ($_ -ieq $resolvedFrom)) { $started = $true }
        $started -and -not $SkipSet.Contains($_)
    }
}

# ─── Shared state populated by steps ─────────────────────────────────────────
$script:NextVersion    = $null
$script:CurrentBranch  = $null

# ─── Command helpers ──────────────────────────────────────────────────────────

# Run a command with a spinner in interactive mode; stream verbosely otherwise.
# On success: single ✓ line with elapsed time. On failure: last ≤50 lines dumped.
function Invoke-Cmd([string]$Exe, [string[]]$ArgList) {
    $label = "$Exe $($ArgList -join ' ')"

    if (-not $IsInteractive) {
        Write-Step "→ $label"
        & $Exe @ArgList | Out-Host
        return $LASTEXITCODE
    }

    # Resolve full exe path so ProcessStartInfo can find it without UseShellExecute
    $cmdInfo     = Get-Command $Exe -ErrorAction SilentlyContinue
    $exeResolved = if ($cmdInfo) { $cmdInfo.Source } else { $Exe }

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName               = $exeResolved
    $psi.WorkingDirectory       = (Get-Location).Path
    $psi.UseShellExecute        = $false
    $psi.CreateNoWindow         = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    foreach ($a in $ArgList) { $psi.ArgumentList.Add($a) }

    $proc = [System.Diagnostics.Process]::new()
    $proc.StartInfo = $psi
    $proc.Start() | Out-Null
    $stdoutTask = $proc.StandardOutput.ReadToEndAsync()
    $stderrTask = $proc.StandardError.ReadToEndAsync()

    $width = try { [Math]::Max(40, $Host.UI.RawUI.WindowSize.Width) } catch { 80 }
    $maxLbl = $width - 14
    $disp   = if ($label.Length -gt $maxLbl) { $label.Substring(0, $maxLbl - 1) + '…' } else { $label }
    $spin   = '⠋','⠙','⠹','⠸','⠼','⠴','⠦','⠧','⠇','⠏'
    $si     = 0
    $sw     = [System.Diagnostics.Stopwatch]::StartNew()

    while (-not $proc.HasExited) {
        $elapsed  = $sw.Elapsed.ToString('m\:ss')
        $spinLine = "  $($spin[$si % $spin.Count])  $disp  [$elapsed]"
        $pad      = ' ' * [Math]::Max(0, $width - $spinLine.Length - 1)
        Write-Host "`r$spinLine$pad" -NoNewline -ForegroundColor $C.Dim
        $si++
        Start-Sleep -Milliseconds 100
    }
    $proc.WaitForExit()
    Write-Host "`r$(' ' * ($width - 1))`r" -NoNewline  # erase spinner line

    $ec      = $proc.ExitCode
    $elapsed = $sw.Elapsed.ToString('m\:ss')
    $stdout  = $stdoutTask.Result
    $stderr  = $stderrTask.Result

    if ($ec -eq 0) {
        Write-Host "  ✓  $disp  [$elapsed]" -ForegroundColor $C.Ok
    } else {
        $lines = (($stdout + "`n" + $stderr).Trim() -split "`r?\n") | Where-Object { $_ -ne '' }
        $tail  = if ($lines.Count -gt 50) { $lines[-50..-1] } else { $lines }
        if ($lines.Count -gt 50) { Write-Host "    ... ($($lines.Count - 50) earlier lines omitted) ..." -ForegroundColor $C.Dim }
        foreach ($ln in $tail) { Write-Host "    $ln" -ForegroundColor $C.Dim }
    }
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

    # Paths that may be auto-committed without human review:
    #   - TODO.md (user notes updated between sessions)
    #   - libs/PSTT, libs/Blazor.Diagrams (submodule pointer moves after commits in the submodule)
    # Only the directory/file name itself is acceptable — changes *inside* a submodule are not.
    $autoCommittable = @('TODO.md', 'libs/PSTT', 'libs/Blazor.Diagrams')

    $notAutoCommittable = $lines | Where-Object {
        $path = $_.Substring([Math]::Min(3, $_.Length)).Trim()
        $autoCommittable -notcontains $path
    }

    if ($notAutoCommittable.Count -gt 0) {
        throw "Working tree is dirty. Commit or stash changes before releasing.`n$status"
    }

    # Stage each auto-committable path that is actually dirty
    $addArgs = @()
    $parts   = @()
    if ($lines | Where-Object { $_ -match 'libs/PSTT\s*$' }) {
        $addArgs += 'libs/PSTT'
        $parts   += 'PSTT submodule pointer'
    }
    if ($lines | Where-Object { $_ -match 'libs/Blazor\.Diagrams\s*$' }) {
        $addArgs += 'libs/Blazor.Diagrams'
        $parts   += 'Blazor.Diagrams submodule pointer'
    }
    if ($lines | Where-Object { $_ -match 'TODO\.md$' }) {
        $addArgs += 'TODO.md'
        $parts   += 'TODO.md'
    }

    Write-Step "Auto-committable changes ($($parts -join ', ')) — committing"
    Assert-Cmd git (@('add') + $addArgs)
    $msg = "chore: update $($parts -join ' and ')"
    Assert-Cmd git @('commit', '-m', $msg)
    Write-Ok "Auto-committed: $msg"
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
    Assert-Cmd docker @('build', '-f', $dockerfile, '-t', 'pstt-dashboard:local', '.') "docker build failed"
    Write-Ok "Docker image built: pstt-dashboard:local"
}

# ─── Step: sync ──────────────────────────────────────────────────────────────
function Step-GitSync {
    Assert-Cmd git @('fetch', '--prune') "git fetch failed"
    $script:CurrentBranch = Get-CmdOutput git @('rev-parse', '--abbrev-ref', 'HEAD')
    Write-Step "Branch: $($script:CurrentBranch)"
    $remoteRef = "origin/$($script:CurrentBranch)"
    $remoteExists = (Invoke-Cmd git @('rev-parse', '--verify', '--quiet', $remoteRef)) -eq 0
    if ($remoteExists) {
        $code = Invoke-Cmd git @('pull', '--rebase', 'origin', $script:CurrentBranch)
        if ($code -ne 0) { throw "git pull --rebase failed. Resolve conflicts and retry." }
        Write-Ok "Synced with $remoteRef"
    } else {
        Write-Warn "Remote branch '$remoteRef' does not exist yet — skipping pull (branch will be pushed at push-changelog)"
        Write-Ok "Local-only branch '$($script:CurrentBranch)' — no remote to sync"
    }
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

# ─── Step: prep-submodules ───────────────────────────────────────────────────
function Step-PrepSubmodules {
    $psttPath = Join-Path $RepoRoot 'libs' 'PSTT'
    Push-Location $psttPath
    try {
        Write-Step "Fetching PSTT remotes..."
        Assert-Cmd git @('fetch', 'origin') "git fetch failed in PSTT"
        Write-Step "Merging PSTT develop → main..."
        Assert-Cmd git @('checkout', 'main') "git checkout main failed in PSTT"
        Assert-Cmd git @('merge', 'origin/develop', '--no-edit') "Merge develop → main failed in PSTT submodule"
        if ($IsDryRun) { Write-Warn "DRYRUN: skipping PSTT main push" }
        else { Assert-Cmd git @('push', 'origin', 'main') "git push PSTT main failed" }
    } finally { Pop-Location }

    Write-Step "Pinning Dashboard submodule pointer to PSTT main..."
    Assert-Cmd git @('config', '-f', '.gitmodules', 'submodule.libs/PSTT.branch', 'main') "Failed to update .gitmodules"
    Assert-Cmd git @('add', '.gitmodules', 'libs/PSTT') "git add failed"
    Assert-Cmd git @('commit', '-m', 'chore: pre-release — pin PSTT submodule to main') "git commit failed"
    if ($IsDryRun) { Write-Warn "DRYRUN: skipping push of submodule prep commit"; return }
    $branch = if ($script:CurrentBranch) { $script:CurrentBranch } else { Get-CmdOutput git @('rev-parse', '--abbrev-ref', 'HEAD') }
    Assert-Cmd git @('push', 'origin', $branch) "git push failed after submodule prep"
    Write-Ok "PSTT submodule pinned to main — ready for release PR"
}

# ─── Step: restore-submodules ────────────────────────────────────────────────
function Step-RestoreSubmodules {
    $psttPath = Join-Path $RepoRoot 'libs' 'PSTT'
    Push-Location $psttPath
    try {
        Write-Step "Switching PSTT submodule back to develop..."
        Assert-Cmd git @('fetch', 'origin') "git fetch failed in PSTT"
        Assert-Cmd git @('checkout', 'develop') "git checkout develop failed in PSTT"
        Assert-Cmd git @('pull', '--rebase', 'origin', 'develop') "git pull develop failed in PSTT"
    } finally { Pop-Location }

    Write-Step "Restoring .gitmodules branch tracking to develop..."
    Assert-Cmd git @('config', '-f', '.gitmodules', 'submodule.libs/PSTT.branch', 'develop') "Failed to update .gitmodules"
    Assert-Cmd git @('add', '.gitmodules', 'libs/PSTT') "git add failed"
    Assert-Cmd git @('commit', '-m', "chore: post-release — restore PSTT submodule to develop [skip ci]") "git commit failed"
    if ($IsDryRun) { Write-Warn "DRYRUN: skipping push of submodule restore commit"; return }
    $branch = if ($script:CurrentBranch) { $script:CurrentBranch } else { Get-CmdOutput git @('rev-parse', '--abbrev-ref', 'HEAD') }
    Assert-Cmd git @('push', 'origin', $branch) "git push failed after submodule restore"
    Write-Ok "PSTT submodule restored to develop tracking"
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
    # Tag the main branch HEAD — that is where the release was merged to, so
    # tag-triggered GitHub Actions workflows will display a meaningful commit.
    Assert-Cmd git @('fetch', 'origin', 'main') "Failed to fetch origin/main for tagging"
    $mainSha = Get-CmdOutput git @('rev-parse', 'origin/main')
    Assert-Cmd git @('tag', '-a', $script:NextVersion, $mainSha, '-m', "Release $($script:NextVersion)") "Failed to create tag"
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

    # Optional: log in to ghcr.io on the remote host before pulling (required for private images)
    $ghcrToken = $env:GHCR_TOKEN
    $ghcrUser  = if ($env:GHCR_USER) { $env:GHCR_USER } else {
        # Try to derive from git remote URL (github.com/<user>/<repo>)
        $remoteUrl = Get-CmdOutput git @('remote', 'get-url', 'origin')
        if ($remoteUrl -match 'github\.com[:/]([^/]+)/') { $Matches[1] } else { '' }
    }
    $loginCmd = if ($ghcrToken -and $ghcrUser) {
        "echo '$ghcrToken' | docker login ghcr.io -u '$ghcrUser' --password-stdin && "
    } else { '' }
    if ($ghcrToken -and -not $ghcrUser) {
        Write-Warn "GHCR_TOKEN set but GHCR_USER could not be determined — skipping registry login"
    }

    $remoteCmd = "${loginCmd}cd '$deployPath' && docker compose -f '$composeFile' pull && docker compose -f '$composeFile' up -d"

    Write-Step "Deploying to $sshTarget : $deployPath ($composeFile)"
    if ($ghcrToken -and $ghcrUser) { Write-Step "  ghcr.io login: $ghcrUser" }
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
    # Start with nothing selected — user builds up the run plan explicitly

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

        if ([string]::IsNullOrWhiteSpace($rawInput)) {
            # Check for missing dependencies before confirming
            $depWarnings = [System.Collections.Generic.List[string]]::new()
            foreach ($s in $StepOrder) {
                if (-not $selected.Contains($s)) { continue }
                if (-not $StepDeps.ContainsKey($s)) { continue }
                foreach ($dep in $StepDeps[$s]) {
                    if (-not $selected.Contains($dep)) {
                        $depWarnings.Add("  '$s' requires '$dep'")
                    }
                }
            }
            if ($depWarnings.Count -gt 0) {
                Write-Host "`n  ⚠ Missing dependencies:" -ForegroundColor $C.Warn
                foreach ($w in $depWarnings) { Write-Host $w -ForegroundColor $C.Warn }
                Write-Host "  Auto-add missing steps? [Y/n] " -ForegroundColor $C.Yellow -NoNewline
                $ans = (Read-Host).Trim().ToLower()
                if ($ans -eq '' -or $ans -eq 'y') {
                    foreach ($s in $StepOrder) {
                        if (-not $selected.Contains($s)) { continue }
                        if (-not $StepDeps.ContainsKey($s)) { continue }
                        foreach ($dep in $StepDeps[$s]) { [void]$selected.Add($dep) }
                    }
                    continue  # redisplay menu with deps added
                }
                # User chose to proceed anyway
            }
            break
        }

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

# Interactive prompt when a step fails: Retry / Dep+retry / Skip / Abort
function Prompt-OnFailure([string]$stepName) {
    if (-not $IsInteractive) { return 'abort' }
    Write-Host "`n  Step '$stepName' failed." -ForegroundColor $C.Fail

    # Identify deps that can be run to unblock this step
    # Use [string[]] cast so an absent key gives empty array, never $null
    [string[]]$deps = if ($StepDeps.ContainsKey($stepName)) { $StepDeps[$stepName] } else { @() }
    if ($deps.Count -gt 0) {
        Write-Host "  This step requires: $($deps -join ', ')" -ForegroundColor $C.Warn
        Write-Host "  [R]etry  [D]ep+retry (run deps first)  [S]kip  [A]bort (default)" -ForegroundColor $C.Yellow
    } else {
        Write-Host "  [R]etry  [S]kip  [A]bort (default)" -ForegroundColor $C.Yellow
    }
    $choice = (Read-Host '  Choice').Trim().ToLower()

    if ($choice -eq 'r') { return 'retry' }
    if ($choice -eq 's') { return 'skip' }
    if ($choice -eq 'd' -and $deps.Count -gt 0) {
        # Run each dep step inline so the failed step can succeed on retry
        foreach ($dep in $deps) {
            Write-Header "[dep] $dep — $($StepDesc[$dep])"
            try {
                & $StepFns[$dep]
            } catch {
                Write-Fail "Dependency step '$dep' failed: $_"
                Write-Warn "Aborting — fix '$dep' first, then retry '$stepName'"
                return 'abort'
            }
        }
        return 'retry'
    }
    return 'abort'
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
    'prep-submodules'      = { Step-PrepSubmodules }
    'pr'             = { Step-CreateMergePR }
    'restore-submodules'   = { Step-RestoreSubmodules }
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
