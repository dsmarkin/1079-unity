using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    /// <summary>Three slots and a rucksack, the PEAK way (the sandbox's kit). Engine-free, runs in dotnet.</summary>
    public class KitTests
    {
        [Test]
        public void FlashlightIsAnItemOfTheCatalogue()
        {
            var spec = Items.Spec(ItemId.Flashlight);
            Assert.AreEqual(ItemId.Flashlight, spec.Id, "фонарика нет в каталоге");
            Assert.AreEqual("Фонарик", spec.Name);
            Assert.Greater(spec.Kg, 0f);
            Assert.AreEqual(ItemKind.Gear, spec.Kind);
        }

        [Test]
        public void SelectingASlotPutsItInTheHandsAndAgainPutsItAway()
        {
            var k = new Kit();
            k.Give(0, new ItemStack(ItemId.Flashlight));
            Assert.IsTrue(k.HandsEmpty, "руки в начале пусты");
            k.Select(0);
            Assert.AreEqual(ItemId.Flashlight, k.InHand.Id);
            k.Select(0);
            Assert.IsTrue(k.HandsEmpty, "второе нажатие убирает");
            k.Select(1);
            Assert.IsTrue(k.HandsEmpty, "пустой слот — пустые руки");
            Assert.AreEqual(Kit.Nothing, k.Selected);
        }

        [Test]
        public void DrawTakesFromTheRucksackIntoTheFreeSlotAndStowPutsItBack()
        {
            var k = new Kit();
            k.Pack.Put(ItemId.Chocolate);
            k.Pack.Put(ItemId.Pot);
            Assert.IsTrue(k.Draw(1), "котелок из рюкзака");
            Assert.AreEqual(ItemId.Pot, k.Slot(0).Id);
            Assert.AreEqual(1, k.Pack.Contents.Count);
            Assert.AreEqual(ItemId.Chocolate, k.Pack.Contents[0].Id, "шоколад остался в рюкзаке");
            k.Select(0);
            Assert.IsTrue(k.Stow(0), "обратно в рюкзак");
            Assert.IsTrue(k.Slot(0).IsEmpty);
            Assert.IsTrue(k.HandsEmpty, "убранное из слота не остаётся в руках");
            Assert.AreEqual(ItemId.Pot, k.Pack.Contents[1].Id);
        }

        [Test]
        public void DrawIntoATakenSlotSwapsWithTheRucksack()
        {
            var k = new Kit();
            k.Give(0, new ItemStack(ItemId.Flashlight));
            k.Pack.Put(ItemId.Chocolate);
            Assert.IsTrue(k.Draw(0, 0));
            Assert.AreEqual(ItemId.Chocolate, k.Slot(0).Id);
            Assert.AreEqual(1, k.Pack.Contents.Count);
            Assert.AreEqual(ItemId.Flashlight, k.Pack.Contents[0].Id, "фонарик ушёл в рюкзак взамен");
        }

        [Test]
        public void NothingMovesWhenTheExchangeWillNotFit()
        {
            var k = new Kit();
            // three branches fill the sack: 48 of 50 litres
            for (int i = 0; i < 3; i++) k.Pack.Put(ItemId.Branch);
            k.Pack.Put(ItemId.Matches);
            k.Give(0, new ItemStack(ItemId.Pot));           // 4 litres will not go where a box of matches was
            Assert.IsFalse(k.Draw(3, 0));
            Assert.AreEqual(ItemId.Pot, k.Slot(0).Id, "котелок остался в слоте");
            Assert.AreEqual(4, k.Pack.Contents.Count);
            Assert.AreEqual(ItemId.Matches, k.Pack.Contents[3].Id, "спички вернулись на своё место");
            Assert.IsFalse(k.Stow(0), "в полный рюкзак не убрать");
            Assert.AreEqual(ItemId.Pot, k.Slot(0).Id);
        }

        [Test]
        public void ConsumingTheLastOfAStackEmptiesTheSlotAndTheHands()
        {
            var k = new Kit();
            k.Give(1, new ItemStack(ItemId.Chocolate));
            k.Give(2, new ItemStack(ItemId.Matches, 3));
            k.Select(1);
            var eaten = k.Consume(1);
            Assert.AreEqual(ItemId.Chocolate, eaten.Id);
            Assert.IsTrue(k.Slot(1).IsEmpty);
            Assert.IsTrue(k.HandsEmpty);
            k.Consume(2);
            Assert.AreEqual(2, k.Slot(2).Amount, "спичек стало на одну меньше");
            Assert.IsTrue(k.Consume(0).IsEmpty, "из пустого слота нечего брать");
        }

        [Test]
        public void GiveFillsSlotsFirstThenTheRucksack()
        {
            var k = new Kit();
            foreach (var s in Items.StarterFor(0)) Assert.IsTrue(k.Give(s));
            Assert.AreEqual(Kit.Slots, k.Taken);
            Assert.AreEqual(Items.StarterFor(0).Count - Kit.Slots, k.Pack.Contents.Count);
            Assert.Greater(k.Kg, Backpack.OwnKg);
            Assert.AreEqual(0, k.SlotOf(ItemId.Rusks), "первый предмет набора лёг в первый слот");
            Assert.AreEqual(Kit.Nothing, k.SlotOf(ItemId.Flashlight));
        }
    }
}
