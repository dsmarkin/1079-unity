using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Core;

namespace Height1079.EditorTools.World
{
    /// <summary>Procedural winter forest of the Northern Urals (docs/MAP.md, «Лес»): Siberian spruce, Siberian fir, Siberian pine (cedar),
    /// Siberian larch, downy birch; tree-line forms and dead snags; understory (young conifers, rowan, willow, juniper, dwarf birch,
    /// crooked birch clumps, windfall and buried hummocks).
    /// Every tree: dense needle sprays with side shoots, dead twigs and hanging lichen below the crown, snow pillows (кухта) on the
    /// branches and a snow skirt where the lower branches are buried under ~1.2 m of snow. Three LODs, saved as meshes + prefabs.
    /// Submeshes: 0 bark, 1 foliage/twig cards, 2 snow, 3 lichen.</summary>
    public static partial class TreeFactory
    {
        const string MeshDir = WorldPaths.Generated + "/Meshes/Trees";
        const string PrefabDir = WorldPaths.Generated + "/Prefabs/Trees";
        public const float PrototypeHeight = 16f, BirchPrototypeHeight = 13f;
        const int SubBark = 0, SubLeaf = 1, SubSnow = 2, SubLichen = 3, Subs = 4;

        static Material foliageSpruce, foliageFir, foliagePine, foliageJuniper, twigs, twigsLarch, twigsWillow, twigsRowan, snowCard, snowLumps, lichen,
            farSpruce, farBirch, farLarch, barkDead, barkFir, barkLarch, barkRowan;

        static void EnsureMaterials()
        {
            foliageSpruce = Materials.Get("NeedlesSpruce", Color.white, TextureFactory.NeedleSpray("needles_spruce", 3, 9f, new Color(.05f, .12f, .09f), new Color(.16f, .27f, .19f), false), null, .05f, true, .42f);
            foliageFir = Materials.Get("NeedlesFir", Color.white, TextureFactory.NeedleSpray("needles_fir", 5, 11f, new Color(.07f, .17f, .12f), new Color(.22f, .36f, .24f), false), null, .08f, true, .42f);
            foliagePine = Materials.Get("NeedlesSiberianPine", Color.white, TextureFactory.NeedleSpray("needles_siberian_pine", 9, 22f, new Color(.08f, .18f, .14f), new Color(.27f, .38f, .3f), true), null, .08f, true, .4f);
            foliageJuniper = Materials.Get("NeedlesJuniper", Color.white, TextureFactory.NeedleSpray("needles_juniper", 21, 6f, new Color(.07f, .12f, .11f), new Color(.2f, .28f, .27f), false), null, .05f, true, .42f);
            twigs = Materials.Get("BirchTwigs", Color.white, TextureFactory.Twigs("birch_twigs", 13, new Color(.3f, .22f, .21f), 3, 0f), null, .02f, true, .35f);
            twigsLarch = Materials.Get("LarchTwigs", Color.white, TextureFactory.Twigs("larch_twigs", 31, new Color(.36f, .31f, .26f), 2, .7f), null, .02f, true, .35f);
            twigsWillow = Materials.Get("WillowTwigs", Color.white, TextureFactory.Twigs("willow_twigs", 33, new Color(.42f, .22f, .14f), 1, 0f), null, .05f, true, .35f);
            twigsRowan = Materials.Get("RowanTwigs", Color.white, TextureFactory.Twigs("rowan_twigs", 35, new Color(.34f, .3f, .29f), 1, .15f), null, .05f, true, .35f);
            snowCard = Materials.Get("SnowOnBranches", Color.white, TextureFactory.SnowCard("snow_card", 17), null, .3f, true, .5f);
            snowLumps = Materials.PH("SnowOnTrees", "snow_02", new Color(.86f, .88f, .93f), 1f, .15f, .5f);
            lichen = Materials.Get("BeardLichen", Color.white, TextureFactory.Beard("beard_lichen", 41), null, .02f, true, .4f);
            farSpruce = Materials.Get("FarConifer", new Color(.1f, .15f, .13f), smoothness: .02f);
            farBirch = Materials.Get("FarBirch", new Color(.36f, .33f, .33f), smoothness: .02f);
            farLarch = Materials.Get("FarLarch", new Color(.33f, .3f, .27f), smoothness: .02f);
            barkDead = Materials.PH("BarkDead", "pine_bark", new Color(.62f, .6f, .58f), 1f);
            barkFir = Materials.PH("BarkFir", "pine_bark", new Color(.62f, .62f, .6f), 1f, .1f, .4f);
            barkLarch = Materials.PH("BarkLarch", "pine_bark", new Color(.82f, .66f, .58f), 1f);
            barkRowan = Materials.Get("BarkRowan", new Color(.34f, .31f, .29f), smoothness: .15f);
        }

        public enum Form { Spruce, Fir, SiberianPine, Larch }

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
            /// <summary>Snow depth: no live foliage below it, a snow skirt over the buried lower branches.</summary>
            public float Buried = 1.1f;
            /// <summary>Side shoots per branch (density of the crown).</summary>
            public int Shoots = 2;
            /// <summary>0..1 hanging beard lichen on the lower crown and dead twigs.</summary>
            public float Lichen = .35f;
            /// <summary>Tree-line form: wind-flagged crown (branches on the leeward side, +Z), thin top, dense skirt.</summary>
            public bool Flagged;
            /// <summary>Standing dead tree: no needles, broken top, many bare twigs.</summary>
            public bool Dead;
            /// <summary>Dead twigs on the bare trunk below the crown (old spruce keeps them for decades).</summary>
            public bool DeadTwigs = true;
        }

        static float Profile(Form f, float rel)
        {
            switch (f)
            {
                case Form.Fir: return Mathf.Pow(1 - rel, 1.25f) * .72f + .03f;
                case Form.SiberianPine: return Mathf.Sin(Mathf.PI * Mathf.Lerp(.25f, 1f, rel)) * .95f * (1 - rel * .15f) + .05f;
                case Form.Larch: return Mathf.Pow(1 - rel, 1.05f) * .85f + .04f;
                default: return Mathf.Pow(1 - rel, .9f) * .95f + .04f;
            }
        }

