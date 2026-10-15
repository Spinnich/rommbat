#!/usr/bin/env python3
"""Emit data/retrobat/bios.json: every firmware file a RetroBat system's emulators read, per slot.

Two vendored authorities, split by question (docs/design/decisions/firmware-from-emulator-evidence.md):

  * RetroBat decides where a file goes. Its batocera-systems.json names a destination and,
    for 174 of 360 entries, an md5, for each system's default emulator. Its path is kept
    exactly, and its md5 ranks first, because ES's BIOS check reports the file missing otherwise
  * retrobios's emulator profiles, and BizHawk's FirmwareDatabase.cs as it scrapes it, decide
    which files and hashes each emulator RetroBat ships reads, cited to the emulator's source.
    They add slots RetroBat's list does not name, and identity to entries it names without a hash

The unit is a slot: one destination path, the files accepted there, and the rows that read it.

  * a slot's path is RetroBat's where it names one; otherwise the emulator's directory, which
    data/retrobat/firmware-rows.json cites to an RB- fact, joined to the profile's path or name
  * BizHawk matches files by hash, not name, so its entries join any slot already accepting
    their hash and add a slot of their own only for a hash no other slot takes
  * a single file's identity is a hash a source states, a sha1 converted through database.json,
    an exact dump-catalog name with one hash, or RomM's known_bios_files naming one hash, in that
    order. A name alone identifies nothing: retrobios's own index files the Store Display Unit
    BIOS under bios_U.sms
  * a zip is identified by the crc32 and size of the files a reader lists inside it, never by
    its container md5 (RM-28)
  * a file with no identity, a path outside bios/, a MAME software list and firmware an emulator
    installs through its own menu are reported, never slotted, so nothing fetches them
  * a system can take entries RetroBat lists under another system, where a row of it was
    measured reading them. Each is copied from that system's entry and tagged with why

Usage: python tools/build-bios-manifest.py [--check]
"""

from __future__ import annotations

import json
import os
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import firmware_sources  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
# A scout points this at a scratch refresh to see what the next adoption would regenerate.
REFERENCE = Path(os.environ.get("ROMMBAT_REFERENCE_DIR") or ROOT / "reference")
OUTPUT = ROOT / "data" / "retrobat" / "bios.json"

SCHEMA = 2

# The manifest is keyed by batocera system names, which is a third vocabulary beside
# es_systems.cfg's <name> and its <path> basename. 99 of the 101 keys are exactly a <path>
# basename; these two are not, and each names a system RetroBat does have under another
# folder. Checked below, so a new divergence fails the build rather than silently dropping a
# system's firmware.
ALIASES = {
    "astrocde": "astrocade",
    "msx": "msx1",
}

# Entries RetroBat's manifest lists under one system that another system's rows read. Keyed by
# the system that takes them; each source names the system they are copied from, the paths, and
# the measurement.
SUPPLEMENT = {
    "gb": [
        {
            "from": "sgb",
            "paths": ["bios/sgb_boot.bin", "bios/sgb2_boot.bin", "bios/SGB1.sfc", "bios/SGB2.sfc"],
            "evidence": (
                "RetroBat's Game Boy wiki page lists these beside gb_bios.bin. gb under "
                "libretro/bsnes on 8.2.1 runs a .gb as a Super Game Boy cartridge: without SGB1.sfc "
                "RetroArch logged Failed to load content, and with it Pokemon Yellow booted in its "
                "SGB border"
            ),
        },
        {
            "from": "gbc",
            "paths": ["bios/gbc_bios.bin"],
            "evidence": (
                "gb under bizhawk/GBHawk on 8.2.1 runs a cartridge whose header flags Game Boy Color "
                "support in Color mode, and refused Pokemon Yellow with Couldn't find required "
                "firmware GBC+World until gbc_bios.bin was present. It takes gb_bios.bin for a mono "
                "cartridge. RetroBat offers no option for GBHawk's console mode"
            ),
        },
    ],
}

RETROBAT = "retrobat"
INSIDE = "bios/"

# No-Intro's region words, as a catalog name writes them, to retrobios's vocabulary.
CATALOG_REGIONS = {
    "usa": "north-america",
    "europe": "europe",
    "japan": "japan",
    "world": "world",
    "korea": "korea",
    "brazil": "brazil",
    "asia": "asia",
    "australia": "oceania",
}


def norm(path: str) -> str:
    path = str(path).replace("\\", "/").strip()
    while path.startswith("./"):
        path = path[2:]
    path = path.lstrip("/")
    if ".." in path.split("/"):
        raise SystemExit(f"a firmware path climbs out of its directory: {path}")
    return path


