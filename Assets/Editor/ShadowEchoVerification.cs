using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Local validation artifacts only; reuse the existing isolated-save review builder.
public static class ShadowEchoVerification
{
    public static void BuildBaselineWindows()
    {
        RequireNewOutput("TestResults/ShadowEcho-20261008/Baseline");
        StackedCityReview.BuildInto("TestResults/ShadowEcho-20261008/Baseline",
            "EchoRun-ShadowEcho-20261008-Baseline");
        Debug.Log("SHADOW_ECHO_BASELINE_BUILD_OK");
    }

    public static void BuildCandidateWindows()
    {
        RequireNewOutput("TestResults/ShadowEcho-20261008/Candidate");
        StackedCityReview.BuildInto("TestResults/ShadowEcho-20261008/Candidate",
            "EchoRun-ShadowEcho-20261008-Candidate");
        Debug.Log("SHADOW_ECHO_CANDIDATE_BUILD_OK");
    }

    public static void BuildCandidateMonoWindows()
    {
        BuildMonoWindows("CandidateMono", "SHADOW_ECHO_CANDIDATE_MONO_BUILD_OK");
    }

    public static void BuildCandidateMonoR2Windows()
    {
        BuildMonoWindows("CandidateMonoR2", "SHADOW_ECHO_CANDIDATE_MONO_R2_BUILD_OK");
    }

    public static void BuildPerformanceBaselineWindows()
    {
        BuildMonoWindows("PerformanceBaseline", "SHADOW_ECHO_PERFORMANCE_BASELINE_BUILD_OK");
    }

    public static void BuildPerformanceCandidateWindows()
    {
        BuildMonoWindows("PerformanceCandidate", "SHADOW_ECHO_PERFORMANCE_CANDIDATE_BUILD_OK");
    }

    public static void BuildVisualEvidenceWindows()
    {
        BuildMonoWindows("VisualEvidenceMono", "SHADOW_ECHO_VISUAL_EVIDENCE_BUILD_OK");
    }

    public static void BuildVisualMotionWindows()
    {
        BuildMonoWindows("VisualMotionMono", "SHADOW_ECHO_VISUAL_MOTION_BUILD_OK");
    }

    private static void BuildMonoWindows(string revision, string successMarker)
    {
        string output = "TestResults/ShadowEcho-20261008/" + revision;
        RequireNewOutput(output);
        ScriptingImplementation previous = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
        try
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            StackedCityReview.BuildInto(output, "EchoRun-ShadowEcho-20261008-" + revision);
            Debug.Log(successMarker);
        }
        finally
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, previous);
        }
    }

    private static void RequireNewOutput(string output)
    {
        if (Directory.Exists(output))
            throw new InvalidOperationException("Preserve existing verification artifacts: " + output);
    }
}
