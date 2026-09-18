using System;

namespace Height1079.Core
{
    /// <summary>How much of the mountain can be seen at all. The metres are what guides quote on the southern side.</summary>
    public enum SkyState : byte { Clear, Cloud, Snow, Blizzard, WhiteOut }

    /// <summary>The route itself: what marks it, what happens when the marks stop reading, where the crevasses really
    /// are, what a snow-cat changes, when the day turns, and where the last roof is.
    ///
    /// The one fact this file is built around: on the southern side losing the route in bad visibility is what kills.
    /// About 70 % of the deaths follow from it — not from falls, not from the cold on its own. So the rules here are
    /// mostly about being able to see the next wand, the МЧС cable and the snow-cat track, and about the right answer
    /// to a пурга being to stop and dig in rather than to walk.
    ///
    /// Anchors:
    /// <list type="bullet">
    /// <item>Wands every ~40 m, 1.5 m tall, readable at about four fifths of the visibility. After 10 cm of new snow
    /// 30–60 % of them stop reading.</item>
    /// <item>The МЧС steel cable was strung in 2013 from 4 900 to 5 250 m, exactly so that a party in cloud has
    /// something to hold. Holding it you cannot lose the route; it stops at 5 250 m and so does the radio.</item>
    /// <item>Crevasses are on the Garabashi glacier between the barrels and the rise to Приют 11 — about eight of
    /// them, four of the upper ones dangerous, the critical band 50–100 m below the rise. The rule a climber has to
    /// learn is that right of the snow-cat lane is forbidden and left of it is free. Bridges are thinner in August
    /// (8 %) than in May–June (2 %). Where a stream runs on the surface there is no crevasse under it.</item>
    /// <item>Snow-cats drop at 4 800 or 5 100 m (the groomed lane really ends at 5 080), 40 minutes instead of five
    /// to six hours, and give no acclimatisation at all. Over 20 m/s of wind or under 200 m of visibility they turn
    /// round at Pastukhov rocks.</item>
    /// <item>Above 4 200 m the only roof is the Red Fox hut on the saddle at 5 300 m — six lying, twelve sitting,
    /// good for 80 m/s, for emergencies only. A fumarole 19 m away is a warm patch, a landmark and sulphur dioxide.
    /// The Soviet ruins beside it are not shelter.</item>
    /// </list></summary>
    public static class AscentRoute
    {
        // ── visibility and what marks the line ────────────────────────────────────────────────────────────

        public static float VisibilityM(SkyState s) => s switch
        {
            SkyState.Cloud => 300f,
            SkyState.Snow => 50f,
            SkyState.Blizzard => 10f,
            SkyState.WhiteOut => 1f,
            _ => 5000f,
        };

        public static string SkyTitle(SkyState s) => s switch
        {
            SkyState.Cloud => "Облачность",
            SkyState.Snow => "Снег",
            SkyState.Blizzard => "Пурга",
            SkyState.WhiteOut => "Сильная пурга",
            _ => "Ясно",
        };

        public const float WandSpacingM = 40f, WandHeightM = 1.5f;
        /// <summary>A wand is picked out at about four fifths of the general visibility — it is a thin stick.</summary>
        public const float WandSightShare = .8f;

        /// <summary>Share of the wands still readable after this much new snow. Ten centimetres is nothing; beyond
        /// that 30–60 % of them go under.</summary>
        public static float WandsReadable(float freshSnowCm)
            => 1f - .6f * Ascent.Clamp01((freshSnowCm - 10f) / 25f);

        /// <summary>Metres between the wands you can still see.</summary>
        public static float WandSpacing(float freshSnowCm) => WandSpacingM / Math.Max(.1f, WandsReadable(freshSnowCm));

        /// <summary>Whether the next wand can be seen from here. Straying off the line makes it further away, which is
        /// what turns a small error into a lost route.</summary>
        public static bool WandInSight(float visibilityM, float freshSnowCm, float offRouteM)
        {
            float half = WandSpacing(freshSnowCm) * .5f, off = Math.Abs(offRouteM);
            return visibilityM * WandSightShare >= (float)Math.Sqrt(half * half + off * off);
        }

