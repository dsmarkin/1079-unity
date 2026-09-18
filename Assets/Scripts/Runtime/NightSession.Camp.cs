using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>A pitched camp as it travels: who put it there, where it stands and whether there is a burner in it.</summary>
    public struct CampNet : INetworkSerializable, IEquatable<CampNet>
    {
        /// <summary>The owner of a camp that came out of a save: nobody alive pitched it.</summary>
        public const ulong NoOwner = ulong.MaxValue;
        /// <summary>1 — the party has something to melt snow with (<see cref="Camp.Sleep"/> reads it).</summary>
        public const byte Burner = 1;

        public int Id;
        public ulong Owner;
        public Vector3 Pos;
        public float Yaw;
        public byte Flags;

        public bool HasBurner => (Flags & Burner) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref Id); s.SerializeValue(ref Owner); s.SerializeValue(ref Pos); s.SerializeValue(ref Yaw); s.SerializeValue(ref Flags); }

        public bool Equals(CampNet o) => Id == o.Id && Owner == o.Owner && Pos == o.Pos && Yaw == o.Yaw && Flags == o.Flags;
        public override int GetHashCode() => Id;
    }

    /// <summary>Camps and saving, on the host.
    ///
    /// The real ascent from Гара-Баши is eight to eleven hours and the game means it, so it has to be playable in
    /// pieces. A player puts up the двойка he carries, crawls into it, and the run is written to a file beside his
    /// player data; next time «Продолжить» in the menu puts the party back at that tent at that hour of the morning
    /// with the bodies and the rucksacks they had.
    ///
    /// The division is the one the rest of the game keeps, only more so, because a save is the one thing two players
    /// must never disagree about:
    /// <list type="bullet">
    /// <item><b>The host owns the save.</b> It is written on the host's machine, out of the host's own
    /// <see cref="Climber"/> pools and <see cref="PackWorld"/>, and only the host loads it. A client asking to sleep
    /// is asking the host to write, exactly as it asks the host for everything else.</item>
    /// <item><b>State is per player, not per room.</b> Two people who walked up the same slope have different
    /// acclimatisation, different frostbite and different rucksacks. Each is stored under his own key — the name he
    /// typed in the menu, or a stable per-install id when he typed none (<see cref="SaveKeys"/>) — and a returning
    /// player is matched by that key. A friend who was never in this save simply joins fresh, at the camp.</item>
    /// <item><b>The mountain is not saved.</b> It is generated from the data in the repository and is the same every
    /// time. What is saved is the players, the clock and the weather, and nothing else.</item>
    /// </list>
    ///
    /// Silent off the southern slope: the night of 1–2 February is one night, <see cref="Camp.AllowedIn"/> says so,
    /// and every entry point here returns on its first line when <see cref="Climb.On"/> is false.</summary>
    public sealed partial class NightSession
    {
        /// <summary>Every camp standing on the mountain. Clients draw them (<see cref="CampView"/>) and measure their
        /// own distance to them; the host decides what may be put up and what may be taken down.</summary>
        public readonly NetworkList<CampNet> CampList = new NetworkList<CampNet>();

        /// <summary>Local: the host has written a save (or failed to), with the place it names.</summary>
        public event Action<bool, string> Saved;

        /// <summary>What a client told the host about itself: the key its saves live under and the money in its purse.
        /// Taken on trust, the way a co-op game takes everything else a player says about himself.</summary>
        sealed class ClientProfile
        {
            public string Key = "";
            public int Roubles = Wallet.StartRoubles;
        }

        readonly Dictionary<ulong, ClientProfile> profiles = new Dictionary<ulong, ClientProfile>();
        /// <summary>Keys, not client ids: a player who drops out and comes back mid-session must not have his
        /// rucksack rolled back to what the save said an hour ago.</summary>
        readonly HashSet<string> restored = new HashSet<string>();
        /// <summary>«camp:client» — one night per camp per body. The file may be written as often as anyone likes;
        /// the acclimatisation is credited once, or a player could farm it by pressing the key twice.</summary>
        readonly HashSet<string> sleptIn = new HashSet<string>();

        /// <summary>The save this session was started from, kept so that a player who is not online right now is not
        /// dropped out of the file when somebody else saves over it.</summary>
        SaveGame loaded;
        int nextCamp = 1;

        /// <summary>Places that already have a levelled platform: a tent may go up on the ice beside one of them.</summary>
        static readonly string[] Platforms =
            { "azau", "krugozor", "mir", "garabashi", "barrels", "redfox", "leaprus", "garabashiHut", "priut11", "priut88" };

        // ── starting up ───────────────────────────────────────────────────────────────────────────────────

        /// <summary>Server, once the run exists: take whatever the menu handed over and put the mountain back the way
        /// it was left. Called from <see cref="OnNetworkSpawn"/>.</summary>
        void InitCamps()
        {
            var pending = Saves.TakePending();
            if (pending == null || !Climb.On || pending.Place != Height1079.Core.World.Current) return;
            loaded = pending;
            // the clock is the only thing the run keeps about the time of day, so putting it back is putting the
            // morning back: 08:40 of a save is 08:40 of the continuation (AscentRoute.HourAt)
            run.SkipAhead(pending.Elapsed);
            MountainStorm.Value = pending.Storm;
            freshSnowCm = Mathf.Max(0f, pending.FreshSnowCm);
            // a night taken in a hut leaves no двойка on the snow: putting one back would stand it inside the wall
            if (pending.Tent)
                AddCamp(CampNet.NoOwner, new Vector3(pending.CampX, pending.CampY, pending.CampZ), pending.CampYaw, pending.Burner);
            run.Record($"Лагерь стоит: {pending.Where}. Продолжаем с {AscentRoute.Clock(pending.Hour)}.");
        }

        /// <summary>Server: a participant of a loaded run wakes up beside the tent and not down on the Azau meadow.
        /// Called from <see cref="OnClientConnected"/> before the hiker is placed.</summary>
        void CampSpawn(Participant p)
        {
            // the camp of the save this session is living in — which is the one it was started from until somebody
            // spends a night somewhere else, and that camp from then on
            if (loaded == null || p == null) return;
            // the same spread the spawn ring uses, so two people never appear inside one another
            float a = run.Players.Count * 2.1f;
            float r = Height1079.Core.World.ElbrusPlan.SpawnRadius + 1.6f;
            p.X = loaded.CampX + Mathf.Cos(a) * r;
            p.Z = loaded.CampZ + Mathf.Sin(a) * r;
        }

        // ── the ground under a tent ───────────────────────────────────────────────────────────────────────

        /// <summary>Everything <see cref="Camp.Check"/> asks about a spot, measured off the same height field, route
        /// line and weather the rest of the ascent is measured off.</summary>
        CampSpot SpotAt(float x, float z)
        {
            float ele = dem != null ? dem.Sample(x, z) : 0f;
            float slope = dem != null ? dem.Fall(x, z, 8f).slopeDeg : 0f;
            var (_, off) = Elbrus.Nearest(Elbrus.SummitRoute, x, z);
            float wind = Climb.Air(ele, x, z, StormNow, freshSnowCm).WindMs;
            return new CampSpot(ele, slope, off, PlatformDistance(x, z), wind, GlacierMonth);
        }

        /// <summary>Metres to the nearest made platform: a station, a barrel, a hut of the moraine, or the emergency
        /// box on the saddle.</summary>
        static float PlatformDistance(float x, float z)
        {
            float best = float.MaxValue;
            foreach (var id in Platforms)
            {
                var p = Elbrus.Get(id);
                best = Mathf.Min(best, Elbrus.Distance(x, z, p.X, p.Z));
            }
            var hut = Climb.SaddleHut;
            return Mathf.Min(best, Elbrus.Distance(x, z, hut.x, hut.y));
        }

        // ── the list ──────────────────────────────────────────────────────────────────────────────────────

        int AddCamp(ulong owner, Vector3 pos, float yaw, bool burner)
        {
            var camp = new CampNet
            {
                Id = nextCamp++,
                Owner = owner,
                Pos = pos,
                Yaw = yaw,
                Flags = (byte)(burner ? CampNet.Burner : 0),
            };
            CampList.Add(camp);
            return camp.Id;
        }

        bool FindCamp(int id, out CampNet camp, out int index)
        {
            for (int i = 0; i < CampList.Count; i++)
                if (CampList[i].Id == id) { camp = CampList[i]; index = i; return true; }
            camp = default; index = -1;
            return false;
        }

        int NearestCampId(Vector3 pos, out float metres)
        {
            int best = 0;
            metres = float.MaxValue;
            for (int i = 0; i < CampList.Count; i++)
            {
                var c = CampList[i];
                float d = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(c.Pos.x, c.Pos.z));
                if (d < metres) { metres = d; best = c.Id; }
            }
            return best;
        }

        // ── pitching and striking ─────────────────────────────────────────────────────────────────────────

        /// <summary>Owner asks to put the tent up here, or to take down the one he is standing at. Both take time
        /// standing still, and the host decides — the same contract the crampons and the thermos work under.</summary>
        public void RequestCamp() => CampRpc();

        [Rpc(SendTo.Server)]
        void CampRpc(RpcParams rpc = default)
        {
            ulong id = rpc.Receive.SenderClientId;
            if (!Climb.On || run == null || packs == null) return;
            var hiker = HikerController.For(id);
            if (hiker == null || hiker.Riding.Value) return;
            string token = Token(id);
            if (!run.Players.TryGetValue(token, out var p) || p.Outcome != Outcome.None) return;
            if (climbJobs.ContainsKey(id)) return;

            var pos = hiker.transform.position;
            int near = NearestCampId(pos, out float metres);
            if (near != 0 && metres <= Camp.SleepReachM)
            {
                climbJobs[id] = new ClimbTask
                {
                    Kind = ClimbJob.Strike, Started = Time.timeAsDouble,
                    Needed = Camp.StrikeSeconds, From = pos, Camp = near,
                };
                return;
            }

            var spot = SpotAt(pos.x, pos.z);
            var verdict = Camp.Check(spot, packs.Has(token, ItemId.Tent), packs.Has(token, ItemId.IceAxe),
                metres, Height1079.Core.World.Current);
            if (verdict != CampVerdict.Ok)
            {
                CampNoteRpc(Say(Camp.Why(verdict)), RpcTarget.Single(id, RpcTargetUse.Temp));
                return;
            }
            climbJobs[id] = new ClimbTask
            {
                Kind = ClimbJob.Pitch, Started = Time.timeAsDouble,
                Needed = Camp.SecondsToPitch(spot), From = pos, Yaw = hiker.LookYaw.Value,
            };
        }

        /// <summary>The tent is up: it leaves the rucksack and becomes a place on the mountain.</summary>
        void PitchDone(ulong id, string token, ClimbTask job, string name)
        {
            if (!packs.Spend(token, ItemId.Tent))
            {
                CampNoteRpc(Say(Camp.Why(CampVerdict.NoTent)), RpcTarget.Single(id, RpcTargetUse.Temp));
                return;
            }
            var at = new Vector3(job.From.x, Ground(job.From.x, job.From.z), job.From.z);
            AddCamp(id, at, job.Yaw, packs.Has(token, ItemId.Burner));
            SyncPacks();
            float ele = dem != null ? dem.Sample(at.x, at.z) : at.y;
            run.Record($"{name} ставит палатку: {Camp.Where(at.x, at.z, ele)}.");
        }

        /// <summary>And down again, back into the rucksack of whoever pulled the pegs.</summary>
        void StrikeDone(ulong id, string token, ClimbTask job, string name)
        {
            if (!FindCamp(job.Camp, out _, out int index)) return;
            CampList.RemoveAt(index);
            packs.Receive(token, new ItemStack(ItemId.Tent));
            SyncPacks();
            run.Record($"{name} сворачивает лагерь.");
        }

        // ── the night, and the file ───────────────────────────────────────────────────────────────────────

        /// <summary>Owner asks to spend the night in the camp beside him: the sortie is closed, the body digests what
        /// it climbed today, and the run goes to disk.</summary>
        public void RequestSleep() => SleepCampRpc();

        [Rpc(SendTo.Server)]
        void SleepCampRpc(RpcParams rpc = default)
        {
            ulong id = rpc.Receive.SenderClientId;
            if (!Climb.On || run == null || packs == null) return;
            var hiker = HikerController.For(id);
            if (hiker == null || hiker.Riding.Value) return;
            if (!run.Players.TryGetValue(Token(id), out var p) || p.Outcome != Outcome.None) return;

            int camp = NearestCampId(hiker.transform.position, out float metres);
            if (camp == 0 || metres > Camp.SleepReachM)
            {
                CampNoteRpc(Say("Рядом нет лагеря. Поставьте палатку (B / 7)."), RpcTarget.Single(id, RpcTargetUse.Temp));
                return;
            }
            SaveHere(camp);
        }

        /// <summary>Writes the night in the camp the player is standing at.</summary>
        void SaveHere(int campId)
        {
            if (!FindCamp(campId, out var camp, out _)) return;
            float ele = dem != null ? dem.Sample(camp.Pos.x, camp.Pos.z) : camp.Pos.y;
            SaveNight(camp.Pos, camp.Yaw, ele, camp.HasBurner, true, "camp:" + campId,
                Camp.Where(camp.Pos.x, camp.Pos.z, ele), null);
        }

        /// <summary>Writes a night, in a двойка on the snow or in a bunk under somebody else's roof. Everybody who is
        /// online is credited with it — once per place per body, because the file may be written as often as anyone
        /// likes and a body sleeps once — and then the whole party, including anybody who was in the save we started
        /// from and is not here tonight, goes into one file.
        ///
        /// <paramref name="night"/> is what a roof adds on top of the night itself: it is called instead of
        /// <see cref="Camp.Sleep"/>, never as well, because climb-high-sleep-low is one rule and
        /// <see cref="Lodging.Sleep"/> already calls it (docs/ELBRUS.md).</summary>
        void SaveNight(Vector3 at, float yaw, float ele, bool burner, bool tent, string place, string where,
            Action<ulong, Climber> night)
        {
            var save = new SaveGame
            {
                Place = Height1079.Core.World.Current,
                SavedUtc = DateTime.UtcNow,
                Elapsed = run.Elapsed,
                Storm = MountainStorm.Value,
                FreshSnowCm = freshSnowCm,
                Seed = WeatherSeed.Value,
                Date = Forecast.DateOf(WeatherDay.Value),
                CampX = at.x, CampY = at.y, CampZ = at.z, CampYaw = yaw, CampEle = ele,
                Burner = burner,
                Tent = tent,
                Where = where,
            };

            bool slept = false;
            foreach (var kv in NetworkManager.ConnectedClients)
            {
                ulong id = kv.Key;
                string token = Token(id);
                if (!run.Players.TryGetValue(token, out var p)) continue;
                var c = ClimberOf(id);

                bool firstNight = sleptIn.Add(place + ":" + id);
                if (firstNight)
                {
                    slept = true;
                    // the high point of the day that is ending, read BEFORE the night resets the sortie: it is the
                    // half of «climb high, sleep low» the programme cannot see afterwards (Programme.Slept)
                    float dayHigh = c.HighestEle;
                    if (night != null) night(id, c); else Camp.Sleep(c, ele, burner);
                    PlanSlept(id, ele, dayHigh);
                    // a body that was carried to 5 100 by a snow-cat yesterday is not carried there today
                    carriedTo.Remove(id);
                }

                var profile = ProfileOf(id);
                var entry = save.Ensure(profile.Key, p.Name);
                entry.Acclim = c.Acclimatisation;
                entry.Highest = c.HighestEle;
                entry.Sickness = c.SicknessLoad;
                entry.Hands = c.Hands; entry.Feet = c.Feet; entry.Face = c.Face;
                entry.Dry = c.Dehydration; entry.Sleep = c.Drowsiness; entry.Blind = c.Blindness; entry.Pulse = c.Pulse;
                entry.Sips = c.ThermosSips;
                entry.Crampons = c.CramponsOn;
                entry.Heat = p.Heat; entry.HandsBar = p.Hands; entry.Clarity = p.Clarity;
                entry.Strength = strength.TryGetValue(id, out var left) ? left : 1f;
                entry.Roubles = profile.Roubles;
                entry.Reg = RescueOf(id);
                entry.Programme = PlanOf(id);
                entry.Pack.Clear();
                var pack = packs.Worn(token);
                if (pack != null) foreach (var s in pack.Contents) entry.Pack.Add(s);
                entry.Hand = packs.Hand(token);

                if (firstNight) NightRpc(c.Acclimatisation, c.HighestEle, RpcTarget.Single(id, RpcTargetUse.Temp));
            }

            // a friend who played this save yesterday and is not here tonight keeps his line in the file
            if (loaded != null)
                foreach (var old in loaded.Climbers)
                    if (save.Find(old.Key) == null) save.Climbers.Add(old);

            // a night that was actually slept moves the mountain on to the next day, which is what makes the board's
            // «завтра» worth reading. A second night in the same place credits nothing and moves nothing, so the date
            // cannot be walked forward by pressing the key twice.
            if (slept && Climb.On)
            {
                WeatherDay.Value += 1;
                save.Date = Forecast.DateOf(WeatherDay.Value);
                freshSnowCm = 0f;
                weatherDt = WeatherTick;
                // and the day starts in the morning, not at the hour the party went to bed: the run clock is the
                // only time of day this game has (NightRun.NewMorning), and the save keeps it, so both go back
                run.NewMorning();
                save.Elapsed = 0f;
                Elapsed.Value = 0f;
                run.Record($"Утро {save.Date:dd.MM}, {AscentRoute.Clock(AscentRoute.RunStartHour)}. {MountainDay.Verdict}");
            }

            string path = Saves.Write(save);
            loaded = save;
            run.Record(path != null
                ? $"Ночёвка: {save.Where}, {AscentRoute.Clock(save.Hour)}. Сохранено."
                : "Сохранить не удалось — проверьте место на диске.");
            SavedRpc(path != null, Say(save.Where), RpcTarget.ClientsAndHost);
        }

        // ── who is who ────────────────────────────────────────────────────────────────────────────────────

        ClientProfile ProfileOf(ulong id)
        {
            if (profiles.TryGetValue(id, out var p)) return p;
            p = new ClientProfile { Key = Token(id) };
            profiles[id] = p;
            return p;
        }

        /// <summary>Owner tells the host who it is: the key its saves live under, what its body already knows about
        /// height, and what is left in its purse. All three are the client's own business between runs, so the host
        /// takes them on trust — and overrides them at once when this save knows this key.</summary>
        public void ReportProfile(string key, float acclim, int roubles)
            => ProfileRpc(Short(key), Mathf.Clamp01(acclim), Mathf.Max(0, roubles));

        [Rpc(SendTo.Server)]
        void ProfileRpc(FixedString64Bytes key, float acclim, int roubles, RpcParams rpc = default)
        {
            ulong id = rpc.Receive.SenderClientId;
            if (!Climb.On) return;
            string k = key.ToString();
            if (k.Length == 0) k = Token(id);
            var profile = ProfileOf(id);
            profile.Key = k;
            profile.Roubles = roubles;

            var saved = loaded != null ? loaded.Find(k) : null;
            if (saved != null && restored.Add(k)) Restore(id, saved);
            else ClimberOf(id).Acclimatisation = Mathf.Clamp01(acclim);
        }

        /// <summary>This player was in the save: give him back the body and the rucksack he stopped with.</summary>
        void Restore(ulong id, SaveClimber saved)
        {
            string token = Token(id);
            var c = ClimberOf(id);
            c.Acclimatisation = saved.Acclim;
            c.HighestEle = saved.Highest;
            c.SicknessLoad = saved.Sickness;
            c.Hands = saved.Hands; c.Feet = saved.Feet; c.Face = saved.Face;
            c.Dehydration = saved.Dry; c.Drowsiness = saved.Sleep; c.Blindness = saved.Blind; c.Pulse = saved.Pulse;
            c.ThermosSips = saved.Sips;
            // the slip left at the ЭВПСО counter is a piece of paper: it outlives the run that filed it
            RescueRestore(id, saved.Reg);
            // and so does the programme: the ticks and the night it remembers come back with the body
            PlanRestore(id, saved.Programme);
            // the kit itself is counted off the rucksack on the next tick (Rental.Carried), and the crampons come off
            // the boots there too if the rucksack turns out not to have any
            c.CramponsOn = saved.Crampons;

            if (packs != null)
            {
                packs.Refill(token, saved.Pack);
                if (!saved.Hand.IsEmpty) packs.GiveHand(token, saved.Hand);
                // a file written before the programme was a piece of paper must not leave its owner without one: it
                // weighs twenty grams, it is issued with the rucksack, and without it he cannot read the programme he
                // is already halfway through (Programmes)
                if (Climb.On && !packs.Has(token, ItemId.ProgrammeSheet))
                    packs.Receive(token, new ItemStack(ItemId.ProgrammeSheet));
                SyncPacks();
            }
            if (run.Players.TryGetValue(token, out var p))
            {
                p.Heat = saved.Heat; p.Hands = saved.HandsBar; p.Clarity = saved.Clarity;
            }
            strength[id] = Mathf.Clamp01(saved.Strength);

            RestoredRpc(saved.Acclim, saved.Highest, saved.Roubles, RpcTarget.Single(id, RpcTargetUse.Temp));
        }

        void CampsClientLeft(ulong clientId)
        {
            profiles.Remove(clientId);
            RescueClientLeft(clientId);
        }

        // ── back to the client ────────────────────────────────────────────────────────────────────────────

        /// <summary>A line for the client, never longer than the field it travels in. Russian is two bytes a letter,
        /// so the byte count is what has to be cut by, not the character count.</summary>
        static FixedString512Bytes Say(string text)
        {
            var s = new FixedString512Bytes();
            s.Append(Cut(text, 500));
            return s;
        }

        /// <summary>A key, same rule: <c>FixedString64Bytes</c> holds sixty-one bytes.</summary>
        static FixedString64Bytes Short(string text)
        {
            var s = new FixedString64Bytes();
            s.Append(Cut(text, 60));
            return s;
        }

        static string Cut(string text, int bytes)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var cut = text;
            while (cut.Length > 0 && System.Text.Encoding.UTF8.GetByteCount(cut) > bytes)
                cut = cut.Substring(0, cut.Length - 1);
            return cut;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void CampNoteRpc(FixedString512Bytes text, RpcParams rpc) => Note(text.ToString());

        [Rpc(SendTo.SpecifiedInParams)]
        void SavedRpc(bool ok, FixedString512Bytes place, RpcParams rpc)
        {
            string text = place.ToString();
            Note(ok ? "Ночёвка записана: " + text : "Сохранить не удалось");
            Saved?.Invoke(ok, text);
        }

        /// <summary>The night is over on the host's books: the acclimatisation goes home with the player, so that his
        /// next run — in this save or in a fresh one — starts with the body he actually earned.</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        void NightRpc(float acclim, float highest, RpcParams rpc) => ClimbGear.RememberSortie(acclim, highest);

        [Rpc(SendTo.SpecifiedInParams)]
        void RestoredRpc(float acclim, float highest, int roubles, RpcParams rpc)
        {
            ClimbGear.RememberSortie(acclim, highest);
            CafeService.Purse.Roubles = Mathf.Max(0, roubles);
            Note("Продолжаем с привала.");
        }
    }
}
