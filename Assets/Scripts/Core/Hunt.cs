using System;
using System.Collections.Generic;

namespace Height1079.Core
{
    /// <summary>The numbers of the sandbox loop "take — hide — carry" (docs/SANDBOX.md): a flat night yard, a fire at
    /// one end, a tent at the other, one Menk walking a ring round the tent, and a blizzard that walls the yard off at
    /// the end of the run. Everything here is metres and seconds; nothing here knows the engine, so the same rules run
    /// in dotnet, in the sandbox and — later — on the host of a real night.</summary>
    public static class HuntRules
    {
        /// <summary>The yard: a square of snow this wide, the fire at one edge and the tent at the other.</summary>
        public const float YardSize = 60f;
        /// <summary>Put a thing down this close to the fire and it counts as brought home.</summary>
        public const float FireRadius = 4f;

        // ── the Menk's three senses ──
        /// <summary>A lit torch is seen from this far, and the Menk walks to it.</summary>
        public const float LightRange = 40f;
        /// <summary>A running player is heard from this far, a thing hitting the snow from this far, a walking step from this far.</summary>
        public const float RunNoise = 20f, DropNoise = 30f, StepNoise = 6f;
        /// <summary>Having reached a sound or a light and found nothing, it looks round for this long.</summary>
        public const float ListenSeconds = 5f;
        /// <summary>A mark in the snow is worth following for this long — the wind takes it after; in a blizzard much sooner.</summary>
        public const float TrackLife = 60f, TrackLifeStorm = 15f;
        /// <summary>How far it will go back along a chain of marks looking for the freshest one.</summary>
        public const float TrackSearch = 30f;

        // ── hiding ──
        /// <summary>Behind cover, or lying flat without a light, a player is not seen beyond this.</summary>
        public const float HiddenRange = 3f;
        /// <summary>Crouching or lying in the open, no light.</summary>
        public const float LowOpenRange = 8f;
        /// <summary>Standing without a light: seen from this far when facing the Menk, and from this far from behind.</summary>
        public const float SeenFaceRange = 12f, SeenBackRange = 6f;
        /// <summary>A body with a lit torch is a body, not a light, from this close: it is chased, not walked to.</summary>
        public const float LitBodyRange = 18f;

        // ── the walk ──
        public const float PatrolSpeed = 1.9f, HurrySpeed = 2.5f, ChaseSpeed = 3.3f;
        /// <summary>Within this it strikes; the blow lands a little further than that because it leans in.</summary>
        public const float Reach = 2.7f, ReachSlack = .7f;
        /// <summary>The ring it walks round the tent when nothing is calling it.</summary>
        public const float PatrolRadius = 14f;
        /// <summary>Every so often on the ring it stops and listens.</summary>
        public const float PauseEvery = 20f, PauseEveryMax = 40f, PauseFor = 4f;
        /// <summary>Losing sight of a player it goes on for this long to where it last saw him before falling back on the marks.</summary>
        public const float LoseAfter = 2.5f;
        /// <summary>Wind-up before the blow lands, and the whole swing.</summary>
        public const float StrikeAt = .55f, StrikeFor = 1.4f;

        // ── the track: two sections and a finish ──
        /// <summary>Things home before the night may be called done (Burglin' Gnomes: three tasks, whatever else).</summary>
        public const int Quota = 3;
        /// <summary>With the quota met, everyone inside the fire's ring for this long ends the night as "вернулись".</summary>
        public const float FinishHold = 5f;
        /// <summary>The second section (the labaz) joins the Menk's beat once this many things from the tent are home
        /// (R.E.P.O.: the next extraction point appears after the previous one has been used).</summary>
        public const int OpensLabaz = 1;
        /// <summary>Degrees of a ring it walks before crossing to its other post.</summary>
        public const float LapDegrees = 400f;

        // ── the blizzard wall ──
        public const float RunSeconds = 15f * 60f;
        /// <summary>The wind starts rising here; at the end of the run it is a wall.</summary>
        public const float StormFrom = 10f * 60f;
        /// <summary>Outside the fire in the wall a player is dead in this many seconds.</summary>
        public const float FreezeSeconds = 30f;
        /// <summary>Metres one can see: a night without a torch, a torch beam, and the wall.</summary>
        public const float VisibilityNight = 12f, VisibilityTorch = 25f, VisibilityStorm = 3f;

        /// <summary>0 … 1: how far the blizzard has come at second <paramref name="t"/> of a run <paramref name="length"/>
        /// seconds long. Nothing until two thirds of the run, then a rise that reaches the wall at the end. The share
        /// is kept, not the minutes, so a three-minute test run has the same shape as the fifteen-minute one.</summary>
        public static float Storm(float t, float length = RunSeconds)
        {
            if (length <= 0f) return 1f;
            float from = length * (StormFrom / RunSeconds);
            return Clamp01((t - from) / Math.Max(length - from, 1e-3f));
        }

        /// <summary>The wind is a wall: nothing is seen, and nobody lives outside the fire.</summary>
        public static bool IsWall(float storm) => storm >= .999f;

