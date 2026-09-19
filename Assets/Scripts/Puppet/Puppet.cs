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
        /// <summary>Thrown, torn off or spent: the body is a sack until the feet find the ground again. Nothing holds
        /// it upright, nothing holds it off the ground, and it has been thrown over — see <see cref="Tip"/>.</summary>
        public bool Limp { get; private set; }
        /// <summary>A stagger: a fraction of a second of the legs being somewhere else, off a landing that was not
        /// quite bad enough to put the body down. Control is not gone, only cut to <see cref="PuppetTuning.TripHold"/>.</summary>
        public bool Stumbling => stumbleUntil > Time.time;
        /// <summary>Metres the knees are giving under the body right now. The ride height really is pulled down by
        /// this much, so the figure drawn from the physics crouches without being told to.</summary>
        public float Crouch => squash;
        /// <summary>0…1 of a stand recovered since the body last went down. Scales the legs and the vertical together,
        /// so getting up takes <see cref="PuppetTuning.GetUp"/> seconds instead of snapping.</summary>
        public float Rise => rise;
        /// <summary>The up axis the body is trying to stand on this step: vertical, plus the lean its own acceleration
        /// has earned. Public because it is the body's intent, and a figure drawn from the physics may want it.</summary>
        public Vector3 StandUp { get; private set; } = Vector3.up;
        /// <summary>Degrees the torso is actually off the vertical. Past about fifty the body is on its way down.</summary>
        public float Tilt { get; private set; }

        // ─── which way the body is facing, and which way it is going: the contract with whatever draws it ───────────
        /// <summary>The way the body itself faces, as a yaw-only rotation about the world vertical.
        ///
        /// The capsule is turned to the camera and to nothing else (<see cref="Upright"/> chases
        /// <see cref="LookRotation"/>; no part of this body has ever turned toward the direction of travel). So a
        /// figure posed from this keeps looking where the player looks while the feet go wherever the sticks send
        /// them — sideways for a side-step, backwards for a retreat — which is how PEAK reads, and it is a fact about
        /// the physics rather than a trick in the drawing.
        ///
        /// While <see cref="Limp"/> the capsule is lying down and this is only the yaw it fell with: draw a fallen
        /// body from the rigidbody's own rotation, not from here.</summary>
        public Quaternion Facing { get; private set; } = Quaternion.identity;
        /// <summary>The same heading in degrees about the world vertical — for readouts and for tests that want to
        /// assert the body did <em>not</em> turn.</summary>
        public float FacingYaw { get; private set; }
        /// <summary>Where the body is actually travelling, in its own frame, m/s: x to its right, y straight ahead.
        /// A side-step is all x, walking backwards is a negative y, and the length of it is the pace. This is the
        /// whole of what a figure needs in order to plant the feet sideways instead of turning to face the way it is
        /// going. Measured from the rigidbody, so a shove from the world shows up in it exactly as a step does.</summary>
        public Vector2 Drift { get; private set; }

        public int GrabMask = ~0;
        /// <summary>Whether the hands do anything at all. Off, this is a body that walks, falls and slides and
        /// nothing else — which is the part that has to be right before climbing is worth writing.</summary>
        public bool HandsEnabled = true;
        float exhaustUntil, restTimer, limpUntil, stumbleUntil;
        /// <summary>When jump was last asked for, and how long the legs stay let go of after a launch. The first is a
        /// timestamp and not a flag on purpose — see <see cref="Drive"/> — and starts far in the past so that a body
        /// built in the first fraction of a second of the game does not jump on its own.</summary>
        float jumpAsked = -99f, jumpClearUntil;
        /// <summary>0…1 — how much of the leg spring is allowed right now (see <see cref="PuppetTuning.LegRise"/>).</summary>
        float legs;
        /// <summary>Metres the ride height is pulled down by the last landing, and 0…1 of a stand recovered since the
        /// body last lay down. Both scale what the legs are allowed to do, which is why they live beside them.</summary>
        float squash, rise = 1f;
        /// <summary>Horizontal velocity last step and the lagged acceleration read out of it. The lean is built on
        /// what the body is measurably doing rather than on what it was told to do, so a shove from the world tips it
        /// exactly as a step does, and nothing has to be told about the shove.</summary>
        Vector3 lastFlat, accel;
        /// <summary>The frictionless skin the body walks on, and the rough one it wears while it is down.</summary>
        PhysicsMaterial slick, rough;
        /// <summary>Speed the body hit the ground with, m/s — what a fall-damage rule would read.</summary>
        public float LastImpact { get; private set; }
        float fallSpeed;

        public System.Action<PuppetHand> Grabbed, Released;
        /// <summary>Speed the body arrived with, m/s, and the speed it left with — the two moments anything outside
        /// the body (sound, snow, a camera) wants to know about.</summary>
        public System.Action<float> Landed, Jumped;

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
            // remembered, not edited: the walking skin is a material this body owns, and a fall swaps it rather than
            // changing its numbers, so whatever the rig chose comes back exactly as it was
            slick = capsule.sharedMaterial;
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

        /// <summary>Hand the body this step's orders. Everything in <see cref="PuppetInput"/> is a state that can be
        /// read again next step — except jump, which arrives as "pressed during this frame" and is gone by the next.
        /// Frames and physics steps are not the same clock (90 Hz of body against whatever the screen is doing), so a
        /// frame that happens to contain no fixed step threw the press away and the player pressed space and nothing
        /// happened. So the press is latched here as a time, and the next step that can use it takes it.</summary>
        public void Drive(PuppetInput next)
        {
            input = next;
            if (next.Jump) jumpAsked = Time.time;
        }

        /// <summary>A jump was asked for recently enough to still count — see <see cref="PuppetTuning.JumpBuffer"/>.</summary>
        public bool JumpWanted => Time.time - jumpAsked <= Mathf.Max(Tuning.JumpBuffer, 0f);

        /// <summary>Put the body somewhere and let go of everything — the sandbox's teleport and the game's respawn.</summary>
        public void Place(Vector3 position)
        {
            left?.Release(); right?.Release();
            Torso.position = position; transform.position = position;
            Torso.linearVelocity = Vector3.zero; Torso.angularVelocity = Vector3.zero;
            Torso.rotation = Quaternion.identity;
            Limp = false; limpUntil = 0f; exhaustUntil = 0f; fallSpeed = 0f; legs = 0f;
            // a body put down by hand has not fallen: the knees, the stand and the lean all start clean, and the
            // remembered velocity is zeroed too or the teleport itself reads as an acceleration and tips the torso
            stumbleUntil = 0f; squash = 0f; rise = 1f; Tilt = 0f;
            lastFlat = Vector3.zero; accel = Vector3.zero; StandUp = Vector3.up;
            // and it has not asked for anything either: a jump latched before a teleport must not fire after it
            jumpAsked = -99f; jumpClearUntil = 0f;
            Facing = Quaternion.identity; FacingYaw = 0f; Drift = Vector2.zero;
            Rough(false);
            Stamina = Tuning.Stamina;
            if (left != null) left.transform.position = position;
            if (right != null) right.transform.position = position;
        }

        public void GoLimp(float seconds)
        {
            Limp = true;
            limpUntil = Mathf.Max(limpUntil, Time.time + seconds);
            // the legs go with the control: a sack does not hold itself off the ground, and the stand has to be
            // earned back over GetUp seconds afterwards rather than handed back with the flag
            legs = 0f; rise = 0f;
            Rough(true);
            left?.Release(); right?.Release();
        }

        /// <summary>The capsule is frictionless on purpose — friction catches on every lip and fights the leg spring —
        /// and a body lying on the ground is the one case where that is plainly wrong: it skates. So it wears a rough
        /// skin while it is down instead of the shared material having its numbers edited under everyone else.</summary>
        void Rough(bool on)
        {
            if (capsule == null) return;
            if (!on) { capsule.sharedMaterial = slick; return; }
            if (rough == null) rough = new PhysicsMaterial("puppet-down") { frictionCombine = PhysicsMaterialCombine.Average };
            float f = Mathf.Clamp01(Tuning.LimpFriction);
            rough.dynamicFriction = f; rough.staticFriction = f;
            capsule.sharedMaterial = rough;
        }

        void OnDestroy() { if (rough != null) PuppetRig.Kill(rough); }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            var t = Tuning;
            Torso.mass = t.TorsoMass;
            capsule.height = t.TorsoHeight; capsule.radius = t.TorsoRadius;

            Sense(t);
            Soften(t, dt);
            float drain = 0f;

            bool exhausted = Exhausted || Limp;
            if (HandsEnabled)
            {
                drain += left.Tick(input.GrabLeft && !Limp, input.PullUp, exhausted, dt);
                drain += right.Tick(input.GrabRight && !Limp, input.PullUp, exhausted, dt);
            }
            else if (Hanging) { left.Release(); right.Release(); }

            if (Limp && Grounded && Time.time > limpUntil && StaminaFraction > .25f) { Limp = false; Rough(false); }

            // the legs come back up to strength over LegRise seconds after they find the ground; off the ground, or
            // while the body is a sack, they have nothing to push against at all. A launch counts as off the ground
            // even though the probe still reports otherwise — see Launch, and JumpClear.
            bool launching = Time.time < jumpClearUntil;
            bool standing = Grounded && !Limp && !Hanging && !Sliding && !launching;
            legs = standing ? Mathf.MoveTowards(legs, 1f, dt / Mathf.Max(t.LegRise, .01f)) : 0f;

            if (!Limp)
            {
                if (Grounded && !Hanging && !launching) Hover(t);
                else if (Sliding && !Hanging) Slide(t);
                Upright(t);
                Move(t, dt, ref drain);
                // taken from the latch and not from this step's orders, so a press cannot fall between two frames.
                // Nothing else here is allowed to swallow it quietly: the legs' own ramp does not gate it (a man
                // jumps off legs that are still coming back), and the launch window is what stops it firing twice.
                if (JumpWanted && Grounded && !Hanging && !launching && Stamina >= t.JumpCost) Launch(t);
            }

            Spend(drain, dt);
        }

        /// <summary>Off the ground. Two things have to happen, and the second is the one that was missing.
        ///
        /// The speed is worked out from the height asked for and the scene's gravity (v = √(2gh)), so the jump is a
        /// size a person can judge rather than an impulse somebody guessed. It <em>replaces</em> the vertical speed
        /// instead of adding to it: the leg spring is underdamped and the body is always drifting a few centimetres a
        /// second up or down, and adding to that gave a jump of a different height every time it was pressed.
        ///
        /// Then the legs are let go of for <see cref="PuppetTuning.JumpClear"/> seconds. The probe reaches half of
        /// LegProbe below the feet, so the body still counts as standing for the first quarter metre of the rise, and
        /// the hover spring meets a climb of several metres a second with its full downward ceiling. That is what ate
        /// the old jump: the body left the ground by about fifteen centimetres and was pulled straight back onto it,
        /// which from the outside is a space bar that does nothing.</summary>
        void Launch(PuppetTuning t)
        {
            float v = Mathf.Sqrt(2f * Mathf.Max(Mathf.Abs(Physics.gravity.y), .01f) * Mathf.Max(t.JumpHeight, 0f));
            var now = Torso.linearVelocity;
            Torso.linearVelocity = new Vector3(now.x, v, now.z);
            jumpClearUntil = Time.time + Mathf.Max(t.JumpClear, .02f);
            jumpAsked = -99f;               // one press, one jump: the latch is spent here
            legs = 0f;
            squash = 0f;                    // pushing off is not a moment to be sitting in the last landing's knees
            Stamina -= t.JumpCost;
            restTimer = 0f;
            Jumped?.Invoke(v);
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
                // a sweep that starts already touching reports a zero normal, and a zero normal turns the gravity
                // cancellation below into NaN and the body into confetti. A body lying on its side after a fall is
                // exactly that case, so it is worth the two lines.
                GroundNormal = info.normal.sqrMagnitude > 1e-6f ? info.normal : Vector3.up;
                SlopeAngle = Vector3.Angle(GroundNormal, Vector3.up);
                Footing = GroundDistance <= toFeet + t.LegProbe * .5f;
                Grounded = Footing && SlopeAngle <= t.FootGrip;
                // too steep to stand on is not the same as thin air: the body keeps touching the slope all the way down
                Sliding = Footing && !Grounded;
            }
            else { GroundDistance = float.PositiveInfinity; GroundNormal = Vector3.up; SlopeAngle = 0f; Footing = Grounded = Sliding = false; }

            float vy = Torso.linearVelocity.y;
            // only a drop counts as a fall, and a drop is when there is nothing under the feet at all. Measuring it
            // from Grounded counted a slide too — the feet never leave the rock in a slide — and the body was being
            // thrown flat at the bottom of every gully by speed it had earned honestly on its feet.
            if (!Footing) { if (vy < 0f) fallSpeed = -vy; }
            else if (fallSpeed > 1f) { Land(t, fallSpeed); fallSpeed = 0f; }
            else fallSpeed = 0f;
        }

        /// <summary>The feet find the ground again after a drop. Three things can happen, and the choice between them
        /// is the whole of whether a fall reads as a fall: the knees give a little, the body staggers, or it is thrown
        /// over and has to get up. The old body only ever did the first, silently.</summary>
        void Land(PuppetTuning t, float speed)
        {
            LastImpact = speed;
            Landed?.Invoke(speed);
            squash = Mathf.Max(squash, PuppetMotion.Squash(speed, t));
            if (speed >= t.LimpFrom)
            {
                GoLimp(PuppetMotion.LimpFor(speed, t));
                Tip(PuppetMotion.TipSpin(speed, t));
            }
            else if (speed >= t.TripFrom)
            {
                stumbleUntil = Mathf.Max(stumbleUntil, Time.time + t.TripTime);
                // a stagger is a fall that was caught: the same throw, a quarter of it, legs still underneath
                Tip(PuppetMotion.TipSpin(speed, t) * .25f);
            }
        }

        /// <summary>Throws the body over the line it was travelling on. This is what turns "the controls went away"
        /// into something an onlooker calls falling over; the damping on the rigidbody bleeds the spin off again, so
        /// the body goes past horizontal and stops rather than rolling away down the hill like a barrel.</summary>
        void Tip(float degreesPerSecond)
        {
            var axis = PuppetMotion.TipAxis(Torso.linearVelocity, transform.right);
            Torso.angularVelocity += axis * (degreesPerSecond * Mathf.Deg2Rad);
        }

        /// <summary>Everything that lags, in one place. The body's own horizontal acceleration is measured, smoothed
        /// and turned into the up axis it will try to stand on; the knees unfold at their own rate; the legs and the
        /// vertical come back after a fall. None of it is cosmetic — the lean is a real torque target, which is what
        /// lets the figure, the camera and a self-test all read the same body.</summary>
        void Soften(PuppetTuning t, float dt)
        {
            var flat = new Vector3(Torso.linearVelocity.x, 0f, Torso.linearVelocity.z);
            var raw = (flat - lastFlat) / Mathf.Max(dt, 1e-4f);
            lastFlat = flat;
            // a first-order lag rather than the raw reading: a derivative taken ninety times a second is mostly noise,
            // and the delay is the point anyway — a chest that leans in the same step the feet do is the machine
            accel = Vector3.Lerp(accel, raw, 1f - Mathf.Exp(-dt / Mathf.Max(t.LeanLag, .01f)));
            // in the air and on a sack there is nothing to lean against: leaning needs a foot on the ground
            StandUp = Grounded && !Limp ? PuppetMotion.LeanUp(accel, t.LeanInto, t.LeanMax) : Vector3.up;
            Tilt = Vector3.Angle(transform.up, Vector3.up);

            squash = Mathf.MoveTowards(squash, 0f, PuppetMotion.Unfold(t) * dt);
            if (!Limp && Grounded) rise = Mathf.MoveTowards(rise, 1f, dt / Mathf.Max(t.GetUp, .02f));

            // ── the heading and the drift, published for whatever draws this body ──────────────────────────────────
            // Read off the capsule and not off the camera: the torso follows the look through a PD controller and is
            // always a little behind it, and a figure posed from the camera instead skates its feet in every turn.
            var fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            // a body on its face has no horizontal forward left; its own up is then the nearest thing to a heading,
            // and a fallen body is drawn from the rigidbody anyway, so this only has to stay finite
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            if (fwd.sqrMagnitude > 1e-4f)
            {
                Facing = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                FacingYaw = Facing.eulerAngles.y;
            }
            var own = Quaternion.Inverse(Facing) * flat;
            Drift = new Vector2(own.x, own.z);
        }

        /// <summary>The legs: a spring that holds the torso at its ride height over whatever the probe found, so steps,
        /// ledges and broken ground cost nothing and the body never catches on a lip.</summary>
        void Hover(PuppetTuning t)
        {
            if (float.IsInfinity(GroundDistance)) return;
            // a landing bends the knees, not the man: the ride height itself is pulled down by the impact and unfolds
            // again over SquashTime. The spring under it is underdamped, so the body settles back up through the
            // target instead of stepping onto it — that overshoot is the spring in a pair of legs.
            float error = (t.HoverHeight - squash) - GroundDistance;
            float vy = Vector3.Dot(Torso.linearVelocity, Vector3.up);
            float a = Mathf.Clamp(error * t.LegSpring - vy * t.LegDamper, -t.LegMaxAccel, t.LegMaxAccel) * legs * rise;
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
        /// rights the body, one turns it. Hanging, the body is allowed to swing — only the yaw is kept.
        ///
        /// What it rights <em>to</em> is not the vertical but <see cref="StandUp"/>, the vertical tilted into whatever
        /// the body is accelerating toward. The lean therefore lives in the physics: the torso really is pitched over
        /// its feet coming out of a stand and hanging back into a stop, and anything reading the rigidbody — the
        /// figure, the camera, a test — sees the same body. Cosmetic lean drawn on top of an upright capsule was the
        /// robot look, because the mass was still standing bolt upright underneath it.</summary>
        void Upright(PuppetTuning t)
        {
            var up = Grounded ? StandUp : Vector3.up;
            float authority = (Grounded ? 1f : Hanging ? .45f : .25f) * rise;
            if (Stumbling) authority *= Mathf.Clamp01(t.TripHold);
            var delta = Quaternion.FromToRotation(transform.up, up);
            delta.ToAngleAxis(out float angle, out var axis);
            if (angle > 180f) angle -= 360f;
            if (!float.IsNaN(axis.x) && Mathf.Abs(angle) > .01f)
            {
                var torque = axis.normalized * (angle * Mathf.Deg2Rad * t.UprightSpring * authority)
                           - Torso.angularVelocity * t.UprightDamper;
                Torso.AddTorque(torque, ForceMode.Acceleration);
            }
            // The yaw target is the look and only ever the look — never the direction of travel. That is the whole of
            // the side-step: press left and the body does not turn left, it goes left while still facing the camera's
            // way, and pressing back makes it walk backwards. Nothing downstream may turn the body toward its
            // velocity either; what it is doing relative to its heading is published as Facing and Drift.
            //
            // The turn is measured about the world vertical, never about the leaned one: yaw taken about a tilted
            // axis feeds the lean back into itself and the body starts to corkscrew out of a turn.
            var want = Vector3.ProjectOnPlane(LookRotation * Vector3.forward, Vector3.up);
            if (want.sqrMagnitude < 1e-4f) return;
            var flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            float yawErr = Vector3.SignedAngle(flat, want, Vector3.up) * Mathf.Deg2Rad;
            float yawRate = Vector3.Dot(Torso.angularVelocity, Vector3.up);
            Torso.AddTorque(Vector3.up * (yawErr * t.TurnSpring * authority - yawRate * t.TurnDamper), ForceMode.Acceleration);
        }

        void Move(PuppetTuning t, float dt, ref float drain)
        {
            var look = LookRotation;
            var wish = look * new Vector3(input.Move.x, 0f, input.Move.y);
            wish.y = 0f;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            // mid-stagger the legs are somewhere else: the player still has a share of them, not all of them
            if (Stumbling) wish *= Mathf.Clamp01(t.TripHold);
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
                // what the legs may change about the speed this step is not one number. Getting under way from a
                // stand is slow, holding a stride is cheap, and letting it die is softer still — without the split
                // the clamp was never reached at all and the body took walking pace inside a single fixed step.
                float cap = PuppetMotion.StepAccel(flat.magnitude, wish.sqrMagnitude > .01f, t);
                var a = Vector3.ClampMagnitude(err / Mathf.Max(dt, 1e-4f), cap);
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
