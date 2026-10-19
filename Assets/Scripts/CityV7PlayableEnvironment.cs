using UnityEngine;

// City presentation follows the existing segment pool.
// All collision, route generation, controls and formal character code are retained.
public static class CityV7PlayableEnvironment
{
    public const float FogEndDistance = 185f;
    // A quiet blue-grey distance layer leaves the warm runner, pale lane paint
    // and cyan echo above it in contrast. Avoid a violet cast on every surface.
    public static readonly Color FogColor = Color32ToColor(0x8296A0);
    public static readonly Color AmbientSkyColor = Color32ToColor(0xBCC9DB);
    public static readonly Color AmbientEquatorColor = Color32ToColor(0x8B98AA);
    public static readonly Color AmbientGroundColor = Color32ToColor(0x616C7D);
    public static readonly Color SkyTint = Color32ToColor(0xADBBD0);
    public static readonly Color KeyLightColor = Color32ToColor(0xF4F5F7);
    public static readonly Color FillLightColor = Color32ToColor(0xC1D5DE);

    // The existing cloud panorama remains the authored sky. The same values are
    // used by the offline material installer and by the runtime sky clone.
    public static void StyleSky(Material material)
    {
        if (material == null) return;
        if (material.HasProperty("_Tint")) material.SetColor("_Tint", SkyTint);
        // The sky shader multiplies by unity_ColorSpaceDouble in linear colour
        // space. The old .55 exposure clipped the bright clouds to near-white.
        if (material.HasProperty("_Exposure")) material.SetFloat("_Exposure", .40f);
        if (material.HasProperty("_Saturation")) material.SetFloat("_Saturation", .34f);
    }

