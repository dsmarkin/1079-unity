using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Height1079.Core;

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
        const double TickSeconds = .25;

        public NightRun Run => run;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer) return;
            dem = TerrainBuilder.LoadDem();
            run = new NightRun(Time.timeAsDouble, (x, z) => TerrainBuilder.Height(dem, x, z));
            menk = new MenkBrain((x, z) => TerrainBuilder.Height(dem, x, z));
            menk.Say = text => run.Record(text);
            menk.Hit = (token, dir, damage) =>
            {
                run.Strike(token, damage, damage > 40f ? "{name}: удар из темноты сбивает с ног." : "{name}: удар сквозь полотнище палатки.");
                if (token.Length > 1 && ulong.TryParse(token.Substring(1), out var id))
                    MenkHitRpc(dir, damage, RpcTarget.Single(id, RpcTargetUse.Temp));
            };
            MenkPos.Value = menk.Pos; MenkYaw.Value = menk.Yaw;
            InitPacks();
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

        static string Token(ulong clientId) => "c" + clientId;

        void OnClientConnected(ulong clientId)
        {
            var hiker = HikerController.For(clientId);
            string name = hiker != null ? hiker.DisplayName : "Путник";
            var p = run.AddPlayer(Token(clientId), name, Time.timeAsDouble);
            if (hiker != null) hiker.TeleportServer(p.X, p.Z);
            // Late joiners get the protocol so far.
            PacksClientJoined(clientId);
            for (int i = 0; i < sentEvents; i++) EventRpc(run.Events[i].Time, run.Events[i].Text, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        void OnClientDisconnected(ulong clientId)
        {
            run.SetOffline(Token(clientId), Time.timeAsDouble);
            PacksClientLeft(clientId);
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
            MenkPos.Value = menk.Pos;
            MenkYaw.Value = menk.Yaw;
            MenkState.Value = (byte)menk.Mode;
            MenkBlows.Value = menk.TentBlows;
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
            if (Controls.MenkWake) menk.WakeNow();
            if (Controls.SkipMinute) run.SkipAhead(60);
            TickMenk();
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

            var names = new FixedString512Bytes();
            foreach (var p in run.Players.Values)
            {
                if (names.Length > 0) names.Append('|');
                names.Append(new FixedString128Bytes($"{p.Token};{p.Name};{(int)p.Outcome};{(p.Online ? 1 : 0)}"));
            }
            foreach (var kv in NetworkManager.ConnectedClients)
            {
                if (!run.Players.TryGetValue(Token(kv.Key), out var p)) continue;
                var k = run.Kindling(p.Token, now);
                PersonalRpc(p.Heat, p.Hands, p.Clarity, p.Exposure, (int)p.Outcome, k?.progress ?? 0f, k?.needed ?? 0f, names, RpcTarget.Single(kv.Key, RpcTargetUse.Temp));
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void PersonalRpc(float heat, float hands, float clarity, float exposure, int outcome, float kindleProgress, float kindleNeeded, FixedString512Bytes party, RpcParams rpc)
        {
            Heat = heat; Hands = hands; Clarity = clarity; Exposure = exposure; MyOutcome = (Outcome)outcome;
            KindleProgress = kindleProgress; KindleNeeded = kindleNeeded;
            Party.Clear();
            string me = Token(NetworkManager.LocalClientId);
            foreach (var entry in party.ToString().Split('|'))
            {
                if (entry.Length == 0) continue;
                var f = entry.Split(';');
                if (f.Length < 4) continue;
                Party.Add((f[1], (Outcome)int.Parse(f[2]), f[3] == "1", f[0] == me));
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void EventRpc(float time, FixedString512Bytes text, RpcParams rpc)
        {
            Events.Add(new NightEvent(time, text.ToString()));
        }
    }
}
