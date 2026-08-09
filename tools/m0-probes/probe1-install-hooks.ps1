<#
.SYNOPSIS
  M0 probe 1: install echo-to-log hooks into every ES event folder.

.DESCRIPTION
  Answers three things at once:

    * what RetroBat's EmulationStation actually passes to a .bat per event, against
      Batocera's documented $1 rom / $2 basename / $3 system / $4 emulator / $5 core
    * whether hooks block the game launch, and for how long, by optionally sleeping a
      known number of seconds and comparing the hook timestamp against the millisecond
      timestamps in emulationstation/emulatorLauncher.log
    * whether ES runs several scripts in one event folder, since `start` and
      `update-gamelists` already ship updatestores.bat and we add a second file beside it

  Everything written is named zz-rommbat-probe.bat or lives under <root>\rommbat-probe\,
  so -Uninstall removes the probe completely.

  The hooks resolve the log through %~dp0..\..\..\.. (four levels), which is itself under
  test: three levels reaches emulationstation\, not the root.

.EXAMPLE
  pwsh -File tools/m0-probes/probe1-install-hooks.ps1 -Root G:\RetroBat
  pwsh -File tools/m0-probes/probe1-install-hooks.ps1 -Root G:\RetroBat -DelaySeconds 8
  pwsh -File tools/m0-probes/probe1-install-hooks.ps1 -Root G:\RetroBat -Uninstall
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [int]    $DelaySeconds = 0,
    [switch] $Uninstall
)

$ErrorActionPreference = 'Stop'

$scriptsDir = Join-Path $Root 'emulationstation\.emulationstation\scripts'
$probeDir = Join-Path $Root 'rommbat-probe'
$hookName = 'zz-rommbat-probe.bat'

if (-not (Test-Path $scriptsDir)) {
    throw "no ES scripts directory at $scriptsDir - is $Root a RetroBat root?"
}

if ($Uninstall) {
    Get-ChildItem -Path $scriptsDir -Recurse -Filter $hookName -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-Item $_.FullName -Force; Write-Host "removed $($_.FullName)" }
    if (Test-Path $probeDir) {
        Remove-Item $probeDir -Recurse -Force
        Write-Host "removed $probeDir"
    }
    Write-Host 'probe uninstalled.'
    return
}

New-Item -ItemType Directory -Force -Path $probeDir | Out-Null

# ping is the sleep that works in a batch file with no console attached; `timeout` fails
# with "input redirection is not supported" when ES launches the script.
#
# The delay is skipped on the very first game-start and applied on every one after, so a
# single ES session yields both the baseline and the blocking measurement. Otherwise the
# probe needs two sessions to say anything.
$sleepLine = if ($DelaySeconds -gt 0) {
    @"
if not exist "%ROOT%\rommbat-probe\first.done" goto :baseline
ping -n $($DelaySeconds + 1) 127.0.0.1 >nul
>>"%LOG%" echo(  SLEPT=$DelaySeconds T_END=%DATE% %TIME%
goto :done
:baseline
>>"%LOG%" echo(  SLEPT=0 T_END=%DATE% %TIME%
echo marker>"%ROOT%\rommbat-probe\first.done"
:done
"@
} else {
    ''
}

$events = Get-ChildItem -Path $scriptsDir -Directory | Select-Object -ExpandProperty Name

foreach ($event in $events) {
    $target = Join-Path (Join-Path $scriptsDir $event) $hookName

    $body = @"
@echo off
rem M0 probe 1. Throwaway: records what ES passes and when. Safe to delete.
setlocal
set "ROOT=%~dp0..\..\..\.."
set "LOG=%ROOT%\rommbat-probe\hooks.log"
>>"%LOG%" echo(=== EVENT=$event T_START=%DATE% %TIME% PID=%RANDOM%
>>"%LOG%" echo(  SCRIPT=%~f0
>>"%LOG%" echo(  CWD=%CD%
>>"%LOG%" echo(  ROOT_RESOLVED=%ROOT%
>>"%LOG%" echo(  ALL=%*
>>"%LOG%" echo(  1=%1
>>"%LOG%" echo(  2=%2
>>"%LOG%" echo(  3=%3
>>"%LOG%" echo(  4=%4
>>"%LOG%" echo(  5=%5
>>"%LOG%" echo(  6=%6
>>"%LOG%" echo(  7=%7
$sleepLine
endlocal
"@

    Set-Content -Path $target -Value $body -Encoding ascii
    Write-Host "installed $event\$hookName"
}

Write-Host ''
Write-Host "log:   $probeDir\hooks.log"
Write-Host "delay: $DelaySeconds s"
if ($DelaySeconds -gt 0) {
    Write-Host 'Launch a game now. If time-to-game grows by roughly the delay, hooks block.' -ForegroundColor Yellow
}
