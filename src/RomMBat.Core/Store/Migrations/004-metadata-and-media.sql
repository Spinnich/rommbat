-- Migration 004: make the store able to say what a file is, and to rebuild a gamelist offline.
--
-- 003 gave M3 somewhere to record files, a resume point and a budget, and every one of its
-- shapes assumed a file was a rom. M4 writes four more kinds of file into the same tree and
-- has to regenerate roms/<folder>/gamelist.xml with the server switched off. Three facts had
-- no home, and each is named with what breaks without it.
--
-- 1. local_file cannot say what kind of file a row is. It was written when every file was a
--    rom, and M4 adds covers, thumbnails, marquees, videos and manuals that sit in
--    roms/<folder>/images, videos and manuals under the same rom_id. Without the column,
--    EvictionPlanner's one-row-per-rom index throws on the second file for a rom, eviction
--    cannot take a rom's media with it, and status cannot tell 2 GB of roms from 2 GB of
--    video. Rebuilt rather than ALTERed, because the column carries a CHECK.
--
-- 2. Nothing holds the metadata a gamelist is written from. Core principle 1 says every
--    operation works with the server unreachable, and a gamelist rewritten after an eviction
--    with no network would otherwise lose every description and every artwork reference it
--    could not re-fetch. This is deliberately *not* a catalog mirror: a row exists only for a
--    rom some set selected, which is what principle 2 permits as "cache catalog rows for
--    selected sync sets only".
--
-- 3. Nothing marks a gamelist entry as RomMBat's. ES's own scraper writes entries into the
--    same file, and removing one that a user scraped would destroy their work. A rom_metadata
--    row is that marker: it exists only for roms RomMBat resolved, so the gamelist writer can
--    drop a stale entry it owns and leave every other entry alone.
--
-- What deliberately gets no column is "is the file we would write the file already there".
-- For media that is local_file, exactly as it is for a rom. For the gamelist it is a byte
-- comparison against the file on disk, which the merge has to read anyway, and a stored hash
-- would be a second answer that can disagree with the first.

-- ---------------------------------------------------------------------------
-- 1. What kind of file a row is
-- ---------------------------------------------------------------------------

CREATE TABLE local_file_v3 (
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

  folder        TEXT    NOT NULL CHECK (
                  length(trim(folder)) > 0
                  AND folder NOT LIKE '%/%'
                  AND folder NOT LIKE '%\%'
                  AND folder NOT LIKE '%:%'
                ),
  rom_id        INTEGER,

  -- What this file is. 'rom' is what 003 recorded and is the default, so every existing row
  -- keeps its meaning. The other five are the media kinds M4 downloads, and they share a
  -- rom_id with the rom they decorate, which is what lets eviction take them along.
  kind          TEXT    NOT NULL DEFAULT 'rom' CHECK (kind IN (
                  'rom',
                  'image',
                  'thumbnail',
                  'marquee',
                  'video',
                  'manual'
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

  -- Unchanged in meaning and more load-bearing than before. A user's own scraper writes media
  -- to exactly the names RomMBat would use, so 'adopted' is the difference between leaving
  -- their artwork alone and deleting it.
  origin        TEXT    NOT NULL DEFAULT 'synced' CHECK (origin IN ('synced', 'adopted'))
);

INSERT INTO local_file_v3 (
  id, relative_path, folder, rom_id, kind, file_name, size_bytes, md5_hash, sha1_hash,
  crc_hash, hash_scope, mtime_utc, verified_at, verified_by, origin
)
SELECT
  id, relative_path, folder, rom_id, 'rom', file_name, size_bytes, md5_hash, sha1_hash,
  crc_hash, hash_scope, mtime_utc, verified_at, verified_by, origin
FROM local_file;

DROP TABLE local_file;

ALTER TABLE local_file_v3 RENAME TO local_file;

CREATE INDEX ix_local_file_rom ON local_file (rom_id);
CREATE INDEX ix_local_file_folder ON local_file (folder);
CREATE INDEX ix_local_file_md5 ON local_file (md5_hash);

-- "The rom file for this rom", which is now a different question from "every file for it".
CREATE INDEX ix_local_file_rom_kind ON local_file (rom_id, kind);

-- ---------------------------------------------------------------------------
-- 2. The metadata a gamelist is written from
-- ---------------------------------------------------------------------------

-- Every column holds a value already converted into what EmulationStation wants, not what
-- RomM sent. RomM's release date is milliseconds against a YYYYMMDDT000000 string, its rating
-- is 0-100 against 0-1, its genres are an array against a single element, and its regions and
-- languages use a different vocabulary in both directions. Storing the raw values would put
-- those five conversions at every read instead of at the one write.
CREATE TABLE rom_metadata (
  rom_id       INTEGER PRIMARY KEY,

  -- Where the entry belongs and what it is called, which is what makes the ownership rule
  -- above work after the file is gone. A gamelist entry is RomMBat's to remove when its
  -- <path> is './' plus an fs_name recorded here for that folder; anything else in the file
  -- was written by the user's own scraper and is left alone. Neither can be read off
  -- local_file, because eviction deletes that row and this decision is made afterwards.
  folder       TEXT NOT NULL CHECK (
                 length(trim(folder)) > 0
                 AND folder NOT LIKE '%/%'
                 AND folder NOT LIKE '%\%'
                 AND folder NOT LIKE '%:%'
               ),
  fs_name      TEXT NOT NULL CHECK (
                 length(trim(fs_name)) > 0
                 AND fs_name NOT LIKE '%/%'
                 AND fs_name NOT LIKE '%\%'
                 AND fs_name NOT LIKE '%:%'
               ),

  name         TEXT NOT NULL CHECK (length(trim(name)) > 0),
  description  TEXT,

  -- The company list, joined. There is no publisher column on purpose: metadatum.companies
  -- merges both roles and arrives alphabetically sorted, so a developer and a publisher
  -- cannot be recovered from it and a column for one would only ever hold a guess.
  developer    TEXT,
  genre        TEXT,
  family       TEXT,
  players      TEXT,

  -- Comma-joined ES language codes, and one ES region code.
  lang         TEXT,
  region       TEXT CHECK (region IS NULL OR region NOT LIKE '%,%'),

  release_date TEXT CHECK (release_date IS NULL OR length(release_date) = 15),
  rating       TEXT CHECK (rating IS NULL OR length(rating) <= 4),

  -- Where each kind of media lives on the RomM host, as a JSON object keyed by kind. Stored
  -- with the /assets/romm/resources/ prefix stripped, so the value is relative and carries the
  -- same shape guarantees as every other path column here: no leading slash, no drive letter,
  -- no backslash. The client puts the prefix back, which is also the step that stops a
  -- prefix-less request answering 200 with the web UI's index.html.
  media_paths  TEXT CHECK (
                 media_paths IS NULL OR (
                   media_paths NOT LIKE '%\%'
                   AND media_paths NOT LIKE '%"/%'
                 )
               ),

  fetched_at   TEXT NOT NULL
);

-- One gamelist is written per folder, so that is how the rows are read back.
CREATE INDEX ix_rom_metadata_folder ON rom_metadata (folder);
