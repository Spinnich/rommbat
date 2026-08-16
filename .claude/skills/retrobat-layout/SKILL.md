---
name: retrobat-layout
description: RetroBat's on-disk layout and integration seams - folder tree, es_systems.cfg, es_savestates.cfg, es_settings.cfg precedence, ES event hooks, and the ES menu entry. Use when reading or writing anything inside a RetroBat install.
---

# RetroBat layout

RomMBat integrates purely through seams RetroBat already has. **Do not fork RetroBat.**

## Tree

| Path                                        | Use                                                     |
| ------------------------------------------- | ------------------------------------------------------- |
| `roms/<system>/`                            | ROMs. Folder names come from `es_systems.cfg`           |
| `roms/<system>/gamelist.xml`                | Metadata ES reads directly                              |
| `roms/<system>/images`, `videos`, `manuals` | Media siblings, named after the ROM file                |
| `saves/`                                    | Emulator save output                                    |
| `bios/`                                     | BIOS and firmware, flat at the root with few exceptions |
| `emulationstation/.emulationstation/`       | ES home: `es_settings.cfg`, `scripts/`, themes          |
| `system/es_menu/*.menu`                     | How RetroBat registers launchable apps in the ES menu   |
| `system/version.info`                       | The version, used for the compatibility gate            |

Locate the root by walking up from `AppContext.BaseDirectory` to a marker
(`retrobat.ini`, `emulationstation/`, `roms/`). Registry lookups are a fallback for fixed
installs only, never the primary path.

## es_systems.cfg is the authority on systems and extensions

Read the **live** copy, not the vendored template: it reflects that machine's actual
configuration. Each `<system>` carries `<name>`, `<fullname>`, `<manufacturer>`,
`<hardware>`, `<release>`, `<path>`, `<extension>` and `<command>`.

**The folder is `<path>`, not `<name>`.** They are different vocabularies and five systems
in the shipped 8.2.0 file disagree: `gw` writes to `gameandwatch`, `powerbomberman` to `pb`,
`casloopy` to `loopy`, `Windows` to `windows`, and `starship` is used **twice**, for
`ghostship` and `starship`, so `<name>` is not even unique. Four entries own no folder under
`roms/` (`library`, `screenshots`, `kodi`, and `retrobat` at `system/es_menu`) and `mess`
declares no path; none is a sync target. `~` expands to `<root>/emulationstation`, so the
ubiquitous `~\..\roms\<folder>` resolves to `<root>/roms/<folder>`. Match
case-insensitively, and parse it as XML: `arcade` and `kodi` sit inside comments, which a
regex over `<system>` would wrongly pick up.

`<extension>` is a **sync filter**. Syncing a file the emulator cannot launch produces the
worst failure this app has: a game that appears in ES, looks right, and dies on launch.

`<manufacturer>`, `<hardware>` and `<release>` let the platform rollout order be derived
rather than hand-maintained.

## es_savestates.cfg is the authority on save states

Per-emulator templates for `<directory>`, `<file>`, `<image>`, `<autosave_file>` and
`<autosave_image>`, plus `firstslot`/`lastslot` and `autosave`/`incremental` flags.
Placeholders: `{{system}}`, `{{core}}`, `{{romfilename}}`, `{{slot}}`, `{{slot0}}`,
`{{slot2d}}`.

Parse it. Never hardcode state paths. `<image>` maps onto RomM's optional `screenshotFile`.
Note the `libretro` entry is core-scoped (`{{system}}/libretro.{{core}}`), so the same game
has independent state sets per core.

**Trust `<file>`, verify `<directory>`.** Across the seven installed emulators M0 drove, every
`<file>` template was correct and one `<directory>` was not: **`flycast` writes
`dreamcast/reicast/states`, not the declared `dreamcast/flycast/sstates`**, which exists and
stays empty. RetroBat's own launcher config (`emulators/flycast/emu.cfg`,
`Dreamcast.SavestatePath`) disagrees with its own `es_savestates.cfg`. So never read an empty
declared directory as "this game has no states", and cross-check against the emulator's
generated config where it matters.

**The declared directory is otherwise the one to use even when the emulator writes elsewhere.**
An emulator may write under its own naming, with RetroBat mirroring into the declared path
about 120 ms later while the game is still running (PPSSPP:
`psp/PPSSPP_STATE/<GAMEID>_<ver>_<slot>.ppst` mirrored to
`psp/ppsspp/<rom filename>_<slot>.ppst`). ES passes the launcher `-state_slot` and
`-state_file` naming the **declared** path, and the launcher hands it to the emulator, so a
state written there is loaded. A manual save mirrors live; an autosave state appears only at
exit. `libretro` needs no mirroring, since RetroArch is pointed at the declared path directly
via `savestate_directory`.

