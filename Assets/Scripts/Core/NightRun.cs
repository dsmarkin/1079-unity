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
        double skipped;

        /// <summary>Testing: move the night clock forward (sky, storms, dawn) without charging the players for the skipped time.</summary>
        public void SkipAhead(double seconds) => skipped += Math.Max(0, seconds);
        public bool Storm { get; private set; }
        /// <summary>Absolute time until which the shared fire burns; 0 when out.</summary>
        public double FireUntil { get; private set; }
        public Outcome Outcome { get; private set; } = Outcome.None;
        public readonly List<NightEvent> Events = new List<NightEvent>();
        public readonly Dictionary<string, Participant> Players = new Dictionary<string, Participant>();

        public static readonly (float x, float z) Start = (WorldData.Camp.x + 2f, WorldData.Camp.z + 3f);

        /// <summary>What a run is about: where it starts, what counts as shelter, where the goal is and how hard the clock presses.
        /// <see cref="Slope"/> is the night of 1–2 February on Kholat Syakhl; the Elbrus location passes its own.</summary>
        public sealed class Scenario
        {
            public string Id = "slope";
            public string Intro = "";
            public (float x, float z) Start;
            /// <summary>Where a shared fire may be kindled (null: nowhere).</summary>
            public Func<float, float, bool> NearFireplace;
            public Func<float, float, bool> AtGoal;
            /// <summary>x, z and the ground height there.</summary>
            public Func<float, float, float, bool> Sheltered;
            public bool Storms = true;
            public SurvivalRules.Profile Profile = SurvivalRules.Profile.Night;
            /// <summary>Spread of the spawn ring around the start, metres.</summary>
            public float SpawnRadius = 1.4f;

            public static readonly Scenario Slope = new Scenario
            {
                Id = "slope",
                Intro = "Ночёвка 31 января у лабаза, долина Ауспии. Подъём ≈1,7 км к палатке на склоне.",
                Start = NightRun.Start,
                NearFireplace = WorldData.NearCamp,
                AtGoal = WorldData.AtGoal,
                Sheltered = (x, z, y) => y < WorldData.ShelterHeight,
            };
        }

        public readonly Scenario Plan;

        /// <param name="groundHeight">Height sampler used for the shelter check (below the tree line).</param>
        public NightRun(double now, Func<float, float, float> groundHeight) : this(now, groundHeight, null) { }

        public NightRun(double now, Func<float, float, float> groundHeight, Scenario scenario)
        {
            Plan = scenario ?? Scenario.Slope;
            this.groundHeight = groundHeight;
            StartedAt = tickedAt = now;
            if (!string.IsNullOrEmpty(Plan.Intro)) Record(Plan.Intro);
        }

        public void Record(string text) => Events.Add(new NightEvent(Elapsed, text));

        public float FireRemaining(double now) => (float)Math.Max(0, FireUntil - now);

        /// <summary>Spawn positions are spread around the camp so a companion never appears inside the first hiker.</summary>
        public static (float x, float z) SpawnFor(int index) => SpawnFor(index, Scenario.Slope);

        public static (float x, float z) SpawnFor(int index, Scenario plan)
        {
            float r = index == 0 ? 0f : plan.SpawnRadius;
            return (plan.Start.x + (float)Math.Cos(index * 2.1) * r, plan.Start.z + (float)Math.Sin(index * 2.1) * r);
        }

        public (float x, float z) Spawn(int index) => SpawnFor(index, Plan);

        public Participant AddPlayer(string token, string name, double now)
        {
            if (Players.TryGetValue(token, out var existing))
            {
                existing.Online = true; existing.LastSeen = now; existing.KindlingStarted = null;
                return existing;
            }
            var (x, z) = SpawnFor(Players.Count, Plan);
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

        /// <summary>Does this participant have what it takes to light the fire — a dry match and something to burn? (Set by the host from the packs.)</summary>
        public Func<string, bool> KindleSupplies = _ => true;
        /// <summary>Called when the fire catches: the match and the firewood are spent here.</summary>
        public Action<string> KindleSpent = _ => { };

        public bool BeginKindling(string token, double now)
        {
            if (!Players.TryGetValue(token, out var p) || p.Outcome != Outcome.None || FireUntil > now || Outcome != Outcome.None) return false;
            if (Plan.NearFireplace == null || !Plan.NearFireplace(p.X, p.Z)) return false;
            if (!KindleSupplies(token)) return false;
            p.KindlingStarted = now; p.KindlingX = p.X; p.KindlingZ = p.Z;
            return true;
        }

        public void StopKindling(string token)
        {
            if (Players.TryGetValue(token, out var p)) p.KindlingStarted = null;
        }

        /// <summary>Logs go into the burning fire: each buys <see cref="Woodwork.SecondsPerLog"/> more of it.</summary>
        public bool FeedFire(string token, int logs, double now)
        {
            if (logs <= 0 || Outcome != Outcome.None) return false;
            if (!Players.TryGetValue(token, out var p) || p.Outcome != Outcome.None) return false;
            if (!WorldData.NearCamp(p.X, p.Z) || FireUntil <= now) return false;
            FireUntil += logs * Woodwork.SecondsPerLog;
            Record($"{p.Name} подкладывает дров в костёр.");
            return true;
        }

        static Outcome PairOutcome(List<Participant> players)
        {
            if (players.Count == 1) return players[0].Outcome;
            if (players.All(p => p.Outcome == Outcome.Arrival)) return Outcome.Together;
            if (players.All(p => SurvivalRules.Fell(p.Outcome))) return Outcome.Lost;
            if (players.Any(p => p.Outcome == Outcome.Arrival) && players.Any(p => SurvivalRules.Fell(p.Outcome))) return Outcome.Separated;
            return Outcome.Dawn;
        }

        /// <summary>Advance the room to <paramref name="now"/>. Returns true when something protocol-worthy changed.</summary>
        public bool Step(double now)
        {
            if (Outcome != Outcome.None) return false;
            float dt = (float)Math.Min(1.0, now - tickedAt);
            tickedAt = now;
            Elapsed = (float)(now - StartedAt + skipped);
            int before = Events.Count;
            bool storm = Plan.Storms && SurvivalRules.StormAt(Elapsed);
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
                bool burning = FireUntil > now, near = Plan.NearFireplace != null && Plan.NearFireplace(p.X, p.Z);
                if (p.KindlingStarted.HasValue)
                {
                    if (burning || !near) p.KindlingStarted = null;
                    else if (now - p.KindlingStarted.Value >= SurvivalRules.KindleSeconds(p.Hands))
                    {
                        p.KindlingStarted = null;
                        FireUntil = now + FireDurationSeconds;
                        KindleSpent(token);
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
                    Sheltered = Plan.Sheltered != null && Plan.Sheltered(p.X, p.Z, groundHeight(p.X, p.Z)),
                    Goal = Plan.AtGoal != null && Plan.AtGoal(p.X, p.Z),
                    // breaking trail on foot soaks the clothes and the heat goes with them; on skis you stay on top
                    Mode = p.Mode, Sink = moving ? p.Sink : 0f,
                };
                if (SurvivalRules.Tick(p, dt, c, Plan.Profile))
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

        /// <summary>A blow from the forest giant: knocks warmth and wits out of the player; a player with nothing left is taken.
        /// Returns true when the blow decided the player's outcome.</summary>
        public bool Strike(string token, float damage, string text)
        {
            if (Outcome != Outcome.None || !Players.TryGetValue(token, out var p) || p.Outcome != Outcome.None) return false;
            p.Heat = Math.Max(0f, p.Heat - damage);
            p.Clarity = Math.Max(0f, p.Clarity - damage * .6f);
            p.KindlingStarted = null;
            Record(text.Replace("{name}", p.Name));
            if (p.Heat > 1f) return false;
            p.Outcome = Outcome.Taken;
            Record($"{p.Name}: {SurvivalRules.Describe(p.Outcome).Title.ToLowerInvariant()} ({SurvivalRules.NightTime(Elapsed)}).");
            return true;
        }

        /// <summary>A hard knock that nobody dealt: a slide stopped with the axe, a leg through a snow bridge on the
        /// Garabashi glacier. Takes warmth and wits and writes a line; whether it ends the run is left to the next
        /// <see cref="Step"/>, which is where the cold has always decided things.</summary>
        public bool Hurt(string token, float damage, string text)
        {
            if (Outcome != Outcome.None || !Players.TryGetValue(token, out var p) || p.Outcome != Outcome.None) return false;
            p.Heat = Math.Max(0f, p.Heat - damage);
            p.Clarity = Math.Max(0f, p.Clarity - damage * .5f);
            p.KindlingStarted = null;
            Record(text.Replace("{name}", p.Name));
            return true;
        }

        /// <summary>A slide nobody arrested, on a belt <see cref="Ascent.FallIsFatal"/> marks. This one does not go
        /// through the warmth: a run-out of three to six hundred metres onto the ice cliffs below the saddle ends the
        /// ascent whatever is left in the participant.</summary>
        public bool Fall(string token, string text)
        {
            if (Outcome != Outcome.None || !Players.TryGetValue(token, out var p) || p.Outcome != Outcome.None) return false;
            p.KindlingStarted = null;
            Record(text.Replace("{name}", p.Name));
            p.Outcome = Outcome.Fall;
            Record($"{p.Name}: {SurvivalRules.Describe(p.Outcome).Title.ToLowerInvariant()}.");
            return true;
        }

        /// <summary>Kindling progress for HUD: (seconds held, seconds needed) or null.</summary>
        public (float progress, float needed)? Kindling(string token, double now)
        {
            if (!Players.TryGetValue(token, out var p) || !p.KindlingStarted.HasValue) return null;
            return ((float)(now - p.KindlingStarted.Value), SurvivalRules.KindleSeconds(p.Hands));
        }
    }
}
