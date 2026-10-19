using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class InstallEchoMegacityDistricts
{
    private const string ModelAPath =
        "Assets/Art/Environment/EchoMegacityDistricts/Models/EchoMegacityDistrictA.fbx";
    private const string ModelBPath =
        "Assets/Art/Environment/EchoMegacityDistricts/Models/EchoMegacityDistrictB.fbx";
    private const string PrefabAPath =
        "Assets/Resources/Art/Environment/EchoMegacityDistrictA.prefab";
    private const string PrefabBPath =
        "Assets/Resources/Art/Environment/EchoMegacityDistrictB.prefab";

    public static void InstallAndBake()
    {
        Install();
        BuildScene.BakeEnvironmentVariants();
    }

    [MenuItem("Tools/EchoRun/Art/Install Megacity Districts")]
    public static void Install()
    {
        InstallVariant("A", ModelAPath, PrefabAPath);
        InstallVariant("B", ModelBPath, PrefabBPath);
        AssetDatabase.SaveAssets();
        Debug.Log("Installed authored megacity district prefabs.");
    }

    private static void InstallVariant(string variant, string modelPath,
        string prefabPath)
    {
        ConfigureModelImporter(modelPath);
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
            throw new InvalidOperationException("Missing authored model: " + modelPath);

        GameObject root = new GameObject("EchoMegacityDistrict" + variant);
        try
        {
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            visual.transform.localScale = Vector3.one;

            RemapMaterials(root);
            Validate(root, variant);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureModelImporter(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("FBX importer unavailable: " + path);

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
        Material coral = LoadMaterial("EchoCoral");

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException("Megacity FBX contains no renderers.");

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
                else if (materialName.IndexOf("Coral", StringComparison.OrdinalIgnoreCase) >= 0)
                    materials[i] = coral;
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
            throw new InvalidOperationException(
                "Missing shared palette material: " + name);
        return material;
    }

    private static void Validate(GameObject root, string variant)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length != 5)
            throw new InvalidOperationException(
                "District " + variant + " must stay at five material batches, got "
                + renderers.Length + ".");

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 size = bounds.size;
        if (size.x < 14.5f || size.x > 15.5f ||
            size.y < 10.5f || size.y > 13.5f ||
            size.z < 5.5f || size.z > 6.5f)
        {
            throw new InvalidOperationException(
                "Unexpected district " + variant + " bounds: " + size.ToString("F3"));
        }

        if (root.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException(
                "Decorative megacity district must not add colliders.");

        Debug.Log("Megacity district " + variant + " bounds: "
                  + size.ToString("F3"));
    }
}
