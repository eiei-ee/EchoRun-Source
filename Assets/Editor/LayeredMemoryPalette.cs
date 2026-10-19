using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Material-only authoring pass. Existing meshes, rigs, textures, colliders,
// prefabs and GUIDs are retained. Re-running writes the same authored values.
public static class LayeredMemoryPalette
{
    private const string BackupRoot = "TestResults/LayeredMemory-20260924/Baseline/";
    private const string OutputRoot = "TestResults/LayeredMemory-20260924/";
    private const string SkyPath = "Assets/Resources/CityV7/ExperienceSky.mat";
    private const string Outfit = "Assets/Art/OrangeEcho/Materials/";
    private const string City = "Assets/Art/StackedCity/Materials/";
    private const string OriginalCity = "Assets/CityAfterimageV7/Materials/";

    private readonly struct Surface
    {
        public readonly string path;
        public readonly int color;
        public readonly float smoothness;
        public readonly float metallic;
        public readonly int emission;
        public readonly float emissionStrength;

        public Surface(string path, int color, float smoothness = .25f,
            float metallic = .03f, int emission = 0, float emissionStrength = 0f)
        {
            this.path = path;
            this.color = color;
            this.smoothness = smoothness;
            this.metallic = metallic;
            this.emission = emission;
            this.emissionStrength = emissionStrength;
        }
    }

