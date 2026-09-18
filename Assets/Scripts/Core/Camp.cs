using System;

namespace Height1079.Core
{
    /// <summary>Why a tent cannot go up here, or <see cref="Ok"/>.</summary>
    public enum CampVerdict : byte
    {
        Ok = 0,
        /// <summary>The ground leans more than a двойка will stand on.</summary>
        TooSteep,
        /// <summary>The Garabashi glacier right of the snow-cat lane: the bridges are what you would be sleeping on.</summary>
        Crevasse,
        /// <summary>Above Pastukhov rocks the surface is ice, and ice takes no pegs. A platform has to exist or be cut,
        /// and cutting one needs an ice axe.</summary>
        BareIce,
        /// <summary>The wind takes the fly out of your hands before it is over the poles.</summary>
        Gale,
        /// <summary>No tent in the rucksack.</summary>
        NoTent,
        /// <summary>Somebody has already pitched here.</summary>
        Taken,
        /// <summary>Not this map: the night on Kholat Syakhl is one night and is not camped through.</summary>
        NotHere,
    }

    /// <summary>Everything the placement rule needs about a spot, and nothing else. The runtime measures all of it —
    /// the height and the slope off the DEM, the offset off <see cref="Elbrus.SummitRoute"/>, the distance to the
    /// nearest roof, the wind out of <see cref="AscentCold.WindAt"/> — and hands it in.</summary>
    public readonly struct CampSpot
    {
        /// <summary>Metres above sea level, as the height field measures them.</summary>
        public readonly float Ele;
        /// <summary>Ground slope here, degrees, always positive.</summary>
        public readonly float SlopeDeg;
        /// <summary>Metres from the route line, SIGNED: positive to the right going up — the sign the crevasse rule
        /// is built on (<see cref="AscentRoute.CrevasseChance"/>).</summary>
        public readonly float OffRouteM;
        /// <summary>Metres to the nearest prepared platform: a hut, the barrels, the box on the saddle. Anything
        /// further than <see cref="Camp.PlatformReachM"/> counts as none.</summary>
        public readonly float PlatformM;
        /// <summary>Wind at the spot, m/s, after the funnel of the shelf and the saddle.</summary>
        public readonly float WindMs;
        /// <summary>Month, for the thickness of the snow bridges.</summary>
        public readonly int Month;

        public CampSpot(float ele, float slopeDeg, float offRouteM = 0f, float platformM = float.MaxValue,
            float windMs = 0f, int month = 7)
        {
            Ele = ele; SlopeDeg = Math.Abs(slopeDeg); OffRouteM = offRouteM;
            PlatformM = platformM; WindMs = Math.Max(0f, windMs); Month = month;
        }

        public bool OnPlatform => PlatformM <= Camp.PlatformReachM;
    }

    /// <summary>A mini camp on the southern slope: where a двойка may be pitched, what it costs in time, and what one
    /// night in it does to a climber. This is the half of "playing the ascent in pieces" that is a rule rather than a
    /// file — <see cref="SaveGame"/> is the other half.
    ///
    /// The real ascent from Гара-Баши takes eight to eleven hours, which is longer than most people sit down to play,
    /// and the honest answer the mountain itself gives is the one guided parties use: you do not do it in one push,
    /// you do it in sorties. So a camp is two things at once. It is a place to stop and come back to, and it is the
    /// night that <see cref="Ascent.Acclimatise"/> has always been waiting for: climb high, sleep low. Walking to
    /// 4 800 and sleeping at 3 900 is not a failed attempt at the summit, it is the second day of an ascent, and the
    /// climber who comes back is measurably stronger for it.
    ///
    /// Where the rules come from:
    /// <list type="bullet">
    /// <item>A tent goes on a levelled platform. Guides cut them; on a slope past about fifteen degrees a двойка
    /// slides down the groundsheet all night and nobody sleeps.</item>
    /// <item>The crevasses of the Garabashi glacier are right of the snow-cat lane between 3 700 and 4 050 m
    /// (<see cref="AscentRoute"/>). The one place on this mountain nobody camps.</item>
    /// <item>Above Pastukhov rocks the surface is the «зеркало» — hard firn going to ice. Pegs do not go into it and
    /// a platform has to be cut. That is what an ice axe is for, and it is why the huts of the moraine at
    /// 4 050–4 200 m and the box on the saddle at 5 300 m are where parties actually sleep.</item>
    /// <item>Beaufort 8 (<see cref="AscentCold.WalkVeryHardMs"/>) is where walking is barely possible; a light fly
    /// cannot be got over the poles in it at all.</item>
    /// </list></summary>
    public static class Camp
    {
        // ── where ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Past this the groundsheet slides all night. A game number, chosen so that the benches of the
        /// moraine and the saddle are campable and the open slope mostly is not.</summary>
        public const float MaxSlopeDeg = 15f;
        /// <summary>How near a hut, a barrel or the box on the saddle counts as standing on a made platform.</summary>
        public const float PlatformReachM = 45f;
        /// <summary>Above Beaufort 8 the fly goes before the poles do.</summary>
        public const float MaxPitchWindMs = AscentCold.WalkVeryHardMs;
        /// <summary>Two camps do not share a platform.</summary>
        public const float SpacingM = 8f;
        /// <summary>How near the tent you have to be to crawl into it and sleep.</summary>
        public const float SleepReachM = 4f;

