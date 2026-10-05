---
summary: The certification record for `gamegear`: the three certified standalone rows, and jgenesis.
read-when: When a result for one of these `gamegear` rows is needed, or before re-driving one.
---

# gamegear: The three standalone rows, and jgenesis

|            | Selected by                      | Confirmed on the ES launch line            |
| ---------- | -------------------------------- | ------------------------------------------ |
| `mednafen` | the game's emulator option in ES | `-emulator mednafen -core gg`              |
| `ares`     | the game's emulator option in ES | `-emulator ares -core GameGear`            |
| `SMSHawk`  | the game's emulator option in ES | `-emulator bizhawk -core SMSHawk`          |
| `jgenesis` | the game's emulator option in ES | `-emulator jgenesis -core genesis_plus_gx` |

## Checklist for the three

| #   | Result on every one of the three                                                            |
| --- | ------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions**, every file at its own md5 after a restore                        |
| 5   | **Pass**, two slots each, where each emulator was found to write                            |
| 6   | **N/A**                                                                                     |
| 7   | **Pass**, carried: each was launched from ES on the synced ROM, art and description present |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                    |
| 9   | **Pass.** 546 present and verified, 2,046 media present, gamelist byte-identical, 0 sent    |

## 4. Battery saves on the three

| Row        | File under `saves/gamegear/`                 | Size     | Slot               | After the row | Changed against its seed |
| ---------- | -------------------------------------------- | -------- | ------------------ | ------------- | ------------------------ |
| `mednafen` | `<rom>.8430050c60db46b3887cf7d7cf2f206f.sav` | 32,768 B | `mednafen:battery` | `d3fb8dda...` | 80 bytes                 |
| `ares`     | `ares/Game Gear/<rom>.ram`                   | 32,768 B | `ares:battery`     | `07aad490...` | 82 bytes                 |
| `SMSHawk`  | `bizhawk/Defenders of Oasis (UE).SaveRAM`    | 8,192 B  | `bizhawk:battery`  | `ad286b39...` | 76 bytes                 |

**Each read the last row's save**: the game's Continue resumed where the previous row stopped, and each
wrote its own progress over it. **mednafen writes its hashed name only**, the md5 being of the whole
`.gg`, and on exit. **ares writes on exit**, and a launch that stays at the title rewrites the same
bytes. **BizHawk names the save after its own title**, `Defenders of Oasis (UE)`, which `saves` joined
to the ROM through the ES launch covering the write, the sidecar beside its states,
`Defenders of Oasis (UE).SMSHawk`, saying the same; it keeps the save it replaced as `.SaveRAM.bak`,
which the rule ignores. **BizHawk needed no firmware.**

At the end the three files were deleted and `saves restore 272855 --apply` brought each back under its
own name, the hashed and the title-named alike, at its own md5, `restored 5 save(s) ... failed 0`
across the system's five.

## 5. States on the three

| Row        | Directory under `saves/gamegear/` | Slots and who made them                          | Round-tripped               |
| ---------- | --------------------------------- | ------------------------------------------------ | --------------------------- |
| `mednafen` | `mednafen/sstates/`               | `.<md5>.mc0` and `.mc9`, both in ES              | `.mc9`, `69a38d0e...`       |
| `ares`     | `ares/Game Gear/`                 | `.bs1` and `.bs2`, both by the agent             | `.bs2`, `0063ae6d...`       |
| `SMSHawk`  | `bizhawk/sstates/SMSHawk/`        | `QuickSave2` and `QuickSave4`, both by the agent | `QuickSave4`, `34bec4ec...` |

**`mednafen` and `ares` declare no state directory**, and each wrote to the one above, which the
supplement declares for `gamegear` (#453). **The maintainer's second mednafen slot was 9**: the slot
stepped down from 0 and wrapped, as on `mastersystem`. **ares keeps its states beside its battery
save** and shows nothing when one is saved, so the agent made both from its own session, `F2`, `F7`,
`F2`. **BizHawk's states were made by the agent with `Ctrl+F2` and `Ctrl+F4`**, the session in ES
having ended before one was saved; their frames, `Framebuffer.bmp` inside each state, differ
(`392452d4...`, `cc7cdb0e...`). **None of the three writes a screenshot file**, which the preview says:
`no screenshot: the server links none to this state`.

## 8. Sessions

| Row        | Journal, UTC         | Length |
| ---------- | -------------------- | ------ |
| `mednafen` | 10:55:13 to 10:56:20 | 1m 7s  |
| `ares`     | 12:03:06 to 12:03:37 | 31s    |
| `SMSHawk`  | 12:05:46 to 12:06:09 | 23s    |
| `jgenesis` | 12:08:05 to 12:10:01 | 1m 55s |

Every one is on the server, rom 272855, jgenesis's being the black-screen session.

## `jgenesis`: driven, and not certifiable at this floor

**From ES the game runs to a black screen.** `emulatorLauncher` starts
`jgenesis-cli.exe -f <rom> --hardware MasterSystem` for every `gamegear` game; the window title reads
`sms - Defenders of Oasis (USA, Europe)`, and the game runs unseen, writing its header to the save.
The agent's own launches through `emulatorLauncher` showed the same, with and without a seed in place,
and so did its boot launch the night before, whose screenshot was black although its write looked
normal. **Run by hand with `--hardware GameGear`**, the title reads `gg - ...` and the game plays in
colour. No ES option sets the flag (RB-410), so the row as a user gets it cannot be played.

**Its saves were measured anyway, from that run by hand, as evidence for when RetroBat launches it
right.** The maintainer played on the keyboard, since a run outside `emulatorLauncher` gets none of
its pad setup:

| What                   | Result                                                                                         |
| ---------------------- | ---------------------------------------------------------------------------------------------- |
| Battery save           | `jgenesis/gg/<rom>.sav`, 32,768 B, `9b53a63a...`, 84 bytes changed against BizHawk's seed      |
| Its upload and restore | Went up as `jgenesis:battery`, and came back at its own md5 with the other four                |
| States                 | `_0.jst` and `_1.jst`, 46,855 B and 49,552 B, in `emulators/jgenesis/states/gg/`               |
| States under `saves/`  | None: only `emulatorLauncher` mirrors them into `saves/gamegear/jgenesis/states/`, so unproven |

**jgenesis rewrites its `.sav` every two seconds while the game autosaves**, and fills the upper 24 KB
of a fresh one with `0x00`.
