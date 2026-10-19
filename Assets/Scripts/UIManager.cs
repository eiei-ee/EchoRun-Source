using UnityEngine;
using UnityEngine.UI;

public enum SingleContractResultTone
{
    Progress,
    Success,
    Danger
}

public class UIManager : MonoBehaviour
{
    private static readonly Color Backdrop = EchoRunUITheme.Backdrop;
    private static readonly Color Surface = EchoRunUITheme.Surface;
    private static readonly Color SurfaceRaised = EchoRunUITheme.SurfaceRaised;
    private static readonly Color Primary = EchoRunUITheme.RouteCyan;
    private static readonly Color PrimaryStrong = EchoRunUITheme.RouteCyanDark;
    private static readonly Color Reward = EchoRunUITheme.Reward;
    private static readonly Color Danger = EchoRunUITheme.Danger;
    private static readonly Color Success = EchoRunUITheme.Success;
    private static readonly Color TextPrimary = EchoRunUITheme.TextPrimary;
    private static readonly Color TextMuted = EchoRunUITheme.TextMuted;
    private static readonly Color Ink = EchoRunUITheme.Ink;
    private static readonly Color PageBackdrop = EchoRunUITheme.PageBackdrop;
    private static readonly Color PageSurface = EchoRunUITheme.PageSurface;
    private static readonly Color PageRaised = EchoRunUITheme.PageRaised;
    private static readonly Color PageSelected = EchoRunUITheme.PageSelected;
    private static readonly Color PageInk = EchoRunUITheme.PageInk;
    private static readonly Color PageMuted = EchoRunUITheme.PageMuted;
    private static readonly Color PageEcho = EchoRunUITheme.PageEcho;

    // ── Menu ──
    GameObject _menuPanel;
    RawImage _menuBackground;
    RectTransform _menuReadabilityVeil;
    RectTransform _menuHeaderSurface;
    Button _startBtn, _settingsBtn, _characterBtn, _reportBtn;
    GameObject _reportPanel;
    bool _ownsEchoReportRoute;
    Text _reportText;
    Button _reportBackBtn;
    Text _menuProtocolText, _menuTitleText, _menuEnglishText, _menuTaglineText;
    Text _menuGenerationText, _menuLearnedText, _menuRuleText, _menuObjectiveText;
    bool _hasStartedSingleContractRun;

    // ── Settings (sub-panel of menu) ──
    GameObject _settingsPanel;
    Slider _masterSlider, _bgmSlider, _sfxSlider;
    Text _masterValueText, _bgmValueText, _sfxValueText, _fpsStatusText,
        _difficultyStatusText;
    Button _muteBtn;
    Button _fps30Btn, _fps60Btn, _fps120Btn;
    Button _difficultyRelaxedBtn, _difficultyStandardBtn,
        _difficultyIntenseBtn;
    Button _cameraLowBtn, _cameraHighBtn;
    Button _largeTextBtn, _highContrastBtn, _reducedMotionBtn;
    Button _settingsBackBtn;
    RectTransform _settingsContent;
    ScrollRect _settingsScroll;
    Text _runGuideText;
    bool _showObservationDetails;
    static readonly Color SettingsAccent = EchoRunUITheme.PageEcho;
    static readonly Color SettingsSelected = EchoRunUITheme.PageSelected;
    static readonly Color SettingsControl = EchoRunUITheme.PageRaised;

    // ── Character (sub-panel of menu) ──
    GameObject _characterPanel;
    Button _characterBackBtn;
    RectTransform _characterContent;
    Text _characterSelectionText;
    RunnerColorPreview _runnerPreview;
    Button _runnerFullViewBtn, _runnerFaceViewBtn;
    readonly Button[] _presetButtons = new Button[6];
    readonly Text[] _presetLabels = new Text[6];
    int _selectedPreset;

    // ── HUD ──
    GameObject _hudPanel;
    GameObject _hudStatsPanel, _hudContractPanel;
    GameObject _contractProgressGroup;
    Text _statsText, _contractText, _contractProgressText, _leadText, _duelFeedbackText;
    Image _contractProgressFill;
    GameObject _buffGroup;
    Text _buffText;
    Button _pauseBtn, _runSettingsBtn;
    bool _runSettingsOpen;
    EchoHudView _echoHudView;
    EchoHudPresenter _echoHudPresenter;
    GameObject _controlHint;
    Text _controlHintText;
    GameObject _landscapeGuard;

    // ── Pause ──
    GameObject _pausePanel;
    Button _resumeBtn, _pauseToMenuBtn, _pauseSettingsBtn;

    // ── GameOver ──
    GameObject _gameOverPanel;
    Text _finalScoreText, _highScoreText, _coinResultText, _shadowResultText;
    Text _gameOverTitleText, _gameOverStatsText;
    Button _restartBtn, _goToMenuBtn;
    Button _resultDetailsBtn;
    Text _resultDetailsText;
    ScrollRect _resultDetailsScroll;
    bool _usesCompactResult, _resultDetailsExpanded;
    SingleContractResultNextAction _resultNextAction;

    private Font _font;
    private Font _titleFont;
    private GameManager _gm;
    private MenuScreenRouter _menuRouter;
    private CanvasScaler _canvasScaler;
    private RectTransform _safeAreaRoot;
    public RectTransform PlatformOverlayRoot => _safeAreaRoot;
#if MINIGAME_SUBPLATFORM_WEIXIN
    private RectTransform _safeAreaTopCover, _safeAreaBottomCover;
#endif
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;
    private float _controlHintTimer;
    private float _nextDuelRefresh;
    private float _nextMenuRefresh;
    private float _fpsSampleElapsed;
    private int _fpsSampleFrames;
    private float _duelFeedbackTimer;
    private int _lastDuelFeedbackSequence = -1;
    private Transform _pendingTextRefreshRoot;
    private int _pendingTextRefreshFrames;
    private readonly RuntimeRoundedSprite _roundedUi = new RuntimeRoundedSprite();

