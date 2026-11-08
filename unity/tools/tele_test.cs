// Telegraph height check + slope capture (play mode). Writes Captures/telegraph_slope.png (low view up a slope).
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>(); var sb = new System.Text.StringBuilder();
var circ = new Soul.EnemyAttack { name = "slam", kind = "circle", radius = 2.5f };
var cone = new Soul.EnemyAttack { name = "sweep", kind = "cone", radius = 3.8f, angle = 2.5f };
var line = new Soul.EnemyAttack { name = "thrust", kind = "line", width = 1.4f, length = 6.5f };
// 1) height band
var o = new UnityEngine.Vector3(0, 0, 0);
var t0 = new Soul.Telegraph(g.enemies.parent, "circle", o, 0, circ, 1);
float gy = t0.origin.y;
sb.AppendFormat("origin y {0:F2}: same floor {1}, +1.0 {2}, +1.6 {3}, +3.2 (roof) {4}, -2 {5}\n", gy,
    t0.Contains(new UnityEngine.Vector3(1, gy, 0)), t0.Contains(new UnityEngine.Vector3(1, gy + 1, 0)), t0.Contains(new UnityEngine.Vector3(1, gy + 1.6f, 0)),
    t0.Contains(new UnityEngine.Vector3(1, gy + 3.2f, 0)), t0.Contains(new UnityEngine.Vector3(1, gy - 2, 0)));
t0.Dispose();
// 2) find a bare terrain slope (grade 0.35 .. 0.9) near the Z0 centre
var c = Soul.ZoneWorld.Center(0); UnityEngine.Vector3 P = UnityEngine.Vector3.zero; bool found = false; float grade = 0;
for (int r = 60; r < 400 && !found; r += 5) for (int k = 0; k < 40 && !found; k++)
{
    float a = k * 6.2832f / 40, x = c.x + UnityEngine.Mathf.Cos(a) * r, z = c.y + UnityEngine.Mathf.Sin(a) * r;
    if (Soul.ZoneWorld.Solid(x, z)) continue;
    float gx = (Soul.Terrain.Height(x + 1, z) - Soul.Terrain.Height(x - 1, z)) / 2, gz = (Soul.Terrain.Height(x, z + 1) - Soul.Terrain.Height(x, z - 1)) / 2;
    float gr = UnityEngine.Mathf.Sqrt(gx * gx + gz * gz); int wt;
    if (gr < .35f || gr > .9f || Soul.Terrain.Water(x, z, out wt) > 0) continue;
    if (Soul.WorldCollision.Height(x, z) > Soul.Terrain.Height(x, z) + .01f) continue;
    P = new UnityEngine.Vector3(x, Soul.Terrain.Height(x, z), z); grade = gr; found = true;
}
if (!found) return sb.Append("no slope found").ToString();
g.Teleport(P + new UnityEngine.Vector3(0, 0, -6)); g.enemies.ClearAll(); g.Simulate(.2f);
P.y = Soul.WorldCollision.Floor(P.x, P.z, P.y);
var tA = new Soul.Telegraph(g.enemies.parent, "circle", P, 0, circ, 1);
var tB = new Soul.Telegraph(g.enemies.parent, "cone", P + new UnityEngine.Vector3(-5, 0, 2), 1.2f, cone, 1);
var tC = new Soul.Telegraph(g.enemies.parent, "line", P + new UnityEngine.Vector3(4, 0, -3), -.6f, line, 1);
foreach (var tt in new[] { tA, tB, tC }) tt.Update(.6f);
sb.AppendFormat("slope at {0} grade {1:F2}; terrain rise over circle: {2:F2} m\n", P.ToString("F1"), grade,
    Soul.Terrain.Height(P.x + 2.5f, P.z) - Soul.Terrain.Height(P.x - 2.5f, P.z));
var cam = UnityEngine.Camera.main;
var rt = new UnityEngine.RenderTexture(1600, 900, 24, UnityEngine.RenderTextureFormat.ARGB32);
System.Action<UnityEngine.Camera, string> shot = (cm, name) =>
{
    cm.targetTexture = rt; cm.Render(); cm.targetTexture = null; UnityEngine.RenderTexture.active = rt;
    var tex = new UnityEngine.Texture2D(1600, 900, UnityEngine.TextureFormat.RGB24, false);
    tex.ReadPixels(new UnityEngine.Rect(0, 0, 1600, 900), 0, 0); tex.Apply(); UnityEngine.RenderTexture.active = null;
    System.IO.File.WriteAllBytes(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Captures/" + name + ".png"), tex.EncodeToPNG());
    UnityEngine.Object.Destroy(tex);
};
var sc = UnityEngine.Object.Instantiate(cam); var side = sc; sc.fieldOfView = 50;
var mid = P + new UnityEngine.Vector3(-.5f, 0, -.3f);   // between the three shapes
// low view from the downhill side so the slope shows in profile (a high camera here only sees a brown overlay)
float gxx = Soul.Terrain.Height(P.x + 1, P.z) - Soul.Terrain.Height(P.x - 1, P.z), gzz = Soul.Terrain.Height(P.x, P.z + 1) - Soul.Terrain.Height(P.x, P.z - 1);
var up = new UnityEngine.Vector3(gxx, 0, gzz).normalized; var across = new UnityEngine.Vector3(-gzz, 0, gxx).normalized;

// from the downhill side, a little off the fall line, looking up the slope
var eye = mid - up * 10 + across * 3; eye.y = Soul.Terrain.Height(eye.x, eye.z) + 2.2f;
sc.transform.position = Soul.Sim.ToUnity(eye); sc.transform.LookAt(Soul.Sim.ToUnity(mid + new UnityEngine.Vector3(0, .3f, 0)));
shot(sc, "telegraph_slope");
UnityEngine.Object.Destroy(side.gameObject);
foreach (var tt in new[] { tA, tB, tC }) tt.Dispose();
rt.Release(); UnityEngine.Object.Destroy(rt);
return sb.ToString();
