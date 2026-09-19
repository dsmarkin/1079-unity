using System;
using System.IO;
using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>Every number the body is made of, in one place, so a session in the sandbox can move them while the game
    /// runs and write the result to disk. Nothing here is a constant in a class somewhere: the whole point of the
    /// sandbox is that the feel of the body is found by hand and then carried into the game as a file.</summary>
    [Serializable]
    public sealed class PuppetTuning
    {
        public string Name = "default";

        // ─── the mass that hangs on the hands ───────────────────────────────────────────────────────────────────────
        /// <summary>Torso mass, kg. A man with a rucksack is 80; the hands carry it and the stamina pays for it.</summary>
        public float TorsoMass = 70f;
        public float HandMass = 3.5f;
        /// <summary>Sole to crown of the climber, m — the stature the whole body is cut to. The physics reads it
        /// nowhere: it is written down because <see cref="HoverHeight"/>, the capsule and the bone lengths in
        /// <see cref="PuppetFigure"/> are three views of one number, and left unwritten they drift apart and the
        /// figure ends up standing with its feet through the snow. The self-test measures the drawn body against it.
        ///
        /// 1.62 m and not the 1.78 this started at: a tall thin doll reads as stilts from behind, and PEAK's own
        /// proportions are short and wide. Only the height came down — the capsule is as broad as it was.</summary>
        public float StandHeight = 1.55f;
        /// <summary>Half-height and radius of the torso capsule, m. The radius is the body's width and is deliberately
        /// not cut with the height: a narrow capsule slips between things a man's shoulders would not.</summary>
        public float TorsoHeight = .90f, TorsoRadius = .28f;
        /// <summary>Shoulder height above the torso centre and the sideways offset of each shoulder, m.</summary>
        public float ShoulderUp = .34f, ShoulderOut = .22f;

        // ─── legs: a capsule floating over the ground on a spring, so steps and ledges cost nothing ─────────────────
        /// <summary>Where the torso centre rides above whatever is under the feet, m. This is the one number the
        /// figure's legs are cut to (pelvis to sole), so it moves with <see cref="StandHeight"/> and nothing else.</summary>
        public float HoverHeight = .82f;
        /// <summary>How far below the feet the leg probe looks, m — this is the height of a step the legs absorb.
        /// Half of it is also how far the body may rise and still count as standing on something, which is why a jump
        /// has to let the legs go for a moment (<see cref="JumpClear"/>).</summary>
        public float LegProbe = .46f;
        public float LegSpring = 140f, LegDamper = 14f;
        /// <summary>Ceiling on what the leg spring may do, m/s². Without it the spring is a catapult: a body that has
        /// been lying down (after a fall) is a metre below its ride height, and the full spring throws it into the air.</summary>
        public float LegMaxAccel = 45f;
        /// <summary>Seconds the legs take to come up to full strength after they find the ground again — the give in
        /// the knees on landing, and the other half of the catapult fix.</summary>
        public float LegRise = .3f;
        /// <summary>Fastest the legs may lift the body, m/s. A body that has been lying down is a metre below its ride
        /// height, and without this cap the spring stands it up by throwing it two metres into the air.</summary>
        public float LegLift = 2f;
        /// <summary>Slope the feet still hold on, degrees. Above it the body slides.</summary>
        public float FootGrip = 48f;
        /// <summary>Sliding down something too steep: how much of the fall line the boots scrub off (0 = ice,
        /// 1 = the slide never starts), the speed it tops out at, and how much the player can still steer, m/s².</summary>
        public float SlideFriction = .38f, SlideTop = 9f, SlideControl = 1.6f;
        /// <summary>How much of the leg spring is left while sliding. It is only there to keep the capsule off the
        /// rock; at full strength the legs would stand the body up on a slope it cannot stand on.</summary>
        public float SlideLift = .35f;
        /// <summary>What is left of the walking speed straight up the steepest slope the feet still hold, and what a
        /// descent is worth. Walking up a mountain is slower than walking across a room; this is the whole difference.</summary>
        public float UphillSpeed = .45f, DownhillSpeed = 1.12f;

        // ─── the knees: how a landing is absorbed ───────────────────────────────────────────────────────────────────
        /// <summary>Under this impact, m/s, the knees do not give at all — a man comes off a kerb and keeps walking.</summary>
        public float SquashFrom = 2.5f;
        /// <summary>Metres the ride height is pulled down per m/s of impact over the threshold, and the deepest it is
        /// ever pulled. At the defaults a two-metre drop folds the body by about 0.16 m and a roof puts it at the cap.
        /// Keep the cap under the capsule's clearance (HoverHeight − TorsoHeight/2) or the body sits on its own shell.</summary>
        public float SquashGive = .045f, SquashMax = .30f;
        /// <summary>Seconds the deepest crouch takes to unfold. It is a rate underneath, so a light landing is over in
        /// a blink and a heavy one hangs — two behaviours out of one number. The leg spring is underdamped, so the
        /// body settles back up <em>through</em> the target rather than stepping onto it.</summary>
        public float SquashTime = .55f;

        // ─── going down: what a fall costs and how long it takes to be a man again ──────────────────────────────────
        /// <summary>Impact that takes the legs away, m/s — about a three-metre drop. Over it there is no control, no
        /// leg spring and no vertical at all: the body is thrown over and lies where it stops.</summary>
        public float LimpFrom = 7f;
        /// <summary>Seconds down after a landing right at the threshold; a harder arrival keeps the body down
        /// proportionally longer, to a ceiling of four seconds.</summary>
        public float LimpTime = .9f;
        /// <summary>Degrees a second of tumble handed to the body as it goes down, doubling by twice the threshold
        /// speed. Without it a knocked-out body stands on the spot with its controls off and nobody reads it as a fall.</summary>
        public float FallSpin = 170f;
        /// <summary>Friction of the capsule while the body is down, 0…1. The walking capsule is frictionless on
        /// purpose — friction catches on every lip and fights the leg spring — and a frictionless body on its side
        /// skates like a bar of soap. It wears this while it lies and gets the slick skin back the moment it is up.</summary>
        public float LimpFriction = .55f;
        /// <summary>Seconds to get back up. The legs and the vertical both come back over it, so the body unfolds off
        /// the ground instead of being snapped upright by the full leg spring the instant the timer runs out.</summary>
        public float GetUp = 1.2f;
        /// <summary>Impact that only staggers, m/s, and how long the stagger lasts — the cheap fall, for the knocks
        /// that are not worth putting a man on the floor for.</summary>
        public float TripFrom = 4f, TripTime = .45f;
        /// <summary>What is left of the player's control through a stagger, 0…1: steering and the vertical both.</summary>
        public float TripHold = .35f;

        // ─── standing up: a PD controller on the torso rotation, the thing that makes a ragdoll look alive ──────────
        /// <summary>The spring that holds the body upright, and its damper. Soft on purpose: at the old 110/16 the
        /// torso was welded to the vertical. Here ω ≈ 8 rad/s and ζ ≈ 0.55, so the chest swings past the target once
        /// and settles, and that overshoot is what the eye reads as the weight of a man.</summary>
        public float UprightSpring = 70f, UprightDamper = 9f;
        /// <summary>How hard the body turns to face where the player looks.</summary>
        public float TurnSpring = 40f, TurnDamper = 7f;
        /// <summary>How much of its own horizontal acceleration the body leans into: 0 is a pole on a spring, 1 is the
        /// lean the physics says a runner takes, over 1 is a cartoon. One knob gives all three of forward on the
        /// start, back on the stop and a shoulder into the turn, because they are the same fact (tan θ = a/g).</summary>
        public float LeanInto = .85f;
        /// <summary>Cap on that lean, degrees.</summary>
        public float LeanMax = 16f;
        /// <summary>Seconds the lean takes to follow a change of pace — the torso's inertia. At 0 the chest leans in
        /// the same step the feet do, which is exactly the machine we are getting away from.</summary>
        public float LeanLag = .18f;

        // ─── walking ────────────────────────────────────────────────────────────────────────────────────────────────
        public float WalkSpeed = 3.0f, RunSpeed = 5.6f;
        /// <summary>Acceleration the legs may use once the body is already at walking pace, m/s², and what little
        /// steering there is in the air. This was 26 with nothing under it and the clamp was never reached: the body
        /// went from a stand to full speed inside one fixed step.</summary>
        public float GroundAccel = 18f, AirAccel = 3.5f;
        /// <summary>What the legs may do from a standstill, m/s², rising to <see cref="GroundAccel"/> by the time the
        /// body is at <see cref="WalkSpeed"/>. This is the third of a second of getting under way.</summary>
        public float StartAccel = 11f;
        /// <summary>What the legs may do when nothing is asked of them, m/s². A man does not stop dead; at the default
        /// he runs on for about a third of a second after the key comes up.</summary>
        public float BrakeAccel = 8f;

        // ─── the jump ───────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>How high the body leaves the ground, m. Kept as a height rather than an impulse because a height
        /// is the thing that can be judged against the man: on a 1.62 m climber with a pack, 0.55 m is a hop that is
        /// plainly a jump and plainly not a superhero. The speed that reaches it follows from the scene's own gravity,
        /// so the jump keeps its size wherever it is used.</summary>
        public float JumpHeight = 1.05f;
        /// <summary>Seconds a press of jump is remembered for. The key is read once a frame and the body steps at
        /// <see cref="PhysicsRate"/>: a frame that happens to contain no fixed step used to drop the press on the
        /// floor, which is exactly the "space does nothing" the sandbox showed. Remembering it also means a press made
        /// a hair before the feet land fires the moment they do, which is how every platformer has done it since.</summary>
        public float JumpBuffer = .18f;
        /// <summary>Seconds the leg spring is let go of after a launch — and, because it is the same window, the
        /// soonest a second jump can fire. The legs are what holds the body at its ride height, and the probe still
        /// calls the body grounded for <see cref="LegProbe"/>/2 after it leaves: meeting a 3 m/s climb, the spring's
        /// damper answers with its full downward ceiling and eats the jump inside ten centimetres. This is that
        /// quarter of a metre, measured in time.</summary>
        public float JumpClear = .22f;
        /// <summary>What a jump costs off the one bar. Below it the legs will not push.</summary>
        public float JumpCost = 8f;

        // ─── hands ──────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>How far a hand reaches from the shoulder, m.</summary>
        public float ArmReach = .78f;
        /// <summary>The spring that pulls a free hand to where the player is pointing it.</summary>
        public float ArmSpring = 900f, ArmDamper = 45f;
        /// <summary>The spring that holds the body against a gripping hand. Too soft and the climber is a yo-yo,
        /// too hard and every touch of the wall throws him off it.</summary>
        public float GripSpring = 2600f, GripDamper = 90f;
        /// <summary>Newtons a grip takes before it tears off the rock. Stamina scales this down as it empties.</summary>
        public float GripBreakForce = 5200f;
        /// <summary>Radius of the sphere that looks for something to hold, m.</summary>
        public float GrabRadius = .22f;
        /// <summary>Metres the hand is pulled toward the body while pulling up — this is how a climber gains height.</summary>
        public float PullIn = .45f;

        // ─── stamina: one bar, as in PEAK ───────────────────────────────────────────────────────────────────────────
        public float Stamina = 100f;
        /// <summary>Per second, per gripping hand, while hanging with the feet off anything.</summary>
        public float HangCost = 11f;
        /// <summary>Per second while gripping but with weight on the feet.</summary>
        public float GripCost = 3.5f;
        /// <summary>Per second while pulling up.</summary>
        public float PullCost = 18f;
        public float RunCost = 6f;
        /// <summary>Per second on firm ground after a pause of <see cref="RestDelay"/> seconds.</summary>
        public float Recovery = 20f, RestDelay = .6f;
        /// <summary>Seconds the hands refuse to close after they have been emptied.</summary>
        public float ExhaustLock = 1.4f;

        // ─── solver ─────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Physics steps per second. Joints holding 70 kg need more than the default fifty.</summary>
        public int PhysicsRate = 90;
        public int SolverIterations = 14;

        public PuppetTuning Copy() => JsonUtility.FromJson<PuppetTuning>(JsonUtility.ToJson(this));

        /// <summary>Where a tuning found in the sandbox is written, so the game can read the same file.</summary>
        public static string PathFor(string name) => Path.Combine(Application.persistentDataPath, "puppet-" + name + ".json");

        public void Save(string name = null)
        {
            if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
            File.WriteAllText(PathFor(Name), JsonUtility.ToJson(this, true));
        }

        /// <summary>Reads a tuning by name; returns the defaults when there is no such file, so a fresh machine works.</summary>
        public static PuppetTuning Load(string name)
        {
            try
            {
                string path = PathFor(name);
                if (File.Exists(path))
                {
                    var t = JsonUtility.FromJson<PuppetTuning>(File.ReadAllText(path));
                    if (t != null) { t.Name = name; return t; }
                }
            }
            catch (Exception e) { Debug.LogWarning("puppet: " + e.Message); }
            return new PuppetTuning { Name = name };
        }
    }
}
