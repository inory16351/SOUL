// Walk into real terrain cliffs (play mode): find steep spots near each zone's centre where the ground differs
// by >= 1.8 m across 6 m, walk low -> high (must stop below the top) and high -> low (must drop through),
// then a hound below chasing the player on top must stay below.
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>(); var sb = new System.Text.StringBuilder();
foreach (var zone in new[] { 0, 1, 2, 3, 4, 5, 7 })
{
    var c = Soul.ZoneWorld.Center(zone); bool done = false;
    for (int r = 30; r < 420 && !done; r += 7) for (int k = 0; k < 36 && !done; k++)
    {
        float a = k * 6.2832f / 36, x = c.x + UnityEngine.Mathf.Cos(a) * r, z = c.y + UnityEngine.Mathf.Sin(a) * r;
        if (Soul.ZoneWorld.At(x, z) != zone || Soul.ZoneWorld.Solid(x, z) || Soul.ZoneRoutes.Dist(x, z) < 14) continue;
        float hx = Soul.Terrain.Height(x + .5f, z) - Soul.Terrain.Height(x - .5f, z), hz = Soul.Terrain.Height(x, z + .5f) - Soul.Terrain.Height(x, z - .5f);
        var n = new UnityEngine.Vector2(hx, hz); if (n.magnitude < 1.3f) continue; n.Normalize();
        float lo = Soul.Terrain.Height(x - n.x * 3, z - n.y * 3), hi = Soul.Terrain.Height(x + n.x * 3, z + n.y * 3);
        int wt; if (hi - lo < 1.8f || Soul.Terrain.Water(x - n.x * 6, z - n.y * 6, out wt) > 0) continue;
        var p = new UnityEngine.Vector3(x, 0, z); var n3 = new UnityEngine.Vector3(n.x, 0, n.y);
        g.Teleport(p - n3 * 6); g.WalkTest(p + n3 * 6, 5);
        var e1 = g.ch.root.position; float up = UnityEngine.Vector3.Dot(e1 - p, n3);
        g.Teleport(p + n3 * 6); g.WalkTest(p - n3 * 6, 5);
        var e2 = g.ch.root.position; float down = UnityEngine.Vector3.Dot(e2 - p, n3);
        sb.AppendFormat("Z{0} cliff ({1:F0},{2:F0}) {3:F1}m->{4:F1}m: up stops at {5:F1} (y {6:F2}) {7}; down reaches {8:F1} (y {9:F2}) {10}",
            zone, x, z, lo, hi, up, e1.y, e1.y < hi - 1 ? "BLOCKED ok" : "CLIMBED bad", down, e2.y, down < 0 && e2.y < hi - 1 ? "DROPPED ok" : "STUCK bad");
        if (p.magnitude < g.enemies.bounds - 12)   // enemies are clamped to the manager bounds
        {
            g.Teleport(p + n3 * 5); g.enemies.passive = false;
            var en = g.enemies.SpawnAt("crawler", p - n3 * 6); g.Simulate(4f);
            sb.AppendFormat(" | hound chasing up: y {0:F2} {1}", en.pos.y, en.pos.y < hi - 1 ? "BLOCKED ok" : "CLIMBED bad");
            en.Despawn();
        }
        sb.AppendLine();
        done = true;
    }
}
return sb.ToString();
