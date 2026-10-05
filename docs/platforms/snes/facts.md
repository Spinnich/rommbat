---
summary: How each emulator RetroBat offers for `snes` behaves, measured, with RB- IDs.
read-when: Before certifying a `snes` row or changing how RomMBat handles a `snes` save, state or firmware.
---

# snes: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-317. Three of fifteen `snes` rows refuse a DSP-1 cartridge, and RetroBat lists nothing for `snes`

On Super Mario Kart with no chip firmware anywhere, `libretro`/`mesen-s` holds on the Nintendo logo and logs that it could not find firmware for DSP `dsp1b.rom`, Mesen asks to select `dsp1b.rom`, 8,192 B, and jgenesis exits 1 with "Cannot load DSP-1 cartridge because DSP-1 ROM is not configured", its `dsp1_rom_path` unset. `bizhawk`/`BSNES` warns "Couldn't find firmware SNES+DSP1b" and carries on. The other eleven reach the title screen, which does not exercise the DSP. `bios snes` answers "RetroBat requires no BIOS for snes", and no sibling's list names the chips, so the supplement has nothing to copy. RomM's known-files list names `snes:dsp1b.data.rom`, `dsp1b.program.rom` and the `st010` pair, the split form bsnes and ares read, not Mesen's single `dsp1b.rom`. **With the firmware all four start**: the split pair at RomM's md5s and `dsp1b.rom` made by joining them, program first, 8,192 B. `mesen-s` and BizHawk find it in `bios\`; Mesen reads only `emulators\mesen\Firmware\`, where RetroBat puts nothing, and jgenesis only the `dsp1_rom_path` key, which RetroBat's launcher never sets and keeps when set by hand. By the RetroBat team's account, relayed by the maintainer, RetroBat's firmware list covers only a system's default emulator, by design, and `snes`'s default, `libretro`/`snes9x`, needs none.

## RB-318. Mesen and Snes9x share the `libretro` `.srm` on `snes`, mednafen joins it, and ares, BizHawk and jgenesis keep their own

Mesen and Snes9x write the loose `<rom>.srm` the seven `libretro` cores share, and state to `mesen/SaveStates/<rom>_<n>.mss` and `snes9x/sstates/<rom>.00<n>`, Shift+F10 being slot 0, the paths RetroBat's `settings.json` and `snes9x.conf` name. **mednafen writes a `.srm` on `snes`**, not the `.sav` of its other systems: `<rom>.608c22b8ff930c62dc2de54bcd6eba72.srm`, the md5 of the whole `.sfc` inside the zip, when no plain `<rom>.srm` is there, and it read and saved into the plain one when it was, as on `gb` and `gbc` (273, 301). Its states are `mednafen/sstates/<rom>.<md5>.mc<n>`. **ares keeps both halves together**: `ares/Super Famicom/<rom>.ram` beside `.bs1` and `.bs2`, written on exit, and a DSP-1 cartridge adds `<rom>.dram`, 512 B, the chip's data RAM. BizHawk's three cores share `bizhawk/<rom>.SaveRAM`, **named after the ROM file on `snes`**, where on `gb` and `gbc` it took BizHawk's own title; the sidecar reads `<rom>.BSNES.Compatibility`. jgenesis writes `jgenesis/sfc/<rom>.sav` and states to `emulators/jgenesis/states/sfc/` under `state_path = "EmulatorFolder"`, which `emulatorLauncher` mirrors into the declared `saves/snes/jgenesis/states/`. Zelda writes its SRAM at boot, so any launch changes the `.srm`. ares shows nothing on screen when a state is saved or its slot steps.

## RB-319. `libretro`/`mednafen_snes` leaves an empty `<rom>.rtc` beside the `.srm`

It writes the 0 B file on every exit for a cartridge with no clock, Zelda, Super Mario Kart, Kirby's Dream Land 3, Yoshi's Island and New Horizons alike, and no other `snes` row writes or touches it then. An empty `.rtc` on `snes` is passed over. For an S-RTC cartridge the file holds the clock, and the `libretro` rule takes it (RB-417).

## RB-320. Not every rom in the `snes` set is a game

`Dark Law - Meaning of Death (Japan) [T-En by AGTP v1.00].zip` holds a single `.bps`, 177,786 B, a translation patch for a ROM the library does not carry. It syncs as any other member, and what the library holds is the user's to keep.

## RB-321. jgenesis closed on its first launch from ES, and the cause was not found

Launched from ES under `jgenesis`, it exited with code 1 after 3.5 s; the launch was the first of the pass whose pad reported as an Xbox 360 Controller. It did not recur: not from `emulatorLauncher` with and without ES's `-p1` arguments, not from its own command line, not on the exact seed it was given in a scratch folder with a copy of its config, and not on the maintainer's next launch from ES. Exit 1 is an error jgenesis returned rather than a panic, and `emulatorLauncher` discards its stderr, so its reason was not seen.

## RB-417. An S-RTC cartridge's clock is a second file on eight `snes` rows, in four names

Verified: RetroBat 8.2.1, 2026-10-05. How: launched Super Shell Monsters Story II (Japan) [T-En by Dynamic Designs v0.90] under all fifteen rows through `emulatorLauncher`, closed each and read `saves/snes/`.

The cartridge carries an S-RTC, and these rows keep its clock beside the 8,192 B save: a loose `<rom>.rtc` under `libretro`'s `snes9x` and `mednafen_snes`, 20 B, and `bsnes`, 16 B, which standalone Snes9x writes too, 20 B; mednafen's `<rom>.<md5>.rtc`, 20 B, beside its hashed `.srm`; ares's `ares/Super Famicom/<rom>.rtc`, 16 B; and jgenesis's `jgenesis/sfc/<rom>.rtc`, 36 B. **`libretro`/`bsnes-jg` names its clock `<rom>.zip#<member>.rtc`**, 16 B, a stem the loose rule does not join, so RomMBat holds it as a `libretro:battery:rtc` save with no rom. `bsnes_hd_beta`, `mesen-s` and standalone Mesen write no clock file, and BizHawk's `Snes9x` core appends 20 B to its `.SaveRAM`, 8,212 B where `BSNES` and `Faust` write 8,192 B into the same shared file. **`libretro`/`snes9x2005` refuses the cartridge**: `emulatorLauncher` exits 0 within 25 s and nothing is written. The cartridge is also a 6 MB ExHiROM, so which of the two it refuses was not separated. Each clock takes its own class B slot beside the save, as on `gb` and `gbc`, and the save keeps the slot it had.

