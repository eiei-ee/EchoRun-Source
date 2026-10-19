using UnityEditor;
using UnityEngine;

public sealed class EchoRoadArtImporter : AssetPostprocessor
{
    private const string AtlasPath = "Assets/Art/Road/EchoRoadAtlas.png";
    private const string NormalPath = "Assets/Art/Road/EchoRoadNormal.png";

    private void OnPreprocessTexture()
    {
        if (assetPath != AtlasPath && assetPath != NormalPath) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.streamingMipmaps = true;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.sRGBTexture = assetPath == AtlasPath;
        if (assetPath == NormalPath)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.convertToNormalmap = true;
            importer.heightmapScale = 0.14f;
        }

        TextureImporterPlatformSettings android =
            importer.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.maxTextureSize = 1024;
        android.format = TextureImporterFormat.ASTC_6x6;
        importer.SetPlatformTextureSettings(android);
    }
}
