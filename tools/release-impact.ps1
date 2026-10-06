#Requires -Version 7

<#
.SYNOPSIS
    Tallies the semver labels on the PRs a release collects, and refuses a stable tag that
    undershoots them.

.DESCRIPTION
    Each merged PR carries one of semver:major, semver:minor or semver:patch (CONTRIBUTING.md,
    "Labels and release notes"). This finds the PRs merged since the previous release tag, writes
    the highest impact and every MAJOR-impact PR as Markdown, and throws when a tag at 1.0.0 or
    later is a smaller bump than the highest label asks for.

    Under 0.x, and for a prerelease, it only reports: versioning.md moves the prerelease counter
    there instead, so the bump a label asks for has no tag form to check against.

    The range is the first-parent history of main, so a PR is found by its merge commit's
    subject. It needs the full history and the tags (checkout fetch-depth: 0) and an
    authenticated gh.

.PARAMETER Version
    The version being released, without the leading v.

.PARAMETER Ref
    The commit being released. HEAD by default, which is the tagged commit in the release job.

.PARAMETER OutFile
    A Markdown file to append the report to. Left out, it is written to the output stream.

.EXAMPLE
    ./tools/release-impact.ps1 -Version 0.1.0-alpha.2
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,
    [string] $Ref = 'HEAD',
    [string] $OutFile
)

$ErrorActionPreference = 'Stop'

function ConvertTo-Core([string] $v) {
    $core = ($v -replace '^v', '') -split '[-+]', 2 | Select-Object -First 1
    $parts = $core -split '\.' | ForEach-Object { [int] $_ }
    [pscustomobject] @{ Major = $parts[0]; Minor = $parts[1]; Patch = $parts[2] }
}

$isStable = -not $Version.Contains('-')
$new = ConvertTo-Core $Version

# A stable release is measured against the previous stable one, so 1.1.0 after 1.1.0-rc.1 still
# collects everything since 1.0.0. Anything else, and the first stable release (1.0.0 follows only
# prereleases), is measured against the previous tag of any kind. The tagged commit itself is
# excluded by starting from its first parent.
$describe = @('describe', '--tags', '--abbrev=0', '--match', 'v*')
$previous = $null
if ($isStable) { $previous = git @describe --exclude 'v*-*' "$Ref^" 2>$null }
if (-not $previous) { $previous = git @describe "$Ref^" 2>$null }
if ($LASTEXITCODE -ne 0 -or -not $previous) {
    Write-Host 'No earlier release tag: this is the first release, so there is nothing to tally.'
    exit 0
}

$subjects = git log --first-parent --format=%s "$previous..$Ref"
if ($LASTEXITCODE -ne 0) { throw "git log $previous..$Ref failed." }
$numbers = $subjects | ForEach-Object {
    if ($_ -match '^Merge pull request #(\d+) ') { [int] $Matches[1] }
    elseif ($_ -match '\(#(\d+)\)$') { [int] $Matches[1] }
} | Sort-Object -Unique

$rank = @{ 'semver:patch' = 1; 'semver:minor' = 2; 'semver:major' = 3 }
$names = @{ 0 = 'none'; 1 = 'patch'; 2 = 'minor'; 3 = 'major' }
$highest = 0
$majors = @()
$unlabelled = @()
foreach ($n in $numbers) {
    $pr = gh pr view $n --json number,title,labels | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw "gh pr view $n failed." }
    # Per label object, because a PR with no labels makes .labels.name $null, and ForEach-Object
    # would index the hashtable with it. Measure-Object returns a double, which [int] makes a key
    # the $names lookup finds.
    $impact = [int] ($pr.labels | ForEach-Object { $rank[$_.name] } | Measure-Object -Maximum).Maximum
    if (-not $impact) { $unlabelled += $pr; continue }
    if ($impact -gt $highest) { $highest = $impact }
    if ($impact -eq 3) { $majors += $pr }
}

$lines = @("**Version impact since ${previous}:** $($names[$highest]), from $($numbers.Count) pull requests.")
if ($unlabelled) {
    $lines += "No semver label on: $(($unlabelled | ForEach-Object { "#$($_.number)" }) -join ', ')."
}
if ($majors) {
    $lines += ''
    $lines += 'MAJOR-impact changes. Say what each one breaks and what to do about it:'
    $lines += $majors | ForEach-Object { "- #$($_.number) $($_.title)" }
}
$lines += ''

if ($OutFile) { $lines | Add-Content -Path $OutFile -Encoding utf8 } else { $lines }

if (-not $isStable -or $new.Major -lt 1) { exit 0 }
$old = ConvertTo-Core $previous
$bump = if ($new.Major -gt $old.Major) { 3 } elseif ($new.Minor -gt $old.Minor) { 2 } else { 1 }
if ($old.Major -ge 1 -and $bump -lt $highest) {
    throw "v$Version is a $($names[$bump]) bump over $previous, but a merged PR is labeled semver:$($names[$highest]). Delete the tag and push the right one."
}
