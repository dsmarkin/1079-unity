#if !HEIGHT1079_NO_ELBRUS
// Compiled only when the southern slope of Elbrus is in the build (docs/ELBRUS.md). It has to live in
// Height1079.Runtime and not in the location's own assembly because a partial class cannot be split across
// assemblies, and because the rest of Height1079.Runtime's Elbrus partials name what is in here.
using System;
using Unity.Netcode;
using UnityEngine;
using Height1079.Core;
using Height1079.Night;

namespace Height1079.Runtime
{
    /// <summary>One climber's state as it travels from the host to everybody else. The host owns the pools — it is the
    /// only one that runs <see cref="Ascent.Tick"/> — and publishes them here; every client rebuilds a
    /// <see cref="Climber"/> from this and works out the rest for itself off the height field, which both sides have.
    /// Nothing that can be derived is sent: the surface, the slope angles, the warning line and the speed multiplier
    /// are all pure functions of this plus a position.</summary>
    public struct ClimbNet : INetworkSerializable, IEquatable<ClimbNet>
    {
        /// <summary>1 — crampons on the boots · 2 — running is still possible here · 4 — the heart is making him
        /// stand · 8 — a slide from here ends on the ice cliffs · 16 — the kit is short and the gate is above ·
        /// 32 — daylight · 64 — the МЧС cable is within reach · 128 — the route cannot be read from here.</summary>
        [Flags]
        public enum Mark : byte
        {
            None = 0, Crampons = 1, MayRun = 2, MustStop = 4, Deadly = 8,
            Barred = 16, Daylight = 32, OnRope = 64, Lost = 128,
        }

        /// <summary>What the host counted in the rucksack, as <see cref="Gear"/>.</summary>
        public ushort Kit;
        public byte Marks;
        /// <summary>The three frostbite pools, the dryness, the drowsiness, the snow-blindness and the heart, 0…255.</summary>
        public byte Hands, Feet, Face, Dry, Sleep, Blind, Pulse;
        /// <summary>Acclimatisation 0…255 and hours of undigested altitude ×10.</summary>
        public byte Acclim, Load;
        public byte Sips;
        /// <summary>Speed multiplier ×100, recovery share ×100, sideways drift ×100 m/s. The speed is packed at a
        /// hundred and not two hundred because a descent is allowed to be faster than a walk
        /// (<see cref="Ascent.DescentSpeed"/>) and 1.6 would have saturated the byte.</summary>
        public byte Speed, Recovery, Drift;
        /// <summary>Felt temperature in whole degrees, and the wind at the climber ×4 m/s.</summary>
        public short Feels;
        public byte Wind;
        /// <summary>What his hands are busy with (<see cref="ClimbJob"/>) and how far along it is, 0…255.</summary>
        public byte Job, Progress;
        /// <summary>The second half of the day, which would not fit in <see cref="Marks"/>: 1 — this step is going
        /// DOWN, 2 — standing in the belt of Pastukhov rocks and outside the corridor between the lava ridges
        /// (<see cref="AscentRoute.MissedTheGate"/>), 4 — registered with the rescuers, 8 — somebody is on the way.</summary>
        public byte Marks2;
        /// <summary>Centimetres of sideways error gathered per metre walked while nothing marks the line, SIGNED —
        /// the host picks the side and holds it, so a lost party drifts steadily off the route instead of shivering
        /// about it (<see cref="AscentRoute.WanderPerMetre"/>). 0 whenever the route can be read.</summary>
        public sbyte Wander;

        /// <summary>The bits of <see cref="Marks2"/>.</summary>
        [Flags]
        public enum Mark2 : byte { None = 0, Down = 1, MissedGate = 2, Filed = 4, HelpComing = 8 }

