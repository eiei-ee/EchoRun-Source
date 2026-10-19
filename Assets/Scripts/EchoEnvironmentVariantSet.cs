using UnityEngine;
using UnityEngine.Rendering;

public sealed class EchoEnvironmentVariantSet : MonoBehaviour
{
    [SerializeField] private GameObject[] variants;
    [SerializeField] private GameObject highQualityOnly;

    public int VariantCount => variants != null ? variants.Length : 0;
    public int ActiveVariantIndex { get; private set; } = -1;

    public void Initialize(GameObject[] visualVariants, GameObject highOnly)
    {
        variants = visualVariants;
        highQualityOnly = highOnly;
        ApplyQuality(VisualQualityController.Current);
    }

    public void SelectFor(int runSeed, float routeDistance,
        int preferredVariantIndex = -1)
    {
        int selected = preferredVariantIndex >= 0
                       && preferredVariantIndex < VariantCount
            ? preferredVariantIndex
            : SelectVariantIndex(runSeed, routeDistance, VariantCount);
        ActiveVariantIndex = selected;
        if (variants == null) return;
        for (int i = 0; i < variants.Length; i++)
        {
            GameObject variant = variants[i];
            bool active = i == selected;
            if (variant != null && variant.activeSelf != active)
                variant.SetActive(active);
        }
    }

    public void ApplyQuality(VisualQuality quality)
    {
        bool high = quality == VisualQuality.High;
        if (highQualityOnly != null
            && highQualityOnly.activeSelf != high)
            highQualityOnly.SetActive(high);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            renderer.shadowCastingMode = high
                ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = high;
        }
    }

    public static int SelectVariantIndex(int runSeed, float routeDistance,
        int variantCount)
    {
        if (variantCount <= 0) return -1;
        int segmentIndex = Mathf.RoundToInt(routeDistance
            / TrackGeometryStandards.StandardSegmentLength);
        uint hash = unchecked((uint)runSeed);
        hash ^= unchecked((uint)segmentIndex) + 0x9E3779B9u
                + (hash << 6) + (hash >> 2);
        hash ^= hash >> 16;
        hash *= 0x7FEB352Du;
        hash ^= hash >> 15;
        return (int)(hash % (uint)variantCount);
    }

    private void OnEnable()
    {
        VisualQualityController.Changed += ApplyQuality;
    }

    private void OnDisable()
    {
        VisualQualityController.Changed -= ApplyQuality;
    }
}
