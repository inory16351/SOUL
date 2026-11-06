using UnityEngine;
using UnityEngine.Rendering;

namespace Soul
{
    public enum Blend { Opaque, Alpha, Add }
    // three.js point-light intensity (candela, decay 2) -> URP intensity. Tune by eye.
    public static class SoulLights { public const float PointScale = 1.2f; }

    // Material factory for the two SOUL shaders. All colours are LINEAR (three.js stores
    // material / vertex colours linear) and may exceed 1 (HDR, bloom); they are written with
    // SetVector so Unity does not apply a gamma->linear conversion.
    public static class Mats
    {
        static Shader lit, unlit;
        public static Shader LitShader => lit ? lit : (lit = Shader.Find("SOUL/Lit"));
        public static Shader UnlitShader => unlit ? unlit : (unlit = Shader.Find("SOUL/Unlit"));
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int MainTex = Shader.PropertyToID("_MainTex");

        public static Material Lit(Color baseLinear, Color emissionLinear, float metallic, float smoothness, bool vertexColor, CullMode cull = CullMode.Back)
        {
            var m = new Material(LitShader);
            m.SetVector(BaseColor, (Vector4)baseLinear);
            m.SetVector("_EmissionColor", (Vector4)emissionLinear);
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_UseVertexColor", vertexColor ? 1 : 0); m.SetFloat("_Cull", (float)cull);
            m.enableInstancing = false;
            return m;
        }
        public static Material Unlit(Color colorLinear, Blend blend, bool vertexColor = false, CullMode cull = CullMode.Back, bool? zwrite = null, bool fog = true, Texture tex = null)
        {
            var m = new Material(UnlitShader);
            SetColor(m, colorLinear);
            m.SetFloat("_UseVertexColor", vertexColor ? 1 : 0);
            m.SetFloat("_UseFog", fog ? 1 : 0);
            m.SetFloat("_Cull", (float)cull);
            if (tex) m.SetTexture(MainTex, tex);
            SetBlend(m, blend, zwrite);
            return m;
        }
        public static void SetBlend(Material m, Blend blend, bool? zwrite = null)
        {
            switch (blend)
            {
                case Blend.Opaque: m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.Zero); m.renderQueue = 2000; break;
                case Blend.Alpha: m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); m.renderQueue = 3000; break;
                case Blend.Add: m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.One); m.renderQueue = 3000; break;
            }
            m.SetFloat("_ZWrite", (zwrite ?? blend == Blend.Opaque) ? 1 : 0);
            m.SetOverrideTag("RenderType", blend == Blend.Opaque ? "Opaque" : "Transparent");
        }
        public static void SetColor(Material m, Color linear) => m.SetVector(BaseColor, (Vector4)linear);
        public static Color GetColor(Material m) => (Color)m.GetVector(BaseColor);
        public static void SetOpacity(Material m, float a) { var c = GetColor(m); c.a = a; SetColor(m, c); }

        // Radial gradient texture (canvas createRadialGradient equivalent), stops: (offset, rgba sRGB 0..1)
        public static Texture2D Radial(int size, params (float o, Color c)[] stops) => Radial(size, 0, stops);
        public static Texture2D Radial(int size, float innerFrac, params (float o, Color c)[] stops)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float h = size / 2f, r0 = innerFrac * h;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + .5f - h) * (x + .5f - h) + (y + .5f - h) * (y + .5f - h));
                float o = Mathf.Clamp01((d - r0) / Mathf.Max(1e-3f, h - r0));
                Color c = stops[stops.Length - 1].c;
                if (d < r0) c = new Color(0, 0, 0, 0);
                else for (int i = 0; i < stops.Length - 1; i++)
                    if (o >= stops[i].o && o <= stops[i + 1].o) { c = Color.Lerp(stops[i].c, stops[i + 1].c, (o - stops[i].o) / Mathf.Max(1e-5f, stops[i + 1].o - stops[i].o)); break; }
                if (d >= h) c.a = 0;
                px[y * size + x] = c;
            }
            t.SetPixels(px); t.Apply(); return t;
        }
    }
}
