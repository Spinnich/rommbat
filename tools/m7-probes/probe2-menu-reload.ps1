<#
.SYNOPSIS
  M7 stage 7a, probe 2: does a `.menu` entry added while EmulationStation is running appear
  after GET /reloadgames, or does it need a restart?

.DESCRIPTION
  `sync` installs the ES menu entry. What it can honestly tell the user afterwards depends
  entirely on this: "it is there now" or "restart the front end". `es_menu` is an ordinary ES
  system (name `retrobat`, extension `.menu`), so /reloadgames ought to pick it up, and ought
  to is not a measurement.

  Three states are timed, each against ES's own model through /systems and
  /systems/retrobat/games:

    1. baseline, before anything is written
    2. after the `.menu` is written and nothing else, which is the bare-filename case
    3. after the <game> element is merged into system/es_menu/gamelist.xml

  Every write is reverted at the end: the two probe files are deleted and gamelist.xml is
  restored from a byte-for-byte backup taken first.
#>
param(
    [string] $Root = 'K:\RetroBat',
    [int]    $DwellSeconds = 5,
    [int]    $VisibleTimeoutSeconds = 30,
    [string] $OutDir = (Join-Path $PSScriptRoot '..\..\probe-output\m7-probe2')
)

$ErrorActionPreference = 'Stop'
$BASE = 'http://127.0.0.1:1234'
$STEM = 'zzprobe7a'

