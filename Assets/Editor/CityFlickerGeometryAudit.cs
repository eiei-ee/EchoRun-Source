using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Diagnostic only: never writes meshes, materials, prefabs, or scene state.
// Reads imported FBXs through the editor snapshot API even when Read/Write is off.
public static class CityFlickerGeometryAudit
{
    const float PlaneTolerance = .01f;
    sealed class Face
    {
        public Vector2[] polygon;
        public Vector3 normal, center;
        public float plane, minX, maxX, minY, maxY;
        public string owner, material, mesh;
    }
    sealed class Pair
    {
        public string description;
        public float area;
        public int triangles;
    }

    public static void Audit()
    {
        var report = new List<string> {
            "Near-coplanar, co-facing triangle overlap; tolerance=0.01m. Coordinates are prefab/root local.",
            "Same-material and downward faces are included for diagnosis; overlap alone is not a visible defect."
        };
        foreach (string segment in new[] { "TrackSegment", "TurnSegment_Left", "TurnSegment_Right" })
            AuditPrefab("Assets/Prefabs/" + segment + ".prefab", report);
        for (int i = 0; i < 4; i++)
            AuditPrefab("Assets/Resources/CityV7/StackedBlock" + i + ".prefab", report);
        AuditPrefab("Assets/Resources/CityV7/UpperTransit.prefab", report);
        for (int i = 0; i < 9; i++)
        {
            var root = new GameObject("AdjacentChunks_" + i);
            try
            {
                var a = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/CityV7/Chunk" + i + ".prefab");
                var b = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/CityV7/Chunk" + ((i + 1) % 9) + ".prefab");
                Object.Instantiate(a, root.transform, false);
                Object.Instantiate(b, root.transform, false).transform.localPosition = Vector3.forward * 20f;
                AuditRoot(root, report, root.name);
            }
            finally { Object.DestroyImmediate(root); }
        }
        Directory.CreateDirectory("TestResults/StackedCityFlicker");
        File.WriteAllLines("TestResults/StackedCityFlicker/geometry-audit.txt", report);
        Debug.Log("CITY_FLICKER_GEOMETRY_AUDIT_DONE");
    }

