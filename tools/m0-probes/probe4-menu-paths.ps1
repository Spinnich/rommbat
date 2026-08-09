<#
.SYNOPSIS
  M0 probe 4: where a .menu executable path is rooted, and whether it can escape upward.

.DESCRIPTION
  Stock .menu files hold a leading-backslash relative path (`\retroarch\retroarch.exe`)
  that resolves under emulators\. This decides where RomMBat is allowed to install, so it
  needs a measurement rather than an inference.

  Three variants are installed, differing only in how the executable is addressed:

    A  ..\..\plugins\rommbat\zz-probe.bat   escape upward from the assumed emulators\ base
    B  \plugins\rommbat\zz-probe.bat        leading backslash read as root-relative
    C  \rommbat\zz-probe.bat                the stock form, under emulators\ (control)

  Each variant passes its own letter as an argument, and the .bat writes
  rommbat-probe\menu-<letter>.txt. Whichever markers appear identify which addressing
  forms work, with no observation needed from the operator. C failing would mean the
  harness itself is broken rather than the path form.

  Passing an argument at all also tests whether the .menu argument line reaches the target.

.EXAMPLE
  pwsh -File tools/m0-probes/probe4-menu-paths.ps1 -Root G:\RetroBat
  pwsh -File tools/m0-probes/probe4-menu-paths.ps1 -Root G:\RetroBat -Report
  pwsh -File tools/m0-probes/probe4-menu-paths.ps1 -Root G:\RetroBat -Uninstall
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [switch] $Report,
    [switch] $Uninstall
)

$ErrorActionPreference = 'Stop'

$menuDir = Join-Path $Root 'system\es_menu'
$probeDir = Join-Path $Root 'rommbat-probe'
$pluginTarget = Join-Path $Root 'plugins\rommbat'
$emulatorTarget = Join-Path $Root 'emulators\rommbat'
$gamelist = Join-Path $menuDir 'gamelist.xml'

$variants = [ordered]@{
    A = '..\..\plugins\rommbat\zz-probe.bat'
    B = '\plugins\rommbat\zz-probe.bat'
    C = '\rommbat\zz-probe.bat'
}

if ($Report) {
    Write-Host '=== .menu path resolution ===' -ForegroundColor Cyan
    foreach ($key in $variants.Keys) {
        $marker = Join-Path $probeDir "menu-$key.txt"
        $ok = Test-Path $marker
        $colour = if ($ok) { 'Green' } else { 'DarkGray' }
        $verdict = if ($ok) { 'LAUNCHED' } else { 'no marker' }
        Write-Host ("  {0}  {1,-38} {2}" -f $key, $variants[$key], $verdict) -ForegroundColor $colour
        if ($ok) { Get-Content $marker | ForEach-Object { Write-Host "       $_" -ForegroundColor DarkGreen } }
    }
    Write-Host ''
    if (Test-Path (Join-Path $probeDir 'menu-C.txt')) {
        Write-Host 'C launched, so the harness works and any failure above is a real path-form failure.'
    } else {
        Write-Host 'C did not launch. Either .bat targets are rejected, or no variant was started at all.' -ForegroundColor Yellow
    }
    return
}

if ($Uninstall) {
    foreach ($key in $variants.Keys) {
        $f = Join-Path $menuDir "zz-rommbat-$key.menu"
        if (Test-Path $f) { Remove-Item $f -Force; Write-Host "removed $f" }
    }
    foreach ($d in @($pluginTarget, $emulatorTarget)) {
        if (Test-Path $d) { Remove-Item $d -Recurse -Force; Write-Host "removed $d" }
    }
    if (Test-Path "$gamelist.rommbat-backup") {
        Move-Item "$gamelist.rommbat-backup" $gamelist -Force
        Write-Host "restored $gamelist"
    }
    Write-Host 'menu probe uninstalled.'
    return
}

# The target writes a marker naming the variant that invoked it, and records how it was
# addressed so the log is self-describing.
$batBody = @'
@echo off
setlocal
set "MARK=%~dp0..\..\rommbat-probe\menu-%1.txt"
>"%MARK%" echo(variant=%1
>>"%MARK%" echo(launched=%DATE% %TIME%
>>"%MARK%" echo(target=%~f0
>>"%MARK%" echo(cwd=%CD%
>>"%MARK%" echo(allargs=%*
endlocal
'@

New-Item -ItemType Directory -Force -Path $probeDir, $pluginTarget, $emulatorTarget | Out-Null
Set-Content -Path (Join-Path $pluginTarget 'zz-probe.bat') -Value $batBody -Encoding ascii
Set-Content -Path (Join-Path $emulatorTarget 'zz-probe.bat') -Value $batBody -Encoding ascii

foreach ($key in $variants.Keys) {
    $menuFile = Join-Path $menuDir "zz-rommbat-$key.menu"
    Set-Content -Path $menuFile -Value "$($variants[$key])`r`n$key" -Encoding ascii
    Write-Host "installed $menuFile  ->  $($variants[$key]) $key"
}

# ES reads names and art for menu entries from es_menu\gamelist.xml, so an entry without
# one shows as a bare filename. Adding entries is part of what "minimum viable" means.
if (-not (Test-Path "$gamelist.rommbat-backup")) {
    Copy-Item $gamelist "$gamelist.rommbat-backup"
}

[xml] $xml = Get-Content $gamelist
foreach ($key in $variants.Keys) {
    $game = $xml.CreateElement('game')
    foreach ($pair in @{ path = "./zz-rommbat-$key.menu"; name = "ZZ RomMBat probe $key"; desc = "M0 probe 4 variant $key : $($variants[$key])"; genre = 'Probe' }.GetEnumerator()) {
        $node = $xml.CreateElement($pair.Key)
        $node.InnerText = $pair.Value
        $game.AppendChild($node) | Out-Null
    }
    $xml.gameList.AppendChild($game) | Out-Null
}
$xml.Save($gamelist)
Write-Host "added 3 entries to $gamelist (backup kept alongside)"

Write-Host ''
Write-Host 'In the RetroBat menu system, launch each of "ZZ RomMBat probe A/B/C", then re-run with -Report.' -ForegroundColor Yellow
