using System;

namespace Height1079.Core
{
    /// <summary>Game balance, not a medical model or a reconstruction of the 1959 deaths. Port of public/survival.js.</summary>
    public static class SurvivalRules
    {
        public const float NightSeconds = 1200f;

        public static string NightTime(float seconds)
        {
            int minutes = (int)Math.Floor(17 * 60 + 40 + seconds / NightSeconds * 790) % 1440;
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        public struct Conditions
        {
            public bool Moving, Storm, Fire, Companion, Sheltered, Goal;
            /// <summary>How the participant is getting through the snow. Defaults to <see cref="Travel.Foot"/>.</summary>
            public Travel Mode;
            /// <summary>How deep they are sinking in right now, metres (<see cref="Skiing.Sink"/>). 0 by default,
            /// and at 0 the snow costs nothing, so conditions built without it behave exactly as before.</summary>
            public float Sink;
        }

        /// <summary>How hard the clock and the cold press in this scenario. The night on the slope uses <see cref="Night"/>;
        /// the Elbrus day is longer and much less cold, because the danger there is height and weather, not a February night.</summary>
        public sealed class Profile
        {
            /// <summary>Multiplier on the heat loss per second.</summary>
            public float Cold = 1f;
            /// <summary>Seconds a participant may stay out before the run ends by itself.</summary>
            public float Seconds = NightSeconds;
            public static readonly Profile Night = new Profile();
            public static readonly Profile Day = new Profile { Cold = .28f, Seconds = 5400f };
        }

        static float Clamp(float v) => Math.Max(0f, Math.Min(100f, v));

        /// <summary>Advances one participant by dt seconds. Returns true when an outcome was just decided.</summary>
        public static bool Tick(Participant p, float dt, Conditions c, Profile profile = null)
        {
            if (p.Outcome != Outcome.None) return false;
            profile = profile ?? Profile.Night;
            p.Elapsed += dt;
            float loss = (c.Storm ? .15f : .07f) * (c.Sheltered ? .45f : 1f) * (c.Companion ? .65f : 1f) * (c.Moving ? .8f : 1f) * profile.Cold
                * TravelHeatFactor(c.Mode, c.Sink);
            p.Heat = Clamp(p.Heat + dt * (c.Fire ? .9f : -loss));
            p.Hands = Clamp(p.Hands + dt * (c.Fire ? 1.4f : p.Heat < 55f ? -.15f : -.025f));
            p.Clarity = Clamp(p.Clarity + dt * (c.Fire ? .3f : p.Heat < 30f ? -.12f : 0f));
            if (c.Storm && !c.Sheltered && !c.Fire) p.Exposure += dt;
            if (p.Heat <= 0f) p.Outcome = Outcome.Cold;
            else if (c.Goal) p.Outcome = Outcome.Arrival;
            else if (p.Elapsed >= profile.Seconds) p.Outcome = Outcome.Dawn;
            return p.Outcome != Outcome.None;
        }

        /// <summary>Mass of a hiker without the load, kg — the reference the snow rules press on the snow with
        /// (<see cref="Skiing.PressureKPa"/>) and measure a pack against.</summary>
        public const float HikerKg = 70f;

        /// <summary>What wading costs in warmth. Snow pushed up the trouser legs and into the boots melts against the
        /// skin, and the heat goes with it: knee-deep тропёжка on foot costs about a quarter more heat per second.
        /// Skis keep you on top of it, and on a волокуша the back carries nothing and stays dry — the reason Russian
        /// ski parties pull sledges at all ("мокрая одежда высасывает из вас тепло"). Returns 1 when nothing sinks,
        /// so conditions that do not set <see cref="Conditions.Sink"/> are untouched.</summary>
        public static float TravelHeatFactor(Travel mode, float sink)
        {
            float wet = Math.Max(0f, Math.Min(1f, sink / .5f));
            float share = mode == Travel.Skis ? .12f : mode == Travel.Hauling ? .06f : mode == Travel.Poles ? .22f : .26f;
            return 1f + share * wet;
        }

        /// <summary>Kilograms on the back cost speed: up to 12 kg is felt only as work, 40 kg halves the pace in deep snow (×0.55).</summary>
        public static float LoadSpeedFactor(float kg)
        {
            float t = Math.Max(0f, Math.Min(1f, (kg - 12f) / 28f));
            return 1f - .45f * t;
        }

        /// <summary>Running needs a light load.</summary>
        public const float RunLimitKg = 25f;

        /// <summary>Seconds of steady kindling needed with the given hand condition.</summary>
        public static float KindleSeconds(float hands) => 3.2f + (100f - hands) * .09f;

        /// <summary>A player who is out of the night without reaching shelter.</summary>
        public static bool Fell(Outcome o) => o == Outcome.Cold || o == Outcome.Taken || o == Outcome.Fall;

        /// <summary>Blizzards: the first front at 110 s, then 170 s of пурга and 100 s of lull, again and again; toward morning (≈05:05)
        /// the wind drops for good, so the waning moon that rose at 04:21 shows over the south-east before the end.</summary>
        public static bool StormAt(float elapsed) => elapsed > 110f && elapsed < 1040f && (elapsed - 110f) % 270f < 170f;

        public static OutcomeText Describe(Outcome o) => o switch
        {
            Outcome.Cold => new OutcomeText("Стихия · холод", "Тепло иссякло до выхода к укрытию. Долгое пребывание на ветру увеличивало потери; движение и близость напарника замедляли их. Это игровой исход, а не версия гибели реальных людей."),
            Outcome.Arrival => new OutcomeText("До рассвета", "Вы дошли до верхнего укрытия. Ночь комнаты заканчивается, когда определится исход каждого участника; общий итог записывается в протокол."),
            Outcome.Dawn => new OutcomeText("Незавершённый маршрут", "Наступил рассвет. Вы пережили ночь, но не достигли верхнего укрытия за отведённое время."),
            Outcome.Together => new OutcomeText("Вместе до укрытия", "Оба путника дошли до верхнего укрытия. Совместный маршрут завершён."),
            Outcome.Separated => new OutcomeText("Разделённая пара", "Один дошёл до укрытия, другой остался на склоне. Протокол показывает, где решения разошлись."),
            Outcome.Lost => new OutcomeText("Никто не дошёл", "Тепло иссякло у всех участников до выхода к укрытию."),
            Outcome.Taken => new OutcomeText("Менк", "Лесной великан, притворявшийся сухим деревом, настиг вас в темноте. Это вымысел игры по мотивам мансийских преданий, а не версия гибели реальных людей."),
            Outcome.Fall => new OutcomeText("Срыв", "Ноги ушли на жёстком фирне, и задержаться не вышло. На косой полке выкат — триста-шестьсот метров до ледовых сбросов; там останавливаются не сами. Это игровой исход, а не описание конкретного случая."),
            _ => new OutcomeText("", "")
        };
    }

