# nes

Nintendo Entertainment System / Famicom. RetroBat calls the folder `nes`, which is what this
file is named after.

**All nine rows `nes` declares are certified.** All nine steps hold on each at RomM
`5.3.0-beta.1` and RetroBat 8.2.1, with step 6 N/A because `nes` has no class D. The move to
`5.3.0` touched step 9 alone, and it was re-run; see "The move to `5.3.0`". The move to the
`5.3.1` floor touched steps 1 and 9, and both were re-run; see "The move to `5.3.1`".
`libretro`/`nestopia` was the first certified `(system, emulator, core)` row in the project,
re-driven on 2026-09-20. `libretro`/`fceumm` and `libretro`/`mesen` followed on 2026-09-21, and
`fceumm` is **the row a stock install gives a user**, selected with no override. The two `bizhawk`
cores, `jgenesis`, `mesen` standalone, `mednafen` and `ares` followed later the same day, the last
four on a build that first gave them battery rules and, for three of them, the state declarations
`es_savestates.cfg` does not carry.

**It certifies those nine rows and nothing wider.** Every `(emulator, core)` pair `nes` declares on
RetroBat 8.2.1 is one of them, which is as close to "`nes` works" as this checklist lets a record
come, and it is still one install at one pair of floors. It says nothing about any of these
emulators on another system: every rule and declaration the last four needed was scoped to `nes`,
because that is the only system they were measured on. `megadrive` has since been measured and
widened the `bizhawk` and `mednafen` rules to name it, with its own `jgenesis` and `ares` rules
beside these; [megadrive.md](megadrive.md) is that record, and nothing here speaks for it.

**Step 5 closed on 2026-09-20 and is the first time any row has passed it.** Finding 258's fix
was driven on a state made after it, and the restored screenshot was checked by its bytes rather
than by its arrival. Step 2 changed on the same day, from requiring an exclusion this library
cannot offer to requiring that nothing be excluded. See "Re-driven at `5.3.0-beta.1`".

**This file is in five parts.** The first is `libretro`/`nestopia` in full, which is the row the
nine steps were first driven against. The second is the two other `libretro` rows, the third the
two `bizhawk` rows and the fourth `jgenesis`, `mesen`, `mednafen` and `ares`, each carrying steps 1,
2, 3 and 6 from the first and driving the rest themselves. The fifth is the other eight rows as
first driven, on steps 4 and 5 only, which is where the save and state shapes live and where six
of the nine turned out to write something RomMBat did not sync. All six have since been carried,
the `bizhawk` pair by #151 and the other four by this file's fourth part.

## The row

|              |                                                                                                                                      |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------ |
| System       | `nes`                                                                                                                                |
| Emulator     | `libretro`                                                                                                                           |
| Core         | `nestopia`                                                                                                                           |
| Selected by  | **Explicit configuration, not RetroBat's default.** `nes.emulator = libretro` and `nes.core = nestopia` are set in `es_settings.cfg` |
| Confirmed by | `emulatorLauncher.log`, which logged `-system nes -emulator libretro -core nestopia` and ran `nestopia_libretro.dll`                 |
| Save class   | A, `provenance: observed` in `save_shapes.json`                                                                                      |

**This row is not the one a user gets out of the box.** With those two keys absent the choice
falls through to the first-listed emulator and core in `es_systems.cfg`, which is
`libretro`/`fceumm`. That is a different row and it is uncertified. Certifying under a forced
setting says nothing about the default, which is why the record names how the row was selected.

`libretro` and `bizhawk` are core-scoped, so this says nothing about `nes` under `fceumm`,
`mesen`, `bizhawk`/`NesHawk`, `bizhawk`/`quickerNES`, `mednafen`, `ares` or `jgenesis` either.

Three further `nes.*` keys were set at the same time and are recorded because they are part of
the configuration this row was measured under: `nes.nestopia_nospritelimit = 1`,
`nes.video_driver = vulkan`, `nes.xbox_layout = 1`. `nes.ungroup` was **removed** rather than set
to false, which is EmulationStation pruning a switch turned off, the behaviour finding 238
describes.

## The install this was measured on

|             |                                                                            |
| ----------- | -------------------------------------------------------------------------- |
| RetroBat    | `8.2.1-stable-win64`, the supported floor                                  |
| RomM        | 5.2.0, the floor at the time                                               |
| Root        | `R:\RetroBat`, found by walking up from the executable                     |
| Store       | schema 14 of 14, WAL                                                       |
| Budget      | `none`. A 2 GB free-space floor still applies; NTFS, 927.4 GB free         |
| Media kinds | `ScrapeVideos` and `ScrapeManual` both `true`, as a fresh 8.2.1 ships them |

**This record spans two sessions and two builds**, and the difference matters to step 4. The
first ran before `saves restore` existed and measured the download half as broken. The second ran
on a deploy of `main` carrying #136 and #140, made by `tools/publish.ps1 -Deploy R:\RetroBat`,
and measured it working. Where the two disagree the second one is the result, and the file says
which is which rather than quietly replacing the earlier text.

The budget being off is deliberate and narrows what this pass proves: **nothing here certifies
`budget`, `evict` or the eviction guards.** None of those is among the nine steps. It also means
a missing cover at step 7 cannot be a headroom problem, which is why it was switched off.

**The 2026-09-20 re-drive ran on the same install moved forward**, and the two rows that changed
are the ones a result can turn on: the server reports `5.3.0-beta.1` and the store is at schema
16 of 16. RetroBat, root, budget and media kinds are as above. The client is a deploy of `main`
at 9ed1fd1, which is the first build here to declare `beta.1` as its floor rather than to
tolerate it.

## The move to `5.3.0-alpha.2`

**Every checklist result below was taken on RomM 5.2.0, and no step has been re-run on
`5.3.0-alpha.2`**, the floor the client now refuses anything below. One later observation, the
slot record in "Conflict resolution, driven both ways", was driven on it and says itself that it
re-runs no step. Mapped onto the nine steps per the
`platform-certification` skill's "When the floor moves", from the `src/` and `data/` diff across
#179 to #199, the finding 258 fix, and `docs/romm-5.3-findings.md`:

