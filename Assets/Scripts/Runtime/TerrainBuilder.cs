using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Loads the world built by the editor pipeline (Resources/World) for the place the session is in
    /// (<see cref="World.Current"/>): the TerrainData asset with layers, trees and rocks, and the raw height grid the
    /// rules use. World x/z of Core map straight onto Unity x/z (x east, z north); y is metres above sea level.</summary>
    public static class TerrainBuilder
    {
        /// <summary>Must match WorldImporter.BaseHeight / ElbrusImporter.BaseHeight (editor).</summary>
        public static float BaseHeight => World.IsElbrus ? Elbrus.HeightMin - 6f : WorldData.HeightMin - 4f;

        public static HeightField LoadDem()
        {
            var asset = Resources.Load<TextAsset>("World/" + World.HeightAsset);
            if (asset == null) throw new System.IO.FileNotFoundException($"Resources/World/{World.HeightAsset}.bytes — open the project in the editor once (menu 1079 → Rebuild world)");
            return HeightField.FromR16(asset.bytes, World.HeightMin, World.HeightMax, World.Resolution, World.GridStep);
        }

        public static Terrain Build()
        {
            var data = Resources.Load<TerrainData>("World/" + World.TerrainAsset);
            if (data == null) { Debug.LogError($"Resources/World/{World.TerrainAsset}.asset missing — menu 1079 → Rebuild world"); return null; }
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = World.IsElbrus ? "Elbrus" : "Kholat Syakhl";
            go.transform.position = new Vector3(-World.Half, BaseHeight, -World.Half);
            var terrain = go.GetComponent<Terrain>();
            var mat = Resources.Load<Material>("World/Materials/Terrain");
            if (mat != null) terrain.materialTemplate = mat;
            // one instanced mesh for all 4 096 patches of the height map instead of 4 096 draws, and the height read
            // in the vertex shader
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = World.IsElbrus ? 6f : 3f;
            // the full four-layer splat, and how far it reaches. It used to run to 2.2 km because the base map behind
            // it is one texel to twelve metres on a 12.3 km terrain and the distance turned to mush; the base map is
            // 2048 now (ElbrusImporter.BaseMapRes), so the expensive shader can stop where it is actually looked at.
            terrain.basemapDistance = World.IsElbrus ? 650f : 350f;
            terrain.treeMaximumFullLODCount = 400;
            terrain.treeLODBiasMultiplier = 1f;
            terrain.drawTreesAndFoliage = true;
            // a terrain that casts its shadow two-sided is rasterised twice into every cascade, and it is the largest
            // mesh in the frame. An honest height field has no thin walls to leak light through.
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            ApplyQuality(terrain);
            // one subscription, whichever terrain is current: -= on a handler that is not subscribed does nothing,
            // and a place switch leaves the old terrain destroyed behind us
            Quality.Changed -= OnQualityChanged;
            Quality.Changed += OnQualityChanged;
            live = terrain;
            if (World.IsElbrus) ReportGround(data);
            return terrain;
        }

        /// <summary>The terrain this session is drawing, so a change of settings reaches it without a restart.</summary>
        static Terrain live;

        static void OnQualityChanged()
        {
            if (live == null) { Quality.Changed -= OnQualityChanged; live = null; return; }
            ApplyQuality(live);
        }

        /// <summary>The three distances the player pays for, straight from <see cref="Quality"/>. Grass is the one
        /// that decides the frame rate on the Azau meadow: it is the only place in the game that has any.</summary>
        static void ApplyQuality(Terrain terrain)
        {
            terrain.treeDistance = Quality.TreeDistance(World.IsElbrus);
            terrain.detailObjectDistance = Quality.DetailDistance;
            terrain.detailObjectDensity = Quality.DetailDensity;
        }

        /// <summary>What the terrain thinks is under the visitor's feet at Azau. The southern slope has no snow at 2 350 m,
        /// so if the first weight comes back as 1 the splat map did not survive the import and the ground renders as snow.</summary>
        static void ReportGround(TerrainData data)
        {
            var (x, z) = Elbrus.Start;
            int n = data.alphamapResolution;
            int c = Mathf.Clamp(Mathf.RoundToInt((x + Elbrus.Half) / Elbrus.Size * (n - 1)), 0, n - 1);
            int r = Mathf.Clamp(Mathf.RoundToInt((z + Elbrus.Half) / Elbrus.Size * (n - 1)), 0, n - 1);
            var a = data.GetAlphamaps(c, r, 1, 1);
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < data.alphamapLayers; k++)
            {
                var layer = k < data.terrainLayers.Length ? data.terrainLayers[k] : null;
                string tex = layer != null && layer.diffuseTexture != null ? layer.diffuseTexture.name : "нет текстуры";
                sb.Append($" {(layer != null ? layer.name : "null")}({tex})={a[0, 0, k]:0.00}");
            }
            Debug.Log($"1079 Эльбрус: грунт у Азау, слоёв {data.alphamapLayers}, alphamap {n}:{sb}");
        }

        /// <summary>Ground height at world x/z using the same math the server uses, so client and authority agree.</summary>
        public static float Height(HeightField dem, float x, float z) => World.Ground(dem, x, z);
    }
}
