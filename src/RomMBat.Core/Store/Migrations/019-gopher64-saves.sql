-- Migration 019: a battery save can live in gopher64's own portable folder.
--
-- local_save.relative_path was pinned under saves/, which is where every emulator RetroBat
-- configures keeps its saves, except gopher64 on n64: it writes them only to
-- emulators/gopher64/portable_data/data/saves/, and RetroBat 8.2.1 neither points them into
-- saves/n64/ nor mirrors them there (#239). The CHECK admits exactly that directory as well,
-- so a shape definition naming anywhere else is still refused, and a ROM or the database can
-- never be recorded as a save.
--
-- The '_' in portable_data is escaped, because LIKE reads it as any one character.
--
-- Rebuilt as 008 built it, because SQLite cannot change a CHECK in place. Every row is copied.

CREATE TABLE local_save_v3 (
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
                          AND (
                            relative_path LIKE 'saves/%'
                            OR relative_path LIKE 'emulators/gopher64/portable!_data/data/saves/%' ESCAPE '!'
                          )
                        ),

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

  content_hash          TEXT    CHECK (content_hash IS NULL OR length(content_hash) = 32),
  size_bytes            INTEGER NOT NULL DEFAULT 0,

  file_mtime_utc        TEXT,
  scanned_at_utc        TEXT    NOT NULL,

  uploaded_content_hash TEXT    CHECK (uploaded_content_hash IS NULL OR length(uploaded_content_hash) = 32),
  uploaded_at_utc       TEXT,

  UNIQUE (relative_path, unit_key)
);

INSERT INTO local_save_v3 (
  id, relative_path, unit_key, system, emulator, shape_class, rom_id, rom_relative_path, slot,
  content_hash, size_bytes, file_mtime_utc, scanned_at_utc, uploaded_content_hash, uploaded_at_utc
)
SELECT
  id, relative_path, unit_key, system, emulator, shape_class, rom_id, rom_relative_path, slot,
  content_hash, size_bytes, file_mtime_utc, scanned_at_utc, uploaded_content_hash, uploaded_at_utc
FROM local_save;

DROP TABLE local_save;

ALTER TABLE local_save_v3 RENAME TO local_save;

CREATE INDEX ix_local_save_rom ON local_save (rom_id);
CREATE INDEX ix_local_save_slot ON local_save (rom_id, slot);
CREATE INDEX ix_local_save_unsent ON local_save (rom_id, uploaded_content_hash);
CREATE INDEX ix_local_save_container ON local_save (relative_path, unit_key);
