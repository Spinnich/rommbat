# Test fixtures

Byte-exact captures from a real RetroBat install, plus recorded RomM API responses.

**Do not hand-edit or reformat anything here.** Trunk is configured to skip this
directory, because a fixture that has been tidied no longer proves what it was captured
to prove.

**Some fixtures are links, not copies.** Both test projects link files in from `reference/`
and `data/` under `fixtures/` names, so a test runs against the same bytes the reference data
and the generators do. Copying them would let the copy drift from the file the numbers were
derived from. **Only ever link a tracked file, in any test project:** `probe-output/` is
gitignored scratch, and linking from it builds on the machine that produced it and nowhere
else.

| Linked in                    | From                             | Used by                                          |
| ---------------------------- | -------------------------------- | ------------------------------------------------ |
| `es_systems.template.cfg`    | `reference/es_systems.cfg`       | Folder, `<extension>` and non-ROM-system parsing |
| `systems_names.lst`          | `reference/systems_names.lst`    | Asserting every bundled folder is a real system  |
| `platforms.json`             | `data/retrobat/platforms.json`   | The mapping regression                           |
| `bios.json`                  | `data/retrobat/bios.json`        | The BIOS manifest and planner                    |
| `batocera-systems.json`      | `reference/batocera-systems.json`| The manifest against the file it is derived from |
| `es_savestates.template.cfg` | `reference/es_savestates.cfg`    | Save-state directory and filename templates      |

`RomMBat.Agent.Tests.csproj` links `reference/es_systems.cfg` as
`fixtures/es_systems.template.cfg` as well, for the same reason: a command gate that reads
the install's `es_systems.cfg` needs a real one to read.

`es_systems.live.json` is a real capture, checked in here: 244 systems from a live 8.2.0
install, recorded by M0 probe 4. It carries system names, paths and extensions only, no
library contents. **It is deliberately not re-captured when the supported version moves.**
What it is for is the shape of a live file against the shipped template, and 8.2.1's
`<extension>` additions (`.decomp`, `.zar`) change data the shipped code reads live and never
compares to this. Re-capture it when a parser trap moves, not when a version does.

`emulatorLauncher.log` is twelve lines cut verbatim out of a real install's five months and
424 launches, with only the Windows user profile path replaced. It is assembled by trap
rather than chronologically, so it is not a slice of the file, and each line is one of the
things the M6 probe found (findings 112 to 118):

| Line     | What it is                                                                              |
| -------- | --------------------------------------------------------------------------------------- |
| 1        | A UTF-8 BOM and a separator, which is how each rotation half opens                      |
| 2        | `[Startup]` with no `-rom`: an `-updatestores` run, not a launch                        |
| 3        | A launch rooted at `D:\RetroBat`, the drive letter this install used to have            |
| 4        | A launch rooted at `E:\RetroBat`, the letter it has now                                 |
| 5        | `-rom` **unquoted**, carrying spaces, commas and parentheses                            |
| 6        | `-core` written **after** `-rom`                                                        |
| 7        | An ES-menu launch: `-system retrobat`, a rom under `system\es_menu\`                    |
| 8        | `-core` present but **empty**, plus a multi-disc rom inside its own folder              |
| 9, 12    | A recorded exit and a failed launch, neither of which is a launch line                  |
| 10, 11   | An unstamped .NET stack-trace continuation                                              |

`es_settings.cfg` is a live capture, checked in here, and it is the one fixture that had to
be **altered** before it could be: **a real `es_settings.cfg` holds plaintext credentials.**
The captured install carried the ScreenScraper password, the RetroAchievements password and
token, and the IGDB client id and secret, all in clear, plus the user's name under three keys.
`tools/m6-probes/m6-redact-es-settings.py` replaces those eight values with obvious
placeholders and asserts that none of them survives anywhere else in the file. **Every other
byte is the capture**, which is what the round-trip test needs: 260 settings across the three
element groups, tab indentation, LF endings, a bare `<?xml version="1.0"?>` with no encoding,
an `&amp;` in `ScreenSaverGameInfo`, and ES's own alphabetical-within-group ordering. Re-run
the script rather than editing the file.

That credentials live in this file is worth carrying beyond the fixture: nothing RomMBat logs,
reports or writes to a probe transcript may echo a value read out of it.

`es_systems.template.cfg` is the **shipped template**, not a live capture, and it tracks the
supported RetroBat version because it is linked from `reference/` rather than copied. It is
enough because it carries every parser trap a live file does: five systems whose `<name>`
differs from their folder, one `<name>` used twice, four entries pointing outside `roms/`,
one with no path, and two systems inside XML comments. M0's live capture agrees with it on
all 240 folders. Shipped code still reads the live file; this is a fixture, not a substitute.

Expected contents, arriving with the milestones that need them:

| Fixture             | From                                                        | Used by                                     |
| ------------------- | ----------------------------------------------------------- | ------------------------------------------- |
| `es_systems.cfg`    | A live RetroBat tree                                        | Folder and `<extension>` resolution         |
| `es_savestates.cfg` | A live RetroBat tree                                        | Save-state directory and filename templates |
| `gamelist.xml`      | A system with user edits (favourite, playcount, lastplayed) | Asserting user fields survive a sync        |
| `openapi.json`      | A pinned RomM version                                       | DTO generation drift                        |

Redact before checking anything in: no server hostname, no token, no personal library
contents beyond what the test needs.
