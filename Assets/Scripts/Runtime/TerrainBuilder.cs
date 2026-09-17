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
            terrain.detailObjectDensity = .8f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            return terrain;
        }

        /// <summary>Ground height at world x/z using the same math the server uses, so client and authority agree.</summary>
        public static float Height(HeightField dem, float x, float z) => World.Ground(dem, x, z);
    }
}
