using System;
using System.Collections.Generic;

namespace Height1079.Core
{
    /// <summary>Documented geometry of the event sites. Every number cites where it comes from; "assumed" marks what the sources do not give.
    /// Placement is computed from the height field so the editor (site prefabs) and tests agree.</summary>
    public static class Sites
    {
        public static class Tent
        {
            /// <summary>Ridge length 4.33 m, one slope 1.14 m, width ~2 m. Source: the criminalistic expertise of the
            /// tent, 3.IV.1959 (case sheets 303–304), and Protocol №199 of 16.IV.1959 (sheets 388–392), Sverdlovsk
            /// forensic laboratory, expert Churkina. (29.V.1959, widely quoted for this, is the date of the
            /// histological analyses; 9.V.1959 in the Russian Wikipedia is wrong as well.)
            /// Fabric: thick cotton of "protective colour" — khaki, not white or grey. Two 4-person "Турист" tents
            /// joined along a double seam; entrance at one end, ventilation sleeve at the other.</summary>
            public const float Length = 4.33f, Width = 2.0f, SlopeLength = 1.14f;
            /// <summary>Ridge height: not given; the documented slope length 1.14 m over half-width 1 m implies ≈0.55 m of wall, plus ~0.5 m vertical sides of a "Турист" tent — assumed 1.05 m.</summary>
            public const float RidgeHeight = 1.05f;
            /// <summary>The snow platform the tent stood on: 4.0 m long, cut 0.5 m into the slope (Gaume &amp; Puzrin
            /// 2021 model of the site). These are the platform's numbers, not the tent's — <see cref="Cuts"/> below is
            /// the knife damage, and the two used to sit here under names that could not be told apart.</summary>
            public const float PadLength = 4.0f, PadCut = 0.5f;
            /// <summary>8 pairs of skis laid under the floor; tent stretched on ski poles and tied with ropes (search protocol 28.02.1959). One pair of skis served as the middle stand (Buyanov).</summary>
            public const int SkiPairsUnderFloor = 8;
            /// <summary>Three cuts made from inside on the downslope roof slant: 0.32, 0.89 and 0.42 m. Churkina found
            /// punctures and thread incisions on the inner surface at 0.6–56× and concluded all three were made from
            /// within, with a knife; some did not go through the whole thickness.</summary>
            public static readonly float[] Cuts = { 0.32f, 0.89f, 0.42f };
            /// <summary>The damage that is NOT a cut, and is bigger than all three of them together: two roughly
            /// rectangular pieces torn out of the same slant, each about 0.80–0.95 m by 0.60–0.75 m (Churkina).
            /// Popular retellings fold all five damages into "the cuts", which is how they came to be missing here.
            /// Modelled at the middle of each range.</summary>
            public static readonly (float length, float width)[] TornSections = { (0.88f, 0.70f), (0.84f, 0.66f) };
            /// <summary>Snow on the tent's northern part when found: 15–20 cm, wind-blown (search reports).</summary>
            public const float SnowOnTentFound = 0.18f;

            /// <summary>Ridge runs along the contour, the cuts face downslope, and the entrance faces SOUTH.
            ///
            /// South is what the record says three times over: the discovery protocol of 28.II.1959 ("the entrance
            /// faces south"), Maslennikov 10.III and Tempalov 18.IV, who adds that the guys on the southern side were
            /// still intact while the northern ones were torn. The 2012 field determination (TL 18.10) agrees, "almost
            /// due south", within about 10°. Atmanaki (7–8.IV) is the one dissent — he puts the tent sideways to the
            /// slope with the entrance east — and this used to follow him, via Buyanov's "entrance toward the pass".
            /// The difference is a right angle and it moves everything: where the cuts face, where the ice axe stands,
            /// which half the snow buried.
            ///
            /// Returns the entrance direction (unit x, z), the downslope direction and the terrain slope.</summary>
            public static (float ex, float ez, float dx, float dz, float slopeDeg) Orientation(HeightField dem)
            {
                var t = WorldData.Tent;
                var (dx, dz, slope) = dem.Fall(t.X, t.Z, 6f);
                float cx = -dz, cz = dx;                     // along the contour, either way round
                if (cz > 0) { cx = -cx; cz = -cz; }          // take the southward one (z is north)
                return (cx, cz, dx, dz, slope);
            }
        }

