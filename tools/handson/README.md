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

`ROMMBAT_AGENT_ROOT` in the main checkout's `.env` names it, beside `ROMMBAT_RETROBAT_ROOT`, the
maintainer's install, which this kit never touches. The tree is paired as the approver test
account, so what it uploads stays apart from the maintainer's own saves. The agent may deploy to
it, pair, sync, reset and drive it without asking ([workflow](../../docs/contributing/workflow.md)).

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

## Functions

| Function                                      | Does                                                                                                |
| --------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| `Test-HandsOnEnv [-Gui]`                      | Preflight: tree, floor, deployed build, pairing, server, and with `-Gui` the session and the screen |
| `Publish-ToAgentTree`                         | `publish.ps1 -Deploy` into the tree, and records branch and sha in `emulators/rommbat/deployed.txt` |
| `Invoke-Agent <args>`                         | The deployed `rommbat-agent.exe` with `--root` set                                                  |
| `Connect-AgentTree [-Repair]`                 | Pairs headlessly, approving with the approver token for exactly the scopes requested                |
| `Start-ES`, `Stop-ES`                         | Start RetroBat and wait for ES's API; end any game, quit, wait for the process to exit              |
| `Get-ESGames <system>`, `Start-Game <path>`   | List a system through ES, and launch through it so the hooks run as for a player                    |
| `Stop-Game`                                   | WM_CLOSE to the emulator, then Escape. `/emukill` does nothing while a game runs (RB-35)            |
| `Start-RomMBatUI`, `Stop-RomMBatUI`           | The deployed `RomMBat.exe` on the tree, standalone                                                  |
| `Send-Key <key> [-Window <proc>] [-HoldMs n]` | `keybd_event` with the scan code. `Ctrl+F2` for a chord. The UI needs `-HoldMs 60`                  |
| `Save-Screenshot <name> [-Window <proc>]`     | A PNG under `probe-output/handson-<date>/`, path returned for the Read tool                         |
| `Assert-TakeoverAllowed`                      | Throws when the session is not Active or ES, an emulator or RomMBat is running                      |

The UI's keys are its desk map: `Up`, `Down`, `Left`, `Right`, `Enter` (A), `Escape` (B),
`Backspace` (L1), `Tab` (X), `F5` (Start).

## Rules the functions enforce

- **The screen is taken only when it is free.** `Start-ES` and `Start-RomMBatUI` refuse while ES,
  `emulatorLauncher` or RomMBat is running, from any tree, because the maintainer may be playing
  over RDP on this machine. Ask them instead.
- **A disconnected session cannot be driven.** With no RDP client attached, screenshots come back
  black and keys go nowhere. `Test-HandsOnEnv -Gui` reports the state; record the GUI half as
  unproven rather than sending keys blind.
- **Look before the next key.** Take a screenshot after each key whose effect matters, and read
  it. A key sent during a fade or a load is lost without an error.
- **Evidence stays local.** Screenshots go under `probe-output/`, which git ignores. The PR's
  Hands-on section names the paths and says what each showed.
