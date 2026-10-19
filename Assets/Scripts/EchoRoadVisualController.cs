using System;
using System.Collections.Generic;
using UnityEngine;

public enum RoadSurfaceRole
{
    Main,
    Turn,
    Seam,
    StartDeck,
    RuntimeFallback
}

[DefaultExecutionOrder(-150)]
public sealed class EchoRoadVisualController : MonoBehaviour
{
    private const string ResourcePath = "Materials/EchoRoad";
    public const float BaseFlowSpeed = 0.08f;
    private const float MaximumFlowSpeed = 0.32f;
    private static readonly int RoadRoleId = Shader.PropertyToID("_RoadRole");
    private static readonly int UvScaleId = Shader.PropertyToID("_RoadUvScale");
    private static readonly int SeamStrengthId = Shader.PropertyToID("_RoadSeamStrength");
    private static readonly int StartDeckBoostId = Shader.PropertyToID("_RoadStartDeckBoost");
    private static readonly int LaneDensityId = Shader.PropertyToID("_RoadLaneDensity");
    private static readonly int SafeLaneHintId = Shader.PropertyToID("_RoadSafeLaneHint");
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");

    private static EchoRoadVisualController _instance;
    private Material _sharedRoadMaterial;
    private bool _ownsRuntimeMaterial;
    private readonly List<Renderer> _registeredRenderers =
        new List<Renderer>();
    private MaterialPropertyBlock _propertyBlock;
    private float _flowSpeed = BaseFlowSpeed;

