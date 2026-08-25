"""A1 and A2: what the rom id index and the total actually cost on GET /api/roms.

Argosy built `with_rom_id_index=false`, measured it as a two to four times regression
against a 23,873-rom library, and reverted it. RomMBat sends that parameter today.
Their measurement is always `platform_ids`-scoped; ours can page the whole library,
where sidecar memoisation applies and the index spans 83k ids rather than one platform.
So the flag is re-measured here, scoped and unscoped, rather than copied either way.

A2 rides along: `resolve_total()` returns len(rom_id_index) while the index is being
built, so `with_total=true` may be free with the index on and paid for with it off.

Read-only. Every request is a GET.
"""

from __future__ import annotations

import statistics
import sys

import _common

REPEATS = 3
LIMIT = 100
OFFSETS = [0, 1000, 3000, 6000]

# What CatalogQuery.ToQueryString sends today, minus the two flags under test.
BASE = {
    "with_char_index": "false",
    "with_filter_values": "false",
    "with_files": "false",
    "order_by": "id",
    "order_dir": "asc",
    "limit": LIMIT,
}


def time_page(params: dict) -> tuple[int, float, int, int | None]:
    """Returns (status, median seconds, body bytes, reported total)."""
    times: list[float] = []
    size = 0
    status = 0
    total = None
    for _ in range(REPEATS):
        status, _headers, payload, elapsed = _common.request("GET", "/api/roms", params=params)
        times.append(elapsed)
        size = len(payload)
        if status == 200:
            import json

            body = json.loads(payload)
            total = body.get("total")
    return status, statistics.median(times), size, total


def biggest_platform() -> tuple[int, str, int]:
    status, body, _ = _common.get_json("/api/platforms")
    if status != 200:
        raise SystemExit(f"GET /api/platforms answered {status}")
    ranked = sorted(body, key=lambda p: p.get("rom_count") or 0, reverse=True)
    top = ranked[0]
    return top["id"], top.get("slug") or top.get("name"), top.get("rom_count") or 0


def main() -> int:
    lines: list[str] = []
    status, heartbeat, _ = _common.get_json("/api/heartbeat")
    version = (heartbeat or {}).get("SYSTEM", {}).get("VERSION", "unknown")
    lines.append(f"server version: {version}")

    pid, slug, count = biggest_platform()
    lines.append(f"largest platform: {slug} (id {pid}), {count} roms")
    lines.append(f"median of {REPEATS}, limit={LIMIT}")
    lines.append("")

    for scope_name, scope in (("scoped (platform_ids)", {"platform_ids": pid}), ("unscoped", {})):
        lines.append(f"## {scope_name}")
        lines.append("")
        lines.append("| offset | index on | index off | total on | total off | bytes on | bytes off |")
        lines.append("| --- | --- | --- | --- | --- | --- | --- |")
        for offset in OFFSETS:
            if scope and offset >= count:
                continue
            row = [str(offset)]
            sizes = []
            totals = []
            for index_on in (True, False):
                params = dict(BASE)
                params.update(scope)
                params["offset"] = offset
                params["with_rom_id_index"] = "true" if index_on else "false"
                params["with_total"] = "true"
                st, secs, size, total = time_page(params)
                if st != 200:
                    row.append(f"HTTP {st}")
                    sizes.append("-")
                    totals.append("-")
                    continue
                row.append(f"{secs * 1000:.0f}ms")
                sizes.append(f"{size / 1024:.0f}K")
                totals.append(str(total))
            lines.append("| " + " | ".join(row + totals + sizes) + " |")
        lines.append("")

    # A2 on its own: is with_total free while the index is on, and paid for with it off?
    lines.append("## A2: what with_total costs, at offset 0, unscoped")
    lines.append("")
    lines.append("| index | with_total | median | total returned |")
    lines.append("| --- | --- | --- | --- |")
    for index_on in (True, False):
        for total_on in (True, False):
            params = dict(BASE)
            params["offset"] = 0
            params["with_rom_id_index"] = "true" if index_on else "false"
            params["with_total"] = "true" if total_on else "false"
            st, secs, _size, total = time_page(params)
            lines.append(
                f"| {'on' if index_on else 'off'} | {'on' if total_on else 'off'} | "
                f"{secs * 1000:.0f}ms | {total if st == 200 else f'HTTP {st}'} |"
            )

    _common.record("a1-a2-rom-id-index", lines)
    return 0


if __name__ == "__main__":
    sys.exit(main())
