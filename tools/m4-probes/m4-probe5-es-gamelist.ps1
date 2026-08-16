<#
.SYNOPSIS
  M4 probe 5: what EmulationStation does to a gamelist RomMBat wrote, and what
  /reloadgames does in each of the three states it can be asked in.

.DESCRIPTION
  M4's no-churn regression asserts a second sync writes a byte-identical gamelist. That
  claim is only meaningful if it is made about the file ES leaves behind, not the file
  RomMBat wrote, because ES rewrites gamelist.xml on exit from its own in-memory model.

  Part A writes a gamelist carrying everything a merge has to survive, drives ES over it,
  and diffs. The corpus deliberately includes the things a naive rewrite loses: an XML
  comment, a self-closing element with attributes, an attribute on <game>, elements
  RomMBat does not own, an ampersand, a non-ASCII title, and a CRLF line ending.

  Part B calls /reloadgames with ES not running, which is the ordinary case for a
  background sync, and times the failure.

  Part C calls it while a game is running. /quit and /emukill both answer 200 and do
  nothing in that state (probe 3), so a 200 from this API is not evidence the action
  happened, and the reload has to be measured by its effect.

  Everything it writes is removed by -Clean, and the system's own gamelist is backed up
  before the first write and restored at cleanup.

.EXAMPLE
  pwsh -File tools/m4-probes/m4-probe5-es-gamelist.ps1 -Root K:\RetroBat
  pwsh -File tools/m4-probes/m4-probe5-es-gamelist.ps1 -Root K:\RetroBat -WithGame
  pwsh -File tools/m4-probes/m4-probe5-es-gamelist.ps1 -Root K:\RetroBat -Clean
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [string] $System = 'mastersystem',
    [string] $Extension = '.sms',
    [switch] $WithGame,
    # Builds the corpus around roms already in the folder instead of stubs, so Part D can
    # launch one that really runs. A stub rom fails before ES records anything, and ES only
    # rewrites a gamelist it has a reason to change.
    [switch] $UseExisting,
    [switch] $Clean,
    [int] $LoadTimeoutSeconds = 180,
    [string] $OutFile
)

$ErrorActionPreference = 'Stop'
$BASE = 'http://127.0.0.1:1234'
$PREFIX = 'zzm4probe'
$MARKER = 'written by RomMBat M4 probe 5'

$esExe = Join-Path $Root 'emulationstation\emulationstation.exe'
$esHome = Join-Path $Root 'emulationstation'
$romDir = Join-Path $Root "roms\$System"
$gamelist = Join-Path $romDir 'gamelist.xml'
$backup = "$gamelist.m4probe-backup"
$written = "$gamelist.m4probe-written"

if (-not (Test-Path $esExe)) { throw "not found: $esExe" }

$log = [Collections.Generic.List[string]]::new()
function Say([string] $Text) { $log.Add($Text); Write-Host $Text }

# ---------------------------------------------------------------- ES process control

function Get-Es { Get-Process -Name 'emulationstation' -ErrorAction SilentlyContinue | Select-Object -First 1 }

function Invoke-Es([string] $Path, [int] $TimeoutSec = 30) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    try {
        $r = Invoke-WebRequest -Uri "$BASE$Path" -TimeoutSec $TimeoutSec -ErrorAction Stop
        $sw.Stop()
        [pscustomobject]@{ Ok = $true; Status = [int]$r.StatusCode; Body = $r.Content; Ms = $sw.Elapsed.TotalMilliseconds; Error = $null }
    } catch {
        $sw.Stop()
        [pscustomobject]@{ Ok = $false; Status = $null; Body = $null; Ms = $sw.Elapsed.TotalMilliseconds; Error = $_.Exception.Message }
    }
}

function Stop-Es {
    $p = Get-Es
    if (-not $p) { return }
    [void](Invoke-Es '/quit' 10)
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline -and -not $p.HasExited) { Start-Sleep -Milliseconds 300; $p.Refresh() }
    if (-not $p.HasExited) { Say '  /quit ignored, killing ES'; $p.Kill() }
    Start-Sleep -Seconds 2
}

