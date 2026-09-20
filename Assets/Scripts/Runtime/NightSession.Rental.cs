#if !HEIGHT1079_NO_ELBRUS
// Compiled only when the southern slope of Elbrus is in the build (docs/ELBRUS.md). It has to live in
// Height1079.Runtime and not in the location's own assembly because a partial class cannot be split across
// assemblies, and because the rest of Height1079.Runtime's Elbrus partials name what is in here.
using Unity.Netcode;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The hire counter on the host. Money is the client's own business; what goes into the rucksack is the
    /// host's, so the order comes here and the host fills it out of <see cref="Rental"/>.</summary>
    public sealed partial class NightSession
    {
        /// <summary>Local: what came of our last hire (what was asked for, what was actually handed over).</summary>
        public event System.Action<ushort, ushort> Hired;

        /// <summary>Owner asks the counter for a set of pieces.</summary>
        public void HireGear(Gear wanted) => HireRpc((ushort)wanted);

        [Rpc(SendTo.Server)]
        void HireRpc(ushort wanted, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            string token = Token(sender);
            var served = Gear.None;
            if (Climb.On && run != null && packs != null
                && run.Players.TryGetValue(token, out var p) && p.Outcome == Outcome.None)
            {
                var have = Rental.Carried(packs, token);
                foreach (var h in Rental.Board())
                {
                    if (((Gear)wanted & h.Piece) == 0 || (have & h.Piece) != 0) continue;
                    if (packs.Receive(token, new ItemStack(h.Item)) != PackResult.Ok) continue;
                    served |= h.Piece;
                }
                if (served != Gear.None)
                {
                    SyncPacks();
                    run.Record($"{p.Name} берёт в прокате: {Rental.Titles(served)}.");
                }
            }
            HiredRpc(wanted, (ushort)served, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void HiredRpc(ushort wanted, ushort served, RpcParams rpc) => Hired?.Invoke(wanted, served);
    }
}
#endif
