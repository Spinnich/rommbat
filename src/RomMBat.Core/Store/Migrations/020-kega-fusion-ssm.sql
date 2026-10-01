-- Migration 020: a battery save can be Kega Fusion's .ssm, in Kega's own folder.
--
-- RetroBat 8.2.1's template Fusion.ini sets SxMFiles to emulators\kega-fusion, and
-- emulatorLauncher never rewrites it, so Kega writes a Master System save as
-- emulators/kega-fusion/<rom>.ssm and reads it only from there (#381). That folder also holds
-- Fusion.exe, Fusion.ini, Plugins/ and every other system's .srm, so the CHECK admits only a
-- .ssm directly in it, not the folder.
--
-- Rebuilt as 019 built it, because SQLite cannot change a CHECK in place. Every row is copied.

CREATE TABLE local_save_v4 (
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
                            OR (
                              relative_path LIKE 'emulators/kega-fusion/%.ssm'
                              AND relative_path NOT LIKE 'emulators/kega-fusion/%/%'
                            )
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

INSERT INTO local_save_v4 (
  id, relative_path, unit_key, system, emulator, shape_class, rom_id, rom_relative_path, slot,
  content_hash, size_bytes, file_mtime_utc, scanned_at_utc, uploaded_content_hash, uploaded_at_utc
)
SELECT
  id, relative_path, unit_key, system, emulator, shape_class, rom_id, rom_relative_path, slot,
  content_hash, size_bytes, file_mtime_utc, scanned_at_utc, uploaded_content_hash, uploaded_at_utc
FROM local_save;

DROP TABLE local_save;

ALTER TABLE local_save_v4 RENAME TO local_save;

CREATE INDEX ix_local_save_rom ON local_save (rom_id);
CREATE INDEX ix_local_save_slot ON local_save (rom_id, slot);
CREATE INDEX ix_local_save_unsent ON local_save (rom_id, uploaded_content_hash);
CREATE INDEX ix_local_save_container ON local_save (relative_path, unit_key);
