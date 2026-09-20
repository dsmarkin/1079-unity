using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static Height1079.EditorTools.World.Surf;

namespace Height1079.EditorTools.World
{
    /// <summary>Skis, poles and the two ways the group carried them — the core mechanic, so this is the most detailed prop in the library.
    ///
    /// Modelled on the wooden touring skis (туристические лыжи) sold in the USSR in the 1950s and on the group's own photographs:
    /// a laminated birch board about 2 m long and 7.5 cm wide, 1.4 cm thick under the foot, a turned-up tip, a little camber so the
    /// middle lifts off the snow, one groove down the running surface, steel edging screwed along the base edges, and a semi-rigid
    /// binding — a steel toe iron with two coil springs, a bail around the heel, a toe strap and a heel strap of harness leather, and a
    /// felt pad where the heel lands. Poles are bamboo with a leather grip, a wrist loop, a cane-and-thong basket and a steel ferrule.
    ///
    /// Prefabs (Assets/Generated/World/Prefabs/Gear), two LODs each, +Z towards the tip, Y up:
    ///   Ski_Left / Ski_Right   pivot on the snow under the middle of the binding (the board itself sits 1.2 cm higher: that is the camber)
    ///   Pole_Bamboo            pivot on the snow at the ferrule
    ///   Ski_Pair_Packed        two skis tied base to base, tips up, poles lashed alongside — what rides on a rucksack when walking
    ///   Ski_Sled               волокуша: both skis flat on the snow, lashed crossbars, rope harness for the rucksack
    ///
    /// Textures: the factory prefers Poly Haven (CC0) scans when they are in Assets/Art/ThirdParty/PolyHaven/Textures and generates
    /// its own albedo + normal pair otherwise, exactly like PropTextures does for the camp props. Leather and felt use scans that are
    /// already in the repository (brown_leather, poly_wool_herringbone); wood, lacquer, bamboo, steel and rope are generated here
    /// because the machine that wrote this file had no route to polyhaven.com. Nothing else has to change to swap them for scans:
    /// put wood_planks (or oak_wood_planks) and bamboo_wall into Assets/Art/ThirdParty/PolyHaven/Textures/&lt;id&gt;/ as
    /// &lt;id&gt;_diff_1k.jpg and &lt;id&gt;_nor_gl_1k.jpg — the .jpg extension matters, it is what WorldPaths.PH builds — add each id to
    /// SOURCES.json with its author and licence, and rebuild. Direct links come from https://api.polyhaven.com/files/&lt;id&gt;.</summary>
    public static class SkiFactory
    {
        const string PrefabDir = WorldPaths.Kit + "/Prefabs/Gear";
        const string MeshDir = WorldPaths.Kit + "/Meshes/Gear";

        // One submesh layout for every prefab here, so builders can be appended into each other with Matrix4x4 transforms.
        const int SubLacquer = 0, SubLaminate = 1, SubBase = 2, SubSteel = 3, SubLeather = 4, SubFelt = 5, SubBamboo = 6, SubRope = 7, SubWood = 8, Subs = 9;

        // ------------------------------------------------------------------ dimensions (metres)
        const float TailZ = -.95f, TipZ = 1.05f;                       // 2.00 m over all, binding centre at z = 0
        const float TailHalf = .0345f, WaistHalf = .0365f, ShovelHalf = .040f;   // 6.9 / 7.3 / 8.0 cm wide
        const float ThickTail = .009f, ThickFoot = .0140f, ThickTip = .005f;    // 1.4 cm under the foot
        const float Camber = .012f;                                    // base lifts this far off the snow at the binding
        const float TipRise = .085f, TipStart = .78f;                  // turned-up tip
        const float GrooveHalf = .0055f, GrooveDepth = .0035f;         // желобок, 11 mm wide
        const float Crown = .0009f;                                    // the top face is slightly arched
        const float EdgeWidth = .005f, EdgeProud = .0004f;             // steel edging on the base edges
        public const float Length = TipZ - TailZ;
        public const float PoleLength = 1.30f;
        const float NodeSpacing = .28f;                                // bamboo internode

        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Material M(string key, Func<Material> make) { if (!cache.TryGetValue(key, out var m) || m == null) cache[key] = m = make(); return m; }

        /// <summary>A Poly Haven material when that scan is in the repository, null otherwise (the caller then generates one).</summary>
        static Material Ph(string name, string id, Color tint, float tile, float smoothness, float normalScale = 1f)
            => Materials.OptionalTex(WorldPaths.PH(id, "diff")) != null ? Materials.PH(name, id, tint, tile, smoothness, normalScale) : null;

        static Material Lacquer => M("SkiLacquer", () => Ph("SkiLacquer", "wood_planks", new Color(.88f, .72f, .5f), 1.5f, .62f, .7f) ?? LacquerGen());
        static Material Laminate => M("SkiLaminate", () => LaminateGen());
        static Material Base => M("SkiBase", () => BaseGen());
        static Material Steel => M("SkiSteel", () => SteelGen());
        static Material Leather => M("SkiLeather", () => Ph("SkiLeather", "brown_leather", new Color(.6f, .44f, .3f), 1f, .26f) ?? PropTextures.Leather("SkiLeather", new Color(.34f, .21f, .12f), 4));
        static Material Felt => M("SkiFelt", () => Ph("SkiFelt", "poly_wool_herringbone", new Color(.4f, .38f, .35f), 3f, .04f) ?? PropTextures.Felt("SkiFelt", new Color(.3f, .29f, .27f), 11));
        static Material Bamboo => M("SkiBamboo", () => Ph("SkiBamboo", "bamboo_wall", new Color(.9f, .8f, .55f), 1f, .5f) ?? BambooGen());
        static Material Rope => M("SkiRope", () => RopeGen());
        static Material RawWood => M("SkiRawWood", () => RawWoodGen());

