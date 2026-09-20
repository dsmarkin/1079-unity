using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    public class RefreshmentsTests
    {
        [Test]
        public void MenuIsPricedAndTimedLikeAMountainCafe()
        {
            var menu = Refreshments.Menu();
            Assert.Greater(menu.Length, 9, "хычины, супы, шашлык, пицца и бар");
            foreach (var d in menu)
            {
                Assert.IsFalse(d.IsEmpty);
                Assert.IsTrue(d.Name.Length > 2, d.Id.ToString());
                Assert.IsTrue(d.Roubles >= 100 && d.Roubles <= 1200, d.Line);
                Assert.Greater(d.Seconds, 10f, d.Line);
                if (d.Portable) { Assert.Greater(d.Kg, 0f); Assert.Greater(d.Litres, 0f); }
            }
            Assert.Greater(Refreshments.Get(DishId.Shurpa).Roubles, Refreshments.Get(DishId.Tea).Roubles, "суп дороже чая");
            StringAssert.Contains("₽", Refreshments.Board(4));
        }

        [Test]
        public void HotSoupWarmsAndBringsTheHandsBack()
        {
            var p = new Participant { Heat = 40f, Hands = 30f, Clarity = 60f };
            var meal = Refreshments.Eat(DishId.Shurpa, p);
            Assert.AreEqual(70f, p.Heat, 1e-3);
            Assert.AreEqual(50f, p.Hands, 1e-3);
            Assert.AreEqual(30f, meal.Warmth, 1e-3);
            StringAssert.Contains("Шурпа", meal.Text);
            // a hiker who is already warm gains only what is left up to 100
            var warm = new Participant { Heat = 92f };
            Assert.AreEqual(8f, Refreshments.Eat(DishId.Shurpa, warm).Warmth, 1e-3);
            Assert.AreEqual(100f, warm.Heat, 1e-3);
            // the night is over for this one: nothing is served
            var done = new Participant { Heat = 10f, Outcome = Outcome.Cold };
            Assert.IsTrue(Refreshments.Eat(DishId.Shurpa, done).IsEmpty);
            Assert.AreEqual(10f, done.Heat, 1e-3);
        }

        [Test]
        public void MulledWineWarmsButCloudsTheHead()
        {
            var p = new Participant { Heat = 50f, Clarity = 80f };
            var meal = Refreshments.Eat(DishId.MulledWine, p);
            Assert.Greater(meal.Warmth, 0f);
            Assert.Less(meal.Clarity, 0f);
            Assert.AreEqual(74f, p.Clarity, 1e-3);
            Assert.Greater(Refreshments.Get(DishId.Coffee).Clarity, 0f, "кофе, наоборот, проясняет");
        }

        [Test]
        public void WalletPaysUntilItIsEmptyAndTakesRefunds()
        {
            var w = Wallet.Start();
            Assert.AreEqual(Wallet.StartRoubles, w.Roubles);
            var shashlyk = Refreshments.Get(DishId.Shashlyk);
            Assert.IsTrue(w.Pay(shashlyk.Roubles));
            Assert.AreEqual(Wallet.StartRoubles - shashlyk.Roubles, w.Roubles);
            w.Refund(shashlyk.Roubles);
            Assert.AreEqual(Wallet.StartRoubles, w.Roubles);
            Assert.IsFalse(w.Pay(Wallet.StartRoubles + 1), "в долг на склоне не наливают");
            Assert.AreEqual(Wallet.StartRoubles, w.Roubles);
            Assert.IsTrue(w.Pay(Wallet.StartRoubles));
            Assert.IsFalse(w.CanAfford(1));
        }

        [Test]
        public void PortablePortionsGoIntoTheRucksack()
        {
            var world = new PackWorld();
            var pack = world.AddPack("a", 0, 0, 0);
            int taken = 0;
            foreach (var d in Refreshments.Menu())
            {
                if (!d.Portable) continue;
                taken++;
                var spec = Items.Spec(d.TakeAway);
                Assert.IsTrue(spec.Name.Length > 2, d.Id.ToString());
                Assert.AreEqual(ItemKind.Food, spec.Kind);
                Assert.AreEqual(PackResult.Ok, world.Receive("a", new ItemStack(d.TakeAway)));
            }
            Assert.Greater(taken, 3, "хычин, шашлык, пицца и стакан с собой");
            Assert.AreEqual(taken, pack.Contents.Count);
            Assert.Less(pack.Litres, Backpack.CapacityLitres);
        }
    }
}
