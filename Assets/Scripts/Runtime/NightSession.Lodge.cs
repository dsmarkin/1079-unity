using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>A night in a приют, on the host.
    ///
    /// The rules are <see cref="Lodging"/>'s and the night itself is <see cref="Camp.Sleep"/>'s — this file owns
    /// neither. What it does is what a host has to do: check that the player is actually standing at a bunk, spend the
    /// night, hang the wet things over the pipes, put the legs back where a warm room leaves them, and write the save,
    /// exactly as a night in a двойка does (<see cref="SaveNight"/>).
    ///
    /// <b>Acclimatisation is not re-invented here.</b> Climb high, sleep low is one rule, it lives in
    /// <see cref="Ascent.Acclimatise"/>, and <see cref="Lodging.Sleep"/> reaches it through <see cref="Camp.Sleep"/>
    /// at the bunk's own height. A bed at 3 710 m is worth what any night at 3 710 m is worth; the six thousand
    /// roubles of a capsule buy a better night, not a better mountain.
    ///
    /// The money is the client's own business, the way it is at the hire counter: the client pays before it asks, and
    /// gets it back if the host says no.</summary>
    public sealed partial class NightSession
    {
        /// <summary>Local: the host answered the ask for a bunk (whether the night happened, and a line to show).</summary>
        public event System.Action<bool, string> Slept;

        /// <summary>Owner: lie down in the bunk in front of you.</summary>
        public void RequestBunk() => BunkRpc();

        [Rpc(SendTo.Server)]
        void BunkRpc(RpcParams rpc = default)
        {
            ulong id = rpc.Receive.SenderClientId;
            if (!Climb.On || run == null || packs == null) return;
            var hiker = HikerController.For(id);
            if (hiker == null || hiker.Riding.Value) return;
            if (!run.Players.TryGetValue(Token(id), out var asked) || asked.Outcome != Outcome.None) return;
            if (!Lodging.AllowedIn(Height1079.Core.World.Current))
            { SleptRpc(false, Say("Здесь не ночуют."), RpcTarget.Single(id, RpcTargetUse.Temp)); return; }

            var bunk = Lodges.Nearest(hiker.transform.position, out float metres, out var at, out float yaw);
            if (bunk.IsEmpty || metres > Lodging.DoorReach)
            { SleptRpc(false, Say("Рядом нет нар."), RpcTarget.Single(id, RpcTargetUse.Temp)); return; }

            string note = "";
            SaveNight(at, yaw, bunk.Ele, true, false, "bunk:" + bunk.Id,
                $"{bunk.Name}, {bunk.Ele:0} м",
                (who, climber) =>
                {
                    string token = Token(who);
                    // one rule, one place: Lodging.Sleep calls Camp.Sleep at the bunk's height and then adds the roof
                    var night = Lodging.Sleep(climber, bunk, packs.Has(token, ItemId.Burner));
                    if (run.Players.TryGetValue(token, out var p)) Lodging.Warm(p, bunk);
                    int dried = Lodging.Dry(packs.Worn(token), bunk);
                    float rest = Lodging.RestFactor(bunk);
                    if (!strength.TryGetValue(who, out var had) || had < rest) strength[who] = rest;
                    if (who != id) return;
                    note = night.IsEmpty ? bunk.Name : night.Note;
                    if (night.Gain > .001f) note += $" Акклиматизация {night.Acclim * 100f:0} % (+{night.Gain * 100f:0}).";
                    if (dried > 0) note += $" Высохло вещей: {dried}.";
                    if (night.ThermosFilled) note += " Термос налит.";
                });
            SyncPacks();
            if (note.Length == 0) note = "В этой койке вы уже ночевали.";
            run.Record($"{asked.Name} ночует: {bunk.Name}.");
            SleptRpc(true, Say(note), RpcTarget.Single(id, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void SleptRpc(bool ok, FixedString512Bytes text, RpcParams rpc)
        {
            string line = text.ToString();
            if (!ok) Note(line);
            Slept?.Invoke(ok, line);
        }
    }
}