    public enum Outcome { None, Cold, Arrival, Dawn, Together, Separated, Lost, Taken, Fall }

    public readonly struct OutcomeText
    {
        public readonly string Title, Note;
        public OutcomeText(string title, string note) { Title = title; Note = note; }
    }

    /// <summary>One participant's state in the night. Positions are in world metres (x east, z south), matching the DEM.</summary>
    public class Participant
    {
        public string Token = "";
        public string Name = "";
        public float Elapsed, Heat = 100f, Hands = 100f, Clarity = 100f, Exposure;
        public Outcome Outcome = Outcome.None;
        public float X, Z;
        /// <summary>How this one is travelling: on foot, with the poles out, on skis, or hauling the pack behind.
        /// Mind the older field <see cref="Travel"/> below, which is metres covered since the last step and shadows the
        /// enum name inside this class — write <c>Height1079.Core.Travel.Foot</c> if you ever need the enum in here.</summary>
        public Travel Mode;
        /// <summary>How deep this one is in the snow right now, metres — the host measures it every tick and the cold
        /// rules read it (<see cref="TravelHeatFactor"/>): wading to the knee wets the clothes and costs heat.</summary>
        public float Sink;
        public bool Online = true;
        public double LastSeen;
        public float Travel;
        public float JoinedAt;
        public double? KindlingStarted;
        public float KindlingX, KindlingZ;
    }
}
