#!/usr/bin/env bash
# run each given .cs (play-mode test) until it returns a result: waits out reloads; if SoulGame was broken by a
# reload during play (NullReference in Teleport), restarts play mode (a broken play session is useless to everyone)
export PYTHONIOENCODING=utf-8; cd "$(dirname "$0")"
J='import sys,json; d=json.loads(sys.stdin.read()); print(((d.get("data") or {}).get("result") or str(d)).strip())'
for f in "$@"; do
  for t in $(seq 1 40); do
    p=$(python umcp.py --code .probe.cs 2>/dev/null | python -c "$J" 2>/dev/null)
    if [ "$p" = "idle|edit" ]; then python umcp.py manage_editor '{"action":"play"}' >/dev/null 2>&1; sleep 8; continue; fi
    [ "$p" = "idle|playing" ] || { sleep 10; continue; }
    r=$(python umcp.py --code "$f" 2>/dev/null | python -c "$J" 2>/dev/null)
    if echo "$r" | grep -q "NullReferenceException"; then python umcp.py manage_editor '{"action":"stop"}' >/dev/null 2>&1; sleep 6; continue; fi
    echo "$r" | grep -q "'success': False" && { sleep 10; continue; }
    echo "$r"; break
  done
done
