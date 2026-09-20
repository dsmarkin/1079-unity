using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Sandbox
{
    /// <summary>Where one spot of the yard is and which way it is turned. Offsets are metres from the yard's centre
    /// (<see cref="SandboxYardLayout.origin"/>), x east and z north, and the yaw is degrees clockwise from north —
    /// the same frame as the game (CLAUDE.md §3).</summary>
    [Serializable]
    public sealed class YardSpot
    {
        public float x, z, yaw;
    }

    /// <summary>Where the body is put down at the start, as an offset from the fire; y is how high above the snow it
    /// is dropped, so it settles on its legs instead of starting inside them.</summary>
    [Serializable]
    public sealed class YardSpawn
    {
        public float x = 1.3f, y = 1.2f, z = 2.1f;
    }

    /// <summary>One piece of cover: <c>kind</c> is <c>rock</c>, <c>spruce</c> or <c>windfall</c>, the position is an
    /// offset from the centre, and <c>height</c> is metres — what the Menk's eye has to get past.</summary>
    [Serializable]
    public sealed class YardCover
    {
        public string kind = "rock";
        public float x, z, height = 2f;
    }

    /// <summary>One thing of the camp's gear at the mouth of the tent: the name of an imported prefab
    /// (<c>Resources/World/Prefabs/Imported</c>), how far ahead of the tent it lies (metres toward the fire) and how
    /// far across the doorway (positive one side, negative the other). Anything inside the corridor is pushed out of
    /// the way in, so a badly placed thing cannot stop the crawl.</summary>
    [Serializable]
    public sealed class YardGear
    {
        public string name;
        public float ahead, across, yaw;
    }

    /// <summary>A tree that is scenery and not cover, at the margins: the name of an imported prefab, where it stands
    /// and how tall it is scaled to, in metres.</summary>
    [Serializable]
    public sealed class YardTree
    {
        public string name;
        public float x, z, yaw, height = 7f;
    }

    /// <summary>How fast time runs over the yard and what the weather does while it does — the pace of the day, not
    /// the look of it: the sky itself is the game's (<see cref="SandboxSky"/> over <c>Night/SkyDome</c>).</summary>
    [Serializable]
    public sealed class YardSky
    {
        /// <summary>The hour of 1 February 1959 the map opens at, in hours (14.5 is half past two in the afternoon).
        /// The afternoon, so the first thing anyone sees is daylight; sunset over Kholat that day is 16:58.</summary>
        public float hour = 14f;

        /// <summary>Real minutes one whole twenty-four hours takes. Eight: about a minute of afternoon light, a
        /// minute of sunset and dusk, five of night, and the dawn at the end.</summary>
        public float dayMinutes = 8f;

        /// <summary>Fronts arrive and pass by themselves, as a condition of the world. Off, the yard is calm until a
        /// run of the errand raises its own front.</summary>
        public bool weather = true;
    }

    /// <summary>The arrangement of the night yard, as data the player reads at start-up: where the fire, the tent and
    /// the labaz stand, what cover is between them, what the camp's gear at the tent mouth is and how wide the way in
    /// stays. Every field defaults to the value the yard was built with in code, so a missing or broken file is the
    /// yard as it always was and nothing throws (docs/SANDBOX.md §11).
    ///
    /// The file is <c>StreamingAssets/sandbox/yard.json</c>: in the project it is <c>Assets/StreamingAssets</c>, and
    /// a built player carries it as a loose file next to the app — on macOS inside
    /// <c>1079-sandbox.app/Contents/Resources/Data/StreamingAssets/sandbox/</c>. Edit it, restart the player, and the
    /// yard is different: no Unity, no rebuild.
    ///
    /// Read once, at the first touch of the yard. <see cref="JsonUtility"/> fills only the fields the file names, so a
    /// file may hold just the one number being tried — but a spot or a row it names it names whole: writing
    /// <c>"fire": { "x": 3 }</c> leaves that spot's yaw at zero rather than at the built-in 25.</summary>
    [Serializable]
    public sealed class SandboxYardLayout
    {
        /// <summary>The name of this arrangement, for the log — so a session says which file it is playing.</summary>
        public string name = "по умолчанию из кода";
        /// <summary>A line for whoever opens the file. The game does not read it; JSON has no comments.</summary>
        public string note = "";

        /// <summary>The square of snow, metres on a side. <see cref="HuntRules.YardSize"/> is what the rules were
        /// written for; nothing else in the rules reads it, so this only changes how much snow is drawn and how far
        /// the margins are.</summary>
        public float size = HuntRules.YardSize;

        /// <summary>Centre of the square in world metres, far south of the range so neither is in the other's
        /// picture. Everything below is an offset from here.</summary>
        public YardSpot origin = new YardSpot { x = 0f, z = -250f };

        /// <summary>The fire: the start, and the finish. Its yaw turns the imported pile of logs.</summary>
        public YardSpot fire = new YardSpot { x = 0f, z = -25f, yaw = 25f };
        /// <summary>The tent, its mouth toward the fire — that is what the yaw of 180 means here.</summary>
        public YardSpot tent = new YardSpot { x = 0f, z = 25f, yaw = 180f };
        /// <summary>The labaz: the second section, east of the line from the fire to the tent.</summary>
        public YardSpot labaz = new YardSpot { x = 23f, z = 2f, yaw = 20f };
        /// <summary>Where a body starts, beside the fire, facing the tent.</summary>
        public YardSpawn spawn = new YardSpawn();

        /// <summary>When the day starts, how fast it runs and whether the weather runs with it.</summary>
        public YardSky sky = new YardSky();

        /// <summary>Half a body plus the arms, metres: the way in from the mouth of the tent to the stove at the
        /// back. Gear laid inside it is slid sideways until it clears, so dressing never decides whether the night
        /// can be played.</summary>
        public float corridor = 1.1f;

        /// <summary>The seed the yaws of the cover are drawn from. Change it to shuffle how the same plan is turned;
        /// it moves nothing.</summary>
        public int seed = 1959;

        /// <summary>The cover, in rows between the fire (south) and the tent (north). Rocks and spruces hide a
        /// standing body; a windfall only one that is down.</summary>
        public List<YardCover> cover = new List<YardCover>
        {
            new YardCover { kind = "rock",     x = -5f, z = -18f, height = 2.0f },
            new YardCover { kind = "windfall", x =  6f, z = -12f, height = 1.1f },
            new YardCover { kind = "spruce",   x = -12f, z = -9f, height = 11f },
            new YardCover { kind = "rock",     x =  0f, z =  -4f, height = 2.2f },
            new YardCover { kind = "windfall", x = 11f, z =  -1f, height = 1.2f },
            new YardCover { kind = "spruce",   x = -8f, z =   3f, height = 12f },
            new YardCover { kind = "rock",     x =  3f, z =   8f, height = 1.9f },
            new YardCover { kind = "windfall", x = -13f, z = 11f, height = 1.0f },
            new YardCover { kind = "spruce",   x = 12f, z =  12f, height = 10f },
            new YardCover { kind = "rock",     x = -3f, z =  17f, height = 2.1f },
            new YardCover { kind = "spruce",   x =  8f, z =  21f, height = 11f },
            new YardCover { kind = "rock",     x = -9f, z =  23f, height = 1.8f },
            // the way east, to the labaz, and the labaz's own ring
            new YardCover { kind = "windfall", x = 18f, z = -14f, height = 1.0f },
            new YardCover { kind = "rock",     x = 17f, z =  -7f, height = 2.0f },
            new YardCover { kind = "spruce",   x = 27f, z =  -8f, height = 11f },
            new YardCover { kind = "rock",     x = 28f, z =  10f, height = 1.9f },
            new YardCover { kind = "windfall", x = 17f, z =   6f, height = 1.1f },
        };

        /// <summary>The camp's things about the mouth of the tent, the way a camp's things lie — dressing, not the
        /// list of what to carry home. The rolled tent of the list lies ahead 3.4, across 1.6, and nothing else lies
        /// on it.</summary>
        public List<YardGear> gear = new List<YardGear>
        {
            new YardGear { name = "Backpack",   ahead = 1.6f, across = -1.55f, yaw = 205f },
            new YardGear { name = "Axe",        ahead = 2.6f, across =  2.3f,  yaw =  30f },
            new YardGear { name = "WoodLog",    ahead = 3.2f, across =  2.6f,  yaw = 100f },
            new YardGear { name = "WoodLog",    ahead = 3.5f, across =  2.4f,  yaw =  96f },
            new YardGear { name = "Pot",        ahead = 3.0f, across = -1.35f, yaw =   0f },
            new YardGear { name = "Can",        ahead = 3.3f, across = -1.75f, yaw =   0f },
            new YardGear { name = "Can",        ahead = 2.9f, across = -1.6f,  yaw =  40f },
            new YardGear { name = "Flashlight", ahead = 2.5f, across = -1.15f, yaw = 250f },
            new YardGear { name = "Bedroll",    ahead = -.5f, across = -1.15f, yaw =   0f },
        };

        /// <summary>Bare birches and dead trees at the margins, so the yard reads as a winter wood and not a row of
        /// pines. Scenery: nothing hides behind these.</summary>
        public List<YardTree> dressing = new List<YardTree>
        {
            new YardTree { name = "Birch_Snow_1",    x = -21f, z =  20f, yaw =  30f, height = 7f },
            new YardTree { name = "Birch_Snow_2",    x =  22f, z = -20f, yaw =  80f, height = 8.5f },
            new YardTree { name = "DeadTree_Snow_1", x = -17f, z = -24f, yaw =   0f, height = 7f },
            new YardTree { name = "Birch_Snow_2",    x = -24f, z =  -6f, yaw = 140f, height = 8f },
            new YardTree { name = "DeadTree_Snow_1", x =  26f, z =  24f, yaw = 200f, height = 6.5f },
        };

        // ── where it is read from ──

        /// <summary>The folder of the loose files a player carries beside it. Under the project it is
        /// <c>Assets/StreamingAssets</c>; in a built player, the Data folder of the app.</summary>
        public const string Folder = "sandbox";
        public const string FileName = "yard.json";

        public static string FilePath => Path.Combine(Application.streamingAssetsPath, Folder, FileName);

        static SandboxYardLayout current;

        /// <summary>The arrangement in play. Read from disk the first time it is asked for, and kept — a run does not
        /// re-read the file, so a yard cannot change under a body standing in it.</summary>
        public static SandboxYardLayout Current
        {
            get { if (current == null) current = ReadFile(FilePath); return current; }
        }

        /// <summary>The file the arrangement in play came from, or null when it is the built-in one.</summary>
        public static string Source { get; private set; }

        /// <summary>Forget what was read, so the next touch reads the file again (the self-test and the editor).</summary>
        public static void Forget() { current = null; Source = null; }

        /// <summary>Put an arrangement in play by hand, for a test or a tool.</summary>
        public static void Use(SandboxYardLayout layout, string source = null)
        {
            current = layout ?? new SandboxYardLayout();
            Source = source;
        }

        /// <summary>Reads one file. Never throws: a file that is not there, not readable or not sense is a line in
        /// the log and the built-in yard. Desktop only — <see cref="File"/> reads the streaming assets folder on Mac
        /// and Windows, which is every platform this game is built for.</summary>
        public static SandboxYardLayout ReadFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    Debug.Log("1079 sandbox: нет " + path + " — расстановка двора по умолчанию из кода");
                    return Fallback();
                }
                var loaded = JsonUtility.FromJson<SandboxYardLayout>(File.ReadAllText(path));
                if (loaded == null)
                {
                    Debug.LogWarning("1079 sandbox: " + path + " пуст — расстановка двора по умолчанию из кода");
                    return Fallback();
                }
                string fault = loaded.Fault();
                if (fault != null)
                {
                    Debug.LogWarning("1079 sandbox: " + path + " — " + fault + "; расстановка двора по умолчанию из кода");
                    return Fallback();
                }
                Source = path;
                Debug.Log("1079 sandbox: расстановка двора «" + loaded.name + "» прочитана из " + path);
                return loaded;
            }
            catch (Exception e)
            {
                Debug.LogWarning("1079 sandbox: " + path + " не прочитан (" + e.Message + ") — расстановка двора по умолчанию из кода");
                return Fallback();
            }
        }

        static SandboxYardLayout Fallback() { Source = null; return new SandboxYardLayout(); }

        /// <summary>What is wrong with this arrangement, in Russian, or null when nothing is. A file is taken whole or
        /// not at all: half a yard read from a file and half from the code would be the hardest thing to explain in
        /// the log.</summary>
        public string Fault()
        {
            if (!(size >= 10f) || size > 1000f) return "размер двора " + size + " м вне 10…1000";
            if (origin == null || fire == null || tent == null || labaz == null || spawn == null) return "не хватает точки (origin, fire, tent, labaz, spawn)";
            if (!(corridor >= 0f) || corridor > 10f) return "ширина прохода " + corridor + " м вне 0…10";
            if (sky == null) return "не хватает раздела sky (час начала, длина суток, погода)";
            if (!(sky.hour >= 0f) || sky.hour >= 24f) return "час начала " + sky.hour + " вне 0…24";
            if (!(sky.dayMinutes >= .5f) || sky.dayMinutes > 1440f) return "сутки за " + sky.dayMinutes + " мин вне 0,5…1440";
            if (cover == null || gear == null || dressing == null) return "не хватает списка (cover, gear, dressing)";
            for (int i = 0; i < cover.Count; i++)
            {
                var c = cover[i];
                if (c == null) return "укрытие " + i + " пусто";
                if (!TryKind(c.kind, out _)) return "укрытие " + i + ": «" + c.kind + "» — бывают rock, spruce, windfall";
                if (!(c.height > 0f) || c.height > 60f) return "укрытие " + i + ": высота " + c.height + " м вне 0…60";
            }
            for (int i = 0; i < gear.Count; i++)
                if (gear[i] == null || string.IsNullOrWhiteSpace(gear[i].name)) return "вещь у входа " + i + " без имени префаба";
            for (int i = 0; i < dressing.Count; i++)
            {
                var t = dressing[i];
                if (t == null || string.IsNullOrWhiteSpace(t.name)) return "дерево по краям " + i + " без имени префаба";
                if (!(t.height > 0f) || t.height > 60f) return "дерево по краям " + i + ": высота " + t.height + " м вне 0…60";
            }
            return null;
        }

        /// <summary>The kind of cover a row names. Unknown words are refused rather than guessed.</summary>
        public static bool TryKind(string word, out SandboxHuntYard.Cover kind)
        {
            kind = SandboxHuntYard.Cover.Rock;
            if (string.IsNullOrWhiteSpace(word)) return false;
            switch (word.Trim().ToLowerInvariant())
            {
                case "rock": kind = SandboxHuntYard.Cover.Rock; return true;
                case "spruce": kind = SandboxHuntYard.Cover.Spruce; return true;
                case "windfall": kind = SandboxHuntYard.Cover.Windfall; return true;
                default: return false;
            }
        }

        /// <summary>This arrangement as the file would hold it — how the shipped default was written.</summary>
        public string ToJson() => JsonUtility.ToJson(this, true);
    }
}
