<#
.SYNOPSIS
  M0 probe 7b: can this PC run a .bat at all, and can it run one from the RetroBat tree?

.DESCRIPTION
  Reading the registry infers the answer. This runs the experiment instead, the same way
  EmulationStation does (ShellExecute, no arguments, exactly like the start and quit hooks),
  and reports the registry layers alongside it so a failure has a named cause.

  Four tests, chosen so a failure isolates itself:

    .bat from the local disk    the .bat association, nothing else
    .bat from the RetroBat tree adds removable-media and antivirus policy
    .bat with a quoted argument the known upstream bug, expected to fail everywhere
    .exe from the RetroBat tree what RomMBat's own hooks will be

  Needs no elevation and changes nothing. Runs under Windows PowerShell 5.1.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File probe7b-check-bat.ps1 -Root K:\RetroBat
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root
)

$ErrorActionPreference = 'Continue'

if (-not (Test-Path $Root)) {
    Write-Host ''
    Write-Host "  $Root not found. Is the stick plugged in, and is that its drive letter here?" -ForegroundColor Red
    Write-Host '  Check the letter in Explorer and pass it as -Root, for example -Root E:\RetroBat' -ForegroundColor Red
    Write-Host ''
    exit 1
}

function Get-KeyValue {
    param([string] $Path, [string] $Name = '(default)')
    if (-not (Test-Path $Path)) { return $null }
    return (Get-ItemProperty -Path $Path -ErrorAction SilentlyContinue).$Name
}

# Runs a script the way ES does and reports whether it actually executed. A missing
# association can raise the "How do you want to open this file?" dialog, which blocks
# ShellExecute, so every wait is bounded.
function Test-Launch {
    param([string] $Label, [string] $File, [string] $Arguments = '', [string] $Marker)

    Remove-Item $Marker -Force -ErrorAction SilentlyContinue
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $File
    $psi.Arguments = $Arguments
    $psi.UseShellExecute = $true

    $note = ''
    try {
        $p = [Diagnostics.Process]::Start($psi)
        if (-not $p.WaitForExit(8000)) {
            $note = 'still running after 8s, look for a dialog on screen'
            try { $p.Kill() } catch { }
        }
    }
    catch [ComponentModel.Win32Exception] {
        $note = "Windows refused to start it: $($_.Exception.Message)"
    }
    catch {
        $note = "failed: $($_.Exception.Message)"
    }

    Start-Sleep -Milliseconds 300
    $ran = Test-Path $Marker
    Remove-Item $Marker -Force -ErrorAction SilentlyContinue

    [pscustomobject]@{ Test = $Label; Ran = $ran; Note = $note }
}

Write-Host ''
Write-Host '=== how .bat is associated on this PC ===' -ForegroundColor Cyan

