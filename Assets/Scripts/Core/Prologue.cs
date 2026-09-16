using System;
using System.Collections.Generic;
using System.Text;

namespace Height1079.Core
{
    /// <summary>Scene kinds from docs/SCRIPT.md: [КАТ] cutscene, [ИГРА] playable beat, [ВЫБОР] a choice the player makes.</summary>
    public enum BeatKind { Cutscene, Play, Choice }

    /// <summary>One beat of a mission. A Choice beat carries its options; the picked option is what reaches the protocol.</summary>
    public sealed class Beat
    {
        public string Id = "", Title = "";
        public BeatKind Kind;
        /// <summary>Documented facts the beat rests on (short, for the archive panel). Never dialogue.</summary>
        public string[] Facts = Array.Empty<string>();
        public string[] Options = Array.Empty<string>();
        /// <summary>How many options a Choice beat expects (Kolevatov's diary picks two of four).</summary>
        public int Picks = 1;
        /// <summary>Member id who leads this beat; empty = the mission lead.</summary>
        public string Lead = "";
    }

    /// <summary>Diary lines: documented ones are quoted with a date; player lines come from choices and play.</summary>
    public sealed class DiaryLine
    {
        public DateTime Date;
        public string Author = "", Text = "";
        public bool Documented;
        public override string ToString() => (Documented ? "*" : "") + Date.ToString("dd.MM") + " " + Author + ": " + Text;
    }

    /// <summary>The mission beats, transcribed from docs/SCRIPT.md. Dialogue is not here — it is written after locations exist.</summary>
    public static class Prologue
    {
        static Beat B(string id, BeatKind kind, string title, string lead = "", params string[] facts)
            => new Beat { Id = id, Kind = kind, Title = title, Lead = lead, Facts = facts };
        static Beat C(string id, string title, int picks, string lead, params string[] options)
            => new Beat { Id = id, Kind = BeatKind.Choice, Title = title, Lead = lead, Picks = picks, Options = options };

