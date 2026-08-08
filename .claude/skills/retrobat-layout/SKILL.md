---
name: retrobat-layout
description: RetroBat's on-disk layout and integration seams - folder tree, es_systems.cfg, es_savestates.cfg, es_settings.cfg precedence, ES event hooks, and the ES menu entry. Use when reading or writing anything inside a RetroBat install.
---

# RetroBat layout

RomMBat integrates purely through seams RetroBat already has. **Do not fork RetroBat.**

## Tree

| Path                                  | Use                                                     |
| ------------------------------------- | ------------------------------------------------------- | -------- | ---------------------------------------- |
| `roms/<system>/`                      | ROMs. Folder names come from `es_systems.cfg`           |
| `roms/<system>/gamelist.xml`          | Metadata ES reads directly                              |
| `roms/<system>/images                 | videos                                                  | manuals` | Media siblings, named after the ROM file |
| `saves/`                              | Emulator save output                                    |
| `bios/`                               | BIOS and firmware, flat at the root with few exceptions |
| `emulationstation/.emulationstation/` | ES home: `es_settings.cfg`, `scripts/`, themes          |
| `system/es_menu/*.menu`               | How RetroBat registers launchable apps in the ES menu   |
| `build.ini`                           | `retrobat_version=`, used for the compatibility gate    |

Locate the root by walking up from `AppContext.BaseDirectory` to a marker
(`retrobat.ini`, `emulationstation/`, `roms/`). Registry lookups are a fallback for fixed
installs only, never the primary path.

## es_systems.cfg is the authority on systems and extensions

Read the **live** copy, not the vendored template: it reflects that machine's actual
configuration. Each `<system>` carries `<name>`, `<fullname>`, `<manufacturer>`,
`<hardware>`, `<release>`, `<path>`, `<extension>` and `<command>`.

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

## es_settings.cfg is how you configure emulators

`emulatorlauncher` regenerates each emulator's INI from ES options at every launch, so
**editing an emulator INI is pointless: it gets clobbered on the next boot.** Write the
option instead. Precedence (`emulatorlauncher/Program.cs`):

```text
es_settings.cfg -> global.<key> -> <system>.<key> -> <system>["<rom filename>"].<key>
```

That last form is a real per-game override. Useful keys:

| Key                       | Effect                                           | Stock default                  |
| ------------------------- | ------------------------------------------------ | ------------------------------ |
| `duckstation_memcardtype` | PS1 memory card mode                             | already `PerGameTitle`         |
| `dolphin_slotA`           | GameCube slot A device                           | already GCI folder (`SlotA=8`) |
| `pcsx2_slot1_memory`      | PS2 card: `game` names it after the ROM basename | shared `Mcd001.ps2`            |

**ES rewrites `es_settings.cfg` on exit**, exactly like `gamelist.xml`. Merge rather than
clobber, write while ES is idle, write atomically. Changing a user's emulator config is
opt-in and reversible.

## Event hooks

`.emulationstation/scripts/<event>/*.bat`. Events include `start`, `game-start`,
`game-end`, `game-selected`, `system-selected`, `quit`, `shutdown`, `sleep`, `wake`,
`update-gamelists`. RetroBat ships its own `.bat` hooks, so the mechanism is proven.

Hooks resolve the agent through `%~dp0..\..\..\`, never an absolute path, matching
RetroBat's own `updatestores.bat`.

**Hooks run inside the game-launch path.** They append to the local journal and exit. No
HTTP, no blocking, no lock waits. Confirm the exact arguments and blocking behaviour
against `docs/retrobat-findings.md` rather than assuming the Batocera convention.

## gamelist.xml

ES writes user edits (favourite, playcount, lastplayed, hidden) back into the same file.
Merge, never clobber; write atomically via temp file plus rename; include only locally
present ROMs. **Key generation by resolved folder, not by platform**, because two RomM
platforms can share one folder.
