---
summary: How `emulatorLauncher` installs and starts emulators, its log, and the hotkeys and versions it exposes.
read-when: Before invoking `emulatorLauncher`, reading its log, or relying on an emulator hotkey.
---

# RetroBat: emulators and the launcher

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-26. `emulatorLauncher.log` is the only in-tree record of what a launch ran

Verified: RetroBat 8.2.0, 2026-08-08, and 8.2.1, 2026-09-28. How: read `emulationstation/emulatorLauncher.log` on each install, read-only.
Each launch line carries the rom, `-system`, `-emulator`, a millisecond timestamp, and `-core` when
there is one: 58 of 383 launches in the 8.2.1 `.log.old` have none.
No other file in the tree holds those together, and a hook is given none of the system, emulator
or core. So `LaunchLog` reads this file and the `game-end` hook is only the trigger.

## RB-112. The log rotates at about 1 MiB into `.log.old`, and the halves do not overlap

Verified: RetroBat 8.2.0, 2026-08-16, and 8.2.1, 2026-09-28. How: compared the last stamp of `.log.old` with the first stamp of the live file.
On 8.2.0 the live file was 503,225 B beside a 1,048,604 B `.log.old`. On 8.2.1 they were 148,769 B
and 1,049,083 B, and `.old` ended eleven minutes before the live file began. Rotation is a size
threshold, so reading `.old` then the live file yields launches in time order. `LaunchLogPosition`
is a timestamp and the signatures read at it, never a byte offset, which would point into the wrong
file after a rotation.

## RB-113. A rom path in the log carries the drive letter the install had at the time

Verified: RetroBat 8.2.0, 2026-08-16. How: counted the roots of every `-rom` in one install's log.
Of 424 launches, 295 read `D:\RetroBat` and 129 read `E:\RetroBat`: one install that moved, in one
continuous log. Stripping the current root would drop 70% of the history, so `LaunchLog` cuts the
path at its `roms\` or `system\` segment instead.

## RB-114. `emulatorLauncher` also runs for non-launches, so `-rom` marks a launch

Verified: RetroBat 8.2.0, 2026-08-16. How: counted `[Startup]` invocation lines against those carrying `-rom`.
Of 730 `[Startup]` lines, 424 were a game launch. The rest were `-updatestores` and similar, so
keying on `[Startup]` over-counts by 72%. `LaunchLog` takes a line as a launch only when it carries
`-rom`.

## RB-115. `-rom` is sometimes unquoted and often not the last flag

Verified: RetroBat 8.2.0, 2026-08-16, and 8.2.1, 2026-09-28. How: matched every launch line against a quoted and a positional read.
In 424 launches, `-rom` was unquoted once, with spaces and parentheses in the path, and not the
final flag 19 times, 5 of them with `-core` after it. On 8.2.1, 111 of 383 launches in `.log.old`
wrote `-core` after `-rom`. `-rom "([^"]+)"` misses the first shape and a positional read the
second, so `LaunchLog` reads a quoted value to its closing quote and an unquoted one to the end of
the line.

## RB-116. The log cannot supply a session's end time

Verified: RetroBat 8.2.0, 2026-08-16, and 8.2.1, 2026-09-28. How: counted launches with no `Process exited with code` line after them.
187 of 424 launches never recorded an exit. The exit codes seen were 226 zero, 2 one, 5 minus one,
3 `-1073741819` (access violation) and 1 `-805306369`. On 8.2.1, `.log.old` holds 358 exits for
383 launches. `PlaytimeCorrelator` takes the end time from the `game-end` hook, never from the log.

## RB-117. The log opens with a UTF-8 BOM and carries unstamped lines

Verified: RetroBat 8.2.0, 2026-08-16, and 8.2.1, 2026-09-28. How: read the first bytes of both halves, and every line without a timestamp.
Both files open with `EF BB BF`, since each rotation half starts a new file. The 8.2.0 log carried
15 unstamped continuation lines across the two files, .NET stack traces among them. `LaunchLog`
tolerates both.

## RB-118. An ES-menu launch is identifiable in the log

