<#
.SYNOPSIS
  F19 and F20: what a battery-backed cart writes when a game is launched and closed without
  the player ever saving.

.DESCRIPTION
  Two questions share one run.

  F19 asks where the save lands and what shape it is, for systems data/retrobat/save_shapes.json
  still lists unclassified. mastersystem is the one that matters most, because it is a wave 1
  platform in the rollout order and its shape is still a guess.

  F20 asks whether a launch alone can produce a save file. M0 already measured that launching
  a PS2 game rewrites both shared memory cards with no in-game save, which is why class D
  needs content hashing. The same question for a class A battery cart decides whether an
  upload needs a floor: if launch-and-quit writes a blank .srm, a naive sync would push that
  blank over a good server save.

  The probe never presses a save key. It snapshots saves/<system>, launches the rom through
  emulatorLauncher, waits, closes the emulator, and diffs. Anything that appears did so
  because the emulator wrote it on its own.

  A file that appears is also inspected: a battery image that is entirely 0x00 or 0xFF is
  an empty SRAM buffer flushed on close, not a real save, and that distinction is the whole
  of F20.

  Only files that appear during the run are removed afterwards, tracked by exact path, so an
  install's real saves are never touched. Pass -KeepArtifacts to leave them in place.

.EXAMPLE
  pwsh -File tools/freegosy-probes/f19-f20-battery-on-close.ps1 -Root K:\RetroBat -System mastersystem -Rom "Phantasy Star (Brazil).zip" -Core genesis_plus_gx
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [Parameter(Mandatory)] [string] $System,
    [Parameter(Mandatory)] [string] $Rom,
    [string] $Emulator = 'libretro',
    [string] $Core = 'genesis_plus_gx',
    [int] $BootSeconds = 45,
    [int] $SettleSeconds = 8,
    [switch] $KeepArtifacts
)

$ErrorActionPreference = 'Stop'

$launcher = Join-Path $Root 'emulationstation\emulatorLauncher.exe'
$launcherDir = Split-Path $launcher
$savesRoot = Join-Path $Root "saves\$System"
$romPath = Join-Path $Root "roms\$System\$Rom"
$stem = [System.IO.Path]::GetFileNameWithoutExtension($Rom)

foreach ($p in @($launcher, $romPath)) { if (-not (Test-Path $p)) { throw "not found: $p" } }

# Refuse a rom that already owns save data, for the same reason probe2 does: launching one
# rewrites its battery save on exit and this probe must not touch real saves.
# StartsWith rather than -like, because rom names routinely contain [ ] tags.
$existing = @(Get-ChildItem $savesRoot -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name.StartsWith($stem, [StringComparison]::OrdinalIgnoreCase) })
if ($existing) {
    throw "$Rom already owns $($existing.Count) file(s) under saves\$System. Pick a rom with none."
}

Write-Host "=== $System / $Emulator" -NoNewline -ForegroundColor Cyan
if ($Core) { Write-Host " / $Core" -NoNewline -ForegroundColor Cyan }
Write-Host " / $Rom ===" -ForegroundColor Cyan
Write-Host '  no save key is ever sent; anything that appears was written by the emulator itself'

function Get-Tree {
    $map = @{}
    if (Test-Path $savesRoot) {
        Get-ChildItem $savesRoot -Recurse -File -Force -ErrorAction SilentlyContinue | ForEach-Object {
            $map[$_.FullName] = "$($_.Length)|$($_.LastWriteTimeUtc.Ticks)"
        }
    }
    $map
}

function Compare-Tree($Before, $After) {
    $rows = @()
    foreach ($k in $After.Keys) {
        $state = if (-not $Before.ContainsKey($k)) { 'new' } elseif ($Before[$k] -ne $After[$k]) { 'changed' } else { $null }
        if ($state) {
            $fi = Get-Item -LiteralPath $k
            $rows += [pscustomobject]@{
                State    = $state
                Relative = $k.Substring($savesRoot.Length).TrimStart('\')
                Bytes    = $fi.Length
                Modified = $fi.LastWriteTime.ToString('HH:mm:ss.fff')
                Full     = $k
            }
        }
    }
    $rows | Sort-Object Modified
}

# The F20 question in one function: is this file an empty buffer flushed on close, or
# something a player actually produced?
function Get-Fill([string] $Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -eq 0) { return 'zero length' }
    $first = $bytes[0]
    $uniform = $true
    foreach ($b in $bytes) { if ($b -ne $first) { $uniform = $false; break } }
    if ($uniform) { return ('uniform 0x{0:X2}, so a blank buffer' -f $first) }
    $distinct = ($bytes | Select-Object -Unique).Count
    "mixed, $distinct distinct byte values"
}

Add-Type @"
using System; using System.Runtime.InteropServices;
public class F19Close {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint type);
}
"@

$script:CleanExit = $false
$before = Get-Tree
Write-Host "  baseline: $($before.Count) file(s) under saves/$System"

