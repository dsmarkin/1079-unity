using NUnit.Framework;
using UnityEngine;
using Height1079.Puppet;

namespace Height1079.Tests
{
    /// <summary>The body of the physics sandbox. These do not simulate — EditMode does not step physics — they check
    /// the things that silently break the sandbox: a rig that is missing a hand, a tuning that does not survive a
    /// round trip through the file it is saved in, hands that collide with the body they belong to.</summary>
    public class PuppetTests
    {
        Puppet.Puppet rig;

        [TearDown]
        public void Cleanup()
        {
            if (rig != null) Object.DestroyImmediate(rig.gameObject);
            rig = null;
        }

        [Test]
        public void RigHasTorsoAndTwoHands()
        {
            var t = new PuppetTuning { TorsoMass = 77f, HandMass = 4f };
            rig = PuppetRig.Build(Vector3.up, t, "TestBody");
            Assert.NotNull(rig.Torso, "нет тела");
            Assert.NotNull(rig.Left, "нет левой руки");
            Assert.NotNull(rig.Right, "нет правой руки");
            Assert.AreEqual(77f, rig.Torso.mass, .001f);
            Assert.AreEqual(PuppetHand.State.Free, rig.Left.Now);
            Assert.AreEqual(PuppetHand.State.Free, rig.Right.Now);
        }

        [Test]
        public void ShouldersSitOnOppositeSides()
        {
            var t = new PuppetTuning { ShoulderOut = .25f, ShoulderUp = .4f };
            rig = PuppetRig.Build(Vector3.zero, t, "TestBody");
            var l = rig.Left.Shoulder; var r = rig.Right.Shoulder;
            Assert.Less(l.x, r.x, "плечи перепутаны местами");
            Assert.AreEqual(.5f, Vector3.Distance(l, r), .001f);
            Assert.AreEqual(.4f, l.y, .001f);
        }

        [Test]
        public void FullStaminaAtStartAndNotExhausted()
        {
            rig = PuppetRig.Build(Vector3.zero, new PuppetTuning { Stamina = 120f }, "TestBody");
            Assert.AreEqual(1f, rig.StaminaFraction, .001f);
            Assert.IsFalse(rig.Exhausted);
            Assert.IsFalse(rig.Hanging);
            // a tired climber holds worse — that is the whole difficulty curve of a wall
            Assert.AreEqual(1f, rig.GripStrength, .001f);
        }

        [Test]
        public void PlaceClearsSpeedAndPutsTheBodyWhereAsked()
        {
            rig = PuppetRig.Build(Vector3.zero, new PuppetTuning(), "TestBody");
            rig.Torso.linearVelocity = new Vector3(3f, -9f, 1f);
            rig.Place(new Vector3(5f, 2f, -4f));
            Assert.AreEqual(new Vector3(5f, 2f, -4f), rig.Torso.position);
            Assert.AreEqual(Vector3.zero, rig.Torso.linearVelocity);
            Assert.IsFalse(rig.Limp);
        }

        [Test]
        public void TuningSurvivesTheFileItIsSavedIn()
        {
            var t = new PuppetTuning { Name = "unit-test", TorsoMass = 81.5f, GripBreakForce = 4321f, PhysicsRate = 120 };
            t.Save();
            var back = PuppetTuning.Load("unit-test");
            Assert.AreEqual(81.5f, back.TorsoMass, .001f);
            Assert.AreEqual(4321f, back.GripBreakForce, .001f);
            Assert.AreEqual(120, back.PhysicsRate);
            System.IO.File.Delete(PuppetTuning.PathFor("unit-test"));
        }

        [Test]
        public void MissingTuningFileGivesDefaults()
        {
            var t = PuppetTuning.Load("no-such-tuning-" + System.Guid.NewGuid().ToString("N"));
            Assert.AreEqual(new PuppetTuning().TorsoMass, t.TorsoMass, .001f);
        }

        [Test]
        public void GripOfAPlainColliderIsTheDefault()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Grip.Of(go.GetComponent<Collider>(), out float hold, out float cost, out bool slippery);
            Assert.AreEqual(1f, hold, .001f);
            Assert.AreEqual(1f, cost, .001f);
            Assert.IsFalse(slippery);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void GripReadsTheComponentAbove()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            child.transform.SetParent(go.transform);
            var g = go.AddComponent<Grip>();
            g.Hold = .4f; g.Cost = 2.5f; g.Slippery = true;
            Grip.Of(child.GetComponent<Collider>(), out float hold, out float cost, out bool slippery);
            Assert.AreEqual(.4f, hold, .001f);
            Assert.AreEqual(2.5f, cost, .001f);
            Assert.IsTrue(slippery);
            Object.DestroyImmediate(go);
        }
    }
}
