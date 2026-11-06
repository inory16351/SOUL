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
        public struct Col { public bool box; public Vector2 c; public float r; public Vector2 half; public float cos, sin; public bool ranged; public float y0, y1; }
        public struct Surf { public int kind; public Vector2 c, half; public float cos, sin, top, yLow, yHigh, r; public int gate; }
        class Chunk { public List<Col> cols; public List<Surf> surfs; }
        static readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
        public static float chunkSize = 32;
        public static bool enabled = true;
        public static float worldRadius = Regions.WORLD_RADIUS;

        public static void Register(Vector2Int key, List<Col> cols, List<Surf> surfs) => chunks[key] = new Chunk { cols = cols, surfs = surfs ?? new List<Surf>() };
        public static void Unregister(Vector2Int key) => chunks.Remove(key);
        public static void Clear() => chunks.Clear();
        public static Vector2Int KeyOf(float x, float z) => new Vector2Int(Mathf.FloorToInt(x / chunkSize), Mathf.FloorToInt(z / chunkSize));

        // sim rotation about +Y: x' = x cos + z sin, z' = -x sin + z cos
        static Vector2 Rot(Vector2 l, float cy, float sy) => new Vector2(l.x * cy + l.y * sy, -l.x * sy + l.y * cy);
        public static Col ToWorld(PropCollider pc, Vector3 pos, float yaw, float scale)
        {
            float cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
            var c = new Vector2(pos.x, pos.z) + Rot(pc.center * scale, cy, sy);
            var col = pc.box ? new Col { box = true, c = c, half = pc.halfSize * scale, cos = Mathf.Cos(yaw + pc.yaw), sin = Mathf.Sin(yaw + pc.yaw) } : new Col { c = c, r = pc.radius * scale };
            if (pc.ranged) { col.ranged = true; col.y0 = pos.y + pc.y0 * scale; col.y1 = pos.y + pc.y1 * scale; }
            return col;
        }
        public static Surf ToWorld(PropSurface ps, Vector3 pos, float yaw, float scale)
        {
            float cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
            var c = new Vector2(pos.x, pos.z) + Rot(ps.center * scale, cy, sy);
            return new Surf { kind = ps.kind, c = c, half = ps.halfSize * scale, cos = Mathf.Cos(yaw + ps.yaw), sin = Mathf.Sin(yaw + ps.yaw),
                top = pos.y + ps.top * scale, yLow = pos.y + ps.yLow * scale, yHigh = pos.y + ps.yHigh * scale, r = ps.radius * scale, gate = ps.gate };
        }

        // ---------- height field ----------
        /// height of the walkable surface at (x, z); `below` limits to surfaces not higher than it
        /// (so a character under a bridge keeps walking on the ground). 0 = base ground.
        public static float Height(float x, float z, float below = float.PositiveInfinity) => Height(x, z, below, out _);
        public static float Height(float x, float z, float below, out int gate)
        {
            float best = 0; gate = 0;
            var k = KeyOf(x, z);
            for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
            {
                if (!chunks.TryGetValue(new Vector2Int(k.x + dx, k.y + dz), out var ch)) continue;
                foreach (var s in ch.surfs)
                {
                    float h = SurfHeight(s, x, z);
                    if (h > best && h <= below) { best = h; gate = s.gate; }
                }
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
        /// ground under a character standing at height footY: highest surface reachable without
        /// climbing more than `climb` (anything higher is a ledge above, not the floor)
        public static float Floor(float x, float z, float footY, float climb = STEP) => Height(x, z, footY + climb);

        // ---------- walls ----------
        /// push the disc (p, radius) out of every wall active at foot height p.y; returns true if moved
        public static bool Resolve(ref Vector3 p, float radius)
        {
            bool moved = false;
            if (!enabled) return false;
            for (int pass = 0; pass < 2; pass++)
            {
                var k = KeyOf(p.x, p.z);
                for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
                {
                    if (!chunks.TryGetValue(new Vector2Int(k.x + dx, k.y + dz), out var ch)) continue;
                    foreach (var col in ch.cols)
                    {
                        if (col.ranged && (p.y + STEP >= col.y1 || p.y < col.y0 - .25f)) continue;
                        moved |= Push(ref p, radius, col);
                    }
                }
            }
            float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
            if (r > worldRadius) { p.x *= worldRadius / r; p.z *= worldRadius / r; moved = true; }
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
        public static bool Ledge(float x, float z, float footY, float climb) => Height(x, z, footY + HEADROOM) > footY + climb + 1e-3f;
        public static Vector3 Move(Vector3 from, Vector3 to, float radius, float climb)
        {
            var p = to; p.y = from.y; Resolve(ref p, radius);
            var d = new Vector2(p.x - from.x, p.z - from.z); float len = d.magnitude;
            Vector2 lead = len > 1e-5f ? d / len * radius * .7f : Vector2.zero;   // probe slightly ahead of the centre
            bool Blocked(Vector3 q) => Ledge(q.x, q.z, from.y, climb) || (len > 1e-5f && Ledge(q.x + lead.x, q.z + lead.y, from.y, climb));
            if (!Blocked(p)) return p;
            // blocked by a ledge: try sliding along each axis
            var a = new Vector3(p.x, from.y, from.z); if (!Ledge(a.x, a.z, from.y, climb)) { Resolve(ref a, radius); return a; }
            var b = new Vector3(from.x, from.y, p.z); if (!Ledge(b.x, b.z, from.y, climb)) { Resolve(ref b, radius); return b; }
            return new Vector3(from.x, from.y, from.z);
        }
    }
}
