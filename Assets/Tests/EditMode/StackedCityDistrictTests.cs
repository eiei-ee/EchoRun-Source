using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Reads the delivered FBXs and prefab bindings; never invokes an authoring installer.</summary>
public sealed class StackedCityDistrictTests
{
    const string ModelFolder = "Assets/Art/StackedCity/Models/";
    const string DistrictFolder = "Assets/Art/StackedCity/Materials/Districts/";
    static readonly string[] Themes = { "Terracotta", "Jade", "BlueTransit", "Sandstone" };
    static readonly string[] Roles =
        { "MainConcrete", "SecondaryMineral", "Glass", "WindowFrame", "WarmWindow", "EnamelWhite" };
    static readonly string[] SourceMaterialPaths =
    {
        "Assets/CityAfterimageV7/Materials/CityV7_Pale.mat",
        "Assets/CityAfterimageV7/Materials/CityV7_Mineral.mat",
        "Assets/CityAfterimageV7/Materials/CityV7_Glass.mat",
        "Assets/Art/ExperienceSlice/WindowFrame.mat",
        "Assets/Art/ExperienceSlice/WarmWindow.mat",
        "Assets/Art/StackedCity/Materials/SC_White.mat"
    };
    static readonly string[] SignModels =
        { "DistrictSignXixia", "DistrictSignYunting", "DistrictSignQinglan", "DistrictSignZhexiang" };
    static readonly string[] NoticeModels =
    {
        "CityNoticeNoodle", "CityNoticeBookshop", "CityNoticeTailor", "CityNoticePost",
        "CityNoticeGarden", "CityNoticeNightBus", "CityNoticeWind", "CityNoticeNeighbours",
        "CityNoticeYesterday", "CityNoticeCat", "CityNoticeBreakfast", "CityNoticeLostAndFound"
    };

    [TestCase("FacadeResidential")]
    [TestCase("FacadeCivic")]
    [TestCase("FacadeTransit")]
    [TestCase("FacadeHeritage")]
    public void ImportedWindowFamilyKeepsWallOrientationClearanceAndGeometryBudget(string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + name + ".fbx");
        Assert.That(asset, Is.Not.Null, "Missing delivered window family: " + name);
        var instance = Object.Instantiate(asset);
        try
        {
            // Preserve the actual FBX axis conversion: resetting its rotation could hide a bad import.
            instance.transform.position = Vector3.zero;
            Assert.That(instance.GetComponentsInChildren<Collider>(true), Is.Empty, name);
            Assert.That(instance.GetComponentsInChildren<Rigidbody>(true), Is.Empty, name);
            Bounds bounds = WorldBounds(instance);
            Assert.That(bounds.size.x, Is.InRange(2f, 3.05f), "Window X footprint: " + name);
            Assert.That(bounds.size.y, Is.InRange(1.8f, 2.2f), "Window +Y height: " + name);
            Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(.010f),
                "Wall backs need physical clearance instead of a coplanar overlay: " + name);
            Assert.That(bounds.max.z, Is.InRange(.14f, .33f),
                "Front must project toward Unity +Z within the runner-side clearance: " + name);
            Assert.That(Triangles(instance), Is.InRange(30L, 864L),
                "A window family must not exceed the previous FacadeBay triangle budget: " + name);

