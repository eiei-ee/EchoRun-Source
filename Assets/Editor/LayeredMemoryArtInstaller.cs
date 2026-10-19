using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Offline authoring only. Formal meshes enter the existing CityV7 pool through
// serialized chunk children; its existing clearance pass owns turn visibility.
public static class LayeredMemoryArtInstaller
{
    private const string Art = "Assets/Art/LayeredMemory/";
    private static readonly string[] Models =
        { "LayeredTerraceQuarter", "LayeredGalleryQuarter", "LayeredArchViaduct", "LayeredRailCurve" };
    private const string ChildName = "LayeredMemoryQuarter";
    private static readonly string[] Names =
        { "LM_Mineral", "LM_Pomegranate", "LM_Stone", "LM_Ink", "LM_Glass", "LM_Window", "LM_Leaf" };
    private static readonly int[] Colors =
        { 0x7095B6, 0xB36280, 0xAAAABC, 0x293C57, 0x385575, 0xDAD2AF, 0x526F55 };

    [MenuItem("Tools/Echo Runner/Layered Memory/Install Art")]
    public static void Install()
    {
        ValidateSources();
        ConfigureTexture(Art + "Textures/MineralPlaster.png");
        ConfigureTexture(Art + "Textures/MineralPaving.png");
        foreach (string name in Models)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath(name));
            importer.addCollider = false;
            importer.importAnimation = false;
            // Rail source meshes are read only by the offline combiner. Runtime
            // references the baked mesh assets, not these source FBX files.
            importer.isReadable = name == "LayeredArchViaduct" || name == "LayeredRailCurve";
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }
        var materials = CreateMaterials();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(Models[0]));
        GameObject gallery = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(Models[1]));
        ValidateModel(model);
        ValidateModel(gallery);

        LayeredMemoryPalette.Install();
        ApplyAuthoredSurfaces();
        var report = new List<string>
        {
            "Layered Memory representative street-front art. Fixed source model, shared materials.",
            "No new runtime generator, colliders, scripts, lane or connector changes.",
            "The existing CityV7 clearance pass hides an obstructing quarter near bends."
        };
        Place(0, -1, model, materials, report);
        Place(2, 1, gallery, materials, report);
        Place(4, -1, model, materials, report);
        Place(7, 1, gallery, materials, report);
        Backup("Assets/Resources/CityV7/UpperTransit.prefab");
        LayeredMemoryRailInstaller.Install(materials, report);
        Backup("Assets/Resources/UI/EchoHud.prefab");
        Backup("Assets/Resources/UI/AsyncEchoSheet.prefab");
        EchoHudPrefabBuilder.Build();
        // The optional WeChat editor assembly continues to own its own UI.
        EditorApplication.ExecuteMenuItem("Tools/EchoRun/Rebuild Friend Challenge UI");
        StackedCityMaterials.BakeSkyReflection();
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(LayeredMemoryReview.DetailOutput);
        File.WriteAllLines(LayeredMemoryReview.DetailOutput + "/art-install.txt", report);
        Debug.Log("LAYERED_MEMORY_ART_OK quarters=4 distinctQuarterModels=2 materials=7");
    }

    public static void InstallAndCapture()
    {
        Install();
        LayeredMemoryReview.CaptureAfter();
    }

    private static void ValidateSources()
    {
        foreach (string name in Models)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(name)) == null)
                throw new InvalidOperationException("Layered Memory model missing: " + name);
        foreach (string path in new[] { Art + "Textures/MineralPlaster.png", Art + "Textures/MineralPaving.png" })
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                throw new InvalidOperationException("Layered Memory source missing: " + path);
        if (Shader.Find("EchoRun/MineralCladding") == null || Shader.Find("Standard") == null)
            throw new InvalidOperationException("Required city surface shaders are unavailable.");
        foreach (int index in new[] { 0, 2, 4, 7 })
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ChunkPath(index)) == null)
                throw new InvalidOperationException("Missing city chunk " + index);
    }

    private static void ConfigureTexture(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.maxTextureSize = 1024;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    private static Dictionary<string, Material> CreateMaterials()
    {
        Directory.CreateDirectory(Art + "Materials");
        AssetDatabase.Refresh();
        var result = new Dictionary<string, Material>();
        for (int index = 0; index < Names.Length; index++)
        {
            string path = Art + "Materials/" + Names[index] + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(index < 3 ? "EchoRun/MineralCladding" : "Standard");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            int rgb = Colors[index];
            material.color = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
            material.enableInstancing = true;
            material.SetFloat("_Glossiness", index == 4 ? .48f : .16f);
            material.SetFloat("_Metallic", index == 3 || index == 4 ? .10f : 0f);
            if (index < 3)
                BindTexture(material, "MineralPlaster", .25f);
            if (index == 5)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", material.color * .12f);
            }
            EditorUtility.SetDirty(material);
            result.Add(Names[index], material);
        }
        return result;
    }

    private static void ApplyAuthoredSurfaces()
    {
        // Reuse the current world-scale shader, without a new screen effect.
        // The previous panel relief belongs to tiled metal,
        // so its strength is zero for worn plaster and paving.
        foreach (string name in new[] { "CityV7_Pale", "CityV7_Mineral" })
        {
            string path = "Assets/CityAfterimageV7/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) throw new InvalidOperationException("Missing source cladding " + path);
            Backup(path);
            BindTexture(material, "MineralPlaster", .22f);
        }
        for (int theme = 0; theme < StackedCityDistrictMaterials.ThemeCount; theme++)
        foreach (var role in new[] { StackedCityDistrictMaterials.Role.MainConcrete,
                     StackedCityDistrictMaterials.Role.SecondaryMineral })
        {
            string path = StackedCityDistrictMaterials.MaterialPath(theme, role);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) throw new InvalidOperationException("Missing district material " + path);
            Backup(path);
            material.shader = Shader.Find("EchoRun/MineralCladding");
            BindTexture(material, "MineralPlaster", .22f);
        }
        const string road = "Assets/Art/OrangeEcho/Materials/OE_RoadDeck.mat";
        Material deck = AssetDatabase.LoadAssetAtPath<Material>(road);
        if (deck == null) throw new InvalidOperationException("Missing current road deck material.");
        Backup(road);
        Color color = deck.color;
        deck.shader = Shader.Find("EchoRun/MineralCladding");
        deck.color = color;
        deck.SetFloat("_Glossiness", .16f);
        deck.SetFloat("_Metallic", 0f);
        BindTexture(deck, "MineralPaving", .28f);
    }

    private static void BindTexture(Material material, string texture, float scale)
    {
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/" + texture + ".png"));
        material.SetFloat("_PanelScale", scale);
        material.SetFloat("_DetailStrength", 0f);
        EditorUtility.SetDirty(material);
    }

    private static void ValidateModel(GameObject model)
    {
        if (model == null || model.GetComponentsInChildren<Collider>(true).Length != 0
            || model.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
            throw new InvalidOperationException("The quarter must contain only authored visual assets.");
        int triangles = 0;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) throw new InvalidOperationException("Quarter mesh missing.");
            for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                triangles += (int)filter.sharedMesh.GetIndexCount(submesh) / 3;
        }
        if (triangles > 28000 || model.GetComponentsInChildren<Renderer>(true).Length > 7)
            throw new InvalidOperationException("Quarter exceeds its authored mesh budget: " + triangles);
        Debug.Log("LAYERED_MEMORY_MODEL triangles=" + triangles);
    }

    private static void Place(int index, int side, GameObject model,
        Dictionary<string, Material> materials, List<string> report)
    {
        string path = ChunkPath(index);
        Backup(path);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform holder = root.transform.Find(ChildName);
            if (holder == null)
            {
                holder = new GameObject(ChildName).transform;
                holder.SetParent(root.transform, false);
            }
            holder.localPosition = new Vector3(side * 17.1f, .1f, 0f);
            holder.localRotation = Quaternion.Euler(0f, -side * 90f, 0f);
            // The FBX importer owns its axis conversion; retain its local rotation.
            GameObject art = holder.childCount == 1 ? holder.GetChild(0).gameObject : null;
            if (art == null || PrefabUtility.GetCorrespondingObjectFromSource(art) != model)
            {
                for (int child = holder.childCount - 1; child >= 0; child--)
                    Object.DestroyImmediate(holder.GetChild(child).gameObject);
                art = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
            }
            foreach (Renderer renderer in art.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    string name = slots[slot] != null ? slots[slot].name : string.Empty;
                    if (!materials.TryGetValue(name, out Material material))
                        throw new InvalidOperationException("Unmapped quarter material: " + name);
                    slots[slot] = material;
                }
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            // Replace this one visual plot, not gameplay. Merely disabling it
            // is insufficient: the pool clearance pass can reactivate children.
            Transform garden = root.transform.Find("StackedRoofGarden_" + side);
            if (garden != null) Object.DestroyImmediate(garden.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            report.Add(path + " " + ChildName + " side=" + side + " origin=" + holder.localPosition);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static string ChunkPath(int index) => "Assets/Resources/CityV7/Chunk" + index + ".prefab";

    private static void Backup(string path)
    {
        if (!File.Exists(path)) return;
        string target = LayeredMemoryReview.DetailOutput + "/Baseline/" + path;
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        if (!File.Exists(target)) File.Copy(path, target);
        if (File.Exists(path + ".meta") && !File.Exists(target + ".meta")) File.Copy(path + ".meta", target + ".meta");
    }

    private static string ModelPath(string name) => Art + "Models/" + name + ".fbx";
}
