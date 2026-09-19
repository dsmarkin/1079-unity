using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Height1079.Core;
using Height1079.Night;

namespace Height1079.Runtime
{
    /// <summary>Server-authoritative night for the whole session. The host runs <see cref="NightRun"/>; clients only receive snapshots.
    /// Shared state travels in NetworkVariables, personal state and protocol events in RPCs.</summary>
    public sealed partial class NightSession : NetworkBehaviour
    {
        public static NightSession Instance { get; private set; }

        public readonly NetworkVariable<float> Elapsed = new NetworkVariable<float>();
        public readonly NetworkVariable<bool> Storm = new NetworkVariable<bool>();
        public readonly NetworkVariable<float> FireRemaining = new NetworkVariable<float>();
        public readonly NetworkVariable<int> RoomOutcome = new NetworkVariable<int>();
        // the Menk (server brain, clients draw it)
        public readonly NetworkVariable<Vector3> MenkPos = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<float> MenkYaw = new NetworkVariable<float>();
        public readonly NetworkVariable<byte> MenkState = new NetworkVariable<byte>();
        public readonly NetworkVariable<byte> MenkBlows = new NetworkVariable<byte>();
        /// <summary>Local: raised when the Menk hit this client (direction, damage).</summary>
        public event System.Action<Vector3, float> MenkHitMe;
        MenkBrain menk;
        readonly List<MenkBrain.Seen> seen = new List<MenkBrain.Seen>();

        // Local mirror for the HUD (filled by RPCs on every client, directly on the host).
        public float Heat = 100, Hands = 100, Clarity = 100, Exposure;
        public Outcome MyOutcome = Outcome.None;
        public float KindleProgress, KindleNeeded;
        public readonly List<NightEvent> Events = new List<NightEvent>();
        public readonly List<(string name, Outcome outcome, bool online, bool you)> Party = new List<(string, Outcome, bool, bool)>();

        NightRun run;
        HeightField dem;
        double lastTick;
        int sentEvents;
        /// <summary>How many lines of the protocol a late guest is told about. The host keeps all of them.</summary>
        const int ProtocolTail = 120;
        const double TickSeconds = .25;

        public NightRun Run => run;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer) return;
            dem = TerrainBuilder.LoadDem();
            run = new NightRun(Time.timeAsDouble, (x, z) => TerrainBuilder.Height(dem, x, z), Height1079.Core.World.Scenario);
            // the forest giant belongs to the taiga of Kholat Syakhl, not to the glaciers of Elbrus
            if (!Height1079.Core.World.IsElbrus)
            {
                menk = new MenkBrain((x, z) => TerrainBuilder.Height(dem, x, z));
                menk.Say = text => run.Record(text);
                menk.Hit = (token, dir, damage) =>
                {
                    run.Strike(token, damage, damage > 40f ? "{name}: удар из темноты сбивает с ног." : "{name}: удар сквозь полотнище палатки.");
                    if (token.Length > 1 && ulong.TryParse(token.Substring(1), out var id))
                        MenkHitRpc(dir, damage, RpcTarget.Single(id, RpcTargetUse.Temp));
                };
                MenkPos.Value = menk.Pos; MenkYaw.Value = menk.Yaw;
            }
            InitPacks();
            // a save the menu picked with «Продолжить»: the clock, the weather and the tent go back before the first
            // player is placed, because CampSpawn puts everybody beside that tent
            InitCamps();
            // and the one seed the whole weather of the day comes out of (MountainDay)
            InitWeather();
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            foreach (var id in NetworkManager.ConnectedClientsIds) OnClientConnected(id);
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        /// <summary>The key a participant is filed under. It is asked for six to eight times per player per frame —
        /// the ascent tick asks twice, the work tick, the weather on the items and the pack sync ask once each — and
        /// «"c" + id» is a boxed ulong and a fresh string every time, hashed immediately afterwards. One string per
        /// client, made once.</summary>
        static string Token(ulong clientId)
        {
            if (tokens.TryGetValue(clientId, out var t)) return t;
            t = "c" + clientId;
            tokens[clientId] = t;
            return t;
        }
        static readonly Dictionary<ulong, string> tokens = new Dictionary<ulong, string>();

