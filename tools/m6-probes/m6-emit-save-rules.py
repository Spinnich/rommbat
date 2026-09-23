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
        "systems": ["nes", "megadrive", "gba", "gb", "gbc"],
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
            "; gba under bizhawk, mGBA, on 8.2.1: Pokemon - Emerald Version (USA, Europe).zip wrote "
            "bizhawk/Pokemon - Emerald Version (USA, Europe).SaveRAM, 131,088 B, named after the "
            "rom file as the sidecar Pokemon - Emerald Version (USA, Europe).mGBA says"
            "; gb under bizhawk, Gambatte, GBHawk and SameBoy alike, on 8.2.1: Pokemon - Yellow "
            "Version - Special Pikachu Edition (USA, Europe) (CGB+SGB Enhanced).zip wrote "
            "bizhawk/Pokemon - Yellow Version (USA, Europe).SaveRAM, 32,768 B, one file for all "
            "three cores, named after BizHawk's own title as the sidecar Pokemon - Yellow Version "
            "(USA, Europe).Gambatte says"
            "; gbc under bizhawk, Gambatte, GBHawk and SameBoy alike, on 8.2.1: Pokemon - Crystal "
            "Version (USA, Europe) (Rev 1).zip wrote bizhawk/Pokemon - Crystal Version (USA, "
            "Europe) (Rev A).SaveRAM, named after BizHawk's own title as the sidecar Pokemon - "
            "Crystal Version (USA, Europe) (Rev A).Gambatte says. One file for all three cores, "
            "each with its own clock: 32,790 B under Gambatte, 32,816 B under SameBoy, and 32,768 B "
            "with no clock under GBHawk"
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
        "emulator": "jgenesis",
        "systems": ["gb"],
        "directory": "jgenesis/gb",
        "extensions": [".sav"],
        "named_after": "rom file",
        "class": "A",
        "evidence": (
            "gb under jgenesis on 8.2.1: Pokemon - Yellow Version - Special Pikachu Edition (USA, "
            "Europe) (CGB+SGB Enhanced).zip wrote jgenesis/gb/<rom>.sav, 32,768 B, and no clock "
            "file, and its states sit apart in jgenesis/states"
        ),
    },
    {
        "emulator": "jgenesis",
        "systems": ["gbc"],
        "directory": "jgenesis/gbc",
        "extensions": [".sav", ".rtc"],
        "named_after": "rom file",
        "class": "B",
        "evidence": (
            "gbc under jgenesis on 8.2.1: Pokemon - Crystal Version (USA, Europe) (Rev 1).zip, MBC3 "
            "with a clock, wrote jgenesis/gbc/<rom>.sav, 32,768 B, and <rom>.rtc, 38 B, the "
            "cartridge clock, which changes on every launch, and its states sit apart in "
            "jgenesis/states"
        ),
    },
    {
        "emulator": "jgenesis",
        "systems": ["gba"],
        "directory": "jgenesis/gba",
        "extensions": [".sav", ".rtc"],
        "named_after": "rom file",
        "class": "B",
        "evidence": (
            "gba under jgenesis on 8.2.1: Pokemon - Emerald Version (USA, Europe).zip wrote "
            "jgenesis/gba/<rom>.sav, 131,072 B, and <rom>.rtc, 59 B, the cartridge clock, which "
            "changes on every launch"
        ),
    },
    {
        "emulator": "mgba",
        "systems": ["gba", "gb", "gbc"],
        "directory": "",
        "extensions": [".sav"],
        "named_after": "rom file",
        "class": "A",
        "evidence": (
            "gba under mgba standalone on 8.2.1: Pokemon - Emerald Version (USA, Europe).zip wrote "
            "a loose <rom>.sav, 131,088 B, the flash and a 16-byte clock footer. mesen writes the "
            "same name, 131,072 B, keeping its clock in <rom>.rtc, and mednafen opens it when "
            "present but refuses mgba's size, so the file is shared and uploads as mgba's"
            "; gb under mgba standalone on 8.2.1: Pokemon - Yellow Version - Special Pikachu "
            "Edition (USA, Europe) (CGB+SGB Enhanced).zip wrote a loose <rom>.sav, 32,768 B with "
            "no footer, and mednafen read and saved back into that file rather than its hashed name"
            "; gbc under mgba standalone on 8.2.1: Pokemon - Crystal Version (USA, Europe) (Rev "
            "1).zip, MBC3 with a clock, wrote a loose <rom>.sav, 32,816 B, the RAM and a 48-byte "
            "clock footer, which moves on every launch, and mednafen read and saved back into it"
        ),
    },
    {
        "emulator": "mesen",
        "systems": ["gba"],
        "directory": "",
        "extensions": [".rtc"],
        "named_after": "rom file",
        "class": "B",
        "evidence": (
            "gba under mesen standalone on 8.2.1: Pokemon - Emerald Version (USA, Europe).zip wrote "
            "a loose <rom>.rtc, 19 B, beside the <rom>.sav mgba's rule carries, and rewrote it on "
            "a launch with no save made"
        ),
    },
    {
        "emulator": "libretro",
        "systems": ["gb", "gbc"],
        "directory": "",
        "extensions": [".rtc"],
        "named_after": "rom file",
        "class": "B",
        "evidence": (
            "gb under libretro on 8.2.1: Pokemon - Silver Version (USA, Europe) (SGB Enhanced) (GB "
            "Compatible).zip, an MBC3 cartridge with a clock, wrote a loose <rom>.rtc beside the "
            ".srm under gambatte (8 B), sameboy (32 B), tgbdual and DoubleCherryGB (4 B), and mesen "
            "standalone wrote the same name (13 B). gambatte and mesen write none for Pokemon Yellow, "
            "which has no clock; the other three write one for every game. Shared as the .srm is, "
            "so class B gives it libretro:battery:rtc; gbc under libretro on 8.2.1: Pokemon - "
            "Crystal Version (USA, Europe) (Rev 1).zip, MBC3 with a clock, wrote the same loose "
            "<rom>.rtc at the same four sizes under the four gbc cores, and mesen standalone 13 B"
        ),
    },
    {
        "emulator": "libretro",
        "systems": ["gba"],
        "directory": "",
        "extensions": [".sav"],
        "named_after": "archive member and content md5",
        "class": "B",
        "evidence": (
            "gba under libretro, core mednafen_gba, on 8.2.1: Pokemon - Emerald Version (USA, "
            "Europe).zip wrote a loose <rom>.zip#<rom>.605b89b67018abcea91e693a4dd25be3.sav, "
            "131,072 B, the md5 being of the whole .gba inside, while RetroArch logged Skipping "
            "SRAM load for the .srm the other cores share; class B gives it libretro:battery:sav. "
            "A .7z of the same ROM wrote <rom>.7z#<rom>.<md5>.sav, and a bare .gba wrote "
            "<rom>.<md5>.sav, mednafen standalone's name"
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
        "systems": ["nes", "megadrive", "gba", "gb", "gbc"],
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
            "; gba under mednafen, core gba, on 8.2.1: Pokemon - Emerald Version (USA, Europe).zip "
            "wrote a loose <rom>.605b89b67018abcea91e693a4dd25be3.sav, 131,072 B, the md5 being of "
            "the whole .gba inside, once no plain <rom>.sav was present"
            "; gb under mednafen, core gb, on 8.2.1: Pokemon - Yellow Version - Special Pikachu "
            "Edition (USA, Europe) (CGB+SGB Enhanced).zip wrote a loose "
            "<rom>.d9290db87b1f0a23b89f99ee4469e34b.sav, 32,768 B, the md5 being of the whole .gb "
            "inside, when no plain <rom>.sav was present"
            "; gbc under mednafen, core gbc, on 8.2.1: Pokemon - Crystal Version (USA, Europe) (Rev "
            "1).zip wrote a loose <rom>.301899b8087289a6436b0a241fbbb474.sav, 32,816 B with the "
            "clock inside, the md5 being of the whole .gbc inside, when no plain <rom>.sav was "
            "present"
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
    {
        "emulator": "ares",
        "systems": ["gb"],
        "directory": "ares/Game Boy",
        "extensions": [".ram"],
        "named_after": "rom file",
        "class": "A",
        "evidence": (
            "gb under ares, core GameBoy, on 8.2.1: Pokemon - Yellow Version - Special Pikachu "
            "Edition (USA, Europe) (CGB+SGB Enhanced).zip read and saved ares/Game Boy/<rom>.ram, "
            "32,768 B, beside its states .bs1 and .bs2"
        ),
    },
    {
        "emulator": "ares",
        "systems": ["gb"],
        "directory": "ares/Game Boy",
        "extensions": [".rtc"],
        "named_after": "rom file",
        "class": "B",
        "evidence": (
            "gb under ares, core GameBoy, on 8.2.1: Pokemon - Silver Version (USA, Europe) (SGB "
            "Enhanced) (GB Compatible).zip, MBC3 with a clock, wrote ares/Game Boy/<rom>.rtc, 13 B, "
            "beside its .ram, 32,768 B, on exit. Its own rule so the .ram keeps ares:battery, as "
            "libretro's .rtc sits beside its .srm on gb"
        ),
    },
    {
        "emulator": "ares",
        "systems": ["gbc"],
        "directory": "ares/Game Boy",
        "extensions": [".ram", ".rtc"],
        "named_after": "rom file",
        "class": "B",
        "evidence": (
            "gbc under ares, core GameBoyColor, on 8.2.1: Pokemon - Crystal Version (USA, Europe) "
            "(Rev 1).zip, MBC3 with a clock, read and saved ares/Game Boy/<rom>.ram, 32,768 B, and "
            "<rom>.rtc, 13 B, which changes on every launch. The directory is gb's name, not "
            "Game Boy Color, while its states .bs1 and .bs2 go to ares/Game Boy Color"
        ),
    },
    {
        "emulator": "ares",
        "systems": ["gba"],
        "directory": "ares/Game Boy Advance",
        "extensions": [".flash", ".rtc"],
        "named_after": "rom file",
        "class": "B",
        "evidence": (
            "gba under ares, core GameBoyAdvance, on 8.2.1: Pokemon - Emerald Version (USA, "
            "Europe).zip wrote ares/Game Boy Advance/<rom>.flash, 131,072 B, and <rom>.rtc, 18 B, "
            "beside its states .bs1 and .bs2"
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
        "file has two owners, unless their names differ in how narrow they are, the narrower "
        "asked first: an archive member and a hash, then a hash, then anything. One emulator may "
        "hold two rules on a system where class B gives each extension its own slot. The loose level is libretro's across saturn, megacd, psx, gb and 12 "
        "more, but not exclusively: on nes, mesen standalone and mednafen write a loose .sav, "
        "which is why this is not one extension list and one loose emulator (#152). named_after "
        "is what the stem joins on: the rom file, the emulator's own title for the game, which "
        "has to be learned (#151), the rom file and a content md5, which mednafen appends, or "
        "the zip, the file inside it and a content md5, which mednafen_gba writes."
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
