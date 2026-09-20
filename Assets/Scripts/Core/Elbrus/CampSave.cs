using System;
using System.Collections.Generic;

namespace Height1079.Core
{
    /// <summary>What a night writes into a file, as a function of what the host knows at bedtime.
    ///
    /// This used to live inside <c>NightSession.SaveNight</c>, a method on a <c>NetworkBehaviour</c>, which meant the
    /// one rule nobody can afford to get wrong — the rule that decides what survives when the player stops playing —
    /// was the one rule no test could reach: it compiles neither in <c>dotnet</c> nor in EditMode without a network
    /// manager, connected clients and a scene. Two of the bugs found in the audit lived exactly here (a night written
    /// under the token key <c>c1</c>, and a second night in the same camp writing the evening clock into a file
    /// called «ночёвка»), and both would have been caught by four lines of test.
    ///
    /// So the shape is: the runtime gathers (it knows about clients, rucksacks and the terrain), this decides, and
    /// the runtime then applies the decision to its own live state. The rules that live here are the three that are
    /// easy to get subtly wrong and impossible to notice:
    /// <list type="bullet">
    /// <item><b>A night that was slept starts a new morning</b> — the date moves on by one, the run clock goes back
    /// to the start of the day and the fresh snow is cleared. A night that was <em>not</em> slept (everybody here has
    /// already had their night in this camp) moves nothing at all.</item>
    /// <item><b>One entry per key, and the key is the player's own</b> — never the client id, which is why a sleeper
    /// with no key is simply not written rather than written under a name no load will ever find.</item>
    /// <item><b>A friend who played this save yesterday and is not here tonight keeps his line</b>, exactly as it
    /// was: the file belongs to the party, not to whoever happens to be online.</item>
    /// </list></summary>
    public static class CampSave
    {
        /// <summary>The camp, the weather and the clock: everything about the night that is not a person. The
        /// runtime fills it from the tent it found, the terrain under it and its own weather state.</summary>
        public struct Night
        {
            public Place Place;
            public DateTime SavedUtc;

            /// <summary>Seconds of the run gone by, as the clock stands right now. If anybody slept it is thrown
            /// away and the save starts the morning; it is here so that a save written without a night keeps it.</summary>
            public float Elapsed;

            /// <summary>The weather-day counter, not a date: <see cref="Forecast.DateOf"/> turns it into one, and the
            /// board at the hut predicts out of the same number.</summary>
            public int Day;
            public bool Storm;
            public float FreshSnowCm;
            public int Seed;

            public float X, Y, Z, Yaw, Ele;
            public bool Burner;
            /// <summary>True for an actual двойка; false for a bunk under somebody else's roof.</summary>
            public bool Tent;
            public string Where;

            /// <summary>Did anybody actually sleep? Set by the runtime, which knows who has already had their night
            /// in this place. This is the difference between a morning and a repeated key press.</summary>
            public bool Slept;
        }

        /// <summary>One body at bedtime, as the host knows it. <see cref="Body"/> is the live climber and is not
        /// copied: the runtime has already run <see cref="Camp.Sleep"/> (or the hut's own night) over it, and this
        /// only reads the numbers off it.</summary>
        public sealed class Sleeper
        {
            /// <summary>The key this player's saves live under — his own, never a client id. A sleeper with an empty
            /// key is skipped: see <see cref="Build"/>.</summary>
            public string Key = "";
            public string Name = "";
            public Climber Body = new Climber();

            /// <summary>The three bars of the night as the session keeps them, 0…100, and what is left in the legs.</summary>
            public float Heat = 100f, Hands = 100f, Clarity = 100f, Strength = 1f;
            public int Roubles;
            public Registration Reg = Rescue.NotFiled;
            public Progress Programme = Progress.None;
            public List<ItemStack> Pack = new List<ItemStack>();
            public ItemStack Hand = ItemStack.Empty;
        }

        /// <summary>The clock the save is written with. A night that was slept begins the morning; one that was not
        /// keeps the evening exactly where it stood.</summary>
        public static float ElapsedAfter(in Night night) => night.Slept ? 0f : Math.Max(0f, night.Elapsed);

        /// <summary>The weather-day the save is written with. This is the whole of «a night moves the mountain on to
        /// the next day», and the reason the date cannot be walked forward by pressing the key twice.</summary>
        public static int DayAfter(in Night night) => night.Slept ? night.Day + 1 : night.Day;

        /// <summary>Builds the file. <paramref name="previous"/> is the save this run was loaded from, or null;
        /// everybody in it who is not in <paramref name="party"/> tonight is carried over untouched.</summary>
        public static SaveGame Build(in Night night, IList<Sleeper> party, SaveGame previous)
        {
            int day = DayAfter(night);
            var save = new SaveGame
            {
                Place = night.Place,
                SavedUtc = night.SavedUtc,
                Elapsed = ElapsedAfter(night),
                Storm = night.Storm,
                // a night in a tent is a night the snow of the day before stops counting
                FreshSnowCm = night.Slept ? 0f : Math.Max(0f, night.FreshSnowCm),
                Seed = night.Seed,
                // the file keeps the date and not the counter; Forecast.DayIndex is its exact inverse, which is how
                // a save that comes back on Thursday gets the Thursday it was promised
                Date = Forecast.DateOf(day),
                CampX = night.X, CampY = night.Y, CampZ = night.Z, CampYaw = night.Yaw, CampEle = night.Ele,
                Burner = night.Burner,
                Tent = night.Tent,
                Where = night.Where ?? "",
            };

            if (party != null)
                for (int i = 0; i < party.Count; i++)
                {
                    var s = party[i];
                    // No key means the client has not said yet which file it is: writing it now would file the night
                    // under something no load will ever match, and the orphan would then be copied into every save
                    // after it by the carry-over below.
                    if (s == null || string.IsNullOrEmpty(s.Key)) continue;
                    Write(save.Ensure(s.Key, s.Name ?? ""), s);
                }

            if (previous != null)
                foreach (var old in previous.Climbers)
                    if (old != null && save.Find(old.Key) == null) save.Climbers.Add(old);

            return save;
        }

        static void Write(SaveClimber entry, Sleeper s)
        {
            var c = s.Body ?? new Climber();
            entry.Acclim = c.Acclimatisation;
            entry.Highest = c.HighestEle;
            entry.Sickness = c.SicknessLoad;
            entry.Hands = c.Hands; entry.Feet = c.Feet; entry.Face = c.Face;
            entry.Dry = c.Dehydration; entry.Sleep = c.Drowsiness; entry.Blind = c.Blindness; entry.Pulse = c.Pulse;
            entry.Sips = c.ThermosSips;
            entry.Crampons = c.CramponsOn;
            entry.Heat = s.Heat; entry.HandsBar = s.Hands; entry.Clarity = s.Clarity;
            entry.Strength = s.Strength;
            entry.Roubles = s.Roubles;
            entry.Reg = s.Reg;
            entry.Programme = s.Programme;
            entry.Pack.Clear();
            if (s.Pack != null) foreach (var stack in s.Pack) entry.Pack.Add(stack);
            entry.Hand = s.Hand;
        }
    }
}
