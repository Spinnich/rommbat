<#
.SYNOPSIS
  M0 probe 2, last item: does flycast_vmupergame really convert Dreamcast's shared VMUs into
  per-game ones, and what does the resulting tree look like?

.DESCRIPTION
  Dreamcast is the purest class-D case in the save model: four port-keyed files
  (vmu_save_A1.bin .. D1.bin) shared by every game, with nothing in the path naming a game.
  es_features.cfg declares flycast_vmupergame, whose description claims "each game will have
  its own VMU in port 1", and the per-game es_settings.cfg override is proven, but the two
  had never been combined. Whether class D converts here decides whether Dreamcast saves can
  be attributed to a rom_id at all.

  Two launches of the same game, differing only in the override:

    control    no override. Confirms the shared files are what the game touches.
    pergame    dreamcast["<rom>"].flycast_vmupergame=1 in es_settings.cfg.

  Each launch snapshots the whole saves/dreamcast subtree before and after, so the per-game
  location discovers itself rather than being guessed, and the generated emu.cfg is read
  after each launch to see how the option is expressed (RetroBat writes PerGameVmu there).

  Port 1 is the interesting one: if only A1 converts, ports B, C and D stay shared and
  unattributable, which is a real limit on what RomMBat can promise.

  Both the es_settings.cfg change and any file the probe creates are reverted afterwards.
  Files the run *modified* are reported rather than restored, so pick a rom you are happy to
  have booted.

.EXAMPLE
  pwsh -File tools/m0-probes/probe2-vmu-pergame.ps1 -Root E:\RetroBat -Rom "Bangai-O (USA).chd"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [Parameter(Mandatory)] [string] $Rom,
    [string] $System = 'dreamcast',
    [string] $Emulator = 'flycast',
    [int] $BootSeconds = 75,
    [switch] $SkipControl
)

$ErrorActionPreference = 'Stop'

$launcher = Join-Path $Root 'emulationstation\emulatorLauncher.exe'
$esCfg = Join-Path $Root 'emulationstation\.emulationstation\es_settings.cfg'
$esBackup = "$esCfg.probe2vmu-backup"
$emuCfg = Join-Path $Root "emulators\$Emulator\emu.cfg"
$savesRoot = Join-Path $Root "saves\$System"
$romPath = Join-Path $Root "roms\$System\$Rom"

foreach ($p in @($launcher, $romPath, $esCfg)) { if (-not (Test-Path $p)) { throw "not found: $p" } }

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
    $rows | Sort-Object Relative
}

function Show-Rows($Label, $Rows) {
    Write-Host "  -- $Label" -ForegroundColor DarkGray
    if (-not $Rows) { Write-Host '     (nothing)' -ForegroundColor DarkGray; return }
    $Rows | ForEach-Object { Write-Host ("    {0,-7} {1,-62} {2,10} B  {3}" -f $_.State, $_.Relative, $_.Bytes, $_.Modified) }
}

# The per-game key must carry the rom's extension: ports["gong"] is ignored where
# ports["gong.libretro"] takes effect, and the failure is silent.
function Set-PerGameOverride([string] $Value) {
    [xml] $x = Get-Content $esCfg -Raw
    $key = "$System[`"$Rom`"].flycast_vmupergame"
    $existing = @($x.config.ChildNodes | Where-Object { $_ -is [System.Xml.XmlElement] -and $_.GetAttribute('name') -eq $key })
    foreach ($n in $existing) { [void]$x.config.RemoveChild($n) }
    if ($null -ne $Value) {
        $n = $x.CreateElement('string')
        $n.SetAttribute('name', $key)
        $n.SetAttribute('value', $Value)
        [void]$x.config.AppendChild($n)
    }
    $x.Save($esCfg)
}

function Get-EmuCfgVmu {
    if (-not (Test-Path $emuCfg)) { return '(no emu.cfg)' }
    $lines = Select-String -Path $emuCfg -Pattern '^(PerGameVmu|Dreamcast\.VMUPath)' | ForEach-Object { $_.Line.Trim() }
    if ($lines) { $lines -join ' ; ' } else { '(no VMU keys)' }
}

function Invoke-Launch([string] $Label) {
    Write-Host ''
    Write-Host "=== $Label ===" -ForegroundColor Cyan
    Write-Host "  emu.cfg before: $(Get-EmuCfgVmu)"
    $before = Get-Tree
    Write-Host "  baseline: $($before.Count) file(s) under saves/$System"

    $preProcs = (Get-Process | Select-Object -ExpandProperty Id)
    $argList = @('-system', $System, '-emulator', $Emulator, '-rom', "`"$romPath`"")
    Start-Process -FilePath $launcher -WorkingDirectory (Split-Path $launcher) -ArgumentList $argList

    $proc = $null
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
        Get-Content (Join-Path $Root 'emulationstation\emulatorLauncher.log') -Tail 12 | ForEach-Object { Write-Host "     $($_.Trim())" -ForegroundColor Red }
        throw 'no emulator window appeared'
    }
    Write-Host "  emulator: $($proc.Name) pid $($proc.Id), booting for $BootSeconds s"
    Start-Sleep -Seconds $BootSeconds
    Write-Host "  window  : [$((Get-Process -Id $proc.Id).MainWindowTitle)]"
    Write-Host "  emu.cfg after launch: $(Get-EmuCfgVmu)"

    $live = Compare-Tree $before (Get-Tree)
    Show-Rows 'while flycast is still running' $live

    if (-not $proc.HasExited) {
        [void]$proc.CloseMainWindow()
        $d = (Get-Date).AddSeconds(40)
        while ((Get-Date) -lt $d -and -not $proc.HasExited) { Start-Sleep -Milliseconds 500; $proc.Refresh() }
        if (-not $proc.HasExited) { $proc.Kill() }
    }
    Start-Sleep -Seconds 5

    $after = Compare-Tree $before (Get-Tree)
    Show-Rows 'after flycast exits' $after
    $after
}

