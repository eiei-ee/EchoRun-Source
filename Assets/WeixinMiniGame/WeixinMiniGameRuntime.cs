using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One persistent platform owner. Shared UI owns layout and the core owns gameplay.
/// </summary>
public sealed class WeixinMiniGameRuntime : MonoBehaviour
{
    private static WeixinMiniGameRuntime _instance;
    private readonly Queue<Action> _messages = new Queue<Action>();
    private readonly object _messageLock = new object();
    private readonly WeixinSafeAreaProvider _safeArea = new WeixinSafeAreaProvider();
    private GameManager _game;
    private GameState _previousGameState;
    private WeixinAsyncEchoTransport _transport;
    private AsyncEchoPanel _panel;
    private AsyncEchoSnapshot _activeBoard;
    private string _activeChallengeId;
    private bool _alive;
    private double _sdkDeadline;
    private bool _sdkPending;
    private string _pendingNotice;
    private double _nextBind;
    public AsyncEchoCloud Cloud { get; private set; }
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
    private Action<WeChatWASM.OnShowListenerResult> _onShow;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { _instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
        if (FindObjectOfType<WeixinMiniGameRuntime>() != null) return;
        new GameObject("Weixin MiniGame Runtime")
            .AddComponent<WeixinMiniGameRuntime>();
#endif
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        _alive = true;
        DontDestroyOnLoad(gameObject);
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
        Screen.orientation = ScreenOrientation.Portrait;
        WeChatSafeArea.Register(_safeArea.Resolve);
#endif
        _transport = new WeixinAsyncEchoTransport(AsyncEchoCloudSettings.Load());
        Cloud = new AsyncEchoCloud(_transport, () => Time.realtimeSinceStartupAsDouble);
        Cloud.Notice += ShowNotice;
        SceneManager.sceneLoaded += OnSceneLoaded;
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
        _sdkPending = true;
        _sdkDeadline = Time.realtimeSinceStartupAsDouble + 5d;
        try { WeChatWASM.WX.InitSDK(code => Enqueue(() => OnSdkInitialized(code))); }
        catch (Exception) { _sdkPending = false; ShowNotice("SDK_NOT_READY"); }
#endif
    }

    private void Update()
    {
        if (!_alive || Cloud == null) return;
        Cloud.Tick(); // Deadline checks precede queued SDK messages, including foreground resume.
        if (_sdkPending && Time.realtimeSinceStartupAsDouble >= _sdkDeadline)
        { _sdkPending = false; ShowNotice("SDK_NOT_READY"); }
        while (true)
        {
            Action action;
            lock (_messageLock)
            { if (_messages.Count == 0) break; action = _messages.Dequeue(); }
            action();
        }
        if (Time.realtimeSinceStartupAsDouble >= _nextBind)
        {
            _nextBind = Time.realtimeSinceStartupAsDouble + 0.25d;
            BindScene();
        }
        // Scene callbacks run before GameManager.Start. Only a stable frame can
        // expose Menu; Restart's temporary Menu must not consume queued invitations.
        Cloud.SetMenuAvailable(_game != null && _game.State == GameState.Menu);
    }