        public static readonly IReadOnlyDictionary<string, Beat[]> Beats = new Dictionary<string, Beat[]>
        {
            ["route-book"] = new[]
            {
                B("upi-corridor", BeatKind.Play, "Коридор УПИ: маршрутная книжка", "", "Маршрут III категории, 300 км; выход 23 января."),
                B("stove", BeatKind.Play, "Сборка подвесной печки на время", "", "Печка на подвесе — конструкция Дятлова."),
                C("roles", "Распределение обязанностей", 1, "", "Завхоз — Дубинина", "Завхоз — Колмогорова"),
                B("zolotaryov-arrives", BeatKind.Cutscene, "Появляется Золотарёв", "", "Присоединился перед выходом ради зачёта на категорию; представлялся «Сашей»."),
            },
            ["train"] = new[]
            {
                B("platform", BeatKind.Cutscene, "Перрон, посадка", "", "Поезд 23 января вечером."),
                B("berths", BeatKind.Play, "Полки: кто где", "", "Плацкартный вагон."),
                B("mandolin", BeatKind.Cutscene, "Мандолина, разговор с Золотарёвым", "", "Мандолина у Кривонищенко и Слободина."),
                B("serov-dawn", BeatKind.Cutscene, "Рассвет в Серове", ""),
            },
            ["serov"] = new[]
            {
                B("station-song", BeatKind.Play, "Вокзал: шуточное пение", "krivonishchenko", "Кривонищенко задержан милицией за то, что в шутку пел и «просил милостыню»."),
                B("police", BeatKind.Cutscene, "Отделение", "krivonishchenko", "Отпущен после объяснений."),
                B("school", BeatKind.Play, "Школа: разговор с детьми", "kolmogorova", "Группа была в школе Серова; дети провожали до поезда."),
                C("school-question", "Что ответить школьнику про палатку", 1, "kolmogorova", "Показать печку", "Показать карту", "Спеть"),
                B("ivdel-train", BeatKind.Cutscene, "Вечерний поезд на Ивдель", ""),
            },
            ["ivdel-vizhay"] = new[]
            {
                B("ivdel-night", BeatKind.Cutscene, "Ночной вокзал Ивделя", "", "Прибытие ночью, ожидание автобуса."),
                B("bus", BeatKind.Cutscene, "Автобус в Вижай", "", "Автобус переполнен; сидели на коленях."),
                B("money", BeatKind.Play, "Деньги и продукты у завхоза", "", "Дубинина отвечала за деньги и продукты."),
                B("cinema", BeatKind.Cutscene, "Кино в клубе", "", "Вечером в Вижае смотрели кино."),
            },
            ["quarter-41"] = new[]
            {
                B("truck", BeatKind.Play, "Кузов грузовика по тракту", "", "Открытый кузов, мороз; пели."),
                B("unload", BeatKind.Play, "Разгрузка, барак", ""),
                B("cart-deal", BeatKind.Cutscene, "Договор о подводе", "", "Подводу с возчиком дали лесозаготовители."),
                C("evening", "Вечер с рабочими", 1, "", "Стихи Бороды", "Кино", "Спать"),
            },
            ["second-northern"] = new[]
            {
                B("lozva-road", BeatKind.Play, "Дорога вдоль Лозьвы, 24 км", "yudin", "Юдин: «известковые скалы»; возчик пел лагерные песни."),
                C("yudin-leg", "Нога болит", 1, "yudin", "Идти", "Пересесть на подводу"),
                B("village-night", BeatKind.Cutscene, "Посёлок; разговоры до трёх ночи", "yudin", "Посёлок брошен после 1952-го; один пригодный дом."),
                B("samples", BeatKind.Play, "Образцы минералов: шесть штук", "yudin", "−8 °C утром 28-го; воспаление седалищного нерва; образцы для института."),
                C("farewell-order", "Прощание: с кого начать", 1, "yudin", "Дятлов", "Дубинина", "Тибо", "Колеватов", "Золотарёв", "Колмогорова", "Дорошенко", "Кривонищенко", "Слободин"),
                B("cart-leaves", BeatKind.Cutscene, "Подвода уходит, девять уходят вверх по Лозьве", ""),
            },
            ["lozva-auspiya"] = new[]
            {
                B("d1-track", BeatKind.Play, "28 января: тропление по очереди, наледь", "doroshenko", "−8 °C."),
                B("d1-camp", BeatKind.Cutscene, "Первая ночёвка: печка, ссора, сгоревшие рукавицы", "doroshenko", "Кривонищенко ушёл от печки; сгорели две рукавицы и телогрейка."),
                B("d2-trail", BeatKind.Play, "29 января: мансийская тропа, ремонт крепления", "slobodin", "−13 °C, слабый ветер."),
                B("d2-birthday", BeatKind.Cutscene, "Дорошенко двадцать один", "slobodin", "Тибо: «P.S. Бездарное письмо за два дня!»"),
                B("d3-wind", BeatKind.Play, "30 января: ветер, снег 120 см, мансийский лабаз", "kolevatov", "−17/−13/−26 °C, сильный юго-западный ветер."),
                C("d3-diary", "Запись дня Колеватова", 2, "kolevatov", "Про манси и их знаки", "Про снег", "Про ветер", "Про печку"),
                B("d4-lead", BeatKind.Play, "31 января: тропление по Дятлову, видимость падает", "zolotaryov", "−18…−24 °C, западный ветер, ясно, плохая видимость; выход в 10."),
                C("d4-decision", "Граница леса: формулировка решения вернуться", 1, "zolotaryov", "«В лесу переночуем, утром пойдём»", "«Ветер — как струя от самолёта. Вниз»", "«Дятлов прав, вниз»"),
                B("d4-tent", BeatKind.Cutscene, "Палатка в 16:00; последняя запись группового дневника", "zolotaryov", "«Тепло и уютно…» — близко к тексту."),
            },
            ["labaz"] = new[]
            {
                C("labaz-leave", "Лабаз: что оставить", 3, "", "Лишние продукты", "Запасные лыжи", "Часть снаряжения", "Мандолина", "Ботинки"),
                B("ascent", BeatKind.Play, "Подъём ~4 км с полным весом", "", "Выход поздним утром."),
                B("tent-pitch", BeatKind.Cutscene, "Установка палатки ~17:00; ужин; «Вечерний Отортен» № 1", "", "Последние кадры плёнок — установка палатки; ужин 18–19."),
                B("fade", BeatKind.Cutscene, "Темнеет. «Это всё, что известно. Дальше — вымышленная ночь»", ""),
            },
        };

