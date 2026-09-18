using System;
using System.IO;
using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    /// <summary>The mini camp and the save beside it: where a двойка may go up, what one night in it is worth, and
    /// that a file written on one evening reads back on the next without taking the game down when it is old, new or
    /// broken. Everything here is engine-free and runs in <c>dotnet</c>.</summary>
    public class CampTests
    {
        // the three kinds of ground a camp meets, as CampSpots
        static CampSpot Bench(float ele = 3900f, float off = -30f) => new CampSpot(ele, 6f, off, float.MaxValue, 4f);
        static CampSpot Mirror(float ele = 4900f) => new CampSpot(ele, 11f, 0f, float.MaxValue, 6f);
        static CampSpot AtHut(float ele, float off = 0f) => new CampSpot(ele, 5f, off, 20f, 5f);

        // ── where a tent may go ───────────────────────────────────────────────────────────────────────────

        [Test]
        public void ATentGoesOnGentleGroundAndSlidesOffAnythingSteeper()
        {
            Assert.AreEqual(CampVerdict.Ok, Camp.Check(new CampSpot(3900f, Camp.MaxSlopeDeg - .5f, -30f, float.MaxValue, 4f)));
            Assert.AreEqual(CampVerdict.TooSteep, Camp.Check(new CampSpot(3900f, Camp.MaxSlopeDeg + .5f, -30f, float.MaxValue, 4f)));
            // the sign of the slope never matters: downhill is as steep as uphill
            Assert.AreEqual(CampVerdict.TooSteep, Camp.Check(new CampSpot(3900f, -24f, -30f, float.MaxValue, 4f)));
            StringAssert.Contains("15", Camp.Why(CampVerdict.TooSteep));
        }

        [Test]
        public void NobodyCampsOnTheBridgesRightOfTheSnowCatLane()
        {
            // 3 700–4 050 m, right of the lane: the same ground AscentRoute charges for walking across
            Assert.Greater(AscentRoute.CrevassePerMetre(3900f, 30f, 7, false), 0f, "предпосылка правила");
            Assert.AreEqual(CampVerdict.Crevasse, Camp.Check(Bench(3900f, 30f)));
            // left of it there is nothing to fall into, and that is the rule the player has to learn
            Assert.AreEqual(CampVerdict.Ok, Camp.Check(Bench(3900f, -30f)));
            // the lane itself is the lane
            Assert.AreEqual(CampVerdict.Ok, Camp.Check(Bench(3900f, AscentRoute.LaneHalfWidthM - 1f)));
            // and above the field the glacier is closed again
            Assert.AreEqual(CampVerdict.Ok, Camp.Check(Bench(AscentRoute.CrevasseToEle + 50f, 30f)));
            // the barrels and the huts of the moraine stand inside the same band, and people sleep on them
            Assert.AreEqual(CampVerdict.Ok, Camp.Check(AtHut(3900f, 30f)), "готовая площадка — доказательство, что мостов под ней нет");
        }

        [Test]
        public void AbovePastukhovRocksATentNeedsAPlatformOrAnIceAxe
            ()
        {
            Assert.IsFalse(Camp.NeedsPlatform(Ascent.GearGateEle), "на скалах ещё фирн");
            Assert.IsTrue(Camp.NeedsPlatform(Ascent.GearGateEle + 1f), "выше — «зеркало»");

            Assert.AreEqual(CampVerdict.BareIce, Camp.Check(Mirror()), "на голом льду без ледоруба площадку не вырубить");
            Assert.AreEqual(CampVerdict.Ok, Camp.Check(Mirror(), hasTent: true, hasIceAxe: true));
            Assert.AreEqual(CampVerdict.Ok, Camp.Check(AtHut(AscentRoute.SaddleHutEle)), "у хижины на седловине площадка уже есть");

            // and cutting one is real work: the pitching takes four minutes more than it does on the moraine
            Assert.AreEqual(Camp.PitchSeconds, Camp.SecondsToPitch(Bench()), 1e-3);
            Assert.AreEqual(Camp.PitchSeconds + Camp.PlatformSeconds, Camp.SecondsToPitch(Mirror()), 1e-3);
            Assert.AreEqual(Camp.PitchSeconds, Camp.SecondsToPitch(AtHut(AscentRoute.SaddleHutEle)), 1e-3);
        }

        [Test]
        public void AGaleTakesTheFlyBeforeThePolesAreUp()
        {
            Assert.AreEqual(CampVerdict.Gale, Camp.Check(new CampSpot(3900f, 5f, -30f, float.MaxValue, Camp.MaxPitchWindMs)));
            Assert.AreEqual(CampVerdict.Ok, Camp.Check(new CampSpot(3900f, 5f, -30f, float.MaxValue, Camp.MaxPitchWindMs - .1f)));
            Assert.AreEqual(AscentCold.WalkVeryHardMs, Camp.MaxPitchWindMs, 1e-4, "порог — 8 баллов Бофорта");
        }

        [Test]
        public void ATentYouDoNotCarryAndAPlaceAlreadyTakenAreRefusedFirst()
        {
            Assert.AreEqual(CampVerdict.NoTent, Camp.Check(Bench(), hasTent: false));
            Assert.AreEqual(CampVerdict.Taken, Camp.Check(Bench(), nearestCampM: Camp.SpacingM - 1f));
            Assert.AreEqual(CampVerdict.Ok, Camp.Check(Bench(), nearestCampM: Camp.SpacingM + 1f));
            // and the night of 1–2 February is one night: it is not camped through
            Assert.AreEqual(CampVerdict.NotHere, Camp.Check(Bench(), place: Place.Kholat));
            Assert.IsTrue(Camp.AllowedIn(Place.Elbrus));
            Assert.IsFalse(Camp.AllowedIn(Place.Kholat));
            foreach (CampVerdict v in Enum.GetValues(typeof(CampVerdict)))
                Assert.AreEqual(v == CampVerdict.Ok, Camp.Why(v).Length == 0, v + ": отказ должен быть объяснён по-русски");
        }

        // ── what a night is worth ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void ClimbHighSleepLowIsExactlyWhatTheCampPays()
        {
            // a day to Pastukhov rocks and back down to the barrels: the classic first sortie
            var c = new Climber { Acclimatisation = .3f, HighestEle = 4650f };
            float gain = Camp.SleepGain(c, 3710f);
            float after = Camp.Sleep(c, 3710f, burner: true);
            Assert.AreEqual(after, c.Acclimatisation, 1e-6, "Sleep возвращает то, с чем просыпаются");
            Assert.AreEqual(gain, after - .3f, 1e-6, "SleepGain обещает ровно то, что даёт Sleep");
            Assert.AreEqual(.3f + Ascent.TouchHighGain + Ascent.SleepGain(3710f), after, 1e-5,
                "выход на 4650 с ночёвкой на 3710 — +0.20 за выход и капля за саму ночёвку");

            // and the next sortie starts from the tent, not from yesterday's high point
            Assert.AreEqual(3710f, c.HighestEle, 1e-3);

            // a night at the same height as the day's high point pays only what the height itself is worth
            var flat = new Climber { Acclimatisation = .3f, HighestEle = 4100f };
            Camp.Sleep(flat, 4100f, burner: true);
            Assert.AreEqual(.3f + Ascent.SleepGain(4100f), flat.Acclimatisation, 1e-5, "без сброса — только сама ночёвка");

            // a day that gained nothing at all costs the body its usual 0.05
            var idle = new Climber { Acclimatisation = .6f, HighestEle = 2350f };
            Camp.Sleep(idle, 2350f, burner: true);
            Assert.AreEqual(.6f - Ascent.DecayPerDay, idle.Acclimatisation, 1e-5);
        }

        [Test]
        public void TwoNightsInCampAreMeasurablyStrongerThanNone()
        {
            var c = new Climber { Acclimatisation = .3f, HighestEle = 4050f };
            Camp.Sleep(c, 3710f, burner: true);                       // выход на 4050, ночёвка на бочках
            c.HighestEle = 4800f;                                     // второй день — выше скал Пастухова
            Camp.Sleep(c, 4100f, burner: true);                       // ночёвка в приюте 88
            Assert.Greater(c.Acclimatisation, .55f, "две ночёвки по правилу — это уже не турист с канатки");
            Assert.Greater(Ascent.Hypoxia(5300f, c.Acclimatisation), Ascent.Hypoxia(5300f, .3f),
                "и на косой полке это слышно в темпе");
            Assert.Greater(Ascent.ToleratedEle(c.Acclimatisation), Ascent.ToleratedEle(.3f));
        }

        [Test]
        public void ANightMendsWhatANightCanMendAndNothingElse()
        {
            var c = new Climber
            {
                Acclimatisation = .5f, HighestEle = 5100f, SicknessLoad = 6f,
                Hands = .2f, Feet = .8f, Face = .34f, Drowsiness = .9f, Pulse = .7f, Blindness = .6f,
                Dehydration = .5f, ThermosSips = 0, StopSeconds = 9f,
            };
            Camp.Sleep(c, 4100f, burner: true);
            Assert.AreEqual(0f, c.Hands, 1e-6, "лёгкое обморожение отходит в спальнике");
            Assert.AreEqual(0f, c.Face, 1e-6);
            Assert.AreEqual(.8f, c.Feet, 1e-6, "настоящее обморожение ночь не лечит");
            Assert.AreEqual(0f, c.Drowsiness, 1e-6);
            Assert.AreEqual(0f, c.Pulse, 1e-6);
            Assert.AreEqual(0f, c.StopSeconds, 1e-6);
            Assert.AreEqual(.1f, c.Blindness, 1e-5, "глаза восстанавливаются двое суток");
            Assert.AreEqual(0f, c.Dehydration, 1e-6, "горелка топит снег");
            Assert.AreEqual(AscentRoute.ThermosSips, c.ThermosSips, "и наливает термос");
            Assert.Less(c.SicknessLoad, 6f, "на 4100 при 0.5 акклиматизации высота переваривается");
        }

        [Test]
        public void ANightWithoutABurnerIsANightOfDrying()
        {
            var wet = new Climber { Acclimatisation = .5f, HighestEle = 4600f, Dehydration = .3f, ThermosSips = 1 };
            var dry = new Climber { Acclimatisation = .5f, HighestEle = 4600f, Dehydration = .3f, ThermosSips = 1 };
            Camp.Sleep(wet, 4100f, burner: true);
            Camp.Sleep(dry, 4100f, burner: false);
            Assert.AreEqual(0f, wet.Dehydration, 1e-6);
            Assert.AreEqual(.3f + Camp.DryNightGain, dry.Dehydration, 1e-5, "снег сам себя не растопит");
            Assert.AreEqual(1, dry.ThermosSips, "и термос остался пустым");
            Assert.AreEqual(wet.Acclimatisation, dry.Acclimatisation, 1e-6, "воду высота не считает — считает высоту");
        }

        [Test]
        public void HighCampsDoNotAdaptAtAll()
        {
            // above 5 000 m the body only spends reserve: a night on the saddle costs, it does not buy
            var c = new Climber { Acclimatisation = .8f, HighestEle = 5416f, SicknessLoad = 2f };
            Camp.Sleep(c, AscentRoute.SaddleHutEle, burner: true);
            Assert.Greater(c.SicknessLoad, 2f, "на 5300 ночь только добавляет недопереваренной высоты");
            Assert.LessOrEqual(c.Acclimatisation, .8f + Ascent.SleepGain(AscentRoute.SaddleHutEle) + 1e-6);
            Assert.AreEqual(0f, Ascent.SleepGain(AscentRoute.SaddleHutEle), 1e-6, "спать на седловине не акклиматизирует");
        }

        // ── the name of the place ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void TheCampKnowsWhereItStands()
        {
            var shelf = Elbrus.Get("shelf");
            Assert.AreEqual("косая полка", Camp.StageAt(shelf.X, shelf.Z));
            StringAssert.Contains("5290", Camp.Where(shelf.X, shelf.Z, 5290f));
            StringAssert.Contains("м", Camp.Where(shelf.X, shelf.Z, 5290f));

            var priut = Elbrus.Get("priut11");
            Assert.AreEqual("Приют 11", Camp.StageAt(priut.X, priut.Z), "скобки и высота в подпись не идут");
            Assert.AreEqual("Бочки", Camp.StageAt(Elbrus.Barrels.X, Elbrus.Barrels.Z));

            // a point on the route between two POIs is named by the stage it is on
            var (x, z) = Elbrus.PointAt(Elbrus.SummitRoute, 4700f);
            Assert.AreEqual("косая полка", Camp.StageAt(x, z));

            // and a point far off both is just the slope
            Assert.AreEqual("южный склон", Camp.StageAt(Elbrus.Half - 200f, -Elbrus.Half + 200f));
        }

        // ── and where that actually is, on our own height field ───────────────────────────────────────────

        static CampSpot Measured(float x, float z, float platformM = float.MaxValue)
        {
            var dem = ElbrusData.Dem;
            float ele = dem.Sample(x, z);
            var (_, off) = Elbrus.Nearest(Elbrus.SummitRoute, x, z);
            return new CampSpot(ele, dem.Fall(x, z, 8f).slopeDeg, off, platformM, 4f);
        }

        [Test]
        public void ThePlacesAPartyWouldStopAreCampableAndTheOpenSlopeIsNot()
        {
            // a rule nobody can obey is not a rule: these are the spots the mountain itself offers, measured on the
            // height field the runtime uses, with the slope span the runtime measures at
            var start = Elbrus.Start;
            Assert.AreEqual(CampVerdict.Ok, Camp.Check(Measured(start.x, start.z, 30f)), "поляна Азау — ровное место");

            var top = Elbrus.Garabashi;
            Assert.AreEqual(CampVerdict.Ok, Camp.Check(Measured(top.X, top.Z, 10f)), "Гара-Баши, 3847");
            var barrels = Elbrus.Barrels;
            Assert.AreEqual(CampVerdict.Ok, Camp.Check(Measured(barrels.X, barrels.Z, 10f)), "бочки, 3710");
            var saddle = Elbrus.Saddle;
            Assert.AreEqual(CampVerdict.Ok, Camp.Check(Measured(saddle.X, saddle.Z, 20f)), "седловина у хижины Red Fox");

            // the barrels lie well right of the summit line, inside the crevasse band: without the platform rule the
            // base camp of the whole mountain would be refused
            var (_, off) = Elbrus.Nearest(Elbrus.SummitRoute, barrels.X, barrels.Z);
            Assert.Greater(off, AscentRoute.LaneHalfWidthM, "бочки лежат правее линии маршрута");
            Assert.AreEqual(CampVerdict.Crevasse, Camp.Check(Measured(barrels.X, barrels.Z)),
                "и без готовой площадки это была бы трещинная зона");

            // and the steep part of the route is not a bedroom: the «зеркало» and the rise onto the shelf
            foreach (float arc in new[] { 3300f, 3900f, 4500f })
            {
                var (x, z) = Elbrus.PointAt(Elbrus.SummitRoute, arc);
                Assert.AreEqual(CampVerdict.TooSteep, Camp.Check(Measured(x, z), hasTent: true, hasIceAxe: true),
                    $"на {arc:0} м маршрута слишком круто");
            }
        }

        // ── the file ──────────────────────────────────────────────────────────────────────────────────────

        static SaveGame Sample()
        {
            var save = new SaveGame
            {
                Place = Place.Elbrus,
                SavedUtc = new DateTime(2026, 9, 18, 5, 40, 12, DateTimeKind.Utc),
                Elapsed = 2190.5f,
                Storm = true,
                FreshSnowCm = 3.25f,
                CampX = 1234.5f, CampY = 5290.25f, CampZ = -678f, CampYaw = 137.5f, CampEle = 5290.25f,
                Burner = true,
                Where = "косая полка, 5290 м",
            };
            var don = save.Ensure("Дон", "Дон");
            don.Acclim = .52f; don.Highest = 5290.25f; don.Sickness = 3.4f;
            don.Hands = .12f; don.Feet = .04f; don.Blind = .02f; don.Dry = .31f; don.Pulse = .1f;
            don.Sips = 3; don.Crampons = true; don.Roubles = 3100; don.Strength = .62f;
            don.Heat = 78f; don.HandsBar = 66f; don.Clarity = 91f;
            foreach (var h in Rental.Board()) don.Pack.Add(new ItemStack(h.Item));
            don.Pack.Add(new ItemStack(ItemId.Matches, 7, 40));
            don.Hand = new ItemStack(ItemId.Tent);

            var friend = save.Ensure("гость-3f9a1c", "Путник");
            friend.Acclim = .3f; friend.Highest = 4650f; friend.Roubles = 7000;
            friend.Pack.Add(new ItemStack(ItemId.Burner));
            return save;
        }

        [Test]
        public void ASaveRoundTripsThroughItsOwnJson()
        {
            var save = Sample();
            string text = save.Text;
            StringAssert.Contains("\"schema\": 1", text);
            StringAssert.Contains("косая полка", text);
            StringAssert.Contains("{\"id\": \"Crampons\"}", text);
            StringAssert.Contains("2026-09-18T05:40:12Z", text);

            var back = SaveGame.Parse(text);
            Assert.IsNotNull(back);
            Assert.AreEqual(Place.Elbrus, back.Place);
            Assert.AreEqual(save.Elapsed, back.Elapsed, .01f);
            Assert.AreEqual(save.SavedUtc.Ticks, back.SavedUtc.Ticks, "время сейва круглое до секунды");
            Assert.IsTrue(back.Storm);
            Assert.AreEqual(save.FreshSnowCm, back.FreshSnowCm, .01f);
            Assert.AreEqual(save.CampX, back.CampX, .01f);
            Assert.AreEqual(save.CampEle, back.CampEle, .01f);
            Assert.IsTrue(back.Burner);
            Assert.AreEqual(2, back.Climbers.Count);

            var don = back.Find("Дон");
            Assert.IsNotNull(don, "игрока ищут по ключу, а ключ — это имя из меню");
            Assert.AreEqual(.52f, don.Acclim, 1e-4);
            Assert.AreEqual(3, don.Sips);
            Assert.IsTrue(don.Crampons);
            Assert.AreEqual(3100, don.Roubles);
            Assert.AreEqual(save.Find("Дон").Pack.Count, don.Pack.Count);
            Assert.AreEqual(ItemId.Tent, don.Hand.Id);
            Assert.AreEqual(Ascent.Required, don.Kit, "прокат в сейве — это просто предметы в рюкзаке");
            var matches = don.Pack[don.Pack.Count - 1];
            Assert.AreEqual(ItemId.Matches, matches.Id);
            Assert.AreEqual(7, matches.Amount);
            Assert.AreEqual(40, matches.Wet);

            var friend = back.Find("гость-3f9a1c");
            Assert.IsNotNull(friend, "второй игрок хранится отдельно, со своей акклиматизацией и своим рюкзаком");
            Assert.AreEqual(.3f, friend.Acclim, 1e-4);
            Assert.AreNotEqual(don.Acclim, friend.Acclim);
            Assert.AreEqual(1, friend.Pack.Count);

            // and the same state written twice is the same file, byte for byte
            Assert.AreEqual(text, back.Text);
        }

        [Test]
        public void AnOldSaveLoadsAndANewerOneIsRefusedInsteadOfHalfRead()
        {
            // a file that predates half the fields: everything missing takes today's default and nothing throws
            var thin = SaveGame.Parse("{\"schema\":1,\"place\":\"elbrus\",\"climbers\":[{\"key\":\"Дон\"}]}");
            Assert.IsNotNull(thin);
            Assert.AreEqual(1, thin.Climbers.Count);
            Assert.AreEqual(.3f, thin.Climbers[0].Acclim, 1e-6, "нет поля — значит турист с канатки");
            Assert.AreEqual(Wallet.StartRoubles, thin.Climbers[0].Roubles);
            Assert.AreEqual(0, thin.Climbers[0].Pack.Count);
            Assert.AreEqual(AscentRoute.ThermosSips, thin.Climbers[0].Sips);

            // an unknown item is dropped, not guessed at
            var future = SaveGame.Parse("{\"schema\":1,\"climbers\":[{\"key\":\"Дон\",\"pack\":[{\"id\":\"Jetpack\"},{\"id\":\"Tent\"}]}]}");
            Assert.IsNotNull(future);
            Assert.AreEqual(1, future.Climbers[0].Pack.Count);
            Assert.AreEqual(ItemId.Tent, future.Climbers[0].Pack[0].Id);

            // a file from a newer build is refused whole: the menu simply will not offer it
            Assert.IsNull(SaveGame.Parse("{\"schema\":" + (SaveGame.Schema + 1) + ",\"place\":\"elbrus\"}"));
            Assert.IsNull(SaveGame.Parse("{\"place\":\"elbrus\"}"), "без версии схемы это не наш файл");
        }

        [Test]
        public void ABrokenSaveIsNeverACrash()
        {
            Assert.IsNull(SaveGame.Parse(""));
            Assert.IsNull(SaveGame.Parse("не json вовсе"));
            Assert.IsNull(SaveGame.Parse("{\"schema\":1,"));
            Assert.IsNull(SaveGame.Parse("[1,2,3]"));
            Assert.IsNull(SaveGame.Parse(null));
            Assert.DoesNotThrow(() => SaveGame.Parse("{\"schema\":1,\"climbers\":\"нет\"}"));
            Assert.DoesNotThrow(() => SaveGame.Parse("{\"schema\":1,\"camp\":7,\"climbers\":[3,{\"key\":\"\"}]}"));
            Assert.AreEqual(0, SaveGame.Parse("{\"schema\":1,\"climbers\":\"нет\"}").Climbers.Count);
        }

        [Test]
        public void JsonWritesNumbersTheSameWayInEveryCountry()
        {
            var o = JsonValue.Object().Set("a", 5290.25).Set("b", 12).Set("s", "кавычка \" и \\ и перевод\nстроки");
            string text = o.ToJson();
            StringAssert.Contains("\"a\":5290.25", text);
            StringAssert.Contains("\"b\":12", text);
            var back = JsonValue.Parse(text);
            Assert.AreEqual(5290.25, back.Num("a"), 1e-9);
            Assert.AreEqual(12, back.Int("b"));
            Assert.AreEqual("кавычка \" и \\ и перевод\nстроки", back.Str("s"));
            // a missing or wrong-typed field is a fallback, never an exception
            Assert.AreEqual(7, back.Int("нет", 7));
            Assert.AreEqual("", back.Str("a"));
            Assert.IsTrue(back["нет"].IsNull);
            Assert.AreEqual(0, back["нет"].Count);
        }

        // ── the slots on disk ─────────────────────────────────────────────────────────────────────────────

        static string TempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "1079-saves-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Test]
        public void SlotsAreOnePerPlaceAndDateAndTheNewestIsWhatContinueTakes()
        {
            string dir = TempDir();
            try
            {
                var newest = default(SaveSlot);
                for (int i = 0; i < SaveStore.KeepPerPlace + 3; i++)
                {
                    var save = Sample();
                    save.SavedUtc = new DateTime(2026, 9, 10, 6, 0, 0, DateTimeKind.Utc).AddDays(i);
                    save.Where = "этап " + i;
                    Assert.IsNotNull(SaveStore.Write(dir, save), "сейв должен лечь на диск");
                }
                var list = SaveStore.List(dir, Place.Elbrus);
                Assert.AreEqual(SaveStore.KeepPerPlace, list.Count, "старые слоты подчищаются");
                Assert.AreEqual("этап " + (SaveStore.KeepPerPlace + 2), list[0].Where, "первым идёт самый свежий");
                Assert.Greater(list[0].SavedUtc.Ticks, list[1].SavedUtc.Ticks);

                newest = SaveStore.Newest(dir, Place.Elbrus);
                Assert.AreEqual(list[0].Path, newest.Path);
                Assert.IsFalse(newest.IsEmpty);
                StringAssert.Contains("elbrus-", Path.GetFileName(newest.Path));
                StringAssert.Contains(".json", newest.Path);

                // the other place has nothing, and asking is not an error
                Assert.AreEqual(0, SaveStore.List(dir, Place.Kholat).Count);
                Assert.IsTrue(SaveStore.Newest(dir, Place.Kholat).IsEmpty);

                var read = SaveStore.Read(newest.Path);
                Assert.IsNotNull(read);
                Assert.AreEqual(2, read.Climbers.Count);

                // no half-written file is ever left under a name the menu reads
                Assert.AreEqual(0, Directory.GetFiles(dir, "*" + SaveStore.Temp).Length);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void AFolderOfRubbishDoesNotTakeTheMenuDown()
        {
            string dir = TempDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, "elbrus-20260101-000000.json"), "не json");
                File.WriteAllText(Path.Combine(dir, "elbrus-20260102-000000.json"),
                    "{\"schema\":" + (SaveGame.Schema + 5) + "}");
                var good = Sample();
                good.SavedUtc = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);
                SaveStore.Write(dir, good);

                var list = SaveStore.List(dir, Place.Elbrus);
                Assert.AreEqual(1, list.Count, "нечитаемые файлы просто не показываются");
                Assert.AreEqual(good.SavedUtc.Ticks, list[0].SavedUtc.Ticks);

                // and a directory that is not there at all is an empty list, not an exception
                Assert.AreEqual(0, SaveStore.List(Path.Combine(dir, "нет-такой"), Place.Elbrus).Count);
                Assert.IsNull(SaveStore.Read(Path.Combine(dir, "нет-такого.json")));
                Assert.IsNull(SaveStore.Write(null, good));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void TheLineUnderContinueSaysWhereAndWhen()
        {
            var save = Sample();
            string dir = TempDir();
            try
            {
                SaveStore.Write(dir, save);
                var slot = SaveStore.Newest(dir, Place.Elbrus);
                var local = slot.SavedUtc.ToLocalTime();
                string line = slot.Line(local);
                StringAssert.Contains("Эльбрус", line);
                StringAssert.Contains("косая полка, 5290 м", line);
                StringAssert.Contains(AscentRoute.Clock(save.Hour), line);
                StringAssert.Contains("сегодня", line);
                StringAssert.Contains("Дон", slot.Party);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void WhenReadsAsAPersonWouldSayIt()
        {
            var now = new DateTime(2026, 9, 18, 21, 5, 0);
            Assert.AreEqual("сегодня", SaveStore.When(now.AddHours(-3), now));
            Assert.AreEqual("вчера", SaveStore.When(now.AddDays(-1), now));
            Assert.AreEqual("позавчера", SaveStore.When(now.AddDays(-2), now));
            Assert.AreEqual("12 сентября", SaveStore.When(now.AddDays(-6), now));
            Assert.AreEqual("18 сентября 2025", SaveStore.When(now.AddYears(-1), now));
        }

        // ── who a save belongs to ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void TheKeyIsTheNameFromTheMenuAndTheFallbackIsStable()
        {
            Assert.AreEqual("Дон", SaveKeys.For("  Дон  ", "гость-1"));
            Assert.AreEqual("Дон Маркин", SaveKeys.For("Дон   Маркин", "гость-1"));
            // an empty field, or the name the menu starts with, is not a name: two strangers would both carry it
            Assert.AreEqual("гость-1", SaveKeys.For("", "гость-1"));
            Assert.AreEqual("гость-1", SaveKeys.For("   ", "гость-1"));
            Assert.AreEqual("гость-1", SaveKeys.For(SaveKeys.DefaultName, "гость-1"));
            Assert.AreEqual("гость-1", SaveKeys.For("путник", "гость-1"));
            Assert.AreEqual(SaveKeys.DefaultName, SaveKeys.For(null, null), "без запасного ключа хотя бы не пусто");
            Assert.AreEqual("гость-3f9a1c", SaveKeys.Guest(0x3f9a1c));
            Assert.AreEqual(SaveKeys.Guest(0x123f9a1c), SaveKeys.Guest(0x3f9a1c), "ключ — шесть шестнадцатеричных цифр");
        }

        // ── the rucksack a camp needs ─────────────────────────────────────────────────────────────────────

        [Test]
        public void TheElbrusRucksackCarriesTheTentTheBurnerAndTheWholeHireKit()
        {
            // the catalogue is indexed by the enum, so a new item appended in one place and not the other would hand
            // out a tent with the weight of a space blanket
            for (int i = 0; i < Items.Count; i++)
                Assert.AreEqual((ItemId)i, Items.Spec((ItemId)i).Id, "каталог предметов сдвинулся относительно ItemId");
            Assert.AreEqual("Палатка-двойка", Items.Spec(ItemId.Tent).Name);
            Assert.AreEqual("Горелка с газом", Items.Spec(ItemId.Burner).Name);

            var pack = new Backpack(1);
            foreach (var s in Items.StarterFor(0, Place.Elbrus)) Assert.IsTrue(pack.Put(s), s.Describe() + " — не влезло");
            Assert.IsTrue(pack.IndexOf(ItemId.Tent) >= 0, "двойка лежит в рюкзаке с самого низа");
            Assert.IsTrue(pack.IndexOf(ItemId.Burner) >= 0);
            Assert.AreEqual(-1, pack.IndexOf(ItemId.Axe), "топор — это 1959 год, а не Азау");

            foreach (var h in Rental.Board()) Assert.IsTrue(pack.Put(new ItemStack(h.Item)), h.Line + " — не влезло");
            Assert.LessOrEqual(pack.Litres, Backpack.CapacityLitres, "палатка, горелка и прокат должны уместиться в 50 л");
            Assert.Greater(pack.Litres, Backpack.CapacityLitres * .7f, "и рюкзак при этом действительно полон");
            Assert.AreEqual(Ascent.Required, Rental.GearOf(pack.Contents), "комплект на месте");
            Assert.AreEqual(Gear.None, Rental.PieceOf(ItemId.Tent), "палатку на скалах Пастухова не проверяют");
            Assert.AreEqual(Gear.None, Rental.PieceOf(ItemId.Burner));

            // the Kholat kit is untouched by all this
            Assert.IsTrue(Items.StarterFor(0, Place.Kholat).Exists(s => s.Id == ItemId.Axe));
            Assert.IsFalse(Items.StarterFor(0, Place.Kholat).Exists(s => s.Id == ItemId.Tent));
        }
    }
}
