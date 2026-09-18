using System;
using System.Collections.Generic;
using System.Globalization;

namespace Height1079.Core
{
    /// <summary>What a step points the compass at.</summary>
    public enum GoalKind : byte
    {
        /// <summary>Nothing to walk to — the step is about what is in the rucksack, on the feet, or on the clock.</summary>
        None,
        /// <summary>One named place on the map (<see cref="Elbrus.Pois"/>).</summary>
        Poi,
        /// <summary>«Любой приют выше N» — the nearest hut on the shelf that is high enough.</summary>
        Bunk,
        /// <summary>«Вниз, в долину» — the nearest of Terskol and the Azau meadow.</summary>
        Valley,
    }

    /// <summary>Where a step sends the player. Kept as a description and not as a pair of coordinates, because
    /// «любой приют выше 3800» has no single point until somebody is standing somewhere: <see cref="Programme.Aim"/>
    /// turns this into a bearing from wherever the player is.</summary>
    public readonly struct Goal
    {
        public readonly GoalKind Kind;
        /// <summary>For <see cref="GoalKind.Poi"/> — the id in <see cref="Elbrus.Pois"/>.</summary>
        public readonly string PoiId;
        /// <summary>For <see cref="GoalKind.Bunk"/> — the floor a hut has to stand above, metres.</summary>
        public readonly float Ele;
        /// <summary>How close counts as «дошёл», metres. 0 when the step is not about being anywhere.</summary>
        public readonly float ReachM;
        /// <summary>What to write beside the mark on the map.</summary>
        public readonly string Label;

        public Goal(GoalKind kind, string poiId, float ele, float reachM, string label)
        { Kind = kind; PoiId = poiId; Ele = ele; ReachM = reachM; Label = label; }

        public static readonly Goal Nowhere = default;
        public bool IsEmpty => Kind == GoalKind.None;
    }

    /// <summary>A mark on the map and a needle on the compass: the point a step is asking for, from here.</summary>
    public readonly struct Bearing
    {
        public readonly bool Has;
        public readonly float X, Z, Ele;
        /// <summary>Metres from the point the bearing was taken at.</summary>
        public readonly float DistanceM;
        /// <summary>Degrees from north, clockwise — the heading a compass needle would take.</summary>
        public readonly float HeadingDeg;
        public readonly string Label;

        public Bearing(float x, float z, float ele, float distanceM, float headingDeg, string label)
        { Has = true; X = x; Z = z; Ele = ele; DistanceM = distanceM; HeadingDeg = headingDeg; Label = label; }

        public static readonly Bearing None = default;

        /// <summary>«Приют 11 · 1,2 км» — one line for the HUD card.</summary>
        public string Line => !Has ? "" : Label + " · " + Programme.Far(DistanceM);
    }

    /// <summary>The night the programme remembers, and the only piece of history its conditions need. «Climb high,
    /// sleep low» is a statement about a day and the night that closed it, so a check on the live position can never
    /// see it: by the time the player wakes up, <see cref="Climber.HighestEle"/> has already been reset to the camp.
    ///
    /// The runtime writes it once, where it already computes <see cref="Ascent.Sortie"/>
    /// (<see cref="Programme.Slept"/>); everything else reads it.</summary>
    public readonly struct LastNight
    {
        public readonly bool Any;
        /// <summary>Where the night was taken, metres.</summary>
        public readonly float Ele;
        /// <summary>The high point of the day that ended in it — <see cref="Ascent.Sortie.HighestEle"/>.</summary>
        public readonly float FromEle;
        /// <summary>How many nights this save has been through. 0 before the first.</summary>
        public readonly int Count;

        public LastNight(float ele, float fromEle, int count)
        { Any = count > 0; Ele = ele; FromEle = fromEle; Count = Math.Max(0, count); }

        public static readonly LastNight None = default;
        /// <summary>Was the night at least this far below the day's high point (<see cref="Ascent.SleepLowerBy"/>)?</summary>
        public bool SleptLow => Any && FromEle - Ele >= Ascent.SleepLowerBy;
    }

