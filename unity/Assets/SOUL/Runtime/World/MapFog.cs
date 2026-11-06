using UnityEngine;

namespace Soul
{
    // Fog of war on the world map (user 2026-11-06: "like Don't Starve, only where you've been").
    // A map-resolution mask: walking reveals a circle around the player; taking a zone's core (its boss)
    // reveals that whole zone and unlocks warping between its pods (ZoneState.HasCore).
    // F1 debug shows the map without fog.
    public static class MapFog
    {
        static bool[] seen; static sbyte[] zoneOf; static Texture2D tex; static bool dirty = true;
        static int W => ZoneWorld.MAP_W; static int H => ZoneWorld.MAP_H;
        static void Ensure()
        {
            if (seen != null) return;
            seen = new bool[W * H]; zoneOf = new sbyte[W * H];
            for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
            {
                var p = ZoneWorld.FromMapUV(new Vector2((i + .5f) / W, (j + .5f) / H));
                zoneOf[j * W + i] = (sbyte)ZoneWorld.At(p.x, p.z);
            }
        }
        public static void Reset() { seen = null; dirty = true; }
        /// reveal a circle (metres) around a world point
        public static void Reveal(Vector3 p, float radius)
        {
            Ensure();
            var uv = ZoneWorld.MapUV(p); int ci = (int)(uv.x * W), cj = (int)(uv.y * H);
            float rx = radius / (ZoneWorld.MaxX - ZoneWorld.MinX) * W, rz = radius / (ZoneWorld.MaxZ - ZoneWorld.MinZ) * H;
            int ri = Mathf.CeilToInt(rx), rj = Mathf.CeilToInt(rz);
            for (int di = -ri; di <= ri; di++) for (int dj = -rj; dj <= rj; dj++)
            {
                int i = ci + di, j = cj + dj; if (i < 0 || j < 0 || i >= W || j >= H) continue;
                if ((di * di) / (rx * rx) + (dj * dj) / (rz * rz) > 1) continue;
                int k = j * W + i; if (!seen[k]) { seen[k] = true; dirty = true; }
            }
        }
        /// reveal a whole zone (its core was taken) plus a little of the rim around it
        public static void RevealZone(int zone)
        {
            Ensure();
            for (int k = 0; k < seen.Length; k++) if (zoneOf[k] == zone && !seen[k]) { seen[k] = true; dirty = true; }
        }
        public static bool Seen(Vector3 p)
        {
            Ensure(); var uv = ZoneWorld.MapUV(p);
            int i = Mathf.Clamp((int)(uv.x * W), 0, W - 1), j = Mathf.Clamp((int)(uv.y * H), 0, H - 1);
            return seen[j * W + i];
        }
        public static bool ZoneTouched(int zone) { Ensure(); for (int k = 0; k < seen.Length; k++) if (seen[k] && zoneOf[k] == zone) return true; return false; }
        /// overlay drawn on top of the map texture: opaque dark paper where unexplored, soft edge
        public static Texture2D Texture()
        {
            Ensure();
            if (!tex) { tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp }; dirty = true; }
            if (!dirty) return tex;
            var px = new Color32[W * H]; var fog = new Color32(18, 17, 16, 255);
            for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
            {
                // texture row 0 = bottom of the drawn map (+Z); the mask row 0 = top (-Z)
                int k = j * W + i; byte a;
                if (seen[k]) a = 0;
                else
                {   // soft edge: partly transparent next to explored pixels
                    int n = 0; for (int di = -1; di <= 1; di++) for (int dj = -1; dj <= 1; dj++) { int ii = i + di, jj = j + dj; if (ii >= 0 && jj >= 0 && ii < W && jj < H && seen[jj * W + ii]) n++; }
                    a = (byte)(n > 0 ? 150 : 255);
                }
                px[(H - 1 - j) * W + i] = new Color32(fog.r, fog.g, fog.b, a);
            }
            tex.SetPixels32(px); tex.Apply(); dirty = false;
            return tex;
        }
    }
}
