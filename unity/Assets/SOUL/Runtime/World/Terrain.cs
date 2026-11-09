using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Base terrain height (지형 고저차, 2026-11-07: terraced shelves + 2-4 m cliffs, up to ±10 m, per zone)
    // and water (늪 물 판정: shallow water slows and sinks, deep toxic sludge hurts without the anti-toxin core,
    // enemies too, puddles in other zones). Steep terrain is a wall going up (WorldCollision.TerrainWall),
    // a drop going down; ramp patches and trails keep a walkable way onto every shelf.
    //   Z0 ash ruins     ruin mounds on 2.4 m shelves                    (+ a few rain puddles)
    //   Z1 fallen city   flat streets, sunken ground in 3 m steps          (+ rain puddles)
    //   Z2 skyscraper    impact trenches (3.3 m steps), stepped rubble rises
    //   Z3 scrap gorge   2 m strata staircase, narrow gullies
    //   Z4 war foundry   slag heaps with 3.2 m ledges between flat yards   (+ oil pools)
    //   Z5 dead woods    root knolls on 2.3 m shelves, steep gullies       (+ mud pools)
    //   Z6 toxic mire    low basin, hummocks, shallow water everywhere, deep toxic sludge patches
    //   Z7 lab           engineered 2.5 m shelves on a 24 m grid: retaining walls or ramps
    // Flattened to 0 around authored things: the original arena, pods, boss arenas, chasms, gate sites
    // (corridors / funnels / ledges). Ruin plots and landmarks get a pad at their own terrain height.
    // Sampled from per-chunk 1 m grids (bilinear) so collision, ground mesh and props agree.
    public static class Terrain
    {
        public const int WATER_NONE = 0, WATER_CLEAN = 1, WATER_MIRE = 2, WATER_TOXIC = 3, WATER_OIL = 4;
        const float CHUNK = 32; const int N = 32;
        class Tile { public float[] h, surf, depth, wet, tox, basin; public byte[] type; }
        static readonly Dictionary<Vector2Int, Tile> tiles = new Dictionary<Vector2Int, Tile>();
        public static bool enabled = true;

        // ---------- queries ----------
        static Tile Get(float x, float z, out float fx, out float fz, out int i, out int j)
        {
            var k = new Vector2Int(Mathf.FloorToInt(x / CHUNK), Mathf.FloorToInt(z / CHUNK));
            if (!tiles.TryGetValue(k, out var t)) { t = Build(k); tiles[k] = t; }
            float lx = x - k.x * CHUNK, lz = z - k.y * CHUNK;
            i = Mathf.Clamp((int)lx, 0, N - 1); j = Mathf.Clamp((int)lz, 0, N - 1); fx = lx - i; fz = lz - j;
            return t;
        }
        static float Bi(float[] a, int i, int j, float fx, float fz)
        {
            int s = N + 1;
            return Mathf.Lerp(Mathf.Lerp(a[i * s + j], a[(i + 1) * s + j], fx), Mathf.Lerp(a[i * s + j + 1], a[(i + 1) * s + j + 1], fx), fz);
        }
        /// walkable ground height (water basins included: you stand on the bottom)
        public static float Height(float x, float z)
        {
            if (!enabled) return 0;
            var t = Get(x, z, out var fx, out var fz, out var i, out var j); return Bi(t.h, i, j, fx, fz);
        }
        /// water depth above the ground at (x, z) (0 = dry) and its kind
        public static float Water(float x, float z, out int type)
        {
            type = WATER_NONE; if (!enabled) return 0;
            var t = Get(x, z, out var fx, out var fz, out var i, out var j);
            float d = Bi(t.depth, i, j, fx, fz); if (d < .05f) return 0;
            type = t.type[(i + (fx > .5f ? 1 : 0)) * (N + 1) + j + (fz > .5f ? 1 : 0)];
            if (type == WATER_NONE) type = WATER_MIRE;
            return d;
        }
        /// wet bank 0..1 just outside the water line (ground darkens, GroundColor) and under it (1)
        public static float Wet(float x, float z)
        {
            if (!enabled) return 0;
            var t = Get(x, z, out var fx, out var fz, out var i, out var j); return Bi(t.wet, i, j, fx, fz);
        }
        /// movement multiplier in water (players and enemies): 1 dry .. 0.6 knee deep
        public static float Slow(float x, float z) { float d = Water(x, z, out _); return d < .08f ? 1 : Mathf.Lerp(1, .6f, Mathf.Clamp01((d - .08f) / .2f)); }
        /// standing in deep toxic sludge (hurts unless the anti-toxin core is owned)
        public static bool Toxic(float x, float z) { float d = Water(x, z, out int ty); return ty == WATER_TOXIC && d > .3f; }
        /// keep the cache bounded: drop tiles far from the player
        public static void Focus(Vector3 p)
        {
            if (tiles.Count < 700) return;
            var c = new Vector2Int(Mathf.FloorToInt(p.x / CHUNK), Mathf.FloorToInt(p.z / CHUNK)); var drop = new List<Vector2Int>();
            foreach (var k in tiles.Keys) if (Mathf.Abs(k.x - c.x) > 9 || Mathf.Abs(k.y - c.y) > 9) drop.Add(k);
            foreach (var k in drop) tiles.Remove(k);
        }
        public static void Clear() => tiles.Clear();
        /// per-chunk grids for the ground / water meshes (33 x 33, row i = x)
        public static void Grid(Vector2Int k, out float[] h, out float[] surf, out float[] depth, out byte[] type)
        {
            if (!tiles.TryGetValue(k, out var t)) { t = Build(k); tiles[k] = t; }
            h = t.h; surf = t.surf; depth = t.depth; type = t.type;
        }
        /// per-chunk toxic weight grid (0 mire .. 1 sludge, smooth) for the water mesh
        public static float[] ToxGrid(Vector2Int k)
        {
            if (!tiles.TryGetValue(k, out var t)) { t = Build(k); tiles[k] = t; }
            return t.tox;
        }
        /// how much of the mire is toxic sludge here (same field as WaterRaw's deep patches, smooth)
        static float ToxicWeight(float x, float z)
        {
            if (ZoneWorld.At(x, z) != 6) return 0;
            float deep = P(x, z, .018f, 5.7f) + .2f * P(x, z, .07f, 1.4f);
            return Sim.SmoothStep(deep, .75f, .79f);
        }

        // ---------- build ----------
        /// a flattening mask: `pad` ones level to their own base height (structure plots, landmarks: a rotated
        /// rectangle / circle), the others to 0 (pods, arenas, chasms, gate corridors)
        struct Flat { public Vector2 c, half; public float r, fall, cos, sin, baseH; public bool pad; }
        /// distance from a pad's flat top (0 on it). The top is the footprint (rounded rectangle) grown by an
        /// irregular 0..5 m (noise; never shrunk past the 1.5 m apron), so no straight plateau edge shows (2026-11-09)
        static float FlatDist(in Flat f, Vector2 p)
        {
            var d = p - f.c; if (!f.pad || f.half.x <= 0) return Organic(d.magnitude, f.r, p);
            float rr = Mathf.Min(3, .35f * Mathf.Min(f.half.x, f.half.y));
            float lx = Mathf.Abs(d.x * f.cos - d.y * f.sin) - (f.half.x - rr), lz = Mathf.Abs(d.x * f.sin + d.y * f.cos) - (f.half.y - rr);
            float sd = new Vector2(Mathf.Max(lx, 0), Mathf.Max(lz, 0)).magnitude - rr;
            float grow = Mathf.Lerp(-1.4f, 5, .5f + .5f * S(p.x, p.y, .06f, 3.9f));
            return Mathf.Max(0, sd - grow);
        }
        /// an authored flat of radius r: the edge wanders ±25 % of the distance outside it (no perfect circles)
        static float Organic(float d, float r, Vector2 p) => d <= r ? d : r + (d - r) * (1 + .3f * S(p.x, p.y, .045f, 6.3f));
        /// 0-level mask at p (1 = untouched)
        static float ZeroMask(List<Flat> flats, float x, float z)
        {
            float F = 1; var p = new Vector2(x, z);
            if (Mathf.Abs(x) < 42 + 40 && Mathf.Abs(z) < 42 + 40)
            {   // original arena: a rounded square whose fall-off edge wanders (was max(|x|,|z|): ruler-straight sides)
                float ax = Mathf.Max(Mathf.Abs(x) - 30, 0), az = Mathf.Max(Mathf.Abs(z) - 30, 0), d = 30 + new Vector2(ax, az).magnitude;
                F = Sim.SmoothStep(Organic(d, 42, p), 42, 72);
            }
            foreach (var f in flats) { if (f.pad) continue; float d = Organic((p - f.c).magnitude, f.r, p); if (d < f.r + f.fall) F = Mathf.Min(F, Sim.SmoothStep(d, f.r, f.r + f.fall)); }
            return F;
        }
        /// trail closeness on a 4 m lattice per chunk (ZoneRoutes.Dist is too dear per 1 m vertex)
        static float[] TrailLattice(float x0, float z0)
        {
            var a = new float[9 * 9];
            for (int i = 0; i <= 8; i++) for (int j = 0; j <= 8; j++) a[i * 9 + j] = TrailW(ZoneRoutes.Dist(x0 + i * 4, z0 + j * 4), x0 + i * 4, z0 + j * 4);
            return a;
        }
        /// 1 on and beside a trail .. 0 from 5-12 m (ZoneRoutes.Dist is only exact within ~12.6 m: buckets hold nearer
        /// segments). The band edge wanders with noise so the cut through the shelves isn't ruler-straight along the
        /// string-pulled trail segments (2026-11-09); the trail itself (< 3.5 m) always stays cliff-free.
        static float TrailW(float dist, float x, float z)
        {
            float w = S(x, z, .05f, 8.8f);
            return 1 - Sim.SmoothStep(dist, 3.5f + 1.5f * (w + 1), 8 + 2 * (w + 1));   // outer edge ≤ 12 m (Dist's exact range)
        }
        /// 0..1 per 4 m lattice point: 1 on flat or hollow ground, 0 on slopes (> ~0.2 grade over 4 m), shelf edges
        /// and crowns (higher than the ground around). Raw relief (no pads / flats: water is masked off those anyway).
        static float[] BasinLattice(float x0, float z0, float[] trails)
        {
            var a = new float[9 * 9];
            for (int i = 0; i <= 8; i++) for (int j = 0; j <= 8; j++)
            {
                float x = x0 + i * 4, z = z0 + j * 4, tw = trails[i * 9 + j], c = Relief(x, z, tw), sum = 0, steep = 0;
                for (int k = 0; k < 6; k++)
                {
                    float an = k * Mathf.PI / 3, n = Relief(x + 4 * Mathf.Cos(an), z + 4 * Mathf.Sin(an), tw);
                    sum += n; steep = Mathf.Max(steep, Mathf.Abs(n - c) / 4);
                }
                float concave = sum / 6 - c;
                a[i * 9 + j] = (1 - Sim.SmoothStep(steep, .07f, .2f)) * Sim.SmoothStep(concave, -.2f, -.02f);
            }
            return a;
        }
        static Tile Build(Vector2Int k)
        {
            var t = Begin(k, out var flats, out var trails); Rows(k, t, flats, trails, 0, N + 1); return t;
        }
        /// build a tile in `parts` row bands (one per step) so the streamer can spread its cost over frames
        public static IEnumerable<bool> BuildSteps(Vector2Int k, int parts)
        {
            if (tiles.ContainsKey(k)) yield break;
            var t = Begin(k, out var flats, out var trails); yield return true;
            for (int p = 0; p < parts; p++) { Rows(k, t, flats, trails, (N + 1) * p / parts, (N + 1) * (p + 1) / parts); yield return true; }
            if (!tiles.ContainsKey(k)) tiles[k] = t;   // a query may have built it meanwhile (same data)
        }
        static Tile Begin(Vector2Int k, out List<Flat> flats, out float[] trails)
        {
            int s = N + 1;
            flats = Flats(k.x * CHUNK, k.y * CHUNK); trails = TrailLattice(k.x * CHUNK, k.y * CHUNK);
            return new Tile { h = new float[s * s], surf = new float[s * s], depth = new float[s * s], wet = new float[s * s], tox = new float[s * s], type = new byte[s * s] };
        }
        static void Rows(Vector2Int k, Tile t, List<Flat> flats, float[] trails, int i0, int i1)
        {
            float x0 = k.x * CHUNK, z0 = k.y * CHUNK; int s = N + 1;
            for (int i = i0; i < i1; i++) for (int j = 0; j <= N; j++)
            {
                float x = x0 + i, z = z0 + j; int idx = i * s + j;
                var p = new Vector2(x, z);
                int ti = Mathf.Min(i / 4, 7), tj = Mathf.Min(j / 4, 7); float fx = (i - ti * 4) / 4f, fz = (j - tj * 4) / 4f;
                float tw = Mathf.Lerp(Mathf.Lerp(trails[ti * 9 + tj], trails[(ti + 1) * 9 + tj], fx), Mathf.Lerp(trails[ti * 9 + tj + 1], trails[(ti + 1) * 9 + tj + 1], fx), fz);
                float F = ZeroMask(flats, x, z);
                float relief = Relief(x, z, tw) * F;
                // structure pads: level to the terrain height at their centre, blend back to the slope around them
                foreach (var f in flats)
                {
                    if (!f.pad) continue; float d = FlatDist(f, p); if (d >= f.r + f.fall) continue;
                    float w = Sim.SmoothStep(d, f.r, f.r + f.fall); relief = Mathf.Lerp(f.baseH, relief, w); F = Mathf.Min(F, w);
                }
                // cache mesas (Caches.Spots): a flat top `rise` above the ground, 0.9 m cliff band with a wandering edge
                for (int mi = 0; mi < Caches.Spots.Length; mi++)
                {
                    var m = Caches.Spots[mi]; float d = (p - m.c).magnitude; if (d > m.r + 4) continue;
                    if (float.IsNaN(mesaBase[mi])) mesaBase[mi] = Relief(m.c.x, m.c.y);
                    float edge = m.r * (1 + .12f * S(x, z, .35f, 2.2f + mi));
                    float top = mesaBase[mi] + m.rise, mk = 1 - Sim.SmoothStep(d, edge, edge + .9f);
                    relief = Mathf.Max(relief, Mathf.Lerp(relief, top, mk)); F = Mathf.Min(F, Sim.SmoothStep(d, edge, edge + 3));
                }
                float wf = Sim.SmoothStep(F, .55f, .9f);
                float depth = WaterRaw(x, z, out byte ty, out float wet) * wf;
                // water only settles where the ground holds it: flat or hollow, never draped over a slope or a shelf
                // edge (2026-11-09: puddles were placed by noise alone and climbed hills)
                if (depth > 0 || wet > 0)
                {
                    t.basin ??= BasinLattice(x0, z0, trails);
                    float b = Mathf.Lerp(Mathf.Lerp(t.basin[ti * 9 + tj], t.basin[(ti + 1) * 9 + tj], fx), Mathf.Lerp(t.basin[ti * 9 + tj + 1], t.basin[(ti + 1) * 9 + tj + 1], fx), fz);
                    depth *= b; wet *= Mathf.Sqrt(b);
                }
                t.wet[idx] = wet * wf;
                // toxic sludge is a continuous weight (no cell-sized seams); trails fade it out so they stay wadable
                // without the core, over 6..13 m instead of a hard 9 m cut (that cut drew straight lines along trails)
                float tox = 0;
                if (ty == WATER_TOXIC || ty == WATER_MIRE)
                {
                    float trail = Sim.SmoothStep(ZoneRoutes.Dist(x, z), 6, 13);
                    tox = ToxicWeight(x, z) * trail;
                    if (ty == WATER_TOXIC) depth = Mathf.Lerp(Mathf.Min(depth, .3f), depth, trail);
                    ty = (byte)(tox > .5f ? WATER_TOXIC : WATER_MIRE);
                }
                t.tox[idx] = tox;
                t.h[idx] = relief - depth; t.depth[idx] = depth; t.type[idx] = ty; t.surf[idx] = relief - .04f;
            }
        }
        /// masks touching the square [x0, x0 + size]² (size 0 = a point): 0-level ones always, pads optionally
        static List<Flat> Flats(float x0, float z0, float size = CHUNK, bool pads = true)
        {
            var l = new List<Flat>(); var c = new Vector2(x0 + size / 2, z0 + size / 2); float reach = size * .75f;
            // the 0-level falls are long (≥ 30 m) so ±10 m of relief eases in at ≤ ~0.5 grade
            void Add(Vector2 p, float r, float fall) { if ((p - c).magnitude < r + fall + reach) l.Add(new Flat { c = p, r = r, fall = fall }); }
            foreach (var p in ZoneAreas.Pods) Add(new Vector2(p.pos.x, p.pos.z), 12, 30);
            foreach (var sh in ZoneAreas.Shrines) Add(new Vector2(sh.pos.x, sh.pos.z), 14, 30);
            foreach (var a in ZoneAreas.Arenas) { Add(new Vector2(a.center.x, a.center.z), a.radius + 12, 34); Add(a.ropeSite.pos, 30, 30); }
            foreach (var ch in ZoneAreas.Chasms) Add(new Vector2(ch.center.x, ch.center.z), ch.halfLength + 12, 30);
            foreach (var s in ZoneWorld.Sites) Add(s.pos, 70, 40);
            if (!pads) return l;
            foreach (var lm in Regions.Landmarks) AddPad(l, c, reach, new Vector2(lm.pos.x, lm.pos.z), Vector2.zero, 0, 16, 12);
            foreach (var pp in WorldStreamer.PlotPads(x0 - 40, z0 - 40, size + 80)) AddPad(l, c, reach, pp.c, pp.half, pp.yaw, 0, 10);
            return l;
        }
        static readonly Dictionary<Vector2, float> padBase = new Dictionary<Vector2, float>();
        static readonly float[] mesaBase = NaNs(Caches.Spots.Length);
        static float[] NaNs(int n) { var a = new float[n]; for (int i = 0; i < n; i++) a[i] = float.NaN; return a; }
        /// a structure pad: flat over its footprint (half > 0: rotated rectangle, else a circle of radius r) at the
        /// terrain height of its centre (with the 0-level masks applied, never the other pads: no recursion)
        static void AddPad(List<Flat> l, Vector2 c, float reach, Vector2 p, Vector2 half, float yaw, float r, float fall)
        {
            float ext = Mathf.Max(r, half.magnitude);
            if ((p - c).magnitude > ext + fall + reach) return;
            if (!padBase.TryGetValue(p, out float b))
            {
                b = Relief(p.x, p.y, TrailW(ZoneRoutes.Dist(p.x, p.y), p.x, p.y)) * ZeroMask(Flats(p.x, p.y, 0, false), p.x, p.y);
                padBase[p] = b;
            }
            l.Add(new Flat { c = p, half = half, r = half.x > 0 ? 0 : r, fall = fall, cos = Mathf.Cos(yaw), sin = Mathf.Sin(yaw), baseH = b, pad = true });
        }

        // ---------- relief per zone ----------
        static float P(float x, float z, float f, float o) => Mathf.PerlinNoise(x * f + o, z * f + o * 1.37f);
        static float S(float x, float z, float f, float o) => (P(x, z, f, o) - .5f) * 2;
        static float Ridged(float x, float z, float f, float o) => 1 - Mathf.Abs(S(x, z, f, o));
        static float Terrace(float h, float step, float sharp)
        {
            float q = Mathf.Floor(h / step), fr = h / step - q;
            return (q + Sim.SmoothStep(fr, 1 - sharp, 1)) * step;
        }
        static float Hash(int x, int z, int k) { unchecked { int h = x * 73856093 ^ z * 19349663 ^ k * 83492791; h ^= h >> 13; h *= 1274126177; return ((h >> 8) & 0xffff) / 65535f; } }
        /// Z7: engineered shelves on a 24 m lattice, levels in 2.5 m steps from a smooth field (neighbours mostly one
        /// step apart); each cell's x / z transition is a retaining wall (1.4 m wide: a cliff) or a 12 m ramp
        static float LabTerrace(float x, float z, float soft)
        {
            const float C = 24, STEP = 2.5f;
            float wx = x + 5 * S(x, z, .013f, 4.6f), wz = z + 5 * S(x, z, .013f, 9.2f);   // gentle bends (2026-11-09)
            float gx = wx / C, gz = wz / C; int ix = Mathf.FloorToInt(gx), iz = Mathf.FloorToInt(gz);
            float L(int a, int b) => Mathf.Clamp(Mathf.Round(20 * (P(a * C, b * C, .006f, 7.7f) - .5f) / STEP), -3, 3) * STEP;
            float Edge(float f, bool ramp) => ramp ? Sim.SmoothStep(f, .22f, .78f) : Sim.SmoothStep(f, .47f, .53f);
            float tx = Mathf.Lerp(Edge(gx - ix, Hash(ix, iz, 78) < .35f), Edge(gx - ix, true), soft);
            float tz = Mathf.Lerp(Edge(gz - iz, Hash(ix, iz, 79) < .35f), Edge(gz - iz, true), soft);
            return Mathf.Lerp(Mathf.Lerp(L(ix, iz), L(ix + 1, iz), tx), Mathf.Lerp(L(ix, iz + 1), L(ix + 1, iz + 1), tx), tz);
        }
        static float SS(float v, float a, float b) => Sim.SmoothStep(v, a, b);
        /// terraced shape `terr` (flat shelves split by 2-4 m cliffs) eased to the continuous `soft` shape on
        /// ramp patches (~20 % of the ground) and along trails, so every shelf has a walkable way up
        static float Shelf(float terr, float soft, float x, float z, float trail, float o)
        {
            float ramp = SS(P(x, z, .014f, o), .6f, .7f);
            return Mathf.Lerp(terr, soft, Mathf.Max(ramp, trail));
        }
        public static float ZoneRelief(int zone, float x, float z) => ZoneRelief(zone, x, z, 0);
        /// relief per zone (±10 m). trail 0..1 = closeness to a trail (no cliffs there)
        public static float ZoneRelief(int zone, float x, float z, float trail)
        {
            float r = 0;
            switch (zone)
            {
                case 0:   // ruin mounds and shelves: 2.4 m steps of a broad rolling field
                {
                    float b = 6.5f * S(x, z, .0055f, 3.1f) + 2.2f * S(x, z, .02f, 8.7f);
                    r = Shelf(Terrace(b, 2.1f, .1f), b, x, z, trail, 1.1f) + .2f * S(x, z, .09f, 4.4f); break;
                }
                case 1:   // fallen city: flat streets, sunken ground collapsing in 3 m steps (to ~-9 m)
                {
                    float wx = 30 * S(x, z, .006f, 1.9f), n = P(x + wx, z - wx, .009f, 2.4f), street = .8f + .5f * S(x, z, .015f, 5.5f);
                    r = Shelf(street - Terrace(9.5f * SS(n, .56f, .8f), 3.1f, .14f), street - 6 * SS(n, .46f, .95f), x, z, trail, 2.2f); break;
                }
                case 2:   // impact trenches (to -10 m, 3.3 m steps) between stepped rubble rises
                {
                    float R = Ridged(x * .45f, z, .0075f, 6.6f), rise = 4.5f * SS(P(x, z, .02f, 4.2f), .55f, .85f);
                    r = Shelf(Terrace(rise, 2.2f, .12f) - Terrace(10 * SS(R, .74f, .99f), 3.3f, .1f), .6f * rise - 5 * SS(R, .6f, 1), x, z, trail, 3.3f)
                        + .2f * S(x, z, .08f, 9.9f); break;
                }
                case 3:   // strata staircase: 2 m beds over a ±9 m tilt, cut by narrow gullies
                {
                    float b = 24 * (P(x * .7f, z, .0065f, 9.1f) - .5f), R = Ridged(x, z, .02f, 1.3f);
                    r = Shelf(Terrace(b, 2, .08f) - 3 * SS(R, .88f, .98f), b - 1.5f * SS(R, .7f, 1), x, z, trail, 4.4f); break;
                }
                case 4:   // slag heaps up to 10 m with 3.2 m ledges, flat yards between
                {
                    float n = P(x, z, .012f, 7.3f), hill = 10 * Mathf.Pow(SS(n, .5f, .85f), 1.2f);
                    r = Shelf(Terrace(hill, 3.2f, .18f), 6.5f * SS(n, .4f, .95f), x, z, trail, 5.5f) + .35f * S(x, z, .1f, 2.8f); break;
                }
                case 5:   // root knolls on 2.3 m shelves, steep-walled gullies
                {
                    float knoll = 7.5f * SS(P(x, z, .014f, 5.9f), .45f, .85f), R = Ridged(x, z, .012f, 3.7f);
                    r = Shelf(Terrace(knoll, 2.0f, .14f) - 4.5f * SS(R, .86f, .97f), .5f * knoll - 1.5f * SS(R, .7f, 1), x, z, trail, 6.6f)
                        + .35f * S(x, z, .12f, 6.2f); break;
                }
                case 6:   // low basin (water sits in it): hummocks with 1.1 m peat banks
                {
                    float hum = 2.2f * SS(P(x, z, .03f, 8.4f), .55f, .85f);
                    r = -2.6f + Shelf(Terrace(hum, 1.1f, .3f), .8f * hum, x, z, trail, 7.7f); break;
                }
                case 7: r = LabTerrace(x, z, trail); break;
            }
            return Mathf.Clamp(r, -10, 10);
        }
        /// unflattened relief with zone blending at borders (used by the map; Build passes its trail lattice)
        public static float Relief(float x, float z) => Relief(x, z, TrailW(ZoneRoutes.Dist(x, z), x, z));
        public static float Relief(float x, float z, float trail)
        {
            int self = ZoneWorld.Sample(x, z, out int other, out float d);
            if (self < 0) { self = Mathf.Max(0, other); other = -1; }
            float r = ZoneRelief(self, x, z, trail);
            if (other >= 0 && d < 26) r = Mathf.Lerp(r, ZoneRelief(other, x, z, trail), .5f * (1 - Sim.SmoothStep(d, 0, 26)));
            return r;
        }
        /// raw water depth by zone (before flattening masks)
        public static float WaterRaw(float x, float z, out byte type) => WaterRaw(x, z, out type, out _);
        /// ... plus the wet bank: a band of soaked ground around every water body (1 at the water line and under it)
        public static float WaterRaw(float x, float z, out byte type, out float wet)
        {
            type = WATER_NONE; wet = 0; int zone = ZoneWorld.At(x, z);
            float n, d = 0;
            float Bank(float v, float lo, float band) => Sim.SmoothStep(v, lo - band, lo);
            switch (zone)
            {
                case 6:
                    n = P(x, z, .035f, 3.3f) + .3f * P(x, z, .12f, 8.2f);
                    d = .35f * Sim.SmoothStep(n, .62f, .7f); type = WATER_MIRE; wet = Bank(n, .62f, .12f);
                    float deep = P(x, z, .018f, 5.7f) + .2f * P(x, z, .07f, 1.4f);
                    if (deep > .74f) { d = Mathf.Max(d, .7f * Sim.SmoothStep(deep, .74f, .8f)); if (deep > .76f) type = WATER_TOXIC; }
                    wet = Mathf.Max(wet, Bank(deep, .74f, .05f));
                    break;
                case 1: n = P(x, z, .08f, 2.2f); d = .2f * Sim.SmoothStep(n, .77f, .8f); type = WATER_CLEAN; wet = Bank(n, .77f, .05f); break;
                case 0: n = P(x, z, .07f, 6.1f); d = .18f * Sim.SmoothStep(n, .8f, .83f); type = WATER_CLEAN; wet = Bank(n, .8f, .05f); break;
                case 4: n = P(x, z, .05f, 4.4f); d = .25f * Sim.SmoothStep(n, .75f, .78f); type = WATER_OIL; wet = Bank(n, .75f, .04f); break;
                case 5: n = P(x, z, .06f, 9.6f); d = .25f * Sim.SmoothStep(n, .76f, .79f); type = WATER_MIRE; wet = Bank(n, .76f, .06f); break;
            }
            if (d < .02f) type = WATER_NONE;
            return d;
        }
    }
}
