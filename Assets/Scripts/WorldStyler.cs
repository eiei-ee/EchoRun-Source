using UnityEngine;

public struct EchoWorldPhasePalette
{
    public Color fog;
    public Color ambientSky;
    public Color ambientEquator;
    public Color ambientGround;
    public Color skyTint;
    public Color keyLight;
    public Color fillLight;
    public Color structure;
    public Color structureEmission;
    public Color depth;
    public Color depthEmission;
    public Color cyan;
    public Color cyanEmission;
    public Color coral;
    public Color coralEmission;
    public Color gold;
    public Color goldEmission;
}

public class WorldStyler : MonoBehaviour
{
    public const string SeamlessSkyShaderName = "EchoRun/SeamlessPanoramicSky";
    public const float StructureMetallic = 0.16f;
    public const float StructureSmoothness = 0.30f;
    public const float HighFillLightIntensity = 0.43f;
    public const float HighReflectionIntensity = 0.24f;
    public const float KeyLightIntensity = 1.13f;
    public const float SkyExposure = 0.72f;
    public const float SkySaturation = 0.22f;
    public const string SideEnergyStationResourcePath =
        "Art/Environment/EchoSideEnergyStation";
    public const string MegacityDistrictAResourcePath =
        "Art/Environment/EchoMegacityDistrictA";
    public const string MegacityDistrictBResourcePath =
        "Art/Environment/EchoMegacityDistrictB";
    public const string ColdWhiteFortressStraight00ResourcePath =
        "Art/Environment/ColdWhiteMemoryFortress/Sample_Straight_00";
    public const string ColdWhiteFortressTurnRight20ResourcePath =
        "Art/Environment/ColdWhiteMemoryFortress/Sample_TurnRight_20";
    public const string ColdWhiteFortressStraight40ResourcePath =
        "Art/Environment/ColdWhiteMemoryFortress/Sample_Straight_40";
    public const string ColdWhiteFortressPhaseAccentMaterialName =
        "ColdWhiteFortress_PhaseAccent";

    private static readonly Color BaseFog = CityV7PlayableEnvironment.FogColor;
    private static readonly Color BaseAmbientSky = CityV7PlayableEnvironment.AmbientSkyColor;
    private static readonly Color BaseAmbientEquator = CityV7PlayableEnvironment.AmbientEquatorColor;
    private static readonly Color BaseAmbientGround = CityV7PlayableEnvironment.AmbientGroundColor;
    private static readonly Color BaseSkyTint = CityV7PlayableEnvironment.SkyTint;
    private static readonly Color BaseKeyLight = CityV7PlayableEnvironment.KeyLightColor;
    private static readonly Color BaseFillLight = CityV7PlayableEnvironment.FillLightColor;
    private static readonly Color BaseStructure = new Color32(103, 128, 159, 255);
    private static readonly Color BaseStructureEmission = new Color(0.009f, 0.006f, 0.014f);
    private static readonly Color BaseDepth = EchoRunUITheme.Backdrop;
    private static readonly Color BaseDepthEmission = new Color(0.004f, 0.003f, 0.007f);
    // Keep legacy material family names so authored prefab bindings remain stable.
    private static readonly Color BaseCyan = EchoRunUITheme.Echo;
    private static readonly Color BaseCyanEmission = new Color(0.085f, 0.035f, 0.12f);
    private static readonly Color BaseCoral = EchoRunUITheme.Danger;
    private static readonly Color BaseCoralEmission = new Color(0.12f, 0.018f, 0.045f);
    private static readonly Color BaseGold = EchoRunUITheme.ActionAccent;
    private static readonly Color BaseGoldEmission = new Color(0.08f, 0.095f, 0.018f);

    public static WorldStyler Instance { get; private set; }

