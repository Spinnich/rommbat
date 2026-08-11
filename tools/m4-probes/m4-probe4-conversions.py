"""M4 probe 4: the conversions, each of which is silently wrong rather than loud.

RomM's metadata and EmulationStation's gamelist agree on almost nothing about units,
scale, vocabulary or shape. Every one of these is a value that would be written without
error and read as a different fact.

Also censuses what real strings contain, since a gamelist ES cannot parse loses the whole
system rather than one entry.
"""

from __future__ import annotations

import collections
import datetime as dt
import unicodedata

import _common as c

PAGE = 250
PAGES = 20

# Titles whose developer and publisher genuinely differ, to test whether companies[] can
# separate them at all.
KNOWN = [
    ("Chrono Trigger", "snes"),
    ("Star Wars: Knights of the Old Republic", "xbox"),
    ("GoldenEye 007", "n64"),
    ("Crash Bandicoot", "psx"),
]

XML_ILLEGAL = {chr(code) for code in range(0x20)} - {"\t", "\n", "\r"}


def sample(lines: list[str]) -> list[dict]:
    status, first, _ = c.page(1, 0)
    total = first["total"] if status == 200 else 0
    offsets = [round(i * (total - PAGE) / (PAGES - 1)) for i in range(PAGES)]
    rows: list[dict] = []
    for offset in offsets:
        status, body, _ = c.page(PAGE, offset)
        if status == 200:
            rows.extend(body["items"])
    lines.append(f"sampled {len(rows):,} rows of {total:,}")
    return rows


def as_date(value: int) -> str:
    """Reads the integer both ways, so the wrong one is visible rather than assumed."""
    try:
        seconds = dt.datetime.fromtimestamp(value, dt.UTC).strftime("%Y-%m-%d")
    except (OverflowError, OSError, ValueError):
        seconds = "out of range"
    try:
        millis = dt.datetime.fromtimestamp(value / 1000, dt.UTC).strftime("%Y-%m-%d")
    except (OverflowError, OSError, ValueError):
        millis = "out of range"
    return f"as seconds {seconds:<12} as milliseconds {millis}"


