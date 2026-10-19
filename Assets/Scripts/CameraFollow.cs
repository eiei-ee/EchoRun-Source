using UnityEngine;

public enum CameraViewHeight
{
    Low = 0,
    High = 1
}

public static class CameraViewSettings
{
    public const string PreferenceKey = "CameraViewHeight";

    public static CameraViewHeight Current => Normalize(
        PlayerPrefs.GetInt(PreferenceKey, (int)CameraViewHeight.Low));

    public static CameraViewHeight Normalize(int value)
    {
        return value == (int)CameraViewHeight.High
            ? CameraViewHeight.High : CameraViewHeight.Low;
    }
}

public class CameraFollow : MonoBehaviour
{
    [Header("Follow")]
    public Transform target;
    public Vector3 offset = new Vector3(0f, 4f, -7f);
    public float smoothSpeed = 8f;

    [Header("Motion Feedback")]
    public float motionResponse = 7f;
    public float speedPullback = 0.38f;
    public float speedLift = 0.06f;
    public float slideCameraDrop = 0.12f;
    public float maximumSpeedFovBoost = 3.5f;
    public float fovResponse = 6f;

    [Header("Action Pulses")]
    public float landingPulseAmplitude = 0.10f;
    public float landingPulseDuration = 0.16f;
    public float impactPulseAmplitude = 0.18f;
    public float impactPulseDuration = 0.22f;

    [Header("Death Feedback")]
    public float deathShakeAmplitude = 0.22f;
    public float deathShakeDuration = 0.55f;

    private Vector3 _velocity = Vector3.zero;
    private PlayerController _pc;
    private Transform _cachedTarget;
    private Camera _camera;
    private float _invSmoothSpeed;
    private Vector3 _shakeOffset;
    private float _shakeTimer;
    private float _groundedTargetY;
    private bool _hasGroundedTargetY;
    private bool _hasMotionFeedback;
    private float _feedbackSpeed01;
    private float _feedbackSlide01;
    private float _smoothedSpeed01;
    private float _smoothedSlide01;
    private float _baseFieldOfView;
    private float _appliedFovOffset;
    private bool _hasBaseFieldOfView;
    private bool _landingPulseActive;
    private float _landingPulseElapsed;
    private float _landingPulseStrength;
    private bool _impactPulseActive;
    private float _impactPulseElapsed;
    private float _impactPulseStrength;
    private Vector3 _appliedTransientOffset;
    private bool _wasDeathSequence;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void Start()
    {
        _invSmoothSpeed = 1f / Mathf.Max(0.01f, smoothSpeed);
        RefreshTargetReference();
        SampleExternallyOwnedFieldOfView();
    }

