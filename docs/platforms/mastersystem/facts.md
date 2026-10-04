---
summary: How each emulator RetroBat offers for `mastersystem` behaves, measured, with RB- IDs.
read-when: Before certifying a `mastersystem` row or changing how RomMBat handles a `mastersystem` save, state or firmware.
---

# mastersystem: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-322. One of ten `mastersystem` rows needs firmware, and RomMBat cannot fetch it

`bizhawk`/`SMSHawk` asks "No BIOS found. Open the firmware manager now?" and on Cancel fails to load the ROM, although RetroBat leaves its `UseBios` sync setting `False`. `emulatorLauncher` names both files RetroBat lists in BizHawk's config, `SMS+Export` as `[BIOS] Sega Master System (USA, Europe) (v1.3).sms` and `SMS+Japan` as the Japanese v2.1, and the export test game starts with the US/EU file alone (md5 `840481177270d5642a14ca71ee72844c`, 8,192 B) and refuses with the Japanese alone. RetroBat's list names both without a hash, so `bios mastersystem` reports them as unverifiable and fetches nothing. The other nine rows reach the intro with no firmware. Kega Fusion's `Fusion.ini` names `bios_U.sms`, `bios_J.sms` and `bios_E.sms` instead, names RetroBat's list does not carry, and boots without them

## RB-323. Each standalone `mastersystem` row keeps its battery save and states in its own place, Mesen's a `.sav` and Kega Fusion's outside `saves/`

Mesen writes a loose `<rom>.sav`, 8,192 B, **not the `.srm` it shared with `libretro` on `snes`**, and rewrites it only when the game changes the SRAM; states go to `mesen/SaveStates/<rom>_<n>.mss`. mednafen writes `<rom>.<md5>.sav`, 32,768 B, the md5 of the whole `.sms` inside the zip, with states at `mednafen/sstates/<rom>.<md5>.mc<n>`. ares keeps `ares/Master System/<rom>.ram`, 32,768 B, written on exit, beside `.bs1` and `.bs2`, and here `F7` stepped the slot. BizHawk writes `bizhawk/Golden Axe Warrior (UE).SaveRAM`, 8,192 B, named after its own title as the sidecar `Golden Axe Warrior (UE).SMSHawk` says, and took ES's `-state_slot`. jgenesis writes `jgenesis/sms/<rom>.sav`, 32,768 B, and mirrors states from `emulators/jgenesis/states/sms/`. Kega Fusion writes `emulators/kega-fusion/<rom>.ssm`, 8,191 B, outside `saves/` as on `megadrive` (**283**), and states to `saves/mastersystem/kega-fusion/<rom>.ss<slot>`, `.ss` rather than `.gs`, under `SMSStateFiles`. Genesis Plus GX trims the `.srm` to 8,191 B and PicoDrive keeps 32,768 B, the same bytes zero-padded, as on `megadrive` (**277**)

## RB-324. mednafen opens no Master System save of Mesen's, and will not start the game while one is there

With Mesen's 8,192 B `<rom>.sav` beside the ROM, mednafen opens the plain name, as it does on `gb`, `gbc` and `snes` (**273**, **301**), and stops with "Error reading from opened file ... Unexpected EOF", since it keeps a 32 KB window. The game never starts. With the plain file moved away it used its hashed name and ran. Same class as **289**, where it refused mGBA's `gba` save for its size

## RB-325. `libretro`/`fbneo` does not boot a `mastersystem` library named by No-Intro, as on `megadrive` (278)

RetroBat launches it with `--subsystem sms`, and FBNeo shows "Romset is unknown" at 640x480 without searching. RetroBat's own `bios/fba/FB Alpha (ClrMame Pro XML, Master System only).dat` names the test game `gaxewarr`, CRC `c7ded988`; the same bytes as `gaxewarr.zip` logged `Romset found` and ran at 256x192

## RB-326. Golden Axe Warrior keeps a committed save written once and a working copy it rewrites

Offset `0x0000` holds `Golden Axe Warrior Ver 1.0` from the first boot; `0x1000`-`0x11FF` holds value and complement byte pairs, written at the first character creation; `0x1200`-`0x13FF` is a working area the game rebuilds from them. After the first character no session changed `0x1000`-`0x11FF`, including a new game with a new name under ares, and mednafen and jgenesis wrote the working area back to the first save's bytes. So on this game a later row's save round-trips its own write, not new progress. Where the game commits again was not found