def join(directory: str, name: str) -> str:
    return norm(f"{directory.rstrip('/')}/{norm(name)}")


class Slot:
    def __init__(self, path: str):
        self.path = path
        self.accepted: dict[str, dict] = {}
        self.readers: set[str] = set()
        self.required = False
        self.aliases: set[str] = set()
        self.supplement: str | None = None
        self.contents: list[dict] = []
        self.retrobat_named = False

    def accept(self, md5: str, origin: str, **facts) -> None:
        md5 = md5.lower()
        current = self.accepted.get(md5)
        if current is None:
            self.accepted[md5] = {"md5": md5, **{k: v for k, v in facts.items() if v not in (None, [])}, "from": [origin]}
            return
        if origin not in current["from"]:
            current["from"].append(origin)
        for key, value in facts.items():
            if value in (None, []):
                continue
            if key == "priority":
                current[key] = min(current.get(key, value), value)
            elif key == "region":
                current[key] = sorted(set(current.get(key, [])) | set(value))
            else:
                current.setdefault(key, value)

    def add_contents(self, files: list[dict], readers: set[str]) -> None:
        for group in self.contents:
            if group["files"] == files:
                group["readers"] = sorted(set(group["readers"]) | readers)
                return
        self.contents.append({"readers": sorted(readers), "files": files})

    @property
    def identified(self) -> bool:
        return bool(self.accepted) or bool(self.contents)

    def render(self) -> dict:
        def rank(entry: dict) -> tuple:
            return (RETROBAT not in entry["from"], entry.get("priority", 1 << 30), entry["md5"])

        body: dict = {
            "path": self.path,
            "accepted": [{**entry, "from": sorted(entry["from"])} for entry in sorted(self.accepted.values(), key=rank)],
            "readers": sorted(self.readers),
            "required": self.required,
        }
        if self.aliases:
            body["aliases"] = sorted(self.aliases)
        if self.supplement:
            body["supplement"] = self.supplement
        if self.contents:
            body["contents"] = sorted(self.contents, key=lambda group: group["readers"])
        return body


class Folder:
    def __init__(self, name: str):
        self.name = name
        self.slots: dict[str, Slot] = {}
        self.reported: dict[tuple[str, str], dict] = {}

    def slot(self, path: str) -> Slot:
        key = path.lower()
        if key not in self.slots:
            self.slots[key] = Slot(path)
        return self.slots[key]

    def report(self, path: str, why: str, readers: set[str]) -> None:
        key = (path.lower(), why)
        entry = self.reported.setdefault(key, {"path": path, "why": why, "readers": set()})
        entry["readers"] |= readers


def catalog_region(evidence: firmware_sources.Evidence, md5: str) -> list[str]:
    row = evidence.row_by_md5(md5)
    name = ((row or {}).get("provenance") or {}).get("no-intro", {}).get("name", "")
    found: list[str] = []
    for group in re.findall(r"\(([^)]*)\)", name):
        words = [word.strip().lower() for word in group.split(",")]
        if words and all(word in CATALOG_REGIONS for word in words):
            found = [CATALOG_REGIONS[word] for word in words]
            break
    return found


def regions(entry: dict) -> list[str]:
    value = entry.get("region")
    if not value:
        return []
    return [str(item).lower() for item in ([value] if isinstance(value, str) else value)]


def stated(evidence: firmware_sources.Evidence, entry: dict) -> list[tuple[str, dict]]:
    """The md5s an entry itself states, a sha1 converted through database.json, with the size
    the source or the database gives."""
    found: list[tuple[str, dict]] = []
    size = entry.get("size") if isinstance(entry.get("size"), int) else None
    for md5 in firmware_sources._hashes(entry.get("md5")):
        if re.fullmatch(r"[0-9a-f]{32}", md5):
            found.append((md5, {"size": size}))
    for sha1 in firmware_sources._hashes(entry.get("sha1")):
        md5 = evidence.md5_of_sha1(sha1)
        if md5 and md5 not in {m for m, _ in found}:
            found.append((md5, {"sha1": sha1, "size": size or evidence.by_sha1[sha1]["size"]}))
    return found


def contents_of(entry: dict) -> list[dict]:
    """The inner files a zip entry lists with a crc32, which is what identifies them."""
    files = []
    for inner in entry.get("contents") or []:
        if isinstance(inner, dict) and inner.get("crc32") and not inner.get("bad_dump"):
            item = {"name": str(inner["name"]), "crc32": str(inner["crc32"]).lower()}
            if isinstance(inner.get("size"), int):
                item["size"] = inner["size"]
            files.append(item)
    return sorted(files, key=lambda item: item["name"].lower())