    public static Material CreateSky()
    {
        var material=new Material(Resources.Load<Shader>("CityV7/CityAfterimageQuietSky"));
        material.SetColor("_Zenith",Color32ToColor(0x57788C));material.SetColor("_Horizon",Color32ToColor(0x9BAAB0));material.SetColor("_Ground",FogColor);return material;
    }
    static Color Color32ToColor(int rgb){return new Color((rgb>>16&255)/255f,(rgb>>8&255)/255f,(rgb&255)/255f);}
    public static void ApplyAtmosphere(Light key=null,Light fill=null)
    {
        bool high = VisualQualityController.Current == VisualQuality.High;
        bool desktop = VisualQualityController.DefaultForPlatform(Application.platform)
                       == VisualQuality.High;
        if (high && desktop)
        {
            // The editor's 40 m default clipped city shadows before the first
            // cross street. Match the desktop player with a bounded sun pass.
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowDistance = 110f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowCascade4Split = new Vector3(.12f, .30f, .60f);
            QualitySettings.pixelLightCount = Mathf.Max(2, QualitySettings.pixelLightCount);
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 90f;
        // Both city grids recycle beyond this cutoff. Keep it independent of
        // the farther skyline and of the sun's nearer shadow distance.
        RenderSettings.fogEndDistance = FogEndDistance;
        RenderSettings.fogColor = FogColor;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = AmbientSkyColor;
        RenderSettings.ambientEquatorColor = AmbientEquatorColor;
        RenderSettings.ambientGroundColor = AmbientGroundColor;

        // A fixed authored sky reflection gives the windows and painted metal
        // a shared environment without rendering extra cameras during a run.
        Cubemap reflection = Resources.Load<Cubemap>("CityV7/StackedCityReflection");
        if (reflection != null)
        {
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = reflection;
        }
        RenderSettings.reflectionIntensity = high ? .30f : .16f;

        if (key != null)
        {
            key.shadows = high && desktop ? LightShadows.Soft : LightShadows.None;
            key.color = KeyLightColor;
            key.intensity = 1.12f;
            key.transform.rotation = Quaternion.Euler(39f, -48f, 0f);
            key.shadowStrength = .68f;
            key.shadowBias = .025f;
            key.shadowNormalBias = .12f;
            RenderSettings.sun = key;
        }
        // Broad neutral fill keeps the elevated-road shadows and building
        // undersides readable without shifting the road towards green.
        if (fill != null)
        {
            fill.enabled = high;
            fill.intensity = high ? .22f : 0f;
            fill.color = FillLightColor;
            fill.transform.rotation = Quaternion.Euler(28f, 145f, 0f);
            fill.shadows = LightShadows.None;
        }
    }
    public static bool Decorate(GameObject segment,TrackSegmentType type,bool refreshClearance=true)
    {
        if(type!=TrackSegmentType.Straight){ReplaceLegacyTurnBuildings(segment,type);if(refreshClearance)RefreshClearance();return false;}
        var data=segment.GetComponent<TrackSegmentData>();
        if(data==null)return false;
        int index=((Mathf.RoundToInt(data.routeDistance/20f)%9)+9)%9;
        var prefab=Resources.Load<GameObject>("CityV7/Chunk"+index);
        if(prefab==null)return false;
        // The authored prefab disabled its fallback surface because its old
        // environment supplied the visible road. City chunks contain buildings
        // only, so restore that independent surface before hiding the old group.
        foreach(Transform child in segment.transform)
        {
            bool ground=child.name=="GroundPlane";
            if(!ground&&!child.name.StartsWith("LaneLine_"))continue;
            var renderer=child.GetComponent<Renderer>();
            if(renderer==null)continue;
            renderer.enabled=true;
            if(ground)EchoRoadVisualController.Instance.ApplyTo(renderer,RoadSurfaceRole.Main);
        }
        var existing=segment.transform.Find("CityV7Environment");
        if(existing!=null&&existing.GetComponent<CityV7ChunkIdentity>().index==index){if(refreshClearance)RefreshClearance();return true;}
        if(existing!=null){existing.gameObject.SetActive(false);Object.Destroy(existing.gameObject);}
        var legacy=segment.transform.Find("EchoEnvironment");if(legacy!=null)legacy.gameObject.SetActive(false);
        var visual=Object.Instantiate(prefab,segment.transform,false);visual.name="CityV7Environment";
        visual.AddComponent<CityV7ChunkIdentity>().index=index;
        if(refreshClearance)RefreshClearance();
        return true;
    }

    static void ReplaceLegacyTurnBuildings(GameObject segment,TrackSegmentType type)
    {
        var legacy=segment.transform.Find("EchoEnvironment");
        var quarter=Resources.Load<GameObject>("CityV7/"+(type==TrackSegmentType.TurnRight?"CornerQuarterRight":"CornerQuarterLeft"));
        var prefab=Resources.Load<GameObject>("CityV7/Chunk0");
        if(legacy==null||prefab==null)return;
        bool hasLegacyCity=false;
        foreach(var child in legacy.GetComponentsInChildren<Transform>(true))
        {
            if(child.name=="MegacityDistrictA"||child.name=="MegacityDistrictB")
            {child.gameObject.SetActive(false);hasLegacyCity=true;}
            if(child.name=="CornerIsland"||child.name=="CornerIslandCore"||child.name=="CornerIslandSignal")
                child.gameObject.SetActive(false);
        }
        if(segment.transform.Find("CityV7Environment")!=null)return;
        if(quarter!=null)
        {
            // Each authored tower is a separate child, so clearance removes
            // only an obstructing footprint instead of the entire corner city.
            var district=Object.Instantiate(quarter,segment.transform,false);
            district.name="CityV7Environment";
            district.AddComponent<CityV7ChunkIdentity>().index=-1;
            return;
        }
        if(!hasLegacyCity)return;
        // Retain the authored turn road, rails, variant controller and colliders.
        // The replacement sits outside the bend and participates in clearance.
        var root=new GameObject("CityV7Environment");
        root.transform.SetParent(segment.transform,false);
        root.AddComponent<CityV7ChunkIdentity>().index=-1;
        var building=Object.Instantiate(prefab,root.transform,false);
        building.name="CityV7TurnPodium";
        var renderers=building.GetComponentsInChildren<Renderer>(true);
        if(renderers.Length==0)return;
        Bounds bounds=TransformBounds(WorldBounds(renderers[0]),segment.transform.worldToLocalMatrix);
        for(int i=1;i<renderers.Length;i++)
            bounds.Encapsulate(TransformBounds(WorldBounds(renderers[i]),segment.transform.worldToLocalMatrix));
        int direction=type==TrackSegmentType.TurnRight?1:-1;
        building.transform.localPosition+=new Vector3(-direction*(16f+bounds.extents.x)-bounds.center.x,0,10f-bounds.center.z);
    }

    // Re-evaluate when the route pool changes, before a newly planned bend is
    // visible. Adjacent straight chunks can otherwise extend into its exit arm
    // or the outside arc of the unchanged gameplay camera.
    public static void RefreshClearance()
    {
        var route=Object.FindObjectsOfType<TrackSegmentData>();
        foreach(var city in Object.FindObjectsOfType<CityV7ChunkIdentity>())
        foreach(Transform building in city.transform)
        {
            var renderers=building.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0)continue;
            Bounds world=WorldBounds(renderers[0]);
            for(int i=1;i<renderers.Length;i++)world.Encapsulate(WorldBounds(renderers[i]));
            bool blocked=false;
            foreach(var segment in route)
            {
                Bounds local=TransformBounds(world,segment.transform.worldToLocalMatrix);
                bool turn=segment.segmentType!=TrackSegmentType.Straight;
                Bounds corridor=turn
                    ? new Bounds(new Vector3(0,3,10),new Vector3(28,6.2f,28))
                    : new Bounds(new Vector3(0,3,0),new Vector3(11.2f,6.2f,20));
                if(!corridor.Intersects(local))continue;
                blocked=true;break;
            }
            building.gameObject.SetActive(!blocked);
        }
    }
    static Bounds WorldBounds(Renderer renderer)
    {
        return TransformBounds(renderer.localBounds,renderer.transform.localToWorldMatrix);
    }
    static Bounds TransformBounds(Bounds bounds,Matrix4x4 matrix)
    {
        Bounds result=new Bounds(matrix.MultiplyPoint3x4(bounds.min),Vector3.zero);
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
            result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(x==0?bounds.min.x:bounds.max.x,y==0?bounds.min.y:bounds.max.y,z==0?bounds.min.z:bounds.max.z)));
        return result;
    }
}
public sealed class CityV7ChunkIdentity:MonoBehaviour{public int index;}
