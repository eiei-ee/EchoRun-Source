using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class PowerUpShopUI : MonoBehaviour
{
    private GameManager _gameManager;
    private MenuScreenRouter _router;
    private GameObject _launcher;
    private GameObject _panel;
    private RectTransform _panelRect;
    private Button _closeButton;
    private Text _walletText;
    private Text _feedbackText;
    private RectTransform _feedbackRect;
    private readonly Text[] _itemState = new Text[4];
    private readonly Button[] _buyButtons = new Button[4];
    private readonly Button[] _equipButtons = new Button[4];
    private readonly RectTransform[] _rows = new RectTransform[4];
    private readonly RectTransform[] _names = new RectTransform[4];
    private readonly RectTransform[] _descriptions = new RectTransform[4];
    private readonly RectTransform[] _states = new RectTransform[4];
    private Vector2Int _lastScreenSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (!PowerUpController.IsAvailable) return;
        if (FindObjectOfType<PowerUpShopUI>() != null) return;
        new GameObject("Power Up Shop UI").AddComponent<PowerUpShopUI>();
    }

    IEnumerator Start()
    {
        if (!PowerUpController.IsAvailable) { enabled = false; yield break; }
        _gameManager = GameManager.Instance;
        Canvas canvas = null;
        for (int i = 0; i < 60
             && (canvas == null || _router == null); i++)
        {
            canvas = FindObjectOfType<Canvas>();
            _router = FindObjectOfType<MenuScreenRouter>();
            if (canvas == null || _router == null) yield return null;
        }
        if (canvas == null || _gameManager == null) yield break;

        Transform parent = canvas.transform.Find("SafeArea") ?? canvas.transform;
        Build(parent);
        if (_router != null)
        {
            _router.Register(MenuScreen.Supply, _panel, _buyButtons[0]);
            _router.RegisterHomeNavigation(_launcher);
        }
        _gameManager.OnStateChanged.AddListener(OnStateChanged);
        _gameManager.OnBankedCoinsChanged.AddListener(OnWalletChanged);
        OnStateChanged(_gameManager.State);
    }

    private void Build(Transform parent)
    {
        Button launcherButton = RuntimePanelFactory.Button("PowerUpLauncher", parent,
            "补给舱", new Vector2(0.50f, 0.12f), new Vector2(180f, 56f),
            RuntimePanelFactory.Raised, 26);
        launcherButton.onClick.AddListener(Open);
        _launcher = launcherButton.gameObject;

        _panel = RuntimePanelFactory.PanelObject("PowerUpShop", parent,
            new Vector2(0.5f, 0.5f), new Vector2(1120f, 720f),
            RuntimePanelFactory.Panel);
        _panelRect = _panel.GetComponent<RectTransform>();

        Text title = RuntimePanelFactory.Text("Title", _panel.transform,
            "补给舱", 40, TextAnchor.MiddleLeft,
            RuntimePanelFactory.TextPrimary);
        title.rectTransform.pivot = new Vector2(0f, 0.5f);
        RuntimePanelFactory.Place(title.rectTransform, new Vector2(0.08f, 0.90f),
            new Vector2(360f, 70f), Vector2.zero);
        Text subtitle = RuntimePanelFactory.Text("Subtitle", _panel.transform,
            "装备将在下一局开场消耗 1 个", 20, TextAnchor.MiddleLeft,
            RuntimePanelFactory.TextMuted);
        subtitle.rectTransform.pivot = new Vector2(0f, 0.5f);
        RuntimePanelFactory.Place(subtitle.rectTransform, new Vector2(0.08f, 0.84f),
            new Vector2(520f, 44f), Vector2.zero);
        _walletText = RuntimePanelFactory.Text("Wallet", _panel.transform, "",
            30, TextAnchor.MiddleRight, RuntimePanelFactory.Reward);
        RuntimePanelFactory.Place(_walletText.rectTransform, new Vector2(0.82f, 0.90f),
            new Vector2(320f, 60f), Vector2.zero);

        for (int i = 0; i < 4; i++) BuildItemRow(i);

        _feedbackText = RuntimePanelFactory.Text("Feedback", _panel.transform, "",
            22, TextAnchor.MiddleLeft, RuntimePanelFactory.TextMuted);
        _feedbackRect = _feedbackText.rectTransform;
        _feedbackText.rectTransform.pivot = new Vector2(0f, 0.5f);
        RuntimePanelFactory.Place(_feedbackText.rectTransform, new Vector2(0.10f, 0.08f),
            new Vector2(700f, 50f), Vector2.zero);
        _closeButton = RuntimePanelFactory.Button("Close", _panel.transform, "返回",
            new Vector2(0.88f, 0.08f), new Vector2(190f, 58f),
            RuntimePanelFactory.Raised, 24);
        _closeButton.onClick.AddListener(Close);
        _panel.SetActive(false);
        ApplyLayout(true);
        Refresh();
    }

    private void BuildItemRow(int index)
    {
        PowerUpId id = (PowerUpId)index;
        PowerUpBalance definition = GameBalanceConfig.GetPowerUp(id);
        float y = 0.75f - index * 0.17f;
        GameObject row = RuntimePanelFactory.PanelObject("Item_" + id, _panel.transform,
            new Vector2(0.5f, y), new Vector2(1000f, 100f),
            EchoRunUITheme.PageRaised);
        _rows[index] = row.GetComponent<RectTransform>();

        string[] glyphs = { "盾", "磁", "×2", "速" };
        Text glyph = RuntimePanelFactory.Text("Glyph", row.transform,
            glyphs[index], 30, TextAnchor.MiddleCenter,
            RuntimePanelFactory.Reward);
        glyph.fontStyle = FontStyle.Bold;
        RuntimePanelFactory.Place(glyph.rectTransform, new Vector2(0.06f, 0.5f),
            new Vector2(72f, 72f), Vector2.zero);

        Text name = RuntimePanelFactory.Text("Name", row.transform,
            definition.displayName, 28, TextAnchor.MiddleLeft,
            RuntimePanelFactory.TextPrimary);
        name.rectTransform.pivot = new Vector2(0f, 0.5f);
        RuntimePanelFactory.Place(name.rectTransform, new Vector2(0.13f, 0.63f),
            new Vector2(260f, 44f), Vector2.zero);
        _names[index] = name.rectTransform;
        Text description = RuntimePanelFactory.Text("Description", row.transform,
            definition.description, 20, TextAnchor.MiddleLeft,
            RuntimePanelFactory.TextMuted);
        description.rectTransform.pivot = new Vector2(0f, 0.5f);
        RuntimePanelFactory.Place(description.rectTransform, new Vector2(0.13f, 0.25f),
            new Vector2(390f, 38f), Vector2.zero);
        _descriptions[index] = description.rectTransform;

        _itemState[index] = RuntimePanelFactory.Text("State", row.transform, "",
            21, TextAnchor.MiddleCenter, RuntimePanelFactory.Reward);
        RuntimePanelFactory.Place(_itemState[index].rectTransform,
            new Vector2(0.60f, 0.5f), new Vector2(140f, 52f), Vector2.zero);
        _states[index] = _itemState[index].rectTransform;

        _buyButtons[index] = RuntimePanelFactory.Button("Buy", row.transform,
            "补充 +1 · " + definition.cost, new Vector2(0.77f, 0.5f),
            new Vector2(180f, 58f), RuntimePanelFactory.Action, 19);
        _buyButtons[index].onClick.AddListener(() => Purchase(id));
        _equipButtons[index] = RuntimePanelFactory.Button("Equip", row.transform,
            "装备", new Vector2(0.93f, 0.5f),
            new Vector2(120f, 58f), RuntimePanelFactory.Raised, 19);
        _equipButtons[index].onClick.AddListener(() => Equip(id));
    }

    private void Open()
    {
        Refresh();
        if (_router != null) _router.Show(MenuScreen.Supply);
        else _panel.SetActive(true);
        RuntimePanelFactory.RefreshText(_panel.transform);
    }

    private void Close()
    {
        if (_router != null) _router.BackToHome();
        else _panel.SetActive(false);
    }

    private void Purchase(PowerUpId id)
    {
        int countBefore = EchoRunSaveSystem.GetPowerUpCount(id);
        bool success = _gameManager != null
                       && _gameManager.TryPurchasePowerUp(id);
        if (success && countBefore <= 0)
            _gameManager.SelectPowerUp(id);
        _feedbackText.text = success
            ? (countBefore <= 0
                ? "已补充并装备；下一局开场消耗 1 个。"
                : "已补充 +1；当前装备保持不变。")
            : "金币不足；完成跑局后再来补给。";
        _feedbackText.color = success
            ? EchoRunUITheme.Success : EchoRunUITheme.Danger;
        Refresh();
    }

    private void Equip(PowerUpId id)
    {
        bool success = _gameManager != null
                       && _gameManager.SelectPowerUp(id);
        _feedbackText.text = success
            ? "已装备；下一局开场消耗 1 个。"
            : "库存为空；请先补充。";
        _feedbackText.color = success
            ? EchoRunUITheme.Success : EchoRunUITheme.Danger;
        Refresh();
    }

    private void Refresh()
    {
        if (_walletText == null) return;
        _walletText.text = "可用金币  " + EchoRunSaveSystem.TotalCoins;
        PowerUpId selected = EchoRunSaveSystem.GetSelectedPowerUp();
        for (int i = 0; i < 4; i++)
        {
            PowerUpId id = (PowerUpId)i;
            PowerUpBalance definition = GameBalanceConfig.GetPowerUp(id);
            int count = EchoRunSaveSystem.GetPowerUpCount(id);
            bool isSelected = selected == id && count > 0;
            _itemState[i].text = isSelected
                ? "库存 " + count + "  ·  下局生效"
                : "库存 " + count;

            int missing = Mathf.Max(0,
                definition.cost - EchoRunSaveSystem.TotalCoins);
            Button buy = _buyButtons[i];
            Text buyLabel = buy.GetComponentInChildren<Text>();
            buyLabel.text = missing > 0
                ? "还差 " + missing
                : "补充 +1 · " + definition.cost;
            buy.interactable = missing <= 0;

            Button equip = _equipButtons[i];
            Text equipLabel = equip.GetComponentInChildren<Text>();
            equipLabel.text = isSelected
                ? "✓ 已装备"
                : count > 0 ? "装备 ×" + count : "无库存";
            equip.interactable = count > 0 && !isSelected;
        }
    }

    private void OnWalletChanged(int value) => Refresh();

    private void OnStateChanged(GameState state)
    {
        bool menu = state == GameState.Menu;
        if (_router == null && _launcher != null) _launcher.SetActive(menu);
        if (!menu && _panel != null) _panel.SetActive(false);
        if (menu) Refresh();
    }

    void Update()
    {
        ApplyLayout(false);
    }

    private void ApplyLayout(bool force)
    {
        Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
        if (!force && screen == _lastScreenSize) return;
        _lastScreenSize = screen;
        if (_panelRect == null) return;

        bool portrait = UILayoutRules.IsCompactPortrait(Screen.width, Screen.height);
        Vector2 launcherSize = UILayoutRules.GetHomeNavigationSize(
            portrait, Application.isMobilePlatform || portrait);
        Vector2 closeSize = RuntimePanelFactory.TouchButtonSize(
            portrait ? new Vector2(260f, 104f) : new Vector2(190f, 58f),
            portrait);
        if (_launcher != null)
        {
            RuntimePanelFactory.Place(_launcher.GetComponent<RectTransform>(),
                UILayoutRules.GetHomeNavigationAnchor(1, portrait),
                launcherSize,
                Vector2.zero);
        }
        if (_closeButton != null)
        {
            RuntimePanelFactory.Place(_closeButton.GetComponent<RectTransform>(),
                portrait ? new Vector2(0.82f, 0.08f) : new Vector2(0.88f, 0.08f),
                closeSize,
                Vector2.zero);
        }
        if (_feedbackRect != null)
        {
            RuntimePanelFactory.Place(_feedbackRect,
                portrait ? new Vector2(0.08f, 0.145f) : new Vector2(0.08f, 0.08f),
                portrait ? new Vector2(760f, 64f) : new Vector2(650f, 50f),
                Vector2.zero);
        }
        _panelRect.sizeDelta = portrait
            ? new Vector2(900f, 1500f)
            : new Vector2(1120f, 720f);
        for (int i = 0; i < _rows.Length; i++)
        {
            if (_rows[i] == null) continue;
            float y = portrait ? 0.73f - i * 0.16f : 0.75f - i * 0.17f;
            RuntimePanelFactory.Place(_rows[i], new Vector2(0.5f, y),
                portrait ? new Vector2(800f, 190f) : new Vector2(1000f, 100f),
                Vector2.zero);
            RuntimePanelFactory.Place(_names[i],
                portrait ? new Vector2(0.20f, 0.72f) : new Vector2(0.13f, 0.63f),
                portrait ? new Vector2(360f, 54f) : new Vector2(260f, 44f),
                Vector2.zero);
            RuntimePanelFactory.Place(_descriptions[i],
                portrait ? new Vector2(0.20f, 0.43f) : new Vector2(0.13f, 0.25f),
                portrait ? new Vector2(200f, 48f) : new Vector2(390f, 38f),
                Vector2.zero);
            RuntimePanelFactory.Place(_states[i],
                portrait ? new Vector2(0.22f, 0.15f) : new Vector2(0.60f, 0.5f),
                portrait ? new Vector2(250f, 52f) : new Vector2(140f, 52f),
                Vector2.zero);
            Vector2 buySize = RuntimePanelFactory.TouchButtonSize(
                portrait ? new Vector2(240f, 104f) : new Vector2(180f, 58f),
                portrait);
            Vector2 equipSize = RuntimePanelFactory.TouchButtonSize(
                portrait ? new Vector2(160f, 104f) : new Vector2(120f, 58f),
                portrait);
            RuntimePanelFactory.Place(_buyButtons[i].GetComponent<RectTransform>(),
                portrait ? new Vector2(0.62f, 0.18f) : new Vector2(0.77f, 0.5f),
                buySize, Vector2.zero);
            RuntimePanelFactory.Place(_equipButtons[i].GetComponent<RectTransform>(),
                portrait ? new Vector2(0.87f, 0.18f) : new Vector2(0.93f, 0.5f),
                equipSize, Vector2.zero);
        }
    }

    void OnDestroy()
    {
        if (_gameManager == null) return;
        _gameManager.OnStateChanged.RemoveListener(OnStateChanged);
        _gameManager.OnBankedCoinsChanged.RemoveListener(OnWalletChanged);
    }
}