Verified: RetroBat 8.2.0, 2026-08-16, and 8.2.1, 2026-09-28. How: counted launches with `-system retrobat` and a rom under `system\es_menu\`.
There were 27 on 8.2.0 and 9 in the 8.2.1 `.log.old`. So RomMBat's own exit, which fires a
`game-end` with no game behind it, is recognised from the log rather than inferred.
`LaunchRecord.IsMenuLaunch` carries it, and `PlaytimeCorrelator` discards that `game-end`.

## RB-252. `-core` means nothing for an emulator that declares no core

Verified: RetroBat 8.2.1, 2026-09-13. How: launched every `nes` row from ES and read the launch lines.
A row whose emulator declares no core inherits the system's `<core>` and passes it down anyway.
`mesen` standalone and `jgenesis` were both launched with `-core nestopia` from `nes.core`, ignored
it, and ran correctly. Read the emulator from `-emulator`, and take `-core` as identifying nothing
for such a row.

## RB-137. An emulator's version cannot be read from its binary

Verified: RetroBat 8.2.0, 2026-08-17. How: read the file version of each emulator a `mastersystem` state was made under.
A libretro core DLL has an empty `ProductVersion` and `FileVersion`, and `jgenesis` and `bizhawk`
each ship two top-level executables. `StateScanner.ReadVersion` declines unless there is exactly
one, so `emulator_version` is null in practice and `retrobat_version` is what identifies the build.

## RB-375. A declared emulator is often not installed

Verified: RetroBat 8.2.0, 2026-08-08. How: looked for an executable under `emulators/` for each of the 13 emulators `es_savestates.cfg` declares.
Six had none: `bizhawk`, `desmume`, `jgenesis`, `mupen64` and `bigpemu` held only a config stub,
and `openmsx` an empty folder. RetroBat downloads an emulator the first time it is launched.
`updates.enabled=false` in `es_settings.cfg` stops RetroBat's own update check but not that
download. So a declaration is no evidence that the emulator exists, and a row is certified only
after its emulator is installed and driven.

## RB-50. Installing an emulator on demand blocks on an untitled modal dialog

Verified: RetroBat 8.2.0, 2026-08-08. How: launched each missing emulator through `emulatorLauncher` and watched the windows it raised.
The launcher logs `[Startup] Emulator update found : proposing to update.` and shows _"The emulator
'\<name\>' is not installed. Install now?"_ with Yes and No. The window has no title and no useful
class name, the log says nothing more, and there is no timeout: three launchers were still waiting
seven hours later. `tools/m0-probes/probe2-install-emulator.ps1` answers it by pressing Enter on
the one visible top-level window the launcher owns.

## RB-420. A zip the launcher extracted ends on an untitled "keep the uncompressed game?" modal

Verified: RetroBat 8.3.0-beta, 2026-10-06. How: launched `gba` under `nosgba` on a `.zip` through `emulatorLauncher`, closed NO\$GBA with WM_CLOSE, and watched the windows the launcher raised.
For an emulator that cannot read an archive, the launcher extracts the game to
`roms\.uncompressed\<system>\<zip>\` and starts the emulator on the extracted file. After the
emulator exits, it shows _"Do you want to keep the uncompressed game for further use?"_ with Yes
and No, Yes focused. It looks like RB-50's dialog: no title, a `MainWindowHandle` of 0, and no
timeout. The launcher stays running until the dialog is answered, so a game can look as if it never
ended. Right, then Enter answers No, which deletes `roms\.uncompressed`. NO\$GBA itself exits on
WM_CLOSE.

## RB-374. BizHawk crashes when `emulatorLauncher` is run without `-core`

Verified: RetroBat 8.2.0, 2026-08-08. How: launched `bizhawk` with and without `-core`, then read `Bizhawk.Controllers.cs`.
Without it the launcher throws `KeyNotFoundException` in
`BizhawkGenerator.CreateControllerConfiguration`, because `inputPortNb[core]` is an unguarded
dictionary lookup and an empty core is not a key. All 36 BizHawk cores that `es_systems.cfg`
declares are among its 42 keys, and ES always passes one, so only a direct invocation or an
unlisted core reaches it. Upstream will not fix it
([emulatorlauncher#1337](https://github.com/RetroBat-Official/emulatorlauncher/issues/1337)), so
anything that runs `emulatorLauncher` itself passes `-core`.

## RB-370. DeSmuME, Mupen64, jgenesis and BizHawk states reach their declared directories

Verified: RetroBat 8.2.0, 2026-08-08. How: installed each on demand, made a real save state, and snapshotted the `saves/<system>` subtree around it.

| Emulator   | System    | Executable                       | `<file>` written    | `<image>`             | `.txt` sidecar holds            |
| ---------- | --------- | -------------------------------- | ------------------- | --------------------- | ------------------------------- |
| `desmume`  | nds       | `DeSmuME-VS2022-x64-Release.exe` | `.ds1`              | the `<file>` template | the rom filename                |
| `mupen64`  | n64       | `RMG.exe`                        | `.st1`              | absent                | `Dr. Mario 64 (U) [!]-1A793636` |
| `jgenesis` | megadrive | `jgenesis-cli.exe`               | `_0.jst`            | absent                | the rom filename                |
| `bizhawk`  | nes       | `EmuHawk.exe`                    | `.QuickSave0.State` | absent                | `Battle City.NesHawk`           |

Each state reached the declared directory while the emulator ran. BizHawk's got there through the
launcher's mirror of its native `emulators/bizhawk/sstates/` (RB-270). `mupen64` is Rosalie's Mupen GUI,
not a mupen64plus binary. DeSmuME's state template, `{{romfilename}}.ds{{slot0}}`, sits beside its
battery save `{{romfilename}}.dsv`, so `SaveStateSchema` compiles `{{slot0}}` to exactly one digit
rather than globbing `<rom>.ds*`, which would take the battery save as slot `v`.

## RB-372. BigPEmu's save state cannot be reached from a keyboard

Verified: RetroBat 8.2.0, 2026-08-08. How: launched Rayman under `bigpemu`, swept F1 to F8 into its window, and read `BigPEmuConfig.bigpcfg`.
It saves states from its own overlay menu. RetroBat's `es_padtokey.cfg` binds only a close hotkey
for it, the sweep wrote no file of any kind, and the config binds no save-state key (only
`System/StateSlot = -1`). A state is made through the overlay with a gamepad, which is how RB-166
drove six and read them off the declared path.

## RB-373. A system overlay can take an emulator's hotkey

Verified: RetroBat 8.2.0, 2026-08-08. How: sent Alt+F2, RetroBat's openMSX save-state key, and watched the screen and the tree.
NVIDIA's Photo mode overlay took the key and opened its own panel, openMSX never saw it, and
nothing in the emulator or RetroBat reported it. A hotkey RomMBat documents or a pass relies on
can be lost this way, so a save is confirmed by the file it writes, never by the keypress.