def archives_of(files: list[dict]) -> dict[str, list[dict]]:
    """Zips a profile describes member by member, through an 'archive' key on each member. Only
    the members it marks required count where it marks any, since the rest are alternatives."""
    grouped: dict[str, list[dict]] = {}
    for entry in files:
        if entry.get("archive") and entry.get("crc32"):
            grouped.setdefault(str(entry["archive"]), []).append(entry)
    found = {}
    for archive, members in grouped.items():
        required = [member for member in members if member.get("required")]
        chosen = required or members
        found[archive] = sorted(
            (
                {"name": str(m["name"]), "crc32": str(m["crc32"]).lower(), **({"size": m["size"]} if isinstance(m.get("size"), int) else {})}
                for m in chosen
            ),
            key=lambda item: item["name"].lower(),
        )
    return found


def single_file_identity(evidence, folder: str, name: str) -> tuple[list[tuple[str, dict]], str | None]:
    """Identity for a file no source states a hash for at this entry: a hash another of the
    folder's sources states under the same name, then a dump-catalog name, then RomM's list."""
    md5s = sorted(evidence.stated_md5s(folder, name))
    if md5s:
        return [(md5, {}) for md5 in md5s], "named"
    if md5 := evidence.catalog_md5(name):
        return [(md5, {})], "catalog"
    if md5 := evidence.romm_md5(name):
        return [(md5, {})], "romm"
    return [], None


