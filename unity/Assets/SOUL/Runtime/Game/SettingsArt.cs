using UnityEngine;

namespace Soul
{
    // Esc settings window art (SoulGame.Settings): Resources/UI/ui_panel.png (ChatGPT, ui-art session; rects from
    // Resources/UI/ui_atlas.md §3, image 1536 x 1024, top-left pixel coords) when present, else the HUD kit
    // (UI/ui_kit.png panel frame), else flat IMGUI in the HUD colours. Pieces carry no text; the game draws labels.
    // Every piece is drawn as a 9-slice with pixel borders (L, R, T, B) scaled to the target height.
    public static class SettingsArt
    {
        public struct Piece
        {
            public RectInt px; public Vector4 border;   // px: top-left image coords as in ui_atlas.md; border L, R, T, B (px)
            public Piece(int x, int y, int w, int h, float l, float r, float t, float b) { px = new RectInt(x, y, w, h); border = new Vector4(l, r, t, b); }
            public bool Valid => px.width > 0 && px.height > 0;
        }
        public static Piece PanelP = new Piece(21, 96, 893, 676, 120, 120, 120, 120),
            ButtonP = new Piece(938, 109, 571, 147, 60, 60, 45, 45), ButtonOnP = new Piece(937, 272, 574, 149, 60, 60, 45, 45), ButtonOffP = new Piece(939, 437, 572, 146, 60, 60, 45, 45),
            TabP = new Piece(950, 639, 275, 96, 60, 60, 30, 20), TabOnP = new Piece(1229, 639, 279, 96, 80, 80, 30, 20),
            TrackP = new Piece(35, 844, 569, 82, 60, 60, 0, 0), KnobP = new Piece(629, 831, 105, 105, 0, 0, 0, 0),
            KeycapP = new Piece(796, 818, 129, 132, 35, 35, 35, 35), KeycapOnP = new Piece(947, 819, 131, 131, 35, 35, 35, 35),
            KeycapWideP = new Piece(1100, 819, 261, 131, 35, 35, 35, 35), KeycapSmallP = new Piece(1383, 821, 122, 127, 30, 30, 30, 30);
        /// slider groove inside TrackP (image px from the piece edges): x 60 .. 515, y 24 .. 56. Its fill is painted in, so we cover it.
        const float GROOVE_L = 60, GROOVE_R = 54, GROOVE_T = 24, GROOVE_B = 26;
        /// on-screen size of the panel frame relative to the art
        public static float PanelScale = .36f;
        const float SRC_W = 1536, SRC_H = 1024;   // atlas image size (UVs stay right if the importer rescales)

        static Texture2D sheet; static bool loaded;
        static void Load() { if (loaded) return; loaded = true; sheet = Resources.Load<Texture2D>("UI/ui_panel"); }
        public static bool HasSheet { get { Load(); return sheet; } }
        static bool Has(Piece p) { Load(); return sheet && p.Valid; }
        static Rect UV(RectInt p) => new Rect(p.x / SRC_W, 1 - (p.y + p.height) / SRC_H, p.width / SRC_W, p.height / SRC_H);
        /// 9-slice: screen borders = image borders * scale (clamped to half the target)
        static void Nine(Rect r, Piece p, float scale)
        {
            var uv = UV(p.px); float uPx = uv.width / p.px.width, vPx = uv.height / p.px.height;
            float l = Mathf.Min(p.border.x * scale, r.width / 2), rr = Mathf.Min(p.border.y * scale, r.width / 2), t = Mathf.Min(p.border.z * scale, r.height / 2), b = Mathf.Min(p.border.w * scale, r.height / 2);
            float[] xs = { r.x, r.x + l, r.xMax - rr, r.xMax }, ys = { r.y, r.y + t, r.yMax - b, r.yMax };
            float[] us = { uv.x, uv.x + p.border.x * uPx, uv.xMax - p.border.y * uPx, uv.xMax }, vs = { uv.yMax, uv.yMax - p.border.z * vPx, uv.y + p.border.w * vPx, uv.y };   // screen y down, uv y up
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
            {
                if (xs[i + 1] - xs[i] < .01f || ys[j + 1] - ys[j] < .01f) continue;
                GUI.DrawTextureWithTexCoords(new Rect(xs[i], ys[j], xs[i + 1] - xs[i], ys[j + 1] - ys[j]), sheet, new Rect(us[i], vs[j + 1], us[i + 1] - us[i], vs[j] - vs[j + 1]), true);
            }
        }
        /// fit the piece's height to r (borders scale with it)
        static void Fit(Rect r, Piece p) => Nine(r, p, r.height / p.px.height);
        static void Fill(Rect r, Color c, Texture2D white) { var o = GUI.color; GUI.color = c * o; GUI.DrawTexture(r, white); GUI.color = o; }
        static void Frame(Rect r, Color c, Texture2D white) { Fill(new Rect(r.x, r.y, r.width, 1), c, white); Fill(new Rect(r.x, r.yMax - 1, r.width, 1), c, white); Fill(new Rect(r.x, r.y, 1, r.height), c, white); Fill(new Rect(r.xMax - 1, r.y, 1, r.height), c, white); }

