-- Migration 003: make the store able to hold files, an interrupted download, and a budget.
--
-- 001 declared local_file so M3 had somewhere honest to write, and 002 filled in what M2
-- resolved. Downloading found five shapes that could not carry the work. Nothing here is a
-- preference, and each is named with what breaks without it.
--
-- 1. local_file's folder and file_name carry no CHECK. 001's one rule is that no column ever
--    holds an absolute path, and relative_path is guarded while the two name columns beside
--    it are not. M3 is the first milestone that writes any of the three, so this is the last
--    moment the constraint costs nothing.
--
-- 2. local_file cannot say what a recorded hash describes. RomM's md5_hash, sha1_hash and
--    crc_hash all describe the *uncompressed* content, measured: a 1,025-byte zip reports the
--    hashes of the 16,400-byte nes file inside it (finding 80). So a row holding the hash of
--    a .zip and a row holding the hash of its content are different facts that would compare
--    equal, and adoption would either re-download everything or accept the wrong file.
--
-- 3. local_file cannot say which check it passed. Only 91% of roms carry an md5 and 96% a
--    sha1 (finding 85), so verification degrades to size for the rest, and "verified" has to
--    mean something a person can read rather than a bare timestamp.
--
-- 4. Nothing holds an interrupted download. A .part on disk is bytes with no provenance: the
--    validator it was started against, its intended destination and its expected length all
--    live here, because resuming without them is how a stale range silently splices.
--
-- 5. Nothing holds an install-wide setting. The per-set byte budget is on sync_set; the
--    global one had no home, and M4's media budget and M5's firmware budget will want the
--    same shelf.
--
-- And one shape 002 declared that turned out to be two facts:
--
-- 6. sync_set_member.state cannot express "excluded because it is multi-file" or "excluded
--    because this filesystem cannot hold it". Both were being folded into
--    excluded_extension, which tells a user their .bin/.cue set is the wrong *format* and
--    sends them to fix the wrong thing. excluded_over_bytes is not available for either:
--    002's header reserves it for the eviction pass, and a cap doing its job is a different
--    fact from a filesystem that physically cannot hold the file.
--
-- Two tables are rebuilt rather than ALTERed, because SQLite cannot add a CHECK to an
-- existing column. Rows are copied even where no shipped build has written one.

-- ---------------------------------------------------------------------------
-- 1. The file inventory, with every name column constrained
-- ---------------------------------------------------------------------------

CREATE TABLE local_file_v2 (
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

  -- A RetroBat folder name, never a path, so it carries the same CHECK as every other name
  -- column in this schema.
  folder        TEXT    NOT NULL CHECK (
                  length(trim(folder)) > 0
                  AND folder NOT LIKE '%/%'
                  AND folder NOT LIKE '%\%'
                  AND folder NOT LIKE '%:%'
                ),
  rom_id        INTEGER,

  -- The file name with its extension. The per-game es_settings.cfg override key is built
  -- from it and a bare stem is ignored silently, so the extension stays attached.
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

  -- What the three hashes above describe. 'archive_content' is the single file inside a
  -- one-entry archive, which is what RomM stores and therefore the only thing worth
  -- comparing against; 'file' is the bytes on disk. A multi-entry archive has no single
  -- content hash, so it stays 'file' and is verified by size.
  hash_scope    TEXT    NOT NULL DEFAULT 'file'
                        CHECK (hash_scope IN ('file', 'archive_content')),
  mtime_utc     TEXT,
  verified_at   TEXT,

  -- Which check the file last passed, so status can say "verified by size, the server has no
  -- hash for this rom" instead of implying a comparison that never happened.
  verified_by   TEXT    NOT NULL DEFAULT 'none'
                        CHECK (verified_by IN ('md5', 'sha1', 'size', 'none')),
  origin        TEXT    NOT NULL DEFAULT 'synced' CHECK (origin IN ('synced', 'adopted'))
);

INSERT INTO local_file_v2 (
  id, relative_path, folder, rom_id, file_name, size_bytes, md5_hash, sha1_hash, crc_hash,
  mtime_utc, verified_at, origin
)
SELECT
  id, relative_path, folder, rom_id, file_name, size_bytes, md5_hash, sha1_hash, crc_hash,
  mtime_utc, verified_at, origin
FROM local_file;

DROP TABLE local_file;

ALTER TABLE local_file_v2 RENAME TO local_file;

CREATE INDEX ix_local_file_rom ON local_file (rom_id);
CREATE INDEX ix_local_file_folder ON local_file (folder);

-- Adoption asks "is a file with this content already here" once per candidate, so the hash
-- is a lookup key rather than a stored attribute.
CREATE INDEX ix_local_file_md5 ON local_file (md5_hash);

-- ---------------------------------------------------------------------------
-- 2. An interrupted download
-- ---------------------------------------------------------------------------

