using System.Linq;
using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    public class PackTests
    {
        static PackWorld World(out Backpack own)
        {
            var w = new PackWorld();
            w.Locate = t => t == "a" ? (0f, 0f) : t == "b" ? (1f, 0f) : ((float, float)?)null;
            own = w.AddPack("a", 0, 0, 0, Items.StarterFor(0));
            return w;
        }

        [Test]
        public void StarterPackFitsAndWeighsAboutElevenKilos()
        {
            var w = World(out var p);
            Assert.AreEqual(Items.Starter.Length + 2, p.Contents.Count, "food and gear, a box of matches and one heavy tool");
            Assert.Greater(p.Kg, 8f); Assert.Less(p.Kg, 14f);
            Assert.Less(p.Litres, Backpack.CapacityLitres);
            Assert.AreEqual(p.Kg, w.CarriedKg("a"), 1e-4);
        }

        [Test]
        public void CapacityLimitsVolume()
        {
            var p = new Backpack(1);
            int n = 0;
            while (p.Put(new ItemStack(ItemId.Firewood, Items.LogsInArmful))) n++;
            Assert.AreEqual((int)(Backpack.CapacityLitres / (Items.Spec(ItemId.Firewood).Litres * Items.LogsInArmful)), n);
            Assert.IsTrue(p.Fits(new ItemStack(ItemId.Matches)), "a box of matches still slips in");
            while (p.Put(ItemId.Pot)) { }
            Assert.IsFalse(p.Fits(new ItemStack(ItemId.Pot)));
            Assert.Less(p.Litres, Backpack.CapacityLitres + .01f);
            Assert.IsFalse(p.Put(ItemId.None));
        }

        [Test]
        public void TakeAndStowMoveOneItemThroughTheHands()
        {
            var w = World(out var p);
            int v = w.Version;
            float kg = w.CarriedKg("a");
            Assert.AreEqual(PackResult.Ok, w.Take("a", p.Id, 2, 0, 0));
            Assert.AreEqual(ItemId.Stew, w.Hand("a").Id);
            Assert.Greater(w.Version, v);
            Assert.AreEqual(kg, w.CarriedKg("a"), 1e-4, "weight in the hands still counts");
            Assert.AreEqual(PackResult.HandsFull, w.Take("a", p.Id, 0, 0, 0));
            Assert.AreEqual(PackResult.Ok, w.Stow("a", p.Id, 0, 0));
            Assert.IsTrue(w.Hand("a").IsEmpty);
            Assert.AreEqual(ItemId.Stew, p.Contents.Last().Id);
            Assert.AreEqual(PackResult.HandsEmpty, w.Stow("a", p.Id, 0, 0));
            Assert.AreEqual(PackResult.NoItem, w.Take("a", p.Id, 99, 0, 0));
        }

        [Test]
        public void DroppedPackStaysInTheSnowAndCanBeWornAgain()
        {
            var w = World(out var p);
            Assert.AreEqual(PackResult.Ok, w.Drop("a", 5, 600, 5, 30));
            Assert.IsNull(w.Worn("a"));
            Assert.AreEqual(0f, w.CarriedKg("a"));
            Assert.AreEqual(PackResult.NoPack, w.Drop("a", 0, 0, 0, 0));
            Assert.AreEqual(PackResult.TooFar, w.Wear("a", p.Id, 50, 50));
            Assert.AreEqual(PackResult.TooFar, w.Take("a", p.Id, 0, 50, 50));
            Assert.AreSame(p, w.Reachable("a", 4, 4));
            Assert.AreEqual(PackResult.Ok, w.Wear("a", p.Id, 4, 4));
            Assert.AreEqual(PackResult.AlreadyWearing, w.Wear("a", p.Id, 4, 4));
        }

        [Test]
        public void CompanionCanReachIntoAPackOnYourBackButNotPutItOn()
        {
            var w = World(out var p);
            Assert.AreSame(p, w.Reachable("b", 1, 0));
            Assert.AreEqual(PackResult.WornByOther, w.Wear("b", p.Id, 1, 0));
            Assert.AreEqual(PackResult.Ok, w.Take("b", p.Id, 0, 1, 0));
            Assert.AreEqual(ItemId.Rusks, w.Hand("b").Id);
            Assert.IsNull(w.Reachable("c", 10, 0), "a pack on a back is only reachable next to its wearer");
        }

        [Test]
        public void LooseItemsArePickedUpWithinReach()
        {
            var w = World(out var p);
            w.Take("a", p.Id, 0, 0, 0);
            Assert.AreEqual(PackResult.Ok, w.DropHand("a", 1, 0, 1, 0));
            var l = w.NearestLoose(0, 0);
            Assert.IsNotNull(l); Assert.AreEqual(ItemId.Rusks, l.Stack.Id);
            Assert.IsNull(w.NearestLoose(10, 10));
            Assert.AreEqual(PackResult.TooFar, w.PickUp("b", l.Id, 10, 10));
            Assert.AreEqual(PackResult.Ok, w.PickUp("b", l.Id, 1, 0));
            Assert.AreEqual(0, w.Loose.Count);
            Assert.AreEqual(PackResult.NoItem, w.PickUp("a", l.Id, 1, 0));
        }

        [Test]
        public void LeavingPlayerLeavesTheirThingsBehind()
        {
            var w = World(out var p);
            w.Take("a", p.Id, 0, 0, 0);
            w.Leave("a", 3, 0, 3);
            Assert.IsNull(p.Wearer);
            Assert.AreEqual(3f, p.X);
            Assert.AreEqual(1, w.Loose.Count);
            Assert.IsTrue(w.Hand("a").IsEmpty);
        }

        [Test]
        public void LoadSlowsBeyondTwelveKilos()
        {
            Assert.AreEqual(1f, SurvivalRules.LoadSpeedFactor(0f));
            Assert.AreEqual(1f, SurvivalRules.LoadSpeedFactor(12f));
            Assert.Less(SurvivalRules.LoadSpeedFactor(25f), 1f);
            Assert.AreEqual(.55f, SurvivalRules.LoadSpeedFactor(40f), 1e-4);
            Assert.AreEqual(.55f, SurvivalRules.LoadSpeedFactor(90f), 1e-4);
        }
    }

    public class WoodTests
    {
        static PackWorld World(string token, ItemId tool)
        {
            var w = new PackWorld { Locate = t => t == token ? (0f, 0f) : ((float, float)?)null };
            w.AddPack(token, 0, 0, 0, new[] { new ItemStack(tool), new ItemStack(ItemId.Matches, Items.MatchesInBox) });
            return w;
        }

        [Test]
        public void ToolsDecideWhatCanBeDoneAndHowLong()
        {
            Assert.IsTrue(Woodwork.CanDo(WorkKind.CutBranch, ToolKind.Saw));
            Assert.IsFalse(Woodwork.CanDo(WorkKind.CutBranch, ToolKind.None), "bare hands do not cut a branch");
            Assert.Less(Woodwork.Seconds(WorkKind.CutBranch, ToolKind.Saw), Woodwork.Seconds(WorkKind.CutBranch, ToolKind.Axe));
            Assert.Less(Woodwork.Seconds(WorkKind.SplitBranch, ToolKind.Axe), Woodwork.Seconds(WorkKind.SplitBranch, ToolKind.Hatchet));
            Assert.Greater(Woodwork.Seconds(WorkKind.CutBranch, ToolKind.Saw, 0f), Woodwork.Seconds(WorkKind.CutBranch, ToolKind.Saw, 100f), "stiff hands are slower");
        }

        [Test]
        public void ASawInTheHandsIsTheToolAndABranchBecomesFirewood()
        {
            var w = World("a", ItemId.Saw);
            var pack = w.Worn("a");
            Assert.AreEqual(ToolKind.None, w.HeldTool("a"), "a tool works only in the hands");
            Assert.AreEqual(PackResult.Ok, w.Take("a", pack.Id, pack.IndexOf(ItemId.Saw), 0, 0));
            Assert.AreEqual(ToolKind.Saw, w.HeldTool("a"));
            // the branch just cut off goes into the snow, the saw back into the pack
            w.Stow("a", pack.Id, 0, 0);
            var branch = w.AddLoose(new ItemStack(ItemId.Branch), 1, 0, 0);
            Assert.AreEqual(PackResult.Ok, w.PickUp("a", branch.Id, 0, 0));
            Assert.AreEqual(ItemId.Branch, w.Hand("a").Id);
        }

        [Test]
        public void FirewoodGathersIntoAnArmfulUpToSixLogs()
        {
            var w = World("a", ItemId.Axe);
            for (int i = 0; i < 3; i++)
            {
                var l = w.AddLoose(new ItemStack(ItemId.Firewood, Woodwork.LogsPerBranch), 1, 0, 0);
                w.PickUp("a", l.Id, 0, 0);
            }
            var hand = w.Hand("a");
            Assert.AreEqual(ItemId.Firewood, hand.Id);
            Assert.AreEqual(Items.LogsInArmful, hand.Amount, "an armful holds no more than six logs");
            Assert.AreEqual(Items.Spec(ItemId.Firewood).Kg * Items.LogsInArmful, hand.Kg, 1e-4, "an armful weighs by the log");
            Assert.AreEqual(1, w.Loose.Count, "what did not fit stays in the snow");
            Assert.IsTrue(w.Spend("a", ItemId.Firewood, 2));
            Assert.AreEqual(Items.LogsInArmful - 2, w.Hand("a").Amount);
        }

        [Test]
        public void MatchesRunOutAndOnlyDryOnesCount()
        {
            var w = World("a", ItemId.Axe);
            Assert.IsTrue(w.Has("a", ItemId.Matches, true));
            for (int i = 0; i < Items.MatchesInBox; i++) Assert.IsTrue(w.Spend("a", ItemId.Matches, 1, true), "match " + i);
            Assert.IsFalse(w.Has("a", ItemId.Matches, true), "the box is empty");
            Assert.IsFalse(w.Spend("a", ItemId.Matches, 1, true));
        }

        [Test]
        public void MatchesInTheHandsGetWetAndDryByTheFire()
        {
            var w = World("a", ItemId.Axe);
            var pack = w.Worn("a");
            w.Take("a", pack.Id, pack.IndexOf(ItemId.Matches), 0, 0);
            for (int i = 0; i < 40; i++) w.Weather("a", .1f, true, false, false);      // a blizzard, four seconds
            Assert.Greater(w.Hand("a").Wet, 0);
            Assert.IsFalse(w.Hand("a").Damp, "a blizzard alone does not soak the box at once");
            for (int i = 0; i < 20; i++) w.Weather("a", .1f, false, true, false);      // two seconds of open water on the brook
            Assert.IsTrue(w.Hand("a").Damp);
            Assert.IsFalse(w.Has("a", ItemId.Matches, true), "damp matches do not count");
            for (int i = 0; i < 200; i++) w.Weather("a", .1f, false, false, true);     // twenty seconds by the fire
            Assert.AreEqual(0, w.Hand("a").Wet);
            Assert.IsTrue(w.Has("a", ItemId.Matches, true));
        }

        [Test]
        public void MatchesInTheRucksackStayDry()
        {
            var w = World("a", ItemId.Axe);
            for (int i = 0; i < 100; i++) w.Weather("a", .1f, true, true, false);
            Assert.IsTrue(w.Has("a", ItemId.Matches, true));
        }

        [Test]
        public void TheFireNeedsAMatchAndWoodAndLogsKeepItGoing()
        {
            var run = new NightRun(0, (x, z) => 700f);
            var w = World("a", ItemId.Axe);
            run.KindleSupplies = t => w.Has(t, ItemId.Matches, true) && w.Has(t, ItemId.Firewood);
            run.KindleSpent = t => { w.Spend(t, ItemId.Matches, 1, true); w.Spend(t, ItemId.Firewood, 1); };
            var p = run.AddPlayer("a", "А", 0);
            run.Move("a", WorldData.Camp.x, WorldData.Camp.z, 0);
            Assert.IsFalse(run.BeginKindling("a", 0), "nothing to burn yet");
            var logs = w.AddLoose(new ItemStack(ItemId.Firewood, Woodwork.LogsPerBranch), WorldData.Camp.x, 0, WorldData.Camp.z);
            w.PickUp("a", logs.Id, WorldData.Camp.x, WorldData.Camp.z);
            Assert.IsTrue(run.BeginKindling("a", 0));
            double t = 0;
            while (run.FireRemaining(t) <= 0 && t < 30) { t += .25; run.Step(t); }
            Assert.Greater(run.FireRemaining(t), 0f, "the fire catches");
            Assert.AreEqual(Items.MatchesInBox - 1, w.Worn("a").Contents[w.Worn("a").IndexOf(ItemId.Matches)].Amount);
            Assert.AreEqual(Woodwork.LogsPerBranch - 1, w.Hand("a").Amount, "one log went into the fire");
            float left = run.FireRemaining(t);
            Assert.IsTrue(run.FeedFire("a", 2, t));
            Assert.AreEqual(left + 2 * Woodwork.SecondsPerLog, run.FireRemaining(t), .3f);
            run.Move("a", WorldData.Tent.X, WorldData.Tent.Z, t);
            Assert.IsFalse(run.FeedFire("a", 1, t), "not by the fire");
        }
    }
}
