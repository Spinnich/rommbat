# Command line

<!-- Generated from rommbat-agent --help by tests/RomMBat.Agent.Tests/CliReferencePageTests.cs.
     Edit the help text in src/RomMBat.Agent/Program.cs and regenerate; an edit here fails the test. -->

`rommbat-agent.exe` sits in RetroBat's `emulators\rommbat` folder beside the RomMBat app, and does
one job each time you run it. Everything the app does, it can do from a terminal. This is
what `rommbat-agent --help` prints.

```text
rommbat-agent <subcommand> [options]

Subcommands
  pair        Pair this install with a RomM server
  status      Report local state, and probe the server unless --offline
  sets        list | add | show | remove | resolve sync sets
  platforms   list | map | unmap the RomM to RetroBat folder mapping
  browse      Print one page of the catalog
  game        show | install | remove <rom-id>: one game, by the id browse prints
  sync        Resolve a set and pull its ROMs into the tree
  budget      Show or set how much of this drive RomMBat may use
  evict       Show what would be removed to get back inside the budget
  bios        Report the BIOS RetroBat needs, and fetch it with --apply
  gamelist    Rewrite gamelist.xml from local state, and tell EmulationStation
  hooks       status | install | uninstall the EmulationStation event hooks
  menu        status | install | uninstall RomMBat's EmulationStation menu entry
  uninstall   Take the hooks, menu entry and memory card settings back out
  saves       What is on disk, what went up, and what is waiting on you
              saves resolve <rom> <slot> --keep-local | --keep-server
              saves restore [<rom> [<slot>]]: put back a save or state the server has
              saves convert <rom> [--revert]: give a game its own memory card, or take it back
  game-start  Record a launch. Journal only, no network
  game-end    Close a launch. Journal only, no network
  flush       One pass over everything waiting, then exit
  outbox      list | drop the entries the server refused or never received: drop <id> | --all-failed | --all-pending, with --apply
  background  start | quit: the pass an EmulationStation hook spawns. Not for typing

Options
  --help, -h        Print this and run nothing, whatever else is on the line
  --root <path>     The RetroBat root, when discovery cannot find it
  --server <url>    The RomM origin. Remembered after the first pairing
  --name <label>    How this device appears in the RomM device list
  --protect         Encrypt the stored token with a passphrase you type
  --passphrase <s>  Unlock a token stored with --protect, for any command that calls out
  --offline         status, sync, bios, saves, game show: work from local state without the server
  --dry-run         sync: say what would happen and write nothing
  --apply           evict, uninstall, game remove: actually remove. bios: actually fetch. saves
                    restore, saves convert: actually write. outbox drop: actually delete. Without it,
                    none of these writes
  --all-failed      outbox drop: every entry the server refused, instead of one id
  --all-pending     outbox drop: every unsent entry, when the server it names is gone
  --at-quit         saves convert: make the change when EmulationStation next closes
  --revert          saves convert: take a game's own memory card back to the shared one
  --all             bios: every system RetroBat knows, not just the ones with games
  --max <size>      budget: the cap, as 64GB, 500MB or none
  --media <kinds>   gamelist: which artwork to fetch, e.g. image,thumbnail,video
  --no-reload       gamelist: write the files without telling EmulationStation
  --no-scan         saves: report what is recorded without rescanning the tree
  --check-files     status: report recorded files and saves whose copy is gone from the tree
  --repair-files    status: drop those file rows, so the budget stops counting them. Saves stay
  --all-sessions    status: list every session read back, up to 50, not only the newest ten
  --keep-local      saves resolve: send this device's copy over the server's
  --keep-server     saves resolve: take the server's copy over this device's
  --content         uninstall: also remove synced games, their media and gamelist entries
  --bios            uninstall: also remove synced firmware under bios/
```
