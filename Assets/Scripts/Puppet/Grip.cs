using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>What a surface is like to hold on to. Put it on a collider to say "this is rock, this is ice"; a
    /// surface without one is read as <see cref="Default"/>, which holds — everything is climbable until somebody says
    /// otherwise, because that is the cheaper mistake in a sandbox.</summary>
    public sealed class Grip : MonoBehaviour
    {
        /// <summary>1 = a jug you could hang a rucksack on, 0 = nothing to hold. Multiplies the break force.</summary>
        [Range(0f, 2f)] public float Hold = 1f;
        /// <summary>What a second of hanging on this costs, as a multiple of the tuning's rate: ice and crumbling
        /// rock are expensive, a ledge is cheap.</summary>
        [Range(0f, 4f)] public float Cost = 1f;
        /// <summary>The hand slides off instead of closing (bare ice, smooth slab).</summary>
        public bool Slippery;

        public static readonly Grip Default = null;

        /// <summary>Grip of whatever was hit, falling back to the defaults.</summary>
        public static void Of(Collider c, out float hold, out float cost, out bool slippery)
        {
            hold = 1f; cost = 1f; slippery = false;
            if (c == null) return;
            var g = c.GetComponentInParent<Grip>();
            if (g == null) return;
            hold = g.Hold; cost = g.Cost; slippery = g.Slippery;
        }
    }
}
