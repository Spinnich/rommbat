-- Migration 005: make the store able to hold a file that belongs to a system rather than a rom.
--
-- 003 gave every file a rom and 004 gave it a kind, and both assumed the same two things:
-- that a file belongs to exactly one rom_id, and that it lives in a folder under roms/.
-- Firmware is neither. bios/psxonpsp660.bin belongs to the psx *system*, bios/coleco.rom is
-- required by three systems at three paths, and nothing under bios/ is inside a roms folder
-- at all. Two shapes had no home, and each is named with what breaks without it.
--
-- 1. local_file.kind cannot say 'firmware'. Without it a BIOS row would have to pretend to be
--    a rom, and then EvictionPlanner's one-row-per-rom index, the gamelist writer's
--    folder grouping and status's per-kind totals would all take it for a game.
--
-- 2. local_file.folder is NOT NULL and holds a roms/ folder name. There is no honest value
--    for firmware: bios/coleco.rom is wanted by colecovision, coleco and msx alike, so any
--    single folder written there would be a lie the next reader believes. It becomes nullable,
--    and null exactly when the row is firmware.
--
-- The CHECK doing that work carries a second rule with it, which is the point of putting it
-- in the schema rather than in a planner: a firmware row has no rom_id. Eviction's candidate
-- query already skips every row whose rom_id is null, so "firmware counts against the budget
-- and is never evicted" stops being a rule someone has to remember and becomes one the
-- database will not let a later change break. It is the right answer on the measurements
-- anyway: every file the reference library can serve totals 18.5 MiB against roughly 550 MB
-- of media for one 100-game set, so evicting firmware frees nothing and leaves a platform
-- unable to boot.
--
-- What deliberately gets no table is the gap report. It is the bundled manifest joined
-- against these rows, so it is answerable with the server unreachable, which is what
-- principle 1 asks of it, and a cached copy of the requirements would be a second answer
-- that can disagree with the manifest that generated it. What the server holds is a fact
-- about the server and is fetched when there is one.
--
-- Rebuilt rather than ALTERed, because SQLite cannot add a CHECK or relax a NOT NULL on an
-- existing column. Rows are copied even where a shipped build has written none.

CREATE TABLE local_file_v4 (
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
  md5_hash      TEXT,
  sha1_hash     TEXT,
  crc_hash      TEXT,
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

INSERT INTO local_file_v4 (
  id, relative_path, folder, rom_id, kind, file_name, size_bytes, md5_hash, sha1_hash,
  crc_hash, hash_scope, mtime_utc, verified_at, verified_by, origin
)
SELECT
  id, relative_path, folder, rom_id, kind, file_name, size_bytes, md5_hash, sha1_hash,
  crc_hash, hash_scope, mtime_utc, verified_at, verified_by, origin
FROM local_file;

DROP TABLE local_file;

ALTER TABLE local_file_v4 RENAME TO local_file;

CREATE INDEX ix_local_file_rom ON local_file (rom_id);
CREATE INDEX ix_local_file_folder ON local_file (folder);
CREATE INDEX ix_local_file_md5 ON local_file (md5_hash);
CREATE INDEX ix_local_file_rom_kind ON local_file (rom_id, kind);

-- "Every firmware file RomMBat knows about", which is one half of the gap report and is
-- asked on every sync.
CREATE INDEX ix_local_file_kind ON local_file (kind);
