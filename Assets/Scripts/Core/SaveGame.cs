using System;
using System.Collections.Generic;
using System.Globalization;

namespace Height1079.Core
{
    /// <summary>One climber inside a save. Everything here is per player and not per room: two people who walked up
    /// the same slope carry different acclimatisation, different frostbite and a different rucksack, and a save that
    /// averaged them would quietly undo the only progression the game has.</summary>
    public sealed class SaveClimber
    {
        /// <summary>Who this is. The key is the name the player typed in the menu, or a stable per-install id when
        /// there is no name (<see cref="SaveKeys"/>): it is what the host matches a returning player against.</summary>
        public string Key = "";
        /// <summary>The name as it was shown in the protocol, for the menu line. Never used for matching.</summary>
        public string Name = "";

        /// <summary>The one thing that is meant to outlive a run (<see cref="Ascent.Acclimatise"/>).</summary>
        public float Acclim = .3f;
        /// <summary>Highest point of the sortie in progress — after a night in camp this is the camp itself.</summary>
        public float Highest;
        public float Sickness, Hands, Feet, Face, Dry, Sleep, Blind, Pulse;
        public int Sips = AscentRoute.ThermosSips;
        public bool Crampons;

        /// <summary>The three bars of the night as <see cref="Participant"/> keeps them, 0…100.</summary>
        public float Heat = 100f, HandsBar = 100f, Clarity = 100f;
        /// <summary>What is left in the legs, 0…1.</summary>
        public float Strength = 1f;
        /// <summary>Roubles left in the purse — what was not spent at the hire counter and the cafés.</summary>
        public int Roubles = Wallet.StartRoubles;

        /// <summary>The slip this one left at the ЭВПСО counter, or <see cref="Rescue.NotFiled"/>. It is a piece of
        /// paper and it outlives a run: a party that registered on Monday is still registered on Tuesday, and a party
        /// that walked past the counter is still nobody's problem.</summary>
        public Registration Reg = Rescue.NotFiled;

        /// <summary>The rucksack, in order. Hired gear is nothing but objects in here, so "what was taken at the hire
        /// counter" needs no field of its own (<see cref="Rental.GearOf"/>).</summary>
        public readonly List<ItemStack> Pack = new List<ItemStack>();
        /// <summary>And whatever was in the hands.</summary>
        public ItemStack Hand = ItemStack.Empty;

        /// <summary>The kit this one is carrying, as the gate at Pastukhov rocks counts it.</summary>
        public Gear Kit => Rental.GearOf(Pack) | Rental.PieceOf(Hand.Id);
    }

    /// <summary>A stopped ascent, on disk. What is saved is the players and the clock, and nothing else: the mountain
    /// is generated from the data in the repository and is the same every time, so storing it would only be a way of
    /// getting it wrong later.
    ///
    /// The file carries <see cref="Schema"/>. A file from a newer schema is refused rather than half-read
    /// (<see cref="FromJson"/> returns null and the menu simply does not offer it), a file from an older one is read
    /// with today's defaults for whatever it lacks, and a field nobody recognises is ignored. That is the whole of the
    /// compatibility promise, and it is enough to keep an old save from taking the game down with it.</summary>
    public sealed class SaveGame
    {
        /// <summary>1 — the first format: place, clock, weather, one camp, one entry per player.</summary>
        public const int Schema = 1;

        public int SchemaVersion = Schema;
        public Place Place = Place.Elbrus;
        /// <summary>When it was written, UTC.</summary>
        public DateTime SavedUtc = DateTime.UtcNow;

        /// <summary>Seconds of the run that had gone by — which is the time of day
        /// (<see cref="AscentRoute.HourAt"/>), and the only clock this game has.</summary>
        public float Elapsed;
        /// <summary>The weather of the mountain: once it has broken it stays broken, so it has to come back broken.</summary>
        public bool Storm;
        public float FreshSnowCm;

