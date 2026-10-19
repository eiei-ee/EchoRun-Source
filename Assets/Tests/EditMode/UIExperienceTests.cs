using NUnit.Framework;
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class UIExperienceTests
{
    [Test]
    public void SafeAreaFallsBackWhenReportedCoordinatesLeaveTheCanvas()
    {
        Rect normalized = UILayoutRules.NormalizeSafeArea(
            new Rect(904f, 508f, 1808f, 1017f), 1808, 1017);

        Assert.AreEqual(new Rect(0f, 0f, 1808f, 1017f), normalized);
    }

    [Test]
    public void SafeAreaKeepsAValidMobileInset()
    {
        Rect normalized = UILayoutRules.NormalizeSafeArea(
            new Rect(0f, 54f, 1080f, 1812f), 1080, 1920);

        Assert.AreEqual(new Rect(0f, 54f, 1080f, 1812f), normalized);
    }

    [Test]
    public void PrimaryAndResultActionsFitLandscapeAndPortraitReferences()
    {
        Vector2 landscape = UILayoutRules.GetReferenceResolution(1920, 1080);
        Vector2 portrait = UILayoutRules.GetReferenceResolution(1080, 1920);
        Vector2 landscapeStart = UILayoutRules.GetPrimaryActionSize(
            1920, 1080, false);
        Vector2 portraitStart = UILayoutRules.GetPrimaryActionSize(
            1080, 1920, true);
        Vector2 landscapeRestart = UILayoutRules.GetRestartButtonSize(
            1920, 1080, false);
        Vector2 portraitRestart = UILayoutRules.GetRestartButtonSize(
            1080, 1920, true);
        Vector2 landscapeMenu = UILayoutRules.GetMenuButtonSize(
            1920, 1080, false);
        Vector2 portraitMenu = UILayoutRules.GetMenuButtonSize(
            1080, 1920, true);
        Vector2 landscapeResult = UILayoutRules.GetResultTextSize(1920, 1080);
        Vector2 portraitResult = UILayoutRules.GetResultTextSize(1080, 1920);

        Assert.AreEqual(new Vector2(520f, 78f), landscapeStart);
        Assert.AreEqual(new Vector2(760f, 104f), portraitStart);
        Assert.AreEqual(new Vector2(380f, 76f), landscapeRestart);
        Assert.AreEqual(new Vector2(520f, 104f), portraitRestart);
        Assert.AreEqual(new Vector2(280f, 60f), landscapeMenu);
        Assert.AreEqual(new Vector2(420f, 104f), portraitMenu);
        Assert.AreEqual(new Vector2(1160f, 260f), landscapeResult);
        Assert.AreEqual(new Vector2(900f, 360f), portraitResult);

        AssertCenteredRectFits(new Vector2(0.5f, 0.255f),
            landscapeStart, landscape);
        AssertCenteredRectFits(new Vector2(0.5f, 0.255f),
            portraitStart, portrait);
        AssertCenteredRectFits(new Vector2(0.5f, 0.40f),
            landscapeResult, landscape);
        AssertCenteredRectFits(new Vector2(0.5f, 0.40f),
            portraitResult, portrait);
        AssertCenteredRectFits(new Vector2(0.5f, 0.18f),
            landscapeRestart, landscape);
        AssertCenteredRectFits(new Vector2(0.5f, 0.18f),
            portraitRestart, portrait);
        AssertCenteredRectFits(new Vector2(0.5f, 0.07f),
            landscapeMenu, landscape);
        AssertCenteredRectFits(new Vector2(0.5f, 0.07f),
            portraitMenu, portrait);
    }

    [TestCase(false, 1920f)]
    [TestCase(true, 1080f)]
    public void HomeNavigationKeepsReadableGapsAtEveryReferenceSize(
        bool portrait, float referenceWidth)
    {
        Vector2 size = UILayoutRules.GetHomeNavigationSize(portrait, portrait);
        Vector2 runner = UILayoutRules.GetHomeNavigationAnchor(0, portrait);
        Vector2 supply = UILayoutRules.GetHomeNavigationAnchor(1, portrait);
        Vector2 settings = UILayoutRules.GetHomeNavigationAnchor(2, portrait);

        Assert.AreEqual(supply.x - runner.x, settings.x - supply.x, 0.0001f);
        float firstSeam = (supply.x - runner.x) * referenceWidth - size.x;
        float secondSeam = (settings.x - supply.x) * referenceWidth - size.x;
        float expectedGap = portrait ? 42.4f : 21.6f;
        Assert.AreEqual(expectedGap, firstSeam, 0.2f);
        Assert.AreEqual(expectedGap, secondSeam, 0.2f);
    }

    private static void AssertCenteredRectFits(
        Vector2 anchor, Vector2 size, Vector2 reference)
    {
        Vector2 center = Vector2.Scale(anchor, reference);
        Vector2 half = size * 0.5f;
        Assert.GreaterOrEqual(center.x - half.x, 0f);
        Assert.LessOrEqual(center.x + half.x, reference.x);
        Assert.GreaterOrEqual(center.y - half.y, 0f);
        Assert.LessOrEqual(center.y + half.y, reference.y);
    }

    [Test]
    public void MenuPresentationSeparatesCalibrationFromChallenge()
    {
        EchoMenuViewData calibration = EchoRunPresentation.BuildMenu(
            0, new PlayerStyleData(), 2, 3);

        Assert.AreEqual("首次回声校准", calibration.generation);
        StringAssert.Contains("跳跃 2 次", calibration.objective);
        StringAssert.Contains("滑铲 3 次", calibration.objective);
        Assert.AreEqual("开始校准", calibration.primaryAction);

        EchoDuelViewData calibrationHud = EchoRunPresentation.BuildDuel(
            false, null, 0f, 2, 3, 1, 2, 0.4f);
        Assert.AreEqual("跳跃 1/2 · 滑铲 2/3", calibrationHud.progress);
        Assert.AreEqual(0.4f, calibrationHud.progress01, 0.001f);

        EchoMenuViewData challenge = EchoRunPresentation.BuildMenu(
            4, new PlayerStyleData(), 2, 3);
        Assert.AreEqual("第 4 代回声", challenge.generation);
        Assert.AreEqual("挑战第 4 代回声", challenge.primaryAction);
        Assert.IsNotEmpty(challenge.learned);
        StringAssert.DoesNotContain("AI识别：", challenge.learned);
        StringAssert.DoesNotContain("权重", challenge.rule);
        StringAssert.DoesNotContain("置信", challenge.rule);
    }

    [Test]
    public void PortraitMenuBackgroundIsBundledWithoutMipmapsOrCpuCopy()
    {
        const string path = "Assets/Resources/Art/Menu/MemoryCorridorMenuPortrait.png";
        var background = Resources.Load<Texture2D>("Art/Menu/MemoryCorridorMenuPortrait");
        Assert.IsNotNull(background);
        Assert.GreaterOrEqual(background.width, 768);
        Assert.That((float)background.height / background.width, Is.InRange(1.9f, 2.1f));
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.IsFalse(importer.mipmapEnabled);
        Assert.IsFalse(importer.isReadable);
        Assert.AreEqual(TextureImporterAlphaSource.None, importer.alphaSource);
        Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
        Assert.LessOrEqual(importer.maxTextureSize, 2048);
    }

    [Test]
    public void OriginalMenuBackgroundIsBundledAtFullQuality()
    {
        const string path =
            "Assets/Resources/Art/Menu/MemoryCorridorMenu.png";
        Texture2D background = Resources.Load<Texture2D>(
            "Art/Menu/MemoryCorridorMenu");
        Assert.IsNotNull(background);
        Assert.GreaterOrEqual(background.width, 1600);
        Assert.GreaterOrEqual(background.height, 900);

        TextureImporter importer = AssetImporter.GetAtPath(path)
            as TextureImporter;
        Assert.IsNotNull(importer);
        Assert.IsFalse(importer.mipmapEnabled);
        Assert.AreEqual(TextureImporterNPOTScale.None, importer.npotScale);
        Assert.AreEqual(TextureImporterType.Default, importer.textureType);
        Assert.AreEqual(TextureImporterCompression.Uncompressed,
            importer.textureCompression);
    }

    [Test]
    public void DuelPresentationMakesContractProgressAndLeadExplicit()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            targetLane = 2,
            targetAction = ShadowAction.Slide,
            progress = 2f,
            targetProgress = 3f,
            lastFeedback = "反制生效：动作正确",
            feedbackSequence = 7
        };

        EchoDuelViewData leading = EchoRunPresentation.BuildDuel(
            true, contract, 2.75f, 2, 2);
        Assert.AreEqual("AI预测已公开 · 打破旧习惯", leading.contract);
        Assert.AreEqual("深度裂解", leading.progress);
        Assert.AreEqual(1f / 3f, leading.progress01, 0.001f);
        Assert.AreEqual(EchoLeadState.Leading, leading.leadState);
        StringAssert.StartsWith("领先 +2.8m", leading.lead);
        StringAssert.StartsWith("反制成功", leading.feedback);
        Assert.AreEqual(7, leading.feedbackSequence);

        EchoDuelViewData trailing = EchoRunPresentation.BuildDuel(
            true, contract, -1.2f, 2, 2);
        Assert.AreEqual(EchoLeadState.Trailing, trailing.leadState);
        StringAssert.StartsWith("落后 -1.2m", trailing.lead);
    }

    [Test]
    public void DuelPredictionAppearsOnlyAfterDetection()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 2,
            predictionLane = 2,
            targetProgress = 100f
        };

        EchoDuelViewData detection = EchoRunPresentation.BuildDuel(
            true, contract, 0f, 2, 2,
            duelPhase: EchoDuelPhase.Detection,
            publicPrediction: "回声预判：你会继续依赖右侧路线");
        EchoDuelViewData reveal = EchoRunPresentation.BuildDuel(
            true, contract, 0f, 2, 2,
            duelPhase: EchoDuelPhase.Reveal,
            publicPrediction: "回声预判：你会继续依赖右侧路线");

        Assert.IsEmpty(detection.prediction);
        Assert.AreEqual("回声预判：你会继续依赖右侧路线",
            reveal.prediction);
    }

    [Test]
    public void RhythmHudDoesNotExposeTheNextRequiredAction()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.DisruptRhythm,
            targetAction = ShadowAction.Jump,
            targetProgress = 4f
        };

        EchoDuelViewData view = EchoRunPresentation.BuildDuel(
            true, contract, 0f, 2, 2);

        Assert.AreEqual("AI预测已公开 · 打破旧习惯", view.contract);
        StringAssert.DoesNotContain("跳跃", view.contract);
    }

    [Test]
    public void MenuRouterKeepsExactlyOneScreenAndHomeNavigationState()
    {
        GameObject root = new GameObject("MenuRouterTest");
        GameObject home = new GameObject("Home");
        GameObject settings = new GameObject("Settings");
        GameObject launcher = new GameObject("Launcher");
        home.transform.SetParent(root.transform);
        settings.transform.SetParent(root.transform);
        launcher.transform.SetParent(root.transform);

        try
        {
            MenuScreenRouter router = root.AddComponent<MenuScreenRouter>();
            router.Initialize(null);
            router.Register(MenuScreen.Home, home);
            router.Register(MenuScreen.Settings, settings);
            router.RegisterHomeNavigation(launcher);
            router.EnterMenu();

            Assert.IsTrue(home.activeSelf);
            Assert.IsFalse(settings.activeSelf);
            Assert.IsTrue(launcher.activeSelf);

            Assert.IsTrue(router.Show(MenuScreen.Settings));
            Assert.IsFalse(home.activeSelf);
            Assert.IsTrue(settings.activeSelf);
            Assert.IsFalse(launcher.activeSelf);

            router.BackToHome();
            Assert.IsTrue(home.activeSelf);
            Assert.IsFalse(settings.activeSelf);
            Assert.IsTrue(launcher.activeSelf);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RunnerAppearanceUsesPropertyBlocksWithoutCloningMaterial()
    {
        Shader shader = Shader.Find("EchoRun/ExoGrayBlueTech");
        Assert.IsNotNull(shader);
        GameObject runner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material shared = new Material(shader);
        Renderer renderer = runner.GetComponent<Renderer>();
        renderer.sharedMaterial = shared;

        try
        {
            Color dark = new Color(0.02f, 0.03f, 0.04f);
            Color light = new Color(0.4f, 0.5f, 0.6f);
            Color emission = new Color(0.1f, 0.8f, 1.2f);
            Assert.AreEqual(1, RunnerAppearanceService.Apply(
                runner.transform, dark, light, emission));
            Assert.AreSame(shared, renderer.sharedMaterial);

            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties, 0);
            AssertColor(dark, properties.GetColor("_DarkColor"));
            AssertColor(light, properties.GetColor("_LightColor"));
            AssertColor(emission, properties.GetColor("_EmissionColor"));
        }
        finally
        {
            Object.DestroyImmediate(runner);
            Object.DestroyImmediate(shared);
        }
    }

    [Test]
    public void RunnerAppearanceSeparatesHardArmorFromSoftInnerLayer()
    {
        Color dark = new Color(0.03f, 0.06f, 0.10f);
        Color light = new Color(0.32f, 0.46f, 0.60f);
        Color emission = new Color(0.95f, 0.72f, 0.48f);

        RunnerAppearanceService.ResolveMaterialPalette(
            "Exo_MAT_BlueTech", dark, light, emission,
            out Color armorDark, out Color armorLight,
            out Color armorEmission);
        RunnerAppearanceService.ResolveMaterialPalette(
            "Body_MAT_BlueTech", dark, light, emission,
            out Color bodyDark, out Color bodyLight,
            out Color bodyEmission);

        Assert.Greater(armorLight.grayscale, light.grayscale * 1.15f);
        Assert.Less(bodyLight.grayscale, light.grayscale * 0.70f);
        Assert.Less(bodyDark.grayscale, armorDark.grayscale);
        Assert.Less(bodyEmission.grayscale, armorEmission.grayscale);
    }

    [Test]
    public void PlayerFacingFontCoversNewInterfaceCopy()
    {
        Font font = Resources.Load<Font>("Fonts/EchoRunSansSC-Regular");
        Assert.IsNotNull(font);
        const string copy =
            "首次回声校准挑战契约补给舱跑者外观库存装备领先落后已破解"
            + "设置主音量音乐音效一键静音已画面帧率辅助显示选择配色立即预览并保存"
            + "跑酷难度休闲标准高压障碍较少密集恢复窗"
            + "大字高对比减少动态返回百分比补充"
            + "本机实时观察你的选路跳跃和滑铲不同动作学习条变亮形成下一局的对手"
            + "还没看清路线习惯旧回声不会丢让它再观察猜中抢先连续两次骗过改猜"
            + "留在身后正在学跑法学够了去终点主动种类受伤红它猜的路青骗它的路白安全路"
            + "后续更新看到了这局观察不会带到下一局重新已经全亮没有形成遇到问题"
            + "本局结果整理变化同样不足当前保持不变原本认为压力偏向开始仍可能"
            + "伤势再受伤即出局恢复中"
            + "《影迹回声》过去正在追上你米回声现身上一局学到×"
            + "已保存的回声回声报告当前已保存动作记录路线证据"
            + "下一局继续观察。动作与选路需要在本局内重新记录，达到条件并到达终点后更新。"
            + "下一局可挑战这代回声。赢下本局、达到更新条件并保存成功后，新代才会生效。"
            + "本局观察详情单项完成不代表全部达标；本局未形成新代时，下局需要重新观察。"
            + "操作与选路反制路成功通过才获得领先奖励；中立路不算反制。"
            + "挑战好友的回声本局使用已确认的好友跑法争取领先"
            + "本局不影响你的回声成长和本地纪录开始好友挑战"
            + "高视角能多看到前方道路，随时可以切换。";
        foreach (char character in copy)
            Assert.IsTrue(font.HasCharacter(character), "UI font is missing: " + character);
    }

    [Test]
    public void BundledFontMatchesValidatedStaticRegularSubset()
    {
        string path = Path.Combine(Application.dataPath,
            "Resources/Fonts/EchoRunSansSC-Regular.otf");
        Assert.IsTrue(File.Exists(path));
        using (SHA256 sha = SHA256.Create())
        {
            string actual = BitConverter.ToString(
                sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
            Assert.AreEqual(
                "0CB2A65839604754D09BAFA67E7F447750032DF4038B04B3B7897B1B033D6623",
                actual,
                "The bundled font must stay the validated static Regular 400 subset.");
        }
    }

    [Test]
    public void AccessibilityPreferencesApplyToRuntimeTextAndPersistMotionChoice()
    {
        bool oldLarge = EchoRunAccessibility.LargeText;
        bool oldContrast = EchoRunAccessibility.HighContrast;
        bool oldMotion = EchoRunAccessibility.ReducedMotion;
        GameObject textObject = new GameObject("AccessibleText", typeof(UnityEngine.UI.Text));

        try
        {
            UnityEngine.UI.Text text = textObject.GetComponent<UnityEngine.UI.Text>();
            text.fontSize = 20;
            EchoRunAccessibility.SetLargeText(false);
            EchoRunAccessibility.SetHighContrast(false);
            EchoRunAccessibility.Prepare(text);
            Assert.AreEqual(20, text.fontSize);

            EchoRunAccessibility.SetLargeText(true);
            EchoRunAccessibility.SetHighContrast(true);
            EchoRunAccessibility.SetReducedMotion(true);
            EchoRunAccessibility.ApplyToHierarchy(textObject.transform);

            Assert.AreEqual(22, text.fontSize);
            EchoRunAccessibleText marker = text.GetComponent<EchoRunAccessibleText>();
            Assert.IsNotNull(marker);
            Assert.IsNotNull(marker.contrastOutline);
            Assert.IsTrue(marker.contrastOutline.enabled);
            Assert.IsTrue(EchoRunAccessibility.ReducedMotion);
        }
        finally
        {
            EchoRunAccessibility.SetLargeText(oldLarge);
            EchoRunAccessibility.SetHighContrast(oldContrast);
            EchoRunAccessibility.SetReducedMotion(oldMotion);
            Object.DestroyImmediate(textObject);
        }
    }

    [Test]
    public void HighContrastOutlineTracksLightAndDarkTextWithoutThickeningDarkActionLabels()
    {
        bool oldContrast = EchoRunAccessibility.HighContrast;
        var owner = new GameObject("AccessibleActionLabel", typeof(Text));
        try
        {
            Text text = owner.GetComponent<Text>();
            text.text = "挑战新回声";
            text.fontStyle = FontStyle.Bold;
            text.color = EchoRunUITheme.Ink;
            EchoRunAccessibility.SetHighContrast(true);
            EchoRunAccessibility.Prepare(text);
            EchoRunAccessibleText marker = text.GetComponent<EchoRunAccessibleText>();
            Assert.IsTrue(marker.contrastOutline == null || !marker.contrastOutline.enabled,
                "Dark text on the bright action button must not gain a black outline.");

            text.color = EchoRunUITheme.TextPrimary;
            EchoRunAccessibility.Prepare(text);
            Assert.IsNotNull(marker.contrastOutline);
            Assert.IsTrue(marker.contrastOutline.enabled);

            text.color = EchoRunUITheme.Ink;
            EchoRunAccessibility.Prepare(text);
            Assert.IsFalse(marker.contrastOutline.enabled,
                "Changing an existing outlined label back to dark ink must disable its outline.");

            EchoRunAccessibility.SetHighContrast(false);
            text.color = EchoRunUITheme.TextPrimary;
            EchoRunAccessibility.Prepare(text);
            Assert.IsFalse(marker.contrastOutline.enabled);
        }
        finally
        {
            EchoRunAccessibility.SetHighContrast(oldContrast);
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void TouchTargetsAndCameraAdaptToOrientation()
    {
        Assert.AreEqual(104f, UILayoutRules.EnsureTouchButtonSize(
            new Vector2(180f, 56f), true, false).y);
        Assert.AreEqual(72f, UILayoutRules.EnsureTouchSliderSize(
            new Vector2(500f, 40f), true, false).y);
        Assert.AreEqual(56f, UILayoutRules.EnsureTouchButtonSize(
            new Vector2(180f, 56f), false, false).y);
        Assert.AreEqual(62f, WorldStyler.GetCameraFieldOfView(true));
        Assert.AreEqual(56f, WorldStyler.GetCameraFieldOfView(false));
        Assert.AreEqual(new Vector3(0f, 5.4f, -12f),
            WorldStyler.GetCameraOffset(true));
        Assert.AreEqual(new Vector3(0f, 4.2f, -10.5f),
            WorldStyler.GetCameraOffset(false));
    }

    private static void AssertColor(Color expected, Color actual)
    {
        Assert.AreEqual(expected.r, actual.r, 0.001f);
        Assert.AreEqual(expected.g, actual.g, 0.001f);
        Assert.AreEqual(expected.b, actual.b, 0.001f);
        Assert.AreEqual(expected.a, actual.a, 0.001f);
    }

    [TestCase(1920, 1080, false)]
    [TestCase(1080, 1920, true)]
    public void CompactHomeKeepsActionsInsideItsSafeAreaCard(int width, int height, bool portrait)
    {
        using (var fixture = new CompactUiFixture(width, height))
        {
            fixture.Call("CreateMenuPanel");
            fixture.Field<Button>("_reportBtn").gameObject.SetActive(true);
            fixture.Call("LayoutMenu", portrait, portrait);
            RectTransform card = fixture.Field<RectTransform>("_menuReadabilityVeil");
            Assert.LessOrEqual(card.anchorMax.x - card.anchorMin.x, portrait ? 1f : .34f);
            Assert.LessOrEqual(card.anchorMax.y, portrait ? .35f : .60f);
            var corners = new Vector3[4];
            card.GetWorldCorners(corners);
            Vector3 minimum = corners[0];
            Vector3 maximum = corners[2];
            foreach (string field in new[] { "_startBtn", "_characterBtn", "_settingsBtn", "_reportBtn" })
            {
                RectTransform button = fixture.Field<Button>(field).GetComponent<RectTransform>();
                button.GetWorldCorners(corners);
                Assert.GreaterOrEqual(corners[0].x, minimum.x - 1f, field);
                Assert.GreaterOrEqual(corners[0].y, minimum.y - 1f, field);
                Assert.LessOrEqual(corners[2].x, maximum.x + 1f, field);
                Assert.LessOrEqual(corners[2].y, maximum.y + 1f, field);
                if (portrait) Assert.GreaterOrEqual(button.rect.height, 104f);
            }
        }
    }

    [Test]
    public void ReportRequiresAValidSavedIdentityAndDoesNotPromiseCrossRunTopUps()
    {
        Assert.IsEmpty(UIManager.BuildSavedEchoReport(null));
        Assert.IsEmpty(UIManager.BuildSavedEchoReport(new ActiveEchoIdentity()));
        string report = UIManager.BuildSavedEchoReport(new ActiveEchoIdentity
        {
            generation = 1, identityId = "saved", pace = 12f,
            style = new EchoIdentityStyleSnapshot { jumpActionSamples = 2, slideActionSamples = 3 }
        });
        StringAssert.Contains("当前已保存", report);
        StringAssert.Contains("跳跃 2 次", report);
        StringAssert.Contains("滑铲 3 次", report);
        StringAssert.Contains("本局内重新记录", report);
    }

    [TestCase(GameplayFlowMode.SingleContract, false)]
    [TestCase(GameplayFlowMode.SingleContract, true)]
    [TestCase(GameplayFlowMode.AsyncChallenge, false)]
    [TestCase(GameplayFlowMode.AsyncChallenge, true)]
    public void SavedReportOwnsTheActualRouteRegardlessOfDashboardRegistrationOrder(
        GameplayFlowMode mode, bool dashboardFirst)
    {
        using (var fixture = new ReportNavigationFixture(mode, dashboardFirst))
        {
            Assert.IsFalse(fixture.ReportButton.gameObject.activeInHierarchy,
                "An empty archive has no report action.");
            Assert.IsFalse(fixture.LegacyLauncher.activeInHierarchy);
            fixture.Router.Show(MenuScreen.Settings);
            fixture.Router.BackToHome();
            Assert.IsFalse(fixture.LegacyLauncher.activeInHierarchy,
                "Returning home must not reactivate the legacy launcher.");

            fixture.SetIdentity(SingleContractValidationIdentity.Create());
            fixture.RefreshOwners(dashboardFirst);
            Assert.IsTrue(fixture.ReportButton.gameObject.activeInHierarchy);
            Assert.IsFalse(fixture.LegacyLauncher.activeInHierarchy);
            fixture.ReportButton.onClick.Invoke();

            Assert.AreEqual(MenuScreen.EchoReport, fixture.Router.CurrentScreen);
            Assert.IsTrue(fixture.SavedPanel.activeInHierarchy,
                "The visible button must open the saved-identity panel, not the old dashboard.");
            Assert.IsFalse(fixture.LegacyPanel.activeInHierarchy);
            StringAssert.Contains("当前已保存", fixture.SavedText.text);
            StringAssert.Contains("路线证据", fixture.SavedText.text);

            fixture.SetIdentity(null);
            fixture.RefreshOwners(dashboardFirst);
            Assert.IsTrue(fixture.Router.IsHome,
                "A report that loses its saved identity must return home.");
            Assert.IsFalse(fixture.ReportButton.gameObject.activeInHierarchy);
            Assert.IsFalse(fixture.LegacyLauncher.activeInHierarchy);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LegacyDashboardKeepsItsReportRouteAndCanHandItBackToSavedReports(bool dashboardFirst)
    {
        using (var fixture = new ReportNavigationFixture(GameplayFlowMode.SixPhaseLegacy, dashboardFirst))
        {
            fixture.SetIdentity(SingleContractValidationIdentity.Create());
            fixture.RefreshOwners(dashboardFirst);
            Assert.IsFalse(fixture.ReportButton.gameObject.activeInHierarchy);
            Assert.IsTrue(fixture.LegacyLauncher.activeInHierarchy);
            fixture.LegacyLauncher.GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(fixture.LegacyPanel.activeInHierarchy);
            Assert.IsFalse(fixture.SavedPanel.activeInHierarchy);
            Assert.IsFalse(fixture.LegacyLauncher.activeInHierarchy);

            fixture.Router.Show(MenuScreen.Settings);
            Assert.IsFalse(fixture.LegacyLauncher.activeInHierarchy);
            fixture.Router.BackToHome();
            Assert.IsTrue(fixture.LegacyLauncher.activeInHierarchy);

            fixture.SetMode(GameplayFlowMode.SingleContract);
            fixture.RefreshOwners(!dashboardFirst);
            fixture.ReportButton.onClick.Invoke();
            Assert.IsTrue(fixture.SavedPanel.activeInHierarchy);
            Assert.IsFalse(fixture.LegacyPanel.activeInHierarchy);
            Assert.IsFalse(fixture.LegacyLauncher.activeInHierarchy);

            fixture.Router.BackToHome();
            fixture.SetMode(GameplayFlowMode.SixPhaseLegacy);
            fixture.RefreshOwners(dashboardFirst);
            fixture.LegacyLauncher.GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(fixture.LegacyPanel.activeInHierarchy);
            Assert.IsFalse(fixture.SavedPanel.activeInHierarchy);
        }
    }

    [Test]
    public void FirstRunTeachingRemainsUntilActionsAndRouteSettlementAreObserved()
    {
        StringAssert.Contains("跳跃", UIManager.BuildSingleContractTeaching(true, 1, 0, 0, false, false));
        string activeGate = UIManager.BuildSingleContractTeaching(true, 1, 1, 0, true, false);
        StringAssert.Contains("反制", activeGate);
        StringAssert.Contains("中立", activeGate);
        Assert.IsEmpty(UIManager.BuildSingleContractTeaching(true, 1, 1, 1, false, false));
    }

    [TestCase(7.3f, "你领先：7.3米")]
    [TestCase(-4.2f, "回声领先：4.2米")]
    public void AsyncFactsResultKeepsTheFinalGapVisible(float lead, string expected)
    {
        GameManager previousGame = GameManager.Instance;
        AIShadowRunner previousShadow = AIShadowRunner.Instance;
        var runtime = new GameObject("Inactive_ResultFacts");
        runtime.SetActive(false);
        try
        {
            GameManager game = runtime.AddComponent<GameManager>();
            AIShadowRunner shadow = runtime.AddComponent<AIShadowRunner>();
            CompactUiFixture.Set(game, "<State>k__BackingField", GameState.GameOver);
            CompactUiFixture.Set(game, "<ActiveGameplayFlowMode>k__BackingField", GameplayFlowMode.AsyncChallenge);
            CompactUiFixture.Set(game, "<LastAsyncChallengeResult>k__BackingField",
                new AsyncChallengeResult(new AsyncChallengeRunParameters("test", 1, 42), "opponent",
                    900f, lead, RunEndReason.FinishReached, lead >= 0f));
            CompactUiFixture.Set(shadow, "<LastSingleContractResultFacts>k__BackingField",
                new SingleContractResultFacts { available = true, isAsync = true, won = lead >= 0f,
                    endReason = RunEndReason.FinishReached });
            typeof(GameManager).GetField("<Instance>k__BackingField",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, game);
            typeof(AIShadowRunner).GetField("<Instance>k__BackingField",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, shadow);
            using (var fixture = new CompactUiFixture(1920, 1080))
            {
                CompactUiFixture.Set(fixture.ui, "_gm", game);
                fixture.Call("CreateGameOverPanel");
                fixture.Call("OnGameStateChanged", GameState.GameOver);
                StringAssert.Contains(expected, fixture.Field<Text>("_shadowResultText").text);
                Assert.AreEqual(SingleContractResultNextAction.RetryAsync,
                    fixture.Field<SingleContractResultNextAction>("_resultNextAction"));
                Assert.IsFalse(fixture.Field<Button>("_resultDetailsBtn").gameObject.activeSelf);
            }
        }
        finally
        {
            typeof(GameManager).GetField("<Instance>k__BackingField",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, previousGame);
            typeof(AIShadowRunner).GetField("<Instance>k__BackingField",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, previousShadow);
            Object.DestroyImmediate(runtime);
        }
    }

    private sealed class ReportNavigationFixture : IDisposable
    {
        private const System.Reflection.BindingFlags Private =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        private const System.Reflection.BindingFlags StaticPrivate =
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        private readonly System.Collections.Generic.Dictionary<System.Reflection.FieldInfo, object> saves =
            new System.Collections.Generic.Dictionary<System.Reflection.FieldInfo, object>();
        private readonly GameObject owner;
        private readonly GameManager game;
        private readonly AITrainingDashboardUI dashboard;
        private readonly CompactUiFixture compact;
        public MenuScreenRouter Router { get; }
        public Button ReportButton => compact.Field<Button>("_reportBtn");
        public GameObject SavedPanel => compact.Field<GameObject>("_reportPanel");
        public Text SavedText => compact.Field<Text>("_reportText");
        public GameObject LegacyPanel => DashboardField<GameObject>("_panel");
        public GameObject LegacyLauncher => DashboardField<GameObject>("_launcher");

        public ReportNavigationFixture(GameplayFlowMode mode, bool dashboardFirst)
        {
            foreach (string fieldName in new[] { "_data", "_initialized",
                         "_singleContractData", "_singleContractInitialized" })
            {
                var field = typeof(EchoRunSaveSystem).GetField(fieldName, StaticPrivate);
                saves.Add(field, field.GetValue(null));
            }
            SetSave("_data", new EchoRunSaveData());
            SetSave("_initialized", true);
            SetSave("_singleContractInitialized", true);
            SetIdentity(null);
            owner = new GameObject("Inactive_ReportNavigation");
            owner.SetActive(false);
            game = owner.AddComponent<GameManager>();
            SetMode(mode);
            Router = owner.AddComponent<MenuScreenRouter>();
            Router.Initialize(game);
            compact = new CompactUiFixture(1920, 1080);
            CompactUiFixture.Set(compact.ui, "_gm", game);
            CompactUiFixture.Set(compact.ui, "_menuRouter", Router);
            compact.Call("CreateMenuPanel");
            compact.Call("CreateEchoReportPanel");
            Router.Register(MenuScreen.Home, compact.Field<GameObject>("_menuPanel"));
            var settings = new GameObject("ReportNavigationSettings", typeof(RectTransform));
            settings.transform.SetParent(compact.Parent, false);
            Router.Register(MenuScreen.Settings, settings);
            dashboard = owner.AddComponent<AITrainingDashboardUI>();
            CompactUiFixture.Set(dashboard, "_gameManager", game);
            CompactUiFixture.Set(dashboard, "_router", Router);
            DashboardCall("Build", compact.Parent);
            Router.RouteChanged += (Action<MenuScreen>)Delegate.CreateDelegate(
                typeof(Action<MenuScreen>), dashboard,
                typeof(AITrainingDashboardUI).GetMethod("OnMenuRouteChanged", Private));
            RefreshOwners(dashboardFirst);
            Router.EnterMenu();
        }

        public void SetIdentity(ActiveEchoIdentity identity) =>
            SetSave("_singleContractData", new EchoSingleContractSaveData { activeIdentity = identity });

        public void SetMode(GameplayFlowMode mode) =>
            CompactUiFixture.Set(game, "gameplayFlowMode", mode);

        public void RefreshOwners(bool dashboardFirst)
        {
            if (dashboardFirst) DashboardCall("RefreshReportNavigation");
            compact.Call("RefreshEchoReportNavigation");
            if (!dashboardFirst) DashboardCall("RefreshReportNavigation");
        }

        private static void SetSave(string name, object value) =>
            typeof(EchoRunSaveSystem).GetField(name, StaticPrivate).SetValue(null, value);
        private void DashboardCall(string method, params object[] values) =>
            typeof(AITrainingDashboardUI).GetMethod(method, Private).Invoke(dashboard, values);
        private T DashboardField<T>(string name) =>
            (T)typeof(AITrainingDashboardUI).GetField(name, Private).GetValue(dashboard);

        public void Dispose()
        {
            compact.Dispose();
            Object.DestroyImmediate(owner);
            foreach (var saved in saves) saved.Key.SetValue(null, saved.Value);
        }
    }

    private sealed class CompactUiFixture : IDisposable
    {
        public readonly UIManager ui;
        private readonly GameObject owner;
        private readonly GameObject viewport;
        public Transform Parent => viewport.transform;
        private const System.Reflection.BindingFlags Private =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        public CompactUiFixture(int width, int height)
        {
            owner = new GameObject("Inactive_CompactUi");
            owner.SetActive(false);
            viewport = new GameObject("CompactUi_Viewport", typeof(RectTransform));
            viewport.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            ui = owner.AddComponent<UIManager>();
            Set(ui, "_font", Resources.Load<Font>("Fonts/EchoRunSansSC-Regular"));
            Set(ui, "_titleFont", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            Set(ui, "_safeAreaRoot", viewport.GetComponent<RectTransform>());
        }
        public void Call(string method, params object[] values) =>
            typeof(UIManager).GetMethod(method, Private).Invoke(ui, values);
        public T Field<T>(string name) => (T)typeof(UIManager).GetField(name, Private).GetValue(ui);
        public static void Set(object instance, string name, object value) =>
            instance.GetType().GetField(name, Private).SetValue(instance, value);
        public void Dispose()
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(viewport);
        }
    }
}
