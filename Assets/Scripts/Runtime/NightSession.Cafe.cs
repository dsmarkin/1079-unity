#if !HEIGHT1079_NO_ELBRUS
// Compiled only when the southern slope of Elbrus is in the build (docs/ELBRUS.md). It has to live in
// Height1079.Runtime and not in the location's own assembly because a partial class cannot be split across
// assemblies, and because the rest of Height1079.Runtime's Elbrus partials name what is in here.
using Unity.Netcode;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The café counter on the host. Money is the client's own business, but the warmth a portion gives back is
    /// the night's, so the order comes here: the host feeds the participant through <see cref="Refreshments"/> and puts
    /// a parcel taken away into the rucksack it keeps.</summary>
    public sealed partial class NightSession
    {
        /// <summary>Local: what came of our last café order (dish, taken away, actually served).</summary>
        public event System.Action<DishId, bool, bool> CafeServed;

        /// <summary>Owner asks a café to serve one portion, eaten at the table or wrapped for the rucksack.</summary>
        public void OrderRefreshment(DishId dish, bool takeAway) => CafeOrderRpc((byte)dish, takeAway);

        [Rpc(SendTo.Server)]
        void CafeOrderRpc(byte dish, bool takeAway, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            var spec = Refreshments.Get((DishId)dish);
            string token = Token(sender);
            bool served = false;
            if (!spec.IsEmpty && run != null && Height1079.Core.World.IsElbrus
                && run.Players.TryGetValue(token, out var p) && p.Outcome == Outcome.None)
            {
                if (takeAway)
                {
                    served = spec.Portable && packs != null && packs.Receive(token, new ItemStack(spec.TakeAway)) == PackResult.Ok;
                    if (served)
                    {
                        SyncPacks();
                        run.Record($"{p.Name} берёт с собой: {Items.Spec(spec.TakeAway).Name}.");
                    }
                }
                else
                {
                    var meal = Refreshments.Eat((DishId)dish, p);
                    served = !meal.IsEmpty;
                    if (served) run.Record($"{p.Name}: {meal.Text}");
                }
            }
            CafeServedRpc(dish, takeAway, served, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void CafeServedRpc(byte dish, bool takeAway, bool served, RpcParams rpc) => CafeServed?.Invoke((DishId)dish, takeAway, served);
    }
}
#endif