    public static EchoRoadVisualController Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindObjectOfType<EchoRoadVisualController>();
            if (_instance != null) return _instance;
            _instance = new GameObject("EchoRoadVisualController")
                .AddComponent<EchoRoadVisualController>();
            return _instance;
        }
    }

    public Material SharedRoadMaterial
    {
        get
        {
            EnsureMaterial();
            return _sharedRoadMaterial;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        EchoRoadVisualController unused = Instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        EnsureMaterial();
        ApplyQuality(VisualQualityController.Current);
        VisualQualityController.Changed += ApplyQuality;
    }

    public void ApplyTo(Renderer renderer, RoadSurfaceRole role)
    {
        if (renderer == null) return;
        EnsureMaterial();
        if (_sharedRoadMaterial == null) return;

        renderer.sharedMaterial = _sharedRoadMaterial;
        MaterialPropertyBlock properties = ReadPropertyBlock(renderer);
        properties.SetFloat(RoadRoleId, (float)role);
        properties.SetFloat(UvScaleId, role == RoadSurfaceRole.Seam ? 2f : 1f);
        properties.SetFloat(SeamStrengthId, role == RoadSurfaceRole.Seam ? 1f : 0f);
        properties.SetFloat(StartDeckBoostId,
            role == RoadSurfaceRole.StartDeck ? 1f : 0f);
        properties.SetFloat(LaneDensityId, IsAuthoredFortressRoad(renderer)
            ? 0f
            : role == RoadSurfaceRole.Turn ? 0.75f : 1f);
        properties.SetFloat(SafeLaneHintId, 0f);
        properties.SetFloat(FlowSpeedId, _flowSpeed);
        renderer.SetPropertyBlock(properties);
        if (!_registeredRenderers.Contains(renderer))
            _registeredRenderers.Add(renderer);
    }

    /// <summary>
    /// Continuously maps the normalized run speed onto the road scan flow.
    /// Renderer property blocks keep phase, lane and shared material ownership
    /// untouched while avoiding per-frame material instances.
    /// </summary>
    public void SetSpeedFeedback(float speed01)
    {
        float nextFlowSpeed = ResolveFlowSpeed(speed01);
        if (!ShouldApplyFlowSpeed(_flowSpeed, nextFlowSpeed)) return;
        _flowSpeed = nextFlowSpeed;
        ApplyFlowSpeedToRegisteredRenderers();
    }

    public void ResetSpeedFeedback()
    {
        _flowSpeed = BaseFlowSpeed;
        ApplyFlowSpeedToRegisteredRenderers();
    }

    public static float ResolveFlowSpeed(float speed01)
    {
        float normalized = Mathf.Clamp01(speed01);
        float eased = Mathf.SmoothStep(0f, 1f, normalized);
        return Mathf.Lerp(BaseFlowSpeed, MaximumFlowSpeed, eased);
    }

    public static bool ShouldApplyFlowSpeed(float current, float next)
    {
        return Mathf.Abs(next - current) >= 0.002f;
    }

    public int ApplyToTrackSegment(GameObject segment, RoadSurfaceRole defaultRole)
    {
        if (segment == null) return 0;
        int applied = 0;
        Renderer[] renderers = segment.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsRoadRenderer(renderer)) continue;
            ApplyTo(renderer, ResolveRole(renderer.name, defaultRole));
            applied++;
        }
        return applied;
    }

    public void ApplyQuality(VisualQuality quality)
    {
        EnsureMaterial();
        if (_sharedRoadMaterial == null) return;
        SetKeyword("_ECHO_NORMALMAP", quality == VisualQuality.High);
        SetKeyword("_ECHO_FAKE_REFLECTION", quality == VisualQuality.High);
        SetKeyword("_ECHO_WET_SURFACE", quality == VisualQuality.High);
        if (quality == VisualQuality.Low)
            SetKeyword("_ECHO_PLANAR_REFLECTION", false);
    }

    public void ApplyPlanarReflection(bool enabled)
    {
        EnsureMaterial();
        if (_sharedRoadMaterial == null) return;
        SetKeyword("_ECHO_PLANAR_REFLECTION",
            enabled && VisualQualityController.Current == VisualQuality.High);
    }

    public void ApplyPhaseTint(Color tint, float intensity)
    {
        Shader.SetGlobalColor("_EchoPhaseTint", tint);
        Shader.SetGlobalFloat("_EchoPhaseIntensity", Mathf.Clamp01(intensity));
    }

    private void EnsureMaterial()
    {
        if (_sharedRoadMaterial != null) return;
        _sharedRoadMaterial = Resources.Load<Material>(ResourcePath);
        if (_sharedRoadMaterial != null) return;

        Shader shader = Shader.Find("EchoRun/Road");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) return;
        _sharedRoadMaterial = new Material(shader)
        {
            name = "EchoRoad_RuntimeFallback"
        };
        if (_sharedRoadMaterial.HasProperty("_Color"))
            _sharedRoadMaterial.SetColor("_Color",
                new Color(0.052f, 0.057f, 0.061f, 1f));
        _ownsRuntimeMaterial = true;
    }

    private static bool IsRoadRenderer(Renderer renderer)
    {
        string objectName = renderer.name;
        if (objectName == "GroundPlane" || objectName == "EntryStrip"
            || objectName == "ExitStrip" || objectName == "Surface"
            || objectName == "EntryCoverage" || objectName == "ExitCoverage"
            || objectName == TrackManager.TurnInnerCornerCapName
            || objectName == "LaunchRoad" || objectName == "Plane"
            || objectName.IndexOf("Seam", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        Material material = renderer.sharedMaterial;
        if (material == null) return false;
        return material.name == "TrackGroundMat"
               || material.name == "TrackGroundMat_Turn"
               || material.name == "TrackSeamMat"
               || material.name == "EchoRoad";
    }

    private static bool IsAuthoredFortressRoad(Renderer renderer)
    {
        Transform current = renderer != null ? renderer.transform : null;
        while (current != null)
        {
            if (current.name == "RoadVisual") return true;
            current = current.parent;
        }
        return false;
    }

    private static RoadSurfaceRole ResolveRole(string rendererName,
        RoadSurfaceRole defaultRole)
    {
        if (rendererName.IndexOf("Seam", StringComparison.OrdinalIgnoreCase) >= 0)
            return RoadSurfaceRole.Seam;
        if (rendererName == "LaunchRoad" || rendererName == "Plane")
            return RoadSurfaceRole.StartDeck;
        if (rendererName == "EntryCoverage" || rendererName == "ExitCoverage"
            || rendererName == TrackManager.TurnInnerCornerCapName)
            return RoadSurfaceRole.RuntimeFallback;
        return defaultRole;
    }

    private void SetKeyword(string keyword, bool enabled)
    {
        if (enabled) _sharedRoadMaterial.EnableKeyword(keyword);
        else _sharedRoadMaterial.DisableKeyword(keyword);
    }

    private MaterialPropertyBlock ReadPropertyBlock(Renderer renderer)
    {
        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();
        _propertyBlock.Clear();
        renderer.GetPropertyBlock(_propertyBlock);
        return _propertyBlock;
    }

    private void ApplyFlowSpeedToRegisteredRenderers()
    {
        for (int i = _registeredRenderers.Count - 1; i >= 0; i--)
        {
            Renderer renderer = _registeredRenderers[i];
            if (renderer == null)
            {
                _registeredRenderers.RemoveAt(i);
                continue;
            }

            MaterialPropertyBlock properties = ReadPropertyBlock(renderer);
            properties.SetFloat(FlowSpeedId, _flowSpeed);
            renderer.SetPropertyBlock(properties);
        }
    }

    private void OnDisable()
    {
        ResetSpeedFeedback();
    }

    private void OnDestroy()
    {
        ResetSpeedFeedback();
        VisualQualityController.Changed -= ApplyQuality;
        if (_ownsRuntimeMaterial && _sharedRoadMaterial != null)
        {
            if (Application.isPlaying) Destroy(_sharedRoadMaterial);
            else DestroyImmediate(_sharedRoadMaterial);
        }
        if (_instance == this) _instance = null;
    }
}
