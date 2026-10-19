using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Uses the shipped player, road prefabs and city decoration in a temporary
// scene. These are fixed composition samples, not proof of a completed run.
public static class StackedCityReview
{
    private const string GameplayScene = "Assets/Scenes/SampleScene.scene";
    private const string Output = "TestResults/StackedCity";
    private const string PolishOutput = "TestResults/StackedCityPolish";
    private const string WallOutput = "TestResults/StackedCityWallDetails";
    private const string CornerOutput = "TestResults/StackedCityContinuity";
    private const string FlickerOutput = "TestResults/StackedCityFlicker";
    private const string DistrictOutput = "TestResults/StackedCityDistricts";

    [MenuItem("Tools/Echo Runner/Stacked City/Capture Composition")]
    public static void Capture()
    {
        CaptureInto(Output, false);
    }

    [MenuItem("Tools/Echo Runner/Stacked City Polish/Capture Before")]
    public static void PolishCaptureBefore()
    {
        CaptureInto(PolishOutput + "/Before", true);
    }

    [MenuItem("Tools/Echo Runner/Stacked City Polish/Capture After")]
    public static void PolishCaptureAfter()
    {
        CaptureInto(PolishOutput + "/After", true);
    }

    [MenuItem("Tools/Echo Runner/Stacked City Wall Details/Capture Before")]
    public static void WallCaptureBefore()
    {
        CaptureInto(WallOutput + "/Before", true);
    }

    [MenuItem("Tools/Echo Runner/Stacked City Wall Details/Capture After")]
    public static void WallCaptureAfter()
    {
        CaptureInto(WallOutput + "/After", true, true);
    }

    [MenuItem("Tools/Echo Runner/Stacked City Continuity/Capture Before")]
    public static void CornerCaptureBefore()
    {
        CaptureInto(CornerOutput + "/Before", false, false, true);
    }

    [MenuItem("Tools/Echo Runner/Stacked City Continuity/Capture After")]
    public static void CornerCaptureAfter()
    {
        CaptureInto(CornerOutput + "/After", false, false, true);
    }

    public static void FlickerCaptureBefore() { CaptureInto(FlickerOutput + "/Before", false, false, true, true); }
    public static void FlickerCaptureAfter() { CaptureInto(FlickerOutput + "/After", false, false, true, true); }
    public static void FlickerBuild() { BuildInto(FlickerOutput, "EchoRun-StackedCity-SurfaceFix"); }
    public static void FlickerCaptureAndBuild() { FlickerCaptureAfter(); FlickerBuild(); }
    public static void DistrictCaptureBefore() { CaptureInto(DistrictOutput + "/Before", true, true, true); }
    public static void DistrictCaptureAfter() { CaptureInto(DistrictOutput + "/After", true, true, true); }
    public static void DistrictBuild() { BuildInto(DistrictOutput, "EchoRun-StackedCity-Districts"); }
    public static void DistrictCaptureAndBuild() { DistrictCaptureAfter(); DistrictBuild(); }
    public static void StreamingBuild() { BuildInto("TestResults/CityStreaming", "EchoRun-CityStreaming"); }
    public static void FinishGateCapture() { CaptureInto("TestResults/CityFinishGate", false, includeFinishGate: true); }
    public static void FinishGateBuild() { BuildInto("TestResults/CityFinishGate", "EchoRun-CityFinishGate"); }
    public static void NoticeReadabilityBefore() { CaptureInto("TestResults/CityNoticeReadability/Before", true, true, true); }
    public static void NoticeReadabilityInstallAndBuild()
    {
        StackedCityDistricts.Install();
        CaptureInto("TestResults/CityNoticeReadability/After", true, true, true);
        BuildInto("TestResults/CityNoticeReadability", "EchoRun-CityNoticeReadability");
    }

    internal static void CaptureInto(string output, bool includeFacadeDetails, bool includeWallStories = false,
        bool includeCornerLanes = false, bool includeFlicker = false, bool includeFinishGate = false)
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Capture outside Play Mode.");

