-- Migration 018: a game can be several files, starting with psx disc sets.
--
-- A multi-file RomM rom was excluded outright until a platform's certification settled how RetroBat
-- wants one laid out. psx is the first: a folder named after the rom holding every disc and an .m3u
-- named after the folder, which RetroBat's EmulationStation lists as one game. So one rom now owns
-- several files, and two tables assumed it owned one.
--
-- local_file gains the kind 'rom_part'. The playlist keeps kind 'rom', because it is what
-- EmulationStation launches and what every reader of a 'rom' row means by the game: the gamelist
-- entry, the media names, save attribution by stem, the launch correlation. The discs and their
-- .cue files are 'rom_part', which those readers skip, while budget and eviction, which count every
-- row of a rom, still see them.
--
-- content_download is keyed by rom_id alone, so a set could keep one partial transfer at a time and
-- a resume of disc 2 would find disc 1's record. It is re-keyed on (rom_id, file_id), where file_id
-- is RomM's own id for the member and 0 for a single-file rom, which every existing row is.
--
-- Both tables are rebuilt, as 013 and 017 rebuilt theirs, because SQLite cannot change a CHECK or a
-- primary key in place. Every row is copied: local_file is the inventory that stops a re-sync
-- re-downloading a library, and content_download holds the partial transfers a user is part way
-- through.

CREATE TABLE local_file_v6 (
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
                  'rom_part',
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
  md5_hash      TEXT,

  hash_scope    TEXT    NOT NULL DEFAULT 'file'
                        CHECK (hash_scope IN ('file', 'archive_content')),
  mtime_utc     TEXT,
  verified_at   TEXT,
  verified_by   TEXT    NOT NULL DEFAULT 'none'
                        CHECK (verified_by IN ('md5', 'sha1', 'size', 'none')),

  origin        TEXT    NOT NULL DEFAULT 'synced' CHECK (origin IN ('synced', 'adopted')),

  CHECK (
    (kind = 'firmware' AND folder IS NULL AND rom_id IS NULL)
    OR (kind <> 'firmware' AND folder IS NOT NULL)
  ),

  CHECK (kind <> 'firmware' OR relative_path LIKE 'bios/%'),

  -- A part belongs to a game. One with no rom_id would be counted by nothing and removed by nothing.
  CHECK (kind <> 'rom_part' OR rom_id IS NOT NULL)
);

INSERT INTO local_file_v6 (
  id, relative_path, folder, rom_id, kind, file_name, size_bytes, md5_hash,
  hash_scope, mtime_utc, verified_at, verified_by, origin
)
SELECT
  id, relative_path, folder, rom_id, kind, file_name, size_bytes, md5_hash,
  hash_scope, mtime_utc, verified_at, verified_by, origin
FROM local_file;

DROP TABLE local_file;

ALTER TABLE local_file_v6 RENAME TO local_file;

CREATE INDEX ix_local_file_rom ON local_file (rom_id);
CREATE INDEX ix_local_file_folder ON local_file (folder);
CREATE INDEX ix_local_file_md5 ON local_file (md5_hash);
CREATE INDEX ix_local_file_rom_kind ON local_file (rom_id, kind);
CREATE INDEX ix_local_file_kind ON local_file (kind);

CREATE TABLE content_download_v2 (
  rom_id        INTEGER NOT NULL,

  -- RomM's id for the member being fetched, from the rom's files[]; 0 for a single-file rom.
  file_id       INTEGER NOT NULL DEFAULT 0,

  part_path     TEXT    NOT NULL UNIQUE CHECK (
                  length(trim(part_path)) > 0
                  AND substr(part_path, 1, 1) NOT IN ('/', '\')
                  AND substr(part_path, 2, 1) <> ':'
                  AND part_path NOT LIKE '%\%'
                  AND part_path <> '..'
                  AND part_path NOT LIKE '../%'
                  AND part_path NOT LIKE '%/../%'
                  AND part_path NOT LIKE '%/..'
                ),

  target_path   TEXT    NOT NULL CHECK (
                  length(trim(target_path)) > 0
                  AND substr(target_path, 1, 1) NOT IN ('/', '\')
                  AND substr(target_path, 2, 1) <> ':'
                  AND target_path NOT LIKE '%\%'
                  AND target_path <> '..'
                  AND target_path NOT LIKE '../%'
                  AND target_path NOT LIKE '%/../%'
                  AND target_path NOT LIKE '%/..'
                ),

  expected_size INTEGER,
  validator     TEXT,
  started_at    TEXT    NOT NULL,
  updated_at    TEXT    NOT NULL,
  attempts      INTEGER NOT NULL DEFAULT 0,
  last_error    TEXT,

  PRIMARY KEY (rom_id, file_id)
);

INSERT INTO content_download_v2 (
  rom_id, file_id, part_path, target_path, expected_size, validator,
  started_at, updated_at, attempts, last_error
)
SELECT
  rom_id, 0, part_path, target_path, expected_size, validator,
  started_at, updated_at, attempts, last_error
FROM content_download;

DROP TABLE content_download;

ALTER TABLE content_download_v2 RENAME TO content_download;
