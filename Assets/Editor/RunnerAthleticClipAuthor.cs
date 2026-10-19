// Canonical authoring source. Only its V4 RunnerReadyIdle is used by the
// accepted runner; V3 RunnerRunningJump/RunnerRunningLand are historical candidates.
// BuildCandidates still writes all three independent clips for reconstruction,
// but does not bind them. Current Jump/Land come from RunnerFlowJumpAuthor;
// see docs/RunnerMotionDependencies.md before rebuilding the accepted motions.
// Ready support is HumanIdle .65 + Quaternius Idle .35.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RunnerAthleticClipAuthor
{
    public const string Source = "Assets/Animations/HumanMotion/Quaternius/UAL1_Standard.fbx";
    public const string OriginalIdleSource = "Assets/Animations/HumanMotion/HumanIdle.fbx";
    public const string RunOriginReferenceSource = "Assets/Animations/HumanMotion/HumanRunForwards.fbx";
    public const string Output = "Assets/Animations/HumanMotion/RunnerAthletic";
    public const string Report = "TestResults/RunnerMotion-20260926/AthleticAuthoring-Final";
    public const float NewIdleSupportWeight = .35f;
    public const float JumpDuration = .9f;
    public const float LandDuration = .16f;
    private const float Fps = 60f;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private sealed class Motion
    {
        public AnimationClip Clip;
        public readonly Dictionary<string, AnimationCurve> Curves = new Dictionary<string, AnimationCurve>();
        public EditorCurveBinding[] Bindings;

        public Motion(AnimationClip clip)
        {
            Clip = clip;
            Bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (EditorCurveBinding binding in Bindings)
            {
                if (binding.type != typeof(Animator) || binding.path != "") continue;
                Require(!Curves.ContainsKey(binding.propertyName), "Duplicate Animator channel: " + binding.propertyName);
                Curves.Add(binding.propertyName, AnimationUtility.GetEditorCurve(clip, binding));
            }
        }

        public float At(string property, float phase, Motion fallback = null)
        {
            AnimationCurve curve;
            string actual = ActualBinding(property);
            if (actual != null && Curves.TryGetValue(actual, out curve)) return curve.Evaluate(Mathf.Clamp01(phase) * Clip.length);
            if (fallback != null)
            {
                actual = fallback.ActualBinding(property);
                if (actual != null && fallback.Curves.TryGetValue(actual, out curve)) return curve.Evaluate(0f);
            }
            return 0f;
        }

        public string ActualBinding(string humanName)
        {
            if (Curves.ContainsKey(humanName)) return humanName;
            string finger = FingerBindingName(humanName);
            return finger != null && Curves.ContainsKey(finger) ? finger : null;
        }
    }

    private sealed class Sources
    {
        public Motion Idle, Jog, Start, Air, Land, OriginalIdle, RunOriginReference;
        public readonly HashSet<string> Muscles = new HashSet<string>(HumanTrait.MuscleName);
        public readonly Dictionary<string, string> MuscleBindings = new Dictionary<string, string>();
        public Motion[] All { get { return new[] { Idle, Jog, Start, Air, Land }; } }
    }

    [MenuItem("Tools/Echo Runner/Athletic Clips/Inspect Source Curves")]
    public static void InspectSources()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Leave Play mode before inspecting clips.");
        Sources sources = LoadSources();
        WriteSourceReport(sources);
        WriteIdleBlendReferenceReport(sources);
        Debug.Log("RUNNER_ATHLETIC_SOURCE_INSPECTED " + Report + "/source-curves.txt");
    }

    [MenuItem("Tools/Echo Runner/Athletic Clips/Rebuild Ready Idle And Historical Jump Land Candidates")]
    public static void BuildCandidates()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Leave Play mode before authoring clips.");
        Sources sources = LoadSources();
        WriteSourceReport(sources);
        WriteIdleBlendReferenceReport(sources);
        ValidateChannels(sources);
        ValidateOriginalIdleSupport(sources);
        EnsureAssetFolder(Output);

        // The standing source is the measured in-place body origin. A jump never
        // adds a second parabola to RootT; the gameplay capsule owns world Y.
        float baseHeight = sources.Idle.At("RootT.y", 0f);
        AnimationClip ready = MakeClip(sources, "RunnerReadyIdle", 2.5f, true,
            (property, time) => ReadyMuscle(sources, property, time / 2.5f),
            time => sources.Idle.At("RootT.y", time / 2.5f));
        PreserveReadySupport(sources, ready);
        AnimationClip jump = MakeClip(sources, "RunnerRunningJump", JumpDuration, false,
            (property, time) => JumpMuscle(sources, property, time), time => baseHeight);
        AuthorFootIk(sources, jump, false);
        AnimationClip land = MakeClip(sources, "RunnerRunningLand", LandDuration, false,
            (property, time) => LandMuscle(sources, property, time),
            time => LandHeight(sources, time));
        AuthorLandBody(sources, land);
        AuthorFootIk(sources, land, true);
        var horizontalOffsets = new Dictionary<string, Vector3>
        {
            { jump.name, AlignRunHorizontalOrigin(sources, jump) },
            { land.name, AlignRunHorizontalOrigin(sources, land) }
        };
        SaveClip(ready);
        SaveClip(jump);
        SaveClip(land);
        AssetDatabase.SaveAssets();
        WriteCandidateReport(sources, baseHeight, horizontalOffsets);
        Debug.Log("RUNNER_ATHLETIC_FINAL_CLIPS_AUTHORED " + Output);
    }

    private static Sources LoadSources()
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
        Require(clips.Length > 0, "Import the audited Quaternius in-place FBX before running this tool: " + Source);
        Func<string, Motion> take = name =>
        {
            AnimationClip[] matches = clips.Where(c => c.name == name || c.name.EndsWith("|" + name, StringComparison.Ordinal)).ToArray();
            Require(matches.Length == 1, "Expected one imported animation named " + name);
            Require(matches[0].isHumanMotion, "Expected imported Humanoid motion: " + matches[0].name);
            return new Motion(matches[0]);
        };
        var sources = new Sources { Idle = take("Idle_Loop"), Jog = take("Jog_Fwd_Loop"),
            Start = take("Jump_Start"), Air = take("Jump_Loop"), Land = take("Jump_Land") };
        AnimationClip[] originalIdle = AssetDatabase.LoadAllAssetsAtPath(OriginalIdleSource).OfType<AnimationClip>()
            .Where(c => c.name == "HumanIdle").ToArray();
        Require(originalIdle.Length == 1 && originalIdle[0].isHumanMotion,
            "Expected the original imported HumanIdle Humanoid clip at " + OriginalIdleSource);
        sources.OriginalIdle = new Motion(originalIdle[0]);
        AnimationClip[] originalRun = AssetDatabase.LoadAllAssetsAtPath(RunOriginReferenceSource).OfType<AnimationClip>()
            .Where(c => c.name == "HumanRun").ToArray();
        Require(originalRun.Length == 1 && originalRun[0].isHumanMotion,
            "Expected the production HumanRun Humanoid clip at " + RunOriginReferenceSource);
        sources.RunOriginReference = new Motion(originalRun[0]);
        foreach (string property in new[] { "RootT.x", "RootT.z" })
            Require(sources.RunOriginReference.Curves.ContainsKey(property)
                && sources.RunOriginReference.Curves[property].length > 0,
                "Production HumanRun lacks the actual origin curve " + property);
        foreach (string humanName in HumanTrait.MuscleName)
        {
            string[] observed = sources.All.Select(m => m.ActualBinding(humanName)).Where(n => n != null).Distinct().ToArray();
            Require(observed.Length <= 1, "Conflicting imported bindings for " + humanName + ": " + string.Join(", ", observed));
            if (observed.Length == 1) sources.MuscleBindings.Add(humanName, observed[0]);
        }
        return sources;
    }

    private static string FingerBindingName(string humanName)
    {
        // Verified against source-curves.txt: HumanTrait "Left Thumb 1
        // Stretched" is serialized as Animator "LeftHand.Thumb.1 Stretched".
        // This maps only the five named digits; Hand Down-Up/In-Out are wrists,
        // and LeftHandT/LeftHandQ remain IK targets rather than finger muscles.
        string[] words = humanName.Split(' ');
        if (words.Length < 3 || (words[0] != "Left" && words[0] != "Right")) return null;
        if (words[1] != "Thumb" && words[1] != "Index" && words[1] != "Middle" && words[1] != "Ring" && words[1] != "Little") return null;
        bool stretch = words.Length == 4 && (words[2] == "1" || words[2] == "2" || words[2] == "3") && words[3] == "Stretched";
        bool spread = words.Length == 3 && words[2] == "Spread";
        if (!stretch && !spread) return null;
        return words[0] + "Hand." + words[1] + "." + string.Join(" ", words.Skip(2));
    }

    private static void ValidateChannels(Sources sources)
    {
        // Names must exist both in this editor's HumanTrait table and actual
        // imported Animator curves. No Transform rotation or guessed aliases.
        string[] required = {
            "Spine Front-Back", "Chest Front-Back", "Left Arm Down-Up", "Right Arm Down-Up",
            "Left Arm Front-Back", "Right Arm Front-Back", "Left Forearm Stretch", "Right Forearm Stretch",
            "Left Upper Leg Front-Back", "Right Upper Leg Front-Back", "Left Upper Leg In-Out",
            "Right Upper Leg In-Out", "Left Lower Leg Stretch", "Right Lower Leg Stretch"
        };
        foreach (string property in required)
        {
            Require(sources.Muscles.Contains(property), "Editor HumanTrait does not contain " + property + "; inspect source-curves.txt.");
            foreach (Motion motion in sources.All)
                Require(motion.ActualBinding(property) != null, motion.Clip.name + " has no Animator muscle curve " + property + "; inspect report.");
        }
        string[] fingerNames = HumanTrait.MuscleName.Where(n => FingerBindingName(n) != null).ToArray();
        Require(fingerNames.Length == 40, "Expected 40 HumanTrait finger channels (two hands, five digits, four channels).");
        foreach (string humanName in fingerNames)
        {
            Require(sources.MuscleBindings.ContainsKey(humanName), "Missing observed finger binding for " + humanName);
            foreach (Motion motion in sources.All)
                Require(motion.ActualBinding(humanName) != null, motion.Clip.name + " is missing " + humanName + "; stop instead of silently opening fingers.");
        }
        foreach (Motion motion in sources.All)
        {
            foreach (string property in new[] { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" })
                Require(motion.Curves.ContainsKey(property), motion.Clip.name + " lacks " + property + "; do not invent an origin.");
            Require(!motion.Curves.Keys.Any(p => p.StartsWith("BodyT.", StringComparison.Ordinal) || p.StartsWith("BodyQ.", StringComparison.Ordinal)),
                "Source exposes BodyT/BodyQ in addition to RootT/RootQ. Inspect their meaning before authoring.");
        }
        foreach (Motion motion in sources.All)
        foreach (string side in new[] { "Left", "Right" })
        {
            foreach (string axis in new[] { "x", "y", "z" })
                Require(motion.Curves.ContainsKey(side + "FootT." + axis), motion.Clip.name + " must provide an actual foot IK translation: " + side + axis);
            foreach (string axis in new[] { "x", "y", "z", "w" })
                Require(motion.Curves.ContainsKey(side + "FootQ." + axis), motion.Clip.name + " must provide an actual foot IK orientation: " + side + axis);
        }
    }

    private static bool IsSupportMuscle(string humanName)
    {
        return humanName.StartsWith("Left Upper Leg ", StringComparison.Ordinal) || humanName.StartsWith("Right Upper Leg ", StringComparison.Ordinal)
            || humanName.StartsWith("Left Lower Leg ", StringComparison.Ordinal) || humanName.StartsWith("Right Lower Leg ", StringComparison.Ordinal)
            || humanName.StartsWith("Left Foot ", StringComparison.Ordinal) || humanName.StartsWith("Right Foot ", StringComparison.Ordinal)
            || humanName.StartsWith("Left Toes ", StringComparison.Ordinal) || humanName.StartsWith("Right Toes ", StringComparison.Ordinal);
    }

    private static bool IsSourceSupportBinding(Sources sources, string actual)
    {
        if (actual.StartsWith("RootT.", StringComparison.Ordinal) || actual.StartsWith("RootQ.", StringComparison.Ordinal)
            || actual.StartsWith("LeftFootT.", StringComparison.Ordinal) || actual.StartsWith("LeftFootQ.", StringComparison.Ordinal)
            || actual.StartsWith("RightFootT.", StringComparison.Ordinal) || actual.StartsWith("RightFootQ.", StringComparison.Ordinal)) return true;
        return sources.MuscleBindings.Any(c => c.Value == actual && IsSupportMuscle(c.Key));
    }

    private static void PreserveReadySupport(Sources sources, AnimationClip ready)
    {
        // Blend the WHOLE support chain in the engine's Humanoid curve space.
        // RootT is not assumed to be world metres; neither source is translated
        // to an invented floor. Both original body-origin and foot-goal curves
        // stay coupled. Retargeted sole contact still needs the real Avatar view.
        float[] times = SampleTimes(2.5f);
        foreach (KeyValuePair<string, string> channel in sources.MuscleBindings.Where(c => IsSupportMuscle(c.Key)))
        {
            string humanName = channel.Key;
            SetCurve(ready, channel.Value, times.Select(t => new Keyframe(t, Mathf.Lerp(
                sources.OriginalIdle.At(humanName, t / 2.5f), sources.Idle.At(humanName, t / 2.5f), NewIdleSupportWeight))).ToArray());
        }
        foreach (string prefix in new[] { "RootT", "LeftFootT", "RightFootT" })
        {
            Vector3[] positions = times.Select(t => Vector3.Lerp(ReadVector(sources.OriginalIdle, prefix, t / 2.5f),
                ReadVector(sources.Idle, prefix, t / 2.5f), NewIdleSupportWeight)).ToArray();
            SetVectorCurves(ready, prefix, times, positions);
        }
        foreach (string prefix in new[] { "RootQ", "LeftFootQ", "RightFootQ" })
        {
            Quaternion[] rotations = times.Select(t => Quaternion.Slerp(ReadQuaternion(sources.OriginalIdle, prefix, t / 2.5f),
                ReadQuaternion(sources.Idle, prefix, t / 2.5f), NewIdleSupportWeight)).ToArray();
            SetQuaternionCurves(ready, prefix, times, rotations);
        }
    }

    private static void ValidateOriginalIdleSupport(Sources sources)
    {
        int count = 0;
        foreach (KeyValuePair<string, AnimationCurve> channel in sources.Idle.Curves)
        {
            if (!IsSourceSupportBinding(sources, channel.Key)) continue;
            Require(sources.OriginalIdle.Curves.ContainsKey(channel.Key), "Original HumanIdle lacks support binding " + channel.Key + "; inspect V4 source report before blending.");
            count++;
        }
        Require(count == 37, "Expected all 37 original/new body, lower-limb and foot support channels.");
        Require(!sources.OriginalIdle.Curves.Keys.Any(p => p.StartsWith("BodyT.", StringComparison.Ordinal) || p.StartsWith("BodyQ.", StringComparison.Ordinal)),
            "Original idle uses an unreviewed BodyT/BodyQ layout; stop before blending reference spaces.");
    }

    private static void WriteIdleBlendReferenceReport(Sources sources)
    {
        Directory.CreateDirectory(Report);
        var report = new StringBuilder("Final ReadyIdle support comparison (selected V4); original 0.65 / Quaternius 0.35.\n");
        report.AppendLine("RootT values are raw imported Humanoid channels, not asserted to be metres. No ground offset or human-scale conversion is added.");
        report.AppendLine("All 37 support channels are blended together. Runtime Avatar foot/sole contact and transition rendering remain the acceptance gate.");
        foreach (Motion motion in new[] { sources.OriginalIdle, sources.Idle })
        {
            string path = AssetDatabase.GetAssetPath(motion.Clip);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(motion.Clip);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            report.AppendLine("\nSource=" + path + " clip=" + motion.Clip.name + " length=" + motion.Clip.length.ToString("R", Invariant));
            report.AppendLine("Humanoid=" + motion.Clip.isHumanMotion + " sourceAvatarValid=" + (avatar != null && avatar.isValid && avatar.isHuman));
            report.AppendLine("keepOriginalPositionY=" + settings.keepOriginalPositionY + " heightFromFeet=" + settings.heightFromFeet
                + " keepOriginalOrientation=" + settings.keepOriginalOrientation + " importerGlobalScale=" + (importer != null ? importer.globalScale.ToString("R", Invariant) : "unavailable"));
            foreach (KeyValuePair<string, AnimationCurve> curve in motion.Curves.Where(c => IsSourceSupportBinding(sources, c.Key)))
            {
                report.Append(curve.Key).Append(" keys=").Append(curve.Value.length);
                foreach (float phase in new[] { 0f, .25f, .5f, .75f, 1f })
                    report.Append(" p").Append(phase.ToString("0.##", Invariant)).Append("=")
                        .Append(curve.Value.Evaluate(phase * motion.Clip.length).ToString("R", Invariant));
                report.AppendLine();
            }
        }
        File.WriteAllText(Report + "/idle-blend-reference.txt", report.ToString());
    }

    private static AnimationClip MakeClip(Sources sources, string name, float duration, bool loop,
        Func<string, float, float> muscle, Func<float, float> height)
    {
        var clip = new AnimationClip { name = name, frameRate = Fps, legacy = false };
        float[] times = SampleTimes(duration);
        foreach (KeyValuePair<string, string> channel in sources.MuscleBindings)
        {
            // Evaluate by canonical HumanTrait name, serialize under the
            // observed Animator binding. Fingers are muscles, not hand IK.
            bool sourceSupport = name == "RunnerRunningLand" && IsSupportMuscle(channel.Key);
            SetCurve(clip, channel.Value, times.Select(t => new Keyframe(t, sourceSupport
                ? muscle(channel.Key, t) : Mathf.Clamp(muscle(channel.Key, t), -.95f, .95f))).ToArray());
        }
        foreach (string axis in new[] { "x", "y", "z" })
        {
            string property = "RootT." + axis;
            float original = sources.Idle.At(property, 0f);
            SetCurve(clip, property, times.Select(t => new Keyframe(t, axis == "y" ? height(t) : original)).ToArray());
        }
        // Preserve the imported model's real orientation, not an assumed
        // identity quaternion. Constant orientation cannot add path turning.
        foreach (string axis in new[] { "x", "y", "z", "w" })
        {
            string property = "RootQ." + axis;
            float original = sources.Idle.At(property, 0f);
            SetCurve(clip, property, new[] { new Keyframe(0f, original), new Keyframe(duration, original) });
        }
        clip.EnsureQuaternionContinuity();
        AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.startTime = 0f;
        settings.stopTime = duration;
        settings.loopTime = loop;
        settings.loopBlend = loop;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionXZ = true;
        settings.keepOriginalPositionY = true;
        settings.loopBlendOrientation = true;
        settings.loopBlendPositionXZ = true;
        settings.loopBlendPositionY = true;
        settings.heightFromFeet = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static float ReadyMuscle(Sources s, string property, float phase)
    {
        if (FingerBindingName(property) != null) return RelaxedFinger(s, property, phase, .58f);
        float idle = s.Idle.At(property, phase);
        if (IsSupportMuscle(property)) return idle;
        float readyRun = s.Jog.At(property, .10f, s.Idle);
        float breathing = Mathf.Sin(phase * Mathf.PI * 2f);
        if (property.Contains("Forearm Stretch")) return Mathf.Lerp(idle, readyRun, .78f);
        if (property.Contains("Arm Down-Up")) return Mathf.Lerp(idle, readyRun, .40f);
        if (property.Contains("Arm Front-Back")) return Mathf.Lerp(idle, readyRun, .30f);
        if (property.Contains("Arm Twist") || property.Contains("Forearm Twist")) return Mathf.Lerp(idle, readyRun, .45f);
        // The source already has a staggered stance. Its legs and foot IK are
        // kept intact; breathing edits are confined to the upper-body muscles.
        if (property == "Chest Front-Back") return Mathf.Lerp(idle, readyRun, .15f) + .008f * breathing;
        if (property == "Spine Front-Back") return Mathf.Lerp(idle, readyRun, .15f) + .004f * breathing;
        return idle;
    }

    private static float JumpMuscle(Sources s, string property, float time)
    {
        if (FingerBindingName(property) != null) return RelaxedFinger(s, property, 0f, .62f);
        // 0-.10: push. .10-.36: collect legs, left knee leading.
        // .36-.55: compact apex. .55-.78: open stride. .78-.90: reach down.
        float phase = time / JumpDuration;
        float runPhase = JumpRunPhase(phase);
        float run = s.Jog.At(property, runPhase, s.Idle);
        float idle = s.Idle.At(property, 0f);
        float startPhase = JumpStartPhase(phase);
        float sampled = s.Start.At(property, startPhase, s.Idle);
        float reach = SmoothRange(phase, .64f, .93f);
        sampled = Mathf.Lerp(sampled, s.Land.At(property, 0f, s.Idle), reach);
        float influence = JumpInfluence(phase);

        // No source Jump_Loop pose is used: the inspected silhouette was a
        // broad near-T arm shape. Running references keep elbows bent.
        if (property.Contains("Arm Down-Up")) return Mathf.Lerp(idle, run, .70f);
        if (property.Contains("Arm Front-Back")) return Mathf.Lerp(idle, run, .92f);
        if (property.Contains("Forearm Stretch")) return Mathf.Lerp(idle, run, .94f);
        if (property.Contains("Arm Twist") || property.Contains("Forearm Twist")) return Mathf.Lerp(idle, run, .70f);
        if (property.Contains("Shoulder")) return Mathf.Lerp(idle, run, .50f);

        // Lower frontal spread independently from sagittal hip/knee flexion.
        // The left leg follows the source collection; the right retains more
        // of the trailing running leg, making a readable fore/aft silhouette.
        if (property.Contains("Upper Leg In-Out")) return Mathf.Lerp(idle, sampled, .12f * influence);
        if (property.Contains("Upper Leg Twist")) return Mathf.Lerp(idle, run, .22f);
        if (property.Contains("Upper Leg Front-Back"))
            return Mathf.Lerp(run, sampled, influence * (property.StartsWith("Left", StringComparison.Ordinal) ? .88f : .58f));
        if (property.Contains("Lower Leg Stretch"))
            return Mathf.Lerp(run, sampled, influence * (property.StartsWith("Left", StringComparison.Ordinal) ? .90f : .62f));
        if (property.Contains("Lower Leg Twist")) return Mathf.Lerp(idle, run, .20f);
        if (property.Contains("Foot") || property.Contains("Toes")) return Mathf.Lerp(run, sampled, influence * .65f);
        if (property.Contains("Front-Back") && (property.StartsWith("Spine", StringComparison.Ordinal) || property.StartsWith("Chest", StringComparison.Ordinal)))
            return Mathf.Lerp(run, sampled, influence * .35f);
        return Mathf.Lerp(run, sampled, influence * .32f);
    }

    private static float LandMuscle(Sources s, string property, float time)
    {
        if (FingerBindingName(property) != null) return RelaxedFinger(s, property, 0f, .62f);
        float phase = time / LandDuration;
        float weight = LandWeight(phase);
        float sourcePhase = LandSourcePhase(time);
        float run = s.Jog.At(property, LandJogPhase(s, time), s.Idle);
        float land = s.Land.At(property, sourcePhase, s.Idle);
        float idle = s.Idle.At(property, 0f);
        // The support chain uses the same two poses/weight as its body origin
        // and foot IK. At the endpoint weight=0, all lower-limb muscles agree
        // with the outgoing Jog phase rather than retaining an Idle leg spread.
        if (IsSupportMuscle(property)) return Mathf.Lerp(run, land, weight);
        if (property.Contains("Arm Down-Up")) return Mathf.Lerp(idle, run, .70f);
        if (property.Contains("Arm Front-Back") || property.Contains("Forearm Stretch")) return Mathf.Lerp(run, land, weight * .18f);
        return Mathf.Lerp(run, land, weight);
    }

    private static float JumpRunPhase(float phase)
    {
        return Track(phase, 0f, .10f, .18f, .30f, .40f, .75f, .66f, .75f, 1f, .10f);
    }

    private static float JumpStartPhase(float phase)
    {
        return Track(phase, 0f, 0f, .10f, .07f, .25f, .19f, .42f, .26f, .62f, .40f, 1f, .70f);
    }

    private static float JumpInfluence(float phase)
    {
        return Track(phase, 0f, 0f, .08f, .28f, .24f, .92f, .43f, 1f, .65f, .86f, .83f, .48f, 1f, .12f);
    }

    private static float LandJogPhase(Sources s, float time)
    {
        return .10f + Mathf.Clamp(time, 0f, LandDuration) / s.Jog.Clip.length;
    }

    private static float LandSourcePhase(float time)
    {
        return Track(time / LandDuration, 0f, 0f, .3125f, .25f, 1f, .60f);
    }

    private static float RelaxedFinger(Sources s, string humanName, float idlePhase, float curlWeight)
    {
        float authored = s.Idle.At(humanName, idlePhase);
        // Source Idle/Jog use tightly closed fingers (~-.9 stretch) and some
        // thumb values below -1. Keep each digit's authored shape/spread, then
        // soften flexion to a half grip instead of setting every finger to 0.
        // Capping BEFORE scaling avoids turning the source thumb's -2.03 into
        // a fully clenched target while the other fingers relax.
        if (humanName.EndsWith("Stretched", StringComparison.Ordinal)) return Mathf.Clamp(authored, -1f, 1f) * curlWeight;
        return Mathf.Clamp(authored, -.9f, .9f) * .85f;
    }

    private static float LandHeight(Sources s, float time)
    {
        // This is the body's local running height, not the gameplay root Y.
        // Idle's .945 baseline was inconsistent with the selected Jog legs
        // (Jog at .10 is about .796), creating a hovering landing posture.
        return Mathf.Lerp(s.Jog.At("RootT.y", LandJogPhase(s, time)),
            s.Land.At("RootT.y", LandSourcePhase(time)), LandWeight(time / LandDuration));
    }

    private static void AuthorLandBody(Sources s, AnimationClip clip)
    {
        float[] times = SampleTimes(LandDuration);
        Vector3[] positions = times.Select(t => Vector3.Lerp(
            ReadVector(s.Jog, "RootT", LandJogPhase(s, t)),
            ReadVector(s.Land, "RootT", LandSourcePhase(t)), LandWeight(t / LandDuration))).ToArray();
        Quaternion[] rotations = times.Select(t => Quaternion.Slerp(
            ReadQuaternion(s.Jog, "RootQ", LandJogPhase(s, t)),
            ReadQuaternion(s.Land, "RootQ", LandSourcePhase(t)), LandWeight(t / LandDuration))).ToArray();
        SetVectorCurves(clip, "RootT", times, positions);
        SetQuaternionCurves(clip, "RootQ", times, rotations);
    }

    private static void AuthorFootIk(Sources s, AnimationClip clip, bool landing)
    {
        float[] times = SampleTimes(landing ? LandDuration : JumpDuration);
        foreach (string side in new[] { "Left", "Right" })
        {
            var positions = new Vector3[times.Length];
            var rotations = new Quaternion[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                float t = times[i];
                if (landing)
                {
                    float weight = LandWeight(t / LandDuration);
                    positions[i] = Vector3.Lerp(ReadVector(s.Jog, side + "FootT", LandJogPhase(s, t)),
                        ReadVector(s.Land, side + "FootT", LandSourcePhase(t)), weight);
                    rotations[i] = Quaternion.Slerp(ReadQuaternion(s.Jog, side + "FootQ", LandJogPhase(s, t)),
                        ReadQuaternion(s.Land, side + "FootQ", LandSourcePhase(t)), weight);
                }
                else
                {
                    // Jump's IK is disabled while airborne, but these values
                    // still participate when blending from/to a Foot IK state.
                    // Use its actual authored phase/left-right collection, not
                    // absent curves that are evaluated as zero foot targets.
                    float phase = t / JumpDuration;
                    float reach = SmoothRange(phase, .64f, .93f);
                    float weight = JumpInfluence(phase) * (side == "Left" ? .89f : .60f);
                    Vector3 airborne = Vector3.Lerp(ReadVector(s.Start, side + "FootT", JumpStartPhase(phase)),
                        ReadVector(s.Land, side + "FootT", 0f), reach);
                    Quaternion airborneRotation = Quaternion.Slerp(ReadQuaternion(s.Start, side + "FootQ", JumpStartPhase(phase)),
                        ReadQuaternion(s.Land, side + "FootQ", 0f), reach);
                    positions[i] = Vector3.Lerp(ReadVector(s.Jog, side + "FootT", JumpRunPhase(phase)), airborne, weight);
                    rotations[i] = Quaternion.Slerp(ReadQuaternion(s.Jog, side + "FootQ", JumpRunPhase(phase)), airborneRotation, weight);
                }
            }
            SetVectorCurves(clip, side + "FootT", times, positions);
            SetQuaternionCurves(clip, side + "FootQ", times, rotations);
        }
    }

    private static Vector3 ReadVector(Motion source, string prefix, float phase)
    {
        return new Vector3(source.At(prefix + ".x", phase), source.At(prefix + ".y", phase), source.At(prefix + ".z", phase));
    }

    private static Quaternion ReadQuaternion(Motion source, string prefix, float phase)
    {
        var value = new Quaternion(source.At(prefix + ".x", phase), source.At(prefix + ".y", phase),
            source.At(prefix + ".z", phase), source.At(prefix + ".w", phase));
        Require(Quaternion.Dot(value, value) > .01f, "Invalid source rotation " + source.Clip.name + ": " + prefix);
        return value.normalized;
    }

    private static void SetVectorCurves(AnimationClip clip, string prefix, float[] times, Vector3[] values)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            int component = axis;
            SetCurve(clip, prefix + "." + "xyz"[axis], times.Select((t, i) => new Keyframe(t, values[i][component])).ToArray());
        }
    }

    private static void SetQuaternionCurves(AnimationClip clip, string prefix, float[] times, Quaternion[] values)
    {
        // Pick one quaternion hemisphere before writing scalar curves, avoiding
        // interpolation through a zero quaternion when equivalent signs differ.
        for (int i = 1; i < values.Length; i++)
            if (Quaternion.Dot(values[i - 1], values[i]) < 0f)
                values[i] = new Quaternion(-values[i].x, -values[i].y, -values[i].z, -values[i].w);
        for (int axis = 0; axis < 4; axis++)
        {
            int component = axis;
            SetCurve(clip, prefix + "." + "xyzw"[axis], times.Select((t, i) => new Keyframe(t, values[i][component])).ToArray());
        }
    }

    private static float LandWeight(float phase)
    {
        return Track(phase, 0f, 0f, .3125f, .32f, .65f, .18f, 1f, 0f);
    }

    private static float Track(float x, params float[] pairs)
    {
        Require(pairs.Length >= 4 && pairs.Length % 2 == 0, "Invalid keypose track.");
        if (x <= pairs[0]) return pairs[1];
        for (int i = 2; i < pairs.Length; i += 2)
        {
            if (x > pairs[i]) continue;
            float t = Mathf.InverseLerp(pairs[i - 2], pairs[i], x);
            return Mathf.Lerp(pairs[i - 1], pairs[i + 1], t * t * (3f - 2f * t));
        }
        return pairs[pairs.Length - 1];
    }

    private static float SmoothRange(float value, float min, float max)
    {
        float t = Mathf.InverseLerp(min, max, value);
        return t * t * (3f - 2f * t);
    }

    private static float[] SampleTimes(float duration)
    {
        var times = new SortedSet<float> { 0f, duration };
        for (int frame = 1; frame < Mathf.CeilToInt(duration * Fps); frame++) times.Add(frame / Fps);
        if (Mathf.Approximately(duration, LandDuration)) times.Add(.05f);
        return times.ToArray();
    }

    private static void SetCurve(AnimationClip clip, string property, Keyframe[] keys)
    {
        foreach (Keyframe key in keys) Require(!float.IsNaN(key.value) && !float.IsInfinity(key.value), "Invalid key in " + property);
        var curve = new AnimationCurve(keys) { preWrapMode = WrapMode.ClampForever, postWrapMode = WrapMode.ClampForever };
        // Dense linear samples prevent interpolation overshoot between the
        // chosen body/IK poses and deliberately narrowed muscle limits.
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curve);
    }

    private static Vector3 AlignRunHorizontalOrigin(Sources sources, AnimationClip clip)
    {
        Require(clip.name == "RunnerRunningJump" || clip.name == "RunnerRunningLand",
            "Horizontal origin adaptation applies only to Jump/Land.");
        Vector3 offset = Vector3.zero;
        foreach (string axis in new[] { "x", "z" })
        {
            string property = "RootT." + axis;
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve("", typeof(Animator), property);
            AnimationCurve original = AnimationUtility.GetEditorCurve(clip, binding);
            Require(original != null && original.length > 0, clip.name + " lacks " + property);
            float reference = sources.RunOriginReference.Curves[property].Evaluate(0f);
            float delta = reference - original.Evaluate(0f);
            Require(!float.IsNaN(delta) && !float.IsInfinity(delta), "Invalid horizontal origin shift: " + property);
            Keyframe[] keys = original.keys;
            for (int i = 0; i < keys.Length; i++) keys[i].value += delta;
            // Translate values only; preserve times, tangents, weights and wrap.
            // FootT/Q are body-relative goals and receive no global translation.
            var shifted = new AnimationCurve(keys)
            {
                preWrapMode = original.preWrapMode,
                postWrapMode = original.postWrapMode
            };
            AnimationUtility.SetEditorCurve(clip, binding, shifted);
            AnimationCurve written = AnimationUtility.GetEditorCurve(clip, binding);
            Require(written != null && written.length == original.length, "Horizontal origin adaptation changed key count.");
            Require(Mathf.Abs(written.Evaluate(0f) - reference) < .00002f, clip.name + " origin differs from HumanRun: " + property);
            foreach (float time in SampleTimes(clip.length))
                Require(Mathf.Abs(written.Evaluate(time) - original.Evaluate(time) - delta) < .00002f,
                    clip.name + " horizontal curve lost its internal motion: " + property);
            if (axis == "x") offset.x = delta;
            else offset.z = delta;
        }
        return offset;
    }

    private static void SaveClip(AnimationClip authored)
    {
        string path = Output + "/" + authored.name + ".anim";
        Object existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing == null) AssetDatabase.CreateAsset(authored, path);
        else
        {
            Require(existing is AnimationClip, "Refusing to replace a non-animation asset: " + path);
            EditorUtility.CopySerialized(authored, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(authored);
        }
    }

    private static void EnsureAssetFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];
        Require(current == "Assets", "Animation output must remain inside Assets.");
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }

    private static void WriteSourceReport(Sources sources)
    {
        Directory.CreateDirectory(Report);
        var report = new StringBuilder("Quaternius actual imported Animator bindings; no importer changes.\n");
        report.AppendLine("Source=" + Source);
        report.AppendLine("HumanTrait muscle names=" + string.Join(" | ", HumanTrait.MuscleName));
        report.AppendLine("Observed canonical-to-Animator muscle bindings=" + sources.MuscleBindings.Count);
        foreach (KeyValuePair<string, string> channel in sources.MuscleBindings)
            report.AppendLine("MUSCLE_BINDING " + channel.Key + " => " + channel.Value);
        var observedMuscles = new HashSet<string>(sources.MuscleBindings.Values);
        foreach (Motion motion in sources.All)
        {
            report.AppendLine("\nCLIP " + motion.Clip.name + " length=" + motion.Clip.length.ToString("R", Invariant));
            foreach (EditorCurveBinding binding in motion.Bindings)
            {
                report.Append(binding.path).Append(" / ").Append(binding.type.Name).Append(" / ").Append(binding.propertyName);
                AnimationCurve curve = AnimationUtility.GetEditorCurve(motion.Clip, binding);
                report.Append(" keys=").Append(curve == null ? 0 : curve.length);
                report.Append(" muscle=").Append(observedMuscles.Contains(binding.propertyName));
                if (curve != null)
                    foreach (float phase in new[] { 0f, .10f, .25f, .50f, .75f, 1f })
                        report.Append(" p").Append(phase.ToString("0.##", Invariant)).Append("=")
                            .Append(curve.Evaluate(phase * motion.Clip.length).ToString("0.######", Invariant));
                report.AppendLine();
            }
            report.AppendLine("Root/body channels present: " + string.Join(", ", motion.Curves.Keys.Where(p =>
                p.StartsWith("Root", StringComparison.Ordinal) || p.StartsWith("Body", StringComparison.Ordinal) || p.StartsWith("Motion", StringComparison.Ordinal))));
            report.AppendLine("Other Animator channels (all authored clips retain valid foot IK; hand IK is excluded): " + string.Join(", ", motion.Curves.Keys.Where(p =>
                !observedMuscles.Contains(p) && !p.StartsWith("RootT.", StringComparison.Ordinal) && !p.StartsWith("RootQ.", StringComparison.Ordinal))));
        }
        File.WriteAllText(Report + "/source-curves.txt", report.ToString());
    }

    private static void WriteCandidateReport(Sources sources, float baseHeight, Dictionary<string, Vector3> horizontalOffsets)
    {
        var report = new StringBuilder("Final unified generation: selected V4 ReadyIdle plus V3 Jump/Land with constant RootT.x/z origin adaptation. All three independent clips generated in this invocation; runtime rendering remains a separate verification gate.\n");
        report.AppendLine("RootT.y baseline from actual Idle at t=0: " + baseHeight.ToString("R", Invariant));
        report.AppendLine("Ready support is original HumanIdle 0.65 + Quaternius Idle 0.35 across all 37 lower-limb, RootT/RootQ and FootT/Q channels. Quaternions use normalized shortest-path Slerp. Upper-body/finger functions are unchanged from V3.");
        report.AppendLine("Jump RootT.y and RootQ remain fixed to source Idle. Its foot IK is synthesized from the same takeoff/reach phases and asymmetric leg weights.");
        report.AppendLine("Land lower limbs, RootT/RootQ and both foot IK targets share JogPhase + LandSourcePhase + LandWeight. Its endpoint equals that Jog support pose after the reported RootT.x/z origin shift; no Idle height baseline or world-ground projection is imposed.");
        report.AppendLine("Horizontal reference=" + RunOriginReferenceSource + " clip=" + sources.RunOriginReference.Clip.name
            + " Evaluate(0) RootT.x=" + sources.RunOriginReference.Curves["RootT.x"].Evaluate(0f).ToString("R", Invariant)
            + " RootT.z=" + sources.RunOriginReference.Curves["RootT.z"].Evaluate(0f).ToString("R", Invariant));
        report.AppendLine("Only Jump/Land RootT.x/z values receive constant offsets; original key timing, tangents, weights, internal motion and wrap modes remain. RootT.y, FootT/Q and every other channel remain unchanged. These raw Humanoid values are not asserted to be world metres.");
        report.AppendLine("All clips have 14 nonzero foot IK target channels for transitions. No BodyT/BodyQ, hand IK targets, object references, events, or Transform curves copied.");
        report.AppendLine("All 40 verified finger-muscle bindings retained; Idle source closed grip softened to 0.58 (Ready) / 0.62 (Jump/Land), digit spread preserved at 0.85.");
        foreach (string name in new[] { "RunnerReadyIdle", "RunnerRunningJump", "RunnerRunningLand" })
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Output + "/" + name + ".anim");
            Require(clip != null, "Missing generated clip " + name);
            report.AppendLine(name + " length=" + clip.length.ToString("R", Invariant) + " human=" + clip.isHumanMotion
                + " floatBindings=" + AnimationUtility.GetCurveBindings(clip).Length);
            Vector3 horizontalOffset;
            if (horizontalOffsets.TryGetValue(name, out horizontalOffset))
            {
                foreach (string axis in new[] { "x", "z" })
                    Require(Mathf.Abs(ClipValue(clip, "RootT." + axis, 0f) - sources.RunOriginReference.Curves["RootT." + axis].Evaluate(0f)) < .00002f,
                        name + " persisted horizontal origin differs from HumanRun: " + axis);
                report.AppendLine("Constant horizontal origin offset: RootT.x=" + horizontalOffset.x.ToString("R", Invariant)
                    + " RootT.z=" + horizontalOffset.z.ToString("R", Invariant)
                    + " persistedStartX=" + ClipValue(clip, "RootT.x", 0f).ToString("R", Invariant)
                    + " persistedStartZ=" + ClipValue(clip, "RootT.z", 0f).ToString("R", Invariant));
            }
            var writtenBindings = new HashSet<string>(AnimationUtility.GetCurveBindings(clip).Where(b => b.path == "" && b.type == typeof(Animator)).Select(b => b.propertyName));
            foreach (KeyValuePair<string, string> channel in sources.MuscleBindings.Where(c => FingerBindingName(c.Key) != null))
                Require(writtenBindings.Contains(channel.Value), name + " omitted finger channel " + channel.Key + " => " + channel.Value);
            report.AppendLine("Verified finger bindings=" + sources.MuscleBindings.Count(c => FingerBindingName(c.Key) != null));
            VerifyFootTargets(clip, writtenBindings);
            report.AppendLine("Verified foot IK channels=14; position magnitudes nonzero; quaternion lengths valid throughout clip.");
            if (name == "RunnerReadyIdle")
            {
                VerifyReadyBlend(sources, clip);
                report.AppendLine("Verified 37 support channels against original 0.65 / Quaternius 0.35 at all authored sample times.");
                foreach (float phase in new[] { 0f, .25f, .5f, .75f, 1f })
                    report.AppendLine("Ready p=" + phase.ToString("R", Invariant)
                        + " originalRootY=" + sources.OriginalIdle.At("RootT.y", phase).ToString("R", Invariant)
                        + " QuaterniusRootY=" + sources.Idle.At("RootT.y", phase).ToString("R", Invariant)
                        + " mixedRootY=" + ClipValue(clip, "RootT.y", phase * 2.5f).ToString("R", Invariant));
            }
            if (name == "RunnerRunningLand")
            {
                VerifyLandEndpoint(sources, clip, horizontalOffsets[name]);
                report.AppendLine("Land endpoint support verified against Jog phase=" + LandJogPhase(sources, LandDuration).ToString("R", Invariant));
                foreach (float time in new[] { 0f, .05f, .10f, LandDuration })
                    report.AppendLine("Land t=" + time.ToString("R", Invariant) + " JogPhase=" + LandJogPhase(sources, time).ToString("R", Invariant)
                        + " weight=" + LandWeight(time / LandDuration).ToString("R", Invariant)
                        + " RootT.y=" + ClipValue(clip, "RootT.y", time).ToString("R", Invariant));
            }
            Require(clip.isHumanMotion, "Generated animation did not become Humanoid: " + name);
            string[] dependencies = AssetDatabase.GetDependencies(Output + "/" + name + ".anim", false);
            Require(!dependencies.Any(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)), "Generated clip unexpectedly depends on FBX.");
            report.AppendLine("Direct dependencies: " + string.Join(", ", dependencies));
        }
        File.WriteAllText(Report + "/authored-clips.txt", report.ToString());
    }

    private static void VerifyReadyBlend(Sources sources, AnimationClip clip)
    {
        KeyValuePair<string, string>[] limbs = sources.MuscleBindings.Where(c => IsSupportMuscle(c.Key)).ToArray();
        Require(limbs.Length == 16, "Expected 16 lower-limb muscle channels in the complete Ready support blend.");
        foreach (float time in SampleTimes(2.5f))
        {
            float phase = time / 2.5f;
            foreach (KeyValuePair<string, string> channel in limbs)
            {
                float expected = Mathf.Lerp(sources.OriginalIdle.At(channel.Key, phase), sources.Idle.At(channel.Key, phase), NewIdleSupportWeight);
                Require(Mathf.Abs(ClipValue(clip, channel.Value, time) - expected) < .00002f, "V4 support muscle blend differs: " + channel.Key);
            }
            foreach (string prefix in new[] { "RootT", "LeftFootT", "RightFootT" })
            {
                Vector3 expected = Vector3.Lerp(ReadVector(sources.OriginalIdle, prefix, phase), ReadVector(sources.Idle, prefix, phase), NewIdleSupportWeight);
                Require(Vector3.Distance(ClipVector(clip, prefix, time), expected) < .00002f, "V4 support position blend differs: " + prefix);
            }
            foreach (string prefix in new[] { "RootQ", "LeftFootQ", "RightFootQ" })
            {
                Quaternion expected = Quaternion.Slerp(ReadQuaternion(sources.OriginalIdle, prefix, phase), ReadQuaternion(sources.Idle, prefix, phase), NewIdleSupportWeight);
                Require(Quaternion.Angle(ClipQuaternion(clip, prefix, time).normalized, expected) < .1f, "V4 support orientation blend differs: " + prefix);
            }
        }
    }

    private static void VerifyFootTargets(AnimationClip clip, HashSet<string> bindings)
    {
        foreach (string side in new[] { "Left", "Right" })
        {
            foreach (string axis in new[] { "x", "y", "z" }) Require(bindings.Contains(side + "FootT." + axis), clip.name + " missing foot translation.");
            foreach (string axis in new[] { "x", "y", "z", "w" }) Require(bindings.Contains(side + "FootQ." + axis), clip.name + " missing foot orientation.");
            for (int i = 0; i <= 30; i++)
            {
                float time = clip.length * i / 30f;
                Vector3 position = ClipVector(clip, side + "FootT", time);
                Quaternion rotation = ClipQuaternion(clip, side + "FootQ", time);
                Require(position.sqrMagnitude > .01f, clip.name + " evaluates an empty foot target at " + time);
                float norm = Quaternion.Dot(rotation, rotation);
                Require(norm > .9f && norm < 1.1f, clip.name + " foot rotation is not a usable quaternion at " + time);
            }
        }
    }

    private static void VerifyLandEndpoint(Sources sources, AnimationClip clip, Vector3 horizontalOffset)
    {
        float phase = LandJogPhase(sources, LandDuration);
        foreach (KeyValuePair<string, string> channel in sources.MuscleBindings.Where(c => IsSupportMuscle(c.Key)))
            Require(Mathf.Abs(ClipValue(clip, channel.Value, LandDuration) - sources.Jog.At(channel.Key, phase)) < .00002f,
                "Land's final support muscle differs from its Jog phase: " + channel.Key);
        foreach (string prefix in new[] { "RootT", "LeftFootT", "RightFootT" })
            Require(Vector3.Distance(ClipVector(clip, prefix, LandDuration),
                ReadVector(sources.Jog, prefix, phase) + (prefix == "RootT" ? horizontalOffset : Vector3.zero)) < .00002f,
                "Land's final support position differs from its Jog phase: " + prefix);
        foreach (string prefix in new[] { "RootQ", "LeftFootQ", "RightFootQ" })
            Require(Quaternion.Angle(ClipQuaternion(clip, prefix, LandDuration).normalized, ReadQuaternion(sources.Jog, prefix, phase)) < .1f,
                "Land's final support orientation differs from its Jog phase: " + prefix);
    }

    private static float ClipValue(AnimationClip clip, string property, float time)
    {
        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property));
        Require(curve != null, clip.name + " is missing " + property);
        return curve.Evaluate(time);
    }

    private static Vector3 ClipVector(AnimationClip clip, string prefix, float time)
    {
        return new Vector3(ClipValue(clip, prefix + ".x", time), ClipValue(clip, prefix + ".y", time), ClipValue(clip, prefix + ".z", time));
    }

    private static Quaternion ClipQuaternion(AnimationClip clip, string prefix, float time)
    {
        return new Quaternion(ClipValue(clip, prefix + ".x", time), ClipValue(clip, prefix + ".y", time),
            ClipValue(clip, prefix + ".z", time), ClipValue(clip, prefix + ".w", time));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
