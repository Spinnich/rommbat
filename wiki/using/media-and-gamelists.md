# Media and gamelists

```powershell
dotnet run --project src/RomMBat.Agent -- gamelist --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- gamelist snes --no-reload --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- gamelist --media all --root D:\retrobat-test
```

`sync` already does all of this. `gamelist` is the same pass on its own, and it needs no
server: everything it writes comes from the local store, so it runs on a handheld that has been
off the network for a week.

Artwork is fetched for covers, thumbnails, marquees and videos by default, and manuals are
opt-in. It counts against the same disk budget the ROMs do. `--media` takes a comma-separated
list, `all`, or `none`.

After writing, the agent asks EmulationStation to reload its gamelists. That only answers while
ES is running and has no effect while a game is up, so a message saying the reload did not
happen is ordinary rather than a fault. `--no-reload` skips the call.
