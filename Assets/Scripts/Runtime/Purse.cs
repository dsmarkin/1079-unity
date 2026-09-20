#if !HEIGHT1079_NO_ELBRUS
// Compiled only when the southern slope of Elbrus is in the build (docs/ELBRUS.md). It has to live in
// Height1079.Runtime and not in the location's own assembly because a partial class cannot be split across
// assemblies, and because the rest of Height1079.Runtime's Elbrus partials name what is in here.
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>What the local hiker has left to spend on the mountain. One purse per client: nobody else's money is
    /// at stake, and the host only ever hears the total, in the profile it saves (<see cref="NightSession"/>).
    ///
    /// It lives on its own and not inside the café because the counters that spend it — the café, the hire desk, the
    /// snow-cat driver, the bunks — are in the location's own assembly, while the two that only read it are partials
    /// of classes that cannot leave <c>Height1079.Runtime</c>.</summary>
    public static class Purse
    {
        public static Wallet Money = Wallet.Start();
    }
}
#endif
