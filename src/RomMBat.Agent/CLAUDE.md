# RomMBat.Agent

`rommbat-agent.exe`: a console process that does one pass and exits. There is no daemon,
because a portable install cannot register a service. Each subcommand is a class under
`Commands/`, and `Program.DispatchAsync` routes to it and turns exceptions into an `ExitCode`.
The subcommand table, with which ones reach the network, is in
[docs/architecture/projects.md](../../docs/architecture/projects.md#srcrommbatagent).

## Traps

- **A command is a printer over a Core service.** Planning, syncing and locking live in Core
  (`Sets/`, `Sync/`) because the UI runs the same sequence. Logic added here is logic the UI
  does not get.
- **`game-start` and `game-end` never open a socket, start a process or wait on a lock**
  (`CLAUDE.md` rule 4). `background` serves only `start` and `quit`.
- **Anything destructive previews by default and writes on `--apply`** (`bios`, `evict`,
  `saves restore`, `uninstall`, `outbox drop`). `sync --dry-run` is the one exception to the naming.
- **Core's `SaveConflictResolver` is the only caller of `overwrite=true`**, reached from
  `saves resolve` here and from the UI's conflict screens. Nothing reached from a flush picks a
  side in a conflict.
- **Help text is user documentation.** Changing a flag or an exit code owes the docs that
  describe it (`pre-pr-verification`, "Documentation parity"), and the guide's
  `wiki/reference/cli.md` is generated from `--help`, so a changed line fails a test until it is
  regenerated (`wiki/README.md`).
- Tests go in `tests/RomMBat.Agent.Tests` and drive `Program.DispatchAsync`, not the command
  class, so the exception handlers are in the path.
