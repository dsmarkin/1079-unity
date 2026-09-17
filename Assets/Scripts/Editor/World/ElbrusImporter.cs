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

            AssetDatabase.DeleteAsset(TerrainAsset);
            AssetDatabase.CreateAsset(data, TerrainAsset);
            EditorUtility.ClearProgressBar();
            Debug.Log($"1079 Эльбрус собран за {clock.Elapsed.TotalSeconds:0.0} с: {Elbrus.Size:0} м, {Elbrus.Ropeways.Length} канатных дорог");
        }

        /// <summary>0 firn (сухой фирн), 1 wind crust and bare glacier ice, 2 lava rock, 3 ash and moraine.</summary>
        static TerrainLayer[] Layers() => new[]
        {
            // small tiles and a strong normal, or the slope reads as white paper at arm's length
            Layer("ElbFirn", "snow_02", 3.5f, .25f, new Color(.94f, .95f, .97f), 1.4f),
            Layer("ElbIce", "snow_03", 5.5f, .5f, new Color(.8f, .88f, .98f), 1.2f),
            Layer("ElbLava", "rock_face_03", 4f, .1f, new Color(.5f, .48f, .48f), 1.1f),
            Layer("ElbAsh", "burned_ground_01", 5f, .06f, new Color(.62f, .58f, .54f), 1f),
        };

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

        /// <summary>Where the southern slope shows what: dry firn on the upper glaciers, blue ice where the wind scours it
        /// and on the steep shelf, black lava on the ribs (Pastukhov rocks, the crags below Mir) and ash below the firn line.</summary>
        static float[,,] Splat(HeightField dem, byte[] rock, byte[] ash)
        {
            int n = Alpha;
            var a = new float[n, n, 4];
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
                    // sastrugi: wind-carved bands of hard crust across the firn, tens of metres wide
                    float sastrugi = Mathf.PerlinNoise(x * .045f + 3.1f, z * .012f + 7.7f);
                    // firn line: nothing but snow above ~3 900 m, patchy between 3 300 and 3 900
                    float snowy = Mathf.Clamp01(Mathf.InverseLerp(3200f, 3900f, elev) + (noise - .5f) * .35f);
                    float rockW = rk * Mathf.Lerp(1f, .35f, snowy);
                    float ashW = sh * Mathf.Lerp(1f, .15f, snowy) * (1f - rockW);
                    // wind crust and bare ice: swept ridges and everything steep above the shelf
                    float ice = Mathf.Clamp01(Mathf.InverseLerp(14f, 30f, slope) * snowy * (.4f + .8f * noise));
                    ice = Mathf.Max(ice, snowy * Mathf.InverseLerp(.56f, .78f, sastrugi) * .75f);
                    ice = Mathf.Max(ice, Mathf.InverseLerp(4850f, 5250f, elev) * Mathf.InverseLerp(12f, 24f, slope));
                    ice *= 1f - rockW;
                    float firn = Mathf.Max(0f, 1f - rockW - ashW - ice);
                    float sum = firn + ice + rockW + ashW;
                    if (sum < 1e-4f) { firn = 1; sum = 1; }
                    a[r, c, 0] = firn / sum; a[r, c, 1] = ice / sum; a[r, c, 2] = rockW / sum; a[r, c, 3] = ashW / sum;
                }
            return a;
        }
    }
}
