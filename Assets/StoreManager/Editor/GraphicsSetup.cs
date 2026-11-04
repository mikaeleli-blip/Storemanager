using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StoreManager.EditorTools
{
    /// <summary>
    /// Graphics setup (ported from Real Life), run automatically when missing and from the Storemanager menu:
    /// a URP pipeline asset (Forward+, HDR, soft shadows, SSAO) on Graphics and every Quality level, linear colour.
    /// </summary>
    [InitializeOnLoad]
    static class GraphicsSetup
    {
        const string SettingsFolder = "Assets/Settings";
        const string PipelinePath = SettingsFolder + "/StoremanagerURP.asset";
        const string RendererPath = SettingsFolder + "/StoremanagerURP_Renderer.asset";
        const string SessionKey = "Storemanager.GraphicsChecked";

        static GraphicsSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SessionState.GetBool(SessionKey, false)) return;
                SessionState.SetBool(SessionKey, true);
                try
                {
                    if (GraphicsSettings.defaultRenderPipeline == null || !File.Exists(PipelinePath)) SetupPipeline();
                }
                catch (Exception e) { Debug.LogException(e); }
            };
        }

        [MenuItem("Storemanager/Graphics/Set Up URP Pipeline")]
        public static void SetupPipeline()
        {
            Directory.CreateDirectory(SettingsFolder);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            var rso = new SerializedObject(renderer);
            SetInt(rso, "m_RenderingMode", 2);          // Forward+
            SetInt(rso, "m_DepthPrimingMode", 0);
            rso.ApplyModifiedPropertiesWithoutUndo();
            AddSsao(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            var pso = new SerializedObject(pipeline);
            SetBool(pso, "m_SupportsHDR", true);
            SetInt(pso, "m_MSAA", 4);
            SetFloat(pso, "m_RenderScale", 1f);
            SetBool(pso, "m_RequireDepthTexture", true);
            SetBool(pso, "m_MainLightShadowsSupported", true);
            SetInt(pso, "m_MainLightShadowmapResolution", 4096);
            SetFloat(pso, "m_ShadowDistance", 150f);
            SetInt(pso, "m_ShadowCascadeCount", 2);
            SetBool(pso, "m_SoftShadowsSupported", true);
            SetInt(pso, "m_SoftShadowQuality", 2);
            SetInt(pso, "m_AdditionalLightsRenderingMode", 1);
            SetInt(pso, "m_AdditionalLightsPerObjectLimit", 8);
            SetBool(pso, "m_AdditionalLightShadowsSupported", false);
            SetInt(pso, "m_ColorGradingMode", 1);
            SetInt(pso, "m_ColorGradingLutSize", 32);
            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            AssetDatabase.SaveAssets();
            Debug.Log("[Storemanager] URP pipeline configured (Forward+, HDR, soft shadows, SSAO).");
        }

        static void AddSsao(UniversalRendererData renderer)
        {
            if (renderer.rendererFeatures.Any(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion")) return;
            Type t = typeof(UniversalRendererData).Assembly.GetTypes().FirstOrDefault(x => x.Name == "ScreenSpaceAmbientOcclusion");
            if (t == null) { Debug.LogWarning("[Storemanager] SSAO feature type not found."); return; }
            var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(t);
            feature.name = "SSAO";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            AssetDatabase.SaveAssets();
            var so = new SerializedObject(renderer);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
            if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
            {
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var fso = new SerializedObject(feature);
            SetFloat(fso, "m_Settings.Intensity", 1.2f);
            SetFloat(fso, "m_Settings.Radius", 0.35f);
            SetFloat(fso, "m_Settings.DirectLightingStrength", 0.3f);
            SetFloat(fso, "m_Settings.Falloff", 100f);
            fso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
        }

        static void SetInt(SerializedObject so, string prop, int v)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.Log("[Storemanager] (setup) property not found: " + prop); return; }
            p.intValue = v;
        }

        static void SetBool(SerializedObject so, string prop, bool v)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.Log("[Storemanager] (setup) property not found: " + prop); return; }
            p.boolValue = v;
        }

        static void SetFloat(SerializedObject so, string prop, float v)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.Log("[Storemanager] (setup) property not found: " + prop); return; }
            p.floatValue = v;
        }
    }

    /// <summary>Import settings for the Blender models and the palette/floor textures.</summary>
    sealed class ModelImport : AssetPostprocessor
    {
        const string Models = "Assets/StoreManager/Resources/Models/";
        const string Textures = "Assets/StoreManager/Resources/Textures/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Models)) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.animationType = ModelImporterAnimationType.None;
            // Shelf rows are merged from the product units at runtime, which needs their vertex data.
            mi.isReadable = assetPath.EndsWith("Products.fbx");
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importNormals = ModelImporterNormals.Import;
            mi.addCollider = false;
        }

        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            if (assetPath.StartsWith(Models))
            {
                // Palette: one flat colour per cell, so no filtering, no mips, no compression.
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.alphaSource = TextureImporterAlphaSource.None;
            }
            else if (assetPath.StartsWith(Textures))
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.filterMode = FilterMode.Trilinear;
                ti.anisoLevel = 8;
                ti.mipmapEnabled = true;
                ti.alphaSource = TextureImporterAlphaSource.None;
            }
        }
    }
}
