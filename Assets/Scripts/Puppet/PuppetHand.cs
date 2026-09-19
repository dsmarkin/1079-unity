using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>One hand. Free, it is a small body on a spring that follows where the player points. Closed on
    /// something, it is nailed to that point and becomes the thing the whole climber hangs from: the pull on the
    /// shoulder is a rope of <see cref="PuppetTuning.ArmReach"/> metres, shortened while pulling up, and when the pull
    /// exceeds what the hold can take the hand tears off.
    ///
    /// This is deliberately not an active ragdoll. PEAK is reported to drive two hands and two feet directly and let a
    /// passive body follow them; forces on a handful of rigidbodies are stable at any frame rate, cheap to send over a
    /// network and, unlike joint drives, can be tuned by a person watching the screen.</summary>
    public sealed class PuppetHand : MonoBehaviour
    {
        public enum State : byte { Free = 0, Reaching = 1, Gripping = 2 }

        Puppet owner;
        Rigidbody body;
        SphereCollider ball;
        /// <summary>−1 left, +1 right.</summary>
        float side;

        public State Now { get; private set; }
        /// <summary>Where the hand closed, in the frame of whatever it closed on.</summary>
        public Vector3 GripPoint { get; private set; }
        /// <summary>What the hand is holding on to; null for the world itself.</summary>
        public Rigidbody Held { get; private set; }
        Transform heldOn;
        Vector3 heldLocal;
        float holdQuality = 1f, holdCost = 1f;
        /// <summary>Newtons the arm is pulling with right now — the HUD draws it and the break test reads it.</summary>
        public float Load { get; private set; }
        FixedJoint joint;

        static readonly Collider[] found = new Collider[24];

        public void Setup(Puppet puppet, float handSide)
        {
            owner = puppet; side = handSide;
            body = GetComponent<Rigidbody>();
            ball = GetComponent<SphereCollider>();
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.mass = owner.Tuning.HandMass;
            body.linearDamping = 1f; body.angularDamping = 4f;
        }

        public Vector3 Shoulder
        {
            get
            {
                var t = owner.Tuning;
                return owner.Torso.position + owner.Torso.transform.up * t.ShoulderUp
                     + owner.Torso.transform.right * (side * t.ShoulderOut);
            }
        }

        /// <summary>Where a hand that is not holding anything wants to be: at the hip when idle, out and up when the
        /// player is reaching. The height of the reach is what a climber aims with — look up and the hand goes up.</summary>
        Vector3 Target(bool reaching)
        {
            var t = owner.Tuning;
            var look = owner.LookRotation;
            Vector3 fwd = look * Vector3.forward, right = look * Vector3.right, up = Vector3.up;
            if (!reaching) return Shoulder + fwd * .18f + right * (side * .12f) - up * .34f;
            return Shoulder + fwd * (t.ArmReach * .80f) + right * (side * .20f) + up * (t.ArmReach * .34f);
        }

        /// <summary>One physics step of this hand. Returns what it costs the stamina this step.</summary>
        public float Tick(bool wantGrab, bool pullUp, bool exhausted, float dt)
        {
            var t = owner.Tuning;
            body.mass = t.HandMass;
            if (Now == State.Gripping)
            {
                if (!wantGrab || exhausted) { Release(); }
                else return HoldOn(pullUp, dt);
            }
            if (Now != State.Gripping)
            {
                bool reaching = wantGrab && !exhausted;
                Now = reaching ? State.Reaching : State.Free;
                Reach(Target(reaching), t);
                if (reaching) TryClose(t);
            }
            return 0f;
        }

        /// <summary>A critically damped spring to the wanted point, plus a hard stop at arm's length so the hand never
        /// ends up across the room when the body is thrown.</summary>
        void Reach(Vector3 target, PuppetTuning t)
        {
            var err = target - body.position;
            body.AddForce(err * t.ArmSpring - body.linearVelocity * t.ArmDamper, ForceMode.Acceleration);
            var fromShoulder = body.position - Shoulder;
            float len = fromShoulder.magnitude;
            if (len > t.ArmReach && len > 1e-4f)
            {
                var dir = fromShoulder / len;
                body.position = Shoulder + dir * t.ArmReach;
                float radial = Vector3.Dot(body.linearVelocity, dir);
                if (radial > 0f) body.linearVelocity -= dir * radial;
            }
        }

        /// <summary>Anything solid inside the palm closes the hand on it.</summary>
        void TryClose(PuppetTuning t)
        {
            int n = Physics.OverlapSphereNonAlloc(body.position, t.GrabRadius, found, owner.GrabMask, QueryTriggerInteraction.Ignore);
            Collider best = null; float bestDist = float.MaxValue; Vector3 bestPoint = default;
            for (int i = 0; i < n; i++)
            {
                var c = found[i];
                if (c == null || c.attachedRigidbody == owner.Torso) continue;
                if (c.attachedRigidbody == body) continue;
                if (owner.IsOwnHand(c)) continue;
                var p = c.ClosestPoint(body.position);
                float d = (p - body.position).sqrMagnitude;
                if (d >= bestDist) continue;
                bestDist = d; best = c; bestPoint = p;
            }
            if (best == null) return;
            Grip.Of(best, out holdQuality, out holdCost, out bool slippery);
            if (slippery || holdQuality <= .01f) return;

            Now = State.Gripping;
            Held = best.attachedRigidbody != null && !best.attachedRigidbody.isKinematic ? best.attachedRigidbody : null;
            heldOn = best.transform;
            GripPoint = bestPoint;
            heldLocal = heldOn.InverseTransformPoint(bestPoint);
            if (Held != null)
            {
                // something that can move: let the physics engine hold the two together
                joint = gameObject.AddComponent<FixedJoint>();
                joint.connectedBody = Held;
                joint.enableCollision = false;
            }
            else
            {
                // the mountain does not move: pinning the hand outright is steadier than any joint and costs nothing
                body.isKinematic = true;
                body.position = bestPoint;
            }
            owner.OnGrabbed(this);
        }

        /// <summary>Holding: the arm is a rope from the hand to the shoulder. Past its length it pulls the body in,
        /// and the pull is applied at the shoulder, so a climber hanging by one arm turns under it the way he should.</summary>
        float HoldOn(bool pullUp, float dt)
        {
            var t = owner.Tuning;
            if (heldOn == null) { Release(); return 0f; }
            if (body.isKinematic) body.position = heldOn.TransformPoint(heldLocal);
            GripPoint = body.position;

            var shoulder = Shoulder;
            var rope = shoulder - GripPoint;
            float len = rope.magnitude;
            float slack = t.ArmReach * (pullUp ? 1f - Mathf.Clamp01(t.PullIn / Mathf.Max(t.ArmReach, .01f)) : 1f);
            Load = 0f;
            if (len > slack && len > 1e-4f)
            {
                var dir = rope / len;                       // shoulder → hand is −dir
                float stretch = len - slack;
                float closing = Vector3.Dot(owner.Torso.linearVelocity, dir);
                float accel = stretch * t.GripSpring - closing * t.GripDamper;
                if (accel > 0f)
                {
                    var force = -dir * accel;               // pull the body toward the hand
                    owner.Torso.AddForceAtPosition(force * owner.Torso.mass, shoulder, ForceMode.Force);
                    Load = accel * owner.Torso.mass;
                    if (Held != null) Held.AddForceAtPosition(-force * owner.Torso.mass, GripPoint, ForceMode.Force);
                }
            }
            // the hold tears off when the arm pulls harder than the rock will take
            if (Load > t.GripBreakForce * holdQuality * owner.GripStrength) { Release(); return 0f; }

            float rate = pullUp ? t.PullCost : (owner.Grounded ? t.GripCost : t.HangCost);
            return rate * holdCost * dt;
        }

        public void Release()
        {
            if (joint != null) { PuppetRig.Kill(joint); joint = null; }
            if (body != null && body.isKinematic)
            {
                body.isKinematic = false;
                // leave with the speed of what was held, so letting go of a moving thing is not a full stop
                body.linearVelocity = owner.Torso.linearVelocity;
            }
            Held = null; heldOn = null; Load = 0f;
            if (Now == State.Gripping) owner.OnReleased(this);
            Now = State.Free;
        }
    }
}
