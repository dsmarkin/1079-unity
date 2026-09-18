using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The ascent on the host's side: one <see cref="Climber"/> per participant, ticked through
    /// <see cref="Ascent.Tick"/> four times a second, and the answer published in
    /// <see cref="HikerController.Climb"/> for everybody to read.
    ///
    /// The division is the same one the rest of the game keeps. The host owns everything that must not be argued with:
    /// the frostbite, the dryness, the drowsiness, the heart, the mountain sickness, the acclimatisation, whether the
    /// crampons are actually on the boots, and every die that decides a fall. The owner of a hiker owns only its
    /// position, and works the pace out for itself from the published pools plus the height field — the same function
    /// of the same numbers, so the two never disagree (<see cref="ClimbGear"/>).
    ///
    /// Nothing here runs anywhere but on the southern slope of Elbrus: on Kholat Syakhl
    /// <see cref="Climb.On"/> is false and <see cref="TickAscentServer"/> returns on its first line.</summary>
    public sealed partial class NightSession
    {
        /// <summary>The weather of the mountain, which is not the blizzard cycle of the 1959 night: it breaks by
        /// itself after noon (<see cref="AscentRoute.WeatherRiskPerHour"/>) and then it stays broken. The clients
        /// read it for the driving snow and for how far anyone can see.</summary>
        public readonly NetworkVariable<bool> MountainStorm = new NetworkVariable<bool>();
        /// <summary>Centimetres of new snow since the wands were last readable, ×4.</summary>
        public readonly NetworkVariable<byte> FreshSnow = new NetworkVariable<byte>();

        /// <summary>The pools are moved four times a second, not every frame: a tick is a dozen height samples and a
        /// handful of dice, and the rules are all rates.</summary>
        const float ClimbTick = .25f;
        /// <summary>The weather is asked about this often, in seconds of the run.</summary>
        const float WeatherTick = 5f;
        /// <summary>A July morning: the snow bridges of the Garabashi glacier are at their thinnest
        /// (<see cref="AscentRoute.BridgeChance"/>).</summary>
        const int GlacierMonth = 7;
        /// <summary>Centimetres of new snow a second of storm lays down, and the most it ever adds up to.</summary>
        const float SnowPerSecondCm = .05f, MaxFreshCm = 40f;

        sealed class ClimbTask
        {
            public ClimbJob Kind;
            public double Started;
            public float Needed;
            public Vector3 From;
            /// <summary>Which way the tent will face (<see cref="ClimbJob.Pitch"/>).</summary>
            public float Yaw;
            /// <summary>Which camp is being taken down (<see cref="ClimbJob.Strike"/>).</summary>
            public int Camp;
        }

        readonly Dictionary<ulong, Climber> climbers = new Dictionary<ulong, Climber>();
        readonly Dictionary<ulong, Vector2> climbWere = new Dictionary<ulong, Vector2>();
        readonly Dictionary<ulong, Vector2> gateSafe = new Dictionary<ulong, Vector2>();
        readonly Dictionary<ulong, float> droppedAt = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, ClimbTask> climbJobs = new Dictionary<ulong, ClimbTask>();
        readonly Dictionary<ulong, bool> wasRiding = new Dictionary<ulong, bool>();
        readonly HashSet<ulong> sortieSent = new HashSet<ulong>();
        readonly List<ulong> climbGone = new List<ulong>();
        float climbDt, weatherDt, freshSnowCm;

        /// <summary>The climber the host keeps for this client, made on first sight.</summary>
        Climber ClimberOf(ulong id)
        {
            if (climbers.TryGetValue(id, out var c)) return c;
            c = new Climber();
            climbers[id] = c;
            return c;
        }

        // ── the tick ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Host tick for the mountain. Driven from <see cref="Update"/>, which already runs on the server
        /// every frame.</summary>
        void TickAscentServer(float dt)
        {
            if (!IsServer || run == null || dem == null || !Climb.On || dt <= 0f) return;
            MountainWeather(dt);
            TickClimbJobs();
            climbDt += dt;
            if (climbDt < ClimbTick) return;
            dt = climbDt; climbDt = 0f;

            float hour = Climb.Hour(run.Elapsed);
            // one flag does for both rules that care about the light: without it the sun cannot burn the eyes, and
            // with it the shelf is not the monotonous dark plod that puts people to sleep on their feet. A white-out
            // counts as dark for both, which is exactly right — there is no sun through a пурга, and an hour of
            // walking a traverse you cannot see is the same hour.
            bool light = !AscentRoute.Dark(hour);

            foreach (var kv in NetworkManager.ConnectedClients)
            {
                ulong id = kv.Key;
                var hiker = kv.Value.PlayerObject != null ? kv.Value.PlayerObject.GetComponent<HikerController>() : null;
                if (hiker == null || !run.Players.TryGetValue(Token(id), out var p)) continue;
                string token = Token(id);
                var pos = hiker.transform.position;
                var at = new Vector2(pos.x, pos.z);
                float moved = climbWere.TryGetValue(id, out var was) ? Vector2.Distance(at, was) : 0f;
                var dir = moved > .02f ? new Vector3(at.x - was.x, 0f, at.y - was.y) : hiker.transform.forward;
                climbWere[id] = at;

                var c = ClimberOf(id);
                if (p.Outcome != Outcome.None) { Sortie(id, c); continue; }

                // what is in the rucksack IS what is on him: the one place the rules meet the pack
                c.Gear = Rental.Carried(packs, token);
                if (!c.Has(Gear.Crampons)) c.CramponsOn = false;

                var point = Climb.Point(dem, pos, dir);
                var air = Climb.Air(point.Ele, pos.x, pos.z, StormNow, freshSnowCm);
                bool riding = hiker.Riding.Value;
                // stepping out of a warm cab at 4 800 or 5 100 m is the trade the snow-cat sells; the ten minutes
                // after it are the coldest of the ascent (AscentRoute.ColdShock)
                bool wasIn = wasRiding.TryGetValue(id, out var before) && before;
                if (wasIn && !riding && point.Ele > AscentRoute.CrevasseToEle) droppedAt[id] = Time.time;
                wasRiding[id] = riding;
                bool sheltered = riding || Climb.Sheltered(pos.x, pos.z, air.VisibilityM);
                bool eyesOpen = light && air.VisibilityM > AscentRoute.LostVisibilityM;
                bool onRope = AscentRoute.RopeProtects(point.Ele, point.OffRouteM, c.Gear);
                var job = climbJobs.TryGetValue(id, out var running) ? running : null;
                // the carabiners of the cow's tail cannot be worked in thick mittens, and neither can a buckle
                bool bare = onRope || (job != null && job.Kind != ClimbJob.Sip);

                // a body jiggling against a capsule collider is not walking: below the walking threshold this one is
                // standing, and every rule that cares (the heart, the recovery, the feet freezing at the stops) reads
                // it from here
                float speedMs = riding || moved / dt <= MovingSpeed ? 0f : moved / dt;
                var step = new Ascent.Step(point, air, speedMs)
                {
                    Daylight = eyesOpen,
                    Sheltered = sheltered,
                    BareHands = bare,
                };
                var report = Ascent.Tick(c, step, dt);
                ColdShock(id, c, air, sheltered, moved > 0f, dt);

                Legs(hiker, id, point, token, report, speedMs, dt);
                if (!riding && p.Outcome == Outcome.None) Dice(id, token, p, point, air, report, moved, dt);
                Gate(id, hiker, point, c.Gear);

                bool canRead = AscentRoute.CanReadTheRoute(point.Ele, point.OffRouteM, air.VisibilityM,
                    freshSnowCm, Climb.CatTrack(pos.x, pos.z));
                Publish(hiker, c, point, air, report, onRope, canRead, eyesOpen, job);
            }
            PruneClimb();
        }

        float StormNow => Mathf.Max(Weather.Storm, MountainStorm.Value ? 1f : 0f);

        /// <summary>What is left in the legs on the southern slope. <see cref="Skiing.Effort"/> is about wading snow
        /// and belongs to the other map; up here the cost is the height being gained, the load on the back and the
        /// pace being asked for, and what comes back is whatever <see cref="Ascent.RecoveryFactor"/> allows —
        /// standing still buys three times less above 5 000 m, keeping the step-and-breathe pace buys a third more,
        /// and from the nausea on nothing comes back at all. The bar itself lives where it always has,
        /// in <see cref="HikerController.Strength"/>, and the legs read it exactly as they read it in the snow.</summary>
        const float ClimbEffort = 1f / 1400f;

        void Legs(HikerController hiker, ulong id, RoutePoint point, string token, Ascent.Report report, float speedMs, float dt)
        {
            float left = strength.TryGetValue(id, out var have) ? have : 1f;
            // Ascent.Report already knows whether this one is standing: below 5 000 m walking gives a third of the
            // rate back and standing gives all of it, above 5 000 walking gives nothing, and from the nausea on
            // nothing comes back anywhere
            left += dt * Rest * report.RecoveryFactor;
            if (speedMs > 0f)
            {
                float load = packs != null ? packs.CarriedKg(token) : 0f;
                float pace = Mathf.Clamp(speedMs / Ascent.BreathPaceMs, .5f, 3f);
                left -= dt * ClimbEffort * (1f + Mathf.Max(0f, point.SlopeDeg) / 15f) * (1f + load / 45f) * pace;
            }
            left = Mathf.Clamp01(left);
            strength[id] = left;
            byte b = (byte)Mathf.RoundToInt(left * 255f);
            if (hiker.Strength.Value != b) hiker.Strength.Value = b;
        }

        /// <summary>Stepping out of a warm cab onto the windiest part of the route with a cold body: for the first ten
        /// minutes the cold works half again as hard (<see cref="AscentRoute.ColdShock"/>). The rate itself lives in
        /// <see cref="AscentCold"/>; only the extra share is added here, because <see cref="Ascent.Tick"/> has already
        /// charged the plain one.</summary>
        void ColdShock(ulong id, Climber c, MountainAir air, bool sheltered, bool moving, float dt)
        {
            if (sheltered || !droppedAt.TryGetValue(id, out var when)) return;
            float extra = AscentRoute.ColdShock(Time.time - when) - 1f;
            if (extra <= 0f) { droppedAt.Remove(id); return; }
            float feels = AscentCold.FeelsC(air.TempC, air.WindMs);
            bool faceCovered = c.Has(Gear.Balaclava) || c.Has(Gear.Mask);
            bool handsCovered = c.Has(Gear.Mittens);
            c.Face = Mathf.Clamp01(c.Face + AscentCold.FreezeRate(Limb.Face, feels, faceCovered, moving) * extra * dt);
            c.Hands = Mathf.Clamp01(c.Hands + AscentCold.FreezeRate(Limb.Hands, feels, handsCovered, moving) * extra * dt);
            c.Feet = Mathf.Clamp01(c.Feet + AscentCold.FreezeRate(Limb.Feet, feels, true, moving) * extra * dt);
        }

        // ── the dice ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Everything that is a chance rather than a rate: the feet going on the ice, a gust taking them out
        /// from under him, and a snow bridge on the Garabashi glacier. All of it is rolled here and nowhere else —
        /// the client never throws a die that can end its own run.</summary>
        void Dice(ulong id, string token, Participant p, RoutePoint point, MountainAir air, Ascent.Report report, float moved, float dt)
        {
            float crevasse = AscentRoute.CrevassePerMetre(point.Ele, point.OffRouteM, GlacierMonth, false) * moved;
            if (crevasse > 0f && Random.value < crevasse) { Crevasse(id, token); return; }

            float slip = report.SlipPerMetre * moved + AscentCold.GustSlipChance(air.WindMs) * dt;
            if (slip <= 0f || Random.value >= slip) return;

            if (Random.value < report.SelfArrest)
            {
                run.Hurt(token, 5f, "{name}: ноги ушли — задержался ледорубом.");
                SlipRpc(0f, false, RpcTarget.Single(id, RpcTargetUse.Temp));
                return;
            }
            bool fatal = report.Deadly || report.RunoutM >= 250f;
            if (fatal)
            {
                run.Fall(token, $"{{name}}: срыв на {Ascent.SurfaceTitle(point.Ele).ToLowerInvariant()}. Задержаться нечем — {Mathf.RoundToInt(report.RunoutM)} м по линии падения воды.");
                SlipRpc(report.RunoutM, true, RpcTarget.Single(id, RpcTargetUse.Temp));
                return;
            }
            run.Hurt(token, Mathf.Min(30f, report.RunoutM * .08f), $"{{name}}: срыв, катится {Mathf.RoundToInt(report.RunoutM)} м вниз по склону.");
            SlipRpc(report.RunoutM, false, RpcTarget.Single(id, RpcTargetUse.Temp));
        }

        void Crevasse(ulong id, string token)
        {
            var c = ClimberOf(id);
            c.StopSeconds = Mathf.Max(c.StopSeconds, 25f);
            run.Hurt(token, 20f, "{name} проваливается в трещину по грудь. Мост держал только на вид — вправо от колеи их четыре.");
            SlipRpc(0f, false, RpcTarget.Single(id, RpcTargetUse.Temp));
        }

        /// <summary>The host's own line at Pastukhov rocks. The owner already refuses to walk uphill without the kit
        /// (<see cref="ClimbGear.Allow"/>) and that is the mechanism; this is the backstop for somebody who ends up
        /// above the line on foot anyway.
        ///
        /// It puts them back only where it can do so without being a punishment: the safe point is remembered on the
        /// approach to the rocks, in the last two hundred metres of height below the gate, so the walk back is the few
        /// minutes that were just walked up. Anybody who was CARRIED above the gate — the snow-cat will take a
        /// passenger to 5 100 m and ask nothing — never recorded such a point and is left where the cat put them,
        /// with the whole descent in front of them and no way up. That is the honest version of the rule.</summary>
        void Gate(ulong id, HikerController hiker, RoutePoint point, Gear have)
        {
            if (hiker.Riding.Value) return;
            if (point.Ele > Ascent.GearGateEle - 200f && point.Ele < Ascent.GearGateEle - 15f)
            {
                gateSafe[id] = new Vector2(hiker.transform.position.x, hiker.transform.position.z);
                return;
            }
            if (Ascent.MayPassGate(point.Ele, have)) { gateSafe.Remove(id); return; }
            if (point.Ele <= Ascent.GearGateEle + 25f) return;
            if (!gateSafe.TryGetValue(id, out var back)) return;
            hiker.TeleportServer(back.x, back.y);
            GateRpc(RpcTarget.Single(id, RpcTargetUse.Temp));
        }

        // ── what goes on the wire ─────────────────────────────────────────────────────────────────────────

        static byte Pool(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        void Publish(HikerController hiker, Climber c, RoutePoint point, MountainAir air, Ascent.Report report,
            bool onRope, bool canRead, bool daylight, ClimbTask job)
        {
            var marks = ClimbNet.Mark.None;
            if (c.CramponsOn) marks |= ClimbNet.Mark.Crampons;
            if (report.MayRun) marks |= ClimbNet.Mark.MayRun;
            if (report.MustStop) marks |= ClimbNet.Mark.MustStop;
            if (report.Deadly) marks |= ClimbNet.Mark.Deadly;
            if (!Ascent.MayPassGate(Ascent.GearGateEle, c.Gear)) marks |= ClimbNet.Mark.Barred;
            if (daylight) marks |= ClimbNet.Mark.Daylight;
            if (onRope) marks |= ClimbNet.Mark.OnRope;
            if (!canRead) marks |= ClimbNet.Mark.Lost;

            float progress = job == null || job.Needed <= 0f ? 0f
                : Mathf.Clamp01((float)(Time.timeAsDouble - job.Started) / job.Needed);

            var net = new ClimbNet
            {
                Kit = (ushort)c.Gear,
                Marks = (byte)marks,
                Hands = Pool(c.Hands), Feet = Pool(c.Feet), Face = Pool(c.Face),
                Dry = Pool(c.Dehydration), Sleep = Pool(c.Drowsiness), Blind = Pool(c.Blindness), Pulse = Pool(c.Pulse),
                Acclim = Pool(c.Acclimatisation),
                Load = (byte)Mathf.Clamp(Mathf.RoundToInt(c.SicknessLoad * 10f), 0, 255),
                Sips = (byte)Mathf.Clamp(c.ThermosSips, 0, 255),
                Speed = (byte)Mathf.Clamp(Mathf.RoundToInt(report.SpeedFactor * 200f), 0, 255),
                Recovery = (byte)Mathf.Clamp(Mathf.RoundToInt(report.RecoveryFactor * 100f), 0, 255),
                Drift = (byte)Mathf.Clamp(Mathf.RoundToInt(report.DriftMs * 100f), 0, 255),
                Feels = (short)Mathf.Clamp(Mathf.RoundToInt(report.FeelsC), -120, 60),
                Wind = (byte)Mathf.Clamp(Mathf.RoundToInt(air.WindMs * 4f), 0, 255),
                Job = (byte)(job != null ? job.Kind : ClimbJob.None),
                Progress = (byte)Mathf.RoundToInt(progress * 255f),
            };
            if (!hiker.Climb.Value.Equals(net)) hiker.Climb.Value = net;
        }

        // ── the weather of the mountain ───────────────────────────────────────────────────────────────────

        /// <summary>Nothing before noon, then it breaks: eight per cent an hour until two, a fifth of an hour after.
        /// Once it has broken it stays broken — a front on the southern side does not blow over in a morning.</summary>
        void MountainWeather(float dt)
        {
            weatherDt += dt;
            if (weatherDt < WeatherTick) return;
            float span = weatherDt; weatherDt = 0f;
            if (!MountainStorm.Value)
            {
                float hour = Climb.Hour(run.Elapsed);
                float risk = AscentRoute.WeatherRiskPerHour(hour);
                float hours = span * AscentRoute.RunHours / Height1079.Core.World.ElbrusPlan.Profile.Seconds;
                if (risk > 0f && Random.value < risk * hours)
                {
                    MountainStorm.Value = true;
                    run.Record("Погода ломается. С седловины тянет снегом, вешки уходят в мглу.");
                }
            }
            if (MountainStorm.Value) freshSnowCm = Mathf.Min(MaxFreshCm, freshSnowCm + span * SnowPerSecondCm);
            byte cm = (byte)Mathf.Clamp(Mathf.RoundToInt(freshSnowCm * 4f), 0, 255);
            if (FreshSnow.Value != cm) FreshSnow.Value = cm;
        }

        // ── the two things a climber does with his hands ──────────────────────────────────────────────────

        /// <summary>Owner asks to put the crampons on or take them off. It takes
        /// <see cref="Ascent.CramponSeconds"/> of standing still, and that is the whole point of the belt above the
        /// rocks: you stop below the ice or you do not pass it.</summary>
        public void RequestCrampons(bool on) => CramponRpc(on);

        [Rpc(SendTo.Server)]
        void CramponRpc(bool on, RpcParams rpc = default)
        {
            ulong id = rpc.Receive.SenderClientId;
            if (!Climb.On || run == null) return;
            var hiker = HikerController.For(id);
            if (hiker == null || !run.Players.TryGetValue(Token(id), out var p) || p.Outcome != Outcome.None) return;
            var c = ClimberOf(id);
            if (on && !c.Has(Gear.Crampons)) { ClimbNoteRpc(0, RpcTarget.Single(id, RpcTargetUse.Temp)); return; }
            if (on == c.CramponsOn) return;
            if (hiker.Riding.Value) return;
            climbJobs[id] = new ClimbTask
            {
                Kind = on ? ClimbJob.CramponsOn : ClimbJob.CramponsOff,
                Started = Time.timeAsDouble,
                Needed = Ascent.CramponSeconds,
                From = hiker.transform.position,
            };
        }

        /// <summary>Owner asks for a sip of hot. A one-litre thermos opened at −25 °C is five sips and no more.</summary>
        public void RequestSip() => SipRpc();

        [Rpc(SendTo.Server)]
        void SipRpc(RpcParams rpc = default)
        {
            ulong id = rpc.Receive.SenderClientId;
            if (!Climb.On || run == null) return;
            var hiker = HikerController.For(id);
            if (hiker == null || !run.Players.TryGetValue(Token(id), out var p) || p.Outcome != Outcome.None) return;
            var c = ClimberOf(id);
            if (!c.Has(Gear.Thermos)) { ClimbNoteRpc(1, RpcTarget.Single(id, RpcTargetUse.Temp)); return; }
            if (c.ThermosSips <= 0) { ClimbNoteRpc(2, RpcTarget.Single(id, RpcTargetUse.Temp)); return; }
            climbJobs[id] = new ClimbTask
            {
                Kind = ClimbJob.Sip,
                Started = Time.timeAsDouble,
                Needed = AscentRoute.SipSeconds,
                From = hiker.transform.position,
            };
        }

        void TickClimbJobs()
        {
            if (climbJobs.Count == 0) return;
            double now = Time.timeAsDouble;
            climbGone.Clear();
            foreach (var kv in climbJobs)
            {
                var hiker = HikerController.For(kv.Key);
                string token = Token(kv.Key);
                bool ok = hiker != null && !hiker.Riding.Value
                    && run.Players.TryGetValue(token, out var p) && p.Outcome == Outcome.None
                    && Vector3.Distance(hiker.transform.position, kv.Value.From) < ClimbGear.JobStillM;
                if (!ok || now - kv.Value.Started >= kv.Value.Needed) climbGone.Add(kv.Key);
            }
            foreach (var id in climbGone)
            {
                var job = climbJobs[id];
                climbJobs.Remove(id);
                var hiker = HikerController.For(id);
                bool done = hiker != null && !hiker.Riding.Value
                    && Vector3.Distance(hiker.transform.position, job.From) < ClimbGear.JobStillM
                    && now - job.Started >= job.Needed;
                if (done) FinishClimb(id, Token(id), job);
            }
        }

        void FinishClimb(ulong id, string token, ClimbTask job)
        {
            var c = ClimberOf(id);
            string name = run.Players.TryGetValue(token, out var p) ? p.Name : "Путник";
            switch (job.Kind)
            {
                case ClimbJob.CramponsOn:
                    c.CramponsOn = true;
                    run.Record($"{name} надевает кошки.");
                    break;
                case ClimbJob.CramponsOff:
                    c.CramponsOn = false;
                    run.Record($"{name} снимает кошки.");
                    break;
                case ClimbJob.Sip:
                    if (c.ThermosSips <= 0) break;
                    c.ThermosSips--;
                    c.Drowsiness = 0f;
                    c.Dehydration = Mathf.Max(0f, c.Dehydration - .35f);
                    if (p != null)
                    {
                        p.Heat = Mathf.Min(100f, p.Heat + 7f);
                        p.Hands = Mathf.Min(100f, p.Hands + 10f);
                    }
                    run.Record($"{name} пьёт из термоса. Осталось глотков: {c.ThermosSips}.");
                    break;
                case ClimbJob.Pitch:
                    PitchDone(id, token, job, name);
                    break;
                case ClimbJob.Strike:
                    StrikeDone(id, token, job, name);
                    break;
            }
        }

        // ── acclimatisation between runs ──────────────────────────────────────────────────────────────────
        // What the body already knows about height arrives with the rest of what a client says about itself
        // (NightSession.Camp.ReportProfile): the key its saves live under, its acclimatisation and its purse. The
        // host takes all three on trust and overrides them at once when the save it loaded knows that key.

        /// <summary>The outing is over: climb high, sleep low. Whatever height this one touched is credited against a
        /// night back down on the meadow, and the answer goes home with the player.</summary>
        void Sortie(ulong id, Climber c)
        {
            if (!sortieSent.Add(id)) return;
            float next = Ascent.Acclimatise(c.Acclimatisation, new Ascent.Sortie(c.HighestEle, Elbrus.Azau.Ele));
            SortieRpc(next, c.HighestEle, RpcTarget.Single(id, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void SortieRpc(float acclim, float highest, RpcParams rpc) => ClimbGear.RememberSortie(acclim, highest);

        // ── back to the client ────────────────────────────────────────────────────────────────────────────

        /// <summary>Local: the host says the feet went. <paramref name="runoutM"/> of 0 is a slide that was arrested
        /// (or a leg through a bridge) — a knock and nothing more.</summary>
        public event System.Action<float, bool> Slipped;
        /// <summary>Local: a short Russian line about the ascent (0 — no crampons, 1 — no thermos, 2 — thermos empty,
        /// 3 — turned back at the gate).</summary>
        public event System.Action<string> ClimbNote;

        [Rpc(SendTo.SpecifiedInParams)]
        void SlipRpc(float runoutM, bool fatal, RpcParams rpc)
        {
            var me = Bootstrap.LocalHiker;
            if (me != null)
            {
                if (runoutM > 0f) me.Climbing?.BeginSlide(runoutM, fatal);
                else me.Knock(Vector3.zero, 1.1f);
            }
            Slipped?.Invoke(runoutM, fatal);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void GateRpc(RpcParams rpc) => Note("Выше скал Пастухова без снаряжения не пускают.");

        [Rpc(SendTo.SpecifiedInParams)]
        void ClimbNoteRpc(byte code, RpcParams rpc) => Note(code switch
        {
            0 => "Кошек нет — их берут в прокате внизу.",
            1 => "Термоса нет.",
            2 => "Термос пуст — глотков больше нет.",
            _ => "",
        });

        /// <summary>A short refusal for the line under the crosshair. It goes through the rucksack's own message
        /// channel, which is already the place a refusal appears and already fades by itself.</summary>
        void Note(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Backpacks.Message = text;
            Backpacks.MessageUntil = Time.time + 3.5f;
            ClimbNote?.Invoke(text);
        }

        void PruneClimb()
        {
            if (climbers.Count <= NetworkManager.ConnectedClients.Count) return;
            climbGone.Clear();
            foreach (var id in climbers.Keys) if (!NetworkManager.ConnectedClients.ContainsKey(id)) climbGone.Add(id);
            foreach (var id in climbGone)
            {
                climbers.Remove(id); climbWere.Remove(id); gateSafe.Remove(id);
                droppedAt.Remove(id); climbJobs.Remove(id); sortieSent.Remove(id);
                wasRiding.Remove(id); strength.Remove(id);
                CampsClientLeft(id);
            }
        }
    }
}
