-- Migration 008: let one path hold several save units, and admit the third attribution route.
--
-- 006 declared itself sufficient for this stage and 007 repeated the claim: "a class C or D
-- unit root goes into local_save's existing columns". Both were written against the plan's
-- reading of class C as "a directory per game", and a read-only sweep of a real install
-- refutes that reading on three systems at once (docs/retrobat-findings.md, 140 and 141).
--
--   ps3        BLUS30109G6A383E91  BLUS30109G6A3B071C  BLUS30109S    one title id, three dirs
--              BCUS98111-AUTOSAVE  BCUS98111-USERDATA                one title id, two dirs
--   psp        UCES01011           ULES01513SYSDATA                  the key is a PREFIX
--   gamecube   69-GXBE-game1.ssx.gci   69-GXBE-settings.ssx.gci      two FILES, no directory
--
-- GameCube is what settles it: there is no directory that could be the unit root, because the
-- files sit in a region folder shared with every other game. So a save unit is a
-- (container, key) pair, and `relative_path` alone can no longer identify a row: every PSP
-- save on an install shares the container `saves/psp/SAVEDATA`.
--
-- 1. `unit_key`, and the UNIQUE moves to (relative_path, unit_key).
--
--    NOT NULL DEFAULT '' rather than nullable, and the reason is 006's own: SQLite treats
--    NULLs as distinct in a UNIQUE index, so a nullable column here would admit one duplicate
--    class A row per rescan, which is exactly the trap 006 avoided on `unsyncable.emulator`.
--    Class A and B carry '' because their unit is the file at `relative_path` itself.
--
--    The CHECK is name-shaped rather than path-shaped: a key is one segment read off a
--    directory or a filename (`UCES01011`, `1944`, `GXBE`, `RSBE`) and a separator in it would
--    mean the grammar matched something it should not have.
--
-- 2. `game_id_binding.learned_from` admits 'sidecar'.
--
--    Stage 2a recorded the save-state `.txt` sidecar's contents onto `local_state` and noted
--    that 2b should read it before building the ROM-header route. It should: measured, PPSSPP's
--    `3rd Birthday, The (Europe).txt` holds `ULES01513_1.00`, whose `ULES01513` prefix is the
--    key for `SAVEDATA/ULES01513SYSDATA` while the stem resolves through the same (folder, stem)
--    index class A uses. That reads no ROM and needs no observed launch.
--
--    It is a distinct provenance rather than a flavour of 'rom_header', because what it is
--    trusting is different: 'rom_header' trusts bytes in the game, 'sidecar' trusts a file
--    RetroBat wrote about the game. A reviewer deciding whether to keep a binding needs to know
--    which. Rebuilt rather than ALTERed, because the value list is a CHECK.
--
-- What this migration deliberately does NOT add, since 006's header set the precedent of
-- saying so:
--
--   * No column for the server's archive digest. Measured, RomM hashes an archive's contents
--     rather than its bytes, so its `content_hash` is not our logical fold and the two must
--     never be compared. But the value we have to send back is already persisted: it is
--     `save_slot.server_content_hash`, recorded from the upload response. A second copy on
--     `local_save` would be a second thing to keep in step.
--   * No column for the unit's member list. It is rediscovered by re-applying the grammar to
--     the container, which is what a rescan does anyway, and a stored list would go stale the
--     moment a game wrote a new savedata directory.
--   * Nothing for `outbox.batch_key`. Class C bundles to one archive and one row, so it never
--     supplies the second row a batch needs; the column keeps its schema and stays unwritten,
--     and the batch report is delivered from SaveSync instead.
--   * No new table at all. `game_id_binding` already had the right shape and a nullable
--     `rom_id`, which is what lets a contested key be recorded as investigated-and-unresolved
--     rather than re-investigated on every scan.

-- ---------------------------------------------------------------------------
-- local_save gains the unit key
-- ---------------------------------------------------------------------------

