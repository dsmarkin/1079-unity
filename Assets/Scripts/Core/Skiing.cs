using System;
using System.Collections.Generic;

namespace Height1079.Core
{
    /// <summary>How a hiker is getting through the snow. Skis and poles are always carried — lashed to the rucksack
    /// when walking — so changing over costs a stop, not a search for gear.</summary>
    public enum Travel
    {
        /// <summary>On foot, skis and poles on the pack. Fine on a wind crust, тропёжка in powder.</summary>
        Foot,
        /// <summary>On foot with the poles taken off the pack: three points of support instead of two.</summary>
        Poles,
        /// <summary>On skis, rucksack on the back.</summary>
        Skis,
        /// <summary>On skis with the rucksack lashed to a spare pair and towed on a rope — a волокуша.</summary>
        Hauling,
    }

    /// <summary>The mathematics of moving through snow: how far you sink, how fast you go, what it costs and how long
    /// the track you leave behind survives. Game balance built on measured anchors, not a snow-mechanics model.
    ///
    /// Anchors used, with sources:
    /// <list type="bullet">
    /// <item>The group's own pace on 31.01 in the valley forest, 1.2 m of snow, packs on: "Таким образом проходим
    /// 1,52 км в час" = 0.42 m/s. That is the whole chain's average including the relief cycle — "Первый сбрасывает
    /// рюкзак и торит тропу 5 минут, отдыхает минут 10—15" and "Каждый торит тропу по 10 минут" (diary,
    /// ermakvagus.com; see docs/MAP.md). The leader's own moment-to-moment speed is higher and his effort is what
    /// empties him in five minutes, which is where <see cref="Effort"/> is scaled from.</item>
    /// <item>A ski sinking 30 cm is still workable, a волокуша rides a crust or a track well and digs into loose
    /// snow, catches on trees in the forest and pulls you sideways on a long traverse; its real gain is that the back
    /// carries nothing and stays dry, "мокрая одежда высасывает из вас тепло" (risk.ru/blog/207055,
    /// pavelrudenko.ru/article/sani-volokushi-dlya-zimnikh-pokhodakh-na-lyzhakh — 40 kg towed, 25 km a day).</item>
    /// <item>Walking: Tobler's hiking function (1993) — fastest at a 5 % downhill, e^(-3.5|g+0.05|).</item>
    /// <item>The 1–2 February footprints survived twenty-six days as raised columns, because snow pressed by a foot
    /// resists the wind while the loose snow around it is scoured away (search reports, dyatlovpass.com/1959-search).
    /// That is why a track both carries weight and lasts.</item>
    /// <item>Drifting snow deposits at Q ≈ 0.008 kg m⁻¹ s⁻¹ and packs to 400 kg/m³ (Gaume &amp; Puzrin 2021), so a
    /// 0.6 × 0.12 m groove fills in about an hour of steady drift. See <see cref="DriftSeconds"/>.</item>
    /// </list></summary>
    public static class Skiing
    {
        /// <summary>Width of the pressed strip a pair of skis leaves, metres (backlog E1.2 uses 0.6 m).</summary>
        public const float TrackWidth = .6f;
        /// <summary>How far past the pressed strip the snow is still pushed about, metres: the edges of a лыжня are
        /// softer than its middle.</summary>
        public const float Feather = .45f;

        /// <summary>Wind speed at which loose snow starts to travel, m/s. Gaume &amp; Puzrin model the slope's snow
        /// transport at 2–12 m/s; below this the track only ages.</summary>
        public const float DriftOnset = 5f;
        /// <summary>Seconds a fresh track keeps in still air with no snowfall. The night clock runs about forty times
        /// faster than the real one (<see cref="SurvivalRules.NightSeconds"/> = 1200 s for some thirteen hours), so
        /// this is roughly seven physical hours — a track outlives a calm night, and a пурга wipes it in half a minute.</summary>
        public const float CalmLife = 640f;
        /// <summary>Never report a track life shorter than this, so nothing divides by zero in a hurricane.</summary>
        public const float LeastLife = 8f;

