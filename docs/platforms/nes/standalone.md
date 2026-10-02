---
summary: The certification record for `nes`: `jgenesis`, `mesen`, `mednafen` and `ares`.
read-when: When a result for one of these `nes` rows is needed, or before re-driving one.
---

# nes: `jgenesis`, `mesen`, `mednafen` and `ares`

**All four certified on 2026-09-21**, at RomM `5.3.0-beta.1` and RetroBat 8.2.1, on the same
install and server as [the re-drive](index.md#the-install-this-was-measured-on). Until this pass none of them could be: `jgenesis` had no battery
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
states in all, from [the first pass](first-pass.md). After the second, the store held no unsyncable
row for `nes` at all.

## Checklist for the four

Steps 1, 2 and 3 are the system's and carry from `nestopia`'s re-drive, and step 6 is N/A for the
same reason. `.zip` was observed to launch on all four.

| #   | `jgenesis`                          | `mesen` standalone                  | `mednafen`                                 | `ares`                              |
| --- | ----------------------------------- | ----------------------------------- | ------------------------------------------ | ----------------------------------- |
| 4   | **Pass, both directions**           | **Pass, both directions**           | **Pass, both directions**, the hashed name | **Pass, both directions**           |
| 5   | **Pass**, two slots, no image       | **Pass**, two slots, no image       | **Pass**, two slots, no image              | **Pass**, two slots, no image       |
| 7   | **Pass.** Box art on screen         | **Pass.** Box art on screen         | **Pass.** Box art on screen                | **Pass.** Box art on screen         |
| 8   | **Pass.** 18:02:04Z, 2m 31s         | **Pass.** 18:25:40Z, 42s            | **Pass.** 18:33:45Z, 28s                   | **Pass.** 18:36:46Z, 1m 0s          |
| 9   | **Pass.** No-op, gamelist unchanged | **Pass.** No-op, gamelist unchanged | **Pass.** No-op, gamelist unchanged        | **Pass.** No-op, gamelist unchanged |

## 4. Battery save on the four

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
held the file computes it from the ROM there. RB-274.

**mednafen puts the hash on only when the name without it is free.** Its own documentation says
`%M` is "empty for first evaluation per full path construction", and the first Zelda session showed
what that means: `Legend of Zelda, The (USA) (Rev 1).sav`, which `mesen` had written minutes
earlier, loaded in mednafen with the name on it, and the name registered there went into the next
file of the same `.sav`. So a plain `<rom>.sav` is one save that `mesen` and `mednafen` both read and
write, uploaded as `mesen:battery` whichever wrote it last, and mednafen writes the hashed name only
for a game with no plain one, which is why the row was driven on Zelda II. A restore refuses to
write a `mednafen:battery` save where a plain `<rom>.sav` would shadow it, rather than leave a file
mednafen never opens. RB-273.

## 5. States on the four

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
tells two apart. RB-275.

## 8 and 9 on the four

`status` read each session back from the server, with its start time and the rom driven: 35
sessions after `jgenesis`, 36 after `mesen`, 39 after `mednafen`, whose count includes the first
Zelda session under it, and 40 after `ares`. The agent's own launches went through
`emulatorLauncher` without ES, so they ran no hooks and recorded no session. Every `start` and `quit` pass in `background.log` exited 0.

After each restore, `sync` answered `nothing to do: 227 games already present`, `0 downloaded, 0
written`, `all 1 unchanged`, with `gamelist.xml` md5'd either side and identical.
