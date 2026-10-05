---
summary: The certification record for `sega32x`: ares, BizHawk and jgenesis, and Kega Fusion.
read-when: When a result for one of these `sega32x` rows is needed, or before re-driving one.
---

# sega32x: ares, BizHawk and jgenesis, and Kega Fusion

|               | Selected by                                          | Confirmed on the `emulatorLauncher.log` line         |
| ------------- | ---------------------------------------------------- | ---------------------------------------------------- |
| `ares`        | the game's emulator option in ES, or its pin         | `ares.exe --system "Mega 32X" <rom>`                 |
| `PicoDrive`   | the game's emulator option in ES                     | `EmuHawk.exe <rom>`, BizHawk's only 32X core         |
| `jgenesis`    | the game's `<emulator>` pin, set with ES closed      | `jgenesis-cli.exe -f <rom> --hardware Sega32X`       |
| `kega-fusion` | the agent's launch, `-emulator kega-fusion -core sega32x` | `Fusion.exe -32x <rom>`                         |

## Checklist for ares, BizHawk and jgenesis

| #   | Result on every one of the three                                                                    |
| --- | --------------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions** on Chaotix's SRAM, every file back at its own md5 after a restore          |
| 5   | **Pass**: two slots each, one deleted and restored at its own md5                                   |
| 6   | **N/A**                                                                                             |
| 7   | **Pass**: Chaotix launched from ES on the synced ROM under each, art and description present        |
| 8   | **Pass**: every session read back by `status` under `recent:`                                       |
| 9   | **Pass**, the system's re-sync ([libretro.md](libretro.md#checklist))                               |

## 4. Battery saves on the three

| Row         | File under `saves/sega32x/`                         | Size    | Slot               | After the delete |
| ----------- | --------------------------------------------------- | ------- | ------------------ | ---------------- |
| `ares`      | `ares/Mega 32X/<rom>.ram`                           | 512 B   | `ares:battery`     | `d5e93679...`    |
| `PicoDrive` | `bizhawk/Knuckles' Chaotix (32X) (JU) [!].SaveRAM` | 1,024 B | `bizhawk:battery`  | `397121bd...`    |
| `jgenesis`  | `jgenesis/32x/<rom>.sav`                            | 512 B   | `jgenesis:battery` | `d5e93679...`    |