Copy-Item $esCfg $esBackup -Force

# Booting a Dreamcast game writes to the shared VMUs, and those hold the install's real save
# data. Copy them aside and put them back, so the probe cannot cost anything.
$vmuDir = Join-Path $savesRoot "$Emulator\vmu"
$vmuBackup = Join-Path ([IO.Path]::GetTempPath()) "rommbat-probe2vmu-$(Get-Date -Format yyyyMMddHHmmss)"
if (Test-Path $vmuDir) {
    New-Item -ItemType Directory -Path $vmuBackup -Force | Out-Null
    Copy-Item "$vmuDir\*" $vmuBackup -Recurse -Force
    Write-Host "backed up $(@(Get-ChildItem $vmuBackup -Recurse -File).Count) VMU file(s) to $vmuBackup" -ForegroundColor DarkGray
}

$created = @()
$summary = @()

try {
    if (-not $SkipControl) {
        Set-PerGameOverride $null
        $control = Invoke-Launch 'control: no override'
        $created += @($control | Where-Object { $_.State -eq 'new' })
        $summary += "control  : $(@($control).Count) file(s) touched, $(@($control | Where-Object { $_.State -eq 'new' }).Count) new"
        $summary += "control  : shared VMUs touched : $(@($control | Where-Object { $_.Relative -match 'vmu_save_[A-D]1\.bin$' }) | ForEach-Object { Split-Path $_.Relative -Leaf }) "
    }

    Set-PerGameOverride '1'
    Write-Host ''
    Write-Host "  wrote $System[`"$Rom`"].flycast_vmupergame=1 to es_settings.cfg" -ForegroundColor DarkGray
    $pergame = Invoke-Launch 'per-game: flycast_vmupergame=1'
    $created += @($pergame | Where-Object { $_.State -eq 'new' })

    $newFiles = @($pergame | Where-Object { $_.State -eq 'new' })
    $newVmu = @($newFiles | Where-Object { $_.Relative -match 'vmu' -or $_.Bytes -eq 131072 })
    $sharedTouched = @($pergame | Where-Object { $_.Relative -match 'vmu[\\/]vmu_save_[A-D]1\.bin$' })

    $summary += "per-game : emu.cfg says            : $(Get-EmuCfgVmu)"
    $summary += "per-game : new file(s)             : $($newFiles.Count)"
    foreach ($f in $newFiles) { $summary += "           $($f.Relative)  $($f.Bytes) B" }
    $summary += "per-game : shared vmu_save_?1.bin still touched : $($sharedTouched.Count)"
    foreach ($f in $sharedTouched) { $summary += "           $($f.Relative) [$($f.State)]" }
    $summary += "per-game : does the rom name appear in a new path : $([bool](@($newFiles | Where-Object { $_.Relative -like "*$([IO.Path]::GetFileNameWithoutExtension($Rom))*" }).Count))"
} finally {
    if (Test-Path $esBackup) {
        Copy-Item $esBackup $esCfg -Force
        Remove-Item $esBackup -Force
        Write-Host ''
        Write-Host 'restored es_settings.cfg' -ForegroundColor DarkGray
    }
    $n = 0
    foreach ($row in $created) { if (Test-Path -LiteralPath $row.Full) { Remove-Item -LiteralPath $row.Full -Force; $n++ } }
    Write-Host "removed $n file(s) created by this probe" -ForegroundColor DarkGray

    if (Test-Path $vmuBackup) {
        Copy-Item "$vmuBackup\*" $vmuDir -Recurse -Force
        Write-Host "restored the shared VMU files from $vmuBackup" -ForegroundColor DarkGray
    }
}

Write-Host ''
Write-Host '=== findings ===' -ForegroundColor Cyan
$summary | ForEach-Object { Write-Host "  $_" }
