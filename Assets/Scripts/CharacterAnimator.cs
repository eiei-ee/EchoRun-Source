using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    [Header("Run Cycle")]
    public float runSwingSpeed = 10f;
    public float armSwingAngle = 32f;
    public float armTuckAngle = 68f;
    public float elbowBendAngle = 72f;
    public float elbowBendVariation = 12f;
    public float legSwingAngle = 34f;
    public float kneeFlexAngle = 72f;
    public float runSpineLean = 14f;
    public float bobAmplitude = 0.045f;

    [Header("Jump Pose")]
    public float jumpArmRaiseAngle = 52f;
    public float jumpLegTuckAngle = 58f;
    public float jumpElbowBendAngle = 72f;
    public float jumpKneeBendAngle = 88f;
    public float jumpSpineLean = 14f;

    [Header("Slide Pose")]
    public float slideHipDrop = 0.86f;
    public float slideSpineLean = -40f;
    public float slideYawAngle = 28f;
    public float slideBodyRoll = -28f;
    public float slideFrontLegAngle = 82f;
    public float slideRearLegAngle = -28f;
    public float slideRearKneeBend = 105f;
    public float slideArmSweep = 48f;

    [Header("Turning")]
    public float lookRotationSpeed = 15f;

    [Header("Motion Feedback")]
    [Tooltip("Maximum visual-only torso roll while changing lanes.")]
    public float maxLaneLeanAngle = 9f;
    [Tooltip("Lateral speed that reaches the maximum visual lane lean.")]
    public float laneLeanVelocity = 7f;
    public float laneLeanResponse = 14f;
    [Tooltip("Duration of the pose-preserving authored slide transition.")]
    public float authoredTransitionDuration = 0.08f;

    [Header("Limb References (set manually or via InitFromHumanoid)")]
    public Transform leftUpperArm;
    public Transform rightUpperArm;
    public Transform leftLowerArm;
    public Transform rightLowerArm;
    public Transform leftUpperLeg;
    public Transform rightUpperLeg;
    public Transform leftLowerLeg;
    public Transform rightLowerLeg;
    public Transform leftFoot;
    public Transform rightFoot;
    public Transform leftHand;
    public Transform rightHand;
    public Transform hipsTransform;
    public Transform bodyTransform;
    public Transform chestTransform;

    [Header("Humanoid")]
    public bool useHumanoidRig;
    public bool useAuthoredAnimations = true;
    public bool useAuthoredSlide = true;
    public bool stabilizeAuthoredRun = true;

    private PlayerController _player;
    private GameManager _gm;
    private Animator _animator;
    private bool _initialized;
    private bool _externalDriver;
    private bool _slideAnimatorFrozen;
    private float _runPhase;
    private float _jumpTime;
    private float _slideTime;
    private int _activeAuthoredState;
    private bool _hasMotionFeedback;
    private float _feedbackJump01;
    private float _feedbackSlide01;
    private float _feedbackLateralVelocity;
    private float _visualLaneLean;
    private RuntimeAnimatorController _phaseMotionController;
    private bool _hasPhaseDrivenAirMotion;
    private bool _phaseJumpFeedbackFresh;
    private float _phaseRenderedPlayerJump01 = -1f;
    private bool _phaseWasJumping;
    private bool _phaseLandingActive;
    private bool _phaseLandingExit;
    private float _phaseLandingElapsed;
    private Transform[] _authoredBlendBones;
    private Vector3[] _authoredBlendPositions;
    private Quaternion[] _authoredBlendRotations;
    private float _authoredBlendElapsed;
    private bool _authoredBlendActive;

    private Vector3 _hipsBasePos;
    private Vector3 _bodyBasePos;
    private Quaternion _hipsBaseRot;
    private Quaternion _bodyBaseRot;
    private Vector3 _chestBasePos;
    private Quaternion _chestBaseRot;
    private Transform _upperChest;
    private Vector3 _upperChestBasePos;
    private Quaternion _upperChestBaseRot;
    private Quaternion _leftFootBaseRot;
    private Quaternion _rightFootBaseRot;
    private Quaternion _leftFootBaseRootRot;
    private Quaternion _rightFootBaseRootRot;
    private Vector3 _hipsBaseRootPos;
    private Quaternion _leftUpperArmBaseRot;
    private Quaternion _rightUpperArmBaseRot;
    private Quaternion _leftLowerArmBaseRot;
    private Quaternion _rightLowerArmBaseRot;
    private Quaternion _leftUpperArmBaseRootRot;
    private Quaternion _rightUpperArmBaseRootRot;
    private Quaternion _leftLowerArmBaseRootRot;
    private Quaternion _rightLowerArmBaseRootRot;
    private Quaternion _leftUpperLegBaseRot;
    private Quaternion _rightUpperLegBaseRot;
    private Quaternion _leftLowerLegBaseRot;
    private Quaternion _rightLowerLegBaseRot;
    private Transform _leftToes;
    private Transform _rightToes;

    private const float PoseLerpSpeed = 10f;
    private static readonly int IdleState = Animator.StringToHash("Idle");
    private static readonly int RunState = Animator.StringToHash("Run");
    private static readonly int JumpState = Animator.StringToHash("Jump");
    private static readonly int SlideState = Animator.StringToHash("Slide");
    private static readonly int LandState = Animator.StringToHash("Land");
    private static readonly int RunnerJumpPhase = Animator.StringToHash("RunnerJumpPhase");
    private const float AuthoredJumpEntry = 0.04f;
    private const float AuthoredLandingDuration = 0.12f;
    private const float AuthoredLandingEntry = 0.025f;
    private const float AuthoredLandingExit = 0.04f;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_initialized) return;

        _player = GetComponentInParent<PlayerController>();
        _gm = GameManager.Instance;

        if (useHumanoidRig)
        {
            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            if (_animator == null) _animator = GetComponentInParent<Animator>();
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            if (_animator != null && _animator.isHuman)
            {
                InitFromHumanoid();

                // Keep the Animator active so WebGL/WeChat continues updating the
                // skinned-mesh bone matrices. Procedural poses are written later in
                // LateUpdate, after Humanoid evaluation.
            }
        }

        PrepareSkinnedMeshes();
        PrepareAuthoredPoseBlend();

        // These fallbacks also support the current Generic EchoRunner skeleton.
        leftUpperArm = FindBone(leftUpperArm, "LeftUpperArm", "LeftArm", "Arm_Upper_L");
        rightUpperArm = FindBone(rightUpperArm, "RightUpperArm", "RightArm", "Arm_Upper_R");
        leftLowerArm = FindBone(leftLowerArm, "LeftLowerArm", "LeftForeArm", "Arm_Lower_L");
        rightLowerArm = FindBone(rightLowerArm, "RightLowerArm", "RightForeArm", "Arm_Lower_R");
        leftUpperLeg = FindBone(leftUpperLeg, "LeftUpperLeg", "LeftUpLeg", "Leg_Upper_L");
        rightUpperLeg = FindBone(rightUpperLeg, "RightUpperLeg", "RightUpLeg", "Leg_Upper_R");
        leftLowerLeg = FindBone(leftLowerLeg, "LeftLowerLeg", "LeftLeg", "Leg_Lower_L");
        rightLowerLeg = FindBone(rightLowerLeg, "RightLowerLeg", "RightLeg", "Leg_Lower_R");
        leftFoot = FindBone(leftFoot, "LeftFoot", "Foot_L");
        rightFoot = FindBone(rightFoot, "RightFoot", "Foot_R");
        _leftToes = FindBone(_leftToes, "LeftToes", "LeftToeBase", "Toes_L");
        _rightToes = FindBone(_rightToes, "RightToes", "RightToeBase", "Toes_R");
        leftHand = FindBone(leftHand, "LeftHand", "Hand_L");
        rightHand = FindBone(rightHand, "RightHand", "Hand_R");
        hipsTransform = FindBone(hipsTransform, "Hips", "Pelvis");
        bodyTransform = FindBone(bodyTransform, "Spine", "Torso");
        chestTransform = FindBone(chestTransform, "Chest", "Spine1");

        _hipsBasePos = hipsTransform != null ? hipsTransform.localPosition : Vector3.zero;
        _bodyBasePos = bodyTransform != null ? bodyTransform.localPosition : Vector3.zero;
        _hipsBaseRot = hipsTransform != null ? hipsTransform.localRotation : Quaternion.identity;
        _bodyBaseRot = bodyTransform != null ? bodyTransform.localRotation : Quaternion.identity;
        _chestBasePos = chestTransform != null ? chestTransform.localPosition : Vector3.zero;
        _chestBaseRot = chestTransform != null ? chestTransform.localRotation : Quaternion.identity;
        _upperChest = _animator != null && _animator.isHuman
            ? _animator.GetBoneTransform(HumanBodyBones.UpperChest) : null;
        _upperChestBasePos = _upperChest != null ? _upperChest.localPosition : Vector3.zero;
        _upperChestBaseRot = _upperChest != null ? _upperChest.localRotation : Quaternion.identity;
        _leftFootBaseRot = BaseRotation(leftFoot);
        _rightFootBaseRot = BaseRotation(rightFoot);
        _leftUpperArmBaseRot = BaseRotation(leftUpperArm);
        _rightUpperArmBaseRot = BaseRotation(rightUpperArm);
        _leftLowerArmBaseRot = BaseRotation(leftLowerArm);
        _rightLowerArmBaseRot = BaseRotation(rightLowerArm);
        _leftUpperLegBaseRot = BaseRotation(leftUpperLeg);
        _rightUpperLegBaseRot = BaseRotation(rightUpperLeg);
        _leftLowerLegBaseRot = BaseRotation(leftLowerLeg);
        _rightLowerLegBaseRot = BaseRotation(rightLowerLeg);

        Quaternion inverseRootRotation = Quaternion.Inverse(transform.rotation);
        _leftUpperArmBaseRootRot = RootRotation(leftUpperArm, inverseRootRotation);
        _rightUpperArmBaseRootRot = RootRotation(rightUpperArm, inverseRootRotation);
        _leftLowerArmBaseRootRot = RootRotation(leftLowerArm, inverseRootRotation);
        _rightLowerArmBaseRootRot = RootRotation(rightLowerArm, inverseRootRotation);
        _leftFootBaseRootRot = RootRotation(leftFoot, inverseRootRotation);
        _rightFootBaseRootRot = RootRotation(rightFoot, inverseRootRotation);
        _hipsBaseRootPos = RootPosition(hipsTransform);
        _initialized = true;
    }

    private void InitFromHumanoid()
    {
        leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        leftUpperLeg = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rightUpperLeg = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        leftLowerLeg = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rightLowerLeg = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
        _leftToes = _animator.GetBoneTransform(HumanBodyBones.LeftToes);
        _rightToes = _animator.GetBoneTransform(HumanBodyBones.RightToes);
        leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        hipsTransform = _animator.GetBoneTransform(HumanBodyBones.Hips);
        bodyTransform = _animator.GetBoneTransform(HumanBodyBones.Spine);
        chestTransform = _animator.GetBoneTransform(HumanBodyBones.Chest);
    }

    private void LateUpdate()
    {
        if (_externalDriver) return;
        if (_player == null) _player = GetComponentInParent<PlayerController>();
        if (_player == null) return;

        if (_gm == null) _gm = GameManager.Instance;
        if (_gm == null || _gm.State != GameState.Playing
            || _gm.IsDeathSequence)
        {
            ResetPhaseDrivenAirMotion();
            if (CanUseAuthoredAnimations())
            {
                ResumeAuthoredMotion();
                ApplyAuthoredMotion(false, 0f, true);
            }
            else
                ApplyIdlePose(Time.deltaTime);
            return;
        }

        ApplyMotion(_player.IsJumping, _player.IsSliding,
            _player.ForwardDirection, _gm.CurrentSpeed, Time.deltaTime);
    }

    private void PrepareSkinnedMeshes()
    {
        // Procedural poses are applied in LateUpdate. WebGL/WeChat can otherwise
        // reuse the matrices produced by Animator evaluation earlier in the frame,
        // leaving the rendered mesh in its bind pose while Transforms are moving.
        SkinnedMeshRenderer[] renderers =
            GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].forceMatrixRecalculationPerRender = true;
    }

    private void PrepareAuthoredPoseBlend()
    {
        _authoredBlendBones = GetComponentsInChildren<Transform>(true);
        _authoredBlendPositions = new Vector3[_authoredBlendBones.Length];
        _authoredBlendRotations = new Quaternion[_authoredBlendBones.Length];
    }

    // The AI shadow owns movement state, but reuses exactly the same pose code.
    public void SetExternalDriver()
    {
        Initialize();
        _externalDriver = true;
    }

    /// <summary>
    /// Supplies normalized presentation input without changing gameplay state.
    /// The existing player and shadow paths continue to work when this is not set.
    /// </summary>
    public void SetMotionFeedback(
        float jump01, float slide01, float lateralVelocity)
    {
        // The explicit ghost overload can supply feedback before its first
        // ApplyMotion call; discover the rig before caching this fresh phase.
        Initialize();
        _hasMotionFeedback = true;
        _feedbackJump01 = Mathf.Clamp01(jump01);
        _feedbackSlide01 = Mathf.Clamp01(slide01);
        _feedbackLateralVelocity = lateralVelocity;
        bool phaseMotion = CanUsePhaseDrivenAirMotion();
        _phaseJumpFeedbackFresh = true;
        _phaseRenderedPlayerJump01 = -1f;
        // Player feedback arrives in Update, before Animator evaluation. Do not
        // write zero on landing: the outgoing Jump pose must retain its last
        // airborne phase while Land blends in.
        if (!_externalDriver && _player != null && _player.IsJumping
            && phaseMotion)
        {
            // PlayerController renders its Rigidbody between the previous and
            // current physics steps. Sample the limbs in that same time window.
            // Cache this Update value so LateUpdate cannot restore the raw step.
            _phaseRenderedPlayerJump01 = ResolveInterpolatedPlayerJumpPhase(
                _feedbackJump01, _player.jumpDuration, Time.fixedDeltaTime,
                Time.time - Time.fixedTime);
            _animator.SetFloat(RunnerJumpPhase,
                Mathf.Min(_phaseRenderedPlayerJump01, 0.999f));
        }
    }

    public static float ResolveInterpolatedPlayerJumpPhase(
        float authoritativePhase, float duration, float fixedStep,
        float timeSinceFixedStep)
    {
        float step = Mathf.Max(0f, fixedStep);
        float elapsed = Mathf.Clamp(timeSinceFixedStep, 0f, step);
        // The authoritative phase remains untouched. Bounds handle the first
        // jump frame and suspended/clamped frame clocks without extrapolation.
        return Mathf.Clamp01(Mathf.Clamp01(authoritativePhase)
            - (step - elapsed) / Mathf.Max(0.2f, duration));
    }

    /// <summary>
    /// Supplies only the authoritative jump phase for an externally driven rig.
    /// Consecutive AI jumps can share a rendered jumping=true frame, so their
    /// visual phase cannot infer a new action solely from a boolean edge.
    /// Legacy controllers and slide/lateral feedback remain unchanged.
    /// </summary>
    public void SetExternalJumpPhase(float jump01)
    {
        Initialize();
        if (!CanUsePhaseDrivenAirMotion()) return;
        _feedbackJump01 = Mathf.Clamp01(jump01);
        _phaseJumpFeedbackFresh = true;
        _phaseRenderedPlayerJump01 = -1f;
    }

    public void ClearMotionFeedback()
    {
        ResetPhaseDrivenAirMotion();
        _hasMotionFeedback = false;
        _feedbackJump01 = 0f;
        _feedbackSlide01 = 0f;
        _feedbackLateralVelocity = 0f;
        _visualLaneLean = 0f;
        _jumpTime = 0f;
        _slideTime = 0f;
        _authoredBlendActive = false;
        if (_animator != null)
        {
            _animator.speed = 1f;
            _slideAnimatorFrozen = false;
        }
    }

    public void ResetMotionFeedback()
    {
        ClearMotionFeedback();
    }

    private void OnDisable()
    {
        ClearMotionFeedback();
    }

    public void ApplyExternalMotion(bool isJumping, bool isSliding,
        Vector3 forward, float speed, float deltaTime)
    {
        Initialize();
        _externalDriver = true;
        ApplyMotion(isJumping, isSliding, forward, speed, deltaTime);
    }

    public void ApplyExternalMotion(bool isJumping, bool isSliding,
        Vector3 forward, float speed, float deltaTime,
        float jump01, float slide01, float lateralVelocity)
    {
        SetMotionFeedback(jump01, slide01, lateralVelocity);
        ApplyExternalMotion(isJumping, isSliding, forward, speed, deltaTime);
    }

    private void ApplyMotion(bool isJumping, bool isSliding,
        Vector3 forward, float speed, float deltaTime)
    {
        RotateTowardForward(forward, deltaTime);
        _jumpTime = isJumping ? _jumpTime + deltaTime : 0f;
        _slideTime = isSliding ? _slideTime + deltaTime : 0f;
        float jumpDuration = _player != null
            ? Mathf.Max(0.2f, _player.jumpDuration)
            : 0.9f;
        float slideDuration = _player != null
            ? Mathf.Max(0.2f, _player.slideDuration)
            : 0.8f;
        float jumpPhase = _hasMotionFeedback && isJumping
            ? _feedbackJump01
            : Mathf.Clamp01(_jumpTime / jumpDuration);
        float slidePhase = _hasMotionFeedback && isSliding
            ? _feedbackSlide01
            : Mathf.Clamp01(_slideTime / slideDuration);
        float lateralVelocity = _hasMotionFeedback
            ? _feedbackLateralVelocity
            : 0f;

        if (CanUseAuthoredAnimations())
        {
            if (isSliding)
            {
                ResetPhaseDrivenAirMotion();
                if (useAuthoredSlide && _animator.HasState(0, SlideState))
                {
                    ResumeAuthoredMotion();
                    ApplyAuthoredSlideMotion(slideDuration, deltaTime);
                }
                else
                {
                    FreezeAuthoredSlideBase();
                    ApplySlidePose(slidePhase);
                }
            }
            else if (CanUsePhaseDrivenAirMotion())
            {
                ResumeAuthoredMotion();
                ApplyPhaseDrivenAirMotion(isJumping, jumpDuration, speed, deltaTime);
            }
            else if (!isJumping)
            {
                ResumeAuthoredMotion();
                ApplyAuthoredMotion(false, speed, false);
                StabilizeAuthoredRunPose();
            }
            else
            {
                ResumeAuthoredMotion();
                ApplyAuthoredMotion(true, speed, false);
                ShapeAuthoredJumpPose(jumpPhase);
            }
            ApplyVisualLaneLean(
                lateralVelocity, isSliding, true, deltaTime);
            return;
        }

        if (isJumping) ApplyJumpPose(jumpPhase, deltaTime);
        else if (isSliding) ApplySlidePose(slidePhase);
        else ApplyRunPose(speed, deltaTime);
        ApplyVisualLaneLean(
            lateralVelocity, isSliding, false, deltaTime);
    }

    private bool CanUseAuthoredAnimations()
    {
        return useAuthoredAnimations
            && _animator != null
            && _animator.runtimeAnimatorController != null;
    }

    private bool CanUsePhaseDrivenAirMotion()
    {
        if (!CanUseAuthoredAnimations()) return false;
        RuntimeAnimatorController controller = _animator.runtimeAnimatorController;
        if (_phaseMotionController != controller)
        {
            _phaseMotionController = controller;
            _hasPhaseDrivenAirMotion = false;
            ResetPhaseDrivenAirMotion();
            if (_animator.HasState(0, LandState))
            {
                foreach (AnimatorControllerParameter parameter in _animator.parameters)
                    if (parameter.nameHash == RunnerJumpPhase
                        && parameter.type == AnimatorControllerParameterType.Float)
                    {
                        _hasPhaseDrivenAirMotion = true;
                        break;
                    }
            }
        }
        return _hasPhaseDrivenAirMotion;
    }

    private void ResetPhaseDrivenAirMotion()
    {
        _phaseJumpFeedbackFresh = false;
        _phaseRenderedPlayerJump01 = -1f;
        _phaseWasJumping = false;
        _phaseLandingActive = false;
        _phaseLandingExit = false;
        _phaseLandingElapsed = 0f;
    }

    private void ApplyPhaseDrivenAirMotion(
        bool isJumping, float jumpDuration, float speed, float deltaTime)
    {
        bool freshFeedback = _phaseJumpFeedbackFresh;
        float renderedPlayerPhase = _phaseRenderedPlayerJump01;
        _phaseJumpFeedbackFresh = false;
        _phaseRenderedPlayerJump01 = -1f;
        if (isJumping)
        {
            _phaseLandingActive = false;
            _phaseLandingExit = false;
            _phaseLandingElapsed = 0f;
            _phaseWasJumping = true;
            float phase = freshFeedback ? _feedbackJump01
                : Mathf.Clamp01(_jumpTime / jumpDuration);
            if (freshFeedback && !_externalDriver && renderedPlayerPhase >= 0f)
                phase = renderedPlayerPhase;
            // A ghost can switch from explicit replay feedback to its own timer.
            // Seed that fallback from authoritative time, never the player's
            // delayed display sample; this preserves the existing external path.
            if (freshFeedback) _jumpTime = _feedbackJump01 * jumpDuration;
            _animator.SetFloat(RunnerJumpPhase, Mathf.Min(phase, 0.999f));
            ApplyAuthoredMotion(true, speed, false);
            return;
        }

        if (_phaseWasJumping)
        {
            _phaseWasJumping = false;
            _phaseLandingActive = true;
            _phaseLandingElapsed = 0f;
            // Land contains a short fitted contact/recovery window. Keeping the
            // Animator at 1 preserves the fixed-second entry/exit blend windows.
            _animator.speed = 1f;
            _animator.CrossFadeInFixedTime(LandState, AuthoredLandingEntry, 0, 0f);
            _animator.Update(0f);
            _activeAuthoredState = LandState;
            return;
        }

        if (_phaseLandingActive)
        {
            _phaseLandingElapsed += Mathf.Max(0f, deltaTime);
            _animator.speed = 1f;
            if (_phaseLandingElapsed < AuthoredLandingDuration) return;
            _phaseLandingActive = false;
            _phaseLandingExit = true;
            _animator.CrossFadeInFixedTime(RunState, AuthoredLandingExit, 0, 0f);
            _animator.Update(0f);
            _activeAuthoredState = RunState;
        }

        ApplyAuthoredMotion(false, speed, false);
        if (_phaseLandingExit)
        {
            if (_animator.IsInTransition(0))
            {
                _animator.speed = 1f;
                return; // Do not apply run-only bone shaping to the outgoing Land.
            }
            _phaseLandingExit = false;
        }
        StabilizeAuthoredRunPose();
    }

    private void FreezeAuthoredSlideBase()
    {
        if (_animator == null) return;
        if (!_slideAnimatorFrozen)
        {
            _animator.Play(IdleState, 0, 0f);
            _animator.Update(0f);
            _activeAuthoredState = IdleState;
            _slideAnimatorFrozen = true;
        }
        _animator.speed = 0f;
    }

    private void ResumeAuthoredMotion()
    {
        if (!_slideAnimatorFrozen || _animator == null) return;
        _animator.speed = 1f;
        _activeAuthoredState = 0;
        _slideAnimatorFrozen = false;
    }

    private void ApplyAuthoredMotion(bool isJumping, float speed, bool idle)
    {
        int targetState = idle ? IdleState : isJumping ? JumpState : RunState;
        if (!_animator.HasState(0, targetState)) return;

        if (_activeAuthoredState != targetState)
        {
            bool leavingSlide = _activeAuthoredState == SlideState;
            if (_activeAuthoredState == 0)
            {
                _animator.Play(targetState, 0, 0f);
            }
            else
            {
                _animator.CrossFadeInFixedTime(
                    targetState,
                    ResolveAuthoredTransitionDuration(
                        _activeAuthoredState, targetState),
                    0,
                    0f);
            }
            if (leavingSlide)
            {
                _authoredBlendActive = false;
                // CrossFade is issued from LateUpdate, after the Animator's
                // normal evaluation for this frame. Register the transition
                // immediately without advancing its fixed-time blend so the
                // slide cannot remain authoritative for one extra frame.
                _animator.Update(0f);
            }
            _activeAuthoredState = targetState;
        }

        _animator.speed = targetState == RunState
            ? Mathf.Clamp(speed / 10f, 0.85f, 1.4f)
            : 1f;
    }

    private float ResolveAuthoredTransitionDuration(int previousState, int targetState)
    {
        // Phase-driven Jump must show the takeoff promptly. This is a visual
        // blend only; physics and the legacy/slide transitions are unchanged.
        if (_hasPhaseDrivenAirMotion && targetState == JumpState)
            return AuthoredJumpEntry;
        // Give the authored landing/recovery a few frames to settle instead of
        // snapping a compressed jump or slide directly into a full run stride.
        // These are animation blends only; action and collider clocks stay owned
        // by PlayerController / AIShadowRunner.
        float minimumDuration = targetState == IdleState ? 0.18f
            : targetState == RunState && previousState == JumpState ? 0.14f
            : targetState == RunState && previousState == SlideState ? 0.12f
            : 0.08f;
        return Mathf.Max(minimumDuration, authoredTransitionDuration);
    }

    private void ApplyAuthoredSlideMotion(
        float slideDuration, float deltaTime)
    {
        if (_animator == null) return;

        if (_activeAuthoredState != SlideState)
        {
            CaptureAuthoredEntryPose();
            _animator.Play(SlideState, 0, 0f);
            _animator.Update(0f);
            _activeAuthoredState = SlideState;
            _authoredBlendElapsed = 0f;
            _authoredBlendActive = true;
        }

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        _animator.speed = state.length > 0.001f
            ? state.length / Mathf.Max(0.2f, slideDuration)
            : 1f;
        BlendAuthoredEntryPose(deltaTime);
    }

    private void CaptureAuthoredEntryPose()
    {
        if (_authoredBlendBones == null
            || _authoredBlendPositions == null
            || _authoredBlendRotations == null)
        {
            PrepareAuthoredPoseBlend();
        }

        for (int i = 0; i < _authoredBlendBones.Length; i++)
        {
            Transform bone = _authoredBlendBones[i];
            if (bone == null || bone == transform) continue;
            _authoredBlendPositions[i] = bone.localPosition;
            _authoredBlendRotations[i] = bone.localRotation;
        }
    }

    private void BlendAuthoredEntryPose(float deltaTime)
    {
        if (!_authoredBlendActive || _authoredBlendBones == null) return;

        _authoredBlendElapsed += Mathf.Max(0f, deltaTime);
        float weight = ResolveTransitionWeight(
            _authoredBlendElapsed, authoredTransitionDuration);
        for (int i = 0; i < _authoredBlendBones.Length; i++)
        {
            Transform bone = _authoredBlendBones[i];
            if (bone == null || bone == transform) continue;
            bone.localPosition = Vector3.Lerp(
                _authoredBlendPositions[i], bone.localPosition, weight);
            bone.localRotation = Quaternion.Slerp(
                _authoredBlendRotations[i], bone.localRotation, weight);
        }

        if (weight >= 1f) _authoredBlendActive = false;
    }

    private void StabilizeAuthoredRunPose()
    {
        if (!stabilizeAuthoredRun) return;
        // Keep the body centred through the stride. Pitch, vertical motion and
        // the authored limbs remain; lateral travel, yaw and roll are removed.
        // Real lane-change lean is applied separately after this run-only step.
        ShapeAuthoredCoreBone(
            hipsTransform, _hipsBasePos, _hipsBaseRot, 0f, 0f, 0f, 0f);
        ShapeAuthoredCoreBone(
            bodyTransform, _bodyBasePos, _bodyBaseRot,
            runSpineLean, 0f, 0f, 0f);
        ShapeAuthoredCoreBone(
            chestTransform, _chestBasePos, _chestBaseRot, 0f, 0f, 0f, 0f);
        ShapeAuthoredCoreBone(
            _upperChest, _upperChestBasePos, _upperChestBaseRot, 0f, 0f, 0f, 0f);

        PreserveRunningKneeFlex(leftLowerLeg, _leftLowerLegBaseRot);
        PreserveRunningKneeFlex(rightLowerLeg, _rightLowerLegBaseRot);

        AlignRunningFootForward(leftFoot, _leftToes, 7f);
        AlignRunningFootForward(rightFoot, _rightToes, 7f);
    }

    private static void ShapeAuthoredCoreBone(
        Transform bone, Vector3 basePosition, Quaternion baseRotation,
        float targetLean, float yawLimit, float rollLimit,
        float maximumHorizontalTravel)
    {
        if (bone == null) return;

        Vector3 position = bone.localPosition;
        float parentScale = bone.parent != null
            ? bone.parent.TransformVector(Vector3.right).magnitude : 1f;
        float localTravel = maximumHorizontalTravel
            / Mathf.Max(0.0001f, parentScale);
        position.x = basePosition.x + Mathf.Clamp(
            position.x - basePosition.x, -localTravel, localTravel);
        bone.localPosition = position;

        Quaternion relativeRotation =
            Quaternion.Inverse(baseRotation) * bone.localRotation;
        Vector3 relativeEuler = SignedEuler(relativeRotation);
        if (targetLean > 0f)
        {
            relativeEuler.x = Mathf.Lerp(
                relativeEuler.x,
                Mathf.Max(relativeEuler.x, targetLean), 0.55f);
        }
        relativeEuler.y = Mathf.Clamp(
            relativeEuler.y, -yawLimit, yawLimit);
        relativeEuler.z = Mathf.Clamp(
            relativeEuler.z, -rollLimit, rollLimit);
        bone.localRotation = baseRotation * Quaternion.Euler(relativeEuler);
    }

    private static void PreserveRunningKneeFlex(
        Transform lowerLeg, Quaternion baseRotation)
    {
        if (lowerLeg == null) return;

        Vector3 relativeEuler = SignedEuler(
            Quaternion.Inverse(baseRotation) * lowerLeg.localRotation);
        // A support leg needs to extend, then fold during recovery. Forcing both
        // knees to remain bent erased the clip's contact phase and shortened its
        // stride. Only guard retargeting extremes; keep ordinary authored angles.
        relativeEuler.x = Mathf.Clamp(relativeEuler.x, -120f, 5f);
        lowerLeg.localRotation =
            baseRotation * Quaternion.Euler(relativeEuler);
    }

    private void AlignRunningFootForward(
        Transform foot, Transform toes, float allowedSplay)
    {
        if (foot == null || toes == null) return;

        Vector3 currentDirection = toes.position - foot.position;
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(
            currentDirection, Vector3.up);
        Vector3 horizontalForward = Vector3.ProjectOnPlane(
            transform.forward, Vector3.up).normalized;
        if (horizontalDirection.sqrMagnitude < 0.000001f
            || horizontalForward.sqrMagnitude < 0.000001f)
        {
            return;
        }

        float yawCorrection = Vector3.SignedAngle(
            horizontalDirection, horizontalForward, Vector3.up);
        if (Mathf.Abs(yawCorrection) <= allowedSplay) return;

        float appliedCorrection = yawCorrection
            - Mathf.Sign(yawCorrection) * allowedSplay;
        foot.rotation = Quaternion.AngleAxis(
            appliedCorrection, Vector3.up) * foot.rotation;
    }

    private static Vector3 SignedEuler(Quaternion rotation)
    {
        Vector3 angles = rotation.eulerAngles;
        angles.x = Mathf.DeltaAngle(0f, angles.x);
        angles.y = Mathf.DeltaAngle(0f, angles.y);
        angles.z = Mathf.DeltaAngle(0f, angles.z);
        return angles;
    }

    public static float ResolveTransitionWeight(
        float elapsed, float duration)
    {
        if (duration <= 0.0001f) return 1f;
        return Mathf.SmoothStep(
            0f, 1f, Mathf.Clamp01(elapsed / duration));
    }

    /// <summary>
    /// x: takeoff, y: apex, z: descent, w: landing preparation.
    /// The weights overlap deliberately so a continuous jump clock cannot pop.
    /// </summary>
    public static Vector4 ResolveJumpPoseWeights(float jump01)
    {
        float phase = Mathf.Clamp01(jump01);
        float takeoff = 1f - SmoothRange(phase, 0.08f, 0.26f);
        float apex = SmoothRange(phase, 0.12f, 0.40f)
            * (1f - SmoothRange(phase, 0.56f, 0.74f));
        float descent = SmoothRange(phase, 0.50f, 0.72f)
            * (1f - SmoothRange(phase, 0.86f, 0.98f));
        float preLanding = SmoothRange(phase, 0.78f, 0.98f);
        return new Vector4(takeoff, apex, descent, preLanding);
    }

    public static float ResolveLaneLeanAngle(
        float lateralVelocity, bool isSliding,
        float velocityForFullLean, float maximumAngle)
    {
        if (isSliding) return 0f;
        float normalizedVelocity = lateralVelocity
            / Mathf.Max(0.01f, Mathf.Abs(velocityForFullLean));
        return -Mathf.Clamp(normalizedVelocity, -1f, 1f)
            * Mathf.Abs(maximumAngle);
    }

    public static float DampVisualValue(
        float current, float target, float response, float deltaTime)
    {
        if (deltaTime <= 0f) return current;
        float weight = 1f - Mathf.Exp(
            -Mathf.Max(0f, response) * deltaTime);
        return Mathf.Lerp(current, target, weight);
    }

    private void ShapeAuthoredJumpPose(float jump01)
    {
        Vector4 weights = ResolveJumpPoseWeights(jump01);
        float torsoPitch = weights.x * 4f
            - weights.y * 2f
            - weights.z * 4f
            + weights.w * 7f;
        float compression = (weights.x + weights.w) * 0.025f;
        if (hipsTransform != null)
            hipsTransform.localPosition += Vector3.down * compression;
        if (bodyTransform != null)
            bodyTransform.localRotation *= Quaternion.Euler(
                torsoPitch, 0f, 0f);

        float kneePreparation = weights.x * 7f + weights.w * 18f;
        if (leftLowerLeg != null)
            leftLowerLeg.localRotation *= Quaternion.Euler(
                -kneePreparation, 0f, 0f);
        if (rightLowerLeg != null)
            rightLowerLeg.localRotation *= Quaternion.Euler(
                -kneePreparation, 0f, 0f);
        float toePreparation = weights.w * 10f;
        if (leftFoot != null)
            leftFoot.localRotation *= Quaternion.Euler(
                toePreparation, 0f, 0f);
        if (rightFoot != null)
            rightFoot.localRotation *= Quaternion.Euler(
                toePreparation, 0f, 0f);
    }

    private void ApplyVisualLaneLean(
        float lateralVelocity, bool isSliding,
        bool authoredPose, float deltaTime)
    {
        float targetLean = ResolveLaneLeanAngle(
            lateralVelocity, isSliding,
            laneLeanVelocity, maxLaneLeanAngle);
        _visualLaneLean = isSliding
            ? 0f
            : DampVisualValue(
                _visualLaneLean, targetLean,
                laneLeanResponse, deltaTime);
        if (bodyTransform == null || isSliding
            || Mathf.Abs(_visualLaneLean) <= 0.001f)
        {
            return;
        }

        if (authoredPose)
        {
            bodyTransform.localRotation *= Quaternion.Euler(
                0f, 0f, _visualLaneLean);
            return;
        }

        Quaternion relative = Quaternion.Inverse(_bodyBaseRot)
            * bodyTransform.localRotation;
        Vector3 relativeEuler = SignedEuler(relative);
        relativeEuler.z = _visualLaneLean;
        bodyTransform.localRotation = _bodyBaseRot
            * Quaternion.Euler(relativeEuler);
    }

    private void RotateTowardForward(Vector3 forward, float deltaTime)
    {
        if (forward.sqrMagnitude < 0.001f) return;
        Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up);
        float t = 1f - Mathf.Exp(-lookRotationSpeed * deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }

    private void ApplyRunPose(float speed, float deltaTime)
    {
        _runPhase += deltaTime * runSwingSpeed * (Mathf.Max(1f, speed) / 10f);
        float stride = Mathf.Sin(_runPhase);
        float armSwing = stride * armSwingAngle;
        float legSwing = stride * legSwingAngle;
        float elbowBend = elbowBendAngle
            + (0.5f - 0.5f * Mathf.Cos(_runPhase * 2f)) * elbowBendVariation;

        ApplyRunArm(leftUpperArm, leftLowerArm,
            _leftUpperArmBaseRootRot, _leftLowerArmBaseRootRot,
            armSwing, armTuckAngle, elbowBend);
        ApplyRunArm(rightUpperArm, rightLowerArm,
            _rightUpperArmBaseRootRot, _rightLowerArmBaseRootRot,
            -armSwing, -armTuckAngle, elbowBend);
        AimRunningForearm(leftLowerArm, leftHand, true, 0.35f);
        AimRunningForearm(rightLowerArm, rightHand, false, 0.35f);
        SetLocalRotation(leftUpperLeg,
            _leftUpperLegBaseRot * Quaternion.Euler(-legSwing, 0f, 0f));
        SetLocalRotation(rightUpperLeg,
            _rightUpperLegBaseRot * Quaternion.Euler(legSwing, 0f, 0f));
        float leftRecovery = 0.5f - 0.5f * Mathf.Cos(_runPhase);
        float rightRecovery = 1f - leftRecovery;
        float leftKneeBend = Mathf.Lerp(10f, kneeFlexAngle, leftRecovery);
        float rightKneeBend = Mathf.Lerp(10f, kneeFlexAngle, rightRecovery);
        SetLocalRotation(leftLowerLeg,
            _leftLowerLegBaseRot * Quaternion.Euler(-leftKneeBend, 0f, 0f));
        SetLocalRotation(rightLowerLeg,
            _rightLowerLegBaseRot * Quaternion.Euler(-rightKneeBend, 0f, 0f));

        float leftFootPitch = Mathf.Lerp(-14f, 20f, leftRecovery)
            + legSwing * 0.1f;
        float rightFootPitch = Mathf.Lerp(-14f, 20f, rightRecovery)
            - legSwing * 0.1f;
        SetLocalRotation(leftFoot,
            _leftFootBaseRot * Quaternion.Euler(leftFootPitch, 0f, 0f));
        SetLocalRotation(rightFoot,
            _rightFootBaseRot * Quaternion.Euler(rightFootPitch, 0f, 0f));

        float bob = Mathf.Abs(Mathf.Sin(_runPhase * 2f)) * bobAmplitude;
        Quaternion hipsRotation = _hipsBaseRot;
        Quaternion bodyRotation = _bodyBaseRot
            * Quaternion.Euler(runSpineLean, 0f, 0f);
        BlendCorePose(_hipsBasePos, hipsRotation,
            _bodyBasePos + Vector3.up * bob, bodyRotation, deltaTime);
    }

    private void ApplyJumpPose(float jump01, float deltaTime)
    {
        // Freeze the lead side from the last run stride so the pose stays stable
        // throughout the jump instead of swapping limbs in mid-air.
        bool leftLegLeads = Mathf.Sin(_runPhase) >= 0f;
        Vector4 weights = ResolveJumpPoseWeights(jump01);
        float airborneShape = Mathf.Clamp01(
            0.72f + weights.y * 0.28f - weights.w * 0.12f);
        float leadingArmSwing = -jumpArmRaiseAngle * airborneShape;
        float trailingArmSwing = jumpArmRaiseAngle
            * Mathf.Lerp(0.28f, 0.40f, weights.y);
        ApplyRunArm(leftUpperArm, leftLowerArm,
            _leftUpperArmBaseRootRot, _leftLowerArmBaseRootRot,
            leftLegLeads ? trailingArmSwing : leadingArmSwing,
            armTuckAngle, jumpElbowBendAngle);
        ApplyRunArm(rightUpperArm, rightLowerArm,
            _rightUpperArmBaseRootRot, _rightLowerArmBaseRootRot,
            leftLegLeads ? leadingArmSwing : trailingArmSwing,
            -armTuckAngle, jumpElbowBendAngle);

        float tuckScale = Mathf.Clamp(
            0.72f + weights.y * 0.28f
            - weights.z * 0.08f + weights.w * 0.06f,
            0.65f, 1f);
        float landingThighLift = weights.w * 8f;
        float leftThighAngle = (leftLegLeads
            ? jumpLegTuckAngle : -jumpLegTuckAngle * 0.3f)
            * tuckScale + landingThighLift;
        float rightThighAngle = (leftLegLeads
            ? -jumpLegTuckAngle * 0.3f : jumpLegTuckAngle)
            * tuckScale + landingThighLift;
        float commonLandingBend = weights.w * jumpKneeBendAngle * 0.22f;
        float leftKneeAngle = (leftLegLeads
            ? -jumpKneeBendAngle : -jumpKneeBendAngle * 0.55f)
            * tuckScale - commonLandingBend;
        float rightKneeAngle = (leftLegLeads
            ? -jumpKneeBendAngle * 0.55f : -jumpKneeBendAngle)
            * tuckScale - commonLandingBend;
        BlendLocalRotation(leftUpperLeg,
            _leftUpperLegBaseRot * Quaternion.Euler(leftThighAngle, 0f, 0f), deltaTime);
        BlendLocalRotation(rightUpperLeg,
            _rightUpperLegBaseRot * Quaternion.Euler(rightThighAngle, 0f, 0f), deltaTime);
        BlendLocalRotation(leftLowerLeg,
            _leftLowerLegBaseRot * Quaternion.Euler(leftKneeAngle, 0f, 0f), deltaTime);
        BlendLocalRotation(rightLowerLeg,
            _rightLowerLegBaseRot * Quaternion.Euler(rightKneeAngle, 0f, 0f), deltaTime);
        float leftFootPitch = Mathf.Lerp(
            leftLegLeads ? 22f : -8f, 10f, weights.w);
        float rightFootPitch = Mathf.Lerp(
            leftLegLeads ? -8f : 22f, 10f, weights.w);
        BlendLocalRotation(leftFoot,
            _leftFootBaseRot * Quaternion.Euler(
                leftFootPitch, 0f, 0f), deltaTime);
        BlendLocalRotation(rightFoot,
            _rightFootBaseRot * Quaternion.Euler(
                rightFootPitch, 0f, 0f), deltaTime);
        float compression = (weights.x + weights.w) * 0.035f;
        float spinePitch = jumpSpineLean + weights.x * 4f
            - weights.z * 4f + weights.w * 6f;
        BlendCorePose(_hipsBasePos + Vector3.down * compression, _hipsBaseRot,
            _bodyBasePos,
            _bodyBaseRot * Quaternion.Euler(
                spinePitch, 0f, 0f), deltaTime);
    }

    private void ApplySlidePose(float phase)
    {
        // Keep the authored skeleton intact: lower and recline the core, shape
        // both legs in local space, and use limited IK only for the support arm.
        float recovery = SmoothRange(phase, 0.76f, 1f);
        float hold = 1f - recovery;
        float poseWeight = SmoothRange(phase, 0.05f, 0.24f) * hold;
        float supportWeight = SmoothRange(phase, 0.14f, 0.28f) * hold;

        const float groundY = 0f;
        Vector3 groundUnderHips = new Vector3(
            _hipsBaseRootPos.x, groundY, _hipsBaseRootPos.z);
        float hipClearance = Mathf.Clamp(
            _hipsBaseRootPos.y - slideHipDrop, 0.08f, 0.16f);
        Vector3 lowHipsRoot = groundUnderHips
            + Vector3.up * hipClearance;
        Quaternion slideYawRotation = Quaternion.Euler(
            0f, slideYawAngle, 0f);
        Vector3 slideForwardRoot = slideYawRotation * Vector3.forward;
        Vector3 slideRightRoot = slideYawRotation * Vector3.right;
        Vector3 slideForwardWorld = transform.TransformDirection(
            slideForwardRoot);
        Vector3 slideRightWorld = transform.TransformDirection(
            slideRightRoot);

        if (hipsTransform != null)
        {
            hipsTransform.position = Vector3.Lerp(
                hipsTransform.position,
                transform.TransformPoint(lowHipsRoot), poseWeight);
        }
        if (bodyTransform != null)
            bodyTransform.localPosition = Vector3.Lerp(
                bodyTransform.localPosition, _bodyBasePos, poseWeight);

        BlendLocalRotationByWeight(hipsTransform,
            _hipsBaseRot * Quaternion.Euler(
                0f, slideYawAngle, slideBodyRoll * 0.35f),
            poseWeight);
        if (bodyTransform != null && chestTransform != null)
        {
            float recline = Mathf.Abs(slideSpineLean) * Mathf.Deg2Rad;
            Vector3 desiredChestUp =
                transform.up * Mathf.Cos(recline)
                - slideForwardWorld * Mathf.Sin(recline)
                + slideRightWorld * Mathf.Sin(
                    Mathf.Abs(slideBodyRoll) * Mathf.Deg2Rad);
            Quaternion targetBodyRotation = Quaternion.FromToRotation(
                chestTransform.up, desiredChestUp.normalized)
                * bodyTransform.rotation;
            bodyTransform.rotation = Quaternion.Slerp(
                bodyTransform.rotation, targetBodyRotation, poseWeight);
        }
        else
        {
            BlendLocalRotationByWeight(bodyTransform,
                _bodyBaseRot * Quaternion.Euler(
                    slideSpineLean, slideYawAngle, slideBodyRoll),
                poseWeight);
        }

        float leftLegLength = LimbLength(
            leftUpperLeg, leftLowerLeg, leftFoot);
        float rightLegLength = LimbLength(
            rightUpperLeg, rightLowerLeg, rightFoot);
        if (leftLegLength > 0f)
        {
            Vector3 leadingFoot = groundUnderHips
                + slideForwardRoot * (leftLegLength * 0.975f)
                - slideRightRoot * (leftLegLength * 0.06f)
                + Vector3.up * 0.12f;
            SolveTwoBoneLimb(leftUpperLeg, leftLowerLeg, leftFoot,
                leadingFoot, _leftFootBaseRootRot, leftLegLength,
                slideYawRotation * new Vector3(-0.08f, 1f, 0.42f),
                poseWeight);
            AimSlideFoot(leftFoot, _leftToes,
                slideForwardWorld, 6f, poseWeight);
        }
        else
        {
            BlendLocalRotationByWeight(leftUpperLeg,
                _leftUpperLegBaseRot * Quaternion.Euler(
                    slideFrontLegAngle, 0f, 0f), poseWeight);
            BlendLocalRotationByWeight(leftLowerLeg,
                _leftLowerLegBaseRot * Quaternion.Euler(8f, 0f, 0f),
                poseWeight);
        }

        if (rightLegLength > 0f)
        {
            Vector3 foldedFoot = groundUnderHips
                + slideForwardRoot * (rightLegLength * 0.62f)
                - slideRightRoot * (rightLegLength * 0.02f)
                + Vector3.up * 0.11f;
            SolveTwoBoneLimb(rightUpperLeg, rightLowerLeg, rightFoot,
                foldedFoot, _rightFootBaseRootRot, rightLegLength,
                slideYawRotation * new Vector3(1f, 0.22f, 0.18f),
                poseWeight);
            Vector3 foldedFootDirection =
                -slideRightWorld * 0.96f + slideForwardWorld * 0.28f;
            AimSlideFoot(rightFoot, _rightToes,
                foldedFootDirection, 4f, poseWeight);
        }
        else
        {
            BlendLocalRotationByWeight(rightUpperLeg,
                _rightUpperLegBaseRot * Quaternion.Euler(
                    slideRearLegAngle, 0f, 0f), poseWeight);
            BlendLocalRotationByWeight(rightLowerLeg,
                _rightLowerLegBaseRot * Quaternion.Euler(
                    -slideRearKneeBend, 0f, 0f), poseWeight);
        }

        // The free left arm keeps the frozen authored pose. It only follows the
        // torso as a child bone and must not perform a second slide gesture.
        float rightUpperArmLength = SegmentLength(
            rightUpperArm, rightLowerArm);
        float rightForearmLength = SegmentLength(
            rightLowerArm, rightHand);
        if (rightUpperArmLength > 0f && rightForearmLength > 0f)
        {
            Vector3 plantedElbow = groundUnderHips
                + slideRightRoot * (rightUpperArmLength * 0.90f)
                - slideForwardRoot * (rightUpperArmLength * 0.24f)
                - Vector3.up * 0.05f;
            AimSegmentAt(rightUpperArm, rightLowerArm,
                transform.TransformPoint(plantedElbow), supportWeight);

            Vector3 raisedForearmDirection = transform.TransformDirection(
                slideYawRotation
                * new Vector3(0.40f, 0.28f, 0.88f).normalized);
            Vector3 raisedHandTarget = rightLowerArm.position
                + raisedForearmDirection * rightForearmLength;
            AimSegmentAt(rightLowerArm, rightHand,
                raisedHandTarget, supportWeight);
        }
    }

    private static float SmoothRange(float value, float start, float end)
    {
        return Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(start, end, value));
    }

    private static void BlendLocalRotationByWeight(
        Transform bone, Quaternion target, float weight)
    {
        if (bone == null) return;
        bone.localRotation = Quaternion.Slerp(
            bone.localRotation, target, Mathf.Clamp01(weight));
    }

    private static float LimbLength(
        Transform upper, Transform lower, Transform end)
    {
        if (upper == null || lower == null || end == null) return 0f;
        return Vector3.Distance(upper.position, lower.position)
            + Vector3.Distance(lower.position, end.position);
    }

    private static float SegmentLength(Transform start, Transform end)
    {
        return start != null && end != null
            ? Vector3.Distance(start.position, end.position)
            : 0f;
    }

    private static void AimSegmentAt(
        Transform start, Transform end, Vector3 target, float weight)
    {
        if (start == null || end == null) return;
        Vector3 currentDirection = end.position - start.position;
        Vector3 desiredDirection = target - start.position;
        if (currentDirection.sqrMagnitude < 0.000001f
            || desiredDirection.sqrMagnitude < 0.000001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.FromToRotation(
            currentDirection, desiredDirection) * start.rotation;
        start.rotation = Quaternion.Slerp(
            start.rotation, targetRotation, Mathf.Clamp01(weight));
    }

    private void AimSlideFoot(
        Transform foot, Transform toes, Vector3 slideForward,
        float toeLiftAngle, float weight)
    {
        if (foot == null || toes == null) return;
        Vector3 currentDirection = toes.position - foot.position;
        if (currentDirection.sqrMagnitude < 0.000001f) return;

        float liftRadians = toeLiftAngle * Mathf.Deg2Rad;
        Vector3 desiredDirection =
            slideForward.normalized * Mathf.Cos(liftRadians)
            + transform.up * Mathf.Sin(liftRadians);
        Quaternion targetRotation = Quaternion.FromToRotation(
            currentDirection, desiredDirection) * foot.rotation;
        foot.rotation = Quaternion.Slerp(
            foot.rotation, targetRotation, Mathf.Clamp01(weight));
    }


    private void SolveTwoBoneLimb(
        Transform upper, Transform lower, Transform end,
        Vector3 targetRootPosition, Quaternion endRootRotation,
        float limbLength, Vector3 poleRootDirection, float weight)
    {
        if (upper == null || lower == null || end == null
            || limbLength <= 0.0001f)
        {
            return;
        }

        Vector3 target = Vector3.Lerp(
            end.position, transform.TransformPoint(targetRootPosition), weight);
        float upperLength = Vector3.Distance(
            upper.position, lower.position);
        float lowerLength = Vector3.Distance(
            lower.position, end.position);
        Vector3 upperToTarget = target - upper.position;
        float distance = Mathf.Clamp(upperToTarget.magnitude,
            Mathf.Abs(upperLength - lowerLength) + 0.001f,
            upperLength + lowerLength - 0.001f);
        Vector3 direction = upperToTarget.sqrMagnitude > 0.000001f
            ? upperToTarget.normalized
            : transform.forward;

        Vector3 pole = Vector3.ProjectOnPlane(
            transform.TransformDirection(poleRootDirection), direction);
        if (pole.sqrMagnitude < 0.000001f)
            pole = Vector3.ProjectOnPlane(transform.right, direction);
        pole.Normalize();

        float along = (upperLength * upperLength
            - lowerLength * lowerLength + distance * distance)
            / (2f * distance);
        float bend = Mathf.Sqrt(Mathf.Max(
            0f, upperLength * upperLength - along * along));
        Vector3 jointTarget = upper.position
            + direction * along + pole * bend;

        Vector3 currentUpper = lower.position - upper.position;
        Vector3 desiredUpper = jointTarget - upper.position;
        if (currentUpper.sqrMagnitude > 0.000001f
            && desiredUpper.sqrMagnitude > 0.000001f)
        {
            upper.rotation = Quaternion.FromToRotation(
                currentUpper, desiredUpper) * upper.rotation;
        }

        Vector3 currentLower = end.position - lower.position;
        Vector3 desiredLower = target - lower.position;
        if (currentLower.sqrMagnitude > 0.000001f
            && desiredLower.sqrMagnitude > 0.000001f)
        {
            lower.rotation = Quaternion.FromToRotation(
                currentLower, desiredLower) * lower.rotation;
        }

        end.rotation = Quaternion.Slerp(
            end.rotation, transform.rotation * endRootRotation, weight);
    }

    private void ApplyIdlePose(float deltaTime)
    {
        BlendLocalRotation(leftUpperArm, _leftUpperArmBaseRot, deltaTime);
        BlendLocalRotation(rightUpperArm, _rightUpperArmBaseRot, deltaTime);
        BlendLocalRotation(leftLowerArm, _leftLowerArmBaseRot, deltaTime);
        BlendLocalRotation(rightLowerArm, _rightLowerArmBaseRot, deltaTime);
        BlendLocalRotation(leftUpperLeg, _leftUpperLegBaseRot, deltaTime);
        BlendLocalRotation(rightUpperLeg, _rightUpperLegBaseRot, deltaTime);
        BlendLocalRotation(leftLowerLeg, _leftLowerLegBaseRot, deltaTime);
        BlendLocalRotation(rightLowerLeg, _rightLowerLegBaseRot, deltaTime);
        BlendLocalRotation(leftFoot, _leftFootBaseRot, deltaTime);
        BlendLocalRotation(rightFoot, _rightFootBaseRot, deltaTime);
        BlendCorePose(_hipsBasePos, _hipsBaseRot,
            _bodyBasePos, _bodyBaseRot, deltaTime);
    }

    private void BlendCorePose(Vector3 hipsPosition, Quaternion hipsRotation,
        Vector3 bodyPosition, Quaternion bodyRotation, float deltaTime)
    {
        float t = 1f - Mathf.Exp(-PoseLerpSpeed * deltaTime);
        if (hipsTransform != null)
        {
            hipsTransform.localPosition = Vector3.Lerp(
                hipsTransform.localPosition, hipsPosition, t);
            hipsTransform.localRotation = Quaternion.Slerp(
                hipsTransform.localRotation, hipsRotation, t);
        }
        if (bodyTransform != null)
        {
            bodyTransform.localPosition = Vector3.Lerp(
                bodyTransform.localPosition, bodyPosition, t);
            bodyTransform.localRotation = Quaternion.Slerp(
                bodyTransform.localRotation, bodyRotation, t);
        }
    }

    private void ApplyRunArm(Transform upperArm, Transform lowerArm,
        Quaternion upperBaseRootRotation, Quaternion lowerBaseRootRotation,
        float swingAngle, float tuckAngle, float bendAngle)
    {
        Quaternion tuck = Quaternion.AngleAxis(tuckAngle, Vector3.forward);
        Quaternion swing = Quaternion.AngleAxis(swingAngle, Vector3.right);
        if (upperArm != null)
            upperArm.rotation = transform.rotation * swing * tuck * upperBaseRootRotation;
        if (lowerArm != null)
        {
            Quaternion elbow = Quaternion.AngleAxis(-bendAngle, Vector3.right);
            lowerArm.rotation = transform.rotation * swing * elbow * tuck
                * lowerBaseRootRotation;
        }
    }

    private void AimRunningForearm(
        Transform lowerArm, Transform hand, bool isLeft,
        float targetVertical)
    {
        if (lowerArm == null || hand == null) return;
        Vector3 currentDirection = hand.position - lowerArm.position;
        if (currentDirection.sqrMagnitude < 0.000001f) return;

        // Solve from the actual elbow-to-wrist vector instead of assuming a
        // local Mixamo axis. Hands travel forward/up and slightly inward.
        Vector3 localTarget = new Vector3(
            isLeft ? 0.08f : -0.08f,
            targetVertical, 0.93f).normalized;
        Vector3 targetDirection = transform.TransformDirection(localTarget);
        lowerArm.rotation = Quaternion.FromToRotation(
            currentDirection.normalized, targetDirection) * lowerArm.rotation;
    }

    private void BlendLocalRotation(Transform bone, Quaternion target, float deltaTime)
    {
        if (bone == null) return;
        float t = 1f - Mathf.Exp(-PoseLerpSpeed * deltaTime);
        bone.localRotation = Quaternion.Slerp(bone.localRotation, target, t);
    }

    private static void SetLocalRotation(Transform bone, Quaternion target)
    {
        if (bone != null) bone.localRotation = target;
    }

    private Transform FindBone(Transform current, params string[] names)
    {
        if (current != null) return current;
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindDescendant(transform, names[i]);
            if (found != null) return found;
        }
        return null;
    }

    private static Quaternion BaseRotation(Transform bone)
    {
        return bone != null ? bone.localRotation : Quaternion.identity;
    }

    private static Quaternion RootRotation(Transform bone, Quaternion inverseRootRotation)
    {
        return bone != null
            ? inverseRootRotation * bone.rotation
            : Quaternion.identity;
    }

    private Vector3 RootPosition(Transform bone)
    {
        return bone != null
            ? transform.InverseTransformPoint(bone.position)
            : Vector3.zero;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            string candidate = descendants[i].name;
            int separator = candidate.LastIndexOf(':');
            if (separator >= 0) candidate = candidate.Substring(separator + 1);
            if (candidate == name) return descendants[i];
        }
        return null;
    }
}
