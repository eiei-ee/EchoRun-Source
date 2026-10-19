using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CityV7DeliveryBuild
{
    public static void Build()
    {
        BuildPlayer("EchoRun-CityV7-MainCharacter",
            "TestResults/CityV7Main/Windows/EchoRun.exe");
    }

    public static void BuildWithExistingSave()
    {
        if (PlayerSettings.companyName != "Eiei-ee")
            throw new InvalidOperationException("Unexpected company: refusing to change save identity.");
        BuildPlayer("EchoRun",
            "TestResults/CityV7Main/Windows-ExistingSave/EchoRun.exe");
    }

    private static void BuildPlayer(string product, string output)
    {
        if (Resources.Load<GameObject>("CityV7/Chunk0") == null)
            throw new InvalidOperationException("City V7 resources missing.");
        string previousProduct = PlayerSettings.productName;
        try
        {
            PlayerSettings.productName = product;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(
                new[] { "Assets/Scenes/SampleScene.scene" }, output,
                BuildTarget.StandaloneWindows64, BuildOptions.CompressWithLz4HC);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("City V7 build failed: " + report.summary.result);
            Debug.Log("CITY_V7_MAIN_BUILD_OK " + Path.GetFullPath(output));
        }
        finally { PlayerSettings.productName = previousProduct; }
    }

    public static void BuildScopeClosureWithExistingSave()
    {
        if (PlayerSettings.companyName != "Eiei-ee")
            throw new InvalidOperationException("Unexpected save company.");
        BuildPlayer("EchoRun", "TestResults/ScopeClosure/Windows/EchoRun.exe");
    }

    public static void PrepareScopeClosure()
    {
        const string path = "Assets/Resources/Art/Menu/MemoryCorridorMenu.png";
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Original menu background missing.");
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        EchoHudPrefabBuilder.Build();
    }

    public static void BuildScopeClosureReadabilityWithExistingSave()
    {
        if (PlayerSettings.companyName != "Eiei-ee")
            throw new InvalidOperationException("Unexpected save company.");
        BuildPlayer("EchoRun", "TestResults/ScopeClosureReadability/Windows/EchoRun.exe");
    }
}
