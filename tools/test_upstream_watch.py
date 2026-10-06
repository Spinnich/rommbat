"""Tests for upstream_watch.py's decisions. Run: python3 -m unittest discover -s tools -p "test_*.py" """

from __future__ import annotations

import unittest

import upstream_watch as watch


def release(tag: str, prerelease: bool = False) -> watch.Release:
    found = watch.normalise(tag, prerelease, "2026-10-06T11:04:59Z")
    assert found is not None
    return found


class NormaliseTest(unittest.TestCase):
    def test_romm_prerelease(self) -> None:
        r = release("5.4.0-alpha.1", True)
        self.assertEqual((r.version, r.line), ("5.4.0-alpha.1", "5.4"))

    def test_retrobat_beta_tag_reads_as_a_prerelease_of_its_version(self) -> None:
        r = release("beta_8.3.0", True)
        self.assertEqual((r.version, r.line), ("8.3.0-beta", "8.3"))
        self.assertLess(r.key, release("8.3.0").key)

    def test_a_two_part_stable_and_a_v_prefix(self) -> None:
        self.assertEqual(release("8.0").line, "8.0")
        self.assertEqual(release("v5.3.1").version, "5.3.1")

    def test_a_tag_that_is_not_a_version_is_skipped(self) -> None:
        self.assertIsNone(watch.normalise("continuous", True, ""))

    def test_the_prerelease_flag_ranks_even_an_unsuffixed_tag_below_its_release(self) -> None:
        self.assertLess(release("8.3.0", True).key, release("8.3.0").key)


class PlanTest(unittest.TestCase):
    def test_lines_above_the_floor_open_one_issue_each(self) -> None:
        releases = [release("5.3.0"), release("5.3.1"), release("5.4.0-alpha.1", True)]
        actions = watch.plan("RomM", "5.3.1", releases, [])
        self.assertEqual([(a.kind, a.title) for a in actions], [("open", "RomM 5.4: scout and adopt")])
        self.assertIn("`5.4.0-alpha.1` | prerelease", actions[0].body)

    def test_a_patch_in_the_floors_line_is_above_the_floor(self) -> None:
        actions = watch.plan("RomM", "5.3.1", [release("5.3.2")], [])
        self.assertEqual([a.title for a in actions], ["RomM 5.3: scout and adopt"])

    def test_a_prerelease_of_the_floor_is_not_above_it(self) -> None:
        self.assertEqual(watch.plan("RomM", "5.3.1", [release("5.3.1-beta.1", True)], []), [])

    def test_an_issue_that_lists_every_release_is_left_alone(self) -> None:
        releases = [release("5.4.0-alpha.1", True)]
        opened = watch.plan("RomM", "5.3.1", releases, [])[0]
        issue = watch.Issue(7, opened.title, opened.body, True)
        self.assertEqual(watch.plan("RomM", "5.3.1", releases, [issue]), [])

    def test_a_new_release_updates_the_table_and_comments(self) -> None:
        first = [release("5.4.0-alpha.1", True)]
        opened = watch.plan("RomM", "5.3.1", first, [])[0]
        edited = "Notes the maintainer added.\n\n" + opened.body
        issue = watch.Issue(7, opened.title, edited, True)
        actions = watch.plan("RomM", "5.3.1", first + [release("5.4.0")], [issue])
        self.assertEqual([(a.kind, a.number) for a in actions], [("update", 7), ("comment", 7)])
        self.assertTrue(actions[0].body.startswith("Notes the maintainer added."))
        self.assertEqual(watch.tags_in(actions[0].body), {"5.4.0-alpha.1", "5.4.0"})
        self.assertIn("**stable**. Adoption is due", actions[1].body)
        self.assertNotIn("alpha.1", actions[1].body)

    def test_a_closed_issue_is_respected_until_upstream_ships_something_new(self) -> None:
        first = [release("beta_8.3.0", True)]
        opened = watch.plan("RetroBat", "8.2.1", first, [])[0]
        closed = watch.Issue(9, opened.title, opened.body, False)
        self.assertEqual(watch.plan("RetroBat", "8.2.1", first, [closed]), [])
        actions = watch.plan("RetroBat", "8.2.1", first + [release("8.3.0")], [closed])
        self.assertEqual([a.kind for a in actions], ["open"])


if __name__ == "__main__":
    unittest.main()
