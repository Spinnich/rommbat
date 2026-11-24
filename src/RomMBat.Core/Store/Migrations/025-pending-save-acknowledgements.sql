-- An acknowledgement can fail after verified bytes are installed.
CREATE TABLE pending_save_ack (
    server_origin TEXT NOT NULL,
    device_id TEXT NOT NULL,
    rom_id INTEGER NOT NULL,
    slot TEXT NOT NULL CHECK (length(slot) > 0),
    save_id INTEGER NOT NULL CHECK (save_id > 0),
    content_hash TEXT NOT NULL CHECK (length(content_hash) = 32),
    relative_path TEXT NOT NULL CHECK (
        length(relative_path) > 0 AND relative_path NOT LIKE '/%' AND relative_path NOT LIKE '%:%'
        AND instr(relative_path, char(92)) = 0 AND instr('/' || relative_path || '/', '/../') = 0
    ),
    server_updated_at TEXT,
    installed INTEGER NOT NULL CHECK (installed IN (0, 1)),
    PRIMARY KEY (server_origin, device_id, save_id)
);
