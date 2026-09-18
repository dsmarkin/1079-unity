using System;

namespace Height1079.Core
{
    /// <summary>What kind of roof a night is spent under. The order is the order of comfort, and every rule below
    /// reads <see cref="Bunk.Comfort"/> rather than this — the kind is for the HUD and for the price board.</summary>
    public enum BunkKind : byte
    {
        /// <summary>Your own двойка on the snow. The reference every other line is measured against; the night
        /// itself is <see cref="Camp.Sleep"/>.</summary>
        Tent = 0,
        /// <summary>A вагончик of the rescuers on the moraine. Diesel, four bunks, and nobody takes money for it.</summary>
        RescueWagon,
        /// <summary>A бочка on the Гарабаши bench: six places, a stove and a kettle.</summary>
        Barrel,
        /// <summary>A приют — a heated hut with a kitchen and a drying room.</summary>
        Hut,
        /// <summary>A capsule hotel. Warm, dry, and priced like a hotel.</summary>
        Capsule,
    }

    /// <summary>One line of the price board: a bunk somewhere on the southern slope, what a night in it costs and
    /// what kind of night it is.</summary>
    public readonly struct Bunk
    {
        /// <summary>The <see cref="Elbrus.Poi"/> id where there is one, so the runtime can put the счёт on the
        /// building the player is standing in.</summary>
        public readonly string Id;
        /// <summary>Russian, as it is written on the board.</summary>
        public readonly string Name;
        public readonly BunkKind Kind;
        /// <summary>Metres above sea level. This is what <see cref="Ascent.Acclimatise"/> is handed.</summary>
        public readonly float Ele;
        /// <summary>Roubles for one bunk for one night. 0 at the rescuers' wagons, and that is not a rounding.</summary>
        public readonly int Roubles;
        /// <summary>How many sleep there.</summary>
        public readonly int Beds;
        /// <summary>A diesel stove: the room is warm, and things left out in it dry.</summary>
        public readonly bool Diesel;
        /// <summary>A kitchen: snow is melted for you, and the thermos is filled without spending your own gas.</summary>
        public readonly bool Kitchen;
        /// <summary>A сушилка — a room with hot pipes where the boots go overnight.</summary>
        public readonly bool DryingRoom;
        /// <summary>0 (a tent on the snow) … 1 (a bed in a capsule). Everything the night gives back is a function of
        /// this one number, so the board can grow a line without growing a rule.</summary>
        public readonly float Comfort;

        public Bunk(string id, string name, BunkKind kind, float ele, int roubles, int beds,
            bool diesel, bool kitchen, bool dryingRoom, float comfort)
        {
            Id = id; Name = name; Kind = kind; Ele = ele; Roubles = Math.Max(0, roubles); Beds = Math.Max(1, beds);
            Diesel = diesel; Kitchen = kitchen; DryingRoom = dryingRoom; Comfort = Ascent.Clamp01(comfort);
        }

        public bool IsEmpty => string.IsNullOrEmpty(Id);
        /// <summary>Nobody takes money for a bunk in a rescuers' wagon.</summary>
        public bool Free => Roubles == 0;
        /// <summary>Anything that melts snow: a kitchen, or a stove you can put a pot on.</summary>
        public bool MeltsSnow => Kitchen || Diesel;
        /// <summary>Wet things come out dry: a drying room, or a diesel stove and a night.</summary>
        public bool Dries => DryingRoom || Diesel;

        /// <summary>«Бочки · Гара-Баши, 3710 м — 2000 ₽» — one line of the board.</summary>
        public string Line => IsEmpty ? "" : Free
            ? $"{Name}, {Ele:0} м — бесплатно"
            : $"{Name}, {Ele:0} м — {Roubles} ₽";
    }

    /// <summary>What one night under a roof did, after everything was clamped.</summary>
    public readonly struct Night
    {
        public readonly BunkKind Kind;
        public readonly string Name;
        /// <summary>What it cost. The caller pays it; this only reports it.</summary>
        public readonly int Roubles;
        /// <summary>Acclimatisation woken up with, and what the night added (or took away).</summary>
        public readonly float Acclim, Gain;
        /// <summary>What the legs come back to, 0…1 (<see cref="Lodging.RestFactor"/>).</summary>
        public readonly float Rest;
        /// <summary>Frostbite up to this much thawed out (<see cref="Lodging.ThawBelow"/>).</summary>
        public readonly float Thaw;
        public readonly bool Dried, ThermosFilled;
        /// <summary>Russian, one line.</summary>
        public readonly string Note;

        public Night(BunkKind kind, string name, int roubles, float acclim, float gain, float rest, float thaw,
            bool dried, bool thermosFilled, string note)
        {
            Kind = kind; Name = name; Roubles = roubles; Acclim = acclim; Gain = gain; Rest = rest; Thaw = thaw;
            Dried = dried; ThermosFilled = thermosFilled; Note = note;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Name);
    }

