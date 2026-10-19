using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Fits authored facade modules to existing, supported wall surfaces at edit time.</summary>
public static class StackedCityArchitecture
{
    const string Art = "Assets/Art/StackedCity/";
    const string ChildName = "StackedArchitecturalDetail";
    static readonly string[] ModelNames = new[] { "FacadeBay", "FacadeCornice", "Wayfinding07", "Wayfinding12", "Wayfinding21",
        "WallDistrictA", "WallDistrictB", "WallTransit", "WallService", "WallMemory", "WallGround", "WallCat",
        "FacadeResidential", "FacadeCivic", "FacadeTransit", "FacadeHeritage" }
        .Concat(StackedCityWallCatalog.ModelNames).ToArray();
    static readonly string[] FacadeModels = { "FacadeResidential", "FacadeCivic", "FacadeTransit", "FacadeHeritage" };
    static readonly Dictionary<string, List<Part>> Models = new Dictionary<string, List<Part>>();
    static readonly Dictionary<Mesh, Geometry> GeometryCache = new Dictionary<Mesh, Geometry>();
    static Mesh cube;
    struct Part { public Mesh mesh; public Matrix4x4 matrix; public string material; }
    struct Triangle { public Vector3 a, b, c; }
    sealed class Geometry { public Vector3[] vertices, normals; public int[][] triangles; }
    sealed class Wall
    {
        public Vector3 normal, right;
        public float plane, minU = float.PositiveInfinity, maxU = float.NegativeInfinity;
        public float minV = float.PositiveInfinity, maxV = float.NegativeInfinity;
        public readonly List<Triangle> triangles = new List<Triangle>();
        public float Width => maxU - minU;
        public float Height => maxV - minV;
        public Vector3 Point(float u, float v, float depth = 0f) => right * u + Vector3.up * v + normal * (plane + depth);
        public bool Contains(float u, float v)
        {
            var p = Point(u, v);
            foreach (var t in triangles)
            {
                var ab = t.b - t.a; var ac = t.c - t.a; var ap = p - t.a;
                float aa = Vector3.Dot(ab, ab), bb = Vector3.Dot(ac, ac), cc = Vector3.Dot(ab, ac);
                float d = aa * bb - cc * cc;
                if (d < .000001f) continue;
                float x = (bb * Vector3.Dot(ap, ab) - cc * Vector3.Dot(ap, ac)) / d;
                float y = (aa * Vector3.Dot(ap, ac) - cc * Vector3.Dot(ap, ab)) / d;
                if (x >= -.0001f && y >= -.0001f && x + y <= 1.0001f) return true;
            }
            return false;
        }
        public bool Supports(float u, float v, float width, float height)
        {
            int nx = Mathf.Max(1, Mathf.CeilToInt(width / .8f));
            int ny = Mathf.Max(1, Mathf.CeilToInt(height / .8f));
            for (int x = 0; x <= nx; x++)
            for (int y = 0; y <= ny; y++)
                if (!Contains(u - width * .5f + width * x / nx, v - height * .5f + height * y / ny)) return false;
            return true;
        }
    }

    /// <summary>Call once after import and before decorating a group of buildings.</summary>
    public static void Prepare()
    {
        Directory.CreateDirectory(Art + "ArchitectureMeshes");
        AssetDatabase.Refresh();
        Models.Clear();
        GeometryCache.Clear();
        if (cube == null)
        {
            var temporary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = temporary.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(temporary);
        }
        foreach (string name in ModelNames)
        {
            string path = Art + "Models/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing authored architecture model: " + path);
            importer.isReadable = true;
            importer.addCollider = false;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            instance.transform.position = Vector3.zero; // Preserve the imported up-axis conversion.
            try
            {
                var parts = new List<Part>();
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<Renderer>();
                    if (renderer == null || filter.sharedMesh == null) continue;
                    if (filter.sharedMesh.subMeshCount != 1) throw new InvalidOperationException("Architecture modules must be merged by material: " + name);
                    parts.Add(new Part { mesh = filter.sharedMesh, matrix = filter.transform.localToWorldMatrix,
                        material = renderer.sharedMaterial.name.Split('.')[0] });
                }
                Models[name] = parts;
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
    }

