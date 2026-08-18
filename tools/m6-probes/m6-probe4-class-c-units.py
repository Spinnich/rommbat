"""What a class C save unit really looks like on disk, per system.

Stage 2b's whole problem is scoping: a shape that names the emulator's data root costs
seven minutes per sync, and one that names the unit costs nothing. This walks the class C
trees on a real install and reports, per candidate unit, the directory name, the key that
name would yield, the file count and the byte size. It also reads PSP's PARAM.SFO and the
state name-mapping sidecars, because both are candidate attribution routes.

    python m6-probe4-class-c-units.py <retrobat-root>

Reads only. Writes nothing into the install.
"""

from __future__ import annotations

import pathlib
import re
import struct
import sys

from _common import record_offline

if len(sys.argv) != 2:
    print(__doc__)
    raise SystemExit(2)

root = pathlib.Path(sys.argv[1])
saves = root / "saves"
lines: list[str] = []

# 4 letters then 5 digits, which is the PSP and PS3 title-id shape. The savedata directory
# is the id with a game-chosen suffix, so the key is a prefix of the segment and never the
# whole of it.
TITLE_ID = re.compile(r"^([A-Z]{4}[0-9]{5})")

# <makercode>-<gamecode>-<internal name>.gci
GCI = re.compile(r"^([0-9A-Za-z]{2})-([0-9A-Za-z]{4})-(.+)\.gci$")


def weigh(directory: pathlib.Path) -> tuple[int, int]:
    files = [p for p in directory.rglob("*") if p.is_file()]
    return len(files), sum(p.stat().st_size for p in files)


def read_sfo(path: pathlib.Path) -> dict[str, str]:
    """Parses a PSP PARAM.SFO into its string and integer entries.

    Format: a 20-byte header holding the two table offsets and the entry count, then one
    16-byte index entry per key, then the key table, then the data table.
    """
    raw = path.read_bytes()
    if len(raw) < 20 or raw[:4] != b"\x00PSF":
        return {}

    key_table, data_table, count = struct.unpack_from("<III", raw, 8)
    entries: dict[str, str] = {}

    for index in range(count):
        offset = 20 + (index * 16)
        key_offset, fmt, used, _total, data_offset = struct.unpack_from("<HHIII", raw, offset)
        name_start = key_table + key_offset
        name = raw[name_start : raw.index(b"\x00", name_start)].decode("utf-8", "replace")
        blob = raw[data_table + data_offset : data_table + data_offset + used]
        if fmt == 0x0404:
            entries[name] = str(struct.unpack("<I", blob)[0])
        else:
            entries[name] = blob.rstrip(b"\x00").decode("utf-8", "replace")

    return entries


def section(title: str) -> None:
    lines.append("")
    lines.append(f"=== {title} ===")


# ---------------------------------------------------------------------------
# psp: SAVEDATA/<GAMEID><suffix>/
# ---------------------------------------------------------------------------

section("psp savedata")
savedata = saves / "psp" / "SAVEDATA"
if savedata.is_dir():
    for unit in sorted(savedata.iterdir()):
        if not unit.is_dir():
            continue
        count, size = weigh(unit)
        match = TITLE_ID.match(unit.name)
        key = match.group(1) if match else "(no title id prefix)"
        lines.append(f"  {unit.name:<24} key={key:<12} {count} files  {size:,} B")
        lines.append(f"    contents: {', '.join(sorted(p.name for p in unit.iterdir()))}")
        sfo = unit / "PARAM.SFO"
        if sfo.is_file():
            parsed = read_sfo(sfo)
            interesting = {k: v for k, v in parsed.items() if k in {"TITLE", "SAVEDATA_TITLE", "SAVEDATA_DIRECTORY", "SAVEDATA_DETAIL", "CATEGORY"}}
            lines.append(f"    PARAM.SFO keys: {sorted(parsed)}")
            for name, value in sorted(interesting.items()):
                lines.append(f"      {name} = {value!r}")
else:
    lines.append("  absent")

section("psp state sidecars (route 3 candidate)")
declared = saves / "psp" / "ppsspp"
if declared.is_dir():
    for sidecar in sorted(declared.glob("*.txt")):
        content = sidecar.read_text(encoding="utf-8", errors="replace").strip()
        match = TITLE_ID.match(content)
        lines.append(f"  rom stem {sidecar.stem!r}")
        lines.append(f"    sidecar holds {content!r} -> key {match.group(1) if match else '(none)'}")
else:
    lines.append("  absent")

section("psp native state directory")
native = saves / "psp" / "PPSSPP_STATE"
if native.is_dir():
    for entry in sorted(native.iterdir()):
        lines.append(f"  {entry.name}")
else:
    lines.append("  absent")

