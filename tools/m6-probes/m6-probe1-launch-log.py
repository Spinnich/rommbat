"""What emulatorLauncher.log really contains, on a real install with real history.

M6 sources its launch facts from this file rather than from the hook arguments, so every
number the plan quotes about it decides parser behaviour: how it rotates, what a launch
line looks like, which launches have no exit, and what the rom path is rooted at. The plan
quotes 268 KB for 5 weeks and 70 launches, measured during M0 on a different install.

    python m6-probe1-launch-log.py <retrobat-root>

Reads only.
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
directory = root / "emulationstation"
lines: list[str] = []

STAMP = re.compile(r"^﻿?(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) \[(\w+)\] (.*)$")
DRIVE_ROOT = re.compile(r"^([A-Za-z]:\\[^\\]+)\\")
EXIT = re.compile(r"Process exited with code (-?\d+)")


def rom_argument(text: str) -> tuple[str | None, bool, bool]:
    """The -rom value, whether it was quoted, and whether it was the final flag.

    Two forms occur and a regex for the quoted one alone misses the other, so the value is
    read positionally: quoted runs to the closing quote, unquoted runs to end of line.
    """
    index = text.find("-rom ")
    if index < 0:
        return None, False, False
    tail = text[index + len("-rom ") :].rstrip()
    if tail.startswith('"'):
        end = tail.find('"', 1)
        return tail[1:end], True, not tail[end + 1 :].strip()
    return tail, False, True


def flag(text: str, name: str) -> str:
    """The value of a -flag, or a marker for present-but-empty and absent."""
    match = re.search(re.escape(name) + r" (\S+)", text)
    if not match:
        return "(absent)"
    return "(empty)" if match.group(1).startswith("-") else match.group(1)


# Oldest first. The rotation writes the older half to .log.old, so a cursor that reads in
# this order sees launches in time order across the boundary.
files = [directory / "emulatorLauncher.log.old", directory / "emulatorLauncher.log"]

roots: collections.Counter[str] = collections.Counter()
systems: collections.Counter[str] = collections.Counter()
emulators: collections.Counter[str] = collections.Counter()
cores: collections.Counter[str] = collections.Counter()
exits: collections.Counter[str] = collections.Counter()
menu_launches = 0
startup_total = 0
startup_with_rom = 0
unquoted_rom = 0
rom_not_final = 0
core_after_rom = 0
unstamped: list[str] = []
spans: list[tuple[str, str, str]] = []

for path in files:
    if not path.exists():
        lines.append(f"=== {path.name}: absent")
        continue
    raw = path.read_bytes()
    text = raw.decode("utf-8", errors="replace")
    body = text.splitlines()
    stamped = [m for m in (STAMP.match(line) for line in body) if m]
    lines.append(f"=== {path.name}")
    lines.append(f"  bytes                {len(raw):,}")
    lines.append(f"  starts with a BOM    {raw[:3] == b'\xef\xbb\xbf'}")
    lines.append(f"  lines                {len(body):,}   stamped {len(stamped):,}")
    if stamped:
        spans.append((path.name, stamped[0].group(1), stamped[-1].group(1)))
        lines.append(f"  span                 {stamped[0].group(1)}  ->  {stamped[-1].group(1)}")
    levels = collections.Counter(m.group(2) for m in stamped)
    lines.append(f"  levels               {dict(levels)}")

    unstamped += [line for line in body if line.strip() and not STAMP.match(line)]

    file_startup = [line for line in body if "[Startup]" in line and "emulatorLauncher.exe" in line]
    file_rom = [line for line in file_startup if "-rom " in line]
    startup_total += len(file_startup)
    startup_with_rom += len(file_rom)
    lines.append(f"  [Startup] lines      {len(file_startup)}   of which carry -rom {len(file_rom)}")

    for line in file_rom:
        value, quoted, final = rom_argument(line)
        if value:
            drive = DRIVE_ROOT.match(value)
            roots[drive.group(1) if drive else "(not rooted)"] += 1
        unquoted_rom += 0 if quoted else 1
        rom_not_final += 0 if final else 1
        core_after_rom += 1 if line.find("-core") > line.find("-rom ") >= 0 else 0
        system = flag(line, "-system")
        systems[system] += 1
        emulators[flag(line, "-emulator")] += 1
        cores[flag(line, "-core")] += 1
        if system == "retrobat":
            menu_launches += 1

    for line in body:
        match = EXIT.search(line)
        if match:
            exits[match.group(1)] += 1

lines.append("")
lines.append("=== across both files")
lines.append(f"  rotation is by size, and the two files do not overlap: {spans}")
lines.append(f"  [Startup] lines {startup_total}, of which {startup_with_rom} are a game launch")
lines.append(f"  recorded exits  {sum(exits.values())}   codes {dict(exits)}")
lines.append(f"  launches with no recorded exit: {startup_with_rom - sum(exits.values())}")
lines.append("")
lines.append("  the -rom argument is not a fixed shape:")
lines.append(f"     unquoted, so a quoted-only regex misses it: {unquoted_rom}")
lines.append(f"     not the final flag on the line:             {rom_not_final}")
lines.append(f"     -core written after -rom rather than before: {core_after_rom}")
lines.append("")
lines.append(f"  drive roots seen in -rom: {dict(roots)}")
lines.append(f"  distinct systems: {len(systems)}")
lines.append(f"  es_menu launches (-system retrobat): {menu_launches}")
lines.append(f"  top emulators: {dict(sorted(emulators.items(), key=lambda kv: -kv[1])[:12])}")
lines.append(f"  -core values:  {dict(sorted(cores.items(), key=lambda kv: -kv[1])[:8])}")
lines.append("")
lines.append(f"  unstamped lines (continuations and BOMs): {len(unstamped)}")
for line in unstamped[:6]:
    lines.append(f"     | {line[:100]}")

record_offline("m6-probe1-launch-log", lines)
