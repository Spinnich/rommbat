<#
.SYNOPSIS
  M0 probe 7, remaining item: what FAT32 and exFAT actually do to the two things the sync
  design leans on, file size and modification time.

.DESCRIPTION
  The portable move test ran on an NTFS stick, so core principle 4's two filesystem
  constraints were designed around but never measured. Both reach into the sync logic:

    4 GB ceiling    FAT32 cannot hold a file larger than 4 GB, and plenty of PS2, GameCube
                    and Wii images exceed it. RomMBat has to detect this before a sync set
                    resolves, not discover it mid-write, so the probe records how the write
                    fails, where it stops, and what the exception carries.
    mtime           FAT32 stores modification time with 2-second granularity and exFAT with
                    10 ms, against NTFS's 100 ns. Conflict logic that leans on mtime
                    equality gets both false matches and spurious conflicts, so the probe
                    measures the real quantisation by writing known values and reading them
                    back.

  It also checks the FAT local-time trap: FAT stores wall-clock local time rather than UTC,
  so a timestamp can shift by an hour across a DST boundary while the file is untouched. A
  round trip through a winter date and a summer date says whether that is live here.

  Read-only against whatever is already on the volume: everything is written into one
  probe directory, which is removed afterwards. It does not format anything.

.EXAMPLE
  pwsh -File tools/m0-probes/probe7-filesystem.ps1 -Drive X:
  pwsh -File tools/m0-probes/probe7-filesystem.ps1 -Drive X: -SkipLargeFile
  pwsh -File tools/m0-probes/probe7-filesystem.ps1 -Drive X: -Compare C: -OutJson probe-output/probe7-fs.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Drive,
    [string] $Compare,
    [switch] $SkipLargeFile,
    [string] $OutJson
)

$ErrorActionPreference = 'Stop'

