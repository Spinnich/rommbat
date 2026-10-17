---
summary: The test-game corpus, the games known to be the best test of something on each system, and how the agent tree pulls them from one RomM collection per system.
read-when: Before choosing games for a certification, a hands-on pass or any test on a real install, or when adding a game to the corpus.
---

# Test-game corpus

**A game is in the corpus because a record says what it is the best test of**: a save medium, a
multi-disc layout, a coprocessor, a clock, a firmware boot. A game used once, or picked without
knowing its fit, stays out until a pass shows it is the best test of something, and then that pass
adds it here with the reason. The corpus is not exhaustive. A system with no table has no game
admitted yet, and its first pass picks by
[Choosing test games](../../.claude/skills/platform-certification/SKILL.md#choosing-test-games)
and records the result here.

## How it is pulled

Each system's games are one RomM collection on the test server, `RomMBat corpus: <system>`, owned
by the approver account, which is the account the agent tree pairs as. `tools/corpus.py` keeps the
collections matching the tables below, writing with `ROMMBAT_TEST_CORPUS_TOKEN`
([local secrets](../contributing/local-secrets.md)), and the agent tree syncs one as an ordinary
collection set:

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
pass that uses it confirms it still covers its row.

**A game the corpus wants in a layout the library lacks is the maintainer's to build in RomM**: a
multi-disc set regrouped as one rom, or a `.bin`/`.cue` copy beside a `.chd`. It is listed under
Wanted with the layout named, and moves into the table once it is on the server. The tables hold
names, sizes and hashes only, never game content.

## nes

| Game                                     | Platform | Size   | md5                                | Covers                                                |
| ---------------------------------------- | -------- | ------ | ---------------------------------- | ----------------------------------------------------- |
| `Legend of Zelda, The (USA) (Rev 1).zip` | `nes`    | 65,652 | `614fb3085826e62f3be3a3fe0b931689` | Battery SRAM, written as soon as a name is registered |

## snes

| Game                                                                            | Platform          | Size      | md5                                | Covers                                              |
| ------------------------------------------------------------------------------- | ----------------- | --------- | ---------------------------------- | --------------------------------------------------- |
| `Legend of Zelda, The - A Link to the Past (USA).zip`                           | `snes`            | 702,836   | `608c22b8ff930c62dc2de54bcd6eba72` | Battery SRAM, written when a name is registered     |
| `Super Mario Kart (USA).zip`                                                    | `snes`            | 351,251   | `7f25ce5a283d902694c52fb1152fa61a` | DSP-1, and the `dsp1b.rom` firmware boot (RB-317)   |
| `Kirby's Dream Land 3 (USA).zip`                                                | `snes`            | 2,236,436 | `201e7658f6194458a3869dde36bf8ec2` | SA-1, its BW-RAM as the save                        |
| `Super Mario World 2 - Yoshi's Island (USA) (Rev 1).zip`                        | `snes`            | 1,286,562 | `ce1e3e33b6e39d37b43d7de599f9e785` | Super FX, its RAM declared in the expansion byte    |
| `New Horizons (USA).zip`                                                        | `snes`            | 1,293,056 | `6fe8db7c73bb14f322ca6045c2fde46a` | HiROM with 32 KB of SRAM                            |
| `Super Shell Monsters Story II (Japan) [T-En by Dynamic Designs v0.90] [n].zip` | `snes-unofficial` | 3,360,018 | `fe6c7d5083495dc199d247ede7eb4a05` | S-RTC clock, a second file beside the save (RB-417) |

What each chip wrote on every row is in [the snes record](snes/index.md#the-edge-case-pass).

## gb

| Game                                                                                      | Platform | Size    | md5                                | Covers                                                    |
| ----------------------------------------------------------------------------------------- | -------- | ------- | ---------------------------------- | --------------------------------------------------------- |
| `Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe) (CGB+SGB Enhanced).zip` | `gb`     | 509,457 | `d9290db87b1f0a23b89f99ee4469e34b` | MBC5 with 32 KB of battery RAM, saved from the first room |
| `Final Fantasy Adventure (USA).zip`                                                       | `gb`     | 159,533 | `24cd3bdf490ef2e1aa6a8af380eccd78` | MBC2, its RAM inside the mapper                           |
| `Pokemon - Silver Version (USA, Europe) (SGB Enhanced) (GB Compatible).zip`               | `gbc`    | 748,101 | `2ac166169354e84d0e2d7cf4cb40b312` | MBC3 with a clock, the second file on `gb`'s rows         |

Silver's clock files per row are in
[the gb record](gb/index.md#a-cartridge-with-a-clock-pokemon-silver-on-gb).

## gbc

| Game                                                  | Platform | Size      | md5                                | Covers                             |
| ----------------------------------------------------- | -------- | --------- | ---------------------------------- | ---------------------------------- |
| `Pokemon - Crystal Version (USA, Europe) (Rev 1).zip` | `gbc`    | 1,029,939 | `301899b8087289a6436b0a241fbbb474` | MBC3 with a clock and 32 KB of RAM |

## gba

| Game                                          | Platform | Size      | md5                                | Covers                                      |
| --------------------------------------------- | -------- | --------- | ---------------------------------- | ------------------------------------------- |
| `Pokemon - Emerald Version (USA, Europe).zip` | `gba`    | 6,976,143 | `605b89b67018abcea91e693a4dd25be3` | 128 KB flash and an RTC, and the clock file |

Wanted: one game each for SRAM, EEPROM and 64 KB flash, the media Emerald does not cover.

## megadrive

| Game                                                                      | Platform    | Size      | md5                                | Covers                                                            |
| ------------------------------------------------------------------------- | ----------- | --------- | ---------------------------------- | ----------------------------------------------------------------- |
| `Sonic & Knuckles + Sonic The Hedgehog 3 (USA) (Lock-on Combination).zip` | `megadrive` | 2,540,313 | `c5b1c655c19f462ade0ac4e17a844d10` | Battery RAM written when a data-select slot is chosen, in seconds |

## mastersystem

No game is admitted. Golden Axe Warrior, which certified the system, commits its save once and then
rewrites a working copy (RB-326), so it cannot show that a later session saved.

Wanted: a battery game that commits every save, at a point reached within minutes.

## gamegear

| Game                                   | Platform   | Size    | md5                                | Covers                                      |
| -------------------------------------- | ---------- | ------- | ---------------------------------- | ------------------------------------------- |
| `Defenders of Oasis (USA, Europe).zip` | `gamegear` | 357,451 | `8430050c60db46b3887cf7d7cf2f206f` | Battery SRAM that autosaves as it is played |

## sega32x

| Game                                       | Platform  | Size      | md5                                | Covers                                    |
| ------------------------------------------ | --------- | --------- | ---------------------------------- | ----------------------------------------- |
| `Knuckles' Chaotix (Japan, USA) (En).zip`  | `sega32x` | 1,702,700 | `47b1095e68b053125cd2cd5b1ac4eb50` | SRAM, 1 KB of odd bytes                   |
| `NBA Jam - Tournament Edition (World).zip` | `sega32x` | 2,287,364 | `966a5e00dd7bcdc70b05c5a0c0e8f7aa` | Acclaim's serial EEPROM, 256 B            |
| `Doom (Japan, USA) (En).zip`               | `sega32x` | 1,490,753 | `79339867d9d4f58b169753d9a29ea1a5` | No battery, so what a row writes for none |

Where each one saves is in [the sega32x record](sega32x/index.md).

## n64

| Game                                               | Platform | Size       | md5                                | Covers                                          |
| -------------------------------------------------- | -------- | ---------- | ---------------------------------- | ----------------------------------------------- |
| `Legend of Zelda, The - Ocarina of Time (USA).zip` | `n64`    | 26,564,897 | `5bd1fe107bf8106b2ab6650abecd54d6` | 32 KB of cartridge SRAM                         |
| `Mario Kart 64 (USA).zip`                          | `n64`    | 8,991,046  | `3a67d9986f54eb282924fca4cd5f6dff` | 512 B EEPROM, and a ghost on the Controller Pak |

## psx

| Game                             | Platform | Size          | md5                                | Covers                                                                          |
| -------------------------------- | -------- | ------------- | ---------------------------------- | ------------------------------------------------------------------------------- |
| `Metal Gear Solid (USA) (Rev 1)` | `psx`    | 779,304,107   | `2cb39eccdb821396ba1e665ac9d1c1ef` | A two-disc set as one rom, two `.chd` and an `.m3u`                             |
| `Metal Gear Solid (USA)`         | `psx`    | 1,437,526,201 | `2f22a92282d9505c417d8cd7987a27f3` | The same set as `.bin`/`.cue` pairs, for the three rows that cannot open `.chd` |

Both save late, through Mei Ling after the opening, so a pass seeds every later row from the first
row's card. That is the price of the layout, which no faster-saving set in the library covers.

Wanted: a second game that saves within minutes, to share a memory card with the first for step 6.
Castlevania: Symphony of the Night did that on the first pass and saves only at the first save room.

## ps2

| Game                                     | Platform | Size          | md5                                | Covers                                                           |
| ---------------------------------------- | -------- | ------------- | ---------------------------------- | ---------------------------------------------------------------- |
| `Burnout 3 - Takedown (USA, Canada).chd` | `ps2`    | 1,775,520,038 | `398eb5af77f65ec3af65ff603dd83acb` | Formats and saves its own card, a profile under one serial       |
| `Devil May Cry 2 (USA)`                  | `ps2`    | 2,114,516,532 | `51936d6f755e5ec68e8994ee2daf67af` | A two-disc set as one rom, saving one unit under disc 1's serial |

What each wrote on the card is in [the ps2 facts](ps2/facts.md).
