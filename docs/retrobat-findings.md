# RetroBat findings (M0)

Measurements from a real RetroBat install and a real network. **Every number here is only
true for the versions named below.** Re-run the probes in `tools/m0-probes/` before trusting
any of it against a different build.

|                    |                                                                   |
| ------------------ | ----------------------------------------------------------------- |
| RetroBat           | `8.2.0-stable-win64`, read from `system/version.info`             |
| Re-checked against | `8.2.1-stable-win64` on 2026-08-25, see below                     |
| RomM               | `5.1.1-beta.1`, read from `GET /api/heartbeat` → `SYSTEM.VERSION` |
| Library under test | 83,131 roms at M0, 83,435 by M4, host redacted                    |
| Host OS            | Windows 11 Pro 10.0.26200                                         |
| .NET               | 10.0.302                                                          |
| Date               | 2026-08-08, extended through M4 on 2026-08-11                     |

Paths are written relative to the RetroBat root and the instance host is redacted, per the
repo rules.

## Probe status

| #   | Probe                                 | State    | Confidence                                                                                          |
| --- | ------------------------------------- | -------- | --------------------------------------------------------------------------------------------------- |
| 1   | ES hook arguments and launch blocking | **done** | blocking, args, concurrency, crash case measured; the `game-start` failure now has its mechanism    |
| 7b  | Why hooks fail, and which form works  | **done** | ES debug logging plus a three-way `.bat`/`.ps1`/`.exe` install; reproduced outside ES               |
| 2   | Save locations and shapes             | **done** | 11 of 13 driven live, 12 launched; **two** declared directories are wrong (`flycast`, `openmsx`)    |
| 3   | Library refresh                       | **done** | ES HTTP API found and exercised live; refresh works without a restart                               |
| 4   | Root discovery and app registration   | **done** | `.menu` rooting measured directly, result changes the layout                                        |
| 5   | Scale probe                           | **done** | API measured against 83k roms; the gamelist ceiling is not where the plan feared                    |
| 6   | Offline behaviour                     | **done** | both halves measured                                                                                |
| 7   | Portable move                         | **done** | two PCs, three drive letters, FAT32 and exFAT measured; the second host's hook failure is explained |

Probe 2 is now done to the limit of what this hardware allows. The per-game
`es_settings.cfg` override works, in both halves the plan asked about, **twelve of the
thirteen `es_savestates.cfg` emulators have been installed and launched, and eleven have been
driven to a real save state**. `bizhawk` joined the list once it was launched with `-core`;
`bigpemu` is the one that got away, because its save state is reachable only through a
gamepad-driven overlay menu.

