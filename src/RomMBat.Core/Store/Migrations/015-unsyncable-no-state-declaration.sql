-- Migration 015: a sixth reason a file under saves/ is not going up, for the case where
-- es_savestates.cfg declares no save-state directory at all.
--
-- The five existing reasons all describe a save shape: one this build does not ship, one
-- nothing declares, a container with no rom to belong to, a save nothing could attribute, a
-- copy somebody else is keeping in step. The state half has a failure of its own that none of
-- them names.
--
-- An emulator with no <emulator> entry in es_savestates.cfg still writes save states, into a
-- directory it names itself. Driven on nes against a real 8.2.1 install: mednafen wrote
-- saves/nes/mednafen/sstates/<rom>.<md5>.mc0, mesen wrote saves/nes/mesen/SaveStates/<rom>_1.mss
-- and ares wrote saves/nes/ares/Famicom/<rom>.bs1, each carrying real state data. StateScanner
-- works from es_savestates.cfg alone, so none of the three is scanned, uploaded or restorable.
-- 30 of wave 1's 81 rows are in that family. See #150.
--
-- They were already counted, because SaveScanner.CountFiles excludes only the directories
-- es_savestates.cfg *declares*. What was wrong is the sentence they were counted under, which
-- told the reader "this release syncs the save states beside them". Being told a number that
-- silently includes your save states, under a promise that save states sync, is the same
-- defect as saying nothing, pointed the other way.
--
-- Separate from 'not_in_this_version' because the fix is different in kind. A deferred shape
-- is understood and waiting on work here; this is a declaration RetroBat does not make, and
-- closing it needs a bundled supplement carrying the directory, filename and slot per row with
-- its own provenance. Keying on the reason rather than on the wording of the detail column is
-- what lets a platform record say which of the two a row is in.
--
-- SQLite cannot widen a CHECK in place, so the table is rebuilt. It is a report rather than a
-- record of anything, rewritten wholesale on every scan, so the copy is for tidiness rather
-- than for safety: losing a row here costs one scan.

CREATE TABLE unsyncable_new (
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
                    'unattributed',
                    -- Understood, attributable, and already being kept in step by something
                    -- else. RomMBat leaves it alone rather than fighting the other writer.
                    'managed_elsewhere',
                    -- No es_savestates.cfg entry declares a save-state directory here, so state
                    -- discovery never looks, whatever the emulator writes.
                    'no_state_declaration'
                  )),
  detail          TEXT    NOT NULL CHECK (length(trim(detail)) > 0),
  file_count      INTEGER NOT NULL DEFAULT 0,
  observed_at_utc TEXT    NOT NULL,
  UNIQUE (system, emulator, reason_kind)
);

INSERT INTO unsyncable_new (id, system, emulator, reason_kind, detail, file_count, observed_at_utc)
SELECT id, system, emulator, reason_kind, detail, file_count, observed_at_utc FROM unsyncable;

DROP TABLE unsyncable;

ALTER TABLE unsyncable_new RENAME TO unsyncable;
