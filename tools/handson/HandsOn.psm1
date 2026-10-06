#Requires -Version 7

<#
.SYNOPSIS
    Drives the agent's own RetroBat tree for a hands-on pass: deploy, pair, start ES, launch
    RomMBat, send keys and take screenshots.

.DESCRIPTION
    Every technique here is already recorded in a skill (platform-certification's waves.md and
    passes.md, retrobat-layout's emulationstation.md, docs/contributing/testing.md). This module
    exists so a session stops rewriting them into probe-output/ and skipping the pass for want
    of them. tools/handson/README.md says which function a change type calls.

    Roots and tokens come from the main checkout's .env. No function prints a token.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:EsOrigin = 'http://127.0.0.1:1234'

# Processes whose presence means someone else has the screen: ES, its launcher, and RomMBat.
# Every emulator runs under emulatorLauncher, so its name stands for all of them.
$script:TakeoverBlockers = @('emulationstation', 'emulatorLauncher', 'RomMBat', 'RetroBat')

#region Environment

function Get-HandsOnRepoRoot {
    (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
}

function Get-HandsOnEnv {
    <#
    .SYNOPSIS
        Reads the main checkout's .env, which a worktree does not have, into a hashtable.
    #>
    $common = (git -C $PSScriptRoot rev-parse --path-format=absolute --git-common-dir).Trim()
    $candidates = @((Split-Path $common -Parent), (Get-HandsOnRepoRoot))
    $file = $candidates | ForEach-Object { Join-Path $_ '.env' } | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $file) { throw "No .env in $($candidates -join ' or '). DEVELOPER_SETUP.md, 'The live tests', says what goes in it." }

    $values = @{}
    foreach ($line in Get-Content $file) {
        if ($line -match '^\s*([A-Z0-9_]+)\s*=\s*(.*?)\s*$') {
            # A trailing ' # note' is a comment, as it is when bash sources the same file.
            $values[$Matches[1]] = ($Matches[2] -replace '\s+#.*$', '').Trim('"')
        }
    }
    $values
}

# Which .env key names the tree every function acts on. Use-ScoutTree moves the whole kit, guards
# included, onto the scout tree a prerelease is smoke-tested in (the version-adoption skill).
$script:TreeKey = 'ROMMBAT_AGENT_ROOT'

function Use-ScoutTree {
    <#
    .SYNOPSIS
        Points every function at ROMMBAT_SCOUT_ROOT, the agent's second tree, for a scout pass
        on a RetroBat prerelease. Use-AgentTree points them back.
    #>
    $root = (Get-HandsOnEnv)['ROMMBAT_SCOUT_ROOT']
    if (-not $root) { throw 'ROMMBAT_SCOUT_ROOT is not set in .env. tools/handson/README.md, "The scout tree", says how to build it.' }
    $script:TreeKey = 'ROMMBAT_SCOUT_ROOT'
    Write-Host "Acting on the scout tree: $root"
}

function Use-AgentTree {
    $script:TreeKey = 'ROMMBAT_AGENT_ROOT'
    Write-Host "Acting on the agent tree: $(Get-AgentRoot)"
}

function Get-AgentRoot {
    $root = (Get-HandsOnEnv)[$script:TreeKey]
    if (-not $root) { throw "$script:TreeKey is not set in .env. tools/handson/README.md says how to build the tree." }
    $root
}

function Get-RetroBatFloor {
    $source = Join-Path (Get-HandsOnRepoRoot) 'src/RomMBat.Core/Diagnostics/RetroBatVersion.cs'
    if ((Get-Content -Raw $source) -match 'Minimum\s*\{\s*get;\s*\}\s*=\s*ProductVersion\.Parse\("([^"]+)"\)') { return $Matches[1] }
    throw "No RetroBat Minimum in $source"
}

function Get-SessionState {
    # 'Active' when a console or RDP client is attached; 'Disc' when an RDP client left, which
    # turns screenshots black and drops injected keys.
    $line = (query session 2>$null) | Where-Object { $_ -match '^>' } | Select-Object -First 1
    if ($line -match '\s(Active|Disc|Conn|Listen)\b') { return $Matches[1] }
    'Unknown'
}

function Get-TakeoverBlockers {
    @(Get-Process -Name $script:TakeoverBlockers -ErrorAction SilentlyContinue |
        ForEach-Object { "$($_.Name) (pid $($_.Id))" })
}

function Get-AgentProcess {
    <#
    .SYNOPSIS
        Running processes by name that execute from the agent tree. The stop functions use it so
        none can reach another install, whatever it has running.
    #>
    param([Parameter(Mandatory)] [string[]] $Name)
    $prefix = (Get-AgentRoot).TrimEnd('\', '/') + '\'
    @(Get-Process -Name $Name -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($prefix, 'OrdinalIgnoreCase') })
}

function Assert-AgentES {
    <#
    .SYNOPSIS
        Throws unless the ES answering on port 1234 can only be the agent tree's: no
        EmulationStation runs from anywhere else.
    #>
    $prefix = (Get-AgentRoot).TrimEnd('\', '/') + '\'
    $foreign = @(Get-Process -Name 'emulationstation' -ErrorAction SilentlyContinue |
        Where-Object { -not ($_.Path -and $_.Path.StartsWith($prefix, 'OrdinalIgnoreCase')) })
    if ($foreign.Count) { throw "An EmulationStation outside the agent tree is running (pid $($foreign.Id -join ', ')); port 1234 may be its. Not calling it." }
}

function Assert-TakeoverAllowed {
    <#
    .SYNOPSIS
        Throws unless the screen is free to drive: an active desktop session and none of ES, an
        emulator or RomMBat running, whichever tree they belong to.
    .DESCRIPTION
        The maintainer plays on this machine over RDP, in the same session the agent drives, so
        a running ES or emulator means they may be at the pad. -WhilePlaying is /certify's form:
        ES and an emulator may run, because the agent has said in chat that it is driving and
        Show-AgentBanner says so on screen. Otherwise ask instead.
    #>
    param([switch] $WhilePlaying)
    $state = Get-SessionState
    if ($state -ne 'Active') { throw "The desktop session is '$state', not Active: screenshots and keys will not work. Record the GUI part as unproven." }
    if ($WhilePlaying) { Show-AgentBanner; return }
    $busy = @(Get-TakeoverBlockers)
    if ($busy.Count) { throw "Not taking the screen: $($busy -join ', ') already running. Ask the maintainer before driving." }
}

function Test-HandsOnEnv {
    <#
    .SYNOPSIS
        Preflight for a hands-on pass. Prints one line per check and returns $true when all pass.
    #>
    [CmdletBinding()]
    param([switch] $Gui)

    $results = [ordered]@{}
    $envValues = Get-HandsOnEnv
    $root = $envValues[$script:TreeKey]
    $scout = $script:TreeKey -eq 'ROMMBAT_SCOUT_ROOT'

    $results[".env has $script:TreeKey"] = [bool]$root
    $results['.env has the server and approver token'] = [bool]($envValues['ROMMBAT_TEST_SERVER'] -and $envValues['ROMMBAT_TEST_APPROVER_TOKEN'])
    $results['agent tree exists'] = [bool]($root -and (Test-Path (Join-Path $root 'RetroBat.exe')) -and (Test-Path (Join-Path $root 'emulationstation')))

    $versionFile = if ($root) { Join-Path $root 'system\version.info' }
    $floor = Get-RetroBatFloor
    $version = if ($versionFile -and (Test-Path $versionFile)) { (Get-Content $versionFile -Raw).Trim() }
    if ($scout) {
        # A scout tree runs the prerelease being scouted, never the floor.
        $results["RetroBat is not the floor ($version)"] = [bool]($version -and -not $version.StartsWith("$floor-"))
    }
    else {
        $results["RetroBat is the floor ($floor)"] = [bool]($version -and $version.StartsWith("$floor-"))
    }

    $agentExe = if ($root) { Join-Path $root 'emulators\rommbat\rommbat-agent.exe' }
    $results['a build is deployed'] = [bool]($agentExe -and (Test-Path $agentExe))
    $results['the tree is paired'] = [bool]($root -and (Test-Path (Join-Path $root 'emulators\rommbat\rommbat.db')) -and
        ((Invoke-Agent status 2>&1 | Out-String) -match 'paired:\s+yes'))

    try {
        $null = Invoke-WebRequest -Uri ($envValues['ROMMBAT_TEST_SERVER'].TrimEnd('/') + '/api/heartbeat') -TimeoutSec 10
        $results['the server answers'] = $true
    }
    catch { $results['the server answers'] = $false }

    if ($Gui) {
        $results["desktop session is Active ($(Get-SessionState))"] = (Get-SessionState) -eq 'Active'
        $busy = @(Get-TakeoverBlockers)
        $results["nothing else has the screen$(if ($busy.Count) { ': ' + ($busy -join ', ') })"] = $busy.Count -eq 0
        # A launch stalled on a prompt looks like a game still loading.
        $waiting = (Get-LauncherDialog) -and -not @(Get-EmulatorProcess).Count
        $results["no emulatorLauncher prompt waiting$(if ($waiting) { ': read it with Save-Screenshot' })"] = -not $waiting
    }

    foreach ($entry in $results.GetEnumerator()) {
        Write-Host ('{0}  {1}' -f ($(if ($entry.Value) { 'ok  ' } else { 'FAIL' })), $entry.Key)
    }
    $ready = -not ($results.Values -contains $false)
    Write-Host $(if ($ready) { 'ready' } else { 'not ready' })
    $ready
}

#endregion

#region Build and pairing

function Publish-ToAgentTree {
    <#
    .SYNOPSIS
        Publishes this checkout into the agent tree and records which commit is there.
    #>
    $repo = Get-HandsOnRepoRoot
    $root = Get-AgentRoot
    $busy = @(Get-Process -Name 'RomMBat', 'rommbat-agent' -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$root*" })
    if ($busy.Count) { throw "RomMBat is running from $root; close it before deploying." }

    & pwsh -NoProfile -File (Join-Path $repo 'tools/publish.ps1') -Deploy $root -NoZip | Out-Host
    if ($LASTEXITCODE) { throw "publish.ps1 failed with exit $LASTEXITCODE" }

    $sha = (git -C $repo rev-parse --short HEAD).Trim()
    $branch = (git -C $repo rev-parse --abbrev-ref HEAD).Trim()
    $dirty = if (git -C $repo status --porcelain) { ' (with uncommitted changes)' } else { '' }
    $line = "$branch $sha$dirty, deployed $(Get-Date -Format 's')"
    Set-Content -Path (Join-Path $root 'emulators\rommbat\deployed.txt') -Value $line
    Write-Host "Deployed: $line"
}

function Invoke-Agent {
    <#
    .SYNOPSIS
        Runs the deployed rommbat-agent.exe against the agent tree, the build a user would run,
        rather than dotnet run against the source.
    #>
    $root = Get-AgentRoot
    $exe = Join-Path $root 'emulators\rommbat\rommbat-agent.exe'
    if (-not (Test-Path $exe)) { throw "No agent in $root. Run Publish-ToAgentTree first." }
    & $exe @args --root $root
}

function Connect-AgentTree {
    <#
    .SYNOPSIS
        Pairs the agent tree as the approver test account, headlessly, the way the live tests do.
    .DESCRIPTION
        Starts `pair`, reads the code it prints, and approves it with the approver token for
        exactly the scopes the request asked for. device.id is kept, so a re-pair updates the
        same RomM device rather than collecting a new one (testing.md, "Re-pairing").
    #>
    [CmdletBinding()]
    param([string] $DeviceName = 'RomMBat agent tree', [switch] $Repair)

    $envValues = Get-HandsOnEnv
    $server = $envValues['ROMMBAT_TEST_SERVER'].TrimEnd('/')
    $root = Get-AgentRoot
    $exe = Join-Path $root 'emulators\rommbat\rommbat-agent.exe'

    if ($Repair) {
        Get-ChildItem (Join-Path $root 'emulators\rommbat') -Filter 'rommbat.db*' | Remove-Item -Force
    }

    $out = New-TemporaryFile
    $err = New-TemporaryFile
    $proc = Start-Process -FilePath $exe -ArgumentList 'pair', '--root', $root, '--server', $server, '--name', "`"$DeviceName`"" `
        -RedirectStandardOutput $out -RedirectStandardError $err -NoNewWindow -PassThru
    try {
        $code = $null
        $deadline = (Get-Date).AddSeconds(60)
        while (-not $code -and (Get-Date) -lt $deadline -and -not $proc.HasExited) {
            Start-Sleep -Milliseconds 500
            $match = Select-String -Path $out -Pattern 'Code:\s+(\S+)' | Select-Object -First 1
            if ($match) { $code = $match.Matches[0].Groups[1].Value -replace '-', '' }
        }
        if (-not $code) { throw "pair printed no code. stderr: $(Get-Content -Raw $err)" }

        $headers = @{ Authorization = "Bearer $($envValues['ROMMBAT_TEST_APPROVER_TOKEN'])" }
        $pending = Invoke-RestMethod -Uri "$server/api/auth/device/pending/$code" -Headers $headers
        $body = @{ user_code = $code; approved_scopes = @($pending.requested_scopes); device_name = $DeviceName; expires_in = 'never' } |
            ConvertTo-Json
        $null = Invoke-RestMethod -Method Post -Uri "$server/api/auth/device/approve" -Headers $headers -ContentType 'application/json' -Body $body

        if (-not $proc.WaitForExit(60000)) { throw 'pair did not finish within 60 s of approval' }
        if ($proc.ExitCode) { throw "pair exited $($proc.ExitCode): $(Get-Content -Raw $err)" }
        Write-Host "Paired $root as the approver account, $($pending.requested_scopes.Count) scopes."
    }
    finally {
        if (-not $proc.HasExited) { $proc.Kill() }
        Remove-Item $out, $err -ErrorAction SilentlyContinue
    }
}

#endregion

#region EmulationStation

function Invoke-ES {
    <#
    .SYNOPSIS
        One call to ES's loopback API (RB-386). POST when -Body is given.
    #>
    param([Parameter(Mandatory)] [string] $Path, [string] $Body, [int] $TimeoutSec = 30)
    Assert-AgentES
    $uri = "$script:EsOrigin$Path"
    if ($PSBoundParameters.ContainsKey('Body')) {
        (Invoke-WebRequest -Uri $uri -Method Post -Body $Body -TimeoutSec $TimeoutSec).Content
    }
    else {
        (Invoke-WebRequest -Uri $uri -TimeoutSec $TimeoutSec).Content
    }
}

function Start-ES {
    <#
    .SYNOPSIS
        Starts RetroBat on the agent tree and waits for ES's API to answer.
    #>
    param([int] $TimeoutSec = 240)
    Assert-TakeoverAllowed
    Show-AgentBanner
    $root = Get-AgentRoot
    Start-Process -FilePath (Join-Path $root 'RetroBat.exe') -WorkingDirectory $root | Out-Null
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try { $null = Invoke-ES '/caps' -TimeoutSec 5; Write-Host 'ES is up'; return } catch { Start-Sleep -Milliseconds 500 }
    }
    throw "ES did not answer /caps within $TimeoutSec s"
}

function Stop-ES {
    <#
    .SYNOPSIS
        Ends any running game, quits ES, and waits for the process to exit, so the quit hook's
        background pass gets to run.
    .DESCRIPTION
        /quit and /emukill answer 200 and do nothing while a game runs (RB-35), so the emulator
        is ended first and the process is polled rather than trusting the answer.
    #>
    param([int] $TimeoutSec = 120)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $es = Get-AgentProcess emulationstation | Select-Object -First 1
    Stop-Game
    if (-not $es) { return }
    try { $null = Invoke-ES '/quit' -TimeoutSec 10 } catch { }
    while (-not $es.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 250; $es.Refresh() }
    if (-not $es.HasExited) { throw "ES still running after $TimeoutSec s" }
    Write-Host 'ES has exited'
}

function Start-Game {
    <#
    .SYNOPSIS
        Launches a game through ES, which runs the game-start and game-end hooks the way a
        player's launch does. -Path is the game's path as /systems/<system>/games lists it.
    #>
    param([Parameter(Mandatory)] [string] $Path, [switch] $NoWait)
    $null = Invoke-ES '/launch' -Body $Path
    Write-Host "Launched $Path"
    if (-not $NoWait) { $null = Wait-Emulator }
}

function Start-EmulatorLauncher {
    <#
    .SYNOPSIS
        Boots one row through emulatorLauncher directly, without ES, and waits for the emulator.
    .DESCRIPTION
        For a boot nobody plays: a scout's smoke, a certification's launch check. No ES means no
        hooks run, so it records no play session; Start-Game is the launch that does. Pass -Core
        for any emulator that has one, because bizhawk crashes without it (savestates.md). -Rom is
        a file name under roms/<system>/, or a full path.
    #>
    param(
        [Parameter(Mandatory)] [string] $System,
        [Parameter(Mandatory)] [string] $Emulator,
        [string] $Core,
        [Parameter(Mandatory)] [string] $Rom,
        [int] $TimeoutSec = 300
    )
    Assert-TakeoverAllowed
    Show-AgentBanner
    $root = Get-AgentRoot
    $romPath = if ([IO.Path]::IsPathRooted($Rom)) { $Rom } else { Join-Path $root "roms\$System\$Rom" }
    if (-not (Test-Path -LiteralPath $romPath)) { throw "No ROM at $romPath" }
    $arguments = "-system $System -emulator $Emulator$(if ($Core) { " -core $Core" }) -rom `"$romPath`""
    Start-Process -FilePath (Join-Path $root 'emulationstation\emulatorLauncher.exe') -ArgumentList $arguments `
        -WorkingDirectory (Join-Path $root 'emulationstation') | Out-Null
    Write-Host "Launched $System/$Emulator$(if ($Core) { "/$Core" }) on $(Split-Path $romPath -Leaf)"
    $null = Wait-Emulator -TimeoutSec $TimeoutSec
}

function Get-EmulatorProcess {
    # Every emulator runs from <tree>\emulators\, and RomMBat's own folder there holds none.
    $prefix = (Get-AgentRoot).TrimEnd('\', '/') + '\emulators\'
    @(Get-Process | Where-Object {
            $_.Path -and $_.Path.StartsWith($prefix, 'OrdinalIgnoreCase') -and
            -not $_.Path.StartsWith("${prefix}rommbat\", 'OrdinalIgnoreCase')
        })
}

function Get-LauncherDialog {
    <#
    .SYNOPSIS
        The handle of a window emulatorLauncher has up on the tree, or $null.
    .DESCRIPTION
        emulatorLauncher's prompts, "not installed, install now?" before a launch and "keep the
        uncompressed game?" after one, are full-screen windows with no title, and the process's
        MainWindowHandle stays 0, so Get-MainWindow cannot see them. They are found by the
        launcher's pid. Neither times out (savestates.md). Which one is up follows from whether
        an emulator has run yet: Wait-Emulator answers the first and Stop-Game the second.
    #>
    $launcher = Get-AgentProcess emulatorLauncher | Select-Object -First 1
    if (-not $launcher) { return $null }
    [HandsOn.Windows]::VisibleOf([uint32]$launcher.Id) | Select-Object -First 1
}

function Wait-Emulator {
    <#
    .SYNOPSIS
        Waits for a launch to reach a running emulator, answering Yes to emulatorLauncher's
        "install now?" on the way, and returns the emulator's process.
    .DESCRIPTION
        The agent may install an emulator on its own trees (certify.md). A launcher window that
        stays up with no emulator running is that prompt; it is screenshotted before the answer,
        and the path printed, so the record shows what was accepted. A launcher that exits with
        no emulator started failed to launch, and emulationstation\emulatorLauncher.log says why.
    #>
    param([int] $TimeoutSec = 300)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $launcherSeen = $false
    $dialogSince = $null
    $answered = $false
    while ((Get-Date) -lt $deadline) {
        $emulator = Get-EmulatorProcess | Select-Object -First 1
        if ($emulator) { Write-Host "Running: $($emulator.Name)"; return $emulator }

        if (Get-AgentProcess emulatorLauncher) { $launcherSeen = $true }
        elseif ($launcherSeen) { throw 'emulatorLauncher exited without starting an emulator; emulationstation\emulatorLauncher.log says why.' }

        $dialog = if (-not $answered) { Get-LauncherDialog }
        if (-not $dialog) { $dialogSince = $null }
        elseif (-not $dialogSince) { $dialogSince = Get-Date }
        elseif (((Get-Date) - $dialogSince).TotalSeconds -ge 2) {
            $shot = Save-Screenshot 'launcher-install-prompt'
            Write-Host "emulatorLauncher is asking to install the emulator; answering Yes. Screenshot: $shot"
            Set-WindowFocus $dialog
            Send-Key Enter -HoldMs 80
            $answered = $true
        }
        Start-Sleep -Milliseconds 500
    }
    $shot = Save-Screenshot 'launch-stalled'
    throw "No emulator running after $TimeoutSec s. Screenshot: $shot"
}

function Stop-Game {
    <#
    .SYNOPSIS
        Ends the running game the way a player does, so emulatorLauncher's mirror and the
        game-end hook both run.
    .DESCRIPTION
        /emukill does nothing while a game runs (RB-35). Closing the emulator's window
        (WM_CLOSE) lets it flush its save; ares can outlast 15 s on that (passes.md), so Escape,
        the QuitEmulator key, follows. emulatorLauncher is never killed: it does the mirror. After
        a zip it extracted, it asks whether to keep the uncompressed game; the answer is No, which
        leaves roms\ as RomMBat synced it.

        An emulator that ignores both is ended by -Force, which loses a save it had not yet
        written, so it is opt-in. A launcher still up after its emulator has gone is a prompt,
        not a deaf emulator.
    #>
    param([int] $TimeoutSec = 30, [switch] $Force)
    $launcher = Get-AgentProcess emulatorLauncher | Select-Object -First 1
    if (-not $launcher) { return }
    Show-AgentBanner
    $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId = $($launcher.Id)" |
        ForEach-Object { Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue })
    foreach ($child in $children) { $null = $child.CloseMainWindow() }

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $escapeSent = $false
    $forced = $false
    $dialogSince = $null
    $declined = $false
    while (-not $launcher.HasExited) {
        if ((Get-Date) -ge $deadline) {
            $running = @(Get-EmulatorProcess)
            if (-not $Force -or $forced -or -not $running.Count) { break }
            Write-Warning "$($running.Name -join ', ') ignored WM_CLOSE and Escape; ending it. A save it had not written is lost."
            $running | Stop-Process -Force
            $forced = $true
            $deadline = (Get-Date).AddSeconds($TimeoutSec)
        }
        Start-Sleep -Milliseconds 500
        $launcher.Refresh()
        if (-not $escapeSent -and (Get-Date) -gt $deadline.AddSeconds(-$TimeoutSec / 2)) {
            $window = $children | Where-Object { -not $_.HasExited -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1
            if ($window) { Set-WindowFocus $window.MainWindowHandle; Send-Key Escape }
            $escapeSent = $true
        }

        # With the emulator gone, a launcher window that stays up is the keep-uncompressed prompt.
        $dialog = if (-not $declined -and -not @(Get-EmulatorProcess).Count) { Get-LauncherDialog }
        if (-not $dialog) { $dialogSince = $null }
        elseif (-not $dialogSince) { $dialogSince = Get-Date }
        elseif (((Get-Date) - $dialogSince).TotalSeconds -ge 2) {
            $shot = Save-Screenshot 'launcher-keep-prompt'
            Write-Host "emulatorLauncher is asking to keep the uncompressed game; answering No. Screenshot: $shot"
            Set-WindowFocus $dialog
            Send-Key Right -HoldMs 80
            Send-Key Enter -HoldMs 80
            $declined = $true
        }
    }
    if (-not $launcher.HasExited) {
        $shot = Save-Screenshot 'stop-game-stalled'
        $hint = if ($Force) { 'end it from the pad or ask the maintainer' } else { 'Stop-Game -Force ends the emulator, losing a save it has not written; or end it from the pad' }
        throw "The game is still running after $TimeoutSec s; $hint. Screenshot: $shot"
    }
    Write-Host 'Game ended'
}

function Get-ESGames {
    param([Parameter(Mandatory)] [string] $System)
    Invoke-ES "/systems/$System/games" | ConvertFrom-Json
}

#endregion

#region Windows, keys and screenshots

if (-not ('HandsOn.Native' -as [type])) {
    Add-Type -Namespace HandsOn -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
[DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
[DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@
}

if (-not ('HandsOn.Windows' -as [type])) {
    Add-Type -Namespace HandsOn -Name Windows -UsingNamespace System.Collections.Generic -MemberDefinition @'
private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
[DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
[DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
[DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
public static IntPtr[] VisibleOf(uint pid) {
    var found = new List<IntPtr>();
    EnumWindows((h, l) => { uint owner; GetWindowThreadProcessId(h, out owner); if (owner == pid && IsWindowVisible(h)) found.Add(h); return true; }, IntPtr.Zero);
    return found.ToArray();
}
'@
}

# The banner is a separate process, because each shell call is a new one and the strip has to
# outlive it. These files are how later calls find it and keep it alive.
$script:BannerState = Join-Path ([IO.Path]::GetTempPath()) 'rommbat-handson-banner.txt'
$script:BannerHeartbeat = Join-Path ([IO.Path]::GetTempPath()) 'rommbat-handson-heartbeat.txt'

function Get-AgentBanner {
    # The banner's process and window handle, or $null when it is not up. A state file outlives a
    # banner that was killed or a reboot, and its PID may since belong to another process, so the
    # process counts only while the stored window exists and is that process's own.
    try { $pidText, $handle = (Get-Content -LiteralPath $script:BannerState -Raw -ErrorAction Stop) -split ' ' } catch { return $null }
    $handle = [IntPtr][long]$handle
    $owner = 0
    if (-not [HandsOn.Native]::IsWindow($handle)) { return $null }
    $null = [HandsOn.Native]::GetWindowThreadProcessId($handle, [ref]$owner)
    if ($owner -ne [uint32]$pidText) { return $null }
    $process = Get-Process -Id $owner -ErrorAction SilentlyContinue
    if (-not $process -or $process.HasExited) { return $null }
    [pscustomobject]@{ Process = $process; Handle = $handle }
}

function Show-AgentBanner {
    <#
    .SYNOPSIS
        Puts "agent is driving" across the top of the screen, or keeps it there, for anyone who
        switches into the session. It takes no input or focus.
    .DESCRIPTION
        Send-Key, Start-ES, Start-RomMBatUI and Stop-Game call it, so the strip is up whenever the
        kit acts. It goes away by itself 3 min after the kit last called it, or on Hide-AgentBanner.
        An emulator in exclusive full screen can draw over it.
    #>
    Set-Content -LiteralPath $script:BannerHeartbeat -Value (Get-Date -Format 'o') -NoNewline
    if (Get-AgentBanner) { return }
    # Start-Process joins -ArgumentList unquoted, and the checkout's path may hold spaces.
    $arguments = "-NoProfile -File `"$(Join-Path $PSScriptRoot 'Banner.ps1')`" -Heartbeat `"$script:BannerHeartbeat`" -StateFile `"$script:BannerState`""
    Start-Process -FilePath (Get-Process -Id $PID).Path -WindowStyle Hidden -ArgumentList $arguments | Out-Null
    $deadline = (Get-Date).AddSeconds(15)
    while (-not (Get-AgentBanner) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }
    if (-not (Get-AgentBanner)) { Write-Warning 'The "agent is driving" banner did not come up. Say in chat that the kit is driving.' }
}

function Hide-AgentBanner {
    <#
    .SYNOPSIS
        Takes the banner down: the agent has handed the session back.
    #>
    $banner = Get-AgentBanner
    if ($banner) { $banner.Process.Kill(); $null = $banner.Process.WaitForExit(5000) }
    Remove-Item -LiteralPath $script:BannerState, $script:BannerHeartbeat -ErrorAction SilentlyContinue
}

# The UI's desk map (src/RomMBat.UI/CLAUDE.md), and the keys emulators read (waves.md).
$script:Keys = @{
    Up = 0x26; Down = 0x28; Left = 0x25; Right = 0x27
    Enter = 0x0D; A = 0x0D; Escape = 0x1B; B = 0x1B; Backspace = 0x08; L1 = 0x08
    Tab = 0x09; X = 0x09; Start = 0x74; Ctrl = 0x11; Shift = 0x10; Alt = 0x12; Space = 0x20
    F1 = 0x70; F2 = 0x71; F3 = 0x72; F4 = 0x73; F5 = 0x74; F6 = 0x75; F7 = 0x76; F8 = 0x77
    F9 = 0x78; F10 = 0x79; F11 = 0x7A; F12 = 0x7B
}
$script:ExtendedKeys = @(0x25, 0x26, 0x27, 0x28)

function Get-MainWindow {
    param([Parameter(Mandatory)] [string] $ProcessName, [int] $TimeoutSec = 30)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        $p = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
        if ($p) { return $p.MainWindowHandle }
        Start-Sleep -Milliseconds 250
    }
    throw "No window for $ProcessName within $TimeoutSec s"
}

function Set-WindowFocus {
    param([Parameter(Mandatory)] [IntPtr] $Handle)
    # Windows refuses SetForegroundWindow to a process that did not take the last input; a
    # bare Alt tap counts as input and lifts the lock.
    [HandsOn.Native]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
    [HandsOn.Native]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
    $null = [HandsOn.Native]::ShowWindow($Handle, 9)
    $null = [HandsOn.Native]::SetForegroundWindow($Handle)
    Start-Sleep -Milliseconds 200
}

function Send-Key {
    <#
    .SYNOPSIS
        Presses a key, or a chord such as Ctrl+F2, with its hardware scan code.
    .DESCRIPTION
        keybd_event with the scan code reaches DirectInput readers that ignore SendKeys
        (EmuHawk, RB-269), and an emulator needs the key held about 400 ms (passes.md). The UI
        reads the key on press, so -HoldMs 60 is enough there. It shows the banner first.
    .EXAMPLE
        Send-Key Down -Window RomMBat
        Send-Key Ctrl+F2 -Window EmuHawk
    #>
    param(
        [Parameter(Mandatory)] [string] $Key,
        [string] $Window,
        [int] $HoldMs = 400,
        [int] $Times = 1,
        [int] $GapMs = 250
    )
    Show-AgentBanner
    if ($Window) { Set-WindowFocus (Get-MainWindow $Window) }
    $vks = foreach ($part in $Key -split '\+') {
        if (-not $script:Keys.ContainsKey($part)) { throw "Unknown key '$part'. Known: $($script:Keys.Keys -join ', ')" }
        [byte]$script:Keys[$part]
    }
    for ($i = 0; $i -lt $Times; $i++) {
        foreach ($vk in $vks) {
            $flags = if ($script:ExtendedKeys -contains $vk) { 1 } else { 0 }
            [HandsOn.Native]::keybd_event($vk, [byte][HandsOn.Native]::MapVirtualKey($vk, 0), $flags, [UIntPtr]::Zero)
        }
        Start-Sleep -Milliseconds $HoldMs
        [array]::Reverse($vks)
        foreach ($vk in $vks) {
            $flags = 2 -bor $(if ($script:ExtendedKeys -contains $vk) { 1 } else { 0 })
            [HandsOn.Native]::keybd_event($vk, [byte][HandsOn.Native]::MapVirtualKey($vk, 0), $flags, [UIntPtr]::Zero)
        }
        [array]::Reverse($vks)
        Start-Sleep -Milliseconds $GapMs
    }
}

function Save-Screenshot {
    <#
    .SYNOPSIS
        Saves a PNG of one window, or the whole screen, under probe-output/handson-<date>/ and
        returns its path for the Read tool.
    .DESCRIPTION
        PrintWindow with PW_RENDERFULLCONTENT captures a GPU-drawn window (Avalonia over Skia, an
        emulator) even when another window covers it. Without -Window the primary screen is
        copied instead, which is what a full-screen ES needs, with the banner hidden for it.
    #>
    param([Parameter(Mandatory)] [string] $Name, [string] $Window)
    Add-Type -AssemblyName System.Drawing
    $dir = Join-Path (Get-HandsOnRepoRoot) "probe-output/handson-$(Get-Date -Format 'yyyyMMdd')"
    New-Item -ItemType Directory -Force $dir | Out-Null
    $path = Join-Path $dir ("{0:HHmmss}-{1}.png" -f (Get-Date), $Name)

    if ($Window) {
        $handle = Get-MainWindow $Window
        $rect = New-Object HandsOn.Native+RECT
        $null = [HandsOn.Native]::GetWindowRect($handle, [ref]$rect)
        $bitmap = [System.Drawing.Bitmap]::new($rect.Right - $rect.Left, $rect.Bottom - $rect.Top)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $hdc = $graphics.GetHdc()
        $ok = [HandsOn.Native]::PrintWindow($handle, $hdc, 2)
        $graphics.ReleaseHdc($hdc)
        if (-not $ok) { $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size) }
    }
    else {
        $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bitmap = [System.Drawing.Bitmap]::new($bounds.Width, $bounds.Height)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $banner = Get-AgentBanner
        # SW_HIDE, then SW_SHOWNOACTIVATE so the strip comes back without taking focus.
        if ($banner) { $null = [HandsOn.Native]::ShowWindow($banner.Handle, 0); Start-Sleep -Milliseconds 100 }
        try { $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size) }
        finally { if ($banner) { $null = [HandsOn.Native]::ShowWindow($banner.Handle, 4) } }
    }
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $bitmap.Dispose()
    $path
}

function Start-RomMBatUI {
    <#
    .SYNOPSIS
        Launches the deployed RomMBat.exe on the agent tree, standalone, and waits for its window.
    #>
    Assert-TakeoverAllowed
    $root = Get-AgentRoot
    Start-Process -FilePath (Join-Path $root 'emulators\rommbat\RomMBat.exe') -ArgumentList '--root', $root -WorkingDirectory $root | Out-Null
    $handle = Get-MainWindow 'RomMBat' -TimeoutSec 60
    Show-AgentBanner
    Set-WindowFocus $handle
    Write-Host 'RomMBat is up'
}

function Stop-RomMBatUI {
    Get-AgentProcess 'RomMBat' | ForEach-Object {
        $null = $_.CloseMainWindow()
        if (-not $_.WaitForExit(10000)) { $_.Kill() }
    }
}

#endregion

Add-Type -AssemblyName System.Windows.Forms

Export-ModuleMember -Function Get-HandsOnEnv, Get-AgentRoot, Use-ScoutTree, Use-AgentTree, Test-HandsOnEnv, Assert-TakeoverAllowed, Show-AgentBanner, Hide-AgentBanner,
    Publish-ToAgentTree, Invoke-Agent, Connect-AgentTree,
    Invoke-ES, Start-ES, Stop-ES, Start-Game, Stop-Game, Get-ESGames, Start-EmulatorLauncher, Wait-Emulator, Get-LauncherDialog,
    Send-Key, Save-Screenshot, Start-RomMBatUI, Stop-RomMBatUI
