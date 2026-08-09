<#
.SYNOPSIS
  M0 probe 2: which PPSSPP save-state directory is authoritative, and in which direction
  RetroBat moves states between them.

.DESCRIPTION
  A live install has the same PSP state present under two different naming schemes at once:

    saves/psp/ppsspp/<rom filename>_<slot>.ppst    matches es_savestates.cfg
    saves/psp/PPSSPP_STATE/<GAMEID>_<ver>_<slot>.ppst   PPSSPP's own memstick layout

  The static evidence cannot say which one RomMBat should read and write, so this drives a
  real launch and watches both directories.

  Part 1, sync-out. Launch a game that has no state, press F2 (PPSSPP's Save State, keycode
  132 in RetroBat's controls.ini), and watch both directories while the emulator is still
  running. This shows whether the copy is made live or only at exit, and which side is the
  original.

  Part 2, sync-in. Delete the native copy, then relaunch passing `-state_slot` and
  `-state_file` exactly as EmulationStation does, with `-state_file` naming the ES-facing
  path. This is the case RomMBat creates when it downloads a state from RomM: the question
  is whether a state placed only in the ES-facing directory reaches the emulator.

  Pick a rom with no existing save state. The probe writes only files belonging to that rom
  and removes them again, so an install's real saves are never touched. Nothing is restored
  from a backup because nothing existing is modified.

.EXAMPLE
  pwsh -File tools/m0-probes/probe2-psp-states.ps1 -Root E:\RetroBat -Rom "Patapon (Europe) (En,Fr,De,Es,It).cso"
  pwsh -File tools/m0-probes/probe2-psp-states.ps1 -Root E:\RetroBat -Rom "..." -KeepArtifacts
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [Parameter(Mandatory)] [string] $Rom,
    [int] $BootSeconds = 35,
    [switch] $KeepArtifacts
)

$ErrorActionPreference = 'Stop'

$launcher = Join-Path $Root 'emulationstation\emulatorLauncher.exe'
$launcherDir = Split-Path $launcher
$log = Join-Path $Root 'emulationstation\emulatorLauncher.log'
$esDir = Join-Path $Root 'saves\psp\ppsspp'
$natDir = Join-Path $Root 'saves\psp\PPSSPP_STATE'
$romPath = Join-Path $Root "roms\psp\$Rom"
$stem = [System.IO.Path]::GetFileNameWithoutExtension($Rom)

foreach ($p in @($launcher, $romPath)) {
    if (-not (Test-Path $p)) { throw "not found: $p" }
}
New-Item -ItemType Directory -Force -Path $esDir, $natDir | Out-Null

if (Get-ChildItem $esDir -File | Where-Object { $_.Name -like "$stem*" }) {
    throw "$Rom already has a state in $esDir. Pick a rom with none, so the probe only ever writes its own files."
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public class Probe2Win {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
}
"@

# Only ever reports files belonging to the rom under test, so an install's real saves stay
# out of the output as well as out of the way.
function Get-Snapshot {
    $rows = @()
    foreach ($pair in @(@{ D = $esDir; N = 'ppsspp' }, @{ D = $natDir; N = 'PPSSPP_STATE' })) {
        Get-ChildItem $pair.D -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "$stem*" -or $_.Name -like "$($script:gameId)*" } |
            ForEach-Object {
                $rows += [pscustomobject]@{
                    Dir      = $pair.N
                    Name     = $_.Name
                    Bytes    = $_.Length
                    Modified = $_.LastWriteTime.ToString('HH:mm:ss.fff')
                }
            }
    }
    $rows
}

function Show-Snapshot([string] $Label) {
    Write-Host "  -- $Label" -ForegroundColor DarkGray
    $rows = Get-Snapshot
    if (-not $rows) { Write-Host '     (nothing)' -ForegroundColor DarkGray; return }
    $rows | ForEach-Object { Write-Host ("     {0,-13} {1,-46} {2,10} B  {3}" -f $_.Dir, $_.Name, $_.Bytes, $_.Modified) }
}

