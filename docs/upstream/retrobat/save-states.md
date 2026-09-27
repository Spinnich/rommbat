---
summary: What `es_savestates.cfg` declares against what emulators write: directories, slots, screenshots and `.txt` sidecars.
read-when: Before reading `es_savestates.cfg`, deriving a state slot, or syncing a state's companions.
---

# RetroBat: save states

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-3. `libretro`, the most important entry, declares no slot bounds at all

Plan says: `es_savestates.cfg` "yields the slot bounds" (L802-806)

Measurement says: `libretro`, the most important entry, declares no slot bounds at all

## RB-4. True, and `bizhawk` is core-scoped too

Plan says: The `libretro` directory is core-scoped (L811)

Measurement says: True, and `bizhawk` is core-scoped too

## RB-5. True except `desmume`, where `<image>` and `<file>` are the same template

Plan says: `<image>` maps directly onto `screenshotFile` (L804-805)

Measurement says: True except `desmume`, where `<image>` and `<file>` are the same template

## RB-38. For PPSSPP the mirrored screenshot is racy: observed correct, zero-byte and entirely absent across three

Plan says: `<image>` maps onto `screenshotFile` (L804-805)

Measurement says: For PPSSPP the mirrored screenshot is **racy**: observed correct, zero-byte and entirely absent across three saves, because the watcher copies before the emulator has written it. Treat as best-effort

## RB-39. The `.txt` beside a PPSSPP state

Plan says: (not addressed) the `.txt` beside a PPSSPP state

Measurement says: It is the **name mapping** to the native scheme (`UCES00995_1.00`), not a save and not disposable. Sync it with the state. Present for exactly those emulators whose native naming differs from the rom

## RB-40. True for `<file>`, which held for all 7 driven emulators, but not for `<directory>`: `flycast` writes

Plan says: `es_savestates.cfg` is the authority on state paths (`retrobat-layout`, L802-806)

Measurement says: True for `<file>`, which held for all 7 driven emulators, but **not for `<directory>`**: `flycast` writes `reicast/states` while the file declares `flycast/sstates`, and the declared dir sits empty

## RB-42. It is absent more often than present: missing in 4 of 7 driven emulators, and correct/zero-byte/missing

Plan says: `<image>` is a normal optional field (L804-805)

Measurement says: It is **absent more often than present**: missing in 4 of 7 driven emulators, and correct/zero-byte/missing across three runs of the same PPSSPP game. Best-effort everywhere

## RB-51. Retracted

Plan says: The `.txt` sidecar appears exactly where native naming differs from the rom filename (RB-39)

Measurement says: **Retracted.** `jgenesis` and `desmume` both wrote one containing the rom filename itself, so it is written unconditionally. Its content is still the mapping and still has to travel with the state

## RB-53. Correct, and the mirror is what makes it so

Plan says: `bizhawk`'s directory is core-scoped like `libretro`'s (RB-4 above)

Measurement says: Correct, and the mirror is what makes it so. **Natively** BizHawk writes to `emulators/bizhawk/sstates/<system>/`, outside `saves/` and **not** core-scoped; RetroBat mirrors that to the declared path

## RB-55. Whether everything beside a state round-trips

Plan says: (not addressed) whether everything beside a state round-trips

Measurement says: No. BizHawk writes a `.State.rap` sibling natively that is **not** mirrored to the ES-facing directory and is not recreated on sync-in

## RB-56. `openmsx` is a second, and worse

Plan says: `flycast` is the only wrong `<directory>` (RB-40)

Measurement says: **`openmsx` is a second, and worse.** It writes to `bios/openmsx/savestates/`, a different top-level tree from the declared `saves/msx1/openmsx`, which stayed empty across two real saves

## RB-57. Still true as a rule, but openMSX writes a real 7.5 KB `.png` beside every state, so the field is worth

Plan says: `<image>` is best-effort everywhere (RB-42)

Measurement says: Still true as a rule, but openMSX writes a real 7.5 KB `.png` beside every state, so the field is worth reading rather than skipping

## RB-134. Confirmed end to end, and the scoped name holds

Previously: Two libretro cores writing one filename would collide server-side (126, 127)

Measurement says: **Confirmed end to end, and the scoped name holds.** `genesis_plus_gx` and `picodrive` both wrote `Phantasy Star (Brazil).state1` for one ROM and landed as two rows, 9,202 B and 6,282 B. Without the scope in the uploaded name one would have replaced the other

