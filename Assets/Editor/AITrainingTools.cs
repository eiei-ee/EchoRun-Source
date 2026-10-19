using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AITrainingTools
{
    [MenuItem("Tools/AI/Run 20K Segment Simulation")]
    public static void RunDefaultSimulation()
    {
        var config = new AITrainingSimulationConfig();
        AITrainingComparisonResult result = AITrainingSimulator.Compare(
            config, EchoRunSaveSystem.GetDirectorWeights(),
            EchoRunSaveSystem.GetDirectorPolicyJson());

        string directory = GetTrainingDataDirectory();
        string path = Path.Combine(directory,
            "comparison-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
        File.WriteAllText(path, JsonUtility.ToJson(result, true));
        AssetDatabase.Refresh();
        Debug.Log(string.Format(
            "AI comparison complete: {0} segments each, "
            + "baseline reward {1:0.000}, LinUCB reward {2:0.000}, "
            + "lift {3:+0.000;-0.000;0.000}\n{4}",
            result.linUcb.totalSegments,
            result.baseline.meanReward, result.linUcb.meanReward,
            result.rewardLift, path));
    }

    [MenuItem("Tools/AI/Export Latest Run Telemetry")]
    public static void ExportLatestRunTelemetry()
    {
        string path = AIRunTelemetry.ExportLatestRun(
            GetTrainingDataDirectory());
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("No completed AI run telemetry is available.");
            return;
        }
        AssetDatabase.Refresh();
        Debug.Log("AI run telemetry exported: " + path);
    }

    private static string GetTrainingDataDirectory()
    {
        string directory = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "TrainingData"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
