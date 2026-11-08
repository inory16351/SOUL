// chunk build cost (play mode): fresh terrain cache, teleport into unvisited areas, time the 49-chunk load
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>(); var sb = new System.Text.StringBuilder();
foreach (var p in new[] { new UnityEngine.Vector3(-600, 0, -800), new UnityEngine.Vector3(250, 0, -500), new UnityEngine.Vector3(1300, 0, 120), new UnityEngine.Vector3(-650, 0, 420) })
{
    g.Teleport(p + new UnityEngine.Vector3(0, 0, 900)); Soul.Terrain.Clear();
    var sw = System.Diagnostics.Stopwatch.StartNew(); g.Teleport(p); sw.Stop();
    sb.AppendFormat("({0:F0},{1:F0}) Z{2}: teleport {3} ms = {4:F1} ms/chunk\n", p.x, p.z, Soul.ZoneWorld.At(p.x, p.z), sw.ElapsedMilliseconds, sw.ElapsedMilliseconds / 49f);
}
return sb.ToString();
