#!/usr/bin/env bash
# Wait until Unity is idle (not compiling) and ready; optional arg "play" also waits for play mode.
# The editor_state resource goes stale while the editor window is unfocused (its heartbeat stops ticking) even
# though execute_code still answers, so when the state is stale we ask the editor directly.
export PYTHONIOENCODING=utf-8
DIR="$(cd "$(dirname "$0")" && pwd)"
PROBE="$DIR/.probe.cs"
echo 'return (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating ? "busy" : "idle") + "|" + (UnityEditor.EditorApplication.isPlaying ? "playing" : "edit");' > "$PROBE"
for i in $(seq 1 75); do
  s=$(python "$DIR/umcp.py" --read mcpforunity://editor/state 2>/dev/null)
  if echo "$s" | grep -q 'is_stale\\":true' || ! echo "$s" | grep -q 'ready_for_tools\\":true'; then
    r=$(timeout 60 python "$DIR/umcp.py" --code "$PROBE" 2>/dev/null)
    if echo "$r" | grep -q '"result":"idle|'; then
      if [ "${1:-}" != "play" ] || echo "$r" | grep -q 'idle|playing'; then echo ready; exit 0; fi
    fi
  elif echo "$s" | grep -q 'is_compiling\\":false'; then
    if [ "${1:-}" != "play" ] || echo "$s" | grep -q 'is_playing\\":true'; then echo ready; exit 0; fi
  fi
  sleep 4
done
echo timeout; exit 1
