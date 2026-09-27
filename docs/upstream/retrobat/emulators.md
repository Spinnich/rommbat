---
summary: How `emulatorLauncher` installs and starts emulators, its log, and the hotkeys and versions it exposes.
read-when: Before invoking `emulatorLauncher`, reading its log, or relying on an emulator hotkey.
---

# RetroBat: emulators and the launcher

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-26. `emulationstation/emulatorLauncher.log` is the only in-tree source carrying rom, system, emulator, core and a

Plan says: Everything the hooks need is available to the hooks (M6 generally)

Measurement says: `emulationstation/emulatorLauncher.log` is the only in-tree source carrying rom, system, emulator, core and a millisecond timestamp together. 268 KB per 5 weeks / 70 launches, two-file rotation

## RB-41. Whether a declared emulator is installed

Plan says: (not addressed) whether a declared emulator is installed

Measurement says: Six of the 13 have **no executable**, only a config stub; RetroBat downloads emulators on demand. A declaration is not evidence the emulator exists, so check for the binary before promising state sync

## RB-50. How an uninstalled emulator gets installed

Plan says: (not addressed) how an uninstalled emulator gets installed

Measurement says: A **modal dialog with no title and no timeout** blocks the launch until answered. Three launchers were found still waiting on it seven hours later

## RB-52. Whether a declared emulator can be launched once installed

Plan says: (not addressed) whether a declared emulator can be launched once installed

Measurement says: `bizhawk` installs and then crashes in `CreateControllerConfiguration` unless the launcher is given **`-core`** (`inputPortNb[core]` is unguarded). ES always passes one; direct invocation does not

## RB-54. BizHawk's hotkeys

Plan says: (not addressed) BizHawk's hotkeys

Measurement says: RetroBat rebinds them: `Save State 1` is **Ctrl+F1** and `Shift+F1` is **Load**, so BizHawk's usual save key silently loads. Only `Quick Save` = `F2` was observed to write

## RB-58. Whether a documented emulator hotkey actually reaches the emulator

Plan says: (not addressed) whether a documented emulator hotkey actually reaches the emulator

Measurement says: Not necessarily. **NVIDIA's Photo mode overlay swallowed Alt+F2**, the key RetroBat binds to openMSX's save state, and nothing anywhere reported it. A system overlay can silently cost a user their save

## RB-112. That describes a smaller install, not the mechanism

Previously: `emulatorLauncher.log` is 268 KB for 5 weeks and 70 launches (plan M6, probe 1)

Measurement says: That describes a smaller install, not the mechanism. Live **503,225 B** for 6 weeks and **159** launches, beside a **1,048,604 B** `.log.old` for the 3 weeks before it at **265** launches. **Rotation is a size threshold near 1 MiB**, and the two files **do not overlap**, so reading `.old` then live yields launches in time order across the boundary

## RB-113. They are rooted at whatever drive letter the install had at the time

Previously: Rom paths in the log are rooted at the install (plan M6 by implication)

Measurement says: **They are rooted at whatever drive letter the install had at the time.** 295 of 424 read `D:\RetroBat` and 129 read `E:\RetroBat`, one install, one continuous log. Relativising by stripping the current root silently discards 70% of the history, so relativise on the `roms\<system>\` segment instead

## RB-114. 730 `[Startup]` lines, of which 424 are a game launch

Previously: `[Startup]` identifies a launch (plan M6 by implication)

Measurement says: **730 `[Startup]` lines, of which 424 are a game launch.** `emulatorLauncher.exe` is also invoked for `-updatestores` and similar, so keying on `[Startup]` over-counts by 72%. The discriminator is the presence of `-rom`

## RB-115. What shape `-rom` takes

Previously: (not addressed) what shape `-rom` takes

Measurement says: Three shapes, and a naive read misses two. **Unquoted once in 424**, with spaces and parentheses in the path, so `-rom "([^"]+)"` misses it. **Not the final flag 19 times**, and **`-core` written after it 5 times**, so a positional read misses those. Read the quoted form to its closing quote and the unquoted form to end of line

