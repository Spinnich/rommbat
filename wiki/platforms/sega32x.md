# 32X

|                 |                                                                             |
| --------------- | --------------------------------------------------------------------------- |
| RetroBat system | `sega32x`                                                                   |
| Games go in     | `roms\sega32x`                                                              |
| BIOS            | None needed. RetroBat lists three 32X files, and no emulator asked for them |
| Tested rows     | [Platforms](index.md#sega32x) lists each emulator and whether it passed     |

## Which emulator to use

Leave RetroBat on its default, RetroArch with the PicoDrive core. It keeps saves for every 32X game
that has one.

Avoid Kega Fusion: it keeps its saves outside RetroBat's `saves` folder and its save states in the
Mega Drive's folder, so neither reaches RomM.

## BIOS

No 32X emulator needs a BIOS. RetroBat lists three 32X files without a checksum, so RomMBat cannot
look them up in RomM; every emulator here starts the games without them.

## Saves

A save follows you to another emulator only where the two read the same file
([Changing emulator](../saves/index.md#changing-emulator)). On the 32X every emulator keeps its own.

## Known issues

Two 32X games save to a different kind of chip, NBA Jam Tournament Edition and NFL Quarterback
Club. ares and BizHawk do not keep those saves at all, so play them on RetroArch's PicoDrive core
or on jgenesis. BizHawk's 32X core is also called PicoDrive, and it is not the one to pick.
