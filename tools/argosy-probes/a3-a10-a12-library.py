"""A3, A10 and A12: firmware coverage, the identifiers endpoint, and payload composition.

A3  Does the md5-only firmware join miss firmware this library actually holds?
    RetroBat names one acceptable md5 per file where several valid dumps exist, so a
    working BIOS under a different revision reads as MissingFromLibrary.

A10 GET /api/roms/identifiers was measured at 504 after 300 s on this library
    (retrobat-findings measurement 81). Argosy reconciles deletions through it at
    23,873 roms. Re-timed here rather than assumed still true.

A12 merged_ra_metadata was 45% of Argosy's page payload. Ours is a different library
    on a different instance, so the share is re-derived rather than carried over.

Read-only. Every request is a GET.
"""

from __future__ import annotations

import json
import pathlib
import sys

import _common

REPO = pathlib.Path(__file__).resolve().parents[2]
IDENTIFIERS_TIMEOUT = 310.0


def our_manifest_md5s() -> dict[str, list[str]]:
    manifest = json.loads((REPO / "data" / "retrobat" / "bios.json").read_text(encoding="utf-8"))
    out: dict[str, list[str]] = {}
    for system, entry in manifest["systems"].items():
        for row in entry["files"]:
            md5 = (row.get("md5") or "").lower()
            if md5:
                out.setdefault(md5, []).append(f"{system}:{row['path']}")
    return out


def probe_a3(lines: list[str]) -> None:
    lines.append("## A3: firmware this library holds, against the md5s RetroBat names")
    lines.append("")
    status, platforms, elapsed = _common.get_json("/api/platforms")
    if status != 200:
        lines.append(f"GET /api/platforms answered {status}")
        return
    ours = our_manifest_md5s()
    lines.append(f"GET /api/platforms -> {status} in {elapsed:.2f}s")
    lines.append(f"md5s RetroBat names, from data/retrobat/bios.json: {len(ours)}")
    lines.append("")

    matched: list[tuple[str, str]] = []
    unmatched: list[tuple[str, str, str]] = []
    no_md5: list[tuple[str, str]] = []
    for platform in platforms:
        slug = platform.get("slug") or platform.get("fs_slug") or str(platform.get("id"))
        for fw in platform.get("firmware") or []:
            name = fw.get("file_name") or "?"
            md5 = (fw.get("md5_hash") or "").lower()
            if not md5:
                no_md5.append((slug, name))
            elif md5 in ours:
                matched.append((slug, name))
            else:
                unmatched.append((slug, name, md5))

    total = len(matched) + len(unmatched) + len(no_md5)
    lines.append(f"firmware records in the library: {total}")
    lines.append(f"  md5 matches a RetroBat requirement: {len(matched)}")
    lines.append(f"  md5 matches nothing RetroBat names: {len(unmatched)}")
    lines.append(f"  no md5 on the RomM record at all:   {len(no_md5)}")
    lines.append("")
    if unmatched:
        lines.append("Unmatched, by platform (what M5 would call MissingFromLibrary):")
        by_platform: dict[str, list[str]] = {}
        for slug, name, _md5 in unmatched:
            by_platform.setdefault(slug, []).append(name)
        for slug in sorted(by_platform):
            names = sorted(by_platform[slug])
            lines.append(f"  {slug}: {len(names)}")
            for name in names:
                lines.append(f"    {name}")
    lines.append("")

    # The half that decides whether any of this costs anything: a RetroBat system whose
    # every named md5 is absent from the library, while the library holds firmware for it.
    lines.append("Systems where the library has firmware but no md5 RetroBat names:")
    manifest = json.loads((REPO / "data" / "retrobat" / "bios.json").read_text(encoding="utf-8"))
    held_by_slug: dict[str, set[str]] = {}
    for platform in platforms:
        slug = platform.get("slug") or platform.get("fs_slug") or ""
        for fw in platform.get("firmware") or []:
            md5 = (fw.get("md5_hash") or "").lower()
            if md5:
                held_by_slug.setdefault(slug, set()).add(md5)
    hits = 0
    for slug, held in sorted(held_by_slug.items()):
        entry = manifest["systems"].get(slug)
        if not entry:
            continue
        required = {(r.get("md5") or "").lower() for r in entry["files"] if r.get("md5")}
        if required and not (required & held):
            hits += 1
            lines.append(
                f"  {slug}: holds {len(held)}, RetroBat names {len(required)}, overlap 0"
            )
    if hits == 0:
        lines.append("  none")
    lines.append("")


def probe_a10(lines: list[str]) -> None:
    lines.append("## A10: GET /api/roms/identifiers, re-timed")
    lines.append("")
    try:
        status, headers, payload, elapsed = _common.request(
            "GET", "/api/roms/identifiers", timeout=IDENTIFIERS_TIMEOUT
        )
        lines.append(f"GET /api/roms/identifiers -> {status} in {elapsed:.1f}s, {len(payload)} bytes")
        if status == 200:
            ids = json.loads(payload)
            lines.append(f"  {len(ids)} ids")
    except Exception as err:  # noqa: BLE001 - the failure mode is the measurement
        lines.append(f"GET /api/roms/identifiers -> raised {type(err).__name__}: {err}")
    for sibling in ("/api/platforms/identifiers", "/api/collections/identifiers"):
        try:
            status, _headers, payload, elapsed = _common.request("GET", sibling, timeout=60.0)
            lines.append(f"GET {sibling} -> {status} in {elapsed:.2f}s, {len(payload)} bytes")
        except Exception as err:  # noqa: BLE001
            lines.append(f"GET {sibling} -> raised {type(err).__name__}: {err}")
    lines.append("")


def probe_a12(lines: list[str]) -> None:
    lines.append("## A12: where one page's payload goes")
    lines.append("")
    params = {
        "with_char_index": "false",
        "with_filter_values": "false",
        "with_rom_id_index": "true",
        "with_total": "true",
        "with_files": "false",
        "order_by": "id",
        "order_dir": "asc",
        "limit": 100,
        "offset": 0,
    }
    status, _headers, payload, elapsed = _common.request("GET", "/api/roms", params=params)
    if status != 200:
        lines.append(f"GET /api/roms answered {status}")
        return
    body = json.loads(payload)
    items = body.get("items") or []
    lines.append(f"limit=100 -> {status} in {elapsed:.2f}s, {len(payload) / 1024:.0f} KiB body")
    lines.append(f"items: {len(items)}")
    lines.append("")

    per_key: dict[str, int] = {}
    for item in items:
        for key, value in item.items():
            per_key[key] = per_key.get(key, 0) + len(json.dumps(value))
    body_total = sum(per_key.values()) or 1
    lines.append("| field | KiB over the page | share |")
    lines.append("| --- | --- | --- |")
    for key, size in sorted(per_key.items(), key=lambda kv: kv[1], reverse=True)[:15]:
        lines.append(f"| `{key}` | {size / 1024:.1f} | {100 * size / body_total:.1f}% |")
    lines.append("")


def main() -> int:
    lines: list[str] = []
    status, heartbeat, _ = _common.get_json("/api/heartbeat")
    version = (heartbeat or {}).get("SYSTEM", {}).get("VERSION", "unknown")
    lines.append(f"server version: {version}")
    lines.append("")
    probe_a3(lines)
    probe_a10(lines)
    probe_a12(lines)
    _common.record("a3-a10-a12-library", lines)
    return 0


if __name__ == "__main__":
    sys.exit(main())
