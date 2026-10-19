using UnityEngine;

public static class RunnerAppearanceService
{
    private static readonly int DarkColor = Shader.PropertyToID("_DarkColor");
    private static readonly int LightColor = Shader.PropertyToID("_LightColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    public static int Apply(Transform model, Color dark, Color light,
        Color emission)
    {
        if (model == null) return 0;

        int changedSlots = 0;
        bool orangeOutfit = model.Find("OrangeEchoOutfit") != null;
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null) continue;

                if (orangeOutfit)
                {
                    // Only authored accent cloth changes; skin, lining, rubber
                    // and the relay remain independent material families.
                    if (!material.name.StartsWith("OE_Jacket_Accent")) continue;
                    properties.Clear();
                    renderer.GetPropertyBlock(properties, index);
                    properties.SetColor(ColorProperty, light);
                    renderer.SetPropertyBlock(properties, index);
                    changedSlots++;
                    continue;
                }

                bool techMaterial = material.HasProperty(DarkColor)
                                    || material.HasProperty(LightColor)
                                    || material.HasProperty(EmissionColor);
                bool legacyOutfit = IsLegacyOutfitMaterial(material.name);
                if (!techMaterial && !legacyOutfit) continue;

                ResolveMaterialPalette(material.name, dark, light, emission,
                    out Color materialDark, out Color materialLight,
                    out Color materialEmission);

                properties.Clear();
                renderer.GetPropertyBlock(properties, index);
                if (material.HasProperty(DarkColor))
                    properties.SetColor(DarkColor, materialDark);
                if (material.HasProperty(LightColor))
                    properties.SetColor(LightColor, materialLight);
                if (material.HasProperty(EmissionColor))
                    properties.SetColor(EmissionColor, materialEmission);
                if (!techMaterial && material.HasProperty(BaseColor))
                    properties.SetColor(BaseColor,
                        IsDarkMaterial(material.name) ? dark : light);
                if (!techMaterial && material.HasProperty(ColorProperty))
                    properties.SetColor(ColorProperty,
                        IsDarkMaterial(material.name) ? dark : light);
                renderer.SetPropertyBlock(properties, index);
                changedSlots++;
            }
        }
        return changedSlots;
    }

    public static void ResolveMaterialPalette(
        string materialName, Color dark, Color light, Color emission,
        out Color materialDark, out Color materialLight,
        out Color materialEmission)
    {
        string name = materialName ?? string.Empty;
        if (name.Contains("Exo_MAT"))
        {
            materialDark = ScaleRgb(dark, 1.05f, 0.004f);
            materialLight = ScaleRgb(light, 1.20f, 0.012f);
            materialEmission = ScaleRgb(emission, 0.55f, 0f);
            return;
        }

        if (name.Contains("Body_MAT"))
        {
            materialDark = ScaleRgb(dark, 0.55f, 0f);
            materialLight = ScaleRgb(light, 0.62f, 0f);
            materialEmission = ScaleRgb(emission, 0.30f, 0f);
            return;
        }

        if (name.Contains("Eye") || name.Contains("Brow"))
        {
            materialDark = ScaleRgb(dark, 0.42f, 0f);
            materialLight = ScaleRgb(light, 0.50f, 0f);
            materialEmission = ScaleRgb(emission, 0.25f, 0f);
            return;
        }

        materialDark = dark;
        materialLight = light;
        materialEmission = emission;
    }

    private static Color ScaleRgb(Color color, float scale, float offset)
    {
        return new Color(
            Mathf.Clamp01(color.r * scale + offset),
            Mathf.Clamp01(color.g * scale + offset),
            Mathf.Clamp01(color.b * scale + offset),
            color.a);
    }

    private static bool IsLegacyOutfitMaterial(string materialName)
    {
        return materialName.Contains("Cloth")
               || materialName.Contains("Pants")
               || materialName.Contains("Shoe")
               || materialName.Contains("Accent");
    }

    private static bool IsDarkMaterial(string materialName)
    {
        return materialName.Contains("Pants") || materialName.Contains("Shoe");
    }
}
