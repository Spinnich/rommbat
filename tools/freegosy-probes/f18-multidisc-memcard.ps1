<#
.SYNOPSIS
  F18: what DuckStation names a memory card, across the layouts a multi-disc set really
  takes on disk.

.DESCRIPTION
  M6 once wanted class D to collapse into class A by naming the memory card after the rom
  file, which is what duckstation_memcardtype=PerGameFileTitle promises. This probe was
  written to check that against a multi-disc set, where "the rom file" is no longer one thing,
  and it ended up withdrawing the recommendation: the stock PerGameTitle names the card from
  gamedb.yaml's saveName with the disc marker removed, so it binds the set, and keying by
  filename is what splits it.

  A real psx folder holds at least three layouts at once, and they are not equivalent:

    single      Spyro the Dragon (USA).chd
    loose set   Final Fantasy VII (USA) (Disc 1|2|3).chd   three entries, no playlist
    foldered    Metal Gear Solid (USA) (Rev 1)\ containing two .chd and one .m3u

  RetroBat's wiki documents a fourth: the .m3u flat in roms/psx beside the discs. Both the
  foldered and the loose layout were driven and both produced a single card for the set, so
  the playlist is not what binds it under DuckStation. Run this against the remaining layouts,
  or with -MemcardType, to see a naming rule change under you.

  -Rom takes a path relative to roms/<system>, so a foldered set is addressed as
  "Game\Game.m3u". -MemcardType writes a per-game es_settings.cfg override for the run and
  removes it afterwards, so the stock configuration is left as it was found.

  The probe never presses a save key, and on DuckStation that is not enough on its own. A card
  is created when the game first touches it, which is not a fixed point in the run: Metal Gear
  Solid produced both slot cards 23 seconds into a launch, while Spyro ran 59 seconds and left
  the directory empty. So a timed unattended launch may measure nothing, and -Interactive,
  which hands the emulator to a person and waits for them to quit, is the mode that answers the
  question for certain.

  What the answer turned out to be, under the stock configuration and launched through the
  .m3u: one card pair for the whole set, "Metal Gear Solid (USA)_1.mcd" and "_2.mcd", where the
  suffix is the console slot and _2 is an empty formatted card. The stem is gamedb.yaml's
  saveName with the disc marker removed. Still unmeasured: the same set with its discs loose
  and no playlist.

.EXAMPLE
  pwsh -File tools/freegosy-probes/f18-multidisc-memcard.ps1 -Root <retrobat-root> -Rom "Spyro the Dragon (USA).chd"
  pwsh -File tools/freegosy-probes/f18-multidisc-memcard.ps1 -Root <retrobat-root> -Rom "Metal Gear Solid (USA) (Rev 1)\Metal Gear Solid (USA) (Rev 1).m3u" -Interactive
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [Parameter(Mandatory)] [string] $Rom,
    [string] $System = 'psx',
    [string] $Emulator = 'duckstation',
    [ValidateSet('', 'PerGameTitle', 'PerGameFileTitle', 'PerGame', 'Shared')]
    [string] $MemcardType = '',
    [int] $BootSeconds = 60,
    [int] $SettleSeconds = 8,
    # Hand the emulator to a person instead of a timer: launch, then wait for them to play,
    # save in-game and quit. DuckStation writes a memory card only when the game asks it to,
    # unlike PCSX2, so an unattended run measures nothing.
    [switch] $Interactive,
    [int] $InteractiveTimeoutMinutes = 30,
    [switch] $KeepArtifacts
)

$ErrorActionPreference = 'Stop'

$launcher = Join-Path $Root 'emulationstation\emulatorLauncher.exe'
$launcherDir = Split-Path $launcher
$savesRoot = Join-Path $Root "saves\$System"
$romPath = Join-Path $Root "roms\$System\$Rom"
$romLeaf = Split-Path $Rom -Leaf
$esCfg = Join-Path $Root 'emulationstation\.emulationstation\es_settings.cfg'

foreach ($p in @($launcher, $romPath)) { if (-not (Test-Path $p)) { throw "not found: $p" } }

