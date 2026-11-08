// cost of the WorldCollision / Terrain queries NavGrid uses, at a ruin plot (play mode)
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>(); var sb = new System.Text.StringBuilder();
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
var wc = (System.Collections.IDictionary)typeof(Soul.WorldCollision).GetField("chunks", F).GetValue(null);
float x = -76, z = 116; var k = Soul.WorldCollision.KeyOf(x, z); int ns = 0, nc = 0;
for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++)
{
    var ch = wc[new UnityEngine.Vector2Int(k.x + i, k.y + j)]; if (ch == null) continue;
    ns += ((System.Collections.IList)ch.GetType().GetField("surfs").GetValue(ch)).Count; nc += ((System.Collections.IList)ch.GetType().GetField("cols").GetValue(ch)).Count;
}
sb.AppendFormat("3x3 around ({0},{1}): {2} surfs, {3} cols, loaded chunks {4}\n", x, z, ns, nc, wc.Count);
var sw = new System.Diagnostics.Stopwatch(); int N = 2000; float acc = 0;
sw.Start(); for (int i = 0; i < N; i++) acc += Soul.Terrain.Height(x + i * .01f, z); sw.Stop(); sb.AppendFormat("Terrain.Height {0:F2} us\n", sw.Elapsed.TotalMilliseconds * 1000 / N);
sw.Reset(); sw.Start(); for (int i = 0; i < N; i++) acc += Soul.WorldCollision.Height(x + i * .01f, z); sw.Stop(); sb.AppendFormat("WC.Height {0:F2} us\n", sw.Elapsed.TotalMilliseconds * 1000 / N);
sw.Reset(); sw.Start(); for (int i = 0; i < N; i++) acc += Soul.WorldCollision.Floor(x + i * .01f, z, -2); sw.Stop(); sb.AppendFormat("WC.Floor {0:F2} us\n", sw.Elapsed.TotalMilliseconds * 1000 / N);
sw.Reset(); sw.Start(); for (int i = 0; i < N; i++) { var p = new UnityEngine.Vector3(x + i * .01f, -2, z); Soul.WorldCollision.Resolve(ref p, .4f); acc += p.x; } sw.Stop(); sb.AppendFormat("WC.Resolve {0:F2} us\n", sw.Elapsed.TotalMilliseconds * 1000 / N);
sw.Reset(); sw.Start(); for (int i = 0; i < N; i++) acc += Soul.WorldCollision.TerrainWall(x + i * .01f, z, 1, 0, -2) ? 1 : 0; sw.Stop(); sb.AppendFormat("WC.TerrainWall {0:F2} us\n", sw.Elapsed.TotalMilliseconds * 1000 / N);
sw.Reset(); sw.Start(); for (int i = 0; i < N; i++) acc += Soul.WorldCollision.Ledge(x + i * .01f, z, -2, .45f) ? 1 : 0; sw.Stop(); sb.AppendFormat("WC.Ledge {0:F2} us\n", sw.Elapsed.TotalMilliseconds * 1000 / N);
sw.Reset(); sw.Start(); for (int i = 0; i < N; i++) acc += Soul.Parts.Has(3) ? 1 : 0; sw.Stop(); sb.AppendFormat("Parts.Has {0:F3} us\n", sw.Elapsed.TotalMilliseconds * 1000 / N);
var pf = typeof(Soul.NavGrid).GetMethod("Profile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
var wf = typeof(Soul.NavGrid).GetMethod("Wall", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
Soul.NavGrid.Clear();
sw.Reset(); sw.Start(); for (int i = 0; i < 400; i++) pf.Invoke(null, new object[] { -90 + i % 20, 106 + i / 20 }); sw.Stop(); sb.AppendFormat("NavGrid.Profile (new cells) {0:F2} us\n", sw.Elapsed.TotalMilliseconds * 1000 / 400);
sw.Reset(); sw.Start(); for (int i = 0; i < 400; i++) wf.Invoke(null, new object[] { -90f + i % 20, 3f, 106f + i / 20, .3f }); sw.Stop(); sb.AppendFormat("NavGrid.Wall {0:F2} us\n", sw.Elapsed.TotalMilliseconds * 1000 / 400);
var pers = typeof(Soul.WorldCollision).GetField("persistent", F).GetValue(null); sb.AppendFormat("persistent cols {0}\n", ((System.Collections.IList)pers.GetType().GetField("cols").GetValue(pers)).Count);
return sb.ToString();
