using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class RuntimeSmokeTests
{
    [UnityTest]
    public IEnumerator CameraViewSettingChangesTheLiveRunAndPersists()
    {
        SavePreferenceSnapshot saved = CaptureSavePreferences();
        try
        {
            InstallIsolatedSave(new EchoRunSaveData());
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return WaitForFreshRun(null, false);
            GameManager game = GameManager.Instance;
            game.StartGame();
            for (int frame = 0; frame < 180
                 && game.State != GameState.Playing; frame++)
                yield return null;
            Assert.AreEqual(GameState.Playing, game.State);
            Camera camera = Camera.main;
            CameraFollow follow = camera.GetComponent<CameraFollow>();
            Assert.IsNotNull(follow);
            Assert.IsNotNull(follow.target);

            string captureRoot = System.IO.Path.GetFullPath(
                "TestResults/CameraView-20260924");
            System.IO.Directory.CreateDirectory(captureRoot);
            EchoRunSaveSystem.SaveCameraViewHeight(CameraViewHeight.Low);
            yield return new WaitForSecondsRealtime(2f);
            Assert.AreEqual(GameState.Playing, game.State);
            float lowY = camera.transform.position.y - follow.target.position.y;
            CaptureOffscreen(System.IO.Path.Combine(captureRoot,
                "low-view.png"));

            EchoRunSaveSystem.SaveCameraViewHeight(CameraViewHeight.High);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(GameState.Playing, game.State);
            float highY = camera.transform.position.y - follow.target.position.y;
            CaptureOffscreen(System.IO.Path.Combine(captureRoot,
                "high-view.png"));
            Assert.Greater(highY, lowY + 0.8f,
                "High view must visibly raise the live gameplay camera.");

            ResetSaveSystemCache();
            EchoRunSaveSystem.EnsureInitialized();
            Assert.AreEqual(CameraViewHeight.High, CameraViewSettings.Current,
                "Reloading the save archive must retain the camera choice.");
        }
        finally
        {
            if (GameManager.Instance != null) GameManager.Instance.ReturnToMenu();
            RestoreSavePreferences(saved);
        }
    }

    [UnityTest]
    public IEnumerator RunSettingsAndMusicRespectPauseFocusAndVolume()
    {
        SavePreferenceSnapshot saved = CaptureSavePreferences();
        AudioManager audio = null;
        try
        {
            InstallIsolatedSave(new EchoRunSaveData());
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;
            GameManager game = GameManager.Instance;
            UIManager ui = Object.FindObjectOfType<UIManager>();
            audio = AudioManager.Instance;
            audio.SendMessage("OnApplicationFocus", true);
            audio.SendMessage("OnApplicationPause", false);
            audio.SetMuted(false);
            audio.SetMasterVolume(1f);
            audio.SetMusicVolume(1f);
            audio.SetSfxVolume(1f);
            AudioSource music = audio.transform.Find("Music Bus").GetComponent<AudioSource>();
            Assert.IsNotNull(music.GetComponent<AudioLowPassFilter>());
            Assert.IsNull(audio.GetComponent<AudioLowPassFilter>(), "SFX must not be filtered with music.");
            game.StartGame();
            // Initial scene load may consume most of the first real-time wait;
            // observe the fade reaching an audible run level before comparing pause.
            float fadeDeadline = Time.realtimeSinceStartup + 8f;
            while (music.volume < 0.24f && Time.realtimeSinceStartup < fadeDeadline)
                yield return null;
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.GreaterOrEqual(music.volume, 0.24f);
            Assert.Greater(music.volume, 0f);
            Assert.LessOrEqual(music.volume, 0.55f);
            float runningLevel = music.volume;
            ScopeCapture("audio-settings-running");
            GameObject.Find("RunSettingsBtn").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(GameState.Paused, game.State);
            Assert.IsNotNull(GameObject.Find("SettingsPanel"));
            Assert.AreEqual(1f, GameObject.Find("BgmSlider").GetComponent<Slider>().value);
            float elapsed = game.RunElapsed;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(elapsed, game.RunElapsed, "Settings must freeze the active run.");
            Assert.Less(music.volume, runningLevel);
            ScopeCapture("audio-settings-overlay");
            Assert.IsTrue(ui.TryCloseRunSettings(), "Escape closes settings before resuming.");
            Assert.AreEqual(GameState.Paused, game.State);
            Assert.IsNotNull(GameObject.Find("PausePanel"));
            GameObject.Find("PauseSettingsBtn").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("SettingsBackBtn").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(GameState.Paused, game.State);
            ScopeCapture("audio-settings-pause");
            GameObject.Find("ResumeBtn").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.IsNull(GameObject.Find("SettingsPanel"));
            game.Pause();
            audio.SendMessage("OnApplicationFocus", false);
            Assert.AreEqual(0f, audio.EffectiveMasterVolume);
            Assert.AreEqual(0f, music.volume);
            audio.SendMessage("OnApplicationPause", true);
            audio.SendMessage("OnApplicationFocus", true);
            Assert.AreEqual(0f, audio.EffectiveMasterVolume, "Focus alone cannot undo OS suspension.");
            audio.SendMessage("OnApplicationPause", false);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.Greater(music.volume, 0f);
            Assert.AreEqual(GameState.Paused, game.State);
            audio.SetMusicVolume(0f);
            Assert.AreEqual(0f, music.volume);
            audio.SetSfxVolume(0f);
            foreach (AudioSource source in audio.GetComponents<AudioSource>())
                Assert.AreEqual(0f, source.volume, "Changing the SFX slider must mute active voices too.");
            audio.SetMuted(true);
            Assert.AreEqual(0f, audio.EffectiveMasterVolume);
            game.ReturnToMenu();
            yield return null;
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsOfType<AudioManager>().Length);
            Assert.IsNotNull(GameObject.Find("MenuPanel"));
            GameObject.Find("SettingsBtn").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("SettingsBackBtn").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(MenuScreenRouter.Instance.IsHome);
        }
        finally
        {
            if (audio != null)
            {
                audio.SendMessage("OnApplicationFocus", true);
                audio.SendMessage("OnApplicationPause", false);
            }
            if (GameManager.Instance != null) GameManager.Instance.ReturnToMenu();
            RestoreSavePreferences(saved);
            if (audio != null)
            {
                audio.masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
                audio.musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
                audio.sfxVolume = PlayerPrefs.GetFloat("SfxVolume", 1f);
                audio.muted = PlayerPrefs.GetInt("AudioMuted", 0) != 0;
                audio.SendMessage("ApplyOutputVolumes");
            }
        }
    }

    [UnityTest]
    public IEnumerator NoInputCalibrationSettlesNaturallyAndCanRestart()
    {
        SavePreferenceSnapshot saved = CaptureSavePreferences();
        try
        {
            InstallIsolatedSave(new EchoRunSaveData());
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;
            GameManager manager = GameManager.Instance;
            Assert.IsTrue(manager.TryConfigureGameplayFlow(GameplayFlowMode.SingleContract,
                new SingleContractValidationConfig { enabled = true, fixedSeed = 1337,
                    forceStandardDifficulty = true, disablePowerUps = true }));
            manager.StartGame();
            yield return null;
            Assert.IsFalse(AIShadowRunner.Instance.HasActiveOpponent);
            Assert.AreEqual(PowerUpId.None, PowerUpController.Instance.ActivePowerUp);
            manager.Pause();
            float elapsed = manager.RunElapsed;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.AreEqual(elapsed, manager.RunElapsed);
            manager.Resume();
            // Real frame updates and normal collisions: no teleport, invulnerability,
            // synthetic gate settlements, time acceleration or forced GameOver.
            float deadline = Time.realtimeSinceStartup + 85f;
            while (manager.State == GameState.Playing && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.GameOver, manager.State, "The natural run must settle.");
            yield return new WaitForSecondsRealtime(3f);
            Assert.IsTrue(manager.LastEndReason == RunEndReason.Collision
                          || manager.LastEndReason == RunEndReason.FinishReached);
            Assert.IsFalse(AIShadowRunner.Instance.LastSingleContractIdentityPromoted,
                "Idle evidence must not invent a formed echo.");
            Debug.Log("SCOPE_NATURAL_IDLE_RUN reason=" + manager.LastEndReason
                + " elapsed=" + manager.RunElapsed + " distance=" + manager.Distance);
            ScopeCapture("natural-idle-result");
            manager.ReturnToMenu();
            yield return null;
            yield return null;
            manager = GameManager.Instance;
            Assert.AreEqual(GameState.Menu, manager.State);
            manager.StartGame();
            yield return null;
            Assert.AreEqual(GameState.Playing, manager.State);
            Assert.IsFalse(AIShadowRunner.Instance.HasActiveOpponent);
            Assert.AreEqual(RunEndReason.None, manager.LastEndReason);
            Assert.IsFalse(MenuScreenRouter.Instance.Show(MenuScreen.Supply));
        }
        finally
        {
            if (GameManager.Instance != null) GameManager.Instance.ReturnToMenu();
            RestoreSavePreferences(saved);
        }
    }

    [UnityTest]
    public IEnumerator ScopeClosureMenusPreviewAndGameplayCapture()
    {
        SavePreferenceSnapshot saved = CaptureSavePreferences();
        var layers = new Dictionary<GameObject, int>();
        try
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;
            GameManager manager = GameManager.Instance;
            if (manager.State != GameState.Menu) manager.ReturnToMenu();
            yield return null;
            ScopeCapture("home");
            RawImage sceneVeil = GameObject.Find("MenuSceneVeil").GetComponent<RawImage>();
            Assert.IsNull(sceneVeil.texture, "Home must retain the live game scene behind its tint.");
            Assert.Less(sceneVeil.color.a, 1f, "The home tint must not obscure the live scene.");
            AssertVisibleTextGeometry("Title");
            Assert.IsTrue(GameObject.Find("StartBtn").GetComponent<Button>().interactable);
            var model = GameObject.Find("player").transform.Find("CharacterModel");
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
                layers[child.gameObject] = child.gameObject.layer;
            GameObject.Find("CharacterBtn").GetComponent<Button>().onClick.Invoke();
            for (int frame = 0; frame < 5; frame++) yield return null;
            var preview = Object.FindObjectOfType<RunnerColorPreview>();
            Assert.IsNotNull(preview);
            Assert.IsNotNull(preview.GetComponent<RawImage>().texture);
            AssertVisibleTextGeometry("CharTitle");
            ScopeCapture("runner");
            GameObject.Find("PresetBtn_4").GetComponent<Button>().onClick.Invoke();
            yield return null;
            ScopeCapture("runner-grape");
            MenuScreenRouter.Instance.BackToHome();
            yield return null;
            foreach (var entry in layers) Assert.AreEqual(entry.Value, entry.Key.layer);
            GameObject.Find("SettingsBtn").GetComponent<Button>().onClick.Invoke();
            for (int frame = 0; frame < 5; frame++) yield return null;
            foreach (string heading in new[] { "SettingsTitle", "FpsLabel", "DifficultyLabel", "AccessibilityLabel" })
                AssertVisibleTextGeometry(heading);
            ScopeCapture("settings");
            MenuScreenRouter.Instance.BackToHome();
            Assert.IsTrue(manager.TryConfigureGameplayFlow(GameplayFlowMode.SingleContract,
                new SingleContractValidationConfig { enabled = true, useFixedIdentity = true,
                    fixedSeed = 1337, freezeDirector = true, disablePowerUps = true,
                    forceStandardDifficulty = true }));
            manager.StartGame();
            yield return new WaitForSecondsRealtime(3f);
            ScopeCapture("challenge");
            Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
            var enabledCanvases = new List<Canvas>();
            foreach (Canvas canvas in canvases)
                if (canvas.enabled) { enabledCanvases.Add(canvas); canvas.enabled = false; }
            try { ScopeCaptureCityBackground(); }
            finally { foreach (Canvas canvas in enabledCanvases) if (canvas != null) canvas.enabled = true; }
            foreach (SwipeDirection direction in new[] { SwipeDirection.Left, SwipeDirection.Right,
                         SwipeDirection.Up, SwipeDirection.Down })
            {
                InputManager.Instance.QueueSwipe(direction, InputIntentSource.Keyboard, Time.unscaledTime);
                yield return new WaitForSecondsRealtime(.25f);
                ScopeCapture("action-" + direction);
                yield return new WaitForSecondsRealtime(.6f);
            }
            manager.Pause();
            yield return null;
            ScopeCapture("pause");
            manager.Resume();
            manager.GameOver();
            yield return new WaitForSecondsRealtime(3f);
            ScopeCapture("result");
            manager.ReturnToMenu();
            yield return null;
            Assert.AreEqual(GameState.Menu, GameManager.Instance.State);
            Assert.IsFalse(MenuScreenRouter.Instance.Show(MenuScreen.Supply));
        }
        finally
        {
            if (MenuScreenRouter.Instance != null) MenuScreenRouter.Instance.BackToHome();
            RestoreSavePreferences(saved);
        }
    }

    private static void ScopeCapture(string name)
    {
        string root = System.IO.Path.GetFullPath("TestResults/VisualSystem-20260923/RuntimeCaptures");
        System.IO.Directory.CreateDirectory(root);
        CaptureOffscreen(System.IO.Path.Combine(root, name + ".png"));
    }

    private static void CaptureOffscreen(string path)
    {
        typeof(EchoVisualCaptureProbe).GetMethod("CaptureOffscreen",
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
            new object[] { path });
    }

    private static void AssertVisibleTextGeometry(string name)
    {
        Text label = GameObject.Find(name).GetComponent<Text>();
        Canvas.ForceUpdateCanvases();
        Assert.Greater(label.cachedTextGenerator.vertexCount, 4,
            name + " has text but no visible glyph geometry; check line height and clipping.");
    }

    private static void ScopeCaptureCityBackground()
    {
        Camera camera = Camera.main;
        RenderTexture previous = camera.targetTexture;
        RenderTexture active = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 24);
        Texture2D pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            pixels.Apply();
            System.IO.File.WriteAllBytes("TestResults/VisualSystem-20260923/RuntimeCaptures/city-background.png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(pixels);
        }
    }

    [UnityTest]
    public IEnumerator BundledAudioAndBalanceLoadInPlayer()
    {
        yield return null;

        Assert.IsNotNull(Resources.Load<AudioClip>("Audio/bgm_transit"));
        Assert.IsNotNull(Resources.Load<AudioClip>("Audio/footstep_01"));
        Assert.IsNotNull(Resources.Load<AudioClip>("Audio/collision"));
        Assert.IsNotNull(Resources.Load<AudioClip>("Audio/coin"));
        Assert.IsNotNull(Resources.Load<AudioClip>("Audio/ui_click"));
        Material shaderRetention = Resources.Load<Material>(
            "Materials/EchoKitVertexColor");
        Assert.IsNotNull(shaderRetention,
            "The runtime vertex-color shader must be retained by a bundled material.");
        Assert.AreEqual("EchoRun/VertexColor", shaderRetention.shader.name);
        Assert.AreEqual(4, GameBalanceConfig.Current.powerUps.Length);
    }

    [UnityTest]
    public IEnumerator RuntimeManagersBootstrapWithoutExceptions()
    {
        for (int frame = 0; frame < 120 && GameManager.Instance == null; frame++)
            yield return null;

        Assert.IsNotNull(GameManager.Instance);
        Assert.IsNotNull(TrackManager.Instance);
        Assert.IsNotNull(PowerUpController.Instance);
        Assert.IsNotNull(AudioManager.Instance);
    }

    [UnityTest]
    public IEnumerator StyledGameplayPropsKeepTheirGameplayColliders()
    {
        for (int frame = 0; frame < 120 && WorldStyler.Instance == null; frame++)
            yield return null;

        Assert.IsNotNull(WorldStyler.Instance);
        GameObject coin = null;
        var obstacles = new GameObject[3];

        try
        {
            coin = new GameObject("VisualTestCoin");
            BoxCollider coinCollider = coin.AddComponent<BoxCollider>();
            coinCollider.isTrigger = true;
            WorldStyler.Instance.StyleCoin(coin);

            Transform coinVisual = coin.transform.Find("StreamlinedVisual");
            Assert.IsNotNull(coinVisual);
            Assert.IsNotNull(coinVisual.GetComponent<EchoCoinVisual>());
            Assert.AreEqual(1,
                coinVisual.GetComponentsInChildren<Renderer>(true).Length,
                "Coin trails must use one combined renderer.");
            MeshFilter coinMesh = coinVisual.GetComponent<MeshFilter>();
            Assert.IsNotNull(coinMesh);
            Assert.IsNotNull(coinMesh.sharedMesh);
            Assert.AreSame(coinCollider, coin.GetComponent<BoxCollider>());
            Assert.IsTrue(coinCollider.isTrigger);

            string[] signatureParts =
            {
                "SlideDroneBody",
                "JumpBlockBody",
                "LaneBulkheadBody"
            };
            Vector3[] colliderSizes =
            {
                new Vector3(3.1f, 0.82f, 1.2f),
                new Vector3(3.2f, 0.9f, 0.7f),
                new Vector3(3.4f, 2.7f, 0.9f)
            };
            Vector3[] colliderCenters =
            {
                new Vector3(0f, 0.95f, 0f),
                new Vector3(0f, -0.45f, 0f),
                new Vector3(0f, 0.25f, 0f)
            };
            for (int i = 0; i < obstacles.Length; i++)
            {
                GameObject obstacle = new GameObject("VisualTestObstacle_" + i);
                obstacles[i] = obstacle;
                BoxCollider gameplayCollider = obstacle.AddComponent<BoxCollider>();
                gameplayCollider.isTrigger = true;
                gameplayCollider.size = colliderSizes[i];
                gameplayCollider.center = colliderCenters[i];
                Obstacle data = obstacle.AddComponent<Obstacle>();
                data.type = (ObstacleType)i;

                WorldStyler.Instance.StyleObstacle(obstacle);

                Transform visual = obstacle.transform.Find("StreamlinedVisual");
                Assert.IsNotNull(visual);
                Assert.IsNotNull(visual.Find(signatureParts[i]));
                Assert.AreSame(gameplayCollider,
                    obstacle.GetComponent<BoxCollider>());
                Assert.IsTrue(gameplayCollider.isTrigger);

                Bounds visualBounds = CombinedRendererBounds(visual);
                Assert.GreaterOrEqual(visualBounds.size.x,
                    gameplayCollider.bounds.size.x * 0.9f,
                    data.type + " visual must visibly block its collider width.");
                Assert.LessOrEqual(visualBounds.size.x,
                    gameplayCollider.bounds.size.x * 1.05f,
                    data.type + " visual must not extend beyond its collider.");
                if (data.type == ObstacleType.Low)
                {
                    Assert.IsNull(visual.Find("SlideShutterBody"),
                        "The old slide shutter must not return.");
                    Assert.LessOrEqual(visualBounds.size.x, 3.15f,
                        "The slide drone must remain lane-sized.");
                    Assert.AreEqual(gameplayCollider.bounds.min.y,
                        visualBounds.min.y, 0.06f,
                        "The slide drone must visibly match its collider bottom.");
                }
                AssertNoPointLikeObstacleParts(visual);
            }

            yield return null;
        }
        finally
        {
            if (coin != null) Object.Destroy(coin);
            foreach (GameObject obstacle in obstacles)
            {
                if (obstacle != null) Object.Destroy(obstacle);
            }
        }
    }

    [UnityTest]
    public IEnumerator CoinPoolRepairsBindingAndKeepsRouteAlignedTrigger()
    {
        for (int frame = 0; frame < 120
             && (TrackManager.Instance == null || WorldStyler.Instance == null);
             frame++)
            yield return null;

        TrackManager track = TrackManager.Instance;
        Assert.IsNotNull(track);
        Assert.IsNotNull(WorldStyler.Instance);

        MethodInfo spawnCoin = typeof(TrackManager).GetMethod(
            "SpawnCoinInstance", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(spawnCoin);

        GameObject originalPrefab = track.coinPrefab;
        GameObject legacyPrefab = new GameObject("LegacyCoinPrefab_Test");
        BoxCollider legacyTrigger = legacyPrefab.AddComponent<BoxCollider>();
        legacyTrigger.size = new Vector3(1f, 1f, 0.4f);
        legacyTrigger.isTrigger = true;
        legacyPrefab.SetActive(false);
        Assert.IsNull(legacyPrefab.GetComponent<Coin>(),
            "The test template must reproduce the missing Coin binding.");
        track.coinPrefab = legacyPrefab;

        GameObject owner = new GameObject("CoinPoolRuntimeOwner");
        owner.transform.SetPositionAndRotation(
            new Vector3(1000f, 0f, 1000f),
            Quaternion.Euler(0f, 90f, 0f));
        GameObject spawned = null;
        try
        {
            Quaternion firstRoute = TrackSpawnRules.CoinRouteRotation(
                owner.transform.rotation, Vector3.forward);
            spawned = spawnCoin.Invoke(track, new object[]
            {
                owner,
                owner.transform.position + Vector3.up,
                firstRoute,
                true,
                17
            }) as GameObject;
            Assert.IsNotNull(spawned);
            yield return null;

            Assert.IsTrue(spawned.activeInHierarchy);
            Assert.AreSame(owner.transform, spawned.transform.parent);
            Assert.AreEqual(1, spawned.GetComponents<Coin>().Length,
                "A pooled pickup must never accumulate Coin components.");
            BoxCollider trigger = spawned.GetComponent<BoxCollider>();
            Assert.IsNotNull(trigger);
            Assert.IsTrue(trigger.isTrigger);
            Assert.AreEqual(new Vector3(1f, 1f, 0.4f), trigger.size);
            Assert.Less(Vector3.Angle(spawned.transform.forward,
                    firstRoute * Vector3.forward), 0.01f,
                "The root trigger must follow the route, not the camera.");

            EchoCoinVisual visual =
                spawned.GetComponentInChildren<EchoCoinVisual>(true);
            Assert.IsNotNull(visual,
                "World styling must attach the single rendered coin visual.");
            Renderer renderer = visual.GetComponent<Renderer>();
            Assert.IsNotNull(renderer);
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Assert.AreEqual(1f,
                properties.GetFloat(Shader.PropertyToID("_ContractMarker")),
                0.001f);

            GameObject firstSpawn = spawned;
            track.ReleaseDynamic(firstSpawn);
            Assert.IsFalse(firstSpawn.activeSelf);

            Quaternion secondRoute = TrackSpawnRules.CoinRouteRotation(
                Quaternion.Euler(0f, -90f, 0f), Vector3.forward);
            GameObject reused = spawnCoin.Invoke(track, new object[]
            {
                owner,
                owner.transform.position + Vector3.up * 2f,
                secondRoute,
                false,
                0
            }) as GameObject;
            spawned = reused;
            Assert.AreSame(firstSpawn, reused,
                "The test must exercise the actual pooled reuse path.");
            yield return null;

            Assert.AreEqual(1, reused.GetComponents<Coin>().Length);
            Assert.Less(Vector3.Angle(reused.transform.forward,
                    secondRoute * Vector3.forward), 0.01f);
            renderer.GetPropertyBlock(properties);
            Assert.AreEqual(0f,
                properties.GetFloat(Shader.PropertyToID("_ContractMarker")),
                0.001f,
                "Reused normal coins must clear the contract marker.");
        }
        finally
        {
            if (spawned != null && spawned.activeSelf)
                track.ReleaseDynamic(spawned);
            track.coinPrefab = originalPrefab;
            Dictionary<GameObject, Queue<GameObject>> pools =
                GetPrivateField<Dictionary<GameObject, Queue<GameObject>>>(
                    track, "_dynamicPools");
            if (pools.TryGetValue(legacyPrefab,
                    out Queue<GameObject> testPool))
            {
                while (testPool.Count > 0)
                    Object.Destroy(testPool.Dequeue());
                pools.Remove(legacyPrefab);
            }
            Object.Destroy(legacyPrefab);
            Object.Destroy(owner);
        }
    }

    private static Bounds CombinedRendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        Assert.IsNotEmpty(renderers);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void AssertNoPointLikeObstacleParts(Transform visual)
    {
        string[] forbiddenNames = { "Node", "Joint", "Hub", "Eye", "Dot" };
        Transform[] parts = visual.GetComponentsInChildren<Transform>();
        foreach (Transform part in parts)
        {
            foreach (string forbiddenName in forbiddenNames)
            {
                Assert.IsFalse(part.name.Contains(forbiddenName),
                    "Point-like obstacle decoration remains: " + part.name);
            }
        }
    }

    [UnityTest]
    public IEnumerator RestartedRunRecreatesTrackBeforeAutoStart()
    {
        GameManager bootstrapManager = GameManager.Instance;
        SceneManager.LoadScene(0);
        for (int frame = 0; frame < 120
             && (GameManager.Instance == null
                 || object.ReferenceEquals(GameManager.Instance, bootstrapManager));
             frame++)
            yield return null;

        Assert.IsNotNull(GameManager.Instance);
        GameManager firstRunManager = GameManager.Instance;

        firstRunManager.Restart();
        for (int frame = 0; frame < 120
             && (GameManager.Instance == null
                 || object.ReferenceEquals(GameManager.Instance, firstRunManager)
                 || GameManager.Instance.State != GameState.Playing);
             frame++)
            yield return null;

        Assert.IsNotNull(GameManager.Instance);
        Assert.AreEqual(GameState.Playing, GameManager.Instance.State);
        Assert.IsNotNull(TrackManager.Instance);

        yield return null;
        Assert.Greater(TrackManager.Instance.ActiveSegmentCount, 0,
            "The restarted run must generate track segments.");

        GameManager restartedManager = GameManager.Instance;
        restartedManager.ReturnToMenu();
        for (int frame = 0; frame < 120
             && (GameManager.Instance == null
                 || object.ReferenceEquals(GameManager.Instance, restartedManager)
                 || GameManager.Instance.State != GameState.Menu);
             frame++)
            yield return null;
        Assert.IsNotNull(GameManager.Instance);
        Assert.AreEqual(GameState.Menu, GameManager.Instance.State);
    }

    [UnityTest]
    public IEnumerator MenuProgressionSystemsBootstrapOnTheRuntimeCanvas()
    {
        yield return null;
        yield return null;

        Assert.IsNotNull(Object.FindObjectOfType<Canvas>());
        Assert.IsNull(Object.FindObjectOfType<PowerUpShopUI>());
        Assert.IsNotNull(Object.FindObjectOfType<AITrainingDashboardUI>());
    }

    [UnityTest]
    public IEnumerator HomePrimaryActionIsInsideSafeAreaAndReceivesPointerClick()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        UIManager ui = null;
        for (int frame = 0; frame < 180
             && (GameManager.Instance == null || ui == null); frame++)
        {
            ui = Object.FindObjectOfType<UIManager>();
            yield return null;
        }

        Assert.IsNotNull(ui);
        Assert.AreEqual(GameState.Menu, GameManager.Instance.State);
        Button start = GetPrivateField<Button>(ui, "_startBtn");
        RectTransform safeArea = GetPrivateField<RectTransform>(
            ui, "_safeAreaRoot");
        AssertButtonVisibleAndRaycastable(start, safeArea);

        ExecuteEvents.Execute(start.gameObject,
            new PointerEventData(EventSystem.current),
            ExecuteEvents.pointerClickHandler);
        yield return null;
        Assert.AreEqual(GameState.Playing, GameManager.Instance.State,
            "The visible primary action must enter gameplay on pointer click.");

        GameManager activeManager = GameManager.Instance;
        activeManager.ReturnToMenu();
        for (int frame = 0; frame < 180
             && (GameManager.Instance == null
                 || ReferenceEquals(GameManager.Instance, activeManager)
                 || GameManager.Instance.State != GameState.Menu); frame++)
            yield return null;
        Assert.AreEqual(GameState.Menu, GameManager.Instance.State);
    }

    [UnityTest]
    public IEnumerator SettingsSoundSlidersKeepVisibleMeaningfulReadouts()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        UIManager ui = null;
        for (int frame = 0; frame < 180
             && (GameManager.Instance == null || ui == null); frame++)
        {
            ui = Object.FindObjectOfType<UIManager>();
            yield return null;
        }

        Assert.IsNotNull(ui);
        Button settings = GetPrivateField<Button>(ui, "_settingsBtn");
        ExecuteEvents.Execute(settings.gameObject,
            new PointerEventData(EventSystem.current),
            ExecuteEvents.pointerClickHandler);
        for (int frame = 0; frame < 5; frame++) yield return null;
        Canvas.ForceUpdateCanvases();

        Text master = GetPrivateField<Text>(ui, "_masterValueText");
        Text music = GetPrivateField<Text>(ui, "_bgmValueText");
        Text effects = GetPrivateField<Text>(ui, "_sfxValueText");
        AssertSoundReadout(master, "主音量");
        AssertSoundReadout(music, "音乐音量");
        AssertSoundReadout(effects, "音效音量");
        foreach (string field in new[] { "_masterSlider", "_bgmSlider", "_sfxSlider" })
        {
            Slider slider = GetPrivateField<Slider>(ui, field);
            Assert.LessOrEqual(slider.fillRect.rect.height, 10f,
                "Visual track must not stretch with the touch hit area.");
            Assert.LessOrEqual(slider.handleRect.rect.height, 32f,
                "The handle must remain compact inside the touch hit area.");
            Assert.GreaterOrEqual(((RectTransform)slider.transform).rect.height, 64f);
        }
        Button highFrameRate = GetPrivateField<Button>(ui, "_fps120Btn");
        Assert.IsTrue(highFrameRate.gameObject.activeInHierarchy);
        int previousRate = GameManager.Instance.GetFrameRate();
        try
        {
            highFrameRate.onClick.Invoke();
            Assert.AreEqual(120, GameManager.Instance.GetFrameRate());
        }
        finally { GameManager.Instance.SetFrameRate(previousRate); }
        Button highCamera = GetPrivateField<Button>(ui, "_cameraHighBtn");
        Button lowCamera = GetPrivateField<Button>(ui, "_cameraLowBtn");
        highCamera.onClick.Invoke();
        Assert.AreEqual(CameraViewHeight.High, CameraViewSettings.Current);
        Assert.AreEqual((int)CameraViewHeight.High,
            PlayerPrefs.GetInt(CameraViewSettings.PreferenceKey));
        Assert.IsTrue(highCamera.GetComponentInChildren<Text>().text.Contains("✓"));
        lowCamera.onClick.Invoke();
        Assert.AreEqual(CameraViewHeight.Low, CameraViewSettings.Current);
        Button back = GetPrivateField<Button>(ui, "_settingsBackBtn");
        GameObject settingsPanel = GetPrivateField<GameObject>(
            ui, "_settingsPanel");
        RectTransform safeArea = GetPrivateField<RectTransform>(
            ui, "_safeAreaRoot");
        RectTransform backRect = back.GetComponent<RectTransform>();
        Assert.AreSame(settingsPanel.transform, back.transform.parent,
            "Settings back must stay outside the scroll content.");
        Assert.AreEqual(new Vector2(0f, 1f), backRect.anchorMin);
        Assert.AreEqual(new Vector2(0f, 1f), backRect.anchorMax);
        Assert.AreEqual(new Vector2(0f, 1f), backRect.pivot);
        Assert.AreEqual(new Vector2(24f, -24f), backRect.anchoredPosition);
        AssertButtonVisibleAndRaycastable(back, safeArea);
    }

    [UnityTest]
    public IEnumerator RepeatedRestartsRestoreRuntimeArtForEveryRun()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return WaitForFreshRun(null, false);

        for (int run = 0; run < 4; run++)
        {
            AssertRuntimeArtIsReady(run + 1);

            GameManager previous = GameManager.Instance;
            previous.Restart();
            yield return WaitForFreshRun(previous, true);
            yield return null;
        }

        AssertRuntimeArtIsReady(5);
    }

    private static IEnumerator WaitForFreshRun(GameManager previous,
        bool requirePlaying)
    {
        for (int frame = 0; frame < 240; frame++)
        {
            bool managerReady = GameManager.Instance != null
                                && !ReferenceEquals(GameManager.Instance, previous);
            bool stateReady = !requirePlaying
                              || GameManager.Instance.State == GameState.Playing;
            bool artReady = WorldStyler.Instance != null
                            && TrackManager.Instance != null;
            if (managerReady && stateReady && artReady) yield break;
            yield return null;
        }

        Assert.Fail("The reloaded run did not finish bootstrapping in time.");
    }

    private static void AssertRuntimeArtIsReady(int runNumber)
    {
        string context = "Run " + runNumber + ": ";
        Assert.IsNotNull(WorldStyler.Instance,
            context + "WorldStyler is missing.");
        Assert.IsNotNull(RenderSettings.skybox,
            context + "the runtime skybox is missing.");
        Assert.IsNotNull(RenderSettings.skybox.shader,
            context + "the runtime skybox shader is missing.");
        Assert.AreEqual(WorldStyler.SeamlessSkyShaderName,
            RenderSettings.skybox.shader.name,
            context + "the authored skybox was not restored.");
        Material citySky = Resources.Load<Material>("CityV7/ExperienceSky");
        Assert.IsNotNull(citySky, context + "the current city sky asset is missing.");
        Assert.AreSame(citySky.GetTexture("_MainTex"),
            RenderSettings.skybox.GetTexture("_MainTex"),
            context + "the current city panoramic sky texture was not restored.");

        Camera camera = Camera.main;
        Assert.IsNotNull(camera, context + "the main camera is missing.");
        Assert.AreEqual(CameraClearFlags.Skybox, camera.clearFlags,
            context + "the camera is not rendering the skybox.");

        Light fill = GameObject.Find("EchoFillLight")?.GetComponent<Light>();
        Assert.IsNotNull(fill, context + "the authored fill light is missing.");

        if (GameManager.Instance == null
            || GameManager.Instance.State != GameState.Playing) return;

        TrackSegmentData[] segments = Object.FindObjectsOfType<TrackSegmentData>();
        Assert.IsNotEmpty(segments,
            context + "no active track segment was generated.");
        foreach (TrackSegmentData segment in segments)
        {
            Transform environment = segment.transform.Find(
                segment.segmentType == TrackSegmentType.Straight
                    ? "CityV7Environment" : "EchoEnvironment");
            Assert.IsNotNull(environment,
                context + segment.name + " has no authored environment.");
            Assert.IsTrue(environment.gameObject.activeInHierarchy,
                context + segment.name + " environment is inactive.");
        }

        foreach (Obstacle obstacle in Object.FindObjectsOfType<Obstacle>())
        {
            Assert.IsNotNull(obstacle.transform.Find("StreamlinedVisual"),
                context + obstacle.name + " kept its primitive obstacle art.");
        }

        foreach (Coin coin in Object.FindObjectsOfType<Coin>())
        {
            Assert.IsNotNull(coin.transform.Find("StreamlinedVisual"),
                context + coin.name + " kept its primitive coin art.");
        }
    }

    [UnityTest]
    public IEnumerator ResultTextAndActionsStayInsideSafeAreaAndRaycast()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        UIManager ui = null;
        for (int frame = 0; frame < 180
             && (GameManager.Instance == null || ui == null); frame++)
        {
            ui = Object.FindObjectOfType<UIManager>();
            yield return null;
        }

        Assert.IsNotNull(ui);
        FieldInfo stateField = typeof(GameManager).GetField(
            "<State>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(stateField);
        stateField.SetValue(GameManager.Instance, GameState.GameOver);
        GameManager.Instance.OnStateChanged.Invoke(GameState.GameOver);
        yield return null;
        yield return null;

        RectTransform safeArea = GetPrivateField<RectTransform>(
            ui, "_safeAreaRoot");
        Text result = GetPrivateField<Text>(ui, "_shadowResultText");
        Assert.IsTrue(result.gameObject.activeInHierarchy);
        AssertRectContained(result.rectTransform, safeArea);
        AssertButtonVisibleAndRaycastable(
            GetPrivateField<Button>(ui, "_restartBtn"), safeArea);
        AssertButtonVisibleAndRaycastable(
            GetPrivateField<Button>(ui, "_goToMenuBtn"), safeArea);
    }

    [UnityTest]
    public IEnumerator FinishMarkerAppearsAheadWithoutBlockingTheRunner()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        for (int frame = 0; frame < 180
             && (GameManager.Instance == null || TrackManager.Instance == null);
             frame++)
            yield return null;

        GameManager gameManager = GameManager.Instance;
        TrackManager track = TrackManager.Instance;
        Assert.IsNotNull(gameManager);
        Assert.IsNotNull(track);
        gameManager.StartGame();
        yield return null;
        yield return null;

        float nearFinish = gameManager.CourseDistance - 10f;
        typeof(GameManager).GetField("<Distance>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
            gameManager, nearFinish);
        typeof(GameManager).GetField("_distanceTraveled",
            BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
            gameManager, nearFinish);
        MethodInfo updateMarker = typeof(TrackManager).GetMethod(
            "UpdateFinishMarker",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(updateMarker);
        updateMarker.Invoke(track, null);

        Transform marker = track.transform.Find("FinishMarker");
        Assert.IsNotNull(marker);
        Assert.IsTrue(marker.gameObject.activeInHierarchy);
        GameObject authoredGate = Resources.Load<GameObject>(FinishGatePresentation.ResourcePath);
        Assert.IsNotNull(authoredGate);
        MeshFilter[] authoredFilters = authoredGate.GetComponentsInChildren<MeshFilter>(true);
        MeshFilter[] runtimeFilters = marker.GetComponentsInChildren<MeshFilter>(true);
        Assert.IsNotEmpty(authoredFilters, "The finish prefab needs real authored geometry.");
        Assert.AreEqual(authoredFilters.Length, runtimeFilters.Length);
        var authoredMeshes = new HashSet<Mesh>();
        foreach (MeshFilter filter in authoredFilters) authoredMeshes.Add(filter.sharedMesh);
        foreach (MeshFilter filter in runtimeFilters)
        {
            Assert.IsNotNull(filter.sharedMesh);
            Assert.IsTrue(authoredMeshes.Contains(filter.sharedMesh),
                "The visible finish must retain its formal city asset instead of primitive substitutes.");
        }
        FinishGatePresentation presentation = marker.GetComponent<FinishGatePresentation>();
        Assert.IsNotNull(presentation);
        Assert.IsNotEmpty(presentation.signalRenderers);
        Assert.Less(presentation.signalRenderers.Length,
            marker.GetComponentsInChildren<Renderer>(true).Length,
            "Signals must be separate from the structural and lettering surfaces.");
        foreach (Renderer signal in presentation.signalRenderers)
        {
            Assert.IsNotNull(signal);
            Assert.IsTrue(signal.transform.IsChildOf(marker));
        }
        Assert.IsNull(marker.Find("ProtocolCore"));
        Light[] finishLights = marker.GetComponentsInChildren<Light>(true);
        Assert.AreEqual(2, finishLights.Length);
        CollectionAssert.AreEquivalent(finishLights, presentation.arrivalLights);
        foreach (Light finishLight in finishLights)
        {
            Assert.AreEqual(LightType.Point, finishLight.type);
            Assert.Greater(finishLight.range, 0f);
            Assert.LessOrEqual(finishLight.range, 5.5f);
            Assert.AreEqual(LightShadows.None, finishLight.shadows);
        }
        foreach (Collider collider in marker.GetComponentsInChildren<Collider>(true))
            Assert.IsFalse(collider.enabled,
                "The visual finish marker must never collide with the runner.");
        Assert.IsEmpty(marker.GetComponentsInChildren<Rigidbody>(true));
        PlayerController player = Object.FindObjectOfType<PlayerController>();
        Assert.IsNotNull(player);
        Vector3 toMarker = marker.position - player.transform.position;
        Assert.Greater(Vector3.Dot(toMarker, player.ForwardDirection), 1f,
            "The finish marker must be visibly ahead of the runner.");

        // Exercise the real MonoBehaviour lifecycle in Play Mode. Edit Mode cannot
        // prove this because the presentation intentionally does not run in the editor.
        presentation.SetApproach(1f, true);
        foreach (Light finishLight in finishLights) Assert.IsTrue(finishLight.enabled);
        marker.gameObject.SetActive(false);
        foreach (Light finishLight in finishLights)
            Assert.IsFalse(finishLight.enabled,
                "Deactivating the finish gate must release its arrival lights.");
        marker.gameObject.SetActive(true);
        updateMarker.Invoke(track, null);
        Assert.IsTrue(marker.gameObject.activeInHierarchy);

        // Crossing the visibility boundary must hide the same instance and its lights;
        // advancing toward the finish must reuse it, without affecting course settlement.
        foreach (float remaining in new[] { 100f, 10f, 0f })
        {
            float distance = gameManager.CourseDistance - remaining;
            typeof(GameManager).GetField("<Distance>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(gameManager, distance);
            typeof(GameManager).GetField("_distanceTraveled",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(gameManager, distance);
            updateMarker.Invoke(track, null);
            Assert.AreSame(marker, track.transform.Find("FinishMarker"));
            Assert.AreEqual(remaining == 10f, marker.gameObject.activeSelf);
            if (!marker.gameObject.activeSelf)
                foreach (Light finishLight in finishLights) Assert.IsFalse(finishLight.enabled);
        }
    }

    [UnityTest]
    public IEnumerator SingleContractMenuUsesOnlyTheSavedIdentityReportAction()
    {
        SceneManager.LoadScene("Assets/Scenes/SampleScene.scene");
        yield return null;
        yield return null;

        UIManager ui = Object.FindObjectOfType<UIManager>();
        AITrainingDashboardUI training =
            Object.FindObjectOfType<AITrainingDashboardUI>();
        Assert.IsNotNull(ui);
        Assert.IsNotNull(training);
        Assert.IsTrue(GameManager.Instance.UsesSingleContractRules);
        ui.SendMessage("RefreshMenuPresentation");
        GameObject launcher = GetPrivateObject(training, "_launcher");
        Assert.IsNotNull(launcher);
        Assert.IsFalse(launcher.activeInHierarchy);
        Button report = GetPrivateField<Button>(ui, "_reportBtn");
        bool hasSavedReport = !string.IsNullOrEmpty(UIManager.BuildSavedEchoReport(
            EchoRunSaveSystem.GetActiveEchoIdentity()));
        Assert.AreEqual(hasSavedReport, report.gameObject.activeInHierarchy);

        MenuScreenRouter.Instance.Show(MenuScreen.Settings);
        MenuScreenRouter.Instance.BackToHome();
        Assert.IsFalse(launcher.activeInHierarchy,
            "Router home navigation must never reactivate the retired SingleContract launcher.");
        if (hasSavedReport)
        {
            report.onClick.Invoke();
            Assert.AreEqual(MenuScreen.EchoReport, MenuScreenRouter.Instance.CurrentScreen);
            Assert.IsTrue(GetPrivateObject(ui, "_reportPanel").activeInHierarchy);
            Assert.IsFalse(GetPrivatePanel(training).activeInHierarchy,
                "The old dashboard must not replace the saved report route during Start.");
            MenuScreenRouter.Instance.BackToHome();
        }
    }

    [UnityTest]
    public IEnumerator RetiredSupplyHasNoLauncherPanelOrRoute()
    {
        yield return null;
        yield return null;
        Assert.IsNull(Object.FindObjectOfType<PowerUpShopUI>());
        Assert.IsNull(GameObject.Find("PowerUpLauncher"));
        Assert.IsNull(GameObject.Find("PowerUpShop"));
        Assert.IsFalse(MenuScreenRouter.Instance.Show(MenuScreen.Supply));
        var training = Object.FindObjectOfType<AITrainingDashboardUI>();
        Assert.IsNotNull(training);
        AssertContainedHorizontally(GetPrivatePanel(training).transform.Find("Title") as RectTransform);
    }

    [TestCase(PowerUpId.Shield)]
    [TestCase(PowerUpId.Magnet)]
    [TestCase(PowerUpId.ScoreBoost)]
    [TestCase(PowerUpId.TurboStart)]
    public void ArchivedSuppliesCannotActivateOrConsumeInventory(PowerUpId id)
    {
        SavePreferenceSnapshot saveBefore = CaptureSavePreferences();
        try
        {
            var inventory = new int[4];
            inventory[(int)id] = 2;
            InstallIsolatedSave(new EchoRunSaveData {
                totalCoins = 200, powerUpInventory = inventory,
                selectedPowerUp = (int)id });
            Assert.IsFalse(GameManager.Instance.TryPurchasePowerUp(id));
            Assert.IsFalse(GameManager.Instance.SelectPowerUp(id));
            var controller = PowerUpController.Instance;
            Assert.IsNotNull(controller);
            controller.BeginRun(true);
            Assert.AreEqual(PowerUpId.None, controller.ActivePowerUp);
            Assert.AreEqual(2, EchoRunSaveSystem.GetPowerUpCount(id));
            Assert.AreEqual(id, EchoRunSaveSystem.GetSelectedPowerUp());
            Assert.AreEqual(200, EchoRunSaveSystem.TotalCoins);
            Assert.IsFalse(controller.TryAbsorbCollision());
            Assert.IsFalse(controller.HasMagnet);
            Assert.AreEqual(1f, controller.ScoreMultiplier);
            Assert.AreEqual(0f, controller.GetTurboStartBonus());
            Assert.IsEmpty(controller.GetStatusText());
        }
        finally { RestoreSavePreferences(saveBefore); }
    }

    private sealed class SavePreferenceSnapshot
    {
        public readonly Dictionary<string, string> strings =
            new Dictionary<string, string>();
        public readonly Dictionary<string, int> ints =
            new Dictionary<string, int>();
        public readonly Dictionary<string, float> floats =
            new Dictionary<string, float>();
    }

    private static readonly string[] SaveStringKeys =
    {
        EchoRunSaveSystem.SaveKey,
        EchoRunSaveSystem.SaveSlotAKey,
        EchoRunSaveSystem.SaveSlotBKey,
        EchoRunSaveSystem.SingleContractSaveSlotAKey,
        EchoRunSaveSystem.SingleContractSaveSlotBKey,
        EchoRunSaveSystem.TelemetryKey,
        "AIShadowProfileV1"
    };

    private static readonly string[] SaveIntKeys =
    {
        EchoRunSaveSystem.ActiveSaveSlotKey,
        EchoRunSaveSystem.SingleContractActiveSaveSlotKey,
        EchoRunSaveSystem.TrainingResetPendingKey,
        "HighScore",
        "TotalCoins",
        "TargetFrameRate",
        "AudioMuted",
        "CharacterPreset",
        CameraViewSettings.PreferenceKey
    };

    private static readonly string[] SaveFloatKeys =
    {
        "MasterVolume",
        "MusicVolume",
        "SfxVolume"
    };

    private static SavePreferenceSnapshot CaptureSavePreferences()
    {
        var snapshot = new SavePreferenceSnapshot();
        foreach (string key in SaveStringKeys)
            if (PlayerPrefs.HasKey(key))
                snapshot.strings[key] = PlayerPrefs.GetString(key);
        foreach (string key in SaveIntKeys)
            if (PlayerPrefs.HasKey(key))
                snapshot.ints[key] = PlayerPrefs.GetInt(key);
        foreach (string key in SaveFloatKeys)
            if (PlayerPrefs.HasKey(key))
                snapshot.floats[key] = PlayerPrefs.GetFloat(key);
        return snapshot;
    }

    private static void InstallIsolatedSave(EchoRunSaveData data)
    {
        ClearSavePreferences();
        PlayerPrefs.SetString(EchoRunSaveSystem.SaveKey,
            JsonUtility.ToJson(data));
        PlayerPrefs.SetInt("TotalCoins", data.totalCoins);
        PlayerPrefs.Save();
        ResetSaveSystemCache();
    }

    private static void RestoreSavePreferences(SavePreferenceSnapshot snapshot)
    {
        ClearSavePreferences();
        foreach (KeyValuePair<string, string> pair in snapshot.strings)
            PlayerPrefs.SetString(pair.Key, pair.Value);
        foreach (KeyValuePair<string, int> pair in snapshot.ints)
            PlayerPrefs.SetInt(pair.Key, pair.Value);
        foreach (KeyValuePair<string, float> pair in snapshot.floats)
            PlayerPrefs.SetFloat(pair.Key, pair.Value);
        PlayerPrefs.Save();
        ResetSaveSystemCache();
    }

    private static void ClearSavePreferences()
    {
        foreach (string key in SaveStringKeys) PlayerPrefs.DeleteKey(key);
        foreach (string key in SaveIntKeys) PlayerPrefs.DeleteKey(key);
        foreach (string key in SaveFloatKeys) PlayerPrefs.DeleteKey(key);
    }

    private static void ResetSaveSystemCache()
    {
        typeof(EchoRunSaveSystem).GetField("_data",
            BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
        typeof(EchoRunSaveSystem).GetField("_initialized",
            BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, false);
        typeof(EchoRunSaveSystem).GetField("_activeSlot",
            BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, -1);
        typeof(EchoRunSaveSystem).GetField("_generation",
            BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, 0L);
        typeof(EchoRunSaveSystem).GetField("_singleContractData",
            BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
        typeof(EchoRunSaveSystem).GetField("_singleContractInitialized",
            BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, false);
        typeof(EchoRunSaveSystem).GetField("_singleContractActiveSlot",
            BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, -1);
        typeof(EchoRunSaveSystem).GetField("_singleContractGeneration",
            BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, 0L);
        typeof(EchoRunSaveSystem).GetField("_trainingResetInProgress",
            BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, false);
        typeof(EchoRunSaveSystem).GetField("_trainingWritesEnabled",
            BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, true);
    }

    private static GameObject GetPrivatePanel(object component)
    {
        return GetPrivateObject(component, "_panel");
    }

    private static GameObject GetPrivateObject(object component, string field)
    {
        return (GameObject)component.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(component);
    }

    private static T GetPrivateField<T>(object component, string field)
        where T : class
    {
        FieldInfo info = component.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(info, "Missing private field: " + field);
        return info.GetValue(component) as T;
    }

    private static void AssertSoundReadout(Text readout, string expectedLabel)
    {
        Assert.IsNotNull(readout);
        Assert.IsTrue(readout.gameObject.activeInHierarchy);
        Assert.IsTrue(readout.enabled);
        StringAssert.StartsWith(expectedLabel, readout.text);
        Assert.Greater(readout.color.a, 0.9f);
        Assert.Greater(readout.preferredWidth, 0f);
        Assert.Greater(readout.cachedTextGenerator.vertexCount, 0);
    }

    private static void AssertButtonVisibleAndRaycastable(
        Button button, RectTransform safeArea)
    {
        Assert.IsNotNull(button);
        Assert.IsTrue(button.gameObject.activeInHierarchy);
        Assert.IsTrue(button.IsInteractable());
        Assert.IsNotNull(button.targetGraphic);
        Assert.IsTrue(button.targetGraphic.enabled);
        Assert.IsTrue(button.targetGraphic.raycastTarget);
        Assert.Greater(button.targetGraphic.canvasRenderer.GetAlpha(), 0.01f);
        AssertRectContained(button.transform as RectTransform, safeArea);

        Canvas canvas = button.GetComponentInParent<Canvas>();
        Assert.IsNotNull(canvas);
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        Assert.IsNotNull(raycaster);
        Assert.IsNotNull(EventSystem.current);
        Canvas.ForceUpdateCanvases();
        RectTransform rect = button.transform as RectTransform;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            canvas.worldCamera, rect.TransformPoint(rect.rect.center));
        var eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPoint
        };
        var hits = new List<RaycastResult>();
        raycaster.Raycast(eventData, hits);
        Assert.IsTrue(hits.Exists(hit =>
                hit.gameObject == button.gameObject
                || hit.gameObject.transform.IsChildOf(button.transform)),
            button.name + " is not reachable by a UI raycast at its center.");
    }

    private static void AssertRectContained(
        RectTransform child, RectTransform container)
    {
        Assert.IsNotNull(child);
        Assert.IsNotNull(container);
        Canvas.ForceUpdateCanvases();
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            container, child);
        Rect rect = container.rect;
        const float tolerance = 0.5f;
        Assert.GreaterOrEqual(bounds.min.x, rect.xMin - tolerance,
            child.name + " extends beyond the left SafeArea edge.");
        Assert.LessOrEqual(bounds.max.x, rect.xMax + tolerance,
            child.name + " extends beyond the right SafeArea edge.");
        Assert.GreaterOrEqual(bounds.min.y, rect.yMin - tolerance,
            child.name + " extends below the SafeArea edge.");
        Assert.LessOrEqual(bounds.max.y, rect.yMax + tolerance,
            child.name + " extends above the SafeArea edge.");
    }

    private static void AssertContainedHorizontally(RectTransform child)
    {
        Assert.IsNotNull(child);
        RectTransform parent = child.parent as RectTransform;
        Assert.IsNotNull(parent);

        float left = parent.rect.xMin
                     + child.anchorMin.x * parent.rect.width
                     + child.anchoredPosition.x
                     - child.pivot.x * child.rect.width;
        float right = left + child.rect.width;
        Assert.GreaterOrEqual(left, parent.rect.xMin - 0.01f,
            child.name + " extends beyond the left panel edge.");
        Assert.LessOrEqual(right, parent.rect.xMax + 0.01f,
            child.name + " extends beyond the right panel edge.");
    }
}
