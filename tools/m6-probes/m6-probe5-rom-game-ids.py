"""Which ROM containers yield a Game ID from a bounded read, and which do not.

Attribution route 2 reads the ID out of the ROM. F17 measured the `.rvz` offset over a
bounded Range against the server; this reads real local images across every class C system,
because the question route 2's scope turns on is not "does 0x58 work" but "which of the
containers a real library actually holds can be read at a constant offset at all".

    python m6-probe5-rom-game-ids.py <retrobat-root>

Reads at most 4 KiB from the head of each ROM. Writes nothing.
"""

from __future__ import annotations

import collections
import pathlib
import re
import sys

from _common import record_offline

if len(sys.argv) != 2:
    print(__doc__)
    raise SystemExit(2)

root = pathlib.Path(sys.argv[1])
roms = root / "roms"
lines: list[str] = []

GAME_CODE = re.compile(rb"^[A-Z0-9]{4}$")
HEAD = 4096


def head(path: pathlib.Path) -> bytes:
    with path.open("rb") as handle:
        return handle.read(HEAD)


def describe(path: pathlib.Path) -> tuple[str, str]:
    """Returns (verdict, detail) for one image, reading only its head."""
    raw = head(path)

    if len(raw) < 0x60:
        return "refused", "shorter than any header this reads"

    # A raw GameCube or Wii disc image carries the code at offset 0, and the Wii magic at
    # 0x18 or the GameCube magic at 0x1C says which.
    if GAME_CODE.match(raw[0:4]):
        wii = raw[0x18:0x1C] == b"\x5d\x1c\x9e\xa3"
        cube = raw[0x1C:0x20] == b"\xc2\x33\x9f\x3d"
        if wii or cube:
            kind = "wii" if wii else "gamecube"
            return "read", f"raw disc image ({kind}), code {raw[0:4].decode()!r} at 0x00"

    if raw[0:3] == b"RVZ":
        version = int.from_bytes(raw[4:8], "little")
        code = raw[0x58:0x5C]
        if version != 1:
            return "refused", f"RVZ format version {version}, not the version 1 this offset was measured against"
        if not GAME_CODE.match(code):
            return "refused", f"RVZ v1 but 0x58 holds {code!r}, which is not a game code"
        return "read", f"RVZ v{version}, code {code.decode()!r} at 0x58"

    if raw[0:4] in (b"WBFS", b"CISO", b"\x01\x00\x00\x00"):
        pass

    # A WAD's header is a size and a type, and its title id sits behind a certificate chain
    # whose length varies, so no constant offset reaches it.
    if len(raw) >= 8 and int.from_bytes(raw[0:4], "big") == 0x20 and raw[4:6] in (b"Is", b"ib"):
        return "refused", f"WAD (type {raw[4:6].decode()!r}), title id is behind a variable-length certificate chain"

    if raw[0:4] == b"CISO":
        return "refused", "CISO: a compressed UMD image, the id is in PARAM.SFO inside the filesystem"

    if raw[0:5] == b"MComp":
        return "refused", "compressed container, no header in the clear"

    # A PS3 or PSX image is an ISO9660 filesystem: the id is a file inside it, not a field.
    return "refused", f"no recognised header, first 8 bytes {raw[0:8].hex(' ')}"


for system in ("gamecube", "wii", "psp", "ps3", "psx"):
    directory = roms / system
    lines.append("")
    lines.append(f"=== {system} ===")

    if not directory.is_dir():
        lines.append("  absent")
        continue

    images = sorted(p for p in directory.iterdir() if p.is_file() and p.suffix.lower() not in {".xml", ".txt", ".m3u"})
    verdicts: collections.Counter[str] = collections.Counter()
    extensions: collections.Counter[str] = collections.Counter()
    samples: dict[str, list[str]] = collections.defaultdict(list)

    for image in images:
        extensions[image.suffix.lower()] += 1
        try:
            verdict, detail = describe(image)
        except OSError as error:
            verdict, detail = "refused", f"unreadable: {error}"
        verdicts[verdict] += 1
        if len(samples[detail.split(",")[0]]) < 2:
            samples[detail.split(",")[0]].append(f"{image.name}: {detail}")

    total = len(images)
    lines.append(f"  {total} images   extensions {dict(extensions)}")
    for verdict, count in sorted(verdicts.items()):
        share = (count / total * 100) if total else 0
        lines.append(f"  {verdict:<8} {count:>5} ({share:.1f}%)")
    for group in sorted(samples):
        for sample in samples[group]:
            lines.append(f"    {sample}")

record_offline("probe5-rom-game-ids", lines)
