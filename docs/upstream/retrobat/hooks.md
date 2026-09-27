---
summary: How EmulationStation fires its event scripts on RetroBat: arguments, timing, concurrency and which script forms run.
read-when: Before changing an ES hook, the journal the hooks write, or what a hook may do in the game-launch path.
---

# RetroBat: hooks

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-2. Three levels reaches `emulationstation/`; the root needs four

Plan says: Hooks resolve the agent through `%~dp0..\..\..\` (L148-150, L375, L750)

Measurement says: Three levels reaches `emulationstation/`; the root needs four. **Fully corrected in the plan during M6**; the M0 experiment 4 text was the last place still carrying three

## RB-17. Hooks do not block

Plan says: Hooks may block game launch; M6 takes its budget from M0 (L355, L749-751)

Measurement says: **Hooks do not block.** The launcher started 30 ms after the hook, three times out of three, while the hook slept 8 s

## RB-18. RetroBat passes three

Plan says: Batocera `game-start` args: `$1` rom, `$2` basename, `$3` system, `$4` emulator, `$5` core (L350-352)

Measurement says: RetroBat passes **three**. `$4` and `$5` are empty and `$3` is not the system. Emulator and core are withheld from the hook

## RB-19. The hook cannot see emulator or core, so neither can come from the hook path

Plan says: Slot derives as `{emulator}:{core}:{slot}`, recorded per state (L806, L809-810)

Measurement says: The hook cannot see emulator or core, so neither can come from the hook path. Another source is required

## RB-20. `game-end` also fires with no preceding `game-start`, including for ES-menu launches and for launches that

Plan says: `game-end` closes the record its `game-start` opened (L748-749)

Measurement says: `game-end` also fires with **no** preceding `game-start`, including for ES-menu launches and for launches that failed. The agent must tolerate an orphan `game-end`, and RomMBat's own exit produces one

## RB-21. Right conclusion, wrong reason

Plan says: Hooks are journal-only to avoid blocking the launch path (L748-751)

Measurement says: Right conclusion, wrong reason. They do not block, but they **run concurrently**, so the lock file is mandatory and the journal must survive interleaved appends from separate processes

## RB-22. `game-start` never fires for a game whose gamelist `<name>` contains a space

Plan says: `game-start` opens the journal record that `game-end` closes (L748-749)

Measurement says: **`game-start` never fires for a game whose gamelist `<name>` contains a space**, confirmed by crossover. That is nearly every real rom, so the journal cannot be built on it. Use `emulatorLauncher.log`

## RB-25. `$3` is the gamelist display name

Plan says: `$3` is the system (L350-352, Batocera convention)

Measurement says: `$3` is the **gamelist display name**. The system is not passed to the hook at all. `$2` is the rom basename

## RB-27. The tree does, and so do the events, but the second host cannot launch a `.bat` or a `.ps1`, only an `.exe`

Plan says: A portable install works on any machine it is plugged into (core principle 4)

Measurement says: The tree does, and so do the events, but **the second host cannot launch a `.bat` or a `.ps1`, only an `.exe`**. Every hook was a `.bat` then, hence total silence. RomMBat must still detect the state

## RB-59. Amended

Plan says: `game-start` never fires when the display name contains a space (RB-22)

Measurement says: **Amended.** ES fires it every time and logs `executing:` for every script in the folder. The `.bat` never starts: ShellExecute routes it through the `batfile` association `cmd /c "%1" %*`, and cmd's quote-stripping rule mangles any line whose arguments carry quotes

## RB-60. Whether some other hook form works

Plan says: (not addressed) whether some other hook form works

Measurement says: `.ps1` fails too, on any parenthesis, because ES builds `powershell <script> <args>` with no `-File`. An `.exe` took a full No-Intro name as three intact arguments. **RomMBat's hooks must be executables**

## RB-61. Whether ES records what it runs

Plan says: (not addressed) whether ES records what it runs

Measurement says: Only at `LogLevel=debug`, which nothing sets and the ES menu does not surface; the default is error-only. And `executing:` is logged even when the process never starts, so it is not evidence of execution

## RB-62. How long the diagnostic evidence lasts

Plan says: (not addressed) how long the diagnostic evidence lasts

Measurement says: `es_log.txt` rotates through four files on **every** ES start, `emulatorLauncher.log` rotates to `.old`, and `RetroBat.log` is overwritten. Collect before the next launch anywhere, or the session is gone

## RB-63. `game-selected` and `system-selected` also fire, on every navigation move, and `game-selected` carries

Plan says: ES events are the nine with folders on disk (L262)

Measurement says: `game-selected` and `system-selected` also fire, on every navigation move, and `game-selected` carries `<system> <rom path> <display name>`, the system that `game-start` withholds. Neither ships a folder

## RB-64. Resolved

Plan says: The second host's hook failure has no candidate cause (RB-27)

Measurement says: **Resolved.** That host runs an `.exe` hook for all four events and neither script form for any, including the three that pass no arguments. `--home` was passed correctly and the volume was writable, so both earlier theories are dead

## RB-65. What stops a host running a hook

Plan says: (not addressed) what stops a host running a hook

Measurement says: Two ordinary, unrelated things, neither of them security software: **Notepad++'s installer takes the `.bat` ProgId** (`HKCR\.bat` = `Notepad++_file`, the original stashed in `Notepad++_backup`), and the **default `Restricted` PowerShell policy** blocks `.ps1`. Both fail silently

## RB-66. Whether an unsigned exe runs from removable media on a strict host

Plan says: (not addressed) whether an unsigned exe runs from removable media on a strict host

Measurement says: Yes. The second host has **Smart App Control active** (`passed Config CI policy and was allowed to run`) and ran an unsigned, locally compiled exe from a USB stick. Relevant to how RomMBat is distributed

## RB-195. Backwards. Size is not the cost

The claim being checked: Having a hook spawn the agent puts an **11 MB** process start in the game-launch path, against **75.5 MB** for the agent, and the size is the cost to measure (**[`docs/PLAN.md`](https://github.com/Spinnich/rommbat/blob/366b5f6bf/docs/PLAN.md), `docs/ARCHITECTURE.md`, the `offline-and-portable` skill**)

What was measured: **Backwards. Size is not the cost.** On the USB stick, 31 interleaved runs each: the **75.9 MB agent reaches `Main` in 34.0 ms**, the **11.0 MB hook takes 59.8 ms to start and 111.3 ms to finish**. `PublishTrimmed` rewrites the framework assemblies and discards the precompiled native code they ship with, so a trimmed app carrying no R2R of its own JITs everything from IL at every start. The same `File.Move` costs **51.5 ms** in the shipped build against **7.0 ms** with `PublishReadyToRun`, in one loop on one stick, so the gap is JIT and not disk. Adding R2R costs **1.8 MB a copy**, 7.2 MB across the four installs, and takes one invocation from **111 ms to 49 ms**

## RB-196. No, and it is worth recording as a wrong guess a measurement caught

The claim being checked: `EnableCompressionInSingleFile` is what costs the hook its start time (**this session's own first guess**)

What was measured: **No, and it is worth recording as a wrong guess a measurement caught.** Compression costs **4 ms** and saves **1.74 MB** (11,017,491 B against 12,752,849 B), so it is kept. The untrimmed build settles it from the other side: at 37.6 MB it does the spool write in **9.3 ms**, near R2R's 7.0 ms and nothing like the shipped build's 51.5 ms

## RB-197. They run during it, not inside it, and nothing waits for them

The claim being checked: The ES hooks run inside the game-launch path, so what they cost delays a launch (**[`docs/PLAN.md`](https://github.com/Spinnich/rommbat/blob/366b5f6bf/docs/PLAN.md), `docs/ARCHITECTURE.md`**)

What was measured: **They run during it, not inside it, and nothing waits for them.** No probe was needed, because 23 launches were already on disk. Joining each `game-start` journal record to `emulatorLauncher.log`'s millisecond `[Startup]` stamp gives a **median of +24 ms**, 20 of 23 between 12 and 44 ms. The hook's own start is ~60 ms, so ES spawned it ~36 ms _before_ emulatorlauncher began and did not block on it, and emulatorlauncher then took **0.5 s to 2.8 s** to reach `[Running]`. The cost of a spawn is contention, not latency. **CLAUDE.md rule 4 is untouched by this**: it forbids the hook touching the network, which was never a cost argument

## RB-209. Yes for `start` and `quit`

The claim being checked: (not addressed) whether the hooks close the loop unattended on real hardware

What was measured: **Yes for `start` and `quit`.** Two sessions: the `start` hook spawned a pass that reached the server and finished with exit 0 (`background start started` 21:51:50Z, `finished, flush exit 0` 21:51:54Z), and the `quit` hook did the same. The spool was drained into the journal both times without a terminal

## RB-212. Whether a hook-spawned pass carries a real play session and a real save to RomM with no terminal used

The claim being checked: (not addressed) whether a hook-spawned pass carries a real play session and a real save to RomM with no terminal used

What was measured: **Both, in one session.** The journal took `start` 22:17:07.7Z, `game-start` 22:17:16.0Z carrying `roms/mastersystem/Phantasy Star (Brazil).zip`, `game-end` 22:18:39.4Z and `quit` 22:18:42.6Z. The play session went up as outbox 24, `libretro:battery`, state `sent`. The save went up as **save 183**, and the server's hash equals the local one

## RB-213. Yes, with one second to spare

The claim being checked: (not addressed) whether the emulator's write really lands inside the window the `quit` pass then reads

What was measured: **Yes, with one second to spare.** RetroArch wrote the 32,768-byte `.srm` at **22:18:38.7Z**, `game-end` fired at 22:18:39.4Z, the `quit` hook at 22:18:42.6Z, the scan saw the new bytes at 22:18:43.1Z and the upload completed at 22:18:47.1Z. Nine seconds from the emulator closing to the save being on the server, unattended

## RB-214. The player's, and three hashes prove it rather than one

The claim being checked: (not addressed) whether the save that went up is the player's rather than a stale buffer being rewritten

What was measured: **The player's, and three hashes prove it rather than one.** The server held save 172 at `1177b02d`, the local file before the session was `338dd456`, and after it `391ecabd`, which is what uploaded. A RetroArch exit that merely flushed what it loaded would have reproduced `338dd456`, so SRAM changed during play

## RB-221. Not for a launch that succeeds

Question: **RB-20 and the M0 section above**: an ES-menu launch fires `game-end` with no preceding `game-start`

Measured: **Not for a launch that succeeds.** A real UI-driven launch fired **both**: `game-start` at 23:16:19.997Z carrying `system/es_menu/rommbat.menu`, and `game-end` at 23:16:44.497Z carrying nothing. M0 measured three launches driven by invoking `emulatorLauncher.exe` **directly**, two of which failed (`path is null`, exit 204). The consequence is unchanged and the code was already right: the discard keys on `IsMenuLaunch` from the launcher log and discards the paired `game-start` too

## RB-222. Whether RomMBat's own launch becomes a play session, for real rather than in a fixture

Question: (not addressed) whether RomMBat's own launch becomes a play session, for real rather than in a fixture

Measured: **It does not.** Both journal rows closed `discarded`, the outbox gained nothing, and its newest `play_session` is still 7a's Master System one at 2026-08-24T22:18:42Z. The `.menu` path was journalled **relative** (`system/es_menu/rommbat.menu`), so rule 1 held at the hook boundary

## RB-232. The whole loop closes and RomMBat still is not a play session

Question: (not addressed) what a real hands-on pass through the ES menu records, on the UI build rather than the stub

Measured: **The whole loop closes and RomMBat still is not a play session.** Two RomMBat sessions from the ES menu on 8.2.1 (62.9 s and 32.8 s) produced `game-start` and `game-end` pairs that both journalled **`discarded`**, with no outbox row, on the first build a person can actually sit in. A PS2 game launched immediately afterwards journalled **`correlated`** and produced outbox row 26, `play_session`, 54,762 ms, state **`sent`** by the detached `background quit` pass. Both paths journalled **relative** (`system/es_menu/rommbat.menu` and `roms/ps2/Armored Core 3 (USA).chd`), so rule 1 held at the hook boundary, and both launches carried **no arguments** (`[Running] RomMBat.exe`), which is RB-220 holding on a second occasion. Re-proves **221** and **222** against the UI rather than a fixture

## RB-346. Hooks do NOT block game launch

**This is the answer M0 called its most important number, and it is the good outcome.**

| Hook fired  | Launcher started | Delta      | Hook still sleeping until |
| ----------- | ---------------- | ---------- | ------------------------- |
| 20:08:00.19 | 20:08:00.219     | **0.03 s** | 20:08:08.35               |
| 20:09:27.84 | 20:09:27.873     | **0.03 s** | 20:09:36.01               |
| 20:10:14.05 | 20:10:14.078     | **0.03 s** | 20:10:22.22               |

`emulatorLauncher` started roughly **30 milliseconds** after the `game-start` hook began, on
all three launches, while that hook still had 8 seconds of sleep ahead of it. EmulationStation
spawns event scripts **fire-and-forget** and does not wait for them.

**The M6 hook budget is therefore not constrained by launch latency**, which removes the
risk the plan was most worried about. The journal-only rule still stands, but for a different
reason: see concurrency below.

## RB-347. Hooks run concurrently, including with each other

Because nothing waits, overlapping hooks interleave. `game-end` fired at 20:09:37.14 and
`quit` at 20:09:39.25, 2.1 seconds later, while `game-end` was mid-sleep. Both slept 8
seconds and both appended to the same log, so their writes interleaved: the `quit` header
landed between `game-end`'s header and `game-end`'s final line.

Later, **three `game-end` hooks were in flight at once** (20:11:57.76, 20:12:03.17,
20:12:07.63), each sleeping 8 seconds.

Two consequences:

1. **The lock file the plan requires is mandatory, not defensive.** Concurrent agent
   invocations are the normal case, not an edge case.
2. **The journal must tolerate interleaved appends from separate processes.** Line-level
   atomicity is not guaranteed by append mode alone across processes; a record that spans
   multiple writes can be split by another process's write.

## RB-348. Arguments: three, not five, and no emulator or core

RetroBat passes **three** arguments to `game-start`, against Batocera's documented five:

```text
ALL = G:\RetroBat\roms\ports\2048.libretro 2048 2048
  1 = G:\RetroBat\roms\ports\2048.libretro
  2 = 2048
  3 = 2048
  4 = (empty)
  5 = (empty)
