using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BuildScene
{
    // Cached shader found on first use
    private static Shader _cachedShader;

    [MenuItem("Tools/Build Scene")]
    public static void Build()
    {
        Debug.Log("=== BUILD SCENE START ===");

        // Detect render pipeline and find working shader first
        DetectRenderPipeline();

        AddTag("Coin");
        AddTag("Obstacle");
        AddLayer("Ground");

        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/Materials");
        EnsureFolder("Assets/Resources/Materials");
        RemoveMissingScriptsFromScene();
        CleanupRuntimeUI();

        CreateCharacterMaterials();
        CreateSkyMaterial();
        CreateShaderRetentionMaterial();
        ConfigureMenuBackgroundTexture();
        EnsureMainCamera();
        CreatePlayer();
        CreateGroundPlane();
        CreateManagers();
        ConfigurePlayer();
        ConfigureGround();
        CreateCameraFollow();
        CreateLighting();
        CreateCoinPrefab();
        CreateObstaclePrefab();
        EchoGameplayArtBuilder.Build();
        CreateTrackSegmentPrefab();
        CreateTurnSegmentPrefabs();
        BakeEnvironmentVariants();
        EnsureFormalTrackManagerBindings();
        EchoHudPrefabBuilder.Build();
        CreateUICanvas();

       AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
       AssetDatabase.Refresh();
        Debug.Log("=== BUILD COMPLETE — Save scene (Ctrl+S), then Play ===");
    }

    // ── render pipeline detection ──────────────────────

    [MenuItem("Tools/Clean Runtime UI From Scene")]
    public static void CleanRuntimeUIFromScene()
    {
        CleanupRuntimeUI();
        CreateUICanvas();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Runtime UI scene objects removed. UIManager recreates them in Play mode.");
    }

    static void CleanupRuntimeUI()
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null)
                Object.DestroyImmediate(canvas.gameObject);
        }

        GameObject legacyOverlay = GameObject.Find("HUDOverlay");
        if (legacyOverlay != null)
            Object.DestroyImmediate(legacyOverlay);
    }

    static void DetectRenderPipeline()
    {
        var rpAsset = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset;
        string rpName = rpAsset != null ? rpAsset.GetType().Name : "Built-in Render Pipeline";
        Debug.Log($"Detected render pipeline: {rpName}");

        // Try to find a working shader
        _cachedShader = Shader.Find("Universal Render Pipeline/Lit");
        if (_cachedShader != null) { Debug.Log("Using URP/Lit shader"); return; }

        _cachedShader = Shader.Find("Standard");
        if (_cachedShader != null) { Debug.Log("Using Standard shader"); return; }

        _cachedShader = Shader.Find("Mobile/Diffuse");
        if (_cachedShader != null) { Debug.Log("Using Mobile/Diffuse shader"); return; }

        // Last resort: get from a primitive
        GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _cachedShader = tmp.GetComponent<MeshRenderer>().sharedMaterial.shader;
        Object.DestroyImmediate(tmp);
        Debug.Log($"Using shader from primitive: {(_cachedShader != null ? _cachedShader.name : "NULL")}");
    }

    // ── character model builders ──────────────────────

    static GameObject CreateHumanoidCharacterModel(GameObject player, GameObject prefab)
    {
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
        model.name = "CharacterModel";
        model.transform.localPosition = new Vector3(0, -1f, 0);
        model.transform.localRotation = Quaternion.identity;

        CharacterAnimator ca = model.GetComponent<CharacterAnimator>();
        if (ca == null) ca = model.AddComponent<CharacterAnimator>();
        SerializedObject caSo = new SerializedObject(ca);
        caSo.FindProperty("useHumanoidRig").boolValue = false;
        caSo.FindProperty("leftUpperArm").objectReferenceValue = FindDescendant(model.transform, "LeftUpperArm");
        caSo.FindProperty("rightUpperArm").objectReferenceValue = FindDescendant(model.transform, "RightUpperArm");
        caSo.FindProperty("leftLowerArm").objectReferenceValue = FindDescendant(model.transform, "LeftLowerArm");
        caSo.FindProperty("rightLowerArm").objectReferenceValue = FindDescendant(model.transform, "RightLowerArm");
        caSo.FindProperty("leftUpperLeg").objectReferenceValue = FindDescendant(model.transform, "LeftUpperLeg");
        caSo.FindProperty("rightUpperLeg").objectReferenceValue = FindDescendant(model.transform, "RightUpperLeg");
        caSo.FindProperty("leftFoot").objectReferenceValue = FindDescendant(model.transform, "LeftFoot");
        caSo.FindProperty("rightFoot").objectReferenceValue = FindDescendant(model.transform, "RightFoot");
        caSo.FindProperty("bodyTransform").objectReferenceValue = FindDescendant(model.transform, "Spine");
        caSo.ApplyModifiedProperties();

        CapsuleCollider cc = player.GetComponent<CapsuleCollider>();
        if (cc != null)
        {
            cc.height = 2.2f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0, 1f, 0);
        }

        Debug.Log("Using humanoid character model");
        return model;
    }

    static Transform FindDescendant(Transform root, string name)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
            if (descendants[i].name == name) return descendants[i];
        return null;
    }

    static GameObject CreateProceduralCharacterModel(GameObject player)
    {
        // Materials
        Material skinMat = LoadOrMakeMat("CharacterSkinMat", new Color(0.82f, 0.58f, 0.42f));
        Material clothMat = LoadOrMakeMat("CharacterClothMat", new Color(0.18f, 0.28f, 0.38f));
        Material pantsMat = LoadOrMakeMat("CharacterPantsMat", new Color(0.1f, 0.15f, 0.21f));
        Material shoeMat = LoadOrMakeMat("CharacterShoeMat", new Color(0.82f, 0.22f, 0.14f));
        Material eyeMat = LoadOrMakeMat("CharacterEyeMat", new Color(0.08f, 0.68f, 0.74f));
        Material pupilMat = LoadOrMakeMat("CharacterPupilMat", new Color(0.008f, 0.015f, 0.025f));
        Material accentMat = LoadOrMakeMat("CharacterAccentMat", new Color(0.96f, 0.54f, 0.1f));

        GameObject model = new GameObject("CharacterModel");
        model.transform.SetParent(player.transform);
        model.transform.localPosition = new Vector3(0, -1f, 0);
        model.AddComponent<CharacterAnimator>();

        // Helper: create part, strip collider, assign material
        System.Func<string, PrimitiveType, Vector3, Vector3, Material, GameObject> P =
            (name, shape, pos, scale, mat) =>
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (mat != null) go.GetComponent<MeshRenderer>().material = mat;
            return go;
        };

        // ── Body ──
        GameObject pelvis = P("Pelvis", PrimitiveType.Capsule,
            new Vector3(0, 0.95f, 0), new Vector3(0.55f, 0.28f, 0.4f), pantsMat);
        pelvis.transform.SetParent(model.transform, false);

        GameObject torso = P("Torso", PrimitiveType.Capsule,
            new Vector3(0, 1.35f, 0), new Vector3(0.62f, 0.52f, 0.5f), clothMat);
        torso.transform.SetParent(model.transform, false);

        GameObject chestPlate = P("ChestPlate", PrimitiveType.Cube,
            new Vector3(0, 1.42f, 0.46f), new Vector3(0.5f, 0.5f, 0.06f), clothMat);
        chestPlate.transform.SetParent(model.transform, false);
        GameObject chestSignal = P("ChestSignal", PrimitiveType.Cube,
            new Vector3(0, 1.43f, 0.53f), new Vector3(0.22f, 0.06f, 0.025f), accentMat);
        chestSignal.transform.SetParent(model.transform, false);

        GameObject belt = P("SignalBelt", PrimitiveType.Cube,
            new Vector3(0, 1.02f, 0), new Vector3(0.6f, 0.08f, 0.45f), accentMat);
        belt.transform.SetParent(model.transform, false);

        GameObject backCore = P("EchoCore", PrimitiveType.Cylinder,
            new Vector3(0, 1.42f, -0.5f), new Vector3(0.22f, 0.08f, 0.22f), eyeMat);
        backCore.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        backCore.transform.SetParent(model.transform, false);

        GameObject neck = P("Neck", PrimitiveType.Capsule,
            new Vector3(0, 1.85f, 0), new Vector3(0.16f, 0.12f, 0.16f), skinMat);
        neck.transform.SetParent(model.transform, false);

        // ── Head + face ──
        GameObject head = P("Head", PrimitiveType.Sphere,
            new Vector3(0, 2.08f, 0), new Vector3(0.4f, 0.43f, 0.4f), skinMat);
        head.transform.SetParent(model.transform, false);

        GameObject helmetBand = P("HelmetBand", PrimitiveType.Cube,
            new Vector3(0, 2.16f, -0.18f), new Vector3(0.43f, 0.22f, 0.28f), clothMat);
        helmetBand.transform.SetParent(model.transform, false);
        foreach (float side in new[] { -1f, 1f })
        {
            GameObject earpiece = P(side < 0 ? "Earpiece_L" : "Earpiece_R",
                PrimitiveType.Cylinder, new Vector3(side * 0.4f, 2.09f, 0f),
                new Vector3(0.1f, 0.06f, 0.1f), accentMat);
            earpiece.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            earpiece.transform.SetParent(model.transform, false);
        }

        // Eyes (on front of head = Z+)
        foreach (float sx in new[] { -0.12f, 0.12f })
        {
            GameObject eye = P(sx < 0 ? "Eye_L" : "Eye_R", PrimitiveType.Sphere,
                new Vector3(sx, 2.1f, 0.35f), new Vector3(0.08f, 0.08f, 0.04f), eyeMat);
            eye.transform.SetParent(model.transform, false);

            GameObject pupil = P(sx < 0 ? "Pupil_L" : "Pupil_R", PrimitiveType.Sphere,
                new Vector3(0, 0, 0.45f), new Vector3(0.5f, 0.5f, 0.15f), pupilMat);
            pupil.transform.SetParent(eye.transform, false);
        }

        // Mouth
        GameObject mouth = P("Mouth", PrimitiveType.Cube,
            new Vector3(0, 1.98f, 0.41f), new Vector3(0.16f, 0.03f, 0.02f), pupilMat);
        mouth.transform.SetParent(model.transform, false);

        // ── Arms (capsules, skin color for lower, cloth for upper) ──
        float shldY = 1.7f;
        GameObject armUpperL = P("Arm_Upper_L", PrimitiveType.Capsule,
            new Vector3(-0.58f, shldY, 0), new Vector3(0.13f, 0.38f, 0.13f), clothMat);
        armUpperL.transform.SetParent(model.transform, false);
        GameObject shoulderL = P("Shoulder_L", PrimitiveType.Sphere,
            new Vector3(-0.58f, 1.73f, 0), new Vector3(0.19f, 0.16f, 0.2f), accentMat);
        shoulderL.transform.SetParent(model.transform, false);
        GameObject armLowerL = P("Arm_Lower_L", PrimitiveType.Capsule,
            new Vector3(0, -0.42f, 0), new Vector3(0.11f, 0.34f, 0.11f), skinMat);
        armLowerL.transform.SetParent(armUpperL.transform, false);
        GameObject handL = P("Hand_L", PrimitiveType.Sphere,
            new Vector3(0, -0.38f, 0), new Vector3(0.1f, 0.1f, 0.1f), skinMat);
        handL.transform.SetParent(armLowerL.transform, false);

        GameObject armUpperR = P("Arm_Upper_R", PrimitiveType.Capsule,
            new Vector3(0.58f, shldY, 0), new Vector3(0.13f, 0.38f, 0.13f), clothMat);
        armUpperR.transform.SetParent(model.transform, false);
        GameObject shoulderR = P("Shoulder_R", PrimitiveType.Sphere,
            new Vector3(0.58f, 1.73f, 0), new Vector3(0.19f, 0.16f, 0.2f), accentMat);
        shoulderR.transform.SetParent(model.transform, false);
        GameObject armLowerR = P("Arm_Lower_R", PrimitiveType.Capsule,
            new Vector3(0, -0.42f, 0), new Vector3(0.11f, 0.34f, 0.11f), skinMat);
        armLowerR.transform.SetParent(armUpperR.transform, false);
        GameObject handR = P("Hand_R", PrimitiveType.Sphere,
            new Vector3(0, -0.38f, 0), new Vector3(0.1f, 0.1f, 0.1f), skinMat);
        handR.transform.SetParent(armLowerR.transform, false);

        // ── Legs (capsules) ──
        GameObject legUpperL = P("Leg_Upper_L", PrimitiveType.Capsule,
            new Vector3(-0.16f, 0.7f, 0), new Vector3(0.18f, 0.32f, 0.18f), pantsMat);
        legUpperL.transform.SetParent(model.transform, false);
        GameObject legLowerL = P("Leg_Lower_L", PrimitiveType.Capsule,
            new Vector3(0, -0.36f, 0), new Vector3(0.15f, 0.32f, 0.15f), skinMat);
        legLowerL.transform.SetParent(legUpperL.transform, false);
        GameObject footL = P("Foot_L", PrimitiveType.Cube,
            new Vector3(0, -0.36f, 0.08f), new Vector3(0.18f, 0.09f, 0.32f), shoeMat);
        footL.transform.SetParent(legLowerL.transform, false);

        GameObject legUpperR = P("Leg_Upper_R", PrimitiveType.Capsule,
            new Vector3(0.16f, 0.7f, 0), new Vector3(0.18f, 0.32f, 0.18f), pantsMat);
        legUpperR.transform.SetParent(model.transform, false);
        GameObject legLowerR = P("Leg_Lower_R", PrimitiveType.Capsule,
            new Vector3(0, -0.36f, 0), new Vector3(0.15f, 0.32f, 0.15f), skinMat);
        legLowerR.transform.SetParent(legUpperR.transform, false);
        GameObject footR = P("Foot_R", PrimitiveType.Cube,
            new Vector3(0, -0.36f, 0.08f), new Vector3(0.18f, 0.09f, 0.32f), shoeMat);
        footR.transform.SetParent(legLowerR.transform, false);

        // ── Wire CharacterAnimator ──
        CharacterAnimator anim = model.GetComponent<CharacterAnimator>();
        SerializedObject animSo = new SerializedObject(anim);
        animSo.FindProperty("leftUpperArm").objectReferenceValue = armUpperL.transform;
        animSo.FindProperty("rightUpperArm").objectReferenceValue = armUpperR.transform;
        animSo.FindProperty("leftLowerArm").objectReferenceValue = armLowerL.transform;
        animSo.FindProperty("rightLowerArm").objectReferenceValue = armLowerR.transform;
        animSo.FindProperty("leftUpperLeg").objectReferenceValue = legUpperL.transform;
        animSo.FindProperty("rightUpperLeg").objectReferenceValue = legUpperR.transform;
        animSo.FindProperty("leftFoot").objectReferenceValue = footL.transform;
        animSo.FindProperty("rightFoot").objectReferenceValue = footR.transform;
        animSo.FindProperty("bodyTransform").objectReferenceValue = torso.transform;
        animSo.ApplyModifiedProperties();

        Debug.Log("Using procedural character model");
        return model;
    }

    static Material LoadOrMakeMat(string name, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(
            $"Assets/Prefabs/Materials/{name}.mat");
        if (mat != null) return mat;
        return CreateMaterial(name, color);
    }

    static Material CreateMaterial(string name, Color color)
    {
        return CreateMaterial(name, color, Color.black, 0f, 0.45f);
    }

    static Material CreateMaterial(string name, Color color, Color emission,
        float metallic, float smoothness)
    {
        if (_cachedShader == null)
        {
            Debug.LogError($"No shader found! Cannot create material {name}");
            return null;
        }

        string matPath = $"Assets/Prefabs/Materials/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        bool isNew = mat == null;
        if (isNew)
            mat = new Material(_cachedShader);
        else
            mat.shader = _cachedShader;
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        if (emission.maxColorComponent > 0f && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (isNew)
            AssetDatabase.CreateAsset(mat, matPath);
        else
            EditorUtility.SetDirty(mat);
        Debug.Log($"Created material: {name} ({_cachedShader.name})");
        return mat;
    }

    // ── materials ──────────────────────────────────────

    static void CreateCharacterMaterials()
    {
        CreateMaterial("CharacterSkinMat", new Color(0.82f, 0.58f, 0.42f),
            Color.black, 0f, 0.55f);
        CreateMaterial("CharacterClothMat", new Color(0.18f, 0.28f, 0.38f),
            Color.black, 0.22f, 0.56f);
        CreateMaterial("CharacterPantsMat", new Color(0.1f, 0.15f, 0.21f),
            Color.black, 0.15f, 0.5f);
        CreateMaterial("CharacterShoeMat", new Color(0.82f, 0.22f, 0.14f),
            new Color(0.32f, 0.025f, 0.008f), 0.2f, 0.58f);
        CreateMaterial("CharacterEyeMat", new Color(0.08f, 0.68f, 0.74f),
            new Color(0.01f, 0.65f, 0.78f), 0.15f, 0.72f);
        CreateMaterial("CharacterPupilMat", new Color(0.008f, 0.015f, 0.025f),
            Color.black, 0.1f, 0.85f);
        CreateMaterial("CharacterAccentMat", new Color(0.96f, 0.54f, 0.1f),
            new Color(0.7f, 0.2f, 0.01f), 0.52f, 0.68f);
    }

    static void CreateSkyMaterial()
    {
        const string texturePath = "Assets/Resources/Art/EchoSky.png";
        const string materialPath = "Assets/Resources/Art/EchoSky.mat";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        Shader shader = Shader.Find(WorldStyler.SeamlessSkyShaderName);
        if (texture == null || shader == null)
        {
            Debug.LogWarning("Echo sky texture or seamless panoramic shader is unavailable.");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "EchoSky" };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetTexture("_MainTex", texture);
        material.SetColor("_Tint", new Color(0.52f, 0.60f, 0.70f));
        material.SetFloat("_Exposure", 0.54f);
        material.SetFloat("_Rotation", 0f);
        material.SetFloat("_SeamBlend", 0.07f);
        material.SetFloat("_HorizonTexY", 0.24f);
        EditorUtility.SetDirty(material);
    }

    public static void EnsureSkyMaterialAsset()
    {
        CreateSkyMaterial();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void CreateShaderRetentionMaterial()
    {
        const string path =
            "Assets/Resources/Materials/EchoKitVertexColor.mat";
        Shader shader = Shader.Find("EchoRun/VertexColor");
        if (shader == null)
            throw new System.InvalidOperationException(
                "Required runtime shader is unavailable: EchoRun/VertexColor");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = "EchoKitVertexColor" };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }
        material.SetFloat("_EmissionStrength", 0.38f);
        EditorUtility.SetDirty(material);
    }

    static void ConfigureMenuBackgroundTexture()
    {
        const string path =
            "Assets/Resources/Art/Menu/MemoryCorridorMenu.png";
        TextureImporter importer = AssetImporter.GetAtPath(path)
            as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.compressionQuality = 100;
        importer.maxTextureSize = 4096;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }

    // ── scene objects ──────────────────────────────────

    static void EnsureMainCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.025f, 0.04f, 0.065f);
        cam.farClipPlane = 140f;
        cam.fieldOfView = 58f;
        EditorUtility.SetDirty(cam.gameObject);
    }

    static void CreatePlayer()
    {
        GameObject player = GameObject.Find("player");
        if (player == null)
        {
            player = new GameObject("player");
            player.transform.position = new Vector3(0, 1.0f, 0);
        }

        // Clear old visuals if re-running
        foreach (Transform child in player.transform)
            Object.DestroyImmediate(child.gameObject);
        foreach (MonoBehaviour behaviour in player.GetComponents<MonoBehaviour>())
            Object.DestroyImmediate(behaviour);
        foreach (var c in player.GetComponents<Collider>())
            Object.DestroyImmediate(c);
        MeshRenderer oldMR = player.GetComponent<MeshRenderer>();
        if (oldMR != null) Object.DestroyImmediate(oldMR);
        MeshFilter oldMF = player.GetComponent<MeshFilter>();
        if (oldMF != null) Object.DestroyImmediate(oldMF);

        // Rigidbody
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null) rb = player.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // CapsuleCollider
        CapsuleCollider cc = player.AddComponent<CapsuleCollider>();
        cc.height = 2.2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0, 1.0f, 0);

        // PlayerController
        if (player.GetComponent<PlayerController>() == null)
            player.AddComponent<PlayerController>();
        if (player.GetComponent<PlayerFeedbackController>() == null)
            player.AddComponent<PlayerFeedbackController>();

        // ── Character Model ──
        GameObject humanoidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Models/EchoRunner/EchoRunner_v1.fbx");
        if (humanoidPrefab == null)
            humanoidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Models/HumanoidCharacter.prefab");

        GameObject model;
        if (humanoidPrefab != null)
            model = CreateHumanoidCharacterModel(player, humanoidPrefab);
        else
            model = CreateProceduralCharacterModel(player);

        // Wire characterModel on PlayerController
        PlayerController pc = player.GetComponent<PlayerController>();
        SerializedObject pcSo = new SerializedObject(pc);
        pcSo.FindProperty("characterModel").objectReferenceValue = model.transform;
        pcSo.ApplyModifiedProperties();

        EditorUtility.SetDirty(player);
    }

    static void CreateGroundPlane()
    {
        GameObject plane = GameObject.Find("Plane");
        if (plane == null)
        {
            plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Plane";
        }

        int layer = LayerMask.NameToLayer("Ground");
        plane.layer = layer;
        plane.transform.position = new Vector3(0, -0.1f, 50f);
        plane.transform.localScale = new Vector3(20f, 1f, 20f);

        // Remove MeshCollider (expensive, and we'll use BoxCollider instead)
        MeshCollider mc = plane.GetComponent<MeshCollider>();
        if (mc != null) Object.DestroyImmediate(mc);

        // Replace with large, efficient BoxCollider
        BoxCollider bc = plane.GetComponent<BoxCollider>();
        if (bc == null) bc = plane.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0, 0);
        bc.size = new Vector3(300f, 1f, 300f);

        // Add GroundFollower to keep ground under player
        foreach (MonoBehaviour behaviour in plane.GetComponents<MonoBehaviour>())
            Object.DestroyImmediate(behaviour);
        plane.AddComponent<GroundFollower>();

        Material mat = CreateMaterial("GroundMat", new Color(0.025f, 0.045f, 0.075f),
            Color.black, 0.05f, 0.25f);
        if (mat != null) plane.GetComponent<MeshRenderer>().material = mat;

        EditorUtility.SetDirty(plane);
    }

    static void CreateManagers()
    {
        RecreateManager("GameManager", typeof(GameManager));
        RecreateManager("InputManager", typeof(InputManager));
        RecreateManager("TrackManager", typeof(TrackManager));
        RecreateManager("WorldStyler", typeof(WorldStyler));
        RecreateManager("UIManager", typeof(UIManager));
        RecreateManager("AudioManager", typeof(AudioManager));
        RecreateManager("ParticleManager", typeof(ParticleManager));
    }

    static void RecreateManager(string name, params System.Type[] components)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) Object.DestroyImmediate(go);
        go = new GameObject(name);
        foreach (System.Type component in components)
            go.AddComponent(component);
        EditorUtility.SetDirty(go);
    }

    static void RemoveMissingScriptsFromScene()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform current in transforms)
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(current.gameObject);
        }
    }

    // ── configure ──────────────────────────────────────

    static void ConfigurePlayer()
    {
        GameObject player = GameObject.Find("player");
        if (player == null) return;

        int layer = LayerMask.NameToLayer("Ground");

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            SerializedObject so = new SerializedObject(pc);
            so.FindProperty("groundLayer").intValue = 1 << layer;
            so.ApplyModifiedProperties();
        }

        EditorUtility.SetDirty(player);
    }

    static void ConfigureGround()
    {
        GameObject plane = GameObject.Find("Plane");
        if (plane == null) return;

        int layer = LayerMask.NameToLayer("Ground");
        plane.layer = layer;

        // Wire GroundFollower to player
        GroundFollower gf = plane.GetComponent<GroundFollower>();
        if (gf != null)
        {
            GameObject player = GameObject.Find("player");
            if (player != null) gf.player = player.transform;
        }

        EditorUtility.SetDirty(plane);
    }

    // ── camera ────────────────────────────────────────

    static void CreateCameraFollow()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        CameraFollow cf = cam.GetComponent<CameraFollow>();
        if (cf != null) Object.DestroyImmediate(cf);
        cf = cam.gameObject.AddComponent<CameraFollow>();

        GameObject player = GameObject.Find("player");
        if (player != null) cf.target = player.transform;

        cf.offset = new Vector3(0f, 4.6f, -8.2f);
        cf.smoothSpeed = 8f;

        EditorUtility.SetDirty(cam.gameObject);
    }

    static void CreateLighting()
    {
        Light sun = Object.FindObjectOfType<Light>();
        if (sun == null)
        {
            GameObject lightGo = new GameObject("Directional Light");
            sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.intensity = 1.02f;
        sun.color = new Color(0.9f, 0.95f, 1f);
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
        EditorUtility.SetDirty(sun.gameObject);
    }

    // ── prefabs ────────────────────────────────────────

    static void CreateCoinPrefab()
    {
        string path = "Assets/Prefabs/Coin.prefab";
        GameObject go = new GameObject("EchoShard");
        go.tag = "Coin";
        BoxCollider trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(1f, 1f, 0.4f);
        go.AddComponent<Coin>();

        Material gold = CreateMaterial("CoinMat", new Color(1f, 0.7f, 0.24f),
            new Color(0.65f, 0.26f, 0.025f), 0.65f, 0.72f);
        Material core = CreateMaterial("CoinCoreMat", new Color(0.12f, 0.58f, 0.62f),
            new Color(0.01f, 0.45f, 0.55f), 0.12f, 0.7f);
        Material dark = CreateMaterial("CoinDarkMat", new Color(0.025f, 0.045f, 0.07f),
            Color.black, 0.4f, 0.7f);

        GameObject shell = CreatePrefabPart("ShardShell", PrimitiveType.Cylinder,
            go.transform, Vector3.zero, new Vector3(0.48f, 0.1f, 0.48f), gold);
        shell.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        GameObject inset = CreatePrefabPart("DarkInset", PrimitiveType.Cylinder,
            go.transform, new Vector3(0f, 0f, -0.12f), new Vector3(0.3f, 0.11f, 0.3f), dark);
        inset.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        GameObject signal = CreatePrefabPart("SignalCore", PrimitiveType.Cube,
            go.transform, new Vector3(0f, 0f, -0.24f), new Vector3(0.12f, 0.5f, 0.07f), core);
        signal.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    static void CreateObstaclePrefab()
    {
        CreateObstacleType("Assets/Prefabs/Obstacle_Low.prefab", "Obstacle_Low",
            new Vector3(3.4f, 1.8f, 0.7f), ObstacleType.Low, new Color(1f, 0.32f, 0.12f));
        CreateObstacleType("Assets/Prefabs/Obstacle_High.prefab", "Obstacle_High",
            new Vector3(3.2f, 0.9f, 0.7f), ObstacleType.High, new Color(1f, 0.68f, 0.12f));
        CreateObstacleType("Assets/Prefabs/Obstacle_Barrier.prefab", "Obstacle_Barrier",
            new Vector3(3.4f, 2.7f, 0.9f), ObstacleType.Barrier, new Color(0.94f, 0.18f, 0.16f));
    }

    static void CreateObstacleType(string path, string name, Vector3 scale, ObstacleType type, Color color)
    {
        GameObject go = new GameObject(name);
        go.tag = "Obstacle";
        Obstacle obs = go.AddComponent<Obstacle>();
        obs.type = type;
        BoxCollider trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = scale;

        Material accent = CreateMaterial(name + "Mat", color,
            color * 1.15f, 0.12f, 0.5f);
        Material frame = CreateMaterial(name + "FrameMat", new Color(0.2f, 0.28f, 0.34f),
            new Color(0.015f, 0.025f, 0.035f), 0.35f, 0.52f);
        Material trim = CreateMaterial(name + "TrimMat", new Color(0.94f, 0.9f, 0.72f),
            new Color(0.75f, 0.52f, 0.18f), 0.15f, 0.5f);

        if (type == ObstacleType.Low)
        {
            trigger.center = new Vector3(0f, 0.65f, 0f);
            CreatePrefabPart("ScannerPostL", PrimitiveType.Cube, go.transform,
                new Vector3(-1.45f, 0.9f, 0f), new Vector3(0.38f, 1.9f, 0.7f), frame);
            CreatePrefabPart("ScannerPostR", PrimitiveType.Cube, go.transform,
                new Vector3(1.45f, 0.9f, 0f), new Vector3(0.38f, 1.9f, 0.7f), frame);
            CreatePrefabPart("ScannerBeam", PrimitiveType.Cube, go.transform,
                new Vector3(0f, 1.42f, 0f), new Vector3(3.3f, 0.42f, 0.68f), accent);
            CreatePrefabPart("ScannerSignal", PrimitiveType.Cube, go.transform,
                new Vector3(0f, 1.42f, -0.38f), new Vector3(1.45f, 0.13f, 0.09f), trim);
            CreatePrefabPart("ScannerBeaconL", PrimitiveType.Cube, go.transform,
                new Vector3(-1.45f, 1.68f, -0.38f), new Vector3(0.48f, 0.34f, 0.1f), trim);
            CreatePrefabPart("ScannerBeaconR", PrimitiveType.Cube, go.transform,
                new Vector3(1.45f, 1.68f, -0.38f), new Vector3(0.48f, 0.34f, 0.1f), trim);
        }
        else if (type == ObstacleType.High)
        {
            trigger.center = new Vector3(0f, -0.45f, 0f);
            CreatePrefabPart("HurdleBase", PrimitiveType.Cube, go.transform,
                new Vector3(0f, -0.46f, 0f), new Vector3(3.2f, 0.66f, 0.78f), accent);
            CreatePrefabPart("HurdleLight", PrimitiveType.Cube, go.transform,
                new Vector3(0f, -0.43f, -0.43f), new Vector3(1.6f, 0.2f, 0.1f), trim);
            CreatePrefabPart("HurdleFootL", PrimitiveType.Cube, go.transform,
                new Vector3(-1.3f, -0.8f, 0f), new Vector3(0.42f, 0.45f, 0.95f), frame);
            CreatePrefabPart("HurdleFootR", PrimitiveType.Cube, go.transform,
                new Vector3(1.3f, -0.8f, 0f), new Vector3(0.42f, 0.45f, 0.95f), frame);
        }
        else
        {
            trigger.center = new Vector3(0f, 0.25f, 0f);
            CreatePrefabPart("ShieldBody", PrimitiveType.Cube, go.transform,
                new Vector3(0f, 0.25f, 0f), new Vector3(3.2f, 2.5f, 0.72f), frame);
            for (int i = -1; i <= 1; i++)
            {
                GameObject chevron = CreatePrefabPart("ShieldSignal", PrimitiveType.Cube,
                    go.transform, new Vector3(i * 0.85f, 0.25f, -0.42f),
                    new Vector3(0.26f, 1.65f, 0.1f), accent);
                chevron.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
            }
            CreatePrefabPart("ShieldTop", PrimitiveType.Cube, go.transform,
                new Vector3(0f, 1.55f, 0f), new Vector3(3.5f, 0.24f, 0.82f), trim);
            CreatePrefabPart("ShieldSideL", PrimitiveType.Cube, go.transform,
                new Vector3(-1.58f, 0.25f, -0.42f), new Vector3(0.18f, 2.45f, 0.1f), accent);
            CreatePrefabPart("ShieldSideR", PrimitiveType.Cube, go.transform,
                new Vector3(1.58f, 0.25f, -0.42f), new Vector3(0.18f, 2.45f, 0.1f), accent);
        }

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    static GameObject CreatePrefabPart(string name, PrimitiveType primitive, Transform parent,
        Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null && material != null) renderer.sharedMaterial = material;
        return part;
    }

    static void CreateTrackSegmentPrefab()
    {
        string path = "Assets/Prefabs/TrackSegment.prefab";
        GameObject seg = new GameObject("TrackSegment");
        seg.layer = LayerMask.NameToLayer("Ground");
        seg.AddComponent<TrackSegmentData>();

        // Ground visual
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "GroundPlane";
        ground.transform.SetParent(seg.transform);
        ground.transform.localPosition = Vector3.zero;
        ground.transform.localScale = new Vector3(
            TrackGeometryStandards.VisualRoadWidth / 10f, 1f,
            TrackGeometryStandards.StandardSegmentLength / 10f);
        ground.layer = LayerMask.NameToLayer("Ground");
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        BoxCollider groundCol = ground.AddComponent<BoxCollider>();
        groundCol.center = Vector3.zero;
        groundCol.size = new Vector3(
            TrackGeometryStandards.WalkableWidth
            / ground.transform.localScale.x,
            0.2f,
            TrackGeometryStandards.StandardSegmentLength
            / ground.transform.localScale.z);

        Material gm = GetOrCreateSharedRoadMaterial();
        if (gm != null) ground.GetComponent<MeshRenderer>().sharedMaterial = gm;

        // Lane markers
        for (int i = 0; i < 3; i++)
        {
            GameObject m = new GameObject("Lane_" + i);
            m.transform.SetParent(seg.transform);
            m.transform.localPosition = new Vector3((i - 1) * 3f, 0.06f, 0);
        }

        // Neutral recessed lane seams. Phase color belongs to authored accent
        // meshes, not to a pair of always-emissive full-length strips.
        Material lineMat = CreateMaterial("LaneInsetMat",
            new Color(0.055f, 0.070f, 0.078f), Color.black, 0.20f, 0.44f);
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "LaneLine_" + (i > 0 ? "R" : "L");
            line.transform.SetParent(seg.transform);
            line.transform.localPosition = new Vector3(i * 1.5f, 0.052f, 0);
            line.transform.localScale = new Vector3(0.045f, 0.012f, 19.2f);
            if (lineMat != null)
                line.GetComponent<MeshRenderer>().sharedMaterial = lineMat;
            Object.DestroyImmediate(line.GetComponent<Collider>());
        }

        Material seamMat = lineMat;
        for (int z = -8; z <= 8; z += 4)
        {
            GameObject seam = CreatePrefabPart("DataSeam", PrimitiveType.Cube, seg.transform,
                new Vector3(0f, 0.075f, z), new Vector3(0.72f, 0.025f, 0.08f), seamMat);
            seam.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
        }

        PrefabUtility.SaveAsPrefabAsset(seg, path);
        Object.DestroyImmediate(seg);
    }

    static void CreateTurnSegmentPrefabs()
    {
        Material trackMat = GetOrCreateSharedRoadMaterial();
        Material lineMat = CreateMaterial("LaneInsetMat_Turn",
            new Color(0.055f, 0.070f, 0.078f), Color.black, 0.20f, 0.44f);
        int groundLayer = LayerMask.NameToLayer("Ground");

        CreateTurnPrefab("Assets/Prefabs/TurnSegment_Right.prefab", "TurnSegment_Right", 1, trackMat, lineMat, groundLayer);
        CreateTurnPrefab("Assets/Prefabs/TurnSegment_Left.prefab", "TurnSegment_Left", -1, trackMat, lineMat, groundLayer);
    }

    [MenuItem("Tools/EchoRun/Fix Turn Road Joins")]
    public static void FixTurnRoadJoins()
    {
        FixTurnRoadJoinPrefab("Assets/Prefabs/TurnSegment_Left.prefab", -1);
        FixTurnRoadJoinPrefab("Assets/Prefabs/TurnSegment_Right.prefab", 1);
        AssetDatabase.SaveAssets();
        Debug.Log("Turn road joins sealed with mirrored inner-corner caps.");
    }

    static void FixTurnRoadJoinPrefab(string path, int turnDir)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ConfigureTurnRoadSurface(root.transform.Find("EntryStrip"),
                true, turnDir);
            ConfigureTurnRoadSurface(root.transform.Find("ExitStrip"),
                false, turnDir);
            Renderer entryRenderer = root.transform.Find("EntryStrip")
                .GetComponent<Renderer>();
            EnsureTurnCornerSupport(root.transform, turnDir,
                entryRenderer != null ? entryRenderer.sharedMaterial : null,
                root.layer);

            Transform runtimeCoverage = root.transform.Find(
                "RuntimeTurnCoverage");
            if (runtimeCoverage != null)
                Object.DestroyImmediate(runtimeCoverage.gameObject);

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/EchoRun/Bake Environment Variants")]
    public static void BakeEnvironmentVariants()
    {
        DetectRenderPipeline();
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/Materials");

        Material[] palette =
        {
            CreateMaterial("EchoStructure", new Color(0.14f, 0.20f, 0.29f),
                new Color(0.008f, 0.018f, 0.034f),
                WorldStyler.StructureMetallic, WorldStyler.StructureSmoothness),
            CreateMaterial("EchoDepth", new Color(0.055f, 0.085f, 0.14f),
                new Color(0.004f, 0.010f, 0.020f), 0.10f, 0.27f),
            CreateMaterial("EchoCyan", new Color(0.22f, 0.84f, 1.00f),
                new Color(0.020f, 0.34f, 0.56f), 0.24f, 0.68f),
            CreateMaterial("EchoCoral", new Color(1.00f, 0.40f, 0.35f),
                new Color(0.58f, 0.060f, 0.028f), 0.14f, 0.50f),
            CreateMaterial("EchoGold", new Color(0.94f, 0.68f, 0.24f),
                new Color(0.48f, 0.19f, 0.015f), 0.52f, 0.72f),
            AssetDatabase.LoadAssetAtPath<Material>(
                InstallColdWhiteMemoryFortress.PhaseAccentMaterialPath)
        };

        GameObject temporaryStyler = null;
        WorldStyler styler = Object.FindObjectOfType<WorldStyler>();
        if (styler == null)
        {
            temporaryStyler = new GameObject("WorldStyler_EnvironmentBake");
            styler = temporaryStyler.AddComponent<WorldStyler>();
        }

        try
        {
            BakeEnvironmentVariantPrefab(styler,
                "Assets/Prefabs/TrackSegment.prefab", TrackSegmentType.Straight,
                palette);
            BakeEnvironmentVariantPrefab(styler,
                "Assets/Prefabs/TurnSegment_Left.prefab", TrackSegmentType.TurnLeft,
                palette);
            BakeEnvironmentVariantPrefab(styler,
                "Assets/Prefabs/TurnSegment_Right.prefab", TrackSegmentType.TurnRight,
                palette);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Baked prehung environment variants into all road prefabs.");
        }
        finally
        {
            if (temporaryStyler != null)
                Object.DestroyImmediate(temporaryStyler);
        }
    }

    private static void BakeEnvironmentVariantPrefab(WorldStyler styler,
        string path, TrackSegmentType segmentType, Material[] palette)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            Debug.LogError("Cannot bake missing road prefab: " + path);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform previous = root.transform.Find("EchoEnvironment");
            if (previous != null)
                Object.DestroyImmediate(previous.gameObject);
            if (root.GetComponent<TrackSegmentData>() == null)
                root.AddComponent<TrackSegmentData>();

            styler.DecorateSegment(root, segmentType);
            Transform environment = root.transform.Find("EchoEnvironment");
            if (environment == null)
                throw new System.InvalidOperationException(
                    "Environment bake failed for " + path);

            Renderer[] renderers = environment.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material runtimeMaterial = renderers[i].sharedMaterial;
                if (runtimeMaterial == null) continue;
                for (int p = 0; p < palette.Length; p++)
                {
                    if (palette[p] != null && palette[p].name == runtimeMaterial.name)
                    {
                        renderers[i].sharedMaterial = palette[p];
                        break;
                    }
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void EnsureSharedRoadMaterialAsset()
    {
        Material material = GetOrCreateSharedRoadMaterial();
        BindExistingRoadPrefabs(material);
        AssetDatabase.SaveAssets();
    }

    private static Material GetOrCreateSharedRoadMaterial()
    {
        const string path = "Assets/Resources/Materials/EchoRoad.mat";
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Materials");

        Shader shader = Shader.Find("EchoRun/Road");
        if (shader == null)
        {
            Debug.LogError("EchoRun/Road shader is missing.");
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = "EchoRoad" };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", new Color(0.045f, 0.065f, 0.095f, 1f));
        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Art/Road/EchoRoadAtlas.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Art/Road/EchoRoadNormal.png");
        if (atlas != null && material.HasProperty("_RoadAtlas"))
            material.SetTexture("_RoadAtlas", atlas);
        if (normal != null && material.HasProperty("_NormalMap"))
            material.SetTexture("_NormalMap", normal);
        if (material.HasProperty("_LaneColor"))
            material.SetColor("_LaneColor", new Color(0.08f, 0.72f, 0.92f, 1f));
        if (material.HasProperty("_EdgeColor"))
            material.SetColor("_EdgeColor", new Color(0.035f, 0.34f, 0.48f, 1f));
        if (material.HasProperty("_Wetness")) material.SetFloat("_Wetness", 0.72f);
        if (material.HasProperty("_ReflectionStrength"))
            material.SetFloat("_ReflectionStrength", 0.18f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BindExistingRoadPrefabs(Material material)
    {
        if (material == null) return;
        string[] paths =
        {
            "Assets/Prefabs/TrackSegment.prefab",
            "Assets/Prefabs/TurnSegment_Left.prefab",
            "Assets/Prefabs/TurnSegment_Right.prefab"
        };
        for (int i = 0; i < paths.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]) == null)
                continue;
            GameObject root = PrefabUtility.LoadPrefabContents(paths[i]);
            try
            {
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    string rendererName = renderers[r].name;
                    if (rendererName == "GroundPlane"
                        || rendererName == "EntryStrip"
                        || rendererName == "ExitStrip"
                        || rendererName == TrackManager.TurnInnerCornerCapName
                        || rendererName.Contains("Seam"))
                        renderers[r].sharedMaterial = material;
                }
                PrefabUtility.SaveAsPrefabAsset(root, paths[i]);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    static void CreateTurnPrefab(string path, string name, int turnDir, Material trackMat, Material lineMat, int groundLayer)
    {
        GameObject seg = new GameObject(name);
        seg.layer = groundLayer;
        seg.AddComponent<TrackSegmentData>();

        float segmentLength = TrackGeometryStandards.StandardSegmentLength;
        float entryLength = TrackGeometryStandards.TurnEntrySurfaceLength(
            segmentLength);
        float exitLength = TrackGeometryStandards.TurnExitSurfaceLength(
            segmentLength);

        // Entry and exit keep their trimmed join envelopes. A separate visual
        // cap seals the inner square without extending either arm underneath a
        // neighboring straight.
        GameObject entry = GameObject.CreatePrimitive(PrimitiveType.Plane);
        entry.name = "EntryStrip";
        entry.transform.SetParent(seg.transform);
        entry.transform.localPosition = new Vector3(0f, 0.05f,
            TrackGeometryStandards.TurnEntrySurfaceCenter(segmentLength));
        entry.transform.localScale = new Vector3(
            TrackGeometryStandards.VisualRoadWidth / 10f, 1f,
            entryLength / 10f);
        entry.layer = groundLayer;
        Object.DestroyImmediate(entry.GetComponent<MeshCollider>());
        BoxCollider entryCol = entry.AddComponent<BoxCollider>();
        entryCol.center = Vector3.zero;
        entryCol.size = new Vector3(
            TrackGeometryStandards.WalkableWidth / entry.transform.localScale.x,
            0.3f, 10f);
        if (trackMat != null) entry.GetComponent<MeshRenderer>().sharedMaterial = trackMat;

        // The entry surface already covers the square around the corner.
        GameObject exitStrip = GameObject.CreatePrimitive(PrimitiveType.Plane);
        exitStrip.name = "ExitStrip";
        exitStrip.transform.SetParent(seg.transform);
        exitStrip.transform.localPosition = new Vector3(
            turnDir * TrackGeometryStandards.TurnExitSurfaceCenter(segmentLength),
            0.05f, segmentLength * 0.5f);
        exitStrip.transform.localRotation = Quaternion.Euler(0, 90f, 0);
        exitStrip.transform.localScale = new Vector3(
            TrackGeometryStandards.VisualRoadWidth / 10f, 1f,
            exitLength / 10f);
        exitStrip.layer = groundLayer;
        Object.DestroyImmediate(exitStrip.GetComponent<MeshCollider>());
        BoxCollider exitCol = exitStrip.AddComponent<BoxCollider>();
        exitCol.center = Vector3.zero;
        exitCol.size = new Vector3(
            TrackGeometryStandards.WalkableWidth
            / exitStrip.transform.localScale.x, 0.3f, 10f);
        if (trackMat != null) exitStrip.GetComponent<MeshRenderer>().sharedMaterial = trackMat;

        EnsureTurnCornerSupport(seg.transform, turnDir, trackMat, groundLayer);

        // Lane markers on entry strip
        for (int i = 0; i < 3; i++)
        {
            GameObject m = new GameObject("Lane_" + i);
            m.transform.SetParent(seg.transform);
            m.transform.localPosition = new Vector3((i - 1) * 3f, 0.08f, 5f);
        }
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "LaneLine_" + (i > 0 ? "R" : "L");
            line.transform.SetParent(seg.transform);
            line.transform.localPosition = new Vector3(i * 1.5f, 0.072f, 5f);
            line.transform.localScale = new Vector3(0.045f, 0.012f, 9.2f);
            if (lineMat != null)
                line.GetComponent<MeshRenderer>().sharedMaterial = lineMat;
            Object.DestroyImmediate(line.GetComponent<Collider>());
        }

        PrefabUtility.SaveAsPrefabAsset(seg, path);
        Object.DestroyImmediate(seg);
    }

    static void ConfigureTurnRoadSurface(Transform surface, bool entry,
        int turnDir)
    {
        if (surface == null)
            throw new System.InvalidOperationException(
                "Turn prefab is missing an authored road surface.");

        float segmentLength = TrackGeometryStandards.StandardSegmentLength;
        float length = entry
            ? TrackGeometryStandards.TurnEntrySurfaceLength(segmentLength)
            : TrackGeometryStandards.TurnExitSurfaceLength(segmentLength);
        Vector3 position = surface.localPosition;
        Vector3 scale = surface.localScale;
        scale.x = TrackGeometryStandards.VisualRoadWidth / 10f;
        scale.z = length / 10f;

        if (entry)
        {
            position.x = 0f;
            position.z = TrackGeometryStandards.TurnEntrySurfaceCenter(
                segmentLength);
            surface.localRotation = Quaternion.identity;
        }
        else
        {
            position.x = turnDir
                         * TrackGeometryStandards.TurnExitSurfaceCenter(
                             segmentLength);
            position.z = segmentLength * 0.5f;
            surface.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        surface.localPosition = position;
        surface.localScale = scale;
        BoxCollider collider = surface.GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.size = new Vector3(
                TrackGeometryStandards.WalkableWidth / scale.x,
                collider.size.y, 10f);
        }
    }

    static void EnsureTurnCornerSupport(Transform root, int turnDir,
        Material roadMaterial, int groundLayer)
    {
        float segmentLength = TrackGeometryStandards.StandardSegmentLength;
        float size = TrackGeometryStandards.TurnInnerCornerSize(segmentLength);
        Vector3 capCenter = TrackGeometryStandards.TurnInnerCornerCenter(
            segmentLength, turnDir);
        capCenter.y = 0.051f;

        Transform existingCap = root.Find(TrackManager.TurnInnerCornerCapName);
        GameObject cap = existingCap != null
            ? existingCap.gameObject
            : GameObject.CreatePrimitive(PrimitiveType.Plane);
        cap.name = TrackManager.TurnInnerCornerCapName;
        cap.transform.SetParent(root, false);
        cap.transform.localPosition = capCenter;
        cap.transform.localRotation = Quaternion.identity;
        cap.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
        cap.layer = groundLayer;
        Collider[] capColliders = cap.GetComponents<Collider>();
        for (int index = 0; index < capColliders.Length; index++)
            Object.DestroyImmediate(capColliders[index]);
        Renderer capRenderer = cap.GetComponent<Renderer>();
        if (capRenderer != null && roadMaterial != null)
            capRenderer.sharedMaterial = roadMaterial;

        Transform existingBridge = root.Find(TrackManager.TurnWalkableBridgeName);
        GameObject bridge = existingBridge != null
            ? existingBridge.gameObject
            : new GameObject(TrackManager.TurnWalkableBridgeName);
        bridge.transform.SetParent(root, false);
        Vector3 bridgeCenter = TrackGeometryStandards.TurnWalkableBridgeCenter(
            segmentLength, turnDir);
        bridgeCenter.y = 0.05f;
        bridge.transform.localPosition = bridgeCenter;
        bridge.transform.localRotation = Quaternion.identity;
        bridge.transform.localScale = Vector3.one;
        bridge.layer = groundLayer;
        BoxCollider bridgeCollider = bridge.GetComponent<BoxCollider>();
        if (bridgeCollider == null)
            bridgeCollider = bridge.AddComponent<BoxCollider>();
        bridgeCollider.enabled = true;
        bridgeCollider.center = Vector3.zero;
        bridgeCollider.size = new Vector3(
            TrackGeometryStandards.TurnWalkableBridgeWidth, 0.3f,
            TrackGeometryStandards.WalkableWidth);
    }

    // ── track manager wiring ───────────────────────────

    [MenuItem("Tools/EchoRun/Ensure Formal Track Manager Bindings")]
    public static void EnsureFormalTrackManagerBindings()
    {
        TrackManager tm = Object.FindObjectOfType<TrackManager>();
        if (tm == null)
        {
            GameObject manager = new GameObject("TrackManager");
            tm = manager.AddComponent<TrackManager>();
        }

        GameObject coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Coin.prefab");
        GameObject obsLow = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Obstacle_Low.prefab");
        GameObject obsHigh = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Obstacle_High.prefab");
        GameObject obsBarrier = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Obstacle_Barrier.prefab");
        GameObject segmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TrackSegment.prefab");
        GameObject turnLeftPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TurnSegment_Left.prefab");
        GameObject turnRightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TurnSegment_Right.prefab");
        if (coinPrefab == null || obsLow == null || obsHigh == null
            || obsBarrier == null || segmentPrefab == null
            || turnLeftPrefab == null || turnRightPrefab == null)
        {
            throw new System.InvalidOperationException(
                "Formal TrackManager binding requires all track, coin and obstacle prefabs.");
        }

        SerializedObject so = new SerializedObject(tm);
        so.FindProperty("coinPrefab").objectReferenceValue = coinPrefab;
        so.FindProperty("trackSegmentPrefab").objectReferenceValue = segmentPrefab;
        so.FindProperty("segmentLength").floatValue =
            TrackGeometryStandards.StandardSegmentLength;
        so.FindProperty("laneDistance").floatValue =
            TrackGeometryStandards.LaneSpacing;
        so.FindProperty("turnLeftPrefab").objectReferenceValue = turnLeftPrefab;
        so.FindProperty("turnRightPrefab").objectReferenceValue = turnRightPrefab;

        SerializedProperty obsArr = so.FindProperty("obstaclePrefabs");
        obsArr.arraySize = 3;
        obsArr.GetArrayElementAtIndex(0).objectReferenceValue = obsLow;
        obsArr.GetArrayElementAtIndex(1).objectReferenceValue = obsHigh;
        obsArr.GetArrayElementAtIndex(2).objectReferenceValue = obsBarrier;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(tm.gameObject);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("FORMAL_TRACK_MANAGER_BINDINGS_OK");
    }

    // ── canvas / UI ────────────────────────────────────

    static void CreateUICanvas()
    {
        // UIManager handles Canvas + all panels at runtime.
        // Just make sure EventSystem exists for UI interaction.
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = new GameObject(name, typeof(Image));
        panel.GetComponent<Image>().color = color;
        panel.transform.SetParent(parent, false);
        return panel;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void AnchorText(RectTransform rt, float ax, float ay, float w, float h)
    {
        rt.anchorMin = new Vector2(ax, ay);
        rt.anchorMax = new Vector2(ax, ay);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
    }

    static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor align)
    {
        GameObject go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = Color.white;
        Font font = AssetDatabase.LoadAssetAtPath<Font>(
            "Assets/Resources/Fonts/EchoRunSansSC-Regular.otf");
        if (font != null) text.font = font;
        else text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return text;
    }

    static void AddOutline(GameObject go, Color color)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(2.5f, -2.5f);
    }

    static void AddShadow(GameObject go, Color color)
    {
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = new Vector2(3f, -3f);
    }

    static Button CreateButton(string name, Transform parent, string label, int fontSize,
        Vector2 anchor, Vector2 size, Color mainColor, Color edgeColor)
    {
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        go.GetComponent<Image>().color = mainColor;

        // Button border/depth via inner shadow panel
        GameObject border = new GameObject("Border", typeof(Image));
        border.transform.SetParent(go.transform, false);
        Image borderImg = border.GetComponent<Image>();
        borderImg.color = edgeColor;
        RectTransform brt = border.GetComponent<RectTransform>();
        Stretch(brt);
        brt.offsetMin = new Vector2(4, 4);
        brt.offsetMax = new Vector2(-4, -4);

        Text labelT = CreateText("Label", go.transform, label, fontSize, TextAnchor.MiddleCenter);
        labelT.color = Color.white;
        labelT.fontStyle = FontStyle.Bold;
        AddOutline(labelT.gameObject, new Color(0, 0, 0, 0.5f));
        Stretch(labelT.GetComponent<RectTransform>());

        return go.GetComponent<Button>();
    }

    static Button CreateSmallButton(string name, Transform parent, string label,
        Vector2 anchor, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        go.GetComponent<Image>().color = color;

        Text labelT = CreateText("Label", go.transform, label, 28, TextAnchor.MiddleCenter);
        labelT.color = Color.white;
        labelT.fontStyle = FontStyle.Bold;
        Stretch(labelT.GetComponent<RectTransform>());

        return go.GetComponent<Button>();
    }

    // ── asset helpers ──────────────────────────────────

    static void DeleteAssetAtPath(string assetPath)
    {
        string fullPath = System.IO.Path.Combine(Application.dataPath, "../", assetPath);
        string metaPath = fullPath + ".meta";
        try
        {
            if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
            if (System.IO.File.Exists(metaPath)) System.IO.File.Delete(metaPath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to delete {assetPath}: {e.Message}");
        }
    }

    // ── tag / layer helpers ────────────────────────────

    static void AddTag(string tag)
    {
        SerializedObject o = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty arr = o.FindProperty("tags");
        for (int i = 0; i < arr.arraySize; i++)
            if (arr.GetArrayElementAtIndex(i).stringValue == tag) return;
        arr.InsertArrayElementAtIndex(arr.arraySize);
        arr.GetArrayElementAtIndex(arr.arraySize - 1).stringValue = tag;
        o.ApplyModifiedProperties();
    }

    static void AddLayer(string layer)
    {
        SerializedObject o = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty arr = o.FindProperty("layers");
        for (int i = 8; i < 32; i++)
            if (arr.GetArrayElementAtIndex(i).stringValue == layer) return;
        for (int i = 8; i < 32; i++)
        {
            if (string.IsNullOrEmpty(arr.GetArrayElementAtIndex(i).stringValue))
            { arr.GetArrayElementAtIndex(i).stringValue = layer; o.ApplyModifiedProperties(); return; }
        }
    }

    static void EnsureFolder(string p)
    {
        if (!AssetDatabase.IsValidFolder(p))
        {
            string parent = System.IO.Path.GetDirectoryName(p).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(p);
            if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(parent));
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