## RB-135. Not slot 0, and not fixed

Previously: (not addressed) which slot a save-state hotkey writes

Measurement says: **Not slot 0, and not fixed.** libretro wrote `.state1` and BizHawk wrote `.QuickSave2.State`. Reading the slot off the filename is what makes both work; expanding `firstslot..lastslot` would have found neither

## RB-136. Wrong, and this corrects it

Previously: The `.txt` sidecar is emitted unconditionally (probe 2, retracted reading)

Measurement says: **Wrong, and this corrects it.** `libretro` wrote none under either core. `jgenesis` wrote the plain rom filename; `bizhawk` wrote `Phantasy Star (B).SMSHawk`, its own truncated name plus the core. Absence and presence both signal nothing; only the contents are ever useful

## RB-215. Whether RetroBat ever leaves `sort_savestates_enable` unset, whose absent default is on

Question: (not addressed) whether RetroBat ever leaves `sort_savestates_enable` unset, whose absent default is **on**

Measured: **Never.** `emulatorlauncher` writes all four sort keys explicitly as `"false"` on every launch (`sort_savefiles_enable`, `sort_savefiles_by_content_enable`, `sort_savestates_enable`, `sort_savestates_by_content_enable`) and puts the core in the path instead: `savestate_directory = "<root>\saves\mastersystem\libretro.genesis_plus_gx"`. So the asymmetric default that misplaces a state on other front ends (savestates on, savefiles off) cannot arise here, and nothing in RomMBat needs to read that key

## RB-216. What the on-disk core folder is actually named

Question: (not addressed) what the on-disk core folder is actually named

Measured: **`libretro.<core>`**, RetroBat's own convention, not the libretro `corename` a front end reading `retroarch.cfg` would produce. Four cores on disk agree: `mastersystem/libretro.genesis_plus_gx`, `mastersystem/libretro.picodrive`, `psx/libretro.mednafen_psx_hw`, `ports/libretro.2048`. This is what `es_savestates.cfg` already declares as `{{system}}/libretro.{{core}}`, so the manifest was the right source and `retroarch.cfg` never needed reading

## RB-217. No, it describes only the last game launched

Question: (not addressed) whether `retroarch.cfg` is safe to read as a description of the install

Measured: **No, it describes only the last game launched.** The copy read named `mastersystem` throughout, including in `savefile_directory`, because that was the last session. It is regenerated per launch, which is rule 2 seen from the reading side rather than the writing side

## RB-251. False, and nothing in the file hints otherwise

Question: An emulator with no `es_savestates.cfg` entry writes no save states

