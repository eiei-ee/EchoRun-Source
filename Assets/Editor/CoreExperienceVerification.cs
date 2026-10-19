using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Developer-only artifacts. Normal two-run play uses the real save/learning flow.
public static class CoreExperienceVerification
{
    private const string Root = "TestResults/CoreExperienceV1";

    [Serializable]
    private sealed class Audit
    {
        public string generatedUtc;
        public string evidence = "Generated definitions and evaluator outcomes; not human play.";
        public float calibrationSeconds = SingleContractFlow.CalibrationDurationSeconds;
        public float challengeSeconds = SingleContractFlow.ChallengeDurationSeconds;
        public List<Row> rows = new List<Row>();
    }

    [Serializable]
    private sealed class Row
    {
        public int seed, originalLane, gate, hypothesisVersion;
        public PredictionGateLane[] lanes;
        public float[] successLeadSeconds;
        public float[] failureLeadSeconds;
    }

    [MenuItem("Tools/Echo Runner/Audit Core Experience V1")]
    public static void AuditRoutes()
    {
        var audit = new Audit { generatedUtc = DateTime.UtcNow.ToString("o") };
        var windows = new PredictionGateDistanceWindow[6];
        for (int i = 0; i < windows.Length; i++)
            windows[i] = new PredictionGateDistanceWindow {
                presentationDistance = 100 + i * 100,
                commitDistance = 120 + i * 100,
                resolveDistance = 140 + i * 100,
                exitDistance = 160 + i * 100 };
        foreach (int seed in new[] { 1337, 4187, 731 })
        for (int original = 0; original < 3; original++)
        foreach (PredictionGateDefinition gate in PredictionGateTemplates.Create(
                     1, seed, original, windows))
        foreach (PredictionGateDefinition definition in new[] {
                     gate, gate.RemapPrediction(StrategyKey.AvoidOriginal, 2) })
        {
            var row = new Row {
                seed = seed, originalLane = original, gate = definition.gateId,
                hypothesisVersion = definition.hypothesisVersion,
                lanes = definition.lanes, successLeadSeconds = new float[3],
                failureLeadSeconds = new float[3] };
            for (int i = 0; i < 3; i++)
            {
                row.successLeadSeconds[i] = PredictionGateEvaluator.Evaluate(
                    definition.gateId, definition.lanes[i].role,
                    GateExecutionOutcome.Success, 20f, 1f).signedLeadSeconds;
                row.failureLeadSeconds[i] = PredictionGateEvaluator.Evaluate(
                    definition.gateId, definition.lanes[i].role,
                    GateExecutionOutcome.Hit, 20f, 1f).signedLeadSeconds;
            }
            audit.rows.Add(row);
        }
        Directory.CreateDirectory(Root);
        File.WriteAllText(Path.Combine(Root, "route-audit.json"),
            JsonUtility.ToJson(audit, true));
        Debug.Log("CORE_EXPERIENCE_ROUTE_AUDIT " + audit.rows.Count);
    }

    public static void BuildPlayer()
    {
        AuditRoutes();
        RacingFeedbackCapture.CaptureCoreExperience();
        string product = PlayerSettings.productName;
        try
        {
            // Separate registry / persistentDataPath from the developer's EchoRun.
            PlayerSettings.productName = "EchoRun-CoreExperienceV1";
            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.enabled) scenes.Add(scene.path);
            if (scenes.Count == 0) throw new InvalidOperationException("No enabled scene.");
            string path = Path.Combine(Root, "Windows", "EchoRun.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildReport report = BuildPipeline.BuildPlayer(scenes.ToArray(), path,
                BuildTarget.StandaloneWindows64, BuildOptions.CompressWithLz4HC);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Core experience build failed: "
                                                    + report.summary.result);
            Debug.Log("CORE_EXPERIENCE_BUILD_OK " + Path.GetFullPath(path));
        }
        finally
        {
            PlayerSettings.productName = product;
        }
    }
}
