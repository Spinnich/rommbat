<#
.SYNOPSIS
  M0 probe 7b: instrument a RetroBat install so a host that silently drops ES hooks
  proves what it dropped and where.

.DESCRIPTION
  Probe 7 moved the stick to a second PC and no ES hook fired there at all: not start,
  not game-end, not quit, while ES itself demonstrably ran and wrote to the same volume.
  The trail is gone (es_log rotates over five files, emulatorLauncher.log rotates, and
  RetroBat.log is overwritten every launch), so the condition has to be reproduced with
  the instruments already in place.

  Three instruments, each answering a different question:

    * ES debug logging, via es_settings.cfg LogLevel=debug. RetroBat's ES logs
      "fireEvent: <dir>/scripts/<event>", " queuing: " and " executing: " on that level,
      so es_log.txt says whether ES even tried, and which directory it resolved.
    * A .bat hook and a .ps1 hook side by side in every event folder. If one runs and the
      other does not, the fault is the interpreter or its file association, not ES.
    * Each hook writes to two destinations, local %TEMP% first and the RetroBat tree
      second. A record in %TEMP% with nothing on the stick means the hook ran and the
      write was lost, which is a different failure from never running.

  Every hook records host facts (computer, user, COMSPEC, HOME, USERPROFILE, PATHEXT,
  resolved root) so the log identifies which machine produced each line.

  Everything written is named zz-rommbat-diag.* or lives under <root>\rommbat-probe\,
  and -Uninstall reverses the es_settings.cfg edit as well.

  ES must not be running: ES rewrites es_settings.cfg from its in-memory model on exit
  and would discard the LogLevel edit.

.EXAMPLE
  pwsh -File tools/m0-probes/probe7b-hook-diagnose.ps1 -Root K:\RetroBat
  pwsh -File tools/m0-probes/probe7b-hook-diagnose.ps1 -Root K:\RetroBat -Uninstall
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [switch] $Uninstall
)

$ErrorActionPreference = 'Stop'

$scriptsDir = Join-Path $Root 'emulationstation\.emulationstation\scripts'
$settings = Join-Path $Root 'emulationstation\.emulationstation\es_settings.cfg'
$probeDir = Join-Path $Root 'rommbat-probe'
$batName = 'zz-rommbat-diag.bat'
$ps1Name = 'zz-rommbat-diag.ps1'
$backup = "$settings.rommbat-bak"

if (-not (Test-Path $scriptsDir)) {
    throw "no ES scripts directory at $scriptsDir - is $Root a RetroBat root?"
}

if (Get-Process -Name 'emulationstation' -ErrorAction SilentlyContinue) {
    throw 'EmulationStation is running. Quit it first, or the es_settings.cfg edit is discarded on exit.'
}

function Set-LogLevel {
    param([AllowEmptyString()] [string] $Value)

    [xml] $xml = Get-Content -Path $settings -Raw
    $node = $xml.config.SelectSingleNode("string[@name='LogLevel']")

    if ([string]::IsNullOrEmpty($Value)) {
        if ($node) { $xml.config.RemoveChild($node) | Out-Null }
    }
    else {
        if (-not $node) {
            $node = $xml.CreateElement('string')
            $node.SetAttribute('name', 'LogLevel')
            $xml.config.AppendChild($node) | Out-Null
        }
        $node.SetAttribute('value', $Value)
    }

    $xml.Save($settings)
}

if ($Uninstall) {
    Get-ChildItem -Path $scriptsDir -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -in @($batName, $ps1Name) } |
        ForEach-Object { Remove-Item $_.FullName -Force; Write-Host "removed $($_.FullName)" }

    if (Test-Path $backup) {
        Copy-Item $backup $settings -Force
        Remove-Item $backup -Force
        Write-Host 'es_settings.cfg restored from backup'
    }
    else {
        Set-LogLevel -Value $null
        Write-Host 'LogLevel removed from es_settings.cfg'
    }

    Write-Host 'probe 7b uninstalled. Collected logs under rommbat-probe\ are left in place.'
    return
}

New-Item -ItemType Directory -Force -Path $probeDir | Out-Null

