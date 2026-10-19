using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class InstallEchoRelayKit
{
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";
    private const string ModelPath =
        "Assets/Models/Mixamo/ExoGray/ExoGray_TPose.fbx";
    private const string ControllerPath =
        "Assets/Animations/HumanMotion/EchoRunHuman.controller";
    private const string AssetFolder =
        "Assets/Art/Characters/EchoRunner/IdentityKit";
    private const string MaterialFolder = AssetFolder + "/Materials";
    private const string MeshFolder = AssetFolder + "/Meshes";
    private const string PrefabPath = AssetFolder + "/EchoRelayKit.prefab";
    private static readonly Vector3 AnchorPosition =
        new Vector3(0f, 1.39f, -0.19f);

    [MenuItem("Tools/Echo Runner/Install Echo Relay Kit")]
    public static void Install()
    {
        GameObject prefab = BuildPrefab();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("player");
        Transform model = player != null
            ? player.transform.Find("CharacterModel")
            : null;
        if (model == null)
            throw new InvalidOperationException("Scene CharacterModel was not found.");

        RemoveExistingKit(model);
        AttachPrefab(model, prefab);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save the relay-kit binding.");

        AssetDatabase.SaveAssets();
        ValidateInstalledScene();
        Debug.Log("ECHO_RELAY_KIT_INSTALL_OK");
    }

    public static void ValidateInstalledScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("player");
        Transform model = player != null
            ? player.transform.Find("CharacterModel")
            : null;
        Transform kit = FindDescendant(model, "EchoRelayKit");
        if (model == null || kit == null)
            throw new InvalidOperationException("Echo relay kit is not bound to CharacterModel.");

        Animator animator = model.GetComponent<Animator>();
        Transform chest = animator != null && animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.Chest)
            : FindDescendant(model, "mixamorig:Spine2");
        if (chest == null || kit.parent != chest)
            throw new InvalidOperationException("Echo relay kit is not attached to the chest bone.");

        Renderer[] renderers = kit.GetComponentsInChildren<Renderer>(true);
        Collider[] colliders = kit.GetComponentsInChildren<Collider>(true);
        if (renderers.Length != 3)
            throw new InvalidOperationException(
                "Echo relay kit must keep its three-renderer mobile budget.");
        if (colliders.Length != 0)
            throw new InvalidOperationException("Echo relay kit must not alter collision.");

        int vertices = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshFilter filter = renderers[i].GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                throw new InvalidOperationException("Echo relay kit mesh binding is missing.");
            vertices += filter.sharedMesh.vertexCount;
        }

        Debug.Log($"ECHO_RELAY_KIT_VALIDATE_OK renderers={renderers.Length} " +
                  $"vertices={vertices} colliders={colliders.Length}");
    }

    [MenuItem("Tools/Echo Runner/Capture Echo Relay Kit Comparison")]
    public static void CaptureComparison()
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (modelAsset == null || controller == null || prefab == null)
            throw new FileNotFoundException("Relay-kit capture assets are missing.");

        GameObject model = UnityEngine.Object.Instantiate(modelAsset);
        model.name = "EchoRelayKitCaptureModel";
        model.SetActive(false);
        Animator animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        CharacterAnimator driver = model.AddComponent<CharacterAnimator>();
        driver.useHumanoidRig = true;
        model.SetActive(true);
        animator.Rebind();
        animator.Update(0f);
        driver.SetExternalDriver();
        for (int i = 0; i < 28; i++)
        {
            animator.Update(1f / 60f);
            driver.ApplyExternalMotion(
                false, false, Vector3.forward, 10f, 1f / 60f);
        }

        GameObject lightObject = new GameObject("EchoRelayKitCaptureLight");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.45f;
        light.transform.rotation = Quaternion.Euler(38f, -28f, 0f);

        GameObject fillObject = new GameObject("EchoRelayKitCaptureFill");
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.18f, 0.55f, 0.82f);
        fill.intensity = 0.42f;
        fill.transform.rotation = Quaternion.Euler(20f, 155f, 0f);

        GameObject cameraObject = new GameObject("EchoRelayKitCaptureCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.018f, 0.03f, 0.05f, 1f);

        string outputFolder = Path.Combine(
            Directory.GetCurrentDirectory(), "Logs", "EchoRelayKit");
        Directory.CreateDirectory(outputFolder);
        CaptureViews(camera, outputFolder, "before");
        AttachPrefab(model.transform, prefab);
        CaptureViews(camera, outputFolder, "after");

        UnityEngine.Object.DestroyImmediate(cameraObject);
        UnityEngine.Object.DestroyImmediate(fillObject);
        UnityEngine.Object.DestroyImmediate(lightObject);
        UnityEngine.Object.DestroyImmediate(model);
        Debug.Log("ECHO_RELAY_KIT_CAPTURE_OK " + outputFolder);
    }

    private static GameObject BuildPrefab()
    {
        EnsureFolder(AssetFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(MeshFolder);

        Material shell = LoadOrCreateMaterial(
            MaterialFolder + "/EchoRelayShell.mat",
            new Color(0.025f, 0.065f, 0.11f, 1f),
            new Color(0f, 0f, 0f, 1f), 0.55f, 0.48f);
        Material signal = LoadOrCreateMaterial(
            MaterialFolder + "/EchoRelaySignal.mat",
            new Color(0.02f, 0.42f, 0.56f, 1f),
            new Color(0f, 1.5f, 2.3f, 1f), 0.18f, 0.62f);
        Material latch = LoadOrCreateMaterial(
            MaterialFolder + "/EchoRelayLatch.mat",
            new Color(0.62f, 0.12f, 0.07f, 1f),
            new Color(1.25f, 0.18f, 0.05f, 1f), 0.24f, 0.5f);

        Mesh shellMesh = BuildMesh(MeshFolder + "/EchoRelayShell.asset",
            new[]
            {
                new Shape(PrimitiveType.Capsule, Vector3.zero,
                    Quaternion.identity, new Vector3(0.34f, 0.25f, 0.12f)),
                new Shape(PrimitiveType.Capsule, new Vector3(-0.20f, 0.13f, 0.01f),
                    Quaternion.Euler(0f, 0f, 28f), new Vector3(0.075f, 0.17f, 0.065f)),
                new Shape(PrimitiveType.Capsule, new Vector3(0.20f, 0.13f, 0.01f),
                    Quaternion.Euler(0f, 0f, -28f), new Vector3(0.075f, 0.17f, 0.065f)),
                new Shape(PrimitiveType.Cube, new Vector3(0f, -0.21f, -0.005f),
                    Quaternion.identity, new Vector3(0.25f, 0.08f, 0.13f)),
                new Shape(PrimitiveType.Capsule, new Vector3(0.24f, 0.31f, 0.005f),
                    Quaternion.Euler(0f, 0f, -8f), new Vector3(0.045f, 0.10f, 0.045f))
            });
        Mesh signalMesh = BuildMesh(MeshFolder + "/EchoRelaySignal.asset",
            new[]
            {
                new Shape(PrimitiveType.Cube, new Vector3(0f, -0.035f, -0.078f),
                    Quaternion.identity, new Vector3(0.055f, 0.31f, 0.026f)),
                new Shape(PrimitiveType.Sphere, new Vector3(0f, 0.13f, -0.09f),
                    Quaternion.identity, new Vector3(0.14f, 0.14f, 0.055f)),
                new Shape(PrimitiveType.Sphere, new Vector3(0.25f, 0.405f, -0.008f),
                    Quaternion.identity, new Vector3(0.075f, 0.075f, 0.055f))
            });
        Mesh latchMesh = BuildMesh(MeshFolder + "/EchoRelayLatch.asset",
            new[]
            {
                new Shape(PrimitiveType.Cube, new Vector3(0.105f, -0.205f, -0.08f),
                    Quaternion.Euler(0f, 0f, -12f), new Vector3(0.075f, 0.07f, 0.028f))
            });

        GameObject root = new GameObject("EchoRelayKit");
        AddMesh(root.transform, "RelayShell", shellMesh, shell, true);
        AddMesh(root.transform, "PulseSignal", signalMesh, signal, false);
        AddMesh(root.transform, "MemoryLatch", latchMesh, latch, false);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        if (prefab == null)
            throw new InvalidOperationException("Could not create EchoRelayKit prefab.");
        return prefab;
    }

    private static void AttachPrefab(Transform model, GameObject prefab)
    {
        Animator animator = model.GetComponent<Animator>();
        Transform chest = animator != null && animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.Chest)
            : FindDescendant(model, "mixamorig:Spine2");
        if (chest == null)
            throw new InvalidOperationException("Character chest bone was not found.");

        GameObject instance =
            (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "EchoRelayKit";
        Transform kit = instance.transform;
        kit.position = model.TransformPoint(AnchorPosition);
        kit.rotation = model.rotation;
        kit.localScale = model.lossyScale;
        kit.SetParent(chest, true);
    }

    private static void RemoveExistingKit(Transform model)
    {
        Transform existing = FindDescendant(model, "EchoRelayKit");
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
    }

    private static void CaptureViews(Camera camera, string folder, string suffix)
    {
        camera.fieldOfView = 34f;
        CaptureView(camera, new Vector3(0f, 1.35f, -3.25f),
            new Vector3(0f, 1.27f, 0f),
            Path.Combine(folder, "rear-close-" + suffix + ".png"));
        camera.fieldOfView = 56f;
        CaptureView(camera, new Vector3(0f, 4.6f, -8.2f),
            new Vector3(0f, 1f, 5f),
            Path.Combine(folder, "game-camera-" + suffix + ".png"));
    }

    private static void CaptureView(Camera camera, Vector3 position,
        Vector3 target, string path)
    {
        const int width = 800;
        const int height = 720;
        RenderTexture renderTexture = new RenderTexture(
            width, height, 24, RenderTextureFormat.ARGB32);
        camera.transform.position = position;
        camera.transform.LookAt(target);
        camera.targetTexture = renderTexture;
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());

        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        renderTexture.Release();
        UnityEngine.Object.DestroyImmediate(renderTexture);
    }

    private static Mesh BuildMesh(string path, Shape[] shapes)
    {
        List<CombineInstance> combine = new List<CombineInstance>(shapes.Length);
        for (int i = 0; i < shapes.Length; i++)
        {
            Shape shape = shapes[i];
            GameObject primitive = GameObject.CreatePrimitive(shape.type);
            Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            combine.Add(new CombineInstance
            {
                mesh = mesh,
                transform = Matrix4x4.TRS(
                    shape.position, shape.rotation, shape.scale)
            });
            UnityEngine.Object.DestroyImmediate(primitive);
        }

        Mesh generated = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
        generated.CombineMeshes(combine.ToArray(), true, true, false);
        generated.RecalculateBounds();
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }

        EditorUtility.CopySerialized(generated, existing);
        UnityEngine.Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static void AddMesh(Transform parent, string name, Mesh mesh,
        Material material, bool castsShadows)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = castsShadows
            ? ShadowCastingMode.On
            : ShadowCastingMode.Off;
        renderer.receiveShadows = castsShadows;
    }

    private static Material LoadOrCreateMaterial(string path, Color color,
        Color emission, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("Standard shader was not found.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        material.SetColor("_EmissionColor", emission);
        if (emission.maxColorComponent > 0f)
            material.EnableKeyword("_EMISSION");
        else
            material.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null) return null;
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
            if (descendants[i].name == name) return descendants[i];
        return null;
    }

    private static void EnsureFolder(string folder)
    {
        string current = "Assets";
        string[] parts = folder.Substring("Assets/".Length).Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private readonly struct Shape
    {
        public readonly PrimitiveType type;
        public readonly Vector3 position;
        public readonly Quaternion rotation;
        public readonly Vector3 scale;

        public Shape(PrimitiveType type, Vector3 position,
            Quaternion rotation, Vector3 scale)
        {
            this.type = type;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }
    }
}