    /// <summary>
    /// Decorates a source building before a parent bake, or a trackside building in its prefab.
    /// variant must be stable and unique within the install; all output is persisted, collider free.
    /// </summary>
    public static void DecorateBuilding(GameObject building, int variant, int theme = -1)
    {
        if (Models.Count == 0) Prepare();
        if (theme < 0) theme = Mathf.Abs(variant / 100 + variant % 100) % 4;
        var previous = building.transform.Find(ChildName);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        previous = building.transform.Find("StackedWallStories");
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        // Gallery piers are narrower than a window bay, but still support the
        // authored cornice. Each feature keeps its own full-footprint check.
        var storyWalls = FindWalls(building).Where(w => w.Width >= 1.3f && w.Height >= 3.0f)
            .OrderByDescending(w => w.Width * w.Height).ToList();
        // The window-detail budget must not discard a smaller, usable podium
        // sign bay just because a tower has ten larger upper-storey faces.
        var walls = storyWalls.Take(10).ToList();
        if (walls.Count == 0)
        {
            Debug.Log("STACKED_ARCHITECTURE_NO_WALLS " + building.name + " variant=" + variant);
            return;
        }
        var glazing = FindGlazing(building);
        // Reserve actual supported wall space before filling secondary faces with windows.
        // Tiny lettering in the distant lower districts would only add geometry cost.
        bool hasDistrictSign = (variant < 1000 || variant >= 2000)
            && DecorateWallStories(building, variant, storyWalls, glazing, theme);
        var root = new GameObject(ChildName);
        root.transform.SetParent(building.transform, false);
        var batches = new Dictionary<string, List<CombineInstance>>();
        int bays = 0, cornices = 0, joints = 0;
        bool signed = hasDistrictSign;
        foreach (var wall in walls)
        {
            Quaternion rotation = Quaternion.LookRotation(wall.normal, Vector3.up);
            // Sparse real window portals fill formerly blank secondary masses. Existing panes
            // are detected from connected mesh pieces and never covered by new decoration.
            float pitchX = new[] { 4.2f, 4.7f, 3.8f, 4.4f }[theme];
            float pitchY = new[] { 3.8f, 4.4f, 3.4f, 4.1f }[theme];
            int columns = Mathf.Min(5, Mathf.FloorToInt((wall.Width - 1f) / pitchX));
            int rows = Mathf.Min(6, Mathf.FloorToInt((wall.Height - .6f) / pitchY));
            for (int row = 0; row < rows && bays < 18; row++)
            for (int column = 0; column < columns && bays < 18; column++)
            {
                float u = (wall.minU + wall.maxU) * .5f + (column + .5f - columns * .5f) * pitchX;
                float v = wall.minV + 2.2f + row * pitchY;
                if (!wall.Supports(u, v, 3.10f, 2.24f) || Occluded(wall, glazing, u, v, 3.2f, 2.36f)) continue;
                // One clear vertical bay stays available for district signs on broad walls.
                if (column == 0 && wall.Width > 9f && row < 2) continue;
                AddModule(batches, root.transform, FacadeModels[theme], wall.Point(u, v, .016f), rotation, Vector3.one);
                glazing.Add(new Bounds(wall.Point(u, v, .18f), Abs(wall.right) * 3.1f + Vector3.up * 2.24f + Abs(wall.normal) * .36f));
                bays++;
            }
            // A limited set of ledges produces actual contact shadows on the tall white faces.
            for (float v = wall.minV + .28f; v < wall.maxV - .12f && cornices < 24; v += 7.6f)
            {
                float u = (wall.minU + wall.maxU) * .5f;
                float width = wall.Width - .4f;
                if (!wall.Supports(u, v, width, .26f) || Occluded(wall, glazing, u, v, width, .34f)) continue;
                AddModule(batches, root.transform, "FacadeCornice", wall.Point(u, v, .012f), rotation, new Vector3(width, 1f, 1f));
                cornices++;
            }
            // Narrow low-contrast panel reveals are separately baked, with gaps at glazing.
            for (float v = wall.minV + 3.8f; v < wall.maxV - .6f && joints < 28; v += 7.6f)
            for (float u = wall.minU + 1.7f; u < wall.maxU - 1.1f && joints < 28; u += 6.8f)
            {
                if (!wall.Supports(u, v, 3.1f, .045f) || Occluded(wall, glazing, u, v, 3.18f, .09f)) continue;
                AddBox(batches, root.transform, wall.Point(u, v, .008f), rotation, new Vector3(3.1f, .022f, .012f), "SC_Joint");
                // Staggered short vertical reveal avoids a perfect graph-paper facade.
                float jointU = u + (((variant + joints) & 1) == 0 ? -1.53f : 1.53f);
                if (wall.Supports(jointU, v - 1.65f, .04f, 3.2f) && !Occluded(wall, glazing, jointU, v - 1.65f, .09f, 3.25f))
                    AddBox(batches, root.transform, wall.Point(jointU, v - 1.65f, .008f), rotation, new Vector3(.022f, 3.2f, .012f), "SC_Joint");
                joints++;
            }
            if (!signed && wall.Width >= 7f && wall.Height >= 7f)
            {
                float u = wall.minU + 2.1f;
                float v = Mathf.Clamp(5f, wall.minV + 3.15f, wall.maxV - 3.15f);
                if (wall.Supports(u, v, 2.9f, 5.3f) && !Occluded(wall, glazing, u, v, 3f, 5.4f))
                {
                    AddModule(batches, root.transform, new[] { "Wayfinding07", "Wayfinding12", "Wayfinding21" }[Mathf.Abs(variant) % 3],
                        wall.Point(u, v, .02f), rotation, Vector3.one);
                    signed = true;
                }
            }
        }
        Save(root, batches, building.name, variant);
        Debug.Log("STACKED_ARCHITECTURE_DETAIL " + building.name + " variant=" + variant + " facade=" + FacadeModels[theme] + " walls=" + walls.Count + " bays=" + bays
            + " cornices=" + cornices + " joints=" + joints + " sign=" + signed + " renderers=" + batches.Count);
        if (root.transform.childCount == 0) UnityEngine.Object.DestroyImmediate(root);
    }