        public Gear Gear => (Gear)Kit;
        public bool Has(Mark m) => ((Mark)Marks & m) != 0;
        public bool Has(Mark2 m) => ((Mark2)Marks2 & m) != 0;
        /// <summary>Metres of sideways error per metre walked, signed.</summary>
        public float WanderPerMetre => Wander / 100f;
        public Going Way => Has(Mark2.Down) ? Going.Down : Going.Up;
        public float SpeedFactor => Speed / 100f;
        public float RecoveryFactor => Recovery / 100f;
        public float DriftMs => Drift / 100f;
        public float WindMs => Wind / 4f;
        public float SickLoad => Load / 10f;
        public Ams Phase => Ascent.Phase(SickLoad);

        /// <summary>The climber as the client sees him: enough of one for every rule that only reads the pools.</summary>
        public Climber Mirror()
        {
            return new Climber
            {
                Gear = Gear,
                CramponsOn = Has(Mark.Crampons),
                Acclimatisation = Acclim / 255f,
                SicknessLoad = SickLoad,
                Hands = Hands / 255f,
                Feet = Feet / 255f,
                Face = Face / 255f,
                Dehydration = Dry / 255f,
                Drowsiness = Sleep / 255f,
                Blindness = Blind / 255f,
                Pulse = Pulse / 255f,
                ThermosSips = Sips,
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Kit); s.SerializeValue(ref Marks);
            s.SerializeValue(ref Hands); s.SerializeValue(ref Feet); s.SerializeValue(ref Face);
            s.SerializeValue(ref Dry); s.SerializeValue(ref Sleep); s.SerializeValue(ref Blind); s.SerializeValue(ref Pulse);
            s.SerializeValue(ref Acclim); s.SerializeValue(ref Load); s.SerializeValue(ref Sips);
            s.SerializeValue(ref Speed); s.SerializeValue(ref Recovery); s.SerializeValue(ref Drift);
            s.SerializeValue(ref Feels); s.SerializeValue(ref Wind);
            s.SerializeValue(ref Job); s.SerializeValue(ref Progress);
            s.SerializeValue(ref Marks2); s.SerializeValue(ref Wander);
        }

        public bool Equals(ClimbNet o)
            => Kit == o.Kit && Marks == o.Marks && Hands == o.Hands && Feet == o.Feet && Face == o.Face
            && Dry == o.Dry && Sleep == o.Sleep && Blind == o.Blind && Pulse == o.Pulse
            && Acclim == o.Acclim && Load == o.Load && Sips == o.Sips
            && Speed == o.Speed && Recovery == o.Recovery && Drift == o.Drift
            && Feels == o.Feels && Wind == o.Wind && Job == o.Job && Progress == o.Progress
            && Marks2 == o.Marks2 && Wander == o.Wander;

        public override int GetHashCode() => Kit << 16 | Marks << 8 | Speed;
    }

    /// <summary>What a climber can be doing with his hands instead of walking. Both take time standing still and both
    /// are given up the moment he moves.</summary>
    public enum ClimbJob : byte
    {
        None = 0,
        /// <summary>Putting crampons on: <see cref="Ascent.CramponSeconds"/> standing, never on the move.</summary>
        CramponsOn = 1,
        CramponsOff = 2,
        /// <summary>A sip of hot out of the thermos: it is the only thing that clears the drowsiness of the shelf.</summary>
        Sip = 3,
        /// <summary>Putting the двойка up (<see cref="Camp.SecondsToPitch"/>). Above Pastukhov rocks the platform has
        /// to be cut first, and the job is four minutes longer for it.</summary>
        Pitch = 4,
        /// <summary>And taking it down again, back into the rucksack.</summary>
        Strike = 5,
    }

    /// <summary>What the two sides of the wire both work out for themselves about the mountain: where a point of the
    /// route is, what the air is doing there, and what time it is. Everything here is a pure function of the height
    /// field, the weather and the run clock, so the host and the client agree without a byte being sent.
    ///
    /// Silent everywhere but on the southern slope: on Kholat Syakhl <see cref="On"/> is false and nothing below is
    /// ever called.</summary>
    public static class Climb
    {
        public static bool On => Height1079.Core.World.IsElbrus;