Write-Host "=== $System / $Emulator / $Rom ===" -ForegroundColor Cyan
if ($MemcardType) {
    # The per-game form, which M0 measured as honoured and outranking the system key. The
    # key must carry the rom extension or it is ignored silently.
    Write-Host "  per-game override: $System[`"$romLeaf`"].duckstation_memcardtype = $MemcardType"
}
else {
    Write-Host '  memory card type left at its stock default (PerGameTitle)'
}

Add-Type @"
using System; using System.Runtime.InteropServices;
public class F18Close {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint type);
}
"@

function Get-Tree {
    $map = @{}
    if (Test-Path $savesRoot) {
        Get-ChildItem $savesRoot -Recurse -File -Force -ErrorAction SilentlyContinue | ForEach-Object {
            $map[$_.FullName] = "$($_.Length)|$($_.LastWriteTimeUtc.Ticks)"
        }
    }
    $map
}

$esBackup = "$esCfg.f18-backup"
$created = @()
$proc = $null
$cleanExit = $false

try {
    if ($MemcardType) {
        Copy-Item $esCfg $esBackup -Force
        $key = "$System[`"$romLeaf`"].duckstation_memcardtype"
        $lines = @(Get-Content $esCfg | Where-Object { $_ -notmatch [regex]::Escape($key) })
        $lines += "<string name=`"$key`" value=`"$MemcardType`" />"
        Set-Content -Path $esCfg -Value $lines -Encoding UTF8
    }

    $before = Get-Tree
    Write-Host "  baseline: $($before.Count) file(s) under saves/$System"

    $argList = @('-system', $System, '-emulator', $Emulator, '-rom', "`"$romPath`"")
    Write-Host "  launching: emulatorLauncher $($argList -join ' ')"
    $proc = Start-Process -FilePath $launcher -WorkingDirectory $launcherDir -ArgumentList $argList -PassThru

    if ($Interactive) {
        Write-Host ''
        Write-Host '  INTERACTIVE. Play the game, save in-game, then quit the emulator.' -ForegroundColor Yellow
        Write-Host '  This script is waiting for DuckStation to exit and will not close it.' -ForegroundColor Yellow
        Write-Host ''
        $limit = (Get-Date).AddMinutes($InteractiveTimeoutMinutes)
        $seen = $false
        while ((Get-Date) -lt $limit) {
            $live = Get-Process -Name 'duckstation*' -ErrorAction SilentlyContinue
            if ($live) { $seen = $true }
            elseif ($seen) { break }
            Start-Sleep -Seconds 2
        }
        $cleanExit = $seen -and -not (Get-Process -Name 'duckstation*' -ErrorAction SilentlyContinue)
        if (-not $seen) { Write-Host '  never saw a duckstation process; did it launch?' -ForegroundColor Yellow }
        Write-Host '  emulator has exited'
        Start-Sleep -Seconds $SettleSeconds
    }
    else {

    Write-Host "  waiting ${BootSeconds}s"
    Start-Sleep -Seconds $BootSeconds

    Write-Host '  closing with Escape'
    $ds = Get-Process -Name 'duckstation*' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($ds -and $ds.MainWindowHandle -ne 0) { [F18Close]::SetForegroundWindow($ds.MainWindowHandle) | Out-Null; Start-Sleep -Milliseconds 400 }
    $esc = 0x1B
    $scan = [byte][F18Close]::MapVirtualKey([uint32]$esc, 0)
    [F18Close]::keybd_event([byte]$esc, $scan, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [F18Close]::keybd_event([byte]$esc, $scan, 2, [UIntPtr]::Zero)

    $deadline = (Get-Date).AddSeconds($SettleSeconds + 20)
    while ((Get-Date) -lt $deadline -and (Get-Process -Name 'duckstation*' -ErrorAction SilentlyContinue)) {
        Start-Sleep -Milliseconds 500
    }
    $cleanExit = -not (Get-Process -Name 'duckstation*' -ErrorAction SilentlyContinue)
    if (-not $cleanExit) {
        Write-Host '  hotkey did not close it; forcing' -ForegroundColor Yellow
        Get-Process -Name 'duckstation*' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds $SettleSeconds
    }

    $after = Get-Tree
    Write-Host ''
    Write-Host '  == what appeared under saves/' -NoNewline -ForegroundColor Cyan
    Write-Host "$System" -ForegroundColor Cyan
    $any = $false
    foreach ($k in $after.Keys) {
        $state = if (-not $before.ContainsKey($k)) { 'new' } elseif ($before[$k] -ne $after[$k]) { 'changed' } else { $null }
        if (-not $state) { continue }
        $any = $true
        if ($state -eq 'new') { $created += $k }
        $fi = Get-Item -LiteralPath $k
        $rel = $k.Substring($savesRoot.Length).TrimStart('\')
        Write-Host ("     {0,-7} {1,-56} {2,9} B" -f $state, $rel, $fi.Length)
        $cardStem = [IO.Path]::GetFileNameWithoutExtension($fi.Name)
        $romStem = [IO.Path]::GetFileNameWithoutExtension($romLeaf)
        Write-Host ("       card stem : {0}" -f $cardStem)
        Write-Host ("       rom stem  : {0}" -f $romStem)
        Write-Host ("       identical : {0}" -f ($cardStem -eq $romStem))
    }
    if (-not $any) { Write-Host '     (nothing)' }

    Write-Host ''
    Write-Host ("  == exit was clean: {0}" -f $cleanExit) -ForegroundColor Cyan

    # What the launcher actually resolved, which is the only place the disc set is visible.
    $log = Join-Path $Root 'emulationstation\emulatorLauncher.log'
    if (Test-Path $log) {
        Write-Host '  == emulatorLauncher, last run' -ForegroundColor Cyan
        Get-Content $log -Tail 40 |
            Where-Object { $_ -match 'm3u|disc|Disc|playlist|memcard|MemoryCard|Running|Generator\] Using' } |
            ForEach-Object { Write-Host ("     {0}" -f ($_ -replace [regex]::Escape($Root), '<root>')) }
    }
}
finally {
    # In interactive mode the person owns the emulator's lifetime, so never kill it here.
    if (-not $Interactive) {
        if ($proc) { Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }
        Get-Process -Name 'duckstation*' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $esBackup) { Move-Item $esBackup $esCfg -Force }
    if (-not $KeepArtifacts -and $created) {
        Write-Host ''
        Write-Host "  removing $($created.Count) file(s) this run created" -ForegroundColor DarkGray
        foreach ($f in $created) { Remove-Item -LiteralPath $f -Force -ErrorAction SilentlyContinue }
    }
}
