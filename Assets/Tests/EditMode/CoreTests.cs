using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    public static class TestData
    {
        /// <summary>Finds Assets/Data/World/&lt;name&gt; from the Unity project root or from the standalone test runner.</summary>
        public static byte[] World(string name)
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                string candidate = Path.Combine(dir, "Assets", "Data", "World", name);
                if (File.Exists(candidate)) return File.ReadAllBytes(candidate);
                dir = Path.GetDirectoryName(dir);
            }
            throw new FileNotFoundException("Assets/Data/World/" + name);
        }

        static HeightField dem;
        public static HeightField Dem => dem ??= HeightField.FromR16(World("height_2049.r16"), WorldData.HeightMin, WorldData.HeightMax);
    }

    public class SurvivalRulesTests
    {
        static Participant Fresh() => new Participant();

        [Test]
        public void WindCostsHeatShelterAndCompanionReduceTheLoss()
        {
            var a = Fresh(); var b = Fresh();
            SurvivalRules.Tick(a, 60, new SurvivalRules.Conditions { Storm = true });
            SurvivalRules.Tick(b, 60, new SurvivalRules.Conditions { Storm = true, Sheltered = true, Companion = true });
            Assert.Less(a.Heat, b.Heat);
            Assert.AreEqual(60f, a.Exposure, 1e-4);
            Assert.AreEqual(0f, b.Exposure);
        }

        [Test]
        public void FireRestoresHandsAndHeatWithinBounds()
        {
            var r = Fresh(); r.Heat = 10; r.Hands = 1;
            SurvivalRules.Tick(r, 200, new SurvivalRules.Conditions { Fire = true });
            Assert.AreEqual(100f, r.Heat); Assert.AreEqual(100f, r.Hands);
        }

        [Test]
        public void OutcomesFreezeTheRunAndColdBeatsArrival()
        {
            var r = Fresh(); r.Heat = .01f;
            SurvivalRules.Tick(r, 1, new SurvivalRules.Conditions { Goal = true });
            Assert.AreEqual(Outcome.Cold, r.Outcome);
            float t = r.Elapsed;
            SurvivalRules.Tick(r, 100, default);
            Assert.AreEqual(t, r.Elapsed);
        }

        [Test]
        public void ArrivalAndDeadlineProduceDistinctProtocols()
        {
            var a = Fresh(); var b = Fresh();
            SurvivalRules.Tick(a, 1, new SurvivalRules.Conditions { Goal = true });
            SurvivalRules.Tick(b, SurvivalRules.NightSeconds, new SurvivalRules.Conditions { Fire = true });
            Assert.AreEqual(Outcome.Arrival, a.Outcome); Assert.AreEqual(Outcome.Dawn, b.Outcome);
            Assert.AreEqual("17:40", SurvivalRules.NightTime(0)); Assert.AreEqual("06:50", SurvivalRules.NightTime(SurvivalRules.NightSeconds));
        }
    }

    public class WorldDataTests
    {
        static float D(string a, string b) { var p = WorldData.Get(a); var q = WorldData.Get(b); return WorldData.Distance(p.X, p.Z, q.X, q.Z); }

        [Test]
        public void HistoricalPointsFitTheAreaAndKeepTheirDocumentedDistances()
        {
            foreach (var p in WorldData.Pois) { Assert.IsTrue(WorldData.Inside(p.X, p.Z, 50), p.Id); Assert.IsTrue(WorldData.Sources.ContainsKey(p.Source), p.Id); }
            float tc = D("tent", "cedar");
            Assert.IsTrue(tc > 1450 && tc < 1580, $"cedar–tent {tc} (protocols: 1.5 km)");
            Assert.Less(D("cedar", "ravine"), 75f, "den is 50–75 m from the cedar");
            Assert.AreEqual(300f, D("dyatlov", "cedar"), 30f); Assert.AreEqual(480f, D("slobodin", "cedar"), 30f); Assert.AreEqual(630f, D("kolmogorova", "cedar"), 30f);
            float lt = D("labaz", "tent");
            Assert.IsTrue(lt > 1500 && lt < 2100, $"labaz–tent {lt} (diary: ~2 km walked on 1 Feb)");
            Assert.AreEqual(130f, D("tent", "tent2020"), 15f, "2020 table vs MP 18.10");
            float den = WorldData.Distance(WorldData.Den.x, WorldData.Den.z, WorldData.P4.X, WorldData.P4.Z);
            Assert.AreEqual(3f, den, .01f, "floor is 3 m downstream of P4");
        }

        [Test]
        public void FrameIsNotMirroredAndRoundTrips()
        {
            // North is +z, east is +x: the cedar is north-east of the tent, the labaz south of it, the summit west.
            Assert.Greater(WorldData.Cedar.Z, WorldData.Tent.Z); Assert.Greater(WorldData.Cedar.X, WorldData.Tent.X);
            Assert.Less(WorldData.Labaz.Z, WorldData.Tent.Z);
            Assert.Less(WorldData.Get("summit").X, WorldData.Tent.X);
            var (x, z) = WorldData.Project(61.77, 59.47);
            var (lat, lon) = WorldData.Unproject(x, z);
            Assert.AreEqual(61.77, lat, 1e-9); Assert.AreEqual(59.47, lon, 1e-9);
            // 1 arc-second of latitude ≈ 30.9 m here.
            Assert.AreEqual(30.9f, WorldData.Project(WorldData.OriginLat + 1 / 3600.0, WorldData.OriginLon).z, .2f);
        }

        [Test]
        public void HeightFieldSamplesAndClamps()
        {
            var h = new float[HeightField.Resolution * HeightField.Resolution];
            for (int r = 0; r < HeightField.Resolution; r++) for (int c = 0; c < HeightField.Resolution; c++) h[r * HeightField.Resolution + c] = c + 1000 * r;
            var f = new HeightField(h);
            Assert.AreEqual(1024 + 1024000, f.Sample(0, 0), 1);
            Assert.AreEqual(1.5f, f.Sample(-HeightField.Half + 3, -HeightField.Half), 1e-3);
            Assert.AreEqual(0f, f.Sample(-9999, -9999), 1e-3);
            Assert.Throws<InvalidDataException>(() => HeightField.FromR16(new byte[] { 1, 2, 3 }, 0, 1));
        }

        [Test]
        public void DemMatchesKnownElevations()
        {
            var dem = TestData.Dem;
            Assert.AreEqual(WorldData.HeightMin, dem.Min, 3f); Assert.AreEqual(WorldData.HeightMax, dem.Max, 3f);
            var s = WorldData.Get("summit");
            Assert.AreEqual(1096.7f, dem.Sample(s.X, s.Z), 4f, "Kholat Syakhl");
            Assert.AreEqual(792f, dem.Sample(WorldData.Saddle.X, WorldData.Saddle.Z), 3f, "pass saddle");
            float tent = dem.Sample(WorldData.Tent.X, WorldData.Tent.Z);
            Assert.IsTrue(tent > 880 && tent < 910, $"tent {tent} (Borzenkov: 903.7 m)");
            // KAN GPS altitudes read 2–7 m above the DEM; the relative drop along the stream must agree.
            float p4 = dem.Sample(WorldData.P4.X, WorldData.P4.Z), m2 = dem.Sample(WorldData.Get("mouth2").X, WorldData.Get("mouth2").Z);
            Assert.AreEqual(641f - 618f, p4 - m2, 6f);
            float camp = WorldData.GroundHeight(dem, WorldData.Camp.x, WorldData.Camp.z);
            Assert.IsTrue(camp > 600 && camp < WorldData.ShelterHeight, $"camp {camp}");
            Assert.Greater(tent, camp);
            Assert.Less(camp, Sites.TreeLine, "the 31 Jan camp is in the forest");
            float toLabaz = WorldData.Distance(WorldData.Camp.x, WorldData.Camp.z, WorldData.Labaz.X, WorldData.Labaz.Z);
            Assert.Less(toLabaz, 15f, $"camp {toLabaz} m from the labaz");
            float toTent = WorldData.Distance(WorldData.Camp.x, WorldData.Camp.z, WorldData.Tent.X, WorldData.Tent.Z);
            Assert.IsTrue(toTent > 1500 && toTent < 2100, $"camp → tent {toTent} m");
            Assert.IsTrue(WorldData.NearCamp(NightRun.Start.x, NightRun.Start.z));
            // 31 Jan camp accounting: every ski and pole of the nine is somewhere on the site
            Assert.AreEqual(Sites.Camp31.SkiPairs, Sites.Camp31.SkiPairsUnderFloor + Sites.Camp31.SkiPairsAsStand);
            Assert.AreEqual(Sites.Tent.SkiPairsUnderFloor, Sites.Camp31.SkiPairsUnderFloor);
            Assert.AreEqual(8, Sites.Camp31.PolesLeftFree);
            // the tent pad is level where the tent and its entrance stand
            float pad = WorldData.PadHeight(dem);
            foreach (var (ox, oz) in new[] { (0f, 0f), (2f, .8f), (-2.5f, -1f), (3f, 0f), (0f, 1.5f), (0f, -1.5f) })
                Assert.AreEqual(pad, WorldData.GroundHeight(dem, WorldData.CampTentPad.x + ox, WorldData.CampTentPad.z + oz), .01f, $"pad at {ox},{oz}");
            float fireLevel = WorldData.GroundHeight(dem, WorldData.Camp.x, WorldData.Camp.z);
            foreach (var (ox, oz) in new[] { (2.5f, 0f), (-2.5f, 0f), (0f, 2.5f), (0f, -2.5f) })
                Assert.AreEqual(fireLevel, WorldData.GroundHeight(dem, WorldData.Camp.x + ox, WorldData.Camp.z + oz), .01f, $"fire circle at {ox},{oz}");
        }

        [Test]
        public void TentFacesThePassAlongTheContour()
        {
            var (ex, ez, dx, dz, slope) = Sites.Tent.Orientation(TestData.Dem);
            Assert.AreEqual(0f, ex * dx + ez * dz, 1e-3f, "ridge along the contour");
            Assert.Greater(dx, .5f, "the slope falls to the east (north-east slope, protocol)");
            Assert.IsTrue(slope > 10 && slope < 30, $"slope {slope}");
            var ascent = WorldData.AscentRoute;
            Assert.Less(WorldData.Distance(ascent[0].x, ascent[0].z, WorldData.Labaz.X, WorldData.Labaz.Z), 5f);
            Assert.Less(WorldData.Distance(ascent[ascent.Length - 1].x, ascent[ascent.Length - 1].z, WorldData.Tent.X, WorldData.Tent.Z), 5f);
        }

        [Test]
        public void TreesComeFromTheCanopyModel()
        {
            var trees = Height1079.Core.Dem.LoadTrees(TestData.World("trees.f32"));
            Assert.Greater(trees.Length, 20000);
            int above = 0; foreach (var t in trees) { Assert.IsTrue(WorldData.Inside(t.X, t.Z), "inside"); if (TestData.Dem.Sample(t.X, t.Z) > 825) above++; }
            Assert.AreEqual(0, above, "no trees above 820 m (canopy-model noise on rocks is dropped)");
            var c = WorldData.Cedar; int near = 0;
            foreach (var t in trees) if (WorldData.Distance(t.X, t.Z, c.X, c.Z) < 20) near++;
            Assert.Greater(near, 0, "the cedar stands in the canopy model");
            float tentTrees = 0; foreach (var t in trees) if (WorldData.Distance(t.X, t.Z, WorldData.Tent.X, WorldData.Tent.Z) < 300) tentTrees++;
            Assert.AreEqual(0f, tentTrees, "open slope around the tent");

            // species grow in belts: no fir at the tree line, birch and larch gain with height; old taiga has snags
            int[] low = new int[5], high = new int[5]; int snags = 0, lineForms = 0;
            foreach (var t in trees)
            {
                float el = TestData.Dem.Sample(t.X, t.Z);
                if (el < 600) low[(int)t.Species]++; else if (el > 740) high[(int)t.Species]++;
                if (t.Form == TreeForm.Snag) { snags++; Assert.IsTrue(t.Species != TreeSpecies.Birch, "birch snags are not modelled"); }
                if (t.Form == TreeForm.TreeLine) { lineForms++; Assert.Greater(el, 655f); }
            }
            Assert.Greater(low[0] + low[1], (low[2] + low[3] + low[4]) * 2, "dark spruce–fir taiga in the valleys");
            Assert.Less(high[1], high[2] / 5, "fir hardly reaches the tree line, birch does");
            Assert.Greater(high[4], low[4], "larch belongs to the upper belt");
            Assert.Greater(snags, trees.Length / 60); Assert.Less(snags, trees.Length / 15);
            Assert.Greater(lineForms, 1000);
        }

        [Test]
        public void UnderstoryFillsTheForestAndLeavesTheTentSlopeOpen()
        {
            var under = Height1079.Core.Dem.LoadUnderstory(TestData.World("understory.f32"));
            Assert.Greater(under.Length, 50000);
            var counts = new int[10];
            foreach (var u in under)
            {
                counts[(int)u.Kind]++;
                Assert.IsTrue(u.Yaw >= 0 && u.Yaw < 360);
                float el = TestData.Dem.Sample(u.X, u.Z);
                if (u.Kind == UnderKind.YoungFir) Assert.Less(el, 700f, "young fir stays in the taiga");
                if (u.Kind == UnderKind.DwarfBirch) Assert.Greater(el, 715f, "dwarf birch is a tree-line shrub");
            }
            foreach (var c in counts) Assert.Greater(c, 1000, "every layer is present");
            Assert.Greater(counts[(int)UnderKind.YoungSpruce], counts[(int)UnderKind.Rowan], "spruce undergrowth dominates");
        }
    }

    public class NightRunTests
    {
        static float Ground(float x, float z) => WorldData.GroundHeight(TestData.Dem, x, z);

        static double Advance(NightRun run, double from, double seconds, double step = .25)
        {
            double t = from;
            for (int i = 0; i < (int)(seconds / step); i++) { t += step; run.Step(t); }
            return t;
        }

        [Test]
        public void ClockStormAndResourcesAreDrivenByTheRun()
        {
            var run = new NightRun(1, Ground);
            var a = run.AddPlayer("a", "А", 1);
            Assert.AreEqual(1, run.Events.Count);
            Assert.AreEqual(NightRun.Start.x, a.X); Assert.AreEqual(NightRun.Start.z, a.Z);
            double t = Advance(run, 1, 130);
            Assert.AreEqual(130f, run.Elapsed, 1e-3);
            Assert.IsTrue(run.Storm);
            Assert.IsTrue(run.Events.Any(e => e.Text.Contains("метель")));
            Assert.IsTrue(a.Heat < 100 && a.Heat > 60);
            t = Advance(run, t, 160);
            Assert.IsFalse(run.Storm);
        }

        [Test]
        public void TwoBlowsOfTheGiantTakeAPlayer()
        {
            var run = new NightRun(1, Ground);
            run.AddPlayer("a", "А", 1);
            Assert.IsFalse(run.Strike("a", 55f, "{name}: удар из темноты."));
            Assert.IsTrue(run.Events.Any(e => e.Text == "А: удар из темноты."));
            Assert.AreEqual(45f, run.Players["a"].Heat, 1e-3);
            Assert.IsTrue(run.Strike("a", 55f, "{name}: удар из темноты."));
            Assert.AreEqual(Outcome.Taken, run.Players["a"].Outcome);
            Assert.IsFalse(run.Strike("a", 55f, "again"));
            run.Step(2);
            Assert.AreEqual(Outcome.Taken, run.Outcome);
        }

        [Test]
        public void KindlingIsValidatedAndTheFireIsShared()
        {
            var run = new NightRun(0, Ground);
            var a = run.AddPlayer("a", "А", 0); var b = run.AddPlayer("b", "Б", 0);
            run.Move("b", 0, 0, 0);
            Assert.IsFalse(run.BeginKindling("b", 0));
            Assert.IsTrue(run.BeginKindling("a", 0));
            run.Move("a", a.X + 1, a.Z, .1);
            Assert.IsNull(a.KindlingStarted, "moving cancels");
            Assert.IsTrue(run.BeginKindling("a", .2)); run.StopKindling("a"); Assert.IsNull(a.KindlingStarted, "release cancels");
            a.Hands = 40; Assert.IsTrue(run.BeginKindling("a", 1));
            float needed = SurvivalRules.KindleSeconds(40); Assert.Greater(needed, 8);
            double t = Advance(run, 1, needed - 1); Assert.AreEqual(0, run.FireUntil, "not yet");
            t = Advance(run, t, 1.5); Assert.Greater(run.FireUntil, t, "lit"); Assert.IsNull(a.KindlingStarted);
            Assert.IsTrue(run.Events.Any(e => e.Text.Contains("окоченевшими руками")));
            Assert.IsFalse(run.BeginKindling("a", t), "already burning");
            float rem = run.FireRemaining(t); Assert.IsTrue(rem > 85 && rem <= 90);
            float hands = a.Hands; Advance(run, t, 20);
            Assert.Greater(a.Hands, hands + 20, "fire restores hands"); Assert.AreEqual(100f, a.Heat); Assert.Less(b.Heat, 100f, "far player still cools");
        }

        [Test]
        public void PersonalOutcomesReconnectAndPairOutcome()
        {
            var run = new NightRun(0, Ground);
            var a = run.AddPlayer("a", "А", 0); var b = run.AddPlayer("b", "Б", 0);
            run.Move("a", WorldData.Tent.X + 3, WorldData.Tent.Z + 3, 0);
            double t = Advance(run, 0, 1);
            Assert.AreEqual(Outcome.Arrival, a.Outcome); Assert.AreEqual(Outcome.None, run.Outcome, "room waits for the partner");
            run.SetOffline("b", t); float heat = b.Heat; t = Advance(run, t, 30); Assert.AreEqual(heat, b.Heat, "offline players do not lose heat");
            Assert.AreSame(b, run.AddPlayer("b", "Б", t)); Assert.IsTrue(b.Online);
            b.Heat = .1f; t = Advance(run, t, 10); Assert.AreEqual(Outcome.Cold, b.Outcome);
            Assert.AreEqual(Outcome.Separated, run.Outcome);
            StringAssert.Contains("разделённая пара", run.Events.Last().Text);
            Assert.IsFalse(run.Step(t + 1000), "finished runs are frozen");
        }

        [Test]
        public void OfflineBeyondGraceLeavesTheNightAndDawnClosesTheRoom()
        {
            var run = new NightRun(0, Ground);
            run.AddPlayer("a", "А", 0); run.AddPlayer("b", "Б", 0);
            run.SetOffline("b", 0);
            double t = Advance(run, 0, NightRun.GraceSeconds + 1);
            Assert.AreEqual(1, run.Players.Count);
            Assert.IsTrue(run.Events.Any(e => e.Text.Contains("выбывает")));
            var a = run.Players["a"];
            while (run.Elapsed < SurvivalRules.NightSeconds && run.Outcome == Outcome.None) { a.Heat = 100; t = Advance(run, t, 10, 1); }
            Assert.AreEqual(Outcome.Dawn, a.Outcome); Assert.AreEqual(Outcome.Dawn, run.Outcome);
        }
    }
}
