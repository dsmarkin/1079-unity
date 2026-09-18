using System;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The weather of one day on the southern slope, as both sides of the wire see it.
    ///
    /// Until now the mountain rolled a die every five seconds and hoped for the best. Now the host draws <b>one
    /// seed</b> when the session starts (or takes the one the save carries) and publishes it together with the date;
    /// everything else — the wind, the temperature, the sky, the hour the front arrives — comes out of
    /// <see cref="Forecast.Day"/>, which is a pure function of those two integers. So the host and every client agree
    /// about the weather without a byte of weather ever being sent, the board at the rescue base predicts the same day
    /// the mountain is actually going to have, and a save that comes back tomorrow gets the tomorrow it was promised.
    ///
    /// Two integers on the wire, and nothing else (<see cref="NightSession.WeatherSeed"/>,
    /// <see cref="NightSession.WeatherDay"/>). That is the same trade <see cref="Climb"/> already makes for the
    /// ground: send what cannot be derived, derive the rest.
    ///
    /// Silent off the southern slope: on Kholat Syakhl <see cref="Known"/> is false and the 1959 blizzard cycle keeps
    /// the weather it always had.</summary>
    public static class MountainDay
    {
        static int seed, day = int.MinValue;
        static DayWeather cached;
        static bool has;

        /// <summary>Has the host published a day yet.</summary>
        public static bool Known
        {
            get
            {
                var s = NightSession.Instance;
                if (!Climb.On || s == null || s.WeatherSeed.Value == 0) return false;
                Refresh(s.WeatherSeed.Value, s.WeatherDay.Value);
                return true;
            }
        }

        static void Refresh(int newSeed, int newDay)
        {
            if (has && newSeed == seed && newDay == day) return;
            seed = newSeed; day = newDay;
            cached = Forecast.Day(seed, Forecast.DateOf(day));
            has = true;
        }

        /// <summary>The seed of this save. 0 before the host has published one.</summary>
        public static int Seed => Known ? seed : 0;
        /// <summary>The date on the mountain.</summary>
        public static DateTime Date => Forecast.DateOf(Known ? day : 0);
        /// <summary>The weather the mountain actually has today.</summary>
        public static DayWeather Day => Known ? cached : default;

        /// <summary>Free-air wind at the Azau meadow, m/s: the day's own figure at 4 000 m taken down to 2 350 m by
        /// the same curve the board is written with.</summary>
        public static float MeadowWindMs => Known ? cached.BaseWindMs * Forecast.WindGain(Elbrus.Azau.Ele) : 0f;

        /// <summary>The meadow wind on the 0…1 scale the visuals and <see cref="Climb.BaseWindMs"/> use, or −1 when
        /// no day has been drawn and the old storm-driven wind should stand.</summary>
        public static float WindShare => Known
            ? Mathf.Clamp01((MeadowWindMs - Climb.CalmMs) / Climb.GaleMs)
            : -1f;

        /// <summary>The sky at this hour of the run: the morning until the front arrives, two steps worse after.</summary>
        public static SkyState SkyNow => Known ? cached.SkyAt(Climb.Hour()) : SkyState.Clear;

        /// <summary>Whether snow is being driven across the slope right now — the flag the particles and the fresh
        /// snow of the wands run on.</summary>
        public static bool Blowing(float hour) => Known && cached.SkyAt(hour) >= SkyState.Snow;

        /// <summary>The board as it hangs at the rescue base: today and the next two days, by height.
        /// <see cref="WeatherBoard.Missed"/> is deliberately not consulted anywhere here — a forecast that told you
        /// its own reliability would not be a forecast.</summary>
        public static string BoardText(int days = 2)
            => Known ? Forecast.BoardText(seed, Forecast.DateOf(day), days) : "";

        /// <summary>«Окно до 14:20: на седловине 9 м/с» — the one line a party actually comes to read.</summary>
        public static string Verdict => Known ? Forecast.Verdict(cached) : "";

        /// <summary>For the protocol line when a session starts.</summary>
        public static string Title => Known
            ? $"{Forecast.DateOf(day):dd.MM} · {AscentRoute.SkyTitle(cached.Morning).ToLowerInvariant()}, ветер на 4000 м {cached.BaseWindMs:0.#} м/с"
            : "";
    }
}
