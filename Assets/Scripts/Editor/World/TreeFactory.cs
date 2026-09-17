using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Core;

namespace Height1079.EditorTools.World
{
    /// <summary>Game-ready procedural trees of the Northern Urals taiga edge (Picea obovata, Abies sibirica, Betula pubescens, Pinus sibirica).
    /// Studio pattern: one generator, several seeded variants per species, three LODs, saved as mesh assets + prefabs that the terrain scatters.
    /// Budget: LOD0 ≈ 3–7k triangles, LOD1 ≈ 1k, LOD2 ≈ 60.</summary>
    public static class TreeFactory
    {
        const string MeshDir = WorldPaths.Generated + "/Meshes/Trees";
        const string PrefabDir = WorldPaths.Generated + "/Prefabs/Trees";
        public const float PrototypeHeight = 16f, BirchPrototypeHeight = 13f;

        // submeshes: 0 bark, 1 foliage cards, 2 snow cards
        static Material foliageSpruce, foliageFir, foliagePine, twigs, snowCard, farSpruce, farBirch;

        static void EnsureMaterials()
        {
            foliageSpruce = Materials.Get("NeedlesSpruce", Color.white, TextureFactory.NeedleSpray("needles_spruce", 3, 9f, new Color(.07f, .16f, .12f), new Color(.2f, .32f, .22f), false), null, .05f, true, .42f);
            foliageFir = Materials.Get("NeedlesFir", Color.white, TextureFactory.NeedleSpray("needles_fir", 5, 11f, new Color(.08f, .19f, .13f), new Color(.24f, .38f, .25f), false), null, .08f, true, .42f);
            foliagePine = Materials.Get("NeedlesSiberianPine", Color.white, TextureFactory.NeedleSpray("needles_siberian_pine", 9, 22f, new Color(.08f, .18f, .14f), new Color(.27f, .38f, .3f), true), null, .08f, true, .4f);
            twigs = Materials.Get("BirchTwigs", Color.white, TextureFactory.Twigs("birch_twigs", 13), null, .02f, true, .35f);
            snowCard = Materials.Get("SnowOnBranches", Color.white, TextureFactory.SnowCard("snow_card", 17), null, .3f, true, .5f);
            farSpruce = Materials.Get("FarConifer", new Color(.13f, .2f, .17f), smoothness: .02f);
            farBirch = Materials.Get("FarBirch", new Color(.42f, .36f, .34f), smoothness: .02f);
        }

        public enum Form { Spruce, Fir, SiberianPine }

        public sealed class ConiferSpec
        {
            public Form Form;
            public float Height = PrototypeHeight, CrownBase = .12f, TrunkRadius = .22f, MaxBranch = 2.4f, Whorl = .42f;
            public int PerWhorl = 5;
            public float Droop = -18f, TipLift = 12f, CardWidth = .7f, SnowAmount = .8f;
            public int Seed;
            public int Leaders = 1;
            /// <summary>Documented damage (the cedar): branches below this height on the given azimuth sector are broken stubs.</summary>
            public float BrokenBelow = 0f; public Vector2 BrokenDirection; public float BrokenHalfAngle = 70f;
            public float DeadBelow = 0f;
        }

        static float Profile(Form f, float rel)
        {
            switch (f)
            {
                case Form.Fir: return Mathf.Pow(1 - rel, 1.15f) * .75f + .04f;
                case Form.SiberianPine: return Mathf.Sin(Mathf.PI * Mathf.Lerp(.25f, 1f, rel)) * .95f * (1 - rel * .15f) + .05f;
                default: return Mathf.Pow(1 - rel, .95f) * .95f + .04f;
            }
        }

