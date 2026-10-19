using System;
using UnityEngine;

public enum VisualQuality
{
    Low,
    High
}

[DefaultExecutionOrder(-200)]
public sealed class VisualQualityController : MonoBehaviour
{
    private const string PreferenceKey = "VisualQuality";
    private static VisualQuality _current;
    private static bool _initialized;

    public static VisualQuality Current
    {
        get
        {
            EnsureInitialized();
            return _current;
        }
    }

    public static event Action<VisualQuality> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        EnsureInitialized();
        if (FindObjectOfType<VisualQualityController>() != null) return;
        GameObject instance = new GameObject("VisualQualityController");
        DontDestroyOnLoad(instance);
        instance.AddComponent<VisualQualityController>();
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    public static void SetQuality(VisualQuality quality)
    {
        EnsureInitialized();
        if (_current == quality) return;

        _current = quality;
        PlayerPrefs.SetInt(PreferenceKey, (int)quality);
        PlayerPrefs.Save();
        Changed?.Invoke(quality);
    }

    public static VisualQuality DefaultForPlatform(RuntimePlatform platform)
    {
        switch (platform)
        {
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.WindowsEditor:
            case RuntimePlatform.OSXPlayer:
            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.LinuxPlayer:
            case RuntimePlatform.LinuxEditor:
                return VisualQuality.High;
            default:
                return VisualQuality.Low;
        }
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        VisualQuality fallback = DefaultForPlatform(Application.platform);
        int saved = PlayerPrefs.GetInt(PreferenceKey, (int)fallback);
        _current = saved == (int)VisualQuality.High
            ? VisualQuality.High
            : VisualQuality.Low;
        _initialized = true;
    }
}
