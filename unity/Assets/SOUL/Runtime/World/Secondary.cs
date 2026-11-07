using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Soul
{
    // Port of secondary.js: world-space Verlet cloth (fixed 120 Hz substeps, constrained
    // stretch, body-capsule collisions) for the torn robe panels and the long face scarf, plus
    // the small spring on the gathered scarf knot (hoodTail). Sim space; the cloth meshes are
    // rebuilt every frame in Unity space (double sided).
    public class Cloth
    {
        static readonly HashSet<string> BLOCKERS = new HashSet<string> { "pelvis", "belly", "chest", "Lthigh", "Rthigh", "Lshin", "Rshin", "LupperArm", "RupperArm" };
        static readonly float[] FRAY = { 0, .41f, .08f, .29f, .025f, .52f, .12f };
        readonly TNode anchor; readonly BodyColliders body;
        readonly float width, length, flare, damping, wind, gravity; readonly int rows, columns; readonly Vector3 offset; readonly bool front; readonly Vector2? arc;
        readonly Vector3[] points, previous, verts; readonly List<(int a, int b, float rest, float k)> links = new List<(int, int, float, float)>();
        readonly Mesh mesh; public readonly GameObject go;
        float accumulator;

        public Cloth(Transform parent, TNode anchor, BodyColliders body, string name, float width, float length, int rows, int columns, Vector3 offset, Color colorSRGB,
            bool front = false, Vector2? arc = null, float flare = .13f, float damping = .952f, float wind = .18f, float gravity = 7.5f, float bend = .32f)
        {
            this.anchor = anchor; this.body = body; this.width = width; this.length = length; this.rows = rows; this.columns = columns; this.offset = offset;
            this.front = front; this.arc = arc; this.flare = flare; this.damping = damping; this.wind = wind; this.gravity = gravity;
            int n = (rows + 1) * (columns + 1);
            points = new Vector3[n]; previous = new Vector3[n]; verts = new Vector3[n];
            var colors = new Color[n]; var tris = new List<int>();
            Color baseC = colorSRGB.linear, worn = ((Color)new Color32(0x75, 0x66, 0x51, 255)).linear, repairC = ((Color)new Color32(0x9a, 0x8e, 0x7c, 255)).linear;
            for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
            {
                bool edge = x == 0 || x == columns; float t = (float)y / rows;
                float stripe = .96f - .29f * Mathf.Pow(t, 1.7f) - (edge ? .045f : 0) + .035f * Mathf.Sin(x * 7 + y * 11);
                bool repair = x >= 2 && x <= 3 && y >= Mathf.FloorToInt(rows * .55f) && y <= Mathf.FloorToInt(rows * .55f) + 1;
                var dye = repair ? repairC : Color.Lerp(baseC, worn, .24f * t * t * t);
                colors[y * (columns + 1) + x] = new Color(dye.r * stripe, dye.g * stripe, dye.b * stripe, 1);
                if (y < rows && x < columns) { int a = y * (columns + 1) + x, b = a + columns + 1; tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 }); }
            }
            void Link(int x, int y, int xx, int yy, float k = 1) => links.Add((y * (columns + 1) + x, yy * (columns + 1) + xx, Vector3.Distance(LocalPoint(x, y), LocalPoint(xx, yy)), k));
            for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
            {
                if (x < columns && y > 0) Link(x, y, x + 1, y);
                if (y < rows) Link(x, y, x, y + 1);
                if (x < columns && y < rows) { Link(x, y, x + 1, y + 1); Link(x + 1, y, x, y + 1); }
                if (y < rows - 1) Link(x, y, x, y + 2, bend);
            }
            mesh = new Mesh { name = name }; mesh.MarkDynamic();
            mesh.vertices = verts; mesh.colors = colors; mesh.SetTriangles(tris, 0);
            var child = parent.Find(name);
            if (!child) throw new System.InvalidOperationException("Missing scene cloth object: " + name);
            go = child.gameObject;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Mats.Lit(Color.white, Color.black, 0, .05f, true, CullMode.Off);
            r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
            Reset();
        }
        Vector3 LocalPoint(int x, int y = 0)
        {
            float t = (float)y / rows, spread = 1 + flare * t;
            if (arc.HasValue)
            {
                float a = Mathf.Lerp(arc.Value.x, arc.Value.y, (float)x / columns);
                return new Vector3(Mathf.Sin(a) * .242f * spread, -t * length, Mathf.Cos(a) * .185f * spread) + offset;
            }
            return new Vector3(((float)x / columns - .5f) * width * spread, -t * length, (front ? 1 : -1) * t * .065f) + offset;
        }
        Vector3 Attach(int x, int y = 0)
        {
            var p = anchor.LocalToWorld(LocalPoint(x, y));
            // the sewn edge can ride over a raised arm instead of being pinned inside its capsule
            if (y == 0) for (int pass = 0; pass < 6; pass++) Collide(ref p);
            return p;
        }
        public void Reset()
        {
            accumulator = 0; anchor.UpdateWorldFromRoot(false);
            for (int i = 0; i < points.Length; i++)
            {
                var p = Attach(i % (columns + 1), i / (columns + 1));
                if (i > columns) Collide(ref p);
                points[i] = p; previous[i] = p;
            }
            Render();
        }
        public void Update(float dt, float time)
        {
            anchor.UpdateWorldFromRoot(false);
            // teleports reset history instead of injecting unbounded energy
            if (Vector3.Distance(points[0], Attach(0)) > 1.2f) Reset();
            accumulator = Mathf.Min(accumulator + dt, .05f);
            while (accumulator >= 1 / 120f) { Step(1 / 120f, time); accumulator -= 1 / 120f; }
            Render();
        }
        void Render()
        {
            int cols = columns + 1;
            for (int i = 0; i < points.Length; i++)
            {
                // ragged cut is render-only; project the cut edge too
                bool lastRow = i >= rows * cols;
                var cut = points[i];
                if (lastRow)
                {
                    float fray = FRAY[(i % cols) % 7];
                    cut = Vector3.Lerp(points[i], points[i - cols], fray);
                    for (int pass = 0; pass < 3; pass++) Collide(ref cut);
                }
                verts[i] = Sim.ToUnity(cut);
            }
            mesh.vertices = verts; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
        void Step(float dt, float time)
        {
            int count = columns + 1;
            var pins = new Vector3[count]; for (int x = 0; x < count; x++) pins[x] = Attach(x);
            var inverse = anchor.matrixWorld.inverse; var world = anchor.matrixWorld;
            for (int i = count; i < points.Length; i++)
            {
                var p = points[i]; var old = p; var velocity = (p - previous[i]) * damping;
                if (velocity.magnitude > .06f) velocity = Sim.SetLength(velocity, .06f);
                p += velocity; p.y -= gravity * dt * dt;
                p.x += Mathf.Sin(time * 1.8f + i * .47f) * wind * dt * dt;
                p.z += (front ? .22f : -.35f) * dt * dt;
                points[i] = p; previous[i] = old;
            }
            for (int pass = 0; pass < 16; pass++)
            {
                for (int x = 0; x < count; x++) points[x] = pins[x];
                foreach (var l in links) Constrain(l.a, l.b, l.rest, l.k);
                for (int i = count; i < points.Length; i++)
                {
                    var local = inverse.MultiplyPoint3x4(points[i]);
                    // only guard the sewn top against flipping across the torso
                    if (Mathf.Abs(local.x) < .25f && local.y > (arc.HasValue ? -.16f : -.48f) && local.y < .13f)
                    {
                        float clearance = arc.HasValue ? .065f : .18f;
                        local.z = front ? Mathf.Max(local.z, clearance) : Mathf.Min(local.z, -clearance);
                        points[i] = world.MultiplyPoint3x4(local);
                    }
                    var p = points[i]; Collide(ref p); p.y = Mathf.Max(Secondary.GroundY + .035f, p.y); points[i] = p;
                }
            }
        }
        // push cloth nodes out of the body capsules (legs, hips, torso, upper arms)
        void Collide(ref Vector3 p)
        {
            if (body == null) return;
            foreach (var c in body.caps)
            {
                if (!BLOCKERS.Contains(c.name)) continue;
                var ab = c.b - c.a; float t = Mathf.Clamp01(Vector3.Dot(p - c.a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f));
                var cp = c.a + ab * t; float d = Vector3.Distance(p, cp), min = c.r + .025f;
                if (d < min) { if (d < 1e-6f) p.y += min; else p = cp + (p - cp) * (min / d); }
            }
        }
        void Constrain(int a, int b, float rest, float k)
        {
            var delta = points[b] - points[a]; float d = delta.magnitude; if (d < .00001f) return;
            delta *= (d - rest) / d * k;
            if (a <= columns) points[b] -= delta; else { points[a] += delta * .5f; points[b] -= delta * .5f; }
        }
    }

    public class Secondary
    {
        readonly List<Cloth> cloths = new List<Cloth>();
        readonly CharacterRig ch;
        /// floor height under the character (set each frame by the game; cloth never sinks below it)
        public static float GroundY;
        Quaternion previousHead; float spring, velocity;

        public Secondary(CharacterRig ch, Transform parent)
        {
            this.ch = ch;
            ch.UpdateWorld(); ch.body.Update();
            // No back drape. Left side of the robe is whole; the right side is ripped away high up
            // (Dune-like): only a few short shredded strips hang from the front and back.
            var skirts = new (string name, float from, float to, bool front, float length, int columns)[]
            {
                ("front-left-robe-panel", -1.46f, -.13f, true, .75f, 7),
                ("back-left-robe-panel", -Mathf.PI, -1.68f, false, .84f, 7),
                ("torn-front-right-strip-a", .13f, .45f, true, .30f, 2),
                ("torn-front-right-strip-b", .58f, .82f, true, .17f, 2),
                ("torn-back-right-strip-a", 2.30f, 2.70f, false, .22f, 2),
                ("torn-back-right-strip-b", 2.80f, Mathf.PI, false, .36f, 2),
            };
            foreach (var s in skirts)
                cloths.Add(new Cloth(parent, ch["pelvis"], ch.body, s.name, .25f, s.length, Mathf.Max(6, Mathf.RoundToInt(s.length * 17)), s.columns, new Vector3(0, .065f, 0),
                    s.front ? (Color)new Color32(0xc5, 0xb6, 0x94, 255) : (Color)new Color32(0xae, 0x9c, 0x80, 255), s.front, new Vector2(s.from, s.to), .26f));
            cloths.Add(new Cloth(parent, ch["head"], ch.body, "long-frayed-face-scarf", .105f, .72f, 12, 4, new Vector3(-.15f, -.015f, .20f), new Color32(0xb6, 0xa6, 0x8b, 255),
                true, null, -.18f, .969f, .28f, 5.5f, .22f));
            previousHead = ch["head"].WorldQuaternion;
        }
        public void Update(float dt, float time, float speed)
        {
            foreach (var c in cloths) c.Update(dt, time);
            var head = ch["head"].WorldQuaternion;
            var delta = Quaternion.Inverse(previousHead) * head;
            // shortest quaternion arc avoids a kick when the character crosses +/- pi
            float sign = delta.w < 0 ? -1 : 1, impulse = 2 * sign * (delta.y + delta.x * .6f);
            previousHead = head;
            float target = Mathf.Clamp(-impulse * 2 - speed * .02f + Mathf.Sin(time * 2.2f) * .015f, -.18f, .18f);
            float remaining = Mathf.Min(dt, .05f);
            while (remaining > 0) { float step = Mathf.Min(1 / 120f, remaining); velocity += ((target - spring) * 110 - velocity * 15) * step; spring += velocity * step; remaining -= step; }
            if (ch.hoodTail != null) ch.hoodTail.quaternion = Sim.EulerXYZ(spring, 0, spring * .35f);
        }
        public void Reset()
        {
            foreach (var c in cloths) c.Reset();
            spring = 0; velocity = 0;
            if (ch.hoodTail != null) ch.hoodTail.quaternion = Quaternion.identity;
            previousHead = ch["head"].WorldQuaternion;
        }
    }
}
