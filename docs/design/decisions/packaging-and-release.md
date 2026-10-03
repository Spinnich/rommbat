---
summary: The portable zip is the artefact, how it is assembled, and who announces RomMBat upstream.
read-when: Changing tools/publish.ps1, the release artefact, an installer, or anything about announcing a release.
---

# Packaging and release

**The portable zip landed early, before wave 1.** `tools/publish.ps1` publishes the three
projects, assembles the seven files an install needs, refuses to package a set missing any of
them, writes `publish/rommbat-<version>-win-x64.zip` (the version is
[SemVer](versioning.md)), and extracts into a tree with `-Deploy`. CI calls
it rather than carrying its own publish steps. It came forward because the platform rollout
redeploys on every defect a pass turns up, and repeating a seven-file hand copy across seven
systems is a defect generator.

Two things it measured, both of which a hand copy gets wrong silently. A publish emits **101 MB
of `.pdb` files** beside the 185 MB payload, so the layout names its files rather than copying
the output directory. And **publishing over a warm output directory that is missing a native
skips every native and still reports success**: deleting `libSkiaSharp.dll` alone left all four
absent, so each project's output is cleaned first.

The zip's entries carry the `emulators/rommbat/` prefix, because the artefact is extracted at
the RetroBat root and `RetroBatInstall.AppDirectory` pins the app to that directory. A flat
archive extracts to a tree whose ES menu entry cannot resolve its executable.

- `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` so no .NET
  install is needed. RetroBat already requires the VC++ redist; add nothing else.
- **A portable zip is the primary artefact**, extracted into the RetroBat tree, requiring
  no admin rights and touching no machine state. A conventional installer is at most a
  convenience wrapper over the same layout, never the only route.
- Setup writes the ES menu entry and the script hooks with relative paths, appends rather
  than replaces existing hooks, and removal takes the tree back to its prior state.
- **`uninstall` always reverses what RomMBat wrote into RetroBat's own files**: the hooks, the
  menu entry, and every per-game memory card conversion, put back from its record and with the
  stranded-save warning. The library is the user's, so synced ROMs with their media and
  gamelist entries go only with `--content`, and synced firmware only with `--bios`. Adopted
  files and saves never go. **Any unsent work refuses the whole removal**, because the outbox
  lives in the folder the user deletes next. An entry the server refused is not waiting to be
  sent, so it does not refuse; the preview names it, and deleting the folder loses it. `emulators/rommbat` and the device in RomM are left
  and named, since a running executable cannot delete itself.
- Document the portable story explicitly, including the FAT32 4 GB ceiling and the
  recommendation to use exFAT or NTFS for any library containing disc images.
- README, getting-started guide, wombat mascot, and a compatibility table of tested
  RetroBat versions.

**Announcing RomMBat upstream is not a milestone step and is not automatic.** Listing it in
`rommapp/romm`'s README or posting it in the RomM Discord is a conversation to have with the
RomM maintainers, on their timing, and it is Spinnich's to open. Nothing in this plan should
be read as scheduling it.
