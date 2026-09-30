---
summary: The certification record for `megadrive`: the four rows that needed code: `bizhawk`, `jgenesis`, `mednafen` and `ares`.
read-when: When a result for one of these `megadrive` rows is needed, or before re-driving one.
---

# megadrive: `bizhawk`, `jgenesis`, `mednafen` and `ares`

**All four certified on 2026-09-21, on the second deploy**, the first build to carry their
megadrive rules. Each was first driven on the first deploy, which is how the rules were measured:
until then `bizhawk`'s `.SaveRAM` was reported `not in this release`, and `mednafen`'s and `ares`'s
states were invisible. Its first flush sent **4 saves and 4 states**, everything those sessions had
left unsyncable.

|              | `bizhawk`/`Genplus-gx`                               | `jgenesis`                          | `mednafen`/`megadrive`                               | `ares`/`MegaDrive`                               |
| ------------ | ---------------------------------------------------- | ----------------------------------- | ---------------------------------------------------- | ------------------------------------------------ |
| Selected by  | `megadrive.emulator = bizhawk`, `.core = Genplus-gx` | `megadrive.emulator = jgenesis`     | `megadrive.emulator = mednafen`, `.core = megadrive` | `megadrive.emulator = ares`, `.core = MegaDrive` |
| Confirmed by | `-emulator bizhawk -core Genplus-gx`, `EmuHawk.exe`  | `-emulator jgenesis`, empty `-core` | `-emulator mednafen -core megadrive`                 | `-emulator ares -core MegaDrive`                 |
| ES's slot    | `-state_slot 3`, **taken**                           | `-state_slot 4`, ignored            | `-state_slot 4`, ignored                             | `-state_slot 4`, ignored                         |

**Every one of the four kept its own save**, apart from the `.srm` the `libretro` cores share and
from each other, so the maintainer's data select was new on each.

## Checklist for the four

| #   | `bizhawk`                                   | `jgenesis`                    | `mednafen`                                 | `ares`                        |
| --- | ------------------------------------------- | ----------------------------- | ------------------------------------------ | ----------------------------- |
| 4   | **Pass, both directions**, the title's name | **Pass, both directions**     | **Pass, both directions**, the hashed name | **Pass, both directions**     |
| 5   | **Pass**, two slots, frame inside the state | **Pass**, two slots, no image | **Pass**, two slots, no image              | **Pass**, two slots, no image |
| 7   | **Pass**, carried                           | **Pass**, carried             | **Pass**, carried                          | **Pass**, carried             |
| 8   | **Pass.** 00:05:17Z, 1m 3s                  | **Pass.** 00:10:21Z, 29s      | **Pass.** 00:13:56Z, 24s                   | **Pass.** 00:17:44Z, 33s      |
| 9   | **Pass**                                    | **Pass**                      | **Pass**                                   | **Pass**                      |

## 4. Battery saves on the four

| Row        | File under `saves/megadrive/`                          | Size     | Slot               | md5           |
| ---------- | ------------------------------------------------------ | -------- | ------------------ | ------------- |
| `bizhawk`  | `bizhawk/Sonic and Knuckles & Sonic 3 (W) [!].SaveRAM` | 16,384 B | `bizhawk:battery`  | `dbdd1cbe...` |
| `jgenesis` | `jgenesis/md/<rom>.sav`                                | 512 B    | `jgenesis:battery` | `1e13c9bf...` |
| `mednafen` | `<rom>.c5b1c655c19f462ade0ac4e17a844d10.sav`           | 1,024 B  | `mednafen:battery` | `e720b429...` |
| `ares`     | `ares/Mega Drive/<rom>.ram`                            | 512 B    | `ares:battery`     | `579678d1...` |

**Each keeps its own file, at its own size.** How much of the cartridge's SRAM window an emulator
keeps is its own choice, and none of these four reads another's file or the `libretro` one, since
each keeps it in its own place. RB-277 records the sizes beside the two `libretro` ones.

**`bizhawk` names the save after its own title for the game**, `Sonic and Knuckles & Sonic 3 (W)
[!]`, and the state sidecar reads `Sonic and Knuckles & Sonic 3 (W) [!].Genplus-gx`, which is what
binds it (#151). The title is unique on this install. **Unlike `nes`, a boot did not change the
save**: the agent's launch left a `.SaveRAM.bak` at the same md5, where RB-272 measured NES
games rewriting theirs on boot.

**`jgenesis` keeps megadrive saves under `jgenesis/md/`**, where `nes` used `jgenesis/nes/`: it
names the directory after its own system. `emulatorLauncher` rewrites `custom_save_path` in
`jgenesis-config.toml` per launch, from `saves\nes\jgenesis` to `saves\megadrive\jgenesis`.
RB-279.

**`mednafen` hashed the name because the plain one was free**, which is RB-273 again: no
`<rom>.sav` existed, the `libretro` file being `.srm`. **The hash is of the whole `.md`**, which the
file inside the zip hashes to; there is no header to leave off as there is on `nes`. A restore onto
a device that never held the file computes it from the ROM, through `MednafenRomHash`, and this
pass is what drove that: the restore below placed the save under the computed name. RB-280.

**`ares` names its directory `Mega Drive`, with a space**, where `nes` used `Famicom`. So one
supplement entry per emulator could not describe `ares` any more, and the schema now takes one per
system. RB-281.

All four went up through the flush on the second deploy, were moved out of the tree with both of
their states, and came back through one `saves restore 203767 --apply`: `restored 4 save(s) and 8
state(s), failed 0, 1.2 MB`, exit 0, **all twelve files at their own md5**. A flush afterwards
reported `49 already in step` and sent nothing.

## 5. States on the four

| Row        | Directory under `saves/megadrive/` | Maintainer's                | Agent's                     | Keys       |
| ---------- | ---------------------------------- | --------------------------- | --------------------------- | ---------- |
| `bizhawk`  | `bizhawk/sstates/Genplus-gx/`      | `QuickSave3`, `7a105de9...` | `QuickSave2`, `0d3425aa...` | `Ctrl+F2`  |
| `jgenesis` | `jgenesis/states/`                 | `_0`, `198c84e4...`         | `_1`, `c0487bc1...`         | `F7`, `F2` |
| `mednafen` | `mednafen/sstates/`                | `.mc0`, `e36f9e9a...`       | `.mc1`, `718b43dc...`       | `F7`, `F2` |
| `ares`     | `ares/Mega Drive/`                 | `.bs1`, `08654beb...`       | `.bs2`, `f03ee96e...`       | `F7`, `F2` |

**`bizhawk` took ES's slot and the other three did not**, as on `nes` (RB-269 and RB-275).
The maintainer's pad key wrote `QuickSave3` twice, the second replacing the first as a `.bak`, and
`Ctrl+F2` wrote slot 2. `jgenesis` and `bizhawk` write in their own trees and `emulatorLauncher`
mirrors the file into `saves/` in the same second, with a `.txt` sidecar; the other two write
straight into `saves/`.

**None writes a screenshot file.** BizHawk's frame is `Framebuffer.bmp` inside the `.State`, and
the two slots hold different frames, `26a1f298...` and `554f351e...`, which the md5-equal restore
carries. The other three have nothing to carry, which the preview says: `no screenshot: the server
links none to this state`. `ares` states are a fixed 212,543 B, so only the md5 tells two apart.

**`ares` did not close when asked.** The agent's launch sent `WM_CLOSE` and ares was still
running 15 seconds later, so it was killed. Its `.ram` was unchanged by that launch, and the state
it wrote was on disk before the close.
