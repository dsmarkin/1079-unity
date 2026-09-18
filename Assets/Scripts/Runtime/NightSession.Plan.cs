using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>How far along the guide's programme one climber is, as it travels to his own client. Three numbers,
    /// which is all the card, the compass and the two maps need: the ticks themselves, the day the programme started
    /// (so that «шестые сутки, программа — день четыре» is sayable) and the count of nights.
    ///
    /// The heights of the last night stay on the host. They are what the <b>conditions</b> read, and conditions are
    /// never evaluated on a client (<see cref="Programme.Advance"/> runs on the server and nowhere else).</summary>
    public struct PlanNet : INetworkSerializable, IEquatable<PlanNet>
    {
        /// <summary>Bit i — step i of <see cref="Programme.Steps"/> is done (<see cref="Progress.Done"/>).</summary>
        public ulong Done;
        /// <summary>The day the programme first ticked anything on, as <see cref="Forecast.DayIndex"/> plus one;
        /// 0 means it has not started.</summary>
        public int Started;
        /// <summary>How many nights this save has been through.</summary>
        public ushort Nights;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref Done); s.SerializeValue(ref Started); s.SerializeValue(ref Nights); }

        public bool Equals(PlanNet o) => Done == o.Done && Started == o.Started && Nights == o.Nights;
        public override int GetHashCode() => Done.GetHashCode() ^ Started;

        public static PlanNet Of(in Progress p) => new PlanNet
        {
            Done = p.Done,
            Started = p.Started == default ? 0 : Forecast.DayIndex(p.Started) + 1,
            Nights = (ushort)Math.Min(p.Night.Count, ushort.MaxValue),
        };

        /// <summary>Back into the value the texts take. The night's heights come back as zeroes on purpose: nothing on
        /// this side asks a condition anything.</summary>
        public Progress ToProgress() => new Progress(Done,
            Started > 0 ? Forecast.DateOf(Started - 1) : default,
            new LastNight(0f, 0f, Nights));
    }

    /// <summary>The guide's programme on the host.
    ///
    /// The division is the one everything else on this mountain keeps. <b>The host ticks the steps</b>: it is the
    /// side that knows where every body stands, what is in every rucksack, whether the crampons are actually on the
    /// boots and how last night went, so it is the only side that may call <see cref="Programme.Advance"/>. What it
    /// publishes is the answer — one <see cref="PlanNet"/> per hiker — and the owner gets a line of his own when a
    /// step closes.
    ///
    /// <see cref="Programme.Advance"/> is pure, idempotent and cheap (twenty-two predicates over one snapshot), so it
    /// runs inside the ascent tick, four times a second, beside everything else the mountain does to a body.
    ///
    /// The night is not ticked here. «Climb high, sleep low» is a statement about a day and the night that closed it,
    /// and by the time anybody wakes up <see cref="Climber.HighestEle"/> has been reset to the camp — so
    /// <see cref="Programme.Slept"/> is called in <see cref="SaveNight"/>, in the one place the acclimatisation is
    /// already being credited (<see cref="Ascent.Acclimatise"/>), and nowhere else.
    ///
    /// Silent off the southern slope: <see cref="Programme.AllowedIn"/> is false on Kholat Syakhl, every entry point
    /// below returns at once and no hiker ever gets a non-empty <see cref="HikerController.Plan"/>.</summary>
    public sealed partial class NightSession
    {
        /// <summary>The progress of every client, by client id. The truth; the clients only read the copy.</summary>
        readonly Dictionary<ulong, Progress> plans = new Dictionary<ulong, Progress>();
        /// <summary>Who has read the board at the rescue base at least once. The board is opened on the client
        /// (<see cref="WeatherBoards"/>), so the client says so; nothing but one step of day seven depends on it.</summary>
        readonly HashSet<ulong> forecastSeen = new HashSet<ulong>();

        public Progress PlanOf(ulong id) => plans.TryGetValue(id, out var p) ? p : Progress.None;

        static bool PlanOn => Programme.AllowedIn(Height1079.Core.World.Current);

        // ── the tick ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>One climber, one snapshot, once a climb tick. Called from <see cref="TickAscentServer"/>, where
        /// the position, the height, the kit and the crampons have all just been worked out anyway.
        ///
        /// <see cref="Standing.Of"/> takes a <see cref="SaveGame"/> and a <see cref="SaveClimber"/>, which is what a
        /// menu or a test has; the host has the live state instead and fills the same fields from it.</summary>
        void TickPlan(ulong id, Participant p, HikerController hiker, Climber c, RoutePoint point, float hour)
        {
            if (!PlanOn || hiker == null) return;
            var pos = hiker.transform.position;
            var now = new Standing
            {
                Place = Height1079.Core.World.Current,
                X = pos.x, Z = pos.z, Ele = point.Ele,
                Hour = hour,
                Date = Forecast.DateOf(WeatherDay.Value),
                Kit = c.Gear,
                CramponsOn = c.CramponsOn,
                Registered = Rescue.Watched(RescueOf(id)),
                ForecastRead = forecastSeen.Contains(id),
                Highest = c.HighestEle,
            };

            var before = PlanOf(id);
            var after = Programme.Advance(before, now);
            if (after.Done != before.Done)
            {
                plans[id] = after;
                string who = p != null ? p.Name : hiker.DisplayName;
                foreach (var step in Programme.NewlyDone(before, after))
                {
                    // the protocol gets one short line, the owner gets the same thing as a notice on his own screen
                    run.Record($"{who}: {Programme.DayOf(step.Day).Head} — {step.Title}: сделано.");
                    PlanDoneRpc((byte)step.Index, RpcTarget.Single(id, RpcTargetUse.Temp));
                }
            }
            PublishPlan(hiker, after);
        }

        void PublishPlan(HikerController hiker, in Progress p)
        {
            if (hiker == null) return;
            var net = PlanNet.Of(p);
            if (!hiker.Plan.Value.Equals(net)) hiker.Plan.Value = net;
        }

        // ── the night ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>A night was taken: the camp's height and the high point of the day that ended in it. Called from
        /// <see cref="SaveNight"/> before <see cref="Camp.Sleep"/> resets the sortie, and only for the bodies the
        /// night is actually credited to.</summary>
        void PlanSlept(ulong id, float campEle, float dayHighEle)
        {
            if (!PlanOn) return;
            plans[id] = Programme.Slept(PlanOf(id), campEle, dayHighEle);
            PublishPlan(HikerController.For(id), plans[id]);
        }

        /// <summary>This player was in the save: the ticks come back with the body.</summary>
        void PlanRestore(ulong id, in Progress p)
        {
            if (!PlanOn) return;
            plans[id] = p;
            PublishPlan(HikerController.For(id), p);
        }

        // ── the board at the rescue base ──────────────────────────────────────────────────────────────────

        /// <summary>Owner: the forecast board has been read. One of the four steps of day seven asks for it, and
        /// reading a slate is a thing that happens on a screen, not on a server.</summary>
        public void ReportForecastRead() => ForecastReadRpc();

        [Rpc(SendTo.Server)]
        void ForecastReadRpc(RpcParams rpc = default)
        {
            if (!PlanOn) return;
            forecastSeen.Add(rpc.Receive.SenderClientId);
        }

        // ── back to the owner ─────────────────────────────────────────────────────────────────────────────

        [Rpc(SendTo.SpecifiedInParams)]
        void PlanDoneRpc(byte index, RpcParams rpc) => Programmes.Ticked(Programme.At(index));

        void PlanClientLeft(ulong id)
        {
            plans.Remove(id);
            forecastSeen.Remove(id);
        }
    }
}
