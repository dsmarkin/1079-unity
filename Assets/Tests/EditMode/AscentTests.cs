using System;
using NUnit.Framework;
using Height1079.Core;
using Step = Height1079.Core.Ascent.Step;

namespace Height1079.Tests
{
    /// <summary>The rules of the ascent from the south. The numbers pinned down here are the ones the design brief
    /// and the sources fix — the belts of the surface, the crampon gate, the ceiling on running, the wind-chill
    /// index, the МЧС cable, the acclimatisation curve — not taste. Where a number is a game choice the test says so.
    /// Everything geographic is checked against our own height field, not against a label.</summary>
    public class AscentTests
    {
        static HeightField Dem => ElbrusData.Dem;

        // the three places the rules behave differently, as RoutePoints
        static RoutePoint Road => new RoutePoint(3900f, 9.7f, 4f);                  // ратрачная колея
        static RoutePoint Mirror => new RoutePoint(4900f, 25f, 8f);                 // «зеркало» над Пастухова
        static RoutePoint Shelf => new RoutePoint(5300f, 5f, 30f);                  // косая полка: полого вдоль, круто поперёк

        [Test]
        public void SurfaceBeltsFollowTheMeasuredHeight()
        {
            Assert.AreEqual(Surface.Groomed, Ascent.SurfaceAt(3999f));
            Assert.AreEqual(Surface.Firn, Ascent.SurfaceAt(4000f));
            Assert.AreEqual(Surface.Firn, Ascent.SurfaceAt(4649f));
            Assert.AreEqual(Surface.Ice, Ascent.SurfaceAt(4650f), "скалы Пастухова — граница льда");
            Assert.AreEqual(Surface.Ice, Ascent.SurfaceAt(5099f));
            Assert.AreEqual(Surface.Sastrugi, Ascent.SurfaceAt(5100f));
            Assert.AreEqual(Surface.LooseOverIce, Ascent.SurfaceAt(5400f));
            Assert.AreEqual(Surface.WindCrust, Ascent.SurfaceAt(5550f));
            Assert.AreEqual(Surface.WindCrust, Ascent.SurfaceAt(5642f));

            Assert.AreEqual(1f, Ascent.Spec(Surface.Groomed).BarefootSpeed, 1e-6, "по колее идут как по дороге");
            Assert.AreEqual(0f, Ascent.SlipPerMetre(Road, false), 1e-6, "ниже 4000 не срываются");
        }

        [Test]
        public void TheShelfPoiIsNotAt5100AndTriggersMustComeFromTheDem()
        {
            // the POI used to be labelled «~5100»; on our height field it stands 280 m higher, and the belt that
            // governs it is the fatal one, not the «зеркало». A mechanic hung on the label would fire in the wrong place.
            var shelf = Elbrus.Get("shelf");
            float measured = Dem.Sample(shelf.X, shelf.Z);
            Assert.Greater(measured, 5300f, "точка косой полки лежит выше 5300 м, а не на 5100");
            Assert.AreEqual(measured, shelf.Ele, 25f, "подпись POI согласована с DEM");
            Assert.AreEqual(Surface.Sastrugi, Ascent.SurfaceAt(measured), "полка — пояс застругов, срыв здесь смертельный");
            Assert.IsTrue(Ascent.SpecAt(measured).Deadly);
        }

        [Test]
        public void EveryRouteStageSitsWhereTheHeightFieldSaysItDoes()
        {
            var route = Elbrus.SummitRoute;
            Assert.AreEqual(6694f, Elbrus.Length(route), 12f, "6,69 км по нашей полилинии");
            float previous = -1f;
            foreach (var (label, ele, s) in Elbrus.RouteStages)
            {
                var p = Elbrus.PointAt(route, s);
                Assert.AreEqual(ele, Dem.Sample(p.x, p.z), 8f, label + ": отметка этапа взята с DEM");
                Assert.Greater(s, previous, label + ": этапы идут по возрастанию длины");
                previous = s;
            }
            // the signpost heights are legitimately different and must not be what the rules read
            Assert.AreEqual(5416f, Elbrus.Saddle.Ele, 1f, "вывеска седловины остаётся 5416");
            Assert.Less(Dem.Sample(Elbrus.Saddle.X, Elbrus.Saddle.Z), 5400f, "радар меряет 5382");
        }

        [Test]
        public void TheSnowCatLaneAlreadyReachesTheTopOfTheGroomedTrack()
        {
            var last = Elbrus.RatrakRoute[Elbrus.RatrakRoute.Length - 1];
            float top = Dem.Sample(last.x, last.z);
            Assert.AreEqual(AscentRoute.RatrakTopEle, top, 20f, "колея кончается на 5080 м, продлевать нечего");
            foreach (float stop in AscentRoute.RatrakStops)
                Assert.Less(stop, top + 25f, "обе точки высадки лежат на колее");
        }

        [Test]
        public void CramponsAreTheGateOnTheIceAndNowhereElse()
        {
            var fresh = new Climber { Acclimatisation = 1f };

            Assert.AreEqual(.4f, Ascent.FootingFactor(4900f, false), 1e-6, "жёсткий фирн и лёд без кошек — ×0.4");
            Assert.AreEqual(1f, Ascent.FootingFactor(4900f, true), 1e-6, "с кошками — как по колее");
            Assert.AreEqual(1f, Ascent.FootingFactor(3900f, false), 1e-6, "ниже 4000 кошки не нужны");

            // the whole speed multiplier, which is what the player feels: the cat road against the mirror above the rocks
            float road = Ascent.SpeedFactor(fresh, Road);
            float mirror = Ascent.SpeedFactor(fresh, Mirror);
            Assert.AreEqual(1f, road, .05f, "на колее темп полный");
            Assert.Greater(road / mirror, 3.2f, "без кошек над скалами темп падает примерно вчетверо");
            Assert.Less(road / mirror, 4.5f);

            fresh.CramponsOn = true;
            Assert.AreEqual(2.5f, Ascent.SpeedFactor(fresh, Mirror) / mirror, .01f, "кошки возвращают ровно ×2.5");
            Assert.AreEqual(.1f, Ascent.SlipPerMetre(Mirror, true) / Ascent.SlipPerMetre(Mirror, false), 1e-5,
                "и оставляют десятую часть шанса сорваться");
            Assert.AreEqual(.008f, Ascent.SlipPerMetre(Mirror, false), 1e-6, "0,8 % на метр в поясе льда");
        }

