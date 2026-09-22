"""Derives data/retrobat/save_rules.json from a real install plus the measured findings.

save_shapes.json says what class a system is. It does not say which files under saves/ are
that class, and stage 1 cannot guess: megacd's shared 4Mbit_cart.brm sits beside per-game
.brm files at the same level and only the name separates them, and xbox's two class-D files
are loose under the system folder where class A normally lives.

So this emits the mechanism: which emulator wrote a battery save, from its system, directory
and extension, and which exact filenames are shared containers that must never be attributed to
a rom. libretro's loose extensions come from walking the install; every other battery rule and
the container list come from findings and are declared here rather than inferred, because a
container is recognised by being named in a finding and not by anything about the file, and an
emulator's own subdirectory is measured one (system, emulator) row at a time.

    python m6-emit-save-rules.py <retrobat-root>

Reads the install. Writes data/retrobat/save_rules.json.
"""

from __future__ import annotations

import collections
import json
import pathlib
import sys

from _common import REPO, record_offline

if len(sys.argv) != 2:
    print(__doc__)
    raise SystemExit(2)

root = pathlib.Path(sys.argv[1])
saves = root / "saves"
lines: list[str] = []

shapes = json.loads((REPO / "data" / "retrobat" / "save_shapes.json").read_text(encoding="utf-8"))

# Named in a finding, never inferred. A shared container holds several games' saves, so it has
# no rom_id to carry and must not be attributed to whichever rom its name happens to resemble.
SHARED_CONTAINERS = {
    "megacd": {
        "4Mbit_cart.brm": "the RAM cart, 512 KB, shared by every Mega CD game (probe 2)",
    },
    "xbox": {
        "eeprom.bin": "console EEPROM, not per game (probe 2)",
        "xbox_hdd.qcow2": "a whole disk image shared by every game (probe 2)",
    },
    "saturn": {
        "kronos/bkram.bin": "Kronos backup RAM, one file for every Saturn game (M6 probe 2)",
    },
    "dreamcast": {
        "flycast/vmu/vmu_save_A1.bin": "port-keyed VMU, shared by every game (probe 2)",
        "flycast/vmu/vmu_save_B1.bin": "port-keyed VMU, shared by every game (probe 2)",
        "flycast/vmu/vmu_save_C1.bin": "port-keyed VMU, shared by every game (probe 2)",
        "flycast/vmu/vmu_save_D1.bin": "port-keyed VMU, shared by every game (probe 2)",
    },
    "ps2": {
        "pcsx2/memcards/Mcd001.ps2": "the default shared memory card (probe 2)",
        "pcsx2/memcards/Mcd002.ps2": "the default shared memory card (probe 2)",
    },
}

# Battery saves outside libretro's loose level, one per (system, emulator). Declared, because
# each is a hands-on measurement: the loose level cannot be widened to a second emulator's
# extension without a collision on libretro's slot (#152).
OTHER_BATTERY_RULES = [
    {
        "emulator": "bizhawk",
        "systems": ["nes", "megadrive"],
        "directory": "bizhawk",
        "extensions": [".saveram"],
        "named_after": "display name",
        "class": "A",
        "evidence": (
            "nes under bizhawk, NesHawk and quickerNES both, on 8.2.1: StarTropics (USA).zip wrote "
            "bizhawk/StarTropics.SaveRAM, and the state sidecar beside it reads "
            "StarTropics.NesHawk (#151)"
            "; megadrive under bizhawk, Genplus-gx, on 8.2.1: Sonic & Knuckles + Sonic The Hedgehog 3 "
            "(USA) (Lock-on Combination).zip wrote bizhawk/Sonic and Knuckles & Sonic 3 (W) "
            "[!].SaveRAM, 16,384 B"
        ),
        "not_a_save_extensions": {
            ".bak": (
                "BizHawk's copy of the save a new one replaced, StarTropics.SaveRAM.bak, written on "
                "exit (#151)"
            ),
        },
    },
    {
        "emulator": "jgenesis",
        "systems": ["nes"],
        "directory": "jgenesis/nes",
        "extensions": [".sav"],
        "named_after": "rom file",
        "class": "A",
        "evidence": (
            "nes under jgenesis on 8.2.1: Wizardry - Proving Grounds of the Mad Overlord (USA).zip "
            "wrote jgenesis/nes/Wizardry - Proving Grounds of the Mad Overlord (USA).sav, 8,192 B, "
            "and its states sit apart in jgenesis/states"
        ),
    },
    {
        "emulator": "jgenesis",
        "systems": ["megadrive"],
        "directory": "jgenesis/md",
        "extensions": [".sav"],
        "named_after": "rom file",
        "class": "A",
        "evidence": (
            "megadrive under jgenesis on 8.2.1: Sonic & Knuckles + Sonic The Hedgehog 3 (USA) "
            "(Lock-on Combination).zip wrote jgenesis/md/<rom>.sav, 512 B, and its states sit apart "
            "in jgenesis/states"
        ),
    },
    {
        "emulator": "mesen",
        "systems": ["nes"],
        "directory": "",
        "extensions": [".sav"],
        "named_after": "rom file",
        "class": "A",
        "evidence": (
            "nes under mesen standalone on 8.2.1: Crystalis (USA).zip wrote a loose "
            "Crystalis (USA).sav, 8,192 B, beside libretro's .srm files"
        ),
    },
    {
        "emulator": "mednafen",
        "systems": ["nes", "megadrive"],
        "directory": "",
        "extensions": [".sav"],
        "named_after": "rom file and content md5",
        "class": "A",
        "evidence": (
            "nes under mednafen on 8.2.1: Final Fantasy (USA).zip wrote a loose "
            "Final Fantasy (USA).24ae5edf8375162f91a6846d3202e3d6.sav, 8,192 B, the md5 being of "
            "the .nes inside less its 16-byte iNES header"
            "; megadrive under mednafen, core megadrive, on 8.2.1: Sonic & Knuckles + Sonic The "
            "Hedgehog 3 (USA) (Lock-on Combination).zip wrote a loose "
            "<rom>.c5b1c655c19f462ade0ac4e17a844d10.sav, 1,024 B, the md5 being of the whole .md "
            "inside"
        ),
    },
    {
        "emulator": "ares",
        "systems": ["nes"],
        "directory": "ares/Famicom",
        "extensions": [".ram"],
        "named_after": "rom file",
        "class": "A",
        "evidence": (
            "nes under ares, core Famicom, on 8.2.1: Dragon Warrior IV (USA).zip wrote "
            "ares/Famicom/Dragon Warrior IV (USA).ram, 8,192 B, beside its state .bs1"
        ),
    },
    {
        "emulator": "ares",
        "systems": ["megadrive"],
        "directory": "ares/Mega Drive",
        "extensions": [".ram"],
        "named_after": "rom file",
        "class": "A",
        "evidence": (
            "megadrive under ares, core MegaDrive, on 8.2.1: Sonic & Knuckles + Sonic The Hedgehog 3 "
            "(USA) (Lock-on Combination).zip wrote ares/Mega Drive/<rom>.ram, 512 B, beside its "
            "states .bs1 and .bs2"
        ),
    },
]

