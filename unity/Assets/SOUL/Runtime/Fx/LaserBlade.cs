using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Soul
{
    // Port of neon.js createLaserBlade().update: the blade's cutting edge is traced by three
    // exported tubes under the sword (line = alpha-blended core, glow + halo = additive
    // sheaths) plus a blue point light; they flicker and brighten while a strike is live.
    public class LaserBlade
    {
        static readonly Color BLUE = new Color(.20f, .62f, 1.0f), CORE = new Color(.80f, .95f, 1.0f);
        // 독에 잠식된 코어 (Z6 껍질 숙주 part): the blade turns violet while it is owned
        static readonly Color VIOLET = new Color(.62f, .16f, 1.0f), VCORE = new Color(.95f, .78f, 1.0f);
        public static bool toxic; float toxicK;
        readonly Material line, glow, halo; readonly Light light;
        readonly Color glowColor, haloColor;
        float t;

        public LaserBlade(CharacterRig ch)
        {
            var sword = ch.model.Part("sword");
            if (!sword) { Debug.LogWarning("[SOUL] LaserBlade: no sword"); return; }
            foreach (Transform c in sword)
            {
                var r = c.GetComponent<MeshRenderer>();
                if (r && r.sharedMaterial && r.sharedMaterial.shader == Mats.UnlitShader)
                {
                    var m = r.sharedMaterial; float dst = m.GetFloat("_DstBlend"), a = Mats.GetColor(m).a;
                    if (dst == (float)BlendMode.One) { if (a > .3f) { if (!glow) glow = r.material; } else if (!halo) halo = r.material; }
                    else if (!line) line = r.material;
                }
                var l = c.GetComponent<Light>();
                if (l && l.color.b > l.color.r && !light) light = l;
            }
            glowColor = BLUE * 2.2f; haloColor = BLUE * 1.1f;
            if (!line || !glow || !halo) Debug.LogWarning($"[SOUL] LaserBlade tubes: line {(bool)line} glow {(bool)glow} halo {(bool)halo}");
        }
        public void Update(float dt, float boost)
        {
            t += dt;
            toxicK = Mathf.MoveTowards(toxicK, toxic ? 1 : 0, dt * 1.5f);
            Color gc = Color.Lerp(BLUE, VIOLET, toxicK) * 2.2f, hc = Color.Lerp(BLUE, VIOLET, toxicK) * 1.1f, cc = Color.Lerp(CORE, VCORE, toxicK);
            float flicker = 1 + Mathf.Sin(t * 47) * .05f + Mathf.Sin(t * 19.3f) * .03f;
            if (glow) { var c = gc; c.a = (.40f + boost * .35f) * flicker; Mats.SetColor(glow, c); }
            if (halo) { var c = hc; c.a = (.10f + boost * .22f) * flicker; Mats.SetColor(halo, c); }
            if (line) { var c = cc * (3 + boost * 2); c.a = 1; Mats.SetColor(line, c); }
            if (light) light.color = Color.Lerp(BLUE, VIOLET, toxicK);
            if (light) light.intensity = (.8f + boost * 1.8f) * flicker * SoulLights.PointScale;
        }
    }
}
