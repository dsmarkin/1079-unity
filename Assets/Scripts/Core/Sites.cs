using System;

namespace Height1079.Core
{
    /// <summary>Documented geometry of the event sites. Every number cites where it comes from; "assumed" marks what the sources do not give.
    /// Placement is computed from the height field so the editor (site prefabs) and tests agree.</summary>
    public static class Sites
    {
        public static class Tent
        {
            /// <summary>Ridge length 4.33 m, one slope 1.14 m, width ~2 m (Churkina expertise 29.05.1959).</summary>
            public const float Length = 4.33f, Width = 2.0f, SlopeLength = 1.14f;
            /// <summary>Ridge height: not given; the documented slope length 1.14 m over half-width 1 m implies ≈0.55 m of wall, plus ~0.5 m vertical sides of a "Турист" tent — assumed 1.05 m.</summary>
            public const float RidgeHeight = 1.05f;
            /// <summary>Platform levelled into the snow: 4.0 m long, 0.5 m cut (Gaume &amp; Puzrin 2021 model of the site).</summary>
            public const float CutLength = 4.0f, CutDepth = 0.5f;
            /// <summary>8 pairs of skis laid under the floor; tent stretched on ski poles and tied with ropes (search protocol 28.02.1959). One pair of skis served as the middle stand (Buyanov).</summary>
            public const int SkiPairsUnderFloor = 8;
            /// <summary>Three cuts made from inside on the downslope side: 0.32, 0.89 and 0.42 m (Churkina expertise).</summary>
            public static readonly float[] Cuts = { 0.32f, 0.89f, 0.42f };
            /// <summary>Snow on the tent's northern part when found: 15–20 cm, wind-blown (search reports).</summary>
            public const float SnowOnTentFound = 0.18f;

            /// <summary>Ridge runs along the contour; the cuts face downslope; the entrance end is the one closer to the pass (Buyanov).
            /// Returns the entrance direction (unit x, z), the downslope direction and the terrain slope.</summary>
            public static (float ex, float ez, float dx, float dz, float slopeDeg) Orientation(HeightField dem)
            {
                var t = WorldData.Tent;
                var (dx, dz, slope) = dem.Fall(t.X, t.Z, 6f);
                float cx = -dz, cz = dx; // contour direction
                float px = WorldData.Saddle.X - t.X, pz = WorldData.Saddle.Z - t.Z;
                if (cx * px + cz * pz < 0) { cx = -cx; cz = -cz; }
                return (cx, cz, dx, dz, slope);
            }
        }

        public static class Labaz
        {
            /// <summary>Birch-bark floor 1.5 × 1.0 m under 2–3 cm of turf (excavation 03.08.2022, Konstantinov).</summary>
            public const float FloorLength = 1.5f, FloorWidth = 1.0f;
            /// <summary>Food ≈55 kg in the 1959 inventory; the cache was dug into snow, covered with firewood, boards(?) and fir branches; marked by one ski stuck in the snow with a torn gaiter on it.</summary>
            public const float FoodKg = 55f;
            /// <summary>Pit depth in the snow: not documented — assumed 0.8 m (valley snow 1.2–2 m per the diary of 31.01).</summary>
            public const float PitDepth = 0.8f;
            public const float MarkerSkiLength = 2.1f;
        }

        public static class Cedar
        {
            /// <summary>Canopy height at the KAN cedar point, Meta/WRI CHM (max within 6 m): 18 m.</summary>
            public const float Height = 18f;
            /// <summary>Branches broken up to 4–5 m on the side facing the slope/tent (Atmanaki; protocols give 2–2.5 m for dry twigs around).</summary>
            public const float BrokenUpTo = 4.5f;
            /// <summary>Fire in a pit under the cedar; the two bodies lay 1 m north of it (protocol). Offset of the pit from the trunk: assumed 1.5 m toward the tent.</summary>
            public const float FireOffset = 1.5f, BodiesNorthOfFire = 1.0f;

            public static (float x, float z) TowardTent(float metres)
            {
                var c = WorldData.Cedar; var t = WorldData.Tent;
                float d = WorldData.Distance(c.X, c.Z, t.X, t.Z);
                return (c.X + (t.X - c.X) / d * metres, c.Z + (t.Z - c.Z) / d * metres);
            }

            public static (float x, float z) Fire => TowardTent(FireOffset);
        }

        public static class Den
        {
            /// <summary>Floor of 14 fir tops and one birch, trunks 1–2 m; about 2 × 1.5 m; branch layer 20–30 cm; four clothing items laid on it (search protocol May 1959).</summary>
            public const int FirTrunks = 14, BirchTrunks = 1;
            public const float Length = 2.0f, Width = 1.5f, Layer = 0.25f, TrunkMaxLength = 2.0f;
            /// <summary>Snow above the floor in May: 2.5–3 m (protocol). In February the depth is unknown; the floor sits in a pit dug in the stream bed.</summary>
            public const float SnowAboveInMay = 2.75f;
        }

        /// <summary>Night camp of 31 Jan 1959 as it stood on the morning of 1 Feb, before the labaz was built and the group left (~15:00).
        /// Counts come from the search inventory of the tent (28.02–02.03.1959) and the labaz protocol, i.e. what the nine carried up the next day.</summary>
        public static class Camp31
        {
            /// <summary>Nine hikers after Yudin turned back on 28 Jan.</summary>
            public const int Hikers = 9;
            /// <summary>9 pairs of skis in use + 1 spare pair (university equipment list). On the slope: 8 pairs under the floor, one pair as the middle stand;
            /// a spare ski marked the labaz. The same pitching is assumed here, the spare pair stands in the snow.</summary>
            public const int SkiPairs = 9, SpareSkiPairs = 1, SkiPairsUnderFloor = 8, SkiPairsAsStand = 1;
            /// <summary>Tent pitched on ski poles and tied with ropes (protocol 28.02.1959): two crossed poles per end, one anchor per end, four side stakes.</summary>
            public const int PolesInTentRig = 10;
            public static int PolesLeftFree => SkiPairs * 2 - PolesInTentRig;
            /// <summary>Found in the tent: 9 rucksacks, 9 blankets, 2 buckets, 2 cooking pots, 3 axes (2 large, 1 small in a leather case), 1 saw, stove with pipe.</summary>
            public const int Rucksacks = 9, Blankets = 9, Buckets = 2, Pots = 2, LargeAxes = 2, SmallAxes = 1, Saws = 1;
            /// <summary>Dyatlov's folding tin stove, 190 × 240 × 400 mm, 4 kg, 3 m pipe; hung from the ridge rope, horizontal pipe out through the rear end,
            /// with an asbestos ring and a ring of raw wooden bars 25–30 cm (Sokhansky; Lebedev's "firewood" at the rear of the tent).</summary>
            public const float StoveHeight = .19f, StoveWidth = .24f, StoveLength = .40f, PipeLength = 3f, RingBarLength = .28f;
            /// <summary>Snow in the valley 1.2–2 m (diary 31.01), so the pad is trampled and walled rather than dug to the ground — assumed depth 0.5 m.</summary>
            public const float PadDepth = .5f;
        }

        /// <summary>Tree line in this valley from the canopy model: 95% of trees stand below ~750 m.</summary>
        public const float TreeLine = 750f;
    }
}
