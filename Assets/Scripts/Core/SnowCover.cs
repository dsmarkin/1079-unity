using System;

namespace Height1079.Core
{
    /// <summary>Snow as a property of the map: how deep the pack is at a point and how well it carries a hiker.
    /// Both are computed procedurally from the height field — altitude belt, wind exposure, curvature, forest and
    /// the stream incision — so they need no extra data file and are the same on the host and on every client.
    /// A measured raster can be dropped in later through <see cref="Raster"/> without touching the callers.
    ///
    /// Depth is metres of snow above the ground (<see cref="World.Ground"/>), for the last days of January.
    /// Numbers and where they come from:
    /// <list type="bullet">
    /// <item>Valley forest 1.2 m: the group's own measurement. Diary 30.01 "снег до 120 см глубиной", 31.01
    /// "по снегу 1,20 м", and 28.01 "глубина снега в этом году значительно меньше, чем в прошлом" — a thin winter
    /// (ermakvagus.com diary transcript, see docs/MAP.md).</item>
    /// <item>The subalpine belt of the Northern Urals averages 160–205 cm and gains about 35 cm per 100 m of height
    /// between 650 and 850 m; the forest belt below gains 10–12 cm per 100 m; drifts reach 300–350 cm and deep snow
    /// collectors 4–6 m (Gorchakovsky, activestudy.info/zakonomernosti-snegonakopleniya-i-snegotayaniya-v-vysokogoryax-urala).</item>
    /// <item>Above the tree line the wind takes it away: mountain tundra holds 4–25 cm, and 20–60 cm between the
    /// boulders of the курумы; 20–25 cm of fresh snow is carried off the bare tops into the subalpine forest within a
    /// day of a snowfall (same source). Diary 31.01 on the ridge: "Наст, голые места."</item>
    /// <item>The tent slope: Gaume &amp; Puzrin (2021, doi 10.1038/s43247-020-00081-8) model a slab 0.5 m thick at the
    /// cut and 0.1 m on the upper slope, with a further 0.24–0.44 m of wind-transported snow deposited over the night
    /// at 2–12 m/s. Wind-drifted snow packs to 400 kg/m³ against 300 in the undisturbed slab.</item>
    /// <item>The ravine of the stream is a snow collector: the four lay under 2–2.5 m of snow and the настил under
    /// 2.5–3 m in May, with a probe reaching 4 m (search protocols, dyatlovpass.com/the-den; see
    /// <see cref="Sites.Den.SnowAboveInMay"/>).</item>
    /// </list>
    /// The shape of the model — which multiplier does what — is a game rule, not a measurement.
    ///
    /// Everything below is the Kholat Syakhl model; <see cref="Depth"/>, <see cref="Crust"/> and <see cref="Forest"/>
    /// hand the question to <see cref="Locations.Active"/>, so another location brings its own snow with it.</summary>
    public static class SnowCover
    {
        /// <summary>A measured depth raster, when there is one: set it and the procedural model below is bypassed.
        /// Metres of snow at world x/z.</summary>
        public static Func<float, float, float> Raster;

        /// <summary>Canopy cover 0..1 at world x/z — canopy_2049.r8 through <see cref="Dem.Mask"/>, if the host wires
        /// it up. Without it the model reads the forest off the altitude belts of docs/MAP.md.</summary>
        public static Func<float, float, float> Canopy;

        /// <summary>Where the wind comes from, compass degrees. The valley diary of 31.01 has "ветер западный, теплый,
        /// пронзительный"; the slope's own flow is the north-westerly stream over the ridge that Gaume &amp; Puzrin
        /// model as katabatic down the eastern side. 295° (WNW) sits between the two.</summary>
        public const float WindFromDeg = 295f;

        /// <summary>Metres the slope and the curvature are read over. 24 m is wide enough to see a break of slope or a
        /// мульда and narrow enough to keep the три каменные гряды-scale forms out of it.</summary>
        public const float Span = 24f;

        /// <summary>Thinnest and deepest the model will return: a blown-clear stone field and the bottom of a drifted
        /// hollow. The 300–350 cm drifts of the sources are outside the playable slope, so 3.2 m is the ceiling.</summary>
        public const float MinDepth = .02f, MaxDepth = 3.2f;

        // level ground, by altitude belt (metres of snow)
        const float ValleyFrom = 500f, ValleyDepth = .88f, ValleyGain = .0011f;   // +10–12 cm per 100 m in the taiga
        const float SubalpineFrom = 620f, SubalpineGain = .0035f;                  // +35 cm per 100 m to the tree line
        const float OpenDepth = .40f, OpenLoss = .0011f;                           // wind-swept slope, thinning upward
        const float BareFrom = Sites.TreeLine - 60f, BareOver = 110f;              // forest → bare tundra

        // what the wind does to that
        const float Sheltered = .85f;      // share of the wind the canopy takes away
        const float Drifting = 1.5f;       // a hollow or the lee of a break holds this much more
        const float Scouring = .62f;       // a ridge or a convex nose keeps this much less
        const float Aspect = .40f;         // lee slopes gain, slopes facing the wind lose
        public const float BendRelief = 2.2f;   // metres of relief over Span that count as a full hollow or ridge
        const float Trapped = .15f;        // snow blown off the tops and caught by the trees
        const float Collector = .95f, CollectorWidth = 8f;   // the stream ravine fills up

        // how well the surface carries: 0 = powder, 1 = wind crust you walk on top of
        const float Loose = .06f, Ceiling = .95f, Board = .55f, Blown = .35f, Frozen = .18f;

