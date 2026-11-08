var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>(); g.Teleport(new UnityEngine.Vector3(1440, 0, 807));
var sb = new System.Text.StringBuilder(); var u = Soul.Sim.ToUnity(new UnityEngine.Vector3(1445, 0, 812));
foreach (var mf in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshFilter>(UnityEngine.FindObjectsSortMode.None))
{
    if (mf.name != "water" || mf.sharedMesh == null) continue; var r = mf.GetComponent<UnityEngine.Renderer>(); var b = r.bounds;
    if (!(u.x > b.min.x && u.x < b.max.x && u.z > b.min.z && u.z < b.max.z)) continue;
    var m = mf.sharedMesh; var v = m.vertices; var uv = new System.Collections.Generic.List<UnityEngine.Vector2>(); m.GetUVs(0, uv); var c = m.colors;
    sb.Append("active=").Append(mf.gameObject.activeInHierarchy).Append(" enabled=").Append(r.enabled).Append(" bounds=").Append(b).Append(" pos=").Append(mf.transform.position).Append('\n');
    for (int i = 0; i < v.Length; i += 97) sb.Append(mf.transform.TransformPoint(v[i])).Append(" uv=").Append(uv.Count > i ? uv[i].ToString() : "none").Append(" c=").Append(c[i]).Append('\n');
}
var gr = UnityEngine.Physics.RaycastAll(new UnityEngine.Ray(u + UnityEngine.Vector3.up * 5, UnityEngine.Vector3.down));
sb.Append("ground h=").Append(Soul.Terrain.Height(1445, 812));
return sb.ToString();