        /// <summary>Metres a player sees without a torch at this much storm.</summary>
        public static float Visibility(float storm) => VisibilityNight + (VisibilityStorm - VisibilityNight) * storm * storm;

        /// <summary>Seconds a mark in the snow lasts at this much storm.</summary>
        public static float TrackLifeAt(float storm) => TrackLife + (TrackLifeStorm - TrackLife) * Clamp01(storm);

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>Degrees between two headings, −180 … 180.</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = (b - a) % 360f;
            if (d > 180f) d -= 360f; else if (d < -180f) d += 360f;
            return d;
        }

        /// <summary>Heading from one point to another, degrees clockwise from north (+z), the way Unity's yaw goes.</summary>
        public static float Heading(float fromX, float fromZ, float toX, float toZ)
            => (float)(Math.Atan2(toX - fromX, toZ - fromZ) * 180.0 / Math.PI);

        public static float Dist(float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public static string Clock(float seconds)
        {
            int s = Math.Max(0, (int)seconds);
            return $"{s / 60}:{s % 60:00}";
        }
    }

    /// <summary>What the Menk is told about a player each tick. The engine fills it: where the body is, which way it
    /// faces, whether the torch is lit, whether it is running, whether it is down in the snow, and whether something
    /// solid stands between the Menk's eyes and the body (<see cref="Covered"/> — a rock, a spruce, a drift).</summary>
    public struct HuntSeen
    {
        public string Name;
        public float X, Z;
        /// <summary>Degrees clockwise from north, where the player faces.</summary>
        public float LookYaw;
        public bool Torch, Running, Walking, Low, Covered, Alive;
    }

    /// <summary>The marks players leave in the snow, as the Menk reads them: a chain of points per player, each with
    /// the second it was made. The wind takes old marks (<see cref="Fade"/>); what is left is followed from the
    /// freshest mark onward along its own chain. The pictures of the prints are somebody else's business.</summary>
    public sealed class TrackChain
    {
        public struct Mark { public float X, Z, Time; public int Chain; }

        readonly List<Mark> marks = new List<Mark>();
        public int Count => marks.Count;
        public Mark this[int i] => marks[i];

        public void Leave(int chain, float x, float z, float time) => marks.Add(new Mark { X = x, Z = z, Time = time, Chain = chain });

        /// <summary>Marks older than <paramref name="life"/> seconds are gone.</summary>
        public void Fade(float now, float life)
        {
            float cutoff = now - life;
            int keep = 0;
            for (int i = 0; i < marks.Count; i++) if (marks[i].Time >= cutoff) marks[keep++] = marks[i];
            if (keep < marks.Count) marks.RemoveRange(keep, marks.Count - keep);
        }

        public void Clear() => marks.Clear();

        /// <summary>The mark of this chain made at this second, or −1 once the wind has taken it.</summary>
        public int IndexOf(int chain, float time)
        {
            if (chain < 0) return -1;
            for (int i = 0; i < marks.Count; i++) if (marks[i].Chain == chain && marks[i].Time == time) return i;
            return -1;
        }

        /// <summary>The freshest mark within <paramref name="radius"/> of a point, or −1.</summary>
        public int Freshest(float x, float z, float radius)
        {
            int best = -1; float bestTime = float.NegativeInfinity;
            for (int i = 0; i < marks.Count; i++)
            {
                var m = marks[i];
                if (HuntRules.Dist(x, z, m.X, m.Z) > radius) continue;
                if (m.Time > bestTime) { bestTime = m.Time; best = i; }
            }
            return best;
        }

        /// <summary>The next mark along the same chain, newer than this one, or −1 at the end of the chain.</summary>
        public int Next(int index)
        {
            if (index < 0 || index >= marks.Count) return -1;
            var m = marks[index];
            int best = -1; float bestTime = float.PositiveInfinity;
            for (int i = 0; i < marks.Count; i++)
            {
                if (i == index || marks[i].Chain != m.Chain || marks[i].Time <= m.Time) continue;
                if (marks[i].Time < bestTime) { bestTime = marks[i].Time; best = i; }
            }
            return best;
        }
    }

    /// <summary>The Menk of the yard. Three senses and nothing else: light calls it, noise calls it, marks in the
    /// snow lead it. When nothing calls, it walks a ring round the tent and stops now and then to listen. It does not
    /// freeze in a beam the way the night's Menk does — the opposite: the beam is what brings it.</summary>
    public sealed class HunterBrain
    {
        public enum State : byte { Patrol, Listen, ToLight, ToNoise, Tracking, Chase, Strike, LookAround, Walk }
        public enum Cue : byte { None, Light, Noise, Tracks, Sight }

        public float X, Z, Yaw;
        public State Mode { get; private set; } = State.Patrol;
        /// <summary>What it is acting on right now.</summary>
        public Cue Why { get; private set; }
        /// <summary>Seconds in the current state.</summary>
        public float StateTime { get; private set; }
        /// <summary>The player it is after, while it is after one.</summary>
        public string Target { get; private set; }
        /// <summary>Where it is going, for whoever draws it.</summary>
        public float GoalX { get; private set; }
        public float GoalZ { get; private set; }

