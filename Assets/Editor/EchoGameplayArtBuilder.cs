using UnityEditor;
using UnityEngine;

public static class EchoGameplayArtBuilder
{
    private const string MaterialPath =
        "Assets/Resources/Materials/EchoCollectible.mat";

    [MenuItem("Tools/Ensure Echo Gameplay Art")]
    public static void Build()
    {
        Shader shader = Shader.Find("EchoRun/Collectible");
        if (shader == null)
        {
            Debug.LogError("EchoRun/Collectible shader is missing.");
            return;
        }
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "EchoCollectible" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }
        material.SetColor("_FrameColor", new Color32(217, 139, 50, 255));
        material.SetColor("_FrameHighlight", new Color32(255, 234, 194, 255));
        material.SetColor("_CoreColor", new Color32(255, 178, 74, 255));
        material.SetColor("_CoreEdgeColor", new Color32(255, 241, 214, 255));
        material.SetColor("_AccentColor", new Color32(31, 231, 231, 255));
        material.SetColor("_ContractColor", new Color(1f, 0.34f, 0.30f, 1f));
        material.SetFloat("_EmissionStrength", 2.05f);
        material.SetFloat("_FrameEmissionStrength", 0.72f);
        material.SetFloat("_AccentEmissionStrength", 0.70f);
        material.SetFloat("_ScanPeriod", 1.5f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
