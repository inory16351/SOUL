// Enemy path finding test (play mode): find a ruin deck reachable by stairs, stand the player on it, summon a
// hound + a warden on the ground, simulate and log. Then a perf run (10 enemies) and the telegraph height check.
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>(); var sb = new System.Text.StringBuilder();
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
var wc = (System.Collections.IDictionary)typeof(Soul.WorldCollision).GetField("chunks", F).GetValue(null);
var cands = new System.Collections.Generic.List<UnityEngine.Vector2>();
var c0 = Soul.ZoneWorld.Center(0); foreach (var p in Soul.WorldStreamer.PlotCentres(c0.x - 250, c0.y - 250, 500)) cands.Add(p);
cands.Sort((u, v) => u.sqrMagnitude.CompareTo(v.sqrMagnitude)); if (cands.Count > 30) cands.RemoveRange(30, cands.Count - 30);
var swF = System.Diagnostics.Stopwatch.StartNew(); Soul.NavGrid.maxExpand = 700;
sb.AppendLine("plots " + cands.Count);
Soul.NavGrid.ResetStats();
UnityEngine.Vector3 top = UnityEngine.Vector3.zero, ground = UnityEngine.Vector3.zero, ledge = UnityEngine.Vector3.zero, ledgeGround = UnityEngine.Vector3.zero;
bool found = false, foundLedge = false, foundWall = false; UnityEngine.Vector3 w1 = UnityEngine.Vector3.zero, w2 = UnityEngine.Vector3.zero; var path = new System.Collections.Generic.List<UnityEngine.Vector3>();
foreach (var pc in cands)
{
    if (found && foundLedge && foundWall) break;
    g.Teleport(new UnityEngine.Vector3(pc.x, 0, pc.y)); g.Simulate(.05f);
    var surfs = new System.Collections.Generic.List<Soul.WorldCollision.Surf>();
    foreach (System.Collections.DictionaryEntry de in wc)
    {
        var list = (System.Collections.IList)de.Value.GetType().GetField("surfs").GetValue(de.Value);
        foreach (var o in list) { var s = (Soul.WorldCollision.Surf)o; if ((s.c - pc).magnitude < 16 && s.kind == Soul.PropSurface.BOX && s.gate == 0 && s.half.x > 1.2f && s.half.y > 1.2f) surfs.Add(s); }
    }
    for (int kw = 0; kw < 4 && !foundWall; kw++)
    {   // ground to ground across the structure: no straight walk, but a path around
        float aw = kw * 6.2832f / 8; var d = new UnityEngine.Vector3(UnityEngine.Mathf.Cos(aw), 0, UnityEngine.Mathf.Sin(aw)) * 13;
        var A = new UnityEngine.Vector3(pc.x, 0, pc.y) + d; var B = new UnityEngine.Vector3(pc.x, 0, pc.y) - d;
        A.y = Soul.WorldCollision.Floor(A.x, A.z, Soul.Terrain.Height(A.x, A.z)); B.y = Soul.WorldCollision.Floor(B.x, B.z, Soul.Terrain.Height(B.x, B.z));
        if (A.y > Soul.Terrain.Height(A.x, A.z) + .05f || B.y > Soul.Terrain.Height(B.x, B.z) + .05f) continue;
        if (!Soul.NavGrid.Straight(A, B, .6f, 40) && Soul.NavGrid.FindPath(A, B, .6f, path)) { foundWall = true; w1 = A; w2 = B; sb.AppendFormat("wall: {0} -> {1} path {2} cells\n", A.ToString("F1"), B.ToString("F1"), path.Count); }
    }
    foreach (var s in surfs)
    {
        float tg = Soul.Terrain.Height(s.c.x, s.c.y), h = s.top - tg;
        var T = new UnityEngine.Vector3(s.c.x, s.top, s.c.y);
        if (Soul.WorldCollision.Height(T.x, T.z) > s.top + .05f) continue;   // something above
        for (int k = 0; k < 4; k++)
        {
            float a = k * 6.2832f / 4 + .4f; var G = new UnityEngine.Vector3(pc.x + UnityEngine.Mathf.Cos(a) * 17, 0, pc.y + UnityEngine.Mathf.Sin(a) * 17);
            G.y = Soul.WorldCollision.Floor(G.x, G.z, Soul.Terrain.Height(G.x, G.z));
            if (G.y > Soul.Terrain.Height(G.x, G.z) + .05f) continue;
            bool reach = Soul.NavGrid.FindPath(G, T, .6f, path), straight = Soul.NavGrid.Straight(G, T, .6f, 40);
            if (!found && h > 2.2f && reach && !straight) { found = true; top = T; ground = G; sb.AppendFormat("stair deck top {0:F2} (+{1:F1}m) at {2} path {3} cells from {4}\n", s.top, h, T.ToString("F1"), path.Count, G.ToString("F1")); break; }
            if (!foundLedge && h > 1.0f && h < 2.0f && !reach) { foundLedge = true; ledge = T; ledgeGround = G; sb.AppendFormat("ledge (no stairs) +{0:F2}m at {1}\n", h, T.ToString("F1")); break; }
        }
    }
}
Soul.NavGrid.maxExpand = 1200; sb.AppendFormat("find {0:F0} ms, {1} searches {2} edges {3:F0} ms nav\n", swF.Elapsed.TotalMilliseconds, Soul.NavGrid.searches, Soul.NavGrid.edgesBuilt, Soul.NavGrid.ms);
System.Func<string, UnityEngine.Vector3, UnityEngine.Vector3, float, string> run = (name, P, S, secs) =>
{
    var o = new System.Text.StringBuilder();
    g.Teleport(P); g.ch.root.position = P; g.ch.UpdateWorld(); g.Simulate(.1f);
    g.enemies.ClearAll(); g.Simulate(.1f);
    g.enemies.passive = true;
    var a = g.enemies.SpawnAt("crawler", S); var b = g.enemies.SpawnAt("warden", S + new UnityEngine.Vector3(2, 0, 0));
    o.AppendFormat("[{0}] player {1} | spawn y {2:F2}/{3:F2}\n", name, g.ch.root.position.ToString("F1"), a.pos.y, b.pos.y);
    float minA = 99, minB = 99; Soul.NavGrid.ResetStats();
    for (int i = 1; i <= secs; i++)
    {
        g.Simulate(1f);
        var pp = g.ch.root.position;
        o.AppendFormat("  t{0}: hound {1} d{2:F1} {3}{4} | warden {5} d{6:F1} {7}{8}\n", i,
            a.pos.ToString("F1"), UnityEngine.Vector3.Distance(a.pos, pp), a.nav.pathing ? "path" : "direct", a.nav.waiting ? " WAIT" : "",
            b.pos.ToString("F1"), UnityEngine.Vector3.Distance(b.pos, pp), b.nav.pathing ? "path" : "direct", b.nav.waiting ? " WAIT" : "");
    }
    o.AppendFormat("  nav: {0} searches, {1} expanded, {2} edges, {3:F1} ms total\n", Soul.NavGrid.searches, Soul.NavGrid.expanded, Soul.NavGrid.edgesBuilt, Soul.NavGrid.ms);
    return o.ToString();
};
if (found) sb.Append(run("stairs: player on deck", top, ground, 14));
if (foundLedge) sb.Append(run("ledge: no path", ledge, ledgeGround, 6));
if (foundWall) sb.Append(run("wall: around the building", w2, w1, 12));
// perf: 10 machines chasing a player on the deck (5 hounds + 5 wardens), EnemyManager.Update at 60 Hz
if (found)
{
    g.Teleport(top); g.ch.root.position = top; g.ch.UpdateWorld(); g.enemies.ClearAll(); g.Simulate(3.5f);
    g.enemies.passive = true;
    for (int i = 0; i < 10; i++) { float a = i * .628f; g.enemies.SpawnAt(i % 2 == 0 ? "crawler" : "warden", ground + new UnityEngine.Vector3(UnityEngine.Mathf.Cos(a) * 3, 0, UnityEngine.Mathf.Sin(a) * 3)); }
    g.Simulate(.8f); Soul.NavGrid.Clear(); Soul.NavGrid.ResetStats(); g.player.pos = g.ch.root.position;
    var sw = new System.Diagnostics.Stopwatch(); double mx = 0, tot = 0, nmx = 0;
    for (int f = 0; f < 600; f++)
    {
        double n0 = Soul.NavGrid.ms; sw.Reset(); sw.Start(); g.enemies.Update(1 / 60f, g.player); sw.Stop();
        double e = sw.Elapsed.TotalMilliseconds; tot += e; if (e > mx) mx = e; if (Soul.NavGrid.ms - n0 > nmx) nmx = Soul.NavGrid.ms - n0;
    }
    sb.AppendFormat("perf 10 enemies x 600 frames (cold grid): EnemyManager.Update avg {0:F3} ms max {1:F2} ms | nav avg {2:F3} ms/frame, worst frame {3:F2} ms, {4} searches, {5} expanded, {6} edges\n",
        tot / 600, mx, Soul.NavGrid.ms / 600, nmx, Soul.NavGrid.searches, Soul.NavGrid.expanded, Soul.NavGrid.edgesBuilt);
    int onTop = 0; foreach (var e in g.enemies.list) if (e.alive && e.pos.y > top.y - .3f) onTop++;
    sb.AppendFormat("  after 10 s: {0}/10 on the deck\n", onTop);
    Soul.NavGrid.ResetStats(); tot = 0; mx = 0; nmx = 0;
    for (int f = 0; f < 300; f++)
    {
        double n0 = Soul.NavGrid.ms; sw.Reset(); sw.Start(); g.enemies.Update(1 / 60f, g.player); sw.Stop();
        double e = sw.Elapsed.TotalMilliseconds; tot += e; if (e > mx) mx = e; if (Soul.NavGrid.ms - n0 > nmx) nmx = Soul.NavGrid.ms - n0;
    }
    sb.AppendFormat("  warm grid: Update avg {0:F3} ms max {1:F2} | nav avg {2:F3} ms/frame worst {3:F2}, {4} searches\n", tot / 300, mx, Soul.NavGrid.ms / 300, nmx, Soul.NavGrid.searches);
    g.enemies.ClearAll();
}
return sb.ToString();
