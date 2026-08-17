-- Migration 007: give save states and unresolved conflicts a home.
--
-- 006 was written for both M6 stages and it holds for the stage 2 work it named: a class C or
-- D unit root goes into local_save's existing columns, game_id_binding already admits
-- 'journal', 'rom_header' and 'user', and outbox.batch_key is still waiting for the writer
-- that class C bundling will give it. None of that changes here. Two shapes it did not
-- anticipate do not fit any table it declared, and both are named below with what breaks
-- without them.
--
-- 1. A save state. It cannot go in local_save, and not for one reason but four.
--
--    local_save.shape_class is CHECK IN ('A','B','C','D'), which describes battery save
--    shapes; a state is none of them. save_slot holds the server-side identity of a
--    negotiated slot, and a state is outside the negotiate protocol entirely. A state needs
--    the emulator, the core and a version recorded with it, because RetroBat's own wiki warns
--    states break when an emulator is updated, and 006 has no column for any of the three.
--    And a state has an optional screenshot, which nothing in 006 can point at.
--
--    Measured against a live RomM rather than assumed, because POST /api/states had never
--    been called from this repo:
--
--      - It is an upsert keyed on (rom_id, file_name). Three posts of one name reused one row
--        across two different payloads. So a state has a server id worth remembering, and no
--        append to prune.
--      - The emulator is NOT part of that key. Five posts of one name under five different
--        emulator values reused one row and moved the stored file between directories. Two
--        libretro cores writing the same filename for one ROM therefore collapse into one
--        server row unless the uploaded name carries the scope, which is what
--        uploaded_file_name records.
--      - StateSchema carries no content_hash and no slot. So "does this state still need
--        sending" is answerable only from a hash this device recorded when it last sent one,
--        which is uploaded_content_hash here, and the {emulator}:{core}:{slot} slot is a local
--        identity that never goes on the wire.
--
-- 2. An unresolved conflict. 006 has nowhere to put one, which is issue #31: SaveSync returns
--    a conflict on an in-memory list, `flush` prints it once, and nothing persists it. The
--    same slot then conflicts again on every flush and emulators/rommbat/replaced/ gains
--    another dated copy each time, against the plan's "keep the previous copy aside until the
--    next successful sync".
--
--    A table rather than a state column on save_slot, deliberately. save_slot's stated job in
--    006's own header is what the server holds, and every column on it is a server fact. A
--    conflict is a local decision that has not been taken yet: it has a lifecycle save_slot
--    does not, it owns a path to the copy taken aside, and M7's UI binds to a list of them.
--    Putting a resolution state on save_slot would also mean a row that is deleted when the
--    server forgets the save, which is the wrong lifetime for the record of a choice.
--
-- Three things deliberately get no column.
--
-- No emulator version table. The version is a property of one state at the moment it was
-- taken, not of an install, so it is denormalised onto local_state. There will be as many
-- distinct values as there are emulator upgrades, which is a handful.
--
-- No state slot on save_slot and no state row in save_slot. States never negotiate.
--
-- No table for the .txt sidecar RetroBat writes beside every state. Its content is a short
-- string (UCES00995_1.00, SLUS-00404, GW7E69) and it is carried as a column, because it is
-- the mapping between RetroBat's ROM-filename scheme and the emulator's own Game-ID scheme.
-- Stage 2b's class C and D attribution wants exactly those identifiers, so it is collected
-- here and consumed there rather than being re-derived from a launch that may never recur.

-- ---------------------------------------------------------------------------
-- Save states on disk
-- ---------------------------------------------------------------------------

