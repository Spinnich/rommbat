# Sync sets

A sync set is a saved choice of games to keep on this device. Your RomM library can be far larger
than one drive, so rather than copying all of it, you say which part you want, and RomMBat keeps
that part in step as the library changes.

## What a set can hold

| Kind               | Holds                                                                                |
| ------------------ | ------------------------------------------------------------------------------------ |
| Platform           | Every game on one RomM platform, such as all your SNES games                         |
| Collection         | One of your RomM collections                                                         |
| Smart collection   | One of your RomM smart collections, which RomM keeps up to date by its own rules     |
| Virtual collection | One RomM builds for you, such as by genre. Made from a terminal only (`sets add`)    |
| Filter             | A search: part of a name, and any of RomM's filters, such as genre, region or rating |
| Picked             | Single games you installed from [Find a game](browse-and-install.md)                 |

Collections need the `collections.read` permission from [pairing](../getting-started/pairing.md).
A set asks RomM what it holds each time it syncs, so a game added to a collection in RomM comes
down on the next sync.

## Making and changing sets

Choose Sync sets on the main menu. On the list:

- Accept opens the set under the cursor.
- The left face button syncs every set.
- The top face button queries every set.
- Start opens the menu, which has New set, as in [Your first sync](../getting-started/first-sync.md).

On a set, the left face button syncs it and the top face button queries it. Delete set is in its
Start menu.
The set's screen shows what it holds, how much RomM says that weighs, how much it takes up on this
device with artwork, when it was last queried, and the games it skipped and why.

**Querying only asks RomM what is in a set, and downloads nothing.** Syncing queries first and
then downloads. Both need the server. Querying a large set takes minutes; press Back and choose
Stop to stop, and the next query continues from where it stopped.

## Limits on one set

A set made on the controller has no limit of its own. The [disk limit](disk-budget.md) covers all
your sets together, and a sync stops adding games when it is reached.

To cap one set, make it from a terminal with `rommbat-agent sets add` and its `--max-games`,
`--max-bytes` and `--order` options (see [Command line](../reference/cli.md)). The set's screen
shows those limits under Limits.

## Games a set skips

The set's screen lists each game it could not put here, with the reason:

- its platform has no RetroBat folder (see [below](#when-games-land-in-the-wrong-folder));
- RomM holds it as a folder, which RomMBat cannot sync yet, or as several files, which it syncs
  only for PlayStation (PS1) disc sets so far;
- it is too large for the drive's filesystem, which on FAT32 means over 4 GB;
- RomM has no file for it.

A game whose file type EmulationStation does not list for its system still comes down. RetroBat's
list of file types covers every emulator a system offers, so it cannot say whether yours opens
the file. The set's screen lists those games under Not listed, because EmulationStation will not
show them.

## When a game leaves a set

A game that leaves a set, because it left the collection or no longer matches the filter, stays
on this device. The set's screen lists it under Left the set. Nothing is removed until you
choose to remove it; see [Disk space](disk-budget.md#making-room).

## Deleting a set

Deleting a set asks what to do with its games:

- Delete it and take its games off this device. RomMBat shows what would go before anything
  goes. A game another set still wants is kept, and so is every save and save state.
- Delete it and leave the games where they are. Nothing on disk changes.

## When games land in the wrong folder

Each RomM platform's games go into one RetroBat system folder, such as `roms\snes`. RomMBat works
out which folder from RomM's own name for the platform and a table of known names. When it
cannot, or gets one wrong, choose Platforms on the main menu.

Open a platform to see where its games go and why. Press Accept to choose a folder yourself, or
choose Use the automatic choice from the Start menu to go back to RomMBat's own choice. A platform with no folder is listed as
unmapped, and a sync skips its games until you choose one.

Games already downloaded stay in the folder they went to. The next sync puts new games in the
new folder.