        /// <summary>Slowest and fastest anything moves here, m/s: you can always inch forward, and nobody outruns a
        /// good descent on 1950s wooden skis.</summary>
        public const float Crawl = .06f, MaxSpeed = 6f;

        /// <summary>Sinking depths the HUD names, metres.</summary>
        public const float Surface = .05f, Ankle = .20f, Knee = .55f, Waist = 1f;

        const float Rad = (float)(Math.PI / 180);

        /// <summary>Every way of travelling, in the order the HUD lists them.</summary>
        public static readonly Travel[] Modes = { Travel.Foot, Travel.Poles, Travel.Skis, Travel.Hauling };

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        // ---------------------------------------------------------------- what presses on the snow

        /// <summary>Support area under a traveller, m²: one valenok sole (0.30 × 0.11 m) at a time on foot, one
        /// 1.95 × 0.07 m ski at a time on skis. The pole baskets add a very little.</summary>
        static float Area(Travel mode) => mode == Travel.Foot ? .035f : mode == Travel.Poles ? .038f : .137f;

        /// <summary>Weight the poles take off the leg as they are planted.</summary>
        static float Relief(Travel mode) => mode == Travel.Poles ? .07f : 0f;

        /// <summary>Share of the load that still presses on the snow through the traveller. On a волокуша the pack is
        /// off the shoulders and rides on its own runners, so it barely counts toward sinking.</summary>
        public const float HaulShare = .18f;
        static float OnBack(Travel mode) => mode == Travel.Hauling ? HaulShare : 1f;

        /// <summary>Pressure a traveller puts on the snow, kPa. A loaded walker makes about 27 kPa, the same man on
        /// skis about 7.</summary>
        public static float PressureKPa(Travel mode, float loadKg)
        {
            float kg = SurvivalRules.HikerKg + Math.Max(0f, loadKg) * OnBack(mode);
            return kg * (1f - Relief(mode)) * 9.81f / 1000f / Area(mode);
        }

        /// <summary>What the surface bears before it gives way, kPa: fresh powder next to nothing, a wind crust as
        /// much as a loaded walker, a trodden лыжня a little more again.</summary>
        public static float HoldKPa(float crust, float packed)
            => 1.2f + 32f * (float)Math.Pow(Clamp01(crust), 1.6) + 26f * (float)Math.Pow(Clamp01(packed), 1.3);

        /// <summary>How far the traveller goes into the snow, metres. The snow underfoot compacts as it is pressed,
        /// so sinking stops before the ground: bearing grows as (1 + h/scale)^2.2, and a deep unsettled pack takes a
        /// longer scale to firm up. Capped by the depth of the pack.</summary>
        public static float Sink(Travel mode, float depth, float crust, float packed, float loadKg)
        {
            depth = Math.Max(0f, depth);
            if (depth <= 0f) return 0f;
            float p = PressureKPa(mode, loadKg), hold = HoldKPa(crust, packed);
            if (hold >= p) return 0f;
            float scale = .13f + .12f * Math.Min(depth, 2.4f);
            float h = scale * ((float)Math.Pow(p / hold, 1f / 2.2f) - 1f);
            return Math.Min(depth, Math.Max(0f, h));
        }

        // ---------------------------------------------------------------- speed

        /// <summary>Pace on firm level ground with nothing on the back, m/s. Skis win by gliding; the волокуша loses a
        /// little to the friction of the load behind ("скорость иногда ниже, чем с рюкзаком").</summary>
        static float Flat(Travel mode) => mode == Travel.Foot ? 1.45f : mode == Travel.Poles ? 1.49f
            : mode == Travel.Skis ? 1.95f : 1.58f;

        /// <summary>Sinking this deep halves the pace. Skis take it far better than boots: a ski ploughs a furrow
        /// where a boot has to be lifted out of a hole every step.</summary>
        static float Wallow(Travel mode, float sink)
        {
            float scale = mode == Travel.Skis ? .23f : mode == Travel.Hauling ? .25f : .30f;
            float power = mode == Travel.Skis || mode == Travel.Hauling ? 1.5f : 1.35f;
            return 1f / (1f + (float)Math.Pow(Math.Max(0f, sink) / scale, power));
        }

