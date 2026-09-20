using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Height1079.Core;
using Height1079.Puppet;
using Height1079.Snow;

namespace Height1079.Sandbox
{
    /// <summary>The physics sandbox: one scene, no world, no network, no night. It builds the test range and one body
    /// and hands the player a panel of every number the body is made of, so the feel of climbing can be found in
    /// minutes instead of a build of the whole mountain. What is found here is saved as a tuning file and read by the
    /// game.</summary>
    public sealed class SandboxBoot : MonoBehaviour
    {
        public const string SceneName = "Sandbox";

        public static SandboxBoot Instance { get; private set; }
        public Puppet.Puppet Body { get; private set; }
        public PuppetTuning Tuning = new PuppetTuning();
        public Camera Cam { get; private set; }
        /// <summary>Hunger, cold and sleep, biting the one bar (<see cref="SandboxVitals"/>).</summary>
        public SandboxVitals Vitals { get; private set; }
        /// <summary>The three slots, the rucksack and the flashlight (<see cref="SandboxGear"/>).</summary>
        public SandboxGear Gear { get; private set; }
        /// <summary>The day is over: the bar was eaten to nothing and the body is down for good — see <see cref="Die"/>.</summary>
        public bool Dead { get; private set; }
        /// <summary>The rucksack window is open (Tab): the cursor is the player's and the look stands still.</summary>
        public bool PackOpen { get; private set; }
        /// <summary>The night run of the yard (docs/SANDBOX.md): N starts and abandons it.</summary>
        public SandboxHunt Hunt { get; private set; }
        /// <summary>The night was won: the screen is up with the words and the button, the body stands where it is.</summary>
        public bool Ended { get; private set; }
        /// <summary>What the body looks like (<see cref="SandboxFigure"/>): the Adventurer from a file by default,
        /// B goes round the three looks.</summary>
        public SandboxFigure Figure { get; private set; }
        /// <summary>What the end-of-day screen says. The bar's own words unless a death had another cause.</summary>
        public string DeathTitle { get; private set; } = "СИЛ НЕ ОСТАЛОСЬ";
        public string DeathLine { get; private set; } = "Голод, холод и сон съели всю полоску. Тело легло в снег.";

        SandboxCameraRig rig;
        bool cursorFree;
        int stand;

