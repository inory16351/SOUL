using UnityEngine;

namespace Soul
{
    // Per-region fog / ambient / sun, eased as the player crosses borders, plus the
    // Elden-Ring-style region title shown when entering a region.
    public class RegionAtmosphere
    {
        readonly Light sun; readonly Camera cam;
        Color fog, sky, eq, ground, sunCol; float density, sunI;
        /// ease rate toward the target look (1/s); SoulGame slows it for the purification reveal
        public float ease = .9f;
        /// global fog thinning (user 2026-11-07: "화면이 뿌옇다"); region tables keep their relative look
        public static float fogScale = .62f;
        /// metres around the player where fog fades out (shaders: _SoulFogClear)
        public static float clearRadius = 22;
        /// 0..1 vista view (전망 지점): the clear zone around the player widens
        public float vista;
        public int current = -1; public RegionDef Current => current >= 0 ? Regions.All[current] : null;
        float candidateTime; int candidate = -1;
        // banner
        public string bannerName, bannerTitle; public float bannerT = -1;
        const float BANNER_LEN = 4.2f;

        public RegionAtmosphere(Light sun, Camera cam)
        {
            this.sun = sun; this.cam = cam;
            if (sun) sun.shadowStrength = .65f;   // softer: tall ruins otherwise blanket the screen in black
            var r = Regions.Look(0); Snap(r);
        }
        void Snap(RegionDef r) { fog = r.fog; sky = r.ambSky; eq = r.ambEquator; ground = r.ambGround; sunCol = r.sun; density = r.fogDensity; sunI = r.sunIntensity; Apply(); }

        public void Update(float dt, Vector3 pos)
        {
            int at = Regions.At(pos);
            if (current < 0) { current = at; Snap(Regions.Look(at)); ShowBanner(Regions.All[at]); }
            // hysteresis: a new region must hold for 0.6 s before it counts (no flicker on borders)
            if (at != current) { if (at != candidate) { candidate = at; candidateTime = 0; } candidateTime += dt; if (candidateTime > .6f) { current = at; ShowBanner(Regions.All[at]); } }
            else candidate = -1;
            var t = Regions.Look(current); float k = Sim.Damp(ease, dt);
            fog = Color.Lerp(fog, t.fog, k); sky = Color.Lerp(sky, t.ambSky, k); eq = Color.Lerp(eq, t.ambEquator, k); ground = Color.Lerp(ground, t.ambGround, k);
            sunCol = Color.Lerp(sunCol, t.sun, k); density = Mathf.Lerp(density, t.fogDensity, k); sunI = Mathf.Lerp(sunI, t.sunIntensity, k);
            Apply();
            Shader.SetGlobalVector("_SoulFogClear", (Vector4)Sim.ToUnity(pos) + new Vector4(0, 0, 0, clearRadius * (1 + vista * 3)));
            if (bannerT >= 0) { bannerT += dt; if (bannerT > BANNER_LEN) bannerT = -1; }
        }
        void Apply()
        {
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fog; RenderSettings.fogDensity = density * fogScale;   // a vista keeps the fog: it hides where the streamed ground ends (far landmarks are drawn as fog-free silhouettes)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky * .85f; RenderSettings.ambientEquatorColor = eq * .85f; RenderSettings.ambientGroundColor = ground * .85f;
            if (sun) { sun.color = sunCol; sun.intensity = sunI; }
            if (cam) cam.backgroundColor = fog;
        }
        void ShowBanner(RegionDef r) { bannerName = r.name; bannerTitle = r.title; bannerT = 0; }

        static GUIStyle nameStyle, titleStyle; static Texture2D white;
        public void DrawGUI()
        {
            if (bannerT < 0) return;
            if (!white) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal };
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            }
            float t = bannerT, a = Mathf.Clamp01(t / .7f) * (1 - Sim.SmoothStep(t, BANNER_LEN - 1.1f, BANNER_LEN));
            float W = Screen.width, y = Screen.height * .32f, line = Mathf.Lerp(120, 360, Sim.Smooth(Mathf.Clamp01(t / 1.2f)));
            GUI.color = new Color(0, 0, 0, .35f * a); GUI.DrawTexture(new Rect(0, y - 46, W, 92), white);
            GUI.color = new Color(.92f, .86f, .72f, .8f * a);
            GUI.DrawTexture(new Rect(W / 2 - line / 2, y + 30, line, 1), white);
            GUI.color = new Color(1, .96f, .88f, a); GUI.Label(new Rect(0, y - 34, W, 52), bannerName, nameStyle);
            GUI.color = new Color(.85f, .8f, .7f, .85f * a); GUI.Label(new Rect(0, y + 32, W, 22), bannerTitle, titleStyle);
            GUI.color = Color.white;
        }
    }
}