# Written into the save tree by RetroArch and by RetroBat, and not a save. The .ldci is the
# hostile one: it is small, it is JSON, and its image_path is an absolute path with a drive
# letter, so relaying it through RomM restores a dangling pointer on any other install.
NOT_A_SAVE = {
    ".ldci": "RetroArch's record of which disc is in the drive; holds an absolute path (F18)",
    ".txt": "RetroBat's name-mapping sidecar; belongs with a state, which is stage 2",
    ".png": "a save-state screenshot, which is stage 2",
    ".jpg": "a save-state screenshot, which is stage 2",
}

lines.append("=== loose files directly under saves/<system>/, which is where class A lives")

by_extension: collections.Counter[str] = collections.Counter()
per_system: dict[str, dict] = {}
containers_seen: list[str] = []

for system_directory in sorted(p for p in saves.iterdir() if p.is_dir()):
    system = system_directory.name
    loose = sorted(p for p in system_directory.iterdir() if p.is_file())
    if not loose:
        continue

    declared = SHARED_CONTAINERS.get(system, {})
    extensions: collections.Counter[str] = collections.Counter()

    for path in loose:
        if path.name in declared:
            containers_seen.append(f"{system}/{path.name}")
            continue
        if path.suffix.lower() in NOT_A_SAVE:
            continue
        extensions[path.suffix.lower()] += 1
        by_extension[path.suffix.lower()] += 1

    shape = shapes["shapes"].get(system)
    per_system[system] = {
        "class": shape["class"] if shape else None,
        "loose_extensions": sorted(extensions),
        "loose_files": sum(extensions.values()),
    }
    lines.append(
        f"  {system:<14} class={per_system[system]['class'] or '?':<3} "
        f"files={per_system[system]['loose_files']:<3} {sorted(extensions)}"
    )

lines.append("")
lines.append(f"=== extensions across every system: {dict(by_extension)}")
lines.append(f"=== declared shared containers found on disk: {containers_seen}")

# Every container declared above, whether or not this install happens to hold it. A rule that
# only covered what one tree contains would let the next tree's container through.
declared_total = sum(len(v) for v in SHARED_CONTAINERS.values())
lines.append(f"=== declared shared containers in total: {declared_total}")

document = {
    "_comment": (
        "Generated by tools/m6-probes/m6-emit-save-rules.py. What save_shapes.json cannot say: "
        "which files under saves/ are the class it names. Extensions are observed on a real "
        "install; shared containers are declared from probe 2, F18 and F19, because a container "
        "is recognised by being named in a finding and by nothing about the file itself."
    ),
    "_retrobat_version": (root / "system" / "version.info").read_text(encoding="utf-8").strip(),
    "_battery_saves_note": (
        "Which emulator wrote a battery save, from the system, the directory it sits in and its "
        "name. directory is relative to saves/<system>/ and empty is the loose level; a rule "
        "with no systems applies to every system. One rule per (system, emulator), because the "
        "emulator is the slot, and no two rules may claim one extension in one directory, or a "
        "file has two owners, unless exactly one of the two names a content hash on the stem, "
        "which then decides. The loose level is libretro's across saturn, megacd, psx, gb and 12 "
        "more, but not exclusively: on nes, mesen standalone and mednafen write a loose .sav, "
        "which is why this is not one extension list and one loose emulator (#152). named_after "
        "is what the stem joins on: the rom file, the emulator's own title for the game, which "
        "has to be learned (#151), or the rom file and a content md5, which mednafen appends."
    ),
    "battery_saves": [
        {
            "emulator": "libretro",
            "directory": "",
            "extensions": sorted(by_extension),
            "named_after": "rom file",
            "evidence": "observed loose under every system in observed below",
        },
        *OTHER_BATTERY_RULES,
    ],
    "not_a_save_extensions": NOT_A_SAVE,
    "shared_containers": SHARED_CONTAINERS,
    "observed": per_system,
}

out = REPO / "data" / "retrobat" / "save_rules.json"
out.write_text(json.dumps(document, indent=2, sort_keys=False) + "\n", encoding="utf-8")
lines.append("")
lines.append(f"=== wrote {out.relative_to(REPO)}, {out.stat().st_size} bytes")

record_offline("m6-emit-save-rules", lines)
