---
summary: How ES reads and rewrites `gamelist.xml`, names media, and treats its scraper options.
read-when: Before writing a gamelist or media, or honoring a scraper option.
---

# RetroBat: gamelists and media

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-388. ES serializes its loaded gamelist model, so an edit it has not reloaded can be lost

Verified: RetroBat 8.2.0, 2026-08-08. How: edited `roms/<system>/gamelist.xml` with ES up, called `/reloadgames`, quit, and diffed the file ES rewrote on exit.
ES holds the gamelist it loaded in memory and serializes that model when it rewrites the file on
exit. An edit followed by `/reloadgames` is in the model, shows at once and survives the rewrite.
An edit with no reload is overwritten when ES next rewrites the file, which it does only when it
has a change of its own (RB-104): that half is inferred from the measured halves, not driven. ES writes no `<game>` for a rom it has no metadata for, even while
listing the rom. `GamelistSync` writes, then reloads. A reload has no effect while a game runs
(RB-107).

## RB-104. ES rewrites a rom gamelist on exit only when it has something to change

Verified: RetroBat 8.2.0, 2026-08-11. How: a session that started ES, called `/reloadgames` and quit, with the file's md5 and mtime compared before and after.
A session that changed nothing left a 1,810-byte file byte-identical, mtime included. A session in
which a game was played rewrote it. So a no-churn check compares RomMBat's second write against
the file ES left behind, not against RomMBat's own first write.

## RB-105. A rewrite reorders and prunes the entry ES touched, and leaves the rest alone

Verified: RetroBat 8.2.0, 2026-08-11. How: played one of two RomMBat-written games, quit, and diffed the gamelist.
The played entry's children were rewritten into ES's own order (`path`, `name`, `desc`, `genre`,
`rating`, `releasedate`, `developer`, `publisher`, `players`, `favorite`, `playcount`,
`lastplayed`, `gametime`, `lang`, `region`, ...), the entry moved to the end of the file, and
`<hidden>false</hidden>` was dropped as a default. The untouched entry kept RomMBat's order.

## RB-103. A rewrite drops every XML comment and keeps unknown elements and attributes

Verified: RetroBat 8.2.0, 2026-08-11. How: seeded a gamelist with comments, unknown elements and attributes, played a game so ES rewrote it, and diffed.
Comments go at document level and inside a `<game>` ES did not otherwise touch. Unknown elements
and attributes stay, including `<scrap/>` in its self-closing form and `id` and `source` on
`<game>`. `GamelistDocument` preserves comments itself but never uses one to carry meaning.

## RB-106. A gamelist entry whose file is missing is inert, and ES keeps it

Verified: RetroBat 8.2.0, 2026-08-11. How: 6 rom files and 3 gamelist entries, one naming a file not on disk; read ES's game count, then forced a rewrite.
ES reported 6 games, so the entry is not a phantom game. It survives ES's rewrite, so a stale entry
left by an eviction stays until RomMBat removes it.

## RB-111. ES lists a rom file with no gamelist entry, so dropping entries hides no game

Verified: RetroBat 8.2.0, 2026-08-08 and 2026-08-11. How: counted ES's games against rom files and gamelist entries, on two separate days.
A capped gamelist only strips art and descriptions. `ParseGamelistOnly`, beside `IgnoreGamelist`
and backing `--gamelist-only`, would make the gamelist authoritative, but it is global and would
change every system, including ones RomMBat does not manage. RomMBat does not set it, and the
sync set's `max_games` bounds a folder instead.

## RB-356. ES loads a 100,000-entry gamelist in about two seconds

Verified: RetroBat 8.2.0, 2026-08-08. How: cold ES starts against synthetic gamelists of 200 to 100,000 entries in an otherwise empty `roms/snes`, timed to `/systems` reporting the full count.
100,000 entries (65 MB) took 2.07 s against 1.67 s for 200, with a working set of 419 MB against
211 MB, roughly 2 MB per 1,000 entries. A real image per entry raised startup to 2.93 s and left
memory flat. At 100,000, a change made on disk showed about a second after a reload. This measures
loading, not on-screen scrolling. A gamelist size is a navigability question, not an ES limit.

## RB-102. ES and its scraper write far more into a gamelist entry than play statistics

Verified: RetroBat 8.2.0, 2026-08-11. How: counted elements and attributes across 4,531 entries in 32 gamelists of a real scraped install, read only.
`playcount` 115, `lastplayed` 115 and `gametime` 114; no `favorite` or `hidden` at all. Beside
them: `scrap` 4,525 (self-closing, `name` and `date` attributes), `id` on `<game>` 4,493,
`cheevosHash` 4,187, `md5` 2,815, `cheevosId` 2,329, `arcadesystemname` 568, `multidisk` 161,
`crc32` 8. `GamelistDocument` merges an allowlist of the elements its caller names and never a
blocklist of ES's.

## RB-101. Scraped media is named after the rom file's stem

Verified: RetroBat 8.2.0, 2026-08-11. How: read the media folders of a real scraped install.
`images/<stem>-image.png`, `images/<stem>-thumb.png`, `images/<stem>-marquee.png` (the marquee
has no folder of its own), `videos/<stem>-video.mp4` and `manuals/<stem>-manual.pdf`, where
`<stem>` is the rom file name without its extension. A user's own scrape writes the same names,
so RomMBat deletes only files it recorded as its own.

