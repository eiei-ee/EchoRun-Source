using System;
using UnityEditor;
using UnityEngine;

// Reapply only the bridge end-cap correction to existing dressed prefabs.
// Running this does not regenerate city layers or any gameplay collider.
public static class CityBridgeSurfaceRepair
{
    [MenuItem("Tools/Echo Runner/Stacked City/Repair Bridge Cross Sections")]
    public static void Apply()
    {
        int modified = 0;
        foreach (string segment in new[] { "TrackSegment", "TurnSegment_Left", "TurnSegment_Right" })
        {
            string path = "Assets/Prefabs/" + segment + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var structure = root.transform.Find("CityBridgeStructure");
                if (structure == null) throw new InvalidOperationException("Bridge structure missing: " + path);
                // Migrate only the first repair's generated mesh, which omitted
                // the authored secondary UV channel. Reclip the unchanged
                // source once; later runs keep the already complete clean mesh.
                var metal = structure.Find("RoofMetal").GetComponent<MeshFilter>();
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/Art/CityLayers/" + segment + "Bridge_1.asset");
                bool restoredUv2Source = AssetDatabase.GetAssetPath(metal.sharedMesh)
                    == "Assets/Art/CityLayers/Clean_Bridge_" + segment + "_RoofMetal.asset"
                    && source != null && source.vertexCount > 0 && source.uv2.Length == source.vertexCount
                    && metal.sharedMesh.uv2.Length != metal.sharedMesh.vertexCount;
                if (restoredUv2Source) metal.sharedMesh = source;
                int count = CitySurfaceRepair.RepairBridgeStructure(structure.gameObject, segment);
                if (count > 0 || restoredUv2Source) PrefabUtility.SaveAsPrefabAsset(root, path);
                modified += count;
                Debug.Log("CITY_BRIDGE_SURFACE_REPAIR " + segment + " clipped triangles=" + count
                    + " restoredUv2=" + restoredUv2Source);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("CITY_BRIDGE_SURFACE_REPAIR_DONE clipped triangles=" + modified);
    }
}
