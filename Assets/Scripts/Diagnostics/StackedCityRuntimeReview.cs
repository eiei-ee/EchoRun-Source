#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Explicit diagnostic route only. Normal play and release players never run
// this harness; the existing validation launch owns the isolated save context.
[DefaultExecutionOrder(10000)]
public sealed class StackedCityRuntimeReview : MonoBehaviour
{
    private const string SideLanesArgument = "-echo-stacked-city-side-lanes";
    private const string ReloadArgument = "-echo-stacked-city-reload-review";
    private const string FinishGateArgument = "-echo-finish-gate-review";
    private bool reviewOrangeArt;
    private bool preserveRunnerArtPalette;
    private bool artTintApplied;
    private static readonly float[] ArtActionDistances = { 25f, 65f, 105f, 145f };
    private int artActionStep;
    private float artCaptureAt = -1f;
    private string artCaptureName;
    private readonly float[] distances = { 1, 12, 40, 70, 110, 180, 260, 350, 450, 500 };
    private readonly float[] finishRemainingDistances = { 28, 20, 10, 3 };
    private readonly List<Transform> vehicles = new List<Transform>();
    private readonly Dictionary<Transform, Vector3> vehiclePositions = new Dictionary<Transform, Vector3>();
    private readonly HashSet<Transform> movedVehicles = new HashSet<Transform>();
    private readonly HashSet<Transform> upperTrainVehicles = new HashSet<Transform>();
    private readonly HashSet<Transform> movedUpperTrains = new HashSet<Transform>();
    private readonly List<Renderer> lowerRenderers = new List<Renderer>();
    private readonly List<Renderer> upperRenderers = new List<Renderer>();
    private readonly List<string> captures = new List<string>();
    private string directory;
    private float started, playingStarted = -1f, nextAudit, nextClearanceAudit;
    private float pendingTurnCapture = -1f;
    private string pendingTurnName;
    private bool reviewCameraPolish;
    private float cameraTurnStarted = -1f;
    private int cameraTurnFrame;
    private readonly float[] cameraTurnTimes = { .05f, .15f, .3f, .6f, 1f };
    private int frames, turns, missingRoadFrames, missingLowerFrames, missingUpperFrames;
    private int lowerBlockCount, upperBlockCount, disabledHazards, captureIndex;
    private int vehicleMotionFrames, vehiclePositionChanges, geometryAudits, clearanceViolations;
    private int upperTrainPositionChanges;
    private int maximumAudioSourceCount, maximumClipSourceCount;
    private bool missingRoad, finished, cachedEnvironment;
    private Vector3 previousForward;
    private PlayerController player;
    private CityLowerDistrict lower;
    private CityUpperTransit upper;
    private bool reviewSideLanes, reviewReload, waitingForReload, reloadObserved;
    private int restartCount, previousGameManagerId, nextGameManagerId, queuedLaneInputs;
    private float phaseStarted;
    private readonly int[] laneFrames = new int[3];
    private float minimumRouteAhead = float.PositiveInfinity, maximumContentAhead;
    private int routeAheadSamples, routeBelowFogSamples, missingCityShellSamples;
    private bool reviewFinishGate, naturalFinishReached;
    private FinishGatePresentation finishGate;
    private int finishCaptureIndex, finishGateActiveFrames, missingFinishGateFrames;
    private int finishGateRendererCount, finishGateSignalCount, maximumFinishGateColliders;
    private int maximumEnabledFinishGateColliders, arrivalLightFrames;
    private float finishGateFirstRemaining = -1f, finishGateLastRemaining = -1f;
    private float minimumNearFinishForwardDot = 1f, maximumNearFinishLateralError;
    private Vector3 firstFinishGatePosition, lastFinishGatePosition, lastFinishGateForward;
    private Color firstFinishEmission, lastFinishEmission;
    private readonly List<string> finishGateRenderers = new List<string>();
    private MaterialPropertyBlock finishSignalProperties;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        if (Array.IndexOf(arguments, "-echo-stacked-city-review") < 0) return;
        if (FindObjectOfType<StackedCityRuntimeReview>() != null) return;
        if (Array.IndexOf(arguments, SingleContractValidationLaunchOptions.EnableArgument) < 0)
        {
            Debug.LogError("STACKED_CITY_REVIEW_REQUIRES_ISOLATED_VALIDATION: add -echo-single-contract-validation.");
            return;
        }
        var review = new GameObject("StackedCityRuntimeReview").AddComponent<StackedCityRuntimeReview>();
        review.reviewSideLanes = Array.IndexOf(arguments, SideLanesArgument) >= 0;
        review.reviewFinishGate = Array.IndexOf(arguments, FinishGateArgument) >= 0;
        review.reviewOrangeArt = Array.IndexOf(arguments, "-echo-orange-art-review") >= 0;
        review.preserveRunnerArtPalette = Array.IndexOf(arguments, "-echo-runner-art-review") >= 0;
        review.reviewCameraPolish = Array.IndexOf(arguments, "-echo-camera-polish-review") >= 0;
        review.reviewOrangeArt |= review.preserveRunnerArtPalette;
        // Finish review owns one complete natural course, not the 500 m reload phases.
        review.reviewReload = !review.reviewFinishGate && Array.IndexOf(arguments, ReloadArgument) >= 0;
        if (review.reviewReload) DontDestroyOnLoad(review.gameObject);
    }

    private void Start()
    {
        directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "StackedCityCaptures");
        Directory.CreateDirectory(directory);
        started = Time.realtimeSinceStartup;
        phaseStarted = started;
        Debug.Log("STACKED_CITY_RUNTIME_REVIEW_STARTED Visual diagnostic; obstacle colliders will be disabled only in this isolated validation route.");
        if (reviewSideLanes) Debug.Log("STACKED_CITY_SIDE_LANES enabled: real queued lane inputs; inspection-down captures are explicitly separate camera poses.");
        if (reviewFinishGate) Debug.Log("STACKED_CITY_FINISH_GATE_REVIEW enabled: natural complete course; no distance, speed or player pose override; director remains frozen by isolated validation.");
    }

    private void LateUpdate()
    {
        if (finished) return;
        float now = Time.realtimeSinceStartup;
        GameManager gm = GameManager.Instance;
        if (now - started >= (reviewFinishGate ? 220f : reviewReload ? 240f : 120f))
        {
            Finish(reviewFinishGate ? "FAILED: finish gate diagnostic timeout" : "Diagnostic timeout");
            return;
        }
        if (waitingForReload)
        {
            if (gm == null || gm.GetInstanceID() == previousGameManagerId) return;
            waitingForReload = false;
            reloadObserved = true;
            nextGameManagerId = gm.GetInstanceID();
            Debug.Log("STACKED_CITY_RELOAD_OBSERVED oldGameManager=" + previousGameManagerId + " newGameManager=" + nextGameManagerId);
        }
        if (gm == null || !gm.ActiveSingleContractValidationConfig.enabled)
        {
            if (now - phaseStarted >= 20f)
                Finish(reviewFinishGate ? "FAILED: isolated validation did not start" : "Isolated validation did not start");
            return;
        }
        if (gm.State == GameState.GameOver)
        {
            if (reviewFinishGate)
            {
                naturalFinishReached = gm.CourseDistance > 0f && gm.RemainingDistance <= .1f
                    && Mathf.Abs(gm.Distance - gm.CourseDistance) <= .1f
                    && gm.LastEndReason == RunEndReason.FinishReached;
                Capture("finish-result");
                Finish(naturalFinishReached
                    ? FinishGateObservationPassed()
                        ? "Completed natural course and observed authored finish gate through approach"
                        : "FAILED: natural course completed, but finish gate observation checks failed"
                    : "FAILED: validation ended before the natural course finish");
            }
            else Finish("Validation route ended before 500 metres");
            return;
        }
        if (gm.State != GameState.Playing) return;
        if (playingStarted < 0f) playingStarted = now;
        frames++;
        if (player == null) player = FindObjectOfType<PlayerController>();
        if (lower == null) lower = FindObjectOfType<CityLowerDistrict>();
        if (upper == null) upper = FindObjectOfType<CityUpperTransit>();
        if (player != null)
        {
            laneFrames[Mathf.Clamp(player.CurrentLane, 0, 2)]++;
            if (reviewSideLanes) DriveSideLanes(gm.Distance);
            if (reviewOrangeArt) ReviewOrangeActions(gm.Distance);
        }
        if (now >= nextAudit)
        {
            AuditRoute();
            nextAudit = now + .2f;
        }
        if (!cachedEnvironment && lowerBlockCount == 25 && upperBlockCount == 9)
            CacheEnvironment();
        if (now >= nextClearanceAudit && cachedEnvironment)
        {
            AuditClearance();
            nextClearanceAudit = now + 2f;
        }
        if (missingRoad) missingRoadFrames++;
        if (lowerBlockCount != 25) missingLowerFrames++;
        if (upperBlockCount != 9) missingUpperFrames++;
        ObserveTraffic();
        if (reviewFinishGate)
        {
            ObserveFinishGate(gm);
            while (finishCaptureIndex < finishRemainingDistances.Length
                && gm.RemainingDistance <= finishRemainingDistances[finishCaptureIndex])
            {
                Capture("finish-remaining-" + finishRemainingDistances[finishCaptureIndex]
                    .ToString("00", CultureInfo.InvariantCulture));
                finishCaptureIndex++;
            }
        }
        if (player != null)
        {
            Vector3 forward = player.ForwardDirection;
            if (previousForward.sqrMagnitude > .1f && Vector3.Angle(previousForward, forward) > 30f)
            {
                turns++;
                string direction = Vector3.Cross(previousForward, forward).y < 0f ? "left" : "right";
                pendingTurnName = "turn-" + turns + "-" + direction + "-after-0.3s";
                pendingTurnCapture = now + .3f;
                cameraTurnStarted = now;
                cameraTurnFrame = 0;
            }
            previousForward = forward;
        }
        if (pendingTurnCapture >= 0f && now >= pendingTurnCapture)
        {
            Capture(pendingTurnName);
            pendingTurnCapture = -1f;
        }
        if (reviewCameraPolish && cameraTurnStarted >= 0f
            && cameraTurnFrame < cameraTurnTimes.Length
            && now - cameraTurnStarted >= cameraTurnTimes[cameraTurnFrame])
        {
            EchoVisualCaptureProbe.CaptureOffscreen(Path.Combine(directory,
                "camera-turn-" + turns + "-" + cameraTurnTimes[cameraTurnFrame]
                    .ToString("0.00", CultureInfo.InvariantCulture) + "s.png"));
            cameraTurnFrame++;
        }
        if (captureIndex < distances.Length && gm.Distance >= distances[captureIndex])
        {
            Capture("distance-" + distances[captureIndex].ToString("000", CultureInfo.InvariantCulture));
            captureIndex++;
        }
        if (!reviewFinishGate && gm.Distance >= 500f && captureIndex == distances.Length)
        {
            if (reviewReload && restartCount == 0)
                RestartReview(gm);
            else
                Finish(reloadObserved ? "Completed 500-metre visual diagnostic after normal Restart scene reload"
                    : "Completed 500-metre visual diagnostic route");
        }
    }

    private void ObserveFinishGate(GameManager gm)
    {
        if (gm.RemainingDistance > 30f) return;
        if (finishGate == null && TrackManager.Instance != null)
        {
            Transform marker = TrackManager.Instance.transform.Find("FinishMarker");
            if (marker != null) finishGate = marker.GetComponent<FinishGatePresentation>();
        }
        if (finishGate == null || !finishGate.gameObject.activeInHierarchy)
        {
            // Leave a small allowance for the marker's activation boundary.
            if (gm.RemainingDistance <= 28f) missingFinishGateFrames++;
            return;
        }

        finishGateActiveFrames++;
        if (finishGateActiveFrames == 1)
        {
            finishGateFirstRemaining = gm.RemainingDistance;
            firstFinishGatePosition = finishGate.transform.position;
            Renderer[] renderers = finishGate.GetComponentsInChildren<Renderer>(true);
            finishGateRendererCount = renderers.Length;
            foreach (Renderer renderer in renderers)
            {
                var materials = new List<string>();
                foreach (Material material in renderer.sharedMaterials)
                    materials.Add(material != null ? material.name : "null");
                finishGateRenderers.Add(renderer.name + " | materials=" + string.Join(",", materials));
            }
            if (finishGate.signalRenderers != null)
                foreach (Renderer signal in finishGate.signalRenderers)
                    if (signal != null) finishGateSignalCount++;
        }
        finishGateLastRemaining = gm.RemainingDistance;
        lastFinishGatePosition = finishGate.transform.position;
        lastFinishGateForward = finishGate.transform.forward;
        Collider[] colliders = finishGate.GetComponentsInChildren<Collider>(true);
        maximumFinishGateColliders = Mathf.Max(maximumFinishGateColliders, colliders.Length);
        int enabledColliders = 0;
        foreach (Collider collider in colliders) if (collider.enabled) enabledColliders++;
        maximumEnabledFinishGateColliders = Mathf.Max(maximumEnabledFinishGateColliders, enabledColliders);
        if (finishGate.arrivalLights != null)
            foreach (Light light in finishGate.arrivalLights)
                if (light != null && light.enabled) { arrivalLightFrames++; break; }
        if (finishGate.signalRenderers != null && finishGate.signalRenderers.Length > 0
            && finishGate.signalRenderers[0] != null)
        {
            if (finishSignalProperties == null) finishSignalProperties = new MaterialPropertyBlock();
            finishGate.signalRenderers[0].GetPropertyBlock(finishSignalProperties);
            lastFinishEmission = finishSignalProperties.GetColor("_EmissionColor");
            if (finishGateActiveFrames == 1) firstFinishEmission = lastFinishEmission;
        }
        if (player != null && gm.RemainingDistance <= 10f)
        {
            Vector3 forward = player.ForwardDirection.normalized;
            float dot = Vector3.Dot(forward, finishGate.transform.forward);
            minimumNearFinishForwardDot = Mathf.Min(minimumNearFinishForwardDot, dot);
            // Lateral alignment is meaningful only once both poses share a straight.
            if (dot > .999f)
            {
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                Vector3 routeCenter = player.transform.position - right * player.RenderedLateralOffset;
                maximumNearFinishLateralError = Mathf.Max(maximumNearFinishLateralError,
                    Mathf.Abs(Vector3.Dot(finishGate.transform.position - routeCenter, right)));
            }
        }
    }

    private bool FinishGateObservationPassed()
    {
        return finishCaptureIndex == finishRemainingDistances.Length && finishGateActiveFrames > 0
            && missingFinishGateFrames == 0 && finishGateRendererCount > 0 && finishGateSignalCount > 0
            && maximumFinishGateColliders == 0;
    }

    private void DriveSideLanes(float distance)
    {
        // Exercise the shipped input buffer/controller/camera ownership. No root
        // teleport, lane setter or CameraFollow override is used for these frames.
        int targetLane = Mathf.FloorToInt(distance / 30f) % 3;
        if (restartCount > 0) targetLane = 2 - targetLane;
        InputManager input = InputManager.Instance;
        if (input == null || input.PendingInputCount != 0 || player.CurrentLane == targetLane) return;
        input.QueueSwipe(targetLane < player.CurrentLane ? SwipeDirection.Left : SwipeDirection.Right,
            InputIntentSource.Replay, Time.unscaledTime);
        queuedLaneInputs++;
    }

    private void ReviewOrangeActions(float distance)
    {
        // Real controller inputs in the existing isolated visual harness.
        if (!artTintApplied)
        {
            if (!preserveRunnerArtPalette)
                RunnerAppearanceService.Apply(player.characterModel,
                    new Color(.047f,.07f,.092f), new Color(.8f,.235f,.067f), Color.black);
            artTintApplied = true; // Memory-only; keep the user's saved cosmetic.
        }
        if (artCaptureAt >= 0f && Time.unscaledTime >= artCaptureAt)
        {
            Capture(artCaptureName + "-jumping-" + player.IsJumping + "-sliding-" + player.IsSliding);
            artCaptureAt = -1f;
        }
        if (artActionStep >= 4 || InputManager.Instance == null) return;
        if (distance < ArtActionDistances[artActionStep] || player.IsJumping || player.IsSliding) return;
        bool jump = artActionStep % 2 == 0;
        InputManager.Instance.QueueSwipe(jump ? SwipeDirection.Up : SwipeDirection.Down,
            InputIntentSource.Replay, Time.unscaledTime);
        artCaptureName = "outfit-" + (jump ? "jump" : "slide") + "-" + artActionStep;
        artCaptureAt = Time.unscaledTime + .23f;
        artActionStep++;
    }

    private void RestartReview(GameManager gm)
    {
        artActionStep = 0; artTintApplied = false; artCaptureAt = -1f;
        WriteReport("Completed 500-metre visual diagnostic before normal Restart", "report-before-reload.txt");
        previousGameManagerId = gm.GetInstanceID();
        restartCount = 1;
        waitingForReload = true;
        player = null; lower = null; upper = null;
        vehicles.Clear(); vehiclePositions.Clear(); movedVehicles.Clear();
        upperTrainVehicles.Clear(); movedUpperTrains.Clear(); lowerRenderers.Clear(); upperRenderers.Clear();
        captures.Clear();
        Array.Clear(laneFrames, 0, laneFrames.Length);
        frames = turns = missingRoadFrames = missingLowerFrames = missingUpperFrames = 0;
        lowerBlockCount = upperBlockCount = disabledHazards = captureIndex = 0;
        vehicleMotionFrames = vehiclePositionChanges = geometryAudits = clearanceViolations = 0;
        upperTrainPositionChanges = maximumAudioSourceCount = maximumClipSourceCount = queuedLaneInputs = 0;
        missingRoad = cachedEnvironment = false;
        pendingTurnCapture = playingStarted = -1f;
        pendingTurnName = null;
        previousForward = Vector3.zero;
        nextAudit = nextClearanceAudit = 0f;
        minimumRouteAhead = float.PositiveInfinity;
        maximumContentAhead = 0f;
        routeAheadSamples = routeBelowFogSamples = missingCityShellSamples = 0;
        phaseStarted = Time.realtimeSinceStartup;
        Debug.Log("STACKED_CITY_NORMAL_RESTART_REQUESTED");
        gm.Restart();
    }

    private void AuditRoute()
    {
        // New obstacles are spawned ahead of the runner. Limit scene scans to
        // five per second; retain their visible presentation and route layout.
        foreach (Obstacle obstacle in FindObjectsOfType<Obstacle>())
        foreach (Collider collider in obstacle.GetComponentsInChildren<Collider>())
            if (collider.enabled) { collider.enabled = false; disabledHazards++; }
        lowerBlockCount = lower != null ? lower.transform.childCount : 0;
        upperBlockCount = upper != null ? upper.transform.childCount : 0;
        missingRoad = false;
        float routeEnd = 0f;
        foreach (TrackSegmentData segment in FindObjectsOfType<TrackSegmentData>())
        {
            routeEnd = Mathf.Max(routeEnd, segment.routeDistance + TrackManager.Instance.segmentLength);
            if (segment.transform.Find("CityV7Environment") == null) missingCityShellSamples++;
            if (segment.segmentType != TrackSegmentType.Straight) continue;
            Transform ground = segment.transform.Find("GroundPlane");
            Renderer renderer = ground != null ? ground.GetComponent<Renderer>() : null;
            Transform roadArt = segment.transform.Find(OrangeEchoRoadVisuals.RootName);
            if (roadArt != null)
            {
                Renderer[] roadRenderers = roadArt.GetComponentsInChildren<Renderer>();
                bool visibleDeck = false;
                foreach (Renderer roadRenderer in roadRenderers)
                    if (roadRenderer.enabled && roadRenderer.name.EndsWith("OE_RoadDeck")) visibleDeck = true;
                if (!visibleDeck) missingRoad = true;
                continue;
            }
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            { missingRoad = true; }
        }
        float ahead = routeEnd - GameManager.Instance.Distance;
        minimumRouteAhead = Mathf.Min(minimumRouteAhead, ahead);
        routeAheadSamples++;
        if (ahead < CityV7PlayableEnvironment.FogEndDistance) routeBelowFogSamples++;
        maximumContentAhead = Mathf.Max(maximumContentAhead,
            TrackManager.Instance.ContentPreparedRouteDistance - GameManager.Instance.Distance);
        AudioSource[] sources = FindObjectsOfType<AudioSource>();
        int clipSources = 0;
        foreach (AudioSource source in sources) if (source.clip != null) clipSources++;
        maximumAudioSourceCount = Mathf.Max(maximumAudioSourceCount, sources.Length);
        maximumClipSourceCount = Mathf.Max(maximumClipSourceCount, clipSources);
    }

    private void CacheEnvironment()
    {
        lowerRenderers.AddRange(lower.GetComponentsInChildren<Renderer>(true));
        upperRenderers.AddRange(upper.GetComponentsInChildren<Renderer>(true));
        foreach (CityTransitLoop loop in FindObjectsOfType<CityTransitLoop>())
        {
            if (loop.vehicles == null) continue;
            foreach (Transform vehicle in loop.vehicles)
            {
                if (vehicle == null || vehiclePositions.ContainsKey(vehicle)) continue;
                vehicles.Add(vehicle);
                vehiclePositions.Add(vehicle, vehicle.localPosition);
                if (upper != null && loop.transform.IsChildOf(upper.transform))
                    upperTrainVehicles.Add(vehicle);
            }
        }
        cachedEnvironment = true;
    }

    private void ObserveTraffic()
    {
        bool movedThisFrame = false;
        foreach (Transform vehicle in vehicles)
        {
            if (vehicle == null) continue;
            Vector3 position = vehicle.localPosition;
            // Local movement ignores a parent block being recycled in world space.
            if ((position - vehiclePositions[vehicle]).sqrMagnitude > .000001f)
            {
                vehiclePositionChanges++;
                movedVehicles.Add(vehicle);
                if (upperTrainVehicles.Contains(vehicle))
                {
                    upperTrainPositionChanges++;
                    movedUpperTrains.Add(vehicle);
                }
                movedThisFrame = true;
            }
            vehiclePositions[vehicle] = position;
        }
        if (movedThisFrame) vehicleMotionFrames++;
    }

    private void AuditClearance()
    {
        geometryAudits++;
        // World renderer bounds at a two-second interval; no mesh vertex reads
        // or claimed full camera-occlusion test. Runtime grids move only in XZ.
        foreach (Renderer renderer in lowerRenderers)
            if (renderer != null && renderer.bounds.max.y >= -3f) clearanceViolations++;
        foreach (Renderer renderer in upperRenderers)
            if (renderer != null && renderer.bounds.min.y <= 8f) clearanceViolations++;
    }

    private void Capture(string name)
    {
        if (reviewReload) name = (restartCount == 0 ? "before-reload-" : "after-reload-") + name;
        string path = Path.Combine(directory, name + ".png");
        EchoVisualCaptureProbe.CaptureOffscreen(path);
        captures.Add(name + " | actualDistance=" + GameManager.Instance.Distance.ToString("F2", CultureInfo.InvariantCulture)
            + " | lane=" + (player != null ? player.CurrentLane : -1)
            + " | lateralOffset=" + (player != null ? player.LateralOffset : 0f)
            + (reviewFinishGate ? " | remaining=" + GameManager.Instance.RemainingDistance
                .ToString("F2", CultureInfo.InvariantCulture) : ""));
        Debug.Log("STACKED_CITY_RUNTIME_FRAME " + path);
        if (reviewOrangeArt && player != null
            && (name == "distance-040" || name.StartsWith("outfit-", StringComparison.Ordinal)))
            CaptureOutfitInspection(name);
        if (reviewSideLanes && player != null && player.CurrentLane != 1)
            CaptureDownwardInspection(name);
    }

    private void CaptureOutfitInspection(string name)
    {
        Camera camera = Camera.main;
        if (camera == null || player.characterModel == null) return;
        Vector3 position = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        float fieldOfView = camera.fieldOfView;
        try
        {
            // Same animated frame and lighting, with an inspection camera only.
            // Keep the normal gameplay capture above as the framing reference.
            Vector3 forward = player.ForwardDirection;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 target = player.characterModel.position + Vector3.up * .9f;
            // The animated skeleton can move relative to CharacterModel. Frame
            // the current baked garment, not its controller/culling origin.
            foreach (var skin in player.characterModel.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!skin.enabled || skin.name != "OE_OrangeEchoClothing") continue;
                var posed = new Mesh();
                try
                {
                    skin.BakeMesh(posed);
                    target = skin.transform.TransformPoint(posed.bounds.center);
                }
                finally { Destroy(posed); }
                break;
            }
            camera.fieldOfView = 34f;
            camera.transform.position = target - forward * 3.4f + right * 1.3f + Vector3.up * .25f;
            camera.transform.LookAt(target);
            EchoVisualCaptureProbe.CaptureOffscreen(Path.Combine(directory, name + "-close-rear.png"));
            camera.transform.position = target + right * 3.7f + Vector3.up * .15f;
            camera.transform.LookAt(target);
            EchoVisualCaptureProbe.CaptureOffscreen(Path.Combine(directory, name + "-close-side.png"));
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = fieldOfView;
        }
    }

    private void CaptureDownwardInspection(string name)
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        Vector3 position = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        try
        {
            Vector3 forward = player.ForwardDirection;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 anchor = player.transform.position;
            float edgeSign = player.CurrentLane == 0 ? -1f : 1f;
            Vector3 routeCenter = anchor - right * player.LateralOffset;
            camera.transform.position = routeCenter + right * edgeSign
                * (TrackGeometryStandards.VisualRoadHalfWidth + .75f) + Vector3.up * 2f;
            camera.transform.LookAt(routeCenter + right * edgeSign * 17f
                + forward * 8f + Vector3.down * 22f);
            string inspection = name + "-inspection-down";
            EchoVisualCaptureProbe.CaptureOffscreen(Path.Combine(directory, inspection + ".png"));
            captures.Add(inspection + " | inspection camera only; actual player lane=" + player.CurrentLane);
        }
        finally { camera.transform.SetPositionAndRotation(position, rotation); }
    }

    private void Finish(string reason)
    {
        if (finished) return;
        finished = true;
        WriteReport(reason, "report.txt");
        enabled = false;
        Application.Quit();
    }

    private void WriteReport(string reason, string fileName)
    {
        float elapsed = Time.realtimeSinceStartup - phaseStarted;
        float playingElapsed = playingStarted < 0f ? 0f : Time.realtimeSinceStartup - playingStarted;
        var report = new StringBuilder();
        report.AppendLine("Visual diagnostic only; obstacle colliders disabled in isolated fixed-seed validation.");
        report.AppendLine("Not collision acceptance, human play acceptance, a performance benchmark, or audible verification.");
        report.AppendLine(reason);
        if (reviewFinishGate)
        {
            GameManager gm = GameManager.Instance;
            report.AppendLine("finishGateReviewRequested=True\nnaturalFinishReached=" + naturalFinishReached
                + "\nfinishGateObservationPassed=" + FinishGateObservationPassed());
            report.AppendLine("finishModeBasis=natural course; no speed, distance or player pose override; isolated validation freezes director and disables hazard colliders.");
            report.AppendLine("courseDistance=" + (gm != null ? gm.CourseDistance : 0f)
                + "\nremainingDistance=" + (gm != null ? gm.RemainingDistance : -1f)
                + "\nlastEndReason=" + (gm != null ? gm.LastEndReason.ToString() : "missing GameManager"));
            report.AppendLine("finishApproachCaptures=" + finishCaptureIndex + "/" + finishRemainingDistances.Length
                + "\nfinishGateActiveFrames=" + finishGateActiveFrames
                + "\nmissingFinishGateFramesInside28m=" + missingFinishGateFrames);
            report.AppendLine("finishGateRendererCount=" + finishGateRendererCount
                + "\nfinishGateSignalCount=" + finishGateSignalCount
                + "\nmaximumFinishGateColliders=" + maximumFinishGateColliders
                + "\nmaximumEnabledFinishGateColliders=" + maximumEnabledFinishGateColliders
                + "\narrivalLightFrames=" + arrivalLightFrames);
            report.AppendLine("finishGateFirstRemaining=" + finishGateFirstRemaining
                + "\nfinishGateLastRemaining=" + finishGateLastRemaining
                + "\nfinishGateFirstPosition=" + firstFinishGatePosition.ToString("F3")
                + "\nfinishGateLastPosition=" + lastFinishGatePosition.ToString("F3")
                + "\nfinishGateLastForward=" + lastFinishGateForward.ToString("F3"));
            report.AppendLine("minimumGateRunnerForwardDotInside10m=" + minimumNearFinishForwardDot
                + "\nmaximumGateLateralErrorOnAlignedStraight=" + maximumNearFinishLateralError
                + "\nfirstSignalEmission=" + firstFinishEmission.ToString("F3")
                + "\nlastSignalEmission=" + lastFinishEmission.ToString("F3"));
            foreach (string renderer in finishGateRenderers) report.AppendLine("finishGateRenderer=" + renderer);
        }
        report.AppendLine("sideLaneInputsEnabled=" + reviewSideLanes + "\nqueuedLaneInputs=" + queuedLaneInputs);
        report.AppendLine("actualLaneFrames=left:" + laneFrames[0] + ";center:" + laneFrames[1] + ";right:" + laneFrames[2]);
        report.AppendLine("reloadReviewRequested=" + reviewReload + "\nnormalRestartCount=" + restartCount
            + "\nreloadObserved=" + reloadObserved + "\npreviousGameManagerId=" + previousGameManagerId
            + "\nnewGameManagerId=" + nextGameManagerId);
        report.AppendLine("distance=" + (GameManager.Instance != null ? GameManager.Instance.Distance : 0f));
        report.AppendLine("frames=" + frames + "\nturns=" + turns + "\nmissingRoadFrames=" + missingRoadFrames);
        report.AppendLine("roadCheckIntervalSeconds=0.2; missingRoadFrames counts frames carrying the last sampled state.");
        report.AppendLine("routeAheadSamples=" + routeAheadSamples + "\nminimumRouteShellAhead=" + minimumRouteAhead
            + "\nrouteShellBelowFogSamples=" + routeBelowFogSamples + "\nmissingCityShellSamples=" + missingCityShellSamples
            + "\nmaximumPreparedContentAhead=" + maximumContentAhead);
        report.AppendLine("lowerBlockCount=" + lowerBlockCount + "\nupperBlockCount=" + upperBlockCount);
        report.AppendLine("missingLowerFrames=" + missingLowerFrames + "\nmissingUpperFrames=" + missingUpperFrames);
        report.AppendLine("disabledHazardColliders=" + disabledHazards);
        report.AppendLine("trackedVehicles=" + vehicles.Count + "\ndistinctMovingVehicles=" + movedVehicles.Count);
        report.AppendLine("vehicleMotionFrames=" + vehicleMotionFrames + "\nvehiclePositionChanges=" + vehiclePositionChanges);
        report.AppendLine("upperTrainCount=" + upperTrainVehicles.Count + "\ndistinctMovingUpperTrains=" + movedUpperTrains.Count
            + "\nupperTrainPositionChanges=" + upperTrainPositionChanges);
        report.AppendLine("clearanceBasis=lower world renderer maxY < -3; upper minY > 8; sampled every 2s; no occlusion raycast.");
        report.AppendLine("geometryAudits=" + geometryAudits + "\nclearanceViolationSamples=" + clearanceViolations);
        report.AppendLine("realtimeElapsedSeconds=" + elapsed.ToString("F3", CultureInfo.InvariantCulture));
        report.AppendLine("playingElapsedSeconds=" + playingElapsed.ToString("F3", CultureInfo.InvariantCulture));
        report.AppendLine("diagnosticMeanFps=" + (frames / Mathf.Max(.001f, playingElapsed)).ToString("F2", CultureInfo.InvariantCulture));
        report.AppendLine("maxAudioSourceCount=" + maximumAudioSourceCount + "\nmaxAudioSourcesWithClip=" + maximumClipSourceCount);
        foreach (AudioSource source in FindObjectsOfType<AudioSource>())
            report.AppendLine("audio=" + source.name + " | clip=" + (source.clip != null ? source.clip.name : "null")
                + " | volume=" + source.volume.ToString("F3", CultureInfo.InvariantCulture) + " | isPlaying=" + source.isPlaying);
        foreach (string capture in captures) report.AppendLine("capture=" + capture);
        File.WriteAllText(Path.Combine(directory, fileName), report.ToString());
        Debug.Log("STACKED_CITY_RUNTIME_REVIEW_COMPLETE\n" + report);
    }
}
#endif
