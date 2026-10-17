#!/usr/bin/env python3
"""Re-derive every number quoted in reference/README.md from the vendored reference data.

Run after ./refresh.sh. If a count moves, every doc and skill citing it is stale.
Needs the vendored rommapp/romm files refresh.sh pulls, romm-slugs.txt included.
"""

import json
import os
import pathlib
import re
import sys

# A scout points this at a scratch refresh at a prerelease tag; the default is the vendored copy.
HERE = pathlib.Path(os.environ.get("ROMMBAT_REFERENCE_DIR") or pathlib.Path(__file__).parent)
FAIL = []


def check(label, got, expected):
    ok = got == expected
    print(f"  {'ok ' if ok else 'DRIFT'}  {label:52} {got}" + ("" if ok else f"  (docs say {expected})"))
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


def firmware_evidence(bios):
    """What retrobios adds to RetroBat's hashless entries, by the rules the manifest follows."""
    sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent / "tools"))
    import firmware_sources

    evidence = firmware_sources.Evidence(HERE)
    print(f"\nFirmware evidence (retrobios {evidence.ref[:12]})")
    rows = list(evidence.rows.values())
    # Not a statistic: a row nothing describes must be resolved in firmware-rows.json or marked.
    check("RetroBat rows no source describes and nothing marks", len(evidence.unresolved), 0)
    check("Row-map entries no RetroBat row uses", evidence.unused, [])
    check("retrobios profiles vendored", len(evidence.profiles), 221)
    check("RetroBat rows", len(rows), 709)
    check("  ...described by a retrobios profile", sum(1 for s in rows if s.get("profiles")), 562)
    check("  ...described by BizHawk's firmware list", sum(1 for s in rows if s.get("bizhawk")), 55)
    check("  ...marked as having no retrobios source", sum(1 for s in rows if "none" in s), 92)

    shapes = {}
    for key, body in bios.items():
        folder = {"astrocde": "astrocade", "msx": "msx1"}.get(key, key)
        for entry in body.get("biosFiles", []):
            if entry["md5"].strip():
                continue
            path = entry["file"]
            name = path.rsplit("/", 1)[-1]
            if not path.startswith("bios/"):
                shape = "outside"
            elif name.lower().endswith(".xml"):
                shape = "xml"
            elif name.lower().endswith(".zip"):
                shape = "zip+" if evidence.zip_contents(folder, name) else "zip"
            elif evidence.stated_md5s(folder, name):
                shape = "stated"
            elif evidence.catalog_md5(name):
                shape = "catalog"
            elif evidence.romm_md5(name):
                shape = "romm"
            else:
                shape = "none"
            shapes[shape] = shapes.get(shape, 0) + 1

    # MAME's software lists are hash metadata RetroBat ships under bios/mame/hash, not firmware.
    check("Hashless entries that are MAME software lists (.xml)", shapes.get("xml", 0), 63)
    check("Hashless BIOS zips", shapes.get("zip", 0) + shapes.get("zip+", 0), 64)
    check("  ...whose contents a row's profile lists", shapes.get("zip+", 0), 24)
    check("Hashless single files under bios/", sum(shapes.get(k, 0) for k in ("stated", "catalog", "romm", "none")), 53)
    check("  ...a profile or BizHawk states a hash for", shapes.get("stated", 0), 7)
    check("  ...named exactly as one dump-catalog entry", shapes.get("catalog", 0), 3)
    check("  ...RomM's known_bios_files names one hash for", shapes.get("romm", 0), 12)
    check("  ...with no identity anywhere", shapes.get("none", 0), 31)

    # Not statistics: each is a hand-maintained map in firmware-rows.json that must stay whole.
    check("Folders whose multi-system profiles have no system map", evidence.unmapped_systems, [])
    check("Folders with a BizHawk row and no BizHawk system map", evidence.unmapped_bizhawk, [])
    check("System map ids no source of the folder uses", evidence.stale_systems, [])
    check("System map entries for folders that need none", evidence.unused_systems, [])

    manifest_slots()


