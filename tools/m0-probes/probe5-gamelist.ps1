<#
.SYNOPSIS
  M0 probe 5, last item: how large a gamelist.xml EmulationStation can load before browsing
  degrades.

.DESCRIPTION
  The API half of probe 5 was measured against RomM. This is the half that needs the live
  install: it sets the per-system gamelist cap that M4 has to enforce, because a gamelist is
  written per folder and nothing else bounds its size.

  "Browsing degrades" has no single number, so four are measured at each library size, all
  read from ES itself rather than judged on screen:

    load       process start -> /systems/<system>/games reports the full count. This is the
               honest cold-start cost, because the HTTP server answers /caps long before the
               gamelists are parsed, so time-to-/caps would flatter the result.
    reload     what M4 pays on every sync. /reloadgames returns in about a millisecond and
               does the work afterwards, so its response time measures nothing; the probe
               renames one entry on disk first and times how long ES takes to report the new
               name back.
    memory     ES working set and private bytes once loaded.
    read       /systems/<system>/games latency and payload size, ES's own view of the list.

  The corpus is stub rom files plus one generated gamelist.xml in an otherwise empty system,
  so nothing real is touched, and every synthetic file is removed by -Clean. Entries carry
  the fields RomM's gamelist_exporter emits, so parse cost is representative.

  By default the <image> paths are written but the image files are not created, which is the
  metadata-only case: a lower bound. -WithImages creates a real 16x16 PNG per entry to
  measure what artwork adds.

.EXAMPLE
  pwsh -File tools/m0-probes/probe5-gamelist.ps1 -Root K:\RetroBat
  pwsh -File tools/m0-probes/probe5-gamelist.ps1 -Root K:\RetroBat -Sizes 25000 -WithImages
  pwsh -File tools/m0-probes/probe5-gamelist.ps1 -Root K:\RetroBat -Clean
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [string] $System = 'snes',
    [string] $Extension = '.sfc',
    [int[]] $Sizes = @(1000, 5000, 10000, 25000, 50000, 100000),
    [switch] $WithImages,
    [switch] $Clean,
    [int] $LoadTimeoutSeconds = 600,
    [string] $OutJson
)

$ErrorActionPreference = 'Stop'
$BASE = 'http://127.0.0.1:1234'
$PREFIX = 'zzprobe5'   # every synthetic file starts with this, so cleanup is exact

$esExe = Join-Path $Root 'emulationstation\emulationstation.exe'
$esHome = Join-Path $Root 'emulationstation'
$esArgs = "--fullscreen-borderless `"--vsync 1`" --home `"$esHome`""
$romDir = Join-Path $Root "roms\$System"
$gamelist = Join-Path $romDir 'gamelist.xml'
$gamelistBackup = "$gamelist.probe5-backup"
$imageDir = Join-Path $romDir 'images'

if (-not (Test-Path $esExe)) { throw "not found: $esExe" }

# ---------------------------------------------------------------- ES process control

function Get-Es { Get-Process -Name 'emulationstation' -ErrorAction SilentlyContinue | Select-Object -First 1 }

function Invoke-Es([string] $Path, [int] $TimeoutSec = 30) {
    try {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $r = Invoke-WebRequest -Uri "$BASE$Path" -TimeoutSec $TimeoutSec -ErrorAction Stop
        $sw.Stop()
        [pscustomobject]@{ Ok = $true; Status = $r.StatusCode; Bytes = $r.RawContentLength; Body = $r.Content; Ms = $sw.Elapsed.TotalMilliseconds }
    } catch {
        [pscustomobject]@{ Ok = $false; Status = $null; Bytes = 0; Body = $_.Exception.Message; Ms = $null }
    }
}

function Stop-Es {
    $p = Get-Es
    if (-not $p) { return }
    # A 200 from /quit is not evidence ES quit (probe 3), so poll the process.
    [void](Invoke-Es '/quit' 10)
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline -and -not $p.HasExited) { Start-Sleep -Milliseconds 300; $p.Refresh() }
    if (-not $p.HasExited) { Write-Host '  /quit ignored, killing ES' -ForegroundColor Yellow; $p.Kill() }
    Start-Sleep -Seconds 2
}

# Readiness is polled through /systems, which is a few KB, rather than through
# /systems/<system>/games, which is the whole library serialised. At 100k entries that
# payload runs to ~100 MB, so polling it would load ES down and inflate the very number
# being measured.
function Get-SystemGameCount {
    $r = Invoke-Es '/systems' 60
    if (-not $r.Ok) { return -1 }
    try {
        $row = ($r.Body | ConvertFrom-Json) | Where-Object { $_.name -eq $System } | Select-Object -First 1
        if ($row) { return [int]$row.totalGames } else { return 0 }
    } catch { return -1 }
}

