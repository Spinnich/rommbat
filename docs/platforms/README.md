# Platform certification records

One file per certified RetroBat system, named `<system>.md` after the folder name in
`es_systems.cfg`.

**Certify per RetroBat system, never per aggregate.** "RetroArch works" is not a claim
anything can be verified against, because each libretro core has its own save naming,
state directory (`{{system}}/libretro.{{core}}`) and BIOS requirements.

A platform is certified when all nine of these pass against a real install. **A platform
is not done at eight of nine**, and none of it can be desk-checked: step 7 requires
actually launching a game.

1. Folder mapping resolves, and the record names **which layer** resolved it.
2. `<extension>` list captured; a known-unsupported file is correctly excluded.
3. Required BIOS from `batocera-systems.json` resolved against RomM by md5; gaps listed.
4. Save shape classified (A/B/C/D) and the battery save round-trips.
5. Save state round-trips including its screenshot, per `es_savestates.cfg`.
6. Where class D applies, the per-game memory card option is verified.
7. A game launches from EmulationStation after sync, with art and metadata.
8. Play session recorded and reaches RomM.
9. Re-sync is a clean no-op.

Load the `platform-certification` skill before starting. Record what failed as well as
what passed; a record that only lists successes is not evidence.

## Rollout order

Second through sixth generation consoles first, which is where the usage is and where the
save shapes stay tractable. Arcade is deliberately last: it is the only wave needing the
explicit folder-choice decision from M2, and it carries romset-version coupling nothing
else does.

| Wave | Systems                                                                                      | Why here                                                         |
| ---- | -------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| 1    | `nes`, `snes`, `gb`, `gbc`, `gba`, `megadrive`, `mastersystem`                               | Class A saves, no BIOS, single files. Proves the spine           |
| 2    | `n64`, `psx`, `saturn`, `segacd`, `pcengine`, `pcenginecd`                                   | Introduces BIOS and disc formats                                 |
| 3    | `ps2`, `gamecube`, `dreamcast`, `xbox`                                                       | The hard save shapes: memory cards, GCI folders, VMU             |
| 4    | `neogeo`, `neogeocd`, `fbneo`                                                                | Arcade: romset-versioned naming, the ten-folder mapping question |
| 5    | `wonderswan`, `wonderswancolor`, `ngp`, `ngpc`, `lynx`, `gamegear`, `atari2600`, `atari7800` | Long tail, mostly class A                                        |

The order is derivable rather than hand-maintained: `es_systems.cfg` carries
`<manufacturer>`, `<hardware>` and `<release>` per system, so filtering to
`hardware=console` and sorting by release year reproduces roughly this list and stays
correct as RetroBat adds systems.

## Nothing is certified yet

The framework has to work end to end on a single platform first, which is M1 through M6.
