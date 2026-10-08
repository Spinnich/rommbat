---
summary: How each emulator RetroBat offers for `gb` behaves, measured, with RB- IDs.
read-when: Before certifying a `gb` row or changing how RomMBat handles a `gb` save, state or firmware.
---

# gb: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-293. Two of fourteen `gb` rows need firmware, and RetroBat's list names neither file for `gb`

`libretro`/`bsnes` runs a `.gb` as a Super Game Boy cartridge and logs `Failed to load content` without `bios/SGB1.sfc`; with it Yellow boots in its SGB border. `bizhawk`/`GBHawk` refuses with "Couldn't find required firmware GBC+World" on Yellow, whose header flags Color support, and with "GB+World" on the mono-only Tetris (World) (Rev 1) when `gb_bios.bin` is also gone; with `gbc_bios.bin` Yellow boots in Color, and with `gb_bios.bin` Tetris boots. BizHawk has a `ConsoleMode` sync setting for GBHawk, and RetroBat's `es_features.cfg` offers no option for it. `libretro`/`sameboy` logs `Loading boot image: bios\dmg_boot.bin`, a file no list names, and falls back to its own. The other eleven boot with no boot ROM at all. RetroBat's manifest lists only `gb_bios.bin` for `gb`, the four SGB files under `sgb` and `gbc_bios.bin` under `gbc`, while its Game Boy wiki page lists `gb_bios.bin` and the four SGB files. RomMBat's `bios.json` builder now supplements `gb` with all five, copied from `sgb` and `gbc` at RetroBat's md5s

## RB-294. A `gb` boot write is not always a recognizable fill, so only a baseline tells it from a save

Nine rows flushed 32,768 B of `0xFF` for Yellow, md5 `3df7b333...`, which the scanner passes over as erased (RB-408). `libretro`/`mesen-s` and `mesen` standalone flushed random bytes, all 256 values present, since Mesen randomizes uninitialized RAM. `libretro`/`tgbdual` and `DoubleCherryGB` flushed mostly `0x00` with Yellow's sprite scratch data in it, because Pokemon's first generation decompresses sprites through cartridge RAM. A fill test would pass all four as saves, so on these only a baseline separates a boot write from a save, as RB-287 says

## RB-295. The `libretro` `.rtc` on `gb` holds a real clock on a clock cartridge, and otherwise, on three cores, only the host time

`tgbdual`, `DoubleCherryGB` and `sameboy` write `<rom>.rtc` as RetroArch's RAM type 1 beside the `.srm` for every game. On Pokemon Yellow, which has no clock, `tgbdual` and `DoubleCherryGB` wrote 4 B, a little-endian Unix time equal to the session's end, and `sameboy` 32 B, zeroed clock registers with the same kind of timestamp at offset 16; `sameboy` read `DoubleCherryGB`'s 4 B file without complaint. `gambatte`, the stock core, writes none for Yellow and 8 B for Pokemon Silver, an MBC3 cartridge with a clock (**298**). No cartridge in the 107-game set has a clock, and RomM files the clock-cart Pokemon titles under `gbc`, but one synced into `gb` keeps its clock in this file. So it is a save on `gb`, `libretro:battery:rtc`, class B beside the `.srm`, and on a cartridge with no clock three cores upload a few bytes of new version per launch

## RB-296. `gb` battery saves live in five places, and each row read the one the row before it left

Six `libretro` cores and `mesen` standalone share the loose `<rom>.srm`, which uploads as `libretro:battery`. **`libretro`/`bsnes` keeps it itself**: RetroArch logs `Content loading skipped` and `Skipping SRAM load` and no SRAM write, yet the core read the seed and wrote the `.srm` back. `mgba` standalone writes a loose `<rom>.sav`, 32,768 B with no footer, and `mednafen` read and saved into that same file, taking its hashed `<rom>.<md5 of the .gb>.sav` only when no plain one is there (**273**). `ares` keeps `ares/Game Boy/<rom>.ram`, `jgenesis` `jgenesis/gb/<rom>.sav` with no clock file. BizHawk's three cores share one `bizhawk/Pokemon - Yellow Version (USA, Europe).SaveRAM`, named after BizHawk's own title, which on `gba` had matched the ROM file. Every row read the file the row before it left and saved over it

## RB-297. Standalone `gb` rows state under their own trees, and `bizhawk` and `mgba` follow ES's `-state_slot`

`mesen`, `mednafen` and `ares` write where they do on `gba`, under their own tree in `saves/gb/`, with ares naming the directory `Game Boy`; `mgba` writes `mgba/sstates/<rom>.ss<n>`, the `savestatePath` RetroBat's `config.ini` sets. The pad's save key followed ES's `-state_slot` on `bizhawk`, which moved from 3 to 4 to 5 over three launches, and on `mgba`, which wrote `.ss3` beside `.ss1`. `Ctrl+F2` sent to EmuHawk by `keybd_event` was missed with keys held 120 ms and landed at 400 ms. ares ignores `WM_CLOSE` and was killed after its states were on disk, but closes cleanly on the pad's hotkey combination

## RB-298. A `gb` clock cartridge's clock is a loose `.rtc` on seven rows, inside the save on five, and nowhere on two

