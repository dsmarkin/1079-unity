using System;
using System.Collections.Generic;

namespace Height1079.Runtime
{
    /// <summary>Whether some window of the game's own owns the keyboard right now — a café order, a hire counter, a
    /// bunk, a rescue slip, a snow-cat driver, a weather board. While one is up, the digits and the letter keys
    /// belong to it and nothing else on the mountain may read them.
    ///
    /// The list is filled at run time rather than written down, because the counters live in the assembly of the map
    /// they stand on: a map left out of the build registers nothing and every question below answers «нет»
    /// (docs/ELBRUS.md). Without this seam the shared half of the game would have to name those counters, and an
    /// assembly cannot reference one that references it back.</summary>
    public static class UiWindows
    {
        static readonly List<Func<bool>> tests = new List<Func<bool>>();

        /// <summary>Adds a question to ask. Idempotent for a named method: two delegates over the same static method
        /// are equal, so an init hook that runs twice still leaves one entry.</summary>
        public static void Watch(Func<bool> open)
        {
            if (open == null || tests.Contains(open)) return;
            tests.Add(open);
        }

        /// <summary>True while any of them has a window up.</summary>
        public static bool AnyOpen
        {
            get
            {
                for (int i = 0; i < tests.Count; i++) if (tests[i]()) return true;
                return false;
            }
        }
    }
}