    private void BindScene()
    {
        GameManager current = GameManager.Instance;
        if (_game != current)
        {
            UnbindGame();
            _game = current;
            if (_game != null)
            {
                _previousGameState = _game.State;
                _game.OnStateChanged.AddListener(OnGameStateChanged);
                _game.LocalSingleContractSettled += OnLocalSettled;
                _game.AsyncChallengeStarted += OnAsyncStarted;
                _game.AsyncChallengeCompleted += OnAsyncCompleted;
                if (_game.IsAsyncChallengeRun && _game.ConfiguredAsyncChallengeParameters != null)
                    OnAsyncStarted(_game.ConfiguredAsyncChallengeParameters);
                if (_game.State != GameState.Menu) Cloud.SetMenuAvailable(false);
            }
        }
        if (_panel == null)
        {
            UIManager ui = FindObjectOfType<UIManager>();
            if (ui != null && ui.PlatformOverlayRoot != null)
            {
                _panel = new GameObject("AsyncEchoPanel", typeof(RectTransform))
                    .AddComponent<AsyncEchoPanel>();
                _panel.transform.SetParent(ui.PlatformOverlayRoot, false);
                _panel.Initialize(this, Cloud);
                if (_pendingNotice != null) { _panel.ShowNotice(_pendingNotice); _pendingNotice = null; }
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { BindScene(); }

    private void OnGameStateChanged(GameState state)
    {
        GameState previous = _previousGameState;
        _previousGameState = state;
        if (state != GameState.Menu) Cloud.SetMenuAvailable(false);
        if (state == GameState.Playing && previous == GameState.Menu)
        {
            // Resume is not a new run. Never consume an invite received while paused.
            Cloud.RunStarted();
            if (!_game.IsAsyncChallengeRun) { _activeBoard = null; _activeChallengeId = null; }
        }
    }

    private void OnLocalSettled() { Cloud.Publish(EchoRunSaveSystem.GetActiveEchoIdentity()); }
    private void OnAsyncStarted(AsyncChallengeRunParameters parameters)
    {
        // Restart creates a new run ID but retains the same frozen board.
        if (_activeBoard != null && parameters.rulesVersion == _activeBoard.Invitation.RulesVersion
            && parameters.runSeed == _activeBoard.RunSeed) _activeChallengeId = parameters.challengeId;
    }
    private void OnAsyncCompleted(AsyncChallengeResult result)
    {
        if (_activeBoard != null && result.challengeId == _activeChallengeId)
            Cloud.Report(result, _activeBoard);
    }

    public void BeginChallenge()
    {
        AsyncEchoSnapshot snapshot = Cloud.CurrentSnapshot;
        if (_game == null || _game.State != GameState.Menu || snapshot == null) return;
        string error;
        var parameters = new AsyncChallengeRunParameters(Guid.NewGuid().ToString("N"),
            snapshot.Invitation.RulesVersion, snapshot.RunSeed);
        if (!_game.TryConfigureAsyncChallenge(snapshot.CreateOpponent(), parameters, out error))
        { ShowNotice(error); return; }
        _activeBoard = snapshot;
        _activeChallengeId = parameters.challengeId;
        if (!_game.TryStartConfiguredRun(out error)) { ShowNotice(error); return; }
        Cloud.DismissInvitation();
    }

    public void BeginOffline()
    {
        Cloud.DismissInvitation();
        if (_game == null || _game.State != GameState.Menu) return;
        _game.TryConfigureGameplayFlow(GameplayFlowMode.SingleContract);
        _game.StartGame();
    }
    public void PublishLocal() { Cloud.Publish(EchoRunSaveSystem.GetActiveEchoIdentity()); }
    public void SharePublished()
    {
        if (Cloud.Published == null) return;
        ShowNotice(_transport.TryShare(Cloud.Published.Invitation) ? "SHARE_REQUESTED" : "ASYNC_UNAVAILABLE");
    }
    public void OpenBoard()
    {
        string boardId = Cloud.InvitationVisible ? Cloud.CurrentSnapshot?.BoardId
            : _game != null && _game.State == GameState.GameOver && _game.IsAsyncChallengeRun
                ? Cloud.LastReportBoardId ?? _activeBoard?.BoardId : Cloud.Published?.BoardId;
        if (boardId != null) Cloud.LoadLeaderboard(boardId);
    }
    public void ShowNotice(string code)
    {
        if (_panel != null) _panel.ShowNotice(code);
        else _pendingNotice = code;
    }

    private void Enqueue(Action message)
    { lock (_messageLock) if (_alive) _messages.Enqueue(message); }

#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
    private void OnSdkInitialized(int code)
    {
        if (!_sdkPending) return;
        // WXInitializeSDK sends Inited=200; this is not a cloud function errCode.
        if (code != 200) { _sdkPending = false; ShowNotice("SDK_NOT_READY"); return; }
        _sdkPending = false;
        _transport.InitializeCloud();
        try
        {
            _onShow = result =>
            {
                var query = result?.query == null ? null : new Dictionary<string, string>(result.query);
                Enqueue(() => ReadInvitation(query));
            };
            WeChatWASM.WX.OnShow(_onShow);
            ReadInvitation(WeChatWASM.WX.GetLaunchOptionsSync()?.query);
        }
        catch (Exception) { ShowNotice("ASYNC_UNAVAILABLE"); }
    }

    private void ReadInvitation(IDictionary<string, string> query)
    {
        AsyncEchoInvitation invitation;
        string error;
        if (!AsyncEchoInvitation.TryParse(query, out invitation, out error)) { ShowNotice(error); return; }
        if (invitation != null) Cloud.ReceiveInvitation(invitation);
    }
#endif

    private void UnbindGame()
    {
        if (_game == null) return;
        _game.OnStateChanged.RemoveListener(OnGameStateChanged);
        _game.LocalSingleContractSettled -= OnLocalSettled;
        _game.AsyncChallengeStarted -= OnAsyncStarted;
        _game.AsyncChallengeCompleted -= OnAsyncCompleted;
        _game = null;
    }
    private void OnDestroy()
    {
        if (_instance != this) return;
        _instance = null;
        lock (_messageLock) { _alive = false; _messages.Clear(); }
        UnbindGame();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        WeChatSafeArea.Unregister(_safeArea.Resolve);
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
        try { if (_onShow != null) WeChatWASM.WX.OffShow(_onShow); }
        catch (Exception) { }
#endif
        Cloud?.Dispose();
    }
}