        /// <summary>How far apart the two height samples of a slope measurement are. One DEM cell of the Elbrus grid
        /// is 6 m, so anything shorter measures the bilinear interpolation rather than the mountain.</summary>
        public const float SlopeSpan = 7f;

        /// <summary>Where the climber is, as the rules want it: the height under his feet, the slope along the way he
        /// is going, the slope across it, and how far he stands from the line of the route.
        ///
        /// The cross slope is the whole reason the косая полка is dangerous — three to eight degrees along the track
        /// and twenty-three to thirty-four across it — so it is measured honestly, as the gradient perpendicular to
        /// the direction of travel, and not taken from the fall line.
        ///
        /// The offset is signed <b>plus to the right going up</b>: that is the sign the whole world was laid out by
        /// (crevasses on the right of the snow-cat lane, brooks on the left, see docs/ELBRUS.md).</summary>
        public static RoutePoint Point(HeightField dem, Vector3 pos, Vector3 dir)
        {
            if (dem == null) return new RoutePoint(pos.y);
            var f = new Vector2(dir.x, dir.z);
            if (f.sqrMagnitude < 1e-4f) f = new Vector2(0, 1);
            f.Normalize();
            // right of a heading (fx, fz) is (fz, −fx) — the same hand as Vector3.Cross(up, forward)
            var r = new Vector2(f.y, -f.x);
            float ele = dem.Sample(pos.x, pos.z);
            float along = Slope(dem, pos.x, pos.z, f);
            float across = Slope(dem, pos.x, pos.z, r);
            var (_, off) = Elbrus.Nearest(Elbrus.SummitRoute, pos.x, pos.z);
            return new RoutePoint(ele, along, Mathf.Abs(across), off);
        }

        /// <summary>Slope in degrees along a unit direction on the ground plane, positive climbing.</summary>
        static float Slope(HeightField dem, float x, float z, Vector2 dir)
        {
            float back = dem.Sample(x - dir.x * SlopeSpan, z - dir.y * SlopeSpan);
            float ahead = dem.Sample(x + dir.x * SlopeSpan, z + dir.y * SlopeSpan);
            return Mathf.Atan2(ahead - back, 2f * SlopeSpan) * Mathf.Rad2Deg;
        }

        // ── the air ───────────────────────────────────────────────────────────────────────────────────────

        /// <summary>A July morning on the meadow at 2 350 m. The lapse rate of <see cref="AscentCold"/> takes it from
        /// here to −12 °C on the summit, which is what the summit measures in summer. It is the fallback: a session
        /// that has drawn a day uses that day's own base (<see cref="MountainDay"/>).</summary>
        public const float MeadowBaseC = 8f;

        /// <summary>Air at the meadow at the coldest hour, °C — the day the host drew, or the July default.</summary>
        public static float BaseTempC => MountainDay.Known ? MountainDay.Day.BaseTempC : MeadowBaseC;
        /// <summary>What the weather's own 0…1 wind means in metres per second down in the valley, and how much of it
        /// the height adds before the funnel of the shelf and the saddle gets hold of it.</summary>
        public const float CalmMs = 2f, GaleMs = 12f, HeightGainMs = .6f;

        /// <summary>Wind on the meadow, m/s, from the weather the whole game shares.</summary>
        public static float BaseWindMs => CalmMs + GaleMs * Mathf.Clamp01(Weather.Wind);

        /// <summary>How much of the mountain can be seen. The day drawn from the save's seed decides it
        /// (<see cref="MountainDay"/>): the morning sky until the front arrives at <see cref="DayWeather.BreakHour"/>,
        /// two steps worse after. The smoothed storm figure of the particles is only the fallback — for the menu, the
        /// demo reel and any session that has not published a day.</summary>
        public static SkyState Sky(float storm)
            => MountainDay.Known ? MountainDay.SkyNow : StormSky(storm);

