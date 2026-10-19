using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit, repeatable authoring step for the production social UI prefab.</summary>
public static class AsyncEchoPanelPrefabBuilder
{
    private const string Path = "Assets/Resources/UI/AsyncEchoSheet.prefab";
    private static Font _font;
    private static Sprite _round;

    [MenuItem("Tools/EchoRun/Rebuild Friend Challenge UI")]
    public static void Build()
    {
        _font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/EchoRunSansSC-Regular.otf");
        if (_font == null) throw new System.InvalidOperationException("The bundled CJK UI font is required.");
        _round = LoadRounded();
        GameObject root = Box("AsyncEchoSheet", null, AsyncEchoPanelView.Backdrop, false);
        try
        {
            var view = root.AddComponent<AsyncEchoPanelView>();
            Stretch((RectTransform)root.transform);
            RectTransform frame = (RectTransform)Box("Frame", root.transform, Color.clear, false).transform;
            frame.anchorMin = frame.anchorMax = new Vector2(.5f, .5f);
            frame.sizeDelta = new Vector2(1016, 1320);
            view.frame = frame;
            view.heading = Label("AsyncEchoTitle", frame, "好友挑战", 56, AsyncEchoPanelView.Foreground, true);
            Top(view.heading.rectTransform, 32, 16, 208, 104);
            view.close = Button("CloseAsyncEcho", frame, "关闭", AsyncEchoPanelView.Raised, AsyncEchoPanelView.Muted, 32);
            RectTransform close = (RectTransform)view.close.transform;
            close.anchorMin = close.anchorMax = Vector2.one;
            close.pivot = Vector2.one;
            close.sizeDelta = new Vector2(144, 104);
            close.anchoredPosition = new Vector2(-32, -20);

            GameObject tabs = Box("Tabs", frame, Color.clear, false);
            Top((RectTransform)tabs.transform, 32, 136, 32, 100);
            view.challengeTab = Button("AsyncEchoChallengeTab", tabs.transform, "好友邀请", Color.clear, AsyncEchoPanelView.Foreground, 36);
            view.boardTab = Button("AsyncEchoBoard", tabs.transform, "挑战榜", Color.clear, AsyncEchoPanelView.Muted, 36);
            Half((RectTransform)view.challengeTab.transform, 0, 6);
            Half((RectTransform)view.boardTab.transform, 1, 6);
            view.challengeTabFill = TabRule(view.challengeTab.transform);
            view.boardTabFill = TabRule(view.boardTab.transform);
            view.challengeTabFill.color = AsyncEchoPanelView.Echo;
            view.boardTabFill.color = Color.clear;

            RectTransform challenge = Rect("ChallengePage", frame);
            PageBounds(challenge);
            view.challengePage = challenge.gameObject;
            view.invitationSummary = (RectTransform)Box("InvitationSummary", challenge, Color.clear, false).transform;
            Top(view.invitationSummary, 0, 0, 0, 330);
            GameObject rule = Box("InvitationStateRule", view.invitationSummary, AsyncEchoPanelView.Echo, false);
            RectTransform ruleRect = (RectTransform)rule.transform;
            ruleRect.anchorMin = Vector2.zero;
            ruleRect.anchorMax = new Vector2(0, 1);
            ruleRect.offsetMin = new Vector2(0, 24);
            ruleRect.offsetMax = new Vector2(5, -16);
            view.invitationRule = rule.GetComponent<Image>();
            view.invitationRule.raycastTarget = false;
            view.invitationStatus = Label("InvitationStatus", view.invitationSummary, "好友邀请", 34, AsyncEchoPanelView.Echo);
            Top(view.invitationStatus.rectTransform, 26, 4, 270, 68);
            view.invitationTitle = Label("InvitationTitle", view.invitationSummary, "好友影子", 48, AsyncEchoPanelView.Foreground, true);
            Top(view.invitationTitle.rectTransform, 26, 62, 270, 100);
            GameObject generationBadge = Box("GenerationBadge", view.invitationSummary, AsyncEchoPanelView.Selected);
            RectTransform badge = (RectTransform)generationBadge.transform;
            badge.anchorMin = badge.anchorMax = Vector2.one;
            badge.pivot = Vector2.one;
            badge.sizeDelta = new Vector2(218, 104);
            badge.anchoredPosition = new Vector2(0, -16);
            view.generation = Label("Generation", badge, "第 1 代", 44, AsyncEchoPanelView.Foreground);
            view.generation.alignment = TextAnchor.MiddleCenter;
            Stretch(view.generation.rectTransform, 12);
            view.invitationDescription = Label("InvitationDescription", view.invitationSummary,
                "这是好友留下的固定跑法。\n本局不学习，也不改变你的回声。", 38, AsyncEchoPanelView.Muted);
            view.invitationDescription.rectTransform.anchorMin = Vector2.zero;
            view.invitationDescription.rectTransform.anchorMax = Vector2.one;
            view.invitationDescription.rectTransform.offsetMin = new Vector2(26, 12);
            view.invitationDescription.rectTransform.offsetMax = new Vector2(0, -170);

            view.actionDock = Box("ActionDock", challenge, Color.clear, false);
            Top((RectTransform)view.actionDock.transform, 0, 346, 0, 236);
            view.start = Button("AsyncEchoBegin", view.actionDock.transform, "开始挑战", AsyncEchoPanelView.Primary, AsyncEchoPanelView.Ink, 46, true);
            view.offline = Button("AsyncEchoOffline", view.actionDock.transform, "先跑单机", AsyncEchoPanelView.Raised, AsyncEchoPanelView.Foreground, 36);
            view.ArrangeActions(true);

            Transform extras = Page("ChallengeOptions", challenge, out view.challengeScroll, false);
            ((RectTransform)view.challengeScroll.transform).offsetMax = new Vector2(0, -510);
            GameObject divider = Box("OptionsDivider", extras, AsyncEchoPanelView.Selected, false);
            divider.GetComponent<Image>().raycastTarget = false;
            Height(divider, 2);
            view.reportCard = Stack("ReportCard", extras, AsyncEchoPanelView.Raised, 20, 10);
            view.reportStatus = FlowLabel("ReportStatus", view.reportCard.transform, "挑战成绩 · 已确认", 36, AsyncEchoPanelView.Foreground);
            view.retry = Button("AsyncEchoRetryReport", view.reportCard.transform, "重新确认上传", AsyncEchoPanelView.Selected, AsyncEchoPanelView.Foreground, 34);
            Height(view.retry.gameObject, 124);
            view.publishCard = Stack("PublishCard", extras, Color.clear, 0, 10);
            view.publishToggle = Button("AsyncEchoPublishToggle", view.publishCard.transform, "我的影子 · 展开", AsyncEchoPanelView.Raised, AsyncEchoPanelView.Foreground, 36);
            Height(view.publishToggle.gameObject, 116);
            view.publishStatus = FlowLabel("PublishStatus", view.publishCard.transform, "跑完单机后，影子会自动发布。", 32, AsyncEchoPanelView.Muted);
            view.publishDetails = Rect("PublishActions", view.publishCard.transform).gameObject;
            Height(view.publishDetails, 128);
            view.publish = Button("AsyncEchoPublish", view.publishDetails.transform, "更新影子", AsyncEchoPanelView.Selected, AsyncEchoPanelView.Foreground, 34);
            view.share = Button("AsyncEchoShare", view.publishDetails.transform, "分享卡片", AsyncEchoPanelView.Echo, AsyncEchoPanelView.Surface, 34);
            Half((RectTransform)view.publish.transform, 0, 0);
            Half((RectTransform)view.share.transform, 1, 0);
            view.publishDetails.SetActive(false);
            view.recent = Button("AsyncEchoRecent", extras, "最近邀请", AsyncEchoPanelView.Raised, AsyncEchoPanelView.Muted, 34);
            Height(view.recent.gameObject, 124);
            FlowLabel("SaveAssurance", extras, "每份影子都有独立的挑战榜。\n本局不影响你的回声成长和本地纪录。", 32, AsyncEchoPanelView.Muted);

            Transform board = Page("BoardPage", frame, out view.boardScroll, true);
            view.boardPage = view.boardScroll.gameObject;
            FlowLabel("BoardScope", board, "当前影子 · 最佳距离排名", 36, AsyncEchoPanelView.Foreground, true);
            FlowLabel("BoardNote", board, "只记录参与这份影子挑战的成绩。", 32, AsyncEchoPanelView.Muted);
            view.rows = Rect("LeaderboardRows", board);
            Vertical(view.rows.gameObject, 10, 0);
            view.rowTemplate = MakeRow(view.rows);
            view.rowTemplate.gameObject.SetActive(false);
            view.emptyBoard = Stack("EmptyBoard", board, AsyncEchoPanelView.Raised, 30, 14);
            view.boardMessage = FlowLabel("BoardMessage", view.emptyBoard.transform, "还没有挑战成绩", 42, AsyncEchoPanelView.Foreground, true);
            view.boardDetail = FlowLabel("BoardDetail", view.emptyBoard.transform, "完成第一场挑战，留下你的纪录。", 36, AsyncEchoPanelView.Muted);
            view.reload = Button("AsyncEchoReloadBoard", board, "刷新挑战榜", AsyncEchoPanelView.Selected, AsyncEchoPanelView.Foreground, 36);
            Height(view.reload.gameObject, 132);

            view.boardPage.SetActive(false);
            view.reportCard.SetActive(false);
            view.recent.gameObject.SetActive(false);
            // Runtime accessibility markers are never serialized into the prefab.
            Directory.CreateDirectory("Assets/Resources/UI");
            PrefabUtility.SaveAsPrefabAsset(root, Path);
            AssetDatabase.SaveAssets();
            Debug.Log("ASYNC_ECHO_UI_PREFAB_BUILT " + Path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static AsyncEchoLeaderboardRow MakeRow(Transform parent)
    {
        GameObject go = Box("LeaderboardRow", parent, AsyncEchoPanelView.Raised);
        Height(go, 174);
        var row = go.AddComponent<AsyncEchoLeaderboardRow>();
        row.surface = go.GetComponent<Image>();
        GameObject badge = Box("RankBadge", go.transform, AsyncEchoPanelView.Surface);
        RectTransform rect = (RectTransform)badge.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
        rect.pivot = new Vector2(0, .5f);
        rect.sizeDelta = new Vector2(80, 94);
        rect.anchoredPosition = new Vector2(16, 0);
        row.rankBadge = badge.GetComponent<Image>();
        row.rank = Label("Rank", badge.transform, "01", 36, AsyncEchoPanelView.Muted);
        row.rank.alignment = TextAnchor.MiddleCenter;
        Stretch(row.rank.rectTransform);
        row.runner = Label("Runner", go.transform, "挑战者", 36, AsyncEchoPanelView.Foreground);
        row.runner.rectTransform.anchorMin = Vector2.zero;
        row.runner.rectTransform.anchorMax = new Vector2(.54f, 1);
        row.runner.rectTransform.offsetMin = new Vector2(116, 12);
        row.runner.rectTransform.offsetMax = new Vector2(-12, -12);
        row.distance = Label("Distance", go.transform, "152.2 米", 44, AsyncEchoPanelView.Foreground);
        row.distance.alignment = TextAnchor.MiddleRight;
        row.distance.rectTransform.anchorMin = new Vector2(.54f, .43f);
        row.distance.rectTransform.anchorMax = Vector2.one;
        row.distance.rectTransform.offsetMin = new Vector2(0, 0);
        row.distance.rectTransform.offsetMax = new Vector2(-24, -8);
        row.lead = Label("Lead", go.transform, "领先 -46.6 米", 32, AsyncEchoPanelView.Muted);
        row.lead.alignment = TextAnchor.MiddleRight;
        row.lead.rectTransform.anchorMin = new Vector2(.54f, 0);
        row.lead.rectTransform.anchorMax = new Vector2(1, .43f);
        row.lead.rectTransform.offsetMin = new Vector2(0, 8);
        row.lead.rectTransform.offsetMax = new Vector2(-24, 0);
        return row;
    }

    private static Transform Page(string name, Transform parent, out ScrollRect scroll, bool withPageBounds)
    {
        RectTransform viewport = Rect(name, parent);
        if (withPageBounds) PageBounds(viewport); else Stretch(viewport);
        // Real hits on whitespace must bubble to this page's ScrollRect.
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36;
        scroll.viewport = viewport;
        RectTransform content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1);
        content.sizeDelta = Vector2.zero;
        Vertical(content.gameObject, 16, 2);
        scroll.content = content;
        return content;
    }

    private static void PageBounds(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(32, 24);
        rect.offsetMax = new Vector2(-32, -276);
    }

    private static GameObject Stack(string name, Transform parent, Color color, int padding, int spacing)
    { GameObject go = Box(name, parent, color); Vertical(go, spacing, padding); return go; }

    private static void Vertical(GameObject go, int spacing, int padding)
    {
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = spacing;
        layout.padding = new RectOffset(padding, padding, padding, padding);
        go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static Text FlowLabel(string name, Transform parent, string value, int size, Color color, bool bold = false)
    { return Label(name, parent, value, size, color, bold); }

    private static Text Label(string name, Transform parent, string value, int size, Color color, bool bold = false)
    {
        Text text = Rect(name, parent).gameObject.AddComponent<Text>();
        text.font = _font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.color = color;
        text.lineSpacing = 1.08f;
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;
        text.raycastTarget = false;
        return text;
    }

    private static Button Button(string name, Transform parent, string label, Color color, Color ink, int size, bool bold = false)
    {
        GameObject go = Box(name, parent, color);
        var button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1.06f, 1.04f, 1.08f);
        colors.pressedColor = new Color(.82f, .78f, .86f);
        colors.disabledColor = new Color(.68f, .63f, .73f, .7f);
        colors.fadeDuration = .1f;
        button.colors = colors;
        Text text = Label("Label", go.transform, label, size, ink, bold);
        text.alignment = TextAnchor.MiddleCenter;
        Stretch(text.rectTransform, 14);
        return button;
    }

    private static GameObject Box(string name, Transform parent, Color color, bool rounded = true)
    {
        GameObject go = Rect(name, parent).gameObject;
        var image = go.AddComponent<Image>();
        image.color = color;
        if (rounded) { image.sprite = _round; image.type = Image.Type.Sliced; }
        return go;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Height(GameObject go, float value)
    { var layout = go.AddComponent<LayoutElement>(); layout.minHeight = layout.preferredHeight = value; }
    private static void Stretch(RectTransform r, float pad = 0)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(pad, pad); r.offsetMax = new Vector2(-pad, -pad); }
    private static void Top(RectTransform r, float left, float top, float right, float height)
    { r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, 1); r.offsetMin = new Vector2(left, -top-height); r.offsetMax = new Vector2(-right, -top); }
    private static void Half(RectTransform r, int column, float pad)
    { r.anchorMin = new Vector2(column * .5f, 0); r.anchorMax = new Vector2((column + 1) * .5f, 1); r.offsetMin = new Vector2(pad + (column == 1 ? 10 : 0), pad); r.offsetMax = new Vector2(-pad - (column == 0 ? 10 : 0), -pad); }

    private static Image TabRule(Transform parent)
    {
        GameObject rule = Box("ActiveRule", parent, AsyncEchoPanelView.Echo, false);
        Image image = rule.GetComponent<Image>();
        image.raycastTarget = false;
        RectTransform rect = (RectTransform)rule.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
        rect.sizeDelta = new Vector2(190f, 7f);
        rect.anchoredPosition = new Vector2(0, 4f);
        return image;
    }

    private static Sprite LoadRounded()
    {
        const string path = "Assets/Resources/UI/ChallengeRounded.png";
        if (!File.Exists(path))
        {
            Directory.CreateDirectory("Assets/Resources/UI");
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(19f - x, x - 44f, 0f), dy = Mathf.Max(19f - y, y - 44f, 0f);
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(19.5f - Mathf.Sqrt(dx*dx + dy*dy)));
            }
            tex.SetPixels(pixels); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteBorder = new Vector4(22, 22, 22, 22);
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
