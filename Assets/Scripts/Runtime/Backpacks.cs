using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The rucksack on this client: what the local hiker carries, which pack is open in the HUD, and the keys that ask the host
    /// to put things in, take them out, drop the pack or pick something up. The host keeps the truth in <see cref="PackWorld"/>.</summary>
    public static class Backpacks
    {
        /// <summary>Pack shown in the HUD window, 0 when closed.</summary>
        public static int OpenPack;
        public static bool UiOpen => OpenPack != 0;
        /// <summary>Last refusal from the host, for a short line in the HUD.</summary>
        public static string Message = "";
        public static float MessageUntil;

        static readonly List<ItemStack> buffer = new List<ItemStack>();
        /// <summary>Owner: work with the axe or the saw is being held down (E, or the HUD button).</summary>
        public static bool Working;

        public static void Note(PackResult r)
        {
            string text = PackWorld.Describe(r);
            if (text.Length == 0) return;
            Message = text; MessageUntil = Time.time + 2.5f;
        }

        public static NightSession Session => NightSession.Instance;

        public static bool TryPack(int id, out PackNet pack)
        {
            pack = default;
            var s = Session;
            if (s == null || id == 0) return false;
            for (int i = 0; i < s.PackList.Count; i++) if (s.PackList[i].Id == id) { pack = s.PackList[i]; return true; }
            return false;
        }

        /// <summary>Contents of a pack, in the order the host keeps them.</summary>
        public static List<ItemStack> Contents(int id)
        {
            buffer.Clear();
            var s = Session;
            if (s == null) return buffer;
            for (int i = 0; i < s.PackItems.Count; i++) if (s.PackItems[i].Pack == id) buffer.Add(s.PackItems[i].Item.Stack);
            return buffer;
        }

        public static float Litres(int id) { float v = 0; foreach (var i in Contents(id)) v += i.Litres; return v; }
        public static float Kg(int id) { float v = Backpack.OwnKg; foreach (var i in Contents(id)) v += i.Kg; return v; }

        /// <summary>Where a pack is: its wearer's back or the snow.</summary>
        public static Vector3 Position(PackNet p)
        {
            if (p.Wearer == PackNet.NoWearer) return p.Pos;
            var h = HikerController.ByClient(p.Wearer);
            return h != null ? h.transform.position : p.Pos;
        }

        public static int MyPackId(HikerController me)
        {
            var s = Session;
            if (s == null || me == null) return 0;
            for (int i = 0; i < s.PackList.Count; i++) if (s.PackList[i].Wearer == me.OwnerClientId) return s.PackList[i].Id;
            return 0;
        }

        /// <summary>Nearest pack that is not on our own back: in the snow or on a companion's back.</summary>
        public static int NearbyPackId(HikerController me, bool groundOnly = false)
        {
            var s = Session;
            if (s == null || me == null) return 0;
            int best = 0; float bd = PackWorld.Reach;
            var p0 = me.transform.position;
            for (int i = 0; i < s.PackList.Count; i++)
            {
                var p = s.PackList[i];
                if (p.Wearer == me.OwnerClientId || (groundOnly && p.Wearer != PackNet.NoWearer)) continue;
                float d = Vector2.Distance(new Vector2(p0.x, p0.z), new Vector2(Position(p).x, Position(p).z));
                if (d <= bd) { bd = d; best = p.Id; }
            }
            return best;
        }

        public static int NearbyLooseId(HikerController me, out ItemStack item)
        {
            item = ItemStack.Empty;
            var s = Session;
            if (s == null || me == null) return 0;
            int best = 0; float bd = PackWorld.Reach;
            var p0 = me.transform.position;
            for (int i = 0; i < s.LooseList.Count; i++)
            {
                var l = s.LooseList[i];
                float d = Vector2.Distance(new Vector2(p0.x, p0.z), new Vector2(l.Pos.x, l.Pos.z));
                if (d <= bd) { bd = d; best = l.Id; item = l.Item.Stack; }
            }
            return best;
        }

        /// <summary>Everything the local hiker carries, in kilograms.</summary>
        public static float CarriedKg(HikerController me)
        {
            if (me == null) return 0f;
            int id = MyPackId(me);
            return (id != 0 ? Kg(id) : 0f) + me.Carried.Value.Stack.Kg;
        }

        public static void Close() => OpenPack = 0;

        /// <summary>Owner keys: Tab opens a rucksack, G takes it off or puts it on, R picks up / stows, X drops what is in the hands.</summary>
        public static void HandleInput(HikerController me)
        {
            var s = Session;
            if (s == null || me == null) return;
            if (Controls.PackOpen)
            {
                if (UiOpen) Close();
                else
                {
                    int id = MyPackId(me);
                    if (id == 0) id = NearbyPackId(me);
                    if (id != 0) OpenPack = id; else Note(PackResult.NoPack);
                }
            }
            if (Controls.PackWear)
            {
                int mine = MyPackId(me);
                if (mine != 0) { s.RequestPack(PackAction.Drop); if (OpenPack == mine) Close(); }
                else
                {
                    int ground = NearbyPackId(me, true);
                    if (ground != 0) s.RequestPack(PackAction.Wear, ground); else Note(PackResult.NoPack);
                }
            }
            if (Controls.PackGrab)
            {
                if (me.Carried.Value.Item != 0)
                {
                    int id = UiOpen ? OpenPack : MyPackId(me);
                    if (id == 0) id = NearbyPackId(me);
                    if (id != 0) s.RequestPack(PackAction.Stow, id); else Note(PackResult.NoPack);
                }
                else
                {
                    int l = NearbyLooseId(me, out _);
                    if (l != 0) s.RequestPack(PackAction.PickUp, l); else Note(PackResult.NoItem);
                }
            }
            if (Controls.PackDropHand && me.Carried.Value.Item != 0) s.RequestPack(PackAction.DropHand);

            // the window closes when the pack goes out of reach or disappears
            if (UiOpen)
            {
                if (!TryPack(OpenPack, out var open)) Close();
                else if (open.Wearer != me.OwnerClientId)
                {
                    var p = Position(open);
                    if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(me.transform.position.x, me.transform.position.z)) > PackWorld.Reach + .6f) Close();
                }
            }
        }

        /// <summary>Hint under the crosshair: what is within reach.</summary>
        public static string Prompt(HikerController me)
        {
            if (Time.time < MessageUntil && Message.Length > 0) return Message;
            if (me == null) return "";
            var carried = me.Carried.Value.Stack;
            var work = WorkHere(me);
            if (work != WorkKind.None) return $"{Woodwork.Title(work, carried.Spec.Tool)} · держите E";
            if (!carried.IsEmpty) return $"В руках: {carried.Describe()} · R — в рюкзак · X — бросить";
            int l = NearbyLooseId(me, out var item);
            if (l != 0) return $"{item.Describe()} · R — поднять";
            int ground = NearbyPackId(me, true);
            if (ground != 0) return "Рюкзак в снегу · G — надеть · Tab — открыть";
            int other = NearbyPackId(me);
            if (other != 0) return "Рюкзак напарника · Tab — открыть";
            return "";
        }

        /// <summary>The trunk the hiker is facing (terrain trees, straight out of the data: see <see cref="Forest"/>).</summary>
        public static bool TreeInFront(Vector3 pos, float yaw, out Vector3 point) => Forest.InFront(pos, yaw, out point, Woodwork.Reach);

        /// <summary>What E would do here (the host decides for real; this is for the prompt and the HUD button).</summary>
        public static WorkKind WorkHere(HikerController me)
        {
            var s = Session;
            if (s == null || me == null) return WorkKind.None;
            return s.ChooseWork("c" + me.OwnerClientId, me.transform.position, me.Yaw, out _, out _);
        }

        /// <summary>Owner: hold E (or the HUD button) to work; the host is told when it starts and when it is let go.</summary>
        public static void HandleWork(HikerController me, bool wants)
        {
            var s = Session;
            if (s == null || me == null) return;
            if (wants == Working) return;
            Working = wants;
            s.RequestWork(wants);
        }

    }
}