    public static int AuditPrefab(string path, List<string> report)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { report.Add("MISSING " + path); return 0; }
        var root = Object.Instantiate(prefab);
        try { return AuditRoot(root, report, path); }
        finally { Object.DestroyImmediate(root); }
    }

    public static int AuditRoot(GameObject root, List<string> report, string label)
    {
        var planes = new Dictionary<string, List<Face>>();
        int faceCount = 0;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            var renderer = filter.GetComponent<Renderer>();
            if (filter.sharedMesh == null || renderer == null || !renderer.enabled || renderer.sharedMaterials.Length == 0) continue;
            var mesh = filter.sharedMesh;
            using (var snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh))
            {
                var data = snapshot[0];
                using (var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                {
                    data.GetVertices(vertices);
                    Matrix4x4 transform = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var positions = new Vector3[vertices.Length];
                    for (int i = 0; i < vertices.Length; i++) positions[i] = transform.MultiplyPoint3x4(vertices[i]);
                    for (int sub = 0; sub < data.subMeshCount; sub++)
                    {
                        var descriptor = data.GetSubMesh(sub);
                        if (descriptor.topology != MeshTopology.Triangles) continue;
                        var material = renderer.sharedMaterials[Mathf.Min(sub, renderer.sharedMaterials.Length - 1)];
                        using (var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp))
                        {
                            data.GetIndices(indices, sub, true);
                            for (int i = 0; i < indices.Length; i += 3)
                            {
                                Vector3 a = positions[indices[i]], b = positions[indices[i + 1]], c = positions[indices[i + 2]];
                                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                                if (normal.sqrMagnitude < .5f) continue;
                                int axis = Mathf.Abs(normal.x) > Mathf.Abs(normal.y) ? (Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? 0 : 2) : (Mathf.Abs(normal.y) > Mathf.Abs(normal.z) ? 1 : 2);
                                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                                string key = Mathf.RoundToInt(normal.x * 1000) + ":" + Mathf.RoundToInt(normal.y * 1000) + ":" + Mathf.RoundToInt(normal.z * 1000);
                                if (!planes.TryGetValue(key, out var list)) planes[key] = list = new List<Face>();
                                list.Add(new Face {
                                    polygon = new[] { new Vector2(a[u], a[v]), new Vector2(b[u], b[v]), new Vector2(c[u], c[v]) },
                                    normal = normal, center = (a + b + c) / 3f, plane = Vector3.Dot(normal, a),
                                    minX = Mathf.Min(a[u], b[u], c[u]), maxX = Mathf.Max(a[u], b[u], c[u]),
                                    minY = Mathf.Min(a[v], b[v], c[v]), maxY = Mathf.Max(a[v], b[v], c[v]),
                                    owner = AnimationUtility.CalculateTransformPath(filter.transform, root.transform),
                                    material = material == null ? "null" : material.name,
                                    mesh = AssetDatabase.GetAssetPath(mesh)
                                });
                                faceCount++;
                            }
                        }
                    }
                }
            }
        }
        var overlaps = new Dictionary<string, Pair>();
        foreach (var plane in planes)
        {
            var faces = plane.Value.OrderBy(f => f.plane).ToArray();
            for (int i = 0; i < faces.Length; i++)
            for (int j = i + 1; j < faces.Length && faces[j].plane - faces[i].plane <= PlaneTolerance; j++)
            {
                Face a = faces[i], b = faces[j];
                if (a.maxX <= b.minX || b.maxX <= a.minX || a.maxY <= b.minY || b.maxY <= a.minY || Vector3.Dot(a.normal, b.normal) < .99999f) continue;
                float area = IntersectionArea(a.polygon, b.polygon);
                if (area < .002f) continue;
                string key = a.owner + " [" + a.material + "] <> " + b.owner + " [" + b.material + "] n=" + plane.Key + " plane=" + a.plane.ToString("F3", CultureInfo.InvariantCulture);
                if (!overlaps.TryGetValue(key, out Pair pair))
                {
                    overlaps[key] = pair = new Pair { description = key + " separation=" + Mathf.Abs(a.plane - b.plane).ToString("F6", CultureInfo.InvariantCulture)
                        + " triangleCenters=" + a.center.ToString("F3") + " / " + b.center.ToString("F3") + " meshes=" + a.mesh + " / " + b.mesh };
                }
                pair.area += area;
                pair.triangles++;
            }
        }
        report.Add(label + " triangles=" + faceCount + " overlapping plane/material pairs=" + overlaps.Count);
        foreach (var pair in overlaps.Values.OrderByDescending(p => p.area).Take(160))
            report.Add(pair.area.ToString("F5", CultureInfo.InvariantCulture) + " m2 triangles=" + pair.triangles + " " + pair.description);
        Debug.Log("CITY_FLICKER_AUDIT " + label + " pairs=" + overlaps.Count);
        return overlaps.Count;
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    static float IntersectionArea(Vector2[] a, Vector2[] b)
    {
        var polygon = new List<Vector2>(a);
        float sign = Mathf.Sign(Cross(b[1] - b[0], b[2] - b[0]));
        for (int edge = 0; edge < 3 && polygon.Count > 0; edge++)
        {
            Vector2 start = b[edge], direction = b[(edge + 1) % 3] - start;
            var output = new List<Vector2>();
            Vector2 previous = polygon[polygon.Count - 1];
            float before = sign * Cross(direction, previous - start);
            foreach (Vector2 current in polygon)
            {
                float after = sign * Cross(direction, current - start);
                if ((after >= 0) != (before >= 0)) output.Add(Vector2.LerpUnclamped(previous, current, before / (before - after)));
                if (after >= 0) output.Add(current);
                previous = current; before = after;
            }
            polygon = output;
        }
        float area = 0;
        for (int i = 0; i < polygon.Count; i++) area += Cross(polygon[i], polygon[(i + 1) % polygon.Count]);
        return Mathf.Abs(area) * .5f;
    }
}
