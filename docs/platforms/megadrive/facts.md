---
summary: How each emulator RetroBat offers for `megadrive` behaves, measured, with RB- IDs.
read-when: Before certifying a `megadrive` row or changing how RomMBat handles a `megadrive` save, state or firmware.
---

# megadrive: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-277. Sonic 3 & Knuckles' `megadrive` SRAM is four sizes across the emulators

Verified: RetroBat 8.3.0, 2026-10-14. How: booted Sonic & Knuckles + Sonic The Hedgehog 3 (USA) (Lock-on Combination) under every row but `fbneo` through `emulatorLauncher` and read each save's size.
Genesis Plus GX, both variants, 980 B, trimmed at the last used byte; PicoDrive 16,384 B, the same bytes zero-padded; BizHawk 16,384 B; mednafen 1,024 B; jgenesis and ares 512 B. The three `libretro` cores share `saves/megadrive/<rom>.srm`, `genesis_plus_gx_wide` rewriting Genesis Plus GX's 980 B file byte for byte and PicoDrive's 16,384 B run replacing it, so switching core uploads a new version of one save at a different size, correctly. Kega Fusion shares that file too since 8.3.0 (RB-283). The other four each keep their own file

## RB-278. `libretro`/`fbneo` does not boot a `megadrive` library named by No-Intro, and the name is the whole reason

Verified: RetroBat 8.3.0, 2026-10-14. How: booted Sonic The Hedgehog (USA, Europe) under `libretro`/`fbneo`, then the same bytes zipped as `sonic.zip`, and read the screen.
RetroBat launches it with `--subsystem md`, under which FBNeo takes the driver from the file name (`md_` plus the stem) and shows "Unknown Romset" for any other. The same Sonic The Hedgehog (USA, Europe) bytes as `sonic.zip` ran to the title screen; under the No-Intro name FBNeo showed "Romset is unknown". So the row boots none of a No-Intro set, which is what RomM serves

## RB-279. `jgenesis` keeps a `megadrive` battery save at `saves/megadrive/jgenesis/md/<rom>.sav`

Verified: RetroBat 8.3.0, 2026-10-14. How: booted Sonic & Knuckles + Sonic The Hedgehog 3 (USA) (Lock-on Combination) under the row through `emulatorLauncher`, saved states on `F2`, `F7`, `F2` (`-state_slot 4` on a second launch), read the tree and `jgenesis-config.toml`.
It is 512 B, named after its own system as `nes` is under `jgenesis/nes/`. `emulatorLauncher` rewrites `custom_save_path` in `jgenesis-config.toml` per launch. States mirror from `emulators/jgenesis/states/md/` into the declared `saves/megadrive/jgenesis/states/` as on `nes` (**275**), `-state_slot 4` ignored, `F7` then `F2` writing `_1`

## RB-280. mednafen's `%M` hash on `megadrive` is the md5 of the whole `.md` inside the zip

Verified: RetroBat 8.3.0, 2026-10-14. How: booted Sonic & Knuckles + Sonic The Hedgehog 3 (USA) (Lock-on Combination) under the row through `emulatorLauncher`, saved states on `F2`, `F7`, `F2`, then again with `-state_slot 4`, and computed the md5 of the `.md` and of the zip.
It is `c5b1c655...` for the test game, against `90855b51...` for the zip. No header is left off, unlike `nes` (**274**). mednafen used the hashed name because no plain `<rom>.sav` existed, which is **273** holding on a second system. States are `mednafen/sstates/<rom>.<md5>.mc<n>`, `-state_slot 4` ignored. `MednafenRomHash` answers for a plain `.md` only; `.smd` is interleaved and decoded before hashing, and `.bin` and `.gen` are undriven

## RB-281. ares keeps `megadrive` saves and states in `saves/megadrive/ares/Mega Drive/`, with a space