        /// <summary>The old guess, straight off the blizzard the particles are drawing.</summary>
        public static SkyState StormSky(float storm)
            => storm < .12f ? SkyState.Clear
             : storm < .45f ? SkyState.Cloud
             : storm < .75f ? SkyState.Snow
             : storm < .94f ? SkyState.Blizzard : SkyState.WhiteOut;

        public static float MetresFromSaddle(float x, float z) => Elbrus.Distance(x, z, Elbrus.Saddle.X, Elbrus.Saddle.Z);

        /// <summary>The air at the climber: the temperature of his height, the wind after the funnel, and how far he
        /// can see.</summary>
        public static MountainAir Air(float ele, float x, float z, float storm, float freshSnowCm)
        {
            float wind = BaseWindMs * (1f + HeightGainMs * Mathf.Clamp01((ele - 3000f) / 2600f));
            wind = AscentCold.WindAt(wind, ele, MetresFromSaddle(x, z));
            return new MountainAir(AscentCold.AirTempC(BaseTempC, ele), wind, AscentRoute.VisibilityM(Sky(storm)), freshSnowCm);
        }

        // ── the clock ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The hour of the day on the slope, from the run's own elapsed seconds.</summary>
        public static float Hour(float elapsed)
            => AscentRoute.HourAt(elapsed, Height1079.Core.ElbrusLocation.Plan.Profile.Seconds);

        public static float SecondsUntil(float hour, float elapsed)
            => AscentRoute.SecondsUntil(hour, elapsed, Height1079.Core.ElbrusLocation.Plan.Profile.Seconds);

        /// <summary>The hour the local client believes it is (the run clock is a NetworkVariable, so everybody agrees).</summary>
        public static float Hour() => Hour(NightSession.Instance != null ? NightSession.Instance.Elapsed.Value : 0f);

        // ── the ground under the feet ─────────────────────────────────────────────────────────────────────

        /// <summary>Standing in the snow-cat's own rolled lane — which is one of the four things that keep a party on
        /// the route when the visibility goes (<see cref="AscentRoute.CanReadTheRoute"/>).</summary>
        public static bool CatTrack(float x, float z)
        {
            var (_, off) = Elbrus.Nearest(Elbrus.RatrakRoute, x, z);
            return Mathf.Abs(off) <= AscentRoute.LaneHalfWidthM;
        }

        /// <summary>The Red Fox box on the saddle, as <see cref="ElbrusWorld"/> puts it down.</summary>
        public static Vector2 SaddleHut => new Vector2(Elbrus.Saddle.X + 25f, Elbrus.Saddle.Z + 12f);

        /// <summary>Inside a roof: a station, a barrel, a hut of the moraine, or the emergency box on the saddle.</summary>
        public static bool Sheltered(float x, float z, float visibilityM)
        {
            if (Height1079.Core.ElbrusLocation.Sheltered(x, z)) return true;
            var hut = SaddleHut;
            return Vector2.Distance(new Vector2(x, z), hut) <= 4f && AscentRoute.HutFound(0f, visibilityM);
        }

        // ── what the belt underfoot asks for ──────────────────────────────────────────────────────────────

        /// <summary>Crampons are worth putting on from the hard firn of the «зеркало» up: below the rocks a boot edge
        /// holds. This is the line the HUD nags about, not a rule — the rule is
        /// <see cref="Ascent.SlipPerMetre"/>, which charges for walking ice without them wherever it is.</summary>
        public static bool WantsCrampons(RoutePoint p)
            => p.Ele >= Ascent.IceFromEle - 60f && p.Steepness > Ascent.SpecAt(p.Ele).SlipFromDeg;

        /// <summary>The gate line for the HUD: what the mountain will not let past without, or "" when the kit is whole.</summary>
        public static string GateLine(Gear have)
        {
            var missing = Ascent.MissingList(have);
            return missing.Length == 0 ? "" : "Не хватает: " + missing;
        }
    }
}
#endif
