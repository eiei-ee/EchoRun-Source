using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Reproducible local art review. Never changes the production save identity.
public static class ExperienceSliceTools
{
    public const string Output = "TestResults/ExperienceSliceV1";

    public static void InspectAndBuildBaseline()
    {
        Directory.CreateDirectory(Output);
        var report = new StringBuilder();
        for (int i = 0; i < 9; i++)
        {
            var chunk = Resources.Load<GameObject>("CityV7/Chunk" + i);
            foreach (var filter in chunk.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                report.AppendLine(i + " | " + filter.name + " | position "
                    + filter.transform.localPosition + " | bounds " + mesh.bounds
                    + " | vertices " + mesh.vertexCount);
            }
        }
        File.WriteAllText(Output + "/mesh-inventory.txt", report.ToString());
        Build("Before");
    }

    public static void BuildAfter() { Build("After"); }
    public static void InstallAndBuildAfter()
    {
        InstallExperienceSlice.Install();
        CaptureHudAndBuildAfter();
    }

    public static void CaptureHudAndBuildAfter()
    {
        RacingFeedbackCapture.CaptureExperienceSlice();
        Build("After");
    }

    private static void Build(string variant)
    {
        string product = PlayerSettings.productName;
        try
        {
            PlayerSettings.productName = "EchoRun-ExperienceSliceV1-Review";
            string path = Output + "/" + variant + "/EchoRun.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/SampleScene.scene" },
                path, BuildTarget.StandaloneWindows64,
                BuildOptions.Development | BuildOptions.CompressWithLz4HC);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Experience slice build failed.");
            Debug.Log("EXPERIENCE_SLICE_BUILD_OK " + variant);
        }
        finally { PlayerSettings.productName = product; }
    }
}
