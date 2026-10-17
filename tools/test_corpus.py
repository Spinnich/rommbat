"""Tests for tools/corpus.py. Run: python -m unittest discover -s tools -p "test_*.py"."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import corpus  # noqa: E402

PAGE = """## How it is pulled

| Not | a | corpus | table | here |
| --- | - | ------ | ----- | ---- |
| a   | b | c      | d     | e    |

## nes

| Game                 | Platform | Size   | md5                                | Covers  | First save |
| -------------------- | -------- | ------ | ---------------------------------- | ------- | ---------- |
| `Zelda (USA).zip`    | `nes`    | 65,652 | `614fb3085826e62f3be3a3fe0b931689` | Battery | A name     |

Wanted: prose under a table is not a row.

## psx

| Game              | Platform | Size        | md5                                | Covers    | First save |
| ----------------- | -------- | ----------- | ---------------------------------- | --------- | ---------- |
| `Set (USA)`       | `psx`    | 779,304,107 | `2cb39eccdb821396ba1e665ac9d1c1ef` | Two discs | Not timed  |
"""


class ParseTests(unittest.TestCase):
    def test_only_tables_with_the_corpus_header_are_read(self):
        parsed = corpus.parse(PAGE)
        self.assertEqual(list(parsed), ["nes", "psx"])
        self.assertEqual(
            parsed["nes"], [corpus.Entry("nes", "Zelda (USA).zip", "nes", 65652, "614fb3085826e62f3be3a3fe0b931689")]
        )
        self.assertEqual(parsed["psx"][0].size, 779304107)

    def test_a_row_without_an_md5_is_refused(self):
        broken = PAGE.replace("`614fb3085826e62f3be3a3fe0b931689`", "unknown")
        with self.assertRaisesRegex(ValueError, "no md5"):
            corpus.parse(broken)

    def test_the_page_in_the_repository_parses(self):
        parsed = corpus.parse(corpus.CORPUS.read_text(encoding="utf-8"))
        self.assertIn("snes", parsed)
        names = [(entry.platform, entry.fs_name) for entries in parsed.values() for entry in entries]
        self.assertEqual(len(names), len(set(names)), "a game appears twice")


class ResolveTests(unittest.TestCase):
    entry = corpus.Entry("nes", "Zelda (USA).zip", "nes", 100, "a" * 32)

    def test_the_exact_file_name_wins_over_a_near_match(self):
        candidates = [
            {"id": 2, "fs_name": "Zelda (USA) (Virtual Console).zip", "md5_hash": "a" * 32, "fs_size_bytes": 100},
            {"id": 1, "fs_name": "Zelda (USA).zip", "md5_hash": "a" * 32, "fs_size_bytes": 100},
        ]
        self.assertEqual(corpus.judge(self.entry, candidates), ("ok", candidates[1]))

    def test_a_moved_md5_is_changed_not_ok(self):
        rom = {"id": 1, "fs_name": "Zelda (USA).zip", "md5_hash": "b" * 32, "fs_size_bytes": 100}
        self.assertEqual(corpus.judge(self.entry, [rom])[0], "changed")

    def test_no_exact_name_is_missing(self):
        self.assertEqual(corpus.judge(self.entry, []), ("missing", None))

    def test_only_a_file_extension_is_stripped(self):
        self.assertEqual(corpus.stem("Metal Gear Solid (USA)"), "Metal Gear Solid (USA)")
        self.assertEqual(corpus.stem("Zelda (USA).zip"), "Zelda (USA)")


class OutcomeTests(unittest.TestCase):
    def test_membership_is_reconciled_both_ways(self):
        outcome = corpus.Outcome("nes", resolved={1: ResolveTests.entry, 2: ResolveTests.entry})
        outcome.collection = {"id": 9}
        outcome.members = {2: "kept", 3: "stray"}
        self.assertEqual(outcome.to_add, [1])
        self.assertEqual(outcome.to_remove, [3])
        self.assertFalse(outcome.matches)

    def test_a_missing_game_does_not_make_a_mismatch(self):
        outcome = corpus.Outcome("nes", resolved={1: ResolveTests.entry}, missing=[ResolveTests.entry])
        outcome.collection = {"id": 9}
        outcome.members = {1: "in"}
        self.assertTrue(outcome.matches)


if __name__ == "__main__":
    unittest.main()
