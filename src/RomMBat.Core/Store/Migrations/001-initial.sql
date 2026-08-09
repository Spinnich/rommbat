-- Migration 001: the whole local store.
--
-- Every table later milestones need is created here, not just the ones M1 writes to, so
-- there is somewhere honest to write from the moment each milestone starts.
--
-- ONE RULE GOVERNS THIS FILE: no column ever holds an absolute path. Every path column
-- carries the same CHECK, spelled out rather than generated, so the constraint is visible
-- in the schema an operator can read with `.schema`. RomMBat.Core.Paths.RelativePath is the
-- typed half of the same rule and tests assert the two agree case for case.
--
-- The CHECK rejects, in order: an empty or blank string, a rooted path (/x or \x, which also covers
-- the UNC \\server\share form), a drive-qualified path (C:\x or C:x), any backslash at all
-- (stored paths are forward-slashed), and '..' as a whole segment anywhere.

-- ---------------------------------------------------------------------------
-- Identity and pairing
-- ---------------------------------------------------------------------------

-- Singleton. The client_device_identifier GUID also lives in emulators/rommbat/device.id,
-- which is the authority: identity has to outlive this database so that re-pairing after a
-- rebuild updates the existing RomM device rather than creating a second one.
CREATE TABLE device (
  id                       INTEGER PRIMARY KEY CHECK (id = 1),
  client_device_identifier TEXT    NOT NULL,
  server_origin            TEXT,
  romm_device_id           TEXT,
  device_name              TEXT,
  granted_scopes           TEXT    NOT NULL DEFAULT '',
  token_protection         TEXT    NOT NULL DEFAULT 'none'
                                   CHECK (token_protection IN ('none', 'passphrase')),
  token_cipher             BLOB,
  token_nonce              BLOB,
  token_tag                BLOB,
  token_kdf_salt           BLOB,
  token_kdf_iterations     INTEGER,
  token_expires_at         TEXT,
  paired_at                TEXT,
  last_seen_at             TEXT
);

-- ---------------------------------------------------------------------------
-- Ordering that survives a wrong clock
-- ---------------------------------------------------------------------------

-- One counter shared by outbox and journal, so entries in the two are orderable against
-- each other. A handheld with a flat RTC produces timestamps that lose every conflict;
-- this is what preserves ordering when the wall clock cannot be trusted.
CREATE TABLE local_sequence (
  id    INTEGER PRIMARY KEY CHECK (id = 1),
  value INTEGER NOT NULL
);

INSERT INTO local_sequence (id, value) VALUES (1, 0);

-- ---------------------------------------------------------------------------
-- Local content index
-- ---------------------------------------------------------------------------

CREATE TABLE local_file (
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
  folder        TEXT    NOT NULL,
  rom_id        INTEGER,
  file_name     TEXT    NOT NULL,
  size_bytes    INTEGER NOT NULL DEFAULT 0,
  md5_hash      TEXT,
  sha1_hash     TEXT,
  crc_hash      TEXT,
  mtime_utc     TEXT,
  verified_at   TEXT,
  origin        TEXT    NOT NULL DEFAULT 'synced' CHECK (origin IN ('synced', 'adopted'))
);

CREATE INDEX ix_local_file_rom ON local_file (rom_id);
CREATE INDEX ix_local_file_folder ON local_file (folder);

-- ---------------------------------------------------------------------------
-- Curation
-- ---------------------------------------------------------------------------

CREATE TABLE sync_set (
  id              INTEGER PRIMARY KEY,
  name            TEXT    NOT NULL UNIQUE,
  scope_kind      TEXT    NOT NULL CHECK (scope_kind IN (
                    'collection', 'smart_collection', 'virtual_collection', 'platform', 'filter'
                  )),
  scope_value     TEXT    NOT NULL,
  max_games       INTEGER,
  max_bytes       INTEGER,
  ordering        TEXT    NOT NULL DEFAULT 'name',
  eviction_policy TEXT    NOT NULL DEFAULT 'keep_favourites',
  enabled         INTEGER NOT NULL DEFAULT 1,
  created_at      TEXT    NOT NULL,
  updated_at      TEXT    NOT NULL
);

-- Membership is re-resolved every sync, because smart-collection membership drifts
-- server-side. A departed member becomes an eviction candidate, never an immediate delete.
CREATE TABLE sync_set_member (
  sync_set_id INTEGER NOT NULL REFERENCES sync_set (id) ON DELETE CASCADE,
  rom_id      INTEGER NOT NULL,
  state       TEXT    NOT NULL DEFAULT 'member' CHECK (state IN ('member', 'departed')),
  resolved_at TEXT    NOT NULL,
  PRIMARY KEY (sync_set_id, rom_id)
);

-- resolved_by records which layer of the resolution chain answered, so the mapping screen
-- can show it and a user override is distinguishable from a guess.
CREATE TABLE platform_map (
  romm_platform_slug TEXT PRIMARY KEY,
  romm_platform_id   INTEGER,
  folder             TEXT,
  resolved_by        TEXT NOT NULL CHECK (resolved_by IN (
                       'user', 'fs_slug', 'bundled', 'normalized', 'unmapped'
                     )),
  updated_at         TEXT NOT NULL
);

-- ---------------------------------------------------------------------------
-- Offline work waiting to go up
-- ---------------------------------------------------------------------------

