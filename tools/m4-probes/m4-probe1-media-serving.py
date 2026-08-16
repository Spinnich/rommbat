"""M4 probe 1: how RomM's media paths actually serve.

The plan assumes M3's download path carries over to media. It does not follow: the four
media fields are static resource paths rather than /api/.../content routes. This measures
what they are, whether the device token matters, whether ranges work, and what the two
`url_*` fields point at.
"""

from __future__ import annotations

import urllib.error
import urllib.parse
import urllib.request
import time

import _common as c

ASSET_PREFIX = "/assets/romm/resources/"

FIELDS = ("path_cover_small", "path_cover_large", "path_manual", "path_video")


def normalise(value: str) -> str:
    """Puts either shape of media path onto the asset prefix exactly once."""
    trimmed = value.lstrip("/")
    if trimmed.startswith(ASSET_PREFIX.lstrip("/")):
        return "/" + trimmed
    return ASSET_PREFIX + trimmed


def fetch(path: str, *, auth: bool, rng: str | None = None):
    """One GET, returning (status, headers, length, first bytes, seconds)."""
    joined = path if path.startswith("/") else "/" + path
    url = c.base_url() + urllib.parse.quote(joined, safe="/?=&:%")
    headers = {}
    if auth:
        headers["Authorization"] = f"Bearer {c.token()}"
    if rng:
        headers["Range"] = rng

    req = urllib.request.Request(url, headers=headers, method="GET")
    started = time.monotonic()
    try:
        with urllib.request.urlopen(req, timeout=120) as response:
            body = response.read()
            return response.status, dict(response.headers), len(body), body[:16], time.monotonic() - started
    except urllib.error.HTTPError as err:
        body = err.read()
        return err.code, dict(err.headers), len(body), body[:16], time.monotonic() - started


def describe(status, headers, length, head, elapsed) -> str:
    return (
        f"{status:>4}  {length:>10,} b  {headers.get('Content-Type', '-'):<26}"
        f"etag={headers.get('ETag', '-'):<22} ranges={headers.get('Accept-Ranges', '-'):<6}"
        f"{elapsed:.3f}s  first bytes={head!r}"
    )


def find_rich_rom(lines: list[str]):
    """The first ROM in the library carrying all four media fields."""
    for offset in range(0, 4000, 250):
        status, body, _ = c.page(250, offset)
        if status != 200:
            lines.append(f"page at offset {offset} answered {status}")
            return None
        for row in body["items"]:
            if all(row.get(field) for field in FIELDS):
                return row
    return None


def main() -> None:
    lines: list[str] = ["M4 probe 1: how RomM's media paths serve", ""]

    rom = find_rich_rom(lines)
    if rom is None:
        lines.append("no ROM in the first 4,000 carried all four media fields")
        c.record("m4-probe1-media-serving", lines)
        return

    lines.append(f"rom id {rom['id']}, platform {rom['platform_slug']}, name {rom.get('name')!r}")
    lines.append("")
    lines.append("== the field values, verbatim")
    for field in FIELDS:
        lines.append(f"  {field:<18} {rom[field]}")
    lines.append(f"  {'merged_screenshots':<18} {(rom.get('merged_screenshots') or ['(none)'])[0]}")
    lines.append("")

    lines.append("== url_cover and url_manual: what host do they name")
    for field in ("url_cover", "url_manual"):
        value = rom.get(field)
        host = urllib.parse.urlparse(value).netloc if value else "(null)"
        # The query carries a third party's API credentials, so only the host is recorded.
        lines.append(f"  {field:<12} host={host}")
    lines.append("")

    lines.append("== each field as given, and normalised onto the asset prefix, authenticated and not")
    for field in FIELDS:
        value = rom[field]
        for candidate, label in (
            (value, "as given"),
            (normalise(value), "prefixed"),
        ):
            for auth in (True, False):
                result = fetch(candidate, auth=auth)
                lines.append(
                    f"  {field:<18} {label:<9} {'bearer' if auth else 'anon  '}  {describe(*result)}"
                )
        lines.append("")

    lines.append("== ranges, on the normalised cover")
    target = normalise(rom["path_cover_large"])
    status, headers, length, head, elapsed = fetch(target, auth=True, rng="bytes=0-99")
    lines.append(
        f"  Range: bytes=0-99 -> {status}, {length} b, "
        f"Content-Range={headers.get('Content-Range', '-')}, ETag={headers.get('ETag', '-')}, {elapsed:.3f}s"
    )
    status, headers, length, head, elapsed = fetch(target, auth=True, rng="bytes=99999999-")
    lines.append(f"  Range past the end -> {status}, {length} b, {elapsed:.3f}s")

    c.record("m4-probe1-media-serving", lines)


if __name__ == "__main__":
    main()