        static Material[] Mats => new[] { Lacquer, Laminate, Base, Steel, Leather, Felt, Bamboo, Rope, RawWood };

        // ------------------------------------------------------------------ generated textures (tileable noise, PropTextures.Pair style)
        static float NP(float u, float v, float f, float seed) => Mathf.PerlinNoise(u * f + seed, v * f + seed * .7f);
        /// <summary>Tileable value noise over the unit square (blends four wrapped offsets).</summary>
        static float NT(float u, float v, float f, float seed)
        {
            float a = NP(u, v, f, seed), b = NP(u - 1, v, f, seed), c = NP(u, v - 1, f, seed), d = NP(u - 1, v - 1, f, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
        static float NFbm(float u, float v, float f, float seed) => (NT(u, v, f, seed) + .5f * NT(u, v, f * 2, seed + 3) + .25f * NT(u, v, f * 4, seed + 7)) / 1.75f;
        static Color Op(Color c) { c.a = 1; return c; }
        /// <summary>Narrow ridge at u = at (glue line, node ring, seam).</summary>
        static float Ridge(float x, float at, float sharp) => Mathf.Exp(-((x - at) * sharp) * ((x - at) * sharp));

        /// <summary>Top of the board: varnished laminated birch. Grain runs along the ski (so it varies across u), two glue lines
        /// where the blank was joined, rubbed varnish where boots and straps have worn it.</summary>
        static Material LacquerGen() => PropTextures.Pair("SkiLacquer",
            (u, v) =>
            {
                float streak = NP(u * 46f, v * 1.4f, 1f, 3f);
                float figure = NFbm(u * 2.5f, v * .7f, 3f, 11f);
                float glue = Mathf.Max(Ridge(u, .33f, 150f), Ridge(u, .67f, 150f));
                float ray = Mathf.Pow(NP(u * 120f, v * .5f, 1f, 17f), 4f);
                float rub = Mathf.Pow(NT(u, v, 7f, 21f), 5f);
                var wood = Color.Lerp(new Color(.82f, .65f, .42f), new Color(.58f, .41f, .24f), streak * .55f + figure * .35f);
                wood = Color.Lerp(wood, new Color(.93f, .84f, .66f), ray * .5f);
                wood = Color.Lerp(wood, new Color(.33f, .23f, .14f), glue * .75f);
                wood = Color.Lerp(wood, new Color(.9f, .87f, .8f), rub * .3f);
                return Op(wood);
            },
            (u, v) => NP(u * 46f, v * 1.4f, 1f, 3f) * .22f - Mathf.Max(Ridge(u, .33f, 150f), Ridge(u, .67f, 150f)) * .55f,
            1.8f, .62f);

        /// <summary>Running surface: tarred and waxed wood, greyed by the snow, long scratches from crust and stones, dirt in the groove.</summary>
        static Material BaseGen() => PropTextures.Pair("SkiBase",
            (u, v) =>
            {
                float grain = NP(u * 52f, v * 1.2f, 1f, 5f);
                float scratch = Mathf.Pow(NP(u * 200f, v * .35f, 1f, 29f), 6f);
                float tar = NFbm(u, v, 5f, 31f);
                var wood = Color.Lerp(new Color(.4f, .32f, .24f), new Color(.24f, .19f, .15f), grain * .5f + tar * .5f);
                wood = Color.Lerp(wood, new Color(.66f, .64f, .6f), scratch * .55f);
                return Op(wood);
            },
            (u, v) => NP(u * 52f, v * 1.2f, 1f, 5f) * .2f + Mathf.Pow(NP(u * 200f, v * .35f, 1f, 29f), 6f) * .4f,
            1.4f, .46f);

        /// <summary>Side wall of the board, sawn through the lamination: seven layers of birch with dark glue lines between them
        /// (they run across u, which the strips map across the thickness) and end grain along v.</summary>
        static Material LaminateGen() => PropTextures.Pair("SkiLaminate",
            (u, v) =>
            {
                float b = u * 7f, band = Mathf.Floor(b), f = b - band;
                float glue = Ridge(Mathf.Min(f, 1f - f), 0f, 13f);
                float tone = .88f + .12f * Mathf.Sin(band * 2.3f);
                float grain = NP(u * 5f, v * 90f, 1f, 7f + band);
                var wood = Color.Lerp(new Color(.8f, .66f, .46f), new Color(.63f, .48f, .3f), grain * .6f) * tone;
                wood = Color.Lerp(wood, new Color(.25f, .17f, .11f), glue * .85f);
                return Op(wood);
            },
            (u, v) => { float f = u * 7f - Mathf.Floor(u * 7f); return -Ridge(Mathf.Min(f, 1f - f), 0f, 13f) * .6f + NP(u * 5f, v * 90f, 1f, 7f) * .15f; },
            2.2f, .5f);

        /// <summary>Bamboo culm: fine fibres along the shaft, a dark swollen ring at every node, sun-bleached patches and handling grime.
        /// One node per half a v unit, so the sweep maps v = metres / (2 · node spacing).</summary>
        static Material BambooGen() => PropTextures.Pair("SkiBamboo",
            (u, v) =>
            {
                float node = Mathf.Pow(Mathf.Abs(Mathf.Cos(v * Mathf.PI)), 55f);
                float fibre = NP(u * 90f, v * 2f, 1f, 13f);
                float blotch = NFbm(u, v, 4f, 23f);
                var cane = Color.Lerp(new Color(.85f, .74f, .46f), new Color(.68f, .57f, .33f), fibre * .5f + blotch * .4f);
                cane = Color.Lerp(cane, new Color(.38f, .27f, .14f), node * .8f);
                cane = Color.Lerp(cane, new Color(.55f, .5f, .4f), Mathf.Pow(blotch, 3f) * .4f);
                return Op(cane);
            },
            (u, v) => Mathf.Pow(Mathf.Abs(Mathf.Cos(v * Mathf.PI)), 55f) * .9f + NP(u * 90f, v * 2f, 1f, 13f) * .12f,
            3f, .52f, 256);

        /// <summary>Edging, toe irons and screws: blued steel, scuffed bright along the wear lines, a little rust in the pits.</summary>
        static Material SteelGen()
        {
            var m = PropTextures.Pair("SkiSteel",
                (u, v) =>
                {
                    float scratch = Mathf.Pow(NP(u * 160f, v * .4f, 1f, 37f), 5f);
                    float pit = Mathf.Pow(NT(u, v, 40f, 41f), 6f);
                    var steel = Color.Lerp(new Color(.26f, .27f, .29f), new Color(.46f, .47f, .48f), NFbm(u, v, 8f, 43f) * .6f);
                    steel = Color.Lerp(steel, new Color(.74f, .75f, .74f), scratch * .7f);
                    steel = Color.Lerp(steel, new Color(.42f, .26f, .15f), pit * .8f);
                    return Op(steel);
                },
                (u, v) => Mathf.Pow(NP(u * 160f, v * .4f, 1f, 37f), 5f) * .3f - Mathf.Pow(NT(u, v, 40f, 41f), 6f) * .6f,
                2f, .52f, 256, .78f);
            return m;
        }

        /// <summary>Three-strand hemp rope: the strands spiral, so they run diagonally across the tube (u around, v along).</summary>
        static Material RopeGen() => PropTextures.Pair("SkiRope",
            (u, v) =>
            {
                float s = Mathf.Abs(Mathf.Sin((u * 3f + v) * Mathf.PI));
                float fuzz = NT(u, v, 60f, 47f);
                var hemp = Color.Lerp(new Color(.44f, .39f, .28f), new Color(.76f, .69f, .52f), Mathf.Pow(s, .7f));
                hemp = Color.Lerp(hemp, new Color(.55f, .52f, .45f), fuzz * .25f);
                return Op(hemp);
            },
            (u, v) => Mathf.Abs(Mathf.Sin((u * 3f + v) * Mathf.PI)) * .8f + NT(u, v, 60f, 47f) * .2f, 3.5f, .06f, 256);

        /// <summary>A cut spruce stick, bark shaved off: the crossbars of the волокуша and the packing spacers.</summary>
        static Material RawWoodGen() => PropTextures.Pair("SkiRawWood",
            (u, v) =>
            {
                float grain = NP(u * 4f, v * 60f, 1f, 53f);
                float knot = Mathf.Pow(NT(u, v, 5f, 59f), 8f);
                var wood = Color.Lerp(new Color(.79f, .68f, .5f), new Color(.6f, .49f, .34f), grain * .6f);
                wood = Color.Lerp(wood, new Color(.36f, .26f, .17f), knot * .8f);
                return Op(wood);
            },
            (u, v) => NP(u * 4f, v * 60f, 1f, 53f) * .3f - Mathf.Pow(NT(u, v, 5f, 59f), 8f) * .3f, 2f, .1f, 256);

        // ------------------------------------------------------------------ the board: a lofted section swept from tail to tip

        /// <summary>One cross-section of the board. The section plane is spanned by +X and <see cref="Up"/>; <see cref="C"/> is the
        /// centre of the running surface, so section y is measured up from the base and x across the ski.</summary>
        struct St
        {
            public Vector3 C, Up;
            public float W;   // half width
            public float T;   // thickness
            public float G;   // groove depth here (fades out at both ends)
            public float S;   // arc length from the tail, metres
        }

        /// <summary>Height of the running surface over the snow: camber lifts the middle, the last 27 cm curl up into the tip.</summary>
        static float BaseY(float z)
        {
            float y = 0f;
            if (z > -.76f && z < .62f) y += Camber * Mathf.Sin((z + .76f) / 1.38f * Mathf.PI);
            if (z > TipStart) { float u = (z - TipStart) / (TipZ - TipStart); y += TipRise * u * u * (1.1f - .1f * u); }
            return y;
        }

        static float HalfWidth(float z)
        {
            float w = z < .35f
                ? Mathf.Lerp(TailHalf, WaistHalf, Mathf.InverseLerp(TailZ, .35f, z))
                : Mathf.Lerp(WaistHalf, ShovelHalf, Mathf.InverseLerp(.35f, .80f, Mathf.Min(z, .80f)));
            if (z > .93f) w *= Mathf.Sqrt(Mathf.Max(.10f, 1f - Mathf.Pow((z - .93f) / (TipZ - .93f), 1.7f)));
            return w;
        }

        static float Thick(float z)
        {
            if (z < -.05f) return Mathf.Lerp(ThickTail, ThickFoot, Mathf.InverseLerp(TailZ, -.05f, z));
            if (z < .55f) return Mathf.Lerp(ThickFoot, .0115f, Mathf.InverseLerp(-.05f, .55f, z));
            return Mathf.Lerp(.0115f, ThickTip, Mathf.InverseLerp(.55f, TipZ, z));
        }

        static float GrooveAt(float z) => GrooveDepth * Noise.Smooth(-.84f, -.72f, z) * Noise.Smooth(.80f, .68f, z);

        /// <summary>Top of the board over the snow at z (where bindings and straps sit).</summary>
        static float TopY(float z) => BaseY(z) + Thick(z);

        static St[] Stations(bool detail)
        {
            var zs = new List<float>();
            float step = detail ? .045f : .18f;
            for (float z = TailZ; z < TipStart - 1e-4f; z += step) zs.Add(z);
            int tipSteps = detail ? 12 : 4;
            for (int i = 0; i <= tipSteps; i++) zs.Add(Mathf.Lerp(TipStart, TipZ, i / (float)tipSteps));
            var st = new St[zs.Count];
            float arc = 0f;
            for (int i = 0; i < zs.Count; i++)
            {
                float z = zs[i];
                float dy = (BaseY(z + .004f) - BaseY(z - .004f)) / .008f;
                st[i] = new St
                {
                    C = new Vector3(0, BaseY(z), z),
                    Up = new Vector3(0, 1, -dy).normalized,
                    W = HalfWidth(z), T = Thick(z), G = GrooveAt(z),
                };
                if (i > 0) arc += (st[i].C - st[i - 1].C).magnitude;
                st[i].S = arc;
            }
            return st;
        }

        /// <summary>The stations whose z falls inside [from, to] — used for parts that cover only a stretch of the ski (steel edging).</summary>
        static St[] Range(St[] st, float from, float to)
        {
            var list = new List<St>();
            foreach (var s in st) if (s.C.z >= from && s.C.z <= to) list.Add(s);
            return list.ToArray();
        }

        static Vector3 At(St s, float x, float y) => s.C + Vector3.right * x + s.Up * y;

        /// <summary>Quad strip running tail to tip between two section rails. The face points along (along × A→B), so the order of the
        /// two rails decides which way it looks: for the top face A→B goes +X, for the base −X, for the left wall +Y, for the right −Y.</summary>
        static void Strip(MeshBuilder mb, int sub, St[] st, Func<St, Vector2> a, Func<St, Vector2> b, float ua, float ub, float vScale)
        {
            for (int i = 0; i + 1 < st.Length; i++)
            {
                St p = st[i], q = st[i + 1];
                Vector2 a0 = a(p), a1 = a(q), b1 = b(q), b0 = b(p);
                mb.Quad(sub, At(p, a0.x, a0.y), At(q, a1.x, a1.y), At(q, b1.x, b1.y), At(p, b0.x, b0.y),
                    new Vector2(ua, p.S * vScale), new Vector2(ua, q.S * vScale), new Vector2(ub, q.S * vScale), new Vector2(ub, p.S * vScale));
            }
        }

        /// <summary>The arch of the top face dies away at both ends, so the end caps meet the top surface exactly.</summary>
        static float CrownAt(float z) => Crown * Noise.Smooth(TailZ, TailZ + .06f, z) * Noise.Smooth(TipZ, TipZ - .06f, z);

        static Vector2 TopSec(St s, float f) => new Vector2(f * s.W, s.T + CrownAt(s.C.z) * (1f - f * f));

        /// <summary>The wooden board with its groove, steel edging and screws. <paramref name="detail"/> false is the far LOD:
        /// no groove, no edging, one span across the top.</summary>
        static void Board(MeshBuilder mb, bool detail)
        {
            var st = Stations(detail);
            int spans = detail ? 4 : 1;
            for (int k = 0; k < spans; k++)
            {
                float f0 = -1f + 2f * k / spans, f1 = -1f + 2f * (k + 1) / spans;
                Strip(mb, SubLacquer, st, s => TopSec(s, f0), s => TopSec(s, f1), (f0 + 1) * .5f, (f1 + 1) * .5f, 1f);
            }
            // side walls, sawn through the lamination (u across the thickness, v along the ski)
            Strip(mb, SubLaminate, st, s => new Vector2(-s.W, 0), s => new Vector2(-s.W, s.T), 0f, 1f, 2f);
            Strip(mb, SubLaminate, st, s => new Vector2(s.W, s.T), s => new Vector2(s.W, 0), 0f, 1f, 2f);
            if (detail)
            {
                // running surface: flat, groove wall, groove floor, groove wall, flat
                Strip(mb, SubBase, st, s => new Vector2(s.W, 0), s => new Vector2(GrooveHalf, 0), 1f, .55f, 1f);
                Strip(mb, SubBase, st, s => new Vector2(GrooveHalf, 0), s => new Vector2(GrooveHalf * .62f, s.G), .55f, .5f, 1f);
                Strip(mb, SubBase, st, s => new Vector2(GrooveHalf * .62f, s.G), s => new Vector2(-GrooveHalf * .62f, s.G), .52f, .48f, 1f);
                Strip(mb, SubBase, st, s => new Vector2(-GrooveHalf * .62f, s.G), s => new Vector2(-GrooveHalf, 0), .5f, .45f, 1f);
                Strip(mb, SubBase, st, s => new Vector2(-GrooveHalf, 0), s => new Vector2(-s.W, 0), .45f, 0f, 1f);
            }
            else Strip(mb, SubBase, st, s => new Vector2(s.W, 0), s => new Vector2(-s.W, 0), 1f, 0f, 1f);

            // tail and tip end faces
            St t0 = st[0], t1 = st[st.Length - 1];
            mb.Quad(SubLaminate, At(t0, -t0.W, 0), At(t0, -t0.W, t0.T), At(t0, t0.W, t0.T), At(t0, t0.W, 0),
                Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
            mb.Quad(SubLaminate, At(t1, -t1.W, 0), At(t1, t1.W, 0), At(t1, t1.W, t1.T), At(t1, -t1.W, t1.T),
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            if (!detail) return;

            // steel edging: an L of two strips along each base edge, standing a hair proud of the wood, over the running stretch only
            var edge = Range(st, TailZ, .88f);
            const float Yb = -EdgeProud, Band = .004f;
            // left side: the wall strip looks -X (rails run up), the base strip looks -Y (rails run -X)
            Strip(mb, SubSteel, edge, s => new Vector2(-s.W - EdgeProud, 0f), s => new Vector2(-s.W - EdgeProud, Band), 0f, 1f, 6f);
            Strip(mb, SubSteel, edge, s => new Vector2(-s.W + EdgeWidth, Yb), s => new Vector2(-s.W - EdgeProud, Yb), 1f, 0f, 6f);
            // right side
            Strip(mb, SubSteel, edge, s => new Vector2(s.W + EdgeProud, Band), s => new Vector2(s.W + EdgeProud, 0f), 1f, 0f, 6f);
            Strip(mb, SubSteel, edge, s => new Vector2(s.W + EdgeProud, Yb), s => new Vector2(s.W - EdgeWidth, Yb), 0f, 1f, 6f);
            // countersunk screws holding the edging, every 18 cm
            for (float z = -.84f; z < .86f; z += .18f)
                foreach (int sgn in new[] { -1, 1 })
                    Screw(mb, new Vector3(sgn * (HalfWidth(z) - EdgeWidth * .5f), TopY(z), z), .0022f);
        }

        /// <summary>A countersunk wood screw seen from the top: a small cone of head with a slot cut across it.</summary>
        static void Screw(MeshBuilder mb, Vector3 top, float r)
        {
            Sweep(mb, SubSteel, new[] { top + Vector3.up * .0006f, top - Vector3.up * .0012f }, (t, a) => Mathf.Lerp(r, r * .55f, t), 6, 1, new Options(), true, true);
            BoxM(mb, SubSteel, Matrix4x4.identity, top + Vector3.up * .0007f, new Vector3(r * 1.9f, .0004f, r * .5f), Quaternion.identity, 40f);
        }

        // ------------------------------------------------------------------ binding: toe iron, springs, bail, straps, felt pad

        /// <summary>Semi-rigid touring binding of the period: a pressed-steel toe iron on a screwed base plate, two coil springs
        /// pulling a steel bail around the heel, a toe strap and a heel strap of harness leather, and a felt pad where the heel lands.
        /// <paramref name="side"/> +1 on the right ski, −1 on the left: the buckles and the tightening lever change sides.</summary>
        static void Binding(MeshBuilder mb, float side, bool detail)
        {
            float plate = TopY(.07f) + .0008f;          // top of the base plate
            float iron = plate + .0008f;                // the irons stand on it
            float yb = TopY(0f) + .028f;                // height the bail runs at
            var I = Matrix4x4.identity;

            BoxM(mb, SubSteel, I, new Vector3(0, plate, .070f), new Vector3(.090f, .0016f, .098f), Quaternion.identity, 25f);
            foreach (int s in new[] { -1, 1 })
            {
                BoxM(mb, SubSteel, I, new Vector3(s * .0415f, iron + .019f, .071f), new Vector3(.003f, .038f, .084f), Quaternion.identity, 25f);
                // the top edge of the iron is rolled outwards so it does not cut the boot
                BoxM(mb, SubSteel, I, new Vector3(s * .0435f, iron + .0375f, .071f), new Vector3(.0055f, .0025f, .084f), Quaternion.Euler(0, 0, -s * 18f), 25f);
            }
            BoxM(mb, SubSteel, I, new Vector3(0, iron + .016f, .1135f), new Vector3(.086f, .032f, .003f), Quaternion.identity, 25f);
            // heel plate with an anti-slip ridge, on top of the felt
            BoxM(mb, SubSteel, I, new Vector3(0, TopY(-.055f) + .0075f, -.055f), new Vector3(.052f, .003f, .022f), Quaternion.identity, 25f);
            BoxM(mb, SubSteel, I, new Vector3(0, TopY(-.055f) + .0105f, -.055f), new Vector3(.052f, .003f, .006f), Quaternion.identity, 25f);
            // felt under the heel
            BoxM(mb, SubFelt, I, new Vector3(0, TopY(-.07f) + .003f, -.070f), new Vector3(.072f, .006f, .086f), Quaternion.identity, 14f);

            if (!detail)
            {
                // far LOD: the bail as a single low-poly loop, no springs, no straps
                Sweep(mb, SubSteel, new[] { new Vector3(.046f, yb, .060f), new Vector3(.052f, yb, -.040f), new Vector3(0, yb, -.105f), new Vector3(-.052f, yb, -.040f), new Vector3(-.046f, yb, .060f) },
                    (t, a) => .0028f, 4, 8, new Options(), true, true);
                return;
            }

            // bail: a 4 mm steel rod from the outside of each iron, round the heel
            var bail = new[]
            {
                new Vector3(.046f, yb, .062f), new Vector3(.0535f, yb - .002f, .004f), new Vector3(.0505f, yb - .004f, -.055f),
                new Vector3(.030f, yb - .005f, -.098f), new Vector3(0, yb - .005f, -.108f), new Vector3(-.030f, yb - .005f, -.098f),
                new Vector3(-.0505f, yb - .004f, -.055f), new Vector3(-.0535f, yb - .002f, .004f), new Vector3(-.046f, yb, .062f),
            };
            Sweep(mb, SubSteel, bail, (t, a) => .0021f, 6, 30, new Options { UvScale = new Vector2(1, 30f) }, true, true);
            // the two coil springs on the front legs of the bail
            foreach (int s in new[] { -1, 1 }) Coil(mb, new Vector3(s * .0462f, yb, .058f), new Vector3(s * .0522f, yb - .002f, .010f), .0048f, .0011f, 6);
            // lugs where the bail hooks into the irons
            foreach (int s in new[] { -1, 1 })
                Sweep(mb, SubSteel, new[] { new Vector3(s * .0395f, yb, .062f), new Vector3(s * .0475f, yb, .062f) }, (t, a) => .0035f, 8, 1, new Options { UvScale = new Vector2(1, 20f) }, true, true);

            // toe strap over the boot, buckle on the outer side
            Ribbon(mb, SubLeather, new[]
            {
                new Vector3(-.0425f, yb - .003f, .080f), new Vector3(-.022f, yb + .029f, .084f),
                new Vector3(.022f, yb + .029f, .084f), new Vector3(.0425f, yb - .003f, .080f),
            }, .030f, .0026f, Vector3.up, I, 12);
            Buckle(mb, new Vector3(side * .034f, yb + .015f, .082f), Quaternion.Euler(0, 0, side * 62f), .022f, .016f);
            // its loose end hanging past the buckle
            Ribbon(mb, SubLeather, new[]
            {
                new Vector3(side * .036f, yb + .010f, .082f), new Vector3(side * .048f, yb - .010f, .076f), new Vector3(side * .052f, yb - .034f, .066f),
            }, .022f, .0024f, Vector3.forward, I, 8);

            // heel strap: a flat belt swept round the heel, so it keeps its width whichever way it turns
            var heel = new[]
            {
                new Vector3(-.0495f, yb + .012f, -.050f), new Vector3(-.0555f, yb + .026f, -.093f), new Vector3(0, yb + .034f, -.120f),
                new Vector3(.0555f, yb + .026f, -.093f), new Vector3(.0495f, yb + .012f, -.050f),
            };
            Sweep(mb, SubLeather, heel, (t, a) => 1f, 8, 18, new Options { UvScale = new Vector2(1, 8f) }, true, true, -1,
                (t, a) => new Vector2(Mathf.Cos(a) * .0014f, Mathf.Sin(a) * .0115f));
            Buckle(mb, new Vector3(side * .040f, yb + .020f, -.072f), Quaternion.Euler(0, side * 68f, 20f), .020f, .015f);
            // keepers that tie the straps down to the irons
            foreach (int s in new[] { -1, 1 })
                Sweep(mb, SubLeather, new[] { new Vector3(s * .0425f, yb - .006f, .080f), new Vector3(s * .0425f, yb - .014f, .080f) },
                    (t, a) => 1f, 6, 2, new Options { UvScale = new Vector2(1, 20f) }, true, true, -1, (t, a) => new Vector2(Mathf.Cos(a) * .0022f, Mathf.Sin(a) * .008f));

            // screws through the plate and the heel plate
            foreach (int s in new[] { -1, 1 })
            {
                Screw(mb, new Vector3(s * .0355f, plate + .0004f, .036f), .0026f);
                Screw(mb, new Vector3(s * .0355f, plate + .0004f, .104f), .0026f);
                Screw(mb, new Vector3(s * .0195f, TopY(-.055f) + .0085f, -.055f), .0024f);
            }
        }

        /// <summary>Helical wire spring around the axis a→b.</summary>
        static void Coil(MeshBuilder mb, Vector3 a, Vector3 b, float coil, float wire, int turns)
        {
            var axis = (b - a).normalized;
            var p1 = Vector3.Cross(axis, Vector3.up);
            if (p1.sqrMagnitude < .01f) p1 = Vector3.Cross(axis, Vector3.right);
            p1.Normalize();
            var p2 = Vector3.Cross(axis, p1);
            int n = turns * 5;
            var path = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, ang = t * turns * Mathf.PI * 2;
                path[i] = Vector3.Lerp(a, b, t) + (p1 * Mathf.Cos(ang) + p2 * Mathf.Sin(ang)) * coil;
            }
            Sweep(mb, SubSteel, path, (t, ang) => wire, 5, n, new Options { UvScale = new Vector2(1, 20f) }, true, true);
        }

        /// <summary>Strap buckle: a steel frame with a centre bar and a tongue, lying in the local XY plane.</summary>
        static void Buckle(MeshBuilder mb, Vector3 c, Quaternion rot, float w, float h)
        {
            const float Wire = .0016f;
            BoxM(mb, SubSteel, Matrix4x4.identity, c + rot * new Vector3(0, h / 2, 0), new Vector3(w, Wire, Wire * 1.4f), rot, 60f);
            BoxM(mb, SubSteel, Matrix4x4.identity, c + rot * new Vector3(0, -h / 2, 0), new Vector3(w, Wire, Wire * 1.4f), rot, 60f);
            BoxM(mb, SubSteel, Matrix4x4.identity, c + rot * new Vector3(-w / 2, 0, 0), new Vector3(Wire, h, Wire * 1.4f), rot, 60f);
            BoxM(mb, SubSteel, Matrix4x4.identity, c + rot * new Vector3(w / 2, 0, 0), new Vector3(Wire, h, Wire * 1.4f), rot, 60f);
            BoxM(mb, SubSteel, Matrix4x4.identity, c, new Vector3(Wire, h, Wire), rot, 60f);                                  // centre bar
            BoxM(mb, SubSteel, Matrix4x4.identity, c + rot * new Vector3(w * .22f, 0, .001f), new Vector3(w * .44f, Wire * .8f, Wire * .7f), rot, 60f); // tongue
        }

        // ------------------------------------------------------------------ prefab assembly

        static Mesh SaveMesh(MeshBuilder mb, string name)
        {
            Directory.CreateDirectory(MeshDir);
            string path = MeshDir + "/" + name + ".asset";
            var mesh = mb.ToMesh(name);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void Marker(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
        }

        /// <summary>Saves a two-LOD prefab with the shared material list. <paramref name="lods"/>: the screen heights where LOD0→1 and 1→culled.</summary>
        static GameObject Save(string name, MeshBuilder lod0, MeshBuilder lod1, float[] lods, Action<Transform> markers = null)
        {
            Directory.CreateDirectory(PrefabDir);
            var root = new GameObject(name);
            var mats = Mats;
            var renderers = new List<Renderer[]>();
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(name + "_LOD" + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = SaveMesh(i == 0 ? lod0 : lod1, name + "_LOD" + i);
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = i == 0 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers.Add(new Renderer[] { mr });
            }
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(lods[0], renderers[0]), new LOD(lods[1], renderers[1]) });
            lg.fadeMode = LODFadeMode.None;
            lg.RecalculateBounds();
            markers?.Invoke(root.transform);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ pole

        /// <summary>A closed turn of strap or rope round a bundle: a superellipse through <paramref name="c"/> spanned by a and b,
        /// so it squares off over a flat load instead of hooping round it.</summary>
        static void Turn(MeshBuilder mb, int sub, Vector3 c, Vector3 a, Vector3 b, Func<float, float, float> radius, int sides, int segments, float uv, Func<float, float, Vector2> section = null)
        {
            var path = new Vector3[13];
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f * Mathf.PI * 2, ca = Mathf.Cos(t), sa = Mathf.Sin(t);
                path[i] = c + a * (Mathf.Sign(ca) * Mathf.Pow(Mathf.Abs(ca), .62f)) + b * (Mathf.Sign(sa) * Mathf.Pow(Mathf.Abs(sa), .62f));
            }
            Sweep(mb, sub, path, radius, sides, segments, new Options { UvScale = new Vector2(1, uv) }, false, false, -1, section);
        }

        /// <summary>Flat leather belt cross-section for <see cref="Sweep"/> (radius 1): <paramref name="half"/> across, <paramref name="thick"/> deep.</summary>
        static Func<float, float, Vector2> Belt(float half, float thick) => (t, a) => new Vector2(Mathf.Cos(a) * thick, Mathf.Sin(a) * half);

        /// <summary>Bamboo pole, 1.30 m: ferrule tip at the origin, shaft up +Y. Nodes every 28 cm, leather grip and wrist loop,
        /// a basket of a bent cane ring with leather thongs, a steel ferrule.</summary>
        static void PoleBuilder(MeshBuilder mb, bool detail)
        {
            var o = new Options { UvScale = new Vector2(1, 1f / (2f * NodeSpacing)) };
            var foot = new Vector3(0, .014f, 0); var top = new Vector3(0, PoleLength, 0);
            float len = PoleLength - .014f;
            Sweep(mb, SubBamboo, new[] { foot, top }, (t, ang) =>
            {
                float metres = t * len;
                float node = Mathf.Pow(Mathf.Abs(Mathf.Cos(metres / NodeSpacing * Mathf.PI)), 40f);
                return Mathf.Lerp(.0130f, .0098f, t) * (1f + .11f * node);
            }, detail ? 9 : 5, detail ? 44 : 8, o, true, true);

            // steel ferrule and its collar
            Sweep(mb, SubSteel, new[] { new Vector3(0, .050f, 0), Vector3.zero }, (t, a) => Mathf.Lerp(.0092f, .0013f, t * t), detail ? 8 : 5, 2, new Options { UvScale = new Vector2(1, 20f) }, true, true);
            if (!detail)
            {
                Turn(mb, SubBamboo, new Vector3(0, .118f, 0), Vector3.right * .062f, Vector3.forward * .062f, (t, a) => .0045f, 4, 10, 3.6f);
                return;
            }
            Sweep(mb, SubSteel, new[] { new Vector3(0, .050f, 0), new Vector3(0, .064f, 0) }, (t, a) => .0108f, 8, 1, new Options { UvScale = new Vector2(1, 20f) }, true, true);

            // basket: a bent cane ring on four leather thongs, lashed to a collar on the shaft
            var c = new Vector3(0, .118f, 0);
            Turn(mb, SubBamboo, c, Vector3.right * .062f, Vector3.forward * .062f, (t, a) => .0045f, 6, 24, 3.6f);
            foreach (var d in new[] { Vector3.right, Vector3.forward, (Vector3.right + Vector3.forward).normalized, (Vector3.right - Vector3.forward).normalized })
                Sweep(mb, SubLeather, new[] { c - d * .062f, c + d * .062f }, (t, a) => .0026f, 5, 2, new Options { UvScale = new Vector2(1, 8f) }, true, true);
            Sweep(mb, SubLeather, new[] { new Vector3(0, .108f, 0), new Vector3(0, .130f, 0) }, (t, a) => .0155f, 10, 1, new Options { UvScale = new Vector2(1, 10f) }, true, true);

            // leather grip and the cap over the end of the culm
            Sweep(mb, SubLeather, new[] { new Vector3(0, PoleLength - .175f, 0), new Vector3(0, PoleLength + .003f, 0) }, (t, a) => Mathf.Lerp(.0143f, .0132f, t), 10, 4, new Options { UvScale = new Vector2(1, 6f) }, false, true);
            // wrist loop hanging off the top of the grip
            Sweep(mb, SubLeather, new[]
            {
                new Vector3(.012f, PoleLength - .018f, 0), new Vector3(.034f, PoleLength - .072f, .020f), new Vector3(.016f, PoleLength - .150f, .030f),
                new Vector3(-.016f, PoleLength - .118f, .012f), new Vector3(-.013f, PoleLength - .034f, -.003f),
            }, (t, a) => 1f, 6, 18, new Options { UvScale = new Vector2(1, 10f) }, true, true, -1, Belt(.0085f, .0014f));
        }

        // ------------------------------------------------------------------ prefabs

        static void SkiBuilder(MeshBuilder mb, float side, bool detail) { Board(mb, detail); Binding(mb, side, detail); }

        static MeshBuilder Ski(float side, bool detail) { var mb = new MeshBuilder(Subs); SkiBuilder(mb, side, detail); return mb; }

        static MeshBuilder Pole(bool detail) { var mb = new MeshBuilder(Subs); PoleBuilder(mb, detail); return mb; }

        /// <summary>Both skis of a pair. Pivot on the snow under the middle of the binding, +Z to the tip; the board itself rides
        /// <see cref="Camber"/> higher there, because that is what camber is.</summary>
        static void SingleSki(string name, float side)
        {
            Save(name, Ski(side, true), Ski(side, false), new[] { .06f, .006f }, t =>
            {
                Marker(t, "Boot", new Vector3(0, TopY(.055f), .055f));      // where the ball of the foot sits
                Marker(t, "Tip", new Vector3(0, BaseY(TipZ), TipZ));
                Marker(t, "Tail", new Vector3(0, BaseY(TailZ), TailZ));
            });
        }

        /// <summary>The pair tied base to base with the tips up and the poles lashed alongside — what rides on the rucksack when the
        /// group walks. Pivot between the two tails at the bottom, the bundle standing along +Y, tips at 2 m.</summary>
        static MeshBuilder Packed(bool detail)
        {
            var mb = new MeshBuilder(Subs);
            var ski = Ski(1f, detail); var pole = Pole(detail);
            // +Z of the ski becomes +Y; the two boards face each other with their running surfaces, so the tips splay apart
            var up = Quaternion.Euler(-90, 0, 0);
            mb.Append(ski, Matrix4x4.TRS(new Vector3(0, -TailZ, -.0015f), up, Vector3.one));
            mb.Append(Ski(-1f, detail), Matrix4x4.TRS(new Vector3(0, -TailZ, .0015f), Quaternion.Euler(0, 180, 0) * up, Vector3.one));
            foreach (int s in new[] { -1, 1 })
                mb.Append(pole, Matrix4x4.TRS(new Vector3(s * .070f, .055f, s * .004f), Quaternion.Euler(0, s * 90f, 0), Vector3.one));
            if (!detail) return mb;
            // two leather straps and a turn of rope holding the bundle together
            foreach (float y in new[] { .50f, 1.26f })
                Turn(mb, SubLeather, new Vector3(0, y, 0), Vector3.right * .090f, Vector3.forward * .023f, (t, a) => 1f, 6, 24, 8f, Belt(.013f, .0022f));
            // two turns of rope well clear of the bindings (they sit around y = 0.95)
            foreach (float y in new[] { .735f, .752f })
                Turn(mb, SubRope, new Vector3(0, y, 0), Vector3.right * .088f, Vector3.forward * .021f, (t, a) => .0045f, 6, 24, 50f);
            // the carrying loop the rucksack straps run through
            Sweep(mb, SubLeather, new[] { new Vector3(-.030f, 1.275f, .020f), new Vector3(0, 1.335f, .050f), new Vector3(.030f, 1.275f, .020f) },
                (t, a) => 1f, 6, 12, new Options { UvScale = new Vector2(1, 6f) }, true, true, -1, Belt(.012f, .002f));
            return mb;
        }

        /// <summary>Волокуша: both skis flat on the snow, three lashed crossbars and a rope harness the rucksack is tied to.
        /// Pivot on the snow in the middle of the sled, +Z the way it is dragged (towards the tips).</summary>
        static MeshBuilder Sled(bool detail)
        {
            var mb = new MeshBuilder(Subs);
            mb.Append(Ski(-1f, detail), Matrix4x4.TRS(new Vector3(-.14f, 0, 0), Quaternion.identity, Vector3.one));
            mb.Append(Ski(1f, detail), Matrix4x4.TRS(new Vector3(.14f, 0, 0), Quaternion.identity, Vector3.one));
            float[] bars = { -.62f, -.20f, .52f };
            foreach (float z in bars)
            {
                float y = TopY(z) + .017f;
                Sweep(mb, SubWood, new[] { new Vector3(-.215f, y, z), new Vector3(.215f, y - .001f, z) }, (t, a) => Mathf.Lerp(.016f, .014f, t), detail ? 8 : 5, detail ? 3 : 1,
                    new Options { UvScale = new Vector2(1, 3f) }, true, true);
                if (!detail) continue;
                foreach (int s in new[] { -1, 1 })
                {
                    var c = new Vector3(s * .14f, TopY(z) + .008f, z);
                    Turn(mb, SubRope, c, Vector3.up * .030f, Vector3.forward * .034f, (t, a) => .0038f, 5, 18, 50f);
                    Turn(mb, SubRope, c + Vector3.forward * .012f, Vector3.up * .030f, Vector3.forward * .030f, (t, a) => .0038f, 5, 18, 50f);
                }
            }
            if (!detail) return mb;
            // harness: two ropes from the front crossbar forward into a loop the rucksack is tied to
            foreach (int s in new[] { -1, 1 })
                Sweep(mb, SubRope, new[]
                {
                    new Vector3(s * .145f, TopY(.52f) + .020f, .52f), new Vector3(s * .120f, .075f, .78f),
                    new Vector3(s * .055f, .110f, 1.02f), new Vector3(0, .125f, 1.15f),
                }, (t, a) => .0048f, 5, 14, new Options { UvScale = new Vector2(1, 50f) }, true, true);
            Turn(mb, SubRope, new Vector3(0, .128f, 1.20f), Vector3.right * .035f, Vector3.forward * .045f, (t, a) => .0045f, 5, 18, 50f);
            // the running surfaces are waxed for dragging: a scrap of felt lashed over the front crossbar to keep the load off the wood
            BoxM(mb, SubFelt, Matrix4x4.identity, new Vector3(0, TopY(.52f) + .032f, .50f), new Vector3(.22f, .006f, .10f), Quaternion.Euler(-4, 0, 0), 12f);
            return mb;
        }

        /// <summary>Builds every ski prefab. Self-contained: call it once from the world pipeline.</summary>
        public static void Build()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(MeshDir);
            cache.Clear();
            SingleSki("Ski_Right", 1f);
            SingleSki("Ski_Left", -1f);
            Save("Pole_Bamboo", Pole(true), Pole(false), new[] { .05f, .005f }, t =>
            {
                Marker(t, "Grip", new Vector3(0, PoleLength - .09f, 0));
                Marker(t, "Basket", new Vector3(0, .118f, 0));
            });
            Save("Ski_Pair_Packed", Packed(true), Packed(false), new[] { .08f, .008f }, t =>
            {
                Marker(t, "Carry", new Vector3(0, 1.325f, .045f));
                Marker(t, "Tips", new Vector3(0, -TailZ + TipZ, 0));
            });
            Save("Ski_Sled", Sled(true), Sled(false), new[] { .09f, .01f }, t =>
            {
                Marker(t, "Hitch", new Vector3(0, .128f, 1.20f));
                Marker(t, "Load", new Vector3(0, TopY(.1f) + .03f, .05f));
            });
            Debug.Log("1079 gear: skis, poles, packed pair and волокуша rebuilt in " + PrefabDir);
        }
    }
}
