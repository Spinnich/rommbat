---
summary: ROM content downloads: ranges, resumption, multi-file ROMs and what the hashes describe.
read-when: Before downloading or verifying ROM content.
---

# RomM: downloads

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-79. A single-file ROM resumes on `Range` and `If-Range`, and a stale validator gets the whole file

Verified: RomM 5.1.1-beta.1, 2026-08-09, and 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: ranged, resumed and stale-validator requests on a 4,458-byte `.zip` row, and `HEAD`, plain and ranged requests on a 26 MB `.chd`; on 5.4.0 ranged, resumed and stale-validator requests on a 1,778-byte `.zip` row, and `HEAD`.
`GET /api/roms/{id}/content/{fs_name}` is nginx serving the file from disk. The plain 200, the
206 and `HEAD` carry the same `ETag`, in nginx's `hex(mtime)-hex(size)` form (`"6a143c9b-116a"`,
`0x116a` being 4,458), and the same total. `Range: bytes=100-1123` answers 206 with exactly
1,024 bytes. A resume with the current `ETag` in `If-Range` completes to a byte-identical file,
and a stale one answers a full 200 rather than a splice. No `Authorization` header answers 401.
So a single-file download sends `Range`, adds `If-Range` when resuming, and discards the partial
file on a 200.

## RB-357. `Content-Disposition`'s plain `filename=` is percent-encoded

Verified: RomM 5.1.1-beta.1, 2026-08-09, and 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: read the header on single-file and multi-file downloads; on 5.4.0 read the header on a single-file and a multi-file download.
The header carries a plain `filename="…"` and `filename*=UTF-8''…`, and both hold the
percent-encoded name (`ANTR%20-%20Another%20NES…`, `Ball%20%28World%29.zip`). A client reading the plain parameter writes a
name full of `%20` that ES cannot match to a gamelist entry. RomMBat never reads the header and
names the file from the row's `fs_name`.

## RM-15. A multi-file ROM's plain and ranged answers are two different zips

Verified: RomM 5.3.0-alpha.2, 2026-09-13, and 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: plain, `bytes=0-` and `bytes=0-1023` requests to a `neogeocd` and a `pcenginecd` row, headers only; on 5.4.0 sent the same three requests for two `win` rows.
The plain 200 is a zip the server builds per request, with no `ETag` and no `Accept-Ranges`. A
ranged request is answered 206 from a cached zip on disk, with an `ETag` whose size half is the
ranged total. The two lengths differ: 2,740,866 against 2,740,768 on `neogeocd`, and 9,439,703
against 9,439,575 on `pcenginecd`; on 5.4.0, 196,854,244 against 196,854,146 on a `win` row, the plain answer
carrying a `Content-Length` and still no `ETag` or `Accept-Ranges`. The plain length is stable across repeats, but the gap
differs per ROM and moves when the cached zip is rebuilt. A resume that starts on the plain
answer and continues on the ranged one would splice two files, and the plain answer has no
validator to catch it. So RomMBat sends no `Range` for a multi-file ROM and restarts an
interrupted one. `LiveContentTests` fails the day the two agree.

## RB-315. One member of a multi-file ROM downloads alone, resumably, through `file_ids`

Verified: RomM 5.3.0, 2026-09-23, and 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: `?file_ids=<id>` on each member of a `psx` disc set: 1 KB ranges at each end of both discs, and the whole `.m3u`; on 5.4.0 asked for one member of a `win` row alone, with a 1 KB range and whole.
`GET /api/roms/{id}/content/{fs_name}?file_ids=<one id>` serves that member as its own file. It
answers a range with 206, an `ETag` and a `Content-Range` over the member's full size, and each
disc's body begins with the CHD magic. The `.m3u` member's md5 matches its row in the detail's
`files[]`, and a member's size matches its row. So RomMBat fetches a `psx` disc set member by member, and each disc resumes and
verifies on its own.

