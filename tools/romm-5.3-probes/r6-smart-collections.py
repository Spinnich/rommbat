"""R6: whether a smart collection's rom_count is what this account pages back (#193).

R2 found the account's largest smart collection advertising 594 roms and paging back a total
of 0. Read in source at 5.3.0-alpha.2, the two numbers come from different users.
`SmartCollection.rom_count` and `rom_ids` are stored columns, recomputed on write by
`refresh_smart_collection` with the **owner's** user id, and its docstring says they "describe
the owner's view, since the row is shared and criteria like `favorite` or `has_saves` answer
differently per user". `GET /api/roms?smart_collection_id=` applies the same criteria with the
**caller's** id, and hides the caller's hidden roms. `GET /api/collections/smart` lists the
caller's own collections and every public one.

So for every smart collection this account can list it records the owner, whether it is
public, which criteria it carries and which of those are per user, the stored rom_count
and rom_ids, and what paging it back as this account answers: the total, and how the paged
ids compare with the stored ones.

Read-only. Every request is a GET.
"""

from __future__ import annotations

import sys

import _common

# Criteria filter_roms resolves through RomUser, so their answer depends on who asks.
PER_USER = {"favorite", "has_saves", "has_states", "last_played", "statuses", "selected_status"}


def main() -> int:
    version = _common.server_version()
    status, me, _ = _common.get_json("/api/users/me")
    if status != 200 or not me:
        raise SystemExit(f"GET /api/users/me answered {status}")

    status, listed, _ = _common.get_json("/api/collections/smart", timeout=300.0)
    if status != 200 or listed is None:
        raise SystemExit(f"GET /api/collections/smart answered {status}")

    lines = [
        "# R6: smart collections, stored count against what this account pages",
        "",
        f"Server reports `{version}`. This account lists {len(listed)} smart collections.",
        "",
        "| id | owner | public | criteria | per user | rom_count | rom_ids | paged total | "
        "paged, stored | paged only | stored only | updated |",
        "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |",
    ]

    for collection in sorted(listed, key=lambda c: -(c.get("rom_count") or 0)):
        criteria = collection.get("filter_criteria") or {}
        carried = sorted(key for key, value in criteria.items() if value not in (None, "", [], {}, False))
        per_user = sorted(set(carried) & PER_USER)
        stored = set(collection.get("rom_ids") or [])

        paged = {row["id"] for row in _common.walk({"smart_collection_id": collection["id"]})}
        _s, first, _e = _common.get_json(
            "/api/roms",
            params={**_common.WALK, "smart_collection_id": collection["id"], "limit": 1},
            timeout=300.0,
        )
        total = (first or {}).get("total")

        owner = "this account" if collection.get("user_id") == me.get("id") else "another account"
        lines.append(
            f"| {collection['id']} | {owner} | {collection.get('is_public')} | "
            f"{', '.join(carried) or '(none)'} | {', '.join(per_user) or '(none)'} | "
            f"{collection.get('rom_count')} | {len(stored)} | {total} | {len(paged & stored)} | "
            f"{len(paged - stored)} | {len(stored - paged)} | {collection.get('updated_at')} |"
        )

    lines += [
        "",
        "Criteria are listed by key only. `last_played` is per user through `RomUser`, and every",
        "scoped page also drops the caller's own hidden roms, which no criterion names.",
    ]

    _common.record("r6-smart-collections", lines)
    return 0


if __name__ == "__main__":
    sys.exit(main())