        /// <summary>One seed for the whole save, and the day it is being played on. The weather is not rolled hour by
        /// hour any more: <see cref="Forecast.Day"/> draws the whole day out of these two, the board at the hut tries
        /// to predict the same day out of the same two, and a save that comes back on Thursday gets the Thursday it
        /// was promised. 0 means a save written before there were seeds — the runtime draws a fresh one.</summary>
        public int Seed;
        /// <summary>The date on the mountain. A night moves it on by one, which is what makes the board's «завтра»
        /// worth reading.</summary>
        public DateTime Date = DateTime.UtcNow.Date;

        /// <summary>Where the party slept. Everybody who loads this save wakes up beside it.</summary>
        public float CampX, CampY, CampZ, CampYaw, CampEle;
        /// <summary>Whether the camp has a burner in it — the night reads it (<see cref="Camp.Sleep"/>).</summary>
        public bool Burner;
        /// <summary>True when what stands there is an actual двойка; false for a night taken in a hut, where the spot
        /// is a bunk under somebody else's roof and putting a tent back on load would stand it inside the wall.</summary>
        public bool Tent = true;
        /// <summary>«косая полка, 5290 м» — written at save time so the menu can show it without loading the mountain.</summary>
        public string Where = "";

        public readonly List<SaveClimber> Climbers = new List<SaveClimber>();

        /// <summary>The hour of the day this save stopped at, for the menu line.</summary>
        public float Hour => AscentRoute.HourAt(Elapsed, World.ElbrusPlan.Profile.Seconds);

