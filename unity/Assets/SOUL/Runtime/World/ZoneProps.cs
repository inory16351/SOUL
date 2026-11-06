using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Sim-space slice models. Ground-level solids stay inside the original colliders.
    public static class ZoneProps
    {
        const int Concrete = 0x817b73, Broken = 0x969085, Dark = 0x514d47;
        const int Steel = 0x484b49, Rust = 0x754c35, Soot = 0x302f2c;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => MeshBuilder.Hex(hex);

        // Two-sided strips keep distant rebar readable at low cost.
        static void Strip(MeshBuilder b, Vector3 a, Vector3 end, float width, int hex)
        {
            Vector3 d = end - a;
            Vector3 side = Vector3.Cross(d, Mathf.Abs(d.normalized.y) > .9f ? Vector3.forward : Vector3.up).normalized * width * .5f;
            b.Quad(a - side, a + side, end + side, end - side, C(hex));
            b.Quad(end - side, end + side, a + side, a - side, C(hex));
        }

        // Eight-triangle fractured wedge, strictly bounded by size in XZ.
        static void Shard(MeshBuilder b, Vector3 p, Vector3 size, int hex)
        {
            Vector3 a = p + V(-size.x * .5f, 0, -size.z * .36f);
            Vector3 c = p + V(-size.x * .2f, 0, size.z * .5f);
            Vector3 d = p + V(size.x * .5f, 0, -size.z * .5f);
            Vector3 at = a + V(size.x * .12f, size.y * .55f, 0);
            Vector3 ct = c + V(0, size.y, -size.z * .1f);
            Vector3 dt = d + V(-size.x * .08f, size.y * .3f, 0);
            b.Tri(at, ct, dt, b.Jitter(hex)); b.Tri(a, d, c, C(Dark));
            b.Quad(a, c, ct, at, C(Dark)); b.Quad(c, d, dt, ct, b.Jitter(hex)); b.Quad(d, a, at, dt, C(Broken));
        }

        // Closed concrete extrusion with an uneven, chipped roof: 24 triangles.
        static void BrokenBlock(MeshBuilder b, Vector3 center, Vector3 size, int hex)
        {
            float x = size.x * .5f, z = size.z * .5f, lo = center.y - size.y * .5f;
            var front = new Vector3[4]; var back = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float px = center.x + Mathf.Lerp(-x, x, i / 3f);
                float y = center.y + size.y * .5f - b.Range(.04f, Mathf.Min(.65f, size.y * .6f));
                front[i] = V(px, y, center.z + z);
                back[i] = V(px, y - b.Range(0, Mathf.Min(.25f, size.y * .12f)), center.z - z);
            }
            for (int i = 0; i < 3; i++)
            {
                Vector3 f = front[i], n = front[i + 1], a = back[i], c = back[i + 1];
                b.Quad(V(f.x, lo, f.z), V(n.x, lo, n.z), n, f, b.Jitter(hex));
                b.Quad(V(c.x, lo, c.z), V(a.x, lo, a.z), a, c, b.Jitter(hex));
                b.Quad(a, f, n, c, b.Jitter(Broken));
            }
            Vector3 f0 = front[0], f3 = front[3], b0 = back[0], b3 = back[3];
            b.Quad(V(b0.x, lo, b0.z), V(f0.x, lo, f0.z), f0, b0, C(Dark));
            b.Quad(V(f3.x, lo, f3.z), V(b3.x, lo, b3.z), b3, f3, C(Dark));
            b.Quad(V(b0.x, lo, b0.z), V(b3.x, lo, b3.z), V(f3.x, lo, f3.z), V(f0.x, lo, f0.z), C(Dark));
        }

        static void GroundCrack(MeshBuilder b, Vector3 a, Vector3 end, float width)
        {
            Vector3 side = Vector3.Cross(end - a, Vector3.up).normalized * width;
            b.Quad(a - side, a + side, end + side * .35f, end - side * .35f, C(0x302d29));
        }

        // Recessed wall foot leaves room for rubble on BOTH faces, within the collider.
        static void WallFoot(MeshBuilder b, float length, float height, float thickness)
        {
            float x = length * .5f, z = thickness * .5f;
            Vector3 a = V(-x, 0, -z + .42f), c = V(-x, 0, z - .42f);
            Vector3 d = V(x, 0, z - .42f), e = V(x, 0, -z + .42f);
            Vector3 at = V(-x, height, -z), ct = V(-x, height, z);
            Vector3 dt = V(x, height, z), et = V(x, height, -z);
            b.Quad(at, ct, dt, et, b.Jitter(Concrete));
            b.Quad(e, d, c, a, C(Dark));
            b.Quad(a, c, ct, at, b.Jitter(Dark));
            b.Quad(c, d, dt, ct, b.Jitter(Concrete));
            b.Quad(d, e, et, dt, b.Jitter(Dark));
            b.Quad(e, a, at, et, b.Jitter(Concrete));
        }

        static void Moss(MeshBuilder b, Vector3 p, float radius)
        {
            const int sides = 7; var rim = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                rim[i] = p + V(Mathf.Sin(a), 0, Mathf.Cos(a)) * (radius * b.Range(.65f, 1));
            }
            for (int i = 0; i < sides; i++)
                b.Tri(p + V(0, .018f, 0), rim[i], rim[(i + 1) % sides], b.Jitter(b.Chance(.4f) ? 0x657c42 : 0x52633b));
        }

        // ---------- purified ground ----------
        public static PropModel Grass(int seed)
        {
            var b = new MeshBuilder(seed); int tufts = b.RangeInt(6, 9);
            for (int t = 0; t < tufts; t++)
            {
                float yaw = b.Range(0, Mathf.PI * 2), radius = b.Range(.15f, 1.05f);
                Vector3 p = V(Mathf.Sin(yaw) * radius, .012f, Mathf.Cos(yaw) * radius);
                Moss(b, p, b.Range(.18f, .35f));
                for (int i = 0; i < 6; i++)
                {
                    float a = b.Range(0, Mathf.PI * 2), h = b.Range(.18f, .46f);
                    Vector3 dir = V(Mathf.Sin(a), 0, Mathf.Cos(a)), side = V(dir.z, 0, -dir.x) * b.Range(.025f, .045f);
                    Vector3 root = p + dir * b.Range(0, .07f), mid = root + dir * .055f + V(0, h * .6f, 0);
                    Vector3 tip = root + dir * b.Range(.1f, .22f) + V(0, h, 0);
                    Color col = b.Jitter(b.Chance(.5f) ? 0x738c46 : 0x566e37, .12f);
                    b.Quad(root - side, root + side, mid + side * .55f, mid - side * .55f, col * .8f);
                    b.Quad(mid - side * .55f, mid + side * .55f, root + side, root - side, col * .8f);
                    b.Tri(mid - side * .55f, mid + side * .55f, tip, col);
                    b.Tri(mid + side * .55f, mid - side * .55f, tip, col);
                }
            }
            return new PropModel { mesh = b.Build("grass"), footprint = 1.5f, height = .5f };
        }

        public static PropModel Flowers(int seed)
        {
            var b = new MeshBuilder(seed); int n = b.RangeInt(5, 8);
            int[] petals = { 0xd3bd78, 0xdedcc6, 0xbf8e92, 0x99a9bd };
            int hex = petals[b.RangeInt(0, petals.Length)];
            for (int i = 0; i < n; i++)
            {
                float a = b.Range(0, Mathf.PI * 2), radius = b.Range(.1f, .72f), h = b.Range(.2f, .34f);
                Vector3 p = V(Mathf.Sin(a) * radius, .012f, Mathf.Cos(a) * radius);
                Vector3 head = p + V(b.Range(-.045f, .045f), h, b.Range(-.045f, .045f));
                Moss(b, p, .16f); b.Beam(p, head, .012f, .007f, 3, C(0x52633b));
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 root = Vector3.Lerp(p, head, .45f), tip = root + V(s * .12f, .055f, .025f);
                    Vector3 mid = Vector3.Lerp(root, tip, .5f), side = V(0, .01f, .035f);
                    b.Quad(root, mid + side, tip, mid - side, C(0x70804a));
                    b.Quad(mid - side, tip, mid + side, root, C(0x52633b));
                }
                for (int k = 0; k < 5; k++)
                {
                    float yaw = a + k * Mathf.PI * 2 / 5;
                    Vector3 dir = V(Mathf.Sin(yaw), 0, Mathf.Cos(yaw)), side = V(dir.z, 0, -dir.x) * .032f;
                    Vector3 neck = head + dir * .045f + V(0, .024f, 0), tip = head + dir * .095f + V(0, .012f, 0);
                    Color col = b.Jitter(hex, .08f);
                    b.Quad(head, neck - side, tip, neck + side, col);
                    b.Quad(neck + side, tip, neck - side, head, col * .8f);
                }
                b.Disc(head + V(0, .027f, 0), 0, .025f, 5, C(0xb49a51));
            }
            return new PropModel { mesh = b.Build("flowers"), footprint = 1, height = .4f };
        }

        // ---------- charging pod (scientist's checkpoint) ----------
        public static PropModel Pod(out Mesh glow)
        {
            var b = new MeshBuilder(77); var g = new MeshBuilder(78);
            // Reclaimed flywheel foundation, wholly inside the 1.5 m collision circle.
            b.Cylinder(V(0, 0, 0), 1.49f, 1.44f, .22f, 16, C(0x454744));
            b.Cylinder(V(0, .22f, 0), 1.4f, 1.25f, .2f, 16, C(0x827561));
            b.Cylinder(V(0, .42f, 0), 1.17f, 1.12f, .12f, 12, C(0x595c54));
            b.Cylinder(V(0, .54f, 0), .86f, .77f, .18f, 12, C(0xa29b83));
            b.Disc(V(0, .725f, 0), 0, .72f, 12, C(0x4a5e58));
            g.Disc(V(0, .729f, 0), .65f, .71f, 20, C(0x72c4c9));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4; Vector3 radial = V(Mathf.Sin(a), 0, Mathf.Cos(a));
                b.Cylinder(radial * 1.28f + V(0, .35f, 0), .055f, .055f, .06f, 5, C(0xafa48a));
                b.Push(); b.RotateY(a); b.Box(V(0, .24f, 1.39f), V(.22f, .065f, .045f), C(0x544839)); b.Pop();
            }
            // Rear half-shell and open front reveal a protected, softly lit charging cradle.
            for (int i = 0; i < 6; i++)
            {
                float a0 = Mathf.PI * (.5f + i / 6f), a1 = Mathf.PI * (.5f + (i + 1) / 6f);
                Vector3 u = V(Mathf.Sin(a0), 0, Mathf.Cos(a0)), v = V(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                Color shell = b.Jitter(i == 2 ? 0x78857b : 0x999581);
                b.Quad(u * .83f + V(0, .68f, 0), v * .83f + V(0, .68f, 0), v * .85f + V(0, 2.48f, 0), u * .85f + V(0, 2.48f, 0), shell);
                b.Quad(u * .85f + V(0, 2.48f, 0), v * .85f + V(0, 2.48f, 0), v * .83f + V(0, .68f, 0), u * .83f + V(0, .68f, 0), C(0x54645b));
                // Uneven reclaimed panels with exposed seam clamps and a soldered repair.
                b.Beam(u * .84f + V(0, .78f, 0), u * .855f + V(0, 2.43f, 0), .022f, .022f, 4, C(0x776d57));
                if (i == 1 || i == 4)
                {
                    b.Push(); b.RotateY((a0 + a1) * .5f);
                    b.Box(V(.025f, 1.28f, .824f), V(.3f, .42f, .035f), C(0x697b70));
                    for (int s = -1; s <= 1; s += 2)
                        b.Beam(V(s * .11f + .025f, 1.42f, .84f), V(s * .11f + .025f, 1.42f, .87f), .025f, .025f, 5, C(0xb4a487));
                    b.Pop();
                }
            }
            for (int i = 0; i < 4; i++)
            {
                float a = .72f + i * Mathf.PI * .5f; Vector3 radial = V(Mathf.Sin(a), 0, Mathf.Cos(a));
                Vector3 foot = radial * 1.08f + V(0, .44f, 0), waist = radial * 1.02f + V(0, 1.48f, 0);
                Vector3 shoulder = radial * .86f + V(0, 2.48f, 0), cap = radial * .43f + V(0, 2.98f, 0);
                Color metal = b.Jitter(i == 1 ? 0x887054 : 0xaea38a);
                b.Beam(foot, waist, .085f, .075f, 5, metal); b.Beam(waist, shoulder, .075f, .065f, 5, metal);
                b.Beam(shoulder, cap, .065f, .07f, 5, metal);
                b.Beam(waist - V(0, .09f, 0), waist + V(0, .09f, 0), .1f, .1f, 5, C(Steel));
            }
            b.Cylinder(V(0, 2.48f, 0), .85f, .56f, .42f, 12, C(0x918d7a));
            b.Cylinder(V(0, 2.9f, 0), .56f, .4f, .18f, 12, C(0xb3aa91));
            b.Cylinder(V(0, 3.08f, 0), .3f, .3f, .08f, 8, C(0x4a5650));
            for (int i = 0; i < 6; i++)
            {
                b.Push(); b.RotateY(i * Mathf.PI / 3);
                b.Box(V(0, 3.161f, .273f), V(.055f, .008f, .025f), C(0x272f2b));
                b.Pop();
            }
            g.Disc(V(0, 3.163f, 0), .19f, .25f, 12, C(0x80cbd0));
            g.Cylinder(V(0, .8f, -.12f), .11f, .085f, 1.53f, 8, C(0x81d7dc));
            for (int i = 0; i < 4; i++) b.Cylinder(V(0, .81f + i * .48f, -.12f), .17f, .17f, .065f, 8, C(0x8c7756));
            b.Box(V(0, 1.44f, -.63f), V(.64f, 1.2f, .18f), C(0x6a796b));
            b.Box(V(0, 2.12f, -.61f), V(.48f, .2f, .2f), C(0xa29d86));
            // Rear console, including repairs, within Rect(0, -1.55, .6, .25).
            b.Box(V(0, .1f, -1.55f), V(1.18f, .2f, .48f), C(Steel));
            b.Box(V(0, 1.03f, -1.56f), V(1.08f, 1.86f, .42f), C(0x6f7264));
            b.Box(V(0, 2.04f, -1.55f), V(1.16f, .16f, .46f), C(0xa3977c));
            b.Box(V(-.17f, 1.54f, -1.333f), V(.55f, .43f, .022f), C(0x353e39));
            g.Beam(V(-.17f, 1.54f, -1.318f), V(-.17f, 1.54f, -1.305f), .115f, .115f, 8, C(0x79c6ca));
            for (int i = 0; i < 3; i++)
            {
                b.Box(V(.32f, 1.43f + i * .16f, -1.329f), V(.12f, .075f, .032f), C(0x998867));
                b.Box(V(-.14f, .57f + i * .12f, -1.342f), V(.59f, .035f, .02f), C(0x353b35));
            }
            b.Box(V(.31f, .94f, -1.78f), V(.31f, .46f, .025f), C(0x887356));
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 a = V(s * .46f, .62f, -1.35f), bend = V(s * .65f, .2f, -1.16f), end = V(s * .8f, .03f, -1.04f);
                b.Beam(a, bend, .04f, .04f, 5, C(0x3d423a)); b.Beam(bend, end, .04f, .032f, 5, C(0x3d423a));
                b.Beam(end, end - V(0, .15f, 0), .032f, .032f, 5, C(0x827054));
                b.Cylinder(V(s * .44f, 2.12f, -1.55f), .04f, .04f, .025f, 5, C(0x514b3e));
            }
            var m = new PropModel { mesh = b.Build("charge-pod"), footprint = 3, height = 3.2f };
            m.colliders.Add(PropCollider.Circle(0, 0, 1.5f)); m.colliders.Add(PropCollider.Rect(0, -1.55f, .6f, .25f));
            glow = g.Build("charge-pod-glow");
            return m;
        }

        // ---------- boss arena ----------
        public static PropModel ArenaWall(BossArena a, out float entranceHalfAngle)
        {
            var b = new MeshBuilder(900 + a.zone); var m = new PropModel { footprint = a.radius + 4, height = 9 };
            const int SEG = 40; float r = a.radius + ZoneAreas.WALL_T * .5f;
            entranceHalfAngle = Mathf.Asin(6.5f / r);
            float ropeYaw = Mathf.Atan2(a.ropeSite.pos.x - a.center.x, a.ropeSite.pos.y - a.center.z), ropeHalf = Mathf.Asin((ZoneWorld.GATE_HALF - 1.5f) / r);
            float seg = Mathf.PI * 2 / SEG, len = 2 * r * Mathf.Sin(seg / 2) + .4f;
            for (int i = 0; i < SEG; i++)
            {
                float yaw = (i + .5f) * seg;
                if (Mathf.Abs(Sim.WrapPi(yaw - a.entranceYaw)) < entranceHalfAngle + seg * .5f) continue;
                if (Mathf.Abs(Sim.WrapPi(yaw - ropeYaw)) < ropeHalf + seg * .3f) continue;
                var o = V(Mathf.Sin(yaw), 0, Mathf.Cos(yaw)) * r; float h = b.Range(5.5f, 9);
                b.Push(); b.Translate(o); b.RotateY(yaw);
                // Continuous low wall explains collision; real window openings begin above reach.
                float sill = 3.5f, lintel = h - .9f, hole = len * b.Range(.25f, .4f), pier = (len - hole) * .5f;
                WallFoot(b, len, sill, ZoneAreas.WALL_T);
                for (int s = -1; s <= 1; s += 2)
                    b.Box(V(s * (hole + pier) * .5f, (sill + lintel) * .5f, 0), V(pier, lintel - sill, ZoneAreas.WALL_T), b.Jitter(Concrete));
                BrokenBlock(b, V(0, (lintel + h) * .5f, 0), V(len, h - lintel, ZoneAreas.WALL_T), Concrete);
                Vector3 root = V(b.Range(-len * .3f, len * .3f), h - .32f, b.Range(-.7f, .7f));
                Vector3 bend = root + V(.06f, .6f, .08f);
                Strip(b, root, bend, .065f, Rust); Strip(b, bend, bend + V(.3f, -.18f, .12f), .055f, Rust);
                for (int s = -1; s <= 1; s += 2)
                {
                    // Both faces dressed inside the collider's 0.1 m face allowance.
                    float z = s * (ZoneAreas.WALL_T * .5f + .008f), x = b.Range(-len * .3f, len * .3f);
                    Vector3 p0 = V(x - .3f, .4f, z - s * .42f * (1 - .4f / sill));
                    Vector3 p1 = V(x + .12f, .6f, z - s * .42f * (1 - .6f / sill));
                    Vector3 p2 = V(x + .3f, 3.2f, z - s * .42f * (1 - 3.2f / sill));
                    Vector3 p3 = V(x - .45f, 3.35f, z - s * .42f * (1 - 3.35f / sill));
                    if (s > 0) b.Quad(p0, p1, p2, p3, b.Jitter(Soot));
                    else b.Quad(p3, p2, p1, p0, b.Jitter(0x635247));
                    // Rotate the wedge so its outer face faces away from the wall on either side.
                    b.Push(); b.Translate(b.Range(-len * .32f, len * .32f), .015f, s * (ZoneAreas.WALL_T * .5f - .18f));
                    b.RotateY(s < 0 ? Mathf.PI : 0);
                    Shard(b, Vector3.zero, V(b.Range(.65f, 1.3f), b.Range(.3f, .65f), .55f), Concrete);
                    if (i % 3 == 0) Shard(b, V(.42f, 0, -.025f), V(.42f, .19f, .48f), Dark);
                    b.Pop();
                }
                b.Pop();
                // Original collider loop and dimensions are intentionally unchanged.
                m.colliders.Add(PropCollider.Rect(o.x, o.z, len * .5f + .2f, ZoneAreas.WALL_T * .5f + .1f, yaw));
            }
            for (int s = -1; s <= 1; s += 2)
            {
                float yaw = a.entranceYaw + s * (entranceHalfAngle + seg * .5f); var o = V(Mathf.Sin(yaw), 0, Mathf.Cos(yaw)) * r;
                b.Push(); b.Translate(o);
                b.Box(V(0, .4f, 0), V(2.76f, .8f, 2.76f), C(Dark));
                BrokenBlock(b, V(s * .025f, 5.65f, 0), V(2.6f, 10.7f, 2.6f), 0x777068);
                for (int k = 0; k < 3; k++)
                {
                    float x = (k - 1) * .68f;
                    Strip(b, V(x, 10.5f, .48f), V(x + .1f, 11.3f, .53f), .07f, Rust);
                    Strip(b, V(x + .1f, 11.3f, .53f), V(x + .3f, 11.15f, .67f), .06f, Rust);
                }
                for (int side = 0; side < 4; side++)
                {
                    b.Push(); b.RotateY(side * Mathf.PI * .5f);
                    b.Quad(V(-.85f, 2, 1.335f), V(-.58f, 1.4f, 1.335f), V(-.12f, 8.3f, 1.335f), V(-.5f, 8.8f, 1.335f), C(0x4e4840));
                    // Loose corner chips rest on the foundation, inside its 1.4 m half extent.
                    Shard(b, V(.87f, .8f, 1.13f), V(.9f, .42f, .5f), Broken);
                    b.Pop();
                }
                b.Pop();
                m.colliders.Add(PropCollider.Rect(o.x, o.z, 1.4f, 1.4f));
            }
            // Flat repairs and branching fractures; highest floor detail is y = .038 m.
            b.Disc(V(0, .015f, 0), 0, a.radius, 40, C(0x514b43));
            for (int i = 0; i < 14; i++)
            {
                float yaw = b.Range(0, Mathf.PI * 2), d = b.Range(3, a.radius * .78f);
                Vector3 p = V(Mathf.Sin(yaw) * d, .022f, Mathf.Cos(yaw) * d);
                b.Disc(p, 0, b.Range(.8f, 2.4f), 5, b.Jitter(0x59534a, .06f));
                Vector3 last = p + V(-1.3f, .01f, -.7f);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 next = last + V(b.Range(.5f, 1.1f), 0, b.Range(-.65f, .75f));
                    GroundCrack(b, last, next, b.Range(.025f, .065f));
                    if (k == 1) GroundCrack(b, next + V(0, .006f, 0), next + V(-.35f, .006f, 1.1f), .033f);
                    last = next;
                }
            }
            m.mesh = b.Build("boss-arena-" + a.zone);
            return m;
        }

        // Existing terrain is uncut; a near-black ground cap supplies depth behind broken lips.
        public static PropModel Chasm(Chasm c, out Mesh glow)
        {
            var b = new MeshBuilder(321); var g = new MeshBuilder(322); var m = new PropModel { footprint = c.halfLength, height = c.hookHeight + 1 };
            float L = c.halfLength, Wd = c.halfWidth, Hh = c.hookHeight; var rope = C(ZoneGates.PartHex[Parts.Rope]);
            b.Quad(V(-L, .012f, -Wd), V(-L, .012f, Wd), V(L, .012f, Wd), V(L, .012f, -Wd), C(0x040405));
            for (int e = -1; e <= 1; e += 2)
            {
                b.Push(); b.RotateY(e < 0 ? Mathf.PI : 0);
                // Fragments are on the blocked side, never in the landing lanes.
                const int pieces = 14; float step = L * 2 / pieces;
                for (int i = 0; i < pieces; i++)
                {
                    float x0 = -L + i * step, x1 = x0 + step, inward = b.Range(.35f, .85f), h = b.Range(.28f, .55f);
                    Vector3 a = V(x0, h, Wd), d = V(x1, h * .9f, Wd);
                    Vector3 q = V(x0 + step * .12f, h * .65f, Wd - inward), n = V(x1 - step * .16f, h * .4f, Wd - inward * .8f);
                    b.Quad(a, d, n, q, b.Jitter(0x55534e));
                    // Torn asphalt exposes a lighter aggregate seam along the broken edge.
                    b.Quad(q + V(0, .004f, 0), Vector3.Lerp(q, a, .12f) + V(0, .004f, 0),
                        Vector3.Lerp(n, d, .16f) + V(0, .004f, 0), n + V(0, .004f, 0), C(0x777065));
                    b.Quad(q, n, V(n.x, .018f, n.z), V(q.x, .018f, q.z), C(0x373431));
                    b.Quad(V(x0, .015f, Wd), V(x1, .015f, Wd), d, a, b.Jitter(Concrete));
                    b.Tri(a, q, V(x0, .015f, Wd), C(Broken)); b.Tri(d, V(x1, .015f, Wd), n, C(Dark));
                    Vector3 root = V((x0 + x1) * .5f, h * .6f, Wd - inward * .72f);
                    Vector3 bend = root + V(.12f, -.06f, -.35f), end = bend + V(.06f, -h * .45f, -.16f);
                    b.Beam(root, bend, .027f, .024f, 3, C(Rust)); b.Beam(bend, end, .024f, .014f, 3, C(Rust));
                    if (i % 3 == 0) Shard(b, V((x0 + x1) * .5f, .02f, Wd - inward - .35f), V(step * .45f, .16f, .4f), Dark);
                }
                b.Pop();
            }
            // Close the street's short ends with fractured lips; keep the middle a dark void.
            for (int e = -1; e <= 1; e += 2)
            {
                b.Push(); b.RotateY(e < 0 ? Mathf.PI : 0);
                for (int i = 0; i < 5; i++)
                {
                    float z0 = Mathf.Lerp(-Wd, Wd, i / 5f), z1 = Mathf.Lerp(-Wd, Wd, (i + 1) / 5f);
                    float depth = b.Range(.25f, .65f), h = b.Range(.15f, .32f);
                    Vector3 a = V(L, h, z0), d = V(L, h * .8f, z1);
                    Vector3 q = V(L - depth, h * .45f, z0 + .05f), n = V(L - depth * .7f, h * .6f, z1 - .05f);
                    b.Quad(a, q, n, d, b.Jitter(0x4f4c46));
                    b.Quad(q, V(q.x, .016f, q.z), V(n.x, .016f, n.z), n, C(0x292827));
                    b.Quad(V(L, .015f, z0), a, d, V(L, .015f, z1), C(Dark));
                    b.Tri(a, V(L, .015f, z0), q, C(Broken));
                    b.Tri(d, n, V(L, .015f, z1), C(Dark));
                }
                b.Pop();
            }
            // Lattice crane with a solid lower casing that explains the mast collider.
            var mast = V(-L - 2.5f, 0, 0);
            b.Box(mast + V(0, .15f, 0), V(1.58f, .3f, 1.58f), C(0x514b40));
            b.Box(mast + V(0, 1.15f, 0), V(1.36f, 2, 1.36f), C(0x6f5840));
            b.Box(mast + V(.1f, 1.27f, .695f), V(.72f, .83f, .035f), C(0x4e5148));
            for (int i = 0; i < 4; i++)
            {
                b.Box(mast + V(.08f, 1.06f + i * .12f, .717f), V(.47f, .035f, .014f), C(0x292c29));
                b.Push(); b.Translate(mast); b.RotateY(i * Mathf.PI * .5f);
                b.Quad(V(-.46f, .3f, .686f), V(-.31f, .3f, .686f), V(-.22f, 1.85f, .686f), V(-.4f, 2.04f, .686f), C(0x4a362a));
                b.Pop();
            }
            for (int s = -1; s <= 1; s += 2)
                for (int t = -1; t <= 1; t += 2)
                {
                    b.Cylinder(mast + V(s * .62f, .3f, t * .62f), .055f, .055f, .07f, 5, C(0x93816a));
                    b.Beam(mast + V(s * .57f, .3f, t * .57f), mast + V(s * .43f + .08f, Hh + 3, t * .43f), .065f, .05f, 4, C(Rust));
                }
            for (int k = 0; k < 6; k++)
            {
                float y0 = Mathf.Lerp(2, Hh + 3, k / 6f), y1 = Mathf.Lerp(2, Hh + 3, (k + 1) / 6f);
                for (int s = -1; s <= 1; s += 2)
                {
                    b.Beam(mast + V(-.5f, y0, s * .49f), mast + V(.5f, y1, s * .49f), .033f, .033f, 3, C(0x625443));
                    b.Beam(mast + V(-.5f, y1, s * .49f), mast + V(.5f, y1, s * .49f), .04f, .04f, 3, C(Rust));
                }
            }
            // Triangular boom: top webbing remains readable from the gameplay camera.
            float boomY = Hh + 2.6f;
            for (int s = -1; s <= 1; s += 2)
                b.Beam(mast + V(0, boomY, s * .43f), V(0, boomY, s * .23f), .085f, .06f, 4, C(0x806447));
            b.Beam(mast + V(0, boomY + .65f, 0), V(0, boomY + .4f, 0), .07f, .05f, 4, C(0x8f7550));
            for (int i = 0; i < 9; i++)
            {
                float t = i / 9f, next = (i + 1) / 9f, x = Mathf.Lerp(mast.x, 0, t), xn = Mathf.Lerp(mast.x, 0, next);
                float z = Mathf.Lerp(.43f, .23f, t), zn = Mathf.Lerp(.43f, .23f, next);
                for (int s = -1; s <= 1; s += 2)
                    b.Beam(V(x, boomY, s * z), V(xn, boomY + Mathf.Lerp(.65f, .4f, next), 0), .035f, .035f, 3, C(Rust));
                b.Beam(V(x, boomY, -z), V(xn, boomY, zn), .03f, .03f, 3, C(0x544b3d));
            }
            Vector3 last = mast + V(0, boomY + .72f, 0);
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f;
                Vector3 next = V(Mathf.Lerp(mast.x, 0, t), boomY + .72f - Mathf.Sin(t * Mathf.PI) * .65f, 0);
                b.Beam(last, next, .026f, .026f, 3, C(0x302f2c)); last = next;
            }
            b.Beam(V(0, boomY + .72f, 0), V(0, Hh + .57f, 0), .04f, .04f, 5, C(0x302f2c));
            b.Beam(V(0, Hh + .67f, -.15f), V(0, Hh + .67f, .15f), .23f, .23f, 8, C(0x74634a));
            b.Box(V(0, Hh + .46f, 0), V(.24f, .4f, .25f), C(Steel));
            // True open ring, centred on HookPos. Only the inner sleeve glows.
            for (int i = 0; i < 12; i++)
            {
                float a0 = i * Mathf.PI / 6, a1 = (i + 1) * Mathf.PI / 6;
                Vector3 p = V(Mathf.Sin(a0) * .48f, Hh + Mathf.Cos(a0) * .48f, 0), q = V(Mathf.Sin(a1) * .48f, Hh + Mathf.Cos(a1) * .48f, 0);
                b.Beam(p, q, .085f, .085f, 5, C(0x8a7957));
                g.Beam(V(Mathf.Sin(a0) * .397f, Hh + Mathf.Cos(a0) * .397f, .018f), V(Mathf.Sin(a1) * .397f, Hh + Mathf.Cos(a1) * .397f, .018f), .023f, .023f, 4, rope);
            }
            m.colliders.Add(PropCollider.Rect(0, 0, L, Wd));
            m.colliders.Add(PropCollider.Rect(mast.x, 0, .8f, .8f));
            m.mesh = b.Build("chasm"); glow = g.Build("chasm-glow");
            return m;
        }

        // +Z firing direction; cable attachment at origin; three inward-curved claws.
        public static Mesh RopeHook()
        {
            var b = new MeshBuilder(66); var rope = C(ZoneGates.PartHex[Parts.Rope]) * .72f;
            b.Beam(Vector3.zero, V(0, 0, .075f), .038f, .065f, 8, C(0x48453d));
            b.Beam(V(0, 0, .065f), V(0, 0, .205f), .065f, .045f, 6, rope);
            b.Beam(V(0, 0, .205f), V(0, 0, .31f), .045f, .008f, 6, C(0x8b8877));
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2 / 3; Vector3 radial = V(Mathf.Sin(a), Mathf.Cos(a), 0);
                Vector3 root = radial * .045f + V(0, 0, .09f), elbow = radial * .14f + V(0, 0, .21f), tip = radial * .055f + V(0, 0, .34f);
                b.Beam(root, elbow, .025f, .022f, 4, C(0x686b63)); b.Beam(elbow, tip, .022f, .004f, 4, C(0x9b9984));
                b.Beam(radial * .052f + V(0, 0, .11f), radial * .082f + V(0, 0, .11f), .024f, .024f, 5, C(0x544a39));
            }
            return b.Build("rope-hook");
        }

        // Basic-parts pickup: worn gear, recessed bolts, offset servo and severed leads.
        public static Mesh Scrap()
        {
            var b = new MeshBuilder(55);
            b.Cylinder(V(0, -.06f, 0), .165f, .165f, .115f, 12, C(0x9d885d));
            for (int i = 0; i < 10; i++)
            {
                b.Push(); b.RotateY(i * Mathf.PI / 5);
                b.Box(V(0, -.006f, .183f), V(.055f, .085f, .065f), b.Jitter(i == 3 ? 0x716048 : 0xb49d6b)); b.Pop();
            }
            b.Disc(V(0, .058f, 0), .063f, .142f, 12, C(0xc2b183));
            b.Cylinder(V(0, .058f, 0), .063f, .058f, .04f, 8, C(0x41433c));
            b.Cylinder(V(0, .098f, 0), .034f, .025f, .055f, 6, C(0xa6a28a));
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * .5f + .3f;
                b.Disc(V(Mathf.Sin(a) * .107f, .061f, Mathf.Cos(a) * .107f), 0, .021f, 5, C(0x4c483b));
            }
            b.Box(V(-.084f, .09f, -.056f), V(.08f, .075f, .13f), C(0x727966));
            b.Box(V(-.084f, .132f, -.056f), V(.066f, .012f, .08f), C(0xa6a18b));
            for (int i = 0; i < 2; i++)
            {
                Vector3 a = V(-.08f + i * .028f, .1f, -.12f), bend = a + V(-.035f, .03f, -.045f);
                b.Beam(a, bend, .009f, .009f, 4, C(0x916845)); b.Beam(bend, bend + V(.025f, -.02f, -.025f), .009f, .006f, 4, C(0xb29b6c));
            }
            return b.Build("scrap");
        }

        // MeshBuilder fixes vertex alpha at one; additive RGB supplies the fade.
        public static Mesh FogGate(float halfWidth, float height)
        {
            var b = new MeshBuilder(5); const int columns = 20, rows = 7;
            for (int layer = 0; layer < 3; layer++)
            {
                float phase = b.Range(0, Mathf.PI * 2); var grid = new Vector3[columns + 1, rows + 1];
                for (int x = 0; x <= columns; x++)
                    for (int y = 0; y <= rows; y++)
                    {
                        float u = x / (float)columns * 2 - 1, v = y / (float)rows;
                        float sway = Mathf.Sin(v * 7 + phase + u * 2) * (1 - u * u);
                        float px = halfWidth * (.98f * u + .025f * sway);
                        float py = height * v * (.94f + .06f * Mathf.Sin(u * 5 + phase));
                        float pz = (layer - 1) * .19f + .105f * Mathf.Sin(u * 9 + phase + v * 5);
                        grid[x, y] = V(px, py, pz);
                    }
                for (int x = 0; x < columns; x++)
                    for (int y = 0; y < rows; y++)
                    {
                        float u = (x + .5f) / columns, v = (y + .5f) / rows;
                        float fade = Mathf.Pow(Mathf.Sin(u * Mathf.PI), .65f) * Mathf.Sin(v * Mathf.PI);
                        float fold = .55f + .45f * Mathf.Sin(u * 22 + v * 5 + phase);
                        float k = fade * (.28f + .4f * fold) * (layer == 1 ? 1 : .66f);
                        Color col = new Color(.23f * k, .57f * k, .63f * k);
                        // Caller uses Cull Off: one sheet avoids doubling additive energy.
                        b.Quad(grid[x, y], grid[x + 1, y], grid[x + 1, y + 1], grid[x, y + 1], col);
                    }
            }
            return b.Build("fog-gate");
        }
    }
}