    private const float ControlHintDuration = 7f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<UIManager>() != null) return;
        new GameObject("UIManager_Runtime").AddComponent<UIManager>();
    }

    void Awake()
    {
        _gm = GameManager.Instance;
        _menuRouter = GetComponent<MenuScreenRouter>();
        if (_menuRouter == null)
            _menuRouter = gameObject.AddComponent<MenuScreenRouter>();
        _menuRouter.Initialize(_gm);
    }

    void Start()
    {
        if (_gm == null) _gm = GameManager.Instance;
        if (_gm == null) return;
        _menuRouter.Initialize(_gm);

        _font = Resources.Load<Font>("Fonts/EchoRunSansSC-Regular");
        if (_font == null)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Debug.LogWarning("Bundled Noto Sans CJK font is missing; Chinese text may not render.");
        }
        _titleFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        EnsureCanvas();
        CreateMenuPanel();
        CreateSettingsPanel();
        CreateCharacterPanel();
        CreateEchoReportPanel();
        CreateHUDPanel();
        CreateControlHint();
        CreatePausePanel();
        CreateGameOverPanel();
        CreateLandscapeGuard();

        _menuRouter.Register(MenuScreen.Home, _menuPanel, _startBtn);
        _menuRouter.Register(MenuScreen.Settings, _settingsPanel, _masterSlider);
        _menuRouter.Register(MenuScreen.Runner, _characterPanel,
            _presetButtons.Length > 0 ? _presetButtons[0] : null);
        RefreshEchoReportNavigation();
        _menuRouter.RegisterHomeNavigation(_settingsBtn.gameObject);
        _menuRouter.RegisterHomeNavigation(_characterBtn.gameObject);

        _gm.OnStateChanged.AddListener(OnGameStateChanged);
        _gm.OnScoreChanged.AddListener(OnScoreChanged);
        _gm.OnCoinsChanged.AddListener(OnCoinsChanged);
        _gm.OnDistanceChanged.AddListener(OnDistanceChanged);
        EchoRunAccessibility.Changed += OnAccessibilityChanged;

        OnGameStateChanged(_gm.State);
        LoadCharacterPreset();
        ApplyResponsiveLayout();
        OnAccessibilityChanged();
        RefreshTextGeometry(_safeAreaRoot);
    }

    void Update()
    {
        ApplySafeArea();
#if MINIGAME_SUBPLATFORM_WEIXIN
        RefreshSafeAreaCoverTone();
#endif
        UpdateLandscapeGuard();
        UpdateFrameRateStatus();
        if (_pendingTextRefreshFrames > 0)
        {
            _pendingTextRefreshFrames--;
            RefreshTextGeometry(_pendingTextRefreshRoot);
            if (_pendingTextRefreshFrames == 0)
                _pendingTextRefreshRoot = null;
        }

        if (_gm != null && _gm.State == GameState.Playing
            && IsSingleContractPresentation(AIShadowRunner.Instance))
        {
            RefreshSingleContractTeaching();
        }
        else if (_controlHint != null && _controlHint.activeSelf)
        {
            _controlHintTimer -= Time.unscaledDeltaTime;
            if (_controlHintTimer <= 0f)
                _controlHint.SetActive(false);
        }

        // Active power-up display
        if (_buffGroup != null && _gm != null && _gm.State == GameState.Playing)
        {
            string powerUpStatus = PowerUpController.Instance != null
                ? PowerUpController.Instance.GetStatusText()
                : "";
            bool active = !string.IsNullOrEmpty(powerUpStatus)
                          || _gm.BuffTimeRemaining > 0f;
            if (_buffGroup.activeSelf != active)
                _buffGroup.SetActive(active);
            if (active && _buffText != null)
                _buffText.text = !string.IsNullOrEmpty(powerUpStatus)
                    ? powerUpStatus
                    : string.Format("{0} {1:F1}s", _gm.BuffName ?? "Buff", _gm.BuffTimeRemaining);
        }

        if (_gm != null && _gm.State == GameState.Playing
            && Time.unscaledTime >= _nextDuelRefresh)
        {
            _nextDuelRefresh = Time.unscaledTime + 0.1f;
            RefreshDuelHud();
        }

        if (_gm != null && _gm.State == GameState.Menu
            && (_menuRouter == null || _menuRouter.IsHome)
            && Time.unscaledTime >= _nextMenuRefresh)
        {
            _nextMenuRefresh = Time.unscaledTime + 0.5f;
            RefreshMenuPresentation();
        }

        if (_duelFeedbackText != null && _duelFeedbackText.gameObject.activeSelf)
        {
            _duelFeedbackTimer -= Time.unscaledDeltaTime;
            if (IsSingleContractPresentation(AIShadowRunner.Instance))
            {
                Color feedbackColor = _duelFeedbackText.color;
                feedbackColor.a = EchoHudView.FeedbackAlpha(
                    EchoRunPresentation.SingleContractFeedbackDurationSeconds
                    - _duelFeedbackTimer, EchoRunAccessibility.ReducedMotion);
                _duelFeedbackText.color = feedbackColor;
            }
            if (_duelFeedbackTimer <= 0f)
                _duelFeedbackText.gameObject.SetActive(false);
        }
    }

    // ═══════════════════════════════════════════════════
    //  Canvas
    // ═══════════════════════════════════════════════════

    void EnsureCanvas()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject cgo = new GameObject("Canvas");
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        _canvasScaler = canvas.GetComponent<CanvasScaler>();
        if (_canvasScaler == null)
            _canvasScaler = canvas.gameObject.AddComponent<CanvasScaler>();
        _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _canvasScaler.referenceResolution = UILayoutRules.GetReferenceResolution(
            Screen.width, Screen.height);
        _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        Transform existingSafeArea = canvas.transform.Find("SafeArea");
        if (existingSafeArea != null)
        {
            _safeAreaRoot = existingSafeArea.GetComponent<RectTransform>();
        }
        else
        {
            GameObject safeArea = new GameObject("SafeArea", typeof(RectTransform));
            safeArea.transform.SetParent(canvas.transform, false);
            _safeAreaRoot = safeArea.GetComponent<RectTransform>();
            Stretch(_safeAreaRoot);
        }
        ApplySafeArea(true);

        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    // ═══════════════════════════════════════════════════
    //  Menu Panel
    // ═══════════════════════════════════════════════════

    void CreateMenuPanel()
    {
        _menuPanel = NewPanel("MenuPanel", Color.clear);
        CreateMenuBackground();

        var titleScrim = new GameObject("MenuTitleScrim", typeof(VerticalScrim));
        titleScrim.transform.SetParent(_menuPanel.transform, false);
        VerticalScrim scrim = titleScrim.GetComponent<VerticalScrim>();
        scrim.color = WithAlpha(PageBackdrop, .62f);
        scrim.raycastTarget = false;
        scrim.rectTransform.anchorMin = new Vector2(0f, .70f);
        scrim.rectTransform.anchorMax = Vector2.one;
        scrim.rectTransform.offsetMin = scrim.rectTransform.offsetMax = Vector2.zero;

        GameObject veil = new GameObject("MenuReadabilityVeil", typeof(Image));
        veil.transform.SetParent(_menuPanel.transform, false);
        Image veilImage = veil.GetComponent<Image>();
        veilImage.color = WithAlpha(PageBackdrop, 0.99f);
        veilImage.raycastTarget = false;
        UIEdgeTreatment.Surface(veilImage, EchoRunUITheme.PageRule);
        _menuReadabilityVeil = veil.GetComponent<RectTransform>();

        GameObject header = new GameObject("MenuDockRule", typeof(Image));
        header.transform.SetParent(_menuPanel.transform, false);
        header.GetComponent<Image>().color = WithAlpha(PageEcho, 0.78f);
        header.GetComponent<Image>().raycastTarget = false;
        _menuHeaderSurface = header.GetComponent<RectTransform>();

        _menuProtocolText = MakeText("Protocol", _menuPanel.transform,
            "本机回声 · 记录来自你的跑法", 16, TextAnchor.MiddleCenter);
        _menuProtocolText.color = TextMuted;

        _menuTitleText = MakeText("Title", _menuPanel.transform, "影迹回声",
            76, TextAnchor.MiddleLeft);
        _menuTitleText.color = PageInk;
        _menuTitleText.fontStyle = FontStyle.Bold;
        _menuTitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _menuTitleText.verticalOverflow = VerticalWrapMode.Overflow;

        _menuEnglishText = MakeText("EnglishTitle", _menuPanel.transform,
            "", 23, TextAnchor.MiddleLeft);
        if (_titleFont != null) _menuEnglishText.font = _titleFont;
        _menuEnglishText.gameObject.SetActive(false);

        _menuTaglineText = MakeText("Tagline", _menuPanel.transform,
            "你的过去，正在追上你", 25, TextAnchor.MiddleLeft);
        _menuTaglineText.color = PageMuted;

        _menuGenerationText = MakeText("EchoGeneration", _menuPanel.transform,
            "你的操作，会变成下一局的对手", 30, TextAnchor.MiddleLeft);
        _menuGenerationText.color = TextPrimary;
        _menuGenerationText.fontStyle = FontStyle.Bold;

        _menuLearnedText = MakeBriefLine("EchoLearned",
            "最近选路：还需要观察", 0f, PageEcho);
        _menuRuleText = MakeBriefLine("EchoRule",
            "尝试选路、跳跃和滑铲，让回声认识你的跑法",
            0f, PageMuted);
        _menuObjectiveText = MakeBriefLine("EchoObjective",
            "跑到终点；观察充分后形成下一局的回声",
            0f, PageInk);
        _menuGenerationText.color = PageInk;
        _menuProtocolText.color = PageMuted;

        _startBtn = MakeButton("StartBtn", _menuPanel.transform, "开始第一局", 28,
            new Vector2(0.19f, 0.245f), new Vector2(520f, 78f),
            EchoRunUITheme.ActionAccent, EchoRunUITheme.ActionAccentDark, Ink);
        _startBtn.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        _startBtn.onClick.AddListener(StartGameFromHome);

        _settingsBtn = MakeButton("SettingsBtn", _menuPanel.transform, "设置", 24,
            new Vector2(0.28f, 0.095f), new Vector2(180f, 56f),
            WithAlpha(PageRaised, 0.80f), PageSelected, PageInk);
        _settingsBtn.onClick.AddListener(ShowSettings);

        _characterBtn = MakeButton("CharacterBtn", _menuPanel.transform, "跑者", 24,
            new Vector2(0.10f, 0.095f), new Vector2(180f, 56f),
            WithAlpha(PageRaised, 0.80f), PageSelected, PageInk);
        _characterBtn.onClick.AddListener(ShowCharacter);

        _reportBtn = MakeButton("EchoReportBtn", _menuPanel.transform, "回声报告", 24,
            new Vector2(0.30f, 0.08f), new Vector2(170f, 56f),
            WithAlpha(PageRaised, 0.80f), PageSelected, PageInk);
        _reportBtn.onClick.AddListener(ShowEchoReport);
        _reportBtn.gameObject.SetActive(false);

        LayoutMenu(false, false);
        _menuPanel.SetActive(false);
    }

    void CreateMenuBackground()
    {
        GameObject background = new GameObject("MenuSceneVeil",
            typeof(RawImage));
        background.transform.SetParent(_menuPanel.transform, false);
        _menuBackground = background.GetComponent<RawImage>();
        _menuBackground.raycastTarget = false;
        Stretch(_menuBackground.rectTransform);
        _menuBackground.transform.SetAsFirstSibling();
        FitMenuBackground();
    }

    void FitMenuBackground()
    {
        Rect safe = UILayoutRules.NormalizeSafeArea(
            WeChatSafeArea.Resolve(Screen.safeArea, Screen.width, Screen.height),
            Screen.width, Screen.height);
        FitMenuBackgroundToViewport(Mathf.RoundToInt(safe.width),
            Mathf.RoundToInt(safe.height));
    }

    void FitMenuBackgroundToViewport(int width, int height)
    {
        if (_menuBackground == null || width <= 0 || height <= 0) return;
        _menuBackground.texture = null;
        _menuBackground.color = WithAlpha(Backdrop, 0.06f);
        _menuBackground.uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    Text MakeBriefLine(string name, string content, float anchorY, Color color)
    {
        Text line = MakeText(name, _menuPanel.transform, content, 24,
            TextAnchor.MiddleLeft);
        line.color = color;
        line.horizontalOverflow = HorizontalWrapMode.Wrap;
        line.verticalOverflow = VerticalWrapMode.Truncate;
        line.resizeTextForBestFit = true;
        line.resizeTextMinSize = 19;
        line.resizeTextMaxSize = 24;
        AnchorText(line.rectTransform, 0.5f, anchorY, 840, 54);
        return line;
    }

    void LayoutMenu(bool portrait, bool largeTargets)
    {
        if (_menuPanel == null) return; // Safe-area setup also runs before panel creation.
        Vector2 menuViewport = ((RectTransform)_menuPanel.transform).rect.size;
        if (_menuHeaderSurface != null)
        {
            _menuHeaderSurface.gameObject.SetActive(portrait);
            _menuHeaderSurface.anchorMin = new Vector2(0.085f, 0.341f);
            _menuHeaderSurface.anchorMax = new Vector2(0.18f, 0.343f);
            _menuHeaderSurface.offsetMin = Vector2.zero;
            _menuHeaderSurface.offsetMax = Vector2.zero;
        }
        if (portrait)
        {
            if (_menuReadabilityVeil != null)
            {
                _menuReadabilityVeil.anchorMin = Vector2.zero;
                _menuReadabilityVeil.anchorMax = new Vector2(1f, 0.347f);
                _menuReadabilityVeil.offsetMin = new Vector2(28f, 8f);
                _menuReadabilityVeil.offsetMax = new Vector2(-28f, 0f);
            }
            // Keep the runner fully visible above one compact action dock.
            // The detailed objective is available on the report page.
            _menuObjectiveText.gameObject.SetActive(false);
            _menuProtocolText.gameObject.SetActive(false);
            LayoutHomeText(_menuTitleText, 0.885f, 0f, 112f, 82);
            LayoutHomeText(_menuTaglineText, 0.824f, 0f, 54f, 30);
            LayoutHomeText(_menuGenerationText, 0.295f, 0f, 100f, 40);
            LayoutHomeText(_menuLearnedText, 0.240f, 0f, 60f, 30);
            LayoutHomeText(_menuRuleText, 0.179f, 0f, 116f, 27);
            SetButtonLayout(_startBtn, new Vector2(0.5f, 0.105f),
                new Vector2(900f, 116f));
            SetButtonLayout(_characterBtn, new Vector2(0.27f, 0.037f),
                new Vector2(410f, 104f));
            SetButtonLayout(_settingsBtn, new Vector2(0.73f, 0.037f),
                new Vector2(410f, 104f));
            if (_reportBtn != null && _reportBtn.gameObject.activeSelf)
            {
                SetButtonLayout(_characterBtn, new Vector2(0.21f, 0.037f), new Vector2(270f, 104f));
                SetButtonLayout(_settingsBtn, new Vector2(0.50f, 0.037f), new Vector2(270f, 104f));
                SetButtonLayout(_reportBtn, new Vector2(0.79f, 0.037f), new Vector2(270f, 104f));
            }
            SetHomeButtonFont(_startBtn, 46);
            SetHomeButtonFont(_characterBtn, 32);
            SetHomeButtonFont(_settingsBtn, 32);
            SetHomeButtonFont(_reportBtn, 32);
            return;
        }
        _menuObjectiveText.gameObject.SetActive(false);
        _menuProtocolText.gameObject.SetActive(false);
        // Restore authored desktop typography after rotating out of portrait.
        SetHomeFont(_menuTitleText, 68);
        SetHomeFont(_menuEnglishText, 23);
        SetHomeFont(_menuTaglineText, 25);
        SetHomeFont(_menuGenerationText, 28);
        SetHomeFont(_menuLearnedText, 24);
        SetHomeFont(_menuRuleText, 24);
        SetHomeFont(_menuObjectiveText, 24);
        SetHomeFont(_menuProtocolText, 16);
        SetHomeButtonFont(_startBtn, 28);
        SetHomeButtonFont(_characterBtn, 24);
        SetHomeButtonFont(_settingsBtn, 24);
        SetHomeButtonFont(_reportBtn, 24);
        const float x = 0.185f;
        float width = Mathf.Max(360f, menuViewport.x * 0.33f - 64f);
        if (_menuReadabilityVeil != null)
        {
            _menuReadabilityVeil.anchorMin = new Vector2(0.02f, 0.04f);
            _menuReadabilityVeil.anchorMax = new Vector2(0.35f, 0.59f);
            _menuReadabilityVeil.offsetMin = Vector2.zero;
            _menuReadabilityVeil.offsetMax = Vector2.zero;
        }
        if (_menuProtocolText != null)
            AnchorText(_menuProtocolText.rectTransform, x,
                0.16f, width, 42f);
        if (_menuTitleText != null)
            AnchorText(_menuTitleText.rectTransform, x,
                0.90f, width, 112f);
        if (_menuEnglishText != null)
            AnchorText(_menuEnglishText.rectTransform, x,
                portrait ? 0.775f : 0.725f, width, 38f);
        if (_menuTaglineText != null)
            AnchorText(_menuTaglineText.rectTransform, x,
                0.815f, width, 50f);
        if (_menuGenerationText != null)
            AnchorText(_menuGenerationText.rectTransform, x,
                0.52f, width, 80f);
        if (_menuLearnedText != null)
            AnchorText(_menuLearnedText.rectTransform, x,
                0.435f, width, 64f);
        if (_menuRuleText != null)
            AnchorText(_menuRuleText.rectTransform, x,
                0.325f, width, 114f);
        if (_menuObjectiveText != null)
            AnchorText(_menuObjectiveText.rectTransform, x,
                0.35f, width, 70f);

        SetButtonLayout(_startBtn,
            new Vector2(x, 0.185f), new Vector2(width,
                largeTargets ? 104f : 78f));
        bool hasReport = _reportBtn != null && _reportBtn.gameObject.activeSelf;
        float navigationWidth = (width - (hasReport ? 24f : 12f)) / (hasReport ? 3f : 2f);
        float navigationStep = (navigationWidth + 12f) / Mathf.Max(1f, menuViewport.x);
        float navigationStart = x - navigationStep * (hasReport ? 1f : .5f);
        SetButtonLayout(_characterBtn,
            new Vector2(navigationStart, 0.085f),
            new Vector2(navigationWidth, largeTargets ? 104f : 56f));
        SetButtonLayout(_settingsBtn,
            new Vector2(navigationStart + navigationStep, 0.085f),
            new Vector2(navigationWidth, largeTargets ? 104f : 56f));
        SetButtonLayout(_reportBtn,
            new Vector2(navigationStart + navigationStep * 2f, 0.085f),
            new Vector2(navigationWidth, largeTargets ? 104f : 56f));
    }

    static void SetHomeFont(Text text, int size)
    {
        if (text == null) return;
        text.resizeTextForBestFit = false;
        EchoRunAccessibility.SetBaseFontSize(text, size);
    }

    static void SetHomeButtonFont(Button button, int size)
    {
        if (button != null) SetHomeFont(button.GetComponentInChildren<Text>(), size);
    }

    static void LayoutHomeText(Text text, float anchorY, float offsetY,
        float height, int size)
    {
        if (text == null) return;
        SetHomeFont(text, size);
        AnchorText(text.rectTransform, 0.5f, anchorY, 900f, height);
        text.rectTransform.anchoredPosition = new Vector2(0f, offsetY);
    }

    void CreateEchoReportPanel()
    {
        _reportPanel = NewPanel("EchoReportPanel", PageBackdrop);
        Text title = MakeText("EchoReportTitle", _reportPanel.transform,
            "已保存的回声", 42, TextAnchor.MiddleCenter);
        title.color = PageInk;
        AnchorText(title.rectTransform, .5f, .88f, 900f, 90f);
        _reportText = MakeText("EchoReportText", _reportPanel.transform,
            "", 30, TextAnchor.UpperLeft);
        _reportText.color = PageInk;
        _reportText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _reportText.verticalOverflow = VerticalWrapMode.Truncate;
        _reportText.lineSpacing = 1.25f;
        _reportText.rectTransform.anchorMin = new Vector2(.08f, .12f);
        _reportText.rectTransform.anchorMax = new Vector2(.92f, .76f);
        _reportText.rectTransform.offsetMin = _reportText.rectTransform.offsetMax = Vector2.zero;
        _reportBackBtn = MakeButton("EchoReportBack", _reportPanel.transform,
            "返回", 30, Vector2.up, new Vector2(180f, 104f),
            PageRaised, PageSelected, PageInk);
        SetTopLeftButtonLayout(_reportBackBtn, new Vector2(180f, 104f));
        _reportBackBtn.onClick.AddListener(() => _menuRouter?.BackToHome());
        _reportPanel.SetActive(false);
    }

    public static string BuildSavedEchoReport(ActiveEchoIdentity identity)
    {
        if (identity == null) return "";
        ActiveEchoIdentity saved = identity.Clone();
        if (!saved.IsSemanticallyValid()) return "";
        string memory = saved.memoryContract != null
            ? saved.memoryContract.BuildMemoryText()
            : "路线记忆仍需观察";
        return "第 " + saved.generation + " 代回声 · 当前已保存\n\n"
            + memory + "\n\n"
            + "动作记录：跳跃 " + saved.style.jumpActionSamples
            + " 次 · 滑铲 " + saved.style.slideActionSamples + " 次\n"
            + "路线证据：" + (saved.memoryContract != null
                ? saved.memoryContract.evidenceCount : 0) + " 次\n\n"
            + (saved.RequiresRouteCalibration
                ? "下一局继续观察。动作与选路需要在本局内重新记录，达到条件并到达终点后更新。"
                : "下一局可挑战这代回声。赢下本局、达到更新条件并保存成功后，新代才会生效。");
    }

    void ShowEchoReport()
    {
        if (_gm == null || !_gm.UsesSingleContractRules) return;
        string report = BuildSavedEchoReport(EchoRunSaveSystem.GetActiveEchoIdentity());
        if (string.IsNullOrEmpty(report) || _reportText == null) return;
        RefreshEchoReportNavigation();
        _reportText.text = report;
        _menuRouter?.Show(MenuScreen.EchoReport);
        ScheduleTextRefresh(_reportPanel.transform);
    }

    void RefreshEchoReportNavigation()
    {
        bool ownsReport = _gm != null && _gm.UsesSingleContractRules;
        if (ownsReport && !_ownsEchoReportRoute && _menuRouter != null
            && _reportPanel != null)
        {
            _ownsEchoReportRoute = true;
            _menuRouter.Register(MenuScreen.EchoReport, _reportPanel, _reportBackBtn);
        }
        else if (!ownsReport)
        {
            _ownsEchoReportRoute = false;
            if (_reportPanel != null) _reportPanel.SetActive(false);
        }

        string report = ownsReport
            ? BuildSavedEchoReport(EchoRunSaveSystem.GetActiveEchoIdentity()) : "";
        bool hasReport = !string.IsNullOrEmpty(report);
        if (_reportBtn != null && _reportBtn.gameObject.activeSelf != hasReport)
        {
            _reportBtn.gameObject.SetActive(hasReport);
            LayoutMenu(UILayoutRules.IsCompactPortrait(Screen.width, Screen.height),
                UsesTouchLayout() || Screen.height > Screen.width);
        }
        if (ownsReport && _reportPanel != null && _reportPanel.activeSelf)
        {
            if (!hasReport) _menuRouter?.BackToHome();
            else if (_reportText != null) _reportText.text = report;
        }
    }

    // ═══════════════════════════════════════════════════
    //  Settings Panel (sub-menu)
    // ═══════════════════════════════════════════════════

    void CreateSettingsPanel()
    {
        _settingsPanel = NewPanel("SettingsPanel", PageBackdrop);
        AddPageArtwork(_settingsPanel.transform);

        // ScrollRect setup
        _settingsScroll = _settingsPanel.AddComponent<ScrollRect>();
        _settingsScroll.horizontal = false;
        _settingsScroll.vertical = true;
        _settingsScroll.movementType = ScrollRect.MovementType.Clamped;

        // Viewport
        GameObject viewport = new GameObject("Viewport", typeof(Image), typeof(Mask));
        viewport.transform.SetParent(_settingsPanel.transform, false);
        viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        RectTransform vpRT = viewport.GetComponent<RectTransform>();
        vpRT.anchorMin = new Vector2(0, 0); vpRT.anchorMax = new Vector2(1, 1);
        vpRT.offsetMin = new Vector2(32, 24); vpRT.offsetMax = new Vector2(-32, -132);

        // Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        _settingsContent = content.AddComponent<RectTransform>();
        _settingsContent.anchorMin = new Vector2(0.5f, 1f);
        _settingsContent.anchorMax = new Vector2(0.5f, 1f);
        _settingsContent.pivot = new Vector2(0.5f, 1f);
        _settingsContent.sizeDelta = new Vector2(1020, 1260);
        _settingsContent.anchoredPosition = Vector2.zero;

        _settingsScroll.viewport = vpRT;
        _settingsScroll.content = _settingsContent;

        Transform c = content.transform;
        foreach (string section in new[] { "AudioCard", "DisplayCard",
                     "DifficultyCard", "CameraCard", "AccessibilityCard", "RunGuideCard" })
        {
            var card = new GameObject(section, typeof(Image));
            card.transform.SetParent(c, false);
            Image surface = card.GetComponent<Image>();
            surface.color = PageSurface;
            surface.raycastTarget = false;
            ApplyRounded(surface);
            UIEdgeTreatment.Surface(surface, EchoRunUITheme.PageRule);
        }

        Text title = MakeText("SettingsTitle", _settingsPanel.transform, "设置", 40, TextAnchor.MiddleCenter);
        title.color = PageInk;
        title.fontStyle = FontStyle.Bold;
        title.verticalOverflow = VerticalWrapMode.Overflow;
        AnchorText(title.GetComponent<RectTransform>(), 0.5f, 1f, 400, 80);
        title.rectTransform.anchoredPosition = new Vector2(0, -64);
        MakeLabel("AudioSectionLabel", c, "声音", new Vector2(0.5f, 0.90f));

        _masterSlider = MakeSlider("MasterVolumeSlider", c,
            new Vector2(0.5f, 0.81f));
        float savedMaster = PlayerPrefs.GetFloat("MasterVolume", 1f);
        _masterSlider.value = savedMaster;
        _masterSlider.onValueChanged.AddListener(v =>
        {
            AudioManager.Instance?.SetMasterVolume(v);
            RefreshVolumeLabels();
        });

        _bgmSlider = MakeSlider("BgmSlider", c, new Vector2(0.5f, 0.68f));
        float savedBgm = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        _bgmSlider.value = savedBgm;
        _bgmSlider.onValueChanged.AddListener(v =>
        {
            AudioManager.Instance?.SetMusicVolume(v);
            RefreshVolumeLabels();
        });

        _sfxSlider = MakeSlider("SfxSlider", c, new Vector2(0.5f, 0.55f));
        float savedSfx = PlayerPrefs.GetFloat("SfxVolume", 1f);
        _sfxSlider.value = savedSfx;
        _sfxSlider.onValueChanged.AddListener(v =>
        {
            AudioManager.Instance?.SetSfxVolume(v);
            RefreshVolumeLabels();
        });

        _muteBtn = MakeSmallButton("AudioMute", c, "一键静音",
            new Vector2(0.5f, 0.47f), new Vector2(220, 60), PageRaised);
        _muteBtn.onClick.AddListener(ToggleAudioMute);

        MakeLabel("FpsLabel", c, "画面帧率", new Vector2(0.34f, 0.42f));
        _fpsStatusText = MakeText("FpsStatus", c, "目标 60 · 正在测量…", 24,
            TextAnchor.MiddleRight);
        _fpsStatusText.color = PageMuted;
        Text fpsHint = MakeText("FpsHint", c,
            "高帧率需要屏幕与设备支持，实际以运行帧率为准。", 22, TextAnchor.UpperLeft);
        fpsHint.color = PageMuted;
        AnchorText(_fpsStatusText.rectTransform, 0.72f, 0.42f, 340, 40);
        _fps30Btn  = MakeSmallButton("Fps30", c, "30",
            new Vector2(0.25f, 0.36f), new Vector2(140, 60), PageRaised);
        _fps60Btn  = MakeSmallButton("Fps60", c, "60",
            new Vector2(0.5f, 0.36f), new Vector2(140, 60), PageRaised);
        _fps120Btn = MakeSmallButton("Fps120", c, "120",
            new Vector2(0.75f, 0.36f), new Vector2(140, 60), PageRaised);

        _fps30Btn.onClick.AddListener(() => { _gm.SetFrameRate(30);  HighlightFps(); });
        _fps60Btn.onClick.AddListener(() => { _gm.SetFrameRate(60);  HighlightFps(); });
        _fps120Btn.onClick.AddListener(() => { _gm.SetFrameRate(120); HighlightFps(); });
        if (_gm != null && !_gm.SupportsHighFrameRate)
        {
            _fps120Btn.gameObject.SetActive(false);
            SetButtonAnchor(_fps30Btn, new Vector2(0.36f, 0.36f));
            SetButtonAnchor(_fps60Btn, new Vector2(0.64f, 0.36f));
        }
        HighlightFps();

        MakeLabel("DifficultyLabel", c, "跑酷难度",
            new Vector2(0.34f, 0.28f));
        _difficultyStatusText = MakeText("DifficultyStatus", c, "", 24,
            TextAnchor.MiddleRight);
        _difficultyStatusText.color = PageMuted;
        AnchorText(_difficultyStatusText.rectTransform, 0.72f, 0.28f,
            360, 40);
        _difficultyRelaxedBtn = MakeSmallButton("DifficultyRelaxed", c,
            "休闲", new Vector2(0.25f, 0.22f), new Vector2(180, 60),
            PageRaised);
        _difficultyStandardBtn = MakeSmallButton("DifficultyStandard", c,
            "标准", new Vector2(0.5f, 0.22f), new Vector2(180, 60),
            PageRaised);
        _difficultyIntenseBtn = MakeSmallButton("DifficultyIntense", c,
            "高压", new Vector2(0.75f, 0.22f), new Vector2(180, 60),
            PageRaised);
        _difficultyRelaxedBtn.onClick.AddListener(() =>
            SetRunDifficulty(RunDifficultyLevel.Relaxed));
        _difficultyStandardBtn.onClick.AddListener(() =>
            SetRunDifficulty(RunDifficultyLevel.Standard));
        _difficultyIntenseBtn.onClick.AddListener(() =>
            SetRunDifficulty(RunDifficultyLevel.Intense));
        HighlightDifficulty();

        MakeLabel("CameraViewLabel", c, "跑道视角",
            new Vector2(0.34f, 0.17f));
        _cameraLowBtn = MakeSmallButton("CameraViewLow", c,
            "低视角", new Vector2(0.32f, 0.12f), new Vector2(260, 60),
            PageRaised);
        _cameraHighBtn = MakeSmallButton("CameraViewHigh", c,
            "高视角", new Vector2(0.68f, 0.12f), new Vector2(260, 60),
            PageRaised);
        _cameraLowBtn.onClick.AddListener(() =>
            SetCameraViewHeight(CameraViewHeight.Low));
        _cameraHighBtn.onClick.AddListener(() =>
            SetCameraViewHeight(CameraViewHeight.High));
        Text cameraHint = MakeText("CameraViewHint", c,
            "高视角能多看到前方道路，随时可以切换。", 22,
            TextAnchor.UpperLeft);
        cameraHint.color = PageMuted;
        HighlightCameraViewHeight();

        MakeLabel("AccessibilityLabel", c, "辅助显示", new Vector2(0.34f, 0.14f));
        _largeTextBtn = MakeSmallButton("LargeText", c, "大字",
            new Vector2(0.22f, 0.08f), new Vector2(210, 60), PageRaised);
        _highContrastBtn = MakeSmallButton("HighContrast", c, "高对比",
            new Vector2(0.50f, 0.08f), new Vector2(210, 60), PageRaised);
        _reducedMotionBtn = MakeSmallButton("ReducedMotion", c, "减少动态",
            new Vector2(0.78f, 0.08f), new Vector2(230, 60), PageRaised);
        _largeTextBtn.onClick.AddListener(() =>
            EchoRunAccessibility.SetLargeText(!EchoRunAccessibility.LargeText));
        _highContrastBtn.onClick.AddListener(() =>
            EchoRunAccessibility.SetHighContrast(!EchoRunAccessibility.HighContrast));
        _reducedMotionBtn.onClick.AddListener(() =>
            EchoRunAccessibility.SetReducedMotion(!EchoRunAccessibility.ReducedMotion));
        RefreshAccessibilityButtons();

        MakeLabel("RunGuideLabel", c, "操作与选路", new Vector2(.5f, 0f));
        _runGuideText = MakeText("RunGuideText", c, "", 24, TextAnchor.UpperLeft);
        _runGuideText.color = PageInk;
        _runGuideText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _runGuideText.verticalOverflow = VerticalWrapMode.Overflow;
        _runGuideText.lineSpacing = 1.15f;
        RefreshRunGuide();

        _settingsBackBtn = MakeButton("SettingsBackBtn",
            _settingsPanel.transform, "返回", 34,
            new Vector2(0f, 1f), new Vector2(280, 76),
            PageRaised, PageSelected, PageInk);
        SetTopLeftButtonLayout(_settingsBackBtn, new Vector2(280f, 76f));
        _settingsBackBtn.transform.SetAsLastSibling();
        _settingsBackBtn.onClick.AddListener(HideSettings);

        // Create the three sound readouts after every other setting label.
        // Tuanjie legacy dynamic fonts can rebuild their atlas while this page
        // is being assembled and invalidate early direct-child Text geometry.
        CreateVolumeReadouts(c);
        RefreshVolumeLabels();
        LayoutSettings(UILayoutRules.IsCompactPortrait(Screen.width, Screen.height));

        _settingsPanel.SetActive(false);
    }

    void CreateVolumeReadouts(Transform parent)
    {
        if (_font != null)
            _font.RequestCharactersInTexture(
                "主音量音乐效0123456789%· ", 28, FontStyle.Bold);

        _masterValueText = MakeVolumeReadout("MasterVolumeValue", parent,
            "主音量 · 100%", 0.86f);
        _bgmValueText = MakeVolumeReadout("BgmValue", parent,
            "音乐音量 · 50%", 0.73f);
        _sfxValueText = MakeVolumeReadout("SfxValue", parent,
            "音效音量 · 100%", 0.60f);
    }

    Text MakeVolumeReadout(string name, Transform parent, string value,
        float anchorY)
    {
        Text readout = MakeText(name, parent, value, 28,
            TextAnchor.MiddleCenter);
        readout.color = Primary;
        readout.fontStyle = FontStyle.Bold;
        readout.horizontalOverflow = HorizontalWrapMode.Overflow;
        readout.verticalOverflow = VerticalWrapMode.Overflow;
        AnchorText(readout.rectTransform, 0.5f, anchorY, 520f, 48f);
        readout.transform.SetAsLastSibling();
        return readout;
    }

    void ShowSettings()
    {
        bool observationDetails = _gm != null
            && (_gm.State == GameState.Playing || _gm.State == GameState.Paused)
            && _gm.IsSingleContractRun
            && AIShadowRunner.Instance != null && !AIShadowRunner.Instance.HasActiveOpponent;
        if (_gm != null && (_gm.State == GameState.Playing || _gm.State == GameState.Paused))
        {
            if (_gm.IsDeathSequence) return;
            if (_gm.State == GameState.Playing) _gm.Pause();
            InputManager.Instance?.ClearInput();
            _runSettingsOpen = true;
            _pausePanel.SetActive(false);
            _hudPanel.SetActive(false);
            _settingsPanel.SetActive(true);
            _settingsPanel.transform.SetAsLastSibling();
            SelectForNavigation(_masterSlider);
        }
        else if (_menuRouter != null) _menuRouter.Show(MenuScreen.Settings);
        else
        {
            if (_menuPanel != null) _menuPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(true);
        }
        AudioManager audio = AudioManager.Instance;
        if (audio != null)
        {
            _masterSlider.SetValueWithoutNotify(audio.masterVolume);
            _bgmSlider.SetValueWithoutNotify(audio.musicVolume);
            _sfxSlider.SetValueWithoutNotify(audio.sfxVolume);
        }
        RefreshVolumeLabels();
        RefreshRunGuide();
        RefreshVolumeReadoutGeometry();
        RefreshTextGeometry(_settingsPanel != null
            ? _settingsPanel.transform : null);
        ScheduleTextRefresh(_settingsPanel != null
            ? _settingsPanel.transform : null);
        Canvas.ForceUpdateCanvases();
        if (_settingsScroll != null)
            _settingsScroll.verticalNormalizedPosition = observationDetails ? 0f : 1f;
        HighlightDifficulty();
        HighlightCameraViewHeight();
    }

    void RefreshVolumeReadoutGeometry()
    {
        Text[] readouts = { _masterValueText, _bgmValueText, _sfxValueText };
        foreach (Text readout in readouts)
        {
            if (readout == null) continue;
            readout.gameObject.SetActive(true);
            readout.enabled = false;
            readout.enabled = true;
            readout.SetAllDirty();
            readout.transform.SetAsLastSibling();
        }
        Canvas.ForceUpdateCanvases();
    }

    void HideSettings()
    {
        EchoRunSaveSystem.SaveLegacyState();
        if (_runSettingsOpen)
        {
            _runSettingsOpen = false;
            InputManager.Instance?.ClearInput();
            OnGameStateChanged(_gm.State);
            return;
        }
        if (_menuRouter != null) _menuRouter.BackToHome();
        else
        {
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            if (_menuPanel != null) _menuPanel.SetActive(true);
        }
    }

    public bool TryCloseRunSettings()
    {
        if (!_runSettingsOpen) return false;
        HideSettings();
        return true;
    }

    void HighlightFps()
    {
        int cur = _gm != null ? _gm.GetFrameRate() : 60;
        Color active = SettingsSelected;
        Color inactive = SettingsControl;
        SetBtnColor(_fps30Btn,  cur == 30  ? active : inactive);
        SetBtnColor(_fps60Btn,  cur == 60  ? active : inactive);
        SetBtnColor(_fps120Btn, cur == 120 ? active : inactive);
        SetButtonLabel(_fps30Btn, cur == 30 ? "✓ 30" : "30");
        SetButtonLabel(_fps60Btn, cur == 60 ? "✓ 60" : "60");
        SetButtonLabel(_fps120Btn, cur == 120 ? "✓ 120" : "120");
        _fpsSampleElapsed = 0f;
        _fpsSampleFrames = 0;
        if (_fpsStatusText != null)
            _fpsStatusText.text = $"目标 {cur} · 正在测量…";
    }

    void SetRunDifficulty(RunDifficultyLevel level)
    {
        RunDifficultySettings.Set(level);
        EchoRunSaveSystem.SaveLegacyState();
        HighlightDifficulty();
    }

    void SetCameraViewHeight(CameraViewHeight height)
    {
        EchoRunSaveSystem.SaveCameraViewHeight(height);
        HighlightCameraViewHeight();
    }

    void HighlightCameraViewHeight()
    {
        CameraViewHeight height = CameraViewSettings.Current;
        SetBtnColor(_cameraLowBtn,
            height == CameraViewHeight.Low ? SettingsSelected : SettingsControl);
        SetBtnColor(_cameraHighBtn,
            height == CameraViewHeight.High ? SettingsSelected : SettingsControl);
        SetButtonLabel(_cameraLowBtn,
            height == CameraViewHeight.Low ? "✓ 低视角" : "低视角");
        SetButtonLabel(_cameraHighBtn,
            height == CameraViewHeight.High ? "✓ 高视角" : "高视角");
    }

    void HighlightDifficulty()
    {
        RunDifficultyLevel level = RunDifficultySettings.Current;
        SetBtnColor(_difficultyRelaxedBtn,
            level == RunDifficultyLevel.Relaxed ? PrimaryStrong : SurfaceRaised);
        SetBtnColor(_difficultyStandardBtn,
            level == RunDifficultyLevel.Standard ? PrimaryStrong : SurfaceRaised);
        SetBtnColor(_difficultyIntenseBtn,
            level == RunDifficultyLevel.Intense ? PrimaryStrong : SurfaceRaised);
        SetButtonLabel(_difficultyRelaxedBtn,
            level == RunDifficultyLevel.Relaxed ? "✓ 休闲" : "休闲");
        SetButtonLabel(_difficultyStandardBtn,
            level == RunDifficultyLevel.Standard ? "✓ 标准" : "标准");
        SetButtonLabel(_difficultyIntenseBtn,
            level == RunDifficultyLevel.Intense ? "✓ 高压" : "高压");
        if (_difficultyStatusText != null)
            _difficultyStatusText.text = _runSettingsOpen
                ? "下次开跑生效 · 本局保持原难度"
                : RunDifficultySettings.Description(level);
    }

    void UpdateFrameRateStatus()
    {
        if (_fpsStatusText == null || _settingsPanel == null
            || !_settingsPanel.activeInHierarchy)
        {
            _fpsSampleElapsed = 0f;
            _fpsSampleFrames = 0;
            return;
        }

        _fpsSampleElapsed += Time.unscaledDeltaTime;
        _fpsSampleFrames++;
        if (_fpsSampleElapsed < 0.5f) return;

        int target = _gm != null ? _gm.GetFrameRate() : 60;
        int actual = Mathf.RoundToInt(_fpsSampleFrames / _fpsSampleElapsed);
        _fpsStatusText.text = $"目标 {target} · 实际约 {actual} FPS";
        _fpsSampleElapsed = 0f;
        _fpsSampleFrames = 0;
    }

    void RefreshVolumeLabels()
    {
        if (_masterValueText != null && _masterSlider != null)
            _masterValueText.text = "主音量 · " + Mathf.RoundToInt(
                _masterSlider.value * 100f) + "%";
        if (_bgmValueText != null && _bgmSlider != null)
            _bgmValueText.text = "音乐音量 · " +
                Mathf.RoundToInt(_bgmSlider.value * 100f) + "%";
        if (_sfxValueText != null && _sfxSlider != null)
            _sfxValueText.text = "音效音量 · " +
                Mathf.RoundToInt(_sfxSlider.value * 100f) + "%";
        bool muted = AudioManager.Instance != null
            ? AudioManager.Instance.IsMuted
            : PlayerPrefs.GetInt("AudioMuted", 0) != 0;
        SetBtnColor(_muteBtn, muted ? PrimaryStrong : SurfaceRaised);
        SetButtonLabel(_muteBtn, muted ? "✓ 已静音" : "一键静音");
    }

    void ToggleAudioMute()
    {
        bool muted = AudioManager.Instance != null
            ? AudioManager.Instance.IsMuted
            : PlayerPrefs.GetInt("AudioMuted", 0) != 0;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMuted(!muted);
        else
            EchoRunSaveSystem.SaveAudio(
                _masterSlider != null ? _masterSlider.value : 1f,
                _bgmSlider != null ? _bgmSlider.value : 0.5f,
                _sfxSlider != null ? _sfxSlider.value : 1f,
                !muted, false);
        RefreshVolumeLabels();
    }

    void OnAccessibilityChanged()
    {
        EchoRunAccessibility.ApplyToHierarchy(_safeAreaRoot);
        RefreshAccessibilityButtons();
        RefreshResultDetailsHeight();
    }

    void RefreshAccessibilityButtons()
    {
        RefreshToggleButton(_largeTextBtn, "大字", EchoRunAccessibility.LargeText);
        RefreshToggleButton(_highContrastBtn, "高对比", EchoRunAccessibility.HighContrast);
        RefreshToggleButton(_reducedMotionBtn, "减少动态", EchoRunAccessibility.ReducedMotion);
    }

    void RefreshToggleButton(Button button, string label, bool enabled)
    {
        if (button == null) return;
        SetBtnColor(button, enabled ? PrimaryStrong : SurfaceRaised);
        SetButtonLabel(button, enabled ? "✓ " + label : label);
    }

    void SetBtnColor(Button btn, Color c)
    {
        if (btn == null) return;
        if (_settingsPanel != null && btn.transform.IsChildOf(_settingsPanel.transform))
        {
            if (c == PrimaryStrong) c = SettingsSelected;
            else if (c == SurfaceRaised) c = SettingsControl;
        }
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = c;
        UIEdgeTreatment.Selection(btn, c == SettingsSelected || c == PrimaryStrong);
    }

    // ═══════════════════════════════════════════════════
    //  Character Panel (sub-menu)
    // ═══════════════════════════════════════════════════

    static readonly (string name, Color dark, Color light, Color emission)[] _presets = {
        ("日光橙", EchoRunUITheme.Backdrop, EchoRunUITheme.ActionAccent, Color.black),
        ("回声青", EchoRunUITheme.Backdrop, EchoRunUITheme.Echo, Color.black),
        ("莓红", EchoRunUITheme.Backdrop, new Color(0.929f, 0.392f, 0.545f), Color.black),
        ("夜蓝", EchoRunUITheme.Backdrop, new Color(0.424f, 0.353f, 0.831f), Color.black),
        ("葡萄紫", EchoRunUITheme.Backdrop, new Color(0.616f, 0.310f, 0.729f), Color.black),
        ("浅粉", EchoRunUITheme.Backdrop, new Color(0.859f, 0.588f, 0.773f), Color.black),
    };

    void CreateCharacterPanel()
    {
        _characterPanel = NewPanel("CharacterPanel", PageBackdrop);
        AddPageArtwork(_characterPanel.transform);

        // ScrollRect
        ScrollRect scroll = _characterPanel.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        // Viewport
        GameObject vp = new GameObject("Viewport", typeof(Image), typeof(Mask));
        vp.transform.SetParent(_characterPanel.transform, false);
        vp.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
        vp.GetComponent<Mask>().showMaskGraphic = false;
        RectTransform vpRT = vp.GetComponent<RectTransform>();
        vpRT.anchorMin = new Vector2(0, 0); vpRT.anchorMax = new Vector2(1, 1);
        vpRT.offsetMin = new Vector2(20, 20); vpRT.offsetMax = new Vector2(-20, -132);

        // Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(vp.transform, false);
        _characterContent = content.AddComponent<RectTransform>();
        _characterContent.anchorMin = new Vector2(0.5f, 1f);
        _characterContent.anchorMax = new Vector2(0.5f, 1f);
        _characterContent.pivot = new Vector2(0.5f, 1f);
        _characterContent.sizeDelta = new Vector2(1020, 1080);
        _characterContent.anchoredPosition = Vector2.zero;

        scroll.viewport = vpRT;
        scroll.content = _characterContent;

        Transform c = content.transform;

        Text title = MakeText("CharTitle", _characterPanel.transform, "跑者外观", 40, TextAnchor.MiddleCenter);
        title.color = PageInk;
        title.fontStyle = FontStyle.Bold;
        title.verticalOverflow = VerticalWrapMode.Overflow;
        AnchorText(title.GetComponent<RectTransform>(), 0.5f, 1f, 400, 80);
        title.rectTransform.anchoredPosition = new Vector2(0f, -64f);

        _characterSelectionText = MakeText("SelectionStatus", c,
            "选择配色；立即预览并保存", 22, TextAnchor.MiddleCenter);
        _characterSelectionText.color = PageMuted;
        AnchorText(_characterSelectionText.rectTransform, 0.5f, 0.94f, 620, 56);

        GameObject previewCard = new GameObject("RunnerPreviewCard", typeof(Image));
        previewCard.transform.SetParent(c, false);
        Image previewCardImage = previewCard.GetComponent<Image>();
        previewCardImage.color = PageSurface;
        previewCardImage.raycastTarget = false;
        ApplyRounded(previewCardImage);
        UIEdgeTreatment.Surface(previewCardImage, EchoRunUITheme.PageRule);

        GameObject preview = new GameObject("RunnerPreview", typeof(RawImage));
        preview.transform.SetParent(c, false);
        preview.GetComponent<RawImage>().raycastTarget = false;
        AnchorText(preview.GetComponent<RectTransform>(), 0.5f, 0.69f, 360f, 430f);
        _runnerPreview = preview.AddComponent<RunnerColorPreview>();
        _runnerFullViewBtn = MakeSmallButton("RunnerFullView", c, "全身",
            new Vector2(.35f, .5f), new Vector2(230f, 104f), PageSelected);
        _runnerFaceViewBtn = MakeSmallButton("RunnerFaceView", c, "面容",
            new Vector2(.65f, .5f), new Vector2(230f, 104f), PageRaised);
        _runnerFullViewBtn.onClick.AddListener(() => SetRunnerPreviewMode(false));
        _runnerFaceViewBtn.onClick.AddListener(() => SetRunnerPreviewMode(true));
        SetRunnerPreviewMode(false);

        // 2 rows × 3 columns of color presets inside scroll content
        float[] colX = { 0.18f, 0.5f, 0.82f };
        float[] rowY = { 0.38f, 0.18f };

        for (int r = 0; r < 2; r++)
        {
            for (int col = 0; col < 3; col++)
            {
                int idx = r * 3 + col;
                if (idx >= _presets.Length) break;
                var preset = _presets[idx];
                CreatePresetButton(preset.name, preset.dark, preset.light, idx,
                    new Vector2(colX[col], rowY[r]), c);
            }
        }

        _characterBackBtn = MakeButton("CharBackBtn", _characterPanel.transform, "返回", 34,
            new Vector2(0f, 1f), new Vector2(280, 76),
            PageRaised, PageSelected, PageInk);
        SetTopLeftButtonLayout(_characterBackBtn, new Vector2(280f, 76f));
        _characterBackBtn.onClick.AddListener(HideCharacter);

        LayoutCharacter(UILayoutRules.IsCompactPortrait(Screen.width, Screen.height));
        _characterPanel.SetActive(false);
    }

    void LayoutCharacter(bool portrait)
    {
        if (_characterContent == null) return;
        _characterContent.sizeDelta = portrait
            ? new Vector2(960f, 1720f) : new Vector2(1640f, 800f);

        RectTransform preview = _characterContent.Find("RunnerPreview") as RectTransform;
        if (preview != null)
            AnchorText(preview, portrait ? 0.5f : 0.25f,
                portrait ? 0.765f : 0.57f,
                portrait ? 616f : 624f, portrait ? 770f : 660f);
        RectTransform previewCard = _characterContent.Find("RunnerPreviewCard") as RectTransform;
        if (previewCard != null)
            AnchorText(previewCard, portrait ? 0.5f : 0.25f,
                portrait ? 0.765f : 0.57f,
                portrait ? 826f : 704f, portrait ? 810f : 700f);

        SetButtonLayout(_runnerFullViewBtn,
            portrait ? new Vector2(.35f, .493f) : new Vector2(.16f, .06f),
            portrait ? new Vector2(230f, 104f) : new Vector2(220f, 76f));
        SetButtonLayout(_runnerFaceViewBtn,
            portrait ? new Vector2(.65f, .493f) : new Vector2(.34f, .06f),
            portrait ? new Vector2(230f, 104f) : new Vector2(220f, 76f));
        SetHomeButtonFont(_runnerFullViewBtn, portrait ? 32 : 26);
        SetHomeButtonFont(_runnerFaceViewBtn, portrait ? 32 : 26);

        if (_characterSelectionText != null)
        {
            AnchorText(_characterSelectionText.rectTransform,
                portrait ? 0.5f : 0.73f, portrait ? 0.432f : 0.83f,
                portrait ? 860f : 740f, 70f);
            SetHomeFont(_characterSelectionText, portrait ? 30 : 28);
        }

        for (int i = 0; i < _presetButtons.Length; i++)
        {
            Vector2 anchor = portrait
                ? new Vector2(0.17f + (i % 3) * 0.33f, 0.315f - (i / 3) * 0.17f)
                : new Vector2(0.62f + (i % 2) * 0.22f, 0.66f - (i / 2) * 0.235f);
            SetButtonLayout(_presetButtons[i], anchor, portrait
                ? new Vector2(272f, 212f) : new Vector2(300f, 164f));
            SetHomeFont(_presetLabels[i], portrait ? 30 : 26);
        }
    }

    void SetRunnerPreviewMode(bool portrait)
    {
        if (_runnerPreview != null) _runnerPreview.SetPortraitMode(portrait);
        SetBtnColor(_runnerFullViewBtn, portrait ? PageRaised : PageSelected);
        SetBtnColor(_runnerFaceViewBtn, portrait ? PageSelected : PageRaised);
        SetButtonLabel(_runnerFullViewBtn, portrait ? "全身" : "✓ 全身");
        SetButtonLabel(_runnerFaceViewBtn, portrait ? "✓ 面容" : "面容");
    }

    void CreatePresetButton(string label, Color dark, Color light, int index,
        Vector2 anchor, Transform parent)
    {
        Button btn = MakeSmallButton("PresetBtn_" + index, parent, "",
            anchor, new Vector2(240, 164), PageSurface);
        _presetButtons[index] = btn;
        btn.onClick.AddListener(() => ApplyCharacterColor(index));

        Text autoLabel = btn.GetComponentInChildren<Text>();
        if (autoLabel != null) Destroy(autoLabel.gameObject);

        GameObject swatch = new GameObject("Swatch", typeof(Image));
        swatch.transform.SetParent(btn.transform, false);
        Image swatchImage = swatch.GetComponent<Image>();
        swatchImage.color = light;
        swatchImage.raycastTarget = false;
        ApplyRounded(swatchImage);
        RectTransform swatchRect = swatch.GetComponent<RectTransform>();
        swatchRect.anchorMin = new Vector2(0.08f, 0.36f);
        swatchRect.anchorMax = new Vector2(0.92f, 0.90f);
        swatchRect.offsetMin = Vector2.zero;
        swatchRect.offsetMax = Vector2.zero;

        GameObject lower = new GameObject("DarkTone", typeof(Image));
        lower.transform.SetParent(swatch.transform, false);
        Image lowerImage = lower.GetComponent<Image>();
        lowerImage.color = dark;
        lowerImage.raycastTarget = false;
        RectTransform lowerRect = lower.GetComponent<RectTransform>();
        lowerRect.anchorMin = Vector2.zero;
        lowerRect.anchorMax = new Vector2(1f, 0.34f);
        lowerRect.offsetMin = Vector2.zero;
        lowerRect.offsetMax = Vector2.zero;

        Text nameLabel = MakeText("PresetLabel_" + index, btn.transform,
            label, 24, TextAnchor.MiddleCenter);
        nameLabel.color = PageInk;
        nameLabel.fontStyle = FontStyle.Normal;
        RectTransform labelRect = nameLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0.02f);
        labelRect.anchorMax = new Vector2(1f, 0.32f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        _presetLabels[index] = nameLabel;
    }

    void ApplyCharacterColor(int presetIndex)
    {
        if (presetIndex < 0 || presetIndex >= _presets.Length) return;
        var preset = _presets[presetIndex];

        var player = GameObject.Find("player");
        if (player == null) return;
        var model = player.transform.Find("CharacterModel");
        if (model == null) return;

        int changedSlots = RunnerAppearanceService.Apply(model,
            preset.dark, preset.light, preset.emission);
        if (changedSlots <= 0)
        {
            if (_characterSelectionText != null)
                _characterSelectionText.text = "当前跑者模型暂不支持配色";
            return;
        }

        _selectedPreset = presetIndex;
        EchoRunSaveSystem.SaveCharacterPreset(presetIndex);
        RefreshCharacterSelection();
    }

    void LoadCharacterPreset()
    {
        int idx = PlayerPrefs.GetInt("CharacterPreset", 0);
        ApplyCharacterColor(Mathf.Clamp(idx, 0, _presets.Length - 1));
    }

    void ShowCharacter()
    {
        if (_menuRouter != null) _menuRouter.Show(MenuScreen.Runner);
        else
        {
            if (_menuPanel != null) _menuPanel.SetActive(false);
            if (_characterPanel != null) _characterPanel.SetActive(true);
        }
        RefreshCharacterSelection();
        RefreshTextGeometry(_characterPanel != null
            ? _characterPanel.transform : null);
        ScheduleTextRefresh(_characterPanel != null
            ? _characterPanel.transform : null);
        Canvas.ForceUpdateCanvases();
        if (_characterPanel != null)
            _characterPanel.GetComponent<ScrollRect>().verticalNormalizedPosition = 1f;
    }

    void HideCharacter()
    {
        if (_menuRouter != null) _menuRouter.BackToHome();
        else
        {
            if (_characterPanel != null) _characterPanel.SetActive(false);
            if (_menuPanel != null) _menuPanel.SetActive(true);
        }
    }

    void RefreshCharacterSelection()
    {
        for (int i = 0; i < _presetButtons.Length; i++)
        {
            bool selected = i == _selectedPreset;
            SetBtnColor(_presetButtons[i], selected
                ? PageSelected : PageSurface);
            if (_presetLabels[i] != null)
                _presetLabels[i].text = selected
                    ? "✓ " + _presets[i].name
                    : _presets[i].name;
        }
        if (_characterSelectionText != null)
            _characterSelectionText.text = "当前配色：" + _presets[_selectedPreset].name;
    }

    // ═══════════════════════════════════════════════════
    //  HUD Panel
    // ═══════════════════════════════════════════════════

    void CreateHUDPanel()
    {
        GameObject hudPrefab = Resources.Load<GameObject>("UI/EchoHud");
        if (hudPrefab != null)
        {
            _hudPanel = Instantiate(hudPrefab,
                _safeAreaRoot != null ? _safeAreaRoot : transform, false);
            _hudPanel.name = "EchoHud";
            _echoHudView = _hudPanel.GetComponent<EchoHudView>();
            _echoHudPresenter = _hudPanel.GetComponent<EchoHudPresenter>();
            if (_echoHudPresenter == null)
                _echoHudPresenter = _hudPanel.AddComponent<EchoHudPresenter>();
            if (_echoHudView != null)
            {
                _echoHudPresenter.Initialize(_echoHudView, _gm);
                _pauseBtn = _echoHudView.PauseButton;
                CreateRunSettingsButton();
                _hudPanel.SetActive(false);
                return;
            }
            Destroy(_hudPanel);
            _hudPanel = null;
            _echoHudPresenter = null;
        }

        _hudPanel = new GameObject("HudPanel", typeof(RectTransform));
        _hudPanel.transform.SetParent(
            _safeAreaRoot != null ? _safeAreaRoot : transform, false);
        Stretch(_hudPanel.GetComponent<RectTransform>());

        _hudStatsPanel = CreateHudSurface("StatsSurface", _hudPanel.transform,
            new Vector2(0f, 1f), new Vector2(430f, 52f),
            new Vector2(18f, -18f), new Vector2(0f, 1f));
        _statsText = MakeText("StatsText", _hudStatsPanel.transform,
            "SCORE 00000   RANGE 000m   SHARDS 00", 18,
            TextAnchor.MiddleLeft);
        _statsText.fontStyle = FontStyle.Bold;
        _statsText.color = TextPrimary;
        Stretch(_statsText.rectTransform);
        _statsText.rectTransform.offsetMin = new Vector2(16f, 0f);
        _statsText.rectTransform.offsetMax = new Vector2(-12f, 0f);

        _hudContractPanel = CreateHudSurface("ContractSurface", _hudPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(700f, 92f),
            new Vector2(0f, -18f), new Vector2(0.5f, 1f));

        _contractText = MakeText("Contract", _hudContractPanel.transform,
            "AI 正在学你的跑法", 25, TextAnchor.MiddleLeft);
        _contractText.fontStyle = FontStyle.Bold;
        _contractText.color = TextPrimary;
        RectTransform contractRect = _contractText.rectTransform;
        contractRect.anchorMin = new Vector2(0.04f, 0.46f);
        contractRect.anchorMax = new Vector2(0.70f, 1f);
        contractRect.offsetMin = Vector2.zero;
        contractRect.offsetMax = Vector2.zero;

        _contractProgressText = MakeText("ContractProgress",
            _hudContractPanel.transform, "0 / 3", 25, TextAnchor.MiddleRight);
        _contractProgressText.fontStyle = FontStyle.Bold;
        _contractProgressText.color = Primary;
        RectTransform progressTextRect = _contractProgressText.rectTransform;
        progressTextRect.anchorMin = new Vector2(0.70f, 0.46f);
        progressTextRect.anchorMax = new Vector2(0.96f, 1f);
        progressTextRect.offsetMin = Vector2.zero;
        progressTextRect.offsetMax = Vector2.zero;

        _leadText = MakeText("Lead", _hudContractPanel.transform,
            "记录路线、动作与节奏", 21, TextAnchor.MiddleCenter);
        _leadText.fontStyle = FontStyle.Bold;
        _leadText.color = TextMuted;
        RectTransform leadRect = _leadText.rectTransform;
        leadRect.anchorMin = new Vector2(0.04f, 0.13f);
        leadRect.anchorMax = new Vector2(0.96f, 0.49f);
        leadRect.offsetMin = Vector2.zero;
        leadRect.offsetMax = Vector2.zero;

        GameObject progressTrack = new GameObject("ProgressTrack", typeof(Image));
        progressTrack.transform.SetParent(_hudContractPanel.transform, false);
        _contractProgressGroup = progressTrack;
        Image progressTrackImage = progressTrack.GetComponent<Image>();
        progressTrackImage.color = SurfaceRaised;
        progressTrackImage.raycastTarget = false;
        ApplyRounded(progressTrackImage);
        RectTransform progressTrackRect = progressTrack.GetComponent<RectTransform>();
        progressTrackRect.anchorMin = new Vector2(0.04f, 0f);
        progressTrackRect.anchorMax = new Vector2(0.96f, 0f);
        progressTrackRect.pivot = new Vector2(0f, 0f);
        progressTrackRect.sizeDelta = new Vector2(0f, 6f);
        progressTrackRect.anchoredPosition = new Vector2(0f, 7f);

        GameObject progressFill = new GameObject("ProgressFill", typeof(Image));
        progressFill.transform.SetParent(progressTrack.transform, false);
        _contractProgressFill = progressFill.GetComponent<Image>();
        _contractProgressFill.color = Primary;
        _contractProgressFill.raycastTarget = false;
        ApplyRounded(_contractProgressFill);
        RectTransform progressFillRect = progressFill.GetComponent<RectTransform>();
        progressFillRect.anchorMin = Vector2.zero;
        progressFillRect.anchorMax = new Vector2(0f, 1f);
        progressFillRect.offsetMin = Vector2.zero;
        progressFillRect.offsetMax = Vector2.zero;

        _duelFeedbackText = MakeText("DuelFeedback", _hudPanel.transform,
            "", 25, TextAnchor.MiddleCenter);
        _duelFeedbackText.fontStyle = FontStyle.Bold;
        _duelFeedbackText.color = Primary;
        AddOutline(_duelFeedbackText.gameObject, WithAlpha(Ink, 0.85f));
        AnchorText(_duelFeedbackText.rectTransform, 0.5f, 0.76f, 660f, 52f);
        _duelFeedbackText.gameObject.SetActive(false);

        _buffGroup = new GameObject("BuffGroup", typeof(RectTransform));
        _buffGroup.transform.SetParent(_hudPanel.transform, false);
        RectTransform bgRT = _buffGroup.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 1); bgRT.anchorMax = new Vector2(0, 1);
        bgRT.pivot = new Vector2(0, 1);
        bgRT.anchoredPosition = new Vector2(22f, -82f);
        bgRT.sizeDelta = new Vector2(360, 30);

        Text buffIcon = MakeText("BuffIcon", _buffGroup.transform, "▶", 20, TextAnchor.MiddleLeft);
        buffIcon.color = Success;
        RectTransform biRT = buffIcon.GetComponent<RectTransform>();
        biRT.anchorMin = new Vector2(0, 0.5f); biRT.anchorMax = new Vector2(0, 0.5f);
        biRT.pivot = new Vector2(0, 0.5f);
        biRT.anchoredPosition = new Vector2(0, 0);
        biRT.sizeDelta = new Vector2(24, 24);

        _buffText = MakeText("BuffText", _buffGroup.transform, "", 22, TextAnchor.MiddleLeft);
        _buffText.color = Success;
        RectTransform btRT = _buffText.GetComponent<RectTransform>();
        btRT.anchorMin = new Vector2(0, 0.5f); btRT.anchorMax = new Vector2(0, 0.5f);
        btRT.pivot = new Vector2(0, 0.5f);
        btRT.anchoredPosition = new Vector2(28, 0);
        btRT.sizeDelta = new Vector2(180, 24);

        _buffGroup.SetActive(false);

        _pauseBtn = MakeIconButton("PauseBtn", _hudPanel.transform, "Ⅱ",
            new Vector2(1, 1), new Vector2(48, 48),
            WithAlpha(SurfaceRaised, 0.96f));
        _pauseBtn.onClick.AddListener(() => _gm.Pause());

        CreateRunSettingsButton();

        _hudPanel.SetActive(false);
    }

    void CreateRunSettingsButton()
    {
        _runSettingsBtn = MakeButton("RunSettingsBtn", _hudPanel.transform,
            "设置", 24, new Vector2(1f, 1f), new Vector2(120f, 64f),
            SurfaceRaised, TextPrimary);
        _runSettingsBtn.onClick.AddListener(ShowSettings);
    }

    static void LayoutTopRightControl(Button button, Vector2 size, Vector2 offset)
    {
        if (button == null) return;
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
    }

    GameObject CreateHudSurface(string name, Transform parent, Vector2 anchor,
        Vector2 size, Vector2 offset, Vector2 pivot)
    {
        GameObject surface = new GameObject(name, typeof(Image));
        surface.transform.SetParent(parent, false);
        Image image = surface.GetComponent<Image>();
        image.color = WithAlpha(Backdrop, 0.88f);
        image.raycastTarget = false;
        ApplyRounded(image);
        RectTransform rect = surface.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
        return surface;
    }

    void RefreshDuelHud(bool forceFeedback = false)
    {
        bool observing = _gm != null && _gm.IsSingleContractRun
            && AIShadowRunner.Instance != null && !AIShadowRunner.Instance.HasActiveOpponent;
        SetButtonLabel(_runSettingsBtn, observing ? "观察详情" : "设置");
        if (_echoHudPresenter != null)
        {
            _echoHudPresenter.Refresh(forceFeedback);
            return;
        }

        AIShadowRunner shadow = AIShadowRunner.Instance;
        if (IsSingleContractPresentation(shadow))
        {
            RefreshSingleContractFallbackHud(shadow, forceFeedback);
            return;
        }

        EchoPhaseVisualController visual = EchoPhaseVisualController.Instance;
        if (visual != null && visual.UsesSingleContractVisualState)
            visual.ReleaseSingleContractVisualState();
        if (_contractProgressGroup != null)
            _contractProgressGroup.SetActive(true);
        if (_contractProgressText != null)
            _contractProgressText.gameObject.SetActive(true);
        EchoDuelViewData view = EchoRunPresentation.BuildDuel(
            shadow != null && shadow.HasActiveOpponent,
            shadow != null ? shadow.ActiveContract : null,
            shadow != null ? shadow.PlayerLead : 0f,
            shadow != null ? shadow.minimumJumpSamples : 2,
            shadow != null ? shadow.minimumSlideSamples : 2,
            shadow != null ? shadow.JumpTrainingSampleCount : 0,
            shadow != null ? shadow.SlideTrainingSampleCount : 0,
            shadow != null ? shadow.CalibrationProgress : 0f,
            shadow != null ? shadow.DuelPhase : EchoDuelPhase.Calibration,
            shadow != null ? shadow.DuelPhaseProgress : 0f,
            shadow != null ? shadow.PublicPrediction : "");

        if (_contractText != null)
            _contractText.text = view.phase + " · " + view.contract;
        if (_contractProgressText != null)
            _contractProgressText.text = view.progress;
        if (_leadText != null)
        {
            _leadText.text = string.IsNullOrEmpty(view.prediction)
                ? view.lead
                : view.prediction + "　|　" + view.lead;
            _leadText.color = view.leadState == EchoLeadState.Leading
                ? Reward
                : view.leadState == EchoLeadState.Trailing
                    ? Danger
                    : TextMuted;
        }
        if (_contractProgressFill != null)
        {
            RectTransform fill = _contractProgressFill.rectTransform;
            fill.anchorMax = new Vector2(view.progress01, 1f);
            _contractProgressFill.color = view.progress01 >= 1f
                ? Success : Primary;
        }

        if (_duelFeedbackText == null || string.IsNullOrEmpty(view.feedback))
            return;
        if (!forceFeedback
            && view.feedbackSequence == _lastDuelFeedbackSequence) return;

        _lastDuelFeedbackSequence = view.feedbackSequence;
        _duelFeedbackText.text = view.feedback;
        _duelFeedbackText.color = view.feedback.StartsWith("回声施压")
                                  || view.feedback.StartsWith("预判命中")
            ? Danger
            : view.feedback.StartsWith("预测失效")
              || view.feedback.StartsWith("偏离")
              || view.feedback.StartsWith("裂解")
              || view.feedback.StartsWith("反制生效")
                ? Reward : Primary;
        _duelFeedbackTimer = 1.8f;
        _duelFeedbackText.gameObject.SetActive(true);
    }

    void RefreshSingleContractFallbackHud(AIShadowRunner shadow,
        bool forceFeedback)
    {
        if (_hudStatsPanel != null) _hudStatsPanel.SetActive(false);
        string powerUpStatus = PowerUpController.Instance != null
            ? PowerUpController.Instance.GetStatusText() : "";
        SingleContractHudData view =
            EchoHudPresenter.BuildSingleContractHudData(
                _gm, shadow, powerUpStatus);

        if (_contractText != null)
        {
            _contractText.text = view.openingMemory
                && !string.IsNullOrEmpty(view.openingTitle)
                ? view.openingTitle + "\n" + view.memory
                : view.memory;
            _contractText.gameObject.SetActive(view.openingMemory);
        }
        if (_contractProgressText != null)
        {
            _contractProgressText.rectTransform.anchorMin = new Vector2(.04f, .52f);
            _contractProgressText.rectTransform.anchorMax = new Vector2(.96f, .98f);
            _contractProgressText.alignment = TextAnchor.MiddleLeft;
            _contractProgressText.text = view.showCalibrationProgress
                ? view.calibrationMeterText : view.prediction;
            _contractProgressText.gameObject.SetActive(
                !string.IsNullOrEmpty(_contractProgressText.text));
        }
        if (_contractProgressGroup != null)
            _contractProgressGroup.SetActive(false);
        if (_contractProgressFill != null && view.showCalibrationProgress)
        {
            RectTransform fill = _contractProgressFill.rectTransform;
            fill.anchorMax = new Vector2(view.calibrationProgress01, 1f);
            _contractProgressFill.color = view.calibrationProgress01 >= 1f
                ? Success : Primary;
        }
        if (_leadText != null)
        {
            _leadText.text = (view.visualState == SingleContractVisualState.Calibration
                ? "" : view.lead + "　|　") + view.injuriesText
                            + "　|　" + view.finishRemainingText;
            _leadText.color = view.leadState
                              == SingleContractLeadState.PlayerLeading
                ? Reward
                : view.leadState == SingleContractLeadState.EchoLeading
                    ? EchoRunUITheme.Echo : TextMuted;
        }

        EchoPhaseVisualController visual = EchoPhaseVisualController.Instance;
        if (visual != null)
            visual.ApplySingleContractVisualState(view.visualState);

        if (_duelFeedbackText == null
            || string.IsNullOrEmpty(view.instantFeedback)) return;
        if (view.feedbackSequence == _lastDuelFeedbackSequence)
        {
            if (_duelFeedbackTimer > 0f)
                _duelFeedbackText.text = view.instantFeedback;
            return;
        }

        _lastDuelFeedbackSequence = view.feedbackSequence;
        _duelFeedbackText.text = view.instantFeedback;
        _duelFeedbackText.color = SingleContractFeedbackColor(
            view.instantFeedbackKind);
        _duelFeedbackTimer =
            EchoRunPresentation.SingleContractFeedbackDurationSeconds;
        _duelFeedbackText.gameObject.SetActive(true);
    }

    bool IsSingleContractPresentation(AIShadowRunner shadow)
    {
        if (_gm != null) return _gm.UsesSingleContractRules;
        return shadow != null && (shadow.ActiveGameplayFlowMode
            == GameplayFlowMode.SingleContract || shadow.ActiveGameplayFlowMode
            == GameplayFlowMode.AsyncChallenge);
    }

    static Color SingleContractFeedbackColor(
        SingleContractInstantFeedback feedback)
    {
        switch (feedback)
        {
            case SingleContractInstantFeedback.PredictionHit:
                return EchoRunUITheme.Echo;
            case SingleContractInstantFeedback.CounterFailed:
            case SingleContractInstantFeedback.ExecutionIncomplete:
            case SingleContractInstantFeedback.ObservationInconclusive:
                return TextPrimary;
            case SingleContractInstantFeedback.EchoRelearned:
                return TextPrimary;
            case SingleContractInstantFeedback.RewriteSucceeded:
                return Reward;
            default:
                return Primary;
        }
    }

    void CreateControlHint()
    {
        _controlHint = new GameObject("ControlHint", typeof(Image));
        _controlHint.transform.SetParent(_safeAreaRoot, false);
        Image background = _controlHint.GetComponent<Image>();
        background.color = WithAlpha(Backdrop, 0.94f);
        ApplyRounded(background);

        RectTransform rt = _controlHint.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 34f);
        rt.sizeDelta = new Vector2(760f, 64f);

        _controlHintText = MakeText("ControlHintText", _controlHint.transform,
            "", 24, TextAnchor.MiddleCenter);
        _controlHintText.fontStyle = FontStyle.Bold;
        _controlHintText.color = TextPrimary;
        Stretch(_controlHintText.GetComponent<RectTransform>());
        AddOutline(_controlHintText.gameObject, WithAlpha(Ink, 0.65f));
        AddPanelRule(_controlHint.transform, Primary);
        _controlHint.SetActive(false);
    }

    void CreateLandscapeGuard()
    {
        Transform canvasRoot = _safeAreaRoot != null
            ? _safeAreaRoot.parent
            : FindObjectOfType<Canvas>()?.transform;
        if (canvasRoot == null) return;

        _landscapeGuard = new GameObject(
            "LandscapeGuard", typeof(Image));
        _landscapeGuard.transform.SetParent(canvasRoot, false);
        _landscapeGuard.GetComponent<Image>().color =
            WithAlpha(Backdrop, 0.99f);
        Stretch(_landscapeGuard.GetComponent<RectTransform>());

        Text message = MakeText("Message", _landscapeGuard.transform,
            "请横屏游玩\n旋转设备以继续", 42, TextAnchor.MiddleCenter);
        message.fontStyle = FontStyle.Bold;
        message.color = TextPrimary;
        message.lineSpacing = 1.25f;
        Stretch(message.GetComponent<RectTransform>());
        AddOutline(message.gameObject, new Color(0f, 0f, 0f, 0.7f));
        _landscapeGuard.SetActive(false);
    }

    Text MakeHUDText(string name, Transform parent, string content, int size,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        Text t = MakeText(name, parent, content, size, TextAnchor.MiddleLeft);
        t.color = Color.white;
        t.fontStyle = FontStyle.Bold;
        AddOutline(t.gameObject, new Color(0, 0, 0, 0.6f));
        RectTransform rt = t.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return t;
    }

    // ═══════════════════════════════════════════════════
    //  Pause Panel
    // ═══════════════════════════════════════════════════

    void CreatePausePanel()
    {
        _pausePanel = NewPanel("PausePanel", WithAlpha(Backdrop, 0.92f));

        Text title = MakeText("PauseTitle", _pausePanel.transform, "跑局已暂停", 42, TextAnchor.MiddleCenter);
        title.color = TextPrimary;
        title.fontStyle = FontStyle.Bold;
        AnchorText(title.GetComponent<RectTransform>(), 0.5f, 0.58f, 620, 100);

        _resumeBtn = MakeButton("ResumeBtn", _pausePanel.transform, "继续游戏", 38,
            new Vector2(0.5f, 0.38f), new Vector2(400, 100),
            EchoRunUITheme.ActionAccent, EchoRunUITheme.ActionAccentDark, Ink);
        _resumeBtn.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        _resumeBtn.onClick.AddListener(() => _gm.Resume());

        _pauseToMenuBtn = MakeButton("PauseToMenuBtn", _pausePanel.transform, "返回主页", 32,
            new Vector2(0.5f, 0.22f), new Vector2(320, 80),
            SurfaceRaised, TextMuted);
        _pauseToMenuBtn.onClick.AddListener(() => _gm.ReturnToMenu());

        _pauseSettingsBtn = MakeButton("PauseSettingsBtn", _pausePanel.transform,
            "设置", 28, Vector2.one, new Vector2(160f, 76f),
            SurfaceRaised, TextPrimary);
        _pauseSettingsBtn.onClick.AddListener(ShowSettings);
        LayoutTopRightControl(_pauseSettingsBtn, new Vector2(160f, 76f),
            new Vector2(-24f, -24f));

        _pausePanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════════
    //  GameOver Panel
    // ═══════════════════════════════════════════════════

    void CreateGameOverPanel()
    {
        _gameOverPanel = NewPanel("GameOverPanel", WithAlpha(Backdrop, 0.94f));

        Text title = MakeText("GOTitle", _gameOverPanel.transform, "本局结束", 68, TextAnchor.MiddleCenter);
        title.color = Danger;
        title.fontStyle = FontStyle.Bold;
        AnchorText(title.GetComponent<RectTransform>(), 0.5f, 0.76f, 500, 90);
        title.gameObject.SetActive(false);

        // Session score
        _finalScoreText = MakeText("FinalScore", _gameOverPanel.transform, "得分: 0", 48, TextAnchor.MiddleCenter);
        _finalScoreText.color = TextPrimary;
        _finalScoreText.fontStyle = FontStyle.Bold;
        AnchorText(_finalScoreText.GetComponent<RectTransform>(), 0.5f, 0.61f, 450, 70);
        _finalScoreText.gameObject.SetActive(false);

        // High score
        _highScoreText = MakeText("HighScore", _gameOverPanel.transform, "最高分: 0", 36, TextAnchor.MiddleCenter);
        _highScoreText.color = Reward;
        _highScoreText.fontStyle = FontStyle.Bold;
        AnchorText(_highScoreText.GetComponent<RectTransform>(), 0.5f, 0.52f, 400, 50);
        _highScoreText.gameObject.SetActive(false);

        // Coins
        _coinResultText = MakeText("CoinResult", _gameOverPanel.transform, "金币: 0", 32, TextAnchor.MiddleCenter);
        _coinResultText.color = Reward;
        AnchorText(_coinResultText.GetComponent<RectTransform>(), 0.5f, 0.45f, 500, 40);
        _coinResultText.gameObject.SetActive(false);

        _shadowResultText = MakeText("ShadowResult", _gameOverPanel.transform,
            "正在整理这局的回声变化", 28, TextAnchor.MiddleCenter);
        _shadowResultText.color = Primary;
        _shadowResultText.fontStyle = FontStyle.Normal;
        _shadowResultText.resizeTextForBestFit = false;
        _shadowResultText.resizeTextMinSize = 18;
        _shadowResultText.resizeTextMaxSize = 28;
        _shadowResultText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _shadowResultText.verticalOverflow = VerticalWrapMode.Truncate;
        _shadowResultText.lineSpacing = 1.05f;
        Vector2 resultTextSize = UILayoutRules.GetResultTextSize(1920, 1080);
        AnchorText(_shadowResultText.GetComponent<RectTransform>(), 0.5f, 0.40f,
            resultTextSize.x, resultTextSize.y);

        CreateResultDetails();

        // Restart
        _restartBtn = MakeButton("RestartBtn", _gameOverPanel.transform, "重新挑战", 30,
            new Vector2(0.5f, 0.18f), new Vector2(380, 76),
            EchoRunUITheme.ActionAccent, EchoRunUITheme.ActionAccentDark, Ink);
        _restartBtn.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        _restartBtn.onClick.AddListener(ExecuteResultAction);

        // Back to menu
        _goToMenuBtn = MakeButton("GoToMenuBtn", _gameOverPanel.transform, "返回主页", 24,
            new Vector2(0.5f, 0.07f), new Vector2(280, 60),
            SurfaceRaised, TextMuted);
        _goToMenuBtn.onClick.AddListener(() => _gm.ReturnToMenu());

        // Create consolidated result text last so WebGL dynamic-font atlas rebuilds
        // cannot leave the earlier score rows without geometry.
        _gameOverTitleText = MakeText("GameOverTitle", _gameOverPanel.transform,
            "本局结果", 48, TextAnchor.MiddleCenter);
        _gameOverTitleText.color = Danger;
        _gameOverTitleText.fontStyle = FontStyle.Bold;
        AnchorText(_gameOverTitleText.GetComponent<RectTransform>(), 0.5f, 0.81f, 700, 80);

        _gameOverStatsText = MakeText("GameOverStats", _gameOverPanel.transform,
            "得分 0 · 距离 0m · 金币 0", 24, TextAnchor.MiddleCenter);
        _gameOverStatsText.color = TextMuted;
        _gameOverStatsText.fontStyle = FontStyle.Normal;
        _gameOverStatsText.lineSpacing = 1.05f;
        AnchorText(_gameOverStatsText.GetComponent<RectTransform>(), 0.5f, 0.68f, 1080, 60);

        _gameOverPanel.SetActive(false);
    }

    void CreateResultDetails()
    {
        GameObject panel = new GameObject("ResultDetails", typeof(Image), typeof(ScrollRect));
        panel.transform.SetParent(_gameOverPanel.transform, false);
        Image background = panel.GetComponent<Image>();
        background.color = WithAlpha(Surface, 0.96f);
        ApplyRounded(background);
        _resultDetailsScroll = panel.GetComponent<ScrollRect>();
        _resultDetailsScroll.horizontal = false;
        _resultDetailsScroll.vertical = true;
        _resultDetailsScroll.movementType = ScrollRect.MovementType.Clamped;
        _resultDetailsScroll.scrollSensitivity = 32f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform),
            typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(panel.transform, false);
        viewport.GetComponent<Image>().color = Color.clear;
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewportRect.offsetMin = new Vector2(24f, 18f);
        viewportRect.offsetMax = new Vector2(-28f, -18f);
        _resultDetailsScroll.viewport = viewportRect;

        _resultDetailsText = MakeText("ResultDetailsText", viewport.transform,
            "", 22, TextAnchor.UpperLeft);
        _resultDetailsText.color = TextMuted;
        _resultDetailsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _resultDetailsText.verticalOverflow = VerticalWrapMode.Overflow;
        _resultDetailsText.lineSpacing = 1.15f;
        RectTransform content = _resultDetailsText.rectTransform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 260f);
        _resultDetailsScroll.content = content;

        GameObject barObject = new GameObject("Scrollbar", typeof(Image), typeof(Scrollbar));
        barObject.transform.SetParent(panel.transform, false);
        barObject.GetComponent<Image>().color = WithAlpha(TextMuted, 0.1f);
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = Vector2.one;
        barRect.offsetMin = new Vector2(-12f, 18f);
        barRect.offsetMax = new Vector2(-6f, -18f);
        GameObject handle = new GameObject("Handle", typeof(Image));
        handle.transform.SetParent(barObject.transform, false);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = WithAlpha(TextMuted, 0.6f);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        Stretch(handleRect);
        Scrollbar scrollbar = barObject.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        _resultDetailsScroll.verticalScrollbar = scrollbar;
        _resultDetailsScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        _resultDetailsBtn = MakeButton("ResultDetailsBtn", _gameOverPanel.transform,
            "查看本局复盘", 22, new Vector2(0.5f, 0.265f), new Vector2(320f, 52f),
            Color.clear, Color.clear, TextMuted);
        _resultDetailsBtn.onClick.AddListener(ToggleResultDetails);
        panel.SetActive(false);
        _resultDetailsBtn.gameObject.SetActive(false);
    }

    public void PresentResultSummary(string fullResult, string title, bool singleContract,
        string factualSummary = null)
    {
        _usesCompactResult = singleContract;
        _resultDetailsExpanded = false;
        if (_gameOverTitleText != null) _gameOverTitleText.text = title;
        if (_shadowResultText != null)
        {
            _shadowResultText.resizeTextForBestFit = !singleContract;
            _shadowResultText.fontStyle = singleContract ? FontStyle.Normal : FontStyle.Bold;
            _shadowResultText.text = factualSummary ?? (singleContract
                ? EchoRunPresentation.BuildSingleContractResultDetails(
                    EchoRunPresentation.BuildSingleContractResultSummary(fullResult), title)
                : fullResult);
        }
        if (_resultDetailsText != null)
            _resultDetailsText.text = singleContract
                ? EchoRunPresentation.BuildSingleContractResultDetails(fullResult, title) : "";
        if (_resultDetailsBtn != null)
            _resultDetailsBtn.gameObject.SetActive(singleContract
                && _resultDetailsText != null
                && !string.IsNullOrWhiteSpace(_resultDetailsText.text));
        if (_resultDetailsScroll != null)
        {
            _resultDetailsScroll.StopMovement();
            _resultDetailsScroll.gameObject.SetActive(false);
        }
        SetButtonLabel(_resultDetailsBtn, "查看本局复盘");
        ApplyResultSummaryLayout(Screen.width, Screen.height);
    }

    void ToggleResultDetails()
    {
        if (!_usesCompactResult || _resultDetailsScroll == null) return;
        _resultDetailsExpanded = !_resultDetailsExpanded;
        _resultDetailsScroll.gameObject.SetActive(_resultDetailsExpanded);
        SetButtonLabel(_resultDetailsBtn,
            _resultDetailsExpanded ? "收起本局复盘" : "查看本局复盘");
        ApplyResultSummaryLayout(Screen.width, Screen.height);
        if (_resultDetailsExpanded)
        {
            _resultDetailsScroll.StopMovement();
            _resultDetailsScroll.verticalNormalizedPosition = 1f;
        }
        ScheduleTextRefresh(_gameOverPanel.transform);
    }

    void ApplyResultSummaryLayout(int width, int height)
    {
        if (_gameOverTitleText == null || _shadowResultText == null) return;
        bool portrait = UILayoutRules.IsCompactPortrait(width, height);
        bool expanded = _usesCompactResult && _resultDetailsExpanded;
        float textWidth = portrait ? 900f : 1120f;
        AnchorText(_gameOverTitleText.rectTransform, 0.5f,
            _usesCompactResult ? (expanded ? 0.86f : 0.76f) : 0.81f,
            textWidth, 96f);
        AnchorText(_gameOverStatsText.rectTransform, 0.5f,
            _usesCompactResult ? (expanded ? 0.78f : 0.665f) : 0.68f,
            textWidth, 54f);
        Vector2 summarySize = _usesCompactResult
            ? new Vector2(textWidth, 104f) : UILayoutRules.GetResultTextSize(width, height);
        AnchorText(_shadowResultText.rectTransform, 0.5f,
            _usesCompactResult ? (expanded ? 0.67f : 0.54f) : 0.40f,
            summarySize.x, summarySize.y);
        SetButtonLayout(_restartBtn, new Vector2(0.5f,
            _usesCompactResult ? (expanded ? 0.245f : 0.38f) : 0.18f),
            UILayoutRules.GetRestartButtonSize(width, height, UsesTouchLayout()));
        SetButtonLayout(_goToMenuBtn, new Vector2(0.5f,
            _usesCompactResult ? (expanded ? 0.07f : 0.155f) : 0.07f),
            UILayoutRules.GetMenuButtonSize(width, height, UsesTouchLayout()));
        SetButtonLayout(_resultDetailsBtn, new Vector2(0.5f, expanded ? 0.15f : 0.265f),
            UILayoutRules.EnsureTouchButtonSize(new Vector2(320f, 52f), UsesTouchLayout(), portrait));
        if (_resultDetailsScroll != null)
            AnchorText(_resultDetailsScroll.GetComponent<RectTransform>(), 0.5f,
                0.465f, textWidth, portrait ? 460f : 290f);
        RefreshResultDetailsHeight();
    }

    void RefreshResultDetailsHeight()
    {
        if (_resultDetailsScroll == null || !_resultDetailsScroll.gameObject.activeInHierarchy)
            return;
        Canvas.ForceUpdateCanvases();
        _resultDetailsText.rectTransform.sizeDelta = new Vector2(0f,
            Mathf.Max(_resultDetailsScroll.viewport.rect.height,
                _resultDetailsText.preferredHeight + 4f));
    }

    // ═══════════════════════════════════════════════════
    //  State Switching
    // ═══════════════════════════════════════════════════

    void OnGameStateChanged(GameState state)
    {
        _runSettingsOpen = false;
        bool resumedFromPause = state == GameState.Playing
                                && _pausePanel != null
                                && _pausePanel.activeSelf;
        if (state == GameState.Menu || state == GameState.GameOver)
            ReleaseSingleContractVisualState();
        if (state != GameState.Playing)
        {
            _duelFeedbackTimer = 0f;
            if (_duelFeedbackText != null)
                _duelFeedbackText.gameObject.SetActive(false);
        }
        if (_menuRouter != null) _menuRouter.ExitMenu();
        else
        {
            if (_menuPanel != null) _menuPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            if (_characterPanel != null) _characterPanel.SetActive(false);
        }
        if (_hudPanel != null) _hudPanel.SetActive(false);
        if (_pausePanel != null) _pausePanel.SetActive(false);
        if (_gameOverPanel != null) _gameOverPanel.SetActive(false);

        switch (state)
        {
            case GameState.Menu:
                if (_controlHint != null) _controlHint.SetActive(false);
                if (_menuRouter != null) _menuRouter.EnterMenu();
                else if (_menuPanel != null) _menuPanel.SetActive(true);
                RefreshMenuPresentation();
                RefreshTextGeometry(_menuPanel != null
                    ? _menuPanel.transform : null);
                ScheduleTextRefresh(_menuPanel != null
                    ? _menuPanel.transform : null);
                break;

            case GameState.Playing:
                if (_hudPanel != null) _hudPanel.SetActive(true);
                SelectForNavigation(null);
                if (!resumedFromPause)
                {
                    if (_gm != null && _gm.IsSingleContractRun)
                        _hasStartedSingleContractRun = true;
                    if (_echoHudPresenter != null)
                        _echoHudPresenter.ResetRun();
                    _lastDuelFeedbackSequence = -1;
                }
                _nextDuelRefresh = 0f;
                RefreshDuelHud();
                ShowControlHintIfNeeded();
                OnScoreChanged(_gm != null ? _gm.Score : 0);
                OnCoinsChanged(_gm != null ? _gm.Coins : 0);
                OnDistanceChanged(_gm != null ? _gm.Distance : 0);
                ScheduleTextRefresh(_hudPanel != null
                    ? _hudPanel.transform : null);
                break;

            case GameState.Paused:
                if (_controlHint != null) _controlHint.SetActive(false);
                if (_hudPanel != null) _hudPanel.SetActive(true);
                if (_pausePanel != null) _pausePanel.SetActive(true);
                SelectForNavigation(_resumeBtn);
                ScheduleTextRefresh(_pausePanel != null
                    ? _pausePanel.transform : null);
                break;

            case GameState.GameOver:
                if (_controlHint != null) _controlHint.SetActive(false);
                if (_gameOverPanel != null) _gameOverPanel.SetActive(true);
                SelectForNavigation(_restartBtn);
                AIShadowRunner resultShadow = AIShadowRunner.Instance;
                string singleContractResultText = resultShadow != null
                    ? resultShadow.FinalizeRunIfNeeded() : "";
                bool singleContractResult = IsSingleContractPresentation(
                    resultShadow);
                bool asyncResult = _gm != null && _gm.IsAsyncChallengeRun;
                bool useResultFacts = singleContractResult && resultShadow != null
                    && resultShadow.LastSingleContractResultFacts.available;
                SingleContractResultViewData resultPresentation = useResultFacts
                    ? EchoRunPresentation.BuildSingleContractResultView(resultShadow.LastSingleContractResultFacts)
                    : default;
                _resultNextAction = useResultFacts ? resultPresentation.nextAction
                    : SingleContractResultNextAction.RestartLocal;
                if (singleContractResult && resultShadow != null)
                {
                    SingleContractHudData resultView =
                        EchoHudPresenter.BuildSingleContractHudData(
                            _gm, resultShadow, "");
                    singleContractResultText = resultView.result;
                }
                RunEndReason resultReason = _gm != null
                    ? _gm.LastEndReason : RunEndReason.None;
                bool wasChallenge = resultShadow != null
                                    && resultShadow.LastRunWasChallenge;
                bool won = resultShadow != null && resultShadow.LastRunWon;
                AsyncChallengeResult asyncSnapshot = asyncResult
                    ? _gm.LastAsyncChallengeResult : null;
                if (asyncSnapshot != null)
                {
                    resultReason = asyncSnapshot.reason;
                    won = asyncSnapshot.won;
                }
                bool settlementSaved = resultShadow != null
                                       && (resultShadow
                                               .LastSingleContractCommitSucceeded
                                           || resultShadow
                                               .LastRunWasTransientValidation);
                bool identityPromoted = resultShadow != null
                                        && resultShadow
                                            .LastSingleContractIdentityPromoted;
                int resultGeneration = resultShadow != null
                    ? resultShadow.Generation : 0;
                ActiveEchoIdentity resultIdentity = resultShadow != null
                    ? resultShadow.ActiveSingleContractIdentityPreview : null;
                bool routeMemoryReady = resultIdentity != null
                                        && !resultIdentity
                                            .RequiresRouteCalibration;
                if (_restartBtn != null)
                    SetButtonLabel(_restartBtn, useResultFacts ? resultPresentation.primaryAction
                        : asyncResult ? "再挑战" : singleContractResult
                        ? GetSingleContractGameOverActionLabel(resultReason,
                            wasChallenge, identityPromoted, resultGeneration,
                            routeMemoryReady)
                        : GetGameOverActionLabel(resultReason,
                            wasChallenge, won, resultGeneration));
                if (_gameOverTitleText != null && resultShadow != null)
                {
                    if (useResultFacts)
                    {
                        _gameOverTitleText.text = resultPresentation.title;
                        _gameOverTitleText.color = resultPresentation.kind == SingleContractResultKind.SaveFailed
                            || resultPresentation.kind == SingleContractResultKind.ValidationIsolationFailed
                            || resultPresentation.kind == SingleContractResultKind.ObservationGenerationFailed
                            ? Danger : won || identityPromoted ? Success : Primary;
                    }
                    else if (asyncResult)
                    {
                        _gameOverTitleText.text = GetAsyncChallengeGameOverTitle(
                            resultReason, won);
                        _gameOverTitleText.color = won ? Success : Danger;
                    }
                    else if (singleContractResult)
                    {
                        _gameOverTitleText.text =
                            GetSingleContractGameOverTitle(
                                singleContractResultText, resultReason,
                                wasChallenge, won);
                        SingleContractResultTone tone =
                            GetSingleContractGameOverTone(
                                settlementSaved, wasChallenge, won,
                                identityPromoted, routeMemoryReady,
                                singleContractResultText.StartsWith(
                                    "回声形成遇到问题"));
                        _gameOverTitleText.color = tone
                            == SingleContractResultTone.Success
                            ? Success
                            : tone == SingleContractResultTone.Progress
                                ? Primary : Danger;
                    }
                    else
                    {
                        bool interrupted = resultReason
                                           == RunEndReason.Collision;
                        _gameOverTitleText.text = interrupted
                            ? "赛程中断"
                            : !resultShadow.LastRunWasChallenge
                                ? (resultShadow.Generation > 0
                                    ? "校准完成" : "继续校准")
                                : resultShadow.LastRunWon
                                    ? "契约完成" : "回声胜出";
                        _gameOverTitleText.color = resultShadow.LastRunWon
                            ? Success : Danger;
                    }
                }
                if (_gm != null)
                {
                    string newRecord = _gm.IsNewHighScore ? "\n新纪录!" : "";
                    if (_finalScoreText != null)
                        _finalScoreText.text = "得分: " + _gm.Score + newRecord;
                    if (_highScoreText != null)
                        _highScoreText.text = "最高分: " + _gm.HighScore;
                    if (_coinResultText != null)
                        _coinResultText.text = "金币: " + _gm.Coins + "  |  总计: " + _gm.TotalCoins;
                    if (_gameOverStatsText != null)
                        _gameOverStatsText.text = "得分 " + _gm.Score
                                                   + (_gm.IsNewHighScore ? "（新纪录）" : "")
                                                   + (singleContractResult ? "" : " · 距离 " + _gm.Distance.ToString("0") + "m")
                                                   + " · 金币 " + _gm.Coins;
                }
                PresentResultSummary(singleContractResultText,
                    _gameOverTitleText != null ? _gameOverTitleText.text : "", singleContractResult,
                    useResultFacts ? resultPresentation.summary : null);
                if (_goToMenuBtn != null)
                    _goToMenuBtn.gameObject.SetActive(!useResultFacts
                        || resultPresentation.nextAction != SingleContractResultNextAction.ReturnToMenu);
                if (asyncResult)
                {
                    if (_gameOverStatsText != null)
                        _gameOverStatsText.text = "距离 "
                            + (asyncSnapshot != null ? asyncSnapshot.distanceMeters : _gm.Distance).ToString("0.0")
                            + "m · 本局金币 " + _gm.Coins + "（不入钱包）";
                    if (_shadowResultText != null)
                    {
                        float finalLead = asyncSnapshot != null ? asyncSnapshot.playerLeadMeters
                            : resultShadow != null ? resultShadow.PlayerLead : 0f;
                        SingleContractHudData finalRace = EchoRunPresentation.BuildSingleContractHud(
                            new SingleContractHudInput
                            {
                                visualState = SingleContractVisualState.Challenge,
                                leadMeters = finalLead
                            });
                        _shadowResultText.text = finalRace.lead + "\n"
                            + (useResultFacts ? resultPresentation.summary
                                : "本局不影响你的回声成长和本地纪录");
                    }
                    if (_resultDetailsBtn != null) _resultDetailsBtn.gameObject.SetActive(false);
                }
                ScheduleTextRefresh(_gameOverPanel != null
                    ? _gameOverPanel.transform : null);
                break;
        }
    }

    public static string GetAsyncChallengeGameOverTitle(RunEndReason reason, bool won)
    {
        if (reason != RunEndReason.FinishReached) return "好友挑战中断";
        return won ? "你跑赢了好友影子" : "好友影子领先";
    }

    void ExecuteResultAction()
    {
        if (_gm == null) return;
        if (_resultNextAction == SingleContractResultNextAction.ReturnToMenu)
            _gm.ReturnToMenu();
        else
            // Restart retains the frozen async configuration and selects local
            // observation/challenge from the currently saved identity.
            _gm.Restart();
    }

    public static string GetGameOverActionLabel(RunEndReason endReason,
        bool wasChallenge, bool won, int generation)
    {
        if (endReason != RunEndReason.FinishReached)
            return "重新挑战";
        if (!wasChallenge)
            return generation > 0 ? "挑战下一代" : "继续校准";
        return won ? "挑战下一代" : "重新挑战";
    }

    public static string GetSingleContractGameOverTitle(
        string result, RunEndReason endReason, bool wasChallenge, bool won)
    {
        string safe = (result ?? "").Trim();
        int newline = safe.IndexOf('\n');
        string firstLine = newline >= 0
            ? safe.Substring(0, newline).Trim() : safe;
        if (!wasChallenge)
        {
            if (safe.Contains("保存失败")) return "回声保存失败";
            bool savedCalibration = firstLine.StartsWith("校准完成")
                                    && !safe.Contains("保存失败");
            if (savedCalibration) return "校准完成";
            if (firstLine.StartsWith("第")
                && firstLine.Contains("代回声已经形成"))
                return firstLine;
            return firstLine.StartsWith("AI 看到了你的跑法")
                   || firstLine.StartsWith("回声形成遇到问题")
                ? firstLine : "AI 看到了你的跑法";
        }

        if (firstLine.StartsWith("你跑赢了")
            || firstLine.Contains("回声胜出")) return firstLine;
        return won ? "你跑赢了回声" : "回声胜出";
    }

    public static SingleContractResultTone GetSingleContractGameOverTone(
        bool settlementSaved, bool wasChallenge, bool won,
        bool identityPromoted, bool routeMemoryReady,
        bool calibrationGenerationError = false)
    {
        if (!settlementSaved || calibrationGenerationError)
            return SingleContractResultTone.Danger;
        if (wasChallenge)
            return won ? SingleContractResultTone.Success
                : SingleContractResultTone.Danger;
        return identityPromoted && routeMemoryReady
            ? SingleContractResultTone.Success
            : SingleContractResultTone.Progress;
    }

    public static string GetSingleContractGameOverActionLabel(
        RunEndReason endReason, bool wasChallenge, bool identityPromoted,
        int generation, bool routeMemoryReady)
    {
        if (!routeMemoryReady) return "让它再观察一局";
        if (!identityPromoted && !wasChallenge) return "再跑一局";
        if (!wasChallenge)
            return "挑战第" + Mathf.Max(1, generation) + "代回声";
        if (identityPromoted)
            return "挑战第" + Mathf.Max(1, generation) + "代回声";
        return "重试第" + Mathf.Max(1, generation) + "代回声";
    }

    void OnScoreChanged(int score)
    {
        RefreshStats();
    }

    void ReleaseSingleContractVisualState()
    {
        if (_echoHudPresenter != null)
        {
            _echoHudPresenter.ReleaseSingleContractVisualState();
            return;
        }

        EchoPhaseVisualController visual = EchoPhaseVisualController.Instance;
        if (visual != null && visual.UsesSingleContractVisualState)
            visual.ReleaseSingleContractVisualState();
    }

    void OnDestroy()
    {
        ReleaseSingleContractVisualState();
        if (_gm != null)
        {
            _gm.OnStateChanged.RemoveListener(OnGameStateChanged);
            _gm.OnScoreChanged.RemoveListener(OnScoreChanged);
            _gm.OnCoinsChanged.RemoveListener(OnCoinsChanged);
            _gm.OnDistanceChanged.RemoveListener(OnDistanceChanged);
        }
        EchoRunAccessibility.Changed -= OnAccessibilityChanged;
        _roundedUi.Dispose();
    }

    void OnCoinsChanged(int coins)
    {
        RefreshStats();
    }

    void OnDistanceChanged(float dist)
    {
        RefreshStats();
    }

    void RefreshStats()
    {
        if (_statsText == null || _gm == null) return;
        _statsText.text = "SCORE " + _gm.Score.ToString("D5")
                          + "   RANGE " + Mathf.FloorToInt(_gm.Distance).ToString("D3") + "m"
                          + "   FINISH " + Mathf.CeilToInt(_gm.RemainingDistance).ToString("D3") + "m"
                          + "   SHARDS " + _gm.Coins.ToString("D2");
    }

    // ═══════════════════════════════════════════════════
    //  UI Helpers
    // ═══════════════════════════════════════════════════

    GameObject NewPanel(string name, Color color)
    {
        GameObject panel = new GameObject(name, typeof(Image));
        Image image = panel.GetComponent<Image>();
        image.color = color;
        panel.transform.SetParent(_safeAreaRoot != null ? _safeAreaRoot : transform, false);
        Stretch(panel.GetComponent<RectTransform>());
        return panel;
    }

    void ShowControlHintIfNeeded()
    {
        if (_controlHint == null || _controlHintText == null) return;
        if (IsSingleContractPresentation(AIShadowRunner.Instance))
        {
            RefreshSingleContractTeaching();
            return;
        }
        bool firstCalibration = AIShadowRunner.Instance == null
                                || AIShadowRunner.Instance.Generation <= 0;
        if (!firstCalibration)
        {
            _controlHint.SetActive(false);
            return;
        }

        _controlHintText.text = UsesTouchLayout()
            ? "左右滑动变道  ·  上滑跳跃  ·  下滑滑铲"
            : "A / D 或拖动变道  ·  W / 空格跳跃  ·  S / Ctrl 滑铲";
        _controlHintTimer = ControlHintDuration;
        _controlHint.SetActive(true);
    }

    static string ControlInstructions(bool touch)
    {
        return touch ? "左右滑动变道 · 上滑跳跃 · 下滑滑铲"
            : "A / D 变道 · W / 空格跳跃 · S / Ctrl 滑铲";
    }

    public static string BuildSingleContractTeaching(bool firstObservation,
        int jumpSamples, int slideSamples, int settledGates, bool gateActive,
        bool touch)
    {
        string controls = firstObservation && (jumpSamples <= 0 || slideSamples <= 0)
            ? ControlInstructions(touch) : "";
        string routes = settledGates == 0 && gateActive
            ? EchoRunPresentation.SingleContractRouteGuide
              + "\n反制路成功通过才获得领先奖励" : "";
        return controls + (!string.IsNullOrEmpty(controls) && !string.IsNullOrEmpty(routes)
            ? "\n" : "") + routes;
    }

    void RefreshSingleContractTeaching()
    {
        if (_controlHint == null || _controlHintText == null) return;
        AIShadowRunner shadow = AIShadowRunner.Instance;
        if (_gm == null || _gm.State != GameState.Playing || shadow == null)
        {
            _controlHint.SetActive(false);
            return;
        }
        string teaching = BuildSingleContractTeaching(
            !shadow.HasActiveOpponent && shadow.Generation <= 0,
            shadow.JumpTrainingSampleCount, shadow.SlideTrainingSampleCount,
            shadow.SingleContractRuntime != null ? shadow.SingleContractRuntime.SettlementCount : 0,
            shadow.IsCurrentSingleContractPredictionGateActive, UsesTouchLayout());
        _controlHintText.text = teaching;
        _controlHint.SetActive(!string.IsNullOrEmpty(teaching));
    }

    void RefreshRunGuide()
    {
        if (_runGuideText == null) return;
        string content = ControlInstructions(UsesTouchLayout()) + "\n"
            + EchoRunPresentation.SingleContractRouteGuide
            + "\n反制路成功通过才获得领先奖励；中立路不算反制。";
        AIShadowRunner shadow = AIShadowRunner.Instance;
        _showObservationDetails = _gm != null
            && (_gm.State == GameState.Playing || _gm.State == GameState.Paused)
            && _gm.IsSingleContractRun && shadow != null && !shadow.HasActiveOpponent;
        if (_showObservationDetails)
        {
            SingleContractHudData data = EchoHudPresenter.BuildSingleContractHudData(_gm, shadow, "");
            content += "\n\n本局观察详情\n" + data.calibrationDetails
                + "\n单项完成不代表全部达标；本局未形成新代时，下局需要重新观察。";
        }
        _runGuideText.text = content;
        LayoutSettings(UILayoutRules.IsCompactPortrait(Screen.width, Screen.height));
    }

    void UpdateLandscapeGuard()
    {
        if (_landscapeGuard == null) return;
        bool shouldShow = UILayoutRules.ShouldShowLandscapeGuard(
            Screen.width, Screen.height, UsesTouchLayout(),
            AllowsPortraitLayout());
        if (_landscapeGuard.activeSelf == shouldShow) return;

        _landscapeGuard.SetActive(shouldShow);
        if (shouldShow)
        {
            _landscapeGuard.transform.SetAsLastSibling();
            if (_gm != null && _gm.State == GameState.Playing)
                _gm.Pause();
        }
    }

    void RefreshMenuPresentation()
    {
        AIShadowRunner shadow = AIShadowRunner.Instance;
        RefreshEchoReportNavigation();
        if (_gm != null && _gm.ConfiguredGameplayFlowMode == GameplayFlowMode.AsyncChallenge)
        {
            if (_menuGenerationText != null) _menuGenerationText.text = "挑战好友的回声";
            if (_menuLearnedText != null) _menuLearnedText.text = "本局使用已确认的好友跑法";
            if (_menuRuleText != null) _menuRuleText.text = "跑到终点，争取领先。\n本局不影响你的回声成长和本地纪录。";
            if (_menuObjectiveText != null) _menuObjectiveText.text = "";
            SetButtonLabel(_startBtn, "开始好友挑战");
            return;
        }
        bool singleContract = _gm != null
                              && _gm.ConfiguredGameplayFlowMode
                              == GameplayFlowMode.SingleContract;
        if (singleContract)
        {
            EchoMenuViewData singleContractView =
                EchoRunPresentation.BuildSingleContractMenu(
                    shadow != null
                        ? shadow.ActiveSingleContractIdentityPreview : null,
                    shadow != null ? shadow.minimumJumpSamples : 2,
                    shadow != null ? shadow.minimumSlideSamples : 2,
                    shadow != null ? shadow.minimumTrainingSamples : 24,
                    shadow != null
                        ? shadow.minimumActiveTrainingSamples : 6,
                    shadow != null ? shadow.minimumActionCategories : 2);
            if (_menuGenerationText != null)
                _menuGenerationText.text = singleContractView.generation;
            if (_menuLearnedText != null)
                _menuLearnedText.text = singleContractView.learned;
            if (_menuRuleText != null)
                _menuRuleText.text = singleContractView.rule + "\n" + singleContractView.objective;
            if (_menuObjectiveText != null)
                _menuObjectiveText.text = singleContractView.objective;
            if (_menuProtocolText != null)
                _menuProtocolText.text = _hasStartedSingleContractRun
                    ? "本机 AI · 观察选路与动作"
                    : UsesTouchLayout()
                        ? "左右滑动变道 · 上滑跳跃 · 下滑滑铲"
                        : "A / D 变道 · W / 空格跳跃 · S / Ctrl 滑铲";
            SetButtonLabel(_startBtn, singleContractView.primaryAction);
            return;
        }

        int generation = shadow != null ? shadow.Generation : 0;
        EchoMenuViewData view = EchoRunPresentation.BuildMenu(
            generation, StyleTracker.GetSnapshot(),
            shadow != null ? shadow.minimumJumpSamples : 2,
            shadow != null ? shadow.minimumSlideSamples : 2,
            shadow != null ? shadow.ContractPreview : null,
            shadow != null ? shadow.EchoClarity : 1f);

        if (_menuGenerationText != null)
            _menuGenerationText.text = view.generation;
        if (_menuLearnedText != null)
            _menuLearnedText.text = generation > 0
                ? "它记住了：" + view.learned
                : "等待采样：" + view.learned;
        if (_menuRuleText != null)
            _menuRuleText.text = generation > 0
                ? "本轮契约：" + view.rule
                : "校准目标：" + view.objective;
        if (_menuObjectiveText != null)
            _menuObjectiveText.text = generation > 0
                ? "挑战目标：" + view.objective
                : "完成校准后生成第 1 代回声";
        SetButtonLabel(_startBtn, view.primaryAction);
    }

    void StartGameFromHome()
    {
        if (_gm == null || (_menuRouter != null && !_menuRouter.IsHome)) return;
        _gm.StartGame();
    }

    public static bool ShouldShowLandscapeGuard(
        int width, int height, bool touchLayout)
    {
        return UILayoutRules.ShouldShowLandscapeGuard(
            width, height, touchLayout, true);
    }

    private static bool AllowsPortraitLayout()
    {
        // All current presentation surfaces and the world camera adapt to
        // portrait. This also keeps resizable Windows touch devices usable.
        return true;
    }

    private static bool UsesTouchLayout()
    {
#if MINIGAME_SUBPLATFORM_WEIXIN
        return true;
#else
        return Application.isMobilePlatform || Input.touchSupported;
#endif
    }

    void ApplySafeArea(bool force = false)
    {
        if (_safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
        Rect safeArea = UILayoutRules.NormalizeSafeArea(
            WeChatSafeArea.Resolve(Screen.safeArea, Screen.width, Screen.height),
            Screen.width, Screen.height);
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
        if (!force && safeArea == _lastSafeArea && screenSize == _lastScreenSize) return;

        _lastSafeArea = safeArea;
        _lastScreenSize = screenSize;
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;
        _safeAreaRoot.anchorMin = anchorMin;
        _safeAreaRoot.anchorMax = anchorMax;
        _safeAreaRoot.offsetMin = Vector2.zero;
        _safeAreaRoot.offsetMax = Vector2.zero;
        _safeAreaRoot.anchoredPosition = Vector2.zero;
        _safeAreaRoot.localScale = Vector3.one;
        _safeAreaRoot.localRotation = Quaternion.identity;
#if MINIGAME_SUBPLATFORM_WEIXIN
        UpdateSafeAreaCover(ref _safeAreaTopCover, "WeChatTopInset",
            new Vector2(0f, anchorMax.y), Vector2.one);
        UpdateSafeAreaCover(ref _safeAreaBottomCover, "WeChatBottomInset",
            Vector2.zero, new Vector2(1f, anchorMin.y));
#endif
        ApplyResponsiveLayout();
    }

#if MINIGAME_SUBPLATFORM_WEIXIN
    void RefreshSafeAreaCoverTone()
    {
        bool menuHome = _menuPanel != null && _menuPanel.activeSelf;
        bool lightPage = (_gm != null && _gm.State == GameState.Menu)
            || (_settingsPanel != null && _settingsPanel.activeSelf)
            || (_characterPanel != null && _characterPanel.activeSelf);
        Color tone = lightPage ? PageBackdrop : Ink;
        if (_safeAreaTopCover != null)
            _safeAreaTopCover.GetComponent<Image>().color = menuHome
                ? new Color32(0, 12, 27, 255) : tone;
        if (_safeAreaBottomCover != null)
            _safeAreaBottomCover.GetComponent<Image>().color = tone;
    }

    void UpdateSafeAreaCover(ref RectTransform cover, string name,
        Vector2 min, Vector2 max)
    {
        if (cover == null)
        {
            var image = new GameObject(name, typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(_safeAreaRoot.parent, false);
            image.color = Ink;
            image.raycastTarget = false;
            cover = image.rectTransform;
        }
        cover.anchorMin = min;
        cover.anchorMax = max;
        cover.offsetMin = cover.offsetMax = Vector2.zero;
    }
#endif

    // Keep visual track thickness independent of the generous slider hit area.
    // Landscape uses the available width; portrait stacks the same controls.
    void LayoutSettings(bool portrait)
    {
        if (_settingsContent == null) return;
        _settingsContent.sizeDelta = portrait
            ? new Vector2(900, _showObservationDetails ? 2980 : 2380)
            : new Vector2(1440, _showObservationDetails ? 1620 : 1280);
        float left = portrait ? 64 : 56;
        float right = portrait ? 64 : 772;
        float width = portrait ? 772 : 612;
        float controlsHeight = portrait ? 104 : 72;
        float displayTop = portrait ? 690 : 0;
        float difficultyTop = portrait ? 1080 : 324;
        float cameraTop = portrait ? 1364 : 594;
        float accessibilityTop = portrait ? 1706 : 714;
        float accessibilityLeft = portrait ? right : left;
        float cardX = portrait ? 0 : 716;
        PlaceSetting("AudioCard", 0, 0, portrait ? 900 : 688, portrait ? 666 : 690);
        PlaceSetting("DisplayCard", cardX, displayTop, portrait ? 900 : 724,
            portrait ? 366 : 300);
        PlaceSetting("DifficultyCard", cardX, difficultyTop,
            portrait ? 900 : 724, portrait ? 260 : 246);
        PlaceSetting("CameraCard", cardX, cameraTop,
            portrait ? 900 : 724, portrait ? 318 : 310);
        PlaceSetting("AccessibilityCard", 0, accessibilityTop,
            portrait ? 900 : 688, 234);
        float guideTop = portrait ? 1964 : 972;
        PlaceSetting("RunGuideCard", 0, guideTop, portrait ? 900 : 1440,
            _showObservationDetails ? (portrait ? 992 : 624) : (portrait ? 392 : 284));
        PlaceSetting("RunGuideLabel", left, guideTop + 32,
            portrait ? 772 : 1328, 54);
        PlaceSetting("RunGuideText", left, guideTop + 108,
            portrait ? 772 : 1328, _showObservationDetails ? (portrait ? 832 : 468) : 240);
        PlaceSetting("AudioSectionLabel", left, 36, width, 48);
        string[] labels = { "MasterVolumeValue", "BgmValue", "SfxValue" };
        string[] sliders = { "MasterVolumeSlider", "BgmSlider", "SfxSlider" };
        for (int i = 0; i < 3; i++)
        {
            PlaceSetting(labels[i], left, 108 + i * 126, width, 44);
            PlaceSetting(sliders[i], left + 20, 158 + i * 126, width - 40, 72);
        }
        PlaceSetting("AudioMute", left, 528, 250, controlsHeight);
        PlaceSetting("FpsLabel", right, displayTop + 36, width, 44);
        PlaceSetting("FpsStatus", right, displayTop + 86, width, 38);
        PlaceSetting("FpsHint", right, displayTop + (portrait ? 274 : 232), width, 64);
        PlaceSetting("DifficultyLabel", right, difficultyTop + 32, width, 44);
        PlaceSetting("DifficultyStatus", right, difficultyTop + 82, width, 40);
        PlaceSetting("CameraViewLabel", right, cameraTop + 32, width, 44);
        float cameraButtonWidth = (width - 12) / 2;
        PlaceSetting("CameraViewLow", right, cameraTop + 102,
            cameraButtonWidth, controlsHeight);
        PlaceSetting("CameraViewHigh", right + cameraButtonWidth + 12,
            cameraTop + 102, cameraButtonWidth, controlsHeight);
        PlaceSetting("CameraViewHint", right, cameraTop + (portrait ? 230 : 200), width, 64);
        PlaceSetting("AccessibilityLabel", accessibilityLeft,
            accessibilityTop + 32, width, 44);
        string[][] rows = {
            new[] { "Fps30", "Fps60", "Fps120" },
            new[] { "DifficultyRelaxed", "DifficultyStandard", "DifficultyIntense" },
            new[] { "LargeText", "HighContrast", "ReducedMotion" }
        };
        float[] rowY = { displayTop + 144, difficultyTop + 140,
            accessibilityTop + 102 };
        float buttonWidth = (width - 24) / 3;
        for (int row = 0; row < rows.Length; row++)
            for (int col = 0; col < 3; col++)
                PlaceSetting(rows[row][col], (row == 2 ? accessibilityLeft : right)
                    + col * (buttonWidth + 12), rowY[row], buttonWidth, controlsHeight);
        foreach (Text text in _settingsContent.GetComponentsInChildren<Text>(true))
        {
            bool button = text.GetComponentInParent<Button>(true) != null;
            text.alignment = button ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            if (!button) text.verticalOverflow = VerticalWrapMode.Overflow;
            int size = portrait ? (button ? 34 : 38) : 28;
            if (text == _fpsStatusText || text == _difficultyStatusText)
                size = portrait ? 30 : 24;
            else if (text.name == "FpsHint" || text.name == "CameraViewHint")
                size = portrait ? 28 : 22;
            else if (text == _runGuideText)
                size = portrait ? 28 : 24;
            EchoRunAccessibility.SetBaseFontSize(text, size);
            text.color = PageInk;
            if (!button && text.name.EndsWith("Label")) text.color = SettingsAccent;
            else if (text == _fpsStatusText || text == _difficultyStatusText
                     || text.name == "FpsHint" || text.name == "CameraViewHint")
                text.color = PageMuted;
            Outline outline = text.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }
        SetTopLeftButtonLayout(_settingsBackBtn, new Vector2(180, portrait ? 104 : 76));
        EchoRunAccessibility.ApplyToHierarchy(_settingsContent);
    }

    void PlaceSetting(string name, float x, float y, float width, float height)
    {
        RectTransform rect = _settingsContent.Find(name) as RectTransform;
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    void ApplyResponsiveLayout()
    {
        bool portrait = UILayoutRules.IsCompactPortrait(
            Screen.width, Screen.height);
        bool largeTargets = UsesTouchLayout() || portrait;
        FitMenuBackground();
        if (_canvasScaler != null)
            _canvasScaler.referenceResolution = UILayoutRules.GetReferenceResolution(
                Screen.width, Screen.height);
        LayoutCharacter(portrait);

        SetTopLeftButtonLayout(_characterBackBtn, TouchButtonSize(portrait
            ? new Vector2(180f, 104f) : new Vector2(180f, 76f), largeTargets, portrait));
        LayoutSettings(portrait);

        if (_hudStatsPanel != null)
        {
            RectTransform stats = _hudStatsPanel.GetComponent<RectTransform>();
            stats.sizeDelta = portrait
                ? new Vector2(460f, 58f)
                : new Vector2(430f, 52f);
            stats.anchoredPosition = portrait
                ? new Vector2(16f, -146f)
                : new Vector2(18f, -18f);
        }
        if (_hudContractPanel != null)
        {
            RectTransform contract = _hudContractPanel.GetComponent<RectTransform>();
            contract.sizeDelta = portrait
                ? new Vector2(820f, 112f)
                : new Vector2(700f, 92f);
            contract.anchoredPosition = portrait
                ? new Vector2(0f, -20f)
                : new Vector2(0f, -18f);
        }
        if (_buffGroup != null)
        {
            RectTransform buff = _buffGroup.GetComponent<RectTransform>();
            buff.anchoredPosition = portrait
                ? new Vector2(22f, -220f)
                : new Vector2(22f, -82f);
            buff.sizeDelta = portrait
                ? new Vector2(520f, 42f)
                : new Vector2(360f, 30f);
        }
        if (_duelFeedbackText != null)
        {
            RectTransform feedback = _duelFeedbackText.rectTransform;
            feedback.sizeDelta = portrait
                ? new Vector2(820f, 72f)
                : new Vector2(660f, 52f);
        }
        if (_controlHint != null)
        {
            RectTransform hint = _controlHint.GetComponent<RectTransform>();
            hint.sizeDelta = portrait
                ? new Vector2(940f, 154f)
                : new Vector2(980f, 130f);
        }
        SetButtonSize(_pauseBtn, largeTargets
            ? new Vector2(104f, 104f)
            : new Vector2(48f, 48f));
        Vector2 settingsSize = largeTargets ? new Vector2(144f, 96f) : new Vector2(120f, 64f);
        LayoutTopRightControl(_runSettingsBtn, settingsSize, new Vector2(-18f, -16f));
        LayoutTopRightControl(_pauseBtn, largeTargets ? new Vector2(96f, 96f)
            : new Vector2(64f, 64f), new Vector2(-30f - settingsSize.x, -16f));
        SetButtonSize(_resumeBtn, TouchButtonSize(
            new Vector2(400f, 100f), largeTargets, portrait));
        SetButtonSize(_pauseToMenuBtn, TouchButtonSize(portrait
            ? new Vector2(420f, 104f) : new Vector2(320f, 80f),
            largeTargets, portrait));
        SetButtonSize(_restartBtn, UILayoutRules.GetRestartButtonSize(
            Screen.width, Screen.height, UsesTouchLayout()));
        SetButtonSize(_goToMenuBtn, UILayoutRules.GetMenuButtonSize(
            Screen.width, Screen.height, UsesTouchLayout()));
        if (_shadowResultText != null)
            _shadowResultText.rectTransform.sizeDelta =
                UILayoutRules.GetResultTextSize(Screen.width, Screen.height);
        ApplyResultSummaryLayout(Screen.width, Screen.height);
        LayoutMenu(portrait, largeTargets);
    }

    static void RefreshTextGeometry(Transform root)
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

    void ScheduleTextRefresh(Transform root)
    {
        if (root == null) return;
        _pendingTextRefreshRoot = root;
        // Dynamic fonts can grow their atlas while a newly activated page is
        // rebuilding. Refresh on successive frames so earlier labels do not
        // keep geometry that points at the previous atlas.
        _pendingTextRefreshFrames = 3;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void AnchorText(RectTransform rt, float ax, float ay, float w, float h)
    {
        rt.anchorMin = new Vector2(ax, ay); rt.anchorMax = new Vector2(ax, ay);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
    }

    static void AddPageArtwork(Transform parent)
    {
        // These pages share the live city behind their translucent surface.
        // A separate concept-art world made the menu promise different gameplay.
        Image surface = parent.GetComponent<Image>();
        if (surface != null) surface.color = WithAlpha(PageBackdrop, 0.94f);
        var header = new GameObject("PageHeader", typeof(Image));
        header.transform.SetParent(parent, false);
        Image headerImage = header.GetComponent<Image>();
        headerImage.color = WithAlpha(PageSurface, .65f);
        headerImage.raycastTarget = false;
        RectTransform headerRect = headerImage.rectTransform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = Vector2.one;
        headerRect.pivot = new Vector2(.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 132f);
        headerRect.anchoredPosition = Vector2.zero;
        var divider = new GameObject("HeaderRule", typeof(Image));
        divider.transform.SetParent(header.transform, false);
        Image dividerImage = divider.GetComponent<Image>();
        dividerImage.color = EchoRunUITheme.PageRule;
        dividerImage.raycastTarget = false;
        RectTransform dividerRect = dividerImage.rectTransform;
        dividerRect.anchorMin = new Vector2(.04f, 0f);
        dividerRect.anchorMax = new Vector2(.96f, 0f);
        dividerRect.sizeDelta = new Vector2(0f, 1.6f);
        dividerRect.anchoredPosition = Vector2.zero;
    }

    void AddMenuGrid(Transform parent)
    {
        for (int i = 0; i < 12; i++)
        {
            float angle = i / 12f * Mathf.PI * 2f;
            Vector2 anchor = new Vector2(
                0.5f + Mathf.Cos(angle) * 0.43f,
                0.5f + Mathf.Sin(angle) * 0.36f);
            GameObject node = new GameObject("TransitNode", typeof(Image));
            node.transform.SetParent(parent, false);
            Image image = node.GetComponent<Image>();
            image.color = WithAlpha(Primary, i % 3 == 0 ? 0.09f : 0.045f);
            ApplyRounded(image);
            RectTransform rt = node.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = i % 3 == 0 ? new Vector2(8f, 8f) : new Vector2(5f, 5f);
            rt.anchoredPosition = Vector2.zero;
        }
    }

    void AddPanelRule(Transform parent, Color color)
    {
        GameObject accent = new GameObject("SignalRule", typeof(Image));
        accent.transform.SetParent(parent, false);
        Image image = accent.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        ApplyRounded(image);
        RectTransform rt = accent.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(58f, 6f);
        rt.anchoredPosition = new Vector2(18f, -10f);
    }

    Text MakeText(string name, Transform parent, string content, int size, TextAnchor align)
    {
        GameObject go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        if (_font != null) t.font = _font;
        EchoRunAccessibility.Prepare(t);
        return t;
    }

    Text MakeLabel(string name, Transform parent, string content, Vector2 anchor)
    {
        Text label = MakeText(name, parent, content, 28, TextAnchor.MiddleCenter);
        label.color = Primary;
        label.fontStyle = FontStyle.Bold;
        // CJK font line metrics exceed the old 40px box, yielding no glyphs.
        label.verticalOverflow = VerticalWrapMode.Overflow;
        AnchorText(label.GetComponent<RectTransform>(), anchor.x, anchor.y, 300, 60);
        return label;
    }

    void AddOutline(GameObject go, Color color)
    {
        Outline o = go.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(2.5f, -2.5f);
    }

    void AddShadow(GameObject go, Color color)
    {
        Shadow s = go.AddComponent<Shadow>();
        s.effectColor = color;
        s.effectDistance = new Vector2(3f, -3f);
    }

    Button MakeButton(string name, Transform parent, string label, int fontSize,
        Vector2 anchor, Vector2 size, Color mainColor, Color edgeColor)
    {
        return MakeButton(name, parent, label, fontSize, anchor, size,
            mainColor, edgeColor, TextPrimary);
    }

    Button MakeButton(string name, Transform parent, string label, int fontSize,
        Vector2 anchor, Vector2 size, Color mainColor, Color edgeColor,
        Color labelColor)
    {
        if (RequiresLargeTouchTargets())
            size.y = Mathf.Max(size.y, 104f);
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        Image background = go.GetComponent<Image>();
        background.color = mainColor;
        ApplyRounded(background);
        UIEdgeTreatment.Surface(background,
            mainColor == EchoRunUITheme.ActionAccent
                ? WithAlpha(Color.white, .30f) : EchoRunUITheme.PageRule, true);
        if (mainColor == EchoRunUITheme.ActionAccent)
        {
            Shadow edge = go.AddComponent<Shadow>();
            edge.effectColor = WithAlpha(edgeColor, .65f);
            edge.effectDistance = new Vector2(0f, -5f);
        }

        Text labelT = MakeText("Label", go.transform, label, fontSize, TextAnchor.MiddleCenter);
        labelT.color = labelColor;
        labelT.fontStyle = FontStyle.Normal;
        Stretch(labelT.GetComponent<RectTransform>());

        Button button = go.GetComponent<Button>();
        UIEdgeTreatment.ButtonStates(button);
        button.onClick.AddListener(() => AudioManager.Instance?.PlayUIClick());
        return button;
    }

    Button MakeSmallButton(string name, Transform parent, string label,
        Vector2 anchor, Vector2 size, Color color)
    {
        if (RequiresLargeTouchTargets())
            size.y = Mathf.Max(size.y, 104f);
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.color = color;
        ApplyRounded(image);
        UIEdgeTreatment.Surface(image, EchoRunUITheme.PageRule, true);

        Text labelT = MakeText("Label", go.transform, label, 28, TextAnchor.MiddleCenter);
        labelT.color = PageInk;
        labelT.fontStyle = FontStyle.Normal;
        Stretch(labelT.GetComponent<RectTransform>());

        Button button = go.GetComponent<Button>();
        UIEdgeTreatment.ButtonStates(button);
        button.onClick.AddListener(() => AudioManager.Instance?.PlayUIClick());
        return button;
    }

    Button MakeIconButton(string name, Transform parent, string label,
        Vector2 anchor, Vector2 size, Color color)
    {
        if (RequiresLargeTouchTargets())
        {
            size.x = Mathf.Max(size.x, 104f);
            size.y = Mathf.Max(size.y, 104f);
        }
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.color = color;
        ApplyRounded(image);
        UIEdgeTreatment.Surface(image, EchoRunUITheme.PageRule, true);

        Text labelT = MakeText("Label", go.transform, label, 28, TextAnchor.MiddleCenter);
        labelT.color = Color.white;
        labelT.fontStyle = FontStyle.Bold;
        Stretch(labelT.GetComponent<RectTransform>());

        Button button = go.GetComponent<Button>();
        UIEdgeTreatment.ButtonStates(button);
        button.onClick.AddListener(() => AudioManager.Instance?.PlayUIClick());
        return button;
    }

    Slider MakeSlider(string name, Transform parent, Vector2 anchor)
    {
        GameObject go = new GameObject(name, typeof(Image), typeof(Slider));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = Color.clear;

        // Background
        GameObject bg = new GameObject("Background", typeof(Image));
        bg.transform.SetParent(go.transform, false);
        Image bgImage = bg.GetComponent<Image>();
        bgImage.color = SettingsControl;
        ApplyRounded(bgImage);
        RectTransform bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.5f); bgRT.anchorMax = new Vector2(1, 0.5f);
        bgRT.sizeDelta = new Vector2(0, 8);
        bgRT.anchoredPosition = Vector2.zero;

        // Fill area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        RectTransform faRT = fillArea.GetComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0, 0.5f);
        faRT.anchorMax = new Vector2(1, 0.5f);
        faRT.sizeDelta = new Vector2(0, 8);
        faRT.anchoredPosition = Vector2.zero;

        // Fill
        GameObject fill = new GameObject("Fill", typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = SettingsAccent;
        ApplyRounded(fillImage);
        RectTransform fRT = fill.GetComponent<RectTransform>();
        Stretch(fRT);

        // Handle slide area
        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        RectTransform haRT = handleArea.GetComponent<RectTransform>();
        haRT.anchorMin = new Vector2(0, 0.5f);
        haRT.anchorMax = new Vector2(1, 0.5f);
        haRT.sizeDelta = Vector2.zero;
        haRT.anchoredPosition = Vector2.zero;

        // Handle
        GameObject handle = new GameObject("Handle", typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = PageInk;
        ApplyRounded(handleImage);
        UIEdgeTreatment.Surface(handleImage, PageBackdrop, true);
        RectTransform hRT = handle.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0, 0.5f); hRT.anchorMax = new Vector2(0, 0.5f);
        float handleSize = 32f;
        hRT.sizeDelta = new Vector2(handleSize, handleSize);
        hRT.anchoredPosition = Vector2.zero;

        Slider slider = go.GetComponent<Slider>();
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.fillRect = fRT;
        slider.handleRect = hRT;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        RectTransform sRT = go.GetComponent<RectTransform>();
        sRT.anchorMin = anchor; sRT.anchorMax = anchor;
        sRT.sizeDelta = RequiresLargeTouchTargets()
            ? new Vector2(600, 72)
            : new Vector2(500, 64);
        sRT.anchoredPosition = Vector2.zero;

        return slider;
    }

    void ApplyRounded(Image image)
    {
        _roundedUi.Apply(image);
    }

    static void SetButtonAnchor(Button button, Vector2 anchor)
    {
        if (button == null) return;
        RectTransform rt = button.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
    }

    static void SetButtonLayout(Button button, Vector2 anchor, Vector2 size)
    {
        if (button == null) return;
        RectTransform rt = button.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }

    static void SetButtonSize(Button button, Vector2 size)
    {
        if (button == null) return;
        button.GetComponent<RectTransform>().sizeDelta = size;
    }

    static void SetTopLeftButtonLayout(Button button, Vector2 size)
    {
        if (button == null) return;
        RectTransform rt = button.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(24f, -24f);
    }

    static bool RequiresLargeTouchTargets()
    {
        return UsesTouchLayout()
               || UILayoutRules.IsCompactPortrait(Screen.width, Screen.height);
    }

    static Vector2 TouchButtonSize(Vector2 requested,
        bool touchLayout, bool portrait)
    {
        return UILayoutRules.EnsureTouchButtonSize(
            requested, touchLayout, portrait);
    }

    static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null) text.text = label;
    }

    static void SelectForNavigation(Selectable selectable)
    {
        UnityEngine.EventSystems.EventSystem eventSystem =
            UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem == null) return;
        eventSystem.SetSelectedGameObject(
            selectable != null ? selectable.gameObject : null);
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