        public static void Panel(Rect r)
        {
            if (Has(PanelP)) { float m = 14; Nine(new Rect(r.x - m, r.y - m, r.width + 2 * m, r.height + 2 * m), PanelP, PanelScale); return; }
            var o = GUI.color; GUI.color = new Color(.02f, .04f, .06f, .92f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = o;
            if (!UiArt.Nine(new Rect(r.x - 14, r.y - 14, r.width + 28, r.height + 28), UiArt.Panel, .13f, 46)) Frame(r, new Color(.35f, .7f, .85f, .7f), Texture2D.whiteTexture);
        }
        public static bool Button(Rect r, string text, GUIStyle style, Texture2D white, bool on = false)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            if (Has(ButtonP)) Fit(r, on || (hover && GUI.enabled) ? ButtonOnP : GUI.enabled ? ButtonP : ButtonOffP);
            else
            {
                Fill(r, on ? new Color(.12f, .38f, .48f, .9f) : hover ? new Color(.1f, .2f, .26f, .9f) : new Color(.05f, .1f, .14f, .88f), white);
                Frame(r, on ? new Color(.45f, .9f, 1f, .9f) : new Color(.3f, .55f, .65f, .6f), white);
            }
            return GUI.Button(r, text, style);
        }
        public static bool Tab(Rect r, string text, bool on, GUIStyle style, Texture2D white)
        {
            if (Has(TabP)) Fit(r, on ? TabOnP : TabP);
            else
            {
                Fill(r, on ? new Color(.1f, .3f, .38f, .92f) : new Color(.04f, .08f, .11f, .8f), white);
                Fill(new Rect(r.x, r.yMax - 3, r.width, 3), on ? new Color(.45f, .9f, 1f) : new Color(.25f, .4f, .48f, .6f), white);
            }
            return GUI.Button(r, text, new GUIStyle(style) { fontStyle = on ? FontStyle.Bold : FontStyle.Normal });
        }
        public static bool Keycap(Rect r, string text, bool on, GUIStyle style, Texture2D white)
        {
            if (Has(KeycapP)) Fit(r, on ? KeycapOnP : r.width > r.height * 1.6f ? KeycapWideP : KeycapP);
            else
            {
                Fill(r, on ? new Color(.12f, .38f, .48f, .95f) : new Color(.08f, .12f, .15f, .95f), white);
                Fill(new Rect(r.x, r.yMax - 3, r.width, 3), new Color(0, 0, 0, .5f), white);
                Frame(r, on ? new Color(.45f, .9f, 1f) : new Color(.45f, .6f, .68f, .8f), white);
            }
            return GUI.Button(r, text, new GUIStyle(style) { fontStyle = FontStyle.Bold });
        }
        static GUIStyle hidden, hiddenThumb;
        /// track + fill + knob; an invisible IMGUI slider on top does the dragging
        public static float Slider(Rect r, float v, float min, float max, Texture2D white)
        {
            float k = Mathf.InverseLerp(min, max, v);
            if (Has(TrackP))
            {
                var tr = new Rect(r.x - 6, r.y - 4, r.width + 12, r.height + 8); float s = tr.height / TrackP.px.height;
                Nine(tr, TrackP, s);
                var bar = new Rect(tr.x + GROOVE_L * s, tr.y + GROOVE_T * s, tr.width - (GROOVE_L + GROOVE_R) * s, tr.height - (GROOVE_T + GROOVE_B) * s);
                Fill(bar, new Color(.02f, .03f, .04f, 1), white);                                          // hide the painted-in fill
                Fill(new Rect(bar.x, bar.y + 1, bar.width * k, bar.height - 2), new Color(.25f, .75f, 1f, .95f), white);
                float ks = r.height + 10; var knob = new Rect(bar.x + bar.width * k - ks / 2, r.y + r.height / 2 - ks / 2, ks, ks);
                GUI.DrawTextureWithTexCoords(knob, sheet, UV(KnobP.px), true);
            }
            else
            {
                var bar = new Rect(r.x, r.y + r.height / 2 - 4, r.width, 8);
                Fill(bar, new Color(.03f, .06f, .08f, .95f), white); Frame(bar, new Color(.3f, .5f, .6f, .6f), white);
                Fill(new Rect(bar.x + 1, bar.y + 1, (bar.width - 2) * k, bar.height - 2), new Color(.25f, .75f, 1f, .95f), white);
                var knob = new Rect(r.x + r.width * k - 9, r.y + r.height / 2 - 11, 18, 22);
                Fill(knob, new Color(.75f, .92f, 1f), white); Frame(knob, new Color(.1f, .25f, .32f), white);
            }
            if (hidden == null) { hidden = new GUIStyle(); hiddenThumb = new GUIStyle { fixedWidth = 18, fixedHeight = 22 }; }
            return GUI.HorizontalSlider(new Rect(r.x, r.y - 2, r.width, r.height + 4), v, min, max, hidden, hiddenThumb);
        }
    }
}
