"""Shared plumbing for the RomM 5.3.0 re-measurement probes.

Every probe reads its server and token from the environment, never from a file the
repository tracks, and never prints the host. Set both before running:

    ROMMBAT_TEST_SERVER=https://your-romm-instance
    ROMMBAT_TEST_APPROVER_TOKEN=rmm_...

Output goes to probe-output/, which is gitignored.
"""

from __future__ import annotations

import json
import os
import pathlib
import re
import time
import urllib.error
import urllib.parse
import urllib.request

OUTPUT_DIR = pathlib.Path(__file__).resolve().parents[2] / "probe-output" / "romm-5.3"


def base_url() -> str:
    raw = os.environ.get("ROMMBAT_TEST_SERVER", "").strip()
    if not raw:
        raise SystemExit("ROMMBAT_TEST_SERVER is not set")
    return raw.rstrip("/")


def token() -> str:
    raw = os.environ.get("ROMMBAT_TEST_APPROVER_TOKEN", "").strip()
    if not raw:
        raise SystemExit("ROMMBAT_TEST_APPROVER_TOKEN is not set")
    return raw


def redact(text: str) -> str:
    """Replaces the instance host and the token with placeholders."""
    host = urllib.parse.urlparse(base_url()).netloc
    out = text.replace(base_url(), "https://<instance>")
    if host:
        out = out.replace(host, "<instance>")
    tok = token()
    if tok:
        out = out.replace(tok, "rmm_<redacted>")
    # Device ids are per-install identifiers and belong in a transcript no more than
    # the host does. Anything UUID-shaped is a device id in every response these
    # probes read.
    out = re.sub(
        r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        "<device>",
        out,
    )
    return out


def request(
    method: str,
    path: str,
    *,
    params: dict | None = None,
    json_body=None,
    raw_body: bytes | None = None,
    content_type: str | None = None,
    headers_extra: dict[str, str] | None = None,
    timeout: float = 120.0,
    read_body: bool = True,
):
    """Issues one request and returns (status, headers, body_bytes, elapsed_seconds)."""
    url = base_url() + path
    if params:
        flat: list[tuple[str, str]] = []
        for key, value in params.items():
            if value is None:
                continue
            if isinstance(value, (list, tuple)):
                flat.extend((key, str(item)) for item in value)
            else:
                flat.append((key, str(value)))
        url += "?" + urllib.parse.urlencode(flat)

    body = raw_body
    headers = {"Authorization": f"Bearer {token()}", "Accept": "application/json"}
    if json_body is not None:
        body = json.dumps(json_body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    elif content_type:
        headers["Content-Type"] = content_type
    if headers_extra:
        headers.update(headers_extra)

    req = urllib.request.Request(url, data=body, headers=headers, method=method)
    started = time.monotonic()
    try:
        with urllib.request.urlopen(req, timeout=timeout) as response:
            payload = response.read() if read_body else b""
            return response.status, dict(response.headers), payload, time.monotonic() - started
    except urllib.error.HTTPError as err:
        payload = err.read()
        return err.code, dict(err.headers), payload, time.monotonic() - started


def get_json(path: str, **kwargs):
    status, _headers, payload, elapsed = request("GET", path, **kwargs)
    parsed = json.loads(payload) if payload else None
    return status, parsed, elapsed


def server_version() -> str:
    _status, heartbeat, _elapsed = get_json("/api/heartbeat")
    return (heartbeat or {}).get("SYSTEM", {}).get("VERSION", "unknown")


# What CatalogQuery.ToQueryString sends on a walk: index off under every scope since #188,
# and the total on so the walk knows where it ends.
WALK = {
    "with_char_index": "false",
    "with_filter_values": "false",
    "with_rom_id_index": "false",
    "with_files": "false",
    "with_total": "true",
    "order_by": "id",
    "order_dir": "asc",
    "limit": 250,
}


def walk(scope: dict):
    """Yields every row a scope pages back, in id order."""
    offset = 0
    while True:
        status, _headers, payload, _elapsed = request(
            "GET", "/api/roms", params={**WALK, **scope, "offset": offset}, timeout=300.0
        )
        if status != 200:
            raise SystemExit(f"GET /api/roms {scope} at offset {offset} answered {status}")
        page = json.loads(payload)
        items = page.get("items") or []
        yield from items
        offset += len(items)
        if not items or offset >= (page.get("total") or 0):
            return


def platforms() -> list[dict]:
    """Every platform holding roms, largest first."""
    status, body, _elapsed = get_json("/api/platforms")
    if status != 200 or body is None:
        raise SystemExit(f"GET /api/platforms answered {status}")
    held = [p for p in body if (p.get("rom_count") or 0) > 0]
    return sorted(held, key=lambda p: -(p.get("rom_count") or 0))


def record(name: str, lines: list[str]) -> None:
    """Writes a probe transcript to probe-output/ and echoes it, redacted."""
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    text = redact("\n".join(lines)) + "\n"
    (OUTPUT_DIR / f"{name}.txt").write_text(text, encoding="utf-8")
    print(text, end="")
