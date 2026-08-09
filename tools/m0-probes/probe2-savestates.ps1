<#
.SYNOPSIS
  M0 probe 2: for one emulator, where a save state really lands and how it relates to the
  template es_savestates.cfg declares.

.DESCRIPTION
  PPSSPP showed the shape of the answer: the emulator writes under its own naming, and
  RetroBat mirrors that into the declared path a moment later, live, leaving a .txt sidecar
  holding the native basename. This generalises that measurement to any emulator.

  Rather than guessing where an emulator writes natively, the probe **snapshots the whole
  saves/<system> subtree** and diffs it, so the native location discovers itself. Three
  snapshots are taken:

    before launch          the baseline
    after the save key     while the emulator is still running
    after the emulator exits   to separate live mirroring from an exit-time pass

  It then reports every file that appeared, marks which fall inside the declared directory,
  and checks the two things the plan depends on: whether the declared <file> template
  actually matches, and whether the declared <image> was produced and is non-empty. The
  screenshot is checked separately because it is mirrored racily and is the field that maps
  onto RomM's optional screenshotFile.

  Pick a rom with no existing save state. Only files that appear during the run are removed
  afterwards, tracked by exact path, so an install's real saves are never touched.

  -SaveKey is in SendKeys syntax ('{F2}', '+{F1}' for shift). RetroBat's own
  .emulationstation/es_padtokey.cfg is the best source for a given emulator; where an
  emulator handles its hotkeys natively, read its generated config after a first launch.

.EXAMPLE
  pwsh -File tools/m0-probes/probe2-savestates.ps1 -Root E:\RetroBat -Emulator libretro -System snes -Rom "Sample (USA).sfc" -SaveKey '{F2}'
  pwsh -File tools/m0-probes/probe2-savestates.ps1 -Root E:\RetroBat -Emulator dolphin -System gamecube -Rom "Sample.iso" -SaveKey '+{F1}' -BootSeconds 60
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [Parameter(Mandatory)] [string] $Emulator,
    [Parameter(Mandatory)] [string] $System,
    [Parameter(Mandatory)] [string] $Rom,
    [string] $SaveKey = '{F2}',
    [string[]] $AlsoWatch,
    [string] $Core,
    [int] $BootSeconds = 40,
    [int] $SettleSeconds = 6,
    [switch] $Autosave,
    [switch] $KeepArtifacts
)

$ErrorActionPreference = 'Stop'

$launcher = Join-Path $Root 'emulationstation\emulatorLauncher.exe'
$launcherDir = Split-Path $launcher
$log = Join-Path $Root 'emulationstation\emulatorLauncher.log'
$savesRoot = Join-Path $Root "saves\$System"
$romPath = Join-Path $Root "roms\$System\$Rom"
$stem = [System.IO.Path]::GetFileNameWithoutExtension($Rom)

foreach ($p in @($launcher, $romPath)) { if (-not (Test-Path $p)) { throw "not found: $p" } }

# Refuse a rom that already owns save data. Launching one rewrites its battery save on
# exit, and this probe must not touch an install's real saves.
# StartsWith, not -like: rom names routinely contain [ ] (translation and version tags),
# which -like reads as a wildcard character class and then throws.
$existing = @(Get-ChildItem (Join-Path $Root "saves\$System") -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name.StartsWith($stem, [StringComparison]::OrdinalIgnoreCase) })
if ($existing) {
    throw "$Rom already owns $($existing.Count) file(s) under saves\$System (e.g. $($existing[0].Name)). Pick a rom with none."
}

# The declared templates, read from the live file rather than assumed.
[xml] $ss = Get-Content (Join-Path $Root 'emulationstation\.emulationstation\es_savestates.cfg') -Raw
$decl = $ss.savestates.emulator | Where-Object { $_.name -eq $Emulator }
if (-not $decl) { throw "es_savestates.cfg declares no emulator named '$Emulator'" }

