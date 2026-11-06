using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Soul
{
    // Procedural low-poly mesh builder for world props. Author in SIM space (three.js coords:
    // +Y up, +Z forward, metres). Build() converts to Unity (x -> -x, winding flipped) and
    // produces flat-shaded, vertex-coloured triangles for the SOUL/Lit shader.
    // Colours: pass sRGB hex (0xRRGGBB) or Color in sRGB; they are converted to linear here.
    // A transform stack (Push/Pop + Translate/RotateY/...) composes parts in local frames.
    public class MeshBuilder
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Color> c = new List<Color>();
        Matrix4x4 m = Matrix4x4.identity;
        readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();
        public System.Random rng;
        public MeshBuilder(int seed = 1) { rng = new System.Random(seed); }
        public int TriangleCount => v.Count / 3;

        // ---------- randomness ----------
        public float Rand() => (float)rng.NextDouble();
        public float Range(float a, float b) => a + (b - a) * Rand();
        public int RangeInt(int a, int bExclusive) => rng.Next(a, bExclusive);
        public bool Chance(float p) => Rand() < p;

        // ---------- colours ----------
        public static Color Hex(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
        /// sRGB colour with random brightness jitter (+-amount), returned in sRGB
        public Color Jitter(Color srgb, float amount = .08f) { float k = 1 + Range(-amount, amount); return new Color(srgb.r * k, srgb.g * k, srgb.b * k); }
        public Color Jitter(int hex, float amount = .08f) => Jitter(Hex(hex), amount);

        // ---------- transform stack (sim space) ----------
        public void Push() => stack.Push(m);
        public void Pop() => m = stack.Pop();
        public void Translate(float x, float y, float z) => m = m * Matrix4x4.Translate(new Vector3(x, y, z));
        public void Translate(Vector3 p) => m = m * Matrix4x4.Translate(p);
        /// rotations use sim (three.js) conventions; angles in radians
        public void RotateX(float a) => m = m * Matrix4x4.Rotate(Sim.AxisAngle(Vector3.right, a));
        public void RotateY(float a) => m = m * Matrix4x4.Rotate(Sim.AxisAngle(Vector3.up, a));
        public void RotateZ(float a) => m = m * Matrix4x4.Rotate(Sim.AxisAngle(Vector3.forward, a));
        public void Rotate(Vector3 axis, float a) => m = m * Matrix4x4.Rotate(Sim.AxisAngle(axis.normalized, a));
        public void Scale(float x, float y, float z) => m = m * Matrix4x4.Scale(new Vector3(x, y, z));
        public Vector3 Apply(Vector3 p) => m.MultiplyPoint3x4(p);

        // ---------- primitives (all in the current local frame) ----------
        /// triangle a,b,c counter-clockwise seen from the outside (three.js front face)
        public void Tri(Vector3 a, Vector3 b, Vector3 cc, Color srgb)
        {
            var lin = srgb.linear; lin.a = 1;
            v.Add(Apply(a)); v.Add(Apply(b)); v.Add(Apply(cc)); c.Add(lin); c.Add(lin); c.Add(lin);
        }
        /// quad a,b,c,d counter-clockwise seen from the outside
        public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color srgb) { Tri(a, b, cc, srgb); Tri(a, cc, d, srgb); }
        /// axis-aligned box centred at `center` (local frame), optional per-face shading
        public void Box(Vector3 center, Vector3 size, Color srgb, float shadeJitter = .06f)
        {
            Vector3 h = size * .5f, o = center;
            Vector3 P(float x, float y, float z) => o + new Vector3(x * h.x, y * h.y, z * h.z);
            Color S(float k) => new Color(srgb.r * k, srgb.g * k, srgb.b * k) * (1 + Range(-shadeJitter, shadeJitter));
            Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), S(1.06f));   // top
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), S(.7f));  // bottom
            Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), S(.95f));     // +z
            Quad(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), S(.85f)); // -z
            Quad(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), S(.9f));      // +x
            Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), S(.8f));  // -x
        }
        /// cylinder / frustum along +Y from y0 to y1 (bottom radius r0, top radius r1)
        public void Cylinder(Vector3 baseCenter, float r0, float r1, float height, int segments, Color srgb, bool caps = true, float jitter = 0)
        {
            var bot = new Vector3[segments]; var top = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2, j0 = 1 + Range(-jitter, jitter), j1 = 1 + Range(-jitter, jitter);
                bot[i] = baseCenter + new Vector3(Mathf.Sin(a) * r0 * j0, 0, Mathf.Cos(a) * r0 * j0);
                top[i] = baseCenter + new Vector3(Mathf.Sin(a) * r1 * j1, height, Mathf.Cos(a) * r1 * j1);
            }
            for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments; float k = .82f + .18f * Mathf.Cos(i / (float)segments * Mathf.PI * 2 - .8f);
                Quad(bot[i], bot[n], top[n], top[i], new Color(srgb.r * k, srgb.g * k, srgb.b * k));
            }
            if (caps)
            {
                Vector3 bc = baseCenter, tc = baseCenter + new Vector3(0, height, 0);
                for (int i = 0; i < segments; i++) { int n = (i + 1) % segments; Tri(tc, top[i], top[n], srgb * 1.05f); Tri(bc, bot[n], bot[i], srgb * .7f); }
            }
        }
        /// cylinder between two points (beams, pipes, branches, cables)
        public void Beam(Vector3 a, Vector3 b, float r0, float r1, int segments, Color srgb)
        {
            var d = b - a; float len = d.magnitude; if (len < 1e-4f) return;
            Push(); Translate(a); m = m * Matrix4x4.Rotate(Sim.FromTo(Vector3.up, d / len));
            Cylinder(Vector3.zero, r0, r1, len, segments, srgb, true); Pop();
        }
        /// irregular rock: a jittered icosphere, flattened by `squash` on Y
        public void Rock(Vector3 center, float radius, Color srgb, float irregular = .25f, float squash = 1, int detail = 1)
        {
            var (verts, faces) = Ico(detail);
            var off = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++) { var p = verts[i] * radius * (1 + Range(-irregular, irregular)); p.y *= squash; off[i] = center + p; }
            foreach (var f in faces)
            {
                Vector3 a = off[f.x], b = off[f.y], cc = off[f.z];
                float k = .8f + .25f * Mathf.Clamp01(((a + b + cc) / 3 - center).normalized.y * .5f + .5f) + Range(-.05f, .05f);
                Tri(a, b, cc, new Color(srgb.r * k, srgb.g * k, srgb.b * k));
            }
        }
        /// flat ring / disc sector lying on the ground (y), from angle a0 to a1 (radians, 0 = +Z)
        public void Disc(Vector3 center, float rInner, float rOuter, int segments, Color srgb, float a0 = 0, float a1 = Mathf.PI * 2)
        {
            for (int i = 0; i < segments; i++)
            {
                float t0 = Mathf.Lerp(a0, a1, i / (float)segments), t1 = Mathf.Lerp(a0, a1, (i + 1) / (float)segments);
                Vector3 o0 = center + new Vector3(Mathf.Sin(t0), 0, Mathf.Cos(t0)) * rOuter, o1 = center + new Vector3(Mathf.Sin(t1), 0, Mathf.Cos(t1)) * rOuter;
                if (rInner <= 0) Tri(center, o0, o1, srgb);
                else { Vector3 i0 = center + new Vector3(Mathf.Sin(t0), 0, Mathf.Cos(t0)) * rInner, i1 = center + new Vector3(Mathf.Sin(t1), 0, Mathf.Cos(t1)) * rInner; Quad(i0, o0, o1, i1, srgb); }
            }
        }

        static (List<Vector3>, List<Vector3Int>) Ico(int detail)
        {
            float t = (1 + Mathf.Sqrt(5)) / 2;
            var verts = new List<Vector3> { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            var faces = new List<Vector3Int> { new Vector3Int(0, 11, 5), new Vector3Int(0, 5, 1), new Vector3Int(0, 1, 7), new Vector3Int(0, 7, 10), new Vector3Int(0, 10, 11), new Vector3Int(1, 5, 9), new Vector3Int(5, 11, 4), new Vector3Int(11, 10, 2), new Vector3Int(10, 7, 6), new Vector3Int(7, 1, 8), new Vector3Int(3, 9, 4), new Vector3Int(3, 4, 2), new Vector3Int(3, 2, 6), new Vector3Int(3, 6, 8), new Vector3Int(3, 8, 9), new Vector3Int(4, 9, 5), new Vector3Int(2, 4, 11), new Vector3Int(6, 2, 10), new Vector3Int(8, 6, 7), new Vector3Int(9, 8, 1) };
            for (int d = 0; d < detail; d++)
            {
                var next = new List<Vector3Int>(); var cache = new Dictionary<long, int>();
                int Mid(int a, int b) { long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; if (cache.TryGetValue(key, out var i)) return i; verts.Add(((verts[a] + verts[b]) * .5f).normalized); cache[key] = verts.Count - 1; return verts.Count - 1; }
                foreach (var f in faces) { int ab = Mid(f.x, f.y), bc = Mid(f.y, f.z), ca = Mid(f.z, f.x); next.Add(new Vector3Int(f.x, ab, ca)); next.Add(new Vector3Int(f.y, bc, ab)); next.Add(new Vector3Int(f.z, ca, bc)); next.Add(new Vector3Int(ab, bc, ca)); }
                faces = next;
            }
            return (verts, faces);   // three.js icosahedron indices: CCW outward already
        }

        /// Unity mesh: vertices converted from sim space, winding flipped, flat normals.
        public Mesh Build(string name = "prop")
        {
            var mesh = new Mesh { name = name };
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            var verts = new Vector3[v.Count]; var tris = new int[v.Count];
            for (int i = 0; i < v.Count; i++) verts[i] = Sim.ToUnity(v[i]);
            for (int i = 0; i + 2 < v.Count; i += 3) { tris[i] = i; tris[i + 1] = i + 2; tris[i + 2] = i + 1; }
            mesh.vertices = verts; mesh.colors = c.ToArray(); mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }

    // ---------- world prop description ----------
    /// Collision footprint on the ground plane in the prop's local sim frame.
    /// Wall-type obstacle. Blocks a character whose feet are between y0 and y1 (minus the step
    /// height, so knee-high walls can be stepped over). Defaults: blocks at every height.
    public struct PropCollider
    {
        public bool box; public Vector2 center; public float radius; public Vector2 halfSize; public float yaw;
        public float y0, y1; public bool ranged;
        public int gate;   // part id: stops blocking once the player owns that part (0 = always solid)
        public PropCollider Gated(int part) { var c = this; c.gate = part; return c; }
        public static PropCollider Circle(float x, float z, float r) => new PropCollider { center = new Vector2(x, z), radius = r };
        /// oriented rectangle (half extents along local x/z, rotated by yaw around +Y, radians)
        public static PropCollider Rect(float x, float z, float halfX, float halfZ, float yaw = 0) => new PropCollider { box = true, center = new Vector2(x, z), halfSize = new Vector2(halfX, halfZ), yaw = yaw };
        /// limit this obstacle to a vertical band (e.g. an upper-floor wall: y0 = floor top, y1 = floor top + wall height)
        public PropCollider Between(float bottom, float top) { var c = this; c.ranged = true; c.y0 = bottom; c.y1 = top; return c; }
    }
    /// Walkable surface (the top a character stands on). Ground level outside any surface is y = 0.
    ///   Box: flat top at `top`.  Ramp: rises along local +Z from yLow (z = -halfZ) to yHigh (z = +halfZ).
    ///   Mound: smooth dome, height * (1 - (d / radius)^2).
    /// Movement rules: walk up <= 0.45 m (STEP), F-jump mounts ledges <= 1.1 m (JUMP); higher ledges and
    /// wide gaps are for future part upgrades -> mark the surface beyond them with gate = 1.
    public struct PropSurface
    {
        public int kind; public Vector2 center, halfSize; public float yaw, top, yLow, yHigh, radius; public int gate;
        public bool noGrab;   // rope edge-grabs ignore this surface (gate ledges: only their anchors count)
        public const int BOX = 0, RAMP = 1, MOUND = 2;
        public static PropSurface Box(float x, float z, float halfX, float halfZ, float top, float yaw = 0, int gate = 0) => new PropSurface { kind = BOX, center = new Vector2(x, z), halfSize = new Vector2(halfX, halfZ), top = top, yaw = yaw, gate = gate };
        public static PropSurface Ramp(float x, float z, float halfX, float halfZ, float yLow, float yHigh, float yaw = 0, int gate = 0) => new PropSurface { kind = RAMP, center = new Vector2(x, z), halfSize = new Vector2(halfX, halfZ), yLow = yLow, yHigh = yHigh, yaw = yaw, gate = gate };
        public static PropSurface Mound(float x, float z, float radius, float height, int gate = 0) => new PropSurface { kind = MOUND, center = new Vector2(x, z), radius = radius, top = height, gate = gate };
    }
    public class PropModel
    {
        public Mesh mesh;
        public List<PropCollider> colliders = new List<PropCollider>();
        public List<PropSurface> surfaces = new List<PropSurface>();
        public float footprint = 1;      // keep-out radius when scattering (metres)
        public float height = 1;
        public bool landmark;
    }
}