        /// <summary>Documented diary lines that the flow inserts on the day they were written (paraphrased, dated; see docs/PROLOGUE.md).</summary>
        public static readonly IReadOnlyList<DiaryLine> Documented = new List<DiaryLine>
        {
            new DiaryLine { Date = new DateTime(1959, 1, 28), Author = "Зина", Text = "Ночь в единственном тёплом доме. Утром −8.", Documented = true },
            new DiaryLine { Date = new DateTime(1959, 1, 28), Author = "Юра Юдин", Text = "Вернулся. Такая жалость.", Documented = true },
            new DiaryLine { Date = new DateTime(1959, 1, 29), Author = "Коля", Text = "P.S. Бездарное письмо за два дня!", Documented = true },
            new DiaryLine { Date = new DateTime(1959, 1, 30), Author = "Саша Колеватов", Text = "Греемся у костра и идём спать.", Documented = true },
            new DiaryLine { Date = new DateTime(1959, 1, 31), Author = "Игорь", Text = "Тепло и уютно. Трудно представить такое на хребте, под воем ветра.", Documented = true },
            new DiaryLine { Date = new DateTime(1959, 2, 1), Author = "«Вечерний Отортен» № 1", Text = "Спорт: рекорд по сборке печки.", Documented = true },
        };

        public static Beat[] BeatsOf(string missionId) => Beats.TryGetValue(missionId, out var b) ? b : throw new KeyNotFoundException(missionId);
    }

    /// <summary>State of one play-through of the prologue: which mission and beat, what was chosen, what went into the diary.
    /// Deterministic and engine-free so the protocol can be produced (and tested) without Unity.</summary>
    public sealed class PrologueRun
    {
        public int MissionIndex { get; private set; }
        public int BeatIndex { get; private set; }
        public bool Finished => MissionIndex >= Campaign.Missions.Count;
        public readonly Dictionary<string, string[]> Choices = new Dictionary<string, string[]>();
        public readonly List<DiaryLine> Diary = new List<DiaryLine>();
        /// <summary>Fatigue per member id, 0..1, accumulated by trek beats and reduced by nights with the stove.</summary>
        public readonly Dictionary<string, float> Fatigue = new Dictionary<string, float>();
        /// <summary>Kilograms left at the labaz (documented list, player picks); affects weight on the ascent only.</summary>
        public float LabazKilograms { get; private set; }
        /// <summary>Best stove assembly time from mission 1, seconds; printed in «Вечерний Отортен».</summary>
        public float StoveSeconds { get; set; } = float.NaN;

        public Mission Mission => Finished ? null : Campaign.Missions[MissionIndex];
        public Beat Beat => Finished ? null : Prologue.BeatsOf(Mission.Id)[BeatIndex];
        public string Lead => Beat == null ? "" : (Beat.Lead != "" ? Beat.Lead : Mission.Leads[0]);

        public PrologueRun() { foreach (var m in Campaign.Roster) Fatigue[m.Id] = 0f; }

        /// <summary>Advance past a Cutscene or Play beat. A Choice beat must go through Choose.</summary>
        public void Advance()
        {
            if (Finished) throw new InvalidOperationException("prologue finished");
            if (Beat.Kind == BeatKind.Choice) throw new InvalidOperationException("beat " + Beat.Id + " needs a choice");
            ApplyPlay(Beat);
            Next();
        }

