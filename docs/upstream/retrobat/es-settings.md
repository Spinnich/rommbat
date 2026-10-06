---
summary: How EmulationStation reads, prunes and rewrites `es_settings.cfg`, and the per-game override form.
read-when: Before writing `es_settings.cfg`, a per-game override, or anything that must survive an ES session.
---

# RetroBat: es_settings.cfg

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-358. `<system>["<rom filename>"].<key>` is a per-game override, and the filename needs its extension

Verified: RetroBat 8.2.0, 2026-08-08. How: six `es_settings.cfg` cases on `ports`, launching a rom each time and reading `video_smooth` from the regenerated `retroarch.cfg`.

| Case | `es_settings.cfg`                                    | Launched        | `video_smooth` |
| ---- | ---------------------------------------------------- | --------------- | -------------- |
| A    | nothing                                              | `gong.libretro` | `false`        |
| B    | `ports.smooth=1`                                     | `gong.libretro` | `true`         |
| C    | `ports.smooth=1` + `ports["2048.libretro"].smooth=0` | `2048.libretro` | `false`        |
| D    | same as C                                            | `gong.libretro` | `true`         |
| E    | `ports.smooth=1` + `ports["gong"].smooth=0`          | `gong.libretro` | `true`         |
| F    | `ports.smooth=1` + `ports["gong.libretro"].smooth=0` | `gong.libretro` | `false`        |

`emulatorlauncher` honors the per-game key, it outranks the system key (C), and it reaches no
other rom (D). A key built from the stem is ignored with no error (E against F), so `EsSettingsFile.PerGameKey` builds it from
RomM's `fs_name` and refuses a name with no extension. ES writes the key with its quotes escaped,
`ports[&quot;2048.libretro&quot;].smooth`.

## RB-248. A per-game emulator choice lives in `gamelist.xml`, not here

Verified: RetroBat 8.2.1, 2026-09-13. How: set one `nes` game's emulator from ES's own menu, then launched five games under fourteen `nes["<rom>.zip"].emulator` keys.
The menu left `es_settings.cfg` byte-identical and added `<emulator>` and `<core>` children to
that game's element in `gamelist.xml`. The fourteen keys, in `PerGameKey`'s form, survived every
ES rewrite and were never read: every launch ran the system-level `nes.emulator` and `nes.core`.
ES picks the emulator before `emulatorlauncher` runs, so RB-358's chain covers feature keys only.

## RB-178. ES serializes the model it loaded at startup, so a key written while it runs is discarded

Verified: RetroBat 8.2.0, 2026-08-08 and 2026-08-24. How: wrote keys before ES started and forced a rewrite; then, on `K:`, merged two keys in atomically under a running ES and read the file after its next write.
A key present when ES starts survives its rewrites, including a nonsense per-game key ES cannot
understand. A key written after it started was on disk, escaped, and gone after ES's next write.
`Language` shows this is not a merge: ES added it at startup and dropped it on that same write.
ES re-indents with tabs and sorts entries alphabetically within the `bool`, `int` and `string`
groups. `EsSettingsFile` matches the indentation and appends a new key at the end, leaving the
sort to ES's next rewrite. RomMBat writes this file only while
the ES process is gone (`EmulationStationProcess`), and re-reads it afterwards.

## RB-179. ES can write the file during launch as well as on exit

Verified: RetroBat 8.2.0, 2026-08-08 and 2026-08-24. How: read the file's mtime across a start-and-quit session, then compared it against the `start` and `quit` hooks' own millisecond stamps over four sessions on `K:`.
A session that only started and quit left the file untouched, so ES does not write it every time.
In the four timed sessions a launch write landed 7.7, 4.9, 1.6 and 1.7 s before the `start` hook
fired. In the one diffed, it added `Language` and nothing else, so ES wrote a change of its own
before the session could make any. That session's exit write landed 2.4 s before the `quit` hook,
and RB-200 times three more. So `background start` never writes this file, because ES has
already loaded its model by then (RB-178). Whether changing a setting mid-session
triggers a write of its own is not known.

## RB-200. The exit write comes before the `quit` hook, and ES is still alive when the hook fires

Verified: RetroBat 8.2.0, 2026-08-24. How: three sessions forced to write on exit by a bogus `LastSystem`, timed from `GET /quit` with the file sampled every 25 ms.

| Event                         | When                     |
| ----------------------------- | ------------------------ |
| ES writes `es_settings.cfg`   | 175.6 / 324.8 / 324.1 ms |
| the `quit` hook stamps itself | 807.3 / 524.8 / 551.6 ms |
| the ES process is gone        | 875.5 / 573.1 / 604.0 ms |

Nothing touched the file again, before the exit or in the 3 s after it. The hook still fires
while ES is alive, so `background quit` polls for the process (RB-201) rather than trusting the
hook.

## RB-170. ES adds and drops `Language=en_US` on its own

Verified: RetroBat 8.2.0, 2026-08-08 and 2026-08-24. How: watched `Language` across ES rewrites on one install, then diffed the file across days on two installs and three GameCube launches.
`Language=en_US` vanished on a rewrite while `fr_FR` survived. On another install the key was
absent one day and present at `en_US` the next, with nothing else changed, and over three
launches ES added it again and changed nothing else, 56 settings to 57. Whether `en_US` is ES's
default is not established, so neither presence nor absence says what the user chose.
`SaveConverter` takes over a key only when it holds the value RomMBat wrote, and a conversion
records whether the key was absent or present before it (migration `010`).

## RB-185. The ES menu shows an absent feature key as "Auto", and hides a per-game override

Verified: RetroBat 8.2.0, 2026-08-24. How: read `pcsx2_slot1_memory` and the other choices in `es_features.cfg`, and the menu after a conversion.
`es_features.cfg` declares `standard`, `folder` and `game` for `pcsx2_slot1_memory` and no
`auto`. Only 3 of 10,724 choice entries declare one, so ES shows AUTO for any unset feature, and
`switchauto` and `sliderauto` do the same for switches and sliders. Reverting a conversion whose
prior state was absent removes the key, which puts the menu back on Auto, where the user was.
After a conversion the system-level menu still reads Auto while the per-game key outranks it.

## RB-210. A change queued under a running ES is applied at the quit

Verified: RetroBat 8.2.0, 2026-08-24. How: queued `pcsx2_slot1_memory` for a PS2 game, ran a whole ES session, and read the file and `background quit`'s log.
Ten seconds after the `start` hook the key was still absent. At the quit the log read
"EmulationStation gone after 10 ms, applying 1 queued change(s)", then
`ps2["Armored Core 3 (USA).chd"].pcsx2_slot1_memory = game`. The row was marked `applied` and the
conversion recorded `prior_state=absent`, so it can be reverted.

## RB-169. The file holds plaintext credentials

Verified: RetroBat 8.2.0, 2026-08-24. How: read a real install's file.
It carries `ScreenScraperPass`, `global.retroachievements.password`,
`global.retroachievements.token` and `IGDBSecret` in clear, with the user's names under
`ScreenScraperUser`, `global.retroachievements.username` and `global.netplay.nickname`. So a
capture is checked in only after `tools/m6-probes/m6-redact-es-settings.py` has redacted it,
and nothing RomMBat logs or reports may echo a credential read from it.
