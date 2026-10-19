using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Persistent presentation bindings. Existing buildings, structural interfaces,
// road colliders and runtime pooling remain owned by their current prefabs.
public static class StackedCityDistricts
{
    const string City = "Assets/Resources/CityV7/";
    const string ReportDirectory = "TestResults/StackedCityDistricts";
    const string CornerModel = "Assets/Art/StackedCity/Models/CityCornerDistrictTower.fbx";

    public static void InstallAndCapture()
    {
        Install();
        StackedCityReview.DistrictCaptureAfter();
    }

    [MenuItem("Tools/Echo Runner/Stacked City Districts/Install Building Identities")]
    public static void Install()
    {
        StackedCityDistrictMaterials.Prepare();
        StackedCityArchitecture.Prepare();
        var cornerImporter = AssetImporter.GetAtPath(CornerModel) as ModelImporter;
        if (cornerImporter == null) throw new InvalidOperationException("Missing authored corner sign-bay model");
        cornerImporter.addCollider = false;
        cornerImporter.importAnimation = false;
        cornerImporter.isReadable = false;
        cornerImporter.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        cornerImporter.SaveAndReimport();
        var report = new List<string>();
        for (int index = 0; index < 9; index++) DressChunk(index, report);
        DressCorner("Left", report);
        DressCorner("Right", report);
        DressFacilities(report);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllLines(ReportDirectory + "/installation.txt", report);
        Debug.Log("STACKED_CITY_DISTRICTS_INSTALLED " + report.Count + " building/structure bindings");
    }

    static int ThemeFor(Transform building)
    {
        // Adjacent podium roofs and deep bases overlap intentionally. Keep
        // their same-side finish consistent across chunks instead of creating
        // differently coloured coplanar surfaces at each 20 m boundary.
        if (building.name == "V7_GroundedTerrace") return building.localPosition.x < 0f ? 0 : 3;
        if (building.name.Contains("Stepped") || building.name.Contains("Civic")
            || building.name.Contains("SkylineLow")) return 1;
        if (building.name.Contains("Gallery") || building.name.Contains("SkylineMid")) return 2;
        return building.localPosition.x < 0f ? 1 : 2;
    }

    static void DressChunk(int index, List<string> report)
    {
        string path = City + "Chunk" + index + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var buildings = root.transform.Cast<Transform>()
                .Where(t => t.name.StartsWith("V7_", StringComparison.Ordinal)
                    && t.GetComponentsInChildren<Renderer>(true).Length > 0).ToArray();
            var supports = buildings.Where(t => t.name == "V7_GroundedTerrace")
                .Select(t => (bounds: BoundsIn(t, root.transform), theme: ThemeFor(t))).ToArray();
            int ordinal = 0;
            foreach (Transform building in buildings)
            {
                int theme = ThemeFor(building);
                StackedCityMaterials.RestoreNearFacadeMaterials(building.gameObject);
                StackedCityArchitecture.DecorateBuilding(building.gameObject, index * 100 + ordinal++, theme);
                int slots = StackedCityDistrictMaterials.Apply(building.gameObject, theme);
                report.Add("Chunk" + index + "/" + building.name + " theme="
                    + StackedCityDistrictMaterials.ThemeName(theme) + " slots=" + slots);
            }
            foreach (Transform footing in root.transform.Cast<Transform>()
                .Where(t => t.name.StartsWith("StackedDeepFooting_", StringComparison.Ordinal)))
            {
                Bounds lower = BoundsIn(footing, root.transform);
                var matches = supports.Where(s => Mathf.Abs(s.bounds.center.x - lower.center.x) < .05f
                    && Mathf.Abs(s.bounds.center.z - lower.center.z) < .05f).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("Unmatched structural footing: " + path + "/" + footing.name);
                StackedCityDistrictMaterials.Apply(footing.gameObject, matches[0].theme);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void DressCorner(string side, List<string> report)
    {
        string path = City + "CornerQuarter" + side + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            for (int index = 0; index < 4; index++)
            {
                var tower = root.transform.Find("CornerTower_" + index);
                var footing = root.transform.Find("CornerFoundation_" + index);
                if (tower == null || footing == null) throw new InvalidOperationException("Missing corner structure " + path);
                int theme = (index + (side == "Left" ? 0 : 2)) % 4;
                foreach (Transform child in tower.Cast<Transform>().Where(t =>
                    t.name.StartsWith("CityCornerTower", StringComparison.Ordinal)
                    || t.name.StartsWith("CityCornerDistrictTower", StringComparison.Ordinal)).ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CornerModel), tower, false);
                model.name = "CityCornerDistrictTower";
                StackedCityArchitecture.DecorateBuilding(tower.gameObject, (side == "Left" ? 3000 : 4000) + index, theme);
                StackedCityDistrictMaterials.Apply(tower.gameObject, theme);
                StackedCityDistrictMaterials.Apply(footing.gameObject, theme);
                report.Add("CornerQuarter" + side + "/" + tower.name + " theme=" + StackedCityDistrictMaterials.ThemeName(theme));
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void DressFacilities(List<string> report)
    {
        int variant = 5000;
        foreach (string side in new[] { "Left", "Right" })
        {
            string path = "Assets/Prefabs/TurnSegment_" + side + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Transform facility in root.GetComponentsInChildren<Transform>(true).Where(t =>
                    t.name == "OverhangTail" || t.name == "OuterMemorySilo" || t.name == "InnerMechanicalFacility").ToArray())
                {
                    // Refit wall stories only on these existing route landmarks.
                    StackedCityArchitecture.DecorateStructure(facility.gameObject, variant++);
                    report.Add(side + "/" + facility.name + " updated civic/transit notices");
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    static Bounds BoundsIn(Transform root, Transform basis)
    {
        Bounds result = default;
        bool initialized = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Bounds bounds = renderer.localBounds;
            Matrix4x4 matrix = basis.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
            {
                Vector3 point = matrix.MultiplyPoint3x4(new Vector3(x == 0 ? bounds.min.x : bounds.max.x,
                    y == 0 ? bounds.min.y : bounds.max.y, z == 0 ? bounds.min.z : bounds.max.z));
                if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; }
                else result.Encapsulate(point);
            }
        }
        if (!initialized) throw new InvalidOperationException("Empty building " + root.name);
        return result;
    }
}
