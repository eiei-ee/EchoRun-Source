using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DetailPolishReview
{
    public const string Output = "TestResults/DetailPolish-20260925";
    public static void InstallSkin()
    {
        const string scenePath = "Assets/Scenes/SampleScene.scene";
        const string materialPath = "Assets/Art/OrangeEcho/Materials/OE_RunnerSkin.mat";
        Directory.CreateDirectory(Output);
        if (!File.Exists(Output + "/scene-before-skin.scene"))
            File.Copy(scenePath, Output + "/scene-before-skin.scene");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Models/Mixamo/ExoGray/Textures/BODY_diffuse.png"));
        material.SetColor("_Color", Color.white);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", .23f);
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Models/Mixamo/ExoGray/Textures/BODY_normal.png"));
        material.SetFloat("_BumpScale", .55f);
        material.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(material);
        Material eyes = MakeFaceMaterial("OE_RunnerEyes", "BODY", false, .34f);
        Material lashes = MakeFaceMaterial("OE_RunnerLashes", "BROW", true, .12f);
        Material eyeCover = MakeFaceMaterial("OE_RunnerEyeCover", "BROW", true, .34f);
        var scene = EditorSceneManager.OpenScene(scenePath);
        Transform model = GameObject.Find("player").transform.Find("CharacterModel");
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            // Source mesh geometry confirms this is the old helmet/neck armour,
            // not a facial part despite the imported name. It clips the courier cap.
            if (renderer.name == "OE_Preserved_EXO_Caruncula")
            { renderer.enabled = false; continue; }
            if (!renderer.enabled) continue;
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;
                Material replacement = null;
                switch (materials[i].name)
                {
                    case "Body_MAT_BlueTech": replacement = material; break;
                    case "Eye_MAT_BlueTech": replacement = eyes; break;
                    case "Brows_MAT_BlueTech": replacement = lashes; break;
                    case "Eye_Spec_MAT_BlueTech": replacement = eyeCover; break;
                }
                if (replacement != null) { materials[i] = replacement; changed = true; }
            }
            if (changed) renderer.sharedMaterials = materials;
        }
        RemoveLegacyShoulderTabs(model);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        Debug.Log("DETAIL_SKIN_INSTALLED original atlas preserved, no emissive skin");
    }
    private static void RemoveLegacyShoulderTabs(Transform model)
    {
        const string sourcePath = "Assets/Art/OrangeEcho/Meshes/OE_Preserved_EXO_BrowsLashes.asset";
        const string cleanPath = "Assets/Art/OrangeEcho/Meshes/OE_HandArmorClean.asset";
        Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(sourcePath);
        if (source == null) throw new InvalidOperationException("Original hand armour mesh missing.");
        Mesh cleaned = UnityEngine.Object.Instantiate(source);
        Vector3[] vertices = cleaned.vertices;
        int removed = 0;
        for (int sub = 0; sub < cleaned.subMeshCount; sub++)
        {
            int[] indices = cleaned.GetTriangles(sub);
            var kept = new List<int>();
            for (int t = 0; t < indices.Length; t += 3)
            {
                bool tab = true;
                for (int corner = 0; corner < 3; corner++)
                {
                    Vector3 v = vertices[indices[t + corner]];
                    tab &= v.y > 1.525f && v.y < 1.54f && Mathf.Abs(v.x) > .095f
                        && Mathf.Abs(v.x) < .14f && v.z > -.08f && v.z < -.04f;
                }
                if (tab) removed++;
                else { kept.Add(indices[t]); kept.Add(indices[t + 1]); kept.Add(indices[t + 2]); }
            }
            cleaned.SetTriangles(kept, sub);
        }
        if (removed != 4)
        {
            UnityEngine.Object.DestroyImmediate(cleaned);
            throw new InvalidOperationException("Expected exactly four legacy shoulder-tab triangles; got " + removed);
        }
        cleaned.name = "OE_HandArmorClean";
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(cleanPath);
        if (saved == null) { AssetDatabase.CreateAsset(cleaned, cleanPath); saved = cleaned; }
        else { EditorUtility.CopySerialized(cleaned, saved); UnityEngine.Object.DestroyImmediate(cleaned); }
        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            if (renderer.name == "OE_Preserved_EXO_BrowsLashes") renderer.sharedMesh = saved;
        EditorUtility.SetDirty(saved);
    }
    private static Material MakeFaceMaterial(string name, string atlas, bool cutout, float smoothness)
    {
        string path = "Assets/Art/OrangeEcho/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Models/Mixamo/ExoGray/Textures/" + atlas + "_diffuse.png"));
        material.SetColor("_Color", Color.white);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", smoothness);
        if (cutout)
        {
            material.SetFloat("_Mode", 1f);
            material.SetFloat("_Cutoff", .30f);
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = 2450;
        }
        EditorUtility.SetDirty(material);
        return material;
    }
    public static void InspectBindings()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.scene");
        var lines = new List<string>();
        Transform model = GameObject.Find("player").transform.Find("CharacterModel");
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            foreach (Material material in renderer.sharedMaterials)
                lines.Add(renderer.name + " | " + AssetDatabase.GetAssetPath(material) + " | " +
                    material.shader.name + " | " + AssetDatabase.GetAssetPath(material.mainTexture));
        }
        File.WriteAllLines(Output + "/character-bindings.txt", lines);
        lines.Clear();
        foreach (string path in new[] { "Assets/Models/Mixamo/ExoGray/ExoGray_TPose.fbx",
            "Assets/Art/OrangeEcho/Models/OrangeEchoOutfit.fbx" })
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null)
                        lines.Add(path + " | " + renderer.name + " | " + material.name + " | " +
                            AssetDatabase.GetAssetPath(material.mainTexture));
        }
        File.WriteAllLines(Output + "/source-bindings.txt", lines);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        Debug.Log("DETAIL_BINDINGS_OK");
    }
    public static void BuildWindows()
    {
        InstallSkin();
        InstallGarmentFit();
        LayeredMemoryPalette.InstallCityPresentation();
        PortraitPolishReview.Prepare();
        InspectBindings();
        string previousName = PlayerSettings.productName;
        try
        {
            PlayerSettings.productName = "EchoRun-Detail-Review";
            string path = Path.GetFullPath(Output + "/Windows-R3/EchoRun.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildReport report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/SampleScene.scene" },
                path, BuildTarget.StandaloneWindows64,
                BuildOptions.Development | BuildOptions.CompressWithLz4HC);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Detail review build failed: " + report.summary.result);
            Debug.Log("DETAIL_POLISH_BUILD_OK " + path);
        }
        finally { PlayerSettings.productName = previousName; AssetDatabase.SaveAssets(); }
    }

    public static void InstallGarmentFit()
    {
        Mesh fitted = CourierGarmentFitRefinement.Build();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.scene");
        Transform model = GameObject.Find("player").transform.Find("CharacterModel");
        SkinnedMeshRenderer garment = null;
        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (renderer.name == "OE_OrangeEchoClothing")
            {
                if (garment != null) throw new InvalidOperationException("Duplicate courier garment.");
                garment = renderer;
            }
        if (garment == null || garment.bones.Length != fitted.bindposes.Length
            || garment.sharedMaterials.Length != fitted.subMeshCount)
            throw new InvalidOperationException("Garment renderer no longer matches its source rig/materials.");
        string currentMesh = AssetDatabase.GetAssetPath(garment.sharedMesh);
        if (currentMesh != CourierGarmentFitRefinement.SourcePath
            && currentMesh != CourierGarmentFitRefinement.OutputPath)
            throw new InvalidOperationException("Unexpected garment mesh: " + currentMesh);
        garment.sharedMesh = fitted;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new IOException("Unable to save fitted garment binding.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        Debug.Log("DETAIL_GARMENT_INSTALLED existing rig and materials preserved");
    }
}
