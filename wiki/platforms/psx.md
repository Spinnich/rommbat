# PlayStation

|                 |                                                                       |
| --------------- | --------------------------------------------------------------------- |
| RetroBat system | `psx`                                                                 |
| Games go in     | `roms\psx`                                                            |
| BIOS            | `psxonpsp660.bin`, which a sync fetches when your RomM library has it |
| Tested rows     | [Platforms](index.md#psx) lists each emulator and whether it passed   |

## Which emulator to use

Leave RetroBat on its default, RetroArch with the Beetle PSX HW core (`mednafen_psx_hw`). Every
emulator RetroBat offers for the PlayStation passed RomMBat's tests, but two of them need care:

- **BizHawk's two cores only ever start disc 1** of a game on several discs, because of how
  RetroBat starts them. Pick another emulator for those games.
- The standalone Mednafen has no memory card at RetroBat's settings, so a game cannot save. Set
  Emulated Memcards to 2 in the system's settings (Game settings, then Per system advanced
  configuration). It also cannot open a `.chd` disc image.

## BIOS

A sync fetches `psxonpsp660.bin` from your RomM library. Every emulator but RetroArch's PCSX
ReARMed core refuses to start a game without it. If your library lacks it, see
[BIOS and firmware](../using/bios.md).

## Games on several discs

A game on several discs arrives in a folder of its own, with a playlist that lists the discs.
EmulationStation shows it as one game, and you change discs from the emulator's menu when the
game asks.

## Saves

A PlayStation save is a memory card. A card that holds one game's saves syncs like any other save,
and a card every game shares does not, since it cannot be tied to one game
([Memory cards](../saves/memory-cards.md)). At RetroBat's settings every emulator but the
standalone Mednafen keeps a card per game, so leave those as they are. Switching DuckStation to a
shared card, or turning on PCSX ReARMed's second card, which every game shares, leaves those saves
on this device.

A save follows you to another emulator only where the two read the same file
([Changing emulator](../saves/index.md#changing-emulator)). On the PlayStation:

- RetroArch's three cores share one card per game.
- BizHawk's two cores share another.

The rest each keep their own.

## Known issues

**A DuckStation save state that RomMBat brought back loads only from EmulationStation's save-state
menu**, not from DuckStation's own load menu.
