using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>Builds a climber: the torso capsule that the physics actually uses, the two hand bodies, and the
    /// <see cref="PuppetFigure"/> that draws all of it. No assets and no prefab — the meshes and the texture are turned
    /// in code by <see cref="PuppetSkin"/> — because the sandbox has to be able to throw a body away and make another
    /// one with a different tuning while the game runs.</summary>
    public static class PuppetRig
    {
        /// <summary>Destroy that also works in the editor: the EditMode tests build a rig, and <c>Object.Destroy</c>
        /// refuses to run outside play mode.</summary>
        internal static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        public static Puppet Build(Vector3 at, PuppetTuning tuning, string name = "Puppet")
        {
            var root = new GameObject(name);
            root.transform.position = at;

            var torso = root.AddComponent<Rigidbody>();
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = tuning.TorsoHeight; capsule.radius = tuning.TorsoRadius; capsule.center = Vector3.zero;
            capsule.material = Slippy();
            var puppet = root.AddComponent<Puppet>();
            puppet.Tuning = tuning;
            torso.mass = tuning.TorsoMass;

            var left = Hand(root.transform, at, "HandL", tuning);
            var right = Hand(root.transform, at, "HandR", tuning);
            puppet.Bind(left, right);

            // the hands must not shove the body they belong to
            Physics.IgnoreCollision(capsule, left.GetComponent<Collider>(), true);
            Physics.IgnoreCollision(capsule, right.GetComponent<Collider>(), true);
            Physics.IgnoreCollision(left.GetComponent<Collider>(), right.GetComponent<Collider>(), true);

            root.AddComponent<PuppetFigure>().Bind(puppet);
            return puppet;
        }

        static PuppetHand Hand(Transform parent, Vector3 at, string name, PuppetTuning tuning)
        {
            var go = new GameObject(name);
            go.transform.position = at + Vector3.up * tuning.ShoulderUp;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = tuning.HandMass;
            var ball = go.AddComponent<SphereCollider>();
            ball.radius = tuning.GrabRadius * .8f;
            ball.material = Grippy();
            // Nothing is drawn here. The hand a player sees is the sculpted one PuppetFigure puts at this very
            // point, and a sphere here as well left a bare ball sticking out through the fingers.
            return go.AddComponent<PuppetHand>();
        }

        /// <summary>The torso slides: friction on the capsule catches on every lip and fights the hover spring.</summary>
        static PhysicsMaterial Slippy() => new PhysicsMaterial("puppet-torso")
        { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };

        static PhysicsMaterial Grippy() => new PhysicsMaterial("puppet-hand")
        { dynamicFriction = .9f, staticFriction = 1f, frictionCombine = PhysicsMaterialCombine.Maximum };
    }

}