        /// <summary>First person or over the shoulder. First is the default and the game's, and the sandbox is here to
        /// answer whether walking <em>feels</em> right, which is a question you cannot ask from four metres behind.</summary>
        public bool FirstPerson { get => rig == null || rig.FirstPerson; set => rig?.SetView(value); }
        /// <summary>Head bob. On by default; off when the eye needs to be a tripod to read a leg spring.</summary>
        public bool HeadBob { get => rig == null || rig.Bob; set { if (rig != null) rig.Bob = value; } }
        /// <summary>Put the camera where it belongs this frame. Only the shot script needs to ask: while
        /// <see cref="Scripted"/> is set the camera is not driven from here at all.</summary>
        public void AimCamera() => rig?.Aim();
        /// <summary>The camera rig itself, for the shot script, which needs to say where to look.</summary>
        public SandboxCameraRig Eye => rig;
        /// <summary>Slow motion, for watching a fall or a slide frame by frame.</summary>
        public float TimeScale = 1f;
        /// <summary>Hands are off by default. First the legs: walking, grades, slipping, falling. Climbing is not
        /// worth tuning until a man can walk across the range without looking wrong.</summary>
        public bool HandsOn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == SceneName) Create();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneName) Create();
        }

        static void Create()
        {
            if (Instance != null) return;
            ClearGame();
            var go = new GameObject("Sandbox");
            DontDestroyOnLoad(go);
            go.AddComponent<SandboxBoot>();
        }

        /// <summary>The game keeps its world, network, HUD, weather and sound in DontDestroyOnLoad, so loading this
        /// scene from the menu does not get rid of any of it. Take the whole of that scene down — the sandbox wants an
        /// empty machine, and coming back rebuilds the game from nothing (Bootstrap.OnSceneLoaded).</summary>
        static void ClearGame()
        {
            var probe = new GameObject("sandbox-probe");
            DontDestroyOnLoad(probe);
            foreach (var root in probe.scene.GetRootGameObjects())
                if (root != probe) Destroy(root);
            Destroy(probe);
        }

        /// <summary>Back to the game: the physics step and the clock are put back the way the game expects them.</summary>
        public void LeaveToGame()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = defaultStep;
            Physics.defaultSolverIterations = defaultIterations;
            Instance = null;
            SceneManager.LoadScene("Main");
            Destroy(gameObject);
        }

        static float defaultStep = .02f;
        static int defaultIterations = 6;

        void Awake()
        {
            Instance = this;
            defaultStep = Time.fixedDeltaTime;
            defaultIterations = Physics.defaultSolverIterations;
            Tuning = PuppetTuning.Load("sandbox");
            ApplySolver();
            Sky();
            // the yard is the sandbox now (docs/SANDBOX.md); the physics range — steps, slopes, the wall, the drops —
            // is built only for the scripts that measure the body on it
            SandboxRange.Build(withRange: SandboxSelfTest.Requested || SandboxShots.Requested || DayRequested);
            if (SandboxRange.YardStand >= 0) stand = SandboxRange.YardStand;
            // `-day` opens on the physics range in daylight instead of the yard at night: the place to look at the
            // figure, the colours and the gait, and to walk the steps and slopes by hand. F11/F12 walk the stands,
            // and the yard is one of them, so the night is a keypress away rather than the only thing there is.
            if (DayRequested && SandboxRange.Stands.Count > 0) stand = 0;
            // the game's own prints, puffs and trail map (Height1079.Snow), under this object so they go when it goes
            SnowPrints.Create(transform);
            Spawn();
            rig = gameObject.AddComponent<SandboxCameraRig>();
            rig.Setup(Cam);
            rig.Bind(Body);
            Vitals = gameObject.AddComponent<SandboxVitals>();
            Gear = gameObject.AddComponent<SandboxGear>();
            gameObject.AddComponent<SandboxHud>();
            Hunt = SandboxHunt.Create(this);
            // the look: the Adventurer from a file over the puppet (the default), hiker-v1, or the sculpted figure
            Figure = gameObject.AddComponent<SandboxFigure>();
            // the keys are not written on the screen any more; say the few worth knowing once
            SandboxHud.Say("Tab — рюкзак · 1 2 3 — слоты · F — фонарик · E — шоколадка · V — вид · B — другая фигура · F1 — настройки тела · F11 — следующий стенд · N — ночь на площадке · F9 — в меню");
            ApplyCursor();
            // `-selftest` drives the body by script and quits: the only way to check physics in a batch build
            if (SandboxSelfTest.Requested) gameObject.AddComponent<SandboxSelfTest>();
            else if (SandboxShots.Requested) gameObject.AddComponent<SandboxShots>();
            // otherwise the night comes down at once: the yard is what the sandbox is for now
            else if (stand == SandboxRange.YardStand && !DayRequested) Hunt.Begin();
        }

        /// <summary>`1079-sandbox -day` — start on the physics range in daylight, with the stands built, instead of
        /// the night yard. The yard is still there under F11/F12; this only changes where the sandbox opens.</summary>
        public static bool DayRequested
        {
            get
            {
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-day") return true;
                return false;
            }
        }

        public void ApplySolver()
        {
            Time.fixedDeltaTime = 1f / Mathf.Clamp(Tuning.PhysicsRate, 30, 240);
            Physics.defaultSolverIterations = Mathf.Clamp(Tuning.SolverIterations, 4, 60);
            Physics.defaultSolverVelocityIterations = Mathf.Max(2, Tuning.SolverIterations / 2);
        }

        /// <summary>A day on a mountain: a low raking sun, snow throwing light back into every shadow, air with depth
        /// in it, and ridges on the horizon. None of the game's night, weather or sky code is dragged in — the sandbox
        /// has to come up in a second — and this is all it takes to stop the range reading as a room.</summary>
        void Sky()
        {
            var sun = new GameObject("Sun", typeof(Light));
            var l = sun.GetComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.25f; l.color = new Color(1f, .96f, .89f);
            l.shadows = LightShadows.Soft;
            // shadows on snow are pale: almost all of what the sun does not light is lit by the ground anyway
            l.shadowStrength = .7f;
            // low and off to one side, so every lip, step and drift throws a shadow long enough to read
            sun.transform.rotation = Quaternion.Euler(34f, -50f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            // a strong gradient ambient is what makes flat matte colour read as round instead of plastic, and on snow
            // the brightest of the three is the ground: that bounce is why nothing on a glacier is ever silhouetted
            RenderSettings.ambientSkyColor = new Color(.66f, .76f, .92f);
            RenderSettings.ambientEquatorColor = new Color(.74f, .78f, .83f);
            RenderSettings.ambientGroundColor = new Color(.82f, .84f, .88f);
            // distance. Without it the ridges are cardboard and the range has no size.
            // (In a player build Unity can strip the fog variants of a shader nothing in the scene fogs — the game
            //  keeps them on purpose, see ProjectSetup.EnsureFogVariants. If they ever are stripped here, the picture
            //  loses its haze and nothing else: the range still builds and still plays.)
            var air = new Color(.78f, .84f, .91f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .0026f;
            RenderSettings.fogColor = air;
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            Cam = camGo.GetComponent<Camera>();
            // the game's sixty degrees, so a slope that looks walkable here looks walkable there
            Cam.nearClipPlane = .08f; Cam.farClipPlane = 1000f; Cam.fieldOfView = 60f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = air;        // the sky is the far end of the air, or the horizon shows as a seam
        }

        /// <summary>Throws away the body and builds a new one — the only way to be sure a change of mass or size has
        /// really taken, and the sandbox's reset key.</summary>
        public void Spawn()
        {
            if (Body != null) Destroy(Body.gameObject);
            var at = stand >= 0 && stand < SandboxRange.Stands.Count ? SandboxRange.Stands[stand].Spawn : new Vector3(0f, 1.2f, -10f);
            Body = PuppetRig.Build(at, Tuning, "Climber");
            Body.HandsEnabled = HandsOn;
            Body.Landed += f => Land = f;
            rig?.Bind(Body);
        }

        public float Land { get; private set; }

        public void GoTo(int index)
        {
            if (index < 0 || index >= SandboxRange.Stands.Count) return;
            stand = index;
            Body?.Place(SandboxRange.Stands[index].Spawn);
            rig?.Snap();          // the eye is put down with the body, not flown across the range to it
        }

        public string StandName => stand >= 0 && stand < SandboxRange.Stands.Count ? SandboxRange.Stands[stand].Name : "—";

        /// <summary>The bar has been eaten to nothing (<see cref="SandboxVitals"/>): the body goes down where it
        /// stands and the day is over. The screen says so and offers a new one (<see cref="SandboxHud"/>).</summary>
        /// <summary>When the day ended, unscaled: the keys that start a new one wait a moment, so the jump the
        /// player was pressing when the blow landed does not skip the screen that says what happened.</summary>
        float diedAt = -99f;

        /// <summary>The night is over and won: the same screen as a death, without the fall.</summary>
        public void ShowEnd(string title, string line)
        {
            if (Dead || Ended) return;
            Ended = true;
            diedAt = Time.unscaledTime;
            PackOpen = false;
            DeathTitle = title; DeathLine = line;
            ApplyCursor();
        }

        public void Die(string title = null, string line = null)
        {
            if (Dead || Body == null) return;
            Dead = true;
            diedAt = Time.unscaledTime;
            PackOpen = false;
            DeathTitle = title ?? "СИЛ НЕ ОСТАЛОСЬ";
            DeathLine = line ?? "Голод, холод и сон съели всю полоску. Тело легло в снег.";
            Body.Die();
            ApplyCursor();
        }

        /// <summary>A new day at the same stand: a whole bar, a fresh body, the kit packed again. A night run that
        /// ended with this death ends with it.</summary>
        public void Restart()
        {
            // on the yard a new day is a new night: the run begins again from the fire
            if (Hunt != null && stand == SandboxRange.YardStand) { Hunt.Begin(); return; }
            Hunt?.End();
            Revive(stand >= 0 && stand < SandboxRange.Stands.Count ? SandboxRange.Stands[stand].Spawn : new Vector3(0f, 1.2f, -10f));
            SandboxHud.Say("новый день: полоска целая, рюкзак собран");
        }

        /// <summary>A fresh body here with a whole bar and the kit packed — the start of a run, or the morning
        /// after a death. Leaves the night run alone: the run itself calls this to put its player at the fire.</summary>
        public void Revive(Vector3 at)
        {
            Dead = false; Ended = false;
            Vitals?.Restart();
            Gear?.Refill();
            Spawn();
            PlaceAt(at);
            ApplyCursor();
        }

        /// <summary>Put the body down here, the eye with it.</summary>
        public void PlaceAt(Vector3 at)
        {
            Body?.Place(at);
            rig?.Snap();
        }

        /// <summary>Tab, or a click on the rucksack.</summary>
        public void TogglePack()
        {
            if (Dead) return;
            PackOpen = !PackOpen;
            ApplyCursor();
        }

        /// <summary>The cursor is the player's while something wants clicking — the rucksack, the end of the day —
        /// or while Esc has let it go; the rest of the time it is the look.</summary>
        void ApplyCursor()
        {
            bool free = cursorFree || PackOpen || Dead || Ended;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

#if ENABLE_INPUT_SYSTEM
        static bool Held(Key k) => Keyboard.current != null && Keyboard.current[k].isPressed;
        static bool Down(Key k) => Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame;
#else
        static bool Held(object k) => false;
        static bool Down(object k) => false;
#endif

        /// <summary>Set while the self-test is driving: the keyboard must not fight it.</summary>
        public bool Scripted;

        void Update()
        {
            if (Body == null) return;
            // the self-test and the shot script drive the body and place the camera themselves; a keyboard and an
            // orbit spring fighting them is how a photograph ends up framed on the back of somebody's head
            if (Scripted) return;
#if ENABLE_INPUT_SYSTEM
            if (Down(Key.F9)) { LeaveToGame(); return; }
            if (Dead || Ended)
            {
                // the day is over: only the way to a new one is left on the keys (and the button on the screen)
                bool settled = Time.unscaledTime - diedAt > (Dead ? 2.5f : 1f);
                if (settled && (Down(Key.Enter) || Down(Key.NumpadEnter) || Down(Key.Space) || Down(Key.F2))) Restart();
                Body.Drive(new PuppetInput { Look = rig.LookRotation });
                return;
            }
            if (Down(Key.Tab)) TogglePack();
            if (Down(Key.Escape))
            {
                if (PackOpen) TogglePack();
                else { cursorFree = !cursorFree; ApplyCursor(); }
            }
            if (Down(Key.F1)) SandboxHud.Panel = !SandboxHud.Panel;
            if (Down(Key.F2)) Spawn();
            // the game's key for this is V, read by physical position (a Russian layout has no V where V is), with F7
            // as the spare that reaches the game under automation. F3 was the sandbox's own and stays.
            if (Down(Key.V) || Down(Key.F7) || Down(Key.F3))
            {
                rig.Toggle();
                SandboxHud.Say(rig.FirstPerson ? "вид от первого лица (V, F7, F3)" : "вид со стороны (V, F7, F3): стоя — камера ходит вокруг тела, шаг — тело идёт за камерой");
            }
            if (Down(Key.F10))
            {
                rig.Bob = !rig.Bob;
                SandboxHud.Say(rig.Bob ? "голова покачивается при ходьбе (F10)" : "голова неподвижна — так виднее работу ног (F10)");
            }
            // the figure: the Adventurer from a file (the default), the game's hiker-v1 or the sculpted one, all
            // over the same pose, to compare on the same walk. B is free in the sandbox; in the game it pitches
            // a camp on Elbrus.
            if (Down(Key.B)) Figure.Next();
            if (Down(Key.F4)) { TimeScale = TimeScale > .9f ? .25f : 1f; Time.timeScale = TimeScale; }
            if (Down(Key.F8)) { HandsOn = !HandsOn; Body.HandsEnabled = HandsOn; SandboxHud.Say(HandsOn ? "руки включены (черновик)" : "руки выключены — работаем над ногами"); }
            if (Down(Key.F5)) { Tuning.Save("sandbox"); SandboxHud.Say("сохранено: " + PuppetTuning.PathFor("sandbox")); }
            if (Down(Key.F6)) { Tuning = PuppetTuning.Load("sandbox"); ApplySolver(); Spawn(); SandboxHud.Say("загружено"); }
            // the kit, on the game's own keys: 1, 2, 3 are the slots (the game's compass, torch and map sit on the
            // same three), 0 or Q empties the hands, F is the light, E the chocolate
            for (int i = 0; i < Kit.Slots; i++) if (Down(Key.Digit1 + i)) Gear.Select(i);
            if (Down(Key.Digit0) || Down(Key.Q)) Gear.PutAway();
            if (Down(Key.F)) Gear.ToggleTorch();
            if (Down(Key.E)) Gear.Eat();
            // the stands came off the digits to make room for the slots: F11 walks round them, F12 walks back, and
            // the F1 panel lists every one. Both reach the game under automation, which letters do not.
            if (Down(Key.F11) && SandboxRange.Stands.Count > 0) GoTo((stand + 1) % SandboxRange.Stands.Count);
            if (Down(Key.F12) && SandboxRange.Stands.Count > 0) GoTo((stand + SandboxRange.Stands.Count - 1) % SandboxRange.Stands.Count);
            // the night run of the yard (docs/SANDBOX.md): N brings the night down and puts the body at the fire;
            // N again gives the day back. Its verbs are the game's own keys — F is the light above, R takes and
            // puts down, X throws — and C and Z are the two things a hunted body does that a walking one does not.
            if (Down(Key.N)) Hunt.Begin();
            if (Hunt.Run != null)
            {
                if (Down(Key.C)) Hunt.ToggleCrouch();
                if (Down(Key.Z)) Hunt.ToggleProne();
                if (Down(Key.R)) Hunt.TakeOrPutDown();
                if (Down(Key.X)) Hunt.Throw();
            }

            // the look and the mouse buttons are the body's only while the cursor is: with the rucksack open, or
            // Esc pressed, a click is a click on the screen
            bool eyesFree = !cursorFree && !PackOpen;
            if (eyesFree && Mouse.current != null)
            {
                rig.Look(Mouse.current.delta.ReadValue());
                rig.Zoom(Mouse.current.scroll.ReadValue().y);
            }

            var move = new Vector2((Held(Key.D) ? 1f : 0f) - (Held(Key.A) ? 1f : 0f),
                                   (Held(Key.W) ? 1f : 0f) - (Held(Key.S) ? 1f : 0f));
            // letter keys do not reach the game under automation; the arrows are the spare pair
            if (move.sqrMagnitude < .01f)
                move = new Vector2((Held(Key.RightArrow) ? 1f : 0f) - (Held(Key.LeftArrow) ? 1f : 0f),
                                   (Held(Key.UpArrow) ? 1f : 0f) - (Held(Key.DownArrow) ? 1f : 0f));
            bool grabL = HandsOn && eyesFree && Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool grabR = HandsOn && eyesFree && Mouse.current != null && Mouse.current.rightButton.isPressed;
            // with the hands off, a click uses what is in them — eats, lights — the PEAK way
            if (!HandsOn && eyesFree && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Gear.Use();
            // a heavy thing from the tent is carried at a walk (Errand.MayRun)
            var carried = Hunt.Held;
            bool mayRun = carried == null || Errand.MayRun(carried.Carry);
            Body.Drive(new PuppetInput
            {
                Move = Vector2.ClampMagnitude(move, 1f),
                Look = rig.LookRotation,
                // from outside, the camera goes round a standing body without turning it; from inside the head the
                // eye is the body and turns it standing still, the way the game's own first person does
                FreeLook = !rig.FirstPerson,
                Run = (Held(Key.LeftShift) || Held(Key.RightShift)) && mayRun,
                Jump = Down(Key.Space),
                Crouch = Hunt.Crouch || Held(Key.LeftCtrl),
                Prone = Hunt.Prone,
                GrabLeft = grabL,
                GrabRight = grabR,
                PullUp = (grabL || grabR) && move.y > .3f,
            });
#endif
        }

        /// <summary>The camera is placed after everything else has moved. Put in <c>Update</c> it is always a frame
        /// behind the body it is strapped to, and from inside the head that reads as a shiver.</summary>
        void LateUpdate()
        {
            if (!Scripted) rig?.Aim();
        }
    }
}
