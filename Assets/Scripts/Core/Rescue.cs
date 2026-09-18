using System;

namespace Height1079.Core
{
    /// <summary>How the rescue service found out that something had gone wrong. There are exactly two ways and one
    /// non-way, and which of them happens is decided at the counter at the bottom of the mountain, hours before
    /// anybody needs it.</summary>
    public enum Alarm : byte
    {
        /// <summary>Nobody knows. The party never registered, and no call got out. This is not a failure state the
        /// game invents to punish anybody — it is what "не зарегистрировался" means.</summary>
        None = 0,
        /// <summary>Somebody got a call out from below the radio ceiling.</summary>
        Sos,
        /// <summary>The control time passed and the party did not come back, so the service started looking.</summary>
        Overdue,
    }

    /// <summary>Who comes, and how.</summary>
    public enum RescueKind : byte
    {
        /// <summary>Nobody.</summary>
        None = 0,
        /// <summary>The machine. Twenty minutes off the saddle, and only in good weather.</summary>
        Helicopter,
        /// <summary>A party on foot from Pastukhov rocks. Hours to get there, and about a day to get anybody down.</summary>
        Foot,
    }

    /// <summary>The slip filed at the counter of the Эльбрусский высокогорный поисково-спасательный отряд МЧС before
    /// a party goes up: who, how many, where to, and by when they will be back. It is a piece of paper and it is the
    /// whole difference between being looked for and not being looked for.</summary>
    public readonly struct Registration
    {
        /// <summary>False for the party that walked past the counter. <see cref="Rescue.NotFiled"/> is that party.</summary>
        public readonly bool Filed;
        /// <summary>Who signed it: the name the game knows the party by.</summary>
        public readonly string Party;
        /// <summary>How many went up. The number the search party is told to find.</summary>
        public readonly int People;
        /// <summary>Where they said they were going, in Russian.</summary>
        public readonly string Route;
        /// <summary>The turn-round time they undertook to keep (<see cref="AscentRoute.TurnaroundHour"/>).</summary>
        public readonly float ControlHour;
        /// <summary>And the hour they undertook to be back in camp by. Past this, plus the grace, somebody starts
        /// walking up.</summary>
        public readonly float BackHour;

        public Registration(string party, int people, string route, float controlHour, float backHour)
        {
            Filed = true;
            Party = string.IsNullOrEmpty(party) ? SaveKeys.DefaultName : party;
            People = Math.Max(1, people);
            Route = string.IsNullOrEmpty(route) ? Rescue.DefaultRoute : route;
            ControlHour = controlHour;
            BackHour = Math.Max(controlHour, backHour);
        }

        public bool IsEmpty => !Filed;

        /// <summary>The slip as it is written out, for the HUD and for the protocol.</summary>
        public string Line => Filed
            ? $"{Party}, {People} чел. — {Route}. Контрольное время {AscentRoute.Clock(ControlHour)}, возвращение к {AscentRoute.Clock(BackHour)}."
            : "Группа не зарегистрирована.";
    }

    /// <summary>A call for help that did or did not go out. Above <see cref="Rescue.SignalCeiling"/> there is no
    /// network on this mountain, so the message is not sent late — it is not sent at all, and the only thing that
    /// changes that is losing height.</summary>
    public readonly struct Sos
    {
        public readonly bool Sent;
        /// <summary>The hour it went out.</summary>
        public readonly float Hour;
        /// <summary>The height it went out from — always below the ceiling when <see cref="Sent"/>.</summary>
        public readonly float FromEle;
        /// <summary>The height it was attempted from, sent or not. This is what the refusal line quotes.</summary>
        public readonly float TriedFromEle;

        public Sos(bool sent, float hour, float fromEle, float triedFromEle)
        { Sent = sent; Hour = hour; FromEle = fromEle; TriedFromEle = triedFromEle; }

        public bool IsEmpty => !Sent && TriedFromEle <= 0f;

        /// <summary>Why it did not go, in Russian, or "".</summary>
        public string Why => Sent ? ""
            : $"Связи нет: выше {Rescue.SignalCeiling:0} м её не бывает. Сбросить {Rescue.DescendForSignalM(TriedFromEle):0} м — и звонить.";
    }

