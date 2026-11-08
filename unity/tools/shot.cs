var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>();
var list = new System.Collections.Generic.List<System.Tuple<string, UnityEngine.Vector3>>();
foreach (var i in new[] { 5, 6, 12 }) { var s = Soul.ZoneWorld.Sites[i]; list.Add(System.Tuple.Create("codex_gate_" + i, new UnityEngine.Vector3(s.pos.x - s.n.x * 9, 0, s.pos.y - s.n.y * 9))); }
list.Add(System.Tuple.Create("codex_shrine_z0", new UnityEngine.Vector3(-110, 0, -88)));
list.Add(System.Tuple.Create("codex_ground_z0", new UnityEngine.Vector3(-300, 0, 40)));
// reeds: first shallow water found near the Z6 centre
int wty; var c6 = Soul.ZoneWorld.Center(6); bool found = false;
for (int r = 0; r < 200 && !found; r += 6) for (int k = 0; k < 24 && !found; k++)
{
    float a = k * 6.2832f / 24, x = c6.x + UnityEngine.Mathf.Cos(a) * r, z = c6.y + UnityEngine.Mathf.Sin(a) * r;
    float d = Soul.Terrain.Water(x, z, out wty);
    if (d > .05f && d < .3f && !Soul.Terrain.Toxic(x, z)) { list.Add(System.Tuple.Create("codex_marsh_z6", new UnityEngine.Vector3(x, 0, z + 4))); found = true; }
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
