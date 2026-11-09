using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Ground-plane collision + height field for the open world (sim space).
    //  - Walls: circles / oriented rectangles on XZ, optionally limited to a vertical band
    //    (an upper-floor wall only blocks on its own storey).
    //  - Surfaces: walkable tops (box / ramp / mound). Height(x, z) = highest surface there, else 0.
    // Props register both per chunk. Static so gameplay code (player, enemies) can call it.
    public static class WorldCollision
    {
        public const float STEP = .45f;      // walk-up height
        public const float JUMP = 1.1f;      // F-jump can mount ledges up to this
        /// gate != 0: a part gate (cracked wall, sludge, gap ...) that stops blocking once that part is owned
        public struct Col { public bool box; public Vector2 c; public float r; public Vector2 half; public float cos, sin; public bool ranged; public float y0, y1; public int gate; }
        public struct Surf { public int kind; public Vector2 c, half; public float cos, sin, top, yLow, yHigh, r; public int gate; public bool noGrab; }
        class Chunk { public List<Col> cols; public List<Surf> surfs; }
        static readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
        public static float chunkSize = 32;
        public static bool enabled = true;
        /// walkable rectangle of the zone world (sim XZ); the border ridges sit just inside it
        public static Rect worldRect = new Rect(ZoneWorld.MinX + 6, ZoneWorld.MinZ + 6, ZoneWorld.MaxX - ZoneWorld.MinX - 12, ZoneWorld.MaxZ - ZoneWorld.MinZ - 12);

        public static void Register(Vector2Int key, List<Col> cols, List<Surf> surfs) => chunks[key] = new Chunk { cols = cols, surfs = surfs ?? new List<Surf>() };
        public static void Unregister(Vector2Int key) => chunks.Remove(key);
        public static void Clear() => chunks.Clear();
        // persistent colliders / surfaces that are not streamed (pods, boss arenas, fog gates); always tested
        static readonly Chunk persistent = new Chunk { cols = new List<Col>(), surfs = new List<Surf>() };
        public static void AddPersistent(List<Col> cols, List<Surf> surfs) { if (cols != null) persistent.cols.AddRange(cols); if (surfs != null) persistent.surfs.AddRange(surfs); }
        public static void RemovePersistent(List<Col> cols) { foreach (var c in cols) persistent.cols.Remove(c); }
        /// neighbourhood i of key k: 0..8 = the 3x3 chunks, 9 = the persistent set
        static bool Near(Vector2Int k, int i, out Chunk ch)
        {
            if (i == 9) { ch = persistent; return true; }
            return chunks.TryGetValue(new Vector2Int(k.x + i / 3 - 1, k.y + i % 3 - 1), out ch);
        }
        public static Vector2Int KeyOf(float x, float z) => new Vector2Int(Mathf.FloorToInt(x / chunkSize), Mathf.FloorToInt(z / chunkSize));

        // sim rotation about +Y: x' = x cos + z sin, z' = -x sin + z cos
        static Vector2 Rot(Vector2 l, float cy, float sy) => new Vector2(l.x * cy + l.y * sy, -l.x * sy + l.y * cy);
        public static Col ToWorld(PropCollider pc, Vector3 pos, float yaw, float scale)
        {
            float cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
            var c = new Vector2(pos.x, pos.z) + Rot(pc.center * scale, cy, sy);
            var col = pc.box ? new Col { box = true, c = c, half = pc.halfSize * scale, cos = Mathf.Cos(yaw + pc.yaw), sin = Mathf.Sin(yaw + pc.yaw) } : new Col { c = c, r = pc.radius * scale };
            col.gate = pc.gate;
            if (pc.ranged) { col.ranged = true; col.y0 = pos.y + pc.y0 * scale; col.y1 = pos.y + pc.y1 * scale; }
            return col;
        }
        public static Surf ToWorld(PropSurface ps, Vector3 pos, float yaw, float scale)
        {
            float cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
            var c = new Vector2(pos.x, pos.z) + Rot(ps.center * scale, cy, sy);
            return new Surf { kind = ps.kind, c = c, half = ps.halfSize * scale, cos = Mathf.Cos(yaw + ps.yaw), sin = Mathf.Sin(yaw + ps.yaw),
                top = pos.y + ps.top * scale, yLow = pos.y + ps.yLow * scale, yHigh = pos.y + ps.yHigh * scale, r = ps.radius * scale, gate = ps.gate, noGrab = ps.noGrab };
        }

        // ---------- height field ----------
        /// height of the walkable surface at (x, z); `below` limits to surfaces not higher than it
        /// (so a character under a bridge keeps walking on the ground). 0 = base ground.
        public static float Height(float x, float z, float below = float.PositiveInfinity) => Height(x, z, below, out _);
        public static float Height(float x, float z, float below, out int gate)
        {
            float best = Terrain.Height(x, z); gate = 0;
            var k = KeyOf(x, z);
            for (int ni = 0; ni < 10; ni++)
            {
                if (!Near(k, ni, out var ch)) continue;
                foreach (var s in ch.surfs)
                {
                    float h = SurfHeight(s, x, z);
                    if (h > best && h <= below) { best = h; gate = s.gate; }
                }
            }
            return best;
        }
        /// highest surface at (x, z) not above `below`, and whether rope edge-grabs may use it
        public static float HeightGrab(float x, float z, float below, out bool grabbable)
        {
            float best = Terrain.Height(x, z); grabbable = false;   // bare terrain is never a rope edge
            var k = KeyOf(x, z);
            for (int ni = 0; ni < 10; ni++)
            {
                if (!Near(k, ni, out var ch)) continue;
                foreach (var s in ch.surfs) { float h = SurfHeight(s, x, z); if (h > best && h <= below) { best = h; grabbable = !s.noGrab; } }
            }
            return best;
        }
        static float SurfHeight(in Surf s, float x, float z)
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
        /// a surface gated by a part nobody holds can't be stepped onto (it acts as a ledge);
        /// once you stand on it (fell or spawned there) it holds you like any floor
        static bool Locked(in Surf s, float h, float footY) => s.gate != 0 && h > footY + .05f && !Parts.Has(s.gate);

        /// ground under a character standing at height footY: highest surface reachable without
        /// climbing more than `climb` (anything higher is a ledge above, not the floor)
        public static float Floor(float x, float z, float footY, float climb = STEP)
        {
            float best = Terrain.Height(x, z), below = footY + climb;
            var k = KeyOf(x, z);
            for (int ni = 0; ni < 10; ni++)
            {
                if (!Near(k, ni, out var ch)) continue;
                foreach (var s in ch.surfs) { float h = SurfHeight(s, x, z); if (h > best && h <= below && !Locked(s, h, footY)) best = h; }
            }
            return best;
        }

        // ---------- walls ----------
        /// push the disc (p, radius) out of every wall active at foot height p.y; returns true if moved
        public static bool Resolve(ref Vector3 p, float radius)
        {
            bool moved = false;
            if (!enabled) return false;
            for (int pass = 0; pass < 2; pass++)
            {
                var k = KeyOf(p.x, p.z);
                for (int ni = 0; ni < 10; ni++)
                {
                    if (!Near(k, ni, out var ch)) continue;
                    foreach (var col in ch.cols)
                    {
                        if (col.ranged && (p.y + STEP >= col.y1 || p.y < col.y0 - .25f)) continue;
                        if (col.gate != 0 && Parts.Has(col.gate)) continue;
                        moved |= Push(ref p, radius, col);
                    }
                }
            }
            if (!worldRect.Contains(new Vector2(p.x, p.z))) { p.x = Mathf.Clamp(p.x, worldRect.xMin, worldRect.xMax); p.z = Mathf.Clamp(p.z, worldRect.yMin, worldRect.yMax); moved = true; }
            return moved;
        }
        static bool Push(ref Vector3 p, float radius, Col col)
        {
            if (!col.box)
            {
                float dx = p.x - col.c.x, dz = p.z - col.c.y, d2 = dx * dx + dz * dz, min = col.r + radius;
                if (d2 >= min * min) return false;
                float d = Mathf.Sqrt(d2); if (d < 1e-4f) { dx = 1; dz = 0; d = 1; }
                p.x = col.c.x + dx / d * min; p.z = col.c.y + dz / d * min; return true;
            }
            float wx = p.x - col.c.x, wz = p.z - col.c.y;
            float lx = wx * col.cos - wz * col.sin, lz = wx * col.sin + wz * col.cos;
            float qx = Mathf.Clamp(lx, -col.half.x, col.half.x), qz = Mathf.Clamp(lz, -col.half.y, col.half.y);
            float ex = lx - qx, ez = lz - qz, e2 = ex * ex + ez * ez;
            if (e2 >= radius * radius) return false;
            float nx, nz;
            if (e2 > 1e-8f) { float e = Mathf.Sqrt(e2); nx = qx + ex / e * radius; nz = qz + ez / e * radius; }
            else
            {
                float px = col.half.x - Mathf.Abs(lx), pz = col.half.y - Mathf.Abs(lz);
                if (px < pz) { nx = Mathf.Sign(lx == 0 ? 1 : lx) * (col.half.x + radius); nz = lz; }
                else { nz = Mathf.Sign(lz == 0 ? 1 : lz) * (col.half.y + radius); nx = lx; }
            }
            p.x = col.c.x + nx * col.cos + nz * col.sin; p.z = col.c.y - nx * col.sin + nz * col.cos;
            return true;
        }

        /// Horizontal move from `from` to `to` for a character with feet at from.y that may climb
        /// `climb` metres: walls are resolved, then ledges higher than the climb are treated as walls
        /// (axis sliding). Returns the allowed position (y unchanged).
        public const float HEADROOM = 2.0f;   // surfaces higher than this above the feet are ceilings, not ledges
        /// a ledge the character cannot climb at (x, z): a surface between footY + climb and footY + HEADROOM
        /// (or a part-gated surface above the feet while the part is missing)
        public static bool Ledge(float x, float z, float footY, float climb)
        {
            float head = footY + HEADROOM;
            var k = KeyOf(x, z);
            for (int ni = 0; ni < 10; ni++)
            {
                if (!Near(k, ni, out var ch)) continue;
                foreach (var s in ch.surfs)
                {
                    float h = SurfHeight(s, x, z);
                    if (h <= head && (h > footY + climb + 1e-3f || Locked(s, h, footY))) return true;
                }
            }
            return false;
        }
        /// steepest walkable terrain grade (rise / run ≈ 45°); steeper terrain is a wall going up
        public const float SLOPE = 1f, SLOPE_PROBE = .5f;
        /// steep bare terrain ahead of (x, z) in direction (dx, dz) (unit) that rises above the feet: cliffs and
        /// over-steep slopes block walking up (players and enemies); walking down is a drop. A jump arc whose
        /// clearance (`climb` above STEP) tops the face mounts it (2026-11-09: double jump onto low shelves)
        public static bool TerrainWall(float x, float z, float dx, float dz, float footY, float climb = 0)
        {
            if (!Terrain.enabled || (dx == 0 && dz == 0)) return false;
            float ax = x + dx * SLOPE_PROBE, az = z + dz * SLOPE_PROBE, ha = Terrain.Height(ax, az);
            if (ha <= footY + .05f) return false;
            if (climb > STEP + .01f)
            {   // jumping: the face may be entered only if the arc clears its top (sampled 0.5..3 m ahead), so a jump
                // never stops halfway up a cliff
                float top = ha; for (float r = 1; r <= 3; r += .5f) top = Mathf.Max(top, Terrain.Height(x + dx * r, z + dz * r));
                if (top <= footY + climb) return false;
            }
            // the face's full gradient (not the rise along the move): sliding or zig-zagging can't climb it
            const float E = .35f;
            float gx = (Terrain.Height(ax + E, az) - Terrain.Height(ax - E, az)) / (2 * E), gz = (Terrain.Height(ax, az + E) - Terrain.Height(ax, az - E)) / (2 * E);
            float g = Mathf.Sqrt(gx * gx + gz * gz);
            return g > SLOPE && (dx * gx + dz * gz) > .2f * g && ha - Terrain.Height(x, z) > .02f;
        }
        /// bare terrain under (x, z) steeper than SLOPE: unit downhill direction (characters left on a cliff face slide off)
        public static bool SteepAt(float x, float z, out Vector2 down)
        {
            down = Vector2.zero; if (!Terrain.enabled) return false;
            const float E = .35f;
            float gx = (Terrain.Height(x + E, z) - Terrain.Height(x - E, z)) / (2 * E), gz = (Terrain.Height(x, z + E) - Terrain.Height(x, z - E)) / (2 * E);
            float g = Mathf.Sqrt(gx * gx + gz * gz); if (g <= SLOPE * 1.15f) return false;
            down = new Vector2(-gx, -gz) / g; return true;
        }
        public static Vector3 Move(Vector3 from, Vector3 to, float radius, float climb)
        {
            var p = to; p.y = from.y; Resolve(ref p, radius);
            var d = new Vector2(p.x - from.x, p.z - from.z); float len = d.magnitude;
            Vector2 dir = len > 1e-5f ? d / len : Vector2.zero, lead = dir * radius * .7f;   // probe slightly ahead of the centre
            bool Blocked(Vector3 q) => Ledge(q.x, q.z, from.y, climb) || (len > 1e-5f && (Ledge(q.x + lead.x, q.z + lead.y, from.y, climb) || TerrainWall(q.x, q.z, dir.x, dir.y, from.y, climb)));
            if (!Blocked(p)) return p;
            // blocked by a ledge / cliff: try sliding along each axis
            float sx = Mathf.Sign(p.x - from.x), sz = Mathf.Sign(p.z - from.z);
            var a = new Vector3(p.x, from.y, from.z); if (!Ledge(a.x, a.z, from.y, climb) && !TerrainWall(a.x, a.z, sx, 0, from.y, climb)) { Resolve(ref a, radius); return a; }
            var b = new Vector3(from.x, from.y, p.z); if (!Ledge(b.x, b.z, from.y, climb) && !TerrainWall(b.x, b.z, 0, sz, from.y, climb)) { Resolve(ref b, radius); return b; }
            return new Vector3(from.x, from.y, from.z);
        }
    }
}
