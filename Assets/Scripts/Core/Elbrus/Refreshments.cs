using System;
using System.Text;

namespace Height1079.Core
{
    /// <summary>What a café of Prielbrusye serves. Kabardino-Balkarian and Caucasian food as it is really sold on the
    /// southern slope — khychiny, shurpa, shashlyk — plus the pizza and the coffee every station café above 3 000 m has.
    /// Prices are the 2020s resort ones (a soup about 450 ₽, a khychin about 250 ₽, a shashlyk about 700 ₽).</summary>
    public enum DishId : byte
    {
        None = 0,
        Khychin, Shurpa, Kharcho, Lagman, Shashlyk, Pizza, Hachapuri, Plov,
        Ayran, Tea, Coffee, MulledWine, HotChocolate,
    }

    /// <summary>Where a dish sits on the board: the hot kitchen, the oven, the grill or the bar.</summary>
    public enum DishKind : byte { Soup, Baked, Grill, Drink }

    /// <summary>One line of the menu: the Russian name as it is chalked on the board, the price in roubles, how long it
    /// takes to eat, what it gives back, and what it becomes if it is taken away in foil or in a cup.</summary>
    public readonly struct Dish
    {
        public readonly DishId Id;
        public readonly DishKind Kind;
        /// <summary>Russian, exactly as it goes on the chalk board and into the HUD.</summary>
        public readonly string Name;
        /// <summary>Price of one portion, roubles.</summary>
        public readonly int Roubles;
        /// <summary>Seconds spent at the table (the hiker stands still for that long).</summary>
        public readonly float Seconds;
        /// <summary>Points of <see cref="Participant.Heat"/> the portion gives back.</summary>
        public readonly float Warmth;
        /// <summary>Points of <see cref="Participant.Hands"/>: a hot bowl or a cup brings the fingers back faster than food does.</summary>
        public readonly float Strength;
        /// <summary>Points of <see cref="Participant.Clarity"/>; mulled wine warms but takes some of it away.</summary>
        public readonly float Clarity;
        /// <summary>One portion as carried out of the café: kilograms and litres in the rucksack.</summary>
        public readonly float Kg, Litres;
        /// <summary>What goes into the rucksack when the portion is taken away, or <see cref="ItemId.None"/> for what can
        /// only be eaten at the table (a bowl of soup, a plate of plov).</summary>
        public readonly ItemId TakeAway;
        /// <summary>Short line for the HUD once it is eaten («Горячо.»).</summary>
        public readonly string Note;

        public Dish(DishId id, DishKind kind, string name, int roubles, float seconds, float warmth, float strength, float clarity,
            float kg, float litres, ItemId takeAway, string note)
        {
            Id = id; Kind = kind; Name = name; Roubles = roubles; Seconds = seconds;
            Warmth = warmth; Strength = strength; Clarity = clarity;
            Kg = kg; Litres = litres; TakeAway = takeAway; Note = note;
        }

        public bool IsEmpty => Id == DishId.None;
        /// <summary>Can this portion leave the café in foil, in a box or in a paper cup?</summary>
        public bool Portable => TakeAway != ItemId.None;
        /// <summary>«Шурпа из баранины — 450 ₽» — one line of the board.</summary>
        public string Line => $"{Name} — {Roubles} ₽";
    }

    /// <summary>What one portion actually did to a participant: the numbers after clamping, so the HUD can say it out loud.</summary>
    public readonly struct Meal
    {
        public readonly DishId Id;
        public readonly string Name, Note;
        public readonly float Warmth, Strength, Clarity, Seconds;
        public readonly int Roubles;

        public Meal(DishId id, string name, string note, float warmth, float strength, float clarity, float seconds, int roubles)
        { Id = id; Name = name; Note = note; Warmth = warmth; Strength = strength; Clarity = clarity; Seconds = seconds; Roubles = roubles; }

        public bool IsEmpty => Id == DishId.None;
        /// <summary>«Шурпа из баранины. Горячо.»</summary>
        public string Text => IsEmpty ? "" : $"{Name}. {Note}";
    }

    /// <summary>The money a hiker came up the mountain with. Roubles only, no change, no cards: the cafés above Krugozor
    /// take cash and nothing else.</summary>
    public struct Wallet
    {
        /// <summary>What one person sets out with — enough for a good few meals and the ropeway back down.</summary>
        public const int StartRoubles = 7000;

        public int Roubles;

        public static Wallet Start() => new Wallet { Roubles = StartRoubles };

        public bool CanAfford(int price) => price >= 0 && Roubles >= price;

        /// <summary>Takes the price out of the purse; false (and nothing taken) when there is not enough.</summary>
        public bool Pay(int price)
        {
            if (!CanAfford(price)) return false;
            Roubles -= price;
            return true;
        }

        /// <summary>Gives the money back (an order given up half-way).</summary>
        public void Refund(int price) { if (price > 0) Roubles += price; }

        /// <summary>«6 250 ₽» — for the HUD.</summary>
        public string Text => Roubles.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ") + " ₽";
    }

    /// <summary>The rules of eating on the slope: the menu of a mountain café and what a hot portion gives back to a
    /// participant. No engine here — the same numbers run in Unity and in the dotnet tests.</summary>
    public static class Refreshments
    {
        /// <summary>How close to the counter an order can be placed, metres.</summary>
        public const float CounterReach = 2.5f;
        /// <summary>Walk further than this from the counter and the order is given up (the money comes back).</summary>
        public const float LeaveReach = 4f;

