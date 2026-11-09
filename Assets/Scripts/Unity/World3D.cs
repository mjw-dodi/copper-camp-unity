// Copper Camp — the 3D world. Builds every set in code at start-up (the hill, the mine drift for the five jobs,
// the dry room, the Uptown street and the interiors) and shows whichever one CC.scene.mode asks for.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CopperCamp
{
    public partial class World3D : MonoBehaviour
    {
        // ---------- kit ----------
        ArtKit art;
        Camera cam;
        Light sun;
        readonly Dictionary<string, Material> tiled = new Dictionary<string, Material>();
        MaterialPropertyBlock mpb;

        Material Tiled(Material m, float u, float v)
        {
            if (m == null) return null;
            string k = m.name + u.ToString("0.0") + "x" + v.ToString("0.0");
            if (tiled.TryGetValue(k, out var t)) return t;
            t = new Material(m) { name = k };
            if (t.HasProperty("_BaseMap")) t.SetTextureScale("_BaseMap", new Vector2(u, v));
            tiled[k] = t;
            return t;
        }

        GameObject Prim(PrimitiveType type, string n, Transform p, Vector3 pos, Vector3 scale, Material m, Color? tint = null, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = n;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (euler != null) go.transform.localRotation = Quaternion.Euler(euler.Value);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            if (tint != null) Tint(r, tint.Value);
            return go;
        }
        GameObject Box(string n, Transform p, Vector3 pos, Vector3 scale, Material m, Color? tint = null, Vector3? euler = null) => Prim(PrimitiveType.Cube, n, p, pos, scale, m, tint, euler);
        GameObject Cyl(string n, Transform p, Vector3 pos, Vector3 scale, Material m, Color? tint = null, Vector3? euler = null) => Prim(PrimitiveType.Cylinder, n, p, pos, scale, m, tint, euler);
        GameObject Ball(string n, Transform p, Vector3 pos, Vector3 scale, Material m, Color? tint = null) => Prim(PrimitiveType.Sphere, n, p, pos, scale, m, tint);

        void Tint(Renderer r, Color c)
        {
            mpb = mpb ?? new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mpb);
        }

        Light PointLight(string n, Transform p, Vector3 pos, Color c, float intensity, float range, bool shadows = false)
        {
            var go = new GameObject(n);
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        TextMesh Sign(string text, Transform p, Vector3 pos, float size, Color c, float yaw = 0)
        {
            var go = new GameObject("Sign " + text);
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.font = font; tm.fontSize = 64; tm.characterSize = size; tm.color = c;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return tm;
        }
        Font font;

        static readonly Color[] COATS = { Hex("3a2a1a"), Hex("2a2a3a"), Hex("4a2a2a"), Hex("2a3a2a"), Hex("5a4a3a"), Hex("3a3a44") };
        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        // ---------- sets ----------
        Transform hillRoot, underRoot, dryRoot, streetRoot, insideRoot, photoRoot;
        readonly Dictionary<string, Transform> interiors = new Dictionary<string, Transform>();

        void Awake()
        {
            art = ArtKit.Instance != null ? ArtKit.Instance : FindAnyObjectByType<ArtKit>();
            font = Resources.Load<Font>("CC/font/DejaVuSerif");   // open-licensed (DejaVu): has the dashes and curly quotes the story text uses
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cam = Camera.main;
            if (cam == null) { var cgo = new GameObject("Main Camera"); cgo.tag = "MainCamera"; cam = cgo.AddComponent<Camera>(); cgo.AddComponent<AudioListener>(); }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = .05f; cam.farClipPlane = 400f;
            // the 3D view only covers the left of the screen while the panel is up, so clear the whole screen first;
            // otherwise the see-through panel shows old frames (text from the last screen smearing on every change)
            var clr = new GameObject("ScreenClear").AddComponent<Camera>();
            clr.transform.SetParent(transform, false);
            clr.depth = cam.depth - 1; clr.clearFlags = CameraClearFlags.SolidColor; clr.backgroundColor = new Color(.04f, .03f, .02f);
            clr.cullingMask = 0; clr.rect = new Rect(0, 0, 1, 1);

            var sgo = new GameObject("Sun");
            sgo.transform.SetParent(transform, false);
            sun = sgo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft;
            sgo.transform.rotation = Quaternion.Euler(35, -30, 0);

            hillRoot = Root("Hill"); underRoot = Root("Under"); dryRoot = Root("DryRoom");
            streetRoot = Root("Street"); insideRoot = Root("Interiors"); photoRoot = Root("Photo");
            BuildPhoto(); BuildHill(); BuildUnder(); BuildDry(); BuildStreet(); BuildInteriors(); BuildWalkOverlay();
            BuildParticles();
        }

        Transform Root(string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            return go.transform;
        }

        // ---------------- PHOTO (title) ----------------
        Transform photoQuad;
        void BuildPhoto()
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "HeroPhoto";
            q.transform.SetParent(photoRoot, false);
            q.GetComponent<Renderer>().sharedMaterial = art.photo;
            photoQuad = q.transform;
        }

        // ---------------- THE HILL ----------------
        static readonly Vector2[] RIDGE =
        {
            new Vector2(200, 268), new Vector2(260, 252), new Vector2(330, 232), new Vector2(400, 208), new Vector2(470, 190), new Vector2(540, 176), new Vector2(600, 168),
            new Vector2(660, 146), new Vector2(720, 126), new Vector2(760, 116), new Vector2(800, 130), new Vector2(850, 160), new Vector2(900, 192), new Vector2(960, 222)
        };
        static readonly Dictionary<string, Vector2> LOCXY = new Dictionary<string, Vector2>
        {
            { "anaconda", new Vector2(560, 170) }, { "mountaincon", new Vector2(650, 140) }, { "speculator", new Vector2(745, 120) }, { "leonard", new Vector2(865, 225) },
            { "orphangirl", new Vector2(300, 235) }, { "original", new Vector2(470, 185) }, { "hennessy", new Vector2(425, 300) }, { "mm", new Vector2(398, 318) },
            { "union", new Vector2(438, 280) }, { "mercury", new Vector2(455, 345) }, { "gamers", new Vector2(372, 330) }, { "library", new Vector2(345, 312) },
            { "stjames", new Vector2(330, 350) }, { "gardens", new Vector2(905, 345) }, { "finntown", new Vector2(525, 300) }, { "gulch", new Vector2(600, 262) },
            { "centerville", new Vector2(620, 200) }, { "meaderville", new Vector2(855, 270) }, { "eastside", new Vector2(545, 335) }, { "chinatown", new Vector2(430, 362) },
            { "courthouse", new Vector2(380, 290) }, { "trestle", new Vector2(760, 395) }, { "granite", new Vector2(800, 128) }
        };

        static float RidgeY(float x)
        {
            if (x <= RIDGE[0].x) return RIDGE[0].y;
            for (int i = 1; i < RIDGE.Length; i++)
                if (x <= RIDGE[i].x) { float f = (x - RIDGE[i - 1].x) / (RIDGE[i].x - RIDGE[i - 1].x); return Mathf.Lerp(RIDGE[i - 1].y, RIDGE[i].y, f); }
            return RIDGE[RIDGE.Length - 1].y;
        }
        // map coordinates (the browser's 960x440 hill view) to metres
        static float MapX(float x) => (x - 600) * .11f;
        static float MapZ(float y) => (300 - y) * .2f;
        static float Height(float X, float Z)
        {
            float px = X / .11f + 600;
            float ridgeH = (300 - RidgeY(px)) * .16f;           // height of the ridge line above the flat
            float ridgeZ = MapZ(RidgeY(px)) + 4;                // the ridge runs where the map ridge is
            float up = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-6, ridgeZ, Z));
            float back = Z > ridgeZ ? Mathf.Clamp01(1 - (Z - ridgeZ) / 40f) : 1;
            float bigButte = 16 * Mathf.Exp(-((X + 46) * (X + 46) + (Z - 4) * (Z - 4)) / 110f);
            float n = (Mathf.PerlinNoise(X * .07f + 3, Z * .07f + 7) - .5f) * 3f;
            return Mathf.Max(0, ridgeH * up * back + n * up) + bigButte;
        }
        public static Vector3 LocPos(string k)
        {
            if (k == null || !LOCXY.TryGetValue(k, out var p)) return new Vector3(0, 6, 8);
            float X = MapX(p.x), Z = MapZ(p.y);
            return new Vector3(X, Height(X, Z), Z);
        }

        readonly List<ParticleSystem> stackSmoke = new List<ParticleSystem>();
        readonly List<Transform> billboards = new List<Transform>();
        Renderer terrain; Transform bigM; Light fireLight; Transform fireRoot; readonly List<GameObject> townWindows = new List<GameObject>();
        void BuildHill()
        {
            // terrain mesh
            int nx = 90, nz = 60; float x0 = -70, x1 = 60, z0 = -24, z1 = 60;
            var verts = new Vector3[(nx + 1) * (nz + 1)]; var uv = new Vector2[verts.Length]; var tris = new int[nx * nz * 6];
            for (int j = 0, i = 0; j <= nz; j++)
                for (int k = 0; k <= nx; k++, i++)
                {
                    float X = Mathf.Lerp(x0, x1, k / (float)nx), Z = Mathf.Lerp(z0, z1, j / (float)nz);
                    verts[i] = new Vector3(X, Height(X, Z), Z); uv[i] = new Vector2(X / 9f, Z / 9f);
                }
            for (int j = 0, t = 0; j < nz; j++)
                for (int k = 0; k < nx; k++)
                {
                    int a = j * (nx + 1) + k;
                    tris[t++] = a; tris[t++] = a + nx + 1; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = a + nx + 1; tris[t++] = a + nx + 2;
                }
            var mesh = new Mesh { name = "Hill", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts; mesh.uv = uv; mesh.triangles = tris; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var tgo = new GameObject("Terrain", typeof(MeshFilter), typeof(MeshRenderer));
            tgo.transform.SetParent(hillRoot, false);
            tgo.GetComponent<MeshFilter>().sharedMesh = mesh;
            terrain = tgo.GetComponent<MeshRenderer>();
            terrain.sharedMaterial = art.dirt;
            Tint(terrain, new Color(.72f, .64f, .56f));

            // mine dumps (grey-brown waste rock) under each headframe, headframes and engine houses
            string[] hf = { "orphangirl", "original", "anaconda", "mountaincon", "speculator", "granite", "leonard" };
            float[] sc = { .8f, .9f, 1, 1, 1.05f, .92f, .9f };
            for (int i = 0; i < hf.Length; i++)
            {
                var p = LocPos(hf[i]);
                var hm = hf[i] == "orphangirl" && art.headframeRed != null ? art.headframeRed : (i % 2 == 1 && art.headframeBlack != null ? art.headframeBlack : art.headframe);
                // the Orphan Girl is the red one, from a photograph of the real headframe; it gets a little extra height so it reads
                PhotoHeadframe(hillRoot, p, 15f * sc[i] * (hm == art.headframeRed ? 1.6f : 1f), i % 3 == 0 && hm != art.headframeRed, hm);
            }
            // the seven stacks of the Anaconda "Neversweat"
            var ana = LocPos("anaconda");
            for (int i = 0; i < 7; i++)
            {
                var sp0 = ana + new Vector3(-7.5f + i * 1.25f, 0, 3);
                sp0.y = Height(sp0.x, sp0.z) - .5f;
                Cyl("Stack", hillRoot, sp0 + new Vector3(0, 6, 0), new Vector3(.8f, 6.5f, .8f), Tiled(art.brick, 1, 4), Hex("5a4038"));
                var smoke = MakeParticles("StackSmoke", hillRoot, new Color(.35f, .32f, .3f, .35f), 2.2f, true);
                smoke.transform.localPosition = sp0 + new Vector3(0, 12.6f, 0);
                var sm = smoke.main; sm.startSpeed = 1.2f; sm.startLifetime = 9; sm.gravityModifier = -.02f; sm.maxParticles = 300;
                var sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .6f, 1, 3.5f));
                var vel = smoke.velocityOverLifetime; vel.enabled = true; vel.x = new ParticleSystem.MinMaxCurve(1.6f); vel.y = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(0f);
                stackSmoke.Add(smoke);
            }

            // Big Butte's "M" (from 1910)
            var mgo = new GameObject("BigM"); mgo.transform.SetParent(hillRoot, false);
            var mp = new Vector3(-46, Height(-46, -1) + .4f, -1);
            mgo.transform.localPosition = mp; mgo.transform.localRotation = Quaternion.Euler(-28, 0, 0);
            Box("M1", mgo.transform, new Vector3(-1.6f, 0, 0), new Vector3(.5f, .06f, 3), art.plain, Color.white);
            Box("M2", mgo.transform, new Vector3(1.6f, 0, 0), new Vector3(.5f, .06f, 3), art.plain, Color.white);
            Box("M3", mgo.transform, new Vector3(-.8f, 0, .6f), new Vector3(.5f, .06f, 2), art.plain, Color.white, new Vector3(0, 35, 0));
            Box("M4", mgo.transform, new Vector3(.8f, 0, .6f), new Vector3(.5f, .06f, 2), art.plain, Color.white, new Vector3(0, -35, 0));
            bigM = mgo.transform;

            // Uptown on the flat and the miners' houses: brick blocks, frame houses with gable roofs, lit windows at night
            BuildHillHouses();
            BuildFarRanges();
            // the Milwaukee Road trestle south of town
            var tr = LocPos("trestle");
            for (int i = 0; i < 9; i++) Box("TrestleBent", hillRoot, new Vector3(tr.x - 8 + i * 2, 1.5f, tr.z), new Vector3(.2f, 3f, .2f), art.timber, null, new Vector3(0, 0, i % 2 == 0 ? 8 : -8));
            Box("TrestleDeck", hillRoot, new Vector3(tr.x, 3.05f, tr.z), new Vector3(18, .25f, .9f), art.timber);

            // fire over the Speculator (1917)
            var sp = LocPos("speculator");
            var fgo = new GameObject("SpeculatorFire"); fgo.transform.SetParent(hillRoot, false); fgo.transform.localPosition = sp + Vector3.up * 2;
            fireLight = PointLight("FireGlow", fgo.transform, Vector3.zero, new Color(1, .45f, .15f), 0, 30);
            fireRoot = fgo.transform;
        }

        /// <summary>A headframe cut out of a Library of Congress photograph (Carol M. Highsmith, public domain),
        /// standing on the hill and turning to face the camera. Falls back to the built model without the photo.</summary>
        void PhotoHeadframe(Transform p, Vector3 pos, float height, bool flip, Material mat = null)
        {
            mat = mat != null ? mat : art.headframe;
            if (mat == null || mat.mainTexture == null) { Headframe(p, pos, height / 8f, flip); return; }
            var tex = mat.mainTexture;
            float aspect = tex.width / (float)tex.height;
            var root = new GameObject("PhotoHeadframe").transform; root.SetParent(p, false);
            root.localPosition = new Vector3(pos.x, Height(pos.x, pos.z) - .35f, pos.z);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(root, false);
            q.transform.localScale = new Vector3(height * aspect * (flip ? -1 : 1), height, 1);
            q.transform.localPosition = new Vector3(0, height / 2, 0);
            q.GetComponent<Renderer>().sharedMaterial = mat;
            q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            billboards.Add(root);
            if (mat == art.headframeRed) return; // that photograph already has its hoist house in it
            // the hoist house behind it, set into the slope
            var hh = new Vector3(pos.x + (flip ? -2.5f : 2.5f), 0, pos.z + 3.5f); hh.y = Height(hh.x, hh.z);
            Box("Hoisthouse", p, hh + new Vector3(0, .9f, 0), new Vector3(4, 3.2f, 3.4f), Tiled(art.timber, 2, 1), Hex("9a8a7a"));
            Box("Roof", p, hh + new Vector3(0, 2.6f, 0), new Vector3(4.3f, .25f, 3.7f), art.steel, Hex("4a3a30"));
            Cyl("HoistStack", p, hh + new Vector3(1.4f, 3.6f, .9f), new Vector3(.35f, 1.6f, .35f), art.brick, Hex("5a4038"));
        }

        void Headframe(Transform p, Vector3 pos, float s, bool steel)
        {
            var root = new GameObject("Headframe"); root.transform.SetParent(p, false); root.transform.localPosition = pos;
            var m = steel ? art.steel : art.timber;
            float h = 7 * s;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0 ? -1 : 1), sz = (i < 2 ? -1 : 1);
                var leg = Box("Leg", root.transform, new Vector3(sx * .9f * s, h / 2, sz * .7f * s), new Vector3(.18f * s, h, .18f * s), m, steel ? Hex("3a3a3a") : (Color?)null);
                leg.transform.localRotation = Quaternion.Euler(sz * -4, 0, sx * 4);
            }
            for (int i = 1; i < 5; i++) Box("Brace", root.transform, new Vector3(0, h * i / 5f, -.7f * s), new Vector3(1.8f * s, .1f * s, .1f * s), m, steel ? Hex("3a3a3a") : (Color?)null);
            Box("Backleg", root.transform, new Vector3(0, h * .45f, 2.2f * s), new Vector3(.16f * s, h * 1.05f, .16f * s), m, steel ? Hex("3a3a3a") : (Color?)null, new Vector3(-28, 0, 0));
            Cyl("Sheave", root.transform, new Vector3(0, h + .3f * s, 0), new Vector3(1.1f * s, .06f * s, 1.1f * s), art.steel, Hex("2a2a2a"), new Vector3(90, 0, 0));
            Box("Hoisthouse", root.transform, new Vector3(0, 1.1f * s, 5.5f * s), new Vector3(3.5f * s, 2.2f * s, 3 * s), Tiled(art.timber, 2, 1));
            Box("Roof", root.transform, new Vector3(0, 2.35f * s, 5.5f * s), new Vector3(3.8f * s, .25f * s, 3.3f * s), art.steel, Hex("4a3a30"));
            Cyl("HoistStack", root.transform, new Vector3(1.2f * s, 3.2f * s, 6.4f * s), new Vector3(.3f * s, 1.6f * s, .3f * s), art.brick);
        }

        // ---------------- THE DRIFT (all five jobs) ----------------
        const float DRIFT = 66f;
        Transform muckProps, timberProps, drillProps, tramProps;
        Transform shovelLoad;
        Transform pile, car, carLoad, shovel, thrown, swingTimber, maul, drillRig, drillSteel, tramCar, faceWall;
        readonly List<GameObject> placedSets = new List<GameObject>();
        readonly List<GameObject> drillHoles = new List<GameObject>();
        readonly List<TextMesh> slowSigns = new List<TextMesh>();
        readonly List<GameObject> chalkMarks = new List<GameObject>();
        readonly List<Transform> drillArms = new List<Transform>(), drillHands = new List<Transform>(), tramArms = new List<Transform>(), tramHands = new List<Transform>();
        Transform drillArm, drillHose, sprag; ParticleSystem sparks; int lastIce = -1;
        static Vector3 HolePos(int i) => new Vector3(-.9f + (i % 4) * .6f, .95f + (i / 4) * .75f, 6.585f);
        Light headlamp, heatLight; readonly List<Light> lanterns = new List<Light>();
        ParticleSystem dustFall, drillDust, steam, snow;

        void BuildUnder()
        {
            var w = underRoot;
            BuildDrift(w);
            faceWall = Box("Face", w, new Vector3(0, 1.6f, DRIFT), new Vector3(4.8f, 3.8f, .4f), Tiled(FaceMat, 1.4f, 1.1f), new Color(1.5f, 1.45f, 1.4f)).transform;

            // muck and hot box: the pile, the car, the shovel and the thrown rock
            muckProps = new GameObject("MuckProps").transform; muckProps.SetParent(w, false);
            // the muck pile: broken ore heaped against the face, big pieces at the bottom, fines on top
            pile = new GameObject("MuckPile").transform; pile.SetParent(muckProps, false); pile.localPosition = new Vector3(-.85f, 0, 2.9f);
            var rr = CC.Rnd(1890);
            var ore = new Color(1.25f, 1.15f, 1.05f);   // the photo rock is dark; lift it
            Ball("Heap", pile, new Vector3(0, -.05f, .35f), new Vector3(1.9f, 1.05f, 1.7f), Tiled(RockMat, 1.4f, 1.4f), ore);
            Ball("Heap2", pile, new Vector3(-.35f, -.08f, -.25f), new Vector3(1.2f, .7f, 1.1f), Tiled(RockMat, 1.4f, 1.4f), ore * .95f);
            Ball("Heap3", pile, new Vector3(.45f, -.1f, .0f), new Vector3(1.0f, .55f, 1.2f), Tiled(RockMat, 1.4f, 1.4f), ore * 1.05f);
            for (int i = 0; i < 46; i++)
            {
                float r = Mathf.Lerp(.07f, .3f, rr() * rr()), ang = rr() * Mathf.PI * 2, dist = Mathf.Sqrt(rr()) * .92f;
                float y = .47f * Mathf.Sqrt(Mathf.Max(0, 1 - dist * dist)) + r * .15f - .05f;
                var rock = Prim(PrimitiveType.Cube, "Rock", pile, new Vector3(Mathf.Cos(ang) * dist * .95f, y, Mathf.Sin(ang) * dist * .85f + .3f), new Vector3(r * 1.3f, r * .75f, r), RockMat,
                    Color.Lerp(new Color(1.3f, 1.2f, 1.1f), new Color(1.1f, .85f, .6f), rr()));
                rock.transform.localRotation = Quaternion.Euler(rr() * 50 - 25, rr() * 360, rr() * 50 - 25);
            }
            car = OreCar(muckProps, new Vector3(1.05f, .78f, 3.1f));
            // the No. 2 shovel in your hands
            shovel = new GameObject("Shovel").transform; shovel.SetParent(muckProps, false);
            Cyl("Handle", shovel, new Vector3(0, 0, .55f), new Vector3(.045f, .6f, .045f), Tiled(art.timber, 1, 2), new Color(.95f, .85f, .7f), new Vector3(90, 0, 0));
            Box("Grip", shovel, new Vector3(0, 0, -.06f), new Vector3(.16f, .04f, .05f), art.timber, Hex("5a3a24"));
            Box("Blade", shovel, new Vector3(0, -.03f, 1.28f), new Vector3(.3f, .025f, .34f), art.steel, Hex("5a5550"), new Vector3(-12, 0, 0));
            shovelLoad = new GameObject("Load").transform; shovelLoad.SetParent(shovel, false);
            for (int i = 0; i < 6; i++)
                Prim(PrimitiveType.Cube, "Chunk", shovelLoad, new Vector3((i % 3 - 1) * .07f, .04f + (i / 3) * .03f, 1.24f + (i % 2) * .08f), new Vector3(.09f, .06f, .08f), art.rock, new Color(.6f, .5f, .4f)).transform.localRotation = Quaternion.Euler(i * 23, i * 47, i * 11);
            carLoad = car.Find("Load");
            thrown = Ball("Thrown", muckProps, Vector3.zero, new Vector3(.3f, .22f, .26f), art.ore).transform;
            Box("IceBucket", muckProps, new Vector3(-1.5f, .25f, 3.6f), new Vector3(.4f, .5f, .4f), PostMat);

            // timber: a set swinging from the hoist, the maul, and the sets already placed
            timberProps = new GameObject("TimberProps").transform; timberProps.SetParent(w, false);
            var pivot = new GameObject("TimberPivot").transform; pivot.SetParent(timberProps, false); pivot.localPosition = new Vector3(0, 2.85f, 4.2f);
            Cyl("Rope", pivot, new Vector3(0, -.45f, 0), new Vector3(.03f, .45f, .03f), art.plain, Hex("8a7a5a"));
            Box("Timber", pivot, new Vector3(0, -1.6f, 0), new Vector3(.3f, 1.6f, .3f), Tiled(PostMat, 1, 2.5f), new Color(.85f, .8f, .72f));
            swingTimber = pivot;
            maul = new GameObject("Maul").transform; maul.SetParent(timberProps, false); maul.localPosition = new Vector3(.75f, .7f, 1.4f);
            Cyl("Handle", maul, new Vector3(0, 0, .45f), new Vector3(.05f, .45f, .05f), art.timber, null, new Vector3(90, 0, 0));
            Box("Head", maul, new Vector3(0, 0, .95f), new Vector3(.12f, .14f, .3f), art.steel, Hex("3a3633"));
            for (int i = 0; i < 12; i++)
            {
                var set = Box("NewSet", timberProps, new Vector3(i % 2 == 0 ? -1.2f : 1.2f, 1.45f, 5 + (i / 2) * .5f), new Vector3(.28f, 2.9f, .28f), Tiled(art.timber, 1, 2));
                set.SetActive(false); placedSets.Add(set);
            }

            // drill: a Leyner-type machine on an arm off a stull column, boring the round laid out in chalk on the face
            drillProps = new GameObject("DrillProps").transform; drillProps.SetParent(w, false);
            Box("DrillFace", drillProps, new Vector3(0, 1.6f, 6.75f), new Vector3(4.8f, 3.9f, .3f), Tiled(FaceMat, 1.4f, 1.1f), new Color(1.5f, 1.42f, 1.34f));
            for (int i = 0; i < 8; i++)
            {
                var hp = HolePos(i);
                var hole = Cyl("Hole", drillProps, hp, new Vector3(.13f, .012f, .13f), art.plain, Hex("050403"), new Vector3(90, 0, 0));
                hole.SetActive(false); drillHoles.Add(hole);
                var mark = new GameObject("ChalkMark").transform; mark.SetParent(drillProps, false); mark.localPosition = hp + new Vector3(0, 0, -.004f);
                Box("ChalkA", mark, Vector3.zero, new Vector3(.16f, .02f, .01f), art.plain, Hex("a8a094"), new Vector3(0, 0, 45));
                Box("ChalkB", mark, Vector3.zero, new Vector3(.16f, .02f, .01f), art.plain, Hex("a8a094"), new Vector3(0, 0, -45));
                chalkMarks.Add(mark.gameObject);
            }
            Cyl("Column", drillProps, new Vector3(1.45f, 1.5f, 5.7f), new Vector3(.13f, 1.55f, .13f), art.steel, Hex("2e2c2a"));
            Box("ColumnJack", drillProps, new Vector3(1.45f, .15f, 5.7f), new Vector3(.3f, .3f, .3f), art.steel, Hex("2e2c2a"));
            drillArm = Box("DrillArm", drillProps, Vector3.zero, Vector3.one, art.steel, Hex("3a3836")).transform;
            drillRig = new GameObject("DrillRig").transform; drillRig.SetParent(drillProps, false);
            Cyl("Cylinder", drillRig, new Vector3(0, 0, 0), new Vector3(.17f, .32f, .17f), art.steel, Hex("4a4846"), new Vector3(90, 0, 0));
            Box("Backhead", drillRig, new Vector3(0, 0, -.36f), new Vector3(.22f, .22f, .12f), art.steel, Hex("2a2826"));
            Box("Handles", drillRig, new Vector3(0, -.02f, -.47f), new Vector3(.56f, .045f, .045f), art.steel, Hex("1e1c1a"));
            Cyl("FeedScrew", drillRig, new Vector3(0, .13f, -.15f), new Vector3(.04f, .34f, .04f), art.rail, null, new Vector3(90, 0, 0));
            Cyl("Chuck", drillRig, new Vector3(0, 0, .34f), new Vector3(.11f, .05f, .11f), art.steel, Hex("2a2826"), new Vector3(90, 0, 0));
            drillSteel = Cyl("Steel", drillRig, new Vector3(0, 0, .66f), new Vector3(.035f, .3f, .035f), art.rail, null, new Vector3(90, 0, 0)).transform;
            Ball("Bit", drillRig, new Vector3(0, 0, .96f), Vector3.one * .065f, art.steel, Hex("6a6866"));
            drillHose = Box("AirHose", drillProps, Vector3.zero, Vector3.one, art.plain, Hex("161412")).transform;
            for (int s2 = 0; s2 < 2; s2++)
            {
                drillArms.Add(Box("Sleeve", drillProps, Vector3.zero, Vector3.one, art.cloth, Hex("3a2a1a")).transform);
                drillHands.Add(Ball("Hand", drillProps, Vector3.zero, Vector3.one * .1f, art.cloth, Hex("b08868")).transform);
            }
            drillRig.localPosition = HolePos(0) + new Vector3(0, 0, -.97f);

            // tram: the car you push down the long drift, and the speed signs at the curves
            tramProps = new GameObject("TramProps").transform; tramProps.SetParent(w, false);
            tramCar = OreCar(tramProps, new Vector3(1, .78f, 2.5f));
            for (int s2 = 0; s2 < 2; s2++)
            {
                tramArms.Add(Box("Sleeve", tramProps, Vector3.zero, Vector3.one, art.cloth, Hex("3a2a1a")).transform);
                tramHands.Add(Ball("Hand", tramProps, Vector3.zero, Vector3.one * .1f, art.cloth, Hex("b08868")).transform);
            }
            sprag = Box("Sprag", tramProps, Vector3.zero, new Vector3(.08f, .08f, 1.1f), PostMat).transform;
            float[] zones = { .3f, .6f, .88f }; int[] lim = { 60, 55, 50 };
            for (int i = 0; i < 3; i++)
            {
                float z = 4 + zones[i] * (DRIFT - 10);
                Box("SignBoard", tramProps, new Vector3(-1.75f, 1.9f, z), new Vector3(.05f, .45f, .7f), art.plain, Hex("d8cfb8"));
                var t = Sign("SLOW\n" + lim[i], tramProps, new Vector3(-1.72f, 1.9f, z), .05f, Hex("8a1a10"), 90);
                slowSigns.Add(t);
            }

            // the miner's headlamp (a candle in the browser game) and the heat glow for the Leonard
            headlamp = new GameObject("Headlamp").AddComponent<Light>();
            headlamp.type = LightType.Spot; headlamp.color = new Color(1, .93f, .8f); headlamp.intensity = 26; headlamp.range = 26;
            headlamp.spotAngle = 105; headlamp.innerSpotAngle = 65; headlamp.shadows = LightShadows.Soft; headlamp.shadowStrength = .85f;
            headlamp.transform.SetParent(underRoot, false);
            heatLight = PointLight("HeatGlow", w, new Vector3(0, 2.5f, 3), new Color(1, .3f, .1f), 0, 9);
        }

        // ---------------- THE DRY ROOM ----------------
        GameObject dryBoss, dryPartner, dryGreen;
        void BuildDry()
        {
            var r = dryRoot;
            Room(r, art.brick, 9, 6, 3.6f);
            for (int i = 0; i < 9; i++)
            {
                Cyl("Hook", r, new Vector3(-3.6f + i * .9f, 2.3f, 2.85f), new Vector3(.03f, .1f, .03f), art.steel, null, new Vector3(90, 0, 0));
                Box("Clothes", r, new Vector3(-3.6f + i * .9f, 1.85f, 2.8f), new Vector3(.45f, .8f, .12f), art.cloth, COATS[i % COATS.Length]);
            }
            Box("Bench", r, new Vector3(0, .45f, 1.9f), new Vector3(6, .08f, .45f), art.timber);
            Box("BenchLeg", r, new Vector3(-2.6f, .22f, 1.9f), new Vector3(.08f, .45f, .4f), art.timber);
            Box("BenchLeg", r, new Vector3(2.6f, .22f, 1.9f), new Vector3(.08f, .45f, .4f), art.timber);
            Box("Stove", r, new Vector3(3.4f, .6f, 1.5f), new Vector3(.7f, 1.2f, .7f), art.steel, Hex("1e1a18"));
            Box("StoveGlow", r, new Vector3(3.4f, .5f, 1.14f), new Vector3(.4f, .2f, .02f), art.glow);
            PointLight("StoveLight", r, new Vector3(3.2f, .8f, .8f), new Color(1, .5f, .2f), 2.2f, 5);
            var lamp = Box("Lamp", r, new Vector3(0, 3.0f, 0), new Vector3(.2f, .25f, .2f), art.glow);
            PointLight("LampLight", lamp.transform, Vector3.zero, new Color(1, .75f, .45f), 4.5f, 10, true);
            dryBoss = Person("Boss", r, new Vector3(0, 0, .6f), 180, Hex("5a4a3a"), Hex("2a2018"), false, Hex("d9a98a"));
            dryPartner = Person("Partner", r, new Vector3(-1.6f, 0, 1.2f), 160, Hex("3a2a1a"), Hex("1a1410"));
            dryGreen = Person("Greenhorn", r, new Vector3(1.7f, 0, 1.4f), 200, Hex("4a3a3a"), Hex("3a3a30"), false, Hex("e0bfa0"));
        }

        /// <summary>A box room: floor, ceiling, back and side walls, open toward -Z where the camera stands.</summary>
        void Room(Transform r, Material wall, float w, float d, float h)
        {
            Box("Floor", r, new Vector3(0, -.05f, 0), new Vector3(w, .1f, d), Tiled(art.timber, w / 2, d / 2), Hex("7a6a5a"));
            Box("Ceiling", r, new Vector3(0, h, 0), new Vector3(w, .1f, d), Tiled(art.timber, w / 2, d / 2), Hex("6a5a4a"));
            Box("Back", r, new Vector3(0, h / 2, d / 2), new Vector3(w, h, .1f), Tiled(wall, w / 2, h / 2));
            Box("Left", r, new Vector3(-w / 2, h / 2, 0), new Vector3(.1f, h, d), Tiled(wall, d / 2, h / 2));
            Box("Right", r, new Vector3(w / 2, h / 2, 0), new Vector3(.1f, h, d), Tiled(wall, d / 2, h / 2));
        }

        // ---------------- THE STREET ----------------
        const float K = 1 / 40f;   // browser street pixels to metres
        GameObject player, kessler, landlady, nellFig; readonly List<GameObject> npcFigs = new List<GameObject>();
        Transform unionWhole, unionRuin; readonly List<GameObject> streetWindows = new List<GameObject>();
        void BuildStreet()
        {
            var s = streetRoot; float L = CC.WORLD * K;
            Box("Boardwalk", s, new Vector3(L / 2, .1f, 0), new Vector3(L + 10, .2f, 3), Tiled(art.timber, L / 2, 2), Hex("8a7a6a"));
            Box("Street", s, new Vector3(L / 2, -.05f, -5), new Vector3(L + 30, .1f, 7), Tiled(art.dirt, L / 3, 2));
            Box("RailN", s, new Vector3(L / 2, .02f, -4.5f), new Vector3(L + 30, .04f, .07f), art.rail);
            Box("RailS", s, new Vector3(L / 2, .02f, -5.6f), new Vector3(L + 30, .04f, .07f), art.rail);
            Box("FarSide", s, new Vector3(L / 2, -.05f, -11), new Vector3(L + 40, .1f, 6), Tiled(art.dirt, L / 3, 2), Hex("6a5a4a"));

            // the night sky and the hill behind Uptown with its lit windows and headframe lamps
            BuildStreetNight(s, L);

            // the places, with filler storefronts between them
            foreach (var sp in CC.STREET) Facade(s, sp);
            var r = CC.Rnd(31);
            string[] names = { "SALOON", "DRY GOODS", "ASSAY OFFICE", "BARBER", "CIGARS", "ROOMS", "BAKERY", "UNDERTAKER", "HARDWARE", "PHOTOGRAPHER", "BANK", "LODGING", "MEAT MARKET", "TAILOR" };
            for (int i = 0; i < CC.STREET.Length - 1; i++)
            {
                var a = CC.STREET[i]; var b = CC.STREET[i + 1];
                float x0 = (a.x + a.w / 2) * K + .3f, x1 = (b.x - b.w / 2) * K - .3f;
                if (x1 - x0 < 1.2f) continue;
                float w = x1 - x0, h = 3 + r() * 2.5f;
                bool brick = r() < .5f;
                Box("Filler", s, new Vector3((x0 + x1) / 2, h / 2, 3.2f), new Vector3(w, h, 3), brick ? Tiled(art.brick, w / 2, h / 2) : Tiled(art.timber, w / 2, h / 2), brick ? (Color?)null : Hex("9a8a7a"));
                Sign(names[(int)(r() * names.Length) % names.Length], s, new Vector3((x0 + x1) / 2, h - .5f, 1.65f), .05f, Hex("f0e4cc"), 0);
                var win = Box("Window", s, new Vector3((x0 + x1) / 2, 1.5f, 1.68f), new Vector3(Mathf.Min(1.2f, w * .6f), 1, .02f), art.window);
                streetWindows.Add(win);
                WindowSpill(s, (x0 + x1) / 2, 1.5f, new Color(1, .75f, .45f));
                Box("Awning", s, new Vector3((x0 + x1) / 2, 2.45f, 1.2f), new Vector3(w * .9f, .06f, 1), art.cloth, r() < .5f ? Hex("6a2a22") : Hex("2a4a3a"), new Vector3(-12, 0, 0));
            }

            // lampposts every 250 px, each with a halo and a pool of light
            for (float x = 125; x < CC.WORLD; x += 250) StreetLamp(s, x * K);

            player = Person("Player", s, new Vector3(3, .2f, 0), 90, Hex("6a4a2a"), Hex("1a1410"));
            PointLight("PlayerFill", player.transform, new Vector3(0, 2.2f, -1.5f), new Color(1, .85f, .65f), 2.2f, 4.5f);
            kessler = Person("KesslerMan", s, new Vector3(0, .2f, .4f), 90, Hex("4a4a52"), Hex("3a3a44"));
            landlady = Person("Landlady", s, new Vector3(CC.STREET[0].x * K + 1.2f, .2f, .9f), 200, Hex("4a3a4a"), null, true, Hex("d9b48f"));
            nellFig = Person("Nell", s, new Vector3(1740 * K - 1.3f, .2f, .8f), 160, Hex("8b2e3a"), Hex("6b1e2a"), true, Hex("e8c9a8"));
            for (int i = 0; i < 9; i++) npcFigs.Add(Person("Npc", s, new Vector3(0, .2f, -1 - (i % 3) * .6f), 90, COATS[i % 5], Hex("1a1410"), i % 4 == 3));
        }

        void StreetHeadframe(Transform s, Vector3 pos, float height, bool flip, Material mat = null)
        {
            mat = mat != null ? mat : art.headframe;
            if (mat == null || mat.mainTexture == null) { Headframe(s, pos, height / 8f, flip); return; }
            var tex = mat.mainTexture;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(s, false);
            q.transform.localScale = new Vector3(height * tex.width / (float)tex.height * (flip ? -1 : 1), height, 1);
            q.transform.localPosition = pos + new Vector3(0, height / 2, -1.2f);
            q.GetComponent<Renderer>().sharedMaterial = mat;
            Tint(q.GetComponent<Renderer>(), new Color(.3f, .26f, .28f));   // silhouettes against the night sky
        }

        void Facade(Transform s, StreetPlace sp)
        {
            float x = sp.x * K, w = sp.w * K * 1.15f, h = sp.h * K * 1.55f;
            var root = new GameObject("Place " + sp.id).transform; root.SetParent(s, false); root.localPosition = new Vector3(x, 0, 3.4f);
            if (sp.style == "stop")
            {
                Cyl("StopPost", root, new Vector3(0, 1.3f, -1.7f), new Vector3(.08f, 1.3f, .08f), art.steel, Hex("2a2a2a"));
                Box("StopSign", root, new Vector3(0, 2.6f, -1.7f), new Vector3(1.4f, .5f, .05f), art.plain, Hex("2a4a3a"));
                Sign("COLUMBIA GARDENS", root, new Vector3(0, 2.6f, -1.75f), .025f, Hex("e8dcc4"));
                Box("Bench", root, new Vector3(0, .5f, -1), new Vector3(1.6f, .08f, .4f), art.timber);
                return;
            }
            var mat = sp.style == "stone" ? art.rock : sp.style == "brick" ? art.brick : art.timber;
            var tint = sp.style == "stone" ? Hex("b0a898") : sp.style == "timber" ? Hex("a08a70") : (Color?)null;
            var whole = new GameObject("Whole").transform; whole.SetParent(root, false);
            Box("Front", whole, new Vector3(0, h / 2, 0), new Vector3(w, h, 3.2f), Tiled(mat, w / 2, h / 2), tint);
            Box("Cornice", whole, new Vector3(0, h + .12f, -1.55f), new Vector3(w + .2f, .25f, .3f), art.timber, Hex("4a3a2a"));
            Box("Door", whole, new Vector3(0, .95f, -1.62f), new Vector3(.9f, 1.9f, .05f), art.timber, Hex("3a2418"));
            int nwin = Mathf.Max(1, Mathf.FloorToInt(w / 1.3f));
            for (int i = 0; i < nwin; i++)
            {
                float wx = -w / 2 + (i + .5f) * w / nwin;
                if (Mathf.Abs(wx) < .7f) continue;
                for (float wy = 1.4f; wy < h - .8f; wy += 2.2f)
                    streetWindows.Add(Box("Window", whole, new Vector3(wx, wy, -1.62f), new Vector3(.55f, .8f, .02f), sp.lights ? art.redGlow : art.window));
                WindowSpill(s, x + wx, 1.4f, sp.lights ? new Color(1, .35f, .3f) : new Color(1, .75f, .45f));
            }
            // a gooseneck lamp over the sign board, and light from the doorway
            float sy = Mathf.Min(h - .5f, 2.6f);
            Box("SignLamp", whole, new Vector3(0, sy + .42f, -1.85f), new Vector3(Mathf.Min(w - .3f, 3.6f), .05f, .05f), art.glow);
            Glow("SignHalo", s, new Vector3(x, sy + .2f, 1.6f), Mathf.Min(w, 4.4f), new Color(1, .8f, .5f, .16f));
            PointLight("DoorLight", whole, new Vector3(0, 1.6f, -2.4f), sp.lights ? new Color(1, .4f, .35f) : new Color(1, .75f, .45f), 1.6f, 4.5f);
            Box("SignBoard", whole, new Vector3(0, Mathf.Min(h - .5f, 2.6f), -1.66f), new Vector3(Mathf.Min(w - .1f, 4.2f), .6f, .04f), art.plain, Hex("2a1f18"));
            var label = new GameObject("Label").AddComponent<PlaceLabel>(); label.transform.SetParent(whole, false);
            label.place = sp; label.text = Sign("", whole, new Vector3(0, Mathf.Min(h - .5f, 2.6f), -1.7f), .04f, Hex("f0cc80"));
            if (sp.spire)
            {
                Box("Tower", whole, new Vector3(0, h + 1.2f, 0), new Vector3(1.1f, 2.4f, 1.1f), Tiled(mat, 1, 1), tint);
                var spire = Cyl("Spire", whole, new Vector3(0, h + 3.4f, 0), new Vector3(.9f, 1.2f, .9f), art.steel, Hex("3a3430"));
                spire.transform.localScale = new Vector3(.7f, 1.3f, .7f);
                Box("CrossV", whole, new Vector3(0, h + 5f, 0), new Vector3(.08f, .6f, .08f), art.plain, Hex("d9a441"));
                Box("CrossH", whole, new Vector3(0, h + 5.1f, 0), new Vector3(.35f, .08f, .08f), art.plain, Hex("d9a441"));
            }
            if (sp.cross)
            {
                Box("RedCrossV", whole, new Vector3(0, h - .3f, -1.63f), new Vector3(.18f, .6f, .02f), art.redGlow);
                Box("RedCrossH", whole, new Vector3(0, h - .3f, -1.63f), new Vector3(.6f, .18f, .02f), art.redGlow);
            }
            if (sp.lights)
                for (int i = 0; i < 6; i++) Ball("RedLamp", whole, new Vector3(-w / 2 + .3f + i * (w - .6f) / 5, h - .1f, -1.7f), Vector3.one * .14f, art.redGlow);
            if (sp.id == "mm" && art.mmSign != null)
            {
                // the M&M's neon blade sign, from a photograph of the real one, hung out over the boardwalk
                var tex = art.mmSign.mainTexture; float sh = 2.6f, sw = sh * tex.width / (float)tex.height;
                var blade = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(blade.GetComponent<Collider>());
                blade.name = "MMBlade"; blade.transform.SetParent(whole, false);
                // a real blade sign sits square to the wall, which the street camera would only see edge-on; swing it toward the street
                const float swing = 38f; float rad = swing * Mathf.Deg2Rad;
                blade.transform.localPosition = new Vector3(-w / 2 + .35f + Mathf.Cos(rad) * sw / 2, h - sh / 2 - .3f, -1.66f - Mathf.Sin(rad) * sw / 2);
                blade.transform.localRotation = Quaternion.Euler(0, swing, 0);
                blade.transform.localScale = new Vector3(sw, sh, 1);
                blade.GetComponent<Renderer>().sharedMaterial = art.mmSign;
                var back = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(back.GetComponent<Collider>());
                back.transform.SetParent(blade.transform, false); back.transform.localRotation = Quaternion.Euler(0, 180, 0);
                back.GetComponent<Renderer>().sharedMaterial = art.mmSign;
                PointLight("NeonGlow", blade.transform, new Vector3(0, 0, -.5f), new Color(1f, .45f, .45f), 1.6f, 4);
            }
            if (sp.id == "union")
            {
                unionWhole = whole;
                var ruin = new GameObject("Ruin").transform; ruin.SetParent(root, false);
                Box("Stump", ruin, new Vector3(-w * .2f, .9f, 0), new Vector3(w * .5f, 1.8f, 3), Tiled(mat, 1, 1), tint, new Vector3(0, 0, 3));
                var rr = CC.Rnd(1914);
                for (int i = 0; i < 14; i++) Box("Rubble", ruin, new Vector3(-w / 2 + rr() * w, .2f + rr() * .3f, -1 + rr() * 2), Vector3.one * (.3f + rr() * .5f), art.rock, tint, new Vector3(rr() * 90, rr() * 90, 0));
                Sign("dynamited June 23, 1914", ruin, new Vector3(w * .15f, 1.1f, -1.7f), .025f, Hex("e8dcc4"));
                unionRuin = ruin;
            }
        }

        // the walking controls under the 3D view: West / East and the "Walk to" chips
        GameObject walkBar; Transform chipRow; readonly List<Button> chipButtons = new List<Button>();
        int heldDir;
        void BuildWalkOverlay()
        {
            var cgo = new GameObject("WalkOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5;
            var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = .5f;
            walkBar = new GameObject("Bar", typeof(RectTransform));
            walkBar.transform.SetParent(cgo.transform, false);
            var rt = (RectTransform)walkBar.transform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(.6f, 0); rt.pivot = new Vector2(0, 0);
            rt.offsetMin = new Vector2(12, 46); rt.offsetMax = new Vector2(-12, 120);
            HoldButton(walkBar.transform, "◀ West", new Vector2(0, 38), new Vector2(110, 34), -1);
            HoldButton(walkBar.transform, "East ▶", new Vector2(118, 38), new Vector2(110, 34), 1);
            var row = new GameObject("Chips", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(walkBar.transform, false);
            var rrt = (RectTransform)row.transform; rrt.anchorMin = new Vector2(0, 0); rrt.anchorMax = new Vector2(1, 0); rrt.pivot = new Vector2(0, 0);
            rrt.offsetMin = new Vector2(0, 0); rrt.offsetMax = new Vector2(0, 32);
            var hl = row.GetComponent<HorizontalLayoutGroup>(); hl.spacing = 4; hl.childForceExpandWidth = false; hl.childControlWidth = true; hl.childControlHeight = true;
            chipRow = row.transform;
            BuildHud(cgo.transform);
            BuildBubble(cgo.transform);
            foreach (var sp in CC.STREET)
            {
                var id = sp.id;
                var b = MakeButton(chipRow, sp.shortName, Vector2.zero, new Vector2(0, 30), 13);
                b.gameObject.AddComponent<LayoutElement>().preferredWidth = 12 + sp.shortName.Length * 8;
                b.onClick.AddListener(() => CC.WalkTo(id));
                b.name = id;
                chipButtons.Add(b);
            }
            walkBar.SetActive(false);
        }

        Button MakeButton(Transform p, string label, Vector2 pos, Vector2 size, int fontSize)
        {
            var go = new GameObject("Btn " + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(p, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(0, 0); rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(.16f, .12f, .09f, .92f);
            var ol = go.AddComponent<Outline>(); ol.effectColor = Hex("c87533"); ol.effectDistance = new Vector2(1, -1);
            var tgo = new GameObject("Text", typeof(RectTransform), typeof(Text)); tgo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)tgo.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var t = tgo.GetComponent<Text>(); t.font = font; t.text = label; t.fontSize = fontSize; t.alignment = TextAnchor.MiddleCenter; t.color = Hex("e8dcc4");
            return go.GetComponent<Button>();
        }

        void HoldButton(Transform p, string label, Vector2 pos, Vector2 size, int dir)
        {
            var b = MakeButton(p, label, pos, size, 16);
            var et = b.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            void Add(UnityEngine.EventSystems.EventTriggerType type, int v)
            {
                var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
                e.callback.AddListener(_ => { heldDir = v; if (CC.scene.walk != null) CC.scene.walk.target = -1; });
                et.triggers.Add(e);
            }
            Add(UnityEngine.EventSystems.EventTriggerType.PointerDown, dir);
            Add(UnityEngine.EventSystems.EventTriggerType.PointerUp, 0);
            Add(UnityEngine.EventSystems.EventTriggerType.PointerExit, 0);
        }

        // ---------------- INTERIORS ----------------
        readonly Dictionary<string, Light> roomLamps = new Dictionary<string, Light>();
        readonly Dictionary<string, List<Transform>> roomHeads = new Dictionary<string, List<Transform>>();
        void BuildInteriors()
        {
            foreach (var kv in CC.INTERIOR)
            {
                if (kv.Key == "dry") continue;
                var r = new GameObject("Inside " + kv.Key).transform; r.SetParent(insideRoot, false); r.gameObject.SetActive(false);
                interiors[kv.Key] = r;
                BuildInterior(r, kv.Key, kv.Value);
                var heads = new List<Transform>();
                foreach (var t in r.GetComponentsInChildren<Transform>(true)) if (t.name == "HeadPivot") heads.Add(t);
                roomHeads[kv.Key] = heads;
                RegisterSpeakers(kv.Key, r);
            }
        }

        void BuildInterior(Transform r, string id, string kind)
        {
            float W = 10, D = 7, H = 3.8f;
            var wall = kind == "hall" || kind == "counter" || kind == "ward" ? art.brick : kind == "church" ? art.rock : art.timber;
            Room(r, wall, W, D, H);
            Color lampCol = new Color(1, .75f, .45f);
            switch (kind)
            {
                case "bar":
                    {
                        bool casino = id == "mercury";
                        Box("Bar", r, new Vector3(0, .55f, 1.6f), new Vector3(7, 1.1f, .7f), Tiled(art.timber, 3, 1), Hex("5a3a24"));
                        Box("BarTop", r, new Vector3(0, 1.12f, 1.55f), new Vector3(7.2f, .06f, .85f), art.timber, Hex("3a2418"));
                        Box("BackBar", r, new Vector3(0, 1.6f, 3.3f), new Vector3(6, 2.2f, .4f), Tiled(art.timber, 3, 1), Hex("4a2e1c"));
                        Box("Mirror", r, new Vector3(0, 1.9f, 3.08f), new Vector3(4.5f, 1.1f, .03f), art.steel, Hex("9aa4aa"));
                        for (int i = 0; i < 16; i++) Cyl("Bottle", r, new Vector3(-2.6f + i * .35f, 1.2f + (i % 2) * .02f, 3.0f), new Vector3(.08f, .14f, .08f), art.glow, Color.HSVToRGB((i * .13f) % 1, .5f, .5f));
                        for (int i = 0; i < 5; i++) Cyl("Stool", r, new Vector3(-2.4f + i * 1.2f, .35f, .9f), new Vector3(.35f, .35f, .35f), art.timber);
                        Person(casino ? "Piano player" : "Teddy", r, new Vector3(casino ? 3.5f : .3f, 0, casino ? 2.3f : 2.4f), 180, casino ? Hex("2a2a3a") : Hex("f0e6d0"), null, false, Hex("d9b48f"), !casino);
                        Person("Drinker", r, new Vector3(-1.8f, 0, .7f), 15, COATS[1], Hex("1a1410"));
                        Person("Drinker", r, new Vector3(1.6f, 0, .6f), -20, COATS[2], Hex("1a1410"));
                        if (casino)
                        {
                            Person("Nell", r, new Vector3(-.4f, 0, -.6f), 160, Hex("8b2e3a"), Hex("6b1e2a"), true, Hex("e8c9a8"));
                            Person("Dancer", r, new Vector3(-3, 0, 0), 150, Hex("6a2a5a"), null, true, Hex("e8c9a8"));
                            Box("Piano", r, new Vector3(3.6f, .6f, 2.9f), new Vector3(1.5f, 1.2f, .6f), art.timber, Hex("1a1210"));
                            lampCol = new Color(1, .3f, .3f);
                        }
                        else
                        {
                            Box("FaroTable", r, new Vector3(-3.3f, .75f, -.6f), new Vector3(1.4f, .08f, 1), art.cloth, Hex("1f4a2a"));
                            Person("Faro dealer", r, new Vector3(-3.3f, 0, .1f), 180, COATS[3], Hex("1a1410"));
                        }
                        break;
                    }
                case "hall":
                    for (int row = 0; row < 4; row++)
                        Box("Bench", r, new Vector3(0, .45f, -1.8f + row * 1.0f), new Vector3(6.5f, .1f, .4f), art.timber);
                    for (int i = 0; i < 8; i++) Person("Member", r, new Vector3(-2.8f + (i % 4) * 1.8f, -.35f, -1.5f + (i / 4) * 2f), 180, COATS[i % 6], i % 2 == 0 ? Hex("1a1410") : (Color?)null);
                    Box("Podium", r, new Vector3(0, .6f, 2.6f), new Vector3(1, 1.2f, .6f), art.timber, Hex("5a3a24"));
                    Person("Speaker", r, new Vector3(0, 0, 3.0f), 180, Hex("3a3a4a"), null, false, Hex("d9b48f"));
                    Box("Banner", r, new Vector3(0, 2.8f, 3.43f), new Vector3(5.5f, .8f, .03f), art.cloth, Hex("8a1e1e"));
                    Sign("BUTTE MINERS' UNION · No. 1", r, new Vector3(0, 2.8f, 3.4f), .035f, Hex("f0e0c0"));
                    break;
                case "church":
                    for (int row = 0; row < 4; row++)
                    {
                        Box("PewL", r, new Vector3(-2.2f, .45f, -2 + row * 1.1f), new Vector3(3, .9f, .3f), art.timber, Hex("4a2e1c"));
                        Box("PewR", r, new Vector3(2.2f, .45f, -2 + row * 1.1f), new Vector3(3, .9f, .3f), art.timber, Hex("4a2e1c"));
                    }
                    Box("Altar", r, new Vector3(0, .55f, 2.9f), new Vector3(2.2f, 1.1f, .8f), art.plain, Hex("e8e0d0"));
                    for (int i = 0; i < 3; i++)
                    {
                        Box("Glass", r, new Vector3(-2.5f + i * 2.5f, 2.4f, 3.44f), new Vector3(.8f, 1.6f, .02f), art.window, Color.HSVToRGB(.6f - i * .2f, .6f, 1));
                    }
                    for (int i = 0; i < 6; i++) Cyl("Candle", r, new Vector3(-.8f + i * .32f, 1.2f, 2.8f), new Vector3(.05f, .12f, .05f), art.glow);
                    Person("Father Brosnan", r, new Vector3(0, 0, 2.2f), 180, Hex("1a1a1e"), null, false, Hex("d9b48f"));
                    Person("Woman praying", r, new Vector3(-2.4f, -.3f, -.9f), 0, Hex("3a2a3a"), Hex("2a1a2a"), true);
                    lampCol = new Color(1, .82f, .6f);
                    break;
                case "shelves":
                    for (int s = 0; s < 3; s++)
                    {
                        float x = -3.5f + s * 3.5f;
                        Box("Shelf", r, new Vector3(x, 1.4f, 3.2f), new Vector3(2.6f, 2.8f, .4f), art.timber, Hex("4a2e1c"));
                        for (int b = 0; b < 30; b++) Box("Book", r, new Vector3(x - 1.15f + (b % 10) * .24f, .6f + (b / 10) * .8f, 3.0f), new Vector3(.14f, .5f, .3f), art.cloth, Color.HSVToRGB((b * .17f + s * .3f) % 1, .45f, .45f));
                    }
                    Box("Table", r, new Vector3(0, .75f, 0), new Vector3(3, .08f, 1.2f), art.timber, Hex("5a3a24"));
                    Box("GreenLamp", r, new Vector3(-.6f, .95f, 0), new Vector3(.3f, .12f, .2f), art.greenGlow);
                    Box("GreenLamp", r, new Vector3(.8f, .95f, 0), new Vector3(.3f, .12f, .2f), art.greenGlow);
                    Person("Miss Haskell", r, new Vector3(2.6f, 0, 1.4f), 200, Hex("4a4a5a"), null, true, Hex("e0c4a8"));
                    Person("Reader", r, new Vector3(-1.2f, -.35f, .9f), 180, COATS[4], null);
                    lampCol = new Color(.75f, 1f, .7f);
                    break;
                case "counter":
                    Box("Counter", r, new Vector3(0, .55f, 1.5f), new Vector3(6, 1.1f, .7f), Tiled(art.timber, 3, 1), Hex("6a4a2c"));
                    Cyl("Urn", r, new Vector3(-1.8f, 1.5f, 1.6f), new Vector3(.45f, .4f, .45f), art.steel, Hex("b08040"));
                    Box("MenuBoard", r, new Vector3(1.5f, 2.6f, 3.43f), new Vector3(2.4f, 1, .03f), art.plain, Hex("1a1a1a"));
                    Sign("PASTIES 15¢\nCOFFEE 5¢", r, new Vector3(1.5f, 2.6f, 3.4f), .04f, Hex("f0f0f0"));
                    Person("Counter woman", r, new Vector3(0, 0, 2.3f), 180, Hex("5a4a3a"), null, true, Hex("d9b48f"), true);
                    Person("Night shift", r, new Vector3(-1, 0, .6f), 20, COATS[0], Hex("1a1410"));
                    Person("Wife", r, new Vector3(1.4f, 0, .5f), -15, Hex("3a2a3a"), Hex("2a1a2a"), true);
                    break;
                case "store":
                    for (int s = 0; s < 2; s++) Box("Shelves", r, new Vector3(-2.5f + s * 5, 1.5f, 3.2f), new Vector3(3.6f, 3, .4f), art.timber, Hex("4a2e1c"));
                    for (int b = 0; b < 24; b++) Box("Goods", r, new Vector3(-4 + (b % 12) * .65f, .6f + (b / 12) * 1.1f, 3.0f), new Vector3(.4f, .45f, .3f), art.cloth, Color.HSVToRGB((b * .11f) % 1, .3f, .5f));
                    Box("Counter", r, new Vector3(-1, .55f, 1.2f), new Vector3(4, 1.1f, .6f), art.timber, Hex("6a4a2c"));
                    for (int i = 0; i < 8; i++) Box("Grille", r, new Vector3(2.3f + i * .15f, 1.7f, 1.2f), new Vector3(.03f, 1.2f, .03f), art.steel, Hex("b08040"));
                    Cyl("Scale", r, new Vector3(-2.2f, 1.2f, 1.2f), new Vector3(.4f, .05f, .4f), art.steel, Hex("b08040"));
                    Person("Clerk", r, new Vector3(-1, 0, 1.9f), 180, Hex("2a2a32"), null, false, Hex("d9c4a8"));
                    break;
                case "steam":
                    for (int t = 0; t < 3; t++) Box("Tier", r, new Vector3(0, .3f + t * .45f, 1.6f + t * .55f), new Vector3(7, .1f, .5f), art.timber, Hex("8a6a4a"));
                    Box("Stove", r, new Vector3(-3.6f, .5f, -.5f), new Vector3(1, 1, 1), art.rock, Hex("3a3a3a"));
                    for (int i = 0; i < 6; i++) Ball("Stone", r, new Vector3(-3.8f + (i % 3) * .25f, 1.05f, -.6f + (i / 3) * .25f), Vector3.one * .22f, art.glow, new Color(1, .5f, .2f));
                    Person("Finn", r, new Vector3(-1.5f, .25f, 2.1f), 180, Hex("d9b48f"), null, false, Hex("d9b48f"));
                    Person("Finn", r, new Vector3(1.2f, .7f, 2.6f), 180, Hex("c9a07a"), null, false, Hex("c9a07a"));
                    lampCol = new Color(1, .55f, .3f);
                    break;
                case "ward":
                    for (int i = 0; i < 4; i++)
                    {
                        Box("Bed", r, new Vector3(-3.3f + i * 2.2f, .5f, 2), new Vector3(1, .5f, 2), art.plain, Hex("e8e8ec"));
                        Box("Headboard", r, new Vector3(-3.3f + i * 2.2f, .9f, 3), new Vector3(1, 1, .06f), art.steel, Hex("d0d0d4"));
                        if (i != 2) Ball("Patient", r, new Vector3(-3.3f + i * 2.2f, .85f, 2.7f), new Vector3(.28f, .25f, .28f), art.cloth, Hex("d9b48f"));
                    }
                    Person("Sister Mary Agnes", r, new Vector3(.4f, 0, .4f), 200, Hex("e8e8ec"), Hex("1a1a1e"), true, Hex("e8c9a8"));
                    lampCol = new Color(.9f, .92f, 1f);
                    break;
                case "room":
                    Box("Table", r, new Vector3(0, .75f, .6f), new Vector3(2, .08f, 1.2f), art.timber, Hex("6a4a2c"));
                    Box("Stove", r, new Vector3(3.5f, .6f, 2.4f), new Vector3(.8f, 1.2f, .8f), art.steel, Hex("1e1a18"));
                    Box("StoveGlow", r, new Vector3(3.5f, .5f, 1.99f), new Vector3(.4f, .2f, .02f), art.glow);
                    Box("Bed", r, new Vector3(-3.4f, .4f, 2.4f), new Vector3(2, .5f, 1), art.cloth, Hex("6a5a4a"));
                    Person("Landlady", r, new Vector3(.8f, 0, 1.6f), 190, Hex("4a3a4a"), null, true, Hex("d9b48f"), true);
                    Cyl("Coffee", r, new Vector3(-.3f, .86f, .6f), new Vector3(.12f, .08f, .12f), art.steel, Hex("2a2a2a"));
                    break;
            }
            DressRoom(r, id, kind, W, D, H, lampCol);
            DressKind(r, id, kind, W, D, H);
            var lamp = Ball("Lamp", r, new Vector3(0, H - .45f, .5f), new Vector3(.3f, .3f, .3f), art.glow);
            roomLamps[id] = PointLight("RoomLamp", lamp.transform, Vector3.zero, lampCol, 5.5f, 12, true);
        }

        // ---------------- particles: falling dust, drill dust, steam, snow ----------------
        void BuildParticles()
        {
            dustFall = MakeParticles("DustFall", underRoot, new Color(.6f, .55f, .5f, .7f), .04f, false);
            var sh = dustFall.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(3.6f, .1f, 5);
            dustFall.transform.localPosition = new Vector3(0, 2.9f, 4);
            var main = dustFall.main; main.startSpeed = .2f; main.gravityModifier = .35f; main.startLifetime = 2.5f;
            drillDust = MakeParticles("DrillDust", underRoot, new Color(.75f, .7f, .65f, .35f), .25f, false);
            drillDust.transform.localPosition = new Vector3(.3f, 1.35f, 6.5f);
            var m2 = drillDust.main; m2.startSpeed = .6f; m2.startLifetime = 2.5f;
            sparks = MakeParticles("Sparks", underRoot, new Color(1, .82f, .45f, 1), .035f, true);
            var msp = sparks.main; msp.startSpeed = 2.4f; msp.startLifetime = .35f; msp.gravityModifier = 1.2f;
            var shs = sparks.shape; shs.shapeType = ParticleSystemShapeType.Sphere; shs.radius = .03f;
            steam = MakeParticles("Steam", underRoot, new Color(1, .95f, .9f, .12f), .9f, false);
            steam.transform.localPosition = new Vector3(0, .5f, 3);
            var m3 = steam.main; m3.startSpeed = .3f; m3.startLifetime = 4; m3.gravityModifier = -.05f;
            snow = MakeParticles("Snow", transform, new Color(1, 1, 1, .85f), .06f, true);
            var m4 = snow.main; m4.startSpeed = 0; m4.gravityModifier = .06f; m4.startLifetime = 9; m4.maxParticles = 1500;
            var sh4 = snow.shape; sh4.shapeType = ParticleSystemShapeType.Box; sh4.scale = new Vector3(60, 1, 50);
            var em4 = snow.emission; em4.rateOverTime = 120;
        }

        ParticleSystem MakeParticles(string n, Transform p, Color c, float size, bool world)
        {
            var go = new GameObject(n); go.transform.SetParent(p, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startColor = c; main.startSize = size; main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local; main.playOnAwake = false; main.maxParticles = 600;
            var em = ps.emission; em.rateOverTime = 0;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = art.particle;
            return ps;
        }

        static void Emit(ParticleSystem ps, float rate)
        {
            var em = ps.emission; em.rateOverTime = rate;
            if (rate > 0 && !ps.isPlaying) ps.Play();
            if (rate <= 0 && ps.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        // ================= per-frame =================
        string lastMode, lastInside;
        Vector3 camPos, camLook; bool camSnap = true;
        float walkPhase, walkAcc, shake;

        void Update()
        {
            var sc = CC.scene;
            string mode = sc.mode ?? "photo";
            bool panel = GameUI.Instance == null || GameUI.Instance.PanelVisible;
            cam.rect = panel ? new Rect(0, 0, .6f, 1) : new Rect(0, 0, 1, 1);

            if (mode != lastMode || sc.inside != lastInside) { camSnap = true; lastMode = mode; lastInside = sc.inside; }
            photoRoot.gameObject.SetActive(mode == "photo");
            hillRoot.gameObject.SetActive(mode == "hill");
            underRoot.gameObject.SetActive(mode == "under");
            dryRoot.gameObject.SetActive(mode == "dry");
            streetRoot.gameObject.SetActive(mode == "walk");
            insideRoot.gameObject.SetActive(mode == "inside");
            foreach (var kv in interiors) kv.Value.gameObject.SetActive(mode == "inside" && kv.Key == sc.inside);
            if (walkBar != null) walkBar.SetActive(mode == "walk");

            bool winter = sc.season == 3;
            Emit(snow, winter && (mode == "hill" || mode == "walk") ? 120 : 0);
            snow.transform.position = cam.transform.position + new Vector3(0, 12, 14);

            UpdateHud(sc.under, mode == "under");
            UpdateBubble(sc);
            switch (mode)
            {
                case "photo": UpdatePhoto(); break;
                case "hill": UpdateHill(sc); break;
                case "under": UpdateUnder(sc.under); break;
                case "dry": UpdateDry(); break;
                case "walk": UpdateWalk(sc); break;
                case "inside": UpdateInside(sc); break;
            }

            float k = camSnap ? 1 : 1 - Mathf.Exp(-Time.deltaTime * 3f);
            camSnap = false;
            var curPos = Vector3.Lerp(cam.transform.position, camPos, k);
            var curLook = Vector3.Lerp(cam.transform.position + cam.transform.forward * 10, camLook, k);
            if (k >= 1) curLook = camLook;
            cam.transform.position = curPos + (shake > 0 ? Random.insideUnitSphere * shake * .06f : Vector3.zero);
            cam.transform.LookAt(curLook);
        }

        void Pose(Vector3 pos, Vector3 look) { camPos = pos; camLook = look; }

        void SetEnv(Color sky, Color ambient, float fog, Color fogCol, bool sunOn, Color sunCol, float sunI)
        {
            cam.backgroundColor = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = fog > 0; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = fog; RenderSettings.fogColor = fogCol;
            sun.enabled = sunOn; sun.color = sunCol; sun.intensity = sunI;
        }

        // smelter smoke hung over the hill: hazy sepia skies, never clean blue
        static readonly Color[] SKY = { new Color(.62f, .57f, .5f), new Color(.68f, .6f, .5f), new Color(.62f, .5f, .38f), new Color(.66f, .64f, .6f) };

        void UpdatePhoto()
        {
            SetEnv(Color.black, Color.white, 0, Color.black, false, Color.white, 0);
            camSnap = true;
            Pose(new Vector3(0, 0, -10), Vector3.zero);
            // fill the view with the photo, with a slow pan
            float aspect = cam.aspect, h = 2 * 10 * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad), w = h * aspect;
            var tex = art.hero; float ta = tex != null ? tex.width / (float)tex.height : 1.6f;
            float qh = Mathf.Max(h, w / ta) * 1.08f;
            photoQuad.localScale = new Vector3(qh * ta, qh, 1);
            photoQuad.localPosition = new Vector3(Mathf.Sin(Time.time * .18f) * (qh * ta - w) * .45f, 0, 0);
        }

        void UpdateHill(SceneState sc)
        {
            bool dark = sc.dark;
            Color sky = dark ? new Color(.09f, .07f, .055f) : SKY[sc.season];
            SetEnv(sky, dark ? new Color(.24f, .2f, .17f) : new Color(.52f, .48f, .42f), dark ? .014f : .011f, sky, true,
                dark ? new Color(1f, .82f, .6f) : (sc.season == 2 ? new Color(1, .8f, .6f) : new Color(1, .92f, .78f)), dark ? .45f : 1.25f);
            foreach (var b in billboards)
            {
                var d = cam.transform.position - b.position; d.y = 0;
                if (d.sqrMagnitude > .01f) b.rotation = Quaternion.LookRotation(-d.normalized);
            }
            foreach (var smk in stackSmoke) Emit(smk, 3);
            sun.transform.rotation = Quaternion.Euler(dark ? 50 : 30, -35, 0);
            Tint(terrain, sc.season == 3 ? new Color(.92f, .9f, .9f) : new Color(.6f, .55f, .5f));
            bigM.gameObject.SetActive(sc.year >= 1910);
            foreach (var wgo in townWindows) wgo.SetActive(dark);
            fireLight.intensity = sc.fire ? 6 + Mathf.Sin(Time.time * 7) * 1.5f : 0;
            // an elevated view from the Flat; a focus swings the view toward that place without diving into the slope
            // the browser game shows the whole hill and rings the place in gold; here the view stays wide and leans toward it
            var target = sc.focus != null ? LocPos(sc.focus) : new Vector3(-2, 8, 10);
            var from = sc.focus != null ? new Vector3(target.x * .55f - 3, 26, -60) : new Vector3(-4, 24, -66);
            from.y = Mathf.Max(from.y, Height(from.x, from.z) + 8);
            Pose(from, Vector3.Lerp(new Vector3(-2, 8, 10), target, .6f) + Vector3.up * 3f);
            UpdateHillExtras(sc, target);
        }

        void UpdateDry()
        {
            SetEnv(new Color(.03f, .025f, .02f), new Color(.22f, .18f, .14f), .02f, new Color(.05f, .04f, .03f), false, Color.white, 0);
            dryGreen.SetActive(CC.S == null || CC.S.originId != "chinese");
            Idle(dryBoss, Time.time); Idle(dryPartner, Time.time + 1.3f); Idle(dryGreen, Time.time + 2.9f);
            Pose(new Vector3(0, 1.65f, -2.9f), new Vector3(0, 1.3f, 1.5f));
        }

        void UpdateInside(SceneState sc)
        {
            SetEnv(new Color(.03f, .025f, .02f), new Color(.3f, .26f, .21f), .012f, new Color(.05f, .04f, .03f), false, Color.white, 0);
            if (sc.inside != null && roomLamps.TryGetValue(sc.inside, out var l)) l.intensity = 6.5f * (.95f + .05f * Mathf.Sin(Time.time * 9));
            foreach (var kv in roomHaze) Emit(kv.Value, kv.Key == sc.inside ? (kv.Key == "sauna" ? 14 : 4) : 0);
            FaceHalos();
            if (sc.inside != null && roomHeads.TryGetValue(sc.inside, out var heads))
                for (int i = 0; i < heads.Count; i++)
                    heads[i].localRotation = heads[i] == speakingHead
                        ? Quaternion.Euler(Mathf.Sin(Time.time * 5f) * 5, 0, Mathf.Sin(Time.time * 2.3f) * 3)     // talking: facing you, nodding
                        : Quaternion.Euler(Mathf.Sin(Time.time * .6f + i) * 3, Mathf.Sin(Time.time * .27f + i * 1.7f) * 16, 0);
            Pose(new Vector3(0, 1.75f, -3.6f), new Vector3(0, 1.3f, 2));
        }

        void UpdateWalk(SceneState sc)
        {
            var Wk = sc.walk; if (Wk == null) return;
            // input: keyboard or the on-screen buttons, ticked at 60 Hz
            int keyDx = 0; bool enter = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && !GameUI.TypingInField)
            {
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) keyDx = -1;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) keyDx = 1;
                enter = kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
            }
#else
            if (!GameUI.TypingInField)
            {
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) keyDx = -1;
                if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) keyDx = 1;
                enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
            }
#endif
            if (keyDx != 0) Wk.target = -1;
            Wk.btn = heldDir;
            walkAcc += Time.unscaledDeltaTime;
            int n = 0;
            while (walkAcc >= 1 / 60f && n++ < 4) { walkAcc -= 1 / 60f; CC.WalkStep(keyDx); if (CC.scene.mode != "walk") return; }
            if (enter && Wk.at != null)
            {
                var sp = CC.FindPlace(Wk.at);
                if (sp != null && !sp.IsRuin && System.Array.Exists(sp.acts, a => CC.LeisureFor(a) != null)) { CC.Sfx("door"); CC.EnterPlace(sp); return; }
            }
            if (GameUI.Instance != null) GameUI.Instance.Walking = Wk.moving;

            bool dark = true;
            SetEnv(Hex("2a1c24"), new Color(.3f, .26f, .27f), .007f, Hex("2a1c24"), true, new Color(.75f, .7f, .9f), .28f);
            sun.transform.rotation = Quaternion.Euler(40, 160, 0);
            foreach (var wgo in streetWindows) wgo.SetActive(dark);
            if (unionWhole != null) { var u = CC.FindPlace("union"); bool ruin = CC.S != null && u.IsRuin; unionWhole.gameObject.SetActive(!ruin); unionRuin.gameObject.SetActive(ruin); }

            float px = Wk.px * K;
            walkPhase += Wk.moving ? Time.deltaTime * 9 : 0;
            player.transform.localPosition = new Vector3(px, .2f, 0);
            player.transform.localRotation = Quaternion.Euler(0, Wk.dir > 0 ? 90 : -90, 0);
            AnimateWalk(player, walkPhase, Wk.moving);
            bool tail = CC.KesslerTail;
            kessler.SetActive(tail);
            if (tail)
            {
                float kx = Mathf.Max(.75f, px - Wk.dir * 120 * K);
                var kp = kessler.transform.localPosition; kp.x = Mathf.Lerp(kp.x, kx, Time.deltaTime * 2); kessler.transform.localPosition = new Vector3(kp.x, .2f, .5f);
                kessler.transform.localRotation = Quaternion.Euler(0, px > kp.x ? 90 : -90, 0);
                AnimateWalk(kessler, walkPhase, Wk.moving);
            }
            nellFig.SetActive(CC.S != null && CC.S.rel.TryGetValue("nell", out var nr) && nr > 0);
            for (int i = 0; i < npcFigs.Count && i < Wk.npcs.Count; i++)
            {
                var npc = Wk.npcs[i]; var f = npcFigs[i];
                f.transform.localPosition = new Vector3(npc.x * K, .2f, -1.2f - npc.lane * .1f);
                f.transform.localRotation = Quaternion.Euler(0, npc.v > 0 ? 90 : -90, 0);
                AnimateWalk(f, Time.time * 7 * npc.speed + npc.ph, true);
            }
            foreach (var b in chipButtons)
            {
                var sp = CC.FindPlace(b.name);
                bool on = sp != null && System.Array.Exists(sp.acts, a => CC.LeisureFor(a) != null);
                b.gameObject.SetActive(on);
                b.GetComponent<Image>().color = Wk.at == b.name ? new Color(.45f, .27f, .12f, .95f) : new Color(.16f, .12f, .09f, .92f);
            }
            float camX = Mathf.Clamp(px, 4, CC.WORLD * K - 4);
            Pose(new Vector3(camX, 2.3f, -8.2f), new Vector3(camX, 2.0f, 2));
            UpdateStreetNight(camX, CC.WORLD * K);
            Idle(landlady, Time.time); Idle(nellFig, Time.time + 2);
        }

        void UpdateUnder(ShiftState U)
        {
            if (U == null) return;
            string k = U.kind;
            muckProps.gameObject.SetActive(k == "muck" || k == "hotmuck");
            timberProps.gameObject.SetActive(k == "timber");
            drillProps.gameObject.SetActive(k == "drill");
            tramProps.gameObject.SetActive(k == "tram");
            faceWall.gameObject.SetActive(k == "tram");

            // darker as the dust builds; red and hot at the Leonard
            float dim = Mathf.Min(.55f, U.dust / 1800f);
            bool hot = k == "hotmuck";
            SetEnv(new Color(.02f, .015f, .012f), Color.Lerp(new Color(.22f, .18f, .145f), new Color(.08f, .06f, .05f), dim / .55f),
                .045f + dim * .05f, new Color(.07f, .05f, .035f), false, Color.white, 0);
            if (hot) RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, new Color(.35f, .12f, .06f), U.heat * .7f);
            heatLight.intensity = hot ? 2 + U.heat * 7 : 0;
            Emit(steam, hot ? 6 + U.heat * 20 : 0);
            if (hot && lastIce >= 0 && U.ice < lastIce) { steam.Emit(80); shake = .5f; }   // the ice hits the hot rock
            lastIce = hot ? U.ice : -1;
            float lampBase = k == "drill" ? 5f : k == "tram" ? 20f : 26f;
            headlamp.intensity = lampBase * (1 - dim * .7f) * (.94f + .06f * Mathf.PerlinNoise(Time.time * 3, .5f));
            float sparkRate = 0;
            FaceHalos();
            shake = U.warning ? .8f : Mathf.Max(0, shake - Time.deltaTime * 2);
            Emit(dustFall, U.warning ? 70 : 0);

            Vector3 eye = new Vector3(0, 1.55f, -.6f), look = new Vector3(0, 1.35f, 6);
            switch (k)
            {
                case "muck":
                case "hotmuck":
                    {
                        eye = new Vector3(-.05f, 1.62f, -.6f) + (U.rest ? new Vector3(0, .12f, 0) : Vector3.zero);
                        look = new Vector3(-.1f, .7f, 3.4f);
                        float left = Mathf.Clamp01(1 - U.prog * .5f);
                        pile.localScale = new Vector3(1, .55f + .45f * left, 1);
                        float fill = Mathf.Clamp01(U.loads / (float)Mathf.Max(1, U.target));
                        carLoad.localScale = new Vector3(.9f, .05f + .3f * fill, 1.45f);
                        carLoad.localPosition = new Vector3(0, .2f + .15f * fill, 0);
                        bool throwing = U.ph == "throw";
                        // where the shovel should be for this phase (local to the drift)
                        // the shovel is held in front of you: offsets are in view space (x right, y up, z forward)
                        Vector3 sp; Vector3 se; bool loaded = false;
                        switch (U.ph)
                        {
                            case "dig":
                                {
                                    float t = Mathf.Sin(Mathf.Clamp01(U.pt / 12f) * Mathf.PI * .5f);
                                    sp = Vector3.Lerp(new Vector3(.24f, -.32f, .62f), new Vector3(-.28f, -.78f, .95f), t);
                                    se = new Vector3(Mathf.Lerp(28, 18, t), Mathf.Lerp(-8, -22, t), 0); break;
                                }
                            case "swing":
                                sp = new Vector3(Mathf.Lerp(-.3f, .35f, U.m), -.42f + Mathf.Sin(U.m * Mathf.PI) * .08f, .3f);
                                se = new Vector3(12, Mathf.Lerp(-22, 22, U.m), 0); loaded = true; break;
                            case "throw":
                                {
                                    float t = U.pt / 8f;
                                    sp = Vector3.Lerp(new Vector3(.25f, -.4f, .3f), new Vector3(.45f, -.3f, .7f), t);
                                    se = new Vector3(Mathf.Lerp(12, -15, t), Mathf.Lerp(22, 35, t), 0); break;
                                }
                            default:
                                // ready: the shovel held low in front of you, blade toward the pile
                                sp = U.rest ? new Vector3(.32f, -.9f, .55f) : new Vector3(.24f, -.42f, .42f) + new Vector3(0, Mathf.Sin(Time.time * 1.6f) * .01f, 0);
                                se = U.rest ? new Vector3(60, -10, 0) : new Vector3(-4, -16, 0); break;
                        }
                        var view = Quaternion.LookRotation(look - eye);
                        float k2 = 1 - Mathf.Exp(-Time.deltaTime * 16);
                        shovel.position = Vector3.Lerp(shovel.position, eye + view * sp, k2);
                        shovel.rotation = Quaternion.Slerp(shovel.rotation, view * Quaternion.Euler(se), k2);
                        shovelLoad.gameObject.SetActive(loaded);
                        thrown.gameObject.SetActive(throwing && U.res != "miss");
                        if (throwing)
                        {
                            float t = U.pt / 8f;
                            var a = shovel.position + shovel.forward * 1.3f; var b = car.position + Vector3.up * .55f;
                            var p = Vector3.Lerp(a, b, t); p.y += Mathf.Sin(t * Mathf.PI) * .6f;
                            thrown.position = p;
                        }
                        break;
                    }
                case "timber":
                    {
                        float ang = U.ph == "swing" ? U.x * 24 : 0;
                        swingTimber.localRotation = Quaternion.Euler(0, 0, ang);
                        swingTimber.localPosition = new Vector3(0, U.ph == "hoist" ? 2.85f + U.hoist / 28f * .8f : 2.85f, 4.2f);
                        for (int i = 0; i < placedSets.Count; i++) placedSets[i].SetActive(i < U.sets);
                        eye = new Vector3(-.2f, 1.55f, -1.8f); look = new Vector3(0, 1.6f, 4.5f);
                        {
                            // the single-jack in your right hand: raised and brought down while the wedges go in
                            var view = Quaternion.LookRotation(look - eye);
                            bool hammering = U.ph == "hammer";
                            float sw = hammering ? Mathf.Abs(Mathf.Sin(U.hammer * .4f)) : 0;
                            var mp = hammering ? new Vector3(.3f, -.22f + sw * .16f, .5f) : new Vector3(.34f, -.34f, .62f);
                            var me = hammering ? new Vector3(-75 + sw * 95, -12, 0) : new Vector3(18, -20, 0);
                            float k3 = 1 - Mathf.Exp(-Time.deltaTime * 18);
                            maul.position = Vector3.Lerp(maul.position, eye + view * mp, k3);
                            maul.rotation = Quaternion.Slerp(maul.rotation, view * Quaternion.Euler(me), k3);
                            maul.localScale = Vector3.one * .6f;
                            if (hammering && U.hammer % 6 == 0) { sparks.transform.position = eye + view * new Vector3(.1f, .1f, 1.2f); sparks.Emit(6); }
                        }
                        break;
                    }
                case "drill":
                    {
                        bool on = U.In("main") && U.stuck == 0;
                        float pr = Mathf.Clamp01(U.prog) * 8;
                        int holes = Mathf.Min(8, Mathf.FloorToInt(pr));
                        float frac = holes >= 8 ? 1 : pr - holes;
                        for (int i = 0; i < drillHoles.Count; i++) { drillHoles[i].SetActive(i < holes); chalkMarks[i].SetActive(i > holes || (i == holes && frac < .05f)); }
                        // the machine sits at the hole it is boring and feeds forward as the hole deepens
                        var hp = HolePos(Mathf.Min(holes, 7));
                        var want = hp + new Vector3(0, 0, -.97f + frac * .32f);
                        var cur = Vector3.Lerp(drillRig.localPosition, want, 1 - Mathf.Exp(-Time.deltaTime * 4));
                        drillRig.localPosition = cur + (on ? Random.insideUnitSphere * (.005f + U.jam * .014f) : Vector3.zero);
                        drillSteel.GetComponent<Renderer>().material.SetColor("_BaseColor", Color.Lerp(new Color(.6f, .6f, .62f), new Color(1, .35f, .1f), U.jam));
                        var col = drillProps.TransformPoint(new Vector3(1.45f, cur.y, 5.7f));
                        Span(drillArm, col, drillRig.TransformPoint(new Vector3(.12f, 0, -.05f)), .07f);
                        Span(drillHose, drillRig.TransformPoint(new Vector3(0, -.05f, -.42f)), drillProps.TransformPoint(new Vector3(-.4f, .05f, 3.8f)), .06f);
                        var bit = drillRig.TransformPoint(new Vector3(0, 0, .96f));
                        drillDust.transform.position = bit + new Vector3(0, 0, -.05f);
                        Emit(drillDust, on ? 45 : 0);
                        sparks.transform.position = bit; sparkRate = on ? 30 + U.jam * 60 : 0;
                        // you stand behind the machine, both hands on the handles
                        // a three-quarter view from above and to the left, so the machine, the steel and the face all show
                        var rp = drillRig.position;
                        eye = new Vector3(Mathf.Clamp(rp.x - 1.0f, -1.7f, 1f), 2.0f, rp.z - .95f);
                        look = rp + new Vector3(.1f, -.1f, .75f);
                        var view = Quaternion.LookRotation(look - eye);
                        for (int s2 = 0; s2 < 2; s2++)
                        {
                            float side = s2 == 0 ? -1 : 1;
                            var shoulder = eye + view * new Vector3(side * .2f, -.4f, .15f);
                            var grip = drillRig.TransformPoint(new Vector3(side * .24f, -.02f, -.47f));
                            bool reach = (grip - shoulder).magnitude < 1.1f;   // only draw your arms when the handles are within reach of the view
                            drillArms[s2].gameObject.SetActive(reach); drillHands[s2].gameObject.SetActive(reach);
                            if (reach) { Span(drillArms[s2], shoulder, grip, .1f); drillHands[s2].position = grip; }
                        }
                        break;
                    }
                case "tram":
                    {
                        float z = 2.5f + U.pos * (DRIFT - 10);
                        tramCar.localPosition = new Vector3(1, .78f, z);
                        for (int i = 0; i < slowSigns.Count; i++)
                        {
                            float ahead = U.zones[i] - U.pos;
                            slowSigns[i].color = ahead > 0 && ahead < .22f ? Hex("c0201a") : Hex("6a4a40");
                        }
                        eye = new Vector3(.5f, 1.8f, z - 2.4f); look = new Vector3(.9f, 1.05f, z + 5);
                        // your arms on the back of the car while you push; the sprag in the wheel when you brake
                        bool pushing = U.In("main") && U.stuck == 0, braking = U.In("aux");
                        var view = Quaternion.LookRotation(look - eye);
                        for (int s2 = 0; s2 < 2; s2++)
                        {
                            float side = s2 == 0 ? -1 : 1;
                            var shoulder = eye + view * new Vector3(side * .22f, -.36f, .1f);
                            var grip = pushing ? tramCar.TransformPoint(new Vector3(side * .36f, .33f, -.86f)) : eye + view * new Vector3(side * .26f, -.75f, .35f);
                            Span(tramArms[s2], shoulder, grip, .1f);
                            tramHands[s2].position = grip;
                        }
                        sprag.gameObject.SetActive(braking);
                        if (braking)
                        {
                            var wheel = tramCar.TransformPoint(new Vector3(.38f, -.42f, -.55f));
                            Span(sprag, wheel + new Vector3(.05f, .75f, -.75f), wheel, .08f);
                            sparks.transform.position = wheel; sparkRate = U.v > .05f ? 25 + U.v * 80 : 0;
                        }
                        tramCar.localRotation = U.stuck > 0 ? Quaternion.Euler(0, 8, 14) : Quaternion.Euler(Mathf.Sin(Time.time * 23) * U.v * .8f, 0, Mathf.Sin(Time.time * 17) * U.v * 1.2f);
                        break;
                    }
            }
            Emit(sparks, sparkRate);
            headlamp.transform.position = eye + new Vector3(.2f, .28f, .05f);
            headlamp.transform.rotation = Quaternion.LookRotation(look - eye) * Quaternion.Euler(5, -4, 0);
            Pose(eye, look);
            if (k == "tram") camSnap = true;
        }
    }

    /// <summary>Keeps a street sign's text in step with the place's label (the boarding house name depends on the origin).</summary>
    public class PlaceLabel : MonoBehaviour
    {
        public StreetPlace place; public TextMesh text;
        void Update() { if (CC.S != null && text != null) { string t = place.Label.ToUpperInvariant(); if (text.text != t) text.text = t; } }
    }
}
