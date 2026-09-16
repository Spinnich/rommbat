"""R2: whether the scoped half of the with_rom_id_index rule still has a reason (#188).

CatalogQuery sends with_rom_id_index=true under a scope and false unscoped. The scoped half
was written from a 5.2.0 latency reading, 3.4 to 3.7x slower with the index off, and R1 and
the A1 re-run found that penalty gone on 5.3.0-alpha.2. What that re-run did not take is the
reading the decision needs: the page size the client actually sends, a scope wider than one
platform, and what with_total costs under a scope once the index is off.

What it takes, at limit=250 (RomPager.DefaultPageSize):

  per scope      the largest platform, the largest regular, smart and virtual collection,
                 index on against off, latency and body bytes, at offsets spread
                 over the scope, and whether total stays non-null with the index off
  interleaving   on and off alternate within each repeat, so a warm cache favours neither
  with_total     scoped, index on and off against with_total on and off, because RomPage.Total
                 is a non-nullable int and the server nulls it when both flags are off

Collections are ranked by rom_count and their rom_ids are never read. A collection's rom_count
is not what this account pages: a smart collection's criteria can resolve per user, so the
table's total column is the scope's real size and rom_count is only how it was chosen.

Read-only. Every request is a GET.
"""

from __future__ import annotations

import json
import statistics
import sys

import _common

REPEATS = 5
LIMIT = 250
OFFSET_FRACTIONS = [0.0, 0.33, 0.66]
VIRTUAL_TYPES = ["genre", "franchise", "company", "mode"]

# What CatalogQuery.ToQueryString sends, minus the two flags under test.
BASE = {
    "with_char_index": "false",
    "with_filter_values": "false",
    "with_files": "false",
    "order_by": "id",
    "order_dir": "asc",
    "limit": LIMIT,
}


def largest(path: str, label: str) -> tuple[str, int, int] | None:
    status, body, _ = _common.get_json(path, timeout=300.0)
    if status != 200 or not body:
        return None
    top = max(body, key=lambda item: item.get("rom_count") or 0)
    count = top.get("rom_count") or 0
    return (f"{label} {top['id']}", top["id"], count) if count else None


def page(params: dict) -> tuple[int, float, int, int | None]:
    status, _headers, payload, elapsed = _common.request("GET", "/api/roms", params=params, timeout=300.0)
    total = json.loads(payload).get("total") if status == 200 else None
    return status, elapsed, len(payload), total


def main() -> int:
    lines: list[str] = []
    _, heartbeat, _ = _common.get_json("/api/heartbeat")
    lines.append(f"server version: {(heartbeat or {}).get('SYSTEM', {}).get('VERSION', 'unknown')}")

    _, platforms, _ = _common.get_json("/api/platforms")
    top_platform = max(platforms, key=lambda p: p.get("rom_count") or 0)
    scopes = [
        (
            f"platform_ids {top_platform.get('slug')}",
            {"platform_ids": top_platform["id"]},
            top_platform.get("rom_count") or 0,
        )
    ]

    for path, key, label in (
        ("/api/collections", "collection_id", "collection"),
        ("/api/collections/smart", "smart_collection_id", "smart collection"),
    ):
        found = largest(path, label)
        if found is None:
            lines.append(f"no {label} with roms on this instance")
            continue
        name, ident, count = found
        scopes.append((name, {key: ident}, count))

    # A generated grouping spans platforms, so it is the widest scope a set can name.
    virtual: list[tuple[str, str, int]] = []
    for kind in VIRTUAL_TYPES:
        status, body, _ = _common.get_json("/api/collections/virtual", params={"type": kind, "limit": 1000})
        if status == 200 and body:
            top = max(body, key=lambda item: item.get("rom_count") or 0)
            virtual.append((f"virtual {kind} '{top.get('name')}'", top["id"], top.get("rom_count") or 0))
    if virtual:
        name, ident, count = max(virtual, key=lambda item: item[2])
        scopes.append((name, {"virtual_collection_id": ident}, count))

    lines.append(f"limit={LIMIT}, median of {REPEATS}, index on and off interleaved within each repeat")
    lines.append("")
    lines.append("| scope | roms | offset | on ms | off ms | on KiB | off KiB | saved KiB | total on | total off |")
    lines.append("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |")

    for name, scope, count in scopes:
        for fraction in OFFSET_FRACTIONS:
            offset = (int(count * fraction) // LIMIT) * LIMIT
            if fraction and offset == 0:
                continue
            times = {True: [], False: []}
            sizes = {True: 0, False: 0}
            totals = {True: None, False: None}
            failed = None
            for repeat in range(REPEATS):
                order = (True, False) if repeat % 2 == 0 else (False, True)
                for index_on in order:
                    params = dict(BASE, **scope, offset=offset, with_total="true")
                    params["with_rom_id_index"] = "true" if index_on else "false"
                    status, secs, size, total = page(params)
                    if status != 200:
                        failed = status
                        break
                    times[index_on].append(secs)
                    sizes[index_on] = size
                    totals[index_on] = total
                if failed:
                    break
            if failed:
                lines.append(f"| {name} | {count} | {offset} | HTTP {failed} | | | | | | |")
                continue
            on_ms = statistics.median(times[True]) * 1000
            off_ms = statistics.median(times[False]) * 1000
            lines.append(
                f"| {name} | {count} | {offset} | {on_ms:.0f} | {off_ms:.0f} | "
                f"{sizes[True] / 1024:.0f} | {sizes[False] / 1024:.0f} | "
                f"{(sizes[True] - sizes[False]) / 1024:.0f} | {totals[True]} | {totals[False]} |"
            )

    lines.append("")
    lines.append(f"## with_total under a scope, offset 0, median of {REPEATS}")
    lines.append("")
    lines.append("| scope | index | with_total | median ms | total returned |")
    lines.append("| --- | --- | --- | --- | --- |")
    for name, scope, _count in scopes:
        results: dict[tuple[bool, bool], list[float]] = {}
        returned: dict[tuple[bool, bool], object] = {}
        combos = [(True, True), (True, False), (False, True), (False, False)]
        for repeat in range(REPEATS):
            for index_on, total_on in combos if repeat % 2 == 0 else list(reversed(combos)):
                params = dict(BASE, **scope, offset=0)
                params["with_rom_id_index"] = "true" if index_on else "false"
                params["with_total"] = "true" if total_on else "false"
                status, secs, _size, total = page(params)
                results.setdefault((index_on, total_on), []).append(secs)
                returned[(index_on, total_on)] = total if status == 200 else f"HTTP {status}"
        for index_on, total_on in combos:
            lines.append(
                f"| {name} | {'on' if index_on else 'off'} | {'on' if total_on else 'off'} | "
                f"{statistics.median(results[(index_on, total_on)]) * 1000:.0f} | "
                f"{returned[(index_on, total_on)]} |"
            )

    _common.record("r2-scoped-index-bandwidth", lines)
    return 0


if __name__ == "__main__":
    sys.exit(main())