    /// <summary>What the mountain looks like where the casualty is, as the duty officer is told it. Every field is
    /// something the runtime already measures for <see cref="Ascent.Tick"/>.</summary>
    public readonly struct RescueAsk
    {
        /// <summary>Height of the casualty, metres.</summary>
        public readonly float Ele;
        /// <summary>Wind there, m/s — after <see cref="AscentCold.WindAt"/>.</summary>
        public readonly float WindMs;
        /// <summary>Visibility there, metres (<see cref="AscentRoute.VisibilityM"/>).</summary>
        public readonly float VisibilityM;
        /// <summary>Whether there is light to fly in.</summary>
        public readonly bool Daylight;

        public RescueAsk(float ele, float windMs = 0f, float visibilityM = 5000f, bool daylight = true)
        { Ele = ele; WindMs = Math.Max(0f, windMs); VisibilityM = Math.Max(0f, visibilityM); Daylight = daylight; }
    }

    /// <summary>Registering with the rescuers, and what happens when a party does not come back.
    ///
    /// In life a party going above the huts leaves a slip at the ЭВПСО МЧС counter: the names, the route, and the
    /// <b>control time</b> they undertake to be back by. Miss it and somebody starts looking. Skip the counter and
    /// nobody does — not out of spite, but because there is no one to look for. That asymmetry is the whole of this
    /// file, and it is the reason the counter at the bottom of the mountain is a decision and not a formality.
    ///
    /// The numbers are the ones the southern side runs on:
    /// <list type="bullet">
    /// <item>There is no network above 5 250 m (<see cref="AscentRoute.RadioCeiling"/>, the height the МЧС cable also
    /// stops at). From the saddle and from the summit an SOS does not go out — you have to lose height first, and
    /// that is a real decision made by somebody who is in no state to make it.</item>
    /// <item>A party on foot goes up from Pastukhov rocks (4 700 m) to the summit in three to four hours.</item>
    /// <item>A helicopter lifts off the saddle in about twenty minutes — and only in good weather.</item>
    /// <item>Without aviation, getting an injured climber off this mountain takes about a day.</item>
    /// </list>
    /// Everything that is a game choice rather than one of those four is marked "balance" on the constant.
    ///
    /// Help is never instant here. The earliest anybody is with you is the SOS hour plus a helicopter's scramble —
    /// over an hour — and the usual case is most of a day. The model exists so that the waiting is honest.</summary>
    public static class Rescue
    {
        /// <summary>What the service is actually going to do about it, in hours of the same clock the run keeps
        /// (<see cref="AscentRoute.HourAt"/>). Everything is an absolute hour, not a countdown, so the runtime can
        /// put it away and compare against it every tick without keeping a timer of its own.</summary>
        public readonly struct Mission
        {
            public readonly RescueKind Kind;
            /// <summary>How the service learned, if it learned at all.</summary>
            public readonly Alarm How;
            /// <summary>The hour the service learned, or +∞.</summary>
            public readonly float AlarmHour;
            /// <summary>The hour rescuers are with the casualty, or +∞.</summary>
            public readonly float ReachHour;
            /// <summary>The hour the casualty is off the mountain, or +∞.</summary>
            public readonly float SafeHour;
            /// <summary>Russian, for the HUD.</summary>
            public readonly string Title, Note;

            public Mission(RescueKind kind, Alarm how, float alarmHour, float reachHour, float safeHour,
                string title, string note)
            { Kind = kind; How = how; AlarmHour = alarmHour; ReachHour = reachHour; SafeHour = safeHour; Title = title; Note = note; }

            /// <summary>Is anybody coming at all.</summary>
            public bool Coming => Kind != RescueKind.None;
            /// <summary>Hours between the alarm and help being there.</summary>
            public float WaitHours => Coming ? ReachHour - AlarmHour : float.PositiveInfinity;
            /// <summary>Hours from the alarm to being off the mountain.</summary>
            public float TotalHours => Coming ? SafeHour - AlarmHour : float.PositiveInfinity;
            /// <summary>Hours still to wait at this hour of the day, or +∞.</summary>
            public float HoursLeft(float hour) => Coming ? Math.Max(0f, ReachHour - hour) : float.PositiveInfinity;
            public bool Arrived(float hour) => Coming && hour >= ReachHour;
            public bool Off(float hour) => Coming && hour >= SafeHour;
        }

