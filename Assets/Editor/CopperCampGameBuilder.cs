#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CopperCamp
{
    /// <summary>Copper Camp > Build Full Game Scene: materials from the browser game's textures, and the one scene the game runs in.</summary>
    public static class CopperCampGameBuilder
    {
        const string ScenePath = "Assets/Scenes/CopperCamp.unity";
        const string MatDir = "Assets/Materials/Game";

        [MenuItem("Copper Camp/Build Full Game Scene")]
        public static void Build()
        {
            EnsureFolder("Assets/Materials"); EnsureFolder(MatDir); EnsureFolder("Assets/Scenes"); EnsureFolder("Assets/Settings");
            ImportSettings();

            Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/CC/tex/" + n + ".png");
            Texture2D bumpRock = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/RockBump.png");
            Texture2D bumpTimber = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/TimberBump.png");
            Texture2D soft = SoftDot();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var game = new GameObject("CopperCamp");
            var kit = game.AddComponent<ArtKit>();
            kit.rock = Lit("Rock", Color.white, T("rock"), bumpRock, .12f, 0);
            kit.dirt = Lit("Dirt", Color.white, T("dirt"), bumpRock, .1f, 0);
            kit.timber = Lit("Timber", Color.white, T("timber"), bumpTimber, .18f, 0);
            kit.brick = Lit("Brick", Color.white, T("brick"), null, .15f, 0);
            kit.steel = Lit("Steel", new Color(.42f, .4f, .38f), null, null, .45f, .5f);
            kit.rail = Lit("Rail", new Color(.5f, .45f, .4f), null, null, .55f, .6f);
            kit.ore = Lit("Ore", new Color(.62f, .36f, .16f), T("rock"), bumpRock, .35f, .3f);
            kit.plain = Lit("Plain", Color.white, null, null, .2f, 0);
            kit.cloth = Lit("Cloth", Color.white, null, null, .08f, 0);
            kit.glow = Emissive("Glow", new Color(1f, .75f, .4f), new Color(1f, .62f, .28f) * 3.5f);
            kit.window = Emissive("Window", new Color(1f, .8f, .5f), new Color(1f, .7f, .35f) * 2.2f);
            kit.redGlow = Emissive("RedGlow", new Color(1f, .3f, .25f), new Color(1f, .15f, .1f) * 3f);
            kit.greenGlow = Emissive("GreenGlow", new Color(.5f, 1f, .5f), new Color(.2f, .9f, .3f) * 2.5f);
            kit.particle = Particle("Particle", soft);
            kit.hero = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/CC/photo/hero.jpg");
            kit.photo = Unlit("Photo", kit.hero);
            kit.headframe = Cutout("HeadframePhoto", "Assets/Resources/CC/sprite/headframe_original.png");
            kit.headframeBlack = Cutout("HeadframeBlack", "Assets/Resources/CC/sprite/headframe_black.png");
            kit.headframeRed = Cutout("HeadframeRed", "Assets/Resources/CC/sprite/headframe_red.png", new Color(1f, .95f, .9f));
            kit.mmSign = Unlit("MMSign", ImportPlain("Assets/Resources/CC/sprite/mm_sign.png"));
            // the mine: photo-scanned rock, floor and timber from Poly Haven (CC0), each with its surface-detail map
            kit.mineRock = Lit("MineRock", new Color(1.75f, 1.68f, 1.6f), MineTex("dark_rock_diff_1k"), MineTex("dark_rock_nor_gl_1k", true), .22f, 0);
            kit.mineFace = Lit("MineFace", new Color(1.6f, 1.52f, 1.45f), MineTex("dark_rock_02_diff_1k"), MineTex("dark_rock_02_nor_gl_1k", true), .25f, 0);
            kit.mineFloor = Lit("MineFloor", new Color(1f, .95f, .9f), MineTex("rocks_ground_02_col_1k"), MineTex("rocks_ground_02_nor_gl_1k", true), .18f, 0);
            kit.mineTimber = Lit("MineTimber", Color.white, MineTex("rough_wood_diff_1k"), MineTex("rough_wood_nor_gl_1k", true), .12f, 0);
            kit.lagging = Lit("MineLagging", Color.white, MineTex("weathered_planks_diff_1k"), MineTex("weathered_planks_nor_gl_1k", true), .12f, 0);
            kit.water = Lit("MineWater", new Color(.05f, .05f, .055f), null, null, .93f, 0);

            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.fieldOfView = 60; cam.allowHDR = true;
            var cd = cam.GetUniversalAdditionalCameraData(); cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var vol = new GameObject("PostProcessing").AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 10; vol.sharedProfile = PostProfile();

            game.AddComponent<World3D>();
            game.AddComponent<GameUI>();

            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.2f, .18f, .15f);
            RenderSettings.reflectionIntensity = .12f;
            RenderSettings.skybox = null;

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Copper Camp: full game scene built at " + ScenePath);
        }

        static void ImportSettings()
        {
            foreach (var n in new[] { "rock", "dirt", "timber", "brick" })
            {
                var ti = AssetImporter.GetAtPath("Assets/Resources/CC/tex/" + n + ".png") as TextureImporter;
                if (ti == null) continue;
                ti.wrapMode = TextureWrapMode.Repeat; ti.maxTextureSize = 1024; ti.mipmapEnabled = true; ti.SaveAndReimport();
            }
            foreach (var path in Directory.GetFiles("Assets/Resources/CC/photo"))
            {
                if (path.EndsWith(".meta")) continue;
                var ti = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
                if (ti == null) continue;
                ti.npotScale = TextureImporterNPOTScale.None; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport();
            }
            foreach (var path in Directory.GetFiles("Assets/Resources/CC/audio"))
            {
                if (path.EndsWith(".meta")) continue;
                var ai = AssetImporter.GetAtPath(path.Replace('\\', '/')) as AudioImporter;
                if (ai == null) continue;
                var s = ai.defaultSampleSettings;
                bool longClip = path.Contains("music_") || path.Contains("amb_");
                s.loadType = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = longClip ? .45f : .7f;
                ai.defaultSampleSettings = s; ai.SaveAndReimport();
            }
        }

        static Material Save(Material m, string name)
        {
            string path = MatDir + "/" + name + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material Lit(string name, Color c, Texture2D albedo, Texture2D bump, float smooth, float metal)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            if (albedo != null) m.SetTexture("_BaseMap", albedo);
            if (bump != null) { m.SetTexture("_BumpMap", bump); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            return Save(m, name);
        }

        static Material Emissive(string name, Color c, Color e)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", .5f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", e);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return Save(m, name);
        }

        static Material Unlit(string name, Texture2D tex)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            return Save(m, name);
        }

        /// <summary>A photo cut-out (alpha clipped, two-sided, unlit) for the headframes.</summary>
        static Material Cutout(string name, string texPath) => Cutout(name, texPath, new Color(.82f, .76f, .68f));

        static Material Cutout(string name, string texPath, Color tint)
        {
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti != null) { ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp; ti.npotScale = TextureImporterNPOTScale.None; ti.SaveAndReimport(); }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) return null;
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetTexture("_BaseMap", tex); m.mainTexture = tex;
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", .4f); m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0);
            m.renderQueue = (int)RenderQueue.AlphaTest;
            return Save(m, name);
        }

        /// <summary>A mine texture from Assets/Textures/Mine; normal maps are imported as normal maps.</summary>
        static Texture2D MineTex(string name, bool normal = false)
        {
            string path = "Assets/Textures/Mine/" + name + ".jpg";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return null;
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.wrapMode = TextureWrapMode.Repeat; ti.maxTextureSize = 1024; ti.mipmapEnabled = true; ti.anisoLevel = 4;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D ImportPlain(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null) { ti.npotScale = TextureImporterNPOTScale.None; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Particle(string name, Texture2D tex)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
            return Save(m, name);
        }

        static Texture2D SoftDot()
        {
            const string path = "Assets/Textures/SoftDot.png";
            EnsureFolder("Assets/Textures");
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                    float a = Mathf.Clamp01(1 - d); a *= a;
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static VolumeProfile PostProfile()
        {
            const string path = "Assets/Settings/GamePostProfile.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(.9f); bloom.intensity.Override(.8f); bloom.scatter.Override(.7f);
            var color = profile.Add<ColorAdjustments>(true); color.postExposure.Override(1.1f); color.contrast.Override(6f); color.saturation.Override(-6f);
            var v = profile.Add<Vignette>(true); v.intensity.Override(.22f); v.smoothness.Override(.45f);
            var g = profile.Add<FilmGrain>(true); g.type.Override(FilmGrainLookup.Medium3); g.intensity.Override(.2f);
            foreach (var c in profile.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
            return profile;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
#endif
