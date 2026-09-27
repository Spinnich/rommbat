---
summary: What RomM and RetroBat already provide that RomMBat builds on rather than rebuilds, and which prior art was mined.
read-when: Choosing how to reach RomM or RetroBat, or considering another client's code as a source.
---

# Integration seams

## RomM already ships a companion-app protocol

All of this is on `rommapp/romm` master today. Verified against a local checkout of the
source, not just the docs.

| Concern            | Endpoint / file                                                                                   |
| ------------------ | ------------------------------------------------------------------------------------------------- |
| Capability probe   | `GET /api/heartbeat` (unauthenticated, returns `SYSTEM.VERSION`)                                  |
| Device pairing     | `backend/endpoints/device_auth.py`, RFC-8628 style                                                |
| Long-lived tokens  | `backend/endpoints/client_tokens.py` (`rmm_` + 64 hex, up to 25/user)                             |
| Device registry    | `backend/endpoints/device.py` (incl. the `sync_config` dict)                                      |
| Save sync protocol | `backend/endpoints/sync.py` (`/negotiate`, `/sessions/{id}/complete`)                             |
| Playtime           | `backend/endpoints/play_sessions.py`                                                              |
| Save/state I/O     | `backend/endpoints/saves.py`, `backend/endpoints/states.py`                                       |
| ES gamelist export | `backend/endpoints/export.py`, `backend/utils/gamelist_exporter.py`                               |
| Platform map       | `backend/utils/platform_aliases.py` (identity, then 138 aliases: a starting point, not an answer) |
| Schema for codegen | `GET /openapi.json` (served at the root, not under `/api`)                                        |

Published references: [Client API Tokens](https://docs.romm.app/latest/developers/client-api-tokens/)
and [Device Sync Protocol](https://docs.romm.app/latest/developers/device-sync-protocol/).
**The docs have drifted from the code** (they show `roms:[{saves:[...]}]` for negotiate and
`mac`/`paths` on device create; the real payloads are `saves:[...]` and
`mac_address`/`sync_config`). Generate the client from `/openapi.json` and treat the
backend as the contract.

## RetroBat's integration seams (no fork needed)

| Seam                                                  | Use                                                                           |
| ----------------------------------------------------- | ----------------------------------------------------------------------------- |
| `roms/<system>/`                                      | Where ROMs land; folder names come from `es_systems.cfg`                      |
| `roms/<system>/gamelist.xml`                          | Metadata ES reads directly                                                    |
| `roms/<system>/images`, `videos`, `manuals`           | Media siblings ES expects (per the RetroBat wiki)                             |
| `saves/`                                              | Emulator save output                                                          |
| `bios/`                                               | BIOS/firmware, flat at the root with few exceptions                           |
| `emulationstation/.emulationstation/scripts/<event>/` | ES event hooks. RetroBat drives these with `.bat`; RomMBat must use an `.exe` |
| `system/es_menu/*.menu`                               | How RetroBat registers launchable apps in the ES menu                         |

ES events include `start`, `game-start`, `game-end`, `game-selected`, `system-selected`,
`quit`, `shutdown`, `sleep`, `wake`, `update-gamelists`. RetroBat ships
`.emulationstation/scripts/start/updatestores.bat` and
`.emulationstation/scripts/update-gamelists/updatestores.bat`, which proves the `.bat`
path works **for a script that takes no arguments**. It does not generalise: M0 measured a
`.bat` failing to start at all once ES quotes an argument, which it does for any value
containing a space. Nine event folders exist on disk; `game-selected` and `system-selected`
fire (ES logs them on every navigation move, with system, rom path and display name) but
ship no folder.

RetroBat's **Content Downloader is not an extension point.** It is an XML feed of
`<repository><name/><url/></repository>` pointing at static content packages with no
lifecycle or config surface, and the repository list ships with RetroBat. Keep it in mind
as a possible distribution channel later, not as the mechanism.

## Reference implementations to mine

| Source                                                           | Take                                                       |
| ---------------------------------------------------------------- | ---------------------------------------------------------- |
| `rommapp/playnite-plugin` `Models/RomM/*`                        | C# DTOs for rom/platform/collection/device/pairing         |
| `rommapp/playnite-plugin` `Downloads/DownloadQueueController.cs` | Concurrent download queue with progress                    |
| `rommapp/grout` `cfw/batocera/data/platforms.json`               | RomM slug → ES folder list, the exact shape to copy        |
| `rommapp/grout` `cfw/*/data/save_directories.json`               | RomM slug → emulator save subdirectory list                |
| `rommapp/grout` `cache/save_sync.go`, `cache/background_sync.go` | Sync state machine and conflict handling, already proven   |
| RomM `backend/utils/gamelist_exporter.py`                        | Authoritative field list for the `<game>` elements to emit |
| `rommapp/argosy-launcher`                                        | **Mined and closed.** See the caveat below                 |
| `abduznik/Freegosy`                                              | **Mined and closed.** See the caveat below                 |

**Argosy was named twice during planning and then never read, and until 2026-08-25 this table
had no row for it** while [freegosy-findings.md](../freegosy-findings.md) told its readers Argosy
had been "mined as trustworthy about the API". That was false, and both places are corrected
rather than quietly reworded. What the pass actually took is small and specific: it sent this
plan to re-measure `with_rom_id_index`, which turned out to be a **3.4 to 3.7 times regression
on a platform-scoped walk** on 5.2.0 (fixed at 7b-2a, and absent on `5.3.0-alpha.2`, which is why
the index is off under every scope again, #188), and to run the BIOS join that found
**84 of RetroBat's 353 requirements are `.zip` files no md5 comparison can ever match**. Neither
number is Argosy's; both are measured here. It targets Android, so **no path from it is valid for
RetroBat and none was taken**, and its own headline cost figure inverts on this library. The full
ledger, including the eighteen leads dropped at triage and the design notes addressed to M7b, is
[argosy-findings.md](../argosy-findings.md). **Treat that document as closed.**

**Freegosy is the one source here that is not `rommapp` and not version-aligned**, and it was
mined under a correspondingly higher bar: it targets RomM 4.9 against the 5.2.0 baseline it was mined under, it
is v0.5.x with one maintainer, and it targets desktop emulators, EmuDeck and RetroDECK, so
**none of its paths is valid for RetroBat and none was taken**. What it was good for was
pointing at save-protocol parameters this plan had never mentioned. Every claim was then
re-asked of the live server, and several of its own answers were wrong at 5.1.x: its play
session payload shape is a 422, its documented 409 body does not exist, and its per-device
isolation model is not what the server does. The full ledger, including the thirteen leads
dropped at triage and the six left open, is
[freegosy-findings.md](../freegosy-findings.md). **Treat that document as closed**; re-reading
the client is unlikely to repay the effort.