    /// <summary>How far along the programme one climber is. A value, not an object: every change returns a new one,
    /// so a check can never accidentally tick a step.
    ///
    /// Steps are held as a bit set, and on disk as a list of their ids — the same trick the rucksack uses for
    /// <see cref="ItemId"/>: the table may grow or be reordered between builds and a save must not shift underneath
    /// it. An id this build does not know is dropped on read.</summary>
    public readonly struct Progress
    {
        /// <summary>Bit i is set when step i of <see cref="Programme.Steps"/> is done.</summary>
        public readonly ulong Done;
        /// <summary>The date on the mountain the programme first ticked anything on, or <c>default</c> before that.
        /// It is what makes «шестые сутки, а программа на четвёртом дне» sayable (<see cref="Programme.Behind"/>).</summary>
        public readonly DateTime Started;
        public readonly LastNight Night;

        public Progress(ulong done, DateTime started, LastNight night)
        { Done = done; Started = started; Night = night; }

        public static readonly Progress None = default;

        public bool IsEmpty => Done == 0 && Night.Count == 0 && Started == default;
        public bool IsDone(int index) => index >= 0 && index < 64 && (Done & (1UL << index)) != 0;
        public Progress With(int index) => index < 0 || index >= 64 ? this
            : new Progress(Done | (1UL << index), Started, Night);

        /// <summary>How many steps are ticked.</summary>
        public int Count
        {
            get { int n = 0; for (ulong b = Done; b != 0; b &= b - 1) n++; return n; }
        }

        /// <summary>The highest step index that is ticked, or −1.</summary>
        public int Furthest
        {
            get { for (int i = 63; i >= 0; i--) if (IsDone(i)) return i; return -1; }
        }

        // ── the format ────────────────────────────────────────────────────────────────────────────────────

        public JsonValue ToJson()
        {
            var o = JsonValue.Object();
            var done = JsonValue.Array();
            var all = Programme.Steps();
            for (int i = 0; i < all.Length; i++) if (IsDone(i)) done.Add(JsonValue.Of(all[i].Id));
            o.Set("done", done);
            if (Started != default) o.Set("started", Started.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (Night.Any)
                o.Set("night", JsonValue.Object()
                    .Set("ele", Math.Round(Night.Ele, 1))
                    .Set("from", Math.Round(Night.FromEle, 1))
                    .Set("count", Night.Count));
            return o;
        }

        /// <summary>Reads a programme block. Anything missing — an old save, a save from before there was a
        /// programme, a hand-edited file — reads as «ещё не начата», which is exactly right.</summary>
        public static Progress FromJson(JsonValue v)
        {
            if (v == null || v.Kind != JsonKind.Object) return None;
            ulong done = 0;
            var list = v["done"];
            for (int i = 0; i < list.Count; i++)
            {
                int index = Programme.IndexOf(list[i].AsString());
                if (index >= 0) done |= 1UL << index;
            }
            DateTime started = default;
            var text = v.Str("started");
            if (text.Length > 0 && DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d))
                started = d.Date;
            var n = v["night"];
            var night = n.Kind == JsonKind.Object
                ? new LastNight(n.Float("ele"), n.Float("from"), Math.Max(0, n.Int("count")))
                : LastNight.None;
            return new Progress(done, started, night);
        }
    }

    /// <summary>Everything a step is allowed to look at. One snapshot, filled by the runtime once a tick and by the
    /// tests by hand: every condition in the programme is a pure function of this and of <see cref="Progress"/>, and
    /// of nothing else. No engine types, no live objects, no time.</summary>
    public sealed class Standing
    {
        /// <summary>Which mountain. The programme only exists on Elbrus (<see cref="Programme.AllowedIn"/>).</summary>
        public Place Place = Place.Elbrus;