        // Installers may leave the batch editor's untitled scratch scene dirty.
        // Unity refuses any additive scene creation in that state. A batch
        // review owns that scratch scene, while an interactive session keeps
        // the user's open scenes and unsaved edits intact.
        if (Application.isBatchMode)
            EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);

        Scene originalScene = SceneManager.GetActiveScene();
        Scene sourceScene = SceneManager.GetSceneByPath(GameplayScene);
        bool openedSource = !sourceScene.IsValid() || !sourceScene.isLoaded;
        Scene captureScene = default;
        var rootStates = new Dictionary<GameObject, bool>();
        bool previousAsync = ShaderUtil.allowAsyncCompilation;
        var qualityState = new CaptureQualityState();
        string directory = Path.GetFullPath(output + "/Composition");
        Directory.CreateDirectory(directory);
        var report = new StringBuilder();
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            if (openedSource)
                sourceScene = EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Additive);
            GameObject sourcePlayer = FindRoot(sourceScene, "player");
            Camera sourceCamera = FindRoot(sourceScene, "Main Camera").GetComponent<Camera>();
            CameraFollow follow = sourceCamera.GetComponent<CameraFollow>();
            if (follow == null) throw new InvalidOperationException("Gameplay CameraFollow missing.");
            Vector3 cameraOffset = WorldStyler.GetCameraOffset(false) + CameraFollow.ResolveMotionOffset(
                Vector3.forward, 1f, 0f, follow.speedPullback, follow.speedLift, follow.slideCameraDrop);
            float fieldOfView = WorldStyler.GetCameraFieldOfView(false)
                + CameraFollow.ResolveSpeedFieldOfViewOffset(1f, follow.maximumSpeedFovBoost);

            for (int index = 0; index < SceneManager.sceneCount; index++)
            foreach (GameObject root in SceneManager.GetSceneAt(index).GetRootGameObjects())
            {
                rootStates.Add(root, root.activeSelf);
                root.SetActive(false);
            }
            captureScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(captureScene);
            var camera = new GameObject("StackedCityReviewCamera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = sourceCamera.nearClipPlane;
            camera.farClipPlane = 420f;
            camera.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.skybox = RequireResource("CityV7/ExperienceSky", typeof(Material)) as Material;
            var key = new GameObject("ReviewKey").AddComponent<Light>();
            key.type = LightType.Directional;
            key.shadows = LightShadows.Soft;
            var fill = new GameObject("ReviewFill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(38f, 145f, 0f);
            CityV7PlayableEnvironment.ApplyAtmosphere(key, fill);
            Object.Instantiate((GameObject)RequireResource("CityV7/ExperienceSkyline", typeof(GameObject)));
            SpawnCity(camera.transform);
            var styleHost = new GameObject("ReviewPickupStyler");
            styleHost.SetActive(false);
            WorldStyler styler = styleHost.AddComponent<WorldStyler>();
            // This inactive diagnostic host deliberately skips Awake. Prepare
            // the same shared obstacle materials before styling its test route.
            typeof(WorldStyler).GetMethod("EnsurePalette", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(styler, null);
            GameObject player = Object.Instantiate(sourcePlayer);
            player.name = "ReviewPlayer";
            foreach (MonoBehaviour script in player.GetComponentsInChildren<MonoBehaviour>(true))
                script.enabled = false;
            foreach (Rigidbody body in player.GetComponentsInChildren<Rigidbody>(true))
                body.isKinematic = true;
            // Reproduce the grounding adjustment in PlayerController.Start
            // without invoking the gameplay/save lifecycle.
            CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
            PlayerController controller = player.GetComponent<PlayerController>();
            float capsuleBottom = capsule.center.y - capsule.height * .5f;
            float groundedAnchorY = TrackGeometryStandards.AuthoredRoadSurfaceTopY - capsuleBottom;
            if (controller.characterModel != null)
            {
                Vector3 local = controller.characterModel.localPosition;
                local.y = capsuleBottom;
                controller.characterModel.localPosition = local;
            }
            player.SetActive(true);
            Animator animator = player.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Play("Run", 0, .23f);
                animator.Update(0f);
            }
            report.AppendLine("Fresh rendered composition samples; gameplay prefabs with manually positioned player/traffic.");
            report.AppendLine("No live input, HUD timing, audio or human play acceptance is claimed.");
            report.AppendLine("WorldStyler landscape camera plus full-speed feedback = " + cameraOffset + "; FOV = " + fieldOfView);
            report.AppendLine("Camera looks at player anchor + forward * 5, matching CameraFollow.");
            report.AppendLine("Grounded player anchor Y = " + groundedAnchorY + "; model local Y = capsule bottom " + capsuleBottom);
            WriteAssetStats(report);
            foreach (int direction in new[] { -1, 1 })
            {
                GameObject route = SpawnRoute(direction, styler);
                try
                {
                    CityV7PlayableEnvironment.RefreshClearance();
                    string side = direction < 0 ? "left" : "right";
                    if (includeFinishGate)
                    {
                        var gate = Object.Instantiate(Resources.Load<GameObject>(FinishGatePresentation.ResourcePath), route.transform);
                        gate.transform.SetPositionAndRotation(new Vector3(0f, 0f, 60f), Quaternion.identity);
                        gate.SetActive(true);
                        foreach (float remaining in new[] { 28f, 15f, 6f })
                        foreach (float lane in new[] { -3f, 0f, 3f })
                        {
                            gate.GetComponent<FinishGatePresentation>().SetApproach(1f - remaining / 30f, remaining < 12f);
                            CaptureView(camera, player, cameraOffset, new Vector3(lane, groundedAnchorY, 60f - remaining), Vector3.forward,
                                4f, directory, side + "-finish-" + remaining + "m-lane-" + (lane + 3f), report);
                        }
                        continue;
                    }
                    CaptureView(camera, player, cameraOffset, new Vector3(0, groundedAnchorY, 42f), Vector3.forward,
                        8f, directory, side + "-straight", report);
                    CaptureView(camera, player, cameraOffset, new Vector3(0, groundedAnchorY, 70f), Vector3.forward,
                        4f, directory, side + "-turn-approach", report);
                    CaptureView(camera, player, cameraOffset, new Vector3(direction * 4f, groundedAnchorY, 80f),
                        Quaternion.Euler(0, direction * 55f, 0) * Vector3.forward,
                        4f, directory, side + "-turn-sweep", report);
                    CaptureView(camera, player, cameraOffset, new Vector3(direction * 28f, groundedAnchorY, 80f),
                        Vector3.right * direction, 4f, directory, side + "-turn-exit", report);
                    if (includeCornerLanes)
                        CaptureCornerLanes(camera, player, cameraOffset, groundedAnchorY, direction, directory, report);
                    if (includeFlicker)
                        StackedCityFlickerReview.Capture(camera, player, route, cameraOffset, groundedAnchorY, direction, directory, report);
                    if (direction == 1)
                    {
                        foreach (float seconds in new[] { 0f, 4f, 8f })
                            CaptureView(camera, player, cameraOffset, new Vector3(0, groundedAnchorY, 28f), Vector3.forward,
                                seconds, directory, "transit-" + seconds.ToString("00"), report);
                        if (includeFacadeDetails)
                            CaptureFacadeDetails(camera, route, directory, report);
                    }
                    if (includeWallStories)
                        CaptureWallStories(camera, route, directory, report, direction == -1);
                }
                finally { Object.DestroyImmediate(route); }
            }
            File.WriteAllText(Path.Combine(directory, "capture-report.txt"), report.ToString(), Encoding.UTF8);
            Debug.Log("STACKED_CITY_CAPTURE_OK " + directory);
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = previousAsync;
            // ApplyAtmosphere also configures desktop shadows. Unlike the
            // temporary scene's RenderSettings, these values belong to the
            // project quality tier and must be restored even when capture fails.
            qualityState.Restore();
            if (originalScene.IsValid() && originalScene.isLoaded)
                SceneManager.SetActiveScene(originalScene);
            if (captureScene.IsValid() && captureScene.isLoaded)
                EditorSceneManager.CloseScene(captureScene, true);
            foreach (var pair in rootStates)
                if (pair.Key != null) pair.Key.SetActive(pair.Value);
            if (openedSource && sourceScene.IsValid() && sourceScene.isLoaded)
                EditorSceneManager.CloseScene(sourceScene, true);
        }
    }

    private sealed class CaptureQualityState
    {
        private readonly ShadowQuality shadows = QualitySettings.shadows;
        private readonly ShadowResolution resolution = QualitySettings.shadowResolution;
        private readonly ShadowProjection projection = QualitySettings.shadowProjection;
        private readonly float distance = QualitySettings.shadowDistance;
        private readonly int cascades = QualitySettings.shadowCascades;
        private readonly Vector3 cascadeSplit = QualitySettings.shadowCascade4Split;
        private readonly int pixelLights = QualitySettings.pixelLightCount;

        public void Restore()
        {
            QualitySettings.shadows = shadows;
            QualitySettings.shadowResolution = resolution;
            QualitySettings.shadowProjection = projection;
            QualitySettings.shadowDistance = distance;
            QualitySettings.shadowCascades = cascades;
            QualitySettings.shadowCascade4Split = cascadeSplit;
            QualitySettings.pixelLightCount = pixelLights;
        }
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        throw new InvalidOperationException("Gameplay scene root missing: " + name);
    }

    private static Object RequireResource(string path, Type type)
    {
        Object asset = Resources.Load(path, type);
        if (asset == null) throw new FileNotFoundException("Missing review resource: " + path);
        return asset;
    }

    private static void SpawnCity(Transform viewer)
    {
        // Run the actual grid initializers, including per-cell traffic phase.
        for (int index = 0; index < 4; index++)
            RequireResource("CityV7/StackedBlock" + index, typeof(GameObject));
        RequireResource("CityV7/UpperTransit", typeof(GameObject));
        foreach (string name in new[] { "CityLowerDistrict", "CityUpperTransit" })
        {
            Type type = Type.GetType(name + ", TempleRun.Runtime", true);
            Component grid = new GameObject("Review" + name).AddComponent(type);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetField("viewer", flags).SetValue(grid, viewer);
            type.GetMethod("Start", flags).Invoke(grid, null);
        }
    }

    private static GameObject SpawnRoute(int direction, WorldStyler styler)
    {
        var route = new GameObject("ReviewRoute");
        for (int i = -1; i < 4; i++)
            SpawnSegment("TrackSegment", new Vector3(0, 0, i * 20), Quaternion.identity,
                i * 20, TrackSegmentType.Straight, route.transform, styler);
        SpawnSegment(direction < 0 ? "TurnSegment_Left" : "TurnSegment_Right", new Vector3(0, 0, 70),
            Quaternion.identity, 80, direction < 0 ? TrackSegmentType.TurnLeft : TrackSegmentType.TurnRight, route.transform, styler);
        for (int i = 1; i <= 7; i++)
            SpawnSegment("TrackSegment", new Vector3(direction * i * 20, 0, 80),
                Quaternion.Euler(0, direction * 90, 0), 80 + i * 20, TrackSegmentType.Straight, route.transform, styler);
        return route;
    }

    private static void SpawnSegment(string name, Vector3 position, Quaternion rotation,
        float distance, TrackSegmentType type, Transform parent, WorldStyler styler)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
        if (prefab == null) throw new FileNotFoundException("Missing road prefab: " + name);
        GameObject segment = Object.Instantiate(prefab, position, rotation, parent);
        segment.SetActive(true);
        TrackSegmentData data = segment.GetComponent<TrackSegmentData>();
        data.segmentType = type;
        data.routeDistance = distance;
        data.entryDirection = rotation * Vector3.forward;
        data.exitDirection = type == TrackSegmentType.Straight ? data.entryDirection
            : Vector3.right * (type == TrackSegmentType.TurnRight ? 1 : -1);
        data.turnPointWorld = position + data.entryDirection * 10f;
        CityV7PlayableEnvironment.Decorate(segment, type);
        // Match the current WorldStyler art binding, including straight and turn
        // decks. Capturing the legacy fallback road gave misleading comparisons.
        OrangeEchoRoadVisuals.Apply(segment, type);
        if (type != TrackSegmentType.Straight) return;
        for (int row = 0; row < 3; row++)
        {
            int lane = (Mathf.RoundToInt(distance / 20f) + row + 30) % 3;
            Vector3 local = new Vector3(TrackGeometryStandards.GetLaneCenter(lane), 1.25f, -5 + row * 5);
            var pickup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Coin.prefab"),
                segment.transform.TransformPoint(local), rotation, segment.transform);
            pickup.SetActive(true);
            styler.StyleCoin(pickup);
        }
        if (Mathf.RoundToInt(distance / 20f) % 3 == 0)
        {
            var obstacle = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Obstacle_Low.prefab"),
                segment.transform.TransformPoint(new Vector3(-3, 0, 7)), rotation, segment.transform);
            obstacle.SetActive(true);
            styler.StyleObstacle(obstacle);
        }
    }

    internal static void CaptureView(Camera camera, GameObject player, Vector3 offset, Vector3 anchor,
        Vector3 forward, float seconds, string directory, string name, StringBuilder report)
    {
        player.transform.SetPositionAndRotation(anchor, Quaternion.LookRotation(forward));
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        camera.transform.position = anchor + forward * offset.z + Vector3.up * offset.y + right * offset.x;
        camera.transform.LookAt(anchor + forward * 5f);
        foreach (MonoBehaviour component in Object.FindObjectsOfType<MonoBehaviour>())
        {
            string typeName = component.GetType().Name;
            if (typeName == "CityLowerDistrict" || typeName == "CityUpperTransit")
            {
                component.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(component, null);
                continue;
            }
            if (component.GetType().Name != "CityTransitLoop") continue;
            MethodInfo sample = component.GetType().GetMethod("Sample", new[] { typeof(float) });
            if (sample == null) throw new MissingMethodException("CityTransitLoop.Sample(float)");
            sample.Invoke(component, new object[] { seconds });
        }
        foreach (EchoCoinVisual coin in Object.FindObjectsOfType<EchoCoinVisual>())
            coin.ApplyViewFacingRotation(Coin.ResolveViewFacingRotation(
                coin.transform.position, camera.transform.position, Quaternion.identity, 0f));
        RenderImage(camera, directory, name);
        report.AppendLine(name + " | anchor=" + anchor + " | forward=" + forward + " | trafficSeconds=" + seconds);
    }

    private static void CaptureCornerLanes(Camera camera, GameObject player, Vector3 offset, float anchorY,
        int direction, string directory, StringBuilder report)
    {
        string side = direction < 0 ? "left" : "right";
        Vector3 sweepForward = Quaternion.Euler(0, direction * 55f, 0) * Vector3.forward;
        var samples = new[]
        {
            (name: "straight", anchor: new Vector3(0f, anchorY, 42f), forward: Vector3.forward),
            (name: "approach", anchor: new Vector3(0f, anchorY, 70f), forward: Vector3.forward),
            (name: "sweep", anchor: new Vector3(direction * 4f, anchorY, 80f), forward: sweepForward),
            (name: "exit", anchor: new Vector3(direction * 28f, anchorY, 80f), forward: Vector3.right * direction)
        };
        foreach (var sample in samples)
        foreach (int lane in new[] { 0, 1, 2 })
        {
            Vector3 right = Vector3.Cross(Vector3.up, sample.forward);
            Vector3 anchor = sample.anchor + right * TrackGeometryStandards.GetLaneCenter(lane);
            string name = side + "-" + sample.name + "-lane-" + lane;
            CaptureView(camera, player, offset, anchor, sample.forward, 4f, directory, name, report);
            if (lane == 1) continue;
            // These downward views deliberately move only the inspection camera.
            // They show building bases and street continuity, not a new gameplay camera.
            float edgeSign = lane == 0 ? -1f : 1f;
            Vector3 inspectionOrigin = sample.anchor + right * edgeSign
                * (TrackGeometryStandards.VisualRoadHalfWidth + .75f) + Vector3.up * 2f;
            Vector3 inspectionTarget = sample.anchor + right * edgeSign * 17f
                + sample.forward * 8f + Vector3.down * 22f;
            camera.transform.position = inspectionOrigin;
            camera.transform.LookAt(inspectionTarget);
            RenderImage(camera, directory, name + "-inspection-down");
            report.AppendLine(name + "-inspection-down | inspection camera only; anchor=" + anchor
                + " | inspectionOrigin=" + inspectionOrigin + " | lookAt=" + inspectionTarget);
        }
    }

    private static void CaptureFacadeDetails(Camera camera, GameObject route, string directory, StringBuilder report)
    {
        // Inspect an actual visible, decorated gameplay chunk from its road-facing
        // side. These additional views show material response at useful scale;
        // the ordinary gameplay camera samples remain the acceptance evidence.
        TrackSegmentData nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (TrackSegmentData segment in route.GetComponentsInChildren<TrackSegmentData>())
        {
            if (segment.segmentType != TrackSegmentType.Straight) continue;
            float distance = Vector3.SqrMagnitude(segment.transform.position - new Vector3(0f, 0f, 40f));
            if (distance >= nearestDistance) continue;
            nearest = segment;
            nearestDistance = distance;
        }
        if (nearest == null) throw new InvalidOperationException("No gameplay segment for facade inspection.");
        Transform environment = nearest.transform.Find("CityV7Environment");
        if (environment == null) throw new InvalidOperationException("Gameplay chunk missing for facade inspection.");
        foreach (int side in new[] { -1, 1 })
        {
            Transform selected = null;
            Bounds selectedBounds = default;
            float closestFace = float.MaxValue;
            foreach (Transform building in environment)
            {
                if (!building.gameObject.activeInHierarchy || !building.name.StartsWith("V7_", StringComparison.Ordinal)) continue;
                Renderer[] renderers = building.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                if (Mathf.Sign(bounds.center.x) != side || bounds.size.y < 8f) continue;
                float face = side < 0 ? -bounds.max.x : bounds.min.x;
                if (face >= closestFace) continue;
                closestFace = face;
                selected = building;
                selectedBounds = bounds;
            }
            if (selected == null) continue;
            float facadeX = side < 0 ? selectedBounds.max.x : selectedBounds.min.x;
            float height = Mathf.Clamp(5.5f, selectedBounds.min.y + 3f, selectedBounds.max.y - 3f);
            Vector3 focus = new Vector3(facadeX, height + 1f, selectedBounds.center.z);
            camera.transform.position = new Vector3(facadeX - side * 11f, height, selectedBounds.center.z - 5f);
            camera.transform.LookAt(focus);
            string name = side < 0 ? "facade-detail-left" : "facade-detail-right";
            RenderImage(camera, directory, name);
            report.AppendLine(name + " | detail inspection of actual gameplay chunk; building=" + selected.name
                + " | camera=" + camera.transform.position + " | focus=" + focus);
        }
    }

    private static void CaptureWallStories(Camera camera, GameObject route, string directory, StringBuilder report, bool firstRoute)
    {
        // These EditorOnly anchors identify modules already fitted to a real
        // gameplay building. Only the inspection camera moves; no wall is
        // relocated or isolated into a separate beauty scene.
        string[] modules = { "WallDistrictA", "WallDistrictB", "WallTransit", "WallService", "WallMemory", "WallGround", "WallCat" };
        float[] widths = { 2.4f, 2.4f, 3.2f, 1.4f, 2.6f, 2.6f, .9f };
        float[] heights = { 6.8f, 6.8f, 1.8f, 1.6f, 2.2f, 2.2f, 1f };
        modules = modules.Concat(StackedCityWallCatalog.ModelNames).ToArray();
        widths = widths.Concat(StackedCityWallCatalog.ModelNames.Select(n => n.StartsWith("DistrictSign") ? 2.4f : 2.6f)).ToArray();
        heights = heights.Concat(StackedCityWallCatalog.ModelNames.Select(n => n.StartsWith("DistrictSign") ? 6.8f : 2.2f)).ToArray();
        foreach (string module in modules)
        {
            string oldCapture = Path.Combine(directory, "wall-story-" + module + ".png");
            if (firstRoute && File.Exists(oldCapture)) File.Delete(oldCapture);
        }
        Vector3 routeFocus = new Vector3(0f, 5f, 40f);
        Transform[] candidates = route.GetComponentsInChildren<Transform>();
        MeshFilter[] visibleMeshes = Object.FindObjectsOfType<MeshFilter>();
        MethodInfo intersect = typeof(HandleUtility).GetMethod("IntersectRayMesh",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
            new[] { typeof(Ray), typeof(Mesh), typeof(Matrix4x4), typeof(RaycastHit).MakeByRefType() }, null);
        if (intersect == null)
        {
            report.AppendLine("Wall story closeups skipped: editor mesh visibility query unavailable.");
            return;
        }
        int captured = 0;
        for (int module = 0; module < modules.Length; module++)
        {
            // Keep a verified first-route view and let the other turn direction
            // supply identities that its buildings reveal more clearly.
            if (!firstRoute && File.Exists(Path.Combine(directory, "wall-story-" + modules[module] + ".png"))) continue;
            Transform selected = null;
            float selectedScale = 1f;
            var matching = new List<Transform>();
            foreach (Transform anchor in candidates)
            {
                if (!anchor.name.StartsWith("Story_" + modules[module], StringComparison.Ordinal)
                    || !anchor.CompareTag("EditorOnly")) continue;
                matching.Add(anchor);
            }
            matching.Sort((a, b) =>
            {
                if (modules[module] == "WallDistrictA")
                {
                    int portalA = a.parent.parent.name == "OverhangTail" ? 0 : 1;
                    int portalB = b.parent.parent.name == "OverhangTail" ? 0 : 1;
                    if (portalA != portalB) return portalA.CompareTo(portalB);
                }
                return Vector3.SqrMagnitude(a.position - routeFocus).CompareTo(Vector3.SqrMagnitude(b.position - routeFocus));
            });
            // Keep inspection bounded: at most twelve real placements and five
            // camera offsets per family. Reject foreground occlusion before render.
            for (int candidate = 0; candidate < Mathf.Min(12, matching.Count) && selected == null; candidate++)
            {
                Transform anchor = matching[candidate];
                float scale = Mathf.Max(anchor.lossyScale.x, anchor.lossyScale.y, anchor.lossyScale.z);
                float width = widths[module] * scale, height = heights[module] * scale;
                float distanceToWall = Mathf.Max(height * .72f, width * .5f)
                    / Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) + .6f;
                distanceToWall = Mathf.Clamp(distanceToWall, 2.5f, 18f);
                foreach (float lateral in new[] { .12f, .45f, -.45f, 0f, -.12f })
                {
                    Vector3 position = anchor.position + anchor.forward * distanceToWall
                        + anchor.right * width * lateral + Vector3.up * height * .035f;
                    if (candidate == 0 && lateral == .12f)
                        WriteWallStoryDiagnostics(modules[module], position, anchor, width, height, visibleMeshes, intersect, report);
                    if (!WallStoryHasClearSight(position, anchor, width, height, visibleMeshes, intersect, out string blocker))
                    {
                        report.AppendLine("wall-story-" + modules[module] + " | rejected occluded view of "
                            + anchor.parent.parent.name + " | blocker=" + blocker);
                        continue;
                    }
                    selected = anchor;
                    selectedScale = scale;
                    camera.transform.position = position;
                    camera.transform.LookAt(anchor.position);
                    break;
                }
            }
            if (selected == null)
            {
                report.AppendLine("wall-story-" + modules[module] + " | no unobstructed fitted module in bounded route sample; not captured");
                continue;
            }
            string name = "wall-story-" + modules[module];
            RenderImage(camera, directory, name);
            report.AppendLine(name + " | detail inspection of fitted gameplay module; parent=" + selected.parent.parent.name
                + " | anchor=" + selected.position + " | outward=" + selected.forward
                + " | camera=" + camera.transform.position + " | scale=" + selectedScale + " | 63 mesh-ray samples clear");
            captured++;
        }
        report.AppendLine("Wall story detail captures=" + captured
            + "; closeups verify fitted art/text, not readability from the running camera.");
    }

    private static bool WallStoryHasClearSight(Vector3 camera, Transform anchor, float width, float height,
        MeshFilter[] meshes, MethodInfo intersect, out string blocker)
    {
        // Thin foreground railings can pass between centre/corner samples and
        // hide a Chinese character. Sample the interior as well as the edges.
        for (int row = 0; row <= 8; row++)
        for (int column = 0; column <= 6; column++)
        {
            Vector2 sample = new Vector2(Mathf.Lerp(-.46f, .46f, column / 6f), Mathf.Lerp(-.46f, .46f, row / 8f));
            // Test almost to the mount, excluding the story's own geometry.
            // A generous front offset can miss a pier face covering the plaque.
            Vector3 target = anchor.position + anchor.forward * .005f + anchor.right * (width * sample.x)
                + anchor.up * (height * sample.y);
            Vector3 delta = target - camera;
            var ray = new Ray(camera, delta.normalized);
            foreach (MeshFilter filter in meshes)
            {
                Renderer renderer = filter.GetComponent<Renderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled
                    || filter.transform.IsChildOf(anchor.parent)
                    || !renderer.gameObject.activeInHierarchy || !renderer.bounds.IntersectRay(ray, out float boundsDistance)
                    || boundsDistance >= delta.magnitude) continue;
                object[] arguments = { ray, filter.sharedMesh, filter.transform.localToWorldMatrix, default(RaycastHit) };
                if (!(bool)intersect.Invoke(null, arguments) || ((RaycastHit)arguments[3]).distance >= delta.magnitude - .01f) continue;
                blocker = renderer.name;
                return false;
            }
        }
        blocker = null;
        return true;
    }

    private static void WriteWallStoryDiagnostics(string module, Vector3 camera, Transform anchor, float width,
        float height, MeshFilter[] meshes, MethodInfo intersect, StringBuilder report)
    {
        report.AppendLine("story-diagnostic " + module + " | building=" + anchor.parent.parent.name + " | anchor="
            + anchor.position + " | local=" + anchor.localPosition + " | outward=" + anchor.forward + " | scale=" + anchor.lossyScale);
        foreach (MeshFilter filter in anchor.parent.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            Vector3[] vertices = filter.sharedMesh.vertices;
            Vector3[] normals = filter.sharedMesh.normals;
            Matrix4x4 normalMatrix = filter.transform.localToWorldMatrix.inverse.transpose;
            float minDepth = float.PositiveInfinity, maxDepth = float.NegativeInfinity;
            float minNormal = float.PositiveInfinity, maxNormal = float.NegativeInfinity;
            int count = 0;
            Bounds bounds = default;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = filter.transform.TransformPoint(vertices[i]);
                Vector3 relative = point - anchor.position;
                if (Mathf.Abs(Vector3.Dot(relative, anchor.right)) > width * .5f + .1f
                    || Mathf.Abs(Vector3.Dot(relative, anchor.up)) > height * .5f + .1f) continue;
                float depth = Vector3.Dot(relative, anchor.forward);
                minDepth = Mathf.Min(minDepth, depth); maxDepth = Mathf.Max(maxDepth, depth);
                if (normals.Length == vertices.Length)
                {
                    float normal = Vector3.Dot(normalMatrix.MultiplyVector(normals[i]).normalized, anchor.forward);
                    minNormal = Mathf.Min(minNormal, normal); maxNormal = Mathf.Max(maxNormal, normal);
                }
                if (count++ == 0) bounds = new Bounds(point, Vector3.zero); else bounds.Encapsulate(point);
            }
            report.AppendLine("story-vertices " + filter.name + " | inside marker footprint=" + count
                + " | worldBounds=" + bounds + " | outwardDepth=" + minDepth + ".." + maxDepth
                + " | outwardNormalDot=" + minNormal + ".." + maxNormal);
        }
        Vector3 delta = anchor.position - camera;
        var ray = new Ray(camera, delta.normalized);
        var hits = new List<(float distance, string name)>();
        foreach (MeshFilter filter in meshes)
        {
            Renderer renderer = filter.GetComponent<Renderer>();
            if (filter.sharedMesh == null || renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                || !renderer.bounds.IntersectRay(ray, out float boundsDistance) || boundsDistance > delta.magnitude + 1f) continue;
            object[] arguments = { ray, filter.sharedMesh, filter.transform.localToWorldMatrix, default(RaycastHit) };
            if (!(bool)intersect.Invoke(null, arguments)) continue;
            float distance = ((RaycastHit)arguments[3]).distance;
            if (distance > delta.magnitude + 1f) continue;
            hits.Add((distance, filter.transform.parent.name + "/" + filter.name));
        }
        hits.Sort((a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < Mathf.Min(8, hits.Count); i++)
            report.AppendLine("story-center-hit " + hits[i].name + " | distance=" + hits[i].distance
                + " | anchorDistance=" + delta.magnitude + " | outwardOfMount="
                + Vector3.Dot(ray.GetPoint(hits[i].distance) - anchor.position, anchor.forward));
    }

    internal static void RenderImage(Camera camera, string directory, string name)
    {
        if (name == "right-straight")
        {
            var lines = new List<string>();
            foreach (Vector2 point in new[] { new Vector2(.3125f,.778f), new Vector2(.675f,.81f) })
            {
                Ray ray = camera.ViewportPointToRay(point);
                var hits = new List<(float distance, Renderer renderer)>();
                foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>())
                    if (renderer.enabled && renderer.bounds.IntersectRay(ray, out float distance))
                        hits.Add((distance,renderer));
                hits.Sort((a,b) => a.distance.CompareTo(b.distance));
                lines.Add("viewport="+point+" ray="+ray);
                for (int i=0; i<Mathf.Min(10,hits.Count); i++)
                {
                    var hit=hits[i];
                    string path=hit.renderer.name;
                    for(Transform node=hit.renderer.transform.parent;node!=null;node=node.parent) path=node.name+"/"+path;
                    string mats="";
                    foreach(Material material in hit.renderer.sharedMaterials) mats+=(material!=null?material.name:"null")+" ";
                    lines.Add(hit.distance+" | "+path+" | "+mats+" | "+hit.renderer.bounds);
                }
            }
            File.WriteAllLines(Path.Combine(directory,"foreground-material-report.txt"),lines);
        }
        const int width = 1600, height = 900;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
        }
    }

    private static void WriteAssetStats(StringBuilder report)
    {
        foreach (string resource in new[] { "StackedBlock0", "StackedBlock1", "StackedBlock2", "StackedBlock3", "UpperTransit" })
        {
            GameObject prefab = (GameObject)RequireResource("CityV7/" + resource, typeof(GameObject));
            int vertices = 0;
            long triangles = 0;
            var materials = new HashSet<Material>();
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                vertices += mesh.vertexCount;
                for (int sub = 0; sub < mesh.subMeshCount; sub++) triangles += mesh.GetIndexCount(sub) / 3;
            }
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            foreach (Material material in renderer.sharedMaterials) materials.Add(material);
            report.AppendLine(resource + " | renderers=" + renderers.Length + " | vertices=" + vertices
                + " | triangles=" + triangles + " | materials=" + materials.Count
                + " | colliders=" + prefab.GetComponentsInChildren<Collider>(true).Length);
        }
    }

    [MenuItem("Tools/Echo Runner/Stacked City/Build Review Player")]
    public static void Build()
    {
        BuildInto(Output, "EchoRun-StackedCity-Review");
    }

    [MenuItem("Tools/Echo Runner/Stacked City Polish/Build Review Player")]
    public static void PolishBuild()
    {
        BuildInto(PolishOutput, "EchoRun-StackedCity-Polish");
    }

    [MenuItem("Tools/Echo Runner/Stacked City Wall Details/Build Review Player")]
    public static void WallBuild()
    {
        BuildInto(WallOutput, "EchoRun-StackedCity-WallDetails");
    }

    [MenuItem("Tools/Echo Runner/Stacked City Continuity/Build Review Player")]
    public static void CornerBuild()
    {
        BuildInto(CornerOutput, "EchoRun-StackedCity-Continuity");
    }

    internal static void BuildInto(string output, string reviewProductName)
    {
        string executable = Path.GetFullPath(output + "/Windows/EchoRun.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executable));
        string productName = PlayerSettings.productName;
        try
        {
            // Isolate QA saves; restore project identity immediately after build.
            PlayerSettings.productName = reviewProductName;
            BuildReport report = BuildPipeline.BuildPlayer(new[] { GameplayScene }, executable,
                BuildTarget.StandaloneWindows64, BuildOptions.Development | BuildOptions.CompressWithLz4HC);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Stacked city review build failed: " + report.summary.result);
            Debug.Log("STACKED_CITY_BUILD_OK " + executable);
        }
        finally { PlayerSettings.productName = productName; }
    }

    [MenuItem("Tools/Echo Runner/Stacked City/Capture And Build Review Player")]
    public static void CaptureAndBuild()
    {
        Capture();
        Build();
    }

    [MenuItem("Tools/Echo Runner/Stacked City Polish/Capture And Build Review Player")]
    public static void PolishCaptureAndBuild()
    {
        PolishCaptureAfter();
        PolishBuild();
    }

    [MenuItem("Tools/Echo Runner/Stacked City Wall Details/Capture And Build Review Player")]
    public static void WallCaptureAndBuild()
    {
        WallCaptureAfter();
        WallBuild();
    }

    [MenuItem("Tools/Echo Runner/Stacked City Continuity/Capture And Build Review Player")]
    public static void CornerCaptureAndBuild()
    {
        CornerCaptureAfter();
        CornerBuild();
    }
}
