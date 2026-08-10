"""F13 and F14: two ways an endpoint can answer without doing what you asked.

F14 — Freegosy sends `platform_id` to /api/roms in one code path and `platform_ids` in
another. Only the plural is declared at 5.1.0. If the server ignores the unknown singular
rather than rejecting it, a sync set scoped to one platform silently resolves to the whole
library, which is the worst possible failure for M2's budget.

F13 — /api/saves/identifiers takes no parameters, the same shape that made
/api/roms/identifiers answer 504 after 300 s on this library. Whether the saves sibling
scales decides if M6 can reconcile server-side deletions through it.
"""

from __future__ import annotations

from _common import get_json, record

lines: list[str] = []

# --- F14: unknown query parameters -----------------------------------------------------
status, platforms, _ = get_json("/api/platforms")
target = max(
    (p for p in platforms if (p.get("rom_count") or 0) > 0),
    key=lambda p: p.get("rom_count") or 0,
)
pid, slug, count = target["id"], target["fs_slug"], target["rom_count"]

lines.append(f"Reference platform: fs_slug={slug}  rom_count={count}")
lines.append("")

common = {
    "limit": 1,
    "with_char_index": "false",
    "with_filter_values": "false",
    "with_rom_id_index": "false",
}

for label, extra in [
    ("no scope at all", {}),
    ("platform_ids (declared)", {"platform_ids": pid}),
    ("platform_id (undeclared)", {"platform_id": pid}),
    ("not_a_real_parameter", {"not_a_real_parameter": "banana"}),
]:
    status, body, elapsed = get_json("/api/roms", params={**common, **extra})
    total = (body or {}).get("total")
    lines.append(f"GET /api/roms  {label}")
    lines.append(f"  status {status}   total={total}   {elapsed:.2f} s")

lines.append("")
lines.append(f"  the platform really holds {count} roms")
lines.append("")

# --- F13: /api/saves/identifiers --------------------------------------------------------
for path in ["/api/saves/identifiers", "/api/platforms/identifiers"]:
    try:
        status, body, elapsed = get_json(path, timeout=310)
        size = len(body) if isinstance(body, (list, dict)) else 0
        lines.append(f"GET {path}")
        lines.append(f"  status {status}   {elapsed:.2f} s   entries={size}")
    except Exception as err:  # noqa: BLE001
        lines.append(f"GET {path}")
        lines.append(f"  failed after the timeout: {type(err).__name__}")

record("f13-f14-endpoint-traps", lines)