        /// <summary>The metrics the brief asks for: how often each sense sent it somewhere.</summary>
        public int WentToLight, WentToNoise, WentByTracks;

        /// <summary>The blow landed on this player.</summary>
        public Action<string> Hit;
        /// <summary>A line for the protocol.</summary>
        public Action<string> Say;

        /// <summary>The places it circles: the tent, and the labaz once that section is open. It walks one ring
        /// for <see cref="HuntRules.LapDegrees"/>, then crosses to the next post and circles there.</summary>
        readonly List<(float x, float z)> posts = new List<(float x, float z)>();
        int post;
        float lapDeg;
        readonly TrackChain tracks;
        readonly Random rnd;
        float pauseIn, patrolAngle, lostFor;
        /// <summary>The mark being walked to, by identity: the chain compacts when the wind takes old marks, so an
        /// index would point at somebody else's step.</summary>
        int trackChain = -1; float trackTime;
        bool swung;
        string noiseFrom;

        public HunterBrain(float tentX, float tentZ, TrackChain tracks, int seed = 1959)
        {
            posts.Add((tentX, tentZ)); this.tracks = tracks;
            rnd = new Random(seed);
            patrolAngle = (float)(rnd.NextDouble() * 360.0);
            X = tentX + (float)Math.Sin(patrolAngle * Math.PI / 180.0) * HuntRules.PatrolRadius;
            Z = tentZ + (float)Math.Cos(patrolAngle * Math.PI / 180.0) * HuntRules.PatrolRadius;
            PostX = tentX; PostZ = tentZ;
            Yaw = patrolAngle + 90f;
            pauseIn = R(HuntRules.PauseEvery, HuntRules.PauseEveryMax);
            GoalX = X; GoalZ = Z;
        }

        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        /// <summary>The post it is circling now.</summary>
        public float PostX { get; private set; }
        public float PostZ { get; private set; }
        public int Posts => posts.Count;

        /// <summary>Another place to walk round: the beat now runs between them.</summary>
        public void AddPost(float x, float z)
        {
            foreach (var p in posts) if (HuntRules.Dist(p.x, p.z, x, z) < 1f) return;
            posts.Add((x, z));
        }

        /// <summary>Something hit the snow: a dropped stove, a thrown camera. Heard within <paramref name="radius"/>.</summary>
        public void Hear(float x, float z, float radius, string who = null)
        {
            if (HuntRules.Dist(X, Z, x, z) > radius) return;
            if (Mode == State.Chase || Mode == State.Strike || Mode == State.ToLight) return;
            Go(State.ToNoise, Cue.Noise, x, z, who);
        }

        void Go(State s, Cue why, float gx, float gz, string who = null)
        {
            if (s == State.ToLight && Mode != State.ToLight) WentToLight++;
            if (s == State.ToNoise && Mode != State.ToNoise) WentToNoise++;
            if (s == State.Tracking && Mode != State.Tracking) WentByTracks++;
            Mode = s; Why = why; StateTime = 0f; swung = false;
            GoalX = gx; GoalZ = gz;
            noiseFrom = who;
        }

        /// <summary>One tick. <paramref name="storm"/> 0..1 slows it a little and shortens the life of the marks —
        /// the caller fades the chain, this only reads it.</summary>
        public void Tick(float dt, IList<HuntSeen> players, float storm = 0f)
        {
            StateTime += dt;
            float slow = 1f - .2f * HuntRules.Clamp01(storm);

            // ── senses ──
            int seen = -1, lit = -1, heard = -1; float seenD = float.MaxValue, litD = float.MaxValue, heardD = float.MaxValue;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (!p.Alive) continue;
                float d = HuntRules.Dist(X, Z, p.X, p.Z);
                if (Sees(p, d) && d < seenD) { seenD = d; seen = i; }
                if (p.Torch && d < HuntRules.LightRange && d < litD) { litD = d; lit = i; }
                float hear = p.Running ? HuntRules.RunNoise : p.Walking ? HuntRules.StepNoise : 0f;
                if (hear > 0f && d < hear && d < heardD) { heardD = d; heard = i; }
            }

            bool busy = Mode == State.Strike;
            if (!busy)
            {
                if (seen >= 0)
                {
                    var p = players[seen];
                    if (Mode != State.Chase) { Go(State.Chase, Cue.Sight, p.X, p.Z); Say?.Invoke("Менк увидел " + p.Name + " и пошёл на него."); }
                    Target = p.Name; GoalX = p.X; GoalZ = p.Z; lostFor = 0f;
                }
                else if (lit >= 0 && Mode != State.Chase)
                {
                    var p = players[lit];
                    if (Mode != State.ToLight) { Go(State.ToLight, Cue.Light, p.X, p.Z, p.Name); Say?.Invoke("Менк заметил свет фонаря."); }
                    else { GoalX = p.X; GoalZ = p.Z; }
                }
                else if (heard >= 0 && Mode != State.Chase && Mode != State.ToLight)
                {
                    var p = players[heard];
                    if (Mode != State.ToNoise) { Go(State.ToNoise, Cue.Noise, p.X, p.Z, p.Name); Say?.Invoke(p.Running ? "Менк услышал бег." : "Менк услышал шаги."); }
                    else { GoalX = p.X; GoalZ = p.Z; }
                }
            }

