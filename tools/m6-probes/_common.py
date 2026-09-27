"""Shared plumbing for the save-data generators in this directory.

`m6-emit-save-rules.py` reads a RetroBat install and writes data/retrobat/save_rules.json.
`m6-redact-es-settings.py` reads an install and writes the repo's test fixtures. Neither
talks to RomM. Transcripts go to probe-output/m6/, which is gitignored.

**es_settings.cfg holds plaintext credentials** (the ScreenScraper password, the
RetroAchievements password and token, the IGDB secret). No transcript may print a value read
out of it, and no capture of it may be checked in unredacted.
"""

from __future__ import annotations

import pathlib

REPO = pathlib.Path(__file__).resolve().parents[2]
OUTPUT_DIR = REPO / "probe-output" / "m6"


def record_offline(name: str, lines: list[str]) -> None:
    """Writes a transcript for a script that never talks to RomM."""
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    text = "\n".join(lines) + "\n"
    (OUTPUT_DIR / f"{name}.txt").write_text(text, encoding="utf-8")
    print(text, end="")