Watch for a `.txt` sidecar carrying the native basename: RetroBat writes it beside the state
unconditionally, and it belongs with the state. **Its contents vary by emulator and one of them
is useful**: some hold nothing but the rom filename, while DuckStation's holds the bare disc
serial (`SLUS-00594`), which is the join key a database-named memory card otherwise has to be
reverse engineered from. Read it rather than assuming. See `save-sync` for the unreliable
`<image>`.

**A declaration is not an installation.** Six of the thirteen emulators in `es_savestates.cfg`
had no executable on a real, well-used install: RetroBat downloads emulators on demand. Check
for the binary before promising state sync for a system.

**And installation is not launchability.** Launching an uninstalled emulator raises a modal
"install now?" dialog with **no window title and no timeout**, which blocks that launch
indefinitely; launchers were found still waiting on it seven hours later. And **`bizhawk`
crashes in `BizhawkGenerator.CreateControllerConfiguration` when the launcher is invoked
without `-core`** (`inputPortNb[core]` is unguarded), which ES never does but a direct
invocation easily does, so **always pass `-core` when driving `emulatorLauncher` yourself**.
Both failures leave the launcher hung or gone with no game started, so detect them from the
launcher rather than recording a play session that did not happen.

## es_settings.cfg is how you configure emulators

`emulatorlauncher` regenerates each emulator's INI from ES options at every launch, so
**editing an emulator INI is pointless: it gets clobbered on the next boot.** Write the
option instead. Precedence (`emulatorlauncher/Program.cs`):

```text
es_settings.cfg -> global.<key> -> <system>.<key> -> <system>["<rom filename>"].<key>
```

That last form is a real per-game override, measured in M0: `emulatorlauncher` honours it, it
outranks the system key, and it affects only its own rom. **Write the rom filename with its
extension** (`ps2["Game (USA).iso"].pcsx2_slot1_memory`). A bare stem is ignored **silently**,
so build the key from `fs_name` and never from a stripped name.

Keys read from the live `es_features.cfg`, with the value RomMBat should set:

| Key                       | Choices                                                 | Set to          | Why                                    |
| ------------------------- | ------------------------------------------------------- | --------------- | -------------------------------------- |
| `duckstation_memcardtype` | `PerGameTitle`, `Shared`, `PerGameFileTitle`, `PerGame` | **leave unset** | stock already binds a disc set         |
| `pcsx2_slot1_memory`      | `standard`, `folder`, `game`                            | **`game`**      | names the card after the rom basename  |
| `dolphin_slotA`           | `8` (GCI folder), `1` (memory card)                     | **`8`**         | already the stock default              |
| `flycast_vmupergame`      | switch, unset by default                                | **on**          | per-game VMU, port 1, **serial-keyed** |

Leave `duckstation_memcardtype` alone. The stock `PerGameTitle` keys the card by DuckStation's
internal database title, which sounds worse than a filename key until a multi-disc set is
driven: the title is `gamedb.yaml`'s `saveName` with the disc marker stripped, so the whole set
shares one card while regions stay separate. `PerGameFileTitle` keys on three separate
filenames and splits it. Also watch `dolphin_sync_saves`, which has RetroBat copying saves
between the dolphin and libretro-dolphin folders on its own.

**ES rewrites `es_settings.cfg` on exit, but only when a setting changed that session.** A
start-and-quit, and even a session that launched a game, leave it untouched. When ES does
rewrite it, it **keeps keys it does not recognise** (a nonsense per-game key survived
intact), so the override is durable. Still merge rather than clobber, write while ES is idle,
and write atomically, for the ordinary reason that two writers share the file.

**ES prunes any setting equal to its own default** on that rewrite, so an entry written at
the stock value disappears. Never read a missing entry as the user having reverted something.

`GET http://127.0.0.1:1234/quit` closes ES cleanly **only when no game is running**. With an
emulator up, `/quit` and `/emukill` both return 200 and do nothing. Poll for the process to
exit rather than trusting the response. Changing a user's emulator config is opt-in and
reversible.

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
`docs/retrobat-findings.md` probe 1. The load-bearing results:

- **Hooks do not block game launch.** The launcher starts ~30 ms after the hook fires,
  regardless of how long the hook runs. They are fire-and-forget.
- **They do run concurrently**, with each other and across events. Three `game-end` hooks
  were seen in flight at once, interleaving writes to one file. A lock file is mandatory and
  the journal must survive interleaved appends from separate processes.
- **`game-start` fires for every game**, contrary to an earlier reading. It is the `.bat`
  that never starts when the display name contains a space. An exe hook is unaffected.
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
- **`game-end` gets none, and fires without a matching `game-start`** for ES-menu launches
  and for failed launches. RomMBat's own exit produces one. Discard orphans.
