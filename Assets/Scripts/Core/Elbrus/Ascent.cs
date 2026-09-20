using System;

namespace Height1079.Core
{
    /// <summary>What is underfoot on the way to the West summit, by the height the radar measures — never by the height
    /// on the signpost. The belts are the ones guides brief their clients with on the southern side.</summary>
    public enum Surface : byte
    {
        /// <summary>Below 4 000 m: the snow-cat road, a rolled lane you can walk in trainers.</summary>
        Groomed,
        /// <summary>4 000–4 650 m: rolled firn, still forgiving.</summary>
        Firn,
        /// <summary>4 650–5 100 m: hard firn going to ice — «зеркало», the mirror above Pastukhov rocks.</summary>
        Ice,
        /// <summary>5 100–5 400 m: firn cut by sastrugi. A slide here ends on the ice cliffs.</summary>
        Sastrugi,
        /// <summary>5 400–5 550 m: loose snow lying on ice, the worst of both.</summary>
        LooseOverIce,
        /// <summary>Above 5 550 m: wind crust on the summit plateau.</summary>
        WindCrust,
    }

    /// <summary>The kit a guided party is checked for at the barrels. Everything here is a separate physical object in
    /// the rucksack; this enum is only how the rules ask "is it on him". <see cref="Ascent.Required"/> is the gate.</summary>
    [Flags]
    public enum Gear
    {
        None = 0,
        /// <summary>Кошки. Put on standing, never on the move (<see cref="Ascent.CramponSeconds"/>).</summary>
        Crampons = 1 << 0,
        /// <summary>Ледоруб. Without it there is no self-arrest at all.</summary>
        IceAxe = 1 << 1,
        /// <summary>Страховочная система с тремя муфтованными карабинами и усом. «Верёвка бесполезна, пока на вас не
        /// надета система»: without it the МЧС cable is a handrail and nothing more.</summary>
        Harness = 1 << 2,
        Helmet = 1 << 3,
        /// <summary>Очки S3–S4.</summary>
        Goggles = 1 << 4,
        /// <summary>Маска S2–S3 for the dark hours and the driving snow.</summary>
        Mask = 1 << 5,
        DownJacket = 1 << 6,
        Balaclava = 1 << 7,
        Mittens = 1 << 8,
        SpareGloves = 1 << 9,
        Thermos = 1 << 10,
        Headlamp = 1 << 11,
        SpaceBlanket = 1 << 12,
    }

    /// <summary>The four phases of mountain sickness the game shows, in the order they arrive. Every one of them is
    /// readable before the next, so a player always has an honest chance to turn round.</summary>
    public enum Ams : byte
    {
        None = 0,
        /// <summary>Headache: −10 % of the strength bar.</summary>
        Headache = 1,
        /// <summary>Nausea: standing still no longer gives strength back.</summary>
        Nausea = 2,
        /// <summary>Ataxia: the controls pull sideways. On the косая полка that is what kills.</summary>
        Ataxia = 3,
        /// <summary>Oedema: a timer of 20–40 minutes that only 500 m of descent stops.</summary>
        Edema = 4,
    }

    /// <summary>Which half of the day a step belongs to. The ascent and the descent of Elbrus are not the same walk
    /// run backwards: going down you face out from the slope, the legs are spent, the sickness is at its worst, the
    /// snow has gone soft in the sun, and the whole task changes from "keep climbing" to "find the way home".
    /// Every rule that differs takes this, and nothing else in the model carries it.</summary>
    public enum Going : byte
    {
        /// <summary>Uphill, or across. The default everywhere, so old callers behave exactly as before.</summary>
        Up = 0,
        /// <summary>Downhill: the half the statistics belong to.</summary>
        Down = 1,
    }

    /// <summary>Everything about a place on the route the rules need, and nothing else: the runtime reads it off the
    /// DEM and the route polyline and hands it in. No rule below looks up a POI or a label.</summary>
    public readonly struct RoutePoint
    {
        /// <summary>Metres above sea level as the height field measures them (<see cref="World.Ground"/>).</summary>
        public readonly float Ele;
        /// <summary>Slope along the direction of travel, degrees, positive climbing.</summary>
        public readonly float SlopeDeg;
        /// <summary>Slope across the direction of travel, degrees, always positive — the angle you would slide down
        /// if your feet went. On the косая полка this is 23–34° while <see cref="SlopeDeg"/> is 3–8°.</summary>
        public readonly float CrossDeg;
        /// <summary>Metres from the route polyline, SIGNED: positive to the RIGHT of the line when climbing. The sign
        /// is the whole of the crevasse rule on the Garabashi glacier (<see cref="AscentRoute.CrevasseChance"/>).</summary>
        public readonly float OffRouteM;

        public RoutePoint(float ele, float slopeDeg = 0f, float crossDeg = 0f, float offRouteM = 0f)
        { Ele = ele; SlopeDeg = slopeDeg; CrossDeg = Math.Abs(crossDeg); OffRouteM = offRouteM; }

        /// <summary>The steeper of the two angles: what decides whether a slip turns into a slide.</summary>
        public float Steepness => Math.Max(Math.Abs(SlopeDeg), CrossDeg);
    }

