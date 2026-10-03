-- Migration 021: mark local_file rows whose rom_id belongs to a server this install left.
--
-- Pointing a paired install at another RomM must not carry the old server's rom ids into
-- anything that reads them: a rebuilt server reuses ids, so a save found through a stale id
-- would upload under another game. Clearing the rows instead would forget which files RomMBat
-- downloaded, and with that its ability to evict them or count them against the budget.
--
-- A stale row keeps its origin and its path and is invisible to every reader that resolves a
-- game by rom id. The next sync that finds the file writes the row afresh, which clears the flag.
--
-- NOT NULL DEFAULT 0: every existing row was recorded against the server it still names.

ALTER TABLE local_file ADD COLUMN stale INTEGER NOT NULL DEFAULT 0
  CHECK (stale IN (0, 1));
