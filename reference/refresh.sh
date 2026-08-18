#!/usr/bin/env bash
# Re-pull the vendored upstream reference data, re-derive the plan's numbers, and
# check the bundled data files still match what their generators emit.
# Requires an authenticated `gh`. Review the resulting diff: a change here can
# invalidate a design decision in docs/PLAN.md.
set -euo pipefail
cd "$(dirname "$0")"

fetch() { # repo path outfile
  echo "  $1  $2"
  gh api "repos/$1/contents/$2" --jq '.content' | base64 -d >"$3"
}

echo "RetroBat:"
fetch RetroBat-Official/retrobat system/configgen/systems_names.lst systems_names.lst
fetch RetroBat-Official/retrobat system/templates/emulationstation/es_systems.cfg es_systems.cfg
fetch RetroBat-Official/emulatorlauncher .emulationstation/es_savestates.cfg es_savestates.cfg
fetch RetroBat-Official/emulatorlauncher batocera-systems/Resources/batocera-systems.json batocera-systems.json

echo "RomM:"
fetch rommapp/romm examples/config.batocera-retrobat.yml config.batocera-retrobat.yml
fetch rommapp/romm backend/models/fixtures/known_bios_files.json romm-known_bios_files.json
fetch rommapp/romm backend/utils/gamelist_exporter.py romm-gamelist_exporter.py

# The slug list is an enum in source, not a data file, so extract it.
echo "  rommapp/romm  backend/handler/metadata/base_handler.py (UniversalPlatformSlug)"
gh api repos/rommapp/romm/contents/backend/handler/metadata/base_handler.py --jq '.content' |
  base64 -d |
  grep -oE '^\s{4}[A-Z0-9_]+ *= *"[^"]+"' |
  sed -E 's/.*"([^"]+)"/\1/' |
  LC_ALL=C sort -u >romm-slugs.txt
# LC_ALL=C is required: these slugs are punctuation-heavy and locale-aware
# collation makes `sort -u` treat some distinct pairs as equal, silently
# undercounting (it collapsed one pair and reported 456 instead of 457).

echo "RetroBat version upstream:"
gh api repos/RetroBat-Official/retrobat/contents/build.ini --jq '.content' |
  base64 -d | grep -E '^retrobat_version=' | sed 's/^/  /'

# data/retrobat/bios.json and data/retrobat/platforms.json are derived from the files
# just fetched, so a refresh that moves a firmware entry or a platform name leaves them
# stale. Checked rather than regenerated, deliberately: this script's contract is "review
# the resulting diff", and silently rewriting a committed generated file hides exactly the
# change the script exists to surface. A failing check names the generator to run, and
# running it stays a decision.
#
# Collected rather than left to `set -e`, so one stale file does not hide the other or
# stop verify.py from reporting the numbers.
echo
stale=0
python3 ../tools/build-bios-manifest.py --check || stale=1
python3 ../tools/build-platform-map.py --check || stale=1

echo
python3 verify.py

if [ "$stale" -ne 0 ]; then
  echo
  echo "A bundled data file no longer matches the vendored source it is derived from."
  echo "Run the generator named above, review its diff, then re-run this script."
  exit 1
fi