def main() -> None:
    lines = ["M4 probe 4: RomM metadata against what a gamelist wants", ""]
    rows = sample(lines)
    lines.append("")

    lines.append("== first_release_date: an integer with no declared unit")
    dated = [row for row in rows if (row.get("metadatum") or {}).get("first_release_date")]
    values = sorted((row["metadatum"]["first_release_date"] for row in dated))
    lines.append(f"  {len(values):,} of {len(rows):,} carry one; min {values[0]}, max {values[-1]}")
    for row in dated[:4]:
        value = row["metadatum"]["first_release_date"]
        lines.append(f"  {(row.get('name') or '')[:34]:<34} {value:>16}  {as_date(value)}")
    seconds_years = collections.Counter(
        dt.datetime.fromtimestamp(v, dt.UTC).year for v in values if 0 < v < 4102444800
    )
    millis_years = collections.Counter(
        dt.datetime.fromtimestamp(v / 1000, dt.UTC).year for v in values if 0 < v / 1000 < 4102444800
    )
    lines.append(f"  read as seconds, the years land in {min(seconds_years or [0])}-{max(seconds_years or [0])}")
    lines.append(f"  read as milliseconds, the years land in {min(millis_years or [0])}-{max(millis_years or [0])}")
    lines.append("  a negative value is a pre-1970 release, which a game genuinely can be:")
    lines.append(f"  values below zero: {sum(1 for v in values if v < 0)}")
    lines.append("")

    lines.append("== average_rating, against a gamelist <rating> of 0-1 to two decimals")
    rated = [
        (row["metadatum"]["average_rating"], row.get("name"))
        for row in rows
        if (row.get("metadatum") or {}).get("average_rating") is not None
    ]
    scores = sorted(value for value, _ in rated)
    lines.append(f"  {len(scores):,} carry one; min {scores[0]:.4f}, max {scores[-1]:.4f}")
    buckets = collections.Counter(int(value // 10) * 10 for value in scores)
    lines.append("  distribution by ten: " + ", ".join(f"{key}-{key + 9}: {count}" for key, count in sorted(buckets.items())))
    lines.append(f"  above 1.0: {sum(1 for value in scores if value > 1.0):,}")
    lines.append("")

    lines.append("== player_count, against a gamelist <players> that a real install writes as 1, 1-2, 1-4")
    counts = collections.Counter((row.get("metadatum") or {}).get("player_count") for row in rows)
    for value, count in counts.most_common(12):
        lines.append(f"  {str(value)!r:<12} {count:>6,}")
    lines.append("")

    lines.append("== companies: can developer and publisher be separated at all")
    lengths = collections.Counter(
        len((row.get("metadatum") or {}).get("companies") or []) for row in rows
    )
    lines.append("  companies[] length: " + ", ".join(f"{k}: {v}" for k, v in sorted(lengths.items())[:10]))
    sorted_count = sum(
        1
        for row in rows
        if (companies := (row.get("metadatum") or {}).get("companies"))
        and companies == sorted(companies)
    )
    with_companies = sum(1 for row in rows if (row.get("metadatum") or {}).get("companies"))
    lines.append(f"  already in sorted order: {sorted_count:,} of {with_companies:,}")
    lines.append("  the per-provider arrays, on titles with a known distinct developer and publisher:")
    for title, slug in KNOWN:
        status, body, _ = c.page(5, 0, search_term=title)
        if status != 200 or not body["items"]:
            continue
        match = next((row for row in body["items"] if row.get("platform_slug") == slug), body["items"][0])
        lines.append(f"    {match.get('name')} [{match.get('platform_slug')}]")
        lines.append(f"      metadatum.companies  {(match.get('metadatum') or {}).get('companies')}")
        for provider in ("igdb_metadata", "ss_metadata", "launchbox_metadata", "gamelist_metadata"):
            block = match.get(provider) or {}
            if "companies" in block:
                lines.append(f"      {provider + '.companies':<21}{block.get('companies')}")
    lines.append("")

    lines.append("== genres, franchises: arrays against single-valued gamelist elements")
    genre_lengths = collections.Counter(len((row.get("metadatum") or {}).get("genres") or []) for row in rows)
    lines.append("  genres[] length: " + ", ".join(f"{k}: {v}" for k, v in sorted(genre_lengths.items())[:8]))
    dupes = sum(
        1
        for row in rows
        if (f := (row.get("metadatum") or {}).get("franchises")) and len(f) != len(set(f))
    )
    lines.append(f"  franchises[] containing a duplicate: {dupes:,}")
    top_genres = collections.Counter(
        genre for row in rows for genre in ((row.get("metadatum") or {}).get("genres") or [])
    )
    lines.append("  commonest genres: " + ", ".join(f"{g}" for g, _ in top_genres.most_common(8)))
    lines.append("")

    lines.append("== regions and languages, against a real install's us/jp/eu/wr and en,fr,de")
    regions = collections.Counter(value for row in rows for value in (row.get("regions") or []))
    langs = collections.Counter(value for row in rows for value in (row.get("languages") or []))
    lines.append("  regions:   " + ", ".join(f"{v}={n}" for v, n in regions.most_common(12)))
    lines.append("  languages: " + ", ".join(f"{v}={n}" for v, n in langs.most_common(12)))
    multi_region = sum(1 for row in rows if len(row.get("regions") or []) > 1)
    lines.append(f"  rows with more than one region: {multi_region:,}")
    lines.append("")

    lines.append("== what real strings contain, since one bad character loses the whole system")
    fields = ("name", "summary", "fs_name")
    stats = {field: collections.Counter() for field in fields}
    worst = {field: "" for field in fields}
    for row in rows:
        for field in fields:
            value = row.get(field) or ""
            if not value:
                continue
            if "&" in value:
                stats[field]["ampersand"] += 1
            if "<" in value or ">" in value:
                stats[field]["angle bracket"] += 1
            if any(ch in XML_ILLEGAL for ch in value):
                stats[field]["control char illegal in XML 1.0"] += 1
            if any(ord(ch) > 0x7F for ch in value):
                stats[field]["non-ASCII"] += 1
            if any(ord(ch) > 0xFFFF for ch in value):
                stats[field]["astral plane"] += 1
            if any(unicodedata.category(ch) == "Cf" for ch in value):
                stats[field]["format char"] += 1
            if len(value) > len(worst[field]):
                worst[field] = value
    for field in fields:
        lines.append(f"  {field}: " + (", ".join(f"{k} {v}" for k, v in stats[field].most_common()) or "nothing notable"))
        lines.append(f"    longest {len(worst[field]):,} chars")

    illegal_in_names = [
        row["fs_name"] for row in rows if any(ch in '<>:"/\\|?*' for ch in row.get("fs_name") or "")
    ]
    lines.append(f"  fs_name containing a character Windows refuses in a path: {len(illegal_in_names):,}")
    for name in illegal_in_names[:5]:
        lines.append(f"    {name}")
    longest = max(rows, key=lambda row: len(row.get("fs_name") or ""))
    lines.append(f"  longest fs_name: {len(longest['fs_name'])} chars, {longest['fs_name']}")

    c.record("m4-probe4-conversions", lines)


if __name__ == "__main__":
    main()
