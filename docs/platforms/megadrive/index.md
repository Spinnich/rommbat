---
summary: The certification record for `megadrive`: each row's standing at the floor, and the system's own steps.
read-when: Before certifying a `megadrive` row, or when asked whether a `megadrive` row works.
---

# megadrive

Sega Mega Drive / Genesis. RetroBat calls the folder `megadrive`, which is what this file is
named after.

**Seven of the eleven rows `megadrive` declares are certified**, at RomM `5.3.0` and RetroBat
8.2.1, on 2026-09-21, all nine steps with step 6 N/A because `megadrive` has no class D:

- `libretro`/`genesis_plus_gx`, **the row a stock install gives a user**, selected with no override
- `libretro`/`genesis_plus_gx_wide` and `libretro`/`picodrive`
- `bizhawk`/`Genplus-gx`, `jgenesis`, `mednafen`/`megadrive` and `ares`/`MegaDrive`

**Four are driven and not certified**, and each says why in its own section:

- `libretro`/`fbneo` **never boots this library.** FBNeo finds a Mega Drive game by its own set
  name, taken from the file name, and every ROM here carries its No-Intro name (RB-278).
- `kega-fusion`/`auto`, `kega-fusion`/`genesis` and `kega-fusion`/`megadrive` **fail step 4**:
  Kega Fusion writes its battery saves into `emulators/kega-fusion/`, outside `saves/`, because
  RetroBat's template `Fusion.ini` sends them there and `emulatorLauncher` never redirects them
  (RB-283). Their states sync, and the pad works only once remapped in Kega's own menu
  (RB-284).

**It certifies those seven rows and nothing wider.** The last four needed code first, as on
`nes`: a battery rule each and, for `mednafen` and `ares`, a state declaration in RomMBat's
bundled supplement. Every one of those is scoped to `nes` and `megadrive`, the two systems they
were measured on.

## Where each row stands

**Every certified row holds at the floor, RomM `5.3.1` and RetroBat 8.2.1.** Steps 1 and 9 were
re-driven at `5.3.1` on 2026-09-24. The other steps carry from the drive at `5.3.0`, since nothing
they exercise changed between the two. Nothing is owed.

| File                             | What it holds                                                                                                                           |
| -------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| This file                        | Steps 1, 2 and 3, which are the system's; what the pass turned up that is not a row, including the fix for another client's `null` save |
| [libretro.md](libretro.md)       | The four `libretro` rows                                                                                                                |
| [standalone.md](standalone.md)   | The four rows that needed code: `bizhawk`, `jgenesis`, `mednafen` and `ares`                                                            |
| [kega-fusion.md](kega-fusion.md) | The three `kega-fusion` rows                                                                                                            |
| [facts.md](facts.md)             | The measured facts about `megadrive`'s emulators, with RB- IDs                                                                          |

## The install this was measured on

|           |                                                                       |
| --------- | --------------------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                             |
| RomM      | `5.3.0`, the floor then, read back by `status` as Supported           |
| Root      | `R:\RetroBat`, found by walking up from the executable                |
| Store     | schema 16 of 16, WAL                                                  |
| Client    | a deploy of the `certify-megadrive` branch by `tools/publish.ps1`     |
| Budget    | `none`, as for `nes`, so a missing cover at step 7 cannot be headroom |
| Test game | Sonic & Knuckles + Sonic The Hedgehog 3 (USA) (Lock-on Combination)   |

**The client was deployed three times, and which build a result was taken on is named.** The
first carried the `null` save refusal and the `--help` fix, and drove the four `libretro` rows.
The second added the megadrive battery rules and state declarations, and is the one the four
rows needing code were certified on. The third added the `kega-fusion` state declaration.

**The test game was picked for its save.** Sonic 3 & Knuckles writes battery RAM the moment a
data-select slot is chosen, so each session made a new save in seconds, and a slot the previous
row used shows on screen as used, which tells whether two emulators read one file. It carries
no `<emulator>` pin in `gamelist.xml`, and no game in the megadrive list does.

**The maintainer played over RDP, and the agent drove the second state on each non-`libretro`
row** from its session on the RetroBat machine: `emulatorLauncher` started with the row's
`-system`, `-emulator`, `-core` and `-rom`, and the keys sent by `keybd_event` with hardware scan
codes. Those launches skip ES, so they run no hooks and record no session.

## The set

|          |                                             |
| -------- | ------------------------------------------- |
| Name     | Spinnich's Sega Genesis Favorites           |
| Scope    | `smart_collection 7`                        |
| Policy   | no game cap, no size cap, ordered by recent |
| Resolves | 252 games, 199.6 MB, into `megadrive`       |

