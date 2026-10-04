---
summary: How each emulator RetroBat offers for `megadrive` behaves, measured, with RB- IDs.
read-when: Before certifying a `megadrive` row or changing how RomMBat handles a `megadrive` save, state or firmware.
---

# megadrive: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-277. Sonic 3 & Knuckles' `megadrive` SRAM is four sizes across the emulators

Genesis Plus GX, both variants, 980 B, trimmed at the last used byte; PicoDrive 16,384 B, the same bytes zero-padded; BizHawk 16,384 B; mednafen 1,024 B; jgenesis and ares 512 B. The three `libretro` cores share `saves/megadrive/<rom>.srm` and read each other's (the earlier data-select slots showed on each), so switching core uploads a new version of one save at a different size, correctly. The other four each keep their own file and read no one else's

## RB-278. `libretro`/`fbneo` does not boot a `megadrive` library named by No-Intro, and the name is the whole reason

RetroBat launches it with `--subsystem md`, under which FBNeo takes the driver from the file name (`md_` plus the stem) and shows "Unknown Romset" for any other. The same Sonic The Hedgehog (USA, Europe) bytes as `sonic.zip` logged `Romset found` and ran at 320x224; under the No-Intro name FBNeo searched for nothing and sat at 640x480. So the row boots none of a No-Intro set, which is what RomM serves. Its one candidate for the test game is `md_sks3`, a two-ROM lock-on set

## RB-279. `jgenesis` keeps a `megadrive` battery save at `saves/megadrive/jgenesis/md/<rom>.sav`

It is 512 B, named after its own system as `nes` is under `jgenesis/nes/`. `emulatorLauncher` rewrites `custom_save_path` in `jgenesis-config.toml` per launch. States mirror from `emulators/jgenesis/states/md/` into the declared `saves/megadrive/jgenesis/states/` as on `nes` (**275**), `-state_slot 4` ignored, `F7` then `F2` writing `_1`

## RB-280. mednafen's `%M` hash on `megadrive` is the md5 of the whole `.md` inside the zip

It is `c5b1c655...` for the test game, against `90855b51...` for the zip. No header is left off, unlike `nes` (**274**). mednafen used the hashed name because no plain `<rom>.sav` existed, which is **273** holding on a second system. States are `mednafen/sstates/<rom>.<md5>.mc<n>`, `-state_slot 4` ignored. `MednafenRomHash` answers for a plain `.md` only; `.smd` is interleaved and decoded before hashing, and `.bin` and `.gen` are undriven

## RB-281. ares keeps `megadrive` saves and states in `saves/megadrive/ares/Mega Drive/`, with a space

They are `<rom>.ram`, 512 B, and `<rom>.bs<n>` states of a fixed 212,543 B. `nes` is `ares/Famicom/`, so the directory is ares's name for the system, and one supplement entry per emulator cannot describe it: `SaveStateSchema` now keeps one entry per system for a supplement emulator and picks by `(emulator, system)`. `-state_slot 4` ignored; `F7` then `F2` wrote `.bs2`. ares ignored `WM_CLOSE` for 15 s and was killed, its `.ram` unchanged

## RB-282. BizHawk's `megadrive` save behaves as its `nes` one, apart from the boot write

`saves/megadrive/bizhawk/Sonic and Knuckles & Sonic 3 (W) [!].SaveRAM`, named after BizHawk's own title as the sidecar `Sonic and Knuckles & Sonic 3 (W) [!].Genplus-gx` says (**263**); ES's `-state_slot 3` became the current slot (**269**); the frame is inside the `.State` (**268**). But a boot to the title left a `.SaveRAM.bak` at the same md5 rather than rewriting the save, where **272** measured NES games changing theirs

## RB-283. Kega Fusion writes a `megadrive` battery save to `emulators/kega-fusion/<rom>.srm`, outside `saves/`

RetroBat's template `Fusion.ini` sets `SRMFiles=.\..\..\emulators\kega-fusion` beside `StateFiles=.\..\..\saves\megadrive\kega-fusion`, and `emulatorLauncher` rewrote neither over five launches. A RetroBat defect, reported upstream as emulatorlauncher#1390: RomMBat does not read the `.srm` there, since its one rule in that folder claims `mastersystem`'s `.ssm` only (#381), and writing the key is ruled out by core principle 2. A real save made later, `9f80af5c...`, is 980 B in Genesis Plus GX's layout and differs from the libretro file in the 6 bytes of the slot chosen, so pointed at `saves\megadrive` Kega would share the libretro cores' loose `.srm`. States land in `saves/megadrive/kega-fusion/<rom>.gs<slot>`, `F5` saving and `F7` stepping the slot down with a wrap from 0 to 9. Kega is not bundled: the folder held only the template until ES downloaded the emulator on a first launch. `auto`, `genesis` and `megadrive` are its `-auto`, `-gen` and `-md` flags

## RB-284. Kega Fusion is playable by pad from ES only after a remap in Kega's own menu

`emulatorLauncher` logged `No specific mapping found for megadrive controller` and wrote `Joystick1Using=3` for player 1 with the Xbox 360 pad on device 0, and RetroBat ships no pad-to-key file for `kega-fusion`, so no pad button saves a state. The keyboard mapping it wrote reaches the game (numpad 8, 2, 4, 6; `J`, `K`, `L`; Right Ctrl for Start). The maintainer remapped in Kega's menu, which Kega saved as `Joystick1Using=2` and arrow keys, and then played to data select and made a save. The agent's blind keyboard Starts never reached data select, which was its timing, not the game. The remap survives: after a later ES launch the file still held `Joystick1Using=2` and the arrow keys, and the pad played, so `emulatorLauncher` does not write its mapping back over Kega's own
