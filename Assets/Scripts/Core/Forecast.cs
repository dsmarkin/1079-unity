using System;

namespace Height1079.Core
{
    /// <summary>The weather of one day on the southern slope, drawn once from the seed and the date and never drawn
    /// again. The runtime builds the day it plays out of this, and the board at the rescue hut tries to predict the
    /// same thing out of the same numbers — which is the only way a forecast can be wrong on purpose rather than by
    /// accident.</summary>
    public readonly struct DayWeather
    {
        /// <summary>Day number the draw was made for (<see cref="Forecast.DayIndex"/>).</summary>
        public readonly int Day;
        /// <summary>Which half of the year this is (<see cref="Forecast.IsWinter"/>). It changes everything: the
        /// base temperature, the wind, and how long a party has between the first sign and the пурга.</summary>
        public readonly bool Winter;
        /// <summary>Air at the Azau meadow at the coldest hour, °C — the base <see cref="AscentCold.AirTempC"/>
        /// works its lapse rate down from.</summary>
        public readonly float BaseTempC;
        /// <summary>Free-air wind at 4 000 m, m/s. Every height on the board is quoted against this one, because
        /// "на седловине в два-три раза сильнее, чем на 4000" is the figure the mountain is described by.</summary>
        public readonly float BaseWindMs;
        /// <summary>The sky the morning starts with.</summary>
        public readonly SkyState Morning;
        /// <summary>The hour the weather turns, or +∞ when the day holds
        /// (<see cref="AscentRoute.WeatherRiskPerHour"/>).</summary>
        public readonly float BreakHour;

        public DayWeather(int day, bool winter, float baseTempC, float baseWindMs, SkyState morning, float breakHour)
        { Day = day; Winter = winter; BaseTempC = baseTempC; BaseWindMs = baseWindMs; Morning = morning; BreakHour = breakHour; }

        /// <summary>The sky at this hour: the morning until it breaks, and <see cref="Forecast.BreakSteps"/> worse
        /// after.</summary>
        public SkyState SkyAt(float hour)
            => hour < BreakHour ? Morning
             : (SkyState)Math.Min((int)SkyState.WhiteOut, (int)Morning + Forecast.BreakSteps);

        /// <summary>Does the day hold to this hour.</summary>
        public bool Holds(float hour) => hour < BreakHour;

        /// <summary>Seconds from the first sign of the front to a full пурга, here and now
        /// (<see cref="AscentRoute.StormOnsetSeconds"/>). In winter it is a quarter of an hour.</summary>
        public float OnsetSeconds => AscentRoute.StormOnsetSeconds(Winter);
    }

    /// <summary>One line of the board: a height and what the day holds there.</summary>
    public readonly struct ForecastLine
    {
        public readonly float Ele;
        /// <summary>Air temperature at this height at the coldest hour, °C.</summary>
        public readonly float TempC;
        /// <summary>Free-air wind, m/s. The funnel of the косая полка and the saddle is <em>not</em> in this number:
        /// it is a fact about the ground, not about the day, and <see cref="AscentCold.WindAt"/> adds it where a
        /// climber is actually standing. The board reports what the synoptic reports.</summary>
        public readonly float WindMs;
        /// <summary>Wind-chill equivalent, °C (<see cref="AscentCold.FeelsC"/>).</summary>
        public readonly float FeelsC;
        public readonly SkyState Sky;
        public readonly float VisibilityM;

        public ForecastLine(float ele, float tempC, float windMs, SkyState sky)
        {
            Ele = ele; TempC = tempC; WindMs = windMs; Sky = sky;
            FeelsC = AscentCold.FeelsC(tempC, windMs);
            VisibilityM = AscentRoute.VisibilityM(sky);
        }

        public int Beaufort => AscentCold.Beaufort(WindMs);
        /// <summary>Below Beaufort 7 a party walks; at 7 and over it goes down (<see cref="AscentCold.TurnBackWind"/>).</summary>
        public bool Walkable => !AscentCold.TurnBackWind(WindMs);

        /// <summary>«5400 м: −14 °C, ветер 12 м/с (6 б.), ощущается −33 °C, ясно» — one chalked line.</summary>
        public string Line
            => $"{Ele:0} м: {TempC:0} °C, ветер {WindMs:0.#} м/с ({Beaufort} б.), ощущается {FeelsC:0} °C, {AscentRoute.SkyTitle(Sky).ToLowerInvariant()}";
    }

