using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class InstallEchoSideEnergyStation
{
    private const string ModelPath =
        "Assets/Art/Environment/EchoSideEnergyStation/Models/EchoSideEnergyStation.fbx";
    private const string PrefabPath =
        "Assets/Resources/Art/Environment/EchoSideEnergyStation.prefab";

    public static void InstallAndBake()
    {
        Install();
        BuildScene.BakeEnvironmentVariants();
    }

    [MenuItem("Tools/EchoRun/Art/Install Side Energy Station")]
    public static void Install()
    {
        ConfigureModelImporter();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
            throw new InvalidOperationException("Missing authored model: " + ModelPath);

        GameObject root = new GameObject("EchoSideEnergyStation");
        try
        {
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            // Blender's authored Z-up basis is preserved in the imported FBX.
            // Convert it once in the production Prefab so its bottom-center
            // pivot and meter scale stay unchanged.
            visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            visual.transform.localScale = Vector3.one;

            RemapMaterials(root);
            ValidateBounds(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Installed authored side energy station prefab: " + PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureModelImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("FBX importer unavailable: " + ModelPath);

        importer.globalScale = 1f;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        importer.isReadable = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
    }

    private static void RemapMaterials(GameObject root)
    {
        Material structure = LoadMaterial("EchoStructure");
        Material depth = LoadMaterial("EchoDepth");
        Material cyan = LoadMaterial("EchoCyan");
        Material gold = LoadMaterial("EchoGold");

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException("Side station FBX contains no renderers.");

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string materialName = materials[i] != null ? materials[i].name : "";
                if (materialName.IndexOf("Cyan", StringComparison.OrdinalIgnoreCase) >= 0)
                    materials[i] = cyan;
                else if (materialName.IndexOf("Gold", StringComparison.OrdinalIgnoreCase) >= 0)
                    materials[i] = gold;
                else if (materialName.IndexOf("Depth", StringComparison.OrdinalIgnoreCase) >= 0)
                    materials[i] = depth;
                else
                    materials[i] = structure;
            }

            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static Material LoadMaterial(string name)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Prefabs/Materials/" + name + ".mat");
        if (material == null)
            throw new InvalidOperationException("Missing shared palette material: " + name);
        return material;
    }

    private static void ValidateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        Vector3 size = bounds.size;
        if (size.x < 4.2f || size.x > 4.8f ||
            size.y < 5.2f || size.y > 5.7f ||
            size.z < 3.1f || size.z > 3.8f)
        {
            throw new InvalidOperationException(
                "Unexpected side station import bounds: " + size.ToString("F3"));
        }

        if (root.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("Decorative side station must not add colliders.");

        Debug.Log("Side station import bounds: " + size.ToString("F3"));
    }
}
