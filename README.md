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
> BIOS all work. **All four save shapes now cross, plus save states and playtime**, along with
> conflict resolution and the Game-ID attribution that directory saves need. A shared container
> such as a PS2 memory card crosses **only for a game you opt in** with `saves convert`, one
> game at a time; anything still genuinely shared is reported with the reason rather than
> passed over. A device that has never held a **directory** save still cannot receive one.
> No platform has been certified against a real emulator; see
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
   The `game-start` and `game-end` hooks sit in the game launch path, so they append to a
   durable local journal, exit, and start nothing. Measured: they do not block the launch,
   the emulator starts about 30 ms later regardless, but they do run **concurrently**, so
   the journal has to survive interleaved writers. The `start` and `quit` hooks are outside
   that path and each starts a short-lived agent pass, which is what flushes the outbox when
   the server is reachable and is why an install nobody administers from a terminal works.
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
| RetroBat | 8.2.1       | Checked from `system/version.info` at startup                    |
| RomM     | 5.2.0       | Checked from `GET /api/heartbeat` at startup                     |
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

### Pairing

Open **RomMBat** from the EmulationStation menu and choose **Pair with RomM** from the footer.
The address is typed on an on-screen keyboard, the QR is on screen to scan with a phone, and
the code is there to read aloud if you would rather type it. Nothing in that flow needs a
keyboard or a mouse.

The footer draws each action's button as a **position** rather than a letter, the way
EmulationStation does, because the bottom face button is A on an Xbox pad, Cross on a
DualSense and B on a Switch Pro. RomMBat uses whatever your `es_input.cfg` says, so the
button that works here is the one that works in EmulationStation.

