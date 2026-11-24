using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Myong.Editor
{
    /// <summary>
    /// URP를 2D 렌더러로. 템플릿의 PC/Mobile 에셋(Assets/Settings)은 그대로 두고
    /// 2D용 렌더러 · 파이프라인 에셋을 새로 만들어 그래픽 기본값과 모든 품질 단계에 지정한다.
    /// </summary>
    public static class RenderSetup
    {
        public const string Folder = "Assets/_Game/Settings";
        public const string RendererPath = Folder + "/Myong2D_Renderer.asset";
        public const string PipelinePath = Folder + "/Myong2D_RPAsset.asset";

        [MenuItem("Myong/Setup/Apply 2D Render Settings", priority = 10)]
        public static void Apply()
        {
            SetupUtil.EnsureFolder(Folder);

            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (renderer == null) renderer = CreateRenderer();

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            if (GraphicsSettings.defaultRenderPipeline != pipeline) GraphicsSettings.defaultRenderPipeline = pipeline;

            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) == pipeline) continue;
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            if (QualitySettings.GetQualityLevel() != current) QualitySettings.SetQualityLevel(current, false);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Myong Setup] 2D render settings applied: {PipelinePath}");
        }

        static Renderer2DData CreateRenderer()
        {
            // 에디터 메뉴(Create > Rendering > URP 2D Renderer)와 같은 경로로 만든다(기본 리소스가 채워진다).
            // 그 내부 함수를 못 찾으면 빈 인스턴스로 만든다.
            Renderer2DData data = null;
            try
            {
                var method = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (method != null)
                {
                    object type2D = Enum.Parse(method.GetParameters()[1].ParameterType, "_2DRenderer");
                    data = method.Invoke(null, new[] { RendererPath, type2D, false, string.Empty }) as Renderer2DData;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Myong Setup] CreateRendererAsset failed, falling back: {e.Message}");
            }

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(data, RendererPath);
            }
            return data;
        }
    }
}
