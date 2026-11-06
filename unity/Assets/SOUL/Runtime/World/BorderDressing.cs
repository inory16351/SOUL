using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // BORDER LOOK (Codex-owned file). The impassable ridges between zones must not read as generic
    // rock walls: they should look like the zones they separate (collapsed building rows in the ruins
    // and city, canyon cliffs in the gorge, a wall of dead trunks and roots in the woods, sunk hulks
    // in the mire, furnace slag in the foundry, a concrete security wall at the lab) and frame the
    // passages so the eye is drawn to them.
    //
    // Contract (ZoneGates / WorldStreamer call these; collision is NOT made here):
    //  - RidgeColor: colour of one ridge height-field triangle (sRGB). `p` = triangle centre (sim),
    //    `steep` 0 flat .. 1 vertical, `noise` 0..1 per triangle.
    //  - Dress: decoration mesh for the chunk at (x0, z0) (size m) or null. Put dressing only where
    //    ZoneWorld.RidgeHeight(x, z) > ZoneWorld.SOLID (inside the blocking ridge) or within 2 m of
    //    it, so it never narrows a walkable route. Use ZoneWorld.Sample(x, z, out other, out dist)
    //    to know both zones of the border and ZoneWorld.Sites / ZoneRoutes for the passages.
    //    Deterministic per chunk (seed from x0, z0): the chunk is rebuilt identically when re-streamed.
    public static class BorderDressing
    {
        static readonly Color Stone = MeshBuilder.Hex(0x5d5852), StoneTop = MeshBuilder.Hex(0x8a8580);
        static readonly int[] Material = { 0x716963, 0x64656a, 0x57535f, 0x704b36, 0x493a32, 0x49473b, 0x49513b, 0x777e80 };
        static readonly int[] Stain = { 0x35302c, 0x343337, 0x37323e, 0x492c20, 0x292321, 0x343c2b, 0x303e25, 0x465451 };
        const int Rust = 0x6b3f26, Steel = 0x4a4744, Soot = 0x2e2a27, Bark = 0x3a3430;
        const int Budget = 6000, PieceReserve = 700;

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => MeshBuilder.Hex(hex);

        public static Color RidgeColor(Vector3 p, float steep, float noise)
        {
            int zone = ZoneWorld.Sample(p.x, p.z, out int other, out float dist);
            // Half of each material at the seam, tending to the local material at the foot.
            float mix = other >= 0 && zone >= 0 ? .5f * (1 - Mathf.Clamp01(dist / ZoneWorld.RIDGE_W)) : 0;
            int local = Mathf.Clamp(zone < 0 ? other : zone, 0, 7);
            int neighbour = other < 0 ? local : Mathf.Clamp(other, 0, 7);
            Color body = Color.Lerp(C(Material[local]), C(Material[neighbour]), mix);
            Color stain = Color.Lerp(C(Stain[local]), C(Stain[neighbour]), mix);
            bool edge = zone < 0 || other < 0;
            float height = Mathf.Clamp01(p.y / (edge ? ZoneWorld.EDGE_H : ZoneWorld.RIDGE_H));
            // Long vertical runoff, offset by height; fine horizontal seams expose strata.
            float runoff = Mathf.Clamp01((Mathf.Sin(p.x * .53f + p.z * .37f + p.y * .09f) - .25f) * 1.4f);
            float seam = Mathf.Clamp01((Mathf.Sin(p.y * 2.8f + p.x * .035f + p.z * .04f) - .65f) * 2);
            body = Color.Lerp(body, stain, runoff * (.2f + .28f * (1 - height)) + seam * .13f);
            body = Color.Lerp(body, StoneTop, height * (1 - Mathf.Clamp01(steep) * .75f) * .42f);
            body *= (1 - Mathf.Clamp01(steep) * .29f) * (.92f + .16f * Mathf.Clamp01(noise));
            body.a = 1;
            return body;
        }

        public static Mesh Dress(float x0, float z0, float size)
        {
            if (size < 6) return null;
            int seed = unchecked(Mathf.RoundToInt(x0 * 16) * 73856093 ^ Mathf.RoundToInt(z0 * 16) * 19349663);
            var b = new MeshBuilder(seed);
            int n = Mathf.Max(2, Mathf.FloorToInt(size / 3));
            float step = size / n;
            var heights = new float[n * n];
            // One ~3 m height grid is shared by all placement/footprint tests.
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
                heights[j * n + i] = ZoneWorld.RidgeHeight(x0 + (i + .5f) * step, z0 + (j + .5f) * step);
            var occupied = new List<Vector3>(12); // x/z centre, y = horizontal bounding radius
            // Give mouth/spur silhouettes first claim on the finite triangle budget.
            for (int pass = 0; pass < 2; pass++)
            for (int j = 1; j < n - 1; j++) for (int i = 1; i < n - 1; i++)
            {
                if (occupied.Count >= 12 || b.TriangleCount > Budget - PieceReserve) break;
                float x = x0 + (i + .5f) * step, z = z0 + (j + .5f) * step, h = heights[j * n + i];
                if (h <= ZoneWorld.SOLID + 1) continue;
                var site = ZoneWorld.NearestSite(V(x, 0, z), 100);
                bool frame = site != null;
                if ((pass == 0) != frame || !b.Chance(frame ? .85f : .36f)) continue;
                int zone = Regions.At(x, z);
                int self = ZoneWorld.Sample(x, z, out int other, out _);
                float radius = zone == 2 ? 5.4f : 2.7f;
                bool overlap = false;
                foreach (var p in occupied)
                    if ((new Vector2(x - p.x, z - p.z)).sqrMagnitude < (radius + p.y) * (radius + p.y)) { overlap = true; break; }
                if (overlap || !Fits(x, z, radius, i, j, n, step, heights)) continue;
                // Align long pieces with the ridge tangent; spurs follow their flare.
                float dx = heights[j * n + i + 1] - heights[j * n + i - 1];
                float dz = heights[(j + 1) * n + i] - heights[(j - 1) * n + i];
                float yaw = Mathf.Atan2(dx, dz) + b.Range(-.18f, .18f);
                if (frame)
                {
                    Vector2 q = site.Local(x, z);
                    yaw = site.yaw;
                    if (Mathf.Abs(q.y) > ZoneWorld.CUT_L - 8)
                        yaw -= Mathf.Atan2(44, 30 * Mathf.Sign(q.x * q.y));
                }
                occupied.Add(V(x, radius, z));
                b.Push(); b.Translate(x, h, z); b.RotateY(yaw);
                Color rock = Color.Lerp(Stone, C(Material[Mathf.Clamp(zone, 0, 7)]), .65f);
                Footing(b, rock, radius);
                if (self < 0 || other < 0) Cliff(b, rock); // the existing edge height field supplies the tall mass
                switch (zone)
                {
                    case 0: case 1: Facade(b, zone, frame); break;
                    case 2: Tower(b); break;
                    case 3: Gorge(b, rock); break;
                    case 4: Foundry(b); break;
                    case 5: Woods(b, frame, false); break;
                    case 6: Mire(b, frame); break;
                    case 7: Security(b); break;
                }
                b.Pop();
            }
            return b.TriangleCount == 0 ? null : b.Build("zone-border-dressing");
        }

        static bool Fits(float x, float z, float radius, int i, int j, int n, float step, float[] heights)
        {
            int cells = Mathf.CeilToInt(radius / step);
            if (i < cells || j < cells || i + cells >= n || j + cells >= n) return false;
            // The whole surrounding square must be solid, not merely the object's pivot.
            // Extra height clearance keeps pieces away from the foot of the smooth ridge.
            float pivot = heights[j * n + i];
            for (int dz = -cells; dz <= cells; dz++) for (int dx = -cells; dx <= cells; dx++)
            {
                float h = heights[(j + dz) * n + i + dx];
                if (h <= ZoneWorld.SOLID + 1 || h - pivot > 1.8f) return false;
            }
            if (ZoneWorld.Opening(x, z, out _) > 0 || ZoneRoutes.Dist(x, z) < 6 + radius) return false;
            foreach (var s in ZoneWorld.Sites)
            {
                Vector2 q = s.Local(x, z);
                // Opening has positive support in this rectangle. Expand by the full
                // silhouette radius, including leaning tops, pipes, roots and rubble.
                if (Mathf.Abs(q.x) < ZoneWorld.HALF_W + 5 + radius &&
                    Mathf.Abs(q.y) < ZoneWorld.CUT_L + 8 + radius) return false;
            }
            return true;
        }

        // Local height relative to the assembly pivot, after its sim-space yaw.
        static float Ground(MeshBuilder b, float x, float z)
        {
            Vector3 p = b.Apply(V(x, 0, z));
            return ZoneWorld.RidgeHeight(p.x, p.z) - p.y;
        }

        static void Footing(MeshBuilder b, Color color, float radius)
        {
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * .5f + b.Range(-.2f, .2f), reach = radius * .3f;
                float x = Mathf.Sin(a) * reach, z = Mathf.Cos(a) * reach;
                b.Rock(V(x, Ground(b, x, z) + .08f, z), .65f, b.Jitter(color), .18f, .65f, 0);
            }
        }

        // Chipped rectangular slab with eight corners; CCW as seen from above.
        static void Slab(MeshBuilder b, Vector3 center, float width, float depth, float thick, Color color, float tilt = 0)
        {
            b.Push(); b.Translate(center); b.RotateZ(tilt);
            const int count = 8;
            var top = new Vector3[count]; var bottom = new Vector3[count];
            for (int k = 0; k < count; k++)
            {
                float a = (k + .5f) * Mathf.PI * .25f;
                float sx = Mathf.Sin(a), sz = Mathf.Cos(a), r = b.Range(.84f, 1);
                float m = Mathf.Max(Mathf.Abs(sx), Mathf.Abs(sz));
                top[k] = V(sx / m * width * .5f * r, thick * .5f, sz / m * depth * .5f * r);
                bottom[k] = top[k] - V(0, thick, 0);
            }
            for (int k = 0; k < count; k++)
            {
                int next = (k + 1) % count;
                b.Tri(V(0, thick * .5f, 0), top[k], top[next], b.Jitter(color, .04f));
                b.Tri(V(0, -thick * .5f, 0), bottom[next], bottom[k], color * .65f);
                b.Quad(bottom[k], bottom[next], top[next], top[k], b.Jitter(color * .8f));
            }
            b.Pop();
        }

        static void Cliff(MeshBuilder b, Color rock)
        {
            // Narrow tall split teeth, embedded in the already 24 m world-edge cliffs.
            for (int k = 0; k < 2; k++)
            {
                float x = k == 0 ? -.8f : .9f;
                b.Push(); b.Translate(x, Ground(b, x, .4f) + 1.1f, .4f); b.Scale(.75f, 3.1f + k, .8f);
                b.Rock(Vector3.zero, 1, b.Jitter(rock), .16f, 1, 0); b.Pop();
            }
        }

        static void Facade(MeshBuilder b, int zone, bool frame)
        {
            Color concrete = b.Jitter(zone == 0 ? 0x8a8580 : 0x77787b);
            float height = frame ? 4.8f : b.Range(2.8f, 4.1f);
            // Individual piers leave real window voids. The upper wall leans inwards.
            for (int k = -1; k <= 1; k++)
            {
                float x = k * 1.4f, baseY = Ground(b, x, 0) - .35f;
                Vector3 a = V(x, baseY, 0), c = V(x + .22f, height - Mathf.Abs(k) * .45f, .22f);
                b.Beam(a, c, .34f, .24f, 4, b.Jitter(concrete));
                b.Beam(c, c + V(.15f, .65f, -.08f), .035f, .023f, 4, C(Rust));
                b.Beam(Vector3.Lerp(a, c, .25f) + V(0, 0, -.29f), c + V(0, -.3f, -.23f), .07f, .12f, 3, C(Soot));
            }
            for (int floor = 0; floor < 3; floor++)
                Slab(b, V(.08f * floor, .3f + floor * height * .43f, .1f), 4, 1.75f - floor * .22f, .25f, concrete, floor % 2 == 0 ? .09f : -.12f);
            // A broken sill and a toppled street lamp lie across the exposed top rubble.
            Slab(b, V(-.4f, .7f, -.9f), 2.2f, .65f, .22f, concrete * .78f, -.3f);
            b.Beam(V(-1.6f, .6f, .85f), V(.9f, 1.05f, 1.1f), .07f, .045f, 5, C(Steel));
            b.Beam(V(.9f, 1.05f, 1.1f), V(1.45f, .85f, 1.1f), .045f, .035f, 4, C(Rust));
            b.Box(V(1.55f, .82f, 1.1f), V(.45f, .18f, .3f), C(Soot));
        }

        static void Tower(MeshBuilder b)
        {
            // Fallen tower slice: repeated storeys lie across an eight metre spine.
            Color concrete = b.Jitter(0x77717c);
            for (int k = 0; k < 5; k++)
            {
                float x = -3.6f + k * 1.8f;
                b.Push(); b.Translate(x, 1.25f + k * .12f, 0); b.RotateZ(1.36f);
                Slab(b, Vector3.zero, 3.4f, 3.1f, .22f, concrete); b.Pop();
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Beam(V(x, Ground(b, x, side), side), V(x, 1.7f, side), .13f, .1f, 4, C(Steel));
                    if (k < 4) b.Beam(V(x, 2.3f, side * 1.15f), V(x + 1.8f, 2.5f, side * 1.15f), .12f, .1f, 4, C(Rust));
                }
            }
            b.Beam(V(-3.7f, .9f, -.8f), V(4.4f, 3.6f, -.7f), .16f, .08f, 4, C(Steel));
            b.Beam(V(1.8f, 1.7f, .4f), V(3.9f, 4.8f, .8f), .12f, .045f, 4, C(Rust));
        }

        static void Gorge(MeshBuilder b, Color rock)
        {
            for (int k = 0; k < 4; k++)
                Slab(b, V((k % 2) * .16f, k * .55f, 0), 4.1f - k * .25f, 2.4f, .62f,
                    b.Jitter(k % 2 == 0 ? rock : rock * .7f), .04f);
            Slab(b, V(.2f, 1.9f, -.65f), 2.7f, .8f, .08f, C(Rust), -.24f);
            b.Push(); b.Translate(-.6f, 2.3f, .2f); b.RotateZ(-1.25f);
            Pipe(b, .38f, 2.1f, C(Rust)); b.Pop();
            b.Beam(V(-1.6f, 1.4f, -.9f), V(.9f, 2.8f, -.65f), .1f, .06f, 4, C(Steel));
        }

        // Open chimney/tank/pipe with dark inner walls and uneven rim, no glowing cap.
        static void Pipe(MeshBuilder b, float radius, float height, Color color)
        {
            const int count = 8;
            for (int k = 0; k < count; k++)
            {
                float a = k * Mathf.PI * 2 / count, next = (k + 1) * Mathf.PI * 2 / count;
                Vector3 u = V(Mathf.Sin(a), 0, Mathf.Cos(a)), v = V(Mathf.Sin(next), 0, Mathf.Cos(next));
                float h0 = height * (k % 3 == 0 ? .88f : 1), h1 = height * (((k + 1) % count) % 3 == 0 ? .88f : 1);
                Vector3 lo = u * radius, ln = v * radius, hi = lo * .91f + V(0, h0, 0), hn = ln * .91f + V(0, h1, 0);
                Vector3 inner = hi - u * .12f, inn = hn - v * .12f;
                b.Quad(lo, ln, hn, hi, b.Jitter(k % 3 == 0 ? color * .65f : color));
                b.Quad(hi, hn, inn, inner, color * 1.1f);
                b.Quad(inner, inn, ln * .7f, lo * .7f, C(Soot));
            }
            b.Disc(V(0, .04f, 0), 0, radius * .7f, count, C(Soot));
        }

        static void Foundry(MeshBuilder b)
        {
            b.Rock(V(0, .35f, 0), 1.6f, C(0x332b29), .18f, .7f, 0);
            b.Push(); b.Translate(-.7f, Ground(b, -.7f, 0) - .2f, 0); Pipe(b, .65f, 3.6f, C(0x554139)); b.Pop();
            for (int side = -1; side <= 1; side += 2)
            {
                b.Beam(V(-1.5f, 1.1f, side * .8f), V(1.5f, 2.4f, side * .8f), .095f, .095f, 4, C(Rust));
                b.Beam(V(1.5f, Ground(b, 1.5f, side * .8f), side * .8f), V(1.5f, 2.4f, side * .8f), .08f, .08f, 4, C(Steel));
            }
            for (int k = 0; k < 5; k++)
            {
                float x = -1.3f + k * .65f, y = 1.2f + k * .28f;
                b.Beam(V(x, y, -.8f), V(x + .1f, y, .8f), .12f, .12f, 6, C(k % 2 == 0 ? Soot : Steel));
            }
        }

        static void Woods(MeshBuilder b, bool frame, bool drowned)
        {
            float h = frame ? 6.5f : b.Range(3.8f, 5.4f);
            Color bark = C(drowned ? 0x454a38 : Bark);
            for (int k = 0; k < 2; k++)
            {
                float x = k == 0 ? -.65f : .8f, top = h * (k == 0 ? 1 : .65f);
                Vector3 mid = V(x + .18f, top * .55f, .1f), tip = V(x + .5f, top, .25f);
                b.Beam(V(x, Ground(b, x, 0) - .3f, 0), mid, .38f, .24f, 6, bark);
                b.Beam(mid, tip, .24f, .07f, 5, bark * 1.15f);
                for (int s = -1; s <= 1; s += 2)
                {
                    b.Beam(mid, V(x + s * .9f, top * .8f, s * .8f), .13f, .035f, 4, bark);
                    b.Beam(V(x, .35f, 0), V(x + s * .75f, Ground(b, x + s * .75f, s * .65f), s * .65f), .2f, .035f, 5, bark);
                }
            }
            b.Beam(V(-1.7f, Ground(b, -1.7f, -.6f) + .2f, -.6f), V(1.5f, Ground(b, 1.5f, -.4f) + .2f, -.4f), .3f, .19f, 7, bark);
        }

        static void Mire(MeshBuilder b, bool frame)
        {
            if (frame || b.Chance(.3f)) { Woods(b, frame, true); return; }
            if (b.Chance(.45f))
            {
                b.Push(); b.Translate(-.3f, -.3f, 0); b.RotateZ(.18f); Pipe(b, 1.1f, 2.5f, C(Rust));
                b.Cylinder(V(0, .7f, 0), 1.06f, 1.04f, .18f, 8, C(0x3c4630), false);
                b.Cylinder(V(0, 1.65f, 0), 1.02f, 1, .12f, 8, C(Steel), false); b.Pop();
                return;
            }
            // Open, sunken hull: narrow keel, splayed sides and exposed transverse ribs.
            Slab(b, V(0, -.1f, 0), 3.8f, 1.2f, .18f, C(Soot));
            for (int side = -1; side <= 1; side += 2)
            {
                b.Push(); b.Translate(0, .55f, side * .7f); b.RotateX(side * .32f);
                Slab(b, Vector3.zero, 4, .16f, 1.25f, C(Rust)); b.Pop();
            }
            for (int k = -1; k <= 1; k++)
            {
                b.Beam(V(k, -.05f, 0), V(k, 1.25f, .9f), .09f, .06f, 4, C(Steel));
                b.Beam(V(k, -.05f, 0), V(k, 1.1f, -.9f), .09f, .06f, 4, C(Steel));
            }
            Slab(b, V(.3f, .06f, 0), 2.5f, 1.3f, .06f, C(0x3c4630));
        }

        static void Security(MeshBuilder b)
        {
            Color concrete = b.Jitter(0x858b8b);
            // Separated panels have unequal broken crowns; fence continues above them.
            for (int k = -1; k <= 1; k++)
            {
                float x = k * 1.25f, bottom = Ground(b, x, 0) - .3f, top = 2.8f - Mathf.Abs(k) * .35f;
                Slab(b, V(x, (bottom + top) * .5f, 0), 1.2f, .5f, top - bottom, concrete);
                b.Beam(V(x, top, 0), V(x + .08f, 4.1f, .15f), .045f, .04f, 4, C(Steel));
                b.Box(V(x + .25f, top - .55f, -.26f), V(.1f, .9f, .025f), C(0x4b5552));
            }
            for (int k = 0; k < 5; k++)
            {
                float x = -1.2f + k * .48f;
                b.Beam(V(x, 2.85f, .1f), V(x + .45f, 3.95f, .14f), .018f, .018f, 3, C(Steel));
                b.Beam(V(x, 3.95f, .14f), V(x + .45f, 2.85f, .1f), .018f, .018f, 3, C(Steel));
            }
            b.Beam(V(-1.2f, 4.1f, .15f), V(1.35f, 4.1f, .15f), .025f, .025f, 4, C(Rust));
            b.Beam(V(.08f, 3.8f, .15f), V(.45f, 3.75f, -.6f), .05f, .05f, 4, C(Steel));
            b.Box(V(.45f, 3.72f, -.7f), V(.36f, .26f, .55f), C(0x737d7c));
            b.Box(V(.45f, 3.69f, -.983f), V(.18f, .14f, .025f), C(Soot));
        }
    }
}
