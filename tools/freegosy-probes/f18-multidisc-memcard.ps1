<#
.SYNOPSIS
  F18: what DuckStation names a memory card, across the layouts a multi-disc set really
  takes on disk.

.DESCRIPTION
  M6 wants class D to collapse into class A by naming the memory card after the rom file,
  which is what duckstation_memcardtype=PerGameFileTitle promises. Multi-disc breaks the
  assumption underneath that, because "the rom file" is no longer one thing.

  A real psx folder holds at least three layouts at once, and they are not equivalent:

    single      Spyro the Dragon (USA).chd
    loose set   Final Fantasy VII (USA) (Disc 1|2|3).chd   three entries, no playlist
    foldered    Metal Gear Solid (USA) (Rev 1)\ containing two .chd and one .m3u

  RetroBat's wiki documents a fourth: the .m3u flat in roms/psx beside the discs. Each is
  a different string for PerGameFileTitle to key on, and a set launched by disc 1 and the
  same set launched by its playlist may not share a card at all.

  -Rom takes a path relative to roms/<system>, so a foldered set is addressed as
  "Game\Game.m3u". -MemcardType writes a per-game es_settings.cfg override for the run and
  removes it afterwards, so the stock configuration is left as it was found.

  The probe never presses a save key. A PS1 launch writes its memory card unprompted, which
  M0 already measured for PS2, so the card appears on its own.

.EXAMPLE
  pwsh -File tools/freegosy-probes/f18-multidisc-memcard.ps1 -Root K:\RetroBat -Rom "Spyro the Dragon (USA).chd"
  pwsh -File tools/freegosy-probes/f18-multidisc-memcard.ps1 -Root K:\RetroBat -Rom "Metal Gear Solid (USA) (Rev 1)\Metal Gear Solid (USA) (Rev 1).m3u" -MemcardType PerGameFileTitle
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
    if ($proc) { Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }
    Get-Process -Name 'duckstation*' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if (Test-Path $esBackup) { Move-Item $esBackup $esCfg -Force }
    if (-not $KeepArtifacts -and $created) {
        Write-Host ''
        Write-Host "  removing $($created.Count) file(s) this run created" -ForegroundColor DarkGray
        foreach ($f in $created) { Remove-Item -LiteralPath $f -Force -ErrorAction SilentlyContinue }
    }
}
