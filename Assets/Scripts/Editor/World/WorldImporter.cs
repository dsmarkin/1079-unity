using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Core;
using Debug = UnityEngine.Debug;

namespace Height1079.EditorTools.World
{
    /// <summary>Source data (Assets/Data/World) + art library → TerrainData asset with layers, splat maps, trees and rocks.
    /// Deterministic: running it twice gives the same world. Menu: 1079 → Rebuild world.</summary>
    public static class WorldImporter
    {
        public const string TerrainAsset = WorldPaths.Generated + "/Kholat.asset";
        public const string HeightResource = WorldPaths.Generated + "/height_2049.bytes";
        public const float BaseHeight = WorldData.HeightMin - 4f;
        public static float Range => WorldData.HeightMax - BaseHeight;
        const int Alpha = 1025;

        [MenuItem("1079/Rebuild world (terrain, trees, sites)")]
        public static void RebuildMenu() => Build(true);

        /// <summary>Bump when a factory changes so existing checkouts rebuild the generated world on next open/check.</summary>
        public const int PipelineVersion = 56;
        const string Stamp = WorldPaths.Generated + "/pipeline.version";

        public static bool IsBuilt => File.Exists(TerrainAsset) && File.Exists(HeightResource) && File.Exists(Stamp)
            && File.ReadAllText(Stamp).Trim() == PipelineVersion.ToString()
            && (!ElbrusImporter.SourcePresent || File.Exists(ElbrusImporter.TerrainAsset));