def build() -> dict:
    evidence = firmware_sources.Evidence(REFERENCE)
    for label, problems in (
        ("rows no retrobios source describes and nothing marks", [r.key for r in evidence.unresolved]),
        ("folders whose multi-system profiles need a 'systems' entry in firmware-rows.json", evidence.unmapped_systems),
        ("folders with a BizHawk row and no 'bizhawk_systems' entry", evidence.unmapped_bizhawk),
        ("system ids no source of the folder uses", evidence.stale_systems),
        ("system map entries for folders that need none", evidence.unused_systems),
    ):
        if problems:
            raise SystemExit(f"{label}: {problems}")

    manifest = json.loads((REFERENCE / "batocera-systems.json").read_text(encoding="utf-8"))
    known = {line.strip() for line in (REFERENCE / "systems_names.lst").read_text(encoding="utf-8").splitlines() if line.strip()}

    unresolved = sorted(key for key in manifest if key not in known and ALIASES.get(key) not in known)
    if unresolved:
        raise SystemExit(f"manifest keys that are no RetroBat system and have no alias: {unresolved}")
    stale = sorted(key for key in ALIASES if key in known)
    if stale:
        raise SystemExit(f"aliases for keys RetroBat now lists directly, so they should go: {stale}")

    row_map = evidence.row_map
    directories = row_map.get("directories", {})
    installed = row_map.get("installed_by_emulator", {})

    # RetroBat's own entries, supplement included, keyed by folder.
    listed: dict[str, list[dict]] = {}
    names: dict[str, str] = {}
    for key, body in sorted(manifest.items()):
        folder = ALIASES.get(key, key)
        names[folder] = body.get("name", key)
        listed.setdefault(folder, []).extend({"md5": e["md5"].strip().lower() or None, "path": e["file"]} for e in body.get("biosFiles", []))
    for key, specs in SUPPLEMENT.items():
        for spec in specs:
            source = {entry["path"]: entry for entry in listed[spec["from"]]}
            taken = {entry["path"] for entry in listed[key]}
            for path in spec["paths"]:
                if path not in source:
                    raise SystemExit(f"{spec['from']} no longer lists {path}, which {key} is supplemented with")
                if path in taken:
                    raise SystemExit(f"{key} already lists {path}, so its supplement entry should go")
                listed[key].append({**source[path], "supplement": spec["evidence"]})

    folders: dict[str, Folder] = {}

    def folder_for(name: str) -> Folder:
        return folders.setdefault(name, Folder(name))

    # 1. RetroBat's list. Its path is the destination, and its md5 ranks first.
    for name, entries in listed.items():
        target = folder_for(name)
        for entry in entries:
            path = norm(entry["path"])
            base = path.rsplit("/", 1)[-1]
            if not path.lower().startswith(INSIDE):
                target.report(path, "outside bios/, where RomMBat never writes", {RETROBAT})
                continue
            if entry["md5"] is None and base.lower().endswith(".xml"):
                target.report(path, "a MAME software list RetroBat ships, not firmware", {RETROBAT})
                continue
            slot = target.slot(path)
            slot.retrobat_named = True
            slot.readers.add(RETROBAT)
            slot.required = True
            if entry.get("supplement"):
                slot.supplement = entry["supplement"]
            if entry["md5"]:
                slot.accept(entry["md5"], RETROBAT, region=catalog_region(evidence, entry["md5"]))
            elif base.lower().endswith(".zip"):
                for files in [contents_of({"contents": evidence.zip_contents(name, base)})]:
                    if files:
                        slot.add_contents(files, {RETROBAT})
            else:
                md5s, origin = single_file_identity(evidence, name, base)
                for md5, facts in md5s:
                    slot.accept(md5, origin, region=catalog_region(evidence, md5), **facts)

    # 2. What each row reads by name, in the directory its emulator reads.
    by_hash: list[tuple[str, str, dict, set[str]]] = []
    for name, rows in sorted(evidence.folder_rows.items()):
        target = folder_for(name)
        for row, sources in rows:
            reader = row.key if row.core else row.emulator
            libretro = row.emulator == "libretro"
            directory = directories.get(row.emulator)

            if sources.get("bizhawk"):
                for entry in evidence.bizhawk_files(name):
                    by_hash.append((name, reader, entry, {reader}))
                continue

            for profile_name in sources.get("profiles", []):
                profile = evidence.profiles[profile_name]
                files = [e for e in evidence.profile_files(profile_name, name) if firmware_sources.is_firmware(e) and firmware_sources.applies_to(e, profile, libretro)]

                for archive, members in archives_of(files).items():
                    if directory is None:
                        target.report(archive, f"{row.emulator} has no cited firmware directory", {reader})
                        continue
                    path = join(directory["path"], archive)
                    if not path.lower().startswith(INSIDE):
                        target.report(path, "outside bios/, where RomMBat never writes", {reader})
                        continue
                    slot = target.slot(path)
                    slot.readers.add(reader)
                    slot.required = slot.required or any(m.get("required") for m in files if m.get("archive") == archive)
                    slot.add_contents(members, {reader})

                for entry in files:
                    if entry.get("archive"):
                        continue
                    base = str(entry["name"])
                    relative = (entry.get("standalone_path") if not libretro else None) or entry.get("path") or base
                    if any(base in item["files"] or relative in item["files"] for item in installed.values()):
                        target.report(relative, f"installed by the emulator: {next(i['why'] for i in installed.values() if base in i['files'] or relative in i['files'])}", {reader})
                        continue
                    if directory is None:
                        target.report(relative, f"{row.emulator} has no cited firmware directory", {reader})
                        continue
                    path = join(directory["path"], relative)
                    if not path.lower().startswith(INSIDE):
                        target.report(path, "outside bios/, where RomMBat never writes", {reader})
                        continue

                    md5s = stated(evidence, entry)
                    origin = f"profile:{profile_name}"
                    zipped = contents_of(entry) if base.lower().endswith(".zip") else []
                    existing = target.slots.get(path.lower())
                    # A name leads to a hash only for a file nothing else identifies, so a slot
                    # RetroBat or a stated hash already identifies borrows nothing by name.
                    if not md5s and not zipped and not (existing and existing.identified):
                        borrowed, how = single_file_identity(evidence, name, base)
                        md5s = borrowed
                        origin = how if how != "named" else origin
                    if not md5s and not zipped and not target.slots.get(path.lower()):
                        target.report(path, "no identity: no source states a hash for it", {reader})
                        continue

                    slot = target.slot(path)
                    slot.readers.add(reader)
                    slot.required = slot.required or bool(entry.get("required"))
                    for alias in entry.get("aliases") or []:
                        slot.aliases.add(join(path.rsplit("/", 1)[0], str(alias).rsplit("/", 1)[-1]))
                    if zipped:
                        slot.add_contents(zipped, {reader})
                    priority = entry.get("priority") if isinstance(entry.get("priority"), int) else None
                    for md5, facts in md5s:
                        slot.accept(md5, origin, priority=priority, region=regions(entry) or catalog_region(evidence, md5), **facts)

    # 3. BizHawk's entries, last, so each joins any slot already accepting its hash.
    bizhawk_dir = directories["bizhawk"]["path"]
    for name, reader, entry, readers in by_hash:
        target = folders[name]
        md5s = stated(evidence, entry)
        destination = str(entry.get("destination") or entry["name"])
        if not md5s:
            target.report(join(bizhawk_dir, destination), "no identity: no source states a hash for it", readers)
            continue
        # One slot is enough for an emulator that finds a file by its hash: RetroBat's, where
        # it names one, since emulatorlauncher points BizHawk at RetroBat's names (RB-322).
        matching = sorted(
            (
                slot
                for slot in target.slots.values()
                if slot.path.lower().startswith(bizhawk_dir.lower() + "/") and any(md5 in slot.accepted for md5, _ in md5s)
            ),
            key=lambda slot: (not slot.retrobat_named, slot.path.lower()),
        )
        if matching:
            slot = matching[0]
            slot.readers |= readers
            slot.required = slot.required or bool(entry.get("required"))
            for md5, facts in md5s:
                if md5 in slot.accepted:
                    slot.accept(md5, "bizhawk", **facts)
            continue
        slot = target.slot(join(bizhawk_dir, destination))
        slot.readers |= readers
        slot.required = slot.required or bool(entry.get("required"))
        for md5, facts in md5s:
            slot.accept(md5, "bizhawk", region=catalog_region(evidence, md5), **facts)

    for key, item in installed.items():
        target = folder_for(key)
        for path in item["files"]:
            if not any(entry["path"].lower() == path.lower() for entry in target.reported.values()):
                target.report(path, f"installed by the emulator: {item['why']}", {"*"})

    systems = {}
    for name in sorted(folders):
        target = folders[name]
        slots = [slot.render() for slot in sorted(target.slots.values(), key=lambda s: s.path.lower())]
        reported = [
            {"path": entry["path"], "why": entry["why"], "readers": sorted(entry["readers"])}
            for entry in sorted(target.reported.values(), key=lambda e: (e["path"].lower(), e["why"]))
        ]
        if not slots and not reported:
            continue
        systems[name] = {"name": names.get(name, name), "slots": slots, "reported": reported}

    all_slots = [slot for body in systems.values() for slot in body["slots"]]
    return {
        "_comment": (
            "Every firmware file a RetroBat system's emulators read, as slots: a destination path "
            "relative to the RetroBat root, the files accepted there by md5 or by the crc32 and size "
            "of a zip's members, and the rows that read it. RetroBat's batocera-systems.json decides "
            "where a file goes and ranks first; retrobios's emulator profiles and BizHawk's firmware "
            "list decide what each emulator accepts. A slot with nothing accepted and no contents is "
            "one RetroBat names without a hash and no source identifies. 'reported' lists what is "
            "read but never fetched, and why. Generated by tools/build-bios-manifest.py from "
            "reference/batocera-systems.json and reference/retrobios/; do not hand-edit."
        ),
        "_schema": SCHEMA,
        "_source": [
            "RetroBat-Official/emulatorlauncher batocera-systems/Resources/batocera-systems.json",
            f"Abdess/retrobios {evidence.ref}",
        ],
        "_systems": len(systems),
        "_slots": len(all_slots),
        "_retrobat_slots": sum(1 for slot in all_slots if RETROBAT in slot["readers"]),
        "_slots_without_identity": sum(1 for slot in all_slots if not slot["accepted"] and not slot.get("contents")),
        "_zip_slots_with_contents": sum(1 for slot in all_slots if slot.get("contents")),
        "_distinct_md5s": len({entry["md5"] for slot in all_slots for entry in slot["accepted"]}),
        "_reported": sum(len(body["reported"]) for body in systems.values()),
        "_aliases": ALIASES,
        "systems": systems,
    }


