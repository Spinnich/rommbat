-- Migration 002: make the curation tables able to hold what M2 resolves.
--
-- 001 declared sync_set, sync_set_member, platform_map and sync_cursor so later milestones
-- had somewhere honest to write. Filling them found three shapes that could not carry the
-- work, and this migration fixes exactly those three. Nothing here is a preference.
--
-- 1. sync_set had nowhere to record the arcade folder choice. The plan requires that choice
--    to be per sync set, and platform_map is keyed by slug for the whole install, so it
--    cannot hold a per-set answer. Without this, arcade either guesses or is unusable.
--
-- 2. sync_set_member held only a rom id. The milestone's done-when is that a user can see
--    exactly which games a set resolves to, and everything must work with the server
--    switched off, so the store has to be able to answer that on its own. It also had no
--    state for an exclusion, and exclusions must be surfaced with counts and extensions
--    rather than recomputed or hidden.
--
-- 3. sync_cursor could record where an incremental sync starts but not where an interrupted
--    full walk stopped. A full walk of an 83k library is about 14 minutes (M0 probe 5), so
--    it will be interrupted, and it has to resume rather than restart.
--
-- 4. platform_map was keyed by RomM's platform slug, which RomM does not keep unique. See
--    the section below for the measurement.
--
-- The same rule as 001 governs: no column holds an absolute path. The columns added here
-- hold folder names and file names, which are neither paths nor rooted, and carry a CHECK
-- that keeps them that way.

-- ---------------------------------------------------------------------------
-- 1. The per-set folder choice
-- ---------------------------------------------------------------------------

-- Which RetroBat folder this set writes to, when the platform alone cannot say. Arcade is
-- the case that forces it: the right folder depends on the romset a file came from and
-- arcade rom names are romset-versioned, so nothing in the slug settles it. Outranks the
-- install-wide platform_map entry, because it is the narrower statement of the same choice.
ALTER TABLE sync_set ADD COLUMN folder_override TEXT;

-- When the set was last resolved, so a stale membership can say how stale it is offline.
ALTER TABLE sync_set ADD COLUMN last_resolved_at TEXT;

-- What the last resolution decided, in one line, for status output with no server.
ALTER TABLE sync_set ADD COLUMN last_resolution_summary TEXT;

-- ---------------------------------------------------------------------------
-- 2. Membership that can be read back with the server off
-- ---------------------------------------------------------------------------

CREATE TABLE sync_set_member_v2 (
  sync_set_id   INTEGER NOT NULL REFERENCES sync_set (id) ON DELETE CASCADE,
  rom_id        INTEGER NOT NULL,

  -- 'member' is in the set. 'departed' left it server-side and is an eviction candidate,
  -- never an immediate delete. The excluded_* states are the reasons a candidate did not
  -- make it, kept per rom so a count and its extensions are a GROUP BY rather than a guess.
  state         TEXT    NOT NULL DEFAULT 'member' CHECK (state IN (
                  'member',
                  'departed',
                  'excluded_extension',
                  'excluded_unmapped',
                  'excluded_over_count',
                  'excluded_over_bytes'
                )),

  -- The resolved RetroBat folder, not the RomM platform: two platforms can legitimately
  -- share one folder, so the folder is the key everything downstream groups by. Null when
  -- the platform did not resolve.
  folder        TEXT CHECK (
                  folder IS NULL OR (
                    length(trim(folder)) > 0
                    AND folder NOT LIKE '%/%'
                    AND folder NOT LIKE '%\%'
                    AND folder NOT LIKE '%:%'
                  )
                ),
  platform_slug TEXT    NOT NULL,

  -- A file name, never a path. The per-game es_settings.cfg override key is built from it
  -- and a bare stem is ignored silently, so the extension has to stay attached.
  fs_name       TEXT    NOT NULL CHECK (
                  length(trim(fs_name)) > 0
                  AND fs_name NOT LIKE '%/%'
                  AND fs_name NOT LIKE '%\%'
                  AND fs_name NOT LIKE '%:%'
                ),
  fs_extension  TEXT,
  size_bytes    INTEGER NOT NULL DEFAULT 0,
  display_name  TEXT    NOT NULL,
  sort_key      TEXT    NOT NULL,

  -- Rank in the set's ordering. Null for anything excluded, because an excluded rom has no
  -- place in the order.
  position      INTEGER,
  resolved_at   TEXT    NOT NULL,
  PRIMARY KEY (sync_set_id, rom_id)
);

