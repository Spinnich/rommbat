#!/usr/bin/env python3
"""Re-derive every number quoted in docs/PLAN.md from the vendored reference data.

Run after ./refresh.sh. If a count moves, the matching section of the plan is stale.
Needs the vendored rommapp/romm files refresh.sh pulls, romm-slugs.txt included.
"""

import json
import pathlib
import re
import sys

HERE = pathlib.Path(__file__).parent
FAIL = []


def check(label, got, expected):
    ok = got == expected
    print(f"  {'ok ' if ok else 'DRIFT'}  {label:52} {got}" + ("" if ok else f"  (plan says {expected})"))
    if not ok:
        FAIL.append(label)


def norm(s):
    return re.sub(r"[^a-z0-9]", "", s.lower())


def read_slugs(path):
    """Parse UniversalPlatformSlug into member name -> slug value.

    Read as text, not imported: the module is only runnable inside RomM's own tree. The
    member names are the vocabulary PLATFORM_FS_ALIASES is written in, so the values alone
    do not decode the alias table. tools/build-platform-map.py reads both files the same
    way, so the two agree on what a pair is.
    """
    return dict(re.findall(r'^\s{4}([A-Z0-9_]+)\s*=\s*"([^"]+)"', path.read_text(encoding="utf-8"), re.M))


def read_aliases(path, members):
    """Parse PLATFORM_FS_ALIASES into folder name -> slug value."""
    block = re.search(r"PLATFORM_FS_ALIASES.*?=\s*\{(.*?)\n\}", path.read_text(encoding="utf-8"), re.S)
    if block is None:
        sys.exit(f"No PLATFORM_FS_ALIASES table in {path.name} -- run ./refresh.sh first")

    pairs = re.findall(r'^\s{4}"([^"]+)":\s*UPS\.([A-Z0-9_]+),', block.group(1), re.M)
    return {folder: members[name] for folder, name in pairs if name in members}


