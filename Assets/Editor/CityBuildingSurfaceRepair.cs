using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The authored deep base has a closed top at local Y zero. Butt that surface
// against its building's closed bottom instead of overlapping the differently
// shaded exterior walls. Only saved wrapper Y positions change; the FBX axis,
// facade widths, vertical depth, ornaments and gameplay geometry are retained.
public static class CityBuildingSurfaceRepair
{
    [MenuItem("Tools/Echo Runner/Stacked City Continuity/Repair Foundation Interfaces")]
    public static void Install()
    {
        int repaired = 0;
        for (int index = 0; index < 9; index++)
            repaired += RepairPrefab("Chunk" + index, "StackedDeepFooting_", "V7_GroundedTerrace");
        foreach (string side in new[] { "Left", "Right" })
            repaired += RepairPrefab("CornerQuarter" + side, "CornerFoundation_", "CornerTower_");
        AssetDatabase.SaveAssets();
        Debug.Log("CITY_BUILDING_SURFACE_REPAIR foundation interfaces=" + repaired);
    }

    static int RepairPrefab(string name, string footingPrefix, string buildingPrefix)
    {
        string path = "Assets/Resources/CityV7/" + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform[] buildings = root.transform.Cast<Transform>()
                .Where(t => t.name.StartsWith(buildingPrefix, StringComparison.Ordinal)).ToArray();
            Transform[] footings = root.transform.Cast<Transform>()
                .Where(t => t.name.StartsWith(footingPrefix, StringComparison.Ordinal)).ToArray();
            if (buildings.Length == 0 || footings.Length != buildings.Length)
                throw new InvalidOperationException("Expected one closed footing per building: " + path);
            foreach (Transform footing in footings)
            {
                Bounds lower = BoundsIn(footing, root.transform);
                Bounds[] matches = buildings.Select(t => BoundsIn(t, root.transform))
                    .Where(b => Mathf.Abs(lower.center.x - b.center.x) < .05f
                        && Mathf.Abs(lower.center.z - b.center.z) < .05f).ToArray();
                if (matches.Length != 1)
                    throw new InvalidOperationException("Ambiguous foundation interface: " + path + "/" + footing.name);
                Vector3 position = footing.localPosition;
                position.y += matches[0].min.y - lower.max.y;
                footing.localPosition = position;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return footings.Length;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Bounds BoundsIn(Transform root, Transform basis)
    {
        Bounds result = default;
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
                if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; }
                else result.Encapsulate(point);
            }
        }
        if (!initialized) throw new InvalidOperationException("Empty foundation or building: " + root.name);
        return result;
    }
}
