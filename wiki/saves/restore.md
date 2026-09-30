# Restoring saves and states

Restoring brings back a save or save state that RomM has and this device does not: one you
deleted by mistake, or states you want on a new device. It is done from a terminal, with
`rommbat-agent saves restore`.

## Restore

In your RetroBat folder, with the server reachable:

1. Run `emulators\rommbat\rommbat-agent.exe saves restore`. It lists everything RomM has that
   this device does not, marking each row as a save or a state, and whether a state has a
   screenshot. It writes nothing.
2. Run it again with `--apply` to put them back.

To restore one game, add its RomM game number: `saves restore 391`. To restore one save of that
game, add the slot name the list shows; a slot picks among saves only, not states. Both still
need `--apply` to write.

## What restore will not do

- It does not write a save for a game you are playing. Quit the game and run it again.
- It does not overwrite a save already on this device. A save that differs on both sides is a
  [conflict](conflicts.md), which you settle there.
- It cannot check a save state it brings back; see [Save states](states.md#what-cannot-be-checked).

The list says which rows it could not place, and why. See [Command line](../reference/cli.md)
for every option.
