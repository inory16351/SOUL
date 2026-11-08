var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>();
g.VistaTick(0); var v = Soul.Vistas.All[8];
g.Teleport(v.pos + new UnityEngine.Vector3(0, 0, -6)); g.Simulate(.1f); g.VistaStart(8);
for (int f = 0; f < 50; f++) g.VistaTick(.05f);
var cam = UnityEngine.Camera.main; var sb = new System.Text.StringBuilder();
sb.Append("far ").Append(cam.farClipPlane).Append(" lookYaw ").Append(v.lookYaw.ToString("F2")).Append(" landmarks ").Append(Soul.Regions.Landmarks.Count).Append('\n');
int on = 0;
foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshRenderer>(UnityEngine.FindObjectsSortMode.None))
{
    if (!r.name.StartsWith("silhouette")) continue;
    if (r.enabled) on++;
    var sp = cam.WorldToViewportPoint(r.bounds.center);
    sb.Append(r.name).Append(" en=").Append(r.enabled).Append(" vp=").Append(sp.ToString("F2")).Append(" size=").Append(r.bounds.size.ToString("F0")).Append('\n');
}
sb.Append("enabled ").Append(on);
return sb.ToString();
