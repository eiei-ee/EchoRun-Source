using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RunnerAuthoredMotionTests
{
    [Test]
    public void RunPresentationPreservesTheAuthoredSupportAndRecoveryKnees()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Models/Mixamo/ExoGray/ExoGray_TPose.fbx");
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/HumanMotion/EchoRunHuman.controller");
        Assert.IsNotNull(source);
        Assert.IsNotNull(controller);

        GameObject model = Object.Instantiate(source);
        try
        {
            Animator animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            // Match the scene's Awake ordering: cache the rig reference before
            // Animator evaluates the replaceable presentation Idle clip.
            CharacterAnimator driver = model.AddComponent<CharacterAnimator>();
            driver.useHumanoidRig = true;
            typeof(CharacterAnimator).GetField("_initialized",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(driver, false);
            driver.SetExternalDriver();
            Transform leftKnee = driver.leftLowerLeg;
            Transform rightKnee = driver.rightLowerLeg;
            Assert.IsNotNull(leftKnee);
            Assert.IsNotNull(rightKnee);
            Quaternion leftBase = leftKnee.localRotation;
            Quaternion rightBase = rightKnee.localRotation;
            Vector3 rootPosition = model.transform.position;
            Vector3 rootScale = model.transform.localScale;
            Quaternion rootRotation = model.transform.rotation;
            int retainedSamples = 0;

            animator.Update(0f);
            driver.ApplyExternalMotion(false, false, Vector3.forward, 10f, 0f);
            for (int sample = 0; sample < 24; sample++)
            {
                animator.Play(Animator.StringToHash("Run"), 0, sample / 24f);
                animator.Update(0f);
                Quaternion leftAuthored = leftKnee.localRotation;
                Quaternion rightAuthored = rightKnee.localRotation;

                driver.ApplyExternalMotion(false, false, Vector3.forward, 10f, 0f);

                retainedSamples += AssertUnmodifiedWithinRetargetingBounds(
                    leftBase, leftAuthored, leftKnee.localRotation);
                retainedSamples += AssertUnmodifiedWithinRetargetingBounds(
                    rightBase, rightAuthored, rightKnee.localRotation);
                Assert.That(Vector3.Distance(rootPosition, model.transform.position),
                    Is.LessThan(0.00001f));
                Assert.That(Quaternion.Angle(rootRotation, model.transform.rotation),
                    Is.LessThan(0.0001f));
                Assert.AreEqual(rootScale, model.transform.localScale);
                Assert.IsFalse(animator.applyRootMotion);
            }

            Assert.GreaterOrEqual(retainedSamples, 24,
                "At least half the sampled source poses must be inside the " +
                "retargeting safety envelope and retain their authored bend.");
        }
        finally
        {
            Object.DestroyImmediate(model);
        }
    }

    private static int AssertUnmodifiedWithinRetargetingBounds(
        Quaternion reference, Quaternion authored, Quaternion presented)
    {
        float pitch = Mathf.DeltaAngle(0f,
            (Quaternion.Inverse(reference) * authored).eulerAngles.x);
        if (pitch < -119.9f || pitch > 4.9f) return 0;
        Assert.That(Quaternion.Angle(authored, presented), Is.LessThan(0.05f),
            "A valid source knee must retain its support/recovery motion; " +
            "presentation must not add a permanent crouch to both legs.");
        return 1;
    }
}