    /// <summary>One climber between runs and inside a run. The runtime owns the object and passes it into
    /// <see cref="Ascent.Tick"/>; every rule that reads it is a pure function of it and of a <see cref="RoutePoint"/>.
    /// <see cref="Acclimatisation"/> is the only field that is meant to outlive a run.</summary>
    public sealed class Climber
    {
        /// <summary>What is on him right now.</summary>
        public Gear Gear = Gear.None;
        /// <summary>Crampons actually on the boots — having them in the pack is not the same thing.</summary>
        public bool CramponsOn;

        /// <summary>0…1, carried between runs. 0.3 is a tourist off the ropeway, 1.0 is a week of climb-high-sleep-low.</summary>
        public float Acclimatisation = .3f;
        /// <summary>Hours of altitude the body has not digested. <see cref="Ascent.Phase"/> turns it into a phase.</summary>
        public float SicknessLoad;
        /// <summary>Highest point reached this run — the acclimatisation book-keeping uses it afterwards.</summary>
        public float HighestEle;
        /// <summary>Seconds the oedema timer has run. At <see cref="Ascent.EdemaTimerSeconds"/> it is over.</summary>
        public float EdemaSeconds;

        /// <summary>Three separate frostbite pools, 0 (warm) … 1 (frostbitten). They never share heat.</summary>
        public float Hands, Feet, Face;
        /// <summary>0…1. At 1 the climber has drunk nothing for a day: sickness comes 40 % faster.</summary>
        public float Dehydration;
        /// <summary>0…1 on the long flat traverse in the dark. A sip from the thermos puts it back to nothing.</summary>
        public float Drowsiness;
        /// <summary>0…1 without goggles after sunrise: 0.75 is «резь», 1 is blind for two days.</summary>
        public float Blindness;
        /// <summary>0…1. Over 1 the climber has to stand and breathe whether the player likes it or not.</summary>
        public float Pulse;
        /// <summary>Seconds of the forced stop still to serve.</summary>
        public float StopSeconds;
        /// <summary>Sips of hot left in the thermos (4–6 in a one-litre flask that is opened at −25 °C).</summary>
        public int ThermosSips = AscentRoute.ThermosSips;

        public Ams Phase => Ascent.Phase(SicknessLoad);
        public bool Has(Gear g) => (Gear & g) == g;
    }

    /// <summary>The ascent of Elbrus from the south as a set of rules, so that the 1 800 m from the top station to the
    /// West summit stop being a run across smooth snow. Nothing here touches the engine: every function takes the
    /// height, the two slope angles and the distance from the route line, and the runtime supplies all four.
    ///
    /// Where the numbers come from (the full write-up is in docs/ELBRUS.md):
    /// <list type="bullet">
    /// <item>The profile is measured along <see cref="Elbrus.SummitRoute"/> on our own height field: 6.69 km, 1 800 m
    /// of gain, in eight sections — the cat road to Приют 11 (9.7°), rolled firn to Pastukhov rocks (17.8°), the
    /// «зеркало» to 5 100 (24–27°, locally 35°), the step onto the shelf (cross-slope 15 → 34°), the косая полка
    /// (940 m at 3–8° along and 23–34° across), the saddle, the summit rise on fixed rope, and the plateau.</item>
    /// <item>Times from guiding companies: 7–10 h up, 3.5–5 h down, 4–6 h up from a snow-cat drop at 5 100.</item>
    /// <item>The hypoxia curve is calibrated against the 230–280 m of gain per hour parties actually manage above
    /// the rocks, not against a physiological model.</item>
    /// <item>МЧС accident material: a Swede without an ice axe died on Pastukhov rocks; rescuers logged falls of
    /// 300 and 150 m. 70 % of the deaths on this side follow from losing the route in bad visibility, which is why
    /// <see cref="AscentRoute"/> is as long as this file.</item>
    /// </list>
    /// The shape of the model — which multiplier does what — is game balance, not a measurement.</summary>
    public static class Ascent
    {
        // ── the belts, by measured height ─────────────────────────────────────────────────────────────────

        public const float FirnFromEle = 4000f, IceFromEle = 4650f, SastrugiFromEle = 5100f,
                           LooseFromEle = 5400f, CrustFromEle = 5550f;

        /// <summary>The косая полка as the rules see it: the wind funnel, the monotony and the fatal run-out all live
        /// in this band of measured height. The POI of the same name sits at 5 380 m on the DEM; the traverse itself
        /// starts at 5 290 m, some 445 m earlier along <see cref="Elbrus.SummitRoute"/>.</summary>
        public const float ShelfFromEle = 5250f, ShelfToEle = 5400f;

        public static Surface SurfaceAt(float ele)
            => ele < FirnFromEle ? Surface.Groomed
             : ele < IceFromEle ? Surface.Firn
             : ele < SastrugiFromEle ? Surface.Ice
             : ele < LooseFromEle ? Surface.Sastrugi
             : ele < CrustFromEle ? Surface.LooseOverIce
             : Surface.WindCrust;