        [Test]
        public void RunningIsGoneAbove4600()
        {
            Assert.IsTrue(Ascent.MayRun(4599f));
            Assert.IsFalse(Ascent.MayRun(4600f), "это не «дороже», это недоступно");
            Assert.IsFalse(Ascent.MayRun(5300f));
        }

        [Test]
        public void WithoutAnIceAxeThereIsNoSelfArrestAtAll()
        {
            foreach (float deg in new[] { 15f, 20f, 25f, 30f, 35f, 40f })
                Assert.AreEqual(0f, Ascent.SelfArrestChance(deg, Gear.Crampons | Gear.Harness), 1e-6,
                    "без ледоруба самозадержания нет — швед на Пастухова");

            var axe = Gear.IceAxe;
            Assert.AreEqual(.8f, Ascent.SelfArrestChance(20f, axe), .01f);
            Assert.AreEqual(.4f, Ascent.SelfArrestChance(30f, axe), .01f);
            Assert.AreEqual(.1f, Ascent.SelfArrestChance(35f, axe), .01f);
            Assert.Greater(Ascent.SelfArrestChance(20f, axe), Ascent.SelfArrestChance(30f, axe));
        }

        [Test]
        public void AFallOnTheShelfRunsOutOnTheIceCliffs()
        {
            Assert.IsTrue(Ascent.FallIsFatal(Shelf), "срыв на косой полке смертельный");
            float run = Ascent.RunoutM(Shelf);
            Assert.Greater(run, Ascent.ShelfRunoutMin - 1f, "улетаешь на 300–600 м");
            Assert.Less(run, Ascent.ShelfRunoutMax + 1f);

            // the shelf is flat along the track and steep across it: the cross-slope is what decides
            Assert.AreEqual(30f, Shelf.Steepness, 1e-6);
            Assert.Greater(Ascent.SlipPerMetre(Shelf, false), 0f, "поперечный уклон 30° — скользко");
            var flat = new RoutePoint(5300f, 5f, 3f);
            Assert.AreEqual(0f, Ascent.SlipPerMetre(flat, false), 1e-6, "на ровном месте того же пояса не срываются");
        }

        [Test]
        public void HypoxiaFollowsTheMeasuredRateOfClimbAndAcclimatisationScalesIt()
        {
            Assert.AreEqual(1f, Ascent.Hypoxia(3800f, 1f), .01f);
            Assert.AreEqual(.85f, Ascent.Hypoxia(4300f, 1f), .01f);
            Assert.AreEqual(.70f, Ascent.Hypoxia(4700f, 1f), .01f);
            Assert.AreEqual(.55f, Ascent.Hypoxia(5100f, 1f), .01f);
            Assert.AreEqual(.40f, Ascent.Hypoxia(5400f, 1f), .01f);
            Assert.AreEqual(.30f, Ascent.Hypoxia(5642f, 1f), .01f);

            Assert.AreEqual(.6f, Ascent.AcclimFactor(.3f), .001f, "при acclim 0.3 вся кривая ×0.6");
            Assert.AreEqual(.55f * .6f, Ascent.Hypoxia(5100f, .3f), .005f);
            for (float e = 3800f; e < 5642f; e += 200f)
                Assert.Greater(Ascent.Hypoxia(e, 1f), Ascent.Hypoxia(e + 200f, 1f), "кривая монотонна");
        }

        [Test]
        public void StrengthComesBackOnlyStandingAndThriceSlowerAbove5000()
        {
            float low = Ascent.RecoveryFactor(3800f, true);
            float high = Ascent.RecoveryFactor(5100f, true);
            Assert.AreEqual(low / 3f, high, 1e-5, "выше 5000 втрое медленнее");
            Assert.AreEqual(0f, Ascent.RecoveryFactor(5100f, false), 1e-6, "и только стоя");
            Assert.Greater(Ascent.RecoveryFactor(5100f, true, true), high, "шаг-вдох даёт бонус");
        }

        [Test]
        public void PushingThePaceAtHeightForcesAStop()
        {
            var c = new Climber { Acclimatisation = 1f, CramponsOn = true };
            var air = new MountainAir(-15f, 5f);
            var step = new Step(new RoutePoint(5400f, 20f, 10f), air, 1.1f);
            bool stopped = false;
            for (int i = 0; i < 300 && !stopped; i++) stopped = Ascent.Tick(c, step, 1f).MustStop;
            Assert.IsTrue(stopped, "попытка ускориться на 5400 загоняет пульс");
            Assert.Greater(c.StopSeconds, 9f, "вынужденная остановка 10–20 с");
            Assert.Less(c.StopSeconds, 21f);

            // at the step-and-breathe pace the heart settles instead
            var calm = new Climber { Acclimatisation = 1f };
            var slow = new Step(new RoutePoint(5400f, 20f, 10f), air, Ascent.BreathPaceMs);
            for (int i = 0; i < 300; i++) Ascent.Tick(calm, slow, 1f);
            Assert.AreEqual(0f, calm.Pulse, 1e-5, "шаг-вдох пульс не загоняет");
        }