        /// <summary>LOD0/LOD1 conifer. <paramref name="detail"/> 1 = full, 0.4 = reduced (fewer whorls, wider cards).</summary>
        public static MeshBuilder Conifer(ConiferSpec s, float detail)
        {
            var rnd = new System.Random(s.Seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MeshBuilder(3);
            float H = s.Height;
            int trunkSides = detail > .7f ? 9 : 5;
            // trunk in 3 bends
            var trunk = new List<Vector3> { Vector3.zero };
            for (int i = 1; i <= 4; i++) trunk.Add(new Vector3((R() - .5f) * .25f * i / 4, H * i / 4f, (R() - .5f) * .25f * i / 4));
            for (int i = 0; i < 4; i++)
            {
                float r0 = s.TrunkRadius * Mathf.Lerp(1f, .05f, i / 4f), r1 = s.TrunkRadius * Mathf.Lerp(1f, .05f, (i + 1) / 4f);
                if (i == 0) { mb.Tube(0, trunk[0] + Vector3.down * .4f, trunk[0] + Vector3.up * .3f, r0 * 1.35f, r0 * 1.05f, trunkSides, 1f); }
                mb.Tube(0, trunk[i], trunk[i + 1], r0, r1, trunkSides, 1f, H * i / 4f, i == 3);
            }
            Vector3 TrunkAt(float y) { float t = Mathf.Clamp01(y / H) * 4; int k = Mathf.Min(3, (int)t); return Vector3.Lerp(trunk[k], trunk[k + 1], t - k); }

            float whorl = s.Whorl / Mathf.Lerp(.45f, 1f, detail);
            float baseY = H * s.CrownBase;
            float az = R() * 360;
            for (float y = baseY; y < H - .35f; y += whorl * (.8f + .4f * R()))
            {
                float rel = (y - baseY) / (H - baseY);
                int count = Mathf.Max(3, Mathf.RoundToInt(s.PerWhorl * Mathf.Lerp(.7f, 1f, detail) * (s.Form == Form.SiberianPine && rel < .3f ? 1.3f : 1f)));
                for (int b = 0; b < count; b++)
                {
                    az += 360f / count + (R() - .5f) * 25f + 137.5f / count;
                    var dir = Quaternion.Euler(0, az, 0) * Vector3.forward;
                    float len = s.MaxBranch * Profile(s.Form, rel) * (.8f + .35f * R());
                    Vector3 root = TrunkAt(y);
                    bool broken = s.BrokenBelow > 0 && y < s.BrokenBelow && Vector2.Angle(new Vector2(dir.x, dir.z), s.BrokenDirection) < s.BrokenHalfAngle;
                    bool dead = y < s.DeadBelow;
                    if (broken)
                    {
                        // broken stub: 10–35 cm of wood with a splintered end, no needles
                        float stub = .1f + .25f * R();
                        mb.Tube(0, root, root + (dir + Vector3.up * .15f).normalized * stub, .045f + .03f * (1 - rel), .02f, 5, 2f, 0, true);
                        continue;
                    }
                    float pitch = s.Droop + (s.Form == Form.SiberianPine ? 30f * (1 - rel) + 10f : 0f) + (R() - .5f) * 10f + rel * 25f;
                    Vector3 d0 = Quaternion.AngleAxis(-pitch, Vector3.Cross(Vector3.up, dir)) * dir;
                    Vector3 mid = root + d0 * len * .55f;
                    Vector3 d1 = Quaternion.AngleAxis(-(pitch + s.TipLift), Vector3.Cross(Vector3.up, dir)) * dir;
                    Vector3 tip = mid + d1 * len * .45f;
                    float br = Mathf.Lerp(.06f, .018f, rel) * (s.Form == Form.SiberianPine ? 1.4f : 1f);
                    mb.Tube(0, root, mid, br, br * .6f, 4, 2f);
                    mb.Tube(0, mid, tip, br * .6f, br * .15f, 3, 2f);
                    if (dead) continue;
                    float w = s.CardWidth * Mathf.Lerp(.7f, 1.3f, Mathf.Clamp01(len / s.MaxBranch)) * Mathf.Lerp(1.5f, 1f, detail);
                    Card(mb, root + d0 * len * .12f, tip + d1 * .15f, w, detail);
                    if (s.SnowAmount > 0 && R() < s.SnowAmount * (1 - rel * .5f)) SnowOn(mb, root + d0 * len * .2f, tip, w * .55f);
                }
            }
            // leaders
            for (int l = 0; l < s.Leaders; l++)
            {
                Vector3 top = TrunkAt(H * .97f);
                Vector3 lead = top + new Vector3((R() - .5f) * (l > 0 ? 1.4f : .1f), 1.1f, (R() - .5f) * (l > 0 ? 1.4f : .1f));
                Card(mb, top - Vector3.up * .5f, lead, s.CardWidth * .6f, detail, true);
            }
            return mb;
        }

        /// <summary>A needle/twig card along a branch: a flat spray plus a second one tilted 60° so it reads from the side.</summary>
        static void Card(MeshBuilder mb, Vector3 a, Vector3 b, float width, float detail, bool vertical = false)
        {
            Vector3 along = (b - a); if (along.sqrMagnitude < 1e-4f) return;
            Vector3 flat = vertical ? Vector3.right : Vector3.Cross(along.normalized, Vector3.up).normalized;
            if (flat.sqrMagnitude < .1f) flat = Vector3.right;
            Vector3 up = Vector3.Cross(flat, along.normalized).normalized;
            if (up.y < 0) up = -up;
            void Plane(Vector3 side, Vector3 normal)
            {
                Vector3 h = side * width * .5f;
                mb.Quad(1, a - h, a + h, b + h * .55f, b - h * .55f, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), true, normal);
            }
            Plane(flat, (up + Vector3.up).normalized);
            if (detail > .3f) Plane(Quaternion.AngleAxis(60, along) * flat, (up + Vector3.up * .5f).normalized);
            if (detail > .9f) Plane(Quaternion.AngleAxis(-60, along) * flat, (up + Vector3.up * .5f).normalized);
        }

