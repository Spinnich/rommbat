"""F17b: which container formats do GameCube and Wii actually arrive in, and does the
raw-image offset ever get exercised?

The header probe found only .rvz in the first page of each platform. If .iso never appears,
the offset-0 path Freegosy also carries is dead code on this library, and a client that only
handles .iso would read a game code out of nothing. This walks every ROM on both platforms
to get the real distribution, then reads the header of one image per format found.
"""

from __future__ import annotations

import collections
import struct
import urllib.parse
import urllib.request

from _common import base_url, get_json, record, token

GC_MAGIC = 0xC2339F3D
WII_MAGIC = 0x5D1C9EA3

lines: list[str] = []


def printable(raw: bytes) -> str:
    return "".join(chr(b) if 32 <= b < 127 else "." for b in raw)


def head(row: dict, length: int = 256) -> tuple[int, bytes]:
    req = urllib.request.Request(
        base_url() + f"/api/roms/{row['id']}/content/{urllib.parse.quote(row['fs_name'])}",
        headers={"Authorization": f"Bearer {token()}", "Range": f"bytes=0-{length - 1}"},
    )
    with urllib.request.urlopen(req, timeout=120) as response:
        return response.status, response.read()


for platform_id, platform in [(475, "gamecube"), (479, "wii")]:
    rows: list[dict] = []
    offset = 0
    while True:
        _status, body, _t = get_json(
            "/api/roms",
            params={
                "platform_ids": platform_id,
                "limit": 250,
                "offset": offset,
                "order_by": "id",
                "order_dir": "asc",
                "with_char_index": "false",
                "with_filter_values": "false",
                "with_rom_id_index": "false",
            },
        )
        page = (body or {}).get("items", [])
        if not page:
            break
        rows.extend(page)
        offset += 250
        if offset >= ((body or {}).get("total") or 0):
            break

    census: collections.Counter[str] = collections.Counter(
        (r.get("fs_extension") or "(none)").lower() for r in rows
    )
    lines.append(f"=== {platform}: {len(rows)} roms walked ===")
    for ext, count in census.most_common():
        lines.append(f"  .{ext:<10s} {count:5d}  ({100 * count / len(rows):.1f}%)")

    seen: set[str] = set()
    for row in rows:
        ext = (row.get("fs_extension") or "").lower()
        if ext in seen or ext not in ("iso", "rvz", "gcm", "wbfs", "ciso"):
            continue
        if row.get("has_multiple_files"):
            continue
        seen.add(ext)
        status, blob = head(row)
        wii = struct.unpack(">I", blob[0x18:0x1C])[0]
        gc = struct.unpack(">I", blob[0x1C:0x20])[0]
        raw = wii == WII_MAGIC or gc == GC_MAGIC
        lines.append("")
        lines.append(f"  .{ext} sample, rom id {row['id']} -> {status}")
        lines.append(f"    magic bytes 0..3: {blob[:4].hex(' ')} ({printable(blob[:4])!r})")
        lines.append(f"    raw disc image (magic at 0x18/0x1C): {raw}")
        lines.append(f"    game code at 0x00: {printable(blob[0x00:0x04])!r}")
        lines.append(f"    game code at 0x58: {printable(blob[0x58:0x5C])!r}")
        if raw:
            lines.append(f"    internal title:    {printable(blob[0x20:0x40]).rstrip('.')!r}")
    lines.append("")

record("f17b-disc-format-census", lines)
