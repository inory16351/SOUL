// Terrain relief review: one high oblique shot per zone (same spots before/after) + measured height range.
// usage (play mode): python umcp.py --code relief_shot.cs   — edit TAG below ("before" / "" for after)
const string TAG = "";
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>();
var sb = new System.Text.StringBuilder();
// height range per zone (Terrain.Height on an 8 m grid; includes flattening and water basins)
var mn = new float[8]; var mx = new float[8]; var cnt = new int[8]; var steep = new int[8];
for (int z = 0; z < 8; z++) { mn[z] = 1e9f; mx[z] = -1e9f; }
for (float x = Soul.ZoneWorld.MinX; x < Soul.ZoneWorld.MaxX; x += 8)
    for (float y = Soul.ZoneWorld.MinZ; y < Soul.ZoneWorld.MaxZ; y += 8)
    {
        int zn = Soul.ZoneWorld.At(x, y); if (zn < 0 || zn > 7 || Soul.ZoneWorld.Solid(x, y)) continue;
        float h = Soul.Terrain.Height(x, y); mn[zn] = UnityEngine.Mathf.Min(mn[zn], h); mx[zn] = UnityEngine.Mathf.Max(mx[zn], h); cnt[zn]++;
        float gr = UnityEngine.Mathf.Max(UnityEngine.Mathf.Abs(Soul.Terrain.Height(x + .5f, y) - h), UnityEngine.Mathf.Abs(Soul.Terrain.Height(x, y + .5f) - h)) / .5f;
        if (gr > 1) steep[zn]++;
    }
for (int z = 0; z < 8; z++) sb.AppendFormat("Z{0}: {1:F1} .. {2:F1} m  steep {3:P1}\n", z, mn[z], mx[z], cnt[z] > 0 ? steep[z] / (float)cnt[z] : 0);
Soul.Terrain.Clear();
// capture spots: a fixed offset from each zone's inner centre (away from the shrine / pod flats)
var cam = UnityEngine.Camera.main; var rt = new UnityEngine.RenderTexture(1600, 900, 24, UnityEngine.RenderTextureFormat.ARGB32);
float fog = UnityEngine.RenderSettings.fogDensity;
// overview spots (z0..z7, same before / after) + close cliff spots found by cliff_walk.cs (after only, dist 45 m)
var spots = new System.Collections.Generic.List<System.Tuple<string, UnityEngine.Vector3, float>>();
for (int z = 0; z < 8; z++) { var c = Soul.ZoneWorld.Center(z); spots.Add(System.Tuple.Create("z" + z, z == 0 ? new UnityEngine.Vector3(-300, 0, 40) : new UnityEngine.Vector3(c.x + 60, 0, c.y + 40), 120f)); }
if (TAG == "")
{
    float[] cl = { 0, -53, -93, 1, -845, -663, 2, -757, 342, 3, 169, -543, 4, 1423, -936, 5, 1430, 16, 7, 84, 458 };
    for (int i = 0; i < cl.Length; i += 3) spots.Add(System.Tuple.Create("z" + (int)cl[i] + "_cliff", new UnityEngine.Vector3(cl[i + 1], 0, cl[i + 2]), 45f));
}
foreach (var sp in spots)
{
    var p = sp.Item2; string z = sp.Item1;
    g.Teleport(p); g.Simulate(.1f);
    for (int f = 0; f < 4; f++) g.ZoneTick(.016f);
    for (int f = 0; f < 60; f++) g.atmo.Update(.1f, p);   // let the zone atmosphere settle (fog / sun of this zone)
    var go = new UnityEngine.GameObject("relief cam"); var cc = go.AddComponent<UnityEngine.Camera>(); cc.CopyFrom(cam);
    var tu = Soul.Sim.ToUnity(p + new UnityEngine.Vector3(0, Soul.Terrain.Height(p.x, p.z), 0));
    var fwd = cam.transform.forward;
    cc.transform.position = tu - fwd * sp.Item3; cc.transform.rotation = UnityEngine.Quaternion.LookRotation(fwd); cc.farClipPlane = 600;
    UnityEngine.RenderSettings.fogDensity = fog * .15f;
    cc.targetTexture = rt; cc.Render(); cc.targetTexture = null;
    UnityEngine.RenderSettings.fogDensity = fog;
    UnityEngine.RenderTexture.active = rt;
    var tex = new UnityEngine.Texture2D(1600, 900, UnityEngine.TextureFormat.RGB24, false);
    tex.ReadPixels(new UnityEngine.Rect(0, 0, 1600, 900), 0, 0); tex.Apply(); UnityEngine.RenderTexture.active = null;
    System.IO.File.WriteAllBytes(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Captures/terrain_relief_" + z + TAG + ".png"), tex.EncodeToPNG());
    UnityEngine.Object.Destroy(tex); UnityEngine.Object.DestroyImmediate(go);
    sb.AppendFormat("shot {0} @ {1}\n", z, p.ToString("F0"));
}
rt.Release(); UnityEngine.Object.Destroy(rt);
return sb.ToString();