-- One in flight per rom, so the primary key is the rom id and starting a second download of
-- the same rom replaces the first rather than racing it.
--
-- The .part lives under emulators/rommbat/partial/, never beside the target, so a power loss
-- cannot leave a half-written file in a folder EmulationStation scans and offers to launch.
-- The verified file is renamed into roms/ and this row is deleted in the same transaction.
--
-- There is deliberately no bytes_done column. The length of the file on disk is the only
-- honest answer after a power cut, and a stored count that disagrees with it is exactly the
-- value a resume would splice at.
CREATE TABLE content_download (
  rom_id        INTEGER PRIMARY KEY,

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

  -- Content-Length from the first response. fs_size_bytes agrees with it for single-file
  -- roms (finding 83), and this is what the transfer is measured against.
  expected_size INTEGER,

  -- The ETag the first response carried, sent back as If-Range. nginx's form is
  -- hex(mtime)-hex(size), so it moves when the file does. A stale one answers 200 with the
  -- whole body rather than a corrupt splice (finding 79), which is what makes resuming safe
  -- even across a server restart.
  validator     TEXT,
  started_at    TEXT    NOT NULL,
  updated_at    TEXT    NOT NULL,
  attempts      INTEGER NOT NULL DEFAULT 0,
  last_error    TEXT
);

-- ---------------------------------------------------------------------------
-- 3. Install-wide settings
-- ---------------------------------------------------------------------------

-- Free-form on purpose: the keys are owned by the code that reads them. Install-local, unlike
-- the set definitions: sync_config does not carry them, so a re-paired device starts back at
-- unlimited. M3 writes content.max_bytes and content.free_space_floor_bytes.
CREATE TABLE setting (
  key        TEXT PRIMARY KEY CHECK (length(trim(key)) > 0),
  value      TEXT,
  updated_at TEXT NOT NULL
);

-- ---------------------------------------------------------------------------
-- 4. Two exclusions that were being reported as a third
-- ---------------------------------------------------------------------------

CREATE TABLE sync_set_member_v3 (
  sync_set_id   INTEGER NOT NULL REFERENCES sync_set (id) ON DELETE CASCADE,
  rom_id        INTEGER NOT NULL,

  -- excluded_multi_file: RomM serves this rom as a zip built on demand. Any Range on that
  -- download is refused 403 by nginx, the rom-level hashes describe neither the zip nor its
  -- members, and RetroBat wants the members rather than the archive, so v1 does not sync it.
  -- It is reported as multi-file rather than as an unsupported format, because the format is
  -- not what is wrong with it.
  --
  -- excluded_filesystem_limit: the target volume cannot hold the file, which today means a
  -- rom over 4 GB on FAT32. Checked before the download starts, because the write fails as
  -- ERROR_DISK_FULL, "There is not enough space on the disk", on a volume with plenty free.
  --
  -- excluded_over_count and excluded_over_bytes stay reserved for the eviction pass, as 002
  -- said: a cap turning a candidate away is counted rather than stored per rom.
  state         TEXT    NOT NULL DEFAULT 'member' CHECK (state IN (
                  'member',
                  'departed',
                  'excluded_extension',
                  'excluded_unmapped',
                  'excluded_multi_file',
                  'excluded_filesystem_limit',
                  'excluded_over_count',
                  'excluded_over_bytes'
                )),

  folder        TEXT CHECK (
                  folder IS NULL OR (
                    length(trim(folder)) > 0
                    AND folder NOT LIKE '%/%'
                    AND folder NOT LIKE '%\%'
                    AND folder NOT LIKE '%:%'
                  )
                ),
  platform_slug TEXT    NOT NULL,

  fs_name       TEXT    NOT NULL CHECK (
                  length(trim(fs_name)) > 0
                  AND fs_name NOT LIKE '%/%'
                  AND fs_name NOT LIKE '%\%'
                  AND fs_name NOT LIKE '%:%'
                ),
  fs_extension  TEXT,
  size_bytes    INTEGER NOT NULL DEFAULT 0,

  -- What the server says this rom's content hashes to, carried on the membership so a
  -- re-sync can decide a file on disk is already the right one without asking. Both are
  -- hashes of the uncompressed content, so for an archive they describe what is inside it.
  -- Null is normal: 9% of roms carry no md5.
  md5_hash      TEXT,
  sha1_hash     TEXT,

  display_name  TEXT    NOT NULL,
  sort_key      TEXT    NOT NULL,
  rom_updated_at TEXT,
  position      INTEGER,
  resolved_at   TEXT    NOT NULL,
  PRIMARY KEY (sync_set_id, rom_id)
);

INSERT INTO sync_set_member_v3 (
  sync_set_id, rom_id, state, folder, platform_slug, fs_name, fs_extension, size_bytes,
  display_name, sort_key, rom_updated_at, position, resolved_at
)
SELECT
  sync_set_id, rom_id, state, folder, platform_slug, fs_name, fs_extension, size_bytes,
  display_name, sort_key, rom_updated_at, position, resolved_at
FROM sync_set_member;

DROP TABLE sync_set_member;

ALTER TABLE sync_set_member_v3 RENAME TO sync_set_member;

CREATE INDEX ix_sync_set_member_state ON sync_set_member (sync_set_id, state, position);
CREATE INDEX ix_sync_set_member_folder ON sync_set_member (folder);
CREATE INDEX ix_sync_set_member_rom ON sync_set_member (rom_id);
