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

Upload these to RomM, under any name, and a sync fetches them under the names SMSHawk looks for:

| For                    | The BIOS                         | md5                                |
| ---------------------- | -------------------------------- | ---------------------------------- |
| A USA or European game | Master System (USA, Europe) v1.3 | `840481177270d5642a14ca71ee72844c` |
| A Japanese game        | Master System (Japan) v2.1       | `24a519c53f67b00640d0048ef7089105` |
| A Korean game          | Master System (Korea)            | `e49e63328c78fe3d24ae32bf903583e0` |

The first two also land as the copies some RetroArch cores read under their own names, such as
`bios_U.sms`, though those cores start without them. The
[firmware matrix](https://github.com/Spinnich/rommbat/blob/main/docs/platforms/firmware.md#mastersystem)
lists every file and which emulator reads it, and [BIOS and firmware](../using/bios.md) covers
what to do when your library lacks one.

## Saves

A save follows you to another emulator only where the two read the same file
([Changing emulator](../saves/index.md#changing-emulator)). On the Master System, RetroArch's
Genesis Plus GX and PicoDrive cores share one save. The rest each keep their own.

## Known issues

**The standalone Mednafen will not start a game after the standalone Mesen has saved it.**
Mednafen opens Mesen's save file, cannot read it, and stops. Move that save out of
`saves\mastersystem` to play the game under Mednafen. A sync does not bring another computer's
Mesen save down for a game that runs under Mednafen here, though restoring one by hand does.
