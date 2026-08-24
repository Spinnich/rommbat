"""Copies a live es_settings.cfg into the test fixtures with its credentials replaced.

**es_settings.cfg holds plaintext credentials.** A real install's file carries the
ScreenScraper password, the RetroAchievements password and token, and the IGDB client id and
secret, all in clear. So the capture the merge-not-clobber test needs cannot be copied in as
it stands, and the fixtures README's redaction rule is not optional here.

Every other byte is kept exactly, because a fixture that has been tidied no longer proves
what it was captured to prove: the tab indentation, the LF endings, the bare
`<?xml version="1.0"?>`, the `&amp;` escaping and ES's own alphabetical-within-group sorting
are all the things the writer has to reproduce.

    python m6-redact-es-settings.py <retrobat-root>

Writes tests/RomMBat.Tests/fixtures/es_settings.cfg. Reads the install, writes the repo.
"""

from __future__ import annotations

import pathlib
import re
import sys

from _common import REPO

if len(sys.argv) != 2:
    print(__doc__)
    raise SystemExit(2)

source = pathlib.Path(sys.argv[1]) / "emulationstation" / ".emulationstation" / "es_settings.cfg"
destination = REPO / "tests" / "RomMBat.Tests" / "fixtures" / "es_settings.cfg"

# name -> placeholder. Anything holding a credential, plus the two that name the person.
REDACTIONS = {
    "ScreenScraperPass": "REDACTED-SCREENSCRAPER-PASS",
    "ScreenScraperUser": "REDACTED-USER",
    "global.retroachievements.password": "REDACTED-RA-PASSWORD",
    "global.retroachievements.token": "REDACTED-RA-TOKEN",
    "global.retroachievements.username": "REDACTED-USER",
    "global.netplay.nickname": "REDACTED-USER",
    "IGDBClientID": "REDACTED-IGDB-CLIENT-ID",
    "IGDBSecret": "REDACTED-IGDB-SECRET",
}

text = source.read_bytes().decode("utf-8")
original_values: list[str] = []

for name, placeholder in REDACTIONS.items():
    pattern = re.compile(r'(<(?:bool|int|string) name="' + re.escape(name) + r'" value=")([^"]*)(")')

    def replace(match: re.Match[str]) -> str:
        original_values.append(match.group(2))
        return match.group(1) + placeholder + match.group(3)

    text, count = pattern.subn(replace, text)
    print(f"  {name}: {count} replaced")

# The check that matters: no value this script took out survives anywhere in the output.
# Asserted rather than trusted, because a redaction that silently matched nothing looks
# exactly like one that worked.
survivors = sorted(
    {
        match.group(1)
        for match in re.finditer(r'<(?:bool|int|string) name="([^"]*)" value="([^"]*)"', text)
        if match.group(2) and match.group(2) in original_values
    }
)
if survivors:
    # Names only. The whole point of the check is that the values must not be printed.
    raise SystemExit(f"redaction failed: a replaced value is still present under {survivors}")

destination.write_bytes(text.encode("utf-8"))
print(f"\n  wrote {destination}")
print(f"  {len(text.encode('utf-8'))} bytes, {text.count(chr(10))} lines, {len(original_values)} values redacted")
