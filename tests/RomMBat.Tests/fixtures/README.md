# Test fixtures

Byte-exact captures from a real RetroBat install, plus recorded RomM API responses.

**Do not hand-edit or reformat anything here.** Trunk is configured to skip this
directory, because a fixture that has been tidied no longer proves what it was captured
to prove.

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