        /// <summary>What one belt does to a walker.</summary>
        public readonly struct SurfaceSpec
        {
            public readonly Surface Kind;
            /// <summary>Russian, for the HUD.</summary>
            public readonly string Title;
            /// <summary>Speed multiplier with no crampons on. With them it is 1 everywhere.</summary>
            public readonly float BarefootSpeed;
            /// <summary>Chance per metre of the feet going, before <see cref="CramponSlip"/>.</summary>
            public readonly float SlipPerMetre;
            /// <summary>Below this steepness nothing slides: a boot edge holds you where the ground is nearly flat.</summary>
            public readonly float SlipFromDeg;
            /// <summary>A slide that starts in this belt ends on the ice cliffs below the saddle.</summary>
            public readonly bool Deadly;

            public SurfaceSpec(Surface kind, string title, float barefootSpeed, float slipPerMetre, float slipFromDeg, bool deadly)
            { Kind = kind; Title = title; BarefootSpeed = barefootSpeed; SlipPerMetre = slipPerMetre; SlipFromDeg = slipFromDeg; Deadly = deadly; }
        }

        static readonly SurfaceSpec[] specs =
        {
            new SurfaceSpec(Surface.Groomed,      "Ратрачная колея",      1.00f, 0f,     90f, false),
            new SurfaceSpec(Surface.Firn,         "Укатанный фирн",        .85f, .0005f, 15f, false),
            new SurfaceSpec(Surface.Ice,          "Жёсткий фирн, лёд",     .40f, .0080f, 10f, false),
            new SurfaceSpec(Surface.Sastrugi,     "Фирн и заструги",       .50f, .0050f, 10f, true),
            new SurfaceSpec(Surface.LooseOverIce, "Рыхлый снег на льду",   .45f, .0060f, 10f, false),
            new SurfaceSpec(Surface.WindCrust,    "Ветровой наст",         .70f, .0010f,  8f, false),
        };

        public static SurfaceSpec Spec(Surface s) => specs[(int)s];
        public static SurfaceSpec SpecAt(float ele) => specs[(int)SurfaceAt(ele)];
        public static string SurfaceTitle(float ele) => SpecAt(ele).Title;

        // ── crampons and the ice axe ──────────────────────────────────────────────────────────────────────

        /// <summary>Seconds of standing still to put crampons on (10–15 with cold fingers). They cannot go on while
        /// walking, which is the whole point of the gate: you stop below the ice or you do not pass it.</summary>
        public const float CramponSeconds = 12f;
        /// <summary>What crampons leave of the chance of slipping.</summary>
        public const float CramponSlip = .1f;

        /// <summary>Speed multiplier of the surface alone. Crampons buy back everything the ice took (×2.5 in the
        /// «зеркало» belt) and nothing more — they do not make anybody faster than the groomed road.</summary>
        public static float FootingFactor(float ele, bool cramponsOn)
            => cramponsOn ? 1f : SpecAt(ele).BarefootSpeed;

        /// <summary>Chance per metre of losing the feet here. The steepness that counts is the greater of the two
        /// angles: the косая полка is flat along the track and 23–34° across it, and it is the across that kills.</summary>
        public static float SlipPerMetre(RoutePoint p, bool cramponsOn) => SlipPerMetre(p, cramponsOn, Going.Up);

        /// <summary>The same, for a step taken in a known direction. Going down it is
        /// <see cref="DescentSlip"/> times worse, and that is the one number the whole "descent is the dangerous
        /// half" idea rests on.</summary>
        public static float SlipPerMetre(RoutePoint p, bool cramponsOn, Going going)
        {
            var s = SpecAt(p.Ele);
            if (p.Steepness <= s.SlipFromDeg) return 0f;
            return s.SlipPerMetre * (cramponsOn ? CramponSlip : 1f) * (going == Going.Down ? DescentSlip : 1f);
        }

        static readonly (float at, float value)[] arrest =
            { (10f, .95f), (20f, .80f), (25f, .60f), (30f, .40f), (35f, .10f), (45f, .02f) };

        /// <summary>Chance of stopping a slide with the ice axe once it has started. Without an axe there is no
        /// self-arrest at all — that is not a penalty, it is the absence of the move.</summary>
        public static float SelfArrestChance(float slopeDeg, Gear gear) => SelfArrestChance(slopeDeg, gear, Going.Up);

        /// <summary>The same, for a known direction. A climber who goes over backwards has to turn face-down onto the
        /// axe before it bites, and by the descent the arms that do it have been walking for eight hours.</summary>
        public static float SelfArrestChance(float slopeDeg, Gear gear, Going going)
            => (gear & Gear.IceAxe) == 0 ? 0f
             : Curve(arrest, Math.Abs(slopeDeg)) * (going == Going.Down ? DescentArrest : 1f);

        // ── up or down ────────────────────────────────────────────────────────────────────────────────────

        /// <summary>What walking downhill is worth to the pace, on top of the air the lungs get back
        /// (<see cref="DescentHypoxiaEase"/>). Guiding companies quote 7–10 h up against 3.5–5 h down on the same
        /// line, so the whole descent has to come out at roughly half the climb or a little less; the two multipliers
        /// together do that, because the slow upper half is where the air gives the most back.</summary>
        public const float DescentSpeed = 1.6f;

        /// <summary>Share of the thin-air penalty handed back on the way down. Losing height is the one thing that
        /// helps hypoxia, and a body that is not lifting itself any more spends far less of what it breathes; at
        /// 5 400 m the multiplier goes from 0.40 to 0.70 by this alone.</summary>
        public const float DescentHypoxiaEase = .5f;