        public static void Build(bool force)
        {
            if (IsBuilt && !force) return;
            var clock = Stopwatch.StartNew();
            Directory.CreateDirectory(WorldPaths.Generated);
            Materials.ResetCache();
            AssetDatabase.StartAssetEditing();
            try
            {
                File.Copy($"{WorldPaths.Source}/height_2049.r16", HeightResource, true);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.ImportAsset(HeightResource);

            var dem = HeightField.FromR16(File.ReadAllBytes($"{WorldPaths.Source}/height_2049.r16"), WorldData.HeightMin, WorldData.HeightMax);
            var canopy = File.ReadAllBytes($"{WorldPaths.Source}/canopy_2049.r8");
            var rock = File.ReadAllBytes($"{WorldPaths.Source}/rock_1025.r8");
            var trees = Dem.LoadTrees(File.ReadAllBytes($"{WorldPaths.Source}/trees.f32"));

            EditorUtility.DisplayProgressBar("1079 world", "Tree and rock library", .1f);
            var protos = TreeFactory.BuildLibrary();
            var underProtos = TreeFactory.BuildUnderstory();
            var understory = File.Exists($"{WorldPaths.Source}/understory.f32") ? Dem.LoadUnderstory(File.ReadAllBytes($"{WorldPaths.Source}/understory.f32")) : new UnderRecord[0];
            var rocks = RockFactory.BuildLibrary();

            EditorUtility.DisplayProgressBar("1079 world", "Event sites", .3f);
            var t = WorldData.Tent;
            var towardTent = new Vector2(t.X - WorldData.Cedar.X, t.Z - WorldData.Cedar.Z);
            var hero = TreeFactory.HeroCedar(towardTent);
            TreeFactory.LoneBirch("DyatlovBirch", 1959);
            TreeFactory.TripleSpruce();
            var branches = AssetDatabase.LoadMainAssetAtPath($"{WorldPaths.PolyHaven}/Models/dry_branches_medium_01/dry_branches_medium_01_1k.gltf") as GameObject;
            if (branches == null) Debug.LogWarning("1079 world: Poly Haven dry branches model not imported (glTFast) — labaz is built without it.");
            SnowFxFactory.Build();
            AnimalTracksFactory.Build(dem);
            SkyFactory.Build();
            ItemsFactory.Build();
            // the ready-made low-poly models in the palette's colours (docs/ART.md «Как подключаются готовые модели»)
            ImportedFactory.Build();
            CargoFactory.Build();
            SkiFactory.Build();
            SiteFactory.BuildRucksack(WorldPaths.Generated + "/Prefabs/Items");
            CreatureFactory.Build();
            SiteFactory.Tent();
            SiteFactory.Labaz(branches);
            SiteFactory.Labaz(branches, morning: true);
            SiteFactory.Camp31Tent();
            SiteFactory.Camp31Fire();
            SiteFactory.CedarSite(hero, towardTent);
            SiteFactory.Den();
            SiteFactory.P4Stone(rocks[1]);
            SiteFactory.Marker("Marker_Event", new Color(.84f, .62f, .3f));
            SiteFactory.Marker("Marker_Version", new Color(.55f, .62f, .7f));
            SiteFactory.Marker("Marker_Landmark", new Color(.45f, .72f, .72f));
            SiteFactory.Marker("Marker_Derived", new Color(.9f, .9f, .88f));
            SiteFactory.RouteFlag("Flag_Footprints", new Color(.85f, .2f, .15f));
            SiteFactory.RouteFlag("Flag_Ascent", new Color(.95f, .6f, .1f));

            EditorUtility.DisplayProgressBar("1079 world", "Heightmap", .45f);
            var data = new TerrainData { heightmapResolution = HeightField.Resolution };
            data.size = new Vector3(WorldData.Size, Range, WorldData.Size);
            int N = HeightField.Resolution;
            var h = new float[N, N];
            for (int r = 0; r < N; r++)
                for (int c = 0; c < N; c++)
                {
                    float x = -HeightField.Half + c * HeightField.Step, z = -HeightField.Half + r * HeightField.Step;
                    h[r, c] = (WorldData.GroundHeight(dem, x, z) - BaseHeight) / Range;
                }
            data.SetHeights(0, 0, h);

            EditorUtility.DisplayProgressBar("1079 world", "Terrain layers and splat maps", .6f);
            data.terrainLayers = Layers();
            data.alphamapResolution = Alpha;
            data.SetAlphamaps(0, 0, Splat(dem, canopy, rock));
            data.baseMapResolution = 1024;

            EditorUtility.DisplayProgressBar("1079 world", "Scattering trees and rocks", .8f);
            Scatter(data, dem, trees, protos, rocks, rock, understory, underProtos);

            AssetDatabase.DeleteAsset(TerrainAsset);
            AssetDatabase.CreateAsset(data, TerrainAsset);
            TerrainMaterial();

            EditorUtility.DisplayProgressBar("1079 world", "Эльбрус: рельеф, канатки, приюты", .9f);
            ElbrusImporter.Build();
            AssetDatabase.SaveAssets();
            File.WriteAllText(Stamp, PipelineVersion.ToString());
            EditorUtility.ClearProgressBar();
            Debug.Log($"1079 world rebuilt in {clock.Elapsed.TotalSeconds:0.0} s: {data.treeInstanceCount} trees+rocks, {protos.Count} tree prototypes");
        }

        public static Material TerrainMaterial()
        {
            string path = WorldPaths.Generated + "/Materials/Terrain.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            m = new Material(Shader.Find("Nature/Terrain/Standard")) { name = "Terrain" };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static TerrainLayer Layer(string name, string id, float tile, float smoothness, Color tint)
        {
            string dir = WorldPaths.Generated + "/TerrainLayers";
            Directory.CreateDirectory(dir);
            string path = $"{dir}/{name}.terrainlayer";
            AssetDatabase.DeleteAsset(path);
            var l = new TerrainLayer
            {
                diffuseTexture = Materials.Tex(WorldPaths.PH(id, "diff")),
                normalMapTexture = Materials.Tex(WorldPaths.PH(id, "nor_gl")),
                tileSize = new Vector2(tile, tile), smoothness = smoothness, metallic = 0, normalScale = .8f,
                diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1),
            };
            AssetDatabase.CreateAsset(l, path);
            return l;
        }

        /// <summary>0 powder snow, 1 wind crust (наст) on exposed ground, 2 stones with lichen (курум), 3 needle litter under the canopy.</summary>
        static TerrainLayer[] Layers() => new[]
        {
            Layer("Snow", "snow_02", 5f, .25f, Color.white),
            Layer("WindCrust", "snow_03", 9f, .35f, new Color(.93f, .95f, 1f)),
            Layer("Kurum", "lichen_rock", 3.5f, .1f, Color.white),
            Layer("NeedleLitter", "winter_leaves", 4f, .05f, Color.white),
        };

        static float[,,] Splat(HeightField dem, byte[] canopy, byte[] rock)
        {
            int n = Alpha;
            // forest density: canopy ≥ 3 m, box-blurred over ±6 m
            var f = new float[n * n];
            for (int r = 0; r < n; r++) for (int c = 0; c < n; c++) f[r * n + c] = canopy[(2 * r) * HeightField.Resolution + 2 * c] >= 3 ? 1 : 0;
            f = Blur(f, n, 3); f = Blur(f, n, 3);
            var a = new float[n, n, 4];
            float step = WorldData.Size / (n - 1);
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    float x = -HeightField.Half + c * step, z = -HeightField.Half + r * step;
                    float elev = dem.Sample(x, z);
                    var (_, _, slope) = dem.Fall(x, z, 4f);
                    float forest = f[r * n + c];
                    float rk = Mathf.Max(rock[r * n + c] / 255f, Mathf.InverseLerp(30f, 42f, slope));
                    float noise = Mathf.PerlinNoise(x * .013f + 3, z * .013f + 7);
                    float crust = Mathf.Clamp01((1 - forest * 2.5f) * Mathf.InverseLerp(700, 820, elev) * (.45f + .5f * noise));
                    float litter = forest * .22f * (.6f + .8f * Mathf.PerlinNoise(x * .05f, z * .05f));
                    float rockW = rk * .95f;
                    float snow = Mathf.Max(0, 1 - crust - rockW - litter);
                    float sum = snow + crust + rockW + litter;
                    a[r, c, 0] = snow / sum; a[r, c, 1] = crust / sum; a[r, c, 2] = rockW / sum; a[r, c, 3] = litter / sum;
                }
            return a;
        }

