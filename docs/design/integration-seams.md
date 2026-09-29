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
| `rommapp/argosy-launcher`                                        | **Mined and closed.** See below                            |
| `abduznik/Freegosy`                                              | **Mined and closed.** See below                            |

**Argosy and Freegosy were read for leads, never as evidence.** Every lead that could change
RomMBat was re-asked of a live server or a real RetroBat install, and what held is an upstream
fact, cited by the skill that relies on it. None of their paths applies: Argosy targets Android
and Freegosy desktop emulators, EmuDeck and RetroDECK. Freegosy targets RomM 4.9, and several of
its answers are wrong against the server: its play-session payload is a 422 (RM-20), the 409 body
it parses does not exist (RM-19), and `device_id` does not isolate saves (RM-18). Argosy's
gamepad conventions shaped the UI and are stated where the code applies them. The leads cut at
triage are in the deleted ledgers, `git log --diff-filter=D -- docs/argosy-findings.md
docs/freegosy-findings.md`; re-reading either client is unlikely to repay the effort.