def main():
    systems = {l.strip() for l in (HERE / "systems_names.lst").read_text().splitlines() if l.strip()}

    slugs_file = HERE / "romm-slugs.txt"
    if not slugs_file.exists():
        sys.exit("romm-slugs.txt missing -- run ./refresh.sh first")
    slugs = {l.strip() for l in slugs_file.read_text().splitlines() if l.strip()}

    members = read_slugs(HERE / "romm-platform_slugs.py")
    aliases = read_aliases(HERE / "romm-platform_aliases.py", members)

    # Upstream's own resolution, minus the config-binding layer: identity first, then the
    # alias table. Walked over RetroBat's folder list rather than over the alias table's
    # keys, because core principle 3 says RetroBat decides what folders exist.
    mapping = {}
    for folder in sorted(systems):
        if folder in slugs:
            mapping[folder] = folder
        elif folder in aliases:
            mapping[folder] = aliases[folder]

    print("\nPlatform mapping")
    check("RetroBat systems", len(systems), 240)
    check("RomM known platform slugs", len(slugs), 459)
    check("  ...and the enum members carrying them", len(members), 459)
    check("Folder aliases upstream publishes", len(aliases), 138)
    check("  ...of those, RetroBat system folders", len(set(aliases) & systems), 94)
    check("  ...naming folders RetroBat lacks", len(set(aliases) - systems), 44)

    check("RetroBat folders resolving to a RomM slug", len(mapping), 166)
    check("  ...by identity, the folder being a slug", sum(1 for f, s in mapping.items() if f == s), 72)
    check("  ...via the alias table", sum(1 for f, s in mapping.items() if f != s), 94)
    check("Mapped slugs RomM does not know", len(set(mapping.values()) - slugs), 0)

    unmapped = sorted(systems - set(mapping))
    check("RetroBat systems with no mapping", len(unmapped), 74)

    by_norm = {norm(s) for s in slugs}
    check(
        "  ...of those, resolved by normalization",
        sum(1 for f in unmapped if norm(f) in by_norm),
        1,
    )

    fanout = {}
    for folder, slug in mapping.items():
        fanout.setdefault(slug, []).append(folder)
    multi = {k: v for k, v in fanout.items() if len(v) > 1}
    check("Distinct RomM slugs reached", len(fanout), 148)
    check("RomM slugs mapping to several folders", len(multi), 10)
    check("  ...widest fan-out (arcade)", max(len(v) for v in multi.values()), 7)

    print("\nFirmware")
    bios = json.loads((HERE / "batocera-systems.json").read_text())
    entries = [(system, b["md5"].strip().lower(), b["file"]) for system, v in bios.items() for b in v.get("biosFiles", [])]

    # The blank string is filtered here and nowhere else, because an entry carrying no md5
    # cannot be joined in either direction: RomMBat can neither find such a file in RomM nor
    # recognise it on disk. Counting it as a requirement inflated every number below by one
    # and made "unknown to RomM" claim a hash that does not exist.
    rb_md5 = {md5 for _system, md5, _file in entries if md5}
    check("RetroBat systems in the BIOS manifest", len(bios), 100)
    check("BIOS entries total", len(entries), 355)
    check("  ...of those carrying no md5 at all", sum(1 for _s, md5, _f in entries if not md5), 181)
    check(
        "  ...systems with no joinable entry at all",
        sum(1 for v in bios.values() if v.get("biosFiles") and not any(b["md5"].strip() for b in v["biosFiles"])),
        29,
    )
    check("Distinct md5s RetroBat requires", len(rb_md5), 156)

    known = json.loads((HERE / "romm-known_bios_files.json").read_text())
    rm_md5 = {v["md5"].lower() for v in known.values() if isinstance(v, dict) and v.get("md5")}
    check("Distinct md5s RomM knows", len(rm_md5), 353)
    check("Overlap", len(rb_md5 & rm_md5), 63)
    check("RetroBat-required, unknown to RomM", len(rb_md5 - rm_md5), 93)

    # Shapes M5's writer depends on. Each one is a rule in code, so a drift here is a bug
    # waiting to happen rather than a statistic.
    paths_by_md5 = {}
    md5s_by_path = {}
    systems_by_path = {}
    for system, md5, file in entries:
        systems_by_path.setdefault(file, set()).add(system)
        if md5:
            paths_by_md5.setdefault(md5, set()).add(file)
            md5s_by_path.setdefault(file, set()).add(md5)

    check("One md5 owing several destination paths", sum(1 for p in paths_by_md5.values() if len(p) > 1), 6)
    check("One destination path taking several md5s", sum(1 for m in md5s_by_path.values() if len(m) > 1), 0)

    # The other direction, and it is a different question. A path several systems require is
    # planned once per system, so the writer has to act on a destination once rather than once
    # per step: msx1, msx2, msx2+ and msxturbor all want bios/openMSX/.../fmpac.rom.
    shared = {f for f, s in systems_by_path.items() if len(s) > 1}
    check("One destination path required by several systems", len(shared), 6)
    check(
        "  ...of those, joinable",
        sum(1 for f in shared if md5s_by_path.get(f)),
        5,
    )
    check("  ...widest, in systems", max(len(systems_by_path[f]) for f in shared), 4)
    check("Entries landing outside bios/", sum(1 for _s, _m, f in entries if not f.startswith("bios/")), 7)
    check(
        "  ...of those, joinable",
        sum(1 for _s, md5, f in entries if md5 and not f.startswith("bios/")),
        0,
    )
    check("Entries under bios/mame/", sum(1 for _s, _m, f in entries if f.startswith("bios/mame/")), 64)
    check(
        "  ...of those, joinable",
        sum(1 for _s, md5, f in entries if md5 and f.startswith("bios/mame/")),
        0,
    )
    check("Deepest destination path, in segments", max(f.count("/") + 1 for _s, _m, f in entries), 6)
    check("Manifest keys that are not a RetroBat system", len(set(bios) - systems), 2)

    print("\nGamelist export")
    exporter = (HERE / "romm-gamelist_exporter.py").read_text(encoding="utf-8")
    # Behaviours rather than counts: M4 reads these off RomM's own exporter, and each is a
    # conversion that would be silently wrong if upstream changed it.
    check(
        "first_release_date is milliseconds",
        "datetime.fromtimestamp(timestamp / 1000" in exporter,
        True,
    )
    check(
        "average_rating is divided by 100",
        "rom.metadatum.average_rating / 100" in exporter,
        True,
    )
    check(
        "releasedate format string",
        '"%Y%m%dT%H%M%S"' in exporter,
        True,
    )
    check(
        "rating written to two decimals",
        'f"{gamelist_rating:.2f}"' in exporter,
        True,
    )
    # The plan diverges here deliberately: companies[] is alphabetically sorted, so indexing
    # it writes the alphabet into two role-bearing fields. See finding 98.
    check(
        "upstream still indexes companies for developer/publisher",
        "rom.metadatum.companies[0]" in exporter and "rom.metadatum.companies[1]" in exporter,
        True,
    )
    check(
        "upstream marquee is sourced from the ScreenScraper logo",
        '"marquee": [ss.get("logo_path"' in exporter,
        True,
    )
    check(
        "gamelist elements RomMBat writes are all present upstream",
        sum(
            1
            for tag in (
                "path", "name", "desc", "image", "thumbnail", "marquee", "video", "manual",
                "developer", "publisher", "genre", "family", "players", "lang", "region",
                "releasedate", "rating",
            )
            if f'"{tag}"' in exporter
        ),
        17,
    )

    print()
    if FAIL:
        print(f"{len(FAIL)} value(s) drifted. Revisit docs/PLAN.md before relying on them.")
        return 1
    print("All reference-derived values match the plan.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