        // ── the counter ───────────────────────────────────────────────────────────────────────────────────

        /// <summary>The turn-round time a party undertakes: 13:00, the same hour the route rules already keep.</summary>
        public const float ControlHour = AscentRoute.TurnaroundHour;
        /// <summary>And the hour they undertake to be back in camp: 16:00. Three hours from the turn-round is what the
        /// descent takes from anywhere on the route.</summary>
        public const float BackHour = 16f;
        /// <summary>How long the duty officer waits past the control time before treating it as an overdue party
        /// rather than a slow one. Balance: an hour is long enough that a late but walking party is not chased, and
        /// short enough that the clock still matters.</summary>
        public const float GraceHours = 1f;

        public const string DefaultRoute = "Западная вершина по южному склону";

        /// <summary>The party that walked past the counter.</summary>
        public static readonly Registration NotFiled = default;

        /// <summary>Fills a slip in. This is the only way a <see cref="Registration"/> with
        /// <see cref="Registration.Filed"/> comes into being.</summary>
        public static Registration File(string party, int people = 1, string route = DefaultRoute,
            float controlHour = ControlHour, float backHour = BackHour)
            => new Registration(party, people, route, controlHour, backHour);

        /// <summary>Is anybody going to notice. The single question the whole counter exists to answer.</summary>
        public static bool Watched(in Registration r) => r.Filed;

        /// <summary>Past the control time and still not back.</summary>
        public static bool PastControl(in Registration r, float hour) => r.Filed && hour >= r.ControlHour;

        /// <summary>Past the hour they said they would be back in camp.</summary>
        public static bool Overdue(in Registration r, float hour) => r.Filed && hour >= r.BackHour;

        public static float OverdueHours(in Registration r, float hour)
            => r.Filed ? Math.Max(0f, hour - r.BackHour) : 0f;

        /// <summary>The hour the search starts by itself, or +∞ for a party nobody registered.</summary>
        public static float SearchStartsHour(in Registration r)
            => r.Filed ? r.BackHour + GraceHours : float.PositiveInfinity;

        /// <summary>Has anybody started looking. False for ever when the slip was never filed — however late it is,
        /// however bad the weather, and whatever the player does.</summary>
        public static bool SearchStarted(in Registration r, float hour) => hour >= SearchStartsHour(r);

        /// <summary>Hours left before the control time, for the HUD line. 0 once it is past.</summary>
        public static float HoursToControl(in Registration r, float hour)
            => r.Filed ? Math.Max(0f, r.ControlHour - hour) : 0f;

        // ── getting word out ──────────────────────────────────────────────────────────────────────────────

        /// <summary>The height the network stops at: the same 5 250 m the МЧС cable stops at, and for the same
        /// reason — that is where the southern side turns its back on the valley.</summary>
        public const float SignalCeiling = AscentRoute.RadioCeiling;
        /// <summary>How far below the ceiling you have to actually be before a call connects. Balance: a metre, so
        /// that "спуститься до 5250" is a real instruction and not a boundary case.</summary>
        public const float SignalMarginM = 1f;

        public static bool CanCall(float ele) => AscentRoute.HasSignal(ele);

        /// <summary>Metres of height to lose before a call will go out from here, or 0 when it already will.</summary>
        public static float DescendForSignalM(float ele) => Math.Max(0f, ele - (SignalCeiling - SignalMarginM));

