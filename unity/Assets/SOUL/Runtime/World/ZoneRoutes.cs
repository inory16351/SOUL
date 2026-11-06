using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Route guiding (경로 유도). The ridges block; these lead the player to where the zone opens:
    //  - trails: worn roads / dirt paths from each zone's hub (pod or centre) to every passage of that
    //    zone, meandering, kept clear of scatter, darker ground (Regions.GroundColor)
    //  - edge debris: zone-typed rubble / wrecks / logs lining the trails (they form lanes)
    //  - traces: creature / machine traces (bones, husks, oil) along the last ~110 m before a passage
    //  - funnels: ridge spurs flaring out of each passage mouth (ZoneWorld.RidgeHeight)
    //  - landmarks: the destination zone's silhouette just past each passage, beside the trail
    // Layout only — meshes for traces / landmarks are Codex-owned (TraceProps, LandmarkModels).
    public static class ZoneRoutes
    {
        public const float HALF = 2.4f, EDGE = 4.6f;      // trail core / soft edge half widths
        public const float BUCKET = 64;
        public class Seg { public Vector2 a, b; public int zone, trail; public float s0, s1; public bool approach; public Vector2 site; }
        public static readonly List<Seg> Segs = new List<Seg>();
        public static readonly List<List<Vector2>> Trails = new List<List<Vector2>>();
        static readonly Dictionary<Vector2Int, List<Seg>> buckets = new Dictionary<Vector2Int, List<Seg>>();

        static ZoneRoutes() { Build(); }

        public static Vector2 Hub(int zone)
        {
            foreach (var p in ZoneAreas.Pods) if (p.zone == zone) return new Vector2(p.pos.x, p.pos.z) + new Vector2(0, 8);
            return ZoneWorld.Center(zone);
        }
        static float N(float x, float z, float f, float o) => (Mathf.PerlinNoise(x * f + o, z * f + o * 1.7f) - .5f) * 2;

        static void Build()
        {
            foreach (var s in ZoneWorld.Sites)
            {
                var mouthA = s.pos - s.n * (ZoneWorld.CUT_L + 8); var mouthB = s.pos + s.n * (ZoneWorld.CUT_L + 8);
                // enter each funnel from the front, straight down the passage axis (spurs flank it)
                AddTrail(s.link.a, Hub(s.link.a), mouthA, s.pos - s.n * 10, s.pos, Front(s, -1));
                AddTrail(s.link.b, Hub(s.link.b), mouthB, s.pos + s.n * 10, s.pos, Front(s, 1));
            }
            foreach (var a in ZoneAreas.Arenas)   // pod plaza -> arena gate, and to the rope ledge
            {
                var gate = new Vector2(a.center.x, a.center.z) + new Vector2(Mathf.Sin(a.entranceYaw), Mathf.Cos(a.entranceYaw)) * (a.radius + 10);
                AddTrail(a.zone, Hub(a.zone), gate, gate, gate);
                var rs = a.ropeSite; AddTrail(a.zone, gate, rs.pos - rs.n * 22, rs.pos - rs.n * 12, rs.pos - rs.n * 12);
            }
        }
        /// the point in front of a funnel where a trail lines up with the passage axis: as far out as 94 m,
        /// but never past rock (narrow zones / massifs close behind the passage)
        static Vector2 Front(ZoneWorld.Site s, float side)
        {
            var mouth = s.pos + s.n * side * (ZoneWorld.CUT_L + 8); var best = mouth;
            for (float k = 4; k <= 52; k += 4)
            {
                var p = mouth + s.n * side * k;
                if (ZoneWorld.Solid(p.x, p.y) || ZoneWorld.Solid(p.x + s.tan.x * 3, p.y + s.tan.y * 3) || ZoneWorld.Solid(p.x - s.tan.x * 3, p.y - s.tan.y * 3)) break;
                best = p;
            }
            return best;
        }
        /// hub -> (meander) -> mouth -> straight -> inner -> site
        static void AddTrail(int zone, Vector2 hub, Vector2 mouth, Vector2 inner, Vector2 site) => AddTrail(zone, hub, mouth, inner, site, mouth);
        static void AddTrail(int zone, Vector2 hub, Vector2 mouth, Vector2 inner, Vector2 site, Vector2 front)
        {
            // route inside the zone (outlines are concave: a straight line could cross mountains), then
            // string-pull and add a gentle meander where there is room
            var route = Route(zone, hub, front);
            var pts = new List<Vector2>();
            for (int i = 0; i < route.Count; i++)
            {
                var q = route[i];
                if (i > 0 && i < route.Count - 1)
                {
                    var dir = (route[i + 1] - route[i - 1]).normalized; var perp = new Vector2(dir.y, -dir.x);
                    var m = q + perp * (12 * N(q.x, q.y, 1 / 120f, 13.7f + zone));
                    ZoneWorld.Sample(m.x, m.y, out _, out float bd);
                    if (ZoneWorld.At(m.x, m.y) == zone && bd > ZoneWorld.RIDGE_W * 2.2f + 8 && ZoneWorld.Funnel(m.x, m.y) < .1f && ZoneWorld.Funnel((m.x + pts[pts.Count - 1].x) * .5f, (m.y + pts[pts.Count - 1].y) * .5f) < .1f) q = m;
                }
                pts.Add(q);
            }
            if ((pts[pts.Count - 1] - front).sqrMagnitude > 1) pts.Add(front);
            if ((pts[pts.Count - 1] - mouth).sqrMagnitude > 1) pts.Add(mouth); if ((inner - mouth).sqrMagnitude > 1) pts.Add(inner); if ((site - inner).sqrMagnitude > 1) pts.Add(site);
            int ti = Trails.Count; Trails.Add(pts);
            float acc = 0;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float L = (pts[i + 1] - pts[i]).magnitude;
                var sg = new Seg { a = pts[i], b = pts[i + 1], zone = zone, trail = ti, s0 = acc, s1 = acc + L, site = site, approach = (pts[i] - site).magnitude < 140 };
                acc += L; Segs.Add(sg);
                float m = EDGE + 8;
                int bx0 = Mathf.FloorToInt((Mathf.Min(sg.a.x, sg.b.x) - m) / BUCKET), bx1 = Mathf.FloorToInt((Mathf.Max(sg.a.x, sg.b.x) + m) / BUCKET);
                int bz0 = Mathf.FloorToInt((Mathf.Min(sg.a.y, sg.b.y) - m) / BUCKET), bz1 = Mathf.FloorToInt((Mathf.Max(sg.a.y, sg.b.y) + m) / BUCKET);
                for (int bx = bx0; bx <= bx1; bx++) for (int bz = bz0; bz <= bz1; bz++)
                {
                    var k = new Vector2Int(bx, bz); if (!buckets.TryGetValue(k, out var l)) buckets[k] = l = new List<Seg>(); l.Add(sg);
                }
            }
        }

        /// grid path (12 m, 8-neighbour BFS) from a to b through cells well inside the zone, string-pulled
        static List<Vector2> Route(int zone, Vector2 a, Vector2 b)
        {
            // narrow passes between massifs: retry with a smaller clearance before giving up
            foreach (float margin in new[] { ZoneWorld.RIDGE_W * 2 + 4, ZoneWorld.RIDGE_W * 1.4f, ZoneWorld.RIDGE_W + 2 })
            { var r = Route(zone, a, b, margin); if (r != null) return r; }
            return new List<Vector2> { a, b };
        }
        static List<Vector2> Route(int zone, Vector2 a, Vector2 b, float margin)
        {
            const float G = 12; var o = ZoneWorld.Outline(zone);
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var v in o) { x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); z0 = Mathf.Min(z0, v.y); z1 = Mathf.Max(z1, v.y); }
            x0 -= G; z0 -= G; int w = Mathf.CeilToInt((x1 - x0) / G) + 2, h = Mathf.CeilToInt((z1 - z0) / G) + 2;
            var ok = new bool[w * h];
            bool Good(float x, float z)
            {
                if (ZoneWorld.Sample(x, z, out _, out float d) != zone || d < margin) return false;
                if (ZoneWorld.Funnel(x, z) > .1f || ZoneWorld.Funnel(x + 6, z) > .1f || ZoneWorld.Funnel(x - 6, z) > .1f || ZoneWorld.Funnel(x, z + 6) > .1f || ZoneWorld.Funnel(x, z - 6) > .1f) return false;   // keep clear of spur flanks
                foreach (var ar in ZoneAreas.Arenas) if (new Vector2(x - ar.center.x, z - ar.center.z).magnitude < ar.radius + 14) return false;
                return true;
            }
            for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) ok[i * h + j] = Good(x0 + i * G, z0 + j * G);
            Vector2Int C(Vector2 p) => new Vector2Int(Mathf.Clamp(Mathf.RoundToInt((p.x - x0) / G), 0, w - 1), Mathf.Clamp(Mathf.RoundToInt((p.y - z0) / G), 0, h - 1));
            Vector2Int Snap(Vector2Int c)
            {   // nearest walkable cell (endpoints sit near ridges / arena gates)
                if (ok[c.x * h + c.y]) return c;
                for (int r = 1; r < 12; r++) for (int dx = -r; dx <= r; dx++) for (int dz = -r; dz <= r; dz++)
                { int i = c.x + dx, j = c.y + dz; if (i >= 0 && j >= 0 && i < w && j < h && ok[i * h + j]) return new Vector2Int(i, j); }
                return c;
            }
            var s = Snap(C(a)); var e = Snap(C(b));
            var prev = new int[w * h]; for (int i = 0; i < prev.Length; i++) prev[i] = -2;
            var q = new Queue<int>(); int si = s.x * h + s.y, ei = e.x * h + e.y; prev[si] = -1; q.Enqueue(si);
            while (q.Count > 0 && prev[ei] == -2)
            {
                int k = q.Dequeue(), ci = k / h, cj = k % h;
                for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue; int ni = ci + dx, nj = cj + dz; if (ni < 0 || nj < 0 || ni >= w || nj >= h) continue;
                    int nk = ni * h + nj; if (prev[nk] != -2 || !ok[nk]) continue; prev[nk] = k; q.Enqueue(nk);
                }
            }
            var cells = new List<Vector2>();
            if (prev[ei] == -2) return null;
            for (int k = ei; k >= 0; k = prev[k]) cells.Add(new Vector2(x0 + (k / h) * G, z0 + (k % h) * G));
            cells.Reverse(); cells[0] = a; cells.Add(b);
            // string pulling: keep a point only when the straight line from the last kept one leaves good ground
            var outp = new List<Vector2> { cells[0] }; int last = 0;
            bool Clear(Vector2 p, Vector2 r) { float L = (r - p).magnitude; for (float t = 6; t < L - 6; t += 6) { var m = Vector2.Lerp(p, r, t / L); if (!Good(m.x, m.y)) return false; } return true; }
            for (int i = 2; i < cells.Count; i++) if (!Clear(cells[last], cells[i])) { outp.Add(cells[i - 1]); last = i - 1; }
            outp.Add(cells[cells.Count - 1]);
            // split long legs so the meander has joints
            var fine = new List<Vector2> { outp[0] };
            for (int i = 1; i < outp.Count; i++) { float L = (outp[i] - outp[i - 1]).magnitude; int n = Mathf.Max(1, Mathf.FloorToInt(L / 70)); for (int k = 1; k <= n; k++) fine.Add(Vector2.Lerp(outp[i - 1], outp[i], k / (float)n)); }
            return fine;
        }
        static float SegDist(Seg s, Vector2 p, out float u)
        {
            var ab = s.b - s.a; float L2 = ab.sqrMagnitude; u = L2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - s.a, ab) / L2) : 0;
            return (s.a + ab * u - p).magnitude;
        }
        /// distance from (x, z) to the nearest trail centre line (large when none is near)
        public static float Dist(float x, float z) => Dist(x, z, out _);
        public static float Dist(float x, float z, out Seg nearest)
        {
            nearest = null; float best = 1e4f; var p = new Vector2(x, z);
            if (!buckets.TryGetValue(new Vector2Int(Mathf.FloorToInt(x / BUCKET), Mathf.FloorToInt(z / BUCKET)), out var l)) return best;
            foreach (var s in l) { float d = SegDist(s, p, out _); if (d < best) { best = d; nearest = s; } }
            return best;
        }
        public static IEnumerable<Seg> SegsNear(float x0, float z0, float size)
        {
            var seen = new HashSet<Seg>();
            for (int bx = Mathf.FloorToInt(x0 / BUCKET); bx <= Mathf.FloorToInt((x0 + size) / BUCKET); bx++)
                for (int bz = Mathf.FloorToInt(z0 / BUCKET); bz <= Mathf.FloorToInt((z0 + size) / BUCKET); bz++)
                    if (buckets.TryGetValue(new Vector2Int(bx, bz), out var l)) foreach (var s in l) if (seen.Add(s)) yield return s;
        }

        /// trail ground colour per zone (sRGB)
        public static Color TrailColor(int zone)
        {
            int[] hex = { 0x4a4644, 0x3f3f42, 0x48454e, 0x5a3a28, 0x3a2a22, 0x4a3e30, 0x2e3526, 0x7c8284 };
            return MeshBuilder.Hex(hex[Mathf.Clamp(zone, 0, hex.Length - 1)]);
        }
        /// blend weight of the trail at a ground point (0 off the trail .. ~1 on it), worn patchy
        public static float TrailWeight(float x, float z)
        {
            float d = Dist(x, z); if (d > EDGE) return 0;
            float k = 1 - Sim.SmoothStep(d, HALF, EDGE);
            return k * (.7f + .3f * Mathf.PerlinNoise(x * .35f + 2.1f, z * .35f + 7.9f));
        }

        // ---------- placements (called per chunk by WorldStreamer) ----------
        public struct Placement { public Vector3 pos; public float yaw; public int zone; public int kind; public int seed; }   // kind 0 edge debris, 1 trace
        static float H01(int a, int b) { unchecked { int h = a * 73856093 ^ b * 19349663; h ^= h >> 13; h *= 1274126177; return ((h >> 8) & 0xffff) / 65535f; } }
        public static void Placements(float x0, float z0, float size, List<Placement> outList)
        {
            const float EDGE_STEP = 13, TRACE_STEP = 17;
            foreach (var s in SegsNear(x0, z0, size))
            {
                var ab = s.b - s.a; float L = ab.magnitude; if (L < 1) continue; var dir = ab / L; var perp = new Vector2(dir.y, -dir.x);
                float yaw = Mathf.Atan2(dir.x, dir.y);
                for (int k = Mathf.CeilToInt(s.s0 / EDGE_STEP); k * EDGE_STEP <= s.s1; k++)
                {
                    float r = H01(s.trail * 7919 + k, 11); if (r < .35f) continue;
                    // irregular rhythm: jitter along the trail, random side (mostly alternating), varied setback
                    float side = H01(k, s.trail + 17) < .75f ? (k % 2 == 0 ? 1 : -1) : (k % 2 == 0 ? -1 : 1);
                    var p = s.a + dir * (k * EDGE_STEP - s.s0 + (H01(k, s.trail + 31) - .5f) * 8) + perp * (side * (5.3f + 2.6f * H01(k, s.trail)));
                    if (p.x < x0 || p.x >= x0 + size || p.y < z0 || p.y >= z0 + size) continue;
                    if ((p - s.site).magnitude < ZoneWorld.CUT_L + 6) continue;
                    outList.Add(new Placement { pos = new Vector3(p.x, 0, p.y), yaw = yaw + (H01(k, 3) - .5f) * 1.2f, zone = s.zone, kind = 0, seed = s.trail * 131 + k });
                }
                if (!s.approach) continue;
                for (int k = Mathf.CeilToInt(s.s0 / TRACE_STEP); k * TRACE_STEP <= s.s1; k++)
                {
                    var p = s.a + dir * (k * TRACE_STEP - s.s0) + perp * ((H01(k, s.trail + 5) - .5f) * 3.2f);
                    float toSite = (p - s.site).magnitude; if (toSite < ZoneWorld.CUT_L + 4 || toSite > 115) continue;
                    if (p.x < x0 || p.x >= x0 + size || p.y < z0 || p.y >= z0 + size) continue;
                    outList.Add(new Placement { pos = new Vector3(p.x, 0, p.y), yaw = H01(k, 9) * Mathf.PI * 2, zone = s.zone, kind = 1, seed = s.trail * 977 + k });
                }
            }
        }
        /// zone-typed edge debris model id + builder
        public static (string id, System.Func<int, PropModel> build) EdgeModel(int zone, int seed)
        {
            float r = H01(seed, 77);
            switch (zone)
            {
                case 0: return r < .45f ? ("RubblePile", RegionProps.RubblePile) : r < .75f ? ("ConcreteSlab", RegionProps.ConcreteSlab) : ("BurntCar", RegionProps.BurntCar);
                case 1: return r < .45f ? ("BurntCar", RegionProps.BurntCar) : r < .75f ? ("RubblePile", RegionProps.RubblePile) : ("StreetLamp", RegionProps.StreetLamp);
                case 2: return r < .55f ? ("ConcreteSlab", RegionProps.ConcreteSlab) : ("RubblePile", RegionProps.RubblePile);
                case 3: case 4: return r < .6f ? ("ScrapHeap", RegionProps.ScrapHeap) : ("PipeRun", RegionProps.PipeRun);
                case 5: case 6: return r < .55f ? ("FallenLog", RegionProps.FallenLog) : ("Stump", RegionProps.Stump);
                default: return r < .6f ? ("ConcreteSlab", RegionProps.ConcreteSlab) : ("Pylon", RegionProps.Pylon);
            }
        }

        /// destination landmarks just past each passage, beside the trail (Regions adds them)
        public static IEnumerable<(Vector3 pos, float yaw, int zone)> PassageLandmarks()
        {
            foreach (var s in ZoneWorld.Sites)
            {
                if (s.link.kind == ZoneGraph.Kind.Free && s.index % 2 == 1) continue;   // not every free gap needs one
                for (int side = 0; side < 2; side++)
                {
                    int toZone = side == 0 ? s.link.b : s.link.a; float sg = side == 0 ? 1 : -1;
                    var p = s.pos + s.n * sg * (ZoneWorld.CUT_L + 62) + s.tan * (36 * (s.index % 2 == 0 ? 1 : -1));
                    if (ZoneWorld.At(p.x, p.y) != toZone || !ZoneWorld.Clear(p.x, p.y, 12)) continue;
                    yield return (new Vector3(p.x, 0, p.y), s.yaw + side * Mathf.PI, toZone);
                }
            }
        }
    }
}
