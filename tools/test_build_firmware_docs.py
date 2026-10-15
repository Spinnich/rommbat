"""Tests for tools/build-firmware-docs.py. Run: python -m unittest discover -s tools -p "test_*.py"."""

from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path

SPEC = importlib.util.spec_from_file_location("build_firmware_docs", Path(__file__).resolve().parent / "build-firmware-docs.py")
build_firmware_docs = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(build_firmware_docs)


class AnchorTests(unittest.TestCase):
    def test_a_repeated_slug_is_numbered_as_github_numbers_it(self):
        # msx2+ slugs to msx2, which the msx2 heading above it already holds.
        self.assertEqual(
            build_firmware_docs.anchors(["firmware matrix", "msx2", "msx2+"]),
            {"firmware matrix": "firmware-matrix", "msx2": "msx2", "msx2+": "msx2-1"},
        )

    def test_the_page_links_each_system_to_its_own_heading(self):
        page = (build_firmware_docs.OUTPUT).read_text(encoding="utf-8")
        self.assertIn("[msx2](#msx2)", page)
        self.assertIn("[msx2+](#msx2-1)", page)


if __name__ == "__main__":
    unittest.main()