function Start-Game([string[]] $Extra) {
    # The rom path must arrive quoted or a name with spaces is split into several arguments
    # and the launcher reports "rom does not exist".
    $argList = @('-system', 'psp', '-emulator', 'ppsspp', '-rom', "`"$romPath`"") + $Extra
    Start-Process -FilePath $launcher -WorkingDirectory $launcherDir -ArgumentList $argList

    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline) {
        $p = Get-Process | Where-Object { $_.Name -like '*PPSSPP*' } | Select-Object -First 1
        if ($p) { return $p }
        Start-Sleep -Milliseconds 500
    }
    Get-Content $log -Tail 5 | ForEach-Object { Write-Host "     $($_.Trim())" -ForegroundColor Red }
    throw 'PPSSPP never started'
}

function Stop-Game($Process) {
    if (-not $Process -or $Process.HasExited) { return }
    [void]$Process.CloseMainWindow()
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline -and -not $Process.HasExited) { Start-Sleep -Milliseconds 500; $Process.Refresh() }
    if (-not $Process.HasExited) { $Process.Kill() }
    Start-Sleep -Seconds 3
}

$script:gameId = '@@none@@'
$findings = @()

try {
    Write-Host '=== part 1: where does a new state land, and when ===' -ForegroundColor Cyan
    Show-Snapshot 'before launch'

    $proc = Start-Game @()
    Start-Sleep -Seconds $BootSeconds

    # The window title carries the game id, which is the native naming key.
    $title = (Get-Process -Id $proc.Id).MainWindowTitle
    if ($title -match '([A-Z]{4}\d{5})') { $script:gameId = $Matches[1] }
    Write-Host "  window: [$title]  game id: $($script:gameId)"
    Show-Snapshot "after boot, before F2"

    [void][Probe2Win]::ShowWindow($proc.MainWindowHandle, 9)
    [void][Probe2Win]::SetForegroundWindow($proc.MainWindowHandle)
    Start-Sleep -Milliseconds 800
    Write-Host "  sending F2 at $((Get-Date).ToString('HH:mm:ss.fff'))"
    [System.Windows.Forms.SendKeys]::SendWait('{F2}')

    Start-Sleep -Seconds 3
    Show-Snapshot 'F2 +3s, emulator still running'
    $live = Get-Snapshot
    Start-Sleep -Seconds 10
    Show-Snapshot 'F2 +13s, emulator still running'

    Stop-Game $proc
    Show-Snapshot 'after exit'
    $afterExit = Get-Snapshot

    $syncedLive = [bool]($live | Where-Object { $_.Dir -eq 'ppsspp' -and $_.Name -like '*.ppst' })
    $changedAtExit = (($live | ConvertTo-Json -Compress) -ne ($afterExit | ConvertTo-Json -Compress))
    $findings += "sync-out happens while the emulator runs : $syncedLive"
    $findings += "anything changed at exit                 : $changedAtExit"

    $esState = Join-Path $esDir "$($stem)_0.ppst"
    $natState = Join-Path $natDir "$($script:gameId)_1.00_0.ppst"
    if ((Test-Path $esState) -and (Test-Path $natState)) {
        $identical = (Get-FileHash $esState).Hash -eq (Get-FileHash $natState).Hash
        $findings += "the two .ppst copies are byte-identical  : $identical"
    }
    $txt = Join-Path $esDir "$stem.txt"
    if (Test-Path $txt) { $findings += "the .txt sidecar holds                   : $(Get-Content $txt)" }

    Write-Host ''
    Write-Host '=== part 2: does a state placed only in the ES-facing directory reach the emulator ===' -ForegroundColor Cyan

    if (-not (Test-Path $esState)) { throw 'no ES-facing state was produced, cannot run part 2' }
    $esHash = (Get-FileHash $esState).Hash
    Get-ChildItem $natDir -File | Where-Object { $_.Name -like "$($script:gameId)*" } | Remove-Item -Force
    Write-Host "  deleted every native file for $($script:gameId)"

    $proc = Start-Game @('-state_slot', '0', '-state_file', "`"$esState`"")
    Start-Sleep -Seconds 12
    Show-Snapshot 'after relaunch with -state_file'

    $recreated = Test-Path $natState
    $findings += "native copy recreated from the ES-facing one: $recreated"
    if ($recreated) {
        $findings += "  and it matches byte for byte            : $((Get-FileHash $natState).Hash -eq $esHash)"
    }
    $invocation = (Select-String -Path $log -Pattern '\[Running\].*PPSSPP' | Select-Object -Last 1).Line
    if ($invocation -match '(--state=.*)$') { $findings += "the emulator was handed              : $($Matches[1])" }

    Stop-Game $proc
} finally {
    $proc = Get-Process | Where-Object { $_.Name -like '*PPSSPP*' } | Select-Object -First 1
    if ($proc) { Stop-Game $proc }

    if (-not $KeepArtifacts) {
        $removed = 0
        foreach ($d in @($esDir, $natDir, (Join-Path $Root 'saves\psp\Cheats'), (Join-Path $Root 'saves\psp\SYSTEM\CACHE'))) {
            Get-ChildItem $d -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -like "$stem*" -or ($script:gameId -ne '@@none@@' -and $_.Name -like "$($script:gameId)*") } |
                ForEach-Object { Remove-Item $_.FullName -Force; $removed++ }
        }
        Write-Host ''
        Write-Host "removed $removed file(s) created by this probe" -ForegroundColor DarkGray
    }
}

Write-Host ''
Write-Host '=== findings ===' -ForegroundColor Cyan
$findings | ForEach-Object { Write-Host "  $_" }
