"""Shared plumbing for the M6 probes.

Same arrangement as M5: reuse the Freegosy probes' request and redaction helpers so the
rule that a host and a token never reach a transcript lives in one place, and only redirect
the output directory. Output goes to probe-output/m6/, which is gitignored.

Every probe in this directory reads. None of them writes into a RetroBat install.
"""

from __future__ import annotations

import importlib.util
import pathlib

# Loaded by path under a distinct name: the Freegosy helper is also called _common, and a
# plain import from this directory finds this file instead.
_source = pathlib.Path(__file__).resolve().parents[1] / "freegosy-probes" / "_common.py"
_spec = importlib.util.spec_from_file_location("freegosy_common", _source)
freegosy = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(freegosy)

REPO = pathlib.Path(__file__).resolve().parents[2]
freegosy.OUTPUT_DIR = REPO / "probe-output" / "m6"

base_url = freegosy.base_url
get_json = freegosy.get_json
record = freegosy.record
redact = freegosy.redact
request = freegosy.request
token = freegosy.token


def record_offline(name: str, lines: list[str]) -> None:
    """Writes a transcript for a probe that never talks to RomM."""
    freegosy.OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    text = "\n".join(lines) + "\n"
    (freegosy.OUTPUT_DIR / f"{name}.txt").write_text(text, encoding="utf-8")
    print(text, end="")


def declared_systems(root: pathlib.Path) -> set[str]:
    """Every <name> in the install's live es_systems.cfg."""
    import xml.etree.ElementTree as ElementTree

    path = root / "emulationstation" / ".emulationstation" / "es_systems.cfg"
    tree = ElementTree.parse(path)
    return {name for name in (s.findtext("name") for s in tree.getroot().findall("system")) if name}
