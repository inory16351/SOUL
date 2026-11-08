// Per-stage chunk build cost (play mode, needs WorldStreamer.Prof): cold terrain cache, teleport, then a 300 m walk.
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>(); var sb = new System.Text.StringBuilder();
var P = Soul.WorldStreamer.Prof; int N = Soul.WorldStreamer.PROF_N;
var p = new UnityEngine.Vector3(-600, 0, -800); var d = new UnityEngine.Vector3(1, 0, 0);
g.Teleport(p + new UnityEngine.Vector3(0, 0, 600)); Soul.Terrain.Clear(); g.Teleport(p);
for (int i = 0; i <= N; i++) P[i] = 0;
for (int f = 0; f < 300 / (3.45f / 60); f++) { p += d * (3.45f / 60); g.open.Update(p); }
while (g.open.PendingBuilds > 0) g.open.Update(p);
string[] names = { "terrain tiles", "ground mesh", "ground detail", "water", "ridge/gates/landmarks/plots", "scatter", "trail props", "reeds/dressing/collision" };
double total = 0; for (int i = 0; i < N; i++) total += P[i];
for (int i = 0; i < N; i++) sb.AppendFormat("{0,-28} {1:F2} ms/chunk\n", names[i], P[i] / P[N]);
sb.AppendFormat("total {0:F2} ms/chunk over {1} chunks\n", total / P[N], P[N]);
return sb.ToString();
