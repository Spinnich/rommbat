<#
.SYNOPSIS
  Does GET /reloadgames do anything while RomMBat is the app in front of EmulationStation?

.DESCRIPTION
  Finding 107 says /reloadgames is ignored while a game is running: 200 in 1 ms, and a ROM
  added to the folder still unreported five seconds later. Finding 203, which is where "the
  reload works" comes from, was measured with NO app in front of ES.

  99.md's probe P2 proved RomMBat is launched through emulatorLauncher and suspended exactly
  as a game is, which is why ES fires zero navigation events behind it. So the reload the UI
  wants to issue after installing games may be structurally impossible until RomMBat exits,
  and both docs/PLAN.md and the 7b briefs currently assume otherwise.

  A 200 is not evidence. This polls /systems for the `retrobat` system's totalGames, which is
  ES's own model, exactly as finding 203 did.

  Writes a .menu into system/es_menu, which is RomMBat's own territory and the same thing
  finding 203 wrote. Removes it again in the -Cleanup phase.

.PARAMETER Phase
  control  ES up, nothing in front. Establishes the reload works on this install today.
  live     RomMBat up from the ES menu. The question.
  after    RomMBat has exited. Does the pending change land now?
  cleanup  Remove the markers and reload.

.EXAMPLE
  .\probe6-reload-under-app.ps1 -Phase control
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('control', 'live', 'silent', 'after', 'cleanup')]
    [string]$Phase,

    [string]$Root = 'K:\RetroBat',

    [string]$Origin = 'http://127.0.0.1:1234',

    # Finding 203 measured 209 ms to visible. Five seconds is finding 107's own window and is
    # long enough that "it did not happen" means it.
    [int]$WaitSeconds = 10,

    [string]$OutDir = 'probe-output'
)

$ErrorActionPreference = 'Stop'
$system = 'retrobat'
$menuDir = Join-Path $Root 'system\es_menu'

New-Item -ItemType Directory -Force $OutDir | Out-Null
$log = Join-Path $OutDir "probe6-$Phase.log"

function Write-Stamp([string]$text) {
    $line = '{0} {1}' -f (Get-Date).ToUniversalTime().ToString('HH:mm:ss.fff'), $text
    Write-Host $line
    Add-Content -Path $log -Value $line
}

function Get-TotalGames {
    try {
        $systems = Invoke-RestMethod -Uri "$Origin/systems" -TimeoutSec 5
        $row = $systems | Where-Object { $_.name -eq $system }
        if ($null -eq $row) { return $null }
        return [int]$row.totalGames
    } catch {
        return $null
    }
}

Write-Stamp "=== phase: $Phase ==="
Write-Stamp "root=$Root origin=$Origin system=$system"

# Whether ES is even answering. A phase that cannot reach the API measures nothing, and
# saying so beats recording a null as a result.
$before = Get-TotalGames
if ($null -eq $before) {
    Write-Stamp "ES API did not answer. Is EmulationStation running?"
    exit 1
}
Write-Stamp "totalGames before: $before"

if ($Phase -eq 'cleanup') {
    Get-ChildItem $menuDir -Filter 'zzprobe*.menu' -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Stamp "removing $($_.Name)"
        Remove-Item $_.FullName -Force
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try { Invoke-WebRequest -Uri "$Origin/reloadgames" -TimeoutSec 5 | Out-Null } catch {}
    $sw.Stop()
    Write-Stamp "GET /reloadgames answered in $($sw.ElapsedMilliseconds) ms"
    Start-Sleep -Seconds 2
    Write-Stamp "totalGames after cleanup: $(Get-TotalGames)"
    exit 0
}

if ($Phase -eq 'after') {
    # No new marker. The question is whether the one written during 'live' lands once the app
    # in front has gone, with or without a second reload.
    Write-Stamp "no marker written; re-reading ES's model after RomMBat exited"
    $seen = Get-TotalGames
    Write-Stamp "totalGames with no further reload: $seen"

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try { Invoke-WebRequest -Uri "$Origin/reloadgames" -TimeoutSec 5 | Out-Null } catch {}
    $sw.Stop()
    Write-Stamp "GET /reloadgames answered in $($sw.ElapsedMilliseconds) ms"
} else {
    $marker = Join-Path $menuDir "zzprobe-$Phase.menu"
    Set-Content -Path $marker -Value 'zzprobe' -Encoding ASCII
    Write-Stamp "wrote $marker"

    if ($Phase -eq 'silent') {
        # The discriminating phase, and the whole reason it exists. The 'live' run showed the
        # count rising once RomMBat exited, which has two explanations that lead to opposite
        # designs: ES rescans on resume regardless, or the reload was deferred rather than
        # discarded. Writing a marker and issuing NO reload separates them. If the count still
        # rises on exit, the reload was never needed.
        Write-Stamp "NO reload issued: this phase measures whether ES rescans on resume by itself"
    } else {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $status = 'no answer'
        try {
            $response = Invoke-WebRequest -Uri "$Origin/reloadgames" -TimeoutSec 5
            $status = $response.StatusCode
        } catch {
            $status = "threw: $($_.Exception.Message)"
        }
        $sw.Stop()

        # The 200 is deliberately reported and deliberately not treated as the answer.
        Write-Stamp "GET /reloadgames -> $status in $($sw.ElapsedMilliseconds) ms (not evidence)"
    }
}

$deadline = (Get-Date).AddSeconds($WaitSeconds)
$changed = $false

while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 250
    $now = Get-TotalGames

    if ($null -ne $now -and $now -ne $before) {
        Write-Stamp "totalGames CHANGED $before -> $now"
        $changed = $true
        break
    }
}

if (-not $changed) {
    Write-Stamp "totalGames UNCHANGED at $before after $WaitSeconds s"
}

Write-Stamp "=== end phase: $Phase, changed=$changed ==="
