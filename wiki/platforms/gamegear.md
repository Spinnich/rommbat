# Game Gear

|                 |                                                                          |
| --------------- | ------------------------------------------------------------------------ |
| RetroBat system | `gamegear`                                                               |
| Games go in     | `roms\gamegear`                                                          |
| BIOS            | None                                                                     |
| Tested rows     | [Platforms](index.md#gamegear) lists each emulator and whether it passed |

## Which emulator to use

Leave RetroBat on its default, RetroArch with the Genesis Plus GX core.

Avoid two emulators:

- **jgenesis** starts every Game Gear game as if it were a Master System game, so the game runs
  but the screen stays black. Nothing in RetroBat's options changes that.
- **RetroArch's FBNeo core** does not start games named the way RomM libraries usually name them,
  because FBNeo expects its own file names.

A few Game Gear cartridges are really Master System games, held as a `.sms` inside the zip. RetroArch's
PicoDrive core plays those in the wrong colours; the other emulators that start
them play them correctly.

## BIOS

No Game Gear emulator needs a BIOS, and RetroBat lists none.

## Saves

A save follows you to another emulator only where the two read the same file
([Changing emulator](../saves/index.md#changing-emulator)). On the Game Gear, RetroArch's
Genesis Plus GX and PicoDrive cores share one save. The rest each keep their own.
