using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Everything the player does with the machinery of the Elbrus map: waiting at a terminal, stepping into a
    /// cabin, a chair or a snow-cat, riding it and getting out. Also drives the shared ropeway clock, which the passenger
    /// can run fast (a real ride from Azau to Gara-Bashi takes some twenty minutes).</summary>
    public sealed class ElbrusRides : MonoBehaviour
    {
        /// <summary>How much faster the ropeways run when the passenger asks for it (F11).</summary>
        public const float Warp = 8f;
        const float TerminalReach = 14f, RatrakReach = 6f;

        RopewayRig rig;
        int car = -1;
        bool boardedAtBottom;
        RatrakRide ratrak;
        bool fast;
        /// <summary>Waiting in the queue at a terminal: the next car that pulls in takes the passenger without another key press.</summary>
        RopewayRig queued;
        bool queuedAtBottom;

        void Update()
        {
            RopewayRig.Clock += Time.deltaTime * (fast ? Warp : 1f);
            if (Controls.TimeWarp) fast = !fast;

            var me = Bootstrap.LocalHiker;
            var hud = Bootstrap.Hud;
            if (me == null || !me.IsOwner) return;

            if (me.Ride != null) { Riding(me, hud); return; }
            rig = null; car = -1; ratrak = null;
            Waiting(me, hud);
        }

        void Riding(HikerController me, HudController hud)
        {
            if (ratrak != null)
            {
                string where = ratrak.Standing ? "E — выйти" : "едем наверх";
                hud?.SetPrompt($"Ратрак · {ratrak.Altitude:0} м · {where}");
                if (ratrak.Standing && Controls.Board)
                {
                    var p = ratrak.transform.position + ratrak.transform.right * 3.2f;
                    me.LeaveRide(new Vector3(p.x, Bootstrap.Dem.Sample(p.x, p.z) + .1f, p.z));
                    ratrak = null;
                }
                return;
            }
            if (rig == null || car < 0 || rig.Car(car) == null) return;
            var state = rig.Line.CarAt(car, RopewayRig.Clock);
            bool arrived = state.Boardable && (boardedAtBottom ? state.S > rig.Line.Length - Ropeway.SlowZone : state.S < Ropeway.SlowZone);
            float left = Mathf.Abs(boardedAtBottom ? rig.Line.Length - state.S : state.S);
            var pos = rig.Car(car).position;
            hud?.SetPrompt($"{rig.Spec.Name} · {pos.y:0} м · ещё {left:0} м" + (fast ? " · ×8 (F11)" : " · F11 — быстрее") + (state.Boardable ? " · E — выйти" : ""));
            if (arrived || (state.Boardable && Controls.Board))
            {
                me.LeaveRide(rig.ExitPoint(car));
                rig = null; car = -1;
                hud?.SetPrompt("");
            }
        }

        void Waiting(HikerController me, HudController hud)
        {
            var p = me.transform.position;
            // a snow-cat first: it stands right in the camp
            RatrakRide nearestCat = null; float best = RatrakReach;
            foreach (var r in ElbrusWorld.Ratraks)
            {
                if (r == null || !r.Standing) continue;
                float d = Vector3.Distance(p, r.transform.position);
                if (d < best) { best = d; nearestCat = r; }
            }
            if (nearestCat != null)
            {
                hud?.SetPrompt("Ратрак до 5100 · E — сесть");
                if (Controls.Board) { me.BoardRide(nearestCat.Seat); nearestCat.Go(); ratrak = nearestCat; }
                return;
            }

            RopewayRig bestRig = null; bool bestBottom = false; int bestCar = -1; float bestD = TerminalReach;
            foreach (var r in ElbrusWorld.Lines)
            {
                if (r == null) continue;
                for (int end = 0; end < 2; end++)
                {
                    bool bottom = end == 0;
                    var t = r.TerminalPoint(bottom);
                    float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(t.x, t.z));
                    if (d >= bestD) continue;
                    bestD = d; bestRig = r; bestBottom = bottom; bestCar = r.Boardable(bottom);
                }
            }
            if (bestRig == null) { hud?.SetPrompt(""); queued = null; return; }
            if (queued != null && (queued != bestRig || queuedAtBottom != bestBottom)) queued = null;
            string dirText = bestBottom ? "наверх" : "вниз";
            bool waiting = queued == bestRig;
            if (bestCar < 0)
            {
                // no car in the loading zone yet: E puts the hiker in the queue and the next one takes them
                float wait = bestRig.Line.SecondsToBoard(RopewayRig.Clock, bestBottom);
                string when = wait < 0 ? "" : $" · кабина через {Mathf.CeilToInt(wait / (fast ? Warp : 1f))} с";
                hud?.SetPrompt(waiting
                    ? $"{bestRig.Spec.Name} · {dirText} · ждём кабину{when}" + (fast ? " · ×8 (F11)" : " · F11 — быстрее")
                    : $"{bestRig.Spec.Name} · {dirText} · E — встать на посадку{when}");
                if (Controls.Board) queued = queued == bestRig ? null : bestRig;
                queuedAtBottom = bestBottom;
                return;
            }
            hud?.SetPrompt($"{bestRig.Spec.Name} · {dirText} · " + (waiting ? "садимся" : "E — сесть"));
            if (!waiting && !Controls.Board) return;
            if (bestRig.Seat(bestCar) == null) return;
            me.BoardRide(bestRig.Seat(bestCar));
            rig = bestRig; car = bestCar; boardedAtBottom = bestBottom; queued = null;
        }
    }
}
