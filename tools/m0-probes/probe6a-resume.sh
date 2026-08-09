#!/usr/bin/env bash
# M0 probe 6a: what an interrupted download can recover, without unplugging anything.
#
# "What happens when Wi-Fi drops mid-download" only matters because of what follows it:
# whether the transfer can resume rather than restart. Killing the client mid-transfer
# reproduces the part that governs the design (a truncated file plus a server that may or
# may not honour Range) without disturbing the operator's network.
#
# Checks, in order:
#   1  does the content endpoint advertise Accept-Ranges and a Content-Length
#   2  does a Range request return 206 with exactly the requested bytes
#   3  after a hard kill mid-transfer, does curl -C - resume and produce a correct file
#
# Reads only. Requires ROMM_URL, ROMM_TOKEN and a rom id.
set -uo pipefail

: "${ROMM_URL:?set ROMM_URL}"
: "${ROMM_TOKEN:?set ROMM_TOKEN}"
ROM_ID="${1:?usage: probe6a-resume.sh <rom-id> <file-name>}"
FILE_NAME="${2:?usage: probe6a-resume.sh <rom-id> <file-name>}"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

AUTH=(-H "Authorization: Bearer ${ROMM_TOKEN}")
ENCODED=$(python -c "import urllib.parse,sys; print(urllib.parse.quote(sys.argv[1]))" "$FILE_NAME")
URL="${ROMM_URL}/api/roms/${ROM_ID}/content/${ENCODED}"

echo "=== 1. HEAD: does the endpoint advertise range support ==="
curl -s -I -m 60 "${AUTH[@]}" "$URL" \
  | grep -i -E "^(HTTP|accept-ranges|content-length|content-type|content-disposition|etag|last-modified)" \
  | sed 's/^/  /'

TOTAL=$(curl -s -I -m 60 "${AUTH[@]}" "$URL" | tr -d '\r' | awk 'tolower($1)=="content-length:"{print $2}')
echo "  parsed content-length: ${TOTAL:-unknown}"
echo

echo "=== 2. Range request: bytes 100-1123 ==="
CODE=$(curl -s -m 60 "${AUTH[@]}" -H "Range: bytes=100-1123" -o "$WORK/range.bin" -w '%{http_code}' "$URL")
GOT=$(wc -c < "$WORK/range.bin")
echo "  status=$CODE  bytes=$GOT  (expected 206 and 1024)"
if [ "$CODE" = "206" ] && [ "$GOT" = "1024" ]; then
  echo "  RESULT: server honours Range"
  RANGE_OK=1
else
  echo "  RESULT: server does NOT honour Range, so downloads cannot resume"
  RANGE_OK=0
fi
echo

echo "=== 3. interrupt and resume ==="
# Cap the rate so there is a window to kill the transfer in.
curl -s -m 600 --limit-rate 3M "${AUTH[@]}" -o "$WORK/partial.bin" "$URL" &
CURL_PID=$!
sleep 3
kill -9 "$CURL_PID" 2>/dev/null
wait "$CURL_PID" 2>/dev/null
PARTIAL=$(wc -c < "$WORK/partial.bin" 2>/dev/null || echo 0)
echo "  killed after 3s with $PARTIAL bytes on disk"

if [ "$RANGE_OK" = "1" ]; then
  RESUME_CODE=$(curl -s -m 600 "${AUTH[@]}" -C - -o "$WORK/partial.bin" -w '%{http_code}' "$URL")
  FINAL=$(wc -c < "$WORK/partial.bin")
  echo "  resume status=$RESUME_CODE  final=$FINAL  expected=${TOTAL:-?}"

  curl -s -m 600 "${AUTH[@]}" -o "$WORK/whole.bin" "$URL"
  if cmp -s "$WORK/partial.bin" "$WORK/whole.bin"; then
    echo "  RESULT: resumed file is byte-identical to a clean download"
  else
    echo "  RESULT: resumed file DIFFERS from a clean download (resume is unsafe)"
  fi
else
  echo "  skipped: server does not honour Range"
fi