        /// <summary>How much likelier the feet are to go downhill: the body faces out from the slope instead of into
        /// it, the crampon takes the snow on the flat of the foot instead of on the front points, and the legs that
        /// have to brake every step are the ones that have just done 1 800 m of climbing. Game balance, calibrated so
        /// that the «зеркало» belt is the place a careless descent ends.</summary>
        public const float DescentSlip = 1.6f;

        /// <summary>And what is left of the self-arrest going down.</summary>
        public const float DescentArrest = .7f;

        /// <summary>The southern slope faces the sun, and after ten in the morning the rolled lane below the
        /// «зеркало» stops carrying: the snow-cat track goes to <see cref="SlushSpeed"/> of its pace and below
        /// <see cref="SlushToEle"/> the going is porridge. It is a property of the snow, not of the direction, so it
        /// is charged both ways — but a party on the way up passed here before eight, and only the descent pays.</summary>
        public const float SlushFromHour = 10f, SlushToEle = 4600f, SlushSpeed = .7f;

        public static bool Slush(float ele, float hour) => hour >= SlushFromHour && ele < SlushToEle;
        public static float SlushFactor(float ele, float hour) => Slush(ele, hour) ? SlushSpeed : 1f;

        /// <summary>The pace multiplier of the direction alone.</summary>
        public static float GoingFactor(Going going) => going == Going.Down ? DescentSpeed : 1f;

        public const float ShelfRunoutMin = 300f, ShelfRunoutMax = 600f;

        /// <summary>How far an unarrested slide carries, metres. On the shelf it is 300–600 m down onto the ice
        /// cliffs; elsewhere it is the 40–300 m the rescue logs record.</summary>
        public static float RunoutM(RoutePoint p)
        {
            var s = SpecAt(p.Ele);
            float steep = Clamp01((p.Steepness - s.SlipFromDeg) / 20f);
            return s.Deadly ? ShelfRunoutMin + (ShelfRunoutMax - ShelfRunoutMin) * steep : 40f + 260f * steep;
        }

        /// <summary>Whether a slide from here is the end of the run.</summary>
        public static bool FallIsFatal(RoutePoint p) => SpecAt(p.Ele).Deadly || RunoutM(p) >= 250f;

        // ── hypoxia ───────────────────────────────────────────────────────────────────────────────────────

        static readonly (float at, float value)[] hypoxia =
            { (3800f, 1f), (4300f, .85f), (4700f, .70f), (5100f, .55f), (5400f, .40f), (5642f, .30f) };

        /// <summary>Multiplier on the top speed from the air alone, calibrated so that a party makes the 230–280 m of
        /// gain per hour that is actually managed above the rocks. A badly acclimatised climber gets
        /// <see cref="AcclimFactor"/> of it.</summary>
        public static float Hypoxia(float ele, float acclim) => Curve(hypoxia, ele) * AcclimFactor(acclim);

        /// <summary>The same, for a step taken in a known direction: going down, <see cref="DescentHypoxiaEase"/> of
        /// whatever the air was taking away is handed back.</summary>
        public static float Hypoxia(float ele, float acclim, Going going)
        {
            float h = Hypoxia(ele, acclim);
            return going == Going.Down ? h + (1f - h) * DescentHypoxiaEase : h;
        }

        /// <summary>What poor acclimatisation does to the whole curve: 1.0 at acclim 1, 0.6 at acclim 0.3 — the
        /// figure the design brief fixes — and worse below that.</summary>
        public static float AcclimFactor(float acclim)
        {
            acclim = Clamp01(acclim);
            return acclim >= .3f ? .6f + .4f * (acclim - .3f) / .7f : .45f + .5f * acclim;
        }

        /// <summary>Above this nobody runs. Not "running costs more" — the move is gone.</summary>
        public const float RunCeiling = 4600f;
        public static bool MayRun(float ele) => ele < RunCeiling;

        /// <summary>Step-and-breathe: one step every two seconds, about 0.28 m/s. Below this pace the heart settles;
        /// above it, it climbs, and the thinner the air the faster.</summary>
        public const float BreathStepSeconds = 2f, StepM = .55f;
        public static float BreathPaceMs => StepM / BreathStepSeconds;

        public const float PulseClimb = .02f, PulseFall = .045f;
        /// <summary>Seconds of the stop the heart makes you take (10–20 s).</summary>
        public const float ForcedStopSeconds = 15f;

        /// <summary>Change in the heart-rate bar per second at this pace and this height.</summary>
        public static float PulseDelta(float ele, float acclim, float speedMs)
        {
            if (speedMs <= BreathPaceMs) return -PulseFall;
            float thin = 1f / Math.Max(.2f, Hypoxia(ele, acclim));
            return (speedMs - BreathPaceMs) * PulseClimb * thin;
        }

        /// <summary>Above this height strength comes back only standing still, and three times slower than at 3 800.</summary>
        public const float RestCeiling = 5000f;
        public const float SlowRest = 1f / 3f;
        /// <summary>What keeping the step-and-breathe pace is worth to the recovery.</summary>
        public const float BreathBonus = 1.35f;

