using System;
using UnityEngine;

/// <summary>Platform supplies a resolved area; shared UI never imports an SDK.</summary>
public static class WeChatSafeArea
{
    private static Func<Rect, int, int, Rect> _provider;

    public static void Register(Func<Rect, int, int, Rect> provider) { _provider = provider; }
    public static void Unregister(Func<Rect, int, int, Rect> provider)
    { if (_provider == provider) _provider = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetProvider() { _provider = null; }

    public static Rect Resolve(Rect fallback, int width, int height)
    { return _provider != null ? _provider(fallback, width, height) : fallback; }

    /// <summary>Pure top-origin logical viewport to bottom-origin Unity pixels.</summary>
    public static Rect Convert(Rect logicalArea, float capsuleBottom,
        float viewportWidth, float viewportHeight, int width, int height, Rect fallback)
    {
        TryConvert(logicalArea, capsuleBottom, viewportWidth, viewportHeight,
            width, height, fallback, out Rect area);
        return area;
    }

    /// <summary>
    /// Returns true only for a usable platform safe area. A missing or invalid
    /// area still uses the Unity fallback and avoids a valid capsule, but must
    /// be retried. All logical coordinates must share the viewport's origin.
    /// </summary>
    public static bool TryConvert(Rect logicalArea, float capsuleBottom,
        float viewportWidth, float viewportHeight, int width, int height,
        Rect fallback, out Rect area)
    {
        area = UILayoutRules.NormalizeSafeArea(fallback, width, height);
        if (!IsFinite(viewportWidth) || !IsFinite(viewportHeight)
            || viewportWidth <= 0f || viewportHeight <= 0f
            || width <= 0 || height <= 0)
            return false;
        float sx = width / viewportWidth;
        float sy = height / viewportHeight;
        const float canvasTolerance = 0.5f; // Match UILayoutRules' physical-pixel tolerance.
        bool platformAreaValid = IsUsableArea(logicalArea, viewportWidth, viewportHeight);
        if (platformAreaValid)
        {
            area = Rect.MinMaxRect(logicalArea.xMin * sx,
                height - logicalArea.yMax * sy,
                logicalArea.xMax * sx, height - logicalArea.yMin * sy);
        }
        if (IsFinite(capsuleBottom) && capsuleBottom > 0f
            && capsuleBottom < viewportHeight)
        {
            float top = Mathf.Min(area.yMax, height - (capsuleBottom + 8f) * sy);
            Rect avoidingCapsule = Rect.MinMaxRect(area.xMin, area.yMin, area.xMax, top);
            if (IsUsableArea(avoidingCapsule, width, height, canvasTolerance)) area = avoidingCapsule;
            else platformAreaValid = false;
        }
        if (!IsUsableArea(area, width, height, canvasTolerance))
        {
            area = UILayoutRules.NormalizeSafeArea(fallback, width, height);
            return false;
        }
        area = UILayoutRules.NormalizeSafeArea(area, width, height);
        return platformAreaValid;
    }

    private static bool IsUsableArea(Rect area, float width, float height, float tolerance = 0f)
    {
        return IsFinite(area.x) && IsFinite(area.y)
               && IsFinite(area.width) && IsFinite(area.height)
               && area.xMin >= -tolerance && area.yMin >= -tolerance
               && area.xMax <= width + tolerance && area.yMax <= height + tolerance
               && area.width >= width * 0.5f && area.height >= height * 0.5f;
    }

    private static bool IsFinite(float value)
    { return !float.IsNaN(value) && !float.IsInfinity(value); }
}
