#if UNITY_EDITOR || (DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN)
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// Explicit Windows QA only. This never changes obstacle colliders, speed, pose,
// track generation, win rules or input lifetimes. A 40s window is not a victory.
public sealed class ShadowEchoReplayProbe : MonoBehaviour
{
    const string Prefix = "-echo-shadow-replay";
    const string ProductPrefix = "EchoRun-ShadowEcho-20261008-";
    const string Protocol = "shadow-echo-distance-replay-v1";
    const float WarmupSeconds = 10f;
    const float SampleSeconds = 30f;
    const float DistanceTolerance = 2f;
    const int MaxInputs = 512;
    const int MaxEvents = 256;
    const int MaxFrames = 40000;

    [Serializable]
    public struct InputStep
    {
        public float distance;
        public int direction;
        public int laneBefore;
        public int laneAfter;
        public float resolvedDistance;
        public bool resolved;
        public bool accepted;
        public int telemetrySequence;
        public int telemetryOutcome;
    }

    [Serializable]
    public struct RouteEvent
    {
        public float distance;
        public int first;
        public int second;
    }

    [Serializable]
    public struct GateEvent
    {
        public float distance;
        public int gateId;
        public int lane;
        public int role;
        public int execution;
        public int reason;
        public float speed;
        public float rewardMeters;
    }

    [Serializable]
    public sealed class RouteRecording
    {
        public string protocol = Protocol;
        public bool valid;
        public string failure;
        public int seed;
        public string identityHash;
        public string gatePlanHash;
        public string balanceHash;
        public float startSpeed;
        public float maxSpeed;
        public float acceleration;
        public float fixedDeltaTime;
        public float jumpDuration;
        public float slideDuration;
        public float laneSwitchSpeed;
        public float windowElapsed;
        public float endDistance;
        public float endLead;
        public int endLane;
        public InputStep[] inputs;
        public RouteEvent[] turns;
        public RouteEvent[] injuries;
        public GateEvent[] gates;
    }

    [Serializable]
    public sealed class RunManifest
    {
        public string protocol = Protocol;
        public string intervalMeaning = "Time.unscaledDeltaTime application frame interval; not GPU time or displayed-present interval";
        public string scope = "Fixed identity/seed, frozen director, normal collisions; finite QA window, not a completed course or human playtest";
        public string mode;
        public string label;
        public string product;
        public string engine;
        public string backend;
        public string graphicsDevice;
        public string graphicsApi;
        public string operatingSystem;
        public string processor;
        public string startedUtc;
        public string routePath;
        public string routeSha256;
        public string framesSha256;
        public string failure;
        public bool valid;
        public bool performanceEligible;
        public bool aliveAtEnd;
        public bool focusedAtEnd;
        public bool developmentBuild;
        public string visualQuality;
        public int qualityLevel;
        public int width;
        public int height;
        public int targetFrameRate;
        public int vSyncCount;
        public string colorSpace;
        public bool largeText;
        public bool highContrast;
        public bool reducedMotion;
        public float fixedDeltaTime;
        public float maximumDeltaTime;
        public int seed;
        public float warmupSeconds = WarmupSeconds;
        public float requestedSampleSeconds = SampleSeconds;
        public float sampleStartRunSeconds;
        public float sampleEndRunSeconds;
        public float capturedSeconds;
        public float windowElapsed;
        public float endDistance;
        public float endLead;
        public int strikes;
        public int frames;
        public int inputs;
        public int turns;
        public int gates;
        public double meanFrameMs;
        public double onePercentLowFps;
        public string normalEndReason;
    }

    struct FrameSample
    {
        public float elapsed;
        public float distance;
        public float milliseconds;
        public float lead;
        public int lane;
        public int strikes;
    }

