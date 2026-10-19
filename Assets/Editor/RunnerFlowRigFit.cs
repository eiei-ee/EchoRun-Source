// Editor-only private candidate bake. No production bindings or runtime rules change.
// Root/foot curve spaces are measured through Animator, never inferred from bone units.
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
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class RunnerFlowRigFit
{
    public const string Folder = "Assets/Animations/HumanMotion/KevinBasicPrivate/Generated";
    public static string ReportFolder => Environment.GetEnvironmentVariable("ECHORUN_JUMP_FIT_REPORT")
        ?? "TestResults/RunnerJumpRevision-20261011/FlowRigFit";
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";
    private const float Rate = 60f, HipsTravel = .035f, FootSplay = 12f;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly string[] Vectors = { "RootT", "LeftFootT", "RightFootT" };
    private static readonly string[] Rotations = { "RootQ", "LeftFootQ", "RightFootQ" };
    private static readonly string[] Muscles = HumanTrait.MuscleName.Select(MuscleProperty).ToArray();
    private static readonly string[] Properties = Muscles
        .Concat(Vectors.SelectMany(p => "xyz".Select(a => p + "." + a)))
        .Concat(Rotations.SelectMany(p => "xyzw".Select(a => p + "." + a))).ToArray();

    [MenuItem("Tools/Echo Runner/Flow Jump/Inspect Rig Fit Calibration")]
    public static void InspectCalibration() { Execute(false); }

    // Small first gate: retain all 21 source root/foot curves and round-trip only
    // the 95 muscles. This tells us whether direct foot rotation can be baked
    // safely before attempting any body/goal-coordinate calibration.
    public static void InspectMuscleRoundTrip()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Leave Play mode first.");
        Directory.CreateDirectory(ReportFolder);
        var csv = new StringBuilder("clip,phase,sourceRootX,sourceRootZ,sourceRootYaw,beforeHipsLocalX,unchangedReplayHipsLocalX,feetEditedReplayHipsLocalX,beforeLeftYaw,beforeRightYaw,editedLeftYaw,editedRightYaw,replayLeftYaw,replayRightYaw,unchangedMaxBonePositionError,unchangedMaxBoneAngleError,feetEditedMaxBonePositionError,feetEditedMaxBoneAngleError,beforeHipsWorldOffsetX,unchangedHipsWorldOffsetX,feetEditedHipsWorldOffsetX\n");
        using (var rig = new Rig())
        foreach (string kind in new[] { "Jump", "Land" })
        {
            AnimationClip source = Load(Folder + "/RunnerFlow" + kind + ".anim", "RunnerFlow" + kind);
            for (int i = 0; i <= 8; i++)
            {
                float phase = i / 8f;
                Dictionary<string, float> input = Read(source, phase * source.length);
                rig.Sample(input, false);
                Snapshot before = rig.Snapshot();
                Dictionary<string, float> roundTrip = new Dictionary<string, float>(input);
                for (int m = 0; m < Muscles.Length; m++) roundTrip[Muscles[m]] = before.Pose.muscles[m];
                rig.Sample(roundTrip, false);
                Snapshot unchanged = rig.Snapshot();
                rig.Sample(input, false);
                AlignFoot(rig.Model.transform, rig.LeftFoot, rig.LeftToes);
                AlignFoot(rig.Model.transform, rig.RightFoot, rig.RightToes);
                Snapshot target = rig.Snapshot();
                Dictionary<string, float> edited = new Dictionary<string, float>(input);
                for (int m = 0; m < Muscles.Length; m++) edited[Muscles[m]] = target.Pose.muscles[m];
                rig.Sample(edited, false);
                Snapshot replay = rig.Snapshot();
                csv.AppendLine(kind + "," + string.Join(",", new[] { phase, input["RootT.x"], input["RootT.z"],
                    Mathf.DeltaAngle(0f, Rotation(input, "RootQ").eulerAngles.y), before.HipsLocalX, unchanged.HipsLocalX, replay.HipsLocalX,
                    before.FootYaw[0], before.FootYaw[1], target.FootYaw[0], target.FootYaw[1], replay.FootYaw[0], replay.FootYaw[1],
                    before.BonePositions.Select((p,n) => Vector3.Distance(p, unchanged.BonePositions[n])).Max(),
                    before.BoneRotations.Select((q,n) => Quaternion.Angle(q, unchanged.BoneRotations[n])).Max(),
                    target.BonePositions.Select((p,n) => Vector3.Distance(p, replay.BonePositions[n])).Max(),
                    target.BoneRotations.Select((q,n) => Quaternion.Angle(q, replay.BoneRotations[n])).Max(),
                    before.HipsWorldOffsetX, unchanged.HipsWorldOffsetX, replay.HipsWorldOffsetX }.Select(F)));
                File.WriteAllText(ReportFolder + "/muscle-roundtrip.csv", csv.ToString());
            }
        }
        Debug.Log("FLOW_RIG_MUSCLE_DIAGNOSTICS_READY " + ReportFolder + "/muscle-roundtrip.csv (no candidates saved)");
    }

    [MenuItem("Tools/Echo Runner/Flow Jump/Build Private Rig Fit Comparison")]
    public static void BuildCandidates() { Execute(true); }

    // Only raise a private clip's body-height curve until the actual
    // rendered skinned geometry clears the model-root ground plane. All muscle,
    // rotation, foot-goal and horizontal-translation curves remain untouched.
    [MenuItem("Tools/Echo Runner/Flow Jump/Fit Private Landing Contact")]
    public static void FitLandingContact() { FitSkinnedContact("Land", "landing"); }

    // The last airborne pose is still visible during the entry blend to Land.
    // Fit it too, so compression cannot pull the shoes below the road at contact.
    public static void FitJumpContact() { FitSkinnedContact("Jump", "jump"); }

    public static void VerifyFootOnlyChange(AnimationClip before, AnimationClip after, string kind)
    {
        var protectedBones = new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg };
        var footBones = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
            HumanBodyBones.LeftToes, HumanBodyBones.RightToes };
        float maximumLegAngle = 0f, beforeTravel = 0f, afterTravel = 0f;
        Quaternion[] previousBefore = null, previousAfter = null;
        var csv = new StringBuilder("phase,maxLegLocalAngleDelta,beforeFootTravel,afterFootTravel\n");
        using (var rig = new Rig())
        {
            Transform[] legs = protectedBones.Select(rig.Animator.GetBoneTransform).ToArray();
            Transform[] feet = footBones.Select(rig.Animator.GetBoneTransform).ToArray();
            Require(legs.Concat(feet).All(bone => bone != null), "Foot-only review requires actual leg and toe bones.");
            int steps = Mathf.CeilToInt(before.length * Rate);
            for (int i = 0; i <= steps; i++)
            {
                float phase = i / (float)steps;
                rig.SampleClip(before, phase);
                Quaternion[] baselineLegs = legs.Select(bone => bone.localRotation).ToArray();
                Quaternion[] baselineFeet = feet.Select(bone => bone.localRotation).ToArray();
                rig.SampleClip(after, phase);
                Quaternion[] candidateFeet = feet.Select(bone => bone.localRotation).ToArray();
                float legAngle = legs.Select((bone, n) => Quaternion.Angle(baselineLegs[n], bone.localRotation)).Max();
                maximumLegAngle = Mathf.Max(maximumLegAngle, legAngle);
                if (previousBefore != null)
                {
                    beforeTravel += baselineFeet.Select((rotation, n) => Quaternion.Angle(previousBefore[n], rotation)).Sum();
                    afterTravel += candidateFeet.Select((rotation, n) => Quaternion.Angle(previousAfter[n], rotation)).Sum();
                }
                previousBefore = baselineFeet; previousAfter = candidateFeet;
                csv.AppendLine(string.Join(",", new[] { phase, legAngle, beforeTravel, afterTravel }.Select(F)));
            }
        }
        Directory.CreateDirectory(ReportFolder);
        File.WriteAllText(ReportFolder + "/" + kind.ToLowerInvariant() + "-foot-only.csv", csv.ToString());
        Require(maximumLegAngle < .1f, "Foot edit changed accepted leg joints: " + F(maximumLegAngle));
        Require(afterTravel < beforeTravel * .9f, "Foot edit did not reduce actual ankle/toe rotation travel: "
            + F(beforeTravel) + " -> " + F(afterTravel));
        Debug.Log("FLOW_" + kind.ToUpperInvariant() + "_FOOT_ONLY_VERIFIED legAngleDelta=" + F(maximumLegAngle)
            + " footTravelBefore=" + F(beforeTravel) + " footTravelAfter=" + F(afterTravel));
    }

    private static void FitSkinnedContact(string kind, string reportName)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Leave Play mode first.");
        Directory.CreateDirectory(ReportFolder);
        AnimationClip source = Load(Folder + "/RunnerFlowFit" + kind + ".anim", "RunnerFlowFit" + kind);
        var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y");
        AnimationCurve original = AnimationUtility.GetEditorCurve(source, binding);
        Require(original != null && original.length > 0, "Private " + kind + " RootT.y missing.");
        AnimationClip candidate = Object.Instantiate(source);
        candidate.name = source.name;
        var report = new StringBuilder("seconds,rootYBefore,rootYAfter,rootYCorrection,soleBefore,soleAfter,measuredDerivative\n");
        try
        {
            var settings = AnimationUtility.GetAnimationClipSettings(candidate);
            settings.keepOriginalPositionY = true; settings.heightFromFeet = false;
            settings.loopBlendOrientation = true; settings.loopBlendPositionXZ = true;
            settings.loopBlendPositionY = true;
            AnimationUtility.SetAnimationClipSettings(candidate, settings);
            int steps = Mathf.CeilToInt(candidate.length * Rate);
            float[] times = Enumerable.Range(0, steps + 1).Select(i => candidate.length * i / steps)
                .Concat(original.keys.Select(k => k.time)).Distinct().OrderBy(t => t).ToArray();
            float[] originalY = times.Select(original.Evaluate).ToArray();
            float[] fittedY = (float[])originalY.Clone();
            float[] before = new float[times.Length], after = new float[times.Length], derivative = new float[times.Length];
            using (var rig = new Rig())
            {
                SetLandingHeightCurve(candidate, binding, times, fittedY);
                // Read every baseline before correcting any key; the final pass
                // checks the complete curve after all its samples have changed.
                for (int i = 0; i < times.Length; i++)
                { rig.SampleClip(candidate, times[i] / candidate.length); before[i] = rig.LowestSkinnedWorldY(); }
                for (int i = 0; i < times.Length; i++)
                {
                    if (before[i] >= .01f) continue;
                    rig.SampleClip(candidate, times[i] / candidate.length);
                    float currentSole = rig.LowestSkinnedWorldY();
                    float currentRootY = fittedY[i];
                    fittedY[i] = currentRootY + .01f;
                    SetLandingHeightCurve(candidate, binding, times, fittedY);
                    rig.SampleClip(candidate, times[i] / candidate.length);
                    derivative[i] = (rig.LowestSkinnedWorldY() - currentSole) / .01f;
                    Require(!float.IsNaN(derivative[i]) && !float.IsInfinity(derivative[i]) && derivative[i] > .0001f,
                        kind + " root-height perturbation did not produce a measurable upward displacement at " + F(times[i]));
                    fittedY[i] = currentRootY + Mathf.Max(0f, (.01f - currentSole) / derivative[i]);
                    SetLandingHeightCurve(candidate, binding, times, fittedY);
                    rig.SampleClip(candidate, times[i] / candidate.length);
                    float sole = rig.LowestSkinnedWorldY();
                    Require(sole >= .005f && sole < .04f,
                        kind + " contact solve failed at " + F(times[i]) + ": sole=" + F(sole));
                }
                for (int i = 0; i < times.Length; i++)
                {
                    rig.SampleClip(candidate, times[i] / candidate.length);
                    after[i] = rig.LowestSkinnedWorldY();
                    report.AppendLine(string.Join(",", new[] { times[i], originalY[i], fittedY[i],
                        fittedY[i] - originalY[i], before[i], after[i], derivative[i] }.Select(F)));
                    Require(fittedY[i] >= originalY[i] && after[i] >= .005f
                        && (before[i] >= .01f || after[i] < .04f),
                        "Final complete " + kind + " curve failed contact verification at " + F(times[i]) + ": sole=" + F(after[i]));
                }
            }
            Require(AnimationUtility.GetCurveBindings(candidate).Length == AnimationUtility.GetCurveBindings(source).Length,
                kind + " fit unexpectedly changed the channel count.");
            // Copy only after the whole sampled curve passes. Reuse the existing
            // asset identity so the private comparison reference remains stable.
            EditorUtility.CopySerialized(candidate, source);
            EditorUtility.SetDirty(source); AssetDatabase.SaveAssets();
            Debug.Log("FLOW_" + reportName.ToUpperInvariant() + "_CONTACT_FITTED " + AssetDatabase.GetAssetPath(source)
                + " samples=" + times.Length + " maxRootYRaise=" + F(fittedY.Select((v, i) => v - originalY[i]).Max())
                + " minSole=" + F(after.Min()));
        }
        finally
        {
            File.WriteAllText(ReportFolder + "/" + reportName + "-contact.csv", report.ToString());
            Object.DestroyImmediate(candidate);
        }
    }

    private static void SetLandingHeightCurve(AnimationClip clip, EditorCurveBinding binding, float[] times, float[] values)
    {
        var curve = new AnimationCurve(times.Select((t, i) => new Keyframe(t, values[i])).ToArray());
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static void Execute(bool writeClips)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Leave Play mode first.");
        Directory.CreateDirectory(ReportFolder);
        var report = new StringBuilder();
        report.AppendLine("Private Flow fit. All 116 channels remain Animator curves. Source and production bindings remain unchanged.");
        report.AppendLine("HumanPose muscles come from corrected actual bones. Root/Foot spaces are solved from measured finite differences.");
        report.AppendLine("Goal target preserves each source goal-to-foot offset after the bone correction. IK weights remain zero.");
        report.AppendLine("All native body/goal and bone replay checks must pass before any candidate is saved.");
        try
        {
            using (var rig = new Rig())
            {
                var run = Load("Assets/Animations/HumanMotion/HumanRunForwards.fbx", "HumanRun");
                rig.Sample(Read(run, 0f));
                float referenceX = rig.Hips.localPosition.x;
                float referenceWorldX = rig.Model.transform.InverseTransformPoint(rig.Hips.position).x;
                report.AppendLine("humanScale=" + F(rig.Animator.humanScale) + " modelScale=" + rig.Model.transform.localScale
                    + " runFirstHipsLocalX=" + F(referenceX) + " runFirstHipsModelX=" + F(referenceWorldX));
                var pending = new List<AnimationClip>();
                try
                {
                    foreach (string kind in new[] { "Jump", "Land" })
                    {
                        AnimationClip source = Load(Folder + "/RunnerFlow" + kind + ".anim", "RunnerFlow" + kind);
                        int steps = writeClips ? Mathf.CeilToInt(source.length * Rate) : 8;
                        var samples = new List<Dictionary<string, float>>();
                        var times = new List<float>();
                        var csv = new StringBuilder("seconds,hipsBeforeX,hipsAfterX,hipsReplayX,leftYawBefore,leftYawAfter,leftYawReplay,rightYawBefore,rightYawAfter,rightYawReplay,bodyPositionError,bodyAngleError,maxBonePositionError,maxBoneAngleError,leftGoalError,rightGoalError\n");
                        for (int i = 0; i <= steps; i++)
                        {
                            float t = source.length * i / steps;
                            Dictionary<string, float> input = Read(source, t);
                            rig.Sample(input);
                            Snapshot before = rig.Snapshot();
                            Vector3 hips = rig.Hips.localPosition;
                            float parentScale = rig.Hips.parent.TransformVector(Vector3.right).magnitude;
                            Require(parentScale > .0001f, "Invalid hips parent scale.");
                            hips.x = referenceX + Mathf.Clamp(hips.x - referenceX,
                                -HipsTravel / parentScale, HipsTravel / parentScale);
                            rig.Hips.localPosition = hips;
                            AlignFoot(rig.Model.transform, rig.LeftFoot, rig.LeftToes);
                            AlignFoot(rig.Model.transform, rig.RightFoot, rig.RightToes);
                            Snapshot target = rig.Snapshot();
                            // Retain the native goal's per-pose offset from the retargeted foot.
                            // A world-space bone edit applies the same displacement/rotation to its goal.
                            target.GoalPositions = new[] {
                                before.GoalPositions[0] + target.FootPositions[0] - before.FootPositions[0],
                                before.GoalPositions[1] + target.FootPositions[1] - before.FootPositions[1] };
                            target.GoalRotations = new[] {
                                target.FootRotations[0] * Quaternion.Inverse(before.FootRotations[0]) * before.GoalRotations[0],
                                target.FootRotations[1] * Quaternion.Inverse(before.FootRotations[1]) * before.GoalRotations[1] };
                            Dictionary<string, float> fitted = new Dictionary<string, float>(input);
                            for (int m = 0; m < Muscles.Length; m++) fitted[Muscles[m]] = target.Pose.muscles[m];
                            // Body rotation affects body-relative goal mappings, so solve it first.
                            SolveRotation(rig, fitted, "RootQ", s => s.Pose.bodyRotation, target.Pose.bodyRotation);
                            SolvePosition(rig, fitted, "RootT", s => s.Pose.bodyPosition, target.Pose.bodyPosition);
                            for (int foot = 0; foot < 2; foot++)
                            {
                                int index = foot;
                                string prefix = foot == 0 ? "LeftFoot" : "RightFoot";
                                SolveRotation(rig, fitted, prefix + "Q", s => s.GoalRotations[index], target.GoalRotations[index]);
                                SolvePosition(rig, fitted, prefix + "T", s => s.GoalPositions[index], target.GoalPositions[index]);
                            }
                            rig.Sample(fitted);
                            Snapshot replay = rig.Snapshot();
                            float positionError = target.BonePositions.Select((p, n) => Vector3.Distance(p, replay.BonePositions[n])).Max();
                            float angleError = target.BoneRotations.Select((q, n) => Quaternion.Angle(q, replay.BoneRotations[n])).Max();
                            float bodyError = Vector3.Distance(target.Pose.bodyPosition, replay.Pose.bodyPosition);
                            float bodyAngle = Quaternion.Angle(target.Pose.bodyRotation, replay.Pose.bodyRotation);
                            float leftGoal = Vector3.Distance(target.GoalPositions[0], replay.GoalPositions[0]);
                            float rightGoal = Vector3.Distance(target.GoalPositions[1], replay.GoalPositions[1]);
                            csv.AppendLine(string.Join(",", new[] { t, before.HipsLocalX, target.HipsLocalX, replay.HipsLocalX,
                                before.FootYaw[0], target.FootYaw[0], replay.FootYaw[0], before.FootYaw[1], target.FootYaw[1], replay.FootYaw[1],
                                bodyError, bodyAngle, positionError, angleError, leftGoal, rightGoal }.Select(F)));
                            File.WriteAllText(ReportFolder + "/" + kind + "-calibration.csv", csv.ToString());
                            if (i == 0) WriteMappingReport(report, input, before, target, fitted, replay, kind);
                            Require(bodyError < .002f && bodyAngle < .4f, kind + " t=" + F(t) + " native body mapping did not converge.");
                            Require(leftGoal < .003f && rightGoal < .003f, kind + " t=" + F(t) + " native foot goal mapping did not converge.");
                            Require(Quaternion.Angle(target.GoalRotations[0], replay.GoalRotations[0]) < .5f
                                && Quaternion.Angle(target.GoalRotations[1], replay.GoalRotations[1]) < .5f,
                                kind + " t=" + F(t) + " native foot goal rotations did not converge.");
                            Require(positionError < .025f && angleError < 6f,
                                kind + " t=" + F(t) + " HumanPose round-trip differs from edited bones: " + F(positionError) + " m, " + F(angleError) + " deg.");
                            Require(Mathf.Abs(replay.HipsLocalX - referenceX) * parentScale <= HipsTravel + .005f,
                                kind + " t=" + F(t) + " hips correction did not survive replay.");
                            Require(replay.FootYaw.All(y => Mathf.Abs(y) <= FootSplay + 2f),
                                kind + " t=" + F(t) + " foot heading correction did not survive Humanoid replay.");
                            samples.Add(fitted); times.Add(t);
                        }
                        report.AppendLine(kind + ": " + samples.Count + " samples passed native mapping and all-bone replay checks.");
                        if (writeClips) pending.Add(CreateClip(source, "RunnerFlowFit" + kind, times, samples));
                    }
                    foreach (AnimationClip clip in pending)
                    {
                        string path = Folder + "/" + clip.name + ".anim";
                        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        if (existing == null) AssetDatabase.CreateAsset(clip, path);
                        else { EditorUtility.CopySerialized(clip, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(clip); }
                    }
                    pending.Clear();
                    if (writeClips) AssetDatabase.SaveAssets();
                    report.AppendLine(writeClips ? "FLOW_RIG_FIT_CANDIDATES_READY" : "FLOW_RIG_FIT_CALIBRATION_PASSED (no files saved)");
                    Debug.Log(report.ToString());
                }
                finally { foreach (AnimationClip clip in pending) if (clip != null && !AssetDatabase.Contains(clip)) Object.DestroyImmediate(clip); }
            }
        }
        catch (Exception e) { report.AppendLine("STOPPED: " + e); throw; }
        finally { File.WriteAllText(ReportFolder + "/" + (writeClips ? "build" : "inspection") + "-report.txt", report.ToString()); }
    }

    private static void SolvePosition(Rig rig, Dictionary<string, float> values, string prefix,
        Func<Snapshot, Vector3> select, Vector3 target)
    {
        for (int iteration = 0; iteration < 3; iteration++)
        {
            rig.Sample(values); Vector3 current = select(rig.Snapshot()), error = target - current;
            if (error.magnitude < .00005f) return;
            Vector3 original = Vector(values, prefix);
            Matrix4x4 matrix = Matrix4x4.identity;
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 changed = original; changed[axis] += .02f; Set(values, prefix, changed);
                rig.Sample(values); Vector3 column = (select(rig.Snapshot()) - current) / .02f;
                matrix.SetColumn(axis, new Vector4(column.x, column.y, column.z, 0f));
            }
            Set(values, prefix, original);
            Require(Mathf.Abs(matrix.determinant) > .000001f, "Cannot observe " + prefix + " through native Animator. No output written.");
            Vector3 delta = matrix.inverse.MultiplyVector(error);
            Require(Finite(delta) && delta.magnitude < 10f, "Unstable positional calibration for " + prefix);
            Set(values, prefix, original + delta);
        }
    }

    private static void SolveRotation(Rig rig, Dictionary<string, float> values, string prefix,
        Func<Snapshot, Quaternion> select, Quaternion target)
    {
        for (int iteration = 0; iteration < 4; iteration++)
        {
            rig.Sample(values); Quaternion current = select(rig.Snapshot());
            Vector3 error = RotationVector(target * Quaternion.Inverse(current));
            if (error.magnitude < .02f) return;
            Quaternion original = Rotation(values, prefix);
            Matrix4x4 matrix = Matrix4x4.identity;
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 unit = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
                Set(values, prefix, Quaternion.AngleAxis(1f, unit) * original);
                rig.Sample(values);
                Vector3 column = RotationVector(select(rig.Snapshot()) * Quaternion.Inverse(current));
                matrix.SetColumn(axis, new Vector4(column.x, column.y, column.z, 0f));
            }
            Set(values, prefix, original);
            Require(Mathf.Abs(matrix.determinant) > .000001f, "Cannot observe " + prefix + " through native Animator. No output written.");
            Vector3 delta = matrix.inverse.MultiplyVector(error);
            Require(Finite(delta) && delta.magnitude < 185f, "Unstable rotational calibration for " + prefix);
            Set(values, prefix, Quaternion.AngleAxis(delta.magnitude, delta.normalized) * original);
        }
    }

    private sealed class Snapshot
    {
        public HumanPose Pose;
        public Vector3[] BonePositions, FootPositions, GoalPositions;
        public Quaternion[] BoneRotations, FootRotations, GoalRotations;
        public float[] FootYaw;
        public float HipsLocalX, HipsWorldOffsetX;
    }

    private sealed class Rig : IDisposable
    {
        public GameObject Model;
        public Animator Animator;
        public Transform Hips, LeftFoot, LeftToes, RightFoot, RightToes;
        private Scene previewScene;
        private AnimatorController controller;
        private AnimatorStateMachine machine;
        private AnimatorState state;
        private AnimationClip probeClip;
        private HumanPoseHandler handler;
        private RunnerFlowFitGoalProbe goals;
        private Transform[] bones;

        public Rig()
        {
            Scene source = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !source.IsValid() || !source.isLoaded;
            try
            {
                if (opened) source = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                GameObject player = source.GetRootGameObjects().Single(g => g.name == "player");
                Transform original = player.transform.Find("CharacterModel");
                Require(original != null, "Actual scene CharacterModel missing.");
                Model = Object.Instantiate(original.gameObject);
                Model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Model.transform.localScale = original.lossyScale;
                foreach (Transform node in Model.GetComponentsInChildren<Transform>(true))
                {
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject);
                    Require(missing == 0, "Missing rig script at " + node.name + " ("
                        + AnimationUtility.CalculateTransformPath(node, Model.transform) + "). Repair script imports before fitting.");
                }
                foreach (MonoBehaviour component in Model.GetComponentsInChildren<MonoBehaviour>(true)) component.enabled = false;
            }
            finally { if (opened && source.IsValid()) EditorSceneManager.CloseScene(source, true); }
            previewScene = EditorSceneManager.NewPreviewScene();
            SceneManager.MoveGameObjectToScene(Model, previewScene);
            Model.name = "FlowFitPrivateRig"; Model.SetActive(true);
            Animator = Model.GetComponent<Animator>();
            Require(Animator != null && Animator.avatar != null && Animator.avatar.isValid && Animator.avatar.isHuman, "Actual Humanoid Avatar missing.");
            controller = new AnimatorController { name = "FlowFitOnly", hideFlags = HideFlags.HideAndDontSave };
            machine = new AnimatorStateMachine { name = "FitLayer", hideFlags = HideFlags.HideAndDontSave };
            state = new AnimatorState { name = "Sample", iKOnFeet = false, writeDefaultValues = true, hideFlags = HideFlags.HideAndDontSave };
            probeClip = new AnimationClip { name = "FlowFitProbe", frameRate = Rate, hideFlags = HideFlags.HideAndDontSave };
            state.motion = probeClip;
            machine.states = new[] { new ChildAnimatorState { state = state, position = Vector3.zero } }; machine.defaultState = state;
            controller.AddLayer(new AnimatorControllerLayer { name = "Fit", defaultWeight = 1f, iKPass = true, stateMachine = machine });
            Animator.runtimeAnimatorController = controller; Animator.applyRootMotion = false;
            Animator.fireEvents = false; Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; Animator.enabled = true;
            goals = Model.AddComponent<RunnerFlowFitGoalProbe>(); goals.enabled = true;
            Animator.Rebind(); Animator.Update(0f);
            handler = new HumanPoseHandler(Animator.avatar, Animator.transform);
            Hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            LeftFoot = Animator.GetBoneTransform(HumanBodyBones.LeftFoot); LeftToes = Animator.GetBoneTransform(HumanBodyBones.LeftToes);
            RightFoot = Animator.GetBoneTransform(HumanBodyBones.RightFoot); RightToes = Animator.GetBoneTransform(HumanBodyBones.RightToes);
            Require(Hips != null && LeftFoot != null && LeftToes != null && RightFoot != null && RightToes != null, "Actual rig misses required bones.");
            bones = Enumerable.Range(0, (int)HumanBodyBones.LastBone).Select(i => Animator.GetBoneTransform((HumanBodyBones)i)).Where(b => b != null).ToArray();
        }

        public void Sample(Dictionary<string, float> values, bool requireGoals = true)
        {
            state.motion = probeClip;
            foreach (string property in Properties)
                AnimationUtility.SetEditorCurve(probeClip, EditorCurveBinding.FloatCurve("", typeof(Animator), property),
                    AnimationCurve.Constant(0f, .01f, values[property]));
            var settings = AnimationUtility.GetAnimationClipSettings(probeClip);
            settings.startTime = 0f; settings.stopTime = .01f;
            settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true; settings.heightFromFeet = false;
            AnimationUtility.SetAnimationClipSettings(probeClip, settings);
            goals.Calls = 0;
            Animator.Rebind(); Animator.Play(0, 0, .5f); Animator.Update(0f);
            Require(!requireGoals || goals.Calls > 0, "Editor Animator did not expose its native IK goal callback. Calibration unavailable; no output written.");
        }

        public void SampleClip(AnimationClip clip, float phase)
        {
            state.motion = clip;
            Animator.Rebind(); Animator.Play(0, 0, phase); Animator.Update(0f);
            AnimatorClipInfo[] active = Animator.GetCurrentAnimatorClipInfo(0);
            Require(active.Length == 1 && active[0].clip == clip,
                "Contact sampler failed to bind the actual private clip.");
        }

        public float LowestSkinnedWorldY()
        {
            float lowest = float.PositiveInfinity;
            var mesh = new Mesh();
            try
            {
                foreach (SkinnedMeshRenderer skin in Model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh == null || !skin.enabled || !skin.gameObject.activeInHierarchy) continue;
                    skin.BakeMesh(mesh);
                    foreach (Vector3 vertex in mesh.vertices)
                        lowest = Mathf.Min(lowest, skin.transform.TransformPoint(vertex).y);
                    mesh.Clear();
                }
            }
            finally { Object.DestroyImmediate(mesh); }
            Require(!float.IsNaN(lowest) && !float.IsInfinity(lowest), "No finite visible skinned geometry for landing fit.");
            return lowest;
        }

        public Snapshot Snapshot()
        {
            var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
            handler.GetHumanPose(ref pose);
            return new Snapshot { Pose = pose,
                BonePositions = bones.Select(b => b.position).ToArray(), BoneRotations = bones.Select(b => b.rotation).ToArray(),
                FootPositions = new[] { LeftFoot.position, RightFoot.position }, FootRotations = new[] { LeftFoot.rotation, RightFoot.rotation },
                GoalPositions = (Vector3[])goals.Positions.Clone(), GoalRotations = (Quaternion[])goals.Rotations.Clone(),
                FootYaw = new[] { Yaw(Model.transform, LeftFoot, LeftToes), Yaw(Model.transform, RightFoot, RightToes) },
                HipsLocalX = Hips.localPosition.x, HipsWorldOffsetX = Hips.position.x - Model.transform.position.x };
        }

        public void Dispose()
        {
            if (handler != null) handler.Dispose();
            if (Model != null) Object.DestroyImmediate(Model);
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            foreach (Object obj in new Object[] { probeClip, state, machine, controller }) if (obj != null) Object.DestroyImmediate(obj);
        }
    }

    private static AnimationClip CreateClip(AnimationClip source, string name, List<float> times, List<Dictionary<string, float>> samples)
    {
        foreach (string prefix in Rotations)
        for (int i = 1; i < samples.Count; i++)
            if (Quaternion.Dot(Rotation(samples[i - 1], prefix), Rotation(samples[i], prefix)) < 0f)
                foreach (char axis in "xyzw") samples[i][prefix + "." + axis] *= -1f;
        AnimationClip clip = Object.Instantiate(source); clip.name = name; clip.frameRate = Rate;
        foreach (string property in Properties)
        {
            var curve = new AnimationCurve(times.Select((t, i) => new Keyframe(t, samples[i][property])).ToArray());
            for (int i = 0; i < curve.length; i++)
            { AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear); }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curve);
        }
        Require(AnimationUtility.GetCurveBindings(clip).Length == 116, "Output must retain exactly 116 Animator channels.");
        return clip;
    }

    private static AnimationClip Load(string path, string name)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().SingleOrDefault(c => c.name == name);
        Require(clip != null && clip.isHumanMotion, "Missing Humanoid clip " + path + " / " + name); return clip;
    }
    private static Dictionary<string, float> Read(AnimationClip clip, float time)
    {
        var curves = AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Animator) && b.path == "")
            .ToDictionary(b => b.propertyName, b => AnimationUtility.GetEditorCurve(clip, b));
        Require(Properties.All(curves.ContainsKey), clip.name + " lacks required 116 channels.");
        return Properties.ToDictionary(p => p, p => curves[p].Evaluate(time));
    }
    private static void AlignFoot(Transform model, Transform foot, Transform toes)
    {
        float yaw = Yaw(model, foot, toes);
        if (Mathf.Abs(yaw) <= FootSplay) return;
        foot.rotation = Quaternion.AngleAxis(-yaw + Mathf.Sign(yaw) * FootSplay, Vector3.up) * foot.rotation;
    }
    private static float Yaw(Transform model, Transform foot, Transform toes)
    {
        Vector3 direction = Vector3.ProjectOnPlane(toes.position - foot.position, Vector3.up);
        if (direction.sqrMagnitude < .000001f) return 0f;
        return Vector3.SignedAngle(Vector3.ProjectOnPlane(model.forward, Vector3.up), direction, Vector3.up);
    }
    private static Vector3 RotationVector(Quaternion q)
    { q.Normalize(); q.ToAngleAxis(out float angle, out Vector3 axis); if (angle > 180f) angle -= 360f; return Mathf.Abs(angle) < .00001f ? Vector3.zero : axis * angle; }
    private static Vector3 Vector(Dictionary<string, float> d, string p) => new Vector3(d[p + ".x"], d[p + ".y"], d[p + ".z"]);
    private static Quaternion Rotation(Dictionary<string, float> d, string p) => new Quaternion(d[p + ".x"], d[p + ".y"], d[p + ".z"], d[p + ".w"]).normalized;
    private static void Set(Dictionary<string, float> d, string p, Vector3 v) { for (int i = 0; i < 3; i++) d[p + "." + "xyz"[i]] = v[i]; }
    private static void Set(Dictionary<string, float> d, string p, Quaternion q) { q.Normalize(); for (int i = 0; i < 4; i++) d[p + "." + "xyzw"[i]] = q[i]; }
    private static bool Finite(Vector3 v) => !float.IsNaN(v.sqrMagnitude) && !float.IsInfinity(v.sqrMagnitude);
    private static string F(float v) => v.ToString("R", Inv);
    private static string MuscleProperty(string name)
    {
        string[] words = name.Split(' ');
        return words.Length >= 3 && (words[0] == "Left" || words[0] == "Right") && new[] { "Thumb", "Index", "Middle", "Ring", "Little" }.Contains(words[1])
            ? words[0] + "Hand." + words[1] + "." + string.Join(" ", words.Skip(2)) : name;
    }
    private static void WriteMappingReport(StringBuilder report, Dictionary<string, float> input, Snapshot before,
        Snapshot target, Dictionary<string, float> fitted, Snapshot replay, string kind)
    {
        report.AppendLine(kind + " first pose inputRootT=" + Vector(input, "RootT") + " nativeBody=" + before.Pose.bodyPosition
            + " targetBody=" + target.Pose.bodyPosition + " fittedRootT=" + Vector(fitted, "RootT") + " replayBody=" + replay.Pose.bodyPosition);
        for (int foot = 0; foot < 2; foot++)
        {
            string prefix = foot == 0 ? "LeftFoot" : "RightFoot";
            report.AppendLine(prefix + " inputCurve=" + Vector(input, prefix + "T") + " nativeGoal=" + before.GoalPositions[foot]
                + " targetGoal=" + target.GoalPositions[foot] + " fittedCurve=" + Vector(fitted, prefix + "T") + " replayGoal=" + replay.GoalPositions[foot]);
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Flow rig fit: " + message); }
}

// Enabled only on the isolated Editor clone. Query goals during the documented IK callback.
// No IK weights are enabled; this exposes stream goals without altering the rendered skeleton.
[ExecuteAlways]
public sealed class RunnerFlowFitGoalProbe : MonoBehaviour
{
    public int Calls;
    public Vector3[] Positions = new Vector3[2];
    public Quaternion[] Rotations = new Quaternion[2];
    private void OnAnimatorIK(int layer)
    {
        Animator animator = GetComponent<Animator>();
        for (int i = 0; i < 2; i++)
        {
            AvatarIKGoal goal = i == 0 ? AvatarIKGoal.LeftFoot : AvatarIKGoal.RightFoot;
            Positions[i] = animator.GetIKPosition(goal); Rotations[i] = animator.GetIKRotation(goal);
            animator.SetIKPositionWeight(goal, 0f); animator.SetIKRotationWeight(goal, 0f);
        }
        Calls++;
    }
}
