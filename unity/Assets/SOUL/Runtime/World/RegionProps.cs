using UnityEngine;
using System.Collections.Generic;

namespace Soul
{
    // All geometry and collider coordinates are authored in MeshBuilder's sim frame.
    // Only Build converts the mesh to Unity; do not mirror the collider coordinates.
    public static class RegionProps
    {
        const float Pi = Mathf.PI;
        const int Concrete = 0x8a8580, ConcreteDark = 0x6f6a64, Char = 0x2e2a27;
        const int Rust = 0x6b3f26, Steel = 0x4a4744, Sand = 0xa39a8a;
        const int SandDark = 0x8f877a, Bone = 0xcfc6b0, Stone = 0x4d4844;
        const int Scrap = 0x7a4128, ScrapDark = 0x5c3220, Oil = 0x2f2c2a, Paint = 0x5f6b5a;
        const int Bark = 0x2b2724, BarkLight = 0x3a3430, Ash = 0x6e6862, Moss = 0x7c8468;

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => MeshBuilder.Hex(hex);

        static PropModel Finish(MeshBuilder b, string name, float radius, float height,
            List<PropCollider> colliders = null, bool landmark = false)
        {
            return new PropModel
            {
                mesh = b.Build(name), footprint = radius, height = height, landmark = landmark,
                colliders = colliders ?? new List<PropCollider>()
            };
        }

        static Vector3 Yaw(Vector3 p, float a)
        {
            float s = Mathf.Sin(a), c = Mathf.Cos(a);
            return V(p.x * c + p.z * s, p.y, p.z * c - p.x * s);
        }

        static void Plate(MeshBuilder b, Vector3 p, Vector3 size, float yaw, float tilt, int hex)
        {
            b.Push(); b.Translate(p); b.RotateY(yaw); b.RotateZ(tilt);
            b.Box(Vector3.zero, size, b.Jitter(hex)); b.Pop();
        }

        static void BentRod(MeshBuilder b, Vector3 start, Vector3 bend, Vector3 end, float r, int hex)
        {
            b.Beam(start, bend, r, r * .9f, 4, C(hex));
            b.Beam(bend, end, r * .9f, r * .6f, 4, C(hex));
        }

        // A tapered square truss; each bay has four rails, four ties and four diagonals.
        // Endpoint rings may lean or lie horizontally, so the same helper builds wreckage.
        static void Truss(MeshBuilder b, Vector3 a, Vector3 end, float r0, float r1,
            int bays, float gauge, int hex)
        {
            Vector3 axis = (end - a).normalized;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 v = Vector3.Cross(axis, u).normalized;
            for (int k = 0; k < bays; k++)
            {
                float t0 = k / (float)bays, t1 = (k + 1f) / bays;
                Vector3 c0 = Vector3.Lerp(a, end, t0), c1 = Vector3.Lerp(a, end, t1);
                float w0 = Mathf.Lerp(r0, r1, t0), w1 = Mathf.Lerp(r0, r1, t1);
                for (int j = 0; j < 4; j++)
                {
                    float x = j < 2 ? -1 : 1, z = j == 0 || j == 3 ? -1 : 1;
                    int n = (j + 1) % 4;
                    float nx = n < 2 ? -1 : 1, nz = n == 0 || n == 3 ? -1 : 1;
                    Vector3 p = c0 + (u * x + v * z) * w0;
                    Vector3 q = c1 + (u * x + v * z) * w1;
                    Vector3 next = c1 + (u * nx + v * nz) * w1;
                    b.Beam(p, q, gauge, gauge, 4, b.Jitter(hex));
                    b.Beam(q, next, gauge * .7f, gauge * .7f, 4, C(hex));
                    b.Beam(p, next, gauge * .55f, gauge * .55f, 4, C(hex));
                }
            }
        }

        static void Cable(MeshBuilder b, Vector3 a, Vector3 end, float sag, float gauge, int steps = 8)
        {
            Vector3 last = a;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 p = Vector3.Lerp(a, end, t) - Vector3.up * (4 * sag * t * (1 - t));
                b.Beam(last, p, gauge, gauge, 3, C(Oil)); last = p;
            }
        }

