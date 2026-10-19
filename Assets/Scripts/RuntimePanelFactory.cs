using UnityEngine;
using UnityEngine.UI;

public static class RuntimePanelFactory
{
    private static Font _font;
    private static Sprite _roundedSprite;

    public static readonly Color Panel = EchoRunUITheme.PageSurface;
    public static readonly Color Raised = EchoRunUITheme.PageRaised;
    public static readonly Color Action = EchoRunUITheme.PageSelected;
    public static readonly Color Reward = EchoRunUITheme.PageEcho;
    public static readonly Color TextPrimary = EchoRunUITheme.PageInk;
    public static readonly Color TextMuted = EchoRunUITheme.PageMuted;

    public static GameObject PanelObject(string name, Transform parent,
        Vector2 anchor, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.color = color;
        ApplyRounded(image);
        if (Mathf.Min(size.x, size.y) >= 48f)
            UIEdgeTreatment.Surface(image, EchoRunUITheme.PageRule);
        return go;
    }

    public static Text Text(string name, Transform parent, string value,
        int size, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = GetFont();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.resizeTextForBestFit = false;
        text.supportRichText = true;
        text.raycastTarget = false;
        EchoRunAccessibility.Prepare(text);
        return text;
    }

    public static Button Button(string name, Transform parent, string label,
        Vector2 anchor, Vector2 size, Color color, int fontSize = 24)
    {
        if (UsesTouchLayout()) size.y = Mathf.Max(size.y, 104f);
        GameObject go = PanelObject(name, parent, anchor, size, color);
        Button button = go.AddComponent<Button>();
        UIEdgeTreatment.Surface(go.GetComponent<Image>(),
            EchoRunUITheme.PageRule, true);
        Text text = Text("Label", go.transform, label, fontSize,
            TextAnchor.MiddleCenter, TextPrimary);
        text.fontStyle = FontStyle.Bold;
        Stretch(text.rectTransform);
        UIEdgeTreatment.ButtonStates(button);
        button.onClick.AddListener(() => AudioManager.Instance?.PlayUIClick());
        return button;
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 size,
        Vector2 offset)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
    }

    public static Vector2 TouchButtonSize(Vector2 requested, bool portrait)
    {
        return UILayoutRules.EnsureTouchButtonSize(
            requested, UsesTouchLayout(), portrait);
    }

    public static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    public static void RefreshText(Transform root)
    {
        if (root == null) return;
        Text[] texts = root.GetComponentsInChildren<Text>(true);
        foreach (Text text in texts)
        {
            text.SetLayoutDirty();
            text.SetVerticesDirty();
        }
        Canvas.ForceUpdateCanvases();
    }

    private static Font GetFont()
    {
        if (_font != null) return _font;
        _font = Resources.Load<Font>("Fonts/EchoRunSansSC-Regular");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _font;
    }

    private static void ApplyRounded(Image image)
    {
        if (_roundedSprite == null)
        {
            const int size = 64;
            const float radius = 15f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "RuntimePanelRoundedTexture";
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x, x - (size - radius - 1f), 0f);
                float dy = Mathf.Max(radius - y, y - (size - radius - 1f), 0f);
                float alpha = Mathf.Clamp01(radius + 0.75f - Mathf.Sqrt(dx * dx + dy * dy));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply(false, true);
            _roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(16f, 16f, 16f, 16f));
        }
        image.sprite = _roundedSprite;
        image.type = Image.Type.Sliced;
    }

    private static bool UsesTouchLayout()
    {
#if MINIGAME_SUBPLATFORM_WEIXIN
        return true;
#else
        return Application.isMobilePlatform || Input.touchSupported;
#endif
    }
}
