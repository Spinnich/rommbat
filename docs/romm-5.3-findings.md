# RomM 5.3.0 findings

What RomM 5.3.0-alpha.2 changes for RomMBat, what it falsifies in this repo, and what it is
too early to say. Written in the shape of [retrobat-findings.md](retrobat-findings.md), and
it carries the same warning with one extra clause.

**Most of this was read rather than measured.** Every row below was settled by reading upstream
source or upstream's own release notes, and this repo's standing rule is that a changelog line
is upstream's belief rather than a measurement. Rows are labelled by the route that settled
them. A row marked `source` was read in `rommapp/romm` and is a fact about upstream's code. A
row marked `notes` is upstream's claim and nothing more. **Neither is yet a fact about
behaviour**, and no workaround comes out of this repo on the strength of one.

The exception is labelled `measured`, taken against a live `5.3.0-alpha.2` server: section C
first, then findings 2, 5, 7 and 9 in stage 2 and findings 3 and 4 in stage 4. Only those have
standing to change code, and a finding labelled `source, then measured` changes code only on the
half that was measured.

|                        |                                                                                      |
| ---------------------- | ------------------------------------------------------------------------------------ |
| Release                | `5.3.0-alpha.2`, 2026-09-13, adopted; `5.3.1`, 2026-09-23, the floor since           |
| API read at            | tag `5.3.0-alpha.1`, plus the `alpha.1` to `alpha.2` delta, see below                |
| Vendored files read at | `master`, because `reference/refresh.sh` fetches the default branch and takes no ref |
| Floor before this      | RomM `5.2.0`, pin `romm-5.2.0.json`, RetroBat `8.2.1`                                |
| Measured against       | `5.3.0-alpha.2`, one live server; finding 11's retention table at `5.3.0-alpha.3`    |
| Date                   | 2026-09-13                                                                           |

## Three things are already true, before any adoption decision

### A. `reference/refresh.sh` is broken against upstream today

`refresh.sh` fetches `examples/config.batocera-retrobat.yml` from the default branch with no
`ref`, and that file is the seed for the platform map. Upstream gutted it. The 167 explicit
pairs [reference/README.md](../reference/README.md) counts are now **4 overrides**:

```yaml
system:
  # Batocera and RetroBat folder names are recognised automatically. The entries
  # below override folders RomM would otherwise match to their own platform.
  platforms:
    atari800: atari8bit
    model2: arcade
    model3: arcade
    pico: sega-pico
```

Confirmed on both the tag and `master`. Anyone running `refresh.sh` now gets a 4 entry seed,
`tools/build-platform-map.py --check` fails, and all four numbers in the mapping table
(167 pairs, 91 unmapped, 18 stale, 13 many to many) stop meaning anything.

**This is not drift to absorb, it is an authority that moved.** Per `CLAUDE.md`, a drift in
`reference/` is a signal to revisit the design, and the design answer here is to re-seed from
where the knowledge went rather than to accept the collapse or to freeze the old file.

