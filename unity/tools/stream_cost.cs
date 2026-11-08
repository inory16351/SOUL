// Streaming cost while moving (play mode): teleport, then walk the stream centre 300 m at 3.45 m/s in 60 Hz frames,
// timing WorldStreamer.Update per frame (chunk builds included). Terrain cache cleared first (cold, like new ground).
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>(); var sb = new System.Text.StringBuilder();
var routes = new[] { new[] { -600f, -800f, 1f, 0f }, new[] { 250f, -560f, 0f, 1f }, new[] { 1250f, 60f, -1f, 0f }, new[] { -700f, 300f, 0f, 1f } };
var all = new System.Collections.Generic.List<double>();
foreach (var r in routes)
{
    var p = new UnityEngine.Vector3(r[0], 0, r[1]); var d = new UnityEngine.Vector3(r[2], 0, r[3]);
    g.Teleport(p); Soul.Terrain.Clear(); g.Teleport(p);
    var sw = new System.Diagnostics.Stopwatch(); var ms = new System.Collections.Generic.List<double>();
    for (int f = 0; f < 300 / (3.45f / 60); f++)
    {
        p += d * (3.45f / 60);
        Soul.WorldStreamer.MaxStep = 0; int pend = g.open.PendingBuilds;
        sw.Restart(); g.open.Update(p); sw.Stop(); ms.Add(sw.Elapsed.TotalMilliseconds);
        if (sw.Elapsed.TotalMilliseconds > 9) sb.AppendFormat("  spike {0:F1} ms: slowest step {1:F1} ms stage {2}, pending {3}->{4}" + System.Environment.NewLine, sw.Elapsed.TotalMilliseconds, Soul.WorldStreamer.MaxStep, Soul.WorldStreamer.MaxStepStage, pend, g.open.PendingBuilds);
    }
    ms.Sort(); double sum = 0; foreach (var v in ms) sum += v; all.AddRange(ms);
    int busy = 0; foreach (var v in ms) if (v > 1) busy++;
    sb.AppendFormat("({0:F0},{1:F0}) frames {2}: mean {3:F2} ms, p95 {4:F2}, p99 {5:F2}, max {6:F2}, frames >1 ms {7}\n", r[0], r[1], ms.Count, sum / ms.Count, ms[(int)(ms.Count * .95f)], ms[(int)(ms.Count * .99f)], ms[ms.Count - 1], busy);
}
all.Sort(); var busyMs = all.FindAll(v => v > 1); double bs = 0; foreach (var v in busyMs) bs += v;
sb.AppendFormat("ALL: p99 {0:F2} ms, max {1:F2} ms, build frames mean {2:F2} ms ({3} frames)\n", all[(int)(all.Count * .99f)], all[all.Count - 1], busyMs.Count > 0 ? bs / busyMs.Count : 0, busyMs.Count);
return sb.ToString();
