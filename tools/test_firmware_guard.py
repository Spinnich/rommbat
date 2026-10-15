"""Tests for tools/firmware_guard.py. Run: python -m unittest discover -s tools -p "test_*.py"."""

from __future__ import annotations

import hashlib
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import firmware_guard  # noqa: E402


class FirmwareGuardTests(unittest.TestCase):
    def test_the_list_holds_both_sources(self):
        known = firmware_guard.known_md5s()
        # psxonpsp660.bin, which RetroBat's list names for psx and the subset carries.
        self.assertIn("c53ca5908936d412331790f4426c6c33", known)
        # SMS_kr.sms, which only BizHawk's list reaches.
        self.assertIn("e49e63328c78fe3d24ae32bf903583e0", known)

    def test_a_planted_file_on_the_list_fails(self):
        # No firmware bytes can be committed to test this, so the planted file is generated and
        # its md5 joins the real list, which is what the guard compares against.
        with tempfile.TemporaryDirectory() as scratch:
            root = Path(scratch)
            subprocess.run(["git", "init", "-q", str(root)], check=True)
            planted = root / "bios" / "planted.bin"
            planted.parent.mkdir()
            planted.write_bytes(os.urandom(4096))
            clean = root / "notes.txt"
            clean.write_text("not firmware\n", encoding="utf-8")
            known = {**firmware_guard.known_md5s(), hashlib.md5(planted.read_bytes()).hexdigest(): "planted.bin"}

            paths = firmware_guard.candidates(root)
            self.assertCountEqual(paths, [planted, clean])
            self.assertEqual(firmware_guard.find(paths, known), [(planted, "planted.bin")])

    def test_an_ignored_file_is_not_a_candidate(self):
        with tempfile.TemporaryDirectory() as scratch:
            root = Path(scratch)
            subprocess.run(["git", "init", "-q", str(root)], check=True)
            (root / ".gitignore").write_text("*.bin\n", encoding="utf-8")
            (root / "dump.bin").write_bytes(b"\0" * 16)

            self.assertEqual(firmware_guard.candidates(root), [root / ".gitignore"])

    def test_the_repository_ignores_firmware_shaped_files(self):
        names = ["bios/scph5501.bin", "bios/32X_G_BIOS.BIN", "bios/bios_U.sms", "bios/kick.rom", "switch/prod.keys", "PS3UPDAT.PUP", "bios/neogeo.zip"]
        # Case-sensitive, as git is on CI's Linux runner, whatever this checkout's setting.
        ignored = subprocess.run(
            ["git", "-c", "core.ignorecase=false", "-C", str(firmware_guard.ROOT), "check-ignore", "--no-index", *names],
            capture_output=True, text=True,
        ).stdout.split()
        self.assertCountEqual(ignored, names)


if __name__ == "__main__":
    unittest.main()
