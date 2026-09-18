using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    /// <summary>Registering with the МЧС отряд, and what happens when a party does not come back. The numbers pinned
    /// down here are the ones the southern side runs on — no network above 5 250 m, three to four hours on foot from
    /// Pastukhov rocks to the summit, twenty minutes off the saddle by air and about a day without it — and the one
    /// rule the whole counter exists for: nobody looks for a party that never signed the slip. Everything is
    /// engine-free and runs in <c>dotnet</c>.</summary>
    public class RescueTests
    {
        static Registration Party => Rescue.File("Дон", 2);
        static RescueAsk Fine(float ele) => new RescueAsk(ele, 5f, AscentRoute.VisibilityM(SkyState.Clear), true);
        static RescueAsk Foul(float ele) => new RescueAsk(ele, 5f, AscentRoute.VisibilityM(SkyState.Cloud), true);

        // ── the slip ──────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void NobodyLooksForAPartyThatNeverSignedTheSlip()
        {
            Assert.IsFalse(Rescue.Watched(Rescue.NotFiled), "не зарегистрировался — никто не ищет");
            Assert.IsTrue(float.IsPositiveInfinity(Rescue.SearchStartsHour(Rescue.NotFiled)));
            Assert.IsFalse(Rescue.SearchStarted(Rescue.NotFiled, 23.9f), "никогда, сколько ни жди");
            Assert.AreEqual(0f, Rescue.OverdueHours(Rescue.NotFiled, 22f), 1e-6);

            // and no operation is planned, however good the weather and however low the casualty is
            var nothing = Rescue.Plan(Rescue.NotFiled, Rescue.NoSos, Fine(4000f));
            Assert.AreEqual(RescueKind.None, nothing.Kind);
            Assert.AreEqual(Alarm.None, nothing.How);
            Assert.IsFalse(nothing.Coming);
            Assert.IsTrue(float.IsPositiveInfinity(nothing.AlarmHour));
            Assert.IsTrue(float.IsPositiveInfinity(nothing.ReachHour));
            StringAssert.Contains("не зарегистрирована", nothing.Note);

            // the same party with a slip in the drawer is looked for
            Assert.IsTrue(Rescue.Watched(Party));
            Assert.IsTrue(Rescue.Plan(Party, Rescue.NoSos, Fine(4000f)).Coming);
        }

        [Test]
        public void TheControlTimeIsThirteenHundredAndTheCampAtFour()
        {
            Assert.AreEqual(AscentRoute.TurnaroundHour, Rescue.ControlHour, 1e-6, "контрольное время — разворот в 13:00");
            Assert.AreEqual(13f, Rescue.ControlHour, 1e-6);
            Assert.AreEqual(16f, Rescue.BackHour, 1e-6, "возвращение в лагерь к 16:00");

            var r = Party;
            Assert.AreEqual(13f, r.ControlHour, 1e-6);
            Assert.AreEqual(16f, r.BackHour, 1e-6);
            Assert.AreEqual(2, r.People);
            StringAssert.Contains("13:00", r.Line);
            StringAssert.Contains("16:00", r.Line);

            Assert.IsFalse(Rescue.PastControl(r, 12.9f));
            Assert.IsTrue(Rescue.PastControl(r, 13f));
            Assert.AreEqual(2f, Rescue.HoursToControl(r, 11f), 1e-6);

            Assert.IsFalse(Rescue.Overdue(r, 15.9f));
            Assert.IsTrue(Rescue.Overdue(r, 16f), "к 16:00 не вернулся — просрочка");
            Assert.AreEqual(1.5f, Rescue.OverdueHours(r, 17.5f), 1e-6);

            // and the search is not instant either: an hour of grace for a party that is only slow
            Assert.AreEqual(r.BackHour + Rescue.GraceHours, Rescue.SearchStartsHour(r), 1e-6);
            Assert.IsFalse(Rescue.SearchStarted(r, 16.5f), "опоздал на полчаса — ещё не ЧП");
            Assert.IsTrue(Rescue.SearchStarted(r, 17f));
        }

        // ── getting word out ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void AboveFiveTwoFiftyTheSosDoesNotGoOutAtAll()
        {
            Assert.AreEqual(AscentRoute.RadioCeiling, Rescue.SignalCeiling, 1e-6);
            Assert.AreEqual(5250f, Rescue.SignalCeiling, 1e-6, "связь пропадает выше 5250 м");

            Assert.IsTrue(Rescue.CanCall(5249f));
            Assert.IsFalse(Rescue.CanCall(5250f));
            Assert.IsFalse(Rescue.CanCall(Elbrus.Saddle.Ele), "с седловины не позвонить");
            Assert.IsFalse(Rescue.CanCall(Elbrus.WestSummit.Ele), "с вершины тем более");

            var fromSummit = Rescue.SendSos(Elbrus.WestSummit.Ele, 12f);
            Assert.IsFalse(fromSummit.Sent, "SOS отсюда не уходит");
            StringAssert.Contains("5250", fromSummit.Why);

            // the only answer is to lose height, and the rule says exactly how much
            float drop = Rescue.DescendForSignalM(Elbrus.WestSummit.Ele);
            Assert.Greater(drop, 380f, "с 5642 надо сбросить почти четыреста метров");
            Assert.IsTrue(Rescue.CanCall(Elbrus.WestSummit.Ele - drop), "сбросил — связь появилась");
            Assert.AreEqual(0f, Rescue.DescendForSignalM(4800f), 1e-6, "ниже потолка сбрасывать нечего");

            var fromRocks = Rescue.SendSos(4800f, 12f);
            Assert.IsTrue(fromRocks.Sent);
            Assert.AreEqual("", fromRocks.Why);
            Assert.AreEqual(12f, fromRocks.Hour, 1e-6);

            // an SOS that never went out leaves an unregistered party exactly where it was
            Assert.IsFalse(Rescue.Plan(Rescue.NotFiled, fromSummit, Fine(5642f)).Coming);
        }

        [Test]
        public void TheEarlierOfTheCallAndTheControlTimeIsWhatStartsIt()
        {
            var r = Party;

            Rescue.AlarmHour(r, Rescue.SendSos(4800f, 11f), out var how);
            Assert.AreEqual(Alarm.Sos, how);
            Assert.AreEqual(11f, Rescue.AlarmHour(r, Rescue.SendSos(4800f, 11f), out _), 1e-6);

            // a call that could not be made leaves only the clock
            float onlyClock = Rescue.AlarmHour(r, Rescue.SendSos(5500f, 11f), out var byClock);
            Assert.AreEqual(Alarm.Overdue, byClock);
            Assert.AreEqual(Rescue.SearchStartsHour(r), onlyClock, 1e-6);

            // and a call made after the search had already started does not move it earlier
            Assert.AreEqual(Rescue.SearchStartsHour(r), Rescue.AlarmHour(r, Rescue.SendSos(4800f, 19f), out _), 1e-6);

            Assert.IsTrue(float.IsPositiveInfinity(Rescue.AlarmHour(Rescue.NotFiled, Rescue.NoSos, out var none)));
            Assert.AreEqual(Alarm.None, none);
        }

        // ── the operation ─────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void TheHelicopterFliesOnlyInGoodWeatherAndOnlyToTheSaddle()
        {
            Assert.IsTrue(Rescue.HelicopterFlies(Fine(5400f)), "седловина, ясно, слабый ветер — борт идёт");

            Assert.IsFalse(Rescue.HelicopterFlies(new RescueAsk(5400f, 18f, 5000f, true)), "ветер");
            Assert.IsFalse(Rescue.HelicopterFlies(Foul(5400f)), "облачность — уже не лётная");
            Assert.IsFalse(Rescue.HelicopterFlies(new RescueAsk(5400f, 5f, AscentRoute.VisibilityM(SkyState.Blizzard), true)), "пурга");
            Assert.IsFalse(Rescue.HelicopterFlies(new RescueAsk(5400f, 5f, 5000f, false)), "темно");
            Assert.IsFalse(Rescue.HelicopterFlies(Fine(Elbrus.WestSummit.Ele)), "с вершины не снимают — надо спуститься на седловину");

            Assert.AreEqual(AscentCold.WalkHardMs, Rescue.HeliWindMs, 1e-6, "тот же порог, что разворачивает группу");
            StringAssert.Contains("Ветер", Rescue.HelicopterWhyNot(new RescueAsk(5400f, 18f, 5000f, true)));
            StringAssert.Contains("видимости", Rescue.HelicopterWhyNot(Foul(5400f)));
        }

        [Test]
        public void ByAirItIsTwentyMinutesAndWithoutAirItIsADay()
        {
            var r = Party;
            var sos = Rescue.SendSos(5240f, 12f);          // just under the ceiling: the one place the saddle can call from
            Assert.IsTrue(sos.Sent);

            var air = Rescue.Plan(r, sos, Fine(5400f));
            Assert.AreEqual(RescueKind.Helicopter, air.Kind);
            Assert.AreEqual(Alarm.Sos, air.How);
            Assert.AreEqual(12f, air.AlarmHour, 1e-6);
            Assert.AreEqual(20f / 60f, air.SafeHour - air.ReachHour, 1e-3, "снимают с седловины минут за двадцать");

            // and it is still not a teleport: the machine has to be scrambled first
            Assert.Greater(air.WaitHours, 0f);
            Assert.AreEqual(Rescue.HeliScrambleHours, air.WaitHours, 1e-6);
            Assert.IsFalse(air.Arrived(12.5f));
            Assert.IsTrue(air.Arrived(13.1f));

            // the same accident under cloud: nobody flies, and it becomes a day
            var ground = Rescue.Plan(r, sos, Foul(5400f));
            Assert.AreEqual(RescueKind.Foot, ground.Kind);
            Assert.AreEqual(24f, ground.TotalHours, 1e-3, "без авиации эвакуация — около суток");
            Assert.Greater(ground.TotalHours, air.TotalHours * 10f, "разница между бортом и пешими — порядок");
            StringAssert.Contains("суток", ground.Note);
        }

        [Test]
        public void OnFootFromPastukhovRocksToTheSummitIsThreeToFourHours()
        {
            Assert.AreEqual(4700f, Rescue.FootFromEle, 1e-6, "пешие спасатели выходят от скал Пастухова");
            Assert.AreEqual(0f, Rescue.FootHours(Rescue.FootFromEle), 1e-6);

            float toSummit = Rescue.FootHours(Elbrus.WestSummit.Ele);
            Assert.GreaterOrEqual(toSummit, 3f, "3–4 часа от скал до вершины");
            Assert.LessOrEqual(toSummit, 4f);

            Assert.Less(Rescue.FootHours(4000f), toSummit, "вниз спасатели идут быстрее, чем вверх");

            // the approach is on top of the walking: the duty crew has to be raised and got up the mountain first
            Assert.Greater(Rescue.FootReachHours(Elbrus.WestSummit.Ele), toSummit);

            var m = Rescue.Plan(Party, Rescue.SendSos(4800f, 10f), Foul(Elbrus.WestSummit.Ele));
            Assert.AreEqual(RescueKind.Foot, m.Kind);
            Assert.AreEqual(Rescue.FootReachHours(Elbrus.WestSummit.Ele), m.WaitHours, 1e-4);
            Assert.Greater(m.WaitHours, 5f, "помощь наверху — это половина дня ожидания");
        }

        [Test]
        public void TheHudCanSayHowLongThereIsToWait()
        {
            var m = Rescue.Plan(Party, Rescue.SendSos(5000f, 12f), Fine(5000f));
            StringAssert.Contains("ждать", Rescue.Line(m, 12.1f));
            StringAssert.Contains("рядом", Rescue.Line(m, m.ReachHour + .01f));
            Assert.AreEqual(0f, m.HoursLeft(m.ReachHour + 1f), 1e-6);

            var nobody = Rescue.Plan(Rescue.NotFiled, Rescue.NoSos, Fine(5000f));
            Assert.IsTrue(float.IsPositiveInfinity(nobody.HoursLeft(12f)));
            StringAssert.Contains("Никто не ищет", Rescue.Line(nobody, 12f));
            StringAssert.Contains("контрольное время", Rescue.CounterText(Rescue.NotFiled));
        }
    }
}
