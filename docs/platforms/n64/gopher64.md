---
summary: The certification record for `n64`: `gopher64`, certified 2026-09-29.
read-when: When a result for one of these `n64` rows is needed, or before re-driving one.
---

# n64: `gopher64`, certified 2026-09-29

Steps 4, 6, 7, 8 and 9 were driven on 2026-09-29 with #239's build deployed to the same install,
selected by `n64.emulator = gopher64` (removed with ES closed afterwards) and confirmed as
`Using Gopher64Generator` on each launch. The maintainer played from ES. Steps 1 to 3 and 5 carry
from 2026-09-27: #239 changes battery saves only.

|     |                                                                                                                                                                                                                                                                                                                                                          |
| --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1-3 | As every row                                                                                                                                                                                                                                                                                                                                             |
| 4   | **Pass, both directions.** The first flush read the three files gopher64 wrote on 2026-09-27 in `emulators/gopher64/portable_data/data/saves/`, attributed each through its launch, and sent them as `gopher64:battery:sra`, `:eep` and `:mpk` under `n64`. The `.sra` and `.eep` were deleted and restored byte for byte, and the game read both, below |
| 5   | **Pass.** `portable_data/data/states/<header>-<sha256>.state0` is mirrored to the declared `gopher64/states/<rom>.state0`; restored, it came back byte for byte and was copied back on the next launch                                                                                                                                                   |
| 6   | **Pass.** The pak is on at gopher64's default, in `<header>-<sha256>.mpk`, 131,072 B, four paks. A ghost saved to it went up, came back byte for byte after the file was deleted, and was offered in Time Trials                                                                                                                                         |
| 7   | **Pass.** Both games launched from ES                                                                                                                                                                                                                                                                                                                    |
| 8   | **Pass.** `status` read back all four ES sessions: 09:09:26 (225805), 09:10:42, 09:16:04 and 09:25:01 UTC (157714)                                                                                                                                                                                                                                       |
| 9   | **Pass.** `nothing to do: 147 games already present, 0 downloaded, 0 written`, `media: 5619 already present`, `gamelists: all 9 unchanged`, and a flush sent nothing                                                                                                                                                                                     |

| File                    | Restored, then                                                                            | On the server            |
| ----------------------- | ----------------------------------------------------------------------------------------- | ------------------------ |
| `.sra`, Ocarina of Time | yesterday's file on the file select screen; saved again, `56edf403...`                    | save 574, sent by `quit` |
| `.eep`, Mario Kart 64   | yesterday's Time Trial records listed; a lap wrote `bfba8093...`                          | save 575, sent by `quit` |
| `.mpk`, Mario Kart 64   | a ghost saved, `3383a99a...`; deleted, restored, and the ghost offered on the next launch | save 576, sent by `quit` |

**gopher64 writes the `.mpk` on boot and on every pak access, often with unchanged bytes**, and
the ghost reached the file only when the game saved it. **Each upload is a new server row**, as
`overwrite` is everywhere (570 to 576 across the pass), and a restore takes the newest and names
the older ones for the same file without restoring them.