    /// <summary>A night in a hut, as against a night in your own tent.
    ///
    /// <see cref="Camp"/> already knows what a двойка on the snow is worth. A приют is a different thing: it is warm,
    /// it is dry, there is a stove and a kitchen and usually a сушилка, and the diesel runs all night. So the body
    /// comes back further, the boots come out dry, the thermos is filled without spending your own gas — and there is
    /// a man at the door who wants money for the bunk. Everywhere except the вагончики of the rescuers on the
    /// moraine, where there never is.
    ///
    /// <b>The acclimatisation is not re-invented here.</b> Climb high, sleep low is one rule and it lives in
    /// <see cref="Ascent.Acclimatise"/>; this file gets it by calling <see cref="Camp.Sleep"/> with the hut's height,
    /// exactly as a tent night does. A bunk at 3 710 m is worth what any night at 3 710 m is worth, and a player who
    /// pays 6 000 ₽ for a capsule buys a better night, not a better mountain.
    ///
    /// Where the places come from (docs/ELBRUS.md, OSM): бочки 3 710, «Нацпарк» 3 900, LeapRus 3 912, Приют 11 /
    /// Дизель-хат 4 050 and Приют 88 4 100, вагончики спасателей 4 100–4 200. Prices are the resort ones of the
    /// 2020s per bunk per night; <see cref="Bunk.Comfort"/> and everything derived from it is balance.</summary>
    public static class Lodging
    {
        /// <summary>How close to the door you have to be to take a bunk, metres. Same reach as the hire counter.</summary>
        public const float DoorReach = 4f;

        /// <summary>What a night gives the legs back at the two ends of the board: a tent on the snow, and a bed.
        /// Between them it is linear in <see cref="Bunk.Comfort"/>.</summary>
        public const float RestFloor = .55f, RestSpan = .45f;

        /// <summary>How much further frostbite thaws under a roof than in a bag (<see cref="Camp.ThawBelow"/>). Real
        /// frostbite still does not come back — a hut is warm, not a hospital.</summary>
        public const float ThawSpan = .30f;

        static readonly Bunk[] board =
        {
            new Bunk("barrels",  "Бочки · Гара-Баши",        BunkKind.Barrel,      3710f, 2000,  6, true,  true,  false, .55f),
            new Bunk("natspark", "Приют «Нацпарк»",           BunkKind.Hut,         3900f, 1500, 20, true,  true,  true,  .60f),
            new Bunk("leaprus",  "LeapRus",                   BunkKind.Capsule,     3912f, 6000, 16, true,  true,  true,  1.00f),
            new Bunk("priut11",  "Приют 11 (Дизель-хат)",     BunkKind.Hut,         4050f, 2200, 24, true,  true,  true,  .65f),
            new Bunk("priut88",  "Приют 88",                  BunkKind.Hut,         4100f, 2000, 12, true,  true,  false, .55f),
            new Bunk("rescuers", "Вагончик спасателей",       BunkKind.RescueWagon, 4150f,    0,  4, true,  false, false, .35f),
        };

        /// <summary>Your own двойка, as a line of the same board: the reference the huts are better than. The night
        /// itself is <see cref="Camp.Sleep"/>; this row is what the menu compares against and what
        /// <see cref="RestFactor"/> is anchored on.</summary>
        public static readonly Bunk Tent =
            new Bunk("tent", "Своя палатка", BunkKind.Tent, 0f, 0, 2, false, false, false, 0f);

        public static int Count => board.Length;
        public static Bunk At(int index) => index >= 0 && index < board.Length ? board[index] : default;

        /// <summary>The board, in the order it hangs — by height.</summary>
        public static Bunk[] Board()
        {
            var list = new Bunk[board.Length];
            Array.Copy(board, list, list.Length);
            return list;
        }

        /// <summary>The bunk with this id, or an empty one. Ids match <see cref="Elbrus.Pois"/> where a POI exists.</summary>
        public static Bunk Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return default;
            if (string.Equals(id, Tent.Id, StringComparison.Ordinal)) return Tent;
            foreach (var b in board) if (string.Equals(b.Id, id, StringComparison.Ordinal)) return b;
            return default;
        }

        /// <summary>The cheapest bunk on the board, which is always the rescuers' wagon and always free.</summary>
        public static Bunk Cheapest()
        {
            var best = board[0];
            foreach (var b in board) if (b.Roubles < best.Roubles) best = b;
            return best;
        }

