using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Height1079.Core;
using Height1079.Runtime;

namespace Height1079.Tests
{
    public class MenkTests
    {
        static MenkBrain.Seen Player(Vector3 pos, float look = 0f, bool torch = false) =>
            new MenkBrain.Seen { Token = "c1", Client = 1, Pos = pos, Look = look, Torch = torch, Alive = true };

        static (MenkBrain brain, List<string> hits, List<string> lines) Make()
        {
            var brain = new MenkBrain((x, z) => 0f);
            var hits = new List<string>(); var lines = new List<string>();
            brain.Hit = (t, d, dmg) => hits.Add(t);
            brain.Say = lines.Add;
            return (brain, hits, lines);
        }

        static void Run(MenkBrain b, ref float clock, float seconds, List<MenkBrain.Seen> players, System.Func<bool> stop = null)
        {
            for (float t = 0; t < seconds; t += .05f)
            {
                clock += .05f;
                b.Tick(.05f, clock, 0f, players);
                if (stop != null && stop()) return;
            }
        }

        [Test]
        public void StandsStillThenBeatsTheTentThenHunts()
        {
            var (b, hits, lines) = Make();
            var start = b.Pos;
            // a player warming at the fire, 30-odd metres from the "tree"
            var fire = new Vector3(WorldData.Camp.x, 0, WorldData.Camp.z);
            var players = new List<MenkBrain.Seen> { Player(fire) };
            float clock = 0f;
            Run(b, ref clock, 150f, players);
            Assert.AreEqual(MenkBrain.State.Tree, b.Mode);
            Assert.AreEqual(start, b.Pos);
            Run(b, ref clock, 60f, players, () => b.Mode == MenkBrain.State.Beating);
            Assert.AreEqual(MenkBrain.State.Beating, b.Mode, "walks to the camp tent and beats it");
            var pad = new Vector3(WorldData.CampTentPad.x, 0, WorldData.CampTentPad.z);
            Assert.Less(Vector3.Distance(new Vector3(b.Pos.x, 0, b.Pos.z), pad), 3.2f);
            Run(b, ref clock, 10f, players, () => b.Mode == MenkBrain.State.Hunting);
            Assert.AreEqual(4, b.TentBlows);
            Assert.AreEqual(MenkBrain.State.Hunting, b.Mode);
            Run(b, ref clock, 30f, players, () => hits.Count > 0);
            Assert.AreEqual(1, hits.Count, "catches a player who stands still");
            Assert.IsTrue(lines.Count >= 2);
        }

        [Test]
        public void ComingCloseWakesItAndATorchFreezesIt()
        {
            var (b, hits, _) = Make();
            float clock = 70f;
            var near = b.Pos + new Vector3(10f, 0, 0);
            var players = new List<MenkBrain.Seen> { Player(near) };
            Run(b, ref clock, 1f, players);
            Assert.AreEqual(MenkBrain.State.Waking, b.Mode);
            Run(b, ref clock, 4f, players);
            Assert.AreEqual(MenkBrain.State.ToTent, b.Mode);
            // shine the tube torch straight at it
            var to = b.Pos - near;
            float look = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            players[0] = Player(near, look, torch: true);
            Run(b, ref clock, .2f, players);
            Assert.AreEqual(MenkBrain.State.Frozen, b.Mode);
            var frozenAt = b.Pos;
            Run(b, ref clock, 3f, players);
            Assert.AreEqual(frozenAt, b.Pos, "it does not move while it pretends");
            Assert.AreEqual(0, hits.Count);
        }
    }
}
