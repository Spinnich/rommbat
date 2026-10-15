#!/usr/bin/env python3
"""Answer firmware questions about a RetroBat system from the bundled manifest. Read-only.

    python tools/firmware.py rows <system>    what each (emulator, core) row reads, with the
                                              emulator source lines retrobios cites
    python tools/firmware.py gaps <system>    the system's slots against the test server's
                                              firmware, and the md5s to ask the maintainer for
    python tools/firmware.py why <path>       how a slot's accepted files rank, and what decides
                                              [--region-order usa,europe,japan,world]

rows and why work offline, over data/retrobat/bios.json and reference/retrobios/. gaps reads
GET /api/platforms with ROMMBAT_TEST_SERVER and ROMMBAT_TEST_APPROVER_TOKEN through
tools/localenv.py, and matches by md5 across the whole library, as a sync does.

Nothing here downloads firmware. Agents get firmware bytes from the test server alone
(docs/design/decisions/firmware-from-emulator-evidence.md).
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import firmware_sources  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
MANIFEST = ROOT / "data" / "retrobat" / "bios.json"

# FirmwareRegionOrder's vocabulary: a user's region word, and the words a manifest entry uses for it.
REGION_WORDS = {
    "usa": {"north-america", "usa", "us"},
    "europe": {"europe"},
    "japan": {"japan"},
    "world": {"world"},
    "korea": {"korea"},
    "brazil": {"brazil"},
    "asia": {"asia"},
    "oceania": {"oceania", "australia"},
}
DEFAULT_REGION_ORDER = "usa,europe,japan,world"


def load_manifest() -> dict:
    return json.loads(MANIFEST.read_text(encoding="utf-8"))


def system_body(manifest: dict, system: str) -> dict:
    folder = manifest.get("_aliases", {}).get(system, system)
    body = manifest["systems"].get(folder)
    if body is None:
        raise SystemExit(f"{system}: no firmware is listed for it, or it is not a RetroBat system folder.")
    return body


def reader_of(row: firmware_sources.Row) -> str:
    return row.key if row.core else row.emulator


def refs_for(evidence: firmware_sources.Evidence, profiles: list[str], path: str) -> list[str]:
    return [
        f"{name}: {ref}"
        for name in profiles
        for ref in firmware_sources.source_refs(evidence.profiles.get(name, {}), path)
    ]


def describe_accepted(entry: dict) -> str:
    region = ",".join(entry.get("region", [])) or "-"
    priority = f" priority {entry['priority']}" if "priority" in entry else ""
    return f"{entry['md5']}  {region}{priority}  from {', '.join(entry['from'])}"


def rows(args: argparse.Namespace) -> int:
    manifest = load_manifest()
    body = system_body(manifest, args.system)
    folder = manifest.get("_aliases", {}).get(args.system, args.system)
    evidence = firmware_sources.Evidence(ROOT / "reference")
    readers: list[tuple[str, dict]] = [("retrobat", {"note": "RetroBat's list, for the system's default emulator"})]
    readers += [(reader_of(row), sources) for row, sources in evidence.folder_rows.get(folder, [])]

    print(f"{folder}: {body['name']}, {len(body['slots'])} slots, {len(body['reported'])} reported")
    for name, sources in readers:
        if "none" in sources:
            source = f"no retrobios source: {sources['none']}"
        elif sources.get("bizhawk"):
            source = "BizHawk's FirmwareDatabase.cs"
        elif sources.get("profiles"):
            source = "retrobios " + ", ".join(sources["profiles"])
        else:
            source = sources.get("note", "")
        slots = [slot for slot in body["slots"] if name in slot["readers"]]
        reported = [entry for entry in body["reported"] if name in entry["readers"] or "*" in entry["readers"]]
        print(f"\n{name}  ({source})")
        if not slots and not reported:
            print("  reads no firmware RomMBat knows of")
        for slot in slots:
            print(f"  {'required' if slot['required'] else 'optional'}  {slot['path']}")
            for entry in slot["accepted"]:
                print(f"      {describe_accepted(entry)}")
            for group in slot.get("contents", []):
                print(f"      zip members: {', '.join(item['name'] + ' crc32 ' + item['crc32'] for item in group['files'])}")
            if not slot["accepted"] and not slot.get("contents"):
                print("      no identity: nothing to fetch it by")
            for ref in refs_for(evidence, sources.get("profiles", []), slot["path"]):
                print(f"      source_ref {ref}")
        for entry in reported:
            print(f"  reported  {entry['path']}: {entry['why']}")
    return 0


def region_rank(entry: dict, order: list[str]) -> int:
    regions = set(entry.get("region", []))
    return next((index for index, word in enumerate(order) if regions & REGION_WORDS[word]), len(order))


def rank_key(entry: dict, order: list[str]) -> tuple[int, int, int]:
    """BiosPlanner.Rank's key: RetroBat's md5 first, then the source's priority, lower first,
    then the region order. Equal keys at the top are a tie, which a sync reports as Ambiguous."""
    return ("retrobat" not in entry["from"], entry.get("priority", 1 << 31), region_rank(entry, order))


def why(args: argparse.Namespace) -> int:
    order = [word.strip().lower() for word in args.region_order.split(",") if word.strip()]
    unknown = [word for word in order if word not in REGION_WORDS]
    if unknown:
        raise SystemExit(f"unknown region in --region-order: {unknown}. Known: {', '.join(REGION_WORDS)}")
    manifest = load_manifest()
    evidence = firmware_sources.Evidence(ROOT / "reference")
    by_reader: dict[str, list[str]] = {}
    for row, sources in evidence.rows.items():
        bucket = by_reader.setdefault(reader_of(row), [])
        bucket.extend(name for name in sources.get("profiles", []) if name not in bucket)

    wanted = args.path.replace("\\", "/").lower()
    found = 0
    for folder, body in manifest["systems"].items():
        for slot in body["slots"]:
            names = [slot["path"], *slot.get("aliases", [])]
            if not any(wanted in (name.lower(), name.lower().rsplit("/", 1)[-1]) for name in names):
                continue
            found += 1
            print(f"{folder}  {slot['path']}  ({'required' if slot['required'] else 'optional'})")
            print(f"  readers: {', '.join(slot['readers'])}")
            if slot.get("supplement"):
                print(f"  supplement: {slot['supplement']}")
            ranked = sorted(slot["accepted"], key=lambda entry: rank_key(entry, order))
            if ranked:
                print(f"  ranked by RetroBat's md5, then priority, then region order {','.join(order)}:")
            for index, entry in enumerate(ranked, 1):
                retrobat, priority, region = rank_key(entry, order)
                tie = " (tie with the best: Ambiguous if RomM holds both)" if index > 1 and rank_key(entry, order) == rank_key(ranked[0], order) else ""
                print(
                    f"   {index}. {entry['md5']}  retrobat={'yes' if not retrobat else 'no'} "
                    f"priority={entry.get('priority', '-')} region={','.join(entry.get('region', [])) or '-'} (rank {region}){tie}"
                )
                print(f"      from {', '.join(entry['from'])}")
            for group in slot.get("contents", []):
                print(f"  a zip, accepted when every member {', '.join(group['readers'])} lists matches by crc32 and size:")
                for item in group["files"]:
                    print(f"      {item['name']}  crc32 {item['crc32']}" + (f"  {item['size']} B" if "size" in item else ""))
            if not ranked and not slot.get("contents"):
                print("  no identity: RetroBat names it without a hash and no source gives one, so nothing fetches it")
            profiles = sorted({p for r in slot["readers"] for p in by_reader.get(r, [])})
            for ref in refs_for(evidence, profiles, slot["path"]):
                print(f"  source_ref {ref}")
            print()
        for entry in body["reported"]:
            if wanted in (entry["path"].lower(), entry["path"].lower().rsplit("/", 1)[-1]):
                found += 1
                print(f"{folder}  {entry['path']}  reported, never fetched: {entry['why']} ({', '.join(entry['readers'])})\n")
    if not found:
        print(f"No slot or reported file is named {args.path}.")
        return 1
    return 0


def read_server_firmware() -> list[dict]:
    import localenv

    localenv.apply()
    server = os.environ.get("ROMMBAT_TEST_SERVER", "").rstrip("/")
    token = os.environ.get("ROMMBAT_TEST_APPROVER_TOKEN", "")
    if not server or not token:
        raise SystemExit("ROMMBAT_TEST_SERVER and ROMMBAT_TEST_APPROVER_TOKEN are needed; see python tools/localenv.py.")
    request = urllib.request.Request(f"{server}/api/platforms", headers={"Authorization": f"Bearer {token}"})
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            platforms = json.load(response)
    except urllib.error.HTTPError as error:
        raise SystemExit(f"GET /api/platforms answered {error.code}; the token needs firmware.read and platforms.read.")
    except (urllib.error.URLError, TimeoutError) as error:
        raise SystemExit(f"The test server is unreachable: {error}")
    return [
        {**firmware, "platform": platform.get("slug")}
        for platform in platforms
        for firmware in platform.get("firmware") or []
    ]


def gaps(args: argparse.Namespace) -> int:
    manifest = load_manifest()
    body = system_body(manifest, args.system)
    order = DEFAULT_REGION_ORDER.split(",")
    records = read_server_firmware()
    by_md5: dict[str, list[dict]] = {}
    by_name: dict[str, list[dict]] = {}
    for record in records:
        if record.get("md5_hash"):
            by_md5.setdefault(record["md5_hash"].lower(), []).append(record)
        by_name.setdefault(str(record.get("file_name", "")).lower(), []).append(record)

    print(f"{args.system}: {len(body['slots'])} slots against {len(records)} firmware records on the test server\n")
    asks: list[str] = []
    for slot in body["slots"]:
        readers = ", ".join(slot["readers"])
        ranked = sorted(slot["accepted"], key=lambda entry: rank_key(entry, order))
        held = [(entry, record) for entry in ranked for record in by_md5.get(entry["md5"], [])]
        servable = [(entry, record) for entry, record in held if not record.get("missing_from_fs")]
        base = slot["path"].rsplit("/", 1)[-1].lower()
        if servable:
            entry, record = servable[0]
            state = f"in RomM  {record['file_name']} ({record['platform']}), md5 {entry['md5']}"
        elif slot.get("contents") and any(not r.get("missing_from_fs") for r in by_name.get(base, [])):
            state = "a zip of that name is in RomM; a sync checks its members before it lands"
        elif not ranked and not slot.get("contents"):
            state = "no identity: nothing to ask for"
        else:
            state = "missing" + ("; RomM has the record but not the bytes" if held else "")
            for entry in ranked:
                asks.append(f"  {entry['md5']}  for {slot['path']}  ({','.join(entry.get('region', [])) or '-'}; {readers})")
            for group in slot.get("contents", []):
                members = ", ".join(f"{item['name']} crc32 {item['crc32']}" for item in group["files"])
                asks.append(f"  a zip named {slot['path'].rsplit('/', 1)[-1]} holding {members}  ({readers})")
        print(f"{'required' if slot['required'] else 'optional'}  {slot['path']}\n    {state}\n    read by {readers}")

    if asks:
        print("\nAsk the maintainer to upload to RomM, any one per slot (RomMBat matches by content, so the name in RomM does not matter):")
        print("\n".join(asks))
    else:
        print("\nNothing to ask for: RomM holds a file for every identified slot.")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    commands = parser.add_subparsers(dest="command", required=True)
    rows_parser = commands.add_parser("rows", help="what each (emulator, core) row of a system reads")
    rows_parser.add_argument("system")
    rows_parser.set_defaults(run=rows)
    gaps_parser = commands.add_parser("gaps", help="a system's slots against the test server")
    gaps_parser.add_argument("system")
    gaps_parser.set_defaults(run=gaps)
    why_parser = commands.add_parser("why", help="how a slot's accepted files rank")
    why_parser.add_argument("path", help="a slot path, as bios/x.bin, or its file name")
    why_parser.add_argument("--region-order", default=DEFAULT_REGION_ORDER)
    why_parser.set_defaults(run=why)
    args = parser.parse_args()
    return args.run(args)


if __name__ == "__main__":
    sys.exit(main())
