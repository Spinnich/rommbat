"""F17: where is the GameCube/Wii game ID inside a disc image?

M6 attributes a class-C or converted class-D save by Game ID, and the fallback route when no
launch has been observed is to read that ID out of the ROM. Freegosy reads 4 ASCII bytes at
offset 0x00 for an `.iso` and **0x58** for an `.rvz`. The `.iso` offset is the documented
GameCube and Wii disc header layout; the `.rvz` offset is container-specific and is the one
worth checking.

No disc image is downloaded. Single-file ROMs accept a bounded Range (M3 finding 78), so this
reads the first 256 bytes of a real image straight off the server. That is enough to see the
header, both magic numbers and the internal title.

Read-only. Touches nothing on the server and writes nothing to a RetroBat tree.
"""

from __future__ import annotations

import struct
import urllib.parse
import urllib.request

from _common import base_url, get_json, record, token

# Offsets in the uncompressed GameCube / Wii disc header, per the documented layout.
GC_MAGIC = 0xC2339F3D  # at 0x1C
WII_MAGIC = 0x5D1C9EA3  # at 0x18
FREEGOSY_RVZ_OFFSET = 0x58

lines: list[str] = []


def printable(raw: bytes) -> str:
    return "".join(chr(b) if 32 <= b < 127 else "." for b in raw)


def describe(blob: bytes, label: str) -> None:
    lines.append(f"  first 8 bytes: {blob[:8].hex(' ')}  ({printable(blob[:8])})")
    if len(blob) < 0x60:
        lines.append(f"  only {len(blob)} bytes returned, cannot inspect the header")
        return

    wii = struct.unpack(">I", blob[0x18:0x1C])[0]
    gc = struct.unpack(">I", blob[0x1C:0x20])[0]
    lines.append(f"  0x18 = {wii:#010x} {'(Wii magic)' if wii == WII_MAGIC else ''}")
    lines.append(f"  0x1C = {gc:#010x} {'(GameCube magic)' if gc == GC_MAGIC else ''}")

    if wii == WII_MAGIC or gc == GC_MAGIC:
        lines.append(f"  -> this is a raw disc image, so the header is at offset 0")
        lines.append(f"     0x00 game code:  {printable(blob[0x00:0x04])!r}")
        lines.append(f"     0x04 maker code: {printable(blob[0x04:0x06])!r}")
        lines.append(f"     0x06 disc id:    {blob[0x06]}   0x07 version: {blob[0x07]}")
        lines.append(f"     0x20 title:      {printable(blob[0x20:0x40]).rstrip('.')!r}")
    else:
        lines.append("  -> not a raw disc image at offset 0, so this is a container")

    candidate = blob[FREEGOSY_RVZ_OFFSET : FREEGOSY_RVZ_OFFSET + 4]
    ok = all(48 <= b <= 57 or 65 <= b <= 90 for b in candidate)
    lines.append(
        f"  {label}: bytes at 0x58 = {candidate.hex(' ')} ({printable(candidate)!r}), "
        f"valid game-code shape: {ok}"
    )


for platform_id, platform in [(475, "gamecube"), (479, "wii")]:
    status, body, _t = get_json(
        "/api/roms",
        params={
            "platform_ids": platform_id,
            "limit": 250,
            "order_by": "id",
            "order_dir": "asc",
            "with_files": "true",
            "with_char_index": "false",
            "with_filter_values": "false",
            "with_rom_id_index": "false",
        },
    )
    rows = (body or {}).get("items", [])

    by_ext: dict[str, dict] = {}
    for row in rows:
        ext = (row.get("fs_extension") or "").lower()
        if ext in ("iso", "rvz", "gcm", "wbfs") and ext not in by_ext:
            if not row.get("has_multiple_files"):
                by_ext[ext] = row

    lines.append(f"=== {platform}: {len(rows)} roms sampled, extensions found: {sorted(by_ext)} ===")
    for ext, row in sorted(by_ext.items()):
        name = row["fs_name"]
        # Bounded Range only. Never issue an unranged GET here: these are multi-gigabyte
        # images and the whole point is to read 256 bytes.
        req = urllib.request.Request(
            base_url() + f"/api/roms/{row['id']}/content/{urllib.parse.quote(name)}",
            headers={"Authorization": f"Bearer {token()}", "Range": "bytes=0-255"},
        )
        with urllib.request.urlopen(req, timeout=90) as response:
            blob = response.read()
            code = response.status
            content_range = response.headers.get("Content-Range")

        size_gb = (row.get("fs_size_bytes") or 0) / 1e9
        lines.append(f"\n.{ext}  rom id {row['id']}  {size_gb:.2f} GB on the server")
        lines.append(f"  GET content with Range: bytes=0-255 -> {code}, Content-Range {content_range}")
        lines.append(f"  {len(blob)} bytes read instead of {row.get('fs_size_bytes')}")
        describe(blob, f".{ext}")
    lines.append("")

record("f17-disc-header-offsets", lines)
