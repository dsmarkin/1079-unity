using System;

namespace Height1079.Core
{
    /// <summary>What is eating the bar right now. The order is the order the pieces are drawn in, from the right end
    /// of the bar inward.</summary>
    public enum Bite { Hunger, Cold, Sleep }

    /// <summary>One bar of strength, as in PEAK: hunger, cold and sleep are not bars of their own but pieces bitten
    /// off the top of the one bar. What is left after the bites is the ceiling the body may fill by resting; the bites
    /// themselves grow with time and go away only when something is done about them — food, a stove, a night.
    ///
    /// The bar is 0…<see cref="Max"/>. Nothing here spends it: spending is the body's business (the puppet runs,
    /// jumps and hangs on it). This class only says how high the bar may go. Backlog E0.1.</summary>
    public sealed class Condition
    {
        public const float Max = 100f;
        public static readonly Bite[] Kinds = { Bite.Hunger, Bite.Cold, Bite.Sleep };

        readonly float[] bites = new float[Kinds.Length];

        /// <summary>How much of the bar this one thing has bitten off, 0…<see cref="Max"/>.</summary>
        public float Of(Bite b) => bites[(int)b];

        /// <summary>All the bites together. Never more than <see cref="Max"/>: the bar cannot be bitten below nothing.</summary>
        public float Bitten
        {
            get { float s = 0f; for (int i = 0; i < bites.Length; i++) s += bites[i]; return s; }
        }

        /// <summary>The top of the bar after the bites — the most the body may have.</summary>
        public float Ceiling => Max - Bitten;
        /// <summary>The same as a share of the bar, 0…1.</summary>
        public float CeilingFraction => Ceiling / Max;
        /// <summary>Nothing left to fill: every bit of the bar is taken. The body cannot run or climb until something
        /// gives a piece back.</summary>
        public bool Spent => Ceiling <= 0f;

        /// <summary>Bite another <paramref name="amount"/> off the bar. Stops at the bottom of the bar: what is
        /// already gone cannot go again.</summary>
        public void Add(Bite b, float amount)
        {
            if (amount <= 0f) return;
            float room = Ceiling;
            if (room <= 0f) return;
            bites[(int)b] += Math.Min(amount, room);
        }

        /// <summary>Give some of the bar back — a bar of chocolate, a stove, sleep.</summary>
        public void Ease(Bite b, float amount)
        {
            if (amount <= 0f) return;
            bites[(int)b] = Math.Max(0f, bites[(int)b] - amount);
        }

        public void Clear(Bite b) => bites[(int)b] = 0f;

        /// <summary>What the world is doing to the body per second. Positive grows a bite, negative eases it: a stove
        /// is a negative cold, a bunk a negative sleep.</summary>
        public sealed class Pressures
        {
            /// <summary>Hunger grows always. The default fills the bar in twelve minutes of play.</summary>
            public float Hunger = Max / 720f;
            /// <summary>Cold depends on where the body is; the caller sets it. Zero indoors.</summary>
            public float Cold;
            /// <summary>Sleep comes slowly: the bar is gone in twenty-five minutes awake.</summary>
            public float Sleep = Max / 1500f;

            public float Of(Bite b)
            {
                switch (b)
                {
                    case Bite.Hunger: return Hunger;
                    case Bite.Cold: return Cold;
                    default: return Sleep;
                }
            }
        }

        /// <summary>Advance by <paramref name="dt"/> seconds under these pressures.</summary>
        public void Tick(float dt, Pressures p)
        {
            if (dt <= 0f || p == null) return;
            foreach (var b in Kinds)
            {
                float rate = p.Of(b) * dt;
                if (rate > 0f) Add(b, rate); else if (rate < 0f) Ease(b, -rate);
            }
        }

        /// <summary>A bar of chocolate: how much hunger it takes away, and the bonus strength it puts on top of the
        /// bar. The bonus is the body's to spend (<c>Puppet.Extra</c>) and does not come back — as in PEAK, food is
        /// the one thing that puts the bar over its top, and only for as long as it is not used.</summary>
        public static class Chocolate
        {
            public const float Hunger = 25f;
            public const float Extra = 25f;
        }
    }
}
