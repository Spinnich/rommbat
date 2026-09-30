# System name as a player knows it

<!-- The template for a system's guide page. Copy it to wiki/platforms/<system>.md, where
     <system> is RetroBat's folder name, once the system has a certified row, and add the page
     to the Platforms section of mkdocs.yml's nav. Write it from the certification record in
     docs/platforms/<system>/, for a player: which emulator to pick, what to supply, and what
     will not work. Never restate a row's status: that lives in platforms/index.md, generated
     from data/certification.json. Leave out a section that has nothing to say, apart from
     "Which emulator to use", and delete this comment. -->

<!-- In the last row, link index.md#<system>, the system's section of the generated page. -->

|                 |                                                                 |
| --------------- | --------------------------------------------------------------- |
| RetroBat system | `system`                                                        |
| Games go in     | `roms\system`                                                   |
| BIOS            | None, or what RomMBat fetches, and what you supply yourself     |
| Tested rows     | [Platforms](index.md) lists each emulator and whether it passed |

## Which emulator to use

The row to leave RetroBat on, and why. Name any row not to pick, and what goes wrong on it, in
the player's terms rather than the record's.

## BIOS

What a sync fetches from RomM, which emulators refuse to start without it, and any file RomMBat
cannot fetch, with where it goes.

## Saves

Which emulators read the same save file, so a game carries over when you switch between them,
and any setting a save needs, named as EmulationStation shows it.

## Known issues

What a player will run into on a tested row, and what to do about it.
