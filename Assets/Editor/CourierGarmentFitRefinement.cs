using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Offline correction of the current authored garment's accessories. The source
// mesh, FBX, bone order, material slots and scene binding are intentionally intact.
public static class CourierGarmentFitRefinement
{
    public const string SourcePath = "Assets/Art/LayeredMemory/Meshes/MemoryCourierClothing.asset";
    public const string OutputPath = "Assets/Art/LayeredMemory/Meshes/MemoryCourierClothing_Fitted.asset";
    private const string ReportPath = "TestResults/DetailPolish-20260925/garment-fit.txt";

    private sealed class Component
    {
        public int Submesh;
        public int[] Triangles;
        public int[] Vertices;
        public Bounds Bounds;
    }

    private struct SurfacePoint
    {
        public Vector3 Point;
        public Vector3 Normal;
        public Vector3 Barycentric;
        public int A, B, C;
    }

    [MenuItem("Tools/Echo Runner/Layered Memory/Build Fitted Garment")]
    public static void BuildFromMenu() => Build();

    // The calling installer assigns this returned mesh to the existing garment
    // renderer and saves the scene. This helper never changes scene objects.
    public static Mesh Build()
    {
        Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(SourcePath);
        Require(source != null, "Source garment missing.");
        Require(source.vertexCount == 8475 && source.subMeshCount == 6,
            "Unexpected garment revision; re-audit component identity first.");
        int[] expectedIndices = { 7914, 2655, 15474, 2088, 4062, 252 };
        for (int sub = 0; sub < expectedIndices.Length; sub++)
            Require(source.GetIndexCount(sub) == expectedIndices[sub],
                "Unexpected topology in material slot " + sub);

        Vector3[] positions = source.vertices;
        Vector3[] normals = source.normals;
        BoneWeight[] originalWeights = source.boneWeights;
        Require(normals.Length == positions.Length && originalWeights.Length == positions.Length,
            "Source normals / skin weights missing.");
        var components = new List<Component>();
        for (int sub = 0; sub < source.subMeshCount; sub++)
            components.AddRange(FindComponents(source.GetTriangles(sub), positions, sub));

        Component shirt = components.Where(c => c.Submesh == 0)
            .OrderByDescending(c => c.Triangles.Length).First();
        Require(shirt.Triangles.Length == 2250 * 3 && shirt.Vertices.Length == 1409,
            "The closed tailored shirt could not be identified.");
        Require(CountBoundaryEdges(shirt.Triangles, positions) == 0,
            "Shirt source must remain a closed surface.");

        Component front = FindStrap(components, true);
        Component back = FindStrap(components, false);
        Require(front.Vertices.Min() == 5668 && front.Vertices.Max() == 5767,
            "Front strap vertex identity changed.");
        Require(back.Vertices.Min() == 5768 && back.Vertices.Max() == 5867,
            "Back strap vertex identity changed.");

        Component[] archive = components.Where(c =>
            c.Bounds.min.y > 1.25f && c.Bounds.max.y < 1.50f
            && c.Bounds.max.z < -0.10f).ToArray();
        Require(archive.Length == 5 && archive.Sum(c => c.Triangles.Length / 3) == 1134
            && archive.Sum(c => c.Vertices.Length) == 869,
            "Expected archive body, face, flap and two memory strips.");
        Require(archive.Select(c => c.Submesh).OrderBy(i => i)
            .SequenceEqual(new[] { 0, 1, 1, 2, 4 }), "Archive material ownership changed.");

        Vector3[] fittedPositions = (Vector3[])positions.Clone();
        Vector3[] fittedNormals = (Vector3[])normals.Clone();
        BoneWeight[] fittedWeights = (BoneWeight[])originalWeights.Clone();
        var touched = new HashSet<int>();
        float maximumMovement = 0f;
        FitStrap(front, 1f, shirt, positions, normals, originalWeights,
            fittedPositions, fittedNormals, fittedWeights, touched, ref maximumMovement);
        FitStrap(back, -1f, shirt, positions, normals, originalWeights,
            fittedPositions, fittedNormals, fittedWeights, touched, ref maximumMovement);

        // The archive volume and every decorative layer keep their authored
        // positions. Their nearest shirt weights replace the rigid Spine2-only
        // binding, so the lower bag no longer slides away from the bending torso.
        foreach (Component part in archive)
        foreach (int index in part.Vertices)
        {
            SurfacePoint point = ClosestSurface(positions[index], -1f,
                shirt.Triangles, positions, normals);
            fittedWeights[index] = InterpolateWeights(point, originalWeights);
            touched.Add(index);
        }

        Require(touched.Count == 1069, "Unexpected affected vertex count.");
        Require(maximumMovement < 0.035f, "Strap correction exceeded 3.5 cm.");
        for (int i = 0; i < fittedWeights.Length; i++)
        {
            BoneWeight weight = fittedWeights[i];
            Require(Mathf.Abs(weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3 - 1f)
                < 0.001f, "Unnormalized fitted weight at vertex " + i);
            if (!touched.Contains(i))
                Require(fittedPositions[i] == positions[i]
                    && fittedWeights[i].Equals(originalWeights[i]),
                    "Unrelated garment vertex changed: " + i);
        }
        foreach (Component part in archive)
        foreach (int index in part.Vertices)
            Require(fittedPositions[index] == positions[index], "Archive shape changed.");

        Mesh fitted = Object.Instantiate(source);
        fitted.name = "MemoryCourierClothing_Fitted";
        fitted.vertices = fittedPositions;
        fitted.normals = fittedNormals;
        fitted.boneWeights = fittedWeights;
        fitted.RecalculateBounds();
        fitted.RecalculateTangents();
        Require(fitted.bindposes.SequenceEqual(source.bindposes), "Bind poses changed.");
        for (int sub = 0; sub < source.subMeshCount; sub++)
            Require(fitted.GetTriangles(sub).SequenceEqual(source.GetTriangles(sub)),
                "Material slot topology changed: " + sub);

        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(OutputPath);
        if (saved == null)
        {
            AssetDatabase.CreateAsset(fitted, OutputPath);
            saved = fitted;
        }
        else
        {
            EditorUtility.CopySerialized(fitted, saved);
            Object.DestroyImmediate(fitted);
            EditorUtility.SetDirty(saved);
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        var report = new StringBuilder();
        report.AppendLine("source=" + SourcePath + " (preserved)");
        report.AppendLine("output=" + OutputPath);
        report.AppendLine("shirt: material slot 0, 2250 triangles, 1409 vertices, closed after positional welding");
        report.AppendLine("front sling: slot 2, vertices 5668..5767, 60 triangles");
        report.AppendLine("back sling: slot 2, vertices 5768..5867, 60 triangles");
        report.AppendLine("archive: slots 0/1/1/2/4, 1134 triangles, 869 vertices; weights only");
        report.AppendLine("strap centre surface clearance=0.005m; original ribbon depth retained");
        report.AppendLine("maximumStrapVertexMoveMetres=" + maximumMovement.ToString("F6",
            System.Globalization.CultureInfo.InvariantCulture));
        report.AppendLine("modifiedVertices=" + touched.Count);
        report.AppendLine("Unchanged: all source files, triangle indices, material slots, bind poses, body/limb geometry.");
        report.AppendLine("This is an offline mesh binding check. Fresh animated runtime frames are still required.");
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log("COURIER_GARMENT_FIT_READY " + OutputPath);
        return saved;
    }

    private static Component FindStrap(List<Component> components, bool front)
    {
        Component[] matches = components.Where(c => c.Submesh == 2
            && c.Triangles.Length == 180 && c.Vertices.Length == 100
            && c.Bounds.min.y > 1.06f && c.Bounds.max.y > 1.50f
            && (front ? c.Bounds.min.z > 0f : c.Bounds.max.z < 0f)).ToArray();
        Require(matches.Length == 1, "Cannot uniquely locate " + (front ? "front" : "back") + " sling.");
        return matches[0];
    }

    private static void FitStrap(Component strap, float side, Component shirt,
        Vector3[] positions, Vector3[] normals, BoneWeight[] weights,
        Vector3[] fitted, Vector3[] fittedNormals, BoneWeight[] fittedWeights,
        HashSet<int> touched, ref float maximumMovement)
    {
        // Ribbon depth pairs have identical X/Y coordinates. Project their
        // shared centre, not each face, to avoid collapsing the closed ribbon.
        var pairs = strap.Vertices.GroupBy(i => new Vector2Int(
            Mathf.RoundToInt(positions[i].x * 100000f),
            Mathf.RoundToInt(positions[i].y * 100000f)));
        foreach (var pair in pairs)
        {
            int[] indices = pair.ToArray();
            float min = indices.Min(i => positions[i].z);
            float max = indices.Max(i => positions[i].z);
            Require(Mathf.Abs(max - min - 0.005f) < 0.0001f,
                "Unexpected ribbon depth pair.");
            Vector3 centre = positions[indices[0]];
            centre.z = (min + max) * 0.5f;
            SurfacePoint surface = ClosestSurface(centre, side,
                shirt.Triangles, positions, normals);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.forward * side, surface.Normal);
            foreach (int index in indices)
            {
                float depth = (positions[index].z - centre.z) * side;
                fitted[index] = surface.Point + surface.Normal * (0.005f + depth);
                fittedNormals[index] = (rotation * normals[index]).normalized;
                fittedWeights[index] = InterpolateWeights(surface, weights);
                maximumMovement = Mathf.Max(maximumMovement,
                    Vector3.Distance(fitted[index], positions[index]));
                touched.Add(index);
            }
        }
    }

