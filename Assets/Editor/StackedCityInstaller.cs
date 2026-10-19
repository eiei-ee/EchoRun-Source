using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Bakes authored city modules into a reusable, collider-free environment kit.
// Runtime only places these prefabs and moves the train; it builds no geometry.
public static class StackedCityInstaller
{
    const string Art = "Assets/Art/StackedCity/";
    const string ResourcesPath = "Assets/Resources/CityV7/";
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static Mesh cube;
    static int architectureVariant;

    public static void InspectBaseline()
    {
        Directory.CreateDirectory("TestResults/CityLayers");
        CityLayerUpgrade.Capture("stacked-baseline");
        var report = new List<string>();
        for (int i = 0; i < 9; i++)
        {
            var root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("CityV7/Chunk" + i));
            foreach (Transform child in root.transform)
            {
                var rs = child.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0) report.Add(i + " " + child.name + " " + BoundsOf(child.gameObject));
            }
            UnityEngine.Object.DestroyImmediate(root);
        }
        Directory.CreateDirectory("TestResults/StackedCity");
        File.WriteAllLines("TestResults/StackedCity/baseline-bounds.txt", report);
        Debug.Log("STACKED_CITY_BASELINE_OK");
    }

    [MenuItem("Tools/EchoRun/Art/Install Stacked City")]
    public static void Install()
    {
        Directory.CreateDirectory(Art + "Materials");
        Directory.CreateDirectory(Art + "Meshes");
        Directory.CreateDirectory(Art + "SourceChunks");
        AssetDatabase.Refresh();
        var temporary = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube = temporary.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(temporary);
        PrepareMaterials();
        StackedCityMaterials.Configure();
        StackedCityArchitecture.Prepare();
        architectureVariant = 1000;
        foreach (string model in new[] { "SkyTrainCar", "SkyPlanter", "SkyViaduct", "SkyTerrace", "SkyStair" })
        {
            string path = Art + "Models/" + model + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing authored model: " + path);
            importer.isReadable = true;
            importer.addCollider = false;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }
        for (int i = 0; i < 4; i++) BuildDistrict(i);
        BuildUpperRail();
        DressTracksideTerraces();
        DressTurnFacilities();
        LowerHorizon();
        var sky = AssetDatabase.LoadAssetAtPath<Material>(ResourcesPath + "ExperienceSky.mat");
        sky.SetFloat("_Exposure", .72f);
        EditorUtility.SetDirty(sky);
        StackedCityMaterials.BakeSkyReflection();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("STACKED_CITY_INSTALLED");
    }

    public static void InstallAndCapture()
    {
        Install();
        StackedCityReview.Capture();
    }

    public static void InstallPolishAndCapture()
    {
        Install();
        StackedCityReview.PolishCaptureAfter();
    }

    public static void InstallPolishAndBuild()
    {
        Install();
        StackedCityReview.PolishCaptureAndBuild();
    }

    public static void InstallWallDetailsAndCapture()
    {
        Install();
        StackedCityReview.WallCaptureAfter();
    }

    public static void InstallWallDetailsAndBuild()
    {
        Install();
        StackedCityReview.WallCaptureAndBuild();
    }

    static Material Mat(string name, Color color, float metallic = 0f, float smoothness = .25f)
    {
        string path = Art + "Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        material.enableInstancing = true;
        materials[name] = material;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void PrepareMaterials()
    {
        materials.Clear();
        Mat("SC_Concrete", new Color(.76f, .77f, .72f));
        Mat("SC_Glass", new Color(.075f, .18f, .205f), .45f, .7f);
        Mat("SC_Metal", new Color(.19f, .25f, .27f), .55f, .35f);
        Mat("SC_White", new Color(.88f, .89f, .84f), .2f, .5f);
        Mat("SC_Rubber", new Color(.045f, .055f, .06f), 0f, .15f);
        Mat("SC_Foliage", new Color(.16f, .28f, .145f), 0f, .15f);
        Mat("SC_Bark", new Color(.25f, .21f, .155f));
        Mat("SC_Street", new Color(.15f, .20f, .21f), .04f, .3f);
        Mat("SC_Marking", new Color(.69f, .66f, .54f));
        var cyan = Mat("SC_Cyan", new Color(.12f, .63f, .71f), .25f, .4f);
        cyan.EnableKeyword("_EMISSION");
        cyan.SetColor("_EmissionColor", new Color(.06f, .28f, .32f));
        var warm = Mat("SC_Warm", new Color(.91f, .64f, .32f), .1f, .4f);
        warm.EnableKeyword("_EMISSION");
        warm.SetColor("_EmissionColor", new Color(.36f, .19f, .055f));
    }

    static GameObject Model(string name, Transform parent, Vector3 position, float yaw = 0f)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Models/" + name + ".fbx");
        if (source == null) throw new InvalidOperationException("Missing model " + name);
        // The placement root is Y-up/+Z-forward. Keep the FBX's own axis
        // conversion on its child, including while the train changes heading.
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        UnityEngine.Object.Instantiate(source, go.transform, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(sourceMaterial =>
            {
                string key = sourceMaterial != null ? sourceMaterial.name.Split('.')[0] : "SC_Concrete";
                return materials.TryGetValue(key, out var material) ? material : materials["SC_Concrete"];
            }).ToArray();
        }
        return go;
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, string material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = cube;
        go.AddComponent<MeshRenderer>().sharedMaterial = materials[material];
        return go;
    }

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static void Building(Transform parent, string name, Vector3 baseCenter, Vector3 size, float yaw)
    {
        GameObject source = null;
        // Reuse the production facade/cleaned mesh binding, not the raw FBX
        // whose overlapping surfaces were already repaired in CityV7.
        for (int i = 0; i < 9 && source == null; i++)
        {
            var chunk = Resources.Load<GameObject>("CityV7/Chunk" + i);
            var building = chunk.transform.Find(name.Replace("CityV7_", "V7_"));
            if (building != null) source = building.gameObject;
        }
        if (source == null) throw new InvalidOperationException("Missing production building " + name);
        var go = UnityEngine.Object.Instantiate(source, parent, false);
        go.name = name;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.localRotation;
        StackedCityMaterials.RestoreNearFacadeMaterials(go);
        StackedCityArchitecture.DecorateBuilding(go, architectureVariant++);
        var bounds = BoundsOf(go);
        // Normalize around the imported bounds, preserving the FBX up-axis conversion.
        float scale = Mathf.Min(size.x / bounds.size.x, size.z / bounds.size.z, size.y / bounds.size.y);
        go.transform.localScale *= scale;
        bounds = BoundsOf(go);
        go.transform.position += baseCenter - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        foreach (var collider in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(mat =>
            {
                return mat != null ? mat : materials["SC_Concrete"];
            }).ToArray();
        }
    }

    static void Street(Transform parent, Vector3 center, float length, bool alongZ)
    {
        Vector3 size = alongZ ? new Vector3(11f, .65f, length) : new Vector3(length, .65f, 11f);
        Box(parent, "StreetDeck", center - Vector3.up * .4f, size + Vector3.up * .35f, "SC_Concrete");
        Box(parent, "StreetSurface", center + Vector3.up * .06f,
            alongZ ? new Vector3(9f, .1f, length - .08f) : new Vector3(length - .08f, .1f, 9f), "SC_Street");
        for (float t = -length * .5f + 4; t < length * .5f; t += 8)
            Box(parent, "StreetMark", center + (alongZ ? Vector3.forward : Vector3.right) * t + Vector3.up * .12f,
                alongZ ? new Vector3(.16f, .035f, 3f) : new Vector3(3f, .035f, .16f), "SC_Marking");
        foreach (float edge in new[] { -5f, 5f })
            Box(parent, "StreetParapet", center + (alongZ ? Vector3.right : Vector3.forward) * edge + Vector3.up * .42f,
                alongZ ? new Vector3(.28f, .75f, length) : new Vector3(length, .75f, .28f), "SC_Concrete");
    }

    static void BuildDistrict(int variant)
    {
        var root = new GameObject("StackedBlock" + variant);
        // Open roads and separate podiums leave vertical views through every level.
        // There is deliberately no full-width ground plane at the first lower level.
        Street(root.transform, new Vector3(0f, -39f, 0f), 96f, false);
        Street(root.transform, new Vector3(0f, -39.05f, 0f), 96f, true);
        Street(root.transform, new Vector3(0f, -22f, 0f), 96f, false);
        Street(root.transform, new Vector3(0f, -22.05f, 0f), 96f, true);
        string[] models = { "CityV7_CivicPodium", "CityV7_SteppedTower", "CityV7_SideGallery", "CityV7_SkylineMid" };
        for (int x = 0; x < 2; x++)
        for (int z = 0; z < 2; z++)
        {
            int index = x + z * 2;
            Vector3 center = new Vector3(x == 0 ? -24f : 24f, 0f, z == 0 ? -23f : 23f);
            float top = -19f + z * 6f;
            // A inhabited podium carries its roof plaza. Window bands establish
            // human scale underneath the street that initially reads as ground.
            Box(root.transform, "OccupiedPodium", center + Vector3.up * (top - 14f), new Vector3(25f, 27f, 22f), "SC_Concrete");
            for (int floor = 0; floor < 6; floor++)
            {
                float y = top - 3.5f - floor * 3.7f;
                foreach (int side in new[] { -1, 1 })
                {
                    Box(root.transform, "PodiumWindowBand", center + new Vector3(side * 12.53f, y, 0f), new Vector3(.065f, 1.65f, 19f), "SC_Glass");
                    Box(root.transform, "PodiumWindowBand", center + new Vector3(0f, y, side * 11.03f), new Vector3(22f, 1.65f, .065f), "SC_Glass");
                }
            }
            var terrace = Model("SkyTerrace", root.transform, center + Vector3.up * top, (variant % 2) * 180f);
            terrace.transform.localScale = new Vector3(1.6f, 1f, 2f);
            foreach (float side in new[] { -1f, 1f })
                Model("SkyPlanter", root.transform, center + new Vector3(side * 8f, top + .03f, -8f), side * 90f);
            // Smaller formal buildings sit on the lowest streets and show
            // another roof level through the openings between these podiums.
            Building(root.transform, models[(index + variant) % models.Length],
                new Vector3(center.x + (x == 0 ? -12f : 12f), -59f, center.z + (z == 0 ? -12f : 12f)),
                new Vector3(17f, 27f, 17f), ((index + variant) % 4) * 90f);
        }
        // Stair endpoints at z=-4.5/+4.5 connect to the plaza edges z=-11/+11.
        foreach (float x in new[] { -24f, 24f })
        {
            Model("SkyStair", root.transform, new Vector3(x, -19f, 0f), 0f);
            Box(root.transform, "LowerStairLanding", new Vector3(x, -19.55f, -7.75f), new Vector3(4f, 1.1f, 6.5f), "SC_Concrete");
            Box(root.transform, "UpperStairLanding", new Vector3(x, -13.55f, 7.75f), new Vector3(4f, 1.1f, 6.5f), "SC_Concrete");
        }
        // The closest below-track boulevard creates the second apparent ground.
        Street(root.transform, new Vector3(42f, -8f, 0f), 96f, true);
        foreach (float z in new[] { -32f, 0f, 32f })
        {
            Box(root.transform, "BoulevardCrosshead", new Vector3(42f, -10f, z), new Vector3(10f, 1.1f, 2f), "SC_Concrete");
            foreach (float x in new[] { 38f, 46f })
                Box(root.transform, "BoulevardPier", new Vector3(x, -30f, z), new Vector3(1.5f, 39f, 1.8f), "SC_Concrete");
        }
        Bake(root, root.name, true);
        PrefabUtility.SaveAsPrefabAsset(root, ResourcesPath + root.name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
    }

    public static Vector3[] RailPath()
    {
        var points = new List<Vector3>();
        Vector2[] centers = { new Vector2(54, 30), new Vector2(-54, 30), new Vector2(-54, -30), new Vector2(54, -30) };
        for (int corner = 0; corner < 4; corner++)
        for (int step = 0; step <= 8; step++)
        {
            float a = (corner * 90f + step * 90f / 8f) * Mathf.Deg2Rad;
            points.Add(new Vector3(centers[corner].x + Mathf.Cos(a) * 18f, 13.16f,
                centers[corner].y + Mathf.Sin(a) * 18f));
        }
        return points.ToArray();
    }

    static void BuildUpperRail()
    {
        var root = new GameObject("UpperTransit");
        var structure = new GameObject("StaticRoot");
        structure.transform.SetParent(root.transform, false);
        var path = RailPath();
        // The deck is baked along the same rounded centerline as the train.
        // Segment overlap closes the curved seams; all track parts share mats.
        for (int i = 0; i < path.Length; i++)
        {
            var a = path[i];
            var b = path[(i + 1) % path.Length];
            float length = Vector3.Distance(a, b);
            var center = (a + b) * .5f - Vector3.up * .16f;
            var tangent = (b - a).normalized;
            var right = Vector3.Cross(Vector3.up, tangent);
            Quaternion rotation = Quaternion.LookRotation(tangent);
            if (length > 20f)
            {
                int count = Mathf.CeilToInt(length / 24f);
                for (int part = 0; part < count; part++)
                {
                    Vector3 position = Vector3.Lerp(a, b, (part + .5f) / count) - Vector3.up * .16f;
                    var span = Model("SkyViaduct", structure.transform, position);
                    span.transform.localRotation = rotation;
                    span.transform.localScale = new Vector3(1f, 1f, length / count / 24f);
                }
                continue;
            }
            var deck = Box(structure.transform, "CurveDeck", center - Vector3.up * .7f,
                new Vector3(7f, 1.4f, length + .7f), "SC_Concrete");
            deck.transform.localRotation = rotation;
            foreach (float offset in new[] { -.85f, .85f, -3.25f, 3.25f })
            {
                bool edge = Mathf.Abs(offset) > 3f;
                var beam = Box(structure.transform, edge ? "CurveParapet" : "CurveRail",
                    center + right * offset + Vector3.up * (edge ? .5f : .09f),
                    new Vector3(edge ? .3f : .1f, edge ? 1f : .16f, length + .45f), edge ? "SC_Concrete" : "SC_Metal");
                beam.transform.localRotation = rotation;
            }
        }
        Bake(structure, "UpperRail", true);
        var motion = new GameObject("LightRail");
        motion.transform.SetParent(root.transform, false);
        var loop = motion.AddComponent<CityTransitLoop>();
        loop.pathPoints = path;
        loop.speed = 11f;
        loop.spacing = 14.8f;
        loop.phaseOffset = 0f;
        loop.vehicles = new Transform[3];
        for (int car = 0; car < loop.vehicles.Length; car++)
            loop.vehicles[car] = Model("SkyTrainCar", motion.transform, Vector3.zero).transform;
        var sound = loop.vehicles[1].gameObject.AddComponent<AudioSource>();
        sound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "Audio/SkyRailLoop.wav");
        sound.loop = true;
        sound.playOnAwake = false;
        sound.spatialBlend = 1f;
        sound.rolloffMode = AudioRolloffMode.Linear;
        sound.minDistance = 8f;
        sound.maxDistance = 70f;
        sound.dopplerLevel = .2f;
        loop.vehicles[1].gameObject.AddComponent<CityTransitAudio>().source = sound;
        loop.Sample(0f);
        PrefabUtility.SaveAsPrefabAsset(root, ResourcesPath + "UpperTransit.prefab");
        UnityEngine.Object.DestroyImmediate(root);
    }

    static void DressTracksideTerraces()
    {
        for (int i = 0; i < 9; i++)
        {
            string path = ResourcesPath + "Chunk" + i + ".prefab";
            string sourcePath = Art + "SourceChunks/Chunk" + i + ".prefab";
            if (!File.Exists(sourcePath))
            {
                var original = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        var old = original.transform.Find("StackedRoofGarden_" + side);
                        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    }
                    PrefabUtility.SaveAsPrefabAsset(original, sourcePath);
                }
                finally { PrefabUtility.UnloadPrefabContents(original); }
            }
            var root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                int buildingIndex = 0;
                foreach (Transform building in root.transform)
                {
                    if (building.GetComponentsInChildren<Renderer>().Length == 0) continue;
                    float side = Mathf.Sign(BoundsOf(building.gameObject).center.x);
                    building.localPosition += Vector3.right * side * 12f;
                    StackedCityMaterials.RestoreNearFacadeMaterials(building.gameObject);
                    StackedCityArchitecture.DecorateBuilding(building.gameObject, i * 100 + buildingIndex++);
                }
                GiveWarmWindowsDepth(root, i);
                // Each side is a separate clearance unit; never group both
                // sides over the road or the whole set is culled at a turn.
                foreach (int side in new[] { -1, 1 })
                {
                    string name = "StackedRoofGarden_" + side;
                    var old = root.transform.Find(name);
                    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    if (i % 3 != 0 || side != (i % 2 == 0 ? -1 : 1)) continue;
                    var garden = new GameObject(name);
                    garden.transform.SetParent(root.transform, false);
                    var terrace = Model("SkyTerrace", garden.transform, new Vector3(side * 7.4f, -.2f, 0f), side < 0 ? 180f : 0f);
                    terrace.transform.localScale = new Vector3(.16f, 1f, 1f);
                    foreach (float z in new[] { -3.5f, 3.5f })
                        Model("SkyPlanter", garden.transform, new Vector3(side * 7.4f, -.15f, z), 90f);
                    // Roof-to-street connection reads under the player's deck.
                    Box(garden.transform, "TerraceSupport", new Vector3(side * 7.4f, -8f, 0f), new Vector3(2.7f, 14f, 10f), "SC_Concrete");
                    for (int floor = 0; floor < 3; floor++)
                        Box(garden.transform, "TerraceWindows", new Vector3(side * 6.02f, -3f - floor * 3.6f, 0f), new Vector3(.06f, 1.6f, 8.5f), "SC_Glass");
                    Bake(garden, "RoofGarden" + side, true);
                }
                // Periodic cross streets are attached to the route, so the
                // running camera can actually see the next ground level.
                if (i % 3 == 1)
                {
                    var crossStreet = new GameObject("StackedCrossStreet");
                    crossStreet.transform.SetParent(root.transform, false);
                    Street(crossStreet.transform, new Vector3(0f, -9f, 0f), 96f, false);
                    foreach (float x in new[] { -32f, -16f, 16f, 32f })
                    {
                        Box(crossStreet.transform, "StreetPier", new Vector3(x, -24f, 0f), new Vector3(1.8f, 28f, 2f), "SC_Concrete");
                        Box(crossStreet.transform, "StreetLanding", new Vector3(x, -9.5f, 6f), new Vector3(9f, 1f, 5f), "SC_Concrete");
                        Model("SkyPlanter", crossStreet.transform, new Vector3(x, -9f, 7f));
                    }
                    Bake(crossStreet, "CrossStreet", true);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    static void DressTurnFacilities()
    {
        int variant = 2000;
        foreach (string side in new[] { "Left", "Right" })
        {
            string path = "Assets/Prefabs/TurnSegment_" + side + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var facilities = root.GetComponentsInChildren<Transform>(true).Where(node =>
                    node.name == "OverhangTail" || node.name == "OuterMemorySilo" || node.name == "InnerMechanicalFacility").ToArray();
                foreach (Transform facility in facilities)
                {
                    bool overhang = facility.name == "OverhangTail";
                    if (!overhang && facility.name != "OuterMemorySilo" && facility.name != "InnerMechanicalFacility") continue;
                    foreach (Renderer renderer in facility.GetComponentsInChildren<Renderer>(true))
                    {
                        var slots = renderer.sharedMaterials;
                        for (int i = 0; i < slots.Length; i++)
                        {
                            if (slots[i] == null) continue;
                            string name = slots[i].name;
                            if (name.Contains("Ceramic") || name.Contains("Concrete")) slots[i] = materials["SC_Concrete"];
                            else if (name.Contains("Metal")) slots[i] = materials["SC_Metal"];
                        }
                        renderer.sharedMaterials = slots;
                    }
                    // These retained turn landmarks sit beside the roadway;
                    // only their visible surfaces receive the city facade kit.
                    if (overhang) StackedCityArchitecture.DecorateStructure(facility.gameObject, variant++);
                    else StackedCityArchitecture.DecorateBuilding(facility.gameObject, variant++);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    static void LowerHorizon()
    {
        string path = ResourcesPath + "ExperienceSkyline.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var ground = root.transform.Find("LowerCityGround");
            if (ground != null)
            {
                var p = ground.localPosition;
                p.y = -66f;
                ground.localPosition = p;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void GiveWarmWindowsDepth(GameObject root, int variant)
    {
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if (!filter.name.Contains("FacadeWarm")) continue;
            // A tiny authored reveal separates lit glazing from the original
            // glass face, including on angled landmark facades after placement.
            var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var normalMatrix = filter.transform.localToWorldMatrix.inverse.transpose;
            for (int v = 0; v < vertices.Length; v++)
            {
                var worldNormal = normalMatrix.MultiplyVector(normals[v]).normalized;
                vertices[v] += filter.transform.worldToLocalMatrix.MultiplyVector(worldNormal * .015f);
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            string path = Art + "Meshes/WarmReveal_" + variant + "_" + filter.name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); }
            filter.sharedMesh = saved;
        }
    }

    static void Bake(GameObject root, string prefix, bool shadows)
    {
        var batches = new Dictionary<Material, List<CombineInstance>>();
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            var renderer = filter.GetComponent<Renderer>();
            if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
            for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
            {
                var material = renderer.sharedMaterials[Mathf.Min(sub, renderer.sharedMaterials.Length - 1)];
                if (!batches.TryGetValue(material, out var list)) batches[material] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub,
                    transform = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            }
        }
        var output = new List<(Mesh, Material)>();
        foreach (var batch in batches.OrderBy(b => b.Key.name))
        {
            var mesh = new Mesh { name = prefix + "_" + batch.Key.name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(batch.Value.ToArray(), true, true);
            mesh.RecalculateBounds();
            string path = Art + "Meshes/" + mesh.name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); }
            output.Add((saved, batch.Key));
        }
        while (root.transform.childCount > 0) UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
        foreach (var item in output)
        {
            var go = new GameObject(item.Item2.name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = item.Item1;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = item.Item2;
            renderer.shadowCastingMode = shadows && !item.Item2.name.Contains("Foliage")
                ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }
    }
}