        /// <summary>What a finished лыжня is worth: skis glide on it, boots stop sliding about on it.</summary>
        static float Glide(Travel mode) => mode == Travel.Skis || mode == Travel.Hauling ? .32f : .08f;

        /// <summary>How much of the pace depends on the hands. Stiff hands cannot work the poles or hold the tow rope;
        /// on foot they hardly matter, which is why frozen hands take the poles' advantage away entirely.</summary>
        static float Grip(Travel mode) => mode == Travel.Foot ? .02f : mode == Travel.Poles ? .12f
            : mode == Travel.Skis ? .10f : .14f;

        /// <summary>Tobler's hiking function (1993), normalised to 1 on the flat: a walker is quickest on a slight
        /// downhill and loses badly on anything steep, up or down.</summary>
        public static float Tobler(float grade) => (float)Math.Exp(-3.5 * (Math.Abs(grade + .05) - .05));

        /// <summary>Weight on the shoulders for the load rule: on a волокуша most of the pack is on the sled, so
        /// <see cref="SurvivalRules.LoadSpeedFactor"/> hardly bites.</summary>
        public static float ShoulderKg(Travel mode, float loadKg)
            => mode == Travel.Hauling ? 12f + Math.Max(0f, loadKg - 12f) * .25f : Math.Max(0f, loadKg);

        /// <summary>Extra work the towed load makes: nothing on a crust or a track, a furrow to plough in loose
        /// snow — "в рыхлом снегу будет зарываться в снег и мешать продвижению".</summary>
        public static float Drag(float loadKg, float depth, float crust, float packed)
            => 1f + .9f * (Math.Max(0f, loadKg) / 25f) * Math.Min(1.4f, Math.Max(0f, depth))
                  * (1f - Clamp01(crust)) * (1f - Clamp01(packed));

        /// <summary>Effect of the slope. <paramref name="slopeDeg"/> is signed: positive climbing, negative
        /// descending. Boots follow Tobler; skis run away downhill, the more so the firmer the surface, and lose the
        /// climb — a wooden ski with no kicker wax and no skins bites in soft snow and slides straight back down a
        /// hard crust, which is why a steep wind-blown climb is walked and not skied (backlog E1.6, "обход по
        /// твёрдому"). A волокуша climbs worse still, its rope pulling back, and must be held on a descent.</summary>
        static float Grade(Travel mode, float slopeDeg, float sink, float depth, float crust, float packed)
        {
            float g = (float)Math.Tan(slopeDeg * Rad);
            if (mode == Travel.Foot || mode == Travel.Poles) return Tobler(g);
            bool haul = mode == Travel.Hauling;
            if (g > 0f)
            {
                float slip = 1f + 1.8f * Clamp01(crust) * (1f - Clamp01(packed));
                return 1f / (1f + (haul ? 8.5f : 6.5f) * slip * (float)Math.Pow(g, 1.25));
            }
            float firm = 1f - .6f * (depth <= 0f ? 0f : Math.Min(1f, sink / .25f));
            float run = 1f + (haul ? 1.6f : 2.6f) * firm * (float)Math.Pow(-g, .85);
            return Math.Min(haul ? 1.8f : 3f, run);
        }

        /// <summary>Speed over the ground, m/s. <paramref name="hands"/> is the hand condition 0..100 of
        /// <see cref="Participant.Hands"/>.</summary>
        public static float Speed(Travel mode, float depth, float crust, float packed, float slopeDeg, float loadKg, float hands = 100f)
        {
            packed = Clamp01(packed);
            float sink = Sink(mode, depth, crust, packed, loadKg);
            float v = Flat(mode);
            v *= Wallow(mode, sink);
            v *= 1f + Glide(mode) * packed;
            v *= SurvivalRules.LoadSpeedFactor(ShoulderKg(mode, loadKg));
            v *= Grade(mode, slopeDeg, sink, depth, crust, packed);
            v *= 1f - Grip(mode) * (1f - Clamp01(hands / 100f));
            if (mode == Travel.Hauling) v /= 1f + .6f * (Drag(loadKg, depth, crust, packed) - 1f);
            return Math.Max(Crawl, Math.Min(MaxSpeed, v));
        }

