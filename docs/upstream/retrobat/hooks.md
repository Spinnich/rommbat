---
summary: How EmulationStation fires its event scripts on RetroBat: arguments, timing, concurrency and which script forms run.
read-when: Before changing an ES hook, the journal the hooks write, or what a hook may do in the game-launch path.
---

# RetroBat: hooks

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-346. Hooks do not block game launch

Verified: RetroBat 8.2.0, 2026-08-08. How: an 8 s sleep in every hook, timed against `emulatorLauncher.log`.
ES spawns event scripts fire-and-forget. `emulatorLauncher` started 30 ms after the `game-start`
hook on three launches of three, while the hook still had 8 s of sleep ahead of it. So a hook's
running time never delays a launch.

## RB-197. A hook runs during the launch, not inside it

Verified: RetroBat 8.2.0, 2026-08-24. How: joined 23 journalled `game-start` records to the launcher log's `[Startup]` stamps.
The launcher's `[Startup]` line lands a median 24 ms after the hook's record (20 of 23 between 12
and 44 ms). Against the hook's own ~60 ms start, ES spawned it before the launcher began and did
not wait. The launcher then takes 0.5 s to 2.8 s to reach `[Running]`. A spawn costs contention,
not latency. Rule 4 is about the network and does not rest on this.

## RB-195. A hook's start cost is JIT, not size

Verified: RetroBat 8.2.0, 2026-08-24. How: 31 interleaved runs each of the hook and the agent on a USB stick.
The 75.9 MB agent reaches `Main` in 34.0 ms; the 11.0 MB trimmed hook took 59.8 ms to start and
111.3 ms to finish. `PublishTrimmed` discards the framework's precompiled code, so a trimmed app
without R2R JITs everything. `PublishReadyToRun` costs 1.8 MB a copy and takes one invocation to
49 ms, so the hook ships with it. `EnableCompressionInSingleFile` costs 4 ms and saves 1.74 MB,
so it stays on.

## RB-347. Hooks run concurrently, including with each other

Verified: RetroBat 8.2.0, 2026-08-08. How: sleeping hooks in every event folder appending to one log.
A `quit` fired 2.1 s after a `game-end` while that hook was mid-sleep, and their appends
interleaved. Three `game-end` hooks were in flight at once. So RomMBat's hooks take a lock, and
the journal survives interleaved appends from separate processes.

## RB-350. ES runs every script in an event folder

Verified: RetroBat 8.2.0, 2026-08-08. How: a probe script beside the shipped `start/updatestores.bat`.
Both ran, 63 ms apart, in alphabetical order. RomMBat installs its hook as a separate file beside
RetroBat's rather than replacing it.

## RB-348. `game-start` gets three arguments, and no system, emulator or core

Verified: RetroBat 8.2.0, 2026-08-08. How: a hook echoing its arguments, on games whose stem and display name differ.
`$1` is the absolute rom path, `$2` the rom basename, `$3` the gamelist `<name>`. `$4` and `$5`
are empty. Batocera documents `$3` as the system; on RetroBat it is the display name. The
launcher's `[Startup]` line in `emulationstation/emulatorLauncher.log` carries `-system`,
`-emulator`, `-core` and `-rom` with a millisecond stamp, so RomMBat takes launch facts from that
log and relativises `$1` at the hook boundary (rule 1).

## RB-349. `game-end` gets no arguments, and can fire with no `game-start`

Verified: RetroBat 8.2.0, 2026-08-08. How: three `es_menu` launches driven by calling `emulatorLauncher.exe` directly.
`game-end` never receives an argument, so it cannot name the game that ended. It also fires for a
launch that fails (`path is null`, exit 204) with no `game-start` before it. RomMBat tolerates a
`game-end` that closes nothing.

## RB-221. An ES-menu launch fires both events

