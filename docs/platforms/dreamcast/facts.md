---
summary: How each emulator RetroBat offers for `dreamcast` behaves, measured, with RB- IDs.
read-when: Before certifying a `dreamcast` row or changing how RomMBat handles a `dreamcast` save, state or firmware.
---

# dreamcast: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-9. Characterized: four port-keyed files shared by all games, but `flycast_vmupergame` converts them, for port 1

Plan says: Dreamcast VMU handling is unverified (L870)

Measurement says: Characterized: four port-keyed files shared by all games, **but `flycast_vmupergame` converts them**, for port 1 only

## RB-49. Confirmed live, but the per-game VMU is named for the disc serial (`T40217N_vmu_save_A1.bin`), not the rom

Plan says: Dreamcast converts to per-game via `flycast_vmupergame` (RB-9 above)

Measurement says: Confirmed live, but the per-game VMU is named for the **disc serial** (`T40217N_vmu_save_A1.bin`), not the rom file, so it does not collapse into class A the way DuckStation's `PerGameFileTitle` does. The comparison is now moot: PS1 stays on its stock database-keyed mode

## RB-362. Flycast VMU: the plan's one unverified class-D case, answered

[`docs/PLAN.md`](https://github.com/Spinnich/rommbat/blob/4fa916583/docs/PLAN.md) line 870 records Dreamcast VMU handling as unverified. The
tree answers it:

```text
saves/dreamcast/flycast/vmu/vmu_save_A1.bin
saves/dreamcast/flycast/vmu/vmu_save_B1.bin
saves/dreamcast/flycast/vmu/vmu_save_C1.bin
saves/dreamcast/flycast/vmu/vmu_save_D1.bin
```

Four files, one per **controller port** (A through D), slot 1 on each. They are keyed by
port, shared by every Dreamcast game, and nothing in the path identifies a game. That is
class D in its purest form.

**But it converts.** `es_features.cfg` declares the option:

```xml
<feature submenu="EMULATION" name="PER GAME VMU" group="ADVANCED SETTINGS"
         value="flycast_vmupergame" preset="switchauto" order="104"
         description="When enabled, each game will have its own VMU in port 1."/>
```

So Dreamcast joins the convertible set rather than the unsyncable one. One caveat is
written into the description itself: **only port 1 becomes per-game.** Ports B, C and D
remain shared and unattributable, so a game that writes to a second VMU still produces
something RomMBat cannot map to a `rom_id`.

## RB-363. And driven: the conversion works, but the per-game VMU is keyed by disc serial

Two launches of Bangai-O (USA), differing only in the override
(`tools/m0-probes/probe2-vmu-pergame.ps1`). The shared VMU files were copied aside first and
restored afterwards, so the install's real Dreamcast saves were not at risk.

| Run                    | `emu.cfg`              | What appeared in `saves/dreamcast/flycast/vmu/`            |
| ---------------------- | ---------------------- | ---------------------------------------------------------- |
| control, no override   | `PerGameVmu = no`      | nothing new; **shared `vmu_save_A1.bin` changed at exit**  |
| `flycast_vmupergame=1` | **`PerGameVmu = yes`** | **new `T40217N_vmu_save_A1.bin`, 131,072 B, written live** |

Four results, and the second one is the awkward one:

1. **The per-game `es_settings.cfg` key reaches a standalone emulator's generated config.**
   The override was proven on a libretro key; this shows the same mechanism driving
   `PerGameVmu` in Flycast's own `emu.cfg`, so it generalizes past RetroArch.
2. **The per-game VMU is named after the disc's product number, not the rom file.**
   `T40217N` is Bangai-O's Dreamcast serial (`T-40217N` with the hyphen dropped); the rom is
   `Bangai-O (USA).chd`, and its name appears nowhere in the path. **So this is not the clean
   collapse into class A that DuckStation's `PerGameFileTitle` gives**, where the card is
   named after the rom file. RomMBat cannot build this path from `fs_name`; attributing a
   Dreamcast VMU means either reading the serial out of the disc image or attributing by
   launch window from `emulatorLauncher.log`. PS1 stays on its stock database-keyed
   mode, so serial attribution is the common case for disc systems, not Dreamcast's alone.
3. **With the option on, the shared file is left alone entirely.** `vmu_save_A1.bin` was not
   touched during the per-game run, so the two shapes do not both receive writes. They do
   share a directory, though: `Dreamcast.VMUPath` is unchanged, so per-game and shared files
   sit side by side and a client listing that directory sees both.
4. **Launching writes the shared VMU with no in-game save**, exactly as PCSX2 does to its
   memory cards. That is the second independent confirmation that **mtime cannot decide
   whether a class-D container needs uploading**.

Ports B, C and D produced no files in either run, so the port-1-only caveat is untested in
the direction that matters: it is not that they stayed shared under the override, it is that
nothing wrote to them at all.
