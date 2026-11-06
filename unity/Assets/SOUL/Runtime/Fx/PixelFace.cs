using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Port of neon.js createFace: a black LED screen in the hood opening. Expressions are lit on
    // a coarse 24 x 16 pixel grid (point-filtered, dim unlit pixels visible) so they read from
    // the top-down camera. Cute "0_0" ring eyes, thin mouth, squint "> <" when hurt.
    public class PixelFace
    {
        static readonly Dictionary<string, string[]> EYES = new Dictionary<string, string[]>
        {
            { "idle",  new[] { ".####.", "##..##", "#....#", "#....#", "#....#", "##..##", ".####." } },
            { "blink", new[] { "......", "......", "......", "######", "......", "......", "......" } },
            { "happy", new[] { "......", "......", ".####.", "##..##", "#....#", "......", "......" } },
            { "focus", new[] { "######", "#....#", "#....#", "##..##", ".####.", "......", "......" } },
            { "hurt",  new[] { "......", "##....", ".###..", "...###", ".###..", "##....", "......" } },
            { "wide",  new[] { ".####.", "##..##", "#.##.#", "#.##.#", "#....#", "##..##", ".####." } },
        };
        static readonly HashSet<string> MIRROR = new HashSet<string> { "hurt" };
        static readonly Dictionary<string, string[]> MOUTH = new Dictionary<string, string[]>
        {
            { "idle", new[] { "######" } }, { "blink", new[] { "######" } }, { "focus", new[] { "########" } },
            { "happy", new[] { "#......#", ".######." } }, { "hurt", new[] { "#.#.#.#", ".#.#.#." } }, { "wide", new[] { ".####.", "#....#", ".####." } },
        };
        const int GRID_W = 24, GRID_H = 16, CELL = 10, W = GRID_W * CELL, H = GRID_H * CELL;
        static readonly Color32 BG = new Color32(0x01, 0x02, 0x05, 255), OFF = new Color32(0x08, 0x14, 0x1c, 255), ON = new Color32(0x7f, 0xe3, 0xff, 255), CORE = new Color32(0xe6, 0xfb, 0xff, 255);

        readonly Texture2D tex; readonly Color32[] px = new Color32[W * H];
        readonly Material mat; readonly Light glow;
        string mood = "idle", drawn = ""; float blinkAt, blinkT = -1, t, hold, lookAt = 3; int look;

        public PixelFace(CharacterRig ch)
        {
            var screen = ch.model.FindDeep("pixel-face-screen");
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "pixel-face" };
            if (screen)
            {
                var r = screen.GetComponent<MeshRenderer>();
                if (r) { mat = r.material; mat.SetTexture(Mats.MainTex, tex); }
                if (screen.parent) foreach (Transform s in screen.parent) { var l = s.GetComponent<Light>(); if (l && !glow) glow = l; }
            }
            else Debug.LogWarning("[SOUL] PixelFace: no pixel-face-screen");
            blinkAt = 2 + Random.value * 3;
            Draw("idle", 0);
        }
        void Rect(int x0, int y0, int w, int h, Color32 c)
        {
            // canvas y runs down; the texture's row 0 is the bottom (uv v=0)
            for (int y = y0; y < y0 + h; y++) { int row = (H - 1 - y) * W; for (int x = x0; x < x0 + w; x++) px[row + x] = c; }
        }
        void Draw(string shape, int dx)
        {
            string key = shape + dx; if (key == drawn) return; drawn = key;
            for (int i = 0; i < px.Length; i++) px[i] = BG;
            var lit = new HashSet<int>();
            var bmp = EYES.TryGetValue(shape, out var e) ? e : EYES["idle"];
            var mouth = MOUTH.TryGetValue(shape, out var m) ? m : MOUTH["idle"];
            foreach (var (ox, mirror) in new[] { (4, false), (14, MIRROR.Contains(shape)) })
                for (int y = 0; y < bmp.Length; y++) { var row = bmp[y]; for (int x = 0; x < row.Length; x++) if (row[x] == '#') lit.Add((ox + dx + (mirror ? row.Length - 1 - x : x)) + (y + 2) * 100); }
            for (int y = 0; y < mouth.Length; y++) { var row = mouth[y]; for (int x = 0; x < row.Length; x++) if (row[x] == '#') lit.Add((12 - (row.Length >> 1) + dx + x) + (y + 12) * 100); }
            for (int y = 0; y < GRID_H; y++) for (int x = 0; x < GRID_W; x++)
            {
                bool on = lit.Contains(x + y * 100);
                Rect(x * CELL + 1, y * CELL + 1, CELL - 2, CELL - 2, on ? ON : OFF);
                if (on) Rect(x * CELL + 3, y * CELL + 3, CELL - 6, CELL - 6, CORE);
            }
            tex.SetPixels32(px); tex.Apply(false);
        }
        public void Set(string m, float seconds = 0) { mood = m; hold = seconds; }
        public void Update(float dt, string state)
        {
            t += dt; if (hold > 0) hold -= dt;
            string b = hold > 0 ? mood : state;
            if (b == "idle")
            {
                if (t > blinkAt && blinkT < 0) blinkT = 0;
                if (blinkT >= 0) { blinkT += dt; if (blinkT > .12f) { blinkT = -1; blinkAt = t + 2 + Random.value * 3.5f; } }
                if (t > lookAt) { look = new[] { -1, 0, 0, 1 }[Random.Range(0, 4)]; lookAt = t + 1.5f + Random.value * 2.5f; }   // glance around
            }
            else look = 0;
            Draw(b == "idle" && blinkT >= 0 ? "blink" : b, b == "idle" ? look : 0);
            float pulse = 1 + Mathf.Sin(t * 3) * .05f;
            if (mat) Mats.SetColor(mat, new Color(1.15f * pulse, 1.15f * pulse, 1.15f * pulse, 1));
            if (glow) glow.intensity = .12f * pulse * SoulLights.PointScale;
        }
    }
}
