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

- **jgenesis** starts Game Gear games correctly since RetroBat 8.3.0, but RomMBat's saves have not
  been checked on it yet.
- **RetroArch's FBNeo core** does not start games named the way RomM libraries usually name them,
  because FBNeo expects its own file names.

A few Game Gear cartridges are really Master System games, held as a `.sms` inside the zip. RetroArch's
PicoDrive core plays those in the wrong colors and jgenesis shows a black screen; the other
emulators that start them play them correctly.

## BIOS

No Game Gear emulator needs a BIOS, and RetroBat lists none. Some cores can show the console's
boot screen from `bios.gg`, which a sync fetches when your library has it.

## Saves

A save follows you to another emulator only where the two read the same file
([Changing emulator](../saves/index.md#changing-emulator)). On the Game Gear, RetroArch's
Genesis Plus GX and PicoDrive cores share one save. The rest each keep their own.