## Steps 1, 2 and 3, for every row

Staged at `5.3.0` before the sessions and re-confirmed on the day. None needs an emulator, so all
eleven rows carry them.

### 1. Mapping

Resolved at layer **`fs_slug`**: RomM's `fs_slug` for the platform is literally `megadrive`,
which is already a folder in this install. As on `nes`, that is a property of this instance.

| `fs_slug`              | Resolved by | What `platforms list` says                                                  |
| ---------------------- | ----------- | --------------------------------------------------------------------------- |
| `megadrive`            | `fs_slug`   | RomM's fs_slug 'megadrive' is already a folder in this install              |
| `megadrive-unofficial` | `bundled`   | the bundled table offers `megadrive`, `megadrive-msu`, and picked the first |

### 2. Extensions

From the live `es_systems.cfg`:

```text
.68k .sgd .smd .bin .gen .md .sg .wad .zip .7z
```

**252 of 252 resolve and nothing is excluded.** Every ROM is a `.zip`, 252 of them holding a
`.md` and one of those a `.bin` beside it. `.zip` was observed to launch on every row except
`fbneo`, which refuses it for its name rather than its extension. The rest of the list is the
system's union and unproven per row.

### 3. BIOS

```console
$ rommbat-agent bios megadrive
RetroBat requires no BIOS for megadrive.
$ echo $?
0
```

A real system with nothing to fetch, which counts as step 3 passing.

### 6. Per-game memory card

**N/A on every row.** `megadrive` is class A in `save_shapes.json`, and no row wrote anything
that is not one game's own save.

## What the pass turned up that is not a row

### Another client's `null` save, refused (RB-276)

**RomMBat downloaded four bytes reading `null` and wrote them as a battery save.** RomM's browser
player uploads the JSON literal when it has no save to send, and the server keeps it as a save.
This install held one at `saves/megadrive/Bare Knuckle III (Japan) [T-En by Twilight Translations
v1.0].srm`, md5 `37a6259cc0c1dae299a7866489dff0bd`, written at 18:10 local by the build before this
branch; earlier ones had landed on `nes` as Ninja Gaiden II and Super Mario Bros. It was moved out of
the tree to `R:\rommbat-evidence\megadrive\` rather than deleted.

**The fix refuses such a save wherever a download is placed**: before the transfer when the server
names that hash, and on the bytes when it does not. That includes "keep server" on a conflict, and
an offer that would have become one, such as `null` in `autosave` landing on the `.srm` that
`libretro:battery` holds; the review of this PR found those two routes. It is never acknowledged, it is counted as
`refused, not a save` rather than failed, and it does not move the exit code, because the server
offers it again on every flush and nothing on the device can change that. The restore preview
lists it among the rows it cannot place, with the reason.

**Both paths were driven on the live server.** `saves restore 173367`, Old Towers, lists its
slotless row as the four bytes `null`. Once the Bare Knuckle file was out of the tree, a flush
was offered rom 189465's `libretro:battery` row and answered `1 refused, not a save`, exit 0.
Rom 189465 holds **three** such rows: slotless, `autosave` (save 94, dated 2026-08-01, which the
old build negotiated at 22:10:14Z) and `libretro:battery`. This device's store holds no record of
uploading the third, so another client wrote it too, and which one is not determined here. All
four rows are left on the server until the PR merges.

**The conflict it left on this install had no exit until a later fix.** Rom 189465's
`libretro:battery` conflict outlived its local file, which went to the evidence folder; keep-local
then had nothing to send and keep-server refused the `null`, so it was reported on every flush. A
conflict with no save on either side now closes with nothing written, whichever answer is given.

### `--help` ran the command

`rommbat-agent saves restore --help` ran a full restore preview. Any `--help` or `-h` now prints
the usage and runs nothing, on every subcommand, which matters most where `--apply` is on the
same line.

## What this file will not claim

- **Nothing about `megadrive` under any build but this one.** Every row was measured on RetroBat
  8.2.1 and RomM `5.3.0`.
- **Nothing about another game.** Sonic 3 & Knuckles is one lock-on cartridge; the sizes above are
  its SRAM window's, and another game's differ.
- **Nothing about `mednafen` on a `.bin`, `.gen` or `.smd`.** `MednafenRomHash` answers only for a
  plain `.md`, the one format this library holds, and answers null for the rest.
- **Nothing about FBNeo on a library named for its dat.** The rename test booted one game and
  drove nothing past the title.
