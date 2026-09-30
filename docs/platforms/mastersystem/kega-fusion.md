---
summary: The certification record for `mastersystem`: the two `kega-fusion` rows.
read-when: When a result for one of these `mastersystem` rows is needed, or before re-driving one.
---

# mastersystem: The two `kega-fusion` rows

**Driven, and not certified: step 4 cannot pass on RetroBat 8.2.1.** The other eight steps pass or
carry, step 6 being N/A.

|              | `kega-fusion`/`auto`                                  | `kega-fusion`/`mastersystem`             |
| ------------ | ----------------------------------------------------- | ---------------------------------------- |
| Selected by  | `mastersystem.emulator = kega-fusion`, `.core = auto` | the agent's launch, `-core mastersystem` |
| Confirmed by | `Fusion.exe -auto`                                    | `Fusion.exe -sms`                        |

| #   | Both rows                                                                                               |
| --- | ------------------------------------------------------------------------------------------------------- |
| 1-3 | **Pass**, carried                                                                                       |
| 4   | **Fail on 8.2.1.** The save lands in `emulators/kega-fusion/<rom>.ssm`, which RomMBat does not scan     |
| 5   | **Pass**, two slots each, round-tripped at their own md5                                                |
| 6   | **N/A**                                                                                                 |
| 7   | **Pass** on launch and art, for `auto` from ES                                                          |
| 8   | **Pass** for `auto`, 17:39:17Z to 17:40:26Z, 1m 8s. Carried to `mastersystem`, which the agent launched |
| 9   | **Pass**                                                                                                |

**Kega's Master System save is `emulators/kega-fusion/<rom>.ssm`**, 8,191 B, beside the `megadrive`
`.srm` from that pass: `Fusion.ini`'s one `SRMFiles` key serves every system. Seeded with the
`libretro` `.srm`, Kega rewrote it on exit on both rows. Its states go where `SMSStateFiles` sends
them, `saves/mastersystem/kega-fusion/<rom>.ss<slot>`, `.ss` where `megadrive` is `.gs`, 32,958 B,
`F5` saving and `F7` stepping the slot down from 0 to 9:

| Row            | `.ss0`        | `.ss9`        | Round-tripped |
| -------------- | ------------- | ------------- | ------------- |
| `auto`, in ES  | `4fb3becd...` | `b4bed337...` | `.ss9`        |
| `mastersystem` | `2960cd52...` | `585dcac1...` | `.ss0`        |

The supplement's new `kega-fusion` entry, scoped to `mastersystem`, is what lets them sync. No image
is written. `Fusion.ini` held `Joystick1Using=255` for player 1 before and after the `auto` session.
