// Installs the locally reviewed FlowFit clips without redistributing licensed motion data.
// This helper is inert until InstallReviewedLocal is explicitly invoked.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RunnerFlowPreviewInstaller
{
    private const string ControllerPath = "Assets/Animations/HumanMotion/EchoRunHuman.controller";
    private const string PrivateFolder = "Assets/Animations/HumanMotion/KevinBasicPrivate/Generated/";
    private static string ReportFolder => Environment.GetEnvironmentVariable("ECHORUN_JUMP_INSTALL_REPORT")
        ?? "TestResults/RunnerJumpRevision-20261011/Binding";
    private const float LandingSeconds = .12f;

    public static void RebuildAndInstallJump()
    {
        RunnerFlowJumpAuthor.BuildFittedCandidates();
        InstallReviewedLocal();
    }

    public static void BuildJumpReviewWindows()
    {
        const string output = "TestResults/JumpRevision-20261011/FootCandidate";
        Require(!Directory.Exists(output), "Preserve the existing review build.");
        ScriptingImplementation previous = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
        try
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            StackedCityReview.BuildInto(output, "EchoRun-JumpFoot-20261011");
        }
        finally { PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, previous); }
    }

    // Explicit local tuning entry. Editing the scene through SerializedObject
    // preserves its existing component identity and unrelated configuration.
    public static void BuildGroundedTimingPreview()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Leave Play mode before tuning the preview.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.scene", OpenSceneMode.Single);
        var players = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerController>(true)).ToArray();
        Require(players.Length == 1, "Expected one scene player.");
        var serialized = new SerializedObject(players[0]);
        serialized.FindProperty("jumpHeight").floatValue = 2.4f;
        serialized.FindProperty("jumpDuration").floatValue = .78f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "Unable to save jump tuning.");
        RunnerFlowJumpAuthor.BuildFittedCandidates();
        InstallReviewedLocal();
        Debug.Log("GROUNDED_JUMP_TIMING_READY height=2.4 seconds=.78");
    }

    public static void InstallReviewedLocal()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Leave Play mode before installing local preview motion.");
        AnimationClip jump = LoadClip(PrivateFolder + "RunnerFlowFitJump.anim", .9f);
        AnimationClip land = LoadClip(PrivateFolder + "RunnerFlowFitLand.anim", LandingSeconds);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Require(controller != null && controller.layers.Length > 0, "The existing production controller must have a base layer.");

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState jumpState = State(machine, "Jump");
        AnimatorState landState = State(machine, "Land");
        Require(jumpState.timeParameterActive && jumpState.timeParameter == "RunnerJumpPhase"
            && !jumpState.iKOnFeet, "Existing Jump must use RunnerJumpPhase with Foot IK disabled.");
        Require(controller.parameters.Any(parameter => parameter.name == "RunnerJumpPhase"
            && parameter.type == AnimatorControllerParameterType.Float), "RunnerJumpPhase must already exist as a float parameter.");
        Require(!landState.timeParameterActive && !landState.speedParameterActive
            && !landState.iKOnFeet, "Existing Land must play forward at its state speed with Foot IK disabled.");

        var protectedStates = new Dictionary<AnimatorState, string>();
        foreach (string name in new[] { "Idle", "Run", "Slide" })
        {
            AnimatorState state = State(machine, name);
            protectedStates.Add(state, EditorJsonUtility.ToJson(state));
        }

        Directory.CreateDirectory(ReportFolder);
        string backup = ReportFolder + "/controller-before-flow.controller";
        if (!File.Exists(backup)) File.Copy(ControllerPath, backup, false);

        Motion previousJump = jumpState.motion;
        Motion previousLand = landState.motion;
        float previousLandSpeed = landState.speed;
        try
        {
            // Preserve the reviewed phase/IK configuration; only bind clips and
            // fit Land's authored duration to the driver's existing recovery window.
            jumpState.motion = jump;
            landState.motion = land;
            landState.speed = land.length / LandingSeconds;
            foreach (KeyValuePair<AnimatorState, string> entry in protectedStates)
                Require(EditorJsonUtility.ToJson(entry.Key) == entry.Value,
                    "The accepted " + entry.Key.name + " state changed during preview installation.");
        }
        catch
        {
            jumpState.motion = previousJump;
            landState.motion = previousLand;
            landState.speed = previousLandSpeed;
            throw;
        }

        EditorUtility.SetDirty(jumpState);
        EditorUtility.SetDirty(landState);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);

        var report = new StringBuilder();
        report.AppendLine("Local FlowFit preview binding; visual acceptance by the user remains separate.");
        report.AppendLine("Controller=" + ControllerPath);
        report.AppendLine("Backup=" + backup + " (first backup retained; never overwritten)");
        report.AppendLine("PreviousJump=" + AssetDatabase.GetAssetPath(previousJump));
        report.AppendLine("PreviousLand=" + AssetDatabase.GetAssetPath(previousLand));
        report.AppendLine("Jump=" + AssetDatabase.GetAssetPath(jump));
        report.AppendLine("Land=" + AssetDatabase.GetAssetPath(land));
        report.AppendLine("LandStateSpeed=" + landState.speed.ToString("R", CultureInfo.InvariantCulture));
        report.AppendLine("LandEffectiveSeconds=" + (land.length / landState.speed).ToString("R", CultureInfo.InvariantCulture));
        report.AppendLine("Idle, Run and Slide state snapshots are unchanged. Jump/Land phase and Foot IK settings are preserved.");
        report.AppendLine("LICENSE NOTE: These clips depend on the locally licensed KevinBasicPrivate source assets and generated curves.");
        report.AppendLine("Keep both source and derived standalone animation files private and Git-ignored; do not commit or redistribute them as reusable assets.");
        report.AppendLine("This controller now references those local assets. A checkout without them cannot reproduce this preview; review licensing and dependency handling before any repository publication.");
        File.WriteAllText(ReportFolder + "/flow-local-preview-binding.txt", report.ToString());
        Debug.Log("RUNNER_FLOW_LOCAL_PREVIEW_INSTALLED");
    }

    private static AnimatorState State(AnimatorStateMachine machine, string name)
    {
        AnimatorState[] matches = machine.states.Where(child => child.state.name == name)
            .Select(child => child.state).ToArray();
        Require(matches.Length == 1, "Expected exactly one existing state named " + name + ".");
        return matches[0];
    }

    private static AnimationClip LoadClip(string path, float seconds)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        Require(clip != null && clip.isHumanMotion && !clip.isLooping,
            "Reviewed local clip must exist, use Humanoid motion and be non-looping: " + path);
        Require(Mathf.Abs(clip.length - seconds) <= .002f,
            "Reviewed local clip has an unexpected duration: " + path);
        return clip;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
