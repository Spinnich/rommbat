#Requires -Version 7

<#
.SYNOPSIS
    Runs every gate CI runs, locally, and says which failed.

.DESCRIPTION
    The gates, in CI's order: a Release build with -warnaserror and --no-incremental, the
    hook and agent publish the process-level tests need, the test suite, trunk check through
    WSL, the docs check, and reference/verify.py. Every gate runs even after one fails, apart
    from the tests, which need the build.

    --no-incremental is the point of the build line: an incremental build after a failed one
    can report 0 errors and leave the tests running against stale binaries
    (pre-pr-verification, "Always").

.PARAMETER Skip
    Gates to leave out, by name: build, publish, test, trunk, docs, reference. A skipped gate
    is reported as skipped, never as passed.

.PARAMETER Fix
    Run trunk fmt before trunk check.

.PARAMETER WslDistro
    The WSL distribution that has trunk installed.

.EXAMPLE
    pwsh -File tools/pre-pr.ps1
    pwsh -File tools/pre-pr.ps1 -Skip trunk,reference
#>

[CmdletBinding()]
param(
    [ValidateSet('build', 'publish', 'test', 'trunk', 'docs', 'reference')]
    [string[]] $Skip = @(),
    [switch] $Fix,
    [string] $WslDistro = 'Ubuntu'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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
    try {
        & $Body
        $results[$Name] = if ($LASTEXITCODE -eq 0) { 'passed' } else { "failed (exit $LASTEXITCODE)" }
    }
    catch {
        $results[$Name] = "failed ($($_.Exception.Message))"
    }
    finally {
        Pop-Location
    }
}

# python3 on Windows can be the Store's placeholder, which prints a hint and exits non-zero.
$python = foreach ($candidate in 'python3', 'python') {
    $cmd = Get-Command $candidate -ErrorAction SilentlyContinue
    if ($cmd -and (& $cmd.Source --version 2>$null) -match '^Python 3') { $cmd.Source; break }
}
if (-not $python) { throw 'No Python 3 on PATH. The docs and reference gates need one.' }

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

if ($results.Contains('build') -and $results['build'] -ne 'passed') {
    $results['test'] = 'not run (build failed)'
}
else {
    Invoke-Gate test {
        dotnet test -c Release --no-build
    }
}

Invoke-Gate trunk {
    # Trunk has no Windows CLI. D:\a b becomes /mnt/d/a b inside WSL.
    $wslRoot = '/mnt/' + $root.Substring(0, 1).ToLowerInvariant() + ($root.Substring(2) -replace '\\', '/')
    $steps = if ($Fix) { 'trunk fmt && trunk check' } else { 'trunk check' }
    wsl -d $WslDistro -- bash -lc "cd '$wslRoot' && $steps"
}

Invoke-Gate docs {
    & $python tools/docs/check.py
}

Invoke-Gate reference {
    Push-Location reference
    try { & $python verify.py } finally { Pop-Location }
}

Write-Host "`n==> Summary" -ForegroundColor Cyan
$failed = $false
foreach ($gate in $results.Keys) {
    $state = $results[$gate]
    $colour = switch -Wildcard ($state) { 'passed' { 'Green' } 'skipped' { 'Yellow' } default { 'Red' } }
    if ($colour -eq 'Red') { $failed = $true }
    Write-Host ('  {0,-10} {1}' -f $gate, $state) -ForegroundColor $colour
}
exit ([int] $failed)
