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
            own = w.AddPack("a", 0, 0, 0, Items.Starter);
            return w;
        }

        [Test]
        public void StarterPackFitsAndWeighsAboutElevenKilos()
        {
            var w = World(out var p);
            Assert.AreEqual(Items.Starter.Length, p.Contents.Count);
            Assert.Greater(p.Kg, 8f); Assert.Less(p.Kg, 14f);
            Assert.Less(p.Litres, Backpack.CapacityLitres);
            Assert.AreEqual(p.Kg, w.CarriedKg("a"), 1e-4);
        }

        [Test]
        public void CapacityLimitsVolume()
        {
            var p = new Backpack(1);
            int n = 0;
            while (p.Put(ItemId.Firewood)) n++;
            Assert.AreEqual((int)(Backpack.CapacityLitres / Items.Spec(ItemId.Firewood).Litres), n);
            while (p.Put(ItemId.Pot)) { }
            Assert.IsFalse(p.Fits(ItemId.Pot));
            Assert.Less(p.Litres, Backpack.CapacityLitres + .01f);
            Assert.IsTrue(p.Fits(ItemId.Matches));
            Assert.IsFalse(p.Put(ItemId.None));
        }

        [Test]
        public void TakeAndStowMoveOneItemThroughTheHands()
        {
            var w = World(out var p);
            int v = w.Version;
            float kg = w.CarriedKg("a");
            Assert.AreEqual(PackResult.Ok, w.Take("a", p.Id, 2, 0, 0));
            Assert.AreEqual(ItemId.Stew, w.Hand("a"));
            Assert.Greater(w.Version, v);
            Assert.AreEqual(kg, w.CarriedKg("a"), 1e-4, "weight in the hands still counts");
            Assert.AreEqual(PackResult.HandsFull, w.Take("a", p.Id, 0, 0, 0));
            Assert.AreEqual(PackResult.Ok, w.Stow("a", p.Id, 0, 0));
            Assert.AreEqual(ItemId.None, w.Hand("a"));
            Assert.AreEqual(ItemId.Stew, p.Contents.Last());
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
            Assert.AreEqual(ItemId.Rusks, w.Hand("b"));
            Assert.IsNull(w.Reachable("c", 10, 0), "a pack on a back is only reachable next to its wearer");
        }

        [Test]
        public void LooseItemsArePickedUpWithinReach()
        {
            var w = World(out var p);
            w.Take("a", p.Id, 0, 0, 0);
            Assert.AreEqual(PackResult.Ok, w.DropHand("a", 1, 0, 1, 0));
            var l = w.NearestLoose(0, 0);
            Assert.IsNotNull(l); Assert.AreEqual(ItemId.Rusks, l.Item);
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
            Assert.AreEqual(ItemId.None, w.Hand("a"));
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
}
