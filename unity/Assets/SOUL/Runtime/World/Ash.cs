using UnityEngine;
using UnityEngine.Rendering;

namespace Soul
{
    // Port of scene.js drifting ash (updateAmbient): 360 grey flakes falling and drifting in a
    // 38 x 13 x 38 m box that wraps around the player. Rendered as one mesh of camera-facing
    // quads (three: PointsMaterial size .035 with attenuation ~ .012 m world size at fov 38).
    public class Ash
    {
        const int COUNT = 360; const float SIZE = .016f;
        readonly Vector3[] pos = new Vector3[COUNT]; readonly float[] fall = new float[COUNT], phase = new float[COUNT];
        readonly Vector3[] verts = new Vector3[COUNT * 4];
        readonly Mesh mesh; readonly Material mat; float time; Vector3 center;
        /// 0 = grey ash, 1 = purified zone: warm glowing pollen / fireflies drifting up (eased)
        public float bloom; float b;
        /// extra drift (sim XZ, m/s): SoulGame.Guide leans the ash toward the zone's boss (경로 유도, subtle)
        public Vector3 wind; Vector3 w;
        uint seed = 4207;
        float Rand() { seed = seed * 1664525u + 1013904223u; return seed / 4294967296f; }

        public Ash(GameObject go)
        {
            var colors = new Color[COUNT * 4]; var tris = new int[COUNT * 6];
            var baseC = ((Color)new Color32(0xb5, 0xaa, 0xa0, 255)).linear;
            for (int i = 0; i < COUNT; i++)
            {
                pos[i] = new Vector3((Rand() - .5f) * 38, Rand() * 13, (Rand() - .5f) * 38);
                var c = baseC * (.55f + Rand() * .45f); c.a = 1;
                for (int k = 0; k < 4; k++) colors[i * 4 + k] = c;
                fall[i] = .13f + Rand() * .22f; phase[i] = Rand() * Mathf.PI * 2;
                int v = i * 4; tris[i * 6] = v; tris[i * 6 + 1] = v + 2; tris[i * 6 + 2] = v + 1; tris[i * 6 + 3] = v; tris[i * 6 + 4] = v + 3; tris[i * 6 + 5] = v + 2;
            }
            mesh = new Mesh { name = "drifting-ash" }; mesh.MarkDynamic();
            mesh.vertices = verts; mesh.colors = colors; mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2000);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat = Mats.Unlit(new Color(1, 1, 1, .52f), Blend.Alpha, true, CullMode.Off, false);
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }
        static float Wrap(float v, float span) => Sim.Mod(v, span);
        public void Update(float dt, Vector3 c)
        {
            float step = float.IsFinite(dt) ? Mathf.Clamp(dt, 0, .1f) : 0;
            time += step; b = Mathf.MoveTowards(b, bloom, step * .35f);
            float fallK = 1 - 1.35f * b; w = Vector3.MoveTowards(w, wind, step * .15f);
            Mats.SetColor(mat, Color.Lerp(new Color(1, 1, 1, .52f), new Color(2.6f, 2.3f, 1.1f, .85f), b));
            if (float.IsFinite(c.x) && float.IsFinite(c.z)) center = new Vector3(c.x, float.IsFinite(c.y) ? c.y : 0, c.z);
            for (int i = 0; i < COUNT; i++)
            {
                var p = pos[i]; float ph = phase[i];
                p.x = center.x - 19 + Wrap(p.x + (.09f + Mathf.Sin(time * .6f + ph) * .13f + w.x) * step - center.x + 19, 38);
                p.y = center.y + Wrap(p.y - fall[i] * fallK * step - center.y, 13);
                p.z = center.z - 19 + Wrap(p.z + (Mathf.Cos(time * .4f + ph) * .1f + w.z) * step - center.z + 19, 38);
                pos[i] = p;
            }
            var cam = Camera.main;
            Vector3 right = cam ? cam.transform.right : Vector3.right, up = cam ? cam.transform.up : Vector3.up;
            float size = SIZE * (1 + b * 1.4f); right *= size * .5f; up *= size * .5f;
            for (int i = 0; i < COUNT; i++)
            {
                var u = Sim.ToUnity(pos[i]); int v = i * 4;
                verts[v] = u - right - up; verts[v + 1] = u + right - up; verts[v + 2] = u + right + up; verts[v + 3] = u - right + up;
            }
            mesh.vertices = verts;
            mesh.bounds = new Bounds(Sim.ToUnity(center), Vector3.one * 60);
        }
    }
}
