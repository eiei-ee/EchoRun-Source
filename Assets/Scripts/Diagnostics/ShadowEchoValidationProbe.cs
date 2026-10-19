#if UNITY_EDITOR || (DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN)
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Explicit, isolated QA only. Menu identity fixtures are not completed player runs.
public sealed class ShadowEchoValidationProbe : MonoBehaviour
{
    private const string ArgumentPrefix = "-echo-shadow-validation=";
    private const string ProductPrefix = "EchoRun-ShadowEcho-20261008-";
    private string scenario;
    private string directory;
    private readonly StringBuilder report = new StringBuilder();
    private bool finished;
    private const string PairedScenario = "visual-paired";
    private bool pairedQuitRequested;
    private readonly StringBuilder pairedSamples = new StringBuilder();
    private float pairedSampleAt, pairedSpan, longestPairedSpan;
    private float previousPairedSample = -1f;
    private bool previousPairedCandidate;
    private int pairedSampleCount, pairedCaptureCount;
    private const string VisualFramesArgument = "-echo-shadow-visual-frames";
    private bool recordVisualFrames;
    private MotionWindow[] motionWindows;
    private int motionFrameCount, lastMotionEngineFrame = -1;

    [Serializable]
    private sealed class MotionWindow
    {
        public string name, directory, axis, status = "pending";
        public float minimum, maximum;
        public int frameCount, firstEngineFrame = -1, lastEngineFrame = -1;
        public float enteredRunSeconds = -1f, exitedRunSeconds = -1f;
        public float enteredDistanceMeters = -1f, exitedDistanceMeters = -1f;
        public float firstRunSeconds = -1f, lastRunSeconds = -1f;
        public float firstRealtimeSeconds = -1f, lastRealtimeSeconds = -1f;
        [NonSerialized] public StreamWriter csv;
    }

    [Serializable]
    private sealed class MotionManifest
    {
        public string protocol = "shadow-echo-real-frames-v1";
        public string result;
        public int totalFrames, maximumFramesPerWindow = 400, maximumTotalFrames = 1000;
        public float maximumCaptureRate = 30f;
        public bool allWindowsCompleted;
        public MotionWindow[] windows;
        public string evidence = "Actual LateUpdate PNG/JSON pairs. Limited by real GameManager.RunElapsed to at most 30 captures per game second; actual cadence may be lower. No fixed-time advance, duplicate, interpolation or captureFramerate. Use each JSON gameTimeSeconds or frames.csv for variable-frame-rate playback. Identity fixture and obstacle collisions disabled; not performance or human acceptance. Window names are capture targets, not proof of shadow/readability coverage.";
    }

