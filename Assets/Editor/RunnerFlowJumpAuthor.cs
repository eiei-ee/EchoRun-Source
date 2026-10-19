// Private licensed comparison. Generated source motions stay in the Git-ignored private folder.
// This tool does not replace production clips or change the scene/controller.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RunnerFlowJumpAuthor
{
    public const string Source = "Assets/Animations/HumanMotion/KevinBasicPrivate/HumanM@Jump01.fbx";
    public const string RunSource = "Assets/Animations/HumanMotion/HumanRunForwards.fbx";
    public const string Output = "Assets/Animations/HumanMotion/KevinBasicPrivate/Generated";
    public static string Report => Environment.GetEnvironmentVariable("ECHORUN_JUMP_REPORT")
        ?? "TestResults/RunnerJumpRevision-20261011/FlowAuthoring";
    private const float JumpDuration = .9f, LandingDuration = .12f, SampleRate = 60f;
    private const float PhysicalJumpSeconds = .78f;
    private static readonly string[] VectorNames = { "RootT", "LeftFootT", "RightFootT" };
    private static readonly string[] RotationNames = { "RootQ", "LeftFootQ", "RightFootQ" };
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly AnimationCurve AirSourcePhase = new AnimationCurve(
        new Keyframe(0f, .10f, .90f, .90f),
        new Keyframe(.25f, .35f, .84f, .84f),
        new Keyframe(.50f, .50f, .48f, .48f),
        new Keyframe(.75f, .59f, .28f, .28f),
        new Keyframe(1f, .64f, .20f, .20f));
    // Keep the stride clock continuous, but let the jump pose own the airborne
    // silhouette. Its final derivative matches the landing recovery clock.
    private static readonly AnimationCurve AirRunPhase = new AnimationCurve(
        new Keyframe(0f, .20f, .50f, .50f),
        new Keyframe(.50f, .425f, .40f, .40f),
        new Keyframe(.80f, .56f, .60f, .60f),
        new Keyframe(1f, .75f, .25f * PhysicalJumpSeconds / LandingDuration,
            .25f * PhysicalJumpSeconds / LandingDuration));

    private sealed class Motion
    {
        public AnimationClip Clip;
        public Dictionary<string, AnimationCurve> Curves;
        public float At(string property, float seconds) => Curves[property].Evaluate(seconds);
        public Vector3 Vector(string prefix, float time) => new Vector3(At(prefix + ".x", time),
            At(prefix + ".y", time), At(prefix + ".z", time));
        public Quaternion Rotation(string prefix, float time)
        {
            var q = new Quaternion(At(prefix + ".x", time), At(prefix + ".y", time),
                At(prefix + ".z", time), At(prefix + ".w", time));
            Require(Quaternion.Dot(q, q) > .001f, Clip.name + " has invalid " + prefix);
            return q.normalized;
        }
    }

    private sealed class Pose
    {
        public float[] Muscles;
        public Vector3[] Vectors;
        public Quaternion[] Rotations;
    }

    [MenuItem("Tools/Echo Runner/Flow Jump/Build Private Comparison")]
    public static void BuildCandidates()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Leave Play mode first.");
        Motion jump = Load(Source, "HumanM@Jump01"), run = Load(RunSource, "HumanRun");
        string[] muscles = HumanTrait.MuscleName.Select(ActualMuscle).ToArray();
        string[] required = muscles.Concat(VectorNames.SelectMany(n => "xyz".Select(a => n + "." + a)))
            .Concat(RotationNames.SelectMany(n => "xyzw".Select(a => n + "." + a))).ToArray();
        foreach (Motion motion in new[] { jump, run })
        foreach (string property in required)
            Require(motion.Curves.ContainsKey(property), motion.Clip.name + " lacks " + property);
        Folder(Output); Directory.CreateDirectory(Report);
        Vector3 origin = run.Vector("RootT", 0f);
        var report = new StringBuilder();
        report.AppendLine("Coherent root/foot/leg sampling from the full jump; baked arm blend keeps the running elbow carriage instead of the source's overhead arm spread.");
        report.AppendLine("Jump source phases .10/.35/.50/.59/.64 at 0/25/50/75/100%; earlier tuck and release avoid a held airborne pose.");
        report.AppendLine("Land recovers in .12 seconds with a decreasing source weight; no second deep compression pulse.");
        report.AppendLine("Run source phase .20..75 advances monotonically; final cadence matches Land at a .78 second physical jump.");
        report.AppendLine("All whole-body blending includes normalized RootQ and FootQ; FootT remains body-relative.");
        report.AppendLine("RootXZ stays at the actual running origin; physics owns all world translation.");
        Describe(jump, report); Describe(run, report);
        Build("RunnerFlowJump", JumpDuration, muscles, time =>
        {
            float phase = time / JumpDuration;
            Pose stride = Read(run, AirRunPhase.Evaluate(phase) * run.Clip.length, muscles, origin, false);
            Pose air = Read(jump, AirSourcePhase.Evaluate(phase) * jump.Clip.length, muscles, origin, false);
            return BlendRunningJump(stride, air, JumpWeight(phase), muscles);
        }, report);
        Build("RunnerFlowLand", LandingDuration, muscles, time =>
        {
            float phase = time / LandingDuration;
            Pose stride = Read(run, Mathf.Lerp(.75f, 1f, phase) * run.Clip.length, muscles, origin, false);
            Pose contact = Read(jump, Mathf.Lerp(.64f, .82f, phase) * jump.Clip.length, muscles, origin, false);
            // The source already contains contact flexion. A second weight
            // pulse exaggerated that dip and delayed the return to running.
            float weight = Mathf.Lerp(.50f, 0f, Smooth(phase));
            return BlendRunningJump(stride, contact, weight, muscles);
        }, report);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Report + "/parameters.txt", report.ToString());
        Debug.Log("FLOW_JUMP_CANDIDATE_READY " + Output);
    }

    private static float JumpWeight(float phase)
    {
        float[] times = { 0f, .22f, .45f, .70f, 1f };
        float[] weights = { .25f, .90f, .88f, .65f, .50f };
        for (int i = 1; i < times.Length; i++)
            if (phase <= times[i]) return Mathf.Lerp(weights[i - 1], weights[i],
                Smooth(Mathf.InverseLerp(times[i - 1], times[i], phase)));
        return weights[weights.Length - 1];
    }

    // Baking root rotation keeps the body's 1.8 m forward origin from orbiting
    // the model while Animator advances. Keep original source muscles/foot goals.
    public static void BuildBakedRootCandidates()
    {
        foreach (string kind in new[] { "Jump", "Land" })
        {
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(Output + "/RunnerFlow" + kind + ".anim");
            Require(source != null, "Build Flow source first.");
            var clip = Object.Instantiate(source);
            clip.name = "RunnerFlowFit" + kind;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionXZ = true;
            settings.loopBlendPositionY = true;
            if (kind == "Land")
            {
                settings.keepOriginalPositionY = false;
                settings.heightFromFeet = true;
            }
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            // The old Run source has right-side normalized twists beyond 1.6
            // and -3.3. Retarget only these two extremes with a smooth bound;
            // preserve the shared whole-body clock and all other muscles.
            BoundTwist(clip, "Right Lower Leg Twist In-Out", .30f);
            BoundTwist(clip, "Right Foot Twist In-Out", .25f);
            AnimationClip beforeFeet = Object.Instantiate(clip);
            try
            {
                QuietFootCurves(clip, kind == "Jump");
                RunnerFlowRigFit.VerifyFootOnlyChange(beforeFeet, clip, kind);
            }
            finally { Object.DestroyImmediate(beforeFeet); }
            string path = Output + "/" + clip.name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) AssetDatabase.CreateAsset(clip, path);
            else { EditorUtility.CopySerialized(clip, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(clip); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("FLOW_BAKED_ROOT_CANDIDATES_READY");
    }

    // Only ankle/toe muscles are edited. Keep every leg, root and foot-goal
    // channel, plus exact boundary poses, so the accepted leg action survives.
    private static void QuietFootCurves(AnimationClip clip, bool airborne)
    {
        foreach (string side in new[] { "Left", "Right" })
        foreach (string muscle in new[] { "Foot Up-Down", "Foot Twist In-Out", "Toes Up-Down" })
        {
            var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), side + " " + muscle);
            AnimationCurve source = AnimationUtility.GetEditorCurve(clip, binding);
            Require(source != null, "Missing foot channel " + binding.propertyName);
            float start = source.Evaluate(0f), end = source.Evaluate(clip.length);
            float relaxed = muscle == "Foot Twist In-Out" ? (start + end) * .5f : 0f;
            float[] times = source.keys.Select(key => key.time).ToArray();
            float[] values = times.Select(time =>
            {
                float phase = time / clip.length;
                float quiet = airborne
                    ? (phase < .22f ? Mathf.Lerp(start, relaxed, Smooth(phase / .22f))
                        : Mathf.Lerp(relaxed, end, Smooth((phase - .70f) / .30f)))
                    : Mathf.Lerp(start, end, Smooth(phase));
                // Retain a little authored flex instead of locking the ankle.
                return Mathf.Lerp(quiet, source.Evaluate(time), .25f);
            }).ToArray();
            Curve(clip, binding.propertyName, times, values);
        }
    }

    private static void BoundTwist(AnimationClip clip, string property, float limit)
    {
        var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), property);
        var curve = AnimationUtility.GetEditorCurve(clip, binding);
        Require(curve != null, "Missing observed twist channel " + property);
        var keys = curve.keys;
        for (int i = 0; i < keys.Length; i++) keys[i].value = limit * (float)Math.Tanh(keys[i].value / limit);
        curve.keys = keys;
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    public static void BuildFittedCandidates()
    {
        BuildCandidates();
        BuildBakedRootCandidates();
        RunnerFlowRigFit.FitJumpContact();
        RunnerFlowRigFit.FitLandingContact();
    }

    private static Pose Read(Motion source, float time, string[] muscles, Vector3 runOrigin, bool fixedY)
    {
        var pose = new Pose { Muscles = muscles.Select(p => source.At(p, time)).ToArray(),
            Vectors = VectorNames.Select(p => source.Vector(p, time)).ToArray(),
            Rotations = RotationNames.Select(p => source.Rotation(p, time)).ToArray() };
        Vector3 origin = source.Vector("RootT", 0f);
        pose.Vectors[0].x = runOrigin.x;
        pose.Vectors[0].z = runOrigin.z;
        if (fixedY) pose.Vectors[0].y = runOrigin.y;
        return pose;
    }

    private static Pose Blend(Pose a, Pose b, float weight)
    {
        return new Pose { Muscles = a.Muscles.Select((v, i) => Mathf.LerpUnclamped(v, b.Muscles[i], weight)).ToArray(),
            Vectors = a.Vectors.Select((v, i) => Vector3.LerpUnclamped(v, b.Vectors[i], weight)).ToArray(),
            Rotations = a.Rotations.Select((v, i) => Quaternion.SlerpUnclamped(v, b.Rotations[i], weight).normalized).ToArray() };
    }

    private static Pose BlendRunningJump(Pose stride, Pose jump, float weight, string[] muscles)
    {
        Pose result = Blend(stride, jump, weight);
        // These authored upper-body channels do not change the foot goals,
        // player root, or the original Run animation. Both sources still use
        // the same monotonic phase for the complete body.
        float armWeight = Mathf.Min(weight, .10f);
        for (int i = 0; i < muscles.Length; i++)
        {
            string muscle = muscles[i];
            if (muscle.Contains(" Arm ") || muscle.Contains(" Forearm ")
                || muscle.Contains(" Shoulder ") || muscle.Contains(" Hand ")
                || muscle.Contains("Hand."))
                result.Muscles[i] = Mathf.Lerp(stride.Muscles[i], jump.Muscles[i], armWeight);
            // A modest baked tuck removes the wide elbow silhouette. Fade it
            // with the body blend so the original running arm pose returns.
            if (muscle.EndsWith(" Arm Down-Up", StringComparison.Ordinal))
                result.Muscles[i] -= .20f * weight;
            if (muscle.EndsWith(" Forearm Stretch", StringComparison.Ordinal))
                result.Muscles[i] -= .12f * weight;
        }
        return result;
    }

    private static void Build(string name, float duration, string[] muscles, Func<float, Pose> sample, StringBuilder report)
    {
        int steps = Mathf.CeilToInt(duration * SampleRate);
        float[] times = Enumerable.Range(0, steps + 1).Select(i => duration * i / steps).ToArray();
        Pose[] poses = times.Select(sample).ToArray();
        for (int i = 1; i < poses.Length; i++)
        for (int r = 0; r < RotationNames.Length; r++)
            if (Quaternion.Dot(poses[i - 1].Rotations[r], poses[i].Rotations[r]) < 0f)
            {
                Quaternion q = poses[i].Rotations[r];
                poses[i].Rotations[r] = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            }
        var clip = new AnimationClip { name = name, frameRate = SampleRate, legacy = false };
        for (int i = 0; i < muscles.Length; i++)
        { int index = i; Curve(clip, muscles[i], times, poses.Select(p => p.Muscles[index]).ToArray()); }
        for (int i = 0; i < VectorNames.Length; i++)
        for (int a = 0; a < 3; a++)
        { int index = i, axis = a; Curve(clip, VectorNames[i] + "." + "xyz"[a], times, poses.Select(p => p.Vectors[index][axis]).ToArray()); }
        for (int i = 0; i < RotationNames.Length; i++)
        for (int a = 0; a < 4; a++)
        { int index = i, axis = a; Curve(clip, RotationNames[i] + "." + "xyzw"[a], times, poses.Select(p => p.Rotations[index][axis]).ToArray()); }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.startTime = 0f; settings.stopTime = duration;
        settings.loopTime = false; settings.loopBlend = false;
        settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true; settings.heightFromFeet = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        Require(AnimationUtility.GetCurveBindings(clip).Length == 116, "Expected 95 muscles + 21 root/foot channels.");
        string path = Output + "/" + name + ".anim";
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null) AssetDatabase.CreateAsset(clip, path);
        else { EditorUtility.CopySerialized(clip, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(clip); }
        var csv = new StringBuilder("seconds,RootT.x,RootT.y,RootT.z,LeftFootT.y,RightFootT.y\n");
        for (int i = 0; i < poses.Length; i++)
            csv.AppendLine(string.Join(",", new[] { times[i], poses[i].Vectors[0].x, poses[i].Vectors[0].y,
                poses[i].Vectors[0].z, poses[i].Vectors[1].y, poses[i].Vectors[2].y }.Select(F)));
        File.WriteAllText(Report + "/" + name + ".csv", csv.ToString());
        report.AppendLine(path + " duration=" + F(duration) + " samples=" + poses.Length
            + " rootY=" + F(poses.Min(p => p.Vectors[0].y)) + ".." + F(poses.Max(p => p.Vectors[0].y)));
    }

    private static void Curve(AnimationClip clip, string property, float[] times, float[] values)
    {
        Require(values.All(v => !float.IsNaN(v) && !float.IsInfinity(v)), "Nonfinite samples in " + property);
        var curve = new AnimationCurve(times.Select((t, i) => new Keyframe(t, values[i])).ToArray())
        { preWrapMode = WrapMode.ClampForever, postWrapMode = WrapMode.ClampForever };
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curve);
    }

    private static Motion Load(string path, string name)
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)
                && (c.name == name || c.name.EndsWith("|" + name, StringComparison.Ordinal))).ToArray();
        Require(clips.Length == 1 && clips[0].isHumanMotion, "Expected one actual Humanoid source " + name);
        var curves = new Dictionary<string, AnimationCurve>();
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clips[0]))
            if (binding.type == typeof(Animator) && binding.path == "")
                curves.Add(binding.propertyName, AnimationUtility.GetEditorCurve(clips[0], binding));
        return new Motion { Clip = clips[0], Curves = curves };
    }

    private static string ActualMuscle(string name)
    {
        string[] words = name.Split(' ');
        if (words.Length >= 3 && (words[0] == "Left" || words[0] == "Right")
            && new[] { "Thumb", "Index", "Middle", "Ring", "Little" }.Contains(words[1]))
            return words[0] + "Hand." + words[1] + "." + string.Join(" ", words.Skip(2));
        return name;
    }

    private static void Describe(Motion motion, StringBuilder report)
    {
        string path = AssetDatabase.GetAssetPath(motion.Clip);
        report.AppendLine("source=" + path + " clip=" + motion.Clip.name + " duration=" + F(motion.Clip.length)
            + " dependencyHash=" + AssetDatabase.GetAssetDependencyHash(path));
        foreach (float phase in new[] { 0f, .1f, .25f, .5f, .75f, 1f })
            report.AppendLine("  phase=" + F(phase) + " rootY=" + F(motion.At("RootT.y", phase * motion.Clip.length)));
    }
    private static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    private static string F(float v) => v.ToString("R", Invariant);
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Coherent jump candidate: " + message); }
}
