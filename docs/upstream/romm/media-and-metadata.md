---
summary: Where RomM's media lives, how it is served, and what its metadata fields mean.
read-when: Before fetching media or converting RomM metadata into a gamelist.
---

# RomM: media and metadata

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-88. `url_cover` and `url_manual` are third-party URLs carrying a third party's credentials

Verified: RomM 5.1.1-beta.1, 2026-08-11, and 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: read both fields' hosts and queries on every row of the library; on 5.4.0 read both fields' hosts and queries on every row.
Every `url_manual` and all but 757 `url_cover` values are `neoclone.screenscraper.fr` API URLs
with someone else's `devid` and `devpassword` in the query string. The other 757 point at
`retroachievements.org`. Both are off-LAN, which breaks the offline story, and the credentials
are not RomMBat's to send. RomMBat reads neither field and fetches media from the server's own
paths.

## RB-89. Media paths come in two shapes: covers are rooted at the asset prefix, the rest are relative to it

Verified: RomM 5.1.1-beta.1, 2026-08-11, and 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: classified every media path in the library, and fetched a cover with and without its query; on 5.4.0 classified every media path again and fetched a cover with and without its query.
`path_cover_small` and `path_cover_large` start with `/assets/romm/resources/` and carry a
`?ts=` query holding a raw space (`ts=2026-09-23 15:40:28`). `path_manual`, `path_video` and
`ss_metadata.logo_path` are relative to that prefix. nginx ignores the query: with and without
it the bytes and `ETag` match. `MediaResource.Normalize` puts either shape onto the prefix
exactly once and drops the query.

## RB-90. A media path requested without the prefix answers 200 with the web UI's `index.html`

Verified: RomM 5.1.1-beta.1, 2026-08-11, and 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: requested a `path_manual` value as given, then under the prefix; on 5.4.0 requested a `path_manual` value as given, then under the prefix.
`roms/185/140110/manual/140110.pdf` as given answers 200, `text/html`, 10,282 bytes of
`<!doctype html>`. Under the prefix it is a 340 KB
`application/pdf`. The status cannot tell them apart, so `RomMConnection` refuses a media
response whose content type is HTML.

## RB-91. Media needs no token, and nginx serves ranges on it

Verified: RomM 5.1.1-beta.1, 2026-08-11, and 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: bearer and anonymous requests for a cover, a manual, a logo and a video, plus ranged requests; on 5.4.0 repeated the bearer, anonymous and ranged requests for a manual.
Bearer and anonymous answers are byte-identical, with the same `hex(mtime)-hex(size)` `ETag`.
`bytes=0-99` answers 206 with a `Content-Range`, and a range past the end answers 416. RomMBat
sends no token for media.

## RB-92. Media coverage and size, across a real library

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: walked all 95,975 rows on 126 platforms, and read the `Content-Range` total of 150 random files per kind.

| Kind        | Rows carrying it | Median size |
| ----------- | ---------------- | ----------- |
| Thumbnail   | 85.0%            | 114 KB      |
| Cover       | 85.0%            | 599 KB      |
| Logo        | 88.2%            | 87 KB       |
| Video       | 17.8%            | 2.50 MB     |
| Manual      | 45.1%            | 2.09 MB     |
| Screenshots | 90.5%            |             |
| `summary`   | 88.9%            |             |

`metadatum` is on every row. Coverage depends on how the library was scraped and changes when
the administrator rescrapes or removes media. Media runs to
tens of times a cartridge system's ROM bytes, which is what a disk budget has to be sized for.

## RB-92b. EmulationStation's marquee is ScreenScraper's `logo_path`, not its `marquee_path`

Verified: RomM 5.1.1-beta.1, 2026-08-11, and 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: read `gamelist_exporter.py`; counted both fields across the library; on 5.4.0 read the exporter at the tag and counted both fields across the library.
RomM's own exporter maps `"marquee": [ss.get("logo_path", ""), gl.get("marquee_path", "")]`.
ES's marquee is game logo art, and ScreenScraper's marquee is an arcade cabinet marquee.
`logo_path` is on 88.2% of rows and `marquee_path` on 86.8%. The exporter is vendored at
`reference/romm-gamelist_exporter.py` and `verify.py` checks the mapping.

## RB-95. `metadatum.first_release_date` is Unix time in milliseconds

Verified: RomM 5.1.1-beta.1, 2026-08-11, and 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: converted all 82,437 values in the library; read the exporter and `utils/datetime.format_utc`.
Read as milliseconds, the values land in 1976 to 2026. Read as seconds, they land in year 0. None
is negative, so a pre-1970 date is unobserved rather than impossible. The exporter divides by
1000, and `GameMetadata.ReleaseDateOf` reads milliseconds.