## RB-116. No. 187 of 424 launches never record `Process exited with code`

Previously: (not addressed) whether the log can supply an end time

Measurement says: **No. 187 of 424 launches never record `Process exited with code`.** End time has to come from the `game-end` hook's own timestamp. Exit codes seen: 226 zero, 2 one, 5 minus one, 3 `-1073741819` (access violation), 1 `-805306369`

## RB-117. Opens with a UTF-8 BOM

Previously: (not addressed) how the log is encoded

Measurement says: **Opens with a UTF-8 BOM**, and carries 15 unstamped continuation lines across the two files, .NET stack traces among them. A line-per-record parser must tolerate both

## RB-118. An ES-menu launch is identifiable rather than inferred

Previously: An orphan `game-end` has to be discarded by inference (plan M6)

Measurement says: **An ES-menu launch is identifiable rather than inferred.** 27 launches carry `-system retrobat` with a `-rom` under `system\es_menu\`. So RomMBat's own exit not becoming a play session is a rule keyed on observable data, which is stronger than the plan assumed was available

## RB-137. It cannot, on any emulator tried

Previously: The emulator version can be read from the binary (stage 2a design)

Measurement says: **It cannot, on any emulator tried.** A libretro core DLL has empty `ProductVersion` and `FileVersion`, and `jgenesis` and `bizhawk` each ship two top-level executables, so the single-executable rule declines. `emulator_version` is null in practice and `retrobat_version` is what identifies the build

## RB-252. What `-core` means for an emulator that declares none

Question: (not addressed) what `-core` means for an emulator that declares none

Measured: **Noise.** A row whose emulator declares no core inherits the system-level `<core>` value and passes it down regardless: `mesen` standalone and `jgenesis` were both launched with `-core nestopia` from `nes.core`, both ignored it, and both ran correctly. Read `-emulator` from the launcher log and treat `-core` as meaningless for such a row

## RB-370. The remaining emulators, downloaded on demand and driven

Four of the six undriven emulators were brought onto disk and three of them driven with a
real save state. Getting them installed turned up a mechanism the plan does not account for.

**The on-demand install is a modal dialog that blocks forever.** Launching an emulator with
no executable writes `[Startup] Emulator update found : proposing to update.` to the log and
raises a RetroBat-styled window reading _"The emulator '\<name\>' is not installed. Install
now?"_ with Yes and No. Then nothing happens until somebody answers. The dialog has **no
window title and no useful class name**, the log says nothing more, and there is **no
timeout**: three `emulatorLauncher` processes were found still sitting on it seven hours
after they were started. `tools/m0-probes/probe2-install-emulator.ps1` answers it by finding
the one visible top-level window the launcher owns and pressing Enter.

| Emulator   | Downloaded | Executable shipped               | Note                                          |
| ---------- | ---------- | -------------------------------- | --------------------------------------------- |
| `desmume`  | 7.4 MB     | `DeSmuME-VS2022-x64-Release.exe` |                                               |
| `mupen64`  | 160.6 MB   | **`RMG.exe`**                    | Rosalie's Mupen GUI, not a mupen64plus binary |
| `jgenesis` | 69.8 MB    | `jgenesis-cli.exe`               |                                               |
| `bizhawk`  | 134.9 MB   | `EmuHawk.exe`                    | **must be launched with `-core`, see below**  |

Results of the four (`tools/m0-probes/probe2-savestates.ps1`):

| Emulator   | System    | Declared `<directory>` | Declared `<file>`          | `<image>`  | `.txt` sidecar holds            | Written |
| ---------- | --------- | ---------------------- | -------------------------- | ---------- | ------------------------------- | ------- |
| `desmume`  | nds       | **ok**                 | **ok** `.ds1`              | see below  | the rom filename                | live    |
| `mupen64`  | n64       | **ok**                 | **ok** `.st1`              | **absent** | `Dr. Mario 64 (U) [!]-1A793636` | live    |
| `jgenesis` | megadrive | **ok**                 | **ok** `_0.jst`            | **absent** | the rom filename                | live    |
| `bizhawk`  | nes       | **ok**, core-scoped    | **ok** `.QuickSave0.State` | **absent** | `Battle City.NesHawk`           | live    |

**Twelve of the thirteen declared emulators have now been installed and launched, eleven have
been driven to a real save state, and `flycast` is no longer the only wrong `<directory>`.**
`openmsx` is the second, and it is worse.

## RB-372. bigpemu launches, but its save state cannot be reached from a keyboard

`bigpemu` installs and runs Rayman correctly. Its save states are driven from BigPEmu's own
overlay menu, and RetroBat's `es_padtokey.cfg` binds only a close hotkey for it. A sweep of
F1 through F8, sent to the focused window, produced **no file of any kind**, and its
`BigPEmuConfig.bigpcfg` contains no save-state key binding (only `System/StateSlot = -1`).
Driving it needs gamepad menu navigation, so its declared template
(`{{system}}/bigpemu/{{romfilename}}_state{{slot2d}}.bigpstate`, with the already-noted
`firstslot="001"`/`lastslot="999"` versus two-digit `{{slot2d}}` inconsistency) stays
**unverified**.

## RB-373. An overlay can eat an emulator hotkey

Worth recording because it cost a probe run and would equally cost a user their save. openMSX
never saw Alt+F2 at all: **NVIDIA's Photo mode overlay grabbed it**, opening its own panel over
the game. Any hotkey RomMBat documents or relies on can be intercepted by a system-wide
overlay, and nothing in the emulator or in RetroBat reports that it happened.

**BizHawk is the strongest confirmation of the mirroring model, and it nearly read as a
second wrong declaration.** Its native location is
`emulators/bizhawk/sstates/<system>/<internal name>.<core>.QuickSave0.State`, which is
**outside the `saves/` tree entirely** and system-scoped rather than core-scoped, and
RetroBat's generated `config.ini` names exactly that path. Watching `saves/nes` alone
therefore reports that nothing happened. What actually happens is the PPSSPP pattern:
RetroBat mirrors the state into the declared ES-facing path, correct in both directions.

```text
native    emulators/bizhawk/sstates/nes/Battle City.NesHawk.QuickSave0.State
ES-facing saves/nes/bizhawk/sstates/NesHawk/BattleCity (Japan).QuickSave0.State
sidecar   saves/nes/bizhawk/sstates/NesHawk/BattleCity (Japan).txt  ->  Battle City.NesHawk
```

The mapping is doing real work here: BizHawk names the file after **its own database title
plus the core** (`Battle City.NesHawk`), while the rom is `BattleCity (Japan).zip`. Deleting
the native copy and relaunching **recreated it from the ES-facing one**, so the declared
directory is authoritative for writes as well as reads, exactly as with PPSSPP.

Two smaller observations. BizHawk writes a **`.State.rap` sibling** (3,612 B, ASCII magic
`RAP\n`) beside the native state; it is **not** mirrored to the ES-facing directory and was
not recreated on sync-in, so it does not round-trip. And the `.State` itself is a zip
(`PK\x03\x04`).

**RetroBat rebinds BizHawk's hotkeys, and the rebinding is a trap for anything scripted.**
Its `config.ini` sets `"Save State 1": "Ctrl+F1"` and `"Load State 1": "Shift+F1"`, so
BizHawk's usual Shift+F1-to-save **loads** here, silently doing nothing when no state
exists. Only `"Quick Save": "F2"` was observed to actually write; neither Ctrl+F1 nor
Ctrl+F3 produced a file, and no cause was isolated.

Two results change what M6 should do:

- **DeSmuME's declared state template collides with its own battery save.** The declaration
  is `{{romfilename}}.ds{{slot0}}`, and DeSmuME writes its battery save as
  `{{romfilename}}.dsv` in the parent directory. A client that expands the template into a
  glob (`<rom>.ds*`) matches `.dsv` and treats a battery save as state slot "v" - the probe
  did exactly that before the result was read carefully. **The slot placeholder must be
  anchored as a single digit**, not `.*`. This compounds the already-recorded trap that
  desmume's `<image>` and `<file>` are the identical template.
- **The `.txt` sidecar is not the difference-marker the earlier reading made it.** The
  previous generalisation was that it appears exactly where the emulator's native naming
  differs from the rom filename. Both `jgenesis` and `desmume` wrote one whose content **is
  the rom filename**, so it is written unconditionally by RetroBat's watcher and its presence
  proves nothing. Its _content_ is still the mapping and still has to travel with the state;
  `mupen64`'s carries the internal rom name plus a CRC (`Dr. Mario 64 (U) [!]-1A793636`).

## RB-374. BizHawk crashes when launched without `-core`, which is not what it first looked like

`bizhawk` downloads and installs, and then dies before the emulator starts:

```text
[Generator] Using BizhawkGenerator
[INFO] Creating controller configuration for BizHawk
[EXCEPTION] [KeyNotFoundException] The given key was not present in the dictionary.
  at EmulatorLauncher.BizhawkGenerator.CreateControllerConfiguration(DynamicJson json, String system, String core)
