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
| Date               | 2026-08-10 and 2026-08-11                                          |

The instance host is redacted throughout, per the repo rules.

## The honest total

35 candidates read, 13 dropped at triage, 22 probed. Of those: **15 confirmed, 5 rejected,
2 corrected, 1 left open.** Six further traps turned up while running the
probes and none of them was on the list.

**The one that would have cost real data** is F1: `GET /api/saves/{id}/content` marks the
device as current on the request, not on receipt, unless `optimistic=false` is passed. On a
handheld that drops Wi-Fi mid-download, the server then believes the device holds a save it
does not, and every later negotiate answers `no_op`. Freegosy passes `optimistic=true`
explicitly, so it did not find this; the parameter simply led us to look.

**One claim of mine was retracted.** F5b first reported that firmware lives on the
`-unofficial` platform twin and that a per-platform lookup could therefore miss it. That was
an artifact of the probe printing only its first match, and the library's owner corrected it:
the `-unofficial` rows are his own filing scheme for demos and prototypes, and the BIOS is on
both rows. The probe now prints every match. What survives is smaller and is recorded as
such.

**Two corrections to things already written down as measured.** F15: `docs/PLAN.md` finding
83 claimed multi-file and an empty `fs_extension` were equivalent both ways, and only one
direction holds; the code never relied on the wrong half. F18: `<extension>` turns out to be
the authority on what EmulationStation **offers**, not on what the emulator can **play**, so
passing the extension filter is not evidence a file will launch. `ps2` lists `.m3u` and PCSX2
cannot use one.

**The one that reverses a recommendation** is also F18, and it took two passes to get right.
The plan tells DuckStation to use `PerGameFileTitle` so a memory card is named after the rom
file. Reading the emulator's database, this document first concluded that every per-game mode
splits a multi-disc set and only `Shared` carries it. **The first real card on disk refuted
that.** The stock `PerGameTitle` names the card from the database with the disc marker
stripped, so a two-disc set shares one card, regions stay separate, and revisions collapse.
The recommendation still has to go, but for the opposite reason: the default is already
correct and the conversion is what would break it.

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

