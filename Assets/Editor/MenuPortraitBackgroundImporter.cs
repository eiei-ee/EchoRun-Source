using UnityEditor;
using UnityEngine;

// Keep the authored phone background small and avoid transparent/mipmapped
// storage for this opaque, screen-sized UI texture.
public sealed class MenuPortraitBackgroundImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath != "Assets/Resources/Art/Menu/MemoryCorridorMenuPortrait.png") return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
