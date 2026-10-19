using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class InstallMemoryPulseShard
{
    public const string ModelPath =
        "Assets/Art/Pickups/MemoryPulseShard/Models/MemoryPulseShard_B.fbx";
    public const string PrefabPath =
        "Assets/Resources/Art/Pickups/MemoryPulseShard_B.prefab";
    public const string MaterialPath =
        "Assets/Resources/Materials/EchoCollectible.mat";
    public const string CoinPrefabPath = "Assets/Prefabs/Coin.prefab";

    [MenuItem("Tools/EchoRun/Art/Install Memory Pulse Shard")]
    public static void InstallAndValidate()
    {
        EnsureFolder("Assets/Resources/Art/Pickups");
        EchoGameplayArtBuilder.Build();
        ConfigureModelImporter();

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        MeshFilter source = model != null
            ? model.GetComponentInChildren<MeshFilter>(true)
            : null;
        if (source == null || source.sharedMesh == null)
            throw new InvalidOperationException(
                "Authored Blender memory pulse FBX is missing a mesh.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
            throw new InvalidOperationException("Memory pulse material is missing.");
        CreateVisualPrefab(source.sharedMesh, material);
        ConfigureCoinPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateInstalledAssets();
        Debug.Log("MEMORY_PULSE_SHARD_INSTALL_OK variant=B source=blender "
            + "renderer=1 "
            + "materials=1 triangles=" + source.sharedMesh.triangles.Length / 3
            + " sway=12 rotationPhaseSpeed=225 bob=0.06 scan=1.5");
    }

    public static void ValidateInstalledAssets()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        MeshFilter authored = model != null
            ? model.GetComponentInChildren<MeshFilter>(true)
            : null;
        if (authored == null || authored.sharedMesh == null)
            throw new InvalidOperationException("Blender FBX was not imported.");
        if (authored.sharedMesh.subMeshCount != 1
            || authored.sharedMesh.triangles.Length / 3 > 4500)
        {
            throw new InvalidOperationException(
                "Blender FBX exceeds the one-mesh production budget.");
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("Memory pulse prefab is missing.");
        if (prefab.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("Visual prefab must be collider-free.");

        MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>(true);
        MeshRenderer renderer = prefab.GetComponentInChildren<MeshRenderer>(true);
        if (filter == null || filter.sharedMesh == null || renderer == null)
            throw new InvalidOperationException("Memory pulse mesh binding is incomplete.");
        if (filter.sharedMesh != authored.sharedMesh)
            throw new InvalidOperationException(
                "Runtime prefab is not bound to the authored Blender mesh.");
        if (renderer.sharedMaterials.Length != 1)
            throw new InvalidOperationException("Memory pulse must use one material.");

        Vector3 size = filter.sharedMesh.bounds.size;
        if (size.x < 0.85f || size.x > 1.08f
            || size.y < 0.12f || size.y > 0.22f
            || size.z < 0.90f || size.z > 1.12f)
        {
            throw new InvalidOperationException(
                "Memory pulse bounds are outside the pickup readability budget: "
                + size);
        }

        Color[] colors = filter.sharedMesh.colors;
        bool hasFrame = false;
        bool hasCore = false;
        bool hasAccent = false;
        for (int index = 0; index < colors.Length; index++)
        {
            hasFrame |= colors[index].r > 0.9f;
            hasCore |= colors[index].g > 0.9f;
            hasAccent |= colors[index].b > 0.9f;
        }
        if (colors.Length != filter.sharedMesh.vertexCount
            || !hasFrame || !hasCore || !hasAccent)
        {
            throw new InvalidOperationException(
                "Blender vertex masks were not preserved by FBX import.");
        }

        GameObject coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CoinPrefabPath);
        BoxCollider trigger = coinPrefab != null
            ? coinPrefab.GetComponent<BoxCollider>()
            : null;
        if (trigger == null || !trigger.isTrigger)
            throw new InvalidOperationException("Coin gameplay root was not preserved.");
        GameObject motionHost = UnityEngine.Object.Instantiate(coinPrefab);
        motionHost.name = "CoinMotionValidation";
        motionHost.hideFlags = HideFlags.HideAndDontSave;
        motionHost.SetActive(false);
        try
        {
            Coin coin = Coin.EnsureRuntimeContract(motionHost);
            Collider runtimeTrigger = motionHost.GetComponent<Collider>();
            if (runtimeTrigger == null || !runtimeTrigger.isTrigger)
                throw new InvalidOperationException(
                    "Coin runtime trigger contract was not preserved.");
            if (coin.rotateSpeed < 200f || coin.rotateSpeed > 260f
                || coin.yawAmplitude < 8f || coin.yawAmplitude > 14f
                || coin.bobHeight < 0.05f || coin.bobHeight > 0.08f)
            {
                throw new InvalidOperationException(
                    "Coin motion is outside the memory pulse specification.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(motionHost);
        }
    }

    private static void ConfigureModelImporter()
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ModelPath) == null)
        {
            AssetDatabase.ImportAsset(ModelPath,
                ImportAssetOptions.ForceSynchronousImport
                | ImportAssetOptions.ForceUpdate);
        }

        ModelImporter importer = AssetImporter.GetAtPath(ModelPath)
            as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException(
                "Missing authored Blender FBX: " + ModelPath);

        importer.globalScale = 0.92f;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        importer.isReadable = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
    }

    private static void CreateVisualPrefab(Mesh mesh, Material material)
    {
        GameObject root = new GameObject("MemoryPulseShard_B");
        try
        {
            GameObject visual = new GameObject("BlenderVisual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureCoinPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CoinPrefabPath);
        try
        {
            Coin coin = root.GetComponent<Coin>();
            if (coin == null)
            {
                BoxCollider trigger = root.GetComponent<BoxCollider>();
                if (root.name != "Coin" || !root.CompareTag("Coin")
                    || trigger == null || !trigger.isTrigger)
                {
                    throw new InvalidOperationException(
                        "Coin gameplay root cannot be safely repaired.");
                }

                Debug.LogWarning(
                    "Coin prefab uses a non-serializable legacy script "
                    + "binding; runtime spawning will attach Coin safely.");
                return;
            }
            ConfigureCoinMotion(coin);
            EditorUtility.SetDirty(coin);
            PrefabUtility.SaveAsPrefabAsset(root, CoinPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureCoinMotion(Coin coin)
    {
        coin.rotateSpeed = 225f;
        coin.yawAmplitude = 12f;
        coin.bobSpeed = 3.5f;
        coin.bobHeight = 0.06f;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }
}
