# Conflicts

A conflict outlives the flush that found it, and `saves resolve` is how it ends. There is no
default side:

```powershell
dotnet run --project src/RomMBat.Agent -- saves resolve 42 "libretro:battery" --keep-local
dotnet run --project src/RomMBat.Agent -- saves resolve 42 "libretro:battery" --keep-server
```

`--keep-local` overwrites the server's copy. Both sides prune the dated copy under
`emulators/rommbat/replaced/` once the slot is back in step. It writes the same save files a
flush does, so it takes the same lock: run it while a flush is in flight and it refuses with
exit 3 rather than doing half of one.
