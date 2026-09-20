namespace Height1079.Core
{
    /// <summary>Which of the mapped places the session plays in. This enum is part of the save format, which is why
    /// it names every location the game has ever had, including ones a particular build was compiled without.</summary>
    public enum Place
    {
        /// <summary>Холатчахль, ночь 1–2 февраля 1959 года — 4 096 × 4 096 м, ArcticDEM 2 м.</summary>
        Kholat,
        /// <summary>Южный склон Эльбруса — 12 288 × 12 288 м, канатные дороги, приюты и маршрут на Западную вершину.
        /// Optional layer: present unless the build sets HEIGHT1079_NO_ELBRUS (Assets/Scripts/Core/Elbrus).</summary>
        Elbrus,
    }

    /// <summary>The one global the whole game reads to know where it is. Set before the session starts (menu) and never
    /// changed while a run is going, so client and host always agree on the frame, the terrain asset and the rules.
    ///
    /// Nothing here knows which place it is answering for: every property is the matching one on
    /// <see cref="Locations.Active"/>. A location that is not compiled into this build cannot be selected
    /// (<see cref="Current"/> refuses it), so no caller ever gets half a location.</summary>
    public static class World
    {
        static Place current = Place.Kholat;

        /// <summary>Where the session is. Assigning a place this build was compiled without leaves it where it was —
        /// an old save or a stale menu choice cannot put the game on a map it does not have.</summary>
        public static Place Current
        {
            get => current;
            set { if (Locations.Has(value)) current = value; }
        }

        /// <summary>Kept for the runtime and the editor, which still branch on it in about fifty places. The core
        /// rules do not use it any more, and it names no type outside the enum above.</summary>
        public static bool IsElbrus => current == Place.Elbrus;

        /// <summary>The location the session is in. Never null.</summary>
        public static ILocation Active => Locations.Of(current);

        public static string Title => Active.Title;

        /// <summary>Half the side of the playable square, metres.</summary>
        public static float Half => Active.Half;
        public static float Size => Half * 2;
        public static int Resolution => Active.Resolution;
        public static float GridStep => Active.GridStep;
        public static float HeightMin => Active.HeightMin;
        public static float HeightMax => Active.HeightMax;
        /// <summary>Name of the TerrainData asset; the game finds it through WorldAssets (Assets/Generated/World).</summary>
        public static string TerrainAsset => Active.TerrainAsset;
        /// <summary>Name of the height raster; the game finds it through WorldAssets (Assets/Generated/World).</summary>
        public static string HeightAsset => Active.HeightAsset;

        /// <summary>Ground height in the active place.</summary>
        public static float Ground(HeightField dem, float x, float z) => Active.Ground(dem, x, z);

        public static NightRun.Scenario Scenario => Active.Scenario;

        /// <summary>Where the run is trying to get to, world x/z.</summary>
        public static (float x, float z) Goal => Active.Goal;
    }
}
