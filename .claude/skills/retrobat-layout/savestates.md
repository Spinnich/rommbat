# State configuration

Part of the [retrobat-layout](SKILL.md) skill. How RetroBat declares where each emulator keeps its states.

## es_savestates.cfg is the authority on save states

Per-emulator templates for `<directory>`, `<file>`, `<image>`, `<autosave_file>` and
`<autosave_image>`, plus `firstslot`/`lastslot` and `autosave`/`incremental` flags.
Placeholders: `{{system}}`, `{{core}}`, `{{romfilename}}`, `{{slot}}`, `{{slot0}}`,
`{{slot2d}}`.

Parse it. Never hardcode state paths. `<image>` maps onto RomM's optional `screenshotFile`.
Note the `libretro` entry is core-scoped (`{{system}}/libretro.{{core}}`), so the same game
has independent state sets per core.

**Do not go looking for `sort_savestates_enable` in `retroarch.cfg` to explain the core
folder.** Other RetroArch front ends derive that segment from the sort flag, whose default
when the key is absent is **on** for savestates and **off** for savefiles, an asymmetry that
silently misplaces a state. RetroBat never leaves it to the default: `emulatorlauncher` writes
all four sort keys as `"false"` on every launch and bakes the core into the path instead
(`savestate_directory = "<root>\saves\mastersystem\libretro.genesis_plus_gx"`). Verified on a
real 8.2.1 install against states from four cores on disk. Two consequences: the hazard does not
exist here, and the folder is named **`libretro.<core>`**, RetroBat's own convention, not the
libretro `corename` that front ends reading `retroarch.cfg` produce. `es_savestates.cfg` is the
source, and it is the stronger one because `retroarch.cfg` is regenerated per launch and
describes only the last game run. See
[argosy-findings.md](../../../docs/argosy-findings.md), A7.

**Trust `<file>`, verify `<directory>`.** Across the twelve emulators M0 drove, every `<file>`
template was correct and one `<directory>` declaration still is not: **`openmsx` writes
`bios/openmsx/savestates/`**, outside the saves tree entirely, against a declared
`saves/msx1/openmsx`. So never read an empty declared directory as "this game has no states",
and cross-check against the emulator's generated config where it matters.

