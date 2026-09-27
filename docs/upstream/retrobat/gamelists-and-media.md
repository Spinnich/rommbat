---
summary: How ES reads and rewrites `gamelist.xml`, names media, and treats its scraper options.
read-when: Before writing a gamelist or media, or honouring a scraper option.
---

# RetroBat: gamelists and media

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-23. Confirmed by mtime for `roms/<system>/gamelist.xml` and `es_settings.cfg`

Plan says: ES may overwrite `gamelist.xml` on exit (M4, L403-404)

Measurement says: Confirmed by mtime for `roms/<system>/gamelist.xml` **and** `es_settings.cfg`. But `system/es_menu/gamelist.xml` was **not** rewritten across two sessions, so menu registration is a gentler case

## RB-29. Only if you do not reload

Plan says: Writing `gamelist.xml` while ES runs may be clobbered on exit (L403-404, M4)

Measurement says: Only if you do not reload. ES holds a stale model and rewrites from it at exit, so **write then `GET /reloadgames`** and the edit sticks and shows immediately. ES merges in place, preserving comments

## RB-44. Not supported

Plan says: A 100k-entry gamelist "would make EmulationStation unusable" (core principle 2, L104)

Measurement says: **Not supported.** ES loads 100,000 entries in **2.07 s** for 419 MB, and 2.93 s with artwork on disk. Cap the gamelist for gamepad navigability, not because ES cannot take it

## RB-101. Confirmed exactly, read off a real scraped install rather than from memory: `images/<stem>-image.png`

Previously: Media is named after the ROM file (plan M4, `retrobat-layout`)

Measurement says: Confirmed exactly, read off a real scraped install rather than from memory: `images/<stem>-image.png`, `images/<stem>-thumb.png`, **`images/<stem>-marquee.png`** (marquee lives under `images/`, not its own folder), `videos/<stem>-video.mp4`, `manuals/<stem>-manual.pdf`, where `<stem>` is the ROM file name without its extension

## RB-102. Incomplete, and two of the four are unobserved

Previously: ES writes back favourite, playcount, lastplayed and hidden (plan M4)

Measurement says: **Incomplete, and two of the four are unobserved.** Across 4,531 entries in 32 real gamelists: `playcount` 115, `lastplayed` 115, **`gametime` 114**, and **no `favorite` and no `hidden` at all**. The merge surface is much wider: `scrap` 4,525 (self-closing, `name` and `date` attributes), `game@id` 4,493, `cheevosHash` 4,187, `md5` 2,815, `cheevosId` 2,329, `arcadesystemname` 568, `multidisk` 161, `crc32` 8. Own an allowlist, never a blocklist

## RB-103. Refuted

Previously: **XML comments survive an ES rewrite** (probe 3, "Writing `gamelist.xml` under a running ES")

Measurement says: **Refuted.** When ES does rewrite the file it drops **every** comment, both at document level and inside a `<game>` it did not otherwise touch. Unknown **elements** and **attributes** do survive, including `<scrap/>` in its self-closing form and `id`/`source` on `<game>`, so the original conclusion holds for everything except comments

## RB-104. Only when it has something to change

Previously: ES rewrites `gamelist.xml` on exit (probe 3)

Measurement says: **Only when it has something to change.** A full session that started ES, called `/reloadgames`, and quit left a 1,810-byte file **byte-identical**, mtime included. The rewrite in probe 3 followed a game actually being played. So the no-churn regression is meaningful, but it has to compare the file **after** ES has touched it, not the one RomMBat wrote

## RB-105. For entries it does not touch

Previously: ES merges in place, so element order survives (probe 3)

Measurement says: **For entries it does not touch.** Playing one game rewrote that entry's children into ES's own order (`path,name,desc,genre,rating,releasedate,developer,publisher,players,favorite,playcount,lastplayed,gametime,lang,region,...`), **moved it to the end of the file**, and **dropped `<hidden>false</hidden>`**, which is the same default-pruning seen on `es_settings.cfg`. The untouched entry kept RomMBat's order exactly

## RB-106. What a gamelist entry with no file behind it does

Previously: (not addressed) what a gamelist entry with no file behind it does

Measurement says: **Nothing.** ES reported 6 games for 6 ROM files while the gamelist held 3 entries, one of them naming a file that does not exist, so a stale entry left by an eviction is not a phantom game. It **does survive the rewrite**, so it is inert but permanent until RomMBat removes it

## RB-111. A cap cannot deliver that on its own

Previously: The per-system cap is for navigability (probe 5, plan M4)

