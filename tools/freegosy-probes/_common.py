"""Shared plumbing for the Freegosy probes.

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
import time
import urllib.error
import urllib.parse
import urllib.request

OUTPUT_DIR = pathlib.Path(__file__).resolve().parents[2] / "probe-output" / "freegosy"


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


def multipart(fields: dict[str, tuple[str, bytes]]) -> tuple[bytes, str]:
    """Builds a multipart/form-data body from {field: (filename, bytes)}."""
    boundary = "----RomMBatFreegosyProbe" + os.urandom(8).hex()
    chunks: list[bytes] = []
    for name, (filename, content) in fields.items():
        chunks.append(f"--{boundary}\r\n".encode())
        chunks.append(
            f'Content-Disposition: form-data; name="{name}"; filename="{filename}"\r\n'.encode()
        )
        chunks.append(b"Content-Type: application/octet-stream\r\n\r\n")
        chunks.append(content)
        chunks.append(b"\r\n")
    chunks.append(f"--{boundary}--\r\n".encode())
    return b"".join(chunks), f"multipart/form-data; boundary={boundary}"


def record(name: str, lines: list[str]) -> None:
    """Writes a probe transcript to probe-output/ and echoes it, redacted."""
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    text = redact("\n".join(lines)) + "\n"
    (OUTPUT_DIR / f"{name}.txt").write_text(text, encoding="utf-8")
    print(text, end="")
