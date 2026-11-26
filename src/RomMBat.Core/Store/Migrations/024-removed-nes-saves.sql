-- SaveScanner forgets missing files. Keep that fact separately so a session download
-- cannot put back a removed save, even if that save never reached RomM.
CREATE TABLE removed_nes_save (
    rom_id INTEGER NOT NULL PRIMARY KEY
);

CREATE TRIGGER remember_removed_nes_save
AFTER DELETE ON local_save
WHEN OLD.system = 'nes' AND OLD.slot = 'libretro:battery' AND OLD.rom_id IS NOT NULL
BEGIN
    INSERT OR IGNORE INTO removed_nes_save (rom_id) VALUES (OLD.rom_id);
END;
