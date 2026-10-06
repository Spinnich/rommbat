#!/usr/bin/env bash
# Re-pull the vendored upstream reference data at a release tag, re-derive the quoted numbers,
# and check the bundled data files still match what their generators emit.
# Requires an authenticated `gh`. Review the resulting diff: a change here can
# invalidate a design decision in docs/design/.
#
#   ./refresh.sh                                   # at the floor in code, into reference/
#   ./refresh.sh --ref romm=5.4.0-alpha.1 --out /tmp/scout
#   ./refresh.sh --ref retrobat=beta_8.3.0 --out /tmp/scout
#
# --ref takes a project's release tag; each project defaults to the floor in code, so the
# vendored files describe the builds RomMBat supports and not upstream's default branch.
# --out writes into a scratch directory instead of reference/, which is how a scout pass reads
# a prerelease without touching the vendored copy (the version-adoption skill).
set -euo pipefail
cd "$(dirname "$0")"

floor() { # source file -> the version its Minimum parses
  grep -oE 'Minimum \{ get; \} = ProductVersion\.Parse\("[^"]+"\)' "../$1" | sed -E 's/.*"([^"]+)".*/\1/'
}

romm_ref=$(floor src/RomM.Client/RomMServerVersion.cs)
retrobat_ref=$(floor src/RomMBat.Core/Diagnostics/RetroBatVersion.cs)
out=.

while [ $# -gt 0 ]; do
  case "$1" in
  --ref)
    case "${2:-}" in
    romm=?*) romm_ref=${2#romm=} ;;
    retrobat=?*) retrobat_ref=${2#retrobat=} ;;
    *)
      echo "--ref wants romm=<tag> or retrobat=<tag>" >&2
      exit 2
      ;;
    esac
    shift 2
    ;;
  --out)
    out=${2:?--out wants a directory}
    shift 2
    ;;
  *)
    echo "unknown argument: $1" >&2
    exit 2
    ;;
  esac
done

mkdir -p "$out"
out=$(cd "$out" && pwd)
here=$(pwd)

fetch() { # repo ref path outfile
  echo "  $1@$2  $3"
  gh api "repos/$1/contents/$3?ref=$2" --jq '.content' | base64 -d >"$out/$4"
}

# A RetroBat release downloads emulatorlauncher from its rolling `continuous` build
# (build.ini's emulatorlauncher_url), so no tag pins it. The nearest derivable ref is the last
# emulatorlauncher commit at or before the RetroBat release was published.
published=$(gh api "repos/RetroBat-Official/retrobat/releases/tags/$retrobat_ref" --jq '.published_at')
launcher_ref=$(gh api "repos/RetroBat-Official/emulatorlauncher/commits?until=$published&per_page=1" --jq '.[0].sha')

echo "RetroBat $retrobat_ref (published $published, emulatorlauncher ${launcher_ref:0:12}):"
fetch RetroBat-Official/retrobat "$retrobat_ref" system/configgen/systems_names.lst systems_names.lst
fetch RetroBat-Official/retrobat "$retrobat_ref" system/templates/emulationstation/es_systems.cfg es_systems.cfg
fetch RetroBat-Official/emulatorlauncher "$launcher_ref" .emulationstation/es_savestates.cfg es_savestates.cfg
fetch RetroBat-Official/emulatorlauncher "$launcher_ref" batocera-systems/Resources/batocera-systems.json batocera-systems.json

echo "RomM $romm_ref:"
fetch rommapp/romm "$romm_ref" backend/models/fixtures/known_bios_files.json romm-known_bios_files.json
fetch rommapp/romm "$romm_ref" backend/utils/gamelist_exporter.py romm-gamelist_exporter.py
fetch rommapp/romm "$romm_ref" backend/utils/platform_slugs.py romm-platform_slugs.py
fetch rommapp/romm "$romm_ref" backend/utils/platform_aliases.py romm-platform_aliases.py

# The slug enum lives in the module just fetched, so derive the list here rather than
# fetching twice. Keeping it as a plain list is what lets verify.py and the normalized-match
# layer ask "is this a slug" without parsing Python.
echo "  (derived)  romm-platform_slugs.py -> romm-slugs.txt"
grep -oE '^\s{4}[A-Z0-9_]+ *= *"[^"]+"' "$out/romm-platform_slugs.py" |
  sed -E 's/.*"([^"]+)"/\1/' |
  LC_ALL=C sort -u >"$out/romm-slugs.txt"
# LC_ALL=C is required: these slugs are punctuation-heavy and locale-aware
# collation makes `sort -u` treat some distinct pairs as equal, silently
# undercounting (it collapsed one pair and reported 456 instead of 457).

echo "RetroBat version at $retrobat_ref:"
gh api "repos/RetroBat-Official/retrobat/contents/build.ini?ref=$retrobat_ref" --jq '.content' |
  base64 -d | grep -E '^retrobat_version=' | sed 's/^/  /'

# The generators and verify.py read the directory just written, so a scratch refresh is
# checked against the committed data without being vendored.
if [ "$out" != "$here" ]; then
  export ROMMBAT_REFERENCE_DIR="$out"
fi

# data/retrobat/bios.json and data/retrobat/platforms.json are derived from the files
# just fetched, so a refresh that moves a firmware entry or a platform name leaves them
# stale. Checked rather than regenerated, deliberately: this script's contract is "review
# the resulting diff", and silently rewriting a committed generated file hides exactly the
# change the script exists to surface.
#
# Collected rather than left to `set -e`, so one stale file does not hide the other or
# stop verify.py from reporting the numbers.
echo
stale=0
python3 ../tools/build-bios-manifest.py --check || stale=1
python3 ../tools/build-platform-map.py --check || stale=1

echo
drift=0
python3 verify.py || drift=1

if [ "$stale" -ne 0 ]; then
  echo
  echo "A bundled data file no longer matches the vendored source it is derived from."
  if [ -n "${ROMMBAT_REFERENCE_DIR:-}" ]; then
    echo "This is a scratch refresh: report it as work the adoption owes, and regenerate nothing."
  else
    echo "Run the generator named above, review its diff, then re-run this script."
  fi
  exit 1
fi
exit "$drift"
