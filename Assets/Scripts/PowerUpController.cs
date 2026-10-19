using UnityEngine;

public sealed class PowerUpController : MonoBehaviour
{
    // Keep historical save/API compatibility, but retire supplies from this release.
    public static bool IsAvailable => false;
    public static PowerUpController Instance { get; private set; }

    public PowerUpId ActivePowerUp { get; private set; } = PowerUpId.None;
    public float TimeRemaining { get; private set; }
    public bool HasMagnet => IsAvailable && ActivePowerUp == PowerUpId.Magnet && TimeRemaining > 0f;
    public float ScoreMultiplier => IsAvailable && ActivePowerUp == PowerUpId.ScoreBoost && TimeRemaining > 0f
        ? Mathf.Max(1f, _definition.value)
        : 1f;
    public float MagnetRadius => GameBalanceConfig.Current.gameplay.magnetRadius;

    private PowerUpBalance _definition;
    private int _shieldCharges;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<PowerUpController>() != null) return;
        GameObject host = new GameObject("Power Up Controller");
        DontDestroyOnLoad(host);
        host.AddComponent<PowerUpController>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (ActivePowerUp == PowerUpId.None || ActivePowerUp == PowerUpId.Shield)
            return;
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            return;

        TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
        if (TimeRemaining <= 0f) ClearActive();
    }

    public void BeginRun()
    {
        BeginRun(true);
    }

    public void BeginRun(bool allowSelectedPowerUp)
    {
        ClearActive();
        if (!IsAvailable || !allowSelectedPowerUp) return;
        PowerUpId selected = EchoRunSaveSystem.GetSelectedPowerUp();
        if (selected == PowerUpId.None || !EchoRunSaveSystem.ConsumePowerUp(selected))
            return;

        _definition = GameBalanceConfig.GetPowerUp(selected);
        if (_definition == null) return;
        ActivePowerUp = selected;
        TimeRemaining = Mathf.Max(0f, _definition.duration);
        _shieldCharges = selected == PowerUpId.Shield ? 1 : 0;
        AIRunTelemetry.RecordEvent("powerup_used", (int)selected);
        AudioManager.Instance?.PlayUIConfirm();
    }

    public bool TryAbsorbCollision()
    {
        if (!IsAvailable || ActivePowerUp != PowerUpId.Shield || _shieldCharges <= 0) return false;
        _shieldCharges--;
        AIRunTelemetry.RecordEvent("shield_absorb", (int)PowerUpId.Shield);
        ClearActive();
        return true;
    }

    public float GetTurboStartBonus()
    {
        return IsAvailable && ActivePowerUp == PowerUpId.TurboStart && _definition != null
            ? Mathf.Max(0f, _definition.value)
            : 0f;
    }

    public string GetStatusText()
    {
        if (!IsAvailable || ActivePowerUp == PowerUpId.None || _definition == null) return "";
        if (ActivePowerUp == PowerUpId.Shield)
            return _definition.displayName + " · 1 次";
        return _definition.displayName + " · " + TimeRemaining.ToString("0.0") + "s";
    }

    private void ClearActive()
    {
        ActivePowerUp = PowerUpId.None;
        TimeRemaining = 0f;
        _shieldCharges = 0;
        _definition = null;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