            switch (Mode)
            {
                case State.Patrol:
                {
                    pauseIn -= dt;
                    if (pauseIn <= 0f) { Go(State.Listen, Cue.None, X, Z); break; }
                    // a ring walked, and there is another post: cross to it
                    if (posts.Count > 1 && lapDeg >= HuntRules.LapDegrees)
                    {
                        post = (post + 1) % posts.Count;
                        PostX = posts[post].x; PostZ = posts[post].z;
                        lapDeg = 0f;
                        // aim for the near side of the next ring
                        float toward = HuntRules.Heading(PostX, PostZ, X, Z) * (float)Math.PI / 180f;
                        Go(State.Walk, Cue.None, PostX + (float)Math.Sin(toward) * HuntRules.PatrolRadius, PostZ + (float)Math.Cos(toward) * HuntRules.PatrolRadius);
                        break;
                    }
                    // round the ring: the goal runs a few metres ahead of it along the circle
                    float step = HuntRules.PatrolSpeed * slow * dt / HuntRules.PatrolRadius * 180f / (float)Math.PI;
                    patrolAngle += step; lapDeg += step;
                    float a = (patrolAngle + 12f) * (float)Math.PI / 180f;
                    GoalX = PostX + (float)Math.Sin(a) * HuntRules.PatrolRadius;
                    GoalZ = PostZ + (float)Math.Cos(a) * HuntRules.PatrolRadius;
                    Walk(GoalX, GoalZ, HuntRules.PatrolSpeed * slow, dt);
                    break;
                }
                case State.Walk:
                {
                    float left = Walk(GoalX, GoalZ, HuntRules.PatrolSpeed * slow, dt);
                    if (left < 2f)
                    {
                        patrolAngle = HuntRules.Heading(PostX, PostZ, X, Z);
                        pauseIn = R(HuntRules.PauseEvery, HuntRules.PauseEveryMax);
                        Go(State.Patrol, Cue.None, X, Z);
                    }
                    break;
                }
                case State.Listen:
                    Turn(Yaw + 40f * dt, dt, 40f);
                    if (StateTime > HuntRules.PauseFor) { pauseIn = R(HuntRules.PauseEvery, HuntRules.PauseEveryMax); Go(State.Patrol, Cue.None, X, Z); }
                    break;

                case State.ToLight:
                case State.ToNoise:
                {
                    float left = Walk(GoalX, GoalZ, HuntRules.HurrySpeed * slow, dt);
                    bool stillLit = Mode == State.ToLight && lit >= 0;
                    if (left < 2f || (Mode == State.ToLight && !stillLit && StateTime > 1f && left < 6f))
                        Go(State.LookAround, Why, X, Z);
                    break;
                }
                case State.LookAround:
                    Turn(Yaw + 70f * dt, dt, 70f);
                    if (StateTime > HuntRules.ListenSeconds)
                    {
                        if (!FollowTracks()) BackToRing();
                    }
                    break;

                case State.Tracking:
                {
                    int at = tracks.IndexOf(trackChain, trackTime);
                    if (at < 0) at = tracks.Freshest(X, Z, HuntRules.TrackSearch);
                    if (at < 0) { trackChain = -1; Go(State.LookAround, Cue.None, X, Z); break; }
                    var m = tracks[at];
                    trackChain = m.Chain; trackTime = m.Time;
                    GoalX = m.X; GoalZ = m.Z;
                    float left = Walk(m.X, m.Z, HuntRules.HurrySpeed * slow, dt);
                    if (left < 1.5f)
                    {
                        int next = tracks.Next(at);
                        if (next < 0) { trackChain = -1; Go(State.LookAround, Cue.Tracks, X, Z); }
                        else { trackChain = tracks[next].Chain; trackTime = tracks[next].Time; }
                    }
                    break;
                }
                case State.Chase:
                {
                    if (seen < 0)
                    {
                        lostFor += dt;
                        float left = Walk(GoalX, GoalZ, HuntRules.ChaseSpeed * slow, dt, HuntRules.Reach * .8f);
                        if (lostFor > HuntRules.LoseAfter && left < 2.5f)
                        {
                            Say?.Invoke("Менк потерял " + Target + " из виду.");
                            Target = null;
                            if (!FollowTracks()) Go(State.LookAround, Cue.None, X, Z);
                        }
                        break;
                    }
                    float d = Walk(GoalX, GoalZ, HuntRules.ChaseSpeed * slow, dt, HuntRules.Reach * .8f);
                    if (d < HuntRules.Reach) Go(State.Strike, Cue.Sight, GoalX, GoalZ);
                    break;
                }
                case State.Strike:
                {
                    int t = Find(players, Target);
                    if (t >= 0) Turn(HuntRules.Heading(X, Z, players[t].X, players[t].Z), dt, 200f);
                    if (StateTime > HuntRules.StrikeAt && !swung)
                    {
                        swung = true;
                        if (t >= 0 && HuntRules.Dist(X, Z, players[t].X, players[t].Z) < HuntRules.Reach + HuntRules.ReachSlack)
                        {
                            Say?.Invoke("Удар. " + players[t].Name + " не встал.");
                            Hit?.Invoke(players[t].Name);
                        }
                    }
                    if (StateTime > HuntRules.StrikeFor)
                    {
                        Target = null;
                        Go(State.LookAround, Cue.None, X, Z);
                    }
                    break;
                }
            }
        }