Measurement says: **A cap cannot deliver that on its own.** ES lists ROM files it has no gamelist entry for (probe 3, and reconfirmed here), so dropping entries hides no games and only strips their art. `ParseGamelistOnly` does exist as an ES setting, beside `IgnoreGamelist`, backing `--gamelist-only`, but it is global and would change every system including ones RomMBat does not manage

## RB-204. Its encoding is not like any other, and merging with the shipped writer would rewrite all 96 entries

The claim being checked: `system/es_menu/gamelist.xml` is a gamelist like any other, so RomMBat's writer can merge into it (**this stage's brief**)

What was measured: **Its encoding is not like any other, and merging with the shipped writer would rewrite all 96 entries.** The stock file is **UTF-8 with a BOM and CRLF endings**; `GamelistDocument` writes no BOM and LF. Against **42 of 42** `roms/<system>/gamelist.xml` across both installs, which are no BOM and LF, this one file is the exception. `GamelistDocument` now records the convention of the file it loaded and reproduces it

## RB-205. Third session, and the strongest one: ES had the change in its model and still left the file alone

The claim being checked: `system/es_menu/gamelist.xml` was not rewritten by ES across two sessions, which is an absence of evidence rather than a guarantee (**probe 7**)

What was measured: **Third session, and the strongest one: ES had the change in its model and still left the file alone.** The entry was written, `/reloadgames` was called, ES listed it by name, and after `/quit` the file's **md5 and mtime were both unchanged** from what the probe had written. Probe 7's two sessions did not include a session where ES had a reason

## RB-206. No, and it is worth recording as a wrong guess a measurement caught, the way 196 was

