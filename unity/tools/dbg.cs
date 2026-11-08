var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>(); g.Teleport(new UnityEngine.Vector3(1440, 0, 807));
var sb = new System.Text.StringBuilder();
foreach (var q in new[] { new UnityEngine.Vector2(1445, 812), new UnityEngine.Vector2(1435, 812), new UnityEngine.Vector2(1445, 800) })
{
    int ty; float d = Soul.Terrain.Water(q.x, q.y, out ty);
    sb.Append(q).Append(" depth=").Append(d.ToString("F2")).Append(" type=").Append(ty).Append(" h=").Append(Soul.Terrain.Height(q.x, q.y).ToString("F2")).Append(" wet=").Append(Soul.Terrain.Wet(q.x, q.y).ToString("F2")).Append('\n');
}
int n = 0, verts = 0; string mat = "";
foreach (var mf in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshFilter>(UnityEngine.FindObjectsSortMode.None))
    if (mf.name == "water" && mf.sharedMesh != null) { var b = mf.GetComponent<UnityEngine.Renderer>().bounds; var u = Soul.Sim.ToUnity(new UnityEngine.Vector3(1445, 0, 812)); if (u.x > b.min.x && u.x < b.max.x && u.z > b.min.z && u.z < b.max.z) { n++; verts += mf.sharedMesh.vertexCount; mat = mf.GetComponent<UnityEngine.Renderer>().sharedMaterial.shader.name + " q" + mf.GetComponent<UnityEngine.Renderer>().sharedMaterial.renderQueue; } }
sb.Append("water meshes over point: ").Append(n).Append(" verts ").Append(verts).Append(' ').Append(mat);
return sb.ToString();
