# Master System

|                 |                                                                              |
| --------------- | ---------------------------------------------------------------------------- |
| RetroBat system | `mastersystem`                                                               |
| Games go in     | `roms\mastersystem`                                                          |
| BIOS            | Only for BizHawk's SMSHawk, and you supply it yourself                       |
| Tested rows     | [Platforms](index.md#mastersystem) lists each emulator and whether it passed |

## Which emulator to use

Leave RetroBat on its default, RetroArch with the Genesis Plus GX core. Avoid two of the
emulators RetroBat offers:

- **Kega Fusion keeps its in-game saves inside its own program folder**, where RomMBat does not
  look, so they never reach RomM. Its save states do sync.
- RetroArch's FBNeo core does not start games named the way RomM libraries usually name them,
  because FBNeo expects its own file names.

## BIOS

Only BizHawk's SMSHawk needs a BIOS, and it refuses to start any game without one. Every other
emulator starts without.

RomMBat cannot fetch the Master System BIOS, because RetroBat lists it without the checksum
RomMBat finds files by ([Files RomMBat cannot fetch](../using/bios.md#files-rommbat-cannot-fetch)).
If you use SMSHawk, copy these into RetroBat's `bios` folder yourself:

- `[BIOS] Sega Master System (USA, Europe) (v1.3).sms`, for a USA or European game
- `[BIOS] Sega Master System (Japan) (v2.1).sms`, for a Japanese one

## Saves

A save follows you to another emulator only where the two read the same file
([Changing emulator](../saves/index.md#changing-emulator)). On the Master System, RetroArch's
Genesis Plus GX and PicoDrive cores share one save. The rest each keep their own.
