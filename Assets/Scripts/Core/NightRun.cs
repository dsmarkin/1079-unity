using System;
using System.Collections.Generic;
using System.Linq;

namespace Height1079.Core
{
    public readonly struct NightEvent
    {
        public readonly float Time; public readonly string Text;
        public NightEvent(float time, string text) { Time = time; Text = text; }
    }

    /// <summary>Authoritative night for one room. Pure state machine: no engine, no sockets, no timers. Port of server/run.js.
    /// Time is passed in as seconds (double) from any monotonic clock.</summary>
    public sealed class NightRun
    {
        public const double GraceSeconds = 120;
        public const float FireDurationSeconds = 90f;

        readonly Func<float, float, float> groundHeight;
        public readonly double StartedAt;
        double tickedAt;
        public float Elapsed { get; private set; }
        public bool Storm { get; private set; }
        /// <summary>Absolute time until which the shared fire burns; 0 when out.</summary>
        public double FireUntil { get; private set; }
        public Outcome Outcome { get; private set; } = Outcome.None;
        public readonly List<NightEvent> Events = new List<NightEvent>();
        public readonly Dictionary<string, Participant> Players = new Dictionary<string, Participant>();

        public static readonly (float x, float z) Start = (WorldData.Camp.x + 2f, WorldData.Camp.z + 3f);

        /// <param name="groundHeight">Height sampler used for the shelter check (below the tree line).</param>
        public NightRun(double now, Func<float, float, float> groundHeight)
        {
            this.groundHeight = groundHeight;
            StartedAt = tickedAt = now;
            Record("Выход от лесного кострища. Цель — верхнее укрытие.");
        }

        public void Record(string text) => Events.Add(new NightEvent(Elapsed, text));

        public float FireRemaining(double now) => (float)Math.Max(0, FireUntil - now);

        /// <summary>Spawn positions are spread around the camp so a companion never appears inside the first hiker.</summary>
        public static (float x, float z) SpawnFor(int index)
        {
            float r = index == 0 ? 0f : 1.4f;
            return (Start.x + (float)Math.Cos(index * 2.1) * r, Start.z + (float)Math.Sin(index * 2.1) * r);
        }

        public Participant AddPlayer(string token, string name, double now)
        {
            if (Players.TryGetValue(token, out var existing))
            {
                existing.Online = true; existing.LastSeen = now; existing.KindlingStarted = null;
                return existing;
            }
            var (x, z) = SpawnFor(Players.Count);
            var p = new Participant { Token = token, Name = name, X = x, Z = z, Online = true, LastSeen = now, JoinedAt = Elapsed };
            Players[token] = p;
            if (Elapsed > 5f) Record($"{name} присоединяется к ночи.");
            return p;
        }

        public void SetOffline(string token, double now)
        {
            if (!Players.TryGetValue(token, out var p)) return;
            p.Online = false; p.LastSeen = now; p.KindlingStarted = null;
        }

        public void Move(string token, float x, float z, double now)
        {
            if (!Players.TryGetValue(token, out var p) || p.Outcome != Outcome.None) return;
            p.Travel += WorldData.Distance(x, z, p.X, p.Z);
            p.X = x; p.Z = z; p.LastSeen = now;
            if (p.KindlingStarted.HasValue && WorldData.Distance(x, z, p.KindlingX, p.KindlingZ) > .5f) p.KindlingStarted = null;
        }

        public bool BeginKindling(string token, double now)
        {
            if (!Players.TryGetValue(token, out var p) || p.Outcome != Outcome.None || FireUntil > now || Outcome != Outcome.None) return false;
            if (!WorldData.NearCamp(p.X, p.Z)) return false;
            p.KindlingStarted = now; p.KindlingX = p.X; p.KindlingZ = p.Z;
            return true;
        }

        public void StopKindling(string token)
        {
            if (Players.TryGetValue(token, out var p)) p.KindlingStarted = null;
        }

        static Outcome PairOutcome(List<Participant> players)
        {
            if (players.Count == 1) return players[0].Outcome;
            if (players.All(p => p.Outcome == Outcome.Arrival)) return Outcome.Together;
            if (players.All(p => p.Outcome == Outcome.Cold)) return Outcome.Lost;
            if (players.Any(p => p.Outcome == Outcome.Arrival) && players.Any(p => p.Outcome == Outcome.Cold)) return Outcome.Separated;
            return Outcome.Dawn;
        }

        /// <summary>Advance the room to <paramref name="now"/>. Returns true when something protocol-worthy changed.</summary>
        public bool Step(double now)
        {
            if (Outcome != Outcome.None) return false;
            float dt = (float)Math.Min(1.0, now - tickedAt);
            tickedAt = now;
            Elapsed = (float)(now - StartedAt);
            int before = Events.Count;
            bool storm = SurvivalRules.StormAt(Elapsed);
            if (storm != Storm) { Storm = storm; Record(storm ? "Видимость упала. Началась метель." : "Ветер ослаб."); }

            foreach (var token in Players.Keys.ToList())
            {
                var p = Players[token];
                if (!p.Online && now - p.LastSeen > GraceSeconds)
                {
                    Players.Remove(token);
                    Record($"{p.Name} пропадает из связи и выбывает из ночи.");
                    continue;
                }
                if (!p.Online || p.Outcome != Outcome.None) continue;
                bool burning = FireUntil > now, near = WorldData.NearCamp(p.X, p.Z);
                if (p.KindlingStarted.HasValue)
                {
                    if (burning || !near) p.KindlingStarted = null;
                    else if (now - p.KindlingStarted.Value >= SurvivalRules.KindleSeconds(p.Hands))
                    {
                        p.KindlingStarted = null;
                        FireUntil = now + FireDurationSeconds;
                        Record($"{p.Name} разжигает общий костёр{(p.Hands < 55f ? " окоченевшими руками" : "")}.");
                    }
                }
                bool companion = Players.Values.Any(o => o != p && o.Online && o.Outcome == Outcome.None && WorldData.Distance(o.X, o.Z, p.X, p.Z) < 5f);
                bool moving = p.Travel / Math.Max(dt, 1e-3f) > .3f;
                p.Travel = 0f;
                p.Elapsed = Elapsed - dt;
                var c = new SurvivalRules.Conditions
                {
                    Moving = moving, Storm = storm, Fire = near && FireUntil > now, Companion = companion,
                    Sheltered = groundHeight(p.X, p.Z) < WorldData.ShelterHeight, Goal = WorldData.AtGoal(p.X, p.Z)
                };
                if (SurvivalRules.Tick(p, dt, c))
                {
                    p.KindlingStarted = null;
                    Record($"{p.Name}: {SurvivalRules.Describe(p.Outcome).Title.ToLowerInvariant()} ({SurvivalRules.NightTime(Elapsed)}).");
                }
            }

            var active = Players.Values.Where(p => p.Online || now - p.LastSeen <= GraceSeconds).ToList();
            if (active.Count > 0 && active.All(p => p.Outcome != Outcome.None))
            {
                Outcome = PairOutcome(active);
                Record($"Итог ночи: {SurvivalRules.Describe(Outcome).Title.ToLowerInvariant()}.");
            }
            return Events.Count != before || Outcome != Outcome.None;
        }

        /// <summary>Kindling progress for HUD: (seconds held, seconds needed) or null.</summary>
        public (float progress, float needed)? Kindling(string token, double now)
        {
            if (!Players.TryGetValue(token, out var p) || !p.KindlingStarted.HasValue) return null;
            return ((float)(now - p.KindlingStarted.Value), SurvivalRules.KindleSeconds(p.Hands));
        }
    }
}
