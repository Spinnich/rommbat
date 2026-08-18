# RetroBat findings (M0)

Measurements from a real RetroBat install and a real network. **Every number here is only
true for the versions named below.** Re-run the probes in `tools/m0-probes/` before trusting
any of it against a different build.

|                    |                                                                   |
| ------------------ | ----------------------------------------------------------------- |
| RetroBat           | `8.2.0-stable-win64`, read from `system/version.info`             |
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
trusted: two of the twelve are wrong.** `flycast` writes to `reicast/states` rather than the
declared `flycast/sstates`, and **`openmsx` writes to `bios/openmsx/savestates/`, a different
top-level tree from the declared `saves/msx1/openmsx`**. Filenames, by contrast, were correct
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
reported on 2026-08-09 and all were open at the time of writing.

| Issue                                                                      | Repo                                 | What it covers                                                             |
| -------------------------------------------------------------------------- | ------------------------------------ | -------------------------------------------------------------------------- |
| [#249](https://github.com/RetroBat-Official/retrobat/issues/249)           | `RetroBat-Official/retrobat`         | `game-start` never runs when the gamelist `<name>` has a space             |
| [#1336](https://github.com/RetroBat-Official/emulatorlauncher/issues/1336) | `RetroBat-Official/emulatorlauncher` | Flycast writes states to `reicast/states`, not the declared path           |
| [#1337](https://github.com/RetroBat-Official/emulatorlauncher/issues/1337) | `RetroBat-Official/emulatorlauncher` | BizHawk crashes on an unguarded `inputPortNb[core]` when `-core` is absent |

`RetroBat-Official/emulationstation` is a fork of `batocera-linux/batocera-emulationstation`
with **issues disabled**, which is why the ES-behaviour report went to `retrobat`.

**#1337 is the low-severity one, deliberately reported as such.** EmulationStation always
passes a core, and all 36 BizHawk cores this install's `es_systems.cfg` declares are among
the 42 keys in `inputPortNb`, so only direct invocation or a future unlisted core can reach
it. It matters to RomMBat because RomMBat is a direct invoker: **pass `-core`**.

A fourth candidate was investigated and **not** filed, because it is not RetroBat's bug:
openMSX never received Alt+F2 in one run because NVIDIA's Photo mode overlay claimed the
combination first.

**#249 was filed before its mechanism was known.** It described a `.bat` hook not running
when the display name contains a space. Probe 7b showed ES fires the event correctly and the
fault is in the handoff to an interpreter, that it also breaks `.ps1` hooks on any
parenthesis, and that both failures reproduce outside EmulationStation. The issue was
[updated with the mechanism](https://github.com/RetroBat-Official/retrobat/issues/249#issuecomment-5232474774)
on 2026-08-09, including two verified fixes (`cmd /s /c "<whole command>"` for `.bat`,
`-File` for `.ps1`) and a suggested retitle, since the original title describes the `.bat`
symptom only.

**Re-check all three before each release.** A fix upstream does not just close a ticket, it
changes what RomMBat should do: #249 landing would let a `.bat` hook work and reopen the
simpler journal design the plan originally wanted, #1336 landing would move Dreamcast states
to a different directory, which is a breaking change for anything that hardcoded the
workaround, and #1337 landing would only relax a constraint, since passing `-core` stays
correct either way. No workaround should be removed until the fix is in a release RomMBat's
compatibility gate accepts.

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
(2026-08-09, open). Filed there rather than on `RetroBat-Official/emulationstation`, which has
issues disabled.

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

**What this means for M6.** Writing the per-game override is durable, so the opt-in
conversion the plan describes is viable. The merge-don't-clobber rule still stands, but for
the ordinary reason that two writers share a file, not because ES is expected to eat the
key. The practical sequence is unchanged: write while ES is idle, write atomically, merge.

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
"RetroBat will sync dolphin and libretro-dolphin saves folders". That is RetroBat copying
save files between two locations on its own schedule. Whether it is on has to be detected
before either location is treated as authoritative.

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
(2026-08-09, open).

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
those same zero-argument cases run fine from a `.bat` on the first host. So this is not
issue #249: **that machine cannot launch a `.bat` or a `.ps1` at all, and can launch an
`.exe`.**

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
| 9c  | (not addressed)                                                                                       | `dolphin_sync_saves` has RetroBat copying saves between the dolphin and libretro-dolphin folders on its own; it must be detected before trusting either                                                                                                                                                                                   |
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

| #   | Previously                                                                           | Measurement says                                                                                                                                                                                                                                                                                                                                                              |
| --- | ------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 112 | `emulatorLauncher.log` is 268 KB for 5 weeks and 70 launches (plan M6, probe 1)      | That describes a smaller install, not the mechanism. Live **503,225 B** for 6 weeks and **159** launches, beside a **1,048,604 B** `.log.old` for the 3 weeks before it at **265** launches. **Rotation is a size threshold near 1 MiB**, and the two files **do not overlap**, so reading `.old` then live yields launches in time order across the boundary                 |
| 113 | Rom paths in the log are rooted at the install (plan M6 by implication)              | **They are rooted at whatever drive letter the install had at the time.** 295 of 424 read `D:\RetroBat` and 129 read `E:\RetroBat`, one install, one continuous log. Relativising by stripping the current root silently discards 70% of the history, so relativise on the `roms\<system>\` segment instead                                                                   |
| 114 | `[Startup]` identifies a launch (plan M6 by implication)                             | **730 `[Startup]` lines, of which 424 are a game launch.** `emulatorLauncher.exe` is also invoked for `-updatestores` and similar, so keying on `[Startup]` over-counts by 72%. The discriminator is the presence of `-rom`                                                                                                                                                   |
| 115 | (not addressed) what shape `-rom` takes                                              | Three shapes, and a naive read misses two. **Unquoted once in 424**, with spaces and parentheses in the path, so `-rom "([^"]+)"` misses it. **Not the final flag 19 times**, and **`-core` written after it 5 times**, so a positional read misses those. Read the quoted form to its closing quote and the unquoted form to end of line                                     |
| 116 | (not addressed) whether the log can supply an end time                               | **No. 187 of 424 launches never record `Process exited with code`.** End time has to come from the `game-end` hook's own timestamp. Exit codes seen: 226 zero, 2 one, 5 minus one, 3 `-1073741819` (access violation), 1 `-805306369`                                                                                                                                         |
| 117 | (not addressed) how the log is encoded                                               | **Opens with a UTF-8 BOM**, and carries 15 unstamped continuation lines across the two files, .NET stack traces among them. A line-per-record parser must tolerate both                                                                                                                                                                                                       |
| 118 | An orphan `game-end` has to be discarded by inference (plan M6)                      | **An ES-menu launch is identifiable rather than inferred.** 27 launches carry `-system retrobat` with a `-rom` under `system\es_menu\`. So RomMBat's own exit not becoming a play session is a rule keyed on observable data, which is stronger than the plan assumed was available                                                                                           |
| 119 | Four top-level directories under `saves/` are emulator-named, not systems (probe 2)  | **Nine**, against the 243 systems the live `es_systems.cfg` declares: `amiga`, `dolphin`, `gameandwatch`, `ghostship`, `loopy`, `mesen`, `pb`, `psxmame`, `windows`                                                                                                                                                                                                           |
| 120 | The second segment is the emulator (plan M6, probe 2)                                | **Not reliably.** `mame/artwork`, `mame/cfg`, `mame/ctrlr`, `n64/sram`, `n64/games`, `n64/sstates`, `psp/SYSTEM`, `psp/Cheats`, `switch/user`, `switch/sdmc`, `rtcw/Main` and `dolphin/User` name no emulator. Where states live it is emulator-**and-core**, so `saves/gbc/libretro.gambatte/` sits beside `saves/gbc/*.srm`. Discovery cannot be positional in either level |
| 121 | A loose file under `saves/<system>/` is a class A battery save (plan M6)             | **`xbox` refutes it**: `eeprom.bin` and a 39,714,816 B `xbox_hdd.qcow2` sit loose at the system root and both are class D. And **`megacd` interleaves classes at one level**, per-game `.brm` and `.srm` beside the shared `4Mbit_cart.brm`, so excluding class D is a named-container list rather than a positional rule                                                     |
| 122 | `save_shapes.json` leaves 21 systems `_unclassified` (F19)                           | Still 21, and **all 21 hold content on the measured install**. `ports` holds content and is absent from the file entirely, not even listed as unclassified. So the bundled data is short of the tree in two different ways                                                                                                                                                    |
| 123 | `dolphin_sync_saves` must be detected before trusting a location (finding 9c)        | **Unset on this install**, as are all four class-D options (`duckstation_memcardtype`, `pcsx2_slot1_memory`, `flycast_vmupergame`, `dolphin_slotA`). Stock is the case to build for; the conversion hazards are stage 2's to detect                                                                                                                                           |
| 124 | RPCS3's 32,451 files make any recursive content hash a performance problem (plan M6) | **The count is of the emulator's data root, not of saves.** `saves/ps3/rpcs3` is 32,451 files and **52.87 GB**, hashing in **426 s warm and 512 s cold**, but that is `dev_hdd0` entire. The savedata subtree is **17 directories, 77 files, 16.3 MB, 0.06 s**. So the input is "scope the save unit in the shape definition", not "budget the hash"                          |
| 125 | (not addressed) what a class A pass actually costs                                   | **37 loose files, 43.0 MB, 0.51 s** across every system on a real install, and **38 MB of that is `xbox`'s class-D disk image** which it must not read. MAME's whole `nvram` tree, for comparison, is 1,531 files and 8.0 s                                                                                                                                                   |

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

| #   | Previously                                                                    | Measurement says                                                                                                                                                                                                                                                                                                                    |
| --- | ----------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 126 | `POST /api/states` had never been called from this repo (plan M6)             | **It is an upsert, not an append.** Three posts of one `file_name` reused one row (id 115) across two different payloads. So there is no slot history to prune, no `autocleanup` to ask for, and a replayed flush is idempotent for free. `PUT /api/states/{id}` works and is unnecessary                                           |
| 127 | The `emulator` distinguishes one state from another (plan M6, by implication) | **It does not. The key is `(rom_id, file_name)` and nothing else.** Five posts of one name under `libretro`, `bizhawk`, `libretro.snes9x`, `libretro.bsnes` and `libretro/evil` all reused id 119, overwriting the row's emulator and moving its stored file between directories each time                                          |
| 128 | (not addressed) whether a bracketed tag separates two states                  | **It does.** `TagProbe [libretro.snes9x].state1` and `TagProbe [libretro.bsnes].state1` produced ids 121 and 122. So the key is the whole `file_name`, not `file_name_no_tags`, and scoping the uploaded name is a working fix for 127                                                                                              |
| 129 | A state has a `slot` and a `content_hash` (plan M6, by implication)           | **Neither exists**, in the pinned schema or in the live response. `{emulator}:{core}:{slot}` is therefore a local identity only, and "is this state in step" is answerable only from a hash the device recorded itself                                                                                                              |
| 130 | The server renames an upload (F6, for saves)                                  | **Not for states.** A save came back `Probe Save [2026-08-17_12-27-44].srm`; a state came back exactly as sent. `file_name_no_tags` is still computed, and strips `(USA)` out of a real ROM name, so it is not a way to recover the name that was sent                                                                              |
| 131 | (not addressed) what a zero-byte `screenshotFile` does                        | **Accepted and stored as a real screenshot row** (id 151, `file_size_bytes: 0`). Since RetroBat's mirror races the emulator writing the image and a zero-byte result was measured across three saves of one game, the client has to suppress it, because nothing downstream does                                                    |
| 132 | Two open download cases turn on whether negotiate volunteers slots (plan M6)  | **It never does, so both cases resolve negatively.** A device with a save on the server negotiated an **empty** `saves` array and got `operations: []`; negotiating one unrelated slot returned exactly that slot. Negotiate is client-driven over the set the client names, so a fresh device cannot discover its saves through it |
| 133 | (not addressed) whether `emulator` is sanitised server-side                   | **It is not.** `libretro/evil` was accepted and became two path segments in the stored state's `file_path`. Worth reporting upstream. RomMBat's own schema refuses a separator in that column, so it cannot send one                                                                                                                |

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

| #   | Previously                                                                         | Measurement says                                                                                                                                                                                                                                                                                                                                                                |
| --- | ---------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 140 | Class C is "a directory per game" (plan, the class table)                          | **Refuted, on three systems at once.** `ps3` holds `BLUS30109G6A383E91`, `BLUS30109G6A3B071C` and `BLUS30109S` for one title id, and `BCUS98111-AUTOSAVE` beside `BCUS98111-USERDATA`. `psp` holds `UCES01011` and `ULES01513SYSDATA`. **`gamecube` has no per-game directory at all**: `69-GXBE-game1.ssx.gci` and `69-GXBE-settings.ssx.gci` are two files in a shared folder |
| 141 | The unit key is the directory's name                                               | **It is a prefix of it.** `ULES01513SYSDATA` carries key `ULES01513`, and `BLUS30187GAMEDAT9ZLDR0F5K7M4000` carries `BLUS30187`. Matching the whole segment finds nothing                                                                                                                                                                                                       |
| 142 | RPCS3 hashing costs 426 s and the scoped subtree 0.06 s (plan, M6)                 | **Confirmed to two decimal places**, re-run rather than inherited: the data root is 32,451 files / 52,868.4 MB / **426.07 s**, its `dev_hdd0/home/*/savedata` subtree 77 files / 16.3 MB / **0.06 s**, MAME's whole `nvram` 1,531 files / 137.3 MB / 8.02 s                                                                                                                     |
| 143 | Reading the ID out of the ROM is the fallback route (plan, M6; F17)                | **It reaches nothing this stage needs.** Every image in five systems, head read only: `gamecube` 178 `.rvz`, **100%** readable at `0x58` with the version checked; `wii` 40 `.rvz` + 13 `.wad`, **75.5%**; `psp` 147 `.cso` + 7 `.chd`, **0%**; `ps3` 23 `.dec.iso`, **0%**; `psx` 386 `.chd`, **0%**. No constant offset reaches a `.cso`, a `.chd` or an ISO9660 image        |
| 144 | `PARAM.SFO` yields the Game ID (start-m6-stage2b brief)                            | **It yields nothing the directory name does not.** Its keys are `SAVEDATA_DIRECTORY`, which is the directory's own name, and `TITLE`, a human string (`'echochrome'`, `'The 3rd Birthday'`). So parsing it buys a fuzzy title match, never an exact key                                                                                                                         |
| 145 | The state `.txt` sidecar may be a cheaper third route (stage 2a ledger)            | **It is, and it is measured.** `ppsspp/3rd Birthday, The (Europe).txt` holds `ULES01513_1.00`, whose `ULES01513` prefix joins `SAVEDATA/ULES01513SYSDATA`, while the stem resolves through `RomIndex`. It needs no ROM read and no observed launch, and it covers only games that have a state                                                                                  |
| 146 | Wii's NAND "is not all attributable" and what counts as a save unit is open (plan) | **Decided from data.** `title/00010000/<hex>/` is the disc-game tree and the hex is the ASCII game code (`52534245` = `RSBE`), which joins exactly to what route 2 reads at `0x58`. `title/00000001/*` is system titles, and `shared2/`, `sys/` and `fst.bin` are system state. A title with `content/title.tmd` and no `data/` is an installed stub, not a save                |
| 147 | The server renames a save (F6, measurement 130)                                    | **True for a bundled directory save too, and the untagged name is the unit key.** `UCES01011.zip` came back `'UCES01011 [2026-08-17_23-52-18].zip'` with `file_name_no_tags` `'UCES01011'` and `file_extension` `'zip'`                                                                                                                                                         |
| 148 | `content_hash` is the MD5 of the bytes uploaded (F3, and the download verify)      | **True for a plain file, false for an archive.** A 24 B payload, 570 B of `'A'`, 570 B random and 570 B of NUL all match exactly. A 570 B zip does not, independent of `Content-Type` and of filename. Rebuilding one member at a different compression level and timestamp gives a different zip and **the same** digest; renaming the member changes it                       |
| 149 | (not addressed) what negotiate compares for a bundled save                         | **The server's own returned digest, and only that.** Sending it answers `no_op (Content is identical)`; sending our logical fold or the archive's MD5 answers `download (Server save is newer)`. Eight candidate reconstructions of the server function reproduce none of the observed values, so it is **not reproducible client-side** and must not be guessed at             |
| 150 | Different content into one slot appends a row (F3)                                 | **Not on this version. The key is `(rom_id, slot, file_name)` and it replaces.** Same name and different content reused id 136 with the content hash updated and no `overwrite` flag; a different name in the same slot made a second row. F3's two uploads shared a name, so its reading does not hold here                                                                    |
| 151 | Negotiate never volunteers a slot the client did not submit (**measurement 132**)  | **Refuted, and 132 is withdrawn.** An **empty** `saves` array returned **13 downloads across two ROMs**, one never named by the client. The mechanism, driven: 13 ops, then `GET /api/saves/134/content` plus `POST /api/saves/134/downloaded`, then 12 ops with that save gone. Negotiate returns a download for every save the **device** has no current sync record for      |
| 152 | A restore writes `file_name_no_tags` plus `file_extension` (plan, M6; F6)          | **Refuted on a real save, which is what 130 half-saw.** `Phantasy Star (Brazil) [2026-08-17_17-01-00].srm` has `file_name_no_tags` `'Phantasy Star'`: the server strips `(Brazil)` as a tag. Writing that produces a filename libretro cannot see. The ROM's own stem plus the extension is the only sound source, and the negotiate operation carries neither                  |
| 153 | MAME's short name **is** the rom basename, so attribution is free (probe 2, plan)  | **Structurally sound and unprovable on this install.** 1,231 `nvram` unit directories against 3 `.zip` files in `roms/mame`, so nothing joins. The names are well-formed MAME short names (`1944`, `19xx`, `1on1gov`, `20pacgal`) and a MAME set names each archive after one, but this library cannot demonstrate it                                                           |

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
