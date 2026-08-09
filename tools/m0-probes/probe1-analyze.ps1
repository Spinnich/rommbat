<#
.SYNOPSIS
  M0 probe 1: correlate the hook log against emulatorLauncher.log to get the blocking answer.

.DESCRIPTION
  emulatorLauncher.log timestamps to the millisecond and records the full launch command
  line, so it is the ground truth for when a game actually started. The hook log records
  when the hook ran. The gap between a game-start hook and the launcher startup that
  follows it is the delay the hook imposed.

  Run after playing a game with the probe hooks installed.

.EXAMPLE
  pwsh -File tools/m0-probes/probe1-analyze.ps1 -Root G:\RetroBat
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root
)

$ErrorActionPreference = 'Stop'

$hookLog = Join-Path $Root 'rommbat-probe\hooks.log'
$launcherLog = Join-Path $Root 'emulationstation\emulatorLauncher.log'

if (-not (Test-Path $hookLog)) {
    Write-Warning "no hook log at $hookLog - either no event fired, or the hook could not resolve the root."
    Write-Warning 'A missing log is itself a finding: it means %~dp0..\..\..\.. did not reach the root.'
    return
}

Write-Host '=== events captured ===' -ForegroundColor Cyan
$raw = Get-Content $hookLog

$events = [System.Collections.Generic.List[object]]::new()
$current = $null
foreach ($line in $raw) {
    if ($line -match '^=== EVENT=(?<ev>\S+) T_START=(?<ts>.+?) PID=') {
        if ($current) { $events.Add($current) }
        $current = [pscustomobject]@{
            Event  = $Matches.ev
            Start  = [datetime]::Parse($Matches.ts.Trim())
            Args   = [ordered]@{}
            Slept  = $null
        }
    } elseif ($current -and $line -match '^\s+(?<k>[A-Z0-9_]+)=(?<v>.*)$') {
        if ($Matches.k -eq 'SLEPT') { $current.Slept = $Matches.v }
        else { $current.Args[$Matches.k] = $Matches.v }
    }
}
if ($current) { $events.Add($current) }

foreach ($e in $events) {
    Write-Host ''
    Write-Host ("{0}  @ {1:HH:mm:ss.ff}" -f $e.Event, $e.Start) -ForegroundColor Green
    foreach ($k in $e.Args.Keys) {
        $v = $e.Args[$k]
        if ($v) { Write-Host ("    {0,-14} {1}" -f $k, $v) }
    }
    $empty = @($e.Args.Keys | Where-Object { $_ -match '^\d$' -and -not $e.Args[$_] })
    if ($empty) { Write-Host ("    {0,-14} {1}" -f '(empty)', ($empty -join ', ')) -ForegroundColor DarkGray }
}

if (-not (Test-Path $launcherLog)) {
    Write-Warning "no emulatorLauncher.log at $launcherLog; cannot compute the blocking delta."
    return
}

Write-Host ''
Write-Host '=== blocking measurement ===' -ForegroundColor Cyan

$launches = Select-String -Path $launcherLog -Pattern '^\uFEFF?(?<ts>[\d\-]+ [\d:.]+) \[INFO\]\s+\[Startup\].*-rom ' |
    ForEach-Object {
        if ($_.Line -match '^\uFEFF?(?<ts>[\d\-]+ [\d:.]+)') {
            [pscustomobject]@{
                Time = [datetime]::Parse($Matches.ts)
                Line = $_.Line
            }
        }
    }

if (-not $launches) {
    Write-Warning 'no game launches found in emulatorLauncher.log.'
    return
}

foreach ($gs in $events | Where-Object Event -eq 'game-start') {
    # The launch this hook belongs to is the first one at or after the hook fired.
    $after = $launches | Where-Object { $_.Time -ge $gs.Start.AddSeconds(-2) } | Select-Object -First 1
    if (-not $after) {
        Write-Host ("game-start @ {0:HH:mm:ss.ff}  -> no launcher startup after it" -f $gs.Start) -ForegroundColor Yellow
        continue
    }
    $delta = ($after.Time - $gs.Start).TotalSeconds
    $verdict = if ($gs.Slept) {
        if ($delta -ge ([double]$gs.Slept - 1)) { 'BLOCKS (delay propagated to launch)' } else { 'does NOT block (launch beat the sleep)' }
    } else { 'baseline' }

    Write-Host ("game-start @ {0:HH:mm:ss.ff} -> launcher @ {1:HH:mm:ss.fff}  delta {2,6:N2}s  slept={3}  {4}" -f `
            $gs.Start, $after.Time, $delta, ($gs.Slept ?? '0'), $verdict)
}

Write-Host ''
Write-Host '=== ordering within an event folder ===' -ForegroundColor Cyan
$updatestores = Select-String -Path $launcherLog -Pattern 'scripts\\(start|update-gamelists)\\.*-updatestores' |
    Select-Object -Last 3
if ($updatestores) {
    Write-Host 'updatestores.bat also ran, so ES executes every script in an event folder:'
    $updatestores | ForEach-Object { Write-Host "    $($_.Line.Trim())" }
} else {
    Write-Host 'updatestores.bat did not run in this session (or the log rotated).'
}