function Get-VolumeFacts([string] $Root) {
    $letter = $Root.TrimEnd('\', ':')
    $di = [IO.DriveInfo]::new($Root)
    $vol = Get-Volume -DriveLetter $letter -ErrorAction SilentlyContinue
    $part = Get-Partition -DriveLetter $letter -ErrorAction SilentlyContinue
    [pscustomobject]@{
        drive            = "$letter`:"
        drive_format     = $di.DriveFormat        # what RomMBat would read
        drive_type       = "$($di.DriveType)"
        volume_fs        = if ($vol) { $vol.FileSystem } else { $null }
        allocation_bytes = if ($vol) { $vol.AllocationUnitSize } else { $null }
        size_gb          = [math]::Round($di.TotalSize / 1GB, 2)
        free_gb          = [math]::Round($di.AvailableFreeSpace / 1GB, 2)
        bus              = if ($part) { (Get-Disk -Number $part.DiskNumber -ErrorAction SilentlyContinue).BusType } else { $null }
    }
}

# The measurement that matters for conflict handling: write a known time, read it back, and
# report the error. Offsets are chosen to straddle the FAT 2 s and exFAT 10 ms boundaries.
function Measure-MtimeGranularity([string] $Dir) {
    $rows = @()
    $base = [DateTime]::new(2026, 6, 15, 13, 45, 0, [DateTimeKind]::Local)
    $offsets = @(0, 1, 10, 100, 500, 999, 1000, 1500, 1999, 2000, 3000)
    foreach ($ms in $offsets) {
        $f = Join-Path $Dir "mtime-$ms.bin"
        [IO.File]::WriteAllBytes($f, [byte[]]::new(16))
        $want = $base.AddMilliseconds($ms)
        [IO.File]::SetLastWriteTime($f, $want)
        $got = [IO.File]::GetLastWriteTime($f)
        $rows += [pscustomobject]@{
            requested_ms = $ms
            requested    = $want.ToString('HH:mm:ss.fff')
            stored       = $got.ToString('HH:mm:ss.fff')
            delta_ms     = [math]::Round(($got - $want).TotalMilliseconds)
        }
        Remove-Item $f -Force
    }
    $rows
}

# Stamping a time with SetLastWriteTime says what the filesystem can store. What RomMBat
# actually compares is the time an emulator's own write landed, so measure that too: write,
# read the mtime straight back, and see how far it is from the clock.
function Measure-NaturalMtime([string] $Dir, [int] $Count = 8, [int] $GapMs = 300) {
    $rows = @()
    for ($i = 0; $i -lt $Count; $i++) {
        $f = Join-Path $Dir "natural-$i.bin"
        $wrote = Get-Date
        [IO.File]::WriteAllBytes($f, [byte[]]::new(64))
        $got = [IO.File]::GetLastWriteTime($f)
        $rows += [pscustomobject]@{
            wrote_at = $wrote.ToString('HH:mm:ss.fff')
            stored   = $got.ToString('HH:mm:ss.fff')
            skew_ms  = [math]::Round(($got - $wrote).TotalMilliseconds)
        }
        Remove-Item $f -Force
        Start-Sleep -Milliseconds $GapMs
    }
    $rows
}

# FAT stores local wall-clock time, NTFS stores UTC. If that is live here, a file stamped in
# one DST period reads back an hour out in the other, with nothing having touched it.
function Measure-DstRoundTrip([string] $Dir) {
    $rows = @()
    foreach ($pair in @(@{ label = 'winter (no DST)'; t = [DateTime]::new(2026, 1, 15, 12, 0, 0, [DateTimeKind]::Local) },
                        @{ label = 'summer (DST)'; t = [DateTime]::new(2026, 7, 15, 12, 0, 0, [DateTimeKind]::Local) })) {
        $f = Join-Path $Dir 'dst.bin'
        [IO.File]::WriteAllBytes($f, [byte[]]::new(16))
        [IO.File]::SetLastWriteTime($f, $pair.t)
        $local = [IO.File]::GetLastWriteTime($f)
        $utc = [IO.File]::GetLastWriteTimeUtc($f)
        $rows += [pscustomobject]@{
            label         = $pair.label
            requested     = $pair.t.ToString('yyyy-MM-dd HH:mm:ss')
            stored_local  = $local.ToString('yyyy-MM-dd HH:mm:ss')
            stored_utc    = $utc.ToString('yyyy-MM-dd HH:mm:ss')
            local_delta_s = [math]::Round(($local - $pair.t).TotalSeconds)
            utc_offset_h  = [math]::Round(($local - $utc).TotalHours, 2)
        }
        Remove-Item $f -Force
    }
    $rows
}

# The failure mode M2 has to pre-empt: what a >4 GB write does, where it stops, and what the
# exception says. Written in chunks so the stopping point is exact.
function Measure-SizeCeiling([string] $Dir, [long] $Free) {
    $target = 4GB + 64MB
    if ($Free -lt ($target + 256MB)) {
        return [pscustomobject]@{ attempted = $false; reason = "needs $([math]::Round($target/1GB,2)) GB free, volume has $([math]::Round($Free/1GB,2)) GB" }
    }
    $f = Join-Path $Dir 'oversize.bin'
    $chunk = [byte[]]::new(8MB)
    $written = 0L
    $err = $null
    $sw = [Diagnostics.Stopwatch]::StartNew()
    try {
        $fs = [IO.File]::Open($f, [IO.FileMode]::Create, [IO.FileAccess]::Write)
        try {
            while ($written -lt $target) {
                $fs.Write($chunk, 0, $chunk.Length)
                $written += $chunk.Length
            }
            $fs.Flush()
        } finally { $fs.Dispose() }
    } catch {
        # PowerShell wraps a failed method call in MethodInvocationException, whose HResult
        # describes the wrapper and not the filesystem, so walk down to the real one.
        $e = $_.Exception
        while ($e.InnerException) { $e = $e.InnerException }
        $err = [pscustomobject]@{
            type    = $e.GetType().FullName
            message = $e.Message
            hresult = '0x{0:X8}' -f $e.HResult
            win32   = $e.HResult -band 0xFFFF
        }
    }
    $sw.Stop()
    $onDisk = if (Test-Path $f) { (Get-Item $f).Length } else { 0 }
    Remove-Item $f -Force -ErrorAction SilentlyContinue
    [pscustomobject]@{
        attempted     = $true
        target_bytes  = $target
        written_bytes = $written
        on_disk_bytes = $onDisk
        stopped_at_gb = [math]::Round($onDisk / 1GB, 4)
        seconds       = [math]::Round($sw.Elapsed.TotalSeconds, 1)
        error         = $err
    }
}

function Invoke-Suite([string] $Root) {
    $facts = Get-VolumeFacts $Root
    Write-Host ''
    Write-Host "=== $($facts.drive)  $($facts.drive_format)  $($facts.size_gb) GB, $($facts.free_gb) GB free ===" -ForegroundColor Cyan
    $facts | Format-List | Out-String -Width 100 | Write-Host

    $dir = Join-Path $Root 'rommbat-probe7-fs'
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    try {
        Write-Host '  -- modification time granularity' -ForegroundColor DarkGray
        $mtime = Measure-MtimeGranularity $dir
        $mtime | Format-Table -AutoSize | Out-String -Width 120 | Write-Host

        Write-Host '  -- what a natural write stores' -ForegroundColor DarkGray
        $natural = Measure-NaturalMtime $dir
        $natural | Format-Table -AutoSize | Out-String -Width 120 | Write-Host

        Write-Host '  -- local time vs UTC across a DST boundary' -ForegroundColor DarkGray
        $dst = Measure-DstRoundTrip $dir
        $dst | Format-Table -AutoSize | Out-String -Width 140 | Write-Host

        $size = $null
        if ($SkipLargeFile) {
            Write-Host '  -- 4 GB ceiling: skipped' -ForegroundColor DarkGray
        } else {
            Write-Host '  -- 4 GB ceiling (writing 4 GB + 64 MB, this takes a while)' -ForegroundColor DarkGray
            $size = Measure-SizeCeiling $dir ([IO.DriveInfo]::new($Root).AvailableFreeSpace)
            $size | Format-List | Out-String -Width 120 | Write-Host
        }

        [pscustomobject]@{ volume = $facts; mtime = $mtime; natural_mtime = $natural; dst = $dst; size_ceiling = $size }
    } finally {
        Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$results = @()
$results += Invoke-Suite ($Drive.TrimEnd('\') + '\')
if ($Compare) { $results += Invoke-Suite ($Compare.TrimEnd('\') + '\') }

if ($OutJson) {
    New-Item -ItemType Directory -Path (Split-Path $OutJson) -Force | Out-Null
    $results | ConvertTo-Json -Depth 6 | Set-Content -Path $OutJson -Encoding utf8
    Write-Host "wrote $OutJson" -ForegroundColor DarkGray
}
