// Authoring source: copy into Assets/Editor only when the baseline capture has finished.
// No animation, rig, collider, root-scale or camera edits. Source assets are immutable.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class RunnerSilhouetteRefinement
{
    public const string SourcePath = "Assets/Art/LayeredMemory/Meshes/MemoryCourierClothing_Fitted.asset";
    public const string Root = "Assets/Art/RunnerAthleteCandidate";
    public const string OutputPath = Root + "/MemoryCourierClothing_Athlete.asset";
    public const string ReportDirectory = "TestResults/RunnerMotion-20260926/SilhouetteCandidate";
    public const string CandidateRevision = "2 - sculpted trainers and shallow textile pouch";
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";
    private const string BackupPath = ReportDirectory + "/original-binding.json";
    private const string OriginalMaterials = "Assets/Art/OrangeEcho/Materials/";
    private static readonly string[] MaterialNames = { "OE_Jacket_Accent", "OE_RelaySignal",
        "OE_Navy_Fabric", "OE_Rubber", "OE_Ivory_Fabric", "OE_RelayMetal" };

    private sealed class Part
    {
        public int Slot;
        public int[] Indices, Vertices;
        public Bounds Bounds;
        public string Role = "untouched";
        public Func<Vector3, Vector3> Map;
        public int TargetSlot;
    }

    [Serializable] private sealed class Binding
    {
        public string scene, garmentMesh, handMesh;
        public string[] garmentMaterials, handMaterials;
    }

    [MenuItem("Tools/Echo Runner/Athlete Candidate/Build Assets Only")]
    public static void BuildCandidate()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Leave Play mode before authoring.");
        Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(SourcePath);
        Require(source != null && source.vertexCount == 8475 && source.subMeshCount == 6,
            "Expected the audited fitted courier revision (8475 vertices, six slots).");
        int[] expected = { 7914, 2655, 15474, 2088, 4062, 252 };
        for (int s = 0; s < 6; s++) Require(source.GetIndexCount(s) == expected[s], "Source topology changed at slot " + s);
        Require(source.blendShapeCount == 0, "Unexpected blend shapes require a separate deformation audit.");
        Vector3[] original = source.vertices;
        Vector3[] positions = (Vector3[])original.Clone();
        Vector3[] normals = source.normals;
        Vector4[] tangents = source.tangents;
        Require(normals.Length == original.Length && tangents.Length == original.Length,
            "Source normal/tangent basis is incomplete.");
        List<Part> parts = Enumerable.Range(0, 6).SelectMany(s => Components(source.GetTriangles(s), original, s)).ToList();
        foreach (Part part in parts) part.TargetSlot = part.Slot;

        Part shirt = One(parts, p => p.Slot == 0 && p.Indices.Length == 6750, "continuous shirt");
        Require(shirt.Vertices.Length == 1409, "Shirt component identity changed.");
        Set(shirt, "shirt: shoulder slope / shaped waist", Torso);
        Part collar = One(parts, p => p.Slot == 1 && p.Bounds.min.y > 1.51f && p.Bounds.max.y < 1.62f,
            "folded collar");
        Require(collar.Indices.Length == 1347 && collar.Vertices.Length == 341, "Collar identity changed.");
        Set(collar, "collar: 40 percent lower, dark fabric", p =>
        {
            p.y = 1.518f + (p.y - 1.518f) * .60f;
            return p;
        });
        collar.TargetSlot = 2;

        Part trousers = One(parts, p => p.Slot == 2 && p.Indices.Length == 7050, "continuous trousers");
        Set(trousers, "trousers: quieter hip / waist outline", p =>
        {
            float band = Bell(p.y, .81f, .96f, 1.085f);
            p.x *= 1f - .065f * band;
            p.z = -.020f + (p.z + .020f) * (1f - .025f * band);
            return p;
        });

        Part[] straps = parts.Where(p => p.Slot == 2 && p.Indices.Length == 180 && p.Vertices.Length == 100
            && p.Bounds.min.y > 1.06f && p.Bounds.max.y > 1.50f).ToArray();
        Require(straps.Length == 2, "Fitted front/back straps must both be present.");
        foreach (Part strap in straps) Set(strap, "strap: follows torso warp", Torso);
        Part zipper = One(parts, p => p.Slot == 4 && p.Indices.Length == 180 && p.Bounds.min.y > 1.07f
            && p.Bounds.max.y > 1.52f, "front zipper");
        Set(zipper, "zipper: follows torso warp", Torso);
        Part buckle = One(parts, p => p.Slot == 5, "strap buckle");
        Set(buckle, "buckle: follows torso warp", Torso);

        Part[] archive = parts.Where(p => p.Bounds.min.y > 1.25f && p.Bounds.max.y < 1.50f
            && p.Bounds.max.z < -.10f).ToArray();
        Require(archive.Length == 5 && archive.Sum(p => p.Indices.Length) == 3402,
            "Expected the fitted archive's five authored pieces.");
        Part archiveFace = One(archive, p => p.Slot == 4, "archive face");
        archiveFace.TargetSlot = 2;
        foreach (Part part in archive.Where(p => p.Slot != 1))
            Set(part, "archive: shallow rounded textile pouch", Archive);
        // Reuse the two existing strips as tiny diagonal seam accents. No new geometry.
        Part[] strips = archive.Where(p => p.Slot == 1).OrderBy(p => p.Bounds.center.x).ToArray();
        Require(strips.Length == 2 && strips.All(p => p.Indices.Length == 384), "Archive strip identity changed.");
        for (int i = 0; i < strips.Length; i++)
        {
            Vector3 centre = strips[i].Bounds.center;
            Vector2 target = i == 0 ? new Vector2(-.040f, 1.400f) : new Vector2(-.014f, 1.391f);
            Set(strips[i], "archive signal: short diagonal seam " + i, p =>
            {
                float width = (p.x - centre.x) * .40f;
                float length = (p.y - centre.y) * .32f;
                return Archive(new Vector3(target.x + width * .342f + length * .940f,
                    target.y - width * .940f + length * .342f, p.z));
            });
        }

        Part[] shoes = parts.Where(p => p.Bounds.max.y < .15f && (p.Slot == 3 || p.Slot == 4)
            && p.Bounds.size.z > .29f).ToArray();
        Require(shoes.Length == 4 && shoes.Count(p => p.Slot == 3) == 2, "Both shoe uppers and both soles are required.");
        foreach (Part shoe in shoes) Set(shoe, shoe.Slot == 3 ? "sole: raised cushioning sidewall, contact pinned"
            : "shoe: sculpted forefoot / heel counter / instep", Shoe);
        Part[] laces = parts.Where(p => p.Slot == 2 && p.Bounds.min.y > .09f && p.Bounds.max.y < .13f
            && p.Bounds.min.z > .02f).ToArray();
        Require(laces.Length == 6, "Expected six existing lace components.");
        foreach (Part lace in laces) Set(lace, "lace: follows instep warp", Shoe);

        var changed = new HashSet<int>();
        float maxMovement = 0f;
        foreach (Part part in parts.Where(p => p.Map != null))
        foreach (int index in part.Vertices)
        {
            Require(changed.Add(index), "A vertex crosses independently authored component boundaries: " + index);
            Vector3 from = original[index], to = part.Map(from);
            Require(Finite(to), "Nonfinite deformed position.");
            positions[index] = to;
            Matrix4x4 jacobian = Jacobian(part.Map, from);
            Require(jacobian.determinant > .025f, "Folded/inverted deformation at vertex " + index);
            normals[index] = jacobian.inverse.transpose.MultiplyVector(normals[index]).normalized;
            Vector3 t = jacobian.MultiplyVector(new Vector3(tangents[index].x, tangents[index].y, tangents[index].z));
            t = (t - normals[index] * Vector3.Dot(normals[index], t)).normalized;
            tangents[index] = new Vector4(t.x, t.y, t.z, tangents[index].w);
            maxMovement = Mathf.Max(maxMovement, Vector3.Distance(from, to));
        }
        Require(maxMovement < .08f, "Candidate deformation exceeded its eight centimetre local budget.");
        for (int i = 0; i < positions.Length; i++)
            if (!changed.Contains(i)) Require(positions[i] == original[i], "Unrelated vertex changed.");
        foreach (Part shoe in shoes)
        foreach (int i in shoe.Vertices)
        {
            Require(positions[i].z == original[i].z, "Foot length changed.");
            if (original[i].y <= .015f) Require(positions[i] == original[i], "Ground contact vertex moved.");
        }

        Mesh result = Object.Instantiate(source);
        result.name = "MemoryCourierClothing_Athlete";
        result.vertices = positions;
        result.normals = normals;
        result.tangents = tangents;
        for (int s = 0; s < 6; s++) result.SetTriangles(parts.Where(p => p.TargetSlot == s).SelectMany(p => p.Indices).ToArray(), s);
        result.RecalculateBounds();
        Require(result.vertexCount == source.vertexCount && result.subMeshCount == 6, "Mesh budget changed.");
        Require(result.boneWeights.SequenceEqual(source.boneWeights), "Any bone-weight modification is forbidden.");
        Require(result.bindposes.SequenceEqual(source.bindposes), "Any bind-pose modification is forbidden.");
        Require(result.uv.SequenceEqual(source.uv) && result.uv2.SequenceEqual(source.uv2), "UVs changed.");
        Require(TriangleKeys(result).SequenceEqual(TriangleKeys(source)), "Triangle identity, winding or multiplicity changed.");
        Require(Mathf.Abs(result.bounds.min.y - source.bounds.min.y) < .000001f, "Foot contact plane changed.");
        Require(Mathf.Abs(result.bounds.max.y - source.bounds.max.y) < .000001f, "Overall character height changed.");
        EnsureFolder(Root);
        SaveAsset(result, OutputPath);
        BuildMaterials();
        AssetDatabase.SaveAssets();
        WriteReport(source, positions, parts, maxMovement, changed.Count);
        Debug.Log("RUNNER_ATHLETE_CANDIDATE_READY " + OutputPath);
    }

    [MenuItem("Tools/Echo Runner/Athlete Candidate/Install Into Existing Runner")]
    public static void Install()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Leave Play mode before installing.");
        Scene scene = OpenTarget();
        Transform model = FindModel(scene);
        SkinnedMeshRenderer garment = FindSkin(model, "OE_OrangeEchoClothing");
        SkinnedMeshRenderer hands = FindSkin(model, "OE_Preserved_EXO_BrowsLashes");
        string current = AssetDatabase.GetAssetPath(garment.sharedMesh);
        Require(current == SourcePath || current == OutputPath, "Runner is not bound to the audited fitted garment.");
        Require(AssetDatabase.GetAssetPath(hands.sharedMesh) == "Assets/Art/OrangeEcho/Meshes/OE_HandArmorClean.asset",
            "Unexpected hand mesh: re-audit before changing its material.");
        Require(garment.sharedMaterials.Length == 6 && hands.sharedMaterials.Length == 1, "Unexpected renderer material counts.");
        var invariants = Snapshot(model, scene);
        Transform[] garmentBones = garment.bones, handBones = hands.bones;
        Transform garmentRoot = garment.rootBone, handRoot = hands.rootBone;
        Mesh handMesh = hands.sharedMesh;
        Binding original = GetBinding(garment, hands);
        Directory.CreateDirectory(ReportDirectory);
        if (!File.Exists(BackupPath))
        {
            Require(current == SourcePath, "Candidate is installed but its original binding backup is missing.");
            File.WriteAllText(BackupPath, JsonUtility.ToJson(original, true));
        }
        else Require(JsonUtility.FromJson<Binding>(File.ReadAllText(BackupPath)).garmentMesh == SourcePath,
            "Existing backup belongs to another garment revision.");
        BuildCandidate();
        try
        {
            garment.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(OutputPath);
            garment.sharedMaterials = MaterialNames.Select(MaterialPath).Select(AssetDatabase.LoadAssetAtPath<Material>).ToArray();
            hands.sharedMaterials = new[] { AssetDatabase.LoadAssetAtPath<Material>(Root + "/OE_AthleteGlove.mat") };
            Require(garment.sharedMesh != null && garment.sharedMaterials.All(m => m != null)
                && hands.sharedMaterial != null, "Candidate assets failed to load.");
            Require(garment.bones.SequenceEqual(garmentBones) && hands.bones.SequenceEqual(handBones)
                && garment.rootBone == garmentRoot && hands.rootBone == handRoot && hands.sharedMesh == handMesh,
                "Renderer rig/hand geometry changed.");
            RequireSnapshots(invariants, Snapshot(model, scene));
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Could not save candidate scene binding.");
            File.WriteAllText(ReportDirectory + "/install.txt", "scene=" + ScenePath + "\nmesh=" + OutputPath
                + "\nOnly garment mesh/six materials and hand material assigned.\n"
                + "All transforms, Animator, colliders, cameras, renderer bones/root bones and hand mesh unchanged.\n"
                + "Rollback: RunnerSilhouetteRefinement.Restore(); no animation clips or controllers authored.\n"
                + "Fresh idle/run/jump/slide frames are required before acceptance.\n");
            Debug.Log("RUNNER_ATHLETE_CANDIDATE_INSTALLED");
        }
        catch
        {
            ApplyBinding(original, garment, hands);
            throw;
        }
    }

    [MenuItem("Tools/Echo Runner/Legacy/Restore Pre Athlete Appearance")]
    public static void Restore()
    {
        if (!LegacyRunnerInstallGuard.Allow("RunnerSilhouetteRefinement.Restore")) return;
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(BackupPath), "Need Edit mode and saved original binding.");
        Scene scene = OpenTarget();
        Transform model = FindModel(scene);
        ApplyBinding(JsonUtility.FromJson<Binding>(File.ReadAllText(BackupPath)),
            FindSkin(model, "OE_OrangeEchoClothing"), FindSkin(model, "OE_Preserved_EXO_BrowsLashes"));
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "Could not save restored appearance.");
        Debug.Log("RUNNER_ATHLETE_ORIGINAL_RESTORED");
    }

    private static Vector3 Torso(Vector3 p)
    {
        float centre = 1f - Smooth(.18f, .30f, Mathf.Abs(p.x));
        float waist = Bell(p.y, 1.035f, 1.16f, 1.40f) * centre;
        float chest = Bell(p.y, 1.29f, 1.405f, 1.51f) * centre;
        p.x *= 1f - .10f * waist + .018f * chest;
        p.z = -.018f + (p.z + .018f) * (1f - .035f * waist);
        // The outer shoulder, not the skeleton, gets a soft 7 mm downward contour.
        p.y -= .007f * Bell(Mathf.Abs(p.x), .095f, .185f, .29f) * Smooth(1.42f, 1.505f, p.y);
        return p;
    }

    private static Vector3 Shoe(Vector3 p)
    {
        if (p.y <= .015f) return p;
        float sourceHeight = p.y;
        float sidewall = Smooth(.015f, .07f, sourceHeight);
        float heel = 1f - Smooth(-.085f, .005f, p.z);
        float toe = Smooth(.11f, .21f, p.z);
        float centre = p.x < 0 ? -.097f : .097f;
        p.x = centre + (p.x - centre) * (1f - sidewall * (.065f * heel + .11f * toe));
        // One continuous warp for sole, upper and laces keeps their interfaces aligned.
        // Cushioning grows above the pinned bottom; no collider/foot-pivot compensation.
        p.y += (.009f + .008f * heel + .010f * toe) * Smooth(.015f, .047f, sourceHeight);
        float upper = Smooth(.042f, .13f, sourceHeight);
        p.y += .017f * Bell(p.z, -.095f, -.065f, .030f) * upper;
        p.y += .017f * Bell(p.z, -.030f, .050f, .150f) * upper;
        p.y += .020f * Smooth(.10f, .19f, p.z) * Smooth(.032f, .080f, sourceHeight);
        return p;
    }

    private static Vector3 Archive(Vector3 p)
    {
        float x = (p.x + .024f) / .078f;
        float y = (p.y - 1.377f) / .102f;
        float radius = Mathf.Sqrt(x * x + y * y);
        // Preserve the existing sewn surface while rounding the oval into a soft,
        // almost-square pouch. The source topology and all fitted skin weights stay put.
        float roundedRectangle = Mathf.Pow(Mathf.Pow(Mathf.Abs(x), 3.2f)
            + Mathf.Pow(Mathf.Abs(y), 3.2f), 1f / 3.2f);
        float corner = roundedRectangle > .00001f ? radius / roundedRectangle : 1f;
        p.x = -.024f + (p.x + .024f) * .94f * corner;
        p.y = 1.377f + (p.y - 1.377f) * .76f * corner;
        // Keep the cloth-contact face near its authored seat and halve protrusion.
        p.z = -.111f + (p.z + .111f) * .55f;
        return Torso(p);
    }

    private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
    private static float Bell(float x, float a, float peak, float b) => Smooth(a, peak, x) * (1f - Smooth(peak, b, x));
    private static void Set(Part part, string role, Func<Vector3, Vector3> map) { part.Role = role; part.Map = map; }
    private static Part One(IEnumerable<Part> parts, Func<Part, bool> predicate, string name)
    {
        Part[] found = parts.Where(predicate).ToArray();
        Require(found.Length == 1, "Cannot uniquely identify " + name + ": " + found.Length);
        return found[0];
    }

    private static Matrix4x4 Jacobian(Func<Vector3, Vector3> map, Vector3 p)
    {
        const float h = .0001f;
        Matrix4x4 m = Matrix4x4.identity;
        for (int axis = 0; axis < 3; axis++)
        {
            Vector3 delta = Vector3.zero; delta[axis] = h;
            Vector3 column = (map(p + delta) - map(p - delta)) / (2f * h);
            m.SetColumn(axis, new Vector4(column.x, column.y, column.z, 0f));
        }
        return m;
    }

    private static void BuildMaterials()
    {
        string[] colours = { "E88B36", "73C9D0", "293541", "1C2730", "DDE3E4", "77838B" };
        for (int i = 0; i < MaterialNames.Length; i++)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterials + MaterialNames[i] + ".mat");
            Require(source != null, "Missing original cloth material " + MaterialNames[i]);
            Material copy = new Material(source) { name = MaterialNames[i] + "_Athlete" };
            copy.SetColor("_Color", Colour(colours[i]));
            copy.SetFloat("_Metallic", i == 5 ? .12f : 0f);
            copy.SetFloat("_Smoothness", i == 5 ? .22f : .14f);
            copy.SetFloat("_Grain", 0f);
            SaveAsset(copy, MaterialPath(MaterialNames[i]));
        }
        Shader shader = Shader.Find("EchoRun/OrangeEchoSurface");
        Require(shader != null, "Existing cloth shader missing.");
        Material glove = new Material(shader) { name = "OE_AthleteGlove" };
        glove.SetColor("_Color", Colour("303A44"));
        glove.SetFloat("_Metallic", 0f); glove.SetFloat("_Smoothness", .10f); glove.SetFloat("_Grain", 0f);
        SaveAsset(glove, Root + "/OE_AthleteGlove.mat");
    }
    private static Color Colour(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color value); return value; }
    private static string MaterialPath(string originalName) => Root + "/" + originalName + "_Athlete.mat";

    private static void WriteReport(Mesh source, Vector3[] positions, List<Part> parts, float maximum, int authored)
    {
        Directory.CreateDirectory(ReportDirectory);
        var report = new StringBuilder();
        report.AppendLine("Runner athlete silhouette candidate: offline authoring report");
        report.AppendLine("revision=" + CandidateRevision);
        report.AppendLine("source=" + SourcePath + " (not modified)");
        report.AppendLine("output=" + OutputPath);
        report.AppendLine("Preserved: 8475 vertex identities, all 10815 oriented triangles, six material slots, every bone weight/bindpose, UVs.");
        report.AppendLine("Changed material ownership only: folded collar slot 1 -> 2; archive face slot 4 -> 2. No extra draw slot.");
        report.AppendLine("Processed component vertices=" + authored + "; max displacement m=" + F(maximum));
        Vector3[] old = source.vertices;
        foreach (Part part in parts)
        {
            Bounds after = BoundsOf(part.Vertices, positions);
            float movement = part.Vertices.Max(i => Vector3.Distance(old[i], positions[i]));
            report.AppendLine(part.Role + " | slot " + part.Slot + " -> " + part.TargetSlot
                + " | triangles=" + part.Indices.Length / 3 + " vertices=" + part.Vertices.Length
                + " | index=" + part.Vertices.Min() + ".." + part.Vertices.Max()
                + " | before=" + BoundsText(part.Bounds) + " | after=" + BoundsText(after) + " | maxMove=" + F(movement));
        }
        report.AppendLine("Assertions passed: exact triangle winding/multiplicity; all unrelated vertex positions unchanged; no inverted local warp;");
        report.AppendLine("shoe longitudinal coordinates unchanged, vertices at/below 0.015 m unchanged, overall foot plane/height unchanged;");
        report.AppendLine("component counts/slots identified uniquely before edits; all original weights, bind poses and UVs unchanged.");
        report.AppendLine("This report is geometry evidence only. It is not proof of animation, skin clipping or player acceptance.");
        File.WriteAllText(ReportDirectory + "/mesh-report.txt", report.ToString());
    }
    private static string F(float v) => v.ToString("F6", CultureInfo.InvariantCulture);
    private static string V(Vector3 v) => "(" + F(v.x) + "," + F(v.y) + "," + F(v.z) + ")";
    private static string BoundsText(Bounds b) => V(b.min) + ".." + V(b.max);
    private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x)
        && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    private static IEnumerable<string> TriangleKeys(Mesh mesh)
    {
        var keys = new List<string>();
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            int[] indices = mesh.GetTriangles(s);
            for (int t = 0; t < indices.Length; t += 3) keys.Add(indices[t] + "/" + indices[t + 1] + "/" + indices[t + 2]);
        }
        return keys.OrderBy(k => k, StringComparer.Ordinal);
    }
    private static Bounds BoundsOf(int[] vertices, Vector3[] positions)
    {
        var bounds = new Bounds(positions[vertices[0]], Vector3.zero);
        foreach (int i in vertices) bounds.Encapsulate(positions[i]);
        return bounds;
    }

    // Weld only for discovering authored connected pieces. Exported vertex identities never change.
    private static List<Part> Components(int[] triangles, Vector3[] positions, int slot)
    {
        var canonical = new Dictionary<Vector3Int, int>();
        var parent = new Dictionary<int, int>();
        var welded = new Dictionary<int, int>();
        foreach (int i in triangles.Distinct())
        {
            Vector3 p = positions[i];
            var key = new Vector3Int(Mathf.RoundToInt(p.x * 100000), Mathf.RoundToInt(p.y * 100000), Mathf.RoundToInt(p.z * 100000));
            if (!canonical.TryGetValue(key, out int owner)) { owner = i; canonical[key] = owner; parent[owner] = owner; }
            welded[i] = owner;
        }
        int Find(int id) { while (parent[id] != id) { parent[id] = parent[parent[id]]; id = parent[id]; } return id; }
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
        return groups.Values.Select(group =>
        {
            int[] vertices = group.Distinct().ToArray();
            return new Part { Slot = slot, Indices = group.ToArray(), Vertices = vertices, Bounds = BoundsOf(vertices, positions) };
        }).ToList();
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
    private static void SaveAsset(Object value, string path)
    {
        Object saved = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (saved == null) AssetDatabase.CreateAsset(value, path);
        else { EditorUtility.CopySerialized(value, saved); EditorUtility.SetDirty(saved); Object.DestroyImmediate(value); }
    }
    private static Scene OpenTarget()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded) return scene;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Require(!SceneManager.GetSceneAt(i).isDirty, "Save the current scene before opening the candidate target.");
        return EditorSceneManager.OpenScene(ScenePath);
    }
    private static Transform FindModel(Scene scene)
    {
        GameObject player = scene.GetRootGameObjects().SingleOrDefault(g => g.name == "player");
        Transform model = player != null ? player.transform.Find("CharacterModel") : null;
        Require(model != null, "Scene player/CharacterModel missing.");
        return model;
    }
    private static SkinnedMeshRenderer FindSkin(Transform model, string name)
    {
        SkinnedMeshRenderer[] matches = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name == name).ToArray();
        Require(matches.Length == 1, "Expected exactly one renderer " + name);
        return matches[0];
    }
    private static Binding GetBinding(SkinnedMeshRenderer garment, SkinnedMeshRenderer hands) => new Binding
    {
        scene = ScenePath, garmentMesh = AssetDatabase.GetAssetPath(garment.sharedMesh), handMesh = AssetDatabase.GetAssetPath(hands.sharedMesh),
        garmentMaterials = garment.sharedMaterials.Select(AssetDatabase.GetAssetPath).ToArray(),
        handMaterials = hands.sharedMaterials.Select(AssetDatabase.GetAssetPath).ToArray()
    };
    private static void ApplyBinding(Binding binding, SkinnedMeshRenderer garment, SkinnedMeshRenderer hands)
    {
        Require(binding != null && binding.scene == ScenePath, "Invalid appearance backup.");
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(binding.garmentMesh);
        Mesh hand = AssetDatabase.LoadAssetAtPath<Mesh>(binding.handMesh);
        Material[] mats = binding.garmentMaterials.Select(AssetDatabase.LoadAssetAtPath<Material>).ToArray();
        Material[] gloves = binding.handMaterials.Select(AssetDatabase.LoadAssetAtPath<Material>).ToArray();
        Require(mesh != null && hand != null && mats.All(m => m != null) && gloves.All(m => m != null), "Backup assets missing.");
        garment.sharedMesh = mesh; garment.sharedMaterials = mats; hands.sharedMesh = hand; hands.sharedMaterials = gloves;
    }
    private static Dictionary<int, string> Snapshot(Transform model, Scene scene)
    {
        var snapshot = new Dictionary<int, string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
            if (component is Transform || component is Animator || component is Collider || component is Camera)
                snapshot[component.GetInstanceID()] = EditorJsonUtility.ToJson(component);
        return snapshot;
    }
    private static void RequireSnapshots(Dictionary<int, string> before, Dictionary<int, string> after)
    {
        Require(before.Count == after.Count && before.All(p => after.TryGetValue(p.Key, out string value) && value == p.Value),
            "Transforms, animation bindings, collider or camera data changed during installation.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Runner athlete candidate: " + message);
    }
}
