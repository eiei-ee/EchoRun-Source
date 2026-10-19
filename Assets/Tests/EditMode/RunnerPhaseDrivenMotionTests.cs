using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Integration coverage for the presentation driver, using the real ExoGray
/// Humanoid. The varied Run clip is intentionally reused as a deterministic
/// non-looping Jump fixture: these tests verify phase sampling and state handoff,
/// while the final Jump/Land clip quality is checked by player-facing captures.
/// No controller or imported clip is written by this fixture.
/// </summary>
public class RunnerPhaseDrivenMotionTests
{
    private const string ModelPath =
        "Assets/Models/Mixamo/ExoGray/ExoGray_TPose.fbx";
    private const string MotionRoot = "Assets/Animations/HumanMotion/";
    private readonly List<Object> _owned = new List<Object>();
    private static readonly HumanBodyBones[] ComparedBones =
    {
        HumanBodyBones.Hips, HumanBodyBones.Spine,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
        HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
        HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
        HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot
    };

    private sealed class Rig
    {
        public GameObject Model;
        public Animator Animator;
        public CharacterAnimator Driver;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _owned.Count - 1; i >= 0; i--)
            if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
        _owned.Clear();
    }

    [Test]
    public void FeedbackSamplesActualHumanoidPoseWithoutLegacyJumpShaping()
    {
        AnimatorController controller = CreateController();
        Rig runner = CreateRig(controller, true);
        Rig reference = CreateRig(controller, false);
        Vector3 rootPosition = runner.Model.transform.position;
        Quaternion rootRotation = runner.Model.transform.rotation;
        Vector3 rootScale = runner.Model.transform.localScale;
        Quaternion[] firstPose = null;
        float largestPoseChange = 0f;

        foreach (float phase in new[] { 0.12f, 0.48f, 0.84f })
        {
            // Deliberately exercise the explicit overload before SetExternalDriver.
            // A first-frame cache reset must not lose the supplied phase.
            Jump(runner, phase);
            runner.Animator.Update(0f);
            Jump(runner, phase);
            SampleReference(reference, phase);
            AssertSamePose(runner, reference);

            Quaternion[] pose = ComparedBones.Select(bone =>
                runner.Animator.GetBoneTransform(bone).localRotation).ToArray();
            if (firstPose == null) firstPose = pose;
            else for (int i = 0; i < pose.Length; i++)
                largestPoseChange = Mathf.Max(largestPoseChange,
                    Quaternion.Angle(firstPose[i], pose[i]));
        }

        Assert.Greater(largestPoseChange, 8f,
            "Passing phases must visibly articulate the Humanoid, not freeze it.");
        Assert.That(Vector3.Distance(rootPosition, runner.Model.transform.position),
            Is.LessThan(0.00001f));
        Assert.That(Quaternion.Angle(rootRotation, runner.Model.transform.rotation),
            Is.LessThan(0.001f));
        Assert.AreEqual(rootScale, runner.Model.transform.localScale);
        Assert.IsFalse(runner.Animator.applyRootMotion);
    }

    [Test]
    public void GhostContinuesFromFeedbackPhaseWhenReplayFeedbackStops()
    {
        AnimatorController controller = CreateController();
        Rig runner = CreateRig(controller, true);
        Rig reference = CreateRig(controller, false);
        Jump(runner, 0.4f);
        runner.Animator.Update(0f);

        runner.Driver.ApplyExternalMotion(
            true, false, Vector3.forward, 10f, 0.09f);
        runner.Animator.Update(0f);
        runner.Driver.ApplyExternalMotion(
            true, false, Vector3.forward, 10f, 0f);
        SampleReference(reference, 0.5f);
        AssertSamePose(runner, reference);
    }

    [Test]
    public void OrdinaryGhostConsecutiveJumpsRestartWithoutAnObservedGroundFrame()
    {
        AnimatorController controller = CreateController();
        Rig runner = CreateRig(controller, true);
        Rig reference = CreateRig(controller, false);

        // This is UpdateGhostPose's ordinary path, not the opening-replay overload.
        // Finish the first jump without ever delivering isJumping=false.
        runner.Driver.SetExternalJumpPhase(0.96f);
        runner.Driver.ApplyExternalMotion(true, false, Vector3.forward, 10f, 0.02f);
        runner.Animator.Update(0f);
        SampleReference(reference, 0.96f);
        AssertSamePose(runner, reference);

        // AI Update can expire its timer and accept the next jump before LateUpdate.
        // The next airborne pose must use this new action's phase, not clamp at .999.
        runner.Driver.SetExternalJumpPhase(0.06f);
        runner.Driver.ApplyExternalMotion(true, false, Vector3.forward, 10f, 0.09f);
        runner.Animator.Update(0f);
        AssertTarget(runner, "Jump");
        SampleReference(reference, 0.06f);
        AssertSamePose(runner, reference);

        // The second jump still owns a normal, single landing when it ends.
        GroundFrame(runner, 0f);
        AssertTarget(runner, "Land");
    }

    [Test]
    public void ExternalJumpPhaseDoesNotChangeLegacyControllerPoses()
    {
        // Neither partial upgrade may opt into phase-driven presentation.
        foreach (bool phaseParameter in new[] { false, true })
        {
            AnimatorController controller = CreateController(phaseParameter, !phaseParameter);
            Rig runner = CreateRig(controller, true);
            Rig reference = CreateRig(controller, true);
            foreach (float dt in new[] { 0.12f, 0.15f, 0.10f })
            {
                runner.Driver.SetExternalJumpPhase(0.92f);
                runner.Driver.ApplyExternalMotion(true, false, Vector3.forward, 10f, dt);
                reference.Driver.ApplyExternalMotion(true, false, Vector3.forward, 10f, dt);
                runner.Animator.Update(dt);
                reference.Animator.Update(dt);
                // Apply the unchanged legacy shaping after Animator evaluation.
                runner.Driver.ApplyExternalMotion(true, false, Vector3.forward, 10f, 0f);
                reference.Driver.ApplyExternalMotion(true, false, Vector3.forward, 10f, 0f);
                AssertSamePose(runner, reference);
            }
            GroundFrame(runner, 0.02f);
            GroundFrame(reference, 0.02f);
            AssertTarget(runner, "Run");
            AssertSamePose(runner, reference);
        }
    }

    [Test]
    public void PlayerJumpInterpolationAdvancesAtBoth60And120Hz()
    {
        const double fixedStep = 0.02;
        const float duration = 0.9f;
        foreach (int fps in new[] { 60, 120 })
        {
            float previous = 0f;
            int advances = 0;
            for (int frame = 1; frame <= fps * 0.8f; frame++)
            {
                double renderTime = (double)frame / fps;
                double fixedTime = System.Math.Floor((renderTime + 1e-10) / fixedStep) * fixedStep;
                float phase = CharacterAnimator.ResolveInterpolatedPlayerJumpPhase(
                    (float)fixedTime / duration, duration, (float)fixedStep,
                    (float)(renderTime - fixedTime));
                if (renderTime > fixedStep)
                {
                    Assert.Greater(phase, previous,
                        fps + " Hz must not repeat the 50 Hz physics pose.");
                    if (previous > 0f)
                        Assert.That(phase - previous,
                            Is.EqualTo(1f / fps / duration).Within(0.000002f),
                            "Crossing a physics tick must not create a pose step.");
                    advances++;
                }
                previous = phase;
            }
            Assert.Greater(advances, fps / 2);
        }
    }

    [Test]
    public void PlayerJumpInterpolationClampsNewJumpsAndDoesNotExtrapolate()
    {
        Assert.That(CharacterAnimator.ResolveInterpolatedPlayerJumpPhase(
            0f, 0.9f, 0.02f, 0.019f), Is.Zero,
            "A newly accepted jump begins at zero even late in a physics step.");
        float previousStep = CharacterAnimator.ResolveInterpolatedPlayerJumpPhase(
            0.4f, 0.9f, 0.02f, 0f);
        Assert.That(CharacterAnimator.ResolveInterpolatedPlayerJumpPhase(
            0.4f, 0.9f, 0.02f, -1f), Is.EqualTo(previousStep));
        Assert.That(CharacterAnimator.ResolveInterpolatedPlayerJumpPhase(
            0.4f, 0.9f, 0.02f, 1f), Is.EqualTo(0.4f),
            "A stalled clock cannot sample ahead of the authoritative action.");
        Assert.That(CharacterAnimator.ResolveInterpolatedPlayerJumpPhase(
            0.4f, 0.9f, 0f, 0f), Is.EqualTo(0.4f));
    }

    [Test]
    public void PlayerContactKeepsTheOutgoingAirbornePoseDuringLandingEntry()
    {
        AnimatorController controller = CreateController();
        Rig runner = CreateRig(controller, true);
        Rig reference = CreateRig(controller, false);
        PlayerController player = runner.Model.AddComponent<PlayerController>();
        PropertyInfo jumping = typeof(PlayerController).GetProperty("IsJumping");
        MethodInfo apply = typeof(CharacterAnimator).GetMethod("ApplyMotion",
            BindingFlags.Instance | BindingFlags.NonPublic);
        jumping.SetValue(player, true);
        // Enter Jump, then exercise the real ordering: feedback in Update,
        // Animator evaluation, and finally the presentation driver's LateUpdate.
        runner.Driver.SetMotionFeedback(0.65f, 0f, 0f);
        apply.Invoke(runner.Driver, new object[] { true, false, Vector3.forward, 10f, 0f });
        runner.Animator.Update(0f);
        runner.Driver.SetMotionFeedback(0.84f, 0f, 0f);
        float renderedPhase = CharacterAnimator.ResolveInterpolatedPlayerJumpPhase(
            0.84f, player.jumpDuration, Time.fixedDeltaTime, Time.time - Time.fixedTime);
        runner.Animator.Update(0f);
        SampleReference(reference, renderedPhase);
        AssertSamePose(runner, reference);
        apply.Invoke(runner.Driver, new object[] { true, false, Vector3.forward, 10f, 1f / 60f });
        // Manual evaluation is required in EditMode and also catches a raw-phase
        // parameter overwrite that would become visible on the following frame.
        runner.Animator.Update(0f);
        AssertSamePose(runner, reference);

        jumping.SetValue(player, false);
        runner.Driver.SetMotionFeedback(0f, 0f, 0f);
        runner.Animator.Update(0f);
        apply.Invoke(runner.Driver, new object[] { false, false, Vector3.forward, 10f, 0f });
        AssertTarget(runner, "Land");
        AssertSamePose(runner, reference);
    }

    [Test]
    public void LandingRunsOnceThenExitsAtFixedTimeWithoutSpeedingTheBlend()
    {
        Rig runner = CreateRig(CreateController(), true);
        EnterLanding(runner);
        AssertTarget(runner, "Land");
        Assert.That(runner.Animator.speed, Is.EqualTo(1f));
        GroundFrame(runner, 0.08f, 28f);
        GroundFrame(runner, 0.03f, 28f);
        AssertTarget(runner, "Land");
        GroundFrame(runner, 0.02f, 28f);
        AssertTarget(runner, "Run");
        Assert.IsTrue(runner.Animator.IsInTransition(0));
        Assert.That(runner.Animator.GetAnimatorTransitionInfo(0).duration,
            Is.EqualTo(0.04f).Within(0.001f));
        Assert.That(runner.Animator.speed, Is.EqualTo(1f),
            "The 40 ms blend must not inherit the fast running playback speed.");

        for (int i = 0; i < 12; i++)
        {
            GroundFrame(runner, 0.03f, 28f);
            AssertTarget(runner, "Run");
        }
        Assert.That(runner.Animator.speed, Is.EqualTo(1.4f).Within(0.001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LandingCanBeInterruptedByJumpOrTheUnchangedSlide(bool slide)
    {
        Rig runner = CreateRig(CreateController(), true);
        EnterLanding(runner);
        GroundFrame(runner, 0.04f);
        if (slide)
        {
            runner.Driver.ApplyExternalMotion(
                false, true, Vector3.forward, 10f, 1f / 60f);
            AssertTarget(runner, "Slide");
            Assert.Greater(runner.Animator.speed, 1f,
                "The accepted authored Slide must retain its duration fitting.");
            runner.Driver.ApplyExternalMotion(
                false, false, Vector3.forward, 10f, 1f / 60f);
            AssertTarget(runner, "Run");
            Assert.That(runner.Animator.GetAnimatorTransitionInfo(0).duration,
                Is.EqualTo(0.12f).Within(0.001f),
                "Slide's existing transition duration must remain unchanged.");
            for (int i = 0; i < 10; i++)
            {
                GroundFrame(runner, 0.03f);
                AssertTarget(runner, "Run");
            }
        }
        else
        {
            Jump(runner, 0.08f);
            runner.Animator.Update(0f);
            AssertTarget(runner, "Jump");
            Assert.That(runner.Animator.GetAnimatorTransitionInfo(0).duration,
                Is.EqualTo(0.04f).Within(0.001f),
                "A fresh jump must promptly interrupt landing recovery.");
            runner.Animator.Update(0.1f);
            Jump(runner, 0.45f);
            runner.Animator.Update(0f);
            Jump(runner, 0.45f);
            Rig reference = CreateRig(runner.Animator.runtimeAnimatorController, false);
            SampleReference(reference, 0.45f);
            AssertSamePose(runner, reference);
            GroundFrame(runner, 0f);
            AssertTarget(runner, "Land");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ResetDoesNotCarryAnAirborneOrLandingStateIntoTheNextRun(bool landed)
    {
        Rig runner = CreateRig(CreateController(), true);
        Jump(runner, 0.9f);
        runner.Animator.Update(0f);
        if (landed) GroundFrame(runner, 0f);
        runner.Driver.ResetMotionFeedback();
        for (int i = 0; i < 10; i++)
        {
            GroundFrame(runner, 0.03f);
            AssertTarget(runner, "Run");
        }
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void APartialControllerUpgradeKeepsTheLegacyJumpToRunPath(
        bool phaseParameter, bool landingState)
    {
        Rig runner = CreateRig(CreateController(phaseParameter, landingState), true);
        Jump(runner, 0.5f);
        runner.Animator.Update(0f);
        AssertTarget(runner, "Jump");
        GroundFrame(runner, 0.016f);
        runner.Animator.Update(0f);
        AssertTarget(runner, "Run");
    }

    private AnimatorController CreateController(
        bool phaseParameter = true, bool landingState = true)
    {
        AnimationClip idle = ImportedClip("HumanIdle.fbx", "HumanIdle");
        AnimationClip run = ImportedClip("HumanRunForwards.fbx", "HumanRun");
        AnimationClip slide = ImportedClip(
            "Visvise/EchoRun_SlideLow_v1_TextMotion_TextMotion0.fbx",
            "EchoRunSlideLow_Candidate1");
        AnimationClip airFixture = Object.Instantiate(run);
        _owned.Add(airFixture);
        airFixture.name = "PhaseSamplerFixture";
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(airFixture);
        settings.loopTime = false;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(airFixture, settings);

        var controller = new AnimatorController();
        _owned.Add(controller);
        controller.AddLayer("Base Layer");
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        _owned.Add(machine);
        if (phaseParameter)
            controller.AddParameter("RunnerJumpPhase", AnimatorControllerParameterType.Float);
        AnimatorState idleState = AddState(machine, "Idle", idle, true);
        AddState(machine, "Run", run, true);
        AnimatorState jump = AddState(machine, "Jump", airFixture, false);
        jump.timeParameterActive = phaseParameter;
        if (phaseParameter) jump.timeParameter = "RunnerJumpPhase";
        AddState(machine, "Slide", slide, false);
        if (landingState)
        {
            AnimatorState land = AddState(machine, "Land", airFixture, true);
            // Only this timing fixture is compressed; the production installer
            // supplies a deliberately trimmed authored landing window.
            land.speed = airFixture.length / 0.12f;
        }
        machine.defaultState = idleState;
        return controller;
    }

    private AnimatorState AddState(
        AnimatorStateMachine machine, string name, Motion motion, bool footIK)
    {
        AnimatorState state = machine.AddState(name);
        _owned.Add(state);
        state.motion = motion;
        state.iKOnFeet = footIK;
        return state;
    }

    private static AnimationClip ImportedClip(string path, string name)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(MotionRoot + path)
            .OfType<AnimationClip>().FirstOrDefault(value => value.name == name);
        Assert.IsNotNull(clip, MotionRoot + path + ": " + name);
        Assert.IsTrue(clip.isHumanMotion);
        return clip;
    }

    private Rig CreateRig(RuntimeAnimatorController controller, bool driven)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Assert.IsNotNull(source);
        GameObject model = Object.Instantiate(source);
        _owned.Add(model);
        model.transform.SetPositionAndRotation(new Vector3(3f, 0f, 7f), Quaternion.identity);
        Animator animator = model.GetComponent<Animator>();
        Assert.IsNotNull(animator);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(0f);
        Assert.IsTrue(animator.isHuman);
        var rig = new Rig { Model = model, Animator = animator };
        if (driven)
        {
            rig.Driver = model.AddComponent<CharacterAnimator>();
            rig.Driver.useHumanoidRig = true;
            // Awake ran before the test assigned the serialized humanoid flag.
            typeof(CharacterAnimator).GetField("_initialized",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rig.Driver, false);
        }
        return rig;
    }

    private static void Jump(Rig runner, float phase)
    {
        runner.Driver.ApplyExternalMotion(
            true, false, Vector3.forward, 10f, 0f, phase, 0f, 0f);
    }

    private static void EnterLanding(Rig runner)
    {
        Jump(runner, 0.96f);
        runner.Animator.Update(0f);
        // A real PlayerFeedbackController supplies zero on the first ground
        // frame. It must not rewind the outgoing Jump during Land's entry.
        runner.Driver.SetMotionFeedback(0f, 0f, 0f);
        GroundFrame(runner, 0f);
    }

    private static void GroundFrame(Rig runner, float dt, float speed = 10f)
    {
        runner.Animator.Update(dt);
        runner.Driver.ApplyExternalMotion(
            false, false, Vector3.forward, speed, dt);
        runner.Animator.Update(0f);
    }

    private static void SampleReference(Rig reference, float phase)
    {
        reference.Animator.SetFloat("RunnerJumpPhase", phase);
        reference.Animator.Play("Jump", 0, 0f);
        reference.Animator.Update(0f);
    }

    private static void AssertTarget(Rig runner, string state)
    {
        AnimatorStateInfo target = runner.Animator.IsInTransition(0)
            ? runner.Animator.GetNextAnimatorStateInfo(0)
            : runner.Animator.GetCurrentAnimatorStateInfo(0);
        Assert.IsTrue(target.IsName(state),
            "Expected current or incoming state " + state + ".");
    }

    private static void AssertSamePose(Rig actual, Rig expected)
    {
        foreach (HumanBodyBones bone in ComparedBones)
        {
            Transform left = actual.Animator.GetBoneTransform(bone);
            Transform right = expected.Animator.GetBoneTransform(bone);
            Assert.That(Quaternion.Angle(left.localRotation, right.localRotation),
                Is.LessThan(0.15f), bone + " must preserve the sampled authored pose.");
            Assert.That(Vector3.Distance(left.localPosition, right.localPosition),
                Is.LessThan(0.0005f), bone + " must not receive old jump compression.");
        }
    }
}
