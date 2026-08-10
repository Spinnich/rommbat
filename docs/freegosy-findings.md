# Freegosy findings

What was taken from reading [`abduznik/Freegosy`](https://github.com/abduznik/Freegosy), what
survived verification against a primary source, and what did not. Written in the shape of
[retrobat-findings.md](retrobat-findings.md), which is the model for how this repository
records a measurement.

**Freegosy is a hypothesis generator, never evidence.** Nothing here is a fact because
Freegosy does it, says it, or tests it. Every row carries the route that settled it, and the
rows that no route settled are labelled open rather than quietly promoted.

|                    |                                                                    |
| ------------------ | ------------------------------------------------------------------ |
| Source read        | `abduznik/Freegosy` at `8a76f69`, v0.5.11, Dart/Flutter, MIT       |
| Source targets     | RomM 4.9, standalone desktop emulators, EmuDeck, RetroDECK         |
| RomM under test    | `5.1.1-beta.1`, read from `GET /api/heartbeat` -> `SYSTEM.VERSION` |
| Schema cross-check | `src/RomM.Client/openapi/romm-5.1.0.json`, the pinned minimum      |
| RetroBat           | `8.2.0-stable-win64`                                               |
| Date               | 2026-08-10                                                         |

The instance host is redacted throughout, per the repo rules.

## The honest total

35 candidates read, 13 dropped at triage, 22 probed. Of those: **11 confirmed, 4 rejected,
1 corrected, 1 partly settled, 5 left open.** Four further traps turned up while running the
probes and none of them was on the list.

**The one that would have cost real data** is F1: `GET /api/saves/{id}/content` marks the
device as current on the request, not on receipt, unless `optimistic=false` is passed. On a
handheld that drops Wi-Fi mid-download, the server then believes the device holds a save it
does not, and every later negotiate answers `no_op`. Freegosy passes `optimistic=true`
explicitly, so it did not find this; the parameter simply led us to look.

**The one that corrects something already written down as measured** is F15: `docs/PLAN.md`
finding 83 claimed multi-file and an empty `fs_extension` were equivalent both ways. Only one
direction holds. The code never relied on the wrong half.

**Freegosy itself was wrong about four things** at 5.1.x, which is the whole argument for the
bar this session was held to: its play-session payload is a 422, its documented 409 body does
not exist, its per-device isolation model is not what the server does, and its curated BIOS
hashes add nothing our manifest lacks.

## Why this source needed a higher bar than the last three

Grout, Argosy and the Playnite plugin sit under `rommapp`, track the server closely, and
were mined as trustworthy about the API. Freegosy is none of those things:

1. It targets **RomM 4.9**. Our baseline is 5.1.0 and the instance measured here is
   5.1.1-beta.1, so every API claim it makes is a claim about a server two minor versions
   behind ours.
2. It is **v0.5.x with one maintainer**, outside the `rommapp` org.
3. It targets **standalone desktop emulators, EmuDeck and RetroDECK**. RetroBat is none of
   those, so **no path from Freegosy is valid here**. Save shapes may transfer; save
   locations never do, and none was copied.
4. Its **tests and its mock server encode its beliefs**, not the server's behaviour.
   `test/mock_romm_server.py` looks like a specification of the RomM API and is a
   specification of what Freegosy expects. It is cited for nothing.

## Verification routes

Each claim graduates by exactly one route, recorded next to it.

| Route         | What it means                                                                            |
| ------------- | ---------------------------------------------------------------------------------------- |
| **live**      | A request made against the live instance, with the request and response quoted           |
| **probe**     | A probe against a real RetroBat install, driven far enough to see the file               |
| **reference** | Vendored under `reference/` and re-derived by `verify.py`, never a number typed by hand  |
| **reasoned**  | An argument from first principles plus a test that fails if it is wrong. Not measured    |
| **open**      | Recorded, not settled. May not enter `docs/PLAN.md` as fact                              |
| **dropped**   | Cut at triage because nothing in RomMBat would change if it were true. Never got a probe |

The pinned schema is used to **screen** candidates, never to settle them. A parameter
declared in `romm-5.1.0.json` is evidence that the parameter exists, not evidence of what
the server does with it.

---

## Triage

35 candidates came out of the read. 22 survived triage, 13 were cut. The cuts are recorded
below with their reasons, because the whole point of writing them down is that nobody mines
this repository again in six months and re-walks the same dead ends.

### Survivors, ranked by what would change

| #   | Claim                                                                                                                           | Touches                                         | Would hold here if                                                                     | Cheapest experiment                                                                       | Verdict             |
| --- | ------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------- | -------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- | ------------------- |
| F1  | `GET /api/saves/{id}/content` takes `optimistic`, **defaulting to true**, which records the device sync before the bytes land   | M6 download step, `romm-api`, `save-sync`       | The server marks the device current on request rather than on the client confirming    | Download a save with the default and with `optimistic=false`, read `last_synced_at` back  | **live, confirmed** |
| F2  | `POST /api/saves` takes `autocleanup` and `autocleanup_limit` (default 10), pruning old saves in the slot server-side           | M6 upload, `romm-api`, `save-sync`              | The server actually deletes; RomMBat has no save-retention story at all today          | Upload N+1 saves into one slot with `autocleanup=true&autocleanup_limit=N`, list the slot | **live, confirmed** |
| F3  | The server dedups identical uploads within a slot, which is what makes our replay-safe flush safe                               | Core principle 1, M6, `offline-and-portable`    | Two identical uploads produce one row. Freegosy busts this deliberately, so it may not | Upload byte-identical content to the same slot twice, count the rows                      | **live, confirmed** |
| F4  | `POST /api/play-sessions` is a standalone ingest taking `{device_id, sessions:[...]}`, not only a field on `/complete`          | M6 flush, `romm-api`, `offline-and-portable`    | Sessions can be flushed without opening a sync session                                 | Post one session through the standalone route and read it back                            | **live, confirmed** |
| F5  | `GET /api/platforms` inlines the full `firmware[]` array with `md5_hash` per platform                                           | M5, `romm-api`, possibly the M2 scale guardrail | The join RomMBat needs is one request, not one per platform                            | Fetch `/api/platforms`, count firmware records and md5s, measure bytes                    | **live, confirmed** |
| F6  | The server's `[YYYY-MM-DD_HH-MM-SS]` filename tag has to be **stripped before writing to disk**, or the emulator cannot find it | M6 restore, `save-sync`                         | RetroBat's emulators locate saves by rom-name match, which they do                     | Upload a save, read the returned `file_name`, check the tag shape and any `-N` suffix     | **live, confirmed** |
| F7  | RomM 4.9+ **isolates** saves per device (Freegosy's framing), rather than merely tracking per device                            | M6 conflict handling, `save-sync`               | A save uploaded by device A is invisible or subordinate to device B                    | List saves for one rom with two different `device_id` values and diff the sets            | **live, rejected**  |
| F8  | `device_syncs[].is_current` answers "does the server have something newer" without a negotiate                                  | M6, `romm-api`                                  | The flag is server-computed per device and trustworthy                                 | Read `GET /api/saves?rom_id=&device_id=` before and after an upload from another device   | **live, corrected** |
| F9  | `SaveSchema.origin_device_id` names the device that produced a save                                                             | M6 conflict handling, `save-sync`               | It is populated on upload, so a client can tell its own save from a peer's             | Read it back off a save uploaded with `device_id` set                                     | **live, confirmed** |
| F10 | `POST /api/saves/{id}/track` and `/untrack` opt a single save out of syncing for one device                                     | M6, `romm-api`                                  | `is_untracked` then changes what negotiate returns                                     | Untrack a save, re-negotiate, see whether the operation disappears                        | **live, confirmed** |
| F11 | `GET /api/saves/summary?rom_id=` returns per-slot counts and the latest save per slot                                           | M6, `romm-api`                                  | It is cheap enough to replace listing every save                                       | Call it against a rom with saves and compare against `GET /api/saves`                     | **live, confirmed** |
| F12 | A 409 on upload carries a structured body with `save_id`, `current_save_time` and `device_sync_time`                            | M6, `romm-api`, `save-sync`                     | The body is actionable, so a conflict can be shown without a second request            | Force a 409 and quote the body                                                            | **live, rejected**  |
| F13 | `GET /api/saves/identifiers` takes no parameters, the same shape that made `/api/roms/identifiers` 504                          | M6 reconcile, `romm-api`                        | Saves are few enough that it answers, unlike the roms sibling                          | Time the call against the live library                                                    | **open**            |
| F14 | `/api/roms` **silently ignores** an unknown query parameter, so `platform_id` resolves the whole library                        | M2 set resolution, `romm-api`                   | The server does not reject unknown params, which FastAPI does not by default           | Compare `platform_id=` against `platform_ids=` on the same platform, read `total`         | **live, confirmed** |
| F15 | A ROM can carry exactly one file and an empty `fs_extension`, which finding 83 treats as the multi-file marker                  | M3 exclusion state and its message, `romm-api`  | Such rows exist in a real library                                                      | Scan a sample of `/api/roms?with_files=true` for `len(files)==1 and fs_extension==''`     | **live, confirmed** |
| F16 | A multi-disc set is one multi-file ROM whose `files[]` includes a `.m3u` plus non-launchable `.cue`/`.ccd`/`.mds`/`.toc`        | M3 seam, the later multi-file milestone         | Real multi-disc rows look like that on this instance                                   | Scan the same sample for `.m3u` members and tally the sibling extensions                  | **live, rejected**  |
| F17 | The GameCube/Wii game ID is 4 ASCII bytes at offset `0x00` in an `.iso` and `0x58` in an `.rvz`                                 | M6 attribution fallback, `save-sync`            | The offsets are right for the containers RetroBat accepts                              | Read the header of a real `.iso` and a real `.rvz`                                        | **open**            |
| F18 | A multi-disc `.m3u` filename carries region tags the save file does not, so save matching needs tag stripping                   | M6 attribution, `save-sync`                     | RetroBat's emulators name per-game saves from the disc, not the playlist               | Probe 2 rerun on a multi-disc PS1 title                                                   | **open**            |
| F19 | Save-shape hypotheses for the systems `save_shapes.json` still lists unclassified (3ds, nds, switch, wiiu, xbox360)             | `data/retrobat/save_shapes.json`, M6            | RetroBat's emulator for each writes the same shape Freegosy's desktop one does         | Probe 2 rerun per system, which needs those emulators installed and driven                | **open**            |
| F20 | A blank save an emulator writes at launch can overwrite a good cloud save, so uploads need a floor                              | M6 change detection, `save-sync`                | RetroBat emulators do write stub saves at launch, which probe 2 already saw for PS2    | Reasoned, plus a test over the observed class-D rewrite behaviour                         | **open**            |
| F21 | Freegosy carries 34 hand-curated BIOS md5s from libretro's docs that our `batocera-systems.json` join may miss                  | M5 gap reporting                                | Any of them is an alternative dump of a file RetroBat requires                         | Set-difference against the 157 md5s in `reference/batocera-systems.json`                  | **rejected**        |
| F22 | `SyncNegotiatePayload.device_id` is optional when the token is device-bound, the device being inferred from the token           | M6, `romm-api`                                  | Our paired token is device-bound, which it is                                          | Negotiate without `device_id` and compare the response                                    | **partly settled**  |

### Dropped at triage

Nothing here got a probe, because nothing in RomMBat changes if it is true.

| #   | Claim                                                                                      | Why it was cut                                                                                                      |
| --- | ------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------- |
| D1  | `POST /api/saves/delete` takes `{saves: [ids]}`                                            | RomMBat never deletes a server save in v1. The endpoint is real and irrelevant                                      |
| D2  | `/api/sync/devices/{id}/push-pull` and `SyncMode` `file_transfer` / `push_pull` exist      | Server-side transport modes RomMBat does not implement. Our device stays on `api`                                   |
| D3  | `/api/roms` can be searched by `md5` and `sha1`                                            | Not declared at 5.1.0, and RomMBat already uses `GET /api/roms/by-hash`, whose costs M3 measured                    |
| D4  | Their resume sends a bare `Range` with no `ETag` and no `If-Range`, and truncates on 200   | Strictly weaker than what M3 landed. Confirmation, not a change. See "Decisions that survived a challenge"          |
| D5  | Their firmware join keys on **filename**                                                   | Exactly what M5 rules out. Confirmation, not a change. See "Decisions that survived a challenge"                    |
| D6  | `SecureStorageService` falls back to plaintext preferences when the keyring is unavailable | They have a keyring and we cannot use DPAPI on a portable drive at all. The divergence is understood and deliberate |
| D7  | Gamepad service, `known_controllers`, the provider layout, the screenshots                 | M7 design leads. Not empirically verifiable in the sense this session means, and out of the factual sections        |
| D8  | `POST /api/token` password login and `POST /api/client-tokens/exchange` pairing            | Both explicitly ruled out by M1. Device pairing is the only auth path                                               |
| D9  | `POST /api/devices` with `allow_existing`, `allow_duplicate`, `reset_syncs`                | RomMBat never calls it; pairing owns device creation. See "Decisions that survived a challenge"                     |
| D10 | `test/mock_romm_server.py` as an API specification                                         | It specifies what Freegosy expects. Useful as a structural analogue for an offline stub, cited for nothing          |
| D11 | Repo scaffolding: `agent.md`, `analysis_options.yaml`, `.github/`                          | Skimmed, nothing to take                                                                                            |
| D12 | A 60-second cooldown on the pre-launch pull check                                          | A UI-latency choice for a client that pulls on launch. RomMBat's hooks never touch the network                      |
| D13 | `SaveSchema.is_public` and `PUT /api/saves/{id}/visibility`                                | A sharing feature, not a sync feature                                                                               |

---

## Experiments

Nothing below is a fact until it carries a route and a quotation. Re-run any of it with the
scripts in `tools/freegosy-probes/`, which read the server and token from the environment and
never print the host.

### F5: `GET /api/platforms` inlines every firmware record, with its md5. **Confirmed, live**

`tools/freegosy-probes/f5-platform-firmware.py`, against RomM 5.1.1-beta.1:

```text
GET /api/platforms
  status 200   433894 bytes   0.39 s
  platforms: 123
  platforms carrying a non-empty firmware[]: 79
  firmware records inlined in total:         656
  of those carrying md5_hash:                656
  of those flagged is_verified:              432
  distinct md5s:                             340
  platforms where firmware_count != len(firmware): 0

GET /api/firmware?platform_id=<ps2>
  status 200   0.10 s   records: 75
  same platform inlined on /api/platforms: 75
  ids only in the dedicated call: []
  ids only inlined:               []
```

**The array is complete, not a preview.** `firmware_count` equals `len(firmware)` on all 123
platforms, and the largest platform's inlined set has the same 75 ids as the dedicated call.
Every one of the 656 records carries `md5_hash`.

So **M5's md5 join is one 424 KB request, not one request per platform.** The plan's step 2,
"list candidates with `GET /api/firmware?platform_id=` and join on `md5_hash`", describes 79
requests on this library where one would do. The dedicated endpoint stays useful for a single
platform, and it is what a per-platform certification pass wants, but a whole-library BIOS gap
report should read the platform list.

This is also a payload the M2 guardrails should know about. It is the same family as the
`GET /api/collections` trap, an unpageable inlined array on a list endpoint, but it is
**three orders of magnitude smaller**: 424 KB for all 123 platforms against 715 KB for a
_single_ collection entry. It is a cost to note, not a trap to avoid.

### F21: Freegosy's BIOS md5s add nothing. **Rejected.** The probe that found it measured two other things

`tools/freegosy-probes/f21-bios-md5-join.py`:

```text
RetroBat requires 157 distinct md5s (reference/batocera-systems.json)

Freegosy carries 28 distinct md5s
  also required by RetroBat:     16
  not required by RetroBat:      12
```

All 16 hits are already in `batocera-systems.json`. The other 12 are files RetroBat does not
require, and our own rule is that **RetroBat is the authority on required BIOS**, so they are
not ours to act on. The lead is closed: Freegosy's registry contains no md5 our join misses.

The join against the live library, however, put a live measurement under two rules the plan
had only argued for:

```text
GET /api/platforms   433894 bytes   0.33 s
  firmware records inlined: 656   distinct md5s: 340
  RetroBat-required md5s this library holds: 49 of 157
  RetroBat-required md5s it does not hold:   108

Filtering the same set by is_verified, which the plan says never to do:
  required-and-held, flagged is_verified:     38
  required-and-held, NOT flagged is_verified: 11

Joining the same set on filename instead of md5:
  served name matches a required name: 39
  served name differs:                10
```

- **`is_verified` would have discarded 11 of the 49 correct BIOS files this library holds**,
  22% of them. The plan's existing figure (94 of 157 md5s unknown to RomM) comes from
  comparing two static files. This is the first measurement of the rule biting on real data.
- **10 of the 49 arrive under a name RetroBat does not use.** Measured examples:
  `segacdbios9303.bin` where RetroBat wants `bios_cd_u.bin`, `flash.bin` for `dc_flash.bin`,
  `sega_100.bin` for `saturn_bios.bin`, `pcfxbios.bin` for `pcfx.rom`, `bios.col` for
  `coleco.rom`. A filename join loses those ten silently, which is exactly the shape of
  failure Freegosy's own `_findMatchingSpec` has.
- **This library holds 49 of the 157 md5s RetroBat requires**, so M5's "needed, not in your
  library" report has 108 rows to write on this instance. That number is a property of one
  library and will differ everywhere.

### F14: `/api/roms` silently ignores an unknown query parameter. **Confirmed, live**

`tools/freegosy-probes/f13-f14-endpoint-traps.py`. Freegosy sends `platform_id` in one code
path and `platform_ids` in another; only the plural is declared at 5.1.0.

```text
Reference platform: fs_slug=psx  rom_count=9500

GET /api/roms  no scope at all
  status 200   total=83131   0.30 s
GET /api/roms  platform_ids (declared)
  status 200   total=9500   0.14 s
GET /api/roms  platform_id (undeclared)
  status 200   total=83131   0.26 s
GET /api/roms  not_a_real_parameter
  status 200   total=83131   0.24 s
```

**A misspelt filter is not an error, it is the whole library.** `platform_id` answers 200 and
returns all 83,131 ROMs. So does a parameter invented on the spot. There is no 422, no warning
and no echo of what was actually applied, which means **a scope typo cannot be distinguished
from a scope that genuinely matches everything** by looking at the response.

RomMBat is safe today by construction: `CatalogQuery.cs:173` sends `platform_ids`. Nothing
tests that, though, and nothing warned about it. The seam is a resolve-time assertion, cheap
to state: a scoped resolve whose `total` equals the unscoped library total is a bug in the
query, not a very large sync set.

### F13: `GET /api/saves/identifiers` exists and is fast, on nothing. **Open**

```text
GET /api/saves/identifiers
  status 200   0.07 s   entries=0
GET /api/platforms/identifiers
  status 200   0.21 s   entries=123
```

It does not 504 the way `GET /api/roms/identifiers` does, and the platform sibling's 0.21 s
for 123 entries matches what M3 measured. **But this account holds no saves**, so 0.07 s on an
empty set says nothing about how it behaves under load, and it takes no parameters, so it can
be neither scoped nor paged. That is the same shape that made the ROM sibling unusable.
**The lead stays open**, and it stays open on purpose: a handful of saves created by the write
probes below would not be a scale test either.

### F15: an empty `fs_extension` does **not** mean multi-file. **Confirmed, live, and it corrects a documented fact**

`tools/freegosy-probes/f15-f16-rom-file-shapes.py` over 2,000 ROMs by ascending id, with
`with_files=true`.

The ROM schema at 5.1.1-beta.1 carries **three** shape flags, not one:
`has_simple_single_file`, `has_nested_single_file`, `has_multiple_files`.

```text
Cross-tabulation of file count against fs_extension and the shape flag:
  files=1 ext=no  has_nested_single_file             157
  files=1 ext=yes has_simple_single_file            1398
  files=n ext=no  (none set)                           2
  files=n ext=no  has_multiple_files                 209
  files=n ext=no  has_nested_single_file             234

The two claims finding 83 makes, checked separately:
  roms with an empty fs_extension:            602
    of those flagged has_multiple_files:      209
    of those flagged has_nested_single_file:  391
  roms flagged has_multiple_files:            209
    of those with an empty fs_extension:      209
```

**One direction holds and the other does not.** Every ROM flagged `has_multiple_files` carries
an empty `fs_extension`, 209 of 209. But only 209 of the 602 extensionless ROMs are multi-file;
**391 are `has_nested_single_file`**, a ROM stored inside a folder. 157 of those hold exactly
one file, which is precisely the case Freegosy calls "single file foldered". Example rows:

```text
id=134184 has_nested_single_file fs_name='Bayonetta Origins Cereza and the Lost Demon (US)'
    member='Bayonetta Origins Cereza and the Lost Demon - (US) (1.0.0).nsp'
```

So finding 83's "every extensionless ROM is multi-file, 105 of 105 both ways" is **half wrong
on this sample**, and `romm-api`'s "`has_multiple_files`, and equivalently an empty
`fs_extension`" is wrong in the same place. The two are not equivalent.

**The code was already right and the stated reason was wrong**, which is the good version of
this. `SetResolver.cs:242` keys the multi-file exclusion on `row.HasMultipleFiles`, not on the
extension, so nothing mis-excludes today. Had anyone taken the plan at its word and
"simplified" that to the extension check the plan calls equivalent, 391 of 602 extensionless
ROMs in this sample would have been excluded as multi-file.

**The seam this leaves, written down and not fixed** (M3 is landed work and out of scope here):
a `has_nested_single_file` ROM falls past the multi-file check at `SetResolver.cs:242` into the
extension check at line 249 with an empty `fs_extension`, matches no `<extension>` entry, and is
reported as `ExcludedExtension`, which `SetsCommand.cs:466` renders as **"skipped, format not
supported by this system"** with the extension shown as `(none)`. For a Switch `.nsp` sitting in
a folder that is the wrong sentence, and it is the exact failure mode M3 wrote the multi-file
state to avoid: it sends someone to fix a format that is not the problem. Whoever picks up
multi-file support owns a third state here, not a second.

### F16: multi-disc sets carry no `.m3u`. **Rejected**

Same probe, same 2,000-ROM sample:

```text
Multi-file roms in the sample: 445
  of those carrying a .m3u member: 0
  member extensions across all multi-file roms:
    .pkg        1716
    .rap        1366
    .bin         911
    .exe         577
    .nsp         317
    .iso         132
```

**Not one `.m3u` in 445 multi-file ROMs.** Multi-disc sets are several images with the disc
number in the member name and no playlist at all:

```text
id=1471 fs_name='Halo 4 (World)'
    'Halo 4 (World) (En,...) (Disc 1).iso'
    'Halo 4 (World) (En,...) (Disc 2).iso'
```

Freegosy's model, that a multi-disc ROM is a playlist plus its discs, does not describe this
library. Its `.cue`/`.ccd`/`.mds`/`.toc` "non-launchable" filter is answering a question this
server does not pose either. **A later multi-file milestone has to build the `.m3u` itself**,
from the member names, rather than expecting one in the payload. The `.rap` members beside
every `.pkg` are PS3 licence files and are the more common multi-file shape here by far.

---

## The write probes

Run against the live instance with permission, scoped to one ROM and a slot named for the
probe. **Everything created was deleted.** The instance began the session with 11 devices,
0 saves and 0 play sessions, and ended it with 11 devices, 0 saves and 0 play sessions.
Scripts: `f1-f12-save-lifecycle.py`, `f1-f12b-conflict-and-optimistic.py`,
`f8-device-syncs-scope.py`, `f4-play-sessions.py`.

### F1: `optimistic` defaults to true and marks the device current before the bytes land. **Confirmed, live. The most consequential finding of the session**

Two devices that had never synced a save, one downloading with the default and one with
`optimistic=false`:

```text
C before any download:            {"is_untracked": false, "is_current": false, ...}
GET content?device_id=C&optimistic=false -> 200, 206 bytes
C after optimistic=false download: {"is_untracked": false, "is_current": false, ...}
POST /api/saves/{id}/downloaded {device_id: C} -> 200
C after the explicit ack:          {"is_untracked": false, "is_current": true, ...}

D before any download:            {"is_untracked": false, "is_current": false, ...}
GET content?device_id=D (default) -> 200, 206 bytes
D after the download:             {"is_untracked": false, "is_current": true, ...}
```

**The default marks the device current on the request, not on the client confirming.**
Device D went from `is_current: false` to `is_current: true` by issuing a GET and nothing
else. With `optimistic=false` the flag stays false until `POST /api/saves/{id}/downloaded`
sets it.

For a client whose premise is a handheld on unreliable Wi-Fi, that default is a way to lose
a save silently. **A download that dies mid-body leaves the server believing the device has
the save**, so the next negotiate answers `no_op` for that slot and the save never comes
down again. Nothing surfaces; the device simply never gets it.

`docs/PLAN.md` already calls `POST /api/saves/{id}/downloaded`, so the intent was right, but
it never passes `optimistic=false`. Without it the ack is decoration: the record is already
written by the time the client makes it. **The two have to travel together**, and the ack has
to come after the bytes are written and verified, not after the response headers arrive. This
is the same discipline M3 landed for ROM downloads, where the `.part` file is verified before
the rename.

### F6: the server renames the upload, and hands back the untagged name. **Confirmed, live, and the server does the work Freegosy does by hand**

```text
POST /api/saves (sent 'probe.srm') -> 200
  file_name         'probe [2026-08-10_22-58-26].srm'
  file_name_no_tags 'probe'
  file_name_no_ext  'probe [2026-08-10_22-58-26]'
  file_extension    'srm'
```

The rename is what `docs/PLAN.md` says it is. What the plan does not say is **what to write
on disk when a save comes back down**, and that is the half that decides whether a restored
save is loadable at all. RetroArch and the standalone emulators find a battery save by
matching the ROM name, so a file written as `probe [2026-08-10_22-58-26].srm` is invisible to
the emulator that needs `probe.srm`. Freegosy hit this as its issues #42 and #28 and answered
it with a hand-written `normalizeSaveFilename` regex, including a speculative `(-\d+)?`
branch for a disambiguating counter.

**None of that is necessary.** The server already returns `file_name_no_tags`, so the on-disk
name is `file_name_no_tags` plus `file_extension` and no client-side regex is involved.
`docs/PLAN.md` says to persist the returned `file_name`, which is right for the server-side
identity and wrong as a filename to write. Both are needed and they are different fields.

Not observed: any `-N` disambiguator. Two saves uploaded inside the same second landed on
timestamps a second apart, so the collision case Freegosy's regex guards against did not
arise here and stays untested.

### F3: identical uploads dedup within a slot. **Confirmed, live**

```text
second identical upload -> 200, id 127
  saves in the slot before=1 after=1
  same row reused: True
  file names now: ['probe [2026-08-10_22-58-26].srm']
```

Byte-identical content posted twice into the same slot **reuses the same row**, id 127 both
times, and the slot count does not move. So core principle 1's "save uploads dedup on
`content_hash` within a slot, so replaying a failed flush is idempotent" is now measured
rather than read off the backend.

Different content into the same slot without `overwrite` **appends a row** (127 then 128)
rather than replacing, while `overwrite=true` reuses the row in place (130 stayed 130). That
pairing is why F2 matters: without cleanup a slot grows one row per genuine change forever.

**And this is where Freegosy's design goes wrong in a way worth recording.** Its bundle path
writes a `freegosy_sync.txt` holding `DateTime.now()` into every archive, described in its own
comment as being there "to bypass server-side deduplication". That guarantees a fresh
`content_hash` on every upload, so every sync of an unchanged directory save creates a new
server row. It is the exact behaviour our "hash the logical contents, not the archive bytes"
rule exists to prevent, arrived at from the opposite direction: they made the archive
deliberately non-deterministic. Ours must be deterministic or dedup cannot work.

### F2: `autocleanup` prunes the slot server-side. **Confirmed, live**

```text
saves in the slot before: 2
  upload 0 with autocleanup=true&autocleanup_limit=2 -> 200, slot now holds 2
  upload 1 with autocleanup=true&autocleanup_limit=2 -> 200, slot now holds 2
  upload 2 with autocleanup=true&autocleanup_limit=2 -> 200, slot now holds 2
```

The slot holds at exactly the limit across three further uploads, keeping the newest. The
parameters default to `autocleanup=false` and `autocleanup_limit=10`, so **the default is
unbounded growth**, and `docs/PLAN.md` has no save-retention story at all today. A device
syncing a changed save every session accumulates one server row per session forever.

This is a decision RomMBat has to make deliberately rather than inherit: server-side pruning
is one line of query string, and the alternative is that the user's RomM fills up with the
same save. It interacts with conflict handling, since the plan defaults to `keep_both`, and a
`keep_both` policy under an unbounded slot is precisely how a library gets unusable.

### F12: the 409 body is a bare string, not the structured detail Freegosy documents. **Rejected as described**

Freegosy's `docs/romm_49_save_sync_research.md` gives the conflict body as
`{"detail": {"error", "message", "save_id", "current_save_time", "device_sync_time"}}`, and
its code reads `response.data['detail'] as Map<String, dynamic>?`. Measured at 5.1.1-beta.1:

```text
B uploads different content into the same slot -> 409
  body, verbatim:
    {
      "detail": "Slot has a newer save since your last sync"
    }
```

**A string, not an object.** Freegosy's cast yields null here and it would lose the conflict
detail entirely. The consequence for M6 is concrete: **a 409 hands the client no server
timestamp and no save id**, so surfacing a useful conflict means fetching the save row
separately. The plan's "a 409 means the slot moved since the last sync; surface it" needs that
extra fetch spelled out, or the UI has nothing to show but the sentence above.

The trigger is also not what the naive reading suggests. The 409 fired for the device whose
**own sync record was not current**, not for the device that had most recently written:

```text
device B uploads different content -> 409
device A uploads again without overwrite -> 200
```

A had uploaded the current save and so was current; B had never synced it. So the rule is
"this device's record is stale for this slot", which is per device, not per save.

### F7: there is no per-device isolation. **Rejected**

Freegosy's research doc frames RomM 4.9 as isolating saves per device. It does not:

```text
GET /api/saves?rom_id&device_id=A -> 2 saves
GET /api/saves?rom_id&device_id=B -> 2 saves
  same id set: True
```

Both devices see the same rows. `device_id` scopes **sync bookkeeping**, never visibility.
That is what `docs/PLAN.md` already assumes, so this is a decision that survived a challenge
rather than a change: the plan passes `device_id` for negotiation and conflict detection, and
never treats it as a filter.

### F8: `device_syncs` is a real roster, but only when you ask. **Confirmed with a correction**

With both devices holding genuine records, the same save listed three ways:

```text
  GET /api/saves?rom_id&device_id=A
    device_syncs holds 2 entries
      A: is_current=True ...
      B: is_current=True ...
  GET /api/saves?rom_id&device_id=B
    device_syncs holds 2 entries
      B: is_current=True ...
      A: is_current=True ...
  GET /api/saves?rom_id&no device_id
    device_syncs holds 0 entries
```

So it lists every device that has a record, with the queried device sorted first. **But it is
empty when no `device_id` is passed**, and empty reads exactly like "nothing has ever synced
this" while actually meaning "you did not ask". A device that has genuinely never synced a
save is **absent** from the array rather than present with `is_current: false`, so absence
carries two different meanings depending on the query. Any code reading this field has to
pass `device_id` and treat a missing entry as "never synced", which is the strongest reason
to pull, not the weakest.

### F9: `origin_device_id` names the uploading device. **Confirmed, live**

`origin_device_id` came back as device A on every row A uploaded, including after a
`overwrite=true` replacement. It is not in `docs/PLAN.md` and it is the cheapest way for a
client to recognise its own upload coming back down, which matters when deciding whether a
`download` operation is worth acting on.

### F10: `track` and `untrack` are flags, not removals. **Confirmed, live**

```text
POST /api/saves/{id}/untrack {device_id: B} -> 200
  B now: {"is_untracked": true, "is_current": true, ...}
```

Untracking sets the flag and leaves `is_current` alone; the record and the save both survive.
Both routes take `{device_id}` in the **body**, not the query, and both require
`devices.write` rather than `assets.write`.

### F11: `/api/saves/summary` is a per-slot inventory. **Confirmed, live**

```text
GET /api/saves/summary?rom_id -> 200  0.08 s
  {"total_count": 2, "slots": [{"slot": "rommbat-freegosy-probe", "count": 2,
   "latest": { ...full SaveSchema... }}]}
```

One request gives every slot on a ROM, its depth and its newest save in full. It takes only
`rom_id`, so it is a per-game call and not a sync-wide one.

### F22: negotiate requires `device_id` unless the token is device-bound. **Partly settled**

```text
POST /api/sync/negotiate with device_id -> 200
  session=141 upload=0 download=0 conflict=0 no_op=1
    no_op  slot='rommbat-freegosy-probe'  reason='No changes since last sync'
POST /api/sync/negotiate without device_id -> 400
  {"detail":"device_id is required (either in the request payload or implicit via
   a device-bound client token)"}
```

The error names the condition exactly. The probe token is an ordinary client token, so the
device-bound half is **untested here** and stays open; a token minted by pairing is
device-bound and should not need the field. Sending it explicitly is correct either way, and
that is what the plan already does.

### F4: play sessions have a standalone ingest, and Freegosy's shape is wrong. **Confirmed, live**

```text
POST /api/play-sessions  bare array, Freegosy's shape -> 422
  {"detail":[{"type":"model_attributes_type","loc":["body"],
   "msg":"Input should be a valid dictionary or object to extract fields from", ...}]}

POST /api/play-sessions  envelope, the 5.1.0 schema's shape -> 201
  {"results":[{"index":0,"status":"created","id":187,"detail":null}],
   "created_count":1,"skipped_count":0}
```

Freegosy posts a bare array with `device_id` on each entry, which its research doc also
describes. At 5.1.1-beta.1 that is a 422. The accepted shape is the envelope the pinned
schema declares, `{device_id, sessions: [...]}`, with `device_id` **outside** the entries.

Four more things the same probe measured, three of which the plan asserts and none of which
it had measured:

```text
POST /api/play-sessions  the identical envelope again -> 201
  {"results":[{"index":0,"status":"duplicate","id":null,"detail":null}],
   "created_count":0,"skipped_count":1}
  sessions after the replay: 1 (was 1)

POST /api/play-sessions  no rom_id on the entry -> 201  (created)
POST /api/play-sessions  101 entries -> 400  {"detail":"Batch size exceeds maximum of 100"}
POST /api/play-sessions  end_time earlier than start_time -> 422
  "Value error, end_time must be after start_time"
```

- **Replay is idempotent and the server says which entries it skipped.** The response is a
  per-index result array with `created_count` and `skipped_count`, so a partially-failed
  flush can be reconciled exactly rather than guessed at. This is the measurement under core
  principle 1's "replaying a failed flush is idempotent", and it is better than the plan
  assumed: the client does not have to infer the dedup, it is told.
- **The 100-per-call cap is real** and enforced with that message.
- **`end_time` must be strictly after `start_time`**, enforced with that message.
- **`rom_id` is genuinely optional.** A session with no ROM is accepted, which is worth
  knowing given `game-end` fires with no preceding `game-start` for ES-menu launches. It is
  not a licence to send one: an unattributed session is not useful, and the plan's rule that
  an orphan `game-end` is discarded stands.

The endpoint works with no sync session open at all, so **playtime can flush without
negotiating saves first**. `docs/PLAN.md` routes play sessions only through
`POST /api/sync/sessions/{session_id}/complete`, which couples the two; they need not be
coupled, and for an agent that wakes on `game-end` with nothing to negotiate, the standalone
route is the whole job.

### Four incidental traps, all measured

Not on the candidate list. They turned up while running the probes above and each would have
cost someone an hour.

- **`download_path` is not a usable URL.** It is served as
  `/api/saves/130/content?timestamp=2026-08-10 23:00:25.474218+00:00`: a raw space and an
  unencoded `+` in the query string. Freegosy concatenates this onto the base URL and requests
  it directly. Build the download URL from the save `id` instead.
- **`POST /api/saves/delete` fails the whole batch if any id is already gone**, answering 404
  and deleting nothing. Autocleanup can remove an id between listing and deleting, so a batch
  built from a stale list can fail entirely. Delete one at a time, or re-list immediately
  before.
- **`POST /api/devices` answers `{device_id, name, created_at}`**, while `GET /api/devices`
  keys the same value `id`. The create response is not a `DeviceSchema`. This is the one place
  Freegosy's defensive `data['device_id'] ?? data['id']` earns its keep.
- **The `emulator` parameter becomes a path segment** in the stored save's `file_path`
  (`users/<hex>/saves/xbox/1393/rommbat-probe/...`), so it is not a free-form label. Anything
  RomMBat sends as `emulator` shapes the server's directory layout.

---

## Decisions that survived a challenge

Recorded because a decision that has been challenged and held is worth more than one that was
never tested, which is how `docs/PLAN.md` already treats the Playnite `RomMRegisterDevice`
disagreement.

| RomMBat's rule                                                           | Freegosy does                                                          | What settled it                                                                    |
| ------------------------------------------------------------------------ | ---------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| Join firmware on **md5 only**, never filename                            | `_findMatchingSpec` matches `spec.fileName.toLowerCase()`              | 10 of the 49 required files this library holds arrive under a different name (F21) |
| Ignore `is_verified`                                                     | Not consulted                                                          | `is_verified` misses 11 of those same 49 files (F21)                               |
| `device_id` is bookkeeping, never a visibility filter                    | Research doc frames 4.9 as isolating saves per device                  | Both devices see the same rows (F7)                                                |
| Hash the **logical contents** of a bundle, never the archive bytes       | Injects a timestamped `freegosy_sync.txt` to defeat server dedup       | Identical content dedups to one row; theirs never can (F3)                         |
| Always send a stable, non-null slot                                      | Moved from timestamped slots to a constant one after two filename bugs | Their own issues #42 and #28, plus the dedup measurement (F3)                      |
| Resume with an `ETag` and `If-Range`, and never `Range` a multi-file ROM | Bare `Range`, no validator, truncates on 200                           | M3's landed work is strictly stronger; nothing here challenges it                  |
| Device pairing is the only auth path                                     | `POST /api/token` password login and `client-tokens/exchange`          | Ruled out by M1; not re-litigated                                                  |
| Never call `POST /api/devices` with host fingerprint fields              | Calls it; its research doc sends `hostname`, its code dropped it       | Not re-probed. The MAC-dedup reading in `docs/PLAN.md` line 168 is unchanged       |

## What stays open

- **F13**, whether `GET /api/saves/identifiers` scales. It answers in 0.07 s on an empty set
  and takes no parameters, which is the shape that made `/api/roms/identifiers` unusable. No
  library here has enough saves to load it.
- **F22**, whether a pairing-minted device-bound token really lets `device_id` be omitted from
  negotiate. The error message says it should; the probe token is not device-bound.
- **F17**, the GameCube and Wii game ID offsets (`0x00` in an `.iso`, `0x58` in an `.rvz`).
  Needs a real disc image of each, which this machine does not have. The `.iso` offset agrees
  with the documented GameCube and Wii disc header layout and is low risk; **the `.rvz` offset
  is container-specific and is the one to check**.
- **F18**, whether a multi-disc save filename drops the region tags its `.m3u` carries.
  Needs probe 2 rerun on a multi-disc title in a real RetroBat install.
- **F19**, save shapes for the systems `save_shapes.json` still lists unclassified. Freegosy
  has strategies for the Switch, 3DS, DS, Wii U and Xbox 360 emulators, so it supplies a
  hypothesis per system, but RetroBat runs different emulators in a different layout and only
  probe 2 can decide. **These are hypotheses to test, not classifications to copy**, and
  nothing has been written into `save_shapes.json`.
- **F20**, whether a blank save written at launch can overwrite a good server save. Probe 2
  already measured that a PS2 launch rewrites both memory cards with no in-game save, so the
  hazard is real in RetroBat; what is unmeasured is whether the resulting file is small enough
  for a size floor to catch, and a size floor is a poor instrument anyway. The content hash
  the plan already mandates is the better guard.
