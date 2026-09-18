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

    /// <summary>An item as it travels: kind, count (logs in an armful, matches in a box) and how damp it is.</summary>
    public struct StackNet : INetworkSerializable, IEquatable<StackNet>
    {
        public byte Item, Amount, Wet;
        public static StackNet Of(ItemStack s) => new StackNet { Item = (byte)s.Id, Amount = s.Amount, Wet = s.Wet };
        public ItemStack Stack => new ItemStack((ItemId)Item, Amount, Wet);
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter { s.SerializeValue(ref Item); s.SerializeValue(ref Amount); s.SerializeValue(ref Wet); }
        public bool Equals(StackNet o) => Item == o.Item && Amount == o.Amount && Wet == o.Wet;
        public override int GetHashCode() => Item << 16 | Amount << 8 | Wet;
    }

    public struct PackItemNet : INetworkSerializable, IEquatable<PackItemNet>
    {
        public int Pack;
        public StackNet Item;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter { s.SerializeValue(ref Pack); Item.NetworkSerialize(s); }
        public bool Equals(PackItemNet o) => Pack == o.Pack && Item.Equals(o.Item);
        public override int GetHashCode() => Pack * 31 + Item.GetHashCode();
    }

    public struct LooseNet : INetworkSerializable, IEquatable<LooseNet>
    {
        public int Id;
        public StackNet Item;
        public Vector3 Pos;
        public float Yaw;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter { s.SerializeValue(ref Id); Item.NetworkSerialize(s); s.SerializeValue(ref Pos); s.SerializeValue(ref Yaw); }
        public bool Equals(LooseNet o) => Id == o.Id && Item.Equals(o.Item) && Pos == o.Pos && Yaw == o.Yaw;
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

        /// <summary>Local: the host says a job with the axe or the saw is done (or was given up).</summary>
        public event Action<WorkKind, bool> WorkAnswer;

        PackWorld packs;
        int packsSent = -1;
        readonly HashSet<string> equipped = new HashSet<string>();

        sealed class Job
        {
            public WorkKind Kind;
            public double Started;
            public float Needed;
            public Vector3 Target;
            public int Loose;
            public Vector3 From;
        }

        readonly Dictionary<ulong, Job> jobs = new Dictionary<ulong, Job>();

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
            // the fire of 31 Jan was built on damp logs: a couple of armfuls are still lying by it
            if (Height1079.Core.World.IsElbrus) return;   // no fire, no firewood on the glaciers of Elbrus
            var c = WorldData.Camp;
            for (int i = 0; i < 2; i++)
            {
                float x = c.x - 3.2f + i * .7f, z = c.z + 2.4f;
                packs.AddLoose(new ItemStack(ItemId.Firewood, 3), x, Ground(x, z), z, 15f * i);
            }
            run.KindleSupplies = t => packs.Has(t, ItemId.Matches, true) && packs.Has(t, ItemId.Firewood);
            run.KindleSpent = t => { packs.Spend(t, ItemId.Matches, 1, true); packs.Spend(t, ItemId.Firewood, 1); };
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
            // Kholat: the first gets the axe, the second the saw, everyone a box of matches. Elbrus: no 1959 tools at
            // all, and the двойка and the burner that turn a halt into a camp (Items.ElbrusStarter)
            packs.AddPack(token, x, Ground(x, z), z, Items.StarterFor(equipped.Count - 1, Height1079.Core.World.Current));
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
                foreach (var it in p.Contents) PackItems.Add(new PackItemNet { Pack = p.Id, Item = StackNet.Of(it) });
            }
            foreach (var l in packs.Loose.Values) LooseList.Add(new LooseNet { Id = l.Id, Item = StackNet.Of(l.Stack), Pos = new Vector3(l.X, l.Y, l.Z), Yaw = l.Yaw });
            foreach (var h in HikerController.All)
            {
                if (h == null) continue;
                var carried = StackNet.Of(packs.Hand(Token(h.OwnerClientId)));
                if (!h.Carried.Value.Equals(carried)) h.Carried.Value = carried;
            }
        }

        /// <summary>What the participant can do here and now with what is in their hands: cut a branch off the tree in front,
        /// split a branch lying at their feet, feed the burning fire. Kindling is the night's own business (NightRun).</summary>
        public WorkKind ChooseWork(string token, Vector3 pos, float yaw, out Vector3 target, out int looseId)
        {
            target = pos; looseId = 0;
            if (packs == null) return WorkKind.None;
            var hand = packs.Hand(token);
            bool nearFire = WorldData.NearCamp(pos.x, pos.z) && FireRemaining.Value > 0f;
            if (hand.Id == ItemId.Firewood && nearFire) { target = pos; return WorkKind.FeedFire; }
            var tool = hand.Spec.Tool;
            if (tool == ToolKind.None) return WorkKind.None;
            // a branch in the snow within reach is split into firewood
            var branch = NearestBranch(pos);
            if (branch != null && Woodwork.CanDo(WorkKind.SplitBranch, tool))
            {
                target = new Vector3(branch.X, branch.Y, branch.Z); looseId = branch.Id;
                return WorkKind.SplitBranch;
            }
            if (Woodwork.CanDo(WorkKind.CutBranch, tool) && Backpacks.TreeInFront(pos, yaw, out var trunk)) { target = trunk; return WorkKind.CutBranch; }
            return WorkKind.None;
        }

        LooseItem NearestBranch(Vector3 pos)
        {
            LooseItem best = null; float bd = Woodwork.Reach;
            foreach (var l in packs.Loose.Values)
            {
                if (l.Stack.Id != ItemId.Branch) continue;
                float d = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(l.X, l.Z));
                if (d <= bd) { bd = d; best = l; }
            }
            return best;
        }

        /// <summary>Owner asks the host to start or give up work with the axe or the saw (E held).</summary>
        public void RequestWork(bool start) => WorkRpc(start);

        [Rpc(SendTo.Server)]
        void WorkRpc(bool start, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            if (!start) { jobs.Remove(sender); return; }
            var hiker = HikerController.For(sender);
            if (hiker == null || packs == null || run == null) return;
            string token = Token(sender);
            if (!run.Players.TryGetValue(token, out var p) || p.Outcome != Outcome.None) return;
            var pos = hiker.transform.position;
            var kind = ChooseWork(token, pos, hiker.LookYaw.Value, out var target, out int looseId);
            if (kind == WorkKind.None) return;
            jobs[sender] = new Job
            {
                Kind = kind,
                Started = Time.timeAsDouble,
                Needed = Woodwork.Seconds(kind, packs.Hand(token).Spec.Tool, p.Hands),
                Target = target,
                Loose = looseId,
                From = pos,
            };
        }

        void TickWork()
        {
            if (jobs.Count == 0) return;
            double now = Time.timeAsDouble;
            foreach (var id in new List<ulong>(jobs.Keys))
            {
                var job = jobs[id];
                var hiker = HikerController.For(id);
                string token = Token(id);
                var hand = packs.Hand(token);
                bool ok = hiker != null && run.Players.TryGetValue(token, out var p) && p.Outcome == Outcome.None
                    && Vector3.Distance(hiker.transform.position, job.From) < .8f
                    && (job.Kind == WorkKind.FeedFire ? hand.Id == ItemId.Firewood : Woodwork.CanDo(job.Kind, hand.Spec.Tool));
                if (!ok) { jobs.Remove(id); WorkDoneRpc((byte)job.Kind, false, RpcTarget.Single(id, RpcTargetUse.Temp)); continue; }
                if (now - job.Started < job.Needed) continue;
                jobs.Remove(id);
                Finish(id, token, job);
                WorkDoneRpc((byte)job.Kind, true, RpcTarget.Single(id, RpcTargetUse.Temp));
            }
        }

        void Finish(ulong client, string token, Job job)
        {
            string name = run.Players.TryGetValue(token, out var p) ? p.Name : "Путник";
            switch (job.Kind)
            {
                case WorkKind.CutBranch:
                {
                    var at = new Vector3(job.Target.x, 0, job.Target.z) + (job.From - job.Target).normalized * .5f;
                    packs.AddLoose(new ItemStack(ItemId.Branch), at.x, Ground(at.x, at.z), at.z, UnityEngine.Random.Range(0f, 360f));
                    run.Record($"{name} {(packs.HeldTool(token) == ToolKind.Saw ? "отпиливает" : "срубает")} сухую ветку.");
                    break;
                }
                case WorkKind.SplitBranch:
                {
                    if (!packs.Loose.TryGetValue(job.Loose, out var branch) || branch.Stack.Id != ItemId.Branch) break;
                    packs.Loose.Remove(job.Loose);
                    packs.AddLoose(new ItemStack(ItemId.Firewood, Woodwork.LogsPerBranch), branch.X, branch.Y, branch.Z, branch.Yaw);
                    run.Record($"{name} разрубает ветку на дрова.");
                    break;
                }
                case WorkKind.FeedFire:
                {
                    var hand = packs.Hand(token);
                    if (hand.Id != ItemId.Firewood) break;
                    if (run.FeedFire(token, hand.Amount, Time.timeAsDouble)) packs.Spend(token, ItemId.Firewood, hand.Amount);
                    break;
                }
            }
            SyncPacks();
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void WorkDoneRpc(byte kind, bool done, RpcParams rpc) => WorkAnswer?.Invoke((WorkKind)kind, done);

        /// <summary>Matches held in the hands take the weather; by the fire everything dries.</summary>
        void TickWeatherOnItems(float dt)
        {
            foreach (var kv in NetworkManager.ConnectedClients)
            {
                var hiker = kv.Value.PlayerObject != null ? kv.Value.PlayerObject.GetComponent<HikerController>() : null;
                if (hiker == null) continue;
                var pos = hiker.transform.position;
                bool water = WorldData.CreekDistance(pos.x, pos.z) < 3.5f && pos.y < WorldData.ShelterHeight;
                bool fire = WorldData.NearCamp(pos.x, pos.z) && FireRemaining.Value > 0f;
                packs.Weather(Token(kv.Key), dt, Weather.Storm > .35f, water, fire);
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
