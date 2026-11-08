// Nav captures (play mode): player on the stair deck, a hound + a warden summoned on the ground; shots while they
// walk round to the stairs and climb (Captures/nav_stairs_1..3.png), seen from a low camera beside the deck.
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>();
var P = new UnityEngine.Vector3(-80.85f, 6.93f, 114.54f); var S = new UnityEngine.Vector3(-67.96f, 2.43f, 119.99f);
g.enemies.ClearAll(); g.Simulate(3.2f);
g.Teleport(P); g.ch.root.position = P; g.ch.UpdateWorld(); g.Simulate(.1f);
g.enemies.passive = true;
var a = g.enemies.SpawnAt("crawler", S); var b = g.enemies.SpawnAt("warden", S + new UnityEngine.Vector3(1.5f, 0, 0));
var cam = UnityEngine.Camera.main; var sc = UnityEngine.Object.Instantiate(cam); sc.fieldOfView = 55;
var mid = (P + S) * .5f; var eye = mid + new UnityEngine.Vector3(4, 0, 14); eye.y = Soul.WorldCollision.Height(eye.x, eye.z) + 9;
sc.transform.position = Soul.Sim.ToUnity(eye); sc.transform.LookAt(Soul.Sim.ToUnity(mid));
var rt = new UnityEngine.RenderTexture(1600, 900, 24, UnityEngine.RenderTextureFormat.ARGB32); var sb = new System.Text.StringBuilder();
float[] at = { 2.5f, 5.5f, 9 }; float t = 0;
for (int i = 0; i < at.Length; i++)
{
    g.Simulate(at[i] - t); t = at[i];
    sc.targetTexture = rt; sc.Render(); sc.targetTexture = null; UnityEngine.RenderTexture.active = rt;
    var tex = new UnityEngine.Texture2D(1600, 900, UnityEngine.TextureFormat.RGB24, false);
    tex.ReadPixels(new UnityEngine.Rect(0, 0, 1600, 900), 0, 0); tex.Apply(); UnityEngine.RenderTexture.active = null;
    System.IO.File.WriteAllBytes(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Captures/nav_stairs_" + (i + 1) + ".png"), tex.EncodeToPNG());
    UnityEngine.Object.Destroy(tex);
    sb.AppendFormat("t{0}: hound {1} warden {2}\n", t, a.pos.ToString("F1"), b.pos.ToString("F1"));
}
UnityEngine.Object.Destroy(sc.gameObject); rt.Release(); UnityEngine.Object.Destroy(rt);
return sb.ToString();
