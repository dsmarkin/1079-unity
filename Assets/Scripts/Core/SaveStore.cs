using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Height1079.Core
{
    /// <summary>One save on disk, as the menu needs to know it without loading the mountain.</summary>
    public readonly struct SaveSlot
    {
        public readonly string Path;
        public readonly Place Place;
        public readonly DateTime SavedUtc;
        /// <summary>«косая полка, 5290 м».</summary>
        public readonly string Where;
        /// <summary>The hour of the day the run stopped at.</summary>
        public readonly float Hour;
        /// <summary>Names of everybody the save carries, for the line under the button.</summary>
        public readonly string Party;

        public SaveSlot(string path, Place place, DateTime savedUtc, string where, float hour, string party)
        { Path = path; Place = place; SavedUtc = savedUtc; Where = where; Hour = hour; Party = party; }

        public bool IsEmpty => string.IsNullOrEmpty(Path);

        /// <summary>«Эльбрус · косая полка, 5290 м · 08:40 · вчера» — <paramref name="now"/> is local time.</summary>
        public string Line(DateTime now)
        {
            string place = Place == Place.Elbrus ? "Эльбрус" : "Холатчахль";
            string where = string.IsNullOrEmpty(Where) ? "склон" : Where;
            return $"{place} · {where} · {AscentRoute.Clock(Hour)} · {SaveStore.When(SavedUtc.ToLocalTime(), now)}";
        }
    }

    /// <summary>Saves as files: where they live, how they are written so that a crash half-way cannot leave a broken
    /// one, and which of them the menu offers.
    ///
    /// The write is the usual two steps — the whole file into a temporary beside it, then a rename, which is atomic on
    /// every filesystem this game runs on. A half-written save therefore never exists under a name the menu would
    /// read; the worst case is a stray <c>.tmp</c> that the next write clears away.
    ///
    /// Slots are one per location plus the date: <c>elbrus-20260918-0840.json</c>. «Продолжить» takes the newest for
    /// the place picked in the menu, and the ones behind it are kept as history — <see cref="KeepPerPlace"/> of them,
    /// because a player who walks into a crevasse at 4 000 m wants the night before back, not a clean slate.
    ///
    /// Engine-free on purpose: the directory is passed in (the runtime passes
    /// <c>Application.persistentDataPath</c>), so the whole of this runs in the dotnet tests against a temp folder.</summary>
    public static class SaveStore
    {
        /// <summary>Under the player's data folder.</summary>
        public const string Folder = "saves";
        public const string Extension = ".json";
        public const string Temp = ".tmp";
        /// <summary>How many dated slots per location are kept.</summary>
        public const int KeepPerPlace = 5;

        public static string SlotId(Place place, DateTime utc)
            => SaveGame.PlaceKey(place) + "-" + utc.ToUniversalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        public static string PathOf(string dir, Place place, DateTime utc)
            => Path.Combine(dir ?? "", SlotId(place, utc) + Extension);

        /// <summary>Writes the save and returns the path, or null when the disk would not have it. Temporary file
        /// first, then a rename: the file under the real name is either the old one or the new one and never a half.</summary>
        public static string Write(string dir, SaveGame save)
        {
            if (save == null || string.IsNullOrEmpty(dir)) return null;
            try
            {
                Directory.CreateDirectory(dir);
                string path = PathOf(dir, save.Place, save.SavedUtc);
                string tmp = path + Temp;
                using (var w = new StreamWriter(tmp, false, new UTF8Encoding(false)))
                {
                    w.Write(save.Text);
                    w.Flush();
                }
                // no window in which the slot has no file at all — which is what delete-then-move used to open, and
                // what this class promises it never does. File.Replace swaps the contents in one step; the three-
                // argument File.Move that would do the same is .NET Standard 2.1 and Unity's Mono is 2.0.
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                Prune(dir, save.Place);
                return path;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        /// <summary>Reads one file. Null when it is missing, unreadable, or written by a newer build.</summary>
        public static SaveGame Read(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                if (!File.Exists(path)) return null;
                return SaveGame.Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        /// <summary>Every readable slot for a place, newest first. Files that do not parse are skipped in silence:
        /// one bad save must not take the menu down with it.</summary>
        public static List<SaveSlot> List(string dir, Place place)
        {
            var slots = new List<SaveSlot>();
            if (string.IsNullOrEmpty(dir)) return slots;
            string[] files;
            try
            {
                if (!Directory.Exists(dir)) return slots;
                files = Directory.GetFiles(dir, SaveGame.PlaceKey(place) + "-*" + Extension);
            }
            catch (IOException) { return slots; }
            catch (UnauthorizedAccessException) { return slots; }

            foreach (var f in files)
            {
                var save = Read(f);
                if (save == null || save.Place != place) continue;
                var names = new StringBuilder();
                foreach (var c in save.Climbers)
                {
                    if (names.Length > 0) names.Append(", ");
                    names.Append(string.IsNullOrEmpty(c.Name) ? c.Key : c.Name);
                }
                slots.Add(new SaveSlot(f, save.Place, save.SavedUtc, save.Where, save.Hour, names.ToString()));
            }
            slots.Sort((a, b) => b.SavedUtc.CompareTo(a.SavedUtc));
            return slots;
        }

        /// <summary>The slot «Продолжить» would take, or an empty one.</summary>
        public static SaveSlot Newest(string dir, Place place)
        {
            var list = List(dir, place);
            return list.Count > 0 ? list[0] : default;
        }

        /// <summary>Keeps the newest <paramref name="keep"/> slots of a place and removes the rest, along with any
        /// temporary file a previous crash left behind.</summary>
        public static void Prune(string dir, Place place, int keep = KeepPerPlace)
        {
            var list = List(dir, place);
            for (int i = Math.Max(0, keep); i < list.Count; i++)
                try { File.Delete(list[i].Path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (var t in Directory.GetFiles(dir, "*" + Extension + Temp))
                    try { File.Delete(t); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        // ── how it reads in the menu ──────────────────────────────────────────────────────────────────────

        static readonly string[] months =
        {
            "января", "февраля", "марта", "апреля", "мая", "июня",
            "июля", "августа", "сентября", "октября", "ноября", "декабря",
        };

        /// <summary>«сегодня», «вчера», «16 сентября». Both arguments are local time.</summary>
        public static string When(DateTime saved, DateTime now)
        {
            int days = (int)(now.Date - saved.Date).TotalDays;
            if (days <= 0) return "сегодня";
            if (days == 1) return "вчера";
            if (days == 2) return "позавчера";
            if (saved.Year != now.Year) return $"{saved.Day} {months[saved.Month - 1]} {saved.Year}";
            return $"{saved.Day} {months[saved.Month - 1]}";
        }
    }
}
