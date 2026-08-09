<#
.SYNOPSIS
  M0 probe 1 follow-up: does a space in the gamelist <name> suppress the game-start hook?

.DESCRIPTION
  Across seven launches, `game-start` fired for every game whose display name has no space
  (2048, four times) and for none whose name has one (Mr Boom, three times). `game-end`
  fired in every case. The correlation is perfect but it is still a correlation, and the
  candidate cause (ES building the script invocation without quoting the name) would break
  `game-start` for essentially every real rom, since real game names nearly all contain
  spaces. That would gut M6.

  This is a crossover, not a repeat: it moves the space onto the game that currently works
  and off the one that currently fails. If the results swap, the name is the cause and
  nothing else about those two entries matters.

    2048     -> "2048 Space Test"   expected to STOP firing game-start
    Mr Boom  -> "MrBoom"            expected to START firing game-start

  Only <name> changes. The rom files, paths and everything else stay put.

.EXAMPLE
  pwsh -File tools/m0-probes/probe1-name-crossover.ps1 -Root K:\RetroBat
  pwsh -File tools/m0-probes/probe1-name-crossover.ps1 -Root K:\RetroBat -Report
  pwsh -File tools/m0-probes/probe1-name-crossover.ps1 -Root K:\RetroBat -Revert
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [switch] $Report,
    [switch] $Revert
)

$ErrorActionPreference = 'Stop'

$gamelist = Join-Path $Root 'roms\ports\gamelist.xml'
$backup = "$gamelist.rommbat-backup"
$hookLog = Join-Path $Root 'rommbat-probe\hooks.log'

# rom stem -> the name to write for the crossover
$crossover = @{
    '2048'   = '2048 Space Test'
    'mrboom' = 'MrBoom'
}

if ($Report) {
    if (-not (Test-Path $hookLog)) { Write-Warning "no hook log at $hookLog"; return }

    $events = @()
    $current = $null
    foreach ($line in Get-Content $hookLog) {
        if ($line -match '^=== EVENT=(?<ev>\S+) T_START=(?<ts>.+?) PID=') {
            $current = [pscustomobject]@{ Event = $Matches.ev; Start = $Matches.ts.Trim(); Rom = '' }
            $events += $current
        } elseif ($current -and $line -match '^\s+1=(?<v>.+)$') {
            $current.Rom = Split-Path -Leaf $Matches.v
        }
    }

    Write-Host '=== events, newest session ===' -ForegroundColor Cyan
    foreach ($e in $events) {
        $label = if ($e.Rom) { $e.Rom } else { '(no args)' }
        $colour = if ($e.Event -eq 'game-start') { 'Green' } else { 'Gray' }
        Write-Host ("  {0,-12} {1,-14} {2}" -f $e.Event, $e.Start.Split(' ')[-1], $label) -ForegroundColor $colour
    }

    $starts = @($events | Where-Object Event -eq 'game-start')
    $ends = @($events | Where-Object Event -eq 'game-end')
    Write-Host ''
    Write-Host ("game-start: {0}    game-end: {1}" -f $starts.Count, $ends.Count)
    Write-Host ''
    Write-Host 'Read it as: which rom appears on a game-start line. A rom that launched but' -ForegroundColor Yellow
    Write-Host 'produced only a game-end did not fire game-start.' -ForegroundColor Yellow
    return
}

if (-not (Test-Path $gamelist)) { throw "no ports gamelist at $gamelist" }

if ($Revert) {
    if (Test-Path $backup) {
        Move-Item $backup $gamelist -Force
        Write-Host "restored $gamelist"
    } else {
        Write-Host 'no backup to restore.'
    }
    return
}

if (-not (Test-Path $backup)) { Copy-Item $gamelist $backup }

[xml] $xml = Get-Content $gamelist
$changed = 0
foreach ($game in $xml.gameList.game) {
    $stem = [IO.Path]::GetFileNameWithoutExtension($game.path)
    if ($crossover.ContainsKey($stem)) {
        Write-Host ("  {0}: '{1}' -> '{2}'" -f $stem, $game.name, $crossover[$stem])
        $game.name = $crossover[$stem]
        $changed++
    }
}
$xml.Save($gamelist)
Write-Host "rewrote $changed entries in $gamelist (backup kept alongside)"

# Start the next capture clean so the crossover session is unambiguous.
if (Test-Path $hookLog) {
    $stamp = Get-Date -Format 'HHmmss'
    Move-Item $hookLog "$hookLog.$stamp.bak"
    Write-Host "archived previous hook log"
}

Write-Host ''
Write-Host 'Launch "2048 Space Test" and "MrBoom" from Ports, quit each, exit ES, then -Report.' -ForegroundColor Yellow