        // ── the МЧС cable ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>Strung in 2013, from 4 900 to 5 250 m and not one metre further either way.</summary>
        public const float RopeFromEle = 4900f, RopeToEle = 5250f;
        /// <summary>How far from the line you can still put a hand on it.</summary>
        public const float RopeReachM = 3f;

        public static bool HasFixedRope(float ele) => ele >= RopeFromEle && ele <= RopeToEle;

        /// <summary>The cable as a handrail: anybody within reach of it knows where the route is, gear or no gear.</summary>
        public static bool RopeInReach(float ele, float offRouteM)
            => HasFixedRope(ele) && Math.Abs(offRouteM) <= RopeReachM;

        /// <summary>The cable as protection. «Верёвка бесполезна, пока на вас не надета система» — without a harness
        /// and a cow's tail the cable stops nothing, and the hands that hold it have to come out of the mittens.</summary>
        public static bool RopeProtects(float ele, float offRouteM, Gear gear)
            => RopeInReach(ele, offRouteM) && (gear & Gear.Harness) != 0;

        // ── losing the route ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Under this much visibility the corridor stops reading by itself.</summary>
        public const float LostVisibilityM = 50f;
        /// <summary>Half-width of the corridor a party keeps to, metres.</summary>
        public const float CorridorM = 50f;

        /// <summary>Can the route still be read from here? Anything will do: enough visibility, a wand, the cable, or
        /// the snow-cat's own track underfoot. With none of them the corridor is gone and the climber drifts down the
        /// true gradient of the slope, which is not where the route goes.</summary>
        public static bool CanReadTheRoute(float ele, float offRouteM, float visibilityM, float freshSnowCm, bool catTrackUnderfoot)
            => visibilityM >= LostVisibilityM
            || RopeInReach(ele, offRouteM)
            || catTrackUnderfoot
            || WandInSight(visibilityM, freshSnowCm, offRouteM);

        /// <summary>Stray more than this far below the saddle and the slope delivers you to the ice cliffs.</summary>
        public const float SaddleStrayM = 150f;
        public static bool OnTheSeracs(float metresBelowSaddle) => metresBelowSaddle > SaddleStrayM;

        /// <summary>The right answer to a пурга is not to walk. A trench 1.5 m deep takes three to four minutes and
        /// costs strength, and the cold then works at a third of its rate.</summary>
        public const float TrenchSeconds = 210f, TrenchDepthM = 1.5f, TrenchColdFactor = 1f / 3f;

        /// <summary>Above this the radio is dead — the same height the cable stops at.</summary>
        public const float RadioCeiling = 5250f;
        public static bool HasSignal(float ele) => ele < RadioCeiling;

        // ── the clock ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Parties leave the barrels between two and four in the morning and walk in the dark until about
        /// half past five.</summary>
        public const float StartFromHour = 2f, StartToHour = 4f, DawnHour = 5.5f;
        /// <summary>The turn-round time. Past it you go down whatever the summit looks like.</summary>
        public const float TurnaroundHour = 13f;

        public static bool Dark(float hour) => hour < DawnHour;
        public static bool PastTurnaround(float hour) => hour >= TurnaroundHour;

        /// <summary>The game's own morning. A run on the southern slope is not a real fourteen-hour day: it lasts
        /// <see cref="SurvivalRules.Profile.Day"/> seconds and covers <see cref="RunHours"/> hours of the clock,
        /// starting at <see cref="RunStartHour"/>. Five in the morning is the late end of what parties actually do
        /// (two to four at the barrels), chosen so that first light comes half an hour in and the rest of the run is
        /// walked in daylight — the sky of the map is a day sky. <see cref="TurnaroundHour"/> then falls at eight
        /// ninths of the run: late, but reachable, and the whole point is that it is reachable and still not enough.</summary>
        public const float RunStartHour = 5f, RunHours = 9f;