function Expand-Template([string] $t) {
    if (-not $t) { return $null }
    $t.Replace('{{system}}', $System).Replace('{{romfilename}}', $stem).Replace('{{core}}', $Core)
}
$declDir = Expand-Template $decl.directory
$declFile = Expand-Template $decl.file
$declImage = Expand-Template $decl.image
if ($Autosave) {
    if ($decl.autosave -ne 'true') { Write-Host "  note: $Emulator does not declare autosave=true" -ForegroundColor Yellow }
    # In autosave mode the interesting templates are the autosave pair, so report against those.
    if ($decl.autosave_file) { $declFile = Expand-Template $decl.autosave_file }
    if ($decl.autosave_image) { $declImage = Expand-Template $decl.autosave_image }
}

Write-Host "=== $Emulator / $System / $Rom ===" -ForegroundColor Cyan
Write-Host "  declared dir  : saves/$declDir"
Write-Host "  declared file : $declFile"
Write-Host "  declared image: $declImage"
if ($decl.firstslot) { Write-Host "  slots         : $($decl.firstslot)..$($decl.lastslot)" }

Add-Type @"
using System; using System.Runtime.InteropServices;
public class Probe2Ss {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint type);
}
"@

# keybd_event with a real scan code, not SendKeys. SendKeys posts window messages, which
# SDL-based emulators never see, and keybd_event with scan code 0 is ignored by them too:
# RetroArch accepts the keystroke only once MapVirtualKey supplies the hardware scan code.
#
# Modifiers follow SendKeys spelling: '+' shift, '^' ctrl, '%' alt, in any order. Getting
# these right matters more than it looks: RetroBat rebinds hotkeys per emulator, and BizHawk
# ships with Ctrl+F1 to save and Shift+F1 to *load*, so the wrong modifier silently loads
# nothing instead of saving.
function Send-HotKey([string] $Spec) {
    $mods = @()
    $rest = $Spec
    while ($rest.Length -and '+^%'.Contains($rest[0])) {
        $mods += switch ($rest[0]) { '+' { 0x10 } '^' { 0x11 } '%' { 0x12 } }
        $rest = $rest.Substring(1)
    }
    $name = $rest.Trim('{', '}')
    $vk = switch -Regex ($name) {
        '^F(\d+)$' { 0x6F + [int]$Matches[1] }   # F1 = 0x70
        '^\d$' { 0x30 + [int]$name }
        '^[A-Za-z]$' { [int][char]$name.ToUpper() }
        default { throw "unsupported key spec '$Spec'" }
    }
    $scan = [byte][Probe2Ss]::MapVirtualKey([uint32]$vk, 0)
    foreach ($m in $mods) { [Probe2Ss]::keybd_event([byte]$m, [byte][Probe2Ss]::MapVirtualKey([uint32]$m, 0), 0, [UIntPtr]::Zero) }
    [Probe2Ss]::keybd_event([byte]$vk, $scan, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [Probe2Ss]::keybd_event([byte]$vk, $scan, 2, [UIntPtr]::Zero)
    foreach ($m in $mods) { [Probe2Ss]::keybd_event([byte]$m, [byte][Probe2Ss]::MapVirtualKey([uint32]$m, 0), 2, [UIntPtr]::Zero) }
}

# Whole-subtree snapshot: the native location does not have to be known in advance, it
# simply shows up in the diff. -AlsoWatch extends this past saves/<system>, which BizHawk
# needs: RetroBat points its savestate path at emulators/bizhawk/sstates/<system>, outside
# the saves tree altogether, so watching saves/ alone reports "nothing happened".
$watched = @($savesRoot) + @($AlsoWatch | Where-Object { $_ } | ForEach-Object {
        if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $Root $_ }
    })

