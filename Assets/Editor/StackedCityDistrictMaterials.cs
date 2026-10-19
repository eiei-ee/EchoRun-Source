using System;
using UnityEditor;
using UnityEngine;

// Offline authoring only. Buildings share a small serialized material family;
// their meshes, UVs, renderer settings and runtime lifecycle are unchanged.
public static class StackedCityDistrictMaterials
{
    public const string Folder = "Assets/Art/StackedCity/Materials/Districts";
    public const int ThemeCount = 4;
    public const int RoleCount = 6;

    public enum Theme { TerracottaResidential, JadeCivic, BlueTransit, SandstoneHeritage }
    public enum Role { MainConcrete, SecondaryMineral, Glass, WindowFrame, WarmWindow, EnamelWhite }

    static readonly string[] ThemeNames = { "Terracotta", "Jade", "BlueTransit", "Sandstone" };
    static readonly string[] Sources =
    {
        "Assets/CityAfterimageV7/Materials/CityV7_Pale.mat",
        "Assets/CityAfterimageV7/Materials/CityV7_Mineral.mat",
        "Assets/CityAfterimageV7/Materials/CityV7_Glass.mat",
        "Assets/Art/ExperienceSlice/WindowFrame.mat",
        "Assets/Art/ExperienceSlice/WarmWindow.mat",
        "Assets/Art/StackedCity/Materials/SC_White.mat"
    };

    // Each row is one architectural palette, ordered by Role. Coloured mineral
    // bodies, pale infill, tinted glass and complementary metal read together.
    static readonly int[,] Palette =
    {
        { 0xA85C77, 0xB88296, 0x424C66, 0x513A50, 0xD1BFA4, 0xB8AABB },
        { 0x607F9C, 0x8CACC3, 0x345D66, 0x304259, 0xC5C6AC, 0xB4C2CD },
        { 0x53769F, 0x8CA5C4, 0x324B6B, 0x293D58, 0xC5C2AF, 0xB5C1D5 },
        { 0x926277, 0xAF8DA6, 0x50536A, 0x4E4058, 0xCEC0AC, 0xC2B3C3 }
    };
    public static int PaletteRgb(int theme, int role)
    {
        ValidateTheme(theme);
        ValidateRole((Role)role);
        return Palette[theme, role];
    }
    static readonly Color[] GlassEmission =
    {
        C(.035f,.048f,.054f), C(.033f,.060f,.047f), C(.030f,.050f,.071f), C(.055f,.054f,.039f)
    };
    static readonly Color[] InteriorEmission =
    {
        C(.135f,.085f,.035f), C(.100f,.105f,.050f), C(.105f,.105f,.085f), C(.135f,.095f,.040f)
    };
    static readonly float[] BodySmoothness = { .20f, .36f, .31f, .18f };
    static readonly float[] GlassSmoothness = { .73f, .77f, .83f, .70f };
    static readonly Material[,] Materials = new Material[ThemeCount, RoleCount];

    public static string ThemeName(int theme)
    {
        ValidateTheme(theme);
        return ThemeNames[theme];
    }

    public static string MaterialPath(int theme, Role role)
    {
        ValidateTheme(theme);
        ValidateRole(role);
        return Folder + "/District_" + ThemeNames[theme] + "_" + role + ".mat";
    }

    public static Material Get(int theme, Role role)
    {
        string path = MaterialPath(theme, role);
        var material = Materials[theme, (int)role];
        if (material == null)
            Materials[theme, (int)role] = material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
            throw new InvalidOperationException("Call StackedCityDistrictMaterials.Prepare before binding " + path);
        return material;
    }