| #   | At `5.3.0-alpha.2` | Why                                                                                                                                     |
| --- | ------------------ | --------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Owed**           | `data/retrobat/platforms.json` was re-sourced from upstream's alias table (#166)                                                        |
| 2   | **Owed**           | The resolver gained a no-file-on-disk exclusion ahead of the extension check (#167), and the index flag on a scoped page changed (#188) |
| 3   | Carried            | No firmware code or bundled BIOS data changed, and the findings doc records no firmware route change                                    |
| 4   | **Owed**           | `SaveSync`, `SaveConflictResolver` and `SaveSlotStore` changed to refuse a superseded row the browser's writer revives (#170)           |
| 5   | **Owed**           | A restore fetches a linked screenshot (#158), and finding 258 renamed the uploaded screenshot and changed how a restore reads the slot  |
| 6   | N/A                | Unchanged: `nes` has no class D                                                                                                         |
| 7   | **Owed**           | The catalog query changed when `GET /api/roms/identifiers` was retired (#199), and that query is what fills the game list               |
| 8   | **Owed**           | The detached `background` pass the hooks spawn writes its log through a new append-only handle (#153)                                   |
| 9   | **Owed**           | Always owed on a move                                                                                                                   |

So the record stands as a 5.2.0 measurement, and the row owes steps 1, 2, 4, 5, 7, 8 and 9
before any result here speaks for the current floor.

## The move to `5.3.0-alpha.3`

**It adds nothing to what is owed, because only step 3 was carried and alpha.3 leaves it
carried.** Mapped from finding 11 of `docs/romm-5.3-findings.md`; no `src/` or `data/` change
came with this move beyond the regenerated DTOs, which no step's code reads.

| #   | At `5.3.0-alpha.3` | Why                                                                                                                        |
| --- | ------------------ | -------------------------------------------------------------------------------------------------------------------------- |
| 1   | Owed, as before    | `GET /api/platforms` is unchanged                                                                                          |
| 2   | **Touched**        | The `GET /api/roms` filters moved into one model shared with smart collections (#4487), which is how the set resolves      |
| 3   | Carried            | The firmware routes and this client's BIOS code are unchanged, and `nes` requires no BIOS                                  |
| 4   | **Touched**        | Every slot is capped at 50 versions (#4540), and a browser session now writes new versions into this client's slot (#4517) |
| 5   | Owed, as before    | `POST /api/states` changed only in how a delete removes its screenshot                                                     |
| 6   | N/A                | Unchanged: `nes` has no class D                                                                                            |
| 7   | **Touched**        | The same filter move, on the query that fills the game list                                                                |
| 8   | Owed, as before    | `POST /api/play-sessions` is unchanged                                                                                     |
| 9   | **Touched**        | Always touched on a move                                                                                                   |

## The move to `5.3.0-beta.1`

**It adds nothing to what is owed either, and it un-touches nothing.** Mapped from finding 12 of
`docs/romm-5.3-findings.md`. The only `src/` change in the move is the regenerated DTOs, four
lines, and no step's code reads either member.

| #   | At `5.3.0-beta.1` | Why                                                                                                                       |
| --- | ----------------- | ------------------------------------------------------------------------------------------------------------------------- |
| 1   | Owed, as before   | `GET /api/platforms` is unchanged again                                                                                   |
| 2   | Owed, as before   | `GET /api/roms` takes the same 58 parameters; the change is how the rows are built, not what is asked for                 |
| 3   | Carried           | No firmware route or BIOS data changed, and `nes` requires no BIOS                                                        |
| 4   | Owed, as before   | `add_save` and `prune_slot` are byte-identical, and the browser writer's slot choice is unchanged, so alpha.3's row holds |
| 5   | Owed, as before   | `POST /api/states` is unchanged; only `GET /api/states/identifiers` changed, and it projects ids                          |
| 6   | N/A               | Unchanged: `nes` has no class D                                                                                           |
| 7   | Owed, as before   | The game list's query is unchanged                                                                                        |
| 8   | Owed, as before   | `POST /api/play-sessions` is unchanged                                                                                    |
| 9   | **Touched**       | Always touched on a move                                                                                                  |

So the row owed steps 1, 2, 4, 5, 7, 8 and 9, at `5.3.0-beta.1` rather than at `alpha.3`. They
were re-driven on 2026-09-20 and the next section is what they came back with. The three tables
above stay as they are, because what a move was expected to touch is a separate record from what
re-running it found.

## Re-driven at `5.3.0-beta.1`, 2026-09-20

**The seven owed steps were re-run in one session against a deploy of `main` at 9ed1fd1**, made
by `tools/publish.ps1 -Deploy R:\RetroBat`, on a server reporting `5.3.0-beta.1`. That build is
the first on this install to carry the floor it was measured against: the previous deploy
predated the retarget, so the record now names a client that refuses anything below `beta.1`
rather than one that merely tolerated it.

| #   | Owed for                             | Re-run result                                                     |
| --- | ------------------------------------ | ----------------------------------------------------------------- |
| 1   | The re-sourced alias table           | **Pass.** Both rows resolve as before, `fs_slug` and `bundled`    |
| 2   | The no-file-on-disk exclusion        | **Pass.** 228 of 228 resolve and nothing is excluded              |
| 4   | The superseded-row guard             | **Pass, both directions.** A new save round-tripped byte for byte |
| 5   | The screenshot fetch and finding 258 | **Pass, and for the first time on any row.** See below            |
| 7   | The retired identifiers endpoint     | **Pass.** Art on screen, and the game list is unchanged           |
| 8   | The append-only background log       | **Pass.** See below                                               |
| 9   | Always owed on a move                | **Pass.** 0 downloaded, 0 written, `gamelist.xml` byte-identical  |

**Step 3 was not re-run and does not need to be.** All three moves carried it for the same
reason, that `nes` requires no BIOS and no firmware code or bundled data changed, and re-running
`bios nes` would re-read the same empty requirement.

**Step 5 is the one that changed, and it is the reason the session happened.** Every earlier
revision of this file recorded the screenshot as not linking, first as RomM's fault and then, at
finding 258, as RomMBat's own naming. The fix could not be believed until a state made after it
was driven, because an unchanged state is never re-sent.

## The move to `5.3.0`

**It touches step 9 and nothing else, and step 9 was re-run.** Mapped from finding 13 of
`docs/romm-5.3-findings.md`, which is 14 upstream commits with no contract change, and from this
repo's `src/` and `data/` diff across the move, which is the two version constants and comments.
It applies to all nine rows alike, because nothing a single row exercises moved.

| #   | At `5.3.0`  | Why                                                                                                  |
| --- | ----------- | ---------------------------------------------------------------------------------------------------- |
| 1   | Carried     | `GET /api/platforms` and the bundled mapping data are unchanged                                      |
| 2   | Carried     | `GET /api/roms` takes the same 58 parameters and the resolver is unchanged                           |
| 3   | Carried     | No firmware route or BIOS data changed, and `nes` requires no BIOS                                   |
| 4   | Carried     | `add_save`, `prune_slot` and negotiate are unchanged, and `s4-older-mtime.py` answers as at `beta.1` |
| 5   | Carried     | `POST /api/states` and the screenshot link rule are unchanged                                        |
| 6   | N/A         | Unchanged: `nes` has no class D                                                                      |
| 7   | Carried     | The game list's query is unchanged; the gallery work in the delta is RomM's own web UI               |
| 8   | Carried     | Both play-session routes are unchanged                                                               |
| 9   | **Touched** | Always touched on a move. **Pass**, re-run on 2026-09-21, see below                                  |

**Step 9 was re-run on a deploy of the adoption branch**, made by `tools/publish.ps1 -Deploy`, with
`status` reading the server as `5.3.0`, Supported. `sync` answered `nothing to do: 227 games
already present, 0 downloaded, 0 written` for `nes`, the same for the 252-game `megadrive` set,
and `gamelists: all 2 unchanged`, with every `gamelist.xml` md5'd either side and identical. One
re-sync covers all nine rows, because none of them owns anything a sync touches that another
does not.

**#4648 is the one change on an authentication path, and it cannot reach this client**: a bearer
request carrying a session cookie now keeps the CSRF check, and `RomMConnection` sends no cookie.

## The move to `5.3.1`

**It touches steps 1 and 9, and both were re-run on 2026-09-24.** Mapped from finding 14 of
`docs/romm-5.3-findings.md`, 161 upstream commits with no schema change, and from this repo's
`src/` and `data/` diff across the move, which is the two version constants and
`PlatformMapStore.Record`'s case-only rekey. It applies to every row alike, because nothing a
single row exercises moved.

| #   | At `5.3.1`  | Why                                                                                                               |
| --- | ----------- | ----------------------------------------------------------------------------------------------------------------- |
| 1   | **Touched** | RomM #4676 lets an `fs_slug` change case and `platform_map` now rekeys for it. **Pass**, see below                |
| 2   | Carried     | `GET /api/roms` takes the same parameters, and `roms/files.py` changed only in typing                             |
| 3   | Carried     | `endpoints/firmware.py` is byte-identical, `firmware_handler.py` changed only in typing, and `data/` is unchanged |
| 4   | Carried     | `saves.py` and `sync.py` are byte-identical, and `s4-older-mtime.py` answers all six cases as at `5.3.0`          |
| 5   | Carried     | `states.py` is byte-identical, and `screenshots_handler.py` changed only in a type annotation                     |
| 6   | N/A         | Unchanged: `nes` has no class D                                                                                   |
| 7   | Carried     | The game list's query is unchanged; the Player changes are gamepad focus in RomM's own web UI                     |
| 8   | Carried     | `play_sessions.py` is byte-identical, and its handler changed only in how it counts rows                          |
| 9   | **Touched** | Always touched on a move. **Pass**, see below                                                                     |

**Both were re-run on a deploy of the adoption branch**, made by `tools/publish.ps1 -Deploy`, with
`status` reading the server as `5.3.1`, Supported. `platforms list` was identical either side of
the deploy, with `nes` resolved by `fs_slug` as before. `sync` answered `nothing to do: 227
games already present, 0 downloaded, 0 written` for `Spinnich's Nintendo Entertainment System Favorites`, and `gamelists: all 8 unchanged`,
with every `gamelist.xml` md5'd either side and identical, and a `flush` moved no save or state.
One re-sync covers every row, because none of them owns anything a sync touches that another does
not.

## Checklist

All nine were stated at `5.3.0-beta.1`, the floor the client declared then, and hold at `5.3.0`
and `5.3.1` by the tables above. Step 3 is
carried from the 5.2.0 measurement for the reason the move tables give; the other eight were
measured or re-measured on 2026-09-20.

| #   | Step                                                          | Result                                                                                     |
| --- | ------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| 1   | Folder mapping resolves, layer recorded                       | **Pass**, at layer `fs_slug`. See below                                                    |
| 2   | `<extension>` captured; every resolved ROM survives the check | **Pass.** 228 of 228 resolved, nothing excluded. The step changed on 2026-09-20, see below |
| 3   | Listed BIOS resolved against RomM by md5                      | **Pass**, carried. RetroBat requires no BIOS for `nes`                                     |
| 4   | Save shape classified, battery save round-trips               | **Pass, both directions.** Class A, and the md5 is equal up and down. See below            |
| 5   | Save state round-trips with its screenshot                    | **Pass, both ways, screenshot included.** See below                                        |
| 6   | Per-game memory card where class D applies                    | **N/A.** See below                                                                         |
| 7   | Launches from EmulationStation with art and metadata          | **Pass.** Box art confirmed rendering in the game list, metadata present                   |
| 8   | Play session recorded and reaches RomM                        | **Pass.** Session 265 on the server, matching the journal to the second. See below         |
| 9   | Re-sync is a clean no-op                                      | **Pass.** 0 downloaded, 0 written, `gamelist.xml` byte-identical                           |

### 1. Mapping

Resolved at layer **`fs_slug`**, not `bundled`. RomM's `fs_slug` for this platform is literally
`nes`, which is already a folder in this install, so layer 2 matches and the bundled table is
never consulted.

**That is a property of this RomM instance, not of the platform.** An instance whose library
folders carry RomM's canonical slugs would resolve the same platform at `bundled` instead. Both
are correct; the record names which happened here.

**Two RomM platforms map to this one folder:**

| `fs_slug`        | id  | Resolved by | Name                                                     |
| ---------------- | --- | ----------- | -------------------------------------------------------- |
| `nes`            | 279 | `fs_slug`   | Nintendo - Family Computer / NES (Headered)              |
| `nes-unofficial` | 306 | `bundled`   | Nintendo - Family Computer / NES (Headered) (Unofficial) |

So `roms/nes/` can receive games from either, and a set scoped to one of them is not the whole
of what lands in the folder.

### 2. Extensions

From the **live** `es_systems.cfg` on this install, not the vendored copy:

```text
.fds .nes .wad .zip .7z
```

**This list is a union across every emulator the system declares, and `nestopia` does not promise
all of it.** RetroBat publishes no per-`(emulator, core)` extension data anywhere: `es_features.cfg`
mentions "extension" 28 times and every one is an N64 controller pak. `.fds` is Famicom Disk
System and `.wad` is not a NES container at all, so neither is a claim about this row until a
launch proves it.

Observed to launch under `libretro`/`nestopia`: **`.zip`**, which is what all 228 synced games
are and what the one logged launch used. `.nes`, `.fds`, `.wad` and `.7z` are declared by the
system and unproven for this row.

**Nothing was excluded, which is the half of the step that matters and it passes.** The set
resolved 228 of 228 twice on 2026-09-20, once as a preview and once for real, with no game
dropped for its extension and none reported as refused.

**The step used to ask for the reverse, and held this row open for a year of calendar time over a
property of the library.** It required a known-unsupported file to be excluded and reported, and
every NES ROM here is `.zip`, so there was nothing to refuse. Changed on 2026-09-20 after the
question was put to the maintainer: the risk runs the other way. A file wrongly downloaded costs
bytes and a game that does not appear, because EmulationStation filters by `<extension>` itself. A
file wrongly **excluded** is a game the user asked for silently missing, with no error and no line
in the report worth questioning. The union list above is the reason: it cannot be precise per
`(emulator, core)`, so over-rejection is the likelier of the two errors, and `.wad` sitting in a
NES list is the proof that the list is not a statement about what any core will take.

**The exclusion half is not abandoned, it moved to where it is real.** Wave 2's disc systems carry
`.chd`, `.cue`, `.bin` and `.m3u` in one set with no manufacturing required, and over-filtering
there drops real games. That is also where multi-disc and multi-file placement has to be settled,
which is the thing this step was a poor proxy for.

### 3. BIOS

```console
$ rommbat-agent bios nes
RetroBat requires no BIOS for nes.
$ echo $?
0
```

**A real system with nothing to fetch, which counts as step 3 passing.** `nes` is one of the
sixteen systems whose requirement is empty, so none of the four fetch states arises: nothing is
present, fetched, missing from the library, or hashless.

The refusal path was checked at the same time, because "no BIOS needed" and "that system does
not exist" must not look alike:

```console
$ rommbat-agent bios ness
'ness' is not a system in this install's es_systems.cfg.
Run 'platforms' to see the systems this install declares.
$ echo $?
2
```

A mistyped name is refused with a non-zero exit rather than reported as a system needing no
firmware.

## The set

Not a hand-picked set. A smart collection, which is an ordinary scope:

|          |                                                    |
| -------- | -------------------------------------------------- |
| Name     | Spinnich's Nintendo Entertainment System Favorites |
| Scope    | `smart_collection 9`                               |
| Policy   | no game cap, no size cap, ordered by recent        |
| Resolves | 228 games, 30.2 MB, into `nes`                     |

### 4. Battery save

Driven on **The Legend of Zelda (USA) (Rev 1)**, played for about two minutes from
EmulationStation, with an in-game save made on the save screen.

|             |                                                                                      |
| ----------- | ------------------------------------------------------------------------------------ |
| On disk     | `saves/nes/Legend of Zelda, The (USA) (Rev 1).srm`, 8,192 B                          |
| Shape       | **Class A confirmed**: a loose `.srm` under `saves/<system>/`, keyed by ROM filename |
| Local md5   | `0dda7bfc92305642b6120324911e0362`                                                   |
| Uploaded as | save **196**, `... [2026-09-12_18-27-13].srm`, 8,192 B                               |
| Server hash | `0dda7bfc92305642b6120324911e0362`, **equal to the local one**                       |

**Checked as content, not as existence.** No `.srm` existed for this system before the session,
and the file is 6,311 non-zero bytes of 8,192 carrying the save-slot name `LINK` at offset 2 in
Zelda's own character encoding. So this is a player save rather than a file the core wrote at
boot, which is the trap `save_shapes.json` records for `mastersystem` and which a size check
alone would not catch.

**The download half was driven twice, and the second time it worked.**

The first pass, before #136, deleted the backed-up `.srm` from the tree and found that neither
`flush` nor `sync` brought it back, which was **#84 reproduced deliberately** rather than
inferred. The mechanism was that save sync is scan-driven: candidates come from saves found on
disk, so a save with no local file was never considered. #136 added `saves restore`, which walks
the server's save list instead, and #140 did the same for states.

**The second pass, on the merge of both, brings it back byte for byte.** Same file, deleted from
the tree again after a backup and an md5:

```console
$ rommbat-agent saves restore --apply
  save   rom 158633  libretro:battery          8 KB  2026-09-12 18:27  saves/nes/Legend of Zelda, The (USA) (Rev 1).srm
  state  rom 158633  libretro.nestopia      10.2 KB  2026-09-12 18:27  saves/nes/libretro.nestopia/Legend of Zelda, The (USA) (Rev 1).state1
restored 1 save(s) and 1 state(s), failed 0, 18.2 KB
```

|                     | Before the delete                  | After the restore                  |
| ------------------- | ---------------------------------- | ---------------------------------- |
| `.srm`, 8,192 B     | `0dda7bfc92305642b6120324911e0362` | `0dda7bfc92305642b6120324911e0362` |
| `.state1`, 10,459 B | `2e4d4b06ade2cfd5cbe2f769531720e7` | `2e4d4b06ade2cfd5cbe2f769531720e7` |

**So step 4 closes in both directions**, and step 5's state half closes with it. A `flush`
afterwards reported `saves: 3 of 3 attributed` and `states: 1 already in step`, and
`saves restore 158633` then answered that everything the server holds for that ROM is already
here. No duplicate server row was created by the round trip.

Three things this pass adds, none of which stops step 4 passing.

**The first `saves restore` after the delete offered nothing, which is #147.** The save became
visible only after an unrelated `saves` run scanned the tree, because the restore asks
`local_save` what this device holds and nothing in the command rebuilds it. The state half of
the same run did not have the bug and offered its row immediately, which is what made the shape
obvious. A person whose save has just vanished runs this command first and is told there is
nothing to restore. **Since fixed by #147** in stage 2 of #195: the find scans the tree before it
reads `local_save`.

**`--apply` exited 7 with `failed 0`, which is #148.** Eighteen states on this server were
written by another client that scopes a state by core, and `es_savestates.cfg` names emulators,
so they can never be placed here. They are folded into the exit code, so on this install the
command cannot exit 0 no matter what it restores. `saves restore 158633` exits 0, because
narrowing to one ROM drops them. **Since fixed by #148** in stage 2 of #195: unplaceable rows are
listed and no longer move the exit code.

**Nothing in the tool surfaced the loss**, which is #142 and is unchanged by either PR.
`status --check-files` answered `1,316 recorded, all present`, which is exactly 228 ROMs plus
1,088 media. That sweep covers downloaded content and never looks at saves, so the save was
recoverable and still invisible. **Since fixed by #142** in stage 2 of #195: the sweep reads
`local_save` and names a missing save, and never repairs its row.

#### The save re-driven at `5.3.0-beta.1`, 2026-09-20

Owed because `SaveSync`, `SaveConflictResolver` and `SaveSlotStore` all changed across the move.
Same game, a second in-game save in a fresh session.

|                      | Value                                                               |
| -------------------- | ------------------------------------------------------------------- |
| Before the session   | `5867d7af3f402454c85464aa84b3457f`, 8,192 B                         |
| After it             | `620bd04701e61863cae7c18257a95cde`, 8,192 B, so the save did change |
| Uploaded             | by the detached `quit` pass, with nobody at a terminal              |
| Deleted and restored | `620bd04701e61863cae7c18257a95cde`, **equal**                       |

The restore named it `the newest of 8 server saves for this file` and listed the seven it did
not restore, three of them in `autosave` from the browser sessions in "RomM's browser player".
**A flush afterwards re-uploaded nothing**: `states: 15 already in step` and `playtime: nothing
queued`, exit 0. So the restore wrote its `local_save` and `local_state` rows back rather than
leaving a file the next scan would treat as new, which is the failure mode #206 describes for an
identical rewrite.

**A save carries no screenshot in RomM's UI, and that is correct rather than a gap.** The
maintainer noticed it beside the states, which do carry one. `POST /api/saves` does take an
optional `screenshotFile` and `SaveSchema` requires a `screenshot` member, so the model allows
it; what is absent is anything to send. RetroArch writes a `.png` beside a save state and never
beside an `.srm`, and the agent runs after the emulator has exited, so it has no framebuffer to
capture. The one `autosave` row on this ROM that does show a thumbnail was written by RomM's
browser player, which captures its own canvas at save time. `RomMConnection.Saves.cs` says as
much at the upload.

### 5. Save state and screenshot

Made in the same session, slot 1.

|            | On disk                                                                           | On the server               |
| ---------- | --------------------------------------------------------------------------------- | --------------------------- |
| State      | `saves/nes/libretro.nestopia/Legend of Zelda, The (USA) (Rev 1).state1`, 10,459 B | state **176**, 10,459 B     |
| Screenshot | `... .state1.png`, 3,367 B                                                        | screenshot **193**, 3,367 B |

**The declared `<directory>` is confirmed to be where nestopia really writes.**
`es_savestates.cfg` declares `{{system}}/libretro.{{core}}` for `libretro`, and
`saves/nes/libretro.nestopia/` is where the files appeared, created at first launch. The
filenames match `{{romfilename}}.state{{slot}}` and its `.png` sibling exactly.

**The uploaded name carries the core scope**, `Legend of Zelda, The (USA) (Rev 1)
[libretro.nestopia].state1`, which is what stops two cores writing one filename from becoming one
overwritten server row. Finding 134 proved that collision; this is the fix holding on a second
platform.

**The state comes back down as well as up**, measured in the same exercise as step 4: deleted
from the tree and restored by `saves restore --apply` at an identical md5, into the same declared
directory it was written from.

**The screenshot did not link, which is finding 138 recurring.** It uploaded, stored against the
ROM at the right name and size, and the state still reads `screenshot: null`. Finding 138
measured this at roughly a third of thirty-five attempts on `mastersystem` under
`genesis_plus_gx`; seeing it on `nes` under `nestopia` shows it is **not specific to a platform or
a core**. Nothing here suggests a RomMBat fault: the asset is on the server, correctly named.

**What changed is the cost of that gap.** While states only went up, an unlinked screenshot was
cosmetic. Now that a state comes back, the screenshot does not come with it: the restore has only
the state's own `screenshot` field to follow, that field is null, and screenshot 193 sits on the
server unreachable. `.state1.png` was the one file of the three that did not return. So step 5
stays open on the screenshot, and the reason it stays open is now a loss rather than an untidy
record.

**The missing link is only half of it, and the other half is RomMBat's.** `RestorableState`
carries no screenshot member, `RestoreAsync` fetches `DownloadStateAsync` and nothing else, and
`RomMConnection.States` has an upload path for a screenshot with no download counterpart. So a
state whose `screenshot` field did link would still not bring its `.png` back today. Step 5 needs
#158 as well as the RomM-side link, and no `(system, emulator, core)` row can pass it on either
alone.

**Since closed on RomMBat's side by #158**, in stage 2 of #195: a restore now fetches a linked
screenshot into the declared `<image>`. Not re-driven on a linked screenshot, because none exists:
at the 5.3.0-alpha.2 floor all 9 `nes` states on the instance read `screenshot: null`, and 7 of 7
re-uploaded ones came back unlinked. The `.srm` and `.state1` of Crystalis (USA) were deleted and
restored byte-identically in the same pass. Findings 138 and 256 in `docs/retrobat-findings.md`.

**Diagnosed since, and neither half was RomM's** (finding 258). RomM links a screenshot to a
state by filename, and RomMBat uploaded this one as `Legend of Zelda, The (USA) (Rev 1).state1
[libretro.nestopia].png`, which that rule can never match against the state's name. The two
paragraphs above that call the link RomM's, and "a finding 138 recurrence", describe the cause
as it was understood when they were written. It is fixed: the image now goes up as the state's
upload name plus `.png`. Screenshot 193 stays unlinked, because an unchanged state is not re-sent,
so step 5 needs a state made after the fix.

**States carry no `content_hash` at all.** The state object has no such field, where the save has
one that matched. So a state cannot be verified on download the way a save can, and RomMBat has
nothing to compare against. The restore says so itself rather than implying a check it cannot
make, and it warns that a state carries emulator and core but no version. That is a property of
RomM's model rather than of this platform.

#### Driven on a state made after the fix, 2026-09-20, and it links

Everything above this heading describes states uploaded before finding 258 was fixed. Three new
ones were made in one EmulationStation session and they are what the step now rests on.

| Slot                  | On disk                                               | Screenshot md5 |
| --------------------- | ----------------------------------------------------- | -------------- |
| `libretro:nestopia:2` | `Legend of Zelda, The (USA) (Rev 1).state2`, 10,252 B | `ecd1d57f...`  |
| `libretro:nestopia:3` | `Legend of Zelda, The (USA) (Rev 1).state3`, 10,253 B | `d75aca69...`  |
| `libretro:nestopia:4` | `Legend of Zelda, The (USA) (Rev 1).state4`, 10,242 B | `d75aca69...`  |

**The slot is RetroArch's choice, not EmulationStation's, and this paragraph first said the
opposite.** `emulatorLauncher.log` logged `-state_slot 2` on the launch and the first state of the
session landed in slot 2, which was read as ES deciding it. The `libretro`/`mesen` pass on
2026-09-21 disproved that: `-state_slot 5` on the launch line, states written as `.state1` and
`.state2`. RetroArch runs with `savestate_auto_index` on and continues from the highest slot
already in the core's directory, and `libretro.nestopia/` already held slot 1 here, so slot 2 was
RetroArch's pick that happened to match. Finding 261.

Slot 2 was deleted from the tree, with its `.png`, and restored:

```console
$ rommbat-agent saves restore 158633
  state  rom 158633  libretro.nestopia        10 KB  2026-09-20 14:23  saves/nes/libretro.nestopia/Legend of Zelda, The (USA) (Rev 1).state2
         with its screenshot, Legend of Zelda, The (USA) (Rev 1).state2.png
$ rommbat-agent saves restore 158633 --apply
restored 1 save(s) and 1 state(s), failed 0, 20.1 KB, with 1 screenshot(s)
```

|                        | Before the delete | After the restore |
| ---------------------- | ----------------- | ----------------- |
| `.state2`, 10,252 B    | `4e257f3b...`     | `4e257f3b...`     |
| `.state2.png`, 2,144 B | `ecd1d57f...`     | `ecd1d57f...`     |

**The screenshot was checked by its bytes, not by its arrival**, which is the check finding 258
makes necessary: RomM can answer a libretro slot with another slot's image, and a restore that
merely produces a `.png` would not notice. Slots 3 and 4 were saved on the same frame and share
one image, `d75aca69...`; slot 2's is its own. What came back is `ecd1d57f...`, so the link is to
this state and not to a neighbour, and not to the slot 1 image that has sat unlinked here since
September 12.

**Slot 1 is still unlinked and is left that way on purpose.** It was uploaded before the fix, an
unchanged state is never re-sent, and leaving it is what makes the contrast between the two
readable on one install.

### 6. Per-game memory card

**N/A, and recorded rather than left blank.** `save_shapes.json` gives `nes` no
`DependsOnEmulator` flag and no `per_game_conversion` block, so there is no shared container to
opt a game out of. This holds for every wave 1 system.

### 7. Launch, art and metadata

A game launched from EmulationStation after the sync, and `emulatorLauncher.log` records the
emulator and core, which is what makes the row's claim checkable rather than assumed.

`gamelist.xml` carries **228 `<game>` elements** with names and descriptions. On disk:

| Kind       | Files | Size   | Coverage of 228    |
| ---------- | ----- | ------ | ------------------ |
| ROMs       | 228   | 31 MB  | -                  |
| `images/`  | 665   | 166 MB | about 2.9 per game |
| `videos/`  | 216   | 362 MB | 94.7%              |
| `manuals/` | 207   | 404 MB | 90.8%              |

**Media is 30 times the ROM bytes here**, 932 MB against 31 MB, which is the ratio the rollout
warns a budget has to be sized for. This wave ran with the budget off, so nothing was truncated.

**Those coverage figures are a dated observation about this RomM library, not a platform result
and not a RomMBat capability.** They say when this platform was last scraped and with what
settings. Video at 94.7% here is well above the 72.1% finding 92 measured library-wide, and the
administrator is actively removing videos, so this number is expected to fall. An absent kind is
the ordinary `Missing` case.

**Box art renders correctly in the EmulationStation game list**, confirmed on screen rather than
inferred from files on disk, so step 7 passes.

**But the art is the wrong source, and that is a defect rather than an observation.** This
install has RetroBat's default `ScrapperImageSrc = sstitle`, meaning Title Screenshot.
`GameMetadata.cs:183-185` hardwires the three tags instead:

```csharp
Add(MediaKind.Image, row.CoverLargePath);
Add(MediaKind.Thumbnail, row.CoverSmallPath);
Add(MediaKind.Marquee, row.ScreenScraper?.LogoPath);
```

`path_cover_large` is RomM's cover, which it sources from ScreenScraper's 2D box: this library's
`url_cover` for a NES game ends `media=box-2D%28us%29` outright. So `<image>` is the box, the
user asked for the title screen, and `ss_metadata.title_screen_path` is populated and unread.

**RomMBat silently ignores a setting the user changed**, which is why this belongs above the
"observation" line. #108 already predicted this exact symptom and had confirmed it on two
installs; this is the third, and the first where a person noticed it from the game list rather
than from reading config. #108 also already designs the re-fetch a source change needs, by
recording which source filled a slot on the `local_file` row. What is arguably wrong there is the
`enhancement` label: the mapping is an enhancement, but "the setting does nothing" is a bug.

RomM exposes fourteen `ss_metadata` paths against the nine values the pickers offer, so the fix
is a mapping rather than new plumbing. Whether a given library actually holds a given kind is the
ordinary `Missing` case, per finding 239.

#### Re-driven at `5.3.0-beta.1`, 2026-09-20, and the video prediction came true

Owed because the catalog query changed when `GET /api/roms/identifiers` was retired, and that
query is what fills the game list. It passes: box art was confirmed on screen again, and the
played game's `gamelist.xml` entry carries `<image>`, `<marquee>`, `<video>`, `<manual>` and
`<desc>`.

On disk, against the earlier capture:

| Kind       | Files | Size   | Offered by the server now |
| ---------- | ----- | ------ | ------------------------- |
| ROMs       | 228   | 32 MB  | -                         |
| `images/`  | 676   | 168 MB | 224 of each of 3 kinds    |
| `videos/`  | 216   | 362 MB | **8**                     |
| `manuals/` | 215   | 423 MB | 209                       |

**The sync's media line fell from 1,088 to 889, and that is the administrator's deletion, not a
regression.** The paragraph above predicted exactly this. Traced rather than assumed: for 208 of
the 216 games with a video on disk, `path_video` now reads null, and their `ss_metadata` carries
`video_path` and `video_normalized_path` both null while still holding the ScreenScraper URLs
the asset would be fetched from. The eight survivors answer with a
`video_normalized/video-normalized.mp4` path. 224 + 224 + 224 + 209 + 8 is 889 exactly.

**The files already on disk stayed, and the gamelist still points at them.** `Removed` only fires
when a _kind_ stops being wanted, which is a setting the install owns; a kind that is still wanted
and merely no longer offered is the ordinary `Missing` case and reclaims nothing. That is the
right way round, because the artwork is still good and the alternative is deleting a user's media
whenever their server has a gap.

### 8. Play session

The hook chain fired on a fresh install with nobody at a terminal, which is rule 4 working. The
block is the earlier capture, taken before the Zelda launch, which is why it stops at 18:13:51:

```text
2026-09-12 18:01:34Z  start  background start started
2026-09-12 18:01:45Z  start  background start finished, flush exit 0
2026-09-12 18:13:46Z  quit   background quit started
2026-09-12 18:13:51Z  quit   background quit finished, flush exit 7
```

`start` and `quit` each spawned a detached pass; `game-start` and `game-end` journalled and
started nothing. **The step passes**, confirmed on the server rather than from the hook log:
`last_played` moved at 18:27:15 from a quit hook that fired at 18:27:07, and `now_playing`
cleared. An earlier revision of this section said the step was not passed, which was true of the
first accidental ten-second launch and was left standing after the Zelda session closed it.

#### The session re-driven at `5.3.0-beta.1`, 2026-09-20

Owed because the detached pass the hooks spawn writes its log through a new append-only handle.
The whole chain fired again, with nobody at a terminal:

```text
2026-09-20 14:20:50Z  start  background start started
2026-09-20 14:20:55Z  start  background start finished, flush exit 0
2026-09-20 14:23:47Z  quit   background quit started
2026-09-20 14:23:54Z  quit   background quit finished, flush exit 0
```

**The append-only handle appends**, which is the half this move owed: the file above carries
every pass back to 2026-09-12 with the new entries at the end, and the truncation visible in the
2026-09-12 block is older than the change.

The journal holds the session the hooks captured, and the correlation closed it:

| Event        | Recorded     | Correlated   | State        |
| ------------ | ------------ | ------------ | ------------ |
| `start`      | 14:20:50.841 | 14:20:51.028 | `correlated` |
| `game-start` | 14:21:42.517 | 14:23:47.876 | `correlated` |
| `game-end`   | 14:23:45.327 | 14:23:47.876 | `correlated` |
| `quit`       | 14:23:47.690 | 14:23:47.876 | `correlated` |

`game-start` carries `roms/nes/Legend of Zelda, The (USA) (Rev 1).zip` and nothing else does,
which is rule 4 holding: the two hooks inside the launch path journalled and started nothing,
and the two outside it each spawned the pass that drained what they left. EmulationStation's own
write agrees, at `<playcount>4</playcount>` and `<lastplayed>20260920T102345</lastplayed>`, the
`game-end` second.

**The server holds it, and that is what makes the step pass:**

| Field             | Session 265           | Matches                           |
| ----------------- | --------------------- | --------------------------------- |
| `start_time`      | `2026-09-20T14:21:42` | `game-start`, journalled at .517  |
| `end_time`        | `2026-09-20T14:23:45` | `game-end`, journalled at .327    |
| `duration_ms`     | `122809`              | the 123 s between them            |
| `device_id`       | `cf1cc550-...`        | `status`'s **`romm device`** line |
| `sync_session_id` | `null`                | -                                 |

**The row carries the RomM-side device id, not the local one**, and the two are different values
that `status` prints on adjacent lines. A `?device_id=` filter given the local id returns `200`
with zero rows, which reads exactly like a session that was never written.

**Reading it back needs a token for the account the install is paired as, and that is #208.**
`SendPlaySessionsAsync` posts and nothing in the client reads back, so `playtime: nothing queued`
says the outbox is empty rather than that a row exists. `GET /api/play-sessions` is scoped to the
authenticated user: with the correct device id above, a second account's token answers `200` with
**0** rows where the owning account's answers `200` with **5**. That is identity rather than
permission, and no scope changes it. A read-only `roms.user.read` token on the owning account is
what settled this step, and `GET /api/roms/{id}` is `403` under that scope, so `last_played` was
never read; the session row is the stronger evidence anyway.

**Closed since, and this paragraph is what it was measured against.** #208 added the read:
`status` now prints a `Playtime` block with the count and the last session, filtered by the
`romm device` id above, so a later pass settles step 8 from the agent rather than from a
second token. The two traps recorded here are unchanged and are why the block reports an empty
answer as found-nothing rather than sent-nothing.

### 9. Re-sync

```text
plan:      nothing to do: all 228 games are already present and verified
done:      228 games already present, 0 downloaded, 0 written
media:     1088 already present
gamelists: all 1 unchanged
```

`gamelist.xml` was compared byte for byte before and after and is **identical**, rather than
taken from the command's own report. A second `flush` exited 0, re-attributed the same one save
and one state, and created **no duplicate server rows**: still save 196 and state 176 alone.

Two junk save records for `River City Ransom (USA)` failed every flush until they were deleted
server-side, which is what the earlier exit 7 was. Their removal is why this run is clean, and
that was confirmed against the server rather than assumed from the quieter output.

#### The re-sync re-driven at `5.3.0-beta.1`, 2026-09-20

Run twice, once before the play session and once after it, and clean both times:

```text
plan:      nothing to do: all 228 games are already present and verified
done:      228 games already present, 0 downloaded, 0 written
media:     889 already present
gamelists: all 1 unchanged
```

`gamelist.xml` was md5'd either side of each run and is identical across both, exit 0.

**It is not identical across the session, and the writer is EmulationStation.** The file went
from `5a35c317...` to `585d259f...` between the two runs, because ES rewrote the played game's
`<playcount>` to 4 and its `<lastplayed>` to `20260920T102345`, which is the `game-end` second.
RomMBat reported `all 1 unchanged` on both sides of that and left the file alone, so the churn
step 9 watches for is absent; a record that md5'd only across the whole session would have read
ES's own write as a sync defect.

## What the pass turned up

**Two server save records that cannot verify, and refusing them is correct.** A flush reported
`2 failed, 21 skipped` for `River City Ransom (USA)`:

```text
saves/nes/River City Ransom (USA).srm: what arrived hashes to 8a7f1ea8506132daf84235d79f1430c8
and the server said 387fab0e16d9b314e6fa4c95addf8f38. Nothing was written and the server was
not told it arrived.
```

Investigated rather than assumed. The server holds three saves for this ROM:

| id  | declared | served   | md5 of bytes  | `content_hash` |
| --- | -------- | -------- | ------------- | -------------- |
| 101 | `.srm`   | a ZIP    | `8a7f1ea8...` | `387fab0e...`  |
| 100 | `.zip`   | a ZIP    | `8a7f1ea8...` | `387fab0e...`  |
| 82  | `.srm`   | raw, 4 B | `37a6259c...` | `37a6259c...`  |

Both zips hold one 4-byte member, `River City Ransom (USA).srm`, whose content is literally
`null` and whose md5 is `37a6259c...`. So the declared `387fab0e...` is **neither the archive
bytes nor the member**, and it is not the member-wise fold either, which was computed and
checked. Record 82, older, has a `content_hash` that matches its bytes exactly.

**So these are two stale records left by another client**, their stored paths naming `freegosy`
and `fceumm`, and RomMBat did the right thing: refused, named both hashes, wrote nothing, and did
not acknowledge. Incidentally this shows #81 is fixed, since the message names both sides.

**One latent defect it exposed, which is RomMBat's.** Record 101 declares `ext=srm` and the
server serves ZIP bytes for it. `SaveSync.DownloadAsync` byte-hashes the download for class A and
moves it to the destination with no unwrapping, so had the hash matched it would have written a
ZIP to `saves/nes/River City Ransom (USA).srm`, which nestopia cannot read. The mismatch masked
it. The trigger here is bad data rather than ordinary operation, so this wants filing and
measuring against a save RomMBat itself uploaded, not a fix written from this record alone.

**Eighteen states on this server can never be placed on this install, and none of them is
RomMBat's.** Their slots name a core where `es_savestates.cfg` names an emulator: `fceumm`,
`genesis_plus_gx`, `snes9x`, `mgba`, `mupen64plus_next`, `pcsx_rearmed`, `beetle_psx_hw` and
`dc`. They were written by another client against the same library, the same one that left the
two stale `River City Ransom` saves. RomMBat names each one and the reason rather than dropping
it, which is right, but it also folds them into the exit code, which is #148 (since fixed in
stage 2 of #195, where they no longer move it). Four of the
eighteen are `nes` rows under `fceumm`, so **this is what a second `(emulator, core)` row's
states would look like to a restore** if RomMBat ever scoped one by core alone. It does not:
`ScopeOf` writes `{emulator}.{core}`, which is what makes these rows unplaceable and RomMBat's
own placeable.

**A stock 8.2.1 install ships 1,231 MAME nvram directories**, and `saves` reports every one as a
directory save that is not sent, with 1,531 files unsyncable for `no matching ROM`. Nothing here
is the user's, and no MAME ROM is on the device. Same class as #83.

## `libretro`/`fceumm` and `libretro`/`mesen`

**Both certified on 2026-09-21**, at RomM `5.3.0-beta.1` and RetroBat 8.2.1, on the same install
and server as the re-drive above. The client was a deploy of `main` at a901af3, the merge of #210,
made by `tools/publish.ps1 -Deploy R:\RetroBat`. So `status`'s `Playtime` block settled step 8,
not a second token. The maintainer played; everything after the quit was checked from the agent,
the store and the server.

|              | `libretro`/`fceumm`                                                     | `libretro`/`mesen`                                                 |
| ------------ | ----------------------------------------------------------------------- | ------------------------------------------------------------------ |
| Selected by  | **Nothing: RetroBat's default.** `nes.emulator` and `nes.core` removed  | `nes.emulator = libretro`, `nes.core = mesen` in `es_settings.cfg` |
| Confirmed by | `-emulator libretro -core fceumm`, ran `fceumm_libretro.dll`            | `-emulator libretro -core mesen`, ran `mesen_libretro.dll`         |
| Game         | Final Fantasy III (Japan) [T-En by Chaos Rush v1.3], rom 190006, no pin | The Legend of Zelda (USA) (Rev 1), rom 158633, no pin              |
| Save made by | Saving from the world-map menu                                          | Registering a name on the file-select screen                       |

**`fceumm` is the row a stock install runs**, because with both keys absent ES falls through to
the first emulator and core `es_systems.cfg` lists for `nes`, `libretro` then `fceumm`. It is the
row most users will actually have. The three unrelated `nes.*` keys recorded for `nestopia` were
still set and do not reach this core.

**Neither game carries a per-game `<emulator>` in `gamelist.xml`**, which is checked because eight
games on this install do, left by "How each row was selected" below: StarTropics and Ultima, the
first two picks, were still pinned to `bizhawk` and would have run the wrong row. A pinned game
overrides the system setting without a trace in `es_settings.cfg`, so read the launch line.

**For a faster pass, pick a game whose battery save is quick to make.** Zelda writes one the
moment a name is registered, where Final Fantasy III needed play to the world map.

### Checklist for both

Steps 1, 2 and 3 are the system's and carry from `nestopia`'s re-drive the same day. Step 6 is N/A
for the same reason.

| #   | `libretro`/`fceumm`                                                                    | `libretro`/`mesen`                                               |
| --- | -------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| 1   | **Pass**, carried: `fs_slug`, the same folder                                          | **Pass**, carried                                                |
| 2   | **Pass**, carried: 228 of 228, nothing excluded. `.zip` observed to launch on this row | **Pass**, carried. `.zip` observed to launch on this row         |
| 3   | **Pass**, carried: RetroBat requires no BIOS for `nes`                                 | **Pass**, carried                                                |
| 4   | **Pass, both directions.** Class A, new `.srm`, equal md5 up and down                  | **Pass, both directions.** Class A, the shared `.srm`, equal md5 |
| 5   | **Pass**, screenshot byte-checked                                                      | **Pass**, screenshot byte-checked                                |
| 6   | **N/A**, no class D                                                                    | **N/A**                                                          |
| 7   | **Pass.** Box art on screen, all five media kinds and `<desc>` in the entry            | **Pass.** Box art on screen                                      |
| 8   | **Pass.** 10:28:21Z to 10:36:59Z, 8m 37s, rom 190006                                   | **Pass.** 10:52:22Z to 10:53:36Z, 1m 14s, rom 158633             |
| 9   | **Pass.** 0 downloaded, 0 written, `gamelist.xml` byte-identical                       | **Pass.** 0 downloaded, 0 written, `gamelist.xml` byte-identical |

**A baseline was taken before either session**: `sync` a no-op with `gamelist.xml` unchanged, and
`flush` with nothing queued. So everything below is the session's and not left over.

### 4. Battery save on both

|                      | `fceumm`                                                 | `mesen`                                                     |
| -------------------- | -------------------------------------------------------- | ----------------------------------------------------------- |
| On disk              | `saves/nes/Final Fantasy III (Japan) [...].srm`, 8,192 B | `saves/nes/Legend of Zelda, The (USA) (Rev 1).srm`, 8,192 B |
| Before the session   | absent, and nothing on the server for the ROM            | `620bd047...`, `nestopia`'s save from 2026-09-20            |
| After it             | `63c189d2...`                                            | `9136743b...`                                               |
| Uploaded as          | save **348**, `libretro:battery`, by the `quit` pass     | save **349**, `libretro:battery`, by the `quit` pass        |
| Deleted and restored | `63c189d2...`, **equal**                                 | `9136743b...`, **equal**                                    |

**Zelda's was checked as content**: the name `SPINNICH` sits at offset 2 in Zelda's own character
encoding and the earlier `LINK` is gone, which is what the maintainer entered. It is the same file
`nestopia` wrote, because the three `libretro` cores share `saves/nes/<rom>.srm`, so this pass
also shows a second core writing into a slot the first one owns: it went up as a new version of
`libretro:battery` with no conflict. The restore named it the newest of 9 server saves and listed
the 8 it did not restore.

**Neither upload needed a terminal.** Both arrived through the detached `quit` pass, which exited 0
each time, before anything was run by hand. A `flush` after each restore reported the restored
files `already in step` and sent nothing.

### 5. State and screenshot on both

Two states each, made on different screens so each has its own image.

| Row      | Slot | On the server | State md5     | Screenshot md5 |
| -------- | ---- | ------------- | ------------- | -------------- |
| `fceumm` | 1    | state **191** | `b46171f3...` | `96f8daa0...`  |
| `fceumm` | 2    | state **192** | `5f97a87b...` | `a55104f5...`  |
| `mesen`  | 1    | state **193** | `3b6d3ec2...` | `758a2549...`  |
| `mesen`  | 2    | state **194** | `b13f8c28...` | `9f4a41bd...`  |

**The declared `<directory>` is where both cores wrote**: `saves/nes/libretro.fceumm/` and
`saves/nes/libretro.mesen/`, per `{{system}}/libretro.{{core}}`, with `{{romfilename}}.state{{slot}}`
and its `.png` beside it. Each uploaded name carries its own core, `[libretro.fceumm]` and
`[libretro.mesen]`, so `mesen`'s Zelda states sit on the server beside `nestopia`'s without
touching them.

On each row, slot 2's state and `.png` were deleted with the `.srm`. The preview named the
screenshot it would bring back, and `--apply` answered `restored 1 save(s) and 1 state(s), failed
0, ... with 1 screenshot(s)`, exit 0. **Every file came back at its own md5**, and slot 2's image
is not slot 1's on either row, so the link is to that state and not to a neighbour.

**`-state_slot` did not pick the slot**, and that corrected the `nestopia` record. `mesen` was
launched with `-state_slot 5` and wrote slots 1 and 2, and RetroArch's log shows it choosing:
`found_last_state_slot: #0` against the empty `libretro.mesen/`. `fceumm`'s launch carried no
`-state_slot` at all. Finding 261.

### 7. Launch on both

Box art for both games was confirmed on screen in the NES game list by the maintainer. Final
Fantasy III's entry carries `<image>`, `<thumbnail>`, `<marquee>`, `<video>` and `<manual>`, each
pointing at a file that exists, and `<desc>`.

### 8. Play session on both

Read from `status` rather than inferred from the journal:

```text
Playtime
  server holds:    24 sessions for romm device cf1cc550-5203-4697-94b5-36757ac9a334
  last session:    2026-09-21 10:28:21Z to 2026-09-21 10:36:59Z, 8m 37s
  its rom:         190006
```

and after the `mesen` session, `25 sessions`, `10:52:22Z to 10:53:36Z, 1m 14s`, rom 158633. Both
starts match the launch lines in `emulatorLauncher.log`, which logs local time four hours behind.
Each `start` and `quit` pass in `background.log` exited 0.

### 9. Re-sync on both

After each session, `sync` answered `nothing to do: all 228 games are already present and
verified`, `0 downloaded, 0 written`, `889 already present` and `all 1 unchanged`, exit 0, with
`gamelist.xml` md5'd either side and identical. Across each session it changed, and as on
2026-09-20 the writer was EmulationStation updating `<playcount>` and `<lastplayed>`.

The install was left on `libretro`/`nestopia` afterwards, which is where it was found.

## `bizhawk`/`NesHawk` and `bizhawk`/`quickerNES`

**Both certified on 2026-09-21**, at RomM `5.3.0-beta.1` and RetroBat 8.2.1, on the same install
and server as above. The client was a deploy of `main` at ca2cc4f, the merge of #214, made by
`tools/publish.ps1 -Deploy R:\RetroBat`, so it carries #151's BizHawk battery saves. The
maintainer played over RDP. The second state on each row was made from the agent's session on the
RetroBat machine, which is described under step 5.

|              | `bizhawk`/`NesHawk`                                                 | `bizhawk`/`quickerNES`                                                 |
| ------------ | ------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| Selected by  | `nes.emulator = bizhawk`, `nes.core = NesHawk` in `es_settings.cfg` | `nes.emulator = bizhawk`, `nes.core = quickerNES` in `es_settings.cfg` |
| Confirmed by | `-emulator bizhawk -core NesHawk`, ran `EmuHawk.exe`                | `-emulator bizhawk -core quickerNES`, ran `EmuHawk.exe`                |
| Game         | Destiny of an Emperor (USA), rom 158207, no pin                     | The Legend of Zelda (USA) (Rev 1), rom 158633, **its pin removed**     |
| Save made by | The in-game "Record" command                                        | Registering a second name on the file-select screen                    |

**Zelda carries a `bizhawk`/`NesHawk` pin in `gamelist.xml`**, left by "How each row was selected"
below, and a pin overrides the system setting. It was removed for the `quickerNES` session with ES
closed and put back afterwards. The install was left on `libretro`/`nestopia`, where it was found.

**Destiny of an Emperor was picked because its BizHawk title is unique on this install.** BizHawk
names a battery save after its own title for the game (finding 263), and a title two ROMs answer
to is contested rather than synced (finding 262). Zelda's is unique too.

### Checklist for both `bizhawk` rows

Steps 1, 2 and 3 are the system's and carry from `nestopia`'s re-drive. Step 6 is N/A for the
same reason.

| #   | `bizhawk`/`NesHawk`                                                        | `bizhawk`/`quickerNES`                                                 |
| --- | -------------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| 1   | **Pass**, carried: `fs_slug`, the same folder                              | **Pass**, carried                                                      |
| 2   | **Pass**, carried: 228 of 228, nothing excluded. `.zip` observed to launch | **Pass**, carried. `.zip` observed to launch                           |
| 3   | **Pass**, carried: RetroBat requires no BIOS for `nes`                     | **Pass**, carried                                                      |
| 4   | **Pass, both directions.** `bizhawk:battery`, equal md5 up and down        | **Pass, both directions.** The file `NesHawk` wrote, read and extended |
| 5   | **Pass**, two slots, screenshot inside the state. See below                | **Pass**, two slots, screenshot inside the state                       |
| 6   | **N/A**, no class D                                                        | **N/A**                                                                |
| 7   | **Pass.** Box art on screen                                                | **Pass.** Box art on screen                                            |
| 8   | **Pass.** 17:19:29Z to 17:21:14Z, 1m 44s, rom 158207                       | **Pass.** 17:37:16Z to 17:38:02Z, 46s, rom 158633                      |
| 9   | **Pass.** 0 downloaded, 0 written, `gamelist.xml` byte-identical           | **Pass.** 0 downloaded, 0 written, `gamelist.xml` byte-identical       |

**A baseline was taken before either session**: `sync` a no-op with `gamelist.xml` unchanged, and
`flush` with nothing queued.

### 4. Battery save on both `bizhawk` rows

|                       | `NesHawk`                                                  | `quickerNES`                                              |
| --------------------- | ---------------------------------------------------------- | --------------------------------------------------------- |
| On disk               | `saves/nes/bizhawk/Destiny of an Emperor.SaveRAM`, 8,192 B | `saves/nes/bizhawk/Legend of Zelda, The.SaveRAM`, 8,192 B |
| Before the session    | absent                                                     | `d77ad9d3...`, holding `LINK` from `NesHawk`              |
| Bound by              | the name sidecar beside the state                          | a launch covering when the save was written               |
| Uploaded              | `bizhawk:battery`, by the `quit` pass                      | `bizhawk:battery`, by the `quit` pass                     |
| At the restore        | `0b7af58a...`                                              | `2bce4ff7...`, holding `LINK` and `TEST`                  |
| Moved aside, restored | `0b7af58a...`, **equal**                                   | `2bce4ff7...`, **equal**, both names intact               |

**The two cores read each other's file**, which the "BizHawk battery saves, driven" pass left
unmeasured. `quickerNES` showed `NesHawk`'s `LINK` on Zelda's file-select screen, and the name
registered there went into the next file slot of the same `.SaveRAM`. Finding 271.

**The hash at the restore is not the hash the session left**, and the difference is the game's.
The second state on each row came from a later launch that sat on the title screen, and both
games rewrote their save on that boot: Destiny changed 4 bytes at `0x400` to `0x403` with its
saved game intact, and Zelda changed with both names intact. Each rewrite went up as a new
`bizhawk:battery` version, which is right, since the bytes changed. Finding 272. The restore
named the newest of 3 server saves on each row and listed the two it did not restore.

A `flush` after each restore reported every restored file `in step` and sent nothing.

### 5. State and screenshot on both `bizhawk` rows

| Row          | Slot | Made by                                         | State md5     | Framebuffer md5 |
| ------------ | ---- | ----------------------------------------------- | ------------- | --------------- |
| `NesHawk`    | 0    | the maintainer, pad key, in a town dialogue     | `0dca16bb...` | `6e27d64b...`   |
| `NesHawk`    | 2    | the agent's session, `Ctrl+F2`, the intro crawl | `ab6c3848...` | `bdeafc1e...`   |
| `quickerNES` | 5    | the maintainer, pad key                         | `97aad3c2...` | `45c1fb40...`   |
| `quickerNES` | 2    | the agent's session, `Ctrl+F2`, the title       | `b6fd46fb...` | `61c5052e...`   |

**BizHawk writes no screenshot file, so the screenshot half of step 5 is the state's own bytes.**
`es_savestates.cfg` declares `{{romfilename}}.QuickSave{{slot0}}.png`, and none of the four
states has one, nor do the two older ones on this install. The frame is inside the `.State`, which is
a zip holding `Framebuffer.bmp` beside the core state. So the step was checked by opening each
state's framebuffer, confirming the two on each row show different screens, and requiring the
restored state to match by md5, which carries the framebuffer with it. RomM holds no screenshot
for a BizHawk state, and the restore preview says `no screenshot: the server links none to this
state`, which is correct. Finding 268.

**The declared `<directory>` is where both cores' states land, and it is a mirror.** EmuHawk
writes `emulators/bizhawk/sstates/nes/<title>.<core>.QuickSave<n>.State`, and `emulatorLauncher`
copies it to `saves/nes/bizhawk/sstates/<core>/<rom>.QuickSave<n>.State` in the same second, with
a `.txt` sidecar holding `<title>.<core>`. The copy is `emulatorLauncher`'s, not EmuHawk's: a
state made in an EmuHawk opened directly never reached `saves/`, and the next launch through the
launcher removed it from the native directory. Finding 270. Each uploaded name carries its core,
`[bizhawk.NesHawk]` and `[bizhawk.quickerNES]`.

On each row both states and the `.SaveRAM` were moved out of the tree. The preview named both
states at their own slots, `QuickSave0` and `QuickSave2` on `NesHawk`, `QuickSave5` and
`QuickSave2` on `quickerNES`, and `--apply` answered `restored 1 save(s) and 2 state(s), failed
0`, exit 0. **Every file came back at its own md5.** This is the first restore of a `bizhawk` state
since finding 258 changed how a restore reads the slot out of an uploaded name, and the first on
any emulator that keeps the slot in the stem.

**The slot is ES's, the reverse of `libretro`.** The `quickerNES` launch carried `-state_slot 5`
and `emulatorLauncher` set EmuHawk's current slot to 5, so the pad's save key wrote `QuickSave5`;
the `NesHawk` launch carried none and the key wrote `QuickSave0`. The pad has no way to pick a slot
in game. `Ctrl+F1` to `Ctrl+F10` on a keyboard save to a slot outright, and over RDP those did not
reach EmuHawk, so the second state on each row was made from the agent's session on the RetroBat
machine: `emulatorLauncher` started with the row's `-system`, `-emulator`, `-core` and `-rom`, and
`Ctrl+F2` sent by `keybd_event` with hardware scan codes, since EmuHawk reads the keyboard through
DirectInput and ignores `SendKeys`. Finding 269. Those two launches skip ES, so the hooks did not
run, and a `flush` by hand sent each state.

### 7. Launch on both `bizhawk` rows

Box art confirmed on screen in the NES game list by the maintainer, for both games.

### 8. Play session on both `bizhawk` rows

From `status`:

```text
Playtime
  server holds:    32 sessions for romm device cf1cc550-5203-4697-94b5-36757ac9a334
  last session:    2026-09-21 17:19:29Z to 2026-09-21 17:21:14Z, 1m 44s
  its rom:         158207
```

and after the `quickerNES` session, `34 sessions`, `17:37:16Z to 17:38:02Z, 46s`, rom 158633. The
33rd is a second, short `NesHawk` session on Destiny in which the keyboard slot keys were tried
over RDP. Every `start` and `quit` pass in `background.log` exited 0, and the battery save and the
first state on each row went up through the `quit` pass before anything was run by hand.

### 9. Re-sync on both `bizhawk` rows

After the `NesHawk` restore, `sync` answered `nothing to do: all 228 games are already present and
verified`, `0 downloaded, 0 written`, `889 already present`, `all 1 unchanged`, exit 0, with
`gamelist.xml` md5'd either side and identical, and a flush after it sent nothing.

**After `quickerNES` it answered 227 and 885, and the missing game is the one `NesHawk` was driven
on.** Destiny of an Emperor (USA) left the set, `departed` at 17:32:32Z. It is not a RomMBat
result. The set is smart collection 9, whose filter is the NES platform and favourite, and a
probe set asking the server for favourites titled "Destiny of an Emperor" now answers only
Destiny of an Emperor II. RomMBat made no write to the server between the `NesHawk` step-9 sync,
which still resolved 228, and 17:32:32Z: one flush that sent nothing, then the restore's reads and
downloads. Nothing in `RomM.Client` writes a collection either. Its one user-side write is
`now_playing: false`, and `RomUserData` has no favourite field. The ROM had been a member since
the set's first sync, 2026-09-12 18:03Z, origin `synced`, and the maintainer does not recall
favouriting it. How it came to be a favourite, and why it stopped, is unexplained.

**Step 9 still passes on `quickerNES`**, because the step is about the re-sync: `0 downloaded, 0
written`, `all 1 unchanged`, `gamelist.xml` identical either side. The departed game kept its
file, its media and its `gamelist.xml` entry, since `RemoveDeparted` drops only entries whose file
has gone, and it is now an eviction candidate rather than a deletion.

## `jgenesis`, `mesen`, `mednafen` and `ares`

**All four certified on 2026-09-21**, at RomM `5.3.0-beta.1` and RetroBat 8.2.1, on the same
install and server as above. Until this pass none of them could be: `jgenesis` had no battery
rule, and the other three had neither a battery rule nor a state declaration RomMBat could read
(#150). The client was a deploy of this branch, built from the working tree that carries both, by
`tools/publish.ps1 -Deploy`. The maintainer played over RDP, and the second state on each row was
made from the agent's session on the RetroBat machine, as for `bizhawk`.

|              | `jgenesis`                                              | `mesen` standalone                            | `mednafen`                                     | `ares`                                     |
| ------------ | ------------------------------------------------------- | --------------------------------------------- | ---------------------------------------------- | ------------------------------------------ |
| Selected by  | `nes.emulator = jgenesis`                               | `nes.emulator = mesen`                        | `nes.emulator = mednafen`                      | `nes.emulator = ares`                      |
| Confirmed by | `-emulator jgenesis`, empty `-core`, `jgenesis-cli.exe` | `-emulator mesen`, empty `-core`, `Mesen.exe` | `-emulator mednafen -core nes`, `mednafen.exe` | `-emulator ares -core Famicom`, `ares.exe` |
| Game         | The Legend of Zelda (USA) (Rev 1), 158633               | The Legend of Zelda (USA) (Rev 1), 158633     | Zelda II - The Adventure of Link (USA), 159313 | The Legend of Zelda (USA) (Rev 1), 158633  |
| Save made by | Registering a name                                      | Registering a name                            | Registering a name                             | Registering a name                         |

`nes.core = nestopia` stayed set throughout and none of the four took it: the launch line carried
each emulator's own default core, or none. Zelda and Zelda II were unpinned in `gamelist.xml` for
these sessions and pinned back afterwards, and the install was left on `libretro`/`nestopia`.

**The first flush on each new build sent what had sat on this install since 2026-09-13**, written
by the first pass through these rows and unsyncable until now: the `jgenesis` Wizardry save on the
first, and the `mesen`, `mednafen` and `ares` saves and states on the second, four saves and three
states in all, from "The other eight rows" below. After the second, the store held no unsyncable
row for `nes` at all.

### Checklist for the four

Steps 1, 2 and 3 are the system's and carry from `nestopia`'s re-drive, and step 6 is N/A for the
same reason. `.zip` was observed to launch on all four.

| #   | `jgenesis`                          | `mesen` standalone                  | `mednafen`                                 | `ares`                              |
| --- | ----------------------------------- | ----------------------------------- | ------------------------------------------ | ----------------------------------- |
| 4   | **Pass, both directions**           | **Pass, both directions**           | **Pass, both directions**, the hashed name | **Pass, both directions**           |
| 5   | **Pass**, two slots, no image       | **Pass**, two slots, no image       | **Pass**, two slots, no image              | **Pass**, two slots, no image       |
| 7   | **Pass.** Box art on screen         | **Pass.** Box art on screen         | **Pass.** Box art on screen                | **Pass.** Box art on screen         |
| 8   | **Pass.** 18:02:04Z, 2m 31s         | **Pass.** 18:25:40Z, 42s            | **Pass.** 18:33:45Z, 28s                   | **Pass.** 18:36:46Z, 1m 0s          |
| 9   | **Pass.** No-op, gamelist unchanged | **Pass.** No-op, gamelist unchanged | **Pass.** No-op, gamelist unchanged        | **Pass.** No-op, gamelist unchanged |

### 4. Battery save on the four

| Row        | File                                                   | Slot               | Restored md5  | In it      |
| ---------- | ------------------------------------------------------ | ------------------ | ------------- | ---------- |
| `jgenesis` | `saves/nes/jgenesis/nes/<rom>.sav`                     | `jgenesis:battery` | `1ec1ae48...` | `LINK`     |
| `mesen`    | `saves/nes/<rom>.sav`                                  | `mesen:battery`    | `47df383e...` | `LINK`     |
| `mednafen` | `saves/nes/<rom>.88c0493fb1146834836c0ff4f3e06e45.sav` | `mednafen:battery` | `57430a0f...` | Zelda II's |
| `ares`     | `saves/nes/ares/Famicom/<rom>.ram`                     | `ares:battery`     | `7169c0de...` | `LINK`     |

Each went up through the detached `quit` pass, was moved out of the tree with both states, came
back through `saves restore --apply` at its own md5, and a flush afterwards sent nothing.

**mednafen's hash is of the ROM less its iNES header, measured on three ROMs**: Final Fantasy
(`24ae5edf...`), Zelda (`d3f45393...`, on its state) and Zelda II (`88c0493f...`) each match the md5
of the `.nes` inside the zip with its first 16 bytes left off. A restore onto a device that never
held the file computes it from the ROM there. Finding 274.

**mednafen puts the hash on only when the name without it is free.** Its own documentation says
`%M` is "empty for first evaluation per full path construction", and the first Zelda session showed
what that means: `Legend of Zelda, The (USA) (Rev 1).sav`, which `mesen` had written minutes
earlier, loaded in mednafen with the name on it, and the name registered there went into the next
file of the same `.sav`. So a plain `<rom>.sav` is one save that `mesen` and `mednafen` both read and
write, uploaded as `mesen:battery` whichever wrote it last, and mednafen writes the hashed name only
for a game with no plain one, which is why the row was driven on Zelda II. A restore refuses to
write a `mednafen:battery` save where a plain `<rom>.sav` would shadow it, rather than leave a file
mednafen never opens. Finding 273.

### 5. States on the four

| Row        | Directory                     | Slots made     | md5s                         |
| ---------- | ----------------------------- | -------------- | ---------------------------- |
| `jgenesis` | `saves/nes/jgenesis/states/`  | `_0`, `_1`     | `471160d0...`, `21b6805b...` |
| `mesen`    | `saves/nes/mesen/SaveStates/` | `_1`, `_2`     | `6f147768...`, `d15c35fa...` |
| `mednafen` | `saves/nes/mednafen/sstates/` | `.mc0`, `.mc1` | `05a6bb18...`, `48004de6...` |
| `ares`     | `saves/nes/ares/Famicom/`     | `.bs1`, `.bs2` | `a48065c6...`, `59e46f2c...` |

The first slot on each row is the maintainer's, from the pad, and the second the agent's, `F7`
then `F2` through `emulatorLauncher`, which works on all four without a modifier. Every state came
back at its own md5 under its own slot name, and the restore preview named each at the slot it was
made in.

**None of the four writes a screenshot.** `jgenesis` declares `{{romfilename}}_{{slot0}}.png` and
wrote none in either session, and the supplement declares no `<image>` for the other three because
none was ever seen. So step 5's screenshot half has nothing to carry on these rows, which the
restore preview says in `no screenshot: the server links none to this state`.

**ES's `-state_slot` decided nothing on any of the four**: launches carrying 6, 6, 3 and 6 wrote
slots 0, 1, 0 and 1. `jgenesis` mirrors like BizHawk, from `emulators/jgenesis/states/nes/` into
the declared directory, with a `.txt` sidecar; the other three write straight into their own tree
under `saves/nes/`, which the supplement names. `ares` states are a fixed 21,719 B, so only the md5
tells two apart. Finding 275.

### 8 and 9 on the four

`status` read each session back from the server, with the start above and the rom driven: 35
sessions after `jgenesis`, 36 after `mesen`, 39 after `mednafen`, whose count includes the first
Zelda session under it, and 40 after `ares`. The agent's own launches went through
`emulatorLauncher` without ES, so they ran no hooks and recorded no session. Every `start` and `quit` pass in `background.log` exited 0.

After each restore, `sync` answered `nothing to do: 227 games already present`, `0 downloaded, 0
written`, `all 1 unchanged`, with `gamelist.xml` md5'd either side and identical.

## The other eight rows

`nes` declares **nine** `(emulator, core)` rows, and the one above is one of them. All nine have
now been driven by hand on this install: a real player battery save and a save state in each,
launched from EmulationStation, with the emulator confirmed from `emulatorLauncher.log` rather
than from configuration.

**This did not certify them.** Steps 4 and 5 are driven for all nine and steps 1, 3, 7, 8 and 9
carry across from the row above. Six of the nine could not sync what they wrote. All eight have
since been certified on passes of their own, in the sections above, so what follows is the earlier
pass, kept as it was measured; where a sentence below says a row cannot sync something, the
sections above are what replaced it.

### How each row was selected

EmulationStation keeps a per-game emulator choice in **`gamelist.xml`**, as `<emulator>` and
`<core>` children of the `<game>` element, and not in `es_settings.cfg`. That was established by
setting one game through ES's own menu and diffing: `es_settings.cfg` came back byte-identical.

**Fourteen `nes["<rom>.zip"].emulator` keys written into `es_settings.cfg` were ignored.** They
survived ES's startup and exit rewrites untouched and were never read: five launches made under
them all ran the system-level `nes.emulator` / `nes.core` pair, `libretro` / `nestopia`. The
`<system>["<rom>"].<key>` chain is `emulatorlauncher`'s, and it covers feature keys; which emulator
runs is resolved by ES before `emulatorlauncher` exists. The skill now says so.

**Two rows declare no core and inherited `-core nestopia` from the system key.** `mesen` standalone
and `jgenesis` were both launched that way and both ignored it, running `Mesen.exe` and
`jgenesis-cli.exe`. For an emulator that declares no core, `-core` in the log is noise.

**`mesen` standalone is not the `mesen` libretro core**, and the two are separate rows that share a
name. They were driven on different games and wrote to different places, which is the clearest way
to see that they are not one thing.

### Where each row actually stores a save

Measured, not declared. Every battery save below was checked for content rather than existence:
the smallest is 499 non-zero bytes over 52 distinct values, where a file a core writes at boot is
uniform.

| Row                    | Battery save                               | Save state                                                    |
| ---------------------- | ------------------------------------------ | ------------------------------------------------------------- |
| `libretro`/`fceumm`    | `saves/nes/<rom>.srm`                      | `saves/nes/libretro.fceumm/<rom>.state1` + `.png`             |
| `libretro`/`mesen`     | `saves/nes/<rom>.srm`                      | `saves/nes/libretro.mesen/<rom>.state1` + `.png`              |
| `libretro`/`nestopia`  | `saves/nes/<rom>.srm`                      | `saves/nes/libretro.nestopia/<rom>.state1` + `.png`           |
| `mednafen`/`nes`       | `saves/nes/<rom>.<md5>.sav`                | `saves/nes/mednafen/sstates/<rom>.<md5>.mc0`                  |
| `mesen` standalone     | `saves/nes/<rom>.sav`                      | `saves/nes/mesen/SaveStates/<rom>_1.mss`                      |
| `ares`/`Famicom`       | `saves/nes/ares/Famicom/<rom>.ram`         | `saves/nes/ares/Famicom/<rom>.bs1`                            |
| `bizhawk`/`NesHawk`    | `saves/nes/bizhawk/<display name>.SaveRAM` | `saves/nes/bizhawk/sstates/NesHawk/<rom>.QuickSave0.State`    |
| `bizhawk`/`quickerNES` | `saves/nes/bizhawk/<display name>.SaveRAM` | `saves/nes/bizhawk/sstates/quickerNES/<rom>.QuickSave0.State` |
| `jgenesis`             | `saves/nes/jgenesis/nes/<rom>.sav`         | `saves/nes/jgenesis/states/<rom>_0.jst`                       |

`<md5>` is of the ROM: Final Fantasy came out as `24ae5edf8375162f91a6846d3202e3d6`, which is the
`.nes` less its 16-byte iNES header, and mednafen adds it only when `<rom>.sav` does not already
exist (findings 273 and 274).

**The screenshot half of that column was recorded for the three `libretro` rows only, and the gap
is not cosmetic here.** `es_savestates.cfg` declares an `<image>` for `bizhawk`
(`{{romfilename}}.QuickSave{{slot0}}.png`) and for `jgenesis` (`{{romfilename}}_{{slot0}}.png`), so
whether those five rows wrote one is a measurable fact this pass did not capture. The other three
declare no entry at all, so there is no `<image>` template to check them against and anything they
wrote would be in their own tree, unread for the same reason their states are. Which rows write one
decides how wide the remaining gap is, and it needs another hands-on pass. It is no longer _this
platform's_ open gap, because the three `libretro` rows closed it on 2026-09-20 and 2026-09-21; it
is what the other six rows owe. **Since measured on all six**: BizHawk writes the frame inside the
state and no `.png` (finding 268), and `jgenesis`, `mesen`, `mednafen` and `ares` write no image at
all (finding 275).

**So `nes` is class A on `libretro` and on nothing else.** `save_shapes.json` gives the system one
entry, `class A`, `provenance: observed`, evidence `loose .srm per rom, libretro`, and the evidence
string was already telling the truth: six of the nine rows write something else entirely, in four
different shapes. The file's own `_note` says shape is a property of `(system, emulator)`; this is
that note measured on one platform across every emulator it declares, and it is the first time the
claim has been checked rather than repeated.

**Only the three libretro rows share one battery file.** All three write
`saves/nes/<rom>.srm`, so switching core continues the same save: Kirby's Adventure was played
under `nestopia`, then under `fceumm`, and the second session carried on from the first and
rewrote the same 8,192 bytes. Across emulator families they are separate files in incompatible
formats, so the same game under `mesen` standalone and under `libretro` holds two unrelated saves
and neither sees the other. **`mesen` standalone and `mednafen` are the exception**, found later:
mednafen reads a plain `<rom>.sav` whenever one exists, so the two share it (finding 273).

### What RomMBat does with them

| Row                   | Battery save      | Save state          |
| --------------------- | ----------------- | ------------------- |
| the three `libretro`  | synced, class A   | synced, core-scoped |
| `bizhawk`, both cores | synced, certified | synced, certified   |
| `jgenesis`            | synced, certified | synced, certified   |
| `ares`                | synced, certified | synced, certified   |
| `mednafen`            | synced, certified | synced, certified   |
| `mesen` standalone    | synced, certified | synced, certified   |

**The table is as it stands now, and what follows is how it stood after the first pass**, when the
last four rows read "deferred", "no shape claims it" and "invisible". Each of those is now carried
by a battery rule in `save_rules.json`, and the three "invisible" states by the bundled
`es_savestates.supplement.xml`.

**"Synced" was upload only for `bizhawk` and `jgenesis`.** A restore could not place either
row's state, because both keep the slot in the stem and the restore read it from the extension.
The restore preview listed all three as "could not tell which slot it is". Fixed with finding 258,
and driven since on both `bizhawk` rows and on `jgenesis`, each restoring two slots to their own
names.

**Those two battery states are different things, and the distinction decides what to build next.**
`UnsyncableReason` separates them where the report's wording does not:

- **Deferred** is `NotInThisVersion`, defined as "The shape is understood and this build does not
  carry it. Stage 2's list." These are the emulators' own subdirectories under `saves/nes/`:
  `jgenesis/nes/`, and `bizhawk/` until #151. Understood, planned, not carried. `ares/Famicom/` is the same
  deferral and reports under `NoStateDeclaration` instead, because it holds a save state as well
  and that is the row that does not promise states sync.
- **No shape claims it** is `UnknownShape`. `mednafen` and `mesen` write their battery save **loose
  under `saves/nes/`**, beside the `.srm` files, which is structurally class A already. The only
  thing rejecting them is the extension: `save_rules.json` recognises `.bcr`, `.bkr`, `.brm` and
  `.srm`, and these two are `.sav`.

**The cheap-looking fix was a trap.** `save_rules.json` hard-wired `loose_emulator` to
`libretro`, so adding `.sav` to the extension list alone would have given mesen's
`Crystalis (USA).sav` the slot `libretro:battery` for that ROM and collided with libretro's own
`Crystalis (USA).srm`, which already syncs in that slot. This install holds both, because that game
was driven under `nestopia` and under `mesen` standalone. **#151 replaced the two globals** with a
`battery_saves` table of one rule per `(system, emulator)`, and loading refuses a table in which
two rules could claim one file, so `mesen` and `mednafen` each need a rule of their own and can no
longer be carried by accident. **Both have one since**, and the two share `.sav` at the loose
level only because mednafen's rule names a content hash on the stem, which the loader allows for
exactly one of a pair (finding 273).

The battery column is a **reported** limitation and the right behaviour for this release. Nothing
is dropped silently. The pass that drove these nine rows read one `saves` row covering all five
directories: "ares, bizhawk, jgenesis, mednafen, mesen hold shared containers or a shape no
declaration covers. This release syncs the battery saves loose under `saves/nes/` (class A), the
save states beside them, and the directory saves the shape definition names."

**That last clause was false for three of the five, and that was the defect.** `mednafen`, `mesen`
and `ares` declare no `es_savestates.cfg` entry, and `StateScanner` finds states only from that
file, so the states they wrote are not scanned, not uploaded and not restorable. They were
mentioned, and that is the part to be precise about: `CountFiles` excludes only declared state
directories, so these three undeclared ones were counted and named by the same `AddSubdirectories`
row. A person with a save state was not told there was none. They were told the directory held
something unsyncable, under a sentence promising that "the save states beside them" sync, with the
state files folded into a count whose reason is about battery saves and shared containers.

Issue #150 split that row in two, on whether `es_savestates.cfg` names the emulator at all. `nes`
reported `bizhawk, jgenesis` under the sentence above (`jgenesis` alone since #151 carried
BizHawk's battery save), and `ares, mednafen, mesen` under a
`no_state_declaration` row that repeats the shape half and ends "a save state written under these
is found only where RetroBat mirrors it into a declared path, and is otherwise not scanned, not
uploaded and not restorable". **Which is a correct message, not a fix.** The states are still
invisible, and making them syncable needs a bundled supplement carrying directory, filename and
slot per row. **That supplement now exists for `nes`**, `data/retrobat/es_savestates.supplement.xml`,
and all three rows' states sync and restore through it. The reach past this platform is unchanged: 30 of wave 1's 81 rows are in that
family, and `docs/platforms/README.md` had been reading "declares no directory" as "writes no
state".

The split is untested against a real install. It was driven against the `nes` tree this pass
measured, reproduced as a fixture, and the shipped `es_savestates.cfg`; nobody has re-run `saves`
on the machine that produced the states.

Two smaller findings from the same pass:

- **BizHawk names a battery save after the display name**, dropping the region tag:
  `StarTropics (USA).zip` produced `bizhawk/StarTropics.SaveRAM`. Its own state sidecar spells the
  convention out, `StarTropics.NesHawk`, so the join key exists. Issue #151, which now carries it:
  the title is joined through that sidecar or a BizHawk launch, and a title two ROMs answer to is
  refused rather than given to either. Driven on both cores on 2026-09-21; see "BizHawk battery
  saves, driven" below.
- **`saves` groups two unsyncable files under `nes/libretro`**, a directory that does not exist,
  and names no filenames. Issue #152.

### BizHawk battery saves, driven

Driven from EmulationStation on 2026-09-21 against the build carrying #151, with the maintainer
at the controller. **This is the one hands-on pass a save-logic change owes, not a certification
of either `bizhawk` row**: steps other than the battery save were not re-run. Findings 262 to 266.

| Pass                                               | What happened                                                                                                                                                                                                      |
| -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| The two saves already on disk, one per core        | Both attributed through their sidecars (`StarTropics.NesHawk`, and Ultima's under `quickerNES`), uploaded, and a second flush a no-op                                                                              |
| Restore, then play                                 | `StarTropics.SaveRAM` restored under its title with the original hash, and **Continue in BizHawk showed the saved progress**. BizHawk's rewrite on exit went up under the USA ROM                                  |
| Restore while another `nes` game runs              | Ultima's restore deferred with the display-name reason while StarTropics ran, and landed with its original hash once it quit                                                                                       |
| No learned title                                   | With the binding forgotten, the restore refused with "Run the game once under bizhawk" and wrote nothing                                                                                                           |
| No save state, `NesHawk`                           | Zelda wrote `Legend of Zelda, The.SaveRAM`; the launch route alone bound it and it went up                                                                                                                         |
| In-game save, `quickerNES`                         | Ultima's changed bytes went up under the same slot and ROM, then a no-op                                                                                                                                           |
| Europe copy of a game already saved in the USA one | **One file for both**: the Europe copy read the USA progress and wrote a new character into `StarTropics.SaveRAM`. Contested, nothing uploaded; `saves bind` settled it to USA and a restore put the USA save back |

Three defects surfaced here that the suite had not caught, and each now has a test: a slot this
device had already sent restored to the ROM's stem rather than the title (finding 266), a restored
file was credited to whichever BizHawk session came last and contested (265), and BizHawk's
`.SaveRAM.bak` was reported as an unknown shape (264).

**Since measured**: `NesHawk` and `quickerNES` read each other's `.SaveRAM`. Zelda's file,
registered under `NesHawk`, showed on the file-select screen under `quickerNES`, which wrote a
second name beside it (finding 271, in "`bizhawk`/`NesHawk` and `bizhawk`/`quickerNES`").

### The hooks carried the whole session

Seventeen launches across eight emulators, with nobody at a terminal, and every `start` and `quit`
pass in `background.log` exited 0. The states were already uploaded by the `quit` hook's detached
pass before a flush was run by hand, which is what the arrangement is for. One line of that log
lost its first eighteen characters, which is issue #153.

## Conflict resolution, driven both ways

Driven on the two `libretro` rows, which are the only ones whose battery saves sync at all. Both
branches of `saves resolve` were exercised, plus the negotiate-driven download that had never been
driven before.

**Both sides of every conflict here were synthesized, and that bounds the claim.** The server side
was uploaded by hand and the local side was byte-edited, so what this proves is RomMBat's handling
of a divergence, not that a real two-device race produces one. No second device exists on this
install.

### Staging one is harder than it looks, and the reason is a finding

**A save uploaded through RomM's web UI cannot conflict with anything.** `slot` is an optional
query parameter on `POST /api/saves` and the web UI does not set it, so the upload lands with
`slot: null`, negotiate keys on the slot, and this device's record for `libretro:battery` never
goes stale. Measured: a web upload at 12:39 left the next flush uploading cleanly with no 409.

It is visible from the restore side, with the local file moved aside so every server row became a
candidate:

```console
$ rommbat-agent saves restore 158593
  save   rom 158593  libretro:battery          8 KB  2026-09-13 11:32  saves/nes/Kirby's Adventure (USA) (Rev 1).srm
  save   rom 158593  libretro:battery          8 KB  2026-09-13 11:38  saves/nes/Kirby's Adventure (USA) (Rev 1).srm
  save   rom 158593  (no slot)                 8 KB  2026-09-13 12:39  saves/nes/Kirby's Adventure (USA) (Rev 1).srm
  save   rom 158593  libretro:battery          8 KB  2026-09-13 12:46  saves/nes/Kirby's Adventure (USA) (Rev 1).srm
```

That is #138 from a second direction: a null-slot save is not only never fetched by negotiate, it
also **cannot conflict**, so a client that does not speak RomMBat's slot convention can never
collide with one. It is also #156, since all four rows resolve to one destination and the offer
says nothing about that. **Since fixed by #156** in stage 2 of #195: the offer is one row per
destination, the newest, and it names the rows it folded.

Staging one needs `POST /api/saves?slot=libretro:battery`. Note `device_id` is validated: an
invented one is refused with `404 Device with ID ... not found`, so the upload was made without it.

### What the machinery does

|                          | `--keep-local`, rom 158593            | `--keep-server`, rom 159313         |
| ------------------------ | ------------------------------------- | ----------------------------------- |
| Detection                | `1 conflicted`, nothing overwritten   | `1 conflicted`, nothing overwritten |
| Copy aside before acting | yes                                   | yes                                 |
| Outcome on disk          | local bytes kept                      | the server's `f78ab191` written     |
| On the server            | sent as save 212, **210 still stood** | 213 untouched                       |
| Copy aside afterwards    | pruned                                | pruned                              |

The conflict report names both hashes, the time it was first seen and the copy-aside path before
asking for a decision, and neither branch is a default:

```text
  rom 158593, slot libretro:battery, since 2026-09-13 12:57:52Z
    here    saves/nes/Kirby's Adventure (USA) (Rev 1).srm  7fda7607
    server  6799e326  2026-09-13 12:56:42Z
    a copy of the local file is at emulators/rommbat/replaced/20260913T125752-Kirby's ... .srm
```

**So `overwrite` means supersede, and the resolver's remark is confirmed on a second shape.** It
was measured on a `psp` class C unit during 7b-3; this is class A on `nes` and behaves the same.

### The download path works, and it had never been driven

Leaving one local file untouched while the server moved produced `1 down (8 KB)`: a save from
elsewhere came down through negotiate rather than through `saves restore`. **It also copied the
local file aside before overwriting**, so the copy-aside rule holds on the download path and not
only on conflicts.

That is the mechanism #155 is about, now known to work when it is not racing a launch.

### Two defects, #157

**A class A download leaves `save_slot` naming the superseded save.** After pulling save 211 down,
the row still read `save_id 209` with the pre-download hash, while 211's content sat on disk. It
does not self-correct: the local file is then in step, so the slot is never negotiated again.
`--keep-local` writes the row correctly. Nothing visibly breaks, because the server-side sync
record **is** updated, so the damage is confined to the device's picture of the server and is
silent.

**It is class A's, not every restore's, and keep-server is a second instance rather than an
inheritance.** `SaveSync.RestoreUnitAsync` and `SaveConflictResolver.FinishUnitAsync`, the class C
halves, both call `SaveSlots.RecordRestored`. `SaveSync.RecordRestored` and
`SaveConflictResolver.KeepServerAsync`, the class A halves, both write `local_save` and stop.
`KeepServerAsync` does not call the download path, so it is broken separately and a fix to the
download alone would leave it broken. Only class A was driven here, which is what this pass can
speak to; the class C recording is read from the code and from the 7b-3 measurement it cites.

**Both class A writers record the slot now**, fixed with the RomM 5.3.0 stage 4 work, which needed
the recorded save id to recognise a superseded row returning to the head of a slot. What is above
is the behaviour this pass measured before the fix. Driven after it on this row, with
`Destiny of an Emperor (USA)`: keep-server left `save_slot` naming the save it took, and a plain
download moved it to the newer save. Recorded in finding 4 of
[romm-5.3-findings.md](../romm-5.3-findings.md); it is not a re-run of any certification step.

**A download's copy aside is never pruned.** Both resolutions removed theirs. The plain download's
copy is still there with no decision to attach to it and no mechanism that will remove it. Since
#211 a download that would replace a save this device never sent is a conflict instead, so such
a copy now always holds bytes the server already has.

### A save this device never sent, driven (#211)

On `libretro`/`nestopia` with StarTropics (USA), rom 159082, on `R:\RetroBat` (RetroBat 8.2.1,
RomM `5.3.0-beta.1`, 2026-09-21), running PR #214's build with the maintainer at the controller. It is
the hands-on pass a save-logic change owes and re-runs no certification step. **The local side is
real and the server side is staged**: the maintainer's two sessions wrote both saves through
EmulationStation, and the "other device" is a slotted upload with no `device_id`, which is the only
way to put a row in a slot this device has never synced (see "Staging one is harder than it looks"
above).

The hooks were off for the two sessions, because the quit hook's flush would otherwise have sent
the local save first, and case M3 needs a device with no sync record for the slot. The
game's BizHawk override was removed from `gamelist.xml` so it ran on `nes`' default core.

| Step                                                 | Result                                                                              |
| ---------------------------------------------------- | ----------------------------------------------------------------------------------- |
| New game `PEER`, quit                                | `.srm` md5 `514d2818`, copied aside as the other device's save                      |
| Second file `LOCAL`, quit                            | md5 `33dcb0d4`, 10:56:26 local; `saves`: attributed, not sent                       |
| The PEER-only save uploaded as the other device      | save 355, 14:58:02Z, origin null                                                    |
| `flush`                                              | **`1 conflicted`**. The file unchanged in bytes and mtime; a copy under `replaced/` |
| `flush` again                                        | The same one conflict, no rewrite, still one copy                                   |
| `saves resolve 159082 libretro:battery --keep-local` | Sent, copy pruned, file unchanged                                                   |
| `hooks install`, `flush`                             | Nothing up or down; `in step`                                                       |
| Launched through ES                                  | Both `PEER` and `LOCAL` on the file select                                          |

Before #211 the first flush took the download: `1 down`, the LOCAL file replaced by the PEER-only
save, and the only record of LOCAL a copy nothing pointed to.

**A download over a save that was sent still works.** With the slot in step, BizHawk's real
StarTropics save bytes (md5 `970db3b8`) were uploaded as the other device, save 358. The flush
answered `1 down` with no conflict, the file became those bytes, the next flush was a no-op, and
the game on `nestopia` showed BizHawk's files and neither `PEER` nor `LOCAL`. Each of nestopia's
rewrites on exit went up as `1 up` with no conflict, once through the quit hook's flush. Nothing
else on the install conflicted in any of the passes.

### A recorded mednafen path a plain save now shadows, driven (#215)

On Final Fantasy (USA), rom 158331, on `R:\RetroBat` (RetroBat 8.2.1, RomM `5.3.0-beta.1`,
2026-09-21), running PR #215's build after its review round. It re-runs no certification step.
The `mednafen:battery` slot was in step at `Final Fantasy (USA).24ae5edf....sav` (md5 `597b2790`,
save 369). The other device was a slotted upload with no `device_id`, and the plain file was a
byte copy of the hashed one, standing in for what mesen standalone writes (finding 273).

| Step                                                  | Result                                                                                                      |
| ----------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `flush`                                               | Nothing up or down                                                                                          |
| The same save, last byte flipped, as the other device | save 378, origin null                                                                                       |
| `Final Fantasy (USA).sav` added beside it, `flush`    | **`1 up, 1 failed`**: the plain file went up as `mesen:battery`, save 379; the download refused as shadowed |
| After                                                 | The hashed file unchanged in bytes and mtime; `save_slot` still save 369, md5 `597b2790`                    |
| Plain file removed, 378 and 379 deleted, `flush`      | Nothing up or down; both files `in step`                                                                    |

Before #215 the refusal ran only when the path was derived, and a slot with a local save took the
recorded path straight through: the unit test for this case received the download.

## RomM's browser player, driven at `5.3.0-alpha.3`

On `libretro`/`nestopia` with The Legend of Zelda (USA) (Rev 1), rom 158633, the maintainer in
RomM's v2 player and at EmulationStation. Finding 259 in `docs/retrobat-findings.md` has the
detail; it re-runs no checklist step.

| Session                                      | What reached RetroBat                                                                            |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| A, resuming this client's `libretro:battery` | `1 down`, raw 8,192 B at the `.srm` nestopia reads, **loaded with the browser's new file on it** |
| RetroBat launch after A                      | Nestopia rewrote the file once; identical rewrites after that re-upload on every flush (#206)    |
| B, a fresh game filed under `autosave`       | **Overwrote the played save with no conflict**, then went up into `libretro:battery`             |
| B again, on the #205 fix                     | **`1 conflicted`, the `.srm` untouched**; `--keep-local` then sent it into `autosave`            |

B is #205, and the fix records it as a conflict instead of writing, confirmed by driving B again on it. The played save was put back
from the copy aside and is the current `libretro:battery` version again.

**Row 2's re-upload does not reproduce at `5.3.0-beta.1` or `5.3.0`**, and the table is left as it
was measured on `5.3.0-alpha.3`. Probe case M4 answers `no_op (Content is identical)` there, so
the hash settles it server-side; finding 259 carries the re-check. #210 guards the client side
anyway, because the loop it would prevent is silent.

## What this file will not claim

- **All nine rows are certified, and only on this install at these two floors.** None of the
  rules or declarations the last four needed reached past `nes` when this was measured, so none
  of these emulators is certified anywhere else by this record, and each system they run on
  starts from nothing, as `megadrive` did.
- **Step 5's screenshot half was byte-checked where a screenshot exists**: as a `.png` for
  `libretro` and inside the state for `bizhawk`. `jgenesis`, `mesen`, `mednafen` and `ares` write
  none, so on those four the step is the state alone.
- **Six passes made one state per row outside EmulationStation**, the two `bizhawk` rows and the
  last four, through `emulatorLauncher` with the row's arguments, because the keyboard slot keys
  did not cross RDP. The state is the emulator's and any mirror `emulatorLauncher`'s either way, but
  the hooks did not run for those launches.
- **A plain `<rom>.sav` is shared by `mesen` and `mednafen`** and goes up as `mesen:battery` even
  when mednafen wrote it last. That is the file's owner by rule and not a claim about who played.
- The conflict results are about RomMBat's handling of a divergence. Both sides were synthesized,
  so nothing here is evidence that a real two-device race produces one, or how often.
- The class D download path is untested anywhere in the project and `nes` contains no class D.
- Media coverage, once step 7 records it, is a dated observation about this RomM library and
  moves when an administrator rescrapes. It is never a platform result.