        /// <summary>Share of the normal recovery rate available here. The runtime multiplies its own strength bar by
        /// it; the bar itself stays in <see cref="Participant"/>, this file never writes it.</summary>
        public static float RecoveryFactor(float ele, bool standing, bool breathPaced = false)
        {
            float f = ele < RestCeiling ? (standing ? 1f : .35f) : (standing ? SlowRest : 0f);
            return breathPaced ? f * BreathBonus : f;
        }

        // ── acclimatisation between runs ──────────────────────────────────────────────────────────────────

        /// <summary>One outing and the night after it. Climb high, sleep low: the gain is credited only if the night
        /// was spent at least <see cref="SleepLowerBy"/> below the day's high point.</summary>
        public readonly struct Sortie
        {
            public readonly float HighestEle, SleptEle, Days;
            public Sortie(float highestEle, float sleptEle, float days = 1f)
            { HighestEle = highestEle; SleptEle = sleptEle; Days = days; }
        }

        public const float TouchLowFrom = 3800f, TouchLowTo = 4200f, TouchHighFrom = 4600f, TouchHighTo = 4800f;
        public const float TouchLowGain = .10f, TouchHighGain = .20f;
        public const float SleepLowerBy = 300f;
        public const float DecayPerDay = .05f;

        static readonly (float at, float value)[] sleepGain =
            { (3700f, 0f), (3900f, .05f), (4100f, .08f), (4400f, .05f), (4800f, 0f) };

        /// <summary>What a night at this height is worth on its own. Above 4 400 it is worth less and less: sleeping
        /// high is what breaks people, not what acclimatises them.</summary>
        public static float SleepGain(float sleptEle) => Curve(sleepGain, sleptEle);

        /// <summary>What touching this height and coming back down is worth.</summary>
        public static float TouchGain(float highestEle, float sleptEle)
        {
            if (sleptEle > highestEle - SleepLowerBy) return 0f;
            if (highestEle >= TouchHighFrom) return TouchHighGain;
            if (highestEle >= TouchLowFrom) return TouchLowGain;
            return 0f;
        }

        /// <summary>The new acclimatisation after an outing. With no gain at all the body gives it back at 0.05 a day.</summary>
        public static float Acclimatise(float acclim, Sortie s)
        {
            float gain = TouchGain(s.HighestEle, s.SleptEle) + SleepGain(s.SleptEle);
            return Clamp01(gain > 0f ? acclim + gain : acclim - DecayPerDay * Math.Max(0f, s.Days));
        }

        // ── mountain sickness ─────────────────────────────────────────────────────────────────────────────

        /// <summary>Hours of undigested altitude at which each phase begins. The gap from the first headache to
        /// ataxia is 6–12 h of continued climbing, which is what the four thresholds are spaced for.</summary>
        public const float HeadacheAt = 1f, NauseaAt = 4.5f, AtaxiaAt = 9f, EdemaAt = 14f;

        public static Ams Phase(float load)
            => load >= EdemaAt ? Ams.Edema : load >= AtaxiaAt ? Ams.Ataxia
             : load >= NauseaAt ? Ams.Nausea : load >= HeadacheAt ? Ams.Headache : Ams.None;

        /// <summary>The height the body tolerates at this acclimatisation: 3 770 m at acclim 0.3, 5 100 m at 1.0.
        /// On 3 500–5 000 m half to three quarters of people get symptoms, and this curve is why.</summary>
        public const float ToleratedBase = 3200f, ToleratedGain = 1900f;
        public static float ToleratedEle(float acclim) => ToleratedBase + ToleratedGain * Clamp01(acclim);

        /// <summary>Above this the body does not adapt at all, however well prepared — it only spends reserve.</summary>
        public const float NoAdaptAbove = 5000f, ReserveBurn = .25f;
        public const float MaxPressure = 1.5f;
        /// <summary>Being dry makes the sickness come 40 % faster.</summary>
        public const float DehydrationBoost = .4f;

        /// <summary>Hours of sickness load gathered per hour spent here. Negative below the tolerated height — that is
        /// the 500 m of descent that stops an oedema, expressed as a rate.</summary>
        public static float SicknessRate(float ele, float acclim, float dehydration)
        {
            float p = (ele - ToleratedEle(acclim)) / 600f;
            if (p > MaxPressure) p = MaxPressure;
            if (ele >= NoAdaptAbove && p < ReserveBurn) p = ReserveBurn;
            if (p > 0f) return p * (1f + DehydrationBoost * Clamp01(dehydration));
            return Math.Max(-1f, p);
        }

        /// <summary>Headache costs a tenth of the strength bar.</summary>
        public static float SicknessStrength(Ams a) => a >= Ams.Headache ? .9f : 1f;
        /// <summary>From the nausea on, standing still gives nothing back.</summary>
        public static bool RecoversInPlace(Ams a) => a < Ams.Nausea;
        /// <summary>Metres per second the controls pull sideways. On the косая полка this is the killing move.</summary>
        public static float AtaxiaDriftMs(Ams a) => a >= Ams.Ataxia ? .35f : 0f;

        /// <summary>Oedema: a timer of 20–40 minutes. The only thing that stops it is losing 500 m of height.</summary>
        public const float EdemaTimerSeconds = 1800f, EdemaDescentM = 500f;

