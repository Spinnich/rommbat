---
summary: ROM content downloads: ranges, resumption, multi-file ROMs and what the hashes describe.
read-when: Before downloading or verifying ROM content.
---

# RomM: downloads

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-78. Backwards for multi-file

Previously: Always send `Range: bytes=0-`, because multi-file ROMs need it for the cached-zip path (plan M3, `romm-api`)

Measurement says: **Backwards for multi-file.** Any `Range` on a multi-file ROM is refused **403** by nginx 1.29.5: `bytes=0-`, `bytes=0-1023` and a mid-file range alike. Without it: 200, `application/zip`, `Content-Length` present, **no `ETag`, no `Accept-Ranges`**. So a multi-file download is not resumable and not conditional, whatever header is sent

## RB-79. Confirmed on `/api/roms/{id}/content/{fs_name}` for a single-file ROM: `Range: bytes=0-` answers 206 with

Previously: Downloads are resumable (M0 probe 6a, measured on a different endpoint)

Measurement says: Confirmed on `/api/roms/{id}/content/{fs_name}` for a **single-file** ROM: `Range: bytes=0-` answers 206 with `Content-Range`, the `ETag` is nginx's `hex(mtime)-hex(size)` form (`"6a45147a-1009"`, where `0x1009` is the 4,105-byte length), a resume with a valid `If-Range` produced a byte-identical file, and a **stale `If-Range` returned a full 200** rather than a splice

## RB-80. All three do

Previously: Only `crc_hash` describes uncompressed content (plan M3, `romm-api`)

Measurement says: **All three do.** A 1,025-byte `.zip` reports `md5`/`sha1`/`crc` matching the 16,400-byte `.nes` **inside** it exactly, and matching nothing about the archive; a `.chd` reports the hashes of its own bytes. So verifying a downloaded archive against `md5_hash` fails every time unless the hash is taken inside it, and adoption must hash inside a local archive too

## RB-83. Safe for single-file: `fs_size_bytes` equalled `Content-Length` exactly

Previously: The budget is arithmetic on `fs_size_bytes` (plan M3)

Measurement says: Safe for single-file: `fs_size_bytes` equalled `Content-Length` exactly. For multi-file it is the **sum of the member files** (2,740,189 = 2,740,080 + 109) against a 2,740,866-byte served zip, ~677 bytes of container. And **HEAD's `Content-Length` is wrong there**: 2,740,768 across three HEADs against a stable 2,740,866 across two GETs, so never pre-flight a size with HEAD

## RB-180. Not on this instance's PS2 `.chd` files, and the client is right to refuse them

The claim being checked: RomM's `sha1_hash` describes the bytes it serves, so a downloaded file can be verified against it (**M3, `ContentHasher`, RB-257**)

What was measured: **Not on this instance's PS2 `.chd` files, and the client is right to refuse them.** Syncing `Armored Core 3 (USA).chd` (rom 191723, 974,163,943 B) downloaded all 929 MB and then failed with "the downloaded file does not match the sha1 the server reported", leaving no `.part` and no rom, which is the verify-then-commit rule working. The server's metadata is what is wrong: two 1 MB `Range` requests, at offset 0 and at `size - 1 MB`, came back **byte-identical to the copy on the real install**, and that copy's sha1 is `0dd306bc…` against the `a5d460d3…` the API reports. So RomM serves one file and records the hash of another. **Not an outlier**: `Gauntlet - Dark Legacy (USA).chd` (rom 192797) mismatches the same way at the same exact size. Nothing here is a client defect and nothing in M6 stage 2c touches it, but it makes the shipped adopt-and-verify path unusable for this library's PS2 titles, so the stage's hands-on pass registered its ROM row by hand and said so

## RB-315. Whether a multi-file rom's members can be fetched one at a time

Question: Whether a multi-file rom's members can be fetched one at a time

Measured: **Yes, resumably.** `GET /api/roms/{id}/content/{name}?file_ids=<one id>` answered 206 to a range, with an `ETag` and a `Content-Range` over the member's full size, and the body began with the CHD magic. Each member's md5 on the detail row's `files[]` matched the bytes. Before regrouping, the `psx` platform held 0 multi-file rows of 9,196; both regrouped roms carry an `.m3u` member RomM wrote.

## RB-357. Probe 6a: interrupted downloads (complete)

The question the plan asks is what happens to an in-flight download when the link drops.
What actually governs the design is what can happen _next_, so this was measured by killing
the client mid-transfer rather than by disturbing a live network
(`tools/m0-probes/probe6a-resume.sh`), using a 19.2 MB rom.

**Resumable download works, and RomM's implementation is correct.** The plan lists resumable
downloads as hand-written client work; that work is viable.

`HEAD /api/roms/{id}/content/{file_name}` returns everything a resumable client needs:

```text
Accept-Ranges: bytes
Content-Length: 19238769
ETag: "6a207214-1258f71"
Last-Modified: Wed, 03 Jun 2026 18:27:32 GMT
```

| Test                                               | Result                                                            |
| -------------------------------------------------- | ----------------------------------------------------------------- |
| `Range: bytes=100-1123`                            | **206**, exactly 1024 bytes                                       |
| Kill at 9,433,088 of 19,238,769 bytes, then resume | **206**, exact final size, **byte-identical to a clean download** |
| `If-Range` with the current ETag                   | **206** partial, resume proceeds                                  |
| `If-Range` with a stale ETag                       | **200** full body, so the client restarts safely                  |
| No `Authorization` header                          | **401**                                                           |

Two requirements for `RomM.Client` follow:

