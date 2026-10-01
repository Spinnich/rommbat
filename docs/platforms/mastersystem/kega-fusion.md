---
summary: The certification record for `mastersystem`: the two `kega-fusion` rows.
read-when: When a result for one of these `mastersystem` rows is needed, or before re-driving one.
---

# mastersystem: The two `kega-fusion` rows

**Certified at RomM `5.3.1` and RetroBat 8.2.1 on 2026-10-01**, when steps 4 and 9 were re-driven
on a build reading Kega's folder (#381). The other seven steps carry from the drive on
2026-09-24, step 6 being N/A.

|              | `kega-fusion`/`auto`                                  | `kega-fusion`/`mastersystem`             |
| ------------ | ----------------------------------------------------- | ---------------------------------------- |
| Selected by  | `mastersystem.emulator = kega-fusion`, `.core = auto` | the agent's launch, `-core mastersystem` |
| Confirmed by | `Fusion.exe -auto`                                    | `Fusion.exe -sms`                        |

| #   | Both rows                                                                                               |
| --- | ------------------------------------------------------------------------------------------------------- |
| 1-3 | **Pass**, carried                                                                                       |
| 4   | **Pass**, class A, `emulators/kega-fusion/<rom>.ssm` up, deleted, restored. Below                       |
| 5   | **Pass**, two slots each, round-tripped at their own md5                                                |
| 6   | **N/A**                                                                                                 |
| 7   | **Pass** on launch and art, for `auto` from ES                                                          |
| 8   | **Pass** for `auto`, 17:39:17Z to 17:40:26Z, 1m 8s. Carried to `mastersystem`, which the agent launched |
| 9   | **Pass**, re-run on 2026-10-01                                                                          |

**Kega's Master System save is `emulators/kega-fusion/<rom>.ssm`**, 8,191 B, beside the `megadrive`
`.srm` from that pass: `Fusion.ini` sends both there, the `.ssm` under `SxMFiles`. Seeded with the
`libretro` `.srm`, Kega rewrote it on exit on both rows. Its states go where `SMSStateFiles` sends
them, `saves/mastersystem/kega-fusion/<rom>.ss<slot>`, `.ss` where `megadrive` is `.gs`, 32,958 B,
`F5` saving and `F7` stepping the slot down from 0 to 9:

| Row            | `.ss0`        | `.ss9`        | Round-tripped |
| -------------- | ------------- | ------------- | ------------- |
| `auto`, in ES  | `4fb3becd...` | `b4bed337...` | `.ss9`        |
| `mastersystem` | `2960cd52...` | `585dcac1...` | `.ss0`        |

The supplement's new `kega-fusion` entry, scoped to `mastersystem`, is what lets them sync. No image
is written. `Fusion.ini` held `Joystick1Using=255` for player 1 before and after the `auto` session.

## 4. The battery save, on 2026-10-01

**Class A, one file per game, slot `kega-fusion:battery`**, read where `Fusion.ini`'s `SxMFiles`
leaves it by a `from_root` rule that claims `.ssm` and nothing else in the folder (#381). The build
from #382 was deployed to `R:` and its first flush sent the `.ssm` already there, `b98e4e38...`,
for rom 239603, and recorded nothing else in the folder: the `megadrive` `.srm`, `Fusion.exe` and
`Fusion.ini` beside it stayed out.

| Row            | What was driven                                                                                                                                              |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `auto`, in ES  | The maintainer started a new game and saved at a church. Kega wrote `b24f88b9...`, 8,191 B, on exit at 13:24:30Z, and the `quit` pass sent it at 13:24:48Z   |
| `mastersystem` | The agent deleted the `.ssm` and ran `saves restore 239603 --apply`. It came back at `b24f88b9...`, and under `-core mastersystem` the game offered Continue |

**A re-sync after both moved nothing**, saves or states. The server keeps both uploads, and the
restore names the newer and passes over the older, save 640.
