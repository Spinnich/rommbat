# Mega Drive and Genesis

|                 |                                                                           |
| --------------- | ------------------------------------------------------------------------- |
| RetroBat system | `megadrive`                                                               |
| Games go in     | `roms\megadrive`                                                          |
| BIOS            | None                                                                      |
| Tested rows     | [Platforms](index.md#megadrive) lists each emulator and whether it passed |

## Which emulator to use

Leave RetroBat on its default, RetroArch with the Genesis Plus GX core. Avoid two of the
emulators RetroBat offers:

- **Kega Fusion keeps its in-game saves inside its own program folder**, where RomMBat does not
  look, so they never reach RomM. Its save states do sync. Its controls also need setting up in
  Kega's own menu before your pad works.
- RetroArch's FBNeo core does not start games named the way RomM libraries usually name them,
  such as `Sonic The Hedgehog (USA, Europe).zip`, because FBNeo expects its own file names.

## BIOS

The Mega Drive needs none, and RetroBat lists none.

## Saves

A save follows you to another emulator only where the two read the same file
([Changing emulator](../saves/index.md#changing-emulator)). On the Mega Drive, RetroArch's Genesis
Plus GX, Genesis Plus GX Wide and PicoDrive cores share one save. The rest each keep their own.

PicoDrive keeps the save at a different size from Genesis Plus GX, so each switch between the two
sends a new copy to RomM. Nothing is lost.
