using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Platform presenter; the authored view never owns cloud state or settlement.</summary>
public sealed class AsyncEchoPanel : MonoBehaviour
{
    private WeixinMiniGameRuntime _runtime;
    private AsyncEchoCloud _cloud;
    private AsyncEchoPanelView _view;
    private Button _entry;
    private Text _toast;
    private GameObject _toastPlate;
    private double _toastUntil;
    private bool _open, _showBoard, _wasInvitationVisible;
    private string _visibleInvitationKey;
    private int _lastGameState = -1;
    private readonly Dictionary<CanvasGroup, bool> _blockedGroups = new Dictionary<CanvasGroup, bool>();
    private GameObject _previousSelection;
    private bool _modalInput;

    public void Initialize(WeixinMiniGameRuntime runtime, AsyncEchoCloud cloud)
    {
        _runtime = runtime;
        _cloud = cloud;
        RuntimePanelFactory.Stretch((RectTransform)transform);
        GameObject prefabAsset = Resources.Load<GameObject>("UI/AsyncEchoSheet");
        var prefab = prefabAsset != null ? prefabAsset.GetComponent<AsyncEchoPanelView>() : null;
        // Optional presentation assets cannot prevent the single-player game starting.
        if (prefab == null) { enabled = false; return; }
        _entry = RuntimePanelFactory.Button("AsyncEchoEntry", transform, "好友挑战",
            Vector2.one, new Vector2(234, 104),
            EchoRunUITheme.WithAlpha(AsyncEchoPanelView.Surface, 0.88f), 30);
        _entry.GetComponentInChildren<Text>().fontStyle = FontStyle.Normal;
        var entryRect = (RectTransform)_entry.transform;
        entryRect.pivot = Vector2.one;
        entryRect.anchoredPosition = new Vector2(-24, -52);
        _entry.onClick.AddListener(Open);
        _view = Instantiate(prefab, transform, false);
        _view.name = "AsyncEchoSheet";
        RuntimePanelFactory.Stretch((RectTransform)_view.transform);
        _view.close.onClick.AddListener(Close);
        _view.challengeTab.onClick.AddListener(() => { _showBoard = false; Refresh(); });
        _view.boardTab.onClick.AddListener(ShowBoard);
        _view.reload.onClick.AddListener(ShowBoard);
        _view.publish.onClick.AddListener(() => _runtime.PublishLocal());
        _view.publishToggle.onClick.AddListener(_view.TogglePublishing);
        _view.share.onClick.AddListener(() => _runtime.SharePublished());
        _view.start.onClick.AddListener(() => _runtime.BeginChallenge());
        _view.offline.onClick.AddListener(() => _runtime.BeginOffline());
        _view.recent.onClick.AddListener(() =>
            { _showBoard = false; _cloud.OpenRecentInvitation(); Refresh(); });
        _view.retry.onClick.AddListener(() => _cloud.RetryReport());
        foreach (Button button in _view.GetComponentsInChildren<Button>(true))
            button.onClick.AddListener(() => AudioManager.Instance?.PlayUIClick());
        _toastPlate = RuntimePanelFactory.PanelObject("AsyncEchoToastPlate", transform,
            new Vector2(0.5f, 0.12f), new Vector2(850, 144), AsyncEchoPanelView.Selected);
        _toastPlate.GetComponent<Image>().raycastTarget = false;
        var toastRect = (RectTransform)_toastPlate.transform;
        toastRect.anchorMin = new Vector2(0.05f, 0.12f);
        toastRect.anchorMax = new Vector2(0.95f, 0.12f);
        toastRect.sizeDelta = new Vector2(0, 130);
        _toast = RuntimePanelFactory.Text("AsyncEchoToast", _toastPlate.transform, "", 34,
            TextAnchor.MiddleCenter, AsyncEchoPanelView.Foreground);
        _toast.raycastTarget = false;
        RuntimePanelFactory.Stretch(_toast.rectTransform, 18);
        _toastPlate.SetActive(false);
        _cloud.Changed += Refresh;
        Refresh();
    }

    public void Open()
    {
        _open = true;
        var game = GameManager.Instance;
        _showBoard = game != null && game.State == GameState.GameOver && game.IsAsyncChallengeRun;
        if (_showBoard) _runtime.OpenBoard();
        Refresh();
    }
    public void ShowBoard() { _showBoard = true; _runtime.OpenBoard(); Refresh(); }
    private void Close()
    {
        _open = false;
        _cloud.DismissInvitation();
        Refresh();
    }