        /// <summary>The hour of the day after this many seconds of a run of <paramref name="runSeconds"/>.</summary>
        public static float HourAt(float elapsedSeconds, float runSeconds)
            => RunStartHour + RunHours * (runSeconds <= 0f ? 0f : Math.Max(0f, elapsedSeconds) / runSeconds);

        /// <summary>Seconds of a run still to go before the clock reads <paramref name="hour"/>, or 0 once it is past.</summary>
        public static float SecondsUntil(float hour, float elapsedSeconds, float runSeconds)
        {
            float target = runSeconds * (hour - RunStartHour) / RunHours;
            return Math.Max(0f, target - Math.Max(0f, elapsedSeconds));
        }

        /// <summary>«07:35» — the clock as the HUD writes it.</summary>
        public static string Clock(float hour)
        {
            int minutes = (int)Math.Floor(hour * 60f) % 1440;
            if (minutes < 0) minutes += 1440;
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        /// <summary>Chance per hour that the weather breaks. Nothing before noon, then it climbs twice.</summary>
        public static float WeatherRiskPerHour(float hour) => hour < 12f ? 0f : hour < 14f ? .08f : .20f;

        /// <summary>Seconds from the first sign to a full пурга: an hour and a half to two in summer, a quarter of an
        /// hour in winter. The temperature can fall 10–15 °C inside half an hour of the same front.</summary>
        public static float StormOnsetSeconds(bool winter) => winter ? 1050f : 6300f;
        public const float FrontTempDropC = 12.5f, FrontTempDropSeconds = 1800f;

        // ── crevasses, where they actually are ────────────────────────────────────────────────────────────

        /// <summary>The Garabashi glacier between the barrels and the rise to Приют 11. Nothing on the saddle,
        /// nothing on the косая полка — the crevasses are down here, where nobody expects them.</summary>
        public const float CrevasseFromEle = 3700f, CrevasseToEle = 4050f;
        public const int CrevasseCount = 8, DangerousCrevasses = 4;
        /// <summary>The critical band is 50–100 m below the rise to the hut.</summary>
        public const float CriticalFromEle = 3950f, CriticalToEle = 4020f;

        /// <summary>How far right of the lane is still the lane.</summary>
        public const float LaneHalfWidthM = 6f;
        /// <summary>Metres of straying right that reach the far side of the crevasse field.</summary>
        public const float CrevasseFieldM = 60f;
        /// <summary>Metres of the cat lane the dangerous crevasses are spread over.</summary>
        public const float CrevasseBandM = 1800f;

        public const float AugustBridge = .08f, EarlySummerBridge = .02f;

        /// <summary>Chance a bridge gives way under one crossing, by month. In August the mosses are thin.</summary>
        public static float BridgeChance(int month) => month >= 7 ? AugustBridge : EarlySummerBridge;

        /// <summary>Chance of going through, per crossing, at this point. Left of the lane it is zero however far you
        /// go — that is the rule the player has to learn. Where a stream runs on the surface there is no crevasse
        /// under it, and that is the hint the world gives him.</summary>
        public static float CrevasseChance(float ele, float signedOffRouteM, int month, bool streamNearby)
        {
            if (streamNearby) return 0f;
            if (ele < CrevasseFromEle || ele > CrevasseToEle) return 0f;
            if (signedOffRouteM <= LaneHalfWidthM) return 0f;                    // left of the lane, and the lane itself
            float into = Ascent.Clamp01((signedOffRouteM - LaneHalfWidthM) / CrevasseFieldM);
            float critical = ele >= CriticalFromEle && ele <= CriticalToEle ? 1.5f : 1f;
            return Math.Min(1f, BridgeChance(month) * into * critical);
        }

        /// <summary>The same chance spread over a metre of walking, for a tick loop: four dangerous crevasses over
        /// 1 800 m of lane.</summary>
        public static float CrevassePerMetre(float ele, float signedOffRouteM, int month, bool streamNearby)
            => CrevasseChance(ele, signedOffRouteM, month, streamNearby) * DangerousCrevasses / CrevasseBandM;

        // ── the snow-cat ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>The two places a snow-cat will put you down. The groomed lane really ends at 5 080 m — the
        /// "5 100" of the price list is the sign, not the radar, and <see cref="Elbrus.RatrakRoute"/> tops out at
        /// 5 087 m on our own height field.</summary>
        public static readonly float[] RatrakStops = { 4800f, 5100f };
        public const float RatrakTopEle = 5080f;
        public const float RatrakMinutes = 40f;
        /// <summary>Forty minutes of engine buy no acclimatisation whatever. This is the whole trade.</summary>
        public const bool RatrakGivesAcclimatisation = false;

        public const float RatrakTurnWindMs = 20f, RatrakTurnVisibilityM = 200f, RatrakFallbackEle = 4650f;

        /// <summary>Where the snow-cat actually stops, given the morning. In wind or cloud it turns round at the rocks.</summary>
        public static float RatrakDropEle(float asked, float windMs, float visibilityM)
            => windMs > RatrakTurnWindMs || visibilityM < RatrakTurnVisibilityM
                ? RatrakFallbackEle : Math.Min(asked, RatrakTopEle + 20f);

        /// <summary>Stepping out of a warm cab onto the windiest part of the route with a cold body: the cold works
        /// half again as hard for the first ten minutes.</summary>
        public const float ColdShockSeconds = 600f, ColdShockFactor = 1.4f;
        public static float ColdShock(float secondsSinceDrop)
            => secondsSinceDrop < ColdShockSeconds ? ColdShockFactor : 1f;

        // ── shelter ───────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Above this height the huts stop. There is exactly one roof higher up.</summary>
        public const float LastHutEle = 4200f;
        public const float SaddleHutEle = 5300f;
        public const int SaddleHutLying = 6, SaddleHutSitting = 12;
        /// <summary>The hut is built to hold 80 m/s.</summary>
        public const float SaddleHutWindMs = 80f;
        /// <summary>The fumarole beside it: a warm patch, a landmark in a white-out, and sulphur dioxide.</summary>
        public const float FumaroleM = 19f;
        /// <summary>The Soviet ruins on the saddle look like shelter and are not.</summary>
        public const bool RuinsShelter = false;

        /// <summary>Whether the hut is found from here. In a white-out you have to be almost on top of it, which is
        /// what the fumarole is for.</summary>
        public static bool HutFound(float metresFromHut, float visibilityM)
            => metresFromHut <= Math.Max(visibilityM, 6f);

        // ── the sun on the snow ───────────────────────────────────────────────────────────────────────────

        /// <summary>Without goggles after sunrise: stinging at an hour and a half, blind at two, two days to come back.</summary>
        public const float BlindHours = 2f, BlindHurtsAt = .75f, BlindRecoveryHours = 48f;
        /// <summary>Ultraviolet gains 4 % per 100 m of height, and the snow throws most of it back up at you.</summary>
        public const float UvPer100M = .04f, SnowAlbedo = 1.8f;

        public static float UvFactor(float ele) => (1f + UvPer100M * ele / 100f) * SnowAlbedo;

        /// <summary>Share of the blindness pool burnt per second.</summary>
        public static float BlindnessRate(float ele, bool goggles, bool daylight)
            => goggles || !daylight ? 0f : UvFactor(ele) / UvFactor(3800f) / (BlindHours * 3600f);

        // ── water ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Litres a day up here against the usual two and a half at home.</summary>
        public const float LitresPerDay = 4f, NormalLitresPerDay = 2.5f;
        /// <summary>Above this an ordinary bottle freezes; only a thermos still pours.</summary>
        public const float BottleFreezeEle = 4600f;
        /// <summary>What a one-litre thermos is worth once it is opened in the cold.</summary>
        public const int ThermosSips = 5;
        public const float SipSeconds = 20f;

        /// <summary>Share of the dehydration pool gathered per second: a day's worth of drying in a day of walking,
        /// faster in the thin dry air above the rocks.</summary>
        public static float DryingRate(float ele, bool moving)
        {
            float thin = 1f + .8f * Ascent.Clamp01((ele - 4000f) / 1600f);
            return (moving ? 1f : .45f) * thin / 86400f;
        }
    }
}
