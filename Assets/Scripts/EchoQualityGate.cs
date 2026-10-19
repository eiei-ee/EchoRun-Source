using UnityEngine;

public sealed class EchoQualityGate : MonoBehaviour
{
    [SerializeField] private GameObject highQualityOnly;

    public void Initialize(GameObject highOnly)
    {
        highQualityOnly = highOnly;
        ApplyQuality(VisualQualityController.Current);
    }

    public void ApplyQuality(VisualQuality quality)
    {
        bool active = quality == VisualQuality.High;
        if (highQualityOnly != null && highQualityOnly.activeSelf != active)
            highQualityOnly.SetActive(active);
    }

    private void OnEnable()
    {
        VisualQualityController.Changed += ApplyQuality;
        ApplyQuality(VisualQualityController.Current);
    }

    private void OnDisable()
    {
        VisualQualityController.Changed -= ApplyQuality;
    }
}
