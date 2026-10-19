using System.IO;
using UnityEditor;
using UnityEngine;

public static class SlidePoseCapture
{
    private const string ModelPath =
        "Assets/Models/Mixamo/ExoGray/ExoGray_TPose.fbx";
    private const string ControllerPath =
        "Assets/Animations/HumanMotion/EchoRunHuman.controller";

    public static void Capture()
    {
        GameObject modelAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                ControllerPath);
        if (modelAsset == null || controller == null)
            throw new FileNotFoundException("Slide capture assets are missing.");

        GameObject model = Object.Instantiate(modelAsset);
        model.name = "SlidePoseCaptureModel";
        model.SetActive(false);
        Animator animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        CharacterAnimator driver = model.AddComponent<CharacterAnimator>();
        driver.useHumanoidRig = true;
        model.SetActive(true);
        animator.Rebind();
        animator.Update(0f);
        driver.SetExternalDriver();
        for (int i = 0; i < 30; i++)
        {
            animator.Update(1f / 60f);
            driver.ApplyExternalMotion(
                false, true, Vector3.forward, 10f, 1f / 60f);
        }

        GameObject lightObject = new GameObject("CaptureLight");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

        GameObject cameraObject = new GameObject("CaptureCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.15f, 0.2f, 1f);
        camera.fieldOfView = 38f;

        string outputFolder = Path.Combine(
            Directory.GetCurrentDirectory(), "Logs");
        Directory.CreateDirectory(outputFolder);
        CaptureView(camera, new Vector3(2.7f, 1.35f, -3.4f),
            new Vector3(0f, 0.65f, 0.35f),
            Path.Combine(outputFolder, "slide-three-quarter.png"));
        CaptureView(camera, new Vector3(3.8f, 1.1f, 0.3f),
            new Vector3(0f, 0.62f, 0.35f),
            Path.Combine(outputFolder, "slide-side.png"));
        camera.fieldOfView = 56f;
        CaptureView(camera, new Vector3(0f, 5.6f, -8.2f),
            new Vector3(0f, 1f, 5f),
            Path.Combine(outputFolder, "slide-game-camera.png"));
        Bounds posedBounds = CalculateBounds(model);
        Vector3 gameLookDirection = new Vector3(0f, -4.6f, 13.2f).normalized;
        CaptureView(camera,
            posedBounds.center - gameLookDirection * 3.2f,
            posedBounds.center,
            Path.Combine(outputFolder, "slide-game-camera-close.png"));
        Vector3 runnerDirection = Vector3.forward;
        Vector3 runnerCameraPosition = model.transform.position
            + Vector3.up * 4.6f - runnerDirection * 8.2f;
        CaptureView(camera, runnerCameraPosition,
            model.transform.position + runnerDirection * 5f,
            Path.Combine(outputFolder, "slide-player-camera.png"));

        Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(lightObject);
        Object.DestroyImmediate(model);
        Debug.Log("SLIDE_POSE_CAPTURE_OK " + outputFolder);
    }

    private static Bounds CalculateBounds(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(model.transform.position + Vector3.up, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void CaptureView(Camera camera, Vector3 position,
        Vector3 target, string path)
    {
        const int width = 720;
        const int height = 720;
        RenderTexture renderTexture = new RenderTexture(
            width, height, 24, RenderTextureFormat.ARGB32);
        camera.transform.position = position;
        camera.transform.LookAt(target);
        camera.targetTexture = renderTexture;
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        Texture2D image = new Texture2D(
            width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());

        RenderTexture.active = previous;
        camera.targetTexture = null;
        Object.DestroyImmediate(image);
        renderTexture.Release();
        Object.DestroyImmediate(renderTexture);
    }
}
