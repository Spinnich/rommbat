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

- **jgenesis** has not been checked since RetroBat 8.3.0 began starting Game Gear games on it as
  Game Gear games. Before 8.3.0 it ran them as Master System games, to a black screen.
- **RetroArch's FBNeo core** does not start games named the way RomM libraries usually name them,
  because FBNeo expects its own file names.

A few Game Gear cartridges are really Master System games, held as a `.sms` inside the zip. RetroArch's
PicoDrive core plays those in the wrong colors; the other emulators that start
them play them correctly.

## BIOS

No Game Gear emulator needs a BIOS, and RetroBat lists none.

## Saves

A save follows you to another emulator only where the two read the same file
([Changing emulator](../saves/index.md#changing-emulator)). On the Game Gear, RetroArch's
Genesis Plus GX and PicoDrive cores share one save. The rest each keep their own.
