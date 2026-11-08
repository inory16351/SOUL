// Hub -> passage reachability over bare terrain (1 m grid, same rule as WorldCollision.Move: TerrainWall uphill,
// ridges solid, drops allowed). Props are ignored (trails are kept clear of them). Set ZONE; one zone per call.
const int ZONE = 0;
var sw = System.Diagnostics.Stopwatch.StartNew();
var hub = Soul.ZoneRoutes.Hub(ZONE);
float x0 = 1e9f, z0 = 1e9f, x1 = -1e9f, z1 = -1e9f;
for (float x = Soul.ZoneWorld.MinX; x < Soul.ZoneWorld.MaxX; x += 8) for (float y = Soul.ZoneWorld.MinZ; y < Soul.ZoneWorld.MaxZ; y += 8)
    if (Soul.ZoneWorld.At(x, y) == ZONE) { x0 = UnityEngine.Mathf.Min(x0, x); z0 = UnityEngine.Mathf.Min(z0, y); x1 = UnityEngine.Mathf.Max(x1, x); z1 = UnityEngine.Mathf.Max(z1, y); }
x0 -= 40; z0 -= 40; x1 += 40; z1 += 40;
int W = (int)(x1 - x0) + 1, H = (int)(z1 - z0) + 1; var seen = new bool[W * H]; var q = new System.Collections.Generic.Queue<int>();
int hi = (int)(hub.x - x0), hj = (int)(hub.y - z0); seen[hi * H + hj] = true; q.Enqueue(hi * H + hj); int n = 0;
int[] di = { 1, -1, 0, 0 }, dj = { 0, 0, 1, -1 };
while (q.Count > 0)
{
    int c = q.Dequeue(); int i = c / H, j = c % H; float x = x0 + i, z = z0 + j; float h = Soul.Terrain.Height(x, z); n++;
    for (int k = 0; k < 4; k++)
    {
        int a = i + di[k], b = j + dj[k]; if (a < 0 || b < 0 || a >= W || b >= H || seen[a * H + b]) continue;
        float nx = x + di[k], nz = z + dj[k];
        if (Soul.ZoneWorld.Solid(nx, nz)) continue;
        int zn = Soul.ZoneWorld.At(nx, nz); if (zn != ZONE && zn >= 0 && Soul.ZoneWorld.NearestSite(new UnityEngine.Vector3(nx, 0, nz), 40) == null) continue;
        if (Soul.WorldCollision.TerrainWall(x, z, di[k], dj[k], h) || Soul.WorldCollision.TerrainWall(x + di[k] * .5f, z + dj[k] * .5f, di[k], dj[k], Soul.Terrain.Height(x + di[k] * .5f, z + dj[k] * .5f))) continue;
        seen[a * H + b] = true; q.Enqueue(a * H + b);
    }
}
var sb = new System.Text.StringBuilder(); sb.AppendFormat("Z{0} hub ({1:F0},{2:F0}) reached {3} cells; ", ZONE, hub.x, hub.y, n);
int miss = 0;
foreach (var s in Soul.ZoneWorld.Sites)
{
    if (s.link.a != ZONE && s.link.b != ZONE) continue;
    bool ok = false;
    for (int r = 0; r <= 12 && !ok; r += 2) for (int t = 0; t < 12 && !ok; t++)
    {
        float ang = t * 6.2832f / 12; int i = (int)(s.pos.x + UnityEngine.Mathf.Cos(ang) * r - x0), j = (int)(s.pos.y + UnityEngine.Mathf.Sin(ang) * r - z0);
        if (i >= 0 && j >= 0 && i < W && j < H && seen[i * H + j]) ok = true;
    }
    if (!ok) miss++;
    sb.AppendFormat("site{0}{1} ", s.index, ok ? "+" : "-MISS");
}
sb.AppendFormat("| miss {0} | {1} ms", miss, sw.ElapsedMilliseconds);
return sb.ToString();