        void OnClientConnected(ulong clientId)
        {
            var hiker = HikerController.For(clientId);
            string name = hiker != null ? hiker.DisplayName : "Путник";
            var p = run.AddPlayer(Token(clientId), name, Time.timeAsDouble);
            // a continued run starts at the tent, not at the bottom of the ropeway
            CampSpawn(p);
            if (hiker != null) hiker.TeleportServer(p.X, p.Z);
            // Late joiners get the protocol so far — the tail of it. One reliable message per line, all in the same
            // frame, and a nine-hour ascent has hundreds of lines: that overruns the transport's send queue and the
            // guest is dropped before he has seen the mountain. Nobody reads further back than this anyway.
            PacksClientJoined(clientId);
            // The roster is sent on change now, so a guest would otherwise wait for the next join or death to learn
            // who is on the slope. Adding him changes the string anyway — this makes it not depend on that.
            sentNames = default;
            int from = Mathf.Max(0, sentEvents - ProtocolTail);
            for (int i = from; i < sentEvents; i++) EventRpc(run.Events[i].Time, run.Events[i].Text, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        void OnClientDisconnected(ulong clientId)
        {
            run.SetOffline(Token(clientId), Time.timeAsDouble);
            PacksClientLeft(clientId);
            // the profile, the rescue slip and the «has read the forecast» flag are filled straight from RPCs and
            // never wait for an ascent tick, so a client who arrived, said who he was and left again would otherwise
            // stay in those three for the rest of the session — and a reused client id would inherit him
            ForgetClient(clientId);
        }

        /// <summary>Called by the server once a player object has its name; keeps the protocol readable.</summary>
        public void RenameServer(ulong clientId, string name)
        {
            if (run != null && run.Players.TryGetValue(Token(clientId), out var p)) p.Name = name;
        }

        [Rpc(SendTo.Server)]
        public void KindleRpc(bool start, RpcParams rpc = default)
        {
            string token = Token(rpc.Receive.SenderClientId);
            if (start) run.BeginKindling(token, Time.timeAsDouble); else run.StopKindling(token);
        }

        void TickMenk()
        {
            // No Menk, no work: on Elbrus there is none, and the loop below was walking every client with a
            // GetComponent and a dictionary lookup each frame to fill a list nobody would read
            if (menk == null) return;
            seen.Clear();
            foreach (var kv in NetworkManager.ConnectedClients)
            {
                var hiker = kv.Value.PlayerObject != null ? kv.Value.PlayerObject.GetComponent<HikerController>() : null;
                if (hiker == null || !run.Players.TryGetValue(Token(kv.Key), out var p)) continue;
                bool light = hiker.TorchOn.Value && hiker.Held.Value == (byte)HeldItem.Flashlight;
                seen.Add(new MenkBrain.Seen
                {
                    Token = p.Token, Client = kv.Key, Pos = hiker.transform.position, Look = hiker.LookYaw.Value,
                    Torch = light, Dynamo = hiker.TorchKind.Value == 1, Alive = p.Online && p.Outcome == Outcome.None
                });
            }
            if (run.Outcome == Outcome.None) menk.Tick(Time.deltaTime, run.Elapsed, Weather.Storm, seen);
            // A NetworkVariable written every frame is sent on every network tick. Ten centimetres and a degree are
            // below what anybody can see on a creature in the dark at thirty metres, and the view interpolates
            // between what it is given anyway (MenkView).
            if ((MenkPos.Value - menk.Pos).sqrMagnitude > .01f) MenkPos.Value = menk.Pos;
            if (Mathf.Abs(Mathf.DeltaAngle(MenkYaw.Value, menk.Yaw)) > 1f) MenkYaw.Value = menk.Yaw;
            if (MenkState.Value != (byte)menk.Mode) MenkState.Value = (byte)menk.Mode;
            if (MenkBlows.Value != menk.TentBlows) MenkBlows.Value = menk.TentBlows;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void MenkHitRpc(Vector3 dir, float damage, RpcParams rpc)
        {
            var me = Bootstrap.LocalHiker;
            if (me != null) me.Knock(dir * (damage > 40f ? 7f : 3.5f) + Vector3.up * (damage > 40f ? 3.5f : 1.5f), damage > 40f ? 2.4f : 1.2f);
            MenkHitMe?.Invoke(dir, damage);
        }

        void Update()
        {
            if (!IsServer || run == null) return;
            if (Controls.MenkWake && menk != null) menk.WakeNow();
            // F6: a minute of the night, or a whole hour of the Elbrus clock — the ascent runs nine hours of the day
            // over an hour and a half of play, and the turn-round time is worth being able to reach in a check
            if (Controls.SkipMinute) run.SkipAhead(Climb.On ? 600 : 60);
            TickMenk();
            TickWork();
            TickAscentServer(Time.deltaTime);
            TickWeatherOnItems(Time.deltaTime);
            SyncPacks();
            double now = Time.timeAsDouble;
            if (now - lastTick < TickSeconds) return;
            lastTick = now;
            foreach (var kv in NetworkManager.ConnectedClients)
            {
                var hiker = kv.Value.PlayerObject != null ? kv.Value.PlayerObject.GetComponent<HikerController>() : null;
                if (hiker == null) continue;
                var pos = hiker.transform.position;
                run.Move(Token(kv.Key), pos.x, pos.z, now);
            }
            run.Step(now);
            Elapsed.Value = run.Elapsed;
            Storm.Value = run.Storm;
            FireRemaining.Value = run.FireRemaining(now);
            RoomOutcome.Value = (int)run.Outcome;
            for (; sentEvents < run.Events.Count; sentEvents++) EventRpc(run.Events[sentEvents].Time, run.Events[sentEvents].Text, RpcTarget.ClientsAndHost);

            // The roster used to ride along in every personal packet: half a kilobyte of names to every client four
            // times a second, and on the receiving end a Split of it into strings just as often — for a list that
            // changes when somebody joins, leaves or dies. It goes out on its own now, to everybody, and only when
            // it is not the same as the last one.
            var names = new FixedString512Bytes();
            foreach (var p in run.Players.Values)
            {
                if (names.Length > 0) names.Append('|');
                names.Append(new FixedString128Bytes($"{p.Token};{p.Name};{(int)p.Outcome};{(p.Online ? 1 : 0)}"));
            }
            if (!names.Equals(sentNames)) { sentNames = names; PartyRpc(names, RpcTarget.ClientsAndHost); }

            foreach (var kv in NetworkManager.ConnectedClients)
            {
                if (!run.Players.TryGetValue(Token(kv.Key), out var p)) continue;
                var k = run.Kindling(p.Token, now);
                PersonalRpc(p.Heat, p.Hands, p.Clarity, p.Exposure, (int)p.Outcome, k?.progress ?? 0f, k?.needed ?? 0f, RpcTarget.Single(kv.Key, RpcTargetUse.Temp));
            }
        }
        FixedString512Bytes sentNames;

        [Rpc(SendTo.SpecifiedInParams)]
        void PersonalRpc(float heat, float hands, float clarity, float exposure, int outcome, float kindleProgress, float kindleNeeded, RpcParams rpc)
        {
            Heat = heat; Hands = hands; Clarity = clarity; Exposure = exposure; MyOutcome = (Outcome)outcome;
            KindleProgress = kindleProgress; KindleNeeded = kindleNeeded;
        }

        /// <summary>Who is on the slope, sent when it changes and not four times a second. A name with a ';' or a
        /// '|' in it would break the packet, so <see cref="Bootstrap.CleanName"/> takes those out before a name ever
        /// gets here — and a field that still fails to parse is skipped rather than throwing every tick.</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        void PartyRpc(FixedString512Bytes party, RpcParams rpc)
        {
            Party.Clear();
            string me = Token(NetworkManager.LocalClientId);
            foreach (var entry in party.ToString().Split('|'))
            {
                if (entry.Length == 0) continue;
                var f = entry.Split(';');
                if (f.Length < 4) continue;
                if (!int.TryParse(f[2], out int outcome)) continue;
                Party.Add((f[1], (Outcome)outcome, f[3] == "1", f[0] == me));
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void EventRpc(float time, FixedString512Bytes text, RpcParams rpc)
        {
            Events.Add(new NightEvent(time, text.ToString()));
        }
    }
}
