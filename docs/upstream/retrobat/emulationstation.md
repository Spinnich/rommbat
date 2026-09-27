---
summary: EmulationStation's HTTP API, the ES menu and `.menu` registration, and what reload, quit and launch really do.
read-when: Before calling ES's HTTP API, registering the ES menu entry, or relying on a reload or a quit.
---

# RetroBat: EmulationStation

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-15. Registration needs two files: the `.menu` plus a `<game>` element in `es_menu/gamelist.xml`

Plan says: The ES menu entry is "a `system/es_menu/*.menu` entry" (L374, L237)

Measurement says: Registration needs **two** files: the `.menu` plus a `<game>` element in `es_menu/gamelist.xml`. `.menu` files are roms of a `retrobat` system parsed by `emulatorLauncher`, not by ES

## RB-16. Refuted

Plan says: Everything lands under `RetroBat/plugins/rommbat/` (L139, `DEVELOPER_SETUP.md` §6)

Measurement says: **Refuted.** `.menu` executable paths resolve under `emulators\` and reject `..\` escapes, so a menu-launched app must live at `<root>/emulators/rommbat/`

## RB-28. Neither

Plan says: Library refresh lives in `update-gamelists` and `-updatestores` (L402-405)

Measurement says: Neither. `-updatestores` drives the content store, not gamelists, and ES's CLI is startup-only. **ES serves an HTTP API on `127.0.0.1:1234`; `GET /reloadgames` is the mechanism**

## RB-30. The ES API also offers `/caps` as a second version source, `/quit` to close ES cleanly before touching

Plan says: (not addressed)

Measurement says: The ES API also offers `/caps` as a second version source, `/quit` to close ES cleanly before touching `es_settings.cfg`, and `/systems/<system>/games` to read ES's own view of the library

## RB-35. Not while a game is running

Plan says: `/quit` closes ES cleanly (probe 3, `retrobat-layout` skill)

Measurement says: Not while a game is running. `/quit` and `/emukill` both return cleanly and do nothing until the emulator exits, so a 200 is not evidence the action happened. Poll for the process

## RB-45. What `GET /reloadgames` costs after M4 writes a gamelist

Plan says: (not addressed) what `GET /reloadgames` costs after M4 writes a gamelist

Measurement says: It answers in 1-2 ms and reloads afterwards, so its response time measures nothing. Time to the change being visible is **269 ms at 200 entries, 1084 ms at 100,000**

## RB-107. Ignored while a game is running

Previously: `/reloadgames` is the refresh mechanism (probe 3, plan M4)

Measurement says: **Ignored while a game is running**, exactly as `/quit` and `/emukill` are. 200 in 1 ms, and a ROM added to the folder was still not reported five seconds later. Reproduced twice. So the one API call M4 depends on shares the trap: a 200 is not evidence the reload happened

## RB-108. 2.04 s, five raw TCP connects and three `HttpClient` requests alike, which is M0 probe 6b's "host up, port

Previously: A short timeout covers a reload with ES absent (plan M4)

Measurement says: **2.04 s**, five raw TCP connects and three `HttpClient` requests alike, which is M0 probe 6b's "host up, port closed" row (2,040 ms) reappearing on loopback. The project's 2 s interactive `ConnectTimeout` fires at almost exactly the same moment and buys nothing. ES being absent is the ordinary case, so this client needs a far shorter one

## RB-201. How long `background quit` has to wait for ES to be gone once the hook has fired

The claim being checked: (not addressed) how long `background quit` has to wait for ES to be gone once the hook has fired

What was measured: **48.3 to 68.2 ms**, three of three. The poll is cheap, which is the result that lets the pass poll rather than guess. It is still the only correct order: the hook fires while ES is alive, and a key written in that window is inside the load-and-serialise window RB-178 measured, so the write waits for the process rather than for the hook

## RB-203. It does, and so does the gamelist entry, with no restart

The claim being checked: `es_menu` is an ordinary ES system, so `/reloadgames` ought to pick a new `.menu` up (**probe 4, reasoned rather than driven**)

What was measured: **It does, and so does the gamelist entry, with no restart.** With ES up: writing `zzprobe7a.menu` alone took `retrobat` from **92 to 93 games in 209 ms**, listed under its bare filename with no image. Adding the `<game>` element and reloading again took **262 ms** for the name to become `RomMBat probe` and for ES to serve an image URL for it. So `sync` can tell the user the entry is ready rather than telling them to restart the front end

## RB-208. It answers 200 and does nothing, so it belongs with `/quit`, `/emukill` and `/reloadgames` rather than apart

The claim being checked: `POST /launch` launches a game, the rom path as the raw request body (**probe 3, [`docs/PLAN.md`](https://github.com/Spinnich/rommbat/blob/ad1c43006/docs/PLAN.md), the `retrobat-layout` skill**)

What was measured: **It answers 200 and does nothing, so it belongs with `/quit`, `/emukill` and `/reloadgames` rather than apart from them.** Probe 3 recorded it "confirmed from the API's own page at `/`", which is documentation rather than a drive. Driven now, twice, with the exact path `/systems/mastersystem/games` reports and with an explicit `text/plain` content type: 200, an empty body, and **`emulatorLauncher.log` did not grow by one byte** and no emulator process appeared. So the API cannot start a game, and a hands-on pass covering `game-start` and `game-end` needs a person at the controller

## RB-211. How long the quit pass waits for ES in practice

The claim being checked: (not addressed) how long the quit pass waits for ES in practice

What was measured: **10 ms**, one poll, on the first real session. Consistent with RB-201's 48 to 68 ms, which was measured from `GET /quit` rather than from the moment the pass starts polling

## RB-233. It is deferred, not discarded, and ES does not rescan on resume by itself

Question: **107 and 203 disagree about `/reloadgames`, and stage 7b-2 needed to know which applies when RomMBat is the app in front** (probe 6)

Measured: **It is deferred, not discarded, and ES does not rescan on resume by itself.** Five phases on the live 8.2.1 install, one variable, minutes apart, polling `/systems` for `retrobat`'s `totalGames` rather than trusting the 200. **Control**, nothing in front: a `.menu` written and reloaded took **93 to 94 in under 300 ms**, so 203 still holds. **Live**, RomMBat up from the ES menu: a second marker plus `GET /reloadgames` answered **200 in 6 ms** and the count was **unchanged at 94 after 10 s**, so 107 applies to RomMBat exactly as it does to a game. **On exit the count was 95 before any further call**, so the deferred reload had been applied on resume. **The discriminating phase**: a third marker written with RomMBat in front and **no reload issued at all** left the count at 95 both during and after, and a reload issued afterwards took it to **96**, proving the third marker was valid all along and simply unqueued. So a reload issued behind RomMBat is **queued and applied when RomMBat exits**, and ES rescans on resume only because it was asked to. **The consequence for the design is the opposite of the one assumed**: `sync` from the interface should still call `/reloadgames` after writing gamelists, and the games appear the moment the user leaves RomMBat, which is when they would look. No workaround is owed and `GamelistSync`'s write-then-reload is correct unchanged. Also recorded: EmulationStation was **unfocused throughout** and the control reload worked anyway, so ES's own reload does not depend on focus

## RB-383. The `.menu` format

`system/es_menu/*.menu` is a plain text file, not XML. Line 1 is the executable, subsequent
lines are arguments:

```text
\retroarch\retroarch.exe
```

```text
\pico8\pico8.exe
-home .\..\..\emulators\pico8 -root_path .\..\..\roms\pico8 -desktop .\..\..\screenshots\pico8
```

## RB-384. Measured: `.menu` paths are rooted at `emulators\` and cannot escape upward

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

## RB-385. `es_menu` is an ordinary ES system, and registration takes two files

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

## RB-386. EmulationStation runs an HTTP API, and it is the refresh mechanism

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

**`/reloadgames` is in the same state, measured during M4 (RB-107).** With RetroArch up
it answered 200 in 1 ms and a ROM added to the folder was still unreported five seconds
later. The one route M4 depends on is therefore not exempt: a sync that writes a gamelist
while a game is running has to reload again afterwards rather than treat the 200 as done.

**It works on loopback with `PublicWebAccess` absent from `es_settings.cfg`**, that is at
ES's default. The binary also carries the string `HttpServerThread : Access disabled for`, and the UI
exposes "ENABLE PUBLIC WEB API ACCESS" under FRONTEND DEVELOPER OPTIONS showing
`http://<addr>:1234`. Read together: the server always runs, and the setting gates
**non-local** callers only. **So RomMBat can refresh ES without asking the user to change any
setting**, which is a much better position than the plan assumed.

## RB-387. `/reloadgames` really reloads, proven against a control

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

## RB-389. What the plan was looking for, and why it is not there

- **`-updatestores` has nothing to do with gamelists.** It drives `batocera-store.exe`, the
  content downloader. `emulatorLauncher`'s complete update-ish switch set is `-updatestores`,
  `-updateall`, `-updatepo`, `-collectversions`. There is no gamelist switch. The confusion
  is understandable: `updatestores.bat` ships in **both** the `start` and `update-gamelists`
  event folders, which makes the two look related.
- **ES's own CLI cannot refresh a running instance.** Its switches are startup-only:
  `--gamelist-only`, `--ignore-gamelist`, `--home`, `--force-kiosk`, `--windowed` and so on.

## RB-390. What a reload costs, now that probe 5 has measured it

`GET /reloadgames` answers in **1-2 ms** and does the work afterwards, so its response time
is not a completion signal. Timing the effect instead, by changing the library on disk and
polling until ES reports it: **269 ms** for a 200-entry system and **1084 ms** for 100,000.
Poll `/systems`, which is a few KB and carries `totalGames`, not
`/systems/<system>/games`, which serialises the whole library (99 MB at 100k entries) and
loads ES down enough to distort what is being measured.
