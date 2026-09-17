using System;

namespace Height1079.Core
{
    public enum RopewayKind
    {
        /// <summary>Jig-back (маятниковая): two cars on one rope, counterweighting each other, standing at the terminals.</summary>
        Pendulum,
        /// <summary>Detachable gondola loop: cabins every <see cref="RopewaySpec.Spacing"/> metres, slowed to walking pace in the stations.</summary>
        Gondola,
        /// <summary>Fixed-grip chair: the same loop, but the chairs never stop and the seats are open.</summary>
        Chair,
    }

    /// <summary>What a ropeway is, before the terrain is known: its towers, how fast it runs and how it carries people.</summary>
    public sealed class RopewaySpec
    {
        public readonly string Id, Name, BottomId, TopId;
        public readonly RopewayKind Kind;
        /// <summary>Line speed, m/s (operator's figure).</summary>
        public readonly float Speed;
        /// <summary>Seats in one car.</summary>
        public readonly int Occupancy;
        /// <summary>Metres between cars on the loop; 0 for a jig-back.</summary>
        public readonly float Spacing;
        /// <summary>Seconds a jig-back car stands at a terminal; 0 for a loop.</summary>
        public readonly float Dwell;
        public readonly (float x, float z)[] Towers;

        public RopewaySpec(string id, string name, RopewayKind kind, string bottomId, string topId,
            float speed, int occupancy, float spacing, float dwell, (float x, float z)[] towers)
        {
            Id = id; Name = name; Kind = kind; BottomId = bottomId; TopId = topId;
            Speed = speed; Occupancy = occupancy; Spacing = spacing; Dwell = dwell; Towers = towers;
        }
    }

    /// <summary>A ropeway placed on the ground: tower tops, the sagging haul rope and where every car is at time t.
    /// No engine types — the runtime only reads positions from here, so the whole mechanism is unit-tested in dotnet.</summary>
    public sealed class Ropeway
    {
        public readonly RopewaySpec Spec;
        /// <summary>Ground height and sheave height of every tower (index matches <see cref="RopewaySpec.Towers"/>).</summary>
        public readonly float[] Ground, TowerHeight;
        /// <summary>Rope polyline, sagged: 12 samples per span. x, y, z in world metres.</summary>
        public readonly (float x, float y, float z)[] Rope;
        readonly float[] cum;          // arc length at each rope sample
        /// <summary>Length of one carry (bottom terminal → top terminal), metres.</summary>
        public readonly float Length;
        /// <summary>Time for one full loop (up and back) or one full jig-back cycle, seconds.</summary>
        public readonly float CycleSeconds;
        public readonly int Cars;

        /// <summary>Metres of clearance the rope keeps over the ground under it.</summary>
        public const float Clearance = 6f;
        /// <summary>Cabins crawl through the terminals at this speed, so passengers can step in and out.</summary>
        public const float LoadSpeed = .8f;
        /// <summary>Length of the slow zone at each end of a loop, metres.</summary>
        public const float SlowZone = 45f;
        /// <summary>Lateral offset of the two strands of a loop (half the track gauge), metres.</summary>
        public const float Gauge = 3.2f;

        const int PerSpan = 12;

        readonly float[] loopTime;     // travel time from the bottom terminal to each rope sample
        readonly float upTime;         // seconds for one carry

        public Ropeway(RopewaySpec spec, Func<float, float, float> ground)
        {
            Spec = spec;
            int n = spec.Towers.Length;
            Ground = new float[n];
            TowerHeight = new float[n];
            for (int i = 0; i < n; i++) Ground[i] = ground(spec.Towers[i].x, spec.Towers[i].z);

            float baseHeight = spec.Kind == RopewayKind.Chair ? 7f : spec.Kind == RopewayKind.Pendulum ? 16f : 11f;
            float terminal = spec.Kind == RopewayKind.Chair ? 6f : spec.Kind == RopewayKind.Pendulum ? 14f : 9f;
            for (int i = 0; i < n; i++) TowerHeight[i] = i == 0 || i == n - 1 ? terminal : baseHeight;
            float clear = spec.Kind == RopewayKind.Chair ? 3.5f : Clearance;
            RaiseTowers(ground, clear);

            Rope = BuildRope();
            cum = new float[Rope.Length];
            for (int i = 1; i < Rope.Length; i++)
            {
                var a = Rope[i - 1]; var b = Rope[i];
                cum[i] = cum[i - 1] + (float)Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) + (a.z - b.z) * (a.z - b.z));
            }
            Length = cum[cum.Length - 1];