        [Test]
        public void AcclimatisationIsClimbHighSleepLow()
        {
            Assert.AreEqual(.10f, Ascent.TouchGain(4000f, 3700f), 1e-6, "выход на 3800–4200 с возвратом");
            Assert.AreEqual(.20f, Ascent.TouchGain(4700f, 3700f), 1e-6, "выход на 4600–4800 с возвратом");
            Assert.AreEqual(0f, Ascent.TouchGain(4700f, 4600f), 1e-6, "без сброса на ночёвку выход не засчитан");
            Assert.AreEqual(.05f, Ascent.SleepGain(3900f), 1e-6);
            Assert.AreEqual(.08f, Ascent.SleepGain(4100f), 1e-6);
            Assert.Less(Ascent.SleepGain(4600f), Ascent.SleepGain(4100f), "спать высоко не акклиматизирует");

            Assert.AreEqual(0f, Ascent.TouchGain(4000f, 3900f), 1e-6,
                "ночёвка в ста метрах ниже высшей точки — это ещё не «sleep low»");

            float a = .3f;
            a = Ascent.Acclimatise(a, new Ascent.Sortie(4000f, 3700f));      // +0.10 выход, ночёвка на 3700 сама по себе ничего
            Assert.AreEqual(.40f, a, 1e-5);
            a = Ascent.Acclimatise(a, new Ascent.Sortie(4700f, 4100f));      // +0.20 выход, +0.08 ночёвка на 4100
            Assert.AreEqual(.68f, a, 1e-5);
            a = Ascent.Acclimatise(a, new Ascent.Sortie(3000f, 3000f, 2f));  // двое суток в долине без набора
            Assert.AreEqual(.58f, a, 1e-5, "распад 0.05 в сутки");
        }

        [Test]
        public void SicknessComesAtLowAcclimatisationAndStaysAwayAtFull()
        {
            var air = new MountainAir(-12f, 6f);
            var where = new RoutePoint(4700f, 18f, 8f);

            var tourist = new Climber { Acclimatisation = .3f, CramponsOn = true };
            var climber = new Climber { Acclimatisation = 1f, CramponsOn = true };
            var step = new Step(where, air, .5f);
            for (int i = 0; i < 3 * 3600; i += 10) { Ascent.Tick(tourist, step, 10f); Ascent.Tick(climber, step, 10f); }

            Assert.Greater((int)tourist.Phase, (int)Ams.None, "acclim 0.3 на 4700 — горняшка приходит");
            Assert.AreEqual(Ams.None, climber.Phase, "acclim 1.0 на 4700 — не приходит");
            Assert.AreEqual(0f, climber.SicknessLoad, 1e-6, "тело успевает за высотой");

            Assert.Less(Ascent.ToleratedEle(.3f), 3900f);
            Assert.Greater(Ascent.ToleratedEle(1f), 5000f);
            Assert.Greater(Ascent.SicknessRate(5200f, 1f, 0f), 0f, "выше 5000 не адаптируются вовсе");
            Assert.Less(Ascent.SicknessRate(4000f, 1f, 0f), 0f, "внизу долг гасится");
            Assert.Greater(Ascent.SicknessRate(4700f, .3f, 1f), Ascent.SicknessRate(4700f, .3f, 0f),
                "обезвоживание ускоряет горняшку");
        }

        [Test]
        public void EveryPhaseIsAnnouncedBeforeTheNextArrives()
        {
            var c = new Climber { Acclimatisation = .3f };
            var p = new RoutePoint(5100f, 10f, 20f);
            c.SicknessLoad = 0f;
            StringAssert.Contains("акклиматизир", Ascent.Warning(c, p));           // ещё ни одной фазы, но уже видно
            c.SicknessLoad = Ascent.HeadacheAt * .6f;
            StringAssert.Contains("виски", Ascent.Warning(c, p));
            c.SicknessLoad = Ascent.HeadacheAt;
            Assert.AreEqual(Ams.Headache, c.Phase);
            c.SicknessLoad = Ascent.AtaxiaAt;
            Assert.AreEqual(Ams.Ataxia, c.Phase);
            Assert.Greater(Ascent.AtaxiaDriftMs(c.Phase), 0f, "атаксию ведёт вбок");
            Assert.IsFalse(Ascent.RecoversInPlace(Ams.Nausea), "при тошноте отдых не помогает");
            Assert.AreEqual(.9f, Ascent.SicknessStrength(Ams.Headache), 1e-6, "головная боль — −10 % сил");
            c.SicknessLoad = Ascent.EdemaAt;
            StringAssert.Contains("500", Ascent.Warning(c, p));
            Assert.AreEqual(Ascent.EdemaDescentM, 500f, 1e-6);
        }

        [Test]
        public void TemperatureFallsOffTheMeadowAtSixTenthsPerHundredMetres()
        {
            Assert.AreEqual(8f, AscentCold.AirTempC(AscentCold.SummerNightBaseC, 2350f), 1e-4);
            Assert.AreEqual(-12f, AscentCold.AirTempC(AscentCold.SummerNightBaseC, 5642f), .3f, "летняя ночь на вершине −12");
            Assert.AreEqual(-32f, AscentCold.AirTempC(AscentCold.WinterBaseC, 5642f), .3f, "зимой −32");
            Assert.AreEqual(-6.7f, AscentCold.AirTempC(AscentCold.SummerNightBaseC, 4800f), 1.5f, "4800 летом ночью −6…−8");
        }