            bool hasFrontGlass = false, hasRearGlass = false;
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<Renderer>();
                Assert.That(filter.sharedMesh, Is.Not.Null, name);
                Assert.That(AssetDatabase.Contains(filter.sharedMesh), Is.True, "Window mesh must be imported and persistent.");
                Assert.That(renderer, Is.Not.Null, name);
                Assert.That(renderer.sharedMaterial, Is.Not.Null, name);
                if (!renderer.sharedMaterial.name.StartsWith("SC_Glass", StringComparison.Ordinal)) continue;
                using (var snapshot = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh))
                using (var normals = new NativeArray<Vector3>(snapshot[0].vertexCount, Allocator.Temp))
                {
                    snapshot[0].GetNormals(normals);
                    Matrix4x4 normalMatrix = filter.transform.localToWorldMatrix.inverse.transpose;
                    foreach (Vector3 normal in normals)
                    {
                        Vector3 worldNormal = normalMatrix.MultiplyVector(normal).normalized;
                        hasFrontGlass |= worldNormal.z > .99f;
                        hasRearGlass |= worldNormal.z < -.99f;
                    }
                }
            }
            Assert.That(hasFrontGlass, Is.True, "Imported glass must have an outward +Z face: " + name);
            Assert.That(hasRearGlass, Is.True, "Glass must retain its authored closed backing: " + name);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void DeliveredPalettesAreSeparatePersistentMaterialsWithDistinctBodiesAndGlazing()
    {
        var bodyColors = new List<Color>();
        var glassColors = new List<Color>();
        for (int roleIndex = 0; roleIndex < Roles.Length; roleIndex++)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPaths[roleIndex]);
            AssertMaterial(source, SourceMaterialPaths[roleIndex]);
            foreach (string theme in Themes)
            {
                string path = DistrictFolder + "District_" + theme + "_" + Roles[roleIndex] + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                AssertMaterial(material, path);
                Assert.That(material, Is.Not.SameAs(source), "A district must not recolor the shared source material: " + path);
                Assert.That(material.shader, Is.SameAs(source.shader), "Keep the authored surface shader: " + path);
                Assert.That(material.mainTexture, Is.SameAs(source.mainTexture), "Keep the source surface detail map: " + path);
                if (roleIndex == 0) bodyColors.Add(material.color);
                if (roleIndex == 2) glassColors.Add(material.color);
            }
        }
        AssertDistinctColors(bodyColors, "Four different names must not conceal identical building tints.");
        AssertDistinctColors(glassColors, "Window glazing must vary with the architectural palette.");
    }

    [Test]
    public void AllNinePlayableChunksActuallyBindAllFourArchitecturalThemes()
    {
        var bodyThemes = new HashSet<string>(StringComparer.Ordinal);
        var glassThemes = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < 9; index++)
        {
            GameObject chunk = Resource("Chunk" + index);
            int districtSlots = 0;
            foreach (Renderer renderer in chunk.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null) continue;
                string path = AssetDatabase.GetAssetPath(material);
                if (!path.StartsWith(DistrictFolder, StringComparison.Ordinal)) continue;
                districtSlots++;
                AssertMaterial(material, chunk.name + "/" + renderer.name);
                foreach (string theme in Themes)
                {
                    string prefix = DistrictFolder + "District_" + theme + "_";
                    if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    if (path.EndsWith("_MainConcrete.mat", StringComparison.Ordinal)) bodyThemes.Add(theme);
                    if (path.EndsWith("_Glass.mat", StringComparison.Ordinal)) glassThemes.Add(theme);
                }
            }
            Assert.That(districtSlots, Is.GreaterThan(0),
                "A palette folder alone is insufficient; the playable prefab needs saved bindings: " + chunk.name);
        }
        CollectionAssert.AreEquivalent(Themes, bodyThemes, "Four themes must reach actual building bodies along the route.");
        CollectionAssert.AreEquivalent(Themes, glassThemes, "Four themes must reach the actual route's windows.");
    }

    [Test]
    public void ExpandedSignCatalogContainsRealPersistentModelsWithNoRuntimePhysics()
    {
        foreach (string name in AllCatalogModels())
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + name + ".fbx");
            Assert.That(model, Is.Not.Null, "Missing authored sign content: " + name);
            Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty, name);
            Assert.That(model.GetComponentsInChildren<Rigidbody>(true), Is.Empty, name);
            Assert.That(model.GetComponentsInChildren<MeshFilter>(true), Is.Not.Empty, name);
            Assert.That(Triangles(model), Is.GreaterThan(20L), "A catalog label cannot stand in for visible geometry: " + name);
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                Assert.That(AssetDatabase.Contains(filter.sharedMesh), Is.True, "Unsaved catalog geometry: " + name);
        }
    }

    [Test]
    public void PlayableStreetAndCornerStoriesUseTwelveNoticesAndFourDistrictIdentities()
    {
        var notices = new HashSet<string>(StringComparer.Ordinal);
        var signs = new HashSet<string>(StringComparer.Ordinal);
        var inspectedStoryLayers = new HashSet<Transform>();
        foreach (string resourceName in StreetAndCornerResources())
        {
            GameObject prefab = Resource(resourceName);
            int anchorCount = 0;
            foreach (Transform node in prefab.GetComponentsInChildren<Transform>(true))
            {
                bool notice = node.name.StartsWith("Story_CityNotice", StringComparison.Ordinal);
                bool sign = node.name.StartsWith("Story_DistrictSign", StringComparison.Ordinal);
                if (!notice && !sign) continue;
                anchorCount++;
                string modelName = node.name.Substring("Story_".Length);
                if (notice) notices.Add(modelName); else signs.Add(modelName);
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + modelName + ".fbx"), Is.Not.Null,
                    "The placed story identity must correspond to a delivered art asset: " + node.name);
                Assert.That(node.CompareTag("EditorOnly"), Is.True, "Inspection markers must be stripped from the player.");
                Assert.That(node.GetComponents<Component>().Length, Is.EqualTo(1), "Inspection markers remain empty transforms.");
                Assert.That(node.childCount, Is.Zero, "Visible story geometry must survive marker stripping.");
                Assert.That(node.parent, Is.Not.Null, resourceName);
                if (inspectedStoryLayers.Add(node.parent)) AssertSavedStoryLayer(node.parent);
            }
            Assert.That(anchorCount, Is.GreaterThan(0), "Expanded local stories must reach this playable resource: " + resourceName);
        }
        CollectionAssert.IsSubsetOf(NoticeModels, notices, "The actual saved route must use all twelve local notices.");
        CollectionAssert.IsSubsetOf(SignModels, signs, "The actual saved route must use all four district identities.");
    }

    static void AssertSavedStoryLayer(Transform layer)
    {
        for (Transform ancestor = layer; ancestor != null; ancestor = ancestor.parent)
            Assert.That(ancestor.CompareTag("EditorOnly"), Is.False, "Baked story geometry would be stripped from the build.");
        var filters = layer.GetComponentsInChildren<MeshFilter>(true);
        Assert.That(filters, Is.Not.Empty, "Empty markers alone do not constitute a visible story catalog: " + layer.name);
        Assert.That(layer.GetComponentsInChildren<Collider>(true), Is.Empty, layer.name);
        Assert.That(layer.GetComponentsInChildren<Rigidbody>(true), Is.Empty, layer.name);
        foreach (MeshFilter filter in filters)
        {
            Assert.That(filter.sharedMesh, Is.Not.Null, layer.name);
            Assert.That(AssetDatabase.Contains(filter.sharedMesh), Is.True, "Baked stories must use saved meshes: " + layer.name);
            var renderer = filter.GetComponent<Renderer>();
            Assert.That(renderer, Is.Not.Null, filter.name);
            Assert.That(renderer.sharedMaterials, Is.Not.Empty, filter.name);
            foreach (Material material in renderer.sharedMaterials) AssertMaterial(material, filter.name);
        }
    }

    static void AssertMaterial(Material material, string context)
    {
        Assert.That(material, Is.Not.Null, context);
        Assert.That(AssetDatabase.Contains(material), Is.True, "Runtime material instance in authored binding: " + context);
        Assert.That(AssetDatabase.GetAssetPath(material), Is.Not.Empty, context);
        Assert.That(material.shader, Is.Not.Null, context);
        Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False, context);
    }

    static void AssertDistinctColors(List<Color> colors, string message)
    {
        Assert.That(colors.Count, Is.EqualTo(4));
        for (int i = 0; i < colors.Count; i++)
        for (int j = i + 1; j < colors.Count; j++)
        {
            Vector3 difference = new Vector3(colors[i].r - colors[j].r, colors[i].g - colors[j].g, colors[i].b - colors[j].b);
            Assert.That(difference.sqrMagnitude, Is.GreaterThan(.002f), message);
        }
    }

    static GameObject Resource(string name)
    {
        GameObject prefab = Resources.Load<GameObject>("CityV7/" + name);
        Assert.That(prefab, Is.Not.Null, "Missing playable resource: " + name);
        return prefab;
    }

    static IEnumerable<string> StreetAndCornerResources()
    {
        for (int index = 0; index < 9; index++) yield return "Chunk" + index;
        yield return "CornerQuarterLeft";
        yield return "CornerQuarterRight";
    }

    static IEnumerable<string> AllCatalogModels()
    {
        foreach (string name in SignModels) yield return name;
        foreach (string name in NoticeModels) yield return name;
    }

    static Bounds WorldBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Assert.That(renderers, Is.Not.Empty, root.name);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static long Triangles(GameObject root)
    {
        long count = 0;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Assert.That(filter.sharedMesh, Is.Not.Null, filter.name);
            for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                count += filter.sharedMesh.GetIndexCount(submesh) / 3;
        }
        return count;
    }
}
