#!/usr/bin/env python3
"""Re-derive every number quoted in docs/PLAN.md from the vendored reference data.

Run after ./refresh.sh. If a count moves, the matching section of the plan is stale.
Needs romm-slugs.txt, which refresh.sh extracts from rommapp/romm.
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


def main():
    systems = {l.strip() for l in (HERE / "systems_names.lst").read_text().splitlines() if l.strip()}

    body = (HERE / "config.batocera-retrobat.yml").read_text().split("platforms:", 1)[1]
    mapping = dict(
        re.findall(r"^\s{4}([A-Za-z0-9_\-.]+):\s*([A-Za-z0-9_\-.]+)\s*$", body, re.M)
    )

    slugs_file = HERE / "romm-slugs.txt"
    if not slugs_file.exists():
        sys.exit("romm-slugs.txt missing -- run ./refresh.sh first")
    slugs = {l.strip() for l in slugs_file.read_text().splitlines() if l.strip()}

    print("\nPlatform mapping")
    check("RetroBat systems", len(systems), 240)
    check("RomM known platform slugs", len(slugs), 457)
    check("Explicit pairs in the YAML", len(mapping), 168)

    unmapped = sorted(systems - set(mapping))
    check("RetroBat systems with no mapping", len(unmapped), 91)

    by_norm = {norm(s) for s in slugs}
    check(
        "  ...of those, resolved by normalization",
        sum(1 for f in unmapped if norm(f) in by_norm),
        16,
    )
    check("YAML entries naming folders RetroBat lacks", len(set(mapping) - systems), 19)

    fanout = {}
    for folder, slug in mapping.items():
        fanout.setdefault(slug, []).append(folder)
    multi = {k: v for k, v in fanout.items() if len(v) > 1}
    check("RomM slugs mapping to several folders", len(multi), 13)
    check("  ...widest fan-out (arcade)", max(len(v) for v in multi.values()), 10)

    print("\nFirmware")
    bios = json.loads((HERE / "batocera-systems.json").read_text())
    rb_md5 = {b["md5"].lower() for v in bios.values() for b in v.get("biosFiles", [])}
    check("RetroBat systems in the BIOS manifest", len(bios), 99)
    check(
        "BIOS entries total",
        sum(len(v.get("biosFiles", [])) for v in bios.values()),
        353,
    )
    check("Distinct md5s RetroBat requires", len(rb_md5), 157)

    known = json.loads((HERE / "romm-known_bios_files.json").read_text())
    rm_md5 = {v["md5"].lower() for v in known.values() if isinstance(v, dict) and v.get("md5")}
    check("Distinct md5s RomM knows", len(rm_md5), 353)
    check("Overlap", len(rb_md5 & rm_md5), 63)
    check("RetroBat-required, unknown to RomM", len(rb_md5 - rm_md5), 94)

    print()
    if FAIL:
        print(f"{len(FAIL)} value(s) drifted. Revisit docs/PLAN.md before relying on them.")
        return 1
    print("All reference-derived values match the plan.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
