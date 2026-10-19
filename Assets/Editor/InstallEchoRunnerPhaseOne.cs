using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class InstallEchoRunnerPhaseOne
{
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";
    private const string ExoMaterialFolder =
        "Assets/Models/Mixamo/ExoGray/Materials";
    private const string AssetFolder =
        "Assets/Art/Characters/EchoRunner/PhaseOneHero";
    private const string MaterialFolder = AssetFolder + "/Materials";
    private const string MeshFolder = AssetFolder + "/Meshes";
    private const string MemorySpinePrefabPath =
        AssetFolder + "/EchoMemorySpine.prefab";
    private const string ContactShadowPrefabPath =
        AssetFolder + "/EchoRunnerContactShadow.prefab";
    private const string BlueTechShader = "EchoRun/ExoGrayBlueTech";
    private const string IdentityShader = "EchoRun/RunnerIdentity";
    private const string ContactShadowShader = "EchoRun/ContactShadow";

    private static readonly Vector3 MemorySpineAnchor =
        new Vector3(0f, 1.24f, -0.205f);
    private static readonly Vector3 MemorySpineScale =
        new Vector3(0.78f, 0.80f, 0.80f);

    private static readonly float[] SegmentPositions =
        { 0.18f, 0f, -0.18f };

    [MenuItem("Tools/Echo Runner/Legacy/Install Phase One Hero Visuals")]
    public static void Install()
    {
        if (!LegacyRunnerInstallGuard.Allow("InstallEchoRunnerPhaseOne.Install")) return;
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("Phase-one hero install cancelled before changing scenes.");
            return;
        }

        GameObject spinePrefab = BuildMemorySpinePrefab();
        GameObject shadowPrefab = BuildContactShadowPrefab();
        ConfigureModelMaterialsUnchecked();

        Scene scene = EditorSceneManager.OpenScene(
            ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("player");
        Transform model = player != null
            ? player.transform.Find("CharacterModel")
            : null;
        if (player == null || model == null)
            throw new InvalidOperationException(
                "Phase-one install requires player/CharacterModel.");

        Transform memorySpine = AttachMemorySpine(model, spinePrefab);
        Transform contactShadow = AttachContactShadow(player, shadowPrefab);
        Renderer shadowRenderer =
            contactShadow.GetComponentInChildren<Renderer>(true);

        EchoRunnerHeroVisual hero =
            player.GetComponent<EchoRunnerHeroVisual>();
        if (hero == null) hero = player.AddComponent<EchoRunnerHeroVisual>();
        hero.ConfigureContactShadow(contactShadow, shadowRenderer);

        Camera mainCamera = Camera.main;
        CameraFollow follow = mainCamera != null
            ? mainCamera.GetComponent<CameraFollow>()
            : null;
        if (follow != null)
        {
            follow.offset = WorldStyler.GetCameraOffset(false);
            EditorUtility.SetDirty(follow);
        }

        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(hero);
        EditorUtility.SetDirty(memorySpine.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException(
                "Could not save SampleScene after phase-one install.");

        AssetDatabase.SaveAssets();
        ValidateInstalledScene();
        Debug.Log("ECHO_RUNNER_PHASE_ONE_INSTALL_OK");
    }

    [MenuItem("Tools/Echo Runner/Legacy/Refresh Phase One Materials")]
    public static void ConfigureModelMaterials()
    {
        if (!LegacyRunnerInstallGuard.Allow("InstallEchoRunnerPhaseOne.ConfigureModelMaterials")) return;
        ConfigureModelMaterialsUnchecked();
    }

    private static void ConfigureModelMaterialsUnchecked()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:Material", new[] { ExoMaterialFolder });
        int configured = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null ||
                material.shader.name != BlueTechShader)
                continue;

            ApplyBlueTechProfile(material, material.name);
            EditorUtility.SetDirty(material);
            configured++;
        }

        AssetDatabase.SaveAssets();
        const int expectedMaterialCount = 5;
        if (configured != expectedMaterialCount)
            throw new InvalidOperationException(
                $"Expected {expectedMaterialCount} BlueTech materials, " +
                $"configured {configured}.");
        Debug.Log($"ECHO_RUNNER_PHASE_ONE_MATERIALS_OK count={configured}");
    }

    public static void ApplyBlueTechProfile(
        Material material, string sourceName)
    {
        if (material == null) return;
        string name = (sourceName ?? string.Empty).ToLowerInvariant();

        Color dark;
        Color light;
        float metallic;
        float smoothness;
        float toneScale;
        float toneOffset;
        float rimStrength;

        if (name.Contains("exo"))
        {
            dark = new Color(0.032f, 0.070f, 0.13f, 1f);
            light = new Color(0.40f, 0.56f, 0.70f, 1f);
            metallic = 0.48f;
            smoothness = 0.58f;
            toneScale = 1.18f;
            toneOffset = 0.035f;
            rimStrength = 0.22f;
        }
        else if (name.Contains("body"))
        {
            dark = new Color(0.018f, 0.028f, 0.043f, 1f);
            light = new Color(0.16f, 0.21f, 0.28f, 1f);
            metallic = 0.06f;
            smoothness = 0.24f;
            toneScale = 0.68f;
            toneOffset = -0.015f;
            rimStrength = 0.07f;
        }
        else
        {
            dark = new Color(0.012f, 0.018f, 0.028f, 1f);
            light = new Color(0.12f, 0.15f, 0.19f, 1f);
            metallic = name.Contains("spec") ? 0.62f : 0.12f;
            smoothness = name.Contains("eye") ? 0.72f : 0.30f;
            toneScale = 0.52f;
            toneOffset = -0.025f;
            rimStrength = 0.08f;
        }

        SetColor(material, "_DarkColor", dark);
        SetColor(material, "_LightColor", light);
        SetColor(material, "_EmissionColor",
            new Color(0.95f, 0.68f, 0.42f, 1f));
        SetColor(material, "_IdentityColor",
            new Color(0.48f, 0.60f, 0.70f, 1f));
        SetFloat(material, "_EmissionStrength", 0.72f);
        SetFloat(material, "_AccentThreshold", 0.075f);
        SetFloat(material, "_Metallic", metallic);
        SetFloat(material, "_Smoothness", smoothness);
        SetFloat(material, "_ToneScale", toneScale);
        SetFloat(material, "_ToneOffset", toneOffset);
        SetFloat(material, "_RimStrength", rimStrength);
        SetFloat(material, "_RimPower", 4.2f);
        material.enableInstancing = true;
    }

    [MenuItem("Tools/Echo Runner/Validate Phase One Hero Visuals")]
    public static void ValidateInstalledScene()
    {
        Scene scene = EditorSceneManager.OpenScene(
            ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("player");
        Transform model = player != null
            ? player.transform.Find("CharacterModel")
            : null;
        Transform memorySpine = FindDescendant(model, "EchoMemorySpine");
        Transform contactShadow = player != null
            ? player.transform.Find("EchoRunnerContactShadow")
            : null;
        if (player == null || model == null || memorySpine == null ||
            contactShadow == null)
            throw new InvalidOperationException(
                "Phase-one scene binding is incomplete.");

        Animator animator = model.GetComponent<Animator>();
        CharacterAnimator driver = model.GetComponent<CharacterAnimator>();
        Transform chest = animator != null && animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.Chest)
            : FindDescendant(model, "mixamorig:Spine2");
        if (chest == null || memorySpine.parent != chest)
            throw new InvalidOperationException(
                "Memory spine is not attached to the chest bone.");
        if (animator == null || animator.avatar == null ||
            !animator.avatar.isHuman || animator.applyRootMotion)
            throw new InvalidOperationException(
                "Humanoid or root-motion contract changed.");
        if (driver == null || !driver.enabled)
            throw new InvalidOperationException(
                "CharacterAnimator must remain enabled.");

        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
        if (capsule == null || Mathf.Abs(capsule.radius - 0.4f) > 0.001f ||
            Mathf.Abs(capsule.height - 2.2f) > 0.001f ||
            Vector3.Distance(capsule.center, new Vector3(0f, 1f, 0f)) > 0.001f)
            throw new InvalidOperationException(
                "Player collider contract changed.");

        Renderer[] spineRenderers =
            memorySpine.GetComponentsInChildren<Renderer>(true);
        Renderer[] shadowRenderers =
            contactShadow.GetComponentsInChildren<Renderer>(true);
        Collider[] addedColliders =
            memorySpine.GetComponentsInChildren<Collider>(true);
        if (spineRenderers.Length != 3 || shadowRenderers.Length != 1)
            throw new InvalidOperationException(
                "Hero renderer budget must remain 3 spine + 1 shadow.");
        if (addedColliders.Length != 0 ||
            contactShadow.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException(
                "Phase-one visuals must not add collision.");

        int segmentCount = 0;
        int legacyNodeCount = 0;
        Transform[] descendants =
            memorySpine.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (descendants[i].name.StartsWith(
                    "MemorySegment_", StringComparison.Ordinal))
                segmentCount++;
            if (descendants[i].name.StartsWith(
                    "MemoryNode_", StringComparison.Ordinal))
                legacyNodeCount++;
        }
        if (segmentCount != 3 || legacyNodeCount != 0)
            throw new InvalidOperationException(
                "Memory spine must expose three integrated segments and no legacy nodes.");

        EchoRunnerHeroVisual hero =
            player.GetComponent<EchoRunnerHeroVisual>();
        if (hero == null || hero.ContactShadow != contactShadow ||
            hero.ContactShadowRenderer == null)
            throw new InvalidOperationException(
                "Contact-shadow runtime binding is stale.");

        CameraFollow follow = Camera.main != null
            ? Camera.main.GetComponent<CameraFollow>()
            : null;
        Vector3 expectedOffset = WorldStyler.GetCameraOffset(false);
        if (follow == null || Vector3.Distance(
                follow.offset, expectedOffset) > 0.001f)
            throw new InvalidOperationException(
                "Camera is not using the phase-one composition.");

        Debug.Log(
            $"ECHO_RUNNER_PHASE_ONE_VALIDATE_OK segments={segmentCount} " +
            $"renderers={spineRenderers.Length + shadowRenderers.Length} " +
            $"colliders={addedColliders.Length} camera={expectedOffset}");
    }

    private static GameObject BuildMemorySpinePrefab()
    {
        EnsureFolder(AssetFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(MeshFolder);

        Material shell = LoadOrCreateIdentityMaterial(
            MaterialFolder + "/EchoMemorySpineShell.mat",
            new Color(0.085f, 0.100f, 0.115f, 1f),
            new Color(0.34f, 0.39f, 0.43f, 1f),
            0f, 0f, 0f, 0.72f, 0.68f);
        Material passiveSignal = LoadOrCreateIdentityMaterial(
            MaterialFolder + "/EchoMemorySpineSignal.mat",
            new Color(0.070f, 0.069f, 0.064f, 1f),
            new Color(0.38f, 0.36f, 0.33f, 1f),
            0.008f, 0f, 0f, 0.22f, 0.50f);
        Material coreSignal = LoadOrCreateIdentityMaterial(
            MaterialFolder + "/EchoMemorySpineCore.mat",
            new Color(0.110f, 0.095f, 0.080f, 1f),
            new Color(0.75f, 0.62f, 0.50f, 1f),
            0.20f, 0.05f, 2.2f, 0.22f, 0.58f);

        Mesh shellMesh = BuildPanelMesh(
            MeshFolder + "/EchoMemorySpineShell.asset",
            new[]
            {
                // One continuous tapered plate carries the whole device. The
                // overlapping collar, foot and rails keep its silhouette
                // readable against the dark back without adding renderers.
                new PanelSpec(new Vector3(0f, 0f, 0.010f),
                    Quaternion.identity,
                    0.31f, 0.17f, 0.59f, 0.060f, 0.035f),
                new PanelSpec(new Vector3(0f, 0.275f, -0.004f),
                    Quaternion.Euler(-3f, 0f, 0f),
                    0.34f, 0.29f, 0.085f, 0.064f, 0.026f),
                new PanelSpec(new Vector3(0f, -0.275f, 0.020f),
                    Quaternion.Euler(4f, 0f, 0f),
                    0.18f, 0.145f, 0.080f, 0.056f, 0.022f),
                new PanelSpec(new Vector3(-0.125f, 0.015f, -0.027f),
                    Quaternion.identity,
                    0.036f, 0.027f, 0.47f, 0.018f, 0.009f),
                new PanelSpec(new Vector3(0.125f, 0.015f, -0.027f),
                    Quaternion.identity,
                    0.036f, 0.027f, 0.47f, 0.018f, 0.009f)
            });
        Mesh passiveMesh = BuildPanelMesh(
            MeshFolder + "/EchoMemorySpineSignal.asset",
            new[]
            {
                new PanelSpec(new Vector3(0f, 0.18f, -0.038f),
                    Quaternion.Euler(-3f, 0f, 0f),
                    0.18f, 0.14f, 0.065f, 0.010f, 0.016f),
                new PanelSpec(new Vector3(0f, -0.18f, -0.012f),
                    Quaternion.Euler(4f, 0f, 0f),
                    0.15f, 0.12f, 0.060f, 0.010f, 0.014f)
            });
        Mesh coreMesh = BuildPanelMesh(
            MeshFolder + "/EchoMemorySpineCore.asset",
            new[]
            {
                new PanelSpec(new Vector3(0f, 0f, -0.026f),
                    Quaternion.identity,
                    0.195f, 0.16f, 0.085f, 0.011f, 0.018f)
            });

        GameObject root = new GameObject("EchoMemorySpine");
        AddMesh(root.transform, "SpineBackplate", shellMesh, shell, true);
        AddMesh(root.transform, "SpinePassiveLamps",
            passiveMesh, passiveSignal, false);
        AddMesh(root.transform, "SpineCoreLamp",
            coreMesh, coreSignal, false);

        string[] segmentNames = { "Upper", "Core", "Lower" };
        Vector2[] segmentSizes =
        {
            new Vector2(0.18f, 0.065f),
            new Vector2(0.195f, 0.085f),
            new Vector2(0.15f, 0.060f)
        };
        for (int i = 0; i < SegmentPositions.Length; i++)
        {
            GameObject marker = new GameObject(
                "MemorySegment_" + segmentNames[i]);
            marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = new Vector3(
                0f, SegmentPositions[i], -0.045f);
            marker.transform.localScale = new Vector3(
                segmentSizes[i].x, segmentSizes[i].y, 0.011f);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
            root, MemorySpinePrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        if (prefab == null)
            throw new InvalidOperationException(
                "Could not create EchoMemorySpine prefab.");
        return prefab;
    }

    private static GameObject BuildContactShadowPrefab()
    {
        EnsureFolder(AssetFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(MeshFolder);

        Shader shader = Shader.Find(ContactShadowShader);
        if (shader == null)
            throw new InvalidOperationException(
                ContactShadowShader + " shader was not compiled.");
        string materialPath =
            MaterialFolder + "/EchoRunnerContactShadow.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            materialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "EchoRunnerContactShadow"
            };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.shader = shader;
        }
        material.SetColor("_Color",
            new Color(0.012f, 0.018f, 0.026f, 0.34f));
        EditorUtility.SetDirty(material);

        Mesh shadowMesh = BuildShadowQuad(
            MeshFolder + "/EchoRunnerContactShadow.asset");
        GameObject root = new GameObject("EchoRunnerContactShadow");
        AddMesh(root.transform, "ShadowDisc", shadowMesh, material, false);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
            root, ContactShadowPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        if (prefab == null)
            throw new InvalidOperationException(
                "Could not create EchoRunnerContactShadow prefab.");
        return prefab;
    }

    private static Transform AttachMemorySpine(
        Transform model, GameObject prefab)
    {
        RemoveExisting(model, "EchoMemorySpine");
        GameObject instance =
            (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "EchoMemorySpine";
        Transform spine = instance.transform;
        FitMemorySpine(model, spine);
        return spine;
    }

    public static void FitMemorySpine(Transform model, Transform spine)
    {
        Animator animator = model.GetComponent<Animator>();
        Transform chest = animator != null && animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.Chest)
            : FindDescendant(model, "mixamorig:Spine2");
        if (chest == null) throw new InvalidOperationException("Character chest bone was not found.");
        spine.SetParent(null, true);
        spine.position = model.TransformPoint(MemorySpineAnchor);
        spine.rotation = model.rotation;
        spine.localScale = Vector3.Scale(model.lossyScale, MemorySpineScale);
        spine.SetParent(chest, true);
    }

    private static Transform AttachContactShadow(
        GameObject player, GameObject prefab)
    {
        Transform existing =
            player.transform.Find("EchoRunnerContactShadow");
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        GameObject instance =
            (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "EchoRunnerContactShadow";
        instance.transform.SetParent(player.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        return instance.transform;
    }

    private static Mesh BuildPanelMesh(string path, PanelSpec[] panels)
    {
        List<CombineInstance> combine =
            new List<CombineInstance>(panels.Length);
        List<Mesh> temporaryMeshes = new List<Mesh>(panels.Length);
        for (int i = 0; i < panels.Length; i++)
        {
            Mesh mesh = CreateTaperedPanelMesh(panels[i]);
            temporaryMeshes.Add(mesh);
            combine.Add(new CombineInstance
            {
                mesh = mesh,
                transform = Matrix4x4.TRS(
                    panels[i].position,
                    panels[i].rotation,
                    Vector3.one)
            });
        }

        Mesh generated = new Mesh
        {
            name = Path.GetFileNameWithoutExtension(path)
        };
        generated.CombineMeshes(combine.ToArray(), true, true, false);
        generated.RecalculateNormals();
        generated.RecalculateBounds();
        for (int i = 0; i < temporaryMeshes.Count; i++)
            UnityEngine.Object.DestroyImmediate(temporaryMeshes[i]);
        return SaveOrUpdateMesh(path, generated);
    }

    private static Mesh CreateTaperedPanelMesh(PanelSpec panel)
    {
        float halfHeight = panel.height * 0.5f;
        float halfDepth = panel.depth * 0.5f;
        float topHalf = panel.topWidth * 0.5f;
        float bottomHalf = panel.bottomWidth * 0.5f;
        float chamfer = Mathf.Min(panel.chamfer,
            Mathf.Min(topHalf, bottomHalf) * 0.72f);
        Vector2[] outline =
        {
            new Vector2(-topHalf + chamfer, halfHeight),
            new Vector2(topHalf - chamfer, halfHeight),
            new Vector2(topHalf, halfHeight - chamfer),
            new Vector2(bottomHalf, -halfHeight + chamfer),
            new Vector2(bottomHalf - chamfer, -halfHeight),
            new Vector2(-bottomHalf + chamfer, -halfHeight),
            new Vector2(-bottomHalf, -halfHeight + chamfer),
            new Vector2(-topHalf, halfHeight - chamfer)
        };

        const int outlineCount = 8;
        Vector3[] vertices = new Vector3[outlineCount * 2];
        for (int i = 0; i < outlineCount; i++)
        {
            vertices[i] = new Vector3(
                outline[i].x, outline[i].y, -halfDepth);
            vertices[i + outlineCount] = new Vector3(
                outline[i].x, outline[i].y, halfDepth);
        }

        List<int> triangles = new List<int>(84);
        for (int i = 1; i < outlineCount - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(i + 1);

            triangles.Add(outlineCount);
            triangles.Add(outlineCount + i + 1);
            triangles.Add(outlineCount + i);
        }

        for (int i = 0; i < outlineCount; i++)
        {
            int next = (i + 1) % outlineCount;
            triangles.Add(i);
            triangles.Add(outlineCount + next);
            triangles.Add(next);
            triangles.Add(i);
            triangles.Add(outlineCount + i);
            triangles.Add(outlineCount + next);
        }

        Mesh mesh = new Mesh
        {
            name = "IntegratedMemorySpinePanel",
            vertices = vertices,
            triangles = triangles.ToArray()
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildShadowQuad(string path)
    {
        Mesh generated = new Mesh
        {
            name = Path.GetFileNameWithoutExtension(path),
            vertices = new[]
            {
                new Vector3(-1f, 0f, -1f),
                new Vector3(1f, 0f, -1f),
                new Vector3(-1f, 0f, 1f),
                new Vector3(1f, 0f, 1f)
            },
            uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f)
            },
            triangles = new[] { 0, 2, 1, 2, 3, 1 }
        };
        generated.RecalculateNormals();
        generated.RecalculateBounds();
        return SaveOrUpdateMesh(path, generated);
    }

    private static Mesh SaveOrUpdateMesh(string path, Mesh generated)
    {
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

    private static void AddMesh(Transform parent, string name,
        Mesh mesh, Material material, bool castsShadows)
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

    private static Material LoadOrCreateIdentityMaterial(
        string path, Color baseColor, Color identityColor,
        float identityStrength, float pulseAmount, float pulseSpeed,
        float metallic, float smoothness)
    {
        Shader shader = Shader.Find(IdentityShader);
        if (shader == null)
            throw new InvalidOperationException(
                IdentityShader + " shader was not compiled.");

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

        material.SetColor("_BaseColor", baseColor);
        material.SetColor("_IdentityColor", identityColor);
        material.SetFloat("_IdentityStrength", identityStrength);
        material.SetFloat("_PulseAmount", pulseAmount);
        material.SetFloat("_PulseSpeed", pulseSpeed);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void RemoveExisting(Transform root, string name)
    {
        Transform existing = FindDescendant(root, name);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
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

    private static void SetColor(
        Material material, string property, Color value)
    {
        if (material.HasProperty(property)) material.SetColor(property, value);
    }

    private static void SetFloat(
        Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    private readonly struct PanelSpec
    {
        public readonly Vector3 position;
        public readonly Quaternion rotation;
        public readonly float topWidth;
        public readonly float bottomWidth;
        public readonly float height;
        public readonly float depth;
        public readonly float chamfer;

        public PanelSpec(Vector3 position, Quaternion rotation,
            float topWidth, float bottomWidth, float height,
            float depth, float chamfer)
        {
            this.position = position;
            this.rotation = rotation;
            this.topWidth = topWidth;
            this.bottomWidth = bottomWidth;
            this.height = height;
            this.depth = depth;
            this.chamfer = chamfer;
        }
    }
}
