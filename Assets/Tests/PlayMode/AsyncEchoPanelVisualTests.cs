using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Renders the shipping friend-challenge panel with an in-memory transport. No SDK,
/// scene loading, gameplay settlement, or save initialization participates. Images
/// prove this panel's Unity rendering, not WeChat device or network acceptance.
/// Run with graphics enabled; -nographics cannot produce visual evidence.
/// </summary>
public sealed class AsyncEchoPanelVisualTests
{
    private const int CaptureLayer = 31;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
    private readonly List<GameObject> _owned = new List<GameObject>();
    private readonly Dictionary<Behaviour, bool> _enabled = new Dictionary<Behaviour, bool>();
    private readonly Dictionary<FieldInfo, object> _statics = new Dictionary<FieldInfo, object>();
    private readonly Dictionary<string, string> _saveStrings = new Dictionary<string, string>();
    private readonly Dictionary<string, int> _saveInts = new Dictionary<string, int>();
    private Camera _camera;
    private Canvas _canvas;
    private GraphicRaycaster _raycaster;
    private RenderTexture _target;
    private AsyncEchoPanel _panel;
    private AsyncEchoCloud _cloud;
    private FakeTransport _transport;
    private Image _underlay;
    private int _underlayClicks;
    private GameObject _selected;
    private bool _progressCaptured;
    private RuntimeRoundedSprite _menuRounded;

    private static readonly string[] SaveStringKeys =
    {
        EchoRunSaveSystem.SaveKey, EchoRunSaveSystem.SaveSlotAKey, EchoRunSaveSystem.SaveSlotBKey,
        EchoRunSaveSystem.SingleContractSaveSlotAKey, EchoRunSaveSystem.SingleContractSaveSlotBKey,
        EchoRunSaveSystem.TelemetryKey, "AIShadowProfileV1"
    };
    private static readonly string[] SaveIntKeys =
    {
        EchoRunSaveSystem.ActiveSaveSlotKey, EchoRunSaveSystem.SingleContractActiveSaveSlotKey,
        EchoRunSaveSystem.TrainingResetPendingKey, "HighScore", "TotalCoins"
    };