        /// <summary>Where the climber stands, in the world frame (x east, z north).</summary>
        public float X, Z;
        /// <summary>Metres above sea level, as the height field measures it — never as the signpost says.</summary>
        public float Ele;
        /// <summary>Time of day, <see cref="AscentRoute.HourAt"/>.</summary>
        public float Hour = AscentRoute.RunStartHour;
        /// <summary>The date on the mountain (<see cref="SaveGame.Date"/>).</summary>
        public DateTime Date;

        /// <summary>What is in the rucksack and in the hands — <see cref="SaveClimber.Kit"/>.</summary>
        public Gear Kit = Gear.None;
        /// <summary>Crampons on the boots right now, not merely in the sack.</summary>
        public bool CramponsOn;
        /// <summary>The slip at the ЭВПСО counter is filed (<see cref="Rescue.Watched"/>).</summary>
        public bool Registered;
        /// <summary>The forecast board has been read at least once in this save.</summary>
        public bool ForecastRead;

        /// <summary>The high point of the sortie in progress — <see cref="Climber.HighestEle"/>. Reset to the camp
        /// by a night, which is why the programme keeps its own <see cref="LastNight"/>.</summary>
        public float Highest;

        public Standing() { }

        /// <summary>The usual way the runtime builds one: the save, the climber in it, and where he is standing.</summary>
        public static Standing Of(SaveGame save, SaveClimber c, float x, float z, float ele,
            bool cramponsOn = false, bool forecastRead = false)
        {
            var s = new Standing { X = x, Z = z, Ele = ele, CramponsOn = cramponsOn, ForecastRead = forecastRead };
            if (save != null) { s.Place = save.Place; s.Hour = save.Hour; s.Date = save.Date; }
            if (c != null) { s.Kit = c.Kit; s.Registered = Rescue.Watched(c.Reg); s.Highest = c.Highest; }
            return s;
        }
    }

    /// <summary>One line of the programme: what to do, why, where, and the test that ticks it.</summary>
    public readonly struct ProgrammeStep
    {
        /// <summary>Position in <see cref="Programme.Steps"/> — the bit in <see cref="Progress.Done"/>.</summary>
        public readonly int Index;
        /// <summary>Which day of the nine it belongs to.</summary>
        public readonly int Day;
        /// <summary>Stable id. This and not the index is what goes into the save.</summary>
        public readonly string Id;
        /// <summary>Two or three words for the HUD card.</summary>
        public readonly string Title;
        /// <summary>The step itself, one line: «Дойти до водопада „Девичьи Косы“, 2800 м».</summary>
        public readonly string Line;
        /// <summary>Why it is in the programme. This is the teaching, and the only long text the game has.</summary>
        public readonly string Why;
        /// <summary>The condition in words, for the card and for anybody reading the table.</summary>
        public readonly string Test;
        public readonly Goal Goal;
        /// <summary>The condition itself: a pure function of the progress so far and of one standing.</summary>
        public readonly Func<Progress, Standing, bool> Rule;

        public ProgrammeStep(int index, int day, string id, string title, string line, string why, string test,
            Goal goal, Func<Progress, Standing, bool> rule)
        {
            Index = index; Day = day; Id = id; Title = title; Line = line; Why = why; Test = test;
            Goal = goal; Rule = rule;
        }

        public static readonly ProgrammeStep Empty = default;
        public bool IsEmpty => Rule == null;
    }

