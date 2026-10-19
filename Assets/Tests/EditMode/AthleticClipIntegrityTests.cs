// These asset checks cover the missing-channel / mixed-coordinate failures seen
// in rendered review. They do not substitute for inspecting movement in play.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class AthleticClipIntegrityTests
{
    private const string ControllerPath = "Assets/Animations/HumanMotion/EchoRunHuman.controller";
    private const string OriginalRunPath = "Assets/Animations/HumanMotion/HumanRunForwards.fbx";

    [TestCase("Idle")]
    [TestCase("Jump")]
    [TestCase("Land")]
    public void AthleticClipAuthorsAllFourteenFootIkChannels(string stateName)
    {
        AnimationClip clip = LoadStateClip(stateName);
        string[] properties = FootIkProperties().ToArray();
        Assert.That(properties.Length, Is.EqualTo(14));
        foreach (string property in properties)
            RequireCurve(clip, property);
    }

    [TestCase("Idle")]
    [TestCase("Jump")]
    [TestCase("Land")]
    public void AthleticClipAuthorsAllFortyActualFingerBindings(string stateName)
    {
        AnimationClip clip = LoadStateClip(stateName);
        // Actual imported Animator names, verified in the clips. HumanTrait's
        // "Left Thumb 1 Stretched" display name is not an equivalent binding.
        string[] properties = FingerProperties().ToArray();
        Assert.That(properties.Length, Is.EqualTo(40));
        foreach (string property in properties)
            RequireCurve(clip, property);
    }

    [TestCase("Jump")]
    [TestCase("Land")]
    public void RunningActionBakesRootRotationAndPositionIntoPose(string stateName)
    {
        AnimationClip clip = LoadStateClip(stateName);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        // These loopBlend fields are Unity's Bake Into Pose flags, including
        // non-looping clips. Root orientation extraction can swing the model's
        // forward body offset sideways even when RootT.x/z curves are aligned.
        Assert.That(settings.loopBlendOrientation, Is.True,
            stateName + " must bake root rotation into pose to preserve the runner's body alignment.");
        Assert.That(settings.loopBlendPositionXZ, Is.True,
            stateName + " must bake root XZ into pose; gameplay owns horizontal translation.");
        Assert.That(settings.loopBlendPositionY, Is.True,
            stateName + " must bake root Y into pose; gameplay owns the physical jump trajectory.");
    }

    [TestCase("Jump", "x")]
    [TestCase("Jump", "z")]
    [TestCase("Land", "x")]
    [TestCase("Land", "z")]
    public void RunningActionRootMeanStaysNearOriginalRunFirstFrame(string stateName, string axis)
    {
        AnimationClip action = LoadStateClip(stateName);
        AnimationClip[] candidates = AssetDatabase.LoadAllAssetsAtPath(OriginalRunPath)
            .OfType<AnimationClip>().Where(clip => clip.name == "HumanRun").ToArray();
        Assert.That(candidates.Length, Is.EqualTo(1), "Expected original imported HumanRun in " + OriginalRunPath);
        AnimationClip run = candidates[0];
        string property = "RootT." + axis;
        double actionMean = TimeMean(RequireCurve(action, property), action.length);
        double runOrigin = RequireCurve(run, property).Evaluate(0f);

        // Raw Humanoid normalized channel values, not world metres. Original Run
        // contains forward progression in RootT.z; its raw mean is not the origin
        // used by the baked running loop. Compare with the actual first-frame root.
        // ReadyIdle retains the separate menu support frame and is not compared.
        Assert.That(Math.Abs(actionMean - runOrigin), Is.LessThan(.12d),
            stateName + " (" + action.name + ") " + property + " mean=" + actionMean.ToString("R")
            + "; original HumanRun first frame=" + runOrigin.ToString("R")
            + ". Recheck the imported root-coordinate alignment before accepting transition captures.");
    }

    private static AnimationClip LoadStateClip(string stateName)
    {
        // Inspect the clip actually used by production. Checking an old named
        // asset would keep passing after the controller switches to a new clip.
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Assert.That(controller, Is.Not.Null, "Missing production controller: " + ControllerPath);
        Assert.That(controller.layers.Length, Is.GreaterThan(0), "Production controller needs a base layer.");
        AnimatorState[] states = controller.layers[0].stateMachine.states
            .Where(child => child.state.name == stateName).Select(child => child.state).ToArray();
        Assert.That(states.Length, Is.EqualTo(1), "Expected exactly one production state: " + stateName);
        AnimationClip clip = states[0].motion as AnimationClip;
        Assert.That(clip, Is.Not.Null, "Production " + stateName + " must directly bind an authored clip.");
        Assert.That(clip.isHumanMotion, Is.True, stateName + " must use Humanoid motion.");
        Assert.That(clip.length, Is.GreaterThan(0f), "Clip duration is empty: " + AssetDatabase.GetAssetPath(clip));
        return clip;
    }

    private static AnimationCurve RequireCurve(AnimationClip clip, string property)
    {
        // A Transform curve, differently cased alias or non-root Animator curve
        // must not accidentally satisfy a required Humanoid channel.
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip)
            .Where(binding => binding.type == typeof(Animator) && binding.path == string.Empty
                && binding.propertyName == property).ToArray();
        Assert.That(bindings.Length, Is.EqualTo(1), clip.name + " must author Animator." + property
            + " at the root; a missing curve can retain another state's leg/finger pose.");
        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, bindings[0]);
        Assert.That(curve, Is.Not.Null, clip.name + " has no readable curve for " + property);
        Assert.That(curve.length, Is.GreaterThan(0), clip.name + " has an empty curve for " + property);
        foreach (Keyframe key in curve.keys)
        {
            Assert.That(float.IsNaN(key.time) || float.IsInfinity(key.time)
                || float.IsNaN(key.value) || float.IsInfinity(key.value), Is.False,
                clip.name + " has a nonfinite key in " + property);
        }
        return curve;
    }

    private static IEnumerable<string> FootIkProperties()
    {
        foreach (string side in new[] { "Left", "Right" })
        {
            foreach (string axis in new[] { "x", "y", "z" }) yield return side + "FootT." + axis;
            foreach (string axis in new[] { "x", "y", "z", "w" }) yield return side + "FootQ." + axis;
        }
    }

    private static IEnumerable<string> FingerProperties()
    {
        foreach (string side in new[] { "Left", "Right" })
        foreach (string digit in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
        {
            string prefix = side + "Hand." + digit + ".";
            for (int joint = 1; joint <= 3; joint++) yield return prefix + joint + " Stretched";
            yield return prefix + "Spread";
        }
    }

    private static double TimeMean(AnimationCurve curve, float duration)
    {
        Assert.That(duration, Is.GreaterThan(0f), "Root comparison needs a nonempty clip.");
        // Uniform normalized-time integration avoids weighting dense key clusters
        // differently from sparse imported curves. Include both endpoints once.
        const int intervals = 256;
        double sum = 0d;
        for (int i = 0; i <= intervals; i++)
        {
            float value = curve.Evaluate(duration * i / intervals);
            Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False, "Nonfinite sampled root coordinate.");
            sum += value * (i == 0 || i == intervals ? .5d : 1d);
        }
        return sum / intervals;
    }
}
