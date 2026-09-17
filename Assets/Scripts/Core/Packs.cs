using System;
using System.Collections.Generic;

namespace Height1079.Core
{
    /// <summary>Things that go into a rucksack. The list is a first draft (food and gear of the 1959 inventory will be worked out separately).</summary>
    public enum ItemId : byte
    {
        None = 0,
        Rusks, Lard, Stew, CondensedMilk, Sugar, Oatmeal, Chocolate,
        Matches, Candles, Batteries, Hatchet, Flask, Pot, Mittens, Socks, Firewood,
    }

    public enum ItemKind : byte { Food, Gear, Clothes, Fuel }

    public readonly struct ItemSpec
    {
        public readonly ItemId Id;
        public readonly string Name;
        public readonly float Kg, Litres;
        public readonly ItemKind Kind;
        public ItemSpec(ItemId id, string name, float kg, float litres, ItemKind kind) { Id = id; Name = name; Kg = kg; Litres = litres; Kind = kind; }
    }

    /// <summary>Item catalogue: name, weight (kg) and volume (litres) of one piece.</summary>
    public static class Items
    {
        static readonly ItemSpec[] specs =
        {
            new ItemSpec(ItemId.None, "—", 0f, 0f, ItemKind.Gear),
            new ItemSpec(ItemId.Rusks, "Сухари (мешочек)", 1f, 3f, ItemKind.Food),
            new ItemSpec(ItemId.Lard, "Сало", .5f, .6f, ItemKind.Food),
            new ItemSpec(ItemId.Stew, "Тушёнка", .45f, .4f, ItemKind.Food),
            new ItemSpec(ItemId.CondensedMilk, "Сгущёнка", .4f, .35f, ItemKind.Food),
            new ItemSpec(ItemId.Sugar, "Сахар", 1f, 1.2f, ItemKind.Food),
            new ItemSpec(ItemId.Oatmeal, "Овсянка", 1f, 1.8f, ItemKind.Food),
            new ItemSpec(ItemId.Chocolate, "Шоколад", .1f, .1f, ItemKind.Food),
            new ItemSpec(ItemId.Matches, "Спички", .05f, .05f, ItemKind.Gear),
            new ItemSpec(ItemId.Candles, "Свечи", .3f, .3f, ItemKind.Gear),
            new ItemSpec(ItemId.Batteries, "Батарейки", .2f, .1f, ItemKind.Gear),
            new ItemSpec(ItemId.Hatchet, "Топорик", 1.1f, 1.5f, ItemKind.Gear),
            new ItemSpec(ItemId.Flask, "Фляга", .9f, 1f, ItemKind.Gear),
            new ItemSpec(ItemId.Pot, "Котелок", .7f, 4f, ItemKind.Gear),
            new ItemSpec(ItemId.Mittens, "Рукавицы", .3f, 1.5f, ItemKind.Clothes),
            new ItemSpec(ItemId.Socks, "Шерстяные носки", .2f, .8f, ItemKind.Clothes),
            new ItemSpec(ItemId.Firewood, "Охапка дров", 6f, 18f, ItemKind.Fuel),
        };

        public static ItemSpec Spec(ItemId id) => (int)id < specs.Length ? specs[(int)id] : specs[0];
        public static int Count => specs.Length;

        /// <summary>What every participant starts the night with (about 11 kg with the rucksack).</summary>
        public static readonly ItemId[] Starter =
        {
            ItemId.Rusks, ItemId.Lard, ItemId.Stew, ItemId.Stew, ItemId.CondensedMilk, ItemId.Sugar, ItemId.Chocolate,
            ItemId.Matches, ItemId.Candles, ItemId.Batteries, ItemId.Flask, ItemId.Pot, ItemId.Mittens, ItemId.Socks,
        };
    }

    /// <summary>A soft canvas rucksack of the 1950s: fixed volume, its own weight, an ordered list of contents.
    /// Either worn by a participant (<see cref="Wearer"/>) or standing in the snow at X/Y/Z.</summary>
    public sealed class Backpack
    {
        public const float CapacityLitres = 50f, OwnKg = 1.6f;
        public readonly int Id;
        public string Wearer;
        public float X, Y, Z, Yaw;
        readonly List<ItemId> items = new List<ItemId>();

        public Backpack(int id) { Id = id; }

