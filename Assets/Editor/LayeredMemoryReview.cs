using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Fixed-camera comparison and development player; never a release/deployment entry point.
public static class LayeredMemoryReview
{
    public const string Output = "TestResults/LayeredMemory-20260924";
    public const string DetailOutput = "TestResults/LayeredMemoryDetail-20260924";

    public static void CaptureDetailBefore()
    {
        StackedCityReview.CaptureInto(DetailOutput + "/Before", true, false, true);
    }

    public static void CaptureDetailAfter()
    {
        StackedCityReview.CaptureInto(DetailOutput + "/After", true, false, true);
    }

    public static void InstallDetailAndCapture()
    {
        LayeredMemoryArtInstaller.Install();
        CaptureDetailAfter();
    }

    public static void BuildDetailWindows()
    {
        StackedCityReview.BuildInto(DetailOutput, "EchoRun-LayeredMemory-DetailReview");
        Debug.Log("LAYERED_MEMORY_DETAIL_BUILD_OK");
    }

    public static void CaptureBefore()
    {
        WriteChunkInventory();
        StackedCityReview.CaptureInto(Output + "/Before", false);
    }

    public static void CaptureAfter()
    {
        StackedCityReview.CaptureInto(Output + "/After", true, false, true);
    }

    public static void PrepareUI()
    {
        EchoHudPrefabBuilder.Build();
        EditorApplication.ExecuteMenuItem("Tools/EchoRun/Rebuild Friend Challenge UI");
        AssetDatabase.SaveAssets();
        CaptureAfter();
    }

    public static void BuildWindows()
    {
        // Reuse the review builder's isolated product identity. PlayerPrefs and
        // telemetry then have their own namespace even during startup/migration.
        StackedCityReview.BuildInto(Output, "EchoRun-LayeredMemory-Review");
        Debug.Log("LAYERED_MEMORY_BUILD_OK " + Path.GetFullPath(Output + "/Windows/EchoRun.exe"));
    }

    private static void WriteChunkInventory()
    {
        var report = new StringBuilder();
        for (int index = 0; index < 9; index++)
        {
            GameObject prefab = Resources.Load<GameObject>("CityV7/Chunk" + index);
            if (prefab == null) throw new InvalidOperationException("Missing city chunk " + index);
            GameObject root = UnityEngine.Object.Instantiate(prefab);
            try
            {
                report.AppendLine("CHUNK " + index);
                foreach (Transform child in root.transform)
                {
                    Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) continue;
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    report.AppendLine(child.name + " position=" + child.localPosition.ToString("F2")
                        + " bounds=" + bounds.center.ToString("F2") + " size=" + bounds.size.ToString("F2"));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/chunk-inventory.txt", report.ToString());
    }
}