Verified: RetroBat 8.2.1, 2026-08-26. How: two RomMBat sessions opened from the ES menu, journal read after.
A successful launch of an `es_menu` entry fires `game-start` carrying
`system/es_menu/rommbat.menu`, then `game-end` carrying nothing. So a missing `game-start` does
not identify a menu launch; RomMBat keys its discard on the launcher log's `es_menu` rom and
discards the paired `game-start` with it.

## RB-222. RomMBat's own launch never becomes a play session

Verified: RetroBat 8.2.1, 2026-08-26. How: two RomMBat sessions from the ES menu, then a PS2 game, outbox read after.
Both RomMBat sessions journalled `discarded` and added no outbox row; the PS2 game straight after
journalled `correlated` and its `play_session` was sent. Both rom paths were journalled relative
to the root.

## RB-63. `game-selected` and `system-selected` fire on every cursor move

Verified: RetroBat 8.2.0, 2026-08-09. How: hooks placed in folders created for the two events.
Neither event ships a folder under `scripts/`. `game-selected` carries
`<system> <rom path> <display name>`, the system `game-start` withholds, but it fires once per
cursor move, so it is at most a last-known-selection hint. RomMBat does not use it.

## RB-351. `game-end` fires when the emulator is killed

Verified: RetroBat 8.2.0, 2026-08-08. How: one launch quit normally and one killed from Task Manager.
`game-end` fired 83 ms after a clean exit (code 0) and 66 ms after the kill (code 1). So a crashed
emulator still closes the journal record, though `game-end` cannot say how the game ended.

## RB-352. A `.bat` hook never starts when the display name holds a space

