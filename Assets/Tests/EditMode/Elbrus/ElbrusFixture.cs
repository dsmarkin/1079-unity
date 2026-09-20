using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    /// <summary>Puts Elbrus on the map before any test in this assembly runs — the test-runner counterpart of the
    /// [RuntimeInitializeOnLoadMethod] hook a player uses and of the [ModuleInitializer] in Tools/CoreTests/Shim.cs
    /// (see Height1079.Core.Locations). Registration is idempotent, so it does not matter who gets there first.</summary>
    [SetUpFixture]
    public class ElbrusFixture
    {
        [OneTimeSetUp]
        public void Register() => ElbrusLocation.Register();
    }

    /// <summary>The one piece of NightRunTests that belongs to Elbrus: only this location sleeps a night and wakes
    /// on the next day, so only here does the clock have to go back to the morning.</summary>
    public class ElbrusNightRunTests
    {
        static float Ground(float x, float z) => WorldData.GroundHeight(TestData.Dem, x, z);

        static double Advance(NightRun run, double from, double seconds, double step = .25)
        {
            double t = from;
            for (int i = 0; i < (int)(seconds / step); i++) { t += step; run.Step(t); }
            return t;
        }

        [Test]
        public void ANightSleptPutsTheClockBackToTheMorning()
        {
            var run = new NightRun(0, Ground, ElbrusLocation.Plan);
            double t = Advance(run, 1, 600);
            Assert.Greater(run.Elapsed, 590f);
            float wasHour = AscentRoute.HourAt(run.Elapsed, ElbrusLocation.Plan.Profile.Seconds);
            Assert.Greater(wasHour, AscentRoute.RunStartHour);
            run.NewMorning();
            Assert.AreEqual(0f, run.Elapsed, 1e-3);
            // and the clock stays there: the skipped hours are given back, not merely hidden for one tick
            t = Advance(run, t, 10);
            Assert.AreEqual(10f, run.Elapsed, .5f);
            Assert.AreEqual(AscentRoute.RunStartHour,
                AscentRoute.HourAt(run.Elapsed, ElbrusLocation.Plan.Profile.Seconds), .05f);
        }
    }
}