- **Every script in an event folder runs**, alphabetically, so install beside
  `updatestores.bat` rather than replacing it.

## gamelist.xml

Merge, never clobber; write atomically via temp file plus rename; include only locally
present ROMs. **Key generation by resolved folder, not by platform**, because two RomM
platforms can share one folder.

**Own an allowlist of the fields you write, never a blocklist of ES's.** The four the plan
first named are not the surface. Across 4,531 entries in 32 gamelists from a real scraped
install: `playcount` 115, `lastplayed` 115, **`gametime` 114**, and **no `favorite` and no
`hidden` at all**, plus `scrap` 4,525 (self-closing, `name` and `date` attributes),
`id` on `<game>` 4,493, `cheevosHash` 4,187, `md5` 2,815, `cheevosId` 2,329,
`arcadesystemname` 568, `multidisk` 161, `crc32` 8.

**Media is named after the ROM file**, stem being the file name without its extension:
`images/<stem>-image.png`, `images/<stem>-thumb.png`, `images/<stem>-marquee.png` (marquee
lives under `images/`, not its own folder), `videos/<stem>-video.mp4`,
`manuals/<stem>-manual.pdf`. Those are the exact names a user's own scrape writes, so never
delete one RomMBat did not create.

**After writing, call `GET http://127.0.0.1:1234/reloadgames`.** M0 measured that ES keeps a
stale in-memory model until asked to reload, and rewrites `gamelist.xml` from that model when
it exits. Write-then-reload makes the edit stick and takes effect immediately; write without
reloading and ES can serialise its stale copy over you. ES writes no `<game>` entry for a rom
it has no metadata for, and **does not list a `<game>` whose `<path>` names a file that is not
on disk**, so a stale entry is inert rather than a phantom game.

**Do not depend on ES preserving what it read.** When it has a reason to rewrite the file it
**drops every XML comment**, at document level and inside a `<game>` alike; moves the entry it
changed to the end; rewrites that entry's children into its own order
(`path,name,desc,genre,rating,releasedate,developer,publisher,players,favorite,playcount,lastplayed,gametime,lang,region,...`);
and prunes `<hidden>false</hidden>` as a default, the same behaviour it has on
`es_settings.cfg`. Unknown elements and attributes do survive. When it has no reason, it
leaves the file **byte-identical, mtime included**, so a no-churn assertion is meaningful but
has to be made about the file ES left behind.

**`/reloadgames` returns in 1-2 ms and does the work afterwards**, so its response is not a
completion signal. Time to the change being visible was 269 ms for a 200-entry list and
1.1 s for 100,000. Poll `/systems` (a few KB, carries `totalGames`) rather than
`/systems/<system>/games`, which serialises the whole library.

**And it is ignored outright while a game is running**, 200 in 1 ms with nothing reloaded,
exactly as `/quit` and `/emukill` are. Reload again after the game ends rather than treating
the 200 as done. With ES absent, which is the ordinary case for a background sync, the connect
is **refused after 2.04 s** on loopback, so this client needs a `ConnectTimeout` far below the
2 s used for reachability or every sync pays it.

**Size is not the constraint you would expect.** ES loaded a 100,000-entry, 65 MB gamelist
in 2.07 s from a cold start for 419 MB of working set, roughly 2 MB per 1,000 entries, and
2.93 s with a real image file per entry. **Do not cap a gamelist**, though: ES lists ROM
files it has no entry for, so dropping entries hides no games and only strips their art,
leaving the user the same number of tiles to scroll past. What bounds navigability is the
sync set's own game cap. Report a folder that grows past a threshold; never truncate it.

## The EmulationStation HTTP API

ES serves an API on `127.0.0.1:1234` whenever it is running. It works on loopback with the
`PublicWebAccess` setting untouched, because that setting gates only non-local callers, so
using it requires no change to the user's configuration.

| Route                     | Method | Use                                                |
| ------------------------- | ------ | -------------------------------------------------- |
| `/reloadgames`            | GET    | Rescan roms and re-read gamelists, no restart      |
| `/systems`                | GET    | Systems as JSON, including `totalGames`            |
| `/systems/<system>/games` | GET    | Games as JSON: `name`, `desc`, `image`             |
| `/caps`                   | GET    | `{"Version": "8.2.0-stable-win64", ...}`           |
| `/quit`                   | GET    | Close ES cleanly, before writing `es_settings.cfg` |
| `/emukill`                | GET    | Kill the running emulator                          |
| `/launch`                 | POST   | Launch a game; rom path as the raw body            |

`POST /reloadgames` is 404; the verb is GET. Treat the whole API as best-effort: it only
answers while ES is running, so every call needs a short timeout and a no-ES fallback.