        public void Choose(params int[] optionIndexes)
        {
            if (Finished) throw new InvalidOperationException("prologue finished");
            var beat = Beat;
            if (beat.Kind != BeatKind.Choice) throw new InvalidOperationException("beat " + beat.Id + " has no choice");
            if (optionIndexes.Length != beat.Picks) throw new ArgumentException("beat " + beat.Id + " expects " + beat.Picks + " pick(s)");
            var picked = new string[optionIndexes.Length];
            for (int i = 0; i < optionIndexes.Length; i++)
            {
                if (optionIndexes[i] < 0 || optionIndexes[i] >= beat.Options.Length) throw new ArgumentOutOfRangeException(nameof(optionIndexes));
                for (int j = 0; j < i; j++) if (optionIndexes[j] == optionIndexes[i]) throw new ArgumentException("duplicate pick");
                picked[i] = beat.Options[optionIndexes[i]];
            }
            Choices[beat.Id] = picked;
            if (beat.Id == "d3-diary") foreach (var p in picked) Diary.Add(new DiaryLine { Date = new DateTime(1959, 1, 30), Author = Campaign.Find("kolevatov").Short + " Колеватов", Text = p });
            if (beat.Id == "d4-decision") Diary.Add(new DiaryLine { Date = new DateTime(1959, 1, 31), Author = Campaign.Find("zolotaryov").Name, Text = picked[0] });
            if (beat.Id == "labaz-leave") { LabazKilograms = 0; foreach (var p in picked) LabazKilograms += LabazWeight(p); }
            if (beat.Id == "yudin-leg" && picked[0] == "Идти") Fatigue["yudin"] = Math.Min(1f, Fatigue["yudin"] + .4f);
            Next();
        }

        static float LabazWeight(string item)
        {
            switch (item)
            {
                case "Лишние продукты": return 55f;
                case "Запасные лыжи": return 6f;
                case "Часть снаряжения": return 12f;
                case "Мандолина": return 1.5f;
                case "Ботинки": return 2f;
                default: return 0f;
            }
        }

        void ApplyPlay(Beat beat)
        {
            if (beat.Kind != BeatKind.Play) return;
            string lead = Lead;
            if (Mission.Mode == MissionMode.Trek)
            {
                // The leader breaks trail and tires fastest; the chain behind tires half as much. Documented weather makes 30–31 January harder.
                float day = beat.Id.StartsWith("d3") || beat.Id.StartsWith("d4") || Mission.Id == "labaz" ? .35f : .2f;
                foreach (var m in Campaign.Roster)
                {
                    if (m.LeftAtSecondNorthern) continue;
                    Fatigue[m.Id] = Math.Min(1f, Fatigue[m.Id] + (m.Id == lead ? day : day * .5f));
                }
            }
        }

        void Next()
        {
            var beats = Prologue.BeatsOf(Mission.Id);
            // A cutscene night with the stove restores the chain.
            string id0 = beats[BeatIndex].Id;
            if (id0 == "d1-camp" || id0 == "d2-birthday" || id0 == "d4-tent" || id0 == "village-night")
                foreach (var m in Campaign.Roster)
                    if (!m.LeftAtSecondNorthern || Mission.Id == "second-northern") Fatigue[m.Id] = Math.Max(0f, Fatigue[m.Id] - .3f);
            BeatIndex++;
            if (BeatIndex >= beats.Length)
            {
                var m = Mission;
                foreach (var line in Prologue.Documented)
                    if (line.Date >= m.Date && line.Date <= m.DateEnd && !Diary.Contains(line)) Diary.Add(line);
                BeatIndex = 0;
                MissionIndex++;
            }
        }

        /// <summary>The prologue protocol, saved next to the night protocols. Plain text, dates first, no evaluation.</summary>
        public string Protocol()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ПРОЛОГ · 23 января — 1 февраля 1959");
            foreach (var m in Campaign.Missions)
            {
                sb.Append(m.Number).Append(". ").Append(m.Title).Append(" — ").Append(m.Date.ToString("dd.MM"));
                if (m.DateEnd != m.Date) sb.Append("–").Append(m.DateEnd.ToString("dd.MM"));
                sb.AppendLine();
                foreach (var b in Prologue.BeatsOf(m.Id))
                    if (b.Kind == BeatKind.Choice && Choices.TryGetValue(b.Id, out var picked))
                        sb.Append("   ").Append(b.Title).Append(": ").AppendLine(string.Join(", ", picked));
            }
            if (!float.IsNaN(StoveSeconds)) sb.Append("Печка: ").Append(StoveSeconds.ToString("0")).AppendLine(" с");
            if (LabazKilograms > 0) sb.Append("Лабаз: ").Append(LabazKilograms.ToString("0")).AppendLine(" кг");
            sb.AppendLine("Дневник:");
            Diary.Sort((a, b) => a.Date.CompareTo(b.Date));
            foreach (var line in Diary) sb.Append("   ").AppendLine(line.ToString());
            return sb.ToString();
        }
    }
}