    /// <summary>One of the nine days, as the sheet prints it.</summary>
    public readonly struct ProgrammeDay
    {
        public readonly int Number;
        /// <summary>«переезд наверх».</summary>
        public readonly string Title;
        /// <summary>The high point of the day, as text: «4600–4800» or «—».</summary>
        public readonly string Reach;
        /// <summary>Where the night is: «3800–3900».</summary>
        public readonly string Sleep;
        /// <summary>Only the reserve day has one.</summary>
        public readonly string Note;

        public ProgrammeDay(int number, string title, string reach, string sleep, string note = "")
        { Number = number; Title = title; Reach = reach; Sleep = sleep; Note = note; }

        public bool IsEmpty => Number <= 0;
        /// <summary>«День 6 · скалы Пастухова».</summary>
        public string Head => IsEmpty ? "" : "День " + Number + " · " + Title;
        /// <summary>«выход 4600–4800, ночёвка 3800–3900».</summary>
        public string Profile => IsEmpty ? ""
            : (Reach == "—" ? "без набора" : "выход " + Reach) + ", ночёвка " + Sleep;
    }

    /// <summary>The guide's programme: the eight-and-a-reserve-day acclimatisation schedule of the southern side,
    /// broken into steps a player can be standing in front of, with the reason for every one of them.
    ///
    /// It is a <b>hint and not a lock</b>. Nothing here gates anything: a player who walks out of Azau on the first
    /// morning and goes straight for the summit is not stopped, he is merely unacclimatised, and the rules that will
    /// kill him live in <see cref="Ascent"/>, not here. Steps tick themselves the moment their condition holds, in
    /// whatever order that happens, and the card simply moves on (<see cref="Current"/>).
    ///
    /// Every condition is a pure function of (<see cref="Progress"/>, <see cref="Standing"/>) — see
    /// <see cref="Holds"/>. The heights the conditions trigger on are the ones <b>our height field measures</b>; the
    /// round figures of the signposts stay in the text, the same split <see cref="Elbrus.RouteStages"/> already
    /// makes. Terskol reads 2261 m on our DEM against 2100 on the sign, the falls read 3060 against 2800 — which is
    /// why the valley band below is 2000–2500 and not «2100–2300».
    ///
    /// Not a quest log. There is no reward, no counter of anything killed and no line the player has to say: it is
    /// the sheet a guide hands out on the first evening, and the game's whole tutorial is in the «зачем».</summary>
    public static partial class Programme
    {
        /// <summary>No programme on Kholat: that night is one night and it has its own script
        /// (<see cref="NightRun.Scenario"/>). Everything below returns nothing for any other place.</summary>
        public static bool AllowedIn(Place place) => place == Place.Elbrus;

        // ── the numbers the conditions trigger on ─────────────────────────────────────────────────────────

        /// <summary>The valley band: the three nights that are taken where people live. Our DEM reads Terskol at
        /// 2261 and the Azau meadow at 2361, both inside it; the sheet still prints the signpost «2100–2300».</summary>
        public const float ValleyLowEle = 2000f, ValleyHighEle = 2500f;
        /// <summary>The shelf the barrels and the huts stand on — the four nights of the second half. Our DEM reads
        /// the barrels at 3702 and LeapRus at 3909.</summary>
        public const float ShelfLowEle = 3650f, ShelfHighEle = 3980f;
        /// <summary>Day 2: the 3 000-metre outing. Chegette is 444 m off the west edge of our square, so the
        /// equivalent is the Krugozor station or the gorge above the village. 2 900 and not 2 950, because our
        /// height field reads the «3000» station at 2 938 (docs/ELBRUS.md) — a threshold the signpost agrees with
        /// would be one no player could ever cross by standing on the platform.</summary>
        public const float WalkHighEle = 2900f;
        /// <summary>Day 4: the snow-and-ice drill. Above this the ground is firn on the terrain splat, so crampons
        /// have something to bite.</summary>
        public const float DrillEle = 3700f;
        /// <summary>Day 5: the radial to Priut 11 — the bottom of <see cref="Ascent.TouchLowFrom"/>'s useful band.</summary>
        public const float PriutEle = 4100f;
        /// <summary>Day 6: Pastukhov rocks — <see cref="Ascent.TouchHighFrom"/>, the touch that is worth double.</summary>
        public const float RocksEle = Ascent.TouchHighFrom;
        /// <summary>Day 7: a rest day is a day you did not climb. The last hut on the route is the ceiling.</summary>
        public const float RestCeilingEle = AscentRoute.LastHutEle;
        /// <summary>Day 8: the summit counts as touched from here, and the descent as finished below
        /// <see cref="RestCeilingEle"/>.</summary>
        public const float SummitTouchEle = 5600f;
        /// <summary>Day 8: how far above the night the pre-dawn start has to have got before it counts as
        /// «вышел». Sixty metres is a few minutes up the cat track from the barrels — enough to be a departure and
        /// not a trip to the outhouse.</summary>
        public const float StartGainM = 60f;

