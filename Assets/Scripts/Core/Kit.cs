using System;

namespace Height1079.Core
{
    /// <summary>What a person carries, the PEAK way: three slots at hand and a rucksack on the back. The slots are
    /// the three things you can get at without stopping — a number key puts one of them in the hands — and the
    /// rucksack is everything else, opened with Tab and gone through by hand. One of the slots may be "in the
    /// hands" (<see cref="Selected"/>); the item there is the one that is drawn in the picture and the one a click
    /// uses. Nothing here knows what using a thing does: eating, lighting and the rest are whoever owns the kit.
    ///
    /// The rucksack is the game's own <see cref="Backpack"/> — same volume rule, same items — so a kit filled from
    /// <see cref="Items.StarterFor(int)"/> weighs what the night's does. Engine-free, tested in dotnet.</summary>
    public sealed class Kit
    {
        public const int Slots = 3;
        /// <summary>No slot: empty hands.</summary>
        public const int Nothing = -1;

        readonly ItemStack[] slots = new ItemStack[Slots];
        public readonly Backpack Pack;
        /// <summary>The slot whose item is in the hands, or <see cref="Nothing"/>.</summary>
        public int Selected { get; private set; } = Nothing;

        public Kit(Backpack pack = null) { Pack = pack ?? new Backpack(0); }

        public ItemStack Slot(int i) => i >= 0 && i < Slots ? slots[i] : ItemStack.Empty;
        /// <summary>What is in the hands right now — empty unless a slot with something in it is selected.</summary>
        public ItemStack InHand => Slot(Selected);
        public bool HandsEmpty => InHand.IsEmpty;
        /// <summary>The slot holding the first item of this kind, or <see cref="Nothing"/>.</summary>
        public int SlotOf(ItemId id)
        {
            for (int i = 0; i < Slots; i++) if (slots[i].Id == id) return i;
            return Nothing;
        }
        public int Taken { get { int n = 0; for (int i = 0; i < Slots; i++) if (!slots[i].IsEmpty) n++; return n; } }
        public int FreeSlot()
        {
            for (int i = 0; i < Slots; i++) if (slots[i].IsEmpty) return i;
            return Nothing;
        }

        /// <summary>The number keys. The slot pressed goes into the hands; pressed again it is put away; an empty
        /// slot is empty hands.</summary>
        public void Select(int i)
        {
            Selected = i < 0 || i >= Slots || slots[i].IsEmpty || Selected == i ? Nothing : i;
        }

        public void PutAway() => Selected = Nothing;

        /// <summary>Put a thing straight into a slot: how the kit is filled at the start. Fails on a taken slot.</summary>
        public bool Give(int slot, ItemStack stack)
        {
            if (slot < 0 || slot >= Slots || stack.IsEmpty || !slots[slot].IsEmpty) return false;
            slots[slot] = stack;
            return true;
        }

        /// <summary>Put a thing wherever it goes: a free slot first, else the rucksack. False if there is room in neither.</summary>
        public bool Give(ItemStack stack)
        {
            if (stack.IsEmpty) return false;
            int free = FreeSlot();
            if (free != Nothing) { slots[free] = stack; return true; }
            return Pack.Put(stack);
        }

        /// <summary>Take a thing out of the rucksack into a slot — the free one, or the one asked for. A slot that
        /// already holds something swaps: its item goes into the rucksack in exchange. If the exchange will not fit
        /// (a saw for a box of matches in a full sack) nothing moves.</summary>
        public bool Draw(int packIndex, int slot = Nothing)
        {
            if (packIndex < 0 || packIndex >= Pack.Contents.Count) return false;
            if (slot == Nothing) slot = FreeSlot();
            if (slot == Nothing) return false;
            if (slot < 0 || slot >= Slots) return false;
            var was = slots[slot];
            var item = Pack.TakeAt(packIndex);
            if (!was.IsEmpty && !Pack.Put(was))
            {
                // put it back where it came from: the list order is what the player sees
                PutBackAt(packIndex, item);
                return false;
            }
            slots[slot] = item;
            return true;
        }

        /// <summary>Put what a slot holds into the rucksack. False on an empty slot or a full sack; the slot then
        /// keeps its item. The hands are emptied if that slot was in them.</summary>
        public bool Stow(int slot)
        {
            if (slot < 0 || slot >= Slots || slots[slot].IsEmpty) return false;
            if (!Pack.Put(slots[slot])) return false;
            slots[slot] = ItemStack.Empty;
            if (Selected == slot) Selected = Nothing;
            return true;
        }

        /// <summary>One unit of a slot is used up — a bar of chocolate eaten, a match struck. A stack of one goes
        /// entirely, and hands holding it are left empty. Returns what was used, empty if the slot had nothing.</summary>
        public ItemStack Consume(int slot)
        {
            if (slot < 0 || slot >= Slots || slots[slot].IsEmpty) return ItemStack.Empty;
            var s = slots[slot];
            slots[slot] = s.With(s.Amount - 1);
            if (slots[slot].IsEmpty && Selected == slot) Selected = Nothing;
            return s.With(1);
        }

        /// <summary>Everything at hand and on the back, kg — the rucksack's own canvas included.</summary>
        public float Kg
        {
            get { float s = Pack.Kg; for (int i = 0; i < Slots; i++) s += slots[i].Kg; return s; }
        }

        void PutBackAt(int index, ItemStack item)
        {
            // Backpack only appends; rebuild the tail so the item lands back at its index
            var tail = new ItemStack[Math.Max(0, Pack.Contents.Count - index)];
            for (int i = tail.Length - 1; i >= 0; i--) tail[i] = Pack.TakeAt(index + i);
            Pack.Put(item);
            foreach (var t in tail) Pack.Put(t);
        }
    }
}
