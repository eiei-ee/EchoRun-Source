using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class EchoRunAccessibleText : MonoBehaviour
{
    [HideInInspector] public int baseFontSize;
    [HideInInspector] public Outline contrastOutline;
}

public static class EchoRunAccessibility
{
    private const string LargeTextKey = "EchoRunLargeText";
    private const string HighContrastKey = "EchoRunHighContrast";
    private const string ReducedMotionKey = "EchoRunReducedMotion";
    private const float LargeTextScale = 1.12f;
    private static bool _initialized;
    private static bool _largeText;
    private static bool _highContrast;
    private static bool _reducedMotion;

    public static bool LargeText { get { EnsureLoaded(); return _largeText; } }
    public static bool HighContrast { get { EnsureLoaded(); return _highContrast; } }
    public static bool ReducedMotion { get { EnsureLoaded(); return _reducedMotion; } }

    public static event Action Changed;

    public static void SetLargeText(bool enabled)
    {
        EnsureLoaded();
        _largeText = enabled;
        SetPreference(LargeTextKey, enabled);
    }

    public static void SetHighContrast(bool enabled)
    {
        EnsureLoaded();
        _highContrast = enabled;
        SetPreference(HighContrastKey, enabled);
    }

    public static void SetReducedMotion(bool enabled)
    {
        EnsureLoaded();
        _reducedMotion = enabled;
        SetPreference(ReducedMotionKey, enabled);
    }

    public static void Prepare(Text text)
    {
        if (text == null) return;
        EchoRunAccessibleText marker = text.GetComponent<EchoRunAccessibleText>();
        if (marker == null)
        {
            marker = text.gameObject.AddComponent<EchoRunAccessibleText>();
            marker.baseFontSize = Mathf.Max(1, text.fontSize);
        }
        Apply(text, marker);
    }

    public static void ApplyToHierarchy(Transform root)
    {
        if (root == null) return;
        Text[] texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++) Prepare(texts[i]);
        Canvas.ForceUpdateCanvases();
    }

    public static void SetBaseFontSize(Text text, int baseSize)
    {
        if (text == null) return;
        EchoRunAccessibleText marker = text.GetComponent<EchoRunAccessibleText>();
        if (marker == null)
            marker = text.gameObject.AddComponent<EchoRunAccessibleText>();
        marker.baseFontSize = Mathf.Max(1, baseSize);
        Apply(text, marker);
    }

    private static void Apply(Text text, EchoRunAccessibleText marker)
    {
        text.fontSize = Mathf.Max(1, Mathf.RoundToInt(marker.baseFontSize
            * (LargeText ? LargeTextScale : 1f)));

        // Dark action-button ink already contrasts with its bright surface.
        // A black outline on that ink fills CJK counters instead of helping readability.
        bool needsDarkOutline = HighContrast && text.color.grayscale >= 0.5f;
        if (needsDarkOutline && marker.contrastOutline == null)
        {
            marker.contrastOutline = text.gameObject.AddComponent<Outline>();
            marker.contrastOutline.effectColor = new Color(0f, 0f, 0f, 0.94f);
            marker.contrastOutline.effectDistance = new Vector2(1.5f, -1.5f);
            marker.contrastOutline.useGraphicAlpha = true;
        }
        if (marker.contrastOutline != null)
            marker.contrastOutline.enabled = needsDarkOutline;

        text.SetLayoutDirty();
        text.SetVerticesDirty();
    }

    private static void SetPreference(string key, bool enabled)
    {
        PlayerPrefs.SetInt(key, enabled ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    private static void EnsureLoaded()
    {
        if (_initialized) return;
        _largeText = PlayerPrefs.GetInt(LargeTextKey, 0) == 1;
        _highContrast = PlayerPrefs.GetInt(HighContrastKey, 0) == 1;
        _reducedMotion = PlayerPrefs.GetInt(ReducedMotionKey, 0) == 1;
        _initialized = true;
    }

    internal static void ReloadForTests()
    {
        _initialized = false;
        EnsureLoaded();
    }
}
