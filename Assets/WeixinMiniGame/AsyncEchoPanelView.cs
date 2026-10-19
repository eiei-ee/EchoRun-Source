using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Serialized presentation only. Cloud and run state belong to AsyncEchoPanel.</summary>
public sealed class AsyncEchoPanelView : MonoBehaviour
{
    public RectTransform frame, invitationSummary;
    public GameObject challengePage, boardPage, publishCard, publishDetails, reportCard, emptyBoard, actionDock;
    public Text heading, invitationStatus, invitationTitle, invitationDescription, generation, publishStatus;
    public Text reportStatus, boardMessage, boardDetail;
    public Button close, challengeTab, boardTab, start, offline, publish, share, publishToggle, recent, retry, reload;
    public Image challengeTabFill, boardTabFill, invitationRule;
    public ScrollRect challengeScroll, boardScroll;
    public RectTransform rows;
    public AsyncEchoLeaderboardRow rowTemplate;
    private readonly List<AsyncEchoLeaderboardRow> _rows = new List<AsyncEchoLeaderboardRow>();
    private bool _showBoard, _publishingExpanded, _fitting;
    private bool _paletteApplied;

    // One project palette; the platform UI does not invent another theme.
    public static Color Backdrop => EchoRunUITheme.PageBackdrop;
    public static Color Surface => EchoRunUITheme.PageSurface;
    public static Color Raised => EchoRunUITheme.PageRaised;
    public static Color Selected => EchoRunUITheme.PageSelected;
    public static Color Primary => EchoRunUITheme.ActionAccent;
    public static Color Foreground => EchoRunUITheme.PageInk;
    public static Color Muted => EchoRunUITheme.PageMuted;
    public static Color Echo => EchoRunUITheme.PageEcho;
    public static Color Danger => EchoRunUITheme.PageDanger;
    public static Color Ink => EchoRunUITheme.Ink;

    public void ShowBoard(bool board)
    {
        bool changed = _showBoard != board;
        _showBoard = board;
        challengePage.SetActive(!board);
        boardPage.SetActive(board);
        challengeTabFill.color = board ? Color.clear : Echo;
        boardTabFill.color = board ? Echo : Color.clear;
        challengeTab.GetComponentInChildren<Text>().color = board ? Muted : Foreground;
        boardTab.GetComponentInChildren<Text>().color = board ? Foreground : Muted;
        if (changed)
        {
            ScrollRect scroll = board ? boardScroll : challengeScroll;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }
        FitFrame();
    }

