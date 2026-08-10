# Test fixtures

Byte-exact captures from a real RetroBat install, plus recorded RomM API responses.

**Do not hand-edit or reformat anything here.** Trunk is configured to skip this
directory, because a fixture that has been tidied no longer proves what it was captured
to prove.

**Some fixtures are links, not copies.** `RomMBat.Tests.csproj` links three files in from
`reference/` and `data/` under `fixtures/` names, so the mapping regression runs against the
same bytes the reference data and the generator do. Copying them would let the copy drift
from the file the numbers were derived from. **Only ever link a tracked file:**
`probe-output/` is gitignored scratch, and linking from it builds on the machine that
produced it and nowhere else.

| Linked in                 | From                       | Used by                                          |
| ------------------------- | -------------------------- | ------------------------------------------------ |
| `es_systems.template.cfg` | `reference/es_systems.cfg` | Folder, `<extension>` and non-ROM-system parsing |
| `systems_names.lst`       | `reference/`               | Asserting every bundled folder is a real system  |
| `platforms.json`          | `data/retrobat/`           | The mapping regression                           |

`es_systems.live.json` is a real capture, checked in here: 244 systems from a live 8.2.0
install, recorded by M0 probe 4. It carries system names, paths and extensions only, no
library contents.

`es_systems.template.cfg` is the **shipped 8.2.0 template**, not a live capture, and it is
enough because it carries every parser trap a live file does: five systems whose `<name>`
differs from their folder, one `<name>` used twice, four entries pointing outside `roms/`,
one with no path, and two systems inside XML comments. M0's live capture agrees with it on
all 240 folders. Shipped code still reads the live file; this is a fixture, not a substitute.

Expected contents, arriving with the milestones that need them:

| Fixture             | From                                                        | Used by                                     |
| ------------------- | ----------------------------------------------------------- | ------------------------------------------- |
| `es_systems.cfg`    | A live RetroBat tree                                        | Folder and `<extension>` resolution         |
| `es_savestates.cfg` | A live RetroBat tree                                        | Save-state directory and filename templates |
| `es_settings.cfg`   | A live RetroBat tree, after ES has written it               | Merge-not-clobber round-trip                |
| `gamelist.xml`      | A system with user edits (favourite, playcount, lastplayed) | Asserting user fields survive a sync        |
| `openapi.json`      | A pinned RomM version                                       | DTO generation drift                        |

Redact before checking anything in: no server hostname, no token, no personal library
contents beyond what the test needs.