    [SetUp]
    public void IsolateSceneAndProgress()
    {
        _progressCaptured = false;
        foreach (GameManager game in Object.FindObjectsOfType<GameManager>(true))
            Assert.IsFalse(game.State == GameState.Playing || game.State == GameState.Paused || game.IsDeathSequence,
                "UI capture will not interrupt an active run, a paused run, or a death sequence. "
                + "Return to a settled menu or use an isolated batch test session first.");
        _underlayClicks = 0;
        _saveStrings.Clear();
        _saveInts.Clear();
        foreach (string key in SaveStringKeys)
            if (PlayerPrefs.HasKey(key)) _saveStrings[key] = PlayerPrefs.GetString(key);
        foreach (string key in SaveIntKeys)
            if (PlayerPrefs.HasKey(key)) _saveInts[key] = PlayerPrefs.GetInt(key);
        _progressCaptured = true;
        _selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        foreach (MonoBehaviour component in Object.FindObjectsOfType<MonoBehaviour>())
        {
            Assembly assembly = component.GetType().Assembly;
            if (assembly == typeof(GameManager).Assembly || assembly == typeof(AsyncEchoPanel).Assembly)
                Suspend(component);
        }
        foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>()) Suspend(canvas);
        ReplaceStatic(typeof(GameManager), "<Instance>k__BackingField", null);
        ReplaceStatic(typeof(MenuScreenRouter), "<Instance>k__BackingField", null);
        ReplaceStatic(typeof(AudioManager), "<Instance>k__BackingField", null);
        ReplaceStatic(typeof(EchoRunAccessibility), "_initialized", true);
        ReplaceStatic(typeof(EchoRunAccessibility), "_largeText", false);
        ReplaceStatic(typeof(EchoRunAccessibility), "_highContrast", false);
        ReplaceStatic(typeof(EchoRunAccessibility), "_reducedMotion", true);
    }

    [TearDown]
    public void RestoreSceneAndVerifyProgress()
    {
        if (!_progressCaptured) return;
        _cloud?.Dispose();
        for (int i = _owned.Count - 1; i >= 0; --i)
            if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
        _owned.Clear();
        _menuRounded?.Dispose();
        _menuRounded = null;
        if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); _target = null; }
        foreach (var pair in _statics) pair.Key.SetValue(null, pair.Value);
        _statics.Clear();
        foreach (var pair in _enabled) if (pair.Key != null) pair.Key.enabled = pair.Value;
        _enabled.Clear();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_selected);
        // No test state was installed in PlayerPrefs. A UI-only interaction must
        // leave every protected save byte and slot selection exactly as it was.
        foreach (string key in SaveStringKeys)
        {
            Assert.AreEqual(_saveStrings.ContainsKey(key), PlayerPrefs.HasKey(key), key);
            if (_saveStrings.ContainsKey(key)) Assert.AreEqual(_saveStrings[key], PlayerPrefs.GetString(key), key);
        }
        foreach (string key in SaveIntKeys)
        {
            Assert.AreEqual(_saveInts.ContainsKey(key), PlayerPrefs.HasKey(key), key);
            if (_saveInts.ContainsKey(key)) Assert.AreEqual(_saveInts[key], PlayerPrefs.GetInt(key), key);
        }
        _saveStrings.Clear(); _saveInts.Clear();
        _progressCaptured = false;
    }

    [UnityTest]
    public IEnumerator Portrait540x1100() { return ExerciseStates(540, 1100); }

    [UnityTest]
    public IEnumerator CompactPortrait360x720() { return ExerciseStates(360, 720); }

    [UnityTest]
    public IEnumerator Landscape1100x540() { return ExerciseStates(1100, 540); }

    [UnityTest]
    public IEnumerator HomePortrait540x1100() { return CaptureHome(540, 1100); }

    [UnityTest]
    public IEnumerator HomeLandscape1100x540() { return CaptureHome(1100, 540); }

    [UnityTest]
    public IEnumerator PublishingIsSecondaryButRemainsAccessible()
    {
        CreateFixture(540, 1100);
        _cloud.SetMenuAvailable(true);
        ActiveEchoIdentity identity = SingleContractValidationIdentity.Create();
        _cloud.ReceiveInvitation(new AsyncEchoInvitation("visual-owner", identity.identityId, 1));
        ReplyIdentity(identity, "visual-owner", "visual-board");
        yield return SettleLayout();
        AsyncEchoPanelView view = _panel.GetComponentInChildren<AsyncEchoPanelView>(true);
        Assert.IsEmpty(view.GetComponentsInChildren<RawImage>(true), "Social actions must not depend on a poster illustration.");
        Assert.IsFalse(view.publishDetails.activeSelf, "Publishing starts collapsed so it does not compete with an invitation.");
        Assert.AreSame(view.actionDock.transform, view.start.transform.parent);
        RectTransform actions = (RectTransform)view.actionDock.transform;
        float summaryBottom = view.invitationSummary.TransformPoint(new Vector3(0, view.invitationSummary.rect.yMin, 0)).y;
        float actionTop = actions.TransformPoint(new Vector3(0, actions.rect.yMax, 0)).y;
        Assert.LessOrEqual(Mathf.Abs(summaryBottom - actionTop) / view.frame.lossyScale.y, 24f,
            "The primary action must follow its invitation, not sit across an empty screen.");
        AssertButtonReachable("AsyncEchoBegin");
        yield return ScrollButtonIntoView("AsyncEchoPublishToggle");
        ClickButton("AsyncEchoPublishToggle");
        yield return SettleLayout();
        Assert.IsTrue(view.publishDetails.activeInHierarchy);
        yield return ScrollButtonIntoView("AsyncEchoPublish");
        AssertButtonReachable("AsyncEchoPublish");
        Assert.IsTrue(view.share.gameObject.activeInHierarchy);
        Assert.IsFalse(view.share.interactable, "Sharing stays disabled until cloud publication succeeds.");
        AssertButtonReachable("AsyncEchoBegin");
        AssertButtonReachable("AsyncEchoOffline");
        Capture("invitation-publishing-expanded", 540, 1100);
        EventSystem.current.SetSelectedGameObject(view.publish.gameObject);
        yield return ScrollButtonIntoView("AsyncEchoPublishToggle");
        ClickButton("AsyncEchoPublishToggle");
        yield return SettleLayout();
        Assert.IsFalse(view.publishDetails.activeSelf);
        Assert.AreSame(view.publishToggle.gameObject, EventSystem.current.currentSelectedGameObject,
            "Collapsing the secondary controls must not strand keyboard focus on a hidden button.");
    }

    [UnityTest]
    public IEnumerator InvitationFailureRetainsOfflineAndRetryActions()
    {
        CreateFixture(360, 720);
        _cloud.SetMenuAvailable(true);
        ActiveEchoIdentity identity = SingleContractValidationIdentity.Create();
        _cloud.ReceiveInvitation(new AsyncEchoInvitation("visual-owner", identity.identityId, 1));
        yield return SettleLayout();
        Assert.IsFalse(FindButton("AsyncEchoBegin").interactable);
        AssertButtonReachable("AsyncEchoOffline");
        Capture("invitation-loading", 360, 720);
        _transport.Fail("NETWORK_ERROR");
        _cloud.Tick();
        yield return SettleLayout();
        Assert.AreEqual(AsyncEchoOperationState.Failed, _cloud.InvitationState);
        Assert.IsFalse(FindButton("AsyncEchoBegin").gameObject.activeInHierarchy);
        AssertButtonReachable("AsyncEchoOffline");
        StringAssert.Contains("网络", VisibleText());
        Capture("invitation-error", 360, 720);
        yield return ScrollButtonIntoView("AsyncEchoRecent");
        ClickButton("AsyncEchoRecent");
        Assert.AreEqual(AsyncEchoOperationState.Pending, _cloud.InvitationState);
        Assert.AreEqual("get", _transport.LastAction);
    }

    [UnityTest]
    public IEnumerator NewInvitationReplacesTheVisiblePreviousBoard()
    {
        CreateFixture(540, 1100);
        _cloud.SetMenuAvailable(true);
        ActiveEchoIdentity identity = SingleContractValidationIdentity.Create();
        _cloud.ReceiveInvitation(new AsyncEchoInvitation("visual-owner", identity.identityId, 1));
        ReplyIdentity(identity, "visual-owner", "visual-board");
        yield return SettleLayout();
        ClickButton("AsyncEchoBoard");
        _transport.Reply(Board(new[] { Entry(1, "previous-runner", "OLD BOARD ROW", 999.9, 10, false) }));
        _cloud.Tick();
        yield return SettleLayout();
        AsyncEchoPanelView view = _panel.GetComponentInChildren<AsyncEchoPanelView>(true);
        Assert.IsTrue(view.boardPage.activeInHierarchy);
        StringAssert.Contains("OLD BOARD ROW", VisibleText());

        // A warm invitation arrives while the old invitation is still visible.
        // The visibility flag alone does not change; its key must reset the page.
        _cloud.ReceiveInvitation(new AsyncEchoInvitation("visual-owner-b", identity.identityId, 1));
        yield return SettleLayout();
        Assert.AreEqual(AsyncEchoOperationState.Pending, _cloud.InvitationState);
        Assert.IsNull(_cloud.CurrentSnapshot);
        Assert.IsTrue(view.challengePage.activeInHierarchy, "A new invitation must reveal its own challenge page.");
        Assert.IsFalse(view.boardPage.activeInHierarchy);
        Assert.IsFalse(view.start.interactable);
        StringAssert.DoesNotContain("OLD BOARD ROW", VisibleText());
        foreach (AsyncEchoLeaderboardRow row in view.rows.GetComponentsInChildren<AsyncEchoLeaderboardRow>(true))
            Assert.IsFalse(row.gameObject.activeSelf, "Old board rows must also be invalidated behind the challenge page.");

        ReplyIdentity(identity, "visual-owner-b", "visual-board-b");
        yield return SettleLayout();
        Assert.AreEqual("visual-board-b", _cloud.CurrentSnapshot.BoardId);
        AssertButtonReachable("AsyncEchoBegin");
        AssertButtonReachable("AsyncEchoOffline");
        Capture("invitation-replaced", 540, 1100);
        ClickButton("AsyncEchoBoard");
        yield return SettleLayout();
        Assert.AreEqual(AsyncEchoOperationState.Pending, _cloud.LeaderboardState);
        StringAssert.DoesNotContain("OLD BOARD ROW", VisibleText());
        _transport.Reply(new AsyncEchoCloudData
        {
            boardId = "visual-board-b", rulesVersion = 1,
            items = new[] { Entry(1, "next-runner", "NEW BOARD ROW", 432.1, 5, true) }
        });
        _cloud.Tick();
        yield return SettleLayout();
        StringAssert.Contains("NEW BOARD ROW", VisibleText());
        StringAssert.DoesNotContain("OLD BOARD ROW", VisibleText());
    }

    [UnityTest]
    public IEnumerator LargeTextCompactPortrait360x720()
    {
        // The original static preference was already captured by SetUp. Enable
        // the real accessibility layout without writing the user's preference.
        typeof(EchoRunAccessibility).GetField("_largeText", Static).SetValue(null, true);
        CreateFixture(360, 720);
        _cloud.SetMenuAvailable(true);
        ActiveEchoIdentity identity = SingleContractValidationIdentity.Create();
        _cloud.ReceiveInvitation(new AsyncEchoInvitation("visual-owner", identity.identityId, 1));
        ReplyIdentity(identity, "visual-owner", "visual-board");
        yield return SettleLayout();
        AsyncEchoPanelView view = _panel.GetComponentInChildren<AsyncEchoPanelView>(true);
        Assert.Greater(view.heading.fontSize, view.heading.GetComponent<EchoRunAccessibleText>().baseFontSize,
            "The capture must exercise the actual large-text mode.");
        AssertTextHeight(view.heading);
        AssertTextHeight(view.invitationTitle);
        AssertTextHeight(view.invitationDescription);
        AssertTextHeight(view.generation);
        AssertTextHeight(view.publishStatus);
        foreach (Text text in view.challengePage.GetComponentsInChildren<Text>(true))
            if (text.name == "SaveAssurance") AssertTextHeight(text);
        AssertButtonReachable("AsyncEchoBegin");
        AssertButtonReachable("AsyncEchoOffline");
        AssertButtonReachable("CloseAsyncEcho");
        Capture("invitation-large-text", 360, 720);
        yield return ScrollButtonIntoView("AsyncEchoRecent");
        AssertButtonReachable("AsyncEchoBegin");
        AssertButtonReachable("AsyncEchoOffline");
        Capture("invitation-large-text-scrolled", 360, 720);
        ClickButton("AsyncEchoBoard");
        _transport.Reply(Board(new AsyncEchoLeaderboardEntry[0]));
        _cloud.Tick();
        yield return SettleLayout();
        AssertTextHeight(view.heading);
        AssertTextHeight(view.boardMessage);
        AssertTextHeight(view.boardDetail);
        Capture("leaderboard-large-text-empty", 360, 720);
        ClickButton("AsyncEchoReloadBoard");
        _transport.Reply(Board(new[] { Entry(1, "large-me", "挑战者（我）", 864.6, 12.5, true) }));
        _cloud.Tick();
        yield return SettleLayout();
        foreach (AsyncEchoLeaderboardRow row in view.rows.GetComponentsInChildren<AsyncEchoLeaderboardRow>())
        {
            AssertTextHeight(row.distance);
            AssertTextHeight(row.lead);
        }
        Capture("leaderboard-large-text-results", 360, 720);
    }

    [UnityTest]
    public IEnumerator NewPlayerChallengeWithoutInvitation()
    {
        CreateFixture(540, 1100);
        _cloud.SetMenuAvailable(true);
        yield return SettleLayout();
        ClickButton("AsyncEchoEntry");
        yield return SettleLayout();
        AsyncEchoPanelView view = _panel.GetComponentInChildren<AsyncEchoPanelView>(true);
        Assert.IsTrue(view.challengePage.activeInHierarchy);
        Assert.IsFalse(view.start.gameObject.activeInHierarchy);
        Assert.IsFalse(view.recent.gameObject.activeInHierarchy);
        Assert.IsFalse(view.share.interactable);
        AssertButtonReachable("AsyncEchoOffline");
        AssertTextHeight(view.heading);
        AssertTextHeight(view.invitationTitle);
        AssertTextHeight(view.invitationDescription);
        AssertUnderlayBlocked();
        Assert.IsFalse(_underlay.GetComponent<Button>().IsInteractable(), "A covered menu button must also reject keyboard Submit.");
        Capture("challenge-new-player", 540, 1100);
        ClickButton("AsyncEchoBoard");
        yield return SettleLayout();
        Assert.IsTrue(view.emptyBoard.activeInHierarchy);
        Assert.IsFalse(view.reload.interactable);
        Assert.That(VisibleText(), Does.Contain("邀请").And.Contain("发布"));
        Assert.AreEqual(0, _cloud.InFlightCount, "An absent board must not produce a cloud request.");
        Assert.IsNull(_transport.LastAction);
        AssertTextHeight(view.boardMessage);
        AssertTextHeight(view.boardDetail);
        Capture("leaderboard-no-shadow", 540, 1100);
    }

    private IEnumerator ExerciseStates(int width, int height)
    {
        Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
            "Visual acceptance needs a graphics-enabled Unity run, without -nographics.");
        CreateFixture(width, height);
        ActiveEchoIdentity identity = SingleContractValidationIdentity.Create();
        _cloud.SetMenuAvailable(true);
        _cloud.ReceiveInvitation(new AsyncEchoInvitation("visual-owner", identity.identityId, 1));
        Assert.AreEqual("get", _transport.LastAction);
        yield return null;
        Assert.IsFalse(FindButton("AsyncEchoBegin").interactable,
            "Starting must stay disabled until the frozen identity is validated.");
        _transport.Reply(new AsyncEchoCloudData
        {
            found = true, boardId = "visual-board", ownerOpenid = "visual-owner",
            identityId = identity.identityId, payloadVersion = ActiveEchoIdentity.CurrentVersion,
            payloadJson = identity.ToJson(), generation = identity.generation, rulesVersion = 1, runSeed = 1337
        });
        _cloud.Tick();
        yield return SettleLayout();
        Assert.AreEqual(AsyncEchoOperationState.Succeeded, _cloud.InvitationState);
        AssertButtonReachable("AsyncEchoBegin");
        AssertButtonReachable("AsyncEchoOffline");
        AssertUnderlayBlocked();
        AssertWhitespaceScrollTarget(_panel.GetComponentInChildren<AsyncEchoPanelView>(true).challengeScroll);
        Capture("invitation-ready", width, height);
        AssertUnderlayNotVisible();

        ClickButton("AsyncEchoBoard");
        Assert.AreEqual("leaderboard", _transport.LastAction);
        _transport.Reply(Board(new AsyncEchoLeaderboardEntry[0]));
        _cloud.Tick();
        yield return SettleLayout();
        Assert.AreEqual(AsyncEchoOperationState.Succeeded, _cloud.LeaderboardState);
        AssertWhitespaceScrollTarget(_panel.GetComponentInChildren<AsyncEchoPanelView>(true).boardScroll);
        Assert.That(VisibleText(), Does.Contain("还没有").Or.Contain("等待").Or.Contain("第一"),
            "An empty leaderboard needs a visible explanation.");
        Capture("leaderboard-empty", width, height);

        yield return ScrollButtonIntoView("AsyncEchoReloadBoard");
        ClickButton("AsyncEchoReloadBoard");
        _transport.Fail("NETWORK_ERROR");
        _cloud.Tick();
        yield return SettleLayout();
        Assert.AreEqual(AsyncEchoOperationState.Failed, _cloud.LeaderboardState);
        Assert.That(VisibleText(), Does.Contain("重试").Or.Contain("无法").Or.Contain("网络"),
            "A failed board must explain the state and retain a retry action.");
        Capture("leaderboard-error", width, height);

        yield return ScrollButtonIntoView("AsyncEchoReloadBoard");
        ClickButton("AsyncEchoReloadBoard");
        _transport.Reply(Board(new[]
        {
            Entry(1, "runner-a", "挑战者 A", 1028.4, 38.7, false),
            Entry(2, "runner-me", "挑战者 B", 864.6, 12.5, true),
            Entry(3, "runner-c", "挑战者 C", 726.2, -18.4, false),
            Entry(4, "runner-d", "挑战者 D", 610.8, -34.6, false),
            Entry(5, "runner-e", "挑战者 E", 508.3, -62.1, false)
        }));
        _cloud.Tick();
        yield return SettleLayout();
        Assert.AreEqual(5, _cloud.Leaderboard.Length);
        StringAssert.Contains("1028.4", VisibleText());
        StringAssert.Contains("864.6", VisibleText());
        AssertUnderlayBlocked();
        Capture("leaderboard-results", width, height);

        AsyncEchoLeaderboardEntry[] completedRows = _cloud.Leaderboard;
        yield return ScrollButtonIntoView("AsyncEchoReloadBoard");
        ClickButton("AsyncEchoReloadBoard");
        Assert.AreEqual(AsyncEchoOperationState.Pending, _cloud.LeaderboardState,
            "The refresh button below a long board must remain reachable by scrolling.");
        _transport.Reply(Board(completedRows));
        _cloud.Tick();

        ClickButton("AsyncEchoChallengeTab");
        yield return SettleLayout();
        AssertButtonReachable("AsyncEchoBegin");
        ClickButton("CloseAsyncEcho");
        yield return null;
        Assert.IsFalse(_cloud.InvitationVisible);
        AssertButtonReachable("AsyncEchoEntry");
        ClickAt(new Vector2(width * .5f, height * .5f));
        Assert.AreEqual(1, _underlayClicks, "Closing the modal must restore the underlying menu input.");
        Assert.IsTrue(_underlay.GetComponent<Button>().IsInteractable(), "Closing must restore keyboard interaction too.");
        ClickButton("AsyncEchoEntry");
        yield return null;
        yield return ScrollButtonIntoView("AsyncEchoRecent");
        ClickButton("AsyncEchoRecent");
        Assert.AreEqual("get", _transport.LastAction);
        Assert.AreEqual(AsyncEchoOperationState.Pending, _cloud.InvitationState,
            "The real recent-invitation button must re-enter the cloud loading state.");
    }

    private IEnumerator CaptureHome(int width, int height)
    {
        Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
            "Menu visual acceptance needs a graphics-enabled Unity run.");
        CreateFixture(width, height);
        _panel.gameObject.SetActive(false);
        _underlay.gameObject.SetActive(false);
        GameObject owner = Own("AsyncEchoVisualInactiveMenu");
        owner.SetActive(false);
        UIManager menu = owner.AddComponent<UIManager>();
        SetPrivate(menu, "_gm", GameManager.Instance);
        Font body = Resources.Load<Font>("Fonts/EchoRunSansSC-Regular");
        Assert.IsNotNull(body, "Capture must use the actual bundled CJK font.");
        SetPrivate(menu, "_font", body);
        SetPrivate(menu, "_titleFont", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        SetPrivate(menu, "_safeAreaRoot", (RectTransform)_canvas.transform);
        _menuRounded = (RuntimeRoundedSprite)typeof(UIManager).GetField("_roundedUi", Private).GetValue(menu);
        InvokePrivate(menu, "CreateMenuPanel");
        InvokePrivate(menu, "LayoutMenu", height > width, height > width);
        InvokePrivate(menu, "FitMenuBackgroundToViewport", width, height);
        GameObject panel = (GameObject)typeof(UIManager).GetField("_menuPanel", Private).GetValue(menu);
        panel.SetActive(true);
        yield return SettleLayout();
        var background = panel.GetComponentInChildren<RawImage>();
        Assert.IsNotNull(background);
        Assert.IsNull(background.texture, "The menu must reveal the real game scene instead of the retired poster.");
        foreach (Button button in panel.GetComponentsInChildren<Button>()) AssertButtonReachable(button);
        foreach (Text label in panel.GetComponentsInChildren<Text>())
            if (!string.IsNullOrEmpty(label.text))
                Assert.Greater(label.cachedTextGenerator.vertexCount, 0, label.name + " did not render glyphs.");
        Capture("home", width, height);
    }

    private void CreateFixture(int width, int height)
    {
        Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
            "Visual acceptance needs a graphics-enabled Unity run, without -nographics.");
        GameObject inactive = Own("AsyncEchoVisualInactiveControllers");
        inactive.SetActive(false); // Neither controller may Awake, load saves, or initialize an SDK.
        GameManager game = inactive.AddComponent<GameManager>();
        typeof(GameManager).GetField("<Instance>k__BackingField", Static).SetValue(null, game);
        var runtime = inactive.AddComponent<WeixinMiniGameRuntime>();
        _transport = new FakeTransport();
        _cloud = new AsyncEchoCloud(_transport, () => 0d);
        typeof(WeixinMiniGameRuntime).GetField("<Cloud>k__BackingField", Private).SetValue(runtime, _cloud);
        if (EventSystem.current == null) Own("AsyncEchoVisualEventSystem").AddComponent<EventSystem>();

        _target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        _target.Create();
        _camera = Own("AsyncEchoVisualCamera").AddComponent<Camera>();
        _camera.enabled = false;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(.05f, .08f, .12f, 1f);
        _camera.cullingMask = 1 << CaptureLayer;
        _camera.orthographic = true;
        _camera.nearClipPlane = .1f;
        _camera.farClipPlane = 10f;
        _camera.targetTexture = _target;
        _canvas = Own("AsyncEchoVisualCanvas", typeof(RectTransform)).AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceCamera;
        _canvas.worldCamera = _camera;
        _canvas.planeDistance = 1f;
        // Same Expand formula as UIManager's CanvasScaler, using the target pixels
        // rather than the Editor Game View dimensions. No production sizing is faked.
        Vector2 reference = UILayoutRules.GetReferenceResolution(width, height);
        _canvas.scaleFactor = Mathf.Min(width / reference.x, height / reference.y);
        _raycaster = _canvas.gameObject.AddComponent<GraphicRaycaster>();
        GameObject underlay = new GameObject("UnderlyingMenuProbe", typeof(RectTransform), typeof(Image), typeof(Button));
        underlay.transform.SetParent(_canvas.transform, false);
        RuntimePanelFactory.Stretch((RectTransform)underlay.transform);
        _underlay = underlay.GetComponent<Image>();
        _underlay.color = new Color(.22f, .28f, .35f, 1f);
        underlay.GetComponent<Button>().onClick.AddListener(() => _underlayClicks++);
        GameObject panel = new GameObject("AsyncEchoPanel", typeof(RectTransform));
        panel.transform.SetParent(_canvas.transform, false);
        _panel = panel.AddComponent<AsyncEchoPanel>();
        _panel.Initialize(runtime, _cloud);
    }

    private IEnumerator SettleLayout()
    {
        yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(.15f); // Finish the production .1s ColorTint transition.
        PrepareCanvas();
    }

    private void ReplyIdentity(ActiveEchoIdentity identity, string owner, string board)
    {
        _transport.Reply(new AsyncEchoCloudData
        {
            found = true, boardId = board, ownerOpenid = owner, identityId = identity.identityId,
            payloadVersion = ActiveEchoIdentity.CurrentVersion, payloadJson = identity.ToJson(),
            generation = identity.generation, rulesVersion = 1, runSeed = 1337
        });
        _cloud.Tick();
    }

    private static void AssertTextHeight(Text text)
    {
        Assert.IsNotNull(text);
        Assert.Greater(text.rectTransform.rect.width, 0f, text.name + " has no layout width.");
        Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1f,
            text.name + " clips its title or wrapped copy vertically at this text size.");
    }

    private void PrepareCanvas()
    {
        foreach (Transform child in _canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = CaptureLayer;
        Canvas.ForceUpdateCanvases();
    }

    private Button FindButton(string name)
    {
        foreach (Button button in _panel.GetComponentsInChildren<Button>(true))
            if (button.name == name) return button;
        Assert.Fail("Shipping AsyncEchoPanel is missing button " + name);
        return null;
    }

    private Vector2 AssertButtonReachable(string name)
    { return AssertButtonReachable(FindButton(name)); }

    private Vector2 AssertButtonReachable(Button button)
    {
        PrepareCanvas();
        string name = button.name;
        Assert.IsTrue(button.gameObject.activeInHierarchy, name + " is hidden.");
        Assert.IsTrue(button.IsInteractable(), name + " is disabled.");
        RectTransform rect = (RectTransform)button.transform;
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(_camera, corner);
            Assert.That(screen.x, Is.InRange(-.5f, _target.width + .5f), name + " leaves horizontal viewport.");
            Assert.That(screen.y, Is.InRange(-.5f, _target.height + .5f), name + " leaves vertical viewport.");
        }
        Vector2 center = RectTransformUtility.WorldToScreenPoint(_camera, rect.TransformPoint(rect.rect.center));
        GameObject hit = TopHit(center);
        Assert.IsTrue(hit == button.gameObject || hit.transform.IsChildOf(button.transform),
            name + " is obscured by " + hit.name + ".");
        return center;
    }

    private void ClickButton(string name) { ClickAt(AssertButtonReachable(name)); }

    private void AssertWhitespaceScrollTarget(ScrollRect scroll)
    {
        PrepareCanvas();
        Rect bounds = scroll.viewport.rect;
        Vector3 world = scroll.viewport.TransformPoint(new Vector3(bounds.xMin + 1f, bounds.yMax - 1f, 0));
        Vector2 point = RectTransformUtility.WorldToScreenPoint(_camera, world);
        GameObject hit = TopHit(point);
        Assert.AreSame(scroll, hit.GetComponentInParent<ScrollRect>(),
            "Whitespace must deliver touch/scroll gestures to its own page.");
        var pointer = new PointerEventData(EventSystem.current) { position = point, scrollDelta = new Vector2(0, -4) };
        Assert.IsNotNull(ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.scrollHandler));
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
        PrepareCanvas();
    }

    private IEnumerator ScrollButtonIntoView(string name)
    {
        Button button = FindButton(name);
        ScrollRect scroll = button.GetComponentInParent<ScrollRect>();
        if (scroll == null) yield break;
        Assert.IsTrue(scroll.gameObject.activeInHierarchy, name + " belongs to a hidden scroll page.");
        scroll.StopMovement();
        for (int step = 0; step < 40; step++)
        {
            PrepareCanvas();
            RectTransform target = (RectTransform)button.transform;
            RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            float bottom = viewport.InverseTransformPoint(corners[0]).y;
            float top = viewport.InverseTransformPoint(corners[1]).y;
            if (bottom >= viewport.rect.yMin - .5f && top <= viewport.rect.yMax + .5f)
            {
                AssertButtonReachable(name);
                yield break;
            }
            float direction = bottom < viewport.rect.yMin ? -1f : 1f;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(_camera, viewport.TransformPoint(viewport.rect.center)),
                scrollDelta = new Vector2(0, direction * 4)
            };
            GameObject hit = TopHit(pointer.position);
            Assert.AreSame(scroll, hit.GetComponentInParent<ScrollRect>(), "Scroll input must hit the visible page.");
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.scrollHandler);
            yield return null;
        }
        Assert.Fail(name + " could not be reached through the real ScrollRect input handler.");
    }

    private void ClickAt(Vector2 position)
    {
        GameObject hit = TopHit(position);
        var pointer = new PointerEventData(EventSystem.current)
            { position = position, button = PointerEventData.InputButton.Left };
        ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
    }

    private GameObject TopHit(Vector2 point)
    {
        var hits = new List<RaycastResult>();
        _raycaster.Raycast(new PointerEventData(EventSystem.current) { position = point }, hits);
        Assert.IsNotEmpty(hits, "No UI raycast at " + point);
        return hits[0].gameObject;
    }

    private void AssertUnderlayBlocked()
    {
        // Include empty space near the sheet edge: buttons alone cannot provide a modal blocker.
        foreach (Vector2 normalized in new[] { new Vector2(.5f, .5f), new Vector2(.08f, .5f), new Vector2(.92f, .5f) })
        {
            GameObject hit = TopHit(Vector2.Scale(normalized, new Vector2(_target.width, _target.height)));
            Assert.IsTrue(hit.transform.IsChildOf(_panel.transform), "Underlying menu leaked through modal raycasts.");
        }
        Assert.AreEqual(0, _underlayClicks);
    }

    private void AssertUnderlayNotVisible()
    {
        Color original = _underlay.color;
        try
        {
            _underlay.color = Color.red;
            Color32[] red = RenderPixels();
            _underlay.color = Color.blue;
            Color32[] blue = RenderPixels();
            int largestDelta = 0;
            // Ignore rounded outer corners, inspect the central sheet where menu
            // lettering used to bleed through the old translucent background.
            for (int y = _target.height / 5; y < _target.height * 4 / 5; y += 4)
            for (int x = _target.width / 5; x < _target.width * 4 / 5; x += 4)
            {
                int i = y * _target.width + x;
                largestDelta = Mathf.Max(largestDelta, Mathf.Abs(red[i].r - blue[i].r), Mathf.Abs(red[i].b - blue[i].b));
            }
            Assert.LessOrEqual(largestDelta, 3, "The underlying menu remains visible through the challenge sheet.");
        }
        finally { _underlay.color = original; }
    }

    private void Capture(string state, int width, int height)
    {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestResults", "VisualSystem-20260923"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, state + "-" + width + "x" + height + ".png");
        Texture2D image = RenderImage();
        try { File.WriteAllBytes(path, image.EncodeToPNG()); }
        finally { Object.DestroyImmediate(image); }
        Debug.Log("ASYNC_ECHO_UI_CAPTURE " + path);
    }

    private Color32[] RenderPixels()
    {
        Texture2D image = RenderImage();
        try { return image.GetPixels32(); }
        finally { Object.DestroyImmediate(image); }
    }

    private Texture2D RenderImage()
    {
        PrepareCanvas();
        RenderTexture previous = RenderTexture.active;
        try
        {
            _camera.Render();
            RenderTexture.active = _target;
            var image = new Texture2D(_target.width, _target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
            image.Apply();
            return image;
        }
        finally { RenderTexture.active = previous; }
    }

    private string VisibleText()
    {
        var values = new List<string>();
        foreach (Text text in _panel.GetComponentsInChildren<Text>())
            if (text.enabled) values.Add(text.text);
        return string.Join("\n", values);
    }

    private GameObject Own(string name, params Type[] components)
    { var go = new GameObject(name, components); _owned.Add(go); return go; }

    private void Suspend(Behaviour component)
    { if (!_enabled.ContainsKey(component)) _enabled.Add(component, component.enabled); component.enabled = false; }

    private void ReplaceStatic(Type type, string name, object value)
    {
        FieldInfo field = type.GetField(name, Static);
        Assert.IsNotNull(field, type.Name + "." + name);
        _statics[field] = field.GetValue(null); field.SetValue(null, value);
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, Private);
        Assert.IsNotNull(field, fieldName);
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, Private);
        Assert.IsNotNull(method, methodName);
        method.Invoke(target, arguments);
    }

    private static AsyncEchoCloudData Board(AsyncEchoLeaderboardEntry[] entries) =>
        new AsyncEchoCloudData { boardId = "visual-board", rulesVersion = 1, items = entries };

    private static AsyncEchoLeaderboardEntry Entry(int rank, string id, string label, double distance, double lead, bool me) =>
        new AsyncEchoLeaderboardEntry { rank = rank, entryId = id, displayLabel = label, distanceMeters = distance,
            playerLeadMeters = lead, playerWon = lead > 0, isMe = me };

    private sealed class FakeTransport : IAsyncEchoTransport
    {
        public bool IsAvailable => true;
        public string UnavailableReason => null;
        public string LastAction { get; private set; }
        private Action<AsyncEchoTransportResult> _complete;
        public void Send(Dictionary<string, object> request, Action<AsyncEchoTransportResult> completed)
        { LastAction = (string)request["action"]; _complete = completed; }
        public void Reply(AsyncEchoCloudData data)
        { _complete(new AsyncEchoTransportResult(JsonUtility.ToJson(new AsyncEchoCloudReply { ok = true, apiVersion = 1, data = data }))); }
        public void Fail(string code) { _complete(new AsyncEchoTransportResult(null, code, false)); }
    }
}
