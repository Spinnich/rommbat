# Disk budget and freeing space

Freeing space is you naming what goes, never RomMBat choosing. There is no screen that picks
games to delete for you: RomMBat guessing which games matter least is a bad policy even when a
person starts it.

Deleting a sync set offers to take its games, and a game's detail screen in browse offers to
take that one. Both show a preview of what goes and what is kept before the press, and neither
can reach a save.

A sync the budget cut short says so, and `rommbat-agent sync` exits as `Partial`. It does not
offer to fix it.

## From the console

`evict` previews unless you pass `--apply`. Apart from `uninstall --content` and `--bios`, it is
the only command in the agent that deletes anything. Partial downloads live in `emulators/rommbat/partial/`; deleting one by hand is safe,
and the next sync starts that ROM again. `evict` also reports what under that directory is dead,
and reclaims it on `--apply`, which is the only thing that ever does. The reclaim needs the tree
lock, so `evict --apply` during a flush evicts and says the sweep will happen next time.

`rommbat-agent status --check-files` counts files the store lists that are gone from disk, as
the disk screen does, and `--repair-files` applies it. Both also show saves whose file or unit
is gone, and the repair leaves those rows alone.
