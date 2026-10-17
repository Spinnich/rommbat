---
summary: How each emulator RetroBat offers for `dreamcast` behaves, measured, with RB- IDs.
read-when: Before certifying a `dreamcast` row or changing how RomMBat handles a `dreamcast` save, state or firmware.
---

# dreamcast: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-9. Flycast keeps four port-keyed VMU files shared by every game, and `flycast_vmupergame` converts port 1 to one per game

Verified: RetroBat 8.3.0, 2026-10-14. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only dreamcast change is Flycast's window position, which writes no VMU, save or BIOS path. How: launched Bangai-O (USA) under `flycast`/`dreamcast` through `emulatorLauncher` for 45 s, with and without `dreamcast["Bangai-O (USA).chd"].flycast_vmupergame` set to `1` in `es_settings.cfg`, read `emu.cfg` and `saves/dreamcast/flycast/vmu/`; `es_settings.cfg` was restored afterwards.
`saves/dreamcast/flycast/vmu/` holds `vmu_save_A1.bin` to `vmu_save_D1.bin`, 131,072 B each, one per controller port, slot 1, with nothing in a path to say which game wrote it. `es_features.cfg` declares the option as `PER GAME VMU`, `flycast_vmupergame`, "each game will have its own VMU in port 1". So Dreamcast joins the convertible set, and a game that writes to a second VMU still produces a file RomMBat cannot attribute

## RB-49. The per-game VMU is named for the disc serial, not the ROM file

Verified: RetroBat 8.3.0, 2026-10-14. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only dreamcast change is Flycast's window position, which writes no VMU, save or BIOS path. How: launched Bangai-O (USA) under `flycast`/`dreamcast` through `emulatorLauncher` for 45 s, with and without `dreamcast["Bangai-O (USA).chd"].flycast_vmupergame` set to `1` in `es_settings.cfg`, read `emu.cfg` and `saves/dreamcast/flycast/vmu/`; `es_settings.cfg` was restored afterwards.
`T40217N_vmu_save_A1.bin` is Bangai-O's serial `T-40217N` with the hyphen dropped, and `Bangai-O (USA).chd` appears nowhere in the path, so it does not collapse into class A the way DuckStation's `PerGameFileTitle` card does. Attributing it means reading the serial out of the disc image or attributing by the launch window from `emulatorLauncher.log`. PS1 stays on its stock database-keyed mode, so serial attribution is the common case for disc systems

## RB-362. Flycast creates all four shared VMU files at its first launch, and a launch rewrites port 1's with no save made

Verified: RetroBat 8.3.0, 2026-10-14. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only dreamcast change is Flycast's window position, which writes no VMU, save or BIOS path. How: launched Bangai-O (USA) under `flycast`/`dreamcast` through `emulatorLauncher` for 45 s, with and without `dreamcast["Bangai-O (USA).chd"].flycast_vmupergame` set to `1` in `es_settings.cfg`, read `emu.cfg` and `saves/dreamcast/flycast/vmu/`; `es_settings.cfg` was restored afterwards. A second launch with no override followed.
The first launch wrote `vmu_save_A1.bin`, `71255c8c...`, and `B1`, `C1` and `D1`, `e72afc56...` each, the same blank image three times. The second changed `vmu_save_A1.bin` to `1bb9a55d...` with nothing saved in the game and left the other three alone. So mtime and size cannot say whether a shared card needs uploading: only the contents separate a formatted empty card from one holding a save

## RB-363. The per-game option works, reaches Flycast's own `emu.cfg`, and leaves the shared files untouched when they exist

Verified: RetroBat 8.3.0, 2026-10-14. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only dreamcast change is Flycast's window position, which writes no VMU, save or BIOS path. How: launched Bangai-O (USA) under `flycast`/`dreamcast` through `emulatorLauncher` for 45 s, with and without `dreamcast["Bangai-O (USA).chd"].flycast_vmupergame` set to `1` in `es_settings.cfg`, read `emu.cfg` and `saves/dreamcast/flycast/vmu/`; `es_settings.cfg` was restored afterwards.

| Run                                    | `emu.cfg`              | What `saves/dreamcast/flycast/vmu/` did                                             |
| -------------------------------------- | ---------------------- | ----------------------------------------------------------------------------------- |
| control, no override                   | `PerGameVmu = no`      | `vmu_save_A1.bin` created, then changed at the next launch                          |
| `flycast_vmupergame=1`, shared present | **`PerGameVmu = yes`** | **`T40217N_vmu_save_A1.bin` created, 131,072 B**; the four shared files not touched |
| `flycast_vmupergame=1`, folder empty   | **`PerGameVmu = yes`** | the per-game file **and** all four shared files created, `B1` to `D1` blank         |

`Dreamcast.VMUPath` is `saves/dreamcast/flycast/vmu` in every run, so per-game and shared files sit side by side. The per-game `es_settings.cfg` key reaches a standalone emulator's generated config, as it does a `libretro` one. Ports B to D never held a game's data in these runs, but Flycast creates them blank whenever they are absent, whether or not the option is set