    public static void Prepare()
    {
        EnsureFolder(Folder);
        for (int theme = 0; theme < ThemeCount; theme++)
        {
            for (int index = 0; index < RoleCount; index++)
            {
                Role role = (Role)index;
                Material source = AssetDatabase.LoadAssetAtPath<Material>(Sources[index]);
                if (source == null)
                    throw new InvalidOperationException("District material source is missing: " + Sources[index]);
                string path = MaterialPath(theme, role);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(source);
                    AssetDatabase.CreateAsset(material, path);
                }
                else
                {
                    // Copy the complete authored source, including texture transforms,
                    // normal/surface maps and shader keywords, without modifying it.
                    EditorUtility.CopySerialized(source, material);
                }
                material.name = "District_" + ThemeNames[theme] + "_" + role;
                int rgb = PaletteRgb(theme, index);
                material.color = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
                material.enableInstancing = true;
                switch (role)
                {
                    case Role.MainConcrete:
                        Surface(material, BodySmoothness[theme], .025f);
                        break;
                    case Role.SecondaryMineral:
                        Surface(material, BodySmoothness[theme] + .035f, .045f);
                        break;
                    case Role.Glass:
                        // Less metal preserves visible diffuse tint in shaded streets;
                        // a small interior term keeps panes from reading as black holes.
                        Surface(material, GlassSmoothness[theme], .28f);
                        Emission(material, GlassEmission[theme]);
                        break;
                    case Role.WindowFrame:
                        Surface(material, .44f, .58f);
                        break;
                    case Role.WarmWindow:
                        Surface(material, .53f, .12f);
                        Emission(material, InteriorEmission[theme]);
                        break;
                    case Role.EnamelWhite:
                        Surface(material, .57f, .20f);
                        break;
                }
                Materials[theme, index] = material;
                EditorUtility.SetDirty(material);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("STACKED_CITY_DISTRICT_MATERIALS_OK themes=" + ThemeCount + " materials=" + ThemeCount * RoleCount);
    }

    // Call for an individual authored building or its matching foundation, not
    // for the whole route. Shared source materials elsewhere are never mutated.
    public static int Apply(GameObject building, int theme)
    {
        if (building == null) throw new ArgumentNullException(nameof(building));
        ValidateTheme(theme);
        int changed = 0;
        foreach (Renderer renderer in building.GetComponentsInChildren<Renderer>(true))
        {
            if (Excluded(renderer.transform, building.transform) || renderer is ParticleSystemRenderer) continue;
            Material[] slots = renderer.sharedMaterials;
            bool modified = false;
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index] == null || !TryRole(slots[index], out Role role)) continue;
                Material replacement = Get(theme, role);
                if (slots[index] == replacement) continue;
                slots[index] = replacement;
                modified = true;
                changed++;
            }
            if (!modified) continue;
            renderer.sharedMaterials = slots;
            EditorUtility.SetDirty(renderer);
        }
        return changed;
    }

    public static bool TryRole(Material material, out Role role)
    {
        role = Role.MainConcrete;
        if (material == null) return false;
        string name = material.name;
        if (name.StartsWith("District_", StringComparison.Ordinal))
        {
            for (int index = 0; index < RoleCount; index++)
                if (name.EndsWith("_" + (Role)index, StringComparison.Ordinal))
                {
                    role = (Role)index;
                    return true;
                }
            return false;
        }
        switch (name)
        {
            case "Concrete": case "SC_Concrete": case "CityV7_Pale":
                role = Role.MainConcrete; return true;
            case "CityV7_Mineral": case "CityV7_BlueStone": case "CityV7_Structure":
                role = Role.SecondaryMineral; return true;
            case "CityV7_Glass": case "SC_Glass":
                role = Role.Glass; return true;
            case "WindowFrame": case "SC_Metal":
                role = Role.WindowFrame; return true;
            case "WarmWindow": case "SC_Warm":
                role = Role.WarmWindow; return true;
            case "SC_White":
                role = Role.EnamelWhite; return true;
            default:
                // Joint lines, foliage, signage textures, road paint and gameplay
                // colours intentionally retain their original authored bindings.
                return false;
        }
    }

    static bool Excluded(Transform node, Transform root)
    {
        for (Transform current = node; current != null; current = current.parent)
        {
            string name = current.name;
            if (name == "StackedWallStories" || name.StartsWith("Story_", StringComparison.Ordinal)
                || name.StartsWith("StackedRoofGarden", StringComparison.Ordinal)
                || name.StartsWith("Street", StringComparison.Ordinal)
                || name.StartsWith("Road", StringComparison.Ordinal)
                || name.StartsWith("Rail", StringComparison.Ordinal)
                || name.StartsWith("Train", StringComparison.Ordinal)) return true;
            if (current == root) break;
        }
        return false;
    }

    static void Surface(Material material, float smoothness, float metallic)
    {
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
    }

    static void Emission(Material material, Color color)
    {
        if (!material.HasProperty("_EmissionColor")) return;
        material.SetColor("_EmissionColor", color);
        material.EnableKeyword("_EMISSION");
        // Emission is a local surface response, without realtime/baked GI work.
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
    }

    static void ValidateTheme(int theme)
    {
        if (theme < 0 || theme >= ThemeCount) throw new ArgumentOutOfRangeException(nameof(theme));
    }

    static void ValidateRole(Role role)
    {
        if ((int)role < 0 || (int)role >= RoleCount) throw new ArgumentOutOfRangeException(nameof(role));
    }

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }
}