$proc = $null
$created = @()
try {
    # -core is mandatory, not optional: M0 measured BizHawk crashing in RetroBat's controller
    # generator on an unguarded inputPortNb[core] lookup when it is absent, so anything
    # RomMBat launches passes one.
    $argList = @('-system', $System, '-emulator', $Emulator, '-rom', "`"$romPath`"")
    if ($Core) { $argList += @('-core', $Core) }
    Write-Host "  launching: emulatorLauncher $($argList -join ' ')"
    $proc = Start-Process -FilePath $launcher -WorkingDirectory $launcherDir -ArgumentList $argList -PassThru

    Write-Host "  waiting ${BootSeconds}s for the emulator to boot and settle"
    Start-Sleep -Seconds $BootSeconds

    $during = Get-Tree
    $liveRows = Compare-Tree $before $during
    Write-Host '  -- while the emulator is still running' -ForegroundColor DarkGray
    if (-not $liveRows) { Write-Host '     (nothing)' -ForegroundColor DarkGray }
    $liveRows | ForEach-Object { Write-Host ("     {0,-7} {1,-52} {2,9} B  {3}" -f $_.State, $_.Relative, $_.Bytes, $_.Modified) }

    # **How the emulator is closed decides whether this probe means anything.** A forced
    # kill skips whatever the emulator does on exit, which is exactly the behaviour under
    # test, so the quit hotkey comes first and a kill is a last resort that invalidates the
    # F20 half. RetroArch is SDL-based, so the keystroke has to be keybd_event with a real
    # hardware scan code: SendKeys posts window messages that SDL never reads.
    Write-Host '  closing the emulator with the quit hotkey (Escape)'
    $ra = Get-Process -Name 'retroarch' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($ra) { [F19Close]::SetForegroundWindow($ra.MainWindowHandle) | Out-Null; Start-Sleep -Milliseconds 400 }
    $esc = 0x1B
    $scan = [byte][F19Close]::MapVirtualKey([uint32]$esc, 0)
    [F19Close]::keybd_event([byte]$esc, $scan, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [F19Close]::keybd_event([byte]$esc, $scan, 2, [UIntPtr]::Zero)

    $deadline = (Get-Date).AddSeconds($SettleSeconds + 15)
    while ((Get-Date) -lt $deadline -and (Get-Process -Name 'retroarch' -ErrorAction SilentlyContinue)) {
        Start-Sleep -Milliseconds 500
    }
    $stillUp = [bool](Get-Process -Name 'retroarch' -ErrorAction SilentlyContinue)
    $script:CleanExit = -not $stillUp
    if ($stillUp) {
        Write-Host '  hotkey did not close it; forcing. The F20 result is void for this run.' -ForegroundColor Yellow
        Get-Process -Name 'retroarch' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    }
    else {
        Write-Host '  exited on its own, so any save-on-exit path ran' -ForegroundColor Green
    }
    Start-Sleep -Seconds $SettleSeconds

    $after = Get-Tree
    $exitRows = Compare-Tree $before $after
    Write-Host '  -- after the emulator exits' -ForegroundColor DarkGray
    if (-not $exitRows) { Write-Host '     (nothing)' -ForegroundColor DarkGray }
    $exitRows | ForEach-Object {
        Write-Host ("     {0,-7} {1,-52} {2,9} B  {3}" -f $_.State, $_.Relative, $_.Bytes, $_.Modified)
    }

    # RetroArch names the path it would use even when it writes nothing, so the shape
    # answer survives a run that produces no file at all.
    $stdout = Join-Path $Root 'emulationstation\.emulationstation\es_launch_stdout.log'
    if (Test-Path $stdout) {
        $redirect = Select-String -Path $stdout -Pattern 'Redirecting save file to "([^"]+)"' -ErrorAction SilentlyContinue |
            Select-Object -Last 1
        if ($redirect) {
            $declared = $redirect.Matches[0].Groups[1].Value
            Write-Host ''
            Write-Host '  == the path the emulator itself declared' -ForegroundColor Cyan
            Write-Host ("     {0}" -f $declared.Substring($Root.TrimEnd('\').Length).TrimStart('\'))
        }
        $sram = Select-String -Path $stdout -Pattern '\[SRAM\].*' -ErrorAction SilentlyContinue
        $sram | ForEach-Object { Write-Host ("     {0}" -f $_.Matches[0].Value) }
    }

    Write-Host ''
    Write-Host '  == verdict' -ForegroundColor Cyan
    Write-Host ("     exit was clean: {0}" -f $script:CleanExit)
    if (-not $script:CleanExit) {
        Write-Host '     F20 is void for this run: a forced kill skips the save-on-exit path' -ForegroundColor Yellow
    }
    if (-not $exitRows) {
        Write-Host '     no save file was written by launch and close alone'
    }
    foreach ($row in $exitRows) {
        if ($row.State -eq 'new') { $created += $row.Full }
        $fill = Get-Fill $row.Full
        $depth = ($row.Relative -split '\\').Count
        Write-Host ("     {0}" -f $row.Relative)
        Write-Host ("       {0} bytes, {1}" -f $row.Bytes, $fill)
        Write-Host ("       path depth under saves/{0}: {1}" -f $System, $depth)
        $matchesStem = $row.Relative -like "$stem*"
        Write-Host ("       named after the rom: {0}" -f $matchesStem)
    }
}
finally {
    if ($proc) { Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }
    Get-Process -Name 'retroarch' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if (-not $KeepArtifacts -and $created) {
        Write-Host ''
        Write-Host "  removing $($created.Count) file(s) this run created" -ForegroundColor DarkGray
        foreach ($f in $created) { Remove-Item -LiteralPath $f -Force -ErrorAction SilentlyContinue }
    }
}
