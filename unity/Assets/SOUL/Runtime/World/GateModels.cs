using UnityEngine;

namespace Soul
{
    // Site-local SIM space: +Z from link.a to link.b, +Y up. Build alone mirrors to Unity.
    // Collision and surfaces belong to ZoneGates. b/g persist; sb/sg vanish with the barrier.
    public static class GateModels
    {
        const int Concrete = 0x8a8580, Weathered = 0x6f6a64, Dark = 0x494640;
        const int Soot = 0x302e2b, Rust = 0x78432c, RustDark = 0x51382c, Steel = 0x44494b;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => MeshBuilder.Hex(hex);
        public static int ZoneA(ZoneWorld.Site s) => s.link.a;
        public static int ZoneB(ZoneWorld.Site s) => s.link.b;

        /// Quad with its front face toward outward (winding fixed automatically).
        public static void Q(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), outward) < 0) b.Quad(p0, p3, p2, p1, c); else b.Quad(p0, p1, p2, p3, c);
        }
        public static void T(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Color c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), outward) < 0) b.Tri(p0, p2, p1, c); else b.Tri(p0, p1, p2, c);
        }

        static Color Masonry(ZoneWorld.Site s)
        {
            int a = ZoneA(s), z = ZoneB(s);
            if (a == 7 || z == 7) return C(0x7b8384);
            if (a == 6 || z == 6) return C(0x747865);
            if (a == 4 || z == 4 || a == 3 || z == 3) return C(0x817164);
            if (a == 2 || z == 2) return C(0x7d7980);
            return C(Concrete);
        }

        // Closed chipped slab contained in the supplied bounds. Both cap heights are exact.
        static void Slab(MeshBuilder b, float x0, float x1, float z0, float z1,
            float bottom, float top, Color color, float chip)
        {
            float cut = Mathf.Min(chip, Mathf.Min(x1 - x0, z1 - z0) * .22f);
            float c0 = cut * b.Range(.5f, 1), c1 = cut * b.Range(.5f, 1);
            var p = new[] { V(x0 + c0, top, z0), V(x1 - c1, top, z0),
                V(x1, top, z0 + c1), V(x1, top, z1 - c0), V(x1 - c0, top, z1),
                V(x0 + c1, top, z1), V(x0, top, z1 - c1), V(x0, top, z0 + c0) };
            Vector3 center = V((x0 + x1) * .5f, top, (z0 + z1) * .5f);
            for (int i = 0; i < p.Length; i++)
            {
                Vector3 a = p[i], d = p[(i + 1) % p.Length];
                Vector3 lo = V(a.x, bottom, a.z), next = V(d.x, bottom, d.z);
                T(b, center, a, d, color, Vector3.up);
                T(b, V(center.x, bottom, center.z), next, lo, color * .65f, Vector3.down);
                Q(b, lo, next, d, a, b.Jitter(color * (i % 2 == 0 ? .83f : .69f), .04f),
                    V((a.x + d.x) * .5f - center.x, 0, (a.z + d.z) * .5f - center.z));
            }
        }

        // Bevelled XY facing; z is its outer face, thickness grows inward. Corner losses
        // stay inside the given rectangle. Closed back prevents see-through chipped edges.
        static void Facing(MeshBuilder b, float x0, float x1, float y0, float y1,
            float z, float thickness, float chip, Color color)
        {
            float e = Mathf.Sign(z), cut = Mathf.Min(chip, Mathf.Min(x1 - x0, y1 - y0) * .24f);
            float a = cut * b.Range(.5f, 1), d = cut * b.Range(.5f, 1);
            var rim = new[] { V(x0 + a, y0, z), V(x1 - d, y0, z), V(x1, y0 + d, z),
                V(x1, y1 - a, z), V(x1 - a, y1, z), V(x0 + d, y1, z),
                V(x0, y1 - d, z), V(x0, y0 + a, z) };
            Vector3 center = V((x0 + x1) * .5f, (y0 + y1) * .5f, z), normal = V(0, 0, e);
            float bevel = Mathf.Min(.045f, cut * .22f);
            for (int i = 0; i < rim.Length; i++)
            {
                Vector3 p = rim[i], q = rim[(i + 1) % rim.Length];
                Vector3 inset = p + (center - p).normalized * bevel;
                Vector3 insetNext = q + (center - q).normalized * bevel;
                Vector3 back = p - normal * thickness, backNext = q - normal * thickness;
                Vector3 edge = p - normal * bevel, edgeNext = q - normal * bevel;
                T(b, center, inset, insetNext, color, normal);
                Q(b, inset, edge, edgeNext, insetNext, color * .78f, normal);
                Q(b, edge, back, backNext, edgeNext, color * .64f, (p + q) * .5f - center);
                T(b, center - normal * thickness, backNext, back, color * .6f, -normal);
            }
        }

        // Surface-following strips for cracks and welds; no floating cylindrical marks.
        static void Ribbon(MeshBuilder b, Vector3 a, Vector3 end, float width, Color color, Vector3 normal)
        {
            Vector3 across = Vector3.Cross(end - a, normal).normalized * width * .5f;
            Q(b, a - across, a + across, end + across, end - across, color, normal);
        }

        static void Stain(MeshBuilder b, float x, float y, float z, float width, float height, Color color)
        {
            Q(b, V(x - width * .5f, y, z), V(x + width * .5f, y, z),
                V(x + width * .22f, y - height, z), V(x - width * .13f, y - height * .83f, z),
                color, V(0, 0, Mathf.Sign(z)));
        }

        static void Bolt(MeshBuilder b, Vector3 center, Vector3 normal, float radius)
        {
            b.Beam(center - normal * .018f, center + normal * .018f, radius, radius * .86f, 4, C(0x77736b));
        }

        // Rectangular-section forged eye; the hole remains open from either approach.
        static void Eye(MeshBuilder b, Vector3 center, float inner, float outer, float depth, Color color)
        {
            const int n = 12;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n, d = (i + 1) * Mathf.PI * 2 / n;
                Vector3 u = V(Mathf.Cos(a), Mathf.Sin(a), 0), v = V(Mathf.Cos(d), Mathf.Sin(d), 0);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 c = center + V(0, 0, side * depth * .5f);
                    Q(b, c + u * inner, c + u * outer, c + v * outer, c + v * inner, color, V(0, 0, side));
                }
                Vector3 f = center + V(0, 0, depth * .5f), r = center - V(0, 0, depth * .5f);
                Q(b, f + u * outer, r + u * outer, r + v * outer, f + v * outer, color * .85f, u + v);
                Q(b, f + u * inner, r + u * inner, r + v * inner, f + v * inner, color * .6f, -u - v);
            }
        }

        /// Unobstructed trail; all geometry stays outside |x| < 5.5. Debris is ankle-low.
        public static void FreeCorridor(ZoneWorld.Site s, MeshBuilder b)
        {
            Color stone = Masonry(s);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 9; i++)
                {
                    float z = -5.5f + i * 1.35f + b.Range(-.2f, .2f);
                    float x = side * b.Range(5.95f, 6.45f), w = b.Range(.35f, .65f);
                    Slab(b, x - w * .5f, x + w * .5f, z - .42f, z + .42f, -.04f,
                        b.Range(.08f, .17f), b.Jitter(stone * .78f), .18f);
                    b.Rock(V(side * b.Range(6.25f, 6.65f), .03f, z + .56f), .22f,
                        b.Jitter(Weathered), .2f, .48f, 0);
                }
                // Ragged shoulders blend into the existing ZoneRoutes trail.
                for (int i = 0; i < 7; i++)
                {
                    float x = side * b.Range(5.65f, 6.2f), z = b.Range(-5.8f, 5.8f);
                    Q(b, V(x - .12f, .008f, z - .3f), V(x + .12f, .008f, z - .2f),
                        V(x + .1f, .008f, z + .5f), V(x - .09f, .008f, z + .35f),
                        b.Jitter(stone * .67f), Vector3.up);
                }
            }
        }

        /// Three courses of collapsed concrete; the complete walking rectangle stays at H.
        public static void Ledge(ZoneWorld.Site s, MeshBuilder b, float hw, float H)
        {
            Color stone = Masonry(s);
            b.Box(V(0, (H - .09f) * .5f, 0), V(hw * 2, H - .09f, 11.68f), stone * .63f);
            b.Box(V(0, H - .045f, 0), V(hw * 2, .09f, 12), stone);
            const int bays = 7;
            float step = hw * 2 / bays;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 3; row++) for (int i = 0; i < bays; i++)
                {
                    float left = -hw + i * step, right = left + step;
                    float y0 = row * (H - .09f) / 3, y1 = (row + 1) * (H - .09f) / 3;
                    Facing(b, left, right, y0, y1 - .018f, side * 5.94f, .16f, .19f,
                        b.Jitter(stone * (row == 0 ? .79f : .94f), .065f));
                    float x = left + step * b.Range(.3f, .7f);
                    Stain(b, x, y1 - .15f, side * 5.944f, b.Range(.15f, .38f),
                        (y1 - y0) * .65f, C(i % 3 == 0 ? RustDark : Dark));
                    if ((i + row) % 3 == 0)
                    {
                        Vector3 a = V(left + .17f, y1 - .12f, side * 5.97f);
                        Vector3 bend = a + V(.36f, -.09f, 0);
                        b.Beam(a, bend, .025f, .022f, 4, C(Rust));
                        b.Beam(bend, bend + V(.17f, -.15f, 0), .022f, .016f, 4, C(RustDark));
                    }
                }
            }
            // Planar fractured paving inlays, not loose stepping stones on the landing.
            for (int i = 0; i < bays; i++) for (int row = 0; row < 3; row++)
            {
                float x0 = -hw + i * step + b.Range(.035f, .14f);
                float x1 = -hw + (i + 1) * step - b.Range(.035f, .16f);
                float z0 = -5.92f + row * 3.94f, z1 = z0 + b.Range(3.55f, 3.8f);
                Q(b, V(x0, H + .006f, z0), V(x1, H + .006f, z0 + .12f),
                    V(x1 - .09f, H + .006f, z1), V(x0, H + .006f, z1 - .08f),
                    b.Jitter(stone, .055f), Vector3.up);
                if ((i + row) % 2 == 0)
                {
                    Vector3 p = V(x0 + .2f, H + .012f, z0 + .4f);
                    Vector3 q = p + V(step * .31f, 0, 1.05f), r = q + V(.55f, 0, .64f);
                    Ribbon(b, p, q, .035f, C(Dark), Vector3.up);
                    Ribbon(b, q, r, .025f, C(Dark), Vector3.up);
                }
            }
            for (int i = 0; i < 8; i++)
            {
                float x = b.Range(-hw + 2, hw - 2), z = b.Range(-4.3f, 4.3f);
                Q(b, V(x - .9f, H + .009f, z - .35f), V(x + .7f, H + .009f, z - .2f),
                    V(x + 1.4f, H + .009f, z + .6f), V(x - .4f, H + .009f, z + .85f),
                    b.Jitter(stone * .88f, .03f), Vector3.up);
            }
        }

        /// Straight walking plane; every fragment vertex follows the slope.
        public static void Ramp(ZoneWorld.Site s, MeshBuilder b, float z0, float z1, float hx, float H)
        {
            float sign = Mathf.Sign(z1 - z0);
            Color stone = Masonry(s);
            Vector3 normal = V(0, 1, -H / (z1 - z0)).normalized;
            Q(b, V(-hx, 0, z0), V(hx, 0, z0), V(hx, H, z1), V(-hx, H, z1), stone * .66f, normal);
            Q(b, V(-hx, 0, z1), V(hx, 0, z1), V(hx, H, z1), V(-hx, H, z1), stone * .65f, V(0, 0, sign));
            Q(b, V(-hx, 0, z0), V(hx, 0, z0), V(hx, 0, z1), V(-hx, 0, z1), stone * .6f, Vector3.down);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int j = 0; j < 8; j++)
                {
                    float t0 = j / 8f, t1 = (j + 1) / 8f;
                    Vector3 a = V(side * hx, 0, Mathf.Lerp(z0, z1, t0));
                    Vector3 d = V(side * hx, 0, Mathf.Lerp(z0, z1, t1));
                    Vector3 top0 = V(a.x, H * t0, a.z), top1 = V(d.x, H * t1, d.z);
                    Color fill = b.Jitter(stone * (j % 3 == 0 ? .67f : .82f));
                    if (j == 0) T(b, a, d, top1, fill, V(side, 0, 0));
                    else Q(b, a, d, top1, top0, fill, V(side, 0, 0));
                }
            }
            for (int j = 0; j < 8; j++) for (int i = 0; i < 5; i++)
            {
                float x0 = -hx + i * hx * .4f + .035f, x1 = x0 + hx * .4f - .07f;
                float t0 = (j + b.Range(.025f, .09f)) / 8, t1 = (j + b.Range(.88f, .97f)) / 8;
                float za = Mathf.Lerp(z0, z1, t0), zb = Mathf.Lerp(z0, z1, t1);
                // Tiny bias prevents z-fighting while retaining the same straight slope.
                Vector3 a = V(x0, H * t0 + .004f, za), d = V(x1, H * t1 + .004f, zb);
                Q(b, a, V(x1, a.y, za), d, V(x0, d.y, zb), b.Jitter(stone, .08f), normal);
                if ((i + j) % 3 == 0)
                {
                    Vector3 p = V(x0 + .2f, H * t0 + .008f, za);
                    Vector3 q = V(x1 - .2f, H * t1 + .008f, zb);
                    Ribbon(b, p, q, .035f, C(Dark), normal);
                }
            }
        }

        /// Salvaged crane eye: bolted shoe, gussets, tapered post and hollow luminous ring.
        public static void Anchor(ZoneWorld.Site s, MeshBuilder b, MeshBuilder g, Vector3 ring, float H, Color glow)
        {
            float post = Mathf.Max(.18f, ring.y - H - .37f);
            Slab(b, ring.x - .57f, ring.x + .57f, ring.z - .48f, ring.z + .48f, H, H + .14f, C(Steel), .13f);
            b.Cylinder(V(ring.x, H + .14f, ring.z), .24f, .19f, post - .14f, 8, C(0x575653));
            b.Cylinder(V(ring.x, H + .22f, ring.z), .29f, .29f, .12f, 8, C(RustDark));
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            {
                Bolt(b, V(ring.x + x * .4f, H + .16f, ring.z + z * .32f), Vector3.up, .085f);
                b.Beam(V(ring.x + x * .41f, H + .17f, ring.z + z * .24f),
                    V(ring.x + x * .13f, H + post * .72f, ring.z + z * .1f), .065f, .045f, 4, C(RustDark));
            }
            Eye(b, ring, .32f, .57f, .24f, C(0x77746a));
            for (int side = -1; side <= 1; side += 2)
                Eye(g, ring + V(0, 0, side * .126f), .395f, .455f, .008f, glow * .75f);
        }

        /// Broken road lips and uninterrupted dark opening. No bridge or luminous edge strips.
        public static void JetGap(ZoneWorld.Site s, MeshBuilder b, MeshBuilder g, float hw, Color glow)
        {
            Color stone = Masonry(s);
            // WorldStreamer does not cut the terrain: this plane masks ground at y=-0.012.
            // True excavated depth requires a terrain change outside this file's ownership.
            Q(b, V(-hw, .006f, -5), V(hw, .006f, -5), V(hw, .006f, 5), V(-hw, .006f, 5), C(0x060708), Vector3.up);
            const int bays = 16;
            float step = hw * 2 / bays;
            for (int side = -1; side <= 1; side += 2) for (int i = 0; i < bays; i++)
            {
                float x0 = -hw + i * step, x1 = x0 + step;
                float edge0 = b.Range(5.02f, 5.25f), edge1 = b.Range(5.02f, 5.25f);
                Vector3 a = V(x0, .13f, side * edge0), d = V(x1, .13f, side * edge1);
                Vector3 rear0 = V(x0, .008f, side * 6), rear1 = V(x1, .008f, side * 6);
                Q(b, a, d, rear1, rear0, b.Jitter(stone * .75f), Vector3.up);
                Vector3 toe0 = V(x0, .014f, side * 5), toe1 = V(x1, .014f, side * 5);
                Q(b, toe0, toe1, d, a, b.Jitter(stone * .38f), V(0, 1, -side));
                T(b, a + V(0, .002f, 0), Vector3.Lerp(a, d, .42f) + V(0, .002f, 0),
                    Vector3.Lerp(a, rear0, .6f) + V(0, .002f, 0), stone * .55f, Vector3.up);
                // Flat depth shading does not occupy the ten-metre opening.
                Q(b, toe0, toe1, V(x1, .008f, side * 4.65f), V(x0, .008f, side * 4.7f), C(0x111213), Vector3.up);
                if (i % 2 == 0)
                {
                    float x = x0 + step * .45f;
                    b.Beam(V(x, .08f, side * 5.6f), V(x + .18f, .10f, side * 5.15f), .024f, .019f, 4, C(Rust));
                    b.Beam(V(x + .18f, .10f, side * 5.15f), V(x + .25f, .028f, side * 5.035f), .019f, .012f, 4, C(RustDark));
                }
            }
        }

        /// Aggregate barricade with exposed bars, broken crown blocks and trapped hot cracks.
        public static void CannonWall(ZoneWorld.Site s, MeshBuilder sb, MeshBuilder sg, float hw, Color glow)
        {
            Color stone = Masonry(s);
            const int bays = 10;
            float step = hw * 2 / bays;
            sb.Box(V(0, 1.5f, 0), V(hw * 2, 3, 3.3f), stone * .59f);
            for (int i = 0; i < bays; i++)
            {
                float left = -hw + i * step, right = left + step, x = (left + right) * .5f;
                float h = sb.Range(3.3f, 4.25f);
                Slab(sb, left, right, -1.64f, 1.64f, 2.85f, h, sb.Jitter(stone), .32f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Facing(sb, left, right, .015f, h - .08f, side * 1.78f, .24f, .36f, sb.Jitter(stone, .08f));
                    Vector3 p = V(x - step * .15f, .28f, side * 1.788f);
                    Vector3 q = V(x + step * .12f, h * .46f, p.z), r = V(x - step * .18f, h - .28f, p.z);
                    Ribbon(sb, p, q, .085f, C(Soot), V(0, 0, side));
                    Ribbon(sb, q, r, .07f, C(Soot), V(0, 0, side));
                    Ribbon(sb, q, q + V(step * .23f, .32f, 0), .04f, C(Dark), V(0, 0, side));
                    Stain(sb, x + step * .3f, h - .5f, side * 1.79f, .24f, .83f, C(RustDark));
                    if (i % 4 == 1)
                    {
                        Vector3 hot0 = Vector3.Lerp(p, q, .55f), hot1 = Vector3.Lerp(p, q, .8f);
                        hot0.z = hot1.z = side * 1.794f;
                        Ribbon(sg, hot0, hot1, .018f, glow * .43f, V(0, 0, side));
                    }
                    if (i % 3 == 0)
                    {
                        Facing(sb, x - .55f, x + .55f, .7f, 1.3f, side * 1.796f, .04f, .17f, C(Rust));
                        for (int k = -1; k <= 1; k += 2)
                            Stain(sb, x + k * .34f, 1.2f, side * 1.799f, .09f, .35f, C(Soot));
                    }
                    Slab(sb, x - .44f, x + .4f, side < 0 ? -1.8f : 1.31f,
                        side < 0 ? -1.31f : 1.8f, 0, .25f, sb.Jitter(stone * .8f), .16f);
                }
                Slab(sb, x - step * sb.Range(.23f, .37f), x + step * sb.Range(.21f, .35f),
                    -.66f, .8f, h - .04f, h + sb.Range(.08f, .17f), sb.Jitter(stone * .88f), .3f);
                sb.Rock(V(x + sb.Range(-.4f, .4f), h - .04f, 1.05f), .4f, sb.Jitter(stone * .83f), .2f, .45f, 0);
                sb.Rock(V(x + sb.Range(-.4f, .4f), h - .02f, -1.17f), .28f, sb.Jitter(stone * .72f), .2f, .5f, 0);
                Vector3 root = V(x + step * .23f, h - .05f, -.35f), bend = root + V(.1f, .27f, .08f);
                sb.Beam(root, bend, .03f, .026f, 4, C(Rust));
                sb.Beam(bend, bend + V(.22f, -.08f, .17f), .026f, .018f, 4, C(RustDark));
            }
        }

        /// Seven-metre ship-hull steel: continuous plates, ribs, rivets and rust, without glow.
        public static void MagnetWall(ZoneWorld.Site s, MeshBuilder sb, MeshBuilder sg, float hw, Color glow)
        {
            const int bays = 11;
            float step = hw * 2 / bays;
            sb.Box(V(0, 3.44f, 0), V(hw * 2, 6.88f, 1.3f), C(RustDark));
            for (int i = 0; i < bays; i++)
            {
                float left = -hw + i * step, right = left + step, x = (left + right) * .5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Color plate = sb.Jitter(i % 3 == 0 ? 0x65594b : i % 3 == 1 ? 0x714a35 : 0x59433a, .075f);
                    Facing(sb, left, right, .08f, 6.92f, side * .76f, .14f, .13f, plate);
                    Stain(sb, x + sb.Range(-.15f, .15f), sb.Range(4.1f, 5.5f), side * .764f,
                        step * sb.Range(.32f, .72f), sb.Range(1.1f, 3.2f), C(0x4e514b));
                    Stain(sb, x + step * .24f, 6.55f, side * .768f, .23f, sb.Range(1.1f, 2.3f), C(RustDark));
                    Stain(sb, x - step * .25f, 3.1f, side * .768f, .17f, 1.4f, C(Rust));
                    sb.Box(V(x, 3.35f, side * .79f), V(step - .12f, .18f, .10f), C(0x4b3930));
                    for (int k = -1; k <= 1; k += 2) for (int row = 0; row < 2; row++)
                        Bolt(sb, V(x + k * (step * .5f - .25f), row == 0 ? .47f : 6.52f, side * .79f), V(0, 0, side), .085f);
                    if (i < bays - 1)
                        sb.Box(V(right - .075f, 3.5f, side * .82f), V(.15f, 6.86f, .16f), C(Steel));
                }
                Slab(sb, left, right, -.9f, .9f, 6.88f, 7, sb.Jitter(0x655344), .09f);
                sb.Box(V(x, .12f, 0), V(step, .24f, 1.8f), C(RustDark));
            }
        }

        /// Level toxic pool with thin mineral crust, small bubbles and drowned industrial drums.
        public static void Sludge(ZoneWorld.Site s, MeshBuilder sb, MeshBuilder sg, float hw, Color glow)
        {
            const int cols = 12, rows = 5;
            for (int i = 0; i < cols; i++) for (int j = 0; j < rows; j++)
            {
                float x0 = -hw + i * hw * 2 / cols, x1 = -hw + (i + 1) * hw * 2 / cols;
                float z0 = -6 + j * 12f / rows, z1 = -6 + (j + 1) * 12f / rows;
                Vector3 middle = V((x0 + x1) * .5f + sb.Range(-.3f, .3f), .03f, (z0 + z1) * .5f + sb.Range(-.3f, .3f));
                var p = new[] { V(x0, .03f, z0), V(x1, .03f, z0), V(x1, .03f, z1), V(x0, .03f, z1) };
                Color liquid = sb.Jitter(0x27301d, .09f);
                for (int k = 0; k < 4; k++) T(sb, middle, p[k], p[(k + 1) % 4], sb.Jitter(liquid, .04f), Vector3.up);
            }
            for (int side = -1; side <= 1; side += 2) for (int i = 0; i < 16; i++)
            {
                float cell = hw * 2 / 16, x = -hw + (i + .5f) * cell;
                float width = cell * sb.Range(.68f, .94f), inner = sb.Range(5.45f, 5.75f);
                Slab(sb, x - width * .5f, x + width * .5f, side < 0 ? -6 : inner,
                    side < 0 ? -inner : 6, .015f, sb.Range(.065f, .11f), sb.Jitter(0x555643), .16f);
            }
            for (int i = 0; i < 18; i++)
            {
                Vector3 p = V(sb.Range(-hw + 1, hw - 1), .038f, sb.Range(-4.8f, 4.8f));
                float r = sb.Range(.23f, .65f);
                sb.Disc(p, r * .7f, r, 9, sb.Jitter(0x4b5130), .2f, 4.8f);
                sb.Rock(p, r * .25f, C(0x41492a), .12f, .3f, 0);
                if (i % 4 == 0) sg.Disc(p + V(0, .004f, 0), r * .75f, r * .83f, 6, glow * .34f, .5f, 2.4f);
            }
            for (int i = 0; i < 6; i++)
            {
                float x = -hw + 1.4f + i * (hw * 2 - 2.8f) / 5, z = sb.Range(-4.3f, 4.3f);
                sb.Push(); sb.Translate(x, -.52f, z); sb.RotateY(sb.Range(-.6f, .6f)); sb.RotateZ(sb.Range(-.22f, .22f));
                sb.Cylinder(Vector3.zero, .44f, .41f, .93f, 10, sb.Jitter(i % 2 == 0 ? 0x575b44 : RustDark));
                sb.Cylinder(V(0, .61f, 0), .445f, .445f, .055f, 10, C(RustDark));
                sb.Cylinder(V(0, .88f, 0), .435f, .435f, .055f, 10, C(Steel));
                sb.Disc(V(.14f, .937f, .08f), .055f, .1f, 7, C(Rust));
                sb.Disc(V(.14f, .938f, .08f), 0, .055f, 7, C(Soot));
                sb.Pop();
                sb.Disc(V(x, .036f, z), .47f, .73f, 9, C(0x171f13), .1f, 5.8f);
            }
        }

        /// Permanent frame/lights; door leaves and every attached fitting vanish together in sb.
        public static void LabDoor(ZoneWorld.Site s, MeshBuilder b, MeshBuilder g, MeshBuilder sb, float hw, Color glow)
        {
            Color stone = Masonry(s);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 10;
                b.Box(V(x, 4.5f, 0), V(2.4f, 9, 3.12f), stone * .71f);
                for (int e = -1; e <= 1; e += 2)
                {
                    for (int row = 0; row < 3; row++)
                    {
                        float y = row * 3;
                        Facing(b, x - 1.2f, x + 1.2f, y, y + 2.98f, e * 1.69f, .2f, .17f, b.Jitter(stone * .88f));
                        Stain(b, x + side * .68f, y + 2.65f, e * 1.694f, .29f, 1.4f, C(0x51554e));
                    }
                    b.Box(V(x - side * .9f, 4.3f, e * 1.63f), V(.17f, 8.6f, .14f), C(Steel));
                    for (int row = 0; row < 5; row++)
                        Bolt(b, V(x - side * .6f, .6f + row * 1.65f, e * 1.67f), V(0, 0, e), .09f);
                }
                Slab(b, x - 1.12f, x + 1.12f, -1.7f, 1.7f, 0, .18f, stone * .63f, .25f);
            }
            // Original lintel centre and opening height: 9.4 and 8.6 metres.
            b.Box(V(0, 9.4f, 0), V(22.4f, 1.6f, 3.18f), stone * .69f);
            for (int e = -1; e <= 1; e += 2)
            {
                Facing(b, -11.2f, 11.2f, 8.6f, 10.2f, e * 1.69f, .18f, .13f, stone * .84f);
                b.Box(V(0, 8.78f, e * 1.63f), V(17.2f, .19f, .14f), C(Steel));
                for (int i = -4; i <= 4; i++)
                    Stain(b, i * 2.25f, 10.08f, e * 1.695f, .25f, b.Range(.35f, .8f), C(Dark));
            }
            Slab(b, -5.2f, 4.6f, -.95f, .95f, 10.2f, 10.26f, C(Steel), .2f);
            b.Beam(V(-8, 10.28f, .9f), V(8, 10.28f, .9f), .07f, .07f, 6, C(RustDark));
            for (int i = -3; i <= 3; i++)
                b.Box(V(i * 2.2f, 10.26f, .9f), V(.12f, .2f, .32f), C(0x696c68));

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 4.3f;
                // Two 8.6 m leaves meet at zero and end exactly at x=+-8.6.
                sb.Box(V(x, 4.3f, 0), V(8.6f, 8.6f, 2.64f), C(0x41494b));
                for (int e = -1; e <= 1; e += 2)
                {
                    for (int row = 0; row < 3; row++)
                    {
                        float y = .18f + row * 2.75f;
                        Facing(sb, x - 4.12f, x + 4.12f, y, y + 2.56f, e * 1.44f, .16f, .19f,
                            sb.Jitter(row == 1 ? 0x60696b : 0x566164, .045f));
                        Stain(sb, x + side * 2.4f, y + 2.2f, e * 1.446f, .28f, 1.65f, C(0x454d49));
                        sb.Box(V(x, y + .12f, e * 1.49f), V(7.7f, .2f, .18f), C(Steel));
                        for (int k = -1; k <= 1; k += 2)
                            Bolt(sb, V(x + k * 3.65f, y + 1.4f, e * 1.49f), V(0, 0, e), .10f);
                    }
                    sb.Box(V(side * .12f, 4.3f, e * 1.48f), V(.2f, 8.5f, .2f), C(Soot));
                    for (int j = 0; j < 3; j++)
                    {
                        float y = 1.55f + j * 2.65f;
                        Facing(sb, side > 0 ? .22f : -1.18f, side > 0 ? 1.18f : -.22f,
                            y, y + .37f, e * 1.598f, .18f, .085f, C(0x717773));
                    }
                }
            }
            // Exactly two approach-side key lamps, in the permanent lintel.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 lamp = V(side * 3, 9.43f, -1.60f);
                b.Beam(lamp + V(0, 0, .16f), lamp + V(0, 0, -.097f), .37f, .31f, 8, C(Steel));
                g.Beam(lamp + V(0, 0, -.098f), lamp + V(0, 0, -.099f), .215f, .215f, 8, glow * .64f);
            }
        }
    }
}