    /// <summary>The board at the rescue hut for one day: the four heights, and the one thing a party actually comes
    /// to read — is there a window or not.</summary>
    public sealed class WeatherBoard
    {
        /// <summary>The day this line of the board is about.</summary>
        public readonly DateTime Date;
        /// <summary>How far ahead it was written. 0 is today, 1 tomorrow.</summary>
        public readonly int LeadDays;
        /// <summary>Whether the synoptic got this one wrong. <b>The HUD must never show this</b> — it is here for the
        /// protocol after the fact and for the tests. A board that told you its own reliability would not be a
        /// forecast.</summary>
        public readonly bool Missed;
        /// <summary>The day the board claims. Equal to <see cref="Forecast.Day"/> for this date unless
        /// <see cref="Missed"/>, in which case it is a perfectly good day that belongs to nobody.</summary>
        public readonly DayWeather Says;
        public readonly ForecastLine[] Lines;
        /// <summary>Is there a window: a readable sky, wind on the saddle under Beaufort 7, and weather that holds to
        /// the turn-round time.</summary>
        public readonly bool Window;
        /// <summary>Russian, the line under the table.</summary>
        public readonly string Verdict;

        public WeatherBoard(DateTime date, int leadDays, bool missed, DayWeather says, ForecastLine[] lines,
            bool window, string verdict)
        { Date = date; LeadDays = leadDays; Missed = missed; Says = says; Lines = lines; Window = window; Verdict = verdict; }

