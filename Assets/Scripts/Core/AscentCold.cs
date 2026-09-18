using System;

namespace Height1079.Core
{
    /// <summary>The three parts of a climber that freeze on their own clock and never share heat with each other.</summary>
    public enum Limb : byte
    {
        /// <summary>Hands. They go the moment the mittens come off for a carabiner, and carabiners cannot be worked
        /// in thick mittens.</summary>
        Hands,
        /// <summary>Feet. They freeze at the stops, not in the walking.</summary>
        Feet,
        /// <summary>Face. A balaclava is the whole of the defence; without one an index below −40 takes 5–10 minutes.</summary>
        Face,
    }

    /// <summary>The air at the climber, as the runtime measures it for this tick.</summary>
    public readonly struct MountainAir
    {
        /// <summary>Air temperature at this height, °C (<see cref="AscentCold.AirTempC"/>).</summary>
        public readonly float TempC;
        /// <summary>Wind at the climber, m/s — after <see cref="AscentCold.WindAt"/> has run the funnel.</summary>
        public readonly float WindMs;
        /// <summary>How far anything can be seen, metres (<see cref="AscentRoute.VisibilityM"/>).</summary>
        public readonly float VisibilityM;
        /// <summary>Centimetres of snow fallen since the wands were last read.</summary>
        public readonly float FreshSnowCm;

        public MountainAir(float tempC, float windMs, float visibilityM = 5000f, float freshSnowCm = 0f)
        { TempC = tempC; WindMs = windMs; VisibilityM = visibilityM; FreshSnowCm = freshSnowCm; }
    }

    /// <summary>Cold and wind on the southern side, and what they do to a body.
    ///
    /// Anchors:
    /// <list type="bullet">
    /// <item>Lapse rate 0.6 °C per 100 m from the Azau meadow at 2 350 m. A summer night base of +8 °C puts the
    /// summit at −12 °C, a winter base of −12 °C puts it at −32 °C.</item>
    /// <item>Measured: 4 800 m on a summer night −6…−8 °C; the summit in summer about −10 °C, to −20 °C in wind;
    /// 5 000 m on a winter night −45…−53 °C, the summit to −60 °C. The lapse line is the fair-weather case — a
    /// winter night on the saddle is colder than any straight line off the meadow predicts.</item>
    /// <item>Time to frostbite of bare skin, by the felt index: −28…−40 °C → 10–30 min; −40…−48 → 5–10;
    /// −48…−55 → 2–5; below −55 → under two minutes.</item>
    /// <item>The saddle is a funnel: 67 m/s measured, 70–80 by calculation. Beaufort 7 (13.9–17.1 m/s) is where
    /// walking into the wind becomes hard and a party turns round; 8 (17.2–20.7) is where walking is barely possible.</item>
    /// </list></summary>
    public static class AscentCold
    {
        // ── temperature ───────────────────────────────────────────────────────────────────────────────────

        public const float LapseFromEle = 2350f, LapsePer100M = .6f;
        /// <summary>Base at the meadow: a summer night, and a winter day.</summary>
        public const float SummerNightBaseC = 8f, WinterBaseC = -12f;

        public static float AirTempC(float baseC, float ele) => baseC - LapsePer100M * (ele - LapseFromEle) / 100f;

        /// <summary>Below this the Siple–Passel bracket is worth less than its own divisor and the formula starts
        /// returning temperatures warmer than the air; below it the felt temperature is simply the air.</summary>
        public const float CalmMs = 1.788f;

        /// <summary>Ветро-холодовой индекс: the Siple–Passel wind-chill equivalent temperature, °C. This is the old
        /// index Russian and Soviet mountaineering tables are built on, and it is deliberately harsher than the
        /// modern JAG/TI formula the weather services use — −20 °C at 7.5 m/s comes out near −40 here and only −32
        /// there, and the frostbite table above is calibrated against the harsh scale.</summary>
        public static float FeelsC(float tempC, float windMs)
        {
            if (windMs <= CalmMs) return tempC;
            double wci = (10 * Math.Sqrt(windMs) - windMs + 10.45) * (33 - tempC);
            return (float)(33 - wci / 22.034);
        }

        // ── frostbite ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Above this index bare skin is not at risk at all.</summary>
        public const float SafeIndexC = -28f;

        static readonly (float at, float value)[] freeze =
            { (-62f, 1f), (-55f, 2f), (-48f, 5f), (-40f, 10f), (-28f, 30f) };

        /// <summary>Minutes to frostbite of exposed skin at this felt temperature, or +∞ when there is no risk.</summary>
        public static float FrostbiteMinutes(float feelsC)
            => feelsC > SafeIndexC ? float.PositiveInfinity : Ascent.Curve(freeze, feelsC);