It is also still a console command, which is what a headless or scripted install uses:

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
rommbat-agent.exe saves convert 191723                             # what converting this game would do
rommbat-agent.exe saves convert 191723 --apply                     # give it its own memory card
rommbat-agent.exe saves convert 191723 --at-quit                   # make the change when ES next closes
rommbat-agent.exe saves convert 191723 --revert                    # put the setting back, or call off a queued change
rommbat-agent.exe flush                      # send queued saves and play sessions
rommbat-agent.exe flush --offline            # do the local half only
rommbat-agent.exe hooks status               # are the EmulationStation hooks installed
rommbat-agent.exe hooks uninstall            # take them back out
rommbat-agent.exe menu status                # is RomMBat in the EmulationStation menu
rommbat-agent.exe menu uninstall             # take the entry back out
```

`sync` installs the hooks **and the EmulationStation menu entry** on its first run, naming
every file it adds, and flushes at the end, so none of it is normally typed. Without the
hooks there is no playtime and no way to tell which game wrote a save.

**Nothing above has to be typed at all on an ordinary install.** The `start` and `quit` hooks
each start a background pass, so saves and play sessions go up when EmulationStation opens and
when it closes. `game-start` and `game-end` start nothing: those two run inside the game-launch
path, write one line to a local journal, and exit.

**`saves convert` needs EmulationStation closed, or `--at-quit`.** EmulationStation loads its
settings at startup and writes that copy back over anything changed underneath it, so a change
made while it is running is discarded without saying so. `--at-quit` records the change and it
is made the next time you close EmulationStation, which is also the only way RomMBat's own
screen can change it, since that screen is opened from the EmulationStation menu.

**This release syncs battery saves, save states, directory saves, shared containers you opt
in, and play sessions.** A directory save such as PPSSPP's `SAVEDATA/` goes up as one archive
and comes back down as one. `saves` lists everything it found that it is not syncing, and why,
rather than leaving you to notice.

**A shared container has no game to belong to, so RomMBat offers to split it, one game at a
time.** A stock PS2 memory card holds every game you have played on it: the one measured while
building this held saves for **11 different games**, which is why none of them can be attributed
or synced. `saves convert` writes a per-game override into `es_settings.cfg` so PCSX2 gives one
game its own card, named after the ROM, which then syncs like any other save.

It previews by default and writes on `--apply`, because it changes your RetroBat configuration:

- **The game starts from an empty card.** What it saved before stays in the shared one, where
  it will no longer look. RomMBat does not move it, and says so before you agree. `--revert`
  puts the setting back exactly, including putting it back to _absent_ if that is what it was.
- **Multi-disc games are refused.** PCSX2 cannot bind discs, so each disc would get its own
  card and the save would vanish at the disc change that the shared card carries through.
- **PS1 is deliberately left alone.** DuckStation's stock mode already binds a disc set through
  its own database, and converting it is the change that would break one.
- **It refuses while EmulationStation is running**, because ES rewrites `es_settings.cfg` from
  the copy it loaded at startup and would discard the change without saying so.
- Per-game cards also break games that deliberately read a prequel's save from the same card.

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

`evict` also reports transfers that died part-way, under `emulators/rommbat/partial/`, and
reclaims them on `--apply`. Those bytes are the only ones the disk budget cannot see, because a
file is only counted once it has arrived whole. It does not run at all while a sync is writing
saves back, and it skips anything a live transfer still holds open.

Two things are skipped on purpose and reported rather than hidden. A ROM RomM holds as
several files (a `.bin`/`.cue` set, most Xbox 360 titles) is not synced in v1: the server
serves it as an archive that cannot be resumed and whose hashes describe neither the archive
nor its contents. And on a FAT32 drive, anything over 4 GB is left out before the download
starts, because the write would otherwise fail with an error message about disk space on a
drive with plenty free.

## Status

RomMBat is built in milestones, and platforms are certified one at a time after the
framework works end to end.

| Milestone | Scope                                                                                                               | State                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| --------- | ------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| M0        | Probes against a real RetroBat install; findings recorded in [docs/retrobat-findings.md](docs/retrobat-findings.md) | **Complete.** All seven answered, against an 83,131 rom library and two PCs                                                                                                                                                                                                                                                                                                                                                                                   |
| M1        | Device pairing, portable identity, SQLite schema and outbox                                                         | **Complete.** `rommbat-agent pair` and `status` work; nothing syncs yet                                                                                                                                                                                                                                                                                                                                                                                       |
| M2        | Paged catalog browsing, sync sets, platform mapping                                                                 | **Complete.** `sets` and `platforms` resolve against a live 123-platform library; nothing downloads yet                                                                                                                                                                                                                                                                                                                                                       |
| M3        | Content sync, resumable downloads, disk budget and eviction                                                         | **Complete.** `sync`, `budget` and `evict` work; resume and verification proven against a live instance                                                                                                                                                                                                                                                                                                                                                       |
| M4        | `gamelist.xml` generation, metadata and media                                                                       | **Complete.** `sync` writes merged gamelists and fetches artwork; conversions measured against a live instance                                                                                                                                                                                                                                                                                                                                                |
| M5        | BIOS and firmware                                                                                                   | **Complete.** `sync` fetches BIOS before ROMs and `bios` reports the gap, offline included                                                                                                                                                                                                                                                                                                                                                                    |
| M6        | Offline-first save, state and playtime sync                                                                         | **Complete.** All four save shapes proven, the last of them on hardware. See below                                                                                                                                                                                                                                                                                                                                                                            |
| M7        | Closing the EmulationStation loop, then the gamepad UI                                                              | **Stages 7a, 7b-1 and 7b-2a complete.** Hooks start the sync passes, RomMBat is in the ES menu, and the menu entry opens a real full-screen interface: pairing, status, and now the sync sets themselves. From a controller you can define what this device should hold, edit its limits, resolve it against RomM and set the disk budget. **Downloading is still a terminal command** until 7b-2b; browse and per-game install are 7b-2c, conflicts are 7b-3 |
| M8        | Packaging, docs, release                                                                                            | Not started                                                                                                                                                                                                                                                                                                                                                                                                                                                   |

M6 is the one milestone where a missed detail loses a save rather than a download, so it
ships in stages small enough to review. The first cut is at the save-class boundary; the
second is at what each remaining piece needs from Game-ID attribution, which is the only hard
dependency among them.

| Stage | Scope                                                                                          | State                                                                                                                     |
| ----- | ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| 1     | ES hooks, the journal, play sessions, class A and B battery saves, the full negotiate protocol | **Complete.** Three games played offline, one flush, everything lands                                                     |
| 2a    | Save states across all 13 emulators, and conflict resolution                                   | **Complete.** States are pushed one way; `saves resolve` picks a side and prunes the copy kept aside                      |
| 2b    | Game-ID attribution, class C directory saves bundled to one archive                            | **Complete.** A PPSSPP `SAVEDATA/` directory went up, came back as a conflict, and the game loaded what the restore wrote |
| 2c    | Class D conversion and the per-game `es_settings.cfg` writer                                   | **Complete.** A PS2 game opted into a per-game memory card, written by the game, synced, and loaded back                  |

**M6's four save shapes, and what proved each.** The milestone asks for one game from each
shape rather than three of the easy one, so the evidence is listed per shape rather than
summarised. **A shape proven by a test and not by an emulator is named as such.**

| Shape                          | Proved by                                                                                            |
| ------------------------------ | ---------------------------------------------------------------------------------------------------- |
| A, one file per game           | A RetroArch `.srm`, played offline and flushed on reconnect (stage 1)                                |
| B, several files per game      | Saturn's `.bcr` and `.bkr` in their own slots (stage 1). **Tests only; no Saturn game was launched** |
| C, a directory per game        | A PPSSPP `SAVEDATA/` directory, up as one archive, back as a conflict, resolved and loaded (2b)      |
| D, a container shared by games | A PS2 card converted per game, written by Armored Core 3, synced, and **loaded back by PCSX2** (2c)  |

Alongside those: a save state with its screenshot across four emulators (2a), a conflict a
person resolves (2a), and play sessions reaching RomM from the ES hooks (stage 1).

**What is not claimed.** Nothing is certified: certification is per `(system, emulator, core)`
and needs all nine steps of the checklist, and the wave rollout starts after M7. Class D was
driven on `(ps2, pcsx2)` only; Dreamcast and PS1 are reported with their measured reasons and
deliberately not converted. And a converted card has never been **downloaded** onto a second
real device, only onto a test one. See
[docs/platforms/README.md](docs/platforms/README.md) for the per-stage records, gaps included.

**Save states are pushed, never pulled.** `POST /api/states` has no slot, no device and no
conflict detection, so there is nothing to negotiate: a state goes up when its contents change
and does not come back down. Anything RomMBat cannot sync is reported by `saves` with the
reason rather than passed over in silence.

### Known upstream issues

M0 filed three RetroBat bugs rather than facts to design around. Two are now resolved and
one is still open. Each is re-checked every release, because a fix upstream changes what
RomMBat should do rather than just closing a ticket. A workaround comes out only once the fix
is in a release the compatibility gate accepts **and** a hands-on pass has seen the fixed
behaviour: a changelog line is what upstream believes, not what lands on disk.

| Issue                                                                                                     | State                       | What it costs                                                                       |
| --------------------------------------------------------------------------------------------------------- | --------------------------- | ----------------------------------------------------------------------------------- |
| [batocera-emulationstation#2196](https://github.com/batocera-linux/batocera-emulationstation/issues/2196) | Open                        | ES event scripts do not run once an argument is quoted, so **hooks must be `.exe`** |
| [emulatorlauncher#1336](https://github.com/RetroBat-Official/emulatorlauncher/issues/1336)                | **Fixed in RetroBat 8.2.1** | Flycast wrote save states to a different directory than the one declared            |
| [emulatorlauncher#1337](https://github.com/RetroBat-Official/emulatorlauncher/issues/1337)                | Closed, will not be fixed   | BizHawk crashes unless the launcher is passed `-core`                               |

**#2196 moved repository, not status.** It was filed as `RetroBat-Official/retrobat#249` and
closed there on 2026-08-21 as an EmulationStation issue; RetroBat's own ES fork has issues
disabled, so it now lives upstream at `batocera-linux/batocera-emulationstation`. The
mechanism, the two verified fixes and the `.exe` hook consequence are unchanged. **This is
the one that still constrains the design**: the hooks stay `.exe`.

