<#
.SYNOPSIS
  M4 probe 6: whether a media path built from a RomM name can be written at all.

.DESCRIPTION
  M4 constructs file names from names the server gave it: <rom stem>-marquee.png under
  roms/<system>/images/. Two things can stop that write, and both have to be handled before
  it rather than at the failed write.

  Length. The longest fs_name in a real library sample is 156 characters, and a media name
  adds a suffix on top of a path that already has roms/<system>/images/ in it. Whether
  260 characters is the ceiling depends on the machine: Windows 10 1607 and later can lift
  it with LongPathsEnabled, and .NET follows the OS.

  Characters. A RomM library is served from Linux, where : ? * " < > | and \ are all legal
  in a file name and none of them is legal in a Windows path.

  Everything it writes goes under a single temporary directory inside the tree and is
  removed at the end.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [string] $OutFile
)

$ErrorActionPreference = 'Continue'

$log = [Collections.Generic.List[string]]::new()
function Say([string] $Text) { $log.Add($Text); Write-Host $Text }

$scratch = Join-Path $Root 'emulators\rommbat\m4probe6'
New-Item -ItemType Directory -Path $scratch -Force | Out-Null

Say "=== M4 probe 6: constructed media paths on $Root ==="

$key = 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem'
$longPaths = (Get-ItemProperty -Path $key -Name LongPathsEnabled -ErrorAction SilentlyContinue).LongPathsEnabled
Say ("  LongPathsEnabled: {0}" -f $(if ($null -eq $longPaths) { 'not set (0)' } else { $longPaths }))
Say ("  scratch root is {0} characters" -f $scratch.Length)

Say ''
Say '--- one long file name, which is the shape a long rom name produces'
foreach ($nameLength in 100, 200, 250, 255, 256, 260) {
    $name = ('a' * ($nameLength - 4)) + '.png'
    $path = Join-Path $scratch $name
    $plain = $null
    $prefixed = $null
    try { [IO.File]::WriteAllBytes($path, [byte[]]::new(4)); $plain = 'ok' } catch { $plain = $_.Exception.InnerException.GetType().Name }
    try { [IO.File]::WriteAllBytes("\\?\$path", [byte[]]::new(4)); $prefixed = 'ok' } catch { $prefixed = $_.Exception.InnerException.GetType().Name }
    Say ("  name {0,4} chars, path {1,4}: plain={2,-22} with \\?\ prefix={3}" -f $name.Length, $path.Length, $plain, $prefixed)
    Remove-Item "\\?\$path" -Force -ErrorAction SilentlyContinue
}

Say ''
Say '--- a long total path built from short names, which is the shape a deep install produces'
$deep = $scratch
foreach ($depth in 1..12) {
    $deep = Join-Path $deep ('d' * 20)
    $file = Join-Path $deep 'probe-image.png'
    $plain = $null
    try {
        New-Item -ItemType Directory -Path $deep -Force -ErrorAction Stop | Out-Null
        [IO.File]::WriteAllBytes($file, [byte[]]::new(4))
        $plain = 'ok'
    } catch { $plain = $_.Exception.InnerException.GetType().Name }
    Say ("  depth {0,2}, path {1,4} chars: {2}" -f $depth, $file.Length, $plain)
    if ($plain -ne 'ok') { break }
}
Remove-Item (Join-Path $scratch ('d' * 20)) -Recurse -Force -ErrorAction SilentlyContinue

Say ''
Say '--- characters that are legal in a RomM file name and not in a Windows one'
foreach ($ch in '<', '>', ':', '"', '/', '\', '|', '?', '*') {
    $name = "probe${ch}name.png"
    $path = Join-Path $scratch $name
    $result = $null
    try {
        [IO.File]::WriteAllBytes($path, [byte[]]::new(4))
        # A colon does not fail: it opens an NTFS alternate data stream, so the bytes land
        # somewhere no directory listing shows and the file the gamelist names is not there.
        $listed = (Get-ChildItem $scratch -File -Force -ErrorAction SilentlyContinue | ForEach-Object { $_.Name }) -join ', '
        $result = "written, and the directory now lists: $listed"
    } catch { $result = $_.Exception.InnerException.GetType().Name }
    Say ("  {0}  -> {1}" -f $ch, $result)
    Get-ChildItem $scratch -File -Force -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -LiteralPath (Join-Path $scratch $_.Name) -Force -ErrorAction SilentlyContinue
    }
}

Say ''
Say '--- a trailing dot or space, which Windows silently strips'
foreach ($name in 'probe-trailing-dot..png', 'probe-trailing-space .png', 'probe-ends-with-dot.', 'probe-ends-with-space ') {
    $path = Join-Path $scratch $name
    try {
        [IO.File]::WriteAllBytes($path, [byte[]]::new(4))
        $landed = (Get-ChildItem $scratch -File | Where-Object { $_.Name -like 'probe-*' } | Select-Object -First 1).Name
        Say ("  {0,-28} -> landed as {1}" -f $name, $landed)
    } catch {
        Say ("  {0,-28} -> {1}" -f $name, $_.Exception.GetType().Name)
    }
    Get-ChildItem $scratch -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}

Say ''
Say '--- reserved device names, which Windows refuses whatever the extension'
foreach ($name in 'CON.png', 'nul-image.png', 'PRN.png', 'COM1.png', 'LPT9.png') {
    $path = Join-Path $scratch $name
    try { [IO.File]::WriteAllBytes($path, [byte[]]::new(4)); Say ("  {0,-16} -> written" -f $name) }
    catch { Say ("  {0,-16} -> {1}" -f $name, $_.Exception.GetType().Name) }
    Remove-Item $path -Force -ErrorAction SilentlyContinue
}

Remove-Item $scratch -Recurse -Force -ErrorAction SilentlyContinue

if ($OutFile) {
    New-Item -ItemType Directory -Path (Split-Path $OutFile) -Force | Out-Null
    $log -join "`n" | Set-Content -Path $OutFile -Encoding utf8
    Write-Host "wrote $OutFile"
}
