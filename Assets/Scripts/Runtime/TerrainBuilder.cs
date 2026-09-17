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
            terrain.drawInstanced = false;
            terrain.heightmapPixelError = World.IsElbrus ? 6f : 3f;
            terrain.basemapDistance = World.IsElbrus ? 2200f : 350f;
            terrain.treeDistance = World.IsElbrus ? 900f : 1400f;
            terrain.treeMaximumFullLODCount = 400;
            terrain.treeLODBiasMultiplier = 1f;
            terrain.drawTreesAndFoliage = true;
            // grass on the Azau meadows: a short draw distance, it is only there where the ground is green anyway
            terrain.detailObjectDistance = World.IsElbrus ? 95f : 60f;
            terrain.detailObjectDensity = 1f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            if (World.IsElbrus) ReportGround(data);
            return terrain;
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
