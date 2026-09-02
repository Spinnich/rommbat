-- Migration 014: sync_set.scope_kind gains 'picked', so a hand-picked set can be a set.
--
-- What the existing shape could not carry is a set whose scope is a list of games rather than
-- a query. Every one of the five kinds 002 declared resolves the same way, by paging
-- GET /api/roms with the scope as a query parameter, and a hand-picked set cannot: **/api/roms
-- has no id-list parameter.** Verified against the pinned romm-5.2.0.json, whose scoping
-- parameters are platform_ids, collection_id, virtual_collection_id and smart_collection_id.
-- So this is a property of the scope rather than a defect to work around, and it is why
-- CatalogQuery.ToQueryString refuses a picked scope outright instead of silently paging the
-- whole library.
--
-- The three shapes this was not given, and why each is worse:
--
-- 1. An id list smuggled inside a 'filter' scope. That overloads one column with two meanings,
--    so every reader of scope_value has to guess which it is holding, and SetResolver would
--    walk the library for a set whose membership is already known.
--
-- 2. An unmanaged download outside any set. That means storing "this orphan is deliberate" and
--    teaching EvictionPlanner to recognise it, which is a set by another name with none of a
--    set's machinery: it would not list, sync, roam, evict or delete like one.
--
-- 3. A new column holding the ids. For this scope the id list **is** the definition, exactly as
--    a filter's JSON is, so scope_value is where it belongs. A second column would be null on
--    five kinds out of six and would have to roam separately.
--
-- No column is added and no column holds an absolute path: the value written here is a JSON
-- array of RomM rom ids, which are integers.
--
-- Rebuilt rather than ALTERed, because SQLite cannot change a CHECK on an existing column.
-- Rows are copied even though the constraint only widens and no existing row can fail it,
-- because assuming a table is empty is how data goes missing on someone's stick. The migration
-- runner turns foreign keys off for the duration, since dropping a parent with them on runs an
-- implicit DELETE that cascades into sync_set_member.
--
-- sync_set_member is untouched. A picked set's members are written from the browse row that
-- was in hand at the moment of the pick, which already carries every field that table wants,
-- so nothing about membership changes shape.

CREATE TABLE sync_set_v3 (
  id              INTEGER PRIMARY KEY,
  name            TEXT    NOT NULL UNIQUE,
  scope_kind      TEXT    NOT NULL CHECK (scope_kind IN (
                    'collection', 'smart_collection', 'virtual_collection', 'platform',
                    'filter', 'picked'
                  )),

  -- A collection, smart-collection or platform id, a virtual collection's string id, filter
  -- JSON, or, for 'picked', a JSON array of rom ids. Never a path, on any kind.
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

INSERT INTO sync_set_v3 (
  id, name, scope_kind, scope_value, max_games, max_bytes, ordering, eviction_policy,
  enabled, folder_override, last_resolved_at, last_resolution_summary, created_at, updated_at
)
SELECT
  id, name, scope_kind, scope_value, max_games, max_bytes, ordering, eviction_policy,
  enabled, folder_override, last_resolved_at, last_resolution_summary, created_at, updated_at
FROM sync_set;

DROP TABLE sync_set;

ALTER TABLE sync_set_v3 RENAME TO sync_set;