        /// <summary>Back to the beat, at the nearest point of the ring of the post it belongs to now.</summary>
        void BackToRing()
        {
            patrolAngle = HuntRules.Heading(PostX, PostZ, X, Z);
            pauseIn = R(HuntRules.PauseEvery, HuntRules.PauseEveryMax);
            Go(State.Patrol, Cue.None, X, Z);
        }

        /// <summary>Marks fresh enough and near enough to follow: take the freshest and go.</summary>
        bool FollowTracks()
        {
            int i = tracks.Freshest(X, Z, HuntRules.TrackSearch);
            if (i < 0) return false;
            trackChain = tracks[i].Chain; trackTime = tracks[i].Time;
            Go(State.Tracking, Cue.Tracks, tracks[i].X, tracks[i].Z);
            Say?.Invoke("Менк пошёл по следам.");
            return true;
        }

        /// <summary>Whether a player is seen: a light is seen from far; behind cover or flat in the snow only up
        /// close; standing in the open from twelve metres if facing it and six from behind.</summary>
        public bool Sees(HuntSeen p, float d)
        {
            if (!p.Alive) return false;
            if (p.Torch) return d < HuntRules.LitBodyRange;
            if (p.Covered) return d < HuntRules.HiddenRange;
            if (p.Low) return d < HuntRules.LowOpenRange;
            float toMenk = HuntRules.Heading(p.X, p.Z, X, Z);
            bool facing = Math.Abs(HuntRules.DeltaAngle(p.LookYaw, toMenk)) < 90f;
            return d < (facing ? HuntRules.SeenFaceRange : HuntRules.SeenBackRange);
        }

        static int Find(IList<HuntSeen> players, string name)
        {
            if (name == null) return -1;
            for (int i = 0; i < players.Count; i++) if (players[i].Alive && players[i].Name == name) return i;
            return -1;
        }

        void Turn(float want, float dt, float rate)
        {
            float d = HuntRules.DeltaAngle(Yaw, want);
            float step = rate * dt;
            Yaw += Math.Abs(d) <= step ? d : Math.Sign(d) * step;
        }

