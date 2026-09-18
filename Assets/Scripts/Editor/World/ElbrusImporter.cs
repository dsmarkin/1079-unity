using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Core;
using Debug = UnityEngine.Debug;

namespace Height1079.EditorTools.World
{
    /// <summary>Second location: Assets/Data/World/elbrus → Resources/World/Elbrus.asset + the Elbrus prefab library.
    /// The terrain is 12 288 m square on a 6 m grid (2049² heightmap), so one Unity terrain covers Azau, both summits
    /// and everything the ropeways and the route touch. Deterministic, like the Kholat importer.</summary>
    public static class ElbrusImporter
    {
        public const string TerrainAsset = WorldPaths.Generated + "/Elbrus.asset";
        public const string HeightResource = WorldPaths.Generated + "/elbrus_height_2049.bytes";
        public static readonly string Source = WorldPaths.Source + "/elbrus";
        public const float BaseHeight = Elbrus.HeightMin - 6f;
        public static float Range => Elbrus.HeightMax - BaseHeight;
        const int Alpha = 1025;
        const int LayerCount = 4;
        /// <summary>Detail map: 2 048 cells over 12 288 m, so one cell of grass density is 6 m square.</summary>
        const int DetailRes = 2048;
        /// <summary>Cells per detail patch. One patch is one mesh per grass kind, so a big patch both pops in as a whole
        /// block at the draw distance (95 m) and risks overflowing the 16-bit index buffer once the meadow gets dense.
        /// Eight cells = a 48 m patch: fine-grained culling and a few thousand quads per mesh even at peak density.</summary>
        const int DetailPerPatch = 8;

        public static bool SourcePresent => File.Exists($"{Source}/height_2049.r16");

        public static void Build()
        {
            if (!SourcePresent)
            {
                Debug.LogWarning("1079: Assets/Data/World/elbrus/height_2049.r16 отсутствует — локация «Эльбрус» не собрана (см. Tools/terrain/elbrus.py).");
                return;
            }
            var clock = Stopwatch.StartNew();

            AssetDatabase.StartAssetEditing();
            try { File.Copy($"{Source}/height_2049.r16", HeightResource, true); }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.ImportAsset(HeightResource);

            var dem = HeightField.FromR16(File.ReadAllBytes($"{Source}/height_2049.r16"), Elbrus.HeightMin, Elbrus.HeightMax,
                Elbrus.Resolution, Elbrus.GridStep);
            var rock = File.ReadAllBytes($"{Source}/rock_1025.r8");
            var ash = File.ReadAllBytes($"{Source}/ash_1025.r8");

            EditorUtility.DisplayProgressBar("1079 Эльбрус", "Станции, кабины, приюты", .1f);
            ElbrusFactory.Build();
            ElbrusProps.Build();

            EditorUtility.DisplayProgressBar("1079 Эльбрус", "Рельеф", .4f);
            var data = new TerrainData { heightmapResolution = Elbrus.Resolution };
            data.size = new Vector3(Elbrus.Size, Range, Elbrus.Size);
            int N = Elbrus.Resolution;
            var h = new float[N, N];
            for (int r = 0; r < N; r++)
                for (int c = 0; c < N; c++)
                    h[r, c] = (dem.At(c, r) - BaseHeight) / Range;
            data.SetHeights(0, 0, h);

            EditorUtility.DisplayProgressBar("1079 Эльбрус", "Слои и текстуры склона", .7f);
            var layers = Layers();
            var splat = Splat(dem, rock, ash);
            data.terrainLayers = layers;
            data.alphamapResolution = Alpha;
            data.SetAlphamaps(0, 0, splat);
            data.baseMapResolution = 1024;
            Report(data, "после SetAlphamaps");

            EditorUtility.DisplayProgressBar("1079 Эльбрус", "Сосновый лес Баксана и валуны", .85f);
            Scatter(data, dem, rock);
            var meadow = Meadow(dem);
            ApplyMeadow(data, meadow.protos, meadow.density);

            EditorUtility.DisplayProgressBar("1079 Эльбрус", "Маршрут: вешки, перила, хижина, трещины", .9f);
            ElbrusAscent.Build(dem);

            EditorUtility.DisplayProgressBar("1079 Эльбрус", "Карта района", .92f);
            ElbrusMapFactory.Build(dem);

            Report(data, "перед сохранением");
            AssetDatabase.DeleteAsset(TerrainAsset);
            AssetDatabase.CreateAsset(data, TerrainAsset);

            // Belt and braces: the splat has been lost on the way into the asset before, and the slope then rendered as
            // unbroken snow. Write it — and the meadow, which travels the same road — once more into the saved asset and
            // check what actually got stored.
            var saved = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAsset);
            if (saved != null)
            {
                saved.terrainLayers = layers;
                saved.alphamapResolution = Alpha;
                saved.SetAlphamaps(0, 0, splat);
                ApplyMeadow(saved, meadow.protos, meadow.density);
                EditorUtility.SetDirty(saved);
                AssetDatabase.SaveAssets();
                Report(saved, "в сохранённом ассете");
            }
            EditorUtility.ClearProgressBar();
            Debug.Log($"1079 Эльбрус собран за {clock.Elapsed.TotalSeconds:0.0} с: {Elbrus.Size:0} м, {Elbrus.Ropeways.Length} канатных дорог");
        }