if (-not (Test-Path $backup)) { Copy-Item $settings $backup }
Set-LogLevel -Value 'debug'
Write-Host "es_settings.cfg: LogLevel=debug (backup at $backup)"

$events = Get-ChildItem -Path $scriptsDir -Directory | Select-Object -ExpandProperty Name

# %TEMP% is written before the tree so a blocked or lost write to the removable volume
# still leaves proof that the hook ran at all.
$batTemplate = @'
@echo off
rem M0 probe 7b. Throwaway diagnostic. Safe to delete.
setlocal
set "ID=%RANDOM%"
set "ARGS=%*"
set "ROOT=%~dp0..\..\..\.."
if not defined TEMP set "TEMP=%USERPROFILE%"
call :emit "%TEMP%\rommbat-diag-%COMPUTERNAME%-bat.log"
call :emit "%ROOT%\rommbat-probe\diag-%COMPUTERNAME%-bat.log"
endlocal
goto :eof

:emit
>>%1 echo(=== bat EVENT=__EVENT__ ID=%ID% T=%DATE% %TIME%
>>%1 echo(  SCRIPT=%~f0
>>%1 echo(  ARGS=%ARGS%
>>%1 echo(  ROOT_RESOLVED=%ROOT%
>>%1 echo(  HOST=%COMPUTERNAME% USER=%USERNAME%
>>%1 echo(  COMSPEC=%COMSPEC%
>>%1 echo(  HOME=%HOME% USERPROFILE=%USERPROFILE%
>>%1 echo(  CWD=%CD%
>>%1 echo(  PATHEXT=%PATHEXT%
goto :eof
'@

$ps1Template = @'
# M0 probe 7b. Throwaway diagnostic. Safe to delete.
$ErrorActionPreference = 'SilentlyContinue'

$root = $PSScriptRoot
1..4 | ForEach-Object { $root = Split-Path -Parent $root }

$lines = @(
    "=== ps1 EVENT=__EVENT__ ID=$PID T=$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff'))"
    "  SCRIPT=$PSCommandPath"
    "  ARGS=$($args -join '  ')"
    "  ROOT_RESOLVED=$root"
    "  HOST=$env:COMPUTERNAME USER=$env:USERNAME"
    "  PSVERSION=$($PSVersionTable.PSVersion) POLICY=$(Get-ExecutionPolicy)"
    "  HOME=$env:HOME USERPROFILE=$env:USERPROFILE"
    "  CWD=$($PWD.Path)"
)

$dests = @(
    (Join-Path $env:TEMP "rommbat-diag-$env:COMPUTERNAME-ps1.log")
    (Join-Path $root "rommbat-probe\diag-$env:COMPUTERNAME-ps1.log")
)

foreach ($dest in $dests) {
    try { Add-Content -Path $dest -Value $lines -Encoding ascii } catch { }
}
'@

foreach ($event in $events) {
    $dir = Join-Path $scriptsDir $event
    Set-Content -Path (Join-Path $dir $batName) -Value $batTemplate.Replace('__EVENT__', $event) -Encoding ascii
    Set-Content -Path (Join-Path $dir $ps1Name) -Value $ps1Template.Replace('__EVENT__', $event) -Encoding ascii
    Write-Host "installed $event\{$batName,$ps1Name}"
}

# ES invokes powershell.exe, not pwsh, and passes no -ExecutionPolicy. A Restricted
# policy there makes the .ps1 hook fail for a reason unrelated to what is being measured.
$wpsPolicy = & powershell.exe -NoProfile -Command 'Get-ExecutionPolicy' 2>$null
Write-Host ''
Write-Host "windows powershell execution policy: $wpsPolicy"
if ($wpsPolicy -in @('Restricted', 'AllSigned')) {
    Write-Host '  the .ps1 hook will not run under that policy, on any host. Fix it or read only the .bat result.' -ForegroundColor Yellow
}

Write-Host ''
Write-Host 'installed. Next:'
Write-Host "  1. baseline on this machine: launch RetroBat, start and exit a game, quit ES"
Write-Host "  2. pwsh -File tools/m0-probes/probe7b-collect.ps1 -Root $Root -Label baseline"
Write-Host '  3. repeat both on the second host, then collect there before ES is started again'