## RB-83. `fs_size_bytes` is the served length for a single file, and the members' sum for a multi-file ROM

Verified: RomM 5.1.1-beta.1, 2026-08-10, and 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: compared `fs_size_bytes`, the detail's `files[]` and `Content-Length` on the rows of RB-79 and RM-15; on 5.4.0 compared the same three on two `win` rows.
For a single-file ROM, `fs_size_bytes` equals the download's `Content-Length`. For a multi-file
ROM it is the sum of the members (2,740,189 = 2,740,080 + 109), and the served zip is larger by
its container, 677 bytes there, 862 on the `pcenginecd` row and 629 on a `win` row on 5.4.0. `HEAD` on a multi-file ROM
reports the cached zip's length, or 0 before one exists, and never the plain GET's. So the disk
budget is arithmetic on `fs_size_bytes`, and nothing pre-flights a size with `HEAD`.

## RB-80. A `.zip` row's hashes describe the file inside it

Verified: RomM 5.1.1-beta.1, 2026-08-10, and 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: downloaded a 4,458-byte `nes` `.zip` and a 26 MB `psx` `.chd`, and hashed the archive, its member and the `.chd`; on 5.4.0 hashed a 1,778-byte `.zip` and its member.
The `.zip` row's `md5_hash`, `sha1_hash` and `crc_hash` match the 24,592-byte `.nes` inside it
and nothing about the archive. The `.chd` row's match its own bytes. So a downloaded zip is
verified by hashing inside it, and adoption hashes inside a local zip too.

## RM-28. A firmware file's md5 is over its own bytes, so a zip's is the container's

Verified: RomM 5.2.0, 2026-08-25, and 5.3.1 source, 2026-09-29, and 5.3.1, 2026-10-03, and 5.4.0, 2026-10-17. How: downloaded the library's 34-member `neogeo.zip` and hashed the bytes; on 5.3.1 read `FirmwareHandler.calculate_file_hashes`; on 2026-10-03 matched the 18 distinct zip md5s in RetroBat's manifest against `GET /api/firmware`'s 313 records by name and md5; on 5.4.0 downloaded every firmware zip under 5 MB the library holds and hashed it against its record, and read `calculate_file_hashes`.
The record's `md5_hash`, `c74b8945...`, is the md5 of the 1,861,788 bytes served, not of any
member, unlike a ROM zip (RB-80). Two zips of the same members hash differently when compression,
order or stored times differ, so an md5 join finds a zip only when both sides hold the same
build. Of the 18 distinct zip md5s RetroBat's manifest names, the library held 4 by name:
`awbios.zip` and `gamate.zip` matched, while `neogeo.zip` (wanted at `dffb72f1...`) and
`neocdz.zip` did not. RomM serves no member hashes, so a zip's members are compared on the
downloaded file, against the crc32 and size a reader's retrobios profile states for each.

## RB-180. RomM's hashes are its last scan's, and can describe some other file

Verified: 2026-08-24, and RomM 5.3.1, 2026-09-29. Carried: RomM 5.4.0, 2026-10-17, the scan diff makes a new row carry its hashes from the insert and leaves how a recorded hash is read untouched, and nothing in the delta recomputes a hash on read, so a hash can still predate the file it describes. How: compared two `ps2` `.chd` rows' `sha1_hash` with the bytes served; today re-read both rows and their files' `Last-Modified`.
On 2026-08-24 two `ps2` `.chd` rows (191723 and 192797) reported a `sha1_hash` that matched no
bytes RomM served: 1 MB ranges at each end were identical to a real install's copy, whose sha1
differs. The full 929 MB download failed verification and left no `.part`. Both files are
unchanged on the server since March, and a rescan on 2026-08-27 and 28 rewrote both rows with
the right hashes. So a recorded hash can be wrong for a file that is served correctly, until
the next scan. A mismatch at the exact size therefore names both hashes and blames the
record, and the sha1 is never checked as a second opinion.