        // ---------------------------------------------------------------- effort

        /// <summary>Share of the strength bar spent per second of easy going in this mode.</summary>
        static float Pace(Travel mode) => mode == Travel.Foot ? .0005f : mode == Travel.Poles ? .00046f
            : mode == Travel.Skis ? .0004f : .00032f;

        /// <summary>How much sinking costs in this mode: тропёжка on foot is the most expensive verb in the game,
        /// skis cut it to a third, and a sled that digs in makes it worse again.</summary>
        static float Wade(Travel mode) => mode == Travel.Foot ? 2.2f : mode == Travel.Poles ? 1.9f
            : mode == Travel.Skis ? 2.6f : 3f;

        /// <summary>Strength spent per SECOND of travel, as a share of a full bar (0..1) — the number a tick loop
        /// multiplies by dt. For the cost of a metre, which is what routes are compared on, use
        /// <see cref="EffortPerMetre"/>. Scaled so that breaking trail on foot through the valley's 1.2 m of snow with
        /// a full pack empties a fresh bar in about four minutes — the diary's leader broke trail for five and then
        /// rested ten to fifteen. Walking a finished лыжня costs a seventh of that per second and a fortieth per
        /// metre, so the лыжня is worth far more to the party than to the man who makes it.</summary>
        public static float Effort(Travel mode, float depth, float crust, float packed, float slopeDeg, float loadKg)
        {
            packed = Clamp01(packed);
            float sink = Sink(mode, depth, crust, packed, loadKg);
            float g = (float)Math.Tan(slopeDeg * Rad);
            float kg = SurvivalRules.HikerKg + Math.Max(0f, loadKg) * OnBack(mode);

            float e = Pace(mode) * (kg / SurvivalRules.HikerKg);
            e *= 1f + Wade(mode) * (float)Math.Pow(sink / .25f, 1.3);
            e *= 1f + 2.6f * Math.Max(0f, g) - .9f * Math.Min(.35f, -Math.Min(0f, g));
            e *= 1f - .18f * packed;                                   // a made track saves you choosing your footing
            if (mode == Travel.Hauling)
            {
                e *= Drag(loadKg, depth, crust, packed);
                e *= 1f + 1.4f * Math.Max(0f, g);                      // the rope pulls back on every climb
            }
            return Math.Max(0f, Math.Min(1f, e));
        }

        /// <summary>Strength spent per metre covered — the number to compare routes with, and the one the backlog's
        /// "шаг по следу = 30 % стоимости" is about.</summary>
        public static float EffortPerMetre(Travel mode, float depth, float crust, float packed, float slopeDeg, float loadKg)
            => Effort(mode, depth, crust, packed, slopeDeg, loadKg)
             / Math.Max(Crawl, Speed(mode, depth, crust, packed, slopeDeg, loadKg));

        /// <summary>Chance per metre of losing your footing. Poles are what a winter hiker carries them for; a hard
        /// crust on a steep slope slides, and hands you cannot open will not catch you.</summary>
        public static float Stumble(Travel mode, float depth, float crust, float packed, float slopeDeg, float loadKg, float hands = 100f)
        {
            float sink = Sink(mode, depth, crust, packed, loadKg);
            float g = (float)Math.Abs(Math.Tan(slopeDeg * Rad));
            float trip = mode == Travel.Foot ? .030f : mode == Travel.Poles ? .012f : mode == Travel.Skis ? .018f : .026f;
            float rough = .35f + .65f * Clamp01(sink / .35f);
            float ice = 1f + 1.6f * Clamp01(crust) * Clamp01(crust) * g;
            float stiff = 1f + .8f * (1f - Clamp01(hands / 100f));
            return Clamp01(trip * rough * (1f + 2.2f * g) * ice * stiff * (1f - .35f * Clamp01(packed)));
        }

