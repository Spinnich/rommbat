<#
.SYNOPSIS
  M0 probe 7b: snapshot everything a session leaves behind, plus the host facts that
  could explain a hook that never ran.

.DESCRIPTION
  Run this on the host that just finished a RetroBat session, before RetroBat is started
  anywhere again. Three of the four artifacts do not survive the next launch:
  es_log.txt rotates through es_log.0-3.txt, emulatorLauncher.log rotates to .old, and
  RetroBat.log is overwritten outright. Losing them is why probe 7's failure could not be
  diagnosed after the fact.

  Copies into <root>\rommbat-probe\collect-<host>-<stamp>\:

    es_log*.txt              did ES fire the event, and which scripts directory did it resolve
    RetroBat.log             the exact ES command line, including whether --home was passed
    emulatorLauncher.log*    what actually launched, with millisecond timestamps
    es_settings.cfg          confirms LogLevel survived the session
    diag-*.log               the probe hooks' own records, from the tree and from %TEMP%

  And writes host-report.txt: identity, the .bat association, execution policy, AV and
  Defender ASR state, AppLocker and SRP policy, removable-media policy, and any Defender
  or AppLocker block events from the last day.

  Read-only apart from the collection directory it creates. Some sections need an elevated
  shell and say so rather than failing.

.EXAMPLE
  pwsh -File tools/m0-probes/probe7b-collect.ps1 -Root K:\RetroBat -Label second-host
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [string] $Label = ''
)

$ErrorActionPreference = 'Continue'

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$name = if ($Label) { "collect-$env:COMPUTERNAME-$Label-$stamp" } else { "collect-$env:COMPUTERNAME-$stamp" }
$dest = Join-Path (Join-Path $Root 'rommbat-probe') $name
New-Item -ItemType Directory -Force -Path $dest | Out-Null

$es = Join-Path $Root 'emulationstation\.emulationstation'
$sources = @(
    (Join-Path $es 'es_log*.txt')
    (Join-Path $es 'es_settings.cfg')
    (Join-Path $es 'es_launch_stdout.log')
    (Join-Path $Root 'emulationstation\emulatorLauncher.log*')
    (Join-Path $Root 'RetroBat.log')
    (Join-Path $Root 'rommbat-probe\diag-*.log')
    (Join-Path $Root 'rommbat-probe\hooks.log')
    (Join-Path $env:TEMP 'rommbat-diag-*.log')
)

foreach ($src in $sources) {
    Get-ChildItem -Path $src -ErrorAction SilentlyContinue | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $dest $_.Name) -Force -ErrorAction SilentlyContinue
        Write-Host "collected $($_.Name) ($($_.Length) bytes, $($_.LastWriteTime))"
    }
}

function Section {
    param([string] $Title, [scriptblock] $Body)

    "", "=== $Title ===" | Add-Content -Path $report
    try { & $Body 2>&1 | Out-String -Width 200 | Add-Content -Path $report }
    catch { "  FAILED: $($_.Exception.Message)" | Add-Content -Path $report }
}

$report = Join-Path $dest 'host-report.txt'
"probe 7b host report  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" | Set-Content -Path $report

$elevated = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

Section 'identity' {
    [pscustomobject]@{
        Computer  = $env:COMPUTERNAME
        User      = $env:USERNAME
        Elevated  = $elevated
        OS        = (Get-CimInstance Win32_OperatingSystem).Caption
        Build     = [Environment]::OSVersion.Version.ToString()
        TimeZone  = (Get-TimeZone).Id
        Root      = $Root
        RootDrive = (Get-Item $Root).PSDrive.Name
    } | Format-List
}

Section 'volume' {
    $letter = (Get-Item $Root).PSDrive.Name
    Get-Volume -DriveLetter $letter | Format-List DriveLetter, FileSystemLabel, FileSystem, DriveType, Size, SizeRemaining
    [System.IO.DriveInfo]::new("$letter`:") | Format-List Name, DriveType, DriveFormat, IsReady
}

Section 'environment' {
    [pscustomobject]@{
        COMSPEC     = $env:COMSPEC
        HOME        = $env:HOME
        USERPROFILE = $env:USERPROFILE
        TEMP        = $env:TEMP
        PATHEXT     = $env:PATHEXT
    } | Format-List
}

# ES runs a .bat through the shell rather than CreateProcess, so a hijacked or missing
# association is a way for a hook to vanish with no error anywhere.
Section 'bat association' {
    foreach ($key in @(
            'Registry::HKEY_CLASSES_ROOT\.bat'
            'Registry::HKEY_CLASSES_ROOT\batfile\shell\open\command'
            'Registry::HKEY_CLASSES_ROOT\.cmd'
            'HKCU:\Software\Classes\.bat'
            'HKCU:\Software\Classes\batfile\shell\open\command'
            'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.bat\UserChoice'
        )) {
        "$key"
        if (Test-Path $key) { Get-ItemProperty $key | Format-List * } else { "  (absent)" }
    }
}

