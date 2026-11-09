// Copper Camp — the second half of the 3D world: people, the night town, the hill's houses and distant ranges,
// and the heads-up gauges drawn over the mine view (the browser game paints these on its canvas).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CopperCamp
{
    public partial class World3D
    {
        // ================= shared bits =================
        Mesh glowQuad, prism;

        /// <summary>A quad with white vertex colours, for the soft-dot particle material (halos, light pools).</summary>
        Mesh GlowQuadMesh()
        {
            if (glowQuad != null) return glowQuad;
            glowQuad = new Mesh { name = "GlowQuad" };
            glowQuad.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) };
            glowQuad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            glowQuad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            glowQuad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            glowQuad.RecalculateNormals(); glowQuad.RecalculateBounds();
            return glowQuad;
        }

        /// <summary>A soft glow: a halo that turns to face the camera, or (flat) a pool of light on the ground.</summary>
        Transform Glow(string n, Transform p, Vector3 pos, float size, Color c, bool flat = false)
        {
            var go = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(size, size, 1);
            if (flat) go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            go.GetComponent<MeshFilter>().sharedMesh = GlowQuadMesh();
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = art.particle;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Tint(r, c);
            if (!flat) halos.Add(go.transform);
            return go.transform;
        }
        readonly List<Transform> halos = new List<Transform>();

        void FaceHalos()
        {
            var cp = cam.transform.position;
            foreach (var h in halos)
                if (h != null && h.gameObject.activeInHierarchy) h.rotation = Quaternion.LookRotation(h.position - cp);
        }

        /// <summary>A triangular prism, ridge along X, base 1 x 1 on y = 0, peak at y = 1: a gable roof.</summary>
        Mesh PrismMesh()
        {
            if (prism != null) return prism;
            Vector3 a = new Vector3(-.5f, 0, -.5f), b = new Vector3(-.5f, 0, .5f), c = new Vector3(-.5f, 1, 0);
            Vector3 d = new Vector3(.5f, 0, -.5f), e = new Vector3(.5f, 0, .5f), f = new Vector3(.5f, 1, 0);
            var v = new List<Vector3>(); var t = new List<int>();
            void Tri(Vector3 p0, Vector3 p1, Vector3 p2) { int i = v.Count; v.Add(p0); v.Add(p1); v.Add(p2); t.Add(i); t.Add(i + 1); t.Add(i + 2); }
            Tri(a, b, c); Tri(d, f, e);                 // gable ends (outward-facing)
            Tri(a, f, d); Tri(a, c, f);                 // front slope
            Tri(b, e, f); Tri(b, f, c);                 // back slope
            prism = new Mesh { name = "Prism" };
            prism.vertices = v.ToArray(); prism.triangles = t.ToArray();
            var uv = new Vector2[v.Count]; for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(v[i].x + v[i].z, v[i].y);
            prism.uv = uv; prism.RecalculateNormals(); prism.RecalculateBounds();
            return prism;
        }

        GameObject Roof(string n, Transform p, Vector3 basePos, Vector3 size, Material m, Color tint)
        {
            var go = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(p, false); go.transform.localPosition = basePos; go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = PrismMesh();
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; Tint(r, tint);
            return go;
        }

        /// <summary>Points a box from a to b (used for arms, the drill arm and the like).</summary>
        static void Span(Transform t, Vector3 a, Vector3 b, float thick)
        {
            var d = b - a; float len = d.magnitude;
            if (len < 1e-4f) return;
            t.position = (a + b) * .5f;
            t.rotation = Quaternion.LookRotation(d / len);
            t.localScale = new Vector3(thick, thick, len);
        }

        // ================= people =================
        static readonly Color[] HAIR = { Hex("2a1a10"), Hex("4a3020"), Hex("1a1410"), Hex("6a4a2a"), Hex("8a6a4a"), Hex("777068") };

        /// <summary>A standing figure about 1.8 m tall, facing +Z: boots, trousers or a long skirt, a coat with sleeves and hands,
        /// a face with eyes and nose (and on some men a moustache), hair and a hat. Legs and arms hang from pivots so they swing.</summary>
        GameObject Person(string n, Transform p, Vector3 pos, float yaw, Color coat, Color? hat = null, bool skirt = false, Color? skin = null, bool apron = false)
        {
            var root = new GameObject(n).transform;
            root.SetParent(p, false); root.localPosition = pos; root.localRotation = Quaternion.Euler(0, yaw, 0);
            int seed = Mathf.Abs((int)(pos.x * 131 + pos.z * 71 + n.Length * 17 + yaw));
            var sk = skin ?? Hex("c9a07a");
            var hair = HAIR[seed % HAIR.Length];
            var trousers = Color.Lerp(coat, Hex("221e1a"), .55f);
            var boots = Hex("17110d");
            var cloth = art.cloth;

            // legs
            for (int s = -1; s <= 1; s += 2)
            {
                var hip = new GameObject(s < 0 ? "LegL" : "LegR").transform; hip.SetParent(root, false); hip.localPosition = new Vector3(s * .1f, .9f, 0);
                if (!skirt) Prim(PrimitiveType.Capsule, "Leg", hip, new Vector3(0, -.43f, 0), new Vector3(.15f, .44f, .16f), cloth, trousers);
                Box("Boot", hip, new Vector3(0, -.84f, .04f), new Vector3(.14f, .11f, .27f), cloth, boots);
            }
            if (skirt)
            {
                Cyl("Skirt1", root, new Vector3(0, .79f, 0), new Vector3(.42f, .13f, .34f), cloth, coat);
                Cyl("Skirt2", root, new Vector3(0, .53f, 0), new Vector3(.5f, .14f, .42f), cloth, Color.Lerp(coat, Color.black, .06f));
                Cyl("Skirt3", root, new Vector3(0, .25f, 0), new Vector3(.58f, .15f, .5f), cloth, Color.Lerp(coat, Color.black, .12f));
            }
            // body: coat over the shoulders, a frock-coat skirt for the men, a belt
            Prim(PrimitiveType.Capsule, "Body", root, new Vector3(0, 1.23f, 0), new Vector3(.44f, .36f, .27f), cloth, coat);
            if (!skirt) Box("CoatTail", root, new Vector3(0, .92f, 0), new Vector3(.42f, .3f, .25f), cloth, Color.Lerp(coat, Color.black, .08f));
            Box("Belt", root, new Vector3(0, 1.0f, 0), new Vector3(.43f, .05f, .26f), cloth, Color.Lerp(coat, Color.black, .5f));
            if (apron) Box("Apron", root, new Vector3(0, .9f, .14f), new Vector3(.38f, .78f, .02f), cloth, Hex("e8dcc4"));
            else if (!skirt) Box("Shirt", root, new Vector3(0, 1.47f, .125f), new Vector3(.12f, .14f, .02f), cloth, Hex("d8ccb4"));
            // arms
            for (int s = -1; s <= 1; s += 2)
            {
                var sh = new GameObject(s < 0 ? "ArmL" : "ArmR").transform; sh.SetParent(root, false); sh.localPosition = new Vector3(s * .27f, 1.5f, 0);
                sh.localRotation = Quaternion.Euler(0, 0, s * 6);
                Prim(PrimitiveType.Capsule, "Sleeve", sh, new Vector3(0, -.29f, 0), new Vector3(.12f, .3f, .13f), cloth, coat);
                Ball("Hand", sh, new Vector3(0, -.61f, .01f), new Vector3(.09f, .1f, .08f), cloth, sk);
            }
            // head
            Cyl("Neck", root, new Vector3(0, 1.63f, 0), new Vector3(.1f, .05f, .1f), cloth, sk);
            var head = new GameObject("HeadPivot").transform; head.SetParent(root, false); head.localPosition = new Vector3(0, 1.77f, 0);
            Ball("Head", head, Vector3.zero, new Vector3(.21f, .25f, .22f), cloth, sk);
            Ball("Hair", head, new Vector3(0, .04f, -.025f), new Vector3(.225f, .22f, .21f), cloth, hair);
            Box("Nose", head, new Vector3(0, -.01f, .11f), new Vector3(.035f, .05f, .04f), cloth, Color.Lerp(sk, Color.black, .12f));
            Ball("EyeL", head, new Vector3(-.045f, .025f, .097f), Vector3.one * .024f, cloth, Hex("1a1410"));
            Ball("EyeR", head, new Vector3(.045f, .025f, .097f), Vector3.one * .024f, cloth, Hex("1a1410"));
            if (!skirt && seed % 3 != 0) Box("Moustache", head, new Vector3(0, -.045f, .1f), new Vector3(.1f, .022f, .025f), cloth, hair);
            if (skirt && hat == null) Ball("Bun", head, new Vector3(0, .05f, -.11f), Vector3.one * .11f, cloth, hair);
            if (hat != null)
            {
                bool derby = seed % 2 == 0;
                Cyl("Brim", head, new Vector3(0, .1f, 0), new Vector3(derby ? .32f : .38f, .012f, derby ? .34f : .38f), cloth, hat.Value);
                if (derby) Ball("Crown", head, new Vector3(0, .15f, 0), new Vector3(.22f, .17f, .23f), cloth, hat.Value);
                else Cyl("Crown", head, new Vector3(0, .17f, 0), new Vector3(.22f, .07f, .22f), cloth, hat.Value);
                Cyl("Band", head, new Vector3(0, .125f, 0), new Vector3(.225f, .015f, .225f), cloth, Color.Lerp(hat.Value, Color.black, .5f));
            }
            return root.gameObject;
        }

        void AnimateWalk(GameObject person, float phase, bool moving)
        {
            if (person == null) return;
            float a = moving ? Mathf.Sin(phase) * 26f : 0f;
            var t = person.transform;
            var l = t.Find("LegL"); var r = t.Find("LegR"); var al = t.Find("ArmL"); var ar = t.Find("ArmR");
            if (l) l.localRotation = Quaternion.Euler(a, 0, 0);
            if (r) r.localRotation = Quaternion.Euler(-a, 0, 0);
            if (al) al.localRotation = Quaternion.Euler(-a * .7f, 0, -6);
            if (ar) ar.localRotation = Quaternion.Euler(a * .7f, 0, 6);
            var hp = t.Find("HeadPivot"); if (hp) hp.localPosition = new Vector3(0, 1.77f + (moving ? Mathf.Abs(Mathf.Sin(phase)) * .02f : 0), 0);
        }

        /// <summary>Small life in figures that stand still: a breath, a turn of the head.</summary>
        void Idle(GameObject person, float t)
        {
            if (person == null) return;
            var hp = person.transform.Find("HeadPivot"); if (hp) hp.localRotation = Quaternion.Euler(Mathf.Sin(t * .7f) * 3, Mathf.Sin(t * .31f) * 14, 0);
        }

        // ================= the hill: houses, far ranges, the focus ring =================
        void BuildHillHouses()
        {
            var r = CC.Rnd(77);
            Color[] paint = { Hex("8a7a68"), Hex("6e6254"), Hex("9a8466"), Hex("7a6a5a"), Hex("a08c72"), Hex("5e5448") };
            for (int i = 0; i < 64; i++)
            {
                float X = -40 + r() * 75, Z = -18 + r() * 12;
                if (Mathf.Abs(X - MapX(760)) < 6 && Z < -14) continue;
                bool uptown = Mathf.Abs(X + 18) < 10;
                bool brick = uptown ? r() < .75f : r() < .25f;
                float gy = Height(X, Z);
                var house = new GameObject("House").transform; house.SetParent(hillRoot, false); house.localPosition = new Vector3(X, gy - .1f, Z);
                house.localRotation = Quaternion.Euler(0, (r() - .5f) * 10, 0);
                if (brick)
                {
                    // a brick block with a flat roof and cornice, two or three storeys
                    float w = 2.2f + r() * 2.4f, h = 2.6f + r() * (uptown ? 5.5f : 2), d = 2 + r() * 1.6f;
                    Box("Block", house, new Vector3(0, h / 2, 0), new Vector3(w, h, d), Tiled(art.brick, w / 2, h / 2), Color.Lerp(Hex("b08a78"), Hex("8a6a5a"), r()));
                    Box("Cornice", house, new Vector3(0, h + .08f, 0), new Vector3(w + .15f, .18f, d + .15f), art.timber, Hex("3a2a20"));
                    int rows = Mathf.Max(1, Mathf.FloorToInt(h / 1.3f)), cols = Mathf.Max(1, Mathf.FloorToInt(w / .9f));
                    for (int y = 0; y < rows; y++)
                        for (int x = 0; x < cols; x++)
                        {
                            var win = Box("Window", house, new Vector3(-w / 2 + (x + .5f) * w / cols, .9f + y * 1.3f, -d / 2 - .02f), new Vector3(.3f, .5f, .02f), art.window);
                            townWindows.Add(win); townDayWindows.Add(Box("Pane", house, win.transform.localPosition + new Vector3(0, 0, -.005f), new Vector3(.3f, .5f, .02f), art.plain, Hex("2a2622")));
                        }
                }
                else
                {
                    // a miner's frame house: gable roof, a chimney, a door and a window or two
                    float w = 1.6f + r() * 1.2f, h = 1.5f + r() * .8f, d = 1.8f + r() * 1.2f;
                    var c = paint[(int)(r() * paint.Length) % paint.Length];
                    bool gableFront = r() < .5f;
                    Box("Walls", house, new Vector3(0, h / 2, 0), new Vector3(w, h, d), Tiled(art.timber, w / 2, h / 2), c);
                    var roofTint = Color.Lerp(Hex("3a2c24"), Hex("5a4436"), r());
                    if (gableFront) { var g = Roof("Roof", house, new Vector3(0, h, 0), new Vector3(d * 1.08f, w * .5f, w * 1.12f), art.steel, roofTint); g.transform.localRotation = Quaternion.Euler(0, 90, 0); }
                    else Roof("Roof", house, new Vector3(0, h, 0), new Vector3(w * 1.08f, w * .42f, d * 1.12f), art.steel, roofTint);
                    Box("Chimney", house, new Vector3(w * .25f, h + w * .35f, d * .15f), new Vector3(.22f, .7f, .22f), art.brick, Hex("6a4a3a"));
                    Box("Door", house, new Vector3(-w * .22f, .45f, -d / 2 - .02f), new Vector3(.32f, .8f, .02f), art.timber, Hex("2a1c14"));
                    var win = Box("Window", house, new Vector3(w * .2f, h * .55f, -d / 2 - .02f), new Vector3(.3f, .36f, .02f), art.window);
                    townWindows.Add(win); townDayWindows.Add(Box("Pane", house, win.transform.localPosition + new Vector3(0, 0, -.005f), new Vector3(.3f, .36f, .02f), art.plain, Hex("2a2622")));
                    if (r() < .5f) Box("Fence", house, new Vector3(0, .3f, -d / 2 - .8f), new Vector3(w * 1.2f, .5f, .05f), art.timber, Hex("5a4a3a"));
                }
            }
            // smoke from a few chimneys, the way the browser game puffs it
            for (int i = 0; i < 6; i++)
            {
                float X = -34 + i * 11, Z = -14 + (i % 2) * 4;
                var s = MakeParticles("ChimneySmoke", hillRoot, new Color(.5f, .46f, .42f, .25f), .9f, true);
                s.transform.localPosition = new Vector3(X, Height(X, Z) + 2.6f, Z);
                var m = s.main; m.startSpeed = .5f; m.startLifetime = 6; m.gravityModifier = -.01f; m.maxParticles = 80;
                var sz = s.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .5f, 1, 2.5f));
                stackSmoke.Add(s);
            }
        }
        readonly List<GameObject> townDayWindows = new List<GameObject>();

        Renderer farNear, farBack;
        Transform focusRing; Material ringMat;
        void BuildFarRanges()
        {
            // the East Ridge and the Continental Divide behind the hill, in two fading layers
            Vector2[] near = { new Vector2(0, 230), new Vector2(60, 215), new Vector2(140, 200), new Vector2(260, 205), new Vector2(380, 185), new Vector2(480, 170), new Vector2(560, 160), new Vector2(640, 135), new Vector2(700, 120), new Vector2(760, 96), new Vector2(820, 112), new Vector2(870, 88), new Vector2(920, 118), new Vector2(960, 104) };
            Vector2[] back = { new Vector2(0, 200), new Vector2(120, 180), new Vector2(240, 190), new Vector2(380, 160), new Vector2(480, 150), new Vector2(560, 128), new Vector2(640, 110), new Vector2(700, 96), new Vector2(760, 80), new Vector2(820, 96), new Vector2(880, 72), new Vector2(960, 90) };
            farNear = RangeCard("EastRidge", near, 72, 1.9f, 1.25f);
            farBack = RangeCard("Divide", back, 105, 2.4f, 1.45f);
            // the gold ring the browser game draws round the place being talked about
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 64f;
                    float a = Mathf.Clamp01(1 - Mathf.Abs(d - .86f) / .04f) + .45f * Mathf.Clamp01(1 - Mathf.Abs(d - .96f) / .02f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(a)));
                }
            tex.Apply();
            ringMat = new Material(art.particle); ringMat.SetTexture("_BaseMap", tex); ringMat.mainTexture = tex;
            var go = new GameObject("FocusRing", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(hillRoot, false);
            go.GetComponent<MeshFilter>().sharedMesh = GlowQuadMesh();
            var rr = go.GetComponent<MeshRenderer>(); rr.sharedMaterial = ringMat; Tint(rr, new Color(.85f, .64f, .25f, .95f));
            focusRing = go.transform;
        }

        Renderer RangeCard(string n, Vector2[] pts, float z, float xScale, float yScale)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < pts.Length; i++)
            {
                float X = (pts[i].x - 480) * .11f * xScale, top = (300 - pts[i].y) * .16f * yScale;
                v.Add(new Vector3(X, -6, 0)); v.Add(new Vector3(X, top, 0));
                if (i > 0) { int k = i * 2; t.Add(k - 2); t.Add(k - 1); t.Add(k); t.Add(k); t.Add(k - 1); t.Add(k + 1); }
            }
            var m = new Mesh { name = n }; m.vertices = v.ToArray(); m.triangles = t.ToArray();
            var uv = new Vector2[v.Count]; for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(v[i].x / 20, v[i].y / 20); m.uv = uv;
            m.RecalculateNormals(); m.RecalculateBounds();
            var go = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(hillRoot, false); go.transform.localPosition = new Vector3(0, 0, z);
            go.GetComponent<MeshFilter>().sharedMesh = m;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = Tiled(art.rock, 1, 1); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        void UpdateHillExtras(SceneState sc, Vector3 target)
        {
            bool dark = sc.dark;
            Tint(farNear, dark ? Hex("2a2228") : sc.season == 3 ? Hex("d8d4d0") : Hex("b8ad9e"));
            Tint(farBack, dark ? Hex("221c22") : sc.season == 3 ? Hex("cac8c4") : Hex("aaa092"));
            foreach (var w in townDayWindows) w.SetActive(!dark);
            bool show = sc.focus != null;
            focusRing.gameObject.SetActive(show);
            if (show)
            {
                float pulse = 1 + Mathf.Sin(Time.time * 2.4f) * .06f;
                var toCam = (cam.transform.position - target).normalized;
                focusRing.position = target + Vector3.up * 8f + toCam * 4f;
                focusRing.rotation = Quaternion.LookRotation(focusRing.position - cam.transform.position);
                focusRing.localScale = new Vector3(19 * pulse, 19 * pulse, 1);
            }
        }

        // ================= the night town =================
        Transform skyQuad;
        readonly List<Renderer> streetHeadframes = new List<Renderer>();

        void BuildStreetNight(Transform s, float L)
        {
            // sky: indigo overhead going to a smelter-red glow at the horizon, with stars (the browser street's gradient)
            const int TW = 1024, TH = 512;
            var tex = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color top = Hex("141122"), mid = Hex("3a2430"), low = Hex("7a4430");
            var px = new Color[TW * TH];
            for (int y = 0; y < TH; y++)
            {
                float f = 1 - y / (float)(TH - 1);                       // 0 at the top
                var c = f < .7f ? Color.Lerp(top, mid, f / .7f) : Color.Lerp(mid, low, (f - .7f) / .3f);
                for (int x = 0; x < TW; x++) px[y * TW + x] = c;
            }
            var r = CC.Rnd(9);
            for (int i = 0; i < 420; i++)
            {
                int x = (int)(r() * TW), y = TH / 3 + (int)(r() * TH * 2 / 3);
                float b = .3f + r() * .7f;
                px[y * TW + x] = Color.Lerp(px[y * TW + x], new Color(1, .98f, .92f), b);
                if (b > .85f && x + 1 < TW) px[y * TW + x + 1] = Color.Lerp(px[y * TW + x + 1], new Color(1, .98f, .92f), b * .5f);
            }
            tex.SetPixels(px); tex.Apply();
            var skyMat = new Material(art.photo); skyMat.SetTexture("_BaseMap", tex); skyMat.mainTexture = tex;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
            q.name = "NightSky"; q.transform.SetParent(s, false); q.transform.localPosition = new Vector3(L / 2, 18, 44); q.transform.localScale = new Vector3(110, 55, 1);
            q.GetComponent<Renderer>().sharedMaterial = skyMat; q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            skyQuad = q.transform;
            Glow("SmelterGlow", s, new Vector3(L * .7f, 4, 40), 60, new Color(1, .45f, .2f, .22f));

            // the hill behind Uptown: one dark silhouette with the lit windows of the Gulch and Centerville on it
            var v = new List<Vector3>(); var t = new List<int>();
            int n = 90;
            for (int i = 0; i <= n; i++)
            {
                float x = -40 + i * (L + 80) / n;
                float h = 9 + 7 * Mathf.Sin(x * .11f) + 3 * Mathf.Sin(x * .37f + 1) + 2 * Mathf.PerlinNoise(x * .2f, 3);
                v.Add(new Vector3(x, -1, 0)); v.Add(new Vector3(x, h, 0));
                if (i > 0) { int k = i * 2; t.Add(k - 2); t.Add(k - 1); t.Add(k); t.Add(k); t.Add(k - 1); t.Add(k + 1); }
            }
            var m = new Mesh { name = "HillBack" }; m.vertices = v.ToArray(); m.triangles = t.ToArray();
            var uv = new Vector2[v.Count]; for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(v[i].x / 6, v[i].y / 6); m.uv = uv;
            m.RecalculateNormals(); m.RecalculateBounds();
            var hgo = new GameObject("HillBack", typeof(MeshFilter), typeof(MeshRenderer)); hgo.transform.SetParent(s, false); hgo.transform.localPosition = new Vector3(0, 0, 34);
            hgo.GetComponent<MeshFilter>().sharedMesh = m;
            var hr = hgo.GetComponent<MeshRenderer>(); hr.sharedMaterial = Tiled(art.rock, 1, 1); Tint(hr, Hex("2a2230"));
            float HillH(float x) => 9 + 7 * Mathf.Sin(x * .11f) + 3 * Mathf.Sin(x * .37f + 1) + 2 * Mathf.PerlinNoise(x * .2f, 3);
            for (int i = 0; i < 150; i++)
            {
                float x = -12 + r() * (L + 24), y = HillH(x) * (.15f + r() * .75f);
                Box("HillLight", s, new Vector3(x, y, 33.8f), new Vector3(.18f, .14f, .02f), art.window);
            }
            for (int i = 0; i < 7; i++)
            {
                float x = 2 + i * 6.5f; float h = HillH(x);
                StreetHeadframe(s, new Vector3(x, h - 1.2f, 33), 5.5f, i % 2 == 0, i % 3 == 1 ? art.headframeBlack : art.headframe);
                // the lamps they kept burning on the sheave decks all night
                Box("SheaveLamp", s, new Vector3(x, h + 3.6f, 31.6f), Vector3.one * .16f, i % 2 == 0 ? art.redGlow : art.glow);
                Glow("SheaveHalo", s, new Vector3(x, h + 3.6f, 31.5f), 2.4f, i % 2 == 0 ? new Color(1, .3f, .2f, .5f) : new Color(1, .75f, .4f, .5f));
            }
        }

        /// <summary>Lamp heads with a halo and a pool of light on the boardwalk.</summary>
        void StreetLamp(Transform s, float x)
        {
            Cyl("Lamppost", s, new Vector3(x, 1.6f, -1.3f), new Vector3(.08f, 1.6f, .08f), art.steel, Hex("1a1a1a"));
            Box("LampArm", s, new Vector3(x, 3.15f, -1.3f), new Vector3(.5f, .05f, .05f), art.steel, Hex("1a1a1a"));
            var head = Box("LampHead", s, new Vector3(x, 3.3f, -1.3f), new Vector3(.25f, .32f, .25f), art.glow);
            PointLight("StreetLamp", head.transform, Vector3.zero, new Color(1, .78f, .5f), 7f, 12);
            Glow("LampHalo", s, new Vector3(x, 3.3f, -1.45f), 2.2f, new Color(1, .8f, .5f, .55f));
            Glow("LampPool", s, new Vector3(x, .22f, -.6f), 5.5f, new Color(1, .75f, .45f, .28f), true);
            Glow("LampPoolStreet", s, new Vector3(x, .02f, -2.6f), 6f, new Color(1, .7f, .4f, .16f), true);
        }

        /// <summary>Lit shop windows throw light on the boardwalk.</summary>
        void WindowSpill(Transform s, float x, float y, Color c)
        {
            Glow("WindowPool", s, new Vector3(x, .22f, 1.1f), 2.6f, new Color(c.r, c.g, c.b, .22f), true);
            Glow("WindowHalo", s, new Vector3(x, y, 1.55f), 1.5f, new Color(c.r, c.g, c.b, .22f));
        }

        void UpdateStreetNight(float camX, float L)
        {
            if (skyQuad != null) skyQuad.localPosition = new Vector3(camX, 18, 44);
            FaceHalos();
        }

        // ================= the gauges over the mine view =================
        GameObject hud; Text hudTitle, hudLine, hudBig; RectTransform hudFill1, hudFill2, hudMark1; Text hudLabel1, hudLabel2;
        GameObject hudBar2; readonly List<RectTransform> hudTicks = new List<RectTransform>(); Image heatVeil; Image hudBg;

        void BuildHud(Transform canvas)
        {
            heatVeil = new GameObject("HeatVeil", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            heatVeil.transform.SetParent(canvas, false);
            var vr = (RectTransform)heatVeil.transform; vr.anchorMin = Vector2.zero; vr.anchorMax = new Vector2(.6f, 1); vr.offsetMin = vr.offsetMax = Vector2.zero;
            heatVeil.color = new Color(.8f, .16f, .04f, 0); heatVeil.raycastTarget = false;

            hud = new GameObject("MineHud", typeof(RectTransform), typeof(Image));
            hud.transform.SetParent(canvas, false);
            var rt = (RectTransform)hud.transform; rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(14, -50); rt.sizeDelta = new Vector2(252, 104);
            hudBg = hud.GetComponent<Image>(); hudBg.color = new Color(.04f, .03f, .025f, .62f); hudBg.raycastTarget = false;
            hudLabel1 = HudText(hud.transform, new Vector2(10, -8), 13, TextAnchor.UpperLeft, new Vector2(230, 16));
            hudFill1 = HudBar(hud.transform, new Vector2(10, -26), out hudMark1);
            hudLabel2 = HudText(hud.transform, new Vector2(10, -40), 13, TextAnchor.UpperLeft, new Vector2(230, 16));
            hudFill2 = HudBar(hud.transform, new Vector2(10, -58), out _);
            hudBar2 = hudFill2.parent.gameObject;
            hudLine = HudText(hud.transform, new Vector2(10, -72), 14, TextAnchor.UpperLeft, new Vector2(230, 34));
            for (int i = 0; i < 3; i++)
            {
                var tk = new GameObject("Tick", typeof(RectTransform), typeof(Image)); tk.transform.SetParent(hudFill2.parent, false);
                var tr = (RectTransform)tk.transform; tr.anchorMin = tr.anchorMax = new Vector2(0, .5f); tr.sizeDelta = new Vector2(3, 16);
                tk.GetComponent<Image>().color = Hex("d9a441"); tk.GetComponent<Image>().raycastTarget = false;
                hudTicks.Add(tr);
            }
            // a big sign in the middle of the view: SLOW 60, OFF THE RAILS, BOUND
            var big = new GameObject("BigSign", typeof(RectTransform), typeof(Image)); big.transform.SetParent(canvas, false);
            var br = (RectTransform)big.transform; br.anchorMin = br.anchorMax = new Vector2(.3f, .72f); br.sizeDelta = new Vector2(260, 46);
            big.GetComponent<Image>().color = Hex("d9a441"); big.GetComponent<Image>().raycastTarget = false;
            hudBig = HudText(big.transform, Vector2.zero, 24, TextAnchor.MiddleCenter, new Vector2(260, 46));
            var hbr = (RectTransform)hudBig.transform; hbr.anchorMin = Vector2.zero; hbr.anchorMax = Vector2.one; hbr.pivot = new Vector2(.5f, .5f); hbr.offsetMin = hbr.offsetMax = Vector2.zero;
            hudBig.color = Hex("1a1410"); hudBig.fontStyle = FontStyle.Bold;
            hud.SetActive(false); big.SetActive(false);
        }

        Text HudText(Transform p, Vector2 pos, int size, TextAnchor anchor, Vector2 box)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(p, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos; rt.sizeDelta = box;
            var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.alignment = anchor; t.color = Hex("e8dcc4"); t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        RectTransform HudBar(Transform p, Vector2 pos, out RectTransform mark)
        {
            var track = new GameObject("Track", typeof(RectTransform), typeof(Image)); track.transform.SetParent(p, false);
            var tr = (RectTransform)track.transform; tr.anchorMin = tr.anchorMax = new Vector2(0, 1); tr.pivot = new Vector2(0, 1);
            tr.anchoredPosition = pos; tr.sizeDelta = new Vector2(230, 9);
            track.GetComponent<Image>().color = new Color(1, 1, 1, .14f); track.GetComponent<Image>().raycastTarget = false;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(track.transform, false);
            var fr = (RectTransform)fill.transform; fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0, 1); fr.offsetMin = fr.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().raycastTarget = false;
            var mk = new GameObject("Mark", typeof(RectTransform), typeof(Image)); mk.transform.SetParent(track.transform, false);
            mark = (RectTransform)mk.transform; mark.anchorMin = mark.anchorMax = new Vector2(0, .5f); mark.sizeDelta = new Vector2(3, 17);
            mk.GetComponent<Image>().color = new Color(1, .95f, .85f, .95f); mk.GetComponent<Image>().raycastTarget = false;
            return fr;
        }

        static void SetBar(RectTransform fill, float v, Color c)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(v), 1);
            fill.GetComponent<Image>().color = c;
        }

        void UpdateHud(ShiftState U, bool on)
        {
            if (hud == null) return;
            var big = hudBig.transform.parent.gameObject;
            hud.SetActive(on && U != null);
            bool panel = GameUI.Instance == null || GameUI.Instance.PanelVisible;
            ((RectTransform)heatVeil.transform).anchorMax = new Vector2(panel ? .6f : 1, 1);
            ((RectTransform)big.transform).anchorMin = ((RectTransform)big.transform).anchorMax = new Vector2(panel ? .3f : .5f, .74f);
            if (!on || U == null) { big.SetActive(false); heatVeil.color = new Color(.8f, .16f, .04f, 0); return; }
            string bigText = null; Color bigCol = Hex("d9a441");
            hudMark1.gameObject.SetActive(false);
            foreach (var tk in hudTicks) tk.gameObject.SetActive(false);
            hudBar2.SetActive(true); hudLabel2.gameObject.SetActive(true);
            float heat = 0;
            switch (U.kind)
            {
                case "muck":
                case "hotmuck":
                    hudLabel1.text = U.rest ? "YOUR BACK — resting" : "YOUR BACK";
                    SetBar(hudFill1, U.fatigue, Hex("b5622b"));
                    if (U.kind == "hotmuck")
                    {
                        hudLabel2.text = "HEAT  ·  ice left " + U.ice;
                        SetBar(hudFill2, U.heat, Hex("c0392b"));
                        heat = U.heat;
                        if (U.heat > .8f) { bigText = "TOO HOT — ICE (B)"; bigCol = Hex("c0392b"); }
                    }
                    else { hudBar2.SetActive(false); hudLabel2.gameObject.SetActive(false); }
                    hudLine.text = "shovelfuls in the car: " + U.loads + " / " + U.target;
                    break;
                case "timber":
                    hudLabel1.text = "THE SWING — tap at the centre";
                    SetBar(hudFill1, 0, Hex("6a9a8a"));
                    hudMark1.gameObject.SetActive(U.ph == "swing");
                    PlaceMark(hudMark1, (U.x + 1) / 2);
                    hudLabel2.text = "SETS PLACED " + U.sets + " / " + U.target;
                    SetBar(hudFill2, U.sets / (float)Mathf.Max(1, U.target), Hex("b88a4a"));
                    hudLine.text = U.crooked > 0 ? "crooked sets: " + U.crooked : U.ph == "hammer" ? "driving the wedges home…" : U.ph == "hoist" ? "hoisting the next set…" : "";
                    break;
                case "drill":
                    hudLabel1.text = "STEEL HEAT — ease off";
                    SetBar(hudFill1, U.jam, Color.Lerp(Hex("8a8a90"), Hex("e05020"), U.jam));
                    hudLabel2.text = "THE ROUND — " + Mathf.FloorToInt(U.prog * 8) + " of 8 holes";
                    SetBar(hudFill2, U.prog, Hex("6a9a8a"));
                    hudLine.text = U.stuck > 0 ? "bound in the hole — let her cool" : U.In("main") ? "drilling…" : "";
                    if (U.stuck > 0) { bigText = "BOUND — LET GO"; bigCol = Hex("c0392b"); }
                    break;
                case "tram":
                    {
                        int next = -1; for (int i = 0; i < 3; i++) if (U.hit[i] == 0 && U.zones[i] > U.pos) { next = i; break; }
                        float lim = next >= 0 ? U.lim[next] : .42f;
                        bool over = U.v > lim + .15f;
                        hudLabel1.text = next >= 0 ? "SPEED — curve ahead, under " + Mathf.RoundToInt(lim * 100) : "SPEED — ease her into the station";
                        SetBar(hudFill1, U.v, over ? Hex("c0392b") : Hex("d9a441"));
                        hudMark1.gameObject.SetActive(true); PlaceMark(hudMark1, Mathf.Min(1, lim + .15f));
                        hudLabel2.text = "TRIP TO THE STATION";
                        SetBar(hudFill2, U.pos, Hex("6a9a8a"));
                        for (int i = 0; i < 3; i++) { hudTicks[i].gameObject.SetActive(true); hudTicks[i].anchoredPosition = new Vector2(230 * U.zones[i], 0); }
                        hudLine.text = "loads delivered: " + U.loads + " / " + U.target + (U.derails > 0 ? "   ·   derailed " + U.derails : "");
                        if (U.stuck > 0) { bigText = "OFF THE RAILS"; bigCol = Hex("c0392b"); }
                        else if (next >= 0 && U.zones[next] - U.pos < .22f) { bigText = "SLOW " + Mathf.RoundToInt(lim * 100); bigCol = over ? Hex("e0603a") : Hex("d9a441"); }
                        else if (next < 0 && U.pos > .85f) { bigText = "STATION — SPRAG HER (B)"; bigCol = U.v > .42f ? Hex("e0603a") : Hex("d9a441"); }
                        break;
                    }
            }
            big.SetActive(bigText != null);
            if (bigText != null) { hudBig.text = bigText; big.GetComponent<Image>().color = bigCol; }
            float a = heat * .32f * (.9f + .1f * Mathf.Sin(Time.time * 5));
            heatVeil.color = new Color(.8f, .16f, .04f, a);
        }

        static void PlaceMark(RectTransform m, float x) { m.anchoredPosition = new Vector2(230 * Mathf.Clamp01(x), 0); }

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ================= the ore car =================
        /// <summary>An open box that is wider at the top (an ore car's body), with outside and inside faces.</summary>
        GameObject Hopper(string n, Transform p, Vector3 pos, Vector2 bottom, Vector2 top, float h, Material m, Color tint)
        {
            float bx = bottom.x / 2, bz = bottom.y / 2, tx = top.x / 2, tz = top.y / 2, y0 = -h / 2, y1 = h / 2;
            Vector3[] B = { new Vector3(-bx, y0, -bz), new Vector3(bx, y0, -bz), new Vector3(bx, y0, bz), new Vector3(-bx, y0, bz) };
            Vector3[] T = { new Vector3(-tx, y1, -tz), new Vector3(tx, y1, -tz), new Vector3(tx, y1, tz), new Vector3(-tx, y1, tz) };
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)   // a b c d go round the face; adds both sides
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2);
                int j = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                t.Add(j); t.Add(j + 1); t.Add(j + 2); t.Add(j); t.Add(j + 2); t.Add(j + 3);
            }
            Quad(B[0], B[1], T[1], T[0]); Quad(B[1], B[2], T[2], T[1]); Quad(B[2], B[3], T[3], T[2]); Quad(B[3], B[0], T[0], T[3]);
            Quad(B[3], B[2], B[1], B[0]);
            var mesh = new Mesh { name = n }; mesh.vertices = v.ToArray(); mesh.triangles = t.ToArray(); mesh.uv = uv.ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(p, false); go.transform.localPosition = pos;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; Tint(r, tint);
            return go;
        }

        /// <summary>A side-dump ore car: a riveted steel body on a frame, a rim, iron straps, four flanged wheels on axles,
        /// couplers at the ends, and a heap of ore ("Load") that the muck game fills.</summary>
        Transform OreCar(Transform p, Vector3 pos)
        {
            var c = new GameObject("OreCar").transform; c.SetParent(p, false); c.localPosition = pos;
            var body = Hex("4a3e36"); var iron = Hex("2a2624");
            Hopper("Body", c, new Vector3(0, .02f, 0), new Vector2(.86f, 1.42f), new Vector2(1.12f, 1.72f), .66f, Tiled(art.steel, 2, 1), body);
            Box("Frame", c, new Vector3(0, -.36f, 0), new Vector3(.9f, .1f, 1.5f), art.steel, iron);
            Box("RimF", c, new Vector3(0, .36f, -.86f), new Vector3(1.16f, .06f, .06f), art.steel, iron);
            Box("RimB", c, new Vector3(0, .36f, .86f), new Vector3(1.16f, .06f, .06f), art.steel, iron);
            Box("RimL", c, new Vector3(-.56f, .36f, 0), new Vector3(.06f, .06f, 1.76f), art.steel, iron);
            Box("RimR", c, new Vector3(.56f, .36f, 0), new Vector3(.06f, .06f, 1.76f), art.steel, iron);
            for (int i = 0; i < 3; i++)
            {
                float z = -.5f + i * .5f;
                Box("StrapL", c, new Vector3(-.5f, .02f, z), new Vector3(.03f, .66f, .07f), art.steel, iron, new Vector3(0, 0, -11));
                Box("StrapR", c, new Vector3(.5f, .02f, z), new Vector3(.03f, .66f, .07f), art.steel, iron, new Vector3(0, 0, 11));
            }
            Box("StrapF", c, new Vector3(0, .02f, -.79f), new Vector3(.07f, .66f, .03f), art.steel, iron, new Vector3(13, 0, 0));
            Box("StrapB", c, new Vector3(0, .02f, .79f), new Vector3(.07f, .66f, .03f), art.steel, iron, new Vector3(-13, 0, 0));
            for (int a = 0; a < 2; a++)
            {
                float z = a == 0 ? -.52f : .52f;
                Cyl("Axle", c, new Vector3(0, -.44f, z), new Vector3(.06f, .42f, .06f), art.steel, iron, new Vector3(0, 0, 90));
                for (int s = -1; s <= 1; s += 2)
                {
                    Cyl("Wheel", c, new Vector3(s * .36f, -.44f, z), new Vector3(.34f, .035f, .34f), art.steel, Hex("1e1c1a"), new Vector3(0, 0, 90));
                    Cyl("Flange", c, new Vector3(s * .32f, -.44f, z), new Vector3(.4f, .01f, .4f), art.steel, Hex("161412"), new Vector3(0, 0, 90));
                }
            }
            Box("CouplerF", c, new Vector3(0, -.33f, -.82f), new Vector3(.12f, .1f, .2f), art.steel, iron);
            Box("CouplerB", c, new Vector3(0, -.33f, .82f), new Vector3(.12f, .1f, .2f), art.steel, iron);
            var load = Ball("Load", c, new Vector3(0, .3f, 0), new Vector3(.95f, .28f, 1.5f), Tiled(art.ore, 2, 2));
            // broken ore lumps proud of the heap: chunky (the heap's own scale is undone so they stay rock-shaped), in the photo rock
            for (int i = 0; i < 10; i++)
            {
                float sz = .13f + (i * 7 % 5) * .025f;
                var lump = Prim(PrimitiveType.Cube, "Lump", load.transform, new Vector3((i % 3 - 1) * .26f, .4f + (i % 2) * .12f, ((i / 3) - 1.5f) * .24f),
                    new Vector3(sz / .95f, sz * .8f / .28f, sz * 1.1f / 1.5f), RockMat, Color.Lerp(new Color(1.2f, 1.0f, .8f), new Color(1.0f, .7f, .5f), (i * 37 % 10) / 10f));
                lump.transform.localRotation = Quaternion.Euler(i * 31 % 40 - 20, i * 53, i * 17 % 30 - 15);
            }
            return c;
        }

        // ================= the interiors, dressed =================
        readonly Dictionary<string, ParticleSystem> roomHaze = new Dictionary<string, ParticleSystem>();

        /// <summary>What every room gets: wainscoting, a baseboard and chair rail, beams overhead, wall lamps with a glow,
        /// a window on the night, a framed picture, and a hanging lamp with a globe.</summary>
        void DressRoom(Transform r, string id, string kind, float W, float D, float H, Color lampCol)
        {
            bool plaster = kind == "church" || kind == "ward";
            var wood = Hex("3e2a1c");
            Box("Wainscot", r, new Vector3(0, .55f, D / 2 - .07f), new Vector3(W - .1f, 1.1f, .04f), Tiled(art.timber, W / 1.5f, 1), plaster ? Hex("8a7a68") : wood);
            Box("WainscotL", r, new Vector3(-W / 2 + .07f, .55f, 0), new Vector3(.04f, 1.1f, D - .1f), Tiled(art.timber, D / 1.5f, 1), plaster ? Hex("8a7a68") : wood);
            Box("WainscotR", r, new Vector3(W / 2 - .07f, .55f, 0), new Vector3(.04f, 1.1f, D - .1f), Tiled(art.timber, D / 1.5f, 1), plaster ? Hex("8a7a68") : wood);
            Box("ChairRail", r, new Vector3(0, 1.12f, D / 2 - .09f), new Vector3(W - .1f, .06f, .06f), art.timber, Hex("2a1c12"));
            Box("Baseboard", r, new Vector3(0, .08f, D / 2 - .09f), new Vector3(W - .1f, .16f, .05f), art.timber, Hex("1e140c"));
            Box("Crown", r, new Vector3(0, H - .08f, D / 2 - .09f), new Vector3(W - .1f, .14f, .08f), art.timber, Hex("2a1c12"));
            for (int i = 0; i < 4; i++) Box("Beam", r, new Vector3(0, H - .12f, -D / 2 + .9f + i * (D - 1.4f) / 3), new Vector3(W, .2f, .22f), art.timber, Hex("3a2818"));
            // wall lamps
            for (int s = -1; s <= 1; s += 2)
            {
                var sx = s * W * .34f;
                Box("SconceArm", r, new Vector3(sx, 2.25f, D / 2 - .18f), new Vector3(.05f, .05f, .22f), art.steel, Hex("8a6a3a"));
                Box("Sconce", r, new Vector3(sx, 2.32f, D / 2 - .3f), new Vector3(.12f, .18f, .12f), art.glow);
                Glow("SconceHalo", r, new Vector3(sx, 2.32f, D / 2 - .36f), 1.1f, new Color(lampCol.r, lampCol.g * .9f, lampCol.b * .8f, .45f));
                var wash = Glow("SconceWash", r, new Vector3(sx, 2.0f, D / 2 - .11f), 2.4f, new Color(lampCol.r, lampCol.g * .85f, lampCol.b * .7f, .18f));
                halos.Remove(wash);   // a wash of light flat on the wall, not a halo that turns to the camera
                PointLight("SconceLight", r, new Vector3(sx, 2.3f, D / 2 - .5f), lampCol, 1.4f, 4.5f);
            }
            // a soft warm fill from behind you, so the near side of the room isn't lost in shadow
            PointLight("FillLight", r, new Vector3(0, 2.4f, -2.8f), Color.Lerp(lampCol, Color.white, .4f), 2.4f, 9);
            // a window on the left wall, black night outside with a little moonlight on the glass
            if (kind != "steam")
            {
                Box("WinFrame", r, new Vector3(-W / 2 + .08f, 1.9f, .6f), new Vector3(.05f, 1.3f, 1.0f), art.timber, Hex("2a1c12"));
                Box("WinGlass", r, new Vector3(-W / 2 + .11f, 1.9f, .6f), new Vector3(.02f, 1.15f, .85f), art.plain, Hex("1c2230"));
                Box("WinBarV", r, new Vector3(-W / 2 + .12f, 1.9f, .6f), new Vector3(.02f, 1.15f, .04f), art.timber, Hex("2a1c12"));
                Box("WinBarH", r, new Vector3(-W / 2 + .12f, 1.9f, .6f), new Vector3(.02f, .04f, .85f), art.timber, Hex("2a1c12"));
            }
            // a picture in a gilt frame
            if (kind != "shelves" && kind != "steam")
            {
                Box("PicFrame", r, new Vector3(W * .36f, 1.75f, D / 2 - .08f), new Vector3(.7f, .55f, .03f), art.plain, Hex("a07a3a"));
                Box("PicCanvas", r, new Vector3(W * .36f, 1.75f, D / 2 - .1f), new Vector3(.58f, .43f, .02f), art.cloth, Color.HSVToRGB((id.Length * .13f) % 1, .35f, .35f));
            }
            // the hanging lamp: chain, brass font and a glass globe
            Cyl("Chain", r, new Vector3(0, H - .3f, .5f), new Vector3(.02f, .3f, .02f), art.steel, Hex("3a3a3a"));
            Cyl("Font", r, new Vector3(0, H - .62f, .5f), new Vector3(.22f, .05f, .22f), art.steel, Hex("a07a3a"));
            Glow("LampHalo", r, new Vector3(0, H - .45f, .3f), 1.8f, new Color(lampCol.r, lampCol.g, lampCol.b, .5f));
            if (kind == "bar" || kind == "steam" || kind == "hall")
            {
                // tobacco smoke in the bars and the hall, steam in the sauna
                var steam = kind == "steam";
                var ps = MakeParticles(steam ? "SaunaSteam" : "Haze", r, steam ? new Color(1, .96f, .92f, .14f) : new Color(.75f, .7f, .62f, .05f), steam ? 1.2f : 2.2f, false);
                ps.transform.localPosition = new Vector3(0, steam ? .6f : 2.2f, 1);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(W * .8f, .4f, D * .6f);
                var m = ps.main; m.startSpeed = steam ? .35f : .05f; m.startLifetime = steam ? 5 : 10; m.gravityModifier = steam ? -.04f : 0; m.maxParticles = 120;
                roomHaze[id] = ps;
            }
        }

        /// <summary>Extra furniture and life for each kind of room, on top of what BuildInterior lays out.</summary>
        void DressKind(Transform r, string id, string kind, float W, float D, float H)
        {
            switch (kind)
            {
                case "bar":
                    {
                        bool casino = id == "mercury";
                        Cyl("FootRail", r, new Vector3(0, .18f, 1.12f), new Vector3(.05f, 3.5f, .05f), art.steel, Hex("b08a40"), new Vector3(0, 0, 90));
                        for (int i = 0; i < 3; i++) { Cyl("Tap", r, new Vector3(-.6f + i * .3f, 1.3f, 1.6f), new Vector3(.04f, .16f, .04f), art.steel, Hex("c09a50")); }
                        for (int row = 0; row < 2; row++)
                            for (int i = 0; i < 14; i++) Cyl("Bottle", r, new Vector3(-2.4f + i * .37f, 2.62f + row * .42f, 3.15f), new Vector3(.07f, .13f, .07f), art.glow, Color.HSVToRGB((i * .17f + row * .4f) % 1, .55f, .45f));
                        Box("Shelf1", r, new Vector3(0, 2.48f, 3.15f), new Vector3(5.6f, .04f, .25f), art.timber, Hex("2a1810"));
                        Box("Shelf2", r, new Vector3(0, 2.9f, 3.15f), new Vector3(5.6f, .04f, .25f), art.timber, Hex("2a1810"));
                        Cyl("Spittoon", r, new Vector3(-3.2f, .12f, .9f), new Vector3(.25f, .12f, .25f), art.steel, Hex("b08a40"));
                        Cyl("Spittoon", r, new Vector3(2.9f, .12f, .9f), new Vector3(.25f, .12f, .25f), art.steel, Hex("b08a40"));
                        // a table near you with glasses and a bottle
                        Cyl("Table", r, new Vector3(1.9f, .72f, -1.6f), new Vector3(.9f, .03f, .9f), art.timber, Hex("4a2e1c"));
                        Cyl("TableLeg", r, new Vector3(1.9f, .36f, -1.6f), new Vector3(.08f, .36f, .08f), art.timber, Hex("2a1810"));
                        Cyl("Glass", r, new Vector3(1.7f, .8f, -1.5f), new Vector3(.07f, .06f, .07f), art.plain, Hex("c8c0a0"));
                        Cyl("Glass", r, new Vector3(2.05f, .8f, -1.75f), new Vector3(.07f, .06f, .07f), art.plain, Hex("c8c0a0"));
                        Cyl("Bottle", r, new Vector3(1.95f, .88f, -1.45f), new Vector3(.08f, .14f, .08f), art.glow, Hex("5a3a1a"));
                        Person("Card player", r, new Vector3(2.6f, -.35f, -1.4f), -100, COATS[4], Hex("1a1410"));
                        if (casino)
                        {
                            Box("CurtainL", r, new Vector3(-W / 2 + .15f, H / 2, 2.2f), new Vector3(.08f, H - .2f, 1.6f), art.cloth, Hex("6a1a1e"));
                            Box("CurtainR", r, new Vector3(W / 2 - .15f, H / 2, 2.2f), new Vector3(.08f, H - .2f, 1.6f), art.cloth, Hex("6a1a1e"));
                            for (int i = 0; i < 7; i++) { Ball("Chandelier", r, new Vector3(-.9f + i * .3f, H - .7f, 1.2f), Vector3.one * .1f, art.redGlow); }
                            Glow("ChandelierHalo", r, new Vector3(0, H - .7f, 1.1f), 3f, new Color(1, .4f, .35f, .35f));
                            Box("Roulette", r, new Vector3(-2.3f, .78f, -1.4f), new Vector3(1.6f, .06f, .9f), art.cloth, Hex("1f4a2a"));
                            Cyl("Wheel", r, new Vector3(-2.7f, .84f, -1.4f), new Vector3(.4f, .03f, .4f), art.timber, Hex("3a1a10"));
                        }
                        break;
                    }
                case "hall":
                    Box("Flag", r, new Vector3(-3.6f, 2.5f, 3.4f), new Vector3(1.2f, .75f, .02f), art.cloth, Hex("8a2a24"));
                    for (int i = 0; i < 3; i++) Box("Stripe", r, new Vector3(-3.6f, 2.25f + i * .25f, 3.38f), new Vector3(1.2f, .08f, .01f), art.cloth, Hex("e8dcc4"));
                    Box("Canton", r, new Vector3(-3.95f, 2.68f, 3.37f), new Vector3(.45f, .38f, .01f), art.cloth, Hex("2a3050"));
                    Box("Charter", r, new Vector3(3.6f, 2.0f, 3.42f), new Vector3(.9f, 1.2f, .03f), art.plain, Hex("d8ccb0"));
                    Box("CharterFrame", r, new Vector3(3.6f, 2.0f, 3.44f), new Vector3(1.0f, 1.3f, .02f), art.timber, Hex("2a1c12"));
                    Box("Gavel", r, new Vector3(.2f, 1.24f, 2.6f), new Vector3(.25f, .06f, .06f), art.timber, Hex("2a1810"));
                    break;
                case "church":
                    Box("Runner", r, new Vector3(0, .01f, -.2f), new Vector3(1.1f, .01f, 5.8f), art.cloth, Hex("6a1a1e"));
                    Box("CrossV", r, new Vector3(0, 2.4f, 3.38f), new Vector3(.12f, 1.1f, .05f), art.plain, Hex("c09a50"));
                    Box("CrossH", r, new Vector3(0, 2.6f, 3.38f), new Vector3(.6f, .12f, .05f), art.plain, Hex("c09a50"));
                    for (int i = 0; i < 3; i++) Glow("GlassGlow", r, new Vector3(-2.5f + i * 2.5f, 2.4f, 3.3f), 2.2f, WithAlpha(Color.HSVToRGB(.6f - i * .2f, .6f, 1), .3f));
                    for (int i = 0; i < 6; i++) Glow("CandleFlame", r, new Vector3(-.8f + i * .32f, 1.36f, 2.75f), .35f, new Color(1, .8f, .4f, .8f));
                    break;
                case "shelves":
                    Glow("ReadingHalo", r, new Vector3(-.6f, 1.05f, -.05f), 1.3f, new Color(.5f, 1f, .55f, .4f));
                    Glow("ReadingHalo", r, new Vector3(.8f, 1.05f, -.05f), 1.3f, new Color(.5f, 1f, .55f, .4f));
                    Box("Ladder", r, new Vector3(-2.1f, 1.4f, 2.8f), new Vector3(.5f, 2.8f, .05f), art.timber, Hex("5a3a24"), new Vector3(-12, 0, 0));
                    Ball("Globe", r, new Vector3(3.4f, 1.25f, .4f), Vector3.one * .45f, art.cloth, Hex("8a7a50"));
                    Cyl("GlobeStand", r, new Vector3(3.4f, .5f, .4f), new Vector3(.1f, .5f, .1f), art.timber, Hex("3a2818"));
                    for (int i = 0; i < 4; i++) Box("Chair", r, new Vector3(-1.1f + i * .75f, .45f, i % 2 == 0 ? -.75f : .75f), new Vector3(.4f, .9f, .4f), art.timber, Hex("4a2e1c"));
                    Box("Rug", r, new Vector3(0, .01f, 0), new Vector3(4.2f, .01f, 2.6f), art.cloth, Hex("2a4a32"));
                    break;
                case "counter":
                    for (int i = 0; i < 5; i++) Cyl("Stool", r, new Vector3(-2.2f + i * 1.1f, .38f, .9f), new Vector3(.32f, .38f, .32f), art.timber, Hex("5a3a24"));
                    Cyl("PieStand", r, new Vector3(.9f, 1.25f, 1.55f), new Vector3(.4f, .14f, .4f), art.plain, Hex("d8c8a8"));
                    Box("Stove", r, new Vector3(3.6f, .6f, 2.6f), new Vector3(.9f, 1.2f, .8f), art.steel, Hex("1e1a18"));
                    Box("StoveGlow", r, new Vector3(3.6f, .55f, 2.19f), new Vector3(.4f, .2f, .02f), art.glow);
                    Glow("StoveHalo", r, new Vector3(3.6f, .55f, 2.1f), 1.2f, new Color(1, .5f, .2f, .5f));
                    break;
                case "store":
                    for (int i = 0; i < 3; i++) Cyl("Barrel", r, new Vector3(-3.8f + i * .7f, .45f, -.8f), new Vector3(.55f, .45f, .55f), art.timber, Hex("6a4a2c"));
                    for (int i = 0; i < 4; i++) Ball("Sack", r, new Vector3(2.4f + (i % 2) * .5f, .3f + (i / 2) * .45f, -1f), new Vector3(.5f, .45f, .45f), art.cloth, Hex("b8a888"));
                    for (int i = 0; i < 2; i++) { Cyl("ShopLamp", r, new Vector3(-2.4f + i * 4.8f, H - .9f, .8f), new Vector3(.3f, .12f, .3f), art.glow); Glow("ShopLampHalo", r, new Vector3(-2.4f + i * 4.8f, H - .95f, .6f), 1.4f, new Color(1, .8f, .5f, .45f)); }
                    Box("Sign", r, new Vector3(0, 3.2f, 3.42f), new Vector3(3.4f, .45f, .03f), art.plain, Hex("2a1f18"));
                    Sign("HENNESSY'S", r, new Vector3(0, 3.2f, 3.39f), .04f, Hex("f0cc80"));
                    break;
                case "steam":
                    Glow("StoneGlow", r, new Vector3(-3.6f, 1.15f, -.75f), 1.6f, new Color(1, .45f, .2f, .55f));
                    Cyl("Bucket", r, new Vector3(-2.8f, .2f, -.9f), new Vector3(.3f, .2f, .3f), art.timber, Hex("6a4a2c"));
                    Box("Whisks", r, new Vector3(3.4f, 1.6f, 3.4f), new Vector3(.4f, .5f, .05f), art.cloth, Hex("4a5a2a"));
                    break;
                case "ward":
                    for (int i = 0; i < 3; i++) Box("Screen", r, new Vector3(-2.2f + i * 2.2f, .9f, 2.4f), new Vector3(.05f, 1.6f, 1.4f), art.cloth, Hex("d8d8d0"));
                    Box("Cross", r, new Vector3(0, 2.8f, 3.4f), new Vector3(.12f, .6f, .03f), art.plain, Hex("1a1a1a"));
                    Box("CrossH", r, new Vector3(0, 2.9f, 3.4f), new Vector3(.4f, .12f, .03f), art.plain, Hex("1a1a1a"));
                    Cyl("Basin", r, new Vector3(3.6f, .9f, 1.2f), new Vector3(.4f, .08f, .4f), art.plain, Hex("e8e8ec"));
                    break;
                case "room":
                    Box("Chair", r, new Vector3(-.9f, .45f, .5f), new Vector3(.42f, .9f, .42f), art.timber, Hex("4a2e1c"));
                    Box("Chair", r, new Vector3(.9f, .45f, .5f), new Vector3(.42f, .9f, .42f), art.timber, Hex("4a2e1c"));
                    Glow("StoveHalo", r, new Vector3(3.5f, .5f, 1.9f), 1.2f, new Color(1, .5f, .2f, .5f));
                    Box("Rug", r, new Vector3(0, .01f, .6f), new Vector3(3.2f, .01f, 2f), art.cloth, Hex("5a2a22"));
                    Box("Trunk", r, new Vector3(-3.4f, .3f, 1.2f), new Vector3(1.0f, .6f, .55f), art.timber, Hex("3a2818"));
                    Cyl("Lantern", r, new Vector3(0, .95f, .6f), new Vector3(.12f, .12f, .12f), art.glow);
                    Glow("LanternHalo", r, new Vector3(0, .98f, .5f), .9f, new Color(1, .8f, .5f, .55f));
                    break;
            }
        }

        // ================= words in the room =================
        // who in each room speaks for which role; filled from the figures' names when the rooms are built
        readonly Dictionary<string, Dictionary<string, Transform>> roomSpeakers = new Dictionary<string, Dictionary<string, Transform>>();
        static readonly Dictionary<string, string> NAME_ROLE = new Dictionary<string, string>
        {
            { "Teddy", "barkeep" }, { "Piano player", "barkeep" }, { "Drinker", "partner" }, { "Nell", "nell" }, { "Dancer", "dancer" },
            { "Speaker", "speaker" }, { "Member", "partner" }, { "Father Brosnan", "priest" }, { "Miss Haskell", "librarian" },
            { "Counter woman", "counter" }, { "Night shift", "partner" }, { "Clerk", "clerk" }, { "Finn", "finn" },
            { "Sister Mary Agnes", "sister" }, { "Landlady", "landlady" }, { "Faro dealer", "dealer" }, { "Reader", "partner" }
        };

        void RegisterSpeakers(string id, Transform room)
        {
            var map = new Dictionary<string, Transform>();
            foreach (var t in room.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "HeadPivot" || t.parent == null) continue;
                if (NAME_ROLE.TryGetValue(t.parent.name, out var role) && !map.ContainsKey(role)) map[role] = t;
                if (!map.ContainsKey("anyone")) map["anyone"] = t;
            }
            if (map.ContainsKey("nell") && !map.ContainsKey("dancer")) map["dancer"] = map["nell"];
            if (map.ContainsKey("dancer") && !map.ContainsKey("nell")) map["nell"] = map["dancer"];
            roomSpeakers[id] = map;
        }

        GameObject bubble; RectTransform bubbleRt, canvasRt; Text bubbleText; CanvasGroup bubbleGroup; int bubbleSerial = -1; float bubbleAge;

        void BuildBubble(Transform canvas)
        {
            canvasRt = (RectTransform)canvas;
            bubble = new GameObject("SpeechBubble", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            bubble.transform.SetParent(canvas, false);
            bubbleRt = (RectTransform)bubble.transform; bubbleRt.anchorMin = bubbleRt.anchorMax = new Vector2(.5f, .5f); bubbleRt.pivot = new Vector2(.5f, 0);
            var img = bubble.GetComponent<Image>(); img.color = new Color(.95f, .9f, .8f, .95f); img.raycastTarget = false;
            var ol = bubble.AddComponent<Outline>(); ol.effectColor = new Color(.35f, .22f, .12f, .9f); ol.effectDistance = new Vector2(1.5f, -1.5f);
            bubbleGroup = bubble.GetComponent<CanvasGroup>(); bubbleGroup.blocksRaycasts = false; bubbleGroup.interactable = false;
            var tail = new GameObject("Tail", typeof(RectTransform), typeof(Image)); tail.transform.SetParent(bubble.transform, false);
            var tr = (RectTransform)tail.transform; tr.anchorMin = tr.anchorMax = new Vector2(.5f, 0); tr.sizeDelta = new Vector2(14, 14); tr.anchoredPosition = new Vector2(0, -2);
            tr.localRotation = Quaternion.Euler(0, 0, 45); tail.GetComponent<Image>().color = img.color; tail.GetComponent<Image>().raycastTarget = false;
            var tgo = new GameObject("Text", typeof(RectTransform), typeof(Text)); tgo.transform.SetParent(bubble.transform, false);
            var trt = (RectTransform)tgo.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(12, 8); trt.offsetMax = new Vector2(-12, -8);
            bubbleText = tgo.GetComponent<Text>(); bubbleText.font = font; bubbleText.fontSize = 17; bubbleText.fontStyle = FontStyle.Italic;
            bubbleText.color = Hex("2a1a10"); bubbleText.alignment = TextAnchor.MiddleCenter; bubbleText.raycastTarget = false;
            bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap; bubbleText.verticalOverflow = VerticalWrapMode.Overflow;
            bubble.SetActive(false);
        }

        Transform speakingHead;
        void UpdateBubble(SceneState sc)
        {
            speakingHead = null;
            if (bubble == null) return;
            bool show = sc.mode == "inside" && !string.IsNullOrEmpty(sc.speech) && sc.inside != null
                        && roomSpeakers.TryGetValue(sc.inside, out var map) && map.Count > 0;
            if (!show) { if (bubble.activeSelf) bubble.SetActive(false); return; }
            var m = roomSpeakers[sc.inside];
            Transform head = sc.speaker != null && m.TryGetValue(sc.speaker, out var h) ? h : m["anyone"];
            if (sc.speechSerial != bubbleSerial)
            {
                bubbleSerial = sc.speechSerial; bubbleAge = 0;
                bubbleText.text = "“" + sc.speech.Trim('“', '”', '"') + "”";
                float w = Mathf.Clamp(bubbleText.preferredWidth + 26, 120, 300);
                bubbleRt.sizeDelta = new Vector2(w, 40);
                float hgt = Mathf.Max(40, bubbleText.preferredHeight + 18);
                bubbleRt.sizeDelta = new Vector2(w, hgt);
            }
            bubbleAge += Time.deltaTime;
            speakingHead = head;
            var screen = cam.WorldToScreenPoint(head.position + Vector3.up * .38f);
            if (screen.z <= 0) { bubble.SetActive(false); return; }
            if (!bubble.activeSelf) bubble.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out var local);
            // keep it inside the 3D view
            float viewRight = canvasRt.rect.width * (cam.rect.width - .5f), viewLeft = -canvasRt.rect.width * .5f;
            float half = bubbleRt.sizeDelta.x / 2 + 8;
            local.x = Mathf.Clamp(local.x, viewLeft + half, viewRight - half);
            local.y = Mathf.Min(local.y + 6, canvasRt.rect.height * .5f - bubbleRt.sizeDelta.y - 60);
            bubbleRt.anchoredPosition = local;
            bubbleGroup.alpha = Mathf.Clamp01(bubbleAge / .35f);
        }
    }
}