    [Serializable]
    private sealed class VisualSample
    {
        public string label, state;
        public int frame, playerLane, ghostDecisionLane, gateId, gateNumber, strikes;
        public float realtimeSeconds, gameTimeSeconds, speedMetersPerSecond, distanceMeters;
        public float playerLeadMeters, ghostDisplayedLane, ghostDisplayedGapMeters;
        public float ghostRootForwardOffsetMeters, ghostRootLateralOffsetMeters;
        public Vector3 playerRootPosition, ghostRootPosition;
        public bool ghostActive, geometricallyNearAndSeparate;
        public string sources = "GameManager: RunElapsed/CurrentSpeed/Distance/CollisionStrikes; PlayerController: CurrentLane/transform/ForwardDirection; AIShadowRunner: PlayerLead, read-only _ghost/_ghostLane/_displayedGhostLane/_displayedGap; SingleContractRuntime.ActiveGateId. Root offsets project world displacement on player forward/right; not route distance on turns. Geometry flag is not visual or human acceptance.";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateWhenRequested()
    {
        string requested = null;
        string[] arguments = Environment.GetCommandLineArgs();
        foreach (string argument in arguments)
            if (argument.StartsWith(ArgumentPrefix, StringComparison.Ordinal))
                requested = argument.Substring(ArgumentPrefix.Length);
        bool wantsVisualFrames = Array.IndexOf(arguments, VisualFramesArgument) >= 0;
        if (wantsVisualFrames && requested != PairedScenario)
        {
            Debug.LogError("SHADOW_ECHO_VISUAL_REFUSED: frame recording requires visual-paired.");
            if (!Application.isEditor) Application.Quit(2);
            return;
        }
        if (requested == null) return;
        if (requested == PairedScenario)
        {
            string error = ValidatePairedArguments(arguments);
            if (error != null)
            {
                Debug.LogError("SHADOW_ECHO_VISUAL_REFUSED: " + error);
                if (!Application.isEditor) Application.Quit(2);
                return;
            }
        }
        if (!Application.productName.StartsWith(ProductPrefix, StringComparison.Ordinal))
        {
            Debug.LogError("SHADOW_ECHO_VALIDATION_REFUSED: requires an isolated review product.");
            return;
        }
        if (requested != "menu-none" && requested != "menu-insufficient"
            && requested != "menu-ready" && requested != "observe"
            && requested != "challenge" && requested != "report-ready"
            && requested != PairedScenario && !IsResultFixture(requested))
        {
            Debug.LogError("SHADOW_ECHO_VALIDATION_REFUSED: unknown scenario.");
            return;
        }
        var host = new GameObject("ShadowEchoValidationProbe_ExplicitQA");
        DontDestroyOnLoad(host);
        ShadowEchoValidationProbe probe = host.AddComponent<ShadowEchoValidationProbe>();
        probe.scenario = requested;
        probe.recordVisualFrames = wantsVisualFrames;
        string playerDirectory = Path.GetDirectoryName(Application.dataPath);
        probe.directory = Path.Combine(playerDirectory, "ShadowEchoCaptures",
            requested + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)
            + (requested == PairedScenario ? "-" + Guid.NewGuid().ToString("N").Substring(0, 8) : ""));
        Directory.CreateDirectory(probe.directory);
        probe.report.AppendLine("Explicit QA in isolated product: " + Application.productName);
        probe.report.AppendLine("Scenario: " + requested);
        probe.report.AppendLine("Identity state is a labelled fixture, not evidence of human play or real victory.");
        if (IsResultFixture(requested))
        {
            probe.report.AppendLine("RESULT LAYOUT FIXTURE ONLY: injected presentation facts, no run, victory, GameOver event or settlement save.");
            probe.report.AppendLine("GameManager remains in Menu; the normal UI renderer is invoked directly. Saved identity must remain unchanged.");
        }
        else if (requested == PairedScenario)
        {
            probe.report.AppendLine("VISUAL IDENTITY FIXTURE: pace matches current balance; normal run/input/pose rules remain in force.");
            probe.report.AppendLine("COLLISIONS OFF: required StackedCityRuntimeReview disables obstacle colliders. Not collision, performance, learned-identity or human acceptance.");
            Application.wantsToQuit += probe.OnPairedQuitRequested;
        }
        else
        {
            probe.report.AppendLine("The subsequent run uses actual StartGame and natural collision/finish callbacks.");
            probe.report.AppendLine("No distance, lead, collision, finish, or result fields are overridden. Not a performance benchmark.");
        }
        try
        {
            if (!IsResultFixture(requested)) probe.PrepareIsolatedIdentity();
            bool low = Array.IndexOf(arguments, "-echo-shadow-low-quality") >= 0;
            bool accessible = Array.IndexOf(arguments, "-echo-shadow-accessible") >= 0;
            VisualQualityController.SetQuality(low ? VisualQuality.Low : VisualQuality.High);
            EchoRunAccessibility.SetLargeText(accessible);
            EchoRunAccessibility.SetHighContrast(accessible);
            EchoRunAccessibility.SetReducedMotion(accessible);
            probe.report.AppendLine("QA quality=" + VisualQualityController.Current
                + "; largeText/highContrast/reducedMotion=" + accessible);
        }
        catch (Exception exception)
        {
            probe.Finish("FAILED during fixture setup: " + exception.Message);
            Debug.LogException(exception);
        }
    }

    private static string ValidatePairedArguments(string[] arguments)
    {
        if (Application.isEditor || !Debug.isDebugBuild
            || Application.platform != RuntimePlatform.WindowsPlayer
            || !Application.productName.StartsWith(ProductPrefix, StringComparison.Ordinal))
            return "visual-paired requires an isolated Windows Development player.";
        foreach (string required in new[] { "-echo-stacked-city-review", "-echo-finish-gate-review",
                     SingleContractValidationLaunchOptions.EnableArgument })
            if (Array.IndexOf(arguments, required) < 0) return "Missing required companion: " + required;
        foreach (string argument in arguments)
            if (argument == SingleContractValidationLaunchOptions.AutoStartArgument
                || argument == SingleContractValidationLaunchOptions.FixedIdentityArgument
                || argument == "-echo-stacked-city-side-lanes" || argument == "-echo-stacked-city-reload-review"
                || argument == "-echo-orange-art-review" || argument == "-echo-runner-art-review"
                || argument == "-echo-camera-polish-review" || argument == "-echo-shadow-low-quality"
                || argument.StartsWith("-echo-shadow-replay", StringComparison.Ordinal)
                || argument.StartsWith(EchoVisualCaptureProbe.CaptureDistancesArgumentPrefix, StringComparison.Ordinal))
                return "Conflicting diagnostic argument: " + argument;
        return null;
    }

    private bool OnPairedQuitRequested()
    {
        // The companion requests quit in LateUpdate at the natural finish.
        // Defer that request one frame so this probe can persist its final evidence.
        if (finished) return true;
        pairedQuitRequested = true;
        return false;
    }

    private static bool IsResultFixture(string value)
    {
        return value == "result-ready" || value == "result-insufficient"
            || value == "result-no-generation" || value == "result-save-failed";
    }

