using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class InstallExperienceSlice
{
    const string Art = "Assets/Art/ExperienceSlice/";
    const string City = "Assets/CityAfterimageV7/Materials/";

    [MenuItem("Tools/EchoRun/Art/Install Experience Slice V1")]
    public static void Install()
    {
        ConfigureTexture(Art + "BlueHourSky.png", 2048, false);
        ConfigureTexture(Art + "MineralPanels.png", 1024, true);
        var sky = MaterialAsset("Assets/Resources/CityV7/ExperienceSky.mat",
            Shader.Find(WorldStyler.SeamlessSkyShaderName));
        sky.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "BlueHourSky.png"));
        sky.SetColor("_Tint", new Color(.5f, .5f, .5f));
        sky.SetFloat("_Exposure", .48f);
        sky.SetFloat("_Saturation", .82f);
        sky.SetFloat("_HorizonTexY", .5f);
        sky.SetFloat("_SeamBlend", .025f);
        sky.SetFloat("_Rotation", 35f);
        EditorUtility.SetDirty(sky);

        Cladding("CityV7_Pale", new Color(.9f, .93f, .96f), .18f);
        Cladding("CityV7_Mineral", new Color(.72f, .78f, .84f), .16f);
        Standard("CityV7_BlueStone", new Color(.22f, .31f, .40f), .12f, .32f);
        Standard("CityV7_Structure", new Color(.10f, .15f, .21f), .55f, .42f);
        Standard("CityV7_Glass", new Color(.11f, .23f, .32f), .60f, .78f);
        Standard("CityV7_Scale", new Color(.45f, .49f, .51f), .35f, .35f);
        var frame = MaterialAsset(Art + "WindowFrame.mat", Shader.Find("Standard"));
        frame.color = new Color(.13f, .20f, .26f);
        frame.SetFloat("_Metallic", .65f);
        frame.SetFloat("_Glossiness", .42f);
        EditorUtility.SetDirty(frame);
        var warm = MaterialAsset(Art + "WarmWindow.mat", Shader.Find("Standard"));
        warm.color = new Color(.63f, .48f, .30f);
        warm.EnableKeyword("_EMISSION");
        warm.SetColor("_EmissionColor", new Color(.28f, .16f, .065f));
        EditorUtility.SetDirty(warm);

        for (int i = 0; i < 9; i++)
        {
            string path = "Assets/Resources/CityV7/Chunk" + i + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Transform child in root.transform)
                {
                    if (child.name != "V7_GroundedTerrace") continue;
                    // Preserve the old top elevation, but connect the paper-
                    // thin terrace to the lower city with a real plinth.
                    Vector3 scale = child.localScale;
                    scale.y = 12.2f;
                    child.localScale = scale;
                    Vector3 p = child.localPosition;
                    p.y = .05f - scale.y * .5f;
                    child.localPosition = p;
                }
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter == null) continue;
                    if (filter.name.EndsWith("__CityV7_Mineral"))
                    {
                        Transform previous = filter.transform.Find("AuthoredFacadeWindows");
                        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                        var facade = new GameObject("AuthoredFacadeWindows");
                        facade.transform.SetParent(filter.transform, false);
                        BakeFacadeWindows(filter, facade.transform, frame, warm);
                    }
                    if (!filter.name.EndsWith("__CityV7_Glass")) continue;
                    Transform old = filter.transform.Find("AuthoredWindowDetail");
                    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    var detail = new GameObject("AuthoredWindowDetail");
                    detail.transform.SetParent(filter.transform, false);
                    BakeWindows(filter, detail.transform, frame, warm);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        var road = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/EchoRoad.mat");
        road.SetColor("_Color", new Color(.13f, .14f, .155f));
        road.SetFloat("_NormalStrength", .28f);
        EditorUtility.SetDirty(road);
        BakeSkyline();
        BakeDeckSupports();
        EchoHudPrefabBuilder.Build();
        AssetDatabase.SaveAssets();
        Debug.Log("EXPERIENCE_SLICE_INSTALLED");
    }

    static void ConfigureTexture(string path, int size, bool repeat)
    {
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = size;
        importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = repeat ? 4 : 1;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    static Material MaterialAsset(string path, Shader shader)
    {
        if (shader == null) throw new InvalidOperationException("Shader missing for " + path);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.enableInstancing = true;
        return material;
    }

    static void Cladding(string name, Color tint, float scale)
    {
        var material = MaterialAsset(City + name + ".mat", Shader.Find("EchoRun/MineralCladding"));
        material.color = tint;
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "MineralPanels.png"));
        material.SetFloat("_PanelScale", scale);
        material.SetFloat("_Glossiness", .24f);
        material.SetFloat("_Metallic", .04f);
        EditorUtility.SetDirty(material);
    }

    static void Standard(string name, Color tint, float metal, float smooth)
    {
        var material = MaterialAsset(City + name + ".mat", Shader.Find("Standard"));
        material.color = tint;
        material.SetFloat("_Metallic", metal);
        material.SetFloat("_Glossiness", smooth);
        EditorUtility.SetDirty(material);
    }

    // Extract the separate glass volumes from the authored mesh. Welding
    // coincident face vertices avoids treating each cube face as a window.
    static List<Bounds> GlassVolumes(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int[] parents = new int[vertices.Length];
        var positions = new Dictionary<Vector3, int>();
        for (int i = 0; i < vertices.Length; i++)
        {
            parents[i] = i;
            if (positions.TryGetValue(vertices[i], out int other)) parents[i] = other;
            else positions.Add(vertices[i], i);
        }
        Func<int, int> find = null;
        find = index => parents[index] == index ? index : parents[index] = find(parents[index]);
        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            parents[find(triangles[i + 1])] = find(triangles[i]);
            parents[find(triangles[i + 2])] = find(triangles[i]);
        }
        var bounds = new Dictionary<int, Bounds>();
        for (int i = 0; i < vertices.Length; i++)
        {
            int key = find(i);
            if (!bounds.TryGetValue(key, out Bounds b)) b = new Bounds(vertices[i], Vector3.zero);
            b.Encapsulate(vertices[i]);
            bounds[key] = b;
        }
        return new List<Bounds>(bounds.Values);
    }

    static void BakeWindows(MeshFilter source, Transform root, Material frame, Material warm)
    {
        var frames = new List<CombineInstance>();
        var lights = new List<CombineInstance>();
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
        try
        {
            int window = 0;
            foreach (Bounds b in GlassVolumes(source.sharedMesh))
            {
                Vector3 size = b.size;
                int normal = size.x < size.y ? (size.x < size.z ? 0 : 2) : (size.y < size.z ? 1 : 2);
                int u = (normal + 1) % 3, v = (normal + 2) % 3;
                if (size[u] < .5f || size[v] < .5f) continue;
                int columns = Mathf.Clamp(Mathf.CeilToInt(size[u] / 1.6f), 1, 16);
                int rows = Mathf.Clamp(Mathf.CeilToInt(size[v] / 2.2f), 1, 16);
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 center = b.center;
                    center[normal] += side * (b.extents[normal] + .012f);
                    for (int col = 0; col <= columns; col++)
                    {
                        Vector3 p = center, s = Vector3.one * .055f;
                        p[u] = Mathf.Lerp(b.min[u], b.max[u], (float)col / columns);
                        s[v] = size[v]; s[normal] = .035f;
                        frames.Add(Box(cubeMesh, p, s));
                    }
                    for (int row = 0; row <= rows; row++)
                    {
                        Vector3 p = center, s = Vector3.one * .055f;
                        p[v] = Mathf.Lerp(b.min[v], b.max[v], (float)row / rows);
                        s[u] = size[u]; s[normal] = .035f;
                        frames.Add(Box(cubeMesh, p, s));
                    }
                    if (window % 3 == 0 && columns > 1)
                    {
                        Vector3 p = center, s = Vector3.one * .012f;
                        p[u] = b.min[u] + size[u] * .5f / columns;
                        p[v] = b.min[v] + size[v] * .5f / rows;
                        s[u] = size[u] / columns - .09f; s[v] = size[v] / rows - .09f;
                        lights.Add(Box(cubeMesh, p, s));
                    }
                }
                window++;
            }
            SaveCombined(source.name + "_Frames", root, frames, frame);
            SaveCombined(source.name + "_Warm", root, lights, warm);
        }
        finally { UnityEngine.Object.DestroyImmediate(cube); }
    }

    static CombineInstance Box(Mesh mesh, Vector3 p, Vector3 s)
    { return new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(p, Quaternion.identity, s) }; }

    public static void BakeFacadeWindows(MeshFilter source, Transform root, Material frame, Material warm, bool fitSurface = false)
    {
        // Fit a restrained window bank to the largest solid wall volume in
        // each authored mineral mesh. Other sculptural masses stay quiet.
        var volumes = GlassVolumes(source.sharedMesh);
        volumes.Sort((a, b) => (b.size.x * b.size.y * b.size.z).CompareTo(a.size.x * a.size.y * a.size.z));
        if (volumes.Count == 0) return;
        Bounds bounds = volumes[0];
        int vertical = 0;
        float upAlignment = 0f;
        for (int axis = 0; axis < 3; axis++)
        {
            Vector3 unit = Vector3.zero; unit[axis] = 1;
            float alignment = Mathf.Abs(source.transform.TransformDirection(unit).y);
            if (alignment > upAlignment) { upAlignment = alignment; vertical = axis; }
        }
        Vector3 lossy = source.transform.lossyScale;
        Vector3 unitScale = new Vector3(Mathf.Max(.001f,Mathf.Abs(lossy.x)),Mathf.Max(.001f,Mathf.Abs(lossy.y)),Mathf.Max(.001f,Mathf.Abs(lossy.z)));
        float height = bounds.size[vertical] * Mathf.Abs(lossy[vertical]);
        if (height < 5f) return;
        var panes = new List<CombineInstance>();
        var frames = new List<CombineInstance>();
        var lights = new List<CombineInstance>();
        MeshCollider surface = null;
        if(fitSurface){surface=source.gameObject.AddComponent<MeshCollider>();surface.sharedMesh=source.sharedMesh;Physics.SyncTransforms();}
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var mesh = cube.GetComponent<MeshFilter>().sharedMesh;
        try
        {
            for (int normal = 0; normal < 3; normal++)
            {
                if (normal == vertical) continue;
                int horizontal = 3 - normal - vertical;
                float width = bounds.size[horizontal] * Mathf.Abs(lossy[horizontal]);
                if (width < 3f) continue;
                int cols = Mathf.Clamp(Mathf.FloorToInt(width / 2.6f), 1, 8);
                int rows = Mathf.Clamp(Mathf.FloorToInt(height / 3.1f), 1, 8);
                float cellW = bounds.size[horizontal] * .78f / cols;
                float cellH = bounds.size[vertical] * .78f / rows;
                foreach (int side in new[] { -1, 1 })
                for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                {
                    Vector3 p = bounds.center;
                    p[normal] += side * (bounds.extents[normal] + .018f/(fitSurface?unitScale[normal]:1f));
                    p[horizontal] += (col + .5f - cols * .5f) * cellW;
                    p[vertical] += (row + .5f - rows * .5f) * cellH;
                    Vector3 s = Vector3.one * .018f;
                    s[horizontal] = cellW * .62f;
                    s[vertical] = cellH * .66f;
                    if(fitSurface)
                    {
                        s[normal]/=unitScale[normal];
                        Vector3 direction=Vector3.zero;direction[normal]=side;
                        direction=source.transform.TransformDirection(direction).normalized;
                        bool supported=true;
                        foreach(Vector2 corner in new[]{Vector2.zero,new Vector2(-.48f,-.48f),new Vector2(-.48f,.48f),new Vector2(.48f,-.48f),new Vector2(.48f,.48f)})
                        {
                            Vector3 point=p;point[horizontal]+=corner.x*s[horizontal];point[vertical]+=corner.y*s[vertical];
                            Vector3 world=source.transform.TransformPoint(point);
                            if(!surface.Raycast(new Ray(world+direction*.15f,-direction),out RaycastHit hit,.22f)||Vector3.Dot(hit.normal,direction)<.9f){supported=false;break;}
                        }
                        if(!supported)continue;
                    }
                    bool lit = (row * 7 + col * 3) % 11 == 4;
                    (lit ? lights : panes).Add(Box(mesh, p, s));
                    // Deep sill and narrow mullion give a visible material
                    // edge without filling the facade with emissive stripes.
                    Vector3 sill = p, sillSize = s;
                    sill[vertical] -= s[vertical] * .5f;
                    sillSize[vertical] = .08f; sillSize[normal] = .11f;
                    sillSize[horizontal] += .14f;
                    if(fitSurface){sillSize[vertical]=.08f/unitScale[vertical];sillSize[normal]=.11f/unitScale[normal];sillSize[horizontal]=s[horizontal]+.14f/unitScale[horizontal];}
                    frames.Add(Box(mesh, sill, sillSize));
                    Vector3 mullionSize = s;
                    mullionSize[horizontal] = .055f; mullionSize[normal] = .06f;
                    if(fitSurface){mullionSize[horizontal]/=unitScale[horizontal];mullionSize[normal]/=unitScale[normal];}
                    frames.Add(Box(mesh, p, mullionSize));
                }
            }
            var glass = AssetDatabase.LoadAssetAtPath<Material>(City + "CityV7_Glass.mat");
            SaveCombined(source.name + "_FacadeGlass", root, panes, glass);
            SaveCombined(source.name + "_FacadeSills", root, frames, frame);
            SaveCombined(source.name + "_FacadeWarm", root, lights, warm);
        }
        finally { if(surface!=null)UnityEngine.Object.DestroyImmediate(surface);UnityEngine.Object.DestroyImmediate(cube); }
    }

    static void SaveCombined(string name, Transform root, List<CombineInstance> pieces, Material material)
    {
        if (pieces.Count == 0) return;
        var generated = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        generated.CombineMeshes(pieces.ToArray(), true, true);
        string path = Art + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { AssetDatabase.CreateAsset(generated, path); mesh = generated; }
        else { EditorUtility.CopySerialized(generated, mesh); UnityEngine.Object.DestroyImmediate(generated); }
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(root, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    static void BakeSkyline()
    {
        var root = new GameObject("ExperienceSkyline");
        try
        {
            var sources = new List<GameObject>();
            var names = new HashSet<string>();
            foreach (int chunk in new[] { 2, 4, 7 })
                foreach (Transform child in Resources.Load<GameObject>("CityV7/Chunk" + chunk).transform)
                    if (child.name.Contains("Skyline") && names.Add(child.name)) sources.Add(child.gameObject);
            if (sources.Count == 0) throw new InvalidOperationException("Authored skyline missing.");
            var mat = MaterialAsset(Art + "DistantCity.mat", Shader.Find("Unlit/Color"));
            mat.color = new Color(.32f, .40f, .49f);
            EditorUtility.SetDirty(mat);
            for (int i = 0; i < 28; i++)
            {
                var go = UnityEngine.Object.Instantiate(sources[i % sources.Count], root.transform);
                go.name = "HorizonBuilding_" + i;
                go.transform.localPosition = Vector3.zero;
                Bounds bounds = RendererBounds(go);
                float height = 28f + (i * 13 % 37);
                go.transform.localScale *= height / Mathf.Max(1f, bounds.size.y);
                bounds = RendererBounds(go);
                float angle = (i * 360f / 28f + 4f) * Mathf.Deg2Rad;
                float radius = 250f + (i % 3) * 18f;
                Vector3 anchor = new Vector3(Mathf.Sin(angle) * radius, -18f, Mathf.Cos(angle) * radius);
                go.transform.localPosition = anchor - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                foreach (var renderer in go.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = mat;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "LowerCityGround";
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -12.2f, 0f);
            ground.transform.localScale = new Vector3(800f, .1f, 800f);
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(City + "CityV7_BlueStone.mat");
            // The horizon is a single authored mesh/material draw, instead of
            // keeping dozens of otherwise identical renderer submissions.
            var skylinePieces = new List<CombineInstance>();
            var skylineObjects = new List<GameObject>();
            foreach (Transform child in root.transform)
            {
                if (!child.name.StartsWith("HorizonBuilding_")) continue;
                skylineObjects.Add(child.gameObject);
                foreach (var filter in child.GetComponentsInChildren<MeshFilter>())
                    skylinePieces.Add(new CombineInstance { mesh = filter.sharedMesh,
                        transform = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            }
            SaveCombined("DistantCitySilhouette", root.transform, skylinePieces, mat);
            foreach (var go in skylineObjects) UnityEngine.Object.DestroyImmediate(go);
            var skylineRenderer = root.transform.Find("DistantCitySilhouette").GetComponent<Renderer>();
            skylineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            skylineRenderer.receiveShadows = false;
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/CityV7/ExperienceSkyline.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void BakeDeckSupports()
    {
        foreach (string name in new[] { "TrackSegment", "TurnSegment_Left", "TurnSegment_Right" })
        {
            string path = "Assets/Prefabs/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform old = root.transform.Find("CityDeckPier");
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var pier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pier.name = "CityDeckPier";
                UnityEngine.Object.DestroyImmediate(pier.GetComponent<Collider>());
                pier.transform.SetParent(root.transform, false);
                pier.transform.localPosition = new Vector3(0f, -6.2f, 0f);
                pier.transform.localScale = new Vector3(1.4f, 12f, 1.8f);
                pier.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(City + "CityV7_BlueStone.mat");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    static Bounds RendererBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