        /// <summary>Which way of travelling covers the most ground per unit of strength here — the question a hiker
        /// asks before putting the skis on. Speed alone would always answer "skis"; this one answers "волокуша" on a
        /// crust and "pull them off and walk" on a blown-clear stone field.</summary>
        public static Travel Best(float depth, float crust, float packed, float slopeDeg, float loadKg)
        {
            Travel best = Travel.Foot;
            float score = -1f;
            foreach (var mode in Modes)
            {
                float s = Speed(mode, depth, crust, packed, slopeDeg, loadKg)
                        / Math.Max(1e-6f, Effort(mode, depth, crust, packed, slopeDeg, loadKg));
                if (s > score) { score = s; best = mode; }
            }
            return best;
        }

        /// <summary>Seconds a fresh track survives in this weather. Physics behind the shape: drifting snow deposits
        /// at Q ≈ 0.008 kg m⁻¹ s⁻¹ and packs to 400 kg/m³ (Gaume &amp; Puzrin 2021), so filling a 0.6 m wide, 0.12 m
        /// deep groove takes about an hour of steady drift; falling snow buries it faster still. In still air a
        /// pressed track lasts far longer than it does in the wind — the group's own footprints stood for
        /// twenty-six days. <paramref name="wind"/> is m/s, <paramref name="snowfall"/> 0..1.</summary>
        public static float DriftSeconds(float wind, float snowfall)
        {
            float drift = Math.Max(0f, wind - DriftOnset) / 10f;
            float fall = Clamp01(snowfall);
            return Math.Max(LeastLife, CalmLife / (1f + 7.5f * drift * drift + 9f * fall));
        }

        /// <summary>Name of the mode for the HUD.</summary>
        public static string Title(Travel mode) => mode switch
        {
            Travel.Poles => "Пешком с палками",
            Travel.Skis => "На лыжах",
            Travel.Hauling => "На лыжах с волокушей",
            _ => "Пешком",
        };

        /// <summary>How deep it is under you, for the HUD. Backlog E1.3 asks for по щиколотку / по колено / по пояс;
        /// the two ends are there so the HUD can also say "on the surface" and "chest-deep".</summary>
        public static string SinkTitle(float sink) => sink < Surface ? "По поверхности"
            : sink < Ankle ? "По щиколотку"
            : sink < Knee ? "По колено"
            : sink < Waist ? "По пояс" : "По грудь";
    }

    /// <summary>A лыжня: the line of pressed snow a party leaves behind. Whoever follows it sinks less, moves faster
    /// and spends far less, a second pass over the same line presses it harder, and the wind fills it in again.
    ///
    /// Memory is a grid one metre to the cell holding the last print made in it, so an hour of walking is a few
    /// thousand entries rather than one per frame. The cell is wider than the track's reach
    /// (<see cref="Skiing.TrackWidth"/>/2 + <see cref="Skiing.Feather"/> = 0.75 m), so looking at the nine cells
    /// around a point finds every print that can matter. Two lines crossing inside one cell keep only the harder of
    /// the two — a simplification the player will not see.
    ///
    /// Ageing is integrated, not dated: the shared drift counter advances by dt/<see cref="Life"/> every time the
    /// track is touched, so a blizzard in the middle of a calm night eats exactly its own share of the track. The
    /// host keeps <see cref="Life"/> in step with the weather (<see cref="Weather"/>).</summary>
    public sealed class SkiTrack
    {
        /// <summary>Side of one memory cell, metres.</summary>
        public const float Cell = 1f;

        struct Print
        {
            public float X, Z;
            /// <summary>How hard this spot was pressed when it was last stepped on, 0..1.</summary>
            public float Packed;
            /// <summary>Value of the drift counter at that moment.</summary>
            public float Drifted;
        }

        readonly Dictionary<long, Print> prints = new Dictionary<long, Print>();
        double clock;
        bool started;
        float drifted;

