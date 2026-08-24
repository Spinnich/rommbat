"""Does a key written into es_settings.cfg while EmulationStation is running survive?

M0 measured the per-game override thoroughly and every one of its writes happened **before**
ES started. The case the agent is actually in is the other one: ES is up, holding the file in
memory since boot, and RomMBat writes a key underneath it. If ES serialises its boot-time
model on a dirty exit, that write is lost and the whole class D design has to move to "write
while ES is down, or verify after it exits".

Three phases, because the middle one needs a person at the machine.

    python m6-probe9-es-settings-two-writer.py <retrobat-root> backup
    (start EmulationStation)
    python m6-probe9-es-settings-two-writer.py <retrobat-root> write
    (change one setting in the ES UI, so the session is dirty, then quit ES)
    python m6-probe9-es-settings-two-writer.py <retrobat-root> check

**This probe writes into a real RetroBat install's EmulationStation configuration.** `backup`
takes a byte copy first and `restore` puts it back:

    python m6-probe9-es-settings-two-writer.py <retrobat-root> restore

The keys it writes are nonsense ones ES can have no default for, so nothing it does changes
how a game runs. It deliberately does **not** write pcsx2_slot1_memory: durability is the
question here, and a key that changes an emulator's behaviour would answer a different one.

**es_settings.cfg holds plaintext credentials** (ScreenScraperPass,
global.retroachievements.password and .token, IGDBSecret), so this transcript reports key
names and counts and never a value it did not write itself. Do not check the file in as a
fixture; synthesise one with the same shape instead.
"""

from __future__ import annotations

import hashlib
import pathlib
import shutil
import sys
import xml.etree.ElementTree as ElementTree

from _common import record_offline

if len(sys.argv) != 3 or sys.argv[2] not in {"backup", "write", "check", "restore"}:
    print(__doc__)
    raise SystemExit(2)

root = pathlib.Path(sys.argv[1])
phase = sys.argv[2]

settings = root / "emulationstation" / ".emulationstation" / "es_settings.cfg"
backup = settings.with_suffix(".cfg.rommbat-p1-backup")

def a_real_rom(install: pathlib.Path) -> tuple[str, str]:
    """A (system, filename) pair that exists on this install.

    The key under test is a nonsense one nothing reads, so the rom is not load-bearing. It is
    taken off the install anyway, because a per-game key naming a rom that is not there is a
    weaker version of the same measurement and a reviewer would rightly ask.
    """
    roms = install / "roms"
    for system in sorted(p for p in roms.iterdir() if p.is_dir()):
        for entry in sorted(system.iterdir()):
            if entry.is_file() and entry.suffix.lower() not in {".xml", ".txt"}:
                return system.name, entry.name
    raise SystemExit(f"no rom found under {roms}, so there is nothing to scope a per-game key to")


PROBE_SYSTEM, PROBE_ROM = a_real_rom(root)
GLOBAL_KEY = "rommbat_probe_p1_global"
PER_GAME_KEY = f'{PROBE_SYSTEM}["{PROBE_ROM}"].rommbat_probe_p1_pergame'
NONCE = "p1-" + hashlib.md5(PROBE_ROM.encode()).hexdigest()[:8]


def digest(path: pathlib.Path) -> str:
    return hashlib.md5(path.read_bytes()).hexdigest()


def entries(path: pathlib.Path) -> dict[str, tuple[str, str]]:
    """Every setting as name -> (group, value), which is the whole of the file's model."""
    root_element = ElementTree.parse(path).getroot()
    return {
        element.get("name", ""): (element.tag, element.get("value", ""))
        for element in root_element
        if element.get("name")
    }


def merge_and_write(path: pathlib.Path, additions: dict[str, str]) -> None:
    """Adds keys to the file without disturbing anything already in it.

    Parsed and re-rendered rather than string-spliced, because the point is to write the
    file the way the shipped writer will and see what ES does to it. Temp file plus rename,
    which is the gamelist writer's discipline.
    """
    tree = ElementTree.parse(path)
    document = tree.getroot()

    for name, value in additions.items():
        existing = next((e for e in document if e.get("name") == name), None)
        if existing is not None:
            existing.set("value", value)
        else:
            element = ElementTree.SubElement(document, "string")
            element.set("name", name)
            element.set("value", value)

    ElementTree.indent(tree, space="\t")
    temporary = path.with_suffix(".cfg.rommbat-tmp")
    tree.write(temporary, encoding="utf-8", xml_declaration=True)
    temporary.replace(path)


lines: list[str] = [f"=== phase: {phase}", f"  file    {settings}"]

if phase == "backup":
    shutil.copy2(settings, backup)
    lines.append(f"  backup  {backup}")
    lines.append(f"  md5     {digest(settings)}")
    lines.append(f"  entries {len(entries(settings))}")
    lines.append("")
    lines.append("  restore with:")
    lines.append(f'    copy /Y "{backup}" "{settings}"')

elif phase == "restore":
    shutil.copy2(backup, settings)
    lines.append(f"  restored from {backup}")
    lines.append(f"  md5     {digest(settings)}")

elif phase == "write":
    before = entries(settings)
    lines.append(f"  md5 before   {digest(settings)}")
    lines.append(f"  entries      {len(before)}")
    lines.append(f"  mtime before {settings.stat().st_mtime_ns}")

    merge_and_write(settings, {GLOBAL_KEY: NONCE, PER_GAME_KEY: NONCE})

    after = entries(settings)
    lines.append(f"  md5 after    {digest(settings)}")
    lines.append(f"  entries      {len(after)}")
    lines.append(f"  lost on merge: {sorted(set(before) - set(after)) or 'none'}")
    lines.append(f"  added:         {sorted(set(after) - set(before))}")
    lines.append("")
    lines.append("  Now change one setting in the ES UI so the session is dirty, quit ES,")
    lines.append("  and run the check phase.")

else:
    found = entries(settings)
    lines.append(f"  md5      {digest(settings)}")
    lines.append(f"  entries  {len(found)}")
    lines.append(f"  mtime    {settings.stat().st_mtime_ns}")
    lines.append("")

    for key in (GLOBAL_KEY, PER_GAME_KEY):
        value = found.get(key)
        verdict = "SURVIVED" if value == NONCE else ("GONE" if value is None else f"CHANGED to {value!r}")
        lines.append(f"  {verdict:<10} {key}")

    original = entries(backup)
    lines.append("")
    lines.append(f"  against the backup: {len(original)} entries before, {len(found)} now")
    lines.append(f"    dropped by ES: {sorted(set(original) - set(found) - {GLOBAL_KEY, PER_GAME_KEY})}")
    lines.append(f"    added by ES:   {sorted(set(found) - set(original) - {GLOBAL_KEY, PER_GAME_KEY})}")

    # Names only, never values. es_settings.cfg holds plaintext credentials
    # (ScreenScraperPass, global.retroachievements.password and .token, IGDBSecret), so a
    # transcript that echoed a value could carry one into a commit or a paste.
    changed = [
        name
        for name in sorted(set(original) & set(found))
        if original[name][1] != found[name][1]
    ]
    lines.append(f"    keys whose value ES changed: {changed or 'none'}")
    lines.append("")
    lines.append("  A dropped user-visible change means ES rewrote the whole file from its")
    lines.append("  boot-time model, which is the outcome that moves the design.")

record_offline("probe9-es-settings-two-writer", lines)