The claim being checked: ES rewrote that gamelist on exit, stripping the BOM and reindenting to two spaces (**this session's own first reading**)

What was measured: **No, and it is worth recording as a wrong guess a measurement caught, the way 196 was.** The rewrite was the probe's own `XmlDocument.Save`, which defaults to CRLF and two-space indentation. The first run compared the post-quit file against the **shipped** one rather than against the bytes the probe itself had just written, so the probe's own write was the only thing the comparison could ever have shown. Retracted, and the probe now hashes the file immediately after writing it

## RB-207. Three `<game>` elements in the stock file are commented out

The claim being checked: (not addressed) what a gamelist merge into `es_menu` must preserve beyond the entries

What was measured: **Three `<game>` elements in the stock file are commented out**, `citra_canary`, `yuzu-early-access` and `zsnes-dos`, which is how RetroBat disables an entry it still ships the markup for. Read together with 205, RomMBat is the **only** writer that could drop them, so the comment preservation `GamelistDocument` already has stops being incidental here. It also explains a count that looks wrong: the file holds 96 `<path>` elements and 93 live `<game>` entries, and ES reports 92 because four entries name a `.menu` that is not on disk

## RB-238. Both switches ship on, and absence is a deliberate off

Question: **What an absent `ScrapeVideos` or `ScrapeManual` means, since 170's pruning makes absence ambiguous in general**

Measured: **Both switches ship on, and absence is a deliberate off. `RetroBat ships templates that override EmulationStation's compiled defaults`, which is the general trap and the reason the first answer here was wrong.** `system/templates/emulationstation/es_settings.cfg` carries `ScrapeVideos` and `ScrapeManual` as **`true`**, byte-identical on a fresh 8.2.1 install and on the used one, and a fresh install's scraper menu shows both **on**. EmulationStation's own compiled defaults are the opposite: `Settings.cpp` at `c686ca8b`, the last commit to that file before the 2026-08-23 release, registers `mBoolMap["ScrapeVideos"] = false` and registers `ScrapeManual` **nowhere**, so `SETTINGS_GETSET(bool, mBoolMap, getBool, setBool, false)` returns `false`. `saveMap` then drops any key equal to its registered default, and any key with no registered default equal to `false`, so **turning a switch off deletes the key** and a literal `value="false"` never occurs. The three states are `true` on, **absent** off, and `false` never. **Seeding is once, not per launch**: the used install's template still says `true` while its live file has no `ScrapeVideos` line and was rewritten by ES at 06:37 the same morning without restoring it. **The consequence for RomMBat** is that the absent branch must be off: reading it as RomMBat's own default left 389 MB of megadrive video and **2.05 GB across the tree** that no setting could reach, and made the round-two fix a no-op for the kind it was written for. Generalises 170 and 235, and corrects the method: for anything in this file, read `system/templates/` and a fresh install, because upstream source is not evidence of what a RetroBat does

## RB-239. Three of them are not media kinds at all, they are source pickers for slots RomMBat already fills

Question: **What the other nine scraper options mean, and which of them RomM can actually serve**

Measured: **Three of them are not media kinds at all, they are source pickers for slots RomMBat already fills.** The source comments name the tag: `ScrapperImageSrc` feeds `<image>`, `ScrapperThumbSrc` feeds `<thumbnail>`, `ScrapperLogoSrc` feeds `<marquee>`. Values are `ss`, `sstitle`, `mixrbv1`, `mixrbv2`, `box-2D`, `box-3D`, `fanart`, `wheel`, `marquee`, and empty for NONE. **RomM's `ss_metadata` carries fourteen paths, not the one `logo_path` RomMBat reads**: `title_screen_path`, `miximage_path`, `miximage_v2_path`, `box2d_path`, `box3d_path`, `box2d_back_path`, `box2d_side_path`, `fanart_path`, `logo_path`, `marquee_path`, `bezel_path`, `physical_path`, `video_path`, `video_normalized_path`. Coverage over 200 rows per platform (megadrive / snes / atari2600): cover **82/100/100%**, `title_screen` **82/100/99%**, `miximage_v2` **82/100/100%**, `box2d_back` **81/100/100%**, `logo` **82/100/100%**, `bezel` **49/97/96%**, `manual` **55/96/95%**. **Every one of those numbers says when that platform was last scraped and with what settings, and nothing about what RomM or ScreenScraper can serve.** That is what makes the apparent `box2d` / `box3d` split a reading error rather than a finding: megadrive shows 0% 2D and 81% 3D, atari2600 the exact inverse, and the cause is Spinnich's own library rather than the platform. `box2d` in that form is new to RomM and the older platforms have not been rescraped for it, and he has recently stopped storing `box3d`, so the recently scraped platform has none. `fanart_path` at 0% on all three is the same class of observation and is **not** evidence the field is unusable. **The design consequence is that coverage must never decide what RomMBat supports**: support what the schema exposes, let an absent path be the ordinary `Missing` case, and re-read the numbers as a snapshot that moves the next time an administrator rescrapes. **`ScrapeMap` and `ScrapePadToKey` are dead**: no map field exists anywhere in the 5.2.0 schema, and padtokey is ES's own input config rather than media. `ScrapeBoxBack` maps to `box2d_back_path` and `ScrapeBezel` to `bezel_path`, both real. ES's own gamelist vocabulary is wider than the menu and reads `titleshot`, `magazine`, `cartridge`, `boxart`, `wheel` and `mix` as paths too

## RB-240. Not by hash, because RomM publishes none for media, and it does not need one

Question: **How a source change could be detected, since a re-fetch has to know the slot was filled differently**

Measured: **Not by hash, because RomM publishes none for media, and it does not need one.** `gamelist_metadata` was **absent on every row sampled** across all three platforms, so its `md5_hash` never arrives, and no media path on any block carries a hash of its own. Recording **which source filled the slot** on the `local_file` row makes a settings change an ordinary `recorded != wanted` comparison, which is the `Discard` path 7b-2b already built for a kind being turned off, widened by one column. **Duplication needs no policy either**: ES removes Box 2D from the Box Source list when Image Source is set to `box-2D`, on `imageSource->setSelectedChangedCallback`, so upstream prevents the collision at the menu and RomMBat mirrors that rather than inventing a rule

## RB-241. No. Switching scrapers can rewrite a source the user never touched

Question: **Whether the stored source value is stable, since the menu changes with the SCRAPE FROM setting**

Measured: **No. Switching scrapers can rewrite a source the user never touched.** `GuiScraperSettings`'s constructor builds every row from `Scraper::getScraper()`, guarding each on `isMediaSupported(...)`, and when the stored value is not in the new scraper's list `selectFirstItem()` picks one and `addSaveFunc` writes it on close. So `ScrapperImageSrc` tracks the last scraper selected when that menu was closed rather than a deliberate choice. The rule for RomMBat is to read the value, map what it recognises, treat anything else as a fallback, and **ignore the `Scraper` setting entirely**, because RomM is not any of the scrapers it names. Spinnich's observation, checked against the source

## RB-356. The gamelist ceiling: there isn't one worth designing around

The last open item, measured on the live install with synthetic corpora in an otherwise empty
`roms/snes` (`tools/m0-probes/probe5-gamelist.ps1`). Each row is a cold ES start against that
size, timed from process start to `/systems` reporting the full count, since ES parses the
gamelists **before** it opens the HTTP port and `/caps` therefore tracks total startup rather
than preceding the load.

| Entries     | `gamelist.xml` | Cold start | Working set | Read the list | Reload effect |
| ----------- | -------------- | ---------- | ----------- | ------------- | ------------- |
| 200 (floor) | 0.13 MB        | 1.67 s     | 211 MB      | 15 ms         | 269 ms        |
| 1,000       | 0.65 MB        | 1.60 s     | 216 MB      | 24 ms         | 533 ms        |
| 5,000       | 3.2 MB         | 1.55 s     | 225 MB      | 104 ms        | 260 ms        |
| 10,000      | 6.5 MB         | 1.54 s     | 240 MB      | 167 ms        | 274 ms        |
| 25,000      | 16.2 MB        | 1.55 s     | 272 MB      | 401 ms        | 538 ms        |
| 50,000      | 32.5 MB        | 2.05 s     | 312 MB      | 769 ms        | 525 ms        |
| **100,000** | **65.0 MB**    | **2.07 s** | **419 MB**  | 1457 ms       | 1084 ms       |

**100,000 entries in one system cost ES about half a second of startup and 208 MB.** Memory
is linear at roughly **2 MB per 1,000 entries**, and cold start barely moves: 1.5 s at every
size up to 25k, 2.05 s from 50k. Nothing here degrades, breaks or thrashes.

Repeated at 100,000 with a real image file per entry on disk, since a gamelist that references
artwork nobody has is the optimistic case:

| 100,000 entries     | Cold start | Working set |
| ------------------- | ---------- | ----------- |
| metadata only       | 2.07 s     | 419 MB      |
| with 100,000 images | **2.93 s** | 402 MB      |

Artwork costs **0.9 s of startup and no memory at load**, which says ES stats the files during
the scan and decodes textures lazily while browsing.

**So the per-system gamelist cap the plan wanted from this probe is not an ES limit.** Core
principle 2's "a 100k-entry gamelist would make EmulationStation unusable" is not supported:
ES loads exactly that in under three seconds. The reason to cap a gamelist is that **a human
cannot navigate 100,000 entries with a gamepad**, which is core principle 3's curation
argument and a product decision, not a measured ceiling. M4 should enforce a cap for
navigability, and can stop treating a large gamelist as a technical hazard.

Two caveats, stated because they bound the claim:

- **This measures loading, not scrolling.** Cold start, reload, working set and the API read
  are all readable from ES; on-screen scroll smoothness is not, and nothing here substitutes
  for it.
- `/reloadgames` returns in 1-2 ms and does the work afterwards, so its response time measures
  nothing. The reload column above is the time until ES reports a change made on disk, which
  is what M4 actually waits for. Even at 100k that is **about a second**.

## RB-388. Writing `gamelist.xml` under a running ES is safe, but only if you reload

ES **does** rewrite `gamelist.xml` on exit: mtime landed at 23:48:31, exactly when ES closed.
But the concurrent edits survived, both the renamed entry and a raw XML comment stamp.
Comparing the file before and after that rewrite:

- **XML comments survive.** ES is not regenerating the document from its model; it loads,
  modifies and saves, so unknown nodes are preserved.

  > **Half of this is withdrawn. See RB-103.** Unknown elements and attributes do
  > survive, `<scrap/>` in its self-closing form included. **Comments do not**: an ES rewrite
  > drops every one, at document level and inside a `<game>` alike. What is preserved is the
  > node tree its parser keeps, and comments are not in it.

- **`<path>` element order is unchanged.**

  > **True only for entries ES did not touch. See RB-105.** The played entry's children
  > were rewritten into ES's own order, the entry moved to the end of the file, and
  > `<hidden>false</hidden>` was pruned as a default.

- **No `<game>` entry was written for the metadata-less probe rom**, even though ES listed it
  in the API. ES only persists entries it has metadata for.

**The reason the write survived is the actionable part, and it is a sequencing rule:** the
edit was made _and then_ `/reloadgames` was called, so ES had it in memory before serialising
on exit. ES demonstrably holds a stale model otherwise, and demonstrably rewrites the file at
exit, so an edit made **without** a following reload would be overwritten by that stale model.

> **Rule for M4: write the gamelist, then immediately `GET /reloadgames`.** That converts the
> plan's "write only while ES is idle" constraint into a much cheaper "write then reload".
> The negative case, editing without reloading and then quitting, was not directly executed;
> it is inferred from the stale-model and rewrite-on-exit measurements, both of which were.
>
> **Two M4 measurements bound the rule.** ES rewrites the file only when it has something to
> change, so a session that touched nothing leaves it byte-identical (RB-104); and the
> reload is ignored outright while a game is running (RB-107), which is precisely when a
> background sync is most likely to be writing.
