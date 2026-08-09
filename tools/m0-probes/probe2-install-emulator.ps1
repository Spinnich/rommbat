<#
.SYNOPSIS
  Bring an emulator that RetroBat has not downloaded yet onto disk, by answering the install
  prompt that otherwise blocks the launch forever.

.DESCRIPTION
  RetroBat downloads emulators on demand, so es_savestates.cfg can declare an emulator that
  has no executable. Launching one puts "[Startup] Emulator update found : proposing to
  update." in emulatorLauncher.log and raises a RetroBat-styled dialog reading

      The emulator '<name>' is not installed.  Install now ?   [Yes] [No]

  and there the launch stops. The dialog carries **no window title and no class name worth
  matching**, and it never times out: three emulatorLauncher processes were found still
  sitting on it seven hours later. Nothing in the log says a prompt is waiting, so a script
  that watches the log alone hangs with no clue why.

  This finds the one visible top-level window belonging to the new emulatorLauncher process,
  focuses it, and presses Enter, which takes the default (Yes). It then waits for the
  emulator directory to stop growing, and stops the emulator once it launches, so the only
  side effect is the install itself.

  Sends Enter through keybd_event with a real scan code rather than SendKeys, for the same
  reason probe2-savestates.ps1 does: the window does not process posted messages.

.EXAMPLE
  pwsh -File tools/m0-probes/probe2-install-emulator.ps1 -Root E:\RetroBat -System nds -Emulator desmume
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [Parameter(Mandatory)] [string] $System,
    [Parameter(Mandatory)] [string] $Emulator,
    [string] $Rom,
    [int] $PromptTimeoutSeconds = 90,
    [int] $InstallTimeoutSeconds = 900
)

$ErrorActionPreference = 'Stop'

$launcher = Join-Path $Root 'emulationstation\emulatorLauncher.exe'
$log = Join-Path $Root 'emulationstation\emulatorLauncher.log'
$emuDir = Join-Path $Root "emulators\$Emulator"

if (-not $Rom) {
    $romDir = Join-Path $Root "roms\$System"
    $Rom = (Get-ChildItem $romDir -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ne 'gamelist.xml' } | Sort-Object Length | Select-Object -First 1).Name
    if (-not $Rom) { throw "no rom found under roms\$System" }
}
$romPath = Join-Path $Root "roms\$System\$Rom"
if (-not (Test-Path $romPath)) { throw "not found: $romPath" }

Add-Type @"
using System; using System.Runtime.InteropServices;
public class Probe2Install {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint type);
}
"@

# The callback runs in its own scope, so the handle has to come back through a script-scoped
# variable. Assigning to a local inside the delegate looks like it works and always returns
# zero.
$script:PromptWindow = [IntPtr]::Zero
function Get-VisibleWindow([int] $ProcessId) {
    $script:PromptWindow = [IntPtr]::Zero
    $cb = [Probe2Install+EnumProc] {
        param($h, $l)
        $owner = 0
        [void][Probe2Install]::GetWindowThreadProcessId($h, [ref]$owner)
        if ($owner -eq $ProcessId -and [Probe2Install]::IsWindowVisible($h)) {
            $script:PromptWindow = $h
            return $false
        }
        return $true
    }
    [void][Probe2Install]::EnumWindows($cb, [IntPtr]::Zero)
    $script:PromptWindow
}

function Get-DirBytes([string] $Path) {
    if (-not (Test-Path $Path)) { return 0 }
    ((Get-ChildItem $Path -Recurse -File -Force -ErrorAction SilentlyContinue) | Measure-Object Length -Sum).Sum
}

$before = Get-DirBytes $emuDir
Write-Host "=== install $Emulator (via $System / $Rom) ===" -ForegroundColor Cyan
Write-Host ("  on disk before: {0:N0} bytes" -f $before)

$preProcs = (Get-Process | Select-Object -ExpandProperty Id)
Start-Process -FilePath $launcher -WorkingDirectory (Split-Path $launcher) `
    -ArgumentList @('-system', $System, '-emulator', $Emulator, '-rom', "`"$romPath`"")

$launcherProc = $null
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline -and -not $launcherProc) {
    $launcherProc = Get-Process -Name emulatorLauncher -ErrorAction SilentlyContinue | Where-Object { $_.Id -notin $preProcs } | Select-Object -First 1
    Start-Sleep -Milliseconds 300
}
if (-not $launcherProc) { throw 'emulatorLauncher did not start' }

# The prompt is the only visible window the launcher owns, and it appears about a second in.
$answered = $false
$deadline = (Get-Date).AddSeconds($PromptTimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    if ($launcherProc.HasExited) { break }
    $h = Get-VisibleWindow $launcherProc.Id
    if ($h -ne [IntPtr]::Zero) {
        [void][Probe2Install]::ShowWindow($h, 9)
        [void][Probe2Install]::SetForegroundWindow($h)
        Start-Sleep -Milliseconds 800
        $scan = [byte][Probe2Install]::MapVirtualKey(0x0D, 0)
        [Probe2Install]::keybd_event(0x0D, $scan, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 120
        [Probe2Install]::keybd_event(0x0D, $scan, 2, [UIntPtr]::Zero)
        Write-Host '  answered the install prompt with Enter (Yes)'
        $answered = $true
        break
    }
    Start-Sleep -Milliseconds 500
}
if (-not $answered) { Write-Host '  no prompt appeared; the emulator may already be installed' -ForegroundColor Yellow }

# Downloads report nothing to the log until they finish, so watch the directory instead.
# Growth has its own short deadline: if the prompt was missed, the launcher sits on it
# forever and waiting out the full install timeout says nothing useful.
$stable = 0
$last = -1
$grew = $false
$growDeadline = (Get-Date).AddSeconds(120)
$deadline = (Get-Date).AddSeconds($InstallTimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    $now = Get-DirBytes $emuDir
    if ($now -gt $before) { $grew = $true }
    if (-not $grew -and (Get-Date) -gt $growDeadline) {
        Write-Host '  nothing downloaded within 120 s; the prompt was probably never answered' -ForegroundColor Red
        break
    }
    if ($now -eq $last -and $grew) { $stable++ } else { $stable = 0 }
    if ($stable -ge 6) { break }
    $last = $now
    Start-Sleep -Seconds 2
}

$after = Get-DirBytes $emuDir
$exe = @(Get-ChildItem $emuDir -Recurse -File -Filter *.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name)
Write-Host ("  on disk after : {0:N0} bytes ({1:N1} MB downloaded)" -f $after, (($after - $before) / 1MB))
Write-Host "  executables   : $($exe -join ', ')"

# Stop whatever launched, so the install is the only lasting effect.
Start-Sleep -Seconds 5
Get-Process | Where-Object { $_.Id -notin $preProcs -and $_.MainWindowHandle -ne 0 -and $_.Name -notmatch 'explorer|pwsh|powershell|WindowsTerminal|Code' } |
    ForEach-Object { Write-Host "  stopping $($_.Name)"; try { $_.Kill() } catch {} }
Get-Process -Name emulatorLauncher -ErrorAction SilentlyContinue | Where-Object { $_.Id -notin $preProcs } | ForEach-Object { try { $_.Kill() } catch {} }

Get-Content $log -Tail 4 | ForEach-Object { Write-Host "  $($_.Trim())" -ForegroundColor DarkGray }