        public IReadOnlyList<ItemId> Contents => items;
        public float Litres { get { float s = 0; foreach (var i in items) s += Items.Spec(i).Litres; return s; } }
        public float Kg { get { float s = OwnKg; foreach (var i in items) s += Items.Spec(i).Kg; return s; } }
        public bool Fits(ItemId id) => id != ItemId.None && Litres + Items.Spec(id).Litres <= CapacityLitres + 1e-4f;

        public bool Put(ItemId id)
        {
            if (!Fits(id)) return false;
            items.Add(id);
            return true;
        }

        public ItemId TakeAt(int index)
        {
            if (index < 0 || index >= items.Count) return ItemId.None;
            var id = items[index];
            items.RemoveAt(index);
            return id;
        }
    }

    /// <summary>An item lying in the snow.</summary>
    public sealed class LooseItem
    {
        public int Id;
        public ItemId Item;
        public float X, Y, Z, Yaw;
    }

    public enum PackResult : byte { Ok, TooFar, HandsFull, HandsEmpty, NoRoom, NoItem, NoPack, AlreadyWearing, WornByOther }

    /// <summary>All rucksacks, hands and loose items of a night. Host-side; every change bumps <see cref="Version"/> so the network layer resends.
    /// Reach is checked on the ground plane against the actor's position; a pack worn by someone is where they are (<see cref="Locate"/>).</summary>
    public sealed class PackWorld
    {
        public const float Reach = 2.2f;
        public readonly Dictionary<int, Backpack> Packs = new Dictionary<int, Backpack>();
        public readonly Dictionary<int, LooseItem> Loose = new Dictionary<int, LooseItem>();
        readonly Dictionary<string, ItemId> hands = new Dictionary<string, ItemId>();
        public int Version { get; private set; }
        /// <summary>Ground position of a participant (x, z), or null if unknown.</summary>
        public Func<string, (float x, float z)?> Locate = _ => null;
        int nextPack = 1, nextLoose = 1;

        void Changed() => Version++;

        public ItemId Hand(string token) => hands.TryGetValue(token, out var i) ? i : ItemId.None;

        public Backpack Worn(string token)
        {
            foreach (var p in Packs.Values) if (p.Wearer == token) return p;
            return null;
        }

        /// <summary>A new rucksack on <paramref name="wearer"/>'s back (or in the snow if null), filled with <paramref name="contents"/>.</summary>
        public Backpack AddPack(string wearer, float x, float y, float z, IEnumerable<ItemId> contents = null)
        {
            var p = new Backpack(nextPack++) { Wearer = wearer, X = x, Y = y, Z = z };
            if (contents != null) foreach (var i in contents) p.Put(i);
            Packs[p.Id] = p;
            Changed();
            return p;
        }

        public LooseItem AddLoose(ItemId item, float x, float y, float z, float yaw = 0f)
        {
            var l = new LooseItem { Id = nextLoose++, Item = item, X = x, Y = y, Z = z, Yaw = yaw };
            Loose[l.Id] = l;
            Changed();
            return l;
        }

