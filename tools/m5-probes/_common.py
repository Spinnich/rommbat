"""Shared plumbing for the M5 probes.

Reuses the Freegosy probes' request and redaction helpers so the rule that a host and a
token never reach a transcript lives in one place, and only redirects the output
directory. Both variables must be set before running the ones that talk to RomM:

    ROMMBAT_TEST_SERVER=https://your-romm-instance
    ROMMBAT_TEST_APPROVER_TOKEN=rmm_...

Output goes to probe-output/m5/, which is gitignored.
"""

from __future__ import annotations

import importlib.util
import json
import pathlib

# Loaded by path under a distinct name: the Freegosy helper is also called _common, and a
# plain import from this directory finds this file instead.
_source = pathlib.Path(__file__).resolve().parents[1] / "freegosy-probes" / "_common.py"
_spec = importlib.util.spec_from_file_location("freegosy_common", _source)
freegosy = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(freegosy)

REPO = pathlib.Path(__file__).resolve().parents[2]
freegosy.OUTPUT_DIR = REPO / "probe-output" / "m5"

base_url = freegosy.base_url
get_json = freegosy.get_json
record = freegosy.record
redact = freegosy.redact
request = freegosy.request
token = freegosy.token


def record_offline(name: str, lines: list[str]) -> None:
    """Writes a transcript for a probe that never talks to RomM.

    The shared recorder redacts by asking for the server and the token, which a probe that
    only reads local files does not have and should not need.
    """
    freegosy.OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    text = "\n".join(lines) + "\n"
    (freegosy.OUTPUT_DIR / f"{name}.txt").write_text(text, encoding="utf-8")
    print(text, end="")


def manifest() -> dict:
    """The vendored requirements manifest, read as-is."""
    path = REPO / "reference" / "batocera-systems.json"
    return json.loads(path.read_text(encoding="utf-8"))


def requirements() -> list[tuple[str, str, str]]:
    """Every (system, md5, destination path) the manifest lists, blanks included."""
    return [
        (system, entry["md5"].strip().lower(), entry["file"])
        for system, body in manifest().items()
        for entry in body.get("biosFiles", [])
    ]
