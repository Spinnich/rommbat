# RomMBat

Sync a self-hosted [RomM](https://github.com/rommapp/romm) library with a
[RetroBat](https://github.com/RetroBat-Official) install on Windows.

RomMBat pulls a chosen subset of ROMs, metadata, media and BIOS down into RetroBat's
native folder layout, and pushes saves, states and play sessions back up. **RomM is the
authority, RetroBat is the player**, so the same collection stays coherent across a
RetroBat machine, the RomM web UI, and RomM's other clients.

The name is a portmanteau of RomM and RetroBat that lands close to "wombat". The mascot
is a wombat.

> [!WARNING]
>
> **Pre-release.** Pairing, device identity, the local store, catalog browsing, platform
> mapping, sync sets, content sync with a disk budget, metadata, media, `gamelist.xml` and
> BIOS all work. **Battery saves, save states, directory saves and playtime now cross**,
> along with conflict resolution and the Game-ID attribution that directory saves need.
> **Shared containers such as a PS2 memory card do not yet**, which is the rest of M6, and a
> device that has never held a directory save cannot receive one yet. No platform has been
> certified against a real emulator; see
> [Platform certification](#platform-certification) for what that means and when it starts.
> The repository also holds the design of record
> ([docs/PLAN.md](docs/PLAN.md)) and the measurements that corrected it
> ([docs/retrobat-findings.md](docs/retrobat-findings.md)). See [Status](#status).

## Why

RetroBat has no concept of a remote library. Today the only way to get a RomM library
onto a RetroBat box is to copy files by hand, and nothing carries saves, states or
playtime back. RomMBat closes both directions without forking RetroBat and without any
change to RomM: it integrates purely through RetroBat's existing folder and script seams,
and through the companion-app protocol RomM already ships.

## What it does

|              |                                                                                                                                               |
| ------------ | --------------------------------------------------------------------------------------------------------------------------------------------- |
| **Pull**     | ROMs, `gamelist.xml` metadata, box art / video / manuals, and BIOS, into `roms/<system>/` and `bios/`                                         |
| **Push**     | Battery saves, save states, directory saves and play sessions, back into RomM                                                                 |
| **Curate**   | Sync Sets: a named scope (collection, smart collection, platform, or a saved filter) plus a policy (max games, max bytes, ordering, eviction) |
| **Offline**  | Everything works with the server unreachable and reconciles on reconnect                                                                      |
| **Portable** | Lives entirely inside the RetroBat tree, survives a drive-letter change and a move to another PC                                              |

### Four constraints that shape the whole design

1. **Offline-first.** RomMBat runs on handheld Windows gaming PCs that are away from the
   RomM instance for days. Local SQLite is the source of truth; the network is optional.
   The EmulationStation hooks sit in the game launch path, so they append to a durable
   local journal and exit, never opening a socket. Measured: they do not block the launch,
   the emulator starts about 30 ms later regardless, but they do run **concurrently**, so
   the journal has to survive interleaved writers. A short-lived agent flushes the outbox
   when the server is reachable.
2. **Libraries reach 100,000+ games**, so the catalog is never mirrored. Online browsing
   is a thin paged client over the API; offline browsing shows the local subset. ROM
   content is strictly opt-in and bounded by a disk budget with eviction.
3. **Curation via Sync Sets.** A 100k library is unnavigable from a couch with a gamepad,
   so the device holds a curated slice, and the set definitions roam with the RomM device
   record.
4. **Portable-first.** Nothing outside the RetroBat tree: no `%APPDATA%`, no registry, no
   Windows service, no scheduled task, no admin rights, no machine-wide .NET requirement.
   No absolute path is ever persisted.

## Requirements

|          | Minimum     | Notes                                                            |
| -------- | ----------- | ---------------------------------------------------------------- |
| RetroBat | 8.2         | Checked from `system/version.info` at startup                    |
| RomM     | 5.1.0       | Checked from `GET /api/heartbeat` at startup                     |
| Windows  | 10 / 11 x64 | RetroBat's own requirement                                       |
| .NET     | none        | Published self-contained; RetroBat already ships the VC++ redist |

Below minimum, RomMBat refuses with a message naming both versions. Above but untested,
it warns and continues.

### Filesystem

A portable RetroBat often lives on exFAT or FAT32, which reaches into the design:

- **FAT32 cannot hold a file larger than 4 GB.** Plenty of PS2, GameCube and Wii images
  exceed that, so RomMBat detects the filesystem up front and skips or refuses oversized
  ROMs rather than failing mid-write. Windows reports the overrun as "There is not enough
  space on the disk" even with plenty free, so RomMBat never passes that message on. **Use
  exFAT or NTFS for any library containing disc images.**
- **FAT32 and exFAT both store modification times to 2 seconds, rounded up.** Measured, and
  exFAT is no finer than FAT32 despite its format allowing it. A freshly written save can
  therefore read as up to 2 seconds in the future. RomMBat compares on content hash first
  and uses mtime only as an ordering tiebreak, with tolerance for that skew.

## Authentication and scopes

**Device pairing is the only authentication path.** No password entry, no token pasting,
no OAuth flow. A gamepad is a terrible keyboard, and the pairing flow exists precisely so
the credential never has to be typed: RomMBat shows an 8-character code and a QR, you
approve it in the RomM web UI, and the token is written into the RetroBat tree.

The only thing you ever have to type is the server URL.

When you approve the pairing request, RomM lets you choose which scopes to grant. Grant
these, and nothing else:

| Scope                                | Needed for                                        | Without it                        |
| ------------------------------------ | ------------------------------------------------- | --------------------------------- |
| `roms.read`                          | Browsing and downloading ROMs                     | Nothing works                     |
| `platforms.read`                     | Platform list, folder mapping                     | Nothing works                     |
| `collections.read`                   | Collection and smart-collection sync sets         | Only platform and filter scopes   |
| `firmware.read`                      | BIOS sync                                         | BIOS must be copied manually      |
| `assets.read`                        | Pulling saves and states down                     | Push-only                         |
| `assets.write`                       | Pushing saves and states up                       | Pull-only, local saves stay local |
| `devices.read` / `devices.write`     | Device identity, sync negotiation, roaming config | No save sync at all               |
| `roms.user.read` / `roms.user.write` | Play sessions, last-played, favourites            | No playtime tracking              |
| `me.read`                            | Reading own account details during pairing        | Pairing fails                     |

**RomMBat never needs any of these, and should never be granted them:** `users.read`,
`users.write`, `roms.write`, `platforms.write`, `tasks.run`, `logs.read`. A RomMBat token
carrying one of those is over-scoped. A token can never exceed its owner's own scopes, so
an over-granted token usually means an admin paired the device rather than a purpose-made
account.

> [!NOTE]
>
> **`me.write` is on neither list, and that is deliberate.** This table is what a RomMBat
> **device** asks for. Approving the request is the other half of the flow and needs
> `me.write`, because `POST /api/auth/device/approve` requires it. Approving in the web UI
> uses your logged-in session, so there is nothing extra to grant and this never comes up.
> It only matters if you drive approval with an API token, which is a developer concern:
> see [DEVELOPER_SETUP.md](DEVELOPER_SETUP.md) section 3.

Granting less than RomMBat asks for is supported: it reads the granted set back and
degrades by feature, telling you what is off, rather than throwing errors at you later.

> [!NOTE]
>
> On a portable install, **the token at rest is only as protected as the drive.** Windows
> DPAPI is not usable here: it binds the ciphertext to one machine or one user profile,
> which would make the drive undecryptable on the next PC. RomMBat defaults portable
> installs to a scoped, expiring token and makes re-pairing cheap. `rommbat-agent pair
--protect` adds AES-GCM under a passphrase you type, at the cost of unattended syncing:
> nothing can decrypt the token without you typing it again. See [SECURITY.md](SECURITY.md).

### Pairing from the console

Until the gamepad UI lands in M7, pairing runs from the agent, QR included:

```powershell
emulators\rommbat\rommbat-agent.exe pair --server https://your-romm-instance
emulators\rommbat\rommbat-agent.exe status
```

The server URL is the only thing you type, and it is remembered afterwards. Scan the QR or
enter the 8-character code in the RomM web UI. The code lasts 10 minutes; press **R** for a
new one, **Q** to quit. `status --offline` skips the reachability probe and answers entirely
from local state.

### Syncing content

```powershell
rommbat-agent.exe sets add "snes favourites" --scope platform --value snes --max-games 40 --max-bytes 8GB
rommbat-agent.exe budget --max 64GB          # how much of this drive RomMBat may use
rommbat-agent.exe sync --dry-run             # what it would fetch, and what it would not
rommbat-agent.exe sync                       # fetch it
rommbat-agent.exe evict                      # what would go to get back inside the budget
rommbat-agent.exe evict --apply              # actually remove it
rommbat-agent.exe gamelist                   # rewrite gamelist.xml from local state
rommbat-agent.exe gamelist --media all       # also fetch manuals, which are off by default
```

### Saves and playtime

```powershell
rommbat-agent.exe saves                      # what is on disk, what went up, what cannot
rommbat-agent.exe saves resolve 42 "ppsspp:savedata" --keep-local  # pick a side on a conflict
rommbat-agent.exe saves bind psp ULUS10057 391                     # whose directory save is this
rommbat-agent.exe saves bind psp ULUS10057 --forget                # work it out again from scratch
rommbat-agent.exe flush                      # send queued saves and play sessions
rommbat-agent.exe flush --offline            # do the local half only
rommbat-agent.exe hooks status               # are the EmulationStation hooks installed
rommbat-agent.exe hooks uninstall            # take them back out
```

`sync` installs the hooks on its first run, naming every file it adds, and flushes at the
end, so neither is normally typed. Without them there is no playtime and no way to tell which
game wrote a save.

**This release syncs battery saves, save states, directory saves and play sessions.** A
directory save such as PPSSPP's `SAVEDATA/` goes up as one archive and comes back down as one.
Shared containers, where a single file holds every game on the system as a PS2 memory card
does, are the last shape and land in the next release. `saves` lists everything it found that
it is not syncing, and why, rather than leaving you to notice.

**A directory save is attributed, not named.** It is keyed by a Game ID (`ULUS10057`, a PS3
title id, a GameCube disc id) and RomM stores no serial, title id or product code anywhere, so
RomMBat works the game out from the launch journal, from the ROM header, or from the sidecar
RetroBat writes beside a save state. Two routes naming different games bind nothing and
reports both candidates, because guessing uploads one game's save under another's name and the
cache would then make that permanent. `saves bind` settles one by hand, or clears one that is
wrong.

`sync` re-resolves each set first, because smart-collection membership drifts server-side,
then prints a plan before doing anything. A second run of an unchanged set downloads nothing
and says so. `sync --dry-run` and `sync --offline` both work with the server unreachable,
answering from what the store already holds.

`sync` then fetches artwork and writes one `gamelist.xml` per RetroBat folder, keyed by the
folder rather than by the platform because two RomM platforms can share one. It merges into
what is already there, so anything EmulationStation or a user's own scraper wrote survives,
and it asks EmulationStation to reload only when something actually changed. `gamelist` does
the same thing on its own and needs no server at all.

**Media is not a rounding error.** At the sizes measured on a real library a game costs about
3.1 MB of cover, thumbnail, marquee and video, so a hundred-game NES set is roughly 12.8 MB of
ROMs and 320 MB of artwork. It counts against the same budget, and manuals are opt-in.

**Nothing is deleted without `evict --apply`.** Eviction never removes a file RomMBat did not
download, and never one whose saves have not reached the server. It takes a game's artwork and
its gamelist entry out with it, and leaves artwork a user scraped themselves alone.

Two things are skipped on purpose and reported rather than hidden. A ROM RomM holds as
several files (a `.bin`/`.cue` set, most Xbox 360 titles) is not synced in v1: the server
serves it as an archive that cannot be resumed and whose hashes describe neither the archive
nor its contents. And on a FAT32 drive, anything over 4 GB is left out before the download
starts, because the write would otherwise fail with an error message about disk space on a
drive with plenty free.

## Status

RomMBat is built in milestones, and platforms are certified one at a time after the
framework works end to end.

| Milestone | Scope                                                                                                               | State                                                                                                          |
| --------- | ------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| M0        | Probes against a real RetroBat install; findings recorded in [docs/retrobat-findings.md](docs/retrobat-findings.md) | **Complete.** All seven answered, against an 83,131 rom library and two PCs                                    |
| M1        | Device pairing, portable identity, SQLite schema and outbox                                                         | **Complete.** `rommbat-agent pair` and `status` work; nothing syncs yet                                        |
| M2        | Paged catalog browsing, sync sets, platform mapping                                                                 | **Complete.** `sets` and `platforms` resolve against a live 123-platform library; nothing downloads yet        |
| M3        | Content sync, resumable downloads, disk budget and eviction                                                         | **Complete.** `sync`, `budget` and `evict` work; resume and verification proven against a live instance        |
| M4        | `gamelist.xml` generation, metadata and media                                                                       | **Complete.** `sync` writes merged gamelists and fetches artwork; conversions measured against a live instance |
| M5        | BIOS and firmware                                                                                                   | **Complete.** `sync` fetches BIOS before ROMs and `bios` reports the gap, offline included                     |
| M6        | Offline-first save, state and playtime sync                                                                         | **In progress**, in three stages. See below                                                                    |
| M7        | Gamepad UI (framework choice deferred to this milestone)                                                            | Not started                                                                                                    |
| M8        | Packaging, docs, release                                                                                            | Not started                                                                                                    |

M6 is the one milestone where a missed detail loses a save rather than a download, so it
ships in stages small enough to review. The first cut is at the save-class boundary; the
second is at what each remaining piece needs from Game-ID attribution, which is the only hard
dependency among them.

| Stage | Scope                                                                                          | State                                                                                                                     |
| ----- | ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| 1     | ES hooks, the journal, play sessions, class A and B battery saves, the full negotiate protocol | **Complete.** Three games played offline, one flush, everything lands                                                     |
| 2a    | Save states across all 13 emulators, and conflict resolution                                   | **Complete.** States are pushed one way; `saves resolve` picks a side and prunes the copy kept aside                      |
| 2b    | Game-ID attribution, class C directory saves bundled to one archive                            | **Complete.** A PPSSPP `SAVEDATA/` directory went up, came back as a conflict, and the game loaded what the restore wrote |
| 2c    | Class D conversion and the per-game `es_settings.cfg` writer                                   | Not started                                                                                                               |

**Save states are pushed, never pulled.** `POST /api/states` has no slot, no device and no
conflict detection, so there is nothing to negotiate: a state goes up when its contents change
and does not come back down. Anything RomMBat cannot sync is reported by `saves` with the
reason rather than passed over in silence.

### Known upstream issues

M0 found three RetroBat bugs rather than facts to design around. All are open, all are
worked around, and each is re-checked every release, because a fix upstream changes what
RomMBat should do rather than just closing a ticket.

| Issue                                                                                      | What it costs                                                                       |
| ------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------- |
| [retrobat#249](https://github.com/RetroBat-Official/retrobat/issues/249)                   | ES event scripts do not run once an argument is quoted, so **hooks must be `.exe`** |
| [emulatorlauncher#1336](https://github.com/RetroBat-Official/emulatorlauncher/issues/1336) | Flycast writes save states to a different directory than the one declared           |
| [emulatorlauncher#1337](https://github.com/RetroBat-Official/emulatorlauncher/issues/1337) | BizHawk crashes unless the launcher is passed `-core`                               |

### Platform certification

A platform counts as supported only after a nine-point checklist passes against a real
install, recorded in `docs/platforms/<system>.md`.

**The unit is `(system, emulator, core)`, not the system.** Two emulators for one console do
not behave alike, and the difference lands exactly where it hurts: `psx` under libretro writes
a plain `.srm` and is class A, while `psx` under DuckStation writes a memory card named from an
internal database title and needs Game-ID attribution. Save-state directories and filenames are
per emulator, and `libretro` and `bizhawk` are core-scoped on top of that, so one game under
two cores has independent state sets. So "snes is certified" is not a claim; "`snes` under
`libretro`/`snes9x` is certified" is, and it says nothing about `snes` under `bizhawk`. Expect
the table below to be two to four passes per row rather than one.

**Certification starts after M7.** Every pass needs a person at the machine launching real
games, and doing that through a terminal rather than the gamepad UI makes a long job longer.
The waves then finish against an M8 package, which is what a user would actually install.

Two things do not wait for that. Each M6 stage owes one hands-on check of the save shape it
added, because that is where being wrong destroys data rather than costing a re-download; and
the automated suite already drives the whole protocol, offline included, against a stub server.

| Wave | Systems                                                                                      | Status                |
| ---- | -------------------------------------------------------------------------------------------- | --------------------- |
| 1    | `nes`, `snes`, `gb`, `gbc`, `gba`, `megadrive`, `mastersystem`                               | Not started, after M7 |
| 2    | `n64`, `psx`, `saturn`, `segacd`, `pcengine`, `pcenginecd`                                   | Not started, after M7 |
| 3    | `ps2`, `gamecube`, `dreamcast`, `xbox`                                                       | Not started, after M7 |
| 4    | `neogeo`, `neogeocd`, `fbneo`                                                                | Not started, after M7 |
| 5    | `wonderswan`, `wonderswancolor`, `ngp`, `ngpc`, `lynx`, `gamegear`, `atari2600`, `atari7800` | Not started, after M7 |

### Compatibility

Every release names the RomM and RetroBat versions it was tested against. Adding a row
here is part of shipping.

| RomMBat    | RomM tested         | RetroBat tested    | Notes                                                               |
| ---------- | ------------------- | ------------------ | ------------------------------------------------------------------- |
| unreleased | 5.1.0, 5.1.1-beta.1 | 8.2.0-stable-win64 | API DTOs are generated from a pinned RomM **5.1.0** `/openapi.json` |

The pinned schema is the minimum supported version on purpose, so the generated DTOs
describe the oldest server the client claims to work with. Moving the pin is a compatibility
decision and moves a row in this table with it; see
[`src/RomM.Client/openapi/README.md`](src/RomM.Client/openapi/README.md).

## Repository layout

```text
src/RomM.Client       API client. DTOs generated from /openapi.json, plus hand-written
                      pairing, resumable download and sync negotiation
src/RomM.Client/openapi
                      The pinned schema, the generator config, and why the pin is where
                      it is. Generated output is committed under Generated/
src/RomMBat.Core      Local state and everything that knows RetroBat's disk layout
src/RomMBat.Agent     Console exe: pair, sync, game-start, game-end, flush, status
src/RomMBat.UI        Gamepad-navigable front end (framework chosen in M7)
tests/RomMBat.Tests   xUnit

docs/PLAN.md          The design of record. Read this before anything else
docs/retrobat-findings.md
                      What a real RetroBat install actually does, measured, plus the
                      contradiction table naming every place the plan was wrong
docs/ARCHITECTURE.md  Project layout, sync state machine, local schema
docs/platforms/       One certification record per RetroBat system
reference/            Vendored upstream data plus a script that re-derives every number
data/retrobat/        Bundled mapping tables (platforms, save directories, save shapes)
tools/m*-probes/      Throwaway probes, one folder per milestone, kept so every measured
                      number is reproducible
.claude/skills/       Task-scoped guides for agents working in this repository
```

## Building

```bash
dotnet build
dotnet test
dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true

trunk fmt && trunk check        # lint, from WSL on Windows
cd reference && ./refresh.sh    # refresh upstream data, verify, check generated data
```

Trunk has no Windows-native CLI, so run it under WSL. [DEVELOPER_SETUP.md](DEVELOPER_SETUP.md)
gives the exact command and a fallback for docs-only changes.

Full setup, including how to point at a RomM instance and stand up a throwaway RetroBat,
is in [DEVELOPER_SETUP.md](DEVELOPER_SETUP.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

> [!IMPORTANT]
>
> **RomMBat is developed primarily with Claude Code, and AI assistance must be disclosed
> in every pull request.** This norm comes from RomM and RomMBat inherits it. It matters
> more here, not less.

## Related projects

| Project                                                       | What it is                                            |
| ------------------------------------------------------------- | ----------------------------------------------------- |
| [RomM](https://github.com/rommapp/romm)                       | The self-hosted ROM manager RomMBat syncs against     |
| [RetroBat](https://github.com/RetroBat-Official/retrobat)     | The Windows retro-gaming distro RomMBat installs into |
| [Grout](https://github.com/rommapp/grout)                     | RomM client for Linux handheld custom firmware        |
| [Playnite plugin](https://github.com/rommapp/playnite-plugin) | RomM client for Playnite on desktop                   |

## Licence

[GPL-3.0](LICENSE), matching the Playnite plugin and Argosy.

RomMBat is not affiliated with either project's maintainers. It ships no ROMs, no BIOS
files and no copyrighted content; it moves files between a server you run and a device
you own.
