using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PortraitPolishReview
{
    public const string Output = "TestResults/PortraitPolish-20260925";

    public static void Prepare()
    {
        Directory.CreateDirectory(Output);
        string roadBackup = Output + "/OE_RoadDeck-before-polish.mat";
        if (!File.Exists(roadBackup))
            File.Copy("Assets/Art/OrangeEcho/Materials/OE_RoadDeck.mat", roadBackup);
        LayeredMemoryPalette.InstallRoadSurfaces();
        BuildHomePreview();
        if (!EditorApplication.ExecuteMenuItem("Tools/EchoRun/Rebuild Friend Challenge UI"))
            throw new InvalidOperationException("Friend UI authoring command unavailable.");
        Debug.Log("PORTRAIT_POLISH_ASSETS_READY");
    }

    private static void BuildHomePreview()
    {
        var root = new GameObject("CityHomePreview");
        try
        {
            // Let the existing road end beyond the fog instead of as a visible
            // platform edge. Keep the detailed city count fixed for menu memory.
            for (int index = 0; index < 10; index++)
            {
                AddPreviewAsset(root.transform, "OrangeEcho/OrangeRoadStraight", index * 20f);
                if (index < 4)
                    AddPreviewAsset(root.transform, "CityV7/Chunk" + (index % 3), index * 20f);
            }
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            const string path = "Assets/Resources/Art/Menu/CityHomePreview.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void AddPreviewAsset(Transform parent, string path, float z)
    {
        GameObject prefab = Resources.Load<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Preview art missing: " + path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = new Vector3(0f, 0f, z);
    }

    public static void BuildWindows()
    {
        Prepare();
        string previousName = PlayerSettings.productName;
        try
        {
            // Local review progress is separate from the user's normal game.
            PlayerSettings.productName = "EchoRun-Polish-Review";
            string path = Path.GetFullPath(Output + "/Windows-R3/EchoRun.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildReport report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/SampleScene.scene" },
                path, BuildTarget.StandaloneWindows64,
                BuildOptions.Development | BuildOptions.CompressWithLz4HC);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Polish review build failed: " + report.summary.result);
            Debug.Log("PORTRAIT_POLISH_BUILD_OK " + path);
        }
        finally
        {
            PlayerSettings.productName = previousName;
            AssetDatabase.SaveAssets();
        }
    }
}