    [MenuItem("Tools/Echo Runner/Layered Memory/Install Palette")]
    public static void Install()
    {
        List<Surface> surfaces = BuildSurfaces();
        // Resolve everything before authoring: an incomplete import should fail
        // explicitly rather than leaving half the city in the previous palette.
        var materials = new List<Material>();
        foreach (Surface surface in surfaces) materials.Add(RequiredMaterial(surface.path));
        Material sky = RequiredMaterial(SkyPath);
        foreach (Surface surface in surfaces) Backup(surface.path);
        Backup(SkyPath);

        var report = new List<string>
        {
            "Layered Memory world palette. Existing materials edited in place.",
            "No mesh, transform, rig, collider, prefab, texture or scene changes.",
            "Mineral blue masonry, pomegranate districts; lemon player and lilac echo.",
            "UIManager owns the runtime player preset; its light tint must match the action accent."
        };
        for (int i = 0; i < surfaces.Count; i++)
        {
            Surface surface = surfaces[i];
            Material material = materials[i];
            Color color = Rgb(surface.color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            SetFloat(material, "_Glossiness", surface.smoothness);
            SetFloat(material, "_Smoothness", surface.smoothness);
            SetFloat(material, "_Metallic", surface.metallic);
            ConfigureRoadDeck(material, surface.path);
            if (material.HasProperty("_EmissionColor"))
            {
                Color emission = Rgb(surface.emission) * surface.emissionStrength;
                emission.a = 1f;
                material.SetColor("_EmissionColor", emission);
                if (surface.emissionStrength > 0f) material.EnableKeyword("_EMISSION");
                else material.DisableKeyword("_EMISSION");
            }
            EditorUtility.SetDirty(material);
            report.Add(surface.path + " #" + surface.color.ToString("X6"));
        }
        CityV7PlayableEnvironment.StyleSky(sky);
        EditorUtility.SetDirty(sky);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(OutputRoot);
        File.WriteAllLines(OutputRoot + "world-palette-install.txt", report);
        Debug.Log("LAYERED_MEMORY_PALETTE_OK materials=" + surfaces.Count
                  + " sky=authored-panorama backup=" + BackupRoot);
    }

    public static void InstallRoadSurfaces()
    {
        foreach (Surface surface in BuildSurfaces())
        {
            if (!surface.path.StartsWith(Outfit + "OE_Road", StringComparison.Ordinal)) continue;
            Material material = RequiredMaterial(surface.path);
            ConfigureRoadDeck(material, surface.path);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Rgb(surface.color));
            SetFloat(material, "_Smoothness", surface.smoothness);
            SetFloat(material, "_Metallic", surface.metallic);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/EchoRun/Art/Polish City Presentation")]
    public static void InstallCityPresentation()
    {
        // A narrow re-authoring pass for the existing road and horizon assets.
        // It does not recolour the runner, echo or district facade families.
        List<Surface> surfaces = BuildSurfaces().FindAll(surface =>
            surface.path.StartsWith(Outfit + "OE_Road", StringComparison.Ordinal)
            || surface.path == OriginalCity + "SkyMiddle.mat"
            || surface.path == OriginalCity + "SkyFar.mat"
            || surface.path == "Assets/Art/ExperienceSlice/DistantCity.mat"
            || surface.path == "Assets/Art/CityLayers/HorizonHaze.mat");
        var materials = new List<Material>();
        foreach (Surface surface in surfaces) materials.Add(RequiredMaterial(surface.path));
        Material sky = RequiredMaterial(SkyPath);
        const string reviewRoot = "TestResults/CityDetailPolish-20260925/";
        foreach (Surface surface in surfaces) Backup(surface.path, reviewRoot + "Before/");
        Backup(SkyPath, reviewRoot + "Before/");

        var report = new List<string>
        {
            "City road and horizon presentation. Existing assets edited in place.",
            "No added meshes, lights, texture samples or render passes.",
            "Palette target: cool mineral city, graphite road, clear pale markings."
        };
        for (int i = 0; i < surfaces.Count; i++)
        {
            Surface surface = surfaces[i];
            Material material = materials[i];
            ConfigureRoadDeck(material, surface.path);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Rgb(surface.color));
            SetFloat(material, "_Glossiness", surface.smoothness);
            SetFloat(material, "_Smoothness", surface.smoothness);
            SetFloat(material, "_Metallic", surface.metallic);
            EditorUtility.SetDirty(material);
            report.Add(surface.path + " #" + surface.color.ToString("X6"));
        }
        CityV7PlayableEnvironment.StyleSky(sky);
        EditorUtility.SetDirty(sky);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(reviewRoot);
        File.WriteAllLines(reviewRoot + "city-presentation-install.txt", report);
        Debug.Log("CITY_PRESENTATION_POLISH_READY materials=" + surfaces.Count + " sky=authored-panorama");
    }

    private static void ConfigureRoadDeck(Material material, string path)
    {
        if (!path.EndsWith("OE_RoadDeck.mat", StringComparison.Ordinal)) return;
        Shader surface = Shader.Find("EchoRun/OrangeEchoSurface");
        if (surface == null) throw new InvalidOperationException("Authored road surface shader missing.");
        material.shader = surface;
        material.SetFloat("_Grain", .025f);
    }

    private static List<Surface> BuildSurfaces()
    {
        var result = new List<Surface>
        {
            // Player cloth stays matte and readable; the existing head and hands
            // are not included in this material family and retain their skin tones.
            new Surface(Outfit + "OE_Jacket_Accent.mat", 0xF3E78E, .18f, 0f),
            new Surface(Outfit + "OE_Navy_Fabric.mat", 0x1D2B43, .16f, 0f),
            new Surface(Outfit + "OE_Ivory_Fabric.mat", 0xD9CDDC, .18f, 0f),
            new Surface(Outfit + "OE_Rubber.mat", 0x18151D, .18f, 0f),
            new Surface(Outfit + "OE_RelayMetal.mat", 0x766A85, .38f, .30f),
            new Surface(Outfit + "OE_RelaySignal.mat", 0xC59AEF, .28f, .08f),
            // Road boundaries remain neutral. Acid yellow is reserved for the
            // player/action channel, not a continuous brightly glowing road edge.
            new Surface(Outfit + "OE_RoadDeck.mat", 0x77818D, .18f, 0f),
            new Surface(Outfit + "OE_RoadJoint.mat", 0x64707C, .15f, 0f),
            new Surface(Outfit + "OE_RoadPaint.mat", 0xD6DADF, .19f, 0f),
            new Surface(Outfit + "OE_RoadIvory.mat", 0x9BA7A9, .20f, .02f),
            new Surface(Outfit + "OE_RoadOrange.mat", 0x597B86, .24f, .02f),
            new Surface(City + "SC_Concrete.mat", 0x6584A2, .20f),
            new Surface(City + "SC_Metal.mat", 0x35435D, .32f, .20f),
            new Surface(City + "SC_White.mat", 0xA8B4C9, .24f, .05f),
            new Surface(City + "SC_Street.mat", 0x4A566B, .19f, .01f),
            new Surface(City + "SC_Marking.mat", 0xB0B8CA, .20f, 0f),
            new Surface(City + "SC_Orange.mat", 0x9C516E, .20f, .01f),
            new Surface(City + "SC_Joint.mat", 0x303B4D, .17f, .01f),
            new Surface(City + "SC_Rubber.mat", 0x211D28, .15f, 0f),
            new Surface(City + "SC_Bark.mat", 0x4B3C48, .18f, 0f),
            new Surface(City + "SC_Foliage.mat", 0x4E674F, .18f, 0f),
            new Surface(City + "SC_Glass.mat", 0x3B5371, .55f, .12f, 0xA1B7DC, .025f),
            new Surface(City + "SC_Cyan.mat", 0xA7B1D4, .32f, .06f, 0xC59AEF, .10f),
            new Surface(City + "SC_Warm.mat", 0xC7BEA5, .32f, .04f, 0xDED2AE, .09f),
            new Surface(City + "FinishGate_Metal.mat", 0x52445F, .32f, .20f),
            new Surface(City + "FinishGate_Signal.mat", 0xF3E78E, .30f, .05f, 0xF3E78E, .12f),
            new Surface(OriginalCity + "CityV7_Pale.mat", 0x7D9BBC, .20f, .02f),
            new Surface(OriginalCity + "CityV7_Mineral.mat", 0x6685A7, .22f, .03f),
            new Surface(OriginalCity + "CityV7_BlueStone.mat", 0x456687, .24f, .05f),
            new Surface(OriginalCity + "CityV7_Structure.mat", 0x425875, .23f, .08f),
            new Surface(OriginalCity + "CityV7_Glass.mat", 0x3C5373, .55f, .16f, 0xA1B7DC, .025f),
            new Surface(OriginalCity + "CityV7_Scale.mat", 0x93A6C0, .22f, .03f),
            new Surface(OriginalCity + "SkyMiddle.mat", 0x657E8C, .20f, 0f),
            new Surface(OriginalCity + "SkyFar.mat", 0x869BA5, .20f, 0f),
            new Surface("Assets/Art/ExperienceSlice/DistantCity.mat", 0x708792, .20f, 0f),
            new Surface("Assets/Art/ExperienceSlice/WindowFrame.mat", 0x324459, .30f, .20f),
            new Surface("Assets/Art/ExperienceSlice/WarmWindow.mat", 0xC4BA9D, .35f, .06f, 0xDED2AE, .10f),
            new Surface("Assets/Art/CityLayers/Concrete.mat", 0x6E89AB, .23f, .015f),
            new Surface("Assets/Art/CityLayers/RoofMetal.mat", 0x465C7B, .31f, .20f),
            new Surface("Assets/Art/CityLayers/OxideTrim.mat", 0x914961, .22f, .02f),
            new Surface("Assets/Art/CityLayers/LowerStreets.mat", 0x4A5873, .18f, .01f),
            new Surface("Assets/Art/CityLayers/HorizonHaze.mat", 0x708792, .20f, 0f)
        };

        // Four established district IDs retain their bindings. Palette values
        // come from their authoring owner so a facade rebuild keeps this direction.
        string[] districts = { "Terracotta", "Jade", "BlueTransit", "Sandstone" };
        string[] roles = { "MainConcrete", "SecondaryMineral", "Glass", "WindowFrame", "WarmWindow", "EnamelWhite" };
        float[] smoothness = { .22f, .26f, .68f, .36f, .42f, .38f };
        float[] metallic = { .025f, .04f, .20f, .28f, .08f, .12f };
        for (int district = 0; district < districts.Length; district++)
        for (int role = 0; role < roles.Length; role++)
        {
            bool window = role == 4;
            bool glass = role == 2;
            result.Add(new Surface(City + "Districts/District_" + districts[district]
                + "_" + roles[role] + ".mat", StackedCityDistrictMaterials.PaletteRgb(district, role), smoothness[role], metallic[role],
                window ? 0xDED2AE : glass ? 0xA1B7DC : 0,
                window ? .10f : glass ? .02f : 0f));
        }
        return result;
    }

    private static Material RequiredMaterial(string path)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) throw new InvalidOperationException("Missing authored world material: " + path);
        return material;
    }

    private static void Backup(string path, string backupRoot = BackupRoot)
    {
        string destination = backupRoot + path;
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (!File.Exists(destination)) File.Copy(path, destination);
        if (File.Exists(path + ".meta") && !File.Exists(destination + ".meta"))
            File.Copy(path + ".meta", destination + ".meta");
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    private static Color Rgb(int rgb) => new Color32(
        (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
}