        /// <summary>How close counts as «дошёл», by what is being walked to: a village is 610 m long, a station is a
        /// building, a waterfall is a slab, and the summit keeps <see cref="Elbrus.GoalRadius"/>.</summary>
        public const float VillageReachM = 250f, StationReachM = 150f, PlaceReachM = 80f, SummitReachM = 40f;

        /// <summary>The iron of day one: what has to be tried on before it is needed, as against the full
        /// <see cref="Ascent.Required"/> that the gate at Pastukhov rocks counts on day seven.</summary>
        public const Gear Iron = Gear.Crampons | Gear.IceAxe | Gear.Harness | Gear.Helmet | Gear.Goggles;

        // ── the table ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The steps in the order the programme walks them. The index is the bit in
        /// <see cref="Progress.Done"/>; <see cref="ProgrammeStep.Id"/> is what the save carries.</summary>
        public static ProgrammeStep[] Steps()
        {
            var list = new ProgrammeStep[table.Length];
            Array.Copy(table, list, list.Length);
            return list;
        }

        /// <summary>The steps of a place: none at all anywhere but Elbrus.</summary>
        public static ProgrammeStep[] Steps(Place place) => AllowedIn(place) ? Steps() : new ProgrammeStep[0];

        public static int Count => table.Length;
        public static ProgrammeStep At(int index) => index >= 0 && index < table.Length ? table[index] : ProgrammeStep.Empty;

        public static ProgrammeStep Get(string id)
        {
            int i = IndexOf(id);
            return i < 0 ? ProgrammeStep.Empty : table[i];
        }

