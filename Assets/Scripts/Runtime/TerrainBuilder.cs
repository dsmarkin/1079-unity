using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Builds the 1:1 slope from the cached DEM at runtime. World x/z of Core map straight onto Unity x/z; y is metres above sea level.</summary>
    public static class TerrainBuilder
    {
        public const int Resolution = 257; // Unity heightmaps are 2^n + 1

        public static float[] LoadDem()
        {
            var asset = Resources.Load<TextAsset>("terrain");
            if (asset == null) throw new System.IO.FileNotFoundException("Resources/terrain.bytes (copy of public/data/terrain.png)");
            return Dem.LoadHeights(asset.bytes);
        }

        public static Terrain Build(float[] dem, Material snow)
        {
            float size = (float)WorldData.Size, half = size / 2f;
            float min = float.MaxValue, max = float.MinValue;
            var grid = new float[Resolution, Resolution];
            for (int r = 0; r < Resolution; r++)
                for (int c = 0; c < Resolution; c++)
                {
                    float x = -half + c / (float)(Resolution - 1) * size, z = -half + r / (float)(Resolution - 1) * size;
                    float h = WorldData.GroundHeight(dem, x, z);
                    grid[r, c] = h; if (h < min) min = h; if (h > max) max = h;
                }
            float range = Mathf.Max(1f, max - min);
            for (int r = 0; r < Resolution; r++) for (int c = 0; c < Resolution; c++) grid[r, c] = (grid[r, c] - min) / range;

            var data = new TerrainData { heightmapResolution = Resolution, size = new Vector3(size, range, size) };
            data.SetHeights(0, 0, grid);
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Slope";
            go.transform.position = new Vector3(-half, min, -half);
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = snow;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 4f;
            return terrain;
        }

        /// <summary>Ground height at world x/z using the same math the server uses, so client and authority agree.</summary>
        public static float Height(float[] dem, float x, float z) => WorldData.GroundHeight(dem, x, z);
    }
}
