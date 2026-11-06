using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Second, purely decorative "ruin" pass over the climbable structures: broken wall tops,
    // rebar bundles, slab-edge teeth and drooping plates, rubble heaps, soot/rust stains and
    // ground grime. Everything is authored in sim space at the structure's root frame and is
    // called from inside RuinStructures.Detail, so the layout random stream is untouched.
    //
    // Safety: every shape is tested against a Guard built from the structure's own walkable
    // surfaces and wall colliders. A shape may not enter the space a standing character could
    // occupy (0.2 m .. 2.05 m above any reachable floor, unless walls keep the body away), so
    // decoration never needs collision and never blocks a walking face. Only the few explicit
    // FallenSlab / LeaningStub pieces register new (ground-level) data.
    internal static class RuinDressing
    {
        const int Concrete = 0x8f8b86, Weathered = 0x7d7a75, Dark = 0x6a6762, Pale = 0xa29d94;
        const int Interior = 0x4c4945, Char = 0x252728, Soot = 0x34312e, Rust = 0x78432c;
        const int RustDeep = 0x5a3424, Steel = 0x44494b;
        // weathering tints, kept close to slab concrete (0x8f8b86) and the ruins ground
        const int SlabSoot = 0x6c6863, SlabDirt = 0x817d76, SlabDust = 0x96918a;
        const int GroundScorch = 0x48443f, GroundDirt = 0x5a554e, GroundDust = 0x77716a;
        const int RustStain = 0x6e5546, MossStain = 0x666b5e;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => MeshBuilder.Hex(hex);

        // ---------------------------------------------------------------- guard
        internal class Guard
        {
            readonly List<WorldCollision.Surf> surfs = new List<WorldCollision.Surf>();
            readonly List<WorldCollision.Col> cols = new List<WorldCollision.Col>();
            const float BodyLow = .2f, BodyHigh = 2.05f, WallSlack = .3f;
            public Guard(PropModel p) { Refresh(p); }
            public void Refresh(PropModel p)
            {
                surfs.Clear(); cols.Clear();
                foreach (var s in p.surfaces) surfs.Add(WorldCollision.ToWorld(s, Vector3.zero, 0, 1));
                foreach (var c in p.colliders) cols.Add(WorldCollision.ToWorld(c, Vector3.zero, 0, 1));
            }
            static float SurfHeight(in WorldCollision.Surf s, float x, float z)
            {
                float wx = x - s.c.x, wz = z - s.c.y;
                if (s.kind == PropSurface.MOUND)
                {
                    float d2 = (wx * wx + wz * wz) / (s.r * s.r); return d2 >= 1 ? float.NegativeInfinity : s.top * (1 - d2);
                }
                float lx = wx * s.cos - wz * s.sin, lz = wx * s.sin + wz * s.cos;
                if (Mathf.Abs(lx) > s.half.x || Mathf.Abs(lz) > s.half.y) return float.NegativeInfinity;
                if (s.kind == PropSurface.BOX) return s.top;
                return Mathf.Lerp(s.yLow, s.yHigh, (lz + s.half.y) / (2 * s.half.y));
            }
            static float WallDistance(in WorldCollision.Col c, float x, float z)
            {
                float wx = x - c.c.x, wz = z - c.c.y;
                if (!c.box) return Mathf.Sqrt(wx * wx + wz * wz) - c.r;
                float lx = wx * c.cos - wz * c.sin, lz = wx * c.sin + wz * c.cos;
                float ex = Mathf.Max(0, Mathf.Abs(lx) - c.half.x), ez = Mathf.Max(0, Mathf.Abs(lz) - c.half.y);
                return Mathf.Sqrt(ex * ex + ez * ez);
            }
            // walls active for feet at footY keep a body centre away from (x, z)
            bool Walled(float x, float z, float foot)
            {
                foreach (var c in cols)
                {
                    if (c.ranged && (foot + WorldCollision.STEP >= c.y1 || foot < c.y0 - .25f)) continue;
                    if (WallDistance(c, x, z) < WallSlack) return true;
                }
                return false;
            }
            /// could a standing character's body touch this point?
            public bool Body(Vector3 q)
            {
                if (q.y > BodyLow && q.y < BodyHigh && !Walled(q.x, q.z, 0)) return true;
                foreach (var s in surfs)
                {
                    float h = SurfHeight(s, q.x, q.z);
                    if (h == float.NegativeInfinity) continue;
                    if (q.y > h + BodyLow && q.y < h + BodyHigh && !Walled(q.x, q.z, h)) return true;
                }
                return false;
            }
            /// highest floor (ground = 0) at (x, z) not above `y`
            public float FloorAt(float x, float z, float y)
            {
                float best = 0;
                foreach (var s in surfs)
                {
                    float h = SurfHeight(s, x, z);
                    if (h > best && h <= y + .05f) best = h;
                }
                return best;
            }
            /// a surface at (x, z) whose top is within `tol` of y (continuous floor, not an edge)
            public bool SurfaceNear(float x, float z, float y, float tol = .12f)
            {
                foreach (var s in surfs)
                {
                    float h = SurfHeight(s, x, z);
                    if (h != float.NegativeInfinity && Mathf.Abs(h - y) < tol) return true;
                }
                return false;
            }
        }

        // ---------------------------------------------------------------- primitives
        static Color Pick(MeshBuilder b, float charChance = .12f, float rustChance = .06f)
        {
            float r = b.Rand();
            if (r < charChance) return b.Jitter(Char, .12f);
            if (r < charChance + rustChance) return b.Jitter(Rust, .12f);
            r = b.Rand();
            return b.Jitter(r < .4f ? Concrete : r < .72f ? Weathered : r < .95f ? Dark : Pale, .1f);
        }
        static Vector3 RandomAxis(MeshBuilder b, float yBias = .25f)
        {
            var a = V(b.Range(-1, 1), b.Range(-yBias, yBias), b.Range(-1, 1));
            return a.sqrMagnitude < 1e-4f ? Vector3.right : a.normalized;
        }

        /// rotated box; rejected (and not drawn) if any sample reaches a standing body
        static bool Block(MeshBuilder b, Guard g, Vector3 c, Vector3 size, Vector3 axis, float angle, Color col)
        {
            var q = Sim.AxisAngle(axis, angle); Vector3 h = size * .5f;
            if (g != null)
                for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++) for (int k = -1; k <= 1; k++)
                    if (g.Body(c + q * V(i * h.x, j * h.y, k * h.z))) return false;
            b.Push(); b.Translate(c); b.Rotate(axis, angle); b.Box(Vector3.zero, size, col, .1f); b.Pop();
            return true;
        }
        static bool Rod(MeshBuilder b, Guard g, Vector3 a, Vector3 c, float r, int hex = Rust)
        {
            if (g != null && (g.Body(a) || g.Body(c) || g.Body((a + c) * .5f))) return false;
            b.Beam(a, c, r, r * .8f, 3, C(hex)); return true;
        }
        /// bent bar: a -> bend -> end, both legs guarded together
        static bool Bar(MeshBuilder b, Guard g, Vector3 a, Vector3 bend, Vector3 end, float r = .02f)
        {
            if (g != null && (g.Body(a) || g.Body(bend) || g.Body(end) || g.Body((a + bend) * .5f) || g.Body((bend + end) * .5f))) return false;
            b.Beam(a, bend, r, r * .9f, 3, C(Rust)); b.Beam(bend, end, r * .9f, r * .6f, 3, C(b.Chance(.5f) ? Rust : RustDeep));
            return true;
        }
        /// flat irregular patch facing +Y (ground grime, soot, oil, dust)
        static void Patch(MeshBuilder b, float x, float y, float z, float r, Color col, int sides = 7)
        {
            var pts = new Vector3[sides]; float a0 = b.Range(0, 6.3f);
            for (int i = 0; i < sides; i++)
            {
                float a = a0 + i * Mathf.PI * 2 / sides, rr = r * b.Range(.55f, 1.1f);
                pts[i] = V(x + Mathf.Sin(a) * rr, y, z + Mathf.Cos(a) * rr * b.Range(.7f, 1));
            }
            var c = V(x, y, z);
            for (int i = 0; i < sides; i++) b.Tri(c, pts[i], pts[(i + 1) % sides], col);
        }
        /// vertical stain on a wall face, `n` is the face's outward normal (horizontal)
        static void Stain(MeshBuilder b, Vector3 baseCentre, Vector3 n, float w, float h, Color col, float taper = .6f)
        {
            Vector3 t = Vector3.Cross(Vector3.up, n).normalized, u = Vector3.up, o = baseCentre + n * .004f;
            // wider at the top (where soot / rust leaks from), tapering down
            Vector3 a = o - t * w * .5f * taper, c = o + t * w * .5f * taper;
            Vector3 d = o + t * w * .5f + u * h, e = o - t * w * .5f + u * h;
            b.Quad(a, c, d, e, col);
        }

        // ---------------------------------------------------------------- composites
        /// jagged broken masonry along a wall top, with rebar bundles
        internal static void BrokenTop(MeshBuilder b, Guard g, Vector3 a, Vector3 c, float thick, float rough = 1)
        {
            Vector3 d = c - a; float len = d.magnitude; if (len < .2f) return; d /= len;
            Vector3 n = Vector3.Cross(Vector3.up, d).normalized;
            bool alongX = Mathf.Abs(d.x) > Mathf.Abs(d.z);
            float level = b.Range(0, .5f), t = 0;
            while (t < len)
            {
                // a random walk of masonry heights, with clean breaks: the profile reads as a
                // torn edge rather than a row of crenellations
                float step = b.Range(.35f, 1.3f), L = Mathf.Min(step, len - t), m = t + L * .5f;
                level = Mathf.Lerp(level, b.Range(-.1f, 1.15f), .65f);
                float h = b.Chance(.16f) ? 0 : level * rough;
                if (h > .07f)
                {
                    float th = thick * b.Range(.72f, 1);
                    float len2 = L * b.Range(.7f, .98f);
                    Vector3 size = alongX ? V(len2, h + .15f, th) : V(th, h + .15f, len2);
                    Vector3 centre = a + d * m + n * b.Range(-.035f, .035f) + Vector3.up * (h * .5f - .08f);
                    // rotating about the wall normal slants the broken top (lower corner sinks into the wall)
                    float slant = b.Range(-.55f, .55f) * Mathf.Clamp01(1.2f - h);
                    Block(b, g, centre, size, n, slant, Pick(b, .2f, .02f));
                    if (b.Chance(.45f)) // narrower spall stacked off-centre
                    {
                        float h2 = h * b.Range(.3f, .7f);
                        Vector3 s2 = alongX ? V(L * b.Range(.25f, .5f), h2, th * .6f) : V(th * .6f, h2, L * b.Range(.25f, .5f));
                        Block(b, g, centre + Vector3.up * (h * .5f + h2 * .35f) + d * b.Range(-.3f, .3f) * L + n * b.Range(-.06f, .06f),
                            s2, RandomAxis(b, .3f), b.Range(.15f, .6f), Pick(b, .3f, .03f));
                    }
                }
                else if (b.Chance(.5f)) // the break exposes the wall core
                {
                    Vector3 size = alongX ? V(L * .8f, .05f, thick * .8f) : V(thick * .8f, .05f, L * .8f);
                    Block(b, g, a + d * m + Vector3.up * .005f, size, Vector3.up, 0, b.Jitter(Interior, .08f));
                }
                if (b.Chance(.26f * rough))
                {
                    int bars = b.RangeInt(2, 5);
                    for (int i = 0; i < bars; i++)
                    {
                        Vector3 root = a + d * (m + (i - bars * .5f) * .09f) + n * b.Range(-.08f, .08f);
                        float rise = b.Range(.35f, 1.1f);
                        Vector3 bend = root + Vector3.up * rise + d * b.Range(-.12f, .12f) + n * b.Range(-.1f, .1f);
                        Vector3 end = bend + V(b.Range(-.35f, .35f), b.Range(-.25f, .1f), b.Range(-.35f, .35f));
                        Bar(b, g, root, bend, end, .018f);
                    }
                }
                t += step;
            }
        }

        /// soot band under a broken top, both faces of a wall of thickness `thick`
        static void SootBand(MeshBuilder b, Vector3 a, Vector3 c, float top, float thick)
        {
            Vector3 d = c - a; float len = d.magnitude; if (len < .2f) return; d /= len;
            Vector3 n = Vector3.Cross(Vector3.up, d).normalized;
            for (int side = -1; side <= 1; side += 2)
                for (float t = 0; t < len - .1f;)
                {
                    float w = Mathf.Min(b.Range(.4f, 1.1f), len - t), h = b.Range(.15f, .42f);
                    Vector3 p = a + d * (t + w * .5f) + n * side * (thick * .5f + .002f) + Vector3.up * (top - h);
                    Stain(b, p, n * side, w, h, b.Jitter(b.Chance(.6f) ? Char : Soot, .1f), b.Range(.3f, .9f));
                    t += w;
                }
        }

        /// rebar cage and a sheared chunk on top of a broken column
        static void ColumnTop(MeshBuilder b, Guard g, float x, float z, float top, float w)
        {
            for (int i = 0; i < 4; i++)
            {
                if (b.Chance(.2f)) continue;
                float sx = (i & 1) == 0 ? -1 : 1, sz = (i & 2) == 0 ? -1 : 1;
                Vector3 root = V(x + sx * w * .32f, top - .05f, z + sz * w * .32f);
                Vector3 bend = root + V(sx * b.Range(0, .12f), b.Range(.4f, 1.1f), sz * b.Range(0, .12f));
                Bar(b, g, root, bend, bend + V(sx * b.Range(.1f, .4f), b.Range(-.3f, 0), sz * b.Range(.1f, .4f)), .022f);
            }
            if (b.Chance(.7f))
                Block(b, g, V(x + b.Range(-.08f, .08f), top + .13f, z + b.Range(-.08f, .08f)),
                    V(w * b.Range(.6f, .95f), b.Range(.18f, .35f), w * b.Range(.6f, .95f)), RandomAxis(b, .2f), b.Range(.15f, .45f), Pick(b, .2f));
            if (b.Chance(.5f)) // a stirrup ring that slipped down
                Rod(b, g, V(x - w * .3f, top + .25f, z + w * .3f), V(x + w * .3f, top + .3f, z + w * .3f), .012f, RustDeep);
        }

        /// broken slab edge: hanging teeth, protruding bent rebar, crumble on top, drooping plates
        static void SlabEdge(MeshBuilder b, Guard g, Vector3 a, Vector3 c, float top, float thick, float broken = 1)
        {
            // `a -> c` runs with the slab on its LEFT seen from above; outward normal = Cross(up, d)
            Vector3 d = c - a; float len = d.magnitude; if (len < .3f) return; d /= len;
            Vector3 n = Vector3.Cross(Vector3.up, d).normalized;
            bool alongX = Mathf.Abs(d.x) > Mathf.Abs(d.z);
            Vector3 up = Vector3.up;
            for (float t = b.Range(0, .3f); t < len;)
            {
                float step = b.Range(.3f, .7f) / Mathf.Max(.6f, broken), L = Mathf.Min(step, len - t);
                Vector3 p = a + d * (t + L * .5f);
                // a seam between two slab pieces is not a broken edge
                Vector3 probe = p + n * .15f;
                if (g.SurfaceNear(probe.x, probe.z, top)) { t += step; continue; }
                // ragged tooth below the slab soffit
                if (b.Chance(.75f))
                {
                    float hang = b.Range(.08f, .35f) * broken, depth = b.Range(.14f, .3f);
                    Vector3 size = alongX ? V(L * .8f, hang, depth) : V(depth, hang, L * .8f);
                    Block(b, g, p + n * b.Range(-.1f, .06f) + up * (top - thick - hang * .5f + .04f), size,
                        d, b.Range(-.35f, .35f), Pick(b, .15f, .05f));
                }
                // protruding bars, bent down by the collapse
                if (b.Chance(.45f * broken))
                {
                    Vector3 root = p + up * (top - thick * .55f);
                    Vector3 bend = root + n * b.Range(.15f, .45f) * broken + d * b.Range(-.1f, .1f) + up * b.Range(-.06f, .04f);
                    Vector3 end = bend + n * b.Range(0, .2f) + d * b.Range(-.15f, .15f) - up * b.Range(.15f, .6f) * broken;
                    if (g.FloorAt(end.x, end.z, end.y) < end.y - .12f) Bar(b, g, root, bend, end, .018f);
                }
                // crumbled lip lying on the slab (low enough to stand on)
                if (b.Chance(.3f))
                    Block(b, g, p - n * b.Range(.08f, .35f) + up * (top + .035f),
                        V(b.Range(.12f, .3f), b.Range(.05f, .11f), b.Range(.1f, .25f)), up, b.Range(0, 3), Pick(b, .1f));
                // a broken-off plate still hinged on its bars, drooping into the void
                if (b.Chance(.1f * broken))
                {
                    float l = b.Range(.5f, 1.1f), w = b.Range(.6f, 1.3f), ang = b.Range(.5f, 1.05f);
                    float sign = Vector3.Dot(Vector3.Cross(d, n), up) > 0 ? -1 : 1;
                    Vector3 hinge = p + up * (top - thick * .5f);
                    Vector3 centre = hinge + n * (Mathf.Cos(ang) * l * .5f) - up * (Mathf.Sin(ang) * l * .5f);
                    Vector3 size = alongX ? V(w, thick * .8f, l) : V(l, thick * .8f, w);
                    if (Block(b, g, centre, size, d, sign * ang, Pick(b, .2f)))
                        for (int i = -1; i <= 1; i += 2)
                            b.Beam(hinge + d * i * w * .3f, centre + d * i * w * .3f - up * .02f, .015f, .012f, 3, C(Rust));
                }
                t += step;
            }
        }

        /// rubble heap: tall angular chunks where walls keep bodies away, flat shards elsewhere
        internal static void Heap(MeshBuilder b, Guard g, float cx, float cz, float floor, float radius, float height, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a = b.Range(0, Mathf.PI * 2), r = radius * Mathf.Sqrt(b.Rand());
                float x = cx + Mathf.Sin(a) * r, z = cz + Mathf.Cos(a) * r;
                float fl = g.FloorAt(x, z, floor + .05f);
                if (Mathf.Abs(fl - floor) > .06f) continue;
                float k = 1 - r / radius;
                float sx = b.Range(.16f, .5f) * (.6f + k), sz = b.Range(.16f, .5f) * (.6f + k);
                float h = Mathf.Max(.08f, height * k * b.Range(.4f, 1.1f));
                float sy = Mathf.Min(h, b.Range(.14f, .45f));
                var col = Pick(b, .14f, .07f);
                bool slab = b.Chance(.15f);
                if (slab) { sx *= 2.2f; sz *= 1.6f; sy = b.Range(.08f, .16f); }
                if (!Block(b, g, V(x, floor + h - sy * .5f, z), V(sx, sy, sz), RandomAxis(b, slab ? .1f : .4f),
                        b.Range(.1f, slab ? .6f : 1.2f), col))
                    Block(b, g, V(x, floor + .045f, z), V(sx * .8f, b.Range(.05f, .1f), sz * .8f), Vector3.up, b.Range(0, 3), col);
                if (i % 9 == 4)
                {
                    Vector3 root = V(x, floor + h * .6f, z);
                    Bar(b, g, root, root + V(b.Range(-.4f, .4f), b.Range(.1f, .5f), b.Range(-.4f, .4f)),
                        root + V(b.Range(-.6f, .6f), b.Range(.0f, .2f), b.Range(-.6f, .6f)), .016f);
                }
            }
            Patch(b, cx, floor + .02f, cz, radius * .9f, b.Jitter(floor < .1f ? GroundDust : SlabDust, .05f), 9);
        }

        /// sagging rebar mat still spanning a hole between two slab pieces (runs along x)
        static void RebarNet(MeshBuilder b, Guard g, float x0, float z0, float x1, float z1, float y)
        {
            for (float z = z0 + b.Range(.08f, .2f); z < z1 - .05f; z += b.Range(.18f, .3f))
            {
                float sag = b.Range(.12f, .45f), xm = (x0 + x1) * .5f + b.Range(-.2f, .2f);
                Vector3 a = V(x0 - .05f, y, z), c = V(x1 + .05f, y, z + b.Range(-.05f, .05f)), m = V(xm, y - sag, z + b.Range(-.08f, .08f));
                if (b.Chance(.3f)) // snapped: two stubs bent down
                {
                    Bar(b, g, a, a + V((xm - x0) * .5f, -.05f, 0), a + V((xm - x0) * .6f, -sag * 1.6f, b.Range(-.1f, .1f)), .016f);
                    Bar(b, g, c, c - V((x1 - xm) * .4f, .05f, 0), c - V((x1 - xm) * .5f, sag * 1.4f, b.Range(-.1f, .1f)), .016f);
                }
                else Bar(b, g, a, m, c, .016f);
            }
            for (float x = x0 + b.Range(.2f, .4f); x < x1 - .1f; x += b.Range(.35f, .6f))
                if (b.Chance(.6f)) Rod(b, g, V(x, y - .12f, z0 - .05f), V(x + b.Range(-.1f, .1f), y - .25f, z1 + .05f), .013f, RustDeep);
        }

        /// spalled patch in a walkable slab: dark aggregate with the top bar mat exposed (flush)
        static void Spall(MeshBuilder b, float x, float z, float top, float w, float d)
        {
            Patch(b, x, top + .006f, z, Mathf.Min(w, d) * .5f, b.Jitter(Interior, .06f), 8);
            Patch(b, x + b.Range(-.1f, .1f), top + .009f, z + b.Range(-.1f, .1f), Mathf.Min(w, d) * .38f, b.Jitter(0x56524c, .06f), 7);
            for (float u = -w * .32f; u <= w * .32f; u += .2f)
                b.Box(V(x + u, top + .018f, z), V(.025f, .018f, d * b.Range(.45f, .7f)), b.Jitter(Rust, .12f), .02f);
            for (float v = -d * .3f; v <= d * .3f; v += .2f)
                b.Box(V(x, top + .03f, z + v), V(w * b.Range(.45f, .7f), .018f, .025f), b.Jitter(RustDeep, .12f), .02f);
        }

        /// flattened rooftop junk (all under 0.2 m): a lying pipe, corrugated sheet, crushed tank
        static void RoofJunk(MeshBuilder b, Guard g, float x, float z, float top)
        {
            float yaw = b.Range(0, Mathf.PI);
            b.Push(); b.Translate(x, top, z); b.RotateY(yaw);
            float k = b.Rand();
            if (k < .4f)
            {
                b.Beam(V(-.8f, .075f, 0), V(.75f, .075f, b.Range(-.3f, .3f)), .07f, .07f, 6, C(b.Chance(.5f) ? Steel : Rust));
                b.Beam(V(.75f, .075f, 0), V(1.1f, .05f, .3f), .07f, .06f, 6, C(RustDeep));
            }
            else if (k < .75f)
                for (int i = 0; i < 6; i++)
                    b.Box(V(-.36f + i * .145f, .02f + (i % 2) * .03f, b.Range(-.05f, .05f)), V(.13f, .02f, b.Range(1.1f, 1.5f)),
                        b.Jitter(i % 3 == 0 ? RustDeep : Rust, .1f), .02f);
            else
            {
                b.Push(); b.Scale(1, .25f, 1); b.Cylinder(Vector3.zero, .52f, .46f, .7f, 8, b.Jitter(Steel, .1f), true, .12f); b.Pop();
                b.Box(V(.55f, .03f, .2f), V(.3f, .04f, .5f), C(Rust));
            }
            b.Pop();
        }

        /// grime, soot and oil patches spread over a rectangle of floor
        static void Grime(MeshBuilder b, Guard g, float x, float z, float w, float d, float floor, int n, bool scorched = true)
        {
            for (int i = 0; i < n; i++)
            {
                float px = x + b.Range(-.5f, .5f) * w, pz = z + b.Range(-.5f, .5f) * d;
                if (Mathf.Abs(g.FloorAt(px, pz, floor + .05f) - floor) > .05f) continue;
                // tones stay close to the surface so stains read as weathering, not holes
                bool ground = floor < .1f;
                float r = ground ? b.Range(.4f, 1.4f) : b.Range(.3f, .9f); float pick = b.Rand();
                int hex = pick < (scorched ? .35f : .12f) ? (ground ? GroundScorch : SlabSoot)
                    : pick < .7f ? (ground ? GroundDirt : SlabDirt) : pick < .88f ? (ground ? GroundDust : SlabDust)
                    : pick < .96f ? RustStain : MossStain;
                Patch(b, px, floor + .008f + i * .0015f, pz, r, b.Jitter(hex, .05f), 9);
            }
        }

        /// rust and soot streaks running down a wall face
        static void Streaks(MeshBuilder b, Vector3 a, Vector3 c, float y0, float y1, Vector3 n, int count)
        {
            Vector3 d = c - a;
            for (int i = 0; i < count; i++)
            {
                float h = Mathf.Min(b.Range(.4f, 1.6f), y1 - y0), top = b.Range(y0 + h, y1);
                Vector3 p = a + d * b.Rand(); p.y = top - h;
                bool rust = b.Chance(.55f);
                Stain(b, p, n, b.Range(.05f, rust ? .14f : .45f), h, b.Jitter(rust ? (b.Chance(.5f) ? Rust : RustDeep) : Soot, .1f), b.Range(.15f, .5f));
            }
        }

        static void PierStreaks(MeshBuilder b, float x, float z, float floor, int side)
        {
            if (b.Chance(.35f)) return;
            int n = b.RangeInt(1, 3);
            for (int i = 0; i < n; i++)
            {
                float w = b.Range(.05f, .2f), h = b.Range(.6f, 2.4f), px = x + b.Range(-.17f + w * .5f, .17f - w * .5f);
                bool rust = b.Chance(.5f);
                Stain(b, V(px, floor + 2.94f - h, z + side * .152f), V(0, 0, side), w, h,
                    b.Jitter(rust ? (b.Chance(.5f) ? Rust : RustDeep) : Soot, .1f), b.Range(.2f, .6f));
            }
        }

        /// slab that fell flat onto the ground: walkable (top <= STEP), drawn as a chipped plate
        static void FallenSlab(MeshBuilder b, PropModel p, float x, float z, float w, float d, float top, float yaw)
        {
            b.Push(); b.Translate(x, 0, z); b.RotateY(yaw);
            b.Box(V(0, top * .5f, 0), V(w, top, d), b.Jitter(Weathered, .06f));
            for (int i = 0; i < 5; i++)
                b.Box(V(b.Range(-.4f, .4f) * w, top + .01f, b.Range(-.4f, .4f) * d), V(b.Range(.2f, .5f), .02f, b.Range(.2f, .5f)), C(b.Chance(.5f) ? Soot : Interior));
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1 : 1;
                Vector3 root = V(sx * w * .5f, top * .5f, b.Range(-.4f, .4f) * d);
                Bar(b, null, root, root + V(sx * b.Range(.15f, .35f), b.Range(-.05f, .05f), 0), root + V(sx * b.Range(.3f, .5f), -top * .45f, b.Range(-.15f, .15f)), .016f);
            }
            b.Pop();
            p.surfaces.Add(PropSurface.Box(x, z, w * .5f, d * .5f, top, yaw));
        }

        /// short broken column, slightly tilted, with a small solid footprint
        static void LeaningStub(MeshBuilder b, PropModel p, float x, float z, float w, float h, float tilt)
        {
            var axis = RandomAxis(b, 0); var q = Sim.AxisAngle(axis, tilt);
            b.Push(); b.Translate(x, 0, z); b.Rotate(axis, tilt);
            b.Box(V(0, h * .5f - .1f, 0), V(w, h + .2f, w), b.Jitter(Weathered, .06f));
            b.Box(V(0, h + .02f, 0), V(w * .7f, .12f, w * .8f), C(Char));
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1 : 1, sz = (i & 2) == 0 ? -1 : 1;
                Vector3 root = V(sx * w * .3f, h - .05f, sz * w * .3f), bend = root + V(sx * .05f, b.Range(.3f, .8f), sz * .05f);
                b.Beam(root, bend, .02f, .018f, 3, C(Rust));
                b.Beam(bend, bend + V(sx * b.Range(.1f, .3f), -.15f, sz * b.Range(.1f, .3f)), .018f, .012f, 3, C(RustDeep));
            }
            b.Pop();
            Vector3 topC = q * V(0, h, 0); float reach = Mathf.Sqrt(topC.x * topC.x + topC.z * topC.z);
            p.colliders.Add(PropCollider.Circle(x + topC.x * .5f, z + topC.z * .5f, w * .62f + reach * .5f).Between(0, h));
        }

        /// a pipe or conduit hanging down a wall face (hugging it, so it never needs collision)
        static void HangingPipe(MeshBuilder b, Guard g, Vector3 top, Vector3 n, float drop)
        {
            Vector3 a = top + n * .1f, bend = a + n * .12f - Vector3.up * drop * .45f + V(b.Range(-.2f, .2f), 0, b.Range(-.2f, .2f)) * .5f;
            Vector3 end = bend - Vector3.up * drop * .55f + n * b.Range(.02f, .2f);
            if (g.Body(a) || g.Body(bend) || g.Body(end)) return;
            b.Beam(a, bend, .055f, .055f, 5, C(Steel)); b.Beam(bend, end, .055f, .05f, 5, C(Rust));
            b.Box(bend, V(.15f, .1f, .15f), C(RustDeep));
        }

        static void EdgesOfSlabs(MeshBuilder b, Guard g, PropModel p, float minTop, float thick, float broken = 1, System.Func<PropSurface, bool> filter = null)
        {
            var list = new List<PropSurface>(p.surfaces);
            foreach (var s in list)
            {
                if (s.kind != PropSurface.BOX || s.top < minTop || s.yaw != 0) continue;
                if (s.halfSize.x < .45f || s.halfSize.y < .45f) continue;
                if (filter != null && !filter(s)) continue;
                float x0 = s.center.x - s.halfSize.x, x1 = s.center.x + s.halfSize.x;
                float z0 = s.center.y - s.halfSize.y, z1 = s.center.y + s.halfSize.y;
                // counter-clockwise from above keeps the slab on each edge's left
                SlabEdge(b, g, V(x0, 0, z1), V(x0, 0, z0), s.top, thick, broken);
                SlabEdge(b, g, V(x0, 0, z0), V(x1, 0, z0), s.top, thick, broken);
                SlabEdge(b, g, V(x1, 0, z0), V(x1, 0, z1), s.top, thick, broken);
                SlabEdge(b, g, V(x1, 0, z1), V(x0, 0, z1), s.top, thick, broken);
                Grime(b, g, s.center.x, s.center.y, s.halfSize.x * 1.8f, s.halfSize.y * 1.8f, s.top,
                    Mathf.Clamp(Mathf.RoundToInt(s.halfSize.x * s.halfSize.y * .3f), 1, 6));
                int spalls = Mathf.Clamp(Mathf.FloorToInt(s.halfSize.x * s.halfSize.y / 4 + b.Rand()), 0, 4);
                for (int i = 0; i < spalls; i++)
                {
                    float w = b.Range(.7f, 1.4f), d = b.Range(.6f, 1.2f);
                    if (w > s.halfSize.x * 1.6f || d > s.halfSize.y * 1.6f) continue;
                    Spall(b, s.center.x + b.Range(-1, 1) * (s.halfSize.x - w * .5f), s.center.y + b.Range(-1, 1) * (s.halfSize.y - d * .5f), s.top, w, d);
                }
            }
        }

        // ---------------------------------------------------------------- per structure
        /// ApartmentRuin / CollapsedTower / small RuinedBuilding shell (14 x 10 m frame)
        internal static void Building(MeshBuilder b, PropModel p, int standing, bool tower)
        {
            var g = new Guard(p);
            const float Storey = 3.2f;
            float yTop = (standing - 1) * Storey + 2.96f;
            float xMax = tower ? -.4f : 7f;   // the tower's crown rests on the right half
            // broken facade tops + soot bands
            BrokenTop(b, g, V(-7, yTop, -5), V(Mathf.Min(3, xMax), yTop, -5), .3f);
            BrokenTop(b, g, V(-7, yTop, 5), V(xMax, yTop, 5), .3f);
            SootBand(b, V(-7, 0, -5), V(Mathf.Min(3, xMax), 0, -5), yTop, .3f);
            SootBand(b, V(-7, 0, 5), V(xMax, 0, 5), yTop, .3f);
            // partitions on the top storey are broken too (lower, rougher)
            float yBase = (standing - 1) * Storey;
            if (!tower)
            {
                BrokenTop(b, g, V(1, yBase + 2.1f, -.5f), V(3.6f, yBase + 2.1f, -.5f), .18f, .7f);
                BrokenTop(b, g, V(5.4f, yBase + 1.3f, -.5f), V(6.8f, yBase + 1.3f, -.5f), .18f, .6f);
            }
            for (int side = -1; side <= 1; side += 2)
                for (int j = 0; j < 3; j++)
                {
                    if (side == 1 && (j == 0 || tower)) continue;
                    ColumnTop(b, g, side * 6.8f, -4 + j * 4, yTop, .4f);
                }
            // every exposed floor edge, roof remnant included
            EdgesOfSlabs(b, g, p, 2.5f, .24f);
            // bar mats still spanning the holes between floor pieces
            for (int f = 1; f < standing; f++)
            {
                if (tower && f >= 2) break;
                if (f == 1) RebarNet(b, g, 3, .8f, 4, 2.4f, Storey - .12f);
                else RebarNet(b, g, 3, 0, 4.2f, 1.8f, f * Storey - .12f);
            }
            // rust / soot runs down the solid piers of both long facades, outside and in
            for (int f = 0; f < standing; f++)
            {
                float y = f * Storey;
                for (int i = 0; i <= 4; i++)
                    if (-7 + i * 2.5f <= xMax) PierStreaks(b, -7 + i * 2.5f, -5, y, -1);
                for (int i = 0; i <= 5; i++)
                {
                    if (-7 + i * 2.8f > xMax) continue;
                    PierStreaks(b, -7 + i * 2.8f, 5, y, 1);
                    if (b.Chance(.4f)) PierStreaks(b, -7 + i * 2.8f, 5, y, -1);
                }
            }
            // rubble along the walls, inside and out; the guard keeps doors and routes flat
            foreach (float x in new[] { -5.6f, -1.2f, 1.6f })
                Heap(b, g, x, -5.45f, 0, b.Range(.9f, 1.5f), b.Range(.5f, 1f), 16);
            foreach (float x in new[] { -5.2f, -2.4f, 2.6f, 5.8f })
                Heap(b, g, x, 5.45f, 0, b.Range(.9f, 1.6f), b.Range(.5f, 1.1f), 16);
            foreach (float x in new[] { -2.6f, 1.6f, 4.8f })
                Heap(b, g, x, 4.55f, 0, b.Range(.7f, 1.2f), b.Range(.4f, .8f), 12);
            Heap(b, g, 2.3f, -.85f, 0, 1.1f, .6f, 12);
            Heap(b, g, 7.4f, 2f, 0, 1.4f, .7f, 14);
            Heap(b, g, -7.4f, 2f, 0, 1.2f, .6f, 12);
            // collapsed corner: the biggest spill, mostly flat shards over the walkable debris
            Heap(b, g, 5.6f, -4.8f, 0, 2.4f, .5f, 34);
            for (int f = 1; f < standing; f++)
            {
                float y = f * Storey;
                Heap(b, g, b.Range(-3, 3), 4.5f, y, 1f, .5f, 10);
                Heap(b, g, 1.6f, -4.5f, y, .9f, .5f, 8);
            }
            // ground grime inside and around the footprint
            Grime(b, g, 0, 0, 13.4f, 9.6f, 0, 16);
            Grime(b, g, 0, -7.2f, 16, 3.5f, 0, 8);
            Grime(b, g, 0, 7.2f, 16, 3.5f, 0, 8);
            // dangling services on the rear facade
            HangingPipe(b, g, V(-5.6f, yTop - .2f, 5.15f), V(0, 0, 1), Mathf.Min(yTop - .3f, 3.6f));
            if (!tower) HangingPipe(b, g, V(6.2f, yTop - .25f, 5.15f), V(0, 0, 1), 2.6f);
            // outside: a fallen floor panel and broken column stubs (small, explicit footprints)
            FallenSlab(b, p, -4.6f, -7.6f, 2.3f, 1.3f, .32f, .25f);
            if (!tower) LeaningStub(b, p, 8.6f, -4.6f, .42f, b.Range(1.1f, 1.9f), b.Range(.08f, .2f));
            if (b.Chance(.6f)) LeaningStub(b, p, -8.7f, -3.2f, .4f, b.Range(.8f, 1.5f), b.Range(.05f, .18f));
            g.Refresh(p);
            Heap(b, g, -4.6f, -7.6f, 0, 1.8f, .3f, 12);
        }

        internal static void ShopRow(MeshBuilder b, PropModel p, float length, float bay, int shops)
        {
            var g = new Guard(p);
            for (int i = 0; i <= shops; i++)
            {
                float x = -length * .5f + i * bay;
                // partitions are only open to the sky in front of the rear roof
                BrokenTop(b, g, V(x, 3.16f, -2.8f), V(x, 3.16f, -.3f), .3f, .8f);
            }
            for (int i = 0; i < shops; i++)
            {
                float x = -length * .5f + (i + .5f) * bay;
                BrokenTop(b, g, V(x - bay * .5f + .25f, 3.15f, -2.8f), V(x + bay * .5f - .25f, 3.15f, -2.8f), .3f, .55f);
                // interior rubble against partitions and the rear wall
                Heap(b, g, x - bay * .5f + .45f, b.Range(-1.8f, 1.6f), 0, b.Range(.8f, 1.2f), b.Range(.5f, .9f), 14);
                Heap(b, g, x + b.Range(-.3f, .3f) * bay, 2.4f, 0, b.Range(.8f, 1.3f), b.Range(.4f, .8f), 12);
                // outside the rear wall (the access ramp lane is kept flat by the guard)
                Heap(b, g, x + b.Range(-.25f, .25f) * bay, 3.25f, 0, b.Range(.9f, 1.5f), b.Range(.5f, 1f), 14);
                Grime(b, g, x, 0, bay - .4f, 5.2f, 0, 5);
                if (b.Chance(.7f)) RoofJunk(b, g, x + b.Range(-.3f, .3f) * bay, b.Range(.5f, 2.1f), 3.4f);
                Grime(b, g, x, -4.3f, bay, 2.2f, 0, 3, false);
                Streaks(b, V(x - bay * .5f, 0, 2.96f), V(x + bay * .5f, 0, 2.96f), .1f, 3.1f, V(0, 0, 1), 4);
            }
            EdgesOfSlabs(b, g, p, 3.3f, .24f);
            HangingPipe(b, g, V(-length * .5f + bay * 1.4f, 3.05f, 2.96f), V(0, 0, 1), 2.4f);
            Heap(b, g, length * .5f + .5f, b.Range(-2, 2), 0, 1.3f, .8f, 14);
            Heap(b, g, -length * .5f - .5f, b.Range(-2, 1), 0, 1.3f, .8f, 14);
            LeaningStub(b, p, length * .5f + 1.8f, -3.9f, .4f, b.Range(1, 1.8f), b.Range(.06f, .18f));
        }

        internal static void ParkingDeck(MeshBuilder b, PropModel p, float top)
        {
            var g = new Guard(p);
            float cap = top + .72f;
            for (int side = -1; side <= 1; side += 2)
            {
                BrokenTop(b, g, V(side * 8.5f, cap, -3.2f), V(side * 8.5f, cap, 5.8f), .25f, .6f);
                BrokenTop(b, g, V(-2.95f, cap, side * 6.25f), V(7.75f, cap, side * 6.25f), .25f, .6f);
                Streaks(b, V(-2.95f, 0, side * 6.377f), V(7.75f, 0, side * 6.377f), top + .05f, cap, V(0, 0, side), 6);
                Streaks(b, V(side * 8.627f, 0, -3.2f), V(side * 8.627f, 0, 5.8f), top + .05f, cap, V(side, 0, 0), 4);
            }
            EdgesOfSlabs(b, g, p, 3f, .24f, 1.1f);
            // rubble drifted against the parapets and around the wrecks
            foreach (var h in new[] { V(-1.8f, 0, 5.8f), V(4.5f, 0, 5.8f), V(8f, 0, -1.8f), V(3.2f, 0, -5.8f), V(8f, 0, 3.2f) })
                Heap(b, g, h.x, h.z, top, b.Range(1, 1.6f), b.Range(.4f, .8f), 16);
            // underneath: heaps at column bases, oil and soot on the ground
            foreach (float x in new[] { -8.5f, -.5f, 8.5f })
                foreach (float z in new[] { -6.5f, 0, 6.5f })
                    if (b.Chance(.55f)) Heap(b, g, x + b.Range(-.3f, .3f), z + b.Range(-.3f, .3f), 0, b.Range(.8f, 1.3f), b.Range(.4f, .8f), 10);
            Grime(b, g, 0, 0, 17, 13, 0, 18);
            RoofJunk(b, g, b.Range(-2, 1), b.Range(-4, -1), top);
            RoofJunk(b, g, b.Range(4.5f, 7.5f), b.Range(.6f, 2.4f), top);
            HangingPipe(b, g, V(-.5f, top - .3f, 6.6f), V(0, 0, 1), 2.2f);
            HangingPipe(b, g, V(4.2f, top - .3f, -6.6f), V(0, 0, -1), 2f);
            FallenSlab(b, p, 11.2f, 2.5f, 2.6f, 1.6f, .3f, 1.25f);
            LeaningStub(b, p, -10.6f, 4.8f, .45f, b.Range(1.2f, 2), b.Range(.08f, .2f));
            g.Refresh(p);
            Heap(b, g, 11.2f, 2.5f, 0, 2f, .3f, 12);
        }

        internal static void Overpass(MeshBuilder b, PropModel p, float start, float end, float deck, float rampEnd, float snap, float stubStart)
        {
            var g = new Guard(p);
            const float thick = .42f;
            // ordinary edges first, then the two sheared ends get much heavier treatment
            EdgesOfSlabs(b, g, p, 4f, thick, .8f);
            SlabEdge(b, g, V(4, 0, snap), V(-4, 0, snap), deck, thick, 2.2f);
            SlabEdge(b, g, V(-4, 0, stubStart), V(4, 0, stubStart), deck, thick, 2.2f);
            for (float x = -3.6f; x <= 3.6f; x += b.Range(.25f, .5f))
            {
                // long bars bridging part of the gap and hanging into it
                Vector3 r0 = V(x, deck - .2f, snap), r1 = V(x + b.Range(-.1f, .1f), deck - .22f, stubStart);
                Bar(b, g, r0, r0 + V(b.Range(-.1f, .1f), b.Range(-.4f, -.1f), b.Range(.4f, 1.2f)), r0 + V(b.Range(-.2f, .2f), -b.Range(.8f, 1.6f), b.Range(.6f, 1.4f)), .02f);
                if (b.Chance(.6f)) Bar(b, g, r1, r1 + V(0, b.Range(-.3f, -.1f), -b.Range(.3f, .9f)), r1 + V(b.Range(-.2f, .2f), -b.Range(.6f, 1.3f), -b.Range(.4f, 1f)), .02f);
            }
            // the missing span lies broken on the ground in the gap
            float gap = (snap + stubStart) * .5f;
            FallenSlab(b, p, -1.7f, gap - .3f, 3.2f, 1.7f, .38f, .35f);
            FallenSlab(b, p, 2.1f, gap + .5f, 2.4f, 1.4f, .3f, -.42f);
            g.Refresh(p);
            Heap(b, g, 0, gap, 0, 3.2f, .3f, 30);
            Heap(b, g, -4.8f, gap, 0, 1.6f, .3f, 12);
            Heap(b, g, 4.9f, gap + .4f, 0, 1.6f, .3f, 12);
            // deck rubble swept against the rails, cracks and oil
            for (int side = -1; side <= 1; side += 2)
                for (float z = rampEnd + 1.5f; z < snap - 1; z += b.Range(3.5f, 6))
                    Heap(b, g, side * 3.55f, z, deck, b.Range(.7f, 1.1f), b.Range(.3f, .6f), 10);
            for (float z = rampEnd + 1; z < snap - .5f; z += b.Range(1.2f, 2.6f))
            {
                float x0 = b.Range(-3.6f, 0), x1 = x0 + b.Range(1, 3.5f), z1 = z + b.Range(-.5f, .5f);
                Vector3 a = V(x0, deck + .006f, z), m = V((x0 + x1) * .5f, deck + .006f, (z + z1) * .5f + b.Range(-.2f, .2f)), c = V(x1, deck + .006f, z1);
                b.Quad(a, a + V(0, 0, .05f), m + V(0, 0, .05f), m, C(Char));
                b.Quad(m, m + V(0, 0, .04f), c + V(0, 0, .03f), c, C(Char));
            }
            // piers: soot and leaks from the deck joints
            foreach (float z in new[] { rampEnd + 2, snap - 1.5f, end - 1.2f })
                for (int side = -1; side <= 1; side += 2)
                {
                    Streaks(b, V(side * 2.6f - .42f, 0, z - .56f), V(side * 2.6f + .42f, 0, z - .56f), .2f, deck - .45f, V(0, 0, -1), 2);
                    Streaks(b, V(side * 2.6f - .42f, 0, z + .56f), V(side * 2.6f + .42f, 0, z + .56f), .2f, deck - .45f, V(0, 0, 1), 2);
                    Heap(b, g, side * 2.6f, z, 0, 1.2f, .6f, 10);
                }
            Grime(b, g, 0, (start + end) * .5f, 12, end - start, 0, 14);
            RoofJunk(b, g, b.Range(.8f, 2.6f), rampEnd + b.Range(2, 5), deck);       // clear of both wrecks
            RoofJunk(b, g, b.Range(-2.6f, -.8f), snap - b.Range(1.5f, 3), deck);
            LeaningStub(b, p, 5.6f, gap + 2.4f, .5f, b.Range(1.4f, 2.4f), b.Range(.1f, .22f));
        }

        internal static void StairRuin(MeshBuilder b, PropModel p, float top)
        {
            var g = new Guard(p);
            // a broken wall remnant standing beside the flight (its own small solid band)
            float wallH = b.Range(1.2f, 1.8f);
            b.Box(V(-3.05f, wallH * .5f, -.9f), V(.3f, wallH, 3.4f), b.Jitter(Dark, .06f));
            p.colliders.Add(PropCollider.Rect(-3.05f, -.9f, .15f, 1.7f).Between(0, wallH));
            g.Refresh(p);
            BrokenTop(b, g, V(-3.05f, wallH, -2.6f), V(-3.05f, wallH, .8f), .3f, 1.2f);
            SootBand(b, V(-3.05f, 0, -2.6f), V(-3.05f, 0, .8f), wallH, .3f);
            EdgesOfSlabs(b, g, p, 2.5f, .3f, 1.2f);
            Heap(b, g, -2.7f, 1.4f, 0, 1.3f, .7f, 14);
            Heap(b, g, 2.6f, 3f, 0, 1.4f, .8f, 14);
            Heap(b, g, -2.4f, -2.8f, 0, 1.2f, .6f, 12);
            Heap(b, g, -.8f, 3.3f, top, .9f, .3f, 8);
            Grime(b, g, 0, 0, 7, 8, 0, 8);
            LeaningStub(b, p, 3.4f, -2.6f, .4f, b.Range(1, 1.7f), b.Range(.08f, .2f));
        }
    }
}