function Get-SystemGameCount {
    $r = Invoke-Es '/systems' 60
    if (-not $r.Ok) { return -1 }
    try {
        $row = ($r.Body | ConvertFrom-Json) | Where-Object { $_.name -eq $System } | Select-Object -First 1
        if ($row) { return [int]$row.totalGames } else { return 0 }
    } catch { return -1 }
}

function Start-EsAndWait {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    Start-Process -FilePath $esExe -ArgumentList "--windowed --resolution 1280 720 --home `"$esHome`"" -WorkingDirectory (Split-Path $esExe) | Out-Null
    $deadline = (Get-Date).AddSeconds($LoadTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ((Invoke-Es '/caps' 5).Ok) { return [math]::Round($sw.Elapsed.TotalMilliseconds) }
        Start-Sleep -Milliseconds 250
    }
    return $null
}

# ---------------------------------------------------------------- corpus

# Two stub roms, so the gamelist has an entry ES keeps and the probe can tell "rewritten
# unchanged" apart from "rewritten without the entry".
$games = @(
    @{ Stem = "$PREFIX-01 (USA)"; Name = 'Probe One & Only'; Desc = "First line.`nSecond line, with an ampersand & a <bracket>." }
    # The non-ASCII characters are built from code points so this file stays ASCII while the
    # gamelist it writes does not: e-acute, and a katakana TE outside Latin-1.
    @{ Stem = "$PREFIX-02 (Japan)"; Name = "Probe Two Pok$([char]0x00E9)mon $([char]0x30C6)"; Desc = 'A real scraped name carries characters no code page covers.' }
)