        /// <summary>Tries to call out. Above the ceiling nothing is sent — the returned <see cref="Sos"/> carries
        /// <see cref="Sos.Why"/> and the metres to lose, and the party has to walk down before it can ask for help.</summary>
        public static Sos SendSos(float ele, float hour)
            => new Sos(CanCall(ele), hour, ele, ele);

        /// <summary>Nobody called.</summary>
        public static readonly Sos NoSos = default;

        /// <summary>When the service first learns, and how. +∞ and <see cref="Alarm.None"/> when it never does: no
        /// slip and no call means no search, and that is the point of the counter.</summary>
        public static float AlarmHour(in Registration r, in Sos sos, out Alarm how)
        {
            float overdue = SearchStartsHour(r);
            float call = sos.Sent ? sos.Hour : float.PositiveInfinity;
            if (call <= overdue && !float.IsPositiveInfinity(call)) { how = Alarm.Sos; return call; }
            if (!float.IsPositiveInfinity(overdue)) { how = Alarm.Overdue; return overdue; }
            how = Alarm.None;
            return float.PositiveInfinity;
        }

        // ── the helicopter ────────────────────────────────────────────────────────────────────────────────

        /// <summary>The ceiling the machine works to. The saddle at 5 300–5 416 m is where it lifts people from and
        /// it is the highest it does: from the summit plateau you walk down to the saddle first. Balance, set just
        /// above the saddle so that the saddle passes and the plateau does not.</summary>
        public const float HeliCeilingEle = 5450f;

        /// <summary>Wind it will not fly in — the same Beaufort 7 the mountain turns a party round at
        /// (<see cref="AscentCold.WalkHardMs"/>). Balance: one threshold for "the wind has decided", used twice.</summary>
        public const float HeliWindMs = AscentCold.WalkHardMs;

        /// <summary>Visibility it will not fly in. Balance, set so that only a clear sky passes: cloud is already
        /// 300 m (<see cref="AscentRoute.VisibilityM"/>) and grounds it.</summary>
        public const float HeliVisibilityM = 1000f;

        /// <summary>From the alarm to the machine being over the casualty: crew, weather, authorisation, transit.
        /// Balance — this is the honest wait, and it is what stops a helicopter from being a teleport.</summary>
        public const float HeliScrambleHours = 1f;

        /// <summary>And the lift itself: about twenty minutes off the saddle.</summary>
        public const float HeliLiftHours = 1f / 3f;

        /// <summary>«Только в хорошую погоду» spelled out: light, height, wind, visibility, all four.</summary>
        public static bool HelicopterFlies(float ele, float windMs, float visibilityM, bool daylight)
            => daylight && ele <= HeliCeilingEle && windMs < HeliWindMs && visibilityM >= HeliVisibilityM;

        public static bool HelicopterFlies(in RescueAsk ask)
            => HelicopterFlies(ask.Ele, ask.WindMs, ask.VisibilityM, ask.Daylight);

        /// <summary>Why it is not flying, in Russian, or "".</summary>
        public static string HelicopterWhyNot(in RescueAsk ask)
        {
            if (!ask.Daylight) return "Темно — борт не пойдёт.";
            if (ask.Ele > HeliCeilingEle) return $"Выше {HeliCeilingEle:0} м борт не работает: снимают с седловины, надо спуститься.";
            if (ask.WindMs >= HeliWindMs) return $"Ветер {ask.WindMs:0} м/с — борт не пойдёт.";
            if (ask.VisibilityM < HeliVisibilityM) return "Нет видимости — борт не пойдёт.";
            return "";
        }

        // ── the party on foot ─────────────────────────────────────────────────────────────────────────────

        /// <summary>Where rescuers on foot start from: Pastukhov rocks, the far end of the snow-cat road.</summary>
        public const float FootFromEle = 4700f;

        /// <summary>Metres of gain an hour. From the rocks to the summit is 942 m and takes three to four hours, and
        /// this is that, taken at the middle of the range.</summary>
        public const float FootGainPerHour = 270f;

