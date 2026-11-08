#!/usr/bin/env bash
# run gates.cs in batches of 3 sites (one execute_code call times out around 30 s)
export PYTHONIOENCODING=utf-8; cd "$(dirname "$0")"
tmp="${TMPDIR:-/tmp}/gates_part.cs"
n=$(python umcp.py execute_code '{"action":"execute","code":"return Soul.ZoneWorld.Sites.Count;"}' | grep -o '"result":[0-9]*' | grep -o '[0-9]*$')
for ((a=0; a<n; a+=3)); do
  sed "s/const int FROM = 0, TO = 99;/const int FROM = $a, TO = $((a+2));/" gates.cs > "$tmp"
  python umcp.py --code "$tmp" | python -c "import sys,json; d=json.loads(sys.stdin.read()); print((d.get('data') or {}).get('result') or d)"
done
