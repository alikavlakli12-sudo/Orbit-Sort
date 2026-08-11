using UnityEditor;
using UnityEngine;

namespace OrbitSort.Editor
{
    public sealed class OrbitSortUiTextureImporter : AssetPostprocessor
    {
        private const string UiTexturePath =
            "Assets/OrbitSort/Resources/UI/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(
                    UiTexturePath,
                    System.StringComparison.Ordinal))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
        }
    }
}
