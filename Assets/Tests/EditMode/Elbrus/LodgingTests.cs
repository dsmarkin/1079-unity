using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    /// <summary>A night under a roof against a night in your own двойка. What is checked here is that the hut gives
    /// the body more back than the tent does and takes money for it, that the вагончики of the rescuers take none,
    /// and — the one that matters for the design — that the acclimatisation is <em>not</em> a second copy of climb
    /// high, sleep low but the same <see cref="Ascent.Acclimatise"/> the tent has always used.</summary>
    public class LodgingTests
    {
        static Climber Fresh(float highest = 4800f) => new Climber { Acclimatisation = .4f, HighestEle = highest };
        static Bunk Barrels => Lodging.Get("barrels");
        static Bunk Priut => Lodging.Get("priut11");
        static Bunk Capsule => Lodging.Get("leaprus");
        static Bunk Wagon => Lodging.Get("rescuers");

        // ── the board ─────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void EveryBunkOnTheSlopeIsOnTheBoardAndStandsWhereTheMapPutsIt()
        {
            Assert.AreEqual(6, Lodging.Count);
            Assert.AreEqual(3710f, Barrels.Ele, 1e-6, "бочки — 3710");
            Assert.AreEqual(3912f, Capsule.Ele, 1e-6, "LeapRus — 3912");
            Assert.AreEqual(4050f, Priut.Ele, 1e-6, "Приют 11 / Дизель-хат — 4050");
            Assert.AreEqual(3900f, Lodging.Get("natspark").Ele, 1e-6, "«Нацпарк» — 3900");
            Assert.IsTrue(Wagon.Ele >= 4100f && Wagon.Ele <= 4200f, "вагончики спасателей — 4100–4200");

            // the ids that have a POI stand at the same place on the map
            Assert.AreEqual(Elbrus.Barrels.Ele, Barrels.Ele, 1f);
            Assert.AreEqual(Elbrus.Get("leaprus").Ele, Capsule.Ele, 1f);
            Assert.AreEqual(Elbrus.Priut.Ele, Priut.Ele, 1f);

            Assert.IsTrue(Lodging.Get("нет такого").IsEmpty);
            StringAssert.Contains("Бочки", Lodging.BoardText());
        }

        [Test]
        public void TheRescuersWagonIsFreeAndTheCapsuleIsTheExpensiveOne()
        {
            Assert.IsTrue(Wagon.Free, "у спасателей за койку не берут");
            Assert.AreEqual(0, Wagon.Roubles);
            StringAssert.Contains("бесплатно", Wagon.Line);
            Assert.AreEqual(Wagon.Id, Lodging.Cheapest().Id);

            foreach (var b in Lodging.Board())
                if (b.Kind != BunkKind.RescueWagon) Assert.Greater(b.Roubles, 0, b.Name + " — платная");

            Assert.Greater(Capsule.Roubles, Priut.Roubles, "капсульный отель дороже приюта");
            Assert.Greater(Priut.Roubles, Lodging.Get("natspark").Roubles);

            // and the purse is the one the hire counter already spends from
            var w = Wallet.Start();
            Assert.IsTrue(Lodging.Book(ref w, Capsule));
            Assert.AreEqual(Wallet.StartRoubles - Capsule.Roubles, w.Roubles);
            Assert.IsFalse(Lodging.Book(ref w, Capsule), "на вторую ночь в капсуле уже нет");
            Assert.AreEqual(Wallet.StartRoubles - Capsule.Roubles, w.Roubles, "неудачная бронь денег не берёт");

            var empty = new Wallet { Roubles = 0 };
            Assert.IsTrue(Lodging.Book(ref empty, Wagon), "к спасателям пускают и без денег");
            Assert.AreEqual(0, empty.Roubles);

            // the whole hire kit plus one night in the barrels still fits into what a visitor comes up with
            Assert.Less(Rental.SetRoubles + Barrels.Roubles, Wallet.StartRoubles, "комплект и ночёвка — по карману");
            Assert.Greater(Rental.SetRoubles + Capsule.Roubles, Wallet.StartRoubles, "комплект и капсула — уже нет");
        }

        // ── what a night is worth ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void ANightInAHutRestoresBetterThanANightInATent()
        {
            Assert.AreEqual(Camp.ThawBelow, Lodging.ThawBelow(Lodging.Tent), 1e-6, "палатка — та же оттайка, что и была");
            Assert.Greater(Lodging.ThawBelow(Barrels), Lodging.ThawBelow(Lodging.Tent), "в тепле обморожение отходит сильнее");
            Assert.Greater(Lodging.ThawBelow(Capsule), Lodging.ThawBelow(Barrels));
            Assert.Less(Lodging.ThawBelow(Capsule), 1f, "настоящее обморожение ночь не лечит нигде");

            Assert.Greater(Lodging.RestFactor(Barrels), Lodging.RestFactor(Lodging.Tent), "силы возвращаются лучше");
            Assert.Greater(Lodging.RestFactor(Capsule), Lodging.RestFactor(Wagon));
            Assert.AreEqual(1f, Lodging.RestFactor(Capsule), 1e-6, "в капсуле отсыпаются полностью");

            // the same frostbite, the same height, the same night: the tent keeps it and the barrel does not
            var inTent = Fresh(); inTent.Hands = .5f; inTent.Feet = .5f;
            Camp.Sleep(inTent, Barrels.Ele, burner: true);
            Assert.AreEqual(.5f, inTent.Hands, 1e-6, "0,5 — это выше порога палатки, в спальнике не отходит");

            var inBarrel = Fresh(); inBarrel.Hands = .5f; inBarrel.Feet = .5f;
            var night = Lodging.Sleep(inBarrel, Barrels);
            Assert.AreEqual(0f, inBarrel.Hands, 1e-6, "в бочке за ночь отошли");
            Assert.AreEqual(0f, inBarrel.Feet, 1e-6);
            Assert.Greater(night.Rest, Lodging.RestFactor(Lodging.Tent));
            Assert.IsTrue(night.ThermosFilled);
            Assert.AreEqual(Barrels.Roubles, night.Roubles);

            // and the three bars of the night come back to what a warm room leaves
            var p = new Participant { Heat = 20f, Hands = 30f, Clarity = 40f };
            Lodging.Warm(p, Barrels);
            Assert.AreEqual(100f * Lodging.RestFactor(Barrels), p.Heat, 1e-3);
            Assert.Greater(p.Heat, 100f * Lodging.RestFactor(Lodging.Tent), "в бочке теплее, чем в палатке");

            var warm = new Participant { Heat = 100f };
            Lodging.Warm(warm, Wagon);
            Assert.AreEqual(100f, warm.Heat, 1e-6, "тёплого ночёвка не остужает");
        }

        [Test]
        public void TheAcclimatisationIsTheSameRuleTheTentUses()
        {
            // one climber sleeps in the barrels, the other in a tent pitched beside them with a burner in it.
            // Climb high, sleep low does not care which, and that is the point: Lodging does not own that rule.
            var inBarrel = Fresh();
            var inTent = Fresh();
            Lodging.Sleep(inBarrel, Barrels);
            Camp.Sleep(inTent, Barrels.Ele, burner: true);
            Assert.AreEqual(inTent.Acclimatisation, inBarrel.Acclimatisation, 1e-6, "койка не акклиматизирует лучше палатки");
            Assert.AreEqual(inTent.SicknessLoad, inBarrel.SicknessLoad, 1e-6);
            Assert.AreEqual(Barrels.Ele, inBarrel.HighestEle, 1e-6, "следующий выход считается от приюта");

            // and it really is climb high, sleep low: a day to 4 800 paid for by a night 1 100 m lower
            Assert.Greater(inBarrel.Acclimatisation, .4f, "выход на 4800 с ночёвкой на 3710 — плюс");
            Assert.AreEqual(Ascent.TouchHighGain + Ascent.SleepGain(Barrels.Ele),
                inBarrel.Acclimatisation - .4f, 1e-5, "ровно то, что даёт Ascent.Acclimatise");

            // a night with nothing above it still pays only what the curve pays
            var idle = new Climber { Acclimatisation = .4f, HighestEle = Priut.Ele };
            Lodging.Sleep(idle, Priut);
            Assert.AreEqual(Ascent.SleepGain(Priut.Ele), idle.Acclimatisation - .4f, 1e-5);

            // the preview line agrees with what the night actually does
            var peek = Fresh();
            float promised = Lodging.SleepGain(peek, Barrels);
            var after = Lodging.Sleep(peek, Barrels);
            Assert.AreEqual(promised, after.Gain, 1e-5);
        }

        [Test]
        public void TheDryingRoomDriesWhatTheSnowDoesNot()
        {
            Assert.IsTrue(Priut.Dries, "в приюте сушилка");
            Assert.IsTrue(Wagon.Dries, "в вагончике дизель");
            Assert.IsFalse(Lodging.Tent.Dries, "в палатке ничего не сохнет");

            var pack = new Backpack(1);
            pack.Put(new ItemStack(ItemId.Matches, 8, 70));
            pack.Put(new ItemStack(ItemId.Socks));
            Assert.IsTrue(pack.Contents[0].Damp, "мокрые спички не зажигаются");

            Assert.AreEqual(0, Lodging.Dry(pack, Lodging.Tent));
            Assert.IsTrue(pack.Contents[0].Damp, "палатка их не высушила");

            Assert.AreEqual(1, Lodging.Dry(pack, Priut));
            Assert.AreEqual(0, pack.Contents[0].Wet);
            Assert.IsFalse(pack.Contents[0].Damp, "утром спички сухие");
            Assert.AreEqual(8, pack.Contents[0].Amount, "сушка ничего не съела");
            Assert.AreEqual(0, Lodging.Dry(pack, Priut), "второй раз сушить нечего");
        }

        [Test]
        public void TheStoveFillsTheThermosAndTheTentRowIsNotAPlace()
        {
            var c = Fresh();
            c.ThermosSips = 0;
            c.Dehydration = .8f;
            var night = Lodging.Sleep(c, Priut);
            Assert.AreEqual(AscentRoute.ThermosSips, c.ThermosSips, "в приюте термос наливают");
            Assert.AreEqual(0f, c.Dehydration, 1e-6, "и воды дают");
            Assert.IsTrue(night.ThermosFilled);
            StringAssert.Contains("приюте", night.Note);

            // the tent row is a line of the price board, not a place: it has no height and cannot be slept in here
            var mine = Fresh();
            Assert.IsTrue(Lodging.Sleep(mine, Lodging.Tent).IsEmpty, "ночь в своей палатке — это Camp.Sleep");
            Assert.AreEqual(.4f, mine.Acclimatisation, 1e-6, "и она ничего не тронула");
            Assert.AreEqual(4800f, mine.HighestEle, 1e-6);
        }

        [Test]
        public void NobodyTakesABunkOnKholatSyakhl()
        {
            Assert.IsTrue(Lodging.AllowedIn(Place.Elbrus));
            Assert.IsFalse(Lodging.AllowedIn(Place.Kholat), "ночь 1–2 февраля — одна ночь");
            Assert.AreEqual(Camp.AllowedIn(Place.Kholat), Lodging.AllowedIn(Place.Kholat), "то же правило, что у лагеря");

            var c = Fresh();
            c.Hands = .5f;
            Assert.IsTrue(Lodging.Sleep(c, Barrels, place: Place.Kholat).IsEmpty);
            Assert.AreEqual(.4f, c.Acclimatisation, 1e-6, "и ничего не изменила");
            Assert.AreEqual(.5f, c.Hands, 1e-6);
            Assert.AreEqual(4800f, c.HighestEle, 1e-6);
        }
    }
}