    private void PrepareIsolatedIdentity()
    {
        // Public reset is restricted to the explicitly named review product above.
        if (!EchoRunSaveSystem.CommitTrainingReset())
            throw new InvalidOperationException("Isolated QA training reset failed.");
        if (scenario == "menu-none" || scenario == "observe")
        {
            report.AppendLine("Seeded identity: none (QA training reset).");
            return;
        }
        int sequence = EchoRunSaveSystem.ReserveRunSequence();
        ActiveEchoIdentity identity = SingleContractValidationIdentity.Create();
        identity.sourceRunSequence = sequence;
        if (scenario == PairedScenario)
        {
            GameplayBalance balance = GameBalanceConfig.Current.gameplay;
            identity.sourceCourseDuration = SingleContractFlow.CalibrationDurationSeconds;
            identity.pace = EchoTimeRules.DistanceForAcceleratingRun(balance.startSpeed,
                balance.maxSpeed, balance.speedIncreaseRate, identity.sourceCourseDuration)
                / identity.sourceCourseDuration;
            report.AppendLine("Fixture pace=" + identity.pace.ToString("R", CultureInfo.InvariantCulture)
                + "; sourceDuration=" + identity.sourceCourseDuration
                + "; GameBalanceConfig start/max/acceleration=" + balance.startSpeed + "/"
                + balance.maxSpeed + "/" + balance.speedIncreaseRate);
        }
        if (scenario == "menu-insufficient")
        {
            identity.memoryContract.confidence = 0.4f;
            identity.memoryContract.evidenceCount = 2;
        }
        identity.identityId = ActiveEchoIdentity.CreateIdentityId(identity);
        identity.memoryContract.identityId = identity.identityId;
        identity.Normalize();
        SaveCommitResult saved = EchoRunSaveSystem.TryCommitSingleContractSettlement(
            new RunSettlementCommit
            {
                transactionId = "shadow-echo-qa-fixture-" + sequence,
                runSequence = sequence,
                endReason = RunEndReason.FinishReached,
                hasActiveOpponent = false,
                calibrationCompleted = true,
                playerWon = false,
                playerLead = 0f,
                resultMessage = "QA identity fixture only; no real run or victory occurred.",
                promotedIdentity = identity
            });
        if (!saved.succeeded)
            throw new InvalidOperationException("QA identity failed the normal save validator: " + saved.error);
        report.AppendLine("Seeded identity through normal validated save transaction: generation="
            + saved.activeIdentity.generation + ", requiresRouteCalibration="
            + saved.activeIdentity.RequiresRouteCalibration);
    }

