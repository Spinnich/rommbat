-- Migration 012: a configuration change RomMBat intends to make but cannot make yet, and what
-- happened when it finally did.
--
-- `saves convert --apply` refuses while EmulationStation is running, because ES loads
-- es_settings.cfg at startup and serialises that model on every write, so a key that appears
-- afterwards is discarded (findings 178 and 179). That refusal is correct and it is also a
-- dead end for the thing M7 is building: **the UI is launched from the ES menu, so it always
-- runs under a live ES.** It can never write that file. Without somewhere to put the intent,
-- the only per-game setting RomMBat has is unreachable from the only interface it ships.
--
-- So the intent is recorded here, and `background quit` drains it once ES is confirmed gone.
--
-- 1. Why this is not a state column on `save_conversion`.
--
--    That table records something RomMBat **did**, and three of its properties make it unable
--    to carry something RomMBat intends to do.
--
--      - prior_state and prior_value are only knowable at the instant of writing, and a CHECK
--        enforces that one is present exactly when the other is. A queued row cannot know
--        either: ES writes es_settings.cfg twice a session, at launch and on exit, so what the
--        file holds at queue time says nothing about what it holds when the change is applied.
--        Two columns constrained to be truthful would have to be filled with a guess.
--      - Its UNIQUE (system, fs_name, setting_key) is right for "what is set right now" and
--        wrong here, because queueing a revert of an applied conversion needs a live row and a
--        pending row on the same triple at the same time.
--      - Its rows are dropped on revert, deliberately and with the reason recorded, because
--        once the setting is back there is nothing left to reverse. A queue whose history is
--        deleted cannot answer the question this table exists for.
--
--    So the two are kept apart and the seam between them is one direction only: applying a row
--    here goes through the existing SaveConverter, which writes save_conversion exactly as a
--    conversion at the terminal does. There is one writer of that table, not two.
--
-- 2. Why the result is kept rather than the row deleted.
--
--    The UI in 7b is not running when the change is applied. It is launched from the ES menu,
--    so it is gone before the quit hook fires, and the next time a person sees it the apply
--    has already happened. If the row were deleted on success the UI could only say "it is
--    not queued any more", which reads the same as never having been queued and the same as
--    having been refused. So applied_at_utc, result and detail stay, and a cancelled change
--    is the one case that deletes: nothing happened, so there is nothing to report.
--
-- 3. Why one outstanding change per target, but any number of finished ones.
--
--    A partial UNIQUE index over the rows that have not applied. Queueing the same target
--    twice is a user changing their mind, which should replace rather than accumulate, and
--    two contradictory pending rows for one key would apply in an order nothing defines.
--    Finished rows are history and several are expected.
--
-- 4. desired_state carries 'remove' as a first-class case, not as a NULL value.
--
--    Reverting a conversion means putting the key back to its prior state, and finding 170
--    established that "absent" and "present at the stock value" are different files to
--    restore. A queued revert of a conversion whose prior state was absent must remove the
--    key, and a NULL desired_value on its own cannot be told from a bug.
--
-- Created rather than rebuilt: it is a new table and nothing has ever written one of these.

CREATE TABLE pending_config (
  id                INTEGER PRIMARY KEY,

  rom_id            INTEGER NOT NULL,

  system            TEXT    NOT NULL CHECK (
                      length(trim(system)) > 0
                      AND system NOT LIKE '%/%'
                      AND system NOT LIKE '%\%'
                      AND system NOT LIKE '%:%'
                    ),

  -- The rom's filename with its extension, never a stem and never a path. Same rule and same
  -- reason as save_conversion: a per-game key built from a stem is ignored silently, and the
  -- emulator then carries on writing to the shared container with nothing to show for it.
  fs_name           TEXT    NOT NULL CHECK (
                      length(trim(fs_name)) > 0
                      AND fs_name NOT LIKE '%/%'
                      AND fs_name NOT LIKE '%\%'
                      AND fs_name NOT LIKE '%:%'
                      AND fs_name NOT LIKE '%' || char(10) || '%'
                      AND fs_name <> '..'
                      AND fs_name LIKE '%.%'
                    ),

  -- The bare es_settings.cfg option, e.g. pcsx2_slot1_memory. Not the full per-game key,
  -- which is derivable and would let the two disagree.
  setting_key       TEXT    NOT NULL CHECK (
                      length(trim(setting_key)) > 0
                      AND setting_key NOT LIKE '%[%'
                      AND setting_key NOT LIKE '%"%'
                      AND setting_key NOT LIKE '%.%'
                    ),

  desired_state     TEXT    NOT NULL CHECK (desired_state IN ('set', 'remove')),
  desired_value     TEXT,

  -- What the user asked for, in their words rather than in keys, because this is what the UI
  -- shows them weeks later when they have forgotten queueing it.
  reason            TEXT    NOT NULL CHECK (length(trim(reason)) > 0),

  queued_at_utc     TEXT    NOT NULL,

  -- Null until background quit gets to it. Set together with result, always.
  applied_at_utc    TEXT,
  result            TEXT    CHECK (result IN ('applied', 'refused', 'failed')),
  detail            TEXT,

  CHECK (
    (desired_state = 'set'    AND desired_value IS NOT NULL AND length(trim(desired_value)) > 0)
    OR
    (desired_state = 'remove' AND desired_value IS NULL)
  ),

  -- A row is either outstanding or finished. Half of each is a row no reader can classify.
  CHECK (
    (applied_at_utc IS NULL     AND result IS NULL)
    OR
    (applied_at_utc IS NOT NULL AND result IS NOT NULL)
  )
);

-- One outstanding change per target; finished rows are history and may repeat.
CREATE UNIQUE INDEX ux_pending_config_outstanding
  ON pending_config (system, fs_name, setting_key)
  WHERE applied_at_utc IS NULL;

CREATE INDEX ix_pending_config_rom ON pending_config (rom_id);
