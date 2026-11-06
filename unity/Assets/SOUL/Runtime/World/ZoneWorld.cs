using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Soul
{
    // The zone graph laid out as a seamless open world (sim space, metres; +X = screen right, -Z = screen up).
    //  - Every zone has its own hand-drawn outline (Outlines below; user decision 2026-11-06: "not squares,
    //    like a real world / other metroidvanias"): a long winding gorge, a sprawling wood, a mire basin,
    //    a narrow skyscraper strip, a small sealed lab ... Linked zones share exact border vertices.
    //  - Outside every zone (gaps between unlinked zones and the world rim) is impassable mountain.
    //  - Borders meander through a small domain warp. Each border is a rock ridge; each ZoneGraph link cuts
    //    a corridor through it at its position along the shared border (Link.t); part links put a gate
    //    there (ZoneGates). Edit the outline table and the whole layout (sites, trails, map) follows.
    //  - Lookups use a precomputed signed-distance raster (RES m, one field per zone, bilinear).
    public static class ZoneWorld
    {
        public const float RIDGE_W = 13;               // half width of a border ridge (each side of the border)
        public const float RIDGE_H = 13, EDGE_H = 24;  // ridge height between zones / mountains outside all zones
        public const float SOLID = .9f;                // ridge taller than this blocks walking
        public const float HALF_W = 7;                 // corridor half width (fully open)
        public const float GATE_HALF = 14;             // gates span the corridor out to here (ridge is full height beyond 12)
        public const float CUT_L = 34;                 // corridor half length along the link direction
        public const float LEDGE_H = 2.6f;             // drop / rope ledge height (> JUMP)

        // ---------- zone outlines (draft for the user's review; edit here) ----------
        // Shared borders must repeat the same vertices in both outlines (any order / direction).
        static readonly Vector2[][] Outlines =
        {
            // Z0 잿빛 폐허 — the hub: a wide broken plain around the original arena
            P(-760,-300, -520,-380, -200,-330, 60,-420, 340,-360, 600,-300, 820,-200, 860,60, 780,300, 520,380, 240,330, 0,420, -260,360, -560,330, -800,200, -820,-60),
            // Z1 무너진 도심 — a block-ish city plateau to the north-west
            P(-760,-300, -980,-330, -1180,-420, -1150,-900, -820,-1080, -420,-1010, -110,-1010, -60,-780, -120,-520, -200,-330, -520,-380),
            // Z2 마천루 묘지 — a long narrow strip running south along the western rim
            P(-760,-300, -980,-330, -1180,-420, -1320,-120, -1280,320, -1220,760, -980,920, -760,780, -700,420, -800,200, -820,-60),
            // Z3 고철 협곡 — a winding canyon band from the city edge east to the foundry
            P(-120,-520, -60,-780, -110,-1010, 120,-1000, 300,-880, 520,-830, 760,-900, 900,-1000, 900,-760, 880,-520, 640,-560, 420,-520, 340,-360, 60,-420),
            // Z4 전쟁 기계 공장 — a walled industrial basin in the north-east
            P(900,-1000, 1150,-1080, 1460,-960, 1520,-640, 1380,-380, 1100,-330, 960,-400, 880,-520, 900,-760),
            // Z5 죽은 숲 — a sprawling wood east of the hub
            P(820,-200, 960,-400, 1100,-330, 1380,-380, 1540,-180, 1580,120, 1480,360, 1300,430, 1050,450, 880,380, 780,300, 860,60),
            // Z6 독성 늪 — a sunken basin south-east
            P(1050,450, 1300,430, 1480,360, 1600,620, 1520,940, 1240,1080, 940,1020, 820,780, 900,560),
            // Z7 과학자의 연구소 — a small sealed plateau under the hub
            P(-260,360, 0,420, 240,330, 320,560, 200,800, -80,860, -300,680),
        };
        static Vector2[] P(params float[] xz) { var v = new Vector2[xz.Length / 2]; for (int i = 0; i < v.Length; i++) v[i] = new Vector2(xz[2 * i], xz[2 * i + 1]); return v; }
        public static float MinX, MaxX, MinZ, MaxZ;     // world rectangle (outline bounds + mountain margin)
        const float RES = 8, MARGIN = 140;
        static int rw, rh, nz; static float[] sd;       // sd[(i * rh + j) * nz + zone]: signed distance (negative inside)
        static readonly Vector2[] centres = new Vector2[8];

        public class Site
        {
            public int index; public ZoneGraph.Link link; public Vector2 pos, n, tan; public float yaw;
            public bool Gate => link.kind == ZoneGraph.Kind.Part;
            public string Label => link.kind == ZoneGraph.Kind.Free ? "통로" : link.kind == ZoneGraph.Kind.Drop ? "낙하 (한 방향)" : Parts.Names[link.part];
            /// local corridor coordinates: x along the border, z along the link (a -> b)
            public Vector2 Local(float x, float z) { var d = new Vector2(x, z) - pos; return new Vector2(Vector2.Dot(d, tan), Vector2.Dot(d, n)); }
        }
        public static readonly List<Site> Sites = new List<Site>();

        /// size balance (user 2026-11-06: hub too big, others too small): pull points near the centre in, push the
        /// rim out. Same transform for every vertex, so shared borders stay shared.
        static Vector2 Balance(Vector2 v) { float r = v.magnitude; return v * (.66f + .46f * Sim.SmoothStep(r, 300, 1700)); }
        static ZoneWorld()
        {
            foreach (var o in Outlines) for (int i = 0; i < o.Length; i++) o[i] = Balance(o[i]);
            BuildRaster(); BuildSites();
        }

        // ---------- raster ----------
        static float SegDist(Vector2 p, Vector2 a, Vector2 b) { var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude)); return (a + ab * t - p).magnitude; }
        static bool Inside(Vector2 p, Vector2[] poly)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) && (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) c = !c;
            return c;
        }
        // ---------- raster: fractal coastlines + interior massifs, signed distance by chamfer transform ----------
        static float H01(int a, int b, int c) { unchecked { int h = a * 73856093 ^ b * 19349663 ^ c * 83492791; h ^= h >> 13; h *= 1274126177; return ((h >> 8) & 0xffff) / 65535f; } }
        /// every outline edge subdivided 5 times with midpoint displacement (canonical per edge, so two zones
        /// sharing an edge get exactly the same coastline)
        /// approximate passage points straight from the hand outlines (shared vertices, arc length at Link.t):
        /// the fractal detail is calmed around them so passages cross a clean stretch of border
        static List<Vector2> PreSites()
        {
            var pts = new List<Vector2>();
            foreach (var l in ZoneGraph.Links)
            {
                var A = Outlines[l.a]; var B = new HashSet<Vector2>(Outlines[l.b]);
                // longest run of consecutive shared vertices in A (wrapping)
                int n = A.Length, bestStart = -1, bestLen = 0;
                for (int st = 0; st < n; st++)
                {
                    if (!B.Contains(A[st]) || B.Contains(A[(st - 1 + n) % n])) continue;
                    int len = 0; while (len < n && B.Contains(A[(st + len) % n])) len++;
                    if (len > bestLen) { bestLen = len; bestStart = st; }
                }
                if (bestLen < 2) continue;
                var run = new List<Vector2>(); for (int k = 0; k < bestLen; k++) run.Add(A[(bestStart + k) % n]);
                var axis = run[run.Count - 1] - run[0]; if (Mathf.Abs(axis.x) >= Mathf.Abs(axis.y) ? axis.x < 0 : axis.y < 0) run.Reverse();
                float total = 0; for (int k = 1; k < run.Count; k++) total += (run[k] - run[k - 1]).magnitude;
                float want = Mathf.Clamp(l.t, .18f, .82f) * total, acc = 0;
                for (int k = 1; k < run.Count; k++)
                {
                    float L = (run[k] - run[k - 1]).magnitude;
                    if (acc + L >= want) { pts.Add(Vector2.Lerp(run[k - 1], run[k], (want - acc) / Mathf.Max(L, 1e-3f))); break; }
                    acc += L;
                }
            }
            return pts;
        }
        static List<Vector2> calm;
        static float Calm(Vector2 m) { float k = 1; foreach (var c in calm) k = Mathf.Min(k, Sim.SmoothStep((m - c).magnitude, 70, 170)); return k; }
        static Vector2[] Detail(Vector2[] o)
        {
            var outp = new List<Vector2>();
            for (int a = 0; a < o.Length; a++)
            {
                Vector2 p = o[a], q = o[(a + 1) % o.Length];
                bool flip = p.x > q.x || (p.x == q.x && p.y > q.y);
                Vector2 c0 = flip ? q : p, c1 = flip ? p : q;
                int seed = Mathf.RoundToInt(c0.x) * 31 + Mathf.RoundToInt(c0.y) * 17 + Mathf.RoundToInt(c1.x) * 7 + Mathf.RoundToInt(c1.y) * 3;
                var seg = new List<Vector2> { c0, c1 }; float amp = (c1 - c0).magnitude * .13f;
                for (int lvl = 0; lvl < 5; lvl++)
                {
                    var nxt = new List<Vector2> { seg[0] };
                    for (int i = 0; i + 1 < seg.Count; i++)
                    {
                        var d = seg[i + 1] - seg[i]; var nrm = new Vector2(-d.y, d.x).normalized;
                        var mid = (seg[i] + seg[i + 1]) * .5f;
                        nxt.Add(mid + nrm * ((H01(seed, lvl, i) * 2 - 1) * amp * Calm(mid))); nxt.Add(seg[i + 1]);
                    }
                    seg = nxt; amp *= .5f;
                }
                if (flip) seg.Reverse();
                for (int i = 0; i < seg.Count - 1; i++) outp.Add(seg[i]);
            }
            return outp.ToArray();
        }
        static void Fill(Vector2[] poly, bool[] mask)
        {
            var xs = new List<float>();
            for (int j = 0; j < rh; j++)
            {
                float z = MinZ + j * RES; xs.Clear();
                for (int a = 0, b = poly.Length - 1; a < poly.Length; b = a++)
                {
                    Vector2 p = poly[a], q = poly[b];
                    if ((p.y > z) != (q.y > z)) xs.Add(p.x + (z - p.y) / (q.y - p.y) * (q.x - p.x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int i0 = Mathf.CeilToInt((xs[k] - MinX) / RES), i1 = Mathf.FloorToInt((xs[k + 1] - MinX) / RES);
                    for (int i = Mathf.Max(0, i0); i <= Mathf.Min(rw - 1, i1); i++) mask[i * rh + j] = true;
                }
            }
        }
        /// chamfer (1, 1.414) distance in cells from every cell to the nearest cell with mask == target
        static float[] Chamfer(bool[] mask, bool target)
        {
            var d = new float[rw * rh]; const float D = 1.4142f;
            for (int k = 0; k < d.Length; k++) d[k] = mask[k] == target ? 0 : 1e6f;
            for (int i = 0; i < rw; i++) for (int j = 0; j < rh; j++)
            {
                int k = i * rh + j; float v = d[k];
                if (i > 0) v = Mathf.Min(v, d[k - rh] + 1);
                if (j > 0) v = Mathf.Min(v, d[k - 1] + 1);
                if (i > 0 && j > 0) v = Mathf.Min(v, d[k - rh - 1] + D);
                if (i > 0 && j < rh - 1) v = Mathf.Min(v, d[k - rh + 1] + D);
                d[k] = v;
            }
            for (int i = rw - 1; i >= 0; i--) for (int j = rh - 1; j >= 0; j--)
            {
                int k = i * rh + j; float v = d[k];
                if (i < rw - 1) v = Mathf.Min(v, d[k + rh] + 1);
                if (j < rh - 1) v = Mathf.Min(v, d[k + 1] + 1);
                if (i < rw - 1 && j < rh - 1) v = Mathf.Min(v, d[k + rh + 1] + D);
                if (i < rw - 1 && j > 0) v = Mathf.Min(v, d[k + rh - 1] + D);
                d[k] = v;
            }
            return d;
        }
        // interior massifs (rock outcrops / cliff clumps) per zone: threshold on low-frequency noise
        static readonly float[] MassifThreshold = { .76f, .77f, .73f, .71f, .77f, .72f, .82f, 2f };
        static bool Protected(Vector2 p, int zone)
        {
            if (zone == 0 && p.magnitude < 170) return true;                                  // hub plaza
            foreach (var pod in ZoneAreas.Pods) if ((p - new Vector2(pod.pos.x, pod.pos.z)).magnitude < 70) return true;
            foreach (var a in ZoneAreas.Arenas) if ((p - new Vector2(a.center.x, a.center.z)).magnitude < a.radius + 70) return true;
            foreach (var c in ZoneAreas.Chasms) if ((p - new Vector2(c.center.x, c.center.z)).magnitude < c.halfLength + 50) return true;
            return false;
        }
        static readonly Vector2[][] detailed = new Vector2[8][];
        static void BuildRaster()
        {
            nz = Outlines.Length;
            calm = PreSites();
            var polys = new Vector2[nz][]; for (int z = 0; z < nz; z++) polys[z] = Detail(Outlines[z]);
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var o in polys) foreach (var v in o) { x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); z0 = Mathf.Min(z0, v.y); z1 = Mathf.Max(z1, v.y); }
            MinX = x0 - MARGIN; MaxX = x1 + MARGIN; MinZ = z0 - MARGIN; MaxZ = z1 + MARGIN;
            rw = Mathf.CeilToInt((MaxX - MinX) / RES) + 1; rh = Mathf.CeilToInt((MaxZ - MinZ) / RES) + 1;
            sd = new float[rw * rh * nz];
            for (int z = 0; z < nz; z++)
            {
                var mask = new bool[rw * rh]; Fill(polys[z], mask);
                // massifs only well inside the zone (pre-massif rim distance > 90 m), never on protected spots
                var rim = Chamfer(mask, false);
                float thr = MassifThreshold[Mathf.Min(z, MassifThreshold.Length - 1)];
                for (int i = 0; i < rw; i++) for (int j = 0; j < rh; j++)
                {
                    int k = i * rh + j; if (!mask[k] || rim[k] * RES < 90) continue;
                    var p = new Vector2(MinX + i * RES, MinZ + j * RES);
                    float m = Mathf.PerlinNoise(p.x * .0065f + 3.7f * z, p.y * .0065f + 1.3f) * .75f + Mathf.PerlinNoise(p.x * .02f + 9.1f, p.y * .02f + 5.5f * z) * .35f;
                    if (m > thr && !Protected(p, z)) mask[k] = false;
                }
                var dOut = Chamfer(mask, false); var dIn = Chamfer(mask, true);
                float best = -1; Vector2 bestP = Vector2.zero;
                for (int k = 0; k < mask.Length; k++)
                {
                    float s = mask[k] ? -(dOut[k] - .5f) * RES : (dIn[k] - .5f) * RES;
                    sd[k * nz + z] = s;
                    if (mask[k] && dOut[k] > best) { best = dOut[k]; bestP = new Vector2(MinX + (k / rh) * RES, MinZ + (k % rh) * RES); }
                }
                if (z < centres.Length) centres[z] = bestP;
                detailed[z] = polys[z];
            }
            centres[0] = Vector2.zero;   // the hub is centred on the original arena by design
        }
        /// signed distances of every zone at (x, z) after the border warp (negative = inside)
        static void Fields(float x, float z, float[] o)
        {
            float wx = 14 * N(x, z, 1 / 260f, 17.3f, 4.1f) + 5 * N(x, z, 1 / 70f, 2.9f, 31.7f);
            float wz = 14 * N(x, z, 1 / 260f, 51.1f, 23.9f) + 5 * N(x, z, 1 / 70f, 77.2f, 8.3f);
            float gx = Mathf.Clamp((x + wx - MinX) / RES, 0, rw - 1.001f), gz = Mathf.Clamp((z + wz - MinZ) / RES, 0, rh - 1.001f);
            int i = (int)gx, j = (int)gz; float fx = gx - i, fz = gz - j;
            int a = (i * rh + j) * nz, b = ((i + 1) * rh + j) * nz, c = (i * rh + j + 1) * nz, d = ((i + 1) * rh + j + 1) * nz;
            for (int k = 0; k < nz; k++) o[k] = Mathf.Lerp(Mathf.Lerp(sd[a + k], sd[b + k], fx), Mathf.Lerp(sd[c + k], sd[d + k], fx), fz);
        }
        static float N(float x, float z, float f, float ox, float oz) => (Mathf.PerlinNoise(x * f + ox, z * f + oz) - .5f) * 2;
        [System.ThreadStatic] static float[] tmpF;

        /// zone id at (x, z); -1 = mountains (outside every zone)
        public static int At(float x, float z) => Sample(x, z, out _, out _);
        /// zone at (x, z) (-1 = mountains), the nearest other zone (-1 = mountains) and the distance to that border (m)
        public static int Sample(float x, float z, out int other, out float dist)
        {
            var f = tmpF ??= new float[16]; Fields(x, z, f);
            int s0 = 0, s1 = -1;
            for (int k = 1; k < nz; k++) if (f[k] < f[s0]) { s1 = s0; s0 = k; } else if (s1 < 0 || f[k] < f[s1]) s1 = k;
            if (f[s0] < 0)
            {   // inside zone s0: the border is either the zone's own rim (mountains) or the neighbour s1
                float rim = -f[s0];
                if (f[s1] < 0) { other = s1; dist = (f[s1] - f[s0]) * .5f; return s0; }   // overlap region: midline
                float toNb = (f[s1] - f[s0]) * .5f;
                if (f[s1] - rim < RES * 1.5f) { other = s1; dist = toNb; } else { other = -1; dist = rim; }   // nearest rim is the shared border
                return s0;
            }
            other = s0; dist = f[s0]; return -1;
        }
        public static Vector2 Center(int zone) => centres[Mathf.Clamp(zone, 0, centres.Length - 1)];
        public static Vector2[] Outline(int zone) => detailed[zone] ?? Outlines[zone];

        // ---------- link sites ----------
        static void BuildSites()
        {
            Sites.Clear();
            var used = new Dictionary<long, int>();
            for (int li = 0; li < ZoneGraph.Links.Length; li++)
            {
                var l = ZoneGraph.Links[li];
                // shared border: raster cells inside a whose b-field is within ~one cell of 0 (and vice versa)
                var pts = new List<Vector2>();
                for (int i = 0; i < rw; i++) for (int j = 0; j < rh; j++)
                {
                    int c = (i * rh + j) * nz; float da = sd[c + l.a], db = sd[c + l.b];
                    if (Mathf.Abs(da) < RES && Mathf.Abs(db) < RES) pts.Add(new Vector2(MinX + i * RES, MinZ + j * RES));
                }
                if (pts.Count < 6) { Debug.LogWarning($"ZoneWorld: Z{l.a} and Z{l.b} share no border"); continue; }
                // order along the border (axis between its two farthest points, pointing +x or +z like the slide)
                Vector2 e0 = pts[0], e1 = pts[0]; foreach (var p in pts) if ((p - e0).sqrMagnitude > (e1 - e0).sqrMagnitude) e1 = p;
                e0 = e1; foreach (var p in pts) if ((p - e1).sqrMagnitude > (e0 - e1).sqrMagnitude) e0 = p;
                var axis = (e0 - e1).normalized; if (Mathf.Abs(axis.x) >= Mathf.Abs(axis.y) ? axis.x < 0 : axis.y < 0) axis = -axis;
                pts.Sort((p, q) => Vector2.Dot(p, axis).CompareTo(Vector2.Dot(q, axis)));
                float t = Mathf.Clamp(l.t, .18f, .82f);
                var p0 = pts[Mathf.Clamp(Mathf.RoundToInt(t * (pts.Count - 1)), 0, pts.Count - 1)];
                // direction a -> b: gradient of (sd_a - sd_b)
                var fa = new float[16]; var fb = new float[16];
                Fields(p0.x + 4, p0.y, fa); Fields(p0.x - 4, p0.y, fb); float gx = (fa[l.a] - fa[l.b]) - (fb[l.a] - fb[l.b]);
                Fields(p0.x, p0.y + 4, fa); Fields(p0.x, p0.y - 4, fb); float gz = (fa[l.a] - fa[l.b]) - (fb[l.a] - fb[l.b]);
                var n = new Vector2(gx, gz); n = n.sqrMagnitude > 1e-6f ? n.normalized : Vector2.up;
                // exact flip from a to b along n near p0
                var pos = p0; float bestS = float.PositiveInfinity;
                for (float s = -60; s <= 60; s += .5f)
                {
                    var q0 = p0 + n * s; var q1 = q0 + n * .5f;
                    if (At(q0.x, q0.y) == l.a && At(q1.x, q1.y) == l.b && Mathf.Abs(s) < bestS) { bestS = Mathf.Abs(s); pos = (q0 + q1) * .5f; }
                }
                Sites.Add(new Site { index = Sites.Count, link = l, pos = pos, n = n, tan = new Vector2(n.y, -n.x), yaw = Mathf.Atan2(n.x, n.y) });
            }
        }
        /// 0 = no corridor, 1 = inside a corridor cut through the ridge
        public static float Opening(float x, float z, out Site site)
        {
            float best = 0; site = null;
            foreach (var s in Sites)
            {
                float dx = x - s.pos.x, dz = z - s.pos.y; if (dx * dx + dz * dz > 60 * 60) continue;
                var l = s.Local(x, z);
                float m = (1 - Sim.SmoothStep(Mathf.Abs(l.x), HALF_W, HALF_W + 5)) * (1 - Sim.SmoothStep(Mathf.Abs(l.y), CUT_L, CUT_L + 8));
                if (m > best) { best = m; site = s; }
            }
            return best;
        }
        public static Site NearestSite(Vector3 p, float within)
        {
            Site best = null; float bd = within * within;
            foreach (var s in Sites) { float d = (new Vector2(p.x, p.z) - s.pos).sqrMagnitude; if (d < bd) { bd = d; best = s; } }
            return best;
        }

        // ---------- border ridges ----------
        /// height of the border ridge at (x, z) (0 away from borders and inside corridors)
        public static float RidgeHeight(float x, float z)
        {
            int self = Sample(x, z, out int other, out float d);
            bool edge = self < 0 || other < 0;
            float w = RIDGE_W * (edge ? 1.5f : 1) * (.8f + .4f * Mathf.PerlinNoise(x * .021f + 5.3f, z * .021f + 9.1f));
            if (self < 0) d = -d;                                    // outside the board: solid all the way out
            if (d > w) return Funnel(x, z);                           // spurs reach out past the ridge band
            float k = Mathf.Clamp01(1 - d / w);
            float prof = k * k * (3 - 2 * k);
            float h = (edge ? EDGE_H : RIDGE_H) * (.65f + .7f * Mathf.PerlinNoise(x * .035f + 1.7f, z * .035f + 3.3f)) + 2.2f * N(x, z, .19f, 8.8f, 1.1f);
            if (self < 0) h *= 1 + .7f * Sim.SmoothStep(-d, 30, 160) + .4f * Mathf.PerlinNoise(x * .008f + 4.4f, z * .008f + 2.2f);   // mountains rise away from the zones
            h *= prof;
            float open = Opening(x, z, out _);
            return Mathf.Max(Mathf.Max(0, h * (1 - open)), Funnel(x, z));
        }
        /// route funnel: two ridge spurs flaring out of each passage mouth on both sides of the border,
        /// so walking along the ridge you are gathered toward the opening (ZoneRoutes trails end here)
        public static float Funnel(float x, float z)
        {
            float best = 0; var a = new Vector2(HALF_W + 7, CUT_L - 4); var b = new Vector2(HALF_W + 37, CUT_L + 40); var ab = b - a;
            foreach (var s in Sites)
            {
                float dx = x - s.pos.x, dz = z - s.pos.y; if (dx * dx + dz * dz > 100 * 100) continue;
                var l = s.Local(x, z); var q = new Vector2(Mathf.Abs(l.x), Mathf.Abs(l.y));
                if (q.y < CUT_L - 8 || q.y > CUT_L + 48) continue;
                float u = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude), d = (a + ab * u - q).magnitude;
                if (d > 6.5f) continue;
                float h = RIDGE_H * .8f * Mathf.Pow(1 - u, .6f) * (1 - Sim.SmoothStep(u, .8f, 1)) * (1 - Sim.SmoothStep(d, 2.5f, 6.5f)) * (.75f + .5f * Mathf.PerlinNoise(x * .05f + 3.3f, z * .05f + 8.1f));
                if (h > best) best = h;
            }
            return best;
        }
        public static bool Solid(float x, float z) => RidgeHeight(x, z) > SOLID;
        /// true if (x, z) is clear of ridges and corridors by `margin` metres (for scatter placement)
        public static bool Clear(float x, float z, float margin)
        {
            int self = Sample(x, z, out _, out float d);
            if (self < 0 || d < RIDGE_W * 1.6f + margin) return false;
            if (ZoneAreas.Blocked(x, z, margin)) return false;
            if (Funnel(x, z) > .2f || ZoneRoutes.Dist(x, z) < ZoneRoutes.EDGE + 1 + margin) return false;   // keep funnels and trails open
            foreach (var s in Sites) { float dx = x - s.pos.x, dz = z - s.pos.y; if (dx * dx + dz * dz < (CUT_L + 14 + margin) * (CUT_L + 14 + margin)) return false; }
            return true;
        }

        // ---------- design check (flood fill over the analytic layout) ----------
        const float GRID = 4;
        static byte[] cls; static int gw, gh;   // 0 free, 1 ridge, 2 + site index = gate block of that site
        static void BuildGrid()
        {
            gw = Mathf.CeilToInt((MaxX - MinX) / GRID); gh = Mathf.CeilToInt((MaxZ - MinZ) / GRID); cls = new byte[gw * gh];
            for (int i = 0; i < gw; i++) for (int j = 0; j < gh; j++)
            {
                float x = MinX + (i + .5f) * GRID, z = MinZ + (j + .5f) * GRID;
                byte c = (byte)(Solid(x, z) ? 1 : 0);
                if (c == 0)
                    foreach (var s in Sites)
                    {
                        if (s.link.kind == ZoneGraph.Kind.Free) continue;
                        var l = s.Local(x, z);
                        if (Mathf.Abs(l.x) < GATE_HALF - 1.5f && Mathf.Abs(l.y) < 6.5f) { c = (byte)(2 + s.index); break; }
                    }
                cls[i * gh + j] = c;
            }
        }
        /// zones reachable from Z0 when walking the laid-out world with `mask` parts (drops closed)
        public static int FloodReach(int mask)
        {
            if (cls == null) BuildGrid();
            var seen = new bool[cls.Length]; var q = new Queue<int>();
            int Idx(float x, float z) => Mathf.FloorToInt((x - MinX) / GRID) * gh + Mathf.FloorToInt((z - MinZ) / GRID);
            int start = Idx(0, 0); seen[start] = true; q.Enqueue(start); int zones = 0;
            while (q.Count > 0)
            {
                int k = q.Dequeue(); int i = k / gh, j = k % gh;
                int z = At(MinX + (i + .5f) * GRID, MinZ + (j + .5f) * GRID); if (z >= 0) zones |= 1 << z;
                for (int d = 0; d < 4; d++)
                {
                    int ni = i + (d == 0 ? 1 : d == 1 ? -1 : 0), nj = j + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (ni < 0 || nj < 0 || ni >= gw || nj >= gh) continue;
                    int nk = ni * gh + nj; if (seen[nk]) continue;
                    byte c = cls[nk];
                    if (c == 1) continue;
                    if (c >= 2) { var s = Sites[c - 2]; if (s.link.kind == ZoneGraph.Kind.Drop || !Parts.Has(mask, s.link.part)) continue; }
                    seen[nk] = true; q.Enqueue(nk);
                }
            }
            return zones;
        }
        /// graph reachability with drops removed and part links two-way (what FloodReach measures)
        static int GraphReach(int mask)
        {
            int seen = 1; for (bool ch = true; ch;)
            {
                ch = false;
                foreach (var l in ZoneGraph.Links)
                {
                    if (l.kind == ZoneGraph.Kind.Drop || (l.kind == ZoneGraph.Kind.Part && !Parts.Has(mask, l.part))) continue;
                    if (((seen >> l.a) & 1) != ((seen >> l.b) & 1)) { seen |= (1 << l.a) | (1 << l.b); ch = true; }
                }
            }
            return seen;
        }
        /// "" when the laid-out world matches the zone graph; else one line per problem
        public static string Validate()
        {
            var sb = new StringBuilder();
            if (Sites.Count != ZoneGraph.Links.Length) sb.AppendLine($"FAIL sites {Sites.Count} / links {ZoneGraph.Links.Length}");
            foreach (var s in Sites)
            {
                var a = s.pos - s.n * (CUT_L + 4); var b = s.pos + s.n * (CUT_L + 4);
                if (At(a.x, a.y) != s.link.a || At(b.x, b.y) != s.link.b) sb.AppendLine($"FAIL site {s.index} Z{s.link.a}->Z{s.link.b}: ends in Z{At(a.x, a.y)} / Z{At(b.x, b.y)}");
                var p = s.pos - s.n * (CUT_L + 4); var q = s.pos + s.n * (CUT_L + 4);
                for (float t = 0; t <= 1; t += .02f) { var m = Vector2.Lerp(p, q, t); if (Solid(m.x, m.y)) { sb.AppendLine($"FAIL site {s.index}: corridor blocked by ridge at {m}"); break; } }
            }
            for (int mask = 0; mask < 1 << 7; mask += 2)
            {
                int f = FloodReach(mask), g = GraphReach(mask);
                if (f != g) sb.AppendLine($"FAIL parts {mask:x2}: world reaches {Bits(f)}, graph {Bits(g)}");
            }
            return sb.ToString();
        }
        static string Bits(int m) { var sb = new StringBuilder(); for (int i = 0; i < 8; i++) if ((m & (1 << i)) != 0) sb.Append('Z').Append(i).Append(' '); return sb.ToString().Trim(); }

        // ---------- debug map ----------
        static Texture2D mapTex;
        public const int MAP_W = 384, MAP_H = 288;
        /// top-down map texture (row 0 of the texture = +Z = bottom of the screen)
        public static Texture2D MapTexture()
        {
            if (mapTex) return mapTex;
            mapTex = new Texture2D(MAP_W, MAP_H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[MAP_W * MAP_H];
            for (int i = 0; i < MAP_W; i++) for (int j = 0; j < MAP_H; j++)
            {
                float x = MinX + (i + .5f) / MAP_W * (MaxX - MinX), z = MaxZ - (j + .5f) / MAP_H * (MaxZ - MinZ);
                int zone = At(x, z);
                var c = zone >= 0 && zone < Regions.All.Count ? Regions.All[zone].ground * 1.25f : new Color(.08f, .08f, .08f);
                if (Solid(x, z)) c = new Color(.16f, .15f, .14f);
                else if (zone >= 0 && ZoneRoutes.Dist(x, z) < 7) c = Color.Lerp(c, ZoneRoutes.TrailColor(zone) * .8f, .75f);
                if (zone >= 0)
                {   // relief shading (light from the upper left) and water
                    float e = 12f, sh = (Terrain.Relief(x - e, z - e) - Terrain.Relief(x + e, z + e)) * .06f;
                    c *= 1 + Mathf.Clamp(sh, -.25f, .25f);
                    if (Terrain.WaterRaw(x, z, out byte wt) > .1f) c = Color.Lerp(c, wt == Terrain.WATER_TOXIC ? new Color(.45f, .8f, .2f) : wt == Terrain.WATER_OIL ? new Color(.05f, .05f, .05f) : new Color(.25f, .32f, .3f), .7f);
                }
                c.a = 1; px[j * MAP_W + i] = c;
            }
            mapTex.SetPixels(px); mapTex.Apply();
            return mapTex;
        }
        /// world (sim XZ) -> normalised map position (0..1, y down)
        public static Vector2 MapUV(Vector3 p) => new Vector2((p.x - MinX) / (MaxX - MinX), (p.z - MinZ) / (MaxZ - MinZ));
        public static Vector3 FromMapUV(Vector2 uv) => new Vector3(MinX + uv.x * (MaxX - MinX), 0, MinZ + uv.y * (MaxZ - MinZ));
    }
}
