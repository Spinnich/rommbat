"""M4 probe 2: what share of a real library actually has media and metadata.

The gamelist fields the plan promises are only worth writing if the library carries them.
This samples the library evenly and counts what is present, then measures the size of the
media that is, because M4 makes media count against the disk budget.
"""

from __future__ import annotations

import random
import urllib.error
import urllib.parse
import urllib.request

import _common as c

PAGE = 250
PAGES = 20
SIZE_SAMPLE = 40
ASSET_PREFIX = "/assets/romm/resources/"


def normalise(value: str) -> str:
    trimmed = value.lstrip("/")
    if trimmed.startswith(ASSET_PREFIX.lstrip("/")):
        return "/" + trimmed
    return ASSET_PREFIX + trimmed


def length_of(path: str) -> int | None:
    """The size of one media file, from a one-byte ranged request."""
    url = c.base_url() + urllib.parse.quote(normalise(path), safe="/?=&:%")
    req = urllib.request.Request(url, headers={"Range": "bytes=0-0"}, method="GET")
    try:
        with urllib.request.urlopen(req, timeout=60) as response:
            response.read()
            content_range = response.headers.get("Content-Range")
            if content_range and "/" in content_range:
                return int(content_range.rsplit("/", 1)[1])
            return None
    except urllib.error.HTTPError:
        return None


def percent(count: int, total: int) -> str:
    return f"{count:>6,}  {100 * count / total:5.1f}%" if total else "     0    0.0%"


def main() -> None:
    status, first, _elapsed = c.page(1, 0)
    if status != 200:
        c.record("m4-probe2-media-census", [f"the first page answered {status}"])
        return

    total = first["total"]
    offsets = [round(i * (total - PAGE) / (PAGES - 1)) for i in range(PAGES)]

    rows = []
    for offset in offsets:
        status, body, _elapsed = c.page(PAGE, offset)
        if status != 200:
            continue
        rows.extend(body["items"])

    counts = {
        "path_cover_small": 0,
        "path_cover_large": 0,
        "path_manual": 0,
        "path_video": 0,
        "merged_screenshots": 0,
        "summary": 0,
        "name": 0,
        "regions": 0,
        "languages": 0,
        "revision": 0,
        "youtube_video_id": 0,
    }
    meta_counts = {
        "genres": 0,
        "franchises": 0,
        "companies": 0,
        "collections": 0,
        "age_ratings": 0,
        "game_modes": 0,
        "player_count": 0,
        "first_release_date": 0,
        "average_rating": 0,
    }
    any_metadatum = 0
    per_platform: dict[str, list[int]] = {}

    for row in rows:
        for key in counts:
            value = row.get(key)
            if value:
                counts[key] += 1

        meta = row.get("metadatum") or {}
        if any(meta.get(key) for key in meta_counts):
            any_metadatum += 1
        for key in meta_counts:
            if meta.get(key):
                meta_counts[key] += 1

        slug = row.get("platform_slug") or "(none)"
        bucket = per_platform.setdefault(slug, [0, 0, 0])
        bucket[0] += 1
        if row.get("path_cover_large"):
            bucket[1] += 1
        if row.get("path_video"):
            bucket[2] += 1

    lines = [
        "M4 probe 2: media and metadata coverage on a real library",
        "",
        f"library total {total:,} roms; sampled {len(rows):,} rows across {PAGES} pages of {PAGE}",
        "",
        "== rom-level fields",
    ]
    for key, count in counts.items():
        lines.append(f"  {key:<20} {percent(count, len(rows))}")

    lines += ["", f"== metadatum, populated at all on {percent(any_metadatum, len(rows))}"]
    for key, count in meta_counts.items():
        lines.append(f"  {key:<20} {percent(count, len(rows))}")

    lines += ["", "== the ten platforms with the most sampled rows"]
    lines.append(f"  {'platform':<24} {'rows':>6} {'cover':>8} {'video':>8}")
    for slug, (rows_seen, covers, videos) in sorted(
        per_platform.items(), key=lambda pair: -pair[1][0]
    )[:10]:
        lines.append(
            f"  {slug:<24} {rows_seen:>6} {100 * covers / rows_seen:7.1f}% {100 * videos / rows_seen:7.1f}%"
        )

    lines += ["", f"== media size, from Content-Range on a {SIZE_SAMPLE}-row random sample"]
    random.seed(4)
    for field in ("path_cover_small", "path_cover_large", "path_manual", "path_video"):
        having = [row for row in rows if row.get(field)]
        if not having:
            lines.append(f"  {field:<18} nothing in the sample carries one")
            continue
        picked = random.sample(having, min(SIZE_SAMPLE, len(having)))
        sizes = [size for size in (length_of(row[field]) for row in picked) if size]
        if not sizes:
            lines.append(f"  {field:<18} no size could be read")
            continue
        sizes.sort()
        lines.append(
            f"  {field:<18} n={len(sizes):<4} median={sizes[len(sizes) // 2]:>12,} b  "
            f"min={sizes[0]:>10,}  max={sizes[-1]:>12,}  mean={sum(sizes) // len(sizes):>11,}"
        )

    c.record("m4-probe2-media-census", lines)


if __name__ == "__main__":
    main()
