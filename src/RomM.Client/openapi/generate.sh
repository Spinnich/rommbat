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

echo "Generated ../Generated/RomMApiSchema.g.cs from $PINNED"
