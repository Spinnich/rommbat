---
summary: How EmulationStation reads, prunes and rewrites `es_settings.cfg`, and the per-game override form.
read-when: Before writing `es_settings.cfg`, a per-game override, or anything that must survive an ES session.
---

# RetroBat: es_settings.cfg

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-31. Confirmed live

Plan says: `<system>["<rom>"].<key>` is a per-game override, granularity unverified (L1106-1112)

Measurement says: **Confirmed live.** It is honoured by `emulatorlauncher`, outranks the system key, and stays scoped to the one rom

## RB-32. The rom key is written as `<rom filename>`

Plan says: (not addressed) the rom key is written as `<rom filename>`

Measurement says: The filename must carry its **extension**. `ports["gong"]` is ignored where `ports["gong.libretro"]` takes effect, on the same rom. Build the key from `fs_name`, and note the failure is silent

## RB-33. Only when a setting changed that session

Plan says: ES rewrites `es_settings.cfg` on exit, so a write can be clobbered (L1118-1119)

Measurement says: Only when a setting **changed** that session. Start-and-quit, and even a session that launched a game, left the file untouched. Amends RB-23 in this table

## RB-34. Whether ES preserves keys it does not model

Plan says: (not addressed) whether ES preserves keys it does not model

Measurement says: It does. A deliberately nonsense per-game key survived a real ES rewrite intact. But ES **prunes any setting equal to its own default** (`Language=en_US` vanished, `fr_FR` survived)

## RB-169. Plaintext credentials, and this constrains what may ever be logged or checked in

The claim being checked: (not addressed) what an `es_settings.cfg` holds

What was measured: **Plaintext credentials, and this constrains what may ever be logged or checked in.** A real install's file carries `ScreenScraperPass`, `global.retroachievements.password`, `global.retroachievements.token` and `IGDBSecret` in clear, plus the user's name under `ScreenScraperUser`, `global.retroachievements.username` and `global.netplay.nickname`. So no capture may be checked in unredacted, and nothing RomMBat writes to a log, a report or a probe transcript may echo a value read out of it

## RB-170. Incomplete as stated: the same key came back on its own, at the same value

The claim being checked: ES prunes a setting whose value equals its own default, measured on `Language=en_US` (**M0, probe 2**)

What was measured: **Incomplete as stated: the same key came back on its own, at the same value.** `Language` was **absent** on 2026-08-23 and ES had **added `Language=en_US`** by 06:32 on 2026-08-24, with nothing else added, nothing dropped and no other value changed, on an install nobody was experimenting with. So `en_US` is either not the default or the pruning is not a plain equals-default test. The rule the code needs is stronger and simpler: **presence is not evidence either.** A key can appear without the user doing anything, so "the key holds the stock value" must not be read as "the user chose this"

## RB-178. Refuted, and it moves the design. A key written while ES is running does not survive