    private void LateUpdate()
    {
        // A zero timestep can leave SmoothDamp's retained velocity invalid.
        // Freeze the complete camera pose while paused.
        if (Time.deltaTime <= 0f || (GameManager.Instance != null
            && GameManager.Instance.State == GameState.Paused)) return;
        RemoveTransientOffset();
        if (target == null) return;

        RefreshTargetReference();
        bool reducedMotion = EchoRunAccessibility.ReducedMotion;
        UpdateMotionFeedback(reducedMotion, Time.unscaledDeltaTime);
        UpdateDeathShake(reducedMotion);

        bool isJumping = _pc != null && _pc.IsJumping;
        if (!_hasGroundedTargetY || !isJumping)
        {
            _groundedTargetY = target.position.y;
            _hasGroundedTargetY = true;
        }
        Vector3 followAnchor = ResolveFollowAnchor(
            target.position, isJumping, _groundedTargetY);

        Vector3 forward = _pc != null
            ? _pc.ForwardDirection
            : Vector3.forward;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        // A lane change moves the runner inside the frame. Following its full
        // lateral offset hides the opposite lane (and its echo) on a phone.
        if (_pc != null)
            followAnchor = ResolveTrackAnchor(followAnchor, forward,
                _pc.RenderedLateralOffset);

        CameraViewHeight viewHeight = CameraViewSettings.Current;
        float aspect = _camera != null ? _camera.aspect : 1f;
        float lookAhead = ResolveLookAhead(viewHeight, aspect);
        SampleExternallyOwnedFieldOfView();
        Vector3 viewOffset = ResolveLaneFramingOffset(
            ResolveViewOffset(offset, viewHeight), aspect,
            _hasBaseFieldOfView ? _baseFieldOfView : 62f,
            _pc != null ? _pc.laneDistance : TrackGeometryStandards.LaneSpacing,
            lookAhead);
        if (GameManager.Instance != null
            && GameManager.Instance.State == GameState.Menu)
        {
            // Present the same runner and city above the home action dock.
            viewOffset = new Vector3(0f, 2.9f, -8.8f);
            lookAhead = 2f;
        }
        Vector3 worldOffset = forward * viewOffset.z
            + Vector3.up * viewOffset.y
            + right * viewOffset.x;
        Vector3 feedbackOffset = reducedMotion
            ? Vector3.zero
            : ResolveMotionOffset(
                forward, _smoothedSpeed01, _smoothedSlide01,
                speedPullback, speedLift, slideCameraDrop);
        Vector3 targetPos = followAnchor + worldOffset + feedbackOffset;
        _invSmoothSpeed = 1f / Mathf.Max(0.01f, smoothSpeed);
        transform.position = Vector3.SmoothDamp(
            transform.position, targetPos,
            ref _velocity, _invSmoothSpeed);
        _appliedTransientOffset = _shakeOffset
            + UpdateActionPulseOffset(right, reducedMotion);
        transform.position += _appliedTransientOffset;

        // Position already eases through a corner. A second, independent yaw
        // lag aimed along the new road can throw the runner out of the frame.
        // Aim along the current boom so its anchor stays centered throughout.
        Vector3 aimForward = Vector3.ProjectOnPlane(
            followAnchor - transform.position, Vector3.up);
        if (aimForward.sqrMagnitude < 0.0001f) aimForward = forward;
        Vector3 lookTarget = followAnchor + aimForward.normalized * lookAhead;
        transform.rotation = Quaternion.LookRotation(lookTarget - transform.position);

        UpdateFieldOfView(reducedMotion, Time.unscaledDeltaTime);
    }

    public void SetMotionFeedback(float speed01, float slide01)
    {
        _hasMotionFeedback = true;
        _feedbackSpeed01 = Mathf.Clamp01(speed01);
        _feedbackSlide01 = Mathf.Clamp01(slide01);
    }

    public void ClearMotionFeedback()
    {
        ResetMotionFeedback();
    }

    public void ResetMotionFeedback()
    {
        _hasMotionFeedback = false;
        _feedbackSpeed01 = 0f;
        _feedbackSlide01 = 0f;
        _smoothedSpeed01 = 0f;
        _smoothedSlide01 = 0f;
        ResetActionPulses();
        _shakeTimer = 0f;
        _shakeOffset = Vector3.zero;
        _wasDeathSequence = false;
        RemoveTransientOffset();
        RestoreExternallyOwnedFieldOfView();
    }

    public void AddLandingPulse(float strength = 1f)
    {
        if (EchoRunAccessibility.ReducedMotion)
        {
            ResetActionPulses();
            return;
        }
        _landingPulseActive = true;
        _landingPulseElapsed = 0f;
        _landingPulseStrength = Mathf.Max(
            _landingPulseStrength, Mathf.Clamp01(strength));
    }

    public void AddImpactPulse(float strength = 1f)
    {
        if (EchoRunAccessibility.ReducedMotion)
        {
            ResetActionPulses();
            return;
        }
        _impactPulseActive = true;
        _impactPulseElapsed = 0f;
        _impactPulseStrength = Mathf.Max(
            _impactPulseStrength, Mathf.Clamp01(strength));
    }

    public void TriggerLandingPulse(float strength = 1f)
    {
        AddLandingPulse(strength);
    }

    public void TriggerImpactPulse(float strength = 1f)
    {
        AddImpactPulse(strength);
    }

    private void OnDisable()
    {
        ResetMotionFeedback();
    }

    private void RefreshTargetReference()
    {
        if (_cachedTarget == target) return;
        _cachedTarget = target;
        _pc = target != null
            ? target.GetComponent<PlayerController>()
            : null;
        _hasGroundedTargetY = target != null;
        if (target != null) _groundedTargetY = target.position.y;
    }

    private void UpdateMotionFeedback(
        bool reducedMotion, float deltaTime)
    {
        if (reducedMotion)
        {
            _smoothedSpeed01 = 0f;
            _smoothedSlide01 = 0f;
            return;
        }

        float targetSpeed = _hasMotionFeedback
            ? _feedbackSpeed01 : 0f;
        float targetSlide = _hasMotionFeedback
            ? _feedbackSlide01 : 0f;
        _smoothedSpeed01 = DampVisualValue(
            _smoothedSpeed01, targetSpeed,
            motionResponse, deltaTime);
        _smoothedSlide01 = DampVisualValue(
            _smoothedSlide01, targetSlide,
            motionResponse * 1.4f, deltaTime);
    }

