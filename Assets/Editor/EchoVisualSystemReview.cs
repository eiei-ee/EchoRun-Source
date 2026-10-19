using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Review artifacts are kept apart from release builds. This never changes saves.
public static class EchoVisualSystemReview
{
    public const string Output = "TestResults/VisualSystem-20260923";

    [MenuItem("Tools/Echo Runner/Prepare Visual System Review")]
    public static void Prepare()
    {
        NightGrapeWorldInstaller.Install();
        EchoHudPrefabBuilder.Build();
        // The WeChat editor assembly stays optional and owns its own prefab.
        if (!EditorApplication.ExecuteMenuItem("Tools/EchoRun/Rebuild Friend Challenge UI"))
            throw new InvalidOperationException("Friend challenge UI builder is unavailable.");
        Debug.Log("ECHO_VISUAL_SYSTEM_PREPARE_OK");
    }

    [MenuItem("Tools/Echo Runner/Build Visual System Review")]
    public static void BuildWindows()
    {
        const string scene = "Assets/Scenes/SampleScene.scene";
        string path = Path.GetFullPath(Output + "/Windows/EchoRun.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        BuildReport report = BuildPipeline.BuildPlayer(new[] { scene }, path,
            BuildTarget.StandaloneWindows64,
            BuildOptions.Development | BuildOptions.CompressWithLz4HC);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Visual system build failed: " + report.summary.result);
        Debug.Log("ECHO_VISUAL_SYSTEM_BUILD_OK " + path);
    }
}