    private IEnumerator Start()
    {
        if (finished) yield break;
        float deadline = Time.realtimeSinceStartup + 20f;
        while ((GameManager.Instance == null || Camera.main == null
                || FindObjectOfType<UIManager>() == null) && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return new WaitForSecondsRealtime(2f);
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Menu)
        {
            Finish("FAILED: expected a normal menu; do not combine this probe with autostart.");
            yield break;
        }
        if (scenario == PairedScenario)
        {
            IEnumerator paired = RunPairedVisual(gm);
            ShadowEchoVisualLateTick tick = gameObject.AddComponent<ShadowEchoVisualLateTick>();
            tick.onLateFrame = () =>
            {
                if (finished) return;
                try
                {
                    if (!paired.MoveNext() && !finished)
                        Finish("FAILED: visual evidence iterator ended without a result.");
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    Finish("FAILED: visual evidence capture: " + exception.Message);
                }
            };
            yield break;
        }
        if (IsResultFixture(scenario))
        {
            string identityBefore = JsonUtility.ToJson(EchoRunSaveSystem.GetActiveEchoIdentity());
            try
            {
                RenderResultLayoutFixture();
            }
            catch (Exception exception)
            {
                Finish("FAILED during result layout fixture: " + exception.Message);
                Debug.LogException(exception);
            }
            if (finished) yield break;
            yield return null;
            yield return new WaitForSecondsRealtime(0.25f);
            Capture("QA-LAYOUT-" + scenario);
            string identityAfter = JsonUtility.ToJson(EchoRunSaveSystem.GetActiveEchoIdentity());
            bool unchanged = identityBefore == identityAfter;
            report.AppendLine("Saved identity unchanged=" + unchanged + "; GameManager remains=" + gm.State);
            Finish(unchanged && gm.State == GameState.Menu
                ? "RESULT LAYOUT FIXTURE captured; no actual run, win, or settlement save."
                : "FAILED: result layout fixture changed persistent identity or game state.");
            yield break;
        }
        Capture("menu");
        if (scenario == "report-ready")
        {
            UIManager ui = FindObjectOfType<UIManager>();
            Button button = ui != null ? GetPrivateField(ui, "_reportBtn") as Button : null;
            if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
            {
                Finish("FAILED: saved report action is unavailable for the ready identity.");
                yield break;
            }
            button.onClick.Invoke();
            yield return null;
            yield return new WaitForSecondsRealtime(0.25f);
            Capture("saved-report-action");
            Finish("Saved report captured through the actual menu button callback; no run started.");
            yield break;
        }
        if (scenario.StartsWith("menu-", StringComparison.Ordinal))
        {
            Finish("Menu fixture captured through normal loaded identity and UI.");
            yield break;
        }
        var validation = new SingleContractValidationConfig
        {
            enabled = true, fixedSeed = 1337, freezeDirector = true,
            disablePowerUps = true, forceStandardDifficulty = true,
            useFixedIdentity = false, autoStart = false
        };
        if (!gm.TryConfigureGameplayFlow(GameplayFlowMode.SingleContract, validation)
            || !gm.TryStartConfiguredRun(out _))
        {
            Finish("FAILED: normal run start was rejected.");
            yield break;
        }
        report.AppendLine("Run started normally; seed=1337; director frozen; collisions remain enabled; no automated input.");
        float began = Time.realtimeSinceStartup;
        float[] captureTimes = { 0.5f, 2f, 5f, 10f, 20f };
        int timedCapture = 0;
        int previousStrikes = 0;
        bool recoveredCaptured = false;
        int lastGate = -1;
        while (Time.realtimeSinceStartup - began < 160f)
        {
            if (gm.State == GameState.GameOver)
            {
                yield return new WaitForSecondsRealtime(0.75f);
                Capture("natural-result-" + gm.LastEndReason);
                Finish("Actual run ended: " + gm.LastEndReason + "; no victory/result injection.");
                yield break;
            }
            if (gm.State == GameState.Playing)
            {
                float elapsed = Time.realtimeSinceStartup - began;
                if (timedCapture < captureTimes.Length && elapsed >= captureTimes[timedCapture])
                    Capture("run-" + captureTimes[timedCapture++].ToString("0.0", CultureInfo.InvariantCulture) + "s");
                if (gm.CollisionStrikes != previousStrikes)
                {
                    previousStrikes = gm.CollisionStrikes;
                    Capture("injury-" + previousStrikes);
                }
                if (previousStrikes > 0 && !recoveredCaptured && gm.CollisionRecoveryTimeRemaining <= 0f)
                {
                    recoveredCaptured = true;
                    report.AppendLine("Recovery ended at frame=" + Time.frameCount + "; capture deferred by one frame.");
                    yield return null;
                    if (gm.State == GameState.Playing) Capture("injury-after-recovery");
                }
                AIShadowRunner shadow = AIShadowRunner.Instance;
                if (shadow != null && shadow.IsCurrentSingleContractPredictionGateActive
                    && shadow.CurrentSingleContractPredictionGateNumber != lastGate)
                {
                    lastGate = shadow.CurrentSingleContractPredictionGateNumber;
                    Capture("active-gate-" + lastGate);
                }
            }
            yield return null;
        }
        Finish("INCOMPLETE: capture window ended before a natural result.");
    }

