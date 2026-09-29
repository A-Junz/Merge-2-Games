using UnityEditor;
using UnityEngine;

namespace MergePrototype.EditorTools
{
    /// <summary>让运行时 Sprite.Create 可以读取透明 4×2 图标表。</summary>
    public sealed class FoodChainTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;
            if (assetPath.StartsWith("Assets/Resources/UI/LevelUpPopup/"))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.isReadable = false;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.spriteBorder = IsNineSlicePopupAsset(assetPath)
                    ? new Vector4(28f, 28f, 28f, 28f)
                    : Vector4.zero;
                return;
            }

            if (!assetPath.StartsWith("Assets/Resources/FoodChains/")) return;

            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
        }

        private static bool IsNineSlicePopupAsset(string path)
        {
            return path.EndsWith("popup_panel.png")
                || path.EndsWith("reward_area.png")
                || path.EndsWith("claim_all_bg.png")
                || path.EndsWith("only_claim_bg.png");
        }
    }
}