    public void TogglePublishing()
    {
        _publishingExpanded = !_publishingExpanded;
        publishDetails.SetActive(_publishingExpanded);
        publishToggle.GetComponentInChildren<Text>().text = _publishingExpanded ? "我的影子 · 收起" : "我的影子 · 展开";
        if (!_publishingExpanded && EventSystem.current != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(publishDetails.transform))
                EventSystem.current.SetSelectedGameObject(publishToggle.gameObject);
        }
        FitFrame();
    }

    public void ArrangeActions(bool hasChallenge)
    {
        var primary = (RectTransform)start.transform;
        var solo = (RectTransform)offline.transform;
        primary.anchorMin = new Vector2(0, .44f);
        primary.anchorMax = Vector2.one;
        primary.offsetMin = Vector2.zero;
        primary.offsetMax = Vector2.zero;
        solo.anchorMin = Vector2.zero;
        solo.anchorMax = hasChallenge ? new Vector2(1, .42f) : Vector2.one;
        solo.offsetMin = Vector2.zero;
        solo.offsetMax = Vector2.zero;
        offline.GetComponent<Image>().color = hasChallenge ? Color.clear : Primary;
        offline.GetComponentInChildren<Text>().color = hasChallenge ? Foreground : Ink;
    }

    public void SetInvitationTone(bool ready, bool loading, bool failed)
    {
        Color tone = failed ? Danger : Echo;
        invitationRule.color = tone;
        invitationStatus.color = tone;
        generation.color = ready ? Foreground : Muted;
        invitationDescription.color = failed ? Danger : Muted;
    }

    public void SetLeaderboard(AsyncEchoOperationState state, AsyncEchoLeaderboardEntry[] items, bool hasBoard)
    {
        bool hasRows = hasBoard && state == AsyncEchoOperationState.Succeeded && items != null && items.Length > 0;
        int count = hasRows ? Mathf.Min(items.Length, 50) : 0;
        emptyBoard.SetActive(count == 0);
        for (int i = 0; i < count; i++)
        {
            if (i == _rows.Count) _rows.Add(Instantiate(rowTemplate, rows));
            _rows[i].gameObject.SetActive(true);
            _rows[i].Bind(items[i]);
        }
        for (int i = count; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
        bool failed = hasBoard && state != AsyncEchoOperationState.Idle
            && state != AsyncEchoOperationState.Pending && state != AsyncEchoOperationState.Succeeded;
        boardMessage.color = failed ? Danger : Foreground;
        if (!hasBoard)
        {
            boardMessage.text = "还没有可查看的挑战榜";
            boardDetail.text = "打开好友邀请，或发布自己的影子。\n每份影子都有独立的挑战榜。";
        }
        else if (state == AsyncEchoOperationState.Idle)
        {
            boardMessage.text = "这份影子的挑战榜";
            boardDetail.text = "刷新后查看最佳距离。";
        }
        else if (state == AsyncEchoOperationState.Pending)
        {
            boardMessage.text = "正在读取成绩";
            boardDetail.text = "稍等片刻，成绩正在返回。";
        }
        else if (state == AsyncEchoOperationState.Succeeded)
        {
            boardMessage.text = "还没有挑战成绩";
            boardDetail.text = "完成第一场挑战，留下你的纪录。";
        }
        else
        {
            boardMessage.text = "暂时无法读取成绩";
            boardDetail.text = "网络暂时不可用。\n可以刷新重试，也可以继续单机。";
        }
        reload.interactable = hasBoard && state != AsyncEchoOperationState.Pending;
    }

    private void OnEnable()
    {
        ApplyPagePalette();
        EchoRunAccessibility.ApplyToHierarchy(transform);
        FitFrame();
    }

    // Existing serialized sheets carry the previous navy palette. Retint them
    // when opened so a code update and an editor prefab rebuild look the same.
    private void ApplyPagePalette()
    {
        if (_paletteApplied) return;
        _paletteApplied = true;
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            Color source = graphic.color;
            Color32 old = source;
            Color target;
            if (SameRgb(old, EchoRunUITheme.Backdrop))
                target = graphic is Text ? Ink : Backdrop;
            else if (SameRgb(old, EchoRunUITheme.Surface)) target = Surface;
            else if (SameRgb(old, EchoRunUITheme.SurfaceRaised)) target = Raised;
            else if (SameRgb(old, EchoRunUITheme.SurfaceSelected)) target = Selected;
            else if (SameRgb(old, EchoRunUITheme.TextPrimary)) target = Foreground;
            else if (SameRgb(old, EchoRunUITheme.TextMuted)) target = Muted;
            else if (SameRgb(old, EchoRunUITheme.Echo)) target = Echo;
            else if (SameRgb(old, EchoRunUITheme.Danger)) target = Danger;
            else continue;
            target.a = source.a;
            graphic.color = target;
        }
        if (share != null)
        {
            Text shareLabel = share.GetComponentInChildren<Text>();
            if (shareLabel != null) shareLabel.color = Surface;
        }
    }

    private static bool SameRgb(Color32 actual, Color expected)
    {
        Color32 sample = expected;
        return actual.r == sample.r && actual.g == sample.g
            && actual.b == sample.b;
    }

    private void OnRectTransformDimensionsChange() { FitFrame(); }

    private void FitFrame()
    {
        if (_fitting || frame == null || invitationSummary == null || challengeScroll == null || actionDock == null) return;
        _fitting = true;
        try
        {
            Rect available = ((RectTransform)transform).rect;
            bool large = Application.isPlaying && EchoRunAccessibility.LargeText;
            bool portrait = available.height > available.width;
            float desiredHeight = portrait ? available.height - 32f
                : (large ? 1400f : 1320f);
            frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Min(portrait ? available.width - 28f : 1160f,
                    Mathf.Max(0f, available.width - 28f)));
            frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Min(desiredHeight, Mathf.Max(0f, available.height - 32f)));
            float summaryTop = 0f;
            float summaryHeight = large ? 370f : 300f;
            float actionHeight = portrait ? 236f : 144f;
            Top(invitationSummary, summaryTop, summaryHeight);
            Top((RectTransform)actionDock.transform,
                summaryTop + summaryHeight + 12f, actionHeight);
            RectTransform scroll = (RectTransform)challengeScroll.transform;
            scroll.offsetMax = new Vector2(0,
                -summaryTop - summaryHeight - actionHeight - 28f);
        }
        finally { _fitting = false; }
    }

    private static void Top(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, 1);
        rect.offsetMin = new Vector2(0, -top - height);
        rect.offsetMax = new Vector2(0, -top);
    }
}
