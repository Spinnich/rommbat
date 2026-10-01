---
summary: The certification record for `megadrive`: the three `kega-fusion` rows.
read-when: When a result for one of these `megadrive` rows is needed, or before re-driving one.
---

# megadrive: The three `kega-fusion` rows

**Driven, and not certified: step 4 cannot pass on RetroBat 8.2.1.** The other eight steps pass or
carry.

|              | `kega-fusion`/`auto`                               | `kega-fusion`/`genesis`             | `kega-fusion`/`megadrive`             |
| ------------ | -------------------------------------------------- | ----------------------------------- | ------------------------------------- |
| Selected by  | `megadrive.emulator = kega-fusion`, `.core = auto` | the install launch, `-core genesis` | the agent's launch, `-core megadrive` |
| Confirmed by | `Fusion.exe -auto`                                 | `Fusion.exe -gen`                   | `Fusion.exe -md`                      |

**Kega Fusion was not on the install at the start.** RetroBat ships only its template
`Fusion.ini` into `emulators/kega-fusion/`; the maintainer installed the emulator through ES,
which downloads it on the first launch of a game under it. **The three cores are Kega's command
line flags** for the console's region handling, and all three write to the same places.

| #   | All three rows                                                                                             |
| --- | ---------------------------------------------------------------------------------------------------------- |
| 1-3 | **Pass**, carried                                                                                          |
| 4   | **Fail on 8.2.1.** A real save was made and lands in `emulators/kega-fusion/`, which RomMBat does not scan |
| 5   | **Pass**, two slots round-tripped at their own md5                                                         |
| 6   | **N/A**                                                                                                    |
| 7   | **Pass** on launch and art. The pad works only after a remap in Kega's own menu, RB-284                    |
| 8   | **Pass** for `auto`, 00:33:59Z (44s) and 01:06:41Z (48s). Carried to the other two                         |
| 9   | **Pass.** 0 downloaded, 0 written, gamelist identical                                                      |

## 4. Where Kega Fusion puts a battery save

**`emulators/kega-fusion/<rom>.srm`.** The install launch wrote `Sonic & Knuckles + Sonic The
Hedgehog 3 (USA) (Lock-on Combination).srm` there, 980 B. RetroBat's `Fusion.ini` template sets
`SRMFiles=.\..\..\emulators\kega-fusion` while `StateFiles=.\..\..\saves\megadrive\kega-fusion`,
and `emulatorLauncher` rewrote neither on any of five launches. RomMBat does not read the `.srm`
there: its one rule in that folder claims `mastersystem`'s `.ssm` only (#381), and core principle 2
rules out writing the key itself. **Recorded as a RetroBat
defect, reported upstream as
[emulatorlauncher#1390](https://github.com/RetroBat-Official/emulatorlauncher/issues/1390), by the
maintainer's ruling**, rather than widened around.
RB-283.

**A real save was made there, and it is the libretro cores' format.** After remapping input in
Kega's own menu, the maintainer launched from ES under `-auto` at 01:06:41Z, chose a data-select
slot, and Kega rewrote the file at 980 B, `9f80af5c...`. It matches Genesis Plus GX's file in
layout and length and differs from it in 6 bytes, the slot chosen. So with `SRMFiles` pointed at
`saves\megadrive`, Kega would read and write the same loose `.srm` the three `libretro` cores
share, and its rows would be the libretro family's step 4. That is what the upstream report asks
for. Until then step 4 fails on the location alone, not on the save.

**The agent's own keyboard attempts never reached data select**, under `-auto` and `-gen`: a
blind Start from the title went straight into the game. The maintainer's session did reach it, so
that was the agent's input timing, not Kega's handling of the cartridge.

## 5. Kega Fusion's states

**`saves/megadrive/kega-fusion/<rom>.gs<slot>`**, where `Fusion.ini` sends them. `F5` saves and
`F7` steps the slot down, wrapping from 0 to 9:

| Slot | md5           | Size      |
| ---- | ------------- | --------- |
| 0    | `91ada93f...` | 140,408 B |
| 9    | `0d173d84...` | 140,408 B |

`es_savestates.cfg` declares nothing for `kega-fusion`, so the third deploy adds a supplement
entry for it, scoped to `megadrive`. Its first flush sent both states, and both came back through
`saves restore --apply` at their own md5, exit 0. No image is written. `Fusion.ini` also names
state folders for `mastersystem` and `segacd`; neither was driven, and neither is declared.

## 7. Playable by pad only after a remap in Kega's own menu

**The maintainer's Xbox 360 pad did nothing in Kega Fusion as `emulatorLauncher` configured it.**
It logged `No specific mapping found for megadrive controller` and wrote `Joystick1Using=3` for
player 1 while the pad is device 0, and RetroBat ships no pad-to-key file for `kega-fusion`, so no
pad button saves a state. The keyboard mapping it wrote does reach the game: numpad 8, 2, 4 and 6
for the pad, `J`, `K` and `L` for A, B and C, and Right Ctrl for Start.

**Remapping inside Kega made it playable.** The maintainer set the controls in Kega's own menu,
which Kega saved into `Fusion.ini` on exit as `Joystick1Using=2` and arrow keys for
`Player1Keys`, and then played the save above. **The remap survives later launches**: after an ES
launch at 06:11:24 local on 2026-09-22 the file still held `Joystick1Using=2` and the arrow keys,
so `emulatorLauncher` does not write its mapping back over it, and the pad played. So the fix for
a user is one visit to Kega's menu, not a setting RetroBat has to change. RB-284.