        /// <summary>And going down to meet somebody below the rocks. Balance: unloaded and losing height, about two
        /// and a half times the climbing rate.</summary>
        public const float FootDropPerHour = 700f;

        /// <summary>From the alarm to a team leaving the rocks: raising the duty crew in Terskol, getting them and
        /// their kit up the ropeway and along the snow-cat road. Balance.</summary>
        public const float MusterHours = 1.5f;

        /// <summary>What an evacuation costs when no machine flies: about a day, end to end. The approach is carved
        /// out of this rather than added to it — the day is the day, however far up the party has to walk first.</summary>
        public const float GroundEvacHours = 24f;

        /// <summary>Hours of walking from the rocks to this height.</summary>
        public static float FootHours(float ele)
            => ele >= FootFromEle ? (ele - FootFromEle) / FootGainPerHour : (FootFromEle - ele) / FootDropPerHour;

        /// <summary>Hours from the alarm to a party on foot being with the casualty.</summary>
        public static float FootReachHours(float ele)
            => MusterHours + AscentRoute.RatrakMinutes / 60f + FootHours(ele);

        // ── what actually happens ─────────────────────────────────────────────────────────────────────────

        /// <summary>The whole operation, from the slip at the counter to the casualty being off the mountain. The
        /// conditions in <paramref name="ask"/> are the ones the duty officer is looking at when he decides; a runtime
        /// that wants the plan to change with the weather simply asks again.</summary>
        public static Mission Plan(in Registration reg, in Sos sos, in RescueAsk ask)
        {
            float alarm = AlarmHour(reg, sos, out var how);
            if (how == Alarm.None)
                return new Mission(RescueKind.None, how, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity,
                    "Никто не ищет",
                    "Группа не зарегистрирована в отряде, и SOS не ушёл. Внизу не знают, что кого-то нет.");

            string learned = how == Alarm.Sos
                ? $"Сигнал принят в {AscentRoute.Clock(alarm)}."
                : $"Группа не вернулась к контрольному сроку. Поиск с {AscentRoute.Clock(alarm)}.";

            if (HelicopterFlies(ask))
            {
                float reach = alarm + HeliScrambleHours;
                return new Mission(RescueKind.Helicopter, how, alarm, reach, reach + HeliLiftHours,
                    "Идёт борт",
                    learned + $" Погода лётная: борт будет через {HeliScrambleHours:0.#} ч, снимут минут за {HeliLiftHours * 60f:0}.");
            }

            float onFoot = alarm + FootReachHours(ask.Ele);
            string why = HelicopterWhyNot(ask);
            return new Mission(RescueKind.Foot, how, alarm, onFoot, alarm + GroundEvacHours,
                "Идут пешком",
                learned + " " + (why.Length > 0 ? why + " " : "")
                    + $"Спасотряд от скал Пастухова — {FootHours(ask.Ele):0.#} ч хода. Без авиации эвакуация занимает около суток.");
        }

        /// <summary>One line for the HUD at this hour of the day.</summary>
        public static string Line(in Mission m, float hour)
        {
            if (!m.Coming) return m.Title + ". " + m.Note;
            if (m.Off(hour)) return "Вы внизу.";
            if (m.Arrived(hour)) return m.Title + ": спасатели рядом.";
            float left = m.HoursLeft(hour);
            return left >= 1f
                ? $"{m.Title}: ждать {left:0.#} ч."
                : $"{m.Title}: ждать {left * 60f:0} мин.";
        }

        /// <summary>The line the counter itself shows before anybody goes up, for the registration window.</summary>
        public static string CounterText(in Registration r)
            => r.Filed
                ? r.Line + " Не вернётесь — начнут искать."
                : "Регистрация в Эльбрусском высокогорном ПСО МЧС: состав, маршрут, контрольное время возвращения. "
                  + "Без неё никто не узнает, что вас нет.";
    }
}
