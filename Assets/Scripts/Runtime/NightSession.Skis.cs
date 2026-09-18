using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Skis on the host's side: who is travelling how, and what it is costing them.
    ///
    /// Changing over is an ask, not a fact — a client presses a key, the host decides and writes the answer into
    /// <see cref="HikerController.Mode"/>, which everyone reads to draw the gear and to work out their own pace. The host also
    /// keeps the strength of every participant: <see cref="Skiing.Effort"/> is a share of a full bar per second, and the
    /// numbers are scaled so that breaking trail on foot through the valley's 1.2 m with a full pack empties a fresh man in
    /// about four minutes, which is what the diary of 31 January describes — "Первый сбрасывает рюкзак и торит тропу 5 минут,
    /// отдыхает минут 10—15". Following a made лыжня costs a fortieth of that per metre, which is the whole point of making one.
    ///
    /// The snow itself is read from <see cref="SnowCover"/> and from the host's own <see cref="SkiTrailFx.Track"/>, so the host
    /// and the client agree about the ground without a single extra byte on the wire.</summary>
    public sealed partial class NightSession
    {
        /// <summary>Standing still gets it back: a full bar in two and a half minutes on the slope, in three quarters of a
        /// minute by the fire. Both are shares per second.</summary>
        const float Rest = 1f / 150f, RestByFire = 1f / 45f;
        /// <summary>Above this a hiker counts as moving and is charged for it, m/s.</summary>
        const float MovingSpeed = .35f;
        /// <summary>The snow is read this often, not every frame: <see cref="SnowCover"/> is a dozen height samples a look.</summary>
        const float SkiTick = .2f;

        readonly Dictionary<ulong, float> strength = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, Vector2> skiWere = new Dictionary<ulong, Vector2>();
        readonly List<ulong> skiGone = new List<ulong>();
        float skiDt;

        /// <summary>Owner asks the host to change over. The host may refuse without saying so — the mode simply does not change.</summary>
        public void RequestTravel(Travel mode) => TravelRpc((byte)mode);

        [Rpc(SendTo.Server)]
        void TravelRpc(byte mode, RpcParams rpc = default)
        {
            if (Height1079.Core.World.IsElbrus || run == null) return;
            ulong sender = rpc.Receive.SenderClientId;
            var hiker = HikerController.For(sender);
            if (hiker == null) return;
            if (mode > (byte)Travel.Hauling) return;
            var want = (Travel)mode;
            string token = Token(sender);
            if (!run.Players.TryGetValue(token, out var p) || p.Outcome != Outcome.None) return;
            if (p.Mode == want) return;
            p.Mode = want;
            hiker.Mode.Value = mode;
            run.Record($"{p.Name} {Changing(want)}.");
        }

        static string Changing(Travel mode) => mode switch
        {
            Travel.Skis => "встаёт на лыжи",
            Travel.Hauling => "впрягается в волокушу",
            Travel.Poles => "снимает лыжи и берёт палки",
            _ => "снимает лыжи",
        };

        /// <summary>Host tick for the snow: charges every participant for the way they are getting through it and publishes what
        /// is left of them. Driven from <see cref="SkiTrailFx"/>, which is the one thing on the host that already runs every
        /// frame for the track and knows how packed the snow is.</summary>
        public void TickSkisServer(float dt)
        {
            if (!IsServer || run == null || dem == null || Height1079.Core.World.IsElbrus || dt <= 0f) return;
            skiDt += dt;
            if (skiDt < SkiTick) return;
            dt = skiDt; skiDt = 0f;
            foreach (var kv in NetworkManager.ConnectedClients)
            {
                ulong id = kv.Key;
                var hiker = kv.Value.PlayerObject != null ? kv.Value.PlayerObject.GetComponent<HikerController>() : null;
                if (hiker == null || !run.Players.TryGetValue(Token(id), out var p)) continue;
                var pos = hiker.transform.position;
                var here = new Vector2(pos.x, pos.z);
                float moved = skiWere.TryGetValue(id, out var was) ? Vector2.Distance(here, was) : 0f;
                skiWere[id] = here;
                p.Mode = ModeOf(hiker);
                if (p.Outcome != Outcome.None) continue;

                float depth = SnowCover.Depth(dem, pos.x, pos.z);
                float crust = SnowCover.Crust(dem, pos.x, pos.z);
                // the snow this one is walking INTO: under the boot it is always trodden, by this very boot
                float packed = SkiTrailFx.Instance != null ? SkiTrailFx.Instance.PackedAhead(pos, hiker.transform.forward) : 0f;
                float load = packs != null ? packs.CarriedKg(Token(id)) : 0f;
                float slope = SlopeAhead(pos, hiker.transform.forward);
                bool walking = moved / dt > MovingSpeed;
                bool fire = WorldData.NearCamp(pos.x, pos.z) && FireRemaining.Value > 0f;

                // the cold rules read how deep this one wades (wet clothes lose heat), so keep it on the participant
                p.Sink = Skiing.Sink(p.Mode, depth, crust, packed, load);

                float left = strength.TryGetValue(id, out var have) ? have : 1f;
                left += dt * (walking ? -Skiing.Effort(p.Mode, depth, crust, packed, slope, load) : fire ? RestByFire : Rest);
                left = Mathf.Clamp01(left);
                strength[id] = left;
                byte b = (byte)Mathf.RoundToInt(left * 255f);
                if (hiker.Strength.Value != b) hiker.Strength.Value = b;
            }
            Prune();
        }

        static Travel ModeOf(HikerController hiker)
        {
            byte v = hiker.Mode.Value;
            return v <= (byte)Travel.Hauling ? (Travel)v : Travel.Foot;
        }

        /// <summary>Slope three metres ahead along the way this hiker is facing, degrees, positive climbing — the sign
        /// <see cref="Skiing.Speed"/> and <see cref="Skiing.Effort"/> both read.</summary>
        float SlopeAhead(Vector3 pos, Vector3 dir)
        {
            var f = new Vector2(dir.x, dir.z);
            if (f.sqrMagnitude < 1e-4f) return 0f;
            f.Normalize();
            const float span = 3f;
            float here = TerrainBuilder.Height(dem, pos.x, pos.z);
            float ahead = TerrainBuilder.Height(dem, pos.x + f.x * span, pos.z + f.y * span);
            return Mathf.Atan2(ahead - here, span) * Mathf.Rad2Deg;
        }

        void Prune()
        {
            if (strength.Count <= NetworkManager.ConnectedClients.Count) return;
            skiGone.Clear();
            foreach (var id in strength.Keys) if (!NetworkManager.ConnectedClients.ContainsKey(id)) skiGone.Add(id);
            foreach (var id in skiGone) { strength.Remove(id); skiWere.Remove(id); }
        }
    }
}
