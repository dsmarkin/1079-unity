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
        Axe, Saw, Branch,
        // taken away from a café of Prielbrusye (see Refreshments); the night on Kholat never hands these out
        Khychin, Shashlyk, PizzaBox, HotCup,
        // hired at the counter on the Azau meadow (see Rental): the kit Ascent.Required checks for above Pastukhov
        // rocks. Every one of them is a separate object in the rucksack with its own weight and its own price.
        Crampons, IceAxe, Harness, Helmet, Goggles, SnowMask, DownJacket, Balaclava, HighMittens, SpareGloves,
        Thermos, Headlamp, SpaceBlanket,
        // the bivouac: what turns a halt into a camp you can come back to (see Camp). Carried, pitched and struck,
        // never checked by Ascent.Required — the gate at Pastukhov rocks is about surviving the day, not the night.
        Tent, Burner,
        // the folded sheet a guide hands out on the first evening (see Programme). It weighs nothing, it does nothing
        // and it is the whole tutorial of the game: nine days, twenty-two lines and the reason for every one of them.
        ProgrammeSheet,
    }

    public enum ItemKind : byte { Food, Gear, Clothes, Fuel }

    /// <summary>What an item can be used for: a big axe chops, a hatchet does the same slowly, the saw cuts.</summary>
    public enum ToolKind : byte { None, Axe, Hatchet, Saw }

    public readonly struct ItemSpec
    {
        public readonly ItemId Id;
        public readonly string Name;
        public readonly float Kg, Litres;
        public readonly ItemKind Kind;
        public readonly ToolKind Tool;
        /// <summary>How many go in one item: logs in an armful, matches in a box. 1 for everything else.</summary>
        public readonly byte MaxAmount;
        /// <summary>Weight and volume scale with the count (an armful of firewood) or not (a box of matches).</summary>
        public readonly bool PerUnit;
        /// <summary>Damp matches do not strike; only these items track it.</summary>
        public readonly bool Soaks;

        public ItemSpec(ItemId id, string name, float kg, float litres, ItemKind kind, ToolKind tool = ToolKind.None, byte maxAmount = 1, bool perUnit = false, bool soaks = false)
        { Id = id; Name = name; Kg = kg; Litres = litres; Kind = kind; Tool = tool; MaxAmount = maxAmount; PerUnit = perUnit; Soaks = soaks; }
    }

    /// <summary>One item with its state: an armful of so many logs, a box with so many matches left, dry or damp.</summary>
    public readonly struct ItemStack : IEquatable<ItemStack>
    {
        public readonly ItemId Id;
        public readonly byte Amount;
        /// <summary>0 dry … 100 soaked; only matches care.</summary>
        public readonly byte Wet;

        public ItemStack(ItemId id, byte amount = 1, byte wet = 0)
        {
            Id = id;
            Amount = id == ItemId.None ? (byte)0 : Math.Max((byte)1, Math.Min(amount, Items.Spec(id).MaxAmount));
            Wet = wet;
        }

        public static readonly ItemStack Empty = default;
        public bool IsEmpty => Id == ItemId.None;
        public ItemSpec Spec => Items.Spec(Id);
        public float Kg => Spec.PerUnit ? Spec.Kg * Amount : Spec.Kg;
        public float Litres => Spec.PerUnit ? Spec.Litres * Amount : Spec.Litres;
        /// <summary>Damp matches (60 and up) do not light.</summary>
        public bool Damp => Spec.Soaks && Wet >= Items.DampAt;
        public ItemStack With(int amount) => amount <= 0 ? Empty : new ItemStack(Id, (byte)Math.Min(amount, Spec.MaxAmount), Wet);
        public ItemStack Wetter(float delta) => new ItemStack(Id, Amount, (byte)Math.Max(0, Math.Min(100, Wet + delta)));
        public bool Equals(ItemStack o) => Id == o.Id && Amount == o.Amount && Wet == o.Wet;
        public override bool Equals(object o) => o is ItemStack s && Equals(s);
        public override int GetHashCode() => (int)Id << 16 | Amount << 8 | Wet;

        public string Describe()
        {
            var spec = Spec;
            string name = spec.Name;
            if (spec.MaxAmount > 1) name += " ×" + Amount;
            if (Damp) name += " (отсырели)";
            else if (spec.Soaks && Wet > 20) name += " (влажные)";
            return name;
        }
    }

    /// <summary>Item catalogue: name, weight (kg) and volume (litres) of one piece.</summary>
    public static class Items
    {
        /// <summary>Matches this damp or worse will not strike.</summary>
        public const byte DampAt = 60;
        public const byte MatchesInBox = 24, LogsInArmful = 6;

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
            new ItemSpec(ItemId.Matches, "Спички", .05f, .05f, ItemKind.Gear, ToolKind.None, MatchesInBox, false, true),
            new ItemSpec(ItemId.Candles, "Свечи", .3f, .3f, ItemKind.Gear),
            new ItemSpec(ItemId.Batteries, "Батарейки", .2f, .1f, ItemKind.Gear),
            new ItemSpec(ItemId.Hatchet, "Топорик", 1.1f, 1.5f, ItemKind.Gear, ToolKind.Hatchet),
            new ItemSpec(ItemId.Flask, "Фляга", .9f, 1f, ItemKind.Gear),
            new ItemSpec(ItemId.Pot, "Котелок", .7f, 4f, ItemKind.Gear),
            new ItemSpec(ItemId.Mittens, "Рукавицы", .3f, 1.5f, ItemKind.Clothes),
            new ItemSpec(ItemId.Socks, "Шерстяные носки", .2f, .8f, ItemKind.Clothes),
            new ItemSpec(ItemId.Firewood, "Дрова", 2.2f, 5f, ItemKind.Fuel, ToolKind.None, LogsInArmful, true),
            new ItemSpec(ItemId.Axe, "Топор", 2.2f, 4f, ItemKind.Gear, ToolKind.Axe),
            new ItemSpec(ItemId.Saw, "Двуручная пила", 2.6f, 7f, ItemKind.Gear, ToolKind.Saw),
            new ItemSpec(ItemId.Branch, "Сухая ветка", 5f, 16f, ItemKind.Fuel),
            new ItemSpec(ItemId.Khychin, "Хычин в фольге", .25f, .5f, ItemKind.Food),
            new ItemSpec(ItemId.Shashlyk, "Шашлык в лаваше", .4f, .9f, ItemKind.Food),
            new ItemSpec(ItemId.PizzaBox, "Пицца в коробке", .45f, 3f, ItemKind.Food),
            new ItemSpec(ItemId.HotCup, "Стакан горячего", .35f, .5f, ItemKind.Food),
            // the hire kit. Weights are what the shelf of a hire counter actually gives out; the volumes are what they
            // take out of a 50-litre sack, which is the point — the full kit is 22 litres and 5.8 kg on top of the food.
            new ItemSpec(ItemId.Crampons, "Кошки, 12 зубьев", 1f, 2.2f, ItemKind.Gear),
            new ItemSpec(ItemId.IceAxe, "Ледоруб", .55f, 2f, ItemKind.Gear),
            new ItemSpec(ItemId.Harness, "Система с усом и тремя карабинами", .9f, 2.5f, ItemKind.Gear),
            new ItemSpec(ItemId.Helmet, "Каска", .35f, 3.5f, ItemKind.Gear),
            new ItemSpec(ItemId.Goggles, "Очки S3–S4", .12f, .6f, ItemKind.Gear),
            new ItemSpec(ItemId.SnowMask, "Маска S2–S3", .15f, .7f, ItemKind.Gear),
            new ItemSpec(ItemId.DownJacket, "Пуховка", 1.1f, 7f, ItemKind.Clothes),
            new ItemSpec(ItemId.Balaclava, "Балаклава", .08f, .3f, ItemKind.Clothes),
            new ItemSpec(ItemId.HighMittens, "Варежки пуховые", .25f, 1.2f, ItemKind.Clothes),
            new ItemSpec(ItemId.SpareGloves, "Запасные перчатки", .12f, .5f, ItemKind.Clothes),
            new ItemSpec(ItemId.Thermos, "Термос, 1 л", .95f, 1.3f, ItemKind.Gear),
            new ItemSpec(ItemId.Headlamp, "Налобный фонарь", .15f, .4f, ItemKind.Gear),
            new ItemSpec(ItemId.SpaceBlanket, "Спасодеяло", .06f, .2f, ItemKind.Gear),
            // a two-man tent packs to about nine litres and weighs what a two-man tent weighs; the burner is the
            // whole of the water supply up here, because there is nothing to drink on this mountain that is not snow
            new ItemSpec(ItemId.Tent, "Палатка-двойка", 2.9f, 9f, ItemKind.Gear),
            new ItemSpec(ItemId.Burner, "Горелка с газом", .6f, 1.5f, ItemKind.Gear),
            // one sheet of paper folded in four: the programme of the ascent, handed out with the rucksack
            new ItemSpec(ItemId.ProgrammeSheet, "Программа восхождения (лист)", .02f, .05f, ItemKind.Gear),
        };

        public static ItemSpec Spec(ItemId id) => (int)id < specs.Length ? specs[(int)id] : specs[0];
        public static int Count => specs.Length;

        /// <summary>What every participant starts the night with, plus one heavy tool each (Tool below).</summary>
        public static readonly ItemId[] Starter =
        {
            ItemId.Rusks, ItemId.Lard, ItemId.Stew, ItemId.Stew, ItemId.CondensedMilk, ItemId.Sugar, ItemId.Chocolate,
            ItemId.Candles, ItemId.Batteries, ItemId.Flask, ItemId.Pot, ItemId.Mittens, ItemId.Socks,
        };

        /// <summary>The heavy tool of the n-th participant: the group carried three axes and a saw, so the first two take the axe and the saw.</summary>
        public static ItemId Tool(int index) => index == 0 ? ItemId.Axe : index == 1 ? ItemId.Saw : ItemId.Hatchet;

        /// <summary>The starting kit of the n-th participant: food, gear, a box of matches and one heavy tool.</summary>
        public static List<ItemStack> StarterFor(int index)
        {
            var list = new List<ItemStack>();
            foreach (var id in Starter) list.Add(new ItemStack(id));
            list.Add(new ItemStack(ItemId.Matches, MatchesInBox));
            list.Add(new ItemStack(Tool(index)));
            return list;
        }

        /// <summary>What a visitor comes up the Azau ropeway with. Nothing of 1959 is in it — no axe, no saw, no
        /// tinned stew for a fire there is nothing to build — and the two things that make a halt into a camp are:
        /// a двойка and a burner. Everything <see cref="Ascent.Required"/> checks for is hired at the counter below
        /// (<see cref="Rental"/>) and goes into the same rucksack; the two together come to about forty of its fifty
        /// litres, which is the point.</summary>
        public static readonly ItemId[] ElbrusStarter =
        {
            ItemId.Rusks, ItemId.Chocolate, ItemId.CondensedMilk, ItemId.Flask, ItemId.Socks,
            ItemId.Tent, ItemId.Burner,
            // and the sheet: it is issued with the rucksack the way a real programme is issued at the first briefing
            ItemId.ProgrammeSheet,
        };

        /// <summary>The starting kit for a place: the night on Kholat Syakhl is carried up the Auspiya,
        /// the day on the southern slope of Elbrus is bought in Azau.</summary>
        public static List<ItemStack> StarterFor(int index, Place place)
        {
            if (place != Place.Elbrus) return StarterFor(index);
            var list = new List<ItemStack>();
            foreach (var id in ElbrusStarter) list.Add(new ItemStack(id));
            list.Add(new ItemStack(ItemId.Matches, MatchesInBox));
            return list;
        }
    }

    /// <summary>A soft canvas rucksack of the 1950s: fixed volume, its own weight, an ordered list of contents.
    /// Either worn by a participant (<see cref="Wearer"/>) or standing in the snow at X/Y/Z.</summary>
    public sealed class Backpack
    {
        public const float CapacityLitres = 50f, OwnKg = 1.6f;
        public readonly int Id;
        public string Wearer;
        public float X, Y, Z, Yaw;
        readonly List<ItemStack> items = new List<ItemStack>();

        public Backpack(int id) { Id = id; }

        public IReadOnlyList<ItemStack> Contents => items;
        public float Litres { get { float s = 0; foreach (var i in items) s += i.Litres; return s; } }
        public float Kg { get { float s = OwnKg; foreach (var i in items) s += i.Kg; return s; } }
        public bool Fits(ItemStack stack) => !stack.IsEmpty && Litres + stack.Litres <= CapacityLitres + 1e-4f;

        public bool Put(ItemStack stack)
        {
            if (!Fits(stack)) return false;
            items.Add(stack);
            return true;
        }

        public bool Put(ItemId id) => Put(new ItemStack(id));

        public ItemStack TakeAt(int index)
        {
            if (index < 0 || index >= items.Count) return ItemStack.Empty;
            var s = items[index];
            items.RemoveAt(index);
            return s;
        }

        /// <summary>Index of the first item of this kind that is usable (matches: a box that still has dry matches), or -1.</summary>
        public int IndexOf(ItemId id, bool usable = false)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Id != id) continue;
                if (usable && items[i].Damp) continue;
                return i;
            }
            return -1;
        }

        public void SetAt(int index, ItemStack stack)
        {
            if (index < 0 || index >= items.Count) return;
            if (stack.IsEmpty) items.RemoveAt(index); else items[index] = stack;
        }
    }

    /// <summary>An item lying in the snow.</summary>
    public sealed class LooseItem
    {
        public int Id;
        public ItemStack Stack;
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
        readonly Dictionary<string, ItemStack> hands = new Dictionary<string, ItemStack>();
        readonly Dictionary<string, float> soak = new Dictionary<string, float>();   // fraction of a wetness point not yet applied
        public int Version { get; private set; }
        /// <summary>Ground position of a participant (x, z), or null if unknown.</summary>
        public Func<string, (float x, float z)?> Locate = _ => null;
        int nextPack = 1, nextLoose = 1;

        void Changed() => Version++;

        public ItemStack Hand(string token) => hands.TryGetValue(token, out var i) ? i : ItemStack.Empty;

        void SetHand(string token, ItemStack stack)
        {
            if (stack.IsEmpty) hands.Remove(token); else hands[token] = stack;
            Changed();
        }

        public Backpack Worn(string token)
        {
            foreach (var p in Packs.Values) if (p.Wearer == token) return p;
            return null;
        }

        /// <summary>A new rucksack on <paramref name="wearer"/>'s back (or in the snow if null), filled with <paramref name="contents"/>.</summary>
        public Backpack AddPack(string wearer, float x, float y, float z, IEnumerable<ItemStack> contents = null)
        {
            var p = new Backpack(nextPack++) { Wearer = wearer, X = x, Y = y, Z = z };
            if (contents != null) foreach (var i in contents) p.Put(i);
            Packs[p.Id] = p;
            Changed();
            return p;
        }

        /// <summary>Throws away whatever is in a participant's rucksack and puts <paramref name="contents"/> there
        /// instead. For one caller only: a save being loaded, where the host has just built a starting rucksack for
        /// somebody who already has one on disk. Anything that will not fit is dropped rather than silently kept.</summary>
        public bool Refill(string token, IEnumerable<ItemStack> contents)
        {
            var pack = Worn(token);
            if (pack == null) return false;
            while (pack.Contents.Count > 0) pack.TakeAt(0);
            if (contents != null) foreach (var s in contents) if (!s.IsEmpty) pack.Put(s);
            Changed();
            return true;
        }

        /// <summary>How many objects may lie about the world at once. Every branch cut, every log split and every
        /// thing dropped used to stay for ever, and the whole list travels over the network on any change to any
        /// rucksack: a night of woodcutting is hundreds of entries nobody will ever pick up. The oldest go first,
        /// which on a night that walks forward is also the furthest away.</summary>
        public const int MaxLoose = 200;

        public LooseItem AddLoose(ItemStack stack, float x, float y, float z, float yaw = 0f)
        {
            var l = new LooseItem { Id = nextLoose++, Stack = stack, X = x, Y = y, Z = z, Yaw = yaw };
            Loose[l.Id] = l;
            Forget();
            Changed();
            return l;
        }

        /// <summary>Drops the oldest entries until the list is within <see cref="MaxLoose"/>. Ids only ever grow, so
        /// the smallest id is the oldest thing lying there.</summary>
        void Forget()
        {
            while (Loose.Count > MaxLoose)
            {
                int oldest = int.MaxValue;
                foreach (int id in Loose.Keys) if (id < oldest) oldest = id;
                if (oldest == int.MaxValue) return;
                Loose.Remove(oldest);
            }
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
            if (!Packs.TryGetValue(packId, out var p)) return PackResult.NoPack;
            if (!InReach(token, p, x, z)) return PackResult.TooFar;
            if (index < 0 || index >= p.Contents.Count) return PackResult.NoItem;
            var wanted = p.Contents[index];
            var held = Hand(token);
            if (!held.IsEmpty)
            {
                // an armful grows in the arms: logs join logs, matches join a half-empty box
                int room = held.Spec.MaxAmount - held.Amount;
                if (held.Id != wanted.Id || room <= 0) return PackResult.HandsFull;
                int moved = Math.Min(room, wanted.Amount);
                SetHand(token, held.With(held.Amount + moved));
                p.SetAt(index, wanted.With(wanted.Amount - moved));
                return PackResult.Ok;
            }
            SetHand(token, p.TakeAt(index));
            return PackResult.Ok;
        }

        /// <summary>Put what is in the hands into a pack.</summary>
        public PackResult Stow(string token, int packId, float x, float z)
        {
            var item = Hand(token);
            if (item.IsEmpty) return PackResult.HandsEmpty;
            if (!Packs.TryGetValue(packId, out var p)) return PackResult.NoPack;
            if (!InReach(token, p, x, z)) return PackResult.TooFar;
            if (!p.Put(item)) return PackResult.NoRoom;
            SetHand(token, ItemStack.Empty);
            return PackResult.Ok;
        }

        /// <summary>Drop what is in the hands into the snow.</summary>
        public PackResult DropHand(string token, float x, float y, float z, float yaw)
        {
            var item = Hand(token);
            if (item.IsEmpty) return PackResult.HandsEmpty;
            SetHand(token, ItemStack.Empty);
            AddLoose(item, x, y, z, yaw);
            return PackResult.Ok;
        }

        /// <summary>Pick a loose item up into the hands; another armful of firewood joins the one already carried.</summary>
        public PackResult PickUp(string token, int looseId, float x, float z)
        {
            if (!Loose.TryGetValue(looseId, out var l)) return PackResult.NoItem;
            if (Dist(x, z, l.X, l.Z) > Reach) return PackResult.TooFar;
            var held = Hand(token);
            if (!held.IsEmpty)
            {
                int room = held.Spec.MaxAmount - held.Amount;
                if (held.Id != l.Stack.Id || room <= 0) return PackResult.HandsFull;
                int moved = Math.Min(room, l.Stack.Amount);
                SetHand(token, held.With(held.Amount + moved));
                var left = l.Stack.With(l.Stack.Amount - moved);
                if (left.IsEmpty) Loose.Remove(looseId); else l.Stack = left;
                Changed();
                return PackResult.Ok;
            }
            Loose.Remove(looseId);
            SetHand(token, l.Stack);
            return PackResult.Ok;
        }

        /// <summary>Something handed over a counter: it goes straight into the worn rucksack, and into the hands when the
        /// rucksack is full or is not being worn.</summary>
        public PackResult Receive(string token, ItemStack stack)
        {
            if (stack.IsEmpty) return PackResult.NoItem;
            var pack = Worn(token);
            if (pack != null && pack.Put(stack)) { Changed(); return PackResult.Ok; }
            return GiveHand(token, stack);
        }

        /// <summary>Force something into the hands (the result of work: a branch just cut off, logs just split).</summary>
        public PackResult GiveHand(string token, ItemStack stack)
        {
            if (stack.IsEmpty) return PackResult.NoItem;
            var held = Hand(token);
            if (held.IsEmpty) { SetHand(token, stack); return PackResult.Ok; }
            int room = held.Spec.MaxAmount - held.Amount;
            if (held.Id != stack.Id || room <= 0) return PackResult.HandsFull;
            SetHand(token, held.With(held.Amount + Math.Min(room, stack.Amount)));
            return PackResult.Ok;
        }

        /// <summary>The usable item of this kind the participant has at hand: in the hands first, then in the worn rucksack.
        /// <paramref name="usable"/> skips damp matches.</summary>
        public bool Has(string token, ItemId id, bool usable = false)
        {
            var held = Hand(token);
            if (held.Id == id && (!usable || !held.Damp)) return true;
            var pack = Worn(token);
            return pack != null && pack.IndexOf(id, usable) >= 0;
        }

        /// <summary>The tool the participant holds (a saw and an axe are used with both hands, so only what is in the hands counts).</summary>
        public ToolKind HeldTool(string token) => Hand(token).Spec.Tool;

        /// <summary>Spend <paramref name="amount"/> of an item: out of the hands first, then out of the worn rucksack.</summary>
        public bool Spend(string token, ItemId id, int amount = 1, bool usable = false)
        {
            var held = Hand(token);
            if (held.Id == id && (!usable || !held.Damp))
            {
                int take = Math.Min(amount, held.Amount);
                SetHand(token, held.With(held.Amount - take));
                amount -= take;
            }
            var pack = Worn(token);
            while (amount > 0 && pack != null)
            {
                int i = pack.IndexOf(id, usable);
                if (i < 0) break;
                var s = pack.Contents[i];
                int take = Math.Min(amount, s.Amount);
                pack.SetAt(i, s.With(s.Amount - take));
                amount -= take;
                Changed();
            }
            return amount == 0;
        }

        /// <summary>Matches carried in the hands take the weather: a blizzard drives snow into the box, open water on the brook soaks it at once.
        /// What lies in the rucksack stays dry; by the fire everything dries out.</summary>
        public void Weather(string token, float dt, bool storm, bool water, bool fire)
        {
            var held = Hand(token);
            float delta = 0f;
            if (held.Spec.Soaks)
            {
                if (water) delta += 30f * dt;
                else if (storm) delta += 3.5f * dt;
            }
            if (fire) delta -= 9f * dt;
            if (!held.IsEmpty && held.Spec.Soaks && Math.Abs(delta) > 1e-6f)
            {
                // wetness is stored as a byte, so keep the fraction until it adds up to a whole point
                float carry = (soak.TryGetValue(token, out var c) ? c : 0f) + delta;
                int whole = (int)(carry > 0 ? Math.Floor(carry) : Math.Ceiling(carry));
                soak[token] = carry - whole;
                if (whole != 0)
                {
                    var next = held.Wetter(whole);
                    if (next.Wet != held.Wet) SetHand(token, next);
                }
            }
            if (!fire) return;
            var pack = Worn(token);
            if (pack == null) return;
            for (int i = 0; i < pack.Contents.Count; i++)
            {
                var s = pack.Contents[i];
                if (!s.Spec.Soaks || s.Wet == 0) continue;
                var next = s.Wetter(Math.Min(-1f, -9f * dt));
                if (next.Wet != s.Wet) { pack.SetAt(i, next); Changed(); }
            }
        }

        /// <summary>A participant leaves: whatever they carry stays on the slope where they were.</summary>
        public void Leave(string token, float x, float y, float z)
        {
            var p = Worn(token);
            if (p != null) { p.Wearer = null; p.X = x; p.Y = y; p.Z = z; Changed(); }
            if (!Hand(token).IsEmpty) DropHand(token, x + .4f, y, z, 0f);
        }

        /// <summary>Everything a participant carries: the worn pack with its contents and what is in the hands.</summary>
        public float CarriedKg(string token)
        {
            var p = Worn(token);
            return (p != null ? p.Kg : 0f) + Hand(token).Kg;
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
