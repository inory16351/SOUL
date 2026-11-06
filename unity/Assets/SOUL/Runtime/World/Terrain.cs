using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Base terrain height (지형 고저차, user decision 2026-11-06: medium relief ≤ ~6 m, different per zone)
    // and water (늪 물 판정: shallow water slows and sinks, deep toxic sludge hurts without the anti-toxin core,
    // enemies too, puddles in other zones). Everything walkable; blocking stays with ridges and gates.
    //   Z0 ash ruins     rolling debris ground with low terrace steps     (+ a few rain puddles)
    //   Z1 fallen city   flat streets, collapsed sinkholes                (+ rain puddles)
    //   Z2 skyscraper    long impact trenches, rubble rises
    //   Z3 scrap gorge   stepped strata terraces, gullies
    //   Z4 war foundry   slag hills between flat yards                   (+ oil pools)
    //   Z5 dead woods    root knolls and gullies                         (+ mud pools)
    //   Z6 toxic mire    low basin, hummocks, shallow water everywhere, deep toxic sludge patches
    //   Z7 lab           engineered terraces (0 / 0.9 / 1.8 m) on a 24 m grid
    // Flattened to 0 around authored things: the original arena, pods, boss arenas, chasms, gate sites
    // (corridors / funnels / ledges), landmarks and ruin structure plots.
    // Sampled from per-chunk 1 m grids (bilinear) so collision, ground mesh and props agree.
    public static class Terrain
    {
        public const int WATER_NONE = 0, WATER_CLEAN = 1, WATER_MIRE = 2, WATER_TOXIC = 3, WATER_OIL = 4;
        const float CHUNK = 32; const int N = 32;
        class Tile { public float[] h, surf, depth; public byte[] type; }
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

        // ---------- build ----------
        struct Flat { public Vector2 c; public float r, fall; }
        static Tile Build(Vector2Int k)
        {
            float x0 = k.x * CHUNK, z0 = k.y * CHUNK; int s = N + 1;
            var t = new Tile { h = new float[s * s], surf = new float[s * s], depth = new float[s * s], type = new byte[s * s] };
            var flats = Flats(x0, z0);
            for (int i = 0; i <= N; i++) for (int j = 0; j <= N; j++)
            {
                float x = x0 + i, z = z0 + j; int idx = i * s + j;
                float F = 1; var p = new Vector2(x, z);
                if (Mathf.Abs(x) < 42 + 16 && Mathf.Abs(z) < 42 + 16) F = Mathf.Min(F, Sim.SmoothStep(Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)), 42, 58));   // original arena
                foreach (var f in flats) { float d = (p - f.c).magnitude; if (d < f.r + f.fall) F = Mathf.Min(F, Sim.SmoothStep(d, f.r, f.r + f.fall)); }
                float relief = Relief(x, z) * F;
                float depth = WaterRaw(x, z, out byte ty) * Sim.SmoothStep(F, .55f, .9f);
                if (ty == WATER_TOXIC && ZoneRoutes.Dist(x, z) < 9) { ty = WATER_MIRE; depth = Mathf.Min(depth, .3f); }   // trails stay wadable without the core
                t.h[idx] = relief - depth; t.depth[idx] = depth; t.type[idx] = ty; t.surf[idx] = relief - .04f;
            }
            return t;
        }
        static List<Flat> Flats(float x0, float z0)
        {
            var l = new List<Flat>(); var c = new Vector2(x0 + CHUNK / 2, z0 + CHUNK / 2); const float NEAR = 140;
            void Add(Vector2 p, float r, float fall) { if ((p - c).magnitude < r + fall + CHUNK) l.Add(new Flat { c = p, r = r, fall = fall }); }
            foreach (var p in ZoneAreas.Pods) Add(new Vector2(p.pos.x, p.pos.z), 12, 14);
            foreach (var sh in ZoneAreas.Shrines) Add(new Vector2(sh.pos.x, sh.pos.z), 14, 14);
            foreach (var a in ZoneAreas.Arenas) { Add(new Vector2(a.center.x, a.center.z), a.radius + 12, 16); Add(a.ropeSite.pos, 30, 14); }
            foreach (var ch in ZoneAreas.Chasms) Add(new Vector2(ch.center.x, ch.center.z), ch.halfLength + 12, 14);
            foreach (var s in ZoneWorld.Sites) if ((s.pos - c).magnitude < NEAR) Add(s.pos, 70, 22);
            foreach (var lm in Regions.Landmarks) Add(new Vector2(lm.pos.x, lm.pos.z), 16, 12);
            foreach (var pp in WorldStreamer.PlotCentres(x0 - 40, z0 - 40, CHUNK + 80)) Add(pp, 24, 10);
            return l;
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
        static float LabTerrace(float x, float z)
        {
            const float C = 24; float gx = x / C, gz = z / C; int ix = Mathf.FloorToInt(gx), iz = Mathf.FloorToInt(gz);
            float L(int a, int b) => Mathf.Floor(Hash(a, b, 77) * 3) * .9f;
            float tx = Sim.SmoothStep(gx - ix, .42f, .58f), tz = Sim.SmoothStep(gz - iz, .42f, .58f);
            return Mathf.Lerp(Mathf.Lerp(L(ix, iz), L(ix + 1, iz), tx), Mathf.Lerp(L(ix, iz + 1), L(ix + 1, iz + 1), tx), tz);
        }
        public static float ZoneRelief(int zone, float x, float z)
        {
            switch (zone)
            {
                case 0: { float r = 2.2f * S(x, z, .011f, 3.1f) + .9f * S(x, z, .035f, 8.7f); return Mathf.Lerp(r, Terrace(r, .9f, .25f), .6f); }
                case 1: { float wx = 30 * S(x, z, .006f, 1.9f); return .6f * S(x, z, .02f, 5.5f) - 3f * Sim.SmoothStep(P(x + wx, z - wx, .014f, 2.4f), .64f, .74f); }
                case 2: return -2.8f * Sim.SmoothStep(Ridged(x * .45f, z, .012f, 6.6f), .86f, .97f) + 1.6f * S(x, z, .03f, 4.2f);
                case 3: { float b = 5 * P(x * .7f, z, .008f, 9.1f); return Terrace(b, 1.3f, .3f) - 2.5f - 2 * Sim.SmoothStep(Ridged(x, z, .02f, 1.3f), .9f, .98f); }
                case 4: return 4 * Mathf.Pow(Sim.SmoothStep(P(x, z, .025f, 7.3f), .55f, .9f), 1.5f) + .4f * S(x, z, .1f, 2.8f);
                case 5: return 2.6f * Sim.SmoothStep(P(x, z, .03f, 5.9f), .45f, .85f) - 1.4f * Sim.SmoothStep(Ridged(x, z, .018f, 3.7f), .88f, .97f) + .4f * S(x, z, .12f, 6.2f);
                case 6: return -.3f + 1.2f * Sim.SmoothStep(P(x, z, .04f, 8.4f), .62f, .85f);
                case 7: return LabTerrace(x, z);
            }
            return 0;
        }
        /// unflattened relief with zone blending at borders (used by Build and the map)
        public static float Relief(float x, float z)
        {
            int self = ZoneWorld.Sample(x, z, out int other, out float d);
            if (self < 0) { self = Mathf.Max(0, other); other = -1; }
            float r = ZoneRelief(self, x, z);
            if (other >= 0 && d < 26) r = Mathf.Lerp(r, ZoneRelief(other, x, z), .5f * (1 - Sim.SmoothStep(d, 0, 26)));
            return r;
        }
        /// raw water depth by zone (before flattening masks)
        public static float WaterRaw(float x, float z, out byte type)
        {
            type = WATER_NONE; int zone = ZoneWorld.At(x, z);
            float n, d = 0;
            switch (zone)
            {
                case 6:
                    n = P(x, z, .035f, 3.3f) + .3f * P(x, z, .12f, 8.2f);
                    d = .35f * Sim.SmoothStep(n, .62f, .7f); type = WATER_MIRE;
                    float deep = P(x, z, .018f, 5.7f) + .2f * P(x, z, .07f, 1.4f);
                    if (deep > .74f) { d = Mathf.Max(d, .7f * Sim.SmoothStep(deep, .74f, .8f)); if (deep > .76f) type = WATER_TOXIC; }
                    break;
                case 1: n = P(x, z, .08f, 2.2f); d = .2f * Sim.SmoothStep(n, .77f, .8f); type = WATER_CLEAN; break;
                case 0: n = P(x, z, .07f, 6.1f); d = .18f * Sim.SmoothStep(n, .8f, .83f); type = WATER_CLEAN; break;
                case 4: n = P(x, z, .05f, 4.4f); d = .25f * Sim.SmoothStep(n, .75f, .78f); type = WATER_OIL; break;
                case 5: n = P(x, z, .06f, 9.6f); d = .25f * Sim.SmoothStep(n, .76f, .79f); type = WATER_MIRE; break;
            }
            if (d < .02f) type = WATER_NONE;
            return d;
        }
    }
}
