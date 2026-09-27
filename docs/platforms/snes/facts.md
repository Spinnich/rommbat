---
summary: How each emulator RetroBat offers for `snes` behaves, measured, with RB- IDs.
read-when: Before certifying a `snes` row or changing how RomMBat handles a `snes` save, state or firmware.
---

# snes: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-317. Three of fifteen refuse a DSP-1 cartridge, and RetroBat lists nothing for `snes`

Question: Which `snes` rows need firmware, and whether RetroBat's list names it

Measured: **Three of fifteen refuse a DSP-1 cartridge, and RetroBat lists nothing for `snes`.** On Super Mario Kart with no chip firmware anywhere, `libretro`/`mesen-s` holds on the Nintendo logo and logs that it could not find firmware for DSP `dsp1b.rom`, Mesen asks to select `dsp1b.rom`, 8,192 B, and jgenesis exits 1 with "Cannot load DSP-1 cartridge because DSP-1 ROM is not configured", its `dsp1_rom_path` unset. `bizhawk`/`BSNES` warns "Couldn't find firmware SNES+DSP1b" and carries on. The other eleven reach the title screen, which does not exercise the DSP. `bios snes` answers "RetroBat requires no BIOS for snes", and no sibling's list names the chips, so the supplement has nothing to copy. RomM's known-files list names `snes:dsp1b.data.rom`, `dsp1b.program.rom` and the `st010` pair, the split form bsnes and ares read, not Mesen's single `dsp1b.rom`. **With the firmware all four start**: the split pair at RomM's md5s and `dsp1b.rom` made by joining them, program first, 8,192 B. `mesen-s` and BizHawk find it in `bios\`; Mesen reads only `emulators\mesen\Firmware\`, where RetroBat puts nothing, and jgenesis only the `dsp1_rom_path` key, which RetroBat's launcher never sets and keeps when set by hand. By the RetroBat team's account, relayed by the maintainer, RetroBat's firmware list covers only a system's default emulator, by design, and `snes`'s default, `libretro`/`snes9x`, needs none.

## RB-318. Where each standalone `snes` row puts its battery save and its states

Question: Where each standalone `snes` row puts its battery save and its states

Measured: Mesen and Snes9x write the loose `<rom>.srm` the seven `libretro` cores share, and state to `mesen/SaveStates/<rom>_<n>.mss` and `snes9x/sstates/<rom>.00<n>`, Shift+F10 being slot 0, the paths RetroBat's `settings.json` and `snes9x.conf` name. **mednafen writes a `.srm` on `snes`**, not the `.sav` of its other systems: `<rom>.608c22b8ff930c62dc2de54bcd6eba72.srm`, the md5 of the whole `.sfc` inside the zip, when no plain `<rom>.srm` is there, and it read and saved into the plain one when it was, as on `gb` and `gbc` (273, 301). Its states are `mednafen/sstates/<rom>.<md5>.mc<n>`. **ares keeps both halves together**: `ares/Super Famicom/<rom>.ram` beside `.bs1` and `.bs2`, written on exit, and a DSP-1 cartridge adds `<rom>.dram`, 512 B, the chip's data RAM. BizHawk's three cores share `bizhawk/<rom>.SaveRAM`, **named after the ROM file on `snes`**, where on `gb` and `gbc` it took BizHawk's own title; the sidecar reads `<rom>.BSNES.Compatibility`. jgenesis writes `jgenesis/sfc/<rom>.sav` and states to `emulators/jgenesis/states/sfc/` under `state_path = "EmulatorFolder"`, which `emulatorLauncher` mirrors into the declared `saves/snes/jgenesis/states/`. Zelda writes its SRAM at boot, so any launch changes the `.srm`. ares shows nothing on screen when a state is saved or its slot steps.

## RB-319. What `libretro`/`mednafen_snes` leaves beside the `.srm`

Question: What `libretro`/`mednafen_snes` leaves beside the `.srm`

Measured: **An empty `<rom>.rtc`**, 0 B, on every exit, for Zelda and Super Mario Kart alike, and no other `snes` row writes or touches it. It was reported as a shape RomMBat does not recognise; an empty `.rtc` on `snes` is now passed over, and one with content is still reported, since no row was seen to write one.

## RB-320. Whether every rom in the `snes` set is a game

Question: Whether every rom in the `snes` set is a game

Measured: **No.** `Dark Law - Meaning of Death (Japan) [T-En by AGTP v1.00].zip` holds a single `.bps`, 177,786 B, a translation patch for a ROM the library does not carry. It syncs as any other member, and what the library holds is the user's to keep.

## RB-321. Why jgenesis closed on its first launch from ES

Question: Why jgenesis closed on its first launch from ES

Measured: **Not found.** Launched from ES under `jgenesis`, it exited with code 1 after 3.5 s; the launch was the first of the pass whose pad reported as an Xbox 360 Controller. It did not recur: not from `emulatorLauncher` with and without ES's `-p1` arguments, not from its own command line, not on the exact seed it was given in a scratch folder with a copy of its config, and not on the maintainer's next launch from ES. Exit 1 is an error jgenesis returned rather than a panic, and `emulatorLauncher` discards its stderr, so its reason was not seen.
