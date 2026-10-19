using System;
using System.IO;
using UnityEditor;

public static class CameraFollowReview
{
    public static void RebuildScriptCache()
    {
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(
            UnityEditor.Compilation.RequestScriptCompilationOptions.CleanBuildCache);
        UnityEngine.Debug.Log("CAMERA_SCRIPT_RECOMPILE_REQUESTED");
    }

    public static void RefreshCameraBinding()
    {
        const string path = "Assets/Scripts/CameraFollow.cs";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        UnityEngine.Debug.Log("CAMERA_SCRIPT_BINDING guid=" + AssetDatabase.AssetPathToGUID(path)
            + " class=" + (script != null ? script.GetClass()?.FullName : "missing"));
        if (script == null || script.GetClass() != typeof(CameraFollow))
            throw new InvalidOperationException("Camera MonoScript import did not resolve CameraFollow.");
    }

    // Isolated product/save and output; building does not reinstall animation.
    public static void BuildWindows()
    {
        string output = Environment.GetEnvironmentVariable("ECHORUN_CAMERA_BUILD")
            ?? "TestResults/CameraFollow-20261011/Candidate";
        if (Directory.Exists(output))
            throw new InvalidOperationException("Preserve the previous camera review build: " + output);
        ScriptingImplementation previous = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
        try
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            StackedCityReview.BuildInto(output, Environment.GetEnvironmentVariable("ECHORUN_REVIEW_PRODUCT")
                ?? "EchoRun-CameraStable-20261012");
        }
        finally { PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, previous); }
    }
}