function Get-Tree {
    $map = @{}
    foreach ($rootDir in $watched) {
        if (-not (Test-Path $rootDir)) { continue }
        Get-ChildItem $rootDir -Recurse -File -Force -ErrorAction SilentlyContinue | ForEach-Object {
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
            # Files outside saves/<system> are shown relative to the RetroBat root, so an
            # emulator writing somewhere unexpected is obvious rather than mangled.
            $rel = if ($k.StartsWith($savesRoot, [StringComparison]::OrdinalIgnoreCase)) {
                $k.Substring($savesRoot.Length).TrimStart('\')
            } else {
                $k.Substring($Root.TrimEnd('\').Length).TrimStart('\')
            }
            $rows += [pscustomobject]@{
                State    = $state
                Relative = $rel
                Bytes    = $fi.Length
                Modified = $fi.LastWriteTime.ToString('HH:mm:ss.fff')
                Full     = $k
            }
        }
    }
    $rows | Sort-Object Modified
}

function Show-Rows($Label, $Rows) {
    Write-Host "  -- $Label" -ForegroundColor DarkGray
    if (-not $Rows) { Write-Host '     (nothing)' -ForegroundColor DarkGray; return }
    $declLeaf = if ($declDir) { Split-Path $declDir -Leaf } else { $null }
    $Rows | ForEach-Object {
        $mark = if ($declLeaf -and $_.Relative.StartsWith($declLeaf, [StringComparison]::OrdinalIgnoreCase)) { '*' } else { ' ' }
        Write-Host ("    {0}{1,-7} {2,-58} {3,11} B  {4}" -f $mark, $_.State, $_.Relative, $_.Bytes, $_.Modified)
    }
}

# The declared templates are literal text plus {{slot...}}, so escape the whole thing and
# reopen only the placeholder. Escaping matters: rom names contain regex metacharacters.
# Note [regex]::Escape escapes '{' but leaves '}' alone, so the placeholder to strip back
# out is the asymmetric '\{\{slot...}}'.
function Get-TemplateRegex([string] $Template) {
    if (-not $Template) { return $null }
    $escaped = [regex]::Escape($Template)
    '^' + ($escaped -replace '\\\{\\\{slot[^}]*\}\}', '.*') + '$'
}

$created = @()
$proc = $null
$findings = @()

$esCfg = Join-Path $Root 'emulationstation\.emulationstation\es_settings.cfg'
$esBackup = "$esCfg.probe2ss-backup"

try {
    if ($Autosave) {
        # RetroBat's shared "AUTO SAVE/LOAD" option. With it on, the emulator writes its
        # resume state on exit, which is the only route for emulators whose save-state
        # hotkey RetroBat binds to a gamepad combo rather than the keyboard.
        Copy-Item $esCfg $esBackup -Force
        [xml] $x = Get-Content $esCfg -Raw
        $n = $x.CreateElement('string'); $n.SetAttribute('name', "$System.autosave"); $n.SetAttribute('value', '1')
        $x.config.AppendChild($n) | Out-Null
        $x.Save($esCfg)
        Write-Host "  set $System.autosave=1 in es_settings.cfg (restored afterwards)" -ForegroundColor DarkGray
    }

    $before = Get-Tree
    Write-Host "  baseline: $($before.Count) file(s) under saves/$System"

    $preProcs = (Get-Process | Select-Object -ExpandProperty Id)
    $argList = @('-system', $System, '-emulator', $Emulator, '-rom', "`"$romPath`"")
    if ($Core) { $argList += @('-core', $Core) }
    Start-Process -FilePath $launcher -WorkingDirectory $launcherDir -ArgumentList $argList

    # The emulator's process name varies, so take whatever new top-level window appears.
    $deadline = (Get-Date).AddSeconds(120)
    while ((Get-Date) -lt $deadline) {
        $proc = Get-Process | Where-Object {
            $_.Id -notin $preProcs -and $_.MainWindowHandle -ne 0 -and
            $_.Name -notmatch 'emulatorLauncher|explorer|pwsh|powershell|WindowsTerminal|Code'
        } | Select-Object -First 1
        if ($proc) { break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $proc) {
        Get-Content $log -Tail 12 | ForEach-Object { Write-Host "     $($_.Trim())" -ForegroundColor Red }
        throw 'no emulator window appeared'
    }
    Write-Host "  emulator: $($proc.Name) pid $($proc.Id)"
    Start-Sleep -Seconds $BootSeconds
    Write-Host "  window  : [$((Get-Process -Id $proc.Id).MainWindowTitle)]"

    if ($Autosave) {
        Write-Host '  autosave mode: no key sent, the state is expected on exit'
    } else {
        [void][Probe2Ss]::ShowWindow($proc.MainWindowHandle, 9)
        [void][Probe2Ss]::SetForegroundWindow($proc.MainWindowHandle)
        Start-Sleep -Milliseconds 1000
        Write-Host "  sending $SaveKey at $((Get-Date).ToString('HH:mm:ss.fff'))"
        Send-HotKey $SaveKey
    }
    Start-Sleep -Seconds $SettleSeconds

    $live = Compare-Tree $before (Get-Tree)
    Show-Rows 'while the emulator is still running' $live

    if ($proc -and -not $proc.HasExited) {
        [void]$proc.CloseMainWindow()
        $d = (Get-Date).AddSeconds(40)
        while ((Get-Date) -lt $d -and -not $proc.HasExited) { Start-Sleep -Milliseconds 500; $proc.Refresh() }
        if (-not $proc.HasExited) { $proc.Kill() }
    }
    Start-Sleep -Seconds 5

    $after = Compare-Tree $before (Get-Tree)
    Show-Rows 'after the emulator exits' $after
    $created = $after

    # The questions the plan actually asks.
    $liveNames = @($live | ForEach-Object { $_.Relative })
    $allNames = @($after | ForEach-Object { $_.Relative })
    $declPath = if ($declFile) { (Join-Path $declDir $declFile).Replace("$System\", '') } else { $null }

    $fileRx = Get-TemplateRegex $declFile
    $matched = @($after | Where-Object { $fileRx -and (Split-Path $_.Relative -Leaf) -match $fileRx })
    $findings += "state files appeared                 : $($after.Count)"
    $findings += "appeared while still running         : $($liveNames.Count -gt 0)"
    $findings += "anything further appeared at exit    : $($allNames.Count -ne $liveNames.Count)"
    $findings += "a file matches the declared <file>   : $($matched.Count -gt 0)"
    if ($matched) { $findings += "  matched                            : $($matched[0].Relative)" }

    if ($declImage) {
        $imgRx = Get-TemplateRegex $declImage
        $img = @($after | Where-Object { (Split-Path $_.Relative -Leaf) -match $imgRx })
        $imgState = if (-not $img) { 'ABSENT' } elseif ($img[0].Bytes -eq 0) { 'ZERO BYTES' } else { "$($img[0].Bytes) B" }
        $findings += "declared <image> produced            : $imgState"
    }
    $txt = @($after | Where-Object { $_.Relative -like '*.txt' })
    if ($txt) { $findings += "name-mapping sidecar                 : $($txt[0].Relative) -> [$((Get-Content -LiteralPath $txt[0].Full -Raw).Trim())]" }
    else { $findings += 'name-mapping sidecar                 : none (native naming already matches the rom filename)' }
} finally {
    if ($proc) {
        $p = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
        if ($p -and -not $p.HasExited) { $p.Kill(); Start-Sleep -Seconds 2 }
    }
    if ($Autosave -and (Test-Path $esBackup)) {
        Copy-Item $esBackup $esCfg -Force
        Remove-Item $esBackup -Force
        Write-Host 'restored es_settings.cfg' -ForegroundColor DarkGray
    }

    if (-not $KeepArtifacts -and $created) {
        $n = 0
        foreach ($row in $created) { if ($row.State -eq 'new' -and (Test-Path -LiteralPath $row.Full)) { Remove-Item -LiteralPath $row.Full -Force; $n++ } }
        Write-Host ''
        Write-Host "removed $n file(s) created by this probe" -ForegroundColor DarkGray
        $changed = @($created | Where-Object { $_.State -eq 'changed' })
        if ($changed) { Write-Host "NOTE: $($changed.Count) pre-existing file(s) were modified and left as-is:" -ForegroundColor Yellow; $changed | ForEach-Object { Write-Host "  $($_.Relative)" -ForegroundColor Yellow } }
    }
}

Write-Host ''
Write-Host '=== findings ===' -ForegroundColor Cyan
$findings | ForEach-Object { Write-Host "  $_" }
