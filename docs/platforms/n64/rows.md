---
summary: The certification record for `n64`: how each row is selected, and steps 4 to 9 on the eight driven on 2026-09-27.
read-when: When a result for one of these `n64` rows is needed, or before re-driving one.
---

# n64: Every row

|                    | Selected by                                       | Confirmed on the ES launch line             |
| ------------------ | ------------------------------------------------- | ------------------------------------------- |
| `mupen64plus_next` | **Nothing: RetroBat's default**                   | `-emulator libretro -core mupen64plus_next` |
| `parallel_n64`     | `n64.emulator = libretro`, `.core = parallel_n64` | `-core parallel_n64 -state_slot 3`          |
| `mupen64`          | `n64.emulator = mupen64`                          | `-emulator mupen64 -core`, empty core       |
| `simple64`         | `n64.emulator = simple64`                         | `-emulator simple64 -core`, empty core      |
| `project64`        | `n64.emulator = project64`                        | `-emulator project64 -core`, empty core     |
| `ares`             | `n64.emulator = ares`, `.core = Nintendo64`       | `-emulator ares -core Nintendo64`           |
| `Ares64`           | `n64.emulator = bizhawk`, `.core = Ares64`        | `-emulator bizhawk -core Ares64`            |
| `Mupen64Plus`      | `n64.emulator = bizhawk`, `.core = Mupen64Plus`   | `-emulator bizhawk -core Mupen64Plus`       |
| `gopher64`         | `n64.emulator = gopher64`                         | `-emulator gopher64 -core`, empty core      |

The maintainer set each from ES's own menu. The copy of `es_settings.cfg` from after the pass is
`R:\rommbat-evidence\n64\es_settings.after-pass.cfg`, and every `n64` key the pass added was
removed with ES closed when it ended.

| #   | Result on every certified row                                                                                                                                                                                                     |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions**, every file below round-tripped at its own md5 through `saves restore`                                                                                                                                  |
| 5   | **Pass**, where each emulator was found to write, one state round-tripped at its own md5                                                                                                                                          |
| 6   | **Pass**, the Controller Pak option at its default and set to a memory pak, below                                                                                                                                                 |
| 7   | **Pass.** Launched from ES after the sync; image, thumbnail, marquee, manual and description present for both games                                                                                                               |
| 8   | **Pass**, all 23 ES sessions read back from RomM by `status --all-sessions`, below                                                                                                                                                |
| 9   | **Pass.** `nothing to do: 147 games already present, 0 downloaded, 0 written`, 580 media present, `gamelists: all 1 unchanged`, `gamelist.xml` byte-identical to a copy taken after the last ES session, and a flush sent nothing |

## 4. Battery saves

| Row                | Ocarina of Time                         | Slot                    | After the row |
| ------------------ | --------------------------------------- | ----------------------- | ------------- |
| `mupen64plus_next` | loose `<rom>.srm`                       | `libretro:battery`      | `d1b51b49...` |
| `parallel_n64`     | the same file                           | `libretro:battery`      | `a646dc29...` |
| `mupen64`          | `sram/<GoodName, 32>-<md5, 8>.sra`      | `mupen64:battery:sra`   | `695efdea...` |
| `simple64`         | the same file                           | `mupen64:battery:sra`   | `4373ca28...` |
| `project64`        | `project64/<header>-<md5>/<header>.sra` | `project64:battery:sra` | `89f32ff3...` |
| `ares`             | `ares/Nintendo 64/<rom>.ram`            | `ares:battery:ram`      | `6fde87dd...` |
| `Ares64`           | `bizhawk/<rom>.SaveRAM`                 | `bizhawk:battery`       | `f833b560...` |
| `Mupen64Plus`      | the same file, in its own format        | `bizhawk:battery`       | `d546edc0...` |

**RMG and simple64 write one file under one rule**, `mupen64`'s, and each save is bound to its ROM
by the launch that wrote it, since the name is mupen64plus's title and not the file; simple64's
launches count because the rule names it as a second writer. **Project64's directory is the title**:
the header's name and the md5 of the ROM in its own 32-bit little-endian word order, so the
binding is learned the same way and a restore places the file inside it. Every restore above came
back byte for byte, Project64's into its directory, RMG's under the learned title.

**BizHawk's two cores share `bizhawk:battery` and cannot read each other's file** (RB-338):
`Mupen64Plus` replaced `Ares64`'s 33,280 B Mario Kart 64 file with its own 296,960 B image on its
first launch, and `Ares64`'s file doubled to 65,536 B once a pak was set, appending the pak to the
SRAM. The `f833b560...` above is that 64 KB file, rewritten by the agent's state launch with the
SRAM half byte-identical to the maintainer's `035b846d...`.

## 5. States