Verified: RetroBat 8.2.0, 2026-08-08. How: seven launches, then a crossover that swapped two entries' `<name>`.
`game-start` produced no record for any game whose `<name>` held a space and a record for every
game whose name did not, and the behaviour followed the name across the swap. ES fires the event
in every case (RB-395); the `.bat` is what never starts. Nearly every scraped name holds a space.
Filed upstream as [#2196](../issues.md#rb-342-2196-the-es-hook-bug-moved-repository).

## RB-396. Only an `.exe` hook survives a real game name

Verified: RetroBat 8.2.0, 2026-08-09. How: a `.bat`, a `.ps1` and an `.exe` hook side by side in all nine event folders.

| Launch                                                   | `.bat` | `.ps1`          | `.exe` |
| -------------------------------------------------------- | ------ | --------------- | ------ |
| `2048`, no space anywhere                                | ran    | ran             | ran    |
| `Mr Boom`, space in the display name                     | no     | ran, name split | ran    |
| `Gradius 2 (Japan, Europe) (En) (Wii U Virtual Console)` | no     | no              | ran    |

The `.exe` received the last name as three intact arguments. RomMBat's hooks are executables.

## RB-397. The `.bat` and `.ps1` failures are the interpreter handoff

Verified: Windows 11, 2026-08-09. How: the same scripts started with `Process.Start`, no ES involved.
ES quotes an argument holding a space and hands the line to ShellExecute. A `.bat` goes through
the `batfile` association, `cmd /c "%1" %*`, and cmd's quote stripping mangles a line whose
arguments carry quotes, so the file never starts and nothing reports it. A `.ps1` runs as
`powershell <script> <args>` with no `-File`, so PowerShell reparses the arguments as code: a
space splits the name and a parenthesis or comma is a parse error. With `-File` all three
arguments arrive intact; ES does not pass it. An `.exe` has no interpreter in between.

## RB-398. A host can fire every event and run no script form at all

Verified: RetroBat 8.2.0, 2026-08-09. How: the three-form install on a second PC under another Windows account.
All four events fired and the `.exe` recorded each, while neither the `.bat` nor the `.ps1` ran
for any event, including the three that pass no arguments. ES was started by `RetroBat.exe` with
`--home` on the stick, and the volume was writable. RomMBat's hooks are executables for this
reason as much as RB-396's, and RomMBat reports when play data arrives with no hook activity.

## RB-399. An ordinary application can take the `.bat` association

Verified: Windows 11, 2026-08-09. How: an elevated registry and policy collection on the host from RB-398.
The two causes on that host are unrelated to security software. Notepad++'s installer, when its
association option is ticked, sets `HKCR\.bat` and `HKCR\.cmd` to `Notepad++_file` and stashes
the original ProgId in `Notepad++_backup`, machine wide. The PowerShell execution policy was the
client default, `Restricted`, and ES passes no `-ExecutionPolicy`. Both fail silently, and
RetroBat's own `updatestores.bat` cannot run there either.

## RB-66. An unsigned exe runs from removable media under Smart App Control

Verified: Windows 11, 2026-08-09. How: the collection from RB-399, with Smart App Control active.
Smart App Control logged `passed Config CI policy and was allowed to run` for an unsigned,
locally compiled hook exe on a USB stick. No AppLocker, attack surface reduction or removable
storage policy was set on that host, so this is one strict setting, not all of them.

## RB-382. A hook reaches the RetroBat root four levels up

Verified: RetroBat 8.2.0, 2026-08-08. How: read the shipped `scripts/start/updatestores.bat` and resolved it after a drive-letter move.
Hooks live at `emulationstation/.emulationstation/scripts/<event>/`. `%~dp0..\..\..\` reaches
`emulationstation/`, which is where the shipped script's `emulatorLauncher.exe` sits, and the
root needs `%~dp0..\..\..\..\`. RomMBat's hooks resolve from their own module path, so nothing
depends on the count at runtime.

## RB-400. A hook's working directory depends on its form

Verified: RetroBat 8.2.0, 2026-08-09. How: the three-form install from RB-396, logging the working directory.
A `.bat` and an `.exe` start in their own folder, a `.ps1` in ES's home, so RomMBat reads no
path from the working directory. ES finds its scripts under the `--home` that `RetroBat.exe`
passes (`RetroBat.log` records it); no `HOME` variable is set, so ES started directly resolves
its scripts under `%USERPROFILE%`. `HKCU\Software\RetroBat\LatestKnownInstallPath` records, per
user, whether `RetroBat.exe` has run there.

## RB-394. The ES logs last one start; `emulatorLauncher.log` lasts weeks

Verified: RetroBat 8.2.0, 2026-08-09. How: read the stick's logs a day after a session on another host.
`es_log.txt` rotates through `es_log.0.txt` to `es_log.3.txt` on every ES start, and the root's
`RetroBat.log` is overwritten. `emulatorLauncher.log` rotates by size into
`emulatorLauncher.log.old`, and 268 KB covered five weeks and 70 launches, both hosts included.
RomMBat reads both launcher files and tolerates a rotation between reads.

## RB-395. ES logs its scripting only at `LogLevel=debug`

Verified: RetroBat 8.2.0, 2026-08-09. How: set `LogLevel` to `debug` in `es_settings.cfg` and launched a game.
The default level is error-only. On `debug`, ES logs `fireEvent:`, `queuing:` and `executing:`
for every script, quoting only arguments that hold a space and writing the script path with
forward slashes. It logs `executing:` for a process that never starts, and no error after, so
the line is not evidence a hook ran.

## RB-209. The `start` and `quit` hooks carry a session and a save to RomM unattended

Verified: RetroBat 8.2.0, 2026-08-24. How: two ES sessions with no RomMBat command run, outbox and server read after.
Each hook's detached pass reached the server and exited 0 with no terminal open. One session
carried both a `play_session` and a battery save, and the server's hash matched the local file.

## RB-213. The emulator's save is on disk before the `quit` pass reads

Verified: RetroBat 8.2.0, 2026-08-24. How: timestamps from RetroArch's `.srm`, the journal and the upload.
RetroArch wrote the `.srm` 0.7 s before `game-end`, the `quit` hook fired 3.2 s after
`game-end`, and the scan saw the new bytes 0.5 s later. The uploaded hash differed from both the
file before play and the server's copy, so the pass sent what the player saved.