**A `<core>` override is honoured for `enabled` and ignored for `system` and `directory`.**
`SaveStateSchema.ReadCores` parses all three, and `MatchDirectory`, the only consumer, reads
`Enabled` alone. `<defaultCoreDirectory>` is not parsed at all. Nothing that ships takes this
path: RetroBat 8.2.1 carries both only as a commented-out sample under `libretro`
(`<core name="fceumm" system="nes" directory="{{system}}"/>`). A user who uncomments it gets the
`enabled="false"` half, and states written under an overridden directory are neither found nor
reported. **Applying `directory` is not a one-line change, because it breaks the reverse
lookup.** Discovery matches `<directory>` templates against directories that exist, which is how
it recovers the system and the core. With `fceumm` overridden to `{{system}}` the directory on
disk is `saves/nes`, which matches with `system=nes` and **no core**, and `libretro` is
core-scoped, so `SaveStateTemplate.Create` answers null and the directory yields nothing. The fix
compiles each overridden core's template as its own directory pattern bound to that core, and
parses `<defaultCoreDirectory>` in the same change, since it is the same mechanism's default.
Not planned while nothing ships it (#33).

**The inverse is also real, and it is worse, because nothing in the file hints at it.** An emulator
with **no entry at all** still writes save states, into a directory it names itself. Measured on
`nes` by driving every emulator the system declares:

| Emulator   | `es_savestates.cfg` | Writes states to                     |
| ---------- | ------------------- | ------------------------------------ |
| `mednafen` | no entry            | `saves/nes/mednafen/sstates/*.mc0`   |
| `mesen`    | no entry            | `saves/nes/mesen/SaveStates/*_1.mss` |
| `ares`     | no entry            | `saves/nes/ares/<profile>/*.bs1`     |

`StateScanner` reads states only through a declaration, so these were invisible to state sync:
not scanned, not uploaded, not restorable. **Never read "declares no state directory" as "this row
has no states"**, which is the reading `docs/platforms/README.md` was built on for 30 of wave 1's
81 rows. Issue #150.

**On `nes` all three are declared now, by RomMBat rather than by RetroBat, and on `megadrive`
mednafen, ares and kega-fusion are. On `snes` so are mednafen, mesen, ares and standalone `snes9x`, whose
slots are `.000` to `.009` under `snes9x/sstates/` (RB-318). On `mastersystem` so are mednafen, mesen,
ares under `ares/Master System/`, and kega-fusion as `.ss<slot>` rather than `megadrive`'s `.gs`
(RB-323).**
`data/retrobat/es_savestates.supplement.xml` is `es_savestates.cfg`'s own format plus a `systems`
attribute, and `StateScanner.LoadSchema` reads the install's file with it beneath: an entry the
install declares always wins, and a supplement entry answers only for the systems it names, so
`saves/pcengine/mesen/` stays undeclared. mednafen's entry uses `{{romhash}}`, RomMBat's own token for
the 32-hex md5 it puts in the name (RB-274). **Add a row to the supplement only from a hands-on
pass**, scoped to the system it was driven on: ares keeps `nes` under `ares/Famicom/` and
`megadrive` under `ares/Mega Drive/`, each named after its own system, so nothing about one
system's layout carries to the next. The supplement may carry one entry per system for the same
emulator, `SaveStateSchema.For(emulator, system)` picks between them, and an install that declares
the emulator itself drops every supplement entry for it (RB-281).

**Kega Fusion writes its battery saves outside `saves/`.** RetroBat's template `Fusion.ini` sets
`SRMFiles` to `emulators\kega-fusion` and `StateFiles` to `saves\megadrive\kega-fusion`, and
`emulatorLauncher` rewrites neither per launch, so its `.srm` never reaches a tree RomMBat reads.
Treat it as RetroBat's to fix, not as a second tree to scan, and never write the key (rule 2).
RB-283. RetroBat ships no Kega Fusion either: the folder holds only that template until ES
downloads the emulator on a first launch.

A row still undeclared on another system is not silent, though, and the difference matters to
whoever fixes it. `SaveScanner.CountFiles` excludes only the directories `StateScanner.LoadSchema`
declares, which is `es_savestates.cfg` plus the supplement for that system, so an undeclared
state directory is counted as unsyncable and `AddSubdirectories` names it in the row it prints.

**`AddSubdirectories` prints two rows, and which one a directory lands in is the emulator's
declaration rather than the directory's path.** An emulator the file names goes to the
`NotInThisVersion` row, whose sentence still ends "the save states beside them", true there. One
it does not name goes to a `NoStateDeclaration` row, which repeats the shape half and replaces
that clause with why the states are invisible. The split asks `SaveStateSchema.For` and not
`MatchDirectory`, because the declared template usually sits below this level: BizHawk declares
`{{system}}/bizhawk/sstates/{{core}}`, so `saves/nes/bizhawk/` matches no state directory while
the emulator is very much declared.

**The directory name is not always the declared name, so it goes through a map first.**
`es_savestates.cfg` declares `name="dolphin"` with `<directory>{{system}}/dolphin</directory>`,
and the save tree RetroBat writes beside it is `dolphin-emu` (RB-361, and `save_shapes.json`
carries `dolphin-emu` for both the gamecube and wii `unit_paths`). Asking `For("dolphin-emu")`
returns null, so without the map `saves/gamecube/dolphin-emu/` and `saves/wii/dolphin-emu/` would
be reported under `no_state_declaration` on every install, for the one emulator whose save states
are measured working (RB-368). `SaveScanner.DeclaredNames` is that map and has one entry;
`mame`, `ppsspp` and `rpcs3` were checked against all 13 declared names and need none. **Add to
it whenever a new row's save directory is spelled differently from its `es_savestates.cfg`
name**, because nothing upstream publishes the correspondence.

Two things that row deliberately does not claim. It does not say the states are lost, because
RetroBat may mirror them into a declared path (PPSSPP writes the undeclared `psp/PPSSPP_STATE/`
and is mirrored live into `psp/ppsspp/`), and nothing at that point can tell that case from
`mednafen`. And it does not fire at all when no `es_savestates.cfg` was found, because its claim
is that the file was read and does not name the emulator, which an install without the file
supports neither half of.

**`flycast` was the second and no longer is.** It wrote `dreamcast/reicast/states` against a
declared `dreamcast/flycast/sstates` on 8.2.0; RetroBat 8.2.1 fixed that
(`emulatorlauncher#1336`) by pointing the save-state watcher at the directory Flycast really
writes. Confirmed by hand, three runs: the state lands in both, same bytes, same millisecond,
live. Flycast still writes `reicast/states` first and `emu.cfg`'s `Dreamcast.SavestatePath`
still names it, so **the declaration became usable without the template moving**. Re-run
`tools/m0-probes/probe2-flycast-mirror.ps1` if that ever looks doubtful; a changelog line is
not a measurement, which is why this one was driven.

**The declared directory is otherwise the one to use even when the emulator writes elsewhere.**
An emulator may write under its own naming, with RetroBat mirroring into the declared path
about 120 ms later while the game is still running (PPSSPP:
`psp/PPSSPP_STATE/<GAMEID>_<ver>_<slot>.ppst` mirrored to
`psp/ppsspp/<rom filename>_<slot>.ppst`). ES passes the launcher `-state_slot` and
`-state_file` naming the **declared** path, and the launcher hands it to the emulator, so a
state written there is loaded. A manual save mirrors live; an autosave state appears only at
exit. `libretro` needs no mirroring, since RetroArch is pointed at the declared path directly
via `savestate_directory`. Nor does it take its slot from `-state_slot`: with
`savestate_auto_index` on, RetroArch continues from the highest slot already in that directory
(RB-261). `bizhawk` is the reverse: `emulatorLauncher` writes `-state_slot` into EmuHawk's
`config.ini` as `SaveSlot`, and the pad's save key writes to that slot (RB-269).

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
