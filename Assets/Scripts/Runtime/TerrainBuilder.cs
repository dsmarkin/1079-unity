using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Loads the world built by the editor pipeline (Resources/World): the TerrainData asset with layers, trees and rocks,
    /// and the raw height grid the rules use. World x/z of Core map straight onto Unity x/z (x east, z north); y is metres above sea level.</summary>
    public static class TerrainBuilder
    {
        /// <summary>Must match WorldImporter.BaseHeight (editor).</summary>
        public const float BaseHeight = WorldData.HeightMin - 4f;

        public static HeightField LoadDem()
        {
            var asset = Resources.Load<TextAsset>("World/height_2049");
            if (asset == null) throw new System.IO.FileNotFoundException("Resources/World/height_2049.bytes — open the project in the editor once (menu 1079 → Rebuild world)");
            return HeightField.FromR16(asset.bytes, WorldData.HeightMin, WorldData.HeightMax);
        }

        public static Terrain Build()
        {
            var data = Resources.Load<TerrainData>("World/Kholat");
            if (data == null) { Debug.LogError("Resources/World/Kholat.asset missing — menu 1079 → Rebuild world"); return null; }
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Kholat Syakhl";
            go.transform.position = new Vector3(-HeightField.Half, BaseHeight, -HeightField.Half);
            var terrain = go.GetComponent<Terrain>();
            var mat = Resources.Load<Material>("World/Materials/Terrain");
            if (mat != null) terrain.materialTemplate = mat;
            terrain.drawInstanced = false;
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 350f;
            terrain.treeDistance = 1400f;
            terrain.treeMaximumFullLODCount = 400;
            terrain.treeLODBiasMultiplier = 1f;
            terrain.drawTreesAndFoliage = true;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            return terrain;
        }

        /// <summary>Ground height at world x/z using the same math the server uses, so client and authority agree.</summary>
        public static float Height(HeightField dem, float x, float z) => WorldData.GroundHeight(dem, x, z);
    }
}
