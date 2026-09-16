using System;
using System.Collections.Generic;

namespace Height1079.Core
{
    /// <summary>The documented expedition members. Facts only (age at January 1959, faculty/role, documented traits) — see docs/PROLOGUE.md.
    /// Real names are used in the prologue and the archive; the night on the slope keeps its fictional pair.</summary>
    public sealed class Member
    {
        public string Id = "", Name = "", Short = "", Role = "";
        public int Age;
        /// <summary>Documented, source-backed traits that drive behaviour and look.</summary>
        public string[] Traits = Array.Empty<string>();
        public bool LeftAtSecondNorthern;
    }

    public enum MissionMode { Cutscene, Walk, Trek }

    public sealed class Mission
    {
        public int Number;
        public string Id = "", Title = "", Location = "";
        public DateTime Date, DateEnd;
        public MissionMode Mode;
        /// <summary>Member ids the player controls, in order.</summary>
        public string[] Leads = Array.Empty<string>();
        public string Summary = "";
    }

    public static class Campaign
    {
        static Member M(string id, string name, string shortName, int age, string role, bool left, params string[] traits)
            => new Member { Id = id, Name = name, Short = shortName, Age = age, Role = role, LeftAtSecondNorthern = left, Traits = traits };

        public static readonly IReadOnlyList<Member> Roster = new List<Member>
        {
            M("dyatlov", "Игорь Дятлов", "Игорь", 23, "руководитель; 5-й курс радиофака УПИ", false, "конструктор: рация, подвесная печка", "ведёт маршрутную книжку", "распределяет обязанности"),
            M("kolmogorova", "Зинаида Колмогорова", "Зина", 22, "5-й курс радиофака УПИ", false, "вела дневник", "разговаривала со школьниками в Серове", "держит группу вместе"),
            M("dubinina", "Людмила Дубинина", "Люда", 20, "3-й курс инженерно-экономического УПИ; завхоз", false, "вела дневник", "деньги и продукты", "ранение в походе 1957 года", "фотографировала"),
            M("slobodin", "Рустем Слободин", "Рустик", 23, "инженер, выпускник мехфака (1958); предприятие п/я 10", false, "спортсмен", "мандолина", "валенки"),
            M("doroshenko", "Юрий Дорошенко", "Юра", 20, "4-й курс радиофака УПИ", false, "история с медведем и геологическим молотком", "берёт тяжёлое", "день рождения 29 января — на лыжне"),
            M("krivonishchenko", "Георгий Кривонищенко", "Юра", 23, "инженер, выпускник стройфака; Челябинск-40", false, "шутник", "мандолина", "задержан на вокзале Серова за шуточное пение", "фотографировал"),
            M("thibeaux", "Николай Тибо-Бриньоль", "Коля", 23, "инженер-строитель (1958); трест в Свердловске", false, "родился в лагере", "ироничный", "любимец группы"),
            M("kolevatov", "Александр Колеватов", "Саша", 24, "4-й курс физтеха УПИ", false, "аккуратист", "трубка", "вёл записи", "лаборант в Москве до УПИ"),
            M("zolotaryov", "Семён Золотарёв", "Саша", 37, "инструктор Коуровской турбазы; фронтовик", false, "старше всех", "38 исполнилось бы 2 февраля", "татуировки", "присоединился перед выходом", "держится отдельно, профессионально"),
            M("yudin", "Юрий Юдин", "Юра", 21, "4-й курс инженерно-экономического УПИ", true, "ревматизм", "сошёл 28 января на 2-м Северном", "собирал образцы минералов", "единственный выживший"),
        };

        static Mission Ms(int n, string id, string title, string location, DateTime date, DateTime? end, MissionMode mode, string summary, params string[] leads)
            => new Mission { Number = n, Id = id, Title = title, Location = location, Date = date, DateEnd = end ?? date, Mode = mode, Summary = summary, Leads = leads };

        static DateTime D(int day, int month = 1) => new DateTime(1959, month, day);

        public static readonly IReadOnlyList<Mission> Missions = new List<Mission>
        {
            Ms(1, "route-book", "Маршрутная книжка", "Свердловск: УПИ, спортклуб, общежитие", D(23), null, MissionMode.Walk, "Получить маршрутную книжку, проверить состав, собрать печку и продукты. Знакомство с Золотарёвым.", "dyatlov"),
            Ms(2, "train", "Поезд 23-го", "Вокзал Свердловска, плацкартный вагон", D(23), D(24), MissionMode.Cutscene, "Ночь в поезде: полки, мандолина, разговор с Золотарёвым. Рассвет в Серове.", "kolmogorova"),
            Ms(3, "serov", "Серов", "Вокзал Серова, отделение, школа", D(24), null, MissionMode.Walk, "Кривонищенко задержан за шуточное пение; группа в школе отвечает детям. Вечером поезд на Ивдель.", "krivonishchenko", "kolmogorova"),
            Ms(4, "ivdel-vizhay", "Ивдель — Вижай", "Ночной вокзал Ивделя, автобус, гостиница в Вижае", D(25), null, MissionMode.Cutscene, "Автобус в Вижай, ночлег, деньги и продукты у завхоза; вечером кино в клубе.", "dubinina"),
            Ms(5, "quarter-41", "41-й квартал", "Кузов грузовика, посёлок лесозаготовителей, барак", D(26), null, MissionMode.Walk, "Грузовик по тракту, разгрузка, договор о подводе, вечер с рабочими.", "thibeaux"),
            Ms(6, "second-northern", "2-й Северный", "Подвода по Лозьве, заброшенный посёлок геологов", D(27), D(28), MissionMode.Walk, "Ночь в единственном тёплом доме; Юдин собирает образцы и возвращается. Прощание со всеми девятью.", "yudin"),
            Ms(7, "lozva-auspiya", "Лозьва — Ауспия", "Русло Лозьвы, устье Ауспии, тайга, ночёвки", D(28), D(31), MissionMode.Trek, "Четыре дня лыжни: тропление, ремонт, дневник, попытка выйти на перевал и возвращение в долину.", "doroshenko", "slobodin", "kolevatov", "zolotaryov"),
            Ms(8, "labaz", "Лабаз", "Долина Ауспии → склон Холатчахля", D(1, 2), null, MissionMode.Trek, "Лабаз утром, подъём днём, палатка на склоне около 17:00. Последний номер «Вечернего Отортена». Дальше — вымышленная ночь.", "dyatlov"),
        };

        public static Member Find(string id)
        {
            foreach (var m in Roster) if (m.Id == id) return m;
            throw new KeyNotFoundException(id);
        }
    }
}
