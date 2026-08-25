<#
.SYNOPSIS
  M7 stage 7a, probe 1: what happens between the `quit` hook firing and EmulationStation
  actually being gone.

.DESCRIPTION
  `background quit` has to apply queued es_settings.cfg changes, and a change written while
  ES is up is discarded (findings 178/179). So the pass polls for the process to exit before
  it writes, and the poll needs a budget taken from a measurement rather than guessed.

  Finding 179 already timed ES's own exit write at 2.4 s BEFORE the quit hook fired, so the
  hook is not racing another write. What was never measured is the gap this design actually
  spends: hook fired -> process gone. That is what this takes.

  Per run:
    1. Clear the spool, so the only records are this run's.
    2. Dirty `LastSystem`, which forces ES to have a changed setting and therefore to write
       es_settings.cfg on exit. Without it a start-and-quit session leaves the file alone
       (finding 33) and the run measures nothing about the write.
    3. Start ES and wait for /caps.
    4. GET /quit, then sample every $SampleMs until the process is gone: is it alive, has
       es_settings.cfg's mtime moved, has a committed .hook file appeared.
    5. Read the quit hook's own `at=` stamp out of the spool file it wrote.

  Writes into the install: the spool (which the agent owns and drains) and `LastSystem`
  (which ES rewrites on every exit anyway). Nothing else is touched. Artifacts land in
  -OutDir, outside the tree.
#>
param(
    [string] $Root = 'K:\RetroBat',
    [int]    $Runs = 3,
    [int]    $SampleMs = 25,
    [int]    $DwellSeconds = 5,
    [int]    $QuitTimeoutSeconds = 120,
    [string] $OutDir = (Join-Path $PSScriptRoot '..\..\probe-output\m7-probe1')
)

$ErrorActionPreference = 'Stop'
$BASE = 'http://127.0.0.1:1234'