# RetroBat.exe stamps this per user profile and it is what passes --home to ES. Absent,
# under a profile that has run RetroBat, means ES was started some other way, and ES then
# resolves its scripts directory under the user profile instead of the tree.
Section 'retrobat registry, did RetroBat.exe run under this profile' {
    $key = 'HKCU:\Software\RetroBat'
    if (Test-Path $key) { Get-ItemProperty $key | Format-List * } else { '  (absent, RetroBat.exe has never run for this user)' }
    'HOME sources:'
    "  process HOME = '$env:HOME'"
    "  HKCU\Environment HOME = '$((Get-ItemProperty 'HKCU:\Environment' -ErrorAction SilentlyContinue).HOME)'"
    "  machine HOME = '$((Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment' -ErrorAction SilentlyContinue).HOME)'"
    'scripts directory ES would use without --home:'
    "  $env:USERPROFILE\.emulationstation\scripts"
    if (Test-Path "$env:USERPROFILE\.emulationstation") {
        '  EXISTS. ES has run against the user profile on this host:'
        Get-ChildItem "$env:USERPROFILE\.emulationstation" -Recurse -Depth 2 -ErrorAction SilentlyContinue |
            Select-Object FullName, Length, LastWriteTime | Format-Table -AutoSize
    }
    else { '  (absent)' }
}

# PSExecutionPolicyPreference is inherited by child shells, so asking powershell.exe from
# an already-bypassed session reports Bypass and hides the real machine policy.
Section 'powershell execution policy' {
    Get-ExecutionPolicy -List | Format-Table -AutoSize
    $saved = $env:PSExecutionPolicyPreference
    $env:PSExecutionPolicyPreference = $null
    "windows powershell, uncontaminated (what ES invokes): " + (& powershell.exe -NoProfile -Command 'Get-ExecutionPolicy' 2>&1)
    $env:PSExecutionPolicyPreference = $saved
}

Section 'antivirus products' {
    Get-CimInstance -Namespace 'root/SecurityCenter2' -ClassName AntiVirusProduct |
        Format-Table displayName, productState, pathToSignedProductExe -AutoSize
}

Section 'defender preferences' {
    $p = Get-MpPreference
    [pscustomobject]@{
        RealtimeDisabled  = $p.DisableRealtimeMonitoring
        ScriptScanDisable = $p.DisableScriptScanning
        ExclusionPath     = ($p.ExclusionPath -join '; ')
        ExclusionProcess  = ($p.ExclusionProcess -join '; ')
    } | Format-List

    'attack surface reduction rules:'
    for ($i = 0; $i -lt $p.AttackSurfaceReductionRules_Ids.Count; $i++) {
        "  $($p.AttackSurfaceReductionRules_Ids[$i]) = $($p.AttackSurfaceReductionRules_Actions[$i])"
    }
    if (-not $p.AttackSurfaceReductionRules_Ids) { '  (none configured)' }
}

Section 'applocker and srp' {
    if ($elevated) { Get-AppLockerPolicy -Effective -Xml } else { '  needs an elevated shell' }
    'SRP (Safer):'
    $safer = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Safer\CodeIdentifiers'
    if (Test-Path $safer) { Get-ItemProperty $safer | Format-List * } else { '  (absent)' }
}

Section 'removable media policy' {
    $rs = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices'
    if (Test-Path $rs) { Get-ChildItem $rs -Recurse | ForEach-Object { $_.Name; Get-ItemProperty $_.PSPath | Format-List * } }
    else { '  (absent, no removable-storage restriction policy)' }
}

# An empty result here is a real answer, so it must not read like a failed query.
Section 'defender block events, last day' {
    $ev = Get-WinEvent -FilterHashtable @{
        LogName   = 'Microsoft-Windows-Windows Defender/Operational'
        Id        = 1116, 1117, 1121, 1122
        StartTime = (Get-Date).AddDays(-1)
    } -ErrorAction SilentlyContinue
    if ($ev) { $ev | Select-Object TimeCreated, Id, Message | Format-List } else { '  (no matching events)' }
}

Section 'applocker block events, last day' {
    $ev = Get-WinEvent -FilterHashtable @{
        LogName   = 'Microsoft-Windows-AppLocker/MSI and Script'
        StartTime = (Get-Date).AddDays(-1)
    } -ErrorAction SilentlyContinue
    if ($ev) { $ev | Select-Object TimeCreated, Id, Message | Format-List } else { '  (no matching events)' }
}

# The single most useful line in the collection: whether ES reached its scripting code,
# and which directory it resolved when it did.
Section 'es_log scripting lines' {
    Get-ChildItem (Join-Path $dest 'es_log*.txt') -ErrorAction SilentlyContinue |
        ForEach-Object {
            "--- $($_.Name) ---"
            Select-String -Path $_.FullName -Pattern 'fireEvent|queuing|executing|scripts' |
                ForEach-Object { $_.Line }
        }
}

Section 'RetroBat.log launch line' {
    Select-String -Path (Join-Path $dest 'RetroBat.log') -Pattern 'Launching|--home|es_settings' -ErrorAction SilentlyContinue |
        ForEach-Object { $_.Line }
}

Write-Host ''
Write-Host "collection: $dest"
if (-not $elevated) { Write-Host 'note: not elevated, AppLocker policy and some event logs were skipped.' -ForegroundColor Yellow }