Verified: RetroBat 8.3.0, 2026-10-14. How: booted Sonic & Knuckles + Sonic The Hedgehog 3 (USA) (Lock-on Combination) under the row through `emulatorLauncher`, saved states on `F2`, `F7`, `F2`, then again with `-state_slot 4`, read the tree.
They are `<rom>.ram`, 512 B, and `<rom>.bs<n>` states of a fixed 212,543 B. `nes` is `ares/Famicom/`, so the directory is ares's name for the system, and one supplement entry per emulator cannot describe it: `SaveStateSchema` now keeps one entry per system for a supplement emulator and picks by `(emulator, system)`. `-state_slot 4` ignored; `F7` then `F2` wrote `.bs2`.

## RB-282. BizHawk's `megadrive` save behaves as its `nes` one, apart from the boot write

Verified: RetroBat 8.3.0, 2026-10-14. How: booted Sonic & Knuckles + Sonic The Hedgehog 3 (USA) (Lock-on Combination) under the row through `emulatorLauncher`, saved states on `F2`, `F7`, `F2` (`-state_slot 3` on a second launch), read the tree.
`saves/megadrive/bizhawk/Sonic and Knuckles & Sonic 3 (W) [!].SaveRAM`, named after BizHawk's own title as the sidecar `Sonic and Knuckles & Sonic 3 (W) [!].Genplus-gx` says (**263**); ES's `-state_slot 3` became the current slot (**269**); the frame is inside the `.State` (**268**). But a boot to the title left a `.SaveRAM.bak` at the same md5 rather than rewriting the save, where **272** measured NES games changing theirs

## RB-283. Kega Fusion writes a `megadrive` battery save loose in `saves/megadrive/` and its states under `kega-fusion/sstates/`, and reads the file the `libretro` cores share

Verified: RetroBat 8.3.0, 2026-10-14. How: launched Sonic & Knuckles + Sonic The Hedgehog 3 (USA) (Lock-on Combination) under `kega-fusion` through `emulatorLauncher` with PicoDrive's 16,384 B `.srm` in place, saved states on `F5`, `F7`, `F5`, read `Fusion.ini` and the tree, and launched Golden Axe Warrior the same way on `mastersystem`.
Each launch rewrites `Fusion.ini` with `SRMFiles=<root>\saves\<system>` and `StateFiles=<root>\saves\<system>\kega-fusion\sstates` (emulatorlauncher#1390), where 8.2.1 left the template's `emulators\kega-fusion` and `saves\megadrive\kega-fusion`. Kega kept PicoDrive's `.srm` at the same md5 and size, so a Kega run shares the loose file the three `libretro` cores share (**277**), and its states are `saves/megadrive/kega-fusion/sstates/<rom>.gs0` and `.gs9`, 140,408 B each, with `F5` saving and `F7` stepping the slot down with a wrap from 0 to 9. `SxMFiles` and `BRMFiles` still name `emulators\kega-fusion`, so `mastersystem`'s `.ssm` is written there, 8,191 B (#381). RomMBat's `kega-fusion` supplement entry still names `{{system}}/kega-fusion`, so the states are not found (#519). Kega is not bundled: the folder held only the template until ES downloaded the emulator on a first launch. `auto`, `genesis` and `megadrive` are its `-auto`, `-gen` and `-md` flags; `-md` was measured

## RB-284. Kega Fusion is playable by pad from ES only after a remap in Kega's own menu

`emulatorLauncher` logged `No specific mapping found for megadrive controller` and wrote `Joystick1Using=3` for player 1 with the Xbox 360 pad on device 0, and RetroBat ships no pad-to-key file for `kega-fusion`, so no pad button saves a state. The keyboard mapping it wrote reaches the game (numpad 8, 2, 4, 6; `J`, `K`, `L`; Right Ctrl for Start). The maintainer remapped in Kega's menu, which Kega saved as `Joystick1Using=2` and arrow keys, and then played to data select and made a save. The agent's blind keyboard Starts never reached data select, which was its timing, not the game. The remap survives: after a later ES launch the file still held `Joystick1Using=2` and the arrow keys, and the pad played, so `emulatorLauncher` does not write its mapping back over Kega's own