        [Test]
        public void TheWindChillIndexIsTheHarshOne()
        {
            float feels = AscentCold.FeelsC(-20f, 7.5f);
            Assert.AreEqual(-38f, feels, 2.5f, "−20 °C при 7,5 м/с ощущается около −38…−40");
            Assert.Less(feels, -34f, "это старый ветро-холодовой индекс, а не мягкая формула метеослужб");
            Assert.AreEqual(-20f, AscentCold.FeelsC(-20f, 0f), 1e-4, "в штиль — просто воздух");
            Assert.Less(AscentCold.FeelsC(-20f, 20f), feels, "сильнее ветер — холоднее");

            Assert.AreEqual(30f, AscentCold.FrostbiteMinutes(-28f), .5f);
            Assert.AreEqual(10f, AscentCold.FrostbiteMinutes(-40f), .5f);
            Assert.AreEqual(5f, AscentCold.FrostbiteMinutes(-48f), .5f);
            Assert.AreEqual(2f, AscentCold.FrostbiteMinutes(-55f), .5f);
            Assert.Less(AscentCold.FrostbiteMinutes(-60f), 2f, "ниже −55 — меньше двух минут");
            Assert.IsTrue(float.IsPositiveInfinity(AscentCold.FrostbiteMinutes(-20f)), "выше −28 кожа не горит");
        }

        [Test]
        public void HandsFeetAndFaceFreezeOnThreeSeparateClocks()
        {
            const float feels = -45f;   // 5–10 минут открытой кожи
            float bareFace = AscentCold.FreezeRate(Limb.Face, feels, false, true);
            float maskedFace = AscentCold.FreezeRate(Limb.Face, feels, true, true);
            Assert.AreEqual(1f / (AscentCold.FrostbiteMinutes(feels) * 60f), bareFace, 1e-7, "лицо без балаклавы горит по таблице");
            Assert.Greater(bareFace / maskedFace, 5f, "балаклава покупает время");

            Assert.Greater(AscentCold.FreezeRate(Limb.Hands, feels, false, true),
                           AscentCold.FreezeRate(Limb.Hands, feels, true, true), "варежки сняли — руки горят");
            Assert.Greater(AscentCold.FreezeRate(Limb.Feet, feels, true, false),
                           AscentCold.FreezeRate(Limb.Feet, feels, true, true), "ноги мёрзнут на остановках, не в движении");

            // the pools do not share heat: a run with bare hands burns the hands and leaves the feet alone
            var c = new Climber { Gear = Gear.Mittens | Gear.Balaclava, Acclimatisation = 1f, CramponsOn = true };
            var step = new Step(new RoutePoint(5000f, 10f, 12f), new MountainAir(-20f, 8f), .4f) { BareHands = true };
            for (int i = 0; i < 300; i++) Ascent.Tick(c, step, 1f);
            Assert.Greater(c.Hands, .5f, "пять минут с голыми руками на ветру — это много");
            Assert.Less(c.Face, c.Hands, "лицо закрыто");
            Assert.Less(c.Feet, c.Hands, "ноги в ботинках и в движении");
        }

        [Test]
        public void TheShelfAndTheSaddleAreAWindFunnel()
        {
            const float baseWind = 10f;
            float below = AscentCold.WindAt(baseWind, 5000f, 2000f);
            float shelf = AscentCold.WindAt(baseWind, 5300f, 2000f);
            float saddle = AscentCold.WindAt(baseWind, 5382f, 0f);

            Assert.AreEqual(baseWind, below, 1e-5, "ниже полки ветер локации без изменений");
            Assert.AreEqual(baseWind * 1.8f + 8f, shelf, 1e-4, "на 5250–5400 ×1.8 и +8 м/с");
            Assert.AreEqual(baseWind * 2.2f + 12f, saddle, 1e-4, "на седловине ×2.2 и +12 м/с");
            Assert.Greater(saddle, shelf);

            Assert.AreEqual(0f, AscentCold.SideDriftMs(14f), 1e-6, "до 15 м/с не сносит");
            Assert.AreEqual(.3f, AscentCold.SideDriftMs(16f), .01f, "боковой ветер >15 м/с — снос 0,3 м/с");
            Assert.AreEqual(0f, AscentCold.GustSlipChance(24f), 1e-6);
            Assert.AreEqual(.015f, AscentCold.GustSlipChance(26f), 1e-6, "шатания с шансом срыва 1,5 % за порыв");

            Assert.AreEqual(7, AscentCold.Beaufort(15f), "7 баллов — 13,9–17,1 м/с");
            Assert.AreEqual(8, AscentCold.Beaufort(19f), "8 баллов — 17,2–20,7 м/с");
            Assert.IsTrue(AscentCold.TurnBackWind(14f), "при 7 баллах — вниз");
            Assert.IsFalse(AscentCold.TurnBackWind(13f));
        }

        [Test]
        public void TheCableRunsExactlyFrom4900To5250()
        {
            Assert.IsFalse(AscentRoute.HasFixedRope(4899f));
            Assert.IsTrue(AscentRoute.HasFixedRope(4900f));
            Assert.IsTrue(AscentRoute.HasFixedRope(5250f));
            Assert.IsFalse(AscentRoute.HasFixedRope(5251f), "перила МЧС кончаются на 5250, выше троса нет");

            Assert.IsTrue(AscentRoute.RopeInReach(5000f, 2f), "за трос можно держаться и без системы");
            Assert.IsFalse(AscentRoute.RopeInReach(5000f, 40f), "если отошёл — нечего держать");
            Assert.IsFalse(AscentRoute.RopeProtects(5000f, 2f, Gear.Crampons | Gear.IceAxe),
                "верёвка бесполезна, пока не надета система");
            Assert.IsTrue(AscentRoute.RopeProtects(5000f, 2f, Gear.Harness));

            Assert.IsTrue(AscentRoute.HasSignal(5249f));
            Assert.IsFalse(AscentRoute.HasSignal(5250f), "связь пропадает выше 5250");
        }

