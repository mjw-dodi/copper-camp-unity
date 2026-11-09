// Copper Camp — the drift, rebuilt: a hand-hewn tunnel with an uneven arched back, rough walls and a rocky floor
// (photo-scanned CC0 textures from Poly Haven with surface-detail maps), square sets of rough timber every metre and
// a half with wedges and lagging boards behind them, ties bedded in ballast with spiked rails, an air line and a water
// line, puddles that catch the lamps, and two crosscuts going off into the dark. Static pieces are merged into a few
// meshes so the web build stays quick.
using System.Collections.Generic;
using UnityEngine;

namespace CopperCamp
{
    public partial class World3D
    {
        // ---------- merging many boxes into one mesh per material ----------
        class Batch { public Material mat; public Color tint; public readonly List<CombineInstance> parts = new List<CombineInstance>(); }
        readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
        readonly Dictionary<PrimitiveType, Mesh> primMeshes = new Dictionary<PrimitiveType, Mesh>();

        Mesh PrimMesh(PrimitiveType t)
        {
            if (primMeshes.TryGetValue(t, out var m)) return m;
            var go = GameObject.CreatePrimitive(t); m = go.GetComponent<MeshFilter>().sharedMesh; Destroy(go);
            primMeshes[t] = m; return m;
        }

        void Add(PrimitiveType t, Material m, Color tint, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            tint = new Color(Mathf.Round(tint.r * 12) / 12, Mathf.Round(tint.g * 12) / 12, Mathf.Round(tint.b * 12) / 12, 1);   // a few shades, few batches
            // one batch per 6 m section: the web renderer lights each object with only its few nearest lamps
            int section = Mathf.FloorToInt(pos.z / SECTION);
            string key = section + "|" + m.name + "|" + ColorUtility.ToHtmlStringRGB(tint);
            if (!batches.TryGetValue(key, out var b)) { b = new Batch { mat = m, tint = tint }; batches[key] = b; }
            b.parts.Add(new CombineInstance { mesh = PrimMesh(t), transform = Matrix4x4.TRS(pos, Quaternion.Euler(euler), scale) });
        }

