using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    /// <summary>The registry as the shared rules see it. Nothing here names a location other than Kholat Syakhl,
    /// which is the point: these tests pass in a build compiled without any optional location.</summary>
    public class LocationTests
    {
        [Test]
        public void KholatIsAlwaysThereAndIsWhatALookupFallsBackTo()
        {
            Assert.AreEqual(Place.Kholat, Locations.Default.Place);
            Assert.IsTrue(Locations.Has(Place.Kholat));
            Assert.AreSame(Locations.Default, Locations.Of(Place.Kholat));
            Assert.AreSame(Locations.Default, Locations.Available[0]);  // the default location is listed first
            // a place this build was compiled without reads as the default rather than as null
            foreach (Place p in System.Enum.GetValues(typeof(Place)))
                Assert.IsNotNull(Locations.Of(p), p.ToString());
        }

        [Test]
        public void RegisteringTwiceGivesOneLocation()
        {
            int was = Locations.Available.Count;
            Locations.Register(Locations.Default);
            Locations.Register(Locations.Default);
            Assert.AreEqual(was, Locations.Available.Count);
            Locations.Register(null);
            Assert.AreEqual(was, Locations.Available.Count);
        }

        [Test]
        public void TheWorldIsWhateverTheActiveLocationSays()
        {
            var was = World.Current;
            try
            {
                World.Current = Place.Kholat;
                var l = Locations.Of(Place.Kholat);
                Assert.AreSame(l, World.Active);
                Assert.AreEqual(l.Title, World.Title);
                Assert.AreEqual(l.Half, World.Half);
                Assert.AreEqual(l.Half * 2, World.Size);
                Assert.AreEqual(l.Resolution, World.Resolution);
                Assert.AreEqual(l.GridStep, World.GridStep);
                Assert.AreEqual(l.HeightMin, World.HeightMin);
                Assert.AreEqual(l.HeightMax, World.HeightMax);
                Assert.AreEqual(l.TerrainAsset, World.TerrainAsset);
                Assert.AreEqual(l.HeightAsset, World.HeightAsset);
                Assert.AreEqual(l.Scenario.Id, World.Scenario.Id);
                Assert.AreEqual(WorldData.GroundHeight(TestData.Dem, 40f, -60f), World.Ground(TestData.Dem, 40f, -60f), 1e-4);
            }
            finally { World.Current = was; }
        }

        [Test]
        public void TheSnowOfTheActivePlaceIsTheKholatModelHere()
        {
            var was = World.Current;
            try
            {
                World.Current = Place.Kholat;
                float x = 120f, z = -340f;
                Assert.AreEqual(SnowCover.KholatDepth(TestData.Dem, x, z), SnowCover.Depth(TestData.Dem, x, z), 1e-6);
                Assert.AreEqual(SnowCover.KholatCrust(TestData.Dem, x, z), SnowCover.Crust(TestData.Dem, x, z), 1e-6);
                float y = TestData.Dem.Sample(x, z);
                Assert.AreEqual(SnowCover.KholatCanopy(y), SnowCover.Forest(x, z, y), 1e-6);
            }
            finally { World.Current = was; }
        }

        [Test]
        public void TheStarterPackComesFromTheLocation()
        {
            var direct = Items.StarterFor(1);
            var byPlace = Items.StarterFor(1, Place.Kholat);
            Assert.AreEqual(direct.Count, byPlace.Count);
            for (int i = 0; i < direct.Count; i++) Assert.AreEqual(direct[i].Id, byPlace[i].Id);
        }
    }
}
