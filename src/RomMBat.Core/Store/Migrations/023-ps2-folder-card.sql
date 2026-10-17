-- Migration 023: PS2 saves on PCSX2's folder card, attributed by RomM's save_target.
--
-- 1. save_conversion and pending_config are dropped.
--
--    Both served the per-game memory card conversion: the first recorded a per-game key RomMBat
--    wrote and what it replaced, the second a key queued for the next EmulationStation quit.
--    RomMBat now writes one system-level setting instead, over any other value and with nothing
--    to put back, and it decides that from the file and the ROMs on disk each quit. Neither
--    table has a reader left, and the project is in alpha with nobody's conversion to carry.
--
-- 2. sync_set_member gains save_target.
--
--    RomM's own name for where a ROM's save lives: BASLUS-21050 for a PS2 game, the directory
--    PCSX2 names after the serial on the folder card. Carried per member like md5_hash, because
--    it is a server fact read on the walk that already fetches it, and the attribution route
--    that reads it runs inside a flush that may have no server at all. Nullable with no CHECK:
--    RomM leaves it null wherever it has not identified the ROM, and ADD COLUMN cannot carry a
--    CHECK SQLite would have to verify against existing rows.

DROP TABLE pending_config;
DROP TABLE save_conversion;

ALTER TABLE sync_set_member ADD COLUMN save_target TEXT;
