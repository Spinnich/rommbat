# Directory saves and memory cards

## Directory saves

A directory save goes up as one archive, and `saves` names the unit rather than the path. Every
PSP save on an install shares the container `saves/psp/SAVEDATA`, so the report prints
`<container>/<key>`. The key is a Game ID, worked out from the launch window, the ROM header or
the save-state sidecar. `saves bind` corrects one, or settles a binding two routes disagreed on.
A binding is local: there is nowhere on the server to put one.

```powershell
dotnet run --project src/RomMBat.Agent -- saves bind psp ULUS10057 391
dotnet run --project src/RomMBat.Agent -- saves bind psp ULUS10057 --forget
```

## Splitting a shared memory card

A shared card is split one game at a time, and `saves convert` is the only command that changes
your RetroBat configuration. It writes a per-game option into `es_settings.cfg`, which survives
where an emulator INI edit would not: RetroBat regenerates those on every launch.

```powershell
rommbat-agent.exe saves convert 191723            # preview: what it would set, and what it costs
rommbat-agent.exe saves convert 191723 --apply    # write it
rommbat-agent.exe saves convert 191723 --revert   # put the setting back to what it was
```

It refuses while EmulationStation is running, because ES discards a setting written underneath
it. Quit ES first. An ES belonging to a different install on the same machine does not block it.

The card PCSX2 then writes is `saves/ps2/pcsx2/memcards/<rom stem>.ps2`.