        static readonly Dish[] menu =
        {
            new Dish(DishId.None, DishKind.Drink, "—", 0, 0f, 0f, 0f, 0f, 0f, 0f, ItemId.None, ""),
            new Dish(DishId.Khychin, DishKind.Baked, "Хычин с картошкой и сыром", 250, 40f, 16f, 10f, 8f, .25f, .5f, ItemId.Khychin, "Тонкий, горячий, в масле."),
            new Dish(DishId.Shurpa, DishKind.Soup, "Шурпа из баранины", 450, 60f, 30f, 20f, 10f, .5f, .5f, ItemId.None, "Горячо. Отпускает."),
            new Dish(DishId.Kharcho, DishKind.Soup, "Суп харчо", 420, 60f, 28f, 18f, 10f, .5f, .5f, ItemId.None, "Острое тепло изнутри."),
            new Dish(DishId.Lagman, DishKind.Soup, "Лагман", 480, 65f, 26f, 16f, 10f, .55f, .6f, ItemId.None, "Плотно и горячо."),
            new Dish(DishId.Shashlyk, DishKind.Grill, "Шашлык из баранины", 700, 90f, 22f, 14f, 9f, .4f, .9f, ItemId.Shashlyk, "С углей, с луком."),
            new Dish(DishId.Pizza, DishKind.Baked, "Пицца", 650, 70f, 18f, 10f, 8f, .45f, 3f, ItemId.PizzaBox, "Из печи, ещё тянется."),
            new Dish(DishId.Hachapuri, DishKind.Baked, "Хачапури по-аджарски", 550, 60f, 20f, 12f, 8f, .45f, 1.2f, ItemId.Khychin, "Сыр, яйцо, масло."),
            new Dish(DishId.Plov, DishKind.Grill, "Плов", 500, 70f, 20f, 12f, 9f, .5f, .8f, ItemId.None, "Тяжело и тепло."),
            new Dish(DishId.Ayran, DishKind.Drink, "Айран", 150, 25f, 2f, 3f, 6f, .35f, .5f, ItemId.None, "Холодный, солоноватый."),
            new Dish(DishId.Tea, DishKind.Drink, "Чай с чабрецом", 200, 30f, 14f, 22f, 8f, .35f, .5f, ItemId.HotCup, "Руки отходят о стакан."),
            new Dish(DishId.Coffee, DishKind.Drink, "Кофе", 250, 30f, 12f, 20f, 14f, .3f, .4f, ItemId.HotCup, "В голове проясняется."),
            new Dish(DishId.MulledWine, DishKind.Drink, "Глинтвейн", 400, 35f, 24f, 20f, -6f, .35f, .5f, ItemId.HotCup, "Тепло, и немного плывёт."),
            new Dish(DishId.HotChocolate, DishKind.Drink, "Горячий шоколад", 300, 30f, 16f, 22f, 6f, .35f, .5f, ItemId.HotCup, "Сладко и густо."),
        };

        /// <summary>The whole board, in the order it is chalked up (index 0 is the empty dish).</summary>
        public static int Count => menu.Length;

        public static Dish Get(DishId id) => (int)id > 0 && (int)id < menu.Length ? menu[(int)id] : menu[0];

        /// <summary>The dishes in board order, without the empty one.</summary>
        public static Dish[] Menu()
        {
            var list = new Dish[menu.Length - 1];
            Array.Copy(menu, 1, list, 0, list.Length);
            return list;
        }

        /// <summary>The chalk board over the counter: one line per dish, at most <paramref name="lines"/> of them
        /// (0 = all). Used both by the café prefab and by the order window.</summary>
        public static string Board(int lines = 0)
        {
            int n = lines <= 0 ? menu.Length - 1 : Math.Min(lines, menu.Length - 1);
            var sb = new StringBuilder();
            for (int i = 1; i <= n; i++)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(menu[i].Line);
            }
            if (n < menu.Length - 1) sb.Append("\n…и напитки");
            return sb.ToString();
        }

        static float Clamp(float v) => Math.Max(0f, Math.Min(100f, v));

        /// <summary>What one portion is worth to a participant, before it is applied (for the HUD and for tests).</summary>
        public static Meal Effect(DishId id)
        {
            var d = Get(id);
            if (d.IsEmpty) return default;
            return new Meal(d.Id, d.Name, d.Note, d.Warmth, d.Strength, d.Clarity, d.Seconds, d.Roubles);
        }

        /// <summary>Eats one portion: warms the participant, brings the hands back and clears (or clouds) the head.
        /// Returns what actually changed after clamping to 0…100 — a hiker who is already warm gains less.
        /// Nothing happens to someone whose night is already decided.</summary>
        public static Meal Eat(DishId id, Participant p)
        {
            var d = Get(id);
            if (d.IsEmpty || p == null || p.Outcome != Outcome.None) return default;
            float heat = Clamp(p.Heat + d.Warmth), hands = Clamp(p.Hands + d.Strength), clarity = Clamp(p.Clarity + d.Clarity);
            var meal = new Meal(d.Id, d.Name, d.Note, heat - p.Heat, hands - p.Hands, clarity - p.Clarity, d.Seconds, d.Roubles);
            p.Heat = heat; p.Hands = hands; p.Clarity = clarity;
            return meal;
        }

        /// <summary>A short Russian line for the protocol: «Шурпа из баранины. Горячо.»</summary>
        public static string Served(DishId id) => Effect(id).Text;
    }
}
