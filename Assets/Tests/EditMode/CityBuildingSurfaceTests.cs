using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CityBuildingSurfaceTests
{
    [TestCase("Chunk0")][TestCase("Chunk1")][TestCase("Chunk2")]
    [TestCase("Chunk3")][TestCase("Chunk4")][TestCase("Chunk5")]
    [TestCase("Chunk6")][TestCase("Chunk7")][TestCase("Chunk8")]
    [TestCase("CornerQuarterLeft")][TestCase("CornerQuarterRight")]
    public void ClosedFoundationMeetsBuildingWithoutOverlappingExteriorWalls(string resource)
    {
        var prefab = Resources.Load<GameObject>("CityV7/" + resource);
        Assert.That(prefab, Is.Not.Null, resource);
        var root = Object.Instantiate(prefab);
        try
        {
            bool corner = resource.StartsWith("Corner", StringComparison.Ordinal);
            string bodyPrefix = corner ? "CornerTower_" : "V7_GroundedTerrace";
            string basePrefix = corner ? "CornerFoundation_" : "StackedDeepFooting_";
            var buildings = root.transform.Cast<Transform>()
                .Where(t => t.name.StartsWith(bodyPrefix, StringComparison.Ordinal)).ToArray();
            var footings = root.transform.Cast<Transform>()
                .Where(t => t.name.StartsWith(basePrefix, StringComparison.Ordinal)).ToArray();
            Assert.That(buildings, Is.Not.Empty);
            Assert.That(footings.Length, Is.EqualTo(buildings.Length));
            foreach (Transform footing in footings)
            {
                Bounds lower = BoundsIn(footing, root.transform);
                Bounds[] matches = buildings.Select(t => BoundsIn(t, root.transform))
                    .Where(b => Mathf.Abs(lower.center.x - b.center.x) < .05f
                        && Mathf.Abs(lower.center.z - b.center.z) < .05f).ToArray();
                Assert.That(matches.Length, Is.EqualTo(1), footing.name + " must belong to exactly one building");
                float sharedHeight = lower.max.y - matches[0].min.y;
                // The former 6 cm overlap made the two differently shaded
                // vertical walls compete at long distance. Oppositely facing
                // closed mating caps can meet, but exterior strips cannot overlap.
                Assert.That(sharedHeight, Is.InRange(-.0002f, .0002f),
                    resource + "/" + footing.name + " has an exterior overlap or open vertical gap");
                Assert.That(lower.size.y, Is.GreaterThan(95.9f), "Do not shorten the city's occupied depth");
                Assert.That(footing.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    static Bounds BoundsIn(Transform root, Transform basis)
    {
        Bounds result = default;
        bool initialized = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Bounds bounds = renderer.localBounds;
            Matrix4x4 matrix = basis.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 point = matrix.MultiplyPoint3x4(new Vector3(x == 0 ? bounds.min.x : bounds.max.x,
                    y == 0 ? bounds.min.y : bounds.max.y, z == 0 ? bounds.min.z : bounds.max.z));
                if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; }
                else result.Encapsulate(point);
            }
        }
        Assert.That(initialized, Is.True, root.name + " needs closed authored geometry");
        return result;
    }
}
