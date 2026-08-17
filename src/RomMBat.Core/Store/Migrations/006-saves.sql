-- Migration 006: give saves, slots, the launch cursor and the unsyncable report a home.
--
-- 001 declared `journal` and `outbox` for this milestone and nothing has ever written a row
-- into either. Those two still fit. Five other shapes had no table at all, and four of them
-- are named below with what breaks without them. M6 ships in two stages, so every table here
-- is declared for both: stage 1 writes class A and B rows, stage 2 writes C and D into the
-- same columns, and there is no second migration against the same tables.
--
-- 1. A save on disk with its logical content hash and whether it has ever been uploaded.
--    RomMBat.Core.Content.SaveGuard says in its own remarks that it cannot answer "is there a
--    save on disk that has never been uploaded", and that gap is why M3 shipped eviction with
--    a mitigation instead of an answer. `uploaded_content_hash IS NULL` is that answer.
--
-- 2. The server-side identity of a slot. Four of its fields cannot be recomputed locally:
--    `save_id`, which is the only way to address the save for download or ack; `file_name`,
--    which the server rewrites to `<name> [YYYY-MM-DD_HH-MM-SS]<ext>` and which is NOT the
--    name to write on disk; `server_content_hash`, which decides a conflict; and
--    `origin_device_id`, which is how this device recognises its own upload coming back down.
--
-- 3. The read position into emulatorLauncher.log. Measured on a real install: the file
--    rotates at a ~1 MiB size threshold and the two halves do not overlap, so a byte offset
--    is wrong across a rotation and a timestamp is not. See docs/retrobat-findings.md, 112.
--
-- 4. What cannot be synced, and why. Stage 1 discovers class C and D and every save state and
--    ships none of them, so the alternative to a table here is a user whose PS3 saves quietly
--    never go up. Reported, not ignored.
--
-- Two things deliberately get no column.
--
-- `journal` is unchanged. It already carries the event, the relativised rom path, the
-- basename, the display name, and the system, emulator and core that correlation fills in
-- from the launch log, plus a state of open, correlated or discarded. A `game-start` row's
-- `recorded_at_utc` is the session start and the matching `game-end` row's is the end, so a
-- play session needs no third timestamp column.
--
-- `game_id_binding` is unchanged and stays empty. Its `learned_from` already admits
-- 'journal', 'rom_header' and 'user', which are stage 2's two attribution routes and a manual
-- override. Stage 1 attributes class A and B by filename and learns nothing.
--
-- `outbox` is rebuilt rather than ALTERed, because both new columns carry a CHECK and SQLite
-- cannot add one to an existing table. Rows are copied even though no shipped build has
-- written any.

-- ---------------------------------------------------------------------------
-- What is on disk
-- ---------------------------------------------------------------------------