**The single most important result is that `es_savestates.cfg`'s `<directory>` cannot be
trusted: on 8.2.1, one of the twelve is wrong.** **`openmsx` writes to
`bios/openmsx/savestates/`, a different top-level tree from the declared
`saves/msx1/openmsx`**, and that is unfixed. `flycast` was the second when this was measured
on 8.2.0, writing `reicast/states` against a declared `flycast/sstates`; RetroBat 8.2.1 fixed
it (`emulatorlauncher#1336`,
[below](#1336-fixed-in-821-driven-and-the-workaround-is-out)), so Dreamcast states now sync
from the declaration. One wrong out of twelve is all the rule needs, and it still holds: an
empty declared directory means you are looking in the wrong place, never that the game has no
states. Filenames, by contrast, were correct
everywhere they could be checked, with one collision worth knowing about: DeSmuME's
`{{romfilename}}.ds{{slot0}}` also matches its own `.dsv` battery save if the slot is
expanded as a wildcard.

**Probe 7b then overturned the single most damaging result in probe 1.** `game-start` was
never broken. ES fires every event and logs `executing:` for every script; what fails is the
handoff to an interpreter. A `.bat` never starts once any argument is quoted, a `.ps1` never
starts once the name contains a parenthesis, and an `.exe` takes a full No-Intro name as
three intact arguments. **RomMBat's hooks are executables**, and both scripted failures
reproduce outside EmulationStation.

Probes 5 and 7 closed their remaining items. **A 100,000-entry gamelist loads in about two
seconds and costs ES 208 MB**, so the per-system cap M4 needs is a navigability decision
rather than a technical ceiling. And **exFAT stores modification times exactly as coarsely as
FAT32, 2 seconds, rounded up**, which puts a freshly written save's mtime as much as two
seconds in the future.

## Upstream issues filed

Three measurements here are RetroBat bugs rather than facts to design around. All were
reported on 2026-08-09. **Two are now resolved and one is still open**; the table carries the
state as of 2026-08-25, checked against RetroBat 8.2.1.

| Issue                                                                            | Repo                                       | State                       | What it covers                                                             |
| -------------------------------------------------------------------------------- | ------------------------------------------ | --------------------------- | -------------------------------------------------------------------------- |
| [#2196](https://github.com/batocera-linux/batocera-emulationstation/issues/2196) | `batocera-linux/batocera-emulationstation` | Open                        | `game-start` never runs when the gamelist `<name>` has a space             |
| [#1336](https://github.com/RetroBat-Official/emulatorlauncher/issues/1336)       | `RetroBat-Official/emulatorlauncher`       | **Fixed in RetroBat 8.2.1** | Flycast writes states to `reicast/states`, not the declared path           |
| [#1337](https://github.com/RetroBat-Official/emulatorlauncher/issues/1337)       | `RetroBat-Official/emulatorlauncher`       | Closed, will not be fixed   | BizHawk crashes on an unguarded `inputPortNb[core]` when `-core` is absent |

A fourth candidate was investigated and **not** filed, because it is not RetroBat's bug:
openMSX never received Alt+F2 in one run because NVIDIA's Photo mode overlay claimed the
combination first.

### #2196: the ES hook bug, moved repository

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
[batocera-emulationstation#2196](https://github.com/batocera-linux/batocera-emulationstation/issues/2196),
where it is open. The mechanism, the retitle and the two verified fixes carried across
unchanged. **Nothing about the design moves**: the hooks stay `.exe`, and a fix landing would
reopen the simpler `.bat` journal design the plan originally wanted.

### #1336: fixed in 8.2.1, driven, and the workaround is out

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
matching `data/retrobat/bios.json` exactly (`e10c53c2…`, `0a93f7940…`). That is the M5
md5-only join working end to end on real data, on a system whose third manifest entry
(`bios/dc/dc.zip`) carries no hash at all.

### #1337: will not be fixed, and it costs RomMBat nothing

It was the low-severity one, deliberately reported as such. EmulationStation always passes a
core, and all 36 BizHawk cores this install's `es_systems.cfg` declares are among the 42 keys in
`inputPortNb`, so only direct invocation or a future unlisted core can reach it.

Upstream closed it on 2026-08-11, saying they will not fix it because there is no reason to run
`emulatorLauncher` directly. RomMBat **is** a direct invoker, so the constraint stands, and it is
now a permanent property of the launcher rather than a workaround waiting on a fix: **pass
`-core`**, which was always correct anyway.

### The standing rule

**Re-check every open issue here before each release**, because a fix upstream does not just
close a ticket, it changes what RomMBat should do. No workaround comes out until the fix is in a
release RomMBat's compatibility gate accepts and a hands-on pass has seen the fixed behaviour: a
changelog line is evidence that upstream believes it is fixed, not evidence of what lands on
disk.

---

## Probe 1: ES hooks (complete)

Measured on the G: install with echo hooks in all nine event folders, an 8 second sleep in
each hook, and `emulatorLauncher.log`'s millisecond timestamps as ground truth
(`tools/m0-probes/probe1-install-hooks.ps1`).

### Hooks do NOT block game launch

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

### Hooks run concurrently, including with each other

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

### Arguments: three, not five, and no emulator or core

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

### `game-end` takes no arguments, and fires without a matching `game-start`

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

### ES runs every script in an event folder

`start/` contains the shipped `updatestores.bat` and the probe's `zz-rommbat-probe.bat`.
Both ran, 63 ms apart, in alphabetical order (20:06:59.667 and 20:06:59.73). Installing a
hook alongside an existing one works, and the plan's append-don't-replace rule is satisfied
by simply adding a separate file.

### `game-end` does fire when the emulator is killed

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

### RESOLVED, and later explained: a space in the game's display name stops a `.bat` hook from starting

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
[batocera-emulationstation#2196](https://github.com/batocera-linux/batocera-emulationstation/issues/2196),
where it is **open**.

A first reading of this attributed the inconsistency to hook concurrency, since the sessions
also differed in whether ES was restarted between launches. The crossover ruled that out:
the deciding variable is the name, not the timing.

---

## Probe 6b: reachability timeout (complete)

**This is the most consequential number measured so far, and it invalidates the plan's
assumption that the OS timeout can serve as the UI budget.**

Raw TCP connect, no client-side timeout, 5 repetitions each
(`tools/m0-probes/probe6-reachability.ps1`):

| Case                                           | First    | Median       | Max      | Error                       |
| ---------------------------------------------- | -------- | ------------ | -------- | --------------------------- |
| **Host absent, address inside the LAN subnet** | 21092 ms | **21049 ms** | 21093 ms | `TimedOut` (10060)          |
| Host up, port closed                           | 2039 ms  | 2040 ms      | 2041 ms  | `ConnectionRefused` (10061) |
| Off-subnet blackhole (TEST-NET-1)              | 5613 ms  | 5668 ms      | 11631 ms | mixed                       |
| Hostname does not resolve                      | 45 ms    | 0.8 ms       | 45 ms    | `HostNotFound` (11001)      |

The case that matters is the first one, and it is the common one: the RomM box is powered
off or unplugged, but its address is still a valid address on the user's subnet. **21
seconds, every time.** It does not improve across repetitions, so there is no negative-ARP
caching to lean on. A UI that calls a reachability check on this path without its own
timeout appears frozen for 21 seconds.

Through `HttpClient`, which is what actually ships
(`tools/m0-probes/probe6-httpclient.cs`):

| Configuration                           | Elapsed  | Exception chain                                                      |
| --------------------------------------- | -------- | -------------------------------------------------------------------- |
| Default handler                         | 21113 ms | `HttpRequestException -> SocketException(TimedOut)`                  |
| `HttpClient.Timeout = 5s`               | 5005 ms  | `TaskCanceledException -> TimeoutException -> TaskCanceledException` |
| `ConnectTimeout = 1s`                   | 1021 ms  | `TaskCanceledException -> TimeoutException`                          |
| `ConnectTimeout = 2s`                   | 2014 ms  | `TaskCanceledException -> TimeoutException`                          |
| `ConnectTimeout = 3s`                   | 3008 ms  | `TaskCanceledException -> TimeoutException`                          |
| `ConnectTimeout = 3s` + `Timeout = 10s` | 3008 ms  | `TaskCanceledException -> TimeoutException`                          |
| User cancels via token after 1s         | 1006 ms  | `TaskCanceledException -> TaskCanceledException`                     |

Three things follow, and all three are binding on `RomM.Client`:

1. **A default `HttpClient` inherits the full 21 second stall.** `SocketsHttpHandler.ConnectTimeout`
   must be set explicitly on every client instance. It caps the wait precisely (within 20 ms
   of the requested value at every value tested), so the mitigation works, but nothing
   applies it by default.
2. **`HttpClient.Timeout` is the wrong lever.** It bounds the whole request including the
   response body, so setting it low enough to make reachability feel responsive would abort
   legitimate large downloads. `ConnectTimeout` bounds only the TCP handshake, which is
   exactly the thing that hangs. Set both, for different reasons.
3. **An unreachable host and a user cancellation are the same exception type.** Both surface
   as `TaskCanceledException`. They are distinguishable only by the inner exception
   (`TimeoutException` for a timeout, absent for a real cancellation) or by checking the
   token's `IsCancellationRequested`. Code that does `catch (TaskCanceledException)` and
   reports "cancelled" will silently mislabel every offline server as a user action.

**Recommended budget: `ConnectTimeout = 2s` for interactive reachability checks.** Two
seconds is above the LAN RTT by orders of magnitude, so it will not produce false negatives
on a healthy network, and it keeps a failed check inside the window where a spinner still
reads as responsive. Sync operations that are already known to be long-running can afford a
longer connect timeout; the UI probe cannot.

---

## Probe 5: scale (mostly complete)

Measured against a **83,131 rom** library on RomM `5.1.1-beta.1` over a LAN, 3 repetitions
per point, median reported (`tools/m0-probes/probe5-scale.py`). Reads only.

### The sidecars are a fixed cost repeated on every page

`GET /api/roms` has **four** flags that default to `true`, not three as the plan says:
`with_char_index`, `with_filter_values`, `with_rom_id_index`, `with_total`. A fifth,
`with_files`, is opt-in.

| Page size    | Sidecars on | Sidecars off | On      | Off     | Sidecar bytes |
| ------------ | ----------- | ------------ | ------- | ------- | ------------- |
| 10           | 366 ms      | 142 ms       | 879 KB  | 38 KB   | 841 KB        |
| 25           | 481 ms      | 345 ms       | 1137 KB | 295 KB  | 842 KB        |
| 50 (default) | 746 ms      | 677 ms       | 1298 KB | 456 KB  | 842 KB        |
| 100          | 1181 ms     | 1084 ms      | 1816 KB | 975 KB  | 841 KB        |
| 250          | 2616 ms     | 2454 ms      | 2913 KB | 2072 KB | 841 KB        |
| 500          | 4881 ms     | 5012 ms      | 5235 KB | 4394 KB | 841 KB        |
| 1000         | 8638 ms     | 8391 ms      | 8846 KB | 8004 KB | 842 KB        |

**The sidecar payload is a flat ~841 KB on every request regardless of page size.** It does
not scale with the page, it is simply resent. Isolated at `limit=10`, where the page itself
is only 38 KB:

| Flag                 | Cost when on      |
| -------------------- | ----------------- |
| `with_rom_id_index`  | **582 KB**        |
| `with_filter_values` | **280 KB**        |
| `with_char_index`    | 0.3 KB            |
| `with_total`         | 0 KB (an integer) |

At the default page size of 50, **65% of the response body is sidecar**. At the default of
`limit=50` with defaults on, walking the whole 83k library takes 1663 pages and would resend
roughly **1.4 GB** of identical sidecar data.

Server time is barely affected (per-flag deltas at `limit=100` were within noise, plus or
minus 55 ms), so **this is a bandwidth and parsing cost, not a database cost**. That is
good news: the fix is free.

**Conclusions for M2:**

- **Request the sidecars once, then disable all four for every subsequent page.** They are
  index and filter metadata for the whole library, not per-page data.
- **Default page size 250 with sidecars off.** Latency is close to linear in page size
  (roughly 10 ms per rom across the range), so larger pages buy little per-rom throughput
  but cost responsiveness and make resumption coarser. 250 gives a 2.5 s page and 333 pages
  for this library.
- **A full catalog walk of 83k roms takes about 14 minutes** at that setting. Sync must be
  resumable and incremental (`updated_after` exists on the endpoint and should be the normal
  path); a full walk is a first-run or repair operation, not routine.

### `GET /api/collections` is not a cheap list

One collection returned **714.8 KB**. The breakdown explains it:

| Field               | Size      | Items |
| ------------------- | --------- | ----- |
| `path_covers_small` | 359,073 B | 4433  |
| `path_covers_large` | 350,207 B | 4433  |
| `rom_ids`           | 35,640 B  | 4455  |
| everything else     | ~130 B    |       |

**99% of the response is two inlined arrays of cover-art paths, one entry per member rom,
duplicated at two sizes.** There is no pagination on `/api/collections`. A user with 20
collections of this size would pull roughly 14 MB to render a list of 20 names.

Note also `rom_ids` has 4455 entries while the cover arrays have 4433, so the arrays are not
positionally aligned with `rom_ids` and must not be zipped together.

`/api/collections/smart` returned 28 items in 67 KB, which is proportionate.
`/api/collections/virtual` returned **HTTP 422** without parameters, so it requires
arguments the other two do not; the client must not treat the three as interchangeable.

### The gamelist ceiling: there isn't one worth designing around

The last open item, measured on the live install with synthetic corpora in an otherwise empty
`roms/snes` (`tools/m0-probes/probe5-gamelist.ps1`). Each row is a cold ES start against that
size, timed from process start to `/systems` reporting the full count, since ES parses the
gamelists **before** it opens the HTTP port and `/caps` therefore tracks total startup rather
than preceding the load.

| Entries     | `gamelist.xml` | Cold start | Working set | Read the list | Reload effect |
| ----------- | -------------- | ---------- | ----------- | ------------- | ------------- |
| 200 (floor) | 0.13 MB        | 1.67 s     | 211 MB      | 15 ms         | 269 ms        |
| 1,000       | 0.65 MB        | 1.60 s     | 216 MB      | 24 ms         | 533 ms        |
| 5,000       | 3.2 MB         | 1.55 s     | 225 MB      | 104 ms        | 260 ms        |
| 10,000      | 6.5 MB         | 1.54 s     | 240 MB      | 167 ms        | 274 ms        |
| 25,000      | 16.2 MB        | 1.55 s     | 272 MB      | 401 ms        | 538 ms        |
| 50,000      | 32.5 MB        | 2.05 s     | 312 MB      | 769 ms        | 525 ms        |
| **100,000** | **65.0 MB**    | **2.07 s** | **419 MB**  | 1457 ms       | 1084 ms       |

**100,000 entries in one system cost ES about half a second of startup and 208 MB.** Memory
is linear at roughly **2 MB per 1,000 entries**, and cold start barely moves: 1.5 s at every
size up to 25k, 2.05 s from 50k. Nothing here degrades, breaks or thrashes.

Repeated at 100,000 with a real image file per entry on disk, since a gamelist that references
artwork nobody has is the optimistic case:

| 100,000 entries     | Cold start | Working set |
| ------------------- | ---------- | ----------- |
| metadata only       | 2.07 s     | 419 MB      |
| with 100,000 images | **2.93 s** | 402 MB      |

Artwork costs **0.9 s of startup and no memory at load**, which says ES stats the files during
the scan and decodes textures lazily while browsing.

**So the per-system gamelist cap the plan wanted from this probe is not an ES limit.** Core
principle 2's "a 100k-entry gamelist would make EmulationStation unusable" is not supported:
ES loads exactly that in under three seconds. The reason to cap a gamelist is that **a human
cannot navigate 100,000 entries with a gamepad**, which is core principle 3's curation
argument and a product decision, not a measured ceiling. M4 should enforce a cap for
navigability, and can stop treating a large gamelist as a technical hazard.

Two caveats, stated because they bound the claim:

- **This measures loading, not scrolling.** Cold start, reload, working set and the API read
  are all readable from ES; on-screen scroll smoothness is not, and nothing here substitutes
  for it.
- `/reloadgames` returns in 1-2 ms and does the work afterwards, so its response time measures
  nothing. The reload column above is the time until ES reports a change made on disk, which
  is what M4 actually waits for. Even at 100k that is **about a second**.

---

## Probe 6a: interrupted downloads (complete)

The question the plan asks is what happens to an in-flight download when the link drops.
What actually governs the design is what can happen _next_, so this was measured by killing
the client mid-transfer rather than by disturbing a live network
(`tools/m0-probes/probe6a-resume.sh`), using a 19.2 MB rom.

**Resumable download works, and RomM's implementation is correct.** The plan lists resumable
downloads as hand-written client work; that work is viable.

`HEAD /api/roms/{id}/content/{file_name}` returns everything a resumable client needs:

```text
Accept-Ranges: bytes
Content-Length: 19238769
ETag: "6a207214-1258f71"
Last-Modified: Wed, 03 Jun 2026 18:27:32 GMT
```

| Test                                               | Result                                                            |
| -------------------------------------------------- | ----------------------------------------------------------------- |
| `Range: bytes=100-1123`                            | **206**, exactly 1024 bytes                                       |
| Kill at 9,433,088 of 19,238,769 bytes, then resume | **206**, exact final size, **byte-identical to a clean download** |
| `If-Range` with the current ETag                   | **206** partial, resume proceeds                                  |
| `If-Range` with a stale ETag                       | **200** full body, so the client restarts safely                  |
| No `Authorization` header                          | **401**                                                           |

Two requirements for `RomM.Client` follow:

1. **Always send `If-Range` with the stored ETag when resuming.** The server handles it
   correctly, returning a full 200 body when the validator no longer matches. A client
   sending a bare `Range` after the file changed on the server would splice two different
   files together and produce a corrupt rom that still has the right length. Handle the 200
   by discarding the partial file and starting over.
2. **Do not parse the filename out of `Content-Disposition`.** The header carries both forms,
   and the plain `filename=` fallback is **percent-encoded rather than plain text**:

   ```text
   filename*=UTF-8''2%20Disney%20Games%20-%20...zip; filename="2%20Disney%20Games%20-%20...zip"
   ```

   A client reading the unstarred parameter gets a literal `%20`-laden name and writes files
   ES cannot match to a gamelist entry. Use `fs_name` from the rom record instead.

What this does **not** cover is a genuine link-layer drop, where the socket stalls rather
than closing. That is the case where probe 6b's 21 second OS timeout applies, and it is why
a read timeout matters separately from `ConnectTimeout`.

---

## Probe 2: save locations and shapes (complete)

### The per-game `es_settings.cfg` override works, in both halves (complete)

**This is the load-bearing result for the whole class-D conversion story.** Every mechanism
the plan proposes for turning a shared save container into a per-game one (PCSX2
`pcsx2_slot1_memory=game`, DuckStation `PerGameFileTitle`, `flycast_vmupergame`) is written
through the per-game form of `es_settings.cfg`, and neither half of that form had been
measured. Both now are (`tools/m0-probes/probe2-per-game-override.ps1`).

`smooth` is used as the test key because it lands in the regenerated
`emulators/retroarch/retroarch.cfg` as `video_smooth`, so every result is read from disk
rather than judged on screen. The per-game value is always the one that _differs_ from the
stock value, so "honoured" and "ignored" cannot both look like the baseline.

| Case | `es_settings.cfg`                                    | Launched        | `video_smooth` | Shows                           |
| ---- | ---------------------------------------------------- | --------------- | -------------- | ------------------------------- |
| A    | nothing                                              | `gong.libretro` | `false`        | baseline                        |
| B    | `ports.smooth=1`                                     | `gong.libretro` | **`true`**     | system scope is honoured at all |
| C    | `ports.smooth=1` + `ports["2048.libretro"].smooth=0` | `2048.libretro` | **`false`**    | **per-game beats system**       |
| D    | same as C                                            | `gong.libretro` | `true`         | the override does not leak      |
| E    | `ports.smooth=1` + `ports["gong"].smooth=0`          | `gong.libretro` | `true`         | **basename form is ignored**    |
| F    | `ports.smooth=1` + `ports["gong.libretro"].smooth=0` | `gong.libretro` | **`false`**    | E's pair, extension restored    |

Three things follow, and all three bind M6:

1. **`emulatorlauncher` honours `<system>["<rom filename>"].<key>`, and it outranks the
   system-scoped key.** The plan's precedence chain is confirmed at the level that matters.
2. **The key must carry the full rom filename including its extension.** E and F differ in
   nothing but the extension on the same rom, and only F took effect. So the key is built
   from RomM's `fs_name`, never from a stem. Getting this wrong fails **silently**: the
   emulator launches normally and simply keeps writing to the shared container.
3. **The override is scoped to exactly one rom** (D), so per-game opt-in really is per-game
   and does not quietly re-configure a user's whole system.

C was re-run through the genuine path as a cross-check, launching 2048 with
`POST http://127.0.0.1:1234/launch` so EmulationStation invoked `emulatorLauncher` itself
rather than the probe doing it. Same result, `video_smooth = "false"`.

The escaping is unremarkable and matches what ES writes itself:

```xml
<string name="ports[&quot;2048.libretro&quot;].smooth" value="0" />
```

### ES only rewrites `es_settings.cfg` when a setting actually changed, and it keeps keys it does not understand

The second half of the question was whether an ES restart survives the override. It does,
but measuring it turned up a **correction to what this document previously recorded**.

The first attempt proved nothing: ES was started, given five seconds, and quit through
`GET /quit`, and **`es_settings.cfg` was never written at all**, mtime unchanged to the
second. A second session that went further and _launched a game_ through `POST /launch`
also left the file untouched. So the blanket claim "ES rewrites `es_settings.cfg` on exit"
is wrong as stated: **the write is conditional on something having changed during the
session.** The earlier mtime evidence came from sessions where an operator was navigating
the UI, which is what dirtied it.

To measure the case that matters, the session was forced dirty by pointing `LastSystem` at
a system with no games, so ES falls back to a real one and has a genuine change to save.
That run did rewrite the file, and the result is the good one:

| Written before the session                                | Present after ES rewrote it |
| --------------------------------------------------------- | --------------------------- |
| `ports.smooth`                                            | **kept**                    |
| `ports["2048.libretro"].smooth`                           | **kept**                    |
| `ports["2048.libretro"].rommbat_probe_unknown` (nonsense) | **kept**                    |

The nonsense key is the informative one: ES preserved a key it can have no knowledge of, so
this is not "ES models the per-game form" but the stronger and more useful **ES round-trips
what it does not recognise**. What ES does change on rewrite is cosmetic and must be
tolerated rather than fought: it re-indents with tabs, and it sorts entries alphabetically
within the `bool`, `int`, `string` groups.

**One real pruning behaviour, and it is a trap.** `<string name="Language" value="en_US" />`
was present before the rewrite and **gone after it**. Setting the same key to `fr_FR` and
repeating the run, it survived. So **ES drops any setting whose value equals its own
default** and keeps the rest. Custom keys are safe because ES has no default to compare
them against, but any code that writes an ES-known key at its stock value must expect the
entry to vanish, and must not read that absence as tampering.

**What this means for M6. Superseded by findings 178 and 179; read those instead.** This
section concluded that the override was durable and that the merge-don't-clobber rule stood
only for the ordinary reason that two writers share a file. That conclusion holds **only for a
write made before ES starts**, which is the one case measured here. Driven the other way, with
ES running, an atomic merged write was discarded by ES's next write. ES serialises a model
loaded at startup, so the key survived above because it predated the load, not because ES
tolerates it. "Write while ES is idle" turns out to be the whole mechanism rather than
prudence.

### The declared schema

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

### What the real save tree contains

Inventoried from a live install with a substantial library
(`probe-output/saves_observed.json`). **The single biggest structural finding is that the
plan's mental model of the saves tree is wrong.**

**Saves are `saves/<system>/<emulator>/...`, not `saves/<system>/`.** Every system that uses
a standalone emulator gets an emulator-named subdirectory (`ps2/pcsx2`, `dreamcast/flycast`,
`saturn/kronos`, `3ds/azahar`, `wii/dolphin-emu`). Only libretro battery saves land loose at
`saves/<system>/*.srm`. Worse, there are also **emulator-named folders at the top level**
(`saves/dolphin/`, `saves/mesen/`, `saves/psxmame/`, `saves/amiga/`) that sit beside the
system folders rather than under them. `saves/dolphin/User/GC/SRAM.USA.raw` exists at the
same time as `saves/gamecube/dolphin-emu/User/GC/`. Any code that assumes a save path
begins with a system name will mis-attribute these.

Observed shapes, with the evidence:

| System                                                                       | Observed            | Notes                                                                                                                                                                                                                        |
| ---------------------------------------------------------------------------- | ------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `nes`, `snes`, `gb`, `gbc`, `gba`, `megadrive`, `n64`, `pcengine`, `sega32x` | **A**               | loose `.srm`, ROM-filename-keyed. The easy case, and it is the majority.                                                                                                                                                     |
| `psx`                                                                        | **A**               | loose `.srm`, so this install runs libretro for PS1, **not** DuckStation. The plan's claim that PS1 is already per-game via DuckStation's `PerGameTitle` default does not apply unless DuckStation is the selected emulator. |
| `saturn`                                                                     | **B**               | `.bcr` (512 KB) **and** `.bkr` (32 KB) per game, both present for every game.                                                                                                                                                |
| `megacd`                                                                     | **B and D at once** | per-game `.brm` + `.srm`, **plus a shared `4Mbit_cart.brm` (512 KB)** holding the RAM cart for all games. Class D, and not in the plan's class-D table.                                                                      |
| `mame`                                                                       | **C**               | `mame/nvram/<shortname>/`, **1231 directories**. Keyed by MAME short name, which _is_ the ROM basename, so attribution here is trivially solvable by filename, unlike the other class-C cases.                               |
| `psp`                                                                        | **C**               | `psp/SAVEDATA/<GAMEID>SYSDATA/` containing `PARAM.SFO`. Game-ID-keyed as predicted.                                                                                                                                          |
| `ps3`                                                                        | **C**               | `ps3/rpcs3/dev_hdd0/home/00000001/savedata` and `dev_hdd0/savedata`. **32451 files** under `ps3/rpcs3/`, which is a real performance constraint on any recursive hash.                                                       |
| `gamecube`                                                                   | **C, multi-file**   | see below                                                                                                                                                                                                                    |
| `wii`                                                                        | **C**               | full NAND tree at `wii/dolphin-emu/User/Wii/title/...`, alongside a lot of shared system state that is not per-game.                                                                                                         |
| `dreamcast`                                                                  | **D**               | see below                                                                                                                                                                                                                    |
| `xbox`                                                                       | **D**               | `xbox/eeprom.bin` (256 B) and `xbox/xbox_hdd.qcow2` (38 MB). A whole disk image, shared by every game.                                                                                                                       |

### Flycast VMU: the plan's one unverified class-D case, answered

`PLAN.md` line 870 records Dreamcast VMU handling as unverified and assigns it to M0. The
tree answers it:

```text
saves/dreamcast/flycast/vmu/vmu_save_A1.bin
saves/dreamcast/flycast/vmu/vmu_save_B1.bin
saves/dreamcast/flycast/vmu/vmu_save_C1.bin
saves/dreamcast/flycast/vmu/vmu_save_D1.bin
```

Four files, one per **controller port** (A through D), slot 1 on each. They are keyed by
port, shared by every Dreamcast game, and nothing in the path identifies a game. That is
class D in its purest form.

**But it converts.** `es_features.cfg` declares the option:

```xml
<feature submenu="EMULATION" name="PER GAME VMU" group="ADVANCED SETTINGS"
         value="flycast_vmupergame" preset="switchauto" order="104"
         description="When enabled, each game will have its own VMU in port 1."/>
```

So Dreamcast joins the convertible set rather than the unsyncable one. One caveat is
written into the description itself: **only port 1 becomes per-game.** Ports B, C and D
remain shared and unattributable, so a game that writes to a second VMU still produces
something RomMBat cannot map to a `rom_id`.

### And driven: the conversion works, but the per-game VMU is keyed by disc serial

Two launches of Bangai-O (USA), differing only in the override
(`tools/m0-probes/probe2-vmu-pergame.ps1`). The shared VMU files were copied aside first and
restored afterwards, so the install's real Dreamcast saves were not at risk.

| Run                    | `emu.cfg`              | What appeared in `saves/dreamcast/flycast/vmu/`            |
| ---------------------- | ---------------------- | ---------------------------------------------------------- |
| control, no override   | `PerGameVmu = no`      | nothing new; **shared `vmu_save_A1.bin` changed at exit**  |
| `flycast_vmupergame=1` | **`PerGameVmu = yes`** | **new `T40217N_vmu_save_A1.bin`, 131,072 B, written live** |

Four results, and the second one is the awkward one:

1. **The per-game `es_settings.cfg` key reaches a standalone emulator's generated config.**
   The override was proven on a libretro key; this shows the same mechanism driving
   `PerGameVmu` in Flycast's own `emu.cfg`, so it generalises past RetroArch.
2. **The per-game VMU is named after the disc's product number, not the rom file.**
   `T40217N` is Bangai-O's Dreamcast serial (`T-40217N` with the hyphen dropped); the rom is
   `Bangai-O (USA).chd`, and its name appears nowhere in the path. **So this is not the clean
   collapse into class A that DuckStation's `PerGameFileTitle` gives**, where the card is
   named after the rom file. RomMBat cannot build this path from `fs_name`; attributing a
   Dreamcast VMU means either reading the serial out of the disc image or attributing by
   launch window from `emulatorLauncher.log`. (That contrast no longer holds: PS1 was later
   left at its stock database-keyed mode, so serial attribution is the common case for disc
   systems rather than Dreamcast's peculiarity.)
3. **With the option on, the shared file is left alone entirely.** `vmu_save_A1.bin` was not
   touched during the per-game run, so the two shapes do not both receive writes. They do
   share a directory, though: `Dreamcast.VMUPath` is unchanged, so per-game and shared files
   sit side by side and a client listing that directory sees both.
4. **Launching writes the shared VMU with no in-game save**, exactly as PCSX2 does to its
   memory cards. That is the second independent confirmation that **mtime cannot decide
   whether a class-D container needs uploading**.

Ports B, C and D produced no files in either run, so the port-1-only caveat is untested in
the direction that matters: it is not that they stayed shared under the override, it is that
nothing wrote to them at all.

### The class-D conversion options, read from `es_features.cfg`

All four options the plan names exist, and their choice lists are wider than assumed:

| Emulator    | Option                    | Choices                                                 | Best for sync            |
| ----------- | ------------------------- | ------------------------------------------------------- | ------------------------ |
| DuckStation | `duckstation_memcardtype` | `PerGameTitle`, `Shared`, `PerGameFileTitle`, `PerGame` | **`PerGameFileTitle`**   |
| PCSX2       | `pcsx2_slot1_memory`      | `standard`, `folder`, `game`                            | **`game`**               |
| Dolphin     | `dolphin_slotA`           | `8` (GCI folder), `1` (memory card)                     | **`8`**, already default |
| Flycast     | `flycast_vmupergame`      | switch, `switchauto` so unset by default                | **on**                   |

**The plan's DuckStation recommendation should change.** It treats the stock `PerGameTitle`
as good enough. `PerGameFileTitle` is strictly better for a sync client: it names the card
after the **rom file**, which is the key RomMBat already matches on, whereas `PerGameTitle`
uses DuckStation's internal database title, which need not equal the filename. Choosing
`PerGameFileTitle` collapses class D into ordinary class-A handling.

> **Superseded.** This recommendation was withdrawn once a real card was measured rather than
> reasoned about. `PerGameTitle` names the card from `gamedb.yaml`'s `saveName` with the disc
> marker removed, which is what binds a multi-disc set onto one card; keying by rom file splits
> the set and loses the save at the disc change. The conclusion above is right about the
> mechanism and wrong about which side of the trade to take. See
> [freegosy-findings.md](freegosy-findings.md), F18. The rest of this section still holds.

**One hazard found while reading these options:** `dolphin_sync_saves`, described as
"RetroBat will sync dolphin and libretro-dolphin saves folders". **That description is the
emulator's and it is misleading**, which finding 189 settles: it is GameCube only, it runs once
per launch inside emulatorlauncher rather than on a schedule, and it reconciles
`GC/<REGION>/` against a `Card A/` subdirectory of that same folder. Whether it is on has to be
detected, and so does the `Card A` it leaves behind, which outlives the setting.

### Dolphin GCI folder: confirmed, but harder than the plan assumes

The plan says GameCube is "already per-game in a stock RetroBat" via the GCI folder default.
Confirmed, with three complications:

```text
saves/gamecube/dolphin-emu/User/GC/USA/01-GALE-SuperSmashBros0110290334.gci
saves/gamecube/dolphin-emu/User/GC/USA/69-GXBE-game1.ssx.gci
saves/gamecube/dolphin-emu/User/GC/USA/69-GXBE-settings.ssx.gci
saves/gamecube/dolphin-emu/User/GC/USA/5D-GUNE-Gauntlet - Dark Legacy.gci.deleted
```

1. **A region subdirectory sits in the path** (`User/GC/USA/`), which no template in the plan
   accounts for.
2. **One game can produce several `.gci` files** (`69-GXBE-` yields both `game1.ssx` and
   `settings.ssx`). So GameCube is per-game but _multi-file_: class B nested inside the
   per-game story, not the clean 1:1 the plan implies.
3. **Dolphin soft-deletes with a `.gci.deleted` suffix** and leaves the file in place. These
   must be excluded or they will sync as live saves.

The filename format is `<makercode>-<gamecode>-<internal name>.gci`, keyed by game code
(`GALE`, `GXBE`), not ROM filename, so the attribution route the plan describes is still
required.

### PPSSPP writes states to two places, and RetroBat mirrors between them (resolved)

The same PSP game appears under two different naming schemes at once:

```text
saves/psp/ppsspp/3rd Birthday, The (Europe)_0.ppst     <- matches es_savestates.cfg
saves/psp/PPSSPP_STATE/ULES01513_1.00_0.ppst           <- PPSSPP's native location
```

Static evidence could not say which is current, so it was driven live on a real PSP library
(`tools/m0-probes/probe2-psp-states.ps1`, Patapon, a game with no prior state so nothing
existing was touched). **Both are real, and neither is stale: RetroBat keeps them in sync.**

**Sync-out is live, not at exit.** The launcher sets `saves/psp` as PPSSPP's content path,
so PPSSPP writes natively to `PPSSPP_STATE/<GAMEID>_<version>_<slot>.ppst`. Pressing F2 at
`00:34:43.294` produced:

| Time         | File                                            |
| ------------ | ----------------------------------------------- |
| 00:34:43.411 | both `.ppst` copies, byte-identical, same mtime |
| 00:34:43.416 | `ppsspp/<rom filename>.txt`                     |
| 00:34:43.431 | `PPSSPP_STATE/<GAMEID>_1.00_0.jpg`              |

So the mirror is made **about 120 ms after the save, while the emulator is still running**.
Re-checking at exit showed nothing further changed, so there is no exit-time pass to wait for.

**The `.txt` is a name-mapping sidecar.** It holds exactly the native basename:

```text
saves/psp/ppsspp/Patapon (Europe) (En,Fr,De,Es,It).txt   ->   UCES00995_1.00
```

That is how RetroBat translates between the ROM-filename scheme and the game-ID scheme. It
is not a save, but it is **not disposable either**: treat it as part of the state.

**The ES-facing directory is the authoritative one, and it is the one RomMBat must use.**
ES passes the launcher `-state_slot N -state_file <ES-facing path>` (the same pattern appears
in the log for `gba` and `ps2`), and the launcher then hands the emulator the ES-facing path
directly:

```text
PPSSPPWindows64.exe -fullscreen "<rom>" --state="...\saves\psp\ppsspp\Patapon (Europe) (En,Fr,De,Es,It)_0.ppst"
```

Deleting every native file and relaunching that way **recreated the native copy byte for
byte** from the ES-facing one. So a state that RomMBat downloads and writes into
`saves/psp/ppsspp/` does reach the emulator, which is exactly the case that mattered. Note
the sync-in copies **only the `.ppst`**; the native `.jpg` was not recreated.

### The state screenshot is unreliable, and that is a race not a one-off

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

### Every other emulator in `es_savestates.cfg`, driven

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

### The `flycast` declared directory is wrong, and RetroBat contradicts itself

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
[Upstream issues filed](#1336-fixed-in-821-driven-and-the-workaround-is-out) for what
8.2.1 changes and what RomMBat still does. `openmsx` below is unfixed, so the rule this finding
states is unchanged.

### The remaining emulators, downloaded on demand and driven

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

### openMSX: the declared directory is wrong, and it points at the wrong tree

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

### bigpemu launches, but its save state cannot be reached from a keyboard

`bigpemu` installs and runs Rayman correctly. Its save states are driven from BigPEmu's own
overlay menu, and RetroBat's `es_padtokey.cfg` binds only a close hotkey for it. A sweep of
F1 through F8, sent to the focused window, produced **no file of any kind**, and its
`BigPEmuConfig.bigpcfg` contains no save-state key binding (only `System/StateSlot = -1`).
Driving it needs gamepad menu navigation, so its declared template
(`{{system}}/bigpemu/{{romfilename}}_state{{slot2d}}.bigpstate`, with the already-noted
`firstslot="001"`/`lastslot="999"` versus two-digit `{{slot2d}}` inconsistency) stays
**unverified**.

### An overlay can eat an emulator hotkey

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

### BizHawk crashes when launched without `-core`, which is not what it first looked like

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

### Six of the thirteen emulators are not installed, and that is normal

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

### Launching a PS2 game rewrites the shared memory cards on its own

Incidental, but it bears directly on class D. The PCSX2 run touched both shared cards without
the game being asked to save anything:

```text
*changed pcsx2\memcards\Mcd001.ps2   8650752 B
*changed pcsx2\memcards\Mcd002.ps2   8650752 B
```

**So a class-D container's mtime changes merely because a game was launched.** Any sync that
uses modification time to decide whether a shared card needs uploading will upload it after
every session. Content hashing is required, not optional, for class D.

---

## Probe 4: root discovery and app registration (complete)

### There is no `build.ini`

**RetroBat 8.2.0 has no `build.ini` anywhere in the tree.** The version lives in
`system/version.info`, as a single line:

```text
8.2.0-stable-win64
```

This contradicts `docs/PLAN.md` line 331, `DEVELOPER_SETUP.md` section 4, and the
compatibility rule in `CLAUDE.md`, all of which name `build.ini`. The string also carries a
channel and an architecture suffix, so it is not directly parseable as a semantic version;
the comparison logic has to split on `-` first.

### Root markers

Present at the root of a stock install: `retrobat.ini`, `emulationstation/`, `roms/`,
`saves/`, `bios/`, `system/`, `emulators/`, `user/`. The plan's proposed marker set
(`retrobat.ini`, `emulationstation/`, `roms/`) is sound; `build.ini` must be dropped from any
marker list it appears in.

### There is no `batocera-systems.json` either (M5)

`docs/PLAN.md` said the BIOS requirements manifest was "present in the tree". A recursive
search of a real 8.2.0 install finds **no file by that name at all**. The data ships as a
.NET string resource named `batocera_systems` inside
`emulationstation/batocera-systems.exe`, 40,644 bytes at offset 7,250 of a 50,688-byte
executable, and it is `reference/batocera-systems.json` **byte for byte apart from a trailing
newline** (99 systems, 353 entries, no system added, removed or changed).

So the `es_systems.cfg` precedent, read the live copy and treat the vendored one as a
template, cannot apply: there is no live copy to read, and prising a string out of an
executable at runtime would bind RomMBat to one build's resource layout. The manifest is
bundled instead. `tools/m5-probes/m5-probe1-manifest-in-install.py` re-derives all of this.

### `bios/` is a shared tree, and RomMBat owns almost none of it (M5)

Before RomMBat writes anything, `bios/` on a real install holds **4,683 files and 373 MB**,
nearly all of it emulator data rather than firmware:

| Under `bios/`      | Files |
| ------------------ | ----- |
| `dolphin-emu/`     | 2,508 |
| `mame/`            | 858   |
| `nxengine/`        | 436   |
| `Machines/`        | 296   |
| `scummvm/`         | 208   |
| `PPSSPP/`          | 167   |
| `dinothawr/`       | 144   |
| flat at `bios/`    | 6     |
| 15 more subfolders | 60    |

Exactly **3** of those files sit at a path `batocera-systems.json` names carrying the md5 it
names, and **none** sits at a named path with a different md5. `bios/mame/hash` alone holds
776 software-list XML files, already recorded above as metadata rather than firmware, and
openMSX keeps its whole user-data directory here, save states included. Nothing in this tree
is RomMBat's to remove.

### The registry fallback exists, and is exactly as stale as the plan assumes

Checked while building M1, not during M0, and recorded here because the code cites it.
RetroBat does write a registry key:

```text
HKCU\Software\RetroBat
    LatestKnownInstallPath    REG_SZ    K:\RetroBat\
    InstallRootUrl            REG_SZ    http://www.retrobat.ovh/repo/win64
    InstallRootUrlNew         REG_SZ    http://www.retrobat.org/repo/win64
```

`K:` is the letter the probe 7 stick ended on, which is the whole point: the value records
where an install was **last seen**, per Windows user, on one machine. On a portable drive it
is stale the moment the letter changes, and on the second host of probe 7 it would not have
existed at all. So it is usable only as the last-resort fallback the plan already calls for,
after walking up from `AppContext.BaseDirectory`, and the value has to be re-checked against
the root markers before it is trusted.

### The hook path arithmetic in the plan is off by one

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

`PLAN.md` cited `%~dp0..\..\..\` as the way a hook resolves the agent. The _pattern_ is right
and the technique is confirmed working, but a hook invoking an agent at
`<root>/emulators/rommbat/` needs `%~dp0..\..\..\..\emulators\rommbat\`.

**Corrected in the plan during M6**, which is the last place it was still wrong: the M0
experiment 4 description asked to "confirm the `%~dp0..\..\..\` pattern". RetroBat's own
`updatestores.bat` uses three levels because it is calling `emulatorLauncher.exe`, which sits
in `emulationstation/`, and that coincidence is what made the wrong number look confirmed.
RomMBat ships executable hooks that resolve the agent from their own module path, so nothing
depends on the count at runtime.

### The `.menu` format

`system/es_menu/*.menu` is a plain text file, not XML. Line 1 is the executable, subsequent
lines are arguments:

```text
\retroarch\retroarch.exe
```

```text
\pico8\pico8.exe
-home .\..\..\emulators\pico8 -root_path .\..\..\roms\pico8 -desktop .\..\..\screenshots\pico8
```

### Measured: `.menu` paths are rooted at `emulators\` and cannot escape upward

Three variants were installed differing only in how the executable was addressed, each
passing its own letter as an argument and writing a self-identifying marker
(`tools/m0-probes/probe4-menu-paths.ps1`). All three were launched from the ES menu.

|     | Executable line                              | Result                                                     |
| --- | -------------------------------------------- | ---------------------------------------------------------- |
| A   | `..\..\plugins\rommbat\zz-probe.bat`         | **rejected**: `[Generator] Failed. path is null`, exit 204 |
| B   | `\plugins\rommbat\zz-probe.bat`              | **rejected**: `[Generator] Failed. path is null`, exit 204 |
| C   | `\rommbat\zz-probe.bat` (under `emulators\`) | **launched**                                               |

The marker C wrote:

```text
variant=C
target=G:\RetroBat\emulators\rommbat\zz-probe.bat
cwd=G:\RetroBat\emulators\rommbat
allargs=C
```

**This settles the layout question, and it refutes the plan's working assumption.**

1. **The executable path is resolved under `emulators\`, and `..\` escapes are refused.**
   `emulatorLauncher`'s generator validates the path and returns "path is null" rather than
   resolving upward. A leading backslash is not root-relative either.
2. **So RomMBat cannot live at `<root>/plugins/rommbat/` and still have an ES menu entry.**
   It must install under **`<root>/emulators/rommbat/`**. This contradicts core principle 4
   and the layout in `DEVELOPER_SETUP.md` section 6, both of which name `plugins/rommbat/`.
3. **The working directory is the executable's own directory**, not the RetroBat root, so
   the app must not assume its CWD and should resolve everything from
   `AppContext.BaseDirectory` as the plan already requires.
4. **`.bat` targets are accepted**, not only `.exe`, and the argument line reaches the
   target intact.

Note this constrains only the **menu-launched** component. Nothing stops the agent, the
database and the outbox living elsewhere in the tree; but since everything must live in one
place for the portable story to stay simple, `emulators/rommbat/` is now the natural home
for all of it.

### `es_menu` is an ordinary ES system, and registration takes two files

This is not a bespoke mechanism. `es_systems.cfg` declares it like any other system:

```xml
<path>~\..\system\es_menu</path>
<extension>.menu</extension>
<command>"%HOME%\emulatorLauncher.exe" -system retrobat -rom %ROM%</command>
```

So a `.menu` file is simply a **ROM of the `retrobat` system**, and the thing that parses it
and resolves the executable path is **`emulatorLauncher`, not EmulationStation**. Two
consequences the plan does not account for:

1. **A minimum viable entry is two files, not one.** The `.menu` supplies the command; the
   display name, description and artwork come from a `<game>` element in
   `system/es_menu/gamelist.xml`, whose `<path>` points at the `.menu` (`./retroarch.menu`).
   A `.menu` with no gamelist entry appears as a bare filename.
2. **`es_menu/gamelist.xml` is subject to the same ES-rewrites-on-exit hazard as every other
   gamelist**, so registration has to merge rather than clobber, exactly like M4.

Paths inside that gamelist are relative (`./altirra.menu`, `./media/altirra-logo.png`), so
the format itself does not force an absolute path anywhere.

---

## Probe 3: library refresh (complete)

### EmulationStation runs an HTTP API, and it is the refresh mechanism

The plan looks for the answer in the `update-gamelists` hook and `-updatestores`. Neither is
it. **ES embeds cpp-httplib and serves an API on `127.0.0.1:1234`**, self-documented by the
HTML page it returns at `/`. Confirmed live against a running ES
(`tools/m0-probes/probe3-refresh.py`):

| Route                     | Method  | Returns                                             | Use                                                                       |
| ------------------------- | ------- | --------------------------------------------------- | ------------------------------------------------------------------------- |
| `/reloadgames`            | **GET** | 200, empty                                          | **Rescan roms and re-read gamelists, no restart**                         |
| `/systems`                | GET     | JSON array                                          | name, fullname, hardwareType, manufacturer, theme, extensions, totalGames |
| `/systems/<system>/games` | GET     | JSON array                                          | name, desc, image, per game                                               |
| `/caps`                   | GET     | `{"Version":"8.2.0-stable-win64","SortName":false}` | a second version source                                                   |
| `/quit`                   | GET     | -                                                   | close ES cleanly                                                          |
| `/emukill`                | GET     | -                                                   | kill the running emulator                                                 |
| `/launch`                 | POST    | -                                                   | launch a game, body is the game                                           |

`POST /reloadgames` returns 404; the verb is GET only. `/games` and `/gamelists` are 404 at
the top level. `POST /launch` takes the game path as the **raw request body**, in the
forward-slash form `/systems/<system>/games` reports (`K:/RetroBat/roms/ports/2048.libretro`),
confirmed from the API's own page at `/`.

**`/quit` and `/emukill` are both ignored while a game is running**, found while driving the
probe 2 launches. With RetroArch up, `GET /emukill` returned cleanly and killed nothing, and
`GET /quit` returned cleanly and left ES running; `GET /caps` still answered 200 throughout,
so ES was alive and serving, just not acting. Closing RetroArch by other means and then
re-issuing `/quit` worked immediately. So **a 200 from this API is not evidence the action
happened**, and any code that shuts ES down before touching `es_settings.cfg` has to poll
for the process actually exiting rather than trust the response.

**`/reloadgames` is in the same state, measured during M4 (finding 107).** With RetroArch up
it answered 200 in 1 ms and a ROM added to the folder was still unreported five seconds
later. The one route M4 depends on is therefore not exempt: a sync that writes a gamelist
while a game is running has to reload again afterwards rather than treat the 200 as done.

**It works on loopback with `PublicWebAccess` absent from `es_settings.cfg`**, that is at
ES's default. The binary also carries the string `HttpServerThread : Access disabled for`, and the UI
exposes "ENABLE PUBLIC WEB API ACCESS" under FRONTEND DEVELOPER OPTIONS showing
`http://<addr>:1234`. Read together: the server always runs, and the setting gates
**non-local** callers only. **So RomMBat can refresh ES without asking the user to change any
setting**, which is a much better position than the plan assumed.

### `/reloadgames` really reloads, proven against a control

A new rom file dropped into `roms/ports/` while ES was running appeared in
`/systems/ports/games` after a `GET /reloadgames`, with no restart. On its own that is
ambiguous, because the endpoint might simply scan the directory per request. A rename test
settles it:

| Step                                                    | ES reports          |
| ------------------------------------------------------- | ------------------- |
| `gamelist.xml` on disk edited: `Gong` → `Gong RELOADED` | still **`Gong`**    |
| after `GET /reloadgames`                                | **`Gong RELOADED`** |

ES held a **stale in-memory model** across the disk change and only picked it up when asked.
So `/systems/<system>/games` reflects ES's model, not the filesystem, and `/reloadgames`
genuinely re-reads both the rom directory and `gamelist.xml`.

### Writing `gamelist.xml` under a running ES is safe, but only if you reload

ES **does** rewrite `gamelist.xml` on exit: mtime landed at 23:48:31, exactly when ES closed.
But the concurrent edits survived, both the renamed entry and a raw XML comment stamp.
Comparing the file before and after that rewrite:

- **XML comments survive.** ES is not regenerating the document from its model; it loads,
  modifies and saves, so unknown nodes are preserved.

  > **Half of this is withdrawn. See finding 103.** Unknown elements and attributes do
  > survive, `<scrap/>` in its self-closing form included. **Comments do not**: an ES rewrite
  > drops every one, at document level and inside a `<game>` alike. What is preserved is the
  > node tree its parser keeps, and comments are not in it.

- **`<path>` element order is unchanged.**

  > **True only for entries ES did not touch. See finding 105.** The played entry's children
  > were rewritten into ES's own order, the entry moved to the end of the file, and
  > `<hidden>false</hidden>` was pruned as a default.

- **No `<game>` entry was written for the metadata-less probe rom**, even though ES listed it
  in the API. ES only persists entries it has metadata for.

**The reason the write survived is the actionable part, and it is a sequencing rule:** the
edit was made _and then_ `/reloadgames` was called, so ES had it in memory before serialising
on exit. ES demonstrably holds a stale model otherwise, and demonstrably rewrites the file at
exit, so an edit made **without** a following reload would be overwritten by that stale model.

> **Rule for M4: write the gamelist, then immediately `GET /reloadgames`.** That converts the
> plan's "write only while ES is idle" constraint into a much cheaper "write then reload".
> The negative case, editing without reloading and then quitting, was not directly executed;
> it is inferred from the stale-model and rewrite-on-exit measurements, both of which were.
>
> **Two M4 measurements bound the rule.** ES rewrites the file only when it has something to
> change, so a session that touched nothing leaves it byte-identical (finding 104); and the
> reload is ignored outright while a game is running (finding 107), which is precisely when a
> background sync is most likely to be writing.

### What the plan was looking for, and why it is not there

- **`-updatestores` has nothing to do with gamelists.** It drives `batocera-store.exe`, the
  content downloader. `emulatorLauncher`'s complete update-ish switch set is `-updatestores`,
  `-updateall`, `-updatepo`, `-collectversions`. There is no gamelist switch. The confusion
  is understandable: `updatestores.bat` ships in **both** the `start` and `update-gamelists`
  event folders, which makes the two look related.
- **ES's own CLI cannot refresh a running instance.** Its switches are startup-only:
  `--gamelist-only`, `--ignore-gamelist`, `--home`, `--force-kiosk`, `--windowed` and so on.

### What a reload costs, now that probe 5 has measured it

`GET /reloadgames` answers in **1-2 ms** and does the work afterwards, so its response time
is not a completion signal. Timing the effect instead, by changing the library on disk and
polling until ES reports it: **269 ms** for a 200-entry system and **1084 ms** for 100,000.
Poll `/systems`, which is a few KB and carries `totalGames`, not
`/systems/<system>/games`, which serialises the whole library (99 MB at 100k entries) and
loads ES down enough to distort what is being measured.

## Superseded detail: the mtime evidence that first suggested this

Not a designed probe run, just file mtimes read after two ES sessions. ES quit at 20:12:16
and 23:06:14; the mtimes line up exactly:

| File                                                     | mtime after the session | Rewritten by ES?                                                     |
| -------------------------------------------------------- | ----------------------- | -------------------------------------------------------------------- |
| `.emulationstation/es_settings.cfg`                      | **23:06:14**            | **yes**, on exit                                                     |
| `roms/ports/gamelist.xml`                                | **23:06:14**            | **yes**, on exit (1412 → 1592 bytes, playcount and lastplayed added) |
| `system/es_menu/gamelist.xml`                            | 19:49:14 (my write)     | **no**, untouched across two sessions                                |
| `es_systems.cfg`, `es_savestates.cfg`, `es_features.cfg` | 19:28:40 (install time) | no                                                                   |

Two consequences:

1. **The plan's clobber hazard is real for both files it names**, though probe 2 later
   narrowed the `es_settings.cfg` half: ES rewrites that file **only when a setting changed
   during the session**, and when it does rewrite it, it preserves keys it does not
   recognise. Both sessions in this table involved an operator navigating the UI, which is
   what dirtied it. Merge, write while ES is idle, write atomically, but expect the file to
   be left alone far more often than this table implies.
2. **`system/es_menu/gamelist.xml` was _not_ rewritten**, across two sessions that included
   launching entries from it. So registering RomMBat in the ES menu appears to be a
   write-once operation rather than a merge-on-every-exit fight. Good news, but it is an
   absence of evidence from two sessions rather than a guarantee; keep the merge logic.

This mtime evidence is what prompted the live probe above, which supersedes it.

## Probe 7: the move (done, and it half failed)

The stick travelled **G: → D: (a second PC, a different Windows user) → K:**, so both the
drive-letter change and the different-host requirement were exercised.

### What survived

| Criterion                                       | Result                                                                                                                                                      |
| ----------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Root discovery                                  | **pass**. Hooks resolved `%~dp0..\..\..\..` to `K:\RetroBat` after the letter change, and ES passed `K:\RetroBat\roms\...` paths                            |
| RetroBat launching a game on the second machine | **pass**. `emulatorLauncher.log` on the stick records `"D:\RetroBat\...\emulatorLauncher.exe" ... -rom "D:\RetroBat\roms\ports\gong.libretro"`, exit code 0 |
| Writes to the stick from the second machine     | **pass**. ES rewrote `roms/ports/gamelist.xml` at 23:18:38 on exit there                                                                                    |
| Drive-letter change on the same machine         | **pass**. Everything worked on K: exactly as on G:                                                                                                          |
| RetroBat storing absolute paths                 | **pass**. See the pre-move audit below                                                                                                                      |

The second machine ran under a different Windows user profile, confirming the install is not
bound to a Windows account.

### What did not: no hook produced anything on the second machine

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

### The FAT32 and exFAT constraints, measured on a second stick

The RetroBat stick is NTFS, so both filesystem constraints were measured separately on a
14.6 GB USB stick formatted first FAT32, then exFAT, then FAT32 again, with the NVMe system
drive as an NTFS control (`tools/m0-probes/probe7-filesystem.ps1`).

#### The 4 GB ceiling fails as "not enough space on the disk"

Writing 4 GB + 64 MB to FAT32 in 8 MB chunks:

| Result           | Value                                                                  |
| ---------------- | ---------------------------------------------------------------------- |
| Stopped at       | **4,286,578,688 bytes** = 4 GiB - 8 MiB, the last chunk that fit whole |
| Exception        | `System.IO.IOException`                                                |
| HRESULT          | `0x80070070`, Win32 **112 `ERROR_DISK_FULL`**                          |
| Message          | **"There is not enough space on the disk."**                           |
| Free at the time | **14.63 GB**                                                           |
| Throughput       | 6.5 MB/s, so the 4 GB write itself took 11 minutes                     |

**The error message is actively misleading and RomMBat must not surface it.** The volume had
14.6 GB free; the file simply could not exceed 4 GiB. A client that reports the OS message
tells the user to free up space, which will not help, and a client that retries after
clearing room retries forever. A seek to `4GiB - 4` followed by an 8-byte write fails the
same way and leaves a **0-byte** file, so the failure is a hard boundary rather than a
partial write.

The 4 GiB - 8 MiB stopping point is an artifact of the chunk size, not a filesystem
boundary: the write that would have crossed 4 GiB failed whole. The real limit is 4 GiB - 1.

**So the pre-flight check is mandatory, exactly as core principle 4 says.** `fs_size_bytes`
from the rom record, compared against a FAT32 target before the download starts, is the only
place this can be caught cheaply. Detection is easy and reliable:
`DriveInfo.DriveFormat` returned `FAT32` and `exFAT` correctly on the removable volume, and
`Get-Volume` agreed.

#### mtime: exFAT is no better than FAT32, and both round up

This is the result that changes the design, because the plan assumes exFAT is the finer of
the two. It is not, at least not through Windows' driver:

| Filesystem | Requested +1 ms     | Requested +1999 ms | Granularity | Direction |
| ---------- | ------------------- | ------------------ | ----------- | --------- |
| FAT32      | stored **+2000 ms** | stored +2000 ms    | **2 s**     | **up**    |
| **exFAT**  | stored **+2000 ms** | stored +2000 ms    | **2 s**     | **up**    |
| NTFS       | stored +0 ms        | stored +0 ms       | exact       | -         |

exFAT's on-disk format has a 10 ms increment field, so a finer value is representable; it is
simply not what this Windows build stores. Treat **exFAT and FAT32 as identical** for
timestamp purposes.

**And the rounding is up, not to nearest, which puts timestamps in the future.** Natural
writes, timed against the wall clock rather than stamped explicitly:

```text
exFAT   wrote 08:03:16.097 -> stored 08:03:18.000   skew +1903 ms
        wrote 08:03:16.448 -> stored 08:03:18.000   skew +1552 ms
        wrote 08:03:16.804 -> stored 08:03:18.000   skew +1196 ms
        wrote 08:03:17.144 -> stored 08:03:18.000   skew  +855 ms
        wrote 08:03:17.485 -> stored 08:03:18.000   skew  +514 ms
        wrote 08:03:17.840 -> stored 08:03:18.000   skew  +159 ms
NTFS    wrote 08:03:19.418 -> stored 08:03:19.418   skew    +0 ms
```

FAT32 behaved identically after the volume was formatted back. Two consequences, and the
second one is not in the plan:

1. **Six files written across 1.7 seconds carry one identical mtime.** Ordering within a
   2-second window is not recoverable, so mtime cannot break ties between saves written in
   the same moment, which is exactly what a multi-file (class B) save looks like.
2. **A file's recorded mtime can be up to 2 seconds in the future.** Core principle 1's
   clock-skew handling compares local timestamps against the server's `Date` header; on a
   FAT volume a freshly written save legitimately reads as newer than the clock that wrote
   it. **Any "this timestamp is in the future, suspect a bad RTC" check needs a tolerance of
   at least 2 seconds**, or every FAT install trips it.

#### The FAT local-time trap did not appear

FAT stores local wall-clock time rather than UTC, which historically shifts timestamps by an
hour across a DST boundary. Stamping a winter date and a summer date and reading both back:

| Stamped             | Stored local        | Stored UTC          | Offset |
| ------------------- | ------------------- | ------------------- | ------ |
| 2026-01-15 12:00:00 | 2026-01-15 12:00:00 | 2026-01-15 17:00:00 | -5 h   |
| 2026-07-15 12:00:00 | 2026-07-15 12:00:00 | 2026-07-15 16:00:00 | -4 h   |

Local time round-tripped exactly in both, and the UTC conversion used the offset in force on
**that** date rather than today's. NTFS produced identical values. So this Windows build
applies per-timestamp DST rules to FAT, and the hour-shift hazard is not live here. Worth
re-checking on a machine in a different timezone before it is called settled.

## Probe 7b: why a hook does not run, and the one form that does

Probe 7 left the second host's failure undiagnosed, and probe 1 left the `game-start`
failure unexplained. Both were blocked on the same thing: **nothing in the tree records what
ES tried to do**. This probe fixes that, and the instrument turned out to answer probe 1 on
the way (`tools/m0-probes/probe7b-hook-diagnose.ps1`, `probe7c-exe-hook.ps1`,
`probe7b-collect.ps1`).

### Most of the evidence does not survive the next launch

Checked against the stick a day later: `es_log.txt` rotates through `es_log.0.txt` to
`es_log.3.txt` on **every ES start**, and **`RetroBat.log` at the root is overwritten
outright**. The second host's session was gone from both, which is why probe 7 could not be
diagnosed after the fact. Collect before RetroBat starts again, anywhere.

**`emulatorLauncher.log` is the exception, and it earns its place in the design.** It rotates
by size rather than per launch, so at 268 KB per 5 weeks it still held **both** second-host
sessions, five weeks apart, alongside everything from the first host. That is the durability
the M6 journal needs and neither ES log has.

### ES logs its scripting decisions, but only on a log level nothing sets

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

### The failure is per interpreter, and `.exe` is the only form that survives a real name

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

### Both mechanisms, reproduced outside EmulationStation

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

### The second host, resolved: it cannot launch a script, only an executable

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

### Both causes, named, and neither is security software

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

### Byproducts

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

## Probe 7: absolute-path audit, taken before the move

Taken before the move, answering the plan's "note whether RetroBat itself stores any
absolute paths that would constrain us" (`tools/m0-probes/probe7-portable.py`).

**RetroBat's live configuration is genuinely portable.** Of **5,636** config files scanned
across the tree (`.cfg`, `.ini`, `.xml`, `.menu`, `.json`, `.bat`, `.info`), only **9**
contain an absolute path, and **not one of them is a file RetroBat reads as live config**:

| File                                                            | What the absolute path is                                                                                                        |
| --------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `bios/mame/hash/ibm5170.xml`, `ibm5170_hdd.xml`, `pico.xml`     | MAME software-list metadata _describing_ historical DOS media (`C:\Windows\system32\diskcopy.dll`), not a path anything resolves |
| `system/templates/gemrb/GemRB.cfg`                              | `H:\GemRB\plugins`, `C:/gemrb-win32-ef32a9a`                                                                                     |
| `system/templates/project64/Config/Project64.cfg`               | `F:\RetroBat-Wip\roms\n64\Mario`                                                                                                 |
| `system/templates/simple64/mupen64plus.cfg`, `simple64-gui.ini` | `C:/retrobat/saves/n64/...`, `C:/retrobat/roms/n64`                                                                              |
| `system/templates/oricutron/oricutron.cfg`                      | `d:/osdk/my`                                                                                                                     |
| `system/templates/pcsx2-16/inis/GSdx.ini`                       | `C:\Windows\Fonts\tahoma.ttf`                                                                                                    |

Every one of the `system/templates/` hits is a **stale developer path baked into a shipped
template**, `F:\RetroBat-Wip\` being the clearest tell. These are the files
`emulatorlauncher` copies and rewrites per launch, which is exactly why rule 2 says never to
edit a generated emulator config: they are treated as disposable, and RetroBat's own authors
evidently treat them that way too.

Confirmed clean, with **zero** absolute paths: `retrobat.ini`, `es_systems.cfg`,
`es_settings.cfg`, `es_savestates.cfg`, `es_features.cfg`, every `gamelist.xml`, and every
`.menu`. `es_systems.cfg` uses `~\..\roms\<system>` and `%HOME%\emulatorLauncher.exe`.

## Other observations

- The live `es_systems.cfg` **differs** from the copy vendored in `reference/`, as expected
  for a per-install generated file. It declares **244 systems and 1176 distinct file
  extensions**, and every single system has a non-empty `<extension>`. The vendored upstream
  copy declares **240**, so a live install carries four systems upstream does not. That gap
  is exactly why rule 3 exists: read extensions from the live file, never from a bundled
  table. `reference/verify.py` continues to assert 240 against the vendored file, which is
  correct and should not be changed to match the live number.
- `es_systems.cfg` uses `~\..\roms\<system>` for `<path>` and `%HOME%\emulatorLauncher.exe`
  in `<command>`, so RetroBat itself already avoids absolute paths in its primary config.
  That is a good sign for probe 7 but is not yet a full audit.
- The `game-start`, `game-end`, `quit`, `reboot`, `shutdown`, `sleep` and `wake` hook
  directories all exist and are **empty** in a stock install. Only `start/` and
  `update-gamelists/` ship a script, both `updatestores.bat`. Installing hooks is therefore
  a pure addition in the common case, but the append-don't-replace rule still matters for
  those two events.

---

## Contradictions with `docs/PLAN.md`

Recorded here as required; the plan is amended in the same change.

| #   | Plan says                                                                                             | Measurement says                                                                                                                                                                                                                                                                                                                          |
| --- | ----------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Read the RetroBat version from `build.ini` (L331)                                                     | No `build.ini` exists. It is `system/version.info`, value `8.2.0-stable-win64`                                                                                                                                                                                                                                                            |
| 2   | Hooks resolve the agent through `%~dp0..\..\..\` (L148-150, L375, L750)                               | Three levels reaches `emulationstation/`; the root needs four. **Fully corrected in the plan during M6**; the M0 experiment 4 text was the last place still carrying three                                                                                                                                                                |
| 3   | `es_savestates.cfg` "yields the slot bounds" (L802-806)                                               | `libretro`, the most important entry, declares no slot bounds at all                                                                                                                                                                                                                                                                      |
| 4   | The `libretro` directory is core-scoped (L811)                                                        | True, and `bizhawk` is core-scoped too                                                                                                                                                                                                                                                                                                    |
| 5   | `<image>` maps directly onto `screenshotFile` (L804-805)                                              | True except `desmume`, where `<image>` and `<file>` are the same template                                                                                                                                                                                                                                                                 |
| 6   | Class-D list is PCSX2 and Dreamcast VMU (L822)                                                        | Add `megacd`'s shared `4Mbit_cart.brm` and `xbox`'s `eeprom.bin` + `xbox_hdd.qcow2`                                                                                                                                                                                                                                                       |
| 7   | PS1 is already per-game via DuckStation `PerGameTitle` (L831, L835)                                   | Only when DuckStation is the selected emulator. This install runs libretro for `psx` and produces plain `.srm`                                                                                                                                                                                                                            |
| 8   | GameCube GCI folder gives "individual `.gci` files" per game (L832)                                   | True, but with a region subdirectory, several files per game, and `.gci.deleted` litter to exclude                                                                                                                                                                                                                                        |
| 9   | Dreamcast VMU handling is unverified (L870)                                                           | Characterised: four port-keyed files shared by all games, **but `flycast_vmupergame` converts them**, for port 1 only                                                                                                                                                                                                                     |
| 9b  | DuckStation `PerGameTitle` is treated as sufficient (L831)                                            | `PerGameFileTitle` is the better value: it keys the card by **rom filename** rather than by DuckStation's internal title. **Later withdrawn**: a driven card showed `PerGameTitle` binds a multi-disc set and the filename key splits it, so the plan's original value was correct. See [freegosy-findings.md](freegosy-findings.md), F18 |
| 9c  | (not addressed)                                                                                       | `dolphin_sync_saves` has RetroBat copying saves between the dolphin and libretro-dolphin folders on its own; it must be detected before trusting either. **Superseded by finding 189**: GameCube only, once per launch, and between one region folder and its own `Card A/`                                                               |
| 10  | Save directories are modelled per system (L815, M6 generally)                                         | The tree is `saves/<system>/<emulator>/`, plus emulator-named folders at the top level                                                                                                                                                                                                                                                    |
| 11  | The unreachable-host timeout "becomes the budget" for UI checks (L383)                                | That timeout is 21s and cannot be a UI budget. `ConnectTimeout` must be set explicitly; 2s recommended                                                                                                                                                                                                                                    |
| 12  | "the three sidecar flags" on `GET /api/roms` (L377-378)                                               | There are **four** default-on flags, plus `with_files` as a fifth opt-in                                                                                                                                                                                                                                                                  |
| 13  | Sidecar cost is framed as a per-page latency question (L377-380)                                      | It is a flat ~841 KB resent on every page, and costs bandwidth not server time. Fetch once, then disable                                                                                                                                                                                                                                  |
| 14  | `GET /api/collections` response size is an open question (L378-379)                                   | 715 KB for a **single** collection, 99% of it inlined cover-art paths, with no pagination available                                                                                                                                                                                                                                       |
| 15  | The ES menu entry is "a `system/es_menu/*.menu` entry" (L374, L237)                                   | Registration needs **two** files: the `.menu` plus a `<game>` element in `es_menu/gamelist.xml`. `.menu` files are roms of a `retrobat` system parsed by `emulatorLauncher`, not by ES                                                                                                                                                    |
| 16  | Everything lands under `RetroBat/plugins/rommbat/` (L139, `DEVELOPER_SETUP.md` §6)                    | **Refuted.** `.menu` executable paths resolve under `emulators\` and reject `..\` escapes, so a menu-launched app must live at `<root>/emulators/rommbat/`                                                                                                                                                                                |
| 17  | Hooks may block game launch; M6 takes its budget from M0 (L355, L749-751)                             | **Hooks do not block.** The launcher started 30 ms after the hook, three times out of three, while the hook slept 8 s                                                                                                                                                                                                                     |
| 18  | Batocera `game-start` args: `$1` rom, `$2` basename, `$3` system, `$4` emulator, `$5` core (L350-352) | RetroBat passes **three**. `$4` and `$5` are empty and `$3` is not the system. Emulator and core are withheld from the hook                                                                                                                                                                                                               |
| 19  | Slot derives as `{emulator}:{core}:{slot}`, recorded per state (L806, L809-810)                       | The hook cannot see emulator or core, so neither can come from the hook path. Another source is required                                                                                                                                                                                                                                  |
| 20  | `game-end` closes the record its `game-start` opened (L748-749)                                       | `game-end` also fires with **no** preceding `game-start`, including for ES-menu launches and for launches that failed. The agent must tolerate an orphan `game-end`, and RomMBat's own exit produces one                                                                                                                                  |
| 21  | Hooks are journal-only to avoid blocking the launch path (L748-751)                                   | Right conclusion, wrong reason. They do not block, but they **run concurrently**, so the lock file is mandatory and the journal must survive interleaved appends from separate processes                                                                                                                                                  |
| 22  | `game-start` opens the journal record that `game-end` closes (L748-749)                               | **`game-start` never fires for a game whose gamelist `<name>` contains a space**, confirmed by crossover. That is nearly every real rom, so the journal cannot be built on it. Use `emulatorLauncher.log`                                                                                                                                 |
| 25  | `$3` is the system (L350-352, Batocera convention)                                                    | `$3` is the **gamelist display name**. The system is not passed to the hook at all. `$2` is the rom basename                                                                                                                                                                                                                              |
| 26  | Everything the hooks need is available to the hooks (M6 generally)                                    | `emulationstation/emulatorLauncher.log` is the only in-tree source carrying rom, system, emulator, core and a millisecond timestamp together. 268 KB per 5 weeks / 70 launches, two-file rotation                                                                                                                                         |
| 27  | A portable install works on any machine it is plugged into (core principle 4)                         | The tree does, and so do the events, but **the second host cannot launch a `.bat` or a `.ps1`, only an `.exe`**. Every hook was a `.bat` then, hence total silence. RomMBat must still detect the state                                                                                                                                   |
| 28  | Library refresh lives in `update-gamelists` and `-updatestores` (L402-405)                            | Neither. `-updatestores` drives the content store, not gamelists, and ES's CLI is startup-only. **ES serves an HTTP API on `127.0.0.1:1234`; `GET /reloadgames` is the mechanism**                                                                                                                                                        |
| 29  | Writing `gamelist.xml` while ES runs may be clobbered on exit (L403-404, M4)                          | Only if you do not reload. ES holds a stale model and rewrites from it at exit, so **write then `GET /reloadgames`** and the edit sticks and shows immediately. ES merges in place, preserving comments                                                                                                                                   |
| 30  | (not addressed)                                                                                       | The ES API also offers `/caps` as a second version source, `/quit` to close ES cleanly before touching `es_settings.cfg`, and `/systems/<system>/games` to read ES's own view of the library                                                                                                                                              |
| 23  | ES may overwrite `gamelist.xml` on exit (M4, L403-404)                                                | Confirmed by mtime for `roms/<system>/gamelist.xml` **and** `es_settings.cfg`. But `system/es_menu/gamelist.xml` was **not** rewritten across two sessions, so menu registration is a gentler case                                                                                                                                        |
| 24  | Portable installs may be FAT32 or exFAT, with a 4 GB ceiling and coarse mtimes (L170-180)             | Sound, but **untested**: the stick under test is NTFS, so neither constraint was exercised. Needs a differently formatted volume                                                                                                                                                                                                          |
| 31  | `<system>["<rom>"].<key>` is a per-game override, granularity unverified (L1106-1112)                 | **Confirmed live.** It is honoured by `emulatorlauncher`, outranks the system key, and stays scoped to the one rom                                                                                                                                                                                                                        |
| 32  | (not addressed) the rom key is written as `<rom filename>`                                            | The filename must carry its **extension**. `ports["gong"]` is ignored where `ports["gong.libretro"]` takes effect, on the same rom. Build the key from `fs_name`, and note the failure is silent                                                                                                                                          |
| 33  | ES rewrites `es_settings.cfg` on exit, so a write can be clobbered (L1118-1119)                       | Only when a setting **changed** that session. Start-and-quit, and even a session that launched a game, left the file untouched. Amends finding 23 in this table                                                                                                                                                                           |
| 34  | (not addressed) whether ES preserves keys it does not model                                           | It does. A deliberately nonsense per-game key survived a real ES rewrite intact. But ES **prunes any setting equal to its own default** (`Language=en_US` vanished, `fr_FR` survived)                                                                                                                                                     |
| 35  | `/quit` closes ES cleanly (probe 3, `retrobat-layout` skill)                                          | Not while a game is running. `/quit` and `/emukill` both return cleanly and do nothing until the emulator exits, so a 200 is not evidence the action happened. Poll for the process                                                                                                                                                       |
| 36  | PPSSPP's two populated state directories mean the declared template is wrong (probe 2, earlier)       | **Neither is stale.** RetroBat mirrors native to ES-facing about 120 ms after each save, live. The declared template is correct and `saves/psp/ppsspp/` is authoritative                                                                                                                                                                  |
| 37  | (not addressed) how a downloaded state reaches the emulator                                           | ES passes `-state_slot` and `-state_file` naming the **ES-facing** path, and the launcher hands it to PPSSPP as `--state=`. Writing there is sufficient; the native copy is rebuilt from it                                                                                                                                               |
| 38  | `<image>` maps onto `screenshotFile` (L804-805)                                                       | For PPSSPP the mirrored screenshot is **racy**: observed correct, zero-byte and entirely absent across three saves, because the watcher copies before the emulator has written it. Treat as best-effort                                                                                                                                   |
| 39  | (not addressed) the `.txt` beside a PPSSPP state                                                      | It is the **name mapping** to the native scheme (`UCES00995_1.00`), not a save and not disposable. Sync it with the state. Present for exactly those emulators whose native naming differs from the rom                                                                                                                                   |
| 40  | `es_savestates.cfg` is the authority on state paths (`retrobat-layout`, L802-806)                     | True for `<file>`, which held for all 7 driven emulators, but **not for `<directory>`**: `flycast` writes `reicast/states` while the file declares `flycast/sstates`, and the declared dir sits empty                                                                                                                                     |
| 41  | (not addressed) whether a declared emulator is installed                                              | Six of the 13 have **no executable**, only a config stub; RetroBat downloads emulators on demand. A declaration is not evidence the emulator exists, so check for the binary before promising state sync                                                                                                                                  |
| 42  | `<image>` is a normal optional field (L804-805)                                                       | It is **absent more often than present**: missing in 4 of 7 driven emulators, and correct/zero-byte/missing across three runs of the same PPSSPP game. Best-effort everywhere                                                                                                                                                             |
| 43  | (not addressed) detecting changes to a class-D shared container                                       | Launching a PS2 game rewrote both `Mcd001.ps2` and `Mcd002.ps2` with no in-game save, so **mtime is useless for class D** and content hashing is mandatory                                                                                                                                                                                |
| 44  | A 100k-entry gamelist "would make EmulationStation unusable" (core principle 2, L104)                 | **Not supported.** ES loads 100,000 entries in **2.07 s** for 419 MB, and 2.93 s with artwork on disk. Cap the gamelist for gamepad navigability, not because ES cannot take it                                                                                                                                                           |
| 45  | (not addressed) what `GET /reloadgames` costs after M4 writes a gamelist                              | It answers in 1-2 ms and reloads afterwards, so its response time measures nothing. Time to the change being visible is **269 ms at 200 entries, 1084 ms at 100,000**                                                                                                                                                                     |
| 46  | exFAT is listed with FAT32 but FAT32's 2 s granularity is the one quoted (L188-190)                   | **exFAT is identical: 2 s.** Its format allows 10 ms, Windows does not use it. Treat the two as the same for timestamps                                                                                                                                                                                                                   |
| 47  | mtime is coarse, so treat it as an ordering tiebreak (L188-190)                                       | Coarse **and rounded up**, so a file's mtime lands up to 2 s in the **future**. A "timestamp ahead of the clock" skew check needs a 2 s tolerance or every FAT install trips it                                                                                                                                                           |
| 48  | FAT32 cannot hold a file larger than 4 GB, so skip or refuse (L184-186)                               | Confirmed, and the failure is **`ERROR_DISK_FULL`, "There is not enough space on the disk"**, on a volume with 14.6 GB free. Never surface that message; pre-flight against `fs_size_bytes`                                                                                                                                               |
| 49  | Dreamcast converts to per-game via `flycast_vmupergame` (finding 9 above)                             | Confirmed live, but the per-game VMU is named for the **disc serial** (`T40217N_vmu_save_A1.bin`), not the rom file, so it does not collapse into class A the way DuckStation's `PerGameFileTitle` does. The comparison is now moot: PS1 stays on its stock database-keyed mode                                                           |
| 50  | (not addressed) how an uninstalled emulator gets installed                                            | A **modal dialog with no title and no timeout** blocks the launch until answered. Three launchers were found still waiting on it seven hours later                                                                                                                                                                                        |
| 51  | The `.txt` sidecar appears exactly where native naming differs from the rom filename (finding 39)     | **Retracted.** `jgenesis` and `desmume` both wrote one containing the rom filename itself, so it is written unconditionally. Its content is still the mapping and still has to travel with the state                                                                                                                                      |
| 52  | (not addressed) whether a declared emulator can be launched once installed                            | `bizhawk` installs and then crashes in `CreateControllerConfiguration` unless the launcher is given **`-core`** (`inputPortNb[core]` is unguarded). ES always passes one; direct invocation does not                                                                                                                                      |
| 53  | `bizhawk`'s directory is core-scoped like `libretro`'s (finding 4 above)                              | Correct, and the mirror is what makes it so. **Natively** BizHawk writes to `emulators/bizhawk/sstates/<system>/`, outside `saves/` and **not** core-scoped; RetroBat mirrors that to the declared path                                                                                                                                   |
| 54  | (not addressed) BizHawk's hotkeys                                                                     | RetroBat rebinds them: `Save State 1` is **Ctrl+F1** and `Shift+F1` is **Load**, so BizHawk's usual save key silently loads. Only `Quick Save` = `F2` was observed to write                                                                                                                                                               |
| 55  | (not addressed) whether everything beside a state round-trips                                         | No. BizHawk writes a `.State.rap` sibling natively that is **not** mirrored to the ES-facing directory and is not recreated on sync-in                                                                                                                                                                                                    |
| 56  | `flycast` is the only wrong `<directory>` (finding 40)                                                | **`openmsx` is a second, and worse.** It writes to `bios/openmsx/savestates/`, a different top-level tree from the declared `saves/msx1/openmsx`, which stayed empty across two real saves                                                                                                                                                |
| 57  | `<image>` is best-effort everywhere (finding 42)                                                      | Still true as a rule, but openMSX writes a real 7.5 KB `.png` beside every state, so the field is worth reading rather than skipping                                                                                                                                                                                                      |
| 58  | (not addressed) whether a documented emulator hotkey actually reaches the emulator                    | Not necessarily. **NVIDIA's Photo mode overlay swallowed Alt+F2**, the key RetroBat binds to openMSX's save state, and nothing anywhere reported it. A system overlay can silently cost a user their save                                                                                                                                 |
| 59  | `game-start` never fires when the display name contains a space (finding 22)                          | **Amended.** ES fires it every time and logs `executing:` for every script in the folder. The `.bat` never starts: ShellExecute routes it through the `batfile` association `cmd /c "%1" %*`, and cmd's quote-stripping rule mangles any line whose arguments carry quotes                                                                |
| 60  | (not addressed) whether some other hook form works                                                    | `.ps1` fails too, on any parenthesis, because ES builds `powershell <script> <args>` with no `-File`. An `.exe` took a full No-Intro name as three intact arguments. **RomMBat's hooks must be executables**                                                                                                                              |
| 61  | (not addressed) whether ES records what it runs                                                       | Only at `LogLevel=debug`, which nothing sets and the ES menu does not surface; the default is error-only. And `executing:` is logged even when the process never starts, so it is not evidence of execution                                                                                                                               |
| 62  | (not addressed) how long the diagnostic evidence lasts                                                | `es_log.txt` rotates through four files on **every** ES start, `emulatorLauncher.log` rotates to `.old`, and `RetroBat.log` is overwritten. Collect before the next launch anywhere, or the session is gone                                                                                                                               |
| 63  | ES events are the nine with folders on disk (L262)                                                    | `game-selected` and `system-selected` also fire, on every navigation move, and `game-selected` carries `<system> <rom path> <display name>`, the system that `game-start` withholds. Neither ships a folder                                                                                                                               |
| 64  | The second host's hook failure has no candidate cause (finding 27)                                    | **Resolved.** That host runs an `.exe` hook for all four events and neither script form for any, including the three that pass no arguments. `--home` was passed correctly and the volume was writable, so both earlier theories are dead                                                                                                 |
| 65  | (not addressed) what stops a host running a hook                                                      | Two ordinary, unrelated things, neither of them security software: **Notepad++'s installer takes the `.bat` ProgId** (`HKCR\.bat` = `Notepad++_file`, the original stashed in `Notepad++_backup`), and the **default `Restricted` PowerShell policy** blocks `.ps1`. Both fail silently                                                   |
| 66  | (not addressed) whether an unsigned exe runs from removable media on a strict host                    | Yes. The second host has **Smart App Control active** (`passed Config CI policy and was allowed to run`) and ran an unsigned, locally compiled exe from a USB stick. Relevant to how RomMBat is distributed                                                                                                                               |

## Measured during M2

Same rules as the table above: these contradict something previously written down, and the
plan is amended in the same change. Measured against RomM 5.1.1 and RetroBat 8.2.0.

| #   | Previously                                                                                       | Measurement says                                                                                                                                                                                                                                                           |
| --- | ------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 67  | The live `es_systems.cfg` carries four systems upstream does not (probe 5, "Other observations") | **Wrong comparison.** That put the live file's 244 `<system>` elements next to `systems_names.lst`'s 240 folder **names**. The shipped template also has 244 active systems, and both files own exactly the same 240 folders under `roms/`. The live file matches upstream |
| 68  | `es_systems.cfg` `<name>` identifies the system (plan L918-928, `retrobat-layout`)               | **`<name>` is not the folder.** Five systems disagree in the shipped file: `gw`/`gameandwatch`, `powerbomberman`/`pb`, `casloopy`/`loopy`, `Windows`/`windows`, and `starship` is used **twice**, for `ghostship` and `starship`. Take the folder from `<path>`            |
| 69  | (not addressed) whether every `<system>` is a sync target                                        | Four own no folder under `roms/` (`library`, `screenshots`, `kodi`, and `retrobat` at `system/es_menu`) and `mess` declares no path at all. Filter on the resolved path, not on a list of names                                                                            |
| 70  | (not addressed) `arcade` and `kodi` appear in the shipped `es_systems.cfg`                       | Both are inside XML comments. A regex over `<system>` finds them; an XML parser correctly does not                                                                                                                                                                         |
| 71  | Generated DTOs are usable for the paged read (plan M2, rule 6)                                   | **`fs_size_bytes` is an `int32`** in `SimpleRomSchema`, `PlatformSchema` and `RomFileSchema`, because the pinned schema declares a bare `integer`. `GET /api/platforms` fails to deserialize on the **first** platform of a real library. Slim hand-written rows instead   |
| 72  | `platform.slug` identifies a platform (plan M2, `platform-mapping`)                              | **Not unique.** A real 123-platform library has **72 distinct slugs**: every system has an `-unofficial` twin sharing one. `fs_slug` and `id` are unique. Keyed by slug, 51 of 123 platforms vanish from the mapping surface                                               |
| 73  | `PUT /api/devices/{id}` takes `DeviceUpdatePayload` (`romm-api`, plan M2)                        | Only when it carries **just the fields being changed**. The generated payload serializes unset properties as explicit nulls and the server answers **500** with a plain-text body. Sending only `sync_config` answers 200 and preserves the rest                           |
| 74  | (not addressed) the live suite's own rate limit                                                  | `POST /api/auth/device/init` is 10/min/IP. One pairing per live test exceeds it and looks like a client fault. Live catalog tests share one pairing per class                                                                                                              |
| 75  | The YAML has 168 explicit pairs and 19 stale entries (plan L848, L852)                           | 167 and 18. `verify.py` split on the first `platforms:` and matched any four-space key, which also catches `scan.gamelist.export`, a boolean. Both the script and the plan now read the block by indentation. Nothing upstream moved                                       |
| 76  | (not addressed) whether `fs_extension` is always present                                         | No. 23 ROMs on one platform of a real instance carry an empty `fs_extension`. They are excluded like any other unlaunchable format, but must not be reported as format "`.`"                                                                                               |
| 77  | (not addressed) whether `order_by=id` is accepted                                                | It is, and ascending id is what makes offset paging survive a library changing mid-walk: new ROMs get higher ids and land past the cursor. Verified live that page two starts after page one with no overlap                                                               |

## Measured during M3

Same rules again. Measured against RomM **5.1.1-beta.1** behind **nginx 1.29.5 / openresty**,
on the 83,131 ROM library, and against RetroBat 8.2.0. Three of these contradict the plan's
M3 section, which is amended in the same change.

The sampled figures come from 2,000 ROMs read as twenty 100-row pages spread evenly across
the library by offset, so they describe a 2.4% sample rather than the whole of it.

| #   | Previously                                                                                                   | Measurement says                                                                                                                                                                                                                                                                                                                                                                        |
| --- | ------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 78  | Always send `Range: bytes=0-`, because multi-file ROMs need it for the cached-zip path (plan M3, `romm-api`) | **Backwards for multi-file.** Any `Range` on a multi-file ROM is refused **403** by nginx 1.29.5: `bytes=0-`, `bytes=0-1023` and a mid-file range alike. Without it: 200, `application/zip`, `Content-Length` present, **no `ETag`, no `Accept-Ranges`**. So a multi-file download is not resumable and not conditional, whatever header is sent                                        |
| 79  | Downloads are resumable (M0 probe 6a, measured on a different endpoint)                                      | Confirmed on `/api/roms/{id}/content/{fs_name}` for a **single-file** ROM: `Range: bytes=0-` answers 206 with `Content-Range`, the `ETag` is nginx's `hex(mtime)-hex(size)` form (`"6a45147a-1009"`, where `0x1009` is the 4,105-byte length), a resume with a valid `If-Range` produced a byte-identical file, and a **stale `If-Range` returned a full 200** rather than a splice     |
| 80  | Only `crc_hash` describes uncompressed content (plan M3, `romm-api`)                                         | **All three do.** A 1,025-byte `.zip` reports `md5`/`sha1`/`crc` matching the 16,400-byte `.nes` **inside** it exactly, and matching nothing about the archive; a `.chd` reports the hashes of its own bytes. So verifying a downloaded archive against `md5_hash` fails every time unless the hash is taken inside it, and adoption must hash inside a local archive too               |
| 81  | Reconcile deletions against `GET /api/roms/identifiers` (plan M3, core principle 2)                          | **504 after 300 s** on 83,131 ROMs, so the mechanism does not work at the scale the plan exists to serve. It takes no parameters, so it cannot be scoped or paged. `/api/platforms/identifiers` answers in **0.3 s** (490 bytes, 123 ids) and `/api/collections/identifiers` in **1.4 s**, so the endpoint family is fine and this member of it is not                                  |
| 82  | 23 ROMs on one platform carry an empty `fs_extension` (finding 76)                                           | The rule behind it: **every multi-file ROM is extensionless and every extensionless ROM is multi-file**, 105 of 105 both ways in the sample. M2's extension filter therefore already excludes **100%** of multi-file ROMs, which is why nothing above reaches a sync set today                                                                                                          |
| 83  | The budget is arithmetic on `fs_size_bytes` (plan M3)                                                        | Safe for single-file: `fs_size_bytes` equalled `Content-Length` exactly. For multi-file it is the **sum of the member files** (2,740,189 = 2,740,080 + 109) against a 2,740,866-byte served zip, ~677 bytes of container. And **HEAD's `Content-Length` is wrong there**: 2,740,768 across three HEADs against a stable 2,740,866 across two GETs, so never pre-flight a size with HEAD |
| 84  | (not addressed) what `GET /api/roms/by-hash` costs                                                           | It accepts `md5_hash`, `sha1_hash` or `crc_hash` and all three returned the same ROM. A hit is a ~12 KB `DetailedRomSchema` in **133-385 ms**; a **miss is a 404 after 8.3 s**. Usable to attribute a handful of unknown local files, never as a library-wide adoption sweep                                                                                                            |
| 85  | (not addressed) whether every ROM carries a hash                                                             | No. Of 1,895 single-file ROMs sampled, **1,724 (91.0%) carry `md5_hash`** and **1,824 (96.3%) `sha1_hash`**. Verification has to degrade to size when the server has no hash to compare against, and say that it did                                                                                                                                                                    |
| 86  | (not addressed) how much of a real library FAT32 cannot hold                                                 | **3.05%**, 61 of 2,000 sampled ROMs over the 4 GB ceiling. The longest `fs_name` was **174 characters**, which with a deep portable root and an `images/` sibling is inside `MAX_PATH` reach. **No `fs_name` in 2,000 carried a character Windows refuses**, so the Linux-to-Windows name hazard is real in principle and unobserved here                                               |
| 87  | (not addressed) the cost of a per-ROM existence check                                                        | `GET /api/roms/{id}/simple` took **4.2 s** for a hit and **0.45 s** for a miss, so checking locally present ROMs one at a time is not a cheap substitute for a reconcile either                                                                                                                                                                                                         |

## Measured during M4

Same rules again. Measured against RomM **5.1.1-beta.1** behind nginx on a library that has
grown to **83,435 ROMs**, and against RetroBat 8.2.0 on two installs: `K:\RetroBat`, the
probe tree, for everything that writes, and a second, real, fully scraped install read only,
for what a user's own gamelists and media actually look like. Probes in `tools/m4-probes/`.

The sampled figures come from **5,000 ROMs read as twenty 250-row pages** spread evenly
across the library by offset, so they describe a 6.0% sample rather than the whole of it.
Two of these contradict something already in this file or in the plan, and both are amended
in the same change.

`backend/utils/gamelist_exporter.py` is now vendored at
`reference/romm-gamelist_exporter.py`, and it **independently confirms two of the conversions
below from upstream's own code**: `datetime.fromtimestamp(timestamp / 1000)` for
`first_release_date` and `average_rating / 100` with a comment naming the scale. `verify.py`
asserts both still hold, so upstream changing either shows up as a drift rather than as a
wrong number in a gamelist.

| #   | Previously                                                                                    | Measurement says                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| --- | --------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 88  | (not addressed) what `url_cover` and `url_manual` point at                                    | **A third party, with a third party's credentials.** Both are `neoclone.screenscraper.fr` API URLs carrying someone else's `devid` and `devpassword` in the query string. Unusable twice over: off-LAN, which breaks the offline story, and not ours to send                                                                                                                                                                                                                                     |
| 89  | Media downloads reuse M3's `/api/roms/{id}/content` path (plan M4)                            | **They are static resource paths, and they come in two shapes.** `path_cover_small`/`path_cover_large` are already rooted at `/assets/romm/resources/` and carry a `?ts=` query containing a **raw space**; `path_manual`, `path_video` and the `ss_metadata` image paths are relative to that prefix. Normalise both onto the prefix exactly once                                                                                                                                               |
| 90  | (not addressed) what the wrong prefix does                                                    | **Answers 200.** Requesting `roms/20/1393/manual/1393.pdf` as given returns **5,826 bytes of the web UI's `index.html`** with an `ETag` and `Accept-Ranges`, and would be written to disk as a PDF. Status is not enough: the content type has to be checked                                                                                                                                                                                                                                     |
| 91  | (not addressed) whether the device token authenticates media                                  | **No token is needed at all.** Bearer and anonymous requests were byte-identical on every media path tried. nginx serves them: `Accept-Ranges: bytes`, an `hex(mtime)-hex(size)` `ETag`, `bytes=0-99` answers 206 with a `Content-Range`, and a range past the end answers 416. M3's resume machinery applies unchanged                                                                                                                                                                          |
| 92  | (not addressed) how much media a real library has, and how big it is                          | cover 84.3%, `merged_screenshots` 84.6%, `path_video` **72.1%**, `path_manual` 46.1%, `summary` 81.9%, `metadatum` populated on 100%. Marquee is provider-scoped: `ss_metadata` is present on 1,093 of 1,250 and carries `logo_path` on 994, so **79.5%** of the library, against `marquee_path` on 1,077. Medians: thumbnail 104 KB, cover **525 KB**, logo 445 KB, video **1.99 MB**, manual **2.45 MB**. So a 100-game `nes` set is ~12.8 MB of ROMs against ~550 MB of media, a factor of 43 |
| 92b | (not addressed) which ScreenScraper asset is EmulationStation's marquee                       | **`logo_path`, not `marquee_path`.** Upstream's own `gamelist_exporter.py` maps `"marquee": [ss.get("logo_path"), gl.get("marquee_path")]`. ES's marquee is game logo art; ScreenScraper's marquee is an arcade cabinet marquee. Now vendored at `reference/romm-gamelist_exporter.py` so the mapping is checkable                                                                                                                                                                               |
| 93  | `GET /api/roms/{id}` is how M4 gets metadata (plan M4 by implication)                         | **It costs N requests and buys nothing.** `SimpleRomSchema`, which the paged read already returns, carries `metadatum`, `summary`, every media path, `regions` and `languages`. `DetailedRomSchema`'s 7 extra fields are all user arrays and were **empty on every ROM tried**. Per-ROM: 0.15 s each, **150 s for 1,000 games**. Per-page: 0 extra requests, 15.7% of a page M2 already reads                                                                                                    |
| 94  | (not addressed) whether the present ROMs can be asked for by id                               | **No.** `GET /api/roms` has 47 query parameters and not one of them takes ROM ids. "Metadata for exactly what is on disk" is not a query, so it is either the walk carrying it or one request per ROM                                                                                                                                                                                                                                                                                            |
| 95  | (not addressed) what unit `metadatum.first_release_date` uses                                 | **Milliseconds.** Read as seconds, all 4,108 sampled values land in year 0; read as milliseconds they land in **1983-2026**. No value was negative, so a pre-1970 release is unobserved rather than impossible                                                                                                                                                                                                                                                                                   |
| 96  | (not addressed) what scale `metadatum.average_rating` uses                                    | **0 to 100**, min 5.0, max 100.0, and **all 3,216 sampled values are above 1.0**. A gamelist `<rating>` is 0-1 to two decimals, so it is a divide by 100. A real scraped install's ratings sit on 17 distinct values in 0.05 steps, which is ScreenScraper's /20 score and finer-grained here                                                                                                                                                                                                    |
| 97  | (not addressed) whether `player_count` maps onto `<players>`                                  | **It is already the same form.** `"1"` 3,406, `"1-2"` 1,008, `"1-4"` 328, up to `"1-16"`. A real install's `<players>` is the identical vocabulary. A straight copy, and the only conversion in this table that is not one                                                                                                                                                                                                                                                                       |
| 98  | `<developer>` and `<publisher>` come from the metadata (plan M4)                              | **Neither role can be recovered.** `metadatum.companies` is a flat array merging both, **alphabetically sorted on 4,197 of 4,197** rows that have one, so any positional reading is reading the alphabet. Chrono Trigger reads `['Squaresoft', 'Squaresoft']`, the same company twice. 3,959 of 5,000 carry exactly two entries. `igdb_metadata.companies` is unsorted but unlabelled                                                                                                            |
| 99  | (not addressed) how an array becomes a single-valued gamelist element                         | The real install already does it: **2,079 of its 4,440 `<genre>` values contain a comma or a slash** (`Racing, Driving`, `Action / Adventure`) out of 111 distinct values. Joining with a comma and a space reproduces the convention rather than departing from it. `franchises` needs deduping first: 18 of 5,000 repeat a name                                                                                                                                                                |
| 100 | (not addressed) whether `regions` and `languages` can be copied                               | **Different vocabularies both ways.** RomM says `Japan`, `USA`, `Europe`, `World`; the real install writes `jp`, `us`, `eu`, `wr`. RomM says `English`, `French`; ES writes `en,fr` comma-joined. 246 of 5,000 rows carry more than one region while `<region>` is single-valued, and `languages` is present on only 18.3%                                                                                                                                                                       |
| 101 | Media is named after the ROM file (plan M4, `retrobat-layout`)                                | Confirmed exactly, read off a real scraped install rather than from memory: `images/<stem>-image.png`, `images/<stem>-thumb.png`, **`images/<stem>-marquee.png`** (marquee lives under `images/`, not its own folder), `videos/<stem>-video.mp4`, `manuals/<stem>-manual.pdf`, where `<stem>` is the ROM file name without its extension                                                                                                                                                         |
| 102 | ES writes back favourite, playcount, lastplayed and hidden (plan M4)                          | **Incomplete, and two of the four are unobserved.** Across 4,531 entries in 32 real gamelists: `playcount` 115, `lastplayed` 115, **`gametime` 114**, and **no `favorite` and no `hidden` at all**. The merge surface is much wider: `scrap` 4,525 (self-closing, `name` and `date` attributes), `game@id` 4,493, `cheevosHash` 4,187, `md5` 2,815, `cheevosId` 2,329, `arcadesystemname` 568, `multidisk` 161, `crc32` 8. Own an allowlist, never a blocklist                                   |
| 103 | **XML comments survive an ES rewrite** (probe 3, "Writing `gamelist.xml` under a running ES") | **Refuted.** When ES does rewrite the file it drops **every** comment, both at document level and inside a `<game>` it did not otherwise touch. Unknown **elements** and **attributes** do survive, including `<scrap/>` in its self-closing form and `id`/`source` on `<game>`, so the original conclusion holds for everything except comments                                                                                                                                                 |
| 104 | ES rewrites `gamelist.xml` on exit (probe 3)                                                  | **Only when it has something to change.** A full session that started ES, called `/reloadgames`, and quit left a 1,810-byte file **byte-identical**, mtime included. The rewrite in probe 3 followed a game actually being played. So the no-churn regression is meaningful, but it has to compare the file **after** ES has touched it, not the one RomMBat wrote                                                                                                                               |
| 105 | ES merges in place, so element order survives (probe 3)                                       | **For entries it does not touch.** Playing one game rewrote that entry's children into ES's own order (`path,name,desc,genre,rating,releasedate,developer,publisher,players,favorite,playcount,lastplayed,gametime,lang,region,...`), **moved it to the end of the file**, and **dropped `<hidden>false</hidden>`**, which is the same default-pruning seen on `es_settings.cfg`. The untouched entry kept RomMBat's order exactly                                                               |
| 106 | (not addressed) what a gamelist entry with no file behind it does                             | **Nothing.** ES reported 6 games for 6 ROM files while the gamelist held 3 entries, one of them naming a file that does not exist, so a stale entry left by an eviction is not a phantom game. It **does survive the rewrite**, so it is inert but permanent until RomMBat removes it                                                                                                                                                                                                            |
| 107 | `/reloadgames` is the refresh mechanism (probe 3, plan M4)                                    | **Ignored while a game is running**, exactly as `/quit` and `/emukill` are. 200 in 1 ms, and a ROM added to the folder was still not reported five seconds later. Reproduced twice. So the one API call M4 depends on shares the trap: a 200 is not evidence the reload happened                                                                                                                                                                                                                 |
| 108 | A short timeout covers a reload with ES absent (plan M4)                                      | **2.04 s**, five raw TCP connects and three `HttpClient` requests alike, which is M0 probe 6b's "host up, port closed" row (2,040 ms) reappearing on loopback. The project's 2 s interactive `ConnectTimeout` fires at almost exactly the same moment and buys nothing. ES being absent is the ordinary case, so this client needs a far shorter one                                                                                                                                             |
| 109 | Long paths are the hazard for constructed media names (plan, principle 4)                     | **The 255-character file name is the ceiling, and `\\?\` does not lift it**: 255 wrote, 256 failed `IOException` both plain and prefixed, because it is a filesystem component limit rather than `MAX_PATH`. Total path reached 306 characters fine on this machine (`LongPathsEnabled=1`). The longest `fs_name` in the sample is 156 characters, so a suffix plus a folder is well inside it                                                                                                   |
| 110 | (not addressed) which characters a constructed name must lose                                 | `<`, `>`, `"`, `\|`, `?`, `*` raise `IOException` and `/`, `\` raise `DirectoryNotFoundException`, all loud. **`:` does not**: it writes an **NTFS alternate data stream**, so the call succeeds, the directory lists a file called `probe`, and the file the gamelist names is not there. A trailing dot or space is silently stripped. `CON.png`, `PRN.png`, `COM1.png` all wrote on Windows 11 26200                                                                                          |
| 111 | The per-system cap is for navigability (probe 5, plan M4)                                     | **A cap cannot deliver that on its own.** ES lists ROM files it has no gamelist entry for (probe 3, and reconfirmed here), so dropping entries hides no games and only strips their art. `ParseGamelistOnly` does exist as an ES setting, beside `IgnoreGamelist`, backing `--gamelist-only`, but it is global and would change every system including ones RomMBat does not manage                                                                                                              |

## Measured during M6

Same rules again. Three read-only probes (`tools/m6-probes/`) against the real 8.2.0 install,
which is the one carrying a substantial library and real play history, not the K: probe
stick. Nothing here wrote into an install. Every number the M6 section quoted about
`emulatorLauncher.log` came from M0 on a different tree, so all of them are re-taken.

The install has moved drive letter since its log began, which is why finding 113 exists at
all. That is luck rather than design, and it is the single most useful thing in this batch.

| #   | Previously                                                                                                           | Measurement says                                                                                                                                                                                                                                                                                                                                                              |
| --- | -------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 112 | `emulatorLauncher.log` is 268 KB for 5 weeks and 70 launches (plan M6, probe 1)                                      | That describes a smaller install, not the mechanism. Live **503,225 B** for 6 weeks and **159** launches, beside a **1,048,604 B** `.log.old` for the 3 weeks before it at **265** launches. **Rotation is a size threshold near 1 MiB**, and the two files **do not overlap**, so reading `.old` then live yields launches in time order across the boundary                 |
| 113 | Rom paths in the log are rooted at the install (plan M6 by implication)                                              | **They are rooted at whatever drive letter the install had at the time.** 295 of 424 read `D:\RetroBat` and 129 read `E:\RetroBat`, one install, one continuous log. Relativising by stripping the current root silently discards 70% of the history, so relativise on the `roms\<system>\` segment instead                                                                   |
| 114 | `[Startup]` identifies a launch (plan M6 by implication)                                                             | **730 `[Startup]` lines, of which 424 are a game launch.** `emulatorLauncher.exe` is also invoked for `-updatestores` and similar, so keying on `[Startup]` over-counts by 72%. The discriminator is the presence of `-rom`                                                                                                                                                   |
| 115 | (not addressed) what shape `-rom` takes                                                                              | Three shapes, and a naive read misses two. **Unquoted once in 424**, with spaces and parentheses in the path, so `-rom "([^"]+)"` misses it. **Not the final flag 19 times**, and **`-core` written after it 5 times**, so a positional read misses those. Read the quoted form to its closing quote and the unquoted form to end of line                                     |
| 116 | (not addressed) whether the log can supply an end time                                                               | **No. 187 of 424 launches never record `Process exited with code`.** End time has to come from the `game-end` hook's own timestamp. Exit codes seen: 226 zero, 2 one, 5 minus one, 3 `-1073741819` (access violation), 1 `-805306369`                                                                                                                                         |
| 117 | (not addressed) how the log is encoded                                                                               | **Opens with a UTF-8 BOM**, and carries 15 unstamped continuation lines across the two files, .NET stack traces among them. A line-per-record parser must tolerate both                                                                                                                                                                                                       |
| 118 | An orphan `game-end` has to be discarded by inference (plan M6)                                                      | **An ES-menu launch is identifiable rather than inferred.** 27 launches carry `-system retrobat` with a `-rom` under `system\es_menu\`. So RomMBat's own exit not becoming a play session is a rule keyed on observable data, which is stronger than the plan assumed was available                                                                                           |
| 119 | Four top-level directories under `saves/` are emulator-named, not systems (probe 2)                                  | **Nine**, against the 243 systems the live `es_systems.cfg` declares: `amiga`, `dolphin`, `gameandwatch`, `ghostship`, `loopy`, `mesen`, `pb`, `psxmame`, `windows`                                                                                                                                                                                                           |
| 120 | The second segment is the emulator (plan M6, probe 2)                                                                | **Not reliably.** `mame/artwork`, `mame/cfg`, `mame/ctrlr`, `n64/sram`, `n64/games`, `n64/sstates`, `psp/SYSTEM`, `psp/Cheats`, `switch/user`, `switch/sdmc`, `rtcw/Main` and `dolphin/User` name no emulator. Where states live it is emulator-**and-core**, so `saves/gbc/libretro.gambatte/` sits beside `saves/gbc/*.srm`. Discovery cannot be positional in either level |
| 121 | A loose file under `saves/<system>/` is a class A battery save (plan M6)                                             | **`xbox` refutes it**: `eeprom.bin` and a 39,714,816 B `xbox_hdd.qcow2` sit loose at the system root and both are class D. And **`megacd` interleaves classes at one level**, per-game `.brm` and `.srm` beside the shared `4Mbit_cart.brm`, so excluding class D is a named-container list rather than a positional rule                                                     |
| 122 | `save_shapes.json` leaves 21 systems `_unclassified` (F19)                                                           | Still 21, and **all 21 hold content on the measured install**. `ports` holds content and is absent from the file entirely, not even listed as unclassified. So the bundled data is short of the tree in two different ways                                                                                                                                                    |
| 123 | `dolphin_sync_saves` must be detected before trusting a location (finding 9c, and see 189 for what it actually does) | **Unset on this install**, as are all four class-D options (`duckstation_memcardtype`, `pcsx2_slot1_memory`, `flycast_vmupergame`, `dolphin_slotA`). Stock is the case to build for; the conversion hazards are stage 2's to detect                                                                                                                                           |
| 124 | RPCS3's 32,451 files make any recursive content hash a performance problem (plan M6)                                 | **The count is of the emulator's data root, not of saves.** `saves/ps3/rpcs3` is 32,451 files and **52.87 GB**, hashing in **426 s warm and 512 s cold**, but that is `dev_hdd0` entire. The savedata subtree is **17 directories, 77 files, 16.3 MB, 0.06 s**. So the input is "scope the save unit in the shape definition", not "budget the hash"                          |
| 125 | (not addressed) what a class A pass actually costs                                                                   | **37 loose files, 43.0 MB, 0.51 s** across every system on a real install, and **38 MB of that is `xbox`'s class-D disk image** which it must not read. MAME's whole `nvram` tree, for comparison, is 1,531 files and 8.0 s                                                                                                                                                   |

**Not re-measured, and stated so rather than assumed forward.** Both available installs are
NTFS, so M0's 2-second FAT and exFAT mtime granularity could not be re-taken here and stands
on its original measurement; the FAT32 handling stays covered by a synthetic test. The full
negotiate, upload, download, ack and complete round trip was **not** driven live in this
branch, so F1, F2, F3, F6 and F12 remain the authority for server behaviour.

---

## Measured during M6 stage 2a

Server-side only. The only probe authorised for this branch was against the live RomM instance
in `DEVELOPER_SETUP.md`; **nothing read or wrote a RetroBat install**, so every RetroBat fact
this stage builds on is inherited from probe 2 above rather than re-taken. Probe artifacts are
under `probe-output/m6s2-*.txt`, which is gitignored; the durable half is checked in as tests.

| #   | Previously                                                                    | Measurement says                                                                                                                                                                                                                                                                                                                                                                                                                         |
| --- | ----------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 126 | `POST /api/states` had never been called from this repo (plan M6)             | **It is an upsert, not an append.** Three posts of one `file_name` reused one row (id 115) across two different payloads. So there is no slot history to prune, no `autocleanup` to ask for, and a replayed flush is idempotent for free. `PUT /api/states/{id}` works and is unnecessary                                                                                                                                                |
| 127 | The `emulator` distinguishes one state from another (plan M6, by implication) | **It does not. The key is `(rom_id, file_name)` and nothing else.** Five posts of one name under `libretro`, `bizhawk`, `libretro.snes9x`, `libretro.bsnes` and `libretro/evil` all reused id 119, overwriting the row's emulator and moving its stored file between directories each time                                                                                                                                               |
| 128 | (not addressed) whether a bracketed tag separates two states                  | **It does.** `TagProbe [libretro.snes9x].state1` and `TagProbe [libretro.bsnes].state1` produced ids 121 and 122. So the key is the whole `file_name`, not `file_name_no_tags`, and scoping the uploaded name is a working fix for 127                                                                                                                                                                                                   |
| 129 | A state has a `slot` and a `content_hash` (plan M6, by implication)           | **Neither exists**, in the pinned schema or in the live response. `{emulator}:{core}:{slot}` is therefore a local identity only, and "is this state in step" is answerable only from a hash the device recorded itself                                                                                                                                                                                                                   |
| 130 | The server renames an upload (F6, for saves)                                  | **Not for states.** A save came back `Probe Save [2026-08-17_12-27-44].srm`; a state came back exactly as sent. `file_name_no_tags` is still computed, and strips `(USA)` out of a real ROM name, so it is not a way to recover the name that was sent                                                                                                                                                                                   |
| 131 | (not addressed) what a zero-byte `screenshotFile` does                        | **Accepted and stored as a real screenshot row** (id 151, `file_size_bytes: 0`). Since RetroBat's mirror races the emulator writing the image and a zero-byte result was measured across three saves of one game, the client has to suppress it, because nothing downstream does                                                                                                                                                         |
| 132 | Two open download cases turn on whether negotiate volunteers slots (plan M6)  | **It never does, so both cases resolve negatively.** A device with a save on the server negotiated an **empty** `saves` array and got `operations: []`; negotiating one unrelated slot returned exactly that slot. Negotiate is client-driven over the set the client names, so a fresh device cannot discover its saves through it. **Withdrawn. See measurement 151**: that device was simply current for the only save on the account |
| 133 | (not addressed) whether `emulator` is sanitised server-side                   | **It is not.** `libretro/evil` was accepted and became two path segments in the stored state's `file_path`. Worth reporting upstream. RomMBat's own schema refuses a separator in that column, so it cannot send one                                                                                                                                                                                                                     |

**Not measured, and named rather than left to read as done.** The cost of spawning the agent
from the hook, which `docs/PLAN.md` assigned to stage 2 by name: taking it means replacing a
binary on a real install and launching a game, and that was not authorised at the time. And no
platform was certified, because that needs a human to start EmulationStation.

---

## Measured during M6 stage 2a, hands-on pass

Spinnich launched `mastersystem`/Phantasy Star (Brazil) on the K: install and made a save state
under four emulators; the agent scanned and flushed. This is the first time anything in this
repository has handled a state a real emulator wrote. It is **not** a certification: it is one
game on one system, which is what `docs/platforms/README.md` asks of each M6 stage.

| #   | Previously                                                                   | Measurement says                                                                                                                                                                                                                                                                                              |
| --- | ---------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 134 | Two libretro cores writing one filename would collide server-side (126, 127) | **Confirmed end to end, and the scoped name holds.** `genesis_plus_gx` and `picodrive` both wrote `Phantasy Star (Brazil).state1` for one ROM and landed as two rows, 9,202 B and 6,282 B. Without the scope in the uploaded name one would have replaced the other                                           |
| 135 | (not addressed) which slot a save-state hotkey writes                        | **Not slot 0, and not fixed.** libretro wrote `.state1` and BizHawk wrote `.QuickSave2.State`. Reading the slot off the filename is what makes both work; expanding `firstslot..lastslot` would have found neither                                                                                            |
| 136 | The `.txt` sidecar is emitted unconditionally (probe 2, retracted reading)   | **Wrong, and this corrects it.** `libretro` wrote none under either core. `jgenesis` wrote the plain rom filename; `bizhawk` wrote `Phantasy Star (B).SMSHawk`, its own truncated name plus the core. Absence and presence both signal nothing; only the contents are ever useful                             |
| 137 | The emulator version can be read from the binary (stage 2a design)           | **It cannot, on any emulator tried.** A libretro core DLL has empty `ProductVersion` and `FileVersion`, and `jgenesis` and `bizhawk` each ship two top-level executables, so the single-executable rule declines. `emulator_version` is null in practice and `retrobat_version` is what identifies the build  |
| 138 | A state screenshot is best-effort because the emulator may not write one     | **True, and there is a second reason.** The image is uploaded, stored against the ROM at the right name and size, and then **not linked to the state**, which reads `screenshot: null` and stays so. Roughly a third of thirty-five attempts. Not reproducible on demand; the request is provably well formed |
| 139 | (not addressed) whether an emulator's battery save keeps the ROM's name      | **BizHawk truncates it**: `Phantasy Star (Brazil).zip` produced `bizhawk/Phantasy Star (B).SaveRAM`. It sits in a subdirectory so this release reports it rather than syncing it, but any future attribution by filename has to expect a truncated stem                                                       |

**Still not measured after the hands-on pass.** Whether `{{slot}}` renders as an empty string at
libretro slot zero: the default slot turned out to be 1, so the zero case was never produced.
The parser accepts zero digits and maps them to slot 0, and the screenshot beside a state is
rendered from the digits that were on disk rather than from the parsed slot, so both halves hold
whichever way it renders. `bigpemu` remains the one emulator of thirteen never driven to a real
state. And the hook-spawn cost is still outstanding.

---

## Measured during M6 stage 2b

Spinnich authorised every probe: the live RomM instance, a read-only sweep of the real
`E:\RetroBat` install (the one probe 2 measured), and the `K:` development stick. Probe
artifacts are under `probe-output/m6/`, which is gitignored; the scripts are checked in under
`tools/m6-probes/`. **Fourteen results, and five of them refute something this document or
`docs/PLAN.md` currently asserts.**

| #   | Previously                                                                         | Measurement says                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| --- | ---------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 140 | Class C is "a directory per game" (plan, the class table)                          | **Refuted, on three systems at once.** `ps3` holds `BLUS30109G6A383E91`, `BLUS30109G6A3B071C` and `BLUS30109S` for one title id, and `BCUS98111-AUTOSAVE` beside `BCUS98111-USERDATA`. `psp` holds `UCES01011` and `ULES01513SYSDATA`. **`gamecube` has no per-game directory at all**: `69-GXBE-game1.ssx.gci` and `69-GXBE-settings.ssx.gci` are two files in a shared folder                                                                                                             |
| 141 | The unit key is the directory's name                                               | **It is a prefix of it.** `ULES01513SYSDATA` carries key `ULES01513`, and `BLUS30187GAMEDAT9ZLDR0F5K7M4000` carries `BLUS30187`. Matching the whole segment finds nothing                                                                                                                                                                                                                                                                                                                   |
| 142 | RPCS3 hashing costs 426 s and the scoped subtree 0.06 s (plan, M6)                 | **Confirmed to two decimal places**, re-run rather than inherited: the data root is 32,451 files / 52,868.4 MB / **426.07 s**, its `dev_hdd0/home/*/savedata` subtree 77 files / 16.3 MB / **0.06 s**, MAME's whole `nvram` 1,531 files / 137.3 MB / 8.02 s                                                                                                                                                                                                                                 |
| 143 | Reading the ID out of the ROM is the fallback route (plan, M6; F17)                | **It reaches nothing this stage needs.** Every image in five systems, head read only: `gamecube` 178 `.rvz`, **100%** readable at `0x58` with the version checked; `wii` 40 `.rvz` + 13 `.wad`, **75.5%**; `psp` 147 `.cso` + 7 `.chd`, **0%**; `ps3` 23 `.dec.iso`, **0%**; `psx` 386 `.chd`, **0%**. No constant offset reaches a `.cso`, a `.chd` or an ISO9660 image                                                                                                                    |
| 144 | `PARAM.SFO` yields the Game ID (start-m6-stage2b brief)                            | **It yields nothing the directory name does not.** Its keys are `SAVEDATA_DIRECTORY`, which is the directory's own name, and `TITLE`, a human string (`'echochrome'`, `'The 3rd Birthday'`). So parsing it buys a fuzzy title match, never an exact key                                                                                                                                                                                                                                     |
| 145 | The state `.txt` sidecar may be a cheaper third route (stage 2a ledger)            | **It is, and it is measured.** `ppsspp/3rd Birthday, The (Europe).txt` holds `ULES01513_1.00`, whose `ULES01513` prefix joins `SAVEDATA/ULES01513SYSDATA`, while the stem resolves through `RomIndex`. It needs no ROM read and no observed launch, and it covers only games that have a state                                                                                                                                                                                              |
| 146 | Wii's NAND "is not all attributable" and what counts as a save unit is open (plan) | **Decided from data.** `title/00010000/<hex>/` is the disc-game tree and the hex is the ASCII game code (`52534245` = `RSBE`), which joins exactly to what route 2 reads at `0x58`. `title/00000001/*` is system titles, and `shared2/`, `sys/` and `fst.bin` are system state. A title with `content/title.tmd` and no `data/` is an installed stub, not a save                                                                                                                            |
| 147 | The server renames a save (F6, measurement 130)                                    | **True for a bundled directory save too, and the untagged name is the unit key.** `UCES01011.zip` came back `'UCES01011 [2026-08-17_23-52-18].zip'` with `file_name_no_tags` `'UCES01011'` and `file_extension` `'zip'`                                                                                                                                                                                                                                                                     |
| 148 | `content_hash` is the MD5 of the bytes uploaded (F3, and the download verify)      | **True for a plain file, false for an archive.** A 24 B payload, 570 B of `'A'`, 570 B random and 570 B of NUL all match exactly. A 570 B zip does not, independent of `Content-Type` and of filename. Rebuilding one member at a different compression level and timestamp gives a different zip and **the same** digest; renaming the member changes it                                                                                                                                   |
| 149 | (not addressed) what negotiate compares for a bundled save                         | **The server's own returned digest, and only that.** Sending it answers `no_op (Content is identical)`; sending our logical fold or the archive's MD5 answers `download (Server save is newer)`. Eight candidate reconstructions of the server function reproduce none of the observed values, so it is **not reproducible client-side** and must not be guessed at                                                                                                                         |
| 150 | Different content into one slot appends a row (F3)                                 | **Not on this version. The key is `(rom_id, slot, file_name)` and it replaces.** Same name and different content reused id 136 with the content hash updated and no `overwrite` flag; a different name in the same slot made a second row. F3's two uploads shared a name, so its reading does not hold here. **Withdrawn. See measurement 160**: this was a same-second update read as the general rule                                                                                    |
| 151 | Negotiate never volunteers a slot the client did not submit (**measurement 132**)  | **Refuted, and 132 is withdrawn.** An **empty** `saves` array returned **13 downloads across two ROMs**, one never named by the client. The mechanism, driven: 13 ops, then `GET /api/saves/134/content` plus `POST /api/saves/134/downloaded`, then 12 ops with that save gone. Negotiate returns a download for every save the **device** has no current sync record for. **Refined by 163**: read "every save" as the newest row per slot, which is the only row negotiate ever looks at |
| 152 | A restore writes `file_name_no_tags` plus `file_extension` (plan, M6; F6)          | **Refuted on a real save, which is what 130 half-saw.** `Phantasy Star (Brazil) [2026-08-17_17-01-00].srm` has `file_name_no_tags` `'Phantasy Star'`: the server strips `(Brazil)` as a tag. Writing that produces a filename libretro cannot see. The ROM's own stem plus the extension is the only sound source, and the negotiate operation carries neither                                                                                                                              |
| 153 | MAME's short name **is** the rom basename, so attribution is free (probe 2, plan)  | **Structurally sound and unprovable on this install.** 1,231 `nvram` unit directories against 3 `.zip` files in `roms/mame`, so nothing joins. The names are well-formed MAME short names (`1944`, `19xx`, `1on1gov`, `20pacgal`) and a MAME set names each archive after one, but this library cannot demonstrate it                                                                                                                                                                       |

**What 151 costs, beyond the correction.** `docs/PLAN.md`, the `save-sync` skill and stage 2a's
ledger all record "a fresh device cannot discover the saves the server holds for it" as a real
functional gap needing a separate inventory pass. There is no gap: negotiating with an empty
`saves` array **is** the inventory pass. And `SaveSlotStore.Map`'s fallback for a slot with no
local file, which 2a called provably unreachable, is provably reachable, so the two download
cases 2a closed negatively are open again.

**Not measured, and named rather than left to read as done.** The cost of spawning the agent
from the hook is **still** outstanding, now for a third stage. Permission was granted this time
and the blocker changed rather than lifted: it needs a game launched on an install carrying the
replaced binary, which is a hands-on step and not a probe this session can take alone.

---

## Measured during M6 stage 2b, hands-on pass

Spinnich synced `Bust-A-Move - Deluxe (USA)` onto the `K:` install, launched it through
EmulationStation, saved from the game's own menu, and confirmed the restored save loaded. This
is the first time anything in this repository has handled a directory save a real emulator
wrote. It is **not** a certification: one game, one system, steps 4 and 9 only.

**Four defects came out of it, and none was reachable from a test that existed**, because each
needed either a real tree or a genuine two-sided divergence against a live server.

| #   | Previously                                                                   | The pass says                                                                                                                                                                                                                                                                                                  |
| --- | ---------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 154 | The PSP unit key is a prefix of the directory name (141, measured on `E:`)   | **Confirmed on a second, independently produced sample.** PPSSPP wrote `SAVEDATA/ULUS100570000/`, four files and 91,607 B, and the key extracted as `ULUS10057`. Matching the whole segment would have found nothing                                                                                           |
| 155 | Route 1 is the only route that can attribute a PSP save (143, 144, 145)      | **True, and it works.** No `.cso` header and no state sidecar existed, and the launch window bound it: `Bust-A-Move - Deluxe (USA).cso was running when ULUS10057 was last written`. The hook fired all four events and the launch log carried `-system psp -emulator ppsspp`                                  |
| 156 | A conflict arrives as a negotiate `conflict` action (stage 1's whole design) | **Not for a real divergence.** A save changed on both sides negotiated as **`upload`**, reason `Client save is newer (no sync history)`, and the POST returned **409**. Negotiate decides from the hashes it is handed; the stale sync record is the part it cannot see. So 409 is the path, not the exception |
| 157 | The server's archive digest is not our fold (148, 149)                       | **Confirmed on real data.** Our fold `4eea879a…` against the server's `a92d31a4…` for the same unit, and a re-sync answered `no_op` only when the server's own value went back on the wire                                                                                                                     |
| 158 | (not addressed) what a class C scan costs on a real tree                     | **4.1 s wall** for the whole `K:` saves tree, 1,231 MAME nvram units and everything else, including hashing                                                                                                                                                                                                    |
| 159 | (not addressed) whether an emulator loads a unit RomMBat restored            | **PPSSPP does.** The staged restore (not atomic, #38) put the server's four files into `SAVEDATA/ULUS100570000/` and Bust-A-Move loaded the save. With the fold proving the bytes identical, a real save round-trips the same way                                                                              |

**The four defects, in the order they surfaced:**

1. **A refusal was cached when it was only an absence.** K:'s MAME nvram tree has 1,231 units
   and no MAME ROMs beside it, so one scan wrote 1,231 negative bindings. A later sync bringing
   those ROMs in would have found every save still unattributed behind a stale row nothing
   clears. Only a contested key is cached now.
2. **A 409 was reported as a failure rather than recorded as a conflict**, which made the
   milestone's own "done when" unreachable through the path a real divergence takes.
3. **A class C conflict recorded no copy aside**, because `File.Exists` is false for a
   container, so the record promised a copy it did not have.
4. **Resolving one could never succeed.** The resolver had its own restore that verified the
   download against `server_content_hash`; for an archive that comparison is always false. It
   refused itself with `what arrived hashes to 0391c0a9 and the conflict recorded 174b2e82`,
   and **failed closed**, which is the one thing that went right. This is why the restore now
   lives in one shared helper: the same code existed twice, one copy was fixed and the other
   was not.

**What the pass did not prove.** The server side of the conflict was synthetic, 141 bytes
substituted into `GAME.DAT` to force divergence, so "the game loads it" was tested against a
payload Bust-A-Move never wrote. It loaded anyway, and byte-preservation is proven separately by
the fold, so the two together carry the claim; a round trip of untouched game-written content
would carry it in one step and was not run. MAME's short-name join is still undemonstrated, Wii
is still undriven, and the hook-spawn cost is still unmeasured.

---

## Still outstanding overall

All seven probes are answered. Four items are left, and each is blocked on hardware or an
upstream bug rather than on effort:

- **`bigpemu`'s templates.** It installs and launches, but its save state is reachable only
  through BigPEmu's own gamepad-driven overlay menu; no keyboard binding exists in RetroBat's
  config and an F1-F8 sweep produced nothing. Verifying it needs synthetic gamepad input.
- **Whether RetroBat mirrors openMSX states into the declared path.** Two real states landed
  in `bios/openmsx/savestates/` and the declared directory stayed empty, but both were made
  under openMSX's default state name rather than the `[guess_title]_0` name RetroBat's own
  hotkey uses. Settling it needs that hotkey to actually reach the emulator.
- **BizHawk's slot hotkeys.** Only `Quick Save` (`F2`, slot 0) was observed to write.
  Ctrl+F1 and Ctrl+F3, which `config.ini` binds to Save State 1 and 3, produced nothing and
  no cause was isolated, so the declared slot range 0-9 is confirmed only at slot 0.
- **Nothing on the second host.** Closed. It cannot launch a `.bat` because Notepad++'s
  installer replaced the `batfile` association, and cannot launch a `.ps1` because the
  execution policy is `Restricted`. An exe hook fires all four events there.
- **The Flycast per-game VMU on ports B, C and D.** The port-1 conversion is measured. Ports
  B to D produced no files in either run, so "only port 1 converts" is still read from the
  feature description rather than observed.

Two things measured here are worth re-checking rather than treating as permanent: the
FAT local-time round trip, which was clean on this machine's timezone and may not be
elsewhere, and both filed upstream bugs.

## Measured during the issue sweep after M6 stage 2b

`tools/m6-probes/m6-probe7-slot-overwrite.py` and `m6-probe8-leftover-row.py`, run against the
live instance at 5.1.1-beta.2 with `autocleanup` off throughout, on probe-only slots cleaned up
afterwards. Read alongside `backend/endpoints/saves.py` and `backend/endpoints/sync.py` at both
the `5.1.0` baseline tag and `5.1.1-beta.2`, which agree with each other and with the probes.

| #   | Previously                                                                                                                                                                  | The probe says                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 160 | `overwrite=true` replaces the row in the slot (plan, M6; **measurement 150**)                                                                                               | **It never replaces. Row identity is the datetime-tagged filename at one-second resolution, so the clock decides.** Two postings inside one second updated row 167 in place; the same pair a second apart made rows 167 and 168. `overwrite` is not part of row identity at all: it suppresses the 409 checks **and** the identical-content dedup. 150 read a same-second update as the general rule and is withdrawn                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| 161 | Identical content into one slot reuses the row unconditionally (F3, 148, and the stub)                                                                                      | **Only without `overwrite`.** The server guards that check with `not overwrite`, measured: identical bytes with `overwrite=true` made row 164 where the same bytes without it reused row 163. So a replayed flush is still free, because a flush never sends `overwrite`, and a repeated `--keep-local` is not                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| 162 | An unregistered `device_id` resolves to no device and skips the conflict checks                                                                                             | **It is a 404.** `overwrite=true` with `device_id=not-a-registered-device` answered 404 and wrote nothing, so a client must send a registered device id and cannot dodge the 409 path by omitting one                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| 163 | (not addressed) whether the row an append leaves behind can come back as a download                                                                                         | **It cannot. Negotiate only ever looks at the newest row per `(rom_id, slot)`.** `backend/endpoints/sync.py` folds the user's slotted saves to one row per slot by `updated_at` before matching anything, and both the client-submitted pass and the unsubmitted-slot pass iterate that fold, which the source comments as "superseded older rows per slot are history, not downloads". Identical at `5.1.0` and `5.1.1-beta.2`. **Driven by probe 8**, which built the shape a resolution leaves (device B's row 169, then device A's row 170 with `overwrite=true` a second later) and negotiated as device A: naming the slot answered `no_op (Content is identical)` on row 170 and never mentioned 169, and an empty `saves` array mentioned neither. So the copy a `--keep-local` leaves one row down is unreachable through the sync protocol, and refines 151 |
| 164 | (not addressed) whether a sync session can always be completed                                                                                                              | **No. A negotiate cancels the device's previous active session, and completing a cancelled one is a 400.** Probe 8's two negotiates left sessions 193 and 194: `/sessions/193/complete` answered **400** and 194 answered 200. `sync.py` cancels active sessions for the device before creating the new one, and `complete` refuses any status outside `PENDING`/`IN_PROGRESS` with `Session is already {status}`. So a client that negotiates twice without completing cannot tidy the first one up                                                                                                                                                                                                                                                                                                                                                                  |
| 165 | (not addressed) whether the client end of all this behaves as documented                                                                                                    | **It does, driven end to end on the real install at `K:\RetroBat` against the live instance.** Phantasy Star (Brazil), `libretro:battery`, rom 239719: the peer diverged the server side (row 171) and the local side was diverged independently, the flush reported a **conflict rather than an error** and copied the local file to `replaced/`, `--keep-local` **appended row 172 beside 171 rather than replacing it**, said "sent it as the newest copy in the slot", pruned the copy aside, and **the next flush answered `2 already in step` with the local file byte-identical**. So 163 holds for RomMBat itself and not only for raw HTTP                                                                                                                                                                                                                   |
| 166 | `bigpemu` declares `001`/`999` against a two-digit `{{slot2d}}`, so its own file is internally inconsistent (**#34**, the plan's stage 2a paragraph, the `save-sync` skill) | **Refuted. The two describe opposite sides of a mirror.** Driven on the real install: BigPEmu writes **three-digit** names in its own tree, `emulators/bigpemu/userdata/game4F7E323A69447A71_state001.bigpstate`, keyed by an internal game id; RetroBat mirrors each to `saves/jaguar/bigpemu/Rayman (World)_state01.bigpstate`, **two-digit and rom-named**, byte-identical in length. So `firstslot`/`lastslot` describe the emulator's native slot range and `<file>` describes the mirror, and neither contradicts the other. Six states driven through the gamepad overlay, slots 1 to 6; `SaveAutoIncr: 1` in `BigPEmuConfig.bigpcfg` is why they came out consecutively. RomMBat read all six correctly off the declared path. **Whether the mirror writes `_state100` past slot 99 is still unmeasured**, and reaching it needs ~94 more saves of one game   |
| 167 | (not addressed) whether everything `bigpemu` writes reaches `saves/`                                                                                                        | **It does not. The Jaguar battery save never leaves the emulator's own tree.** `game4F7E323A69447A71_eeprom.bigpeep`, 128 bytes, sits in `emulators/bigpemu/userdata/` with no counterpart anywhere under `saves/jaguar/`. Same class of trap as openMSX's states landing in `bios/openmsx/savestates/`: a client reading only the declared tree concludes the game has no battery save. `jaguar` is in `save_shapes.json`'s `_unclassified` list and this is the concrete reason it has to stay there until the native path is modelled                                                                                                                                                                                                                                                                                                                              |
| 168 | A `.txt` sidecar's presence and contents say nothing for emulators not yet driven (**145**, the `save-sync` skill)                                                          | **`bigpemu` writes one, and its contents are the mapping between the two naming schemes.** `saves/jaguar/bigpemu/Rayman (World).txt` holds `game4F7E323A69447A71`, which is exactly the key BigPEmu's native filenames use. So it is the same shape as PPSSPP's `ULES01513_1.00`: not a console serial, but a stable per-game identifier that joins the mirror back to the native tree. It carries no underscore, so the first-underscore split in `GameIdAttributor.FromSidecar` returns it whole                                                                                                                                                                                                                                                                                                                                                                    |

**What this changes.** `SaveConflictResolver.KeepLocalAsync` is the only caller of
`overwrite=true`, so a resolution appends a row and leaves the server's copy one row down.
That is untidy rather than lossy, and 163 is the part that decides it: the row left behind is
no longer the newest in its slot, so negotiate stops mentioning it entirely and cannot offer
the rejected copy back as a download. `autocleanup=true&autocleanup_limit=10` on every upload
then bounds the history. Without 163 this paragraph was reasoning rather than measurement, and
it is reasoning of the same kind that produced 150. The command's own success message said it
"replaced the server's copy" and no longer does. The stub modelled the replacement, so no test
in the suite could see the append.

**Taken by hand, once, on the real install.** Measurement 165 is the only place any of this has
been driven through `SaveConflictResolver` and `SaveSync` against a live server rather than
through the stub or through raw HTTP, and it is what closes the gap between "the server appends
and never re-offers" and "the product does the right thing with that". **It is not a
certification**: one shape (class A), one system, one emulator, and the diverged local save was
written by editing the file in place rather than by a game launch, because the launch exercises
save detection rather than conflict resolution and nothing in that path changed.

**What remains a dependency rather than a gap.** `KeepLocalAsync` records a sync record for the
row it wrote and never acks the row it superseded, so that row keeps a permanent hole in this
device's sync history. 163 makes the hole unreachable rather than filling it, driven rather than
reasoned, and that stays a dependency on server behaviour rather than on anything this client
controls: it holds while negotiate pairs on the newest row per slot, and the mechanism #53 set
out would fire exactly as written if that ever stopped.

## Measured during M6 stage 2c

Read-only sweeps of the real install at `E:\RetroBat` plus one accidental observation, before
any probe wrote anything. `tools/m6-probes/m6-probe9-es-settings-two-writer.py` was then driven on
`K:\RetroBat` with EmulationStation running, which is findings 178 and 179 and the one result
here that changed the design; the install was restored byte for byte afterwards.
`m6-redact-es-settings.py` produced the checked-in fixture.

| #   | The claim being checked                                                                                                                                                                 | What was measured                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 169 | (not addressed) what an `es_settings.cfg` holds                                                                                                                                         | **Plaintext credentials, and this constrains what may ever be logged or checked in.** A real install's file carries `ScreenScraperPass`, `global.retroachievements.password`, `global.retroachievements.token` and `IGDBSecret` in clear, plus the user's name under `ScreenScraperUser`, `global.retroachievements.username` and `global.netplay.nickname`. So no capture may be checked in unredacted, and nothing RomMBat writes to a log, a report or a probe transcript may echo a value read out of it                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| 170 | ES prunes a setting whose value equals its own default, measured on `Language=en_US` (**M0, probe 2**)                                                                                  | **Incomplete as stated: the same key came back on its own, at the same value.** `Language` was **absent** on 2026-08-23 and ES had **added `Language=en_US`** by 06:32 on 2026-08-24, with nothing else added, nothing dropped and no other value changed, on an install nobody was experimenting with. So `en_US` is either not the default or the pruning is not a plain equals-default test. The rule the code needs is stronger and simpler: **presence is not evidence either.** A key can appear without the user doing anything, so "the key holds the stock value" must not be read as "the user chose this"                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| 171 | The bundled `shared_containers` declarations are reachable (`save_rules.json`, `SaveDiscoveryTests`)                                                                                    | **Seven of ten never were.** `SharedContainerReason` had one caller, asking with a bare filename from a non-recursive enumeration, so the seven declarations naming a path with a separator (`ps2/pcsx2/memcards/Mcd00{1,2}.ps2`, dreamcast's four VMUs, `saturn/kronos/bkram.bin`) could not match. The test covering it called the lookup table rather than the scanner, and passed. The shared PS2 cards were being counted inside an unread `pcsx2/` subdirectory instead of named                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| 172 | (not addressed) how a real PS2 library splits between single-disc and multi-disc                                                                                                        | **302 single-disc titles against 7 two-disc sets, with no `.m3u` anywhere and no per-game folders.** So loose sibling files are the only layout the conversion refusal has to read, and RetroBat's `ps2` listing `.m3u` in `<extension>` neither binds a set nor means anything by its absence, which matches its own wiki saying PCSX2 cannot use one                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| 173 | (not addressed) how a disc marker is written                                                                                                                                            | **`(Disc N)`, always, N numeric, 202 files across `psx`, `saturn`, `dreamcast`, `gamecube`, `3do` and `ps2`.** No `(Disk`, `(CD` or `(Side` appears. **53 of the 202 carry text after the marker** (`(Rev 1)`, `(Unl)`, translation tags), the filename-side twin of the 130 subtitled `gamedb` stems in F18, so a set's base title is the text **before** the marker and never the stem with the marker cut out of its middle                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| 174 | All four class D options and `dolphin_sync_saves` are unset on a stock install (**probe 2**)                                                                                            | **Still true, re-read on a heavily used install.** None of `pcsx2_slot1_memory`, `duckstation_memcardtype`, `dolphin_slotA`, `flycast_vmupergame` or `dolphin_sync_saves` appears in the 261-setting file, and no per-game `[&quot;` key of any kind does. `ps2.emulator` is `pcsx2`, so `(ps2, pcsx2)` is the pair to build for                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| 175 | A PS2 launch rewrites both shared memory cards, and an empty card is the same size as a real one (**the `save-sync` skill, F18**)                                                       | **Both reproduced on PS2, having been measured on PS1.** `Mcd001.ps2` and `Mcd002.ps2` are each 8,650,752 B with identical mtimes to the second, and their byte histograms are **256 distinct values against 76**. So mtime cannot decide whether either changed, size cannot tell a formatted empty card from one holding a save, and only the contents separate them                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| 176 | A `.txt` sidecar holds the emulator's native basename, and DuckStation's holds a bare serial (**145, 168**)                                                                             | **PCSX2's is a third format: the serial plus a CRC.** `saves/ps2/pcsx2/Armored Core 3 (USA).txt` holds `SLUS-20435 (FDB4D261)`, and the two others on the install match its shape. So it is neither the bare serial DuckStation writes nor the underscore-joined form PPSSPP writes, and `GameIdAttributor.FromSidecar`'s first-underscore split leaves it whole rather than parsing it. Relevant to **#37**, and not on stage 2c's path, because a converted PS2 card is rom-named and attributes by filename                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| 177 | (not addressed) whether the `K:` test stick can stand in for the real install on PS2                                                                                                    | **It cannot, today.** `K:\RetroBat` holds **no PS2 ROM, no PCSX2 binary** (`emulators/pcsx2` is `inis/` and `portable.ini` only) **and no PS2 BIOS**, against `E:`'s 319 ROMs, full PCSX2 and six `scph*` images. RetroBat downloads emulators on demand and an uninstalled one raises a modal dialog with no title and no timeout, so standing `K:` up is three steps with a known indefinite hang among them. **The two-writer probe needs none of that** and belongs on `K:`, which has EmulationStation and a real 56-setting file                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| 178 | ES keeps keys it does not recognise, so the per-game override is durable and the hazard is ordinary two-writer contention (**M0 probe 2, `docs/PLAN.md`, the `retrobat-layout` skill**) | **Refuted, and it moves the design. A key written while ES is running does not survive.** Driven on `K:` with EmulationStation up: two custom keys were merged in atomically and confirmed on disk in escaped form, and ES's next write discarded both. **`Language` is the proof it is not a merge**, because ES added that key itself at startup and dropped it again on the same write, so what ES serialises is a model loaded at boot and not the file as it stands. The rule covering this and M0 together is **ES loads `es_settings.cfg` at startup and serialises that model on every write: a key present at load survives, including one ES cannot understand, and a key that appears afterwards is discarded.** M0's nonsense key survived because it was written before ES started. So "write while ES is idle" is not prudence, it is the only thing that works, and merging plus atomicity do not help: this write was both and was still discarded                                                                                                                                                                                                         |
| 179 | ES writes `es_settings.cfg` on exit, only when a setting changed that session (**M0 probe 2, finding 33**)                                                                              | **There are two writes per session, not one: the file is also rewritten during launch.** Timed against the hook spool, which stamps ES's own `start` and `quit` events to the millisecond. The launch write landed **7.7 s before the `start` hook fired** and added `Language`, merging into a file it had just read; the same key appeared unprompted on a second install the same morning. The session's other write landed **2.4 s before the `quit` hook**, so it is the exit write M0 described, confirmed rather than replaced. **What is not established, and an earlier revision of this row wrongly claimed, is that a mid-session setting change triggers its own write**: the toggle and the quit were not separated in time, so the one observed write is equally explained by either, and nothing here distinguishes them. The design consequence is unchanged either way, because an exit write alone is enough to discard anything written underneath it.                                                                                                                                                                                                  |
| 180 | RomM's `sha1_hash` describes the bytes it serves, so a downloaded file can be verified against it (**M3, `ContentHasher`, finding 85**)                                                 | **Not on this instance's PS2 `.chd` files, and the client is right to refuse them.** Syncing `Armored Core 3 (USA).chd` (rom 191723, 974,163,943 B) downloaded all 929 MB and then failed with "the downloaded file does not match the sha1 the server reported", leaving no `.part` and no rom, which is the verify-then-commit rule working. The server's metadata is what is wrong: two 1 MB `Range` requests, at offset 0 and at `size - 1 MB`, came back **byte-identical to the copy on the real install**, and that copy's sha1 is `0dd306bc…` against the `a5d460d3…` the API reports. So RomM serves one file and records the hash of another. **Not an outlier**: `Gauntlet - Dark Legacy (USA).chd` (rom 192797) mismatches the same way at the same exact size. Nothing here is a client defect and nothing in M6 stage 2c touches it, but it makes the shipped adopt-and-verify path unusable for this library's PS2 titles, so the stage's hands-on pass registered its ROM row by hand and said so                                                                                                                                                          |
| 181 | A rom carrying no md5 reports it as null, so `nothingToCompare` reaches the size fallback (**`ContentPlanner`**)                                                                        | **It reports an empty string.** Rom 191723 comes back with `md5_hash: ''` and `crc_hash: ''` rather than null, with only `sha1_hash` populated. `ContentPlanner.nothingToCompare` tests `member.Md5Hash is null && member.Sha1Hash is null`, and an empty string is not null, so for a rom whose hashes are **all** empty strings the size-only adoption path is unreachable and the file is re-downloaded and re-refused on every sync. Not reached here, because this rom's sha1 is populated, and not investigated further because it is M3's code rather than this stage's. Finding 85 measured that 9% of a real library carries no md5, so how RomM represents that absence decides whether that 9% is adoptable                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| 182 | (not addressed) what a converted PCSX2 memory card is called                                                                                                                            | **`<rom stem>.ps2`, so the extension is replaced and not appended.** Driven end to end: `ps2["Armored Core 3 (USA).chd"].pcsx2_slot1_memory=game` produced `saves/ps2/pcsx2/memcards/Armored Core 3 (USA).ps2`, 8,650,752 B. **Two naming rules are in play at once and confusing them is the whole trap**: the `es_settings.cfg` key must carry `.chd` or it is ignored silently (M0 cases E and F), while the card PCSX2 writes drops it. The consequence is the good one: the stem `Armored Core 3 (USA)` is exactly the `(folder, stem)` key class A attribution already uses, so a converted card resolves through the existing `RomIndex` with no new route, which is the claim the whole PS2 story rested on. It lands **three levels deep**, under `saves/ps2/pcsx2/memcards/`, where class A discovery only reads files loose directly under `saves/<system>/`                                                                                                                                                                                                                                                                                                    |
| 183 | A PS2 launch rewrites both shared memory cards, so mtime cannot decide whether one changed (**175, the `save-sync` skill**)                                                             | **True while the game is using them, and a converted game leaves the shared card completely alone.** After the converted launch, `Mcd001.ps2` was **untouched**: mtime still the moment it was copied, md5 unchanged at `d3334798…`. So the conversion really does redirect the writes, the stranded save really is stranded, and the warning's wording is honest rather than cautious. The new card holds **exactly one game's saves**, `SLUS-20435` with 4 entries, against `Mcd001`'s 11 games, which is the class D attribution problem solved and measured rather than argued                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| 184 | (not addressed) what `pcsx2_slot1_memory=game` does to console slot 2                                                                                                                   | **Slot 2 stays shared, exactly as the option name says, and it moved its mtime without changing a byte.** `Mcd002.ps2` was written at `08:28:22.0017`, the same instant as the new card, and its md5 is unchanged at `96cebf28…`, 76 distinct byte values, no game serial in it. So it is F18's formatted-empty card reproduced on PS2, and it is a fresh demonstration on the converted path that **mtime moves while content does not**, which is why every save is content-hashed                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| 185 | (not addressed) what the ES menu shows for a feature RomMBat has overridden per game                                                                                                    | **"Auto", which is EmulationStation's own name for the key being absent, and the per-game override is invisible there.** `es_features.cfg` declares three choices for `pcsx2_slot1_memory` (`standard`, `folder`, `game`) and no `auto`; across the whole file only 3 of 10,724 choice entries declare one explicitly, so ES synthesises AUTO for any unset feature, and `switchauto`/`sliderauto` are the same idea for switches and sliders. **Two consequences.** Reverting a conversion to `prior_state = 'absent'` returns the menu to Auto, which is where the user actually was; writing the stock value instead would leave them somewhere visibly different, which is what migration 010's two-column prior state exists to prevent. And after a conversion the system-scoped menu still reads **Auto** while the per-game key silently outranks it, so a user checking the menu sees no sign that one of their games has been converted. **`folder`, the third choice, is not used and not measured**: whether it is per-game or merely a different container format for a still-shared card is unknown here, and `game` is the one M0 read and this stage drove |
| 186 | (not addressed) whether eviction really refuses a ROM whose converted card holds an unsent save                                                                                         | **It does, driven on hardware under real budget pressure, and it fails closed rather than pretending to succeed.** With the budget 485.5 MB over and a save the player had just made, `evict` held Armored Core 3 back with `1 save file for this game on disk has not reached the server yet`, evicted two unrelated games, and reported **"2 held back (still short)"** rather than reporting success at freeing enough. After one flush the same command offered the same ROM for eviction, so both directions are proven and the guard is not blocking spuriously. The same flush also sent **1 play session**, so the hooks caught the launch alongside                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| 187 | (not addressed) whether reverting a conversion really restores the file                                                                                                                 | **It does, and the only thing that moves afterwards is ES's own bookkeeping.** Against a byte copy taken before the conversion: **57 settings before, 57 after the revert, nothing added, nothing dropped, no `pcsx2_slot1_memory` and no RomMBat key left**. The file's md5 differs, and the whole difference is `LastSystem`, which ES rewrites to record where the user was in the UI. So a byte comparison is the wrong assertion for this file and a **setting-set comparison is the right one**, which is the same lesson M4 learned about `gamelist.xml`: compare what the writer owns, not the bytes a second writer also touches. The `absent` prior state was honoured, the key being removed rather than written at a stock value                                                                                                                                                                                                                                                                                                                                                                                                                               |
| 188 | (not addressed) what happens to the per-game card after a revert                                                                                                                        | **It stays on disk, keeps its `local_save` row, and goes on syncing, which is the decision rather than an oversight.** After the revert the row is still there, still class D, still in step, and the conversion record is deleted with no tombstone. So progress made while converted is never orphaned by un-converting, and the user is told plainly that the game will no longer read it. A re-sync afterwards is a clean no-op: `nothing to do`, 0 downloaded, 0 written, gamelists unchanged, and a flush moves nothing                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |

---

## Measured during the Dolphin and hook-spawn pass, after M6 stage 2c

Two things the stage 2c ledger recorded as owed: `dolphin_sync_saves` detection, agreed in ruling 5
and not delivered, and the hook-spawn cost, deferred four times with the reason recorded each time.
Both are taken here.

`tools/m6-probes/m6-probe10-hook-spawn-cost.py` times process starts against a scratch tree beside
the install. `m6-probe11-dolphin-sync-saves.py` toggles one key and snapshots the save tree, and it
is the second probe in the repository that writes into a real RetroBat configuration.
**Three launches on `K:` with Spinnich**, on a Samsung USB flash drive, which is the slowest medium
RomMBat ships to and so the honest one for a launch-path measurement.

| #   | The claim being checked                                                                                                                                                                                                                                     | What was measured                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 189 | `dolphin_sync_saves` is RetroBat copying save files between the dolphin and libretro-dolphin folders **on its own schedule**, and must be detected before either location is trusted (**findings 9c and 123, `docs/PLAN.md`, the `retrobat-layout` skill**) | **Wrong in all three parts, and the code was going to be built against it.** Read from `emulatorlauncher`, `Dolphin.Generator.cs`: it is **GameCube only** (declared twice in `es_features.cfg`, both under `gamecube`, and the `wii` branch never calls `SyncGCSaves`), it runs **once per launch inside emulatorlauncher before Dolphin starts** rather than on any schedule, and the two locations are **`GC/<REGION>/` and its own `Card A/` subdirectory**, not two emulator folders. Nothing moves while RomMBat is running, which is what makes it detectable at all                                                                                                                                                                                |
| 190 | (not addressed) what `Card A` holds once the option is on                                                                                                                                                                                                   | **The region root as it was before the launch, so one session behind.** Driven on `K:`: with the option on, launching wrote `Card A/41-G3SE-BUST A MOVE 3000.gci` at md5 `6242a2ff`, the previous session's save, and Dolphin then wrote `6bca9b1a` over the region root. `Card A` is a snapshot taken by emulatorlauncher, not a mirror kept in step                                                                                                                                                                                                                                                                                                                                                                                                      |
| 191 | (not addressed) whether a save RomMBat removes stays removed while the option is on                                                                                                                                                                         | **No, and what replaces it is stale.** The region-root `.gci` was deleted, as a transfer dropping a member does, and the next launch **copied it back out of `Card A`** holding `6242a2ff` rather than the `6bca9b1a` that was removed. The whole trace is one line, `[INFO] GameCube saves have been synced.` This is the one-sided branch of `SyncGCSaves`, and it is the real hazard: the mtime branch cannot bite, because a save RomMBat restores is written with the current time and always wins                                                                                                                                                                                                                                                    |
| 192 | `Card A` and the `.old` files a reconciliation leaves would be double-counted by class C discovery                                                                                                                                                          | **Neither can be, by construction rather than by intent.** `SaveUnitScanner.SafeFiles` is `Directory.EnumerateFiles(path)` with no `SearchOption`, so a `Card A` subdirectory is invisible, and `X.gci.old` fails `KeyOf` exactly as `X.gci.deleted` already does. The existing code fails closed here, so what it needed was the report and not a fix                                                                                                                                                                                                                                                                                                                                                                                                     |
| 193 | GameCube is class C (`save_shapes.json`)                                                                                                                                                                                                                    | **Class C in slot A only, and only at the default.** `dolphin_slotA` is labelled **SAVE FORMAT** in the ES menu, `GCI FOLDER` (8) against `MEMORY CARD` (1); at 1 the container becomes one shared raw `SRAM.<REGION>.raw`, which is class D, the inverse of what conversion does for PS2. **Slot B is worse: RetroBat never rewrites it**, leaving Dolphin's stock relative default, so `SlotB = 1` points at top-level `saves/dolphin/User/GC/SRAM.EUR.raw`, which Dolphin then region-substitutes. A 16 MB `SRAM.USA.raw` appeared there during a GameCube launch on `K:`, and `E:` has carried one since August. Both are outside every container `save_shapes.json` declares, and `NANDRootPath` points into the same tree even for a GameCube launch |
| 194 | (not addressed) whether the Game-ID launch-window correlation attributes a real save on hardware                                                                                                                                                            | **Yes, and this is its first proof.** A `.gci` is named `41-G3SE-BUST A MOVE 3000.gci` and carries a game code rather than a rom filename, so nothing else could attribute it. The binding was learned unprompted: `gamecube/G3SE` to `roms/gamecube/Bust-A-Move 3000 (USA).rvz`, `learned_from=journal`, detail _was running when G3SE was last written (16:18:30Z against 16:19:19Z)_. Discovered, attributed, bundled and uploaded as save 181 in one pass                                                                                                                                                                                                                                                                                              |
| 195 | Having a hook spawn the agent puts an **11 MB** process start in the game-launch path, against **75.5 MB** for the agent, and the size is the cost to measure (**`docs/PLAN.md`, `docs/ARCHITECTURE.md`, the `offline-and-portable` skill**)                | **Backwards. Size is not the cost.** On the USB stick, 31 interleaved runs each: the **75.9 MB agent reaches `Main` in 34.0 ms**, the **11.0 MB hook takes 59.8 ms to start and 111.3 ms to finish**. `PublishTrimmed` rewrites the framework assemblies and discards the precompiled native code they ship with, so a trimmed app carrying no R2R of its own JITs everything from IL at every start. The same `File.Move` costs **51.5 ms** in the shipped build against **7.0 ms** with `PublishReadyToRun`, in one loop on one stick, so the gap is JIT and not disk. Adding R2R costs **1.8 MB a copy**, 7.2 MB across the four installs, and takes one invocation from **111 ms to 49 ms**                                                            |
| 196 | `EnableCompressionInSingleFile` is what costs the hook its start time (**this session's own first guess**)                                                                                                                                                  | **No, and it is worth recording as a wrong guess a measurement caught.** Compression costs **4 ms** and saves **1.74 MB** (11,017,491 B against 12,752,849 B), so it is kept. The untrimmed build settles it from the other side: at 37.6 MB it does the spool write in **9.3 ms**, near R2R's 7.0 ms and nothing like the shipped build's 51.5 ms                                                                                                                                                                                                                                                                                                                                                                                                         |
| 197 | The ES hooks run inside the game-launch path, so what they cost delays a launch (**`docs/PLAN.md`, `docs/ARCHITECTURE.md`**)                                                                                                                                | **They run during it, not inside it, and nothing waits for them.** No probe was needed, because 23 launches were already on disk. Joining each `game-start` journal record to `emulatorLauncher.log`'s millisecond `[Startup]` stamp gives a **median of +24 ms**, 20 of 23 between 12 and 44 ms. The hook's own start is ~60 ms, so ES spawned it ~36 ms _before_ emulatorlauncher began and did not block on it, and emulatorlauncher then took **0.5 s to 2.8 s** to reach `[Running]`. The cost of a spawn is contention, not latency. **CLAUDE.md rule 4 is untouched by this**: it forbids the hook touching the network, which was never a cost argument                                                                                            |
| 198 | (not addressed) whether a save this device uploaded is restored if it goes missing locally                                                                                                                                                                  | **It is not.** With the GameCube unit deleted and its row forgotten, a flush planned no download and the region root stayed empty; the save had to be fetched from `/api/saves/{id}/content` by hand. So the two halves compound: RetroBat puts back a stale copy RomMBat cannot see, and the good copy on the server is unreachable to this device. This is the ledger's `IsOwnUpload` question, measured rather than inferred                                                                                                                                                                                                                                                                                                                            |
| 199 | `Language` appears on its own, so presence is not evidence of authorship (**finding 170**)                                                                                                                                                                  | **Third corroboration, on a third occasion.** Across the three GameCube launches ES added `Language=en_US` and changed nothing else: 56 settings before, 57 after. The `gamecube.dolphin_sync_saves` key written while ES was down survived all three sessions untouched, which is finding 179's rule holding in the direction the writer depends on                                                                                                                                                                                                                                                                                                                                                                                                       |

---

## Measured during M7 stage 7a

Two probes, both against `K:` and both authorised: `tools/m7-probes/probe1-quit-ordering.ps1`
times what happens between `GET /quit` and EmulationStation actually being gone, over three
sessions; `probe2-menu-reload.ps1` registers a `.menu` entry under a **running** ES and times how
long it takes to appear in ES's own model. Both revert everything they write.

Probe 1 forces the exit write rather than hoping for one: it sets `LastSystem` to a bogus value
before each session, so ES has a changed setting and therefore a reason to write on exit
(finding 33). All three sessions produced a write.

| #   | The claim being checked                                                                                                                                            | What was measured                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 200 | The `quit` hook could fire before ES writes `es_settings.cfg`, which would make polling for the process merely tidy rather than necessary (**this stage's brief**) | **The write comes first, every time, and the gap is sub-second rather than finding 179's 2.4 s.** Three sessions, timed from `GET /quit`: the file's mtime moved at **175.6 / 324.8 / 324.1 ms**, the `quit` hook stamped itself at **807.3 / 524.8 / 551.6 ms**, and the process was gone at **875.5 / 573.1 / 604.0 ms**. So ES writes, then fires the hook **200 to 630 ms** later, then exits. **Nothing touched the file again**, neither before the exit nor in the three seconds sampled after it, at a 25 ms sample interval                     |
| 201 | (not addressed) how long `background quit` has to wait for ES to be gone once the hook has fired                                                                   | **48.3 to 68.2 ms**, three of three. The poll is cheap, which is the result that lets the pass poll rather than guess. It is still the only correct order: the hook fires while ES is alive, and a key written in that window is inside the load-and-serialise window finding 178 measured, so the write waits for the process rather than for the hook                                                                                                                                                                                                  |
| 202 | ES's launch write lands before the `start` hook, so `start` is inside the discard window (**finding 179, one session**)                                            | **Corroborated on three more.** Reading the file's mtime at the first sample of each session against the `start` hook's own stamp: the launch write preceded the hook by **4.9 / 1.6 / 1.7 s**. So `background start` must never write `es_settings.cfg`: by the time it runs, ES has already loaded its model _and_ already written the file once                                                                                                                                                                                                       |
| 203 | `es_menu` is an ordinary ES system, so `/reloadgames` ought to pick a new `.menu` up (**probe 4, reasoned rather than driven**)                                    | **It does, and so does the gamelist entry, with no restart.** With ES up: writing `zzprobe7a.menu` alone took `retrobat` from **92 to 93 games in 209 ms**, listed under its bare filename with no image. Adding the `<game>` element and reloading again took **262 ms** for the name to become `RomMBat probe` and for ES to serve an image URL for it. So `sync` can tell the user the entry is ready rather than telling them to restart the front end                                                                                               |
| 204 | `system/es_menu/gamelist.xml` is a gamelist like any other, so RomMBat's writer can merge into it (**this stage's brief**)                                         | **Its encoding is not like any other, and merging with the shipped writer would rewrite all 96 entries.** The stock file is **UTF-8 with a BOM and CRLF endings**; `GamelistDocument` writes no BOM and LF. Against **42 of 42** `roms/<system>/gamelist.xml` across both installs, which are no BOM and LF, this one file is the exception. `GamelistDocument` now records the convention of the file it loaded and reproduces it                                                                                                                       |
| 205 | `system/es_menu/gamelist.xml` was not rewritten by ES across two sessions, which is an absence of evidence rather than a guarantee (**probe 7**)                   | **Third session, and the strongest one: ES had the change in its model and still left the file alone.** The entry was written, `/reloadgames` was called, ES listed it by name, and after `/quit` the file's **md5 and mtime were both unchanged** from what the probe had written. Probe 7's two sessions did not include a session where ES had a reason                                                                                                                                                                                               |
| 206 | ES rewrote that gamelist on exit, stripping the BOM and reindenting to two spaces (**this session's own first reading**)                                           | **No, and it is worth recording as a wrong guess a measurement caught, the way 196 was.** The rewrite was the probe's own `XmlDocument.Save`, which defaults to CRLF and two-space indentation. The first run compared the post-quit file against the **shipped** one rather than against the bytes the probe itself had just written, so the probe's own write was the only thing the comparison could ever have shown. Retracted, and the probe now hashes the file immediately after writing it                                                       |
| 207 | (not addressed) what a gamelist merge into `es_menu` must preserve beyond the entries                                                                              | **Three `<game>` elements in the stock file are commented out**, `citra_canary`, `yuzu-early-access` and `zsnes-dos`, which is how RetroBat disables an entry it still ships the markup for. Read together with 205, RomMBat is the **only** writer that could drop them, so the comment preservation `GamelistDocument` already has stops being incidental here. It also explains a count that looks wrong: the file holds 96 `<path>` elements and 93 live `<game>` entries, and ES reports 92 because four entries name a `.menu` that is not on disk |

### The hands-on pass for 7a

Two full EmulationStation sessions on `K:`, started by launching `RetroBat.exe` and ended
through `/quit`, with **no RomMBat command run between the setup sync and the verification**.

| #   | The claim being checked                                                                                                         | What was measured                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| --- | ------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 208 | `POST /launch` launches a game, the rom path as the raw request body (**probe 3, `docs/PLAN.md`, the `retrobat-layout` skill**) | **It answers 200 and does nothing, so it belongs with `/quit`, `/emukill` and `/reloadgames` rather than apart from them.** Probe 3 recorded it "confirmed from the API's own page at `/`", which is documentation rather than a drive. Driven now, twice, with the exact path `/systems/mastersystem/games` reports and with an explicit `text/plain` content type: 200, an empty body, and **`emulatorLauncher.log` did not grow by one byte** and no emulator process appeared. So the API cannot start a game, and a hands-on pass covering `game-start` and `game-end` needs a person at the controller |
| 209 | (not addressed) whether the hooks close the loop unattended on real hardware                                                    | **Yes for `start` and `quit`.** Two sessions: the `start` hook spawned a pass that reached the server and finished with exit 0 (`background start started` 21:51:50Z, `finished, flush exit 0` 21:51:54Z), and the `quit` hook did the same. The spool was drained into the journal both times without a terminal                                                                                                                                                                                                                                                                                            |
| 210 | (not addressed) whether an apply-at-quit change waits for the quit                                                              | **It waits, and it applies.** `pcsx2_slot1_memory` was queued while ES was down, was **absent from `es_settings.cfg` ten seconds after the `start` hook fired**, and was written at the quit: _EmulationStation gone after 10 ms, applying 1 queued change(s)_, then _Applied - set ps2["Armored Core 3 (USA).chd"].pcsx2_slot1_memory = game_. The row is stamped `applied` and `save_conversion` recorded `prior_state=absent`, so it is reversible                                                                                                                                                        |
| 211 | (not addressed) how long the quit pass waits for ES in practice                                                                 | **10 ms**, one poll, on the first real session. Consistent with finding 201's 48 to 68 ms, which was measured from `GET /quit` rather than from the moment the pass starts polling                                                                                                                                                                                                                                                                                                                                                                                                                           |

**The pass was completed with Spinnich at the controller**, since finding 208 makes a scripted
game launch impossible. Phantasy Star (Brazil) on `mastersystem`, libretro, class A.

| #   | The claim being checked                                                                                               | What was measured                                                                                                                                                                                                                                                                                                                                      |
| --- | --------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 212 | (not addressed) whether a hook-spawned pass carries a real play session and a real save to RomM with no terminal used | **Both, in one session.** The journal took `start` 22:17:07.7Z, `game-start` 22:17:16.0Z carrying `roms/mastersystem/Phantasy Star (Brazil).zip`, `game-end` 22:18:39.4Z and `quit` 22:18:42.6Z. The play session went up as outbox 24, `libretro:battery`, state `sent`. The save went up as **save 183**, and the server's hash equals the local one |
| 213 | (not addressed) whether the emulator's write really lands inside the window the `quit` pass then reads                | **Yes, with one second to spare.** RetroArch wrote the 32,768-byte `.srm` at **22:18:38.7Z**, `game-end` fired at 22:18:39.4Z, the `quit` hook at 22:18:42.6Z, the scan saw the new bytes at 22:18:43.1Z and the upload completed at 22:18:47.1Z. Nine seconds from the emulator closing to the save being on the server, unattended                   |
| 214 | (not addressed) whether the save that went up is the player's rather than a stale buffer being rewritten              | **The player's, and three hashes prove it rather than one.** The server held save 172 at `1177b02d`, the local file before the session was `338dd456`, and after it `391ecabd`, which is what uploaded. A RetroArch exit that merely flushed what it loaded would have reproduced `338dd456`, so SRAM changed during play                              |

## Measured while mining Argosy

One probe, against the development install, read-only: nothing was written and no game was
launched. It reads the generated `retroarch.cfg` and the save states already on disk. The lead it
was answering came from Argosy's `docs/save-id-to-path.md`; the ledger is
[argosy-findings.md](argosy-findings.md), A7.

| #   | Question                                                                                                    | Measured                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| --- | ----------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 215 | (not addressed) whether RetroBat ever leaves `sort_savestates_enable` unset, whose absent default is **on** | **Never.** `emulatorlauncher` writes all four sort keys explicitly as `"false"` on every launch (`sort_savefiles_enable`, `sort_savefiles_by_content_enable`, `sort_savestates_enable`, `sort_savestates_by_content_enable`) and puts the core in the path instead: `savestate_directory = "<root>\saves\mastersystem\libretro.genesis_plus_gx"`. So the asymmetric default that misplaces a state on other front ends (savestates on, savefiles off) cannot arise here, and nothing in RomMBat needs to read that key |
| 216 | (not addressed) what the on-disk core folder is actually named                                              | **`libretro.<core>`**, RetroBat's own convention, not the libretro `corename` a front end reading `retroarch.cfg` would produce. Four cores on disk agree: `mastersystem/libretro.genesis_plus_gx`, `mastersystem/libretro.picodrive`, `psx/libretro.mednafen_psx_hw`, `ports/libretro.2048`. This is what `es_savestates.cfg` already declares as `{{system}}/libretro.{{core}}`, so the manifest was the right source and `retroarch.cfg` never needed reading                                                       |
| 217 | (not addressed) whether `retroarch.cfg` is safe to read as a description of the install                     | **No, it describes only the last game launched.** The copy read named `mastersystem` throughout, including in `savefile_directory`, because that was the last session. It is regenerated per launch, which is rule 2 seen from the reading side rather than the writing side                                                                                                                                                                                                                                           |

## Measured in M7b, the gamepad UI

A real Avalonia window, opened from the ES menu entry 7a installed, with the 8BitDo pad
Spinnich actually uses. Two probes: an input sweep on the dev box, and one full
EmulationStation session on `K:` at **8.2.1**. To make 219 observable at all, the probe
created `.emulationstation/scripts/game-selected/` and `system-selected/`, which ES fires
events for and ships no folder for; both were removed afterwards.

| #   | Question                                                                                                           | Measured                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| --- | ------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 218 | (not addressed) whether `es_input.cfg` is enough to drive a UI, or whether a controller layout has to be detected  | **Enough, and detection is the wrong shape.** The file is semantic: it records which physical input is `a` on each pad, not what kind of pad it is. All 21 names in the 8BitDo's config resolved from live hardware through the shipped parser. Two of them cannot be expressed by a vendor-id table at all: `select` and `hotkey` are the **same button** (`button6`), and the Switch Pro reports its d-pad as **buttons 11-14** where the 8BitDo and Xbox report a **hat**                                                                                                                                                                                            |
| 219 | (not addressed) whether EmulationStation keeps reading the pad while a full-screen app is in front of it           | **It does not, and nothing on our side is needed.** Instrumented with a stamping hook on `game-selected`: ES logged 63 navigation events up to 23:16:17.984, then **zero** for the 26.5 s RomMBat was in front, while five D-pad presses landed in RomMBat at 23:16:27.7 to 23:16:32.1. ES resumed 0.64 s after our exit and re-announced the unchanged selection. A `.menu` app is suspended exactly as a game is                                                                                                                                                                                                                                                      |
| 220 | (not addressed) what `emulatorLauncher` hands a `.menu` app, and whether PadToKey competes for the pad             | **Nothing, and it does not.** A real ES-menu launch carries **no `-p1*` controller arguments**, where a game launch carries `-p1index`, `-p1guid`, `-p1path`, `-p1name` and the button/axis/hat counts. So `[SdlGameController] connected` and `[PadToKey] Add joystick` / `Start listening` never appear: PadToKey loads `es_padtokey.cfg` and attaches to nothing. The app is launched argument-free (`[Running] ...\RomMBat.exe`) and **has the controller to itself**                                                                                                                                                                                               |
| 221 | **finding 20 and the M0 section above**: an ES-menu launch fires `game-end` with no preceding `game-start`         | **Not for a launch that succeeds.** A real UI-driven launch fired **both**: `game-start` at 23:16:19.997Z carrying `system/es_menu/rommbat.menu`, and `game-end` at 23:16:44.497Z carrying nothing. M0 measured three launches driven by invoking `emulatorLauncher.exe` **directly**, two of which failed (`path is null`, exit 204). The consequence is unchanged and the code was already right: the discard keys on `IsMenuLaunch` from the launcher log and discards the paired `game-start` too                                                                                                                                                                   |
| 222 | (not addressed) whether RomMBat's own launch becomes a play session, for real rather than in a fixture             | **It does not.** Both journal rows closed `discarded`, the outbox gained nothing, and its newest `play_session` is still 7a's Master System one at 2026-08-24T22:18:42Z. The `.menu` path was journalled **relative** (`system/es_menu/rommbat.menu`), so rule 1 held at the hook boundary                                                                                                                                                                                                                                                                                                                                                                              |
| 223 | (not addressed) an analog trigger's resting value                                                                  | **`-32768`, not zero, on the same pad whose sticks rest at zero.** L2 and R2 settle fully negative after release and stay there. So "any non-zero axis reading is an input" is wrong: an axis binding names a **direction**, and only a reading of that sign is that input. A naive reader reports both triggers permanently held                                                                                                                                                                                                                                                                                                                                       |
| 224 | (not addressed) what an unreachable server costs through the interface, rather than through a handler in isolation | **2046, 2002 and 2004 ms**, driven three times against `192.0.2.1:8080` through `ServerProbes.TryContactAsync`, which is the call the pairing screen makes. So M0 experiment 6's 2 s budget holds end to end and is not just a property of the handler. The address matters: it is TEST-NET-1 from RFC 5737 and routes nowhere, where a made-up **hostname** fails at DNS in milliseconds and never exercises the connect timeout at all                                                                                                                                                                                                                                |
| 225 | (not addressed) whether `es_input.cfg`'s face-button names match the labels printed on the pad                     | **`a` and `b` do; `x` and `y` are the other way round.** On the 8BitDo the file maps `a` to SDL button 0 and `b` to 1, which are the buttons an Xbox-layout pad prints A and B on. But it maps **`x` to button 3 and `y` to button 2**, and 3 is the button printed **Y** while 2 is printed **X**. Found by a person pressing the button a footer told them to: a hint reading "X" ran on `es_input`'s `x` and did nothing until Y was pressed. So a UI that shows button prompts must not use these names as labels. `a` and `b` are safe, `x` and `y` are not, and the file is still the authority on _which physical input_ is meant, just not on what it is called |

## Measured in M7b, second hands-on pass, through a remote session

A **different controller** on the same live install at **8.2.1**, presented by Parsec's virtual
pad (a ViGEm device that enumerates as `Xbox 360 Controller for Windows`, `VID_045E&PID_028E`),
driven from a macOS client on 2026-08-26. SDL 2.32.8, loaded from
`K:\RetroBat\emulationstation\SDL2.dll` as the shipped reader loads it. The value of the pass
was that the pad **is not always present**, which a controller sitting on a desk never
reproduces.

| #   | Question                                                                                                                              | Measured                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| --- | ------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 226 | (not addressed) whether a console process can enumerate joysticks through RetroBat's SDL2                                             | **Not with SDL's default backend, and the failure is silent.** `SDL_NumJoysticks()` returned **0** for a continuous 120 s in a console process, while Windows reported three present game controllers at the same moment: the Parsec pad, an Xbox Wireless Controller over Bluetooth LE, and the 8BitDo. SDL 2.32.8 defaults to the **RAWINPUT** joystick backend, which needs a window message pump. Setting `SDL_JOYSTICK_RAWINPUT=0` in the environment made the same process report **1** device immediately. RomMBat's own UI is unaffected, because Avalonia pumps messages; **a console probe of controller state is not**, and one that reports "no controller" is measuring itself rather than the machine                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| 227 | (not addressed) whether the GUID a probe reads is the GUID `es_input.cfg` holds                                                       | **Only under the same SDL backend.** Bytes 14-15 are SDL's driver signature and byte 12-13 its version. The same Parsec pad reads `030000005e0400008e02000014017801` under XInput (`0x78`, ASCII `x`) and `es_input.cfg` records `030000005e0400008e02000000007200` under rawinput (`0x72`, ASCII `r`, version zeroed). `EsInputMap.NormalizeGuid` zeroes bytes 2-3, the name CRC, and deliberately nothing else, so those two do not compare equal. Every controller row in the live file ends `7200`, `6800` or `6803` and none ends `78`: EmulationStation writes what its own backend saw. A GUID measured with the backend changed is therefore **not comparable to the file**, which is a trap for the next probe rather than a defect                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| 228 | (not addressed) whether EmulationStation has already solved the on-screen keyboard, and what it binds                                 | **It has, and two of its bindings were taken by RomMBat for something else.** `GuiTextEditPopupKeyboard` on a live 8.2.1 session binds **A** to press the highlighted key, **Start** to OK, **B** to BACK, **L (`pageup`) to DELETE**, **R (`pagedown`) to SPACE**, a face button to SHIFT, and the d-pad to MOVE CURSOR. Corroborated in `resources/locale/*/LC_MESSAGES/emulationstation2.po`, which carries `MOVE CURSOR`, `SHIFT`, `SPACE`, `DELETE`, `RESET` and `SHIFTS FOR UPPER, LOWER, AND SPECIAL` as ES's own strings. So **the shoulders are not free**: RomMBat had put the case toggle on L1/R1, which is where a RetroBat user's thumb already expects delete and space. That string also says ES's keyboard has **three** layers (upper, lower, special) plus an `ALT GR`, where RomMBat's two suffice only because its one field is a URL. **Superseded on every detail by 234**, which read the source instead of the screen: the face buttons are `y` for SHIFT and `x` for RESET, and RomMBat now carries upstream's layout rather than two layers of its own                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| 229 | (not addressed) whether EmulationStation itself treats a controller as hotpluggable                                                   | **It does, and says so on screen.** The same string table carries `%s connected` and `%s disconnected` as ES's own notifications. A front end launched from inside ES that cannot notice a pad arriving is the odd one out, which is what RomMBat was until this stage                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| 230 | (not addressed) why EmulationStation's footer draws icons rather than naming buttons                                                  | **Because a letter is wrong on two layouts out of three, and the live install has all three.** The bottom face button is A on an Xbox pad, Cross on a DualSense and B on a Switch Pro, and `es_input.cfg` on `K:` configures Switch Pro, DualSense, PS4, 8BitDo and Xbox 360. ES draws a four-dot diamond with one dot filled, which names a **position** rather than a label, and position is the one thing every layout agrees on. It is also what `es_input.cfg` already encodes: `a` is the bottom button, `b` the right, `y` the left and `x` the top. Spinnich's observation, checked against the file rather than taken on trust                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| 231 | (not addressed) whether RomMBat survives a controller switched off and on, on a locally connected pad                                 | **All three states, measured on the 8BitDo through the shipped `GamepadReader`.** The probe launched with the pad **off** and reported `NoDevice`; the pad was switched on 24.4 s later and it went `Ready` and named it, which is the case that used to be unrecoverable for the life of the process. **16 of the 21 configured names** then read correctly (`a b x y pageup pagedown l2 r2 l3 r3 up down left right joystick1left joystick2up`). Switched off, it returned to `NoDevice` at 09:00:43.517; switched back on it returned to `Ready` at 09:00:54.609 and input resumed. **The 11.1 s between those two is the pad's own re-pairing plus the tester's hands, not RomMBat's latency**, which the log cannot separate and which is bounded by the 1 s scan interval. The run also re-confirmed **225** from the other side, by a person rather than by reading: the button printed **X** logs `y` and the one printed **Y** logs `x`, because the file maps `y` to button 2 and `x` to button 3. Probe 5                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| 232 | (not addressed) what a real hands-on pass through the ES menu records, on the UI build rather than the stub                           | **The whole loop closes and RomMBat still is not a play session.** Two RomMBat sessions from the ES menu on 8.2.1 (62.9 s and 32.8 s) produced `game-start` and `game-end` pairs that both journalled **`discarded`**, with no outbox row, on the first build a person can actually sit in. A PS2 game launched immediately afterwards journalled **`correlated`** and produced outbox row 26, `play_session`, 54,762 ms, state **`sent`** by the detached `background quit` pass. Both paths journalled **relative** (`system/es_menu/rommbat.menu` and `roms/ps2/Armored Core 3 (USA).chd`), so rule 1 held at the hook boundary, and both launches carried **no arguments** (`[Running] RomMBat.exe`), which is finding 220 holding on a second occasion. Re-proves **221** and **222** against the UI rather than a fixture                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| 233 | **107 and 203 disagree about `/reloadgames`, and stage 7b-2 needed to know which applies when RomMBat is the app in front** (probe 6) | **It is deferred, not discarded, and ES does not rescan on resume by itself.** Five phases on the live 8.2.1 install, one variable, minutes apart, polling `/systems` for `retrobat`'s `totalGames` rather than trusting the 200. **Control**, nothing in front: a `.menu` written and reloaded took **93 to 94 in under 300 ms**, so 203 still holds. **Live**, RomMBat up from the ES menu: a second marker plus `GET /reloadgames` answered **200 in 6 ms** and the count was **unchanged at 94 after 10 s**, so 107 applies to RomMBat exactly as it does to a game. **On exit the count was 95 before any further call**, so the deferred reload had been applied on resume. **The discriminating phase**: a third marker written with RomMBat in front and **no reload issued at all** left the count at 95 both during and after, and a reload issued afterwards took it to **96**, proving the third marker was valid all along and simply unqueued. So a reload issued behind RomMBat is **queued and applied when RomMBat exits**, and ES rescans on resume only because it was asked to. **The consequence for the design is the opposite of the one assumed**: `sync` from the interface should still call `/reloadgames` after writing gamelists, and the games appear the moment the user leaves RomMBat, which is when they would look. No workaround is owed and `GamelistSync`'s write-then-reload is correct unchanged. Also recorded: EmulationStation was **unfocused throughout** and the control reload worked anyway, so ES's own reload does not depend on focus |
| 234 | **228 said ES ships a keyboard and left what it contains unread, which the layout this stage copies depends on**                      | **Read from upstream's source, which is where it lives: the tables are compiled in, not shipped as data.** `es-core/src/guis/GuiTextEditPopupKeyboard.cpp` in `batocera-linux/batocera-emulationstation`, which RetroBat builds as `RetroBat-Official/emulationstation`. **Three layouts exist and no more**, `kbUs`, `kbFr` and `kbKr`, each a 13-column grid of **four faces per key** (lower, upper, alted, alted-upper) over five rows, with `DEL`, `OK` and `ALT` down the right edge and `SHIFT`, `SPACE`, `RESET`, `CANCEL` along the bottom. `OK` spans two rows and the bottom row spans 2/7/2/2. **The buttons**: `a` presses the key, `start` OK, `b` BACK, `pageup` DELETE, `pagedown` SPACE, **`y` SHIFT and `x` RESET**, and RESET means _commit the empty string and close_ rather than clear the field. Left and right wrap; up and down go to the text field. A key whose face is empty on the current layer is drawn, holds focus and does nothing, which is why every layer is the same shape. `altKeys()` clears shift on the way in                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| 235 | (not addressed) which language picks the layout, and where a Windows RetroBat keeps it                                                | **`es_settings.cfg`'s `Language`, and only by accident.** ES asks `SystemConf` for `system.language`; `SystemConf` falls back to ES's own `Settings` when it has no config file of its own, and `Paths.cpp` sets that file **only under `#if defined(WIN32) && defined(_DEBUG)`**, so a Windows release build has none and the fallback is the whole path. `batocera.conf` is **absent on the live 8.2.1 install**, which agrees. The value is split on `_` and the part before it lowercased, so `fr_FR` resolves and a bare `FR` would not, and anything but `fr` or `ko` gets `kbUs`. `Language` is also **pruned when it equals ES's default** (finding 170), so absent is the ordinary reading of "default" rather than evidence nobody chose                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| 236 | (not addressed) which values `metadata_providers` and `statuses` accept, since the filter screen has to offer a list                  | **Probed, because the schema does not say and the server does not complain.** The pinned 5.2.0 schema declares both as a bare array of strings with no enumeration, and `GET /api/roms` **silently ignores** a `metadata_providers` value it does not recognise: `metadata_providers=zzz-not-a-provider` returned the full **96,060**, identical to no filter at all. So a wrong entry in a picker is worse than a missing one, since the user picks a provider and is handed the whole library. Probed one at a time against the live instance, where a recognised value moves the total and an unrecognised one does not: **igdb 473, ss 87,079, ra 32,537, hasheous 66**, and **moby, launchbox, tgdb, flashpoint, hltb, gamelist, libretro all 0**, which still proves recognition. **`sgdb` is ignored**, so deriving the list from `SimpleRomSchema`'s `*_id` fields would have shipped a dead option; `screenscraper` and `playmatch` are ignored too. Eleven recognised in all. **Statuses cannot be probed the same way**: an unrecognised status returns zero rather than everything, so `backlogged` looks exactly like a real status nobody has used, and `RomUserStatus` in the schema is the authority for its five                                                                                                                                                                                                                                                                                                                                                        |
| 237 | (not addressed) whether `filter_values` covers every filter `GET /api/roms` accepts                                                   | **No, and the mismatch runs both ways.** The sidecar reports **eleven** keys: genres, franchises, collections, companies, game_modes, age_ratings, player_counts, regions, languages, tags, platforms. The endpoint accepts **eleven** array filters: the same list minus `game_modes` and `platforms`, plus `statuses` and `metadata_providers`. So `game_modes` is a value list with no filter behind it and must not become a picker, `platforms` is the scope rather than a facet, and the two the endpoint adds have no value list and need finding 236's vocabularies. A screen driven off the sidecar would offer one facet that does nothing and miss two that work                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |

## Measured in M7b stage 2b, the third hands-on round

One question, forced by a fix that did not work. RomMBat had been taught to follow RetroBat's
scraper switches in the second round; the switch was then turned off on the live 8.2.1 install
and the videos stayed. Answered wrongly first from upstream source alone, then correctly by
Spinnich installing a second RetroBat at `K:\RetroBat-Default` and looking at the menu, which
is the measurement the whole finding turns on.

| #   | Question                                                                                                          | Measured                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| --- | ----------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 238 | **What an absent `ScrapeVideos` or `ScrapeManual` means, since 170's pruning makes absence ambiguous in general** | **Both switches ship on, and absence is a deliberate off. `RetroBat ships templates that override EmulationStation's compiled defaults`, which is the general trap and the reason the first answer here was wrong.** `system/templates/emulationstation/es_settings.cfg` carries `ScrapeVideos` and `ScrapeManual` as **`true`**, byte-identical on a fresh 8.2.1 install and on the used one, and a fresh install's scraper menu shows both **on**. EmulationStation's own compiled defaults are the opposite: `Settings.cpp` at `c686ca8b`, the last commit to that file before the 2026-08-23 release, registers `mBoolMap["ScrapeVideos"] = false` and registers `ScrapeManual` **nowhere**, so `SETTINGS_GETSET(bool, mBoolMap, getBool, setBool, false)` returns `false`. `saveMap` then drops any key equal to its registered default, and any key with no registered default equal to `false`, so **turning a switch off deletes the key** and a literal `value="false"` never occurs. The three states are `true` on, **absent** off, and `false` never. **Seeding is once, not per launch**: the used install's template still says `true` while its live file has no `ScrapeVideos` line and was rewritten by ES at 06:37 the same morning without restoring it. **The consequence for RomMBat** is that the absent branch must be off: reading it as RomMBat's own default left 389 MB of megadrive video and **2.05 GB across the tree** that no setting could reach, and made the round-two fix a no-op for the kind it was written for. Generalises 170 and 235, and corrects the method: for anything in this file, read `system/templates/` and a fresh install, because upstream source is not evidence of what a RetroBat does |

## Measured while scoping RetroBat's remaining scraper options, after M7b stage 2b

Spinnich walked the scraper menu on a fresh 8.2.1 and listed the nine options RomMBat does not
read. Answered from `GuiScraperSettings.cpp` and `MetaData.cpp` at `c686ca8b` (the last commit
to each before the 2026-08-23 release) for the RetroBat half, and by 200-row samples per
platform against the live RomM 5.2.0 instance for the other half.

| #   | Question                                                                                                | Measured                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| --- | ------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 239 | **What the other nine scraper options mean, and which of them RomM can actually serve**                 | **Three of them are not media kinds at all, they are source pickers for slots RomMBat already fills.** The source comments name the tag: `ScrapperImageSrc` feeds `<image>`, `ScrapperThumbSrc` feeds `<thumbnail>`, `ScrapperLogoSrc` feeds `<marquee>`. Values are `ss`, `sstitle`, `mixrbv1`, `mixrbv2`, `box-2D`, `box-3D`, `fanart`, `wheel`, `marquee`, and empty for NONE. **RomM's `ss_metadata` carries fourteen paths, not the one `logo_path` RomMBat reads**: `title_screen_path`, `miximage_path`, `miximage_v2_path`, `box2d_path`, `box3d_path`, `box2d_back_path`, `box2d_side_path`, `fanart_path`, `logo_path`, `marquee_path`, `bezel_path`, `physical_path`, `video_path`, `video_normalized_path`. Coverage over 200 rows per platform (megadrive / snes / atari2600): cover **82/100/100%**, `title_screen` **82/100/99%**, `miximage_v2` **82/100/100%**, `box2d_back` **81/100/100%**, `logo` **82/100/100%**, `bezel` **49/97/96%**, `manual` **55/96/95%**. **Every one of those numbers says when that platform was last scraped and with what settings, and nothing about what RomM or ScreenScraper can serve.** That is what makes the apparent `box2d` / `box3d` split a reading error rather than a finding: megadrive shows 0% 2D and 81% 3D, atari2600 the exact inverse, and the cause is Spinnich's own library rather than the platform. `box2d` in that form is new to RomM and the older platforms have not been rescraped for it, and he has recently stopped storing `box3d`, so the recently scraped platform has none. `fanart_path` at 0% on all three is the same class of observation and is **not** evidence the field is unusable. **The design consequence is that coverage must never decide what RomMBat supports**: support what the schema exposes, let an absent path be the ordinary `Missing` case, and re-read the numbers as a snapshot that moves the next time an administrator rescrapes. **`ScrapeMap` and `ScrapePadToKey` are dead**: no map field exists anywhere in the 5.2.0 schema, and padtokey is ES's own input config rather than media. `ScrapeBoxBack` maps to `box2d_back_path` and `ScrapeBezel` to `bezel_path`, both real. ES's own gamelist vocabulary is wider than the menu and reads `titleshot`, `magazine`, `cartridge`, `boxart`, `wheel` and `mix` as paths too |
| 240 | **How a source change could be detected, since a re-fetch has to know the slot was filled differently** | **Not by hash, because RomM publishes none for media, and it does not need one.** `gamelist_metadata` was **absent on every row sampled** across all three platforms, so its `md5_hash` never arrives, and no media path on any block carries a hash of its own. Recording **which source filled the slot** on the `local_file` row makes a settings change an ordinary `recorded != wanted` comparison, which is the `Discard` path 7b-2b already built for a kind being turned off, widened by one column. **Duplication needs no policy either**: ES removes Box 2D from the Box Source list when Image Source is set to `box-2D`, on `imageSource->setSelectedChangedCallback`, so upstream prevents the collision at the menu and RomMBat mirrors that rather than inventing a rule                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| 241 | **Whether the stored source value is stable, since the menu changes with the SCRAPE FROM setting**      | **No. Switching scrapers can rewrite a source the user never touched.** `GuiScraperSettings`'s constructor builds every row from `Scraper::getScraper()`, guarding each on `isMediaSupported(...)`, and when the stored value is not in the new scraper's list `selectFirstItem()` picks one and `addSaveFunc` writes it on close. So `ScrapperImageSrc` tracks the last scraper selected when that menu was closed rather than a deliberate choice. The rule for RomMBat is to read the value, map what it recognises, treat anything else as a fallback, and **ignore the `Scraper` setting entirely**, because RomM is not any of the scrapers it names. Spinnich's observation, checked against the source                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| 242 | (not addressed) whether `platform_id` filters `GET /api/roms`                                           | **No, and it is silently ignored exactly as 236 found for `metadata_providers`.** `platform_id=303` returned `total` **96,060**, the whole library, identical to no filter; the parameter is `platform_ids`, plural, which returned 3,006 for megadrive and 3,454 for snes. Two probes run minutes apart returned byte-identical counts for two different platforms, which is what exposed it. Second instance of the same trap on this endpoint, so treat an unrecognised query parameter here as returning everything rather than erroring                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
