using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    public struct PackNet : INetworkSerializable, IEquatable<PackNet>
    {
        public const ulong NoWearer = ulong.MaxValue;
        public int Id;
        public ulong Wearer;
        public Vector3 Pos;
        public float Yaw;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter { s.SerializeValue(ref Id); s.SerializeValue(ref Wearer); s.SerializeValue(ref Pos); s.SerializeValue(ref Yaw); }
        public bool Equals(PackNet o) => Id == o.Id && Wearer == o.Wearer && Pos == o.Pos && Yaw == o.Yaw;
        public override int GetHashCode() => Id;
    }

    public struct PackItemNet : INetworkSerializable, IEquatable<PackItemNet>
    {
        public int Pack;
        public byte Item;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter { s.SerializeValue(ref Pack); s.SerializeValue(ref Item); }
        public bool Equals(PackItemNet o) => Pack == o.Pack && Item == o.Item;
        public override int GetHashCode() => Pack * 31 + Item;
    }

    public struct LooseNet : INetworkSerializable, IEquatable<LooseNet>
    {
        public int Id;
        public byte Item;
        public Vector3 Pos;
        public float Yaw;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter { s.SerializeValue(ref Id); s.SerializeValue(ref Item); s.SerializeValue(ref Pos); s.SerializeValue(ref Yaw); }
        public bool Equals(LooseNet o) => Id == o.Id && Item == o.Item && Pos == o.Pos && Yaw == o.Yaw;
        public override int GetHashCode() => Id;
    }

    public enum PackAction : byte { Drop = 1, Wear = 2, Take = 3, Stow = 4, DropHand = 5, PickUp = 6 }

    /// <summary>Rucksacks: the host keeps a <see cref="PackWorld"/>, every client gets the packs, their contents and loose items as lists.
    /// Clients ask for actions by RPC; the host checks reach from its own view of the hikers.</summary>
    public sealed partial class NightSession
    {
        public readonly NetworkList<PackNet> PackList = new NetworkList<PackNet>();
        public readonly NetworkList<PackItemNet> PackItems = new NetworkList<PackItemNet>();
        public readonly NetworkList<LooseNet> LooseList = new NetworkList<LooseNet>();
        /// <summary>Local: the host's answer to our last pack action (for the HUD).</summary>
        public event Action<PackResult> PackAnswer;

        PackWorld packs;
        int packsSent = -1;
        readonly HashSet<string> equipped = new HashSet<string>();

        public PackWorld Packs => packs;

        void InitPacks()
        {
            packs = new PackWorld();
            packs.Locate = token =>
            {
                if (token.Length > 1 && ulong.TryParse(token.Substring(1), out var id))
                {
                    var h = HikerController.For(id);
                    if (h != null) return (h.transform.position.x, h.transform.position.z);
                }
                return null;
            };
            // firewood gathered by the fire, to try carrying it
            var c = WorldData.Camp;
            for (int i = 0; i < 3; i++)
            {
                float x = c.x - 3.2f + i * .7f, z = c.z + 2.4f;
                packs.AddLoose(ItemId.Firewood, x, Ground(x, z), z, 15f * i);
            }
        }

        static float Ground(float x, float z)
        {
            float h = TerrainBuilder.Height(Bootstrap.Dem, x, z);
            if (Physics.Raycast(new Vector3(x, h + 2f, z), Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.attachedRigidbody == null)
                return hit.point.y;
            return h;
        }

        void PacksClientJoined(ulong clientId)
        {
            string token = Token(clientId);
            if (!equipped.Add(token)) return;
            var p = run.Players.TryGetValue(token, out var pl) ? pl : null;
            float x = p?.X ?? 0, z = p?.Z ?? 0;
            packs.AddPack(token, x, Ground(x, z), z, Items.Starter);
        }

        void PacksClientLeft(ulong clientId)
        {
            string token = Token(clientId);
            if (!run.Players.TryGetValue(token, out var p)) return;
            packs.Leave(token, p.X, Ground(p.X, p.Z), p.Z);
        }

        void SyncPacks()
        {
            if (packs == null || packs.Version == packsSent) return;
            packsSent = packs.Version;
            PackList.Clear(); PackItems.Clear(); LooseList.Clear();
            foreach (var p in packs.Packs.Values)
            {
                ulong wearer = PackNet.NoWearer;
                if (p.Wearer != null && ulong.TryParse(p.Wearer.Substring(1), out var id)) wearer = id;
                PackList.Add(new PackNet { Id = p.Id, Wearer = wearer, Pos = new Vector3(p.X, p.Y, p.Z), Yaw = p.Yaw });
                foreach (var it in p.Contents) PackItems.Add(new PackItemNet { Pack = p.Id, Item = (byte)it });
            }
            foreach (var l in packs.Loose.Values) LooseList.Add(new LooseNet { Id = l.Id, Item = (byte)l.Item, Pos = new Vector3(l.X, l.Y, l.Z), Yaw = l.Yaw });
            foreach (var h in HikerController.All)
            {
                if (h == null) continue;
                byte carried = (byte)packs.Hand(Token(h.OwnerClientId));
                if (h.Carried.Value != carried) h.Carried.Value = carried;
            }
        }

        /// <summary>Owner asks the host to do something with a rucksack or a loose item.</summary>
        public void RequestPack(PackAction action, int target = 0, int index = 0) => PackRpc((byte)action, target, index);

        [Rpc(SendTo.Server)]
        void PackRpc(byte action, int target, int index, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            var hiker = HikerController.For(sender);
            if (hiker == null || packs == null) return;
            if (run.Players.TryGetValue(Token(sender), out var pl) && pl.Outcome != Outcome.None) return;
            string token = Token(sender);
            var pos = hiker.transform.position;
            float yaw = hiker.LookYaw.Value;
            var front = pos + Quaternion.Euler(0, yaw, 0) * Vector3.forward * .75f;
            float fy = Ground(front.x, front.z);
            PackResult r;
            switch ((PackAction)action)
            {
                case PackAction.Drop: r = packs.Drop(token, front.x, fy, front.z, yaw + 180f); break;
                case PackAction.Wear: r = packs.Wear(token, target, pos.x, pos.z); break;
                case PackAction.Take: r = packs.Take(token, target, index, pos.x, pos.z); break;
                case PackAction.Stow: r = packs.Stow(token, target, pos.x, pos.z); break;
                case PackAction.DropHand: r = packs.DropHand(token, front.x, fy, front.z, yaw); break;
                case PackAction.PickUp: r = packs.PickUp(token, target, pos.x, pos.z); break;
                default: return;
            }
            SyncPacks();
            PackResultRpc((byte)r, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void PackResultRpc(byte result, RpcParams rpc) => PackAnswer?.Invoke((PackResult)result);
    }
}
