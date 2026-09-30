# Game Boy Color

|                 |                                                                     |
| --------------- | ------------------------------------------------------------------- |
| RetroBat system | `gbc`                                                               |
| Games go in     | `roms\gbc`                                                          |
| BIOS            | `gbc_bios.bin`, which a sync fetches when your RomM library has it  |
| Tested rows     | [Platforms](index.md#gbc) lists each emulator and whether it passed |

## Which emulator to use

Leave RetroBat on its default, RetroArch with the Gambatte core. Every emulator RetroBat offers
for the Game Boy Color passed RomMBat's tests, so any of them is safe to pick.

## BIOS

A sync fetches the Game Boy Color boot ROM, `gbc_bios.bin`, from your RomM library. Only BizHawk's
GBHawk core refuses to start without it. If your library lacks it, see
[BIOS and firmware](../using/bios.md).

## Saves

A save follows you to another emulator only where the two read the same file
([Changing emulator](../saves/index.md#changing-emulator)). On the Game Boy Color:

- All four of RetroArch's cores and the standalone Mesen share one save.
- The standalone mGBA and Mednafen share another.
- BizHawk's three cores share a third.

The rest each keep their own.

## Known issues

- **A game with a clock, such as Pokemon Crystal, can lose its time when you change emulator**, even
  between emulators that share the save, since each keeps the clock its own way. Some then ask you
  to set the clock again, and some show the wrong time. Stay on one emulator and the clock syncs
  with the save.
- For a game with a clock, the standalone mGBA, Mednafen, Mesen, ares and jgenesis rewrite it every
  time you play, even if you did not save, so each session sends a small new copy to RomM. That is
  expected.
