# BIOS and firmware

Some emulators need a BIOS or firmware file from the original console before a game will start.
If your RomM library has those files, RomMBat puts them in RetroBat's `bios` folder for you.

## What to upload to RomM

Upload the BIOS files for your systems to RomM, under any platform and any name. RomMBat finds
each file by its checksum (md5), so `bios.sms` in RomM still lands as the Master System BIOS
under the name each emulator looks for, and one file can fill several of those names.

To see which files a system's emulators read, open the
[firmware matrix](https://github.com/Spinnich/rommbat/blob/main/docs/platforms/firmware.md) and
find the system by its RetroBat folder name, such as `mastersystem` or `psx`. Each row is one
file RomMBat places, with:

- **Required**: RetroBat's own list names it, or an emulator refuses to start some games
  without it. Upload these first.
- **Readers**: which emulators and cores read it. RetroBat's list stands for the system's
  default emulator, so a file it alone reads is one you can skip if you play on another.
- **Accepted**: the checksums RomMBat accepts there. Upload a file with any one of them. Where
  there are several, they are usually versions for different regions, and RomMBat picks by the
  order below.

A file listed under "Read, never fetched" is one RomMBat cannot place for you (below).

`rommbat-agent bios <system>` answers the same question for your own library: what is present,
what it would fetch, and the checksum of each file your library lacks (below).

### Switch and PS3

RomMBat fetches no firmware for either.

- **PS3**: RPCS3 installs the system software itself, from Sony's `PS3UPDAT.PUP`, through its own
  menu (File, then Install Firmware). Install it there once; nothing in RomM changes that.
- **Switch**: each emulator installs the system firmware through its own menu, the same way. The
  key files (`prod.keys`, `title.keys`) are read from a folder RetroBat sets per emulator, which
  RomMBat has not yet measured, so it reports them rather than guess where they go. Place them
  where RetroBat's own documentation for your emulator says.

## What a sync fetches

Before downloading games, a sync fetches the BIOS files that any emulator RetroBat offers for
those games' systems reads, when your RomM library has them. That covers more than RetroBat's own
list, which names only what each system's default emulator needs. It finds each file by its
checksum, so the file's name in RomM does not matter. You need to have given this device the
`firmware.read` permission when you [paired](../getting-started/pairing.md).

**A missing BIOS never stops a sync.** Most emulators need only some of a system's files, or
none. So a file your library lacks is reported, and the games still come down.

When your library holds several versions of one file, RomMBat prefers the one RetroBat lists,
then the one the emulator prefers, then the one from your preferred region: USA, then Europe,
Japan and world, unless you change the order (below). If nothing tells two versions apart,
RomMBat fetches neither and says so, rather than guess.

RomMBat never overwrites a BIOS file you put there. If the file in `bios` is not one any emulator
accepts, RomMBat reports it and leaves yours in place, because a BIOS that works for you is worth
more than RomMBat's idea of the right one. The one file it replaces is its own download, untouched
since, when your library later gains a better version.

## Files RomMBat cannot fetch

Some files have to come from you:

- A file no source gives a checksum for, so RomMBat cannot tell which file in your library is the
  right one. When one of those is not in `bios` and your library has a file under exactly its
  name, the report names that file and its checksum for you to judge, and RomMBat still leaves it
  alone.
- A file your RomM library does not have.
- A zipped firmware set whose files inside are not the ones the emulator expects. RomMBat
  downloads a zip with the right name, checks every file inside it, and keeps it only if they
  match.
- Firmware an emulator installs through its own menu, such as the Switch and PS3 system software,
  and files an emulator reads from its own folder rather than `bios`.

Copy these into RetroBat's `bios` folder yourself, where RetroBat's own documentation says they
go. This is why a game on a [certified row](../platforms/index.md) can still refuse to start: the
row was tested with its BIOS in place.

## Checking from a terminal

`rommbat-agent bios` reports, for each system you have games on, which files are present, which
it would fetch, which your library lacks, and which it cannot check, and under each file which
emulators read it. Under a file your library lacks, it prints the checksum to search for, and
why, when RomM has a record that is not usable. Files it cannot check are listed at the end, with
a library file of the same name where there is one. It writes nothing until you add `--apply`.
`--all` covers every system RetroBat knows. `--region-order japan,usa,europe,world` changes which
region's version is preferred, and `--region-order default` puts it back. See
[Command line](../reference/cli.md).
