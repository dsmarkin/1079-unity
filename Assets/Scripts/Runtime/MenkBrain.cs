using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Server-side behaviour of the Menk, the forest giant of Mansi tales (fiction of the game).
    /// It stands among the spruces as a dead tree; wakes when someone comes close, lingers with a light on it, or when the night
    /// is deep enough; walks to the camp tent and beats it; then hunts the nearest living player. A torch beam on it makes it
    /// freeze and pretend to be a tree again — but not for long, and not every time. After a blow it withdraws into the dark,
    /// slips ahead of the players and stands still again.</summary>
    public sealed class MenkBrain
    {
        public enum State : byte { Tree, Waking, ToTent, Beating, Hunting, Striking, Frozen, Retreat }

        public struct Seen
        {
            public string Token; public ulong Client; public Vector3 Pos; public float Look; public bool Torch; public bool Dynamo; public bool Alive;
        }

        public const float WalkSpeed = 1.9f, HuntSpeed = 3.3f, Reach = 2.7f, Height = 4.3f;

        public Vector3 Pos;
        public float Yaw;
        public State Mode = State.Tree;
        /// <summary>Counts blows on the tent, so clients can shake it once per blow.</summary>
        public byte TentBlows;

        readonly System.Func<float, float, float> ground;
        readonly System.Random rnd = new System.Random(1959);
        float stateTime, litTime, lastFreeze = -100f, sleepFor, clock, freezeFor;
        int freezes, blowsLeft;
        bool swung, visitedTent;
        string target;
        State afterFreeze;
        Vector3 tentStand;

        public System.Action<string, Vector3, float> Hit;   // token, direction, damage
        public System.Action<string> Say;                    // protocol line

        public MenkBrain(System.Func<float, float, float> ground)
        {
            this.ground = ground;
            // among the spruces up the valley side from the camp tent, beside the way the group goes in the morning
            var pad = new Vector3(WorldData.CampTentPad.x, 0, WorldData.CampTentPad.z);
            var up = new Vector3(WorldData.Tent.X - pad.x, 0, WorldData.Tent.Z - pad.z).normalized;
            Pos = pad + Quaternion.Euler(0, 38f, 0) * up * 30f;
            Pos.y = ground(Pos.x, Pos.z);
            Yaw = Quaternion.LookRotation(pad - Pos).eulerAngles.y;
            var toPad = new Vector3(Pos.x - pad.x, 0, Pos.z - pad.z).normalized;
            tentStand = pad + toPad * 2.6f;
            tentStand.y = ground(tentStand.x, tentStand.z);
            sleepFor = 160f;
        }

        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        void Go(State s)
        {
            Mode = s; stateTime = 0f; swung = false;
        }

        /// <summary>One server frame. <paramref name="elapsed"/> is the night clock, <paramref name="blizzard"/> 0..1.</summary>
        public void Tick(float dt, float elapsed, float blizzard, List<Seen> players)
        {
            clock += dt;
            stateTime += dt;
            Seen? nearest = null; float nearestD = float.MaxValue;
            foreach (var p in players)
            {
                if (!p.Alive) continue;
                float d = Flat(p.Pos - Pos).magnitude;
                if (d < nearestD) { nearestD = d; nearest = p; }
            }
            bool lit = false;
            foreach (var p in players) if (p.Alive && Lights(p)) { lit = true; break; }
            litTime = lit ? litTime + dt : 0f;

            switch (Mode)
            {
                case State.Tree:
                    // someone walks right up to it, or keeps a light on it, or it has waited long enough
                    if (nearest.HasValue && elapsed > 60f && (nearestD < 14f || (litTime > 2.5f && nearestD < 22f) || (stateTime > sleepFor && nearestD < 90f)))
                    {
                        Go(State.Waking);
                        target = nearest.Value.Token;
                        Say(visitedTent ? "В темноте хрустнуло, будто дерево шагнуло." : "Одно из сухих деревьев у лагеря шевельнулось.");
                    }
                    break;

                case State.Waking:
                    if (nearest.HasValue) Turn(nearest.Value.Pos, dt, 40f);
                    if (stateTime > 3.2f) Go(!visitedTent ? State.ToTent : State.Hunting);
                    break;

                case State.ToTent:
                    if (TryFreeze(lit)) break;
                    if (Walk(tentStand, WalkSpeed * (1f - .2f * blizzard), dt) < .4f)
                    {
                        Go(State.Beating);
                        blowsLeft = 4;
                        visitedTent = true;
                        Say("Кто-то огромный бьёт по палатке.");
                    }
                    break;

                case State.Beating:
                    Turn(new Vector3(WorldData.CampTentPad.x, Pos.y, WorldData.CampTentPad.z), dt, 120f);
                    if (stateTime > .75f && !swung)
                    {
                        swung = true;
                        TentBlows++;
                        // whoever hides inside or presses against the canvas gets it through the cloth
                        foreach (var p in players)
                            if (p.Alive && Flat(p.Pos - new Vector3(WorldData.CampTentPad.x, 0, WorldData.CampTentPad.z)).magnitude < 3.4f && rnd.NextDouble() < .45)
                                Hit(p.Token, Flat(p.Pos - Pos).normalized, 30f);
                    }
                    if (stateTime > 1.5f)
                    {
                        if (--blowsLeft > 0) { stateTime = 0f; swung = false; }
                        else Go(State.Hunting);
                    }
                    break;

                case State.Hunting:
                {
                    if (TryFreeze(lit)) break;
                    var t = Find(players, target);
                    if (!t.HasValue || Flat(t.Value.Pos - Pos).magnitude > 60f) t = nearest;
                    if (!t.HasValue || Flat(t.Value.Pos - Pos).magnitude > 75f || stateTime > 50f) { Go(State.Retreat); break; }
                    target = t.Value.Token;
                    float d = Walk(t.Value.Pos, HuntSpeed * (1f - .1f * blizzard), dt, Reach * .8f);
                    if (d < Reach) Go(State.Striking);
                    break;
                }

                case State.Striking:
                {
                    var t = Find(players, target);
                    if (t.HasValue) Turn(t.Value.Pos, dt, 200f);
                    if (stateTime > .55f && !swung)
                    {
                        swung = true;
                        if (t.HasValue && Flat(t.Value.Pos - Pos).magnitude < Reach + .7f)
                        {
                            Hit(t.Value.Token, Flat(t.Value.Pos - Pos).normalized, 55f);
                        }
                    }
                    if (stateTime > 1.4f) Go(rnd.NextDouble() < .6 ? State.Retreat : State.Hunting);
                    break;
                }

                case State.Frozen:
                    // pretending: it stays a tree while the light is on it, then goes on
                    if (stateTime > (lit ? freezeFor : Mathf.Min(freezeFor, 2.2f))) Go(afterFreeze);
                    break;

                case State.Retreat:
                {
                    if (!nearest.HasValue) { Go(State.Tree); sleepFor = 60f; break; }
                    var away = Flat(Pos - nearest.Value.Pos).normalized;
                    if (away.sqrMagnitude < .01f) away = Vector3.forward;
                    Walk(Pos + away * 10f, HuntSpeed, dt);
                    bool unseen = true;
                    foreach (var p in players) if (p.Alive && (Flat(p.Pos - Pos).magnitude < 38f || Lights(p))) unseen = false;
                    if (unseen || stateTime > 25f) Ambush(players, nearest.Value);
                    break;
                }
            }
            Pos.y = ground(Pos.x, Pos.z);
        }

        /// <summary>Out of sight it slips ahead of the player, onto the way up the slope, and stands still there.</summary>
        void Ambush(List<Seen> players, Seen who)
        {
            var goal = new Vector3(WorldData.Tent.X, who.Pos.y, WorldData.Tent.Z);
            var ahead = Flat(goal - who.Pos).normalized;
            if (ahead.sqrMagnitude < .01f) ahead = Vector3.forward;
            for (int tries = 0; tries < 8; tries++)
            {
                var p = who.Pos + Quaternion.Euler(0, R(-55f, 55f), 0) * ahead * R(34f, 50f);
                bool seen = false;
                foreach (var o in players) if (o.Alive && Flat(o.Pos - p).magnitude < 26f) seen = true;
                if (seen) continue;
                Pos = p; Pos.y = ground(p.x, p.z);
                Yaw = Quaternion.LookRotation(Flat(who.Pos - p)).eulerAngles.y;
                break;
            }
            Go(State.Tree);
            sleepFor = R(70f, 130f);
            freezes = 0;
        }

        bool TryFreeze(bool lit)
        {
            if (!lit) return false;
            // the first beams freeze it; then it stops caring and comes on
            if (clock - lastFreeze < 8f) return false;
            if (freezes >= 2) { if (clock - lastFreeze > 40f) freezes = 0; else return false; }
            freezes++; lastFreeze = clock;
            afterFreeze = Mode;
            freezeFor = R(4f, 7f);
            Go(State.Frozen);
            return true;
        }

        bool Lights(Seen p)
        {
            if (!p.Torch) return false;
            var to = Flat(Pos + Vector3.up * 2.5f - p.Pos);
            float d = to.magnitude;
            if (d > (p.Dynamo ? 14f : 28f) || d < .5f) return false;
            float ang = Mathf.Abs(Mathf.DeltaAngle(p.Look, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg));
            return ang < (p.Dynamo ? 30f : 22f);
        }

        static Seen? Find(List<Seen> players, string token)
        {
            foreach (var p in players) if (p.Alive && p.Token == token) return p;
            return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        void Turn(Vector3 to, float dt, float rate)
        {
            var d = Flat(to - Pos);
            if (d.sqrMagnitude < .01f) return;
            float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Yaw = Mathf.MoveTowardsAngle(Yaw, want, rate * dt);
        }

        /// <summary>Walks toward <paramref name="to"/>, turning as it goes; returns the remaining distance.</summary>
        float Walk(Vector3 to, float speed, float dt, float stopAt = 0f)
        {
            var d = Flat(to - Pos);
            float dist = d.magnitude;
            Turn(to, dt, 110f);
            if (dist <= stopAt) return dist;
            // it only strides where it faces, so it swings round before charging
            float facing = Mathf.Abs(Mathf.DeltaAngle(Yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg));
            float k = Mathf.Clamp01(1f - facing / 90f);
            var fwd = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
            Pos += fwd * Mathf.Min(speed * k * dt, dist);
            return Flat(to - Pos).magnitude;
        }
    }
}
