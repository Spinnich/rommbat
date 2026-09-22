-- Migration 017: the extension stops gating a sync, and excluded_extension becomes excluded_folder.
--
-- <extension> in es_systems.cfg is a union across every emulator a system offers, so a file that
-- passes it may still not open in the emulator that runs, and one that fails it costs only a game
-- EmulationStation does not list. SetResolver no longer excludes on it and reports the second case
-- as a note on the resolution instead.
--
-- The one case the old gate caught that still needs a state is a rom RomM holds as a folder around
-- a single file: an empty fs_extension, a folder name for fs_name, has_multiple_files false. It
-- was reported as an unsupported format with no extension, which sent people to fix a format that
-- was fine. excluded_folder names it, and it waits on the same per-platform placement work as
-- excluded_multi_file.
--
-- Existing excluded_extension rows with no extension become excluded_folder, which is what they
-- were. Rows with a real extension are dropped: they are now members, and only a resolve can place
-- them with a position. A complete resolve rewrites every excluded row anyway.
--
-- SQLite cannot change a CHECK in place, so the table is rebuilt as 016 did, carrying every column
-- by name and recreating the three indexes. The rename happens in that copy, because the old
-- CHECK refuses the new name.

DELETE FROM sync_set_member
WHERE state = 'excluded_extension' AND length(trim(COALESCE(fs_extension, ''))) > 0;

CREATE TABLE sync_set_member_v5 (
  sync_set_id   INTEGER NOT NULL REFERENCES sync_set (id) ON DELETE CASCADE,
  rom_id        INTEGER NOT NULL,

  -- excluded_folder: RomM holds this rom as a folder around a single file. Where that file lands
  -- is a per-platform placement question, answered in certification, as for multi-file.
  --
  -- excluded_multi_file: RomM holds this rom as several files. Also waits on placement: chd sets
  -- with an m3u, bin/cue sets, update and DLC files bound for other folders, and which parts of
  -- RomM's subfolder structure to ignore.
  --
  -- excluded_no_file_on_disk, excluded_filesystem_limit, excluded_over_count and
  -- excluded_over_bytes are unchanged from 016.
  state         TEXT    NOT NULL DEFAULT 'member' CHECK (state IN (
                  'member',
                  'departed',
                  'excluded_folder',
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
  md5_hash      TEXT,
  sha1_hash     TEXT,
  display_name  TEXT    NOT NULL,
  sort_key      TEXT    NOT NULL,
  rom_updated_at TEXT,
  position      INTEGER,
  resolved_at   TEXT    NOT NULL,
  has_multiple_files INTEGER NOT NULL DEFAULT 0 CHECK (has_multiple_files IN (0, 1)),

  PRIMARY KEY (sync_set_id, rom_id)
);

INSERT INTO sync_set_member_v5 (
  sync_set_id, rom_id, state, folder, platform_slug, fs_name, fs_extension, size_bytes,
  md5_hash, sha1_hash, display_name, sort_key, rom_updated_at, position, resolved_at,
  has_multiple_files
)
SELECT
  sync_set_id, rom_id,
  CASE state WHEN 'excluded_extension' THEN 'excluded_folder' ELSE state END,
  folder, platform_slug, fs_name, fs_extension, size_bytes,
  md5_hash, sha1_hash, display_name, sort_key, rom_updated_at, position, resolved_at,
  has_multiple_files
FROM sync_set_member;

DROP TABLE sync_set_member;

ALTER TABLE sync_set_member_v5 RENAME TO sync_set_member;

CREATE INDEX ix_sync_set_member_state ON sync_set_member (sync_set_id, state, position);
CREATE INDEX ix_sync_set_member_folder ON sync_set_member (folder);
CREATE INDEX ix_sync_set_member_rom ON sync_set_member (rom_id);
