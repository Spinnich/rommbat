<#
.SYNOPSIS
  M0 probe 2: whether the per-game es_settings.cfg override is honoured, and whether it
  survives EmulationStation rewriting the file.

.DESCRIPTION
  The plan's durable lever for converting class-D shared save containers into per-game ones
  is the per-game form of es_settings.cfg:

    global.<key>  ->  <system>.<key>  ->  <system>["<rom filename>"].<key>

  Everything downstream of that (PCSX2 per-game cards, Flycast per-game VMU, and per-game
  decisions about whether to convert at all) assumes the last form works and stays written.
  Neither had been measured, so this probe measures both.

  Part 1, launcher precedence. Five launches driven straight at emulatorLauncher.exe, which
  is the process that reads es_settings.cfg and applies the precedence. `smooth` is used as
  the observable because it lands in the regenerated retroarch.cfg as `video_smooth`, so the
  result is read from disk rather than from the screen. The per-game value is deliberately
  the one that differs from the stock value, so "honoured" and "ignored" cannot both look
  like the baseline.

    A  no keys                                       launch B-rom    baseline
    B  <sys>.smooth=1                                launch B-rom    system scope works at all
    C  <sys>.smooth=1  <sys>["A-rom.ext"].smooth=0   launch A-rom    per-game beats system
    D  <sys>.smooth=1  <sys>["A-rom.ext"].smooth=0   launch B-rom    override does not leak
    E  <sys>.smooth=1  <sys>["B-rom"].smooth=0       launch B-rom    is the extension required
    F  <sys>.smooth=1  <sys>["B-rom.ext"].smooth=0   launch B-rom    E's pair, extension restored

  E and F differ only in the extension on the same rom, so the extension rule does not rest
  on comparing two different roms.

  Part 2, survival. ES only rewrites es_settings.cfg when a setting actually changed during
  the session, so a start-and-quit proves nothing: the file is simply not written. The run
  is forced dirty by pointing LastSystem at a system with no games, which makes ES fall back
  and rewrite. An unknown key is injected alongside the real one to separate "ES models the
  per-game form" from "ES preserves whatever it does not recognise".

  Needs EmulationStation closed at the start. Restores es_settings.cfg on the way out,
  including after a failure.

.EXAMPLE
  pwsh -File tools/m0-probes/probe2-per-game-override.ps1 -Root K:\RetroBat
  pwsh -File tools/m0-probes/probe2-per-game-override.ps1 -Root K:\RetroBat -SkipRestart
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [string] $System = 'ports',
    [string] $RomA = '2048.libretro',
    [string] $RomB = 'gong.libretro',
    [string] $Emulator = 'libretro',
    [switch] $SkipRestart
)

$ErrorActionPreference = 'Stop'

$esCfg = Join-Path $Root 'emulationstation\.emulationstation\es_settings.cfg'
$launcher = Join-Path $Root 'emulationstation\emulatorLauncher.exe'
$launcherDir = Split-Path $launcher
$raCfg = Join-Path $Root 'emulators\retroarch\retroarch.cfg'
$romDir = Join-Path $Root "roms\$System"
$backup = "$esCfg.probe2-backup"
$esApi = 'http://127.0.0.1:1234'

foreach ($p in @($esCfg, $launcher, $raCfg)) {
    if (-not (Test-Path $p)) { throw "not found: $p" }
}
if (Get-Process emulationstation -ErrorAction SilentlyContinue) {
    throw 'EmulationStation is running. Close it first; this probe rewrites es_settings.cfg.'
}

# Everything is layered onto a pristine copy, so a case never inherits the previous case's keys.
Copy-Item $esCfg $backup -Force

function Set-EsSettings {
    param([hashtable] $Keys = @{}, [string] $LastSystem)

    [xml] $xml = Get-Content $backup -Raw
    if ($LastSystem) {
        ($xml.config.string | Where-Object { $_.name -eq 'LastSystem' }).value = $LastSystem
    }
    foreach ($name in $Keys.Keys) {
        $node = $xml.CreateElement('string')
        $node.SetAttribute('name', $name)      # inner quotes serialise as &quot;, which is ES's own form
        $node.SetAttribute('value', $Keys[$name])
        $xml.config.AppendChild($node) | Out-Null
    }
    $xml.Save($esCfg)
}

