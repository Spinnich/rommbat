#!/usr/bin/env python3
"""M0 probe 2: emit data/retrobat/save_directories.json and save_shapes.json.

Shape follows grout's `cfw/*/data/save_directories.json`: an object keyed by identifier
whose values are lists of save directories relative to a known root.

Two deliberate departures from grout, both documented in the emitted files:

  * keyed by **RetroBat system folder**, not by RomM slug. This file describes RetroBat's
    disk layout; the slug join is a separate mapping table and belongs with the platform
    map, not here.
  * paths are relative to `saves/`, and RetroBat nests them `<system>/<emulator>`, so the
    entries are two segments deep where grout's are one.

Every entry carries provenance. `observed` means it was seen on a real install; `declared`
means it comes from es_savestates.cfg or es_features.cfg but has not been seen written.
Nothing here is guessed, and entries that need a live launch to confirm say so.

Usage: python tools/m0-probes/probe2-emit-data.py <retrobat-root>
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from probe2_static import inventory_saves, parse_savestates  # noqa: E402

# Directories under saves/<system>/ that hold emulator state or config rather than battery
# saves, so they must not be advertised as save locations.
NON_SAVE_DIRS = {"SYSTEM", "Cheats", "PPSSPP_STATE", "artwork", "ctrlr", "cfg", "ini", "hash", "samples"}

# Shape classifications that were confirmed by direct observation of a real install.
# Anything not listed stays "unclassified" rather than being guessed at.
CONFIRMED_SHAPES = {
    "nes": ("A", "loose .srm per rom, libretro"),
    "snes": ("A", "loose .srm per rom, libretro"),
    "gb": ("A", "loose .srm per rom, libretro"),
    "gbc": ("A", "loose .srm per rom, libretro"),
    "gba": ("A", "loose .srm per rom, libretro"),
    "megadrive": ("A", "loose .srm per rom, libretro"),
    # Both driven under libretro genesis_plus_gx with no save key ever pressed, so the file
    # that appeared was written by the emulator alone. RetroArch names the destination in
    # its own log ("Redirecting save file to saves/<system>/<rom>.srm") even on a run that
    # writes nothing, which is what classifies gamegear despite its cart staying untouched.
    "mastersystem": ("A", "loose .srm per rom, libretro genesis_plus_gx; written at boot before any player save"),
    "gamegear": ("A", "loose .srm per rom, libretro genesis_plus_gx; path declared by the emulator, cart untouched in the run"),
    "n64": ("A", "loose .srm per rom, libretro"),
    "pcengine": ("A", "loose .srm per rom, libretro"),
    "pcenginecd": ("A", "loose .srm per rom, libretro"),
    "sega32x": ("A", "loose .srm per rom, libretro"),
    # Driven under both emulators with the same two-disc set. libretro names the .srm from the
    # .m3u stem; DuckStation names the card from its gamedb saveName with the disc marker
    # removed, and writes one per console slot rather than one per disc.
    "psx": ("A", "loose .srm when libretro is selected; DuckStation instead writes memcards/<saveName minus disc>_<slot>.mcd, see shape_depends_on_emulator"),
    "saturn": ("B", ".bcr and .bkr both written per rom"),
    "megacd": ("BD", "per-rom .brm and .srm, plus a shared 4Mbit_cart.brm RAM cart"),
    "mame": ("C", "nvram/<shortname>/ where shortname is the rom basename, so attribution is by filename"),
    "psp": ("C", "SAVEDATA/<GAMEID>/ containing PARAM.SFO, keyed by game id not filename"),
    "ps3": ("C", "rpcs3/dev_hdd0/**/savedata keyed by TITLEID; 32k+ files, hashing is expensive"),
    "gamecube": ("C", "GCI folder under dolphin-emu/User/GC/<REGION>/, several .gci per game, .gci.deleted must be excluded"),
    "wii": ("C", "NAND tree under dolphin-emu/User/Wii/title/, mixed with non-per-game system state"),
    "dreamcast": ("D", "flycast/vmu/vmu_save_<PORT>1.bin shared by all games; convertible via flycast_vmupergame, port 1 only"),
    "xbox": ("D", "eeprom.bin and xbox_hdd.qcow2, a whole disk image shared by every game"),
    "ps2": ("D", "shared Mcd001.ps2 by default; convertible via pcsx2_slot1_memory=game"),
}

# Per-game conversion levers, read out of es_features.cfg on a real install.
CONVERSIONS = {
    # The one lever here that should not be pulled. Measured on a driven two-disc set: stock
    # PerGameTitle produced a single card for the set, so converting to a filename-keyed mode
    # splits it and the save disappears at the disc change.
    "psx": {"option": "duckstation_memcardtype", "set_to": None, "apply": False,
            "keys_by": "gamedb saveName with the disc marker removed, not the rom filename",
            "note": "Stock PerGameTitle binds a multi-disc set through DuckStation's own database, "
                    "with or without a .m3u: a foldered two-disc set and a loose three-disc set each "
                    "produced one card, named '<saveName minus disc>_<slot>.mcd'. The slot suffix is not "
                    "a disc number and how many slots appear depends on the game. "
                    "Regions stay separate because saveName carries them; revisions share a serial and a card. "
                    "Save states are keyed on the rom file instead, so they are per disc while the card is "
                    "per set. Not measured: the 130 stems keeping a subtitle behind the disc marker"},
    "ps2": {"option": "pcsx2_slot1_memory", "set_to": "game", "keys_by": "rom basename"},
    "gamecube": {"option": "dolphin_slotA", "set_to": "8", "keys_by": "game code",
                 "note": "already the stock default; still needs attribution because .gci names carry the game code, not the filename"},
    "dreamcast": {"option": "flycast_vmupergame", "set_to": "on", "keys_by": "disc serial",
                  "note": "driven: produces vmu/<SERIAL>_vmu_save_A1.bin (T40217N for Bangai-O), not a rom-named file, "
                          "so it needs game-id attribution like class C. Port 1 only; ports B, C and D stay shared"},
}

# es_savestates.cfg's <directory> is not authoritative. These are the emulators whose
# declared directory was checked by driving a real save state, and openmsx is the one it
# still gets wrong.
STATE_DIRECTORY_VERIFIED = [
    "libretro", "ppsspp", "duckstation", "pcsx2", "dolphin", "gopher64",
    "desmume", "mupen64", "jgenesis", "bizhawk", "flycast",
]
# flycast is verified on 8.2.1 and everything else on 8.2.0. Worth carrying in the file
# rather than only here: a reader checking one entry against an install has to know which
# build the entry describes.
STATE_DIRECTORY_VERIFIED_NOTE = (
    'flycast was a correction until RetroBat 8.2.1 fixed emulatorlauncher#1336. It writes saves/<system>/reicast/states natively and RetroBat now mirrors into the declared flycast/sstates in the same millisecond, confirmed by hand on 8.2.1 over three runs (tools/m0-probes/probe2-flycast-mirror.ps1). Read the declared path. Everything else in this file is measured on 8.2.0.'
)
# Emulators that write somewhere else entirely and rely on RetroBat mirroring into the
# declared path. Read and write the declared path; the native one is not addressable.
NATIVE_STATE_LOCATIONS = {
    "ppsspp": "saves/psp/PPSSPP_STATE/<GAMEID>_<version>_<slot>.ppst",
    "flycast": "saves/dreamcast/reicast/states/<rom filename>_<slot>.state",
    "bizhawk": "emulators/bizhawk/sstates/<system>/<internal title>.<core>.QuickSave<slot>.State",
    "openmsx": "bios/openmsx/savestates/<state name>.oms (plus a real .png screenshot)",
}
STATE_DIRECTORY_CORRECTIONS = {
    "openmsx": {
        "declared": "{{system}}/openmsx",
        "actual": "../bios/openmsx/savestates",
        "why": "RetroBat puts openMSX's whole user-data directory under bios/openmsx/, so states land outside "
               "the saves tree entirely. The declared directory stayed empty across two real saves. Note the "
               "path is relative to saves/, hence the leading ../; whether RetroBat mirrors into the declared "
               "path under its own state naming is unverified.",
    },
}
# Slot placeholders must expand to a single digit, never a wildcard: DeSmuME's declared
# {{romfilename}}.ds{{slot0}} otherwise also matches its own .dsv battery save.
STATE_FILE_TEMPLATE_NOTE = (
    "expand {{slot...}} as a single digit, not a wildcard; desmume's .ds{{slot0}} collides with its .dsv battery save"
)


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__)
        return 2

    root = Path(sys.argv[1])
    observed = inventory_saves(root / "saves")
    savestates = parse_savestates(root / "emulationstation" / ".emulationstation" / "es_savestates.cfg")

    header = {
        "_comment": (
            "Generated by tools/m0-probes/probe2-emit-data.py from a real RetroBat install. "
            "Shape follows grout cfw/*/data/save_directories.json. Keyed by RetroBat system "
            "folder, not RomM slug; the slug join lives in the platform map. Paths are "
            "relative to <retrobat root>/saves/ and contain no absolute component."
        ),
        "_retrobat_version": (root / "system" / "version.info").read_text(encoding="utf-8").strip(),
        "_provenance": "observed = seen on disk; declared = from es_savestates.cfg or es_features.cfg, not yet seen written",
    }

    # save_directories.json
    directories: dict[str, list[str]] = {}
    for system, info in sorted(observed.items()):
        entries = []
        if info["loose_files"]:
            entries.append(system)
        for sub in info["subdirectories"]:
            name = Path(sub["path"]).name
            if name in NON_SAVE_DIRS or not sub["file_count"]:
                continue
            entries.append(sub["path"])
        if entries:
            directories[system] = sorted(set(entries))

    state_dirs = {e["name"]: e["directory"] for e in savestates["emulators"]}

    save_directories = {
        **header,
        "_state_directory_templates": state_dirs,
        "_state_directory_verified": STATE_DIRECTORY_VERIFIED,
        "_state_directory_verified_note": STATE_DIRECTORY_VERIFIED_NOTE,
        "_state_directory_corrections": STATE_DIRECTORY_CORRECTIONS,
        "_native_state_locations": NATIVE_STATE_LOCATIONS,
        "_state_file_template_note": STATE_FILE_TEMPLATE_NOTE,
        "directories": directories,
    }

    # save_shapes.json
    shapes = {}
    for system, (cls, why) in sorted(CONFIRMED_SHAPES.items()):
        entry = {
            "class": cls,
            "evidence": why,
            "provenance": "observed" if system in observed else "declared",
        }
        if system in CONVERSIONS:
            entry["per_game_conversion"] = CONVERSIONS[system]
        if system == "psx":
            entry["shape_depends_on_emulator"] = True
        shapes[system] = entry

    unclassified = sorted(set(observed) - set(CONFIRMED_SHAPES))

    save_shapes = {
        **header,
        "_classes": {
            "A": "one file per game",
            "B": "several files per game",
            "C": "directory per game",
            "D": "one container shared by many games",
        },
        "_unclassified": unclassified,
        "_note": (
            "Shape is a property of (system, emulator), not of system alone. psx is the "
            "worked example: libretro writes class A .srm, DuckStation writes memcards."
        ),
        "shapes": shapes,
    }

    out = Path("data/retrobat")
    out.mkdir(parents=True, exist_ok=True)
    (out / "save_directories.json").write_text(json.dumps(save_directories, indent=2) + "\n", encoding="utf-8")
    (out / "save_shapes.json").write_text(json.dumps(save_shapes, indent=2) + "\n", encoding="utf-8")

    print(f"save_directories.json : {len(directories)} systems, {sum(len(v) for v in directories.values())} directories")
    print(f"save_shapes.json      : {len(shapes)} classified, {len(unclassified)} unclassified")
    print(f"unclassified          : {', '.join(unclassified)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