| Row                | Directory under `saves/n64/`                 | Slots and who made them                       | Round-tripped                                         |
| ------------------ | -------------------------------------------- | --------------------------------------------- | ----------------------------------------------------- |
| `mupen64plus_next` | `libretro.mupen64plus_next/`                 | `state1`, `state2`, both in ES                | `state2` and its `.png`, `7426c7d9...`, `e024670a...` |
| `parallel_n64`     | `libretro.parallel_n64/`                     | `state1`, `state2`, both in ES                | `state1` and its `.png`, `3ae6a3a2...`, `550e926d...` |
| `mupen64`          | `mupen64/`, mirrored from RMG's `Save/State` | `.st4` in ES; `.st1` by the agent             | `.st4`, `640b81e4...`                                 |
| `simple64`         | `state/`, named with the `sram/` title       | `.st0`, `.st1`, both in ES                    | `.st1`, `1224b50f...`                                 |
| `project64`        | `project64/sstates/<header>-<md5>/`          | `<its own name>.pj.zip` in ES                 | `.pj.zip`, `f747acf4...`                              |
| `ares`             | `ares/Nintendo 64/`                          | `.bs1` in ES; `.bs2` by the agent             | `.bs1`, `c73cfb7e...`                                 |
| `Ares64`           | `bizhawk/sstates/Ares64/`                    | `QuickSave2`, `QuickSave4`, both by the agent | `QuickSave4`, `fa595399...`                           |
| `Mupen64Plus`      | `bizhawk/sstates/Mupen64Plus/`               | `QuickSave2`, `QuickSave4`, both by the agent | `QuickSave2`, `67c1c6f9...`                           |
| `gopher64`         | `gopher64/states/`, mirrored                 | `.state0` in ES                               | `.state0`, `bb08b270...`                              |

**Each `libretro` screenshot differs from the other slot's**, so the returned image is the slot's
own. **RMG, simple64, Project64, ares, BizHawk and gopher64 write no `.png`.** A restored RMG and a
restored gopher64 state were copied back into the emulator's own directory by `emulatorLauncher` on
the next launch, byte-identical, with the emulator's own copy moved away first.

**simple64 names a state with the title its battery save carries**
(`Legend of Zelda, The - Ocarina o-5BD1FE10.st0`), and **Project64 keeps one directory per game**,
named as its battery directory is, with a file named from its own database
(`The Legend of Zelda - Ocarina of Time (U) (V1.0).pj.zip`). The supplement declares both, the
first with `titled_by="mupen64"` and the second with `titled_by="project64"` and
`per_game_directory="true"`; the state joins its ROM through the battery binding, and a restore
names it with the learned title or keeps the name it was sent under (RB-340). **Project64
reached slot 0 only**: RetroBat's shortcut file binds F2 and F4 and no slot key, and ES's
`-state_slot` is not passed on, so the agent's F2 wrote over slot 0; the maintainer's state was put
back from the copy taken first. **gopher64 kept one slot** and ignored `-state_slot 6`.

## 6. The Controller Pak

| Row                | At RetroBat's default | With a memory pak                                                                           | Round-tripped             |
| ------------------ | --------------------- | ------------------------------------------------------------------------------------------- | ------------------------- |
| `mupen64plus_next` | memory                | pak 1 inside the `.srm`, Mario Kart 64's ghost `NKTJ`                                       | `.srm`, `08a87a11...`     |
| `parallel_n64`     | **none**              | `parallel_pak1 = memory`: the same `.srm`, and the ghost `mupen64plus_next` saved was there | `.srm`, `f22b0091...`     |
| `mupen64`          | **none**              | `mupen64_pak1 = 0`: `sram/<title>.mpk`, 131,072 B, four paks                                | `.mpk`, `36883898...`     |
| `simple64`         | memory                | the same `.mpk`, RMG's ghost present                                                        | `.mpk`, `2084fe26...`     |
| `project64`        | memory                | `<header>_Cont_1.mpk`, 32,768 B, empty to start                                             | `.mpk`, `fea7a155...`     |
| `ares`             | always present        | `<rom>.pak`, 32,768 B, empty to start; ares offers no option                                | `.pak`, `8595e4a7...`     |
| `Ares64`           | **none**              | `bizhawk_n64_pak1 = 2`: appended to the `.SaveRAM`                                          | `.SaveRAM`, `4c0e4364...` |
| `Mupen64Plus`      | **none**              | the same setting, inside its own image                                                      | `.SaveRAM`, `d8e34da0...` |

**A pak goes where the battery save goes, and a row reads another's only where they share the file**:
the two `libretro` cores share the `.srm`, and RMG and simple64 the `.mpk`, so the ghost carried
between each pair and nowhere else. **Four rows have no pak at RetroBat's default**, so a game that
saves only to the pak saves nothing there until the option is set (RB-339). A transfer pak was
not driven: it holds a Game Boy cartridge's save, which is `gb`'s or `gbc`'s. Every Mario Kart 64
EEPROM round-tripped too: RMG `2aac454b...`, simple64 `b809cae4...`, Project64 `bce6f8fc...`, ares
`d6882b6c...`.

## 8. Sessions

`rommbat-agent status --all-sessions` read back 23 sessions for this device on 2026-09-27, one for
every ES launch in `emulatorLauncher.log` and none for the agent's own:

| Row                | Sessions, UTC                                  |
| ------------------ | ---------------------------------------------- |
| `mupen64plus_next` | 12:09:00, 12:10:27 (225805); 12:11:04 (157714) |
| `parallel_n64`     | 12:22:52 (225805); 12:24:26, 12:35:56 (157714) |
| `mupen64`          | 12:44:55 (225805); 12:46:25, 12:47:16 (157714) |
| `simple64`         | 12:59:21 (225805); 13:00:52 (157714)           |
| `project64`        | 13:07:36 (225805); 13:09:32 (157714)           |
| `ares`             | 13:15:41 (225805); 13:16:40 (157714)           |
| `Ares64`           | 13:26:46 (225805); 13:27:54, 13:28:41 (157714) |
| `Mupen64Plus`      | 13:41:53 (225805); 13:43:10, 13:43:45 (157714) |
| `gopher64`         | 13:52:42 (225805); 13:54:30 (157714)           |
