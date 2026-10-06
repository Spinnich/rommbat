---
summary: The discipline for writing files RetroBat or EmulationStation also own, file by file.
read-when: Before writing any file inside the RetroBat tree.
---

# Writing into someone else's tree

RomMBat writes into a directory RetroBat also owns, and two of those files are rewritten
by EmulationStation on exit. Every writer therefore follows the same discipline: **read,
merge only the fields RomMBat owns, write atomically via temp file plus rename, and never
clobber.**

| File                          | Who else writes it                                      | Rule                                                                                                                      |
| ----------------------------- | ------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `roms/<system>/gamelist.xml`  | ES writes back favorite, playcount, lastplayed, hidden  | Merge. Only locally present ROMs. Keyed by **resolved folder**, not by platform                                           |
| `es_settings.cfg`             | ES discards anything written while it runs              | Refuse while ES is up, or queue with `--at-quit` and apply from `background quit`. Merge. Opt-in and reversible           |
| `system/es_menu/gamelist.xml` | RetroBat ships it; ES reads it and never writes it back | Merge one `<game>`. Keep the BOM, the CRLF and the commented-out entries: RomMBat is the only writer that could damage it |
| `scripts/<event>/*.bat`       | RetroBat ships its own                                  | Append idempotently, never replace. Uninstall cleanly                                                                     |
| Emulator INIs                 | `emulatorlauncher` regenerates them every launch        | **Never write these.** Write the RetroBat option instead                                                                  |

Gamelists key by **resolved folder** because the platform mapping is many-to-many: `snes`
and `sfam` can both resolve to `snes`, and several arcade platforms into `mame`. One
gamelist per platform would have the second write clobber the first.

The `es_settings.cfg` precedence chain, from `Program.cs:384-388`:

```text
es_settings.cfg  ->  global.<key>  ->  <system>.<key>  ->  <system>["<rom filename>"].<key>
```

That last form is a genuine per-game override, and it is the lever that turns shared
memory cards into per-game ones without touching an emulator config.

## Where RomMBat's files live

Everything RomMBat owns lives inside the RetroBat tree. Nothing goes to `%APPDATA%`, the
registry, a service or a scheduled task. The subdirectory is not a free choice: a `.menu` entry
resolves its executable under `emulators\` and `emulatorLauncher` refuses `..\` escapes, so
anything launched from the ES menu lives there (RB-384).

```text
<RetroBat root>/
  emulators/rommbat/      forced by the .menu path rules, see RB-384
    rommbat-agent.exe     the seven installed files start here
    RomMBat.exe
    rommbat-hook.exe      the source hooks install copies into each event folder
    e_sqlite3.dll         needed by both the agent and the UI, one copy serves both
    libSkiaSharp.dll      the UI's three Avalonia natives; losing one breaks it at launch
    av_libglesv2.dll
    libHarfBuzzSharp.dll
    rommbat.db            SQLite: file index, sync sets, outbox, cursors
    device.id             the client_device_identifier GUID
    logs/
    outbox/
  roms/<system>/          ROMs, gamelist.xml, images/, videos/, manuals/
  bios/                   firmware, at the paths batocera-systems.json specifies
  saves/<system>/<emulator>/   emulator save output, two levels deep
  emulationstation/
    emulatorLauncher.exe  what %~dp0..\..\..\ from a hook resolves to
    .emulationstation/
      es_settings.cfg     RetroBat options, including the per-game override form
      es_savestates.cfg   per-emulator save-state schema
      es_features.cfg     the per-game option definitions (memory cards, VMUs)
      scripts/<event>/    the .bat hooks; reach the root with %~dp0..\..\..\..\
  system/es_menu/
    rommbat.menu          line 1 the exe path, relative to emulators/
    gamelist.xml          must also carry a <game> entry or the app shows as a filename
    media/
      rommbat-logo.png    the artwork that entry points at, written by menu install
  system/version.info     the version string, e.g. 8.2.1-stable-win64
```
