using System;

namespace Height1079.Core
{
    /// <summary>Which of the two mapped places the session plays in.</summary>
    public enum Place
    {
        /// <summary>Холатчахль, ночь 1–2 февраля 1959 года — 4 096 × 4 096 м, ArcticDEM 2 м.</summary>
        Kholat,
        /// <summary>Южный склон Эльбруса — 12 288 × 12 288 м, канатные дороги, приюты и маршрут на Западную вершину.</summary>
        Elbrus,
    }

    /// <summary>The one global the whole game reads to know where it is. Set before the session starts (menu) and never
    /// changed while a run is going, so client and host always agree on the frame, the terrain asset and the rules.</summary>
    public static class World
    {
        public static Place Current = Place.Kholat;
        public static bool IsElbrus => Current == Place.Elbrus;

        public static string Title => Current == Place.Elbrus ? "Эльбрус · южный склон" : "Холатчахль · ночь 1–2 февраля";

        /// <summary>Half the side of the playable square, metres.</summary>
        public static float Half => Current == Place.Elbrus ? Elbrus.Half : HeightField.Half;
        public static float Size => Half * 2;
        public static int Resolution => Current == Place.Elbrus ? Elbrus.Resolution : HeightField.Resolution;
        public static float GridStep => Current == Place.Elbrus ? Elbrus.GridStep : HeightField.Step;
        public static float HeightMin => Current == Place.Elbrus ? Elbrus.HeightMin : WorldData.HeightMin;
        public static float HeightMax => Current == Place.Elbrus ? Elbrus.HeightMax : WorldData.HeightMax;
        /// <summary>Name of the TerrainData asset under Resources/World.</summary>
        public static string TerrainAsset => Current == Place.Elbrus ? "Elbrus" : "Kholat";
        /// <summary>Name of the height raster under Resources/World.</summary>
        public static string HeightAsset => Current == Place.Elbrus ? "elbrus_height_2049" : "height_2049";

        /// <summary>Ground height in the active place (the Kholat map carves its stream and levels the camp pads;
        /// on Elbrus the DEM is the ground).</summary>
        public static float Ground(HeightField dem, float x, float z)
            => Current == Place.Elbrus ? dem.Sample(x, z) : WorldData.GroundHeight(dem, x, z);

        /// <summary>Huts, barrels and terminals a hiker can shelter in on the Elbrus map.</summary>
        static readonly string[] ShelterIds = { "azau", "krugozor", "mir", "garabashi", "barrels", "redfox", "leaprus", "garabashiHut", "priut11", "priut88" };

        public static bool ElbrusSheltered(float x, float z)
        {
            foreach (var id in ShelterIds)
            {
                var p = Elbrus.Get(id);
                if (Elbrus.Distance(x, z, p.X, p.Z) < 16f) return true;
            }
            return false;
        }

        public static NightRun.Scenario Scenario => Current == Place.Elbrus ? ElbrusPlan : NightRun.Scenario.Slope;

        /// <summary>Day on the southern slope: no February night, no blizzard cycle, and 90 minutes to get up and down.
        /// The cold still bites above the shelf, which is why the profile is not switched off entirely.</summary>
        public static readonly NightRun.Scenario ElbrusPlan = new NightRun.Scenario
        {
            Id = "elbrus",
            Intro = "Поляна Азау, 2350 м. Наверх — канатной дорогой через Кругозор и Мир до Гара-Баши, дальше ратрак или пешком. Западная вершина — 5642 м.",
            Start = Elbrus.Start,
            NearFireplace = null,
            AtGoal = Elbrus.AtSummit,
            Sheltered = (x, z, y) => ElbrusSheltered(x, z),
            Storms = false,
            Profile = SurvivalRules.Profile.Day,
            SpawnRadius = 2.2f,
        };
    }
}
