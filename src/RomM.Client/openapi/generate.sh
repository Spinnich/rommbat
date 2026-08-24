#!/usr/bin/env bash
# Regenerate src/RomM.Client/Generated/RomMApiSchema.g.cs from the pinned schema.
#
# The generated file is committed on purpose: an upstream deploy must not be able
# to change the contract mid-session. Re-run this only when deliberately moving
# the pin, and review the diff.
set -euo pipefail

cd "$(dirname "$0")"

PINNED="romm-5.1.0.json"
# Kept beside nswag.json rather than in the system temp directory: NSwag resolves
# the path itself, and a Git Bash /tmp path is not one a Windows process can open.
NORMALIZED="openapi-normalized.tmp.json"
trap 'rm -f "$NORMALIZED"' EXIT

python3 normalize.py "$PINNED" "$NORMALIZED"

# nswag.json reads its input from $(SchemaPath) so the pinned file stays the
# thing under version control and the normalised copy stays a build artefact.
dotnet nswag run nswag.json "/variables:SchemaPath=$NORMALIZED"

# NSwag disables CS1573 and CS1591 in its own header but not CS1570 or CS1572, and
# Directory.Build.props turns the doc-comment warnings on for the whole solution while
# build.yml builds with -warnaserror. The <summary> blocks here are the schema's
# `description` strings carried verbatim, so a RomM release whose description text contains
# a raw < or & would fail the build in a file nobody authored. Added here rather than by
# hand, so a regeneration does not drop them.
GENERATED="../Generated/RomMApiSchema.g.cs"

if ! grep -q "disable 1570" "$GENERATED"; then
  sed -i '/^#pragma warning disable 1591 /a#pragma warning disable 1570 // Disable "CS1570 XML comment has badly formed XML" (schema description text is carried verbatim)#pragma warning disable 1572 // Disable "CS1572 XML comment has a param tag, but there is no parameter by that name"' "$GENERATED"
fi

echo "Generated $GENERATED from $PINNED"
