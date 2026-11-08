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
            { "curious", new[] { ".####.", "##..##", "#..###", "#..###", "#....#", "##..##", ".####." } },
            { "sad",   new[] { "......", "##....", ".##...", "..####", "..#..#", "..####", "......" } },
            { "angry", new[] { "#.....", "##....", ".####.", "##..##", "#....#", "##..##", ".####." } },
            { "sleep", new[] { "......", "......", "......", "#....#", ".####.", "......", "......" } },
            { "blank", new[] { "......", "......", "......", ".####.", ".####.", "......", "......" } },   // 무표정 (원혼)
        };
        static readonly HashSet<string> MIRROR = new HashSet<string> { "hurt", "sad", "angry" };
        static readonly Dictionary<string, string[]> MOUTH = new Dictionary<string, string[]>
        {
            { "idle", new[] { "######" } }, { "blink", new[] { "######" } }, { "focus", new[] { "########" } },
            { "happy", new[] { "#......#", ".######." } }, { "hurt", new[] { "#.#.#.#", ".#.#.#." } }, { "wide", new[] { ".####.", "#....#", ".####." } },
            { "curious", new[] { ".##.", "#..#", ".##." } }, { "sad", new[] { ".######.", "#......#" } },
            { "angry", new[] { "########", "#.#..#.#" } }, { "sleep", new[] { "..##.." } }, { "blank", new[] { "####" } },
        };
        // 점점 늘어나는 표정: only 0_0 and blinking at the start; each purified zone unlocks the next face
        // (기획_결정사항.md order). Locked faces fall back to the idle 0_0.
        public static readonly string[] ORDER = { "hurt", "wide", "focus", "curious", "happy", "sad", "angry", "sleep" };
        public static int unlocked;
        public static bool Unlocked(string m) { if (m == "idle" || m == "blink") return true; int i = System.Array.IndexOf(ORDER, m); return i >= 0 && i < unlocked; }
        // 원혼 파츠 (기획_결정사항.md): equipped grudge parts are permanent and take the face over, in the same
        // steps as the ending routes — 0-1 normal · 2-3 reddish · 4-5 red, expressions break to blank · 6+ red, blank only
        public static int grudge;
        public static int GrudgeStage => grudge <= 1 ? 0 : grudge <= 3 ? 1 : grudge <= 5 ? 2 : 3;
        static readonly Color32 RED_ON = new Color32(0xff, 0x3a, 0x2a, 255), RED_CORE = new Color32(0xff, 0xc8, 0xb8, 255);
        static readonly float[] REDNESS = { 0, .35f, .75f, 1 };
        float glitch, glitchAt = 4; Color glowBase = Color.white;
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
            if (glow) glowBase = glow.color;
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
            int stage = GrudgeStage; string key = shape + dx + "/" + stage; if (key == drawn) return; drawn = key;
            float red = REDNESS[stage]; var on = Color32.Lerp(ON, RED_ON, red); var core = Color32.Lerp(CORE, RED_CORE, red);
            var off = Color32.Lerp(OFF, new Color32(0x1c, 0x08, 0x07, 255), red);
            for (int i = 0; i < px.Length; i++) px[i] = BG;
            var lit = new HashSet<int>();
            var bmp = EYES.TryGetValue(shape, out var e) ? e : EYES["idle"];
            var mouth = MOUTH.TryGetValue(shape, out var m) ? m : MOUTH["idle"];
            foreach (var (ox, mirror) in new[] { (4, false), (14, MIRROR.Contains(shape)) })
                for (int y = 0; y < bmp.Length; y++) { var row = bmp[y]; for (int x = 0; x < row.Length; x++) if (row[x] == '#') lit.Add((ox + dx + (mirror ? row.Length - 1 - x : x)) + (y + 2) * 100); }
            for (int y = 0; y < mouth.Length; y++) { var row = mouth[y]; for (int x = 0; x < row.Length; x++) if (row[x] == '#') lit.Add((12 - (row.Length >> 1) + dx + x) + (y + 12) * 100); }
            for (int y = 0; y < GRID_H; y++) for (int x = 0; x < GRID_W; x++)
            {
                bool lit1 = lit.Contains(x + y * 100);
                Rect(x * CELL + 1, y * CELL + 1, CELL - 2, CELL - 2, lit1 ? on : off);
                if (lit1) Rect(x * CELL + 3, y * CELL + 3, CELL - 6, CELL - 6, core);
            }
            tex.SetPixels32(px); tex.Apply(false);
        }
        public void Set(string m, float seconds = 0) { if (!Unlocked(m)) return; mood = m; hold = seconds; }
        /// show a face regardless of unlocks (debug preview)
        public void Preview(string m, float seconds = 2) { mood = m; hold = seconds; }
        public void Update(float dt, string state)
        {
            t += dt; if (hold > 0) hold -= dt;
            string b = hold > 0 ? mood : state;
            if (hold <= 0 && !Unlocked(b)) b = "idle";
            // grudge takes the face: stage 3 shows only the blank face; stage 2 breaks expressions to blank now and then
            int stage = GrudgeStage;
            if (stage >= 3) b = "blank";
            else if (stage == 2)
            {
                if (t > glitchAt) { glitch = .18f + Random.value * .5f; glitchAt = t + 2.5f + Random.value * 4; }
                if (glitch > 0) { glitch -= dt; b = "blank"; }
            }
            if (b == "idle")
            {
                if (t > blinkAt && blinkT < 0) blinkT = 0;
                if (blinkT >= 0) { blinkT += dt; if (blinkT > .12f) { blinkT = -1; blinkAt = t + 2 + Random.value * 3.5f; } }
                if (t > lookAt) { look = new[] { -1, 0, 0, 1 }[Random.Range(0, 4)]; lookAt = t + 1.5f + Random.value * 2.5f; }   // glance around
            }
            else look = 0;
            Draw(b == "idle" && blinkT >= 0 ? "blink" : b, b == "idle" ? look : 0);
            float pulse = 1 + Mathf.Sin(t * 3) * .05f;
            if (glow) glow.color = Color.Lerp(glowBase, new Color(1, .23f, .16f), REDNESS[stage]);
            if (mat) Mats.SetColor(mat, new Color(1.15f * pulse, 1.15f * pulse, 1.15f * pulse, 1));
            if (glow) glow.intensity = .12f * pulse * SoulLights.PointScale;
        }
    }
}