| #   | Claim                                                                                                                           | Touches                                         | Would hold here if                                                                     | Cheapest experiment                                                                       | Verdict                    |
| --- | ------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------- | -------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- | -------------------------- |
| F1  | `GET /api/saves/{id}/content` takes `optimistic`, **defaulting to true**, which records the device sync before the bytes land   | M6 download step, `romm-api`, `save-sync`       | The server marks the device current on request rather than on the client confirming    | Download a save with the default and with `optimistic=false`, read `last_synced_at` back  | **live, confirmed**        |
| F2  | `POST /api/saves` takes `autocleanup` and `autocleanup_limit` (default 10), pruning old saves in the slot server-side           | M6 upload, `romm-api`, `save-sync`              | The server actually deletes; RomMBat has no save-retention story at all today          | Upload N+1 saves into one slot with `autocleanup=true&autocleanup_limit=N`, list the slot | **live, confirmed**        |
| F3  | The server dedups identical uploads within a slot, which is what makes our replay-safe flush safe                               | Core principle 1, M6, `offline-and-portable`    | Two identical uploads produce one row. Freegosy busts this deliberately, so it may not | Upload byte-identical content to the same slot twice, count the rows                      | **live, confirmed**        |
| F4  | `POST /api/play-sessions` is a standalone ingest taking `{device_id, sessions:[...]}`, not only a field on `/complete`          | M6 flush, `romm-api`, `offline-and-portable`    | Sessions can be flushed without opening a sync session                                 | Post one session through the standalone route and read it back                            | **live, confirmed**        |
| F5  | `GET /api/platforms` inlines the full `firmware[]` array with `md5_hash` per platform                                           | M5, `romm-api`, possibly the M2 scale guardrail | The join RomMBat needs is one request, not one per platform                            | Fetch `/api/platforms`, count firmware records and md5s, measure bytes                    | **live, confirmed**        |
| F6  | The server's `[YYYY-MM-DD_HH-MM-SS]` filename tag has to be **stripped before writing to disk**, or the emulator cannot find it | M6 restore, `save-sync`                         | RetroBat's emulators locate saves by rom-name match, which they do                     | Upload a save, read the returned `file_name`, check the tag shape and any `-N` suffix     | **live, confirmed**        |
| F7  | RomM 4.9+ **isolates** saves per device (Freegosy's framing), rather than merely tracking per device                            | M6 conflict handling, `save-sync`               | A save uploaded by device A is invisible or subordinate to device B                    | List saves for one rom with two different `device_id` values and diff the sets            | **live, rejected**         |
| F8  | `device_syncs[].is_current` answers "does the server have something newer" without a negotiate                                  | M6, `romm-api`                                  | The flag is server-computed per device and trustworthy                                 | Read `GET /api/saves?rom_id=&device_id=` before and after an upload from another device   | **live, corrected**        |
| F9  | `SaveSchema.origin_device_id` names the device that produced a save                                                             | M6 conflict handling, `save-sync`               | It is populated on upload, so a client can tell its own save from a peer's             | Read it back off a save uploaded with `device_id` set                                     | **live, confirmed**        |
| F10 | `POST /api/saves/{id}/track` and `/untrack` opt a single save out of syncing for one device                                     | M6, `romm-api`                                  | `is_untracked` then changes what negotiate returns                                     | Untrack a save, re-negotiate, see whether the operation disappears                        | **live, confirmed**        |
| F11 | `GET /api/saves/summary?rom_id=` returns per-slot counts and the latest save per slot                                           | M6, `romm-api`                                  | It is cheap enough to replace listing every save                                       | Call it against a rom with saves and compare against `GET /api/saves`                     | **live, confirmed**        |
| F12 | A 409 on upload carries a structured body with `save_id`, `current_save_time` and `device_sync_time`                            | M6, `romm-api`, `save-sync`                     | The body is actionable, so a conflict can be shown without a second request            | Force a 409 and quote the body                                                            | **live, rejected**         |
| F13 | `GET /api/saves/identifiers` takes no parameters, the same shape that made `/api/roms/identifiers` 504                          | M6 reconcile, `romm-api`                        | Saves are few enough that it answers, unlike the roms sibling                          | Time the call against the live library                                                    | **open**                   |
| F14 | `/api/roms` **silently ignores** an unknown query parameter, so `platform_id` resolves the whole library                        | M2 set resolution, `romm-api`                   | The server does not reject unknown params, which FastAPI does not by default           | Compare `platform_id=` against `platform_ids=` on the same platform, read `total`         | **live, confirmed**        |
| F15 | A ROM can carry exactly one file and an empty `fs_extension`, which finding 83 treats as the multi-file marker                  | M3 exclusion state and its message, `romm-api`  | Such rows exist in a real library                                                      | Scan a sample of `/api/roms?with_files=true` for `len(files)==1 and fs_extension==''`     | **live, confirmed**        |
| F16 | A multi-disc set is one multi-file ROM whose `files[]` includes a `.m3u` plus non-launchable `.cue`/`.ccd`/`.mds`/`.toc`        | M3 seam, the later multi-file milestone         | Real multi-disc rows look like that on this instance                                   | Scan the same sample for `.m3u` members and tally the sibling extensions                  | **live, rejected**         |
| F17 | The GameCube/Wii game ID is 4 ASCII bytes at offset `0x00` in an `.iso` and `0x58` in an `.rvz`                                 | M6 attribution fallback, `save-sync`            | The offsets are right for the containers RetroBat accepts                              | Read the header of a real `.iso` and a real `.rvz`                                        | **live, confirmed**        |
| F18 | A multi-disc `.m3u` filename carries region tags the save file does not, so save matching needs tag stripping                   | M6 attribution, `save-sync`                     | RetroBat's emulators name per-game saves from the disc, not the playlist               | Probe 2 rerun on a multi-disc PS1 title, driven far enough that a card appears            | **probe, corrected twice** |
| F19 | Save-shape hypotheses for the systems `save_shapes.json` still lists unclassified (3ds, nds, switch, wiiu, xbox360)             | `data/retrobat/save_shapes.json`, M6            | RetroBat's emulator for each writes the same shape Freegosy's desktop one does         | Probe 2 rerun per system, which needs those emulators installed and driven                | **probe, confirmed**       |
| F20 | A blank save an emulator writes at launch can overwrite a good cloud save, so uploads need a floor                              | M6 change detection, `save-sync`                | RetroBat emulators do write stub saves at launch, which probe 2 already saw for PS2    | Reasoned, plus a test over the observed class-D rewrite behaviour                         | **probe, refuted**         |
| F21 | Freegosy carries 34 hand-curated BIOS md5s from libretro's docs that our `batocera-systems.json` join may miss                  | M5 gap reporting                                | Any of them is an alternative dump of a file RetroBat requires                         | Set-difference against the 157 md5s in `reference/batocera-systems.json`                  | **rejected**               |
| F22 | `SyncNegotiatePayload.device_id` is optional when the token is device-bound, the device being inferred from the token           | M6, `romm-api`                                  | Our paired token is device-bound, which it is                                          | Negotiate without `device_id` and compare the response                                    | **live, confirmed**        |

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

### F5b: fetching one BIOS by md5. **Confirmed**, plus a claim of mine that was **retracted**

`f5b-bios-fetch-by-md5.py` is M5's flow done by hand: read the requirement out of
`reference/batocera-systems.json`, join on md5 alone, verify what arrives, write it to the
manifest's path. It worked, and `bios/psxonpsp660.bin` is now on the test install with a
verified md5.

**`is_verified` is `False` on the exact file the emulator needs to boot.** Both copies carry
`is_verified: False` while the md5 is precisely what RetroBat requires. This is the sharpest
instance of the rule: filtering on that flag refuses the one file without which no PS1 game
runs at all.

**Retracted: "firmware lives on the `-unofficial` twin, so a per-platform lookup can miss
it".** The first run of this probe reported `found on platform fs_slug='psx-unofficial'` and
I built a correctness argument on it. That was an artifact of the probe taking `matches[0]`
out of iteration order, not a fact about the library. The file is on **both** rows:

```text
  records whose md5_hash matches: 2
  every platform row carrying this md5:
    fs_slug='psx-unofficial'  slug='psx'  firmware_id=905  is_verified=False
    fs_slug='psx'             slug='psx'  firmware_id=599  is_verified=False
```

A lookup scoped to `psx` would have found it. The probe now prints every matching row, which
is what stops the same mistake being made from its output again.

**And the `-unofficial` platforms are a user's filing scheme, not a RomM behaviour.** They
are how the owner of this library separates demos, prototypes, unlicensed and aftermarket
titles: a folder named `psx-unofficial` becomes a platform whose `fs_slug` is that folder and
whose `slug` RomM resolves to `psx`. So the earlier "36% of firmware sits on unofficial
twins" is a statement about one person's organisation and carries no risk on its own.
Measured properly across all 656 records:

```text
firmware records: 656
  md5 also present on a slug-sibling platform: 504
  md5 present on only one platform row:        152
```

Of the 152 singletons, nearly all sit on the **official** row while the twin lacks them
(`megacd` 17, `nds` 16, `mastersystem` 14, `3do` 13, `nes` 12), which is harmless: a lookup
scoped to the platform the ROMs came from finds them. **Exactly 3 records sit on an
`-unofficial` row whose sibling lacks them**, all on `channelf-unofficial`, and that platform
has no official sibling at all, so a scoped lookup finds those too.

What survives, then, is smaller and different from what I first wrote:

- Reading `/api/platforms` once is **cheaper** than one request per platform, which is what
  F5 measured. It is not a correctness fix, and the plan has been corrected to say so.
- **The same md5 legitimately appears on several platform rows**, 504 of 656 times here. A
  global md5 join therefore returns several hits per required file, and the client must
  dedupe on md5 and take any one rather than treating multiplicity as ambiguity or
  downloading each.
- The twins are **user-created and unpredictable in number and naming**, which strengthens
  the existing rule to key on `fs_slug` and `id` and never on `slug`.

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

### F17: the `.rvz` game-ID offset is real, and the `.iso` path it sits beside is dead here. **Confirmed, live**

M6's attribution fallback, for a save with no observed launch, is to read the Game ID out of
the ROM. Freegosy reads 4 ASCII bytes at offset `0x00` for an `.iso` and **`0x58`** for an
`.rvz`. The first is the documented GameCube and Wii disc header layout; the second is
container-specific and was the one worth checking.

**No disc image was downloaded.** M3 established that a single-file ROM accepts a bounded
`Range`, so `f17-disc-header-offsets.py` reads 256 bytes of a real image off the server:

```text
.rvz  rom id 304465 (gamecube)  1.16 GB on the server
  GET content with Range: bytes=0-255 -> 206, Content-Range bytes 0-255/1158389088
  256 bytes read instead of 1158389088
  first 8 bytes: 52 56 5a 01 01 00 00 00  (RVZ.....)
  -> not a raw disc image at offset 0, so this is a container
  .rvz: bytes at 0x58 = 47 57 37 50 ('GW7P'), valid game-code shape: True

.rvz  rom id 306687 (wii)  0.18 GB on the server
  first 8 bytes: 52 56 5a 01 01 00 00 00  (RVZ.....)
  .rvz: bytes at 0x58 = 52 55 55 45 ('RUUE'), valid game-code shape: True
```

**`0x58` holds a well-formed game code on both**, a GameCube PAL title and a USA Wii title,
so the offset transfers. It works because the RVZ header embeds a copy of the original disc's
first bytes for identification; the `01 00 00 00` after the magic is the format version, and
**a future RVZ revision that moves that field breaks the offset**, so check the version before
trusting it.

The census underneath is the more useful half. `f17b-disc-format-census.py` walked **every**
ROM on both platforms:

```text
=== gamecube: 1793 roms walked ===
  .rvz         1792  (99.9%)
  .(none)         1  (0.1%)

=== wii: 189 roms walked ===
  .rvz          148  (78.3%)
  .wad           33  (17.5%)
  .(none)         8  (4.2%)
```

**Not one `.iso` in 1,982 ROMs.** The offset-0 path both Freegosy and this plan reach for is
correct in principle and never exercised on this library, so a client that handled only `.iso`
would resolve nothing at all for GameCube and would silently read `RVZ.` as a game code.

**And `.wad` is a third container neither client handles.** 17.5% of the Wii library, and it
has no disc header anywhere:

```text
bytes 0..31: 00 00 00 20 49 73 00 00 00 00 0a 00 00 00 00 00 ...
header size = 0x20, wad type = 'Is'
0x18/0x1C disc magic present: False False
```

A WAD is an installable Wii title, and its title ID lives inside the ticket, whose offset
depends on the preceding certificate-chain size rather than being fixed. **It cannot be read
by a constant offset at all**, which makes it the case where the launch-journal correlation
route is not merely preferred but required. That is the route `docs/PLAN.md` already ranks
first; this is a platform where the fallback simply does not exist.

### F19: mastersystem and gamegear are class A. **Confirmed, probe**

Both driven on the test stick under `libretro` / `genesis_plus_gx` by
`f19-f20-battery-on-close.ps1`, which **never presses a save key**, so anything that appears
was written by the emulator alone.

```text
=== mastersystem / libretro / genesis_plus_gx / Phantasy Star (Brazil).zip ===
  baseline: 0 file(s) under saves/mastersystem
  -- while the emulator is still running
     new     Phantasy Star (Brazil).srm      65536 B  06:30:17.597
  closing the emulator with the quit hotkey (Escape)
  exited on its own, so any save-on-exit path ran
  -- after the emulator exits
     new     Phantasy Star (Brazil).srm       8188 B  06:30:41.754
```

A loose `.srm` at `saves/<system>/`, named after the ROM, one level deep. **Class A**, the
same as `nes`, `snes` and `megadrive`. Both are now classified in
`data/retrobat/save_shapes.json` through its generator, and `_unclassified` drops from 23
entries to 21.

**RetroArch names the destination in its own log even on a run that writes nothing**, which
is what classified `gamegear` despite its cart never being touched:

```text
[Override] Redirecting save file to "<root>\saves\gamegear\Defenders of Oasis (USA, Europe) (Virtual Console).srm".
[SRAM] Skipping SRAM load.
```

That log line is a better source than the file, because it states the intent rather than the
outcome. Worth reusing wherever an emulator has to be classified without a real save.

**One honest gap.** The Game Gear run's exit had to be forced, twice, because the Escape
hotkey did not close it, so the probe declared its own F20 half void for that run and it is
recorded as void rather than quietly folded in. The shape answer stands because it does not
depend on how the process ended.

### F20: a launch alone writes a battery save, and no size floor can catch it. **Confirmed, probe. Freegosy's guard is refuted**

The Master System run above is the whole finding. **The game was booted and left at its title
screen. No save key was ever sent. No progress of any kind was made.** It still produced a
save file, twice: 65,536 bytes while running and 8,188 bytes after a clean exit.

The mid-run write is not incidental. `retroarch.cfg` on this install carries

```text
autosave_interval = "10"
```

so the SRAM buffer is flushed every ten seconds, which means **the file exists within seconds
of boot** and survives a crash or a forced kill. Waiting for a clean exit is not a protection.

What the file actually contains settles the design question:

```text
00000000: 5048 414e 5441 5359 2053 5441 5220 2020  PHANTASY STAR
00000010: 2020 2020 2020 4241 434b 5550 2052 414d        BACKUP RAM
00000020: 5052 4f47 5241 4d4d 4544 2042 5920 2020  PROGRAMMED BY
```

That is the cartridge formatting its own backup RAM at boot. **Freegosy's answer to this
hazard is a 100-byte minimum upload size**, and this file defeats it comfortably: 8,188 bytes,
35 distinct byte values, legible ASCII. A blankness test fails too, since the content is
neither all `0x00` nor all `0xFF`. **Nothing about the file in isolation distinguishes
"the cart formatted itself" from "the player saved."**

So the size floor is not a smaller version of the right answer, it is the wrong instrument.
The only thing that separates the two cases is comparison against a previously known state,
which is what `content_hash` already provides. The consequence for M6 is narrow and real:
**the first save seen for a ROM with no local baseline is not evidence that anything was
played**, so it must not win a conflict against a server save on recency alone.

This also widens a hazard the plan had scoped to class D. M0 measured a PS2 launch rewriting
both shared memory cards with no in-game save, and the plan concluded that **class D**
therefore needs content hashing rather than mtime. The same is now measured for **class A**
on an ordinary battery cart, so "mtime cannot decide whether this needs uploading" is a
general rule, not a shared-container one.

### F22: a device-bound token really does make `device_id` optional. **Confirmed, live**

The earlier run left this half open because the probe token was an ordinary client token.
Pairing the test install minted a device-bound one, which closes it:

```text
device-bound token, NO device_id   -> 200  session 142, 1 operation
device-bound token, WITH device_id -> 200  session 143, 1 operation
```

Identical outcomes. The 400 seen earlier (`device_id is required (either in the request
payload or implicit via a device-bound client token)`) is specific to a token with no device
behind it. RomMBat's token comes from pairing, so it may omit the field; sending it anyway is
harmless and more explicit, which is what the plan already does.

### F18: multi-disc is per emulator, the stock memory card mode holds a set together, and the conversion this plan recommends is what would break it. **Corrected twice, probe. It corrects this document's own F16 reading, then its own first answer**

F16 concluded that a later milestone "has to build the `.m3u` itself, from the member names".
That is true and it is not nearly enough. Three things had to be measured before the shape of
the problem was visible, and a real `psx` folder holds three different layouts at once:

```text
roms/psx/
  Spyro the Dragon (USA).chd                        single disc
  Final Fantasy VII (USA) (Disc 1|2|3).chd          three loose discs, no playlist
  Metal Gear Solid (USA) (Rev 1)/                   a folder holding
    Metal Gear Solid (USA) (Disc 1) (Rev 1).chd
    Metal Gear Solid (USA) (Disc 2) (Rev 1).chd
    Metal Gear Solid (USA) (Rev 1).m3u              two bare filenames, one per line
```

RetroBat's wiki documents a fourth, the `.m3u` flat in `roms/psx/` beside the discs. So the
playlist may or may not exist, and may or may not sit in a folder with its discs.

**The first answer here was wrong, and a real card refutes it.** Reading `gamedb.yaml` alone,
the disc number is plainly in every title (`name = 'Metal Gear Solid (Disc 1)'`,
`'Final Fantasy VII (Disc 2)'`, and so on), from which this document concluded that
`PerGameTitle` splits a set, `PerGame` splits it on the serial, `PerGameFileTitle` splits it
hardest of all, and only `Shared` holds it together. **That conclusion did not survive the
first card that appeared on disk.** The two-disc Metal Gear Solid set, launched once through
its `.m3u` and played until the game saved:

```text
saves/psx/duckstation/memcards/
  Metal Gear Solid (USA)_1.mcd    131072 B   124 distinct byte values
  Metal Gear Solid (USA)_2.mcd    131072 B    14 distinct byte values
```

**One card for a two-disc set, and `_1` / `_2` are the two console slots, not the two discs.**
Both files appeared in the same second at boot; only `_1` was rewritten at the moment the
player saved, and `_2` still holds a formatted empty card. So the set is unified, and the
mode that unified it is the stock one, straight out of the generated `settings.ini`:

```text
[MemoryCards]
Card1Type=PerGameTitle
Card2Type=PerGameTitle
UsePlaylistTitle=true
```

**The card stem is `saveName` with the disc marker removed.** It is worth being exact about
which of the three candidate strings it matched, because all three were live:

```text
gamedb name      Metal Gear Solid (Disc 1)          strip the disc marker -> Metal Gear Solid
gamedb saveName  Metal Gear Solid (USA) (Disc 1)    strip the disc marker -> Metal Gear Solid (USA)   <- the card
rom / m3u stem   Metal Gear Solid (USA) (Rev 1)
```

The card carries `(USA)`, so it is not from `name`. It carries no `(Rev 1)`, so it is not from
the filename either, which also means `UsePlaylistTitle=true` does not mean "name the card
after the playlist file". It means DuckStation resolves the playlist to a single disc set and
then names it from the database. **Regions stay separate and discs collapse together**, which
is the combination a user would choose deliberately.

`f18d-psx-save-tree.py` checks that mechanically against all 10,764 database entries rather
than by eye, and the result is stronger than the reasoning above: the card's stem resolves to
**both** of the set's serials at once, which is the unification itself rather than an inference
from it.

```text
Metal Gear Solid (USA) (Rev 1).srm
   stem: 'Metal Gear Solid (USA) (Rev 1)'
   matches a rom or playlist filename : True
   matches no gamedb title
duckstation/memcards/Metal Gear Solid (USA)_1.mcd
   stem_<slot>: 'Metal Gear Solid (USA)'
   matches a rom or playlist filename : False
   matches gamedb saveName with the disc marker removed: SLUS-00594
   matches gamedb saveName with the disc marker removed: SLUS-00776
```

Those two lines are the whole story in miniature. **The libretro card is keyed on a filename
and matches no database title; the DuckStation card is keyed on a database title and matches no
filename.** One system, one game, one session, and the two emulators do not share a single
naming input.

That inverts the cost. `PerGameTitle` is the stock default and it already holds a set
together; `PerGameFileTitle`, which `docs/PLAN.md` recommends precisely because it keys on the
rom file, is the change that would split one. Applying the recommended conversion to a
multi-disc PS1 title makes the save disappear at the disc change, and the stock configuration
that the conversion was meant to improve on does not have that failure.

**And `.m3u` support cannot be inferred from the extension list.** This is the trap, and it
caught this probe before it caught anyone else. `f18b-m3u-support-census.py` over the live
`es_systems.cfg`:

```text
systems declaring a <path>: 243
  list .m3u in <extension>:     44
  do not list .m3u:            199

  psx   .7z .cbn .ccd .chd .cso .cue .img .iso .m3u .mdf .pbp .squashfs .toc .zip
  ps2   .7z .bin .chd .cso .gz .iso .m3u .mdf .squashfs .zip
```

**`ps2` lists `.m3u`.** RetroBat's own wiki says of PCSX2: _"PCSX2 does not support m3u usage
for multi-disc games"_, and directs the user to the emulator's quick menu to change discs
instead. So EmulationStation will index the playlist, `emulatorLauncher` will hand it over,
and the emulator will not understand it. `<extension>` is the authority on **what ES offers**,
never on **what the emulator can play**.

That qualifies a rule this plan leans on hard. "File extensions come from RetroBat, never
from RomM" remains right, and the extension list remains necessary, but it is **not
sufficient**: passing the filter is not evidence a file will launch. The failure it produces
is the exact one M2 wrote the filter to prevent, a game that appears in EmulationStation,
looks correct, and dies.

Eight disc-based systems do not list `.m3u` at all, so a set there is always N entries:
`3do`, `amigacd32`, `atomiswave`, `cdi`, `naomi`, `psp`, `wii`, `xbox`. Note `gamecube` lists
it and `wii` does not, though both are Dolphin.

**What this costs the plan.** Both of M6's per-game memory card conversions turn out to be
unsafe on a multi-disc title, for the same reason and with the same fix.

M6 recommends `pcsx2_slot1_memory=game` to convert PS2 out of class D, keyed by rom basename.
For a **multi-disc PS2 game that option destroys the save at the disc change**, because PCSX2
cannot bind the discs and each basename gets its own card, where the stock shared
`Mcd001.ps2` would have carried it through. M6 also recommends
`duckstation_memcardtype=PerGameFileTitle` for PS1, on the reasoning that keying by rom file
is more predictable than keying by an emulator's internal title. Measured, that trade is a bad
one: the internal title is what binds a disc set, and the rom file is what breaks it apart.

So neither conversion can be a per-system decision. Both are right for single-disc titles and
wrong for multi-disc ones, which the `<system>["<rom>"]` override form already allows
expressing per game. For PS1 specifically the better default is to **apply no conversion at
all** and read the stock `PerGameTitle` layout, which needs database-backed attribution rather
than filename attribution but does not lose saves.

**Revisions share a card and regions do not, which is the combination attribution needs.**
DuckStation's database carries two naming fields that behave differently, and the card above
settles which one is in play. For the eight Metal Gear Solid disc-1 releases in it:

```text
serial          name                                saveName
SLUS-00594      Metal Gear Solid (Disc 1)           Metal Gear Solid (USA) (Disc 1)
SLES-01370      Metal Gear Solid (Disc 1)           Metal Gear Solid (Europe) (Disc 1)
SLES-01506      Metal Gear Solid (Disc 1)           Metal Gear Solid (France) (Disc 1)
SLES-01507      Metal Gear Solid (Disc 1)           Metal Gear Solid (Germany) (Disc 1)
SLES-01508      Metal Gear Solid (Disc 1)           Metal Gear Solid (Italy) (Disc 1)
SLES-01734      Metal Gear Solid (Disc 1)           Metal Gear Solid (Spain) (Disc 1)
SLPM-86114      Metal Gear Solid (Disc 1) (Ichi)    Metal Gear Solid (Japan) (Disc 1) (Ichi)
```

**`name` collapses six releases onto one string. `saveName` separates all of them.** Across
the whole database `saveName` is very nearly a key: 10,081 entries, 10,074 distinct values,
7 collisions and all of them demo or duplicate-serial oddities.

Which field the card is named from decides the attribution story, and it is `saveName`:

- had it been **`name`**, a user holding the USA and five European releases would get **one
  shared card**, a French save and an American save would land in it together, and no card
  could be attributed to a single `rom_id`
- keyed on **`saveName`**, which is what the card measured, each regional release gets its own
  card and attribution is very nearly one-to-one

**Revisions behave well either way.** `Metal Gear Solid (USA) (Rev 1)` carries serial
`SLUS-00594`, the same as the base USA release, so it is one database entry and one card, and
the card that appeared carries no `(Rev 1)`. That is the behaviour a user wants: a revision
inherits its saves.

**What is still not measured is the loose layout, and it is the one RomM produces.** The set
above was launched through a `.m3u`. Whether `PerGameTitle` also unifies three discs launched
individually with no playlist, the Final Fantasy VII layout sitting in the same folder, was not
driven. The evidence leans towards yes, because the card was named from the database rather
than from the playlist file, so the same lookup should resolve each loose disc to the same
disc set. That is a reading of one observation, not a second observation, and the layout it
concerns is exactly the one a RomM sync creates. **It should be driven before M6 commits.**

**The same game under two emulators, and one naming rule does not cover either of them.** The
run also drove the set under `libretro mednafen_psx_hw`, so one title produced two complete and
disjoint save sets:

```text
saves/psx/
  Metal Gear Solid (USA) (Rev 1).srm                          131072 B   libretro card
  Metal Gear Solid (USA) (Rev 1).ldci                             163 B   disc index
  duckstation/
    Metal Gear Solid (USA) (Rev 1)_01.sav                    1975732 B   state
    Metal Gear Solid (USA) (Rev 1).txt                            10 B   serial sidecar
    memcards/Metal Gear Solid (USA)_1.mcd                     131072 B   card
    memcards/Metal Gear Solid (USA)_2.mcd                     131072 B   card, empty
  libretro.mednafen_psx_hw/
    Metal Gear Solid (USA) (Rev 1).state1                    1774123 B   state
    Metal Gear Solid (USA) (Rev 1).state1.png                  30175 B   screenshot
```

Three things in that tree are worth carrying into M6:

- **Both cards are PS1 memory cards** and both open with the `MC` magic, so the same in-game
  progress now exists twice in two formats under two names. Nothing can merge them, which is
  what the per-emulator attribution design already assumes.
- **DuckStation uses two different keys at once.** Its memory card is named from the database
  (`Metal Gear Solid (USA)`) and its save state is named from the rom file
  (`Metal Gear Solid (USA) (Rev 1)_01.sav`). One emulator, one game, one run, two naming rules.
  Attribution cannot resolve a system with a single rule per emulator.
- **The `.txt` beside the state holds exactly `SLUS-00594`** and nothing else. RetroBat is
  writing a file to serial mapping into the save tree for free, which is the join key that
  database-named cards otherwise need to be reverse engineered from.

The libretro side unifies the set too, but by a different mechanism: the `.srm` is named from
the `.m3u` stem, so the playlist binds the discs by filename where DuckStation binds them by
database lookup. A libretro state is also a **two-file unit**, `.state1` plus a real
`.state1.png` screenshot, which the bundling rules have to keep together.

One correction to the bundled data on the way past: the generated
`emulators/duckstation/settings.ini` puts memory cards at
`saves/psx/duckstation/memcards`, a **third** level, where
`data/retrobat/save_directories.json` records only `psx/duckstation`.

### Six incidental traps, all measured

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
- **RetroArch writes an absolute path into the save tree.** A multi-disc launch leaves
  `saves/<system>/<playlist stem>.ldci` recording which disc was in the drive, and the path it
  records is absolute, drive letter and all:

  ```json
  {
    "version": "1.0",
    "image_index": 0,
    "image_path": "<root>\\roms\\psx\\Metal Gear Solid (USA) (Rev 1)\\Metal Gear Solid (USA) (Disc 1) (Rev 1).chd"
  }
  ```

  This is the portable-install rule being broken by an upstream file, inside the directory
  RomMBat is going to sync. Round-tripping it through RomM to a machine whose RetroBat sits on
  another drive restores a dangling pointer. It is small, it is JSON, and its `image_index` is
  genuinely worth preserving, so the choice is to exclude it or to rewrite `image_path` on
  restore. It cannot simply be copied.

- **The emulator creates an empty second memory card, and size cannot tell it apart from a
  real one.** A PS1 launch produces a card per console slot whether or not the game ever
  touches slot 2, and both are exactly 131072 bytes. Only the byte histogram separates them:
  124 distinct values in the card that holds a save against 14 in the formatted empty one.
  Uploading on file size, or on existence, ships an empty card as if it were progress.

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

## The state of the test RetroBat install

F18, F19 and F20 all need a real install driven far enough to see a file. The USB tree is
RetroBat `8.2.0-stable-win64` on NTFS, and it started this session unable to answer any of
them:

- **`roms/` was nearly empty**: content in `halflife`, `jaguar`, `jaguarcd`, `msx1`, `ports`
  and `sonic-mania` only.
- **Only RetroArch is installed.** Every other emulator is a folder shell with no executable:
  `duckstation`, `desmume`, `melonds`, `dolphin-emu`, `azahar`, `cemu`, `xenia`, `ryujinx`
  and `pcsx2` are all 0 MB. RetroBat downloads them on demand, and M0 already measured that
  doing so raises **a modal dialog with no title and no timeout** that blocks the launch until
  someone answers it.

**That decided which of the three got answered.** RetroArch needs no install and no dialog,
so F19 and F20 were re-aimed at the systems it already serves, which turned out to be the
better target anyway: `mastersystem` is a **wave 1** platform in the rollout order and its
shape was still a guess, while the 3DS, Switch, Wii U and Xbox 360 systems the original lead
named are not certified for a long time yet.

Staging was done with the agent itself, which exercised M3 against a real target in passing:
two `--scope filter` sets capped at one game each resolved, downloaded and landed correctly,
and the extension filter excluded an `.xiso.iso` from the Master System set on the way
through. **F18 was answered afterwards**, once DuckStation was installed through that dialog
and a multi-disc set was placed by hand, by a person playing Metal Gear Solid until the game
wrote a card. It could not have been answered any other way: the card is created when the game
first touches it, so a timed unattended launch produces nothing to read.

## What stays open

- **F13**, whether `GET /api/saves/identifiers` scales. It answers in 0.07 s on an empty set
  and takes no parameters, which is the shape that made `/api/roms/identifiers` unusable. No
  library here has enough saves to load it.
- **Half of F18**, whether `PerGameTitle` also unifies a set whose discs are loose in
  `roms/psx` with no `.m3u` to bind them. That is the layout a RomM sync produces, and it is
  the only untested combination left: the set that was driven had a playlist. The measured card
  was named from the database rather than from the playlist file, which is a reason to expect
  the same disc-set lookup to resolve each loose disc identically, but expecting is not
  measuring. One unattended launch of Final Fantasy VII disc 1 answers it.
- **The 21 systems still unclassified in `save_shapes.json`.** F19 closed `mastersystem` and
  `gamegear`; the rest divide into ones RetroArch can answer cheaply (`fbneo`, `msx1`,
  `supergrafx`, `amiga`, `amstradcpc`, `apple2`) and ones needing a standalone emulator
  installed first (`3ds`, `nds`, `switch`, `wiiu`, `xbox360`, `psvita`, `naomi`, `atomiswave`).
  Freegosy supplies a hypothesis for several of the second group, but it runs different
  emulators in a different layout, so **those stay hypotheses to test and none has been
  written into `save_shapes.json`.**
