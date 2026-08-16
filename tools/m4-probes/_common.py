"""Shared plumbing for the M4 probes.

Reuses the Freegosy probes' request and redaction helpers so the rule that a host and a
token never reach a transcript lives in one place, and only redirects the output
directory. Both variables must be set before running:

    ROMMBAT_TEST_SERVER=https://your-romm-instance
    ROMMBAT_TEST_APPROVER_TOKEN=rmm_...

Output goes to probe-output/m4/, which is gitignored.
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

freegosy.OUTPUT_DIR = pathlib.Path(__file__).resolve().parents[2] / "probe-output" / "m4"

base_url = freegosy.base_url
get_json = freegosy.get_json
record = freegosy.record
redact = freegosy.redact
request = freegosy.request
token = freegosy.token

# Every costly sidecar off, per core principle 2. with_total stays on: it is an integer.
PAGE_DEFAULTS = {
    "with_char_index": "false",
    "with_filter_values": "false",
    "with_rom_id_index": "false",
    "order_by": "id",
    "order_dir": "asc",
}


def page(limit: int, offset: int, **extra):
    """One page of /api/roms with the sidecars off."""
    params = dict(PAGE_DEFAULTS)
    params.update(extra)
    params["limit"] = limit
    params["offset"] = offset
    return get_json("/api/roms", params=params)