    private void UpdateDeathShake(bool reducedMotion)
    {
        if (reducedMotion)
        {
            _shakeTimer = 0f;
            _shakeOffset = Vector3.zero;
            _wasDeathSequence = GameManager.Instance != null
                                && GameManager.Instance.IsDeathSequence;
            return;
        }

        bool isDeathSequence = GameManager.Instance != null
                               && GameManager.Instance.IsDeathSequence;
        if (isDeathSequence && !_wasDeathSequence)
        {
            _shakeTimer = Mathf.Max(0.01f, deathShakeDuration);
        }
        _wasDeathSequence = isDeathSequence;
        if (_shakeTimer <= 0f) return;

        _shakeOffset = Random.insideUnitSphere
            * Mathf.Max(0f, deathShakeAmplitude)
            * (_shakeTimer / Mathf.Max(0.01f, deathShakeDuration));
        _shakeTimer -= Time.unscaledDeltaTime;
        if (_shakeTimer <= 0f) _shakeOffset = Vector3.zero;
    }

    private Vector3 UpdateActionPulseOffset(
        Vector3 right, bool reducedMotion)
    {
        if (reducedMotion)
        {
            ResetActionPulses();
            return Vector3.zero;
        }

        float deltaTime = Time.unscaledDeltaTime;
        float landing = 0f;
        if (_landingPulseActive)
        {
            _landingPulseElapsed += deltaTime;
            float progress = _landingPulseElapsed
                / Mathf.Max(0.01f, landingPulseDuration);
            landing = EvaluateDampedPulse(
                progress,
                landingPulseAmplitude * _landingPulseStrength,
                1f);
            if (progress >= 1f)
            {
                _landingPulseActive = false;
                _landingPulseStrength = 0f;
            }
        }

        float impact = 0f;
        if (_impactPulseActive)
        {
            _impactPulseElapsed += deltaTime;
            float progress = _impactPulseElapsed
                / Mathf.Max(0.01f, impactPulseDuration);
            impact = EvaluateDampedPulse(
                progress,
                impactPulseAmplitude * _impactPulseStrength,
                2f);
            if (progress >= 1f)
            {
                _impactPulseActive = false;
                _impactPulseStrength = 0f;
            }
        }

        return Vector3.down * landing
            + right * impact
            + Vector3.up * Mathf.Abs(impact) * 0.18f;
    }

    private void ResetActionPulses()
    {
        _landingPulseActive = false;
        _landingPulseElapsed = 0f;
        _landingPulseStrength = 0f;
        _impactPulseActive = false;
        _impactPulseElapsed = 0f;
        _impactPulseStrength = 0f;
    }

    private void RemoveTransientOffset()
    {
        if (_appliedTransientOffset.sqrMagnitude <= 0.0000001f)
            return;
        transform.position -= _appliedTransientOffset;
        _appliedTransientOffset = Vector3.zero;
    }

