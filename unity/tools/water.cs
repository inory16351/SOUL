var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>();
var list = new System.Collections.Generic.List<System.Tuple<string, UnityEngine.Vector3>>();
int[] zones = { 6, 6, 0, 4, 5 }; int[] kinds = { 2, 3, 1, 4, 2 }; string[] names = { "water_mire", "water_toxic", "water_rain_z0", "water_oil_z4", "water_mud_z5" };
for (int q = 0; q < zones.Length; q++)
{
    var c = Soul.ZoneWorld.Center(zones[q]); bool found = false;
    for (int r = 20; r < 400 && !found; r += 5) for (int k = 0; k < 48 && !found; k++)
    {
        float a = k * 6.2832f / 48, x = c.x + UnityEngine.Mathf.Cos(a) * r, z = c.y + UnityEngine.Mathf.Sin(a) * r;
        if (Soul.ZoneWorld.At(x, z) != zones[q]) continue;
        int ty; float d = Soul.Terrain.Water(x, z, out ty);
        if (ty == kinds[q] && d > .12f) { list.Add(System.Tuple.Create(names[q], new UnityEngine.Vector3(x - 5, 0, z - 5))); found = true; }
    }
}
var cam = UnityEngine.Camera.main; var sb = new System.Text.StringBuilder();
var rt = new UnityEngine.RenderTexture(1600, 900, 24, UnityEngine.RenderTextureFormat.ARGB32);
foreach (var it in list)
{
    g.Teleport(it.Item2); g.Simulate(.1f);
    for (int f = 0; f < 4; f++) g.ZoneTick(.016f);
    cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
    UnityEngine.RenderTexture.active = rt;
    var tex = new UnityEngine.Texture2D(1600, 900, UnityEngine.TextureFormat.RGB24, false);
    tex.ReadPixels(new UnityEngine.Rect(0, 0, 1600, 900), 0, 0); tex.Apply(); UnityEngine.RenderTexture.active = null;
    System.IO.File.WriteAllBytes(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Captures/" + it.Item1 + ".png"), tex.EncodeToPNG());
    UnityEngine.Object.Destroy(tex);
    sb.Append(it.Item1).Append(" @ ").Append(it.Item2.ToString("F0")).Append('\n');
}
rt.Release(); UnityEngine.Object.Destroy(rt);
return sb.ToString();