```

Meanwhile `emulatorLauncher` was invoked with `-system ports -emulator libretro -core`. So
**the system, emulator and core are known to the launcher and withheld from the hook.**

This is a direct problem for M6, which derives the RomM `slot` as `{emulator}:{core}:{slot}`
and wants the emulator and core recorded alongside every state. **The hook cannot supply
any of the three.** They have to come from somewhere else: the per-system emulator choice in
`es_settings.cfg`, or the transient `-gameinfo` XML the launcher is handed (which lives in
`%TEMP%\emulationstation.tmp\game.xml`, outside the tree, and is not a durable source).

`$1` is an **absolute path**. Rule 1 forbids persisting it, so relativising against the
discovered root is mandatory work at the hook boundary, not an optimisation.

**`$2` and `$3` are now disambiguated**, from a launch where the rom stem and the display
name differ:

```text
ALL = K:\RetroBat\roms\ports\mrboom.libretro mrboom MrBoom
  1 = K:\RetroBat\roms\ports\mrboom.libretro     absolute rom path
  2 = mrboom                                      rom basename, extension stripped
  3 = MrBoom                                      gamelist <name>, NOT the system
```

So the real signature is `$1` rom path, `$2` basename, **`$3` display name**, with `$4` and
`$5` unused. Batocera documents `$3` as the system; in RetroBat it is the gamelist display
name, and the system is not passed at all. `$3` is also the argument whose spaces suppress
the hook entirely, described below.

## RB-349. `game-end` takes no arguments, and fires without a matching `game-start`

`game-end` received **zero** arguments on every occurrence, confirming Batocera's
documentation. It cannot identify the game that ended, so it has to be correlated with
something else. The plan assumes that something is the preceding `game-start`; the name-space
bug below means it cannot be, and `emulatorLauncher.log` has to serve instead.

More surprising: **launching an `es_menu` entry fires `game-end` with no preceding
`game-start`.** Three menu launches produced three `game-end` events and zero `game-start`
events. Two of those three launches _failed_ (`[Generator] Failed. path is null`, exit code 204) and `game-end` fired anyway.

That has a direct bearing on RomMBat itself, which is launched from that menu: **RomMBat
exiting will fire `game-end`**, and the agent must tolerate a `game-end` that closes nothing.
A naive implementation would attribute a play session to whatever game was launched last.

## RB-350. ES runs every script in an event folder

`start/` contains the shipped `updatestores.bat` and the probe's `zz-rommbat-probe.bat`.
Both ran, 63 ms apart, in alphabetical order (20:06:59.667 and 20:06:59.73). Installing a
hook alongside an existing one works, and the plan's append-don't-replace rule is satisfied
by simply adding a separate file.

## RB-351. `game-end` does fire when the emulator is killed

Confirmed in a second session. Two launches of the same game, distinguished only by how
they ended, with `emulatorLauncher.log` recording the exit code:

| Launch     | Ended by                                 | Launcher result                              | `game-end` hook               |
| ---------- | ---------------------------------------- | -------------------------------------------- | ----------------------------- |
| 23:05:29.4 | quit normally                            | `Process exited with code 0` at 23:05:35.367 | fired 23:05:35.45, **+83 ms** |
| 23:05:40.7 | `retroarch.exe` killed from Task Manager | `Process exited with code 1` at 23:06:06.424 | fired 23:06:06.49, **+66 ms** |

So a crashed or killed emulator still closes the journal record, and it does so as promptly
as a clean exit. The agent does not need a separate reaper for abandoned sessions, though it
still needs to treat a very long session as suspect since `game-end` cannot report _how_ the
game ended, only that it did.

## RB-352. RESOLVED, and later explained: a space in the game's display name stops a `.bat` hook from starting

The mechanism is in probe 7b below, and it is not what this section first concluded. ES fires
`game-start` for every game. What fails is the handoff from ES to the interpreter, so the
symptom belongs to the `.bat`, not to the event. An `.exe` hook is unaffected.

Across seven launches, `game-start` fired for every game whose gamelist `<name>` had no
space and for none whose name had one. A crossover confirmed the cause: the two entries
swapped names, nothing else changed, and the behaviour swapped with them.

| Rom file          | `<name>`                      | Launched OK | `game-start`     |
| ----------------- | ----------------------------- | ----------- | ---------------- |
| `2048.libretro`   | `2048`                        | yes, 4x     | **fired 4 of 4** |
| `mrboom.libretro` | `Mr Boom`                     | yes, 3x     | **fired 0 of 3** |
| `mrboom.libretro` | **renamed** `MrBoom`          | yes         | **fired**        |
| `2048.libretro`   | **renamed** `2048 Space Test` | yes         | **did not fire** |

Both crossover launches ran to completion (`Process exited with code 0`) and both fired
`game-end`. Only `<name>` changed.

The arguments a working launch receives look like this:

```text
ALL = K:\RetroBat\roms\ports\mrboom.libretro mrboom MrBoom
```

**The mechanism was undetermined here and is now measured**, in probe 7b. Briefly: ES quotes
any argument containing a space (`es-core/src/Scripting.cpp`, `fireEvent`:
`script += " \"" + arg + "\"";`) and hands the whole string to the shell. For a `.bat` that
means the `batfile` association, `cmd /c "%1" %*`, and cmd's quote-stripping rule then
mangles a line whose arguments carry their own quotes, so the batch file never starts. An
earlier revision of this document blamed unquoted arguments; that was wrong and is retracted.

The behaviour is nonetheless solid: it is crossover-confirmed, reproducible, and the script
does not execute at all, which is why _nothing_ appears in the log rather than a truncated
record. **Nothing downstream depends on the explanation**, only on the behaviour, so the
design conclusion below is unaffected.

**Why this is severe for a `.bat`.** Practically every real rom has spaces in its scraped
display name ("Super Mario World", "Metal Gear Solid (USA) (Disc 1)"). So on a real library a
`.bat` hook **would fail for very nearly every game**, and M6's journal, which opens its
record on `game-start`, would almost never see an opening record. The launch-window
attribution route for class-C and class-D saves depends on the same event and would fail
with it. Probe 7b's exe hook removes that, but only for a hook that is an exe.

**The mitigation is already in the tree: `emulationstation/emulatorLauncher.log`.** It is
written on every launch, timestamped to the millisecond, and carries strictly more than the
hook ever did:

```text
2026-08-08 23:27:32.048 [INFO] [Startup] "...\emulatorLauncher.exe" -gameinfo "..."
  -system ports -emulator libretro -core  -rom "K:\RetroBat\roms\ports\mrboom.libretro"