        public static int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < table.Length; i++)
                if (string.Equals(table[i].Id, id, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>The nine days, including the reserve one, which has no steps in it on purpose.</summary>
        public static ProgrammeDay[] Days()
        {
            var list = new ProgrammeDay[days.Length];
            Array.Copy(days, list, list.Length);
            return list;
        }

        /// <summary>The last day of the programme — the reserve one.</summary>
        public static int LastDay => days.Length;
        public static ProgrammeDay DayOf(int number)
            => number >= 1 && number <= days.Length ? days[number - 1] : default;

        /// <summary>The steps of one day, in order.</summary>
        public static ProgrammeStep[] StepsOf(int day)
        {
            int n = 0;
            foreach (var s in table) if (s.Day == day) n++;
            var list = new ProgrammeStep[n];
            n = 0;
            foreach (var s in table) if (s.Day == day) list[n++] = s;
            return list;
        }

        // ── the conditions ────────────────────────────────────────────────────────────────────────────────

        /// <summary>Does this step's condition hold right now? Pure: the same pair always gives the same answer, and
        /// nothing here writes anything. A step that is already done still answers honestly — being done and being
        /// satisfied are different questions.</summary>
        public static bool Holds(in ProgrammeStep step, in Progress p, Standing now)
            => !step.IsEmpty && now != null && AllowedIn(now.Place) && step.Rule(p, now);

        public static bool Holds(int index, in Progress p, Standing now) => Holds(At(index), p, now);
        public static bool Holds(string id, in Progress p, Standing now) => Holds(Get(id), p, now);

        /// <summary>Ticks every step whose condition holds, wherever it stands in the programme, and returns the new
        /// progress. This is the whole of the «не запирает» rule: a player who summits on the first morning gets the
        /// summit step ticked and the card jumps to what is left, and nothing anywhere refuses him anything.
        ///
        /// Call it every tick; it is cheap and idempotent. Off Elbrus it returns the progress untouched.</summary>
        public static Progress Advance(in Progress before, Standing now)
        {
            if (now == null || !AllowedIn(now.Place)) return before;
            var after = before;
            for (int i = 0; i < table.Length; i++)
            {
                if (after.IsDone(i)) continue;
                if (table[i].Rule(after, now)) after = after.With(i);
            }
            if (after.Done != before.Done && after.Started == default && now.Date != default)
                after = new Progress(after.Done, now.Date, after.Night);
            return after;
        }

        /// <summary>What changed between two progresses — the steps the HUD should say out loud, in order.</summary>
        public static ProgrammeStep[] NewlyDone(in Progress before, in Progress after)
        {
            ulong fresh = after.Done & ~before.Done;
            int n = 0;
            for (ulong b = fresh; b != 0; b &= b - 1) n++;
            var list = new ProgrammeStep[n];
            n = 0;
            for (int i = 0; i < table.Length; i++) if ((fresh & (1UL << i)) != 0) list[n++] = table[i];
            return list;
        }

        /// <summary>Records a night. The runtime calls this once, where it already builds
        /// <see cref="Ascent.Sortie"/> for <see cref="Ascent.Acclimatise"/>: the camp height, and the high point of
        /// the day that ended in it. The next <see cref="Advance"/> sees it and ticks whatever night steps now hold.</summary>
        public static Progress Slept(in Progress p, float campEle, float dayHighEle)
            => new Progress(p.Done, p.Started, new LastNight(campEle, Math.Max(dayHighEle, campEle), p.Night.Count + 1));

        /// <summary>The same, straight from the sortie the acclimatisation is already being computed from.</summary>
        public static Progress Slept(in Progress p, in Ascent.Sortie s) => Slept(p, s.SleptEle, s.HighestEle);

        // ── where the programme stands ────────────────────────────────────────────────────────────────────

        /// <summary>A step below something already done: the player went past it. It is not failed and not hidden —
        /// the card just stops pointing at it, because pointing a man at «переночевать в долине» when he is already
        /// living in the barrels would be worse than saying nothing.</summary>
        public static bool LeftBehind(in Progress p, int index)
            => index >= 0 && index < table.Length && !p.IsDone(index) && index < p.Furthest;

        /// <summary>«Что делать сейчас»: the first step that is neither done nor left behind. Empty when the
        /// programme has nothing left to point at.</summary>
        public static ProgrammeStep Current(in Progress p, Place place = Place.Elbrus)
        {
            if (!AllowedIn(place)) return ProgrammeStep.Empty;
            int furthest = p.Furthest;
            for (int i = 0; i < table.Length; i++)
                if (!p.IsDone(i) && i > furthest) return table[i];
            return ProgrammeStep.Empty;
        }

        /// <summary>«А потом»: the one after <see cref="Current"/>.</summary>
        public static ProgrammeStep Next(in Progress p, Place place = Place.Elbrus)
        {
            var cur = Current(p, place);
            if (cur.IsEmpty) return ProgrammeStep.Empty;
            for (int i = cur.Index + 1; i < table.Length; i++)
                if (!p.IsDone(i)) return table[i];
            return ProgrammeStep.Empty;
        }

        /// <summary>The day the card is showing. When there is nothing left to point at it is the reserve day.</summary>
        public static int CurrentDay(in Progress p, Place place = Place.Elbrus)
        {
            var cur = Current(p, place);
            return cur.IsEmpty ? LastDay : cur.Day;
        }

        /// <summary>Nothing left to point at — either everything is done, or the player ran past the end of it.</summary>
        public static bool Finished(in Progress p, Place place = Place.Elbrus) => Current(p, place).IsEmpty;

        /// <summary>Every single step ticked. The strict reading: a party that walked the whole programme.</summary>
        public static bool Walked(in Progress p)
        {
            for (int i = 0; i < table.Length; i++) if (!p.IsDone(i)) return false;
            return true;
        }

        /// <summary>Done steps out of all of them, 0…1 — the bar in the HUD.</summary>
        public static float Share(in Progress p) => table.Length == 0 ? 1f : (float)p.Count / table.Length;

        /// <summary>Which night of the programme this is: 1 the first, 0 before any. Nights and not dates are what
        /// the schedule is measured in.</summary>
        public static int Nights(in Progress p) => p.Night.Count;

        // ── the programme day against the date in the save ────────────────────────────────────────────────

        /// <summary>Which calendar day of the trip it is: 1 on the day the programme first ticked anything, and one
        /// more for every date that has gone by since (<see cref="SaveGame.Date"/>, which a night moves on).
        ///
        /// <b>The programme's day is not the date, and is not meant to be.</b> <see cref="CurrentDay"/> is a
        /// position in a list of steps and it moves when steps are done; the date moves when a night is taken. A
        /// player who spends two evenings on day three moves neither. A player who sleeps four nights in Terskol
        /// burns four dates and stays on day one. Tying the two together would mean either failing a day at midnight —
        /// a quest log with a timer, which is the one thing this is not — or refusing to let a night move the
        /// weather, which is where the dates are actually needed (<see cref="Forecast.Day"/>).
        ///
        /// So they run side by side and the HUD says both when they disagree (<see cref="Behind"/>,
        /// <see cref="PaceText"/>): «шестые сутки, программа — четвёртый день» is a true and useful sentence, and it
        /// is the only place the two numbers meet.</summary>
        public static int CalendarDay(in Progress p, DateTime date)
        {
            if (p.Started == default || date == default) return 1;
            int n = (int)Math.Floor((date.Date - p.Started.Date).TotalDays) + 1;
            return Math.Max(1, n);
        }

        /// <summary>Days behind the schedule: positive when the trip has taken longer than the programme says, 0 when
        /// it is on time or ahead.</summary>
        public static int Behind(in Progress p, DateTime date)
            => Math.Max(0, CalendarDay(p, date) - CurrentDay(p));

        /// <summary>The line under the card, or empty when there is nothing worth saying.</summary>
        public static string PaceText(in Progress p, DateTime date)
        {
            int slip = Behind(p, date);
            if (slip <= 0) return "";
            return "Идут " + CalendarDay(p, date) + "-е сутки, программа — день " + CurrentDay(p)
                + " (отставание " + slip + " " + Dney(slip) + ")";
        }

        // ── the bearing ───────────────────────────────────────────────────────────────────────────────────

        /// <summary>Where the compass needle goes and where the mark on the map sits, from where the player is
        /// standing. «Любой приют выше 3800» resolves to the nearest hut that is high enough, «в долину» to the
        /// nearer of Terskol and Azau, and a step that is about the rucksack has no mark at all.</summary>
        public static Bearing Aim(in ProgrammeStep step, float x, float z) => Aim(step.Goal, x, z);

        public static Bearing Aim(in Goal goal, float x, float z)
        {
            switch (goal.Kind)
            {
                case GoalKind.Poi:
                {
                    var poi = Find(goal.PoiId);
                    return poi == null ? Bearing.None : At(poi, goal.Label, x, z);
                }
                case GoalKind.Bunk:
                {
                    Elbrus.Poi best = null;
                    float bestD = float.MaxValue;
                    foreach (var poi in Elbrus.Pois)
                    {
                        if (poi.Kind != Elbrus.Kind.Hut || poi.Ele < goal.Ele) continue;
                        float d = Elbrus.Distance(x, z, poi.X, poi.Z);
                        if (d < bestD) { bestD = d; best = poi; }
                    }
                    return best == null ? Bearing.None : At(best, goal.Label, x, z);
                }
                case GoalKind.Valley:
                {
                    var a = Elbrus.Get("terskol");
                    var b = Elbrus.Get("azau");
                    var near = Elbrus.Distance(x, z, a.X, a.Z) <= Elbrus.Distance(x, z, b.X, b.Z) ? a : b;
                    return At(near, goal.Label, x, z);
                }
            }
            return Bearing.None;
        }

        static Bearing At(Elbrus.Poi poi, string label, float x, float z)
            => new Bearing(poi.X, poi.Z, poi.Ele, Elbrus.Distance(x, z, poi.X, poi.Z),
                HeadingDeg(x, z, poi.X, poi.Z), string.IsNullOrEmpty(label) ? poi.Label : label);

        static Elbrus.Poi Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var poi in Elbrus.Pois) if (poi.Id == id) return poi;
            return null;
        }

