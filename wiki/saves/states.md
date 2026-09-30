# Save states

A save state is a snapshot an emulator takes of the whole game at one moment, separate from the
saves the game makes itself. RomMBat sends your save states to RomM, with their screenshot, but
it only brings one back when you ask.

## Going up

A save state goes up whenever you make a new one or overwrite an old one, at the same times as
your saves (see [How saves sync](index.md#when-it-happens)). Each state is filed under the
emulator and core that made it, so the same game played in two emulators keeps two separate sets
of states.

States go up for the emulators RetroBat keeps a save-state folder for. Some emulators keep their
states somewhere of their own choosing; RomMBat lists those under `rommbat-agent saves` rather
than sending them, except where it has been tested against that emulator.

## Coming back

**A sync never brings a save state down.** Deleting a state is something you meant to do, and a
state that came back by itself would look like a fault. To bring states back, see
[Restoring saves and states](restore.md).

A state that comes back is put where the emulator that made it looks for it, named after the
game file on this device.

## What cannot be checked

RomMBat cannot check a save state it brings back. RomM publishes no checksum for a state, so there
is nothing to compare it with. A state also does not record which version of the emulator made
it, and an emulator can refuse a state from a different version of itself. RomMBat says both of
these whenever it restores a state.