function Get-RetroarchValue {
    param([string] $Key)
    $line = Select-String -Path $raCfg -Pattern "^$Key\s*=" | Select-Object -First 1
    if (-not $line) { return '<absent>' }
    ($line.Line -split '=', 2)[1].Trim().Trim('"')
}

# retroarch.cfg is regenerated at launch, so the value is read while the emulator is up,
# before anything the launcher does on exit can put the stock config back.
function Invoke-Launch {
    param([string] $Rom)

    $rom = Join-Path $romDir $Rom
    if (-not (Test-Path $rom)) { throw "rom not found: $rom" }

    $before = (Get-Item $raCfg).LastWriteTimeUtc
    # The rom path must reach emulatorLauncher quoted. Passed bare, a path containing a
    # space arrives as several arguments and the launcher reports "rom does not exist".
    Start-Process -FilePath $launcher -WorkingDirectory $launcherDir `
        -ArgumentList '-system', $System, '-emulator', $Emulator, '-rom', "`"$rom`""

    $deadline = (Get-Date).AddSeconds(45)
    while ((Get-Date) -lt $deadline -and (Get-Item $raCfg).LastWriteTimeUtc -eq $before) {
        Start-Sleep -Milliseconds 400
    }
    if ((Get-Item $raCfg).LastWriteTimeUtc -eq $before) { throw "retroarch.cfg never regenerated for $Rom" }
    Start-Sleep -Milliseconds 1200

    $value = Get-RetroarchValue 'video_smooth'
    Get-Process retroarch, emulatorLauncher -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 800
    $value
}

