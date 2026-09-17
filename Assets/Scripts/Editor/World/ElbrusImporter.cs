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
        const int LayerCount = 6;

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
            data.terrainLayers = Layers();
            data.alphamapResolution = Alpha;
            data.SetAlphamaps(0, 0, Splat(dem, rock, ash));
            data.baseMapResolution = 1024;

            EditorUtility.DisplayProgressBar("1079 Эльбрус", "Сосновый лес Баксана и валуны", .85f);
            Scatter(data, dem, rock);
            Meadow(data, dem);

            EditorUtility.DisplayProgressBar("1079 Эльбрус", "Карта района", .92f);
            ElbrusMapFactory.Build(dem);

            AssetDatabase.DeleteAsset(TerrainAsset);
            AssetDatabase.CreateAsset(data, TerrainAsset);
            EditorUtility.ClearProgressBar();
            Debug.Log($"1079 Эльбрус собран за {clock.Elapsed.TotalSeconds:0.0} с: {Elbrus.Size:0} м, {Elbrus.Ropeways.Length} канатных дорог");
        }

        /// <summary>0 firn, 1 wind crust and bare ice, 2 lava rock, 3 volcanic ash, 4 alpine turf, 5 moraine scree.
        /// The lower slope is what a visitor sees in July: below the Garabashi tongue there is no snow at all, only
        /// grey moraine, lava ribs and green meadow down towards Azau.</summary>
        static TerrainLayer[] Layers() => new[]
        {
            // small tiles and a strong normal, or the slope reads as white paper at arm's length
            Layer("ElbFirn", "snow_02", 3.5f, .25f, new Color(.94f, .95f, .97f), 1.4f),
            Layer("ElbIce", "snow_03", 5.5f, .5f, new Color(.8f, .88f, .98f), 1.2f),
            Layer("ElbLava", "rock_face_03", 4f, .1f, new Color(.5f, .48f, .48f), 1.1f),
            Layer("ElbAsh", "burned_ground_01", 5f, .06f, new Color(.62f, .58f, .54f), 1f),
            Made("ElbTurf", 4f, .06f, TextureFactory.Ground("elb_turf", new Color(.24f, .3f, .15f), new Color(.46f, .5f, .27f), 6f, 91, .18f)),
            Made("ElbScree", 5f, .05f, TextureFactory.Ground("elb_scree", new Color(.31f, .3f, .29f), new Color(.58f, .56f, .54f), 8f, 47, .3f)),
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

        static TerrainLayer Layer(string name, string id, float tile, float smoothness, Color tint, float normal = .8f)
        {
            string dir = WorldPaths.Generated + "/TerrainLayers";
            Directory.CreateDirectory(dir);
            string path = $"{dir}/{name}.terrainlayer";
            AssetDatabase.DeleteAsset(path);
            var l = new TerrainLayer
            {
                diffuseTexture = Materials.Tex(WorldPaths.PH(id, "diff")),
                normalMapTexture = Materials.Tex(WorldPaths.PH(id, "nor_gl")),
                tileSize = new Vector2(tile, tile), smoothness = smoothness, metallic = 0, normalScale = normal,
                diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1),
            };
            AssetDatabase.CreateAsset(l, path);
            return l;
        }

        /// <summary>Where the southern slope shows what, on a July morning. Snow begins where the glaciers do: the tongue of
        /// Gara-Bashi comes down to about 3 300 m, below it are only shaded patches, and by 3 000 m there is none at all —
        /// grey moraine and lava at Krugozor and Mir, alpine meadow and pine forest down at Azau. Above the firn line the
        /// wind lays bare ice on the ridges and the sastrugi bands, and the lava ribs (Pastukhov rocks) stay black.</summary>
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
                    // sastrugi: wind-carved bands of hard crust across the firn, tens of metres wide
                    float sastrugi = Mathf.PerlinNoise(x * .045f + 3.1f, z * .012f + 7.7f);
                    // firn line in summer: patches from 3 250 m, continuous snow above 3 700 m, nothing below 3 050 m
                    float snowy = Mathf.Clamp01(Mathf.InverseLerp(3250f, 3700f, elev) + (patch - .55f) * .6f);
                    snowy *= Mathf.InverseLerp(3050f, 3260f, elev);
                    // north-facing hollows hold their snow longer, south-facing ribs lose it first
                    snowy *= Mathf.Lerp(1f, .55f, Mathf.InverseLerp(26f, 42f, slope));
                    float bare = 1f - snowy;

                    float lava = rk * bare * Mathf.Lerp(.55f, 1f, Mathf.InverseLerp(10f, 30f, slope));
                    float ashW = sh * bare * Mathf.InverseLerp(3300f, 3750f, elev) * (1f - lava);
                    // meadow: the Azau bowl and the grassy shelves of the Baksan valley, gone above ~3 000 m
                    float grass = (1f - Mathf.InverseLerp(2600f, 3020f, elev)) * (1f - Mathf.InverseLerp(26f, 40f, slope)) * (.55f + .75f * noise);
                    float turf = Mathf.Clamp01(grass) * bare * (1f - lava) * (1f - ashW);
                    float scree = Mathf.Max(0f, bare - lava - ashW - turf);

                    // wind crust and bare ice: swept ridges and everything steep above the shelf
                    float ice = Mathf.Clamp01(Mathf.InverseLerp(14f, 30f, slope) * snowy * (.4f + .8f * noise));
                    ice = Mathf.Max(ice, snowy * Mathf.InverseLerp(.56f, .78f, sastrugi) * .75f);
                    ice = Mathf.Max(ice, Mathf.InverseLerp(4850f, 5250f, elev) * Mathf.InverseLerp(12f, 24f, slope));
                    ice = Mathf.Min(ice, snowy);
                    float firn = Mathf.Max(0f, snowy - ice);

                    float sum = firn + ice + lava + ashW + turf + scree;
                    if (sum < 1e-4f) { scree = 1; sum = 1; }
                    a[r, c, 0] = firn / sum; a[r, c, 1] = ice / sum; a[r, c, 2] = lava / sum;
                    a[r, c, 3] = ashW / sum; a[r, c, 4] = turf / sum; a[r, c, 5] = scree / sum;
                }
            return a;
        }

        /// <summary>Alpine meadow: real grass on the terrain, not just a green texture. Thick in the Azau bowl and on the
        /// grassy shelves of the Baksan valley, thinning out as the meadow gives way to moraine at about 3 000 m.</summary>
        static void Meadow(TerrainData data, HeightField dem)
        {
            var grass = TextureFactory.GrassTuft("elb_grass", 77, new Color(.17f, .27f, .1f), new Color(.45f, .55f, .22f), new Color(.62f, .56f, .3f));
            var flowers = TextureFactory.GrassTuft("elb_grass_dry", 78, new Color(.3f, .32f, .14f), new Color(.66f, .63f, .33f), new Color(.75f, .7f, .42f));
            data.detailPrototypes = new[]
            {
                Detail(grass, new Color(.62f, .7f, .42f), new Color(.72f, .68f, .44f), 1.1f, .75f),
                Detail(flowers, new Color(.74f, .72f, .48f), new Color(.8f, .74f, .5f), .8f, .6f),
            };
            const int Res = 1024;
            data.SetDetailResolution(Res, 32);
            var thick = new int[Res, Res];
            var thin = new int[Res, Res];
            float step = Elbrus.Size / Res;
            for (int r = 0; r < Res; r++)
                for (int c = 0; c < Res; c++)
                {
                    float x = -Elbrus.Half + (c + .5f) * step, z = -Elbrus.Half + (r + .5f) * step;
                    float elev = dem.Sample(x, z);
                    if (elev > 3050f) continue;
                    var (_, _, slope) = dem.Fall(x, z, 10f);
                    float m = (1f - Mathf.InverseLerp(2600f, 3020f, elev)) * (1f - Mathf.InverseLerp(28f, 42f, slope));
                    m *= .4f + .9f * Mathf.PerlinNoise(x * .006f + 5, z * .006f + 13);
                    if (m <= .02f) continue;
                    thick[r, c] = Mathf.RoundToInt(Mathf.Clamp01(m) * 7f);
                    thin[r, c] = Mathf.RoundToInt(Mathf.Clamp01(m) * 3f);
                }
            data.SetDetailLayer(0, 0, 0, thick);
            data.SetDetailLayer(0, 0, 1, thin);
            data.wavingGrassAmount = .32f; data.wavingGrassSpeed = .38f; data.wavingGrassStrength = .42f;
            data.wavingGrassTint = new Color(.72f, .76f, .55f);
        }

        static DetailPrototype Detail(Texture2D tex, Color healthy, Color dry, float height, float width) => new DetailPrototype
        {
            prototypeTexture = tex,
            renderMode = DetailRenderMode.GrassBillboard,
            usePrototypeMesh = false,
            healthyColor = healthy, dryColor = dry,
            minHeight = height * .55f, maxHeight = height,
            minWidth = width * .6f, maxWidth = width,
            noiseSpread = 6f,
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
            var rocks = RockFactory.BuildLibrary();
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
                    if (elev > 2700f) continue;
                    var (_, _, slope) = dem.Fall(px, pz, 10f);
                    if (slope > 34f) continue;
                    // thinning out towards the tree line, and a clearing around the Azau terminals and their square
                    float density = (1f - Mathf.InverseLerp(2440f, 2700f, elev)) * Mathf.Lerp(1f, .35f, Mathf.InverseLerp(20f, 34f, slope));
                    density *= .35f + .75f * Mathf.PerlinNoise(px * .004f + 3, pz * .004f + 9);
                    if (Elbrus.Distance(px, pz, azau.X, azau.Z) < 130f) continue;
                    if (rnd.NextDouble() > density * .55f) continue;
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
