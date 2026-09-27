---
summary: EmulationStation's HTTP API, the ES menu and `.menu` registration, and what reload, quit and launch really do.
read-when: Before calling ES's HTTP API, registering the ES menu entry, or relying on a reload or a quit.
---

# RetroBat: EmulationStation

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-383. A `.menu` is plain text: the executable, then its arguments

Verified: RetroBat 8.2.0, 2026-08-08. How: read the shipped `system/es_menu/*.menu` files.
Line 1 is the executable and each later line is arguments, not XML:

```text
\pico8\pico8.exe
-home .\..\..\emulators\pico8 -root_path .\..\..\roms\pico8 -desktop .\..\..\screenshots\pico8
```

## RB-384. A `.menu` executable path resolves under `emulators\` and cannot escape it

Verified: RetroBat 8.2.0, 2026-08-08. How: three `.menu` variants differing only in the executable line, each launched from the ES menu.

| Executable line                             | Result                                           |
| ------------------------------------------- | ------------------------------------------------ |
| `..\..\plugins\rommbat\zz-probe.bat`        | refused: `[Generator] Failed. path is null`, 204 |
| `\plugins\rommbat\zz-probe.bat`             | refused, the same way                            |
| `\rommbat\zz-probe.bat`, under `emulators\` | launched                                         |

A leading backslash is not root-relative. The launched process gets its own directory as its
working directory, a `.bat` target is accepted, and the argument line arrives intact. So
RomMBat installs at `emulators/rommbat/` and its line is `\rommbat\RomMBat.exe`.

## RB-385. `es_menu` is an ordinary ES system, so registration takes two files

Verified: RetroBat 8.2.0, 2026-08-08. How: read `es_systems.cfg` and the stock `system/es_menu/gamelist.xml`.
`es_systems.cfg` declares `es_menu` as the `retrobat` system with `<extension>.menu</extension>`
and a command of `emulatorLauncher.exe -system retrobat -rom %ROM%`. A `.menu` is a ROM of that
system, and `emulatorLauncher` parses it, not ES. The `.menu` supplies the command. The name,
description and artwork come from a `<game>` element in `system/es_menu/gamelist.xml` whose
`<path>` names it (`./retroarch.menu`), and its paths are relative. `EsMenuEntry` writes both,
merging its one element into a file other entries share (RB-205).

## RB-203. A reload picks up a new `.menu` and its gamelist element, with no restart

Verified: RetroBat 8.2.0, 2026-08-24. How: wrote a `.menu`, then its `<game>` element, under a running ES, polling `/systems`.
The `.menu` alone took `retrobat` from 92 games to 93 in 209 ms after `GET /reloadgames`,
listed under its bare filename with no image. Adding the `<game>` element and reloading again
gave it its name and artwork in 262 ms. So `sync` reports the entry ready rather than asking for
a restart.

## RB-386. ES serves an HTTP API on `127.0.0.1:1234`, with no setting changed

Verified: RetroBat 8.2.0, 2026-08-08. How: called every route but `/launch`, listed from ES's own page at `/`, against a running ES with `PublicWebAccess` absent from `es_settings.cfg`; read `emulatorLauncher`'s switches and ES's startup options.

| Route                     | Method | Returns                                             |
| ------------------------- | ------ | --------------------------------------------------- |
| `/reloadgames`            | GET    | 200, empty. Rescans roms and re-reads gamelists     |
| `/systems`                | GET    | JSON: name, fullname, extensions, `totalGames`, ... |
| `/systems/<system>/games` | GET    | JSON: `name`, `desc`, `image` per game              |
| `/caps`                   | GET    | `{"Version":"8.2.0-stable-win64","SortName":false}` |
| `/quit`                   | GET    | Closes ES                                           |
| `/emukill`                | GET    | Kills the running emulator                          |
| `/launch`                 | POST   | Does nothing (RB-208)                               |

`POST /reloadgames` is 404. `PublicWebAccess` gates only non-local callers. Neither
`-updatestores`, which drives the content store, nor ES's command line, whose switches are
startup-only, can refresh a running ES. RomMBat calls `/reloadgames` and `/systems` and nothing
else.

## RB-387. ES serves its in-memory model, and `/reloadgames` re-reads the disk

Verified: RetroBat 8.2.0, 2026-08-08. How: renamed a game in `gamelist.xml` on disk, read `/systems/ports/games` before and after a reload.
After the disk edit ES still reported the old name. After `GET /reloadgames` it reported the
new one, and a rom added to the folder appeared, with no restart. So `/systems/<system>/games`
is ES's model, not the filesystem, and a reload re-reads both the rom folder and the gamelist.

## RB-390. A reload answers at once and takes effect later

Verified: RetroBat 8.2.0, 2026-08-08. How: changed a library on disk, then polled `/systems` until ES reported it.
`GET /reloadgames` answers in 1 to 2 ms and does the work afterwards, so its response time
signals nothing. The change is visible in 269 ms at 200 entries and 1,084 ms at 100,000. Poll
`/systems`, a few KB carrying `totalGames`, not `/systems/<system>/games`, which reached 99 MB
at 100,000 entries and loads ES enough to distort the measurement.

## RB-35. `/quit` and `/emukill` answer 200 and do nothing while a game runs

Verified: RetroBat 8.2.0, 2026-08-08. How: called both with RetroArch up, then again after closing it.
Both returned cleanly and left ES and RetroArch running, while `/caps` kept answering. After
RetroArch was closed by other means, a re-issued `/quit` worked at once. A 200 from this API is not evidence the action happened, so anything that needs ES gone polls
for the process rather than trusting the response.

## RB-107. `/reloadgames` has no effect while a game runs

Verified: RetroBat 8.2.0, 2026-08-11. How: added a rom with RetroArch up, reloaded, polled `/systems` for 5 s, twice.
It answered 200 in 1 ms and the new rom was still unreported five seconds later, the same trap
as RB-35. What happens once the emulator exits was measured with RomMBat in front instead
(RB-233).

## RB-233. A reload issued behind an app is deferred, not discarded

Verified: RetroBat 8.2.1, 2026-08-26. How: five phases writing a `.menu` marker, with and without RomMBat in front and with and without a reload, polling `totalGames`.
With RomMBat in front, a reload answered 200 in 6 ms and the count held for 10 s, then rose as
RomMBat exited with no further call. A marker written with no reload issued stayed uncounted
after RomMBat exited, until a later reload. So ES applies a queued reload on resume and never
rescans by itself. The reload worked with ES unfocused. RomMBat's sync screen issues the reload
after writing gamelists, and the games appear when the user leaves it.

## RB-208. `POST /launch` answers 200 and launches nothing

Verified: RetroBat 8.2.0, 2026-08-24. How: posted the exact path `/systems/mastersystem/games` reports, as the raw body with and without `text/plain`, twice.
The response was 200 with an empty body, `emulatorLauncher.log` did not grow by a byte, and no
emulator process appeared. So the API cannot start a game, and a hands-on pass that covers
`game-start` and `game-end` needs a person at the controller.

## RB-108. With ES absent, a loopback connect is refused after 2.04 s

Verified: RetroBat 8.2.0, 2026-08-11. How: five raw TCP connects and three `HttpClient` requests to `127.0.0.1:1234` with ES closed.
Every one took 2.04 s, the closed-port figure RB-353 recorded on the LAN. ES being absent is the
ordinary case for a background sync, and the project's 2 s interactive timeout would buy
nothing, so `EmulationStationClient` sets a 400 ms `ConnectTimeout`.

## RB-201. ES is gone within 70 ms of the `quit` hook firing

Verified: RetroBat 8.2.0, 2026-08-24. How: timed three sessions from the `quit` hook's stamp to the ES process exiting, then read the first real `background quit` pass.
The process was gone 48.3, 52.4 and 68.2 ms after the hook stamped itself. The first real pass
saw it gone at its first poll, 10 ms in. The hook fires while ES is still alive and can still
write `es_settings.cfg`, so `background quit` with queued config polls for the process before
applying it, with a 30 s budget sized for a stalled shutdown rather than the usual one.