        /// <summary>What the splat map says is under the visitor's feet at Azau (2 350 m): there must be no snow there.
        /// Then the same question for the meadow: how many grass tufts the detail layers really carry around the square.
        /// Both maps have gone missing on the way into the asset before, and both are invisible until the build runs.</summary>
        static void Report(TerrainData data, string when)
        {
            var (x, z) = Elbrus.Start;
            int n = data.alphamapResolution;
            int c = Mathf.Clamp(Mathf.RoundToInt((x + Elbrus.Half) / Elbrus.Size * (n - 1)), 0, n - 1);
            int r = Mathf.Clamp(Mathf.RoundToInt((z + Elbrus.Half) / Elbrus.Size * (n - 1)), 0, n - 1);
            var a = data.GetAlphamaps(c, r, 1, 1);
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < data.alphamapLayers; k++) sb.Append($" {k}={a[0, 0, k]:0.00}");
            Debug.Log($"1079 Эльбрус: грунт у Азау {when}, слоёв {data.alphamapLayers}:{sb}");
            ReportGrass(data, when);
        }

        /// <summary>Grass around the Azau square: prototypes, detail resolution and the number of instances the density
        /// map actually holds in a window about 400 m across. Zero here means the meadow did not survive the asset.</summary>
        static void ReportGrass(TerrainData data, string when)
        {
            var protos = data.detailPrototypes;
            int w = data.detailWidth, h = data.detailHeight;
            const int Win = 64;
            if (protos.Length == 0 || w < Win || h < Win)
            {
                Debug.Log($"1079 Эльбрус: трава у Азау {when}: слоёв деталей {protos.Length}, карта {w}×{h} — травы нет");
                return;
            }
            var (x, z) = Elbrus.Start;
            int dc = Mathf.Clamp(Mathf.RoundToInt((x + Elbrus.Half) / Elbrus.Size * (w - 1)) - Win / 2, 0, w - Win);
            int dr = Mathf.Clamp(Mathf.RoundToInt((z + Elbrus.Half) / Elbrus.Size * (h - 1)) - Win / 2, 0, h - Win);
            float side = Win * Elbrus.Size / w;
            long total = 0;
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < protos.Length; k++)
            {
                long sum = 0;
                foreach (int v in data.GetDetailLayer(dc, dr, Win, Win, k)) sum += v;
                total += sum;
                var tex = protos[k].prototypeTexture;
                sb.Append($" {(tex != null ? tex.name : "прототип " + k)}={sum}");
            }
            Debug.Log($"1079 Эльбрус: трава у Азау {when}: слоёв деталей {protos.Length}, карта {w}×{h} (ячейка {Elbrus.Size / w:0.0} м), "
                    + $"в окне {side:0} м — {total} пучков ({total / (side * side):0.00} на м²):{sb}");
        }

        /// <summary>0 firn and wind crust, 1 lava rock, 2 alpine turf, 3 moraine scree and ash. Four and no more: the
        /// built-in terrain shader paints four layers in one pass, and the fifth needs an add-pass shader that does not
        /// survive into a player build — the ground then renders blank white, which is exactly how it looked.
        /// The lower slope is what a visitor sees in July: below the Gara-Bashi tongue there is no snow at all, only
        /// grey moraine, lava ribs and green meadow down towards Azau.</summary>
        static TerrainLayer[] Layers() => new[]
        {
            // small tiles and a strong normal, or the slope reads as white paper at arm's length; by July the firn of the
            // Gara-Bashi tongue is old and dusty, so the bake takes a little off the white and cools what is left
            Layer("ElbFirn", "snow_02", 3.5f, .3f, new Color(.9f, .92f, .96f), 1.4f),
            // the lava of Elbrus is dark andesite, almost black where it is fresh, and rock_face_03 is a warm sandstone
            // (average sRGB 131/105/80 — sand). Three quarters of its colour is drained away and what is left is darkened
            // to about 70/63/56: dark grey stone with a brown cast. The tint is baked into the pixels, see Layer().
            Layer("ElbLava", "rock_face_03", 4f, .1f, new Color(.6f, .585f, .55f), 1.1f, .75f),
            // July turf on the southern slope is olive and khaki with burnt straw through it, not a lawn: the two ends of
            // the gradient are dark olive soil and dry straw, and the speckle puts stones and bare ground in between
            Made("ElbTurf", 4f, .06f, TextureFactory.Ground("elb_turf", new Color(.19f, .21f, .13f), new Color(.44f, .43f, .29f), 5f, 91, .24f)),
            Made("ElbScree", 5f, .05f, TextureFactory.Ground("elb_scree", new Color(.22f, .22f, .21f), new Color(.42f, .41f, .38f), 8f, 47, .24f)),
        };

        /// <summary>A layer whose albedo we generate ourselves: the Poly Haven library here has no grass or scree scan.</summary>
        static TerrainLayer Made(string name, float tile, float smoothness, Texture2D albedo)
        {
            string dir = WorldPaths.Generated + "/TerrainLayers";
            Directory.CreateDirectory(dir);
            string path = $"{dir}/{name}.terrainlayer";
            AssetDatabase.DeleteAsset(path);
            var l = new TerrainLayer { diffuseTexture = albedo, tileSize = new Vector2(tile, tile), smoothness = smoothness, metallic = 0 };
            AssetDatabase.CreateAsset(l, path);
            return l;
        }

        /// <summary>A layer built on a Poly Haven scan, with the tint baked into its own copy of the albedo.
        /// It is baked and not set through <c>TerrainLayer.diffuseRemapMin/Max</c> on purpose: the built-in terrain shader
        /// (Nature/Terrain/Standard) never reads that pair — it belongs to the HDRP terrain shader — so a tint put there
        /// shows up in the inspector, changes nothing on screen, and the sandstone scan stayed sand all the way into the
        /// build. <paramref name="desaturate"/> 0..1 drains the scan's own colour before the tint multiplies it, which is
        /// how a warm sandstone becomes grey andesite instead of merely a darker sandstone.</summary>
        static TerrainLayer Layer(string name, string id, float tile, float smoothness, Color tint, float normal = .8f, float desaturate = 0f)
        {
            string dir = WorldPaths.Generated + "/TerrainLayers";
            Directory.CreateDirectory(dir);
            string path = $"{dir}/{name}.terrainlayer";
            AssetDatabase.DeleteAsset(path);
            var baked = TextureFactory.Tinted($"{name}_diff", WorldPaths.PH(id, "diff"), tint, desaturate);
            var l = new TerrainLayer
            {
                diffuseTexture = baked != null ? baked : Materials.Tex(WorldPaths.PH(id, "diff")),
                normalMapTexture = Materials.Tex(WorldPaths.PH(id, "nor_gl")),
                tileSize = new Vector2(tile, tile), smoothness = smoothness, metallic = 0, normalScale = normal,
            };
            AssetDatabase.CreateAsset(l, path);
            return l;
        }

        /// <summary>Where the southern slope shows what, on a July morning. Snow begins where the glaciers do: the tongue of
        /// Gara-Bashi comes down to about 3 300 m, below it are only shaded patches, and by 3 000 m there is none at all —
        /// grey moraine and lava at Krugozor and Mir, alpine meadow and pine forest down at Azau.</summary>
        static float[,,] Splat(HeightField dem, byte[] rock, byte[] ash)
        {
            int n = Alpha;
            var a = new float[n, n, LayerCount];
            float step = Elbrus.Size / (n - 1);
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    float x = -Elbrus.Half + c * step, z = -Elbrus.Half + r * step;
                    float elev = dem.Sample(x, z);
                    var (_, _, slope) = dem.Fall(x, z, 12f);
                    float rk = rock[r * n + c] / 255f;
                    float sh = ash[r * n + c] / 255f;
                    float noise = Mathf.PerlinNoise(x * .0035f + 11, z * .0035f + 5);
                    float patch = Mathf.PerlinNoise(x * .014f + 21, z * .014f + 33);
                    // firn line in summer: patches from 3 250 m, continuous snow above 3 700 m, nothing below 3 050 m
                    float snowy = Mathf.Clamp01(Mathf.InverseLerp(3250f, 3700f, elev) + (patch - .55f) * .6f);
                    snowy *= Mathf.InverseLerp(3050f, 3260f, elev);
                    // the steep sunny ribs lose their snow first; shaded hollows keep it
                    snowy *= Mathf.Lerp(1f, .55f, Mathf.InverseLerp(26f, 42f, slope));
                    float bare = 1f - snowy;

                    // bare lava belongs to the volcano itself: down in the Baksan valley the same mask means moraine
                    // and grassy shelves, not black rock, so it only opens up above the tree line
                    float lava = rk * bare * Mathf.Lerp(.55f, 1f, Mathf.InverseLerp(10f, 30f, slope))
                               * Mathf.InverseLerp(2650f, 3150f, elev);
                    // meadow: the Azau bowl and the grassy shelves of the Baksan valley, gone above ~3 000 m
                    // the meadow follows the forest: green up to the tree line at about 2 700 m, thinning out to bare
                    // moraine by 3 150 m — the Azau bowl and the shelves above it are grass, not sand, and the bench at
                    // Mir (3 455 m) is three hundred metres clear of the last of it
                    float grass = (1f - Mathf.InverseLerp(2800f, 3150f, elev)) * (1f - Mathf.InverseLerp(34f, 48f, slope)) * (.8f + .5f * noise);
                    float turf = Mathf.Clamp01(grass) * bare * (1f - lava);
                    // everything else that is bare: moraine gravel, and the volcanic ash fields higher up
                    float scree = Mathf.Max(0f, bare - lava - turf);
                    scree = Mathf.Max(scree, sh * bare * (1f - lava - turf));

                    float sum = snowy + lava + turf + scree;
                    if (sum < 1e-4f) { scree = 1; sum = 1; }
                    a[r, c, 0] = snowy / sum; a[r, c, 1] = lava / sum; a[r, c, 2] = turf / sum; a[r, c, 3] = scree / sum;
                }
            return a;
        }

        /// <summary>Alpine meadow: real grass on the terrain, not just a green texture. A July meadow at 2 400 m is not a
        /// lawn — it is olive and khaki, run through with burnt straw, and broken by stones, scree fans and cattle paths —
        /// so there are three kinds of tuft and the density map has holes in it. Ankle-to-knee high (0.3–0.55 m), which is
        /// what an alpine sward actually stands at, and dense enough that it reads as a sward from eye height instead of
        /// as scattered haycocks. Thick in the Azau bowl and on the grassy shelves of the Baksan valley, gone by 3 150 m,
        /// three hundred metres below the bench at Mir.
        /// Returns the prototypes and the density maps so they can be written into the saved asset a second time.</summary>
        static (DetailPrototype[] protos, int[][,] density) Meadow(HeightField dem)
        {
            // the meadow grass itself: muted olive, a few dry blades in every tuft
            var grass = TextureFactory.GrassTuft("elb_grass", 77, new Color(.22f, .24f, .16f), new Color(.4f, .43f, .29f), new Color(.56f, .52f, .34f));
            // burnt-out straw: the south-facing patches that dry off first
            var straw = TextureFactory.GrassTuft("elb_grass_dry", 78, new Color(.34f, .31f, .2f), new Color(.58f, .55f, .38f), new Color(.7f, .64f, .44f));
            // low sedge with small pale heads — bellflowers and edelweiss scale, just enough to break the uniformity
            var flowers = TextureFactory.GrassTuft("elb_grass_flower", 79, new Color(.24f, .26f, .17f), new Color(.44f, .46f, .3f),
                new Color(.62f, .58f, .38f), new Color(.86f, .86f, .78f), .3f, 13, .8f);
            var protos = new[]
            {
                // healthy/dry multiply the card, so they stay pale and slightly warm: card × colour lands near .27/.29/.17,
                // an olive, not the .28/.39/.09 acid green the old pair produced
                Detail(grass, new Color(.68f, .68f, .58f), new Color(.74f, .7f, .52f), .55f, .46f, 14f),
                Detail(straw, new Color(.72f, .69f, .56f), new Color(.78f, .72f, .56f), .48f, .42f, 19f),
                Detail(flowers, new Color(.7f, .7f, .6f), new Color(.76f, .72f, .56f), .34f, .3f, 26f),
            };

            var density = new int[3][,];
            for (int k = 0; k < density.Length; k++) density[k] = new int[DetailRes, DetailRes];
            // peak tufts per 6 m cell (36 m²): 4.1 per m² where the meadow is at its best, and the noise below takes the
            // usual spot down to about a third of that. Kept well under the 16-bit index budget of a 48 m patch.
            const float PeakGrass = 84f, PeakStraw = 40f, PeakFlowers = 22f;
            float step = Elbrus.Size / DetailRes;
            for (int r = 0; r < DetailRes; r++)
                for (int c = 0; c < DetailRes; c++)
                {
                    float x = -Elbrus.Half + (c + .5f) * step, z = -Elbrus.Half + (r + .5f) * step;
                    float elev = dem.Sample(x, z);
                    if (elev > 3150f) continue;
                    var (_, _, slope) = dem.Fall(x, z, 10f);
                    float m = (1f - Mathf.InverseLerp(2800f, 3150f, elev)) * (1f - Mathf.InverseLerp(30f, 44f, slope));
                    if (m <= 0f) continue;
                    // two scales of emptiness: whole shoulders of the valley that carry no sward at all (~220 m across),
                    // and the bare ground, stones and paths inside a pasture (~22 m, the finest a 6 m density cell can
                    // carry without aliasing)
                    float pasture = Mathf.Clamp01(Mathf.PerlinNoise(x * .0045f + 5, z * .0045f + 13) * 1.75f - .28f);
                    float bald = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.33f, .52f, Mathf.PerlinNoise(x * .045f + 31, z * .045f + 7)));
                    m *= pasture * (.25f + .85f * bald);
                    if (m <= .03f) continue;
                    m = Mathf.Clamp01(m);
                    // straw takes over where the sward thins out, so the dry patches are dry and not just sparser green
                    float dryShare = Mathf.Clamp01(1.25f - m);
                    density[0][r, c] = Mathf.RoundToInt(m * PeakGrass * (1.1f - .5f * dryShare));
                    density[1][r, c] = Mathf.RoundToInt(m * PeakStraw * (.6f + .8f * dryShare));
                    density[2][r, c] = Mathf.RoundToInt(m * PeakFlowers);
                }
            return (protos, density);
        }

        /// <summary>Write the meadow into a TerrainData. Called twice: once on the data being built, once on what came back
        /// out of the asset, for the same reason the splat map is written twice.</summary>
        static void ApplyMeadow(TerrainData data, DetailPrototype[] protos, int[][,] density)
        {
            data.detailPrototypes = protos;
            data.SetDetailResolution(DetailRes, DetailPerPatch);
            for (int k = 0; k < density.Length; k++) data.SetDetailLayer(0, 0, k, density[k]);
            data.wavingGrassAmount = .3f; data.wavingGrassSpeed = .34f; data.wavingGrassStrength = .3f;
            // the built-in grass shader uses this as lerp(grey .5, tint, wave) × 2, so anything above .5 brightens the
            // moving blades. The old .72/.76/.55 was a +50 % green boost on top of an already green card — half the acid.
            data.wavingGrassTint = new Color(.52f, .53f, .45f);
        }

        static DetailPrototype Detail(Texture2D tex, Color healthy, Color dry, float height, float width, float noise) => new DetailPrototype
        {
            prototypeTexture = tex,
            renderMode = DetailRenderMode.GrassBillboard,
            usePrototypeMesh = false,
            healthyColor = healthy, dryColor = dry,
            minHeight = height * .45f, maxHeight = height,
            minWidth = width * .55f, maxWidth = width,
            // high spread = the healthy/dry blend and the size vary tuft by tuft instead of drifting over a whole hillside,
            // which is what turns an even carpet into patches
            noiseSpread = noise,
        };

        // ── forest and boulders ───────────────────────────────────────────────────────────────────────────
        /// <summary>Pine forest of the Baksan valley and the boulders of the moraines. The tree line on the southern slope
        /// runs at about 2 500–2 700 m: Azau sits right at its upper edge, with the forest falling away east towards Terskol.</summary>
        static void Scatter(TerrainData data, HeightField dem, byte[] rock)
        {
            var protos = new List<TreePrototype>();
            var trees = TreeFactory.BuildElbrusLibrary();
            foreach (var p in trees) protos.Add(new TreePrototype { prefab = p.Prefab, bendFactor = 0 });
            int rockBase = protos.Count;
            var rocks = RockFactory.BuildElbrusLibrary();
            foreach (var g in rocks) protos.Add(new TreePrototype { prefab = g, bendFactor = 0 });
            data.treePrototypes = protos.ToArray();

            var inst = new List<TreeInstance>(60000);
            var rnd = new System.Random(1969);
            float half = Elbrus.Half;
            var azau = Elbrus.Azau;
            const float Grid = 9f;
            for (float z = -half + Grid; z < half - Grid; z += Grid)
                for (float x = -half + Grid; x < half - Grid; x += Grid)
                {
                    float px = x + ((float)rnd.NextDouble() - .5f) * Grid, pz = z + ((float)rnd.NextDouble() - .5f) * Grid;
                    float elev = dem.Sample(px, pz);
                    if (elev > 2760f) continue;
                    var (_, _, slope) = dem.Fall(px, pz, 10f);
                    if (slope > 34f) continue;
                    // thinning out towards the tree line, and a clearing around the Azau terminals and their square
                    float density = (1f - Mathf.InverseLerp(2500f, 2760f, elev)) * Mathf.Lerp(1f, .4f, Mathf.InverseLerp(22f, 34f, slope));
                    density *= .35f + .75f * Mathf.PerlinNoise(px * .004f + 3, pz * .004f + 9);
                    if (Elbrus.Distance(px, pz, azau.X, azau.Z) < 130f) continue;
                    if (rnd.NextDouble() > density * .85f) continue;
                    int pi = rnd.Next(trees.Count);
                    float hs = .7f + (float)rnd.NextDouble() * .6f;
                    inst.Add(new TreeInstance
                    {
                        prototypeIndex = pi,
                        position = new Vector3((px + half) / Elbrus.Size, 0, (pz + half) / Elbrus.Size),
                        heightScale = hs, widthScale = hs * (.85f + .3f * (float)rnd.NextDouble()),
                        rotation = (float)rnd.NextDouble() * Mathf.PI * 2,
                        color = Color.Lerp(Color.white, new Color(.82f, .88f, .8f), (float)rnd.NextDouble()),
                        lightmapColor = Color.white,
                    });
                }
            int forest = inst.Count;

            // boulders: moraine blocks of the lower slope, thinning out above the firn line
            int n = Alpha;
            float step = Elbrus.Size / (n - 1);
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    float m = rock[r * n + c] / 255f;
                    float x = -half + c * step + ((float)rnd.NextDouble() - .5f) * step;
                    float z = -half + r * step + ((float)rnd.NextDouble() - .5f) * step;
                    float elev = dem.Sample(x, z);
                    if (elev > 3400f) continue;
                    float chance = (.12f + m * .5f) * (1f - Mathf.InverseLerp(3000f, 3400f, elev) * .6f);
                    if (rnd.NextDouble() > chance * .25f) continue;
                    float s = .4f + (float)rnd.NextDouble() * (.7f + m);
                    inst.Add(new TreeInstance
                    {
                        prototypeIndex = rockBase + rnd.Next(rocks.Count),
                        position = new Vector3((x + half) / Elbrus.Size, 0, (z + half) / Elbrus.Size),
                        heightScale = s * (.8f + .3f * (float)rnd.NextDouble()), widthScale = s,
                        rotation = (float)rnd.NextDouble() * Mathf.PI * 2, color = Color.white, lightmapColor = Color.white,
                    });
                }
            data.SetTreeInstances(inst.ToArray(), true);
            Debug.Log($"1079 Эльбрус: {forest} деревьев в долине, {inst.Count - forest} валунов");
        }

    }
}
