using System.Collections.Generic;

namespace Height1079.Core
{
    /// <summary>Холатчахль, the night of 1–2 February 1959: the location the game is built around and the one that is
    /// always in the build. Everything it answers with already lived somewhere in the core — this class only gathers
    /// it in one place so that <see cref="World"/> can stop asking which place it is in.</summary>
    public sealed class KholatLocation : ILocation
    {
        public static readonly KholatLocation Instance = new KholatLocation();
        KholatLocation() { }

        public Place Place => Place.Kholat;
        public string Title => "Холатчахль · ночь 1–2 февраля";

        public float Half => HeightField.Half;
        public int Resolution => HeightField.Resolution;
        public float GridStep => HeightField.Step;
        public float HeightMin => WorldData.HeightMin;
        public float HeightMax => WorldData.HeightMax;

        public string TerrainAsset => "Kholat";
        public string HeightAsset => "height_2049";

        /// <summary>The map carves its stream and levels the camp pads, so the ground is not the bare DEM.</summary>
        public float Ground(HeightField dem, float x, float z) => WorldData.GroundHeight(dem, x, z);

        public NightRun.Scenario Scenario => NightRun.Scenario.Slope;

        /// <summary>The tent on the slope: what the night is walked towards.</summary>
        public (float x, float z) Goal => (WorldData.Tent.X, WorldData.Tent.Z);

        public float SnowDepth(HeightField dem, float x, float z) => SnowCover.KholatDepth(dem, x, z);
        public float SnowCrust(HeightField dem, float x, float z) => SnowCover.KholatCrust(dem, x, z);
        public float Canopy(float x, float z, float y) => SnowCover.KholatCanopy(y);

        public List<ItemStack> Starter(int index) => Items.StarterFor(index);
    }
}