**Each read the `libretro` save**: slot 1 showed the hub progress on every row. **Each wrote the
game's change back**: deleting slot 1 changed 20 bytes against the seed, the same 20 on all three,
so ares's and jgenesis's files are equal and BizHawk's is the same bytes in the odd positions.
**Two sessions before that wrote nothing new**, a new game in slot 2 under ares and BizHawk and a walk
past the hub's exit sign under BizHawk, because Chaotix had not saved
([index.md](index.md#the-install-this-was-measured-on)). **ares writes on `Esc` only**, rewriting
the same bytes when nothing changed. **BizHawk names the save after its own title**, joined to the ROM
through the name sidecar beside its states, `Knuckles' Chaotix (32X) (JU) [!].PicoDrive`, and keeps
the save it replaced as `.SaveRAM.bak`, which the rule ignores.

At the end the three files were deleted and `saves restore 209635 --apply` brought each back under its
own name at its own md5, `restored 3 save(s) and 0 state(s), failed 0`, each the newest of two server
saves for its file, the older being the seed's upload.

**NBA Jam keeps no save under ares or BizHawk** (RB-415). Booted with no file, ares wrote 512 B of
`0xFF`, passed over as erased, and BizHawk 8,192 B of `0x00`, which uploaded as `bizhawk:battery` by
the maintainer's ruling (RB-414). ares, seeded with the 256 B format and run from ES, rewrote it
unchanged. jgenesis formats it, 256 B in `jgenesis/32x/<rom>.sav`, as PicoDrive does.

## 5. States on the three

| Row         | Directory under `saves/sega32x/` | Slots                         | Round-tripped                 |
| ----------- | -------------------------------- | ----------------------------- | ----------------------------- |
| `ares`      | `ares/Mega Drive/`               | `.bs1` and `.bs2`, 1,010,843 B | `.bs2`, `1d0957e8...`        |
| `PicoDrive` | `bizhawk/sstates/PicoDrive/`     | `QuickSave2` and `QuickSave4` | `QuickSave4`, `2478404a...`   |
| `jgenesis`  | `jgenesis/states/`               | `_0.jst` and `_1.jst`         | `_1.jst`, `a7e3d05a...`       |

**The agent made every state from its own session**: `F2`, `F7`, `F2` under ares and jgenesis,
`Ctrl+F2` and `Ctrl+F4` under BizHawk, through `emulatorLauncher` with the row's arguments. **ares
writes its 32X states under the parent's name, `ares/Mega Drive/`**, beside nothing, its battery save
being in `ares/Mega 32X/` (RB-413), and shows nothing when one is saved. **jgenesis writes to
`emulators/jgenesis/states/32x/` and `emulatorLauncher` mirrors that into the declared
`jgenesis/states/` on exit**, as BizHawk's mirror does from `emulators/bizhawk/sstates/sega32x/`.
**None of the three writes a screenshot file**: BizHawk's frame is `Framebuffer.bmp` inside the state,
and its two differ (`d44f0fcb...`, `bdd6c3b5...`). Each restore brought the state back md5-identical,
`restored 0 save(s) and 3 state(s), failed 0`.

## 8. Sessions

| Row         | Game    | Journal, UTC         | Length | What was done                      |
| ----------- | ------- | -------------------- | ------ | ---------------------------------- |
| `ares`      | Chaotix | 17:04:00 to 17:06:35 | 2m 34s | slot 1 read                        |
| `ares`      | NBA Jam | 17:10:26 to 17:12:15 | 1m 49s | initials, nothing written          |
| `ares`      | Chaotix | 17:15:30 to 17:16:15 | 45s    | new game in slot 2, nothing saved  |
| `PicoDrive` | Chaotix | 17:19:47 to 17:21:03 | 1m 16s | new game in slot 2, nothing saved  |
| `PicoDrive` | Chaotix | 17:23:03 to 17:23:50 | 47s    | the exit sign, nothing saved       |
| `PicoDrive` | Chaotix | 17:26:14 to 17:26:44 | 30s    | slot 1 deleted                     |
| `ares`      | Chaotix | 17:27:38 to 17:28:05 | 27s    | slot 1 deleted                     |
| `jgenesis`  | Chaotix | 17:32:54 to 17:33:28 | 34s    | slot 1 deleted                     |

Every one is on the server against its rom. The agent's own launches run no hooks and record none.

## `kega-fusion`/`sega32x`: driven, and not certified at this floor

**Step 4 cannot pass on 8.2.1**, for `megadrive`'s reason (RB-283): RetroBat's `Fusion.ini` sends the
battery save to `emulators/kega-fusion/`, and RomMBat reads only `mastersystem`'s `.ssm` there.
NBA Jam's boot format landed as `emulators/kega-fusion/<rom>.srm`, 256 B, the same bytes jgenesis
writes. **Its states land in the parent's tree**, `saves/megadrive/kega-fusion/<rom>.gs0` and `.gs9`,
676,671 B each, from `F5`, `F7`, `F5`, where `saves` reports them as `megadrive` states with `no rom`
(RB-416). By the maintainer's ruling they are recorded, not attributed. Kega booted all three games
with no BIOS, and was not played: its pad needs a remap in its own menu first (RB-284), and the row
cannot certify whatever a session showed.

| #   | Result                                                                              |
| --- | ----------------------------------------------------------------------------------- |
| 1-3 | **Pass**, the system's                                                              |
| 4   | **Fail on 8.2.1**: the save lands in `emulators/kega-fusion/`, RB-283               |
| 5   | **Fail on 8.2.1**: the states land under `saves/megadrive/`, RB-416                 |
| 6   | **N/A**                                                                             |
| 7-9 | **Not driven**: no ES session, for the reasons above                                |