CREATE TABLE local_save_v2 (
  id                    INTEGER PRIMARY KEY,
  relative_path         TEXT    NOT NULL CHECK (
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

  -- '' for class A and B, whose unit is the file at relative_path. For class C it is the key
  -- read off the container's entries, which is a title id, a MAME short name, a GameCube game
  -- code or a Wii NAND code.
  unit_key              TEXT    NOT NULL DEFAULT '' CHECK (
                          unit_key NOT LIKE '%/%'
                          AND unit_key NOT LIKE '%\%'
                          AND unit_key NOT LIKE '%:%'
                          AND unit_key NOT LIKE '%' || char(10) || '%'
                        ),

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

  slot                  TEXT    NOT NULL CHECK (length(trim(slot)) > 0),

  -- For class A and B this is the MD5 of the file. For class C it is the logical fold over the
  -- unit's contents: sorted archive-relative paths plus each file's own digest. It is NOT what
  -- goes on the wire for class C, because RomM computes its own archive digest by a function
  -- this client cannot reproduce; that value lives on save_slot.server_content_hash.
  content_hash          TEXT    CHECK (content_hash IS NULL OR length(content_hash) = 32),
  size_bytes            INTEGER NOT NULL DEFAULT 0,

  file_mtime_utc        TEXT,
  scanned_at_utc        TEXT    NOT NULL,

  uploaded_content_hash TEXT    CHECK (uploaded_content_hash IS NULL OR length(uploaded_content_hash) = 32),
  uploaded_at_utc       TEXT,

  UNIQUE (relative_path, unit_key)
);

-- Copied even though every shipped build wrote only class A and B rows, which all take the
-- default key. Same discipline as 006's own outbox rebuild.
INSERT INTO local_save_v2 (
  id, relative_path, unit_key, system, emulator, shape_class, rom_id, rom_relative_path, slot,
  content_hash, size_bytes, file_mtime_utc, scanned_at_utc, uploaded_content_hash, uploaded_at_utc
)
SELECT
  id, relative_path, '', system, emulator, shape_class, rom_id, rom_relative_path, slot,
  content_hash, size_bytes, file_mtime_utc, scanned_at_utc, uploaded_content_hash, uploaded_at_utc
FROM local_save;

DROP TABLE local_save;

ALTER TABLE local_save_v2 RENAME TO local_save;

CREATE INDEX ix_local_save_rom ON local_save (rom_id);
CREATE INDEX ix_local_save_slot ON local_save (rom_id, slot);
CREATE INDEX ix_local_save_unsent ON local_save (rom_id, uploaded_content_hash);

-- "Every unit under this container", which is what a class C rescan asks before it can tell
-- which stored rows no longer exist on disk.
CREATE INDEX ix_local_save_container ON local_save (relative_path, unit_key);

-- ---------------------------------------------------------------------------
-- game_id_binding admits the sidecar route
-- ---------------------------------------------------------------------------

CREATE TABLE game_id_binding_v2 (
  id                INTEGER PRIMARY KEY,
  system            TEXT NOT NULL CHECK (
                      length(trim(system)) > 0
                      AND system NOT LIKE '%/%'
                      AND system NOT LIKE '%\%'
                    ),
  game_id           TEXT NOT NULL CHECK (
                      length(trim(game_id)) > 0
                      AND game_id NOT LIKE '%/%'
                      AND game_id NOT LIKE '%\%'
                      AND game_id NOT LIKE '%:%'
                    ),

  -- Nullable on purpose, and it is doing real work rather than being permissive. A row with a
  -- null rom_id records "this key was investigated and could not be bound": either nothing
  -- resolved it, or two routes disagreed and the fail-closed rule refused to pick. Without it
  -- a contested key is re-investigated on every scan and reported afresh each time.
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

  -- 'sidecar' is new. 'user' now has a caller too: `saves bind` is what unlearns a wrong
  -- binding, which is the answer this stage owes to "a wrong binding is permanent otherwise".
  learned_from      TEXT NOT NULL CHECK (learned_from IN ('journal', 'rom_header', 'sidecar', 'user')),

  -- Why the binding is what it is, or which routes disagreed when rom_id is null. Shown by
  -- `saves` so a user can see what a binding rests on before trusting or clearing it.
  detail            TEXT,

  learned_at        TEXT NOT NULL,
  UNIQUE (system, game_id)
);

INSERT INTO game_id_binding_v2 (
  id, system, game_id, rom_id, rom_relative_path, learned_from, detail, learned_at
)
SELECT id, system, game_id, rom_id, rom_relative_path, learned_from, NULL, learned_at
FROM game_id_binding;

DROP TABLE game_id_binding;

ALTER TABLE game_id_binding_v2 RENAME TO game_id_binding;

CREATE INDEX ix_game_id_binding_rom ON game_id_binding (rom_id);