        // Closed, chipped polygon extrusion, rather than a rectangular slab silhouette.
        static void Slab(MeshBuilder b, float width, float depth, float thickness, int hex)
        {
            const int n = 8;
            var top = new Vector3[n]; var bottom = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * Pi * 2 / n;
                float r = b.Range(.82f, 1);
                top[i] = V(Mathf.Sin(a) * width * .5f * r, thickness * .5f,
                    Mathf.Cos(a) * depth * .5f * r);
                bottom[i] = top[i] - Vector3.up * thickness;
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                b.Tri(V(0, thickness * .5f, 0), top[i], top[j], b.Jitter(hex));
                b.Tri(V(0, -thickness * .5f, 0), bottom[j], bottom[i], C(ConcreteDark));
                b.Quad(bottom[i], bottom[j], top[j], top[i], b.Jitter(hex));
            }
        }

        // A hollow pipe along local X. Separate inner faces and a jagged annular lip
        // preserve the dark opening without covering it with a cylinder cap.
        static void Tube(MeshBuilder b, float length, float radius, bool burst, int hex)
        {
            const int n = 10;
            var a = new Vector3[n]; var z = new Vector3[n];
            var ai = new Vector3[n]; var zi = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float t = i * Pi * 2 / n;
                Vector3 d = V(0, Mathf.Cos(t), Mathf.Sin(t));
                float flare = burst ? b.Range(1.05f, 1.4f) : 1;
                float x = length * .5f + (burst ? b.Range(-.4f, .35f) : 0);
                a[i] = V(-length * .5f, 0, 0) + d * radius;
                ai[i] = a[i] - d * .12f;
                z[i] = V(x, 0, 0) + d * radius * flare;
                zi[i] = z[i] - d * .12f;
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                b.Quad(a[i], a[j], z[j], z[i], b.Jitter(hex));
                b.Quad(ai[j], ai[i], zi[i], zi[j], C(Oil));
                b.Quad(z[i], z[j], zi[j], zi[i], C(Rust));
                b.Quad(a[j], a[i], ai[i], ai[j], C(Steel));
            }
        }

        // Raised rib arches remain open underneath. Each side can end at a different
        // angle, exposing broken tips. The feet are returned in the root frame.
        static void Ribs(MeshBuilder b, List<PropCollider> colliders, float length,
            float width, float height, int count, float gauge, float zOffset, int hex)
        {
            for (int i = 0; i < count; i++)
            {
                float z = zOffset + (i / (float)(count - 1) - .5f) * length;
                float taper = .75f + .25f * Mathf.Sin((i + 1f) / (count + 1) * Pi);
                for (int side = -1; side <= 1; side += 2)
                {
                    float reach = b.Range(1.15f, 1.58f);
                    Vector3 last = V(side * width * taper, -.12f, z);
                    colliders.Add(PropCollider.Circle(last.x, last.z, gauge * 1.65f));
                    for (int k = 1; k <= 4; k++)
                    {
                        float a = reach * k / 4;
                        Vector3 p = V(side * width * taper * Mathf.Cos(a),
                            -.12f + height * taper * Mathf.Sin(a), z + .16f * Mathf.Sin(a));
                        b.Beam(last, p, gauge, gauge * .86f, 5, b.Jitter(hex)); last = p;
                    }
                }
            }
        }

        // Window bays are empty geometry, not painted rectangles. The central ground
        // bay in the entrance wall has no sill, and its collider is split at the door.
        static void Wall(MeshBuilder b, List<PropCollider> colliders, Vector3 origin,
            float yaw, float length, float height, bool entrance)
        {
            const float thick = .38f;
            int bays = Mathf.Min(5, Mathf.Max(3, Mathf.FloorToInt(length / 1.9f)));
            if (bays % 2 == 0) bays++;
            float step = length / bays, pier = .36f;
            int floors = Mathf.Max(1, Mathf.FloorToInt(height / 2.6f));
            float storey = height / floors;
            b.Push(); b.Translate(origin); b.RotateY(yaw);
            for (int i = 0; i <= bays; i++)
            {
                float x = -length * .5f + i * step;
                float h = height - b.Range(0, .65f);
                b.Box(V(x, h * .5f, 0), V(pier, h, thick), b.Jitter(ConcreteDark));
                if (i % 2 == 0)
                    BentRod(b, V(x, h, 0), V(x + .08f, h + .45f, 0),
                        V(x + b.Range(-.4f, .4f), h + .62f, .16f), .027f, Rust);
            }
            for (int f = 0; f < floors; f++) for (int i = 0; i < bays; i++)
            {
                float x = -length * .5f + (i + .5f) * step, y = f * storey;
                bool door = entrance && f == 0 && i == bays / 2;
                if (!door)
                    b.Box(V(x, y + .43f, 0), V(step - pier, .86f, thick), b.Jitter(Concrete));
                float lintel = f == floors - 1 ? b.Range(.25f, .9f) : .38f;
                b.Box(V(x, y + storey - lintel * .5f, 0), V(step - pier, lintel, thick), b.Jitter(Concrete));
                if (!door && b.Chance(.5f))
                    b.Quad(V(x - .2f, y + .12f, thick * .51f), V(x - .17f, y + .12f, thick * .51f),
                        V(x + .15f, y + .7f, thick * .51f), V(x + .13f, y + .7f, thick * .51f), C(Char));
            }
            b.Pop();
            if (entrance)
            {
                float gap = step - pier, segment = (length + pier - gap) * .5f;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 p = origin + Yaw(V(sign * (gap + segment) * .5f, 0, 0), yaw);
                    colliders.Add(PropCollider.Rect(p.x, p.z, segment * .5f, thick * .5f, yaw));
                }
            }
            else colliders.Add(PropCollider.Rect(origin.x, origin.z, (length + pier) * .5f, thick * .5f, yaw));
        }

        public static PropModel RuinedBuilding(int seed)
        {
            // Low-rise two-storey shell with an open stairwell to the first floor.
            var p = RuinStructures.Building(seed, 2);
            p.mesh.name = "ruined-building";
            return p;
        }

        public static PropModel ConcreteSlab(int seed)
        {
            var b = new MeshBuilder(seed); var p = new PropModel();
            float run = b.Range(3, 6), width = b.Range(2, 3.6f);
            float yaw = b.Range(-.5f, .5f) + Pi * .5f, rise = run * b.Range(.12f, .32f);
            RuinStructures.Flight(b, p, 0, 0, width, run, 0, rise, yaw);
            b.Push(); b.RotateY(yaw);
            for (int i = 0; i < 6; i++)
            {
                float x = (i - 2.5f) * width / 7;
                BentRod(b, V(x, rise - .12f, run * .5f - .12f),
                    V(x, rise - .13f, run * .5f + .28f),
                    V(x + .08f, rise - .3f, run * .5f + .48f), .028f, Rust);
            }
            RuinStructures.Detail(b, seed ^ 307, () =>
            {
                // Dark aggregate seams and pale chipped scars follow the slope.
                for(int i=0;i<4;i++)
                {
                    float z=-run*.38f+i*run*.23f, y=rise*(z/run+.5f)+.018f;
                    b.Beam(V(-width*.35f,y,z),V(width*.22f,y,z),.012f,.008f,3,C(0x4c4945));
                }
            });
            b.Pop();
            p.mesh = b.Build("concrete-slab");
            p.footprint = Mathf.Sqrt(run * run + width * width) * .5f + .5f;
            p.height = rise;
            return p;
        }

        public static PropModel RubblePile(int seed)
        {
            // Irregular heap: 2-5 overlapping lumps (one walkable mound surface each, h <= .32 r keeps the
            // slope walkable) meshed as one jittered height field, then broken slabs, rocks, bricks and
            // rebar dropped on top. No circular outline, no regular facet grid.
            var b = new MeshBuilder(seed); var p = new PropModel();
            float main = b.Range(1.5f, 2.7f), along = b.Range(0, Mathf.PI * 2);
            var lumps = new List<Vector4> { new Vector4(0, 0, main, b.Range(.38f, Mathf.Min(1.05f, main * .32f))) };
            int n = b.RangeInt(2, 6);
            for (int i = 1; i < n; i++)
            {
                float a = along + (i % 2 == 0 ? Mathf.PI : 0) + b.Range(-.9f, .9f), d = main * b.Range(.5f, 1.05f);
                float r = main * b.Range(.4f, .78f);
                lumps.Add(new Vector4(Mathf.Sin(a) * d, Mathf.Cos(a) * d, r, Mathf.Min(r * .32f, b.Range(.18f, .7f))));
            }
            float H(float x, float z)
            {
                float best = 0;
                foreach (var l in lumps) { float dx = x - l.x, dz = z - l.y, q = (dx * dx + dz * dz) / (l.z * l.z); if (q < 1) best = Mathf.Max(best, l.w * (1 - q)); }
                return best;
            }
            float R = 0; foreach (var l in lumps) R = Mathf.Max(R, new Vector2(l.x, l.y).magnitude + l.z);
            float ox = b.Range(0, 50), oz = b.Range(0, 50);
            const float STEP = .42f; int cells = Mathf.CeilToInt(2 * R / STEP);
            var vtx = new Vector3[cells + 1, cells + 1];
            for (int i = 0; i <= cells; i++) for (int j = 0; j <= cells; j++)
            {
                float x = -R + i * STEP + b.Range(-.15f, .15f), z = -R + j * STEP + b.Range(-.15f, .15f), h = H(x, z);
                float rough = (Mathf.PerlinNoise(x * 1.7f + ox, z * 1.7f + oz) - .5f) * .16f * Mathf.Min(1, h / .15f);
                vtx[i, j] = V(x, h > .005f ? h + rough - .02f : -.03f, z);
            }
            for (int i = 0; i < cells; i++) for (int j = 0; j < cells; j++)
            {
                Vector3 a = vtx[i, j], c1 = vtx[i + 1, j], c2 = vtx[i + 1, j + 1], c3 = vtx[i, j + 1];
                if (Mathf.Max(Mathf.Max(a.y, c1.y), Mathf.Max(c2.y, c3.y)) < .02f) continue;
                for (int t = 0; t < 2; t++)
                {
                    Vector3 q0 = a, q1 = t == 0 ? c2 : c3, q2 = t == 0 ? c1 : c2;
                    float y = (q0.y + q1.y + q2.y) / 3;
                    int hex = b.Chance(.1f) ? 0x3b3835 : b.Chance(.12f) ? 0x8a857c : b.Chance(.06f) ? 0x6e4a33 : 0x6a6762;
                    var col = b.Jitter(hex, .12f) * (.78f + .3f * Mathf.Clamp01(y / .6f));   // sooty at the foot, paler on top
                    b.Tri(q0, q1, q2, col);
                }
            }
            // debris lying on the heap
            int pieces = Mathf.RoundToInt(R * R * 1.6f) + 4;
            for (int k = 0; k < pieces; k++)
            {
                var l = lumps[b.RangeInt(0, lumps.Count)]; float a = b.Range(0, Mathf.PI * 2), d = l.z * Mathf.Sqrt(b.Rand()) * .85f;
                float x = l.x + Mathf.Sin(a) * d, z = l.y + Mathf.Cos(a) * d, y = H(x, z);
                float kind = b.Rand();
                if (kind < .38f)
                {   // broken slab fragment, tilted
                    b.Push(); b.Translate(x, y + .04f, z); b.RotateY(b.Range(0, Mathf.PI * 2)); b.RotateX(b.Range(-.45f, .45f)); b.RotateZ(b.Range(-.3f, .3f));
                    b.Box(Vector3.zero, V(b.Range(.35f, 1.1f), b.Range(.07f, .16f), b.Range(.25f, .8f)), b.Jitter(b.Chance(.3f) ? 0x8a857c : 0x77726b, .1f), .1f);
                    b.Pop();
                }
                else if (kind < .78f) b.Rock(V(x, y + .03f, z), b.Range(.1f, .32f), b.Jitter(b.Chance(.25f) ? 0x4a4642 : 0x625e58, .1f), .35f, b.Range(.55f, 1f));
                else if (kind < .92f)
                {   // brick / block
                    b.Push(); b.Translate(x, y + .05f, z); b.RotateY(b.Range(0, Mathf.PI)); b.RotateX(b.Range(-.3f, .3f));
                    b.Box(Vector3.zero, V(.22f, .1f, .11f), b.Jitter(b.Chance(.5f) ? 0x7a4c3a : 0x5f5a54, .12f), .1f); b.Pop();
                }
                else
                {   // bent rebar sticking out
                    var s = V(x, y, z); var m = s + V(b.Range(-.3f, .3f), b.Range(.25f, .6f), b.Range(-.3f, .3f));
                    b.Beam(s, m, .018f, .018f, 4, b.Jitter(0x5a3a28, .1f)); b.Beam(m, m + V(b.Range(-.35f, .35f), b.Range(-.05f, .2f), b.Range(-.35f, .35f)), .016f, .016f, 4, b.Jitter(0x5a3a28, .1f));
                }
            }
            foreach (var l in lumps) p.surfaces.Add(PropSurface.Mound(l.x, l.y, l.z, l.w));
            float top = 0; foreach (var l in lumps) top = Mathf.Max(top, l.w);
            p.mesh = b.Build("rubble-pile"); p.footprint = R; p.height = top + .3f;
            return p;
        }

        public static PropModel BurntCar(int seed)
        {
            var b = new MeshBuilder(seed); float length = b.Range(3.7f, 4.4f), crush = b.Range(.72f, 1.12f);
            float yaw = b.Range(-.18f, .18f);
            b.Push(); b.RotateY(yaw);
            b.Box(V(0, .28f, 0), V(1.65f, .34f, length), C(Char));
            Plate(b, V(0, .59f, length * .32f), V(1.62f, .16f, length * .32f), .03f, b.Range(-.12f, .1f), Rust);
            Plate(b, V(0, .55f, -length * .36f), V(1.6f, .19f, length * .25f), -.08f, -.07f, Steel);
            // Dark interior, open windscreen and irregular skeletal roof pillars.
            b.Box(V(0, .46f, -.15f), V(1.35f, .12f, 1.65f), C(Oil));
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(V(side * .79f, .59f, -.1f), V(.13f, .4f, 1.85f), b.Jitter(Steel));
                b.Beam(V(side * .75f, .65f, .85f), V(side * .53f, crush, .36f), .055f, .045f, 4, C(Rust));
                b.Beam(V(side * .76f, .64f, -.98f), V(side * .6f, crush + .15f, -.62f), .055f, .04f, 4, C(Steel));
                // Empty axle ends; intentionally no wheels.
                for (int j = -1; j <= 1; j += 2)
                    b.Beam(V(side * .62f, .2f, j * length * .32f), V(side * .96f, .17f, j * length * .32f), .075f, .06f, 5, C(Oil));
            }
            // Keep the original Plate's random draws; replace its intact roof box
            // with two buckled sheets exposing the burned cabin below.
            b.Jitter(Char);
            for(int i=0;i<6;i++) b.Range(-.06f,.06f);
            b.Quad(V(-.58f,crush+.12f,-.7f),V(-.58f,crush,.45f),
                V(.05f,crush-.18f,.32f),V(.02f,crush-.16f,-.65f),C(Char));
            b.Quad(V(.02f,crush-.16f,-.65f),V(.05f,crush-.18f,.32f),
                V(.68f,crush+.02f,.45f),V(.62f,crush+.1f,-.7f),C(Rust));
            Plate(b, V(-.95f, .48f, .1f), V(.09f, .55f, .95f), -.65f, .12f, Rust);
            for (int i = 0; i < 5; i++) b.Box(V((i - 2) * .22f, .42f, length * .5f + .025f), V(.08f, .22f, .06f), C(Steel));
            RuinStructures.Detail(b, seed ^ 311, () =>
            {
                // Burned seat pans and a split dashboard remain inside the shell.
                for(int side=-1;side<=1;side+=2)
                {
                    b.Box(V(side*.36f,.57f,-.4f),V(.5f,.12f,.55f),C(0x363a3b));
                    b.Box(V(side*.8f,.64f,-.1f),V(.015f,.12f,.5f),C(0x5e7470));
                    b.Tri(V(side*.54f,.73f,.72f),V(side*.48f,crush-.05f,.42f),
                        V(side*.24f,.74f,.7f),C(0x263b3e));
                    b.Box(V(side*.55f,.51f,length*.497f),V(.27f,.11f,.026f),C(0x8f8b86));
                }
                b.Beam(V(-.65f,.63f,.65f),V(.65f,.63f,.65f),.07f,.055f,5,C(Oil));
                b.Box(V(0,.3f,-length*.5f),V(1.42f,.1f,.08f),C(0x7d7a75));
                for(int i=0;i<4;i++)
                    b.Box(V(-.48f+i*.3f,.691f,length*.32f),V(.08f,.008f,.32f),C(0x252728));
            });
            b.Pop();
            Vector3 colliderCenter = Yaw(V(-.12f, 0, 0), yaw);
            return Finish(b, "burnt-car", length * .62f, crush + .3f,
                new List<PropCollider> { PropCollider.Rect(colliderCenter.x, colliderCenter.z, 1.22f, length * .5f, yaw) });
        }

        public static PropModel StreetLamp(int seed)
        {
            var b = new MeshBuilder(seed); float h = b.Range(4, 5), lean = b.Range(-.65f, .65f);
            b.Cylinder(Vector3.zero, .23f, .17f, .32f, 6, C(Steel));
            Vector3 knee = V(lean * .35f, h * .65f, .08f), top = V(lean, h - .3f, .2f);
            BentRod(b, V(0, .22f, 0), knee, top, .085f, Steel);
            Vector3 arm = top + V(.8f, -.12f, .15f);
            b.Beam(top, arm, .07f, .05f, 5, C(Rust));
            Plate(b, arm + V(.15f, -.1f, 0), V(.65f, .18f, .32f), .1f, -.18f, Char);
            b.Box(arm + V(.15f, -.205f, 0), V(.45f, .025f, .21f), C(ConcreteDark));
            Cable(b, top, knee + V(.45f, -.5f, .05f), .35f, .015f, 5);
            for (int i = 0; i < 4; i++)
                b.Cylinder(V((i < 2 ? -1 : 1) * .14f, .31f, (i % 2 == 0 ? -1 : 1) * .14f), .035f, .035f, .045f, 4, C(Rust));
            RuinStructures.Detail(b, seed ^ 313, () =>
            {
                b.Box(V(.08f,.65f,-.045f),V(.06f,.23f,.11f),C(Oil));
                BentRod(b,V(.12f,.65f,-.05f),V(.24f,.46f,-.08f),V(.1f,.38f,-.04f),.012f,Rust);
                // Torn maintenance sticker and chipped paint on the lower pole.
                b.Box(V(.03f,1.1f,-.071f),V(.09f,.17f,.012f),C(0x5e7470));
                b.Beam(arm+V(.35f,-.18f,0),arm+V(.4f,-.48f,.05f),.012f,.01f,3,C(Oil));
                Plate(b,arm+V(.4f,-.5f,.05f),V(.24f,.025f,.17f),.3f,.5f,0x7d7a75);
            });
            return Finish(b, "street-lamp", 2.1f, h, new List<PropCollider> { PropCollider.Circle(0, 0, .26f) });
        }

        public static PropModel RadioTower(int seed)
        {
            var b = new MeshBuilder(seed); var colliders = new List<PropCollider>();
            float h = b.Range(26.5f, 29), baseWidth = b.Range(2.2f, 2.8f);
            Vector3 snap = V(b.Range(-.65f, .65f), h - 2, b.Range(-.4f, .4f));
            Truss(b, V(0, .4f, 0), snap, baseWidth, .45f, 10, .105f, Steel);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            {
                b.Box(V(x * baseWidth, .18f, z * baseWidth), V(.9f, .55f, .9f), C(ConcreteDark));
                colliders.Add(PropCollider.Circle(x * baseWidth, z * baseWidth, .64f));
            }
            Vector3 end = snap + V(b.Range(2.8f, 4.3f), -6.2f, .7f);
            Truss(b, snap, end, .45f, .17f, 3, .065f, Rust);
            BentRod(b, snap, snap + V(.15f, 1.3f, 0), snap + V(.7f, 1.65f, .1f), .06f, Steel);
            Cable(b, snap + V(-.5f, -.2f, 0), end, 2, .025f);
            for (int i = 0; i < 3; i++)
            {
                float y = h * (.43f + i * .16f);
                b.Beam(V(-1, y, 0), V(1.5f, y, 0), .075f, .075f, 4, C(Rust));
                b.Box(V(1.45f, y + .6f, 0), V(.28f, 1.3f, .5f), b.Jitter(ConcreteDark));
            }
            // Ladder on the rear face, with irregular missing rungs.
            for (int side = -1; side <= 1; side += 2)
                b.Beam(V(side * .24f, 1, -baseWidth), snap + V(side * .24f, -.5f, -.5f), .028f, .028f, 4, C(Rust));
            for (int i = 0; i < 31; i++) if (b.Chance(.85f))
            {
                float t = i / 31f;
                Vector3 p = Vector3.Lerp(V(0, 1, -baseWidth), snap + V(0, -.5f, -.5f), t);
                b.Beam(p - V(.25f, 0, 0), p + V(.25f, 0, 0), .025f, .025f, 4, C(Rust));
            }
            return Finish(b, "radio-tower", 5.7f, h, colliders, true);
        }

        public static PropModel Dune(int seed)
        {
            var b = new MeshBuilder(seed); var p = new PropModel();
            float r = b.Range(3, 6.8f), h = b.Range(.65f, Mathf.Min(1.35f, r * .34f));
            RuinStructures.Mound(b, p, r, h, Sand, 12, 48);
            p.mesh = b.Build("ash-dune"); p.footprint = r; p.height = h;
            return p;
        }

        public static PropModel RockSpire(int seed)
        {
            var b = new MeshBuilder(seed); float h = b.Range(4, 9), radius = b.Range(.7f, 1.25f);
            float drift = b.Range(-.65f, .65f);
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f, r = radius * Mathf.Lerp(1.1f, .43f, t);
                b.Rock(V(drift * t, h * (.09f + .19f * i), .12f * Mathf.Sin(i)), r,
                    b.Jitter(i % 2 == 0 ? Stone : SandDark), .18f, h * .125f / r, 1);
                b.Push(); b.Translate(drift * t, h * (.1f + .19f * i), 0); b.RotateY(b.Range(-.3f, .3f));
                Slab(b, r * 2.35f, r * 1.8f, .17f, SandDark); b.Pop();
            }
            return Finish(b, "rock-spire", radius * 1.5f + .35f, h,
                new List<PropCollider> { PropCollider.Circle(0, 0, radius * 1.15f) });
        }

        public static PropModel Pylon(int seed)
        {
            var b = new MeshBuilder(seed); var colliders = new List<PropCollider>();
            float h = b.Range(10, 14), lean = b.Range(1.5f, 3.6f), foot = b.Range(1.25f, 1.65f);
            Vector3 crown = V(lean, h - .7f, b.Range(-.5f, .5f));
            Truss(b, V(0, .2f, 0), crown, foot, .35f, 6, .09f, Steel);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            {
                b.Box(V(x * foot, .12f, z * foot), V(.65f, .38f, .65f), C(SandDark));
                colliders.Add(PropCollider.Circle(x * foot, z * foot, .46f));
            }
            for (int j = 0; j < 2; j++)
            {
                Vector3 p = crown - V(lean * j * .14f, j * 2.4f, 0);
                b.Beam(p - V(2.6f, 0, 0), p + V(2.6f, .2f, 0), .1f, .1f, 4, C(Rust));
                b.Beam(p - V(2.6f, 0, 0), p - V(0, 1.3f, 0), .06f, .06f, 4, C(Steel));
                b.Beam(p + V(2.6f, .2f, 0), p - V(0, 1.3f, 0), .06f, .06f, 4, C(Steel));
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 tip = p + V(sign * 2.35f, -.45f, 0);
                    b.Cylinder(tip, .15f, .15f, .55f, 6, C(Bone));
                    for (int k = 0; k < 3; k++) b.Disc(tip + V(0, .1f + k * .16f, 0), .05f, .23f, 6, C(SandDark));
                    Cable(b, tip, V(sign * b.Range(4, 6), .1f, 3.5f + j), 1.1f, .025f);
                }
            }
            return Finish(b, "leaning-pylon", 8, h, colliders);
        }

        public static PropModel BoneRibs(int seed)
        {
            var b = new MeshBuilder(seed); var colliders = new List<PropCollider>();
            float length = b.Range(4, 6), width = b.Range(1.1f, 1.65f), h = b.Range(1.7f, 2.6f);
            Ribs(b, colliders, length, width, h, b.RangeInt(5, 8), .12f, 0, Bone);
            b.Beam(V(0, .12f, -length * .55f), V(.12f, .2f, length * .55f), .2f, .15f, 6, C(Steel));
            for (int i = 0; i < 9; i++)
                b.Box(V(.04f, .2f, (i - 4) * length / 8), V(.52f, .25f, .24f), b.Jitter(SandDark));
            return Finish(b, "mechanical-ribs", length * .62f + .15f, h, colliders);
        }

        public static PropModel GiantRemains(int seed)
        {
            var b = new MeshBuilder(seed); var colliders = new List<PropCollider>();
            float s = b.Range(.94f, 1.06f), ribWidth = b.Range(3, 3.6f);
            // Body lies along Z: feet at -15, head at +12. The outstretched arm
            // introduces an unmistakably asymmetric silhouette in the overhead view.
            Ribs(b, colliders, 10 * s, ribWidth, 4.5f * s, 8, .29f, 2 * s, Bone);
            b.Beam(V(0, .55f, -7 * s), V(0, .8f, 8 * s), .65f, .5f, 8, C(Steel));
            for (int i = 0; i < 11; i++)
            {
                float z = (-6 + i * 1.3f) * s;
                b.Box(V(0, .65f, z), V(1.6f, .7f, .55f), b.Jitter(SandDark));
                colliders.Add(PropCollider.Circle(0, z, .85f));
            }
            // Skull armor surrounds two recessed top-facing sockets, never fills them.
            Vector3 skull = V(.25f, .2f, 11 * s);
            b.Push(); b.Translate(skull); b.RotateY(b.Range(-.15f, .15f));
            b.Box(V(0, .5f, 0), V(4.4f, 1.3f, 4.2f), C(Oil));
            b.Push(); b.Translate(0, 1.25f, 1.6f); b.Scale(1, 1, .45f);
            b.Rock(Vector3.zero, 2.2f, C(Bone), .12f, .65f, 1); b.Pop();
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Plate(b, V(sign * 1.95f, 1.45f, -.45f), V(.65f, 1.2f, 2.7f), sign * .12f, sign * .18f, Bone);
                b.Box(V(sign * .96f, 1.19f, -.55f), V(1.15f, .04f, 1.4f), C(Char));
                Plate(b, V(sign * .98f, 2.02f, .25f), V(1.6f, .35f, .45f), sign * -.13f, 0, SandDark);
            }
            b.Box(V(0, 1.55f, -.7f), V(.4f, 1, 1.65f), C(Bone));
            Plate(b, V(.1f, 1.1f, -2.05f), V(3.6f, .55f, .6f), .08f, .08f, Bone);
            for (int i = 0; i < 5; i++) b.Box(V((i - 2) * .5f, 1.47f, -2.05f), V(.18f, .24f, .32f), C(Steel));
            b.Pop(); colliders.Add(PropCollider.Rect(skull.x, skull.z, 2.5f, 2.6f));
            b.Rock(V(0, .5f, -5.7f * s), 2.3f, C(ScrapDark), .18f, .55f, 1);
            colliders.Add(PropCollider.Circle(0, -5.7f * s, 2.25f));
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 hip = V(sign * 1.4f, .6f, -6 * s), knee = V(sign * 2, .7f, -10 * s);
                Vector3 ankle = V(sign * 2.5f, .3f, -13.7f * s);
                b.Beam(hip, knee, .85f, .65f, 7, C(SandDark));
                b.Beam(knee, ankle, .65f, .42f, 7, C(Bone));
                b.Box(ankle + V(0, -.05f, -.65f), V(1.25f, .65f, 2), C(Steel));
                colliders.Add(PropCollider.Rect(sign * 2, -10 * s, 1.05f, 4.9f * s));
            }
            Vector3 shoulder = V(3 * s, 1.3f, 5 * s), elbow = V(7 * s, .7f, 3.5f * s), hand = V(11 * s, .4f, 5 * s);
            b.Beam(shoulder, elbow, .85f, .6f, 8, C(Bone));
            b.Rock(elbow, .9f, C(Steel), .12f, 1, 1);
            b.Beam(elbow, hand, .6f, .4f, 8, C(SandDark));
            b.Box(hand, V(1.9f, .6f, 1.6f), C(Bone));
            for (int i = 0; i < 4; i++)
            {
                Vector3 start = hand + V(.65f, .03f, (i - 1.5f) * .4f);
                BentRod(b, start, start + V(.9f, .07f, (i - 1.5f) * .16f), start + V(1.4f, -.23f, (i - 1.5f) * .26f), .15f, Bone);
            }
            BentRod(b, hand + V(-.3f, 0, -.7f), hand + V(.1f, .1f, -1.3f), hand + V(.8f, -.1f, -1.5f), .2f, Bone);
            for (int i = 0; i <= 5; i++)
            {
                Vector3 p = i < 3 ? Vector3.Lerp(shoulder, elbow, i / 2f) : Vector3.Lerp(elbow, hand, (i - 2) / 3f);
                colliders.Add(PropCollider.Circle(p.x, p.z, i == 5 ? 1.25f : .85f));
            }
            return Finish(b, "giant-remains", 17 * s, 5 * s, colliders, true);
        }

        public static PropModel ScrapHeap(int seed)
        {
            var b = new MeshBuilder(seed); float r = b.Range(1.5f, 3.5f);
            b.Rock(V(0, .35f, 0), r * .74f, C(Oil), .2f, .65f, 1);
            int count = b.RangeInt(16, 25);
            for (int i = 0; i < count; i++)
            {
                float a = b.Range(0, 2 * Pi), reach = b.Range(0, r * .8f);
                Vector3 p = V(Mathf.Sin(a) * reach, .3f + (1 - reach / r) * r * .6f, Mathf.Cos(a) * reach);
                Plate(b, p, V(b.Range(.5f, 1.3f), .09f, b.Range(.3f, 1)), a, b.Range(-.8f, .8f), i % 4 == 0 ? Paint : Scrap);
                if (i % 4 == 0) b.Beam(p, p + V(.65f, .3f, -.2f), .07f, .07f, 4, C(Steel));
            }
            for (int i = 0; i < 3; i++)
            {
                b.Push(); b.Translate(b.Range(-r * .6f, r * .6f), .6f, b.Range(-r * .6f, r * .6f));
                b.RotateZ(b.Range(.15f, .8f)); float wheel = b.Range(.35f, .65f);
                b.Disc(Vector3.zero, wheel * .55f, wheel, 12, C(Steel));
                b.Disc(V(0, -.08f, 0), wheel * .55f, wheel, 12, C(ScrapDark), 2 * Pi, 0);
                for (int j = 0; j < 8; j++)
                {
                    float a = j * Pi * .25f;
                    b.Beam(Vector3.zero, V(Mathf.Sin(a) * wheel, 0, Mathf.Cos(a) * wheel), .035f, .035f, 4, C(ScrapDark));
                    Plate(b, V(Mathf.Sin(a) * wheel, 0, Mathf.Cos(a) * wheel), V(.16f, .13f, .18f), a, 0, Scrap);
                }
                b.Pop();
            }
            return Finish(b, "scrap-heap", r + .7f, r * .8f + .8f,
                new List<PropCollider> { PropCollider.Circle(0, 0, r) });
        }

        public static PropModel CanyonWall(int seed)
        {
            var b = new MeshBuilder(seed); var colliders = new List<PropCollider>();
            float length = b.Range(14, 22), h = b.Range(5, 9), depth = b.Range(2, 4);
            const int columns = 7;
            float step = length / columns;
            for (int i = 0; i < columns; i++)
            {
                float x = (i - 3) * step, top = h * b.Range(.72f, 1), z = b.Range(-.25f, .25f);
                for (int j = 0; j < 4; j++)
                {
                    b.Push(); b.Translate(x + b.Range(-.14f, .14f), top * (j + .5f) / 4 - .12f, z);
                    b.Scale(step * .66f, top * .17f, depth * .55f);
                    b.Rock(Vector3.zero, 1, b.Jitter(j % 2 == 0 ? Stone : ScrapDark), .16f, 1, j % 2); b.Pop();
                    b.Box(V(x, top * j / 4 + .15f, z), V(step * .95f, .075f, depth * .92f), C(Oil));
                }
                colliders.Add(PropCollider.Rect(x, z, step * .57f, depth * .56f));
            }
            return Finish(b, "canyon-wall", length * .56f, h + .5f, colliders);
        }

        static void Container(MeshBuilder b, Vector3 p, float yaw, int hex, bool open)
        {
            b.Push(); b.Translate(p); b.RotateY(yaw);
            // Six metre long X axis. Walls are ribbed panels with selected ribs buckled.
            b.Box(V(0, .12f, 0), V(6, .24f, 2.4f), C(Oil));
            b.Box(V(0, 2.48f, 0), V(6, .16f, 2.4f), b.Jitter(hex));
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(V(0, 1.3f, side * 1.14f), V(6, 2.4f, .1f), b.Jitter(hex));
                for (int i = 0; i < 13; i++)
                {
                    float x = (i - 6) * .45f, dent = b.Chance(.22f) ? b.Range(-.16f, -.06f) : 0;
                    Vector3 low = V(x, .24f, side * 1.22f);
                    Vector3 mid = V(x + dent, 1.4f, side * (1.22f + dent));
                    Vector3 top = V(x, 2.35f, side * 1.22f), edge = V(side * .04f, 0, 0);
                    Color rib = C(i % 3 == 0 ? ScrapDark : hex);
                    b.Quad(low - edge, low + edge, mid + edge, mid - edge, rib);
                    b.Quad(mid - edge, mid + edge, top + edge, top - edge, rib);
                }
                for (int end = -1; end <= 1; end += 2)
                    b.Box(V(end * 2.93f, 1.3f, side * 1.14f), V(.14f, 2.6f, .14f), C(ScrapDark));
            }
            b.Box(V(-2.94f, 1.3f, 0), V(.12f, 2.4f, 2.2f), C(ScrapDark));
            if (!open)
            {
                b.Box(V(2.94f, 1.3f, 0), V(.12f, 2.4f, 2.2f), C(hex));
                for (int side = -1; side <= 1; side += 2)
                    b.Beam(V(3.025f, .3f, side * .55f), V(3.025f, 2.3f, side * .55f), .025f, .025f, 4, C(Steel));
            }
            else
            {
                // Door folded inward: no uncollided obstacle outside the footprint.
                Plate(b, V(2.45f, 1.2f, .62f), V(.12f, 2.25f, 1.05f), -.8f, 0, Scrap);
                b.Box(V(1.2f, .5f, -.4f), V(1.2f, .7f, .8f), C(Oil));
            }
            b.Box(V(.4f, 1.65f, -1.205f), V(1.25f, .38f, .025f), C(SandDark));
            for (int i = 0; i < 3; i++) Plate(b, V(b.Range(-2, 2), 2.59f, b.Range(-.7f, .7f)), V(.7f, .035f, .35f), b.Range(-1, 1), 0, ScrapDark);
            b.Pop();
        }

        public static PropModel ContainerStack(int seed)
        {
            var b = new MeshBuilder(seed); var colliders = new List<PropCollider>();
            int count = b.RangeInt(1, 4); float yaw = b.Range(-.22f, .22f);
            Container(b, Vector3.zero, yaw, Scrap, b.Chance(.5f));
            colliders.Add(PropCollider.Rect(0, 0, 3.08f, 1.27f, yaw));
            for (int i = 1; i < count; i++)
                Container(b, V(b.Range(-.45f, .45f), i * 2.6f, b.Range(-.18f, .18f)),
                    yaw + b.Range(-.07f, .07f), i == 1 ? Paint : ScrapDark, false);
            return Finish(b, "container-stack", 4, count * 2.6f + .06f, colliders);
        }

        public static PropModel PipeRun(int seed)
        {
            var b = new MeshBuilder(seed); float length = b.Range(8, 12), radius = b.Range(.55f, .85f);
            b.Push(); b.Translate(-length * .12f, 1.1f, 0); Tube(b, length * .72f, radius, true, Scrap); b.Pop();
            b.Push(); b.Translate(length * .4f, .7f, .2f); b.RotateZ(-.2f);
            Tube(b, length * .18f, radius, false, ScrapDark); b.Pop();
            for (int i = -1; i <= 1; i++)
            {
                float x = i * length * .32f;
                b.Box(V(x, .32f, 0), V(.55f, .65f, radius * 2 + .45f), C(ConcreteDark));
                b.Push(); b.Translate(x, 1.1f, 0); b.RotateZ(Pi * .5f);
                b.Cylinder(V(0, -.1f, 0), radius + .08f, radius + .08f, .2f, 10, C(Steel), false);
                b.Disc(V(0, -.1f, 0), radius, radius + .16f, 10, C(ScrapDark), 2 * Pi, 0);
                b.Disc(V(0, .1f, 0), radius, radius + .16f, 10, C(ScrapDark)); b.Pop();
            }
            for (int i = 0; i < 4; i++)
                Plate(b, V(length * .25f + b.Range(-.4f, .5f), .13f, b.Range(-1.2f, 1.2f)), V(.5f, .08f, .35f), b.Range(-Pi, Pi), .2f, Scrap);
            return Finish(b, "broken-pipe-run", length * .55f + .4f, 2.35f,
                new List<PropCollider> { PropCollider.Rect(0, 0, length * .5f + .35f, radius + .45f) });
        }

        public static PropModel CraneWreck(int seed)
        {
            var b = new MeshBuilder(seed); var colliders = new List<PropCollider>();
            float length = b.Range(23, 27), lift = b.Range(2, 3.2f);
            Vector3 a = V(-length * .46f, 1.05f, 0), end = V(length * .46f, lift, .7f);
            Truss(b, a, end, .85f, .48f, 12, .13f, Scrap);
            // Broad broken gantry legs splay to either side of the fallen boom.
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 foot = V(-length * .32f, .45f, sign * 4.2f);
                Vector3 joint = V(-length * .22f, 1.1f, 0);
                Truss(b, foot, joint, .36f, .45f, 2, .11f, ScrapDark);
                b.Box(foot, V(2.2f, .65f, .9f), C(Oil));
                colliders.Add(PropCollider.Rect(foot.x, foot.z, 1.15f, .5f));
                // Circles along angled leg members stay in the root frame.
                for (int i = 1; i <= 3; i++)
                {
                    Vector3 p = Vector3.Lerp(foot, joint, i / 4f);
                    colliders.Add(PropCollider.Circle(p.x, p.z, .65f));
                }
            }
            Vector3 cab = V(-length * .3f, .9f, -1.9f);
            b.Push(); b.Translate(cab); b.RotateZ(.22f);
            b.Box(Vector3.zero, V(2.5f, 1.6f, 2), C(ScrapDark));
            b.Box(V(0, .5f, -1.015f), V(1.8f, .5f, .035f), C(Oil));
            b.Box(V(0, .82f, 0), V(2.7f, .12f, 2.2f), C(Paint));
            for (int i = 0; i < 4; i++) b.Box(V(1.27f, -.3f + i * .18f, 0), V(.04f, .07f, .9f), C(Steel));
            b.Pop(); colliders.Add(PropCollider.Rect(cab.x, cab.z, 1.5f, 1.15f));
            Cable(b, end, V(length * .12f, .18f, 2.3f), .6f, .04f);
            BentRod(b, V(length * .12f, .3f, 2.3f), V(length * .12f + .35f, .25f, 2.65f),
                V(length * .12f + .6f, .3f, 2.3f), .12f, Steel);
            // The boom rests low enough to obstruct walking, so it also has collision.
            colliders.Add(PropCollider.Rect(0, .35f, length * .48f, .95f, -Mathf.Atan2(.7f, length * .92f)));
            return Finish(b, "crane-wreck", length * .54f, lift + .9f, colliders, true);
        }

        public static PropModel DeadTree(int seed)
        {
            var b = new MeshBuilder(seed); float h = b.Range(4, 9), radius = b.Range(.3f, .55f);
            Vector3 middle = V(b.Range(-.35f, .35f), h * .45f, b.Range(-.25f, .25f));
            Vector3 crown = middle + V(b.Range(-.7f, .7f), h * .48f, b.Range(-.45f, .45f));
            b.Beam(V(0, -.15f, 0), middle, radius, radius * .65f, 7, C(Bark));
            b.Beam(middle, crown, radius * .65f, .075f, 6, C(BarkLight));
            int count = b.RangeInt(3, 7);
            for (int i = 0; i < count; i++)
            {
                float a = i * 2.4f + b.Range(-.4f, .4f), reach = b.Range(1, 2.3f);
                Vector3 start = Vector3.Lerp(middle, crown, b.Range(-.2f, .7f));
                Vector3 bend = start + V(Mathf.Sin(a) * reach, b.Range(.3f, 1), Mathf.Cos(a) * reach);
                Vector3 tip = bend + V(Mathf.Sin(a + .7f) * .65f, b.Range(.3f, 1.1f), Mathf.Cos(a + .7f) * .65f);
                b.Beam(start, bend, radius * .38f, .085f, 5, C(Bark));
                b.Beam(bend, tip, .085f, .018f, 4, C(BarkLight));
                if (b.Chance(.6f)) b.Beam(bend, bend + V(-.4f, .7f, .3f), .055f, .012f, 4, C(Ash));
            }
            for (int i = 0; i < 5; i++)
            {
                float a = i * 2 * Pi / 5;
                b.Beam(V(0, .3f, 0), V(Mathf.Sin(a) * radius * 2.2f, .02f, Mathf.Cos(a) * radius * 2.2f), .16f, .025f, 5, C(Bark));
            }
            return Finish(b, "dead-tree", 3.4f, h + 1.1f,
                new List<PropCollider> { PropCollider.Circle(0, 0, radius * 1.25f) });
        }

        // Thick hollow trunk with unequal rim heights and a dark recessed interior.
        // Omit sectors at the front when an open arch is requested for the great tree.
        static void HollowTrunk(MeshBuilder b, float radius, float height, int segments, bool arch)
        {
            var outer = new Vector3[segments]; var inner = new Vector3[segments];
            var rim = new Vector3[segments]; var insideRim = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i * 2 * Pi / segments;
                Vector3 d = V(Mathf.Sin(a), 0, Mathf.Cos(a));
                float r = radius * b.Range(.9f, 1.08f), top = height * b.Range(.78f, 1);
                outer[i] = d * r - V(0, .12f, 0); inner[i] = d * r * .62f;
                rim[i] = d * r * .73f + V(.12f, top, 0);
                insideRim[i] = d * r * .53f + V(.12f, top, 0);
            }
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                bool doorway = arch && (i == 0 || i == segments - 1);
                Vector3 lo = doorway ? Vector3.Lerp(outer[i], rim[i], .42f) : outer[i];
                Vector3 next = doorway ? Vector3.Lerp(outer[j], rim[j], .42f) : outer[j];
                Vector3 li = doorway ? Vector3.Lerp(inner[i], insideRim[i], .42f) : inner[i];
                Vector3 ni = doorway ? Vector3.Lerp(inner[j], insideRim[j], .42f) : inner[j];
                b.Quad(lo, next, rim[j], rim[i], b.Jitter(i % 3 == 0 ? BarkLight : Bark));
                b.Quad(ni, li, insideRim[i], insideRim[j], C(Char));
                b.Quad(rim[i], rim[j], insideRim[j], insideRim[i], C(Ash));
                if (doorway) b.Quad(next, lo, li, ni, C(BarkLight));
                if (arch && (i == 1 || i == segments - 1))
                {
                    Vector3 cut = Vector3.Lerp(outer[i], rim[i], .42f);
                    Vector3 cutInner = Vector3.Lerp(inner[i], insideRim[i], .42f);
                    if (i == 1) b.Quad(outer[i], cut, cutInner, inner[i], C(BarkLight));
                    else b.Quad(inner[i], cutInner, cut, outer[i], C(BarkLight));
                }
            }
            b.Disc(V(0, .01f, 0), 0, radius * .59f, segments, C(Char));
        }

        public static PropModel Stump(int seed)
        {
            var b = new MeshBuilder(seed); float h = b.Range(.8f, 1.5f), r = b.Range(.45f, .8f);
            HollowTrunk(b, r, h, 9, false);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 2 * Pi / 5;
                b.Beam(V(Mathf.Sin(a) * r * .6f, .3f, Mathf.Cos(a) * r * .6f),
                    V(Mathf.Sin(a) * r * 1.8f, -.03f, Mathf.Cos(a) * r * 1.8f), .17f, .035f, 5, C(Bark));
            }
            b.Rock(V(r * .55f, .15f, -.25f), .3f, C(Moss), .25f, .25f, 0);
            return Finish(b, "broken-stump", r * 1.9f, h, new List<PropCollider> { PropCollider.Circle(0, 0, r * 1.1f) });
        }

        public static PropModel FallenLog(int seed)
        {
            var b = new MeshBuilder(seed); float length = b.Range(5, 8), r = b.Range(.4f, .7f), yaw = b.Range(-.35f, .35f);
            b.Push(); b.RotateY(yaw);
            Vector3 a = V(-length * .5f, r * .7f, 0), end = V(length * .5f, r * .45f, .1f);
            b.Beam(a, V(0, r, .18f), r, r * .85f, 8, C(Bark));
            b.Beam(V(0, r, .18f), end, r * .85f, r * .58f, 7, C(BarkLight));
            b.Beam(end, end + V(.035f, -.002f, 0), r * .55f, r * .55f, 7, C(Ash));
            for (int i = 0; i < 5; i++)
            {
                float x = b.Range(-length * .3f, length * .35f), side = i % 2 == 0 ? -1 : 1;
                b.Beam(V(x, r * .8f, 0), V(x + .4f, b.Range(.6f, 1.1f), side * r * 1.45f), .13f, .025f, 5, C(Bark));
            }
            b.Push(); b.Translate(a); b.Scale(.3f, 1, 1);
            b.Rock(Vector3.zero, r * 1.6f, C(BarkLight), .22f, 1, 1); b.Pop();
            for (int i = 0; i < 7; i++)
            {
                float angle = i * 2 * Pi / 7;
                BentRod(b, a, a + V(-.35f, Mathf.Sin(angle) * r * 1.3f, Mathf.Cos(angle) * r * 1.3f),
                    a + V(-.7f, Mathf.Sin(angle) * r * 1.65f, Mathf.Cos(angle) * r * 1.8f), .09f, Bark);
            }
            b.Beam(a + V(.4f, r * .78f, 0), end + V(-.4f, r * .53f, .08f), .025f, .018f, 3, C(Char));
            b.Pop();
            Vector3 center = Yaw(V(-.3f, 0, 0), yaw);
            return Finish(b, "fallen-log", length * .5f + 1.05f, r * 2.6f + .1f,
                new List<PropCollider> { PropCollider.Rect(center.x, center.z, length * .5f + .5f, r * 1.85f, yaw) });
        }

        public static PropModel RootTangle(int seed)
        {
            var b = new MeshBuilder(seed); float r = b.Range(1.5f, 2.5f);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.4f + b.Range(-.3f, .3f);
                Vector3 start = V(b.Range(-.35f, .35f), .14f, b.Range(-.35f, .35f));
                Vector3 bend = V(Mathf.Sin(a) * r * .55f, b.Range(.08f, .18f), Mathf.Cos(a) * r * .55f);
                Vector3 tip = V(Mathf.Sin(a + .3f) * r, .015f, Mathf.Cos(a + .3f) * r);
                b.Beam(start, bend, .12f, .065f, 5, b.Jitter(Bark));
                b.Beam(bend, tip, .065f, .015f, 4, C(BarkLight));
                b.Beam(bend, V(Mathf.Sin(a - .45f) * r * .85f, .02f, Mathf.Cos(a - .45f) * r * .85f), .05f, .008f, 4, C(Bark));
            }
            for (int i = 0; i < 4; i++) b.Rock(V(b.Range(-.8f, .8f), .06f, b.Range(-.8f, .8f)), .22f, C(Moss), .2f, .3f, 0);
            return Finish(b, "root-tangle", r + .1f, .32f);
        }

        public static PropModel GreatDeadTree(int seed)
        {
            var b = new MeshBuilder(seed); var colliders = new List<PropCollider>();
            float h = b.Range(19, 21), radius = b.Range(1.9f, 2.3f);
            HollowTrunk(b, radius, h * .53f, 12, true);
            // Ring collision leaves the hollow and its +Z entrance navigable.
            for (int i = 1; i < 11; i++)
            {
                float a = (i + .5f) * 2 * Pi / 12;
                colliders.Add(PropCollider.Circle(Mathf.Sin(a) * radius * .84f, Mathf.Cos(a) * radius * .84f, radius * .31f));
            }
            Vector3 fork = V(.2f, h * .43f, -.2f), top = V(-1.3f, h - .35f, -.9f);
            b.Beam(fork + V(-.75f, 0, 0), V(-.85f, h * .7f, -.4f), .85f, .52f, 8, C(Bark));
            b.Beam(V(-.85f, h * .7f, -.4f), top, .52f, .09f, 7, C(BarkLight));
            b.Beam(fork + V(.75f, 0, 0), V(2, h * .72f, .7f), .72f, .26f, 7, C(BarkLight));
            for (int i = 0; i < 8; i++)
            {
                float a = i * 2.4f + b.Range(-.25f, .25f), t = b.Range(.38f, .78f);
                Vector3 start = V(-.4f, h * t, -.2f);
                float reach = b.Range(3.3f, 6);
                Vector3 bend = start + V(Mathf.Sin(a) * reach, b.Range(.5f, 2), Mathf.Cos(a) * reach);
                Vector3 tip = bend + V(Mathf.Sin(a + .4f) * 1.5f, b.Range(1, 2.5f), Mathf.Cos(a + .4f) * 1.5f);
                b.Beam(start, bend, .42f, .21f, 7, C(Bark));
                b.Beam(bend, tip, .21f, .035f, 5, C(BarkLight));
                b.Beam(bend, bend + V(Mathf.Sin(a - .7f) * 1.6f, 1.25f, Mathf.Cos(a - .7f) * 1.6f), .14f, .025f, 5, C(Ash));
            }
            // Buttresses avoid the front doorway. High sections receive collision;
            // the tapered outer roots lie below ankle height and remain decorative.
            for (int i = 1; i <= 7; i++)
            {
                float a = i * 2 * Pi / 8, reach = b.Range(4.6f, 6);
                Vector3 direction = V(Mathf.Sin(a), 0, Mathf.Cos(a));
                Vector3 start = direction * radius * .7f + V(0, 1.8f, 0);
                Vector3 bend = direction * radius * 1.4f + V(0, .45f, 0);
                Vector3 end = direction * reach + V(0, .02f, 0);
                b.Beam(start, bend, .6f, .32f, 6, C(Bark));
                b.Beam(bend, end, .32f, .035f, 5, C(BarkLight));
                Vector3 c = direction * radius * 1.25f;
                colliders.Add(PropCollider.Circle(c.x, c.z, .65f));
            }
            for (int i = 0; i < 7; i++)
            {
                float a = b.Range(.65f, 2 * Pi - .65f);
                b.Rock(V(Mathf.Sin(a) * radius, .14f, Mathf.Cos(a) * radius), .45f, C(Moss), .2f, .25f, 0);
            }
            return Finish(b, "great-dead-tree", 8.5f, h + 1, colliders, true);
        }

        public static PropModel Pebbles(int seed, int hex)
        {
            var b = new MeshBuilder(seed); int count = b.RangeInt(6, 15);
            for (int i = 0; i < count; i++)
            {
                float a = b.Range(0, 2 * Pi), radius = b.Range(.07f, .22f);
                float reach = Mathf.Sqrt(b.Rand()) * (1.5f - radius * 1.25f);
                b.Rock(V(Mathf.Sin(a) * reach, .025f, Mathf.Cos(a) * reach), radius,
                    b.Jitter(hex, .14f), .24f, b.Range(.35f, .7f), 0);
            }
            return Finish(b, "pebbles", 1.5f, .22f);
        }
    }
}
