using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CityBridgeSurfaceTests
{
    [TestCase("TrackSegment")]
    [TestCase("TurnSegment_Left")]
    [TestCase("TurnSegment_Right")]
    public void StructuralFacesHaveOneVisibleOwnerAtBridgeCrossSections(string segment)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + segment + ".prefab");
        var instance = Object.Instantiate(prefab);
        try
        {
            var structure = instance.transform.Find("CityBridgeStructure");
            Assert.That(structure, Is.Not.Null);
            var report = new List<string>();
            var audit = System.Type.GetType("CityFlickerGeometryAudit, TempleRun.Editor", true);
            int overlaps = (int)audit.GetMethod("AuditRoot").Invoke(null,
                new object[] { structure.gameObject, report, segment });
            Assert.That(overlaps, Is.Zero, string.Join("\n", report));

            // The correction must preserve the original white concrete slab,
            // piers and the exposed lower edge of the metal girders.
            var concrete = structure.Find("Concrete").GetComponent<MeshFilter>();
            Assert.That(concrete.sharedMesh, Is.SameAs(AssetDatabase.LoadAssetAtPath<Mesh>(
                "Assets/Art/CityLayers/" + segment + "Bridge_0.asset")));
            var metal = structure.Find("RoofMetal").GetComponent<MeshFilter>();
            Assert.That(System.Array.Exists(metal.sharedMesh.vertices,
                vertex => Mathf.Abs(vertex.y + 1.825f) < .001f), Is.True);
            var originalMetal = AssetDatabase.LoadAssetAtPath<Mesh>(
                "Assets/Art/CityLayers/" + segment + "Bridge_1.asset");
            Vector2[] uv2 = metal.sharedMesh.uv2;
            Assert.That(uv2.Length, Is.EqualTo(metal.sharedMesh.vertexCount),
                "Bridge clipping must retain authored secondary UVs.");
            Vector2 minimum = Vector2.positiveInfinity, maximum = Vector2.negativeInfinity;
            foreach (Vector2 uv in originalMetal.uv2)
            {
                minimum = Vector2.Min(minimum, uv);
                maximum = Vector2.Max(maximum, uv);
            }
            foreach (Vector2 uv in uv2)
            {
                Assert.That(float.IsNaN(uv.x) || float.IsNaN(uv.y)
                    || float.IsInfinity(uv.x) || float.IsInfinity(uv.y), Is.False);
                Assert.That(uv.x, Is.InRange(minimum.x - .0001f, maximum.x + .0001f));
                Assert.That(uv.y, Is.InRange(minimum.y - .0001f, maximum.y + .0001f));
            }
            Assert.That(structure.GetComponentsInChildren<Collider>(), Is.Empty);
        }
        finally { Object.DestroyImmediate(instance); }
    }
}
