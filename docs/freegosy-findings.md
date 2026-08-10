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

| #   | Claim                                                                                                                          | Touches                                              | Would hold here if                                                                    | Cheapest experiment                                                                     | Verdict |
| --- | ------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------- | ------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- | ------- |
| F1  | `GET /api/saves/{id}/content` takes `optimistic`, **defaulting to true**, which records the device sync before the bytes land | M6 download step, `romm-api`, `save-sync`            | The server marks the device current on request rather than on the client confirming   | Download a save with the default and with `optimistic=false`, read `last_synced_at` back  | pending |
| F2  | `POST /api/saves` takes `autocleanup` and `autocleanup_limit` (default 10), pruning old saves in the slot server-side          | M6 upload, `romm-api`, `save-sync`                   | The server actually deletes; RomMBat has no save-retention story at all today          | Upload N+1 saves into one slot with `autocleanup=true&autocleanup_limit=N`, list the slot  | pending |
| F3  | The server dedups identical uploads within a slot, which is what makes our replay-safe flush safe                              | Core principle 1, M6, `offline-and-portable`         | Two identical uploads produce one row. Freegosy busts this deliberately, so it may not | Upload byte-identical content to the same slot twice, count the rows                       | pending |
| F4  | `POST /api/play-sessions` is a standalone ingest taking `{device_id, sessions:[...]}`, not only a field on `/complete`         | M6 flush, `romm-api`, `offline-and-portable`         | Sessions can be flushed without opening a sync session                                | Post one session through the standalone route and read it back                             | pending |
| F5  | `GET /api/platforms` inlines the full `firmware[]` array with `md5_hash` per platform                                          | M5, `romm-api`, possibly the M2 scale guardrail      | The join RomMBat needs is one request, not one per platform                            | Fetch `/api/platforms`, count firmware records and md5s, measure bytes                     | **live, confirmed** |
| F6  | The server's `[YYYY-MM-DD_HH-MM-SS]` filename tag has to be **stripped before writing to disk**, or the emulator cannot find it | M6 restore, `save-sync`                              | RetroBat's emulators locate saves by rom-name match, which they do                     | Upload a save, read the returned `file_name`, check the tag shape and any `-N` suffix      | pending |
| F7  | RomM 4.9+ **isolates** saves per device (Freegosy's framing), rather than merely tracking per device                           | M6 conflict handling, `save-sync`                    | A save uploaded by device A is invisible or subordinate to device B                    | List saves for one rom with two different `device_id` values and diff the sets              | pending |
| F8  | `device_syncs[].is_current` answers "does the server have something newer" without a negotiate                                 | M6, `romm-api`                                       | The flag is server-computed per device and trustworthy                                | Read `GET /api/saves?rom_id=&device_id=` before and after an upload from another device     | pending |
| F9  | `SaveSchema.origin_device_id` names the device that produced a save                                                           | M6 conflict handling, `save-sync`                    | It is populated on upload, so a client can tell its own save from a peer's            | Read it back off a save uploaded with `device_id` set                                       | pending |
| F10 | `POST /api/saves/{id}/track` and `/untrack` opt a single save out of syncing for one device                                   | M6, `romm-api`                                       | `is_untracked` then changes what negotiate returns                                    | Untrack a save, re-negotiate, see whether the operation disappears                          | pending |
| F11 | `GET /api/saves/summary?rom_id=` returns per-slot counts and the latest save per slot                                         | M6, `romm-api`                                       | It is cheap enough to replace listing every save                                      | Call it against a rom with saves and compare against `GET /api/saves`                       | pending |
| F12 | A 409 on upload carries a structured body with `save_id`, `current_save_time` and `device_sync_time`                          | M6, `romm-api`, `save-sync`                          | The body is actionable, so a conflict can be shown without a second request           | Force a 409 and quote the body                                                              | pending |
| F13 | `GET /api/saves/identifiers` takes no parameters, the same shape that made `/api/roms/identifiers` 504                        | M6 reconcile, `romm-api`                             | Saves are few enough that it answers, unlike the roms sibling                         | Time the call against the live library                                                      | **open** |
| F14 | `/api/roms` **silently ignores** an unknown query parameter, so `platform_id` resolves the whole library                      | M2 set resolution, `romm-api`                        | The server does not reject unknown params, which FastAPI does not by default          | Compare `platform_id=` against `platform_ids=` on the same platform, read `total`           | **live, confirmed** |
| F15 | A ROM can carry exactly one file and an empty `fs_extension`, which finding 83 treats as the multi-file marker                | M3 exclusion state and its message, `romm-api`       | Such rows exist in a real library                                                     | Scan a sample of `/api/roms?with_files=true` for `len(files)==1 and fs_extension==''`       | **live, confirmed** |
| F16 | A multi-disc set is one multi-file ROM whose `files[]` includes a `.m3u` plus non-launchable `.cue`/`.ccd`/`.mds`/`.toc`      | M3 seam, the later multi-file milestone              | Real multi-disc rows look like that on this instance                                  | Scan the same sample for `.m3u` members and tally the sibling extensions                     | **live, rejected** |
| F17 | The GameCube/Wii game ID is 4 ASCII bytes at offset `0x00` in an `.iso` and `0x58` in an `.rvz`                               | M6 attribution fallback, `save-sync`                 | The offsets are right for the containers RetroBat accepts                             | Read the header of a real `.iso` and a real `.rvz`                                          | pending |
| F18 | A multi-disc `.m3u` filename carries region tags the save file does not, so save matching needs tag stripping                 | M6 attribution, `save-sync`                          | RetroBat's emulators name per-game saves from the disc, not the playlist              | Probe 2 rerun on a multi-disc PS1 title                                                     | pending |
| F19 | Save-shape hypotheses for the systems `save_shapes.json` still lists unclassified (3ds, nds, switch, wiiu, xbox360)           | `data/retrobat/save_shapes.json`, M6                 | RetroBat's emulator for each writes the same shape Freegosy's desktop one does        | Probe 2 rerun per system, which needs those emulators installed and driven                  | pending |
| F20 | A blank save an emulator writes at launch can overwrite a good cloud save, so uploads need a floor                            | M6 change detection, `save-sync`                     | RetroBat emulators do write stub saves at launch, which probe 2 already saw for PS2   | Reasoned, plus a test over the observed class-D rewrite behaviour                            | pending |
| F21 | Freegosy carries 34 hand-curated BIOS md5s from libretro's docs that our `batocera-systems.json` join may miss                | M5 gap reporting                                     | Any of them is an alternative dump of a file RetroBat requires                        | Set-difference against the 157 md5s in `reference/batocera-systems.json`                     | **rejected** |
| F22 | `SyncNegotiatePayload.device_id` is optional when the token is device-bound, the device being inferred from the token         | M6, `romm-api`                                       | Our paired token is device-bound, which it is                                          | Negotiate without `device_id` and compare the response                                       | pending |

### Dropped at triage

Nothing here got a probe, because nothing in RomMBat changes if it is true.

| #   | Claim                                                                                       | Why it was cut                                                                                                    |
| --- | ------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| D1  | `POST /api/saves/delete` takes `{saves: [ids]}`                                              | RomMBat never deletes a server save in v1. The endpoint is real and irrelevant                                     |
| D2  | `/api/sync/devices/{id}/push-pull` and `SyncMode` `file_transfer` / `push_pull` exist        | Server-side transport modes RomMBat does not implement. Our device stays on `api`                                  |
| D3  | `/api/roms` can be searched by `md5` and `sha1`                                              | Not declared at 5.1.0, and RomMBat already uses `GET /api/roms/by-hash`, whose costs M3 measured                   |
| D4  | Their resume sends a bare `Range` with no `ETag` and no `If-Range`, and truncates on 200     | Strictly weaker than what M3 landed. Confirmation, not a change. See "Decisions that survived a challenge"          |
| D5  | Their firmware join keys on **filename**                                                     | Exactly what M5 rules out. Confirmation, not a change. See "Decisions that survived a challenge"                   |
| D6  | `SecureStorageService` falls back to plaintext preferences when the keyring is unavailable   | They have a keyring and we cannot use DPAPI on a portable drive at all. The divergence is understood and deliberate |
| D7  | Gamepad service, `known_controllers`, the provider layout, the screenshots                   | M7 design leads. Not empirically verifiable in the sense this session means, and out of the factual sections        |
| D8  | `POST /api/token` password login and `POST /api/client-tokens/exchange` pairing              | Both explicitly ruled out by M1. Device pairing is the only auth path                                              |
| D9  | `POST /api/devices` with `allow_existing`, `allow_duplicate`, `reset_syncs`                  | RomMBat never calls it; pairing owns device creation. See "Decisions that survived a challenge"                    |
| D10 | `test/mock_romm_server.py` as an API specification                                           | It specifies what Freegosy expects. Useful as a structural analogue for an offline stub, cited for nothing          |
| D11 | Repo scaffolding: `agent.md`, `analysis_options.yaml`, `.github/`                            | Skimmed, nothing to take                                                                                          |
| D12 | A 60-second cooldown on the pre-launch pull check                                            | A UI-latency choice for a client that pulls on launch. RomMBat's hooks never touch the network                     |
| D13 | `SaveSchema.is_public` and `PUT /api/saves/{id}/visibility`                                  | A sharing feature, not a sync feature                                                                             |

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
*single* collection entry. It is a cost to note, not a trap to avoid.

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
