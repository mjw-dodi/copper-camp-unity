#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CopperCamp
{
    /// <summary>
    /// One click builds the whole trial scene: Copper Camp > Build Drift Scene.
    /// Everything is made from plain shapes and textures generated in code, so no art files are needed.
    /// Copper Camp > Build Web (small) makes an uncompressed, size-optimised WebGL build in WebBuild/.
    /// </summary>
    public static class CopperCampBuilder
    {
        const string ScenePath = "Assets/Scenes/Drift.unity";
        const string WebBuildFolder = "WebBuild";

        [MenuItem("Copper Camp/Build Drift Scene")]
        public static void Build()
        {
            EnsureFolder("Assets/Materials");
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Textures");
            EnsureFolder("Assets/Settings");

            EnableLampShadows();

            // textures made in code: grey-scale detail, the material colour does the tinting
            Texture2D rockTex = MakeTexture("Rock", false, RockHeight);
            Texture2D rockBump = MakeTexture("RockBump", true, RockHeight);
            Texture2D timberTex = MakeTexture("Timber", false, TimberGrain);
            Texture2D timberBump = MakeTexture("TimberBump", true, TimberGrain);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material wallMat = MakeMat("Rock", new Color(0.42f, 0.36f, 0.31f), rockTex, rockBump, new Vector2(1.7f, 1.2f), 0.12f, 0f);
            Material floorMat = MakeMat("Floor", new Color(0.30f, 0.24f, 0.19f), rockTex, rockBump, new Vector2(1.5f, 1.7f), 0.22f, 0f);
            Material rubbleMat = MakeMat("Rubble", new Color(0.40f, 0.33f, 0.27f), rockTex, rockBump, Vector2.one, 0.1f, 0f);
            Material timberMat = MakeMat("Timber", new Color(0.55f, 0.38f, 0.22f), timberTex, timberBump, new Vector2(1f, 2f), 0.18f, 0f);
            Material steelMat = MakeMat("Steel", new Color(0.30f, 0.28f, 0.26f), rockTex, null, new Vector2(2f, 2f), 0.35f, 0.4f);
            Material railMat = MakeMat("Rail", new Color(0.45f, 0.38f, 0.33f), null, null, Vector2.one, 0.5f, 0.5f);
            Material oreMat = MakeMat("Ore", new Color(0.62f, 0.36f, 0.16f), rockTex, rockBump, Vector2.one, 0.35f, 0.3f);
            Material lanternMat = MakeMat("LanternGlow", new Color(1f, 0.75f, 0.4f), null, null, Vector2.one, 0.5f, 0f);
            lanternMat.EnableKeyword("_EMISSION");
            lanternMat.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.28f) * 3.5f);
            lanternMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(lanternMat);

            GameObject world = new GameObject("Drift");

            // tunnel shell, in 5 m sections so every section picks up its nearby lanterns
            for (int i = 0; i < 6; i++)
            {
                float z = 0.5f + i * 5f;
                Box("Floor" + i, world.transform, new Vector3(0f, -0.1f, z), new Vector3(4.2f, 0.2f, 5f), floorMat);
                Box("WallLeft" + i, world.transform, new Vector3(-2.1f, 1.5f, z), new Vector3(0.2f, 3.2f, 5f), wallMat);
                Box("WallRight" + i, world.transform, new Vector3(2.1f, 1.5f, z), new Vector3(0.2f, 3.2f, 5f), wallMat);
                Box("Ceiling" + i, world.transform, new Vector3(0f, 3.1f, z), new Vector3(4.4f, 0.2f, 5f), wallMat);
            }
            Box("EndWall", world.transform, new Vector3(0f, 1.5f, 28.1f), new Vector3(4.2f, 3.2f, 0.2f), wallMat);

            // timber sets every few metres, like the real drifts
            for (int i = 0; i < 7; i++)
            {
                float z = 3f + i * 3.6f;
                Box("PostL" + i, world.transform, new Vector3(-1.85f, 1.45f, z), new Vector3(0.3f, 3.0f, 0.3f), timberMat);
                Box("PostR" + i, world.transform, new Vector3(1.85f, 1.45f, z), new Vector3(0.3f, 3.0f, 0.3f), timberMat);
                Box("Cap" + i, world.transform, new Vector3(0f, 2.95f, z), new Vector3(4.1f, 0.3f, 0.3f), timberMat);
            }

            // a hanging lantern on every set, alternating sides, so the drift reads all the way down
            for (int i = 0; i < 7; i++)
            {
                float z = 3f + i * 3.6f;
                GameObject lantern = Box("Lantern" + i, world.transform, new Vector3(i % 2 == 0 ? -1.45f : 1.45f, 2.55f, z + 0.25f), new Vector3(0.14f, 0.2f, 0.14f), lanternMat);
                Light glow = new GameObject("LanternLight" + i).AddComponent<Light>();
                glow.transform.SetParent(lantern.transform, false);
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.66f, 0.35f);
                glow.range = 6.5f;
                glow.intensity = 3.4f;
                glow.shadows = LightShadows.None;
            }

            // rails and the ore car (on wheels) on the right
            Box("RailA", world.transform, new Vector3(0.65f, 0.03f, 13f), new Vector3(0.08f, 0.06f, 30f), railMat);
            Box("RailB", world.transform, new Vector3(1.35f, 0.03f, 13f), new Vector3(0.08f, 0.06f, 30f), railMat);
            for (int i = 0; i < 9; i++)
                Box("Tie" + i, world.transform, new Vector3(1.0f, -0.01f, 0.6f + i * 1.4f), new Vector3(1.1f, 0.05f, 0.18f), timberMat);
            GameObject car = Box("OreCar", world.transform, new Vector3(1.0f, 0.62f, 2.0f), new Vector3(1.1f, 0.7f, 1.7f), steelMat);
            Box("OreLoad", car.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.9f, 0.25f, 0.9f), oreMat);
            float[] wx = { 0.65f, 1.35f };
            float[] wz = { 1.45f, 2.55f };
            foreach (float x in wx)
                foreach (float z in wz)
                {
                    GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    wheel.name = "Wheel";
                    wheel.transform.SetParent(world.transform, false);
                    wheel.transform.position = new Vector3(x, 0.2f, z);
                    wheel.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                    wheel.transform.localScale = new Vector3(0.36f, 0.04f, 0.36f);
                    wheel.GetComponent<Renderer>().sharedMaterial = steelMat;
                    Object.DestroyImmediate(wheel.GetComponent<Collider>());
                }

            // muck pile on the left: a heap of broken rock rather than a few spheres
            GameObject pile = new GameObject("MuckPile");
            pile.transform.SetParent(world.transform, false);
            pile.transform.position = new Vector3(-0.9f, 0.15f, 2.0f);
            Random.InitState(1890);
            for (int i = 0; i < 16; i++)
            {
                float r = Mathf.Lerp(0.15f, 0.65f, Random.value) * (i < 4 ? 1.4f : 1f);
                float ang = Random.value * Mathf.PI * 2f;
                float dist = Random.value * (i < 4 ? 0.25f : 0.65f);
                GameObject s = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                s.name = "Rubble" + i;
                s.transform.SetParent(pile.transform, false);
                s.transform.localPosition = new Vector3(Mathf.Cos(ang) * dist, r * 0.25f + (i < 4 ? 0.12f : 0f), Mathf.Sin(ang) * dist);
                s.transform.localRotation = Random.rotation;
                s.transform.localScale = new Vector3(r, r * Random.Range(0.5f, 0.8f), r * Random.Range(0.7f, 1.1f));
                s.GetComponent<Renderer>().sharedMaterial = rubbleMat;
                Object.DestroyImmediate(s.GetComponent<Collider>());
            }

            // the load that flies into the car
            GameObject rockLoad = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rockLoad.name = "ThrownRock";
            rockLoad.transform.SetParent(world.transform, false);
            rockLoad.transform.localScale = new Vector3(0.3f, 0.22f, 0.26f);
            rockLoad.GetComponent<Renderer>().sharedMaterial = oreMat;
            Object.DestroyImmediate(rockLoad.GetComponent<Collider>());
            rockLoad.SetActive(false);

            // camera with post-processing, headlamp (a shadow-casting spot) and shovel
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.015f, 0.012f);
            cam.fieldOfView = 70f;
            cam.allowHDR = true;
            camGo.transform.position = new Vector3(0f, 1.55f, -0.6f);
            camGo.transform.rotation = Quaternion.Euler(6f, 0f, 0f);
            UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;

            GameObject lampGo = new GameObject("Headlamp");
            lampGo.transform.SetParent(camGo.transform, false);
            lampGo.transform.localPosition = new Vector3(0.2f, 0.28f, 0.05f);
            lampGo.transform.localRotation = Quaternion.Euler(5f, -4f, 0f);
            Light lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.color = new Color(1f, 0.82f, 0.58f);
            lamp.range = 26f;
            lamp.spotAngle = 105f;
            lamp.innerSpotAngle = 65f;
            lamp.intensity = 26f;
            lamp.shadows = LightShadows.Soft;
            lamp.shadowStrength = 0.85f;

            GameObject pivot = new GameObject("ShovelPivot");
            pivot.transform.SetParent(camGo.transform, false);
            pivot.transform.localPosition = new Vector3(0.45f, -0.5f, 0.7f);
            pivot.transform.localRotation = Quaternion.Euler(10f, -8f, 0f);
            Box("ShovelHandle", pivot.transform, new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.05f, 1.1f), timberMat, true, new Vector3(0f, 0f, 0.5f));
            Box("ShovelBlade", pivot.transform, new Vector3(0f, 0f, 0f), new Vector3(0.3f, 0.03f, 0.34f), steelMat, true, new Vector3(0f, 0f, 1.12f));

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.fogColor = new Color(0.07f, 0.05f, 0.035f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.18f, 0.145f);
            // an empty scene still reflects Unity's default blue sky; a mine should not
            RenderSettings.reflectionIntensity = 0.12f;

            MakePostVolume();

            // interface
            GameObject canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            Text hud = MakeText("Hud", canvasGo.transform, font, 26, TextAnchor.UpperCenter, new Vector2(0.1f, 0.9f), new Vector2(0.9f, 0.98f), new Color(0.95f, 0.88f, 0.7f));
            Text message = MakeText("Message", canvasGo.transform, font, 30, TextAnchor.MiddleCenter, new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.78f), new Color(1f, 0.93f, 0.78f));

            RectTransform track = MakeImage("MeterTrack", canvasGo.transform, new Vector2(0.25f, 0.1f), new Vector2(0.75f, 0.15f), new Color(0.12f, 0.09f, 0.07f, 0.9f));
            RectTransform zone = MakeImage("MeterZone", track, new Vector2(0.4f, 0f), new Vector2(0.6f, 1f), new Color(0.35f, 0.7f, 0.3f, 0.95f));
            RectTransform marker = MakeImage("MeterMarker", track, new Vector2(0f, 0f), new Vector2(0f, 1f), new Color(1f, 0.95f, 0.8f, 1f));
            RectTransform fatigueTrack = MakeImage("BackTrack", canvasGo.transform, new Vector2(0.25f, 0.06f), new Vector2(0.75f, 0.085f), new Color(0.12f, 0.09f, 0.07f, 0.9f));
            RectTransform fatigueFill = MakeImage("BackFill", fatigueTrack, new Vector2(0f, 0f), new Vector2(0f, 1f), new Color(0.8f, 0.35f, 0.2f, 1f));
            MakeText("BackLabel", canvasGo.transform, font, 18, TextAnchor.MiddleRight, new Vector2(0.05f, 0.055f), new Vector2(0.24f, 0.09f), new Color(0.85f, 0.75f, 0.6f)).text = "Back strain";

            // the game itself
            GameObject root = new GameObject("GameRoot");
            MuckGame game = root.AddComponent<MuckGame>();
            game.hudText = hud;
            game.messageText = message;
            game.meterZone = zone;
            game.meterMarker = marker;
            game.fatigueFill = fatigueFill;
            game.shovelPivot = pivot.transform;
            game.rock = rockLoad.transform;
            game.pile = pile.transform;
            game.oreCar = car.transform;
            game.headLamp = lamp;
            game.viewCamera = cam;
            EditorUtility.SetDirty(game);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Copper Camp: Drift scene built and saved to " + ScenePath);
            EditorUtility.DisplayDialog("Copper Camp", "Drift scene built and saved to " + ScenePath + ".\n\nPress the Play button, then Space (or click) to dig.", "OK");
        }

        [MenuItem("Copper Camp/Build Web (small)")]
        public static void BuildWebSmall()
        {
            EnableLampShadows();
            AssetDatabase.SaveAssets();
            NamedBuildTarget web = NamedBuildTarget.WebGL;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.High);
            PlayerSettings.SetIl2CppCodeGeneration(web, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.stripEngineCode = true;
            Debug.Log("Copper Camp: " + TrySetWasmCodeOptimization());
            AssetDatabase.SaveAssets();

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = System.Array.ConvertAll(System.Array.FindAll(EditorBuildSettings.scenes, x => x.enabled), x => x.path),
                locationPathName = WebBuildFolder,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            Debug.Log("Copper Camp: Web build " + s.result + " in " + s.totalTime + ", " + (s.totalSize / 1e6).ToString("0.0") + " MB, errors " + s.totalErrors + ", output " + s.outputPath);
        }

        // Unity's WebGL "Code Optimization" setting lives in the WebGL module; set it by reflection so this
        // script still compiles if the module or the name ever changes.
        static string TrySetWasmCodeOptimization()
        {
            foreach (System.Reflection.Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type t = asm.GetType("UnityEditor.WebGL.UserBuildSettings");
                if (t == null) continue;
                System.Reflection.PropertyInfo p = t.GetProperty("codeOptimization", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (p == null || !p.CanWrite || !p.PropertyType.IsEnum) return "code optimization setting not found; left as is";
                foreach (string name in new[] { "DiskSizeLTO", "DiskSize" })
                {
                    if (!System.Enum.IsDefined(p.PropertyType, name)) continue;
                    p.SetValue(null, System.Enum.Parse(p.PropertyType, name));
                    return "code optimization set to " + name;
                }
                return "no disk-size code optimization option; left as is";
            }
            return "WebGL build settings type not found; code optimization left as is";
        }

        // URP only draws spot/point-light shadows when the pipeline asset allows them.
        static void EnableLampShadows()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                SerializedObject so = new SerializedObject(asset);
                SerializedProperty p = so.FindProperty("m_AdditionalLightShadowsSupported");
                if (p != null) p.boolValue = true;
                p = so.FindProperty("m_AdditionalLightsRenderingMode");
                if (p != null && p.intValue != 1) p.intValue = 1; // per pixel
                p = so.FindProperty("m_SoftShadowsSupported");
                if (p != null) p.boolValue = true;
                p = so.FindProperty("m_AdditionalLightsPerObjectLimit");
                if (p != null) p.intValue = 8;
                p = so.FindProperty("m_SupportsHDR");
                if (p != null) p.boolValue = true;
                // the build strips shader versions by these "prefilter" fields, which Unity only refreshes when the asset is
                // edited in the Inspector; set them so lamp-lit and lamp-shadow versions are always kept
                p = so.FindProperty("m_PrefilteringModeAdditionalLight");
                if (p != null) p.intValue = 5;          // keep all: per-vertex, per-pixel and off
                p = so.FindProperty("m_PrefilteringModeAdditionalLightShadows");
                if (p != null) p.intValue = 1;          // keep both with and without
                p = so.FindProperty("m_PrefilteringModeMainLightShadows");
                if (p != null) p.intValue = 4;          // main light with and without shadows, and off
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
        }

        static void MakePostVolume()
        {
            const string path = "Assets/Settings/DriftPostProfile.asset";
            AssetDatabase.DeleteAsset(path);
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            Tonemapping tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.7f);
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(1.25f);
            color.contrast.Override(6f);
            color.saturation.Override(-8f);
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            FilmGrain grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.22f);

            foreach (VolumeComponent c in profile.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            GameObject go = new GameObject("PostProcessing");
            Volume v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 10f;
            v.sharedProfile = profile;
        }

        delegate float HeightFn(float u, float v);

        static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x * freq + 17.3f * i, y * freq + 9.1f * i);
                amp *= 0.5f; freq *= 2.03f;
            }
            return sum;
        }

        static float RockHeight(float u, float v)
        {
            float n = Fbm(u * 6f, v * 6f, 5);
            float ridge = 1f - Mathf.Abs(Fbm(u * 3f + 40f, v * 3f + 40f, 4) * 2f - 1f);
            return Mathf.Clamp01(0.35f + n * 0.75f - Mathf.Pow(ridge, 6f) * 0.35f);
        }

        static float TimberGrain(float u, float v)
        {
            float streak = Fbm(u * 22f, v * 1.5f, 4);
            float knots = Mathf.PerlinNoise(u * 4f + 3f, v * 4f + 7f);
            return Mathf.Clamp01(0.45f + streak * 0.7f - Mathf.Pow(knots, 8f) * 0.6f);
        }

        // Seamlessly tiling grey-scale texture: blend the noise with copies offset by one tile.
        static Texture2D MakeTexture(string name, bool normalMap, HeightFn fn)
        {
            const int size = 256;
            string path = "Assets/Textures/" + name + ".png";
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float h = fn(u, v) * (1f - u) * (1f - v)
                            + fn(u + 1f, v) * u * (1f - v)
                            + fn(u, v + 1f) * (1f - u) * v
                            + fn(u + 1f, v + 1f) * u * v;
                    float g = normalMap ? h : Mathf.Lerp(0.55f, 1f, h);
                    px[y * size + x] = new Color(g, g, g, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath), path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.maxTextureSize = 256;
            ti.mipmapEnabled = true;
            if (normalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.convertToNormalmap = true;
                ti.heightmapScale = 0.12f;
            }
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        static Material MakeMat(string name, Color color, Texture2D albedo, Texture2D bump, Vector2 tiling, float smoothness, float metallic)
        {
            string path = "Assets/Materials/" + name + ".mat";
            AssetDatabase.DeleteAsset(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material m = new Material(shader);
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (albedo != null && m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", albedo);
                m.SetTextureScale("_BaseMap", tiling);
            }
            if (bump != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", bump);
                m.EnableKeyword("_NORMALMAP");
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material mat)
        {
            return Box(name, parent, position, scale, mat, false, Vector3.zero);
        }

        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material mat, bool local, Vector3 localOffset)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (local) go.transform.localPosition = localOffset; else go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Text MakeText(string name, Transform parent, Font font, int size, TextAnchor anchor, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            Text t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = anchor; t.color = color; t.text = "";
            return t;
        }

        static RectTransform MakeImage(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return rt;
        }
    }
}
#endif
