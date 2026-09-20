using System.Collections.Generic;
using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    /// <summary>The sandbox loop's rules (Core/Hunt.cs): the blizzard clock, the Menk's three senses and its hiding
    /// rules, the marks in the snow, the list of things and the debrief. Engine-free, so they run in dotnet.</summary>
    public class HuntTests
    {
        const float FireX = 0f, FireZ = -110f, TentX = 0f, TentZ = -60f, LabazX = 24f, LabazZ = -83f;

        static HuntSeen Standing(float x, float z, float look = 0f, bool torch = false, bool running = false, bool walking = false, bool low = false, bool covered = false)
            => new HuntSeen { Name = "Путник", X = x, Z = z, LookYaw = look, Torch = torch, Running = running, Walking = walking, Low = low, Covered = covered, Alive = true };

        static HunterBrain Menk(TrackChain tracks = null)
        {
            var b = new HunterBrain(TentX, TentZ, tracks ?? new TrackChain());
            return b;
        }

        static void Run(HunterBrain b, float seconds, List<HuntSeen> players, System.Func<bool> stop = null, float storm = 0f)
        {
            for (float t = 0f; t < seconds; t += .05f)
            {
                b.Tick(.05f, players, storm);
                if (stop != null && stop()) return;
            }
        }

        [Test]
        public void TheBlizzardRisesInTheLastThirdAndWallsTheYardAtTheEnd()
        {
            Assert.AreEqual(0f, HuntRules.Storm(0f), 1e-4);
            Assert.AreEqual(0f, HuntRules.Storm(600f), 1e-4);
            Assert.AreEqual(.5f, HuntRules.Storm(750f), 1e-3);
            Assert.AreEqual(1f, HuntRules.Storm(900f), 1e-4);
            Assert.IsTrue(HuntRules.IsWall(HuntRules.Storm(901f)));
            // a short run keeps the shape: two thirds calm, then the rise
            Assert.AreEqual(0f, HuntRules.Storm(120f, 180f), 1e-4);
            Assert.AreEqual(1f, HuntRules.Storm(180f, 180f), 1e-4);
            Assert.AreEqual(HuntRules.VisibilityNight, HuntRules.Visibility(0f), 1e-4);
            Assert.AreEqual(HuntRules.VisibilityStorm, HuntRules.Visibility(1f), 1e-4);
            Assert.AreEqual(HuntRules.TrackLifeStorm, HuntRules.TrackLifeAt(1f), 1e-4);
            Assert.AreEqual("12:05", HuntRules.Clock(725f));
        }

        [Test]
        public void WhatItSeesAndWhatItDoesNot()
        {
            var b = Menk();
            b.X = 0f; b.Z = 0f;
            // standing in the open, facing it: twelve metres; back turned: six
            Assert.IsTrue(b.Sees(Standing(0f, 10f, look: 180f), 10f), "facing it at 10 m");
            Assert.IsFalse(b.Sees(Standing(0f, 10f, look: 0f), 10f), "back turned at 10 m");
            Assert.IsTrue(b.Sees(Standing(0f, 5f, look: 0f), 5f), "back turned at 5 m");
            // behind a rock: only up close, whatever the pose
            Assert.IsFalse(b.Sees(Standing(0f, 5f, look: 180f, covered: true), 5f), "behind cover at 5 m");
            Assert.IsTrue(b.Sees(Standing(0f, 2.5f, look: 180f, covered: true), 2.5f), "cover does not help at 2.5 m");
            // flat in the open snow: eight
            Assert.IsTrue(b.Sees(Standing(0f, 6f, low: true), 6f));
            Assert.IsFalse(b.Sees(Standing(0f, 9f, low: true), 9f));
            // a lit torch lights the body up close, behind anything; further off it is a light to walk to, not a body
            Assert.IsTrue(b.Sees(Standing(0f, 15f, torch: true, covered: true, low: true), 15f));
            Assert.IsFalse(b.Sees(Standing(0f, 35f, torch: true), 35f));
        }

        [Test]
        public void ALitTorchCallsItAndItWalksToTheLight()
        {
            var b = Menk();
            // hidden behind a drift thirty metres off, torch on
            var players = new List<HuntSeen> { Standing(b.X + 30f, b.Z, torch: true, covered: true, low: true) };
            float before = HuntRules.Dist(b.X, b.Z, players[0].X, players[0].Z);
            Run(b, 6f, players, () => b.Mode == HunterBrain.State.ToLight);
            Assert.AreEqual(HunterBrain.State.ToLight, b.Mode);
            Assert.AreEqual(HunterBrain.Cue.Light, b.Why);
            Run(b, 3f, players);
            Assert.Less(HuntRules.Dist(b.X, b.Z, players[0].X, players[0].Z), before - 5f, "it has come a good way toward the light");
            Assert.AreEqual(1, b.WentToLight);
            Assert.AreEqual(HunterBrain.State.ToLight, b.Mode, "still a light, not yet a body");
            // torch off: it finishes the walk, looks round and goes back to the ring
            players[0] = Standing(players[0].X, players[0].Z, covered: true, low: true);
            Run(b, 30f, players, () => b.Mode == HunterBrain.State.LookAround);
            Assert.AreEqual(HunterBrain.State.LookAround, b.Mode);
            Run(b, 8f, players, () => b.Mode == HunterBrain.State.Patrol);
            Assert.AreEqual(HunterBrain.State.Patrol, b.Mode);
        }

        [Test]
        public void RunningIsHeardFromTwentyMetresWalkingOnlyFromSix()
        {
            var b = Menk();
            var walking = new List<HuntSeen> { Standing(b.X + 15f, b.Z, walking: true, covered: true, low: true) };
            Run(b, 3f, walking);
            Assert.AreNotEqual(HunterBrain.State.ToNoise, b.Mode, "a step at 15 m is not heard");
            var running = new List<HuntSeen> { Standing(b.X + 15f, b.Z, running: true, covered: true, low: true) };
            Run(b, 3f, running, () => b.Mode == HunterBrain.State.ToNoise);
            Assert.AreEqual(HunterBrain.State.ToNoise, b.Mode);
            Assert.AreEqual(1, b.WentToNoise);
        }

        [Test]
        public void AThingHittingTheSnowIsHeardFromThirtyMetresAndItLooksRoundWhereItLanded()
        {
            var b = Menk();
            var nobody = new List<HuntSeen>();
            b.Hear(b.X + 25f, b.Z, HuntRules.DropNoise);
            Assert.AreEqual(HunterBrain.State.ToNoise, b.Mode);
            Run(b, 20f, nobody, () => b.Mode == HunterBrain.State.LookAround);
            Assert.AreEqual(HunterBrain.State.LookAround, b.Mode);
            Assert.Less(HuntRules.Dist(b.X, b.Z, b.GoalX, b.GoalZ), 2.5f);
            b.Hear(b.X + 40f, b.Z, HuntRules.DropNoise);
            Assert.AreEqual(HunterBrain.State.LookAround, b.Mode, "forty metres is out of earshot");
        }

        [Test]
        public void WithNothingToChaseItWalksTheRingRoundTheTentAndStopsToListen()
        {
            var b = Menk();
            var nobody = new List<HuntSeen>();
            float x0 = b.X, z0 = b.Z;
            bool listened = false;
            for (float t = 0f; t < 60f; t += .05f)
            {
                b.Tick(.05f, nobody);
                if (b.Mode == HunterBrain.State.Listen) listened = true;
                float r = HuntRules.Dist(b.X, b.Z, TentX, TentZ);
                Assert.IsTrue(r > HuntRules.PatrolRadius - 3f && r < HuntRules.PatrolRadius + 3f, $"stays on the ring: r = {r:0.0}");
            }
            Assert.Greater(HuntRules.Dist(b.X, b.Z, x0, z0), 5f, "it walks");
            Assert.IsTrue(listened, "and stops to listen");
        }

        [Test]
        public void StandingInTheOpenInFrontOfItGetsYouKilled()
        {
            var b = Menk();
            var hits = new List<string>();
            b.Hit = hits.Add;
            var players = new List<HuntSeen> { Standing(b.X + 8f, b.Z, look: HuntRules.Heading(b.X + 8f, b.Z, b.X, b.Z)) };
            Run(b, 3f, players, () => b.Mode == HunterBrain.State.Chase);
            Assert.AreEqual(HunterBrain.State.Chase, b.Mode);
            Run(b, 8f, players, () => hits.Count > 0);
            Assert.AreEqual(1, hits.Count, "the blow lands on a player who does not move");
            Assert.AreEqual("Путник", hits[0]);
            Run(b, 3f, players, () => b.Mode == HunterBrain.State.LookAround);
            Assert.AreEqual(HunterBrain.State.LookAround, b.Mode, "then it looks round and goes on");
        }

        [Test]
        public void LosingSightItFollowsTheFreshestMarksAndTheWindTakesThem()
        {
            var tracks = new TrackChain();
            var b = Menk(tracks);
            // a chain of marks leading away east from where the player was seen
            float sx = b.X + 6f, sz = b.Z;
            for (int i = 0; i < 12; i++) tracks.Leave(0, sx + i * 1.5f, sz, i * .5f);
            var players = new List<HuntSeen> { Standing(sx, sz, look: HuntRules.Heading(sx, sz, b.X, b.Z)) };
            Run(b, 2f, players, () => b.Mode == HunterBrain.State.Chase);
            Assert.AreEqual(HunterBrain.State.Chase, b.Mode);
            // gone: hidden behind a rock further east, out of sight
            players[0] = Standing(sx + 40f, sz, covered: true, low: true);
            Run(b, 12f, players, () => b.Mode == HunterBrain.State.Tracking);
            Assert.AreEqual(HunterBrain.State.Tracking, b.Mode, "lost sight — follows the marks");
            Assert.AreEqual(1, b.WentByTracks);
            float xBefore = b.X;
            Run(b, 6f, players, () => b.Mode != HunterBrain.State.Tracking);
            Assert.Greater(b.X, xBefore + 3f, "and walks along the chain");

            // the marks: freshest first, next along the chain, gone after their life
            int f = tracks.Freshest(sx + 18f, sz, 30f);
            Assert.AreEqual(11, f);
            Assert.AreEqual(-1, tracks.Next(11));
            Assert.AreEqual(4, tracks.Next(3));
            tracks.Fade(now: 60f + 3f, life: HuntRules.TrackLife);
            Assert.AreEqual(6, tracks.Count, "marks older than a minute are gone");
            tracks.Fade(now: 6f + HuntRules.TrackLifeStorm, life: HuntRules.TrackLifeStorm);
            Assert.AreEqual(0, tracks.Count, "in a blizzard, fifteen seconds");
        }

        [Test]
        public void TheListAndWhatOnePairOfHandsCanDoWithIt()
        {
            var e = Errand.Standard(FireX, FireZ, TentX, TentZ);
            Assert.AreEqual(4, e.Total);
            var diary = e.Items.Find(i => i.Name == "дневник");
            var stove = e.Items.Find(i => i.Name == "печка");
            var roll = e.Items.Find(i => i.Carry == Carry.Pair);
            Assert.IsTrue(Errand.CanLift(diary));
            Assert.IsFalse(Errand.CanLift(roll), "the rolled tent takes two");
            Assert.IsTrue(Errand.CanLift(roll, 2));
            Assert.IsTrue(Errand.MayRun(Carry.Light) && Errand.MayTorch(Carry.Light) && Errand.MayLie(Carry.Light));
            Assert.IsFalse(Errand.MayRun(Carry.Heavy) || Errand.MayTorch(Carry.Heavy) || Errand.MayLie(Carry.Heavy));
            foreach (var i in e.Items) Assert.Less(HuntRules.Dist(i.X, i.Z, TentX, TentZ), 5f, i.Name + " lies at the tent");

            Assert.IsTrue(e.Take(diary, "А"));
            Assert.IsFalse(e.Take(stove, "А"), "one slot in the hands");
            Assert.AreSame(diary, e.Held("А"));
            Assert.IsFalse(e.PutDown("А", TentX, TentZ + 3f), "put down at the tent — not home");
            Assert.IsTrue(diary.OnSnow);
            Assert.IsTrue(e.Take(diary, "Б"));
            Assert.IsTrue(e.PutDown("Б", FireX + 3f, FireZ), "inside the fire's radius — home");
            Assert.IsTrue(diary.Delivered);
            Assert.AreEqual(1, e.Delivered);
            Assert.IsFalse(Errand.CanLift(diary), "and gone from the world");
            Assert.IsTrue(e.Take(stove, "А"));
            var dropped = e.Drop("А", 5f, -80f);
            Assert.AreSame(stove, dropped);
            Assert.AreEqual(5f, stove.X, 1e-4); Assert.AreEqual(-80f, stove.Z, 1e-4);
        }

        [Test]
        public void OutsideTheFireTheWallKillsInHalfAMinuteInsideItTheRunIsWon()
        {
            var run = new HuntRun(FireX, FireZ, TentX, TentZ, length: 180f);
            run.Join("Путник");
            // parked far from the Menk's ring and out of its sight, until the wall
            var away = new List<HuntSeen> { Standing(FireX + 20f, FireZ, covered: true, low: true) };
            for (float t = 0f; t < 179f; t += .1f) run.Tick(.1f, away);
            Assert.IsFalse(run.Wall);
            Assert.IsTrue(run.Find("Путник").Alive);
            for (float t = 0f; t < 31f; t += .1f) run.Tick(.1f, away);
            Assert.IsTrue(run.Wall);
            Assert.IsFalse(run.Find("Путник").Alive, "froze outside the fire");
            Assert.IsTrue(run.Over);
            Assert.AreEqual("все погибли", run.Outcome);
            Assert.IsTrue(run.Events.Exists(e => e.Text.Contains("замёрз")));

            var home = new HuntRun(FireX, FireZ, TentX, TentZ, length: 180f);
            home.Join("Путник");
            var atFire = new List<HuntSeen> { Standing(FireX + 2f, FireZ, covered: true, low: true) };
            for (float t = 0f; t < 195f; t += .1f) home.Tick(.1f, atFire);
            Assert.IsTrue(home.Over);
            Assert.AreEqual("вернулись", home.Outcome);
            Assert.IsTrue(home.Find("Путник").Alive);
        }

        [Test]
        public void TheRunKeepsAProtocolAndWritesTheDebrief()
        {
            var run = new HuntRun(FireX, FireZ, TentX, TentZ, length: 180f);
            run.Join("Путник");
            var diary = run.Errand.Items.Find(i => i.Name == "дневник");
            var roll = run.Errand.Items.Find(i => i.Carry == Carry.Pair);
            Assert.AreEqual("нужны двое", run.Take("Путник", roll));
            Assert.IsNull(run.Take("Путник", diary));
            Assert.AreEqual("руки заняты", run.Take("Путник", run.Errand.Items.Find(i => i.Name == "печка")));
            Assert.IsTrue(run.PutDown("Путник", FireX + 1f, FireZ + 1f));
            Assert.IsNull(run.Take("Путник", run.Errand.Items.Find(i => i.Name == "печка")));
            var thrown = run.Throw("Путник", TentX + 2f, TentZ + 2f);
            Assert.AreEqual("печка", thrown.Name);
            Assert.AreEqual(HunterBrain.State.ToNoise, run.Menk.Mode, "the stove hitting the snow is heard from the ring");
            var players = new List<HuntSeen> { Standing(TentX + 2f, TentZ + 2f) };
            run.Tick(.1f, players);
            run.Kill("Путник", "Менк");
            run.DropOnDeath("Путник", TentX + 2f, TentZ + 2f);
            var report = run.Report();
            Assert.IsTrue(report[0].StartsWith("Принесли 1 из 4"), report[0]);
            Assert.IsTrue(report.Exists(l => l.Contains("печка") && l.Contains("м от костра")), "where the stove lies");
            Assert.IsTrue(report.Exists(l => l.Contains("погиб на 1-й минуте (Менк)")));
            Assert.IsTrue(report.Exists(l => l.StartsWith("Менк шёл на свет")));
            Assert.IsTrue(run.Events.Exists(e => e.Text.Contains("взял: дневник")));
            Assert.IsTrue(run.Events.Exists(e => e.Text.Contains("У костра: дневник")));
        }

        [Test]
        public void TheCampHasTwoSectionsAndTheLabazJoinsTheBeatAfterTheTentsFirstThing()
        {
            var run = new HuntRun(FireX, FireZ, TentX, TentZ, length: 900f, labazX: LabazX, labazZ: LabazZ);
            run.Join("Путник");
            Assert.AreEqual(8, run.Errand.Total);
            Assert.AreEqual(4, run.Errand.TotalIn("лабаз"));
            foreach (var i in run.Errand.Items.FindAll(x => x.Section == "лабаз"))
                Assert.Less(HuntRules.Dist(i.X, i.Z, LabazX, LabazZ), 3f, i.Name + " lies at the labaz");
            Assert.IsFalse(run.LabazOpen);
            Assert.AreEqual(1, run.Menk.Posts);
            var diary = run.Errand.Items.Find(i => i.Name == "дневник");
            Assert.IsNull(run.Take("Путник", diary));
            Assert.IsTrue(run.PutDown("Путник", FireX + 1f, FireZ));
            Assert.IsTrue(run.LabazOpen, "the first thing home opens the labaz");
            Assert.AreEqual(2, run.Menk.Posts);
            Assert.IsTrue(run.Events.Exists(e => e.Text.StartsWith("Теперь лабаз")));

            // nobody about: the Menk's beat now reaches the labaz as well as the tent
            var nobody = new List<HuntSeen>();
            bool atTent = false, atLabaz = false;
            for (float t = 0f; t < 240f; t += .1f)
            {
                run.Tick(.1f, nobody);
                var m = run.Menk;
                if (HuntRules.Dist(m.X, m.Z, TentX, TentZ) < HuntRules.PatrolRadius + 3f) atTent = true;
                if (HuntRules.Dist(m.X, m.Z, LabazX, LabazZ) < HuntRules.PatrolRadius + 3f) atLabaz = true;
            }
            Assert.IsTrue(atTent && atLabaz, $"walks both rings: tent {atTent}, labaz {atLabaz}");
        }

        [Test]
        public void ThreeThingsHomeAndEveryoneAtTheFireEndsTheNightBeforeTheWall()
        {
            var run = new HuntRun(FireX, FireZ, TentX, TentZ, length: 900f, labazX: LabazX, labazZ: LabazZ);
            run.Join("Путник");
            foreach (var name in new[] { "дневник", "фотоаппарат" })
            {
                Assert.IsNull(run.Take("Путник", run.Errand.Items.Find(i => i.Name == name)));
                Assert.IsTrue(run.PutDown("Путник", FireX + 1f, FireZ));
            }
            Assert.IsFalse(run.QuotaMet);
            // at the fire with two home: the night goes on
            var atFire = new List<HuntSeen> { Standing(FireX + 2f, FireZ, covered: true, low: true) };
            for (float t = 0f; t < 8f; t += .1f) run.Tick(.1f, atFire);
            Assert.IsFalse(run.Over, "two is not the quota");
            Assert.IsNull(run.Take("Путник", run.Errand.Items.Find(i => i.Name == "сухари")));
            Assert.IsTrue(run.PutDown("Путник", FireX + 1f, FireZ));
            Assert.IsTrue(run.QuotaMet);
            Assert.IsTrue(run.Events.Exists(e => e.Text.StartsWith("Хватит.")));
            // away from the fire the finish does not count down
            var away = new List<HuntSeen> { Standing(FireX + 12f, FireZ, covered: true, low: true) };
            for (float t = 0f; t < 8f; t += .1f) run.Tick(.1f, away);
            Assert.IsFalse(run.Over, "the quota is met but the player is not at the fire");
            for (float t = 0f; t < HuntRules.FinishHold + .5f; t += .1f) run.Tick(.1f, atFire);
            Assert.IsTrue(run.Over);
            Assert.AreEqual("вернулись", run.Outcome);
            Assert.Less(run.Elapsed, 60f, "well before the wall");
            var report = run.Report();
            Assert.IsTrue(report[0].StartsWith("Принесли 3 из 8 (нужно 3)"), report[0]);
            Assert.IsTrue(report.Exists(l => l.StartsWith("Из палатки 2 из 4, из лабаза 1 из 4")), string.Join(" | ", report));
        }
    }
}
