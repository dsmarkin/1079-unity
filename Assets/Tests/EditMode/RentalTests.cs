using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    /// <summary>The hire counter at the bottom of the mountain, and the clock the ascent runs on. Both exist for one
    /// reason: the gate at Pastukhov rocks has to be a decision a player makes with money and time, not a wall that
    /// appears out of nowhere. So what is pinned down here is that the board covers exactly what the gate checks,
    /// that the whole kit is an expensive but payable choice out of one purse, that it fits in one rucksack next to
    /// the food, and that the turn-round time is reachable inside a run.</summary>
    public class RentalTests
    {
        static Gear AllPieces
        {
            get
            {
                var all = Gear.None;
                foreach (var h in Rental.Board()) all |= h.Piece;
                return all;
            }
        }

        [Test]
        public void TheBoardIsExactlyWhatTheGateChecksFor()
        {
            var board = Rental.Board();
            Assert.AreEqual(Ascent.Required, AllPieces, "в прокате ровно то, что проверяют выше скал Пастухова");
            Assert.AreEqual(board.Length, board.Select(h => h.Piece).Distinct().Count(), "каждая позиция — одна");
            Assert.AreEqual(board.Length, board.Select(h => h.Item).Distinct().Count(), "и один предмет");
            foreach (var h in board)
            {
                Assert.AreNotEqual(ItemId.None, h.Item, h.Piece.ToString());
                Assert.AreEqual(h.Piece, Rental.PieceOf(h.Item));
                Assert.AreEqual(h.Item, Rental.ItemOf(h.Piece));
                Assert.IsTrue(h.Name.Length > 2, h.Piece.ToString());
                Assert.IsTrue(h.Roubles >= 100 && h.Roubles <= 1500, h.Line);
                Assert.Greater(h.Kg, 0f, h.Line);
                Assert.Greater(h.Litres, 0f, h.Line);
            }
            Assert.AreEqual(Gear.None, Rental.PieceOf(ItemId.Rusks), "сухари снаряжением не считаются");
        }

        [Test]
        public void TheWholeKitIsMostOfThePurseButNotAllOfIt()
        {
            int set = Rental.SetRoubles;
            Assert.Less(set, Wallet.StartRoubles, "комплект должен быть подъёмным");
            Assert.Greater(set, Wallet.StartRoubles / 2, "и ощутимой тратой: больше половины кошелька");
            Assert.Less(set, Rental.PieceByPieceRoubles, "комплектом дешевле, чем по одному");
            // and what is left still buys a hot lunch and a couple of drinks on the way
            int left = Wallet.StartRoubles - set;
            Assert.Greater(left, Refreshments.Get(DishId.Shurpa).Roubles + Refreshments.Get(DishId.Tea).Roubles,
                "после проката хватает на обед в кафе");

            Assert.AreEqual(set, Rental.BundleRoubles(Gear.None), 1f, "с пустым рюкзаком комплект стоит ровно столько");
            Assert.AreEqual(0, Rental.BundleRoubles(Ascent.Required), "собранному комплекту нечего добирать");
            var half = Gear.Crampons | Gear.IceAxe | Gear.DownJacket;
            Assert.Less(Rental.BundleRoubles(half), set, "часть комплекта дешевле целого");
            Assert.Less(Rental.BundleRoubles(half), Rental.RestRoubles(half), "и на добор действует та же скидка");
        }

        [Test]
        public void TheKitFitsOneRucksackAndIsFeltOnTheBack()
        {
            var pack = new Backpack(1);
            foreach (var s in Items.StarterFor(0)) Assert.IsTrue(pack.Put(s), "стартовый набор влезает");
            foreach (var h in Rental.Board()) Assert.IsTrue(pack.Put(new ItemStack(h.Item)), h.Line + " — не влезло");
            Assert.LessOrEqual(pack.Litres, Backpack.CapacityLitres, "50 л хватает на еду и на прокат, но впритык");
            Assert.Greater(pack.Litres, Backpack.CapacityLitres * .6f, "и рюкзак при этом действительно полон");

            float bare = Items.StarterFor(0).Sum(s => s.Kg) + Backpack.OwnKg;
            Assert.Greater(pack.Kg - bare, 4f, "комплект весит несколько килограммов");
            Assert.Less(SurvivalRules.LoadSpeedFactor(pack.Kg), SurvivalRules.LoadSpeedFactor(bare), "и это видно по шагу");
        }

        [Test]
        public void HavingTheObjectIsHavingTheGear()
        {
            var packs = new PackWorld();
            var pack = packs.AddPack("a", 0, 0, 0);
            Assert.AreEqual(Gear.None, Rental.Carried(packs, "a"));
            Assert.IsFalse(Ascent.MayPassGate(Ascent.GearGateEle, Rental.Carried(packs, "a")));

            foreach (var h in Rental.Board()) pack.Put(new ItemStack(h.Item));
            var have = Rental.Carried(packs, "a");
            Assert.AreEqual(Ascent.Required, have, "всё в рюкзаке — комплект собран");
            Assert.AreEqual("", Ascent.MissingList(have));
            Assert.IsTrue(Ascent.MayPassGate(Ascent.GearGateEle + 1f, have));

            // an axe carried in the hands counts exactly as much as one in the sack
            var hands = new PackWorld();
            hands.AddPack("b", 0, 0, 0);
            hands.GiveHand("b", new ItemStack(ItemId.IceAxe));
            Assert.AreEqual(Gear.IceAxe, Rental.Carried(hands, "b"));
        }

        [Test]
        public void MissingIsSaidOutLoudInRussian()
        {
            var have = Ascent.Required & ~(Gear.Crampons | Gear.IceAxe);
            string line = Ascent.MissingList(have);
            StringAssert.Contains("кошки", line);
            StringAssert.Contains("ледоруб", line);
            Assert.AreEqual(2, line.Split(',').Length, "ровно две недостающие позиции");
            StringAssert.Contains("каска", Rental.Titles(Gear.Helmet));
            Assert.AreEqual("", Rental.Titles(Gear.None));
        }
    }

    /// <summary>The clock the Elbrus run keeps, and the line of the route the whole world was laid out against.</summary>
    public class AscentDayTests
    {
        static float RunSeconds => World.ElbrusPlan.Profile.Seconds;

        [Test]
        public void TheRunCoversAMorningAndTheTurnaroundIsInsideIt()
        {
            Assert.AreEqual(AscentRoute.RunStartHour, AscentRoute.HourAt(0f, RunSeconds), 1e-4);
            Assert.AreEqual(AscentRoute.RunStartHour + AscentRoute.RunHours, AscentRoute.HourAt(RunSeconds, RunSeconds), 1e-4);

            Assert.IsTrue(AscentRoute.Dark(AscentRoute.RunStartHour), "выходят затемно");
            Assert.IsFalse(AscentRoute.Dark(AscentRoute.HourAt(RunSeconds * .5f, RunSeconds)), "к середине забега светло");

            float toDawn = AscentRoute.SecondsUntil(AscentRoute.DawnHour, 0f, RunSeconds);
            Assert.Greater(toDawn, 0f);
            Assert.Less(toDawn, RunSeconds * .15f, "рассвет — в начале забега, а не в середине");

            float toTurn = AscentRoute.SecondsUntil(AscentRoute.TurnaroundHour, 0f, RunSeconds);
            Assert.Greater(toTurn, RunSeconds * .5f, "контрольное время — поздно");
            Assert.Less(toTurn, RunSeconds, "но до конца забега его успевают пройти");
            Assert.IsTrue(AscentRoute.PastTurnaround(AscentRoute.HourAt(RunSeconds, RunSeconds)));
            Assert.AreEqual(0f, AscentRoute.SecondsUntil(AscentRoute.TurnaroundHour, RunSeconds, RunSeconds), 1e-4);
        }

        [Test]
        public void TheClockReadsAsAClock()
        {
            Assert.AreEqual("05:00", AscentRoute.Clock(5f));
            Assert.AreEqual("13:00", AscentRoute.Clock(AscentRoute.TurnaroundHour));
            Assert.AreEqual("05:30", AscentRoute.Clock(AscentRoute.DawnHour));
            Assert.AreEqual("07:45", AscentRoute.Clock(7.75f));
        }

        [Test]
        public void OffRouteIsPlusToTheRightGoingUp()
        {
            // a straight line running north: east of it is the right hand of somebody walking up it
            var north = new (float x, float z)[] { (0f, 0f), (0f, 100f) };
            var (s, off) = Elbrus.Nearest(north, 10f, 50f);
            Assert.AreEqual(50f, s, .01f);
            Assert.AreEqual(10f, off, .01f, "восточнее линии, идущей на север, — справа");
            (s, off) = Elbrus.Nearest(north, -7f, 20f);
            Assert.AreEqual(-7f, off, .01f, "западнее — слева");

            // and the same on the real route: the summit line runs roughly north, so +x is +offset
            var on = Elbrus.PointAt(Elbrus.SummitRoute, 2000f);
            var (_, zero) = Elbrus.Nearest(Elbrus.SummitRoute, on.x, on.z);
            Assert.AreEqual(0f, zero, 1.5f, "на самой линии отклонения нет");
            var (_, right) = Elbrus.Nearest(Elbrus.SummitRoute, on.x + 30f, on.z);
            var (_, leftSide) = Elbrus.Nearest(Elbrus.SummitRoute, on.x - 30f, on.z);
            Assert.Greater(right, 0f, "вправо по ходу подъёма — плюс");
            Assert.Less(leftSide, 0f, "влево — минус");
            Assert.AreEqual(30f, right, 6f);
        }

        [Test]
        public void TheSignOfTheOffsetIsTheWholeCrevasseRule()
        {
            // the crevasse field of the Garabashi glacier, in the critical band
            const float ele = AscentRoute.CriticalFromEle + 20f;
            Assert.Greater(AscentRoute.CrevasseChance(ele, 30f, 7, false), 0f, "вправо от колеи — можно провалиться");
            Assert.AreEqual(0f, AscentRoute.CrevasseChance(ele, -30f, 7, false), 1e-6, "влево — сколько угодно");
            Assert.AreEqual(0f, AscentRoute.CrevasseChance(ele, 30f, 7, true), 1e-6, "где ручей — трещины нет");
        }

        [Test]
        public void AFallOnTheShelfIsItsOwnOutcome()
        {
            Assert.IsTrue(SurvivalRules.Fell(Outcome.Fall));
            var text = SurvivalRules.Describe(Outcome.Fall);
            Assert.IsTrue(text.Title.Length > 2);
            Assert.IsTrue(text.Note.Length > 40, "исход объясняется словами, а не кодом");

            var run = new NightRun(0, (x, z) => 0f, World.ElbrusPlan);
            run.AddPlayer("a", "Путник", 0);
            Assert.IsTrue(run.Fall("a", "{name} срывается."));
            Assert.AreEqual(Outcome.Fall, run.Players["a"].Outcome);
            Assert.IsFalse(run.Fall("a", "{name} срывается ещё раз."), "дважды не срываются");
            Assert.IsTrue(run.Events.Any(e => e.Text.Contains("Путник")));
        }

        [Test]
        public void AKnockTakesWarmthAndLeavesTheOutcomeToTheCold()
        {
            var run = new NightRun(0, (x, z) => 0f, World.ElbrusPlan);
            var p = run.AddPlayer("a", "Путник", 0);
            float heat = p.Heat;
            Assert.IsTrue(run.Hurt("a", 20f, "{name} проваливается."));
            Assert.AreEqual(heat - 20f, p.Heat, 1e-3);
            Assert.AreEqual(Outcome.None, p.Outcome, "удар сам по себе исхода не решает");
        }
    }
}