        /// <summary>The whole board as it is chalked up.</summary>
        public string Text
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(Forecast.DayTitle(Date, LeadDays));
                foreach (var l in Lines) { sb.Append('\n'); sb.Append(l.Line); }
                sb.Append('\n');
                sb.Append(Verdict);
                return sb.ToString();
            }
        }
    }

    /// <summary>The board at the МЧС hut at the bottom: the next day or two, by height, and whether there is a window.
    ///
    /// Two rules shape the whole file.
    ///
    /// <b>It is deterministic in the seed and the date.</b> <see cref="Day"/> is the weather the mountain actually
    /// has; the runtime builds its day out of it. <see cref="Post"/> is what the board says about that same day. Both
    /// are pure functions of (seed, date), so a forecast and the day it forecasts cannot drift apart by accident, and
    /// a save that comes back on Thursday sees the Thursday it was promised.
    ///
    /// <b>And it is wrong sometimes.</b> A synoptic is not a god, and a mountain forecast is a poor one: about four
    /// days in five come out right at a day's notice and about two in three at two days'. A missed forecast here is
    /// not noise — it is a perfectly plausible day drawn from a shifted seed, so the board never prints nonsense, it
    /// just prints somebody else's weather. Which is exactly what it feels like.
    ///
    /// The numbers under it, all of them from docs/ELBRUS.md:
    /// <list type="bullet">
    /// <item>Lapse rate 0.6 °C per 100 m (<see cref="AscentCold.AirTempC"/>): a summer night base of +8 °C puts
    /// 4 800 m at −6.7 °C and the summit at −11.8 °C, which is the measured −6…−8 and −10.</item>
    /// <item>A winter night at 5 000 m is −45…−53 °C and the summit goes to −60 — far colder than any straight lapse
    /// line off the meadow. <see cref="WinterNightExtra"/> is the difference, and it exists because the measurement
    /// says so.</item>
    /// <item>Wind on the saddle is two to three times what it is at 4 000 m.</item>
    /// <item>Beaufort 7 (13.9–17.1 m/s) is where a party turns round (<see cref="AscentCold.WalkHardMs"/>).</item>
    /// <item>The weather breaks after noon at 8 % an hour and after 14:00 at 20 %
    /// (<see cref="AscentRoute.WeatherRiskPerHour"/>), and from the first sign to a full пурга is an hour and a half
    /// to two in summer and a quarter of an hour in winter.</item>
    /// </list></summary>
    public static class Forecast
    {
        /// <summary>The four heights the board is written for: the barrels, the rocks, the saddle, the summit.</summary>
        public static readonly float[] Heights = { 3800f, 4800f, 5400f, 5642f };

        /// <summary>Winter is November to March: the half of the year the slope is not walked in, and the half the
        /// −45…−53 °C belongs to.</summary>
        public static bool IsWinter(int month) => month >= 11 || month <= 3;

        /// <summary>Days since the epoch the draws are indexed by. Any fixed epoch would do; this one keeps the
        /// numbers small and positive for the dates the game is set in.</summary>
        static readonly DateTime Epoch = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static int DayIndex(DateTime date) => (int)Math.Floor((date.Date - Epoch.Date).TotalDays);

        /// <summary>The date a day index stands for — the inverse of <see cref="DayIndex"/>. A runtime that wants the
        /// host and every client to draw the same day publishes the seed and one integer and calls this.</summary>
        public static DateTime DateOf(int day) => Epoch.Date.AddDays(day);

        // ── the draw ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>A 32-bit avalanche of (seed, day, salt). Deterministic, engine-free, and the same on every
        /// machine — which is what lets the host and the board agree without exchanging a byte.</summary>
        static uint Hash(int seed, int day, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                h ^= (uint)day * 2246822519u;
                h = (h ^ (h >> 15)) * 2246822519u;
                h ^= (uint)salt * 3266489917u;
                h = (h ^ (h >> 13)) * 3266489917u;
                return h ^ (h >> 16);
            }
        }

        /// <summary>The same, as a number in [0, 1).</summary>
        public static float Unit(int seed, int day, int salt) => (Hash(seed, day, salt) & 0xffffff) / 16777216f;

        // ── what the day is actually like ─────────────────────────────────────────────────────────────────

        /// <summary>Spread of the base temperature about the seasonal mean, °C. Balance: a summer day at the meadow
        /// runs +3…+13 and a winter one −20…−4.</summary>
        public const float SummerSpreadC = 5f, WinterSpreadC = 8f;

        // wind at 4 000 m by percentile: a median summer day is a light breeze and one day in twenty is a gale.
        // Balance, laid out so that the saddle (×2.5) crosses Beaufort 7 on about two summer days in five.
        static readonly (float at, float value)[] summerWind =
            { (0f, 1f), (.5f, 4f), (.8f, 8f), (.95f, 15f), (1f, 25f) };
        static readonly (float at, float value)[] winterWind =
            { (0f, 3f), (.5f, 11f), (.8f, 20f), (.95f, 32f), (1f, 45f) };

        /// <summary>How much of the wind at 4 000 m each height gets. The saddle at 2.5 is the middle of the
        /// measured "two to three times"; the rest of the curve is the shape that joins it up.</summary>
        static readonly (float at, float value)[] windGain =
            { (2350f, .55f), (3800f, .95f), (4000f, 1f), (4800f, 1.4f), (5400f, 2.5f), (5642f, 2.8f) };

        /// <summary>How much colder a winter night is than the lapse line off the meadow predicts. Anchored on two
        /// measurements: 5 000 m at −45…−53 °C and the summit near −60 °C. Zero in summer, where the lapse line is
        /// the fair-weather case and comes out right on its own.</summary>
        static readonly (float at, float value)[] winterNightExtra =
            { (3800f, 0f), (4300f, -6f), (5000f, -21f), (5642f, -28f) };

        // the morning sky by percentile, summer and winter
        static readonly (float at, SkyState sky)[] summerSky =
            { (.62f, SkyState.Clear), (.87f, SkyState.Cloud), (.97f, SkyState.Snow), (1f, SkyState.Blizzard) };
        static readonly (float at, SkyState sky)[] winterSky =
            { (.34f, SkyState.Clear), (.62f, SkyState.Cloud), (.86f, SkyState.Snow), (.97f, SkyState.Blizzard), (1f, SkyState.WhiteOut) };

        /// <summary>How many steps down the sky goes when the front arrives: clear turns to snow, cloud to пурга.</summary>
        public const int BreakSteps = 2;

        /// <summary>The hour the board stops caring. Nothing is walked on this route after eight in the evening.</summary>
        public const float LastHour = 20f;

        public static float WindGain(float ele) => Ascent.Curve(windGain, ele);
        public static float WinterNightExtra(float ele) => Ascent.Curve(winterNightExtra, ele);

        /// <summary>The weather this seed gives this date. Call it with the day the run plays on and build the run
        /// out of the answer; call it with tomorrow and you have what the board is trying to guess.</summary>
        public static DayWeather Day(int seed, DateTime date)
        {
            int day = DayIndex(date);
            bool winter = IsWinter(date.Month);

            float baseC = (winter ? AscentCold.WinterBaseC : AscentCold.SummerNightBaseC)
                        + (Unit(seed, day, 1) * 2f - 1f) * (winter ? WinterSpreadC : SummerSpreadC);
            float wind = Ascent.Curve(winter ? winterWind : summerWind, Unit(seed, day, 2));
            var sky = Pick(winter ? winterSky : summerSky, Unit(seed, day, 3));
            float breaks = BreakHour(seed, day);

            return new DayWeather(day, winter, baseC, wind, sky, breaks);
        }

        static SkyState Pick((float at, SkyState sky)[] table, float u)
        {
            foreach (var row in table) if (u < row.at) return row.sky;
            return table[table.Length - 1].sky;
        }

        /// <summary>The hour the weather turns, drawn from <see cref="AscentRoute.WeatherRiskPerHour"/> itself — the
        /// same 8 % and 20 % an hour the route rules charge, spent once for the whole day instead of rolled every
        /// hour. About half the days hold to 16:00, which is what the southern side gives you.</summary>
        public static float BreakHour(int seed, int day)
        {
            float u = Unit(seed, day, 5), alive = 1f;
            for (float h = 12f; h < LastHour; h += 1f)
            {
                float mass = alive * AscentRoute.WeatherRiskPerHour(h);
                if (mass <= 0f) continue;
                if (u < mass) return h + u / mass;
                u -= mass;
                alive -= mass;
            }
            return float.PositiveInfinity;
        }

        /// <summary>One line of a day, at one height.</summary>
        public static ForecastLine LineAt(in DayWeather d, float ele)
            => new ForecastLine(ele,
                AscentCold.AirTempC(d.BaseTempC, ele) + (d.Winter ? WinterNightExtra(ele) : 0f),
                d.BaseWindMs * WindGain(ele),
                d.Morning);

        // ── is there a window ─────────────────────────────────────────────────────────────────────────────

        /// <summary>The height the window is decided at: the saddle, because that is where the funnel is and where a
        /// party turns round.</summary>
        public const float WindowEle = 5400f;

        /// <summary>A window is three things at once: you can see, you can stand up, and it lasts until the
        /// turn-round time. Miss any one of them and the day is not a summit day.</summary>
        public static bool Window(in DayWeather d, float byHour = AscentRoute.TurnaroundHour)
            => d.Morning <= SkyState.Cloud
            && !AscentCold.TurnBackWind(d.BaseWindMs * WindGain(WindowEle))
            && d.BreakHour >= byHour;

        /// <summary>Why, in Russian.</summary>
        public static string Verdict(in DayWeather d, float byHour = AscentRoute.TurnaroundHour)
        {
            if (d.Morning > SkyState.Cloud) return $"Окна нет: {AscentRoute.SkyTitle(d.Morning).ToLowerInvariant()} с утра.";
            float saddle = d.BaseWindMs * WindGain(WindowEle);
            if (AscentCold.TurnBackWind(saddle))
                return $"Окна нет: на седловине {saddle:0} м/с — {AscentCold.Beaufort(saddle)} баллов, это разворот.";
            if (d.BreakHour < byHour)
                return $"Окна нет: портится к {AscentRoute.Clock(d.BreakHour)}, раньше контрольного времени.";
            return float.IsPositiveInfinity(d.BreakHour)
                ? $"Окно: держит весь день, на седловине {saddle:0} м/с."
                : $"Окно до {AscentRoute.Clock(d.BreakHour)}: на седловине {saddle:0} м/с.";
        }

        // ── and how often it is wrong ─────────────────────────────────────────────────────────────────────

        /// <summary>Share of forecasts that come out wrong, by how far ahead they were written. A day ahead a
        /// mountain forecast is right about four times in five, two days ahead about two times in three, and past
        /// that it stops being a forecast. Balance, but calibrated on that.</summary>
        public const float MissAtOneDay = .20f, MissAtTwoDays = .35f, MissPerExtraDay = .08f, MissCeiling = .5f;

        public static float MissChance(int leadDays)
        {
            if (leadDays <= 0) return 0f;                       // today, out of the window, is not a forecast
            if (leadDays == 1) return MissAtOneDay;
            return Math.Min(MissCeiling, MissAtTwoDays + MissPerExtraDay * (leadDays - 2));
        }

        /// <summary>Whether this particular posting is one of the wrong ones. Deterministic, like everything else:
        /// the same board is wrong every time it is read, and a one-day and a two-day forecast for the same date are
        /// drawn separately, so the board can correct itself as the day comes closer.</summary>
        public static bool Missed(int seed, DateTime date, int leadDays)
            => Unit(seed, DayIndex(date), 6 + leadDays) < MissChance(leadDays);

        /// <summary>A missed forecast is somebody else's day, not nonsense: the same date re-drawn from a shifted
        /// seed. Everything on the board stays plausible and everything stays reproducible.</summary>
        const int MissSeedShift = 0x5bf03635;
        static int MissSeed(int seed, int leadDays) => unchecked(seed + MissSeedShift * (leadDays + 1));

        // ── the board ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The board for one date as it is chalked up on <paramref name="postedOn"/>.</summary>
        public static WeatherBoard Post(int seed, DateTime date, DateTime postedOn)
        {
            int lead = Math.Max(0, DayIndex(date) - DayIndex(postedOn));
            bool missed = Missed(seed, date, lead);
            var says = missed ? Day(MissSeed(seed, lead), date) : Day(seed, date);

            var lines = new ForecastLine[Heights.Length];
            for (int i = 0; i < Heights.Length; i++) lines[i] = LineAt(says, Heights[i]);

            return new WeatherBoard(date.Date, lead, missed, says, lines, Window(says), Verdict(says));
        }

        /// <summary>The whole board as it hangs: today and the next day or two.</summary>
        public static WeatherBoard[] Board(int seed, DateTime postedOn, int days = 2)
        {
            days = Math.Max(1, days);
            var all = new WeatherBoard[days + 1];
            for (int i = 0; i <= days; i++) all[i] = Post(seed, postedOn.Date.AddDays(i), postedOn);
            return all;
        }

        public static string DayTitle(DateTime date, int leadDays) => leadDays switch
        {
            0 => "Сегодня",
            1 => "Завтра",
            2 => "Послезавтра",
            _ => date.ToString("dd.MM", System.Globalization.CultureInfo.InvariantCulture),
        };

        /// <summary>The whole board as text, for the prop and for the protocol.</summary>
        public static string BoardText(int seed, DateTime postedOn, int days = 2)
        {
            var sb = new System.Text.StringBuilder("Прогноз · ЭВПСО МЧС");
            foreach (var b in Board(seed, postedOn, days)) { sb.Append("\n\n"); sb.Append(b.Text); }
            return sb.ToString();
        }
    }
}