        [Test]
        public void TheRouteIsLostOnlyWhenNothingMarksItAtAll()
        {
            Assert.AreEqual(5000f, AscentRoute.VisibilityM(SkyState.Clear), 1e-3);
            Assert.AreEqual(300f, AscentRoute.VisibilityM(SkyState.Cloud), 1e-3);
            Assert.AreEqual(50f, AscentRoute.VisibilityM(SkyState.Snow), 1e-3);
            Assert.AreEqual(10f, AscentRoute.VisibilityM(SkyState.Blizzard), 1e-3);
            Assert.AreEqual(1f, AscentRoute.VisibilityM(SkyState.WhiteOut), 1e-3);

            const float blizzard = 10f;
            Assert.IsTrue(AscentRoute.CanReadTheRoute(5000f, 2f, blizzard, 0f, false), "держась за трос, маршрут не теряют");
            Assert.IsTrue(AscentRoute.CanReadTheRoute(4200f, 20f, blizzard, 0f, true), "по следам ратрака тоже");
            Assert.IsTrue(AscentRoute.CanReadTheRoute(5300f, 0f, 60f, 0f, false), "видимость выше 50 м — коридор читается");
            Assert.IsFalse(AscentRoute.CanReadTheRoute(5300f, 30f, blizzard, 0f, false),
                "выше троса, в пургу, в стороне от линии — вешек не видно");

            Assert.AreEqual(1f, AscentRoute.WandsReadable(10f), 1e-6);
            Assert.Less(AscentRoute.WandsReadable(30f), .7f, "после снегопада >10 см 30–60 % вешек не читаются");
            Assert.Greater(AscentRoute.WandSpacing(30f), AscentRoute.WandSpacingM, "уцелевшие вешки стоят реже");
            Assert.IsTrue(AscentRoute.WandInSight(300f, 0f, 0f), "в облачности вешка видна");
            Assert.IsFalse(AscentRoute.WandInSight(10f, 0f, 0f), "в пургу — нет");

            Assert.IsTrue(AscentRoute.OnTheSeracs(160f), "отклонение >150 м вниз от седловины — ледовые сбросы");
            Assert.IsFalse(AscentRoute.OnTheSeracs(140f));
            Assert.AreEqual(1f / 3f, AscentRoute.TrenchColdFactor, 1e-6, "в траншее переохлаждение втрое медленнее");
        }

        [Test]
        public void CrevassesAreRightOfTheLaneOnTheLowerGlacier()
        {
            const int august = 8, june = 6;
            Assert.Greater(AscentRoute.CrevasseChance(3980f, 50f, august, false), 0f, "вправо от колеи — нельзя");
            Assert.AreEqual(0f, AscentRoute.CrevasseChance(3980f, -200f, august, false), 1e-6, "влево — сколько угодно");
            Assert.AreEqual(0f, AscentRoute.CrevasseChance(3980f, 50f, august, true), 1e-6,
                "где течёт ручей, трещины нет — это подсказка в мире");

            Assert.Greater(AscentRoute.CrevasseChance(3980f, 50f, august, false),
                           AscentRoute.CrevasseChance(3980f, 50f, june, false), "в августе мосты тоньше");
            Assert.AreEqual(AscentRoute.AugustBridge, AscentRoute.BridgeChance(august), 1e-6);
            Assert.AreEqual(AscentRoute.EarlySummerBridge, AscentRoute.BridgeChance(june), 1e-6);

            // and nowhere else: not on the shelf, not on the saddle, not above the rocks
            Assert.AreEqual(0f, AscentRoute.CrevasseChance(5300f, 80f, august, false), 1e-6, "на полке трещин нет");
            Assert.AreEqual(0f, AscentRoute.CrevasseChance(5382f, 80f, august, false), 1e-6, "на седловине тоже");
            Assert.AreEqual(0f, AscentRoute.CrevasseChance(4500f, 80f, august, false), 1e-6, "выше ледника — тоже");
            Assert.AreEqual(4, AscentRoute.DangerousCrevasses);
            Assert.AreEqual(8, AscentRoute.CrevasseCount);
        }

        [Test]
        public void TheSnowCatBuysTimeAndSellsAcclimatisation()
        {
            Assert.IsFalse(AscentRoute.RatrakGivesAcclimatisation, "заброска не даёт акклиматизации");
            Assert.AreEqual(40f, AscentRoute.RatrakMinutes, 1e-6, "40 минут вместо 5–6 часов");

            Assert.AreEqual(5080f, AscentRoute.RatrakDropEle(5100f, 5f, 3000f), 20f, "в хорошее утро довозят до верха колеи");
            Assert.AreEqual(AscentRoute.RatrakFallbackEle, AscentRoute.RatrakDropEle(5100f, 25f, 3000f), 1e-6,
                "при ветре >20 м/с разворачивается на 4650");
            Assert.AreEqual(AscentRoute.RatrakFallbackEle, AscentRoute.RatrakDropEle(5100f, 5f, 150f), 1e-6,
                "и при видимости <200 м");

            Assert.AreEqual(1.4f, AscentRoute.ColdShock(0f), 1e-6, "первые десять минут коэффициент холода ×1.4");
            Assert.AreEqual(1.4f, AscentRoute.ColdShock(599f), 1e-6);
            Assert.AreEqual(1f, AscentRoute.ColdShock(601f), 1e-6);

            // a climber put down at 5 100 with a ropeway-day acclimatisation is in real trouble at once
            var dropped = new Climber { Acclimatisation = .3f, CramponsOn = true, Gear = Ascent.Required };
            var step = new Step(new RoutePoint(5100f, 12f, 18f), new MountainAir(-18f, 14f), .4f);
            for (int i = 0; i < 2 * 3600; i += 10) Ascent.Tick(dropped, step, 10f);
            Assert.IsTrue(dropped.Phase >= Ams.Headache, "два часа на 5100 без акклиматизации — горняшка уже здесь");
        }

