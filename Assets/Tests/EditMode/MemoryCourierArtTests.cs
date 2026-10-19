using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class MemoryCourierArtTests
{
    [TestCase("Run")]
    [TestCase("Jump")]
    [TestCase("Slide")]
    public void InstalledCourierDeformsWithExistingHumanoidMotion(string state)
    {
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        Mesh baked = null;
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.scene");
            // OpenScene unloads transient native objects from the previous scene.
            baked = new Mesh();
            GameObject player = GameObject.Find("player");
            Assert.IsNotNull(player);
            Transform model = player.transform.Find("CharacterModel");
            Assert.IsNotNull(model);
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(s => s.name == "OE_OrangeEchoClothing").ToArray();
            Assert.AreEqual(1, skins.Length, "The live runner must have one bound courier garment.");
            var skin = skins[0];
            Assert.AreSame(model.Find("OrangeEchoOutfit/OE_OrangeEchoClothing"), skin.transform);
            Assert.AreEqual("Assets/Art/RunnerAthleteCandidate/MemoryCourierClothing_Athlete.asset",
                AssetDatabase.GetAssetPath(skin.sharedMesh));
            Assert.AreEqual(6, skin.sharedMaterials.Length);
            string[] materialNames = { "OE_Jacket_Accent", "OE_RelaySignal", "OE_Navy_Fabric",
                "OE_Rubber", "OE_Ivory_Fabric", "OE_RelayMetal" };
            CollectionAssert.AreEqual(materialNames.Select(name =>
                    "Assets/Art/RunnerAthleteCandidate/" + name + "_Athlete.mat").ToArray(),
                skin.sharedMaterials.Select(AssetDatabase.GetAssetPath).ToArray());
            Assert.That(skin.sharedMesh.triangles.Length / 3, Is.LessThanOrEqualTo(11000));
            Assert.IsEmpty(skin.GetComponentsInChildren<Collider>(true));
            Animator animator = model.GetComponent<Animator>();
            Assert.IsNotNull(animator);
            Assert.AreSame(model, skin.rootBone, "The garment must use the live player skeleton.");
            Assert.AreEqual(skin.sharedMesh.bindposes.Length, skin.bones.Length);
            Assert.IsTrue(skin.bones.All(b => b != null && b.IsChildOf(model)),
                "Garment bones must belong to the live runner, not an imported duplicate skeleton.");
            Assert.AreEqual(1, Object.FindObjectsOfType<Animator>(true).Length,
                "The temporary import skeleton must not be saved into the game scene.");
            Assert.IsTrue(animator.isHuman);
            Assert.IsFalse(animator.applyRootMotion);
            Assert.AreEqual("Assets/Animations/HumanMotion/EchoRunHuman.controller",
                AssetDatabase.GetAssetPath(animator.runtimeAnimatorController));
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            skin.forceMatrixRecalculationPerRender = true;
            Transform leg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            CollectionAssert.Contains(skin.bones, leg, "The garment must actually bind to the animated leg.");
            Quaternion rest = leg.localRotation;
            float maximumRotation = 0f;
            float maximumDeformation = 0f;
            Vector3[] firstPose = null;
            foreach (float normalized in new[] { .15f, .45f, .75f })
            {
                // The accepted Jump state is driven by the physical jump phase,
                // so Animator.Play's normalized time alone does not sample it.
                if (state == "Jump") animator.SetFloat("RunnerJumpPhase", normalized);
                animator.Play(state, 0, normalized);
                animator.Update(0f);
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName(state));
                maximumRotation = Mathf.Max(maximumRotation, Quaternion.Angle(rest, leg.localRotation));
                skin.BakeMesh(baked);
                Assert.IsTrue(baked.vertices.All(v => !float.IsNaN(v.x) && !float.IsNaN(v.y)
                    && !float.IsNaN(v.z) && !float.IsInfinity(v.sqrMagnitude)));
                Assert.That(baked.bounds.size.magnitude, Is.InRange(.7f, 3f), state + " deformation bounds");
                Assert.AreEqual(skin.sharedMesh.vertexCount, baked.vertexCount);
                Vector3[] vertices = baked.vertices;
                if (firstPose == null) firstPose = vertices;
                else for (int i = 0; i < vertices.Length; i++)
                    maximumDeformation = Mathf.Max(maximumDeformation,
                        Vector3.Distance(firstPose[i], vertices[i]));
            }
            Assert.Greater(maximumRotation, 10f, "Do not accept T-pose captures as motion verification.");
            Assert.Greater(maximumDeformation, .025f,
                state + " must deform the visible garment, not only rotate an unbound skeleton.");
        }
        finally
        {
            if (baked != null) Object.DestroyImmediate(baked);
            if (setup.Any(s => s.isLoaded) && setup.Any(s => s.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
