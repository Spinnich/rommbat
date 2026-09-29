# Emulator settings

Part of the [retrobat-layout](SKILL.md) skill. How RomMBat configures emulators, and when it may write.

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

**That chain is `emulatorlauncher`'s, and it covers feature keys only. Which emulator and core
run is not a feature key and is not in this file at all.** EmulationStation resolves that itself,
before `emulatorlauncher` exists, and passes the answer down as `-emulator` and `-core`. It reads
the per-game choice from **`gamelist.xml`**, as two children of the `<game>` element:

```xml
<emulator>libretro</emulator>
<core>fceumm</core>
```

Driven on 8.2.1: setting the emulator from ES's own game options menu left `es_settings.cfg`
**byte-identical** and added exactly those two elements, and ES moved the edited entry to the end
of the file as it does for any rewrite. Writing `nes["<rom>.zip"].emulator` into `es_settings.cfg`
instead is accepted, survives ES's startup and exit rewrites untouched, and **is never read**: a
whole session of launches ran the system-level `nes.emulator` / `nes.core` pair while fourteen such
keys sat in the file. So it fails silently and looks exactly like a working configuration.

Two consequences for RomMBat, which writes `gamelist.xml`:

- **The merge must not touch `<emulator>` or `<core>`.** It does not: `GamelistDocument` merges by
  an allowlist of the elements the caller names, and neither appears in `GameMetadata`. Verified on
  the live install, a full sync left both elements in place and reported `gamelists: all 1 unchanged`.
  This is the first time that allowlist was tested against an install that actually had them set;
  the field census it was designed from had none, because nothing had ever set a per-game emulator
  there.
- **Dropping them would change which emulator runs**, and save and state paths follow the emulator,
  so it would strand a user's saves rather than merely losing a preference.

**A row whose emulator declares no core inherits the system-level `<core>` value**, which is noise
rather than a fallback: `mesen` standalone and `jgenesis` were launched with `-core nestopia` from
`nes.core`, and both ignored it and ran correctly. Read `-emulator` from the launcher log and treat
`-core` as meaningless for an emulator that declares none.

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
filenames and splits it.

**`dolphin_sync_saves` does not do what its description says, and four documents repeated the
description.** It is GameCube only, it runs once per launch inside `emulatorlauncher` before
Dolphin starts, and it reconciles `saves/gamecube/dolphin-emu/User/GC/<REGION>/` against a
**`Card A/` subdirectory of that same folder**, newest wins, loser renamed `.old`, every failure
swallowed. A `.gci` in `Card A` with nothing beside it is copied **back out**, so a save removed
from the region root reappears one session stale. `DolphinSaveSync` detects and reports it and
never acts on it. RB-189.

**GameCube's save class is set by `dolphin_slotA`, which the menu calls SAVE FORMAT.** `8` is
the GCI folder RomMBat treats as class C; `1` is one shared raw `SRAM.<REGION>.raw`, class D.
Slot B is never rewritten by RetroBat, so it stays at Dolphin's stock relative default in
top-level `saves/dolphin/`, beside the system folders. `save_rules.json` declares the slot A and
slot B cards for all three regions as shared containers, so `saves` names each one it finds. RB-193.

**Never write this file while EmulationStation is running. The write is discarded.** ES loads
`es_settings.cfg` at startup and serialises that model on every write, so a key present at load
survives (ones ES cannot understand included) and **a key that appears afterwards does not**.
Driven with ES up: two custom keys merged in atomically and confirmed on disk were gone after
ES's next write. `Language` proves it is not a merge, because ES added that key itself at
startup and dropped it again on the same write. M0's nonsense key survived only because it was
written **before** ES started.

Merging and atomicity do not save you here; both were done and the write still vanished. **ES
writes twice a session**, at launch as well as on exit, timed against ES's own hook events: the
launch write landed 7.7 s before the `start` hook and the other 2.4 s before `quit`. So the safe
window is strictly "while ES is not running". Detect ES, refuse with a reason, and **re-read after writing** to
confirm the key is there rather than trusting the rename.

**ES prunes any setting equal to its own default** on that rewrite, so an entry written at
the stock value disappears. Never read a missing entry as the user having reverted something.

`GET http://127.0.0.1:1234/quit` closes ES cleanly **only when no game is running**. With an
emulator up, `/quit` and `/emukill` both return 200 and do nothing. Poll for the process to
exit rather than trusting the response. Changing a user's emulator config is opt-in and
reversible.

## The window in which `es_settings.cfg` can be written

**Only while EmulationStation is not running, and "not running" means the process is gone.**
Timed across three sessions from `GET /quit`:

| Event                               | When                     |
| ----------------------------------- | ------------------------ |
| ES writes `es_settings.cfg` on exit | 175.6 / 324.8 / 324.1 ms |
| the `quit` hook fires               | 807.3 / 524.8 / 551.6 ms |
| the process is gone                 | 875.5 / 573.1 / 604.0 ms |

So the exit write comes **first**, with 200 to 630 ms to spare, and nothing writes the file
again afterwards. The hook still fires while ES is alive, for another 48 to 68 ms, and that
window is inside the load-and-serialise window a key is discarded in. **Poll for the process,
not for the hook.** It is cheap: 10 ms, one poll, on a real session.

**`start` is inside the discard window, not outside it.** ES's launch write lands 1.6 to 4.9 s
**before** the `start` hook fires, so by the time a start-hook pass runs, ES has already loaded
its model and already written the file once. Never write config there.

**So a UI launched from the ES menu can never write this file at all**, because it runs under a
live ES by construction. It queues the change instead (`pending_config`, migration `012`) and
`background quit` applies it once the process is confirmed gone.
