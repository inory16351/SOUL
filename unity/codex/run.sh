#!/usr/bin/env bash
# Runs the Codex modeling jobs in order (01_*.md ... 05_*.md), each with the shared rules prepended.
# A job that finishes is marked in done/<job>; a usage-limit error stops the run with exit code 75 so a
# caller can retry later. Re-running skips finished jobs.
#   bash unity/codex/run.sh            run all pending jobs
#   bash unity/codex/run.sh --probe    only check whether Codex is usable (exit 0 yes, 75 limited)
set -u
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DIR="$ROOT/unity/codex"
mkdir -p "$DIR/logs" "$DIR/done" "$DIR/results"
limited() { grep -qi "usage limit\|rate limit\|try again at" "$1"; }

if [ "${1:-}" = "--probe" ]; then
  out="$DIR/logs/probe.log"
  timeout 180 codex exec --skip-git-repo-check -s read-only -C "$ROOT" "Reply with exactly the word OK and nothing else." >"$out" 2>&1
  if limited "$out"; then echo "LIMITED $(grep -o 'try again at[^.]*' "$out" | head -1)"; exit 75; fi
  grep -q "^OK" "$out" && { echo "READY"; exit 0; }
  echo "UNKNOWN (see $out)"; exit 1
fi

for job in "$DIR"/0[1-9]_*.md; do
  id="$(basename "$job" .md)"
  [ -f "$DIR/done/$id" ] && { echo "skip $id (done)"; continue; }
  echo "=== $id  $(date '+%H:%M:%S')"
  log="$DIR/logs/$id.log"
  { cat "$DIR/00_common.md"; echo; echo "---"; echo; cat "$job"; } | \
    timeout 3600 codex exec --skip-git-repo-check -s workspace-write -C "$ROOT" - >"$log" 2>&1
  code=$?
  if limited "$log"; then echo "LIMITED during $id: $(grep -o 'try again at[^.]*' "$log" | head -1)"; exit 75; fi
  if [ $code -ne 0 ]; then echo "FAILED $id (exit $code, see $log)"; exit $code; fi
  date '+%F %T' >"$DIR/done/$id"; echo "done $id"
done
echo "ALL DONE"
