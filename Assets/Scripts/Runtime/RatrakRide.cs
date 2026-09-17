using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>A snow-cat of the Garabashi drivers. It waits on the packed road by the barrels, takes a passenger up to
    /// about 5 100 m under the косая полка, stands there while people get out, and grinds back down to its place.</summary>
    public sealed class RatrakRide : MonoBehaviour
    {
        /// <summary>Climbing speed, m/s (a loaded snow-cat does 12–18 km/h on this slope).</summary>
        public const float UpSpeed = 4.2f, DownSpeed = 5.6f;
        public const float TurnaroundSeconds = 25f;

        HeightField dem;
        float s;            // arc length along Elbrus.RatrakRoute
        float home;         // where this machine waits between runs
        float length;
        int dir;            // 0 standing, +1 up, -1 down
        float wait;
        float side;         // lateral offset from the road centre, so three machines stand side by side
        public Transform Seat { get; private set; }
        public bool Standing => dir == 0;
        /// <summary>Metres above sea level right now — the HUD shows it while riding.</summary>
        public float Altitude => transform.position.y;

        public void Setup(HeightField heights, float startS, float offset)
        {
            dem = heights;
            length = Elbrus.Length(Elbrus.RatrakRoute);
            home = s = Mathf.Clamp(startS, 0f, length);
            side = offset;
            Seat = transform.Find("Seat");
            if (Seat == null) Seat = transform;
            Park();
        }

        /// <summary>Called when a passenger is aboard and wants to go up.</summary>
        public void Go()
        {
            if (dir == 0 && s < length - 1f) { dir = 1; wait = 0f; }
        }

        void Update()
        {
            if (dem == null) return;
            float dt = Time.deltaTime;
            if (dir == 0)
            {
                if (wait > 0f) { wait -= dt; }
                else if (s > home + 1f) dir = -1;              // turnaround at the top is over, head home
            }
            else
            {
                s = Mathf.Clamp(s + dir * (dir > 0 ? UpSpeed : DownSpeed) * dt, home, length);
                if (dir > 0 && s >= length) { dir = 0; wait = TurnaroundSeconds; }
                if (dir < 0 && s <= home) { dir = 0; wait = 0f; }
            }
            Park();
        }

        /// <summary>Sits the machine on the road at its current arc-length, tilted to the slope.</summary>
        void Park()
        {
            var (x, z) = Elbrus.PointAt(Elbrus.RatrakRoute, s);
            var ahead = Elbrus.PointAt(Elbrus.RatrakRoute, Mathf.Min(length, s + 8f));
            var back = Elbrus.PointAt(Elbrus.RatrakRoute, Mathf.Max(0f, s - 8f));
            var fwd = new Vector3(ahead.x - back.x, dem.Sample(ahead.x, ahead.z) - dem.Sample(back.x, back.z), ahead.z - back.z);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            x += right.x * side; z += right.z * side;
            var (dx, dz, slope) = dem.Fall(x, z, 12f);
            var normal = (Vector3.up * Mathf.Cos(slope * Mathf.Deg2Rad) - new Vector3(dx, 0, dz).normalized * Mathf.Sin(slope * Mathf.Deg2Rad)).normalized;
            transform.SetPositionAndRotation(new Vector3(x, dem.Sample(x, z) + .35f, z), Quaternion.LookRotation(fwd, normal));
        }
    }
}