        /// <summary>A balaclava, and mittens that stay on, buy about eight times the exposure. Bare fingers on steel
        /// go faster than the table's bare skin. Feet freeze standing still and hardly at all while walking.</summary>
        public const float CoveredFace = .12f, CoveredHands = .10f, WorkingHands = 1.15f,
                           WalkingFeet = .15f, StandingFeet = .8f;

        /// <summary>Share of one limb's pool burnt per second. <paramref name="covered"/> means a balaclava on the
        /// face or mittens still on the hands; for the feet it is always true and <paramref name="moving"/> decides.</summary>
        public static float FreezeRate(Limb limb, float feelsC, bool covered, bool moving)
        {
            float minutes = FrostbiteMinutes(feelsC);
            if (float.IsPositiveInfinity(minutes)) return 0f;
            float rate = 1f / (minutes * 60f);
            switch (limb)
            {
                case Limb.Face: return rate * (covered ? CoveredFace : 1f);
                case Limb.Hands: return rate * (covered ? CoveredHands : WorkingHands);
                default: return rate * (moving ? WalkingFeet : StandingFeet);
            }
        }

        public static string LimbTitle(Limb l) => l switch
        {
            Limb.Hands => "Руки",
            Limb.Feet => "Ноги",
            _ => "Лицо",
        };

        /// <summary>The HUD line for a pool that is burning, or "".</summary>
        public static string FrostbiteWarning(Limb l, float pool)
            => pool < .35f ? "" : pool < .75f ? LimbTitle(l) + ": не чувствуются" : LimbTitle(l) + ": обморожение";

        // ── the wind funnel ───────────────────────────────────────────────────────────────────────────────

        /// <summary>The косая полка and the saddle stand in a funnel between the two summits. Everything the location
        /// weather says is multiplied and added to here, and nowhere else on the route.</summary>
        public const float ShelfGain = 1.8f, ShelfAdd = 8f, SaddleGain = 2.2f, SaddleAdd = 12f;
        /// <summary>How far from the saddle the funnel still holds, metres.</summary>
        public const float SaddleReachM = 250f;
        /// <summary>Measured on the saddle; the calculated ceiling is 70–80.</summary>
        public const float MeasuredSaddleMaxMs = 67f;

        /// <summary>Wind at the climber. <paramref name="metresFromSaddle"/> is the plain distance to
        /// <see cref="Elbrus.Saddle"/>; the saddle term fades out over <see cref="SaddleReachM"/> so there is no step
        /// in the weather as you walk onto it.</summary>
        public static float WindAt(float baseWindMs, float ele, float metresFromSaddle)
        {
            float w = Math.Max(0f, baseWindMs);
            if (ele >= Ascent.ShelfFromEle && ele <= Ascent.ShelfToEle) w = Math.Max(w, baseWindMs * ShelfGain + ShelfAdd);
            float near = Ascent.Clamp01(1f - metresFromSaddle / SaddleReachM);
            if (near > 0f) w = Math.Max(w, baseWindMs * (1f + (SaddleGain - 1f) * near) + SaddleAdd * near);
            return w;
        }

        /// <summary>A side wind this strong pushes you off the track; this strong makes you stagger.</summary>
        public const float DriftFromMs = 15f, StaggerFromMs = 25f;
        public const float DriftMs = .3f, GustSlip = .015f;

        /// <summary>Metres per second the wind moves the climber sideways — toward the fall line, on the shelf.</summary>
        public static float SideDriftMs(float windMs)
        {
            if (windMs <= DriftFromMs) return 0f;
            return DriftMs * (1f + Ascent.Clamp01((windMs - StaggerFromMs) / 15f));
        }

        /// <summary>Chance that one gust knocks the feet out, above the staggering threshold.</summary>
        public static float GustSlipChance(float windMs) => windMs <= StaggerFromMs ? 0f : GustSlip;

        // ── Beaufort ──────────────────────────────────────────────────────────────────────────────────────

        static readonly float[] beaufort = { .3f, 1.6f, 3.4f, 5.5f, 8f, 10.8f, 13.9f, 17.2f, 20.8f, 24.5f, 28.5f, 32.7f };

        public static int Beaufort(float ms)
        {
            for (int i = 0; i < beaufort.Length; i++) if (ms < beaufort[i]) return i;
            return 12;
        }

        /// <summary>Beaufort 7 — «идти против ветра тяжело». At 7 and over a party on this route goes down.</summary>
        public const float WalkHardMs = 13.9f;
        /// <summary>Beaufort 8 — «очень тяжело ходить».</summary>
        public const float WalkVeryHardMs = 17.2f;
        public static bool TurnBackWind(float ms) => ms >= WalkHardMs;

        public static string WindTitle(float ms)
            => ms >= StaggerFromMs ? "Шатает, сбивает с ног"
             : ms >= WalkVeryHardMs ? "Очень тяжело идти"
             : ms >= WalkHardMs ? "Идти против ветра тяжело"
             : ms >= beaufort[5] ? "Резкие порывы" : "";
    }
}
