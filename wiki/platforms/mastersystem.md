# Master System

|                 |                                                                              |
| --------------- | ---------------------------------------------------------------------------- |
| RetroBat system | `mastersystem`                                                               |
| Games go in     | `roms\mastersystem`                                                          |
| BIOS            | Only for BizHawk's SMSHawk, fetched when your library has it                 |
| Tested rows     | [Platforms](index.md#mastersystem) lists each emulator and whether it passed |

## Which emulator to use

Leave RetroBat on its default, RetroArch with the Genesis Plus GX core. Avoid RetroArch's FBNeo
core: it does not start games named the way RomM libraries usually name them, because FBNeo
expects its own file names.

Kega Fusion keeps its in-game saves inside its own program folder rather than in `saves`.
RomMBat syncs them from there, along with its save states. Its controls need setting up in Kega's
own menu before your pad works.

## BIOS

Only BizHawk's SMSHawk needs a BIOS, and it refuses to start any game without one. Every other
emulator starts without.

A sync fetches both Master System BIOS files from your RomM library, under the names SMSHawk looks
for, whatever they are called in RomM:

- `[BIOS] Sega Master System (USA, Europe) (v1.3).sms`, for a USA or European game
- `[BIOS] Sega Master System (Japan) (v2.1).sms`, for a Japanese one

It also fetches the copies some RetroArch cores read under their own names, such as `bios_U.sms`.
If your library lacks them, see [BIOS and firmware](../using/bios.md).

## Saves

A save follows you to another emulator only where the two read the same file
([Changing emulator](../saves/index.md#changing-emulator)). On the Master System, RetroArch's
Genesis Plus GX and PicoDrive cores share one save. The rest each keep their own.

## Known issues

**The standalone Mednafen will not start a game after the standalone Mesen has saved it.**
Mednafen opens Mesen's save file, cannot read it, and stops. Move that save out of
`saves\mastersystem` to play the game under Mednafen. A sync does not bring another computer's
Mesen save down for a game that runs under Mednafen here, though restoring one by hand does.