# Cold start, timed against the library actually being readable rather than the API answering.
function Start-EsAndWait([int] $Expected) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    Start-Process -FilePath $esExe -ArgumentList $esArgs -WorkingDirectory (Split-Path $esExe) | Out-Null

    $capsMs = $null
    $loadMs = $null
    $count = -1
    $deadline = (Get-Date).AddSeconds($LoadTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $caps = Invoke-Es '/caps' 5
        if ($caps.Ok) {
            if (-not $capsMs) { $capsMs = [math]::Round($sw.Elapsed.TotalMilliseconds) }
            $count = Get-SystemGameCount
            if ($count -ge $Expected) { $loadMs = [math]::Round($sw.Elapsed.TotalMilliseconds); break }
        }
        Start-Sleep -Milliseconds 250
    }
    $sw.Stop()
    [pscustomobject]@{ CapsMs = $capsMs; LoadMs = $loadMs; Reported = $count }
}

# ---------------------------------------------------------------- corpus

# 16x16 opaque PNG, small enough that N copies cost nothing but real enough that ES
# decodes a texture rather than skipping a missing file.
$PNG16 = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAJUlEQVR4nGP8//8/AzZgYmLCJs7EQCQYVUgxYMEmyMhIkh8BAgAA//8DAFQoAwGXrBLnAAAAAElFTkSuQmCC')

# Long enough to be representative: real scraped descriptions run to a few hundred characters,
# and the description is the bulk of a gamelist's bytes.
$DESC = ('A synthetic entry written by RomMBat M0 probe 5 to measure how large a gamelist ' +
    'EmulationStation can load. It carries the same fields RomM gamelist_exporter emits so ' +
    'that parse cost is representative rather than optimistic, including a description of ' +
    'roughly the length a scraped entry has in practice. ')

function New-Corpus([int] $Count) {
    New-Item -ItemType Directory -Path $romDir -Force | Out-Null
    if ($WithImages) { New-Item -ItemType Directory -Path $imageDir -Force | Out-Null }

    # Membership is decided from one directory enumeration rather than a Test-Path per file:
    # at 100k entries the per-file form costs minutes on removable media. It also means
    # -WithImages backfills artwork for a corpus that already has its roms.
    $haveRoms = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    Get-ChildItem $romDir -File -Filter "$PREFIX*$Extension" -ErrorAction SilentlyContinue | ForEach-Object { [void]$haveRoms.Add($_.Name) }
    $haveImages = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    if ($WithImages) {
        Get-ChildItem $imageDir -File -Filter "$PREFIX*.png" -ErrorAction SilentlyContinue | ForEach-Object { [void]$haveImages.Add($_.Name) }
    }

    for ($i = 0; $i -lt $Count; $i++) {
        $stem = "{0}-{1:D6} (USA)" -f $PREFIX, $i
        if (-not $haveRoms.Contains("$stem$Extension")) {
            [IO.File]::WriteAllBytes((Join-Path $romDir "$stem$Extension"), [byte[]]::new(0))
        }
        if ($WithImages -and -not $haveImages.Contains("$stem-image.png")) {
            [IO.File]::WriteAllBytes((Join-Path $imageDir "$stem-image.png"), $PNG16)
        }
    }

    if ($haveRoms.Count -gt $Count) {
        Get-ChildItem $romDir -File -Filter "$PREFIX*$Extension" | Sort-Object Name | Select-Object -Skip $Count | ForEach-Object {
            Remove-Item $_.FullName -Force
            Remove-Item (Join-Path $imageDir ($_.BaseName + '-image.png')) -Force -ErrorAction SilentlyContinue
        }
    }

    # Back up the real gamelist once, and never back up one this probe wrote: a second run
    # against an existing corpus would otherwise snapshot the synthetic file and "restore"
    # it over the real one at cleanup.
    if ((Test-Path $gamelist) -and -not (Test-Path $gamelistBackup)) {
        if (-not (Select-String -Path $gamelist -Pattern $PREFIX -SimpleMatch -Quiet)) {
            Copy-Item $gamelist $gamelistBackup -Force
        }
    }

    $sw = [IO.StreamWriter]::new($gamelist, $false, [Text.UTF8Encoding]::new($false))
    try {
        $sw.WriteLine('<?xml version="1.0"?>')
        $sw.WriteLine('<gameList>')
        for ($i = 0; $i -lt $Count; $i++) {
            $name = "{0}-{1:D6} (USA)" -f $PREFIX, $i
            $sw.WriteLine("`t<game>")
            $sw.WriteLine("`t`t<path>./$name$Extension</path>")
            $sw.WriteLine("`t`t<name>Probe Game $i (USA)</name>")
            $sw.WriteLine("`t`t<desc>$DESC</desc>")
            $sw.WriteLine("`t`t<image>./images/$name-image.png</image>")
            $sw.WriteLine("`t`t<rating>0.7</rating>")
            $sw.WriteLine("`t`t<releasedate>19960301T000000</releasedate>")
            $sw.WriteLine("`t`t<developer>RomMBat Probe</developer>")
            $sw.WriteLine("`t`t<publisher>RomMBat Probe</publisher>")
            $sw.WriteLine("`t`t<genre>Action</genre>")
            $sw.WriteLine("`t`t<players>1-2</players>")
            $sw.WriteLine("`t</game>")
        }
        $sw.WriteLine('</gameList>')
    } finally { $sw.Dispose() }

    (Get-Item $gamelist).Length
}