**#1336 is fixed, and the workaround is out.** 8.2.1 pointed Flycast's save-state watcher at
the directory Flycast actually writes, so a state is mirrored into the declared
`saves/<system>/flycast/sstates`. Confirmed by hand rather than taken from the changelog:
three runs of a real Dreamcast game on a real 8.2.1 install put the state in both places, same
bytes, same millisecond, while the emulator was still running. Dreamcast states now sync from
the declared directory like any other emulator's. `openmsx` is still wrong and still reported.

**#1337 will not be fixed, and that costs RomMBat nothing.** Upstream's position is that
there is no reason to run `emulatorLauncher` directly. RomMBat is a direct invoker, so the
constraint stands unchanged and is not a workaround for a bug: **pass `-core`**, which is
correct either way.

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

**Certification starts after M7 stage 7b.** Every pass needs a person at the machine launching real
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

| RomMBat    | RomM tested | RetroBat tested    | Notes                                                               |
| ---------- | ----------- | ------------------ | ------------------------------------------------------------------- |
| unreleased | 5.2.0       | 8.2.1-stable-win64 | API DTOs are generated from a pinned RomM **5.2.0** `/openapi.json` |

The pinned schema is the minimum supported version on purpose, so the generated DTOs
describe the oldest server the client claims to work with. Moving the pin is a compatibility
decision and moves a row in this table with it; see
[`src/RomM.Client/openapi/README.md`](src/RomM.Client/openapi/README.md).