## RB-418. A `snes` coprocessor does not change how a row sizes or names the battery save, and ares adds the SA-1's internal RAM

Verified: RetroBat 8.2.1, 2026-10-05. How: launched Super Mario Kart (USA), Kirby's Dream Land 3 (USA), Super Mario World 2 - Yoshi's Island (USA) (Rev 1) and New Horizons (USA) under all fifteen rows through `emulatorLauncher`, closed each and read `saves/snes/`.

Every row that wrote a save wrote the size the cartridge header declares: 2,048 B for Super Mario Kart, a DSP-1 HiROM cartridge; 32,768 B for Kirby's Dream Land 3, an SA-1 cartridge whose BW-RAM it is; 32,768 B for Yoshi's Island, a Super FX cartridge that declares it in the expansion-RAM byte rather than the SRAM one; and 32,768 B for New Horizons, a HiROM cartridge. Names follow RB-318 on every row. **ares writes the SA-1's internal RAM as `ares/Super Famicom/<rom>.iram`**, 2,048 B, beside the `.ram`, as it writes a DSP-1's data RAM as `.dram`, and both take a class B slot of their own. `libretro`/`mesen-s` wrote nothing for Yoshi's Island in a 25 s boot, where every other row wrote the 32,768 B file at once. With the DSP-1 firmware in place, all fifteen rows boot Super Mario Kart (RB-317).
