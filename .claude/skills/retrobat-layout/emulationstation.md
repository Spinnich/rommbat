# EmulationStation seams

Part of the [retrobat-layout](SKILL.md) skill. The menu entry, the event hooks and the HTTP API.

## The ES menu entry is two files, and one of them is somebody else's

`es_menu` is not a bespoke mechanism. `es_systems.cfg` declares it like any other system,
named `retrobat`, with `<extension>.menu</extension>`, so **a `.menu` is a ROM of that system
and the thing that parses it is `emulatorLauncher`, not EmulationStation.** Registration
therefore takes two files:

- `system/es_menu/<app>.menu`, plain text, no trailing newline. Line 1 is the executable and
  later lines are arguments. **The path resolves under `emulators\` and `..\` escapes are
  refused outright** (`[Generator] Failed. path is null`, exit 204), which is why RomMBat
  installs at `emulators/rommbat/` and why its line is `\rommbat\RomMBat.exe`. **The portable
  zip therefore carries the `emulators/rommbat/` prefix on every entry** and is extracted at
  the RetroBat root: a flat archive extracts to a tree whose menu entry cannot resolve its
  executable, and `hooks install` reports the hook missing.
- a `<game>` element in `system/es_menu/gamelist.xml` whose `<path>` names the `.menu`
  (`./rommbat.menu`). **Without it the entry shows under its bare filename with no artwork**,
  driven rather than assumed. Artwork convention: `<image>` and `<marquee>` both pointing at
  `./media/<name>-logo.png`, which is what all 92 shipped entries do.

**Both halves are picked up live.** With ES running, writing the `.menu` took the `retrobat`
system from 92 games to 93 in **209 ms** after `GET /reloadgames`, and adding the `<game>`
element gave it its name and artwork **262 ms** after a second reload. No restart.

**This gamelist is not encoded like the others and ES does not rewrite it.** The stock file is
**UTF-8 with a BOM and CRLF**, where all 42 `roms/<system>/gamelist.xml` measured across two
installs are neither; a writer that emits its own convention rewrites all 96 entries to add
one. And ES left it byte- and mtime-identical across three sessions, including one where it
had the change in its model, so **RomMBat is the only writer that can damage it**. Three of its
`<game>` elements are commented out (`citra_canary`, `yuzu-early-access`, `zsnes-dos`), which
is how RetroBat withdraws an entry whose markup it still ships, so a merge that drops comments
resurrects them. Its own indentation is inconsistent, two entries out of 93, so byte identity
against the stock file is not achievable and is not the assertion to write.

**Do not re-assert a field the user changed.** The name and the artwork are what they see on
their own front end. Fill in what is absent, report what differs, correct nothing. Same rule as
a per-game setting somebody else wrote.

## Event hooks

`.emulationstation/scripts/<event>/`. Nine folders ship: `start`, `game-start`, `game-end`,
`quit`, `shutdown`, `sleep`, `wake`, `update-gamelists`, `reboot`. ES also fires
`game-selected` and `system-selected` on every navigation move, with no folder for either.

**Write the hook as an `.exe`, never a `.bat`.** RetroBat's own `updatestores.bat` works only
because it takes no arguments. M0 measured both scripted forms failing to start on ordinary
rom names, silently and with no error anywhere:

| Form   | Fails when                           | Why                                                                                      |
| ------ | ------------------------------------ | ---------------------------------------------------------------------------------------- |
| `.bat` | any argument is quoted, so any space | ShellExecute uses `cmd /c "%1" %*`, whose quote-stripping rule mangles the line          |
| `.ps1` | the name contains `(`, `)` or `,`    | ES omits `-File`, so it is an implicit `-Command` and PowerShell parses the tail as code |
| `.exe` | not observed                         | arguments arrive through normal `CommandLineToArgvW` splitting                           |

Hooks resolve the agent relative to their own location, never an absolute path. **Mind the
depth**: a hook sits at `.emulationstation/scripts/<event>/`, so three levels up lands in
`emulationstation/` (where `emulatorLauncher.exe` lives) and reaching the RetroBat root takes
four. The agent is four levels up plus `emulators\rommbat\`. Do not rely on the working
directory; it differs by hook form.

**M0 measured the hook behaviour; do not assume the Batocera convention.** See
RB-346 to RB-352. The load-bearing results:

- **Hooks do not block game launch.** The launcher starts ~30 ms after the hook fires,
  regardless of how long the hook runs. They are fire-and-forget.
- **They do run concurrently**, with each other and across events. Three `game-end` hooks
  were seen in flight at once, interleaving writes to one file. A lock file is mandatory and
  the journal must survive interleaved appends from separate processes.
- **`game-start` fires for every game.** It is the `.bat` that never starts when the display
  name contains a space. An exe hook is unaffected.
- **Take the launch facts from `emulationstation/emulatorLauncher.log` anyway**, with
  `game-end` as the trigger. It carries rom path, `-system`, `-emulator` and `-core` with a
  millisecond timestamp and rotates across two files, and the hook is told none of those
  three. Open the journal record on `game-start`, but do not source facts from it.
- **`game-start` gets three arguments, not five**: `$1` absolute rom path, `$2` rom
  basename, `$3` gamelist display name. `$4` and `$5` are **empty**, so the **system,
  emulator and core are not available to the hook** even though `emulatorLauncher` receives
  all three. Batocera documents `$3` as the system; that is wrong here.
- **ES logs its scripting decisions only at `LogLevel=debug`** in `es_settings.cfg`, and
  logs `executing:` even for a process that never starts. Useful for diagnosis, not proof
  of execution.
- **A host can be unable to run a script at all.** In the M0 portable-move test the tree
  worked on a second PC while no hook produced anything. Two causes there, both silent:
  **Notepad++'s installer had taken the `.bat` association** (`HKCR\.bat` = `Notepad++_file`),
  and the PowerShell execution policy was the default **`Restricted`**. An `.exe` hook fires
  all four events there. This is the strongest reason the hook is an exe. Detect and report
  the state anyway; never assume silence means nothing was played.
- **`game-end` gets none.** It fires without a matching `game-start` for launches that
  **fail**, but a successful ES-menu launch fires **both**: driven live on 8.2.1, RomMBat's own
  menu entry produced a `game-start` carrying `system/es_menu/rommbat.menu` and a `game-end`
  carrying nothing. M0's "no preceding `game-start`" came from three launches driven by calling
  `emulatorLauncher.exe` directly, two of which failed. **So never key the discard on a missing
  `game-start`**: key it on the launcher log's `-system retrobat` with a rom under
  `system\es_menu\`, and discard the paired `game-start` with it. RB-221 and RB-222.
- **Every script in an event folder runs**, alphabetically, so install beside
  `updatestores.bat` rather than replacing it.
- **`start` and `quit` may start a process; `game-start` and `game-end` may not.** That is
  CLAUDE.md rule 4's boundary and it is the reason the rule exists rather than an exception to
  it: the rule forbids network work _because_ hooks run in the game-launch path, and only
  those two do. RomMBat's `start` and `quit` hooks spawn
  `emulators/rommbat/rommbat-agent.exe background <event>` detached, `UseShellExecute=false`,
  `CreateNoWindow=true`, no wait. **`CreateNoWindow` is load-bearing**: the agent is a console
  app and ES is full screen, so without it a console flashes over the front end at every boot.

## The EmulationStation HTTP API

ES serves an API on `127.0.0.1:1234` whenever it is running. It works on loopback with the
`PublicWebAccess` setting untouched, because that setting gates only non-local callers, so
using it requires no change to the user's configuration.

| Route                     | Method | Use                                            |
| ------------------------- | ------ | ---------------------------------------------- |
| `/reloadgames`            | GET    | Rescan roms and re-read gamelists, no restart  |
| `/systems`                | GET    | Systems as JSON, including `totalGames`        |
| `/systems/<system>/games` | GET    | Games as JSON: `name`, `desc`, `image`         |
| `/caps`                   | GET    | `{"Version": "8.2.0-stable-win64", ...}`       |
| `/quit`                   | GET    | Close ES. RomMBat does not call it             |
| `/emukill`                | GET    | Kill the running emulator                      |
| `/launch`                 | POST   | **Does nothing.** 200 and no launch; see below |

`POST /reloadgames` is 404; the verb is GET. Treat the whole API as best-effort: it only
answers while ES is running, so every call needs a short timeout and a no-ES fallback.

**A 200 from this API is never evidence the action happened**, and that now covers every route
that does something. `/quit` and `/emukill` are ignored while a game is running; `/reloadgames`
is too, and answers in 1-2 ms before doing the work either way; and **`POST /launch` does not
launch anything at all**.

**"Ignored" is the wrong word for `/reloadgames`, and the difference decides a design.** It is
**deferred, not discarded**: a reload issued while an app is in front of ES is queued and
applied when that app exits. Measured on 8.2.1 with RomMBat itself as the app in front, which
is the case that matters because an ES-menu launch is suspended exactly as a game is (finding
233):

| With RomMBat in front                               | `totalGames`                               |
| --------------------------------------------------- | ------------------------------------------ |
| marker written, reload issued, 200 in 6 ms          | unchanged for 10 s                         |
| RomMBat exits                                       | **the change lands, with no further call** |
| marker written, **no** reload issued, RomMBat exits | **no change, ever**                        |

The third row is the one that carries it: **ES does not rescan on resume by itself**, so the
call is still required, it simply takes effect later. **So issue `/reloadgames` after writing
gamelists even from the interface**, and expect the games to appear when the user leaves
RomMBat rather than while they are still in it. Do not build a workaround, do not tell the user
to restart the front end, and do not skip the call on the theory that ES will notice.

**The stop path is included.** The sync screen runs the same
`GamelistSync` pass the agent does, through `LibrarySyncService`, and it runs it **after a stop
as well as after a completed run**. A run that ended early still touched folders, and leaving
their lists unwritten would be work postponed rather than a run that stopped.

**A rolled-back game needs no gamelist handling of its own**, which is worth knowing before
adding some. `GamelistSync` writes from `local_file`, and the rollback removes the row with the
bytes, so a game that was taken back is simply never written. Verified on the live install: a
sync stopped mid-transfer left no row without a file, nothing under `partial/`, and the store
byte-identical to before the run.

The control reload worked with **ES unfocused**, so ES's own reload does not depend on focus.

**`POST /launch` answers 200 and launches nothing.** Driven twice with the exact path
`/systems/<system>/games` reports and an explicit `text/plain` body: an empty response,
`emulatorLauncher.log` did not grow by a byte, and no emulator process appeared (RB-208). **A
hands-on pass covering `game-start` and `game-end` needs a person at the controller; it cannot
be scripted through this API.**