        /// <summary>The board over the door.</summary>
        public static string BoardText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var b in board)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(b.Line);
            }
            return sb.ToString();
        }

        // ── what a night here is worth ────────────────────────────────────────────────────────────────────

        /// <summary>What the strength bar comes back <em>to</em> after one night, 0…1. A tent leaves a little over
        /// half of it; a bed in a heated room gives all of it. The runtime raises its own bar to this and never
        /// lowers it.</summary>
        public static float RestFactor(in Bunk b) => RestFloor + RestSpan * Ascent.Clamp01(b.Comfort);

        /// <summary>Frostbite below this much is gone in the morning. In a tent it is <see cref="Camp.ThawBelow"/>;
        /// under a roof with the diesel running it is further, but never all the way — tissue that is gone is gone,
        /// and no night fixes it.</summary>
        public static float ThawBelow(in Bunk b) => Camp.ThawBelow + ThawSpan * Ascent.Clamp01(b.Comfort);

        /// <summary>The camp is a thing of the southern slope, and so is the hut. The night of 1–2 February is one
        /// night: nobody takes a bunk on Холатчахль.</summary>
        public static bool AllowedIn(Place place) => Camp.AllowedIn(place);

        /// <summary>One night in this bunk. The whole of climb-high-sleep-low is <see cref="Camp.Sleep"/> at the
        /// bunk's own height — this file does not own that rule and does not repeat it. What it adds is the roof:
        /// frostbite thaws further, the thermos is filled off the hut's stove rather than your own gas, and the legs
        /// come back to <see cref="RestFactor"/> instead of what a tent gives.
        ///
        /// <paramref name="burner"/> is there for a hut whose diesel has run out and a party with their own gas; a
        /// roof with a stove already melts snow and it changes nothing.
        ///
        /// The <see cref="Tent"/> row is refused: it is a line of the price board, not a place, and it has no height
        /// of its own. A night in your own tent is <see cref="Camp.Sleep"/> at the height the tent actually stands
        /// at, and that is the only place it should ever be.</summary>
        public static Night Sleep(Climber c, in Bunk b, bool burner = false, float days = 1f, Place place = Place.Elbrus)
        {
            if (c == null || b.IsEmpty || b.Kind == BunkKind.Tent || !AllowedIn(place)) return default;

            float before = c.Acclimatisation;
            bool melts = burner || b.MeltsSnow;

            // one rule, one place: the night, the acclimatisation and the water are Camp's
            Camp.Sleep(c, b.Ele, melts, days);

            // and this is what the roof adds on top of it
            float thaw = ThawBelow(b);
            if (c.Hands < thaw) c.Hands = 0f;
            if (c.Feet < thaw) c.Feet = 0f;
            if (c.Face < thaw) c.Face = 0f;
            if (melts) c.ThermosSips = AscentRoute.ThermosSips;

            return new Night(b.Kind, b.Name, b.Roubles, c.Acclimatisation, c.Acclimatisation - before,
                RestFactor(b), thaw, b.Dries, melts, Note(b));
        }

        /// <summary>What a night here would be worth, without changing anything — for the line the HUD shows before
        /// the player pays. Acclimatisation comes straight from <see cref="Camp.SleepGain"/>.</summary>
        public static float SleepGain(Climber c, in Bunk b, float days = 1f)
            => c == null || b.IsEmpty ? 0f : Camp.SleepGain(c, b.Ele, days);

        /// <summary>Puts the three bars of the night back where a warm room leaves them. Never lowers anything: a
        /// hiker who walked in warm does not come out colder for having slept indoors.</summary>
        public static void Warm(Participant p, in Bunk b)
        {
            if (p == null || b.IsEmpty || p.Outcome != Outcome.None) return;
            float to = 100f * RestFactor(b);
            if (p.Heat < to) p.Heat = to;
            if (p.Hands < to) p.Hands = to;
            if (p.Clarity < to) p.Clarity = to;
        }

        /// <summary>Hangs the wet things over the pipes. Returns how many came out dry; 0 where there is nothing to
        /// dry them with — a tent on the snow dries nothing, which is why damp matches are a real problem.</summary>
        public static int Dry(Backpack pack, in Bunk b)
        {
            if (pack == null || b.IsEmpty || !b.Dries) return 0;
            int dried = 0;
            for (int i = 0; i < pack.Contents.Count; i++)
            {
                var s = pack.Contents[i];
                if (!s.Spec.Soaks || s.Wet == 0) continue;
                pack.SetAt(i, s.Wetter(-s.Wet));
                dried++;
            }
            return dried;
        }

        /// <summary>Takes the price of a bunk out of the purse. False and nothing taken when there is not enough;
        /// true and nothing taken at the rescuers' wagons, because there is nothing to take.</summary>
        public static bool Book(ref Wallet wallet, in Bunk b)
            => !b.IsEmpty && (b.Free || wallet.Pay(b.Roubles));

        static string Note(in Bunk b) => b.Kind switch
        {
            BunkKind.Tent => "Ночь в палатке: тепло только своё.",
            BunkKind.RescueWagon => "Вагончик спасателей: дизель, четыре койки, денег не берут.",
            BunkKind.Barrel => "В бочке тепло и сухо, на плите чайник.",
            BunkKind.Capsule => "Капсула: постель, свет, тепло. Утром не хочется вставать.",
            _ => "В приюте тепло и сухо: кухня, сушилка, дизель до утра.",
        };
    }
}
