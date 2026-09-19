using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>What the body is being asked to do this step. Filled by whoever drives it — the sandbox from the
    /// keyboard, the game from its own controls, a test from a script — so the body itself knows nothing about input,
    /// layouts or the network.</summary>
    public struct PuppetInput
    {
        /// <summary>x = right, y = forward, in the frame of <see cref="Look"/>. Length ≤ 1.</summary>
        public Vector2 Move;
        /// <summary>Where the player is looking: the hands reach along it and the body turns to it.</summary>
        public Quaternion Look;
        public bool Run, Jump;
        /// <summary>Holding a hand out (and closing it on whatever it touches).</summary>
        public bool GrabLeft, GrabRight;
        /// <summary>Pulling up on whatever is held.</summary>
        public bool PullUp;

        public static PuppetInput Idle => new PuppetInput { Look = Quaternion.identity };
    }
}