        static void SnowOn(MeshBuilder mb, Vector3 a, Vector3 b, float width)
        {
            Vector3 along = b - a; if (along.sqrMagnitude < 1e-4f) return;
            Vector3 side = Vector3.Cross(along.normalized, Vector3.up).normalized * width * .5f;
            Vector3 lift = Vector3.up * .06f;
            // single-sided, facing the sky
            mb.Quad(2, a - side + lift, a + side + lift, b + side * .4f + lift, b - side * .4f + lift, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), false, Vector3.up);
        }

        /// <summary>Far LOD: two stacked cones with a trunk base (~60 triangles).</summary>
        static MeshBuilder FarConifer(ConiferSpec s)
        {
            var mb = new MeshBuilder(3);
            float r = s.MaxBranch * (s.Form == Form.SiberianPine ? .95f : .8f);
            float cb = s.Height * s.CrownBase;
            mb.Tube(0, Vector3.zero, Vector3.up * (cb + .5f), s.TrunkRadius, s.TrunkRadius * .8f, 4);
            mb.Cone(1, Vector3.up * cb, r, (s.Height - cb) * .62f, 7);
            mb.Cone(1, Vector3.up * (cb + (s.Height - cb) * .42f), r * .62f, (s.Height - cb) * .58f, 7);
            return mb;
        }

        public sealed class BirchSpec { public float Height = BirchPrototypeHeight, TrunkRadius = .14f; public int Seed; public bool Crooked; }

