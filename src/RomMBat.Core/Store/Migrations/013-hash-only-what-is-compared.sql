-- Migration 013: local_file loses crc_hash and sha1_hash, because nothing reads either back
-- and computing them is most of the cost of verifying a download.
--
-- What the existing shape could not carry is not a value: it is the cost of producing one.
-- Every ROM download is verified, and `ContentHasher` computed md5, sha1 and crc32 in a single
-- pass over the bytes. RomMBat only ever *compares* md5, or sha1 when the server published no
-- md5. crc_hash was written on every row and read by nothing, anywhere.
--
-- Measured on the development machine, hashing a 3.41 GB PS2 image with the file already in the
-- OS cache, so these are processor numbers rather than disk ones:
--
--     read only        7,637 MB/s
--     md5 only           594 MB/s
--     md5 + sha1         338 MB/s
--
-- So a second algorithm costs 43% of the throughput, and RomMBat was paying for a third on top.
-- On the machine that measurement came from it hardly matters, because the download runs at
-- 34.5 MB/s and verification has an order of magnitude of headroom. **That is the wrong machine
-- to reason from.** RomMBat's target is a handheld off a cheap USB stick, where the download can
-- be several times faster and the processor several times slower, and there verification stops
-- being free and starts being the thing that doubles a sync.
--
-- Dropping the sha1 *comparison* is a second decision, and it does not rest on the sample this
-- header used to cite. That sample was 1,616 rom rows from three platforms, in which no row
-- carried a sha1 without also carrying an md5. **Finding 85 measured the opposite on the same
-- library**: 91.0% of 1,895 single-file roms carry md5 and 96.3% sha1, which puts at least 5.3%
-- of rows, about a hundred, in exactly the state the sample says is empty. Finding 181 says how
-- both can have been observed: rom 191723 reports `md5_hash: ''` rather than null, so a query
-- testing for a value present counts an empty string as an md5. Which number is right is #112,
-- it needs the live instance, and nothing here should be read as having settled it.
--
-- What the comparison is worth is a separate question from how many rows reach it, and that is
-- the argument this migration actually stands on. **sha1 is a second number the same server
-- published, not an independent check.** It catches a transfer that went wrong in flight, which
-- is what the length check catches too, and it cannot catch a server whose record is wrong:
-- finding 180 measured exactly that, two ps2 `.chd` files served byte-correct against sha1
-- values that describe some other file, so the strongest check available made the download
-- unusable rather than safe. A row with only a sha1 now adopts by length and `VerificationOf`
-- records `VerifiedBy.Size`, so it is honestly recorded as weakly verified rather than silently
-- trusted.
--
-- That is one server, which is the limit of every claim here, and the cost of the other choice
-- is 43% of the hashing throughput on every device.
--
-- Dropping the columns rather than leaving them unwritten, because a field that nothing fills
-- and nothing reads is a question every later session has to answer again. RomM publishes both
-- on the rom row, so a feature that wants either can add it back with its own migration and its
-- own reason.
--
-- `verified_by` keeps 'sha1' in its CHECK. Nothing produces it any more, and rows written
-- before this migration carry it: a value that is no longer written is not the same as a value
-- that was never valid.
--
-- SQLite cannot drop a column that participates in a table this old without a rebuild, and the
-- table carries two multi-column CHECKs that have to survive, so it is rebuilt whole. Rows are
-- copied even though the column being lost is the only thing changing: this table is the
-- inventory that stops a re-sync re-downloading a library, and rebuilding it empty would cost
-- every user their entire disk budget's worth of re-verification.

CREATE TABLE local_file_v5 (
  id            INTEGER PRIMARY KEY,
  relative_path TEXT    NOT NULL UNIQUE CHECK (
                  length(trim(relative_path)) > 0
                  AND substr(relative_path, 1, 1) NOT IN ('/', '\')
                  AND substr(relative_path, 2, 1) <> ':'
                  AND relative_path NOT LIKE '%\%'
                  AND relative_path <> '..'
                  AND relative_path NOT LIKE '../%'
                  AND relative_path NOT LIKE '%/../%'
                  AND relative_path NOT LIKE '%/..'
                ),

  -- The roms/ folder this file lives in, and null for firmware, which lives under bios/ and
  -- can be required by several systems at once.
  folder        TEXT    CHECK (
                  folder IS NULL
                  OR (
                    length(trim(folder)) > 0
                    AND folder NOT LIKE '%/%'
                    AND folder NOT LIKE '%\%'
                    AND folder NOT LIKE '%:%'
                  )
                ),
  rom_id        INTEGER,

  kind          TEXT    NOT NULL DEFAULT 'rom' CHECK (kind IN (
                  'rom',
                  'image',
                  'thumbnail',
                  'marquee',
                  'video',
                  'manual',
                  'firmware'
                )),

  file_name     TEXT    NOT NULL CHECK (
                  length(trim(file_name)) > 0
                  AND file_name NOT LIKE '%/%'
                  AND file_name NOT LIKE '%\%'
                  AND file_name NOT LIKE '%:%'
                ),
  size_bytes    INTEGER NOT NULL DEFAULT 0,

  -- Kept because it is read. BiosPlanner matches a recorded md5 against the manifest's, which
  -- is the one place a stored hash is compared rather than a freshly computed one.
  md5_hash      TEXT,

  hash_scope    TEXT    NOT NULL DEFAULT 'file'
                        CHECK (hash_scope IN ('file', 'archive_content')),
  mtime_utc     TEXT,
  verified_at   TEXT,
  verified_by   TEXT    NOT NULL DEFAULT 'none'
                        CHECK (verified_by IN ('md5', 'sha1', 'size', 'none')),

  origin        TEXT    NOT NULL DEFAULT 'synced' CHECK (origin IN ('synced', 'adopted')),

  -- Firmware has no game and no roms folder; everything else has both. Written as one
  -- constraint because they are one fact, and because a row that satisfied half of it would
  -- be read as a rom by every query that filters on rom_id.
  CHECK (
    (kind = 'firmware' AND folder IS NULL AND rom_id IS NULL)
    OR (kind <> 'firmware' AND folder IS NOT NULL)
  ),

  -- Firmware is written to the path the manifest names, and the manifest only ever names
  -- paths under bios/. Refusing anything else here is the last line of the same rule the
  -- manifest reader enforces: a future upstream entry that grew an md5 outside bios/ would
  -- otherwise write into an emulator's install directory.
  CHECK (kind <> 'firmware' OR relative_path LIKE 'bios/%')
);

INSERT INTO local_file_v5 (
  id, relative_path, folder, rom_id, kind, file_name, size_bytes, md5_hash,
  hash_scope, mtime_utc, verified_at, verified_by, origin
)
SELECT
  id, relative_path, folder, rom_id, kind, file_name, size_bytes, md5_hash,
  hash_scope, mtime_utc, verified_at, verified_by, origin
FROM local_file;

DROP TABLE local_file;

ALTER TABLE local_file_v5 RENAME TO local_file;

CREATE INDEX ix_local_file_rom ON local_file (rom_id);
CREATE INDEX ix_local_file_folder ON local_file (folder);
CREATE INDEX ix_local_file_md5 ON local_file (md5_hash);
CREATE INDEX ix_local_file_rom_kind ON local_file (rom_id, kind);
CREATE INDEX ix_local_file_kind ON local_file (kind);
