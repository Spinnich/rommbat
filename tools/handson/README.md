# Hands-on kit

`HandsOn.psm1` drives the agent tree, a RetroBat install that belongs to the agent, end to end:
deploy a build, pair it, start EmulationStation, launch a game, open RomMBat, send keys and take
screenshots. What each kind of change owes is in the `pre-pr-verification` skill, "Hands-on by
change type". This page says how to do it.

```powershell
Import-Module ./tools/handson/HandsOn.psm1 -Force
Test-HandsOnEnv -Gui        # one line per check, then ready or not ready
```

## The agent tree

`ROMMBAT_AGENT_ROOT` in the main checkout's `.env` names it. It is the only tree hands-on passes
and `/certify` use, and the kit touches no other but the scout tree below: the stop functions act
only on processes running from the selected tree, and the ES calls refuse while an ES from anywhere
else runs. The tree
is paired as the approver test account, so what it uploads stays apart from the maintainer's own
saves. The agent may deploy to it, pair, sync, reset and drive it without asking
([workflow](../../docs/contributing/workflow.md)), and the maintainer plays it over RDP during
`/certify`.

Building it from scratch, after `./tools/retrobat-install.ps1 -Path D:\retrobat-pristine`:

```powershell
Copy-Item -Recurse D:\retrobat-pristine D:\retrobat-agent    # then set ROMMBAT_AGENT_ROOT
Publish-ToAgentTree
Connect-AgentTree
Invoke-Agent hooks install; Invoke-Agent menu install
Invoke-Agent sets add smb --scope filter --search "Super Mario Bros. (World)"
Invoke-Agent sync
```

`Connect-AgentTree -Repair` re-pairs, keeping `device.id`, so RomM keeps one device for the tree.

Adopting a new RetroBat stable rebuilds the tree this way from a pristine install of that version,
because a tree upgraded in place is not the build a user installs fresh.

## The scout tree

`ROMMBAT_SCOUT_ROOT` names a second tree the agent owns, which runs a RetroBat **prerelease** for
a scout pass (the `version-adoption` skill) and nothing else. It is rebuilt for each scout, the
same way as the agent tree but from `./tools/retrobat-install.ps1 -Version <tag>`, and the agent
may build, deploy to, pair, sync and drive it without asking. `Use-ScoutTree` points every
function at it, guards included, until `Use-AgentTree` points them back; `Test-HandsOnEnv` then
checks that the tree is **not** at the floor. Only one ES runs at a time, so stop one tree's ES
before starting the other's.

## Functions

| Function                                      | Does                                                                                                |
| --------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| `Test-HandsOnEnv [-Gui]`                      | Preflight: tree, floor, deployed build, pairing, server, and with `-Gui` the session and the screen |
| `Publish-ToAgentTree`                         | `publish.ps1 -Deploy` into the tree, and records branch and sha in `emulators/rommbat/deployed.txt` |
| `Invoke-Agent <args>`                         | The deployed `rommbat-agent.exe` with `--root` set                                                  |
| `Connect-AgentTree [-Repair]`                 | Pairs headlessly, approving with the approver token for exactly the scopes requested                |
| `Start-ES`, `Stop-ES`                         | Start RetroBat and wait for ES's API; end any game, quit, wait for the process to exit              |
| `Get-ESGames <system>`, `Start-Game <path>`   | List a system through ES, and launch through it so the hooks run as for a player                    |
| `Start-EmulatorLauncher`                      | Boot one row through `emulatorLauncher` without ES, so no hooks: `-System -Emulator -Core -Rom`     |
| `Wait-Emulator`, `Get-LauncherDialog`         | Wait for the emulator, answering "install now?"; the handle of a launcher prompt, or nothing       |
| `Stop-Game [-Force]`                          | WM_CLOSE, then Escape, then No to "keep the uncompressed game?". `-Force` ends a deaf emulator     |
| `Start-RomMBatUI`, `Stop-RomMBatUI`           | The deployed `RomMBat.exe` on the tree, standalone                                                  |
| `Send-Key <key> [-Window <proc>] [-HoldMs n]` | `keybd_event` with the scan code. `Ctrl+F2` for a chord. The UI needs `-HoldMs 60`                  |
| `Save-Screenshot <name> [-Window <proc>]`     | A PNG under `probe-output/handson-<date>/`, path returned for the Read tool                         |
| `Assert-TakeoverAllowed [-WhilePlaying]`      | Throws when the session is not Active or ES, an emulator or RomMBat is running                      |
| `Show-AgentBanner`, `Hide-AgentBanner`        | Put up, or take down, the "agent is driving" strip across the top of the screen                     |
| `Use-ScoutTree`, `Use-AgentTree`              | Point every function at the scout tree, or back at the agent tree                                   |

The UI's keys are its desk map: `Up`, `Down`, `Left`, `Right`, `Enter` (A), `Escape` (B),
`Backspace` (L1), `Tab` (X), `F5` (Start).

## Rules the functions enforce

- **The screen is taken only when it is free.** `Start-ES`, `Start-EmulatorLauncher` and
  `Start-RomMBatUI` refuse while ES, `emulatorLauncher` or RomMBat is running, from any tree,
  because the maintainer may be playing over RDP on this machine. Ask them instead.
- **The kit says when it is driving.** The maintainer plays in the same session the kit drives,
  and switching into the RDP window to check on a pass is input Windows cannot tell from play, so
  the kit does not read input to decide. The agent says in chat before it starts driving and when
  it hands the session back, and the kit shows a red strip across the top of the screen while it
  acts: `Send-Key`, `Start-ES`, `Start-EmulatorLauncher`, `Start-RomMBatUI`, `Stop-Game` and
  `Assert-TakeoverAllowed -WhilePlaying` put it up. It is topmost, click-through and never takes
  focus, `Save-Screenshot` hides it for a full-screen capture, and it goes away 3 min after the
  kit last acted, or on `Hide-AgentBanner`. An emulator in exclusive full screen can draw over it.
- **A disconnected session cannot be driven.** With no RDP client attached, screenshots come back
  black and keys go nowhere. `Test-HandsOnEnv -Gui` reports the state; record the GUI half as
  unproven rather than sending keys blind.
- **emulatorLauncher's prompts are answered, not waited out.** A first launch of an emulator
  RetroBat downloads on demand asks "install now?", and an emulator that cannot read a zip ends
  on "keep the uncompressed game?". Neither has a title or a timeout, and neither is a window
  `-Window` can find. `Start-Game` and `Start-EmulatorLauncher` wait for the emulator through
  `Wait-Emulator`, which answers Yes to the first, and `Stop-Game` answers No to the second, so
  `roms/` stays as RomMBat synced it. Each screenshots the prompt first and prints the path.
  `Test-HandsOnEnv -Gui` fails while one waits unanswered, because a stalled launch otherwise
  looks like a slow one. `Stop-Game` never kills an emulator unless given `-Force`, because that
  loses a save it has not written.
- **Look before the next key.** Take a screenshot after each key whose effect matters, and read
  it. A key sent during a fade or a load is lost without an error.
- **A game the maintainer plays is launched from the pad, not with `Start-Game`.** A `/launch`
  made before the pad has touched ES carries no pad, so the pad does nothing in the game
  (RB-419). Set the row, leave ES on its menu, and let the maintainer launch it. `Start-Game` is
  for launches the agent drives with `Send-Key`.
- **Evidence stays local.** Screenshots go under `probe-output/`, which git ignores. The PR's
  Hands-on section names the paths and says what each showed.