-- One row per state file found under a declared state directory.
CREATE TABLE local_state (
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

  system                TEXT    NOT NULL CHECK (
                          length(trim(system)) > 0
                          AND system NOT LIKE '%/%'
                          AND system NOT LIKE '%\%'
                          AND system NOT LIKE '%:%'
                        ),

  -- The emulator name as es_savestates.cfg declares it, and the core where the emulator is
  -- core-scoped. Core is '' rather than NULL so the UNIQUE index below cannot admit a
  -- duplicate per rescan: SQLite treats NULLs as distinct.
  --
  -- Both are path- and name-shaped for the same reason the save emulator is: the server
  -- writes the emulator into the stored state's file_path as a directory segment. Measured,
  -- it does not sanitise it, so an emulator carrying a separator becomes two segments there.
  emulator              TEXT    NOT NULL CHECK (
                          length(trim(emulator)) > 0
                          AND emulator NOT LIKE '%/%'
                          AND emulator NOT LIKE '%\%'
                          AND emulator NOT LIKE '%:%'
                        ),
  core                  TEXT    NOT NULL DEFAULT '' CHECK (
                          core NOT LIKE '%/%'
                          AND core NOT LIKE '%\%'
                          AND core NOT LIKE '%:%'
                        ),

  -- What produced it, so a state is never restored silently onto a different build. Both are
  -- nullable because both are best-effort reads of the install: the emulator binary is not
  -- always resolvable to a single executable, and a RetroBat that predates version.info has
  -- no version to read.
  emulator_version      TEXT,
  retrobat_version      TEXT,

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

  -- {emulator}:{core}:{slot}, with 'auto' for an autosave. Local only: POST /api/states has
  -- no slot field at all. Non-null and non-blank for the same reason a save slot is, so a
  -- rescan pairs a state with the row it already has.
  slot                  TEXT    NOT NULL CHECK (length(trim(slot)) > 0),

  -- The screenshot beside the state, when the emulator declares a distinct <image> template
  -- and a file is actually there. Absent and zero-byte are both ordinary: the mirrored image
  -- was measured correct, zero-byte and missing across three saves of one game, so this being
  -- NULL says nothing about the state.
  screenshot_path       TEXT    CHECK (
                          screenshot_path IS NULL OR (
                            length(trim(screenshot_path)) > 0
                            AND substr(screenshot_path, 1, 1) NOT IN ('/', '\')
                            AND substr(screenshot_path, 2, 1) <> ':'
                            AND screenshot_path NOT LIKE '%\%'
                            AND screenshot_path <> '..'
                            AND screenshot_path NOT LIKE '../%'
                            AND screenshot_path NOT LIKE '%/../%'
                            AND screenshot_path NOT LIKE '%/..'
                            AND screenshot_path LIKE 'saves/%'
                          )
                        ),

  -- The content of the .txt sidecar RetroBat writes beside a state, which is the emulator's
  -- own name for the game (UCES00995_1.00, SLUS-00404, GW7E69). Its presence signals nothing
  -- (some hold only the ROM filename), but where it holds a serial it is the Game ID that
  -- class C and D attribution otherwise has to read out of a ROM header.
  native_name           TEXT,

  content_hash          TEXT    CHECK (content_hash IS NULL OR length(content_hash) = 32),
  size_bytes            INTEGER NOT NULL DEFAULT 0,
  file_mtime_utc        TEXT,
  scanned_at_utc        TEXT    NOT NULL,

  -- The server's own id, which the upsert hands back and which is the only way to address a
  -- state for listing or deletion.
  state_id              INTEGER,

  -- The name this device sent, which is NOT the name on disk: it carries the emulator and
  -- core so two cores of one emulator do not collapse into one server row. Kept rather than
  -- re-derived, because the rule that builds it is this client's convention and a row written
  -- by an older build has to stay addressable.
  uploaded_file_name    TEXT    CHECK (
                          uploaded_file_name IS NULL OR (
                            length(trim(uploaded_file_name)) > 0
                            AND uploaded_file_name NOT LIKE '%/%'
                            AND uploaded_file_name NOT LIKE '%\%'
                          )
                        ),

  uploaded_content_hash TEXT    CHECK (uploaded_content_hash IS NULL OR length(uploaded_content_hash) = 32),
  uploaded_at_utc       TEXT,

  -- One row per local slot per ROM. Two cores are two slots and therefore two rows, which is
  -- the local half of the collision the uploaded name closes on the server side.
  UNIQUE (rom_id, slot)
);

CREATE INDEX ix_local_state_rom ON local_state (rom_id);

-- "Which states have never gone up", which is what `saves` reports and what a flush selects.
CREATE INDEX ix_local_state_unsent ON local_state (uploaded_content_hash);

-- ---------------------------------------------------------------------------
-- Conflicts waiting on the user
-- ---------------------------------------------------------------------------

-- One row per (rom_id, slot) that negotiated as a conflict and has not been resolved.
--
-- The row survives the flush that found it, which is the whole point: without it the same
-- slot conflicts again on every flush, MoveAside writes another dated copy each time, and the
-- only evidence a user has is console output that has scrolled away.
CREATE TABLE save_conflict (
  rom_id              INTEGER NOT NULL,
  slot                TEXT    NOT NULL CHECK (length(trim(slot)) > 0),

  -- Where the local save is, and where the copy taken before anything else happened went.
  -- local_copy_path is nullable because the copy is a courtesy in the conflict path rather
  -- than a precondition: nothing is being overwritten while a conflict is unresolved.
  local_path          TEXT    NOT NULL CHECK (
                        length(trim(local_path)) > 0
                        AND substr(local_path, 1, 1) NOT IN ('/', '\')
                        AND substr(local_path, 2, 1) <> ':'
                        AND local_path NOT LIKE '%\%'
                        AND local_path <> '..'
                        AND local_path NOT LIKE '../%'
                        AND local_path NOT LIKE '%/../%'
                        AND local_path NOT LIKE '%/..'
                      ),
  local_copy_path     TEXT    CHECK (
                        local_copy_path IS NULL OR (
                          length(trim(local_copy_path)) > 0
                          AND substr(local_copy_path, 1, 1) NOT IN ('/', '\')
                          AND substr(local_copy_path, 2, 1) <> ':'
                          AND local_copy_path NOT LIKE '%\%'
                          AND local_copy_path <> '..'
                          AND local_copy_path NOT LIKE '../%'
                          AND local_copy_path NOT LIKE '%/../%'
                          AND local_copy_path NOT LIKE '%/..'
                        )
                      ),

  -- The two sides, so the user is choosing between described things rather than between
  -- "local" and "server". The 409 body carries none of this, measured: it is a bare string.
  local_hash          TEXT    CHECK (local_hash IS NULL OR length(local_hash) = 32),
  server_hash         TEXT    CHECK (server_hash IS NULL OR length(server_hash) = 32),
  server_updated_at   TEXT,
  server_save_id      INTEGER,
  reason              TEXT    NOT NULL DEFAULT '',

  -- first_seen_at_utc is kept separate from last_seen_at_utc so a conflict standing for a
  -- week is distinguishable from one found this morning, and so re-observing one does not
  -- reset how long the user has been living with it.
  first_seen_at_utc   TEXT    NOT NULL,
  last_seen_at_utc    TEXT    NOT NULL,

  -- Null while it stands. Resolved rows are kept rather than deleted, so `saves` can say what
  -- was decided and the copy under replaced/ has something pointing at it until it is pruned.
  resolved_at_utc     TEXT,
  resolution          TEXT    CHECK (resolution IS NULL OR resolution IN ('keep_local', 'keep_server')),

  PRIMARY KEY (rom_id, slot)
);

-- "What is still waiting on the user", which is the only query the UI and `saves` make.
CREATE INDEX ix_save_conflict_open ON save_conflict (resolved_at_utc);
