using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CityFinishGateTests
{
    const string ModelPath = "Assets/Art/StackedCity/Models/CityFinishGate.fbx";
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    static readonly int TintColor = Shader.PropertyToID("_Color");

    [Test]
    public void DeliveredGateUsesPersistentAuthoredGeometryAndMaterialsWithoutPhysics()
    {
        GameObject prefab = Resource();
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), Is.Not.Null,
            "The finish must use a delivered city asset, not runtime primitive geometry.");
        Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        Assert.That(prefab.GetComponent<FinishGatePresentation>(), Is.Not.Null);
        Assert.That(prefab.transform.Find("ProtocolCore"), Is.Null,
            "The retired floating sphere must not remain inside the new gate.");
        MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
        Assert.That(filters, Is.Not.Empty);
        long triangles = 0;
        var materials = new HashSet<Material>();
        foreach (MeshFilter filter in filters)
        {
            Assert.That(filter.sharedMesh, Is.Not.Null, filter.name);
            Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Is.EqualTo(ModelPath),
                "Every visible part must come from the imported finish model: " + filter.name);
            for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                triangles += filter.sharedMesh.GetIndexCount(submesh) / 3;
            var renderer = filter.GetComponent<Renderer>();
            Assert.That(renderer, Is.Not.Null, filter.name);
            Assert.That(renderer.sharedMaterials, Is.Not.Empty, filter.name);
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.That(material, Is.Not.Null, filter.name);
                Assert.That(AssetDatabase.Contains(material), Is.True, filter.name);
                Assert.That(material.shader, Is.Not.Null, filter.name);
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False, filter.name);
                materials.Add(material);
            }
        }
        Assert.That(triangles, Is.InRange(100L, 11999L));
        Assert.That(materials.Count, Is.InRange(3, 6));
    }

    [Test]
    public void AuthoredTrianglesKeepTheEntireThreeLaneOpeningClear()
    {
        GameObject instance = Object.Instantiate(Resource());
        try
        {
            // Retain the saved FBX transform, including its axis conversion.
            instance.transform.position = Vector3.zero;
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Assert.That(bounds.size.x, Is.InRange(10.5f, 11.8f));
            Assert.That(bounds.min.y, Is.InRange(-.01f, .01f));
            Assert.That(bounds.max.y, Is.InRange(5.4f, 5.95f));
            Assert.That(bounds.size.z, Is.LessThanOrEqualTo(1.21f));

            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
            using (var snapshot = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh))
            using (var vertices = new NativeArray<Vector3>(snapshot[0].vertexCount, Allocator.Temp))
            {
                snapshot[0].GetVertices(vertices);
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                using (var indices = new NativeArray<int>((int)filter.sharedMesh.GetIndexCount(submesh), Allocator.Temp))
                {
                    snapshot[0].GetIndices(indices, submesh);
                    for (int index = 0; index < indices.Length; index += 3)
                    {
                        var polygon = new List<Vector3>(3)
                        {
                            matrix.MultiplyPoint3x4(vertices[indices[index]]),
                            matrix.MultiplyPoint3x4(vertices[indices[index + 1]]),
                            matrix.MultiplyPoint3x4(vertices[indices[index + 2]])
                        };
                        // Triangle clipping also catches a beam spanning the opening when
                        // all three original vertices happen to lie outside the opening.
                        polygon = Clip(polygon, Vector3.right, -4.649f);
                        polygon = Clip(polygon, Vector3.left, -4.649f);
                        polygon = Clip(polygon, Vector3.down, -4.249f);
                        Assert.That(Area(polygon), Is.LessThan(0.0000001f),
                            "Visible geometry intrudes into the runner opening: " + filter.name
                            + " submesh=" + submesh + " triangle=" + index / 3);
                    }
                }
            }
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void ApproachFeedbackChangesOnlySignalPropertyBlocksAndPreservesSharedSurfaces()
    {
        GameObject instance = Object.Instantiate(Resource());
        try
        {
            FinishGatePresentation presentation = instance.GetComponent<FinishGatePresentation>();
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.signalRenderers, Is.Not.Empty);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            var signals = new HashSet<Renderer>(presentation.signalRenderers);
            Assert.That(signals.Count, Is.EqualTo(presentation.signalRenderers.Length));
            Assert.That(signals.Count, Is.LessThan(renderers.Length),
                "Mineral structure and sign lettering must not be registered as approach signals.");
            var signalMaterials = new HashSet<Material>();
            var materialStates = new Dictionary<Material, string>();
            var bindings = new Dictionary<Renderer, Material[]>();
            Color sentinel = new Color(.19f, .27f, .31f, .73f);
            foreach (Renderer signal in signals)
            {
                Assert.That(signal, Is.Not.Null);
                Assert.That(signal.transform.IsChildOf(instance.transform), Is.True);
                foreach (Material material in signal.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null);
                    Assert.That(material.HasProperty(EmissionColor), Is.True);
                    Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.True,
                        "The MPB needs an emissive surface to be visible.");
                    signalMaterials.Add(material);
                }
            }
            foreach (Renderer renderer in renderers)
            {
                bindings.Add(renderer, renderer.sharedMaterials);
                var block = new MaterialPropertyBlock();
                block.SetColor(TintColor, sentinel);
                renderer.SetPropertyBlock(block);
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (!materialStates.ContainsKey(material))
                        materialStates.Add(material, EditorJsonUtility.ToJson(material));
                    if (!signals.Contains(renderer))
                        Assert.That(signalMaterials.Contains(material), Is.False,
                            "Signal glow requires its own material, separate from the architectural surfaces.");
                }
            }

            foreach (float progress in new[] { -2f, .5f, 3f })
            {
                presentation.SetApproach(progress, false);
                Color expected = Color.Lerp(presentation.distantEmission,
                    presentation.arrivalEmission, Mathf.Clamp01(progress));
                foreach (Renderer renderer in renderers)
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    AssertColor(block.GetColor(TintColor), sentinel,
                        "Approach feedback must preserve other per-renderer properties.");
                    AssertColor(block.GetColor(EmissionColor),
                        signals.Contains(renderer) ? expected : Color.clear,
                        "Only dedicated signals may receive approach emission: " + renderer.name);
                    CollectionAssert.AreEqual(bindings[renderer], renderer.sharedMaterials,
                        "Approaching the gate must not allocate or replace material bindings.");
                }
                foreach (var state in materialStates)
                    Assert.That(EditorJsonUtility.ToJson(state.Key), Is.EqualTo(state.Value),
                        "Runtime feedback mutated an authored shared material: " + state.Key.name);
            }
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void ArrivalLightsHaveBoundedCostAndRespectExplicitApproachState()
    {
        GameObject instance = Object.Instantiate(Resource());
        try
        {
            instance.SetActive(true);
            FinishGatePresentation presentation = instance.GetComponent<FinishGatePresentation>();
            Assert.That(presentation, Is.Not.Null);
            Light[] lights = instance.GetComponentsInChildren<Light>(true);
            Assert.That(lights.Length, Is.EqualTo(2));
            CollectionAssert.AreEquivalent(lights, presentation.arrivalLights);
            foreach (Light light in lights)
            {
                Assert.That(light.type, Is.EqualTo(LightType.Point));
                Assert.That(light.range, Is.InRange(.1f, 5.5f));
                Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
            }
            presentation.SetApproach(1f, true);
            foreach (Light light in lights) Assert.That(light.enabled, Is.True);
            presentation.SetApproach(1f, false);
            foreach (Light light in lights) Assert.That(light.enabled, Is.False);
            presentation.SetApproach(0f, false);
            foreach (Light light in lights) Assert.That(light.enabled, Is.False);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    static GameObject Resource()
    {
        GameObject prefab = Resources.Load<GameObject>(FinishGatePresentation.ResourcePath);
        Assert.That(prefab, Is.Not.Null, "Missing installed city finish prefab.");
        Assert.That(AssetDatabase.Contains(prefab), Is.True);
        return prefab;
    }

    static void AssertColor(Color actual, Color expected, string message)
    {
        Assert.That(Vector4.Distance(actual, expected), Is.LessThan(.00001f), message);
    }

    static List<Vector3> Clip(List<Vector3> input, Vector3 normal, float minimum)
    {
        var output = new List<Vector3>();
        if (input.Count == 0) return output;
        Vector3 previous = input[input.Count - 1];
        float previousDistance = Vector3.Dot(previous, normal) - minimum;
        foreach (Vector3 current in input)
        {
            float distance = Vector3.Dot(current, normal) - minimum;
            if ((distance >= 0f) != (previousDistance >= 0f))
                output.Add(Vector3.LerpUnclamped(previous, current,
                    previousDistance / (previousDistance - distance)));
            if (distance >= 0f) output.Add(current);
            previous = current;
            previousDistance = distance;
        }
        return output;
    }

    static float Area(List<Vector3> polygon)
    {
        float area = 0f;
        for (int index = 1; index + 1 < polygon.Count; index++)
            area += Vector3.Cross(polygon[index] - polygon[0],
                polygon[index + 1] - polygon[0]).magnitude * .5f;
        return area;
    }
}