        public static class Labaz
        {
            /// <summary>Birch-bark floor 1.5 × 1.0 m under 2–3 cm of turf (excavation 03.08.2022, Konstantinov).</summary>
            public const float FloorLength = 1.5f, FloorWidth = 1.0f;
            /// <summary>Food ≈55 kg in the 1959 inventory, alongside a mandolin, spare boots and Dyatlov's warm boots;
            /// marked by one pair of skis stuck upright in the snow with a torn gaiter tied to them (protocol 02.03.1959).</summary>
            public const float FoodKg = 55f;
            /// <summary>Height of the snow heaped over the cache. It is a mound, not a pit: the 2022 excavation found the
            /// birch-bark mat at ground level under 2–3 cm of turf, the protocol of 02.03.1959 describes the store as
            /// "laid over with firewood, covered with boards and fir branches", and the search photographs of 2 March
            /// show a snow mound with skis standing in it. The height itself is not documented — assumed.</summary>
            public const float MoundHeight = 0.8f;
            public const float MarkerSkiLength = 2.1f;
        }

        public static class Cedar
        {
            /// <summary>Canopy height at the KAN cedar point, Meta/WRI CHM (max within 6 m): 18 m. This is the tree as
            /// it stands today, measured from space — the 1959 case files give no height for the cedar at all, and any
            /// figure presented as historical would be invented.</summary>
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
            /// <summary>Nine pairs of skis: eight stood up in the snow beside the tent and one used as a stand.
            /// They are NOT under the floor here. Skis as flooring and a levelled pad are documented for the slope
            /// camp of 1 February (protocol 28.02.1959) and only there; in the forest the diary of 30.01 says the
            /// tent went up on fir branches, and 31.01 describes the same camp.</summary>
            public const int SkiPairs = 9, SpareSkiPairs = 1, SkiPairsAtTent = 8, SkiPairsAsStand = 1;
            /// <summary>Tent stretched on ski poles and tied with ropes: two crossed poles per end, one anchor per end,
            /// four side stakes. The rig itself is from the slope protocol of 28.02.1959 — the diary does not describe
            /// how the forest tent was held up, only what it stood on — so the count is an assumption, the bed is not.</summary>
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
        /// <summary>Where the nine lay on the morning after, and on whose word.
        ///
        /// Drawn as outlines, never as bodies: these are real people with living relatives, and two of them lay
        /// nearly undressed. An outline carries the place and the posture, which is what the scene has to say, and
        /// carries one thing a model could not — that this is a reconstruction by someone standing on the spot.
        ///
        /// The three on the slope have their own coordinates and lay, by the 1959 protocols, almost on the line
        /// from the tent to the cedar with their heads toward the tent. The two at the cedar were found a metre
        /// north of the fire at its foot. The four in the ravine have no individual coordinates at all — only the
        /// KAN point for the group of them — so they are spread around it and the note says so.</summary>
        public static class Fallen
        {
            public sealed class Spot
            {
                public string Id = "", Note = "";
                public float X, Z;
                /// <summary>Head toward the tent, as the protocols put the three on the slope.</summary>
                public bool HeadToTent;
            }

            static Spot At(string id, float x, float z, bool head, string note)
                => new Spot { Id = id, X = x, Z = z, HeadToTent = head, Note = note };

            public static readonly IReadOnlyList<Spot> All = Build();

            static IReadOnlyList<Spot> Build()
            {
                var k = WorldData.Get("kolmogorova");
                var sl = WorldData.Get("slobodin");
                var dy = WorldData.Get("dyatlov");
                var (fx, fz) = Cedar.Fire;
                var rv = WorldData.Ravine;
                return new List<Spot>
                {
                    At("kolmogorova", k.X, k.Z, true, "≈630 м от кедра выше по склону, лицом вниз, головой к палатке (протоколы 1959)"),
                    At("slobodin", sl.X, sl.Z, true, "≈480 м от кедра, на линии палатка — кедр (протоколы 1959)"),
                    At("dyatlov", dy.X, dy.Z, true, "≈300 м от кедра, у берёзы, лицом вверх, головой к палатке (протоколы 1959)"),
                    At("doroshenko", fx - 0.55f, fz + Cedar.BodiesNorthOfFire, false, "у подножия кедра, в метре к северу от костра, лицом вниз, на тонком слое лапника"),
                    At("krivonishchenko", fx + 0.55f, fz + Cedar.BodiesNorthOfFire, false, "рядом с Дорошенко, на спине"),
                    At("dubinina", rv.X - 1.4f, rv.Z + 0.6f, false, "в русле ручья ниже настила; индивидуальных координат нет, место — по точке КАН"),
                    At("kolevatov", rv.X - 0.4f, rv.Z + 1.1f, false, "в русле ручья ниже настила; индивидуальных координат нет"),
                    At("zolotaryov", rv.X + 0.7f, rv.Z + 0.3f, false, "в русле ручья ниже настила; индивидуальных координат нет"),
                    At("thibeaux", rv.X + 1.5f, rv.Z - 0.5f, false, "в русле ручья ниже настила; индивидуальных координат нет"),
                };
            }
        }

        public const float TreeLine = 750f;
    }
}
