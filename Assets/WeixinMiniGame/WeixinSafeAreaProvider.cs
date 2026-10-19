using System;
using UnityEngine;

public sealed class WeixinSafeAreaProvider
{
    public delegate bool AreaReader(Rect fallback, int width, int height, out Rect area);

    private readonly AreaReader _readArea;
    private readonly Func<float> _now;
    private Vector2Int _screen;
    private Rect _fallback;
    private Rect _area;
    private float _refreshAfter;
    private bool _hasArea;

    public WeixinSafeAreaProvider() : this(ReadPlatformArea, () => Time.unscaledTime) { }

    public WeixinSafeAreaProvider(AreaReader readArea, Func<float> now)
    {
        _readArea = readArea ?? throw new ArgumentNullException(nameof(readArea));
        _now = now ?? throw new ArgumentNullException(nameof(now));
    }

    public Rect Resolve(Rect fallback, int width, int height)
    {
        var size = new Vector2Int(width, height);
        fallback = UILayoutRules.NormalizeSafeArea(fallback, width, height);
        bool sameViewport = _hasArea && _screen == size && _fallback == fallback;
        float now = _now();
        if (sameViewport && now < _refreshAfter) return _area;
        Rect area = sameViewport ? _area : fallback;
        bool resolved = false;
        try { resolved = _readArea(fallback, width, height, out area); }
        catch (Exception) { area = sameViewport ? _area : fallback; }
        _screen = size;
        _fallback = fallback;
        _area = UILayoutRules.NormalizeSafeArea(area, width, height);
        _hasArea = true;
        // Startup fallbacks must recover without requiring a canvas resize.
        _refreshAfter = now + (resolved ? 5f : 1f);
        return _area;
    }

    private static bool ReadPlatformArea(Rect fallback, int width, int height, out Rect area)
    {
        area = fallback;
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
        var window = WeChatWASM.WX.GetWindowInfo();
        var capsule = WeChatWASM.WX.GetMenuButtonBoundingClientRect();
        if (window == null) return false;
        Rect logicalArea = window.safeArea != null
            ? Rect.MinMaxRect((float)window.safeArea.left, (float)window.safeArea.top,
                (float)window.safeArea.right, (float)window.safeArea.bottom) : new Rect();
        return WeChatSafeArea.TryConvert(logicalArea,
            capsule != null ? (float)capsule.bottom : 0f,
            (float)window.windowWidth, (float)window.windowHeight,
            width, height, fallback, out area);
#else
        return false;
#endif
    }
}