    private void SampleExternallyOwnedFieldOfView()
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        if (_camera == null) return;
        _baseFieldOfView = ResolveExternallyOwnedBaseFieldOfView(
            _camera.fieldOfView,
            _baseFieldOfView,
            _appliedFovOffset,
            _hasBaseFieldOfView);
        _hasBaseFieldOfView = true;
    }

    private void UpdateFieldOfView(
        bool reducedMotion, float deltaTime)
    {
        SampleExternallyOwnedFieldOfView();
        if (_camera == null || !_hasBaseFieldOfView) return;

        float targetOffset = reducedMotion
            ? 0f
            : ResolveSpeedFieldOfViewOffset(
                _smoothedSpeed01, maximumSpeedFovBoost);
        _appliedFovOffset = reducedMotion
            ? 0f
            : DampVisualValue(
                _appliedFovOffset, targetOffset,
                fovResponse, deltaTime);
        _camera.fieldOfView = _baseFieldOfView + _appliedFovOffset;
    }

    private void RestoreExternallyOwnedFieldOfView()
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        SampleExternallyOwnedFieldOfView();
        if (_camera != null && _hasBaseFieldOfView)
            _camera.fieldOfView = _baseFieldOfView;
        _appliedFovOffset = 0f;
    }

    public static Vector3 ResolveFollowAnchor(
        Vector3 targetPosition, bool isJumping, float groundedY)
    {
        if (isJumping)
            targetPosition.y = Mathf.Lerp(
                groundedY, targetPosition.y, 0.12f);
        return targetPosition;
    }

    public static Vector3 ResolveViewOffset(
        Vector3 baseOffset, CameraViewHeight viewHeight)
    {
        if (viewHeight == CameraViewHeight.High)
            baseOffset.y += 1.25f;
        return baseOffset;
    }

    public static Vector3 ResolveTrackAnchor(Vector3 playerAnchor,
        Vector3 forward, float renderedLateralOffset)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, flatForward.normalized);
        return playerAnchor - right * renderedLateralOffset;
    }

    public static float ResolveLookAhead(CameraViewHeight viewHeight,
        float aspect = 0.5f)
    {
        if (aspect >= 1f)
            return viewHeight == CameraViewHeight.High ? 8f : 6f;
        return viewHeight == CameraViewHeight.High ? 20f : 16f;
    }

    public static Vector3 ResolveLaneFramingOffset(Vector3 viewOffset,
        float aspect, float verticalFov, float laneDistance, float lookAhead)
    {
        // Reserve a body-width margin on both outer lanes, including a nearby
        // echo 2.5m behind and its jump. Fit to the actual camera viewport, not
        // Screen dimensions, so safe areas and portrait render targets work.
        // The envelope is fixed for a viewport: an echo changing lanes/gap
        // must not make the camera zoom in and out during play.
        float halfWidth = Mathf.Max(0f, laneDistance) + 0.65f;
        float halfFov = Mathf.Clamp(verticalFov, 30f, 90f) * 0.5f * Mathf.Deg2Rad;
        float requiredDepth = halfWidth
            / (Mathf.Tan(halfFov) * Mathf.Max(0.3f, aspect) * 0.92f);
        float distance = Mathf.Max(0.1f, -viewOffset.z);
        for (int iteration = 0; iteration < 5; iteration++)
        {
            float pitch = Mathf.Atan2(viewOffset.y, distance + lookAhead);
            float depth = (distance - 3.1f) * Mathf.Cos(pitch)
                + (viewOffset.y - 5f) * Mathf.Sin(pitch);
            if (depth >= requiredDepth) break;
            distance += (requiredDepth - depth) / Mathf.Cos(pitch);
        }
        viewOffset.x = 0f;
        viewOffset.z = -distance;
        return viewOffset;
    }

    public static Vector3 ResolveMotionOffset(
        Vector3 forward, float speed01, float slide01,
        float pullback, float lift, float slideDrop)
    {
        Vector3 direction = forward.sqrMagnitude > 0.0001f
            ? forward.normalized
            : Vector3.forward;
        float speed = Mathf.Clamp01(speed01);
        float slide = Mathf.Clamp01(slide01);
        return -direction * Mathf.Max(0f, pullback) * speed
            + Vector3.up * (
                Mathf.Max(0f, lift) * speed
                - Mathf.Max(0f, slideDrop) * slide);
    }

    public static float ResolveSpeedFieldOfViewOffset(
        float speed01, float maximumBoost)
    {
        return Mathf.Clamp01(speed01) * Mathf.Max(0f, maximumBoost);
    }

    public static float ResolveExternallyOwnedBaseFieldOfView(
        float currentFieldOfView, float trackedBase,
        float previouslyAppliedOffset, bool hasTrackedBase)
    {
        if (!hasTrackedBase) return currentFieldOfView;
        float expected = trackedBase + previouslyAppliedOffset;
        return Mathf.Abs(currentFieldOfView - expected) > 0.05f
            ? currentFieldOfView
            : trackedBase;
    }

    public static float EvaluateDampedPulse(
        float normalizedTime, float amplitude, float cycles)
    {
        if (normalizedTime <= 0f || normalizedTime >= 1f) return 0f;
        float envelope = 1f - normalizedTime;
        return Mathf.Sin(
            normalizedTime * Mathf.PI * 2f * Mathf.Max(0.5f, cycles))
            * envelope * amplitude;
    }

    public static float DampVisualValue(
        float current, float target, float response, float deltaTime)
    {
        if (deltaTime <= 0f) return current;
        float weight = 1f - Mathf.Exp(
            -Mathf.Max(0f, response) * deltaTime);
        return Mathf.Lerp(current, target, weight);
    }
}
