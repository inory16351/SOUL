using UnityEngine;

namespace Soul
{
    // Illustrated world map (user 2026-11-06: "the map looks like vector art; per-terrain images and landmark
    // positions"). Every pixel takes its zone's painted swatch from UI/map_terrain.png (3 x 3: 8 zones +
    // mountains, tiled at world scale) and adds relief shading, ink coastlines, dashed trails and water.
    // Built progressively (rows per frame) the first time the map opens; falls back to ZoneWorld.MapTexture.
    // UI/map_world.png (user 2026-11-07), when present, replaces all of that: one illustrated sheet painted over the exact
    // layout export (unity/tools/map_base.cs -> Captures/map_base.png, same MapUV framing, image top = MinZ) and re-inked
    // with the layout masks (tools/map_compose.py) so ridges, water and trails sit on the real terrain.
    // Landmark / pod / shrine / boss pictograms come from UI/map_icons.png (4 x 4, see Icon* constants).
    public static class MapPainter
    {
        public const int W = 640;
        public static int H => Mathf.RoundToInt(W * (ZoneWorld.MaxZ - ZoneWorld.MinZ) / (ZoneWorld.MaxX - ZoneWorld.MinX));
        static Texture2D tex, swatch, icons, world; static Color[] px; static int row = -1; static bool tried;
        const float TILE = 280;   // metres per swatch repeat

        // map icon sheet indices (UI/map_icons.png, row-major from the top-left)
        public const int I_RADIO = 0, I_TOWER = 1, I_CROWN = 2, I_CRANE = 3, I_STACKS = 4, I_TREE = 5, I_TANKER = 6, I_DOME = 7,
            I_BONES = 8, I_BOSS = 9, I_POD = 10, I_SHRINE = 11, I_CHASM = 12, I_DOOR = 13, I_RUIN = 14, I_MARK = 15;
        public static int LandmarkIcon(string id)
        {
            if (id.Contains("Radio")) return I_RADIO; if (id.Contains("Crown")) return I_CROWN; if (id.Contains("Tower") || id.Contains("Office")) return I_TOWER;
            if (id.Contains("Crane")) return I_CRANE; if (id.Contains("Stacks")) return I_STACKS; if (id.Contains("Tree")) return I_TREE;
            if (id.Contains("Tanker")) return I_TANKER; if (id.Contains("Dome")) return I_DOME; if (id.Contains("Giant") || id.Contains("Bone")) return I_BONES;
            return I_RUIN;
        }

        static bool Load()
        {
            if (tried) return swatch; tried = true;
            world = Resources.Load<Texture2D>("UI/map_world");
            swatch = Resources.Load<Texture2D>("UI/map_terrain"); icons = Resources.Load<Texture2D>("UI/map_icons");
            return world || swatch && swatch.isReadable;
        }
        public static bool HasIcons { get { Load(); return icons; } }
        public static bool Icon(Rect r, int i)
        {
            Load(); if (!icons || i < 0 || i > 15) return false;
            int c = i % 4, rr = i / 4; float s = .25f, inset = .05f;
            GUI.DrawTextureWithTexCoords(r, icons, new Rect(c * s + inset * s, 1 - (rr + 1) * s + inset * s, s * (1 - 2 * inset), s * (1 - 2 * inset)), true);
            return true;
        }

        static Color Swatch(int cell, float x, float z)
        {
            float u = Mathf.Repeat(x / TILE, 1), v = Mathf.Repeat(z / TILE, 1);
            int c = cell % 3, r = cell / 3; const float s = 1f / 3, m = .02f;
            return swatch.GetPixelBilinear(c * s + (m + u * (1 - 2 * m)) * s, 1 - (r + 1) * s + (m + v * (1 - 2 * m)) * s);
        }
        static Color PaintPixel(float x, float z)
        {
            int zone = ZoneWorld.Sample(x, z, out int other, out float d);
            Color c;
            if (zone < 0)
            {   // mountains / massifs: rock swatch, darker the deeper into the range
                c = Swatch(8, x, z) * (1 - .3f * Sim.SmoothStep(d, 10, 120));
                if (d < 4) c = Color.Lerp(c, new Color(.12f, .1f, .09f), .7f);              // ink coastline
            }
            else
            {
                c = Swatch(Mathf.Clamp(zone, 0, 7), x, z);
                float rid = ZoneWorld.RidgeHeight(x, z);
                if (rid > .9f) c = Color.Lerp(c, Swatch(8, x, z), Mathf.Clamp01(rid / 8) * .85f);
                if (other >= 0 && d < 2.5f) c = Color.Lerp(c, new Color(.15f, .12f, .1f), .55f);   // zone border ink
                float sh = (Terrain.Relief(x - 10, z - 10) - Terrain.Relief(x + 10, z + 10)) * .05f; c *= 1 + Mathf.Clamp(sh, -.22f, .22f);
                if (Terrain.WaterRaw(x, z, out byte wt) > .1f)
                    c = Color.Lerp(c, wt == Terrain.WATER_TOXIC ? new Color(.55f, .85f, .25f) : wt == Terrain.WATER_OIL ? new Color(.06f, .06f, .07f) : new Color(.28f, .36f, .36f), .65f);
                float td = ZoneRoutes.Dist(x, z);
                if (td < 3.2f && Mathf.Repeat((x + z) * .09f, 1) < .62f) c = Color.Lerp(c, new Color(.17f, .13f, .1f), .6f);   // dashed trail
            }
            c.a = 1; return c;
        }

        /// the painted map (null until the first rows exist); call every GUI frame while the map is open
        public static Texture2D Texture()
        {
            if (!Load()) return null;
            if (world) return world;
            int h = H;
            if (!tex) { tex = new Texture2D(W, h, TextureFormat.RGB24, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp }; px = new Color[W * h]; row = 0; }
            if (row >= 0 && row < h)
            {
                int end = Mathf.Min(h, row + 24);
                for (int j = row; j < end; j++)
                {
                    float z = ZoneWorld.MaxZ - (j + .5f) / h * (ZoneWorld.MaxZ - ZoneWorld.MinZ);   // texture row 0 = bottom = +Z
                    for (int i = 0; i < W; i++) px[j * W + i] = PaintPixel(ZoneWorld.MinX + (i + .5f) / W * (ZoneWorld.MaxX - ZoneWorld.MinX), z);
                }
                row = end;
                if (row >= h) { tex.SetPixels(px); tex.Apply(); row = h; px = null; }
            }
            return row >= h ? tex : null;
        }
        public static float Progress => world ? 1 : row < 0 ? 0 : row / (float)H;
    }
}
