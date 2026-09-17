using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    public static class ElbrusData
    {
        static HeightField dem;
        public static HeightField Dem => dem ??= HeightField.FromR16(
            TestData.World(Path.Combine("elbrus", "height_2049.r16")), Elbrus.HeightMin, Elbrus.HeightMax,
            Elbrus.Resolution, Elbrus.GridStep);

        public static float Ground(float x, float z) => Dem.Sample(x, z);
    }

    public class ElbrusMapTests
    {
        [Test]
        public void ProjectionRoundTripsAndKeepsNorthUp()
        {
            var (x, z) = Elbrus.Project(43.352410, 42.437843);         // West summit: north-west of the map centre
            Assert.Greater(z, 4000f, "вершина севернее центра карты");
            Assert.Less(x, -1500f, "и западнее");
            var (lat, lon) = Elbrus.Unproject(x, z);
            Assert.AreEqual(43.352410, lat, 1e-5);
            Assert.AreEqual(42.437843, lon, 1e-5);
        }

        [Test]
        public void EveryPlaceFitsInsideTheMap()
        {
            foreach (var p in Elbrus.Pois)
                Assert.IsTrue(Elbrus.Inside(p.X, p.Z, 300f), $"{p.Label} вне карты: {p.X:0}, {p.Z:0}");
            foreach (var line in Elbrus.Ropeways)
                foreach (var t in line.Towers)
                    Assert.IsTrue(Elbrus.Inside(t.x, t.z, 100f), "опора вне карты: " + line.Id);
        }

        [Test]
        public void DemMatchesPublishedElevations()
        {
            var dem = ElbrusData.Dem;
            Assert.AreEqual(2049, dem.Res);
            Assert.AreEqual(6144f, dem.HalfSize, .01);
            Assert.Greater(dem.Max, 5630f);
            // the two summits are corrected to the surveyed figures; everything else stays as the DEM measured it
            var w = Elbrus.Get("westSummit");
            Assert.AreEqual(5642f, dem.Sample(w.X, w.Z), 3f, "Западная вершина");
            var e = Elbrus.Get("eastSummit");
            Assert.AreEqual(5621f, dem.Sample(e.X, e.Z), 3f, "Восточная вершина");
            foreach (var id in new[] { "azau", "mir", "garabashi", "leaprus", "pastukhov" })
            {
                var p = Elbrus.Get(id);
                Assert.AreEqual(p.Ele, dem.Sample(p.X, p.Z), 60f, p.Label);
            }
        }

        [Test]
        public void SummitRouteClimbsFromGarabashiToTheTop()
        {
            var r = Elbrus.SummitRoute;
            var dem = ElbrusData.Dem;
            var start = Elbrus.Get("garabashi");
            Assert.Less(Elbrus.Distance(r[0].x, r[0].z, start.X, start.Z), 60f, "тропа начинается у верхней станции");
            var top = Elbrus.Get("westSummit");
            var last = r[r.Length - 1];
            Assert.Less(Elbrus.Distance(last.x, last.z, top.X, top.Z), 20f, "и кончается на вершине");
            float length = Elbrus.Length(r);
            Assert.Greater(length, 6500f, "длина классического маршрута ≈8 км");
            Assert.Less(length, 11000f, "длина классического маршрута ≈8 км");
            Assert.Greater(dem.Sample(last.x, last.z) - dem.Sample(r[0].x, r[0].z), 1700f, "набор высоты ≈1800 м");
            foreach (var p in r) Assert.IsTrue(Elbrus.Inside(p.x, p.z, 100f), "точка маршрута вне карты");
        }

        [Test]
        public void RatrakRoadStopsBelowTheShelf()
        {
            var dem = ElbrusData.Dem;
            var last = Elbrus.RatrakRoute[Elbrus.RatrakRoute.Length - 1];
            float top = dem.Sample(last.x, last.z);
            Assert.Greater(top, 4950f, "ратрак разворачивается около 5100 м");
            Assert.Less(top, 5200f, "ратрак разворачивается около 5100 м");
            var first = Elbrus.RatrakRoute[0];
            Assert.Less(Elbrus.Distance(first.x, first.z, Elbrus.Barrels.X, Elbrus.Barrels.Z), 60f, "выезжает от бочек");
        }
    }

    public class RopewayTests
    {
        static Ropeway Build(string id) => new Ropeway(Elbrus.Ropeways.First(r => r.Id == id), ElbrusData.Ground);

        [Test]
        public void EveryLineConnectsItsTwoTerminals()
        {
            foreach (var spec in Elbrus.Ropeways)
            {
                var line = new Ropeway(spec, ElbrusData.Ground);
                var bottom = Elbrus.Terminal(spec.BottomId);
                var top = Elbrus.Terminal(spec.TopId);
                var first = spec.Towers[0];
                var lastTower = spec.Towers[spec.Towers.Length - 1];
                // a jig-back and the gondola beside it have separate buildings, up to ~150 m apart at Azau and Krugozor
                Assert.Less(Elbrus.Distance(first.x, first.z, bottom.x, bottom.z), 200f, spec.Id + " низ");
                Assert.Less(Elbrus.Distance(lastTower.x, lastTower.z, top.x, top.z), 200f, spec.Id + " верх");
                Assert.Greater(line.Length, 700f, spec.Id + " длина очереди");
                Assert.Less(line.Length, 2600f, spec.Id + " длина очереди");
            }
        }

        [Test]
        public void RopeClearsTheGroundAllTheWay()
        {
            foreach (var spec in Elbrus.Ropeways)
            {
                var line = new Ropeway(spec, ElbrusData.Ground);
                // what must clear the slope is the floor of the car, not the rope: everything hangs Drop below it
                float drop = Ropeway.Drop(spec.Kind);
                float clear = spec.Kind == RopewayKind.Chair ? 2f : 2.5f;
                for (float s = 30f; s < line.Length - 30f; s += 10f)
                {
                    var p = line.PointAt(s);
                    Assert.Greater(p.y - drop - ElbrusData.Ground(p.x, p.z), clear, $"{spec.Id}: кабина задевает склон на {s:0} м");
                }
                for (int i = 0; i < line.TowerHeight.Length; i++)
                {
                    float h = line.TowerHeight[i];
                    bool terminal = i == 0 || i == line.TowerHeight.Length - 1;
                    // a terminal is low on purpose: the rope comes down so the floor of the car meets the platform
                    Assert.Greater(h, terminal ? drop + .5f : 5f, spec.Id + " высота опоры");
                    Assert.Less(h, 48.1f, spec.Id + " высота опоры");
                }
            }
        }

        [Test]
        public void GondolaCabinsRideTheWholeLoopAndCanBeBoardedAtBothEnds()
        {
            var line = Build("gondola3");
            Assert.Greater(line.Cars, 20, "кабины идут через каждые 110 м");
            for (int i = 0; i < line.Cars; i += 7)
            {
                var a = line.CarAt(i, 0);
                var b = line.CarAt(i, line.CycleSeconds);
                Assert.AreEqual(a.S, b.S, 2f, "кабина возвращается туда же за один круг");
                Assert.AreEqual(a.Up, b.Up, "и на ту же ветвь");
            }
            bool bottom = false, top = false;
            for (double t = 0; t < line.CycleSeconds; t += 1.0)
            {
                if (line.BoardableCar(t, true) >= 0) bottom = true;
                if (line.BoardableCar(t, false) >= 0) top = true;
            }
            Assert.IsTrue(bottom && top, "кабину можно поймать и внизу, и наверху");
        }

        [Test]
        public void GondolaRideTakesAboutTheAdvertisedTime()
        {
            foreach (var id in new[] { "gondola1", "gondola2", "gondola3" })
            {
                var line = Build(id);
                float minutes = line.CycleSeconds / 2f / 60f;
                Assert.Greater(minutes, 3f, id + ": подъём за 5–7 минут по данным оператора");
                Assert.Less(minutes, 12f, id + ": подъём за 5–7 минут по данным оператора");
            }
        }

        [Test]
        public void JigBackCarsAlwaysCounterbalanceEachOther()
        {
            var line = Build("pendulum1");
            Assert.AreEqual(2, line.Cars);
            for (double t = 0; t < line.CycleSeconds; t += 3.0)
            {
                var a = line.CarAt(0, t);
                var b = line.CarAt(1, t);
                Assert.AreEqual(line.Length, a.S + b.S, 1.5f, "вагоны — два конца одного троса");
            }
            int standing = 0;
            for (double t = 0; t < line.CycleSeconds; t += 1.0) if (line.CarAt(0, t).Speed <= 0.01f) standing++;
            Assert.Greater(standing, 60, "стоянка на станции ≈45 с с каждой стороны");
        }

        [Test]
        public void SpeedDropsToWalkingPaceInsideTheStations()
        {
            var line = Build("gondola1");
            Assert.Less(line.SpeedAt(2f), 1.2f);
            Assert.Less(line.SpeedAt(line.Length - 2f), 1.2f);
            Assert.AreEqual(5f, line.SpeedAt(line.Length / 2f), .01);
        }
    }

    public class ElbrusScenarioTests
    {
        [Test]
        public void DayRunIsLongerAndMilderThanTheNight()
        {
            var plan = World.ElbrusPlan;
            Assert.IsFalse(plan.Storms);
            Assert.Greater(plan.Profile.Seconds, SurvivalRules.NightSeconds * 3);
            var p = new Participant { Heat = 100 };
            SurvivalRules.Tick(p, 60f, new SurvivalRules.Conditions(), plan.Profile);
            Assert.Greater(p.Heat, 97f, "за минуту на дневном профиле теряется меньше трёх единиц тепла");
        }

        [Test]
        public void SummitEndsTheRunAndHutsShelter()
        {
            var plan = World.ElbrusPlan;
            var top = Elbrus.WestSummit;
            Assert.IsTrue(plan.AtGoal(top.X, top.Z));
            Assert.IsFalse(plan.AtGoal(top.X + 200f, top.Z));
            var hut = Elbrus.Get("priut11");
            Assert.IsTrue(plan.Sheltered(hut.X, hut.Z, 4050f));
            Assert.IsFalse(plan.Sheltered(hut.X + 400f, hut.Z, 4050f));
            Assert.IsNull(plan.NearFireplace, "костров на леднике не жгут");
        }

        [Test]
        public void SlopeRunIsUnchanged()
        {
            var plan = NightRun.Scenario.Slope;
            Assert.IsTrue(plan.Storms);
            Assert.AreSame(SurvivalRules.Profile.Night, plan.Profile);
            Assert.IsTrue(plan.NearFireplace(WorldData.Camp.x, WorldData.Camp.z));
        }
    }
}
