// One nav scenario (play mode): player stands at __P__, a hound + a warden are summoned at __S__ and chase for
// __SECS__ s (passive: no attacks). Logs position / height / path state each second. Fill the placeholders with sed.
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>(); var o = new System.Text.StringBuilder();
var P = new UnityEngine.Vector3(__P__); var S = new UnityEngine.Vector3(__S__); int secs = __SECS__;
g.enemies.ClearAll(); g.Simulate(3.2f);
g.Teleport(P); g.ch.root.position = P; g.ch.UpdateWorld(); g.Simulate(.1f);
g.enemies.passive = true;
var a = g.enemies.SpawnAt("crawler", S); var b = g.enemies.SpawnAt("warden", S + new UnityEngine.Vector3(1.5f, 0, 0));
o.AppendFormat("player {0} | spawn y {1:F2}/{2:F2}\n", g.ch.root.position.ToString("F2"), a.pos.y, b.pos.y);
Soul.NavGrid.ResetStats(); float minA = a.pos.y, minB = b.pos.y; var sw = System.Diagnostics.Stopwatch.StartNew();
for (int i = 1; i <= secs; i++)
{
    for (int f = 0; f < 10; f++) { g.Simulate(.1f); minA = UnityEngine.Mathf.Min(minA, a.pos.y); minB = UnityEngine.Mathf.Min(minB, b.pos.y); }
    var pp = g.ch.root.position;
    o.AppendFormat("  t{0}: hound {1} d{2:F1} {3}{4} | warden {5} d{6:F1} {7}{8}\n", i,
        a.pos.ToString("F1"), UnityEngine.Vector3.Distance(a.pos, pp), a.nav.pathing ? "path" : "direct", a.nav.waiting ? " WAIT" : "",
        b.pos.ToString("F1"), UnityEngine.Vector3.Distance(b.pos, pp), b.nav.pathing ? "path" : "direct", b.nav.waiting ? " WAIT" : "");
}
o.AppendFormat("  lowest y: hound {0:F2} warden {1:F2} | nav {2} searches, {3} expanded, {4} edges, {5:F1} ms | wall {6:F0} ms\n", minA, minB,
    Soul.NavGrid.searches, Soul.NavGrid.expanded, Soul.NavGrid.edgesBuilt, Soul.NavGrid.ms, sw.Elapsed.TotalMilliseconds);
return o.ToString();
