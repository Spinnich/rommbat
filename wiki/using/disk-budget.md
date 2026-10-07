# Disk space

RomMBat has two limits on how much of the drive it uses. Both are under Disk space on the main
menu: step each one with Left and Right, then choose Save at the bottom. Back with unsaved
changes asks whether to discard them.

| Setting           | What it means                                                                                    |
| ----------------- | ------------------------------------------------------------------------------------------------ |
| Always leave free | RomMBat stops downloading before the drive gets this empty. Always on, 2 GB unless you change it |
| Limit RomMBat to  | The most RomMBat's own downloads may take up together. No limit unless you set one               |

The limit counts games and their artwork together, across every sync set. Games you put in
RetroBat yourself are never counted, and never removed.

When a sync reaches either limit, it skips the games that do not fit, names them, and finishes
with Stopped by the disk budget. The games that did fit are all there.

## Making room

**RomMBat never picks games to delete on its own.** You choose what goes:

- Delete a sync set and choose to take its games off (see [Sync sets](sync-sets.md#deleting-a-set)).
- Take one game off from its screen in [Browse library](browse-and-install.md#take-one-game-off).

Both show what would go before anything goes. A game another set still wants is kept, and saves
and save states are never removed.

From a terminal, `rommbat-agent evict` shows which games would go to get back inside your limit,
and removes them when you add `--apply`. It never removes a game you put there yourself, or one
whose save has not reached RomM yet.

## When the numbers look wrong

If you delete games by hand, RomMBat still counts them against the limit until it finds out they
are gone. On the Disk space screen, Check the files behind these numbers, in the Start menu, checks every
file RomMBat has recorded against the drive, and offers to forget the ones that are not there. Forgetting only changes
RomMBat's records: it deletes nothing, and it never forgets a save, because RomM may still have
it and [restoring](../saves/restore.md) brings it back.

A download that was cut off leaves a partial file in `emulators\rommbat\partial\`, and the next
sync of that game carries on from it. `rommbat-agent evict --apply` clears out any that are no
longer needed. Leave the folder alone while RomMBat is running: a save being restored is
unpacked there too.
