using UnityEngine;

public enum PlayerFeedbackCue
{
    None,
    JumpStart,
    Land,
    SlideStart,
    SlideEnd,
    RecoverableImpact,
    FatalImpact
}

/// <summary>
/// Presentation-only bridge from authoritative player/contract outcomes to
/// animation, camera, sound and VFX. It never decides whether an action is
/// accepted and never writes movement, lane, collider or contract state.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
[DefaultExecutionOrder(50)]
public sealed class PlayerFeedbackController : MonoBehaviour
{
    [Header("Contact Placement")]
    [Min(0f)] public float groundContactOffset = 1f;

    private PlayerController _player;
    private CapsuleCollider _capsuleCollider;
    private CharacterAnimator _characterAnimator;
    private CameraFollow _cameraFollow;
    private AIShadowRunner _shadowRunner;
    private EchoRoadVisualController _roadVisualController;
    private AudioManager _audioEventSource;
    private GameManager _gameManager;
    private GameManager _stateEventSource;
    private int _lastHandledSequence;
    private bool _continuousFeedbackActive;
    private bool _actionAudioPaused;
    private bool _slideLoopOwned;

    public int LastHandledSequence => _lastHandledSequence;

    private void Awake()
    {
        ResolveLocalReferences();
    }

    private void OnEnable()
    {
        ResolveLocalReferences();
        RefreshGameStateSubscription();
        SubscribePlayer();
        RefreshShadowSubscription();
        RefreshAudioSubscription();
    }

    private void Start()
    {
        ResolveLocalReferences();
        RefreshGameStateSubscription();
        SubscribePlayer();
        RefreshShadowSubscription();
        RefreshAudioSubscription();
    }

    private void Update()
    {
        if (_player == null)
        {
            ResolveLocalReferences();
            SubscribePlayer();
            if (_player == null) return;
        }

        RefreshShadowSubscription();
        RefreshGameStateSubscription();
        RefreshAudioSubscription();
        if (_cameraFollow == null) ResolveCamera();

        bool playing = _gameManager != null
                       && _gameManager.State == GameState.Playing
                       && !_gameManager.IsDeathSequence;
        if (!playing)
        {
            if (_continuousFeedbackActive)
                ResetContinuousFeedback();
            return;
        }

        PlayerMotionSnapshot motion = _player.MotionSnapshot;
        _continuousFeedbackActive = true;
        _characterAnimator?.SetMotionFeedback(
            motion.Jump01, motion.Slide01, motion.LateralVelocity);
        _cameraFollow?.SetMotionFeedback(motion.Speed01, motion.Slide01);
        AudioManager.Instance?.SetSpeedFeedback(motion.Speed01);
        if (_roadVisualController == null)
            _roadVisualController = EchoRoadVisualController.Instance;
        _roadVisualController?.SetSpeedFeedback(motion.Speed01);
        SynchronizeActionAudio(motion);
        EmitContinuousContact(motion);
    }

    private void ResolveLocalReferences()
    {
        if (_player == null) _player = GetComponent<PlayerController>();
        if (_capsuleCollider == null && _player != null)
            _capsuleCollider = _player.GetComponent<CapsuleCollider>();
        if (_characterAnimator == null && _player != null)
        {
            Transform model = _player.characterModel;
            _characterAnimator = model != null
                ? model.GetComponent<CharacterAnimator>()
                : GetComponentInChildren<CharacterAnimator>(true);
        }
        RefreshGameStateSubscription();
        ResolveCamera();
    }

    private void ResolveCamera()
    {
        Camera main = Camera.main;
        if (main != null)
            _cameraFollow = main.GetComponent<CameraFollow>();
        if (_cameraFollow == null)
            _cameraFollow = FindObjectOfType<CameraFollow>();
    }

    private void SubscribePlayer()
    {
        if (_player == null) return;
        _player.ActionRaised -= HandleAction;
        _player.ActionRaised += HandleAction;
    }

