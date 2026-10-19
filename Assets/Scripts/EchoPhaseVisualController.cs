using UnityEngine;

public struct EchoPhaseVisualStyle
{
    public Color tint;
    public float intensity;
    public float coral;
    public float bloomBoost;
    public float contrast;
}

[DefaultExecutionOrder(-110)]
public sealed class EchoPhaseVisualController : MonoBehaviour
{
    public static EchoPhaseVisualController Instance { get; private set; }
    public bool UsesSingleContractVisualState =>
        _usesSingleContractVisualState;
    public SingleContractVisualState ActiveSingleContractVisualState =>
        _singleContractVisualState;
    public EchoPhaseVisualStyle TargetStyle => _target;

    private EchoDuelPhase _phase = EchoDuelPhase.None;
    private EchoPhaseVisualStyle _current;
    private EchoPhaseVisualStyle _target;
    private bool _usesSingleContractVisualState;
    private SingleContractVisualState _singleContractVisualState =
        SingleContractVisualState.Calibration;
    private bool _singleContractReducedMotion;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<EchoPhaseVisualController>() != null) return;
        GameObject instance = new GameObject("EchoPhaseVisualController");
        DontDestroyOnLoad(instance);
        instance.AddComponent<EchoPhaseVisualController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _target = StyleFor(EchoDuelPhase.Calibration);
        _current = _target;
        Apply(_current);
    }

    private void Update()
    {
        if (_usesSingleContractVisualState)
        {
            bool reducedMotion = EchoRunAccessibility.ReducedMotion;
            if (reducedMotion != _singleContractReducedMotion)
            {
                _singleContractReducedMotion = reducedMotion;
                _target = StyleFor(_singleContractVisualState,
                    reducedMotion);
            }
        }
        else
        {
            AIShadowRunner shadow = AIShadowRunner.Instance;
            EchoDuelPhase phase = shadow != null
                ? shadow.DuelPhase : EchoDuelPhase.Calibration;
            if (phase != _phase)
            {
                _phase = phase;
                _target = StyleFor(phase);
            }
        }

        float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f);
        _current.tint = Color.Lerp(_current.tint, _target.tint, blend);
        _current.intensity = Mathf.Lerp(_current.intensity,
            _target.intensity, blend);
        _current.coral = Mathf.Lerp(_current.coral, _target.coral, blend);
        _current.bloomBoost = Mathf.Lerp(_current.bloomBoost,
            _target.bloomBoost, blend);
        _current.contrast = Mathf.Lerp(_current.contrast,
            _target.contrast, blend);
        Apply(_current);
    }

    public void ApplySingleContractVisualState(
        SingleContractVisualState state, bool immediate = false)
    {
        _usesSingleContractVisualState = true;
        _singleContractVisualState = state;
        _singleContractReducedMotion = EchoRunAccessibility.ReducedMotion;
        _target = StyleFor(state, _singleContractReducedMotion);
        if (!immediate) return;

        _current = _target;
        Apply(_current);
    }

    public void ReleaseSingleContractVisualState()
    {
        _usesSingleContractVisualState = false;
        _phase = EchoDuelPhase.None;
    }

    public static EchoPhaseVisualStyle StyleFor(
        SingleContractVisualState state)
    {
        return StyleFor(state, false);
    }

    public static EchoPhaseVisualStyle StyleFor(
        SingleContractVisualState state, bool reducedMotion)
    {
        switch (state)
        {
            case SingleContractVisualState.Calibration:
                return Make(EchoRunUITheme.HudCalibrationAccent,
                    0.24f, 0f, 0f, 0f);
            case SingleContractVisualState.Challenge:
                return Make(EchoRunUITheme.HudChallengeAccent,
                    0.42f, 0.08f, 0.03f, 0.02f);
            case SingleContractVisualState.RelearnPulse:
                return reducedMotion
                    ? Make(EchoRunUITheme.HudRelearnAccent,
                        0.42f, 0.58f, 0.025f, 0.018f)
                    : Make(EchoRunUITheme.HudRelearnAccent,
                        0.50f, 0.70f, 0.04f, 0.025f);
            case SingleContractVisualState.Finale:
                return Make(EchoRunUITheme.HudFinaleAccent,
                    0.55f, 0.88f, 0.08f, 0.045f);
            default:
                return Make(EchoRunUITheme.HudCalibrationAccent,
                    0.24f, 0f, 0f, 0f);
        }
    }

    public static EchoPhaseVisualStyle StyleFor(EchoDuelPhase phase)
    {
        switch (phase)
        {
            case EchoDuelPhase.Detection:
                return Make(new Color(0.18f, 0.48f, 0.78f), 0.24f, 0f, 0f, 0f);
            case EchoDuelPhase.Reveal:
                return Make(new Color(0.08f, 0.88f, 1f), 0.38f, 0.06f, 0.02f, 0.01f);
            case EchoDuelPhase.Resistance:
                return Make(new Color(0.50f, 0.28f, 0.96f), 0.44f, 0.08f, 0.02f, 0.015f);
            case EchoDuelPhase.Counterattack:
                return Make(new Color(1f, 0.20f, 0.12f), 0.50f, 0.70f, 0.04f, 0.025f);
            case EchoDuelPhase.Rewrite:
                return Make(new Color(0.06f, 0.94f, 0.54f), 0.46f, 0.16f, 0.03f, 0.02f);
            case EchoDuelPhase.Finale:
                return Make(new Color(1f, 0.55f, 0.08f), 0.55f, 0.88f, 0.08f, 0.045f);
            case EchoDuelPhase.Finished:
                return Make(new Color(0.16f, 0.62f, 0.78f), 0.12f, 0f, 0f, 0f);
            default:
                return Make(new Color(0.12f, 0.36f, 0.55f), 0.05f, 0f, 0f, 0f);
        }
    }

    private static EchoPhaseVisualStyle Make(Color tint, float intensity,
        float coral, float bloom, float contrast)
    {
        return new EchoPhaseVisualStyle
        {
            tint = tint,
            intensity = intensity,
            coral = coral,
            bloomBoost = bloom,
            contrast = contrast
        };
    }

    private static void Apply(EchoPhaseVisualStyle style)
    {
        Shader.SetGlobalColor("_EchoPhaseTint", style.tint);
        Shader.SetGlobalFloat("_EchoPhaseIntensity", style.intensity);
        Shader.SetGlobalFloat("_EchoPhaseCoral", style.coral);
        Shader.SetGlobalFloat("_EchoPhaseBloomBoost", style.bloomBoost);
        Shader.SetGlobalFloat("_EchoPhaseContrast", style.contrast);
        if (WorldStyler.Instance != null)
            WorldStyler.Instance.ApplyPhaseVisualStyle(style);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
