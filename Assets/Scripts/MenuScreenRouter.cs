using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum MenuScreen
{
    Home,
    Settings,
    Runner,
    Supply,
    EchoReport
}

public sealed class MenuScreenRouter : MonoBehaviour
{
    private sealed class ScreenEntry
    {
        public GameObject panel;
        public Selectable firstSelected;
    }

    public static MenuScreenRouter Instance { get; private set; }

    public MenuScreen CurrentScreen { get; private set; } = MenuScreen.Home;
    public bool IsHome => CurrentScreen == MenuScreen.Home;
    public bool IsMenuVisible => _menuVisible;
    public event Action<MenuScreen> RouteChanged;

    private readonly Dictionary<MenuScreen, ScreenEntry> _screens =
        new Dictionary<MenuScreen, ScreenEntry>();
    private readonly List<GameObject> _homeNavigation = new List<GameObject>();
    private GameManager _gameManager;
    private bool _menuVisible;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    public void Initialize(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public void Register(MenuScreen screen, GameObject panel,
        Selectable firstSelected = null)
    {
        if (panel == null) return;
        if (screen == MenuScreen.Supply && !PowerUpController.IsAvailable)
        {
            panel.SetActive(false);
            return;
        }
        _screens[screen] = new ScreenEntry
        {
            panel = panel,
            firstSelected = firstSelected
        };
        ApplyVisibility(false);
    }

    public void RegisterHomeNavigation(GameObject navigation)
    {
        if (navigation == null || _homeNavigation.Contains(navigation)) return;
        _homeNavigation.Add(navigation);
        ApplyVisibility(false);
    }

    public bool Show(MenuScreen screen)
    {
        if (screen == MenuScreen.Supply && !PowerUpController.IsAvailable) return false;
        if (!_menuVisible || !IsInMenuState() || !_screens.ContainsKey(screen))
            return false;

        CurrentScreen = screen;
        ApplyVisibility(true);
        RouteChanged?.Invoke(CurrentScreen);
        return true;
    }

    public void BackToHome()
    {
        Show(MenuScreen.Home);
    }

    public void EnterMenu()
    {
        _menuVisible = true;
        CurrentScreen = MenuScreen.Home;
        ApplyVisibility(true);
        RouteChanged?.Invoke(CurrentScreen);
    }

    public void ExitMenu()
    {
        _menuVisible = false;
        ApplyVisibility(false);
        Select(null);
    }

    void Update()
    {
        if (!_menuVisible || !IsInMenuState() || IsHome) return;
        if (Input.GetKeyDown(KeyCode.Escape)) BackToHome();
    }

    private bool IsInMenuState()
    {
        return _gameManager == null || _gameManager.State == GameState.Menu;
    }

    private void ApplyVisibility(bool updateSelection)
    {
        foreach (KeyValuePair<MenuScreen, ScreenEntry> pair in _screens)
        {
            bool active = _menuVisible && pair.Key == CurrentScreen;
            if (pair.Value.panel != null
                && pair.Value.panel.activeSelf != active)
                pair.Value.panel.SetActive(active);
        }

        bool showHomeNavigation = _menuVisible && IsHome;
        for (int i = _homeNavigation.Count - 1; i >= 0; i--)
        {
            GameObject navigation = _homeNavigation[i];
            if (navigation == null)
            {
                _homeNavigation.RemoveAt(i);
                continue;
            }
            if (navigation.activeSelf != showHomeNavigation)
                navigation.SetActive(showHomeNavigation);
        }

        if (!_menuVisible || IsHome || !_screens.TryGetValue(
                CurrentScreen, out ScreenEntry activeEntry))
        {
            if (updateSelection && IsHome
                && _screens.TryGetValue(MenuScreen.Home, out ScreenEntry home))
                Select(home.firstSelected);
            return;
        }

        activeEntry.panel.transform.SetAsLastSibling();
        if (updateSelection) Select(activeEntry.firstSelected);
    }

    private static void Select(Selectable selectable)
    {
        if (EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(
            selectable != null ? selectable.gameObject : null);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
