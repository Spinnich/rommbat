"""Tests for upstream_watch.py's decisions. Run: python3 -m unittest discover -s tools -p "test_*.py" """

from __future__ import annotations

import unittest

import upstream_watch as watch


def release(tag: str, prerelease: bool = False, *assets: str) -> watch.Release:
    found = watch.normalize(tag, prerelease, "2026-10-06T11:04:59Z", list(assets))
    assert found is not None
    return found


def retrobat(tag: str, shipped: str, prerelease: bool = False) -> watch.Release:
    channel = "beta" if prerelease else "stable"
    setup = f"RetroBat-v{shipped}-{channel}-win64-setup.exe"
    return release(tag, prerelease, setup, setup + ".sha256.txt")


# The table #568 carried before the Version column existed.
LEGACY = (
    "Tracks RetroBat 8.3.\n\n"
    f"{watch.START}\n"
    "| Tag | Kind | Published | Run |\n"
    "| --- | --- | --- | --- |\n"
    "| `8.3.1` | **stable** | 2026-10-16 | `/upstream retrobat 8.3.1` |\n"
    f"{watch.END}\n"
)


class NormalizeTest(unittest.TestCase):
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
        self.assertIsNone(watch.normalize("continuous", True, ""))

    def test_the_prerelease_flag_ranks_even_an_unsuffixed_tag_below_its_release(self) -> None:
        self.assertLess(release("8.3.0", True).key, release("8.3.0").key)

    def test_the_setup_asset_names_the_shipped_version(self) -> None:
        r = retrobat("8.3.1", "8.3.1.1")
        self.assertEqual((r.tag, r.shipped, r.line), ("8.3.1", "8.3.1.1", "8.3"))
        self.assertGreater(r.key, release("8.3.1").key)

    def test_a_beta_asset_ships_its_core_version(self) -> None:
        self.assertEqual(retrobat("beta_8.3.0", "8.3.0", True).shipped, "8.3.0")

    def test_a_release_with_no_setup_asset_ships_its_tag(self) -> None:
        self.assertEqual(release("5.4.0-alpha.1", True).shipped, "5.4.0-alpha.1")
        self.assertEqual(release("5.4.0", False, "notes.txt").shipped, "5.4.0")

    def test_two_setup_assets_are_ambiguous_and_ship_the_tag(self) -> None:
        r = release("8.3.1", False, "RetroBat-v8.3.1.1-stable-win64-setup.exe",
                    "RetroBat-v8.3.1.2-stable-win64-setup.exe")
        self.assertEqual(r.shipped, "8.3.1")


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
        self.assertEqual(
            watch.rows_in(actions[0].body), {("5.4.0-alpha.1", "5.4.0-alpha.1"), ("5.4.0", "5.4.0")}
        )
        self.assertIn("**stable**. Adoption is due", actions[1].body)
        self.assertNotIn("alpha.1", actions[1].body)

    def test_a_closed_issue_is_respected_until_upstream_ships_something_new(self) -> None:
        first = [release("beta_8.3.0", True)]
        opened = watch.plan("RetroBat", "8.2.1", first, [])[0]
        closed = watch.Issue(9, opened.title, opened.body, False)
        self.assertEqual(watch.plan("RetroBat", "8.2.1", first, [closed]), [])
        actions = watch.plan("RetroBat", "8.2.1", first + [release("8.3.0")], [closed])
        self.assertEqual([a.kind for a in actions], ["open"])

    def test_the_table_shows_the_shipped_version_and_the_comment_names_both(self) -> None:
        opened = watch.plan("RetroBat", "8.3.0", [retrobat("8.3.1", "8.3.1.1")], [])[0]
        self.assertIn("| `8.3.1` | `8.3.1.1` | **stable** |", opened.body)
        issue = watch.Issue(5, opened.title, opened.body, True)
        actions = watch.plan(
            "RetroBat", "8.3.0", [retrobat("8.3.1", "8.3.1.1"), retrobat("8.3.2", "8.3.2.1")], [issue]
        )
        self.assertIn("`8.3.2` ships RetroBat 8.3.2.1 and is **stable**", actions[1].body)

    def test_a_reupload_under_a_listed_tag_is_announced(self) -> None:
        opened = watch.plan("RetroBat", "8.3.0", [retrobat("8.3.1", "8.3.1.1")], [])[0]
        issue = watch.Issue(5, opened.title, opened.body, True)
        actions = watch.plan("RetroBat", "8.3.0", [retrobat("8.3.1", "8.3.1.2")], [issue])
        self.assertEqual([(a.kind, a.number) for a in actions], [("update", 5), ("comment", 5)])
        self.assertEqual(watch.rows_in(actions[0].body), {("8.3.1", "8.3.1.2")})
        self.assertIn("`8.3.1` now ships RetroBat 8.3.1.2", actions[1].body)

    def test_a_closed_issue_that_lists_the_tag_but_not_the_version_reopens_the_line(self) -> None:
        opened = watch.plan("RetroBat", "8.3.0", [retrobat("8.3.1", "8.3.1.1")], [])[0]
        closed = watch.Issue(9, opened.title, opened.body, False)
        self.assertEqual(watch.plan("RetroBat", "8.3.0", [retrobat("8.3.1", "8.3.1.1")], [closed]), [])
        actions = watch.plan("RetroBat", "8.3.0", [retrobat("8.3.1", "8.3.1.2")], [closed])
        self.assertEqual([a.kind for a in actions], ["open"])

    def test_a_legacy_table_announces_a_tag_whose_version_differs(self) -> None:
        issue = watch.Issue(568, "RetroBat 8.3: scout and adopt", LEGACY, True)
        actions = watch.plan("RetroBat", "8.3.0", [retrobat("8.3.1", "8.3.1.1")], [issue])
        self.assertEqual([a.kind for a in actions], ["update", "comment"])
        self.assertIn("| Tag | Version | Kind |", actions[0].body)
        self.assertEqual(watch.rows_in(actions[0].body), {("8.3.1", "8.3.1.1")})
        self.assertIn("`8.3.1` now ships RetroBat 8.3.1.1", actions[1].body)

    def test_a_legacy_row_whose_tag_is_its_version_stays_quiet(self) -> None:
        issue = watch.Issue(568, "RetroBat 8.3: scout and adopt", LEGACY, True)
        self.assertEqual(watch.plan("RetroBat", "8.3.0", [retrobat("8.3.1", "8.3.1")], [issue]), [])


if __name__ == "__main__":
    unittest.main()
