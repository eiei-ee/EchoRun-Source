using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class OrangeEchoArtTests
{
    [TestCase("TrackSegment", TrackSegmentType.Straight)]
    [TestCase("TurnSegment_Right", TrackSegmentType.TurnRight)]
    [TestCase("TurnSegment_Left", TrackSegmentType.TurnLeft)]
    public void ReusedRoadRetainsCollisionAndHasOneAuthoredVisual(string name, TrackSegmentType type)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
        Assert.IsNotNull(asset);
        var root = Object.Instantiate(asset);
        try
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            int[] ids = colliders.Select(c => c.GetInstanceID()).ToArray();
            Matrix4x4[] transforms = colliders.Select(c => c.transform.localToWorldMatrix).ToArray();
            bool[] enabled = colliders.Select(c => c.enabled).ToArray();
            OrangeEchoRoadVisuals.Apply(root, type);
            root.SetActive(false); root.SetActive(true);
            OrangeEchoRoadVisuals.Apply(root, type);
            CollectionAssert.AreEqual(ids, root.GetComponentsInChildren<Collider>(true).Select(c => c.GetInstanceID()));
            CollectionAssert.AreEqual(transforms, colliders.Select(c => c.transform.localToWorldMatrix));
            CollectionAssert.AreEqual(enabled, colliders.Select(c => c.enabled));
            Assert.AreEqual(1, root.GetComponentsInChildren<Transform>(true).Count(t => t.name == OrangeEchoRoadVisuals.RootName));
            Transform art = root.transform.Find(OrangeEchoRoadVisuals.RootName);
            Assert.IsNotNull(art);
            Assert.Zero(art.GetComponentsInChildren<Collider>(true).Length);
            Assert.AreEqual(Vector3.one, art.localScale);
            Assert.AreEqual(Vector3.zero, art.localPosition);
            var renderers = art.GetComponentsInChildren<Renderer>(true);
            Assert.AreEqual(5, renderers.Length);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Assert.AreEqual(type == TrackSegmentType.Straight ? 11f : 15.5f, bounds.size.x, .04f);
            Assert.AreEqual(type == TrackSegmentType.Straight ? 20f : 15.5f, bounds.size.z, .04f);
            if (type != TrackSegmentType.Straight)
            {
                Assert.AreEqual(type == TrackSegmentType.TurnRight ? -5.5f : -10f, bounds.min.x, .04f);
                Assert.AreEqual(type == TrackSegmentType.TurnRight ? 10f : 5.5f, bounds.max.x, .04f);
                Assert.AreEqual(0f, bounds.min.z, .04f);
                Assert.AreEqual(15.5f, bounds.max.z, .04f);
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ClothingUsesOriginalSkeletonAndTintDoesNotReachLiningOrHead()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.scene");
            var player = GameObject.Find("player");
            var model = player.transform.Find("CharacterModel");
            var animator = model.GetComponent<Animator>();
            Assert.IsTrue(animator.avatar.isHuman);
            Assert.IsFalse(animator.applyRootMotion);
            Assert.AreEqual(1, model.GetComponentsInChildren<Animator>(true).Length);
            Assert.AreEqual("Assets/Models/Mixamo/ExoGray/ExoGray_TPose.fbx", AssetDatabase.GetAssetPath(animator.avatar));
            Assert.AreEqual(model, player.GetComponent<PlayerController>().characterModel);
            var capsule = player.GetComponent<CapsuleCollider>();
            Assert.AreEqual(.4f, capsule.radius, .0001f);
            Assert.AreEqual(2.2f, capsule.height, .0001f);
            var outfit = model.Find("OrangeEchoOutfit");
            Assert.IsNotNull(outfit);
            foreach (var skin in outfit.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Assert.IsTrue(skin.bones.All(b => b != null && b.IsChildOf(model) && !b.IsChildOf(outfit)));
                foreach (var weight in skin.sharedMesh.boneWeights)
                    Assert.AreEqual(1f, weight.weight0+weight.weight1+weight.weight2+weight.weight3, .01f);
                var baked = new Mesh();
                skin.BakeMesh(baked);
                Assert.Less(baked.bounds.size.magnitude, 3f, skin.name + " has an invalid skinning scale");
                Object.DestroyImmediate(baked);
            }
            Assert.Greater(RunnerAppearanceService.Apply(model, Color.blue, Color.red, Color.green), 0);
            var block = new MaterialPropertyBlock();
            foreach (var renderer in outfit.GetComponentsInChildren<Renderer>())
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                Material material = renderer.sharedMaterials[i];
                block.Clear(); renderer.GetPropertyBlock(block, i);
                if (material.name.StartsWith("OE_Jacket_Accent"))
                    Assert.AreEqual(Color.red, block.GetColor("_Color"));
                else Assert.IsTrue(block.isEmpty, material.name + " unexpectedly tinted");
            }
        }
        finally
        {
            if (setup.Any(s => s.isLoaded) && setup.Any(s => s.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