    static bool DecorateWallStories(GameObject building, int variant, List<Wall> walls, List<Bounds> occupied, int theme)
    {
        var root = new GameObject("StackedWallStories");
        root.transform.SetParent(building.transform, false);
        var batches = new Dictionary<string, List<CombineInstance>>();
        int choice = Mathf.Abs(variant / 100 + variant % 100);
        var blockers = FindWalls(building, false);
        float streetSide = Mathf.Abs(building.transform.position.x) < 1f ? 0f : Mathf.Sign(building.transform.position.x);
        // Prioritize faces turned toward the road, then the approaching runner.
        var visibleWalls = walls.OrderByDescending(w => -w.normal.x * streetSide * 2f - w.normal.z
            + Mathf.Min(w.Width * w.Height, 500f) * .001f).ToList();
        int signs = 0, services = 0, cats = 0;
        bool district = false;
        foreach (var wall in visibleWalls)
        {
            if (!district && signs < 2 && TryStory(root.transform, batches, occupied, blockers, wall,
                StackedCityWallCatalog.DistrictModel(theme), new Vector2(2.4f, 6.8f), 6.4f, choice))
            {
                district = true;
                signs++;
            }
            if (signs < (district ? 2 : 1))
            {
                // Shops, community notices and occasional quiet jokes share a
                // consistent sign system, but neighbouring buildings get new wording.
                string model = StackedCityWallCatalog.NoticeModel(choice);
                Vector2 size = new Vector2(2.6f, 2.2f);
                float scale = StackedCityWallCatalog.NoticeScale(model);
                bool placed = TryStory(root.transform, batches, occupied, blockers, wall, model, size, 3.3f, choice + 1, scale);
                // Narrow piers keep the original sign instead of losing it or covering windows.
                if (!placed && scale > 1f)
                    placed = TryStory(root.transform, batches, occupied, blockers, wall, model, size, 3.3f, choice + 1);
                if (placed) signs++;
            }
            if (services < 2 && TryStory(root.transform, batches, occupied, blockers, wall,
                "WallService", new Vector2(1.4f, 1.6f), 2.0f, choice + 2)) services++;
            if (choice % 5 == 0 && cats == 0 && TryStory(root.transform, batches, occupied, blockers, wall,
                "WallCat", new Vector2(.9f, 1f), 1.3f, choice + 3)) cats++;
            if (signs >= 2 && services >= 2 && (choice % 5 != 0 || cats > 0)) break;
        }
        Save(root, batches, building.name + "_WallStories", variant);
        Debug.Log("STACKED_WALL_STORIES " + building.name + " variant=" + variant + " signs=" + signs
            + " service=" + services + " cats=" + cats + " renderers=" + batches.Count);
        if (batches.Count == 0) UnityEngine.Object.DestroyImmediate(root);
        return district;
    }

