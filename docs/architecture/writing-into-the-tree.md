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
| `roms/<system>/gamelist.xml`  | ES writes back favourite, playcount, lastplayed, hidden | Merge. Only locally present ROMs. Keyed by **resolved folder**, not by platform                                           |
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
