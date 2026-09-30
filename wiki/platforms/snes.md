# Super Nintendo

|                 |                                                                      |
| --------------- | -------------------------------------------------------------------- |
| RetroBat system | `snes`                                                               |
| Games go in     | `roms\snes`                                                          |
| BIOS            | None, apart from a chip's firmware for a few games on some emulators |
| Tested rows     | [Platforms](index.md#snes) lists each emulator and whether it passed |

## Which emulator to use

Leave RetroBat on its default, RetroArch with the Snes9x core. Every emulator RetroBat offers for
the Super Nintendo passed RomMBat's tests. The default is also the easy choice for a game with an
extra chip, since it needs no firmware for one (below).

## BIOS

RetroBat lists no BIOS for the Super Nintendo, and RomMBat fetches none. A few games carry an
extra chip, such as Super Mario Kart's DSP-1, and three emulators refuse to start those without
the chip's firmware:

- RetroArch's Mesen-S core, which reads `dsp1b.rom` from RetroBat's `bios` folder.
- The standalone Mesen, which reads `dsp1b.rom` from `emulators\mesen\Firmware`, not from `bios`.
- jgenesis, which needs the file's path set in its own configuration by hand.

RomMBat cannot fetch these files, so you supply them yourself. On the default emulator you need
none of them.

## Saves

A save follows you to another emulator only where the two read the same file
([Changing emulator](../saves/index.md#changing-emulator)). On the Super Nintendo:

- All seven of RetroArch's cores, the standalone Mesen, Snes9x and Mednafen share one save.
- BizHawk's three cores share another.

The rest each keep their own.
