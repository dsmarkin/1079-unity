using System;
using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    /// <summary>The board at the rescue hut. Two things are checked here above everything else: that the forecast and
    /// the day it forecasts come out of the same seed and the same date, so they cannot drift apart; and that the
    /// synoptic is honestly wrong about one day in five, because a board that was never wrong would not be a board.
    /// The heights, the lapse rate, the winter cold and the Beaufort line are the measured numbers of docs/ELBRUS.md.</summary>
    public class ForecastTests
    {
        const int Seed = 1079;
        static readonly DateTime July = new DateTime(2026, 7, 14);
        static readonly DateTime January = new DateTime(2026, 1, 14);

        static DayWeather Made(float baseC, float windMs, SkyState sky, float breakHour, bool winter = false)
            => new DayWeather(0, winter, baseC, windMs, sky, breakHour);

        // ── deterministic ─────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void TheSameSeedAndDateGiveTheSameDayEveryTime()
        {
            var a = Forecast.Day(Seed, July);
            var b = Forecast.Day(Seed, July);
            Assert.AreEqual(a.BaseTempC, b.BaseTempC, 0f);
            Assert.AreEqual(a.BaseWindMs, b.BaseWindMs, 0f);
            Assert.AreEqual(a.Morning, b.Morning);
            Assert.AreEqual(a.BreakHour, b.BreakHour, 0f);

            // the time of day the question is asked at changes nothing: the draw is by date
            Assert.AreEqual(a.BaseWindMs, Forecast.Day(Seed, July.AddHours(19)).BaseWindMs, 0f);

            // and the board is a function of (seed, date, when it was posted) and of nothing else
            var p = Forecast.Post(Seed, July.AddDays(1), July);
            var q = Forecast.Post(Seed, July.AddDays(1), July);
            Assert.AreEqual(p.Missed, q.Missed);
            Assert.AreEqual(p.Window, q.Window);
            Assert.AreEqual(p.Text, q.Text);

            // a different seed is a different mountain summer
            int differ = 0;
            for (int i = 0; i < 60; i++)
                if (Math.Abs(Forecast.Day(Seed, July.AddDays(i)).BaseWindMs - Forecast.Day(Seed + 1, July.AddDays(i)).BaseWindMs) > 1e-6f)
                    differ++;
            Assert.Greater(differ, 50, "другое зерно — другая погода");
        }

        [Test]
        public void AForecastThatDidNotMissIsExactlyTheDayThatComes()
        {
            int checkedDays = 0, missed = 0, missedAndDifferent = 0;
            for (int i = 0; i < 400; i++)
            {
                var date = July.AddDays(i + 1);
                var board = Forecast.Post(Seed, date, date.AddDays(-1));
                var truth = Forecast.Day(Seed, date);
                checkedDays++;
                if (!board.Missed)
                {
                    // this is the whole point of drawing both from the same seed: the board does not lie by accident
                    Assert.AreEqual(truth.BaseTempC, board.Says.BaseTempC, 0f, "прогноз не врёт, когда не промахнулся");
                    Assert.AreEqual(truth.BaseWindMs, board.Says.BaseWindMs, 0f);
                    Assert.AreEqual(truth.Morning, board.Says.Morning);
                    Assert.AreEqual(truth.BreakHour, board.Says.BreakHour, 0f);
                    Assert.AreEqual(Forecast.Window(truth), board.Window);
                }
                else
                {
                    missed++;
                    if (Math.Abs(truth.BaseWindMs - board.Says.BaseWindMs) > 1e-6f
                        || truth.Morning != board.Says.Morning) missedAndDifferent++;
                }
            }
            Assert.AreEqual(400, checkedDays);
            Assert.Greater(missed, 0, "синоптик иногда ошибается");
            Assert.Greater(missedAndDifferent, missed / 2, "промах — это чужой день, а не тот же самый");
        }

        [Test]
        public void TheSynopticIsWrongAboutOneDayInFiveAndOneInThreeTwoDaysOut()
        {
            const int days = 4000;
            int one = 0, two = 0, today = 0;
            for (int i = 0; i < days; i++)
            {
                var date = July.AddDays(i);
                if (Forecast.Missed(Seed, date, 0)) today++;
                if (Forecast.Missed(Seed, date, 1)) one++;
                if (Forecast.Missed(Seed, date, 2)) two++;
            }
            Assert.AreEqual(0, today, "сегодняшнюю погоду синоптик видит в окно");
            Assert.AreEqual(Forecast.MissAtOneDay, one / (float)days, .025f, "на сутки вперёд промах примерно в 20 % случаев");
            Assert.AreEqual(Forecast.MissAtTwoDays, two / (float)days, .025f, "на двое суток — примерно в 35 %");
            Assert.Greater(Forecast.MissChance(2), Forecast.MissChance(1), "чем дальше, тем хуже");
            Assert.LessOrEqual(Forecast.MissChance(9), Forecast.MissCeiling);

            // the one-day and the two-day posting for the same date are drawn apart, so the board can correct itself
            int corrected = 0;
            for (int i = 0; i < 200; i++)
            {
                var date = July.AddDays(i + 2);
                if (Forecast.Missed(Seed, date, 2) && !Forecast.Missed(Seed, date, 1)) corrected++;
            }
            Assert.Greater(corrected, 0, "вчерашний промах может исправиться сегодня");
        }

        // ── by height ─────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void TheBoardIsWrittenForFourHeights()
        {
            Assert.AreEqual(4, Forecast.Heights.Length);
            Assert.AreEqual(3800f, Forecast.Heights[0], 1e-6);
            Assert.AreEqual(4800f, Forecast.Heights[1], 1e-6);
            Assert.AreEqual(5400f, Forecast.Heights[2], 1e-6);
            Assert.AreEqual(5642f, Forecast.Heights[3], 1e-6, "вершина");

            var board = Forecast.Post(Seed, July, July);
            Assert.AreEqual(4, board.Lines.Length);
            for (int i = 0; i < 4; i++) Assert.AreEqual(Forecast.Heights[i], board.Lines[i].Ele, 1e-6);
            StringAssert.Contains("5642", board.Text);
            StringAssert.Contains("Сегодня", board.Text);
        }

        [Test]
        public void ASummerNightFollowsTheLapseRateAndAWinterOneDoesNot()
        {
            // summer: the plain lapse line off the meadow is the fair-weather case and it is right
            var summer = Made(AscentCold.SummerNightBaseC, 4f, SkyState.Clear, float.PositiveInfinity);
            float at4800 = Forecast.LineAt(summer, 4800f).TempC;
            Assert.LessOrEqual(at4800, -6f, "летняя ночь на 4800 — −6…−8");
            Assert.GreaterOrEqual(at4800, -8f);
            Assert.AreEqual(-11.8f, Forecast.LineAt(summer, 5642f).TempC, 1f, "на вершине летом около −10…−12");
            Assert.AreEqual(AscentCold.AirTempC(AscentCold.SummerNightBaseC, 4800f), at4800, 1e-3,
                "летом на доске ровно линия 0,6 °C на 100 м, без поправок");

            // winter: it is very much not right, and the board carries the measured difference
            var winter = Made(AscentCold.WinterBaseC, 8f, SkyState.Clear, float.PositiveInfinity, winter: true);
            float at5000 = Forecast.LineAt(winter, 5000f).TempC;
            Assert.LessOrEqual(at5000, -45f, "зимой на 5000 ночью −45…−53");
            Assert.GreaterOrEqual(at5000, -53f);
            Assert.Less(Forecast.LineAt(winter, 5642f).TempC, -55f, "вершина зимой — до −60");
            Assert.Less(at5000, AscentCold.AirTempC(AscentCold.WinterBaseC, 5000f), "прямая от поляны зиму недосчитывает");

            // and the felt temperature is the wind-chill the rest of the model uses
            var line = Forecast.LineAt(summer, 5642f);
            Assert.AreEqual(AscentCold.FeelsC(line.TempC, line.WindMs), line.FeelsC, 1e-4);
            Assert.Less(line.FeelsC, line.TempC, "ветер холоднее воздуха");
        }

        [Test]
        public void TheSaddleIsTwoToThreeTimesWindierThanFourThousand()
        {
            float gain = Forecast.WindGain(5400f) / Forecast.WindGain(4000f);
            Assert.GreaterOrEqual(gain, 2f, "на седловине ветер в 2–3 раза сильнее, чем на 4000");
            Assert.LessOrEqual(gain, 3f);
            Assert.Greater(Forecast.WindGain(5642f), Forecast.WindGain(4800f));
            Assert.Less(Forecast.WindGain(3800f), Forecast.WindGain(4800f));

            var d = Made(AscentCold.SummerNightBaseC, 6f, SkyState.Clear, float.PositiveInfinity);
            Assert.AreEqual(6f * Forecast.WindGain(5400f), Forecast.LineAt(d, 5400f).WindMs, 1e-4);

            // the Beaufort 7 line is the same one the mountain turns a party round at
            Assert.IsTrue(Forecast.LineAt(Made(0f, 2f, SkyState.Clear, 99f), 5400f).Walkable);
            Assert.IsFalse(Forecast.LineAt(Made(0f, 7f, SkyState.Clear, 99f), 5400f).Walkable, "7×2.5 = 17,5 м/с — это разворот");
            Assert.AreEqual(7, AscentCold.Beaufort(AscentCold.WalkHardMs + .1f));
        }

        // ── the window ────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void AWindowNeedsTheSkyTheWindAndTheClockAllThree()
        {
            var good = Made(AscentCold.SummerNightBaseC, 3f, SkyState.Clear, float.PositiveInfinity);
            Assert.IsTrue(Forecast.Window(good));
            StringAssert.Contains("Окно", Forecast.Verdict(good));

            Assert.IsFalse(Forecast.Window(Made(AscentCold.SummerNightBaseC, 3f, SkyState.Snow, float.PositiveInfinity)),
                "снег с утра — не окно");
            Assert.IsFalse(Forecast.Window(Made(AscentCold.SummerNightBaseC, 9f, SkyState.Clear, float.PositiveInfinity)),
                "на седловине 22 м/с — не окно");
            Assert.IsFalse(Forecast.Window(Made(AscentCold.SummerNightBaseC, 3f, SkyState.Clear, 11f)),
                "портится раньше контрольного времени — не окно");
            Assert.IsTrue(Forecast.Window(Made(AscentCold.SummerNightBaseC, 3f, SkyState.Clear, 13.5f)),
                "держит до разворота — окно");

            StringAssert.Contains("Окна нет", Forecast.Verdict(Made(AscentCold.SummerNightBaseC, 3f, SkyState.Clear, 11f)));
            StringAssert.Contains("седловине", Forecast.Verdict(Made(AscentCold.SummerNightBaseC, 9f, SkyState.Clear, 99f)));
        }

        [Test]
        public void TheDayBreaksAtTheRateTheRouteRulesAlreadyCharge()
        {
            const int days = 4000;
            int holdsToTurnaround = 0, holdsToCamp = 0, breaksBeforeNoon = 0;
            for (int i = 0; i < days; i++)
            {
                float h = Forecast.BreakHour(Seed, i);
                if (!float.IsPositiveInfinity(h) && h < 12f) breaksBeforeNoon++;
                if (h >= AscentRoute.TurnaroundHour) holdsToTurnaround++;
                if (h >= Rescue.BackHour) holdsToCamp++;
            }
            Assert.AreEqual(0, breaksBeforeNoon, "до полудня погода не портится");
            Assert.AreEqual(.92f, holdsToTurnaround / (float)days, .03f, "+8 %/час после полудня — к 13:00 держат 92 %");
            Assert.AreEqual(.54f, holdsToCamp / (float)days, .04f, "+20 %/час после 14:00 — к 16:00 остаётся чуть больше половины");

            // and the sky the runtime reads after the front is worse than the morning it started with
            var d = Made(AscentCold.SummerNightBaseC, 3f, SkyState.Clear, 14f);
            Assert.AreEqual(SkyState.Clear, d.SkyAt(13f));
            Assert.Greater((int)d.SkyAt(15f), (int)SkyState.Clear, "после фронта — хуже");
            Assert.IsTrue(d.Holds(13.9f));
            Assert.IsFalse(d.Holds(14.1f));

            // summer gives an hour and a half to get down, winter a quarter of an hour
            Assert.AreEqual(AscentRoute.StormOnsetSeconds(false), d.OnsetSeconds, 1e-6);
            Assert.Less(Made(0f, 3f, SkyState.Clear, 14f, winter: true).OnsetSeconds, d.OnsetSeconds);
        }

        [Test]
        public void HalfTheSummerDaysAreAWindowAndHardlyAnyWinterOneIs()
        {
            // forty days of every year, kept inside the season: July–August against January–February
            const int days = 3000;
            int summer = 0, winter = 0;
            for (int i = 0; i < days; i++)
            {
                if (Forecast.Window(Forecast.Day(Seed, new DateTime(1960 + i / 40, 7, 1).AddDays(i % 40)))) summer++;
                if (Forecast.Window(Forecast.Day(Seed, new DateTime(1960 + i / 40, 1, 5).AddDays(i % 40)))) winter++;
            }
            Assert.AreEqual(.5f, summer / (float)days, .07f, "летом окно примерно на половине дней");
            Assert.Less(winter / (float)days, .2f, "зимой — считанные дни");
            Assert.Greater(summer, winter * 3, "это не одна и та же гора");
        }

        [Test]
        public void TheWholeBoardHangsAsTodayAndTheNextTwoDays()
        {
            var all = Forecast.Board(Seed, July);
            Assert.AreEqual(3, all.Length);
            Assert.AreEqual(0, all[0].LeadDays);
            Assert.AreEqual(1, all[1].LeadDays);
            Assert.AreEqual(2, all[2].LeadDays);
            Assert.IsFalse(all[0].Missed, "сегодняшний день синоптик видит");
            Assert.AreEqual(July.Date, all[0].Date);

            string text = Forecast.BoardText(Seed, July);
            StringAssert.Contains("Завтра", text);
            StringAssert.Contains("Послезавтра", text);
            StringAssert.Contains("ЭВПСО", text);

            Assert.IsTrue(Forecast.IsWinter(January.Month));
            Assert.IsFalse(Forecast.IsWinter(July.Month));
            Assert.IsTrue(Forecast.Day(Seed, January).Winter);
        }
    }
}
