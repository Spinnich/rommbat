<#
.SYNOPSIS
  M0 probe 7c: is an .exe hook the one form of ES event script that survives a real rom name?

.DESCRIPTION
  Probe 7b measured both scripted forms failing on ordinary filenames, for different
  reasons and at different thresholds:

    .bat  ES hands it to ShellExecute, whose batfile association is cmd /c "%1" %*. As
          soon as any argument carries its own quotes, cmd's quote-stripping rule mangles
          the line and the script never starts. Any space anywhere is enough.
    .ps1  ES builds "powershell <script> <args>" with no -File, so it is an implicit
          -Command and PowerShell re-parses the tail as code. A space splits the name
          across arguments; a parenthesis or comma is a parse error and nothing runs.

  An .exe has no interpreter in the path and takes its arguments through the normal
  CommandLineToArgvW rules, so it should be immune to both. This installs one and lets a
  single launch decide.

  The exe is built with the .NET Framework compiler shipped in every Windows install, so
  the probe needs no SDK and no build step in the repo.

.EXAMPLE
  pwsh -File tools/m0-probes/probe7c-exe-hook.ps1 -Root K:\RetroBat
  pwsh -File tools/m0-probes/probe7c-exe-hook.ps1 -Root K:\RetroBat -Uninstall
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [switch] $Uninstall
)

$ErrorActionPreference = 'Stop'

$scriptsDir = Join-Path $Root 'emulationstation\.emulationstation\scripts'
$probeDir = Join-Path $Root 'rommbat-probe'
$exeName = 'zz-rommbat-diag.exe'

if (-not (Test-Path $scriptsDir)) { throw "no ES scripts directory at $scriptsDir" }

if ($Uninstall) {
    Get-ChildItem -Path $scriptsDir -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq $exeName } |
        ForEach-Object { Remove-Item $_.FullName -Force; Write-Host "removed $($_.FullName)" }
    Remove-Item (Join-Path $probeDir $exeName) -Force -ErrorAction SilentlyContinue
    Write-Host 'probe 7c uninstalled.'
    return
}

New-Item -ItemType Directory -Force -Path $probeDir | Out-Null

# The event name comes from the folder the exe sits in, so one binary serves every event.
$source = @'
using System;
using System.IO;
using System.Text;

class Hook
{
    static void Main(string[] args)
    {
        string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string eventDir = Path.GetDirectoryName(exe);
        string eventName = Path.GetFileName(eventDir);
        string root = Path.GetFullPath(Path.Combine(eventDir, @"..\..\..\.."));
        string host = Environment.GetEnvironmentVariable("COMPUTERNAME");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== exe EVENT=" + eventName
            + " ID=" + System.Diagnostics.Process.GetCurrentProcess().Id
            + " T=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        sb.AppendLine("  SCRIPT=" + exe);
        sb.AppendLine("  ARGC=" + args.Length);
        for (int i = 0; i < args.Length; i++) { sb.AppendLine("  ARG" + i + "=[" + args[i] + "]"); }
        sb.AppendLine("  RAW=" + Environment.CommandLine);
        sb.AppendLine("  ROOT_RESOLVED=" + root);
        sb.AppendLine("  HOST=" + host + " USER=" + Environment.GetEnvironmentVariable("USERNAME"));
        sb.AppendLine("  CWD=" + Environment.CurrentDirectory);
        string text = sb.ToString();

        // Local first, so a blocked or lost write to the tree still proves the hook ran.
        string[] dests = new string[] {
            Path.Combine(Path.GetTempPath(), "rommbat-diag-" + host + "-exe.log"),
            Path.Combine(root, @"rommbat-probe\diag-" + host + "-exe.log")
        };
        foreach (string dest in dests)
        {
            try { File.AppendAllText(dest, text); } catch { }
        }
    }
}
'@

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "no C# compiler at $csc" }

$cs = Join-Path $probeDir 'zz-rommbat-diag.cs'
$exe = Join-Path $probeDir $exeName
Set-Content -Path $cs -Value $source -Encoding utf8

& $csc /nologo /target:exe /platform:anycpu /out:$exe $cs | Out-Null
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw 'compile failed' }
Remove-Item $cs -Force

Write-Host "built $exe"

foreach ($event in (Get-ChildItem -Path $scriptsDir -Directory | Select-Object -ExpandProperty Name)) {
    Copy-Item $exe (Join-Path (Join-Path $scriptsDir $event) $exeName) -Force
    Write-Host "installed $event\$exeName"
}

Write-Host ''
Write-Host 'installed. Launch a rom whose filename contains spaces and parentheses,'
Write-Host 'then read rommbat-probe\diag-<host>-exe.log.'