def manifest_slots():
    """The slots tools/build-bios-manifest.py derives, which BiosManifestTests holds the bundled copy to."""
    import importlib.util

    builder_path = pathlib.Path(__file__).resolve().parent.parent / "tools" / "build-bios-manifest.py"
    spec = importlib.util.spec_from_file_location("build_bios_manifest", builder_path)
    builder = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(builder)
    builder.REFERENCE = HERE
    document = builder.build()
    systems = document["systems"]
    slots = [slot for body in systems.values() for slot in body["slots"]]
    retrobat = [slot for slot in slots if "retrobat" in slot["readers"]]

    print("\nFirmware manifest (data/retrobat/bios.json)")
    check("Folders with a slot", sum(1 for body in systems.values() if body["slots"]), 127)
    check("Slots", len(slots), 943)
    check("  ...RetroBat's list names", len(retrobat), 296)
    check("  ...of those, with RetroBat's md5", sum(1 for s in retrobat if any("retrobat" in a["from"] for a in s["accepted"])), 179)
    check(
        "  ...of those, hashless and identified by another source",
        sum(1 for s in retrobat if not any("retrobat" in a["from"] for a in s["accepted"]) and (s["accepted"] or s.get("contents"))),
        46,
    )
    check("  ...with no identity anywhere", sum(1 for s in slots if not s["accepted"] and not s.get("contents")), 71)
    check("  ...zips identified by their members", sum(1 for s in slots if s.get("contents")), 331)
    check("  ...where several accepted files compete", sum(1 for s in slots if len(s["accepted"]) > 1), 14)
    # Not a statistic: a pass acts on a path once, so every folder's slot on it must agree.
    agreed = {}
    for s in slots:
        agreed.setdefault(s["path"].lower(), set()).add(
            (tuple(a["md5"] for a in s["accepted"]), tuple(sorted(str(g["files"]) for g in s.get("contents", []))))
        )
    check("Shared paths whose slots accept different files", sorted(p for p, v in agreed.items() if len(v) > 1), [])
    check("Distinct accepted md5s", len({a["md5"] for s in slots for a in s["accepted"]}), 352)
    check("Distinct destination paths", len({s["path"] for s in slots}), 615)
    check("Reported, never fetched", sum(len(body["reported"]) for body in systems.values()), 2287)
    check(
        "  ...MAME software lists",
        sum(1 for body in systems.values() for r in body["reported"] if "software list" in r["why"]),
        63,
    )


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
    check("RetroBat systems", len(systems), 241)
    check("RomM known platform slugs", len(slugs), 459)
    check("  ...and the enum members carrying them", len(members), 459)
    check("Folder aliases upstream publishes", len(aliases), 138)
    check("  ...of those, RetroBat system folders", len(set(aliases) & systems), 93)
    check("  ...naming folders RetroBat lacks", len(set(aliases) - systems), 45)

    check("RetroBat folders resolving to a RomM slug", len(mapping), 166)
    check("  ...by identity, the folder being a slug", sum(1 for f, s in mapping.items() if f == s), 73)
    check("  ...via the alias table", sum(1 for f, s in mapping.items() if f != s), 93)
    check("Mapped slugs RomM does not know", len(set(mapping.values()) - slugs), 0)

    unmapped = sorted(systems - set(mapping))
    check("RetroBat systems with no mapping", len(unmapped), 75)

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
    # recognize it on disk. Counting it as a requirement inflated every number below by one
    # and made "unknown to RomM" claim a hash that does not exist.
    rb_md5 = {md5 for _system, md5, _file in entries if md5}
    check("RetroBat systems in the BIOS manifest", len(bios), 101)
    check("BIOS entries total", len(entries), 360)
    check("  ...of those carrying no md5 at all", sum(1 for _s, md5, _f in entries if not md5), 186)
    check(
        "  ...systems with no joinable entry at all",
        sum(1 for v in bios.values() if v.get("biosFiles") and not any(b["md5"].strip() for b in v["biosFiles"])),
        30,
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
    check("Entries landing outside bios/", sum(1 for _s, _m, f in entries if not f.startswith("bios/")), 6)
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

    firmware_evidence(bios)

    print("\nGamelist export")
    exporter = (HERE / "romm-gamelist_exporter.py").read_text(encoding="utf-8")
    datetime_utils = (HERE / "romm-datetime.py").read_text(encoding="utf-8")
    # Behaviors rather than counts: M4 reads these off RomM's own exporter, and each is a
    # conversion that would be silently wrong if upstream changed it.
    check(
        "first_release_date is milliseconds",
        "format_utc(timestamp," in exporter and "datetime.fromtimestamp(timestamp_ms / 1000" in datetime_utils,
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
    # Upstream stopped indexing companies[] at 5.3.0 and reads the split roles instead.
    # Both halves are checked: the second is what flips if the indexing ever comes back.
    # The divergence this replaced ends per row rather than outright, because
    # primary_developer falls back to companies[0] wherever developers is empty, and a row
    # populates the split only when it is rescanned. See RB-98 and RM-7.
    check(
        "upstream reads developer and publisher off the split roles",
        "rom.metadatum.primary_developer" in exporter
        and "rom.metadatum.primary_publisher" in exporter,
        True,
    )
    check(
        "upstream indexes companies for developer/publisher",
        "rom.metadatum.companies[0]" in exporter and "rom.metadatum.companies[1]" in exporter,
        False,
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
        print(f"{len(FAIL)} value(s) drifted. Revisit reference/README.md and the docs citing them.")
        return 1
    print("All reference-derived values match the docs.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