$layers = @(
    @{ What = 'Open With choice (should be ABSENT)'; Path = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.bat\UserChoice'; Name = 'ProgId' }
    @{ What = 'per-user .bat  (should be ABSENT)'; Path = 'HKCU:\Software\Classes\.bat' }
    @{ What = 'machine .bat   (should be batfile)'; Path = 'Registry::HKEY_CLASSES_ROOT\.bat' }
    @{ What = 'batfile command'; Path = 'Registry::HKEY_CLASSES_ROOT\batfile\shell\open\command' }
)

foreach ($l in $layers) {
    $name = if ($l.Name) { $l.Name } else { '(default)' }
    $v = Get-KeyValue -Path $l.Path -Name $name
    $shown = if ($null -eq $v -or $v -eq '') { '(absent)' } else { $v }
    "  {0,-36} {1}" -f $l.What, $shown | Write-Host
}

"  {0,-36} {1}" -f 'PATHEXT contains .BAT', $($env:PATHEXT -split ';' -contains '.BAT') | Write-Host

Write-Host ''
Write-Host '=== can it actually run one ===' -ForegroundColor Cyan
Write-Host '  If a dialog appears asking how to open a file, that IS the answer. Cancel it.' -ForegroundColor Yellow

# [IO.Path]::Combine rather than Join-Path: Join-Path validates the drive qualifier and
# throws on a letter this machine does not have mounted.
$localDir = [IO.Path]::Combine($env:TEMP, 'rommbat-batcheck')
$treeDir = [IO.Path]::Combine($Root, 'rommbat-probe')
New-Item -ItemType Directory -Force -Path $localDir | Out-Null

$results = @()

$localMarker = Join-Path $localDir 'ok.txt'
$localBat = Join-Path $localDir 'check.bat'
Set-Content -Path $localBat -Encoding ascii -Value "@echo off`r`n>>`"$localMarker`" echo ok"
$results += Test-Launch -Label '.bat from local disk, no arguments' -File $localBat -Marker $localMarker

$treeMarker = Join-Path $localDir 'ok2.txt'
$treeBat = [IO.Path]::Combine($treeDir, 'check.bat')
Set-Content -Path $treeBat -Encoding ascii -Value "@echo off`r`n>>`"$treeMarker`" echo ok"
$results += Test-Launch -Label '.bat from the RetroBat tree' -File $treeBat -Marker $treeMarker

$results += Test-Launch -Label '.bat with a quoted argument' -File $treeBat -Arguments 'rom "Mr Boom"' -Marker $treeMarker

$exe = [IO.Path]::Combine($treeDir, 'zz-rommbat-diag.exe')

# Watch the %TEMP% copy, not the one in the tree: run from rommbat-probe rather than from
# an event folder, the exe resolves its root four levels up to the drive root and its write
# to the tree fails silently. The local copy is written first and is unaffected.
$exeLog = [IO.Path]::Combine($env:TEMP, "rommbat-diag-$env:COMPUTERNAME-exe.log")
if (Test-Path $exe) {
    $before = if (Test-Path $exeLog) { (Get-Item $exeLog).Length } else { 0 }
    Test-Launch -Label 'ignored' -File $exe -Marker (Join-Path $localDir 'never') | Out-Null
    Start-Sleep -Milliseconds 400
    $after = if (Test-Path $exeLog) { (Get-Item $exeLog).Length } else { 0 }
    $results += [pscustomobject]@{ Test = '.exe from the RetroBat tree'; Ran = ($after -gt $before); Note = '' }
}
else {
    $results += [pscustomobject]@{ Test = '.exe from the RetroBat tree'; Ran = $false; Note = 'probe exe not present, run probe7c first' }
}

Remove-Item $localBat, $treeBat -Force -ErrorAction SilentlyContinue
Remove-Item $localDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ''
foreach ($r in $results) {
    $mark = if ($r.Ran) { 'RAN    ' } else { 'DID NOT' }
    $colour = if ($r.Ran) { 'Green' } else { 'Red' }
    Write-Host ("  {0}  {1,-36} {2}" -f $mark, $r.Test, $r.Note) -ForegroundColor $colour
}

Write-Host ''
Write-Host '=== verdict ===' -ForegroundColor Cyan

$local = ($results | Where-Object { $_.Test -like '*local disk*' }).Ran
$tree = ($results | Where-Object { $_.Test -like '*RetroBat tree' -and $_.Test -like '.bat*' }).Ran
$quoted = ($results | Where-Object { $_.Test -like '*quoted*' }).Ran
$exeRan = ($results | Where-Object { $_.Test -like '.exe*' }).Ran

if (-not $local) {
    Write-Host '  BROKEN: this PC cannot run a .bat at all. That explains every silent hook.' -ForegroundColor Red
    Write-Host '  Look at the Open With choice above; if it is present, that is the cause.' -ForegroundColor Red
}
elseif (-not $tree) {
    Write-Host '  The association is fine, but a .bat will not run from the RetroBat tree.' -ForegroundColor Red
    Write-Host '  Suspect antivirus or removable-media policy. host-report.txt has both.' -ForegroundColor Red
}
else {
    Write-Host '  .bat execution is healthy on this PC. The hook failure has another cause.' -ForegroundColor Green
}

if (-not $quoted) {
    Write-Host '  The quoted-argument test failing is EXPECTED everywhere. It is upstream bug #249.' -ForegroundColor DarkGray
}
else {
    Write-Host '  The quoted-argument test PASSED, which no machine has done yet. Worth reporting.' -ForegroundColor Yellow
}

if ($exeRan) { Write-Host '  The .exe hook runs here, which is what RomMBat needs.' -ForegroundColor Green }
else { Write-Host '  The .exe did not run. That would block RomMBat itself, so capture why.' -ForegroundColor Red }

Write-Host ''
