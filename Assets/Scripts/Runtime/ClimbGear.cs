using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The mountain as one hiker's legs feel it. On the southern slope of Elbrus this component sits between
    /// <see cref="Ascent"/> and <see cref="HikerController"/> the way <see cref="SkiGear"/> sits between
    /// <see cref="Skiing"/> and the snow of Kholat Syakhl.
    ///
    /// It owns nothing. The pools — frostbite, dryness, drowsiness, the heart, the sickness — belong to the host, which
    /// runs <see cref="Ascent.Tick"/> and publishes the answer in <see cref="HikerController.Climb"/>. What happens
    /// here is only what the legs need every physics step and cannot wait a network tick for: where the climber stands
    /// (<see cref="Climb.Point"/>), what that does to his pace, whether running is possible at all, which way the wind
    /// and the ataxia are pulling him, whether the gear gate is shut in front of him, and whether he is sliding.
    ///
    /// Every rule it applies is a pure function of the published state and of the height field, which both sides have,
    /// so the host and the owner never disagree about the ground.
    ///
    /// On Kholat Syakhl the component does nothing at all: <see cref="Climb.On"/> is false and every entry point
    /// returns before it touches anything.</summary>
    [DisallowMultipleComponent]
    public sealed class ClimbGear : MonoBehaviour
    {
        /// <summary>How fast an unarrested slide runs away, m/s, and how hard it pulls down the fall line.</summary>
        public const float SlideTopMs = 18f, SlidePull = 16f;
        /// <summary>Moving further than this gives up whatever the hands were doing — crampons go on standing still.</summary>
        public const float JobStillM = .7f;
        /// <summary>Above this the gate of Pastukhov rocks starts refusing an unequipped climber the uphill.</summary>
        public static float GateFromEle => Ascent.GearGateEle - 6f;
        /// <summary>Where the mountain begins, as far as the pace is concerned. <see cref="Ascent.Hypoxia"/> scales the
        /// whole curve by the acclimatisation, including the flat part below 3 800 m, so a tourist straight off the
        /// bus would cross the Azau meadow at six tenths of a walking pace — which is not what the rule is about.
        /// The runtime fades the multiplier in over the kilometre from the meadow to the top station, so a walk across
        /// a square at 2 350 m is a walk across a square and the air only starts to tell where it actually is thin.</summary>
        public const float ThinFromEle = 2900f, ThinToEle = 3850f;

        HikerController hiker;
        Climber mirror = new Climber();
        RoutePoint here;
        MountainAir air;
        float speedFactor = 1f;
        bool mayRun = true, barred;
        float slideLeft, slideTotal;
        bool slideFatal;

        /// <summary>Where the climber stands, as the rules read it. Sampled every physics step by the owner.</summary>
        public RoutePoint Where => here;
        /// <summary>The air at him: temperature of his height, wind after the funnel, how far he can see.</summary>
        public MountainAir Air => air;
        /// <summary>Surface × air × sickness × drowsiness, straight out of <see cref="Ascent.SpeedFactor"/>.</summary>
        public float SpeedFactor => speedFactor;
        /// <summary>False above <see cref="Ascent.RunCeiling"/>: not "running costs more" — the move is gone.</summary>
        public bool MayRun => mayRun;
        /// <summary>Sideways metres per second: the wind's push plus the ataxia's pull.</summary>
        public float DriftMs => State.DriftMs;
        /// <summary>The kit is short and the gate of Pastukhov rocks is right here.</summary>
        public bool Barred => barred;
        /// <summary>Hands busy with crampons or the thermos: the legs are locked until it is done or given up.</summary>
        public bool Busy => Job != ClimbJob.None;
        public ClimbJob Job => Climb.On && hiker != null ? (ClimbJob)hiker.Climb.Value.Job : ClimbJob.None;
        /// <summary>0…1 of the job in hand.</summary>
        public float JobProgress => hiker != null ? hiker.Climb.Value.Progress / 255f : 0f;
        /// <summary>Falling down the line of the water with nothing to stop it.</summary>
        public bool Sliding => slideLeft > 0f;
        /// <summary>Metres of the run-out already covered, 0…1 of it.</summary>
        public float SlideShare => slideTotal <= 0f ? 0f : 1f - slideLeft / slideTotal;
        /// <summary>This run-out ends on the ice cliffs: the host has already written the outcome.</summary>
        public bool SlideFatal => slideFatal;
        /// <summary>What the host says about this climber.</summary>
        public ClimbNet State => hiker != null ? hiker.Climb.Value : default;
        /// <summary>The climber as this client sees him — good for every rule that only reads the pools.</summary>
        public Climber Mirror => mirror;
        public Gear Kit => State.Gear;
        public bool CramponsOn => State.Has(ClimbNet.Mark.Crampons);

        // ── what the body remembers between runs ──────────────────────────────────────────────────────────

        /// <summary>Acclimatisation is the one thing about a climber that is meant to outlive a run, so it lives on
        /// the player's own machine: a first outing to the rocks and back is worth a fifth of the bar, and a body that
        /// is left alone gives it back at 0.05 a day (<see cref="Ascent.Acclimatise"/>).</summary>
        const string AcclimKey = "1079.elbrus.acclim", HighestKey = "1079.elbrus.highest";

        /// <summary>What this player's body knows about height, 0.3 for somebody straight off the ropeway.</summary>
        public static float Acclimatisation
        {
            get { try { return Mathf.Clamp01(PlayerPrefs.GetFloat(AcclimKey, .3f)); } catch { return .3f; } }
        }

        /// <summary>The highest point of the last outing, for the line the menu could show one day.</summary>
        public static float LastHighest
        {
            get { try { return PlayerPrefs.GetFloat(HighestKey, 0f); } catch { return 0f; } }
        }

        /// <summary>The host has closed the books on an outing: climb high, sleep low, and this is what is left.</summary>
        public static void RememberSortie(float acclim, float highest)
        {
            try
            {
                PlayerPrefs.SetFloat(AcclimKey, Mathf.Clamp01(acclim));
                PlayerPrefs.SetFloat(HighestKey, Mathf.Max(highest, PlayerPrefs.GetFloat(HighestKey, 0f)));
                PlayerPrefs.Save();
            }
            catch { /* a player with no writable prefs simply starts fresh every time */ }
        }

        void Awake() { hiker = GetComponent<HikerController>(); }

        /// <summary>Owner: tell the host who this is and what the body already knows, once the session is up. The key
        /// is what a save is filed under (<see cref="Saves.MyKey"/>) — if the host has a save with that key, the
        /// answer comes straight back and overrides all of this.</summary>
        bool told;

        void Update()
        {
            if (told || !Climb.On || hiker == null || !hiker.IsSpawned || !hiker.IsOwner) return;
            var s = NightSession.Instance;
            if (s == null) return;
            told = true;
            s.ReportProfile(Saves.MyKey, Acclimatisation, CafeService.Purse.Roubles);
        }

        /// <summary>Owner, every physics step: read the ground, then work out the pace from it. The same three lines
        /// the host runs a quarter of a second later, on the same numbers.</summary>
        public void Sample(Vector3 moving)
        {
            if (!Climb.On || hiker == null || Bootstrap.Dem == null) { speedFactor = 1f; mayRun = true; barred = false; return; }
            var net = State;
            mirror = net.Mirror();
            var dir = moving.sqrMagnitude > .01f ? moving : transform.forward;
            here = Climb.Point(Bootstrap.Dem, transform.position, dir);
            var s = NightSession.Instance;
            air = Climb.Air(here.Ele, transform.position.x, transform.position.z, Weather.Storm, 0f);
            float thin = Mathf.InverseLerp(ThinFromEle, ThinToEle, here.Ele);
            speedFactor = Mathf.Lerp(1f, Mathf.Clamp(Ascent.SpeedFactor(mirror, here), .05f, 1f), thin);
            // and what is left in the legs, the way the лыжня does it: under a third of the bar costs half the pace
            speedFactor *= Mathf.Lerp(.5f, 1f, Mathf.Clamp01(hiker.Strength.Value / 255f / .35f));
            if (net.Has(ClimbNet.Mark.MustStop)) speedFactor = 0f;
            mayRun = Ascent.MayRun(here.Ele);
            barred = !Ascent.MayPassGate(Ascent.GearGateEle, net.Gear) && here.Ele >= GateFromEle;
            if (s == null) { speedFactor = 1f; mayRun = true; barred = false; }
        }

        /// <summary>What is left of the player's wish once the mountain has had its say: above Pastukhov rocks without
        /// the kit the uphill component is simply gone. Not a wall — you may traverse, and you may go down; you may
        /// not go up. The host holds the same line and puts back anybody who gets above it anyway.</summary>
        public Vector3 Allow(Vector3 wish)
        {
            if (!barred || wish.sqrMagnitude < 1e-4f || Bootstrap.Dem == null) return wish;
            var (dx, dz, slope) = Bootstrap.Dem.Fall(transform.position.x, transform.position.z, 10f);
            if (slope < .4f) return wish;
            var uphill = new Vector3(-dx, 0f, -dz).normalized;
            float up = Vector3.Dot(wish, uphill);
            return up <= 0f ? wish : wish - uphill * up;
        }

        /// <summary>The push the wind and the ataxia give, across the way he is going.</summary>
        public Vector3 Drift(Vector3 forward)
        {
            float ms = DriftMs;
            if (!Climb.On || ms <= 0f) return Vector3.zero;
            // the wind of the funnel blows across the shelf and down the fall line, which is where it kills; the
            // ataxia pulls the same way, because that is the way the ground already leans
            if (Bootstrap.Dem == null) return Vector3.zero;
            var (dx, dz, slope) = Bootstrap.Dem.Fall(transform.position.x, transform.position.z, 10f);
            if (slope < 1f) return Vector3.zero;
            return new Vector3(dx, 0f, dz).normalized * ms;
        }

        // ── the slide ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The host says the feet went and the axe did not hold: the run-out starts here and the player has
        /// no say in it until it is over.</summary>
        public void BeginSlide(float runoutM, bool fatal)
        {
            slideTotal = slideLeft = Mathf.Max(5f, runoutM);
            slideFatal = fatal;
        }

        /// <summary>Metres of the run-out just covered.</summary>
        public void Slid(float metres)
        {
            if (slideLeft <= 0f) return;
            slideLeft = Mathf.Max(0f, slideLeft - Mathf.Max(0f, metres));
        }

        public void StopSlide() { slideLeft = 0f; slideTotal = 0f; slideFatal = false; }

        // ── keys ──────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Owner keys of the ascent: C (or F1) puts the crampons on and takes them off, T (or F2) is a sip out
        /// of the thermos. Both are asks — the host decides, because both take time standing still.</summary>
        public void HandleInput()
        {
            if (!Climb.On || hiker == null || !hiker.IsOwner) return;
            var s = NightSession.Instance;
            if (s == null) return;
            if (Controls.Crampons) s.RequestCrampons(!CramponsOn);
            if (Controls.Thermos) s.RequestSip();
        }

        /// <summary>The line under the crosshair when the mountain has something to say about the next step.</summary>
        public string Prompt()
        {
            if (!Climb.On || hiker == null) return "";
            var net = State;
            switch ((ClimbJob)net.Job)
            {
                case ClimbJob.CramponsOn: return $"Надеваете кошки… стойте · {Mathf.RoundToInt(net.Progress / 255f * 100f)}%";
                case ClimbJob.CramponsOff: return $"Снимаете кошки… · {Mathf.RoundToInt(net.Progress / 255f * 100f)}%";
                case ClimbJob.Sip: return $"Глоток из термоса… · {Mathf.RoundToInt(net.Progress / 255f * 100f)}%";
                case ClimbJob.Pitch: return $"Ставите палатку… стойте · {Mathf.RoundToInt(net.Progress / 255f * 100f)}%";
                case ClimbJob.Strike: return $"Сворачиваете лагерь… · {Mathf.RoundToInt(net.Progress / 255f * 100f)}%";
            }
            if (Sliding) return "Срыв! Вас несёт вниз";
            if (barred) return "Выше скал Пастухова не пускают · " + Climb.GateLine(net.Gear);
            return "";
        }
    }
}
