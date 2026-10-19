using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Offline material authoring. Runtime uses these serialized textures and a
// prefiltered sky reflection; no textures or reflection probes are rebuilt.
public static class StackedCityMaterials
{
    const string Art = "Assets/Art/StackedCity/";
    const string City = "Assets/CityAfterimageV7/Materials/";
    const string ReflectionPath = "Assets/Resources/CityV7/StackedCityReflection.cubemap";

    public static void Configure()
    {
        Directory.CreateDirectory(Art + "Textures");
        AuthorPanels();
        AssetDatabase.Refresh();
        ConfigureTexture("ArchitecturalPanels.png", true);
        ConfigureTexture("ArchitecturalSurface.png", false);
        Cladding(City + "CityV7_Pale.mat", new Color(.91f,.90f,.85f), .23f, .02f);
        Cladding(City + "CityV7_Mineral.mat", new Color(.81f,.84f,.84f), .27f, .035f);
        Cladding(City + "CityV7_BlueStone.mat", new Color(.38f,.46f,.51f), .30f, .12f);
        Cladding(City + "CityV7_Structure.mat", new Color(.42f,.46f,.47f), .31f, .15f);
        Cladding("Assets/Art/CityLayers/Concrete.mat", new Color(.79f,.81f,.79f), .21f, .015f);
        Cladding(Art + "Materials/SC_Concrete.mat", new Color(.88f,.88f,.82f), .22f, .015f);
        Standard(City + "CityV7_Glass.mat", new Color(.23f,.34f,.38f), .88f, .58f);
        Standard(Art + "Materials/SC_Glass.mat", new Color(.21f,.33f,.36f), .86f, .56f);
        Standard(Art + "Materials/SC_Metal.mat", new Color(.24f,.29f,.30f), .46f, .68f);
        Standard(Art + "Materials/SC_White.mat", new Color(.91f,.92f,.88f), .63f, .24f);
        Standard(Art + "Materials/SC_Street.mat", new Color(.16f,.185f,.19f), .24f, .02f);
        Standard(Art + "Materials/SC_Marking.mat", new Color(.86f,.83f,.72f), .28f, 0f);
        Standard("Assets/Art/ExperienceSlice/WindowFrame.mat", new Color(.30f,.36f,.37f), .44f, .55f);
        Standard(Art + "Materials/SC_Orange.mat", new Color(.93f,.40f,.095f), .32f, .08f);
        Standard(Art + "Materials/SC_Joint.mat", new Color(.29f,.32f,.32f), .15f, 0f);
        AssetDatabase.SaveAssets();
    }

    static Material MaterialAt(string path)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    public static void RestoreNearFacadeMaterials(GameObject building)
    {
        // These source tower meshes were once treated as horizon silhouettes.
        // They now appear beside the playable road: restore their named facade
        // materials only on near-city instances, leaving the far skyline alone.
        foreach (var renderer in building.GetComponentsInChildren<Renderer>(true))
        {
            int separator = renderer.name.IndexOf("__CityV7_", StringComparison.Ordinal);
            if (separator < 0) continue;
            string materialName = renderer.name.Substring(separator + 2);
            var facade = AssetDatabase.LoadAssetAtPath<Material>(City + materialName + ".mat");
            if (facade == null) continue;
            var slots = renderer.sharedMaterials;
            for (int i=0; i<slots.Length; i++)
                if (slots[i] != null && (slots[i].name == "SkyMiddle" || slots[i].name == "SkyFar"))
                    slots[i] = facade;
            renderer.sharedMaterials = slots;
        }
    }

    static void Standard(string path, Color color, float smoothness, float metal)
    {
        var material = MaterialAt(path);
        material.shader = Shader.Find("Standard");
        material.color = color;
        material.SetFloat("_Metallic", metal);
        material.SetFloat("_Glossiness", smoothness);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }

