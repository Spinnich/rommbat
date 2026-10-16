-- Migration 022: sync_set.scope_kind narrows to 'collection', 'smart_collection' and 'platform'.
--
-- A set of any other kind is deleted, with its membership and its walk cursor. Nothing on disk
-- is touched: a game such a set downloaded stays where it is and is treated by eviction like
-- any other download no set claims, and saves are never a set's to remove.
--
-- The members are deleted by hand rather than left to ON DELETE CASCADE, because the migration
-- runner turns foreign keys off for the duration, so the cascade does not fire and the
-- reference check after this script would refuse the orphans.
--
-- Rebuilt rather than ALTERed, because SQLite cannot change a CHECK on an existing column. The
-- surviving rows are copied by column, as every earlier rebuild of this table did.

DELETE FROM sync_set_member
WHERE sync_set_id IN (
  SELECT id FROM sync_set
  WHERE scope_kind NOT IN ('collection', 'smart_collection', 'platform')
);

-- A resolve's resumable walk is keyed by the set's id, as SetResolveService.EndpointFor writes it.
DELETE FROM sync_cursor
WHERE endpoint IN (
  SELECT 'roms:set:' || id FROM sync_set
  WHERE scope_kind NOT IN ('collection', 'smart_collection', 'platform')
);

DELETE FROM sync_set
WHERE scope_kind NOT IN ('collection', 'smart_collection', 'platform');

CREATE TABLE sync_set_v4 (
  id              INTEGER PRIMARY KEY,
  name            TEXT    NOT NULL UNIQUE,
  scope_kind      TEXT    NOT NULL CHECK (scope_kind IN (
                    'collection', 'smart_collection', 'platform'
                  )),

  -- A collection, smart-collection or platform id. Never a path.
  scope_value     TEXT    NOT NULL,
  max_games       INTEGER,
  max_bytes       INTEGER,
  ordering        TEXT    NOT NULL DEFAULT 'name',
  eviction_policy TEXT    NOT NULL DEFAULT 'keep_favourites',
  enabled         INTEGER NOT NULL DEFAULT 1,

  folder_override TEXT CHECK (
                    folder_override IS NULL OR (
                      length(trim(folder_override)) > 0
                      AND folder_override NOT LIKE '%/%'
                      AND folder_override NOT LIKE '%\%'
                      AND folder_override NOT LIKE '%:%'
                    )
                  ),

  last_resolved_at        TEXT,
  last_resolution_summary TEXT,

  created_at      TEXT    NOT NULL,
  updated_at      TEXT    NOT NULL
);

INSERT INTO sync_set_v4 (
  id, name, scope_kind, scope_value, max_games, max_bytes, ordering, eviction_policy,
  enabled, folder_override, last_resolved_at, last_resolution_summary, created_at, updated_at
)
SELECT
  id, name, scope_kind, scope_value, max_games, max_bytes, ordering, eviction_policy,
  enabled, folder_override, last_resolved_at, last_resolution_summary, created_at, updated_at
FROM sync_set;

DROP TABLE sync_set;

ALTER TABLE sync_set_v4 RENAME TO sync_set;