        /// <summary>Seconds a fresh track survives in the weather as it stands now. The host sets it from
        /// <see cref="Skiing.DriftSeconds"/>, directly or through <see cref="Weather"/>.</summary>
        public float Life = Skiing.DriftSeconds(0f, 0f);

        /// <summary>Prints still remembered. Faded ones are counted until <see cref="Forget"/> throws them out.</summary>
        public int Count => prints.Count;

        /// <summary>How much of a track's life the weather has eaten since this track began — 1 means a print made at
        /// the start would be gone by now.</summary>
        public float Drifted => drifted;

        public void Weather(float wind, float snowfall) => Life = Skiing.DriftSeconds(wind, snowfall);

        void Advance(double now)
        {
            if (!started) { started = true; clock = now; return; }
            float dt = (float)Math.Max(0, now - clock);
            clock = now;
            drifted += dt / Math.Max(Skiing.LeastLife, Life);
        }

        static long Key(int cx, int cz) => (long)cx << 32 | (uint)cz;
        static int Index(float v) => (int)Math.Floor(v / Cell);

        /// <summary>How hard one pass in this mode presses the snow. Skis press a wide strip flat, a sled smooths it
        /// further; boots punch a narrow, deep trail that helps a walker and does little for a skier.</summary>
        static float Weight(Travel by) => by == Travel.Skis ? .55f : by == Travel.Hauling ? .65f
            : by == Travel.Poles ? .30f : .34f;

        /// <summary>Leave a print at this spot. The runtime calls it as the traveller moves, every half metre or so.</summary>
        public void Stamp(float x, float z, double now, Travel by)
        {
            Advance(now);
            float weight = Weight(by);
            long k = Key(Index(x), Index(z));
            float live = prints.TryGetValue(k, out var old) ? Live(old) : 0f;
            // a second pass over the same line presses it harder, with diminishing returns
            prints[k] = new Print { X = x, Z = z, Packed = live + weight * (1f - live), Drifted = drifted };
        }

        float Live(Print p) => p.Packed * Fade(drifted - p.Drifted);

        static float Fade(float erosion) => erosion >= 1f ? 0f : (float)Math.Pow(1f - Math.Max(0f, erosion), 1.2);

        /// <summary>How packed the snow is at this spot, 0..1 — what <see cref="Skiing"/> takes as
        /// <c>packed</c>. Full value inside the pressed strip, softer at its edges, nothing beyond them.</summary>
        public float Packed(float x, float z, double now)
        {
            Advance(now);
            int cx = Index(x), cz = Index(z);
            float best = 0f;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!prints.TryGetValue(Key(cx + dx, cz + dz), out var p)) continue;
                    float ex = x - p.X, ez = z - p.Z;
                    float across = Across((float)Math.Sqrt(ex * ex + ez * ez));
                    if (across <= 0f) continue;
                    float v = Live(p) * across;
                    if (v > best) best = v;
                }
            return best > 1f ? 1f : best;
        }

        /// <summary>Across the лыжня: the pressed strip holds its value, the edges where snow was only pushed aside
        /// give way.</summary>
        public static float Across(float metresFromCentre)
        {
            float half = Skiing.TrackWidth * .5f;
            if (metresFromCentre <= half) return 1f;
            if (metresFromCentre >= half + Skiing.Feather) return 0f;
            float t = 1f - (metresFromCentre - half) / Skiing.Feather;
            return t * t * (3f - 2f * t);
        }

        /// <summary>Throw away the prints the wind has filled in.</summary>
        public void Forget(double now)
        {
            Advance(now);
            List<long> gone = null;
            foreach (var kv in prints)
                if (drifted - kv.Value.Drifted >= 1f) (gone ?? (gone = new List<long>())).Add(kv.Key);
            if (gone == null) return;
            foreach (var k in gone) prints.Remove(k);
        }

        /// <summary>Forget everything (a new night, a new run).</summary>
        public void Clear()
        {
            prints.Clear();
            drifted = 0f;
            started = false;
        }
    }
}
