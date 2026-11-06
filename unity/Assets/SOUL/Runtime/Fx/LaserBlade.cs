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
            float flicker = 1 + Mathf.Sin(t * 47) * .05f + Mathf.Sin(t * 19.3f) * .03f;
            if (glow) { var c = glowColor; c.a = (.40f + boost * .35f) * flicker; Mats.SetColor(glow, c); }
            if (halo) { var c = haloColor; c.a = (.10f + boost * .22f) * flicker; Mats.SetColor(halo, c); }
            if (line) { var c = CORE * (3 + boost * 2); c.a = 1; Mats.SetColor(line, c); }
            if (light) light.intensity = (.8f + boost * 1.8f) * flicker * SoulLights.PointScale;
        }
    }
}
