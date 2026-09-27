---
summary: How each emulator RetroBat offers for `gbc` behaves, measured, with RB- IDs.
read-when: Before certifying a `gbc` row or changing how RomMBat handles a `gbc` save, state or firmware.
---

# gbc: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-299. One of twelve, and RetroBat's `gbc` list names it

Question: Which `gbc` rows need firmware, and whether RetroBat's list names it

Measured: **One of twelve, and RetroBat's `gbc` list names it.** With `gb_bios.bin`, `gbc_bios.bin` and the four Super Game Boy files all out of `bios/`, eleven rows boot Crystal to the intro. `bizhawk`/`GBHawk` refuses with "Couldn't find required firmware GBC+World. This prevents the core from loading" and boots once `gbc_bios.bin` is back, which `bios gbc --apply` fetches. Unlike `gb` (293), `gbc` needs no supplement.

## RB-300. Every row keeps it, each its own way, and every change of row that was checked lost it

Question: Where each `gbc` row keeps a clock cartridge's clock, and whether it survives a change of row

Measured: **Every row keeps it, each its own way, and every change of row that was checked lost it.** The four `libretro` cores and Mesen share the loose `<rom>.rtc`, written as RAM type #1: 8 B under `gambatte`, the Unix time at which the game clock reads zero; 4 B under `tgbdual` and `DoubleCherryGB`, the host time at exit; 32 B under `sameboy`; 13 B under Mesen. mGBA and mednafen append a 48 B footer to the `.sav`, BizHawk's Gambatte 22 B and SameBoy 48 B to the `.SaveRAM`, and GBHawk keeps none. ares writes `ares/Game Boy/<rom>.rtc`, 13 B, and jgenesis `jgenesis/gbc/<rom>.rtc`, 38 B. Each row's own clock round-trips through RomMBat at its own md5. Seeded from the row before, `tgbdual` showed about 10 PM for a clock set to about 9 AM, `sameboy` about 4:22 AM, GBHawk a wrong time, all with no prompt, and ares, BizHawk's Gambatte and SameBoy, and jgenesis, each seeded with the RAM alone and so given no clock, asked for one. Mesen, and mednafen on mGBA's `.sav`, which it read and saved into with the same 48 B footer, were not checked. Mesen's, ares's, jgenesis's and mGBA's clocks change on a launch with nothing saved. The slot stays one, `libretro:battery:rtc`, because on disk it is one file.

## RB-301. Where each standalone `gbc` row puts its battery save and its states

Question: Where each standalone `gbc` row puts its battery save and its states

Measured: Mesen writes the loose `.srm` and `.rtc` the `libretro` cores share. mGBA writes a loose `<rom>.sav`, 32,816 B, and mednafen read and saved into it rather than its hashed `<rom>.301899b8087289a6436b0a241fbbb474.sav`, which it writes only alone. **ares splits one row across two trees**: its battery save and clock go to `ares/Game Boy/`, `gb`'s name, and its states `.bs1` and `.bs2` to `ares/Game Boy Color/`. It writes on exit only: killed after 15 s of `WM_CLOSE` it wrote nothing, ended with `Esc`, its `QuitEmulator` key, it wrote both, and once it closed on `WM_CLOSE` alone and wrote both, so 297's "ignores `WM_CLOSE`" is sometimes only slow. BizHawk's three cores share `bizhawk/Pokemon - Crystal Version (USA, Europe) (Rev A).SaveRAM`, named after its own title where the ROM file says `(Rev 1)`, while its states are named after the ROM file. jgenesis writes `jgenesis/gbc/<rom>.sav` and `.rtc`, and states in `jgenesis/states/`. Mesen, mGBA, mednafen and ares state as on `gb` (297), under `mesen/SaveStates`, `mgba/sstates`, `mednafen/sstates` and ares's Color tree.

## RB-302. Beside the `.ram`, as on `gbc`

Question: Where ares keeps a clock cartridge's clock on `gb`

Measured: **Beside the `.ram`, as on `gbc`.** A copy of Pokemon - Silver Version (USA, Europe) (SGB Enhanced) (GB Compatible) placed in `roms/gb` for one launch under `ares`/`GameBoy` wrote `ares/Game Boy/<rom>.ram`, 32,768 B, and `<rom>.rtc`, 13 B, on exit. `gb`'s rule read only the `.ram`; the `.rtc` takes its own class B rule, `ares:battery:rtc`, so the `.ram` keeps `ares:battery` and nothing uploaded moves. jgenesis was given no such rule on `gb`: the clock cartridges known here are Color titles, a `.gbc`, which jgenesis files under `jgenesis/gbc/` (298), and a mono-only one may not exist.