**Resolved in stage 1 (#166), and it was three failures rather than one.** The seed was the
known one. The second is that `refresh.sh` also extracted the slug enum from
`backend/handler/metadata/base_handler.py`, which no longer holds it: upstream moved
`UniversalPlatformSlug` to `backend/utils/platform_slugs.py`, so the `grep` matched nothing,
and under `set -o pipefail` that killed the script before it reached the seed at all. The third
is `verify.py`'s `companies` assertion, which is #171 and is untouched here.

The re-seed walks **RetroBat's** system list and asks upstream's own rules what each folder
resolves to, rather than importing the alias table and correcting it, so the 44 keys naming
folders RetroBat lacks are never seen. Re-derived, not adjusted: 166 folders resolve (72 by
identity, 94 by alias), 74 are unmapped, normalization rescues 1 of those where it used to
rescue 16, and 10 slugs fan out where 13 did. Two slugs the old seed carried, `daphne` and
`rpgmaker`, are not `UniversalPlatformSlug` values at all and could never have matched a row.

**Four entries left the table, and two of the four name real slugs.** `odyssey` and `atari8bit`
are `UniversalPlatformSlug` values, so "two slugs RomM never had" is not the whole accounting.
`odyssey` is a seed error corrected: Magnavox Odyssey is not the Odyssey², and `odyssey-2` →
`odyssey2` now carries the real case. `atari8bit` is upstream's suggested binding for `atari800`,
recorded and not applied, so layer 2 covers it wherever the RomM folder is itself named
`atari800`; a RomM folder RetroBat lacks (`atari-8bit`, `a800`) that resolves to `atari8bit` has
no layer-3 entry, cannot be reached by normalization, and needs a manual mapping. Accepted, and
recorded in the `platform-mapping` skill.

**One slug changed which folder wins.** `model2` resolved to `["lindbergh"]` and now resolves to
`["model2", "lindbergh"]`. `lindbergh` is not a RomM slug, so the old first choice could only
ever have come from the seed; a sync set scoped to `model2` wrote into `roms/lindbergh` and now
writes into `roms/model2`, which relocates games for anyone who already synced it. Unlike
`arcade`, `model2` is not in `REQUIRES_EXPLICIT_CHOICE`, so it auto-resolves and the change is
silent. Kept, as a seed error corrected.

### B. 5.3.0 servers already meet today's client

Above `LastTested` RomMBat warns and continues, so a 5.3.0 server and a 5.2.0 floor client
are a combination that exists right now, in the field, regardless of when the floor moves.
Anything 5.3.0 adds that today's code mishandles is a **bug against the current floor**, not
adoption work that can wait. Finding 6 is the one that bites.

`ProductVersion` handles `5.3.0-alpha.2` correctly and on purpose: it compares numeric
components and ignores the suffix, so the alpha ranks as `5.3.0` and lands above `LastTested`
in the warn band. That is the documented lenient direction. **Do not "fix" it to be
semver-strict**, which would rank every prerelease below its release and, more to the point,
would refuse every stock RetroBat install whose suffix names a channel.

### C. Multi-file ROMs answer a `Range` now, and the two answers are different files (`measured`)

**Measured, not read.** `LiveContentTests.A_range_on_a_multi_file_rom_is_refused_which_is_why_none_is_sent`
fails against a live `5.3.0-alpha.2`, which is the job that test was written to do: it is kept as
a test rather than as prose "so a server that changes its mind is noticed here instead of in the
field". The server changed its mind.

The rule it encodes is a 5.2.0 measurement, recorded at `PLAN.md:1226` and in the `romm-api`
skill: **any `Range` on a multi-file ROM is refused 403 by nginx**, so multi-file is not
resumable by any header. Re-measured on one multi-file ROM, `neogeocd`, repeated and stable:

| Request               | Status | `Content-Length` | `ETag`              | `Accept-Ranges` |
| --------------------- | ------ | ---------------- | ------------------- | --------------- |
| no `Range`            | 200    | 2,740,790        | absent              | absent          |
| `Range: bytes=0-`     | 206    | 2,740,768        | `"6aa70200-29d220"` | implied by 206  |
| `Range: bytes=0-1023` | 206    | 1,024            | same                | implied by 206  |

The 403 is gone and multi-file is range-served. **The interesting part is the 22 byte
difference.** The two responses to one URL describe different resources: only the ranged one
carries a validator, and `Content-Range` totals 2,740,768 against the plain response's
2,740,790. Both figures are stable across repeated requests, so this is not a timestamp in a
zip rebuilt per call. Range semantics assume one representation, and here there are two.

**This is finding B again, and it is the sharper case.** A 5.3.0 server already meets today's
client, so this is a bug against the current floor rather than adoption work. **Today's code is
still correct, and its stated reason is now wrong.** `RomMConnection.Content.cs:86` gates the
header on `!request.IsMultiFile` and the comment above it gives the 403 as the reason, which is
the thing that stopped being true. The behaviour must not change on the strength of the refusal
having gone: a resume that begins on the plain response and continues on the ranged one splices
two artifacts, and the plain response carries no `ETag`, so the stale-validator restart that
protects the single-file path has nothing to compare against here.

**Single-file is unaffected**, re-checked on a 47 GB `.iso` rather than inferred: the plain 200
carries `ETag`, `Accept-Ranges: bytes` and a `Content-Length` of 47,976,480,768, and the 206
reports the same `ETag` and the same total. The two representations agree, which is nginx
serving one file from disk, and it is the contrast that makes the multi-file mismatch a finding
rather than a quirk of the measurement.

**Re-measured across a second platform in stage 1**, which #180 asked for before anything
acted on the first reading. It reproduces, and it is worse than "22 bytes" suggested:

| ROM                      | plain 200 `Content-Length` | ranged `Content-Range` total | gap |
| ------------------------ | -------------------------- | ---------------------------- | --- |
| `neogeocd`, rom 272137   | 2,740,866                  | 2,740,768                    | 98  |
| `pcenginecd`, rom 272627 | 9,439,703                  | 9,439,567                    | 136 |

**The gap is per-ROM, not a constant.** The plain length is stable across repeats within a
session (three requests, 2,740,866 every time), so it is still not a per-call rebuild, but the
first neogeocd reading above recorded 2,740,790 for that platform and this one reads 2,740,866,
so it is not stable across days either. Neither figure is retracted; both are what was served
when they were taken.

**The mechanism is now visible in the `ETag`.** nginx's validator is `hex(mtime)-hex(size)`, and
its size half is exactly the ranged total in both rows: `0x29d220` is 2,740,768 and `0x90094f` is
9,439,567. So the ranged answer is **nginx serving a zip that exists on disk** while the plain
answer is **one the application builds for the request**. They are two artifacts by construction,
which is why no amount of retrying will make them agree and why the mismatch, not the withdrawn
403, is the durable reason not to send the header.

**Acted on in stage 1 (#180).** The test is re-aimed rather than made version-aware: it now
asserts that the two answers are not interchangeable, which a 403 satisfies and a mismatched 206
satisfies, and it fails loudly on the day a server makes them agree. Four comments that gave the
403 as their reason now give the mismatch, in `RomMConnection.Content.cs`, `RomContent.cs`,
`RomRow.cs` and `SyncSetStore.cs`. Multi-file resume is **still not built**: it would need the
transfer pinned to the ranged representation for its whole life, and the plain response offers no
validator by which a mix could be detected.

**Owed:** the 5.2.0 reading stays where it is, labelled, per the version move checklist's rule on
provenance, and both sites name this section.

## Findings

### 1. The platform folder authority moved into source (`source`)

`backend/utils/platform_aliases.py` is the new home:

| Symbol                                | What it is                                                                                                                    |
| ------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `PLATFORM_FS_ALIASES`                 | 138 folder name to slug entries, for folder names that differ from RomM slugs                                                 |
| `PLATFORM_SLUG_FOLDERS`               | the reverse map, built by `_unambiguous_folders()`                                                                            |
| `resolve_platform_slug(fs_slug, cfg)` | config binding first, then identity if the folder name is itself a slug, then the alias table, then the folder name unchanged |
| `resolve_fs_slug(slug, cfg)`          | slug back to folder, `None` when nothing maps or several folders collapse onto it                                             |

Two structural points fall out, and both matter more than the entry count.

**The identity cases became implicit.** `resolve_platform_slug` returns `key` when the folder
name is already a valid `UniversalPlatformSlug`, which is why `3do: 3do`, `nes: nes` and the
rest left the YAML. Reconstructing the full mapping means the alias table **plus** the slug
enum, and this repo already vendors the enum as `reference/romm-slugs.txt`. So the re-seed
source is strictly better than the file it replaces: it is the code RomM runs, not an example
nobody executes.

**The table is a three way union, and 44 of its keys are not RetroBat.** Counted against
`reference/systems_names.lst`:

|                                                  |     |
| ------------------------------------------------ | --- |
| Alias keys                                       | 138 |
| Of those, names that are RetroBat system folders | 94  |
| Of those, names RetroBat does not have           | 44  |

The 44 are ES-DE and Batocera spellings (`atarijaguar`, `atarilynx`, `atarixe`, `gc`,
`megadrivejp`, `cdimono1`, `fba`, `mame-advmame`). RetroBat's own names for those are
`jaguar`, `lynx`, `xegs`, `gamecube`, `megadrive`. **Core principle 3 is unchanged by this**:
RetroBat's live `es_systems.cfg` remains the authority on what folders exist, and upstream's
table is a better seed for what they map to. Feeding the 44 into our map unfiltered would put
folders in it that no RetroBat install has.

`_unambiguous_folders()` dropping many to one collapses independently confirms the existing
finding that the mapping is many to many, and `resolve_fs_slug` returning `None` for `arcade`
is upstream reaching the same conclusion this repo did.

**Owed:** `refresh.sh` re-sourced, `tools/build-platform-map.py` re-pointed, `verify.py`'s
four numbers re-derived rather than edited, the mapping table in `reference/README.md` and the
`platform-mapping` skill corrected.

### 2. A ROM identity triple lands on the ROM schema (`source`)

`DetailedRomSchema` gains **three** fields, not two, and all three are declared columns readable
in source rather than a claim in a release note:

| Field                | Declared at                                               |
| -------------------- | --------------------------------------------------------- |
| `title_id`           | `backend/models/rom.py:806`, response schema `rom.py:407` |
| `save_target`        | `backend/models/rom.py:810`, response schema `rom.py:408` |
| `save_target_layout` | `backend/models/rom.py:814`, response schema `rom.py:409` |

Upstream carries them as one unit, `RomIdentity` at `backend/models/rom.py:153-155`, described
there as "the triple that travels from the scan through to the `Rom` columns of the same names".
`save_target_layout` is an enum, `SaveTargetLayout` at `backend/models/rom.py:137-142`:

```python
class SaveTargetLayout(enum.StrEnum):
    FOLDER_EXACT = "folder-exact"
    FOLDER_PREFIX = "folder-prefix"
    FILE_EXACT = "file-exact"
    FILE_PREFIX = "file-prefix"
    FOLDER_SPLIT = "folder-split"
```

The values are extracted from game binaries during scan, gated on `TITLE_ID_EXTRACTION_ENABLED`
in the heartbeat. Upstream names PSX, PS2, PS3, PSP, PS Vita, Switch, 3DS, Wii, Wii U, GameCube,
Dreamcast, Xbox and Xbox 360, and says the purpose includes determining save locations for
device sync.

**Two different things are deferred here, and only one of them is unmeasured.** That the columns
exist, and what shapes `save_target` can take, are settled in source today. Whether any real
library actually carries values in them, and whether those values agree with what this repo
measured, needs a live instance and is what #168 gates. Filing the first as belief is what left
the falsified premise below standing unqualified.

**This falsifies a claim this repo repeats in four places.** "RomM stores no serial, title ID
or product code anywhere, so no API lookup exists" appears in
[save-sync](../.claude/skills/save-sync/SKILL.md) and at `PLAN.md` lines 2602, 3699 and 3957.
It was true at 5.2.0 and it is the premise the whole attribution design rests on. **All four now
carry a one-line qualifier naming this finding**, because `save-sync` is the file a later session
loads and this document is linked from nothing.

The complement is close to exact. This repo measured header reads at GameCube 100%, Wii 75.5%,
and **0% of PSP, PS3 and PSX**, because no constant offset reaches a `.cso`, a `.chd` or an
ISO9660 filesystem. Upstream is claiming coverage of precisely the systems where our own route
returns nothing.

**What this is not.** It is not a replacement for the three existing routes, and the existing
design already says why: routes are asked in parallel because their agreement is the only
evidence a binding has, and disagreement fails closed. `title_id` becomes a fourth route with
the same standing as the others. It is also capability gated, so a server with extraction off,
or a library scanned before the feature existed, answers nothing. The header and sidecar
routes stay regardless of what the measurement shows.

**`save_target_layout` names the same distinctions this repo measured, which makes the
comparison a real one.** `save-sync` records that psp's key is a _prefix_ of the segment
(`ULES01513SYSDATA`), that `ps3` keeps three directories under one title id, and that `gamecube`
has no per-game directory at all. Upstream's five members read as the same taxonomy arrived at
independently, and independent agreement is worth more than either reading alone. The open
question is therefore sharper than "what shape is `save_target`": it is **whether the layout
upstream assigns agrees, per system, with the shape this repo measured**, and a disagreement is
the interesting case rather than a tie break.

**Owed, and in this order:** measure coverage on a real library first, then decide. Nothing in
`save-sync` changes on the strength of a release note.

**Measured, 2026-09-16, by rescanning named rows rather than reading a library-wide fraction.**
The first reading was confounded: extraction runs during a scan and `_should_extract_title_ids`
re-reads only a row carrying no id, so a platform scanned before the feature existed reads 0%
whatever the extractor can do. `should_scan_rom` honours a `roms_ids` list, so the experiment is
a handful of named rows per system rather than a platform of 9,196. **28 of 33 rows answered.**

| System    | Container          | Result | `title_id`           | `save_target`       | Layout          |
| --------- | ------------------ | ------ | -------------------- | ------------------- | --------------- |
| psx       | `.chd`             | 3 of 4 | `SLES-00972`         | `SLES-00972`        | `file-prefix`   |
| ps2       | `.chd`             | 4 of 4 | `SCUS-97472`         | `BASCUS-97472`      | `folder-prefix` |
| psp       | `.cso`             | 4 of 4 | `ULES00151`          | `ULES00151`         | `folder-prefix` |
| ps3       | `.dec.iso`, folder | 4 of 4 | `BLUS30443`          | `BLUS30443`         | `folder-prefix` |
| 3ds       | `.zcci`            | 3 of 3 | `00040000000EC400`   | `00040000/000ec400` | `folder-split`  |
| dreamcast | `.chd`             | 3 of 3 | `MK-5100050`         | `MK-5100050`        | `file-prefix`   |
| xbox      | `.xiso.iso`        | 3 of 3 | `MS-100`             | `4D530064`          | `folder-exact`  |
| xbox360   | `.iso`             | 3 of 3 | `4D5307E6`           | `4D5307E6`          | `folder-exact`  |
| switch    | folder             | 0 of 3 | encrypted, see below |                     |                 |
| psvita    | `.zip`             | 0 of 2 | nothing              |                     |                 |

**The premise this repo carried is now falsified in the client's favour.** The measured claim was
0% of PSP, PS3 and PSX "because no constant offset reaches a `.cso`, a `.chd` or an ISO9660
filesystem". That is still true of a **constant offset**, and upstream does not use one: it
parses the container. So the three systems this repo reads nothing from are three the server
reads almost everything from, and the complement is close to exact rather than merely claimed.

**`save_target` is computed, not copied, which is what settles what these fields are for.** Xbox
is the clearest: `MS-100` becomes `4D530064`, which is `ascii("MS")` followed by `100` as a
16-bit hex number, and `TC-003` becomes `54430003` the same way. PS2 prefixes `BA` to reach the
memory card folder PCSX2 creates, and 3ds splits the 16 hex digits in half and lower-cases the
second. A field carrying identity would need none of that. `title_id` is what was read out of the
binary and `save_target` is where saves land, and the two are different questions.

**Where both routes answer they agree exactly.** This repo's header route reads `head[0x58..0x5C]`,
four ASCII bytes. Upstream stores the same four bytes hex encoded: every one of the 1,601 distinct
GameCube ids on the live library is eight hex digits decoding to a printable `A-Z0-9` code, 1,549
leading `G`, 34 `D` and 18 `P`. `47553459` is `GU4Y`. That is independent agreement on the value,
not merely on the taxonomy, and it is the answer #168 asked for third.

**A serial is not unique per ROM, and that is the point rather than a limitation.** On GameCube,
scanned end to end, 1,793 rows carry an id and 1,601 are distinct: 167 ids are shared by 359 rows.
**Two thirds of that is intrinsic and one third is a property of the library it was measured on.**
The instance's multi-disc releases sit as loose files rather than one folder per game, so RomM
holds a row per disc where a foldered library would hold one row with several files. Folding each
disc set back into one game leaves **101 groups over 222 rows, 12% of the platform**, and those
are revisions and re-releases. Both kinds are correct: Disc 1 and Disc 2 share a memory card, and
a patched revision does not move the player's save, so a key that separated either would be
useless for the job it has. The existing first-wins rule already covers it and already gives this
as its reason, and the hash remains what identifies a ROM.

**Re-taken 2026-09-16 with a committed probe**, `tools/romm-5.3-probes/r5-gamecube-title-ids.py`,
because the sample above recorded no method. The first three numbers reproduce exactly: 1,793 rows,
1,601 distinct ids, all 1,601 decoding to a game code (G 1,549, D 34, P 18), 167 shared over 359.
The fold does not quite: removing `(Disc N)` from each name leaves **104 groups over 232 rows**,
13% of the platform, against 101 over 222. The probe lists every group it keeps, and they are the
same kinds the paragraph above names: revisions, `(USA)` against `(USA, Canada)` and `(Korea)`,
retitled re-releases, and two 2-in-1 discs whose disc tag is followed by a title the fold does not
strip. So the difference is the folding rule and not the library, and the conclusion does not
move. Compare a later reading against 104 and 232, which the script can reproduce.

**Read the 12% rather than the 20% when reasoning about the field**, and read the 20% when
reasoning about what a real install will hand the attributor, because loose multi-disc files are
common and RomMBat does not get to require otherwise.

**Three systems answer nothing, for three different reasons.**

- **switch** needs `prod.keys` to decrypt a header, and upstream leaves that out deliberately.
  Not a gap to work around. The 4% that do answer are the family whose files may carry the id in
  the filename, which is why `_should_extract_title_ids` re-reads the Switch family every scan.
- **ps3-psn**, 24 rows and 0 answering, against 170 of 170 on decrypted discs. PSN `.pkg` content
  is a shape the extractor does not cover yet rather than one it fails at.
- **psvita**, 12 rows of `.zip` and 0 answering, which is the container rather than the platform.
- **One row failed where its neighbours did not**: `Metal Gear Solid (Europe) (Disc 1).chd`, a
  single 402 MB `.chd` in the same platform and category as three `.chd` rows that answered. The
  shape explains nothing, so this one is upstream's to explain, and it is reported.

**What this does not change.** It is still a fourth route and not a replacement. It is capability
gated on `TITLE_ID_EXTRACTION_ENABLED`, it answers only for rows scanned since the feature landed,
and the agreement of routes is still the only evidence a binding has. What it does change is the
shape of the answer, because **`PUT /roms/{id}/identity` makes the route run both ways**: on
GameCube and Wii, where this repo reads 100% and 75.5%, RomMBat is the client that endpoint's own
description is about.

### 3. Memory card endpoints are for the browser player, not for us (`source`, then `measured`)

Eleven new endpoints under `/memory-cards`, with versions, sharing and visibility. Read
alongside the Streaming V2 entry, these exist to give container pooled PS2 and GameCube browser
sessions a server managed card.

**That is the opposite direction from RomMBat's model**, which is a real emulator on a real
disk writing real files, and whose class C and class D work converts shared containers to per
game wherever the emulator allows. Memory cards are not our save transport, and this finding
exists mainly so the next session does not re-litigate it.

Two uses were filed as surviving, both narrow: #82's raw GameCube card finding a first class home
server side, and interop with a card a browser streaming session wrote. **Stage 4 (#169) settled
both against the conclusion**, first from source at tag `5.3.0-alpha.2`, then on the live server,
which needs no streaming broker for the card routes.

**Read in source.** `MemoryCard` is scoped by `(user, emulator)`, and its own docstring says one
Dolphin card serves both GameCube and Wii. There is no ROM on the record at all, so a card is a
class D container by construction rather than something that can be made per game. Its data is a
history of `MemoryCardVersion` rows, each "the whole card image, e.g. the zipped PCSX2 folder
card". The streaming claim describes a Dolphin card by reading `.gci` names out of the zip
(`summarize_card`), so the Dolphin card upstream exchanges is a **GCI folder**, which is class C,
and not the raw `SRAM.<REGION>.raw` that #82 is about.

**Measured, 2026-09-16,** with `tools/romm-5.3-probes/s2-memory-card-record.py`, on a throwaway card
deleted afterwards:

| Question                            | Answer                                                                                                   |
| ----------------------------------- | -------------------------------------------------------------------------------------------------------- |
| What a card record holds            | `id`, `user_id`, `emulator`, `name`, `slot` (always 1), `is_public`, `platform_id`, timestamps           |
| A card never synced                 | `GET /{id}/content` is 404, "Memory card has no stored data yet"                                         |
| Is a version whole or a delta       | **Whole.** A two-game zip came back byte-identical, 376 B, both `.gci` members                           |
| `content_hash`                      | over the zip's contents, not its bytes: `5fa2e028...` stored against an MD5 of `a398e3dd...`             |
| Is an identical upload deduplicated | **No.** Two uploads of one zip made two versions with one hash, as source says of the upload route       |
| A bare `SRAM.USA.raw`               | **400**, "not a readable zip archive"                                                                    |
| A zip holding `SRAM.USA.raw`        | **200.** The server never looks at the layout inside, so it cannot say whether an emulator would take it |

**What that does to the two uses.** #82 gains nothing: the only card the server would store is a
zip it does not validate, and the format streaming would hydrate for Dolphin is a GCI folder, the
per-game `.gci` files this client already syncs as class C units. Interop would mean reading
`.gci` members out of a whole-card version for the games a device holds, and writing back would
mean rebuilding a whole card, which is two writers on one container with no per-game identity on
either side. So the conclusion stands and is now evidence rather than a reading: **not a
transport, and not a bridge worth building**. PCSX2's card is a folder card upstream, which points
the same way as steering PCSX2 itself onto `folder`; what that choice writes on a RetroBat install
is unmeasured and is #80.

### 4. Two new writers on the saves the conflict route was measured against (`source`, then `measured`)

`emulatorjs.auto_save_sync` uploads whenever the emulator writes. Streaming V2 pulls saves and
states back with history, under `STREAMING_STATE_HISTORY_LIMIT` defaulting to 50 states per
rom, emulator and user.

Server side save rows can now change far more often, and from more directions, than when this
repo's negotiation behaviour was measured at 5.2.0. That lands on #156, #157 and #138, and on
the in flight write guard. Neither feature is on by default, which bounds the blast radius and
does not remove it.

**Stage 4 (#170) measured the browser writer and read the streaming one.** The server behind the
`Live*` tests runs with `EJS_ENABLE_AUTO_SAVE_SYNC` false and streaming disabled with no containers,
so nothing here was a browser session or a streaming session. What made the browser half
measurable anyway is that the flag adds no route.

**Read in source: the browser writes in place, and only the rate is new.**
`frontend/src/views/Player/EmulatorJS/utils.ts` `saveSave` makes one of two calls. With a save
loaded it sends `PUT /api/saves/{id}` with the row's own `file_name` and `device_id` only; with none
loaded it sends `POST /api/saves` with `emulator` set to the core and **no slot**, then holds the row
it made. `auto_save_sync` calls that on EmulatorJS's save interval, gated on two identical ticks,
where Save & Quit called it once. The update route keeps the id, the name and the slot, writes
`content_hash`, lets `updated_at` move on update, and runs no 409 check, no dedup and no device
check.

**Measured, 2026-09-16,** with `tools/romm-5.3-probes/s1-browser-save-writer.py`, which replays
those exact calls against a throwaway slot on one ROM, with `device_id` omitted on the browser's
calls as an ordinary web login sends it, and deletes every row and device afterwards:

| Case                                                                            | What negotiate answered                                                   |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| A. browser PUT over this device's row, local unchanged                          | `download`, same save id, new hash, "Server save is newer than last sync" |
| B. the same, and local also changed                                             | `conflict`, "Both sides changed since last sync"; ordinary upload 409     |
| C. keep-local appends a row, then the browser PUTs into the older row it loaded | `conflict` against the **older** row, which now lists first               |
| E. as C, but the older row was a peer's, never synced by this device            | **`download`**, "Server save is newer (no sync history)"                  |
| D. nothing loaded: one POST, then two PUTs                                      | one null-slot row, same id throughout; a fresh device is never offered it |

**The conflict route recognises the browser writer, with one exception, and the exception is a
rule this repo wrote down as a dependency.** A, B and C are the answers a client wants. E is the
case `docs/PLAN.md` described as "were negotiate ever to volunteer a superseded row, the resolution
would be undone by the next flush", and it is what happens: with no sync record for the revived row
negotiate compares timestamps, the browser's write is newer, and the next flush would overwrite the
save a person chose to keep with a continuation of the one they rejected. **Acted on in stage 4**:
a download naming a save id lower than the slot's recorded one is recorded as a conflict, since
ids only grow and a lower id at the head of a slot means something wrote into an older row or
deleted the newer one. Only the first was measured to reach a download; the second is unmeasured,
and a conflict is the answer that loses nothing either way. That
reads the recorded id, which is what made #157 a prerequisite rather than a tidy-up, and both
class A writers now record it.

**Driven on hardware after the fix**, RetroBat 8.2.1 on the install's own account, `nes` under
`libretro`/`nestopia`. EmulationStation launched `Destiny of an Emperor (USA)` with no save
anywhere, RetroArch wrote an 8,192 B `.srm` on close, and the `quit` hook's pass uploaded it as
save 225. That is the core initialising cartridge RAM, 4 non-zero bytes, and not a player save.
Then case E by hand: a peer row 226, a local edit, a flush that recorded the conflict through the
409, keep-local appending 227, and the browser's `PUT` into 226. **The next flush recorded a
conflict naming 226 as older than 227 and wrote nothing**, where the build before it would have
taken the download. Keep-server then brought the browser's bytes down and `save_slot` read 226; a
flush with nothing changed moved nothing; a newer peer row 228 came down as an ordinary download
with `save_slot` following it to 228, so neither #157 path and not the new refusal misfired. Every
row and file the pass made was deleted afterwards.

**D is #138 at a higher rate, not a new defect.** A browser session that loads nothing makes one
null-slot row and keeps writing into it, so it does not pile up rows, and the protocol still cannot
see it. `saves restore` still can, with #156's collision, since fixed in stage 2 of #195 by
offering only the newest row per destination and naming the rest.

**Read in source at `5.3.0-alpha.2`: the browser's states (#190). Only one writer rewrites a state
in place, and it cannot reach a row this client holds, so nothing was measured.** Three frontend
paths post a state, and none of them calls `stateApi.updateState`, which is defined in
`services/api/state.ts` and called nowhere.

| Writer                                                     | Route, name, emulator                                                        | In place |
| ---------------------------------------------------------- | ---------------------------------------------------------------------------- | -------- |
| `views/Player/EmulatorJS` (the v2 player wraps the same)   | `POST /api/states`, `<fs_name_no_ext> [<ISO timestamp>].state`, the EJS core | no       |
| `console/views/Play.vue`                                   | `POST /api/states`, `state.save` every time, `emulatorjs`                    | **yes**  |
| `v2/components/GameDetails/SaveDataTab.vue`, manual upload | `POST /api/states`, the uploaded file's own name, no emulator                | by name  |

`auto_save_sync` changes nothing for states: `installAutoSaveSync` subscribes to `saveSaveFiles`
only, so a state is still written on a save-state press and on Save & Quit. The console view
rewrites because `store_state_file` in `handler/asset_store.py` updates whatever row already holds
`(user, rom, file_name)`, the upsert `romm-api` records as measured, and a fixed name makes every
save that one row.

**Neither player reaches `StateSync`.** Every upload this client makes is named
`<stem> [<emulator>[.<core>]]<ext>`, and neither a timestamp nor `state.save` can equal that, so no
player write lands on a row this device sent. Restore reports both shapes as unrestorable rather
than placing them: `emulatorjs` is not declared in `es_savestates.cfg`, and the two EJS cores that
share a declared emulator name, `ppsspp` and `desmume`, produce a `.state` that neither emulator's
`<file>` template matches.

**A manual upload under this client's exact name is the one writer that can land on its row**,
such as a state downloaded from the web UI and uploaded again. It replaces the bytes under the same
id and **clears the emulator**, because the update writes the caller's emulator and the upload
sends none. Read in code, `StateSync` does nothing about it: `RunAsync` decides what needs sending
from the local hash alone and never reads the server row, and restore skips a destination that
already exists. The next local change overwrites the upload without a word. That is the same last
writer wins that two devices on one account already get, because states have no conflict route.
Recorded rather than acted on, since no player write can take this path.

**Read in source, not measured: streaming.** `handler/streaming/saves.py` stores each pulled save
archive as a **new null-slot row**, `<rom stem> [<emulator> <timestamp>].saves.zip`, dropped when
its hash matches any save already held for the ROM. Negotiate never offers one. `saves restore`
would list one as restorable to `saves/<system>/<stem>.zip`, because a null slot matches no
declared unit path, and `--apply` then fails its hash check, because the server's digest over a
zip is not the MD5 of its bytes: closed, with a preview that promised more.

`handler/streaming/states.py` keeps every capture as a state row and prunes past the limit with
`user_states_for_emulator`, which filters the user's states for the ROM **on the emulator name
alone**. It does not ask who wrote them. This client uploads states with the emulator field set to
`emulator[.core]`, which is `pcsx2`, `dolphin` and `xemu` for three standalones streaming also names
that way, so a game streamed past fifty captures loses this device's oldest state rows on the
server. The local files survive and are not re-sent, because a state is in step by the hash this
device recorded. Libretro states go up as `libretro.<core>` and do not share streaming's
`retroarch` budget. Recorded rather than acted on: streaming is off on the only server reachable,
so nothing here has seen it happen.

### 5. ROM identity is keyed on the path, and the new endpoint is a write-back (`source`)

Read at `5.3.0-alpha.2`, both halves of the release note are wrong as written, and each
pointed at a different conclusion than the source does.

**Identity is not content hashed.** `0126_unique_rom_full_path` moves the unique index off
`(platform_id, fs_name)` and onto `(platform_id, full_path_hash)`, where `full_path_hash` is
`sha256(fs_path + "/" + fs_name)`. The digest is there because that path is 5804 bytes of
utf8mb4 against InnoDB's 3072 byte key limit, not because content decides identity. What it
buys is a custom library structure, where one platform may hold identically named files in
different folders. The direction of travel is the opposite of the note's: a file that moves
between folders inside a platform used to keep its row for free, because the filename was the
key, and now does not.

**What survives a move is a rescue, not a key.** `scan.py` flags every entry whose path is
absent as `missing_from_fs` before it identifies files, then, for a file with no full path
match, hashes it and looks for a missing entry with the same hash, so a moved or renamed ROM
carries over its collections, notes and uploaded assets instead of spawning a duplicate. That
is scan time behaviour and it is gated on hashing being on at all
(`calculate_hashes = not cnfg.SKIP_HASH_CALCULATION`), so an instance with hashing off turns
every move into an orphan next to a duplicate.

**`PUT /roms/{id}/identity` rebinds nothing.** It stores the identity triple `title_id`,
`save_target` and `save_target_layout` that a client extracted from a ROM the server cannot
read, under scope `roms.write`, normalising the triple for the Switch family on the way in.
That makes it the return path for finding 2 rather than a repair tool: the systems where this
repo's header reads succeed and upstream's extractor answers nothing are systems where RomMBat
could supply the id rather than only consume one.

**Measured on 2026-09-16, and it fires.** A lone `.zip` was moved into a new subfolder of its
platform, keeping the filename byte for byte, and the platform was quick scanned.

|                   | Before                                         | After                 |
| ----------------- | ---------------------------------------------- | --------------------- |
| rom id            | 160135                                         | **160135**            |
| `created_at`      | 2026-03-17T00:32:41Z                           | **unchanged**         |
| rom file row id   | 517802                                         | **517802**            |
| `md5_hash`        | `0823b8d4...`                                  | **unchanged**         |
| `fs_name`         | `Snorlax's Lunch Time (Europe) (GameCube).zip` | `moved`               |
| `full_path`       | `roms/pokemini/Snorlax's ... .zip`             | `roms/pokemini/moved` |
| `missing_from_fs` | false                                          | false                 |

The platform still holds 38 rows, no row is flagged missing, and no duplicate was spawned, so
the reassociation carried the row rather than the scan creating a second one. **A cached binding
against a rom id survives a move**, which is the question #177 asked.

**That experiment was impure, and the impurity is the more useful half.** A directory under a
platform folder is RomM's own convention for one game held as several files, so the move did not
read as "same ROM, new path". It read as a new folder-shaped ROM, and the row's `fs_name` became
the folder name while `name` stayed `Lunch Time`. Within one platform there is no subfolder that
is not already a game, so isolating the path needs a rename instead.

**Both controls were run, and the mechanism holds in every direction.** Moving the file back and
rescanning returned **every** field to its original value except `updated_at`, including
`fs_name`, `fs_extension` and `has_multiple_files`, so the round trip is lossless at the row
level and a reorganisation is recoverable. Renaming the archive in place, same folder, then
isolates the path change: rom id, `created_at`, file row id, all three hashes and `name` were
untouched, `fs_name` and `full_path` followed the new filename, the platform stayed at 38 rows and
nothing was flagged missing. So the reassociation fires on a pure path change and the move result
was not an artifact of the folder conversion.

**Why a rename cannot break it, which is a property of a measured rule rather than luck.** The
archive was renamed and the member inside it was not, and RomM's rom hashes describe the member
rather than the container (finding 80 in `docs/retrobat-findings.md`: a `.zip` reports the hashes
of the file inside it and nothing about the archive). A container rename is invisible to the
value the reassociation matches on. Untested, and worth knowing before relying on this: whether
renaming the **member** also leaves the match intact.

**What it cost is the filename, not the identity, and the filename is load bearing.** RomMBat
writes `roms/{folder}/{fs_name}` and keys `es_settings.cfg` per-game overrides on the rom
filename, which `EsSettingsFile.PerGameKey` refuses to build without an extension because
`emulatorlauncher` ignores a key built from a stem. The row that comes back from a move like this
one has no extension. Nothing breaks today, because per-game conversion exists only for
`dreamcast`, `gamecube`, `ps2` and `psx` and a multi-disc row is already refused by name. It
stops being hypothetical the moment a library is tidied: **foldering a multi-disc set is exactly
what turns a refusal into a row `PerGameKey` cannot express**, and foldering is the correct way
to store those games, so a correctly stored multi-disc game would convert worse than an
incorrectly stored one. #183, which is a design question about what a folder-shaped ROM lands as
on disk rather than a missing guard.

**It also reproduced a known falsification.** The row is extensionless with `has_multiple_files`
false, so "every extensionless ROM is multi-file" is wrong in a second, independent way. That was
already found in `docs/freegosy-findings.md`; finding 82 in `docs/retrobat-findings.md` still
asserted it and now carries the withdrawal. The code reads the flag and never the extension, so
nothing mis-excluded either time.

### 6. Physical games mean a sync set can hold a row with no file (`source`, now `measured`)

`POST /roms/physical` creates a rom with no file on disk, and `DetailedRomSchema` gains
`is_physical` and `has_file_on_disk`. A catalog page can now return rows that cannot be
downloaded.

**This is finding B's concrete case.** It needs no floor move and no pin move to reach a user,
and the fix is a filter plus a null check rather than a feature.

**Upgraded from `notes` in stage 1 (#167), and the hazard is wider than physical games.** Both
fields are declared on `RomSchema`, the base that `SimpleRomSchema` extends, so they are on every
row `GET /api/roms` returns rather than only on the detail route; confirmed on a live
`5.3.0-alpha.2` page, which carries `is_physical`, `has_file_on_disk` and `missing_from_fs`
together. And `has_file_on_disk` is a property, not a column:

```python
return not self.is_physical and not self.missing_from_fs
```

`missing_from_fs` is **required at the 5.2.0 floor**, so a ROM deleted from the server's disk has
been reaching the download path since before any of this, with no physical game involved. That is
the half of the bug that was already in the field.

**What was built.** The drop is client-side and derives the answer where the server does not give
it: `RomRow.HasFileOnDisk` honours `has_file_on_disk` when present and falls back to
`not is_physical and not missing_from_fs` when it is absent, which is what keeps a 5.2.0 server's
whole library from being excluded. `SetResolver` gives it its own `ExcludedNoFileOnDisk` state
ahead of the shape and extension checks, because neither is what is wrong with the row, and
`PickedSetService` refuses the same case per game.

**Server-side filtering is deliberately not used.** `GET /api/roms` takes both a `missing` and a
`physical` boolean, but only `missing` exists at 5.2.0; `physical` arrived with this release. That
was the first reason, and the floor move retired it. The one that holds at `alpha.2` is that a row
the server filters out never reaches the resolver, so the sync summary could not count it as
skipped for having no file, and a game would disappear from a set with nothing said.

### 7. The gamelist exporter was substantially rewritten (`source`)

`backend/utils/gamelist_exporter.py` on `master` no longer resembles the vendored copy. It now
parses and merges existing gamelists through `defusedxml` (upstream's "merge instead of
overwriting" fix), took `ASSET_DIRS` into `config.PLATFORM_MEDIA_DIRS`, and gained
`HAS_FILE_ON_DISK_FILTERS`, `rel_platform_folder` and `join_rel_path`.

This repo derives two unit conversions from that file, that `first_release_date` is
milliseconds and `average_rating` is on 0 to 100, and `verify.py` asserts them as behaviours
rather than counts precisely so a rewrite like this one is survivable. **Both survive**, read at
`master`: `first_release_date` is still divided by 1000 at line 125, and `average_rating` by 100
at line 340 with the "0-100 scale" comment intact. Asserting behaviours rather than line numbers
is what made that a two minute read instead of a re-derivation.

**Re-pulled and re-derived, 2026-09-14.** `refresh.sh` moved the vendored copy and `verify.py`
came back with exactly one drift, the companies check, which is the one the re-pull was
expected to flip. Everything else held: both unit conversions, the `marquee` / `logo_path`
rule, the `releasedate` and rating format strings, and all seventeen gamelist elements
RomMBat writes. Of the three deliberate divergences, region/lang and genre are untouched.

**One divergence ends, per row rather than outright.** The exporter now reads
`primary_developer` and `primary_publisher`, which is this repo's own upstream follow up 3
(`PLAN.md` "Optional follow ups to RomM itself") implemented upstream. But each property
falls back to the indexing it replaced:

```python
return next(iter(self.developers or companies[:1]), None)
```

So the alphabet still reaches `<developer>` on any row whose split is empty, and **a row only
carries the split once it has been rescanned under 5.3.0**. Measured on the live
`5.3.0-alpha.2` library, 3,000 rows across ten platforms: the one platform that had been
rescanned carried `developers` on 398 of 400 rows, and the nine that had not carried it on 0
of 300 each while still carrying `companies`. Where the split is present it is exactly one
developer and one publisher and `companies` is the two of them sorted, so indexing assigns
both roles wrongly on **41% of rows** (163 of 398). `4x4 Evo 2` is
`companies=[Sierra, Terminal Reality]`, which reads Sierra as the developer when Terminal
Reality developed it.

**Re-taken 2026-09-16 over the whole library rather than a sample**, with
`tools/romm-5.3-probes/r4-company-split.py`, because the sample recorded no method and its 41% is a
property of the one platform it happened to find rescanned. Two readings: the numbers below are the
second, and the first differed by 14 split rows because a scan of `nes-unofficial` was running
between them.

| Rows   | `companies`   | Split         | One and one   | Sorted pair   | Wrong role   |
| ------ | ------------- | ------------- | ------------- | ------------- | ------------ |
| 95,993 | 83,037, 86.5% | 18,150, 18.9% | 17,606, 97.0% | 18,065, 99.5% | 3,903, 21.5% |

The last three are shares of split rows. **Two claims above do not hold across the library.** The
wrong-role rate is **21.5%** and not 41%, and it is a per-platform number that ranges from 0 on
`channelf` and `gamate` to 45% on `gamecube` (809 of 1,792) and 83% on `arcadia` (40 of 48).
And a split row is **not always one developer and one publisher**: 3.0% carry more than one of
either, so `companies` is still their sorted concatenation on 99.5% of split rows but a client
that writes `developers[0]` is choosing one of several on the rest. What survives is the shape of
the finding: 53 platforms carry the split and 72 carry none, so one library holds both at once and
the join stays as the fallback. `wii` is the outlier worth knowing, with 79 split rows of which only
32 sort to `companies`.

The operative consequence for M4: writing `developers[0]` and `publishers[0]` is right where
they are populated and empty where they are not, so a client that switches to them
unconditionally loses the field on every un-rescanned row. The join stays as the fallback.
#172.

`reference/romm-known_bios_files.json` is **unchanged** on `master`.

### 8. Grants tightened on the export endpoints (`source`)

`POST /export/gamelist-xml` and `POST /export/pegasus` now require a `PLATFORMS` / `WRITE`
grant and enforce platform visibility. **RomMBat calls neither**, confirmed by grep rather than
assumed: no hand written C# reaches either path, and the single hit repo-wide is the string
`pegasus_export` as a JSON property name at `src/RomM.Client/Generated/RomMApiSchema.g.cs:5639`,
which is a generated DTO field and not a call site. So the tightened grant costs the pairing
scope set nothing.

What #176 still owes is the line in the `romm-api` skill, because an answer that lives only in
this document is one the next session re-derives.

### 9. Performance measurements move, and must not be edited (`measured`)

`SCAN_WORKERS` and `WEB_SERVER_CONCURRENCY` both default to 4, up from 1. An N+1 in
`GET /api/roms` is fixed. Pooled connections are now recycled before the server drops them.

Every timing in this repo is a measurement of 5.2.0 at concurrency 1, and each one carries the
scope it was taken on: **8 minutes 15 seconds for a platform scope of 9,196 roms** at 250 rows a
page before #88, 2.3 s against 8.5 s for a `platform_ids` scoped page at a library of 88,331,
and the 300 s session timeout. Per the version move checklist those are provenance and **do not
get rewritten to new numbers**, and that includes the scope: a re-measure that walks the whole
88,331 rom library has measured a different thing and has nothing to compare against. They get
re-measured on the same scope, and the new numbers get their own attribution.

**Re-measured 2026-09-14 on `5.3.0-alpha.2`, and the scoped penalty is gone.** Same probes, same
flags, same page sizes, same platform. The `limit=100` page rows and the `with_total` pairs below
are `tools/argosy-probes/a1-a2-rom-id-index.py`, run unchanged from the commit that took the 5.2.0
readings, so the two columns come from one script. The two walk rows are
`tools/romm-5.3-probes/r1-page-walk.py`. Two things differ from the 5.2.0 pass besides the server,
and both belong in any reading of the table: the library is 95,993 roms against 88,331, and the
metadata mix has moved with it.

| Reading, on the largest platform (`psx`, 9,196 roms) | 5.2.0, 88,331 roms | 5.3.0-alpha.2, 95,993 roms |
| ---------------------------------------------------- | ------------------ | -------------------------- |
| Scoped page, `limit=100`, rom id index on            | 2,288 to 2,494 ms  | 285 to 353 ms              |
| Scoped page, `limit=100`, rom id index off           | 8,305 to 8,665 ms  | 274 to 328 ms              |
| What turning the index off costs, scoped             | **3.4 to 3.7x**    | **nothing**                |
| Unscoped page, `limit=100`, index on against off     | 1.13 to 1.18x      | 1.15 to 1.20x              |
| Full scoped walk, 250 a page, index off              | **8 m 15 s**       | **22.5 s**                 |
| Full scoped walk, 250 a page, index on               | not walked         | 24.1 s                     |
| `GET /api/roms/identifiers`                          | 504 after 300.0 s  | 200 after 176.7 s          |

**What the N+1 fix bought is the scoped case specifically.** Unscoped, the index is worth the
same 1.15x it was worth at 5.2.0, and costs the same 604 KiB a page. Scoped, the 3.4 to 3.7x that
`romm-api`'s "off only when the request is unscoped" rule was written from does not reproduce at
all: index off is inside the noise, and marginally ahead. The rule survives on bandwidth, which
was never its argument, and its stated reason is a 5.2.0 reason. With the floor at `alpha.2` that
reason holds on no supported server. The behaviour was kept at the time and the decision went to
#188, because changing it wanted a bandwidth reading this section did not take.

**Decided 2026-09-16 on that reading: the index is off under every scope.**
`tools/romm-5.3-probes/r2-scoped-index-bandwidth.py`, at the client's 250 a page, index on and
off interleaved within each of five repeats, on the widest scopes this account can page:

| Scope, 250 a page                     | Ids    | Index on   | Index off  | Off saves a page |
| ------------------------------------- | ------ | ---------- | ---------- | ---------------- |
| `platform_ids` psx                    | 9,196  | 548-604 ms | 557-575 ms | 63 KiB           |
| `virtual_collection_id` Single-player | 16,441 | 543-953 ms | 487-936 ms | 112 KiB          |

`total` stayed non-null in every off row because `with_total=true` is sent, and under a scope its
separate computation was inside the noise of the page. The account has no regular collection, and
its largest smart collection advertises 594 roms and pages to a total of 0, so neither kind has a
reading; a virtual collection spanning platforms is the widest scope a set can name, and it was
measured. What the index costs is its id count, so a wider scope only widens the saving.

**The 594 against 0 is the server working as written, not a defect** (#193).
`tools/romm-5.3-probes/r6-smart-collections.py` read all 29 smart collections this account can
list: every one is public, owned by another account, and filters on `favorite` and `platform_ids`,
and every one pages back 0 while advertising between 6 and 594. `refresh_smart_collection` stores
`rom_count` and `rom_ids` computed as the owner and says so in its docstring, and
`smart_collection_id` applies the same criteria as the caller, who has marked none of those
favourites. The picker no longer shows the stored count, and the rule is in `romm-api`.

**A2 is unchanged in shape.** `with_total` is free with the index on (262 ms against 264 ms) and
costs with it off (320 ms against 187 ms), which is `resolve_total()` returning the length of an
index that is already being built. With the index off under every scope, `CatalogQuery` pays for
that count on every walk: inside the noise of a scoped page (R2), and 133 ms unscoped.

**`/api/roms/identifiers` moved from impossible to merely unusable.** It completes now, at
176.7 s for 95,993 ids, where 5.2.0 answered 504 on the 300 s wall at a smaller library. The
decision to refuse it stands: three minutes is not a page a sync can wait on, and the endpoint
still takes no parameters, so it can be neither scoped nor paged. The rejection is now a
judgement about latency rather than a report that it does not work. A one-off probe re-took it on
2026-09-16 at **200 after 181.4 s**, 95,993 ids in a 656 KiB body, on the same library, so the two
agree.

**And it is a judgement about the server's memory, which is why no probe for it is committed.**
The route eager-loads every ROM's relationships to return ids and keeps running after the client
disconnects. The live suite's budgeted call gave up at 10 s, and about sixty runs of it in one
session took the container from 2 GB to **20.9 GiB**, the web workers holding their peak until
recycled. Reported as rommapp/romm#4577. The live test, the client method and both probes that
called it are removed, so the two readings above are not re-taken at the next adoption until
upstream fixes it.

**The payload share moved, and the library moved with it, so this one attributes to neither.**
`ss_metadata` is 61.2% of a 100 row page against 46.4% at 5.2.0, and `igdb_metadata`, 20.5%
then, is out of the top fifteen now. The A12 conclusion is unchanged and was never a number:
a payload share is a property of a library's metadata mix, not of a client or a release.

### 10. Not adopting (`notes`)

Recorded so the next session does not re-open them: Jukebox, walkthroughs, physical game
barcode scanning, library aware recommendations, the four new metadata sources (Steam, Demozoo,
Pouet, CSDb), js-dos and PICO-8 in browser play, and Emulator Streaming V2 as a feature.
RomMBat's job is a local RetroBat install, and none of these reach it. Streaming survives only
as findings 3 and 4, which are about contention rather than adoption.

The filesystem structure changes (Structure B detection removed, `filesystem.roms_folder` and
`filesystem.firmware_folder` gone, `GET /setup/library` reshaped) are **server administration**
and inert here, because RomMBat maps to RetroBat's folders and never to the server's. They are
listed so that inertness is a recorded conclusion rather than an omission.

**Inert for the client is not inert for whoever stands one up.** 5.3.0 makes the layout an
explicit declaration and **an instance refuses to start** until `filesystem.structure` is set in
`config.yml`, printing the equivalent template if the removed keys are still present. Anyone
following `DEVELOPER_SETUP.md` to raise a disposable RomM, for a schema capture or to answer one
of the questions below, hits that before the server ever listens. The structure-A equivalent is:

```yaml
filesystem:
  structure:
    default: "roms/{platform}/{game}"
    firmware: "bios/{platform}"
```

### 11. The `alpha.2` to `alpha.3` delta (`source`)

`5.3.0-alpha.3` was published 2026-09-17, 319 commits and 67 non-test backend files after
`alpha.2`. It was this adoption's target until finding 12 retargeted it at `beta.1`, which is the
floor. Read at both tags, and the served schema diffed against the pin it replaces. Nothing in
this section is measured yet; the three questions it opens are at the end of this document.

**The contract barely moves where this client reads.** One operation leaves, the streaming
`state-frame` route, and none arrive. On a route RomMBat calls, the only change is `slot` on
`POST /api/saves` gaining `maxLength: 255`, so a longer slot is now a 422 rather than a row; the
longest slot this client writes is a class C `{emulator}:{kind}` far below it. `GET /api/roms`
takes the same parameters in a different order, because #4487 moved them into one
`RomFilterParams` model shared with smart collections, and `collection_id` and
`smart_collection_id` now refuse a value below 1. The facet filters read the `roms_facets` mirror,
which already existed at `alpha.2`. The routes a download, a firmware fetch, a play session and a
negotiate use are unchanged, and `backend/main.py` is untouched, so the pin still depends on the
version and not the instance.

**#4540 caps every slot, whatever the client asks, and for this client the cap does not move.**
`add_save` computes the tighter of `MAX_SAVES_PER_SLOT` (env, default 50, `0` disables it) and
`autocleanup_limit` when the client set `autocleanup`, first clamping the client's ask to
`MAX_AUTOCLEANUP_LIMIT` (env, default 100) and to at least 1, and prunes past it on every slotted
upload, retries included. `prune_slot` keeps the newest by `updated_at` then `id` and deletes the
rest with their files and screenshots. Before, pruning ran only when a client asked.
**RomMBat has always asked**: `UploadSaveAsync` sends `autocleanup=true&autocleanup_limit=10` on
every save upload (`src/RomM.Client/Saves/RomMConnection.Saves.cs`, `AutoCleanupLimit`), which
`docs/PLAN.md` records as the M6 decision. So its own slots were bounded at 10 before alpha.3 and
are bounded at 10 now, and #4540 changes nothing for what it uploads itself. What the server cap
governs is every **other** writer on those slots, which asks for no cleanup: a peer, and RomM's
browser player.

**Measured against such a writer, and it costs this client nothing**
(`tools/romm-5.3-probes/s3-slot-retention.py`, two runs on the live `alpha.3`). A device uploaded
and negotiated, then a peer with no device put 51 versions in the slot, sending no cleanup
parameters, which is the case `MAX_SAVES_PER_SLOT` actually governs:

| After 51 peer versions                  | Answer                                                             |
| --------------------------------------- | ------------------------------------------------------------------ |
| Rows in the slot                        | 50, and the device's own version is one of those deleted           |
| Negotiate, the device's copy unchanged  | `download` of the newest, "Server save is newer (no sync history)" |
| Negotiate, the device's copy edited     | `upload`, "Client save is newer (no sync history)"                 |
| The ordinary upload that answer invites | **409**, "Slot has a newer save since your last sync"              |

So deleting the version a sync was recorded against makes negotiate forget the history, and the
upload guard does not: it still holds this device's record for the slot. Two answers that disagree,
and this client already reconciles them, because `SaveSync` records a negotiated `upload` that
comes back 409 as a conflict, a path first driven on hardware for a different cause. An unchanged
copy takes the peer's newest version, which is right, and an edited one becomes a conflict to
settle rather than an overwrite. The prune ranks on `updated_at` as read: a `PUT` onto the oldest
surviving version kept it through the next upload, and the next oldest went instead.

**#4540 also renames a slotted save's screenshot to the save's stem, and does nothing for
states.** Finding 258 of `retrobat-findings.md` is the state side.

**The browser player now writes into slots, and by default into this client's.** Three parts:

- **A session opens a new version, then rewrites only that one.** `saveSave` in
  `views/Player/EmulatorJS/utils.ts` holds the version the session created, starting from none:
  the first write `POST`s into the slot with `overwrite=true`, which skips both the stale-device
  409 and the content-hash dedup, and every later write in the session `PUT`s that new row in
  place. At `alpha.2` the player `PUT` the save it loaded, which is finding 4's case A. At `alpha.3`
  a loaded save is left alone and the session appends beside it.
- **The slot is the loaded save's, or the newest slotted save's.** `Player.vue` passes
  `loadedSave?.slot || props.saveSlot`, and the v2 player seeds `props.saveSlot` from
  `preferredSlot(rom.user_saves)`, the slot of the newest save that has one, falling back to
  `autosave`. So for a game this client has synced, a browser session writes into
  `libretro:battery` or whatever slot this client used.
- **The file is `<fs_name_no_ext>.srm` under the EmulatorJS core's name**, `autocleanup` only in
  `autosave`. For a class A libretro slot that is the shape this client downloads and writes to the
  ROM's stem. For a bundled slot it is a raw `.srm` in a slot this client expects to hold an
  archive.

To this client that is ordinary protocol: a newer row in a slot it holds, so `download` if the
local file is unchanged and `conflict` if not. What is not known is whether the bytes are right,
which is a question about EmulatorJS and not about RomM.

**Inert here**, listed so it is a conclusion and not an omission: nginx moves to TLS 1.2 and above
and serves precompressed frontend assets only, scoped away from `/library/` so a ROM download is
untouched; the patcher, Steam metadata and covers, the manual upload routes, the scan and title id
extraction order (#4566, which changes which disc of a set a title id is read from), multi-disc
playlist handling, and every streaming change. The two screenshot fixes in the notes, #4478 and
#4526, are the player's and streaming's.

### 12. The `alpha.3` to `beta.1` delta (`source`)

`5.3.0-beta.1` was published 2026-09-18, one day after `alpha.3`, and was the floor from that
adoption until `5.3.0` replaced it (finding 13). 160 commits, and a recursive tree diff of the two tags gives 52 files added, 5 removed
and 367 modified. Of those, **31 are non-test backend files and the rest is frontend v2**.

**Count the files from the trees, not from the compare endpoint.** `GET /repos/{o}/{r}/compare/{a}...{b}`
caps its `files` array at 300 and returned exactly 300 here, with no flag in the payload saying
so. Two frontend files this finding turns on, `views/Player/EmulatorJS/utils.ts` and its
`Player.vue`, were missing from that list and are changed. The backend half happened to fit, so
every contract reading below stands, but the first pass read "the save writer is untouched" off a
truncated list and that was wrong. Compare blob shas from `git/trees?recursive=1` instead.

**The contract is the smallest move of the four pins.** The same 246 operations and 272 schemas,
none added, removed or renamed, and the generated diff is four lines. `RomFileSchema.last_modified`
becomes nullable, `SystemDict` gains `GIT_BRANCH` (a nullable string, filled only on a
`development` build), and the `X-Upload-Total-Size` and `X-Upload-Total-Chunks` headers on
`POST /api/roms/upload/start` drop to `minimum: 0` because an empty ROM file is accepted now. No
hand-written line names any of the three, and RomMBat does not upload ROMs. The pin's README has
the detail, including why the `required` on `GIT_BRANCH` does not reach the DTO.

**The backend delta is one shape repeated: answer for ids without building rows.** #4584, #4586,
#4587, #4589 and #4590 add `get_save_ids`, `get_state_ids` and the `RomVisibility`,
`RomVisibilityLabel` and `RomDeletionTarget` named tuples, and point the identifier, visibility
and file routes at them. `GET /api/saves/identifiers` was building a `Save` per row to read
`.id` off it, and now projects the column.

**That lands on finding 9, and nothing here is re-measured.** The identifiers walk, 200 after
176.7 s for 95,993 ids, is the reading most likely to have moved, and it is `alpha.2`'s. It is
left exactly as written, because a measurement is attributed to the build it was taken on and
editing it would invent a number. Re-measuring it is owed and is in the open table below.

**The per-slot cap that finding 11 measured is byte-identical here.** `add_save` and `prune_slot`
are untouched between the tags; the only save-side change is the ids projection. So the `alpha.3`
retention table describes the code `beta.1` ships, and `s3-slot-retention.py` says so in its
docstring rather than implying a re-run happened.

**The browser save writer was substantially rewritten, and finding 11's reading survives it.**
`utils.ts` gains a canvas screenshot capture per save version, an SRAM dump that flushes the core
before reading, and a pending-asset path; the naming moved into `services/api/save.ts` as
`sessionSaveFile` and `sessionScreenshotFile`, which confirm `<fs_name_no_ext>.srm` for a new
session save and the save's stem for its picture. **The slot decision is unchanged.** `Player.vue`
still resolves `loadedSave?.slot || props.saveSlot`, `saveSlot` is still
`chosenSlot(slotChoice)` seeded from `preferredSlot(rom.user_saves)`, and `v2/utils/saveSlots.ts`
is byte-identical at both tags: `preferredSlot` still returns the slot of the newest slotted save
and only falls back to `autosave` when there is none. `saveSave` still `POST`s with
`overwrite: true` into that slot and `PUT`s its own row afterwards.

So for a game this client has synced, a browser session still writes into `libretro:battery` or
whatever slot this client used. **The release notes say the opposite** ("Ordinary play goes to the
`autosave` slot"), and read against the source that line describes a game with no slotted save
rather than one this client syncs. This is the `notes` label earning its keep: acting on the
changelog here would have retired a live finding that is still true.

**The notes' breaking changes are not new since `alpha.3`.** The filesystem-structure declaration
and the one-entry-per-container streaming config both landed earlier in the 5.3.0 line, and no
config or structure file changes between the two tags; the release notes are cumulative from
5.2.0. They cost the operator a `config.yml` edit on upgrade and are not a client contract.
Finding 1 is the folder-authority half of the same work.

**Inert here**, for the same reason the `alpha.3` list was written: the jukebox and soundtrack
player, walkthroughs, recommendations, physical games, js-dos and PICO-8, the Steam and demoscene
metadata sources, and the v2 UI work that is most of the 367 modified files.

### 13. The `beta.1` to `5.3.0` delta (`source`)

`5.3.0` was published 2026-09-21 at 15:07Z, the first stable of the line, and was the floor from
that adoption until `5.3.1` replaced it (finding 14). 14 commits across 79 files, counted by diffing blob shas from both tags'
recursive trees, neither truncated, and the compare endpoint lists the same 79.

**The contract did not move at all.** The capture is byte-identical to the `beta.1` pin once
`info.version` is set aside: the same 246 operations and 272 schemas, none added, removed or
changed, and `generate.sh` reproduces the committed DTOs exactly. This is the first pin move with
no generated diff.

**Most of the backend delta is one formatting commit.** #4638 moves the backend to Python 3.14's
unparenthesised `except A, B:` and parenthesised `with` blocks, which accounts for every one-line
change in `handler/`, `utils/`, `sync_watcher.py`, `endpoints/memory_cards.py` and the save and
sync tests. The behaviour those tests pin is unchanged.

**One change is on the authentication path, and it cannot reach this client.** #4648 stops a
bearer or basic header from skipping the CSRF check when the request also carries a session
cookie that resolves to a user, because the session authenticates first and the request runs as
the cookie's owner. RomMBat authenticates by bearer token only, and `RomMConnection` builds its one
handler with `UseCookies = false`, which every RomM request (the content download included) goes
through. So it never sends a session cookie, and its POSTs are exempt exactly as before.

**Inert here**: #4633 answers a hidden platform as a missing one on the chunked ROM upload, and
RomMBat does not upload ROMs. #4634 budgets a zip member in the patcher, and #4636 changes only a
comment on the inline HTML route. #4641 is the v2 gallery's sort in the URL. Nothing under
`frontend/src/v2/views/Player/` changed, so finding 12's reading of the browser save writer stands
at `5.3.0`.

**So every measurement attributed to `beta.1` carries to `5.3.0`**, and it stays attributed to
`beta.1`: `add_save`, `prune_slot`, negotiate and the play-session routes are byte-identical, and a
reading is not re-labelled because the build it describes did not change.

### 14. The `5.3.0` to `5.3.1` delta (`source`, then `measured`)

`5.3.1` was published 2026-09-23 at 03:05Z, a patch release two days after `5.3.0`, and is the
floor from this adoption. 161 commits across 225 files, counted by diffing blob shas from both
tags' recursive trees, neither truncated: 42 added, 1 removed, 182 modified. The compare endpoint
lists the same 225, under its 300 cap. 57 are non-test backend files and 111 are frontend.
**No migration is added.**

**The contract moves in two operation parameters and no schema.** Every `/api/music/*` page caps
`limit` at 1,000 where it took 10,000 (#4716), and `POST /api/streaming/sessions/{platform}/heartbeat`
gains an optional `container` query parameter (#4596). The 198 paths and 272 schemas are otherwise
identical, and `generate.sh` reproduces the committed DTOs exactly, because NSwag emits types here
and not a client. RomMBat calls neither route.

**Every route this client depends on is byte-identical**: `endpoints/saves.py`, `sync.py`,
`states.py`, `screenshots.py`, `play_sessions.py`, `device.py`, `firmware.py`, `platform.py`,
`collections.py`, `auth.py`, `activity.py` and `heartbeat.py`. `endpoints/roms/__init__.py`
changed, and only in typing: `CustomLimitOffsetPage` now extends a `TypedLimitOffsetPage` whose
`create` is cast for mypy, `total` keeps its nullable type, and `RomFiltersDict` moves module.
Most of the backend delta is that kind of change, a run of mypy PRs (#4655, #4709, #4711, #4713,
#4718, #4719, #4723) that swap `type: ignore` comments for types and route DML row counts through
one `affected_rows` helper.

**Two changes reach something RomMBat reads, and neither is on a route it calls differently.**

- **#4676 lets a platform's `fs_slug` change case on a rescan.** `get_platform_by_fs_slug` falls
  back to a case-insensitive match, and `scan_platform` writes the folder's on-disk spelling where
  it wrote the lowercased `config.yml` key. The platform keeps its id. RomMBat keys `platform_map`
  on the exact `fs_slug`, so a case change left the old row behind as a second platform, and
  `PlatformMapStore.Record` now rekeys a row whose id matches and whose key differs only in case.
  Sync sets store the platform id and are unaffected. **From source, not driven**: all 125
  platforms on the live library have a lowercase `fs_slug`, so nothing moved there.
- **#4687 changes GameCube's `save_target` to the ASCII game code.** The sigil pin moves, and
  `save_target` becomes `GAFE` where it was `47414645`; `title_id` stays hex. Only a rescan writes
  it: the 50 most recently updated GameCube rows on the live library, last written 2026-09-14,
  still carried hex in both fields on 2026-09-24. Nothing in RomMBat reads `save_target`, so this
  corrects a sentence in `save-sync` (finding 2's route 4 must accept both shapes) and changes no
  code.

**Inert here**: #4653 revokes an account's Redis sessions on a credential change and stops an
in-flight request resurrecting one, and RomMBat holds no session, only a bearer token. #4694
checks ownership on the RetroAchievements refresh, consumes invites atomically, and passes `--`
before a 7-Zip member name; none is on this client's path. The frontend delta includes
`views/Player/`, but only gamepad focus (#4681, #4707) and the streaming heartbeat: the browser
save writer and its `preferredSlot` are untouched, so finding 12's reading stands at `5.3.1`.

**Measured at `5.3.1`**: `s4-older-mtime.py` answers all six cases as at `5.3.0`, M1
`no_op (No changes since last sync)`, M2 `upload`, M3 `download (Server save is newer (no sync
history))`, M4 `no_op (Content is identical)`, M5 returning the existing row for a peer's identical
upload, and M6 refusing the edit 409 until the peer's row is acknowledged. A deploy of the adoption
branch, with `status` reading `5.3.1` as Supported, resolved every platform mapping identically,
answered `nothing to do` for all eight sets, left all eight gamelists byte-identical, and flushed
nothing in either direction.

## What the floor move costs, and what gates it

**The `5.3.1` move, 2026-09-24.** The first patch release of the line, adopted the day after it
shipped. The pin is sha256 `fe182c0a...`, 198 paths and 272 schemas, captured from a server
reporting `5.3.1` at capture time, the instance the `Live*` tests point at. `demo.romm.app` still
reported `5.2.0`, so the capture is self-hosted again. `refresh.sh` drifted on the RetroBat side
only, the same four alias counts as at `5.3.0`, all of it #204's held-back work, so those vendored
files stay as committed. The RomM side moved one file, `romm-platform_aliases.py`, which gains
#4676's `resolve_fs_folder` and changes no alias; it matches the `5.3.1` tag and is vendored, and
`verify.py` is clean. **`5.3.0` and every prerelease of it are now refused**, because
`ProductVersion` drops the suffix and each reads as `5.3.0`. Finding 14 is the delta.

**The `5.3.0` move, 2026-09-21.** The first stable of the line, adopted the day it shipped. The
pin is sha256 `d7fa6ecb...`, 198 paths and 272 schemas, captured from a server reporting `5.3.0` at
capture time, the instance the `Live*` tests point at. `demo.romm.app` still reported `5.2.0`, so
the capture is self-hosted again. `refresh.sh` drifted on the RetroBat side only, four alias counts
off by one each, all of it #204's held-back work, so the vendored files stay as committed.
**The suffix drop's silent end closes here**: `LastTested` is a stable at last, so `5.3.1` and
above warn, and every `5.3.0` prerelease reads **Supported** against the floor, which is safe
because finding 13 finds no save or state change between them. Finding 13 is the delta.

**The `beta.1` move, 2026-09-18.** Same procedure again, and the smallest delta of the four. The
pin is sha256 `26ace330...`, 198 paths and 272 schemas, captured from a server reporting
`5.3.0-beta.1` at capture time, which is also the version the live instance the `Live*` tests
point at now runs. `refresh.sh` drifts exactly as it did for `alpha.3`, on the RetroBat side only,
and the vendored files stay held back for the RetroBat adoption that ships it (#204). The suffix
drop below applies with the numbers moved once more: `5.3.0-alpha.3` is now the tag below the
floor that reads **Supported**, and `LastTested` is `beta.1`. Finding 12 is the delta.

**The `alpha.3` move, 2026-09-17.** Same procedure, smaller delta. `refresh.sh` drifted only on
the RetroBat side, and all of it is unreleased work on RetroBat's default branch after 8.2.1
(`gzdoom` renamed `doom`, an `amiberry` state declaration, `.decomp` on four systems), so the
vendored files were left at their committed state for the RetroBat adoption that ships it. The pin
is sha256 `4ef28e4b...`, 198 paths and 272 schemas, captured from a server reporting `5.3.0-alpha.3`
at capture time. The suffix drop described below still applies with the numbers moved:
`5.3.0-alpha.2` is now the tag below the floor that reads **Supported**, and `LastTested` is
`alpha.3`. The paragraphs that follow are the `alpha.2` decision and are left as written.

**The decision is to move the floor to 5.3.0-alpha.2**, not to the `alpha.1` this document was
first written against. `alpha.2` was published on 2026-09-13, about eight hours after `alpha.1`,
which is the same day this assessment was made: the tag moved underneath the work rather than
after it. The repo's standing rule is to track the newest stable and to adopt within one release
of it appearing, and picking the older of two same-day prereleases would be adopting a build
that was already superseded before the PR opened. It is also the version a live server is
actually running, which is what turns the remaining questions from reading into measurement.

**What that costs is one read, and it is done.** `alpha.1` to `alpha.2` is 19 commits across 21
files, every one of them under `backend/`: twelve migrations revised, `roms_handler.py`
(+340/-142), `endpoints/roms/__init__.py`, `utils/database.py`, and their tests. Two things
follow. It does not touch `backend/models/rom.py` or `backend/endpoints/responses/rom.py`, so
**finding 2's source reads hold at either tag**. It does land squarely on the `GET /api/roms`
query path, which is where finding 9's timings and this repo's paging and scoping behaviour
live, so the re-measurement that finding 9 owes is attributed to `alpha.2` and not to `alpha.1`.

The version move checklist in the `pre-pr-verification` skill is the procedure.

| Step                                                       | State                                                                                                                                          |
| ---------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `refresh.sh`, resolve drift rather than editing the number | **Done for the RomM half.** One drift, the companies check, resolved in finding 7; the RetroBat files are held back, see `reference/README.md` |
| Read the upstream changelog end to end                     | Done, this document                                                                                                                            |
| Move `Minimum`, `LastTested`, README table and compat row  | **Done**, and a test asserts the trio agree                                                                                                    |
| Re-check open issues in `retrobat-findings.md`             | Not applicable, no RetroBat move in this adoption                                                                                              |
| Leave provenance alone                                     | See finding 9                                                                                                                                  |
| **Move the pinned OpenAPI schema**                         | **Done**, sha256 `44cdd228...`, 199 paths and 272 schemas                                                                                      |

**What a prerelease floor cannot do, and it is worth knowing before relying on it.**
`ProductVersion` drops the version suffix on purpose, because RetroBat's names a channel and a
semver-strict parse would refuse every stock install. So a floor declared as `5.3.0-alpha.2` is
`5.3.0` to every comparison the check takes part in, and a server reporting `5.3.0-alpha.1`, the
tag `alpha.2` superseded, is **Supported**. That is the right trade and not a defect, but the
README names a precision the startup check does not enforce, so a test now asserts the gap
rather than leaving it to be rediscovered.

**The same drop is silent at the other end.** `LastTested` is `5.3.0-alpha.2` too, so a server
reporting the final `5.3.0`, or any later `5.3.0` prerelease, compares equal to it and reads as
**Supported** with no untested warning, although this release never ran against that build.
Only `5.3.1` and above warn. That is accepted rather than fixed, because it lasts exactly as long
as the floor is a prerelease: adopting the stable moves the tested row to it. A test asserts
this end as well.

**Two things the floor move broke, both of them correct failures.** A stub server fixed at
`5.2.0` became a stub of a refusal, which stopped two pairing screen tests before they could ask
for an approval; the stub now defaults to the floor. And `RomFilterValues` carried no
`developers` or `publishers`, so the live filter picker test hit its unmapped-facet skip and
stopped checking the rest of the facets silently. Both are the version move doing its job: the
first is the compatibility check working, and the second is a live test finding a gap that no
offline test could.

`src/RomM.Client/openapi/generate.sh` regenerates DTOs from the pinned file, but the pinned
file itself is a byte exact `/openapi.json` captured from a running server, per
`DEVELOPER_SETUP.md`. FastAPI builds that schema at runtime and the repo does not hold one.

**The server that answers is the server the floor now names.** The instance the `Live*` tests
point at runs `5.3.0-alpha.2`, which is how section C came to be measured, and with the floor at
`alpha.2` it is also a legitimate source for the pin: a capture from it describes the same build
the floor declares. That removes the Docker step from this adoption. `docker compose` v5.1.4 is
on this machine with the daemon stopped, and a disposable instance is still the documented route
whenever the floor and the reachable server disagree, which is the situation this adoption
avoided rather than solved.

Two conditions on the capture, because a pin is a byte exact artifact and not a convenience.
It must come from a server that reports `5.3.0-alpha.2` at `/api/heartbeat`, checked at capture
time rather than assumed, since the instance is somebody's live library and can be upgraded
underneath the work exactly as upstream's tag was. And the capture is what regenerates the DTOs,
so `src/RomM.Client/openapi/generate.sh` runs against it and the diff gets reviewed, per the
committed-DTOs rule.

**Findings 2, 5, 6 and 9 are now measurable rather than parked**, on that same server, with
every reading attributed to `alpha.2`.

## Open, and needing a live instance

**Three questions left this table without one.** Whether the exporter's unit conversions survived
the rewrite, and whether RomMBat calls either export endpoint, are answered above from upstream
source and a repo grep. Finding 6's row shape is answered from the schema's inheritance and then
confirmed on a live page in stage 1. Reach for the cheaper route first: a question parked behind a
Docker daemon that a read answers today is a question nobody answers.

| #   | Question                                                                             | State                                                           |
| --- | ------------------------------------------------------------------------------------ | --------------------------------------------------------------- |
| 2   | What fraction of a real PSX, PS2, PS3 and PSP library actually carries a `title_id`? | **Answered**, 28 of 33 named rows on rescan. Finding 2          |
| 2   | Does `save_target_layout` agree, per system, with the shape this repo measured?      | **Answered.** It agrees, and the values agree too. Finding 2    |
| 5   | Can a move change what a rom id means to a cached binding or a set row?              | **Answered.** The id survives, the filename does not            |
| 7   | Are `developers` and `publishers` populated on real rows?                            | **Answered**, and the answer is per row. Finding 7              |
| 9   | Every timing in this repo, re-measured at concurrency 4                              | **Answered**, finding 9                                         |
| 3   | What a memory card record and version hold, and whether a raw card is accepted       | **Answered.** Whole card, zip only, layout unchecked. Finding 3 |
| 4   | Does the conflict route recognise the browser's save writer?                         | **Answered**, four cases of five. Case E acted on. Finding 4    |
| 4   | Does a streaming session prune or shadow this client's saves and states?             | **Read, not measured.** Streaming is off on the server reached  |
| 4   | Does the browser player rewrite states in place?                                     | **Read.** Only the console view, never on this client's row     |
| 11  | What reaches a slot this device is missing when 50 versions arrive while it is away? | **Answered.** A download, or a 409 that becomes a conflict      |
| 11  | Does a browser session's `.srm` in a libretro slot land and load under RetroBat?     | **Answered.** It does; a fresh one overwrote, #205, finding 259 |
| 11  | What does a browser `.srm` do to a bundled (class C) slot this client holds?         | **Read.** Refused before the tree, then offered every flush     |
| 12  | Every timing in finding 9, re-measured against `beta.1`'s projected id endpoints     | **Open.** The 176.7 s walk is `alpha.2`'s and is left as that   |
| 12  | Does the browser's new per-version save screenshot reach this client?                | **Read.** No. Saves carry no screenshot here, only states do    |
