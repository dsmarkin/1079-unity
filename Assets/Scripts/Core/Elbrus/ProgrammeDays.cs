using System;

namespace Height1079.Core
{
    /// <summary>The sheet itself: nine days, twenty-two steps, and the reason for every one of them.
    ///
    /// The schedule is the ordinary southern-side one guides run — three nights in the valley with two
    /// acclimatisation outings, a move up to the barrels, two radials from them, a rest day, the summit night, and a
    /// day in hand for the weather. Chegette, which the second day usually uses, is 444 m off the west edge of our
    /// square (docs/ELBRUS.md), so the 3 000-metre outing is the Krugozor station or the gorge above the village —
    /// the same height, the same purpose.
    ///
    /// The texts are written the way a programme is written and not the way a quest log is: an imperative line with
    /// a height in it, and underneath it the reason, because the reason is the entire tutorial. Nobody is asked to
    /// fetch anything and nothing is rewarded.
    ///
    /// Heights in the <b>conditions</b> are what our height field measures; heights in the <b>text</b> are what the
    /// signposts say. On this mountain those differ by up to 260 m in the valley, and pretending otherwise would
    /// either put a step on a slope where it cannot be met or print a number no map agrees with.</summary>
    public static partial class Programme
    {
        static readonly ProgrammeDay[] days =
        {
            new ProgrammeDay(1, "приезд и снаряжение", "—",         "2100–2300"),
            new ProgrammeDay(2, "выход на 3000",       "3000–3200", "2100–2300"),
            new ProgrammeDay(3, "водопад и обсерватория", "3200–3400", "2100–2300"),
            new ProgrammeDay(4, "переезд наверх",      "3800–3900", "3800–3900"),
            new ProgrammeDay(5, "Приют 11",            "4100–4200", "3800–3900"),
            new ProgrammeDay(6, "скалы Пастухова",     "4600–4800", "3800–3900"),
            new ProgrammeDay(7, "отдых и прогноз",     "—",         "3800–3900"),
            new ProgrammeDay(8, "штурм",               "5642",      "3800–3900"),
            new ProgrammeDay(9, "резерв", "—", "3800–3900",
                "Девятый день не расписан. Он стоит в программе затем, что окно на 5400 бывает не в тот день, "
                + "который вам удобен: если восьмого дня прогноз плохой — вы отдыхаете и идёте девятого. "
                + "Программа без запасного дня — это программа, в которой кто-то выйдет в непогоду."),
        };

        // ── conditions ────────────────────────────────────────────────────────────────────────────────────
        // Every one of them is a pure function of (progress, standing). They are written out here rather than
        // hidden behind an enum so the table can be read as a table: the line and the test stand side by side.

        static bool Band(float v, float lo, float hi) => v >= lo && v <= hi;

        /// <summary>A night in this band of heights, after a day that got at least this high. The second half is
        /// «climb high, sleep low» itself — the same comparison <see cref="Ascent.TouchGain"/> makes.</summary>
        static Func<Progress, Standing, bool> Night(float lo, float hi, float fromAtLeast = 0f, float fromBelow = 0f)
            => (p, s) => p.Night.Any && Band(p.Night.Ele, lo, hi)
                         && p.Night.FromEle >= fromAtLeast
                         && (fromBelow <= 0f || p.Night.FromEle < fromBelow);

        /// <summary>Standing within reach of the step's goal.</summary>
        static Func<Progress, Standing, bool> AtGoal(Goal goal) => (p, s) => Near(goal, s.X, s.Z);

        /// <summary>Touched this height on the sortie in progress, whether or not you are still up there.</summary>
        static Func<Progress, Standing, bool> Touched(float ele) => (p, s) => s.Highest >= ele || s.Ele >= ele;

        /// <summary>Everything in this set is in the rucksack or in the hands.</summary>
        static Func<Progress, Standing, bool> Carrying(Gear set) => (p, s) => (s.Kit & set) == set;