        public SaveClimber Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var c in Climbers) if (string.Equals(c.Key, key, StringComparison.Ordinal)) return c;
            return null;
        }

        public SaveClimber Ensure(string key, string name)
        {
            var c = Find(key);
            if (c != null) { c.Name = name; return c; }
            c = new SaveClimber { Key = key, Name = name };
            Climbers.Add(c);
            return c;
        }

        // ── the format ────────────────────────────────────────────────────────────────────────────────────

        public static string PlaceKey(Place p) => p == Place.Elbrus ? "elbrus" : "kholat";
        public static Place PlaceOf(string key) => string.Equals(key, "kholat", StringComparison.OrdinalIgnoreCase)
            ? Place.Kholat : Place.Elbrus;

        public const string Stamp = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        public JsonValue ToJson()
        {
            var root = JsonValue.Object()
                .Set("schema", SchemaVersion)
                .Set("game", "1079")
                .Set("place", PlaceKey(Place))
                .Set("saved", SavedUtc.ToUniversalTime().ToString(Stamp, CultureInfo.InvariantCulture))
                .Set("where", Where)
                .Set("elapsed", Round(Elapsed, 2))
                .Set("hour", Round(Hour, 3))
                .Set("storm", Storm)
                .Set("freshSnowCm", Round(FreshSnowCm, 2))
                .Set("seed", Seed)
                .Set("date", Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            root.Set("camp", JsonValue.Object()
                .Set("x", Round(CampX, 2)).Set("y", Round(CampY, 2)).Set("z", Round(CampZ, 2))
                .Set("yaw", Round(CampYaw, 1)).Set("ele", Round(CampEle, 2)).Set("burner", Burner)
                .Set("tent", Tent));

            var list = JsonValue.Array();
            foreach (var c in Climbers) list.Add(ClimberJson(c));
            root.Set("climbers", list);
            return root;
        }

        static JsonValue ClimberJson(SaveClimber c)
        {
            var o = JsonValue.Object()
                .Set("key", c.Key)
                .Set("name", c.Name)
                .Set("acclim", Round(c.Acclim, 4))
                .Set("highest", Round(c.Highest, 1))
                .Set("sickness", Round(c.Sickness, 3))
                .Set("hands", Round(c.Hands, 4)).Set("feet", Round(c.Feet, 4)).Set("face", Round(c.Face, 4))
                .Set("dry", Round(c.Dry, 4)).Set("sleepy", Round(c.Sleep, 4)).Set("blind", Round(c.Blind, 4))
                .Set("pulse", Round(c.Pulse, 4))
                .Set("sips", c.Sips)
                .Set("crampons", c.Crampons)
                .Set("heat", Round(c.Heat, 2)).Set("handsBar", Round(c.HandsBar, 2)).Set("clarity", Round(c.Clarity, 2))
                .Set("strength", Round(c.Strength, 4))
                .Set("roubles", c.Roubles);
            if (c.Reg.Filed)
                o.Set("rescue", JsonValue.Object()
                    .Set("party", c.Reg.Party)
                    .Set("people", c.Reg.People)
                    .Set("route", c.Reg.Route)
                    .Set("control", Round(c.Reg.ControlHour, 3))
                    .Set("back", Round(c.Reg.BackHour, 3)));
            var pack = JsonValue.Array();
            foreach (var s in c.Pack) if (!s.IsEmpty) pack.Add(StackJson(s));
            o.Set("pack", pack);
            if (!c.Hand.IsEmpty) o.Set("hand", StackJson(c.Hand));
            return o;
        }

        static JsonValue StackJson(ItemStack s)
        {
            var o = JsonValue.Object().Set("id", s.Id.ToString());
            if (s.Amount > 1) o.Set("n", s.Amount);
            if (s.Wet > 0) o.Set("wet", s.Wet);
            return o;
        }

        static ItemStack Stack(JsonValue v)
        {
            if (v == null || v.Kind != JsonKind.Object) return ItemStack.Empty;
            // items are written by name on purpose: the enum may grow, and a save must not shift underneath it
            if (!Enum.TryParse<ItemId>(v.Str("id"), false, out var id) || id == ItemId.None) return ItemStack.Empty;
            if (!Enum.IsDefined(typeof(ItemId), id)) return ItemStack.Empty;
            int n = Math.Max(1, v.Int("n", 1));
            int wet = Math.Max(0, Math.Min(100, v.Int("wet", 0)));
            return new ItemStack(id, (byte)Math.Min(255, n), (byte)wet);
        }

        static double Round(float v, int digits) => Math.Round(v, digits);

        /// <summary>Reads a save. Returns null when the file is from a newer schema than this build knows, which is
        /// the one case where guessing would be worse than not offering the slot at all.</summary>
        public static SaveGame FromJson(JsonValue root)
        {
            if (root == null || root.Kind != JsonKind.Object) return null;
            int schema = root.Int("schema", 0);
            if (schema <= 0 || schema > Schema) return null;

            var save = new SaveGame
            {
                SchemaVersion = schema,
                Place = PlaceOf(root.Str("place", "elbrus")),
                Elapsed = Math.Max(0f, root.Float("elapsed")),
                Storm = root.Flag("storm"),
                FreshSnowCm = Math.Max(0f, root.Float("freshSnowCm")),
                Where = root.Str("where"),
                Seed = root.Int("seed", 0),
            };
            save.SavedUtc = ParseStamp(root.Str("saved"));
            save.Date = ParseDate(root.Str("date"), save.SavedUtc.Date);

            var camp = root["camp"];
            save.CampX = camp.Float("x");
            save.CampY = camp.Float("y");
            save.CampZ = camp.Float("z");
            save.CampYaw = camp.Float("yaw");
            save.CampEle = camp.Float("ele", camp.Float("y"));
            save.Burner = camp.Flag("burner");
            // a save from before huts could be slept in has no flag and always meant a tent
            save.Tent = camp.Flag("tent", true);

            var list = root["climbers"];
            for (int i = 0; i < list.Count; i++)
            {
                var v = list[i];
                if (v.Kind != JsonKind.Object) continue;
                string key = v.Str("key");
                if (key.Length == 0) continue;
                var c = new SaveClimber
                {
                    Key = key,
                    Name = v.Str("name", key),
                    Acclim = Ascent.Clamp01(v.Float("acclim", .3f)),
                    Highest = v.Float("highest"),
                    Sickness = Math.Max(0f, v.Float("sickness")),
                    Hands = Ascent.Clamp01(v.Float("hands")),
                    Feet = Ascent.Clamp01(v.Float("feet")),
                    Face = Ascent.Clamp01(v.Float("face")),
                    Dry = Ascent.Clamp01(v.Float("dry")),
                    Sleep = Ascent.Clamp01(v.Float("sleepy")),
                    Blind = Ascent.Clamp01(v.Float("blind")),
                    Pulse = Ascent.Clamp01(v.Float("pulse")),
                    Sips = Math.Max(0, v.Int("sips", AscentRoute.ThermosSips)),
                    Crampons = v.Flag("crampons"),
                    Heat = v.Float("heat", 100f),
                    HandsBar = v.Float("handsBar", 100f),
                    Clarity = v.Float("clarity", 100f),
                    Strength = Ascent.Clamp01(v.Float("strength", 1f)),
                    Roubles = Math.Max(0, v.Int("roubles", Wallet.StartRoubles)),
                };
                var slip = v["rescue"];
                if (slip.Kind == JsonKind.Object && slip.Str("party").Length > 0)
                    c.Reg = Rescue.File(slip.Str("party"), slip.Int("people", 1),
                        slip.Str("route", Rescue.DefaultRoute),
                        slip.Float("control", Rescue.ControlHour), slip.Float("back", Rescue.BackHour));
                var pack = v["pack"];
                for (int k = 0; k < pack.Count; k++)
                {
                    var s = Stack(pack[k]);
                    if (!s.IsEmpty) c.Pack.Add(s);
                }
                c.Hand = Stack(v["hand"]);
                save.Climbers.Add(c);
            }
            return save;
        }

        static DateTime ParseDate(string text, DateTime fallback)
            => DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d.Date : fallback;

        static DateTime ParseStamp(string text)
        {
            if (DateTime.TryParseExact(text, Stamp, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)) return t;
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out t)) return t;
            return DateTime.UtcNow;
        }

        public string Text => ToJson().ToJson(true) + "\n";

        /// <summary>Reads a whole file. Null when it is not JSON at all, or not a save this build understands.</summary>
        public static SaveGame Parse(string text) => FromJson(JsonValue.TryParse(text));
    }

    /// <summary>Who a save belongs to. The key is the name the player typed in the menu, trimmed and with its inner
    /// runs of spaces collapsed, because that is the only thing the two machines of a co-op session both know about
    /// a player.
    ///
    /// When there is no name — an empty field, or the default «Путник» that the menu starts with and which two
    /// strangers would both be carrying — the key falls back to a <b>stable per-install id</b>, which the runtime
    /// generates once and keeps in the player prefs. It is per machine, so it never collides between two players, and
    /// it never changes, so a player who never types a name still comes back to his own acclimatisation.</summary>
    public static class SaveKeys
    {
        /// <summary>What the menu starts with, and therefore not a name.</summary>
        public const string DefaultName = "Путник";

        public static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var s = name.Trim();
            while (s.IndexOf("  ", StringComparison.Ordinal) >= 0) s = s.Replace("  ", " ");
            return s;
        }

        /// <summary>The key for this player. <paramref name="installId"/> is the stable fallback.</summary>
        public static string For(string name, string installId)
        {
            var s = Clean(name);
            if (s.Length == 0 || string.Equals(s, DefaultName, StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrEmpty(installId) ? DefaultName : installId;
            return s;
        }

        /// <summary>A fallback id out of a number: «гость-3f9a1c».</summary>
        public static string Guest(int seed) => "гость-" + (seed & 0xffffff).ToString("x6", CultureInfo.InvariantCulture);
    }
}