        /// <summary>Walks toward a point, turning as it goes; returns the distance still to go.</summary>
        float Walk(float tx, float tz, float speed, float dt, float stopAt = 0f)
        {
            float dist = HuntRules.Dist(X, Z, tx, tz);
            if (dist < 1e-3f) return dist;
            float want = HuntRules.Heading(X, Z, tx, tz);
            Turn(want, dt, 110f);
            if (dist <= stopAt) return dist;
            // it only strides where it faces, so it swings round before charging
            float facing = Math.Abs(HuntRules.DeltaAngle(Yaw, want));
            float k = HuntRules.Clamp01(1f - facing / 90f);
            float step = Math.Min(speed * k * dt, dist);
            double a = Yaw * Math.PI / 180.0;
            X += (float)Math.Sin(a) * step; Z += (float)Math.Cos(a) * step;
            return HuntRules.Dist(X, Z, tx, tz);
        }
    }

    /// <summary>How a thing is carried: in one hand (run, keep the torch), in both (walk, no torch, no lying down),
    /// or by two people at once.</summary>
    public enum Carry : byte { Light, Heavy, Pair }

    public sealed class ErrandItem
    {
        public string Name;
        public Carry Carry;
        /// <summary>Which section it lies in: "палатка" or "лабаз".</summary>
        public string Section = "палатка";
        public float X, Z;
        public bool Delivered;
        /// <summary>Who has it in hand, or null on the snow.</summary>
        public string HeldBy;

        public bool OnSnow => !Delivered && HeldBy == null;
    }

    /// <summary>The list: what lies in the tent and what may be done with each thing. Delivery is a place, not a
    /// button — put it down inside the fire's radius and it is home.</summary>
    public sealed class Errand
    {
        public readonly List<ErrandItem> Items = new List<ErrandItem>();
        readonly float fireX, fireZ;

        public Errand(float fireX, float fireZ) { this.fireX = fireX; this.fireZ = fireZ; }

        /// <summary>The brief's four: two light, one heavy, one for two. Laid out around the tent's mouth.</summary>
        public static Errand Standard(float fireX, float fireZ, float tentX, float tentZ)
        {
            var e = new Errand(fireX, fireZ);
            // the tent's mouth is toward the fire
            float dx = fireX - tentX, dz = fireZ - tentZ; float len = (float)Math.Sqrt(dx * dx + dz * dz); if (len < 1e-3f) { dx = 0; dz = -1; len = 1; }
            dx /= len; dz /= len;
            float sx = -dz, sz = dx;
            e.Items.Add(new ErrandItem { Name = "дневник", Carry = Carry.Light, X = tentX + dx * 2.2f + sx * 1.1f, Z = tentZ + dz * 2.2f + sz * 1.1f });
            e.Items.Add(new ErrandItem { Name = "фотоаппарат", Carry = Carry.Light, X = tentX + dx * 2.6f - sx * 1.3f, Z = tentZ + dz * 2.6f - sz * 1.3f });
            e.Items.Add(new ErrandItem { Name = "печка", Carry = Carry.Heavy, X = tentX + dx * 1.6f + sx * 2.4f, Z = tentZ + dz * 1.6f + sz * 2.4f });
            e.Items.Add(new ErrandItem { Name = "свёрнутая палатка", Carry = Carry.Pair, X = tentX + dx * 3.2f, Z = tentZ + dz * 3.2f });
            return e;
        }

        public int Delivered { get { int n = 0; foreach (var i in Items) if (i.Delivered) n++; return n; } }
        public int Total => Items.Count;
        public int DeliveredIn(string section) { int n = 0; foreach (var i in Items) if (i.Delivered && i.Section == section) n++; return n; }
        public int TotalIn(string section) { int n = 0; foreach (var i in Items) if (i.Section == section) n++; return n; }

        /// <summary>The night of the sandbox: one thing — the stove at the back of the tent, both hands, a walk.</summary>
        public static Errand Stove(float fireX, float fireZ, float tentX, float tentZ)
        {
            var e = new Errand(fireX, fireZ);
            float dx = fireX - tentX, dz = fireZ - tentZ; float len = (float)Math.Sqrt(dx * dx + dz * dz); if (len < 1e-3f) { dx = 0; dz = -1; len = 1; }
            dx /= len; dz /= len;
            e.Items.Add(new ErrandItem { Name = "печка", Carry = Carry.Heavy, X = tentX - dx * 1.4f, Z = tentZ - dz * 1.4f });
            return e;
        }

        /// <summary>The whole camp: the tent's four, and at the labaz — the cache dug into the snow and covered
        /// with firewood, marked by one ski — rusks and candles to carry in a hand, firewood in both, and the spare
        /// skis, which take two.</summary>
        public static Errand Camp(float fireX, float fireZ, float tentX, float tentZ, float labazX, float labazZ)
        {
            var e = Standard(fireX, fireZ, tentX, tentZ);
            float dx = fireX - labazX, dz = fireZ - labazZ; float len = (float)Math.Sqrt(dx * dx + dz * dz); if (len < 1e-3f) { dx = 0; dz = -1; len = 1; }
            dx /= len; dz /= len;
            float sx = -dz, sz = dx;
            e.Items.Add(new ErrandItem { Name = "сухари", Carry = Carry.Light, Section = "лабаз", X = labazX + dx * 1.6f + sx * .9f, Z = labazZ + dz * 1.6f + sz * .9f });
            e.Items.Add(new ErrandItem { Name = "свечи", Carry = Carry.Light, Section = "лабаз", X = labazX + dx * 1.9f - sx * 1.0f, Z = labazZ + dz * 1.9f - sz * 1.0f });
            e.Items.Add(new ErrandItem { Name = "дрова", Carry = Carry.Heavy, Section = "лабаз", X = labazX - dx * 1.2f + sx * 1.4f, Z = labazZ - dz * 1.2f + sz * 1.4f });
            e.Items.Add(new ErrandItem { Name = "запасные лыжи", Carry = Carry.Pair, Section = "лабаз", X = labazX - dx * 1.0f - sx * 1.8f, Z = labazZ - dz * 1.0f - sz * 1.8f });
            return e;
        }

        public static bool MayRun(Carry c) => c == Carry.Light;
        public static bool MayTorch(Carry c) => c == Carry.Light;
        public static bool MayLie(Carry c) => c == Carry.Light;

        /// <summary>Whether one pair of hands (or two) can pick this up.</summary>
        public static bool CanLift(ErrandItem item, int hands = 1) => item != null && item.OnSnow && (item.Carry != Carry.Pair || hands >= 2);

        public ErrandItem Held(string who) { foreach (var i in Items) if (i.HeldBy == who) return i; return null; }

        public bool Take(ErrandItem item, string who, int hands = 1)
        {
            if (!CanLift(item, hands) || Held(who) != null) return false;
            item.HeldBy = who;
            return true;
        }

        /// <summary>Put down where the carrier stands. Inside the fire's radius the thing is home and gone from the
        /// world; anywhere else it lies where it was dropped. Returns true when it counted.</summary>
        public bool PutDown(string who, float x, float z)
        {
            var item = Held(who);
            if (item == null) return false;
            item.HeldBy = null; item.X = x; item.Z = z;
            if (HuntRules.Dist(x, z, fireX, fireZ) <= HuntRules.FireRadius) { item.Delivered = true; return true; }
            return false;
        }

        /// <summary>The carrier died: the thing falls where he fell.</summary>
        public ErrandItem Drop(string who, float x, float z)
        {
            var item = Held(who);
            if (item == null) return null;
            item.HeldBy = null; item.X = x; item.Z = z;
            return item;
        }

        public bool InFire(float x, float z) => HuntRules.Dist(x, z, fireX, fireZ) <= HuntRules.FireRadius;
    }

    /// <summary>One run of the yard: the clock, the blizzard on it, the list, the marks, the Menk, and the protocol
    /// of what happened — the same <see cref="NightEvent"/> lines the night writes, so the same screen shows them.</summary>
    public sealed class HuntRun
    {
        public sealed class Member
        {
            public string Name; public bool Alive = true; public float DiedAt = -1f; public string Cause; public float Cold, SecondsLow;
        }

        public readonly List<NightEvent> Events = new List<NightEvent>();
        public readonly List<Member> Party = new List<Member>();
        public readonly Errand Errand;
        public readonly TrackChain Tracks = new TrackChain();
        public readonly HunterBrain Menk;
        public readonly float FireX, FireZ, TentX, TentZ, LabazX, LabazZ, Length;
        public float Elapsed { get; private set; }
        public bool Over { get; private set; }
        /// <summary>How it ended: "вернулись", "все погибли", or "" while it runs.</summary>
        public string Outcome { get; private set; } = "";
        /// <summary>The second section is on the Menk's beat and on the list's second line.</summary>
        public bool LabazOpen { get; private set; }
        /// <summary>Things home before the night may be called done: the rule's three, or all of a shorter list.</summary>
        public int QuotaNeeded => Math.Min(HuntRules.Quota, Errand.Total);
        /// <summary>Enough is home: the night may be called done at the fire.</summary>
        public bool QuotaMet => Errand.Delivered >= QuotaNeeded;
        public bool HasLabaz => !float.IsNaN(LabazX);
        float wallFor, homeFor;

        public float Storm => HuntRules.Storm(Elapsed, Length);
        public bool Wall => HuntRules.IsWall(Storm);

        public HuntRun(float fireX, float fireZ, float tentX, float tentZ, float length = HuntRules.RunSeconds, int seed = 1959,
                       float labazX = float.NaN, float labazZ = float.NaN, Errand errand = null)
        {
            FireX = fireX; FireZ = fireZ; TentX = tentX; TentZ = tentZ; LabazX = labazX; LabazZ = labazZ; Length = length;
            Errand = errand ?? (float.IsNaN(labazX) ? Errand.Standard(fireX, fireZ, tentX, tentZ) : Errand.Camp(fireX, fireZ, tentX, tentZ, labazX, labazZ));
            Menk = new HunterBrain(tentX, tentZ, Tracks, seed);
            Menk.Say = Record;
            Menk.Hit = who => Kill(who, "Менк");
            Record("Вышли от костра. В палатке: " + string.Join(", ", Errand.Items.FindAll(i => i.Section == "палатка").ConvertAll(i => i.Name)) + ".");
            if (Errand.Total > 1) Record($"Хватит {QuotaNeeded} вещей у костра, потом — все к огню, пока не накрыло.");
        }

        public void Record(string text) => Events.Add(new NightEvent(Elapsed, text));

        /// <summary>The clock jumps ahead: for tests and shots that cannot wait ten minutes for the front.</summary>
        public void Skip(float seconds) { if (!Over) Elapsed += Math.Max(0f, seconds); }

        public Member Join(string name)
        {
            var m = new Member { Name = name };
            Party.Add(m);
            return m;
        }

        public Member Find(string name) { foreach (var m in Party) if (m.Name == name) return m; return null; }

        /// <summary>A tick of the run. <paramref name="players"/> is what the Menk is told; the cold and the metrics
        /// are read off the same list.</summary>
        public void Tick(float dt, IList<HuntSeen> players)
        {
            if (Over) return;
            Elapsed += dt;
            float storm = Storm;
            Tracks.Fade(Elapsed, HuntRules.TrackLifeAt(storm));
            Menk.Tick(dt, players, storm);

            bool wall = Wall;
            // the living are counted off the party, not off what was seen this tick: a tick with nobody in the
            // list (a test with no players about) is not a night on which everybody died
            int alive = 0, home = 0;
            foreach (var m in Party)
            {
                if (!m.Alive) continue;
                alive++;
                int at = -1;
                for (int i = 0; i < players.Count; i++) if (players[i].Name == m.Name) { at = i; break; }
                if (at < 0) continue;
                var p = players[at];
                if (p.Low) m.SecondsLow += dt;
                bool inFire = Errand.InFire(p.X, p.Z);
                if (inFire) home++;
                if (wall && !inFire)
                {
                    m.Cold += dt;
                    if (m.Cold >= HuntRules.FreezeSeconds) { Kill(p.Name, "замёрз в пурге"); alive--; }
                }
                else m.Cold = 0f;
            }
            if (wall)
            {
                if (wallFor == 0f) Record("Пурга. Дальше трёх метров ничего не видно.");
                wallFor += dt;
                if (alive > 0 && home == alive && wallFor > 10f) End("вернулись");
            }
            // the finish: enough is home and everyone is at the fire — the night is called before the wall; with
            // everything home there is nothing to wait for
            if (!Over && alive > 0 && Errand.Delivered == Errand.Total) End("вернулись");
            else if (!Over && QuotaMet && alive > 0 && home == alive) { homeFor += dt; if (homeFor >= HuntRules.FinishHold) End("вернулись"); }
            else homeFor = 0f;
            if (alive == 0 && Party.Count > 0) End("все погибли");
        }

        void End(string outcome)
        {
            Over = true; Outcome = outcome;
            Record("Конец забега: " + outcome + ".");
        }

        public void Kill(string who, string cause)
        {
            var m = Find(who);
            if (m == null || !m.Alive) return;
            m.Alive = false; m.DiedAt = Elapsed; m.Cause = cause;
            Record(who + " погиб: " + cause + ".");
        }

        /// <summary>A player picks up what lies at his feet. Returns why not, or null.</summary>
        public string Take(string who, ErrandItem item, int hands = 1)
        {
            if (item == null || !item.OnSnow) return "здесь ничего нет";
            if (Errand.Held(who) != null) return "руки заняты";
            if (item.Carry == Carry.Pair && hands < 2) return "нужны двое";
            Errand.Take(item, who, hands);
            Record(who + " взял: " + item.Name + ".");
            return null;
        }

        public bool PutDown(string who, float x, float z)
        {
            var item = Errand.Held(who);
            if (item == null) return false;
            bool home = Errand.PutDown(who, x, z);
            Record(home ? "У костра: " + item.Name + "." : who + " положил " + item.Name + ".");
            if (home) Progress();
            return home;
        }

        /// <summary>What a thing home changes: the labaz opens after the tent's first, the quota is called.</summary>
        void Progress()
        {
            if (HasLabaz && !LabazOpen && Errand.DeliveredIn("палатка") >= HuntRules.OpensLabaz)
            {
                LabazOpen = true;
                Menk.AddPost(LabazX, LabazZ);
                Record("Теперь лабаз: " + string.Join(", ", Errand.Items.FindAll(i => i.Section == "лабаз").ConvertAll(i => i.Name)) + ". Менк ходит и туда.");
            }
            if (Errand.Total > 1 && Errand.Delivered == QuotaNeeded) Record("Хватит. Все к костру — ночь окончена, когда все у огня.");
        }

        /// <summary>Thrown: it lands a few metres away and the snow hears it.</summary>
        public ErrandItem Throw(string who, float x, float z)
        {
            var item = Errand.Drop(who, x, z);
            if (item == null) return null;
            Record(who + " бросил " + item.Name + ".");
            Menk.Hear(x, z, HuntRules.DropNoise, who);
            return item;
        }

        /// <summary>The carrier fell: what he carried lies beside him.</summary>
        public ErrandItem DropOnDeath(string who, float x, float z)
        {
            var item = Errand.Drop(who, x, z);
            if (item != null) Record(item.Name + " лежит там, где упал " + who + ".");
            return item;
        }

        /// <summary>The debrief, as lines.</summary>
        public List<string> Report()
        {
            var lines = new List<string>();
            var brought = new List<string>(); var left = new List<string>();
            foreach (var i in Errand.Items)
            {
                if (i.Delivered) brought.Add(i.Name);
                else left.Add($"{i.Name} — {HuntRules.Dist(i.X, i.Z, FireX, FireZ):0} м от костра");
            }
            lines.Add(Errand.Total == 1
                ? (Errand.Delivered == 1 ? "Печка у костра." : "Печка не донесена.")
                : $"Принесли {Errand.Delivered} из {Errand.Total} (нужно {QuotaNeeded})" + (brought.Count > 0 ? ": " + string.Join(", ", brought) : "") + ".");
            if (HasLabaz) lines.Add($"Из палатки {Errand.DeliveredIn("палатка")} из {Errand.TotalIn("палатка")}, из лабаза {Errand.DeliveredIn("лабаз")} из {Errand.TotalIn("лабаз")}" + (LabazOpen ? "." : " (лабаз не открылся)."));
            if (left.Count > 0) lines.Add("Осталось лежать: " + string.Join("; ", left) + ".");
            foreach (var m in Party)
                lines.Add(m.Alive ? $"{m.Name} — вернулся." : $"{m.Name} — погиб на {(int)(m.DiedAt / 60f) + 1}-й минуте ({m.Cause}).");
            lines.Add($"Менк шёл на свет {Menk.WentToLight}, на шум {Menk.WentToNoise}, по следам {Menk.WentByTracks}.");
            float low = 0f; foreach (var m in Party) low += m.SecondsLow;
            lines.Add($"Лёжа и на корточках: {low:0} с.");
            return lines;
        }
    }
}