        void Flush(Transform parent, string name)
        {
            foreach (var b in batches.Values)
            {
                if (b.parts.Count == 0) continue;
                var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(b.parts.ToArray(), true, true);
                var go = new GameObject(name + " " + b.mat.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = b.mat; Tint(r, b.tint);
            }
            batches.Clear();
        }

        // ---------- the tunnel itself ----------
        static float Fbm(float x, float y)
        {
            float v = 0, a = .5f, f = 1;
            for (int i = 0; i < 4; i++) { v += a * Mathf.PerlinNoise(x * f + 11.3f * i, y * f + 7.1f * i); a *= .5f; f *= 2.1f; }
            return v;   // about 0..1
        }

        /// <summary>The cross-section of the drift: up the left wall, over an arched back, down the right wall.
        /// Returns points and outward normals.</summary>
        static void Profile(float halfW, float wallH, float archH, out List<Vector2> pts, out List<Vector2> nrm)
        {
            pts = new List<Vector2>(); nrm = new List<Vector2>();
            for (int i = 0; i <= 6; i++) { pts.Add(new Vector2(-halfW, wallH * i / 6f)); nrm.Add(new Vector2(-1, 0)); }
            for (int i = 1; i < 12; i++)
            {
                float a = Mathf.PI - Mathf.PI * i / 12f;
                pts.Add(new Vector2(Mathf.Cos(a) * halfW, wallH + Mathf.Sin(a) * archH));
                nrm.Add(new Vector2(Mathf.Cos(a) / halfW, Mathf.Sin(a) / archH).normalized);
            }
            for (int i = 6; i >= 0; i--) { pts.Add(new Vector2(halfW, wallH * i / 6f)); nrm.Add(new Vector2(1, 0)); }
        }

        /// <summary>A rough drift running along +Z from z0 to z1: walls and back in one sub-mesh (rock), the floor in another.
        /// gaps: z ranges where the left (-1) or right (+1) wall is left open for a crosscut.</summary>
        Mesh DriftMesh(float z0, float z1, float halfW, float wallH, float archH, float rough, int seed, List<Vector3> gaps = null, bool capEnd = false)
        {
            Profile(halfW, wallH, archH, out var P, out var N);
            int np = P.Count, nz = Mathf.CeilToInt((z1 - z0) / .3f) + 1;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var wallTris = new List<int>(); var floorTris = new List<int>();
            // arc length along the profile, for the texture
            var arc = new float[np]; for (int k = 1; k < np; k++) arc[k] = arc[k - 1] + Vector2.Distance(P[k], P[k - 1]);
            for (int j = 0; j < nz; j++)
            {
                float z = Mathf.Lerp(z0, z1, j / (float)(nz - 1));
                for (int k = 0; k < np; k++)
                {
                    float low = Mathf.Clamp01(P[k].y / .5f);                          // walls meet the floor cleanly
                    float n = Fbm(z * .55f + seed, k * .45f + seed * .3f);
                    float bulge = (Mathf.PerlinNoise(z * .11f + seed, P[k].x > 0 ? 3 : 9) - .4f) * .35f;
                    float d = (.05f + rough * n + bulge) * low;
                    var p = P[k] + N[k] * Mathf.Max(0, d);
                    v.Add(new Vector3(p.x, p.y, z)); uv.Add(new Vector2(arc[k] / 2.2f, z / 2.2f));
                }
            }
            bool Gap(float z, Vector2 p)
            {
                if (gaps == null) return false;
                foreach (var g in gaps) if (Mathf.Sign(p.x) == g.x && Mathf.Abs(p.x) > halfW * .9f && p.y < g.z && z > g.y - 1.15f && z < g.y + 1.15f) return true;
                return false;
            }
            for (int j = 0; j < nz - 1; j++)
                for (int k = 0; k < np - 1; k++)
                {
                    int a = j * np + k, b = a + 1, c = a + np, d = c + 1;
                    var mid = (P[k] + P[k + 1]) * .5f; float zm = (v[a].z + v[c].z) * .5f;
                    if (Gap(zm, mid)) continue;
                    wallTris.Add(a); wallTris.Add(b); wallTris.Add(c);
                    wallTris.Add(b); wallTris.Add(d); wallTris.Add(c);
                }
            // the floor: a strip across, slightly uneven, a little lower at the sides (the drainage ditch)
            int fBase = v.Count, nf = 9;
            for (int j = 0; j < nz; j++)
            {
                float z = Mathf.Lerp(z0, z1, j / (float)(nz - 1));
                for (int i = 0; i < nf; i++)
                {
                    float x = Mathf.Lerp(-halfW - .05f, halfW + .05f, i / (float)(nf - 1));
                    float edge = Mathf.Abs(x) / halfW;
                    float y = (Fbm(x * .9f + seed, z * .7f) - .5f) * .06f - (edge > .8f ? (edge - .8f) * .25f : 0);
                    v.Add(new Vector3(x, y, z)); uv.Add(new Vector2(x / 2f, z / 2f));
                }
            }
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nf - 1; i++)
                {
                    int a = fBase + j * nf + i, b = a + 1, c = a + nf, d = c + 1;
                    floorTris.Add(a); floorTris.Add(c); floorTris.Add(b);
                    floorTris.Add(b); floorTris.Add(c); floorTris.Add(d);
                }
            if (capEnd)
            {
                // close the far end with a rough rock face
                int cBase = v.Count; float zc = z1;
                v.Add(new Vector3(0, wallH * .6f, zc + .3f)); uv.Add(new Vector2(.5f, .5f));
                for (int k = 0; k < np; k++) { v.Add(new Vector3(P[k].x * 1.05f, P[k].y, zc)); uv.Add(new Vector2(P[k].x / 2, P[k].y / 2)); }
                for (int k = 0; k < np - 1; k++) { wallTris.Add(cBase); wallTris.Add(cBase + 1 + k); wallTris.Add(cBase + 2 + k); }
            }
            var mesh = new Mesh { name = "Drift", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2; mesh.SetTriangles(wallTris, 0); mesh.SetTriangles(floorTris, 1);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        GameObject MeshObject(string n, Transform p, Mesh m, Material[] mats, Vector3 pos, Vector3 euler)
        {
            var go = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(p, false); go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(euler);
            go.GetComponent<MeshFilter>().sharedMesh = m;
            go.GetComponent<MeshRenderer>().sharedMaterials = mats;
            return go;
        }

        Material RockMat => art.mineRock != null ? art.mineRock : art.rock;
        Material FaceMat => art.mineFace != null ? art.mineFace : art.rock;
        Material FloorMat => art.mineFloor != null ? art.mineFloor : art.dirt;
        Material PostMat => art.mineTimber != null ? art.mineTimber : art.timber;
        Material LagMat => art.lagging != null ? art.lagging : art.timber;

        const float CROSS_L = 28f, CROSS_R = 46f, SECTION = 6f;

        void BuildDrift(Transform w)
        {
            var rnd = CC.Rnd(1899);
            float halfW = 2.05f, wallH = 2.6f, archH = .75f;
            // the main drift, with two openings for crosscuts
            var gaps = new List<Vector3> { new Vector3(-1, CROSS_L, 2.45f), new Vector3(1, CROSS_R, 2.45f) };
            // in 6 m sections (same noise, so they join seamlessly), each lit by the lamps nearest it
            for (float z0 = -3f; z0 < DRIFT + .6f - .01f; z0 += SECTION)
            {
                float z1 = Mathf.Min(z0 + SECTION, DRIFT + .6f);
                var drift = DriftMesh(z0, z1, halfW, wallH, archH, .24f, 3, gaps, z1 >= DRIFT + .6f - .01f);
                MeshObject("Drift", w, drift, new[] { Tiled(RockMat, 1, 1), Tiled(FloorMat, 1, 1) }, Vector3.zero, Vector3.zero);
            }
            // the near end behind you, closed
            var back = DriftMesh(-.01f, .01f, halfW, wallH, archH, 0, 5, null, true);
            MeshObject("DriftEnd", w, back, new[] { Tiled(RockMat, 1, 1), Tiled(FloorMat, 1, 1) }, new Vector3(0, 0, -3f), new Vector3(0, 180, 0));
            // the crosscuts: short drifts going off to the side, ending in rock and darkness
            foreach (var g in gaps)
            {
                var cc = DriftMesh(0, 9, 1.2f, 1.95f, .55f, .2f, (int)g.y, null, true);
                MeshObject("Crosscut", w, cc, new[] { Tiled(RockMat, 1, 1), Tiled(FloorMat, 1, 1) }, new Vector3(g.x * (halfW - .15f), 0, g.y), new Vector3(0, g.x * 90, 0));
                // a timber frame round the opening hides the joint
                float x = g.x * 1.95f;
                Add(PrimitiveType.Cube, PostMat, Hex("8a7a68"), new Vector3(x, 1.3f, g.y - 1.3f), new Vector3(.3f, 2.6f, .3f));
                Add(PrimitiveType.Cube, PostMat, Hex("8a7a68"), new Vector3(x, 1.3f, g.y + 1.3f), new Vector3(.3f, 2.6f, .3f));
                Add(PrimitiveType.Cube, PostMat, Hex("8a7a68"), new Vector3(x, 2.65f, g.y), new Vector3(.32f, .32f, 2.9f));
                // a cold lantern hung a way down it, so you can see how far it goes
                var lamp = Box("CrosscutLamp", w, new Vector3(g.x * 5.5f, 1.7f, g.y), new Vector3(.12f, .18f, .12f), art.glow);
                PointLight("CrosscutGlow", lamp.transform, Vector3.zero, new Color(1, .7f, .45f), 1.2f, 3.5f);
            }

            // square sets every metre and a half: posts, cap, wedges, lagging over the back and behind the posts
            var postTint = Hex("9a8a78"); var lagTint = Hex("8a7e70");
            int set = 0;
            for (float z = 1.5f; z < DRIFT - 1.5f; z += 1.5f, set++)
            {
                if (Mathf.Abs(z - CROSS_L) < 1.6f || Mathf.Abs(z - CROSS_R) < 1.6f) continue;
                float lean = (rnd() - .5f) * 3f;
                Add(PrimitiveType.Cube, Tiled(PostMat, 1, 2.5f), postTint, new Vector3(-1.82f, 1.4f, z), new Vector3(.28f, 2.85f, .28f), new Vector3(lean, 0, (rnd() - .5f) * 2));
                Add(PrimitiveType.Cube, Tiled(PostMat, 1, 2.5f), postTint, new Vector3(1.82f, 1.4f, z), new Vector3(.28f, 2.85f, .28f), new Vector3(-lean, 0, (rnd() - .5f) * 2));
                Add(PrimitiveType.Cube, Tiled(PostMat, 3, 1), postTint * .92f, new Vector3(0, 2.95f, z), new Vector3(4.05f, .3f, .3f), new Vector3(0, 0, (rnd() - .5f) * 1.5f));
                Add(PrimitiveType.Cube, PostMat, postTint * .8f, new Vector3(-1.62f, 2.72f, z), new Vector3(.12f, .14f, .26f), new Vector3(0, 0, 30));
                Add(PrimitiveType.Cube, PostMat, postTint * .8f, new Vector3(1.62f, 2.72f, z), new Vector3(.12f, .14f, .26f), new Vector3(0, 0, -30));
                // lagging: boards running from this set to the next
                for (int b = 0; b < 6; b++)
                {
                    if (rnd() < .15f) continue;
                    float bx = -1.6f + b * .64f;
                    Add(PrimitiveType.Cube, Tiled(LagMat, 1, 1.5f), lagTint * (.85f + rnd() * .2f), new Vector3(bx, 3.13f + rnd() * .03f, z + .75f), new Vector3(.24f, .04f, 1.5f), new Vector3(0, (rnd() - .5f) * 3, 0));
                }
                for (int side = -1; side <= 1; side += 2)
                    for (int b = 0; b < 4; b++)
                    {
                        if (rnd() < .2f) continue;
                        Add(PrimitiveType.Cube, Tiled(LagMat, 1.5f, 1), lagTint * (.8f + rnd() * .2f), new Vector3(side * 1.99f, .5f + b * .62f, z + .75f), new Vector3(.04f, .24f, 1.5f), new Vector3(0, (rnd() - .5f) * 3, (rnd() - .5f) * 3));
                    }
                // a lantern every fourth set
                if (set % 4 == 0)
                {
                    var lantern = Box("Lantern", w, new Vector3(set % 8 == 0 ? -1.45f : 1.45f, 2.55f, z + .2f), new Vector3(.14f, .2f, .14f), art.glow);
                    Box("LanternHook", w, new Vector3(set % 8 == 0 ? -1.45f : 1.45f, 2.73f, z + .2f), new Vector3(.02f, .16f, .02f), art.steel, Hex("1a1a1a"));
                    if (lanterns.Count < 9) lanterns.Add(PointLight("LanternLight", lantern.transform, Vector3.zero, new Color(1, .74f, .48f), 2.8f, 7f));
                    Glow("LanternHalo", w, new Vector3(set % 8 == 0 ? -1.45f : 1.45f, 2.55f, z + .1f), .7f, new Color(1, .75f, .45f, .5f));
                }
            }

            // the track: ballast, ties, spiked rails
            for (float z = 0; z < DRIFT; z += 2) Add(PrimitiveType.Cube, Tiled(FloorMat, 1, 1.2f), Hex("6a6258"), new Vector3(1, .02f, z + 1), new Vector3(1.6f, .06f, 2.02f));
            for (float z = .4f; z < DRIFT; z += .55f)
            {
                Add(PrimitiveType.Cube, Tiled(PostMat, 2, 1), Hex("5a4a3c") * (.85f + rnd() * .25f), new Vector3(1 + (rnd() - .5f) * .06f, .06f, z), new Vector3(1.15f, .07f, .16f), new Vector3(0, (rnd() - .5f) * 5, 0));
                foreach (float rx in new[] { .65f, 1.35f })
                    foreach (float s in new[] { -.06f, .06f })
                        Add(PrimitiveType.Cube, art.rail, Hex("2a2622"), new Vector3(rx + s, .105f, z), new Vector3(.025f, .03f, .025f));
            }
            foreach (float rx in new[] { .65f, 1.35f })
            {
                for (float z = 0; z < DRIFT; z += SECTION)
                {
                    float len = Mathf.Min(SECTION, DRIFT - z), zc = z + len / 2;
                    Add(PrimitiveType.Cube, art.rail, Hex("5a5048"), new Vector3(rx, .1f, zc), new Vector3(.09f, .015f, len));     // base
                    Add(PrimitiveType.Cube, art.rail, Hex("5a5048"), new Vector3(rx, .125f, zc), new Vector3(.02f, .04f, len));     // web
                    Add(PrimitiveType.Cube, art.rail, Hex("8a8078"), new Vector3(rx, .155f, zc), new Vector3(.05f, .03f, len));     // head, worn bright
                }
            }

            // the air line along the right, hung from the caps, and the water line low on the left
            for (float z = 0; z < DRIFT; z += SECTION)
            {
                float len = Mathf.Min(SECTION, DRIFT - z), zc = z + len / 2;
                Add(PrimitiveType.Cylinder, art.steel, Hex("3a3634"), new Vector3(1.55f, 2.62f, zc), new Vector3(.14f, len / 2, .14f), new Vector3(90, 0, 0));
                Add(PrimitiveType.Cylinder, art.steel, Hex("3a3634"), new Vector3(-1.62f, .32f, zc), new Vector3(.08f, len / 2, .08f), new Vector3(90, 0, 0));
                Add(PrimitiveType.Cube, art.steel, Hex("1a1a1a"), new Vector3(-.3f, 3.05f, zc), new Vector3(.01f, .01f, len));
            }
            for (float z = 3; z < DRIFT; z += 6)
            {
                Add(PrimitiveType.Cylinder, art.steel, Hex("2a2624"), new Vector3(1.55f, 2.62f, z), new Vector3(.22f, .03f, .22f), new Vector3(90, 0, 0));
                Add(PrimitiveType.Cube, art.steel, Hex("1a1816"), new Vector3(1.55f, 2.74f, z + .75f), new Vector3(.02f, .2f, .02f));
            }
            // a bell wire along the back, and the odd rock fallen from it
            for (int i = 0; i < 40; i++)
            {
                float z = 2 + rnd() * (DRIFT - 4), x = (rnd() < .5f ? -1 : 1) * (1.45f + rnd() * .3f), s = .08f + rnd() * .22f;
                Add(PrimitiveType.Cube, RockMat, new Color(1.5f, 1.45f, 1.4f) * (.8f + rnd() * .3f), new Vector3(x, s * .35f, z), new Vector3(s * 1.3f, s * .8f, s), new Vector3(rnd() * 40, rnd() * 360, rnd() * 40));
            }
            Flush(w, "DriftParts");

            // puddles in the low places, black and shining
            for (int i = 0; i < 9; i++)
            {
                float z = 5 + i * 6.5f + rnd() * 3, x = rnd() < .6f ? -1.2f + rnd() * .6f : 1.75f;
                var pd = Cyl("Puddle", w, new Vector3(x, .015f, z), new Vector3(.5f + rnd() * .6f, .003f, .9f + rnd() * 1.2f), art.water != null ? art.water : art.steel, art.water != null ? (Color?)null : Hex("101012"));
                pd.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
