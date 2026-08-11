"""M4 probe 3: what metadata for the locally present ROMs costs.

Two shapes are on offer and the answer decides whether M4 makes N requests or none at all.

Per ROM: GET /api/roms/{id} returns DetailedRomSchema, which is the same family as the
GET /api/collections trap under core principle 2.

Per page: GET /api/roms already returns SimpleRomSchema, which carries every field M4
wants. M2's walk fetches those pages and its slim RomRow throws the metadata away, so the
bytes are already paid for.

Measured on the worst ROM findable rather than a quiet one.
"""

from __future__ import annotations

import json
import statistics
import time

import _common as c

PAGE = 250
SCAN_PAGES = 8
REPEATS = 3

# Every field of SimpleRomSchema M4 would read, which is what a page has to carry.
WANTED = (
    "id",
    "name",
    "summary",
    "metadatum",
    "path_cover_small",
    "path_cover_large",
    "path_manual",
    "path_video",
    "regions",
    "languages",
    "fs_name",
)


def timed_get(path: str, **kwargs):
    best = []
    payload = None
    status = None
    for _ in range(REPEATS):
        started = time.monotonic()
        status, _headers, body, _elapsed = c.request("GET", path, **kwargs)
        best.append(time.monotonic() - started)
        payload = body
    return status, payload, statistics.median(best)


def worst_roms(lines: list[str]) -> list[dict]:
    """The heaviest ROMs the sample offers, by the arrays DetailedRomSchema inflates."""
    candidates: list[dict] = []
    status, first, _ = c.page(1, 0)
    total = first["total"] if status == 200 else 0
    offsets = [round(i * (total - PAGE) / (SCAN_PAGES - 1)) for i in range(SCAN_PAGES)]

    for offset in offsets:
        # with_files is opt-in, so a row's files[] is empty without it and "the ROM with the
        # most files" would otherwise be whichever one came first.
        status, body, _ = c.page(PAGE, offset, with_files="true")
        if status == 200:
            candidates.extend(body["items"])

    lines.append(f"scanned {len(candidates):,} rows (with_files=true) for the heaviest ROMs")

    picked: list[dict] = []
    picked.append(("most siblings", max(candidates, key=lambda row: len(row.get("sibling_roms") or []))))
    picked.append(("most files", max(candidates, key=lambda row: len(row.get("files") or []))))
    picked.append(("longest summary", max(candidates, key=lambda row: len(row.get("summary") or ""))))

    # A ROM with saves is what makes all_user_saves and all_user_states non-empty, and that is
    # the whole reason DetailedRomSchema could be a different cost from SimpleRomSchema. Both
    # counts are reported even when zero, because zero is the measurement.
    for label, flag in (("has saves", "has_saves"), ("has states", "has_states")):
        status, body, _ = c.page(5, 0, **{flag: "true"})
        found = body["total"] if status == 200 else -1
        lines.append(f"  {flag}=true matches {found:,} roms for this account")
        if status == 200 and body["items"]:
            picked.append((label, body["items"][0]))

    return picked


def main() -> None:
    lines = ["M4 probe 3: the cost of metadata for the ROMs that are present", ""]

    picked = worst_roms(lines)
    lines.append("")
    lines.append("== per ROM: GET /api/roms/{id} (DetailedRomSchema) against /simple")
    lines.append(
        f"  {'which':<14} {'id':>7} {'siblings':>9} {'files':>6} "
        f"{'detailed b':>11} {'detailed s':>11} {'simple b':>10} {'simple s':>9}"
    )

    detailed_times = []
    for label, row in picked:
        rom_id = row["id"]
        _status, detailed, detailed_seconds = timed_get(f"/api/roms/{rom_id}")
        _status, simple, simple_seconds = timed_get(f"/api/roms/{rom_id}/simple")
        detailed_times.append(detailed_seconds)
        lines.append(
            f"  {label:<14} {rom_id:>7} {len(row.get('sibling_roms') or []):>9} "
            f"{len(row.get('files') or []):>6} {len(detailed or b''):>11,} {detailed_seconds:>11.3f} "
            f"{len(simple or b''):>10,} {simple_seconds:>9.3f}"
        )

    lines.append("")
    lines.append("== what DetailedRomSchema adds over SimpleRomSchema, on the heaviest of those")
    heaviest = max(picked, key=lambda pair: len(pair[1].get("sibling_roms") or []))[1]
    _status, detailed, _ = timed_get(f"/api/roms/{heaviest['id']}")
    _status, simple, _ = timed_get(f"/api/roms/{heaviest['id']}/simple")
    detailed_doc = json.loads(detailed)
    simple_doc = json.loads(simple)
    extra = sorted(set(detailed_doc) - set(simple_doc))
    lines.append(f"  rom {heaviest['id']}, fields only on detailed: {', '.join(extra)}")
    for key in extra:
        value = detailed_doc[key]
        size = len(json.dumps(value))
        shape = f"{len(value)} entries" if isinstance(value, list) else type(value).__name__
        lines.append(f"    {key:<24} {shape:<14} {size:>9,} b")

    lines.append("")
    lines.append("== per page: GET /api/roms already carries all of it")
    status, body, elapsed = c.page(PAGE, 0)
    raw = json.dumps(body).encode()
    trimmed = json.dumps(
        {"items": [{key: row.get(key) for key in WANTED} for row in body["items"]]}
    ).encode()
    lines.append(f"  one page of {PAGE}: {len(raw):,} b in {elapsed:.3f}s, status {status}")
    lines.append(f"  the {len(WANTED)} fields M4 reads out of it: {len(trimmed):,} b")
    lines.append(
        f"  so metadata is {100 * len(trimmed) / len(raw):.1f}% of a page already being fetched, "
        "and costs no request at all"
    )

    lines.append("")
    lines.append("== what that means for a set")
    median_detailed = statistics.median(detailed_times)
    for games in (40, 250, 1000):
        pages = -(-games // PAGE)
        lines.append(
            f"  {games:>5} games: per-rom {games} requests, {games * median_detailed:8.1f}s at the "
            f"median above; per-page {pages} request(s) already made by the walk"
        )

    c.record("m4-probe3-metadata-cost", lines)


if __name__ == "__main__":
    main()