        static float[] Blur(float[] src, int n, int radius)
        {
            var tmp = new float[src.Length]; var dst = new float[src.Length];
            float k = 1f / (2 * radius + 1);
            for (int r = 0; r < n; r++)
            {
                float acc = 0; int row = r * n;
                for (int c = -radius; c <= radius; c++) acc += src[row + Mathf.Clamp(c, 0, n - 1)];
                for (int c = 0; c < n; c++) { tmp[row + c] = acc * k; acc += src[row + Mathf.Min(n - 1, c + radius + 1)] - src[row + Mathf.Max(0, c - radius)]; }
            }
            for (int c = 0; c < n; c++)
            {
                float acc = 0;
                for (int r = -radius; r <= radius; r++) acc += tmp[Mathf.Clamp(r, 0, n - 1) * n + c];
                for (int r = 0; r < n; r++) { dst[r * n + c] = acc * k; acc += tmp[Mathf.Min(n - 1, r + radius + 1) * n + c] - tmp[Mathf.Max(0, r - radius) * n + c]; }
            }
            return dst;
        }

        /// <summary>Keep-out circles around hand-built sites (metres).</summary>
        static readonly (Func<(float x, float z)> at, float radius)[] KeepOut =
        {
            (() => (WorldData.Cedar.X, WorldData.Cedar.Z), 3.5f),
            (() => WorldData.Den, 3.5f),
            (() => (WorldData.P4.X, WorldData.P4.Z), 1.5f),
            (() => (WorldData.Labaz.X, WorldData.Labaz.Z), 1.8f),
            (() => WorldData.Camp, 4.5f),
            (() => WorldData.CampTentPad, 5.5f),
            (() => (WorldData.Tent.X, WorldData.Tent.Z), 8f),
            (() => (WorldData.Get("dyatlov").X, WorldData.Get("dyatlov").Z), 2f),
            (() => (WorldData.Get("triple").X, WorldData.Get("triple").Z), 2f),
        };

        static bool Blocked(float x, float z)
        {
            foreach (var (at, radius) in KeepOut) { var p = at(); if (WorldData.Distance(x, z, p.x, p.z) < radius) return true; }
            if (WorldData.CreekDistance(x, z) < 2.2f) return true;
            return !WorldData.Inside(x, z, 3);
        }

