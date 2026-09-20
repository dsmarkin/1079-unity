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

    /// <summary>How a body is built, metres in the body's own frame off the pelvis (x to its right, y up, z ahead):
    /// what the figure needs in order to solve a pose for a given set of limbs. The sculpted figure's are constants
    /// in <see cref="PuppetFigure"/>; a worn model's are measured off its rest pose by <see cref="PuppetSkeleton"/>
    /// and the figure solves against them while the model is worn — so the model's bones, aimed along the solved
    /// segments, end exactly at the solved joints. Nothing stretches, the feet land where the solver put them, and
    /// a model whose legs are longer than the sculpted figure's does not walk in a crouch.</summary>
    public struct PuppetProportions
    {
        public bool Valid;
        /// <summary>The pelvis above the sole, standing at rest.</summary>
        public float PelvisRide;
        /// <summary>The ankle joint above the sole.</summary>
        public float AnkleUp;
        /// <summary>The hip joints and the shoulder joints, off the pelvis.</summary>
        public Vector3 HipL, HipR, ShoulderL, ShoulderR;
        public float ThighL, ThighR, ShinL, ShinR, UpperArmL, UpperArmR, ForearmL, ForearmR;

        public Vector3 Hip(int side) => side == 0 ? HipL : HipR;
        public Vector3 Shoulder(int side) => side == 0 ? ShoulderL : ShoulderR;
        public float Thigh(int side) => side == 0 ? ThighL : ThighR;
        public float Shin(int side) => side == 0 ? ShinL : ShinR;
        public float UpperArm(int side) => side == 0 ? UpperArmL : UpperArmR;
        public float Forearm(int side) => side == 0 ? ForearmL : ForearmR;
        /// <summary>The model has an arm on this side at all.</summary>
        public bool HasArm(int side) => UpperArm(side) > 1e-4f;

        /// <summary>The same build at another size.</summary>
        public PuppetProportions Scaled(float k) => new PuppetProportions
        {
            Valid = Valid, PelvisRide = PelvisRide * k, AnkleUp = AnkleUp * k,
            HipL = HipL * k, HipR = HipR * k, ShoulderL = ShoulderL * k, ShoulderR = ShoulderR * k,
            ThighL = ThighL * k, ThighR = ThighR * k, ShinL = ShinL * k, ShinR = ShinR * k,
            UpperArmL = UpperArmL * k, UpperArmR = UpperArmR * k, ForearmL = ForearmL * k, ForearmR = ForearmR * k,
        };
    }
}