# ---------------------------------------------------------------------------
# ps3: the emulator data root against the savedata subtree
# ---------------------------------------------------------------------------

section("ps3 rpcs3")
rpcs3 = saves / "ps3" / "rpcs3"
if rpcs3.is_dir():
    count, size = weigh(rpcs3)
    lines.append(f"  whole data root {rpcs3.relative_to(saves).as_posix()}: {count} files, {size:,} B")
    for candidate in sorted(rpcs3.glob("dev_hdd0/home/*/savedata")):
        count, size = weigh(candidate)
        lines.append(f"  savedata tree {candidate.relative_to(saves).as_posix()}: {count} files, {size:,} B")
        for unit in sorted(candidate.iterdir()):
            if not unit.is_dir():
                continue
            unit_count, unit_size = weigh(unit)
            match = TITLE_ID.match(unit.name)
            key = match.group(1) if match else "(no title id prefix)"
            lines.append(f"    {unit.name:<28} key={key:<12} {unit_count} files  {unit_size:,} B")
    for stray in sorted(rpcs3.glob("dev_hdd0/savedata")):
        count, size = weigh(stray)
        lines.append(f"  ALSO {stray.relative_to(saves).as_posix()}: {count} files, {size:,} B")
        lines.append(f"    contents: {', '.join(sorted(p.name for p in stray.iterdir()))}")
else:
    lines.append("  absent")

# ---------------------------------------------------------------------------
# mame: nvram/<shortname>/, the case that needs no attribution
# ---------------------------------------------------------------------------

section("mame nvram")
nvram = saves / "mame" / "nvram"
if nvram.is_dir():
    units = sorted(p for p in nvram.iterdir() if p.is_dir())
    loose = sorted(p for p in nvram.iterdir() if p.is_file())
    total_files = 0
    total_size = 0
    for unit in units:
        count, size = weigh(unit)
        total_files += count
        total_size += size
    lines.append(f"  {len(units)} unit directories, {total_files} files, {total_size:,} B")
    lines.append(f"  {len(loose)} loose files directly under nvram/: {[p.name for p in loose[:10]]}")
    for unit in units[:8]:
        count, size = weigh(unit)
        lines.append(f"    {unit.name:<20} {count} files  {size:,} B  contents={sorted(p.name for p in unit.iterdir())}")
    roms = root / "roms" / "mame"
    if roms.is_dir():
        stems = {p.stem for p in roms.iterdir() if p.is_file()}
        matched = sum(1 for unit in units if unit.name in stems)
        lines.append(f"  short names matching a roms/mame basename: {matched} of {len(units)} (roms present: {len(stems)})")
    else:
        lines.append("  roms/mame is absent, so the shortname-is-basename claim cannot be checked here")
else:
    lines.append("  absent")

# ---------------------------------------------------------------------------
# gamecube: GCI folder, several files per game code, soft deletes
# ---------------------------------------------------------------------------

section("gamecube gci")
gc = saves / "gamecube" / "dolphin-emu" / "User" / "GC"
if gc.is_dir():
    for entry in sorted(gc.rglob("*")):
        if entry.is_file():
            match = GCI.match(entry.name)
            key = match.group(2) if match else "(not a live .gci)"
            lines.append(f"  {entry.relative_to(gc).as_posix():<60} key={key:<8} {entry.stat().st_size:,} B")
    codes: dict[str, int] = {}
    for entry in gc.rglob("*.gci"):
        match = GCI.match(entry.name)
        if match:
            codes[match.group(2)] = codes.get(match.group(2), 0) + 1
    lines.append(f"  live game codes: {codes}")
else:
    lines.append("  absent")

# ---------------------------------------------------------------------------
# wii: the NAND tree, which mixes per-game saves with system state
# ---------------------------------------------------------------------------

section("wii nand")
wii = saves / "wii" / "dolphin-emu" / "User" / "Wii"
if wii.is_dir():
    for entry in sorted(wii.rglob("*")):
        if entry.is_file():
            lines.append(f"  {entry.relative_to(wii).as_posix():<70} {entry.stat().st_size:,} B")
    for title in sorted(wii.glob("title/*/*")):
        if title.is_dir():
            count, size = weigh(title)
            lines.append(f"  title unit {title.relative_to(wii).as_posix():<40} {count} files  {size:,} B")
else:
    lines.append("  absent")

# ---------------------------------------------------------------------------
# dreamcast: shared and per-game vmu in one directory
# ---------------------------------------------------------------------------

section("dreamcast vmu")
vmu = saves / "dreamcast" / "flycast" / "vmu"
if vmu.is_dir():
    for entry in sorted(vmu.iterdir()):
        lines.append(f"  {entry.name:<40} {entry.stat().st_size:,} B")
else:
    lines.append("  absent")

record_offline("probe4-class-c-units", lines)
