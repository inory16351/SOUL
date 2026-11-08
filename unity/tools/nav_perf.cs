// Nav cost with 10 machines (5 hounds + 5 wardens) chasing a player at __P__ from __S__ (play mode).
// Times EnemyManager.Update at 60 Hz: cold grid (first 5 s) then warm grid (next 5 s).
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>(); var sb = new System.Text.StringBuilder();
var P = new UnityEngine.Vector3(__P__); var S = new UnityEngine.Vector3(__S__);
g.enemies.ClearAll(); g.Simulate(3.2f);
g.Teleport(P); g.ch.root.position = P; g.ch.UpdateWorld(); g.Simulate(.1f);
g.enemies.passive = true;
for (int i = 0; i < 10; i++) { float a = i * .628f; g.enemies.SpawnAt(i % 2 == 0 ? "crawler" : "warden", S + new UnityEngine.Vector3(UnityEngine.Mathf.Cos(a) * 3, 0, UnityEngine.Mathf.Sin(a) * 3)); }
g.Simulate(.8f); g.player.pos = g.ch.root.position;
var sw = new System.Diagnostics.Stopwatch();
foreach (var phase in new[] { "cold", "warm" })
{
    if (phase == "cold") Soul.NavGrid.Clear();
    Soul.NavGrid.ResetStats(); double tot = 0, mx = 0, nmx = 0; int over1 = 0;
    for (int f = 0; f < 300; f++)
    {
        double n0 = Soul.NavGrid.ms; sw.Reset(); sw.Start(); g.enemies.Update(1 / 60f, g.player); sw.Stop();
        double e = sw.Elapsed.TotalMilliseconds, n = Soul.NavGrid.ms - n0; tot += e; if (e > mx) mx = e; if (n > nmx) nmx = n; if (n > 1) over1++;
    }
    sb.AppendFormat("{0}: EnemyManager.Update avg {1:F3} ms max {2:F2} | nav avg {3:F3} ms/frame, worst {4:F2}, frames >1 ms {5}/300 | {6} searches {7} expanded {8} edges\n",
        phase, tot / 300, mx, Soul.NavGrid.ms / 300, nmx, over1, Soul.NavGrid.searches, Soul.NavGrid.expanded, Soul.NavGrid.edgesBuilt);
}
int up = 0; foreach (var e in g.enemies.list) if (e.alive && e.pos.y > P.y - .3f) up++;
sb.AppendFormat("after 10 s: {0}/10 at the player's height\n", up);
g.enemies.ClearAll();
return sb.ToString();