    static void Cladding(string path, Color color, float smoothness, float metal)
    {
        var material = MaterialAt(path);
        material.shader = Shader.Find("EchoRun/MineralCladding");
        material.color = color;
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/ArchitecturalPanels.png"));
        material.SetTexture("_SurfaceDetail", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/ArchitecturalSurface.png"));
        material.SetFloat("_PanelScale", 1f / 4.8f);
        material.SetFloat("_DetailStrength", .8f);
        material.SetFloat("_Glossiness", smoothness);
        material.SetFloat("_Metallic", metal);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }

    static void ConfigureTexture(string name, bool srgb)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "Textures/" + name);
        importer.sRGBTexture = srgb;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.SaveAndReimport();
    }

    static float Noise(int x, int y)
    {
        unchecked
        {
            uint h = (uint)((x & 1023) * 374761393 + (y & 1023) * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 65535) / 65535f;
        }
    }

    static float JointDistance(int p)
    {
        int wrapped = (p % 512 + 512) % 512;
        return Mathf.Min(wrapped, 512 - wrapped);
    }

    static float Height(int x, int y)
    {
        float joint = Mathf.Min(JointDistance(x), JointDistance(y));
        return Mathf.SmoothStep(0f, 1f, joint / 4f) * .8f + Noise(x,y) * .025f;
    }

    static void AuthorPanels()
    {
        const int size = 1024;
        var albedo = new Texture2D(size,size,TextureFormat.RGB24,false);
        var surface = new Texture2D(size,size,TextureFormat.RGBA32,false,true);
        var colors = new Color32[size*size];
        var packed = new Color32[size*size];
        for (int y=0;y<size;y++) for (int x=0;x<size;x++)
        {
            float distance = Mathf.Min(JointDistance(x),JointDistance(y));
            float joint = 1f-Mathf.SmoothStep(0f,1f,distance/3.4f);
            float reveal = 1f-Mathf.SmoothStep(0f,1f,distance/11f);
            float grain = (Noise(x,y)-.5f)*.025f;
            float panel = ((x/512 + y/512*3)%4)*.009f;
            float pore = Noise(x+97,y+73)>.996f ? .055f : 0f;
            float value = .91f + panel + grain - joint*.30f - reveal*.035f - pore;
            colors[y*size+x] = new Color(value,value*.997f,value*.984f,1);
            float slopeX = Mathf.Clamp((Height(x-1,y)-Height(x+1,y))*.65f,-.48f,.48f);
            float slopeY = Mathf.Clamp((Height(x,y-1)-Height(x,y+1))*.65f,-.48f,.48f);
            packed[y*size+x] = new Color(.5f+slopeX,.5f+slopeY,1f-joint*.27f-reveal*.06f,.5f+grain*4f-joint*.24f);
        }
        albedo.SetPixels32(colors); albedo.Apply();
        surface.SetPixels32(packed); surface.Apply();
        File.WriteAllBytes(Art+"Textures/ArchitecturalPanels.png",albedo.EncodeToPNG());
        File.WriteAllBytes(Art+"Textures/ArchitecturalSurface.png",surface.EncodeToPNG());
        Object.DestroyImmediate(albedo); Object.DestroyImmediate(surface);
    }

    public static void BakeSkyReflection()
    {
        const int size = 128;
        const string sourcePath = Art + "Textures/StackedCityReflection.exr";
        var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CityV7/ExperienceSky.mat");
        if (sky == null) throw new InvalidOperationException("Stacked city reflection requires ExperienceSky.mat.");
        Directory.CreateDirectory(Art + "Textures");
        Scene previousScene = SceneManager.GetActiveScene();
        Material previousSky = RenderSettings.skybox;
        GameObject host = null;
        Cubemap cubemap = null;
        try
        {
            RenderSettings.skybox = sky;
            // A preview scene cannot be active in this editor. This synchronous,
            // sky-only bake uses a temporary non-serialized host in the current
            // scene; neither opening nor saving any scene is necessary.
            host = new GameObject("SkyReflectionAuthoring") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(host, previousScene);
            var probe = host.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
            probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
            probe.cullingMask = 0;
            probe.resolution = size;
            probe.hdr = true;
            probe.intensity = 1f;
            // Unity owns the six face orientations and the specular convolution.
            // An ordinary Apply(true) mip chain only downsamples each face; it
            // does not represent Standard's increasing surface roughness.
            if (!Lightmapping.BakeReflectionProbe(probe, sourcePath))
                throw new InvalidOperationException("Stacked city sky reflection bake failed.");
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Reflection EXR has no texture importer.");
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureShape = TextureImporterShape.TextureCube;
            settings.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
            importer.SetTextureSettings(settings);
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = size;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.format = TextureImporterFormat.RGBAHalf;
            platform.maxTextureSize = size;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();
            var filtered = AssetDatabase.LoadAssetAtPath<Cubemap>(sourcePath);
            if (filtered == null || filtered.mipmapCount < 7)
                throw new InvalidOperationException("Reflection bake did not produce the expected filtered cubemap mip chain.");
            cubemap = new Cubemap(filtered.width, TextureFormat.RGBAHalf, true)
            {
                name = "StackedCityReflection", filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp
            };
            for (int mip = 0; mip < cubemap.mipmapCount; mip++)
                for (int face = 0; face < 6; face++)
                    cubemap.SetPixels(filtered.GetPixels((CubemapFace)face, mip), (CubemapFace)face, mip);
            // Preserve the importer-authored roughness levels during upload.
            cubemap.Apply(false, false);
            var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(cubemap, ReflectionPath);
                cubemap = null;
            }
            else
            {
                EditorUtility.CopySerialized(cubemap, existing);
                EditorUtility.SetDirty(existing);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("STACKED_CITY_REFLECTION_OK NativeSpecular size=" + filtered.width + " mips=" + filtered.mipmapCount);
        }
        finally
        {
            if (cubemap != null) Object.DestroyImmediate(cubemap);
            if (host != null) Object.DestroyImmediate(host);
            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousScene);
                RenderSettings.skybox = previousSky;
            }
        }
    }
}
