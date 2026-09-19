using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>The give in the body. <see cref="Puppet"/> keeps one capsule on one leg spring because that is the
    /// part that has to stay stable; everything that makes it read as a man carrying his own weight instead of a
    /// Boston Dynamics mule is a thin layer laid over that, and this is where the arithmetic of those layers lives.
    ///
    /// Why layers and not an active ragdoll. Human Fall Flat and Gang Beasts drive every bone of a jointed skeleton
    /// through a ConfigurableJoint slerp drive chasing an animated pose — a PD controller per joint, a dozen jointed
    /// bodies per player, a solver run hot, and a body that comes apart the moment the frame rate or the network
    /// slips. PEAK does not pay that either (docs/PHYSICS.md): a few driven parts and a passive doll dragged after
    /// them. What all of them have in common is not joint control — it is <em>lag</em>. The visible body is always
    /// chasing a target it never quite catches, and the gap between the two is what the eye reads as weight. So every
    /// function here is a target with a delay in front of it, and every one of them can be turned off by a number.</summary>
    public static class PuppetMotion
    {
        /// <summary>The up axis a walker actually stands on. A man speeding up leans into it, a man stopping hangs
        /// back, a man turning drops a shoulder into the turn — and all three are one fact: what a body holds vertical
        /// is not gravity but gravity plus its own acceleration, with tan(lean) = a / g.
        ///
        /// <paramref name="into"/> is how much of that is taken: 1 is what the physics says, less is a stiffer walker,
        /// more is a cartoon. The cap earns its keep — the legs can put 20 m/s² through a step, and the honest answer
        /// to that is a sixty-degree lean, which is a man diving rather than walking.</summary>
        public static Vector3 LeanUp(Vector3 accel, float into, float maxDegrees)
        {
            accel.y = 0f;
            if (into <= 0f || maxDegrees <= 0f || accel.sqrMagnitude < 1e-4f) return Vector3.up;
            var want = Vector3.up * Mathf.Max(Physics.gravity.magnitude, .01f) + accel * into;
            if (want.sqrMagnitude < 1e-6f) return Vector3.up;
            // RotateTowards keeps the length of the first argument, so this both clamps the lean and normalises it
            return Vector3.RotateTowards(Vector3.up, want.normalized, maxDegrees * Mathf.Deg2Rad, 0f);
        }

        /// <summary>How far the knees give on a landing, metres. Under the threshold a man steps off a kerb and keeps
        /// walking; over it he folds, and the harder he lands the deeper he folds — which is the whole difference
        /// between arriving off a step and arriving off a roof.</summary>
        public static float Squash(float impact, PuppetTuning t)
            => Mathf.Clamp((impact - t.SquashFrom) * t.SquashGive, 0f, t.SquashMax);

        /// <summary>Metres a second the knees unfold at. Deliberately a rate and not a duration: the deepest landing
        /// then takes the whole of <see cref="PuppetTuning.SquashTime"/> to come back up while a light one is over in
        /// a blink, and that is two behaviours out of one number instead of two.</summary>
        public static float Unfold(PuppetTuning t) => t.SquashMax / Mathf.Max(t.SquashTime, .02f);

        /// <summary>The ceiling on what the legs may do to the body's speed this step, m/s². Three regimes, because a
        /// man has three: getting under way from a stand is the slow part, holding a stride is cheap, and letting the
        /// stride die is its own, softer number. With a single ceiling the clamp was never reached at all — the body
        /// went from nothing to walking pace inside one fixed step, and no amount of visual smoothing hides that.</summary>
        public static float StepAccel(float speed, bool asked, PuppetTuning t)
        {
            if (!asked) return Mathf.Max(t.BrakeAccel, 0f);
            float rolling = t.WalkSpeed > .01f ? Mathf.Clamp01(speed / t.WalkSpeed) : 1f;
            return Mathf.Lerp(t.StartAccel, t.GroundAccel, rolling);
        }

        /// <summary>Seconds the body stays down after a landing that took the legs away. At the threshold it is the
        /// tuned figure; twice as fast an arrival keeps it down twice as long, and four seconds is where it stops —
        /// past that the player is watching a corpse rather than a fall.</summary>
        public static float LimpFor(float impact, PuppetTuning t)
            => Mathf.Clamp(t.LimpTime * Over(impact, t), t.LimpTime, 4f);

        /// <summary>Degrees a second of tumble handed to the body as it goes down, doubling by twice the threshold
        /// speed. This is the single most important number in a fall: without it a knocked-out body stands on the spot
        /// with its controls switched off, and the player reports — correctly — that there are no falls in the game.</summary>
        public static float TipSpin(float impact, PuppetTuning t) => t.FallSpin * Mathf.Clamp(Over(impact, t), 1f, 2f);

        static float Over(float impact, PuppetTuning t) => t.LimpFrom > .01f ? impact / t.LimpFrom : 1f;

        /// <summary>The axis to tumble about: across the line of travel, so the body goes over its own toes in the
        /// direction it was going. A body that arrived straight down has no line of travel, so it goes over the way it
        /// was facing — deliberately not a random axis, because a fall the self-test cannot predict it cannot check.</summary>
        public static Vector3 TipAxis(Vector3 travel, Vector3 facingRight)
        {
            travel.y = 0f;
            if (travel.sqrMagnitude < .04f)
                return facingRight.sqrMagnitude > 1e-4f ? facingRight.normalized : Vector3.right;
            return Vector3.Cross(Vector3.up, travel.normalized);
        }
    }
}
