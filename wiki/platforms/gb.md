# Game Boy

|                 |                                                                     |
| --------------- | ------------------------------------------------------------------- |
| RetroBat system | `gb`                                                                |
| Games go in     | `roms\gb`                                                           |
| BIOS            | Six boot ROMs, which a sync fetches when your RomM library has them |
| Tested rows     | [Platforms](index.md#gb) lists each emulator and whether it passed  |

## Which emulator to use

Leave RetroBat on its default, RetroArch with the Gambatte core. Every emulator RetroBat offers
for the Game Boy passed RomMBat's tests, so any of them is safe to pick, with the BIOS below.

## BIOS

A sync fetches six files from your RomM library: the Game Boy boot ROM `gb_bios.bin`, the Game Boy
Color boot ROM `gbc_bios.bin`, and four Super Game Boy files. RetroBat itself lists only the first
for the Game Boy. Two emulators refuse to start without some of them:

- RetroArch's bsnes core plays a Game Boy game as a Super Game Boy, so it will not start one
  without `SGB1.sfc`.
- BizHawk's GBHawk core needs the boot ROM for the console it plays a game as: `gb_bios.bin` for
  a Game Boy-only game, and `gbc_bios.bin` for one made for both the Game Boy and the Game Boy
  Color, which it plays in color.

Every other emulator starts without any of them. If your library lacks one, see
[BIOS and firmware](../using/bios.md).

## Saves

A save follows you to another emulator only where the two read the same file
([Changing emulator](../saves/index.md#changing-emulator)). On the Game Boy:

- All six of RetroArch's cores and the standalone Mesen share one save.
- The standalone mGBA and Mednafen share another.
- BizHawk's three cores share a third.

The rest each keep their own.

## Known issues

- **A game with a clock can lose its time when you change emulator**, since each emulator keeps
  the clock its own way. On the default emulator, the clock syncs with the save.
- Under jgenesis, the save of a Game Boy Color game kept in the `gb` folder, such as Pokemon Silver,
  does not sync. Play those from the Game Boy Color system instead.
- RetroArch's TGB Dual, DoubleCherryGB and SameBoy cores rewrite a small clock file every time you
  play, so each session sends a new copy of it to RomM. That is expected.