        static readonly float FlowX = -(float)Math.Sin(WindFromDeg * Math.PI / 180);
        static readonly float FlowZ = -(float)Math.Cos(WindFromDeg * Math.PI / 180);

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        static float Smooth(float t) { t = Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>Depth of the snow pack, metres, at a point of the world frame. The measured raster wins; after
        /// that the active location answers (<see cref="ILocation.SnowDepth"/>).</summary>
        public static float Depth(HeightField dem, float x, float z)
        {
            if (Raster != null) return Math.Max(0f, Raster(x, z));
            return Locations.Active.SnowDepth(dem, x, z);
        }

        /// <summary>The procedural model this file documents: the late-January pack of Kholat Syakhl.</summary>
        public static float KholatDepth(HeightField dem, float x, float z)
        {
            float y = dem.Sample(x, z);
            var (fx, fz, slopeDeg) = dem.Fall(x, z, Span);
            float trees = Forest(x, z, y);
            float reach = 1f - Sheltered * trees;                                  // how much wind gets to the ground
            float lee = Clamp(fx * FlowX + fz * FlowZ, -1f, 1f);                   // +1: the ground falls away downwind
            float bend = Clamp(Hollow(dem, x, z, Span) / BendRelief, -1f, 1f);     // +1: a hollow, -1: a ridge

            float depth = Belt(y);
            depth *= 1f + reach * (bend > 0f ? Drifting * bend : Scouring * bend);
            depth *= 1f + reach * Aspect * lee * Math.Min(1f, slopeDeg / 12f);
            depth += Trapped * trees;
            float creek = WorldData.CreekDistance(x, z);
            depth += Collector * (float)Math.Exp(-creek * creek / (2 * CollectorWidth * CollectorWidth));
            return Clamp(depth, MinDepth, MaxDepth);
        }

        /// <summary>How well the surface bears weight: 0 — powder you go through, 1 — the wind crust of the diary's
        /// "наст, голые места", which carries a loaded walker. Late January is still early in the season, so the
        /// ceiling stays below 1: the wind-packed surface of these mountains only reaches a density of 0.50–0.55 by
        /// the beginning of March.</summary>
        public static float Crust(HeightField dem, float x, float z) => Locations.Active.SnowCrust(dem, x, z);

        /// <summary>The Kholat half of <see cref="Crust"/>: the wind board of the diary's "наст, голые места".</summary>
        public static float KholatCrust(HeightField dem, float x, float z)
        {
            float y = dem.Sample(x, z);
            var (fx, fz, _) = dem.Fall(x, z, Span);
            float trees = Forest(x, z, y);
            float reach = 1f - Sheltered * trees;
            float lee = Clamp(fx * FlowX + fz * FlowZ, -1f, 1f);
            float windward = .5f * (1f - lee);                                      // 1 facing the wind, 0 in the lee
            float worked = reach * (Board + Blown * windward);
            worked *= 1f + Frozen * Clamp01((y - Sites.TreeLine) / 250f);           // the higher, the harder it is worked
            // where the wind has taken almost everything there is nothing soft left to break through: a hard floor
            // of old wind board, or the stones of the "голые места" the diary notes on the ridge
            float scraped = (1f - trees) * (1f - Smooth(Depth(dem, x, z) / .25f));
            worked = Math.Max(worked, scraped * .95f);
            return Clamp(Loose + (Ceiling - Loose) * Clamp01(worked), 0f, 1f);
        }

        /// <summary>Depth on level, sheltered ground at this altitude, before wind and forest work on it.</summary>
        public static float Belt(float y)
        {
            float forest = ValleyDepth
                + Math.Max(0f, Math.Min(y, SubalpineFrom) - ValleyFrom) * ValleyGain
                + Math.Max(0f, Math.Min(y, Sites.TreeLine) - SubalpineFrom) * SubalpineGain;
            float open = OpenDepth - Math.Max(0f, y - Sites.TreeLine) * OpenLoss;
            float bare = Smooth((y - BareFrom) / BareOver);
            return forest + (open - forest) * bare;
        }

        /// <summary>How much canopy stands over a point, 0..1. The measured mask wins; after that the active location
        /// answers (<see cref="ILocation.Canopy"/>).</summary>
        public static float Forest(float x, float z, float y)
        {
            if (Canopy != null) return Clamp01(Canopy(x, z));
            return Clamp01(Locations.Active.Canopy(x, z, y));
        }

        /// <summary>The Kholat belts of docs/MAP.md: dark taiga to ~620 m, the tree line at 700–750 m ("кончились
        /// ели, пошёл редкий березняк", diary 31.01), bare tundra above.</summary>
        public static float KholatCanopy(float y)
            => 1f - Smooth((y - SubalpineFrom) / (Sites.TreeLine + 30f - SubalpineFrom));

        /// <summary>How the ground bends over <paramref name="span"/> metres: positive in a hollow, where the point
        /// sits below its surroundings and the drift settles; negative on a ridge or a convex break, which is blown
        /// clear. Metres of relief — the mean of the four neighbours minus the point itself.</summary>
        public static float Hollow(HeightField dem, float x, float z, float span = Span)
        {
            float c = dem.Sample(x, z);
            float m = (dem.Sample(x + span, z) + dem.Sample(x - span, z)
                     + dem.Sample(x, z + span) + dem.Sample(x, z - span)) * .25f;
            return m - c;
        }
    }
}
