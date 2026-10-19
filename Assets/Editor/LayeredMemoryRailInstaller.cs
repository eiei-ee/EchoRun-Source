using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Authoring only: replace the static bridge meshes, keeping the existing
// serialized LightRail path, train instances, timing and sound untouched.
internal static class LayeredMemoryRailInstaller
{
    private const string Art = "Assets/Art/LayeredMemory/";
    private const string PrefabPath = "Assets/Resources/CityV7/UpperTransit.prefab";

    internal static void Install(Dictionary<string, Material> materials, List<string> report)
    {
        GameObject span = RequireModel("LayeredArchViaduct");
        GameObject curve = RequireModel("LayeredRailCurve");
        GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        var authored = new GameObject("LayeredRailAuthoring");
        authored.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            Transform structure = prefab.transform.Find("StaticRoot");
            CityTransitLoop loop = prefab.GetComponentInChildren<CityTransitLoop>(true);
            if (structure == null || loop == null || loop.pathPoints == null || loop.pathPoints.Length < 4)
                throw new InvalidOperationException("Existing upper transit structure/path missing.");
            if (structure.GetComponentsInChildren<MonoBehaviour>(true).Length != 0
                || structure.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("StaticRoot has acquired non-visual ownership; do not replace it.");

            long previousTriangles = TriangleCount(structure);
            Vector3[] points = loop.pathPoints;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 a = points[i], b = points[(i + 1) % points.Length];
                float length = Vector3.Distance(a, b);
                if (length < .01f) throw new InvalidOperationException("Degenerate rail path segment.");
                Quaternion rotation = Quaternion.LookRotation((b - a).normalized);
                int count = length > 20f ? Mathf.CeilToInt(length / 24f) : 1;
                for (int part = 0; part < count; part++)
                {
                    bool straight = length > 20f;
                    var holder = new GameObject(straight ? "ArchSpan" : "CurveLink");
                    holder.transform.SetParent(authored.transform, false);
                    holder.transform.localPosition = Vector3.Lerp(a, b, (part + .5f) / count) - Vector3.up * .16f;
                    holder.transform.localRotation = rotation;
                    holder.transform.localScale = new Vector3(1f, 1f,
                        straight ? length / count / 24f : length + .70f);
                    // Retain the FBX's authored axis-conversion child transform.
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(straight ? span : curve, holder.transform);
                    foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                    {
                        Material[] slots = renderer.sharedMaterials;
                        for (int slot = 0; slot < slots.Length; slot++)
                        {
                            string key = slots[slot] != null ? slots[slot].name : string.Empty;
                            if (!materials.TryGetValue(key, out Material material))
                                throw new InvalidOperationException("Unmapped rail material: " + key);
                            slots[slot] = material;
                        }
                        renderer.sharedMaterials = slots;
                        if (renderer.bounds.min.y <= 8.4f)
                            throw new InvalidOperationException("Bridge intrudes into gameplay/camera clearance: " + renderer.bounds);
                    }
                }
            }
            long triangles = TriangleCount(authored.transform);
            if (triangles >= 65000)
                throw new InvalidOperationException("Static bridge exceeds its mesh budget: " + triangles);
            BakeInto(authored.transform, structure);
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            report.Add("UpperTransit static bridge triangles: " + previousTriangles + " -> " + triangles
                + "; batchedRenderers=" + structure.GetComponentsInChildren<Renderer>(true).Length
                + "; serialized train path/motion/audio retained.");
        }
        finally
        {
            Object.DestroyImmediate(authored);
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    private static GameObject RequireModel(string name)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Models/" + name + ".fbx");
        if (model == null || model.GetComponentsInChildren<Collider>(true).Length != 0
            || model.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
            throw new InvalidOperationException("Expected a formal, visual-only rail model: " + name);
        return model;
    }

    private static long TriangleCount(Transform root)
    {
        long triangles = 0;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
            triangles += filter.sharedMesh.GetIndexCount(sub) / 3;
        return triangles;
    }

    private static void BakeInto(Transform source, Transform target)
    {
        var batches = new Dictionary<Material, List<CombineInstance>>();
        foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            Material[] materials = filter.GetComponent<Renderer>().sharedMaterials;
            for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
            {
                Material material = materials[sub];
                if (!batches.TryGetValue(material, out var list))
                    batches[material] = list = new List<CombineInstance>();
                list.Add(new CombineInstance
                {
                    mesh = filter.sharedMesh, subMeshIndex = sub,
                    transform = source.worldToLocalMatrix * filter.transform.localToWorldMatrix
                });
            }
        }
        if (batches.Count > 4) throw new InvalidOperationException("Too many bridge material batches.");
        Directory.CreateDirectory(Art + "Meshes");
        AssetDatabase.Refresh();
        var retained = new HashSet<Transform>();
        foreach (var batch in batches.OrderBy(pair => pair.Key.name))
        {
            var mesh = new Mesh { name = "LayeredUpperRail_" + batch.Key.name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(batch.Value.ToArray(), true, true);
            mesh.RecalculateBounds();
            string path = Art + "Meshes/" + mesh.name + ".asset";
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); }
            Transform child = target.Find(batch.Key.name);
            if (child == null)
            {
                child = new GameObject(batch.Key.name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                child.SetParent(target, false);
            }
            child.GetComponent<MeshFilter>().sharedMesh = saved;
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = batch.Key;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            retained.Add(child);
        }
        for (int i = target.childCount - 1; i >= 0; i--)
            if (!retained.Contains(target.GetChild(i))) Object.DestroyImmediate(target.GetChild(i).gameObject);
    }
}