        [Test]
        public void NobodyPassesPastukhovRocksWithoutTheKit()
        {
            Assert.IsTrue(Ascent.MayPassGate(4649f, Gear.None), "ниже скал снаряжение не спрашивают");
            Assert.IsFalse(Ascent.MayPassGate(4650f, Gear.None), "выше — жёсткий гейт");
            Assert.IsFalse(Ascent.MayPassGate(4650f, Ascent.Required & ~Gear.IceAxe), "без ледоруба — нет");
            Assert.IsTrue(Ascent.MayPassGate(4650f, Ascent.Required));
            Assert.AreEqual(3, Ascent.Carabiners, "три муфтованных карабина на усе");

            Assert.AreEqual(Gear.IceAxe | Gear.Thermos, Ascent.Missing(Ascent.Required & ~(Gear.IceAxe | Gear.Thermos)));
            StringAssert.Contains("ледоруб", Ascent.MissingList(Ascent.Required & ~Gear.IceAxe));
            Assert.AreEqual("", Ascent.MissingList(Ascent.Required), "полный комплект — молча");
        }

        [Test]
        public void SunOnTheSnowBlindsWithoutGogglesAndTheThermosKeepsYouAwake()
        {
            Assert.AreEqual(0f, AscentRoute.BlindnessRate(5000f, true, true), 1e-9, "в очках ничего не происходит");
            Assert.AreEqual(0f, AscentRoute.BlindnessRate(5000f, false, false), 1e-9, "и до рассвета тоже");
            float rate = AscentRoute.BlindnessRate(3800f, false, true);
            Assert.AreEqual(AscentRoute.BlindHurtsAt, rate * 1.5f * 3600f, .02f, "резь через полтора часа");
            Assert.AreEqual(1f, rate * AscentRoute.BlindHours * 3600f, .02f, "через два часа не видит");
            Assert.Greater(AscentRoute.UvFactor(5642f), AscentRoute.UvFactor(3800f), "+4 % на 100 м высоты");

            // the monotony of the shelf: flat, dark, an hour and a half of it
            var c = new Climber { Acclimatisation = 1f, CramponsOn = true };
            var step = new Step(new RoutePoint(5300f, 4f, 28f), new MountainAir(-20f, 10f), .4f) { Daylight = false };
            for (int i = 0; i < 90 * 60; i += 10) Ascent.Tick(c, step, 10f);
            Assert.Greater(c.Drowsiness, Ascent.DrowsyFrom, "полтора часа по полке в темноте — засыпают на ходу");
            Assert.AreEqual(Ascent.DrowsySpeed, Ascent.DrowsyFactor(c.Drowsiness), 1e-6, "иначе темп ×0.7");
            c.Drowsiness = 0f;                                      // глоток из термоса
            Assert.AreEqual(1f, Ascent.DrowsyFactor(c.Drowsiness), 1e-6);

            // and it is the shelf that does it, not the height
            Assert.AreEqual(0f, Ascent.DrowsinessRate(new RoutePoint(5300f, 4f, 28f), false), 1e-9, "днём не клонит");
            Assert.AreEqual(0f, Ascent.DrowsinessRate(new RoutePoint(4200f, 4f, 6f), true), 1e-9, "ниже полки тоже");
        }

        [Test]
        public void TheClockDecidesMoreThanTheLegsDo()
        {
            Assert.IsTrue(AscentRoute.Dark(4f), "выход в 2:00–4:00 идёт в темноте");
            Assert.IsFalse(AscentRoute.Dark(6f), "светает к 5:30");
            Assert.IsFalse(AscentRoute.PastTurnaround(12.9f));
            Assert.IsTrue(AscentRoute.PastTurnaround(13f), "контрольное время разворота — 13:00");

            Assert.AreEqual(0f, AscentRoute.WeatherRiskPerHour(11f), 1e-6);
            Assert.AreEqual(.08f, AscentRoute.WeatherRiskPerHour(12f), 1e-6, "+8 %/час после полудня");
            Assert.AreEqual(.20f, AscentRoute.WeatherRiskPerHour(14f), 1e-6, "+20 %/час после 14:00");

            Assert.Greater(AscentRoute.StormOnsetSeconds(false), 90f * 60f - 1f, "летом 90–120 минут на разворот");
            Assert.Less(AscentRoute.StormOnsetSeconds(true), 21f * 60f, "зимой 15–20 минут");
            Assert.Greater(AscentRoute.FrontTempDropC, 10f, "температура падает на 10–15 градусов за полчаса");
        }

        [Test]
        public void AboveTheHutsThereIsExactlyOneRoof()
        {
            Assert.AreEqual(4200f, AscentRoute.LastHutEle, 1e-6);
            Assert.AreEqual(5300f, AscentRoute.SaddleHutEle, 1e-6, "хижина Red Fox 5300 на седловине");
            Assert.AreEqual(6, AscentRoute.SaddleHutLying);
            Assert.AreEqual(12, AscentRoute.SaddleHutSitting);
            Assert.AreEqual(80f, AscentRoute.SaddleHutWindMs, 1e-6, "держит ветер до 80 м/с");
            Assert.IsFalse(AscentRoute.RuinsShelter, "остатки советских хижин укрытия не дают");
            Assert.AreEqual(19f, AscentRoute.FumaroleM, 1e-6, "фумарола в 19 метрах — тёплое пятно и ориентир");

            Assert.IsTrue(AscentRoute.HutFound(5f, 10f), "в пургу хижину надо найти с десяти метров");
            Assert.IsFalse(AscentRoute.HutFound(60f, 10f));
            Assert.IsTrue(AscentRoute.HutFound(60f, 300f));
        }

        [Test]
        public void WaterIsHeavierHereThanAnywhereElse()
        {
            Assert.Greater(AscentRoute.LitresPerDay, AscentRoute.NormalLitresPerDay, "3,5–4,5 л вместо 2,5");
            Assert.AreEqual(4600f, AscentRoute.BottleFreezeEle, 1e-6, "обычная бутылка замерзает выше 4600");
            Assert.IsTrue(AscentRoute.ThermosSips >= 4 && AscentRoute.ThermosSips <= 6, "термос — 4–6 глотков");
            Assert.AreEqual(.4f, Ascent.DehydrationBoost, 1e-6, "обезвоживание ускоряет горняшку на 40 %");
            Assert.Greater(AscentRoute.DryingRate(5300f, true), AscentRoute.DryingRate(3800f, true), "наверху сушит сильнее");
            Assert.Greater(AscentRoute.DryingRate(5300f, true), AscentRoute.DryingRate(5300f, false), "в движении — сильнее");
        }