    private static SurfacePoint ClosestSurface(Vector3 point, float side,
        int[] triangles, Vector3[] positions, Vector3[] normals)
    {
        float nearest = float.PositiveInfinity;
        SurfacePoint result = default;
        for (int t = 0; t < triangles.Length; t += 3)
        {
            int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
            Vector3 bary = ClosestBarycentric(point, positions[a], positions[b], positions[c]);
            Vector3 normal = (normals[a] * bary.x + normals[b] * bary.y + normals[c] * bary.z).normalized;
            if (normal.z * side < 0.12f) continue;
            Vector3 closest = positions[a] * bary.x + positions[b] * bary.y + positions[c] * bary.z;
            float distance = (point - closest).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            result = new SurfacePoint { Point = closest, Normal = normal,
                Barycentric = bary, A = a, B = b, C = c };
        }
        Require(!float.IsInfinity(nearest), "No outward shirt surface found.");
        return result;
    }

    private static BoneWeight InterpolateWeights(SurfacePoint point, BoneWeight[] weights)
    {
        var sums = new Dictionary<int, float>();
        Add(weights[point.A], point.Barycentric.x);
        Add(weights[point.B], point.Barycentric.y);
        Add(weights[point.C], point.Barycentric.z);
        var ranked = sums.Where(p => p.Value > 0f).OrderByDescending(p => p.Value).Take(4).ToArray();
        float sum = ranked.Sum(p => p.Value);
        Require(sum > 0.999f, "Surface transfer would discard significant skin influence.");
        int[] ids = new int[4];
        float[] values = new float[4];
        for (int i = 0; i < ranked.Length; i++)
        { ids[i] = ranked[i].Key; values[i] = ranked[i].Value / sum; }
        return new BoneWeight { boneIndex0 = ids[0], boneIndex1 = ids[1], boneIndex2 = ids[2], boneIndex3 = ids[3],
            weight0 = values[0], weight1 = values[1], weight2 = values[2], weight3 = values[3] };

        void Add(BoneWeight weight, float factor)
        {
            Accumulate(weight.boneIndex0, weight.weight0 * factor);
            Accumulate(weight.boneIndex1, weight.weight1 * factor);
            Accumulate(weight.boneIndex2, weight.weight2 * factor);
            Accumulate(weight.boneIndex3, weight.weight3 * factor);
        }
        void Accumulate(int bone, float value)
        {
            sums[bone] = (sums.TryGetValue(bone, out float previous) ? previous : 0f) + value;
        }
    }

