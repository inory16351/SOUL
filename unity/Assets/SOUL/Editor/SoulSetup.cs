using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Soul.EditorTools
{
    // One-time project setup: linear colour space, URP pipeline asset (HDR, soft shadows),
    // a post-processing profile matching the web build (ACES, bloom ~ UnrealBloom .62/.5/.88),
    // SOUL shaders always included in builds.
    public static class SoulSetup
    {
        const string Dir = "Assets/SOUL/Settings";

        [MenuItem("SOUL/Setup Project (URP, Linear, Post FX)")]
        public static void Setup()
        {
            Directory.CreateDirectory(Dir);
            PlayerSettings.colorSpace = ColorSpace.Linear;

            var rdPath = Dir + "/SoulRenderer.asset";
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rdPath);
            if (!rd) { rd = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(rd, rdPath); }
            var urpPath = Dir + "/SoulURP.asset";
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
            if (!urp) { urp = UniversalRenderPipelineAsset.Create(rd); AssetDatabase.CreateAsset(urp, urpPath); }
            urp.supportsHDR = true;
            urp.msaaSampleCount = 4;
            urp.shadowDistance = 45;
            urp.shadowCascadeCount = 2;
            var so = new SerializedObject(urp);
            void Set(string prop, System.Action<SerializedProperty> f) { var p = so.FindProperty(prop); if (p != null) f(p); else Debug.LogWarning("[SOUL] URP field missing: " + prop); }
            Set("m_MainLightShadowmapResolution", p => p.intValue = 4096);
            Set("m_SoftShadowsSupported", p => p.boolValue = true);
            Set("m_AdditionalLightsRenderingMode", p => p.intValue = 1);   // per pixel
            Set("m_AdditionalLightsPerObjectLimit", p => p.intValue = 8);
            Set("m_HDRColorBufferPrecision", p => p.intValue = 0);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);

            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = urp; }
            QualitySettings.SetQualityLevel(current, true);

            // post-processing profile
            var profPath = Dir + "/SoulPostFX.asset";
            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profPath);
            if (!prof) { prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, profPath); }
            T Comp<T>() where T : VolumeComponent { if (!prof.TryGet<T>(out var c)) { c = prof.Add<T>(true); AssetDatabase.AddObjectToAsset(c, prof); } return c; }
            var bloom = Comp<Bloom>();
            bloom.threshold.Override(.9f); bloom.intensity.Override(.9f); bloom.scatter.Override(.62f); bloom.highQualityFiltering.Override(true);
            var tone = Comp<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            var ca = Comp<ColorAdjustments>(); ca.postExposure.Override(.07f);
            var vig = Comp<Vignette>(); vig.intensity.Override(.22f); vig.smoothness.Override(.45f);
            EditorUtility.SetDirty(prof);

            // keep the SOUL shaders in player builds (materials are created at runtime)
            var gs = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset"));
            var list = gs.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in new[] { "SOUL/Lit", "SOUL/Unlit", "SOUL/Water" })
            {
                var sh = Shader.Find(name); if (!sh) continue;
                bool has = false; for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) has = true;
                if (!has) { list.InsertArrayElementAtIndex(list.arraySize); list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh; }
            }
            gs.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[SOUL] project setup done (URP + Linear + PostFX)");
        }
    }
}
