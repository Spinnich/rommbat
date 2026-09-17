"""R3: what GET /api/roms/identifiers costs, which finding 9 cites and no probe recorded (#186).

The endpoint takes no parameters, so it can be neither scoped nor paged, and the only reading
that means anything is the whole library at once. 5.2.0 answered 504 on the 300 s wall; the
5.3.0-alpha.2 reading was a 200 after 176.7 s at 95,993 roms.

The timeout is 900 s rather than the client's budget, because the question is how long the
server takes and not whether RomMBat waits for it. The server keeps working after a client
gives up, so this runs alone: a live suite or another probe running beside it measures both.

Read-only. Every request is a GET.
"""

from __future__ import annotations

import json
import sys

import _common


def main() -> int:
    version = _common.server_version()
    _status, first, _elapsed = _common.get_json(
        "/api/roms", params={**_common.WALK, "limit": 1}, timeout=300.0
    )
    library = (first or {}).get("total")

    status, _headers, payload, elapsed = _common.request(
        "GET", "/api/roms/identifiers", timeout=900.0
    )

    count = None
    if status == 200:
        body = json.loads(payload)
        count = len(body) if isinstance(body, list) else f"a {type(body).__name__}, not a list"

    _common.record(
        "r3-identifiers",
        [
            "# R3: GET /api/roms/identifiers",
            "",
            f"Server reports `{version}`. The library pages back a total of {library} roms.",
            "",
            "| status | elapsed | body | identifiers |",
            "| --- | --- | --- | --- |",
            f"| {status} | {elapsed:.1f} s | {len(payload) / 1024:.0f} KiB | {count} |",
        ],
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