def main() -> int:
    document = build()
    rendered = json.dumps(document, indent=2, ensure_ascii=False) + "\n"

    if "--check" in sys.argv:
        # Compared as parsed JSON, not as bytes: Trunk's prettier owns the formatting of
        # everything under data/, and re-indenting the file is not drift. Content is.
        current = json.loads(OUTPUT.read_text(encoding="utf-8")) if OUTPUT.exists() else None
        if current != document:
            print(f"{OUTPUT.relative_to(ROOT)} is stale. Run tools/build-bios-manifest.py.")
            return 1
        print(f"{OUTPUT.relative_to(ROOT)} is up to date.")
        return 0

    if os.environ.get("ROMMBAT_REFERENCE_DIR"):
        sys.exit("ROMMBAT_REFERENCE_DIR is set: only --check runs against a scratch refresh.")
    OUTPUT.write_text(rendered, encoding="utf-8")
    print(
        f"{OUTPUT.relative_to(ROOT)}: {document['_systems']} systems, {document['_slots']} slots "
        f"({document['_retrobat_slots']} RetroBat's), {document['_distinct_md5s']} accepted md5s, "
        f"{document['_zip_slots_with_contents']} zips by contents, {document['_slots_without_identity']} "
        f"with no identity, {document['_reported']} reported."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
