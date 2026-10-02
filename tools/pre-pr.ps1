#Requires -Version 7

<#
.SYNOPSIS
    Runs every gate CI runs, locally, and says which failed.

.DESCRIPTION
    The gates, in CI's order: a Release build with -warnaserror and --no-incremental, the
    hook and agent publish the process-level tests need, the test suite, the install package
    tools/publish.ps1 builds, trunk check through WSL, the docs checker's own tests and the
    docs check, the guide's mkdocs build --strict, reference/verify.py and the two generator --check runs, and the LF check on
    *.sh. Every gate runs even after one fails, apart from the tests, which need the build.

    In a linked git worktree the trunk gate is reported as skipped: trunk in WSL cannot read
    one, and CI's trunk check covers it.

    --no-incremental is the point of the build line: an incremental build after a failed one
    can report 0 errors and leave the tests running against stale binaries
    (pre-pr-verification, "Always").

.PARAMETER Skip
    Gates to leave out, by name: build, publish, test, package, trunk, docs, guide,
    reference, line-endings. A skipped gate is reported as skipped, never as passed. Skipping build
    runs the tests against whatever binaries are already there, and skipping publish runs
    the process-level tests against whatever hook and agent an earlier publish left.

.PARAMETER Fix
    Run trunk fmt before trunk check.

.PARAMETER Quiet
    Hold each gate's output and print only its name, plus the last 80 lines of a gate that
    fails. For agent sessions, where the full build and test output would fill the context.

.PARAMETER WslDistro
    The WSL distribution that has trunk installed.

.EXAMPLE
    pwsh -File tools/pre-pr.ps1
    pwsh -File tools/pre-pr.ps1 -Skip trunk,reference
    pwsh -File tools/pre-pr.ps1 -Quiet
#>

[CmdletBinding()]
param(
    [string[]] $Skip = @(),
    [switch] $Fix,
    [switch] $Quiet,
    [string] $WslDistro = 'Ubuntu'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Under pwsh -File, -Skip a,b arrives as the one string 'a,b', so split it here rather than
# relying on ValidateSet.
$gates = 'build', 'publish', 'test', 'package', 'trunk', 'docs', 'guide', 'reference', 'line-endings'
$Skip = @($Skip -split ',' | ForEach-Object Trim | Where-Object { $_ })
$unknown = $Skip | Where-Object { $_ -notin $gates }
if ($unknown) { throw "Unknown gate in -Skip: $($unknown -join ', '). The gates: $($gates -join ', ')." }

$root = Split-Path -Parent $PSScriptRoot
$results = [ordered]@{}

function Invoke-Gate {
    param([string] $Name, [scriptblock] $Body)

    if ($Skip -contains $Name) {
        $results[$Name] = 'skipped'
        return
    }
    Write-Host "`n==> $Name" -ForegroundColor Cyan
    Push-Location $root
    $output = [System.Collections.Generic.List[object]]::new()
    try {
        if ($Quiet) { & $Body *>&1 | ForEach-Object { $output.Add($_) } } else { & $Body }
        $results[$Name] = if ($LASTEXITCODE -eq 0) { 'passed' } else { "failed (exit $LASTEXITCODE)" }
    }
    catch {
        $results[$Name] = "failed ($($_.Exception.Message))"
    }
    finally {
        Pop-Location
    }
    if ($Quiet -and $results[$Name] -ne 'passed') {
        $output | Select-Object -Last 80 | ForEach-Object { Write-Host $_ }
    }
}

# python3 on Windows can be the Store's placeholder, which prints a hint and exits non-zero.
$python = foreach ($candidate in 'python3', 'python') {
    $cmd = Get-Command $candidate -ErrorAction SilentlyContinue
    if ($cmd -and (& $cmd.Source --version 2>$null) -match '^Python 3') { $cmd.Source; break }
}
$needPython = { if (-not $python) { throw 'no Python 3 on PATH' } }

Invoke-Gate build {
    dotnet build -c Release -warnaserror --no-incremental
}

Invoke-Gate publish {
    # CI publishes these so the two process-level tests run instead of skipping.
    dotnet publish src/RomMBat.Hook -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
    if ($LASTEXITCODE -eq 0) {
        dotnet publish src/RomMBat.Agent -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
    }
}

if ($results['build'] -notin 'passed', 'skipped') {
    $results['test'] = 'not run (build failed)'
}
else {
    Invoke-Gate test {
        dotnet test -c Release --no-build
    }
}

Invoke-Gate package {
    # CI's publish-check job: all three projects, the seven-file set, and the zip.
    pwsh -NoProfile -File tools/publish.ps1
}

# A linked worktree's .git file and its back-pointer both hold Windows paths, which trunk's
# git inside WSL cannot follow, and neither GIT_DIR nor relative worktree paths get it there.
if ((Test-Path (Join-Path $root '.git') -PathType Leaf) -and $Skip -notcontains 'trunk') {
    $results['trunk'] = "skipped (a git worktree, which trunk in WSL cannot read; CI's trunk check covers it)"
}
else {
    Invoke-Gate trunk {
        # Trunk has no Windows CLI. D:\a b becomes /mnt/d/a b inside WSL.
        $wslRoot = '/mnt/' + $root.Substring(0, 1).ToLowerInvariant() + ($root.Substring(2) -replace '\\', '/')
        # Quiet holds the output, so trunk's "Apply formatting?" prompt would wait unseen.
        $check = if ($Quiet) { 'trunk check --no-fix' } else { 'trunk check' }
        $steps = if ($Fix) { "trunk fmt && $check" } else { $check }
        wsl -d $WslDistro -- bash -lc "cd '$wslRoot' && $steps"
    }
}

Invoke-Gate docs {
    & $needPython
    & $python -m unittest discover -s tools/docs
    if ($LASTEXITCODE -eq 0) { & $python tools/docs/check.py }
}

Invoke-Gate guide {
    # CI's Guide workflow. The site goes to a temp folder so the gate leaves the tree as it was.
    & $needPython
    & $python -m mkdocs --version *> $null
    if ($LASTEXITCODE -ne 0) { throw 'no MkDocs; pip install -r tools/docs/requirements.txt' }
    $site = Join-Path ([IO.Path]::GetTempPath()) "rommbat-guide-$PID"
    try { & $python -m mkdocs build --strict --quiet --site-dir $site }
    finally { Remove-Item -Recurse -Force $site -ErrorAction SilentlyContinue }
}

Invoke-Gate reference {
    & $needPython
    Push-Location reference
    try { & $python verify.py } finally { Pop-Location }
    if ($LASTEXITCODE -eq 0) { & $python tools/build-platform-map.py --check }
    if ($LASTEXITCODE -eq 0) { & $python tools/build-bios-manifest.py --check }
}

Invoke-Gate line-endings {
    # CI's check: every shell script is LF in the index, whatever the working tree holds.
    $crlf = git ls-files --eol -- '*.sh' | Where-Object { $_ -notmatch '^i/(lf|none) ' }
    $crlf | ForEach-Object { Write-Host "CRLF in $(($_ -split "`t")[-1])" }
    $global:LASTEXITCODE = [int] [bool] $crlf
}

Write-Host "`n==> Summary" -ForegroundColor Cyan
$failed = $false
foreach ($gate in $results.Keys) {
    $state = $results[$gate]
    $colour = switch -Wildcard ($state) { 'passed' { 'Green' } 'skipped*' { 'Yellow' } default { 'Red' } }
    if ($colour -eq 'Red') { $failed = $true }
    Write-Host ('  {0,-13} {1}' -f $gate, $state) -ForegroundColor $colour
}
exit ([int] $failed)