The claim being checked: ES keeps keys it does not recognise, so the per-game override is durable and the hazard is ordinary two-writer contention (**M0 probe 2, [`docs/PLAN.md`](https://github.com/Spinnich/rommbat/blob/d33109479/docs/PLAN.md), the `retrobat-layout` skill**)

What was measured: **Refuted, and it moves the design. A key written while ES is running does not survive.** Driven on `K:` with EmulationStation up: two custom keys were merged in atomically and confirmed on disk in escaped form, and ES's next write discarded both. **`Language` is the proof it is not a merge**, because ES added that key itself at startup and dropped it again on the same write, so what ES serialises is a model loaded at boot and not the file as it stands. The rule covering this and M0 together is **ES loads `es_settings.cfg` at startup and serialises that model on every write: a key present at load survives, including one ES cannot understand, and a key that appears afterwards is discarded.** M0's nonsense key survived because it was written before ES started. So "write while ES is idle" is not prudence, it is the only thing that works, and merging plus atomicity do not help: this write was both and was still discarded

## RB-179. There are two writes per session, not one: the file is also rewritten during launch

The claim being checked: ES writes `es_settings.cfg` on exit, only when a setting changed that session (**M0 probe 2, RB-33**)

What was measured: **There are two writes per session, not one: the file is also rewritten during launch.** Timed against the hook spool, which stamps ES's own `start` and `quit` events to the millisecond. The launch write landed **7.7 s before the `start` hook fired** and added `Language`, merging into a file it had just read; the same key appeared unprompted on a second install the same morning. The session's other write landed **2.4 s before the `quit` hook**, so it is the exit write M0 described, confirmed rather than replaced. **What is not established, and an earlier revision of this row wrongly claimed, is that a mid-session setting change triggers its own write**: the toggle and the quit were not separated in time, so the one observed write is equally explained by either, and nothing here distinguishes them. The design consequence is unchanged either way, because an exit write alone is enough to discard anything written underneath it.

## RB-185. "Auto", which is EmulationStation's own name for the key being absent, and the per-game override is invisible

The claim being checked: (not addressed) what the ES menu shows for a feature RomMBat has overridden per game

What was measured: **"Auto", which is EmulationStation's own name for the key being absent, and the per-game override is invisible there.** `es_features.cfg` declares three choices for `pcsx2_slot1_memory` (`standard`, `folder`, `game`) and no `auto`; across the whole file only 3 of 10,724 choice entries declare one explicitly, so ES synthesises AUTO for any unset feature, and `switchauto`/`sliderauto` are the same idea for switches and sliders. **Two consequences.** Reverting a conversion to `prior_state = 'absent'` returns the menu to Auto, which is where the user actually was; writing the stock value instead would leave them somewhere visibly different, which is what migration 010's two-column prior state exists to prevent. And after a conversion the system-scoped menu still reads **Auto** while the per-game key silently outranks it, so a user checking the menu sees no sign that one of their games has been converted. **`folder`, the third choice, is not used and not measured**: whether it is per-game or merely a different container format for a still-shared card is unknown here, and `game` is the one M0 read and this stage drove

## RB-199. Third corroboration, on a third occasion

The claim being checked: `Language` appears on its own, so presence is not evidence of authorship (**RB-170**)

What was measured: **Third corroboration, on a third occasion.** Across the three GameCube launches ES added `Language=en_US` and changed nothing else: 56 settings before, 57 after. The `gamecube.dolphin_sync_saves` key written while ES was down survived all three sessions untouched, which is RB-179's rule holding in the direction the writer depends on

## RB-200. The write comes first, every time, and the gap is sub-second rather than RB-179's 2.4 s

The claim being checked: The `quit` hook could fire before ES writes `es_settings.cfg`, which would make polling for the process merely tidy rather than necessary (**this stage's brief**)

What was measured: **The write comes first, every time, and the gap is sub-second rather than RB-179's 2.4 s.** Three sessions, timed from `GET /quit`: the file's mtime moved at **175.6 / 324.8 / 324.1 ms**, the `quit` hook stamped itself at **807.3 / 524.8 / 551.6 ms**, and the process was gone at **875.5 / 573.1 / 604.0 ms**. So ES writes, then fires the hook **200 to 630 ms** later, then exits. **Nothing touched the file again**, neither before the exit nor in the three seconds sampled after it, at a 25 ms sample interval

## RB-202. Corroborated on three more

The claim being checked: ES's launch write lands before the `start` hook, so `start` is inside the discard window (**RB-179, one session**)

What was measured: **Corroborated on three more.** Reading the file's mtime at the first sample of each session against the `start` hook's own stamp: the launch write preceded the hook by **4.9 / 1.6 / 1.7 s**. So `background start` must never write `es_settings.cfg`: by the time it runs, ES has already loaded its model _and_ already written the file once

## RB-210. It waits, and it applies

The claim being checked: (not addressed) whether an apply-at-quit change waits for the quit

What was measured: **It waits, and it applies.** `pcsx2_slot1_memory` was queued while ES was down, was **absent from `es_settings.cfg` ten seconds after the `start` hook fired**, and was written at the quit: _EmulationStation gone after 10 ms, applying 1 queued change(s)_, then _Applied - set ps2["Armored Core 3 (USA).chd"].pcsx2_slot1_memory = game_. The row is stamped `applied` and `save_conversion` recorded `prior_state=absent`, so it is reversible

## RB-248. Where a per-game emulator choice lives

Question: (not addressed) where a **per-game emulator choice** lives

Measured: **In `gamelist.xml`, not `es_settings.cfg`.** Setting one game's emulator through ES's own menu left `es_settings.cfg` **byte-identical** and added `<emulator>` and `<core>` children to that `<game>` element. Fourteen `nes["<rom>.zip"].emulator` keys written in exactly the form `EsSettingsFile.PerGameKey` produces survived every ES rewrite untouched and were **never read**: five launches under them all ran the system-level `nes.emulator` / `nes.core` pair. The `<system>["<rom>"].<key>` chain is `emulatorlauncher`'s and covers feature keys; ES resolves the emulator before `emulatorlauncher` exists. It fails silently and looks exactly like a working configuration

## RB-358. The per-game `es_settings.cfg` override works, in both halves (complete)

**This is the load-bearing result for the whole class-D conversion story.** Every mechanism
the plan proposes for turning a shared save container into a per-game one (PCSX2
`pcsx2_slot1_memory=game`, DuckStation `PerGameFileTitle`, `flycast_vmupergame`) is written
through the per-game form of `es_settings.cfg`, and neither half of that form had been
measured. Both now are (`tools/m0-probes/probe2-per-game-override.ps1`).

`smooth` is used as the test key because it lands in the regenerated
`emulators/retroarch/retroarch.cfg` as `video_smooth`, so every result is read from disk
rather than judged on screen. The per-game value is always the one that _differs_ from the
stock value, so "honoured" and "ignored" cannot both look like the baseline.

| Case | `es_settings.cfg`                                    | Launched        | `video_smooth` | Shows                           |
| ---- | ---------------------------------------------------- | --------------- | -------------- | ------------------------------- |
| A    | nothing                                              | `gong.libretro` | `false`        | baseline                        |
| B    | `ports.smooth=1`                                     | `gong.libretro` | **`true`**     | system scope is honoured at all |
| C    | `ports.smooth=1` + `ports["2048.libretro"].smooth=0` | `2048.libretro` | **`false`**    | **per-game beats system**       |
| D    | same as C                                            | `gong.libretro` | `true`         | the override does not leak      |
| E    | `ports.smooth=1` + `ports["gong"].smooth=0`          | `gong.libretro` | `true`         | **basename form is ignored**    |
| F    | `ports.smooth=1` + `ports["gong.libretro"].smooth=0` | `gong.libretro` | **`false`**    | E's pair, extension restored    |

Three things follow, and all three bind M6:

1. **`emulatorlauncher` honours `<system>["<rom filename>"].<key>`, and it outranks the
   system-scoped key.** The plan's precedence chain is confirmed at the level that matters.
2. **The key must carry the full rom filename including its extension.** E and F differ in
   nothing but the extension on the same rom, and only F took effect. So the key is built
   from RomM's `fs_name`, never from a stem. Getting this wrong fails **silently**: the
   emulator launches normally and simply keeps writing to the shared container.
3. **The override is scoped to exactly one rom** (D), so per-game opt-in really is per-game
   and does not quietly re-configure a user's whole system.

C was re-run through the genuine path as a cross-check, launching 2048 with
`POST http://127.0.0.1:1234/launch` so EmulationStation invoked `emulatorLauncher` itself
rather than the probe doing it. Same result, `video_smooth = "false"`.

The escaping is unremarkable and matches what ES writes itself:

```xml
<string name="ports[&quot;2048.libretro&quot;].smooth" value="0" />
```

## RB-359. ES only rewrites `es_settings.cfg` when a setting actually changed, and it keeps keys it does not understand

The second half of the question was whether an ES restart survives the override. It does,
but measuring it turned up a **correction to what this document previously recorded**.

The first attempt proved nothing: ES was started, given five seconds, and quit through
`GET /quit`, and **`es_settings.cfg` was never written at all**, mtime unchanged to the
second. A second session that went further and _launched a game_ through `POST /launch`
also left the file untouched. So the blanket claim "ES rewrites `es_settings.cfg` on exit"
is wrong as stated: **the write is conditional on something having changed during the
session.** The earlier mtime evidence came from sessions where an operator was navigating
the UI, which is what dirtied it.

To measure the case that matters, the session was forced dirty by pointing `LastSystem` at
a system with no games, so ES falls back to a real one and has a genuine change to save.
That run did rewrite the file, and the result is the good one:

| Written before the session                                | Present after ES rewrote it |
| --------------------------------------------------------- | --------------------------- |
| `ports.smooth`                                            | **kept**                    |
| `ports["2048.libretro"].smooth`                           | **kept**                    |
| `ports["2048.libretro"].rommbat_probe_unknown` (nonsense) | **kept**                    |

The nonsense key is the informative one: ES preserved a key it can have no knowledge of, so
this is not "ES models the per-game form" but the stronger and more useful **ES round-trips
what it does not recognise**. What ES does change on rewrite is cosmetic and must be
tolerated rather than fought: it re-indents with tabs, and it sorts entries alphabetically
within the `bool`, `int`, `string` groups.

**One real pruning behaviour, and it is a trap.** `<string name="Language" value="en_US" />`
was present before the rewrite and **gone after it**. Setting the same key to `fr_FR` and
repeating the run, it survived. So **ES drops any setting whose value equals its own
default** and keeps the rest. Custom keys are safe because ES has no default to compare
them against, but any code that writes an ES-known key at its stock value must expect the
entry to vanish, and must not read that absence as tampering.

**What this means for M6. Superseded by RB-178 and RB-179; read those instead.** This
section concluded that the override was durable and that the merge-don't-clobber rule stood
only for the ordinary reason that two writers share a file. That conclusion holds **only for a
write made before ES starts**, which is the one case measured here. Driven the other way, with
ES running, an atomic merged write was discarded by ES's next write. ES serialises a model
loaded at startup, so the key survived above because it predated the load, not because ES
tolerates it. "Write while ES is idle" turns out to be the whole mechanism rather than
prudence.
