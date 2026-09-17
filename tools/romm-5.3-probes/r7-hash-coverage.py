"""R7: how many roms carry a sha1 and no md5, counting an empty string as absent (#112).

Two records disagree. A sample of 1,616 rows from three platforms found no row with a sha1 and
no md5, and finding 85 measured 91.0% md5 against 96.3% sha1 over 1,895 single-file roms,
which puts about a hundred rows in exactly that state. Finding 181 says how both can have been
seen: the server reports a missing hash as '' rather than null, so a test for "not null" counts
an empty string as a hash. Neither sample recorded its query.

This walks every platform, so it is a population and not a sample, and classifies each hash as
null, '' or a value. Presence means a value. The single-file population is the one finding 85
named: not has_multiple_files, and a file on disk (neither physical nor missing).

Read-only. Every request is a GET.
"""

from __future__ import annotations

import collections
import sys

import _common

HASHES = ("md5_hash", "sha1_hash", "crc_hash")


def form(value) -> str:
    if value is None:
        return "null"
    return "empty" if not str(value).strip() else "value"


def main() -> int:
    version = _common.server_version()
    held = _common.platforms()

    forms = {name: collections.Counter() for name in HASHES}
    joint = collections.Counter()
    loose = collections.Counter()
    rows = 0
    single = 0
    sha1_only: collections.Counter = collections.Counter()
    single_by_platform: collections.Counter = collections.Counter()

    for platform in held:
        slug = platform.get("slug") or str(platform["id"])
        for row in _common.walk({"platform_ids": platform["id"]}):
            rows += 1
            if row.get("has_multiple_files") or row.get("is_physical") or row.get("missing_from_fs"):
                continue
            single += 1
            single_by_platform[slug] += 1

            for name in HASHES:
                forms[name][form(row.get(name))] += 1

            md5 = form(row.get("md5_hash")) == "value"
            sha1 = form(row.get("sha1_hash")) == "value"
            joint[(md5, sha1)] += 1
            if sha1 and not md5:
                sha1_only[slug] += 1

            # The same pair read the way a "not null" test reads it.
            loose[(row.get("md5_hash") is not None, row.get("sha1_hash") is not None)] += 1
        print(f"  {slug}: {single_by_platform[slug]} single-file, {sha1_only[slug]} sha1 only", file=sys.stderr)

    def share(part: int) -> str:
        return f"{part} ({part / single:.1%})" if single else str(part)

    lines = [
        "# R7: hash coverage across every platform",
        "",
        f"Server reports `{version}`. {len(held)} platforms, {rows} rows, {single} single-file "
        "with a file on disk.",
        "",
        "## Each hash, by form",
        "",
        "| hash | value | empty string | null |",
        "| --- | --- | --- | --- |",
    ]
    for name in HASHES:
        lines.append(
            f"| `{name}` | {share(forms[name]['value'])} | {share(forms[name]['empty'])} | "
            f"{share(forms[name]['null'])} |"
        )

    lines += [
        "",
        "## md5 against sha1",
        "",
        "| md5 | sha1 | counting '' as absent | counting '' as present |",
        "| --- | --- | --- | --- |",
    ]
    for md5, sha1 in ((True, True), (True, False), (False, True), (False, False)):
        lines.append(
            f"| {'yes' if md5 else 'no'} | {'yes' if sha1 else 'no'} | {share(joint[(md5, sha1)])} | "
            f"{share(loose[(md5, sha1)])} |"
        )

    lines += [
        "",
        "## Where a sha1 arrives with no md5",
        "",
        "| platform | single-file | sha1 only |",
        "| --- | --- | --- |",
    ]
    for slug, count in sha1_only.most_common():
        if count:
            lines.append(f"| {slug} | {single_by_platform[slug]} | {count} |")

    _common.record("r7-hash-coverage", lines)
    return 0


if __name__ == "__main__":
    sys.exit(main())
