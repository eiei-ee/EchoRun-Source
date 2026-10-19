using UnityEngine;

[DefaultExecutionOrder(-120)]
public sealed class PostFxController : MonoBehaviour
{
    public const int PlanarReflectionResolution = 256;
    public const float PlanarReflectionUpdateRate = 20f;

    public static PostFxController Instance { get; private set; }
    public bool BloomEnabled { get; private set; } = true;
    public bool ColorGradingEnabled { get; private set; } = true;
    public bool VignetteEnabled { get; private set; } = true;
    public bool PlanarReflectionEnabled { get; private set; }
    public bool IsPlanarReflectionActive { get; private set; }

    private Camera _mainCamera;
    private EchoPostFxEffect _effect;
    private Camera _reflectionCamera;
    private RenderTexture _reflectionTexture;
    private float _nextReflectionUpdate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<PostFxController>() != null) return;
        GameObject instance = new GameObject("PostFxController");
        DontDestroyOnLoad(instance);
        instance.AddComponent<PostFxController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        VisualQualityController.Changed += ApplyQuality;
        EnsureCameraEffect();
        ApplyQuality(VisualQualityController.Current);
    }

    private void Update()
    {
        EnsureCameraEffect();
        ApplyState();
        if (IsPlanarReflectionActive
            && Time.unscaledTime >= _nextReflectionUpdate)
        {
            _nextReflectionUpdate = Time.unscaledTime
                + 1f / PlanarReflectionUpdateRate;
            RenderPlanarReflection();
        }
    }

    public void SetBloomEnabled(bool enabled)
    {
        BloomEnabled = enabled;
        ApplyState();
    }

    public void SetColorGradingEnabled(bool enabled)
    {
        ColorGradingEnabled = enabled;
        ApplyState();
    }

    public void SetVignetteEnabled(bool enabled)
    {
        VignetteEnabled = enabled;
        ApplyState();
    }

    public void SetPlanarReflectionEnabled(bool enabled)
    {
        PlanarReflectionEnabled = enabled;
        ApplyState();
    }

    public static bool SupportsHighFx(RuntimePlatform platform)
    {
        return platform == RuntimePlatform.WindowsPlayer
               || platform == RuntimePlatform.WindowsEditor;
    }

    private void ApplyQuality(VisualQuality unused)
    {
        ApplyState();
    }

    private void ApplyState()
    {
        bool highFx = VisualQualityController.Current == VisualQuality.High
                      && SupportsHighFx(Application.platform);
        if (_effect != null)
        {
            _effect.Configure(BloomEnabled, ColorGradingEnabled,
                VignetteEnabled);
            _effect.enabled = highFx
                              && (BloomEnabled || ColorGradingEnabled
                                  || VignetteEnabled);
        }

        bool reflection = highFx && PlanarReflectionEnabled;
        if (IsPlanarReflectionActive != reflection)
        {
            IsPlanarReflectionActive = reflection;
            if (reflection) EnsureReflectionResources();
            else ReleaseReflectionResources();
            EchoRoadVisualController.Instance.ApplyPlanarReflection(reflection);
        }
    }

    private void EnsureCameraEffect()
    {
        Camera current = Camera.main;
        if (current == null || current == _mainCamera) return;
        if (_effect != null) _effect.enabled = false;
        _mainCamera = current;
        _effect = current.GetComponent<EchoPostFxEffect>();
        if (_effect == null) _effect = current.gameObject.AddComponent<EchoPostFxEffect>();
    }

    private void EnsureReflectionResources()
    {
        if (_reflectionTexture == null)
        {
            _reflectionTexture = new RenderTexture(PlanarReflectionResolution,
                PlanarReflectionResolution, 16, RenderTextureFormat.ARGB32)
            {
                name = "EchoPlanarReflection_256",
                filterMode = FilterMode.Bilinear,
                useMipMap = false
            };
            _reflectionTexture.Create();
        }
        if (_reflectionCamera == null)
        {
            GameObject cameraObject = new GameObject("EchoReflectionCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            _reflectionCamera = cameraObject.AddComponent<Camera>();
            _reflectionCamera.enabled = false;
        }
        Shader.SetGlobalTexture("_EchoPlanarReflectionTex", _reflectionTexture);
        Shader.SetGlobalFloat("_EchoPlanarReflectionStrength", 0.12f);
    }

    private void RenderPlanarReflection()
    {
        if (_mainCamera == null || _reflectionCamera == null
            || _reflectionTexture == null) return;
        const float planeHeight = 0.04f;
        Transform source = _mainCamera.transform;
        Vector3 sourcePosition = source.position;
        _reflectionCamera.transform.position = new Vector3(sourcePosition.x,
            planeHeight * 2f - sourcePosition.y, sourcePosition.z);
        Vector3 euler = source.eulerAngles;
        _reflectionCamera.transform.rotation = Quaternion.Euler(-euler.x,
            euler.y, euler.z);
        _reflectionCamera.fieldOfView = _mainCamera.fieldOfView;
        _reflectionCamera.aspect = 1f;
        _reflectionCamera.nearClipPlane = _mainCamera.nearClipPlane;
        _reflectionCamera.farClipPlane = Mathf.Min(90f, _mainCamera.farClipPlane);
        _reflectionCamera.clearFlags = CameraClearFlags.Skybox;
        _reflectionCamera.backgroundColor = _mainCamera.backgroundColor;
        int emissiveLayer = LayerMask.NameToLayer("EchoEmissive");
        _reflectionCamera.cullingMask = emissiveLayer >= 0
            ? 1 << emissiveLayer : 0;
        _reflectionCamera.targetTexture = _reflectionTexture;
        _reflectionCamera.Render();
        Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(
            _reflectionCamera.projectionMatrix, true);
        Shader.SetGlobalMatrix("_EchoReflectionVP",
            gpuProjection * _reflectionCamera.worldToCameraMatrix);
    }

    private void ReleaseReflectionResources()
    {
        Shader.SetGlobalFloat("_EchoPlanarReflectionStrength", 0f);
        if (_reflectionCamera != null)
        {
            if (Application.isPlaying) Destroy(_reflectionCamera.gameObject);
            else DestroyImmediate(_reflectionCamera.gameObject);
            _reflectionCamera = null;
        }
        if (_reflectionTexture != null)
        {
            _reflectionTexture.Release();
            if (Application.isPlaying) Destroy(_reflectionTexture);
            else DestroyImmediate(_reflectionTexture);
            _reflectionTexture = null;
        }
    }

    private void OnDestroy()
    {
        VisualQualityController.Changed -= ApplyQuality;
        if (_effect != null) _effect.enabled = false;
        ReleaseReflectionResources();
        if (Instance == this) Instance = null;
    }
}
