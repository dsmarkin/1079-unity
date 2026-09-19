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
        /// <summary>Half-height and radius of the torso capsule, m.</summary>
        public float TorsoHeight = 1.15f, TorsoRadius = .28f;
        /// <summary>Shoulder height above the torso centre and the sideways offset of each shoulder, m.</summary>
        public float ShoulderUp = .42f, ShoulderOut = .22f;

        // ─── legs: a capsule floating over the ground on a spring, so steps and ledges cost nothing ─────────────────
        /// <summary>Where the torso centre rides above whatever is under the feet, m.</summary>
        public float HoverHeight = 1.05f;
        /// <summary>How far below the feet the leg probe looks, m — this is the height of a step the legs absorb.</summary>
        public float LegProbe = .55f;
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

        // ─── standing up: a PD controller on the torso rotation, the thing that makes a ragdoll look alive ──────────
        public float UprightSpring = 110f, UprightDamper = 16f;
        /// <summary>How hard the body turns to face where the player looks.</summary>
        public float TurnSpring = 40f, TurnDamper = 7f;

        // ─── walking ────────────────────────────────────────────────────────────────────────────────────────────────
        public float WalkSpeed = 3.0f, RunSpeed = 5.6f;
        /// <summary>Acceleration used to reach the wanted speed, m/s²; the cap is what keeps the body from teleporting.</summary>
        public float GroundAccel = 26f, AirAccel = 3.5f;

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