```

That single line supplies the rom path, **the system, the emulator and the core**, which
solves the separate problem that the hook withholds all three. Measured viability on a real
install: **268 KB covering 5 weeks and 70 launches**, with a two-file rotation
(`emulatorLauncher.log` plus `emulatorLauncher.log.old`).

**Recommended design change for M6.** Treat `game-end`, which fires reliably in every case
measured including crashes and menu launches, as the trigger, and read the launch facts from
`emulatorLauncher.log` rather than from the hook arguments. That recommendation survives
probe 7b: an exe `game-start` hook is reliable, but it is still never told the system, the
emulator or the core, and `game-end` still fires in cases that had no `game-start`. So open
the record on `game-start` and corroborate with it, and take the facts from the log. The
parser must read both rotated files and tolerate a rotation happening between reads.

**Filed upstream:** [RetroBat-Official/retrobat#249](https://github.com/RetroBat-Official/retrobat/issues/249)
(2026-08-09). Filed there rather than on `RetroBat-Official/emulationstation`, which has issues
disabled. Closed on 2026-08-21 as an upstream issue and refiled at
[batocera-emulationstation#2196](https://github.com/batocera-linux/batocera-emulationstation/issues/2196).
Its state is tracked in [upstream-issues.md](../issues.md).

A first reading of this attributed the inconsistency to hook concurrency, since the sessions
also differed in whether ES was restarted between launches. The crossover ruled that out:
the deciding variable is the name, not the timing.

## RB-382. The hook path arithmetic in the plan is off by one

Hooks live at `emulationstation/.emulationstation/scripts/<event>/`. The shipped
`scripts/start/updatestores.bat` is a single line:

```bat
%~dp0..\..\..\emulatorLauncher.exe -updatestores
```

`emulatorLauncher.exe` is at `emulationstation/emulatorLauncher.exe`, so **three levels up
reaches `emulationstation/`, not the RetroBat root.** Reaching the root from a hook needs
four:

```text
%~dp0..\..\..\        -> <root>/emulationstation/
%~dp0..\..\..\..\     -> <root>/
```

[`docs/PLAN.md`](https://github.com/Spinnich/rommbat/blob/3f3e16a33/docs/PLAN.md) cited `%~dp0..\..\..\` as the way a hook resolves the agent. The _pattern_ is right
and the technique is confirmed working, but a hook invoking an agent at
`<root>/emulators/rommbat/` needs `%~dp0..\..\..\..\emulators\rommbat\`.

**Corrected in the plan during M6**, which is the last place it was still wrong: the M0
experiment 4 description asked to "confirm the `%~dp0..\..\..\` pattern". RetroBat's own
`updatestores.bat` uses three levels because it is calling `emulatorLauncher.exe`, which sits
in `emulationstation/`, and that coincidence is what made the wrong number look confirmed.
RomMBat ships executable hooks that resolve the agent from their own module path, so nothing
depends on the count at runtime.

## RB-392. What did not: no hook produced anything on the second machine

**No ES hook produced any output on the second PC**: not `start`, not `game-end`, not `quit`.
`rommbat-probe\hooks.log` kept an mtime of 23:15:55 while ES demonstrably ran there from
23:18:00 to 23:18:38 (its own `es_log.txt` and the gamelist rewrite both prove the session).
The hook `.bat` files were present on the stick throughout.

This is not a permissions problem: **ES itself wrote `gamelist.xml` to that same stick during
that same session**, so the volume was writable. The `.bat` files simply were not executed.

The operator reports noticing nothing unusual on that machine, so there is no observed
SmartScreen or antivirus prompt to point at. **Cause undetermined**, and it needs a
deliberate diagnostic run on that PC rather than a guess.

**RESOLVED by probe 7b's rerun on that machine: it cannot launch a `.bat` or a `.ps1`, only
an `.exe`.** Every hook installed at the time of this run was a `.bat`, which is why nothing
at all appeared. With a `.bat`, a `.ps1` and an `.exe` installed side by side, the same host
fired all four events and produced an exe record for every one, while neither script form
produced anything, including for the three events that pass no arguments. See probe 7b
below. The mistaken reading, that the hooks did not fire, is retracted: they fired, and
nothing could run them.

**The design consequence stands, and is now sharper.** Shipping the hook as an executable is
what makes it work on this host, so that is a requirement rather than a preference. RomMBat
still cannot assume its hooks run on a given host, so it needs to **detect** the condition,
by writing a heartbeat from the `start` hook and noticing when a sync finds play data with no
corresponding hook activity, and to say so plainly rather than silently losing every play
session. It reinforces sourcing launch facts from `emulatorLauncher.log`, which recorded
**both** second-host sessions five weeks apart and is the only in-tree log that survives that
long.

## RB-394. Most of the evidence does not survive the next launch

Checked against the stick a day later: `es_log.txt` rotates through `es_log.0.txt` to
`es_log.3.txt` on **every ES start**, and **`RetroBat.log` at the root is overwritten
outright**. The second host's session was gone from both, which is why probe 7 could not be
diagnosed after the fact. Collect before RetroBat starts again, anywhere.

**`emulatorLauncher.log` is the exception, and it earns its place in the design.** It rotates
by size rather than per launch, so at 268 KB per 5 weeks it still held **both** second-host
sessions, five weeks apart, alongside everything from the first host. That is the durability
the M6 journal needs and neither ES log has.

## RB-395. ES logs its scripting decisions, but only on a log level nothing sets

`es_settings.cfg` accepts `<string name="LogLevel" value="debug" />`, and the default is
error-only: a whole session of `es_log.txt` held three `ERROR` lines and nothing else. On
`debug`, ES narrates the scripting path:

```text
DEBUG  fireEvent: game-start "<root>\roms\ports\mrboom.libretro" mrboom Mr Boom
DEBUG    queuing: <root>/emulationstation/.emulationstation/scripts/game-start/zz-rommbat-diag.bat <root>\roms\ports\mrboom.libretro mrboom "Mr Boom"
DEBUG    executing: <root>/emulationstation/.emulationstation/scripts/game-start/zz-rommbat-diag.bat <root>\roms\ports\mrboom.libretro mrboom "Mr Boom"
```

Three things fall out of that single trace. ES **quotes only arguments containing a space**,
not every argument. It writes the script path with **forward slashes**. And it reports
`executing:` for a script that demonstrably never ran, so **the log line is not evidence the
process started**, and no error is logged when it does not.

## RB-396. The failure is per interpreter, and `.exe` is the only form that survives a real name

A `.bat`, a `.ps1` and an `.exe` hook were installed side by side in all nine event folders,
each writing to `%TEMP%` first and the tree second, then three launches:

| Launch                                                   | `.bat` | `.ps1`          | `.exe` |
| -------------------------------------------------------- | ------ | --------------- | ------ |
| `2048`, no space anywhere                                | ran    | ran             | ran    |
| `Mr Boom`, space in the display name                     | **no** | ran, name split | ran    |
| `Gradius 2 (Japan, Europe) (En) (Wii U Virtual Console)` | **no** | **no**          | ran    |

ES logged `executing:` for all four scripts on all three launches, and two independent
`.bat` files (this probe's and probe 1's) stayed silent together. The exe received the
hardest case cleanly:

```text
ARGC=3
ARG0=[<root>\roms\msx1\Gradius 2 (Japan, Europe) (En) (Wii U Virtual Console).zip]
ARG1=[Gradius 2 (Japan, Europe) (En) (Wii U Virtual Console)]
ARG2=[Gradius 2 (Japan, Europe) (En) (Wii U Virtual Console)]
```

## RB-397. Both mechanisms, reproduced outside EmulationStation

Six invocations of a trivial logging `.bat` and `.ps1`, no ES involved
(`tools/m0-probes/` scratch harness, reproduced with `Process.Start`):

| Invocation                                            | Result                    |
| ----------------------------------------------------- | ------------------------- |
| ShellExecute `.bat`, plain arguments                  | ran                       |
| **ShellExecute `.bat`, one quoted argument**          | **nothing ran, no error** |
| `cmd /c "<path>" <args> "Mr Boom"`                    | nothing ran               |
| `cmd /c <bare path> <args> "Mr Boom"`                 | ran                       |
| `powershell <script.ps1> ... "Mr Boom"`               | ran, **4** args not 3     |
| `powershell <script.ps1> ... "(Japan, Europe)"`       | nothing ran, parse error  |
| `powershell -File <script.ps1> ... "(Japan, Europe)"` | ran, 3 args intact        |

So:

- **`.bat`**: ShellExecute resolves it through the `batfile` association, `cmd /c "%1" %*`.
  Quoting the script path is fine on its own, but once an argument carries its own quotes,
  cmd's quote-stripping rule mangles the line and the batch file never starts. Failure is
  silent, with no exception and no exit code to observe. **Any space anywhere is enough.**
- **`.ps1`**: ES builds `powershell <script> <args>` with **no `-File`**, so it is an
  implicit `-Command` and PowerShell reparses the tail as code. A space splits the display
  name across arguments; a parenthesis or comma is a parse error and nothing runs. `-File`
  fixes both, and ES does not pass it.
- **`.exe`**: no interpreter in the path, arguments arrive through ordinary
  `CommandLineToArgvW` splitting. Not observed to fail.

**Design consequence: RomMBat's ES hooks are executables, not `.bat` files.** RomMBat ships
a self-contained exe already, so this costs nothing. It also means the plan's claim that
`game-start` is unusable is withdrawn: the event was always firing.

Caveats that keep `emulatorLauncher.log` as the data source anyway. The hook is still never
told the system, emulator or core. `game-end` still fires with no preceding `game-start`.
And an unsigned exe on removable media is exactly the sort of thing a strict host may block,
which is the open question probe 7 left.

## RB-398. The second host, resolved: it cannot launch a script, only an executable

The rerun on the second host (a different Windows account, the stick mounted at `D:`) closes
probe 7's open item. **All four events fired**, and the exe hook recorded every one:

```text
=== exe EVENT=start       12:21:29   ROOT_RESOLVED=D:\RetroBat
=== exe EVENT=game-start  12:21:49   ARGC=3  ARG0=[D:\RetroBat\roms\ports\gong.libretro]
=== exe EVENT=game-end    12:27:33
=== exe EVENT=quit        12:27:39
```

**No `.bat` log and no `.ps1` log exists for that host at all**, while ES's own debug log
shows it resolved and reported `executing:` for all four scripts, both `.bat` files
included. Three of those four events (`start`, `game-end`, `quit`) pass **no arguments**, and
those same zero-argument cases run fine from a `.bat` on the first host. So this is not the
argument-quoting bug (#249, now
[batocera-emulationstation#2196](https://github.com/batocera-linux/batocera-emulationstation/issues/2196)):
**that machine cannot launch a `.bat` or a `.ps1` at all, and can launch an `.exe`.**

That explains probe 7's original total silence exactly. Every hook installed at the time was
a `.bat`, so nothing ran, and nothing was logged to say so.

Two theories are dead. `RetroBat.log` on that host records
`Launching D:\RetroBat\emulationstation\emulationstation.exe ... --home D:\RetroBat\emulationstation`,
so ES was started by `RetroBat.exe` and its home resolved to the stick. And the volume was
writable throughout, since the exe wrote to it.

## RB-399. Both causes, named, and neither is security software

A collector run on that host settles it. There are **two independent causes**, one per script
type, which is why nothing at all ran:

**`.bat` and `.cmd`: Notepad++ owns the association.**

```text
HKCR\.bat   (default) = Notepad++_file    Notepad++_backup = batfile
HKCR\.cmd   (default) = Notepad++_file    Notepad++_backup = cmdfile
HKCR\batfile\shell\open\command = "%1" %*        (intact, but unreachable)
HKCU\...\FileExts\.bat\UserChoice                (absent)
```

Notepad++'s installer offers file-association checkboxes covering `.bat`, and taking them
**replaces the `batfile` ProgId outright**, stashing the original in a `Notepad++_backup`
value. The `batfile` command is still correct and simply never consulted. This is not an Open
With choice; `UserChoice` is absent, so it is machine level and applies to every user.

**`.ps1`: the execution policy is `Restricted`**, the Windows client default, read
uncontaminated by clearing `PSExecutionPolicyPreference` before asking. ES passes no
`-ExecutionPolicy`, so the hook could never have run there. The first host reads
`RemoteSigned`, which is why the same file works there.

**Everything else is clean**, which matters because it rules out the theories that would have
been harder to design around: Defender only with realtime scanning on, **no** attack surface
reduction rules, no exclusions, an empty AppLocker policy, no removable-storage restriction
policy, and no Defender block events. That collection was taken **elevated**, so those are
real negatives rather than sections the collector skipped. Smart App Control is active and logged
`passed Config CI policy and was allowed to run`, so **an unsigned exe on removable media ran
under Smart App Control**, which is a useful result for RomMBat's own distribution.

**RetroBat is affected on this machine too.** Its own
`.emulationstation/scripts/start/updatestores.bat` cannot run there either, and just as
silently.

**Do not read this as a common configuration.** It is one machine out of the two tested, and
Notepad++ claims `.bat` only if its file-association option is selected during install, which
is not the default. The useful part is not the frequency, which this sample cannot measure,
but the failure mode: **an ordinary application can take the association, and everything
downstream fails with no error anywhere.**

## RB-400. Byproducts

- **ES fires `game-selected` and `system-selected`** on every navigation move, and
  `game-selected` carries `<system> <rom path> <display name>`, which is the system the
  `game-start` hook is not given. Neither has a folder under `scripts/` by default. Chatty
  (one per cursor move), so useful only as a last-known-selection hint.
- **Working directory differs by hook form**: a `.bat` gets its own folder, a `.ps1` gets
  ES's home, an `.exe` gets its own folder. Nothing should depend on it.
- **`RetroBat.log` records the ES command line**, including
  `--home <root>\emulationstation`. No `HOME` variable exists in the process, user or machine
  environment, so ES started by anything other than `RetroBat.exe` would resolve its scripts
  directory under `%USERPROFILE%` instead of the tree. Not what happened on the second host,
  whose `RetroBat.log` shows `--home` passed correctly, but it stays a real failure mode for
  anyone launching `emulationstation.exe` directly.
  `HKCU\Software\RetroBat\LatestKnownInstallPath` records per user profile whether
  `RetroBat.exe` has ever run there.
