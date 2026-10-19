using UnityEngine;

public class Coin : MonoBehaviour
{
    public float rotateSpeed = 225f;
    public float yawAmplitude = 12f;
    public float bobSpeed = 3.5f;
    public float bobHeight = 0.06f;
    public bool IsEchoContractMarker { get; private set; }
    public int EchoChallengeStepId { get; private set; }

    private float _baseY;
    private float _phaseOffset;
    private float _rotationPhase;
    private Quaternion _baseRotation;
    private Transform _player;
    private Transform _viewer;
    private EchoCoinVisual _visual;
    private static Transform _cachedPlayer;
    private static Transform _cachedViewer;
    private static int _lastPlayerLookupFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlayerCache()
    {
        _cachedPlayer = null;
        _cachedViewer = null;
        _lastPlayerLookupFrame = -1;
    }

    void OnEnable()
    {
        _baseY = transform.position.y;
        _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        _rotationPhase = Random.Range(0f, Mathf.PI * 2f);
        _baseRotation = transform.rotation;
        _player = _cachedPlayer;
        _viewer = _cachedViewer;
        _visual = GetComponentInChildren<EchoCoinVisual>(true);
        _visual?.ResetPresentation();
    }

    void Update()
    {
        bool reducedMotion = EchoRunAccessibility.ReducedMotion;
        float yaw = reducedMotion
            ? 0f
            : Mathf.Sin(Time.time * rotateSpeed * Mathf.Deg2Rad
                        + _rotationPhase)
              * Mathf.Max(0f, yawAmplitude);
        UpdateVisualFacing(yaw);

        PowerUpController powerUps = PowerUpController.Instance;
        if (powerUps != null && powerUps.HasMagnet && ResolvePlayer() != null)
        {
            float radius = Mathf.Max(0.01f, powerUps.MagnetRadius);
            float distance = Vector3.Distance(transform.position, _player.position);
            if (distance <= radius)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, _player.position + Vector3.up * 0.7f,
                    Mathf.Lerp(8f, 24f, Mathf.Clamp01(1f - distance / radius))
                    * Time.deltaTime);
                return;
            }
        }

        float bob = reducedMotion
            ? 0f
            : Mathf.Sin((Time.time + _phaseOffset) * bobSpeed) * bobHeight;
        Vector3 pos = transform.position;
        pos.y = _baseY + bob;
        transform.position = pos;
    }

    private void UpdateVisualFacing(float yaw)
    {
        if (_visual == null)
            _visual = GetComponentInChildren<EchoCoinVisual>(true);
        if (_visual == null) return;

        Transform viewer = ResolveViewer();
        Quaternion facing = viewer != null
            ? ResolveViewFacingRotation(
                transform.position, viewer.position, _baseRotation, yaw)
            : _baseRotation * Quaternion.Euler(0f, yaw, 0f);
        _visual.ApplyViewFacingRotation(facing);
    }

    private Transform ResolveViewer()
    {
        if (_viewer != null) return _viewer;
        if (_cachedViewer != null)
        {
            _viewer = _cachedViewer;
            return _viewer;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            _cachedViewer = mainCamera.transform;
            _viewer = _cachedViewer;
            return _viewer;
        }

        _viewer = ResolvePlayer();
        if (_viewer != null) _cachedViewer = _viewer;
        return _viewer;
    }

    private Transform ResolvePlayer()
    {
        if (_player != null) return _player;
        if (_cachedPlayer != null)
        {
            _player = _cachedPlayer;
            return _player;
        }
        if (_lastPlayerLookupFrame == Time.frameCount) return null;

        _lastPlayerLookupFrame = Time.frameCount;
        PlayerController player = FindObjectOfType<PlayerController>();
        _cachedPlayer = player != null ? player.transform : null;
        _player = _cachedPlayer;
        return _player;
    }

    public static Quaternion ResolveViewFacingRotation(
        Vector3 coinPosition, Vector3 viewerPosition,
        Quaternion fallbackRotation, float yawOffset)
    {
        Vector3 viewerToCoin = coinPosition - viewerPosition;
        viewerToCoin.y = 0f;
        Quaternion facing = viewerToCoin.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(viewerToCoin.normalized, Vector3.up)
            : fallbackRotation;
        return facing * Quaternion.Euler(0f, yawOffset, 0f);
    }

    public static Coin EnsureRuntimeComponent(GameObject owner)
    {
        if (owner == null) return null;
        Coin coin = owner.GetComponent<Coin>();
        return coin != null ? coin : owner.AddComponent<Coin>();
    }

    public static Coin EnsureRuntimeContract(GameObject owner)
    {
        Coin coin = EnsureRuntimeComponent(owner);
        if (owner == null) return coin;

        Collider trigger = owner.GetComponent<Collider>();
        if (trigger == null)
        {
            BoxCollider box = owner.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 0.4f);
            trigger = box;
        }
        trigger.isTrigger = true;
        return coin;
    }

    public void ConfigureEchoContractMarker(bool isMarker,
        int challengeStepId = 0)
    {
        IsEchoContractMarker = isMarker;
        EchoChallengeStepId = isMarker ? Mathf.Max(0, challengeStepId) : 0;
        EchoCoinVisual visual = GetComponentInChildren<EchoCoinVisual>(true);
        if (visual != null) visual.SetContractMarker(isMarker);
    }
}
