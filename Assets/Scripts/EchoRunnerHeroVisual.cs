using UnityEngine;

[DisallowMultipleComponent]
public sealed class EchoRunnerHeroVisual : MonoBehaviour
{
    [Header("Ground Contact")]
    [SerializeField] private Transform contactShadow;
    [SerializeField] private Renderer contactShadowRenderer;
    [SerializeField] private float shadowRadius = 0.52f;
    [SerializeField] private float maximumTrackedHeight = 3.2f;
    [SerializeField] private float groundedAlpha = 0.34f;
    [SerializeField] private float airborneAlpha = 0.07f;
    [SerializeField] private float raycastLift = 0.75f;
    [SerializeField] private float raycastDistance = 8f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private PlayerController _player;
    private CapsuleCollider _capsule;
    private MaterialPropertyBlock _propertyBlock;

    public Transform ContactShadow => contactShadow;
    public Renderer ContactShadowRenderer => contactShadowRenderer;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        UpdateContactShadow();
    }

    private void LateUpdate()
    {
        UpdateContactShadow();
    }

    public void ConfigureContactShadow(
        Transform shadow, Renderer shadowRenderer)
    {
        contactShadow = shadow;
        contactShadowRenderer = shadowRenderer;
        CacheReferences();
    }

    private void CacheReferences()
    {
        if (_player == null) _player = GetComponent<PlayerController>();
        if (_capsule == null) _capsule = GetComponent<CapsuleCollider>();
        if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
    }

    private void UpdateContactShadow()
    {
        if (contactShadow == null || contactShadowRenderer == null) return;
        CacheReferences();

        Vector3 origin = _capsule != null
            ? _capsule.bounds.center + Vector3.up * raycastLift
            : transform.position + Vector3.up * raycastLift;
        int mask = _player != null && _player.groundLayer.value != 0
            ? _player.groundLayer.value
            : Physics.DefaultRaycastLayers;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                Mathf.Max(0.1f, raycastDistance), mask,
                QueryTriggerInteraction.Ignore))
        {
            contactShadowRenderer.enabled = false;
            return;
        }

        contactShadowRenderer.enabled = true;
        Renderer surfaceRenderer =
            hit.collider.GetComponent<Renderer>();
        if (surfaceRenderer == null)
            surfaceRenderer = hit.collider.GetComponentInParent<Renderer>();
        Vector3 visualSurfacePoint = ResolveVisualSurfacePoint(
            hit.point, hit.normal, surfaceRenderer != null,
            surfaceRenderer != null ? surfaceRenderer.bounds : default);
        contactShadow.position = visualSurfacePoint + hit.normal * 0.012f;
        contactShadow.rotation = Quaternion.FromToRotation(
            Vector3.up, hit.normal);

        float characterBaseY = _capsule != null
            ? _capsule.bounds.min.y
            : transform.position.y;
        float height = Mathf.Max(0f, characterBaseY - hit.point.y);
        float scale = shadowRadius * ResolveShadowScale(
            height, maximumTrackedHeight);
        contactShadow.localScale = new Vector3(scale, 1f, scale);

        float alpha = ResolveShadowAlpha(
            height, maximumTrackedHeight, groundedAlpha, airborneAlpha);
        contactShadowRenderer.GetPropertyBlock(_propertyBlock);
        Color color = new Color(0.012f, 0.018f, 0.026f, alpha);
        _propertyBlock.SetColor(ColorId, color);
        contactShadowRenderer.SetPropertyBlock(_propertyBlock);
    }

    public static float ResolveShadowScale(
        float height, float maximumHeight)
    {
        float normalized = Mathf.Clamp01(
            Mathf.Max(0f, height) / Mathf.Max(0.01f, maximumHeight));
        return Mathf.Lerp(1f, 1.22f, normalized);
    }

    public static float ResolveShadowAlpha(
        float height, float maximumHeight,
        float grounded, float airborne)
    {
        float normalized = Mathf.Clamp01(
            Mathf.Max(0f, height) / Mathf.Max(0.01f, maximumHeight));
        return Mathf.Lerp(
            Mathf.Clamp01(grounded), Mathf.Clamp01(airborne), normalized);
    }

    public static Vector3 ResolveVisualSurfacePoint(
        Vector3 physicsHitPoint, Vector3 surfaceNormal,
        bool hasRendererBounds, Bounds rendererBounds)
    {
        if (!hasRendererBounds || surfaceNormal.y < 0.85f ||
            rendererBounds.size.y > 0.05f)
            return physicsHitPoint;

        float visualY = rendererBounds.max.y;
        if (Mathf.Abs(visualY - physicsHitPoint.y) > 1.5f)
            return physicsHitPoint;
        physicsHitPoint.y = visualY;
        return physicsHitPoint;
    }
}
