<#
.SYNOPSIS
  M0 probe 6b: how long a connection to an unreachable RomM instance takes to fail.

.DESCRIPTION
  The UI performs a reachability check before every sync, so the worst-case failure
  latency is the budget every one of those checks has to fit inside. Four distinct
  failure modes produce very different numbers, and only the slowest one matters:

    lan-absent      address inside the local subnet with no host answering ARP
    lan-closed      a host that is up, on a port nothing is listening on
    offsubnet       a routed address that blackholes (RFC 5737 TEST-NET-1)
    dns-fail        a hostname that does not resolve

  Repetitions are reported individually rather than averaged, because Windows caches
  negative ARP entries and the first attempt is not the same measurement as the fifth.

.PARAMETER Subnet
  Local subnet prefix, used to build the lan-absent target.

.PARAMETER LanAbsent
  An address on your own subnet with nothing answering. Required: there is no sane default,
  since the measurement is of ARP behaviour on a real local network.

.PARAMETER LanClosed
  A host on your own subnet that is up, with nothing listening on -Port. The gateway usually
  works.

.EXAMPLE
  pwsh -File tools/m0-probes/probe6-reachability.ps1 -LanAbsent 192.168.1.253 -LanClosed 192.168.1.1
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $LanAbsent,
    [Parameter(Mandatory)] [string] $LanClosed,
    [int]    $Port = 8080,
    [int]    $Reps = 5,
    [string] $OutFile = 'probe-output/probe6-reachability.json'
)

$ErrorActionPreference = 'Stop'

function Measure-TcpConnect {
    param(
        [string] $Label,
        [string] $Target,
        [int]    $TargetPort
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $client = [System.Net.Sockets.TcpClient]::new()
    $outcome = 'connected'
    $detail = ''

    try {
        # No client-side timeout: the point is to observe what the OS stack does.
        $client.ConnectAsync($Target, $TargetPort).GetAwaiter().GetResult()
    } catch {
        $inner = $_.Exception
        while ($inner.InnerException) { $inner = $inner.InnerException }
        $outcome = 'failed'
        $detail = if ($inner -is [System.Net.Sockets.SocketException]) {
            "$($inner.SocketErrorCode) ($($inner.NativeErrorCode))"
        } else {
            $inner.GetType().Name
        }
    } finally {
        $sw.Stop()
        $client.Dispose()
    }

    [pscustomobject]@{
        label      = $Label
        target     = "${Target}:${TargetPort}"
        ms         = [math]::Round($sw.Elapsed.TotalMilliseconds, 1)
        outcome    = $outcome
        error      = $detail
    }
}

$cases = @(
    @{ Label = 'lan-absent'; Target = $LanAbsent;   Port = $Port }
    @{ Label = 'lan-closed'; Target = $LanClosed;   Port = 47812 }
    @{ Label = 'offsubnet';  Target = '192.0.2.1';  Port = $Port }
    @{ Label = 'dns-fail';   Target = 'romm-probe-does-not-exist.invalid'; Port = $Port }
)

$results = [System.Collections.Generic.List[object]]::new()

foreach ($case in $cases) {
    Write-Host "== $($case.Label) -> $($case.Target):$($case.Port)" -ForegroundColor Cyan
    for ($i = 1; $i -le $Reps; $i++) {
        $r = Measure-TcpConnect -Label $case.Label -Target $case.Target -TargetPort $case.Port
        $r | Add-Member -NotePropertyName rep -NotePropertyValue $i
        $results.Add($r)
        '   rep {0}: {1,9:N1} ms  {2} {3}' -f $i, $r.ms, $r.outcome, $r.error | Write-Host
    }
}

Write-Host ''
Write-Host '== summary (ms) ==' -ForegroundColor Cyan
$results | Group-Object label | ForEach-Object {
    $ms = $_.Group.ms
    [pscustomobject]@{
        case   = $_.Name
        first  = $_.Group[0].ms
        min    = ($ms | Measure-Object -Minimum).Minimum
        median = ($ms | Sort-Object)[[int]([math]::Floor($ms.Count / 2))]
        max    = ($ms | Measure-Object -Maximum).Maximum
        error  = $_.Group[0].error
    }
} | Format-Table -AutoSize

$dir = Split-Path -Parent $OutFile
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }

[pscustomobject]@{
    captured_utc = (Get-Date).ToUniversalTime().ToString('o')
    os           = [System.Environment]::OSVersion.VersionString
    reps         = $Reps
    results      = $results
} | ConvertTo-Json -Depth 6 | Set-Content -Path $OutFile -Encoding utf8

Write-Host "wrote $OutFile"