$perGameA = "$System[`"$RomA`"].smooth"
$perGameB = "$System[`"$RomB`"].smooth"
$perGameBnoExt = "$System[`"$([System.IO.Path]::GetFileNameWithoutExtension($RomB))`"].smooth"

# Named Overrides rather than Keys: $hashtable.Keys resolves to the hashtable's own key
# collection, not to a member called Keys.
$cases = @(
    @{ Id = 'A'; Overrides = @{}; Rom = $RomB; Expect = 'false'; Asks = 'baseline, no keys set' }
    @{ Id = 'B'; Overrides = @{ "$System.smooth" = '1' }; Rom = $RomB; Expect = 'true'; Asks = 'system-scoped key is honoured' }
    @{ Id = 'C'; Overrides = @{ "$System.smooth" = '1'; $perGameA = '0' }; Rom = $RomA; Expect = 'false'; Asks = 'per-game beats system' }
    @{ Id = 'D'; Overrides = @{ "$System.smooth" = '1'; $perGameA = '0' }; Rom = $RomB; Expect = 'true'; Asks = 'override stays scoped to its rom' }
    @{ Id = 'E'; Overrides = @{ "$System.smooth" = '1'; $perGameBnoExt = '0' }; Rom = $RomB; Expect = 'true'; Asks = 'basename without extension is ignored' }
    @{ Id = 'F'; Overrides = @{ "$System.smooth" = '1'; $perGameB = '0' }; Rom = $RomB; Expect = 'false'; Asks = 'same rom as E, extension restored' }
)

$results = @()

try {
    Write-Host '=== part 1: is the per-game override honoured by emulatorLauncher ===' -ForegroundColor Cyan
    Write-Host ''

    foreach ($case in $cases) {
        Set-EsSettings -Keys $case.Overrides
        $actual = Invoke-Launch -Rom $case.Rom
        $pass = $actual -eq $case.Expect
        $results += [pscustomobject]@{
            Case     = $case.Id
            Rom      = $case.Rom
            Keys     = if ($case.Overrides.Count) { ($case.Overrides.Keys | Sort-Object) -join ' + ' } else { '(none)' }
            Expected = $case.Expect
            Actual   = $actual
            Verdict  = if ($pass) { 'as expected' } else { 'UNEXPECTED' }
            Asks     = $case.Asks
        }
        $colour = if ($pass) { 'Green' } else { 'Red' }
        Write-Host ("  {0}  {1,-16} video_smooth={2,-7} expected {3,-7} {4}" -f `
                $case.Id, $case.Rom, $actual, $case.Expect, $case.Asks) -ForegroundColor $colour
    }

    if (-not $SkipRestart) {
        Write-Host ''
        Write-Host '=== part 2: does the key survive ES rewriting es_settings.cfg ===' -ForegroundColor Cyan
        Write-Host ''

        $survivalKeys = @{
            "$System.smooth"                                = '1'
            $perGameA                                       = '0'
            "$System[`"$RomA`"].rommbat_probe_unknown"      = 'zzz'
        }

        # Two sessions: a clean one to show ES leaves the file alone, then a forced-dirty one
        # to show what it does when it actually serialises.
        foreach ($session in @(
                @{ Name = 'clean session'; Last = $null },
                @{ Name = 'forced-dirty session (LastSystem points at an empty system)'; Last = 'snes' })) {

            Set-EsSettings -Keys $survivalKeys -LastSystem $session.Last
            $written = (Get-Item $esCfg).LastWriteTimeUtc

            Start-Process -FilePath (Join-Path $Root 'emulationstation\emulationstation.exe') -WorkingDirectory (Join-Path $Root 'emulationstation')
            $deadline = (Get-Date).AddSeconds(90)
            $up = $false
            while ((Get-Date) -lt $deadline) {
                try { Invoke-WebRequest -Uri "$esApi/caps" -TimeoutSec 2 | Out-Null; $up = $true; break }
                catch { Start-Sleep -Milliseconds 1000 }
            }
            if (-not $up) { throw 'EmulationStation did not answer on 127.0.0.1:1234' }

            Start-Sleep -Seconds 5
            Invoke-WebRequest -Uri "$esApi/quit" -TimeoutSec 5 | Out-Null
            $deadline = (Get-Date).AddSeconds(60)
            while ((Get-Date) -lt $deadline -and (Get-Process emulationstation -ErrorAction SilentlyContinue)) {
                Start-Sleep -Milliseconds 500
            }
            Start-Sleep -Seconds 2

            $rewrote = (Get-Item $esCfg).LastWriteTimeUtc -ne $written
            $kept = @($survivalKeys.Keys | Where-Object {
                    Select-String -Path $esCfg -SimpleMatch -Pattern ('name="' + ($_ -replace '"', '&quot;') + '"') -Quiet
                })

            Write-Host ("  {0}" -f $session.Name) -ForegroundColor Yellow
            Write-Host ("    ES rewrote the file : {0}" -f $rewrote)
            Write-Host ("    probe keys surviving: {0} of {1}" -f $kept.Count, $survivalKeys.Count) `
                -ForegroundColor $(if ($kept.Count -eq $survivalKeys.Count) { 'Green' } else { 'Red' })

            $results += [pscustomobject]@{
                Case = $session.Name; Rom = '-'; Keys = 'survival'
                Expected = 'all keys kept'
                Actual = "rewrote=$rewrote kept=$($kept.Count)/$($survivalKeys.Count)"
                Verdict = if ($kept.Count -eq $survivalKeys.Count) { 'as expected' } else { 'UNEXPECTED' }
                Asks = 'ES round-trips the per-game form and unknown keys'
            }
        }
    }
} finally {
    Copy-Item $backup $esCfg -Force
    Remove-Item $backup -Force -ErrorAction SilentlyContinue
    Get-Process retroarch, emulatorLauncher -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Host ''
    Write-Host "restored $esCfg" -ForegroundColor DarkGray
}

Write-Host ''
$results | Format-Table Case, Rom, Expected, Actual, Verdict, Asks -AutoSize -Wrap