            loopTime = new float[Rope.Length];
            for (int i = 1; i < Rope.Length; i++)
                loopTime[i] = loopTime[i - 1] + (cum[i] - cum[i - 1]) / SpeedAt((cum[i] + cum[i - 1]) * .5f);
            upTime = loopTime[loopTime.Length - 1];

            if (spec.Kind == RopewayKind.Pendulum)
            {
                Cars = 2;
                CycleSeconds = 2 * (spec.Dwell + upTime);
            }
            else
            {
                Cars = Math.Max(2, (int)Math.Round(2 * Length / Math.Max(1f, spec.Spacing)));
                CycleSeconds = 2 * upTime;
            }
        }

        /// <summary>Raises towers until the sagging rope clears the ground everywhere. Terminals are never raised
        /// (their platform is the ground), so a span that cannot be cleared lifts the tower next to it instead.</summary>
        void RaiseTowers(Func<float, float, float> ground, float clear)
        {
            int n = Spec.Towers.Length;
            for (int pass = 0; pass < 12; pass++)
            {
                float worst = 0;
                for (int i = 1; i < n; i++)
                {
                    var a = Spec.Towers[i - 1]; var b = Spec.Towers[i];
                    float ya = Ground[i - 1] + TowerHeight[i - 1], yb = Ground[i] + TowerHeight[i];
                    float span = Elbrus.Distance(a.x, a.z, b.x, b.z);
                    float sag = Math.Min(.022f * span, 9f);
                    float need = 0;
                    for (int k = 1; k < PerSpan; k++)
                    {
                        float t = (float)k / PerSpan;
                        float x = a.x + (b.x - a.x) * t, z = a.z + (b.z - a.z) * t;
                        float y = ya + (yb - ya) * t - 4 * sag * t * (1 - t);
                        need = Math.Max(need, ground(x, z) + clear - y);
                    }
                    if (need <= 0) continue;
                    worst = Math.Max(worst, need);
                    // lift the inner tower(s) of this span; a terminal keeps its height
                    bool aFixed = i - 1 == 0, bFixed = i == n - 1;
                    if (aFixed && bFixed) continue;
                    if (!aFixed) TowerHeight[i - 1] = Math.Min(48f, TowerHeight[i - 1] + need * (bFixed ? 1f : .6f));
                    if (!bFixed) TowerHeight[i] = Math.Min(48f, TowerHeight[i] + need * (aFixed ? 1f : .6f));
                }
                if (worst < .05f) break;
            }
        }

        (float x, float y, float z)[] BuildRope()
        {
            int n = Spec.Towers.Length;
            var pts = new (float x, float y, float z)[(n - 1) * PerSpan + 1];
            int w = 0;
            for (int i = 1; i < n; i++)
            {
                var a = Spec.Towers[i - 1]; var b = Spec.Towers[i];
                float ya = Ground[i - 1] + TowerHeight[i - 1], yb = Ground[i] + TowerHeight[i];
                float span = Elbrus.Distance(a.x, a.z, b.x, b.z);
                float sag = Math.Min(.022f * span, 9f);
                int k0 = i == 1 ? 0 : 1;
                for (int k = k0; k <= PerSpan; k++)
                {
                    float t = (float)k / PerSpan;
                    pts[w++] = (a.x + (b.x - a.x) * t, ya + (yb - ya) * t - 4 * sag * t * (1 - t), a.z + (b.z - a.z) * t);
                }
            }
            return pts;
        }

        /// <summary>Line speed at arc-length s: the full speed in the open, walking pace inside the terminals of a loop.</summary>
        public float SpeedAt(float s)
        {
            if (Spec.Kind == RopewayKind.Pendulum) return Spec.Speed;
            float edge = Math.Min(s, Length - s);
            if (edge >= SlowZone) return Spec.Speed;
            float t = Math.Max(0f, edge) / SlowZone;
            return LoadSpeed + (Spec.Speed - LoadSpeed) * t * t * (3 - 2 * t);
        }

        /// <summary>Position on the rope at arc-length s (clamped), before the lateral offset of the strand.</summary>
        public (float x, float y, float z) PointAt(float s)
        {
            if (s <= 0) return Rope[0];
            if (s >= Length) return Rope[Rope.Length - 1];
            int lo = 0, hi = cum.Length - 1;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (cum[mid] <= s) lo = mid; else hi = mid; }
            float t = (s - cum[lo]) / Math.Max(1e-4f, cum[hi] - cum[lo]);
            var a = Rope[lo]; var b = Rope[hi];
            return (a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }

        /// <summary>Unit direction of travel (uphill) at arc-length s.</summary>
        public (float x, float y, float z) DirectionAt(float s)
        {
            var a = PointAt(Math.Max(0, s - 3f));
            var b = PointAt(Math.Min(Length, s + 3f));
            float dx = b.x - a.x, dy = b.y - a.y, dz = b.z - a.z;
            float m = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return m < 1e-5f ? (0, 0, 1) : (dx / m, dy / m, dz / m);
        }

        /// <summary>Arc-length reached after travelling for <paramref name="seconds"/> from the bottom terminal.</summary>
        public float DistanceAfter(float seconds)
        {
            if (seconds <= 0) return 0;
            if (seconds >= upTime) return Length;
            int lo = 0, hi = loopTime.Length - 1;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (loopTime[mid] <= seconds) lo = mid; else hi = mid; }
            float t = (seconds - loopTime[lo]) / Math.Max(1e-5f, loopTime[hi] - loopTime[lo]);
            return cum[lo] + (cum[hi] - cum[lo]) * t;
        }

        public readonly struct Car
        {
            /// <summary>Arc-length from the bottom terminal.</summary>
            public readonly float S;
            /// <summary>true while the car is on the ascending strand.</summary>
            public readonly bool Up;
            /// <summary>Current speed, m/s. A jig-back car standing in a terminal reads 0.</summary>
            public readonly float Speed;
            public Car(float s, bool up, float speed) { S = s; Up = up; Speed = speed; }
            /// <summary>The car can be boarded and left while it barely moves.</summary>
            public bool Boardable => Speed <= LoadSpeed + .05f;
        }

        /// <summary>Where car <paramref name="index"/> is at time <paramref name="t"/> (seconds, any monotonic clock).</summary>
        public Car CarAt(int index, double t)
        {
            if (Spec.Kind == RopewayKind.Pendulum)
            {
                double cycle = CycleSeconds;
                double u = ((t % cycle) + cycle) % cycle;
                float s; float v;
                if (u < Spec.Dwell) { s = 0; v = 0; }
                else if (u < Spec.Dwell + upTime) { s = DistanceAfter((float)(u - Spec.Dwell)); v = SpeedAt(s); }
                else if (u < 2 * Spec.Dwell + upTime) { s = Length; v = 0; }
                else { s = Length - DistanceAfter((float)(u - 2 * Spec.Dwell - upTime)); v = SpeedAt(s); }
                // car 0 goes up while car 1 comes down: they are the two ends of the same rope
                bool up = u >= Spec.Dwell && u < Spec.Dwell + upTime;
                if (index % 2 == 0) return new Car(s, up, v);
                return new Car(Length - s, !up, v);
            }
            else
            {
                double loop = CycleSeconds;
                double u = ((t + (double)index * loop / Cars) % loop + loop) % loop;
                if (u < upTime)
                {
                    float s = DistanceAfter((float)u);
                    return new Car(s, true, SpeedAt(s));
                }
                else
                {
                    float s = Length - DistanceAfter((float)(u - upTime));
                    return new Car(s, false, SpeedAt(s));
                }
            }
        }

        /// <summary>Index of the car a passenger standing at the given terminal can step into now, or -1.</summary>
        public int BoardableCar(double t, bool atBottom)
        {
            for (int i = 0; i < Cars; i++)
            {
                var c = CarAt(i, t);
                if (!c.Boardable) continue;
                bool here = atBottom ? c.S < SlowZone : c.S > Length - SlowZone;
                if (!here) continue;
                // only step into a car that is about to leave in the direction we want
                if (Spec.Kind != RopewayKind.Pendulum && c.Up != atBottom) continue;
                return i;
            }
            return -1;
        }
    }
}
