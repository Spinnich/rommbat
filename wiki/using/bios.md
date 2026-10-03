# BIOS and firmware

Some emulators need a BIOS or firmware file from the original console before a game will start.
If your RomM library has those files, RomMBat puts them in RetroBat's `bios` folder for you.

## What a sync fetches

Before downloading games, a sync fetches the BIOS files RetroBat lists for the systems those
games are on, when your RomM library has them. It finds each one by its checksum, so the file's
name in RomM does not matter. You need to have given this device the `firmware.read` permission
when you [paired](../getting-started/pairing.md).

**A missing BIOS never stops a sync.** RetroBat's list names every file any of a system's
emulators might read, and most emulators need only some of them, or none. So a file your library
lacks is reported, and the games still come down.

RomMBat never overwrites a BIOS file that is already there. If the file in `bios` is not the one
RetroBat lists, RomMBat reports it and leaves yours in place, because a BIOS that works for you is
worth more than RomMBat's idea of the right one.

## Files RomMBat cannot fetch

Some files have to come from you:

- RetroBat lists about half of its BIOS files without a checksum, so RomMBat cannot tell which
  file in your library is the right one.
- A file your RomM library does not have.
- Some zipped firmware, such as `neogeo.zip`. A zip's checksum covers the zip itself, so it
  matches only when your copy was built exactly like RetroBat's, even if the files inside are the
  same. When your library has a zip under the same name that does not match, the report names it
  and RomMBat leaves it alone.

Copy these into RetroBat's `bios` folder yourself, where RetroBat's own documentation says they
go. This is why a game on a [certified row](../platforms/index.md) can still refuse to start: the
row was tested with its BIOS in place.

## Checking from a terminal

`rommbat-agent bios` reports, for each system you have games on, which files are present, which
it would fetch, which your library lacks, and which it cannot check. Under a file your library
lacks, it prints the checksum to search for, and why, when RomM has a record that is not usable or
a zip by the same name. It writes nothing until you
add `--apply`. `--all` covers every system RetroBat knows. See
[Command line](../reference/cli.md).