    /// <summary>Signs and small equipment only, for existing structural portals.</summary>
    public static void DecorateStructure(GameObject structure, int variant)
    {
        if (Models.Count == 0) Prepare();
        var previous = structure.transform.Find("StackedWallStories");
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var walls = FindWalls(structure).Where(w => w.Width >= 1.3f && w.Height >= 3f)
            .OrderByDescending(w => w.Width * w.Height).Take(10).ToList();
        if (walls.Count > 0) DecorateWallStories(structure, variant, walls, FindGlazing(structure), 2);
    }

    static bool TryStory(Transform root, Dictionary<string, List<CombineInstance>> batches, List<Bounds> occupied, List<Wall> blockers,
        Wall wall, string model, Vector2 size, float preferredHeight, int variant, float scale = 1f)
    {
        size *= scale;
        const float border = .18f;
        if (wall.Width < size.x + border * 2f || wall.Height < size.y + border * 2f) return false;
        float low = wall.minV + size.y * .5f + border;
        float high = wall.maxV - size.y * .5f - border;
        float left = wall.minU + size.x * .5f + border;
        float right = wall.maxU - size.x * .5f - border;
        // Test a bounded selection of positions; never bridge holes or paste over glazing.
        float[] fractions = variant % 2 == 0 ? new[] { .18f, .82f, .5f, 0f, 1f } : new[] { .82f, .18f, .5f, 1f, 0f };
        float[] heights = { Mathf.Clamp(preferredHeight, low, high), Mathf.Clamp(9f, low, high), low,
            Mathf.Clamp(15f, low, high), (low + high) * .5f, high };
        foreach (float v in heights)
        foreach (float fraction in fractions)
        {
            float u = Mathf.Lerp(left, right, fraction);
            // Road chunks are 20 m apart. Broad podiums overlap at their ends,
            // so keep their lettering in the exposed middle of the chunk.
            Vector3 mount = wall.Point(u, v);
            if (root.parent.name.Contains("GroundedTerrace") && Mathf.Abs(mount.z) > 5f) continue;
            if (!wall.Supports(u, v, size.x + .08f, size.y + .08f)
                || Occluded(wall, occupied, u, v, size.x + .28f, size.y + .28f)
                || !Exposed(wall, blockers, u, v, size)) continue;
            Vector3 position = wall.Point(u, v, .016f);
            Quaternion rotation = Quaternion.LookRotation(wall.normal, Vector3.up);
            AddModule(batches, root, model, position, rotation, new Vector3(scale, scale, 1f), true);
            occupied.Add(new Bounds(wall.Point(u, v, .15f), Abs(wall.right) * (size.x + .28f)
                + Vector3.up * (size.y + .28f) + Abs(wall.normal) * .3f));
            // Inspection anchors are stripped from player builds; no per-sign scripts or fonts.
            var anchor = new GameObject("Story_" + model);
            anchor.tag = "EditorOnly";
            anchor.transform.SetPositionAndRotation(position, rotation);
            anchor.transform.localScale = new Vector3(scale, scale, 1f);
            anchor.transform.SetParent(root, true);
            Debug.Log("STACKED_WALL_PLACED " + root.parent.name + " " + model + " at=" + position + " normal=" + wall.normal + " scale=" + scale);
            return true;
        }
        return false;
    }

