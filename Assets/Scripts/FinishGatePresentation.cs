using UnityEngine;

// The authored gate owns only presentation; course completion stays in GameManager.
public sealed class FinishGatePresentation : MonoBehaviour
{
    public const string ResourcePath = "CityV7/FinishGate";
    public Renderer[] signalRenderers = new Renderer[0];
    public Light[] arrivalLights = new Light[0];
    public Color distantEmission = new Color(.025f, .24f, .29f);
    public Color arrivalEmission = new Color(.06f, .80f, .95f);
    private MaterialPropertyBlock properties;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public void SetApproach(float progress, bool lightsEnabled)
    {
        if (properties == null) properties = new MaterialPropertyBlock();
        Color emission = Color.Lerp(distantEmission, arrivalEmission, Mathf.Clamp01(progress));
        foreach (Renderer signal in signalRenderers)
        {
            if (signal == null) continue;
            signal.GetPropertyBlock(properties);
            properties.SetColor(EmissionColor, emission);
            signal.SetPropertyBlock(properties);
        }
        foreach (Light light in arrivalLights)
            if (light != null) light.enabled = lightsEnabled;
    }

    private void OnDisable()
    {
        foreach (Light light in arrivalLights)
            if (light != null) light.enabled = false;
    }
}