## RB-96. `metadatum.average_rating` is on a 0 to 100 scale

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17, for the RomM half; RetroBat 8.2.0, 2026-08-11, for the gamelist counts, and 8.3.0, 2026-10-14, for the scraper that writes them. How: read all 57,241 ratings in the library and the exporter; counted `<rating>` values in 32 gamelists of a real scraped install; read `ScreenScraper.cpp` at ES `45f37fd7`.
Every value is between 5.0 and 100.0, and none is 1.0 or below. A gamelist `<rating>` is 0 to 1
with two decimals, and a scraped install's ratings sit on 17 values in 0.05 steps, because ES's
scraper divides ScreenScraper's score out of 20 by 20. The exporter divides by 100, and so does
`GameMetadata.RatingOf`, which clamps above 100.

## RB-97. `player_count` is already EmulationStation's `<players>` form

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17, for the RomM half; RetroBat 8.2.0, 2026-08-11, for the gamelist counts, and 8.3.0, 2026-10-14, for the scraper that writes them. How: counted the library's values; read `<players>` in 32 gamelists of a real scraped install; read `ScreenScraper.cpp` at ES `45f37fd7`; on 5.4.0 counted the library's values again.
It is a string: `1` 60,983, `1-2` 24,153, `1-4` 6,628, `2` 1,161, across 45 distinct values up
to `1-16`. A scraped install's `<players>` uses the same vocabulary, because ES's scraper copies
ScreenScraper's `joueurs` text as it comes. RomMBat copies it
unchanged, the only metadata field it does not convert.

## RB-98. `metadatum.companies` merges developer and publisher and sorts them, so neither role survives

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: walked all 95,975 rows on 126 platforms.
`companies` is present on 84,838 rows and alphabetically sorted on every one of them, so reading
it by position reads the alphabet. 81,067 rows carry exactly two, and 34,197 name one company
twice: Chrono Trigger is `['Squaresoft', 'Squaresoft']`.

`developers` and `publishers` carry the roles, but only on a row scanned since 5.3.0, because the
server fills them on scan rather than backfilling. They are on 54,760 rows (57.1%) on 103
platforms, and 23 platforms carry none, so one library holds both shapes. Where they are present,
94.6% of rows have exactly one of each. On 99.5% of rows, `companies` is those roles sorted
together, and `companies[0]` names someone other than the developer on 25.6%. `GameMetadata`
writes `<developer>` as `companies` deduplicated and comma-joined, which claims no role.

## RB-99. RomM's arrays become comma-joined gamelist text, as a scraped install already writes

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17, for the RomM half; RetroBat 8.2.0, 2026-08-11, for the gamelist counts, and 8.3.0, 2026-10-14, for the scraper that writes them. How: counted repeated franchises in the library; read `<genre>` in 32 gamelists of a real scraped install; read `ScreenScraper.cpp` at ES `45f37fd7`; on 5.4.0 counted repeated franchises again.
In the scraped install, 2,079 of 4,440 `<genre>` values contain a comma or a slash (`Racing,
Driving`, `Action / Adventure`), across 111 distinct values: ES's scraper joins ScreenScraper's
genre and subgenre as `<genre> / <subgenre>`. So joining with a comma and a space follows the
existing convention. `franchises` repeats a name on 147 rows, so
`GameMetadata` deduplicates before joining or picking the first.

## RB-100. `regions` and `languages` use different vocabularies from a gamelist

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17, for the RomM half; RetroBat 8.2.0, 2026-08-11, for the gamelist counts, and 8.3.0, 2026-10-14, for the code that writes them. How: counted the library's values; read `<region>` and `<lang>` in 32 gamelists of a real scraped install; read `LangParser.cpp` and `FileData::detectLanguageAndRegion` at ES `45f37fd7`; on 5.4.0 counted the library's values again.
RomM writes `Japan`, `USA`, `Europe`, `World` and `Unlicensed`, and a gamelist writes `jp`, `us`,
`eu`, `wr`. RomM writes `English`, `French`, and ES writes `en,fr` comma-joined. ES does not
scrape either field: it parses both from the rom file name. 15,162 rows
carry more than one region while `<region>` holds one, and `languages` is on only 16.3% of rows.
`EsVocabulary` maps both to ES codes rather than copying them.

## RM-7. The gamelist exporter reads the split roles, and falls back to indexing `companies` (`source`)

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: read `backend/models/rom.py` and `backend/utils/gamelist_exporter.py` at the `5.3.1` tag; on 5.4.0 read the exporter at the tag, where the date goes through `utils/datetime.format_utc`, and ran `reference/verify.py`.
The exporter writes `<developer>` and `<publisher>` from `primary_developer` and
`primary_publisher`, and each falls back to the position in `companies` when the role is empty:

```python
return next(iter(self.developers or companies[:1]), None)
```

So on a row without the split (RB-98), upstream's `<developer>` and `<publisher>` are the
alphabet's first and second company. RomMBat reads neither role, on any row (RB-98). The same file divides
`first_release_date` by 1000 and `average_rating` by 100, and `verify.py` asserts both
conversions as behaviors rather than line numbers. `tools/romm-5.3-probes/r4-company-split.py`
re-takes the split counts for the whole library.
