using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    /// <summary>Elbrus seen through the contract the shared rules use: registered, selectable, and answering for the
    /// frame, the run and the snow. If this assembly is compiled out (HEIGHT1079_NO_ELBRUS), so are these tests —
    /// and LocationTests in the core assembly still has to pass.</summary>
    public class ElbrusLocationTests
    {
        [Test]
        public void ElbrusRegistersItselfAndOnlyOnce()
        {
            ElbrusLocation.Register();
            Assert.IsTrue(Locations.Has(Place.Elbrus));
            int was = Locations.Available.Count;
            ElbrusLocation.Register();
            Assert.AreEqual(was, Locations.Available.Count, "registration is idempotent");
            Assert.AreSame(ElbrusLocation.Instance, Locations.Of(Place.Elbrus));
        }

        [Test]
        public void SelectingElbrusSwitchesTheFrameTheRunAndTheKit()
        {
            ElbrusLocation.Register();
            var was = World.Current;
            try
            {
                World.Current = Place.Elbrus;
                Assert.AreEqual(Place.Elbrus, World.Current);
                Assert.IsTrue(World.IsElbrus);
                Assert.AreEqual(Elbrus.Half, World.Half);
                Assert.AreEqual(Elbrus.Resolution, World.Resolution);
                Assert.AreEqual(Elbrus.GridStep, World.GridStep);
                Assert.AreEqual(Elbrus.HeightMin, World.HeightMin);
                Assert.AreEqual(Elbrus.HeightMax, World.HeightMax);
                Assert.AreEqual("Elbrus", World.TerrainAsset);
                Assert.AreEqual("elbrus_height_2049", World.HeightAsset);
                Assert.AreEqual("elbrus", World.Scenario.Id);
                Assert.IsFalse(World.Scenario.Storms, "no February blizzard cycle on the day route");
                StringAssert.Contains("Эльбрус", World.Title);
            }
            finally { World.Current = was; }
        }

        [Test]
        public void TheHutsAreShelterAndTheOpenSlopeIsNot()
        {
            var hut = Elbrus.Get("barrels");
            Assert.IsTrue(ElbrusLocation.Sheltered(hut.X, hut.Z));
            Assert.IsFalse(ElbrusLocation.Sheltered(hut.X + 400f, hut.Z + 400f));
            Assert.IsTrue(ElbrusLocation.Plan.Sheltered(hut.X, hut.Z, hut.Ele));
        }

        [Test]
        public void ElbrusBringsItsOwnSnowAndItsOwnRucksack()
        {
            ElbrusLocation.Register();
            var l = Locations.Of(Place.Elbrus);
            // no canopy anywhere on the slope, whatever the height
            Assert.AreEqual(0f, l.Canopy(0f, 0f, 3500f), 1e-6);
            // firn above the shelf carries better than the moraine below it
            var dem = Flat(3000f);
            var high = Flat(4600f);
            Assert.Less(l.SnowCrust(dem, 0f, 0f), l.SnowCrust(high, 0f, 0f));
            Assert.Greater(l.SnowDepth(dem, 0f, 0f), l.SnowDepth(high, 0f, 0f));

            var kit = l.Starter(0);
            Assert.IsTrue(kit.Exists(s => s.Id == ItemId.Tent), "двойка");
            Assert.IsTrue(kit.Exists(s => s.Id == ItemId.Burner), "горелка");
            Assert.IsTrue(kit.Exists(s => s.Id == ItemId.ProgrammeSheet), "лист программы");
            Assert.IsFalse(kit.Exists(s => s.Id == ItemId.Axe), "no 1959 axe on the southern slope");
        }

        /// <summary>A height field that is the same everywhere, so the altitude belt is the only thing being read.</summary>
        static HeightField Flat(float y)
        {
            const int res = 9;
            var h = new float[res * res];
            for (int i = 0; i < h.Length; i++) h[i] = y;
            return new HeightField(h, res, Elbrus.GridStep);
        }
    }
}
