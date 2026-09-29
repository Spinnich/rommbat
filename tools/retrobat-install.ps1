#Requires -Version 7

<#
.SYNOPSIS
    Stands up a pristine RetroBat from an upstream GitHub release, for development.

.DESCRIPTION
    Resolves a release of RetroBat-Official/retrobat, downloads its win64 setup.exe into a
    cache shared by every worktree, checks it against the .sha256.txt published beside it, and
    extracts it into an empty directory.

    The setup.exe is a .NET WinForms wizard with no silent mode. Its payload is a ZIP appended
    to the executable, followed by the ZIP's length as a little-endian int64, and the wizard
    does nothing to the tree beyond extracting that ZIP. So this reads the ZIP in place rather
    than driving the wizard. What it skips is the wizard's optional, system-wide prerequisite
    installers, which it reports on instead.

    A dev tool only. RomMBat never installs RetroBat for a user.

.PARAMETER Version
    latest (the default) is the newest stable release. prerelease is the newest release of any
    kind, which is a beta whenever one is ahead of the stable. Anything else is an exact release
    tag, such as 8.2.1 or beta_8.3.0.

.PARAMETER Path
    Where to install. It must not exist, or be empty, because the wizard refuses a non-empty
    folder and a tree extracted over another is not pristine.

.PARAMETER CacheDir
    Where downloaded installers are kept, about 1.9 GB each. Nothing prunes it.

.EXAMPLE
    ./tools/retrobat-install.ps1 -Path D:\retrobat-pristine
    ./tools/retrobat-install.ps1 -Version prerelease -Path D:\retrobat-beta
    ./tools/retrobat-install.ps1 -Version 8.2.1 -Path D:\retrobat-8.2.1
#>

