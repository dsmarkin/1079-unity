using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>A climber's body: one torso rigidbody floating over its feet on a spring, held upright by a PD
    /// controller, with two hands (<see cref="PuppetHand"/>) that can close on the world and carry it. One stamina bar
    /// pays for everything, and when it runs out the hands open and the body goes limp until the legs are back under it.
    ///
    /// Nothing here knows about the network, the night or the mountain: it takes a <see cref="PuppetInput"/> and a
    /// <see cref="PuppetTuning"/> and does physics. That is what makes it testable in the sandbox.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class Puppet : MonoBehaviour
    {
        public PuppetTuning Tuning = new PuppetTuning();

        public Rigidbody Torso { get; private set; }
        CapsuleCollider capsule;
        PuppetHand left, right;
        public PuppetHand Left => left;
        public PuppetHand Right => right;

        PuppetInput input = PuppetInput.Idle;
        public Quaternion LookRotation => input.Look;

        public bool Grounded { get; private set; }
        /// <summary>The probe found something close under the feet — walkable or not.</summary>
        public bool Footing { get; private set; }
        /// <summary>Standing on something too steep to hold: the body goes down the fall line and the player only steers.</summary>
        public bool Sliding { get; private set; }
        /// <summary>Angle of whatever is under the feet, degrees.</summary>
        public float SlopeAngle { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        /// <summary>Metres from the feet to whatever is under them, or +∞ over a drop.</summary>
        public float GroundDistance { get; private set; } = float.PositiveInfinity;
        public bool Hanging => HandsEnabled
                            && ((left != null && left.Now == PuppetHand.State.Gripping)
                             || (right != null && right.Now == PuppetHand.State.Gripping));

        public float Stamina { get; private set; }
        public float StaminaFraction => Tuning.Stamina > 0f ? Mathf.Clamp01(Stamina / Tuning.Stamina) : 0f;
        /// <summary>Empty hands: everything lets go and nothing closes until it is over.</summary>
        public bool Exhausted => exhaustUntil > Time.time;
        /// <summary>Tired hands hold worse — a grip taken on the last of the stamina tears off.</summary>
        public float GripStrength => Mathf.Lerp(.35f, 1f, StaminaFraction);
        /// <summary>Thrown, torn off or spent: the body is a sack until the feet find the ground again.</summary>
        public bool Limp { get; private set; }

        public int GrabMask = ~0;
        /// <summary>Whether the hands do anything at all. Off, this is a body that walks, falls and slides and
        /// nothing else — which is the part that has to be right before climbing is worth writing.</summary>
        public bool HandsEnabled = true;
        float exhaustUntil, restTimer, limpUntil;
        /// <summary>0…1 — how much of the leg spring is allowed right now (see <see cref="PuppetTuning.LegRise"/>).</summary>
        float legs;
        /// <summary>Speed the body hit the ground with, m/s — what a fall-damage rule would read.</summary>
        public float LastImpact { get; private set; }
        float fallSpeed;

        public System.Action<PuppetHand> Grabbed, Released;
        public System.Action<float> Landed;

        void Awake() => Init();

        /// <summary>Everything Awake would do, callable by hand. The EditMode tests build a body outside play mode,
        /// where Unity never calls Awake at all: without this the torso was null and the stamina zero in every test.</summary>
        public void Init()
        {
            if (Torso != null) return;
            Torso = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            Torso.interpolation = RigidbodyInterpolation.Interpolate;
            Torso.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Torso.freezeRotation = false;
            Torso.angularDamping = 3f;
            Stamina = Tuning.Stamina;
        }

        /// <summary>Called by the rig once the hands exist.</summary>
        public void Bind(PuppetHand l, PuppetHand r)
        {
            Init();
            left = l; right = r;
            left.Setup(this, -1f); right.Setup(this, 1f);
        }

        public bool IsOwnHand(Collider c)
        {
            var rb = c.attachedRigidbody;
            if (rb == null) return false;
            return (left != null && rb == left.GetComponent<Rigidbody>()) || (right != null && rb == right.GetComponent<Rigidbody>());
        }

        public void Drive(PuppetInput next) => input = next;

        /// <summary>Put the body somewhere and let go of everything — the sandbox's teleport and the game's respawn.</summary>
        public void Place(Vector3 position)
        {
            left?.Release(); right?.Release();
            Torso.position = position; transform.position = position;
            Torso.linearVelocity = Vector3.zero; Torso.angularVelocity = Vector3.zero;
            Torso.rotation = Quaternion.identity;
            Limp = false; limpUntil = 0f; exhaustUntil = 0f; fallSpeed = 0f; legs = 0f;
            Stamina = Tuning.Stamina;
            if (left != null) left.transform.position = position;
            if (right != null) right.transform.position = position;
        }

        public void GoLimp(float seconds)
        {
            Limp = true;
            limpUntil = Mathf.Max(limpUntil, Time.time + seconds);
            left?.Release(); right?.Release();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            var t = Tuning;
            Torso.mass = t.TorsoMass;
            capsule.height = t.TorsoHeight; capsule.radius = t.TorsoRadius;

            Sense(t);
            float drain = 0f;

            bool exhausted = Exhausted || Limp;
            if (HandsEnabled)
            {
                drain += left.Tick(input.GrabLeft && !Limp, input.PullUp, exhausted, dt);
                drain += right.Tick(input.GrabRight && !Limp, input.PullUp, exhausted, dt);
            }
            else if (Hanging) { left.Release(); right.Release(); }

            if (Limp && Grounded && Time.time > limpUntil && StaminaFraction > .25f) Limp = false;

            // the legs come back up to strength over LegRise seconds after they find the ground; off the ground, or
            // while the body is a sack, they have nothing to push against at all
            bool standing = Grounded && !Limp && !Hanging && !Sliding;
            legs = standing ? Mathf.MoveTowards(legs, 1f, dt / Mathf.Max(t.LegRise, .01f)) : 0f;

            if (!Limp)
            {
                if (Grounded && !Hanging) Hover(t);
                else if (Sliding && !Hanging) Slide(t);
                Upright(t);
                Move(t, dt, ref drain);
                if (input.Jump && Grounded && !Hanging && Stamina > 8f)
                {
                    Torso.linearVelocity = new Vector3(Torso.linearVelocity.x, 0f, Torso.linearVelocity.z);
                    Torso.AddForce(Vector3.up * 4.6f, ForceMode.VelocityChange);
                    Stamina -= 8f;
                    restTimer = 0f;
                }
            }

            Spend(drain, dt);
        }

        /// <summary>What is under the feet, and how hard the last landing was.</summary>
        void Sense(PuppetTuning t)
        {
            float toFeet = t.HoverHeight;
            var origin = Torso.position;
            float radius = Mathf.Max(.05f, t.TorsoRadius * .9f);
            float reach = toFeet - radius + t.LegProbe;
            bool hit = Physics.SphereCast(origin, radius, Vector3.down, out var info, reach, GrabMask, QueryTriggerInteraction.Ignore)
                       && info.collider.attachedRigidbody != Torso && !IsOwnHand(info.collider);
            if (hit)
            {
                GroundDistance = info.distance + radius;
                GroundNormal = info.normal;
                SlopeAngle = Vector3.Angle(info.normal, Vector3.up);
                Footing = GroundDistance <= toFeet + t.LegProbe * .5f;
                Grounded = Footing && SlopeAngle <= t.FootGrip;
                // too steep to stand on is not the same as thin air: the body keeps touching the slope all the way down
                Sliding = Footing && !Grounded;
            }
            else { GroundDistance = float.PositiveInfinity; GroundNormal = Vector3.up; SlopeAngle = 0f; Footing = Grounded = Sliding = false; }

            float vy = Torso.linearVelocity.y;
            if (!Grounded && vy < 0f) fallSpeed = -vy;
            else if (Grounded && fallSpeed > 1f)
            {
                LastImpact = fallSpeed;
                Landed?.Invoke(fallSpeed);
                // a real drop throws the body down: this is where the game's fall damage and the ragdoll hook in
                if (fallSpeed > 9f) GoLimp(Mathf.Clamp(fallSpeed * .12f, .5f, 3f));
                fallSpeed = 0f;
            }
            else if (Grounded) fallSpeed = 0f;
        }

        /// <summary>The legs: a spring that holds the torso at its ride height over whatever the probe found, so steps,
        /// ledges and broken ground cost nothing and the body never catches on a lip.</summary>
        void Hover(PuppetTuning t)
        {
            if (float.IsInfinity(GroundDistance)) return;
            float error = t.HoverHeight - GroundDistance;
            float vy = Vector3.Dot(Torso.linearVelocity, Vector3.up);
            float a = Mathf.Clamp(error * t.LegSpring - vy * t.LegDamper, -t.LegMaxAccel, t.LegMaxAccel) * legs;
            Torso.AddForce(Vector3.up * a, ForceMode.Acceleration);
            // legs stand a body up, they do not launch it
            var now = Torso.linearVelocity;
            if (error > 0f && now.y > t.LegLift) Torso.linearVelocity = new Vector3(now.x, t.LegLift, now.z);
            // and nothing creeps downhill: cancel the part of gravity the slope would otherwise turn into speed
            Torso.AddForce(-Vector3.ProjectOnPlane(Physics.gravity, GroundNormal), ForceMode.Acceleration);
        }

        /// <summary>Too steep to stand: the body runs down the fall line, the boots scrub off part of it, and the
        /// player is left with a little steering. The legs still hold the body off the rock — a capsule dragged along
        /// the surface catches on every facet and jitters — but at a fraction of their strength.</summary>
        void Slide(PuppetTuning t)
        {
            if (float.IsInfinity(GroundDistance)) return;
            // along the NORMAL, not along the world's vertical. Damping the vertical speed of a sliding body damps
            // the slide itself: the first version of this held the climber on a sixty-degree slope at walking pace.
            float gap = (t.HoverHeight - GroundDistance) * Mathf.Cos(SlopeAngle * Mathf.Deg2Rad);
            float closing = Vector3.Dot(Torso.linearVelocity, GroundNormal);
            float lift = Mathf.Clamp(gap * t.LegSpring - closing * t.LegDamper, -t.LegMaxAccel, t.LegMaxAccel) * t.SlideLift;
            Torso.AddForce(GroundNormal * lift, ForceMode.Acceleration);

            var fall = Vector3.ProjectOnPlane(Vector3.down, GroundNormal);   // length is sin(slope)
            float pull = Physics.gravity.magnitude * fall.magnitude * (1f - Mathf.Clamp01(t.SlideFriction));
            if (fall.sqrMagnitude > 1e-6f) Torso.AddForce(fall.normalized * pull, ForceMode.Acceleration);

            var speed = Torso.linearVelocity;
            var flat = new Vector3(speed.x, 0f, speed.z);
            if (flat.magnitude > t.SlideTop)
            {
                flat = flat.normalized * t.SlideTop;
                Torso.linearVelocity = new Vector3(flat.x, speed.y, flat.z);
            }
        }

        /// <summary>Standing up and facing the way the player looks. Two PD controllers on the same rigidbody: one
        /// rights the body, one turns it. Hanging, the body is allowed to swing — only the yaw is kept.</summary>
        void Upright(PuppetTuning t)
        {
            var up = Vector3.up;
            float authority = Grounded ? 1f : Hanging ? .45f : .25f;
            var delta = Quaternion.FromToRotation(transform.up, up);
            delta.ToAngleAxis(out float angle, out var axis);
            if (angle > 180f) angle -= 360f;
            if (!float.IsNaN(axis.x) && Mathf.Abs(angle) > .01f)
            {
                var torque = axis.normalized * (angle * Mathf.Deg2Rad * t.UprightSpring * authority)
                           - Torso.angularVelocity * t.UprightDamper;
                Torso.AddTorque(torque, ForceMode.Acceleration);
            }
            var want = Vector3.ProjectOnPlane(LookRotation * Vector3.forward, up);
            if (want.sqrMagnitude < 1e-4f) return;
            var flat = Vector3.ProjectOnPlane(transform.forward, up);
            float yawErr = Vector3.SignedAngle(flat, want, up) * Mathf.Deg2Rad;
            float yawRate = Vector3.Dot(Torso.angularVelocity, up);
            Torso.AddTorque(up * (yawErr * t.TurnSpring * authority - yawRate * t.TurnDamper), ForceMode.Acceleration);
        }

        void Move(PuppetTuning t, float dt, ref float drain)
        {
            var look = LookRotation;
            var wish = look * new Vector3(input.Move.x, 0f, input.Move.y);
            wish.y = 0f;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            bool running = input.Run && wish.sqrMagnitude > .01f && Grounded && !Hanging && Stamina > 1f;
            float speed = running ? t.RunSpeed : t.WalkSpeed;
            if (running) drain += t.RunCost * dt;

            if (Sliding && !Hanging)
            {
                // the feet are gone: steering only, and it is worth about a fifth of a step
                Torso.AddForce(wish * t.SlideControl, ForceMode.Acceleration);
                return;
            }
            if (Grounded && !Hanging)
            {
                // uphill costs, downhill pays: the grade under the next step, −1 straight down to +1 straight up
                var alongSlope = Vector3.ProjectOnPlane(wish, GroundNormal);
                float grade = alongSlope.sqrMagnitude > 1e-6f ? Vector3.Dot(alongSlope.normalized, Vector3.up) : 0f;
                float steep = Mathf.Sin(Mathf.Min(SlopeAngle, t.FootGrip) * Mathf.Deg2Rad) / Mathf.Max(Mathf.Sin(t.FootGrip * Mathf.Deg2Rad), .01f);
                speed *= grade > 0f ? Mathf.Lerp(1f, t.UphillSpeed, grade * steep)
                                    : Mathf.Lerp(1f, t.DownhillSpeed, -grade * steep);
                var along = Vector3.ProjectOnPlane(wish, GroundNormal).normalized * (wish.magnitude * speed);
                var v = Torso.linearVelocity;
                var flat = new Vector3(v.x, 0f, v.z);
                var err = new Vector3(along.x, 0f, along.z) - flat;
                var a = Vector3.ClampMagnitude(err / Mathf.Max(dt, 1e-4f), t.GroundAccel);
                Torso.AddForce(a, ForceMode.Acceleration);
            }
            else
            {
                // in the air, or hanging: a little steering, that is all. Hanging, this is what swings the body.
                Torso.AddForce(wish * t.AirAccel, ForceMode.Acceleration);
            }
        }

        void Spend(float drain, float dt)
        {
            if (drain > 0f) { Stamina -= drain; restTimer = 0f; }
            else if (Grounded && !Hanging)
            {
                restTimer += dt;
                if (restTimer > Tuning.RestDelay) Stamina += Tuning.Recovery * dt;
            }
            Stamina = Mathf.Clamp(Stamina, 0f, Tuning.Stamina);
            if (Stamina <= 0f && !Exhausted)
            {
                exhaustUntil = Time.time + Tuning.ExhaustLock;
                left?.Release(); right?.Release();
                if (!Grounded) GoLimp(Tuning.ExhaustLock);
            }
        }

        internal void OnGrabbed(PuppetHand h) => Grabbed?.Invoke(h);
        internal void OnReleased(PuppetHand h) => Released?.Invoke(h);
    }
}