function New-Corpus {
    New-Item -ItemType Directory -Path $romDir -Force | Out-Null

    if ($UseExisting) {
        $real = Get-ChildItem $romDir -File -Filter "*$Extension" -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notlike "$PREFIX*" } | Select-Object -First 2
        if ($real.Count -lt 1) { throw "roms/$System holds no *$Extension to build the corpus from" }
        $script:games = @($real | ForEach-Object {
                @{
                    Stem = $_.BaseName
                    Name = "$($_.BaseName) & Friends"
                    Desc = "First line.`nSecond line, with an ampersand & a <bracket>."
                }
            })
    } else {
        foreach ($g in $games) { [IO.File]::WriteAllBytes((Join-Path $romDir "$($g.Stem)$Extension"), [byte[]]::new(16)) }
    }

    # Guarded on the probe's own comment, not on the file name prefix: with -UseExisting the
    # corpus carries no prefix, and a second run would otherwise snapshot the probe's file and
    # restore it over the real one at cleanup.
    if ((Test-Path $gamelist) -and -not (Test-Path $backup)) {
        if (-not (Select-String -Path $gamelist -Pattern $MARKER -SimpleMatch -Quiet)) { Copy-Item $gamelist $backup -Force }
    }

    $sb = [Text.StringBuilder]::new()
    [void]$sb.Append("<?xml version=`"1.0`"?>`n")
    [void]$sb.Append("<gameList>`n")
    # A comment RomMBat would have no reason to write, to prove unknown nodes survive.
    [void]$sb.Append("`t<!-- $MARKER -->`n")
    foreach ($g in $games) {
        # id and source are attributes a scraper writes; RomMBat owns neither.
        [void]$sb.Append("`t<game id=`"424242`" source=`"ScreenScraper.fr`">`n")
        [void]$sb.Append("`t`t<path>./$($g.Stem)$Extension</path>`n")
        [void]$sb.Append("`t`t<name>$([Security.SecurityElement]::Escape($g.Name))</name>`n")
        [void]$sb.Append("`t`t<desc>$([Security.SecurityElement]::Escape($g.Desc))</desc>`n")
        [void]$sb.Append("`t`t<genre>Action, Adventure</genre>`n")
        [void]$sb.Append("`t`t<developer>RomMBat Probe</developer>`n")
        [void]$sb.Append("`t`t<publisher>RomMBat Probe</publisher>`n")
        [void]$sb.Append("`t`t<players>1-2</players>`n")
        [void]$sb.Append("`t`t<releasedate>19910301T000000</releasedate>`n")
        [void]$sb.Append("`t`t<rating>0.85</rating>`n")
        [void]$sb.Append("`t`t<lang>en,fr</lang>`n")
        [void]$sb.Append("`t`t<region>us</region>`n")
        # Fields RomMBat does not own and must preserve through a merge.
        [void]$sb.Append("`t`t<playcount>7</playcount>`n")
        [void]$sb.Append("`t`t<lastplayed>20260810T211500</lastplayed>`n")
        [void]$sb.Append("`t`t<gametime>3600</gametime>`n")
        [void]$sb.Append("`t`t<favorite>true</favorite>`n")
        [void]$sb.Append("`t`t<hidden>false</hidden>`n")
        [void]$sb.Append("`t`t<cheevosHash>0FEC9C1F5973C7A1DC55318EC97D8D17</cheevosHash>`n")
        [void]$sb.Append("`t`t<scrap name=`"ScreenScraper`" date=`"20260611T094008`" />`n")
        # A second comment, inside the entry rather than at document level, because the two
        # placements are what tell "ES drops every comment" apart from "ES drops the ones
        # outside an entry it kept".
        [void]$sb.Append("`t`t<!-- $MARKER inside a game -->`n")
        [void]$sb.Append("`t</game>`n")
    }
    # An entry naming a file that is not on disk. M4 writes only locally present roms, so what
    # matters is what a stale entry left behind by an eviction would do: appear as a phantom
    # game, or be ignored.
    [void]$sb.Append("`t<game>`n")
    [void]$sb.Append("`t`t<path>./$PREFIX-phantom-not-on-disk$Extension</path>`n")
    [void]$sb.Append("`t`t<name>Phantom, no file behind it</name>`n")
    [void]$sb.Append("`t`t<desc>An entry for a rom that was evicted.</desc>`n")
    [void]$sb.Append("`t</game>`n")
    [void]$sb.Append("</gameList>`n")

    # UTF-8 without a BOM, which is what ES itself writes.
    [IO.File]::WriteAllText($gamelist, $sb.ToString(), [Text.UTF8Encoding]::new($false))
    Copy-Item $gamelist $written -Force
    (Get-Item $gamelist).Length
}

function Remove-Corpus {
    if (Test-Path $romDir) {
        Get-ChildItem $romDir -File -Filter "$PREFIX*" -ErrorAction SilentlyContinue | Remove-Item -Force
        Get-ChildItem $romDir -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            Get-ChildItem $_.FullName -File -Filter "$PREFIX*" -ErrorAction SilentlyContinue | Remove-Item -Force
        }
    }
    Remove-Item $written -Force -ErrorAction SilentlyContinue
    if (Test-Path $backup) { Move-Item $backup $gamelist -Force }
    elseif (Test-Path $gamelist) { Remove-Item $gamelist -Force }
    Write-Host "  removed the probe corpus from roms/$System"
}

function Compare-Gamelist([string] $Before, [string] $After) {
    $b = [IO.File]::ReadAllBytes($Before)
    $a = [IO.File]::ReadAllBytes($After)
    Say ("  bytes before {0:N0}, after {1:N0}, identical: {2}" -f $b.Length, $a.Length,
        ($b.Length -eq $a.Length -and -not (Compare-Object $b $a -SyncWindow 0)))

    $bt = [IO.File]::ReadAllText($Before)
    $at = [IO.File]::ReadAllText($After)
    Say ("  declaration:  before {0}  after {1}" -f ($bt -split "`r?`n")[0], ($at -split "`r?`n")[0])
    Say ("  BOM:          before {0}  after {1}" -f ($b[0] -eq 0xEF), ($a[0] -eq 0xEF))
    Say ("  line endings: before {0}  after {1}" -f
        $(if ($bt.Contains("`r`n")) { 'CRLF' } else { 'LF' }),
        $(if ($at.Contains("`r`n")) { 'CRLF' } else { 'LF' }))
    Say ("  tab indent:   before {0}  after {1}" -f $bt.Contains("`n`t<game"), $at.Contains("`n`t<game"))
    Say ("  comment at document level survived: {0}" -f ($at -match [regex]::Escape("<!-- $MARKER -->")))
    Say ("  comment inside a <game> survived:   {0}" -f $at.Contains("$MARKER inside a game"))
    Say ("  <scrap .../> survived: {0}" -f ($at -match '<scrap\b'))
    Say ("  self-closing form kept: {0}" -f ($at -match '<scrap[^>]*/>'))

    $bd = New-Object System.Xml.XmlDocument; $bd.Load($Before)
    $ad = New-Object System.Xml.XmlDocument; $ad.Load($After)
    Say ("  <game> entries: before {0} after {1}" -f $bd.SelectNodes('/gameList/game').Count, $ad.SelectNodes('/gameList/game').Count)

    foreach ($g in $ad.SelectNodes('/gameList/game')) {
        $path = $g.SelectSingleNode('path').InnerText
        $order = ($g.SelectNodes('*') | ForEach-Object { $_.LocalName }) -join ','
        $attrs = ($g.Attributes | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ' '
        Say "  after: $path"
        Say "    attributes: $(if ($attrs) { $attrs } else { '(none)' })"
        Say "    order: $order"
    }
    foreach ($g in $bd.SelectNodes('/gameList/game')) {
        $path = $g.SelectSingleNode('path').InnerText
        $order = ($g.SelectNodes('*') | ForEach-Object { $_.LocalName }) -join ','
        Say "  before: $path"
        Say "    order: $order"
    }

    if ($b.Length -ne $a.Length -or (Compare-Object $b $a -SyncWindow 0)) {
        Say ''
        Say '  --- the file ES left behind, verbatim'
        foreach ($line in ($at -split "`r?`n")) { Say "  | $line" }
    }
}

# ---------------------------------------------------------------- run

if ($Clean) { Stop-Es; Remove-Corpus; return }

try {
    Say "=== M4 probe 5: roms/$System on $Root ==="
    Stop-Es

    Say ''
    Say '--- Part B: /reloadgames with ES not running'
    for ($i = 1; $i -le 3; $i++) {
        $r = Invoke-Es '/reloadgames' 5
        Say ("  attempt {0}: ok={1} status={2} {3:N0} ms  {4}" -f $i, $r.Ok, $r.Status, $r.Ms, $r.Error)
    }

    Say ''
    Say '--- Part A: what ES does to a gamelist RomMBat wrote'
    $bytes = New-Corpus
    Say ("  wrote {0:N0} bytes, {1} entries" -f $bytes, $games.Count)

    $onDisk = (Get-ChildItem $romDir -File -Filter "*$Extension" -ErrorAction SilentlyContinue).Count
    $startMs = Start-EsAndWait
    Say ("  ES answered /caps after {0} ms" -f $startMs)
    Say ("  {0} rom files on disk, {1} gamelist entries plus one phantom" -f $onDisk, $games.Count)
    Say ("  ES reports {0} games in {1}: the phantom entry is listed: {2}" -f
        (Get-SystemGameCount), $System, ((Get-SystemGameCount) -gt $onDisk))

    $reload = Invoke-Es '/reloadgames' 120
    Say ("  /reloadgames: status {0}, {1:N0} ms" -f $reload.Status, $reload.Ms)
    Start-Sleep -Seconds 3
    Say ("  ES reports {0} games after the reload" -f (Get-SystemGameCount))

    $mtimeBefore = (Get-Item $gamelist).LastWriteTime
    Stop-Es
    $mtimeAfter = (Get-Item $gamelist).LastWriteTime
    Say ("  gamelist mtime: {0} -> {1}, rewritten on exit: {2}" -f $mtimeBefore, $mtimeAfter, ($mtimeAfter -ne $mtimeBefore))
    Say ''
    Compare-Gamelist $written $gamelist

    if ($WithGame) {
        Say ''
        Say '--- Part C: /reloadgames while a game is running'
        [void](Start-EsAndWait)
        $rom = (Get-ChildItem (Join-Path $Root 'roms\ports') -File | Select-Object -First 1)
        if ($rom) {
            $body = ($rom.FullName -replace '\\', '/')
            Say "  launching $body"
            try { Invoke-WebRequest -Uri "$BASE/launch" -Method Post -Body $body -TimeoutSec 30 | Out-Null } catch { Say "  /launch: $($_.Exception.Message)" }
            Start-Sleep -Seconds 20
            $running = Get-Process -Name 'retroarch' -ErrorAction SilentlyContinue
            Say ("  an emulator is running: {0}" -f [bool]$running)

            # A rom added while the game is up. If the reload acted, ES reports it after the
            # call; if it was swallowed like /quit is, the count only moves later.
            $sentinel = Join-Path $romDir "$PREFIX-03 (Europe)$Extension"
            [IO.File]::WriteAllBytes($sentinel, [byte[]]::new(16))
            $before = Get-SystemGameCount
            $r = Invoke-Es '/reloadgames' 120
            Say ("  /reloadgames while playing: status {0}, {1:N0} ms" -f $r.Status, $r.Ms)
            Start-Sleep -Seconds 5
            $after = Get-SystemGameCount
            Say ("  {0} games before, {1} after: the reload acted: {2}" -f $before, $after, ($after -gt $before))
            if ($running) { [void](Invoke-Es '/emukill' 10); Start-Sleep -Seconds 5 }
            $stillRunning = Get-Process -Name 'retroarch' -ErrorAction SilentlyContinue
            Say ("  after /emukill, an emulator is still running: {0}" -f [bool]$stillRunning)
            if ($stillRunning) { $stillRunning | Stop-Process -Force; Start-Sleep -Seconds 3 }
        } else {
            Say '  no rom under roms/ports to launch'
        }

        # Part A found ES leaving the file alone when nothing changed. Playing one of the
        # probe's own entries dirties that system's model, which is the case where ES does
        # rewrite, and the only one where reformatting could show up.
        Say ''
        Say '--- Part D: the rewrite, forced by playing one of the probe entries'
        Remove-Item $sentinel -Force -ErrorAction SilentlyContinue
        [void](Invoke-Es '/reloadgames' 60)
        Start-Sleep -Seconds 3
        $probeRom = (Join-Path $romDir "$($games[0].Stem)$Extension") -replace '\\', '/'
        Say "  launching $probeRom"
        try { Invoke-WebRequest -Uri "$BASE/launch" -Method Post -Body $probeRom -TimeoutSec 30 | Out-Null } catch { Say "  /launch: $($_.Exception.Message)" }
        Start-Sleep -Seconds 25
        Get-Process -Name 'retroarch' -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Seconds 3
        $mtimeBeforeD = (Get-Item $gamelist).LastWriteTime
        Stop-Es
        $mtimeAfterD = (Get-Item $gamelist).LastWriteTime
        Say ("  gamelist mtime: {0} -> {1}, rewritten on exit: {2}" -f $mtimeBeforeD, $mtimeAfterD, ($mtimeAfterD -ne $mtimeBeforeD))
        Say ''
        Compare-Gamelist $written $gamelist
    }
} finally {
    if ($OutFile) {
        New-Item -ItemType Directory -Path (Split-Path $OutFile) -Force | Out-Null
        $log -join "`n" | Set-Content -Path $OutFile -Encoding utf8
        Write-Host "wrote $OutFile"
    }
    Write-Host ''
    Write-Host 'Corpus left in place. Run with -Clean to remove it and restore the gamelist.'
}