        /// <summary>LOD0/LOD1 conifer. <paramref name="detail"/> 1 = full, 0.4 = reduced (fewer whorls, wider cards, no lichen).</summary>
        public static MeshBuilder Conifer(ConiferSpec s, float detail)
        {
            var rnd = new System.Random(s.Seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MeshBuilder(Subs);
            float H = s.Height;
            bool full = detail > .7f;
            int trunkSides = full ? 9 : 5;
            float topAt = s.Dead ? H * (.62f + .25f * R()) : H;   // snags lose their top
            // trunk in 4 bends
            var trunk = new List<Vector3> { Vector3.zero };
            float lean = s.Flagged ? .5f : .25f;
            for (int i = 1; i <= 4; i++) trunk.Add(new Vector3((R() - .5f) * lean * i / 4, H * i / 4f, (R() - .5f) * lean * i / 4 + (s.Flagged ? .1f * i : 0)));
            for (int i = 0; i < 4; i++)
            {
                float y0 = H * i / 4f, y1 = H * (i + 1) / 4f;
                if (y0 >= topAt) break;
                float r0 = s.TrunkRadius * Mathf.Lerp(1f, .05f, i / 4f), r1 = s.TrunkRadius * Mathf.Lerp(1f, .05f, (i + 1) / 4f);
                var b = trunk[i + 1];
                bool last = y1 >= topAt || i == 3;
                if (y1 > topAt) { b = Vector3.Lerp(trunk[i], trunk[i + 1], (topAt - y0) / (y1 - y0)); r1 = Mathf.Lerp(r0, r1, (topAt - y0) / (y1 - y0)); }
                if (i == 0) mb.Tube(SubBark, trunk[0] + Vector3.down * .4f, trunk[0] + Vector3.up * .3f, r0 * 1.35f, r0 * 1.05f, trunkSides, 1f);
                mb.Tube(SubBark, trunk[i], b, r0, r1, trunkSides, 1f, y0, last);
                if (s.Dead && last)
                {
                    // splintered break
                    for (int k = 0; k < 4; k++)
                    {
                        var d = Quaternion.Euler(0, k * 90 + R() * 40, 0) * Vector3.forward * r1 * .6f;
                        mb.Tube(SubBark, b + d, b + d * .4f + Vector3.up * (.2f + .5f * R()), r1 * .35f, .005f, 3, 1f);
                    }
                    break;
                }
            }
            Vector3 TrunkAt(float y) { float t = Mathf.Clamp01(y / H) * 4; int k = Mathf.Min(3, (int)t); return Vector3.Lerp(trunk[k], trunk[k + 1], t - k); }
            float TrunkR(float y) => s.TrunkRadius * Mathf.Lerp(1f, .05f, Mathf.Clamp01(y / H));

            float whorl = s.Whorl / Mathf.Lerp(.45f, 1f, detail);
            float baseY = Mathf.Max(H * s.CrownBase, s.Buried * .75f);
            float az = R() * 360;
            Vector3 lee = Vector3.forward; // flagged trees: branches survive on the leeward side (+Z; the importer turns the tree)

            // dead twigs and lichen on the bare trunk below the crown
            if (s.DeadTwigs && full)
            {
                for (float y = s.Buried; y < baseY; y += .28f + .2f * R())
                {
                    int n = 2 + rnd.Next(3);
                    for (int b = 0; b < n; b++)
                    {
                        var dir = Quaternion.Euler(0, R() * 360, 0) * Vector3.forward;
                        var root = TrunkAt(y) + dir * TrunkR(y) * .8f;
                        if (R() < .35f) continue;
                        float len = (.15f + .7f * R() * R()) * (1f - .5f * y / (baseY + 1f));
                        var tip = root + (dir + Vector3.down * (.15f + .6f * R()) + new Vector3(R() - .5f, 0, R() - .5f) * .5f).normalized * len;
                        mb.Tube(SubBark, root, tip, .012f, .003f, 3, 2f);
                        if (R() < s.Lichen) Beard(mb, tip, .3f + .4f * R(), .25f);
                    }
                }
            }

            float ceiling = s.Dead ? topAt - .2f : H - .35f;
            for (float y = baseY; y < ceiling; y += whorl * (.8f + .4f * R()))
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
                    if (s.Flagged)
                    {
                        // windward branches are killed above the snow; the skirt under the snow line stays full
                        float windward = Vector3.Dot(dir, lee);
                        bool inSkirt = y < s.Buried + 1.2f;
                        if (!inSkirt && windward < -.1f) broken = R() < .75f;
                        if (!inSkirt) len *= Mathf.Lerp(.35f, 1.15f, (windward + 1) * .5f) * Mathf.Lerp(1f, .6f, rel);
                        else len *= 1.15f;
                    }
                    if (broken)
                    {
                        float stub = .1f + .25f * R();
                        mb.Tube(SubBark, root, root + (dir + Vector3.up * .15f).normalized * stub, .045f + .03f * (1 - rel), .02f, 5, 2f, 0, true);
                        continue;
                    }
                    bool dead = s.Dead || y < s.DeadBelow;
                    float pitch = s.Droop + (s.Form == Form.SiberianPine ? 30f * (1 - rel) + 10f : 0f) + (R() - .5f) * 10f + rel * 25f;
                    if (s.Form == Form.Larch) pitch = -4f + rel * 20f + (R() - .5f) * 12f;
                    Vector3 axis = Vector3.Cross(Vector3.up, dir);
                    Vector3 d0 = Quaternion.AngleAxis(-pitch, axis) * dir;
                    Vector3 mid = root + d0 * len * .55f;
                    Vector3 d1 = Quaternion.AngleAxis(-(pitch + s.TipLift), axis) * dir;
                    Vector3 tip = mid + d1 * len * .45f;
                    float br = Mathf.Lerp(.06f, .018f, rel) * (s.Form == Form.SiberianPine ? 1.4f : 1f);
                    mb.Tube(SubBark, root, mid, br, br * .6f, full ? 4 : 3, 2f);
                    mb.Tube(SubBark, mid, tip, br * .6f, br * .15f, 3, 2f);
                    if (dead)
                    {
                        // bare twiggy branch with lichen
                        if (!full) continue;
                        for (int k = 0; k < 3; k++)
                        {
                            var a = Vector3.Lerp(root, tip, .3f + .22f * k);
                            var side = Vector3.Cross(d0, Vector3.up).normalized * (k % 2 == 0 ? 1 : -1);
                            var e = a + (side + d0 * .6f + Vector3.down * .2f).normalized * len * .3f;
                            mb.Tube(SubBark, a, e, br * .3f, .003f, 3, 2f);
                            if (R() < s.Lichen * 1.5f) Beard(mb, e, .25f + .35f * R(), .2f);
                        }
                        continue;
                    }
                    float w = s.CardWidth * Mathf.Lerp(.7f, 1.3f, Mathf.Clamp01(len / s.MaxBranch)) * Mathf.Lerp(1.5f, 1f, detail);
                    Card(mb, root + d0 * len * .1f, tip + d1 * .18f, w, detail);
                    // side shoots: the spray fans out, the crown turns opaque
                    int shoots = full ? s.Shoots : Mathf.Min(1, s.Shoots);
                    for (int k = 0; k < shoots; k++)
                    {
                        float at = .3f + .5f * (k + R() * .5f) / Mathf.Max(1, shoots);
                        var a = Vector3.Lerp(root, tip, at);
                        var side = Vector3.Cross(d0, Vector3.up).normalized * (k % 2 == 0 ? 1 : -1);
                        var sd = (side * .8f + d0 + Vector3.down * (s.Form == Form.Spruce ? .35f : .1f)).normalized;
                        Card(mb, a, a + sd * len * (.35f + .2f * R()), w * .8f, detail * .5f);
                    }
                    // snow pillow on the spray (heavier low in the crown, where the branches are wide)
                    if (s.SnowAmount > 0 && R() < s.SnowAmount * (1 - rel * .45f))
                    {
                        // кухта: two or three clumps along the spray, not one slab
                        int clumps = full ? 2 + rnd.Next(2) : 1;
                        for (int k = 0; k < clumps; k++)
                        {
                            float u0 = .12f + .7f * (k + R() * .6f) / clumps, u1 = Mathf.Min(1f, u0 + .22f + .15f * R());
                            var pa = Vector3.Lerp(root, tip, u0); var pb = Vector3.Lerp(root, tip, u1);
                            var off = Vector3.Cross(d0, Vector3.up).normalized * (R() - .5f) * w * .25f + Vector3.up * .05f;
                            Pillow(mb, pa + off, pb + off, w * (.22f + .16f * R()), (.05f + .1f * s.SnowAmount * (1 - rel)) * (.7f + .6f * R()), full ? 3 : 2, rnd);
                        }
                    }
                    if (full && rel < .35f && R() < s.Lichen * .5f) Beard(mb, Vector3.Lerp(root, tip, .5f) + Vector3.down * .05f, .25f + .3f * R(), .2f);
                }
            }
            if (!s.Dead)
            {
                for (int l = 0; l < s.Leaders; l++)
                {
                    Vector3 top = TrunkAt(H * .97f);
                    Vector3 lead = top + new Vector3((R() - .5f) * (l > 0 ? 1.4f : .1f), 1.1f, (R() - .5f) * (l > 0 ? 1.4f : .1f));
                    Card(mb, top - Vector3.up * .5f, lead, s.CardWidth * .6f, detail, true);
                }
                // snow skirt over the buried lower branches (not on larch: it has no skirt of evergreen boughs)
                if (s.Buried > 0 && s.Form != Form.Larch)
                {
                    // a low drift over the buried boughs, flush with the snow surface (no wall)
                    float rr = s.MaxBranch * Profile(s.Form, 0) * (s.Flagged ? 1.1f : .7f);
                    Surf.Lump(mb, SubSnow, new Vector3(0, -.08f, s.Flagged ? .35f : 0), new Vector3(rr, Mathf.Min(.35f, s.Buried * .3f), rr * (s.Flagged ? 1.25f : 1f)), s.Seed + 7, .55f, skirt: .15f);
                }
            }
            return mb;
        }

        /// <summary>A needle/twig card along a branch: a flat spray plus one or two copies tilted ±60° so it reads from the side.</summary>
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
                mb.Quad(SubLeaf, a - h, a + h, b + h * .55f, b - h * .55f, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), true, normal);
            }
            Plane(flat, (up + Vector3.up).normalized);
            if (detail > .3f) Plane(Quaternion.AngleAxis(60, along) * flat, (up + Vector3.up * .5f).normalized);
            if (detail > .9f) Plane(Quaternion.AngleAxis(-60, along) * flat, (up + Vector3.up * .5f).normalized);
        }

        /// <summary>Hanging lichen tuft: a vertical double-sided card below <paramref name="at"/>, turned to a random side.</summary>
        static void Beard(MeshBuilder mb, Vector3 at, float length, float width)
        {
            float a = (at.x * 13.7f + at.z * 7.3f + at.y * 3.1f) % 3.14f;
            var side = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * width * .5f;
            var down = Vector3.down * length;
            mb.Quad(SubLichen, at - side + down, at + side + down, at + side, at - side, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), true);
        }

        /// <summary>Snow lying along a branch spray: a lumpy pillow, thick in the middle, thin at the edges and the tip.</summary>
        static void Pillow(MeshBuilder mb, Vector3 a, Vector3 b, float width, float height, int segments, System.Random rnd)
        {
            Vector3 along = b - a; if (along.sqrMagnitude < 1e-4f) return;
            Vector3 side = Vector3.Cross(along.normalized, Vector3.up).normalized;
            float seed = (float)rnd.NextDouble() * 50f;
            Surf.Emit(mb, SubSnow, segments, 4, (u, v) =>
            {
                float taper = Mathf.Sin(Mathf.PI * Mathf.Lerp(.08f, 1f, u)) * (1f - .45f * u);
                float across = v * 2f - 1f;
                float bump = .75f + .5f * Mathf.PerlinNoise(seed + u * 3f, v * 2f);
                return Vector3.Lerp(a, b, u) + side * across * width * .5f * (.4f + .6f * taper) + Vector3.up * height * (1f - across * across) * taper * bump;
            }, new Surf.Options { Flip = true });
        }

        /// <summary>Far LOD: two stacked cones with a trunk base (~60 triangles).</summary>
        static MeshBuilder FarConifer(ConiferSpec s)
        {
            var mb = new MeshBuilder(Subs);
            float r = s.MaxBranch * (s.Form == Form.SiberianPine ? .95f : .8f);
            float cb = Mathf.Max(s.Height * s.CrownBase, s.Buried * .75f);
            float top = s.Dead ? s.Height * .7f : s.Height;
            mb.Tube(SubBark, Vector3.zero, Vector3.up * (s.Dead ? top : cb + .5f), s.TrunkRadius, s.TrunkRadius * (s.Dead ? .3f : .8f), 4);
            if (s.Dead) return mb;
            int sub = SubLeaf;
            mb.Cone(sub, Vector3.up * cb, r, (top - cb) * .62f, 7);
            mb.Cone(sub, Vector3.up * (cb + (top - cb) * .42f), r * .62f, (top - cb) * .58f, 7);
            return mb;
        }

        public sealed class BirchSpec { public float Height = BirchPrototypeHeight, TrunkRadius = .14f; public int Seed; public bool Crooked; public int Stems = 1; public float Buried = 1.1f; }

        /// <summary>Downy birch in winter: leafless, chalk-white trunk(s), upward main limbs with secondary limbs and a fine purple-brown twig haze.</summary>
        public static MeshBuilder Birch(BirchSpec s, float detail)
        {
            var rnd = new System.Random(s.Seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MeshBuilder(Subs);
            bool full = detail > .7f;
            for (int stem = 0; stem < s.Stems; stem++)
            {
                float H = s.Height * (stem == 0 ? 1f : .6f + .3f * R());
                float tr = s.TrunkRadius * (stem == 0 ? 1f : .7f);
                var baseOff = stem == 0 ? Vector3.zero : Quaternion.Euler(0, stem * 137f, 0) * Vector3.forward * .15f;
                Vector3 lean = new Vector3((R() - .5f) * (s.Crooked ? .9f : .35f), 0, (R() - .5f) * (s.Crooked ? .9f : .35f));
                if (stem > 0) lean += baseOff.normalized * H * .12f;
                Vector3 p0 = baseOff + Vector3.down * .3f, p1 = baseOff + new Vector3(lean.x * .3f, H * .35f, lean.z * .3f), p2 = baseOff + new Vector3(lean.x, H * .7f, lean.z), p3 = baseOff + new Vector3(lean.x * 1.2f, H, lean.z * 1.2f);
                if (s.Crooked) { p1 += new Vector3(R() - .5f, 0, R() - .5f) * .4f; p2 += new Vector3(R() - .5f, 0, R() - .5f) * .6f; }
                int sides = full ? 8 : 5;
                mb.Tube(SubBark, p0, p1, tr * 1.15f, tr * .8f, sides, .5f);
                mb.Tube(SubBark, p1, p2, tr * .8f, tr * .45f, sides, .5f, H * .35f);
                mb.Tube(SubBark, p2, p3, tr * .45f, .01f, sides, .5f, H * .7f, true);
                int limbs = Mathf.RoundToInt(Mathf.Lerp(6, 14, detail) * (stem == 0 ? 1f : .6f));
                for (int i = 0; i < limbs; i++)
                {
                    float t = Mathf.Lerp(.3f, .94f, i / (float)limbs) + (R() - .5f) * .05f;
                    Vector3 root = t < .7f ? Vector3.Lerp(p1, p2, (t - .35f) / .35f) : Vector3.Lerp(p2, p3, (t - .7f) / .3f);
                    if (root.y < s.Buried) continue;
                    float az = i * 137.5f + R() * 30;
                    Vector3 dir = (Quaternion.Euler(0, az, 0) * Vector3.forward * .75f + Vector3.up * (.9f + .4f * R())).normalized;
                    float len = H * .32f * (1.1f - t) * (.8f + .4f * R()) + .6f;
                    Vector3 mid = root + dir * len * .55f + Vector3.down * len * .06f;
                    Vector3 end = mid + (dir + Vector3.down * .15f).normalized * len * .45f;
                    mb.Tube(SubBark, root, mid, tr * .28f * (1.1f - t), tr * .15f, full ? 4 : 3, 1f);
                    mb.Tube(SubBark, mid, end, tr * .15f, .008f, 3, 1f);
                    if (detail < .3f) continue;
                    int twigsN = full ? 5 : 2;
                    for (int k = 0; k < twigsN; k++)
                    {
                        Vector3 a = Vector3.Lerp(root, end, .25f + .75f * k / twigsN);
                        Vector3 tdir = (dir + new Vector3(R() - .5f, R() * .3f - .45f, R() - .5f)).normalized;
                        Card(mb, a, a + tdir * (1.2f + R() * 1.1f), 1.3f + .4f * R(), detail);
                    }
                    if (full && R() < .5f) Pillow(mb, root + Vector3.up * .03f, mid + Vector3.up * .03f, tr * .9f, .04f, 2, rnd);
                }
            }
            return mb;
        }

        static MeshBuilder FarBirch(BirchSpec s)
        {
            var mb = new MeshBuilder(Subs);
            mb.Tube(SubBark, Vector3.zero, Vector3.up * s.Height * .75f, s.TrunkRadius, .02f, 4);
            mb.Cone(SubLeaf, Vector3.up * s.Height * .38f, s.Height * .22f, s.Height * .66f, 6);
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

        public static GameObject SavePrefab(string name, MeshBuilder lod0, MeshBuilder lod1, MeshBuilder lod2, Material bark, Material foliage, Material farFoliage, float trunkRadius, float height)
            => SavePrefab(name, lod0, lod1, lod2, bark, foliage, farFoliage, trunkRadius, height, new[] { .3f, .08f, .012f });

        /// <summary>Saves a 3-LOD prefab. <paramref name="lods"/>: screen heights where LOD0→1, 1→2 and 2→culled switch. Radius 0 = no collider.</summary>
        public static GameObject SavePrefab(string name, MeshBuilder lod0, MeshBuilder lod1, MeshBuilder lod2, Material bark, Material foliage, Material farFoliage, float trunkRadius, float height, float[] lods)
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
                mr.sharedMaterials = i < 2
                    ? new[] { Wind(bark, .6f), Wind(foliage, 1f), Wind(snowLumps, 1f), Wind(lichen, 1.4f) }
                    : new[] { Wind(bark, .6f), Wind(farFoliage, 0f), Wind(snowLumps, 1f), Wind(lichen, 1.4f) };
                mr.shadowCastingMode = i == 0 || (i == 1 && lods[2] < .02f) ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers.Add(new Renderer[] { mr });
                i++;
            }
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(lods[0], renderers[0]), new LOD(lods[1], renderers[1]), new LOD(lods[2], renderers[2]) });
            lg.fadeMode = LODFadeMode.None;
            lg.RecalculateBounds();
            if (trunkRadius > 0)
            {
                var col = root.AddComponent<CapsuleCollider>();
                col.radius = trunkRadius * 1.1f; col.height = height * .6f; col.center = new Vector3(0, height * .3f, 0);
            }
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>A copy of a library material on the wind shader, used only by standing trees (logs and cut branches keep the still original).</summary>
        static Material Wind(Material src, float flutter)
        {
            if (src == null) return null;
            var shader = Shader.Find("Height1079/TreeWind");
            if (shader == null) { Debug.LogWarning("1079: Height1079/TreeWind shader missing, trees stay still"); return src; }
            string name = $"{src.name}_Wind{(flutter > 0f ? "" : "Far")}";
            string path = $"{WorldPaths.Generated}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool fresh = m == null;
            if (fresh) { Directory.CreateDirectory(Path.GetDirectoryName(path)); m = new Material(shader); }
            m.shader = shader;
            m.CopyPropertiesFromMaterial(src);
            m.name = name;
            bool cut = src.IsKeywordEnabled("_ALPHATEST_ON");
            m.SetFloat("_Cutoff", cut ? src.GetFloat("_Cutoff") : 0f);
            m.SetFloat("_Flutter", flutter);
            m.renderQueue = cut ? 2450 : 2000;
            m.enableInstancing = true;
            if (fresh) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            return m;
        }

        public struct Prototype { public GameObject Prefab; public TreeSpecies Species; public TreeForm Form; public float Height; }

        /// <summary>Species library: spruce ×5, fir ×3, Siberian pine ×3, larch ×3, birch ×5 (2 crooked, 1 multi-stem);
        /// tree-line forms: flagged spruce ×3, stunted cedar ×1, stunted larch ×1, crooked birch ×2; snags: spruce ×2, fir ×1, larch ×1.</summary>
        public static List<Prototype> BuildLibrary()
        {
            EnsureMaterials();
            var list = new List<Prototype>();
            var bark = Materials.Bark; var cedarBark = Materials.BarkCedar; var birchBark = Materials.BirchBark;
            void Add(string name, TreeSpecies sp, TreeForm f, ConiferSpec s, Material b, Material leaf, Material far)
                => list.Add(new Prototype { Species = sp, Form = f, Height = s.Height, Prefab = SavePrefab(name, Conifer(s, 1), Conifer(s, .4f), FarConifer(s), b, leaf, far, s.TrunkRadius, s.Height) });
            void AddBirch(string name, TreeForm f, BirchSpec s)
                => list.Add(new Prototype { Species = TreeSpecies.Birch, Form = f, Height = s.Height, Prefab = SavePrefab(name, Birch(s, 1), Birch(s, .4f), FarBirch(s), birchBark, twigs, farBirch, s.TrunkRadius, s.Height) });

            for (int v = 0; v < 5; v++)
                Add($"Spruce_{v}", TreeSpecies.Spruce, TreeForm.Normal, new ConiferSpec { Form = Form.Spruce, Seed = 100 + v, MaxBranch = 2.0f + .25f * v, PerWhorl = 6, Whorl = .36f, Droop = -26f + 3 * v, TipLift = 20f, CardWidth = .8f, CrownBase = .04f + .03f * v, SnowAmount = .7f + .06f * v, Shoots = 2 + v % 2, Lichen = .25f + .08f * v, TrunkRadius = .2f + .02f * v }, bark, foliageSpruce, farSpruce);
            for (int v = 0; v < 3; v++)
                Add($"Fir_{v}", TreeSpecies.Fir, TreeForm.Normal, new ConiferSpec { Form = Form.Fir, Seed = 200 + v, MaxBranch = 1.5f + .2f * v, PerWhorl = 6, Whorl = .34f, Droop = -6f, TipLift = 8f, CardWidth = .66f, TrunkRadius = .17f, CrownBase = .03f, SnowAmount = .95f, Shoots = 2, Lichen = .3f }, barkFir, foliageFir, farSpruce);
            for (int v = 0; v < 3; v++)
                Add($"SiberianPine_{v}", TreeSpecies.SiberianPine, TreeForm.Normal, new ConiferSpec { Form = Form.SiberianPine, Seed = 300 + v, MaxBranch = 3.2f + .3f * v, PerWhorl = 7, Droop = -5f, TipLift = 25f, CardWidth = 1.0f, TrunkRadius = .3f, CrownBase = .2f, Whorl = .5f, Leaders = 1 + v, SnowAmount = .75f, Shoots = 2, Lichen = .2f }, cedarBark, foliagePine, farSpruce);
            for (int v = 0; v < 3; v++)
                Add($"Larch_{v}", TreeSpecies.Larch, TreeForm.Normal, new ConiferSpec { Form = Form.Larch, Seed = 350 + v, MaxBranch = 2.4f + .3f * v, PerWhorl = 5, Whorl = .5f, TipLift = 18f, CardWidth = 1.1f, TrunkRadius = .24f, CrownBase = .15f + .05f * v, SnowAmount = .15f, Shoots = 2, Lichen = .45f, Buried = 0f }, barkLarch, twigsLarch, farLarch);
            for (int v = 0; v < 5; v++)
                AddBirch($"Birch_{v}", TreeForm.Normal, new BirchSpec { Seed = 400 + v, Crooked = v >= 3, TrunkRadius = v >= 3 ? .12f : .15f, Stems = v == 2 ? 3 : 1 });

            // tree line: flagged, stunted, crooked
            for (int v = 0; v < 3; v++)
                Add($"SpruceFlagged_{v}", TreeSpecies.Spruce, TreeForm.TreeLine, new ConiferSpec { Form = Form.Spruce, Seed = 120 + v, Height = 9f, MaxBranch = 1.5f + .2f * v, PerWhorl = 5, Whorl = .34f, Droop = -30f, TipLift = 14f, CardWidth = .72f, CrownBase = .02f, SnowAmount = .9f, Flagged = true, Shoots = 2, Lichen = .5f, TrunkRadius = .15f, DeadTwigs = false }, bark, foliageSpruce, farSpruce);
            Add("SiberianPineStunted_0", TreeSpecies.SiberianPine, TreeForm.TreeLine, new ConiferSpec { Form = Form.SiberianPine, Seed = 330, Height = 8f, MaxBranch = 2.6f, PerWhorl = 6, Droop = 2f, TipLift = 30f, CardWidth = .95f, TrunkRadius = .22f, CrownBase = .05f, Whorl = .5f, Leaders = 3, SnowAmount = .85f, Flagged = true, Lichen = .4f }, cedarBark, foliagePine, farSpruce);
            Add("FirStunted_0", TreeSpecies.Fir, TreeForm.TreeLine, new ConiferSpec { Form = Form.Fir, Seed = 230, Height = 8f, MaxBranch = 1.3f, PerWhorl = 5, Whorl = .34f, Droop = -10f, TipLift = 8f, CardWidth = .6f, TrunkRadius = .13f, CrownBase = .02f, SnowAmount = 1f, Flagged = true, Lichen = .5f }, barkFir, foliageFir, farSpruce);
            Add("LarchStunted_0", TreeSpecies.Larch, TreeForm.TreeLine, new ConiferSpec { Form = Form.Larch, Seed = 360, Height = 9f, MaxBranch = 2.2f, PerWhorl = 4, Whorl = .45f, TipLift = 25f, CardWidth = 1.0f, TrunkRadius = .2f, CrownBase = .08f, SnowAmount = .1f, Flagged = true, Leaders = 2, Lichen = .6f, Buried = 0f }, barkLarch, twigsLarch, farLarch);
            AddBirch("BirchCrooked_0", TreeForm.TreeLine, new BirchSpec { Seed = 450, Height = 7f, Crooked = true, TrunkRadius = .1f, Stems = 3, Buried = .8f });
            AddBirch("BirchCrooked_1", TreeForm.TreeLine, new BirchSpec { Seed = 451, Height = 6f, Crooked = true, TrunkRadius = .09f, Stems = 4, Buried = .7f });

            // snags
            for (int v = 0; v < 2; v++)
                Add($"SpruceSnag_{v}", TreeSpecies.Spruce, TreeForm.Snag, new ConiferSpec { Form = Form.Spruce, Seed = 140 + v, MaxBranch = 1.6f, PerWhorl = 4, Whorl = .5f, Droop = -30f, TipLift = 5f, CrownBase = .1f, Dead = true, Lichen = .7f, TrunkRadius = .2f }, barkDead, foliageSpruce, farSpruce);
            Add("FirSnag_0", TreeSpecies.Fir, TreeForm.Snag, new ConiferSpec { Form = Form.Fir, Seed = 240, MaxBranch = 1.2f, PerWhorl = 4, Whorl = .45f, Droop = -15f, CrownBase = .05f, Dead = true, Lichen = .7f, TrunkRadius = .16f }, barkDead, foliageFir, farSpruce);
            Add("SiberianPineSnag_0", TreeSpecies.SiberianPine, TreeForm.Snag, new ConiferSpec { Form = Form.SiberianPine, Seed = 340, MaxBranch = 2.4f, PerWhorl = 4, Whorl = .6f, CrownBase = .25f, Dead = true, Lichen = .6f, TrunkRadius = .28f }, barkDead, foliagePine, farSpruce);
            Add("LarchSnag_0", TreeSpecies.Larch, TreeForm.Snag, new ConiferSpec { Form = Form.Larch, Seed = 370, MaxBranch = 2f, PerWhorl = 4, Whorl = .6f, CrownBase = .2f, Dead = true, Lichen = .6f, TrunkRadius = .22f, Buried = 0f }, barkDead, twigsLarch, farLarch);
            return list;
        }

        // ------------------------------------------------------------------ understory

        public struct UnderPrototype { public GameObject Prefab; public UnderKind Kind; public float Size; public bool Upright; }

        /// <summary>Understory library: several variants per kind. <see cref="UnderPrototype.Size"/> is the reference size the importer scales from
        /// (height for trees and shrubs, length for windfall, diameter for hummocks).</summary>
        public static List<UnderPrototype> BuildUnderstory()
        {
            EnsureMaterials();
            var list = new List<UnderPrototype>();
            var small = new[] { .2f, .09f, .045f };   // small things vanish early (a 3 m shrub at ~60 m)
            var shrub = new[] { .25f, .12f, .07f };
            void Tree(string name, UnderKind kind, ConiferSpec s, Material b, Material leaf)
                => list.Add(new UnderPrototype { Kind = kind, Size = s.Height, Upright = true, Prefab = SavePrefab(name, Conifer(s, 1), Conifer(s, .4f), FarConifer(s), b, leaf, farSpruce, s.Height > 3.5f ? s.TrunkRadius : 0f, s.Height, small) });

            // young conifers: heavily snow-loaded, low skirts, no dead twigs yet
            for (int v = 0; v < 3; v++)
                Tree($"YoungSpruce_{v}", UnderKind.YoungSpruce, new ConiferSpec { Form = Form.Spruce, Seed = 600 + v, Height = 4.5f, MaxBranch = 1.2f + .15f * v, PerWhorl = 5, Whorl = .26f, Droop = -24f, TipLift = 18f, CardWidth = .55f, TrunkRadius = .07f, CrownBase = 0f, SnowAmount = 1f, Shoots = 2, Lichen = 0f, DeadTwigs = false, Buried = .9f }, Materials.Bark, foliageSpruce);
            for (int v = 0; v < 2; v++)
                Tree($"YoungFir_{v}", UnderKind.YoungFir, new ConiferSpec { Form = Form.Fir, Seed = 620 + v, Height = 4f, MaxBranch = .95f + .1f * v, PerWhorl = 5, Whorl = .24f, Droop = -5f, TipLift = 6f, CardWidth = .5f, TrunkRadius = .06f, CrownBase = 0f, SnowAmount = 1f, Shoots = 2, Lichen = 0f, DeadTwigs = false, Buried = .9f }, barkFir, foliageFir);
            for (int v = 0; v < 2; v++)
                Tree($"YoungPine_{v}", UnderKind.YoungPine, new ConiferSpec { Form = Form.SiberianPine, Seed = 640 + v, Height = 3.5f, MaxBranch = 1.3f, PerWhorl = 5, Whorl = .3f, Droop = 0f, TipLift = 30f, CardWidth = .7f, TrunkRadius = .07f, CrownBase = 0f, SnowAmount = 1f, Shoots = 1, Lichen = 0f, DeadTwigs = false, Buried = .8f, Leaders = 1 + v }, Materials.BarkCedar, foliagePine);

            // rowan: a few grey stems, upright twigs, a few clusters of berries left from autumn
            for (int v = 0; v < 3; v++)
            {
                var mb0 = Shrub(660 + v, 4f, 2 + v, .05f, 70f, .55f, 4, true, 1f);
                var mb1 = Shrub(660 + v, 4f, 2 + v, .05f, 70f, .55f, 2, false, .4f);
                list.Add(new UnderPrototype { Kind = UnderKind.Rowan, Size = 4f, Upright = true, Prefab = SavePrefab($"Rowan_{v}", mb0, mb1, FarShrub(4f, .8f), barkRowan, twigsRowan, twigsRowan, 0f, 4f, small) });
            }
            // willow clumps: many straight reddish stems out of the snow
            for (int v = 0; v < 3; v++)
            {
                var mb0 = Shrub(680 + v, 2.2f, 9 + 3 * v, .018f, 30f, .45f, 2, false, 1f);
                var mb1 = Shrub(680 + v, 2.2f, 5 + v, .018f, 30f, .45f, 1, false, .4f);
                list.Add(new UnderPrototype { Kind = UnderKind.Willow, Size = 2.2f, Upright = true, Prefab = SavePrefab($"Willow_{v}", mb0, mb1, FarShrub(2.2f, .9f), barkRowan, twigsWillow, twigsWillow, 0f, 2.2f, shrub) });
            }
            // juniper: low dark mound half in the snow
            for (int v = 0; v < 2; v++)
                list.Add(new UnderPrototype { Kind = UnderKind.Juniper, Size = 1.2f, Prefab = SavePrefab($"Juniper_{v}", Mound(700 + v, 1.2f, foliageJuniper != null, 1f), Mound(700 + v, 1.2f, true, .4f), SnowOnly(700 + v, 1.2f), Materials.Bark, foliageJuniper, foliageJuniper, 0f, .6f, shrub) });
            // dwarf birch: twig tufts barely above the snow
            for (int v = 0; v < 2; v++)
            {
                var mb0 = Shrub(720 + v, .7f, 7, .008f, 50f, .6f, 1, false, 1f);
                list.Add(new UnderPrototype { Kind = UnderKind.DwarfBirch, Size = .7f, Upright = true, Prefab = SavePrefab($"DwarfBirch_{v}", mb0, Shrub(720 + v, .7f, 4, .008f, 50f, .6f, 1, false, .4f), SnowOnly(720 + v, .5f), barkRowan, twigs, twigs, 0f, .7f, small) });
            }
            // crooked multi-stem birch at the tree line
            for (int v = 0; v < 2; v++)
            {
                var s = new BirchSpec { Seed = 740 + v, Height = 3f, Crooked = true, TrunkRadius = .05f, Stems = 4 + v, Buried = .5f };
                list.Add(new UnderPrototype { Kind = UnderKind.BirchClump, Size = 3f, Upright = true, Prefab = SavePrefab($"BirchClump_{v}", Birch(s, 1), Birch(s, .4f), FarBirch(s), Materials.BirchBark, twigs, farBirch, 0f, 3f, small) });
            }
            // windfall: a trunk under the snow, root plate or broken end, dead branches sticking out
            for (int v = 0; v < 3; v++)
                list.Add(new UnderPrototype { Kind = UnderKind.Windfall, Size = 10f, Prefab = SavePrefab($"Windfall_{v}", Windfall(760 + v, 10f, 1f), Windfall(760 + v, 10f, .4f), SnowOnly(760 + v, 3f, 10f), barkDead, twigs, twigs, 0f, 1f, shrub) });
            // hummocks: buried stumps and shrubs; sometimes a twig tip shows
            for (int v = 0; v < 3; v++)
                list.Add(new UnderPrototype { Kind = UnderKind.Hummock, Size = 1f, Prefab = SavePrefab($"Hummock_{v}", Hummock(780 + v, v == 1), Hummock(780 + v, false), SnowOnly(780 + v, 1f), barkDead, twigs, twigs, 0f, .5f, small) });
            return list;
        }

        /// <summary>Multi-stem deciduous shrub: <paramref name="stems"/> from one base, fanning by <paramref name="spread"/>°, twig cards at the tips.</summary>
        static MeshBuilder Shrub(int seed, float height, int stems, float radius, float spread, float twigSize, int twigsPerStem, bool berries, float detail)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MeshBuilder(Subs);
            for (int i = 0; i < stems; i++)
            {
                float az = i * 137.5f + R() * 40f, tilt = spread * (.3f + .7f * R());
                var dir = Quaternion.Euler(0, az, 0) * Quaternion.Euler(tilt, 0, 0) * Vector3.up;
                float h = height * (.6f + .4f * R());
                var a = new Vector3((R() - .5f) * .2f, -.2f, (R() - .5f) * .2f);
                var mid = a + dir * h * .5f + new Vector3(R() - .5f, 0, R() - .5f) * h * .05f;
                var b = mid + (dir + Vector3.up * .3f).normalized * h * .5f;
                mb.Tube(SubBark, a, mid, radius, radius * .6f, detail > .7f ? 4 : 3, 2f);
                mb.Tube(SubBark, mid, b, radius * .6f, radius * .15f, 3, 2f);
                for (int k = 0; k < twigsPerStem; k++)
                {
                    var at = Vector3.Lerp(mid, b, .2f + .8f * k / Mathf.Max(1, twigsPerStem));
                    var td = (dir + new Vector3(R() - .5f, .5f, R() - .5f)).normalized;
                    Card(mb, at, at + td * twigSize * (1.2f + R()), twigSize * 1.6f, detail);
                }
                if (detail > .7f && R() < .4f) Pillow(mb, a + dir * h * .35f, mid + dir * h * .1f, radius * 4f, .03f, 2, rnd);
            }
            Surf.Lump(mb, SubSnow, Vector3.down * .05f, new Vector3(.3f, .15f, .3f) * Mathf.Min(1.5f, height), seed, .5f, skirt: .08f);
            return mb;
        }

        static MeshBuilder FarShrub(float height, float width)
        {
            var mb = new MeshBuilder(Subs);
            mb.Cone(SubLeaf, Vector3.zero, width * .5f, height, 5);
            return mb;
        }

        /// <summary>Juniper: a low mound of needle cards radiating from the centre, snow in the middle.</summary>
        static MeshBuilder Mound(int seed, float size, bool withCards, float detail)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MeshBuilder(Subs);
            int n = detail > .7f ? 18 : 8;
            for (int i = 0; i < n; i++)
            {
                var dir = Quaternion.Euler(0, i * 360f / n + R() * 15f, 0) * Vector3.forward;
                var a = Vector3.up * size * .15f;
                var b = dir * size * (.5f + .3f * R()) + Vector3.up * size * (.25f + .2f * R());
                if (withCards) Card(mb, a, b, size * .55f, detail);
            }
            Surf.Lump(mb, SubSnow, Vector3.up * size * .2f, new Vector3(.4f, .22f, .35f) * size, seed, .45f, skirt: .2f);
            return mb;
        }

        static MeshBuilder SnowOnly(int seed, float size, float length = 0f)
        {
            var mb = new MeshBuilder(Subs);
            Surf.Lump(mb, SubSnow, Vector3.zero, length > 0 ? new Vector3(.55f, .25f, length * .5f) : new Vector3(.5f, .3f, .5f) * size, seed, .5f);
            return mb;
        }

        /// <summary>A fallen trunk buried in snow: a long lumpy mound, the root plate or broken end at one side, dead branches up through the snow.</summary>
        static MeshBuilder Windfall(int seed, float length, float detail)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MeshBuilder(Subs);
            bool full = detail > .7f;
            float half = length * .5f;
            Surf.Lump(mb, SubSnow, Vector3.zero, new Vector3(.6f, .28f, half), seed, .55f, skirt: .2f);
            if (seed % 2 == 0)
            {
                // root plate: an upturned disc of roots and earth, snow on its top edge
                var c = new Vector3(0, .6f, -half - .1f);
                Surf.Lump(mb, SubBark, c, new Vector3(1.1f, .9f, .2f), seed + 1, .5f, full: true);
                Surf.Lump(mb, SubSnow, c + new Vector3(0, .75f, 0), new Vector3(.9f, .18f, .25f), seed + 2, .4f);
                if (full)
                    for (int k = 0; k < 10; k++)
                    {
                        float a = k / 10f * Mathf.PI * 2f;
                        var p = c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * .9f;
                        mb.Tube(SubBark, p, p + new Vector3(Mathf.Cos(a), Mathf.Sin(a), R() - .5f) * (.3f + .5f * R()), .04f, .005f, 3, 2f);
                    }
            }
            else
            {
                // broken end sticking out of the snow
                var e = new Vector3(0, .2f, half);
                mb.Tube(SubBark, e - Vector3.forward * .6f, e + new Vector3(0, .15f, .5f), .22f, .18f, full ? 7 : 4, 1f, 0, true);
                for (int k = 0; k < 4; k++)
                {
                    var d = Quaternion.Euler(0, 0, k * 90 + R() * 30) * Vector3.up * .15f;
                    mb.Tube(SubBark, e + new Vector3(0, .15f, .5f) + d * .5f, e + new Vector3(0, .15f, .75f + R() * .3f) + d, .05f, .005f, 3, 1f);
                }
            }
            int branches = full ? 7 : 3;
            for (int k = 0; k < branches; k++)
            {
                var p = new Vector3((R() - .5f) * .4f, .3f, Mathf.Lerp(-half * .7f, half * .8f, R()));
                var tip = p + new Vector3((R() - .5f) * 1.2f, .5f + R() * 1.1f, (R() - .5f) * .6f);
                mb.Tube(SubBark, p, tip, .035f, .005f, 3, 2f);
                if (full && R() < .5f) Card(mb, Vector3.Lerp(p, tip, .5f), tip + Vector3.up * .2f, .5f, .4f);
            }
            return mb;
        }

        static MeshBuilder Hummock(int seed, bool twig)
        {
            var rnd = new System.Random(seed);
            var mb = new MeshBuilder(Subs);
            Surf.Lump(mb, SubSnow, Vector3.down * .08f, new Vector3(.6f, .3f, .55f), seed, .5f, skirt: .1f);
            if (twig)
                for (int k = 0; k < 3; k++)
                {
                    var p = new Vector3((float)rnd.NextDouble() - .5f, .25f, (float)rnd.NextDouble() - .5f) * .5f;
                    mb.Tube(SubBark, p, p + new Vector3((float)rnd.NextDouble() - .5f, .5f, (float)rnd.NextDouble() - .5f) * .6f, .015f, .003f, 3, 2f);
                }
            return mb;
        }

        // ------------------------------------------------------------------ named trees

        /// <summary>The hero cedar (Siberian pine) at the KAN point: 18 m from the canopy model, a thick old trunk, branches broken up to 4.5 m on the side facing the tent.</summary>
        public static GameObject HeroCedar(Vector2 towardTent)
        {
            EnsureMaterials();
            var s = new ConiferSpec
            {
                Form = Form.SiberianPine, Seed = 1959, Height = Sites.Cedar.Height, TrunkRadius = .32f, MaxBranch = 4.2f, PerWhorl = 7, Whorl = .5f,
                Droop = -4f, TipLift = 28f, CardWidth = 1.05f, CrownBase = .1f, Leaders = 3, SnowAmount = .6f, Shoots = 2, Lichen = .4f,
                BrokenBelow = Sites.Cedar.BrokenUpTo, BrokenDirection = towardTent.normalized, BrokenHalfAngle = 75f, DeadBelow = 2.2f, Buried = 0f, DeadTwigs = false,
            };
            return SavePrefab("HeroCedar", Conifer(s, 1), Conifer(s, .55f), FarConifer(s), Materials.BarkCedar, foliagePine, farSpruce, s.TrunkRadius, s.Height);
        }

        /// <summary>Birch by which Dyatlov was found (protocol: head 5–7 cm behind its trunk, arm on its branch).</summary>
        public static GameObject LoneBirch(string name, int seed)
        {
            EnsureMaterials();
            var s = new BirchSpec { Seed = seed, Height = 6.5f, TrunkRadius = .1f, Crooked = true, Buried = 0f };
            return SavePrefab(name, Birch(s, 1), Birch(s, .4f), FarBirch(s), Materials.BirchBark, twigs, farBirch, s.TrunkRadius, s.Height);
        }

        /// <summary>KAN landmark "triple tree": three spruce stems from one root.</summary>
        public static GameObject TripleSpruce()
        {
            EnsureMaterials();
            var all = new MeshBuilder[3];
            for (int lod = 0; lod < 3; lod++)
            {
                var mb = new MeshBuilder(Subs);
                for (int k = 0; k < 3; k++)
                {
                    var s = new ConiferSpec { Form = Form.Spruce, Seed = 500 + k, Height = 12f - k * 1.5f, MaxBranch = 1.6f, TrunkRadius = .14f, CrownBase = .15f, Shoots = 2 };
                    var part = lod == 2 ? FarConifer(s) : Conifer(s, lod == 0 ? 1 : .4f);
                    mb.Append(part, Matrix4x4.TRS(Quaternion.Euler(0, k * 120, 0) * new Vector3(.35f, 0, 0), Quaternion.Euler(0, k * 120, (k - 1) * 6f), Vector3.one));
                }
                all[lod] = mb;
            }
            return SavePrefab("TripleSpruce", all[0], all[1], all[2], Materials.Bark, foliageSpruce, farSpruce, .5f, 12f);
        }
    }
}
