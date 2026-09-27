# Upstream issues

Every issue RomMBat has raised with a project it depends on, and every problem it has decided is
someone else's to fix but not yet reported. This is the one list. The findings docs and the
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

States below were read from GitHub on **2026-09-27**.

## Tracked

| Issue                                                                                                     | What it covers                                                                                                                                                                                       | Stage                                                                                                                                                              | RomMBat meanwhile                                                                                                                                                                                                                                                                                  | Recorded in                                                             |
| --------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| [batocera-emulationstation#2196](https://github.com/batocera-linux/batocera-emulationstation/issues/2196) | ES event scripts do not run once an argument holds a space (`.bat`) or a parenthesis (`.ps1`)                                                                                                        | 1, open. Filed 2026-08-21, moved from [retrobat#249](https://github.com/RetroBat-Official/retrobat/issues/249), which RetroBat closed as an EmulationStation issue | **Hooks stay `.exe`.** A fix would reopen the simpler `.bat` journal design                                                                                                                                                                                                                        | `retrobat-findings.md`, "Upstream issues filed"; `README.md`            |
| [emulatorlauncher#1376](https://github.com/RetroBat-Official/emulatorlauncher/issues/1376)                | DirectInput-indexed generators (Kega Fusion, mednafen, Snes9x, Mesen, PCSX2 and others) count a non-controller HID device such as a Logitech LIGHTSPEED receiver, so player 1 lands one pad too high | 2, fixed upstream: closed 2026-09-25, "Fixed". Probably in `beta_8.3.0` (2026-09-26), not checked                                                                  | A pad that does nothing under such a row on an affected machine is not counted against the row. Measured 2026-09-22 by reading `DirectInputInfo.Controllers` from `EmulatorLauncher.Common.dll`: the receiver's vendor-page collection passes the Usage 4/5 filter, which does not check UsagePage | this register only; finding 284 is the Kega pad mapping it showed up in |
| [emulatorlauncher#1377](https://github.com/RetroBat-Official/emulatorlauncher/issues/1377)                | `nosgba` is handed the zip unextracted, and NO$GBA cannot open it                                                                                                                                    | 2, fixed upstream: closed 2026-09-25, "Done, added zip". Probably in `beta_8.3.0`, not checked                                                                     | `gba` under `nosgba` is recorded as not certifiable. Its saves also sit outside `saves/`, in `emulators/nosgba/BATTERY/`, which the issue does not cover                                                                                                                                           | finding 286; `platforms/gba.md`                                         |
| [emulatorlauncher#1389](https://github.com/RetroBat-Official/emulatorlauncher/issues/1389)                | gopher64's `n64` battery saves stay in `emulators/gopher64/portable_data/data/saves/`, with no mirror into `saves/n64/`                                                                              | 1, open. Filed 2026-09-27                                                                                                                                          | `n64` under `gopher64` is recorded as not certifiable. RomMBat #239 reads the folder where it is, which a fix here would make a plain `saves/` rule                                                                                                                                                | finding 341; `platforms/n64.md`; #239                                   |
| [romm#4577](https://github.com/rommapp/romm/issues/4577)                                                  | `/api/roms/identifiers` builds a full ORM object per ROM to return ids, which took a live instance to 20.9 GiB                                                                                       | 3, probably: closed 2026-09-17 by romm#4586 and romm#4589, before `5.3.0`. Not re-measured on the `5.3.1` floor                                                    | The client method, its live test and the probes that called it are removed. Bringing them back is the adoption                                                                                                                                                                                     | `romm-5.3-findings.md`; `docs/PLAN.md`                                  |

## Not yet reported

Each of these was judged upstream's to fix in the finding that measured it, and none has been
filed. Filing one is the maintainer's call; when it is filed it moves to [Tracked](#tracked).

| Where                               | What                                                                                                                                                                         | Measured in                                                        |
| ----------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| RetroBat, `Fusion.ini` template     | Kega Fusion's battery saves go to `emulators/kega-fusion/`, outside `saves/`, while its states go to `saves/<system>/kega-fusion`. The same class as #1389                   | finding 283; `platforms/megadrive.md`, `platforms/mastersystem.md` |
| emulatorLauncher, BizHawk generator | `psx` under both BizHawk cores is handed disc 1's `.cue` in place of the `.m3u`, so disc 2 is unreachable from the game entry                                                | finding 314; `platforms/psx.md`                                    |
| RomM, `POST /api/states`            | The `emulator` value is not sanitised: `libretro/evil` became two path segments in the stored `file_path`. RomMBat cannot send one, since its own schema refuses a separator | finding 133; `docs/PLAN.md`                                        |

## Retired

| Issue                                                                                      | What it covered                                                              | Retired                     | Why                                                                                                                                      |
| ------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------- | --------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| [emulatorlauncher#1336](https://github.com/RetroBat-Official/emulatorlauncher/issues/1336) | Flycast wrote states to `reicast/states`, not the declared `flycast/sstates` | Adopted with RetroBat 8.2.1 | Fixed in 8.2.1 (commit `5fafcb2b`); three runs of a real Dreamcast game on 8.2.1 put the state in both places, and the workaround is out |
| [emulatorlauncher#1337](https://github.com/RetroBat-Official/emulatorlauncher/issues/1337) | BizHawk crashes when `emulatorLauncher` is run without `-core`               | Won't fix, 2026-08-11       | Upstream: "there is no reason to run this directly". **Standing constraint: always pass `-core`**, which is correct either way           |

Also investigated and not filed, because it is not RetroBat's: openMSX once missed Alt+F2 because
NVIDIA's Photo mode overlay claimed the combination first.