-- Entries carry the real local mtime and content hash, never the sync time. A week offline
-- is a bigger negotiate payload and nothing else, as long as the timestamps are honest.
CREATE TABLE outbox (
  id              INTEGER PRIMARY KEY,
  local_sequence  INTEGER NOT NULL UNIQUE,
  kind            TEXT    NOT NULL CHECK (kind IN ('save', 'state', 'play_session')),
  rom_id          INTEGER,
  slot            TEXT,
  relative_path   TEXT CHECK (
                    relative_path IS NULL OR (
                      length(trim(relative_path)) > 0
                      AND substr(relative_path, 1, 1) NOT IN ('/', '\')
                      AND substr(relative_path, 2, 1) <> ':'
                      AND relative_path NOT LIKE '%\%'
                      AND relative_path <> '..'
                      AND relative_path NOT LIKE '../%'
                      AND relative_path NOT LIKE '%/../%'
                      AND relative_path NOT LIKE '%/..'
                    )
                  ),
  content_hash    TEXT,
  size_bytes      INTEGER,
  file_mtime_utc  TEXT,
  recorded_at_utc TEXT    NOT NULL,
  payload         TEXT,
  state           TEXT    NOT NULL DEFAULT 'pending'
                          CHECK (state IN ('pending', 'sent', 'failed')),
  attempts        INTEGER NOT NULL DEFAULT 0,
  last_attempt_at TEXT,
  last_error      TEXT
);

CREATE INDEX ix_outbox_pending ON outbox (state, local_sequence);

-- ---------------------------------------------------------------------------
-- What the hooks write
-- ---------------------------------------------------------------------------

-- Append-only and dumb, because it is written on the game-launch path. Correlation and
-- hashing are too slow to do inside a launch and happen later, into the outbox.
--
-- ES spawns hooks fire-and-forget and they run concurrently, so this table must tolerate
-- interleaved appends from separate processes. game-end also fires with no preceding
-- game-start, so an orphan record is normal rather than a fault.
CREATE TABLE journal (
  id                INTEGER PRIMARY KEY,
  local_sequence    INTEGER NOT NULL UNIQUE,
  event             TEXT    NOT NULL CHECK (event IN (
                      'start', 'game-start', 'game-end', 'quit', 'shutdown', 'sleep', 'wake'
                    )),
  rom_relative_path TEXT CHECK (
                      rom_relative_path IS NULL OR (
                        length(trim(rom_relative_path)) > 0
                        AND substr(rom_relative_path, 1, 1) NOT IN ('/', '\')
                        AND substr(rom_relative_path, 2, 1) <> ':'
                        AND rom_relative_path NOT LIKE '%\%'
                        AND rom_relative_path <> '..'
                        AND rom_relative_path NOT LIKE '../%'
                        AND rom_relative_path NOT LIKE '%/../%'
                        AND rom_relative_path NOT LIKE '%/..'
                      )
                    ),
  rom_basename      TEXT,
  display_name      TEXT,
  system            TEXT,
  emulator          TEXT,
  core              TEXT,
  process_id        INTEGER,
  recorded_at_utc   TEXT    NOT NULL,
  correlated_at_utc TEXT,
  state             TEXT    NOT NULL DEFAULT 'open'
                            CHECK (state IN ('open', 'correlated', 'discarded'))
);

CREATE INDEX ix_journal_open ON journal (state, local_sequence);

-- Directory saves are keyed by an internal Game ID and RomM stores no serial or title ID
-- anywhere, so class C and D attribution has to be learned and cached.
CREATE TABLE game_id_binding (
  id                INTEGER PRIMARY KEY,
  system            TEXT NOT NULL,
  game_id           TEXT NOT NULL,
  rom_id            INTEGER,
  rom_relative_path TEXT CHECK (
                      rom_relative_path IS NULL OR (
                        length(trim(rom_relative_path)) > 0
                        AND substr(rom_relative_path, 1, 1) NOT IN ('/', '\')
                        AND substr(rom_relative_path, 2, 1) <> ':'
                        AND rom_relative_path NOT LIKE '%\%'
                        AND rom_relative_path <> '..'
                        AND rom_relative_path NOT LIKE '../%'
                        AND rom_relative_path NOT LIKE '%/../%'
                        AND rom_relative_path NOT LIKE '%/..'
                      )
                    ),
  learned_from      TEXT NOT NULL CHECK (learned_from IN ('journal', 'rom_header', 'user')),
  learned_at        TEXT NOT NULL,
  UNIQUE (system, game_id)
);

-- ---------------------------------------------------------------------------
-- Talking to the server
-- ---------------------------------------------------------------------------

CREATE TABLE sync_cursor (
  endpoint        TEXT PRIMARY KEY,
  updated_after   TEXT,
  etag            TEXT,
  last_run_at     TEXT,
  last_success_at TEXT
);

-- Singleton. skew_seconds is local minus server, corrected for half the round trip, so a
-- positive value means the device clock runs fast.
CREATE TABLE clock (
  id                       INTEGER PRIMARY KEY CHECK (id = 1),
  last_server_date_utc     TEXT,
  local_at_observation_utc TEXT,
  skew_seconds             REAL,
  round_trip_ms            INTEGER,
  last_contact_utc         TEXT,
  restamp_offered_at       TEXT
);
