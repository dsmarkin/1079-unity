using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Loads the world built by the editor pipeline for the place the session is in (<see cref="World.Current"/>):
    /// the TerrainData asset with layers, trees and rocks, and the raw height grid the rules use. Both are the heaviest
    /// files in the project and only the game needs them, so they live outside Resources and are reached through
    /// <see cref="WorldAssets"/> (see <see cref="WorldKit"/>).
    /// World x/z of Core map straight onto Unity x/z (x east, z north); y is metres above sea level.</summary>
    public static class TerrainBuilder
    {
        /// <summary>Where the bottom of the terrain sits: the lowest node of this map's height raster, less the margin
        /// its own importer left under it (<see cref="ILocationView.GroundMargin"/>). Must match what the editor wrote.</summary>
        public static float BaseHeight => World.HeightMin - LocationViews.GroundMargin;

        public static HeightField LoadDem()
        {
            var asset = WorldAssets.Load<TextAsset>(World.HeightAsset);
            if (asset == null) throw new System.IO.FileNotFoundException($"{WorldKit.GeneratedDir}/{World.HeightAsset}.bytes — open the project in the editor once (menu 1079 → Rebuild world)");
            return HeightField.FromR16(asset.bytes, World.HeightMin, World.HeightMax, World.Resolution, World.GridStep);
        }

        public static Terrain Build()
        {
            var data = WorldAssets.Load<TerrainData>(World.TerrainAsset);
            if (data == null) { Debug.LogError($"{WorldKit.GeneratedDir}/{World.TerrainAsset}.asset missing — menu 1079 → Rebuild world"); return null; }
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = World.TerrainAsset;
            go.transform.position = new Vector3(-World.Half, BaseHeight, -World.Half);
            var terrain = go.GetComponent<Terrain>();
            var mat = Resources.Load<Material>("World/Materials/Terrain");
            if (mat != null) terrain.materialTemplate = mat;
            // Instancing would draw all 4 096 patches of the height map as one mesh instead of 4 096, and it works in
            // the editor — but the built player has no instanced variant of Nature/Terrain/Standard (nothing in
            // Resources references it, and Unity strips what nothing references: CLAUDE.md §4). The terrain then
            // renders with no textures at all — a white plain with trees standing on it. Turning this back on means
            // first getting that variant into the build and checking it IN THE BUILD (docs/BACKLOG.md PF.4).
            terrain.drawInstanced = false;
            // a map twelve kilometres across is not drawn like a valley four across: it is looked at from further
            // away, so it may be coarser, and its base map is one texel to six metres instead of one to two
            bool wide = World.Size > 8000f;
            terrain.heightmapPixelError = wide ? 6f : 3f;
            // the full four-layer splat, and how far it reaches. On the wide map it used to run to 2.2 km, and the
            // base map behind it turned to mush; the base map is 2048 now (the map's own importer), so the expensive
            // shader can stop where it is actually looked at.
            terrain.basemapDistance = wide ? 650f : 350f;
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
            // whatever this map wants to know about the ground it has just been given
            LocationViews.Active?.Inspect(terrain);
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
            terrain.treeDistance = Quality.TreeDistance(World.Size > 8000f);
            terrain.detailObjectDistance = Quality.DetailDistance;
            terrain.detailObjectDensity = Quality.DetailDensity;
        }

        /// <summary>Ground height at world x/z using the same math the server uses, so client and authority agree.</summary>
        public static float Height(HeightField dem, float x, float z) => World.Ground(dem, x, z);
    }
}
