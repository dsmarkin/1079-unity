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
        }

        static float Clamp(float v) => Math.Max(0f, Math.Min(100f, v));

        /// <summary>Advances one participant by dt seconds. Returns true when an outcome was just decided.</summary>
        public static bool Tick(Participant p, float dt, Conditions c)
        {
            if (p.Outcome != Outcome.None) return false;
            p.Elapsed += dt;
            float loss = (c.Storm ? .15f : .07f) * (c.Sheltered ? .45f : 1f) * (c.Companion ? .65f : 1f) * (c.Moving ? .8f : 1f);
            p.Heat = Clamp(p.Heat + dt * (c.Fire ? .9f : -loss));
            p.Hands = Clamp(p.Hands + dt * (c.Fire ? 1.4f : p.Heat < 55f ? -.15f : -.025f));
            p.Clarity = Clamp(p.Clarity + dt * (c.Fire ? .3f : p.Heat < 30f ? -.12f : 0f));
            if (c.Storm && !c.Sheltered && !c.Fire) p.Exposure += dt;
            if (p.Heat <= 0f) p.Outcome = Outcome.Cold;
            else if (c.Goal) p.Outcome = Outcome.Arrival;
            else if (p.Elapsed >= NightSeconds) p.Outcome = Outcome.Dawn;
            return p.Outcome != Outcome.None;
        }

        /// <summary>Seconds of steady kindling needed with the given hand condition.</summary>
        public static float KindleSeconds(float hands) => 3.2f + (100f - hands) * .09f;

        /// <summary>A player who is out of the night without reaching shelter.</summary>
        public static bool Fell(Outcome o) => o == Outcome.Cold || o == Outcome.Taken;

        /// <summary>Blizzards: the first front at 110 s, then 170 s of пурга and 100 s of lull, again and again.</summary>
        public static bool StormAt(float elapsed) => elapsed > 110f && (elapsed - 110f) % 270f < 170f;

        public static OutcomeText Describe(Outcome o) => o switch
        {
            Outcome.Cold => new OutcomeText("Стихия · холод", "Тепло иссякло до выхода к укрытию. Долгое пребывание на ветру увеличивало потери; движение и близость напарника замедляли их. Это игровой исход, а не версия гибели реальных людей."),
            Outcome.Arrival => new OutcomeText("До рассвета", "Вы дошли до верхнего укрытия. Ночь комнаты заканчивается, когда определится исход каждого участника; общий итог записывается в протокол."),
            Outcome.Dawn => new OutcomeText("Незавершённый маршрут", "Наступил рассвет. Вы пережили ночь, но не достигли верхнего укрытия за отведённое время."),
            Outcome.Together => new OutcomeText("Вместе до укрытия", "Оба путника дошли до верхнего укрытия. Совместный маршрут завершён."),
            Outcome.Separated => new OutcomeText("Разделённая пара", "Один дошёл до укрытия, другой остался на склоне. Протокол показывает, где решения разошлись."),
            Outcome.Lost => new OutcomeText("Никто не дошёл", "Тепло иссякло у всех участников до выхода к укрытию."),
            Outcome.Taken => new OutcomeText("Менк", "Лесной великан, притворявшийся сухим деревом, настиг вас в темноте. Это вымысел игры по мотивам мансийских преданий, а не версия гибели реальных людей."),
            _ => new OutcomeText("", "")
        };
    }

    public enum Outcome { None, Cold, Arrival, Dawn, Together, Separated, Lost, Taken }

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
        public bool Online = true;
        public double LastSeen;
        public float Travel;
        public float JoinedAt;
        public double? KindlingStarted;
        public float KindlingX, KindlingZ;
    }
}
