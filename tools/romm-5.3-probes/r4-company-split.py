"""R4: whether developers and publishers are populated, and what indexing companies costs (#186).

Finding 7 and RomRow.Developers cite a sample of 3,000 rows over ten platforms: 398 of 400
rows carrying the split on the one platform rescanned since 5.3.0, 0 of 300 on each of nine
that were not, and companies[0] naming someone other than the developer on 41% of split rows.
No script recorded how that sample was drawn, so this walks every platform instead. A whole
library is a population rather than a sample, and the next adoption can re-take it exactly.

Per row it counts:

  companies      non-empty
  split          developers or publishers non-empty, which only a scan since 5.3.0 writes
  one and one    exactly one developer and exactly one publisher
  sorted pair    companies equals developers + publishers, sorted
  wrong role     companies[0] is not developers[0], on rows carrying both

Read-only. Every request is a GET.
"""

from __future__ import annotations

import sys

import _common


def main() -> int:
    version = _common.server_version()
    held = _common.platforms()

    columns = ["rows", "companies", "split", "one and one", "sorted pair", "wrong role"]
    totals = dict.fromkeys(columns, 0)
    per_platform: list[tuple[str, dict]] = []

    for platform in held:
        counts = dict.fromkeys(columns, 0)
        for row in _common.walk({"platform_ids": platform["id"]}):
            meta = row.get("metadatum") or {}
            companies = meta.get("companies") or []
            developers = meta.get("developers") or []
            publishers = meta.get("publishers") or []

            counts["rows"] += 1
            counts["companies"] += bool(companies)
            if not (developers or publishers):
                continue
            counts["split"] += 1
            counts["one and one"] += len(developers) == 1 and len(publishers) == 1
            counts["sorted pair"] += companies == sorted(developers + publishers)
            if companies and developers:
                counts["wrong role"] += companies[0] != developers[0]

        # Two platforms can share a slug and differ in folder, so both go in the label.
        per_platform.append((f"{platform.get('slug')} ({platform.get('fs_slug')})", counts))
        for key in columns:
            totals[key] += counts[key]
        print(f"  {per_platform[-1][0]}: {counts}", file=sys.stderr)

    def share(part: int, whole: int) -> str:
        return f"{part} ({part / whole:.1%})" if whole else str(part)

    lines = [
        "# R4: the company split, walked across every platform",
        "",
        f"Server reports `{version}`. {len(held)} platforms, {totals['rows']} rows.",
        "",
        "## The library",
        "",
        "| rows | companies | split | one and one | sorted pair | wrong role |",
        "| --- | --- | --- | --- | --- | --- |",
        f"| {totals['rows']} | {share(totals['companies'], totals['rows'])} | "
        f"{share(totals['split'], totals['rows'])} | {share(totals['one and one'], totals['split'])} | "
        f"{share(totals['sorted pair'], totals['split'])} | {share(totals['wrong role'], totals['split'])} |",
        "",
        "Shares in the last three columns are of split rows.",
        "",
        "## Platforms carrying any split",
        "",
        "| platform | rows | companies | split | one and one | sorted pair | wrong role |",
        "| --- | --- | --- | --- | --- | --- | --- |",
    ]
    unsplit = []
    for slug, counts in per_platform:
        if not counts["split"]:
            unsplit.append((slug, counts))
            continue
        lines.append(
            f"| {slug} | {counts['rows']} | {counts['companies']} | {share(counts['split'], counts['rows'])} | "
            f"{counts['one and one']} | {counts['sorted pair']} | {counts['wrong role']} |"
        )

    lines += [
        "",
        f"## {len(unsplit)} platforms carrying no split",
        "",
        f"{sum(c['rows'] for _s, c in unsplit)} rows, "
        f"{sum(c['companies'] for _s, c in unsplit)} of them carrying companies.",
    ]

    _common.record("r4-company-split", lines)
    return 0


if __name__ == "__main__":
    sys.exit(main())
