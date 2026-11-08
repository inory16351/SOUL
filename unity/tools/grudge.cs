var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>();
var sb = new System.Text.StringBuilder();
g.GrudgeTick(0);
var pod = Soul.ZoneAreas.Pods[0];
var p = pod.pos + new UnityEngine.Vector3(3, 0, 3);
g.Teleport(p);
g.GrudgeDrop(0, p + new UnityEngine.Vector3(3, 0, 0));
g.GrudgeTick(.02f);
sb.Append("carried before walk ").Append(Soul.GrudgeParts.carried.Count).Append('\n');
g.WalkTest(p + new UnityEngine.Vector3(3, 0, 0), 3);
g.GrudgeTick(.02f);
sb.Append("carried after walk ").Append(Soul.GrudgeParts.carried.Count).Append('\n');
// submit at the pod: 15 min of play time
int scrap0 = g.scrap;
Soul.GrudgeParts.Submit(0, pod);
sb.Append("pending ").Append(Soul.GrudgeParts.pending.Count).Append(" cleansed ").Append(Soul.GrudgeParts.IsCleansed(0)).Append('\n');
for (int i = 0; i < 14; i++) g.GrudgeTick(60);
sb.Append("after 14 min cleansed ").Append(Soul.GrudgeParts.IsCleansed(0)).Append('\n');
g.GrudgeTick(61);
sb.Append("after 15 min cleansed ").Append(Soul.GrudgeParts.IsCleansed(0)).Append(" scrap +").Append(g.scrap - scrap0).Append('\n');
// equip parts one by one: face stage and route follow
for (int z = 1; z <= 7; z++)
{
    Soul.GrudgeParts.carried.Add(z); Soul.GrudgeParts.Equip(z);
    sb.Append("equipped ").Append(Soul.GrudgeParts.Count).Append(" stage ").Append(Soul.PixelFace.GrudgeStage).Append(" route ").Append(Soul.GrudgeParts.Route).Append('\n');
}
Soul.GrudgeParts.carried.Add(3); Soul.GrudgeParts.Equip(3);
sb.Append("re-equip same zone count ").Append(Soul.GrudgeParts.Count).Append('\n');
Soul.GrudgeParts.Reset();
return sb.ToString();
