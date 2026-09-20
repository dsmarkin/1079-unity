#if !HEIGHT1079_NO_ELBRUS
using Unity.Netcode;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Everything about a hiker that belongs to the southern slope of Elbrus and to no other map: the two
    /// network variables the mountain writes, the gear under the boots, and the debug key that walks the summit route.
    ///
    /// This is a partial of <see cref="HikerController"/> and therefore has to stay in <c>Height1079.Runtime</c> — a
    /// partial class cannot be split across assemblies. What makes it optional is the <c>#if</c> above: with
    /// HEIGHT1079_NO_ELBRUS the file is not compiled, the <c>partial void</c> hooks declared in the shared file have no
    /// implementation, and every call to them disappears at compile time (docs/ELBRUS.md).
    ///
    /// <b>The set of <see cref="NetworkVariable{T}"/>s on the player object differs between the two builds</b>, which
    /// is why a build with the location and a build without it cannot play together.</summary>
    public sealed partial class HikerController
    {
        /// <summary>Everything the mountain is doing to this one on the southern slope of Elbrus: the frostbite pools,
        /// the sickness, the heart, the kit in the rucksack and what the rules make of it. The host writes it — it is
        /// the only one that runs <see cref="Ascent.Tick"/> — and both the legs and the HUD read it
        /// (<see cref="ClimbGear"/>).</summary>
        public readonly NetworkVariable<ClimbNet> Climb = new NetworkVariable<ClimbNet>();

        /// <summary>How far along the guide's programme this one is (<see cref="Programme"/>). The host ticks the
        /// steps — it is the only side that can see every condition — and publishes the answer here for the card, the
        /// compass and the two maps (<see cref="Programmes"/>).</summary>
        public readonly NetworkVariable<PlanNet> Plan = new NetworkVariable<PlanNet>();

        ClimbGear climb;

        /// <summary>The mountain under the boots on the southern slope; silent on every other map.</summary>
        public ClimbGear Climbing => climb;

        /// <summary>True only where the mountain has a say — the component exists on every player object, the map
        /// decides whether it is listened to.</summary>
        bool OnTheMountain => climb != null && Height1079.Core.World.IsElbrus;

        partial void ClimbAwake()
        {
            climb = GetComponent<ClimbGear>();
            if (climb == null) climb = gameObject.AddComponent<ClimbGear>();
        }

        partial void MapInput()
        {
            climb.HandleInput();
            Camps.HandleInput(this);
            Programmes.HandleInput(this);
        }

        partial void ClimbRide()
        {
            if (OnTheMountain) climb.Sample(Vector3.zero);
        }

        partial void ClimbLocked(ref bool locked)
        {
            if (OnTheMountain && (climb.Busy || climb.Sliding)) locked = true;
        }

        partial void ClimbWish(ref Vector3 wish)
        {
            if (!OnTheMountain) return;
            climb.Sample(wish);
            wish = climb.Allow(wish);
        }

        partial void ClimbMayRun(ref bool mayRun)
        {
            if (OnTheMountain && !climb.MayRun) mayRun = false;
        }

        partial void ClimbSpeed(ref float factor)
        {
            if (OnTheMountain) factor = climb.SpeedFactor;
        }

        /// <summary>The feet went and the axe did not hold: nothing the player does matters until the run-out is spent.
        /// On the косая полка that is three to six hundred metres down the line of the water (Ascent.RunoutM).</summary>
        partial void ClimbSlide(ref bool sliding)
        {
            if (!OnTheMountain || !climb.Sliding) return;
            sliding = true;
            var fall = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
            body.AddForce(fall * ClimbGear.SlidePull, ForceMode.Acceleration);
            var run = body.linearVelocity;
            var flat = new Vector2(run.x, run.z);
            if (flat.magnitude > ClimbGear.SlideTopMs)
            {
                flat = flat.normalized * ClimbGear.SlideTopMs;
                body.linearVelocity = new Vector3(flat.x, run.y, flat.y);
            }
            climb.Slid(flat.magnitude * Time.fixedDeltaTime);
            if (!climb.Sliding) stumbleUntil = Time.time + 1.6f;
        }

        partial void ClimbDrift(Vector3 forward)
        {
            if (!OnTheMountain || climb.Sliding) return;
            var push = climb.Drift(forward);
            if (push.sqrMagnitude <= 1e-4f) return;
            var now = body.linearVelocity;
            body.linearVelocity = new Vector3(now.x + push.x, now.y, now.z + push.z);
        }

        partial void ClimbBoard() => climb?.StopSlide();

        /// <summary>The nine stages of the summit route, for jumping between them with F4. The climb takes eight hours
        /// of play from the bottom, so testing what the saddle looks like by walking to it is not testing. Off the
        /// mountain the key does nothing.</summary>
        static readonly (string Name, float S)[] RouteStops =
        {
            ("Гара-Баши, 3847", 0f), ("Приют 11, 4050", 1096f), ("Скалы Пастухова, 4650", 3035f),
            ("Выход на 5100", 4031f), ("Косая полка, 5290", 4511f), ("Седловина, 5382", 5451f),
            ("Вершинный взлёт, 5450", 5806f),
            // eighty metres short of the top: standing on the summit itself ends the run the moment you land, and the
            // point of the key is to look around up there, not to win
            ("Вершинное плато, 5630", 6600f), ("Поляна Азау, 2350", -1f),
        };
        int routeStop = -1;

        /// <summary>Owner only: step to the next stage of the route and stand there.</summary>
        partial void JumpAlongRoute()
        {
            if (!IsOwner || !Height1079.Core.World.IsElbrus || Bootstrap.Dem == null) return;
            routeStop = (routeStop + 1) % RouteStops.Length;
            var stop = RouteStops[routeStop];
            float x, z;
            if (stop.S < 0f) { x = Elbrus.Start.x; z = Elbrus.Start.z; }
            else { var p = Elbrus.PointAt(Elbrus.SummitRoute, stop.S); x = p.x; z = p.z; }
            if (Ride != null) LeaveRide(new Vector3(x, Bootstrap.Dem.Sample(x, z) + .1f, z));
            else Place(new Vector3(x, Bootstrap.Dem.Sample(x, z) + .6f, z));
            stumbleUntil = 0f; tripUntil = 0f; impact = 0f; stuckFor = 0f; stuckAt = transform.position;
            // and arrive able to walk: kitted out, crampons on, acclimatised (NightSession.DebugOutfit)
            NightSession.Instance?.DebugOutfit();
            Bootstrap.Hud?.SetStatus($"F4: {stop.Name} · снаряжение выдано");
        }
    }
}
#endif
