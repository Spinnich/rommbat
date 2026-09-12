#Requires -Version 7

<#
.SYNOPSIS
    Publishes RomMBat and lays out the portable tree a RetroBat install needs.

.DESCRIPTION
    Three projects publish self-contained win-x64, and what a RetroBat install actually
    needs is seven files drawn from all three. This assembles them, refuses to package an
    incomplete set, and optionally extracts into a tree.

    Losing one of the four native libraries breaks the UI with no error a user can read,
    so the file manifest below is the point of this script rather than the zip.

.PARAMETER Deploy
    A RetroBat root to copy the seven files into. Local state in emulators/rommbat is left
    alone, so this is the safe way to redeploy over a paired install.

.PARAMETER NoPublish
    Lay out and package whatever the last publish left behind. This is how the incomplete-set
    refusal is exercised: delete a file from publish\ui and re-run with this, since a normal
    run republishes the file before the manifest check can see it is gone.

.EXAMPLE
    ./tools/publish.ps1
    ./tools/publish.ps1 -Deploy D:\retrobat-test
#>

[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $OutputPath,
    [string] $Deploy,
    [switch] $NoZip,
    [switch] $NoPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot 'publish'
}

# Checked before the publish rather than after it, so a wrong path costs no build. The same
# markers RootMarkers.IsRoot uses, for the same reason: pointing at the wrong directory and
# quietly building a tree there is worse than being told the path is not a RetroBat install.
if ($Deploy) {
    $isRoot = (Test-Path (Join-Path $Deploy 'retrobat.ini')) -or
              ((Test-Path (Join-Path $Deploy 'emulationstation')) -and (Test-Path (Join-Path $Deploy 'roms')))

    if (-not $isRoot) {
        throw "$Deploy is not a RetroBat install. Expected retrobat.ini, or both emulationstation\ and roms\."
    }
}

# The three publishes, at the paths .github/workflows/build.yml already uses. CI calls this
# script, so there is one copy of these arguments rather than two that drift.
$projects = @(
    @{ Name = 'agent'; Path = 'src/RomMBat.Agent' }
    @{ Name = 'ui';    Path = 'src/RomMBat.UI' }
    @{ Name = 'hook';  Path = 'src/RomMBat.Hook' }
)

# What an install needs, and where each file comes from. The agent and the UI both carry
# e_sqlite3.dll and the two are byte-identical, so one copy in a shared directory serves
# both. Naming files explicitly is what keeps the .pdb files out: a publish emits 101 MB of
# them beside the 185 MB payload, and a wildcard copy ships the lot.
$manifest = @(
    @{ File = 'rommbat-agent.exe';     From = 'agent' }
    @{ File = 'RomMBat.exe';           From = 'ui' }
    @{ File = 'rommbat-hook.exe';      From = 'hook' }
    @{ File = 'e_sqlite3.dll';         From = 'ui' }
    @{ File = 'libSkiaSharp.dll';      From = 'ui' }
    @{ File = 'av_libglesv2.dll';      From = 'ui' }
    @{ File = 'libHarfBuzzSharp.dll';  From = 'ui' }
)

if (-not $NoPublish) {
    foreach ($project in $projects) {
        $target = Join-Path $OutputPath $project.Name
        Write-Host "Publishing $($project.Path) to $target"

        # Cleaned rather than published over. Removing one native library from a warm output
        # directory makes the next publish consider its copy step up to date and skip every
        # native, and it still reports success: measured, deleting libSkiaSharp.dll alone left
        # all four missing. The manifest check below would catch it, but a publish that quietly
        # drops the natives is worth not producing in the first place.
        if (Test-Path $target) {
            Remove-Item $target -Recurse -Force
        }

        dotnet publish (Join-Path $repoRoot $project.Path) `
            -c $Configuration -r win-x64 --self-contained `
            -p:PublishSingleFile=true `
            -o $target

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed for $($project.Path) with exit code $LASTEXITCODE."
        }
    }
}

# The zip is extracted at the RetroBat root, so it carries the emulators\rommbat prefix rather
# than seven bare filenames: RetroBatInstall.AppDirectory pins the app to that directory and
# hooks install looks for the hook there, so a flat archive extracts to a tree whose ES menu
# entry cannot resolve its executable.
$staging = Join-Path $OutputPath 'staging'
$layout = Join-Path $staging 'emulators\rommbat'
$zip = Join-Path $OutputPath 'rommbat-win-x64.zip'

if (Test-Path $staging) {
    Remove-Item $staging -Recurse -Force
}

# A stale zip goes with the stale layout, before anything can fail. Left in place, a run that
# refuses an incomplete set still leaves an archive under the name a release is cut from.
if (Test-Path $zip) {
    Remove-Item $zip -Force
}

New-Item -ItemType Directory -Path $layout -Force | Out-Null

$missing = @()

foreach ($entry in $manifest) {
    $source = Join-Path (Join-Path $OutputPath $entry.From) $entry.File

    if (-not (Test-Path $source)) {
        $missing += "$($entry.File) (expected from the $($entry.From) publish)"
        continue
    }

    if ((Get-Item $source).Length -eq 0) {
        $missing += "$($entry.File) is empty"
        continue
    }

    Copy-Item $source (Join-Path $layout $entry.File) -Force
}

# Refused rather than zipped. A package short one native library installs cleanly and then
# fails at launch, which costs far more to diagnose than a build that stops here.
if ($missing.Count -gt 0) {
    throw "The publish is incomplete, so nothing was packaged:`n  " + ($missing -join "`n  ")
}

$bytes = (Get-ChildItem $layout -File | Measure-Object -Property Length -Sum).Sum
Write-Host ("Laid out {0} files, {1:N1} MB, in {2}" -f $manifest.Count, ($bytes / 1MB), $layout)

if (-not $NoZip) {
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip
    Write-Host "Packaged $zip"
}

if (-not $Deploy) {
    return
}

# Forced by RetroBatInstall.AppDirectory, not a free choice: a system\es_menu entry resolves
# its executable under emulators\ and emulatorLauncher refuses a ..\ escape.
$destination = Join-Path $Deploy 'emulators\rommbat'
New-Item -ItemType Directory -Path $destination -Force | Out-Null

foreach ($entry in $manifest) {
    $target = Join-Path $destination $entry.File

    try {
        Copy-Item (Join-Path $layout $entry.File) $target -Force
    }
    catch [System.IO.IOException] {
        throw "Could not replace $($entry.File). Close EmulationStation and RomMBat.exe, then run this again."
    }
}

# Only the seven are written, so rommbat.db and device.id survive a redeploy and the install
# stays paired.
Write-Host "Deployed $($manifest.Count) files to $destination"
Write-Host "Local state in that directory was left alone, so the install stays paired."
