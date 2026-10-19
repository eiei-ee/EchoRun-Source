using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CityFinishGateInstaller
{
    private const string Art = "Assets/Art/StackedCity/";
    private const string PrefabPath = "Assets/Resources/CityV7/FinishGate.prefab";

    public static void InstallAndCapture()
    {
        Install();
        StackedCityReview.FinishGateCapture();
    }

    [MenuItem("Tools/Echo Runner/Stacked City/Install Finish Gate")]
    public static void Install()
    {
        AssetDatabase.Refresh();
        string modelPath = Art + "Models/CityFinishGate.fbx";
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null) throw new FileNotFoundException(modelPath);
        importer.addCollider = false;
        importer.importAnimation = false;
        importer.isReadable = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
        var materials = new Dictionary<string, Material>
        {
            { "SC_Concrete", RequireMaterial("Assets/CityAfterimageV7/Materials/CityV7_Pale.mat") },
            { "SC_White", RequireMaterial(Art + "Materials/SC_White.mat") },
            { "SC_Orange", RequireMaterial(Art + "Materials/SC_Orange.mat") },
            { "SC_Metal", FinishMaterial("FinishGate_Metal", "SC_Metal", new Color(.09f, .14f, .16f), false) },
            { "SC_Cyan", FinishMaterial("FinishGate_Signal", "SC_Cyan", new Color(.06f, .68f, .78f), true) }
        };
        var root = new GameObject("FinishGate");
        try
        {
            // Preserve the imported FBX's axis conversion beneath a Y-up placement root.
            GameObject model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform, false);
            model.name = "CityFinishGate";
            var signals = new List<Renderer>();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] source = renderer.sharedMaterials;
                if (source.Any(material => material != null && material.name.Split('.')[0] == "SC_Cyan"))
                    signals.Add(renderer);
                renderer.sharedMaterials = source.Select(material => materials[material.name.Split('.')[0]]).ToArray();
            }
            if (signals.Count == 0) throw new InvalidOperationException("Authored finish signal mesh missing.");
            var presentation = root.AddComponent<FinishGatePresentation>();
            presentation.signalRenderers = signals.ToArray();
            presentation.arrivalLights = new[] { ArrivalLight(root.transform, -3.6f), ArrivalLight(root.transform, 3.6f) };
            root.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("CITY_FINISH_GATE_INSTALLED " + PrefabPath + "; renderers="
                + root.GetComponentsInChildren<Renderer>(true).Length + "; signals=" + signals.Count);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Material RequireMaterial(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(path) ?? throw new FileNotFoundException(path);
    }

    private static Material FinishMaterial(string name, string sourceName, Color color, bool emission)
    {
        string path = Art + "Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var configured = new Material(RequireMaterial(Art + "Materials/" + sourceName + ".mat"));
        configured.name = name;
        configured.color = color;
        configured.SetFloat("_Metallic", emission ? .2f : .45f);
        configured.SetFloat("_Glossiness", .34f);
        configured.enableInstancing = true;
        if (emission)
        {
            configured.SetColor("_EmissionColor", new Color(.025f, .24f, .29f));
            configured.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            configured.EnableKeyword("_EMISSION");
        }
        if (material == null) { material = configured; AssetDatabase.CreateAsset(material, path); }
        else { EditorUtility.CopySerialized(configured, material); Object.DestroyImmediate(configured); }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Light ArrivalLight(Transform parent, float x)
    {
        var light = new GameObject(x < 0 ? "ArrivalLightLeft" : "ArrivalLightRight").AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.localPosition = new Vector3(x, 2.4f, -.6f);
        light.type = LightType.Point;
        light.color = new Color(.16f, .71f, .83f);
        light.intensity = .48f;
        light.range = 5.5f;
        light.shadows = LightShadows.None;
        light.enabled = false;
        return light;
    }
}
