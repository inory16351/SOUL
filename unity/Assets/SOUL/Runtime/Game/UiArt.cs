using UnityEngine;

namespace Soul
{
    // Generated UI art (ChatGPT, same style chat as the concept deck; saved by the local receiver into
    // Resources/UI). Missing files fall back to the old flat GUI, so the game runs without them.
    //   UI/icons.png      4 x 4 icon sheet (index 0..15, row-major from the top-left):
    //                     0 slash · 1 whirlwind · 2 laser wave · 3 parry · 4 blade storm · 5 dash · 6 jump · 7 rope
    //                     8 jet · 9 cannon · 10 magnet · 11 anti-toxin core · 12 lab keys · 13 pod · 14 shrine · 15 scrap
    //   UI/ui_kit.png     HUD frames (see UiArt.Kit rects)
    //   UI/map_paper.png  map background paper / terminal texture
    public static class UiArt
    {
        static Texture2D icons, kit, paper; static bool loaded;
        public const int SLASH = 0, WHIRL = 1, WAVE = 2, PARRY = 3, STORM = 4, DASH = 5, JUMP = 6, ROPE = 7, JET = 8, CANNON = 9, MAGNET = 10, CORE = 11, KEYS = 12, POD = 13, SHRINE = 14, SCRAP = 15;
        static void Load()
        {
            if (loaded) return; loaded = true;
            icons = Resources.Load<Texture2D>("UI/icons"); kit = Resources.Load<Texture2D>("UI/ui_kit"); paper = Resources.Load<Texture2D>("UI/map_paper");
        }
        public static bool HasIcons { get { Load(); return icons; } }
        public static Texture2D Paper { get { Load(); return paper; } }
        public static Texture2D Kit { get { Load(); return kit; } }
        /// draw icon `i` of the 4 x 4 sheet into r (inset trims the sheet's cell padding)
        public static bool Icon(Rect r, int i, float inset = .06f)
        {
            Load(); if (!icons || i < 0 || i > 15) return false;
            int c = i % 4, row = i / 4; float s = .25f;
            var uv = new Rect(c * s + inset * s, 1 - (row + 1) * s + inset * s, s * (1 - 2 * inset), s * (1 - 2 * inset));
            GUI.DrawTextureWithTexCoords(r, icons, uv, true); return true;
        }
        // UI kit pieces (pixel rects in ui_kit.png, Unity texture space: origin bottom-left), found by alpha components
        public static readonly RectInt HpBar = new RectInt(184, 816, 1148, 188), BossBar = new RectInt(20, 640, 1496, 148),
            Slot = new RectInt(468, 432, 188, 196), Button = new RectInt(1004, 116, 424, 196), Panel = new RectInt(64, 16, 860, 376);
        static Rect UV(RectInt p) { Load(); return new Rect(p.x / (float)kit.width, p.y / (float)kit.height, p.width / (float)kit.width, p.height / (float)kit.height); }
        /// draw a kit piece stretched into r
        public static bool Piece(Rect r, RectInt p) { Load(); if (!kit) return false; GUI.DrawTextureWithTexCoords(r, kit, UV(p), true); return true; }
        /// draw a kit piece as a 9-slice (corners keep their size: border = fraction of the piece kept at each edge)
        public static bool Nine(Rect r, RectInt p, float border, float screenBorder)
        {
            Load(); if (!kit) return false; var uv = UV(p);
            float bu = uv.width * border, bv = uv.height * border * p.width / p.height;
            float[] xs = { r.x, r.x + screenBorder, r.xMax - screenBorder, r.xMax }, ys = { r.y, r.y + screenBorder, r.yMax - screenBorder, r.yMax };
            float[] us = { uv.x, uv.x + bu, uv.xMax - bu, uv.xMax }, vs = { uv.yMax, uv.yMax - bv, uv.y + bv, uv.y };   // screen y down, uv y up
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
                GUI.DrawTextureWithTexCoords(new Rect(xs[i], ys[j], xs[i + 1] - xs[i], ys[j + 1] - ys[j]), kit, new Rect(us[i], vs[j + 1], us[i + 1] - us[i], vs[j] - vs[j + 1]), true);
            return true;
        }
        /// part id -> icon index
        public static int PartIcon(int part) => part == Parts.Rope ? ROPE : part == Parts.Jet ? JET : part == Parts.Cannon ? CANNON : part == Parts.Magnet ? MAGNET : part == Parts.Toxic ? CORE : part == Parts.LabKeys ? KEYS : -1;
    }
}
