---
summary: How each emulator RetroBat offers for `gba` behaves, measured, with RB- IDs.
read-when: Before certifying a `gba` row or changing how RomMBat handles a `gba` save, state or firmware.
---

# gba: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-285. Three of ten `gba` rows need `gba_bios.bin`

`ares` ("Game Boy Advance - BIOS (World) is required to play this game"), `jgenesis` (exit 1, "No Game Boy Advance BIOS provided"; its `--help` calls the path "required for GBA emulation") and `mesen` ("This game requires a firmware file") refuse without it. The three `libretro` cores, `mgba`, `mednafen`, `bizhawk` and `nosgba` boot to the intro. `batocera-systems.json` carries no optional flag, so RomMBat fetches it whenever RomM has it, and by the maintainer's ruling that stands. All three refusals boot once `bios gba --apply` restores it, so `emulatorLauncher` passes the file from `bios/`; Mesen copies it to `emulators/mesen/Firmware/` on first use. A gap report is right for three rows and overstated for seven, and cannot say which without reading `<system>.emulator`

## RB-286. `nosgba` boots a zipped `gba` library only through the bare `.gba` beside the zip, which the emulator then deletes

`emulatorLauncher` runs `no$gba.exe /f "<rom>.zip"` and NO$GBA answers "Cartridge not found"; with `<rom>.gba` beside the zip the same launch boots Emerald, without the BIOS though its INI sets "Emulate BIOS Functions" to "By real GBA.ROM". That `.gba` was present before each of two launches and gone after. **The emulator deletes it, not `emulatorLauncher`**: it unzips by running an external `PKUNZIP.EXE` ("LOADING PKUNZIP..." in its loader) that its folder does not hold, takes the `.gba` for PKUNZIP's output, and removes it as a temp file, mid-session when run by hand with no `emulatorLauncher`. Handed a bare `.gba` path, it keeps the file. `NosGbaGenerator` never extracts the zip and has no cleanup of its own ([#1377](https://github.com/RetroBat-Official/emulatorlauncher/issues/1377)). So a sync, which places the zip RomM serves, cannot make the row work. Its saves land in `emulators/nosgba/BATTERY/<rom>.SAV`, outside `saves/` as Kega Fusion's do (**283\*\*): it read a raw 131,072 B seed and wrote it back compressed, 3,583 B headed `NocashGbaBackup`. No state can be synced: `F8` is Write Snapshot, a Save As dialog opening in the user's `Documents`, so a state lands wherever the user picks. Its pad's Start and Select are mapped differently from every other row, `NO$GBA.INI`numbering them 3 and 4 with no mapping from`emulatorLauncher`

## RB-287. A `gba` boot writes a battery save on every row that got past boot and closed

Each wrote 131,072 B of `0xFF` for Emerald, md5 `41d2e2c0...`, an unwritten 128 KB flash chip; BizHawk's is 131,088 B. That is RB-404 on a second system: no property of the file separates it from a save, only a baseline does. All were moved out of the tree before a flush

## RB-288. Three `gba` emulators meet on the loose `.sav` before a save is made, and two keep Emerald's clock in a second file

`libretro`/`mgba` and `gpsp` share the loose `<rom>.srm`. `libretro`/`mednafen_gba` writes a loose `<rom>.zip#<inner rom>.<md5>.sav`, the zip and the file inside it joined by `#`, which the `libretro` rule (`.srm` and three others) does not match. `mgba` standalone writes the loose `<rom>.sav`; `mednafen` rewrote that same file because the plain name existed (**273**), and also wrote `mednafen/backup/<rom>.<md5>/`; `mesen` writes the loose `<rom>.sav` plus `<rom>.rtc`. `jgenesis` writes `jgenesis/gba/<rom>.sav` and `.rtc`. `bizhawk` writes `bizhawk/<rom>.SaveRAM` named after the ROM file, not a title of its own as on `nes` and `megadrive`. So three emulators meet on the loose `.sav`, and two keep Emerald's clock in a second file, which is class B

## RB-289. One Emerald save moves between the `gba` emulators, except into mednafen

mGBA standalone, BizHawk, Mesen, jgenesis, ares, mednafen_gba and NO$GBA each read the 131,072 B libretro save and saved over it. mGBA and BizHawk write 131,088 B, the flash and a 16-byte clock footer; Mesen, jgenesis and ares keep the flash at 131,072 B and the clock in an `.rtc` of 19, 59 and 18 B. **mednafen refuses mGBA's file**: `Save game memory file ... is an incorrect size(131088 bytes). The correct size is 65536 or 131072 bytes.`, and the game does not load. Since mednafen opens the plain `<rom>.sav` whenever it exists (**273**), a device where mGBA standalone has run cannot play the game under mednafen until that file moves. Given a 131,072 B save with no clock, mednafen ran it and Emerald reported that its internal battery had run dry; Mesen, given the same, did not. **Mesen reads mGBA's 131,088 B file**, continuing with no clock message, and on exit writes it back at 131,072 B, the flash byte for byte with the footer dropped, even with nothing saved

## RB-290. `libretro`/`mednafen_gba` keeps its own battery save, not the `.srm` the other `gba` cores share

RetroArch logged `Redirecting save file to ...srm` and then `Skipping SRAM load`, and the core wrote `<rom>.zip#<rom>.605b89b67018abcea91e693a4dd25be3.sav`: mednafen's `%f.%M` over RetroArch's `archive#member` path, the md5 being of the whole `.gba`. It carries `libretro:battery:sav`, class B's per-extension slot, so it is kept apart from `libretro:battery`, and a restore names it from the zip's one member through `MednafenRomHash.ArchiveMemberOf`. **A `.7z` gets the same form**, `<rom>.7z#<rom>.<md5>.sav`, which the rule claims but a restore cannot name, since the member cannot be read from a `.7z` here (#221). **A bare `.gba` gets `<rom>.<md5>.sav`**, mednafen standalone's name, hashed even with a plain `<rom>.sav` present, so there the two share `mednafen:battery`

## RB-291. A `gba` clock file changes on every launch, whether or not anything is saved

Mesen's `.rtc` (`05bee840` to `da65e7fe`), jgenesis's `.rtc` (`3e5823a1` to `2a2ed802`) and BizHawk's `.SaveRAM` (`6bb6d384` to `2e7801cb`) each changed on a launch in which the agent sent only state keys, the flash beside them unchanged. Each records the host time. So a session uploads a new version of that slot whether or not the game was saved

## RB-292. mednafen writes compressed backups of a save it loads under `saves/gba/`

The backups are `mednafen/backup/<rom>.<md5>/0.sav` to `2.sav`, 2.8 to 2.9 KB each, rotated beside a one-byte `C.sav` counter, per `filesys.fname_savbackup %f.%m%z%p.%x`. They are not saves. `saves` counts the four files under `gba` as `not in this release`
