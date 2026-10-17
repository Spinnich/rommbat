---
summary: The test-game corpus, the games known to be the best test of something on each system, and how the agent tree pulls them from one private RomM collection per system.
read-when: Before choosing games for a certification, a hands-on pass or any test on a real install, or when adding a game to the corpus.
---

# Test-game corpus

**A game is in the corpus because a record says what it is the best test of**: a save medium, a
coprocessor, a clock, a firmware boot, a save behavior. A game used once, or picked without knowing
its fit, stays out until a pass shows it is the best test of something, and then that pass adds it
here with the reason. The corpus is not exhaustive. A system with no table has no game admitted yet,
and its first pass picks by
[Choosing test games](../../.claude/skills/platform-certification/SKILL.md#choosing-test-games)
and records the result here.

**Save logic has to be testable quickly, so the First save column is part of the rule.** A game
that first saves within a minute or two is preferred, and a slower one is admitted only for a case
no quick game covers, with its cost stated. "Not timed" means no record measured it, and the next
pass that plays the game owes the number.

**A file layout is never a reason to admit a game.** The maintainer restructures a multi-disc or
multi-file game in RomM, or converts its files (`.iso` for `.chd`, `.bin`/`.cue` for either), once
a game is worth it. So the corpus admits the best-saving game that has the shape, and Wanted names
the layouts to build.

## How it is pulled

Each system's games are one private RomM collection on the test server, `RomMBat corpus: <system>`,
owned by the test account the agent tree pairs as. They are private because the maintainer's own
account has no use for them. `tools/corpus.py` keeps the collections matching the tables below,
writing with `ROMMBAT_TEST_DEV_TOKEN` ([local secrets](../contributing/local-secrets.md)), and the
agent tree syncs one as an ordinary collection set:

```powershell
python3 tools/corpus.py snes            # each game resolved, the collection compared, the set's line
python3 tools/corpus.py snes --apply    # create the collection, add and remove members to match
Invoke-Agent sets add corpus-snes --scope collection --value <id> --folder snes
```

`--folder` lands every member in the system's folder, which matters where a game is filed under
another RomM platform, as Pokemon Silver is for `gb`. Rom ids differ per server, so the tables key
each game by its file name and RomM platform and record its size and md5, and the tool resolves the
id. It reports a game it cannot find as missing and one whose size or md5 has moved as changed;
neither blocks the rest of the system. A changed game owes this page its new size and md5, once the
pass that uses it confirms it still covers its row. A game on Wanted moves into its table once it is
on the server. The tables hold names, sizes and hashes only, never game content.

## nes

| Game                                     | Platform | Size   | md5                                | Covers                    | First save                |
| ---------------------------------------- | -------- | ------ | ---------------------------------- | ------------------------- | ------------------------- |
| `Legend of Zelda, The (USA) (Rev 1).zip` | `nes`    | 65,652 | `614fb3085826e62f3be3a3fe0b931689` | Battery SRAM, 8 KB `.srm` | When a name is registered |

## snes

| Game                                                                            | Platform          | Size      | md5                                | Covers                                              | First save                |
| ------------------------------------------------------------------------------- | ----------------- | --------- | ---------------------------------- | --------------------------------------------------- | ------------------------- |
| `Legend of Zelda, The - A Link to the Past (USA).zip`                           | `snes`            | 702,836   | `608c22b8ff930c62dc2de54bcd6eba72` | Battery SRAM                                        | When a name is registered |
| `Super Mario Kart (USA).zip`                                                    | `snes`            | 351,251   | `7f25ce5a283d902694c52fb1152fa61a` | DSP-1, and the `dsp1b.rom` firmware boot (RB-317)   | Not timed                 |
| `Kirby's Dream Land 3 (USA).zip`                                                | `snes`            | 2,236,436 | `201e7658f6194458a3869dde36bf8ec2` | SA-1, its BW-RAM as the save                        | Not timed                 |
| `Super Mario World 2 - Yoshi's Island (USA) (Rev 1).zip`                        | `snes`            | 1,286,562 | `ce1e3e33b6e39d37b43d7de599f9e785` | Super FX, its RAM declared in the expansion byte    | Not timed                 |
| `New Horizons (USA).zip`                                                        | `snes`            | 1,293,056 | `6fe8db7c73bb14f322ca6045c2fde46a` | HiROM with 32 KB of SRAM                            | Not timed                 |
| `Super Shell Monsters Story II (Japan) [T-En by Dynamic Designs v0.90] [n].zip` | `snes-unofficial` | 3,360,018 | `fe6c7d5083495dc199d247ede7eb4a05` | S-RTC clock, a second file beside the save (RB-417) | Not timed                 |

The four chip games were booted and not played, so what each wrote is measured and a save through
play is not. What each chip wrote on every row is in
[the snes record](snes/index.md#the-edge-case-pass).

## gb

| Game                                                                                      | Platform | Size    | md5                                | Covers                                     | First save                    |
| ----------------------------------------------------------------------------------------- | -------- | ------- | ---------------------------------- | ------------------------------------------ | ----------------------------- |
| `Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe) (CGB+SGB Enhanced).zip` | `gb`     | 509,457 | `d9290db87b1f0a23b89f99ee4469e34b` | MBC5 with 32 KB of battery RAM             | Menu, Save, in the first room |
| `Final Fantasy Adventure (USA).zip`                                                       | `gb`     | 159,533 | `24cd3bdf490ef2e1aa6a8af380eccd78` | MBC2, its RAM inside the mapper            | After the first fight         |
| `Pokemon - Silver Version (USA, Europe) (SGB Enhanced) (GB Compatible).zip`               | `gbc`    | 748,101 | `2ac166169354e84d0e2d7cf4cb40b312` | MBC3 with a clock, the second file on `gb` | Start, Save, from the bedroom |

Silver's clock files per row are in
[the gb record](gb/index.md#a-cartridge-with-a-clock-pokemon-silver-on-gb).

## gbc

| Game                                                  | Platform | Size      | md5                                | Covers                             | First save                                  |
| ----------------------------------------------------- | -------- | --------- | ---------------------------------- | ---------------------------------- | ------------------------------------------- |
| `Pokemon - Crystal Version (USA, Europe) (Rev 1).zip` | `gbc`    | 1,029,939 | `301899b8087289a6436b0a241fbbb474` | MBC3 with a clock and 32 KB of RAM | Played from the intro, clock set. Not timed |

## gba

| Game                                          | Platform | Size      | md5                                | Covers                   | First save                        |
| --------------------------------------------- | -------- | --------- | ---------------------------------- | ------------------------ | --------------------------------- |
| `Pokemon - Emerald Version (USA, Europe).zip` | `gba`    | 6,976,143 | `605b89b67018abcea91e693a4dd25be3` | 128 KB flash and a clock | Not timed; later rows were seeded |

Wanted: one game each for SRAM, EEPROM and 64 KB flash, the media Emerald does not cover.

## megadrive

| Game                                                                      | Platform    | Size      | md5                                | Covers               | First save                     |
| ------------------------------------------------------------------------- | ----------- | --------- | ---------------------------------- | -------------------- | ------------------------------ |
| `Sonic & Knuckles + Sonic The Hedgehog 3 (USA) (Lock-on Combination).zip` | `megadrive` | 2,540,313 | `c5b1c655c19f462ade0ac4e17a844d10` | Battery RAM, 3 slots | In seconds, on choosing a slot |

## mastersystem

No game is admitted. Golden Axe Warrior, which certified the system, commits its save once and then
rewrites only a working area (RB-326), so it cannot show that a later session saved.

Wanted: a battery game that commits every save, within a minute or two of boot.

## gamegear

| Game                                   | Platform   | Size    | md5                                | Covers                 | First save                        |
| -------------------------------------- | ---------- | ------- | ---------------------------------- | ---------------------- | --------------------------------- |
| `Defenders of Oasis (USA, Europe).zip` | `gamegear` | 357,451 | `8430050c60db46b3887cf7d7cf2f206f` | Battery SRAM, autosave | Within seconds of play, autosaved |

## sega32x

| Game                                       | Platform  | Size      | md5                                | Covers                                    | First save                                  |
| ------------------------------------------ | --------- | --------- | ---------------------------------- | ----------------------------------------- | ------------------------------------------- |
| `Knuckles' Chaotix (Japan, USA) (En).zip`  | `sega32x` | 1,702,700 | `47b1095e68b053125cd2cd5b1ac4eb50` | SRAM, 1 KB of odd bytes                   | Deleting a slot at the data select, at once |
| `NBA Jam - Tournament Edition (World).zip` | `sega32x` | 2,287,364 | `966a5e00dd7bcdc70b05c5a0c0e8f7aa` | Acclaim's serial EEPROM, 256 B            | Only when it formats a blank one, at boot   |
| `Doom (Japan, USA) (En).zip`               | `sega32x` | 1,490,753 | `79339867d9d4f58b169753d9a29ea1a5` | No battery, so what a row writes for none | None                                        |

Where each one saves is in [the sega32x record](sega32x/index.md).

## n64

| Game                                               | Platform | Size       | md5                                | Covers                                          | First save              |
| -------------------------------------------------- | -------- | ---------- | ---------------------------------- | ----------------------------------------------- | ----------------------- |
| `Legend of Zelda, The - Ocarina of Time (USA).zip` | `n64`    | 26,564,897 | `5bd1fe107bf8106b2ab6650abecd54d6` | 32 KB of cartridge SRAM                         | Not timed               |
| `Mario Kart 64 (USA).zip`                          | `n64`    | 8,991,046  | `3a67d9986f54eb282924fca4cd5f6dff` | 512 B EEPROM, and a ghost on the Controller Pak | A lap writes the EEPROM |

## psx

No game is admitted. Metal Gear Solid, the only multi-disc set the first pass used, saves only
through Mei Ling after the opening, and Castlevania: Symphony of the Night first saves at its first
save room, well into the castle. Neither is a quick test, and the layouts they showed are the
maintainer's to build for whichever game is.

Wanted: a game that saves within a minute or two of boot, to test a memory card. A two-disc game
that does, as `.chd` with an `.m3u`, as `.bin`/`.cue` with an `.m3u` and as `.iso`, for the rows
that cannot open `.chd`. A second quick-saving game for a shared card.

## ps2

| Game                                     | Platform | Size          | md5                                | Covers                                                         | First save                   |
| ---------------------------------------- | -------- | ------------- | ---------------------------------- | -------------------------------------------------------------- | ---------------------------- |
| `Burnout 3 - Takedown (USA, Canada).chd` | `ps2`    | 1,775,520,038 | `398eb5af77f65ec3af65ff603dd83acb` | Formats and saves its own card, a profile under one serial     | A profile. Not timed         |
| `Devil May Cry 2 (USA)`                  | `ps2`    | 2,114,516,532 | `51936d6f755e5ec68e8994ee2daf67af` | A two-disc set as one rom, one save unit under disc 1's serial | Slow, and the pass played it |

Devil May Cry 2 is the only two-disc `ps2` set a pass measured, and it is in for that, not for its
speed. Wanted: a two-disc `ps2` game that saves within a minute or two of boot.

What each wrote on the card is in [the ps2 facts](ps2/facts.md).