$esExe = Join-Path $Root 'emulationstation\emulationstation.exe'
$esHome = Join-Path $Root 'emulationstation'
$esArgs = "--fullscreen-borderless --home `"$esHome`""
$menuDir = Join-Path $Root 'system\es_menu'
$menuFile = Join-Path $menuDir "$STEM.menu"
$gamelist = Join-Path $menuDir 'gamelist.xml'
$backup = Join-Path $OutDir 'gamelist.xml.before'

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Get-Es { Get-Process -Name 'emulationstation' -ErrorAction SilentlyContinue | Select-Object -First 1 }

function Invoke-Es([string] $Path, [int] $TimeoutSec = 10) {
    try {
        $r = Invoke-WebRequest -Uri "$BASE$Path" -TimeoutSec $TimeoutSec -ErrorAction Stop
        [pscustomobject]@{ Ok = $true; Status = [int]$r.StatusCode; Body = $r.Content }
    } catch {
        [pscustomobject]@{ Ok = $false; Status = $null; Body = $_.Exception.Message }
    }
}

function Get-RetrobatCount {
    $r = Invoke-Es '/systems' 30
    if (-not $r.Ok) { return -1 }
    $row = ($r.Body | ConvertFrom-Json) | Where-Object { $_.name -eq 'retrobat' } | Select-Object -First 1
    if ($row) { [int]$row.totalGames } else { 0 }
}

function Get-ProbeGame {
    $r = Invoke-Es '/systems/retrobat/games' 30
    if (-not $r.Ok) { return $null }
    ($r.Body | ConvertFrom-Json) | Where-Object { $_.name -match $STEM -or $_.name -eq 'RomMBat probe' } | Select-Object -First 1
}

# Reload, then poll ES's model until the count reaches $Expected. /reloadgames answers in
# 1-2 ms and does the work afterwards, so its response time measures nothing.
function Wait-Visible([scriptblock] $Condition) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    [void](Invoke-Es '/reloadgames' 30)
    $deadline = (Get-Date).AddSeconds($VisibleTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (& $Condition) { $sw.Stop(); return [math]::Round($sw.Elapsed.TotalMilliseconds, 1) }
        Start-Sleep -Milliseconds 50
    }
    $sw.Stop()
    return $null
}

if (Get-Es) { throw 'EmulationStation is already running. Close it first.' }
if (Test-Path $menuFile) { throw "probe file already present: $menuFile" }

Copy-Item $gamelist $backup -Force
$hashBefore = (Get-FileHash -Algorithm MD5 $gamelist).Hash
$results = [ordered]@{}

try {
    Start-Process -FilePath $esExe -ArgumentList $esArgs -WorkingDirectory (Split-Path $esExe) | Out-Null

    $deadline = (Get-Date).AddSeconds(180)
    while ((Get-Date) -lt $deadline -and -not (Invoke-Es '/caps' 5).Ok) { Start-Sleep -Milliseconds 250 }
    if (-not (Invoke-Es '/caps' 5).Ok) { throw 'ES never answered /caps' }
    Start-Sleep -Seconds $DwellSeconds

    $baseline = Get-RetrobatCount
    $results['baselineCount'] = $baseline
    Write-Host "  baseline retrobat totalGames = $baseline"

    # ---- state 2: the .menu alone, no gamelist entry
    Set-Content -Path $menuFile -Value "\rommbat\RomMBat.exe" -NoNewline -Encoding ASCII
    $results['menuOnlyVisibleMs'] = Wait-Visible { (Get-RetrobatCount) -ge ($baseline + 1) }
    $results['menuOnlyCount'] = Get-RetrobatCount
    $bare = Get-ProbeGame
    $results['menuOnlyName'] = if ($bare) { $bare.name } else { $null }
    $results['menuOnlyImage'] = if ($bare) { $bare.image } else { $null }
    Write-Host "  after .menu only: count=$($results['menuOnlyCount']) visibleMs=$($results['menuOnlyVisibleMs']) name=$($results['menuOnlyName'])"

    # ---- state 3: plus the <game> element
    $doc = [xml](Get-Content -Raw -Path $gamelist)
    $game = $doc.CreateElement('game')
    foreach ($pair in @(
            @('path', "./$STEM.menu"),
            @('name', 'RomMBat probe'),
            @('desc', 'Probe entry, deleted at the end of this run.'),
            @('image', './media/dolphin-logo.png'))) {
        $el = $doc.CreateElement($pair[0]); $el.InnerText = $pair[1]; [void]$game.AppendChild($el)
    }
    [void]$doc.DocumentElement.AppendChild($game)
    $doc.Save($gamelist)
    $results['hashAfterProbeWrote'] = (Get-FileHash -Algorithm MD5 $gamelist).Hash
    $results['mtimeAfterProbeWrote'] = (Get-Item $gamelist).LastWriteTimeUtc.ToString('o')
    Copy-Item $gamelist (Join-Path $OutDir 'gamelist.xml.afterProbeWrote') -Force

    $results['withEntryVisibleMs'] = Wait-Visible { $g = Get-ProbeGame; $g -and $g.name -eq 'RomMBat probe' }
    $named = Get-ProbeGame
    $results['withEntryName'] = if ($named) { $named.name } else { $null }
    $results['withEntryImage'] = if ($named) { $named.image } else { $null }
    $results['withEntryCount'] = Get-RetrobatCount
    Write-Host "  after gamelist entry: count=$($results['withEntryCount']) visibleMs=$($results['withEntryVisibleMs']) name=$($results['withEntryName']) image=$($results['withEntryImage'])"

    Write-Host '  LOOK AT THE SCREEN: is "RomMBat probe" in the RetroBat menu, with artwork?' -ForegroundColor Yellow
    Start-Sleep -Seconds 12
} finally {
    $p = Get-Es
    if ($p) {
        [void](Invoke-Es '/quit' 10)
        $deadline = (Get-Date).AddSeconds(60)
        while ((Get-Date) -lt $deadline -and -not $p.HasExited) { Start-Sleep -Milliseconds 200; $p.Refresh() }
        if (-not $p.HasExited) { $p.Kill() }
        Start-Sleep -Seconds 2
    }

    # Did ES rewrite the gamelist on the way out? Probe 7 said it did not across two sessions,
    # and that is an absence of evidence rather than a guarantee, so it is recorded here too.
    Copy-Item $gamelist (Join-Path $OutDir 'gamelist.xml.afterEsQuit') -Force
    $results['gamelistHashAfterEsQuit'] = (Get-FileHash -Algorithm MD5 $gamelist).Hash
    $results['mtimeAfterEsQuit'] = (Get-Item $gamelist).LastWriteTimeUtc.ToString('o')
    $results['gamelistHashBefore'] = $hashBefore
    # The only comparison that answers "did ES rewrite it": our own bytes against the ones
    # left behind. Against the shipped file it is our own write that shows up.
    $results['esRewroteIt'] = ($results['hashAfterProbeWrote'] -and
        $results['gamelistHashAfterEsQuit'] -ne $results['hashAfterProbeWrote'])

    Remove-Item $menuFile -Force -ErrorAction SilentlyContinue
    Copy-Item $backup $gamelist -Force
    $results['restoredHash'] = (Get-FileHash -Algorithm MD5 $gamelist).Hash
    $results['restoredClean'] = ($results['restoredHash'] -eq $hashBefore)

    [pscustomobject]$results | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $OutDir 'summary.json')
    [pscustomobject]$results | Format-List | Out-String | Write-Host
    Write-Host "artifacts in $OutDir"
}