-- One row per save unit: for class A and B a single file, for class C and D the root of the
-- unit. Keyed by path, because that is what a rescan rediscovers and what a slot is derived
-- from.
CREATE TABLE local_save (
  id                    INTEGER PRIMARY KEY,
  relative_path         TEXT    NOT NULL UNIQUE CHECK (
                          length(trim(relative_path)) > 0
                          AND substr(relative_path, 1, 1) NOT IN ('/', '\')
                          AND substr(relative_path, 2, 1) <> ':'
                          AND relative_path NOT LIKE '%\%'
                          AND relative_path <> '..'
                          AND relative_path NOT LIKE '../%'
                          AND relative_path NOT LIKE '%/../%'
                          AND relative_path NOT LIKE '%/..'
                          AND relative_path LIKE 'saves/%'
                        ),

  -- The RetroBat system folder, and the emulator whose shape definition claimed this path.
  -- Shape is a property of (system, emulator), never of system alone: psx under libretro is
  -- class A and psx under DuckStation is a database-keyed memory card.
  system                TEXT    NOT NULL CHECK (
                          length(trim(system)) > 0
                          AND system NOT LIKE '%/%'
                          AND system NOT LIKE '%\%'
                          AND system NOT LIKE '%:%'
                        ),
  emulator              TEXT    NOT NULL CHECK (
                          length(trim(emulator)) > 0
                          AND emulator NOT LIKE '%/%'
                          AND emulator NOT LIKE '%\%'
                          AND emulator NOT LIKE '%:%'
                        ),
  shape_class           TEXT    NOT NULL CHECK (shape_class IN ('A', 'B', 'C', 'D')),

  -- Null until attribution succeeds. A save with no rom_id is never uploaded and is reported
  -- in `unsyncable` instead, because an upload needs a rom_id and guessing one is worse than
  -- saying so.
  rom_id                INTEGER,
  rom_relative_path     TEXT    CHECK (
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

  -- Saves pair on (rom_id, slot) and a null slot negotiates as `upload` forever, piling up
  -- duplicates. There is no such thing as a row here without one, so it is NOT NULL and the
  -- CHECK refuses blank as well as absent.
  slot                  TEXT    NOT NULL CHECK (length(trim(slot)) > 0),

  -- The hash of the logical contents, never of an archive: sorted relative paths plus each
  -- file's own digest, folded into one. 32 hex characters, matching RomM's content_hash.
  content_hash          TEXT    CHECK (content_hash IS NULL OR length(content_hash) = 32),
  size_bytes            INTEGER NOT NULL DEFAULT 0,

  -- The file's real mtime, which goes on the wire as updated_at. Never the sync time, or an
  -- offline edit loses every conflict. Coarse on FAT and exFAT, so content_hash decides and
  -- this only breaks ties.
  file_mtime_utc        TEXT,
  scanned_at_utc        TEXT    NOT NULL,

  -- SaveGuard's third question, and the whole reason this table exists. Null means this save
  -- has never reached the server, so the rom it belongs to cannot be evicted. It holds the
  -- content hash that was uploaded rather than a flag, so a save changed since its upload is
  -- distinguishable from one that was never sent.
  uploaded_content_hash TEXT    CHECK (uploaded_content_hash IS NULL OR length(uploaded_content_hash) = 32),
  uploaded_at_utc       TEXT
);

CREATE INDEX ix_local_save_rom ON local_save (rom_id);
CREATE INDEX ix_local_save_slot ON local_save (rom_id, slot);

-- "Which roms have a save that has never gone up", which is what eviction asks per rom and
-- what `status` asks in aggregate.
CREATE INDEX ix_local_save_unsent ON local_save (rom_id, uploaded_content_hash);

-- ---------------------------------------------------------------------------
-- What the server holds
-- ---------------------------------------------------------------------------

-- One row per (rom_id, slot), which is the pairing key the negotiate protocol uses.
CREATE TABLE save_slot (
  rom_id              INTEGER NOT NULL,
  slot                TEXT    NOT NULL CHECK (length(trim(slot)) > 0),

  save_id             INTEGER,

  -- Two different names, and writing the wrong one to disk makes the save invisible to the
  -- emulator, which finds a battery save by matching the rom name. `file_name` is the
  -- server's identity, tagged with the upload timestamp. `file_name_no_tags` plus
  -- `file_extension` is what goes on disk. The server returns both, so no regex is involved.
  file_name           TEXT    CHECK (
                        file_name IS NULL OR (
                          length(trim(file_name)) > 0
                          AND file_name NOT LIKE '%/%'
                          AND file_name NOT LIKE '%\%'
                        )
                      ),
  file_name_no_tags   TEXT    CHECK (
                        file_name_no_tags IS NULL OR (
                          length(trim(file_name_no_tags)) > 0
                          AND file_name_no_tags NOT LIKE '%/%'
                          AND file_name_no_tags NOT LIKE '%\%'
                        )
                      ),
  file_extension      TEXT    CHECK (
                        file_extension IS NULL OR (
                          file_extension NOT LIKE '%/%'
                          AND file_extension NOT LIKE '%\%'
                          AND file_extension NOT LIKE '%.%'
                        )
                      ),

  server_content_hash TEXT    CHECK (server_content_hash IS NULL OR length(server_content_hash) = 32),
  server_updated_at   TEXT,

  -- Names the device that uploaded the current save, so a `download` operation for a save
  -- this device itself sent is recognisable rather than acted on blindly.
  origin_device_id    TEXT,

  last_negotiated_at  TEXT,
  PRIMARY KEY (rom_id, slot)
);

-- ---------------------------------------------------------------------------
-- Where the launch log was last read
-- ---------------------------------------------------------------------------

-- Singleton. Deliberately not a byte offset: emulatorLauncher.log rotates at roughly 1 MiB
-- into emulatorLauncher.log.old, so an offset points into the wrong file afterwards. The
-- cursor is the timestamp of the newest launch consumed plus a signature of that line, and a
-- pass consumes every launch strictly newer. That is idempotent under replay, which is what a
-- flush interrupted halfway needs.
--
-- live_size_bytes is how a rotation is noticed at all: the live file getting smaller than it
-- was is the only in-tree signal that one happened.
CREATE TABLE launch_cursor (
  id              INTEGER PRIMARY KEY CHECK (id = 1),
  last_event_utc  TEXT,
  last_signature  TEXT,
  live_size_bytes INTEGER,
  read_at_utc     TEXT
);

INSERT INTO launch_cursor (id) VALUES (1);

-- ---------------------------------------------------------------------------
-- What cannot be synced, and why
-- ---------------------------------------------------------------------------

-- The alternative to this table is silence. Stage 1 discovers class C and D and every save
-- state and ships none of them, and a user whose PS3 saves are not going up is entitled to be
-- told rather than to find out.
--
-- emulator defaults to '' rather than being nullable, because SQLite treats NULLs as distinct
-- in a UNIQUE index and a nullable column here would admit one duplicate row per rescan.
CREATE TABLE unsyncable (
  id              INTEGER PRIMARY KEY,
  system          TEXT    NOT NULL CHECK (
                    length(trim(system)) > 0
                    AND system NOT LIKE '%/%'
                    AND system NOT LIKE '%\%'
                  ),
  emulator        TEXT    NOT NULL DEFAULT '' CHECK (
                    emulator NOT LIKE '%/%'
                    AND emulator NOT LIKE '%\%'
                  ),
  reason_kind     TEXT    NOT NULL CHECK (reason_kind IN (
                    -- The shape is understood and this build does not ship it. Stage 2's list.
                    'not_in_this_version',
                    -- No shape definition claims this path, so nothing may be assumed about it.
                    'unknown_shape',
                    -- A container holding several games' saves, which has no rom_id to carry.
                    'shared_container',
                    -- The shape is supported and the save could not be tied to a rom.
                    'unattributed'
                  )),
  detail          TEXT    NOT NULL CHECK (length(trim(detail)) > 0),
  file_count      INTEGER NOT NULL DEFAULT 0,
  observed_at_utc TEXT    NOT NULL,
  UNIQUE (system, emulator, reason_kind)
);

-- ---------------------------------------------------------------------------
-- The outbox grows two columns
-- ---------------------------------------------------------------------------

-- `emulator` because it is a required upload parameter and is not a free-form label: the
-- server writes it into the stored save's file_path as a directory segment
-- (users/<hex>/saves/xbox/1393/<slot>/...), so it has to be a safe path segment and it has to
-- survive the queue rather than be re-derived by splitting a slot string at flush time.
--
-- `batch_key` because class B takes one slot per file, so saturn's .bcr and .bkr are two rows
-- describing one save. Without a key tying them, a flush that lands one and fails the other
-- reports two independent results and each looks fine on its own.
--
-- Nothing writes `batch_key` yet. Stage 1 sends saves straight from local_save rather than
-- through the outbox, so the column is carried for stage 2, which routes class B and class C
-- through here and is where the batch acquires a second row to tie.
CREATE TABLE outbox_v2 (
  id              INTEGER PRIMARY KEY,
  local_sequence  INTEGER NOT NULL UNIQUE,
  kind            TEXT    NOT NULL CHECK (kind IN ('save', 'state', 'play_session')),
  rom_id          INTEGER,
  slot            TEXT,
  emulator        TEXT    CHECK (
                    emulator IS NULL OR (
                      length(trim(emulator)) > 0
                      AND emulator NOT LIKE '%/%'
                      AND emulator NOT LIKE '%\%'
                      AND emulator NOT LIKE '%:%'
                    )
                  ),
  batch_key       TEXT    CHECK (batch_key IS NULL OR length(trim(batch_key)) > 0),
  relative_path   TEXT    CHECK (
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

INSERT INTO outbox_v2 (
  id, local_sequence, kind, rom_id, slot, relative_path, content_hash, size_bytes,
  file_mtime_utc, recorded_at_utc, payload, state, attempts, last_attempt_at, last_error
)
SELECT
  id, local_sequence, kind, rom_id, slot, relative_path, content_hash, size_bytes,
  file_mtime_utc, recorded_at_utc, payload, state, attempts, last_attempt_at, last_error
FROM outbox;

DROP TABLE outbox;

ALTER TABLE outbox_v2 RENAME TO outbox;

CREATE INDEX ix_outbox_pending ON outbox (state, local_sequence);
CREATE INDEX ix_outbox_rom ON outbox (rom_id, state);
CREATE INDEX ix_outbox_batch ON outbox (batch_key);