        public static string PhaseTitle(Ams a) => a switch
        {
            Ams.Headache => "Горная болезнь · головная боль",
            Ams.Nausea => "Горная болезнь · тошнота",
            Ams.Ataxia => "Горная болезнь · атаксия",
            Ams.Edema => "Отёк",
            _ => "",
        };

        /// <summary>The line for the HUD, chosen so that every phase is announced before the next one arrives and the
        /// first warning comes before there is any phase at all. A player must always be able to turn round in time.</summary>
        public static string Warning(Climber c, RoutePoint p) => Warning(c, p, Going.Up);

        /// <summary>The same, knowing which way the party is walking. On the descent the sickness lines stay — they
        /// are still true — but the corridor between the lava ridges outranks all of them, because it is the thing
        /// that actually kills here and the only one a player can still do something about.</summary>
        public static string Warning(Climber c, RoutePoint p, Going going)
        {
            if (going == Going.Down && AscentRoute.MissedTheGate(p.Ele, p.OffRouteM))
                return "Скалы Пастухова остались в стороне. Между двумя грядами — коридор; вы идёте мимо него.";
            if (c.Phase == Ams.Edema) return "Отёк. Дышать нечем. Спасает только сброс 500 м — вниз, немедленно.";
            if (c.Phase == Ams.Ataxia) return "Ноги не слушаются, ведёт вбок. На полке это смертельно.";
            if (c.Phase == Ams.Nausea) return "Тошнит. Отдых на месте больше не возвращает силы.";
            if (c.Phase == Ams.Headache) return "Голову сдавило. Дальше вверх — хуже.";
            if (c.SicknessLoad > HeadacheAt * .5f) return "Давит виски. Высота набирается быстрее, чем тело привыкает.";
            if (SicknessRate(p.Ele, c.Acclimatisation, c.Dehydration) > 0f) return "Дыхания не хватает. Здесь уже не акклиматизируются.";
            return "";
        }

        // ── monotony on the shelf ─────────────────────────────────────────────────────────────────────────

        /// <summary>The косая полка is 940 m at 3–8° walked in the dark for an hour and a half to two hours. People
        /// fall asleep on their feet on it. A sip of hot from the thermos clears it; nothing else does.</summary>
        public const float DrowsyPerHour = .55f, DrowsySpeed = .7f, DrowsyFrom = .5f;

        public static float DrowsinessRate(RoutePoint p, bool dark)
        {
            if (!dark || p.Ele < ShelfFromEle || p.Ele > ShelfToEle) return 0f;
            if (Math.Abs(p.SlopeDeg) > 10f) return 0f;                 // a real climb wakes you up
            return DrowsyPerHour / 3600f;
        }

        /// <summary>What being half asleep does to the pace.</summary>
        public static float DrowsyFactor(float drowsiness)
            => drowsiness >= DrowsyFrom ? DrowsySpeed : 1f;

        // ── the gear gate ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>Pastukhov rocks. Above this height a climber without the full kit is not slowed down — he is
        /// turned back. Below it nothing is required.</summary>
        public const float GearGateEle = 4650f;
        /// <summary>Three screw-gate carabiners on the cow's tail.</summary>
        public const int Carabiners = 3;

        public const Gear Required = Gear.Crampons | Gear.IceAxe | Gear.Harness | Gear.Helmet | Gear.Goggles
            | Gear.Mask | Gear.DownJacket | Gear.Balaclava | Gear.Mittens | Gear.SpareGloves | Gear.Thermos
            | Gear.Headlamp | Gear.SpaceBlanket;

        public static Gear Missing(Gear have) => Required & ~have;
        public static bool MayPassGate(float ele, Gear have) => ele < GearGateEle || Missing(have) == Gear.None;

        public static string GearTitle(Gear one) => one switch
        {
            Gear.Crampons => "кошки",
            Gear.IceAxe => "ледоруб",
            Gear.Harness => "страховочная система с усом и карабинами",
            Gear.Helmet => "каска",
            Gear.Goggles => "очки S3–S4",
            Gear.Mask => "маска S2–S3",
            Gear.DownJacket => "пуховка",
            Gear.Balaclava => "балаклава",
            Gear.Mittens => "варежки",
            Gear.SpareGloves => "запасные перчатки",
            Gear.Thermos => "термос",
            Gear.Headlamp => "налобный фонарь",
            Gear.SpaceBlanket => "спасодеяло",
            _ => "",
        };