Pokemon - Silver Version (USA, Europe) (SGB Enhanced) (GB Compatible), header type `0x10` (MBC3 with timer, RAM and battery), synced into `gb` by a filter set with `--folder gb` and booted under all fourteen rows. **In a loose `<rom>.rtc`**: `libretro`/`gambatte` 8 B, `sameboy` 32 B, `tgbdual` and `DoubleCherryGB` 4 B, and `mesen` standalone 13 B, where `gambatte` and `mesen` write none for a cartridge without a clock. **Inside the save, past the 32,768 B of RAM**: `libretro`/`bsnes`, `mgba`, `mednafen` and `bizhawk`/`SameBoy` at 32,816 B, `bizhawk`/`Gambatte` at 32,790 B. **Nowhere**: `libretro`/`mesen-s` and `bizhawk`/`GBHawk` write 32,768 B and no clock file. `ares` writes `ares/Game Boy/<rom>.rtc`, 13 B, beside its `.ram`. **`jgenesis` keeps a `.gbc` ROM's saves in `jgenesis/gbc/`**, a `.sav` and a 38 B `.rtc`, choosing the directory by the file inside the zip where Yellow's `.gb` went to `jgenesis/gb/`; the `gb` rule reads only the latter, so there that save is reported and not synced. `gambatte`'s 8 B is the Unix time at which the game clock reads zero, so the clock is the host time less it, and it stays unchanged across a launch with nothing saved. Driven on the stock row: the maintainer set the clock and saved; the `.srm` went up as `libretro:battery` and the `.rtc` as `libretro:battery:rtc`; both, with a state and its screenshot, were moved out and restored at their own md5; and the relaunched game showed the clock about five minutes on, the time since the save. That launch saved nothing and still changed the `.srm` in 335 bytes, 334 of them in the first 8 KB bank, which Pokemon uses as scratch

## RB-428. A `gb` MBC2 save is 512 half-bytes, which the fourteen rows store in three forms and each reads from the one before

Verified: RetroBat 8.2.1, 2026-10-08. How: Final Fantasy Adventure (USA), header `0x06` (MBC2 with battery), saved in game under all fourteen rows, each continuing from the file the row before it left.

**One nibble a byte, high nibble set**: `libretro`/`mesen-s`, `bsnes` and `sameboy`, `mesen` standalone, `mednafen`, `jgenesis` and `bizhawk`/`GBHawk` and `SameBoy`, at 512 B. `libretro`/`gambatte` and `bizhawk`/`Gambatte` pad the same 512 B to 8,192 B with `0xFF`, and `tgbdual` and `DoubleCherryGB` to 8,192 B with `0x00`. **Two nibbles a byte, the even address in the low nibble**: `mgba` and `ares`, at 256 B, byte for byte the same. The six `libretro` cores and `mesen` share the `.srm` and each rewrote it at its own size; BizHawk's three cores share one `.SaveRAM` the same way. Every boot write was `0xFF` throughout at the row's size, which the scanner passes over (RB-408). So an MBC2 save lands in a slot the `gb` rules already read, and a size change between cores is a new version of that slot, not a new one

## RB-429. `mednafen` refuses mGBA's 256 B MBC2 `<rom>.sav` on `gb`, and the game does not start

Verified: RetroBat 8.2.1, 2026-10-08. How: Final Fantasy Adventure (USA) under `mednafen`/`gb` with mGBA's packed 256 B save at the plain name.

`mednafen` opens the plain `<rom>.sav` before its hashed name (RB-273) and stops with "Error reading from opened file … Unexpected EOF", leaving the file unchanged. On Pokemon Yellow it read and saved into mGBA's file (RB-296), so on `gb` the refusal follows the cartridge's RAM type, not the system. `refuses_plain` on mednafen's rule is keyed on the system and stays off for `gb`, so an `mgba:battery` download of an MBC2 save to a device that runs the game under `mednafen` leaves that game unable to start until the file is moved. Whether mGBA reads `mednafen`'s 512 B form is not settled

## RB-430. Pokemon Silver's RAM moves between `gb` rows and its clock does not

Verified: RetroBat 8.2.1, 2026-10-08. How: the stock row's save, `.srm` 32,768 B with `gambatte`'s 8 B `.rtc`, left in place for the rows sharing them and copied as RAM alone into each other row's own file; each row continued and saved in game.

Every row but `libretro`/`mesen-s` read the RAM and continued. **No row read `gambatte`'s clock**: `tgbdual`, `DoubleCherryGB`, `sameboy` and `mesen` showed an unrelated time from it, and `bsnes` passed it over and asked for the time. `bizhawk`/`GBHawk` showed an unrelated time from BizHawk Gambatte's 32,790 B file and saved 32,768 B with no clock. The rows given RAM alone showed a time of their own (`mgba`, `mednafen`) or asked for one (`ares`, `jgenesis`, `bizhawk`/`Gambatte` and `SameBoy`). Each then wrote its own clock form (RB-298), the four `libretro` cores and `mesen` rewriting the shared `.rtc` at 4, 4, 32 and 13 B. `mesen-s` offered no Continue on that `.srm`, with the `.rtc` beside it or not, and a new game saved 32,768 B and no clock. RomMBat moves each file in its slot unchanged, so on a device that switches rows Silver asks for the time again or keeps a wrong one