        static void Scatter(TerrainData data, HeightField dem, TreeRecord[] trees, List<TreeFactory.Prototype> protos, List<GameObject> rocks, byte[] rockMask,
            UnderRecord[] understory, List<TreeFactory.UnderPrototype> underProtos)
        {
            var list = new List<TreePrototype>();
            var byKey = new Dictionary<(TreeSpecies, TreeForm), List<int>>();
            var protoHeight = new List<float>();
            foreach (var p in protos)
            {
                var key = (p.Species, p.Form);
                if (!byKey.TryGetValue(key, out var l)) byKey[key] = l = new List<int>();
                l.Add(list.Count);
                list.Add(new TreePrototype { prefab = p.Prefab, bendFactor = 0 });
                protoHeight.Add(p.Height);
            }
            var byKind = new Dictionary<UnderKind, List<int>>();
            foreach (var p in underProtos)
            {
                if (!byKind.TryGetValue(p.Kind, out var l)) byKind[p.Kind] = l = new List<int>();
                l.Add(list.Count);
                list.Add(new TreePrototype { prefab = p.Prefab, bendFactor = 0 });
                protoHeight.Add(p.Size);
            }
            int rockBase = list.Count;
            foreach (var r in rocks) list.Add(new TreePrototype { prefab = r, bendFactor = 0 });
            data.treePrototypes = list.ToArray();

            var inst = new List<TreeInstance>(trees.Length + understory.Length + 20000);
            var rnd = new System.Random(1959);
            // tree-line forms are wind-flagged: their branches point downwind (+Z of the model → south-east, like the storm wind)
            const float LeeYaw = 135f * Mathf.Deg2Rad;
            foreach (var tr in trees)
            {
                if (Blocked(tr.X, tr.Z)) continue;
                if (!byKey.TryGetValue((tr.Species, tr.Form), out var variants)) variants = byKey[(tr.Species, TreeForm.Normal)];
                int hash = (int)(Mathf.Abs(tr.X) * 7919f + Mathf.Abs(tr.Z) * 104729f);
                int pi = variants[hash % variants.Count];
                float hs = Mathf.Clamp(tr.Height / protoHeight[pi], .22f, 1.75f);
                float ws = hs * (.85f + .3f * (float)rnd.NextDouble());
                if (tr.Species == TreeSpecies.Birch && tr.Height < 7) ws *= .8f;
                float yaw = tr.Form == TreeForm.TreeLine ? LeeYaw + ((float)rnd.NextDouble() - .5f) * .7f : (float)rnd.NextDouble() * Mathf.PI * 2;
                inst.Add(new TreeInstance
                {
                    prototypeIndex = pi,
                    position = new Vector3((tr.X + HeightField.Half) / WorldData.Size, 0, (tr.Z + HeightField.Half) / WorldData.Size),
                    heightScale = hs, widthScale = ws,
                    rotation = yaw,
                    color = Color.Lerp(Color.white, new Color(.8f, .88f, .82f), (float)rnd.NextDouble()),
                    lightmapColor = Color.white,
                });
            }
            int canopyTrees = inst.Count;
            foreach (var u in understory)
            {
                if (Blocked(u.X, u.Z)) continue;
                // the open slope under the tent stays open (search photos, 1959)
                if (WorldData.Distance(u.X, u.Z, WorldData.Tent.X, WorldData.Tent.Z) < 300f) continue;
                if (!byKind.TryGetValue(u.Kind, out var variants)) continue;
                int hash = (int)(Mathf.Abs(u.X) * 7919f + Mathf.Abs(u.Z) * 104729f);
                int pi = variants[hash % variants.Count];
                float s = Mathf.Clamp(u.Size / protoHeight[pi], .2f, 2.2f);
                inst.Add(new TreeInstance
                {
                    prototypeIndex = pi,
                    position = new Vector3((u.X + HeightField.Half) / WorldData.Size, 0, (u.Z + HeightField.Half) / WorldData.Size),
                    heightScale = s, widthScale = s * (.85f + .3f * (float)rnd.NextDouble()),
                    rotation = u.Yaw * Mathf.Deg2Rad,
                    color = Color.Lerp(Color.white, new Color(.85f, .9f, .85f), (float)rnd.NextDouble()),
                    lightmapColor = Color.white,
                });
            }
            Debug.Log($"1079 world: {canopyTrees} canopy trees, {inst.Count - canopyTrees} understory");
            int trees2 = inst.Count;
            // boulders on stone ridges and steep open ground
            const int n = Alpha;
            float step = WorldData.Size / (n - 1);
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    float m = rockMask[r * n + c] / 255f;
                    if (m < .3f || rnd.NextDouble() > (m - .3f) * .3f) continue;
                    float x = -HeightField.Half + c * step + ((float)rnd.NextDouble() - .5f) * step, z = -HeightField.Half + r * step + ((float)rnd.NextDouble() - .5f) * step;
                    if (Blocked(x, z)) continue;
                    float s = .35f + (float)rnd.NextDouble() * (.5f + m);
                    inst.Add(new TreeInstance
                    {
                        prototypeIndex = rockBase + rnd.Next(rocks.Count),
                        position = new Vector3((x + HeightField.Half) / WorldData.Size, 0, (z + HeightField.Half) / WorldData.Size),
                        heightScale = s * (.8f + .3f * (float)rnd.NextDouble()), widthScale = s,
                        rotation = (float)rnd.NextDouble() * Mathf.PI * 2, color = Color.white, lightmapColor = Color.white,
                    });
                }
            data.SetTreeInstances(inst.ToArray(), true);
            Debug.Log($"1079 world: {inst.Count - trees2} boulders");
        }
    }

    /// <summary>Import rules for third-party textures (studio convention: the file suffix decides the import type).</summary>
    public sealed class ThirdPartyTexturePostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(WorldPaths.PolyHaven)) return;
            var imp = (TextureImporter)assetImporter;
            if (assetPath.Contains("_nor_gl")) imp.textureType = TextureImporterType.NormalMap;
            else if (assetPath.Contains("_rough") || assetPath.Contains("_arm")) imp.sRGBTexture = false;
            imp.maxTextureSize = 1024;
            imp.anisoLevel = 4;
        }
    }

    /// <summary>Import rules for the third-party model packs (Quaternius, Kenney — ImportedFactory bakes them into
    /// palette prefabs): the file's own scale (metres come out of the fit in the factory, not out of the file), no
    /// rig, no cameras or lights, readable meshes for the bake. Written here rather than in .meta files, which this
    /// repository does not keep.</summary>
    public sealed class ThirdPartyModelPostprocessor : AssetPostprocessor
    {
        public static bool Wants(string path) => path.StartsWith(ImportedFactory.Quaternius) || path.StartsWith("Assets/Art/ThirdParty/Kenney");

        /// <summary>Skinned characters (Quaternius Modular Men) keep their bones: the puppet turns them (PuppetSkeleton).</summary>
        public const string Characters = "Assets/Art/ThirdParty/Quaternius/ModularMen/";

        void OnPreprocessModel()
        {
            if (!Wants(assetPath)) return;
            if (assetPath.StartsWith(Characters)) ApplyRig((ModelImporter)assetImporter);
            else Apply((ModelImporter)assetImporter);
        }

        /// <summary>A character with a skeleton: Generic rig with its own avatar, bones left as transforms, no clips.
        /// Unity imports the meshes rigid in the rest pose; FigureFactory strips the Animator and the puppet poses the bones.</summary>
        public static void ApplyRig(ModelImporter imp)
        {
            imp.useFileScale = true;
            imp.globalScale = 1f;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.optimizeGameObjects = false;
            imp.importAnimation = false;
            imp.importBlendShapes = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.isReadable = false;
        }

        public static void Apply(ModelImporter imp)
        {
            imp.useFileScale = true;
            imp.globalScale = 1f;
            imp.importAnimation = false;
            imp.animationType = ModelImporterAnimationType.None;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importBlendShapes = false;
            imp.importVisibility = false;
            imp.isReadable = true;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.generateSecondaryUV = false;
            imp.importNormals = ModelImporterNormals.Import;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }
    }
}