1. **Always send `If-Range` with the stored ETag when resuming.** The server handles it
   correctly, returning a full 200 body when the validator no longer matches. A client
   sending a bare `Range` after the file changed on the server would splice two different
   files together and produce a corrupt rom that still has the right length. Handle the 200
   by discarding the partial file and starting over.
2. **Do not parse the filename out of `Content-Disposition`.** The header carries both forms,
   and the plain `filename=` fallback is **percent-encoded rather than plain text**:

   ```text
   filename*=UTF-8''2%20Disney%20Games%20-%20...zip; filename="2%20Disney%20Games%20-%20...zip"
   ```

   A client reading the unstarred parameter gets a literal `%20`-laden name and writes files
   ES cannot match to a gamelist entry. Use `fs_name` from the rom record instead.

What this does **not** cover is a genuine link-layer drop, where the socket stalls rather
than closing. That is the case where the 21 second OS timeout of RB-353 applies, and it is why
a read timeout matters separately from `ConnectTimeout`.

## RM-15. Multi-file ROMs answer a `Range` now, and the two answers are different files (`measured`)

**Measured, not read.** `LiveContentTests.A_range_on_a_multi_file_rom_is_refused_which_is_why_none_is_sent`
fails against a live `5.3.0-alpha.2`, which is the job that test was written to do: it is kept as
a test rather than as prose "so a server that changes its mind is noticed here instead of in the
field". The server changed its mind.

The rule it encodes is a 5.2.0 measurement, recorded at [`docs/PLAN.md:1226`](https://github.com/Spinnich/rommbat/blob/38334f1ef/docs/PLAN.md#L1226) and in the `romm-api`
skill: **any `Range` on a multi-file ROM is refused 403 by nginx**, so multi-file is not
resumable by any header. Re-measured on one multi-file ROM, `neogeocd`, repeated and stable:

| Request               | Status | `Content-Length` | `ETag`              | `Accept-Ranges` |
| --------------------- | ------ | ---------------- | ------------------- | --------------- |
| no `Range`            | 200    | 2,740,790        | absent              | absent          |
| `Range: bytes=0-`     | 206    | 2,740,768        | `"6aa70200-29d220"` | implied by 206  |
| `Range: bytes=0-1023` | 206    | 1,024            | same                | implied by 206  |

The 403 is gone and multi-file is range-served. **The interesting part is the 22 byte
difference.** The two responses to one URL describe different resources: only the ranged one
carries a validator, and `Content-Range` totals 2,740,768 against the plain response's
2,740,790. Both figures are stable across repeated requests, so this is not a timestamp in a
zip rebuilt per call. Range semantics assume one representation, and here there are two.

**This is finding B again, and it is the sharper case.** A 5.3.0 server already meets today's
client, so this is a bug against the current floor rather than adoption work. **Today's code is
still correct, and its stated reason is now wrong.** `RomMConnection.Content.cs:86` gates the
header on `!request.IsMultiFile` and the comment above it gives the 403 as the reason, which is
the thing that stopped being true. The behaviour must not change on the strength of the refusal
having gone: a resume that begins on the plain response and continues on the ranged one splices
two artifacts, and the plain response carries no `ETag`, so the stale-validator restart that
protects the single-file path has nothing to compare against here.

**Single-file is unaffected**, re-checked on a 47 GB `.iso` rather than inferred: the plain 200
carries `ETag`, `Accept-Ranges: bytes` and a `Content-Length` of 47,976,480,768, and the 206
reports the same `ETag` and the same total. The two representations agree, which is nginx
serving one file from disk, and it is the contrast that makes the multi-file mismatch a finding
rather than a quirk of the measurement.

**Re-measured across a second platform in stage 1**, which #180 asked for before anything
acted on the first reading. It reproduces, and it is worse than "22 bytes" suggested:

| ROM                      | plain 200 `Content-Length` | ranged `Content-Range` total | gap |
| ------------------------ | -------------------------- | ---------------------------- | --- |
| `neogeocd`, rom 272137   | 2,740,866                  | 2,740,768                    | 98  |
| `pcenginecd`, rom 272627 | 9,439,703                  | 9,439,567                    | 136 |

**The gap is per-ROM, not a constant.** The plain length is stable across repeats within a
session (three requests, 2,740,866 every time), so it is still not a per-call rebuild, but the
first neogeocd reading above recorded 2,740,790 for that platform and this one reads 2,740,866,
so it is not stable across days either. Neither figure is retracted; both are what was served
when they were taken.

**The mechanism is now visible in the `ETag`.** nginx's validator is `hex(mtime)-hex(size)`, and
its size half is exactly the ranged total in both rows: `0x29d220` is 2,740,768 and `0x90094f` is
9,439,567. So the ranged answer is **nginx serving a zip that exists on disk** while the plain
answer is **one the application builds for the request**. They are two artifacts by construction,
which is why no amount of retrying will make them agree and why the mismatch, not the withdrawn
403, is the durable reason not to send the header.

**Acted on in stage 1 (#180).** The test is re-aimed rather than made version-aware: it now
asserts that the two answers are not interchangeable, which a 403 satisfies and a mismatched 206
satisfies, and it fails loudly on the day a server makes them agree. Four comments that gave the
403 as their reason now give the mismatch, in `RomMConnection.Content.cs`, `RomContent.cs`,
`RomRow.cs` and `SyncSetStore.cs`. Multi-file resume is **still not built**: it would need the
transfer pinned to the ranged representation for its whole life, and the plain response offers no
validator by which a mix could be detected.

**Owed:** the 5.2.0 reading stays where it is, labelled, per the version move checklist's rule on
provenance, and both sites name this section.