    private static Vector3 ClosestBarycentric(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return new Vector3(1, 0, 0);
        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return new Vector3(0, 1, 0);
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
        { float v = d1 / (d1 - d3); return new Vector3(1 - v, v, 0); }
        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return new Vector3(0, 0, 1);
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
        { float w = d2 / (d2 - d6); return new Vector3(1 - w, 0, w); }
        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
        { float w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); return new Vector3(0, 1 - w, w); }
        float denominator = 1f / (va + vb + vc);
        float insideV = vb * denominator, insideW = vc * denominator;
        return new Vector3(1f - insideV - insideW, insideV, insideW);
    }

    private static List<Component> FindComponents(int[] triangles, Vector3[] positions, int submesh)
    {
        var canonical = new Dictionary<Vector3Int, int>();
        var parent = new Dictionary<int, int>();
        var welded = new Dictionary<int, int>();
        foreach (int index in triangles.Distinct())
        {
            Vector3Int key = PositionKey(positions[index]);
            if (!canonical.TryGetValue(key, out int owner))
            { owner = index; canonical[key] = owner; parent[owner] = owner; }
            welded[index] = owner;
        }
        int Find(int id)
        {
            while (parent[id] != id) { parent[id] = parent[parent[id]]; id = parent[id]; }
            return id;
        }
        for (int t = 0; t < triangles.Length; t += 3)
        {
            parent[Find(welded[triangles[t + 1]])] = Find(welded[triangles[t]]);
            parent[Find(welded[triangles[t + 2]])] = Find(welded[triangles[t]]);
        }
        var groups = new Dictionary<int, List<int>>();
        for (int t = 0; t < triangles.Length; t += 3)
        {
            int root = Find(welded[triangles[t]]);
            if (!groups.TryGetValue(root, out List<int> group)) groups[root] = group = new List<int>();
            group.Add(triangles[t]); group.Add(triangles[t + 1]); group.Add(triangles[t + 2]);
        }
        var result = new List<Component>();
        foreach (List<int> group in groups.Values)
        {
            int[] vertices = group.Distinct().ToArray();
            var bounds = new Bounds(positions[vertices[0]], Vector3.zero);
            foreach (int index in vertices) bounds.Encapsulate(positions[index]);
            result.Add(new Component { Submesh = submesh, Triangles = group.ToArray(), Vertices = vertices, Bounds = bounds });
        }
        return result;
    }

    private static int CountBoundaryEdges(int[] triangles, Vector3[] positions)
    {
        var counts = new Dictionary<string, int>();
        for (int t = 0; t < triangles.Length; t += 3)
        for (int edge = 0; edge < 3; edge++)
        {
            string a = PositionKey(positions[triangles[t + edge]]).ToString();
            string b = PositionKey(positions[triangles[t + (edge + 1) % 3]]).ToString();
            string key = string.CompareOrdinal(a, b) < 0 ? a + "/" + b : b + "/" + a;
            counts[key] = (counts.TryGetValue(key, out int count) ? count : 0) + 1;
        }
        return counts.Count(pair => pair.Value != 2);
    }

    private static Vector3Int PositionKey(Vector3 p) => new Vector3Int(
        Mathf.RoundToInt(p.x * 100000f), Mathf.RoundToInt(p.y * 100000f), Mathf.RoundToInt(p.z * 100000f));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Garment fit: " + message);
    }
}
