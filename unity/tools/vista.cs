var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>();
var sb = new System.Text.StringBuilder();
var t0 = System.DateTime.Now;
g.VistaTick(0);   // builds the vista list
sb.Append("vistas ").Append(Soul.Vistas.All.Count).Append(" build ").Append((System.DateTime.Now - t0).TotalSeconds.ToString("F2")).Append("s\n");
foreach (var v in Soul.Vistas.All) sb.Append(" z").Append(v.zone).Append(v.shrine ? " shrine " : " high ").Append(v.pos.ToString("F0")).Append(" relief ").Append(Soul.Terrain.Relief(v.pos.x, v.pos.z).ToString("F1")).Append('\n');
var cam = UnityEngine.Camera.main;
var rt = new UnityEngine.RenderTexture(1600, 900, 24, UnityEngine.RenderTextureFormat.ARGB32);
System.Action<string> shot = name =>
{
    cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; UnityEngine.RenderTexture.active = rt;
    var tex = new UnityEngine.Texture2D(1600, 900, UnityEngine.TextureFormat.RGB24, false);
    tex.ReadPixels(new UnityEngine.Rect(0, 0, 1600, 900), 0, 0); tex.Apply(); UnityEngine.RenderTexture.active = null;
    System.IO.File.WriteAllBytes(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Captures/" + name + ".png"), tex.EncodeToPNG());
    UnityEngine.Object.Destroy(tex);
};
// normal view with the thinner fog (Z0 streets)
g.Teleport(new UnityEngine.Vector3(-300, 0, 40)); g.Simulate(.1f); for (int f = 0; f < 30; f++) g.ZoneTick(.05f);
for (int f = 0; f < 60; f++) g.VistaTick(.05f);
shot("fog_after_z0");
int[] pick = { -1, -1 };
for (int i = 0; i < Soul.Vistas.All.Count; i++) { var v = Soul.Vistas.All[i]; if (v.zone == 0 && !v.shrine && pick[0] < 0) pick[0] = i; if (v.zone == 0 && v.shrine && pick[1] < 0) pick[1] = i; }
foreach (var idx in pick)
{
    if (idx < 0) continue; var v = Soul.Vistas.All[idx];
    g.Teleport(v.pos + new UnityEngine.Vector3(0, 0, -6)); g.Simulate(.1f);
    g.VistaStart(idx);
    for (int f = 0; f < 50; f++) g.VistaTick(.05f);
    // the camera / fog are applied by FrameTail normally; push them here
    shot("vista_" + (v.shrine ? "shrine" : "high") + "_z0");
    sb.Append("captured ").Append(idx).Append('\n');
}
rt.Release(); UnityEngine.Object.Destroy(rt);
return sb.ToString();
