using System;
using System.Collections.Generic;
using System.Text;

namespace Height1079.Core
{
    /// <summary>One line of the hire board: the object that goes into the rucksack, the piece of
    /// <see cref="Gear"/> it counts as for <see cref="Ascent.Required"/>, and the price of hiring it for the ascent.</summary>
    public readonly struct Hire
    {
        public readonly ItemId Item;
        /// <summary>Which bit of the checked kit this object is. Exactly one flag.</summary>
        public readonly Gear Piece;
        /// <summary>Roubles for one day — the counter charges by the day and nobody rents a harness for an hour.</summary>
        public readonly int Roubles;
        /// <summary>Russian, as it is written on the counter's own list.</summary>
        public readonly string Name;

        public Hire(ItemId item, Gear piece, int roubles)
        { Item = item; Piece = piece; Roubles = roubles; Name = Items.Spec(item).Name; }

        public bool IsEmpty => Item == ItemId.None;
        public float Kg => Items.Spec(Item).Kg;
        public float Litres => Items.Spec(Item).Litres;
        /// <summary>«Кошки, 12 зубьев — 500 ₽» — one line of the board.</summary>
        public string Line => $"{Name} — {Roubles} ₽";
    }

    /// <summary>The hire counter at the bottom of the mountain, and the only way an unprepared visitor gets past
    /// Pastukhov rocks. Everything <see cref="Ascent.Required"/> asks for is on this board and nothing else is.
    ///
    /// Prices are the ones the hire shops of Azau and Terskol actually charge by the day in the 2020s: crampons and a
    /// down jacket are the expensive lines, a balaclava is pocket money. The whole kit at the counter's own bundle
    /// price is <see cref="SetRoubles"/> — well over half of what a visitor comes up with
    /// (<see cref="Wallet.StartRoubles"/>), which is the trade the game is about: the mountain is cheap, being ready
    /// for it is not.
    ///
    /// No engine here: the same board runs in Unity and in the dotnet tests.</summary>
    public static class Rental
    {
        /// <summary>How close to the stand an order can be placed, metres. The same reach as a café counter, so the
        /// two windows feel like the same gesture.</summary>
        public const float CounterReach = 3f;
        /// <summary>Walk further than this and the window closes.</summary>
        public const float LeaveReach = 5f;
        /// <summary>Seconds the man behind the counter takes to find one thing in your size.</summary>
        public const float ServeSeconds = 3f;

        static readonly Hire[] board =
        {
            new Hire(ItemId.Crampons, Gear.Crampons, 500),
            new Hire(ItemId.IceAxe, Gear.IceAxe, 350),
            new Hire(ItemId.Harness, Gear.Harness, 450),
            new Hire(ItemId.Helmet, Gear.Helmet, 250),
            new Hire(ItemId.Goggles, Gear.Goggles, 350),
            new Hire(ItemId.SnowMask, Gear.Mask, 300),
            new Hire(ItemId.DownJacket, Gear.DownJacket, 900),
            new Hire(ItemId.Balaclava, Gear.Balaclava, 150),
            new Hire(ItemId.HighMittens, Gear.Mittens, 300),
            new Hire(ItemId.SpareGloves, Gear.SpareGloves, 200),
            new Hire(ItemId.Thermos, Gear.Thermos, 250),
            new Hire(ItemId.Headlamp, Gear.Headlamp, 250),
            new Hire(ItemId.SpaceBlanket, Gear.SpaceBlanket, 200),
        };

        public static int Count => board.Length;
        public static Hire At(int index) => index >= 0 && index < board.Length ? board[index] : default;

        /// <summary>The whole board, in the order it hangs on the wall.</summary>
        public static Hire[] Board()
        {
            var list = new Hire[board.Length];
            Array.Copy(board, list, list.Length);
            return list;
        }

        /// <summary>What every line costs added up — what a visitor pays picking the kit apart, piece by piece.</summary>
        public static int PieceByPieceRoubles
        {
            get { int sum = 0; foreach (var h in board) sum += h.Roubles; return sum; }
        }

        /// <summary>The counter's own price for the lot. Twelve per cent off the sum of the lines, because that is how
        /// a hire shop sells a kit, and because the game wants the whole kit to be one decision.</summary>
        public static int SetRoubles => 3900;

        public static float SetKg { get { float v = 0; foreach (var h in board) v += h.Kg; return v; } }
        public static float SetLitres { get { float v = 0; foreach (var h in board) v += h.Litres; return v; } }

        /// <summary>Which piece of the checked kit an object counts as, or <see cref="Gear.None"/> for anything else.</summary>
        public static Gear PieceOf(ItemId id)
        {
            foreach (var h in board) if (h.Item == id) return h.Piece;
            return Gear.None;
        }

        /// <summary>The object that stands for one piece of the kit, or <see cref="ItemId.None"/>.</summary>
        public static ItemId ItemOf(Gear piece)
        {
            foreach (var h in board) if (h.Piece == piece) return h.Item;
            return ItemId.None;
        }

        public static int Roubles(ItemId id)
        {
            foreach (var h in board) if (h.Item == id) return h.Roubles;
            return 0;
        }

        /// <summary>What a pile of things adds up to as <see cref="Gear"/>. This is the one place the rules meet the
        /// rucksack: having the object is having the gear, and nothing else counts.</summary>
        public static Gear GearOf(IEnumerable<ItemStack> items)
        {
            var have = Gear.None;
            if (items == null) return have;
            foreach (var s in items) have |= PieceOf(s.Id);
            return have;
        }

        /// <summary>Everything one participant is carrying, as <see cref="Gear"/>: the worn rucksack and the hands.</summary>
        public static Gear Carried(PackWorld packs, string token)
        {
            if (packs == null) return Gear.None;
            var have = PieceOf(packs.Hand(token).Id);
            var pack = packs.Worn(token);
            if (pack != null) have |= GearOf(pack.Contents);
            return have;
        }

        /// <summary>What is still missing from a rucksack, as a price: what the counter would charge to finish the kit
        /// line by line.</summary>
        public static int RestRoubles(Gear have)
        {
            int sum = 0;
            foreach (var h in board) if ((have & h.Piece) == 0) sum += h.Roubles;
            return sum;
        }

        /// <summary>The bundle price for everything still missing — the same twelve per cent off that the whole kit
        /// gets, pro rata. With an empty rucksack it is <see cref="SetRoubles"/>.</summary>
        public static int BundleRoubles(Gear have)
        {
            int rest = RestRoubles(have);
            if (rest <= 0) return 0;
            int full = PieceByPieceRoubles;
            return full <= 0 ? rest : (int)Math.Round(rest * (double)SetRoubles / full);
        }

        /// <summary>A set of pieces written out in Russian: «кошки, ледоруб, каска».</summary>
        public static string Titles(Gear set)
        {
            var sb = new StringBuilder();
            foreach (var h in board)
            {
                if ((set & h.Piece) == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Ascent.GearTitle(h.Piece));
            }
            return sb.ToString();
        }

        /// <summary>The board over the counter, one line per piece.</summary>
        public static string BoardText()
        {
            var sb = new StringBuilder();
            foreach (var h in board)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(h.Line);
            }
            return sb.ToString();
        }
    }
}