## RB-204. `system/es_menu/gamelist.xml` ships with a BOM and CRLF, unlike a rom gamelist

Verified: RetroBat 8.2.0, 2026-08-24, and 8.2.1, 2026-09-28. How: read the first bytes and line endings of the stock file, and of 42 `roms/<system>/gamelist.xml` across two 8.2.0 installs.
Every rom gamelist measured is no BOM and LF, 42 of 42. Writing that convention over the
`es_menu` file would rewrite every one of its entries in order to add one, so `GamelistDocument`
records the BOM and line endings of the file it loaded and reproduces them.

## RB-205. ES does not rewrite `system/es_menu/gamelist.xml`, even with a change in its model

Verified: RetroBat 8.2.0, 2026-08-08 and 2026-08-24. How: three sessions, the last writing an entry, calling `/reloadgames`, confirming ES listed it, quitting, and comparing md5 and mtime.
The file's md5 and mtime were unchanged after all three, although the third had a change of its
own in ES's model. What RomMBat writes into this file is therefore what the user keeps, and `EsMenuEntry`
merges its one element into it.

## RB-207. The stock `es_menu` gamelist disables three entries by commenting them out

Verified: RetroBat 8.2.0, 2026-08-24, and 8.2.1, 2026-09-28. How: parsed the stock file and checked each live `<path>` against the `.menu` files on disk.
`citra_canary`, `yuzu-early-access` and `zsnes-dos` sit inside comments, so the file holds 96
`<path>` elements and 93 live `<game>` entries. On 8.2.1, one live entry names a `.menu` that is
not on disk, `suyu.menu`, which ES does not list (RB-106). Since ES never rewrites this file
(RB-205), RomMBat is the only writer that could drop those comments, and `GamelistDocument`
keeps them.

## RB-238. `ScrapeVideos` and `ScrapeManual` ship on, and an absent key means off

Verified: RetroBat 8.2.1, 2026-09-01, with the template re-read 2026-09-28. How: read `system/templates/emulationstation/es_settings.cfg` and the scraper menu on a fresh install, a used install's live file, and `Settings.cpp` at `c686ca8b`.
RetroBat's template seeds both as `true`, once at install. ES's compiled default is `false` for
`ScrapeVideos` and unregistered for `ScrapeManual`, and `saveMap` drops a key equal to its
default or, unregistered, equal to `false`. So turning a switch off deletes the key: the file says
`true` or nothing, never `false`. A template overriding ES's compiled defaults is general, so a
RetroBat default is read from `system/templates/` and a fresh install, not from ES's source.
`MediaPolicy` reads an absent key as off.

## RB-239. Three scraper options pick a source for a slot, and RomM exposes fourteen media paths

Verified: RetroBat 8.2.1 and RomM 5.2.0, 2026-09-01; the schema half against RomM 5.3.1, 2026-09-28. How: read `GuiScraperSettings.cpp` and `MetaData.cpp` at `c686ca8b`, sampled 200 roms per platform on the live instance, and read `RomSSMetadata` in the pinned schema.
`ScrapperImageSrc` feeds `<image>`, `ScrapperThumbSrc` `<thumbnail>` and `ScrapperLogoSrc`
`<marquee>`, taking `ss`, `sstitle`, `mixrbv1`, `mixrbv2`, `box-2D`, `box-3D`, `fanart`, `wheel`,
`marquee`, or empty for none. `RomSSMetadata` has fourteen `*_path` fields. `ScrapeBoxBack` maps
to `box2d_back_path`, `ScrapeBezel` to `bezel_path`, and `ScrapeMap` and `ScrapePadToKey` to
nothing. Coverage of a kind says when a platform was last
scraped, not what RomM serves: in the sample megadrive held 0% `box2d` and 81% `box3d`, atari2600
the reverse. So coverage never decides which kinds RomMBat supports; an absent path is `Missing`.

## RB-240. No media path carries a hash, and ES itself stops two slots taking one source

Verified: RetroBat 8.2.1 and RomM 5.2.0, 2026-09-01; the schema half against RomM 5.3.1, 2026-09-28. How: read `RomSSMetadata` and `RomGamelistMetadata` in the schema, and `GuiScraperSettings.cpp` at `c686ca8b`.
No `*_path` or `*_url` field carries a hash of its own, so a change of source is detectable only by
recording which source filled a slot. ES removes Box 2D from the box source list (`ScrapperThumbSrc`)
when the image source is `box-2D`, in `imageSource->setSelectedChangedCallback`, so the collision is
prevented at the menu. RomMBat does not read the source pickers yet (#108).

## RB-241. Changing the scraper can rewrite a source picker the user never touched

Verified: RetroBat 8.2.1, 2026-09-01. How: the maintainer saw SCRAPE FROM change the image source in the menu; read `GuiScraperSettings`'s constructor at `c686ca8b` to confirm why.
Every row is built from `Scraper::getScraper()` and guarded on `isMediaSupported(...)`. When the
stored value is not in the new scraper's list, `selectFirstItem()` picks one and `addSaveFunc`
writes it on close. So `ScrapperImageSrc` records the scraper last selected when that menu
closed, not a deliberate choice. A reader maps the values it recognizes, falls back on anything
else, and ignores `Scraper`, since RomM is none of the scrapers it names.