        /// <summary>Downy birch in winter: leafless, chalk-white trunk, upward main limbs, fine twig cards.</summary>
        public static MeshBuilder Birch(BirchSpec s, float detail)
        {
            var rnd = new System.Random(s.Seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MeshBuilder(3);
            float H = s.Height;
            Vector3 lean = new Vector3((R() - .5f) * (s.Crooked ? .9f : .35f), 0, (R() - .5f) * (s.Crooked ? .9f : .35f));
            Vector3 p0 = Vector3.down * .3f, p1 = new Vector3(lean.x * .3f, H * .35f, lean.z * .3f), p2 = new Vector3(lean.x, H * .7f, lean.z), p3 = new Vector3(lean.x * 1.2f, H, lean.z * 1.2f);
            int sides = detail > .7f ? 8 : 5;
            mb.Tube(0, p0, p1, s.TrunkRadius * 1.15f, s.TrunkRadius * .8f, sides, .5f);
            mb.Tube(0, p1, p2, s.TrunkRadius * .8f, s.TrunkRadius * .45f, sides, .5f, H * .35f);
            mb.Tube(0, p2, p3, s.TrunkRadius * .45f, .01f, sides, .5f, H * .7f, true);
            int limbs = Mathf.RoundToInt(Mathf.Lerp(5, 11, detail));
            for (int i = 0; i < limbs; i++)
            {
                float t = Mathf.Lerp(.38f, .92f, i / (float)limbs) + (R() - .5f) * .05f;
                Vector3 root = t < .7f ? Vector3.Lerp(p1, p2, (t - .35f) / .35f) : Vector3.Lerp(p2, p3, (t - .7f) / .3f);
                float az = i * 137.5f + R() * 30;
                Vector3 dir = (Quaternion.Euler(0, az, 0) * Vector3.forward * .75f + Vector3.up * (.9f + .4f * R())).normalized;
                float len = H * .32f * (1.1f - t) * (.8f + .4f * R()) + .6f;
                Vector3 end = root + dir * len;
                mb.Tube(0, root, end, s.TrunkRadius * .28f * (1.1f - t), .012f, detail > .7f ? 4 : 3, 1f, 0, false);
                if (detail < .3f) continue;
                int twigsN = detail > .7f ? 4 : 2;
                for (int k = 0; k < twigsN; k++)
                {
                    Vector3 a = Vector3.Lerp(root, end, .35f + .65f * k / twigsN);
                    Vector3 tdir = (dir + new Vector3(R() - .5f, R() * .3f - .35f, R() - .5f)).normalized;
                    Card(mb, a, a + tdir * (1.1f + R() * .9f), 1.1f, detail);
                }
            }
            return mb;
        }

        static MeshBuilder FarBirch(BirchSpec s)
        {
            var mb = new MeshBuilder(3);
            mb.Tube(0, Vector3.zero, Vector3.up * s.Height * .75f, s.TrunkRadius, .02f, 4);
            mb.Cone(1, Vector3.up * s.Height * .38f, s.Height * .22f, s.Height * .66f, 6);
            return mb;
        }

        static Mesh SaveMesh(MeshBuilder mb, string name)
        {
            Directory.CreateDirectory(MeshDir);
            string path = $"{MeshDir}/{name}.asset";
            var mesh = mb.ToMesh(name);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Prefab with LODGroup (0: full, 1: reduced, 2: cones) and a trunk capsule collider.</summary>
        public static GameObject SavePrefab(string name, MeshBuilder lod0, MeshBuilder lod1, MeshBuilder lod2, Material bark, Material foliage, Material farFoliage, float trunkRadius, float height)
        {
            Directory.CreateDirectory(PrefabDir);
            var root = new GameObject(name);
            var renderers = new List<Renderer[]>();
            int i = 0;
            foreach (var mb in new[] { lod0, lod1, lod2 })
            {
                var go = new GameObject($"{name}_LOD{i}", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mb, $"{name}_LOD{i}");
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterials = i < 2 ? new[] { bark, foliage, snowCard } : new[] { bark, farFoliage, snowCard };
                mr.shadowCastingMode = i == 2 ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                renderers.Add(new Renderer[] { mr });
                i++;
            }
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(.22f, renderers[0]), new LOD(.07f, renderers[1]), new LOD(.012f, renderers[2]) });
            lg.fadeMode = LODFadeMode.None;
            lg.RecalculateBounds();
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = trunkRadius * 1.1f; col.height = height * .6f; col.center = new Vector3(0, height * .3f, 0);
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public struct Prototype { public GameObject Prefab; public TreeSpecies Species; public float Height; }

        /// <summary>Builds the species library: 4 spruce, 3 fir, 3 Siberian pine, 4 birch (2 crooked tree-line forms).</summary>
        public static List<Prototype> BuildLibrary()
        {
            EnsureMaterials();
            var list = new List<Prototype>();
            var bark = Materials.Bark; var cedarBark = Materials.BarkCedar; var birchBark = Materials.BirchBark;
            for (int v = 0; v < 4; v++)
            {
                var s = new ConiferSpec { Form = Form.Spruce, Seed = 100 + v, MaxBranch = 2.2f + .25f * v, PerWhorl = 5, Droop = -22f, TipLift = 18f, CardWidth = .75f, CrownBase = .06f + .03f * v, SnowAmount = .85f };
                list.Add(new Prototype { Species = TreeSpecies.Spruce, Height = s.Height, Prefab = SavePrefab($"Spruce_{v}", Conifer(s, 1), Conifer(s, .4f), FarConifer(s), bark, foliageSpruce, farSpruce, s.TrunkRadius, s.Height) });
            }
            for (int v = 0; v < 3; v++)
            {
                var s = new ConiferSpec { Form = Form.Fir, Seed = 200 + v, MaxBranch = 1.7f + .2f * v, PerWhorl = 5, Droop = -8f, TipLift = 6f, CardWidth = .62f, TrunkRadius = .18f, CrownBase = .05f, SnowAmount = .9f };
                list.Add(new Prototype { Species = TreeSpecies.Fir, Height = s.Height, Prefab = SavePrefab($"Fir_{v}", Conifer(s, 1), Conifer(s, .4f), FarConifer(s), bark, foliageFir, farSpruce, s.TrunkRadius, s.Height) });
            }
            for (int v = 0; v < 3; v++)
            {
                var s = new ConiferSpec { Form = Form.SiberianPine, Seed = 300 + v, MaxBranch = 3.2f + .3f * v, PerWhorl = 6, Droop = -5f, TipLift = 25f, CardWidth = .95f, TrunkRadius = .3f, CrownBase = .2f, Whorl = .55f, Leaders = 1 + v, SnowAmount = .75f };
                list.Add(new Prototype { Species = TreeSpecies.SiberianPine, Height = s.Height, Prefab = SavePrefab($"SiberianPine_{v}", Conifer(s, 1), Conifer(s, .4f), FarConifer(s), cedarBark, foliagePine, farSpruce, s.TrunkRadius, s.Height) });
            }
            for (int v = 0; v < 4; v++)
            {
                var s = new BirchSpec { Seed = 400 + v, Crooked = v >= 2, TrunkRadius = v >= 2 ? .11f : .15f };
                list.Add(new Prototype { Species = TreeSpecies.Birch, Height = s.Height, Prefab = SavePrefab($"Birch_{v}", Birch(s, 1), Birch(s, .4f), FarBirch(s), birchBark, twigs, farBirch, s.TrunkRadius, s.Height) });
            }
            return list;
        }

        /// <summary>The hero cedar (Siberian pine) at the KAN point: 18 m from the canopy model, a thick old trunk, branches broken up to 4.5 m on the side facing the tent.</summary>
        public static GameObject HeroCedar(Vector2 towardTent)
        {
            EnsureMaterials();
            var s = new ConiferSpec
            {
                Form = Form.SiberianPine, Seed = 1959, Height = Sites.Cedar.Height, TrunkRadius = .32f, MaxBranch = 4.2f, PerWhorl = 7, Whorl = .5f,
                Droop = -4f, TipLift = 28f, CardWidth = 1.05f, CrownBase = .1f, Leaders = 3, SnowAmount = .6f,
                BrokenBelow = Sites.Cedar.BrokenUpTo, BrokenDirection = towardTent.normalized, BrokenHalfAngle = 75f, DeadBelow = 2.2f,
            };
            return SavePrefab("HeroCedar", Conifer(s, 1), Conifer(s, .55f), FarConifer(s), Materials.BarkCedar, foliagePine, farSpruce, s.TrunkRadius, s.Height);
        }

        /// <summary>Birch by which Dyatlov was found (protocol: head 5–7 cm behind its trunk, arm on its branch).</summary>
        public static GameObject LoneBirch(string name, int seed)
        {
            EnsureMaterials();
            var s = new BirchSpec { Seed = seed, Height = 6.5f, TrunkRadius = .1f, Crooked = true };
            return SavePrefab(name, Birch(s, 1), Birch(s, .4f), FarBirch(s), Materials.BirchBark, twigs, farBirch, s.TrunkRadius, s.Height);
        }

        /// <summary>KAN landmark "triple tree": three spruce stems from one root.</summary>
        public static GameObject TripleSpruce()
        {
            EnsureMaterials();
            var all = new MeshBuilder[3];
            for (int lod = 0; lod < 3; lod++)
            {
                var mb = new MeshBuilder(3);
                for (int k = 0; k < 3; k++)
                {
                    var s = new ConiferSpec { Form = Form.Spruce, Seed = 500 + k, Height = 12f - k * 1.5f, MaxBranch = 1.6f, TrunkRadius = .14f, CrownBase = .15f };
                    var part = lod == 2 ? FarConifer(s) : Conifer(s, lod == 0 ? 1 : .4f);
                    mb.Append(part, Matrix4x4.TRS(Quaternion.Euler(0, k * 120, 0) * new Vector3(.35f, 0, 0), Quaternion.Euler(0, k * 120, (k - 1) * 6f), Vector3.one));
                }
                all[lod] = mb;
            }
            return SavePrefab("TripleSpruce", all[0], all[1], all[2], Materials.Bark, foliageSpruce, farSpruce, .5f, 12f);
        }
    }
}
