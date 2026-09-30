# How saves sync

```powershell
dotnet run --project src/RomMBat.Agent -- hooks status --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- hooks install --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- menu status --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- menu install --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- saves --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- flush --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- flush --offline --root D:\retrobat-test
```

`sync` installs the hooks and the ES menu entry on its first run and flushes before anything
else it does, so none of this is normally typed.

The `start` and `quit` hooks trigger a pass; `game-start` and `game-end` do not. Those two run
inside the game-launch path, so they write a spool file and exit, and the `start` or `quit` that
brackets them picks the record up by spawning `rommbat-agent background <event>`. A tree with
hooks and no agent simply spools, and the next `sync` drains it. The pass writes what it did to
`emulators\rommbat\logs\background.log`, the only place to look, since it runs with no console
window.

`flush` works with the server unreachable: draining the spool, correlating play sessions and
rescanning saves are all local, so `--offline` is a real mode rather than a preview. `saves` is
the report of what is on disk, what has gone up, what cannot go up and why, and what is waiting
on a decision.

## Uninstalling

`hooks uninstall` removes exactly RomMBat's own file from each event folder and nothing else in
them, and `menu uninstall` removes its `.menu`, its artwork and its one `<game>` element, leaving
the entries RetroBat put in that gamelist alone. `uninstall` does both, reverts every per-game
memory card conversion from its record, and with `--content` and `--bios` removes synced games
and firmware; it refuses while any save, session or hook event is unsent.