        /// <summary>What is still missing, for the HUD, or "" when the kit is complete.</summary>
        public static string MissingList(Gear have)
        {
            var gone = Missing(have);
            if (gone == Gear.None) return "";
            var sb = new System.Text.StringBuilder();
            foreach (Gear one in Enum.GetValues(typeof(Gear)))
            {
                if (one == Gear.None || (gone & one) == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(GearTitle(one));
            }
            return sb.ToString();
        }

        // ── one tick ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Everything the runtime measures for this tick. All of it is engine-free: heights and angles off
        /// the DEM, the wind and the visibility off the weather, the rest off the player's own input.</summary>
        public struct Step
        {
            public RoutePoint Where;
            /// <summary>Air at the climber. The runtime runs <see cref="AscentCold.WindAt"/> and
            /// <see cref="AscentCold.AirTempC"/> before filling this in, because only it knows where the saddle is.</summary>
            public MountainAir Air;
            /// <summary>The pace the player is asking for, m/s. 0 when standing.</summary>
            public float SpeedMs;
            /// <summary>Daylight for the snow-blindness rule; the shelf monotony uses the opposite.</summary>
            public bool Daylight;
            /// <summary>Inside a hut, a trench or the snow-cat cab: the frostbite pools stop burning.</summary>
            public bool Sheltered;
            /// <summary>Mittens off to work the carabiners or the axe. The hands start burning at once.</summary>
            public bool BareHands;

            /// <summary>Which half of the day this step belongs to. <see cref="Going.Up"/> by default, so a runtime
            /// that has not been taught about the descent yet behaves exactly as it did.</summary>
            public Going Way;
            /// <summary>The hour of the day (<see cref="AscentRoute.HourAt"/>), for the snow that goes soft after ten.
            /// 0 by default, which is the middle of the night and charges nothing.</summary>
            public float Hour;
            /// <summary>A snow-cat has been up this morning and its track is underfoot: the route can be read off it
            /// even in cloud (<see cref="AscentRoute.CanReadTheRoute"/>).</summary>
            public bool CatTrack;
            /// <summary>0 (fresh) … 1 (nothing left in the legs) — the runtime's own strength bar, inverted. It only
            /// feeds the wandering rule: a spent party keeps a line worse than a fresh one.</summary>
            public float Tiredness;

            public Step(RoutePoint where, MountainAir air, float speedMs = 0f)
            {
                Where = where; Air = air; SpeedMs = speedMs;
                Daylight = true; Sheltered = false; BareHands = false;
                Way = Going.Up; Hour = 0f; CatTrack = false; Tiredness = 0f;
            }
        }

        /// <summary>What the rules answer for this tick. Everything is a multiplier or a chance: the runtime keeps its
        /// own speed and strength model and applies these to it.</summary>
        public readonly struct Report
        {
            /// <summary>Multiply the locomotion's speed by this: surface × air × sickness × drowsiness.</summary>
            public readonly float SpeedFactor;
            /// <summary>Multiply the strength recovery by this. 0 means nothing comes back here.</summary>
            public readonly float RecoveryFactor;
            public readonly bool MayRun;
            /// <summary>Chance the feet go, per metre.</summary>
            public readonly float SlipPerMetre;
            /// <summary>Chance of arresting the slide once it has started.</summary>
            public readonly float SelfArrest;
            public readonly float RunoutM;
            public readonly bool Deadly;
            /// <summary>Sideways metres per second: the wind's push plus the ataxia's pull.</summary>
            public readonly float DriftMs;
            public readonly Ams Sickness;
            /// <summary>Wind-chill equivalent temperature, °C.</summary>
            public readonly float FeelsC;
            /// <summary>Minutes to frostbite of bare skin at this index, or +∞ when there is no risk.</summary>
            public readonly float FrostbiteMinutes;
            /// <summary>True while the heart is making the climber stand still.</summary>
            public readonly bool MustStop;
            /// <summary>Nothing marks the line from here: no visibility, no wand, no cable, no snow-cat track
            /// (<see cref="AscentRoute.CanReadTheRoute"/>).</summary>
            public readonly bool RouteLost;
            /// <summary>Metres of sideways error gathered per metre walked while the line cannot be read
            /// (<see cref="AscentRoute.WanderPerMetre"/>). The runtime multiplies it by the metres covered and picks
            /// the side; 0 whenever anything at all still marks the route.</summary>
            public readonly float WanderPerMetre;
            /// <summary>Standing in the belt of Pastukhov rocks and outside the corridor between the two lava ridges
            /// (<see cref="AscentRoute.MissedTheGate"/>). Going down, this is the thing that kills people here.</summary>
            public readonly bool MissedTheGate;
            /// <summary>Russian line for the HUD, or "".</summary>
            public readonly string Warning;

            public Report(float speedFactor, float recoveryFactor, bool mayRun, float slipPerMetre, float selfArrest,
                float runoutM, bool deadly, float driftMs, Ams sickness, float feelsC, float frostbiteMinutes,
                bool mustStop, string warning, bool routeLost = false, float wanderPerMetre = 0f,
                bool missedTheGate = false)
            {
                SpeedFactor = speedFactor; RecoveryFactor = recoveryFactor; MayRun = mayRun;
                SlipPerMetre = slipPerMetre; SelfArrest = selfArrest; RunoutM = runoutM; Deadly = deadly;
                DriftMs = driftMs; Sickness = sickness; FeelsC = feelsC; FrostbiteMinutes = frostbiteMinutes;
                MustStop = mustStop; Warning = warning;
                RouteLost = routeLost; WanderPerMetre = wanderPerMetre; MissedTheGate = missedTheGate;
            }
        }

        /// <summary>The speed multiplier on its own, for anything that wants to ask without ticking: the surface, the
        /// air, the sickness and the drowsiness. Below 4 000 m on the cat road with a fresh climber it is 1; just
        /// above the rocks without crampons it is about a quarter of that, and crampons give 2.5 of those back.</summary>
        public static float SpeedFactor(Climber c, RoutePoint p) => SpeedFactor(c, p, Going.Up, 0f);

        /// <summary>The same, for a step taken in a known direction at a known hour: downhill the air gives some of
        /// itself back and the legs carry themselves, and after ten in the morning the lane below the «зеркало» has
        /// turned to porridge and takes a third of it away again.</summary>
        public static float SpeedFactor(Climber c, RoutePoint p, Going going, float hour)
            => FootingFactor(p.Ele, c.CramponsOn)
             * Hypoxia(p.Ele, c.Acclimatisation, going)
             * SicknessStrength(c.Phase)
             * DrowsyFactor(c.Drowsiness)
             * GoingFactor(going)
             * SlushFactor(p.Ele, hour);

        /// <summary>Advances the climber by dt seconds and answers for this tick. The runtime keeps the position, the
        /// strength bar and the dice; this only moves the pools that belong to the mountain.</summary>
        public static Report Tick(Climber c, Step s, float dt)
        {
            dt = Math.Max(0f, dt);
            var p = s.Where;
            if (p.Ele > c.HighestEle) c.HighestEle = p.Ele;

            // the heart
            bool standing = s.SpeedMs <= 0f || c.StopSeconds > 0f;
            if (c.StopSeconds > 0f) c.StopSeconds = Math.Max(0f, c.StopSeconds - dt);
            c.Pulse = Clamp01(c.Pulse + PulseDelta(p.Ele, c.Acclimatisation, standing ? 0f : s.SpeedMs) * dt);
            if (c.Pulse >= 1f && c.StopSeconds <= 0f) { c.StopSeconds = ForcedStopSeconds; standing = true; }

            // the altitude
            float rate = SicknessRate(p.Ele, c.Acclimatisation, c.Dehydration);
            c.SicknessLoad = Math.Max(0f, c.SicknessLoad + rate * dt / 3600f);
            if (c.Phase == Ams.Edema) c.EdemaSeconds += dt; else c.EdemaSeconds = 0f;

            // the cold, three pools that never share heat
            float feels = AscentCold.FeelsC(s.Air.TempC, s.Air.WindMs);
            float minutes = AscentCold.FrostbiteMinutes(feels);
            if (!s.Sheltered)
            {
                bool faceCovered = c.Has(Gear.Balaclava) || c.Has(Gear.Mask);
                bool handsCovered = c.Has(Gear.Mittens) && !s.BareHands;
                c.Face = Clamp01(c.Face + AscentCold.FreezeRate(Limb.Face, feels, faceCovered, !standing) * dt);
                c.Hands = Clamp01(c.Hands + AscentCold.FreezeRate(Limb.Hands, feels, handsCovered, !standing) * dt);
                c.Feet = Clamp01(c.Feet + AscentCold.FreezeRate(Limb.Feet, feels, true, !standing) * dt);
            }

            // the long flat dark traverse, and the sun on the snow
            c.Drowsiness = Clamp01(c.Drowsiness + DrowsinessRate(p, !s.Daylight) * dt);
            c.Blindness = Clamp01(c.Blindness + AscentRoute.BlindnessRate(p.Ele, c.Has(Gear.Goggles), s.Daylight) * dt);
            c.Dehydration = Clamp01(c.Dehydration + AscentRoute.DryingRate(p.Ele, !standing) * dt);

            float drift = AscentCold.SideDriftMs(s.Air.WindMs) + AtaxiaDriftMs(c.Phase);
            float recovery = RecoversInPlace(c.Phase) ? RecoveryFactor(p.Ele, standing) : 0f;

            // the second half of the day: can the line still be read, and how fast does a party that cannot read it
            // drift off it. Going down this is the rule that decides the run — not the cold and not the slip.
            bool canRead = AscentRoute.CanReadTheRoute(p.Ele, p.OffRouteM, s.Air.VisibilityM, s.Air.FreshSnowCm, s.CatTrack);
            float wander = AscentRoute.WanderPerMetre(s.Way, canRead, c.Phase, s.Tiredness);

            return new Report(
                SpeedFactor(c, p, s.Way, s.Hour), recovery, MayRun(p.Ele),
                SlipPerMetre(p, c.CramponsOn, s.Way), SelfArrestChance(p.Steepness, c.Gear, s.Way), RunoutM(p), FallIsFatal(p),
                drift, c.Phase, feels, minutes, c.StopSeconds > 0f, Warning(c, p, s.Way),
                !canRead, wander, AscentRoute.MissedTheGate(p.Ele, p.OffRouteM));
        }

        // ── helpers ───────────────────────────────────────────────────────────────────────────────────────

        internal static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>Linear interpolation over a table sorted by <c>at</c>, clamped at both ends.</summary>
        public static float Curve((float at, float value)[] table, float at)
        {
            if (at <= table[0].at) return table[0].value;
            for (int i = 1; i < table.Length; i++)
            {
                if (at > table[i].at) continue;
                var (a, b) = (table[i - 1], table[i]);
                float span = b.at - a.at;
                return span <= 0f ? b.value : a.value + (b.value - a.value) * (at - a.at) / span;
            }
            return table[table.Length - 1].value;
        }
    }
}
