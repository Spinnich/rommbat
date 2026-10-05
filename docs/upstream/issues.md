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
   behaviour on a real install, and the workaround is out of the code and the docs. Only then does
   the entry move to [Retired](#retired), with the date and the pass that proved it.

A **won't fix** retires straight away when nothing in RomMBat depends on it changing, and its
standing constraint is kept in [Retired](#retired) so the reason for the code stays findable.

**Re-read this list whenever a floor moves** (`CLAUDE.md`, "The floor moves forward"), and whenever
a RetroBat or RomM release or prerelease appears: an entry reaching stage 3 is what makes a release
worth adopting sooner.

States below were read from GitHub on **2026-09-27**, apart from romm#4839, read on 2026-09-29, and
emulatorlauncher#1390, read on 2026-10-01.

## Tracked

| Issue                                                                                                     | What it covers                                                                                                                                                                                                                                | Stage                                                                                                                                                                                                                                                                                       | RomMBat meanwhile                                                                                                                                                                                                                                                                                  | Recorded in                                                        |
| --------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| [batocera-emulationstation#2196](https://github.com/batocera-linux/batocera-emulationstation/issues/2196) | ES event scripts do not run once an argument holds a space (`.bat`) or a parenthesis (`.ps1`)                                                                                                                                                 | 1, open. Filed 2026-08-21, moved from [retrobat#249](https://github.com/RetroBat-Official/retrobat/issues/249), which RetroBat closed as an EmulationStation issue                                                                                                                          | **Hooks stay `.exe`.** A fix would reopen the simpler `.bat` journal design                                                                                                                                                                                                                        | RB-342                                                             |
| [emulatorlauncher#1376](https://github.com/RetroBat-Official/emulatorlauncher/issues/1376)                | DirectInput-indexed generators (Kega Fusion, mednafen, Snes9x, Mesen, PCSX2 and others) count a non-controller HID device such as a Logitech LIGHTSPEED receiver, so player 1 lands one pad too high                                          | 2, fixed upstream: closed 2026-09-25, "Fixed". Probably in `beta_8.3.0` (2026-09-26), not checked                                                                                                                                                                                           | A pad that does nothing under such a row on an affected machine is not counted against the row. Measured 2026-09-22 by reading `DirectInputInfo.Controllers` from `EmulatorLauncher.Common.dll`: the receiver's vendor-page collection passes the Usage 4/5 filter, which does not check UsagePage | this register only; RB-284 is the Kega pad mapping it showed up in |
| [emulatorlauncher#1377](https://github.com/RetroBat-Official/emulatorlauncher/issues/1377)                | `nosgba` is handed the zip unextracted, and NO\$GBA cannot open it                                                                                                                                                                            | 2, fixed upstream: closed 2026-09-25, "Done, added zip". Probably in `beta_8.3.0`, not checked                                                                                                                                                                                              | `gba` under `nosgba` is recorded as not certifiable. Its saves also sit outside `saves/`, in `emulators/nosgba/BATTERY/`, which the issue does not cover                                                                                                                                           | RB-286; `platforms/gba/`                                           |
| [emulatorlauncher#1389](https://github.com/RetroBat-Official/emulatorlauncher/issues/1389)                | gopher64's `n64` battery saves stay in `emulators/gopher64/portable_data/data/saves/`, with no mirror into `saves/n64/`                                                                                                                       | 1, open. Filed 2026-09-27                                                                                                                                                                                                                                                                   | RomMBat reads the folder where it is (#239), which a fix here would make a plain `saves/` rule and migration 019 unnecessary                                                                                                                                                                       | RB-341; `platforms/n64/`; #239                                     |
| [emulatorlauncher#1390](https://github.com/RetroBat-Official/emulatorlauncher/issues/1390)                | Kega Fusion's battery saves go to `emulators/kega-fusion/` (`SRMFiles`, `SxMFiles`, `BRMFiles` in RetroBat's `Fusion.ini`), while its states go to `saves/<system>/kega-fusion`                                                               | 2, fixed upstream in part: closed 2026-09-27 as already done by `8172cda4` (2026-09-25), which writes `SRMFiles` into `saves/<system>/` and `StateFiles` into `saves/<system>/kega-fusion/sstates/` per launch, and leaves `SxMFiles` and `BRMFiles`. Probably in `beta_8.3.0`, not checked | RomMBat reads `mastersystem`'s `.ssm` where it is (#381, migration 020), since the fix leaves it there. The `megadrive` `.srm` is not read, so those rows stay not certifiable until the floor carries the fix, which also moves the states the supplement declares                                | RB-283; `platforms/megadrive/`, `platforms/mastersystem/`; #381    |
| [emulatorlauncher#1391](https://github.com/RetroBat-Official/emulatorlauncher/issues/1391)                | `psx` under both BizHawk cores is started on disc 1's `.cue` in place of the `.m3u`, so later discs are unreachable from the game entry                                                                                                       | 1, open. Filed 2026-09-27, the launch line re-captured on 8.2.1 that day                                                                                                                                                                                                                    | Both BizHawk `psx` rows are certified on disc 1; attribution and restore map disc 1's name to the set (RB-332)                                                                                                                                                                                     | RB-314; `platforms/psx/`                                           |
| [romm#4839](https://github.com/rommapp/romm/issues/4839)                                                  | `emulator` on `POST /api/states` is not validated, so a `/` becomes extra directories in `file_path`; `validate_path` still refuses `..` and absolute paths. `build_saves_file_path` joins it the same way, read in the source and not driven | 2, fixed upstream by romm#4851 on 2026-09-28, in no release; `5.3.1` still takes a `/`, re-measured 2026-09-29                                                                                                                                                                              | RomMBat's own schema refuses a separator in that column, so it never sends one                                                                                                                                                                                                                     | RB-133; `romm-api`                                                 |

## Not yet reported

A problem a finding judges upstream's to fix goes here until it is filed, which is the maintainer's
call, and then moves to [Tracked](#tracked).

- **emulatorlauncher: `gamegear` under jgenesis is started with `--hardware MasterSystem`**, so a
  Game Gear cartridge runs to a black screen; `--hardware GameGear` plays it. Judged
  upstream's on 2026-10-05, from the `gamegear` pass. Meanwhile `gamegear` under `jgenesis` is recorded
  as not certifiable. RB-410; `platforms/gamegear/`.

## Retired

| Issue                                                                                      | What it covered                                                                                               | Retired                             | Why                                                                                                                                                                                     |
| ------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------- | ----------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [emulatorlauncher#1336](https://github.com/RetroBat-Official/emulatorlauncher/issues/1336) | Flycast wrote states to `reicast/states`, not the declared `flycast/sstates`                                  | Adopted with RetroBat 8.2.1         | Fixed in 8.2.1 (commit `5fafcb2b`); three runs of a real Dreamcast game on 8.2.1 put the state in both places, and the workaround is out                                                |
| [emulatorlauncher#1337](https://github.com/RetroBat-Official/emulatorlauncher/issues/1337) | BizHawk crashes when `emulatorLauncher` is run without `-core`                                                | Won't fix, 2026-08-11               | Upstream: "there is no reason to run this directly". **Standing constraint: always pass `-core`**, which is correct either way                                                          |
| [romm#4577](https://github.com/rommapp/romm/issues/4577)                                   | `/api/roms/identifiers` built a full ORM object per ROM to return ids, which took a live instance to 20.9 GiB | Adopted with RomM 5.3.1, 2026-09-28 | Fixed by romm#4586 and romm#4589 in 5.3.0; five live calls on 5.3.1 answered 95,989 ids in under a second (RB-81). RomMBat still does not call it, because it cannot be scoped to a set |

Also investigated and not filed, because it is not RetroBat's: openMSX once missed Alt+F2 because
NVIDIA's Photo mode overlay claimed the combination first.

## RB-342. #2196: the ES hook bug, moved repository

It was filed as `RetroBat-Official/retrobat#249` because
`RetroBat-Official/emulationstation` is a fork of `batocera-linux/batocera-emulationstation`
with **issues disabled**, so there was nowhere else for an ES-behaviour report to go.

**It was filed before its mechanism was known.** It described a `.bat` hook not running when
the display name contains a space. Probe 7b showed ES fires the event correctly and the fault
is in the handoff to an interpreter, that it also breaks `.ps1` hooks on any parenthesis, and
that both failures reproduce outside EmulationStation. The issue was
[updated with the mechanism](https://github.com/RetroBat-Official/retrobat/issues/249#issuecomment-5232474774)
on 2026-08-09, including two verified fixes (`cmd /s /c "<whole command>"` for `.bat`, `-File`
for `.ps1`) and a suggested retitle, since the original title describes the `.bat` symptom
only.

RetroBat closed #249 on 2026-08-21 as an upstream EmulationStation issue, and it was refiled
the same day at
[batocera-emulationstation#2196](https://github.com/batocera-linux/batocera-emulationstation/issues/2196).
The mechanism, the retitle and the two verified fixes carried across
unchanged. **Nothing about the design moves**: the hooks stay `.exe`, and a fix landing would
reopen the simpler `.bat` journal design the plan originally wanted.

## RB-343. #1336: fixed in 8.2.1, driven, and the workaround is out

RetroBat 8.2.1 (2026-08-23) lists `FLYCAST: fix savestates` in its changelog. The fix is
[commit `5fafcb2b`](https://github.com/RetroBat-Official/emulatorlauncher/commit/5fafcb2b), one
line in `Flycast.Generator.cs`:

```csharp
- string emulatorPath = Path.Combine(path, "data");
+ string emulatorPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "reicast", "states");
```

So the mechanism was not the one this finding assumed. A `FlycastSaveStatesMonitor` was already
there in 8.2.0, doing for Flycast what the mirroring described above does for the other
non-`libretro` emulators. It was watching the emulator's own `data` directory, which Flycast
never writes states to, so the mirror never fired and the declared directory stayed empty.
Pointing the watcher at `saves/<system>/reicast/states` should make a state appear under the
declared `saves/<system>/flycast/sstates` as well.

**Flycast still writes `reicast/states` first**, and `Dreamcast.SavestatePath` still names it:
the generator's path composition is unchanged, and 8.2.1's `es_savestates.cfg` is byte-identical
to 8.2.0's, so the declaration was not moved either. What changed is that the declared directory
is now expected to be populated rather than to stay empty.

**Confirmed by hand on 8.2.1, and the workaround is out.**
`tools/m0-probes/probe2-flycast-mirror.ps1` was run three times against `K:\RetroBat` with
`Sega Tetris (Japan) (Rev A).chd`, Flycast 2.7, on 2026-08-25:

|                                    |                                                                       |
| ---------------------------------- | --------------------------------------------------------------------- |
| Written natively                   | `saves/dreamcast/reicast/states/Sega Tetris (Japan) (Rev A)_1.state`  |
| Mirrored to the declared directory | `saves/dreamcast/flycast/sstates/Sega Tetris (Japan) (Rev A)_1.state` |
| Size, both                         | identical, 1,541,183 / 1,541,250 / 1,541,372 B across the runs        |
| Timing                             | **the same millisecond**, while the emulator was still running        |
| Declared `<image>`                 | **absent**, all three runs                                            |
| `.txt` sidecar                     | present, holding the rom filename                                     |

`Dreamcast.SavestatePath` in the generated `emulators/flycast/emu.cfg` still reads
`saves\dreamcast\reicast\states`, so the emulator's own path is unchanged and the fix is
purely the mirror. Two details corroborate the mechanism rather than just the outcome:
`flycast/sstates` **did not exist** before the first launch and was created by the launcher's
`PrepareEmulatorRepository()`, and the mirror timing matches what probe 2 measured for the
other non-`libretro` emulators, about 120 ms, live rather than at exit.

So `flycast` is out of `StateScanner.WrongDeclaredDirectories` and into the verified list in
`data/retrobat/save_directories.json`. **Dreamcast states now sync.** This is a save-shape
check, not a certification: `(dreamcast, flycast)` still owes the other eight steps.

**The general rule survives the fix.** "Do not treat `es_savestates.cfg`'s `<directory>` as
authoritative on its own" was never only about Flycast: `openmsx` still writes
`bios/openmsx/savestates/`, a different top-level tree, and that is unfixed. One of twelve is
still one.

**A by-product worth recording.** Getting a Dreamcast game onto the install meant pulling
`dc_boot.bin` and `dc_flash.bin` out of RomM's firmware endpoint, and both arrived with md5s
matching `data/retrobat/bios.json` exactly (`e10c53c2…`, `0a93f7940…`). That is the
md5-only BIOS join working end to end on real data, on a system whose third manifest entry
(`bios/dc/dc.zip`) carries no hash at all.

## RB-344. #1337: will not be fixed, and it costs RomMBat nothing

It was the low-severity one, deliberately reported as such. EmulationStation always passes a
core, and all 36 BizHawk cores this install's `es_systems.cfg` declares are among the 42 keys in
`inputPortNb`, so only direct invocation or a future unlisted core can reach it.

Upstream closed it on 2026-08-11, saying they will not fix it because there is no reason to run
`emulatorLauncher` directly. RomMBat **is** a direct invoker, so the constraint stands, and it is
now a permanent property of the launcher rather than a workaround waiting on a fix: **pass
`-core`**, which was always correct anyway.

## RB-345. The standing rule

**Re-check every open issue here before each release**, because a fix upstream does not just
close a ticket, it changes what RomMBat should do. No workaround comes out until the fix is in a
release RomMBat's compatibility gate accepts and a hands-on pass has seen the fixed behaviour: a
changelog line is evidence that upstream believes it is fixed, not evidence of what lands on
disk.
