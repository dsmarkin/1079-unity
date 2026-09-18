using System;
using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    /// <summary>The guide's programme. What is checked here is not «does the list exist» but the three promises it
    /// makes: a step ticks when its condition holds and never before, the order can be broken and the programme
    /// follows rather than argues, and the whole thing survives being written to disk and read back — including by a
    /// build that has never heard of it.</summary>
    public class ProgrammeTests
    {
        // ── the standings the tests walk through ──────────────────────────────────────────────────────────

        static Standing Where(string poiId, float ele = float.NaN)
        {
            var p = Elbrus.Get(poiId);
            return new Standing
            {
                X = p.X, Z = p.Z,
                Ele = float.IsNaN(ele) ? p.Ele : ele,
                Highest = float.IsNaN(ele) ? p.Ele : ele,
                Date = new DateTime(2026, 7, 14),
            };
        }

        static Standing High(float ele) => new Standing
        {
            X = Elbrus.Barrels.X, Z = Elbrus.Barrels.Z, Ele = ele, Highest = ele,
            Date = new DateTime(2026, 7, 14),
        };

        static int Ix(string id) => Programme.IndexOf(id);
        static bool Has(Progress p, string id) => p.IsDone(Ix(id));

        /// <summary>Walks the whole sheet in order, the way a client who does as he is told would. Returns the
        /// progress after every step of the table has been met.</summary>
        static Progress WalkTheProgramme()
        {
            var p = Progress.None;

            // день 1
            p = Programme.Advance(p, Where("terskol"));
            var kit = Where("azau");
            kit.Kit = Programme.Iron;
            p = Programme.Advance(p, kit);
            p = Programme.Slept(p, 2261f, 2261f);
            p = Programme.Advance(p, Where("terskol"));

            // день 2
            p = Programme.Advance(p, Where("krugozor", 2938f));
            p = Programme.Slept(p, 2261f, 2938f);
            p = Programme.Advance(p, Where("terskol"));

            // день 3
            p = Programme.Advance(p, Where("devichiKosy", 3060f));
            p = Programme.Advance(p, Where("terskolObs", 3088f));
            p = Programme.Slept(p, 2261f, 3088f);
            p = Programme.Advance(p, Where("terskol"));

            // день 4
            p = Programme.Advance(p, Where("garabashi", 3842f));
            var drill = High(3750f);
            drill.CramponsOn = true;
            p = Programme.Advance(p, drill);
            p = Programme.Slept(p, 3702f, 3842f);
            p = Programme.Advance(p, High(3702f));

            // день 5
            p = Programme.Advance(p, High(4150f));
            p = Programme.Slept(p, 3702f, 4150f);
            p = Programme.Advance(p, High(3702f));

            // день 6
            p = Programme.Advance(p, High(4700f));
            p = Programme.Slept(p, 3702f, 4700f);
            p = Programme.Advance(p, High(3702f));

            // день 7
            var desk = Where("azau");
            desk.ForecastRead = true;
            desk.Registered = true;
            desk.Kit = Ascent.Required;
            p = Programme.Advance(p, desk);
            p = Programme.Slept(p, 3702f, 3702f);
            p = Programme.Advance(p, High(3702f));

            // день 8
            var off = High(3800f);
            off.Hour = AscentRoute.RunStartHour;
            p = Programme.Advance(p, off);
            p = Programme.Advance(p, Where("westSummit", 5642f));
            var home = High(3900f);
            home.Highest = 5642f;
            p = Programme.Advance(p, home);
            return p;
        }

        // ── the sheet ─────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void TheSheetIsNineDaysAndEveryStepIsSayableOutLoud()
        {
            Assert.AreEqual(9, Programme.LastDay, "восемь рабочих дней и резерв");
            Assert.AreEqual(22, Programme.Count);

            for (int day = 1; day <= 8; day++)
                Assert.Greater(Programme.StepsOf(day).Length, 0, "у дня " + day + " должны быть шаги");
            Assert.AreEqual(0, Programme.StepsOf(9).Length, "девятый день — резерв, в нём нечего делать");
            StringAssert.Contains("непогод", Programme.DayOf(9).Note);

            var seen = new System.Collections.Generic.HashSet<string>();
            var steps = Programme.Steps();
            for (int i = 0; i < steps.Length; i++)
            {
                var s = steps[i];
                Assert.AreEqual(i, s.Index, "индекс шага — это его бит в сейве");
                Assert.IsTrue(seen.Add(s.Id), "id шага «" + s.Id + "» встречается дважды");
                Assert.IsTrue(s.Title.Length > 0 && s.Title.Length <= 26, s.Id + ": заголовок карточки короткий");
                Assert.Greater(s.Line.Length, 12, s.Id + ": строка шага — это указание, а не слово");
                Assert.Greater(s.Why.Length, 180, s.Id + ": объяснение «зачем» — это и есть обучение");
                Assert.Greater(s.Test.Length, 8, s.Id + ": условие должно быть сказано словами");
                Assert.IsTrue(s.Day >= 1 && s.Day <= 8, s.Id + ": шаг лежит в одном из восьми дней");
                Assert.IsNotNull(s.Rule);
            }

            // дни идут по порядку и не перемешаны
            for (int i = 1; i < steps.Length; i++)
                Assert.GreaterOrEqual(steps[i].Day, steps[i - 1].Day, "шаги лежат по дням, по возрастанию");

            // это программа гида, а не квестлог
            var sheet = Programme.SheetText();
            StringAssert.Contains("скалам Пастухова, 4650", sheet);
            StringAssert.Contains("ЭВПСО", Programme.Get("register").Line);
            // в программе восхождения не выдают наград, опыта и уровней — она сообщает высоты и время
            string[] rpg = { "награ", "опыт", "уровен", "бонус", "очков опыта", "задани" };
            foreach (var s in steps)
                foreach (var word in rpg)
                {
                    Assert.IsFalse(s.Line.Contains(word), s.Id + ": «" + word + "» в строке шага");
                    Assert.IsFalse(s.Why.Contains(word), s.Id + ": «" + word + "» в объяснении");
                }
        }

        [Test]
        public void TheProgrammeLeansOnTheRulesThatAreAlreadyWritten()
        {
            // «climb high, sleep low» — это Ascent.SleepLowerBy, а не своя копия правила
            Assert.AreEqual(Ascent.TouchHighFrom, Programme.RocksEle, 1e-6, "шестой день — это касание 4600");
            Assert.AreEqual(AscentRoute.LastHutEle, Programme.RestCeilingEle, 1e-6);
            Assert.AreEqual(Ascent.Required, Ascent.Required & Programme.Iron | Ascent.Required,
                "железо первого дня — часть гейта на 4650");
            Assert.AreEqual(Gear.None, Ascent.Missing(Programme.Iron) & Programme.Iron);

            // высоты шагов согласованы с тем, что меряет наше поле высот (docs/ELBRUS.md)
            Assert.Less(Programme.WalkHighEle, 2938f, "станция «3000» читается у нас на 2938");
            Assert.Less(Programme.ValleyLowEle, 2261f, "Терскол читается на 2261");
            Assert.Greater(Programme.ValleyHighEle, 2361f, "поляна Азау читается на 2361");
            Assert.Less(Programme.ShelfLowEle, 3702f, "бочки читаются на 3702");
            Assert.Greater(Programme.ShelfHighEle, 3909f, "LeapRus читается на 3909");
            Assert.Less(Programme.SummitTouchEle, 5642f);
        }

        // ── шаг засчитывается по условию и не засчитывается без него ──────────────────────────────────────

        [Test]
        public void AStepTicksOnItsConditionAndOnNothingElse()
        {
            var p = Progress.None;

            // «дойти до Терскола» — только когда до Терскола дошли
            Assert.IsFalse(Programme.Holds("arrive", p, Where("azau")), "с Азау до посёлка три километра");
            Assert.IsTrue(Programme.Holds("arrive", p, Where("terskol")));
            Assert.IsFalse(Programme.Advance(p, Where("azau")).IsDone(Ix("arrive")));
            Assert.IsTrue(Programme.Advance(p, Where("terskol")).IsDone(Ix("arrive")));

            // «взять железо» — по рюкзаку, и половины не хватает
            var half = Where("azau");
            half.Kit = Gear.Crampons | Gear.IceAxe;
            Assert.IsFalse(Programme.Holds("hire", p, half));
            half.Kit = Programme.Iron;
            Assert.IsTrue(Programme.Holds("hire", p, half));

            // «надеть кошки» — надеть, а не иметь в рюкзаке
            var firn = High(3750f);
            firn.Kit = Ascent.Required;
            Assert.IsFalse(Programme.Holds("crampons", p, firn), "кошки в рюкзаке — это не кошки на ногах");
            firn.CramponsOn = true;
            Assert.IsTrue(Programme.Holds("crampons", p, firn));
            var flat = High(3000f);
            flat.CramponsOn = true;
            Assert.IsFalse(Programme.Holds("crampons", p, flat), "внизу снега нет, цеплять нечего");

            // «записаться» и «прочитать прогноз» — два разных окна у одного стола
            var desk = Where("azau");
            Assert.IsFalse(Programme.Holds("register", p, desk));
            Assert.IsFalse(Programme.Holds("forecast", p, desk));
            desk.Registered = true;
            Assert.IsTrue(Programme.Holds("register", p, desk));
            Assert.IsFalse(Programme.Holds("forecast", p, desk));
            desk.ForecastRead = true;
            Assert.IsTrue(Programme.Holds("forecast", p, desk));

            // «комплект» — это весь гейт, а не железо
            var iron = Where("azau");
            iron.Kit = Programme.Iron;
            Assert.IsFalse(Programme.Holds("kit", p, iron));
            iron.Kit = Ascent.Required;
            Assert.IsTrue(Programme.Holds("kit", p, iron));

            // «вершина» — по расстоянию до точки, а не по высоте
            Assert.IsTrue(Programme.Holds("summit", p, Where("westSummit")));
            Assert.IsFalse(Programme.Holds("summit", p, Where("eastSummit")), "Восточная — это не Западная");
        }

        [Test]
        public void ANightCountsOnlyWhenItWasTakenLowAfterADayTakenHigh()
        {
            var p = Progress.None;
            var home = Where("terskol");

            Assert.IsFalse(Programme.Holds("night1", p, home), "без ночи ни один ночной шаг не идёт");

            // первая ночь — просто внизу
            p = Programme.Slept(p, 2261f, 2261f);
            Assert.IsTrue(Programme.Holds("night1", p, home));
            Assert.IsFalse(Programme.Holds("night2", p, home), "climb high, sleep low: выхода наверх не было");

            // выход на 3000 и ночь внизу — вот теперь да
            p = Programme.Slept(p, 2261f, 2938f);
            Assert.IsTrue(Programme.Holds("night2", p, home));
            Assert.IsTrue(p.Night.SleptLow, "678 м между максимумом дня и ночёвкой — это больше трёхсот");

            // та же ночь, но проведённая наверху, не считается ни за что из долины
            var up = Programme.Slept(Progress.None, 3910f, 4700f);
            Assert.IsFalse(Programme.Holds("night1", up, home));
            Assert.IsFalse(Programme.Holds("night2", up, home));
            Assert.IsTrue(Programme.Holds("settle", up, home), "ночь на полке 3650–3980 — это четвёртый день");
            Assert.IsTrue(Programme.Holds("night6", up, home), "после 4700 и ночёвки на 3910 — шестой день");
            Assert.IsFalse(Programme.Holds("night5", Programme.Slept(Progress.None, 3702f, 3900f), home),
                "выход на 3900 — это не выход к Приюту 11");

            // ночь на той же высоте, на которой ходили, не даёт ничего
            var same = Programme.Slept(Progress.None, 3910f, 3950f);
            Assert.IsFalse(Programme.Holds("night5", same, home));
            Assert.IsFalse(Programme.Holds("night6", same, home));
            Assert.IsFalse(same.Night.SleptLow);

            Assert.AreEqual(2, Programme.Nights(p), "ночи считаются");
        }

        [Test]
        public void TheRestDayIsADayYouDidNotClimbAndTheStartIsTheOneAfterIt()
        {
            // день отдыха не должен зачитываться в день переезда наверх: там максимум дня — станция 3842,
            // и он тоже ниже потолка. Разводит их то, что скалы Пастухова уже пройдены.
            var moveUp = Programme.Slept(Progress.None, 3702f, 3842f);
            Assert.IsFalse(Programme.Holds("rest", moveUp, High(3702f)), "четвёртый день — это не отдых");

            var afterRocks = Programme.Advance(Progress.None, High(4700f));
            Assert.IsTrue(Has(afterRocks, "rocks"));
            afterRocks = Programme.Slept(afterRocks, 3702f, 4700f);
            Assert.IsFalse(Programme.Holds("rest", afterRocks, High(3702f)), "день с выходом на 4700 — не отдых");
            afterRocks = Programme.Slept(afterRocks, 3702f, 3702f);
            Assert.IsTrue(Programme.Holds("rest", afterRocks, High(3702f)));

            // выход на штурм: затемно, от койки на полке, после скал Пастухова
            var dark = High(3800f);
            dark.Hour = AscentRoute.RunStartHour;
            Assert.IsTrue(AscentRoute.Dark(dark.Hour), "забег начинается затемно");
            Assert.IsTrue(Programme.Holds("start", afterRocks, dark));

            var late = High(3800f);
            late.Hour = 9f;
            Assert.IsFalse(Programme.Holds("start", afterRocks, late), "в девять утра это уже не штурм");

            var inBed = High(3720f);
            inBed.Hour = AscentRoute.RunStartHour;
            Assert.IsFalse(Programme.Holds("start", afterRocks, inBed), "выйти — значит уйти от приюта");

            // без акклиматизации это просто утренняя прогулка, и четвёртый день её не зачтёт
            var early = Programme.Slept(Progress.None, 3702f, 3842f);
            Assert.IsFalse(Programme.Holds("start", early, dark));
        }

        [Test]
        public void TheDescentIsPartOfTheDayAndNotAnAfterthought()
        {
            var p = Programme.Advance(Progress.None, Where("westSummit", 5642f));
            Assert.IsTrue(Has(p, "summit"));
            Assert.IsFalse(Has(p, "down"), "на вершине спуск ещё не сделан");

            var still = High(4800f);
            still.Highest = 5642f;
            Assert.IsFalse(Programme.Holds("down", p, still), "4800 — это ещё гора");

            var back = High(3900f);
            back.Highest = 5642f;
            Assert.IsTrue(Programme.Holds("down", p, back));

            var neverUp = High(3900f);
            Assert.IsFalse(Programme.Holds("down", p, neverUp), "спуск с вершины требует вершины");
        }

        // ── порядок можно нарушить ────────────────────────────────────────────────────────────────────────

        [Test]
        public void NothingIsLockedAndTheProgrammeJumpsForwardToWhereThePlayerIs()
        {
            // первое утро, ни одного шага не сделано — карточка показывает первый
            var p = Progress.None;
            Assert.AreEqual("arrive", Programme.Current(p).Id);
            Assert.AreEqual("hire", Programme.Next(p).Id);
            Assert.AreEqual(1, Programme.CurrentDay(p));

            // и человек уходит на вершину в первый же день
            p = Programme.Advance(p, Where("westSummit", 5642f));
            Assert.IsTrue(Has(p, "summit"), "условие выполнено — шаг засчитан, порядок ни при чём");
            Assert.IsFalse(Has(p, "arrive"), "и при этом в Терсколе он не был");

            // программа не спорит и не возвращает его назад: она перескакивает вперёд
            Assert.AreEqual("down", Programme.Current(p).Id);
            Assert.AreEqual(8, Programme.CurrentDay(p));
            Assert.IsTrue(Programme.LeftBehind(p, Ix("arrive")), "первый день остался позади");
            Assert.IsTrue(Programme.LeftBehind(p, Ix("hire")), "прокат тоже остался позади");
            Assert.IsTrue(Has(p, "rocks"), "по пути наверх он прошёл 4600, и это засчитано");
            Assert.IsFalse(Programme.LeftBehind(p, Ix("down")), "то, что впереди, позади не остаётся");

            var back = High(3900f);
            back.Highest = 5642f;
            p = Programme.Advance(p, back);
            Assert.IsTrue(Programme.Finished(p), "показывать больше нечего");
            Assert.IsFalse(Programme.Walked(p), "но пройденной программу это не делает");
            Assert.IsTrue(Programme.Current(p).IsEmpty, "текущего шага больше нет");
            Assert.IsTrue(Programme.Next(p).IsEmpty, "и следующего тоже");
            Assert.AreEqual(9, Programme.CurrentDay(p), "дальше только резервный день");
        }

        [Test]
        public void StepsOutOfOrderAreStillCountedWhenTheyHappen()
        {
            // регистрируется в первый день, хотя в программе это седьмой
            var desk = Where("azau");
            desk.Registered = true;
            var p = Programme.Advance(Progress.None, desk);
            Assert.IsTrue(Has(p, "register"));
            Assert.IsFalse(Has(p, "arrive"));

            // а потом всё-таки идёт по программе — и первый день догоняется как ни в чём не бывало
            p = Programme.Advance(p, Where("terskol"));
            Assert.IsTrue(Has(p, "arrive"), "шаг ниже уже сделанного тоже засчитывается");
            Assert.AreEqual(Ix("register"), p.Furthest);

            // карточка при этом смотрит вперёд, а не назад
            Assert.AreEqual("kit", Programme.Current(p).Id);
        }

        [Test]
        public void OneTickCanCloseSeveralStepsAtOnce()
        {
            var all = Where("azau");
            all.Kit = Ascent.Required;
            all.Registered = true;
            all.ForecastRead = true;
            var after = Programme.Advance(Progress.None, all);
            var fresh = Programme.NewlyDone(Progress.None, after);
            Assert.AreEqual(4, fresh.Length, "прокат, прогноз, регистрация и комплект — всё у одной стойки");
            Assert.AreEqual("hire", fresh[0].Id, "новые шаги приходят по порядку таблицы");
            Assert.AreEqual("kit", fresh[3].Id);
            Assert.AreEqual(0, Programme.NewlyDone(after, after).Length);
        }

        [Test]
        public void TheWholeProgrammeWalkedThroughEndsAsWalked()
        {
            var p = WalkTheProgramme();
            var steps = Programme.Steps();
            for (int i = 0; i < steps.Length; i++)
                Assert.IsTrue(p.IsDone(i), "не засчитан шаг «" + steps[i].Id + "»");

            Assert.AreEqual(Programme.Count, p.Count);
            Assert.AreEqual(1f, Programme.Share(p), 1e-6);
            Assert.IsTrue(Programme.Walked(p), "после восьмого дня программа пройдена");
            Assert.IsTrue(Programme.Finished(p));
            Assert.AreEqual(9, Programme.CurrentDay(p), "дальше остаётся только резервный день");
            Assert.AreEqual(7, Programme.Nights(p), "восемь дней — семь ночей");
            StringAssert.Contains("Программа пройдена", Programme.CardText(p, High(3702f)));
        }

        // ── день программы и дата сейва ───────────────────────────────────────────────────────────────────

        [Test]
        public void TheProgrammeDayIsNotTheDateAndTheHudSaysSoWhenTheyDiverge()
        {
            var start = new DateTime(2026, 7, 14);
            var first = Where("terskol");
            first.Date = start;
            var p = Programme.Advance(Progress.None, first);
            Assert.AreEqual(start, p.Started, "программа начата в тот день, когда впервые что-то засчиталось");
            Assert.AreEqual(1, Programme.CalendarDay(p, start));
            Assert.AreEqual(0, Programme.Behind(p, start));
            Assert.AreEqual("", Programme.PaceText(p, start), "пока всё вовремя, говорить нечего");

            // два захода в один день ничего не двигают: дата не сдвинулась, шаг не сделан
            var again = Where("azau");
            again.Date = start;
            p = Programme.Advance(p, again);
            Assert.AreEqual(1, Programme.CalendarDay(p, start));
            Assert.AreEqual(1, Programme.CurrentDay(p));

            // а вот три ночи в Терсколе сдвигают дату и не сдвигают программу
            var third = start.AddDays(3);
            Assert.AreEqual(4, Programme.CalendarDay(p, third));
            Assert.AreEqual(1, Programme.CurrentDay(p), "программа стоит там, где стоят её шаги");
            Assert.AreEqual(3, Programme.Behind(p, third));
            StringAssert.Contains("программа — день 1", Programme.PaceText(p, third));
            StringAssert.Contains("отставание 3 дня", Programme.PaceText(p, third));

            // и наоборот: два дня программы за одни сутки — отставания нет
            var fast = Programme.Advance(WalkTheProgramme(), first);
            Assert.AreEqual(0, Programme.Behind(fast, start.AddDays(2)));

            // у программы, которую ещё не начинали, сутки первые
            Assert.AreEqual(1, Programme.CalendarDay(Progress.None, start));
        }

        // ── пеленг и отметка на карте ─────────────────────────────────────────────────────────────────────

        [Test]
        public void EveryStepGivesTheCompassSomethingToPointAt()
        {
            float ax = Elbrus.Azau.X, az = Elbrus.Azau.Z;
            foreach (var s in Programme.Steps())
            {
                var aim = Programme.Aim(s, ax, az);
                Assert.IsTrue(aim.Has, s.Id + ": у каждого шага есть цель в мире");
                Assert.Greater(aim.Label.Length, 3, s.Id);
                Assert.IsTrue(aim.HeadingDeg >= 0f && aim.HeadingDeg < 360f, s.Id);
                Assert.IsTrue(Elbrus.Inside(aim.X, aim.Z), s.Id + ": цель лежит на карте");
                StringAssert.Contains(aim.Label, aim.Line);
            }

            // точка — это точка
            var summit = Programme.Aim(Programme.Get("summit"), ax, az);
            Assert.AreEqual(Elbrus.WestSummit.X, summit.X, .1f);
            Assert.AreEqual(Elbrus.WestSummit.Z, summit.Z, .1f);
            Assert.AreEqual(Elbrus.Distance(ax, az, Elbrus.WestSummit.X, Elbrus.WestSummit.Z), summit.DistanceM, .1f);

            // север — ноль, восток — девяносто
            Assert.AreEqual(0f, Programme.HeadingDeg(0, 0, 0, 100), 1e-3);
            Assert.AreEqual(90f, Programme.HeadingDeg(0, 0, 100, 0), 1e-3);
            Assert.AreEqual(180f, Programme.HeadingDeg(0, 0, 0, -100), 1e-3);
            Assert.AreEqual(270f, Programme.HeadingDeg(0, 0, -100, 0), 1e-3);
            Assert.IsTrue(summit.HeadingDeg > 330f || summit.HeadingDeg < 30f,
                "от Азау Западная вершина почти точно на север, а вышло " + summit.HeadingDeg);

            // «любой приют выше 3800» — это ближайший из подходящих, а не один назначенный
            var fromBarrels = Programme.Aim(Programme.Get("settle"), Elbrus.Barrels.X, Elbrus.Barrels.Z);
            Assert.Less(fromBarrels.DistanceM, 60f, "от бочек ближайшая койка — сами бочки");
            var fromPriut = Programme.Aim(Programme.Get("settle"), Elbrus.Priut.X, Elbrus.Priut.Z);
            Assert.Less(fromPriut.DistanceM, 200f, "от Приюта 11 — приюты на той же морене");

            // «в долину» — ближайшее из двух мест, где ночуют внизу
            var night = Programme.Get("night1");
            Assert.Less(Programme.Aim(night, Elbrus.Azau.X, Elbrus.Azau.Z).DistanceM, 200f);
            var terskol = Elbrus.Get("terskol");
            Assert.Less(Programme.Aim(night, terskol.X, terskol.Z).DistanceM, 100f);

            Assert.AreEqual("240 м", Programme.Far(238f));
            Assert.AreEqual("1,2 км", Programme.Far(1234f));
        }

        [Test]
        public void TheCardSaysWhatToDoNowAndWhereItIs()
        {
            var at = Where("azau");
            var text = Programme.CardText(Progress.None, at);
            StringAssert.Contains("День 1", text);
            StringAssert.Contains("Терскол", text);
            StringAssert.Contains("Дальше:", text);
            // до посёлка от Азау — километры, и это должно быть видно
            StringAssert.Contains("км", text);
        }

        // ── вторая локация ────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void ThereIsNoProgrammeOnKholat()
        {
            Assert.IsTrue(Programme.AllowedIn(Place.Elbrus));
            Assert.IsFalse(Programme.AllowedIn(Place.Kholat));
            Assert.AreEqual(0, Programme.Steps(Place.Kholat).Length);
            Assert.AreEqual(Programme.Count, Programme.Steps(Place.Elbrus).Length);

            var night = new Standing { Place = Place.Kholat, X = 0, Z = 0, Ele = 1079f, Highest = 5642f };
            night.Kit = Ascent.Required;
            night.Registered = true;
            night.ForecastRead = true;
            night.CramponsOn = true;

            Assert.AreEqual(Progress.None.Done, Programme.Advance(Progress.None, night).Done,
                "на Холатчахле программа молчит, что бы ни лежало в рюкзаке");
            Assert.IsFalse(Programme.Holds("kit", Progress.None, night));
            Assert.IsTrue(Programme.Current(Progress.None, Place.Kholat).IsEmpty);
            Assert.IsTrue(Programme.Next(Progress.None, Place.Kholat).IsEmpty);
            Assert.AreEqual("", Programme.CardText(Progress.None, night));
            Assert.AreEqual("", Programme.SheetText(Place.Kholat));

            // и та же самая стоянка на Эльбрусе программу, конечно, двигает
            night.Place = Place.Elbrus;
            Assert.IsTrue(Programme.Advance(Progress.None, night).Count > 0);
        }

        // ── сейв ──────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void ProgressSurvivesTheRoundTripThroughJson()
        {
            var p = Programme.Advance(Progress.None, Where("terskol"));
            p = Programme.Slept(p, 2261f, 3088f);
            p = Programme.Advance(p, Where("terskol"));
            p = Programme.Advance(p, High(4700f));
            Assert.IsTrue(Has(p, "rocks"));

            var back = Progress.FromJson(JsonValue.Parse(p.ToJson().ToJson()));
            Assert.AreEqual(p.Done, back.Done);
            Assert.AreEqual(p.Count, back.Count);
            Assert.AreEqual(p.Started, back.Started);
            Assert.AreEqual(p.Night.Count, back.Night.Count);
            Assert.AreEqual(p.Night.Ele, back.Night.Ele, .1f);
            Assert.AreEqual(p.Night.FromEle, back.Night.FromEle, .1f);
            Assert.AreEqual(Programme.Current(p).Id, Programme.Current(back).Id);

            // шаги пишутся именами, а не номерами: таблица может поменяться, сейв не должен съехать
            var text = p.ToJson().ToJson();
            StringAssert.Contains("\"arrive\"", text);
            StringAssert.Contains("\"rocks\"", text);
            StringAssert.Contains("2026-07-14", text);

            // чужой или устаревший id просто не читается
            var odd = Progress.FromJson(JsonValue.Parse(
                "{\"done\":[\"arrive\",\"такого-шага-нет\",17,null,\"summit\"]}"));
            Assert.AreEqual(2, odd.Count);
            Assert.IsTrue(Has(odd, "arrive"));
            Assert.IsTrue(Has(odd, "summit"));
            Assert.AreEqual(0, odd.Night.Count);
            Assert.AreEqual(default(DateTime), odd.Started);

            // пустая программа не занимает места
            Assert.IsTrue(Progress.None.IsEmpty);
            Assert.AreEqual(Progress.None.Done, Progress.FromJson(null).Done);
            Assert.AreEqual(0, Progress.FromJson(JsonValue.Parse("\"нет\"")).Count);
        }

        [Test]
        public void TheSaveCarriesTheProgrammeAndAnOlderFileStillReads()
        {
            var save = new SaveGame
            {
                Place = Place.Elbrus,
                Date = new DateTime(2026, 7, 18),
                Where = "бочки, 3710 м",
            };
            var don = save.Ensure("Дон", "Дон");
            don.Programme = WalkTheProgramme();
            var friend = save.Ensure("Кот", "Кот");

            Assert.AreEqual(2, SaveGame.Schema, "поле добавлено — схема поднята");
            var text = save.Text;
            StringAssert.Contains("\"programme\"", text);
            StringAssert.Contains("\"summit\"", text);

            var read = SaveGame.Parse(text);
            Assert.IsNotNull(read);
            Assert.AreEqual(SaveGame.Schema, read.SchemaVersion);
            Assert.IsTrue(Programme.Walked(read.Find("Дон").Programme), "прогресс доехал целиком");
            Assert.AreEqual(don.Programme.Night.Count, read.Find("Дон").Programme.Night.Count);
            Assert.IsTrue(read.Find("Кот").Programme.IsEmpty, "у кого программы нет, у того её и не появится");

            // файл старой схемы читается, и программа в нём просто не начата
            var old = SaveGame.Parse(
                "{\"schema\":1,\"place\":\"elbrus\",\"date\":\"2026-07-18\",\"elapsed\":600,"
                + "\"camp\":{\"x\":10,\"z\":20,\"ele\":3710},"
                + "\"climbers\":[{\"key\":\"Дон\",\"acclim\":0.6,\"pack\":[{\"id\":\"Crampons\"}]}]}");
            Assert.IsNotNull(old, "сейв предыдущей схемы должен открываться");
            Assert.AreEqual(1, old.SchemaVersion);
            var him = old.Find("Дон");
            Assert.AreEqual(.6f, him.Acclim, 1e-4, "всё остальное прочиталось как раньше");
            Assert.AreEqual(Gear.Crampons, him.Kit);
            Assert.IsTrue(him.Programme.IsEmpty);
            Assert.AreEqual("arrive", Programme.Current(him.Programme).Id, "он в самом начале, и это правда");
            Assert.AreEqual(0, Programme.Nights(him.Programme));

            // а сейв из будущего по-прежнему не открывается, а не открывается наполовину
            Assert.IsNull(SaveGame.Parse("{\"schema\":" + (SaveGame.Schema + 1) + ",\"place\":\"elbrus\"}"));
        }

        [Test]
        public void TheRuntimeRecordsANightWhereItAlreadyComputesTheSortie()
        {
            // ровно тот же Sortie, который уходит в Ascent.Acclimatise
            var sortie = new Ascent.Sortie(4700f, 3702f);
            var p = Programme.Slept(Progress.None, sortie);
            Assert.AreEqual(3702f, p.Night.Ele, 1e-3);
            Assert.AreEqual(4700f, p.Night.FromEle, 1e-3);
            Assert.AreEqual(1, p.Night.Count);
            Assert.Greater(Ascent.Acclimatise(.4f, sortie), .4f, "и акклиматизация считает ту же ночь");

            // ночь выше дневного максимума — максимумом становится сама ночёвка
            var odd = Programme.Slept(Progress.None, 3910f, 3000f);
            Assert.AreEqual(3910f, odd.Night.FromEle, 1e-3);

            // Standing собирается из сейва одной строкой
            var save = new SaveGame { Place = Place.Elbrus, Date = new DateTime(2026, 7, 18) };
            var c = save.Ensure("Дон", "Дон");
            c.Highest = 4700f;
            c.Pack.Add(new ItemStack(ItemId.Crampons));
            c.Reg = Rescue.File("Дон", 1);
            var now = Standing.Of(save, c, Elbrus.Barrels.X, Elbrus.Barrels.Z, 3702f, true, true);
            Assert.AreEqual(Place.Elbrus, now.Place);
            Assert.AreEqual(4700f, now.Highest, 1e-3);
            Assert.AreEqual(Gear.Crampons, now.Kit);
            Assert.IsTrue(now.Registered);
            Assert.IsTrue(now.CramponsOn);
            Assert.IsTrue(now.ForecastRead);
            Assert.AreEqual(save.Date, now.Date);
            Assert.IsTrue(Programme.Advance(Progress.None, now).IsDone(Ix("rocks")));
        }
    }
}