        static float Dist(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        /// <summary>Ground position of a pack: its wearer's if worn.</summary>
        public (float x, float z)? Where(Backpack p)
        {
            if (p.Wearer == null) return (p.X, p.Z);
            return Locate(p.Wearer);
        }

        bool InReach(string token, Backpack p, float x, float z)
        {
            if (p.Wearer == token) return true;
            var w = Where(p);
            return w.HasValue && Dist(x, z, w.Value.x, w.Value.z) <= Reach;
        }

        /// <summary>Nearest pack the actor can open: own first, then packs in the snow and on other backs within reach.</summary>
        public Backpack Reachable(string token, float x, float z, bool includeOwn = true, bool includeWorn = true)
        {
            if (includeOwn) { var own = Worn(token); if (own != null) return own; }
            Backpack best = null; float bd = float.MaxValue;
            foreach (var p in Packs.Values)
            {
                if (p.Wearer == token || (!includeWorn && p.Wearer != null)) continue;
                var w = Where(p);
                if (!w.HasValue) continue;
                float d = Dist(x, z, w.Value.x, w.Value.z);
                if (d <= Reach && d < bd) { bd = d; best = p; }
            }
            return best;
        }

        public LooseItem NearestLoose(float x, float z)
        {
            LooseItem best = null; float bd = Reach;
            foreach (var l in Loose.Values)
            {
                float d = Dist(x, z, l.X, l.Z);
                if (d <= bd) { bd = d; best = l; }
            }
            return best;
        }

        /// <summary>Take the rucksack off and stand it in the snow at the given spot.</summary>
        public PackResult Drop(string token, float x, float y, float z, float yaw)
        {
            var p = Worn(token);
            if (p == null) return PackResult.NoPack;
            p.Wearer = null; p.X = x; p.Y = y; p.Z = z; p.Yaw = yaw;
            Changed();
            return PackResult.Ok;
        }

        /// <summary>Put on a rucksack standing in the snow.</summary>
        public PackResult Wear(string token, int packId, float x, float z)
        {
            if (Worn(token) != null) return PackResult.AlreadyWearing;
            if (!Packs.TryGetValue(packId, out var p)) return PackResult.NoPack;
            if (p.Wearer != null) return PackResult.WornByOther;
            if (Dist(x, z, p.X, p.Z) > Reach) return PackResult.TooFar;
            p.Wearer = token;
            Changed();
            return PackResult.Ok;
        }

        /// <summary>Take item <paramref name="index"/> out of a pack into the hands (own pack, one in the snow, or a companion's back).</summary>
        public PackResult Take(string token, int packId, int index, float x, float z)
        {
            if (Hand(token) != ItemId.None) return PackResult.HandsFull;
            if (!Packs.TryGetValue(packId, out var p)) return PackResult.NoPack;
            if (!InReach(token, p, x, z)) return PackResult.TooFar;
            var item = p.TakeAt(index);
            if (item == ItemId.None) return PackResult.NoItem;
            hands[token] = item;
            Changed();
            return PackResult.Ok;
        }

        /// <summary>Put what is in the hands into a pack.</summary>
        public PackResult Stow(string token, int packId, float x, float z)
        {
            var item = Hand(token);
            if (item == ItemId.None) return PackResult.HandsEmpty;
            if (!Packs.TryGetValue(packId, out var p)) return PackResult.NoPack;
            if (!InReach(token, p, x, z)) return PackResult.TooFar;
            if (!p.Put(item)) return PackResult.NoRoom;
            hands.Remove(token);
            Changed();
            return PackResult.Ok;
        }

        /// <summary>Drop what is in the hands into the snow.</summary>
        public PackResult DropHand(string token, float x, float y, float z, float yaw)
        {
            var item = Hand(token);
            if (item == ItemId.None) return PackResult.HandsEmpty;
            hands.Remove(token);
            AddLoose(item, x, y, z, yaw);
            return PackResult.Ok;
        }

        /// <summary>Pick a loose item up into the hands.</summary>
        public PackResult PickUp(string token, int looseId, float x, float z)
        {
            if (Hand(token) != ItemId.None) return PackResult.HandsFull;
            if (!Loose.TryGetValue(looseId, out var l)) return PackResult.NoItem;
            if (Dist(x, z, l.X, l.Z) > Reach) return PackResult.TooFar;
            Loose.Remove(looseId);
            hands[token] = l.Item;
            Changed();
            return PackResult.Ok;
        }

        /// <summary>A participant leaves: whatever they carry stays on the slope where they were.</summary>
        public void Leave(string token, float x, float y, float z)
        {
            var p = Worn(token);
            if (p != null) { p.Wearer = null; p.X = x; p.Y = y; p.Z = z; Changed(); }
            if (Hand(token) != ItemId.None) DropHand(token, x + .4f, y, z, 0f);
        }

        /// <summary>Everything a participant carries: the worn pack with its contents and the item in the hands.</summary>
        public float CarriedKg(string token)
        {
            var p = Worn(token);
            return (p != null ? p.Kg : 0f) + Items.Spec(Hand(token)).Kg;
        }

        public static string Describe(PackResult r) => r switch
        {
            PackResult.Ok => "",
            PackResult.TooFar => "Слишком далеко",
            PackResult.HandsFull => "Руки заняты",
            PackResult.HandsEmpty => "В руках ничего нет",
            PackResult.NoRoom => "В рюкзак не влезет",
            PackResult.NoItem => "Этого уже нет",
            PackResult.NoPack => "Рюкзака нет",
            PackResult.AlreadyWearing => "Рюкзак уже на спине",
            PackResult.WornByOther => "Этот рюкзак на чужой спине",
            _ => "",
        };
    }
}
