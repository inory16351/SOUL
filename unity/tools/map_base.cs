// Exports the exact top-down world layout for the illustrated map (MapPainter / ZoneWorld.MapUV mapping).
// Image row 0 (top) = MinZ, column 0 = MinX  (same as the M map on screen).
// Captures/map_mask.png : R = zone+1 (0 = mountains), G = water type * 50, B = 1 trail | 2 massif | 4 zone border | 8 corridor
// Captures/map_markers.txt : kind x y (pixels) for sites, landmarks, pods, arenas, shrines
int W = 2048, R0 = 0, R1 = -1;
float wx = Soul.ZoneWorld.MaxX - Soul.ZoneWorld.MinX, wz = Soul.ZoneWorld.MaxZ - Soul.ZoneWorld.MinZ;
int H = UnityEngine.Mathf.RoundToInt(W * wz / wx); if (R1 < 0) R1 = H;
var sw = System.Diagnostics.Stopwatch.StartNew();
var dir = System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Captures/");
var buf = new byte[W * (R1 - R0) * 3];
for (int j = R0; j < R1; j++)
{
    float z = Soul.ZoneWorld.MinZ + (j + .5f) / H * wz;
    for (int i = 0; i < W; i++)
    {
        float x = Soul.ZoneWorld.MinX + (i + .5f) / W * wx;
        int other; float d; int zone = Soul.ZoneWorld.Sample(x, z, out other, out d);
        int k = ((j - R0) * W + i) * 3; byte flags = 0, wt = 0;
        if (zone >= 0)
        {
            if (Soul.ZoneWorld.RidgeHeight(x, z) > Soul.ZoneWorld.SOLID) flags |= 2;
            if (other >= 0 && d < 2.5f) flags |= 4;
            if (Soul.ZoneRoutes.Dist(x, z) < 3.2f) flags |= 1;
            byte t; if (Soul.Terrain.WaterRaw(x, z, out t) > .1f) wt = t;
        }
        buf[k] = (byte)(zone + 1); buf[k + 1] = (byte)(wt * 50); buf[k + 2] = flags;
    }
}
System.IO.File.WriteAllBytes(dir + "map_mask_" + R0 + ".raw", buf);
var sb = new System.Text.StringBuilder();
sb.Append("size ").Append(W).Append(' ').Append(H).Append(' ').Append(Soul.ZoneWorld.MinX).Append(' ').Append(Soul.ZoneWorld.MaxX).Append(' ').Append(Soul.ZoneWorld.MinZ).Append(' ').Append(Soul.ZoneWorld.MaxZ).Append('\n');
System.Action<string, float, float> M = (kind, x, z) => { var uv = Soul.ZoneWorld.MapUV(new UnityEngine.Vector3(x, 0, z)); sb.Append(kind).Append(' ').Append((uv.x * W).ToString("F1")).Append(' ').Append((uv.y * H).ToString("F1")).Append('\n'); };
if (R0 == 0)
{
    foreach (var s in Soul.ZoneWorld.Sites) M(s.Gate ? "gate" : "site", s.pos.x, s.pos.y);
    foreach (var l in Soul.Regions.Landmarks) M("landmark", l.pos.x, l.pos.z);
    foreach (var p in Soul.ZoneAreas.Pods) M("pod", p.pos.x, p.pos.z);
    foreach (var a in Soul.ZoneAreas.Arenas) M("arena", a.center.x, a.center.z);
    foreach (var s in Soul.ZoneAreas.Shrines) M("shrine", s.pos.x, s.pos.z);
    for (int zz = 0; zz < Soul.ZoneGraph.Zones.Length; zz++) { var c = Soul.ZoneWorld.Center(zz); M("zone" + zz, c.x, c.y); }
    System.IO.File.WriteAllText(dir + "map_markers.txt", sb.ToString());
}
return "rows " + R0 + "-" + R1 + " of " + H + " in " + sw.ElapsedMilliseconds + " ms";