[CmdletBinding()]
param(
    [string] $Version = 'latest',
    [Parameter(Mandatory)] [string] $Path,
    [string] $CacheDir = (Join-Path $env:LOCALAPPDATA 'rommbat-dev\retrobat-installers')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = 'RetroBat-Official/retrobat'
# Against PowerShell's location, not the process directory, which cd does not move.
$Path = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($Path)
$CacheDir = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($CacheDir)

# Checked before the download, so a wrong path costs nothing.
if ((Test-Path -LiteralPath $Path) -and (Get-ChildItem -LiteralPath $Path -Force | Select-Object -First 1)) {
    throw "$Path is not empty. Remove it or choose another path; a pristine tree is extracted into an empty one."
}

if ($Path -match '\s') {
    Write-Warning "$Path contains a space. RetroBat's own installer warns against that, and some emulators fail under it."
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'The GitHub CLI (gh) is needed to resolve the release. See DEVELOPER_SETUP.md, section 1.'
}

function Get-Release([string] $Version) {
    if ($Version -notin 'latest', 'prerelease') {
        $json = gh api "repos/$repo/releases/tags/$Version" 2>$null
        if ($LASTEXITCODE -ne 0) { throw "No release tagged '$Version' in $repo. Tags look like 8.2.1 or beta_8.3.0." }
        return $json | ConvertFrom-Json
    }

    $json = gh api "repos/$repo/releases?per_page=30"
    if ($LASTEXITCODE -ne 0) { throw "Could not list the releases of $repo." }

    $releases = $json | ConvertFrom-Json |
        Where-Object { -not $_.draft -and ($Version -eq 'prerelease' -or -not $_.prerelease) } |
        Sort-Object { [datetime] $_.published_at } -Descending

    if (-not $releases) { throw "No $Version release found in $repo." }
    return $releases | Select-Object -First 1
}

$release = Get-Release $Version
$kind = if ($release.prerelease) { 'prerelease' } else { 'stable' }
Write-Host "Release: $($release.name) (tag $($release.tag_name), $kind, published $(([datetime] $release.published_at).ToString('yyyy-MM-dd')))"

# Upstream names neither tags nor assets regularly (8.0 shipped RetroBat-v8.0.0.0-...), so the
# asset is found by its suffix rather than built from the tag.
$setup = @($release.assets | Where-Object { $_.name -like '*-win64-setup.exe' })
if ($setup.Count -ne 1) {
    throw "Expected one *-win64-setup.exe asset in $($release.tag_name), found $($setup.Count)."
}
$setup = $setup[0]

$hashAsset = $release.assets | Where-Object { $_.name -eq "$($setup.name).sha256.txt" }
if (-not $hashAsset) {
    throw "$($release.tag_name) publishes no $($setup.name).sha256.txt, so the download cannot be checked."
}

$expected = (Invoke-RestMethod $hashAsset.browser_download_url).Trim().Split()[0].ToUpperInvariant()
if ($expected -notmatch '^[0-9A-F]{64}$') {
    throw "$($hashAsset.name) does not hold a sha256: '$expected'."
}

# Not New-Item, which reads [ and ] in a path as a wildcard.
[System.IO.Directory]::CreateDirectory($CacheDir) | Out-Null
$installer = Join-Path $CacheDir $setup.name
$partial = "$installer.partial"

function Test-Installer([string] $File) {
    (Get-Item -LiteralPath $File).Length -eq $setup.size -and
        (Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash -eq $expected
}

if ((Test-Path -LiteralPath $installer) -and (Test-Installer $installer)) {
    Write-Host "Cached: $installer"
}
else {
    Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue

    # curl.exe ships with Windows and resumes an interrupted download, which Invoke-WebRequest
    # does not; at 1.9 GB that matters.
    Write-Host ("Downloading {0} ({1:N2} GB) to {2}" -f $setup.name, ($setup.size / 1GB), $CacheDir)
    curl.exe --fail --location --continue-at - --output $partial $setup.browser_download_url
    if ($LASTEXITCODE -ne 0) { throw "curl failed with exit code $LASTEXITCODE. Run again to resume." }

    if (-not (Test-Installer $partial)) {
        Remove-Item -LiteralPath $partial -Force
        throw "$($setup.name) does not match $($hashAsset.name). The partial download was deleted; run again."
    }

    Move-Item -LiteralPath $partial $installer
}

Write-Host "Verified sha256 $expected"

# A bounded, seekable view of the ZIP inside the installer. ZipArchive reads offsets relative
# to the start of its stream, and the ZIP's own offsets are relative to the ZIP, not the exe.
if (-not ('RomMBatDev.SubStream' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;

namespace RomMBatDev
{
    public sealed class SubStream : Stream
    {
        private readonly Stream inner;
        private readonly long start;
        private readonly long length;
        private long position;

        public SubStream(Stream inner, long start, long length)
        {
            this.inner = inner;
            this.start = start;
            this.length = length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;

        public override long Position
        {
            get => position;
            set => position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long left = length - position;
            if (left <= 0) return 0;
            if (count > left) count = (int)left;
            inner.Position = start + position;
            int read = inner.Read(buffer, offset, count);
            position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => position + offset,
                _ => length + offset,
            };
            return position;
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
'@
}

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.ZipFile

$file = [System.IO.File]::OpenRead($installer)
try {
    # BinaryReader reads little-endian and fully, on any PowerShell 7; Stream.ReadExactly
    # needs 7.3.
    $reader = [System.IO.BinaryReader]::new($file, [System.Text.Encoding]::UTF8, $true)
    $file.Position = $file.Length - 8
    $zipLength = $reader.ReadInt64()
    $zipStart = $file.Length - 8 - $zipLength

    $magic = 0
    if ($zipStart -gt 0) {
        $file.Position = $zipStart
        $magic = $reader.ReadUInt32()
    }

    # The layout is RetroBat's own and undocumented, so a release that changes it fails here,
    # by name, rather than extracting garbage.
    if ($zipStart -le 0 -or $magic -ne 0x04034B50) {
        throw "$($setup.name) is not laid out as a ZIP followed by its int64 length. Install it by hand with the wizard."
    }

    $zip = [System.IO.Compression.ZipArchive]::new(
        [RomMBatDev.SubStream]::new($file, $zipStart, $zipLength),
        [System.IO.Compression.ZipArchiveMode]::Read)

    try {
        Write-Host ("Extracting {0:N0} entries to {1}" -f $zip.Entries.Count, $Path)
        [System.IO.Directory]::CreateDirectory($Path) | Out-Null
        [System.IO.Compression.ZipFileExtensions]::ExtractToDirectory($zip, $Path)
    }
    finally {
        $zip.Dispose()
    }
}
finally {
    $file.Dispose()
}

$versionInfo = Join-Path $Path 'system\version.info'
if (-not (Test-Path -LiteralPath $versionInfo)) {
    throw "Extracted, but $versionInfo is missing, so this is not a tree RomMBat can read."
}
Write-Host "Installed: system\version.info reads $((Get-Content -LiteralPath $versionInfo -Raw).Trim())"

# What the wizard's prerequisite page would have offered, checked the way it checks them. A
# machine that already runs RetroBat has these; a clean one may not.
$missing = @()
$vc = 'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes'
foreach ($arch in 'x64', 'x86') {
    $key = Get-ItemProperty "$vc\$arch" -ErrorAction SilentlyContinue
    if (-not $key) { $key = Get-ItemProperty "$($vc -replace 'SOFTWARE', 'SOFTWARE\WOW6432Node')\$arch" -ErrorAction SilentlyContinue }
    if (-not $key -or $key.Installed -ne 1) { $missing += "Visual C++ 2015-2022 $arch runtime" }
}
if (-not (Test-Path (Join-Path $env:WINDIR 'System32\XAudio2_7.dll'))) {
    $missing += 'DirectX 9 June 2010 redistributable'
}
if (-not ((Test-Path 'HKLM:\SYSTEM\CurrentControlSet\Services\dokan1') -or (Test-Path 'HKLM:\SYSTEM\CurrentControlSet\Services\dokan2'))) {
    $missing += 'Dokany (only for Xbox images under Cxbx-Reloaded)'
}
if (-not (Test-Path "${env:ProgramFiles(x86)}\WinFsp\bin\winfsp-x64.dll")) {
    $missing += 'WinFsp (only for SquashFS images)'
}

if ($missing) {
    Write-Warning ("Not found on this machine, and not installed by this script. Run the wizard at {0} and tick them on its prerequisites page:`n  {1}" -f $installer, ($missing -join "`n  "))
}