        [Test]
        public void OneTickAnswersEverythingTheRuntimeNeeds()
        {
            var c = new Climber { Acclimatisation = .8f, Gear = Ascent.Required, CramponsOn = true };
            var air = new MountainAir(AscentCold.AirTempC(AscentCold.SummerNightBaseC, 5300f),
                                      AscentCold.WindAt(9f, 5300f, 900f), AscentRoute.VisibilityM(SkyState.Cloud));
            var r = Ascent.Tick(c, new Step(Shelf, air, .35f), 1f);

            Assert.Greater(r.SpeedFactor, 0f);
            Assert.Less(r.SpeedFactor, .6f, "на 5300 быстро не ходят");
            Assert.IsFalse(r.MayRun);
            Assert.Greater(r.SlipPerMetre, 0f);
            Assert.Greater(r.SelfArrest, 0f, "ледоруб есть");
            Assert.IsTrue(r.Deadly, "полка");
            Assert.Greater(r.RunoutM, 300f);
            Assert.Less(r.FeelsC, air.TempC, "ветер холоднее воздуха");
            Assert.AreEqual(5300f, c.HighestEle, 1e-6, "тик запоминает высшую точку — для акклиматизации после забега");

            // and it is a pure function of the state: the same tick from the same state answers the same
            var twin = new Climber { Acclimatisation = .8f, Gear = Ascent.Required, CramponsOn = true };
            var again = Ascent.Tick(twin, new Step(Shelf, air, .35f), 1f);
            Assert.AreEqual(r.SpeedFactor, again.SpeedFactor, 1e-6);
            Assert.AreEqual(r.SlipPerMetre, again.SlipPerMetre, 1e-6);
        }

        // ── the second half of the day ────────────────────────────────────────────────────────────────────

        [Test]
        public void TheDescentIsTheDangerousHalfOfTheSameSlope()
        {
            // one point, one climber, one difference: which way he is walking
            Assert.AreEqual(Ascent.SlipPerMetre(Mirror, true) * Ascent.DescentSlip,
                Ascent.SlipPerMetre(Mirror, true, Going.Down), 1e-9);
            Assert.Greater(Ascent.SlipPerMetre(Mirror, true, Going.Down), Ascent.SlipPerMetre(Mirror, true, Going.Up),
                "на спуске срываются чаще: идёшь спиной к склону, ноги устали");
            Assert.Greater(Ascent.SlipPerMetre(Shelf, false, Going.Down), Ascent.SlipPerMetre(Shelf, false, Going.Up));

            // but the direction does not invent a slip where the ground holds
            Assert.AreEqual(0f, Ascent.SlipPerMetre(Road, false, Going.Down), 1e-9, "ниже 4000 не срываются ни вверх, ни вниз");

            // and the axe works worse the wrong way up
            Assert.Less(Ascent.SelfArrestChance(30f, Gear.IceAxe, Going.Down), Ascent.SelfArrestChance(30f, Gear.IceAxe, Going.Up));
            // 1e-6 and not 1e-9: both sides are floats, and the two ways of getting there round differently — the
            // method multiplies by DescentArrest inside and returns one float, the line below multiplies the
            // returned float again. A single float step near 0.28 is about 3e-8, so a tolerance of 1e-9 was asking
            // float arithmetic for double precision. .NET 8 happened to give the same answer both ways and Unity's
            // Mono did not, so the test passed in dotnet and failed in the editor.
            Assert.AreEqual(Ascent.SelfArrestChance(30f, Gear.IceAxe) * Ascent.DescentArrest,
                Ascent.SelfArrestChance(30f, Gear.IceAxe, Going.Down), 1e-6);
            Assert.AreEqual(0f, Ascent.SelfArrestChance(30f, Gear.None, Going.Down), 1e-9, "без ледоруба самозадержания нет и вниз");

            // the old two-argument calls are the ascent, unchanged
            Assert.AreEqual(Ascent.SlipPerMetre(Mirror, true, Going.Up), Ascent.SlipPerMetre(Mirror, true), 1e-9);
            Assert.AreEqual(Ascent.SelfArrestChance(30f, Gear.IceAxe, Going.Up), Ascent.SelfArrestChance(30f, Gear.IceAxe), 1e-9);
        }

        [Test]
        public void GoingDownIsFasterAndTheSlushTakesSomeOfItBack()
        {
            var c = new Climber { Acclimatisation = .6f, Gear = Ascent.Required, CramponsOn = true };

            // losing height is the one thing that helps the lungs, and the legs carry themselves
            Assert.Greater(Ascent.Hypoxia(5400f, .6f, Going.Down), Ascent.Hypoxia(5400f, .6f, Going.Up));
            Assert.Less(Ascent.Hypoxia(5400f, .6f, Going.Down), 1f, "но воздух не становится воздухом поляны");
            Assert.AreEqual(Ascent.Hypoxia(3800f, .6f), Ascent.Hypoxia(3800f, .6f, Going.Up), 1e-9);

            float up = Ascent.SpeedFactor(c, Shelf, Going.Up, 8f);
            float down = Ascent.SpeedFactor(c, Shelf, Going.Down, 8f);
            Assert.Greater(down / up, 2f, "с полки вниз идут вдвое с лишним быстрее — 3,5–5 часов против 7–10");

            // and below the «зеркало» after ten in the morning the road has turned to porridge
            Assert.IsFalse(Ascent.Slush(4500f, 9.9f), "до десяти держит");
            Assert.IsTrue(Ascent.Slush(4500f, 10f));
            Assert.IsFalse(Ascent.Slush(4700f, 12f), "выше 4600 снег не раскисает");
            Assert.AreEqual(.7f, Ascent.SlushFactor(3900f, 12f), 1e-6, "по колее ×0,7");

            Assert.AreEqual(Ascent.SpeedFactor(c, Road, Going.Down, 9f) * Ascent.SlushSpeed,
                Ascent.SpeedFactor(c, Road, Going.Down, 12f), 1e-6, "чавкающая каша съедает треть темпа");
            Assert.Less(Ascent.SpeedFactor(c, Road, Going.Down, 12f), Ascent.SpeedFactor(c, Road, Going.Down, 9f),
                "тот же спуск по той же колее в полдень медленнее, чем в девять");

            // the one-argument call is still the ascent at night, with nothing melted
            Assert.AreEqual(Ascent.SpeedFactor(c, Road, Going.Up, 0f), Ascent.SpeedFactor(c, Road), 1e-9);
        }