-- Copy first even though no shipped build has ever written a row here, because assuming a
-- table is empty is how data goes missing on someone's stick.
INSERT INTO sync_set_member_v2 (
  sync_set_id, rom_id, state, platform_slug, fs_name, display_name, sort_key, resolved_at
)
SELECT sync_set_id, rom_id, state, '', '(unknown)', '(unknown)', '', resolved_at
FROM sync_set_member;

DROP TABLE sync_set_member;

ALTER TABLE sync_set_member_v2 RENAME TO sync_set_member;

CREATE INDEX ix_sync_set_member_state ON sync_set_member (sync_set_id, state, position);
CREATE INDEX ix_sync_set_member_folder ON sync_set_member (folder);
CREATE INDEX ix_sync_set_member_rom ON sync_set_member (rom_id);

-- ---------------------------------------------------------------------------
-- 3. A resumable full walk
-- ---------------------------------------------------------------------------

-- Where an interrupted walk stopped. Null means no walk is in flight. Offset paging is
-- ordered by ascending rom id so a rom added mid-walk lands past the cursor rather than
-- shifting every later page.
ALTER TABLE sync_cursor ADD COLUMN resume_offset INTEGER;

-- When the in-flight walk started. updated_after only advances to this once the walk
-- finishes, so an interrupted walk cannot skip the window it never covered.
ALTER TABLE sync_cursor ADD COLUMN walk_started_at TEXT;

-- How many rows the walk expects, from the one sidecar flag left on.
ALTER TABLE sync_cursor ADD COLUMN walk_total INTEGER;

-- ---------------------------------------------------------------------------
-- 4. The mapping surface, keyed by something RomM actually keeps unique
-- ---------------------------------------------------------------------------

-- 001 made romm_platform_slug the primary key. It is not unique in RomM, which was measured
-- against a real 123-platform library: only 72 slugs are distinct there, because every
-- system has an "-unofficial" twin carrying the same slug (fs_slug 'gb' and 'gb-unofficial'
-- are both slug 'gb'). Keyed by slug, 51 of those 123 platforms silently disappear from the
-- mapping surface and no one can point the unofficial set at a different folder.
--
-- fs_slug is the identifier RomM constrains unique, it is stable across a rescan, and it is
-- the one a person recognises, so it is the key. The slug stays as a column because it is
-- what the bundled table is looked up by, and the numeric id stays because it is what a rom
-- row carries.
CREATE TABLE platform_map_v2 (
  romm_fs_slug       TEXT PRIMARY KEY CHECK (length(trim(romm_fs_slug)) > 0),
  romm_platform_slug TEXT NOT NULL,
  romm_platform_id   INTEGER,

  -- What RomM calls it, so the mapping screen reads properly with no server.
  display_name       TEXT,

  -- The folder that syncs. Null when nothing is applied, which is a normal state.
  folder             TEXT CHECK (
                       folder IS NULL OR (
                         length(trim(folder)) > 0
                         AND folder NOT LIKE '%/%'
                         AND folder NOT LIKE '%\%'
                         AND folder NOT LIKE '%:%'
                       )
                     ),

  -- A normalized match, offered for confirmation and never applied. Kept apart from folder
  -- on purpose: folder is what syncs, this is what is being asked about. Accepting one
  -- writes it to folder and sets resolved_by to 'user'.
  suggested_folder   TEXT,

  -- Which layer answered. 'user' is a choice, 'fs_slug' and 'bundled' are applied guesses,
  -- 'normalized' is a suggestion that is not applied, 'unmapped' is nothing found.
  resolved_by        TEXT NOT NULL CHECK (resolved_by IN (
                       'user', 'fs_slug', 'bundled', 'normalized', 'unmapped'
                     )),

  -- Every folder the bundled table names for this slug, space separated, best first.
  candidate_folders  TEXT,

  -- 1 when the platform must not resolve on its own and someone has to pick. Arcade.
  requires_choice    INTEGER NOT NULL DEFAULT 0,

  -- Why the resolution came out this way, in one sentence, for the screen and for status.
  explanation        TEXT,
  updated_at         TEXT NOT NULL
);

INSERT INTO platform_map_v2 (
  romm_fs_slug, romm_platform_slug, romm_platform_id, folder, resolved_by, updated_at
)
SELECT romm_platform_slug, romm_platform_slug, romm_platform_id, folder, resolved_by, updated_at
FROM platform_map;

DROP TABLE platform_map;

ALTER TABLE platform_map_v2 RENAME TO platform_map;

CREATE INDEX ix_platform_map_slug ON platform_map (romm_platform_slug);
CREATE INDEX ix_platform_map_folder ON platform_map (folder);
