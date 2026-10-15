"""Shared reading of the two firmware authorities, for the vendoring step and the manifest.

RetroBat says where a file goes and which files its own BIOS check wants, for the default
emulator only. retrobios's emulator profiles say which files each emulator's code reads, cited
file:line. This module joins the two through RetroBat's rows: every (folder, emulator, core)
that es_systems.cfg offers is resolved to the retrobios sources that describe it, by the core
name each profile declares, then by data/retrobat/firmware-rows.json where that is wrong or
silent. A row that resolves to nothing and is not explicitly marked as having no source fails
the caller, so coverage never shrinks without a diff saying so.

Imported, not run: tools/vendor-retrobios.py and reference/verify.py are the callers.
"""

from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path
from xml.etree import ElementTree

ROOT = Path(__file__).resolve().parent.parent
ROWS = ROOT / "data" / "retrobat" / "firmware-rows.json"

# BizHawk's firmware list is not an emulator profile in retrobios but a platform file scraped
# from its FirmwareDatabase.cs, and it keys systems in a vocabulary of its own.
BIZHAWK = "platforms/bizhawk"


@dataclass(frozen=True)
class Row:
    folder: str
    emulator: str
    core: str

    @property
    def key(self) -> str:
        return f"{self.emulator}/{self.core}"


def read_rows(es_systems: Path) -> list[Row]:
    """Every (folder, emulator, core) es_systems.cfg offers. The folder is <path>'s basename,
    which is the vocabulary bios.json and the platform map use; <name> differs for a few. An
    emulator with no <cores>, often written self-closing, is one row with an empty core. Parsed
    as XML, as EsSystemsFile does: a pattern over the text lets a self-closing <emulator/> take
    the next emulator's cores."""
    root = ElementTree.parse(es_systems).getroot()
    rows: list[Row] = []
    for system in root.iter("system"):
        path = (system.findtext("path") or "").strip()
        if not path:
            continue
        folder = re.split(r"[\\/]", path.rstrip("\\/"))[-1]
        for emulator in system.iter("emulator"):
            cores = [(core.text or "").strip() for core in emulator.iter("core")] or [""]
            rows.extend(Row(folder, emulator.get("name", ""), core) for core in cores)
    return rows


def read_row_map(path: Path = ROWS) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def index_profiles(profiles: dict[str, dict]) -> dict[str, list[str]]:
    """Core name, lowercased, to the profiles that declare it. An alias profile stands for the
    profile it names, because retrobios keeps the files on the target and leaves the alias empty."""
    by_core: dict[str, list[str]] = {}
    for name, profile in sorted(profiles.items()):
        target = profile.get("alias_of") or name
        for core in profile.get("cores") or []:
            bucket = by_core.setdefault(str(core).lower(), [])
            if target not in bucket:
                bucket.append(target)
    return by_core


def resolve(row: Row, by_core: dict[str, list[str]], overrides: dict[str, dict]) -> dict:
    """The sources for one row: {"profiles": [...]}, {"bizhawk": true} or {"none": reason}.

    An override names either the exact emulator/core or emulator/* for every core of one
    emulator; the exact key wins. Without one, a libretro row is looked up by its core and a
    standalone row by its emulator, which is how retrobios names both."""
    override = overrides.get(row.key) or overrides.get(f"{row.emulator}/*")
    if override is not None:
        return override
    name = (row.core if row.emulator == "libretro" else row.emulator).lower()
    found = by_core.get(name)
    return {"profiles": found} if found else {}


def resolve_all(rows: list[Row], profiles: dict[str, dict], row_map: dict) -> tuple[dict[Row, dict], list[Row]]:
    by_core = index_profiles(profiles)
    overrides = row_map.get("emulators", {})
    resolved: dict[Row, dict] = {}
    unresolved: list[Row] = []
    for row in rows:
        sources = resolve(row, by_core, overrides)
        if not sources:
            unresolved.append(row)
            continue
        missing = [name for name in sources.get("profiles", []) if name not in profiles]
        if missing:
            raise SystemExit(f"{row.folder} {row.key} names retrobios profiles that do not exist: {missing}")
        resolved[row] = sources
    return resolved, unresolved


def profiles_named(resolved: dict[Row, dict]) -> set[str]:
    return {name for sources in resolved.values() for name in sources.get("profiles", [])}


def _hashes(value) -> list[str]:
    if isinstance(value, str):
        return [value.lower()]
    return [str(item).lower() for item in value or []]