**Both minimums track the newest upstream stable rather than the oldest version that works.**
Every measured rule in this repository is a measurement of one build, so a supported range
means owning that measurement across the range, on a `(system, emulator, core)` matrix that is
already several passes per row. RomMBat adopts a new RomM or RetroBat stable within one release
and raises the floor with it. Earlier rows in this table stay accurate about what was tested;
they are not a support commitment.

## Repository layout

```text
src/RomM.Client       API client. DTOs generated from /openapi.json, plus hand-written
                      pairing, resumable download and sync negotiation
src/RomM.Client/openapi
                      The pinned schema, the generator config, and why the pin is where
                      it is. Generated output is committed under Generated/
src/RomMBat.Core      Local state and everything that knows RetroBat's disk layout
src/RomMBat.Agent     Console exe: pair, sync, game-start, game-end, flush, status
src/RomMBat.UI        Gamepad-navigable front end (Avalonia, Win32 + Skia)
tests/RomMBat.Tests   xUnit, over Core and Client
tests/RomMBat.Agent.Tests
                      xUnit, over the Agent's subcommands and their gates

docs/PLAN.md          The design of record. Read this before anything else
docs/retrobat-findings.md
                      What a real RetroBat install actually does, measured, plus the
                      contradiction table naming every place the plan was wrong
docs/{freegosy,argosy}-findings.md
                      One ledger per mined reference implementation, recording what
                      survived verification and what did not. Both are closed
docs/ARCHITECTURE.md  Project layout, sync state machine, local schema
docs/platforms/       One certification record per RetroBat system
reference/            Vendored upstream data plus a script that re-derives every number
data/retrobat/        Bundled mapping tables (platforms, save directories, save shapes)
data/media/           The ES menu entry's artwork, embedded into RomMBat.Core
tools/m*-probes/      Throwaway probes, one folder per milestone, kept so every measured
                      number is reproducible
tools/{freegosy,argosy}-probes/
                      The same, for the probes that verified a mined reference
                      implementation's claims
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

| Project                                                   | What it is                                            |
| --------------------------------------------------------- | ----------------------------------------------------- |
| [RomM](https://github.com/rommapp/romm)                   | The self-hosted ROM manager RomMBat syncs against     |
| [RetroBat](https://github.com/RetroBat-Official/retrobat) | The Windows retro-gaming distro RomMBat installs into |

## Licence

[GPL-3.0](LICENSE), matching the RomM Playnite plugin and Argosy.

RomMBat is not affiliated with either project's maintainers. It ships no ROMs, no BIOS
files and no copyrighted content; it moves files between a server you run and a device
you own.
