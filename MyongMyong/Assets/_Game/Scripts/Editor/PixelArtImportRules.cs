using Myong.Unity;
using UnityEditor;
using UnityEngine;

namespace Myong.Editor
{
    /// <summary>
    /// 도트 텍스처 임포트 규칙. Assets/_Game/Art/ 아래의 모든 텍스처에 자동으로 걸린다:
    /// 스프라이트 · PPU 통일 · Point 필터 · 압축 없음 · 밉맵 끔. 피벗과 Single/Multiple은 건드리지 않는다.
    /// </summary>
    sealed class PixelArtImportRules : AssetPostprocessor
    {
        public const string ArtFolder = "Assets/_Game/Art";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder + "/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            // 처음 들어올 때는 Single(파일 하나 = 스프라이트 하나). 그 뒤에 시트로 바꾼 것(Multiple)은 그대로 둔다.
            if (importer.importSettingsMissing || importer.spriteImportMode == SpriteImportMode.None) importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelArt.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }

        /// <summary>이미 들어와 있는 텍스처에도 규칙을 적용한다(다시 임포트).</summary>
        [MenuItem("Myong/Setup/Reimport Art (Pixel Rules)", priority = 12)]
        public static void ReimportArt()
        {
            if (!AssetDatabase.IsValidFolder(ArtFolder)) return;
            AssetDatabase.ImportAsset(ArtFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }
    }
}
