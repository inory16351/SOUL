"""Tiny MCP-over-HTTP client for the running mcp-for-unity server.
usage: python umcp.py <tool> [json-args | @file.json]
       python umcp.py --list | --read <uri>
       python umcp.py --code <file.cs>   (execute_code with file contents)
"""
import json, sys, urllib.request

URL = "http://127.0.0.1:8080/mcp"
sid = None

def post(msg, timeout=600):
    global sid
    h = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
    if sid: h["mcp-session-id"] = sid
    req = urllib.request.Request(URL, json.dumps(msg).encode(), h)
    with urllib.request.urlopen(req, timeout=timeout) as r:
        sid = r.headers.get("mcp-session-id") or sid
        body = r.read().decode("utf-8", "replace")
    out = None
    for line in body.splitlines():
        if line.startswith("data:"):
            d = json.loads(line[5:])
            if "id" in d and d.get("id") == msg.get("id"): out = d
    if out is None and body.strip().startswith("{"): out = json.loads(body)
    return out

post({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2025-03-26", "capabilities": {}, "clientInfo": {"name": "claude", "version": "1"}}})
post({"jsonrpc": "2.0", "method": "notifications/initialized"})

a = sys.argv[1:]
if a[0] == "--list":
    r = post({"jsonrpc": "2.0", "id": 2, "method": "tools/list"})
    for t in r["result"]["tools"]: print(t["name"], "-", (t.get("description") or "")[:110].replace("\n", " "))
elif a[0] == "--schema":
    r = post({"jsonrpc": "2.0", "id": 2, "method": "tools/list"})
    for t in r["result"]["tools"]:
        if t["name"] == a[1]: print(json.dumps(t, indent=1)[:6000])
elif a[0] == "--read":
    r = post({"jsonrpc": "2.0", "id": 2, "method": "resources/read", "params": {"uri": a[1]}})
    print(json.dumps(r, ensure_ascii=False)[:20000])
else:
    if a[0] == "--code":
        tool = "execute_code"
        args = {"action": "execute", "code": open(a[1], encoding="utf-8").read()}
        if len(a) > 2: args.update(json.loads(a[2]))
    else:
        tool = a[0]
        raw = a[1] if len(a) > 1 else "{}"
        args = json.load(open(raw[1:], encoding="utf-8")) if raw.startswith("@") else json.loads(raw)
    r = post({"jsonrpc": "2.0", "id": 2, "method": "tools/call", "params": {"name": tool, "arguments": args}})
    res = r.get("result", r)
    for c in res.get("content", []) if isinstance(res, dict) else []:
        if c.get("type") == "text": print(c["text"][:30000])
        elif c.get("type") == "image":
            import base64, os
            p = os.path.join(os.path.dirname(os.path.abspath(__file__)), "shot.png")
            open(p, "wb").write(base64.b64decode(c["data"])); print("image ->", p)
    if not isinstance(res, dict) or "content" not in res: print(json.dumps(r, ensure_ascii=False)[:5000])
