// Find nav test spots in the loaded chunks around a ruin plot (play mode, < 15 s): a deck reached by stairs,
// a ledge with no stairs, and two ground points with a building between them. Prints coordinates for nav_run.cs.
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>(); var sb = new System.Text.StringBuilder();
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
var wc = (System.Collections.IDictionary)typeof(Soul.WorldCollision).GetField("chunks", F).GetValue(null);
var c0 = Soul.ZoneWorld.Center(0); var plots = new System.Collections.Generic.List<UnityEngine.Vector2>();
foreach (var p in Soul.WorldStreamer.PlotCentres(c0.x - 120, c0.y - 120, 240)) plots.Add(p);
plots.Sort((u, v) => (u - c0).sqrMagnitude.CompareTo((v - c0).sqrMagnitude));
var sw = System.Diagnostics.Stopwatch.StartNew(); var path = new System.Collections.Generic.List<UnityEngine.Vector3>();
bool deck = false, ledge = false, wall = false; Soul.NavGrid.ResetStats();
for (int pi = 0; pi < plots.Count && pi < 8 && !(deck && ledge && wall) && sw.ElapsedMilliseconds < 12000; pi++)
{
    var pc = plots[pi]; g.Teleport(new UnityEngine.Vector3(pc.x, 0, pc.y)); g.Simulate(.05f);
    var surfs = new System.Collections.Generic.List<Soul.WorldCollision.Surf>();
    foreach (System.Collections.DictionaryEntry de in wc)
    {
        var list = (System.Collections.IList)de.Value.GetType().GetField("surfs").GetValue(de.Value);
        foreach (var o in list) { var s = (Soul.WorldCollision.Surf)o; if ((s.c - pc).magnitude < 40 && s.kind == Soul.PropSurface.BOX && s.gate == 0 && s.half.x > 1.2f && s.half.y > 1.2f) surfs.Add(s); }
    }
    sb.AppendFormat("plot {0} ({1:F0},{2:F0}): {3} deck surfaces\n", pi, pc.x, pc.y, surfs.Count);
    foreach (var s in surfs)
    {
        if (sw.ElapsedMilliseconds > 12000) break;
        float h = s.top - Soul.Terrain.Height(s.c.x, s.c.y); var T = new UnityEngine.Vector3(s.c.x, s.top, s.c.y);
        if (Soul.WorldCollision.Height(T.x, T.z) > s.top + .05f) continue;
        bool wantDeck = !deck && h > 2.2f, wantLedge = !ledge && h > 1.0f;
        if (!wantDeck && !wantLedge) continue;
        for (int k = 0; k < 4; k++)
        {
            float a = k * 1.5708f + .4f; var G = new UnityEngine.Vector3(s.c.x + UnityEngine.Mathf.Cos(a) * 14, 0, s.c.y + UnityEngine.Mathf.Sin(a) * 14);
            float tg = Soul.Terrain.Height(G.x, G.z); G.y = Soul.WorldCollision.Floor(G.x, G.z, tg); if (G.y > tg + .05f) continue;
            bool reach = Soul.NavGrid.FindPath(G, T, .6f, path);
            if (wantDeck && reach && !Soul.NavGrid.Straight(G, T, .6f, 40)) { deck = true; sb.AppendFormat("DECK top=({0:F2},{1:F2},{2:F2}) ground=({3:F2},{4:F2},{5:F2}) h {6:F1} path {7}\n", T.x, T.y, T.z, G.x, G.y, G.z, h, path.Count); break; }
            if (wantLedge && !reach) { ledge = true; sb.AppendFormat("LEDGE top=({0:F2},{1:F2},{2:F2}) ground=({3:F2},{4:F2},{5:F2}) h {6:F1}\n", T.x, T.y, T.z, G.x, G.y, G.z, h); break; }
        }
    }
    for (int kw = 0; kw < 4 && !wall; kw++)
    {
        float aw = kw * .785f; var d = new UnityEngine.Vector3(UnityEngine.Mathf.Cos(aw), 0, UnityEngine.Mathf.Sin(aw)) * 13;
        var A = new UnityEngine.Vector3(pc.x, 0, pc.y) + d; var B = new UnityEngine.Vector3(pc.x, 0, pc.y) - d;
        float ta = Soul.Terrain.Height(A.x, A.z), tb = Soul.Terrain.Height(B.x, B.z);
        A.y = Soul.WorldCollision.Floor(A.x, A.z, ta); B.y = Soul.WorldCollision.Floor(B.x, B.z, tb);
        if (A.y > ta + .05f || B.y > tb + .05f) continue;
        if (!Soul.NavGrid.Straight(A, B, .6f, 40) && Soul.NavGrid.FindPath(A, B, .6f, path)) { wall = true; sb.AppendFormat("WALL a=({0:F2},{1:F2},{2:F2}) b=({3:F2},{4:F2},{5:F2}) path {6}\n", A.x, A.y, A.z, B.x, B.y, B.z, path.Count); }
    }
}
sb.AppendFormat("{0:F0} ms, nav {1} searches {2} expanded {3} edges {4:F0} ms ({5:F1} us/edge)\n", sw.Elapsed.TotalMilliseconds, Soul.NavGrid.searches, Soul.NavGrid.expanded, Soul.NavGrid.edgesBuilt, Soul.NavGrid.ms, Soul.NavGrid.ms * 1000 / UnityEngine.Mathf.Max(1, Soul.NavGrid.edgesBuilt));
return sb.ToString();