```

**An earlier revision of this document attributed that to having no gamepad attached. That
was wrong and is retracted.** The first two attempts ran with no pad _and_ no `-core`; the
successful one added both a pad and `-core NesHawk`, so two variables moved at once.
Re-running with the pad still attached and `-core` omitted **reproduces the crash exactly**,
and the upstream source says why (`Bizhawk.Controllers.cs`, line 91):

```csharp
int maxPad = inputPortNb[core];
```

an unguarded `Dictionary<string, int>` lookup keyed by core name. With no `-core`, `core` is
empty and the indexer throws. The gamepad was irrelevant.

**Scope, checked rather than assumed:** all 36 distinct BizHawk cores that this install's
`es_systems.cfg` declares are present among the 42 keys in `inputPortNb`, so a launch driven
by EmulationStation, which always passes a core, cannot hit this. It bites anything invoking
`emulatorLauncher.exe` directly, which is what these probes do, and it would bite a core
added to `es_systems.cfg` but not to the dictionary. Every other emulator driven here
(`libretro`, `desmume`, `mupen64`, `jgenesis`, `flycast`, `bigpemu`, `openmsx`) launches from
the same command shape with no `-core`.

For RomMBat the lesson survives the correction: a declaration is not a promise, and neither
is an installed binary. **Detect a failed launch from the launcher's exit rather than
assuming an installed emulator works**, and never record a play session for a game that
never started. And when RomMBat drives `emulatorLauncher` itself, **pass `-core`**.

## RB-375. Six of the thirteen emulators are not installed, and that is normal

`es_savestates.cfg` describes 13 emulators. On a substantial, well-used install, **six had no
executable at all**, only a leftover config file or an empty folder:

| Emulator                                               | State on disk           |
| ------------------------------------------------------ | ----------------------- |
| `bizhawk`, `desmume`, `jgenesis`, `mupen64`, `bigpemu` | config stub only, 0 exe |
| `openmsx`                                              | empty folder            |

RetroBat downloads emulators on demand: attempting a `desmume` launch produced
`[Startup] Emulator update found : proposing to update.` and the launcher exited without
starting anything. Two consequences:

- **A declaration in `es_savestates.cfg` says nothing about whether that emulator exists**, so
  RomMBat must check for the binary before promising state sync for a system.
- Those six are **untested rather than broken**. Their templates are unverified, and given
  `flycast`, at least one more directory being wrong would not be surprising.

`updates.enabled=false` in `es_settings.cfg` does suppress RetroBat's own update check
(`[Startup] Updates not enabled, not looking for updates.`) but **not** the missing-emulator
download prompt, which is a separate path.