    static bool Exposed(Wall wall, List<Wall> blockers, float u, float v, Vector2 size)
    {
        // A supported plane can still be an internal face behind a pier or
        // cladding return. Check outward segments across the entire footprint,
        // including the few centimetres immediately in front of the mounting face.
        int nx = Mathf.CeilToInt(size.x / .55f), ny = Mathf.CeilToInt(size.y / .55f);
        for (int x = 0; x <= nx; x++)
        for (int y = 0; y <= ny; y++)
        {
            Vector3 point = wall.Point(u - size.x * .5f + size.x * x / nx, v - size.y * .5f + size.y * y / ny);
            foreach (var blocker in blockers)
            {
                float denominator = Vector3.Dot(wall.normal, blocker.normal);
                if (Mathf.Abs(denominator) < .001f) continue;
                float distance = (blocker.plane - Vector3.Dot(point, blocker.normal)) / denominator;
                if (distance <= .012f || distance > 3f) continue;
                Vector3 hit = point + wall.normal * distance;
                float hitU = Vector3.Dot(hit, blocker.right);
                if (hitU < blocker.minU || hitU > blocker.maxU || hit.y < blocker.minV || hit.y > blocker.maxV) continue;
                if (blocker.Contains(hitU, hit.y)) return false;
            }
        }
        return true;
    }

    static List<Wall> FindWalls(GameObject building, bool claddingOnly = true)
    {
        var walls = new Dictionary<string, Wall>();
        foreach (var filter in building.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = filter.GetComponent<Renderer>();
            if (renderer == null || filter.sharedMesh == null) continue;
            string name = filter.name.ToLowerInvariant();
            if (claddingOnly && (name.Contains("facade") || name.Contains("window") || name.Contains("detail"))) continue;
            var geometry = ReadGeometry(filter.sharedMesh);
            var vertices = geometry.vertices;
            var matrix = filter.transform.localToWorldMatrix;
            var normalMatrix = matrix.inverse.transpose;
            for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
            {
                string mat = MaterialName(renderer, sub);
                // SideGallery's occupied body uses BlueStone; its Mineral mesh is
                // only a thin roof cap. Both are authored cladding surfaces.
                if (claddingOnly && !(mat.Contains("pale") || mat.Contains("mineral") || mat.Contains("concrete")
                    || mat.Contains("white") || mat.Contains("bluestone"))) continue;
                int[] triangles = geometry.triangles[sub];
                for (int index = 0; index < triangles.Length; index += 3)
                {
                    Vector3 a = matrix.MultiplyPoint3x4(vertices[triangles[index]]);
                    Vector3 b = matrix.MultiplyPoint3x4(vertices[triangles[index + 1]]);
                    Vector3 c = matrix.MultiplyPoint3x4(vertices[triangles[index + 2]]);
                    Vector3 cross = Vector3.Cross(b - a, c - a);
                    if (cross.sqrMagnitude < .001f) continue;
                    Vector3 normal = cross.normalized;
                    // Imported axis conversions or a mirrored placement can reverse the
                    // transformed winding. Follow the authored outward normals in that case.
                    if (geometry.normals.Length == vertices.Length)
                    {
                        Vector3 authoredNormal = normalMatrix.MultiplyVector(geometry.normals[triangles[index]]
                            + geometry.normals[triangles[index + 1]] + geometry.normals[triangles[index + 2]]);
                        if (Vector3.Dot(normal, authoredNormal) < 0f) normal = -normal;
                    }
                    if (Mathf.Abs(normal.y) > .01f) continue;
                    float plane = Vector3.Dot(a, normal);
                    string key = Mathf.RoundToInt(normal.x * 1000) + "_" + Mathf.RoundToInt(normal.z * 1000) + "_" + Mathf.RoundToInt(plane * 100);
                    if (!walls.TryGetValue(key, out Wall wall)) walls[key] = wall = new Wall { normal = normal, right = Vector3.Cross(Vector3.up, normal), plane = plane };
                    wall.triangles.Add(new Triangle { a = a, b = b, c = c });
                    foreach (Vector3 point in new[] { a, b, c })
                    {
                        float u = Vector3.Dot(point, wall.right);
                        wall.minU = Mathf.Min(wall.minU, u); wall.maxU = Mathf.Max(wall.maxU, u);
                        wall.minV = Mathf.Min(wall.minV, point.y); wall.maxV = Mathf.Max(wall.maxV, point.y);
                    }
                }
            }
        }
        return walls.Values.ToList();
    }