        /// <summary>The belt where the surface stops taking a peg. Same line as the gear gate, and for the same
        /// reason: above Pastukhov rocks this mountain is ice.</summary>
        public const float IceFromEle = Ascent.GearGateEle;

        /// <summary>True where a tent needs a platform that is either already there or has to be cut.</summary>
        public static bool NeedsPlatform(float ele) => ele > IceFromEle;

        /// <summary>Can a tent go up here? Everything is checked in the order a climber would notice it.</summary>
        public static CampVerdict Check(CampSpot spot, bool hasTent = true, bool hasIceAxe = false,
            float nearestCampM = float.MaxValue, Place place = Place.Elbrus)
        {
            if (!AllowedIn(place)) return CampVerdict.NotHere;
            if (!hasTent) return CampVerdict.NoTent;
            if (nearestCampM < SpacingM) return CampVerdict.Taken;
            if (spot.SlopeDeg > MaxSlopeDeg) return CampVerdict.TooSteep;
            // a made platform is proof there are no bridges under it: the barrels and the huts of the moraine stand
            // inside the same band of height the crevasse rule covers, and parties have slept on them for forty years
            if (!spot.OnPlatform && AscentRoute.CrevassePerMetre(spot.Ele, spot.OffRouteM, spot.Month, false) > 0f)
                return CampVerdict.Crevasse;
            if (NeedsPlatform(spot.Ele) && !spot.OnPlatform && !hasIceAxe) return CampVerdict.BareIce;
            if (spot.WindMs >= MaxPitchWindMs) return CampVerdict.Gale;
            return CampVerdict.Ok;
        }

        /// <summary>The camp is a thing of the southern slope. The night of 1–2 February is one night: it is not saved
        /// and not slept through, and every entry point here says so rather than misbehaving quietly.</summary>
        public static bool AllowedIn(Place place) => place == Place.Elbrus;

        public static string Why(CampVerdict v) => v switch
        {
            CampVerdict.TooSteep => $"Слишком круто: нужен уклон не больше {MaxSlopeDeg:0}°.",
            CampVerdict.Crevasse => "Трещины. Правее колеи ратрака на мостах не ночуют.",
            CampVerdict.BareIce => "Выше скал Пастухова здесь голый лёд. Нужна готовая площадка или ледоруб, чтобы вырубить свою.",
            CampVerdict.Gale => "Ветер вырывает тент из рук. Здесь двойку не поставить.",
            CampVerdict.NoTent => "Палатки в рюкзаке нет.",
            CampVerdict.Taken => "Лагерь уже стоит рядом.",
            CampVerdict.NotHere => "Здесь не ночуют.",
            _ => "",
        };

        // ── how long ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Seconds of standing still to get a двойка up on ground that takes a peg. The run compresses nine
        /// hours of the morning into ninety minutes (<see cref="AscentRoute.RunHours"/>), so this is the few minutes
        /// of real work that a tent takes, at the scale the rest of the clock runs on.</summary>
        public const float PitchSeconds = 45f;
        /// <summary>And to take it down and pack it.</summary>
        public const float StrikeSeconds = 25f;
        /// <summary>Cutting a platform out of the «зеркало» with an ice axe, on top of the pitching. Longer than the
        /// trench of <see cref="AscentRoute.TrenchSeconds"/>, and for the same reason: it is real work at height, and
        /// it is meant to be felt — four minutes of standing on the ice is a decision, not a keystroke.</summary>
        public const float PlatformSeconds = 180f;

        public static float SecondsToPitch(CampSpot spot)
            => PitchSeconds + (NeedsPlatform(spot.Ele) && !spot.OnPlatform ? PlatformSeconds : 0f);

        // ── the night ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Hours of the night the body gets credited with.</summary>
        public const float NightHours = 8f;
        /// <summary>Frostbite this far along thaws overnight in a bag; past it the tissue is gone and a night does
        /// nothing for it (<see cref="AscentCold.FrostbiteWarning"/> calls the same line «не чувствуются»).</summary>
        public const float ThawBelow = .35f;
        /// <summary>A night with a burner: snow is melted, the flask and the thermos are filled, the dryness goes.
        /// A night without one only makes it worse — there is no water on this mountain that is not snow.</summary>
        public const float DryNightGain = .18f;

