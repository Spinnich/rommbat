# Game Boy Advance

|                 |                                                                     |
| --------------- | ------------------------------------------------------------------- |
| RetroBat system | `gba`                                                               |
| Games go in     | `roms\gba`                                                          |
| BIOS            | `gba_bios.bin`, which a sync fetches when your RomM library has it  |
| Tested rows     | [Platforms](index.md#gba) lists each emulator and whether it passed |

## Which emulator to use

Leave RetroBat on its default, RetroArch with the mGBA core. Avoid NO&#36;GBA, the one emulator
RetroBat offers that did not pass: it cannot open a zipped game, and it keeps its saves inside its
own program folder, where RomMBat does not look.

## BIOS

A sync fetches `gba_bios.bin` from your RomM library. Most emulators start without it, but ares,
jgenesis and the standalone Mesen refuse to start any game until it is in place. If your library
does not have it, see [BIOS and firmware](../using/bios.md).

## Saves

A save follows you to another emulator only where the two read the same file
([Changing emulator](../saves/index.md#changing-emulator)). On the Game Boy Advance:

- RetroArch's mGBA and gpSP cores share one save.
- The standalone mGBA and Mesen share another.

The rest each keep their own.

## Known issues

- **The standalone Mednafen will not load a game after the standalone mGBA has saved it.** Mednafen
  opens the same save file, refuses mGBA's version of it for its size, and the game does not start.
  Move that save out of `saves\gba` to play the game under Mednafen. A sync does not bring
  another computer's mGBA save down for a game that runs under Mednafen here, though restoring one
  by hand does.
- **Quit the standalone mGBA a few seconds after you save.** Its quit button closes it at once,
  and can beat the save to the disk.
- For a game with a clock, such as the Pokemon games, Mesen, jgenesis and BizHawk rewrite the save
  every time you play, even if you did not save, so each session sends a small new copy to RomM.
  That is expected.