        /// <summary>Degrees from north, clockwise, in the game frame (x east, z north) — what a compass shows.</summary>
        public static float HeadingDeg(float fromX, float fromZ, float toX, float toZ)
        {
            float deg = (float)(Math.Atan2(toX - fromX, toZ - fromZ) * 180.0 / Math.PI);
            return deg < 0f ? deg + 360f : deg;
        }

        /// <summary>Is the player close enough to a step's goal to count as there?</summary>
        public static bool Near(in Goal goal, float x, float z)
        {
            if (goal.ReachM <= 0f) return false;
            var aim = Aim(goal, x, z);
            return aim.Has && aim.DistanceM <= goal.ReachM;
        }

        // ── text ──────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>«1,2 км» / «240 м».</summary>
        public static string Far(float metres)
        {
            if (metres < 1000f) return Math.Round(metres / 10f) * 10 + " м";
            return (metres / 1000f).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " км";
        }

        static string Dney(int n)
        {
            int t = n % 100, o = n % 10;
            if (t >= 11 && t <= 14) return "дней";
            if (o == 1) return "день";
            if (o >= 2 && o <= 4) return "дня";
            return "дней";
        }

        /// <summary>The HUD card: what to do now, why, and where it is from here. Empty off Elbrus and empty when
        /// the programme has nothing left to say.</summary>
        public static string CardText(in Progress p, Standing now)
        {
            if (now == null || !AllowedIn(now.Place)) return "";
            var step = Current(p, now.Place);
            if (step.IsEmpty) return Walked(p)
                ? "Программа пройдена: восемь дней и Западная вершина."
                : "Программа пройдена насквозь, часть дней осталась несделанной. Дальше — по своему усмотрению.";
            var sb = new System.Text.StringBuilder();
            sb.Append(DayOf(step.Day).Head).Append('\n');
            sb.Append(step.Line);
            var aim = Aim(step, now.X, now.Z);
            if (aim.Has) sb.Append('\n').Append(aim.Line);
            var next = Next(p, now.Place);
            if (!next.IsEmpty) sb.Append('\n').Append("Дальше: ").Append(next.Line);
            var pace = PaceText(p, now.Date);
            if (pace.Length > 0) sb.Append('\n').Append(pace);
            return sb.ToString();
        }

        /// <summary>The whole sheet, as it would be handed out on the first evening. Used by the board in the hire
        /// shop and by anybody who wants to read the programme without playing it.</summary>
        public static string SheetText(Place place = Place.Elbrus)
        {
            if (!AllowedIn(place)) return "";
            var sb = new System.Text.StringBuilder();
            sb.Append("Программа восхождения · южный склон · ").Append(LastDay).Append(" дней\n");
            foreach (var day in days)
            {
                sb.Append('\n').Append(day.Head).Append(" — ").Append(day.Profile).Append('\n');
                foreach (var step in table)
                {
                    if (step.Day != day.Number) continue;
                    sb.Append("  · ").Append(step.Line).Append('\n');
                }
                if (day.Note.Length > 0) sb.Append("  ").Append(day.Note).Append('\n');
            }
            return sb.ToString();
        }
    }
}