Measured: **False, and nothing in the file hints otherwise.** Driven on `nes`: `mednafen` wrote `saves/nes/mednafen/sstates/<rom>.<md5>.mc0`, `mesen` wrote `saves/nes/mesen/SaveStates/<rom>_1.mss`, `ares` wrote `saves/nes/ares/Famicom/<rom>.bs1`. `StateScanner` works from `es_savestates.cfg` alone, so none is scanned, uploaded or restorable (#150). They are not silent: `SaveScanner.CountFiles` excludes only **declared** state directories, so these are counted and named. They were counted under a reason string promising that the save states beside them sync, which #150 fixed by splitting `AddSubdirectories` into a declared row and a `no_state_declaration` one

## RB-261. No. RetroArch does, from what is already in the core's state directory

Question: Whether ES's `-state_slot` decides a `libretro` state's slot suffix (**258**)

Measured: **No. RetroArch does, from what is already in the core's state directory.** `nes` under `libretro`/`mesen` was launched on The Legend of Zelda (USA) (Rev 1) with `-state_slot 5` on the `emulatorLauncher.log` line, and the two states made in the session landed as `.state1` and `.state2`. RetroArch's own log, `es_launch_stdout.log`, says why: `found_last_state_slot: #0` against an empty `saves/nes/libretro.mesen/`, then `Saving state ... .state1` and `... .state2`. The `retroarch.cfg` written for that launch carries `savestate_auto_index = "true"`, which starts from the highest slot on disk for that game and core. The `nestopia` pass that RB-258 records, `-state_slot 2` and states 2, 3 and 4, fits this reading as well as the one written there, because slot 1 already existed in `libretro.nestopia/`; it was coincidence, not cause. Where ES's number comes from is not measured, and `5` matches the four `nestopia` states the game already held. Nothing in `src/` reads `-state_slot`, so this corrects the docs and not the code. For a `libretro` row, read the slot from the `Saving state` lines in `es_launch_stdout.log` or from the file on disk, never from the launch line

## RB-268. No, never, and the screenshot is inside the state instead

Question: Whether BizHawk writes the `<image>` `es_savestates.cfg` declares for it, `{{romfilename}}.QuickSave{{slot0}}.png`

Measured: **No, never, and the screenshot is inside the state instead.** Five states across two cores, and none has a `.png` in the declared directory or in BizHawk's own `emulators/bizhawk/sstates/nes/`. A `.State` is a zip of `BizState 1.0`, `BizVersion.txt`, `Core.bin.zst`, `SyncSettings.json` and **`Framebuffer.bmp`**, 256x224, 229,430 B before compression, which is the frame at the moment of saving. Two states on different screens carry different framebuffers (`6e27d64b...` and `bdeafc1e...` for Destiny of an Emperor). So a state that round-trips byte for byte brings its screenshot with it, RomM holds no screenshot row for a BizHawk state, its UI shows none, and the restore preview's `no screenshot: the server links none to this state` is correct rather than a missed link. The declaration is RetroBat's, and nothing reads it for this emulator

## RB-269. Yes, the reverse of `libretro`

Question: Whether ES's `-state_slot` decides a `bizhawk` state's slot (**261**)

Measured: **Yes, the reverse of `libretro`.** `emulatorLauncher` writes it into EmuHawk's `config.ini` as `SaveSlot`: a launch with `-state_slot 5` left `SaveSlot: 5` and the pad's save-state key wrote `QuickSave5`, while a launch with no `-state_slot` left `SaveSlot: 10` and the same key wrote `QuickSave0`. The pad key is `F2` sent through pad-to-key (`[SendKey] Press 'KEY_F2' to emuhawk` in `emulatorLauncher.log`), so it always saves to the current slot. Choosing a slot from inside the game is keyboard only: `Ctrl+F1` to `Ctrl+F10` save to slots 1 to 10 outright, and `F6` and `F7` step the slot. EmuHawk reads the keyboard through DirectInput, so a key sent by `WScript.Shell.SendKeys` is ignored and one sent by `keybd_event` with the hardware scan code is not. For a `bizhawk` row, read the slot from the file on disk

## RB-270. No, and the next launch through `emulatorLauncher` removes it

Question: Whether a BizHawk state written without `emulatorLauncher` running reaches the declared directory

Measured: **No, and the next launch through `emulatorLauncher` removes it.** EmuHawk started directly with `--lua` wrote `Destiny of an Emperor.NesHawk.QuickSave2.State` into `emulators/bizhawk/sstates/nes/` and nothing appeared under `saves/nes/bizhawk/sstates/NesHawk/`. The next launch through `emulatorLauncher` rebuilt the native directory from the declared one, and the unmirrored `QuickSave2` was gone. Launched through `emulatorLauncher`, the same save mirrored within the same second, `.txt` sidecar included. So the mirror belongs to the launcher and not to the emulator, and a state saved in a BizHawk opened any other way is lost at the next ES launch of that game. It extends the save-sync skill's "the declared path is authoritative in both directions" to a file the declared side never held

## RB-360. The declared schema

`.emulationstation/es_savestates.cfg` in the live install is **byte-identical to the copy
vendored in `reference/`**, so the vendored file is trustworthy for this version.

It defines **13 emulators**, which is the full extent of RetroBat's machine-readable
save-state knowledge. Every other emulator in the tree is undescribed, so state sync
coverage is bounded by this list, not by the 244 systems.

Two directories are core-scoped, not one. The plan names `libretro`; **`bizhawk` is also
core-scoped** (`{{system}}/bizhawk/sstates/{{core}}`). Both produce independent state sets
per core for the same game.

Four parsing traps, all of which a parser written from the plan's description would hit
(`probe-output/es_savestates.json`):

| Emulator   | Trap                                                                                                                                                                                         |
| ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `libretro` | **No `firstslot`/`lastslot` attributes at all.** The plan says the file "yields the slot bounds"; for the single most important emulator it does not. A default is required.                 |
| `desmume`  | **`<image>` and `<file>` are the identical template** (`{{romfilename}}.ds{{slot0}}`). Uploading `<image>` as `screenshotFile` would upload the state file itself.                           |
| `bigpemu`  | `firstslot="001"` is a zero-padded string, and `lastslot="999"` needs three digits while the file template uses two-digit `{{slot2d}}`. RetroBat's own file is internally inconsistent here. |
| 5 others   | No `autosave`/`incremental` attributes, so those default to unknown rather than false.                                                                                                       |

The commented-out `<core name="..." enabled="false"/>` and `<defaultCoreDirectory>` elements
show a per-core override mechanism exists but ships disabled. A parser must tolerate `<core>`
children appearing, because a user can enable them.

## RB-367. The state screenshot is unreliable, and that is a race not a one-off

`es_savestates.cfg` maps `<image>` onto RomM's optional `screenshotFile`. Across three
observed saves the ES-facing `.jpg` came out **three different ways**:

| Save                        | ES-facing `.jpg` | Native `.jpg` |
| --------------------------- | ---------------- | ------------- |
| 3rd Birthday (pre-existing) | **0 bytes**      | 120,539 B     |
| Patapon, first run          | 37,927 B         | 37,927 B      |
| Patapon, second run         | **absent**       | 37,927 B      |

The timestamps above explain it: the watcher finished its copy at `.416` while PPSSPP only
wrote its own screenshot at `.431`, **15 ms too late to be picked up**. Depending on where
the emulator is in writing that file when the watcher looks, the mirrored screenshot is
correct, truncated to zero, or never created.

**So RomMBat must treat the state screenshot as best-effort**: absent and zero-byte are both
normal, and neither means the state itself is bad. The `.ppst` was correct in every case. If
a screenshot is wanted, the native `PPSSPP_STATE/` copy was right all three times, but
reading it requires the `.txt` mapping to find the file.

Also present and _not_ saves, so they need excluding: `psp/SYSTEM/` (config, `ppsspp.ini`),
`psp/SYSTEM/CACHE/` (shader caches, `<GAMEID>.vkshadercache`), `psp/Cheats/` (a
`<GAMEID>.ini` is created per game merely by launching it).

## RB-368. Every other emulator in `es_savestates.cfg`, driven

`tools/m0-probes/probe2-savestates.ps1` generalises the PPSSPP measurement: it snapshots the
**whole `saves/<system>` subtree** before and after a real save, so the emulator's native
location discovers itself rather than having to be guessed. Each row below is a real launch
of a real game with a real save state.

| Emulator      | System    | Declared `<directory>` | Declared `<file>`    | `<image>`  | `.txt` sidecar               | Written |
| ------------- | --------- | ---------------------- | -------------------- | ---------- | ---------------------------- | ------- |
| `libretro`    | snes      | **ok**                 | **ok** `.state1`     | ok 1163 B  | none needed                  | live    |
| `ppsspp`      | psp       | **ok**                 | **ok** `_0.ppst`     | **racy**   | `UCES00995_1.00`             | live    |
| `duckstation` | psx       | **ok**                 | **ok** `_resume.sav` | **absent** | `SLUS-00404`                 | at exit |
| `pcsx2`       | ps2       | **ok**                 | **ok** `.resume.p2s` | ok 183 KB  | `SLUS-20265 (79646C72)`      | at exit |
| `dolphin`     | gamecube  | **ok**                 | **ok** `.s01`        | **absent** | `GW7E69`                     | live    |
| `flycast`     | dreamcast | **WRONG, see below**   | **ok** `_1.state`    | **absent** | none needed                  | live    |
| `gopher64`    | n64       | **ok**                 | **ok** `.state0`     | **absent** | `TWINE-72E3E7B4...` (sha256) | live    |

`duckstation` and `pcsx2` were driven through RetroBat's shared **AUTO SAVE/LOAD** option
(`<system>.autosave=1` in `es_settings.cfg`) rather than a keypress, because RetroBat binds
their save-state hotkey to a **gamepad combo only** (`XInput-0/Back & XInput-0/X`) with no
keyboard equivalent. That route is worth having anyway: it is the only measurement of the
`autosave_file` and `autosave_image` templates, which nothing had checked, and **both matched**.

Four results generalise, and they are what M6 should be built on:

1. **The declared `<file>` template was correct for all seven of these.** Filenames can be
   trusted.
2. **The `.txt` sidecar holds the native basename** and must be carried with the state.
   Across this batch it looked like a difference-marker, appearing for the five emulators
   whose native naming differs from the rom filename and not for `libretro` or `flycast`.
   **That reading is retracted**: `jgenesis` and `desmume`, driven later, both wrote one
   containing the rom filename itself, so RetroBat emits it unconditionally and its presence
   signals nothing. Its content is still the mapping.
3. **The `<image>` is absent far more often than it is present**: missing outright in four
   of seven, and for PPSSPP correct, zero-byte and missing across three runs of the same
   game. `screenshotFile` is best-effort **everywhere**, not just on PPSSPP.
4. **Timing splits by route, not by emulator.** A manual save is mirrored live, within about
   120 ms, while the emulator is still running. An autosave state appears only at exit. No
   emulator needed a separate exit-time pass for a manual save.

`libretro` is the one that needs no mirroring at all, because RetroBat points RetroArch
straight at the declared path:

```text
savestate_directory = "E:\RetroBat\saves\snes\libretro.snes9x"
```

## RB-369. The `flycast` declared directory is wrong, and RetroBat contradicts itself

This is the one real template failure, and it is RetroBat disagreeing with its own launcher.
`es_savestates.cfg` declares:

```xml
<emulator name="flycast" firstslot="1" lastslot="9">
  <directory>{{system}}/flycast/sstates</directory>
```

but the config RetroBat's own `FlycastGenerator` writes at launch says:

```text
Dreamcast.SavestatePath = E:\RetroBat\saves\dreamcast\reicast\states
Dreamcast.VMUPath       = E:\RetroBat\saves\dreamcast\flycast\vmu
```

and that is where the state landed: `saves/dreamcast/reicast/states/<rom filename>_1.state`.
`reicast` is Flycast's former name, so the emulator kept the legacy directory and the ES-facing
declaration was never updated. Both `saves/dreamcast/flycast/sstates/` and
`saves/dreamcast/flycast/states/` exist on the install and are **empty**, which is exactly the
trap: a client that trusts the declaration finds an empty directory and concludes there are no
states, rather than concluding it is looking in the wrong place. Note the **VMU path still uses
`flycast/`**, so the two halves of Dreamcast live under different directory names.

**So RomMBat must not treat `es_savestates.cfg`'s `<directory>` as authoritative on its own.**
The `<file>` template held everywhere, but the directory did not. Where it matters, cross-check
against the emulator's generated config, and never read an empty declared directory as
"this game has no states".

**Filed upstream:** [RetroBat-Official/emulatorlauncher#1336](https://github.com/RetroBat-Official/emulatorlauncher/issues/1336)
(2026-08-09). **Fixed in RetroBat 8.2.1**: the save-state watcher was watching the wrong source
directory, and pointing it at `reicast/states` makes a state mirror into the declared
`flycast/sstates`. Everything measured above is what 8.2.0 did; see the issue's section under
[Upstream issues filed](../issues.md#rb-343-1336-fixed-in-821-driven-and-the-workaround-is-out) for what
8.2.1 changes and what RomMBat still does. `openmsx` below is unfixed, so the rule this finding
states is unchanged.

## RB-371. openMSX: the declared directory is wrong, and it points at the wrong tree

`bigpemu` (22.9 MB, `BigPEmu.exe`) and `openmsx` (31.2 MB, `openmsx.exe`) were installed on
demand once Jaguar and MSX1 roms were available. openMSX both launched and saved:

|                                         |                                                                                            |
| --------------------------------------- | ------------------------------------------------------------------------------------------ |
| Declared                                | `saves/msx1/openmsx/<rom filename>_<slot>.oms` + `.png`                                    |
| Written                                 | **`bios/openmsx/savestates/quicksave.oms`** + `quicksave.png` (7,531 B, a real screenshot) |
| Declared directory after two real saves | **empty**                                                                                  |

**RetroBat puts openMSX's whole user-data directory under `bios/openmsx/`**, not under
`saves/`, and savestates land in its `savestates/` subdirectory. So a client that trusts
`es_savestates.cfg` here looks in the wrong tree entirely, not merely the wrong folder, which
is a worse version of the `flycast` failure.

Two limits on that result, stated because they bound it. The state was made by typing
`savestate` into openMSX's own console, so it took openMSX's default name (`quicksave`)
rather than the `[guess_title]_0` name RetroBat's `kbhotkeys.tcl` binds to Alt+F2. **Whether
RetroBat would mirror a state written under its own naming into the declared path was not
established**: the second attempt's typed command never reached the console, proven by
openMSX's own `persistent/console/history.txt` containing only the first command. So this is
"no mirroring observed", not "mirroring disproved". Unlike every other emulator here, the
`<image>` is real and substantial.
