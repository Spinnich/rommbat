# The guide

This folder is the end-user guide. [mkdocs.yml](../mkdocs.yml) builds it with MkDocs Material,
and the Guide workflow builds it with `--strict` on every PR and publishes it to GitHub Pages
from main. This file is a note to contributors and is not part of the site.

To preview it, from the repository root:

```bash
python3 -m pip install -r tools/docs/requirements.txt
mkdocs serve
```

The pages under `getting-started/`, `using/` and `saves/` are drafts that read like developer
notes: `dotnet run` commands, a throwaway tree at `D:\retrobat-test`. `mkdocs.yml` lists them as
drafts, so `mkdocs serve` shows them with a banner and the published site leaves them out.
Each one is rewritten in the guide's voice (second person, task first, for someone who is not a
developer, per [the writing guide](../docs/contributing/writing.md#by-layer)), its command
blocks give way to links to the generated `reference/cli.md`, and it then moves out of
`draft_docs` into the nav. Until then, a change that falsifies a sentence in a draft corrects
it, as for any other doc.

A guide page links to a developer doc by its full GitHub URL: the build only sees `wiki/`, and
`--strict` fails on a relative link that leaves it.
