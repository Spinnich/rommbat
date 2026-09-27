# RomMBat.Hook

`rommbat-hook.exe`: the EmulationStation event hook. Four copies are installed, one per event
folder, and the event it serves is the name of the folder it runs from. It writes one spool
file, and for `start` and `quit` also starts a detached `rommbat-agent background <event>`.
Load `retrobat-layout`, "Event hooks", before changing it.

## Traps

- **It references nothing, not even Core.** The three types it needs are compiled in from Core's
  source (see the `.csproj`), so every megabyte is paid four times on a portable drive rather
  than dragging in the store and the API client.
- **No socket, no database, no lock, no waiting.** `game-start` and `game-end` run inside the
  game-launch path (`CLAUDE.md` rule 4). Which events may spawn a pass is
  `SpoolRecord.BackgroundEvents`, and `HookSpawnTests` asserts it.
- **It must be an `.exe`.** A `.bat` hook never starts once an argument is quoted, and a `.ps1`
  never starts once the name contains a parenthesis.
- **The spool record is written before anything is spawned**, so a failed spawn never costs the
  play session. An I/O failure exits non-zero and ES ignores it; failing a launch over a missed
  record is the wrong trade.
- Trimmed, single-file and ReadyToRun on purpose; the `.csproj` comments hold the measurements.
