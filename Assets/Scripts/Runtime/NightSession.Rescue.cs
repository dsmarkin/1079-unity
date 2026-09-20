#if !HEIGHT1079_NO_ELBRUS
// Compiled only when the southern slope of Elbrus is in the build (docs/ELBRUS.md). It has to live in
// Height1079.Runtime and not in the location's own assembly because a partial class cannot be split across
// assemblies, and because the rest of Height1079.Runtime's Elbrus partials name what is in here.
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>What the rescue service knows about one climber, as it travels from the host to that climber.
    ///
    /// Two things and no more: the slip he left at the counter, and what — if anything — is being done about him now.
    /// Both are the host's: it owns the registration, it owns the SOS and it owns the plan, exactly as it owns the
    /// frostbite. The client only reads this and writes the Russian line out of it every frame
    /// (<see cref="Rescue.Line"/>), so the countdown ticks smoothly without the wire carrying a countdown.</summary>
    public struct RescueNet : INetworkSerializable, System.IEquatable<RescueNet>
    {
        /// <summary>1 — the slip was filed.</summary>
        public const byte FiledFlag = 1;

        public byte Flags;
        public byte People;
        /// <summary>Who comes (<see cref="RescueKind"/>) and how the service learned (<see cref="Alarm"/>).</summary>
        public byte Kind, How;
        public float ControlHour, BackHour;
        /// <summary>Absolute hours of the run clock, +∞ when there is no operation.</summary>
        public float AlarmHour, ReachHour, SafeHour;
        public FixedString64Bytes Party;
        public FixedString128Bytes Route;
        /// <summary>Why the plan is what it is, in Russian, straight out of <see cref="Rescue.Plan"/>.</summary>
        public FixedString512Bytes Note;

        public bool Filed => (Flags & FiledFlag) != 0;

        public Registration Reg => Filed
            ? Rescue.File(Party.ToString(), People, Route.ToString(), ControlHour, BackHour)
            : Rescue.NotFiled;

        /// <summary>The operation as <see cref="Rescue.Line"/> wants it. The title is derived and not sent: there are
        /// exactly three of them.</summary>
        public Rescue.Mission Mission => new Rescue.Mission((RescueKind)Kind, (Alarm)How,
            AlarmHour, ReachHour, SafeHour, Title, Note.ToString());

        public string Title => (RescueKind)Kind switch
        {
            RescueKind.Helicopter => "Идёт борт",
            RescueKind.Foot => "Идут пешком",
            _ => "Никто не ищет",
        };

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Flags); s.SerializeValue(ref People);
            s.SerializeValue(ref Kind); s.SerializeValue(ref How);
            s.SerializeValue(ref ControlHour); s.SerializeValue(ref BackHour);
            s.SerializeValue(ref AlarmHour); s.SerializeValue(ref ReachHour); s.SerializeValue(ref SafeHour);
            s.SerializeValue(ref Party); s.SerializeValue(ref Route); s.SerializeValue(ref Note);
        }

        public bool Equals(RescueNet o)
            => Flags == o.Flags && People == o.People && Kind == o.Kind && How == o.How
            && ControlHour.Equals(o.ControlHour) && BackHour.Equals(o.BackHour)
            && AlarmHour.Equals(o.AlarmHour) && ReachHour.Equals(o.ReachHour) && SafeHour.Equals(o.SafeHour)
            && Party.Equals(o.Party) && Route.Equals(o.Route) && Note.Equals(o.Note);

        public override int GetHashCode() => Flags ^ (Kind << 8) ^ (How << 16);
    }

    /// <summary>Registering with the ЭВПСО МЧС, and what happens when a party does not come back — on the host.
    ///
    /// The rules are all in <see cref="Rescue"/>; this is the wiring. A party fills a slip in at the desk inside the
    /// base on the Azau meadow (<c>Elb_RescueDesk</c>, see <see cref="RescueDesk"/>), and from then on the duty
    /// officer is watching the clock. Miss the hour you said you would be back by, plus the grace, and somebody
    /// starts looking. Press SOS below the radio ceiling and somebody starts looking at once. Walk past the desk and
    /// <b>nobody ever does</b> — not out of spite, but because there is nothing to look for. That asymmetry is the
    /// whole point of the counter, and the HUD is allowed to be blunt about it.
    ///
    /// Help is never instant: a helicopter is an hour of scramble and twenty minutes of lifting, and only in flying
    /// weather; a party on foot is hours from Pastukhov rocks; without aviation an evacuation is about a day. The
    /// plan is re-asked every few seconds, because the weather that grounds the machine is the weather of the minute.
    ///
    /// The slip goes into the save beside the climber it belongs to (<see cref="SaveClimber.Reg"/>), so a party that
    /// registered on Monday is still registered when the save comes back on Tuesday.
    ///
    /// Silent off the southern slope: every entry point returns when <see cref="Climb.On"/> is false.</summary>
    public sealed partial class NightSession
    {
        /// <summary>One climber's paperwork on the host.</summary>
        sealed class Watch
        {
            public Registration Reg = Rescue.NotFiled;
            public Sos Sos = Rescue.NoSos;
            public Rescue.Mission Mission;
            /// <summary>Said once, when the rescuers actually get there.</summary>
            public bool Arrived;
            /// <summary>Wall-clock seconds at which the plan is asked for again — the weather may have changed.</summary>
            public double Replan;
            /// <summary>Said once, above the last roof, that nobody is going to come looking.</summary>
            public bool Told;
            /// <summary>What was last sent to this client, so an unchanged slip does not travel every tick.</summary>
            public RescueNet Sent;
            public bool EverSent;
        }

        readonly Dictionary<ulong, Watch> watches = new Dictionary<ulong, Watch>();

        /// <summary>How often the duty officer looks at the weather again, in wall-clock seconds.</summary>
        const float ReplanSeconds = 5f;

        Watch WatchOf(ulong id)
        {
            if (watches.TryGetValue(id, out var w)) return w;
            w = new Watch();
            watches[id] = w;
            return w;
        }

        /// <summary>Host, every ascent tick: has anybody noticed, and what are they doing about it.</summary>
        Watch RescueTick(ulong id, string token, Participant p, Climber c, RoutePoint point, MountainAir air,
            float hour, bool daylight)
        {
            var w = WatchOf(id);

            // above the last roof, unregistered, once: this is the sentence the counter exists to make true
            if (!w.Reg.Filed && !w.Told && point.Ele >= AscentRoute.LastHutEle)
            {
                w.Told = true;
                run.Record($"{p.Name} идёт выше приютов, не отметившись в ЭВПСО. Если не вернётся, искать не начнут.");
                RescueNoteRpc(Say("Вы не зарегистрированы в отряде. Не вернётесь — никто не будет знать, что вас нет."),
                    RpcTarget.Single(id, RpcTargetUse.Temp));
            }

            bool known = w.Sos.Sent || Rescue.SearchStarted(w.Reg, hour);
            if (known && !w.Mission.Off(hour) && Time.timeAsDouble >= w.Replan)
            {
                w.Replan = Time.timeAsDouble + ReplanSeconds;
                var ask = new RescueAsk(point.Ele, air.WindMs, air.VisibilityM, daylight);
                bool wasComing = w.Mission.Coming;
                w.Mission = Rescue.Plan(w.Reg, w.Sos, ask);
                if (!wasComing && w.Mission.Coming)
                    run.Record(w.Mission.How == Alarm.Sos
                        ? $"ЭВПСО принял сигнал от группы «{w.Reg.Party}». {w.Mission.Title}."
                        : $"Группа «{w.Reg.Party}» не вернулась к контрольному сроку. {w.Mission.Title}.");
            }
            if (w.Mission.Arrived(hour) && !w.Arrived)
            {
                w.Arrived = true;
                run.Record(w.Mission.Kind == RescueKind.Helicopter
                    ? $"Борт над {p.Name}. Ждали {w.Mission.WaitHours:0.#} ч."
                    : $"Спасотряд дошёл до {p.Name}. Ждали {w.Mission.WaitHours:0.#} ч.");
            }

            Send(id, w, hour);
            return w;
        }

        void Send(ulong id, Watch w, float hour)
        {
            var net = new RescueNet
            {
                Flags = (byte)(w.Reg.Filed ? RescueNet.FiledFlag : 0),
                People = (byte)Mathf.Clamp(w.Reg.Filed ? w.Reg.People : 0, 0, 255),
                Kind = (byte)w.Mission.Kind,
                How = (byte)w.Mission.How,
                ControlHour = w.Reg.Filed ? w.Reg.ControlHour : 0f,
                BackHour = w.Reg.Filed ? w.Reg.BackHour : 0f,
                AlarmHour = w.Mission.AlarmHour,
                ReachHour = w.Mission.ReachHour,
                SafeHour = w.Mission.SafeHour,
                Party = Short(w.Reg.Filed ? w.Reg.Party : ""),
                Route = Mid(w.Reg.Filed ? w.Reg.Route : ""),
                Note = Say(w.Mission.Coming ? w.Mission.Note : ""),
            };
            if (w.EverSent && net.Equals(w.Sent)) return;
            w.Sent = net; w.EverSent = true;
            RescueRpc(net, RpcTarget.Single(id, RpcTargetUse.Temp));
        }

        // ── the desk ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Owner: leave the slip at the ЭВПСО counter. The host signs it in the name the protocol knows the
        /// player by, because that is the name a search party would be given.</summary>
        public void FileRescue(int people, string route, float controlHour, float backHour)
            => FileRescueRpc((byte)Mathf.Clamp(people, 1, 12), Mid(route), controlHour, backHour);

        [Rpc(SendTo.Server)]
        void FileRescueRpc(byte people, FixedString128Bytes route, float controlHour, float backHour,
            RpcParams rpc = default)
        {
            ulong id = rpc.Receive.SenderClientId;
            if (!Climb.On || run == null) return;
            if (!run.Players.TryGetValue(Token(id), out var p) || p.Outcome != Outcome.None) return;
            var w = WatchOf(id);
            w.Reg = Rescue.File(p.Name, people, route.ToString(),
                Mathf.Clamp(controlHour, AscentRoute.RunStartHour, Forecast.LastHour),
                Mathf.Clamp(backHour, AscentRoute.RunStartHour, Forecast.LastHour));
            w.Told = true;
            w.Replan = 0d;
            run.Record($"ЭВПСО: {w.Reg.Line}");
            RescueNoteRpc(Say("Записаны в ЭВПСО. " + Rescue.CounterText(w.Reg)), RpcTarget.Single(id, RpcTargetUse.Temp));
            Send(id, w, Climb.Hour(run.Elapsed));
        }

        // ── the button ────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Owner: press SOS. Above <see cref="Rescue.SignalCeiling"/> the message is not sent late — it is
        /// not sent at all, and the only thing that changes that is losing height. The refusal says how much.</summary>
        public void SendSos() => SosRpc();

        [Rpc(SendTo.Server)]
        void SosRpc(RpcParams rpc = default)
        {
            ulong id = rpc.Receive.SenderClientId;
            if (!Climb.On || run == null || dem == null) return;
            var hiker = HikerController.For(id);
            if (hiker == null || !run.Players.TryGetValue(Token(id), out var p) || p.Outcome != Outcome.None) return;
            var w = WatchOf(id);
            if (w.Sos.Sent)
            {
                RescueNoteRpc(Say("Сигнал уже принят. " + Rescue.Line(w.Mission, Climb.Hour(run.Elapsed))),
                    RpcTarget.Single(id, RpcTargetUse.Temp));
                return;
            }
            var pos = hiker.transform.position;
            float ele = dem.Sample(pos.x, pos.z);
            float hour = Climb.Hour(run.Elapsed);
            var sos = Rescue.SendSos(ele, hour);
            if (!sos.Sent)
            {
                // the honest part: how many metres of descent buy a phone call
                RescueNoteRpc(Say(sos.Why), RpcTarget.Single(id, RpcTargetUse.Temp));
                return;
            }
            w.Sos = sos;
            w.Replan = 0d;
            run.Record($"{p.Name} даёт SOS с {ele:0} м, {AscentRoute.Clock(hour)}.");
            RescueNoteRpc(Say($"SOS ушёл с {ele:0} м. Ждите, помощь идёт не сразу."),
                RpcTarget.Single(id, RpcTargetUse.Temp));
            Send(id, w, hour);
        }

        // ── the save ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The slip this climber filed, for the save.</summary>
        Registration RescueOf(ulong id) => WatchOf(id).Reg;

        /// <summary>And back again, when a save turns out to know this player.</summary>
        void RescueRestore(ulong id, Registration reg)
        {
            var w = WatchOf(id);
            w.Reg = reg;
            if (reg.Filed) w.Told = true;
            w.EverSent = false;
        }

        void RescueClientLeft(ulong id) => watches.Remove(id);

        // ── back to the client ────────────────────────────────────────────────────────────────────────────

        /// <summary>A key, the middle size: <c>FixedString128Bytes</c> holds a hundred and twenty-five bytes, which is
        /// about sixty Russian letters — enough for the name of a route.</summary>
        static FixedString128Bytes Mid(string text)
        {
            var s = new FixedString128Bytes();
            s.Append(Cut(text, 124));
            return s;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void RescueRpc(RescueNet net, RpcParams rpc) => RescueDesk.Take(net);

        [Rpc(SendTo.SpecifiedInParams)]
        void RescueNoteRpc(FixedString512Bytes text, RpcParams rpc) => Note(text.ToString());
    }
}
#endif