    private Material _structureMaterial;
    private Material _deepStructureMaterial;
    private Material _cyanMaterial;
    private Material _coralMaterial;
    private Material _goldMaterial;
    private Material _fortressPhaseAccentMaterial;
    private Material _skyMaterial;
    private GameObject _sideEnergyStationPrefab;
    private GameObject _megacityDistrictAPrefab;
    private GameObject _megacityDistrictBPrefab;
    private GameObject _coldWhiteFortressStraight00Prefab;
    private GameObject _coldWhiteFortressTurnRight20Prefab;
    private GameObject _coldWhiteFortressStraight40Prefab;
    private Light _keyLight;
    private Light _fillLight;
    private Vector2Int _lastCameraScreenSize;
    private GameObject _homeCityPreview;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<WorldStyler>() != null) return;
        new GameObject("WorldStyler").AddComponent<WorldStyler>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsurePalette();
        ConfigureAtmosphere();
        VisualQualityController.Changed += ApplyVisualQuality;
    }

    void Start()
    {
        ApplyCameraLayout(true);

        ConfigureLighting();
        CityV7PlayableEnvironment.ApplyAtmosphere(_keyLight, _fillLight);

        GameObject floor = GameObject.Find("Plane");
        Renderer floorRenderer = floor != null ? floor.GetComponent<Renderer>() : null;
        if (floorRenderer != null)
            EchoRoadVisualController.Instance.ApplyTo(
                floorRenderer, RoadSurfaceRole.StartDeck);

        BuildStartDeck();
        StyleCharacter();
        StartCoroutine(CreateHomeCityPreview());
    }

    void Update()
    {
        ApplyCameraLayout(false);
        if (_homeCityPreview != null)
        {
            bool visible = GameManager.Instance != null && GameManager.Instance.State == GameState.Menu;
            if (_homeCityPreview.activeSelf != visible) _homeCityPreview.SetActive(visible);
        }
    }

    private System.Collections.IEnumerator CreateHomeCityPreview()
    {
        // The menu streets have no collision and disappear before the live
        // track starts using its own pool. Anchor them to the physical floor,
        // not the first animation pose: the runner can still be settling.
        yield return null;
        GameObject prefab = Resources.Load<GameObject>("Art/Menu/CityHomePreview");
        if (prefab == null) yield break;
        _homeCityPreview = Instantiate(prefab);
        _homeCityPreview.name = "HomeCityPreview";
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null && Physics.Raycast(player.transform.position + Vector3.up,
                Vector3.down, out RaycastHit ground, 10f, player.groundLayer,
                QueryTriggerInteraction.Ignore))
        {
            // The authored deck top is local Y=0.10. Leave a small separation
            // from the existing floor to avoid coincident surfaces.
            _homeCityPreview.transform.position = Vector3.up * (ground.point.y - .08f);
        }
        _homeCityPreview.SetActive(GameManager.Instance != null
            && GameManager.Instance.State == GameState.Menu);
    }

    private void ApplyCameraLayout(bool force)
    {
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
        if (!force && screenSize == _lastCameraScreenSize) return;
        _lastCameraScreenSize = screenSize;
        Camera camera = Camera.main;
        if (camera == null) return;

        bool portrait = UILayoutRules.IsCompactPortrait(
            screenSize.x, screenSize.y);
        camera.clearFlags = _skyMaterial != null
            ? CameraClearFlags.Skybox
            : CameraClearFlags.SolidColor;
        // Extend visibility for the authored horizon only; follow position,
        // lane framing and FOV retain their existing gameplay values.
        camera.farClipPlane = 420f;
        camera.fieldOfView = GetCameraFieldOfView(portrait);
        camera.backgroundColor = new Color32(20, 13, 36, 255);
        CameraFollow follow = camera.GetComponent<CameraFollow>();
        if (follow != null) follow.offset = GetCameraOffset(portrait);
    }

    public static float GetCameraFieldOfView(bool portrait)
    {
        return portrait ? 62f : 56f;
    }

    public static Vector3 GetCameraOffset(bool portrait)
    {
        return portrait
            ? new Vector3(0f, 5.4f, -12f)
            : new Vector3(0f, 4.2f, -10.5f);
    }

    public void DecorateSegment(GameObject segment, TrackSegmentType segmentType,
        bool refreshCityClearance = true)
    {
        if (segment == null) return;
        if (CityV7PlayableEnvironment.Decorate(segment, segmentType, refreshCityClearance))
        {
            OrangeEchoRoadVisuals.Apply(segment, segmentType);
            return;
        }
        EnsurePalette();
        Transform existing = segment.transform.Find("EchoEnvironment");
        GameObject environment;
        EchoEnvironmentVariantSet variantSet;
        if (existing == null)
        {
            environment = new GameObject("EchoEnvironment");
            environment.transform.SetParent(segment.transform, false);
            variantSet = environment.AddComponent<EchoEnvironmentVariantSet>();
            if (segmentType == TrackSegmentType.Straight)
                BuildStraightEnvironment(environment.transform, variantSet);
            else
                BuildTurnEnvironment(environment.transform, segmentType, variantSet);
        }
        else
        {
            environment = existing.gameObject;
            variantSet = environment.GetComponent<EchoEnvironmentVariantSet>();
        }

        RemapEnvironmentPalette(environment);
        bool coldWhiteFortress = IsColdWhiteFortressEnvironment(
            environment.transform);
        if (coldWhiteFortress)
            DisableLegacyTrackVisualRenderers(segment.transform);
        if (variantSet == null) return;
        TrackSegmentData data = segment.GetComponent<TrackSegmentData>();
        float routeDistance = data != null ? data.routeDistance : 0f;
        int runSeed = GameManager.Instance != null
            ? GameManager.Instance.RunSeed : 0;
        variantSet.ApplyQuality(VisualQualityController.Current);
        bool sampleEnabled = TrackManager.Instance != null
                             && TrackManager.Instance
                                 .UsesColdWhiteFortressSample;
        int preferredVariant = TrackManager
            .ColdWhiteFortressVisualVariantIndex(segmentType,
                routeDistance, sampleEnabled);
        if (preferredVariant < 0 && coldWhiteFortress
            && segmentType == TrackSegmentType.Straight)
        {
            // The scan gate is a deliberate transition landmark, never random
            // repeating dressing. Ordinary segments select only open variants.
            preferredVariant = EchoEnvironmentVariantSet.SelectVariantIndex(
                runSeed, routeDistance, Mathf.Min(2, variantSet.VariantCount));
        }
        variantSet.SelectFor(runSeed, routeDistance, preferredVariant);
        if (segmentType != TrackSegmentType.Straight)
            CityV7PlayableEnvironment.Decorate(segment, segmentType, refreshCityClearance);
        OrangeEchoRoadVisuals.Apply(segment, segmentType);
    }

    private static bool IsColdWhiteFortressEnvironment(Transform environment)
    {
        if (environment == null) return false;
        Transform variants = environment.Find("VisualVariants");
        if (variants == null) return false;
        for (int index = 0; index < variants.childCount; index++)
        {
            if (variants.GetChild(index).name.IndexOf("ColdWhite",
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static void DisableLegacyTrackVisualRenderers(
        Transform segmentRoot)
    {
        if (segmentRoot == null) return;
        for (int index = 0; index < segmentRoot.childCount; index++)
        {
            Transform child = segmentRoot.GetChild(index);
            string childName = child.name;
            bool legacyRoadVisual = childName == "GroundPlane"
                                    || childName == "EntryStrip"
                                    || childName == "ExitStrip"
                                    || childName == TrackManager
                                        .TurnInnerCornerCapName
                                    || childName.StartsWith("LaneLine_",
                                        System.StringComparison.Ordinal)
                                    || childName.StartsWith("DataSeam",
                                        System.StringComparison.Ordinal);
            if (!legacyRoadVisual) continue;
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
        }
    }

    private void ConfigureAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = BaseFog;
        RenderSettings.fogStartDistance = 52f;
        RenderSettings.fogEndDistance = 130f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = BaseAmbientSky;
        RenderSettings.ambientEquatorColor = BaseAmbientEquator;
        RenderSettings.ambientGroundColor = BaseAmbientGround;
        ApplyVisualQuality(VisualQualityController.Current);

        // City integration replaces the buildings, not the original panoramic sky.
        _skyMaterial = CreateSeamlessSkyMaterial();
        if (_skyMaterial != null) RenderSettings.skybox = _skyMaterial;
    }

    public static Material CreateSeamlessSkyMaterial()
    {
        // The city slice uses a latitude-longitude sky with its horizon at
        // the equator. Preserve the older concept-art remap as a fallback.
        Material citySky = Resources.Load<Material>("CityV7/ExperienceSky");
        if (citySky != null)
        {
            var cityRuntimeSky = new Material(citySky);
            CityV7PlayableEnvironment.StyleSky(cityRuntimeSky);
            return cityRuntimeSky;
        }
        Material source = Resources.Load<Material>("Art/EchoSky");
        Material material = source != null && source.shader != null &&
            source.shader.name == SeamlessSkyShaderName
            ? new Material(source)
            : CreateSkyMaterialFromTexture();
        if (material == null) return null;

        material.name = "EchoSky_Seamless_Runtime";
        material.SetColor("_Tint", BaseSkyTint);
        material.SetFloat("_Exposure", SkyExposure);
        material.SetFloat("_Saturation", SkySaturation);
        material.SetFloat("_Rotation", 0f);
        material.SetFloat("_SeamBlend", 0.07f);
        material.SetFloat("_HorizonTexY", 0.24f);
        return material;
    }

    private static Material CreateSkyMaterialFromTexture()
    {
        Shader shader = Shader.Find(SeamlessSkyShaderName);
        Texture2D texture = Resources.Load<Texture2D>("Art/EchoSky");
        if (shader == null || texture == null) return null;

        Material material = new Material(shader);
        material.SetTexture("_MainTex", texture);
        return material;
    }

    private void ConfigureLighting()
    {
        // The scene's sun is stable even when authored facade lights are
        // present. FindObjectOfType can otherwise select a sign or the fill.
        _keyLight = RenderSettings.sun;
        if (_keyLight == null || _keyLight.type != LightType.Directional
            || _keyLight.name == "EchoFillLight")
        {
            _keyLight = null;
            foreach (Light candidate in FindObjectsOfType<Light>())
            {
                if (candidate.type != LightType.Directional
                    || !candidate.enabled || candidate.name == "EchoFillLight") continue;
                _keyLight = candidate;
                break;
            }
        }
        if (_keyLight == null)
        {
            GameObject sunObject = new GameObject("EchoCitySun");
            sunObject.transform.SetParent(transform, false);
            _keyLight = sunObject.AddComponent<Light>();
            _keyLight.type = LightType.Directional;
        }

        GameObject fillObject = GameObject.Find("EchoFillLight");
        if (fillObject == null)
        {
            fillObject = new GameObject("EchoFillLight");
            fillObject.transform.SetParent(transform, false);
            fillObject.transform.rotation = Quaternion.Euler(38f, 145f, 0f);
        }
        _fillLight = fillObject.GetComponent<Light>();
        if (_fillLight == null) _fillLight = fillObject.AddComponent<Light>();
        _fillLight.type = LightType.Directional;
        _fillLight.color = BaseFillLight;
        _fillLight.shadows = LightShadows.None;
        ApplyVisualQuality(VisualQualityController.Current);
    }

    private void EnsurePalette()
    {
        if (_structureMaterial != null) return;
        _structureMaterial = MakeMaterial("EchoStructure",
            BaseStructure, BaseStructureEmission,
            StructureMetallic, StructureSmoothness);
        _deepStructureMaterial = MakeMaterial("EchoDepth",
            BaseDepth, BaseDepthEmission,
            0.10f, 0.27f);
        _cyanMaterial = MakeMaterial("EchoCyan",
            BaseCyan, BaseCyanEmission, 0.12f, 0.42f);
        _coralMaterial = MakeMaterial("EchoCoral",
            BaseCoral, BaseCoralEmission, 0.10f, 0.36f);
        _goldMaterial = MakeMaterial("EchoGold",
            BaseGold, BaseGoldEmission, 0.18f, 0.42f);
        _fortressPhaseAccentMaterial = MakeMaterial(
            ColdWhiteFortressPhaseAccentMaterialName,
            BaseCyan, BaseCyanEmission, 0.12f, 0.42f);
    }

    private void BuildStraightEnvironment(Transform parent,
        EchoEnvironmentVariantSet variantSet)
    {
        if (TryBuildColdWhiteStraightEnvironment(parent, variantSet)) return;

        GameObject common = new GameObject("Common");
        common.transform.SetParent(parent, false);
        CreateCapsule("LeftIsland", common.transform, new Vector3(-11.7f, -1.52f, 0f),
            new Vector3(2.15f, 9.7f, 0.58f), _deepStructureMaterial,
            new Vector3(90f, 0f, 0f));
        CreateCapsule("RightIsland", common.transform, new Vector3(11.7f, -1.52f, 0f),
            new Vector3(2.15f, 9.7f, 0.58f), _deepStructureMaterial,
            new Vector3(90f, 0f, 0f));
        CreateBeam("LeftRail", common.transform,
            new Vector3(-TrackGeometryStandards.EdgeRailOffset, 0.24f, -9.7f),
            new Vector3(-TrackGeometryStandards.EdgeRailOffset, 0.24f, 9.7f),
            0.09f, _cyanMaterial);
        CreateBeam("RightRail", common.transform,
            new Vector3(TrackGeometryStandards.EdgeRailOffset, 0.24f, -9.7f),
            new Vector3(TrackGeometryStandards.EdgeRailOffset, 0.24f, 9.7f),
            0.09f, _cyanMaterial);
        BuildGroundBollards(common.transform);

        GameObject visualVariants = new GameObject("VisualVariants");
        visualVariants.transform.SetParent(parent, false);
        GameObject cityLeft = CreateVariant("Variant_A_CityLeft",
            visualVariants.transform);
        BuildMegacityDistrict(cityLeft.transform, false, -1, 0f, 0.96f);
        BuildSideEnergyStation(cityLeft.transform, 1, 5.2f, 0.94f);

        GameObject cityRight = CreateVariant("Variant_B_CityRight",
            visualVariants.transform);
        BuildMegacityDistrict(cityRight.transform, true, 1, 0f, 0.96f);
        BuildSignalArch(cityRight.transform, 5.5f);

        GameObject cityGate = CreateVariant("Variant_C_CityGate",
            visualVariants.transform);
        BuildMegacityDistrict(cityGate.transform, true, -1, 2.2f, 0.88f);
        BuildMegacityDistrict(cityGate.transform, false, 1, -2.2f, 0.88f);

        GameObject highQualityOnly = new GameObject("HighQualityOnly");
        highQualityOnly.transform.SetParent(parent, false);
        variantSet.Initialize(new[] { cityLeft, cityRight, cityGate },
            highQualityOnly);
    }

    private void BuildStartDeck()
    {
        if (GameObject.Find("EchoStartDeck") != null) return;

        GameObject deck = new GameObject("EchoStartDeck");
        GameObject launchRoad = CreateCube("LaunchRoad", deck.transform,
            new Vector3(0f, -0.22f, 34f),
            new Vector3(TrackGeometryStandards.VisualRoadWidth, 0.28f, 78f),
            _structureMaterial);
        BoxCollider launchCollider = launchRoad.GetComponent<BoxCollider>();
        if (launchCollider != null)
        {
            launchCollider.size = new Vector3(
                TrackGeometryStandards.WalkableWidth
                / TrackGeometryStandards.VisualRoadWidth, 1f, 1f);
        }
        EchoRoadVisualController.Instance.ApplyTo(
            launchRoad.GetComponent<Renderer>(), RoadSurfaceRole.StartDeck);
        CreateBeam("LaunchRailL", deck.transform,
            new Vector3(-TrackGeometryStandards.EdgeRailOffset, 0.16f, -5f),
            new Vector3(-TrackGeometryStandards.EdgeRailOffset, 0.16f, 73f),
            0.09f, _structureMaterial);
        CreateBeam("LaunchRailR", deck.transform,
            new Vector3(TrackGeometryStandards.EdgeRailOffset, 0.16f, -5f),
            new Vector3(TrackGeometryStandards.EdgeRailOffset, 0.16f, 73f),
            0.09f, _structureMaterial);
        CreateCapsule("LaunchIslandL", deck.transform, new Vector3(-12f, -1.25f, 34f),
            new Vector3(3.5f, 38f, 0.82f), _deepStructureMaterial,
            new Vector3(90f, 0f, 0f));
        CreateCapsule("LaunchIslandR", deck.transform, new Vector3(12f, -1.25f, 34f),
            new Vector3(3.5f, 38f, 0.82f), _deepStructureMaterial,
            new Vector3(90f, 0f, 0f));

        // Keep the opening stretch visually quiet. Authored sample landmarks own
        // the transition gate, so the launch deck must not add a second doorway.
        for (int side = -1; side <= 1; side += 2)
        {
            BuildPylon(deck.transform, side, 35f, 7.2f, side < 0);
            BuildPylon(deck.transform, side, 58f, 9.5f, side > 0);
        }
    }

    private void BuildTurnEnvironment(Transform parent,
        TrackSegmentType segmentType, EchoEnvironmentVariantSet variantSet)
    {
        if (segmentType == TrackSegmentType.TurnRight
            && TryBuildColdWhiteTurnRightEnvironment(parent, variantSet))
            return;

        int direction = segmentType == TrackSegmentType.TurnRight ? 1 : -1;
        GameObject common = new GameObject("Common");
        common.transform.SetParent(parent, false);
        CreateCapsule("CornerIsland", common.transform,
            new Vector3(-direction * 11f, -1.50f, 10f),
            new Vector3(2.15f, 9.5f, 0.60f), _deepStructureMaterial,
            new Vector3(90f, 0f, 0f));
        CreateBeam("TurnEntryRail", common.transform,
            new Vector3(-direction * TrackGeometryStandards.EdgeRailOffset,
                0.22f, 0.25f),
            new Vector3(-direction * TrackGeometryStandards.EdgeRailOffset,
                0.22f, 13.75f),
            0.09f, _cyanMaterial);
        CreateBeam("TurnExitRail", common.transform,
            new Vector3(0.25f, 0.22f,
                10f + TrackGeometryStandards.EdgeRailOffset),
            new Vector3(direction * 13.75f, 0.22f,
                10f + TrackGeometryStandards.EdgeRailOffset),
            0.09f, _goldMaterial);
        BuildCornerIslandDetails(common.transform, direction);

        GameObject visualVariants = new GameObject("VisualVariants");
        visualVariants.transform.SetParent(parent, false);
        GameObject city = CreateVariant("Variant_A_CornerCity",
            visualVariants.transform);
        BuildMegacityDistrict(city.transform, false, -direction, 10f, 0.92f,
            TrackGeometryStandards.TurnNearDecorationCenterOffset);
        GameObject signal = CreateVariant("Variant_B_CornerSignal",
            visualVariants.transform);
        BuildMegacityDistrict(signal.transform, true, -direction, 13f, 0.88f,
            TrackGeometryStandards.TurnNearDecorationCenterOffset);
        BuildTurnSignal(signal.transform, direction);

        GameObject highQualityOnly = new GameObject("HighQualityOnly");
        highQualityOnly.transform.SetParent(parent, false);
        variantSet.Initialize(new[] { city, signal }, highQualityOnly);
    }

    private static GameObject CreateVariant(string name, Transform parent)
    {
        GameObject variant = new GameObject(name);
        variant.transform.SetParent(parent, false);
        return variant;
    }

    private bool TryBuildColdWhiteStraightEnvironment(Transform parent,
        EchoEnvironmentVariantSet variantSet)
    {
        if (_coldWhiteFortressStraight00Prefab == null)
            _coldWhiteFortressStraight00Prefab = Resources.Load<GameObject>(
                ColdWhiteFortressStraight00ResourcePath);
        if (_coldWhiteFortressStraight40Prefab == null)
            _coldWhiteFortressStraight40Prefab = Resources.Load<GameObject>(
                ColdWhiteFortressStraight40ResourcePath);
        if (_coldWhiteFortressStraight00Prefab == null
            || _coldWhiteFortressStraight40Prefab == null)
            return false;

        GameObject common = new GameObject("Common");
        common.transform.SetParent(parent, false);
        GameObject visualVariants = new GameObject("VisualVariants");
        visualVariants.transform.SetParent(parent, false);
        GameObject open = InstantiateAuthoredVariant(
            _coldWhiteFortressStraight00Prefab,
            "Variant_A_ColdWhiteOpen", visualVariants.transform);
        GameObject openReverse = InstantiateAuthoredVariant(
            _coldWhiteFortressStraight00Prefab,
            "Variant_B_ColdWhiteOpenReverse", visualVariants.transform);
        openReverse.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        GameObject exit = InstantiateAuthoredVariant(
            _coldWhiteFortressStraight40Prefab,
            "Variant_C_ColdWhiteExit", visualVariants.transform);
        GameObject highQualityOnly = new GameObject("HighQualityOnly");
        highQualityOnly.transform.SetParent(parent, false);
        variantSet.Initialize(new[] { open, openReverse, exit },
            highQualityOnly);
        return true;
    }

    private bool TryBuildColdWhiteTurnRightEnvironment(Transform parent,
        EchoEnvironmentVariantSet variantSet)
    {
        if (_coldWhiteFortressTurnRight20Prefab == null)
            _coldWhiteFortressTurnRight20Prefab = Resources.Load<GameObject>(
                ColdWhiteFortressTurnRight20ResourcePath);
        if (_coldWhiteFortressTurnRight20Prefab == null) return false;

        GameObject common = new GameObject("Common");
        common.transform.SetParent(parent, false);
        GameObject visualVariants = new GameObject("VisualVariants");
        visualVariants.transform.SetParent(parent, false);
        GameObject turn = InstantiateAuthoredVariant(
            _coldWhiteFortressTurnRight20Prefab,
            "Variant_A_ColdWhiteTurnRight", visualVariants.transform);
        GameObject highQualityOnly = new GameObject("HighQualityOnly");
        highQualityOnly.transform.SetParent(parent, false);
        variantSet.Initialize(new[] { turn }, highQualityOnly);
        return true;
    }

    private static GameObject InstantiateAuthoredVariant(GameObject prefab,
        string instanceName, Transform parent)
    {
        GameObject instance = Instantiate(prefab, parent, false);
        instance.name = instanceName;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private void BuildTurnSignal(Transform parent, int direction)
    {
        BuildPylonAt(parent, -direction * 8.8f, 13.5f, 4.8f, false);
        CreateBeam("TurnSignalSpan", parent,
            new Vector3(-direction * 8.8f, 4.6f, 13.5f),
            new Vector3(direction * 3.2f, 4.6f, 17.2f),
            0.20f, _structureMaterial);
        CreateBeam("TurnSignalLight", parent,
            new Vector3(-direction * 5.6f, 4.48f, 14.5f),
            new Vector3(direction * 1.8f, 4.48f, 16.8f),
            0.055f, _cyanMaterial);
    }

    private void BuildSideEnergyStation(Transform parent, int side,
        float z, float scale)
    {
        if (_sideEnergyStationPrefab == null)
            _sideEnergyStationPrefab = Resources.Load<GameObject>(
                SideEnergyStationResourcePath);
        if (_sideEnergyStationPrefab == null) return;

        GameObject station = Instantiate(_sideEnergyStationPrefab, parent, false);
        station.name = "SideEnergyStation";
        station.transform.localPosition = new Vector3(side * 14.2f, -0.7f, z);
        station.transform.localRotation = Quaternion.Euler(
            0f, side > 0 ? -90f : 90f, 0f);
        station.transform.localScale = Vector3.one * scale;
    }

    private void BuildMegacityDistrict(Transform parent, bool useVariantB,
        int side, float z, float scale, float lateralOffset = 13.2f)
    {
        GameObject prefab;
        if (useVariantB)
        {
            if (_megacityDistrictBPrefab == null)
                _megacityDistrictBPrefab = Resources.Load<GameObject>(
                    MegacityDistrictBResourcePath);
            prefab = _megacityDistrictBPrefab;
        }
        else
        {
            if (_megacityDistrictAPrefab == null)
                _megacityDistrictAPrefab = Resources.Load<GameObject>(
                    MegacityDistrictAResourcePath);
            prefab = _megacityDistrictAPrefab;
        }

        if (prefab == null) return;

        GameObject district = Instantiate(prefab, parent, false);
        district.name = useVariantB
            ? "MegacityDistrictB"
            : "MegacityDistrictA";
        district.transform.localPosition = new Vector3(
            side * Mathf.Max(0f, lateralOffset), -0.72f, z);
        district.transform.localRotation = Quaternion.Euler(
            0f, side > 0 ? -90f : 90f, 0f);
        district.transform.localScale = Vector3.one * scale;
    }

    private void RemapEnvironmentPalette(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material replacement = ResolvePaletteMaterial(materials[materialIndex]);
                if (replacement == null || replacement == materials[materialIndex]) continue;
                materials[materialIndex] = replacement;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }
    }

    private Material ResolvePaletteMaterial(Material material)
    {
        if (material == null) return null;
        string materialName = material.name;
        if (materialName.IndexOf(
                ColdWhiteFortressPhaseAccentMaterialName,
                System.StringComparison.OrdinalIgnoreCase) >= 0)
            return _fortressPhaseAccentMaterial;
        if (materialName.IndexOf("Cyan", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return _cyanMaterial;
        if (materialName.IndexOf("Coral", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return _coralMaterial;
        if (materialName.IndexOf("Gold", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return _goldMaterial;
        if (materialName.IndexOf("Depth", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return _deepStructureMaterial;
        if (materialName.IndexOf("Structure", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return _structureMaterial;
        return null;
    }

    public void ApplyPhaseVisualStyle(EchoPhaseVisualStyle style)
    {
        EnsurePalette();
        float intensity = Mathf.Clamp01(style.intensity);
        EchoWorldPhasePalette palette = BuildPhasePalette(style);
        RenderSettings.fogColor = palette.fog;
        RenderSettings.ambientSkyColor = palette.ambientSky;
        RenderSettings.ambientEquatorColor = palette.ambientEquator;
        RenderSettings.ambientGroundColor = palette.ambientGround;
        if (_skyMaterial != null && _skyMaterial.HasProperty("_Tint"))
            _skyMaterial.SetColor("_Tint", palette.skyTint);
        if (_keyLight != null) _keyLight.color = palette.keyLight;
        if (_fillLight != null) _fillLight.color = palette.fillLight;

        ApplyMaterialColors(_structureMaterial, palette.structure,
            palette.structureEmission);
        ApplyMaterialColors(_deepStructureMaterial, palette.depth,
            palette.depthEmission);
        ApplyMaterialColors(_cyanMaterial, palette.cyan,
            palette.cyanEmission);
        ApplyMaterialColors(_coralMaterial, palette.coral,
            palette.coralEmission);
        ApplyMaterialColors(_goldMaterial, palette.gold,
            palette.goldEmission);
        Color accent = PhaseHue(BaseCyan,
            style.tint, 0.72f + intensity * 0.28f);
        Color accentEmission = style.tint
            * Mathf.Lerp(0.08f, 0.16f, intensity);
        accentEmission.a = 1f;
        ApplyMaterialColors(_fortressPhaseAccentMaterial, accent,
            accentEmission);
        CityV7PlayableEnvironment.ApplyAtmosphere(_keyLight, _fillLight);
    }

    public static EchoWorldPhasePalette BuildPhasePalette(
        EchoPhaseVisualStyle style)
    {
        float intensity = Mathf.Clamp01(style.intensity);
        float coralBoost = Mathf.Clamp01(style.coral);
        return new EchoWorldPhasePalette
        {
            fog = PhaseHue(BaseFog, style.tint, intensity * 0.10f),
            ambientSky = PhaseHue(BaseAmbientSky, style.tint, intensity * 0.10f),
            ambientEquator = PhaseHue(BaseAmbientEquator, style.tint,
                intensity * 0.08f),
            ambientGround = PhaseHue(BaseAmbientGround, style.tint,
                intensity * 0.06f),
            skyTint = PhaseHue(BaseSkyTint, style.tint, intensity * 0.08f),
            keyLight = PhaseHue(BaseKeyLight, style.tint, intensity * 0.05f),
            fillLight = PhaseHue(BaseFillLight, style.tint, intensity * 0.10f),
            structure = PhaseHue(BaseStructure, style.tint, intensity * 0.10f),
            structureEmission = PhaseEmission(BaseStructureEmission,
                style.tint, intensity * 0.18f, 1f),
            depth = PhaseHue(BaseDepth, style.tint, intensity * 0.10f),
            depthEmission = PhaseEmission(BaseDepthEmission, style.tint,
                intensity * 0.16f, 1f),
            cyan = PhaseHue(BaseCyan, style.tint, intensity * 0.72f),
            cyanEmission = PhaseEmission(BaseCyanEmission, style.tint,
                intensity * 0.82f, 1.08f),
            coral = PhaseHue(BaseCoral, style.tint, intensity * 0.58f),
            coralEmission = PhaseEmission(BaseCoralEmission, style.tint,
                intensity * 0.72f, 1f + coralBoost * 0.34f),
            gold = PhaseHue(BaseGold, style.tint, intensity * 0.62f),
            goldEmission = PhaseEmission(BaseGoldEmission, style.tint,
                intensity * 0.74f, 1f + coralBoost * 0.12f)
        };
    }

    private static Color PhaseHue(Color baseColor, Color phaseTint,
        float weight)
    {
        float peak = Mathf.Max(0.001f, baseColor.maxColorComponent);
        float phasePeak = Mathf.Max(0.001f, phaseTint.maxColorComponent);
        Color matchedHue = phaseTint * (peak / phasePeak);
        matchedHue.a = baseColor.a;
        return Color.Lerp(baseColor, matchedHue, Mathf.Clamp01(weight));
    }

    private static Color PhaseEmission(Color baseColor, Color phaseTint,
        float weight, float boost)
    {
        Color shifted = PhaseHue(baseColor, phaseTint, weight) * boost;
        shifted.a = 1f;
        return shifted;
    }

    private static void ApplyMaterialColors(Material material, Color color,
        Color emission)
    {
        if (material == null) return;
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }
    }

    private void BuildGroundBollards(Transform parent)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 3; i++)
            {
                float z = -7f + i * 7f;
                CreateCylinder("GroundBollard", parent,
                    new Vector3(side * 8.15f, 0.36f, z),
                    new Vector3(0.15f, 0.36f, 0.15f), _deepStructureMaterial);
                CreateCapsule("BollardSignal", parent,
                    new Vector3(side * 8.15f, 0.72f, z - 0.02f),
                    new Vector3(0.13f, 0.08f, 0.13f),
                    i == 2 ? _coralMaterial : _cyanMaterial);
            }
        }
    }

    private void BuildCornerIslandDetails(Transform parent, int direction)
    {
        float x = -direction * 10.5f;
        CreateCylinder("CornerIslandCore", parent,
            new Vector3(x, 0.16f, 10f),
            new Vector3(1.6f, 0.18f, 1.6f), _structureMaterial);
        CreateBeam("CornerIslandSignal", parent,
            new Vector3(x - direction * 1.4f, 0.42f, 7.5f),
            new Vector3(x + direction * 1.4f, 0.42f, 12.5f),
            0.08f, _coralMaterial);
    }

    private void BuildPylon(Transform parent, int side, float z, float height, bool accent)
    {
        float x = side * (11.4f + (Mathf.Abs(z) < 1f ? 0.8f : 0f));
        BuildPylonAt(parent, x, z, height, accent);
    }

    private void BuildPylonAt(Transform parent, float x, float z, float height, bool accent)
    {
        CreateCylinder("PylonFoot", parent, new Vector3(x, 0.12f, z),
            new Vector3(0.72f, 0.18f, 0.72f), _deepStructureMaterial);
        CreateCylinder("PylonBody", parent, new Vector3(x, height * 0.48f, z),
            new Vector3(0.48f, height * 0.48f, 0.48f), _structureMaterial);
        CreateCapsule("PylonCrown", parent, new Vector3(x, height + 0.05f, z),
            new Vector3(0.66f, 0.28f, 0.66f),
            accent ? _coralMaterial : _cyanMaterial);
        CreateCapsule("PylonLens", parent, new Vector3(x, height * 0.66f, z - 0.49f),
            new Vector3(0.12f, height * 0.16f, 0.08f),
            accent ? _goldMaterial : _cyanMaterial);
    }

    private void BuildSignalArch(Transform parent, float z)
    {
        const float halfWidth = 8.4f;
        const float height = 6.2f;
        const int segments = 7;
        const float openingHalfWidth = 3.4f;
        BuildSegmentedArch("TransitArch", parent, z, halfWidth, height, segments,
            openingHalfWidth, _structureMaterial, _cyanMaterial, 0.23f);

        float wingTipX = halfWidth - halfWidth * 2f / segments * 2f;
        float normalizedTipX = wingTipX / halfWidth;
        float wingTipY = height * Mathf.Sqrt(
            Mathf.Max(0f, 1f - normalizedTipX * normalizedTipX));
        CreateCapsule("ArchSignalLeft", parent,
            new Vector3(-wingTipX, wingTipY - 0.12f, z - 0.28f),
            new Vector3(0.42f, 0.08f, 0.07f), _coralMaterial,
            new Vector3(0f, 0f, 90f));
        CreateCapsule("ArchSignalRight", parent,
            new Vector3(wingTipX, wingTipY - 0.12f, z - 0.28f),
            new Vector3(0.42f, 0.08f, 0.07f), _coralMaterial,
            new Vector3(0f, 0f, 90f));
    }

    private void BuildTransitHalos(Transform parent)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject halo = new GameObject("TransitHalo");
            halo.transform.SetParent(parent, false);
            Vector3 center = new Vector3(side * 12.2f, 4.8f, 0.5f);
            const int segments = 8;
            const float radius = 2.2f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 start = center + new Vector3(
                    Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, 0f);
                Vector3 end = center + new Vector3(
                    Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0f);
                CreateBeam("HaloArc", halo.transform, start, end, 0.13f,
                    i == 1 || i == 5
                        ? (side > 0 ? _goldMaterial : _cyanMaterial)
                        : _structureMaterial);
            }
            CreateCylinder("HaloPylon", halo.transform,
                new Vector3(center.x, 2.1f, center.z + 0.25f),
                new Vector3(0.30f, 2.1f, 0.30f), _deepStructureMaterial);
        }
    }

    private void BuildDataTotems(Transform parent)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 2; i++)
            {
                float z = -4f + i * 9f;
                CreateCapsule("DataTotem", parent, new Vector3(side * 13.2f, 2.2f + i, z),
                    new Vector3(1.15f, 2.2f + i, 1.15f), _structureMaterial,
                    new Vector3(0f, side * (12f + i * 7f), 0f));
                CreateCylinder("DataBand", parent, new Vector3(side * 13.2f, 3.2f + i, z),
                    new Vector3(1.28f, 0.09f, 1.28f),
                    i == 0 ? _coralMaterial : _cyanMaterial);
            }
        }
    }

    public void StyleCoin(GameObject coin)
    {
        if (coin == null || coin.transform.Find("StreamlinedVisual") != null) return;
        DisableRenderers(coin);
        GameObject visual = new GameObject("StreamlinedVisual");
        visual.transform.SetParent(coin.transform, false);
        EchoCoinVisual coinVisual = visual.AddComponent<EchoCoinVisual>();
        coinVisual.Initialize();
        Coin coinData = coin.GetComponent<Coin>();
        coinVisual.SetContractMarker(coinData != null
            && coinData.IsEchoContractMarker);
    }

    public void StyleObstacle(GameObject obstacle)
    {
        if (obstacle == null || obstacle.transform.Find("StreamlinedVisual") != null) return;
        Obstacle data = obstacle.GetComponent<Obstacle>();
        if (data == null) return;

        DisableRenderers(obstacle);
        GameObject visual = new GameObject("StreamlinedVisual");
        visual.transform.SetParent(obstacle.transform, false);

        if (data.type == ObstacleType.Low)
        {
            BuildSlideDrone(visual.transform);
        }
        else if (data.type == ObstacleType.High)
        {
            BuildJumpBlock(visual.transform);
        }
        else
        {
            BuildLaneBulkhead(visual.transform);
        }

        GameObject highQualityOnly = new GameObject("HighQualityOnly");
        highQualityOnly.transform.SetParent(visual.transform, false);
        BuildObstacleHighDetails(highQualityOnly.transform, data.type);
        EchoQualityGate qualityGate = visual.AddComponent<EchoQualityGate>();
        qualityGate.Initialize(highQualityOnly);
    }

    private void BuildSlideDrone(Transform parent)
    {
        // A compact hovering machine replaces the old rail. Its solid outline
        // matches the gameplay collider and leaves a clear slide gap below it.
        CreateCube("SlideDroneBody", parent,
            new Vector3(0f, 0.95f, 0f),
            new Vector3(2.80f, 0.82f, 1.05f), _structureMaterial);
        CreateCube("SlideDroneFace", parent,
            new Vector3(0f, 0.95f, -0.56f),
            new Vector3(2.28f, 0.43f, 0.10f), _deepStructureMaterial);
        CreateSphere("SlideDronePodL", parent,
            new Vector3(-1.24f, 0.95f, 0f),
            new Vector3(0.55f, 0.66f, 1.18f), _deepStructureMaterial);
        CreateSphere("SlideDronePodR", parent,
            new Vector3(1.24f, 0.95f, 0f),
            new Vector3(0.55f, 0.66f, 1.18f), _deepStructureMaterial);
        CreateCube("SlideDroneSignalL", parent,
            new Vector3(-0.42f, 0.95f, -0.63f),
            new Vector3(0.13f, 0.32f, 0.06f), _cyanMaterial,
            new Vector3(0f, 0f, -30f));
        CreateCube("SlideDroneSignalR", parent,
            new Vector3(0.42f, 0.95f, -0.63f),
            new Vector3(0.13f, 0.32f, 0.06f), _cyanMaterial,
            new Vector3(0f, 0f, 30f));
    }

    private void BuildJumpBlock(Transform parent)
    {
        // One unbroken low body matches the full-width jump collider. There are
        // no joints, floating nodes or diagonal gaps that look traversable.
        CreateCapsule("JumpBlockBody", parent,
            new Vector3(0f, -0.45f, 0f),
            new Vector3(0.72f, 1.58f, 0.52f), _structureMaterial,
            new Vector3(0f, 0f, 90f));
        CreateCapsule("JumpBlockInset", parent,
            new Vector3(0f, -0.45f, -0.45f),
            new Vector3(0.46f, 1.39f, 0.08f), _deepStructureMaterial,
            new Vector3(0f, 0f, 90f));
        CreateCapsule("JumpBlockSignal", parent,
            new Vector3(0f, -0.45f, -0.55f),
            new Vector3(0.11f, 1.20f, 0.045f), _coralMaterial,
            new Vector3(0f, 0f, 90f));
    }

    private void BuildLaneBulkhead(Transform parent)
    {
        Vector3 center = new Vector3(0f, 0.25f, 0f);
        // Sphere scale is the rendered diameter. Match the 3.4 x 2.7 collider
        // instead of the previous half-width panel.
        CreateSphere("LaneBulkheadBody", parent, center,
            new Vector3(3.30f, 2.62f, 0.58f), _structureMaterial);
        CreateSphere("LaneBulkheadInset", parent,
            center + new Vector3(0f, 0f, -0.52f),
            new Vector3(2.82f, 2.16f, 0.18f), _deepStructureMaterial);
        CreateCapsule("LaneBulkheadCore", parent,
            center + new Vector3(0f, 0f, -0.67f),
            new Vector3(0.18f, 0.78f, 0.045f), _cyanMaterial);
        CreateCapsule("LaneBulkheadRailL", parent,
            center + new Vector3(-0.72f, 0f, -0.65f),
            new Vector3(0.10f, 0.72f, 0.045f), _goldMaterial,
            new Vector3(0f, 0f, -18f));
        CreateCapsule("LaneBulkheadRailR", parent,
            center + new Vector3(0.72f, 0f, -0.65f),
            new Vector3(0.10f, 0.72f, 0.045f), _goldMaterial,
            new Vector3(0f, 0f, 18f));
    }

    private void BuildObstacleHighDetails(Transform parent, ObstacleType type)
    {
        if (type == ObstacleType.Low)
        {
            CreateCube("ScannerHighlight", parent,
                new Vector3(0f, 1.18f, -0.64f),
                new Vector3(1.7f, 0.06f, 0.035f), _goldMaterial);
        }
        else if (type == ObstacleType.High)
        {
            CreateCapsule("HurdleHighlight", parent,
                new Vector3(0f, -0.30f, -0.57f),
                new Vector3(0.055f, 1.1f, 0.035f), _cyanMaterial,
                new Vector3(0f, 0f, 90f));
        }
        else
        {
            CreateCapsule("GateRipple", parent,
                new Vector3(0f, 0.25f, -0.76f),
                new Vector3(0.08f, 0.98f, 0.035f), _coralMaterial);
        }
    }

    private void StyleCharacter()
    {
        GameObject player = GameObject.Find("player");
        if (player == null) return;
        Transform model = player.transform.Find("CharacterModel");
        if (model == null || model.Find("StreamlinedSuit") != null) return;
        if (model.Find("EchoRunner_Rig") != null ||
            model.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) return;

        string[] boxParts = { "ChestPlate", "ChestSignal", "SignalBelt", "HelmetBand", "Mouth" };
        for (int i = 0; i < boxParts.Length; i++)
        {
            Transform part = model.Find(boxParts[i]);
            if (part != null)
            {
                Renderer renderer = part.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = false;
            }
        }

        GameObject suit = new GameObject("StreamlinedSuit");
        suit.transform.SetParent(model, false);
        CreateSphere("ChestShell", suit.transform, new Vector3(0f, 1.43f, 0.42f),
            new Vector3(0.48f, 0.48f, 0.12f), _structureMaterial);
        CreateCapsule("ChestSignal", suit.transform, new Vector3(0f, 1.43f, 0.55f),
            new Vector3(0.07f, 0.22f, 0.035f), _goldMaterial,
            new Vector3(0f, 0f, 90f));
        CreateCylinder("SuitRing", suit.transform, new Vector3(0f, 1.03f, 0f),
            new Vector3(0.58f, 0.05f, 0.45f), _cyanMaterial);
        CreateSphere("HelmetShell", suit.transform, new Vector3(0f, 2.16f, -0.17f),
            new Vector3(0.43f, 0.25f, 0.29f), _structureMaterial);
    }

    private static void DisableRenderers(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = false;
    }

    private static void BuildSegmentedArch(string name, Transform parent, float z,
        float halfWidth, float height, int segments, float openingHalfWidth,
        Material body, Material accent, float radius)
    {
        GameObject arch = new GameObject(name);
        arch.transform.SetParent(parent, false);
        Vector3 previous = new Vector3(-halfWidth, 0f, z);
        if (Mathf.Abs(previous.x) >= openingHalfWidth)
        {
            CreateSphere("ArcNode", arch.transform, previous,
                Vector3.one * radius * 1.22f, body);
        }
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float x = Mathf.Lerp(-halfWidth, halfWidth, t);
            float normalized = x / halfWidth;
            float y = height * Mathf.Sqrt(Mathf.Max(0f, 1f - normalized * normalized));
            Vector3 current = new Vector3(x, y, z);
            bool segmentOutsideOpening = previous.x <= -openingHalfWidth
                && current.x <= -openingHalfWidth
                || previous.x >= openingHalfWidth
                && current.x >= openingHalfWidth;
            if (segmentOutsideOpening)
            {
                CreateBeam("ArcJoint", arch.transform, previous, current, radius,
                    (i == 2 || i == segments - 1) ? accent : body);
            }
            if (Mathf.Abs(current.x) >= openingHalfWidth)
            {
                CreateSphere("ArcNode", arch.transform, current,
                    Vector3.one * radius * 1.22f, body);
            }
            previous = current;
        }
    }

    private static GameObject CreateBeam(string name, Transform parent, Vector3 start,
        Vector3 end, float radius, Material material)
    {
        Vector3 delta = end - start;
        GameObject beam = CreateCylinder(name, parent, (start + end) * 0.5f,
            new Vector3(radius, delta.magnitude * 0.5f, radius), material);
        if (delta.sqrMagnitude > 0.0001f)
            beam.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        return beam;
    }

    private static GameObject CreateCapsule(string name, Transform parent, Vector3 position,
        Vector3 scale, Material material, Vector3 euler = default)
    {
        return CreatePrimitive(name, PrimitiveType.Capsule, parent, position, scale, material, euler);
    }

    private static GameObject CreateCylinder(string name, Transform parent, Vector3 position,
        Vector3 scale, Material material, Vector3 euler = default)
    {
        return CreatePrimitive(name, PrimitiveType.Cylinder, parent, position, scale, material, euler);
    }

    private static GameObject CreateSphere(string name, Transform parent, Vector3 position,
        Vector3 scale, Material material, Vector3 euler = default)
    {
        return CreatePrimitive(name, PrimitiveType.Sphere, parent, position, scale, material, euler);
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 position,
        Vector3 scale, Material material, Vector3 euler = default)
    {
        return CreatePrimitive(name, PrimitiveType.Cube, parent, position, scale, material, euler);
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent,
        Vector3 position, Vector3 scale, Material material, Vector3 euler = default)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = position;
        primitive.transform.localEulerAngles = euler;
        primitive.transform.localScale = scale;
        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }
        Renderer renderer = primitive.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        return primitive;
    }

    private static Material MakeMaterial(string name, Color color, Color emission,
        float metallic, float smoothness)
    {
        Shader shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
            ? Shader.Find("Universal Render Pipeline/Lit")
            : Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Mobile/Diffuse");
        Material material = new Material(shader) { name = name };
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        if (emission.maxColorComponent > 0f && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }
        return material;
    }

    private void ApplyVisualQuality(VisualQuality quality)
    {
        bool high = quality == VisualQuality.High;
        Shader.SetGlobalFloat("_EchoVisualHigh", high ? 1f : 0f);
        // Quality changes and phase changes share the same city lighting;
        // neither may restore the older fortress fill/reflection values.
        CityV7PlayableEnvironment.ApplyAtmosphere(_keyLight, _fillLight);
    }

    void OnDestroy()
    {
        VisualQualityController.Changed -= ApplyVisualQuality;
        if (_skyMaterial != null) Destroy(_skyMaterial);
        DestroyPaletteMaterial(_structureMaterial);
        DestroyPaletteMaterial(_deepStructureMaterial);
        DestroyPaletteMaterial(_cyanMaterial);
        DestroyPaletteMaterial(_coralMaterial);
        DestroyPaletteMaterial(_goldMaterial);
        DestroyPaletteMaterial(_fortressPhaseAccentMaterial);
        if (Instance == this) Instance = null;
    }

    private static void DestroyPaletteMaterial(Material material)
    {
        if (material == null) return;
        if (Application.isPlaying) Destroy(material);
        else DestroyImmediate(material);
    }
}
