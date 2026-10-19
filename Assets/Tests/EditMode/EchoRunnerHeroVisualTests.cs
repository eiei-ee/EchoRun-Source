using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class EchoRunnerHeroVisualTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";
    private const string SignalMaterialPath =
        "Assets/Art/Characters/EchoRunner/PhaseOneHero/Materials/" +
        "EchoMemorySpineSignal.mat";
    private const string CoreMaterialPath =
        "Assets/Art/Characters/EchoRunner/PhaseOneHero/Materials/" +
        "EchoMemorySpineCore.mat";

    [Test]
    public void ContactShadowProfileFadesAndSoftensWithHeight()
    {
        Assert.AreEqual(1f,
            EchoRunnerHeroVisual.ResolveShadowScale(0f, 3f), 0.0001f);
        Assert.AreEqual(1.22f,
            EchoRunnerHeroVisual.ResolveShadowScale(8f, 3f), 0.0001f);
        Assert.AreEqual(0.34f,
            EchoRunnerHeroVisual.ResolveShadowAlpha(
                0f, 3f, 0.34f, 0.07f), 0.0001f);
        Assert.AreEqual(0.07f,
            EchoRunnerHeroVisual.ResolveShadowAlpha(
                8f, 3f, 0.34f, 0.07f), 0.0001f);

        Bounds visiblePlane = new Bounds(
            new Vector3(0f, -0.1f, 0f),
            new Vector3(200f, 0f, 200f));
        Vector3 projected = EchoRunnerHeroVisual.ResolveVisualSurfacePoint(
            new Vector3(2f, 0.4f, 3f), Vector3.up,
            true, visiblePlane);
        Assert.AreEqual(new Vector3(2f, -0.1f, 3f), projected);

        Bounds solidRoad = new Bounds(
            Vector3.zero, new Vector3(10f, 1f, 10f));
        Vector3 solidHit = new Vector3(2f, 0.5f, 3f);
        Assert.AreEqual(solidHit,
            EchoRunnerHeroVisual.ResolveVisualSurfacePoint(
                solidHit, Vector3.up, true, solidRoad));
    }

    [Test]
    public void IdentityAndGhostShadersUseIntegratedLowIntensityProfiles()
    {
        Shader bodyShader = Shader.Find("EchoRun/ExoGrayBlueTech");
        Shader identityShader = Shader.Find("EchoRun/RunnerIdentity");
        Shader shadowShader = Shader.Find("EchoRun/ContactShadow");
        Shader ghostShader = Resources.Load<Shader>("Shaders/EchoGhost");
        Assert.IsNotNull(bodyShader);
        Assert.IsNotNull(identityShader);
        Assert.IsNotNull(shadowShader);
        Assert.IsNotNull(ghostShader);

        Material signal = AssetDatabase.LoadAssetAtPath<Material>(
            SignalMaterialPath);
        Material core = AssetDatabase.LoadAssetAtPath<Material>(
            CoreMaterialPath);
        Assert.IsNotNull(signal);
        Assert.IsNotNull(core);
        Assert.AreEqual(identityShader, signal.shader);
        Assert.AreEqual(identityShader, core.shader);
        Color coreIdentity = core.GetColor("_IdentityColor");
        Assert.Greater(coreIdentity.r, coreIdentity.b);
        Assert.Greater(coreIdentity.g, coreIdentity.b);
        Assert.Less(coreIdentity.r - coreIdentity.b, 0.5f);
        Assert.That(signal.GetFloat("_IdentityStrength"),
            Is.InRange(0.005f, 0.02f));
        Assert.That(core.GetFloat("_IdentityStrength"),
            Is.InRange(0.20f, 0.40f));
        Assert.Greater(core.GetFloat("_PulseAmount"), 0f);

        Material ghost = new Material(ghostShader);
        try
        {
            Color body = AIShadowRunner.ResolveGhostBodyColor(
                false, false, 0f);
            // The current digital-echo palette uses a translucent cyan body.
            // Keep the body visible, with a cool hue distinct from the runner.
            Assert.That(body.a, Is.InRange(0.30f, 0.60f));
            Assert.Greater(body.b, body.r);
            Assert.Greater(body.g, body.r);
            Assert.AreEqual(
                AIShadowRunner.ResolveGhostBodyColor(false, true, 0f),
                AIShadowRunner.ResolveGhostBodyColor(false, true, 17f));
            Assert.LessOrEqual(ghost.GetFloat("_EmissionStrength"), 0.5f);
            Assert.LessOrEqual(ghost.GetFloat("_ScanStrength"), 0.25f);
            Assert.LessOrEqual(ghost.GetFloat("_GlitchStrength"), 0.03f);
        }
        finally
        {
            Object.DestroyImmediate(ghost);
        }
    }

    [Test]
    public void InstalledHeroLayerPreservesPlayerAndAnimationContracts()
    {
        SceneSetup[] previousSetup =
            EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("player");
            Assert.IsNotNull(player);

            PlayerController controller =
                player.GetComponent<PlayerController>();
            CapsuleCollider capsule =
                player.GetComponent<CapsuleCollider>();
            Transform model = player.transform.Find("CharacterModel");
            Assert.IsNotNull(controller);
            Assert.IsNotNull(capsule);
            Assert.IsNotNull(model);
            Assert.AreEqual(model, controller.characterModel);
            Assert.AreEqual(0.4f, capsule.radius, 0.0001f);
            Assert.AreEqual(2.2f, capsule.height, 0.0001f);
            Assert.AreEqual(new Vector3(0f, 1f, 0f), capsule.center);

            Animator animator = model.GetComponent<Animator>();
            CharacterAnimator driver =
                model.GetComponent<CharacterAnimator>();
            Assert.IsNotNull(animator);
            Assert.IsNotNull(animator.avatar);
            Assert.IsTrue(animator.avatar.isHuman);
            Assert.IsFalse(animator.applyRootMotion);
            Assert.IsNotNull(driver);
            Assert.IsTrue(driver.enabled);

            Transform memorySpine =
                FindDescendant(model, "EchoMemorySpine");
            Assert.IsNotNull(memorySpine);
            Assert.AreEqual(
                animator.GetBoneTransform(HumanBodyBones.Chest),
                memorySpine.parent);
            Assert.AreEqual(3,
                memorySpine.GetComponentsInChildren<Renderer>(true).Length);
            Assert.AreEqual(0,
                memorySpine.GetComponentsInChildren<Collider>(true).Length);
            Assert.AreEqual(0,
                memorySpine.GetComponentsInChildren<Rigidbody>(true).Length);

            Transform upper = FindDescendant(
                memorySpine, "MemorySegment_Upper");
            Transform core = FindDescendant(
                memorySpine, "MemorySegment_Core");
            Transform lower = FindDescendant(
                memorySpine, "MemorySegment_Lower");
            Assert.IsNotNull(upper);
            Assert.IsNotNull(core);
            Assert.IsNotNull(lower);
            Assert.IsNull(FindDescendant(memorySpine, "MemoryNode_01"));
            float upperArea = upper.localScale.x * upper.localScale.y;
            float coreArea = core.localScale.x * core.localScale.y;
            Assert.AreEqual(1.42f, coreArea / upperArea, 0.08f);

            Transform backplate = FindDescendant(
                memorySpine, "SpineBackplate");
            Assert.IsNotNull(backplate);
            MeshFilter backplateMesh = backplate.GetComponent<MeshFilter>();
            Assert.IsNotNull(backplateMesh);
            Assert.Greater(backplateMesh.sharedMesh.vertexCount, 16);

            Transform shadow =
                player.transform.Find("EchoRunnerContactShadow");
            EchoRunnerHeroVisual hero =
                player.GetComponent<EchoRunnerHeroVisual>();
            Assert.IsNotNull(shadow);
            Assert.IsNotNull(hero);
            Assert.AreEqual(shadow, hero.ContactShadow);
            Assert.IsNotNull(hero.ContactShadowRenderer);
            Assert.AreEqual(0,
                shadow.GetComponentsInChildren<Collider>(true).Length);
        }
        finally
        {
            bool hasLoadedScene = false;
            for (int i = 0; i < previousSetup.Length; i++)
            {
                if (!previousSetup[i].isLoaded) continue;
                hasLoadedScene = true;
                break;
            }

            if (hasLoadedScene)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            else
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null) return null;
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
            if (descendants[i].name == name) return descendants[i];
        return null;
    }
}
