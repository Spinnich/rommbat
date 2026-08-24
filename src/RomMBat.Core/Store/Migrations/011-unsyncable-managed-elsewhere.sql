-- Migration 011: a fifth reason a file under saves/ is not going up, for the case where
-- something other than RomMBat is already moving it.
--
-- The four existing reasons all describe a limitation of RomMBat: a shape it does not ship, a
-- path nothing declares, a container with no rom to belong to, a save it could not attribute.
-- None of them fits what M6 stage 2c measured about `dolphin_sync_saves`.
--
-- With that option on, emulatorlauncher reconciles
-- saves/gamecube/dolphin-emu/User/GC/<REGION>/ against a `Card A` subdirectory of it, once per
-- launch, before Dolphin starts. The files in `Card A` are ordinary per-game .gci saves. They
-- are not shared, RomMBat understands their shape perfectly well, and they can be attributed.
-- They are simply a second copy that RomMBat does not read, kept in step by somebody else.
--
-- Reporting those as 'shared_container' would be false in the plain sense of the word, and
-- 'unknown_shape' would be false twice over, since the shape is the one already being synced
-- three directory levels up. So the reason is its own.
--
-- **What the user needs told is not "these are skipped".** It is that a copy exists which
-- RomMBat neither uploads nor evicts, and that the copy can put a deleted save back. Driven on
-- a real install: with the option on, removing the region-root .gci and launching restored it
-- from `Card A`, holding the *previous* session's bytes, reported only as
-- "GameCube saves have been synced." The detail column carries that sentence, which is why it
-- is text and not an enum.
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
                    'managed_elsewhere'
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