# /reloadgames answers in about a millisecond and reloads afterwards, so time the effect
# rather than the response: drop one more rom in, then poll the cheap /systems count until
# ES reports it. This is what M4 pays after writing a gamelist.
function Measure-Reload([int] $Count) {
    $sentinelRom = Join-Path $romDir "$PREFIX-sentinel$Extension"
    [IO.File]::WriteAllBytes($sentinelRom, [byte[]]::new(0))

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $call = Invoke-Es '/reloadgames' 600
    $deadline = (Get-Date).AddSeconds($LoadTimeoutSeconds)
    $seen = $false
    while ((Get-Date) -lt $deadline) {
        if ((Get-SystemGameCount) -gt $Count) { $seen = $true; break }
        Start-Sleep -Milliseconds 250
    }
    $sw.Stop()

    Remove-Item $sentinelRom -Force -ErrorAction SilentlyContinue
    [void](Invoke-Es '/reloadgames' 600)
    [pscustomobject]@{
        CallMs   = if ($call.Ms) { [math]::Round($call.Ms) } else { $null }
        EffectMs = if ($seen) { [math]::Round($sw.Elapsed.TotalMilliseconds) } else { $null }
    }
}

function Remove-Corpus {
    if (Test-Path $romDir) {
        Get-ChildItem $romDir -File -Filter "$PREFIX*" -ErrorAction SilentlyContinue | Remove-Item -Force
    }
    if (Test-Path $imageDir) {
        Get-ChildItem $imageDir -File -Filter "$PREFIX*" -ErrorAction SilentlyContinue | Remove-Item -Force
        if (-not (Get-ChildItem $imageDir -Force -ErrorAction SilentlyContinue)) { Remove-Item $imageDir -Force }
    }
    if (Test-Path $gamelistBackup) { Move-Item $gamelistBackup $gamelist -Force }
    elseif (Test-Path $gamelist) { Remove-Item $gamelist -Force }
    Write-Host "  removed the synthetic corpus from roms/$System" -ForegroundColor DarkGray
}

# ---------------------------------------------------------------- run

if ($Clean) { Stop-Es; Remove-Corpus; return }

$results = @()
Write-Host "=== gamelist ceiling: roms/$System on $Root ===" -ForegroundColor Cyan
Write-Host "  images on disk: $([bool]$WithImages)"

try {
    foreach ($n in ($Sizes | Sort-Object)) {
        Write-Host ''
        Write-Host "--- $n entries" -ForegroundColor Cyan
        Stop-Es
        $bytes = New-Corpus $n
        Write-Host ("  gamelist.xml: {0:N0} bytes ({1:N1} MB)" -f $bytes, ($bytes / 1MB))

        $start = Start-EsAndWait $n
        if (-not $start.LoadMs) {
            Write-Host "  ES did not report $n games within $LoadTimeoutSeconds s (reported $($start.Reported))" -ForegroundColor Red
        }

        $p = Get-Es
        $ws = if ($p) { $p.WorkingSet64 } else { 0 }
        $pb = if ($p) { $p.PrivateMemorySize64 } else { 0 }

        $read = Invoke-Es "/systems/$System/games" 300
        $reload = Measure-Reload $n
        $p2 = Get-Es
        $wsAfter = if ($p2) { $p2.WorkingSet64 } else { 0 }

        $row = [pscustomobject]@{
            entries         = $n
            gamelist_bytes  = $bytes
            with_images     = [bool]$WithImages
            caps_ms         = $start.CapsMs
            load_ms         = $start.LoadMs
            reported_games  = $start.Reported
            working_set_mb  = [math]::Round($ws / 1MB, 1)
            private_mb      = [math]::Round($pb / 1MB, 1)
            read_ms         = if ($read.Ms) { [math]::Round($read.Ms) } else { $null }
            read_bytes      = $read.Bytes
            reload_call_ms  = $reload.CallMs
            reload_ms       = $reload.EffectMs
            ws_after_reload = [math]::Round($wsAfter / 1MB, 1)
        }
        $results += $row
        $row | Format-List | Out-String -Width 100 | Write-Host
    }
} finally {
    if ($results) {
        $results | Format-Table entries, gamelist_bytes, caps_ms, load_ms, reported_games, working_set_mb, read_ms, reload_call_ms, reload_ms -AutoSize | Out-String -Width 200 | Write-Host
        if ($OutJson) {
            New-Item -ItemType Directory -Path (Split-Path $OutJson) -Force | Out-Null
            $results | ConvertTo-Json -Depth 4 | Set-Content -Path $OutJson -Encoding utf8
            Write-Host "wrote $OutJson" -ForegroundColor DarkGray
        }
    }
    Write-Host ''
    Write-Host 'Corpus left in place. Run with -Clean to remove it and restore the gamelist.' -ForegroundColor Yellow
}