def _basename(name) -> str:
    return str(name).replace("\\", "/").rsplit("/", 1)[-1].lower()


class Evidence:
    """What the vendored sources say identifies a file a RetroBat folder's rows read.

    A file's identity comes from a hash a source states, never from a name alone: retrobios's
    own by-name index files a Store Display Unit BIOS under bios_U.sms. The one place a name
    leads is a dump catalog, where the name is the catalog's own and stands for one dump."""

    def __init__(self, reference: Path):
        import yaml  # Only the generator and verify.py need it; mkdocs already installs it.

        self.profiles = {
            path.stem: yaml.safe_load(path.read_text(encoding="utf-8"))
            for path in sorted((reference / "retrobios" / "emulators").glob("*.yml"))
        }
        self.bizhawk = yaml.safe_load((reference / "retrobios" / "platforms" / "bizhawk.yml").read_text(encoding="utf-8"))
        subset = json.loads((reference / "retrobios" / "database-subset.json").read_text(encoding="utf-8"))
        self.ref = subset["_ref"]
        self.database = subset["files"]
        self.by_sha1 = {sha1.lower(): row for sha1, row in self.database.items()}
        self.catalog: dict[str, set[str]] = {}
        for row in self.database.values():
            for name, entry in (row.get("provenance") or {}).items():
                if isinstance(entry, dict) and entry.get("name"):
                    self.catalog.setdefault(entry["name"].lower(), set()).add(row["md5"].lower())
        known = json.loads((reference / "romm-known_bios_files.json").read_text(encoding="utf-8"))
        self.romm: dict[str, set[str]] = {}
        for key, entry in known.items():
            if isinstance(entry, dict) and entry.get("md5"):
                self.romm.setdefault(_basename(key.split(":", 1)[-1]), set()).add(entry["md5"].lower())

        rows = read_rows(reference / "es_systems.cfg")
        row_map = read_row_map()
        resolved, self.unresolved = resolve_all(rows, self.profiles, row_map)
        keys = {row.key for row in rows}
        emulators = {row.emulator for row in rows}
        self.unused = sorted(
            key
            for key in row_map.get("emulators", {})
            if (key[:-2] not in emulators if key.endswith("/*") else key not in keys)
        )
        self.rows = resolved
        self.folder_profiles: dict[str, set[str]] = {}
        self.folder_bizhawk: set[str] = set()
        for row, sources in resolved.items():
            self.folder_profiles.setdefault(row.folder, set()).update(sources.get("profiles", []))
            if sources.get("bizhawk"):
                self.folder_bizhawk.add(row.folder)

    def entries(self, folder: str) -> list[dict]:
        """Every file entry a source for one of this folder's rows lists. Not yet narrowed by
        system: a multi-system profile contributes its other systems' files too, which can only
        widen a name match, never invent a hash."""
        found = [
            entry
            for name in sorted(self.folder_profiles.get(folder, ()))
            for entry in self.profiles[name].get("files") or []
            if isinstance(entry, dict)
        ]
        if folder in self.folder_bizhawk:
            found += [entry for system in self.bizhawk["systems"].values() for entry in system.get("files") or []]
        return found

    def named(self, folder: str, name: str) -> list[dict]:
        wanted = _basename(name)
        return [
            entry
            for entry in self.entries(folder)
            if wanted == _basename(entry.get("name", "")) or wanted in {_basename(a) for a in entry.get("aliases") or []}
        ]

    def stated_md5s(self, folder: str, name: str) -> set[str]:
        """md5s a source states for this name, a sha1 converted through database.json. A sha1
        the database has no row for cannot be joined on RomM's md5, so it is dropped."""
        found: set[str] = set()
        for entry in self.named(folder, name):
            found.update(_hashes(entry.get("md5")))
            found.update(self.by_sha1[sha1]["md5"].lower() for sha1 in _hashes(entry.get("sha1")) if sha1 in self.by_sha1)
        return found

    def zip_contents(self, folder: str, name: str) -> list[dict]:
        for entry in self.named(folder, name):
            if entry.get("contents"):
                return entry["contents"]
        return []

    def catalog_md5(self, name: str) -> str | None:
        found = self.catalog.get(_basename(name), set())
        return next(iter(found)) if len(found) == 1 else None

    def romm_md5(self, name: str) -> str | None:
        found = self.romm.get(_basename(name), set())
        return next(iter(found)) if len(found) == 1 else None
