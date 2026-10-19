using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Places authored structural assets. Every footprint remains a separate
// clearance unit; runtime only instantiates and pools the saved prefabs.
public static class StackedCityContinuity
{
    const string Art = "Assets/Art/StackedCity/";
    const string City = "Assets/Resources/CityV7/";

    public static void InstallAndCapture()
    {
        Install();
        StackedCityReview.CornerCaptureAfter();
    }

    public static void InstallAndBuild()
    {
        Install();
        StackedCityReview.CornerCaptureAndBuild();
    }

    [MenuItem("Tools/Echo Runner/Stacked City Continuity/Install Authored Structures")]
    public static void Install()
    {
        // Existing facade and wall-story bindings stay intact. This layer can
        // be reinstalled independently of texture/lighting authoring.
        Prepare("CityDeepBase");
        Prepare("CityCornerTower");
        ExtendStreetBuildings();
        BuildCorner(-1);
        BuildCorner(1);
        GroundTurnStructures();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("STACKED_CITY_CONTINUITY_INSTALLED");
    }

    static void Prepare(string name)
    {
        string path = Art + "Models/" + name + ".fbx";
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new FileNotFoundException("Missing authored continuity model", path);
        importer.addCollider = false;
        importer.importAnimation = false;
        importer.isReadable = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
    }

    static GameObject Model(string name, Transform parent, Vector3 position, Vector3 scale, float yaw = 0f)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Models/" + name + ".fbx"), root.transform, false);
        root.transform.localPosition = position;
        root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        root.transform.localScale = scale;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
            {
                string materialName = source.name.Split('.')[0];
                var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/" + materialName + ".mat");
                if (material == null) throw new InvalidOperationException("Missing continuity material: " + materialName);
                return material;
            }).ToArray();
            renderer.shadowCastingMode = renderer.sharedMaterials.Any(m => m.name == "SC_Foliage")
                ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }
        return root;
    }

    static Bounds LocalBounds(GameObject root, Transform basis)
    {
        Bounds bounds = default;
        bool initialized = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Bounds local = renderer.localBounds;
            Matrix4x4 matrix = basis.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 point = matrix.MultiplyPoint3x4(new Vector3(x == 0 ? local.min.x : local.max.x,
                    y == 0 ? local.min.y : local.max.y, z == 0 ? local.min.z : local.max.z));
                if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }

    static void RemoveChildren(Transform parent, string prefix)
    {
        foreach (Transform child in parent.Cast<Transform>().Where(t => t.name.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
            Object.DestroyImmediate(child.gameObject);
    }

    static void ExtendStreetBuildings()
    {
        for (int index = 0; index < 9; index++)
        {
            string path = City + "Chunk" + index + ".prefab";
            var chunk = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RemoveChildren(chunk.transform, "StackedDeepFooting_");
                int count = 0;
                foreach (Transform building in chunk.transform.Cast<Transform>().ToArray())
                {
                    if (building.name != "V7_GroundedTerrace") continue;
                    Bounds footprint = LocalBounds(building.gameObject, chunk.transform);
                    // Both authored mating faces are closed. A vertical overlap
                    // makes differently shaded side walls fight at their join.
                    var foundation = Model("CityDeepBase", chunk.transform,
                        new Vector3(footprint.center.x, footprint.min.y, footprint.center.z),
                        new Vector3(footprint.size.x / 20f, 1f, footprint.size.z / 20f));
                    foundation.name = "StackedDeepFooting_" + count++;
                }
                PrefabUtility.SaveAsPrefabAsset(chunk, path);
                Debug.Log("CITY_CONTINUITY_FOUNDATIONS Chunk" + index + " count=" + count);
            }
            finally { PrefabUtility.UnloadPrefabContents(chunk); }
        }
    }

    static void BuildCorner(int direction)
    {
        string name = direction < 0 ? "CornerQuarterLeft" : "CornerQuarterRight";
        var root = new GameObject(name);
        try
        {
            // The route turns at local (0, 10). Buildings ahead of the corner
            // replace the empty sky wedge, while the outgoing arm stays clear.
            Vector3[] positions = {
                new Vector3(0f, -12f, 46f), new Vector3(-30f, -16f, 52f),
                new Vector3(30f, -20f, 62f), new Vector3(-36f, -10f, 10f)
            };
            float[] scales = { 1f, .85f, 1.1f, .9f };
            for (int index = 0; index < positions.Length; index++)
            {
                Vector3 position = positions[index];
                position.x *= direction;
                var tower = Model("CityCornerTower", root.transform, position, Vector3.one * scales[index], index % 2 * 180f);
                tower.name = "CornerTower_" + index;
                Bounds footprint = LocalBounds(tower, root.transform);
                var foundation = Model("CityDeepBase", root.transform,
                    new Vector3(footprint.center.x, footprint.min.y, footprint.center.z),
                    new Vector3(footprint.size.x / 20f, 1f, footprint.size.z / 20f));
                foundation.name = "CornerFoundation_" + index;
                // Footings are below all route heights, so a culled tower never
                // removes the lower inhabited layer beside an adjacent road.
            }
            PrefabUtility.SaveAsPrefabAsset(root, City + name + ".prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    static void GroundTurnStructures()
    {
        // Retained portal piers used to stop at road height. Carry them down
        // into occupied bases, without widening or changing their road opening.
        foreach (string side in new[] { "Left", "Right" })
        {
            string path = "Assets/Prefabs/TurnSegment_" + side + ".prefab";
            var segment = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RemoveChildren(segment.transform, "StackedPortalBase_");
                int count = 0;
                foreach (Transform portal in segment.GetComponentsInChildren<Transform>(true).Where(t => t.name == "OverhangTail").ToArray())
                {
                    Bounds portalBounds = LocalBounds(portal.gameObject, segment.transform);
                    foreach (int edge in new[] { -1, 1 })
                    {
                        var foundation = Model("CityDeepBase", segment.transform,
                            new Vector3(portalBounds.center.x + edge * (portalBounds.extents.x - 2.15f),
                                Mathf.Min(-.4f, portalBounds.min.y), portalBounds.center.z), new Vector3(.215f, 1f, .35f));
                        foundation.name = "StackedPortalBase_" + count++;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(segment, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(segment); }
        }
    }
}