    static List<Bounds> FindGlazing(GameObject building)
    {
        var result = new List<Bounds>();
        foreach (var filter in building.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = filter.GetComponent<Renderer>();
            if (renderer == null || filter.sharedMesh == null) continue;
            string name = filter.name.ToLowerInvariant();
            var glassSubmeshes = Enumerable.Range(0, filter.sharedMesh.subMeshCount)
                .Where(sub => MaterialName(renderer, sub).Contains("glass") || MaterialName(renderer, sub).Contains("warm")).ToArray();
            if (glassSubmeshes.Length == 0 || name.Contains("sill") || name.Contains("frame")) continue;
            // FBX and baked panes use welded positions with split normals. Weld by position,
            // then union triangles so an entire facade is not treated as one solid pane.
            var geometry = ReadGeometry(filter.sharedMesh);
            var vertices = geometry.vertices;
            var triangles = glassSubmeshes.SelectMany(sub => geometry.triangles[sub]).ToArray();
            var used = new HashSet<int>(triangles);
            var parents = new int[vertices.Length];
            var welded = new Dictionary<Vector3, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                parents[i] = i;
                if (welded.TryGetValue(vertices[i], out int other)) parents[i] = other;
                else welded[vertices[i]] = i;
            }
            Func<int, int> find = null;
            find = i => parents[i] == i ? i : parents[i] = find(parents[i]);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                parents[find(triangles[i + 1])] = find(triangles[i]);
                parents[find(triangles[i + 2])] = find(triangles[i]);
            }
            var bounds = new Dictionary<int, Bounds>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!used.Contains(i)) continue;
                int key = find(i); var point = filter.transform.TransformPoint(vertices[i]);
                if (!bounds.TryGetValue(key, out Bounds value)) value = new Bounds(point, Vector3.zero);
                value.Encapsulate(point); bounds[key] = value;
            }
            result.AddRange(bounds.Values);
        }
        return result;
    }

    static string MaterialName(Renderer renderer, int submesh)
    {
        var materials = renderer.sharedMaterials;
        if (materials.Length == 0) return string.Empty;
        var material = materials[Mathf.Min(submesh, materials.Length - 1)];
        return material == null ? string.Empty : material.name.ToLowerInvariant();
    }

    static Geometry ReadGeometry(Mesh mesh)
    {
        if (GeometryCache.TryGetValue(mesh, out Geometry geometry)) return geometry;
        // The original CityV7 FBXs deliberately disable Read/Write. The editor
        // snapshot API reads those assets without retaining CPU mesh data in players.
        using (var snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh))
        {
            var data = snapshot[0];
            geometry = new Geometry { triangles = new int[data.subMeshCount][], normals = Array.Empty<Vector3>() };
            using (var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
            {
                data.GetVertices(vertices);
                geometry.vertices = vertices.ToArray();
                if (data.HasVertexAttribute(VertexAttribute.Normal))
                {
                    data.GetNormals(vertices);
                    geometry.normals = vertices.ToArray();
                }
            }
            for (int sub = 0; sub < data.subMeshCount; sub++)
            {
                var descriptor = data.GetSubMesh(sub);
                if (descriptor.topology != MeshTopology.Triangles)
                {
                    geometry.triangles[sub] = Array.Empty<int>();
                    continue;
                }
                using (var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp))
                {
                    data.GetIndices(indices, sub, true);
                    geometry.triangles[sub] = indices.ToArray();
                }
            }
        }
        GeometryCache.Add(mesh, geometry);
        return geometry;
    }

    static bool Occluded(Wall wall, List<Bounds> glazing, float u, float v, float width, float height)
    {
        foreach (var b in glazing)
        {
            float depth = Vector3.Dot(b.center, wall.normal) - wall.plane;
            float radius = Vector3.Dot(b.extents, Abs(wall.normal));
            if (depth + radius < -.12f || depth - radius > .4f) continue;
            float paneU = Vector3.Dot(b.center, wall.right);
            float paneRadius = Vector3.Dot(b.extents, Abs(wall.right));
            if (Mathf.Abs(paneU - u) < paneRadius + width * .5f && Mathf.Abs(b.center.y - v) < b.extents.y + height * .5f) return true;
        }
        return false;
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    static void AddModule(Dictionary<string, List<CombineInstance>> batches, Transform root, string model, Vector3 position, Quaternion rotation, Vector3 scale, bool enamel = false)
    {
        Matrix4x4 placement = root.worldToLocalMatrix * Matrix4x4.TRS(position, rotation, scale);
        foreach (Part part in Models[model])
            Add(batches, enamel && part.material == "SC_Concrete" ? "SC_White" : part.material,
                new CombineInstance { mesh = part.mesh, transform = placement * part.matrix });
    }
    static void AddBox(Dictionary<string, List<CombineInstance>> batches, Transform root, Vector3 position, Quaternion rotation, Vector3 scale, string material)
        => Add(batches, material, new CombineInstance { mesh = cube, transform = root.worldToLocalMatrix * Matrix4x4.TRS(position, rotation, scale) });
    static void Add(Dictionary<string, List<CombineInstance>> batches, string material, CombineInstance instance)
    {
        if (!batches.TryGetValue(material, out var batch)) batches[material] = batch = new List<CombineInstance>();
        batch.Add(instance);
    }
    static void Save(GameObject root, Dictionary<string, List<CombineInstance>> batches, string building, int variant)
    {
        foreach (var batch in batches.OrderBy(b => b.Key))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/" + batch.Key + ".mat");
            if (material == null) throw new InvalidOperationException("Prepare architecture material first: " + batch.Key);
            string safeName = string.Concat(building.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));
            var combined = new Mesh { name = safeName + "_" + variant + "_" + batch.Key, indexFormat = IndexFormat.UInt32 };
            combined.CombineMeshes(batch.Value.ToArray(), true, true);
            combined.RecalculateBounds();
            string path = Art + "ArchitectureMeshes/" + combined.name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(combined, path); saved = combined; }
            else { EditorUtility.CopySerialized(combined, saved); UnityEngine.Object.DestroyImmediate(combined); }
            EditorUtility.SetDirty(saved);
            saved.UploadMeshData(false);
            var child = new GameObject(batch.Key, typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(root.transform, false);
            child.GetComponent<MeshFilter>().sharedMesh = saved;
            var renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }
}
