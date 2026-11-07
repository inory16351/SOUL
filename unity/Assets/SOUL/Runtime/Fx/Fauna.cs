using UnityEngine;
using UnityEngine.Rendering;

namespace Soul
{
    // Life returning to a purified zone (되살아나는 세계): a few butterflies fluttering around the
    // player and birds circling overhead. One dynamic mesh of flapping wing pairs (sim space).
    // `weight` 0..1 fades them in / out (they shrink away), driven by ZoneState of the current zone.
    public class Fauna
    {
        const int BUTTERFLIES = 14, BIRDS = 5, N = BUTTERFLIES + BIRDS;
        readonly Mesh mesh; readonly MeshRenderer renderer;
        readonly Vector3[] verts = new Vector3[N * 6]; readonly Vector3[] pos = new Vector3[N], vel = new Vector3[N];
        readonly float[] phase = new float[N], speed = new float[N];
        float time, w; public float weight;
        uint seed = 9311; float Rand() { seed = seed * 1664525u + 1013904223u; return seed / 4294967296f; }

        public Fauna(GameObject go)
        {
            var cols = new Color[N * 6]; var tris = new int[N * 6];
            Color[] wings = { new Color(.95f, .86f, .45f), new Color(.95f, .95f, .9f), new Color(.85f, .55f, .62f), new Color(.6f, .72f, .95f) };
            for (int i = 0; i < N; i++)
            {
                bool bird = i >= BUTTERFLIES;
                var c = bird ? new Color(.16f, .15f, .14f) : wings[i % wings.Length];
                for (int k = 0; k < 6; k++) { cols[i * 6 + k] = c.linear; tris[i * 6 + k] = i * 6 + k; }
                phase[i] = Rand() * 10; speed[i] = bird ? .25f + Rand() * .15f : 1 + Rand();
                pos[i] = new Vector3((Rand() - .5f) * 20, bird ? 13 + Rand() * 5 : .6f + Rand() * 1.4f, (Rand() - .5f) * 20);
            }
            mesh = new Mesh { name = "fauna" }; mesh.MarkDynamic();
            mesh.vertices = verts; mesh.colors = cols; mesh.triangles = tris; mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4000);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = Mats.Unlit(new Color(1.3f, 1.3f, 1.3f, 1), Blend.Opaque, true, CullMode.Off);
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.enabled = false;
        }

        public void Update(float dt, Vector3 center)
        {
            time += dt; w = Mathf.MoveTowards(w, weight, dt * .4f);
            renderer.enabled = w > .01f; if (!renderer.enabled) return;
            for (int i = 0; i < N; i++)
            {
                bool bird = i >= BUTTERFLIES; float ph = phase[i];
                Vector3 p;
                if (bird)
                {   // wide circles around a point that trails the player
                    float r = 14 + 6 * Mathf.Sin(ph), a = time * speed[i] + ph * 2;
                    p = new Vector3(center.x + Mathf.Sin(a) * r, center.y + 13 + 3 * Mathf.Sin(ph * 3) + Mathf.Sin(time * .7f + ph) * .6f, center.z + Mathf.Cos(a) * r);
                    vel[i] = new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a));
                }
                else
                {   // erratic wander that stays within ~11 m of the player, re-seeded when left behind
                    p = pos[i];
                    var home = new Vector3(center.x + Mathf.Sin(ph * 7.1f) * 7, center.y + .9f + Mathf.Sin(time * .9f + ph) * .5f, center.z + Mathf.Cos(ph * 5.3f) * 7);
                    var wander = new Vector3(Mathf.Sin(time * 1.7f * speed[i] + ph), Mathf.Sin(time * 2.3f + ph * 2) * .6f, Mathf.Cos(time * 1.3f * speed[i] + ph * 3));
                    vel[i] = Vector3.Lerp(vel[i], (home - p) * .25f + wander * 1.2f, dt * 2);
                    p += vel[i] * dt;
                    if ((p - center).sqrMagnitude > 30 * 30) p = home;
                    pos[i] = p;
                }
                // flapping wing pair: two triangles hinged on the body line
                float flap = Mathf.Sin(time * (bird ? 7 : 22) * (bird ? 1 : speed[i]) + ph * 4) * (bird ? .5f : .9f);
                float span = (bird ? .55f : .15f) * w;
                var fwd = vel[i].sqrMagnitude > 1e-4f ? new Vector3(vel[i].x, 0, vel[i].z).normalized : Vector3.forward;
                var side = new Vector3(fwd.z, 0, -fwd.x);
                var nose = p + fwd * span * .8f; var tail = p - fwd * span * .6f;
                for (int s = 0; s < 2; s++)
                {
                    float sg = s == 0 ? 1 : -1;
                    var tip = p + side * sg * span * Mathf.Cos(flap) + Vector3.up * span * Mathf.Sin(flap);
                    int v = i * 6 + s * 3;
                    verts[v] = Sim.ToUnity(nose); verts[v + 1] = Sim.ToUnity(tail); verts[v + 2] = Sim.ToUnity(tip);
                }
            }
            mesh.vertices = verts;
            mesh.bounds = new Bounds(Sim.ToUnity(center), Vector3.one * 80);
        }
    }
}
