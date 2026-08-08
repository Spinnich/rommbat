---
name: platform-certification
description: Certifying a RetroBat system end to end before claiming it works. Use when adding support for a platform or when asked whether a platform is done.
---

# Platform certification

Once the framework works end to end on one platform, stop building horizontally and certify
platforms one at a time. Each surfaces its own edge cases; certifying in isolation keeps
them from arriving as one intermixed pile.

**Certify per RetroBat system, never per aggregate.** "RetroArch works" is unverifiable:
each libretro core has its own save naming, its own state directory
(`{{system}}/libretro.{{core}}`) and its own BIOS needs.

## Checklist

Record results in `docs/platforms/<system>.md`. All nine, or it is not certified.

1. Folder mapping resolves, and the resolution layer is recorded.
2. `<extension>` list captured from the live `es_systems.cfg`; a known-unsupported file is
   correctly excluded from the sync set and reported.
3. Required BIOS from `batocera-systems.json` resolved against RomM **by md5**; gaps listed
   with expected filename and hash.
4. Save shape classified A/B/C/D, and a battery save round-trips.
5. A save state round-trips including its screenshot, per `es_savestates.cfg`.
6. Where class D applies, the per-game memory card option is verified via `es_settings.cfg`.
7. A game launches from EmulationStation after sync, with art and metadata present.
8. A play session is recorded and reaches RomM.
9. **Re-sync is a clean no-op**: zero uploads, zero downloads, no gamelist churn. This is
   the strongest single signal that slots, cursors and mapping are all correct.

## Wave order

| Wave | Systems                                                                                      | Why here                                                                            |
| ---- | -------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| 1    | `nes`, `snes`, `gb`, `gbc`, `gba`, `megadrive`, `mastersystem`                               | Class A saves, no BIOS, single files. Proves the spine                              |
| 2    | `n64`, `psx`, `saturn`, `segacd`, `pcengine`, `pcenginecd`                                   | Introduces BIOS and disc formats                                                    |
| 3    | `ps2`, `gamecube`, `dreamcast`, `xbox`                                                       | The hard save shapes                                                                |
| 4    | `neogeo`, `neogeocd`, `fbneo`                                                                | Arcade: romset-versioned naming, the fan-out question, 12 BIOS files for `neogeocd` |
| 5    | `wonderswan`, `wonderswancolor`, `ngp`, `ngpc`, `lynx`, `gamegear`, `atari2600`, `atari7800` | Long tail, mostly class A                                                           |

Arcade is last on purpose: it is the only wave needing the explicit folder-choice decision
and the only one coupled to romset versions.

The order is derivable rather than hand-maintained: `es_systems.cfg` carries
`<manufacturer>`, `<hardware>` and `<release>`, so filtering to `hardware=console` for
Atari, Bandai, NEC, Nintendo, Sega, SNK and Sony and sorting by year reproduces this list
and stays correct as RetroBat adds systems.
