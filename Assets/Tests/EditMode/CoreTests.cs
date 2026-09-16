using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    public static class TestData
    {
        /// <summary>Finds Assets/Data/terrain.png from the Unity project root or from the standalone test runner.</summary>
        public static byte[] TerrainPng()
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                string candidate = Path.Combine(dir, "Assets", "Data", "terrain.png");
                if (File.Exists(candidate)) return File.ReadAllBytes(candidate);
                dir = Path.GetDirectoryName(dir);
            }
            throw new FileNotFoundException("Assets/Data/terrain.png");
        }
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
        [Test]
        public void HistoricalPointsFitTheTileAndKeepTheirSeparation()
        {
            foreach (var p in WorldData.Pois) Assert.IsTrue(Math.Abs(p.X) < WorldData.Size / 2 && Math.Abs(p.Z) < WorldData.Size / 2, p.Id);
            float d = WorldData.Distance(WorldData.Cedar.X, WorldData.Cedar.Z, WorldData.Tent.X, WorldData.Tent.Z);
            Assert.IsTrue(d > 1300 && d < 1600, $"cedar–tent {d}");
            Assert.Less(WorldData.Distance(WorldData.Cedar.X, WorldData.Cedar.Z, WorldData.Ravine.X, WorldData.Ravine.Z), 65f);
        }

        [Test]
        public void SamplingRespectsPixelCentresAndInterpolates()
        {
            var a = new float[65536]; for (int i = 0; i < a.Length; i++) a[i] = i % 256;
            Assert.AreEqual(127.5f, WorldData.Sample(a, 0, 0), 1e-3);
            Assert.AreEqual(0f, WorldData.Sample(a, (float)(-WorldData.Size / 2), 0), 1e-3);
            Assert.AreEqual(255f, WorldData.Sample(a, (float)(WorldData.Size / 2), 0), 1e-3);
        }

        [Test]
        public void DemDecodesAndPutsTheCampBelowTheTreeLine()
        {
            Assert.Throws<InvalidDataException>(() => Dem.DecodePng(new byte[] { 1, 2, 3 }));
            var dem = Dem.LoadHeights(TestData.TerrainPng());
            float camp = WorldData.GroundHeight(dem, WorldData.Camp.x, WorldData.Camp.z);
            float tent = WorldData.GroundHeight(dem, WorldData.Tent.X, WorldData.Tent.Z);
            Assert.IsTrue(camp > 600 && camp < WorldData.ShelterHeight, $"camp {camp}");
            Assert.Greater(tent, camp);
        }
    }

    public class NightRunTests
    {
        static float[] dem;
        static float Ground(float x, float z) => WorldData.GroundHeight(dem ??= Dem.LoadHeights(TestData.TerrainPng()), x, z);

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