    private IEnumerator RunPairedVisual(GameManager gm)
    {
        if (FindObjectOfType<StackedCityRuntimeReview>() == null)
            throw new InvalidOperationException("Required collision-disabled visual companion is absent.");
        ActiveEchoIdentity identity = EchoRunSaveSystem.GetActiveEchoIdentity();
        if (identity == null || identity.RequiresRouteCalibration
            || Mathf.Abs(AIShadowRunner.CalculateSingleContractGhostPaceScale(identity.pace,
                identity.sourceCourseDuration, gm.startSpeed, gm.maxSpeed, gm.speedIncreaseRate) - 1f) > .0001f)
            throw new InvalidOperationException("Fixture pace does not match actual GameManager balance.");
        AddFixtureLabel("QA IDENTITY FIXTURE | COLLISIONS OFF | NOT PERFORMANCE / HUMAN ACCEPTANCE");
        if (recordVisualFrames) InitializeMotionWindows();
        Capture("menu");
        var validation = new SingleContractValidationConfig
        {
            enabled = true, fixedSeed = 1337, freezeDirector = true, disablePowerUps = true,
            forceStandardDifficulty = true, useFixedIdentity = false, autoStart = false
        };
        if (!gm.TryConfigureGameplayFlow(GameplayFlowMode.SingleContract, validation)
            || !gm.TryStartConfiguredRun(out _))
            throw new InvalidOperationException("Normal run start rejected.");
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player == null || InputManager.Instance == null || AIShadowRunner.Instance == null
            || !AIShadowRunner.Instance.HasActiveOpponent)
            throw new InvalidOperationException("Normal challenge services or opponent missing.");
        report.AppendLine("Normal challenge started with saved fixture, seed=1337; companion owns collider disabling. Player/ghost position and speed are never assigned by this probe.");
        float began = Time.realtimeSinceStartup;
        float[] times = { .5f, 2f, 4f, 6f, 10f };
        float[] distances = { 70f, 110f, 330f, 350f, 490f, 500f, 510f };
        int timeIndex = 0, distanceIndex = 0, lastGate = -1;
        pairedSamples.AppendLine("frame,run_seconds,speed_mps,distance_m,gate_id,player_lane,ghost_displayed_lane,player_lead_m,ghost_displayed_gap_m,root_forward_offset_m,root_lateral_offset_m,geometry_candidate");
        while (Time.realtimeSinceStartup - began < 210f)
        {
            // Advanced once per LateUpdate after runner pose/camera/companion.
            // Unlike WaitForEndOfFrame this also works for offscreen batch players.
            yield return null;
            if (gm.State == GameState.GameOver)
            {
                Capture("natural-result-" + gm.LastEndReason);
                Finish(gm.LastEndReason == RunEndReason.FinishReached && gm.RemainingDistance <= .1f
                    ? "VISUAL_CAPTURE_COMPLETE: natural finish; coverage still requires inspecting images/recording."
                    : "FAILED: visual route ended before its natural finish.");
                yield break;
            }
            if (pairedQuitRequested || gm.State != GameState.Playing)
                throw new InvalidOperationException("Companion quit early or run left Playing.");
            // A small deterministic input schedule, never a lane/pose setter.
            // Keep an adjacent lane through the opening, then use centre later.
            int targetLane = gm.Distance < 100f ? 0 : 1;
            InputManager input = InputManager.Instance;
            if (input.PendingInputCount == 0 && player.CurrentLane != targetLane)
            {
                input.QueueSwipe(targetLane < player.CurrentLane ? SwipeDirection.Left : SwipeDirection.Right,
                    InputIntentSource.Replay, Time.unscaledTime);
                report.AppendLine("Queued normal lane input: run=" + gm.RunElapsed.ToString("R", CultureInfo.InvariantCulture)
                    + "; current=" + player.CurrentLane + "; target=" + targetLane);
            }
            if (gm.RunElapsed >= pairedSampleAt)
            {
                pairedSampleAt = gm.RunElapsed + .1f;
                VisualSample sample = ReadVisualSample("sample");
                float interval = previousPairedSample < 0f ? 0f : sample.gameTimeSeconds - previousPairedSample;
                pairedSpan = sample.geometricallyNearAndSeparate && previousPairedCandidate
                    && interval <= .25f ? pairedSpan + interval : 0f;
                longestPairedSpan = Mathf.Max(longestPairedSpan, pairedSpan);
                previousPairedSample = sample.gameTimeSeconds;
                previousPairedCandidate = sample.geometricallyNearAndSeparate;
                pairedSampleCount++;
                pairedSamples.AppendLine(string.Join(",", sample.frame, F(sample.gameTimeSeconds),
                    F(sample.speedMetersPerSecond), F(sample.distanceMeters), sample.gateId, sample.playerLane,
                    F(sample.ghostDisplayedLane), F(sample.playerLeadMeters), F(sample.ghostDisplayedGapMeters),
                    F(sample.ghostRootForwardOffsetMeters), F(sample.ghostRootLateralOffsetMeters),
                    sample.geometricallyNearAndSeparate ? 1 : 0));
            }
            if (recordVisualFrames)
            {
                CaptureMotionFrame(gm);
                continue; // No duplicate still capture from this probe in the same LateUpdate.
            }
            if (timeIndex < times.Length && gm.RunElapsed >= times[timeIndex])
                Capture("opening-" + F(times[timeIndex++]) + "s");
            if (distanceIndex < distances.Length && gm.Distance >= distances[distanceIndex])
                Capture("route-" + F(distances[distanceIndex++]) + "m");
            AIShadowRunner shadow = AIShadowRunner.Instance;
            if (shadow.IsCurrentSingleContractPredictionGateActive
                && shadow.CurrentSingleContractPredictionGateNumber != lastGate)
            {
                lastGate = shadow.CurrentSingleContractPredictionGateNumber;
                Capture("active-gate-" + lastGate);
            }
        }
        Finish("FAILED: visual route timeout before natural finish.");
    }

    private void InitializeMotionWindows()
    {
        motionWindows = new[]
        {
            new MotionWindow { name = "opening", axis = "RunElapsed", minimum = 1f, maximum = 7f },
            new MotionWindow { name = "shadow-candidate", axis = "Distance", minimum = 430f, maximum = 540f },
            new MotionWindow { name = "high-speed", axis = "RunElapsed", minimum = 80f, maximum = 86f }
        };
        foreach (MotionWindow window in motionWindows)
        {
            window.directory = Path.Combine(directory, "motion", window.name);
            if (Directory.Exists(window.directory))
                throw new IOException("Motion output directory already exists: " + window.directory);
            Directory.CreateDirectory(window.directory);
        }
        report.AppendLine("Optional real-frame recording enabled; at most 30 captures per game second, actual cadence retained; 400 frames/window, 1000 total. PNG candidates require visual inspection.");
    }

    private void CaptureMotionFrame(GameManager gm)
    {
        foreach (MotionWindow window in motionWindows)
        {
            float value = window.axis == "Distance" ? gm.Distance : gm.RunElapsed;
            if (value >= window.maximum && (window.status == "pending" || window.status == "recording"))
            {
                window.exitedRunSeconds = gm.RunElapsed;
                window.exitedDistanceMeters = gm.Distance;
                window.status = window.frameCount > 0 ? "complete" : "missed";
                window.csv?.Dispose();
                window.csv = null;
                Debug.Log("SHADOW_ECHO_VISUAL_WINDOW " + window.name + " " + window.status
                    + " frames=" + window.frameCount);
            }
            if (value < window.minimum || value >= window.maximum) continue;
            if (window.status == "pending")
            {
                window.status = "recording";
                window.enteredRunSeconds = gm.RunElapsed;
                window.enteredDistanceMeters = gm.Distance;
                window.csv = new StreamWriter(new FileStream(Path.Combine(window.directory, "frames.csv"),
                    FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
                window.csv.AutoFlush = true;
                window.csv.WriteLine("index,engine_frame,game_time_seconds,realtime_seconds,delta_game_seconds,delta_realtime_seconds,speed_mps,distance_m,gate_id,player_lane,ghost_displayed_lane,player_lead_m,png,json");
            }
            if (lastMotionEngineFrame == Time.frameCount
                || (window.frameCount > 0 && (double)gm.RunElapsed - window.lastRunSeconds < 1.0 / 30.0))
                return;
            if (window.frameCount >= 400 || motionFrameCount >= 1000)
                throw new InvalidOperationException("Real-frame recording hard limit reached; refusing truncation or duplicate frames.");
            VisualSample sample = ReadVisualSample(window.name);
            if (sample.frame <= lastMotionEngineFrame || float.IsNaN(sample.gameTimeSeconds)
                || float.IsInfinity(sample.gameTimeSeconds)
                || (window.frameCount > 0 && (sample.gameTimeSeconds <= window.lastRunSeconds
                    || sample.realtimeSeconds <= window.lastRealtimeSeconds)))
                throw new InvalidOperationException("Non-increasing real frame/time in motion recording.");
            int index = window.frameCount + 1;
            string stem = "frame-" + index.ToString("D6", CultureInfo.InvariantCulture);
            string png = stem + ".png", json = stem + ".json";
            string pngPath = Path.Combine(window.directory, png);
            if (File.Exists(pngPath) || File.Exists(Path.Combine(window.directory, json)))
                throw new IOException("Refusing to overwrite recorded frame: " + pngPath);
            // Snapshot immediately before rendering within the same LateUpdate.
            // Offscreen rendering does not advance the simulation clock.
            EchoVisualCaptureProbe.CaptureOffscreen(pngPath);
            File.WriteAllText(Path.Combine(window.directory, json), JsonUtility.ToJson(sample, true), Encoding.UTF8);
            window.csv.WriteLine(string.Join(",", index, sample.frame, F(sample.gameTimeSeconds),
                F(sample.realtimeSeconds), window.frameCount == 0 ? "" : F(sample.gameTimeSeconds - window.lastRunSeconds),
                window.frameCount == 0 ? "" : F(sample.realtimeSeconds - window.lastRealtimeSeconds),
                F(sample.speedMetersPerSecond), F(sample.distanceMeters), sample.gateId,
                sample.playerLane, F(sample.ghostDisplayedLane), F(sample.playerLeadMeters), png, json));
            if (window.frameCount == 0)
            {
                window.firstEngineFrame = sample.frame;
                window.firstRunSeconds = sample.gameTimeSeconds;
                window.firstRealtimeSeconds = sample.realtimeSeconds;
            }
            window.lastEngineFrame = lastMotionEngineFrame = sample.frame;
            window.lastRunSeconds = sample.gameTimeSeconds;
            window.lastRealtimeSeconds = sample.realtimeSeconds;
            window.frameCount++;
            motionFrameCount++;
            return;
        }
    }

    private bool CloseMotionStreams()
    {
        bool succeeded = true;
        if (motionWindows == null) return true;
        foreach (MotionWindow window in motionWindows)
        {
            try { window.csv?.Dispose(); }
            catch (Exception exception)
            {
                succeeded = false;
                Debug.LogError("SHADOW_ECHO_VISUAL_STREAM_CLOSE_FAILED " + exception.Message);
            }
            finally { window.csv = null; }
        }
        return succeeded;
    }

    private string FinalizeMotionEvidence(string result)
    {
        bool complete = motionWindows != null;
        if (motionWindows != null)
            foreach (MotionWindow window in motionWindows)
            {
                if (window.status != "complete")
                {
                    complete = false;
                    window.status = window.frameCount > 0 ? "incomplete" : "missed";
                }
            }
        if (!CloseMotionStreams()) result = "FAILED: motion evidence stream could not close.";
        if (!complete && result.StartsWith("VISUAL_CAPTURE_COMPLETE:", StringComparison.Ordinal))
            result = "FAILED: natural finish reached but one or more motion windows are incomplete.";
        File.WriteAllText(Path.Combine(directory, "motion-manifest.json"), JsonUtility.ToJson(new MotionManifest
        {
            result = result, totalFrames = motionFrameCount, allWindowsCompleted = complete, windows = motionWindows
        }, true), Encoding.UTF8);
        return result;
    }

    private void OnDestroy()
    {
        if (recordVisualFrames) CloseMotionStreams();
    }

    private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private VisualSample ReadVisualSample(string label)
    {
        GameManager gm = GameManager.Instance;
        AIShadowRunner shadow = AIShadowRunner.Instance;
        PlayerController player = FindObjectOfType<PlayerController>();
        if (gm == null || shadow == null || player == null)
            throw new InvalidOperationException("Missing services for an actual visual sample.");
        GameObject ghost = GetPrivateField(shadow, "_ghost") as GameObject;
        var sample = new VisualSample
        {
            label = label, state = gm.State.ToString(), frame = Time.frameCount,
            realtimeSeconds = Time.realtimeSinceStartup, gameTimeSeconds = gm.RunElapsed,
            speedMetersPerSecond = gm.CurrentSpeed, distanceMeters = gm.Distance,
            strikes = gm.CollisionStrikes, playerLane = player.CurrentLane,
            playerRootPosition = player.transform.position, playerLeadMeters = shadow.PlayerLead,
            gateId = shadow.SingleContractRuntime != null ? shadow.SingleContractRuntime.ActiveGateId : -1,
            gateNumber = shadow.CurrentSingleContractPredictionGateNumber,
            ghostDecisionLane = (int)GetPrivateField(shadow, "_ghostLane"),
            ghostDisplayedLane = (float)GetPrivateField(shadow, "_displayedGhostLane"),
            ghostDisplayedGapMeters = (float)GetPrivateField(shadow, "_displayedGap"),
            ghostActive = ghost != null && ghost.activeInHierarchy
        };
        if (ghost != null)
        {
            sample.ghostRootPosition = ghost.transform.position;
            Vector3 delta = sample.ghostRootPosition - sample.playerRootPosition;
            sample.ghostRootForwardOffsetMeters = Vector3.Dot(delta, player.ForwardDirection.normalized);
            sample.ghostRootLateralOffsetMeters = Vector3.Dot(delta,
                Vector3.Cross(Vector3.up, player.ForwardDirection).normalized);
        }
        sample.geometricallyNearAndSeparate = sample.ghostActive && gm.State == GameState.Playing
            && Mathf.Abs(sample.playerLeadMeters) <= 1f && Mathf.Abs(sample.ghostDisplayedGapMeters) <= 1f
            && Mathf.Abs(sample.ghostRootForwardOffsetMeters) <= 1f
            && Mathf.Abs(sample.ghostDisplayedLane - sample.playerLane) >= .8f;
        return sample;
    }

    private void RenderResultLayoutFixture()
    {
        AIShadowRunner shadow = AIShadowRunner.Instance;
        UIManager ui = FindObjectOfType<UIManager>();
        if (shadow == null || ui == null
            || (bool)GetPrivateField(shadow, "_runStarted"))
            throw new InvalidOperationException("A result layout fixture requires an idle, unstarted menu.");

        var facts = new SingleContractResultFacts
        {
            available = true, endReason = RunEndReason.FinishReached,
            wasChallenge = true, won = true, promotionBuilt = true,
            commitAttempted = true, commitSucceeded = true, identityPromoted = true,
            generationBefore = 3, savedGeneration = 4, savedPreferredLane = 2,
            savedIdentityCanChallenge = scenario != "result-insufficient"
        };
        if (scenario == "result-no-generation")
        {
            facts.promotionBuilt = false;
            facts.commitAttempted = false;
            facts.commitSucceeded = false;
            facts.identityPromoted = false;
            facts.savedGeneration = 3;
        }
        else if (scenario == "result-save-failed")
        {
            facts.commitSucceeded = false;
            facts.identityPromoted = false;
            facts.savedGeneration = 3;
        }
        SingleContractResultViewData view = EchoRunPresentation.BuildSingleContractResultView(facts);
        // Mirror the existing presentation fixture. Do not emit GameManager state events
        // or start/finalize a run; this isolated process exits without a settlement.
        SetPrivateField(shadow, "_runFinalized", true);
        SetPrivateField(shadow, "<LastSingleContractResultFacts>k__BackingField", facts);
        SetPrivateField(shadow, "<LastRunWasChallenge>k__BackingField", true);
        SetPrivateField(shadow, "<LastRunWon>k__BackingField", true);
        SetPrivateField(shadow, "<LastSingleContractCommitSucceeded>k__BackingField", facts.commitSucceeded);
        SetPrivateField(shadow, "<LastSingleContractIdentityPromoted>k__BackingField", facts.identityPromoted);
        SetPrivateField(shadow, "<LastResult>k__BackingField", view.summary);
        MethodInfo render = typeof(UIManager).GetMethod("OnGameStateChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        if (render == null) throw new MissingMethodException("UIManager.OnGameStateChanged");
        render.Invoke(ui, new object[] { GameState.GameOver });
        AddFixtureLabel();
        report.AppendLine("Injected result kind=" + view.kind + "; action=" + view.primaryAction
            + "; nextAction=" + view.nextAction + "; facts=" + JsonUtility.ToJson(facts));
    }

    private static object GetPrivateField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(target.GetType().Name, name);
        return field.GetValue(target);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }

    private static void AddFixtureLabel(string visualText = null)
    {
        Text existing = FindObjectOfType<Text>();
        var host = new GameObject("QA_ResultLayoutFixture_Label", typeof(Canvas));
        Canvas canvas = host.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        var labelObject = new GameObject("ExplicitFixtureLabel", typeof(RectTransform), typeof(Image));
        labelObject.transform.SetParent(host.transform, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -8f);
        rect.sizeDelta = new Vector2(Mathf.Min(Screen.width - 16f, 650f), 48f);
        labelObject.GetComponent<Image>().color = new Color(0.08f, 0.02f, 0.02f, 0.95f);
        var textObject = new GameObject("LabelText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(labelObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        Text text = textObject.GetComponent<Text>();
        text.font = existing != null && existing.font != null
            ? existing.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = "QA RESULT LAYOUT FIXTURE\nNo real run, win or settlement save";
        text.raycastTarget = false;
        if (visualText != null)
        {
            text.text = visualText;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = 18;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 8f);
            rect.sizeDelta = new Vector2(Mathf.Max(1f, Screen.width - 16f), 40f);
            labelObject.GetComponent<Image>().raycastTarget = false;
        }
    }

    private void Capture(string label)
    {
        string path = Path.Combine(directory, label + "-" + Screen.width + "x" + Screen.height + ".png");
        EchoVisualCaptureProbe.CaptureOffscreen(path);
        if (scenario == PairedScenario)
        {
            File.WriteAllText(Path.ChangeExtension(path, ".json"),
                JsonUtility.ToJson(ReadVisualSample(label), true), Encoding.UTF8);
            pairedCaptureCount++;
        }
        GameManager gm = GameManager.Instance;
        AIShadowRunner shadow = AIShadowRunner.Instance;
        report.AppendLine(label + ": state=" + (gm != null ? gm.State.ToString() : "missing")
            + ", frame=" + Time.frameCount
            + ", distance=" + (gm != null ? gm.Distance.ToString("F3", CultureInfo.InvariantCulture) : "missing")
            + ", strikes=" + (gm != null ? gm.CollisionStrikes.ToString() : "missing")
            + ", recoverySeconds=" + (gm != null ? gm.CollisionRecoveryTimeRemaining.ToString("F3", CultureInfo.InvariantCulture) : "missing")
            + ", actualLead=" + (shadow != null ? shadow.PlayerLead.ToString("F3", CultureInfo.InvariantCulture) : "missing")
            + ", hasOpponent=" + (shadow != null && shadow.HasActiveOpponent));
        File.WriteAllText(Path.Combine(directory, "report.txt"), report.ToString(), Encoding.UTF8);
    }

    private void Finish(string result)
    {
        if (finished) return;
        finished = true;
        if (recordVisualFrames)
        {
            try { result = FinalizeMotionEvidence(result); }
            catch (Exception exception)
            {
                result = "FAILED: motion evidence finalization: " + exception.Message;
                Debug.LogError(result);
            }
            finally { CloseMotionStreams(); }
        }
        if (scenario == PairedScenario)
        {
            Application.wantsToQuit -= OnPairedQuitRequested;
            report.AppendLine("Read-only 10Hz geometric samples=" + pairedSampleCount
                + "; screenshot/JSON pairs=" + pairedCaptureCount
                + "; longest observed near/separate geometry seconds=" + F(longestPairedSpan)
                + ". Not a visual/human pass. Captures can interrupt the sampling cadence.");
            File.WriteAllText(Path.Combine(directory, "visual-samples.csv"), pairedSamples.ToString(), Encoding.UTF8);
        }
        report.AppendLine(result);
        File.WriteAllText(Path.Combine(directory, "report.txt"), report.ToString(), Encoding.UTF8);
        Debug.Log("SHADOW_ECHO_VALIDATION_FINISHED " + result + " directory=" + directory);
        Application.Quit(scenario == PairedScenario && !result.StartsWith("VISUAL_CAPTURE_COMPLETE:", StringComparison.Ordinal) ? 2 : 0);
    }
}

// Created only by visual-paired; it does not change old probe/production ordering.
[DefaultExecutionOrder(20000)]
internal sealed class ShadowEchoVisualLateTick : MonoBehaviour
{
    internal Action onLateFrame;
    private void LateUpdate() { onLateFrame?.Invoke(); }
}
#endif
