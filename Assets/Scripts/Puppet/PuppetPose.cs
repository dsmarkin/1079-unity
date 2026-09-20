using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>The pose <see cref="PuppetFigure"/> solved this frame, in world space, for anything that wants to draw
    /// the same man with other geometry — a skinned model from a file (<see cref="PuppetSkeleton"/>) most of all.
    /// Every segment is a start and an end point; the joints are where the figure's own two-bone IK put them, the
    /// pelvis and head carry a full frame (Y up the spine, Z the front). Read-only by construction: the figure
    /// writes it, everybody else copies it.</summary>
    public struct PuppetPose
    {
        /// <summary>False until the figure has been solved once.</summary>
        public bool Valid;
        /// <summary>The figure's scale against the stature its bones were drawn at.</summary>
        public float Scale;
        /// <summary>The body has been thrown over: the torso frame is the capsule's own rotation.</summary>
        public bool Limp;
        /// <summary>The pelvis point and the torso's frame.</summary>
        public Vector3 Pelvis;
        public Quaternion Body;
        /// <summary>The head's centre and its frame — most of the torso's lean, righted a little.</summary>
        public Vector3 Head;
        public Quaternion HeadRot;
        public PuppetLegPose LegL, LegR;
        public PuppetArmPose ArmL, ArmR;

        public PuppetLegPose Leg(int side) => side == 0 ? LegL : LegR;
        public PuppetArmPose Arm(int side) => side == 0 ? ArmL : ArmR;
    }

    /// <summary>One leg: hip joint → knee → ankle, the boot's frame, and where its sole touches down.</summary>
    public struct PuppetLegPose
    {
        public Vector3 Hip, Knee, Ankle;
        /// <summary>The boot's frame: Z the toe, Y up through the shaft — pitched heel-to-toe through a step.</summary>
        public Quaternion Foot;
        /// <summary>The point of the sole under the ankle, on (or a centimetre into) the ground the boot stands on.</summary>
        public Vector3 Sole;
        /// <summary>The way the knee bends, as the solver was told to bend it — a twist hint when the leg is so
        /// straight that the knee itself does not say.</summary>
        public Vector3 Bend;
    }

    /// <summary>One arm: shoulder → elbow → wrist → the middle of the palm, and the hand's frame.</summary>
    public struct PuppetArmPose
    {
        public Vector3 Shoulder, Elbow, Wrist, Hand;
        /// <summary>The palm's frame — its up runs back along the forearm, its right is the thin axis of the palm.</summary>
        public Quaternion Grip;
        /// <summary>The way the elbow bends (backward, for a body's own arm).</summary>
        public Vector3 Bend;
    }
}