        static Goal Poi(string id, float reachM, string label) => new Goal(GoalKind.Poi, id, 0f, reachM, label);
        static Goal Bunk(float ele, string label) => new Goal(GoalKind.Bunk, null, ele, 0f, label);
        static Goal Valley(string label) => new Goal(GoalKind.Valley, null, 0f, 0f, label);

        static ProgrammeStep[] Build()
        {
            var terskol = Poi("terskol", VillageReachM, "Терскол, 2100");
            var azau = Poi("azau", StationReachM, "Поляна Азау · прокат и ЭВПСО");
            var krugozor = Poi("krugozor", StationReachM, "Старый Кругозор, 3000");
            var falls = Poi("devichiKosy", PlaceReachM, "Водопад «Девичьи Косы», 2800");
            var obs = Poi("terskolObs", PlaceReachM, "Обсерватория «Пик Терскол», 3127");
            var garabashi = Poi("garabashi", StationReachM, "Гара-Баши, 3847");
            var priut = Poi("priut11", PlaceReachM, "Приют 11, 4050");
            var rocks = Poi("pastukhov", PlaceReachM, "Скалы Пастухова, 4650");
            var summit = Poi("westSummit", SummitReachM, "Западная вершина, 5642");
            var shelf = Bunk(ShelfLowEle, "Приют выше 3700");
            var down = Valley("Ночёвка в долине");

            // A rest day is a day you did not climb — but only after the work is done, or the move up to the
            // barrels on day four would tick it: that evening the day's high point is the station, 3 847 m, which
            // is also below the ceiling. So this one condition reads the programme itself. It still locks nothing:
            // touch Pastukhov rocks once, then take a night on the shelf without climbing, and it ticks.
            var quiet = Night(ShelfLowEle, ShelfHighEle, 0f, RestCeilingEle);
            Func<Progress, Standing, bool> restNight = (p, s) => p.IsDone(Rocks) && quiet(p, s);

            // Setting off for the summit is setting off after the acclimatisation, in the dark, from a bunk on the
            // shelf. Every session of this game starts at 05:00 and dawn is at 05:30 (AscentRoute), so «затемно» on
            // its own would be true of any morning at all — including the crampon drill of day four. The two extra
            // clauses are what make it the штурм and not a walk: the night was taken up on the shelf, and Pastukhov
            // rocks are behind you. Neither of them blocks anything; they only decide when the line is ticked.
            Func<Progress, Standing, bool> summitStart = (p, s) =>
                p.IsDone(Rocks) && p.Night.Any && p.Night.Ele >= ShelfLowEle
                && s.Hour < AscentRoute.DawnHour && s.Ele >= p.Night.Ele + StartGainM;

            var rows = new[]
            {
                // ── день 1 ────────────────────────────────────────────────────────────────────────────────
                Row(1, "arrive", "Терскол",
                    "Дойти до посёлка Терскол, 2100 м",
                    "Программа начинается внизу, а не на горе. Три километра дороги вдоль Баксана от поляны Азау: "
                    + "здесь вы проведёте три первые ночи, здесь прокат, магазин, связь и стол ЭВПСО. Посёлок "
                    + "стоит на 2100–2300 — на высоте, на которой люди живут постоянно, и именно с неё организм "
                    + "начинает перестраиваться. От мечети наверх уходит тропа, по которой пойдёте послезавтра.",
                    "быть ближе " + (int)VillageReachM + " м к Терсколу",
                    terskol, AtGoal(terskol)),

                Row(1, "hire", "Прокат",
                    "Взять в прокате кошки, ледоруб, систему, каску и очки",
                    "Железо берут в первый день, а не в ночь перед штурмом: кошки надо примерить к ботинку, систему — "
                    + "подогнать по себе, а очки — понять, что в них видно. С 4650 м, от скал Пастухова, без полного "
                    + "комплекта наверх не идут — и это не формальность, там начинается лёд. Комплект целиком "
                    + "(пуховка, маска, термос, фонарь, спасодеяло) доберёте к седьмому дню; сегодня — железо.",
                    "кошки, ледоруб, система, каска и очки в рюкзаке",
                    azau, Carrying(Iron)),

                Row(1, "night1", "Первая ночь",
                    "Переночевать внизу, 2100–2300 м",
                    "Первая ночь — на высоте, на которой живут. Подняться спать выше в день приезда — это головная "
                    + "боль на вторые сутки и отёк на четвёртые. Ниже 2500 сон восстанавливает полностью, и это "
                    + "единственная ночь программы, после которой вы проснётесь по-настоящему свежим.",
                    "ночь на " + (int)ValleyLowEle + "–" + (int)ValleyHighEle + " м",
                    down, Night(ValleyLowEle, ValleyHighEle)),

                // ── день 2 ────────────────────────────────────────────────────────────────────────────────
                Row(2, "walk3000", "Кругозор",
                    "Подняться на 3000 м — Старый Кругозор — и не оставаться там",
                    "Первый акклиматизационный выход: набрать восемьсот метров и отдать их обратно. Обычно на "
                    + "этот день берут Чегет; он лежит за краем нашей карты, и равноценная замена — канатная дорога "
                    + "или подъём по ущелью до Старого Кругозора, 3000 м. Смысл не в высоте, а в том, что тело "
                    + "сегодня побывало наверху, а ночует внизу. Это и есть climb high, sleep low: наверху кровь "
                    + "получает сигнал делать больше эритроцитов, внизу вы спите и восстанавливаетесь. Обратный "
                    + "порядок — жить высоко и гулять низко — ломает людей на третьи сутки.",
                    "подняться выше " + (int)WalkHighEle + " м",
                    krugozor, Touched(WalkHighEle)),

                Row(2, "night2", "Вниз ночевать",
                    "Вернуться в Терскол и переночевать",
                    "Высоту нельзя унести с собой: её оставляют наверху, а адаптацию получают ночью внизу. "
                    + "Разница между дневным максимумом и ночёвкой должна быть хотя бы триста метров — сегодня она "
                    + "около восьмисот, и это работает в полную силу.",
                    "ночь на " + (int)ValleyLowEle + "–" + (int)ValleyHighEle + " м после выхода выше "
                    + (int)WalkHighEle,
                    down, Night(ValleyLowEle, ValleyHighEle, WalkHighEle)),

                // ── день 3 ────────────────────────────────────────────────────────────────────────────────
                Row(3, "falls", "Девичьи Косы",
                    "Дойти до водопада «Девичьи Косы», 2800 м",
                    "Пять километров тропы от мечети и восемьсот метров набора — второй выход, и первый, который вы "
                    + "идёте ногами, а не едете. Двадцать пять метров Чыранбаши-Су по скальной плите сотней "
                    + "отдельных струй, веером в пятнадцать метров внизу, грот за полотном. Тропа размечена "
                    + "туриками, краской по валунам и указателями: читать её учатся сегодня, потому что выше "
                    + "скал Пастухова никакой разметки, кроме вешек, уже не будет.",
                    "быть ближе " + (int)PlaceReachM + " м к водопаду",
                    falls, AtGoal(falls)),

                Row(3, "obs", "Обсерватория",
                    "Подняться от водопада к обсерватории «Пик Терскол», 3127 м",
                    "От водопада до купола — четыреста метров тропы, и это верхняя точка выхода: 3100–3150, ровно "
                    + "столько, сколько дал бы Чегет. Самая высокогорная астростанция России и Европы: "
                    + "двухметровый «Цейсс» под полноповоротным куполом двадцати метров в поперечнике и весом "
                    + "в двести пятьдесят тонн. Работают здесь вахтой по месяцу — дольше на этой высоте не держат "
                    + "никого, и это стоит запомнить прежде, чем захочется пожить в бочках подольше.",
                    "быть ближе " + (int)PlaceReachM + " м к обсерватории",
                    obs, AtGoal(obs)),

                Row(3, "night3", "Третья ночь",
                    "Спуститься в Терскол и переночевать",
                    "Третья и последняя ночь внизу. Завтра вы поднимаетесь на гору и остаётесь там до конца "
                    + "программы, поэтому сегодня — просушить всё, что промокло, и разобрать рюкзак: наверху ни "
                    + "сушилки, ни магазина, ни лишнего места в бочке не будет.",
                    "ночь на " + (int)ValleyLowEle + "–" + (int)ValleyHighEle + " м после выхода выше 3000",
                    down, Night(ValleyLowEle, ValleyHighEle, 3000f)),

                // ── день 4 ────────────────────────────────────────────────────────────────────────────────
                Row(4, "garabashi", "Гара-Баши",
                    "Подняться канатной дорогой на Гара-Баши, 3847 м",
                    "Три очереди: Азау — Старый Кругозор — Мир — Гара-Баши, верхняя станция канатной дороги в "
                    + "Европе. Полтора километра набора за двадцать минут — это переезд, а не восхождение, и "
                    + "акклиматизации он не даёт совсем. Всё, что у вас есть, заработано первыми тремя днями; "
                    + "канатка только переносит вас туда, где вы будете спать. Первые полчаса наверху будет "
                    + "знобить и захочется сесть — это нормально, садитесь.",
                    "быть ближе " + (int)StationReachM + " м к станции Гара-Баши",
                    garabashi, AtGoal(garabashi)),

                Row(4, "crampons", "Кошки",
                    "Надеть кошки и пройти в них по фирну выше приютов",
                    "Снежно-ледовые занятия: сегодня, на ровном и при свете, а не в четыре утра на «зеркале». "
                    + "Кошки надевают стоя и никогда на ходу, это двенадцать секунд. Идти в них надо всей "
                    + "подошвой сразу и шире обычного, чтобы зубья не цепляли штанину: первая же зацепленная кошка "
                    + "на косой полке — это срыв, а под полкой ледовые сбросы.",
                    "кошки надеты и вы выше " + (int)DrillEle + " м",
                    garabashi, (p, s) => s.CramponsOn && s.Ele >= DrillEle),

                Row(4, "settle", "Койка",
                    "Занять койку в бочках или в приюте выше 3700 м и переночевать",
                    "Бочки — две тысячи с человека, приют «Нацпарк» — полторы, вагончик спасателей — бесплатно, "
                    + "LeapRus — шесть. Смотреть надо не на цену, а на то, что внутри: дизель и сушилка важнее "
                    + "вида из окна, потому что четыре ночи подряд вы будете сушить в этом доме ботинки. "
                    + "Ночёвка на 3800–3900 — самая высокая в программе; выше спать не нужно, там уже не "
                    + "восстанавливаются.",
                    "ночь на " + (int)ShelfLowEle + "–" + (int)ShelfHighEle + " м",
                    shelf, Night(ShelfLowEle, ShelfHighEle)),

                // ── день 5 ────────────────────────────────────────────────────────────────────────────────
                Row(5, "priut", "Приют 11",
                    "Подняться радиально к Приюту 11 и выше, до 4100–4200 м",
                    "Первый выход с новой высоты ночёвки: километр по ратрачной колее до морены, где стоял "
                    + "«Приют одиннадцати» — самая высокая гостиница Советского Союза, сгоревшая в 1998-м. "
                    + "Сегодня наверху надо просто побыть: посидеть двадцать минут на 4100 и пойти вниз. Выше "
                    + "4200 не надо — завтра будет своё, и потратить силы сегодня значит не дойти завтра.",
                    "подняться выше " + (int)PriutEle + " м",
                    priut, Touched(PriutEle)),

                Row(5, "night5", "Вниз к бочкам",
                    "Спуститься к ночёвке и переночевать на 3800 м",
                    "Триста метров вниз на ночь — та же схема, что в долине, только числа другие. Ночевать на "
                    + "4100 после выхода на 4100 не даёт ничего: правило требует, чтобы ночь была ниже дневного "
                    + "максимума хотя бы на триста метров, иначе это не акклиматизация, а просто ночь на высоте.",
                    "ночь на " + (int)ShelfLowEle + "–" + (int)ShelfHighEle + " м после выхода выше "
                    + (int)PriutEle,
                    shelf, Night(ShelfLowEle, ShelfHighEle, PriutEle)),

                // ── день 6 ────────────────────────────────────────────────────────────────────────────────
                Row(6, "rocks", "Скалы Пастухова",
                    "Выйти к скалам Пастухова, 4650 м, при силах — до 4800",
                    "Главный выход программы и единственный, после которого штурм становится реальным: касание "
                    + "4600–4800 стоит вдвое дороже, чем касание 4100. Здесь же гейт: с 4650 без полного "
                    + "комплекта снаряжения дальше не идут, и сегодня вы узнаёте об этом днём, на своих ногах, а "
                    + "не ночью в темноте. Разворачивайтесь по часам и по ветру, а не по самочувствию: "
                    + "самочувствие на этой высоте врёт последним.",
                    "подняться выше " + (int)RocksEle + " м",
                    rocks, Touched(RocksEle)),

                Row(6, "night6", "Последний спуск на ночь",
                    "Вернуться в приют и переночевать на 3800 м",
                    "Восемьсот метров вниз. Это последняя ночь перед обычным днём; следующая кончится выходом в "
                    + "два часа. Сегодня же проверьте, что кошки садятся на ботинок, что фонарь горит и что "
                    + "запасные батарейки есть — ночью на морозе ничего из этого не чинится.",
                    "ночь на " + (int)ShelfLowEle + "–" + (int)ShelfHighEle + " м после выхода выше "
                    + (int)RocksEle,
                    shelf, Night(ShelfLowEle, ShelfHighEle, RocksEle)),

                // ── день 7 ────────────────────────────────────────────────────────────────────────────────
                Row(7, "forecast", "Прогноз",
                    "Прочитать прогноз на доске у спасателей",
                    "Окно — это не «хорошая погода», а ветер на 5400 слабее восемнадцати метров в секунду и "
                    + "видимость, которая держится до тринадцати часов. Доска показывает сегодня и двое суток "
                    + "вперёд; чем дальше срок, тем чаще она ошибается, поэтому окончательное решение принимают "
                    + "утром, а не накануне. Если окна нет — девятый день затем в программе и стоит.",
                    "прогноз прочитан",
                    azau, (p, s) => s.ForecastRead),

                Row(7, "register", "ЭВПСО",
                    "Записаться у спасателей ЭВПСО: состав, маршрут, контрольное время",
                    "Регистрация — не бумажка, а единственный способ, которым кто-то вообще узнает, что вы не "
                    + "вернулись. Записанной группе поиск начинают через час после контрольного времени, даже "
                    + "если рации нет и SOS никто не слал; незаписанной не начинают вовсе, потому что никто не "
                    + "знает, что вы на горе. Контрольное время ставьте честное: разворот в 13:00, возвращение "
                    + "к 16:00. Записаться можно в любой день — стол внизу, на Азау.",
                    "регистрация в ЭВПСО оформлена",
                    azau, (p, s) => s.Registered),

                Row(7, "kit", "Комплект",
                    "Собрать полный комплект: пуховка, маска, балаклава, рукавицы, запасные перчатки, термос, "
                    + "фонарь, спасодеяло",
                    "То, что на гейте с 4650 считают целиком. Разница между списком и комплектом — запасные "
                    + "перчатки и спасодеяло: первые нужны, когда основные улетели с гребня, второе — когда идти "
                    + "вы уже не можете, а вертолёт выше 5450 не сядет и придётся ждать людей снизу. Собирают "
                    + "всё накануне вечером: в два часа ночи вы не соберёте ничего.",
                    "в рюкзаке всё, что требует гейт на 4650",
                    azau, Carrying(Ascent.Required)),

                Row(7, "rest", "Отбой",
                    "Провести день без набора высоты и лечь рано",
                    "День отдыха — это день, в который вы никуда не идёте. Набрать сегодня уже нечего: всё, что "
                    + "тело успело, оно успело за шесть дней, а лишний выход накануне штурма заберёт те силы, "
                    + "которых завтра не хватит на последние триста метров до вершины. Выход в два-четыре часа "
                    + "ночи означает отбой в семь вечера.",
                    "ночь на " + (int)ShelfLowEle + "–" + (int)ShelfHighEle + " м после дня, в который вы не "
                    + "поднимались выше " + (int)RestCeilingEle + " м (и скалы Пастухова уже пройдены)",
                    shelf, restNight),

                // ── день 8 ────────────────────────────────────────────────────────────────────────────────
                Row(8, "start", "Выход",
                    "Выйти из приюта затемно, до рассвета",
                    "Настоящий штурм начинают в два-четыре часа ночи, и на то три причины, из которых ни одна не "
                    + "про рассвет над Кавказом. Ночью фирн держит: после десяти утра ниже 4600 он "
                    + "раскисает, и вы будете проваливаться по колено и тратить вдвое больше сил. Ночью тихо: "
                    + "дневной ветер и грозовая облачность приходят к полудню. И главное — только ранний выход "
                    + "даёт успеть наверх и обратно до контрольного времени в 13:00, потому что вверх идут "
                    + "десять-двенадцать часов, а вниз человек идёт уже уставшим.",
                    "быть в темноте (до " + AscentRoute.Clock(AscentRoute.DawnHour) + ") на "
                    + (int)StartGainM + " м выше ночёвки в приюте, когда скалы Пастухова уже пройдены",
                    rocks, summitStart),

                Row(8, "summit", "Вершина",
                    "Взойти на Западную вершину, 5642 м",
                    "От Гара-Баши это 1800 метров набора и 6,7 километра по маршруту: ратрачная колея, «зеркало» "
                    + "над скалами, косая полка, седловина, вершинный взлёт, плато. Если в 13:00 вы не на вершине — "
                    + "вы разворачиваетесь там, где стоите. Контрольное время придумали те, кто считал, сколько "
                    + "людей осталось на спуске, а не на подъёме.",
                    "быть ближе " + (int)SummitReachM + " м к Западной вершине",
                    summit, AtGoal(summit)),

                Row(8, "down", "Спуск",
                    "Спуститься с вершины к приютам, ниже 4200 м",
                    "Вершина — половина пути, и не та половина, на которой гибнут. Вниз идут в кошках, лицом к "
                    + "склону там, где круто, и по своим же следам: человек устал, видит хуже и весит столько же, "
                    + "а склон под ним тот же самый. Программа кончается, когда вы в приюте, а не когда вы "
                    + "сфотографировались наверху.",
                    "побывать выше " + (int)SummitTouchEle + " м и вернуться ниже " + (int)RestCeilingEle + " м",
                    shelf, (p, s) => s.Highest >= SummitTouchEle && s.Ele <= RestCeilingEle),
            };

            for (int i = 0; i < rows.Length; i++)
                rows[i] = new ProgrammeStep(i, rows[i].Day, rows[i].Id, rows[i].Title, rows[i].Line, rows[i].Why,
                    rows[i].Test, rows[i].Goal, rows[i].Rule);
            return rows;
        }

        /// <summary>A row before it knows its index; <see cref="Build"/> numbers them.</summary>
        static ProgrammeStep Row(int day, string id, string title, string line, string why, string test,
            Goal goal, Func<Progress, Standing, bool> rule)
            => new ProgrammeStep(-1, day, id, title, line, why, test, goal, rule);

        static readonly ProgrammeStep[] table = Build();

        /// <summary>The index of the Pastukhov-rocks step, looked up once. The two conditions that read the
        /// programme itself run every tick, and a linear scan by string is not what they should pay for.</summary>
        static int rocksIndex = -1;
        static int Rocks => rocksIndex >= 0 ? rocksIndex : (rocksIndex = IndexOf("rocks"));
    }
}