        /// <summary>One night in this camp. Returns the acclimatisation the climber wakes up with.
        ///
        /// This is the whole of «climb high, sleep low» as the game plays it: the day's high point is whatever the
        /// climber reached before stopping, the night is spent at the camp, and <see cref="Ascent.Acclimatise"/>
        /// decides what that was worth. Sleeping three hundred metres or more below the high point is what pays; a
        /// night high with nothing above it pays much less, and a day that gained nothing at all costs
        /// <see cref="Ascent.DecayPerDay"/>.
        ///
        /// Everything else the night does is the honest short list: the heart and the drowsiness go, the eyes mend by
        /// a day's worth, mild frostbite comes back and real frostbite does not, the thermos is refilled if there is
        /// anything to melt snow with, and the altitude the body has not digested is digested at the rate of the
        /// camp's own height — which above 5 000 m is no rate at all, because up there nobody adapts.</summary>
        public static float Sleep(Climber c, float campEle, bool burner, float days = 1f)
        {
            if (c == null) return 0f;
            days = Math.Max(0f, days);
            float highest = Math.Max(c.HighestEle, campEle);

            c.Acclimatisation = Ascent.Acclimatise(c.Acclimatisation, new Ascent.Sortie(highest, campEle, days));

            // water first: a dry night is a night the sickness comes on faster
            c.Dehydration = burner ? 0f : Ascent.Clamp01(c.Dehydration + DryNightGain * days);
            c.SicknessLoad = Math.Max(0f, c.SicknessLoad
                + Ascent.SicknessRate(campEle, c.Acclimatisation, c.Dehydration) * NightHours * days);
            c.EdemaSeconds = 0f;

            c.Drowsiness = 0f;
            c.Pulse = 0f;
            c.StopSeconds = 0f;
            c.Blindness = Ascent.Clamp01(c.Blindness - days * 24f / AscentRoute.BlindRecoveryHours);
            if (c.Hands < ThawBelow) c.Hands = 0f;
            if (c.Feet < ThawBelow) c.Feet = 0f;
            if (c.Face < ThawBelow) c.Face = 0f;
            if (burner) c.ThermosSips = AscentRoute.ThermosSips;

            // the next sortie starts from the tent, not from yesterday's high point
            c.HighestEle = campEle;
            return c.Acclimatisation;
        }

        /// <summary>What one night here would be worth, without changing anything. For the line the HUD shows before
        /// the player commits to it.</summary>
        public static float SleepGain(Climber c, float campEle, float days = 1f)
            => c == null ? 0f
             : Ascent.Acclimatise(c.Acclimatisation, new Ascent.Sortie(Math.Max(c.HighestEle, campEle), campEle, days))
             - c.Acclimatisation;

        // ── naming the place ──────────────────────────────────────────────────────────────────────────────

        /// <summary>A named place this near owns the camp's name.</summary>
        public const float PoiNameM = 140f;
        /// <summary>And the route names it this far off the line; further out it is just the slope.</summary>
        public const float RouteNameM = 300f;

        /// <summary>Where a camp is, in the words the menu uses: «косая полка», «Приют 11», «южный склон».</summary>
        public static string StageAt(float x, float z)
        {
            Elbrus.Poi near = null;
            float best = PoiNameM;
            foreach (var p in Elbrus.Pois)
            {
                float d = Elbrus.Distance(x, z, p.X, p.Z);
                if (d < best) { best = d; near = p; }
            }
            if (near != null) return Plain(near.Label);

            var (s, off) = Elbrus.Nearest(Elbrus.SummitRoute, x, z);
            if (Math.Abs(off) > RouteNameM) return "южный склон";
            string stage = "южный склон";
            foreach (var (label, _, at) in Elbrus.RouteStages)
            {
                if (at > s) break;
                stage = label;
            }
            return Plain(stage);
        }

        /// <summary>«косая полка, 5290 м» — the line the menu shows under «Продолжить».</summary>
        public static string Where(float x, float z, float ele)
            => $"{StageAt(x, z)}, {(int)Math.Round(ele)} м";

        /// <summary>A POI label without its height tail or its aside: «Косая полка · 5290–5380» → «косая полка»,
        /// «Приют 11 (Дизель-хат) · 4050» → «Приют 11».</summary>
        static string Plain(string label)
        {
            if (string.IsNullOrEmpty(label)) return "южный склон";
            int cut = label.Length;
            int dot = label.IndexOf('·');
            if (dot >= 0 && dot < cut) cut = dot;
            int brace = label.IndexOf('(');
            if (brace >= 0 && brace < cut) cut = brace;
            string s = label.Substring(0, cut).Trim();
            if (s.Length == 0) return "южный склон";
            // proper names keep their capital; «Косая полка» is a description and reads better in lower case
            return s.StartsWith("Косая", StringComparison.Ordinal) || s.StartsWith("Скалы", StringComparison.Ordinal)
                || s.StartsWith("Зеркало", StringComparison.Ordinal) || s.StartsWith("Седловина", StringComparison.Ordinal)
                || s.StartsWith("Плато", StringComparison.Ordinal) || s.StartsWith("Вершинный", StringComparison.Ordinal)
                ? char.ToLowerInvariant(s[0]) + s.Substring(1)
                : s;
        }
    }
}
