"""F18b: which RetroBat systems will *index* an .m3u, which is not the same as playing one.

A multi-disc set is several files. EmulationStation shows one entry per launchable file, so
without something to bind them the set is N games in the gallery and, worse, N memory cards
under any per-game card mode, which loses the save at the disc change.

The `.m3u` playlist is the binding mechanism and it is not universal. **This probe measures
only half of that**, and the half it measures is the misleading one, which is the point of
running it.

`<extension>` decides what EmulationStation indexes and offers to launch. It says nothing
about whether the emulator behind the system can consume the file. The two come apart in
exactly the place it hurts: `ps2` lists `.m3u`, and RetroBat's own wiki says plainly
"PCSX2 does not support m3u usage for multi-disc games". So ES will show the playlist, the
launcher will hand it over, and the emulator will not understand it.

Read the output as "ES will offer this", never as "this works". Emulator support is a
separate fact and the per-emulator wiki page is the source for it.

Read-only. Reads the live es_systems.cfg and nothing else.

  python f18b-m3u-support-census.py <retrobat-root>
"""

from __future__ import annotations

import pathlib
import sys
import xml.etree.ElementTree as ET

from _common import record

if len(sys.argv) != 2:
    print(__doc__)
    raise SystemExit(2)

root = pathlib.Path(sys.argv[1])
cfg = root / "emulationstation" / ".emulationstation" / "es_systems.cfg"
tree = ET.parse(cfg)

lines: list[str] = []
version = (root / "system" / "version.info").read_text(encoding="utf-8").strip()
lines.append(f"es_systems.cfg from RetroBat {version}")
lines.append("")

# Folder is <path>, never <name>: they are different vocabularies and the shipped file
# disagrees on five systems.
systems = []
for system in tree.getroot().findall("system"):
    path = (system.findtext("path") or "").replace("\\", "/").rstrip("/")
    folder = path.rsplit("/", 1)[-1] if path else ""
    exts = {e.strip().lower() for e in (system.findtext("extension") or "").split() if e.strip()}
    if folder:
        systems.append((folder, exts))

with_m3u = sorted(f for f, e in systems if ".m3u" in e)
without = sorted(f for f, e in systems if ".m3u" not in e)

lines.append(f"systems declaring a <path>: {len(systems)}")
lines.append(f"  list .m3u in <extension>:     {len(with_m3u)}")
lines.append(f"  do not list .m3u:            {len(without)}")
lines.append("")
lines.append("Systems whose <extension> lists .m3u, so ES will index and offer one.")
lines.append("This is NOT a statement that the emulator can play it: ps2 is in this list")
lines.append("and RetroBat's wiki says PCSX2 does not support m3u for multi-disc games.")
for index in range(0, len(with_m3u), 6):
    lines.append("  " + "  ".join(f"{f:<16}" for f in with_m3u[index : index + 6]))
lines.append("")

# The disc-based systems that cannot express a set as one entry are the interesting ones:
# every other system is single-file anyway, so the absence costs nothing there.
DISC_BASED = {
    "ps2", "gamecube", "wii", "dreamcast", "saturn", "segacd", "megacd", "3do", "amigacd32",
    "pcenginecd", "pcfx", "neogeocd", "cdi", "xbox", "psp", "naomi", "atomiswave",
}
disc_without = sorted(DISC_BASED & set(without))
lines.append("Disc-based systems that do not even list .m3u, where a set is always N entries:")
for folder in disc_without:
    exts = next(e for f, e in systems if f == folder)
    lines.append(f"  {folder:<14} {' '.join(sorted(exts))}")
lines.append("")

lines.append("The two the wiki documents, side by side. Both list .m3u; only one can use it:")
for folder in ("psx", "ps2"):
    match = [e for f, e in systems if f == folder]
    if match:
        lines.append(f"  {folder:<5} {' '.join(sorted(match[0]))}")
lines.append("")
lines.append("  psx: the wiki documents the playlist as the supported multi-disc route")
lines.append("  ps2: the wiki says PCSX2 does not support m3u; disc changes go through the")
lines.append("       emulator's own quick menu instead, so the set cannot be one entry")

record("f18b-m3u-support-census", lines)
