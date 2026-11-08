// Terrain relief checks (edit or play mode): height range per zone + every trail sampled every 0.5 m for
// terrain walls (WorldCollision.TerrainWall) walking it both ways. Trails must have 0 walls.
var sb = new System.Text.StringBuilder(); var sw = System.Diagnostics.Stopwatch.StartNew();
var mn = new float[8]; var mx = new float[8]; var cnt = new int[8]; var steep = new int[8];
for (int z = 0; z < 8; z++) { mn[z] = 1e9f; mx[z] = -1e9f; }
for (float x = Soul.ZoneWorld.MinX; x < Soul.ZoneWorld.MaxX; x += 8)
    for (float y = Soul.ZoneWorld.MinZ; y < Soul.ZoneWorld.MaxZ; y += 8)
    {
        int zn = Soul.ZoneWorld.At(x, y); if (zn < 0 || zn > 7 || Soul.ZoneWorld.Solid(x, y)) continue;
        float h = Soul.Terrain.Height(x, y); mn[zn] = UnityEngine.Mathf.Min(mn[zn], h); mx[zn] = UnityEngine.Mathf.Max(mx[zn], h); cnt[zn]++;
        float gr = UnityEngine.Mathf.Max(UnityEngine.Mathf.Abs(Soul.Terrain.Height(x + .5f, y) - h), UnityEngine.Mathf.Abs(Soul.Terrain.Height(x, y + .5f) - h)) / .5f;
        if (gr > Soul.WorldCollision.SLOPE) steep[zn]++;
    }
for (int z = 0; z < 8; z++) sb.AppendFormat("Z{0}: {1:F1} .. {2:F1} m  steep(>45°) {3:P1}\n", z, mn[z], mx[z], cnt[z] > 0 ? steep[z] / (float)cnt[z] : 0);
int walls = 0; float maxG = 0; var where = new System.Text.StringBuilder();
foreach (var s in Soul.ZoneRoutes.Segs)
{
    var d = s.b - s.a; float L = d.magnitude; if (L < .01f) continue; var u = d / L;
    for (float t = 0; t <= L; t += .5f)
    {
        var p = s.a + u * t; float h = Soul.Terrain.Height(p.x, p.y);
        float g2 = UnityEngine.Mathf.Abs(Soul.Terrain.Height(p.x + u.x * .5f, p.y + u.y * .5f) - h) / .5f; if (g2 > maxG) maxG = g2;
        if (Soul.WorldCollision.TerrainWall(p.x, p.y, u.x, u.y, h) || Soul.WorldCollision.TerrainWall(p.x, p.y, -u.x, -u.y, h))
        { walls++; if (walls < 8) where.AppendFormat(" ({0:F0},{1:F0} Z{2})", p.x, p.y, s.zone); }
    }
}
sb.AppendFormat("trails: {0} wall samples, max grade {1:F2}{2}\n", walls, maxG, where);
sb.AppendFormat("{0} ms\n", sw.ElapsedMilliseconds);
return sb.ToString();
