// Gate walk matrix (play mode): per link site, Teleport(pos - n*30) -> WalkTest(pos + n*30, 20), parts 0 / all (127) x both directions.
// Output per site: "idx Za-Zb kind : m0+ m0- m127+ m127-" (1 = got through), plus whether it matches the design rule.
// Expected: free passage all through; drop only from the high side (a -> b); rope never by walking; wall gates only with the part.
const int FROM = 0, TO = 99;   // site index range (split runs: execute_code times out around 30 s)
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>();
var sb = new System.Text.StringBuilder(); int owned = Soul.Parts.owned, bad = 0;
foreach (var s in Soul.ZoneWorld.Sites)
{
    if (s.index < FROM || s.index > TO) continue;
    var res = new System.Text.StringBuilder();
    bool ok = true;
    foreach (var mask in new[] { 0, 127 })
        foreach (var dir in new[] { 1, -1 })
        {
            Soul.Parts.owned = mask;
            var n = new UnityEngine.Vector3(s.n.x, 0, s.n.y) * dir; var p = new UnityEngine.Vector3(s.pos.x, 0, s.pos.y);
            g.Teleport(p - n * 30); g.WalkTest(p + n * 30, 20);
            var q = g.ch.root.position; var e = s.Local(q.x, q.z);
            bool through = e.y * dir > 15;
            res.Append(through ? '1' : '0');
            var k = s.link.kind; bool want;
            if (k == Soul.ZoneGraph.Kind.Free) want = true;
            else if (k == Soul.ZoneGraph.Kind.Drop) want = dir == 1;
            else if (s.link.part == Soul.Parts.Rope) want = false;
            else want = mask == 127;   // wall gates open both ways once the part is owned
            if (through != want) ok = false;
        }
    if (!ok) bad++;
    sb.AppendFormat("{0,2} Z{1}-Z{2} {3,-8}: {4} {5}\n", s.index, s.link.a, s.link.b, s.link.kind == Soul.ZoneGraph.Kind.Part ? Soul.Parts.Names[s.link.part] : s.link.kind.ToString(), res, ok ? "ok" : "MISMATCH");
}
Soul.Parts.owned = owned;
sb.Insert(0, "mismatches: " + bad + "\n");
return sb.ToString();
