"""R1: the paging timings this repo measured at 5.2.0, re-taken at 5.3.0-alpha.2.

5.3.0 changes the defaults every one of those numbers was measured under: SCAN_WORKERS
and WEB_SERVER_CONCURRENCY both default to 4 where they were 1, an N+1 in GET /api/roms
is fixed, and pooled connections are recycled before the server drops them.

The 5.2.0 readings do not get rewritten. A measurement carries the version it was taken
on, so this writes a second set beside them rather than over them. Two things differ
between the passes and both belong in the comparison: the server version, and the
library, which was 88,331 roms then and is larger now.

What it re-takes:

  the full platform-scoped walk  a resolve of the largest platform at 250 rows a page,
                                 8 m 15 s at 5.2.0 with the rom id index off, which is
                                 what #88 was
  the same walk with the index   the rule in the romm-api skill is that a scoped request
                                 keeps it on, because turning it off cost 3.4 to 3.7x
  the unscoped page cost         RomPager's own comment records 2.5 s a page

Read-only. Every request is a GET.
"""

from __future__ import annotations

import json
import statistics
import sys
import time

import _common

PAGE = 250
UNSCOPED_SAMPLES = 8

# What CatalogQuery.ToQueryString sends, minus the index flag under test.
BASE = {
    "with_char_index": "false",
    "with_filter_values": "false",
    "with_files": "false",
    "with_total": "true",
    "order_by": "id",
    "order_dir": "asc",
    "limit": PAGE,
}


def largest_platform() -> tuple[int, str, int]:
    status, platforms, _elapsed = _common.get_json("/api/platforms")
    if status != 200 or not platforms:
        raise SystemExit(f"GET /api/platforms answered {status}")
    widest = max(platforms, key=lambda p: p.get("rom_count") or 0)
    return widest["id"], widest["fs_slug"], widest["rom_count"]


def walk(params: dict, expected: int) -> tuple[float, int, list[float], int]:
    """Pages to exhaustion. Returns (wall seconds, pages, per-page times, rows seen)."""
    offset = 0
    rows = 0
    pages: list[float] = []
    started = time.monotonic()
    while offset < expected:
        status, _headers, payload, elapsed = _common.request(
            "GET", "/api/roms", params={**params, "offset": offset}
        )
        if status != 200:
            raise SystemExit(f"page at offset {offset} answered {status}")
        pages.append(elapsed)
        batch = json.loads(payload).get("items", [])
        if not batch:
            break
        rows += len(batch)
        offset += PAGE
    return time.monotonic() - started, len(pages), pages, rows


def clock(seconds: float) -> str:
    return f"{int(seconds // 60)} m {seconds % 60:04.1f} s"


def main() -> int:
    platform_id, slug, count = largest_platform()
    status, heartbeat, _elapsed = _common.get_json("/api/heartbeat")
    version = (heartbeat or {}).get("SYSTEM", {}).get("VERSION", "unknown")

    lines = [
        "# R1: paging, re-measured",
        "",
        f"Server reports `{version}` at /api/heartbeat. Largest platform is `{slug}` at "
        f"{count} roms; the library holds {{library}} roms.",
        "",
        f"Page size {PAGE}, `order_by=id&order_dir=asc`, the flags CatalogQuery sends.",
        "",
        "## The full scoped walk",
        "",
        "| rom id index | pages | wall | median page | slowest page |",
        "| --- | --- | --- | --- | --- |",
    ]

    scoped = {**BASE, "platform_ids": platform_id}
    library = None
    for label, index in (("on", "true"), ("off", "false")):
        wall, pages, times, rows = walk({**scoped, "with_rom_id_index": index}, count)
        lines.append(
            f"| {label} | {pages} | {clock(wall)} | "
            f"{statistics.median(times) * 1000:.0f}ms | {max(times) * 1000:.0f}ms |"
        )
        print(f"  scoped index {label}: {clock(wall)} over {pages} pages, {rows} rows", file=sys.stderr)

    lines += [
        "",
        "## The unscoped page, sampled rather than walked",
        "",
        f"{UNSCOPED_SAMPLES} pages spread across the library.",
        "",
        "| rom id index | median | slowest |",
        "| --- | --- | --- |",
    ]

    status, first, _elapsed = _common.get_json("/api/roms", params={**BASE, "limit": 1})
    library = (first or {}).get("total")
    step = max(1, ((library or 0) - PAGE) // UNSCOPED_SAMPLES)
    for label, index in (("on", "true"), ("off", "false")):
        times = []
        for sample in range(UNSCOPED_SAMPLES):
            _status, _headers, _payload, elapsed = _common.request(
                "GET",
                "/api/roms",
                params={**BASE, "with_rom_id_index": index, "offset": sample * step},
            )
            times.append(elapsed)
        lines.append(
            f"| {label} | {statistics.median(times) * 1000:.0f}ms | {max(times) * 1000:.0f}ms |"
        )

    lines[2] = lines[2].replace("{library}", str(library))
    _common.record("r1-page-walk", lines)
    return 0


if __name__ == "__main__":
    sys.exit(main())
