<#
.SYNOPSIS
  Does RetroBat 8.2.1 mirror a Flycast save state into the directory es_savestates.cfg
  declares? One hands-on pass, to decide whether StateScanner keeps its flycast workaround.

.DESCRIPTION
  M0 measured that Flycast writes saves/dreamcast/reicast/states/ while es_savestates.cfg
  declares {{system}}/flycast/sstates, and that the declared directory exists and stays
  empty. That was filed as RetroBat-Official/emulatorlauncher#1336 and fixed in RetroBat
  8.2.1: commit 5fafcb2b pointed FlycastSaveStatesMonitor at saves/<system>/reicast/states
  instead of the emulator's own data directory, which it never writes states to.

  So the claim to test is narrow. Flycast still writes reicast/states first, and emu.cfg's
  Dreamcast.SavestatePath still names it; what should have changed is that the declared
  directory is now populated too, under RetroBat's own naming, the way PPSSPP and the other
  non-libretro emulators already behave.

  This wraps probe2-savestates.ps1, which does the real work (snapshot the whole
  saves/<system> subtree, drive a save, diff, match the declared <file> template), and then
  answers the one question 8.2.1 raises: did anything land under flycast/sstates, does its
  name match the declared template, and is the reicast/states copy still there. Both
  directories sit under saves/dreamcast, so the wrapped probe already watches them.

  Three outcomes, and each decides something:

    mirrored      a file under saves/dreamcast/flycast/sstates matching the declared
                  <file> template. The fix works. Remove "flycast" from
                  StateScanner.WrongDeclaredDirectories, drop the correction from
                  tools/m0-probes/probe2-emit-data.py, and record the pass in
                  docs/platforms/ and docs/retrobat-findings.md.

    not mirrored  only reicast/states got the state. The changelog line does not describe
                  what lands on disk. Keep the workaround, and reopen upstream with this
                  output.

    no state      the save key never took. Not a result. Check the key against
                  .emulationstation/es_padtokey.cfg and run it again.

  Read-only against the install apart from the state the run creates, which is removed again
  unless -KeepArtifacts is passed. The wrapped probe is always run with -KeepArtifacts,
  because it deletes what it created before returning and there would be nothing left here
  to look at; the cleanup below is this script's own.

  Needs a Dreamcast rom in roms/dreamcast with no existing save data, and a person at the
  machine: the save key is sent to the running emulator.

.EXAMPLE
  pwsh -File tools/m0-probes/probe2-flycast-mirror.ps1 -Root K:\RetroBat -Rom "Sample (USA).chd"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [Parameter(Mandatory)] [string] $Rom,
    [string] $SaveKey = '{F2}',
    [int] $BootSeconds = 60,
    [switch] $KeepArtifacts
)

$ErrorActionPreference = 'Stop'

$version = (Get-Content (Join-Path $Root 'system\version.info') -Raw).Trim()
Write-Host "RetroBat $version"
if ($version -notmatch '^8\.2\.1') {
    Write-Warning "This probe asks a question about 8.2.1. On $version the answer says nothing about the fix."
}

$declared = Join-Path $Root 'saves\dreamcast\flycast\sstates'
$native = Join-Path $Root 'saves\dreamcast\reicast\states'

# Both are reported before the run, because "the declared directory was already populated"
# and "the run populated it" are different answers and only the diff separates them.
foreach ($d in @($declared, $native)) {
    $n = @(Get-ChildItem $d -File -Force -ErrorAction SilentlyContinue).Count
    Write-Host ("before: {0,-4} file(s) in {1}" -f $n, $d.Substring($Root.Length).TrimStart('\'))
}

& (Join-Path $PSScriptRoot 'probe2-savestates.ps1') `
    -Root $Root -Emulator flycast -System dreamcast -Rom $Rom `
    -SaveKey $SaveKey -BootSeconds $BootSeconds -KeepArtifacts

# The wrapped probe reports against the declared directory already. This is the 8.2.1
# question stated on its own, so the outcome does not have to be read out of the diff.
# StartsWith, not -like: rom names routinely contain [ ], which -like reads as a wildcard.
$stem = [System.IO.Path]::GetFileNameWithoutExtension($Rom)
$mirrored = @(Get-ChildItem $declared -File -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name.StartsWith($stem, [StringComparison]::OrdinalIgnoreCase) })
$wrote = @(Get-ChildItem $native -File -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name.StartsWith($stem, [StringComparison]::OrdinalIgnoreCase) })

Write-Host ''
Write-Host '--- emulatorlauncher#1336, the 8.2.1 question ---'
Write-Host ("declared saves/dreamcast/flycast/sstates : {0} file(s)" -f $mirrored.Count)
$mirrored | ForEach-Object { Write-Host ("    {0}  {1} B" -f $_.Name, $_.Length) }
Write-Host ("native   saves/dreamcast/reicast/states  : {0} file(s)" -f $wrote.Count)
$wrote | ForEach-Object { Write-Host ("    {0}  {1} B" -f $_.Name, $_.Length) }

Write-Host ''
if ($mirrored.Count -gt 0) {
    Write-Host 'MIRRORED. The declared directory is usable on 8.2.1; the flycast workaround can come out.'
}
elseif ($wrote.Count -gt 0) {
    Write-Host 'NOT MIRRORED. The state exists but only natively. Keep the workaround and reopen #1336.'
}
else {
    Write-Host 'NO STATE. The save key never took, so this run decides nothing. Check the key and repeat.'
}

# Tracked by exact path, so an install's real saves are never touched.
if (-not $KeepArtifacts) {
    $created = @($mirrored) + @($wrote)
    foreach ($f in $created) { Remove-Item $f.FullName -Force -ErrorAction SilentlyContinue }
    if ($created.Count) { Write-Host "" ; Write-Host "removed $($created.Count) file(s) created by this probe" }
}