    private void Update()
    {
        if (_cloud == null || _view == null) return;
        GameManager game = GameManager.Instance;
        int gameState = game != null ? (int)game.State : -1;
        if (_lastGameState != gameState) { _lastGameState = gameState; Refresh(); }
        ApplyVisibility();
        _toastPlate.SetActive(Time.realtimeSinceStartupAsDouble < _toastUntil);
    }

    private void ApplyVisibility()
    {
        GameManager game = GameManager.Instance;
        bool allowed = game != null && (game.State == GameState.Menu || game.State == GameState.GameOver);
        bool home = game == null || game.State != GameState.Menu || MenuScreenRouter.Instance == null
            || MenuScreenRouter.Instance.IsHome;
        if (!allowed) _open = false;
        _entry.gameObject.SetActive(_cloud.IsAvailable && allowed && home && !_open);
        bool visible = _open && allowed && home;
        _view.gameObject.SetActive(visible);
        SetModalInput(visible);
    }

    private void SetModalInput(bool visible)
    {
        if (_modalInput == visible) return;
        _modalInput = visible;
        if (visible)
        {
            _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (transform.parent != null)
            {
                foreach (Transform sibling in transform.parent)
                {
                    if (sibling == transform) continue;
                    var group = sibling.GetComponent<CanvasGroup>();
                    if (group == null) group = sibling.gameObject.AddComponent<CanvasGroup>();
                    _blockedGroups[group] = group.interactable;
                    group.interactable = false;
                }
            }
            EventSystem.current?.SetSelectedGameObject(_view.close.gameObject);
        }
        else
        {
            foreach (var item in _blockedGroups)
                if (item.Key != null) item.Key.interactable = item.Value;
            _blockedGroups.Clear();
            EventSystem.current?.SetSelectedGameObject(
                _previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
            _previousSelection = null;
        }
    }

    private void Refresh()
    {
        if (_cloud == null || _view == null) return;
        string invitationKey = _cloud.RecentInvitation?.Key;
        if (_cloud.InvitationVisible && (!_wasInvitationVisible || _visibleInvitationKey != invitationKey))
        { _open = true; _showBoard = false; }
        _visibleInvitationKey = _cloud.InvitationVisible ? invitationKey : null;
        _wasInvitationVisible = _cloud.InvitationVisible;
        GameManager game = GameManager.Instance;
        bool menu = game != null && game.State == GameState.Menu;
        bool asyncResult = game != null && game.State == GameState.GameOver && game.IsAsyncChallengeRun;
        bool ready = _cloud.InvitationVisible && _cloud.CurrentSnapshot != null;
        bool loading = _cloud.InvitationVisible && _cloud.InvitationState == AsyncEchoOperationState.Pending;
        bool failed = _cloud.InvitationVisible && !ready && !loading;
        _entry.GetComponentInChildren<Text>().text = asyncResult ? "查看挑战成绩" : "好友挑战";
        _view.ShowBoard(_showBoard);
        _view.invitationStatus.text = ready ? "已就绪" : loading ? "正在读取好友影子" : failed ? "邀请暂不可用" : "好友邀请";
        _view.invitationTitle.text = ready ? "好友影子" : loading ? "准备挑战中" : failed ? "稍后再试一次" : asyncResult ? "这场挑战已结束" : "还没有好友邀请";
        _view.invitationDescription.text = ready ? "这是好友留下的固定跑法。\n本局不学习，也不改变你的回声。"
            : loading ? "正在读取这份邀请里的影子。\n等待时也可以继续单机。"
            : failed ? ErrorLabel(_cloud.InvitationError)
            : asyncResult ? "成绩上传状态见下方。\n也可以切到挑战榜查看最佳距离。"
            : "好友分享卡片后，就能在这里挑战。\n先跑一局，也可以留下自己的影子。";
        _view.generation.text = ready ? "第 " + _cloud.CurrentSnapshot.Generation + " 代" : loading ? "读取中" : "尚无影子";
        _view.SetInvitationTone(ready, loading, failed);
        _view.start.gameObject.SetActive(ready || loading);
        _view.start.interactable = menu && ready;
        SetLabel(_view.start, loading ? "正在准备…" : "开始挑战");
        _view.offline.gameObject.SetActive(menu);
        SetLabel(_view.offline, ready || loading ? "先跑单机" : "去跑一局");
        _view.offline.interactable = menu;
        _view.ArrangeActions(ready || loading);
        _view.recent.gameObject.SetActive(menu && _cloud.RecentInvitation != null);
        _view.recent.interactable = !loading;
        SetLabel(_view.recent, failed ? "重新获取影子" : "最近邀请");
        _view.publishCard.SetActive(!asyncResult);
        _view.publish.interactable = _cloud.IsAvailable && _cloud.PublishState != AsyncEchoOperationState.Pending;
        _view.share.interactable = _cloud.Published != null;
        _view.publishStatus.text = _cloud.PublishState == AsyncEchoOperationState.Pending ? "正在发布你的影子…"
            : _cloud.Published != null ? "已发布，可以分享给好友。"
            : _cloud.PublishState == AsyncEchoOperationState.Failed ? "发布未完成，展开后可以重试。"
            : "跑完单机后，影子会自动发布。";
        bool hasReport = _cloud.LastReportBoardId != null;
        _view.reportCard.SetActive(hasReport && (!_cloud.InvitationVisible
            || _cloud.CurrentSnapshot?.BoardId == _cloud.LastReportBoardId));
        _view.reportStatus.text = "上次挑战成绩 · " + StateLabel(_cloud.ReportState);
        bool canRetry = hasReport && (_cloud.ReportState == AsyncEchoOperationState.Failed
            || _cloud.ReportState == AsyncEchoOperationState.Unconfirmed);
        _view.retry.gameObject.SetActive(canRetry);
        _view.retry.interactable = canRetry;
        bool hasBoard = _cloud.InvitationVisible ? _cloud.CurrentSnapshot != null
            : asyncResult ? hasReport : _cloud.Published != null;
        // A previous invitation's loaded board must never masquerade as this one.
        string expectedBoard = _cloud.InvitationVisible ? _cloud.CurrentSnapshot?.BoardId
            : asyncResult ? _cloud.LastReportBoardId : _cloud.Published?.BoardId;
        bool matchingBoard = expectedBoard != null && expectedBoard == _cloud.LeaderboardBoardId;
        _view.SetLeaderboard(matchingBoard ? _cloud.LeaderboardState : AsyncEchoOperationState.Idle,
            matchingBoard ? _cloud.Leaderboard : null, hasBoard);
        ApplyVisibility();
    }

    private static void SetLabel(Button button, string label)
    { button.GetComponentInChildren<Text>().text = label; }
    public static string BuildLeaderboardText(AsyncEchoOperationState state, AsyncEchoLeaderboardEntry[] items)
    {
        if (state == AsyncEchoOperationState.Pending) return "正在加载挑战榜……";
        if (state != AsyncEchoOperationState.Succeeded) return "暂时无法加载挑战榜，请重试。";
        if (items == null || items.Length == 0) return "还没有挑战成绩。";
        var text = new StringBuilder("该影子的挑战榜\n\n");
        foreach (var item in items)
            text.Append(item.rank).Append(". ").Append(item.displayLabel).Append(item.isMe ? "（我）" : "")
                .Append("\n").Append(item.distanceMeters.ToString("0.0")).Append(" 米    领先 ")
                .Append(item.playerLeadMeters.ToString("+0.0;-0.0;0.0")).Append(" 米\n\n");
        return text.ToString();
    }
    public void ShowNotice(string code)
    { if (_toast != null) { _toast.text = ErrorLabel(code); _toastUntil = Time.realtimeSinceStartupAsDouble + 4d; } }
    private static string StateLabel(AsyncEchoOperationState state)
    {
        switch (state)
        {
            case AsyncEchoOperationState.Pending: return "处理中";
            case AsyncEchoOperationState.Succeeded: return "已确认";
            case AsyncEchoOperationState.Unconfirmed: return "未确认，可重试";
            case AsyncEchoOperationState.Failed: return "失败，可重试";
            case AsyncEchoOperationState.Unavailable: return "暂不可用";
            default: return "尚未开始";
        }
    }
    public static string ErrorLabel(string code)
    {
        switch (code)
        {
            case "SHARE_REQUESTED": return "已请求打开微信分享界面";
            case "INVITATION_WAITING": return "收到好友邀请，返回菜单后可查看";
            case "SHADOW_NOT_FOUND": return "这份好友影子暂时不可用";
            case "RULES_UNSUPPORTED": case "RULES_RETIRED": case "API_UNSUPPORTED": return "挑战版本不受支持，请更新游戏";
            case "TIMEOUT": case "NETWORK_ERROR": return "网络暂时不可用，稍后可重试";
            case "CLOUD_NOT_CONFIGURED": case "ASYNC_UNAVAILABLE": case "CLOUD_UNAVAILABLE": case "SDK_NOT_READY":
                return "异步挑战暂不可用，可继续单机";
            default: return "暂时无法完成好友挑战操作，可继续单机";
        }
    }
    private void OnDisable() { SetModalInput(false); }
    private void OnDestroy() { SetModalInput(false); if (_cloud != null) _cloud.Changed -= Refresh; }
}
