using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ActionFeedbackMotionCameraTests
{
    [Test]
    public void JumpPoseClockMovesThroughTakeoffApexDescentAndPreparation()
    {
        Vector4 takeoff = CharacterAnimator.ResolveJumpPoseWeights(0f);
        Vector4 apex = CharacterAnimator.ResolveJumpPoseWeights(0.5f);
        Vector4 descent = CharacterAnimator.ResolveJumpPoseWeights(0.72f);
        Vector4 preparation = CharacterAnimator.ResolveJumpPoseWeights(1f);

        Assert.Greater(takeoff.x, 0.99f);
        Assert.Less(takeoff.w, 0.01f);
        Assert.Greater(apex.y, 0.95f);
        Assert.Greater(descent.z, 0.95f);
        Assert.Greater(preparation.w, 0.99f);
        Assert.Less(preparation.x, 0.01f);
    }

    [Test]
    public void AuthoredEntryBlendUsesFixedSecondsAndClampsAtBothEnds()
    {
        Assert.AreEqual(0f,
            CharacterAnimator.ResolveTransitionWeight(0f, 0.08f), 0.0001f);
        Assert.AreEqual(0.5f,
            CharacterAnimator.ResolveTransitionWeight(0.04f, 0.08f), 0.0001f);
        Assert.AreEqual(1f,
            CharacterAnimator.ResolveTransitionWeight(0.08f, 0.08f), 0.0001f);
        Assert.AreEqual(1f,
            CharacterAnimator.ResolveTransitionWeight(2f, 0f), 0.0001f);
    }

    [Test]
    public void LaneLeanIsVisualOnlyClampedAndSuppressedDuringSlide()
    {
        Assert.AreEqual(-9f,
            CharacterAnimator.ResolveLaneLeanAngle(40f, false, 7f, 9f),
            0.0001f);
        Assert.AreEqual(9f,
            CharacterAnimator.ResolveLaneLeanAngle(-40f, false, 7f, 9f),
            0.0001f);
        Assert.AreEqual(0f,
            CharacterAnimator.ResolveLaneLeanAngle(40f, true, 7f, 9f),
            0.0001f);
    }

    [Test]
    public void CameraMotionOffsetPullsBackAndLowersDuringSlide()
    {
        Vector3 result = CameraFollow.ResolveMotionOffset(
            Vector3.forward, 1f, 1f,
            0.38f, 0.06f, 0.12f);

        Assert.AreEqual(0f, result.x, 0.0001f);
        Assert.AreEqual(-0.06f, result.y, 0.0001f);
        Assert.AreEqual(-0.38f, result.z, 0.0001f);
    }

    [Test]
    public void CameraFovTracksExternalOrientationBaseWithoutFeedbackDrift()
    {
        float firstBase = CameraFollow.ResolveExternallyOwnedBaseFieldOfView(
            56f, 0f, 0f, false);
        float unchangedBase = CameraFollow.ResolveExternallyOwnedBaseFieldOfView(
            59.2f, firstBase, 3.2f, true);
        float portraitBase = CameraFollow.ResolveExternallyOwnedBaseFieldOfView(
            62f, unchangedBase, 3.2f, true);

        Assert.AreEqual(56f, firstBase, 0.0001f);
        Assert.AreEqual(56f, unchangedBase, 0.0001f,
            "The camera must not absorb its own dynamic FOV offset.");
        Assert.AreEqual(62f, portraitBase, 0.0001f,
            "A new WorldStyler base must be sampled after orientation changes.");
    }

    [Test]
    public void HighCameraViewKeepsOrientationBaseAndShowsMoreTrack()
    {
        foreach (bool portrait in new[] { true, false })
        {
            Vector3 baseOffset = WorldStyler.GetCameraOffset(portrait);
            Vector3 low = CameraFollow.ResolveViewOffset(
                baseOffset, CameraViewHeight.Low);
            Vector3 high = CameraFollow.ResolveViewOffset(
                baseOffset, CameraViewHeight.High);

            Assert.AreEqual(baseOffset, low,
                "Low view must retain the current phone and desktop framing.");
            Assert.AreEqual(baseOffset.y + 1.25f, high.y, 0.0001f);
            Assert.AreEqual(baseOffset.z, high.z, 0.0001f,
                "Camera height must not change turn clearance behind the player.");
            Assert.Greater(CameraFollow.ResolveLookAhead(CameraViewHeight.High),
                CameraFollow.ResolveLookAhead(CameraViewHeight.Low));
        }
    }

    [Test]
    public void CameraPulseAndDampingAreBoundedDeterministicFunctions()
    {
        Assert.AreEqual(0f,
            CameraFollow.EvaluateDampedPulse(0f, 0.2f, 1f), 0.0001f);
        Assert.Greater(
            CameraFollow.EvaluateDampedPulse(0.25f, 0.2f, 1f), 0f);
        Assert.AreEqual(0f,
            CameraFollow.EvaluateDampedPulse(1f, 0.2f, 1f), 0.0001f);

        float damped = CameraFollow.DampVisualValue(
            0f, 1f, 7f, 1f / 60f);
        Assert.That(damped, Is.GreaterThan(0f).And.LessThan(1f));
        Assert.AreEqual(3.5f,
            CameraFollow.ResolveSpeedFieldOfViewOffset(2f, 3.5f),
            0.0001f);
    }

    [Test]
    public void CameraResetRestoresTheExternallyOwnedBaseFov()
    {
        GameObject gameObject = new GameObject("FeedbackCamera");
        try
        {
            Camera camera = gameObject.AddComponent<Camera>();
            CameraFollow follow = gameObject.AddComponent<CameraFollow>();
            SetField(follow, "_camera", camera);
            SetField(follow, "_baseFieldOfView", 56f);
            SetField(follow, "_hasBaseFieldOfView", true);
            SetField(follow, "_appliedFovOffset", 3f);
            camera.fieldOfView = 59f;

            follow.SetMotionFeedback(1f, 1f);
            follow.AddLandingPulse();
            follow.ResetMotionFeedback();

            Assert.AreEqual(56f, camera.fieldOfView, 0.0001f);
            Assert.AreEqual(0f,
                GetField<float>(follow, "_appliedFovOffset"), 0.0001f);
            Assert.IsFalse(GetField<bool>(follow, "_landingPulseActive"));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void CameraResetPreservesAnExternalOrientationFovChange()
    {
        GameObject gameObject = new GameObject("FeedbackCamera_Orientation");
        try
        {
            Camera camera = gameObject.AddComponent<Camera>();
            CameraFollow follow = gameObject.AddComponent<CameraFollow>();
            SetField(follow, "_camera", camera);
            SetField(follow, "_baseFieldOfView", 56f);
            SetField(follow, "_hasBaseFieldOfView", true);
            SetField(follow, "_appliedFovOffset", 3f);
            camera.fieldOfView = 62f;

            follow.ResetMotionFeedback();

            Assert.AreEqual(62f, camera.fieldOfView, 0.0001f,
                "Reset must not overwrite WorldStyler's new orientation base.");
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    private static void SetField<T>(object target, string name, T value)
    {
        FieldInfo field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, name);
        field.SetValue(target, value);
    }

    private static T GetField<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, name);
        return (T)field.GetValue(target);
    }
}