$esExe = Join-Path $Root 'emulationstation\emulationstation.exe'
$esHome = Join-Path $Root 'emulationstation'
$esArgs = "--fullscreen-borderless --home `"$esHome`""
$settings = Join-Path $Root 'emulationstation\.emulationstation\es_settings.cfg'
$spool = Join-Path $Root 'emulators\rommbat\spool'

if (-not (Test-Path $esExe)) { throw "not found: $esExe" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Get-Es { Get-Process -Name 'emulationstation' -ErrorAction SilentlyContinue | Select-Object -First 1 }

function Invoke-Es([string] $Path, [int] $TimeoutSec = 10) {
    try {
        $r = Invoke-WebRequest -Uri "$BASE$Path" -TimeoutSec $TimeoutSec -ErrorAction Stop
        [pscustomobject]@{ Ok = $true; Status = [int]$r.StatusCode }
    } catch {
        [pscustomobject]@{ Ok = $false; Status = $null }
    }
}

function Get-Mtime([string] $Path) {
    if (Test-Path $Path) { (Get-Item $Path).LastWriteTimeUtc } else { $null }
}

# Forces a changed setting so ES has a reason to write on exit. ES rewrites LastSystem itself
# on every session, so leaving a bogus value behind changes nothing a user would notice.
function Set-DirtyLastSystem {
    $text = Get-Content -Raw -Path $settings
    $dirty = [regex]::Replace($text, '(<string name="LastSystem" value=")[^"]*(")', '${1}zzprobe${2}')
    if ($dirty -eq $text) { Write-Host '  LastSystem not present, nothing dirtied' -ForegroundColor Yellow }
    Set-Content -Path $settings -Value $dirty -NoNewline
}

$results = @()

for ($run = 1; $run -le $Runs; $run++) {
    Write-Host "=== run $run of $Runs ===" -ForegroundColor Cyan

    $p = Get-Es
    if ($p) { throw 'EmulationStation is already running. Close it first.' }

    Remove-Item -Path (Join-Path $spool '*') -Force -ErrorAction SilentlyContinue
    Set-DirtyLastSystem

    $settingsBefore = Get-Mtime $settings
    $settingsHashBefore = (Get-FileHash -Algorithm MD5 $settings).Hash

    Start-Process -FilePath $esExe -ArgumentList $esArgs -WorkingDirectory (Split-Path $esExe) | Out-Null

    $up = $false
    $deadline = (Get-Date).AddSeconds(180)
    while ((Get-Date) -lt $deadline) {
        if ((Invoke-Es '/caps' 5).Ok) { $up = $true; break }
        Start-Sleep -Milliseconds 250
    }
    if (-not $up) { throw 'ES never answered /caps' }
    Write-Host '  up'

    Start-Sleep -Seconds $DwellSeconds

    $process = Get-Es
    if (-not $process) { throw 'ES answered /caps and has no process' }

    $samples = @()
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $tQuitRequest = (Get-Date).ToUniversalTime()

    [void](Invoke-Es '/quit' 10)
    $quitReturnedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 1)

    $settingsMovedMs = $null
    $hookSeenMs = $null
    $goneMs = $null
    $limit = $QuitTimeoutSeconds * 1000

    while ($sw.Elapsed.TotalMilliseconds -lt $limit) {
        $process.Refresh()
        $alive = -not $process.HasExited
        $mtime = Get-Mtime $settings
        $hooks = @(Get-ChildItem -Path $spool -Filter '*.hook' -ErrorAction SilentlyContinue)

        $at = [math]::Round($sw.Elapsed.TotalMilliseconds, 1)
        $samples += [pscustomobject]@{ ms = $at; alive = $alive; settingsMtime = $mtime; hooks = $hooks.Count }

        if (-not $settingsMovedMs -and $mtime -ne $settingsBefore) { $settingsMovedMs = $at }
        if (-not $hookSeenMs -and $hooks.Count -gt 0) { $hookSeenMs = $at }
        if (-not $goneMs -and -not $alive) { $goneMs = $at }

        # Keep sampling three seconds past the exit, which is where a write ES makes on the
        # way out but after the hook would show up.
        if ($goneMs -and $at -gt $goneMs + 3000) { break }
        Start-Sleep -Milliseconds $SampleMs
    }

    $sw.Stop()

    # The hook stamps its own record to the millisecond, which is the only clock that says
    # when the hook fired rather than when this loop noticed the file.
    $quitRecord = $null
    foreach ($f in @(Get-ChildItem -Path $spool -Filter '*.hook' -ErrorAction SilentlyContinue)) {
        $body = Get-Content -Raw -Path $f.FullName
        if ($body -match 'event=quit') {
            if ($body -match 'at=([^\r\n]+)') { $quitRecord = [datetime]::Parse($Matches[1]).ToUniversalTime() }
        }
        Copy-Item $f.FullName (Join-Path $OutDir ("run$run-" + $f.Name))
    }

    $results += [pscustomobject]@{
        run                   = $run
        quitRequestUtc        = $tQuitRequest.ToString('o')
        quitReturnedMs        = $quitReturnedMs
        quitHookAtUtc         = if ($quitRecord) { $quitRecord.ToString('o') } else { $null }
        hookFiredMsAfterQuit  = if ($quitRecord) { [math]::Round(($quitRecord - $tQuitRequest).TotalMilliseconds, 1) } else { $null }
        settingsMovedMs       = $settingsMovedMs
        processGoneMs         = $goneMs
        hookToGoneMs          = if ($quitRecord -and $goneMs) { [math]::Round($goneMs - ($quitRecord - $tQuitRequest).TotalMilliseconds, 1) } else { $null }
        settingsHashChanged   = ((Get-FileHash -Algorithm MD5 $settings).Hash -ne $settingsHashBefore)
        samples               = $samples.Count
    }

    $samples | Export-Csv -NoTypeInformation -Path (Join-Path $OutDir "run$run-samples.csv")
    $results[-1] | Format-List | Out-String | Write-Host

    Start-Sleep -Seconds 3
}

$results | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $OutDir 'summary.json')
$results | Format-Table -AutoSize
Write-Host "artifacts in $OutDir"