    private void RefreshGameStateSubscription()
    {
        GameManager next = GameManager.Instance;
        _gameManager = next;
        if (_stateEventSource == next) return;
        if (_stateEventSource != null)
            _stateEventSource.OnStateChanged.RemoveListener(
                HandleGameStateChanged);
        _stateEventSource = next;
        if (_stateEventSource != null)
            _stateEventSource.OnStateChanged.AddListener(
                HandleGameStateChanged);
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state != GameState.Playing)
            ResetContinuousFeedback();
    }

    private void RefreshShadowSubscription()
    {
        AIShadowRunner next = AIShadowRunner.Instance;
        if (_shadowRunner == next) return;
        if (_shadowRunner != null)
            _shadowRunner.PredictionGateSettlementConsumed -=
                HandlePredictionGateSettlement;
        _shadowRunner = next;
        if (_shadowRunner != null)
            _shadowRunner.PredictionGateSettlementConsumed +=
                HandlePredictionGateSettlement;
    }

    private void HandleAction(PlayerActionSignal signal)
    {
        if (!ShouldHandleSequence(_lastHandledSequence, signal.Sequence))
            return;
        _lastHandledSequence = signal.Sequence;

        PlayerFeedbackCue cue = CueFor(signal.Edge);
        Vector3 contact = UsesGroundContact(cue)
            ? ResolveGroundContact(signal.Position)
            : signal.Position;
        Vector3 forward = signal.Forward;
        switch (cue)
        {
            case PlayerFeedbackCue.JumpStart:
                PauseActionFootsteps();
                AudioManager.Instance?.PlayJump();
                ParticleManager.Instance?.EmitTakeoff(contact, forward);
                break;
            case PlayerFeedbackCue.Land:
                AudioManager.Instance?.PlayLand(0.72f);
                ParticleManager.Instance?.EmitLand(contact, forward, 0.72f);
                _cameraFollow?.AddLandingPulse(0.65f);
                ResumeActionFootsteps();
                break;
            case PlayerFeedbackCue.SlideStart:
                PauseActionFootsteps();
                AudioManager.Instance?.PlaySlide();
                AudioManager.Instance?.BeginSlideLoop();
                _slideLoopOwned = true;
                ParticleManager.Instance?.EmitSlideStart(contact, forward);
                break;
            case PlayerFeedbackCue.SlideEnd:
                EndOwnedSlideLoop();
                AudioManager.Instance?.PlaySlideExit();
                ParticleManager.Instance?.EmitSlideEnd(contact, forward);
                break;
            case PlayerFeedbackCue.RecoverableImpact:
                AudioManager.Instance?.PlayImpactResult(false);
                ParticleManager.Instance?.EmitImpactResult(
                    contact, -forward, false);
                _cameraFollow?.AddImpactPulse(
                    signal.Edge == PlayerActionEdge.ImpactAbsorbed
                        ? 0.35f : 0.62f);
                break;
            case PlayerFeedbackCue.FatalImpact:
                // GameManager remains the owner of the fatal sound and broad
                // death burst; this layer adds only the directional contact.
                ParticleManager.Instance?.EmitImpactResult(
                    contact, -forward, true);
                _cameraFollow?.AddImpactPulse(1f);
                break;
        }
    }

    private void HandlePredictionGateSettlement(
        PredictionGateSettlement settlement)
    {
        if (!settlement.IsCounterSuccess || _player == null) return;
        PlayerMotionSnapshot motion = _player.MotionSnapshot;
        Vector3 contact = ResolveGroundContact(transform.position);
        ParticleManager.Instance?.EmitCounterSuccess(contact, motion.Forward);
        AudioManager.Instance?.PlayCounterSuccess();
    }

    private void SynchronizeActionAudio(PlayerMotionSnapshot motion)
    {
        if (motion.IsSliding)
        {
            PauseActionFootsteps();
            if (!_slideLoopOwned)
            {
                AudioManager.Instance?.BeginSlideLoop();
                _slideLoopOwned = true;
            }
            return;
        }

        if (_slideLoopOwned) EndOwnedSlideLoop();
        if (motion.IsJumping) PauseActionFootsteps();
        else ResumeActionFootsteps();
    }

    private void PauseActionFootsteps()
    {
        if (_actionAudioPaused) return;
        AudioManager.Instance?.PauseFootstepsForAction();
        _actionAudioPaused = true;
    }

    private void ResumeActionFootsteps()
    {
        if (!_actionAudioPaused) return;
        AudioManager.Instance?.ResumeFootstepsAfterAction();
        _actionAudioPaused = false;
    }

    private void EndOwnedSlideLoop()
    {
        if (!_slideLoopOwned) return;
        AudioManager.Instance?.EndSlideLoop();
        _slideLoopOwned = false;
        _actionAudioPaused = false;
    }

    private void EmitContinuousContact(PlayerMotionSnapshot motion)
    {
        if (!motion.IsSliding) return;
        Vector3 contact = ResolveGroundContact(transform.position);
        ParticleManager.Instance?.EmitSlideSustain(
            contact, motion.Forward, motion.Slide01);
    }

    private void HandleFootstepEmitted(int footstepSequence)
    {
        if (!_continuousFeedbackActive || _player == null) return;
        PlayerMotionSnapshot motion = _player.MotionSnapshot;
        if (motion.IsJumping || motion.IsSliding) return;
        Vector3 contact = ResolveGroundContact(transform.position);
        Vector3 footfall = ResolveAlternatingFootfallPosition(
            contact, motion.Forward, footstepSequence);
        ParticleManager.Instance?.EmitTrail(footfall);
    }

    private void RefreshAudioSubscription()
    {
        AudioManager next = AudioManager.Instance;
        if (_audioEventSource == next) return;
        if (_audioEventSource != null)
            _audioEventSource.FootstepEmitted -= HandleFootstepEmitted;
        _audioEventSource = next;
        if (_audioEventSource != null)
            _audioEventSource.FootstepEmitted += HandleFootstepEmitted;
    }

    private Vector3 ResolveGroundContact(Vector3 sourcePosition)
    {
        int groundMask = _player != null ? _player.groundLayer.value : 0;
        if (groundMask != 0)
        {
            Vector3 origin = sourcePosition + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.down,
                    out RaycastHit hit, 6f, groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                Renderer surfaceRenderer =
                    hit.collider.GetComponent<Renderer>();
                if (surfaceRenderer == null)
                    surfaceRenderer =
                        hit.collider.GetComponentInParent<Renderer>();
                Vector3 visualSurface =
                    EchoRunnerHeroVisual.ResolveVisualSurfacePoint(
                        hit.point, hit.normal, surfaceRenderer != null,
                        surfaceRenderer != null
                            ? surfaceRenderer.bounds
                            : default);
                return visualSurface + hit.normal * 0.012f;
            }
        }

        if (_capsuleCollider != null)
        {
            return ResolveColliderGroundContactPosition(
                sourcePosition, _capsuleCollider.bounds);
        }
        return ResolveGroundContactPosition(
            sourcePosition, groundContactOffset);
    }

    private void ResetContinuousFeedback()
    {
        _continuousFeedbackActive = false;
        _characterAnimator?.ClearMotionFeedback();
        // Let FOV and follow distance settle instead of snapping on death or
        // pause; hard restoration is reserved for component teardown.
        _cameraFollow?.SetMotionFeedback(0f, 0f);
        AudioManager.Instance?.SetSpeedFeedback(0f);
        _roadVisualController?.ResetSpeedFeedback();
        EndOwnedSlideLoop();
        ResumeActionFootsteps();
    }

    private void OnDisable()
    {
        if (_player != null) _player.ActionRaised -= HandleAction;
        if (_shadowRunner != null)
            _shadowRunner.PredictionGateSettlementConsumed -=
                HandlePredictionGateSettlement;
        if (_stateEventSource != null)
            _stateEventSource.OnStateChanged.RemoveListener(
                HandleGameStateChanged);
        if (_audioEventSource != null)
            _audioEventSource.FootstepEmitted -= HandleFootstepEmitted;
        _shadowRunner = null;
        _stateEventSource = null;
        _audioEventSource = null;
        ResetContinuousFeedback();
        // Unity can keep a managed proxy after the camera component has already
        // been destroyed during scene teardown. The explicit Unity null check
        // avoids invoking native Camera APIs through that stale proxy.
        if (_cameraFollow != null)
            _cameraFollow.ClearMotionFeedback();
        _cameraFollow = null;
    }

    private void OnDestroy()
    {
        if (_player != null) _player.ActionRaised -= HandleAction;
        if (_shadowRunner != null)
            _shadowRunner.PredictionGateSettlementConsumed -=
                HandlePredictionGateSettlement;
        if (_stateEventSource != null)
            _stateEventSource.OnStateChanged.RemoveListener(
                HandleGameStateChanged);
        if (_audioEventSource != null)
            _audioEventSource.FootstepEmitted -= HandleFootstepEmitted;
    }

    public static PlayerFeedbackCue CueFor(PlayerActionEdge edge)
    {
        switch (edge)
        {
            case PlayerActionEdge.JumpStarted:
                return PlayerFeedbackCue.JumpStart;
            case PlayerActionEdge.Landed:
                return PlayerFeedbackCue.Land;
            case PlayerActionEdge.SlideStarted:
                return PlayerFeedbackCue.SlideStart;
            case PlayerActionEdge.SlideEnded:
                return PlayerFeedbackCue.SlideEnd;
            case PlayerActionEdge.ImpactAbsorbed:
            case PlayerActionEdge.ImpactRecovered:
                return PlayerFeedbackCue.RecoverableImpact;
            case PlayerActionEdge.FatalImpact:
                return PlayerFeedbackCue.FatalImpact;
            default:
                return PlayerFeedbackCue.None;
        }
    }

    public static bool ShouldHandleSequence(int lastSequence, int nextSequence)
    {
        return nextSequence > 0 && nextSequence > lastSequence;
    }

    public static bool UsesGroundContact(PlayerFeedbackCue cue)
    {
        return cue == PlayerFeedbackCue.JumpStart
               || cue == PlayerFeedbackCue.Land
               || cue == PlayerFeedbackCue.SlideStart
               || cue == PlayerFeedbackCue.SlideEnd;
    }

    public static Vector3 ResolveGroundContactPosition(
        Vector3 rootPosition, float groundOffset)
    {
        return rootPosition + Vector3.down * Mathf.Max(0f, groundOffset);
    }

    public static Vector3 ResolveColliderGroundContactPosition(
        Vector3 rootPosition, Bounds capsuleBounds)
    {
        return new Vector3(
            rootPosition.x, capsuleBounds.min.y, rootPosition.z);
    }

    public static Vector3 ResolveAlternatingFootfallPosition(
        Vector3 groundContact, Vector3 forward, int footfallIndex,
        float lateralOffset = 0.14f)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;
        Vector3 right = Vector3.Cross(
            Vector3.up, flatForward.normalized).normalized;
        float side = (footfallIndex & 1) == 0 ? -1f : 1f;
        return groundContact
               + right * side * Mathf.Max(0f, lateralOffset)
               + Vector3.up * 0.012f;
    }
}
