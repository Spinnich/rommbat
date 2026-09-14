-- Migration 016: a sixth membership state, for a rom RomM has a row for and no file behind.
--
-- RomM 5.3.0 adds physical games: POST /roms/physical creates a rom that never had a file, and
-- the response schema gains is_physical and has_file_on_disk. has_file_on_disk is a property
-- rather than a column, `not is_physical and not missing_from_fs`, and that second half is the
-- part that matters here: missing_from_fs is required at the 5.2.0 floor, so a rom deleted from
-- the server's disk has been reaching the download path since before any of this. Above
-- LastTested the version check warns and continues, so a 5.3.0 server and a 5.2.0-floor client
-- already meet in the field and neither cause waits for a floor move.
--
-- Its own state rather than one of the five. excluded_unmapped and excluded_extension both say
-- something is wrong on this machine and send someone here to change it, when the row is either
-- exactly as its owner intended or a problem on the server. excluded_multi_file says the file is
-- the wrong shape, and there is no file to have a shape.
--
-- Checked ahead of the shape and the extension in SetResolver for the same reason, so a physical
-- game with no fs_extension is not reported as an unsupported format.
--
-- SQLite cannot widen a CHECK in place, so the table is rebuilt. Unlike 011's report table this
-- one holds real membership, including positions eviction ranks on, so every column is carried
-- across by name and the three indexes are recreated.

CREATE TABLE sync_set_member_v4 (
  sync_set_id   INTEGER NOT NULL REFERENCES sync_set (id) ON DELETE CASCADE,
  rom_id        INTEGER NOT NULL,

  -- excluded_multi_file: RomM serves this rom as a zip built on demand, which is not resumable
  -- by any header, the rom-level hashes describe neither the zip nor its members, and RetroBat
  -- wants the members rather than the archive, so v1 does not sync it. It is reported as
  -- multi-file rather than as an unsupported format, because the format is not what is wrong
  -- with it.
  --
  -- excluded_no_file_on_disk: RomM has the row and no file behind it. Two causes the download
  -- path has to treat alike, which is upstream's own framing: a physical game never had a file,
  -- and a missing one no longer does.
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
                  'excluded_no_file_on_disk',
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

  -- 009's column, carried forward rather than re-added, so a membership that already knows a
  -- rom's shape does not forget it and wait for the next resolve to say so again.
  has_multiple_files INTEGER NOT NULL DEFAULT 0 CHECK (has_multiple_files IN (0, 1)),

  PRIMARY KEY (sync_set_id, rom_id)
);

INSERT INTO sync_set_member_v4 (
  sync_set_id, rom_id, state, folder, platform_slug, fs_name, fs_extension, size_bytes,
  md5_hash, sha1_hash, display_name, sort_key, rom_updated_at, position, resolved_at,
  has_multiple_files
)
SELECT
  sync_set_id, rom_id, state, folder, platform_slug, fs_name, fs_extension, size_bytes,
  md5_hash, sha1_hash, display_name, sort_key, rom_updated_at, position, resolved_at,
  has_multiple_files
FROM sync_set_member;

DROP TABLE sync_set_member;

ALTER TABLE sync_set_member_v4 RENAME TO sync_set_member;

CREATE INDEX ix_sync_set_member_state ON sync_set_member (sync_set_id, state, position);
CREATE INDEX ix_sync_set_member_folder ON sync_set_member (folder);
CREATE INDEX ix_sync_set_member_rom ON sync_set_member (rom_id);
