using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Lanes")]
    public float laneDistance = TrackGeometryStandards.LaneSpacing;
    public float laneSwitchSpeed = 20f;

    [Header("Jump")]
    public float jumpHeight = 2.4f;
    public float jumpDuration = 0.78f;

    [Header("Slide")]
    public float slideDuration = 0.8f;
    public float slideColliderHeight = 1f;

    [Header("Ground Check")]
    public LayerMask groundLayer;

    [Header("Character Model")]
    public Transform characterModel;

    [Header("Fall Off")]
    public float fallOffY = -5f;

    public int CurrentLane { get; private set; } = 1;
    public float LateralOffset => _laneOffset;
    public float RenderedLateralOffset => ResolveRenderedLateralOffset(
        _laneOffset, transform.position,
        _rb != null ? _rb.position : transform.position,
        ForwardDirection,
        _rb != null && _rb.interpolation != RigidbodyInterpolation.None);
    public bool IsJumping { get; private set; }
    public bool IsSliding { get; private set; }
    public Vector3 ForwardDirection { get; private set; } = Vector3.forward;
    public ObstacleContactDiagnostic LastObstacleContact { get; private set; }
    public int ResolvedObstacleCount => _resolvedObstacles.Count;
    public int DuplicateObstacleContactCount { get; private set; }
    public event System.Action<PlayerActionSignal> ActionRaised;
    public PlayerMotionSnapshot MotionSnapshot => PlayerMotionFeedback.Project(
        CurrentLane, IsJumping, _jumpTimer, jumpDuration,
        IsSliding, _slideTimer, slideDuration, _lateralVelocity,
        _gm != null ? _gm.CurrentSpeed : 0f,
        _gm != null ? _gm.startSpeed : 0f,
        _gm != null ? _gm.maxSpeed : 0f,
        ForwardDirection, _laneOffset);

    private float _jumpTimer;
   private float _slideTimer;
    private float _jumpGroundY;
    private float _originalColliderHeight;
    private Vector3 _originalColliderCenter;
    private Vector3 _originalModelPos;
    private CapsuleCollider _capsuleCollider;
    private Rigidbody _rb;
    private TrackSegmentData _lastTurnSegment;
    private float _laneOffset;
    private float _lateralVelocity;
    private int _nextSignalSequence = 1;
    private int _nextActionId = 1;
    private int _activeJumpActionId;
    private int _activeSlideActionId;
    private int _activeLaneActionId;
    private int _activeLaneFromLane;
    private float _laneActionTimer;
    private GameManager _gm;
    private InputManager _input;
    private TrackManager _trackMgr;
    private readonly RaycastHit[] _obstacleSweepHits = new RaycastHit[8];
    private readonly Collider[] _obstacleOverlapHits = new Collider[8];
    private readonly System.Collections.Generic.Dictionary<int, Collider>
        _resolvedObstacles =
            new System.Collections.Generic.Dictionary<int, Collider>();
    private readonly System.Collections.Generic.List<int>
        _resolvedObstacleCleanup =
            new System.Collections.Generic.List<int>();

    public static float ResolveRenderedLateralOffset(float physicsOffset,
        Vector3 renderedPosition, Vector3 physicsPosition,
        Vector3 forwardDirection, bool usesRigidbodyInterpolation)
    {
        if (!usesRigidbodyInterpolation) return physicsOffset;

        Vector3 forward = Vector3.ProjectOnPlane(
            forwardDirection, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(
            Vector3.up, forward.normalized).normalized;
        return physicsOffset
               + Vector3.Dot(renderedPosition - physicsPosition, right);
    }

    private const float GROUND_RAY_DIST = 0.3f;

    // Saved originals so body-part adjustments are idempotent across scene reloads
    private bool _modelPartsSaved;
    private Vector3 _savedHeadPos, _savedTorsoScale;
    private Vector3 _savedLegUpperL, _savedLegUpperR, _savedLegLowerL, _savedLegLowerR;
    private Vector3 _savedFootL, _savedFootR;

    void Start()
    {
        _gm = GameManager.Instance;
        _input = InputManager.Instance;
        _trackMgr = TrackManager.Instance;

        _rb = GetComponent<Rigidbody>();
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;

        _capsuleCollider = GetComponent<CapsuleCollider>();
        if (_capsuleCollider != null)
        {
            _originalColliderHeight = _capsuleCollider.height;
            _originalColliderCenter = _capsuleCollider.center;
        }

        if (characterModel != null)
        {
            // Position model at capsule bottom so feet sit on the track surface
            if (_capsuleCollider != null)
            {
                float capsuleBottom = _capsuleCollider.center.y - _capsuleCollider.height * 0.5f;
                characterModel.localPosition = new Vector3(
                    characterModel.localPosition.x, capsuleBottom, characterModel.localPosition.z);
            }
            _originalModelPos = characterModel.localPosition;

            ApplyBodyPartAdjustments();
        }
    }

    void ApplyBodyPartAdjustments()
    {
        Transform head = characterModel.Find("Head");
        Transform torso = characterModel.Find("Torso");

        // Save originals on first call so adjustments never compound across restarts
        if (!_modelPartsSaved)
        {
            if (head != null) _savedHeadPos = head.localPosition;
            if (torso != null) _savedTorsoScale = torso.localScale;

            Transform t;
            t = characterModel.Find("Leg_Upper_L"); if (t != null) _savedLegUpperL = t.localScale;
            t = characterModel.Find("Leg_Upper_R"); if (t != null) _savedLegUpperR = t.localScale;
            t = characterModel.Find("Leg_Lower_L"); if (t != null) _savedLegLowerL = t.localScale;
            t = characterModel.Find("Leg_Lower_R"); if (t != null) _savedLegLowerR = t.localScale;
            t = characterModel.Find("Foot_L"); if (t != null) _savedFootL = t.localScale;
            t = characterModel.Find("Foot_R"); if (t != null) _savedFootR = t.localScale;

            _modelPartsSaved = true;
        }

        if (head != null) head.localPosition = _savedHeadPos;
        if (torso != null) torso.localScale = _savedTorsoScale;

        ApplyScaleFromSaved("Leg_Upper_L", _savedLegUpperL, 1f);
        ApplyScaleFromSaved("Leg_Upper_R", _savedLegUpperR, 1f);
        ApplyScaleFromSaved("Leg_Lower_L", _savedLegLowerL, 1f);
        ApplyScaleFromSaved("Leg_Lower_R", _savedLegLowerR, 1f);
        ApplyScaleFromSaved("Foot_L", _savedFootL, 1f);
        ApplyScaleFromSaved("Foot_R", _savedFootR, 1f);
    }

    void ApplyScaleFromSaved(string name, Vector3 saved, float multiplier)
    {
        Transform t = characterModel.Find(name);
        if (t != null) t.localScale = new Vector3(saved.x * multiplier, saved.y, saved.z * multiplier);
    }

   void Update()
   {
       if (_gm == null || _gm.State != GameState.Playing) return;
       if (_gm.IsDeathSequence) return;

       HandleInput();
       UpdateSlide();
       float offeredLaneCenter = AITrackDirector.Instance != null
           ? AITrackDirector.Instance.CurrentLaneIncentiveCenter : 1f;
       StyleTracker.TickLane(CurrentLane, Time.deltaTime,
           offeredLaneCenter);
   }

   void FixedUpdate()
   {
       if (_gm == null || _gm.State != GameState.Playing) return;
        if (_gm.IsDeathSequence)
        {
            _rb.velocity = Vector3.zero;
            return;
        }

        if (_rb.position.y < fallOffY)
        {
            _gm.GameOver();
            return;
        }

        UpdateForwardDirection();

        Vector3 forward = ForwardDirection;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        // Lane switching - use tracked scalar offset, not world position projection
        float targetLateral = (CurrentLane - 1) * laneDistance;
        float currentOffset = _laneOffset;
        Vector3 planarVelocity = CalculatePlanarVelocity(forward,
            _gm.CurrentSpeed, right, currentOffset, targetLateral,
            laneSwitchSpeed, Time.fixedDeltaTime, out float nextOffset);

        SweepForObstacleContact(planarVelocity);
        if (_gm.State != GameState.Playing || _gm.IsDeathSequence) return;

        _laneOffset = nextOffset;
        _lateralVelocity = Vector3.Dot(planarVelocity, right);
        if (_activeLaneActionId != 0)
        {
            _laneActionTimer += Time.fixedDeltaTime;
            if (Mathf.Abs(_laneOffset - targetLateral) <= 0.0001f)
                CompleteLaneChange();
        }
        Vector3 vel = _rb.velocity;
        vel.x = planarVelocity.x;
        vel.z = planarVelocity.z;

        vel = UpdateJumpVelocity(vel, Time.fixedDeltaTime);

        _rb.velocity = vel;
    }

    private Vector3 UpdateJumpVelocity(Vector3 velocity, float fixedDeltaTime)
    {
        if (!IsJumping) return velocity;

        _jumpTimer += fixedDeltaTime;
        float progress = Mathf.Clamp01(
            _jumpTimer / Mathf.Max(0.01f, jumpDuration));
        float targetHeight = EvaluateJumpArc(progress) * jumpHeight;
        float targetY = _jumpGroundY + targetHeight;
        velocity.y = CalculateVerticalCorrectionVelocity(
            _rb.position.y, targetY, fixedDeltaTime);

        if (progress >= 1f)
            CompleteJump();
        return velocity;
    }

    void UpdateForwardDirection()
    {
        if (_trackMgr == null) return;

        TrackSegmentData turnSeg = _trackMgr.FindTurnAtPosition(_rb.position);
        if (turnSeg == null)
        {
            _lastTurnSegment = null;
            return;
        }

        if (turnSeg == _lastTurnSegment) return;

        Vector3 entryDir = turnSeg.entryDirection;
        float distPastCorner = Vector3.Dot(_rb.position - turnSeg.turnPointWorld, entryDir);

        if (distPastCorner > 0f)
        {
            Vector3 entryRight = Vector3.Cross(Vector3.up, entryDir).normalized;
            Vector3 exitDir = turnSeg.exitDirection;
            Vector3 exitRight = Vector3.Cross(Vector3.up, exitDir).normalized;

            float laneOffset = Vector3.Dot(_rb.position - turnSeg.turnPointWorld, entryRight);

            ForwardDirection = exitDir;
            _laneOffset = laneOffset;

            Vector3 newPos = turnSeg.turnPointWorld
                + exitDir * distPastCorner
                + exitRight * laneOffset;
            newPos.y = _rb.position.y;
            _rb.position = newPos;

            _lastTurnSegment = turnSeg;
        }
    }

    void HandleInput()
    {
        if (_input == null
            || !_input.TryPeekSwipe(out BufferedSwipeCommand command))
            return;
        SwipeDirection swipe = command.direction;

        int previousLane = CurrentLane;
        bool accepted = false;
        bool temporarilyBlocked = false;

        switch (swipe)
        {
            case SwipeDirection.Left:
                if (CurrentLane > 0)
                {
                    AIShadowRunner.Instance?.RecordPlayerAction(
                        ShadowAction.Left, CurrentLane);
                    CurrentLane--;
                    accepted = true;
                }
                break;
            case SwipeDirection.Right:
                if (CurrentLane < 2)
                {
                    AIShadowRunner.Instance?.RecordPlayerAction(
                        ShadowAction.Right, CurrentLane);
                    CurrentLane++;
                    accepted = true;
                }
                break;
            case SwipeDirection.Up:
               if (!IsJumping && !IsSliding && IsGrounded())
               {
                   AIShadowRunner.Instance?.RecordPlayerAction(
                       ShadowAction.Jump, CurrentLane);
                    BeginJump();
                    accepted = true;
               }
                else
                {
                    temporarilyBlocked = true;
                }
                break;
            case SwipeDirection.Down:
               if (!IsSliding && !IsJumping && IsGrounded())
               {
       AIShadowRunner.Instance?.RecordPlayerAction(
           ShadowAction.Slide, CurrentLane);
                    BeginSlide();
                accepted = true;
                }
                else
                {
                    temporarilyBlocked = true;
                }
                break;
        }

        if (CurrentLane != previousLane)
        {
            AITrackDirector.Instance?.RecordLaneChange(CurrentLane);
            BeginLaneChange(previousLane, CurrentLane);
        }

        if (temporarilyBlocked)
        {
            _input.DeferSwipe(command);
            return;
        }
        _input.ResolveSwipe(command, accepted
                ? InputIntentOutcome.Executed
                : InputIntentOutcome.Rejected,
            CurrentLane);
    }

    void UpdateSlide()
    {
        if (!IsSliding) return;

       _slideTimer += Time.deltaTime;

       if (_slideTimer >= slideDuration)
           CompleteSlide();
    }

    private void BeginJump()
    {
        IsJumping = true;
        _jumpTimer = 0f;
        _jumpGroundY = _rb != null ? _rb.position.y : transform.position.y;
        _activeJumpActionId = NextActionId();
        AITrackDirector.Instance?.RecordJump();
        RaiseAction(PlayerActionEdge.JumpStarted, _activeJumpActionId,
            jumpDuration, CurrentLane, CurrentLane, ResolvePlayerPosition());
    }

    private void CompleteJump()
    {
        if (!IsJumping) return;

        int actionId = _activeJumpActionId != 0
            ? _activeJumpActionId
            : NextActionId();
        IsJumping = false;
        _activeJumpActionId = 0;
        RaiseAction(PlayerActionEdge.Landed, actionId, jumpDuration,
            CurrentLane, CurrentLane, ResolvePlayerPosition());
    }

    private void BeginSlide()
    {
        IsSliding = true;
        _slideTimer = 0f;
        _activeSlideActionId = NextActionId();
        AITrackDirector.Instance?.RecordSlide();
        ApplySlideCollider();
        RaiseAction(PlayerActionEdge.SlideStarted, _activeSlideActionId,
            slideDuration, CurrentLane, CurrentLane, ResolvePlayerPosition());
    }

    private void CompleteSlide()
    {
        if (!IsSliding) return;

        int actionId = _activeSlideActionId != 0
            ? _activeSlideActionId
            : NextActionId();
        IsSliding = false;
        if (characterModel != null)
            characterModel.localPosition = _originalModelPos;
        if (_capsuleCollider != null)
        {
            _capsuleCollider.height = _originalColliderHeight;
            _capsuleCollider.center = _originalColliderCenter;
        }
        _activeSlideActionId = 0;
        RaiseAction(PlayerActionEdge.SlideEnded, actionId, slideDuration,
            CurrentLane, CurrentLane, ResolvePlayerPosition());
    }

    private void BeginLaneChange(int fromLane, int toLane)
    {
        _activeLaneActionId = NextActionId();
        _activeLaneFromLane = fromLane;
        _laneActionTimer = 0f;
        float targetOffset = (toLane - 1) * laneDistance;
        float duration = Mathf.Abs(targetOffset - _laneOffset)
                         / Mathf.Max(0.01f, laneSwitchSpeed);
        RaiseAction(PlayerActionEdge.LaneChangeStarted,
            _activeLaneActionId, duration, fromLane, toLane,
            ResolvePlayerPosition());
    }

    private void CompleteLaneChange()
    {
        if (_activeLaneActionId == 0) return;

        int actionId = _activeLaneActionId;
        int fromLane = _activeLaneFromLane;
        float duration = _laneActionTimer;
        _activeLaneActionId = 0;
        _laneActionTimer = 0f;
        RaiseAction(PlayerActionEdge.LaneChangeCompleted, actionId,
            duration, fromLane, CurrentLane, ResolvePlayerPosition());
    }

    private void ApplySlideCollider()
    {
        if (_capsuleCollider == null) return;

        // Keep the capsule bottom fixed while lowering its top. The model itself
        // stays at scale 1; CharacterAnimator supplies the visible crouch pose.
        float minimumHeight = _capsuleCollider.radius * 2f;
        float height = Mathf.Clamp(slideColliderHeight,
            minimumHeight, _originalColliderHeight);
        float bottom = _originalColliderCenter.y - _originalColliderHeight * 0.5f;
        Vector3 center = _originalColliderCenter;
        center.y = bottom + height * 0.5f;
        _capsuleCollider.height = height;
        _capsuleCollider.center = center;
    }

    private void RaiseAction(PlayerActionEdge edge, int actionId,
        float duration, int fromLane, int toLane, Vector3 position)
    {
        PlayerMotionSnapshot motion = MotionSnapshot;
        var signal = new PlayerActionSignal(edge, NextSignalSequence(),
            actionId, Time.time, Mathf.Max(0f, duration), position,
            motion.Forward, fromLane, toLane, motion);
        ActionRaised?.Invoke(signal);
    }

    private Vector3 ResolvePlayerPosition()
    {
        return _rb != null ? _rb.position : transform.position;
    }

    private int NextSignalSequence()
    {
        int sequence = _nextSignalSequence++;
        if (_nextSignalSequence <= 0) _nextSignalSequence = 1;
        return sequence;
    }

    private int NextActionId()
    {
        int actionId = _nextActionId++;
        if (_nextActionId <= 0) _nextActionId = 1;
        return actionId;
    }

    bool IsGrounded()
    {
        if (_capsuleCollider == null) return false;
        float bottom = _rb.position.y + _capsuleCollider.center.y
                       - _capsuleCollider.height / 2f;
        float rayStart = bottom + 0.1f;
        return Physics.Raycast(new Vector3(_rb.position.x, rayStart, _rb.position.z),
                               Vector3.down, GROUND_RAY_DIST + 0.2f, groundLayer);
    }

   void OnTriggerEnter(Collider other)
   {
        Coin coin = other.GetComponent<Coin>();
        if (coin != null)
        {
            if (coin.IsEchoContractMarker)
                _gm.AddContractMarker();
            else
                _gm.AddCoins(1);
            AITrackDirector.Instance?.RecordCoin();
            AIShadowRunner.Instance?.RecordCoin(coin.IsEchoContractMarker,
                coin.EchoChallengeStepId);
            AudioManager.Instance?.PlayCoin();
            ParticleManager.Instance?.EmitCoin(other.transform.position,
                transform.position + Vector3.up * 0.8f);
            if (TrackManager.Instance != null)
                TrackManager.Instance.ReleaseDynamic(other.gameObject);
            else
                other.gameObject.SetActive(false);
            return;
        }

        Obstacle obs = other.GetComponentInParent<Obstacle>();
        if (obs != null)
            HandleObstacleContact(other, obs, ObstacleContactSource.Trigger);
   }

   void SweepForObstacleContact(Vector3 movement)
   {
       if (_capsuleCollider == null || movement.sqrMagnitude < 0.0001f) return;

       CleanupResolvedObstacles();

       GetCapsuleSweepShape(out Vector3 pointA, out Vector3 pointB,
           out float radius);

       // CapsuleCast does not report colliders that already overlap the
       // capsule. Pooled triggers and low frame rates can place the player
       // inside an obstacle before this check runs, so handle overlaps first.
       int overlapCount = Physics.OverlapCapsuleNonAlloc(pointA, pointB,
           radius, _obstacleOverlapHits, Physics.AllLayers,
           QueryTriggerInteraction.Collide);
       Collider overlappingObstacle = FindUnresolvedObstacleCollider(
           _obstacleOverlapHits, overlapCount);
       if (overlappingObstacle != null)
       {
           HandleObstacleContact(overlappingObstacle,
               overlappingObstacle.GetComponentInParent<Obstacle>(),
               ObstacleContactSource.Overlap);
           return;
       }

       float distance = movement.magnitude * Time.fixedDeltaTime + 0.05f;
       int hitCount = Physics.CapsuleCastNonAlloc(pointA, pointB, radius,
           movement.normalized, _obstacleSweepHits, distance,
           Physics.AllLayers, QueryTriggerInteraction.Collide);

       Collider closestObstacle = null;
       float closestDistance = float.MaxValue;
       for (int i = 0; i < hitCount; i++)
       {
           Collider candidate = _obstacleSweepHits[i].collider;
           if (candidate == null || IsResolved(candidate)) continue;
           if (candidate.GetComponentInParent<Obstacle>() == null) continue;
           if (_obstacleSweepHits[i].distance < closestDistance)
           {
               closestObstacle = candidate;
               closestDistance = _obstacleSweepHits[i].distance;
           }
       }

       if (closestObstacle == null) return;

       HandleObstacleContact(closestObstacle,
           closestObstacle.GetComponentInParent<Obstacle>(),
           ObstacleContactSource.Sweep);
   }

   private Collider FindUnresolvedObstacleCollider(Collider[] candidates,
       int count)
   {
       if (candidates == null) return null;
       int limit = Mathf.Min(Mathf.Max(0, count), candidates.Length);
       for (int i = 0; i < limit; i++)
       {
           Collider candidate = candidates[i];
           if (candidate == null || IsResolved(candidate)) continue;
           if (candidate.GetComponentInParent<Obstacle>() != null)
               return candidate;
       }
       return null;
   }

   public static Collider FindObstacleCollider(Collider[] candidates, int count,
       Collider ignored)
   {
       if (candidates == null) return null;
       int limit = Mathf.Min(Mathf.Max(0, count), candidates.Length);
       for (int i = 0; i < limit; i++)
       {
           Collider candidate = candidates[i];
           if (candidate == null || candidate == ignored) continue;
           if (candidate.GetComponentInParent<Obstacle>() != null)
               return candidate;
       }
       return null;
   }

   void GetCapsuleSweepShape(out Vector3 pointA, out Vector3 pointB,
       out float radius)
   {
       Vector3 scale = transform.lossyScale;
       float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y),
           Mathf.Abs(scale.z));
       radius = _capsuleCollider.radius * maxScale;
       Vector3 axis = _capsuleCollider.direction == 0
           ? transform.right
           : (_capsuleCollider.direction == 2 ? transform.forward : transform.up);
       float height = _capsuleCollider.height * maxScale;
       float pointOffset = Mathf.Max(0f, height * 0.5f - radius);
       Vector3 center = transform.TransformPoint(_capsuleCollider.center);
       pointA = center + axis * pointOffset;
       pointB = center - axis * pointOffset;
   }

   void HandleObstacleContact(Collider other, Obstacle obs,
       ObstacleContactSource source)
   {
       if (obs == null || _gm == null || _gm.State != GameState.Playing
           || _gm.IsDeathSequence) return;

       int obstacleId = GetObstacleId(obs);
        if (_resolvedObstacles.ContainsKey(obstacleId))
        {
            DuplicateObstacleContactCount++;
            RecordObstacleDiagnostic(source, obstacleId, obs.type,
               new ObstacleContactEvaluation(
                   ObstacleContactOutcome.AlreadyResolved,
                   ObstacleContactReason.PreviouslyResolved, 0f), false);
           return;
       }

       Bounds playerBounds = _capsuleCollider != null
           ? _capsuleCollider.bounds
           : new Bounds(_rb.position, Vector3.zero);
       ObstacleContactEvaluation evaluation = ObstacleContactRules.Evaluate(
           obs.type, playerBounds, other.bounds, IsJumping, IsSliding,
           ForwardDirection);
       _resolvedObstacles[obstacleId] = other;
       RecordObstacleDiagnostic(source, obstacleId, obs.type, evaluation);
       EchoChallengeObstacleTag challengeTag =
           obs.GetComponentInParent<EchoChallengeObstacleTag>();
       EchoChallengeObstacleBinding challengeBinding = challengeTag != null
           ? challengeTag.Binding : default;
       PredictionGateObstacleTag predictionGateTag =
           obs.GetComponentInParent<PredictionGateObstacleTag>();
       PredictionGateObstacleBinding predictionGateBinding =
           predictionGateTag != null
               ? predictionGateTag.Binding : default;
       int trackingId = TrackManager.GetObstacleTrackingId(obs.gameObject);

       if (evaluation.Passed)
       {
           AIShadowRunner shadow = AIShadowRunner.Instance;
           if (shadow != null && predictionGateBinding.IsBound)
           {
               shadow.RecordPredictionGateObstaclePassed(
                   predictionGateBinding, trackingId);
           }
           bool firstSettlement = shadow == null
               || shadow.RecordDodge(obs.type,
                   trackingId,
                   CurrentLane, challengeBinding);
           if (firstSettlement)
           {
               AIPlayerSkillEstimator.RecordObstacleOutcome(obs.type, true);
               AITrackDirector.Instance?.RecordDodge();
               AudioManager.Instance?.PlayDodgeObstacle();
           }
           return;
       }

           AIPlayerSkillEstimator.RecordObstacleOutcome(
               obs.type, false);
           StyleTracker.RecordMistake();
           AITrackDirector.Instance?.RecordObstacleHit();
           if (predictionGateBinding.IsBound)
           {
               AIShadowRunner.Instance?.RecordPredictionGateObstacleHit(
                   predictionGateBinding, trackingId);
           }
           AIShadowRunner.Instance?.RecordObstacleHit(challengeBinding);
           Vector3 playerPosition = ResolvePlayerPosition();
           Vector3 contactPosition = other != null
               ? other.ClosestPoint(playerPosition)
               : playerPosition;
           if (PowerUpController.Instance != null
               && PowerUpController.Instance.TryAbsorbCollision())
           {
               RaiseAction(PlayerActionEdge.ImpactAbsorbed, NextActionId(),
                   0f, CurrentLane, CurrentLane, contactPosition);
               if (TrackManager.Instance != null)
                   TrackManager.Instance.ReleaseDynamic(other.gameObject);
               else
                   other.gameObject.SetActive(false);
               return;
           }
           if (_gm.TryRecoverFromCollision())
           {
               RaiseAction(PlayerActionEdge.ImpactRecovered, NextActionId(),
                   0f, CurrentLane, CurrentLane, contactPosition);
               if (TrackManager.Instance != null)
                   TrackManager.Instance.ReleaseDynamic(other.gameObject);
               else
                   other.gameObject.SetActive(false);
               return;
           }
           StopBeforeObstacle(other);
           _lateralVelocity = 0f;
           RaiseAction(PlayerActionEdge.FatalImpact, NextActionId(),
               0f, CurrentLane, CurrentLane, contactPosition);
           _gm.GameOver();
   }

   private bool IsResolved(Collider collider)
   {
       Obstacle obstacle = collider != null
           ? collider.GetComponentInParent<Obstacle>()
           : null;
       return obstacle != null
              && _resolvedObstacles.ContainsKey(GetObstacleId(obstacle));
   }

   private static int GetObstacleId(Obstacle obstacle)
   {
       return obstacle != null ? obstacle.gameObject.GetInstanceID() : 0;
   }

   public void ForgetResolvedObstacle(GameObject obstacleInstance)
   {
       Obstacle obstacle = obstacleInstance != null
           ? obstacleInstance.GetComponentInParent<Obstacle>()
           : null;
       if (obstacle != null)
           _resolvedObstacles.Remove(GetObstacleId(obstacle));
   }

   private void CleanupResolvedObstacles()
   {
       _resolvedObstacleCleanup.Clear();
       foreach (System.Collections.Generic.KeyValuePair<int, Collider> pair
                in _resolvedObstacles)
       {
           Collider collider = pair.Value;
           if (collider == null || !collider.gameObject.activeInHierarchy
               || IsFullyBehindPlayer(collider.bounds))
               _resolvedObstacleCleanup.Add(pair.Key);
       }

       for (int i = 0; i < _resolvedObstacleCleanup.Count; i++)
           _resolvedObstacles.Remove(_resolvedObstacleCleanup[i]);
   }

   private bool IsFullyBehindPlayer(Bounds obstacleBounds)
   {
       Vector3 direction = ForwardDirection.sqrMagnitude > 0.0001f
           ? ForwardDirection.normalized
           : Vector3.forward;
       Bounds playerBounds = _capsuleCollider != null
           ? _capsuleCollider.bounds
           : new Bounds(_rb.position, Vector3.zero);
       float obstacleRear = Vector3.Dot(obstacleBounds.center, direction)
                            + ProjectedHalfExtent(obstacleBounds.extents,
                                direction);
       float playerRear = Vector3.Dot(playerBounds.center, direction)
                          - ProjectedHalfExtent(playerBounds.extents, direction);
       return obstacleRear < playerRear;
   }

   private static float ProjectedHalfExtent(Vector3 extents, Vector3 direction)
   {
       return Mathf.Abs(direction.x) * extents.x
              + Mathf.Abs(direction.y) * extents.y
              + Mathf.Abs(direction.z) * extents.z;
   }

   private void RecordObstacleDiagnostic(ObstacleContactSource source,
       int obstacleId, ObstacleType type,
       ObstacleContactEvaluation evaluation, bool updateDisplay = true)
   {
       var diagnostic = new ObstacleContactDiagnostic
       {
           source = source,
           obstacleId = obstacleId,
           type = type,
           seed = _gm != null ? _gm.RunSeed : 0,
           speed = _gm != null ? _gm.CurrentSpeed : 0f,
           lane = CurrentLane,
           jumping = IsJumping,
           sliding = IsSliding,
           verticalClearance = evaluation.verticalClearance,
           outcome = evaluation.outcome,
           reason = evaluation.reason
       };
       if (updateDisplay) LastObstacleContact = diagnostic;
       AIRunTelemetry.RecordObstacleContact(diagnostic);
   }

   public static float EvaluateJumpArc(float progress)
   {
       float t = Mathf.Clamp01(progress);
       return 4f * t * (1f - t);
   }

   public static float CalculateVerticalCorrectionVelocity(
       float currentY, float targetY, float fixedDeltaTime)
   {
       return (targetY - currentY) / Mathf.Max(0.0001f, fixedDeltaTime);
   }

   public static Vector3 CalculatePlanarVelocity(Vector3 forward,
       float forwardSpeed, Vector3 right, float currentOffset,
       float targetOffset, float switchSpeed, float fixedDeltaTime,
       out float nextOffset)
   {
       float deltaTime = Mathf.Max(0.0001f, fixedDeltaTime);
       nextOffset = Mathf.MoveTowards(currentOffset, targetOffset,
           Mathf.Max(0f, switchSpeed) * deltaTime);
       float lateralSpeed = (nextOffset - currentOffset) / deltaTime;
       return forward.normalized * Mathf.Max(0f, forwardSpeed)
              + right.normalized * lateralSpeed;
   }

   public static Vector3 CalculateObstacleStopPosition(Bounds obstacleBounds,
       Vector3 playerPosition, Vector3 forward, float clearance)
   {
       Vector3 direction = forward.sqrMagnitude > 0.0001f
           ? forward.normalized
           : Vector3.forward;
       Vector3 extents = obstacleBounds.extents;
       float projectedHalfDepth = Mathf.Abs(direction.x) * extents.x
                                  + Mathf.Abs(direction.y) * extents.y
                                  + Mathf.Abs(direction.z) * extents.z;
       float obstacleFront = Vector3.Dot(obstacleBounds.center, direction)
                             - projectedHalfDepth;
       float safeProjection = obstacleFront - Mathf.Max(0f, clearance);
       float correction = safeProjection - Vector3.Dot(playerPosition, direction);
       return correction < 0f
           ? playerPosition + direction * correction
           : playerPosition;
   }

   void StopBeforeObstacle(Collider obstacle)
   {
       float clearance = _capsuleCollider != null
           ? _capsuleCollider.radius + 0.05f
           : 0.45f;
       _rb.position = CalculateObstacleStopPosition(
           obstacle.bounds, _rb.position, ForwardDirection, clearance);
       _rb.velocity = Vector3.zero;
   }

}
