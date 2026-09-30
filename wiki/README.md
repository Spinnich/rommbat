# The guide

This folder is the end-user guide. [mkdocs.yml](../mkdocs.yml) builds it with MkDocs Material,
and the Guide workflow builds it with `--strict` on every PR and publishes it to GitHub Pages
from main. This file is a note to contributors and is not part of the site.

To preview it, from the repository root:

```bash
python3 -m pip install -r tools/docs/requirements.txt
mkdocs serve
```

Every page is in the guide's voice: second person, task first, for someone who is not a
developer ([the writing guide](../docs/contributing/writing.md#by-layer)). It leads with the
gamepad and names a terminal command only where the controller cannot do the job, linking to
the generated `reference/cli.md` for the options. A new page goes into `nav` in `mkdocs.yml`,
since `--strict` fails on a page the nav leaves out. A change to a screen or a command corrects
the page that describes it, in the same PR.

Two pages are generated, and a test fails when either is stale:

| Page                 | Built from                                     | By                                                   |
| -------------------- | ---------------------------------------------- | ---------------------------------------------------- |
| `platforms/index.md` | `data/certification.json` and `es_systems.cfg` | `tests/RomMBat.Tests/PlatformSupportPageTests.cs`    |
| `reference/cli.md`   | `rommbat-agent --help`                         | `tests/RomMBat.Agent.Tests/CliReferencePageTests.cs` |

Edit the source, never the page, then regenerate with `ROMMBAT_REGENERATE_DOCS=1 dotnet test`
and commit what it writes.

A guide page links to a developer doc by its full GitHub URL: the build only sees `wiki/`, and
`--strict` fails on a relative link that leaves it.
