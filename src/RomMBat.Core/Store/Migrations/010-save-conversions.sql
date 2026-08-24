-- Migration 010: remember which games RomMBat opted into a per-game save container, and what
-- was there before.
--
-- Every table shipped so far describes something RomMBat observed: a file on disk, a slot the
-- server holds, a launch, a binding it learned. This is the first that records something
-- RomMBat *did to the user's machine*, and specifically to a file RomMBat does not own. Class
-- D conversion writes `<system>["<rom filename>"].<key>` into es_settings.cfg, and the rule
-- governing it is that it is opt-in, explained and **reversible**. Nothing existing can carry
-- that, and the two candidates are worth naming:
--
--   local_save   is about bytes on disk. A conversion changes no file under saves/ at the
--                moment it is applied; it changes where an emulator will write next time.
--   save_slot    is the server-side identity of a negotiated slot. A conversion never reaches
--                the server at all, and must not: it is a local operation that has to work
--                with the network down.
--
-- 1. The prior state, which is what reversibility actually needs, and it is two columns.
--
--    "The key was absent" and "the key was set to the stock value" are different prior states
--    and restoring the wrong one leaves the user somewhere they never were. They cannot be
--    told apart by reading the file later, and M6 stage 2c measured why the obvious shortcuts
--    fail in both directions:
--
--      - ES prunes a setting whose value equals its own default (M0), so a key written at the
--        stock value disappears on ES's next write. Absence is not evidence of a revert.
--      - ES also ADDS keys on its own. `Language` was absent from a real install's file on one
--        day and present at `en_US` the next, on two independent installs, written by ES at
--        startup with nothing else changed (finding 170). **So presence is not evidence
--        either**, and "the key holds the stock value" must never be read as "the user chose
--        this". A user browsing PCSX2's options in the ES menu can materialise the key without
--        intending anything by it.
--
--    Hence prior_state is recorded explicitly at the moment of conversion, with prior_value
--    NULL exactly when prior_state is 'absent', enforced rather than trusted.
--
-- 2. applied_value, so "has someone changed this since" is answerable.
--
--    The rule is that RomMBat must not take over a setting the user made themselves. Given
--    presence proves nothing about authorship, the only sound test is whether the file still
--    holds the value RomMBat wrote. If it does not, something else changed it and the
--    conversion is reported rather than silently re-applied or silently reverted.
--
-- 3. UNIQUE on (system, fs_name, setting_key) rather than on rom_id.
--
--    That triple is the key emulatorlauncher itself reads. rom_id is carried because the guard
--    and the report join on it, but it cannot be the uniqueness key: the setting is a property
--    of a filename in a system folder, and it is that filename the emulator matches on.
--
--    fs_name is name-shaped, not path-shaped, and the CHECK says so. It is the rom's file as
--    it sits on disk, extension included, because a per-game key built from a stem is ignored
--    silently by emulatorlauncher and the emulator then carries on writing to the shared
--    container with nothing to show anything went wrong.
--
-- A reverted conversion deletes its row rather than keeping a tombstone. Once the setting is
-- back at its prior state there is nothing left to reverse, and no reader has been identified
-- that would want the history. The per-game container the conversion produced stays on disk
-- and keeps syncing, which is a decision taken deliberately: it holds real progress, and where
-- it came from is evident from where it sits rather than from a row here.
--
-- Created rather than rebuilt, because it is a new table and nothing has ever written one of
-- these rows. There is no data to copy.

CREATE TABLE save_conversion (
  id                INTEGER PRIMARY KEY,

  rom_id            INTEGER NOT NULL,

  system            TEXT    NOT NULL CHECK (
                      length(trim(system)) > 0
                      AND system NOT LIKE '%/%'
                      AND system NOT LIKE '%\%'
                      AND system NOT LIKE '%:%'
                    ),

  -- The rom's filename with its extension, never a stem and never a path.
  fs_name           TEXT    NOT NULL CHECK (
                      length(trim(fs_name)) > 0
                      AND fs_name NOT LIKE '%/%'
                      AND fs_name NOT LIKE '%\%'
                      AND fs_name NOT LIKE '%:%'
                      AND fs_name NOT LIKE '%' || char(10) || '%'
                      AND fs_name <> '..'
                      -- Must carry an extension. A per-game key built from a stem is ignored
                      -- silently, so a stem reaching this table is the bug that costs a save.
                      AND fs_name LIKE '%.%'
                    ),

  -- The es_settings.cfg option, e.g. pcsx2_slot1_memory. Not the full per-game key, which is
  -- derivable from system, fs_name and this, and storing a derived value invites the two to
  -- disagree.
  setting_key       TEXT    NOT NULL CHECK (
                      length(trim(setting_key)) > 0
                      AND setting_key NOT LIKE '%[%'
                      AND setting_key NOT LIKE '%"%'
                      AND setting_key NOT LIKE '%.%'
                    ),

  applied_value     TEXT    NOT NULL CHECK (length(trim(applied_value)) > 0),

  prior_state       TEXT    NOT NULL CHECK (prior_state IN ('absent', 'present')),
  prior_value       TEXT,

  converted_at_utc  TEXT    NOT NULL,

  -- Absent means there was no value; present means there was one. Anything else is a row that
  -- cannot be reverted correctly, so it is refused at write time rather than discovered later.
  CHECK (
    (prior_state = 'absent'  AND prior_value IS NULL)
    OR
    (prior_state = 'present' AND prior_value IS NOT NULL)
  ),

  UNIQUE (system, fs_name, setting_key)
);

CREATE INDEX ix_save_conversion_rom ON save_conversion (rom_id);