        [Test]
        public void ComingDownYouHaveToFindTheGateBetweenTheLavaRidges()
        {
            // the corridor is a fact about the ground: the two ridges of Pastukhov rocks, and the wands between them
            Assert.IsFalse(AscentRoute.AtTheGate(4500f));
            Assert.IsTrue(AscentRoute.AtTheGate(4650f), "скалы Пастухова");
            Assert.IsFalse(AscentRoute.AtTheGate(4800f));
            Assert.IsTrue(AscentRoute.InTheGate(4650f, 30f), "в коридоре");
            Assert.IsTrue(AscentRoute.MissedTheGate(4650f, 60f), "мимо гряды");
            Assert.IsFalse(AscentRoute.MissedTheGate(5300f, 600f), "выше коридора мимо него не пройти");
            Assert.Greater(AscentRoute.MetresToTheGate(Elbrus.Saddle.Ele), 700f);
            Assert.AreEqual(0f, AscentRoute.MetresToTheGate(4000f), 1e-6);

            // while anything marks the line nobody wanders: that is what the wands and the cable are for
            Assert.AreEqual(0f, AscentRoute.WanderPerMetre(Going.Down, true), 1e-9);
            Assert.AreEqual(0f, AscentRoute.WanderPerMetre(Going.Down, true, Ams.Ataxia, 1f), 1e-9);

            // blind, the descent drifts off two and a half times as fast as the climb
            float down = AscentRoute.WanderPerMetre(Going.Down, false);
            float up = AscentRoute.WanderPerMetre(Going.Up, false);
            Assert.Greater(down, up, "вниз уходят с линии быстрее: наверх ведёт склон, вниз — только маршрут");
            Assert.Greater(AscentRoute.WanderPerMetre(Going.Down, false, Ams.Ataxia), down, "атаксия");
            Assert.Greater(AscentRoute.WanderPerMetre(Going.Down, false, Ams.None, 1f), down, "и пустые ноги");

            // and that is the arithmetic of the 70 %: from the saddle to the rocks is some 2,5 км of route,
            // and at this rate the ±38 м коридор is gone long before it comes into sight
            float fromSaddle = 2500f * down;
            Assert.Greater(fromSaddle, AscentRoute.GateHalfWidthM,
                "слепой спуск с седловины выносит за гряды — это и есть главная опасность спуска");
        }

        [Test]
        public void OneTickKnowsWhichWayTheDayIsGoing()
        {
            var c = new Climber { Acclimatisation = .5f, Gear = Ascent.Required, CramponsOn = true };
            var gate = new RoutePoint(4650f, -22f, 12f, 60f);                       // мимо коридора, вниз
            var whiteOut = new MountainAir(-12f, 6f, AscentRoute.VisibilityM(SkyState.Blizzard));

            var descending = Ascent.Tick(c, new Step(gate, whiteOut, .5f) { Way = Going.Down, Hour = 12f, Tiredness = .8f }, 1f);
            Assert.IsTrue(descending.RouteLost, "ни видимости, ни вешки, ни троса, ни колеи");
            Assert.Greater(descending.WanderPerMetre, 0f);
            Assert.IsTrue(descending.MissedTheGate, "коридор остался в стороне");
            StringAssert.Contains("коридор", descending.Warning);

            // the same place, the same weather, walked upward: the same lost route, less of everything else
            var twin = new Climber { Acclimatisation = .5f, Gear = Ascent.Required, CramponsOn = true };
            var climbing = Ascent.Tick(twin, new Step(gate, whiteOut, .5f) { Way = Going.Up, Hour = 12f, Tiredness = .8f }, 1f);
            Assert.IsTrue(climbing.RouteLost);
            Assert.Less(climbing.WanderPerMetre, descending.WanderPerMetre);
            Assert.Less(climbing.SlipPerMetre, descending.SlipPerMetre, "на спуске шанс срыва выше при прочих равных");
            Assert.Greater(climbing.SelfArrest, descending.SelfArrest);
            Assert.Less(climbing.SpeedFactor, descending.SpeedFactor);

            // and with the snow-cat track underfoot nobody wanders anywhere
            var onTrack = new Climber { Acclimatisation = .5f, Gear = Ascent.Required, CramponsOn = true };
            var kept = Ascent.Tick(onTrack, new Step(gate, whiteOut, .5f) { Way = Going.Down, CatTrack = true }, 1f);
            Assert.IsFalse(kept.RouteLost);
            Assert.AreEqual(0f, kept.WanderPerMetre, 1e-9);

            // a default Step is still the ascent in clear air, and reports nothing new
            var plain = Ascent.Tick(new Climber(), new Step(Road, new MountainAir(-2f, 3f), .5f), 1f);
            Assert.IsFalse(plain.RouteLost);
            Assert.AreEqual(0f, plain.WanderPerMetre, 1e-9);
            Assert.IsFalse(plain.MissedTheGate);
        }
    }
}
