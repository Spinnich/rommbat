---
summary: Every upstream bug RomMBat has raised or decided is someone else's to fix, its state, and what RomMBat does meanwhile.
read-when: Before working around an upstream bug, filing one, or moving the RomM or RetroBat floor.
---

# Upstream issues

Every issue RomMBat has raised with a project it depends on, and every problem it has decided is
someone else's to fix but not yet reported. This is the one list. The facts beside it and the
platform records keep the measurement behind each entry and link here for its state.

## When an entry leaves this list

**An issue is tracked until RomMBat has taken advantage of the fix, not until upstream closes it.**
Closing means upstream believes it is fixed; what reaches a user is a release. So an entry moves
through four stages and retires only at the last:

1. **Open**, upstream.
2. **Fixed upstream**: closed as done, or a fix merged, in no release yet.
3. **In a release**: the tag that first carries the fix, read from the release and its commits,
   not assumed from dates.
4. **Adopted**: RomMBat's floor has moved to that release, a hands-on pass has seen the fixed
   behavior on a real install, and the workaround is out of the code and the docs. A changelog
   line shows upstream believes it is fixed, and only the pass shows what lands on disk. Only then
   does the entry move to [Retired](#retired), with the date and the pass that proved it.

A **won't fix** retires straight away when nothing in RomMBat depends on it changing, and its
standing constraint is kept in [Retired](#retired) so the reason for the code stays findable.

**Re-read this list whenever a floor moves** (`CLAUDE.md`, "The floor moves forward"), before each
RomMBat release, and whenever a RetroBat or RomM release or prerelease appears: an entry reaching
stage 3 is what makes a release worth adopting sooner.

States below were read from GitHub on **2026-10-14**, against RetroBat 8.3.0 and RomM 5.4.0.

## Tracked

| Issue                                                                                                     | What it covers                                                                                                                                                                                                                                | Stage                                                                                                                                                                                                                                                                       | RomMBat meanwhile                                                                                                                                                                                                                                                                                  | Recorded in                                                        |
| --------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| [batocera-emulationstation#2196](https://github.com/batocera-linux/batocera-emulationstation/issues/2196) | ES event scripts do not run once an argument holds a space (`.bat`) or a parenthesis (`.ps1`)                                                                                                                                                 | 1, open. Filed 2026-08-21, moved from [retrobat#249](https://github.com/RetroBat-Official/retrobat/issues/249), which RetroBat closed as an EmulationStation issue                                                                                                          | **Hooks stay `.exe`.** A fix would reopen the simpler `.bat` journal design                                                                                                                                                                                                                        | RB-342                                                             |
| [emulatorlauncher#1376](https://github.com/RetroBat-Official/emulatorlauncher/issues/1376)                | DirectInput-indexed generators (Kega Fusion, mednafen, Snes9x, Mesen, PCSX2 and others) count a non-controller HID device such as a Logitech LIGHTSPEED receiver, so player 1 lands one pad too high                                          | 3, in RetroBat 8.3.0, whose changelog ships it as "Directinput devices: only enumerate attached devices". Closed 2026-09-25. Not measured: it needs the receiver                                                                                                            | A pad that does nothing under such a row on an affected machine is not counted against the row. Measured 2026-09-22 by reading `DirectInputInfo.Controllers` from `EmulatorLauncher.Common.dll`: the receiver's vendor-page collection passes the Usage 4/5 filter, which does not check UsagePage | this register only; RB-284 is the Kega pad mapping it showed up in |
| [emulatorlauncher#1377](https://github.com/RetroBat-Official/emulatorlauncher/issues/1377)                | `nosgba` is handed the zip unextracted, and NO\$GBA cannot open it                                                                                                                                                                            | 3, in RetroBat 8.3.0. Closed 2026-09-25; a zip booted Pokemon Emerald on the `beta_8.3.0` scout tree, and `fc453066` mirrors its saves into `saves/gba/`                                                                                                                    | `gba` under `nosgba` stays not certified until its first certification at 8.3.0 (#520)                                                                                                                                                                                                             | RB-286; `platforms/gba/`                                           |
| [emulatorlauncher#1389](https://github.com/RetroBat-Official/emulatorlauncher/issues/1389)                | gopher64's `n64` battery saves stay in `emulators/gopher64/portable_data/data/saves/`, with no mirror into `saves/n64/`                                                                                                                       | 3, in RetroBat 8.3.0. Closed 2026-10-05; `453c3138` copies the folder to `saves/n64/gopher64/` at exit and back at launch, measured 2026-10-14 (RB-341)                                                                                                                     | RomMBat still reads the portable folder where it is (#239) and does not read the copy, which `saves` lists as not syncable (#524)                                                                                                                                                                  | RB-341; `platforms/n64/`; #239                                     |
| [emulatorlauncher#1390](https://github.com/RetroBat-Official/emulatorlauncher/issues/1390)                | Kega Fusion's battery saves go to `emulators/kega-fusion/` (`SRMFiles`, `SxMFiles`, `BRMFiles` in RetroBat's `Fusion.ini`), while its states go to `saves/<system>/kega-fusion`                                                               | 3, in RetroBat 8.3.0. Closed 2026-09-27 as already done by `8172cda4`, which writes `SRMFiles` into `saves/<system>/` and `StateFiles` into `saves/<system>/kega-fusion/sstates/` per launch, and leaves `SxMFiles` and `BRMFiles`; measured on the `beta_8.3.0` scout tree | RomMBat reads `mastersystem`'s `.ssm` where it is (#381, migration 020), since the fix leaves it there. The loose `.srm` is read as `libretro:battery` on `megadrive` and `sega32x`, but the supplement's state directory is now wrong, so those rows stay not certified until #519                | RB-283; `platforms/megadrive/`, `platforms/mastersystem/`; #381    |
| [emulatorlauncher#1391](https://github.com/RetroBat-Official/emulatorlauncher/issues/1391)                | `psx` under both BizHawk cores is started on disc 1's `.cue` in place of the `.m3u`, so later discs are unreachable from the game entry                                                                                                       | 3, in RetroBat 8.3.0. Closed 2026-10-07; the generator no longer swaps the `.m3u`, measured 2026-10-14 (RB-314)                                                                                                                                                             | Both BizHawk `psx` rows hold at 8.2.1, with steps 2, 4 and 5 owed at 8.3.0 (#523); attribution and restore still map disc 1's name to the set (RB-332)                                                                                                                                             | RB-314; `platforms/psx/`                                           |
| [emulatorlauncher#1398](https://github.com/RetroBat-Official/emulatorlauncher/issues/1398)                | `gamegear` under jgenesis is started with `--hardware MasterSystem`, so a Game Gear cartridge runs to a black screen; `--hardware GameGear` plays it                                                                                          | 3, in RetroBat 8.3.0. Closed 2026-10-07 by `37e1b4ed`, which maps `gamegear` to `GameGear`; read in source                                                                                                                                                                  | `gamegear` under `jgenesis` stays not certified until #522                                                                                                                                                                                                                                         | RB-410; `platforms/gamegear/`                                      |
| [romm#4839](https://github.com/rommapp/romm/issues/4839)                                                  | `emulator` on `POST /api/states` is not validated, so a `/` becomes extra directories in `file_path`; `validate_path` still refuses `..` and absolute paths. `build_saves_file_path` joins it the same way, read in the source and not driven | 3, in RomM 5.4.0. Fixed by romm#4851 on 2026-09-28, which 5.4.0 carries; `5.3.1`, the floor, still takes a `/`                                                                                                                                                              | RomMBat's own schema refuses a separator in that column, so it never sends one                                                                                                                                                                                                                     | RB-133; `romm-api`                                                 |

## Not yet reported

A problem a finding judges upstream's to fix goes here until it is filed, which is the
maintainer's call, and then moves to [Tracked](#tracked).

| Problem                                                                                                                                                                                                   | Project                     | RomMBat meanwhile                                                                                               | Recorded in |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------- | --------------------------------------------------------------------------------------------------------------- | ----------- |
| `POST /launch` answers 200 and starts nothing for a grouped parent system's own games, since f600b969 skips any game whose system is a group system. Off by default: RetroBat ships every group ungrouped | RetroBat's EmulationStation | `Start-Game` refuses such a game and names the `<system>.ungroup` setting. RomMBat itself never calls `/launch` | RB-433      |

## Retired

| Issue                                                                                      | What it covered                                                                                               | Retired                             | Why                                                                                                                                                                                                               |
| ------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------- | ----------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [emulatorlauncher#1336](https://github.com/RetroBat-Official/emulatorlauncher/issues/1336) | Flycast wrote states to `reicast/states`, not the declared `flycast/sstates`                                  | Adopted with RetroBat 8.2.1         | Fixed in 8.2.1 (commit `5fafcb2b`); three runs of a real Dreamcast game on 8.2.1 put the state in both places, and the workaround is out                                                                          |
| [emulatorlauncher#1337](https://github.com/RetroBat-Official/emulatorlauncher/issues/1337) | BizHawk crashed when `emulatorLauncher` was run without `-core`                                               | Won't fix, 2026-08-11               | Upstream: "there is no reason to run this directly", yet `cf6af506`, in 8.2.1, made the lookup fall back to one input port (RB-374). **Standing constraint: always pass `-core`**, which gives every player a pad |
| [romm#4577](https://github.com/rommapp/romm/issues/4577)                                   | `/api/roms/identifiers` built a full ORM object per ROM to return ids, which took a live instance to 20.9 GiB | Adopted with RomM 5.3.1, 2026-09-28 | Fixed by romm#4586 and romm#4589 in 5.3.0; five live calls on 5.3.1 answered 95,989 ids in under a second (RB-81). RomMBat still does not call it, because it cannot be scoped to a set                           |

Also investigated and not filed, because it is not RetroBat's: openMSX once missed Alt+F2 because
NVIDIA's Photo mode overlay claimed the combination first.

## RB-342. #2196: the ES hook bug, filed with Batocera

Verified: RetroBat 8.3.0, 2026-10-14. How: read the issue and its comments on GitHub; read `Scripting.cpp` at ES `45f37fd7`, unchanged since 8.2.1's `c686ca8b`.
`RetroBat-Official/emulationstation` is a fork of `batocera-linux/batocera-emulationstation`
with issues disabled, so an ES-behavior report goes to Batocera. RetroBat closed
`RetroBat-Official/retrobat#249` as an EmulationStation issue. #2196 holds the original
`.bat` report, and a comment carries the mechanism (RB-397) and two verified fixes:
`cmd /s /c "<whole command>"` for `.bat`, and `-File` for `.ps1`. 8.3.0's ES still starts a
`.ps1` as `powershell <command>`, with no `-File`. The hooks stay `.exe` (RB-396), and a fix
would reopen the simpler `.bat` journal design.

## RB-343. #1336: Flycast's states reach the declared directory through the launcher's mirror

Verified: RetroBat 8.2.1, 2026-08-25, driven, and 8.3.0, 2026-10-14, in source. How: ran `tools/m0-probes/probe2-flycast-mirror.ps1` three times with `Sega Tetris (Japan) (Rev A).chd`; read `Flycast.Generator.cs` at `79f5b1aa`, the launcher's head when 8.3.0 was tagged, and 8.3.0's `es_savestates.cfg`.
Flycast writes its states to `saves/<system>/reicast/states`, which `Dreamcast.SavestatePath`
in the generated `emu.cfg` names. `FlycastSaveStatesMonitor` watches that directory and copies
each state into the declared `saves/<system>/flycast/sstates` under the declared
`{{romfilename}}_{{slot0}}.state`, in the same millisecond and while the emulator runs. The
declared `<image>` never appears, and a `.txt` sidecar holds the rom filename.

So `flycast` is in the verified list in `data/retrobat/save_directories.json`, and Dreamcast
states sync. `openmsx` still writes outside its declared directory (RB-371), so
`es_savestates.cfg`'s `<directory>` is not authoritative on its own.