    readonly InputStep[] inputs = new InputStep[MaxInputs];
    readonly RouteEvent[] turns = new RouteEvent[MaxEvents];
    readonly RouteEvent[] injuries = new RouteEvent[MaxEvents];
    readonly GateEvent[] gates = new GateEvent[MaxEvents];
    readonly FrameSample[] frames = new FrameSample[MaxFrames];
    GameManager manager;
    PlayerController player;
    TrackManager track;
    InputManager input;
    AIShadowRunner shadow;
    RunManifest manifest;
    RouteRecording expected;
    RouteRecording observed;
    string directory;
    string mode;
    string routePath;
    string label;
    string failure;
    string expectedRouteSha;
    int seed = 1337;
    int requestedFps = 60;
    VisualQuality requestedQuality = VisualQuality.High;
    int inputCount, turnCount, injuryCount, gateCount, frameCount;
    int replayIndex;
    int pendingInput = -1;
    float pendingSince;
    float beganAt;
    float sampleStarted = -1f;
    float nextDecision;
    int lastForwardX, lastForwardZ, lastStrikes;
    bool running, finished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateWhenRequested()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        bool requested = Array.Exists(arguments, value => value.StartsWith(Prefix + "=", StringComparison.Ordinal));
        if (!requested) return;
        if (Application.isEditor || !Debug.isDebugBuild
            || Application.platform != RuntimePlatform.WindowsPlayer
            || !Application.productName.StartsWith(ProductPrefix, StringComparison.Ordinal))
        {
            Debug.LogError("SHADOW_ECHO_REPLAY_REFUSED: requires an isolated Windows Development player.");
            return;
        }
        var host = new GameObject("ShadowEchoReplayProbe_ExplicitQA");
        DontDestroyOnLoad(host);
        var probe = host.AddComponent<ShadowEchoReplayProbe>();
        probe.directory = Path.Combine(Path.GetDirectoryName(Application.dataPath),
            "ShadowEchoPerformance", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)
            + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            Directory.CreateDirectory(probe.directory);
            probe.mode = Argument(arguments, Prefix + "=");
            probe.Configure(arguments);
        }
        catch (Exception exception)
        {
            probe.Invalidate("Setup failed: " + exception.Message);
            probe.Finish(false);
        }
    }

    static string Argument(string[] arguments, string name)
    {
        string result = null;
        foreach (string argument in arguments)
        {
            if (!argument.StartsWith(name, StringComparison.Ordinal)) continue;
            if (result != null) throw new ArgumentException("Duplicate argument: " + name);
            result = argument.Substring(name.Length);
        }
        return result;
    }

    void Configure(string[] arguments)
    {
        if (mode != "record" && mode != "replay") throw new ArgumentException("Mode must be record or replay.");
        foreach (string argument in arguments)
        {
            if ((argument.StartsWith("-echo-", StringComparison.Ordinal)
                 && !argument.StartsWith(Prefix, StringComparison.Ordinal))
                || argument.StartsWith("-profiler-", StringComparison.Ordinal)
                || argument == "-batchmode" || argument == "-nographics")
                throw new ArgumentException("Do not combine the replay probe with another QA/capture/profiler or headless mode.");
        }
        label = Argument(arguments, Prefix + "-label=") ?? Application.productName;
        if (label.Length > 160) throw new ArgumentException("Label is too long.");
        string seedText = Argument(arguments, Prefix + "-seed=");
        if (seedText != null && (!int.TryParse(seedText, out seed) || seed <= 0))
            throw new ArgumentException("Seed must be a positive integer.");
        string fpsText = Argument(arguments, Prefix + "-fps=");
        if (fpsText != null && (!int.TryParse(fpsText, out requestedFps)
            || requestedFps < 30 || requestedFps > 240))
            throw new ArgumentException("FPS cap must be between 30 and 240.");
        string quality = Argument(arguments, Prefix + "-quality=") ?? "high";
        if (quality != "high" && quality != "low") throw new ArgumentException("Quality must be high or low.");
        requestedQuality = quality == "low" ? VisualQuality.Low : VisualQuality.High;
        routePath = Argument(arguments, Prefix + "-input=");
        if (mode == "replay")
        {
            if (string.IsNullOrWhiteSpace(routePath) || !Path.IsPathRooted(routePath))
                throw new ArgumentException("Replay requires an absolute -echo-shadow-replay-input= path.");
            if (new FileInfo(routePath).Length > 1024 * 1024) throw new ArgumentException("Route file exceeds 1 MiB.");
            string json = File.ReadAllText(routePath);
            expected = JsonUtility.FromJson<RouteRecording>(json);
            if (expected == null || expected.protocol != Protocol || !expected.valid
                || expected.seed <= 0 || expected.inputs == null || expected.inputs.Length > MaxInputs
                || expected.turns == null || expected.turns.Length > MaxEvents
                || expected.injuries == null || expected.injuries.Length > MaxEvents
                || expected.gates == null || expected.gates.Length > MaxEvents
                || !Finite(expected.windowElapsed) || expected.windowElapsed < WarmupSeconds + SampleSeconds)
                throw new ArgumentException("A valid completed record-mode route is required.");
            if (seedText != null && seed != expected.seed) throw new ArgumentException("Seed differs from route.");
            seed = expected.seed;
            float previousDistance = -1f;
            foreach (InputStep step in expected.inputs)
            {
                if (!Finite(step.distance) || step.distance < 0f || step.distance < previousDistance
                    || !Finite(step.resolvedDistance) || step.resolvedDistance < step.distance
                    || step.laneBefore < 0 || step.laneBefore > 2 || step.laneAfter < 0 || step.laneAfter > 2
                    || step.direction < (int)SwipeDirection.Up || step.direction > (int)SwipeDirection.Right
                    || !step.resolved || !step.accepted || step.telemetryOutcome != (int)InputIntentOutcome.Executed)
                    throw new ArgumentException("Route contains invalid/unexecuted input.");
                previousDistance = step.distance;
            }
            expectedRouteSha = Sha256(json);
        }
        else if (routePath != null) throw new ArgumentException("Record mode writes a new route; do not supply an input path.");

        // This public reset is permitted only after the isolated product guard.
        // It happens before scene Awake, so cached training models start equally.
        if (!EchoRunSaveSystem.CommitTrainingReset()) throw new InvalidOperationException("Isolated training reset failed.");
        VisualQualityController.SetQuality(requestedQuality);
        EchoRunAccessibility.SetLargeText(false);
        EchoRunAccessibility.SetHighContrast(false);
        EchoRunAccessibility.SetReducedMotion(false);
    }

    IEnumerator Start()
    {
        if (finished) yield break;
        float deadline = Time.realtimeSinceStartup + 25f;
        while (Time.realtimeSinceStartup < deadline)
        {
            manager = GameManager.Instance;
            input = InputManager.Instance;
            shadow = AIShadowRunner.Instance;
            track = TrackManager.Instance;
            if (player == null) player = FindObjectOfType<PlayerController>();
            if (manager != null && input != null && shadow != null && track != null && player != null) break;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(2f);
        if (manager == null || input == null || shadow == null || track == null || player == null)
        {
            Invalidate("Runtime dependencies did not initialize."); Finish(false); yield break;
        }
        try
        {
            manager.SetFrameRate(requestedFps);
            QualitySettings.vSyncCount = 0;
            var config = new SingleContractValidationConfig
            {
                enabled = true, fixedSeed = seed, freezeDirector = true,
                disablePowerUps = true, forceStandardDifficulty = true,
                useFixedIdentity = true, autoStart = false
            };
            if (manager.State != GameState.Menu || !manager.TryConfigureGameplayFlow(GameplayFlowMode.SingleContract, config)
                || !manager.TryStartConfiguredRun(out string error))
                throw new InvalidOperationException("Normal configured StartGame was rejected.");
            if (!shadow.HasActiveOpponent || shadow.SingleContractRuntime == null)
                throw new InvalidOperationException("Expected the normal fixed-identity challenge runtime.");
            observed = SnapshotRouteBasis();
            manifest = SnapshotManifest();
            if (expected != null) ValidateBasis();
            if (failure != null) Finish(false);
            else
            {
                lastForwardX = Mathf.RoundToInt(player.ForwardDirection.x);
                lastForwardZ = Mathf.RoundToInt(player.ForwardDirection.z);
                lastStrikes = manager.CollisionStrikes;
                input.SwipeResolved += OnInputResolved;
                shadow.PredictionGateSettlementConsumed += OnGate;
                beganAt = Time.unscaledTime;
                running = true;
            }
        }
        catch (Exception exception)
        {
            Invalidate("Start failed: " + exception.Message);
            Finish(false);
        }
    }

    RouteRecording SnapshotRouteBasis()
    {
        SingleContractFlow flow = shadow.SingleContractRuntime;
        var plan = new StringBuilder();
        for (int index = 0; index < flow.GateCount; index++)
        {
            PredictionGateDefinition gate = flow.GetGate(index).Definition;
            gate.runId = 0; // Session sequence is deliberately not a route identity.
            plan.Append(JsonUtility.ToJson(gate)).Append('\n');
        }
        return new RouteRecording
        {
            seed = manager.RunSeed,
            identityHash = shadow.OpponentIdentityPreview.ComputeHash(),
            gatePlanHash = Sha256(plan.ToString()),
            balanceHash = Sha256(JsonUtility.ToJson(GameBalanceConfig.Current)),
            startSpeed = manager.startSpeed, maxSpeed = manager.maxSpeed,
            acceleration = manager.speedIncreaseRate, fixedDeltaTime = Time.fixedDeltaTime,
            jumpDuration = player.jumpDuration, slideDuration = player.slideDuration,
            laneSwitchSpeed = player.laneSwitchSpeed
        };
    }

    RunManifest SnapshotManifest()
    {
        return new RunManifest
        {
            mode = mode, label = label, product = Application.productName,
            engine = Application.unityVersion,
#if ENABLE_IL2CPP
            backend = "IL2CPP",
#else
            backend = "Mono",
#endif
            graphicsDevice = SystemInfo.graphicsDeviceName,
            graphicsApi = SystemInfo.graphicsDeviceVersion,
            operatingSystem = SystemInfo.operatingSystem,
            processor = SystemInfo.processorType,
            startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            developmentBuild = Debug.isDebugBuild,
            visualQuality = VisualQualityController.Current.ToString(),
            qualityLevel = QualitySettings.GetQualityLevel(),
            width = Screen.width, height = Screen.height,
            targetFrameRate = Application.targetFrameRate, vSyncCount = QualitySettings.vSyncCount,
            colorSpace = QualitySettings.activeColorSpace.ToString(),
            largeText = EchoRunAccessibility.LargeText,
            highContrast = EchoRunAccessibility.HighContrast,
            reducedMotion = EchoRunAccessibility.ReducedMotion,
            fixedDeltaTime = Time.fixedDeltaTime, maximumDeltaTime = Time.maximumDeltaTime,
            seed = seed, routePath = routePath, routeSha256 = expectedRouteSha
        };
    }

    void ValidateBasis()
    {
        if (expected.seed != observed.seed || expected.identityHash != observed.identityHash
            || expected.gatePlanHash != observed.gatePlanHash || expected.balanceHash != observed.balanceHash
            || !Same(expected.startSpeed, observed.startSpeed) || !Same(expected.maxSpeed, observed.maxSpeed)
            || !Same(expected.acceleration, observed.acceleration) || !Same(expected.fixedDeltaTime, observed.fixedDeltaTime)
            || !Same(expected.jumpDuration, observed.jumpDuration) || !Same(expected.slideDuration, observed.slideDuration)
            || !Same(expected.laneSwitchSpeed, observed.laneSwitchSpeed))
            Invalidate("Route identity, gate plan, balance or movement configuration differs from recording.");
    }

    void LateUpdate()
    {
        if (!running || finished) return;
        if (failure != null) { Finish(false); return; }
        if (manager == null || player == null || input == null || shadow == null
            || manager.State != GameState.Playing || manager.IsDeathSequence)
        {
            Invalidate("Run left normal Playing or entered death before the window completed."); Finish(false); return;
        }
        if (!Application.isFocused || Input.anyKeyDown || Input.touchCount != 0)
        {
            Invalidate("Player lost focus or received external input."); Finish(false); return;
        }
        if (!Same(Time.timeScale, 1f) || !Same(Time.fixedDeltaTime, manifest.fixedDeltaTime)
            || !Same(Time.maximumDeltaTime, manifest.maximumDeltaTime)
            || Screen.width != manifest.width || Screen.height != manifest.height
            || QualitySettings.GetQualityLevel() != manifest.qualityLevel
            || VisualQualityController.Current != requestedQuality
            || Application.targetFrameRate != manifest.targetFrameRate || QualitySettings.vSyncCount != manifest.vSyncCount
            || EchoRunAccessibility.LargeText || EchoRunAccessibility.HighContrast || EchoRunAccessibility.ReducedMotion)
        {
            Invalidate("Runtime presentation/timing settings changed."); Finish(false); return;
        }
        float elapsed = Time.unscaledTime - beganAt;
        ObserveRouteEvents();
        if (pendingInput >= 0 && Time.unscaledTime - pendingSince > 0.25f)
            Invalidate("An input did not resolve within the existing buffer lifetime.");
        if (failure != null) { Finish(false); return; }

        if (sampleStarted < 0f && elapsed >= WarmupSeconds)
        {
            // Begin after a whole warmup frame. The following complete intervals
            // include the final interval crossing 30s; no long frames are removed.
            sampleStarted = elapsed;
            manifest.sampleStartRunSeconds = elapsed;
        }
        else if (sampleStarted >= 0f)
        {
            if (frameCount >= MaxFrames || !Finite(Time.unscaledDeltaTime) || Time.unscaledDeltaTime <= 0f)
            {
                Invalidate("Frame capacity exceeded or invalid application interval."); Finish(false); return;
            }
            frames[frameCount++] = new FrameSample
            {
                elapsed = elapsed, distance = manager.Distance,
                milliseconds = Time.unscaledDeltaTime * 1000f,
                lead = shadow.PlayerLead, lane = player.CurrentLane, strikes = manager.CollisionStrikes
            };
            if (elapsed - sampleStarted >= SampleSeconds) { Finish(true); return; }
        }

        if (mode == "record") DriveRecord(elapsed);
        else DriveReplay();
    }

    void ObserveRouteEvents()
    {
        int x = Mathf.RoundToInt(player.ForwardDirection.x);
        int z = Mathf.RoundToInt(player.ForwardDirection.z);
        if (x != lastForwardX || z != lastForwardZ)
        {
            if (turnCount >= MaxEvents) { Invalidate("Turn capacity exceeded."); return; }
            turns[turnCount++] = new RouteEvent { distance = manager.Distance, first = x, second = z };
            lastForwardX = x; lastForwardZ = z;
        }
        if (manager.CollisionStrikes != lastStrikes)
        {
            if (injuryCount >= MaxEvents) { Invalidate("Injury capacity exceeded."); return; }
            injuries[injuryCount++] = new RouteEvent
            { distance = manager.Distance, first = manager.CollisionStrikes, second = player.CurrentLane };
            lastStrikes = manager.CollisionStrikes;
        }
    }

    void DriveRecord(float elapsed)
    {
        if (elapsed < 2f || elapsed < nextDecision || pendingInput >= 0 || input.PendingInputCount != 0) return;
        nextDecision = elapsed + 0.04f;
        if (player.IsJumping || player.IsSliding
            || Mathf.Abs(player.LateralOffset - (player.CurrentLane - 1) * player.laneDistance) > 0.08f
            || track.IsInsideTurnTransition(player.transform.position)) return;
        if (!ThreatInLane(player.CurrentLane, out float distance, out ObstacleType type)) return;
        float speed = manager.CurrentSpeed;
        float avoidDistance = Mathf.Min(22f, Mathf.Max(8f, speed * 0.6f + 2f));
        if (distance > avoidDistance) return;
        int target = player.CurrentLane;
        float bestClearance = distance;
        for (int side = -1; side <= 1; side += 2)
        {
            int lane = player.CurrentLane + side;
            if (lane < 0 || lane > 2) continue;
            bool found = ThreatInLane(lane, out float clearance, out _);
            if (!found) clearance = 24f;
            float crossingDistance = speed * player.laneDistance / Mathf.Max(0.1f, player.laneSwitchSpeed);
            if (clearance > distance + crossingDistance + 2f && clearance > bestClearance)
            { target = lane; bestClearance = clearance; }
        }
        if (target != player.CurrentLane)
        {
            Issue(target < player.CurrentLane ? SwipeDirection.Left : SwipeDirection.Right);
            return;
        }
        float actionLead = type == ObstacleType.High
            ? speed * player.jumpDuration * 0.45f + 1.5f
            : speed * player.slideDuration * 0.30f + 1.5f;
        if (distance <= actionLead && type != ObstacleType.Barrier)
            Issue(type == ObstacleType.Low ? SwipeDirection.Down : SwipeDirection.Up);
        // A blocked barrier is allowed to cause a real failure. No invulnerability,
        // forced success, teleport, controller-state change or extended buffer.
    }

    bool ThreatInLane(int lane, out float distance, out ObstacleType type)
    {
        track.GetTrackPoseAhead(player.transform.position, player.ForwardDirection,
            player.RenderedLateralOffset, lane, 0f, out Vector3 position, out Vector3 forward);
        return track.TryGetUpcomingObstacleInLane(position, forward, lane, null,
            out distance, out type, out _);
    }

    void DriveReplay()
    {
        if (replayIndex >= expected.inputs.Length) return;
        InputStep next = expected.inputs[replayIndex];
        if (manager.Distance < next.distance) return;
        if (pendingInput >= 0 || input.PendingInputCount != 0)
        { Invalidate("Replay reached another fixed input with an unresolved queue; no retry attempted."); return; }
        if (player.CurrentLane != next.laneBefore || manager.Distance - next.distance > DistanceTolerance)
        { Invalidate("Replay input reached a different lane or missed its distance window."); return; }
        replayIndex++;
        Issue((SwipeDirection)next.direction);
    }

    void Issue(SwipeDirection direction)
    {
        if (inputCount >= MaxInputs) { Invalidate("Input capacity exceeded."); return; }
        pendingInput = inputCount++;
        inputs[pendingInput] = new InputStep
        {
            distance = manager.Distance, direction = (int)direction,
            laneBefore = player.CurrentLane, laneAfter = -1, telemetryOutcome = (int)InputIntentOutcome.Pending
        };
        pendingSince = Time.unscaledTime;
        input.QueueSwipe(direction, InputIntentSource.Replay, pendingSince);
    }

    void OnInputResolved(SwipeDirection direction, bool accepted)
    {
        if (!running || finished) return;
        if (pendingInput < 0 || inputs[pendingInput].direction != (int)direction)
        { Invalidate("Unexpected input resolution."); return; }
        InputStep step = inputs[pendingInput];
        step.resolved = true; step.accepted = accepted;
        step.laneAfter = player.CurrentLane; step.resolvedDistance = manager.Distance;
        inputs[pendingInput] = step;
        pendingInput = -1;
        if (!accepted) Invalidate("Existing input buffer/controller rejected, expired or dropped an input.");
    }

    void OnGate(PredictionGateSettlement settlement)
    {
        if (!running || finished) return;
        if (gateCount >= MaxEvents) { Invalidate("Gate capacity exceeded."); return; }
        gates[gateCount++] = new GateEvent
        {
            distance = manager.Distance, gateId = settlement.gateId, lane = player.CurrentLane,
            role = (int)settlement.chosenRole, execution = (int)settlement.execution,
            reason = (int)settlement.executionReason, speed = settlement.speedAtResolution,
            rewardMeters = settlement.signedLeadMeters
        };
    }

    void Invalidate(string reason)
    {
        if (failure == null) failure = reason;
    }

    void Finish(bool completedWindow)
    {
        if (finished) return;
        finished = true;
        running = false;
        if (input != null) input.SwipeResolved -= OnInputResolved;
        if (shadow != null) shadow.PredictionGateSettlementConsumed -= OnGate;
        try
        {
            if (manifest == null) manifest = new RunManifest
            { mode = mode, label = label, product = Application.productName, seed = seed };
            if (observed == null) observed = new RouteRecording { seed = seed };
            manifest.aliveAtEnd = manager != null && manager.State == GameState.Playing && !manager.IsDeathSequence;
            manifest.focusedAtEnd = Application.isFocused;
            manifest.windowElapsed = beganAt > 0f ? Time.unscaledTime - beganAt : 0f;
            manifest.endDistance = manager != null ? manager.Distance : 0f;
            manifest.endLead = shadow != null ? shadow.PlayerLead : 0f;
            manifest.strikes = manager != null ? manager.CollisionStrikes : 0;
            manifest.normalEndReason = manager != null ? manager.LastEndReason.ToString() : "NotStarted";
            if (!completedWindow || !manifest.aliveAtEnd || sampleStarted < WarmupSeconds || frameCount == 0
                || manifest.windowElapsed - sampleStarted < SampleSeconds)
                Invalidate("Normal gameplay did not survive the complete warmup and sample window.");
            if (pendingInput >= 0 || (input != null && input.PendingInputCount != 0))
                Invalidate("Window ended with unresolved input.");
            string telemetryJson = manager != null ? AIRunTelemetry.GetLatestRunJson() : "";
            CheckInputTelemetry(AIRunTelemetry.FromJson(telemetryJson));
            if (mode == "replay" && expected != null) CompareRoute();

            observed.windowElapsed = manifest.windowElapsed;
            observed.endDistance = manifest.endDistance;
            observed.endLead = manifest.endLead;
            observed.endLane = player != null ? player.CurrentLane : -1;
            observed.inputs = Copy(inputs, inputCount);
            observed.turns = Copy(turns, turnCount);
            observed.injuries = Copy(injuries, injuryCount);
            observed.gates = Copy(gates, gateCount);
            observed.valid = failure == null;
            observed.failure = failure ?? "";
            string routeJson = JsonUtility.ToJson(observed, true);
            string outputRoute = Path.Combine(directory, mode == "record" ? "route.json" : "observed-route.json");
            File.WriteAllText(outputRoute, routeJson, new UTF8Encoding(false));
            if (mode == "record") { manifest.routePath = outputRoute; manifest.routeSha256 = Sha256(routeJson); }
            if (!string.IsNullOrEmpty(telemetryJson))
                File.WriteAllText(Path.Combine(directory, "telemetry.json"), telemetryJson, new UTF8Encoding(false));
            WriteFrames();
            manifest.valid = failure == null;
            manifest.performanceEligible = manifest.valid && mode == "replay";
            manifest.failure = failure ?? "";
            manifest.frames = frameCount; manifest.inputs = inputCount;
            manifest.turns = turnCount; manifest.gates = gateCount;
            File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
            Debug.Log("SHADOW_ECHO_REPLAY_FINISHED valid=" + manifest.valid + " mode=" + mode
                + " failure=" + manifest.failure + " directory=" + directory);
        }
        catch (Exception exception)
        {
            Invalidate("Export/validation failed: " + exception.Message);
            if (manifest == null) manifest = new RunManifest { mode = mode, label = label };
            manifest.valid = false;
            manifest.performanceEligible = false;
            manifest.failure = failure;
            try
            {
                File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
            }
            catch (Exception writeError)
            {
                Debug.LogError("SHADOW_ECHO_REPLAY_INVALID: no manifest could be saved: " + writeError.Message);
            }
            Debug.LogError("SHADOW_ECHO_REPLAY_INVALID: export/validation failed: " + exception);
        }
        Application.Quit(failure == null ? 0 : 2);
    }

    void CheckInputTelemetry(AIRunTelemetryData telemetry)
    {
        if (telemetry == null || telemetry.runCapsule == null || telemetry.runCapsule.inputs == null
            || telemetry.seed != seed || telemetry.runCapsule.inputs.Count != inputCount)
        { Invalidate("Input telemetry is missing or contains an unexpected count/seed."); return; }
        for (int index = 0; index < inputCount; index++)
        {
            AIRunInputSample actual = telemetry.runCapsule.inputs[index];
            InputStep step = inputs[index];
            step.telemetrySequence = actual.sequence; step.telemetryOutcome = actual.outcome;
            inputs[index] = step;
            if (actual.source != (int)InputIntentSource.Replay || actual.direction != step.direction
                || actual.outcome != (int)InputIntentOutcome.Executed || actual.resolvedAt < actual.issuedAt
                || !step.resolved || !step.accepted || actual.lane != step.laneAfter)
                Invalidate("Input telemetry contains external input or a non-executed/mismatched command.");
        }
    }

    void CompareRoute()
    {
        if (replayIndex != expected.inputs.Length || inputCount != expected.inputs.Length)
            Invalidate("Fixed replay did not consume exactly the recorded input table.");
        for (int index = 0; index < Math.Min(inputCount, expected.inputs.Length); index++)
        {
            InputStep a = inputs[index], b = expected.inputs[index];
            if (a.direction != b.direction || a.laneBefore != b.laneBefore || a.laneAfter != b.laneAfter
                || !Near(a.distance, b.distance) || !Near(a.resolvedDistance, b.resolvedDistance))
                Invalidate("Input route differs from recording.");
        }
        CompareEvents(turns, turnCount, expected.turns, "Turn sequence differs from recording.");
        CompareEvents(injuries, injuryCount, expected.injuries, "Injury sequence differs from recording.");
        if (gateCount != expected.gates.Length) Invalidate("Gate count differs from recording.");
        for (int index = 0; index < Math.Min(gateCount, expected.gates.Length); index++)
        {
            GateEvent a = gates[index], b = expected.gates[index];
            if (a.gateId != b.gateId || a.lane != b.lane || a.role != b.role || a.execution != b.execution
                || a.reason != b.reason || !Near(a.distance, b.distance)
                || Mathf.Abs(a.speed - b.speed) > 0.5f || Mathf.Abs(a.rewardMeters - b.rewardMeters) > 0.3f)
                Invalidate("Gate outcome/speed/reward sequence differs from recording.");
        }
        if (!Near(manager.Distance, expected.endDistance) || !Near(shadow.PlayerLead, expected.endLead)
            || player.CurrentLane != expected.endLane)
            Invalidate("End-of-window distance, lead or lane differs from recording.");
    }

    void CompareEvents(RouteEvent[] actual, int count, RouteEvent[] reference, string error)
    {
        if (count != reference.Length) { Invalidate(error); return; }
        for (int index = 0; index < count; index++)
            if (actual[index].first != reference[index].first || actual[index].second != reference[index].second
                || !Near(actual[index].distance, reference[index].distance)) Invalidate(error);
    }

    void WriteFrames()
    {
        var text = new StringBuilder(frameCount * 75 + 128);
        text.AppendLine("frame,run_elapsed_s,distance_m,application_interval_ms,lead_m,lane,strikes");
        var sorted = new double[frameCount];
        double total = 0d;
        for (int index = 0; index < frameCount; index++)
        {
            FrameSample frame = frames[index];
            sorted[index] = frame.milliseconds; total += frame.milliseconds;
            text.Append(index).Append(',').Append(F(frame.elapsed)).Append(',').Append(F(frame.distance)).Append(',')
                .Append(F(frame.milliseconds)).Append(',').Append(F(frame.lead)).Append(',')
                .Append(frame.lane).Append(',').Append(frame.strikes).Append('\n');
        }
        string csv = text.ToString();
        manifest.framesSha256 = Sha256(csv);
        File.WriteAllText(Path.Combine(directory, "frames.csv"), csv, new UTF8Encoding(false));
        if (frameCount == 0) return;
        Array.Sort(sorted);
        int slowCount = Math.Max(1, (int)Math.Ceiling(frameCount * 0.01d));
        double slowSum = 0d;
        for (int index = frameCount - slowCount; index < frameCount; index++) slowSum += sorted[index];
        manifest.meanFrameMs = total / frameCount;
        manifest.onePercentLowFps = 1000d / (slowSum / slowCount);
        manifest.capturedSeconds = (float)(total / 1000d);
        manifest.sampleEndRunSeconds = frames[frameCount - 1].elapsed;
    }

    static T[] Copy<T>(T[] source, int count)
    {
        var copy = new T[count]; Array.Copy(source, copy, count); return copy;
    }
    static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Same(float a, float b) => Finite(a) && Finite(b) && Mathf.Abs(a - b) <= 0.0001f;
    static bool Near(float a, float b) => Finite(a) && Finite(b) && Mathf.Abs(a - b) <= DistanceTolerance;
    static string Sha256(string text)
    {
        using (SHA256 hash = SHA256.Create())
        {
            byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(text));
            var result = new StringBuilder(64);
            foreach (byte value in bytes) result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }

    void OnApplicationQuit()
    {
        if (!finished && directory != null)
        {
            Invalidate("Application quit before a completed QA window.");
            Finish(false);
        }
    }
}
#endif
