using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Height1079.Puppet;

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

        float yaw, pitch = 8f, orbit = 4.5f;
        bool firstPerson, cursorFree;
        int stand;
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
            SandboxRange.Build();
            Spawn();
            gameObject.AddComponent<SandboxHud>();
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            // `-selftest` drives the body by script and quits: the only way to check physics in a batch build
            if (SandboxSelfTest.Requested) gameObject.AddComponent<SandboxSelfTest>();
            else if (SandboxShots.Requested) gameObject.AddComponent<SandboxShots>();
        }

        public void ApplySolver()
        {
            Time.fixedDeltaTime = 1f / Mathf.Clamp(Tuning.PhysicsRate, 30, 240);
            Physics.defaultSolverIterations = Mathf.Clamp(Tuning.SolverIterations, 4, 60);
            Physics.defaultSolverVelocityIterations = Mathf.Max(2, Tuning.SolverIterations / 2);
        }

        void Sky()
        {
            var sun = new GameObject("Sun", typeof(Light));
            var l = sun.GetComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.1f; l.color = new Color(1f, .97f, .9f);
            l.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, 35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            // a strong gradient ambient is what makes flat matte colour read as round instead of plastic:
            // the shaded side of a body must never fall to black
            RenderSettings.ambientSkyColor = new Color(.72f, .79f, .90f);
            RenderSettings.ambientEquatorColor = new Color(.60f, .62f, .66f);
            RenderSettings.ambientGroundColor = new Color(.44f, .42f, .40f);
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            Cam = camGo.GetComponent<Camera>();
            Cam.nearClipPlane = .05f; Cam.farClipPlane = 600f; Cam.fieldOfView = 65f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(.62f, .70f, .78f);
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
        }

        public float Land { get; private set; }

        public void GoTo(int index)
        {
            if (index < 0 || index >= SandboxRange.Stands.Count) return;
            stand = index;
            Body?.Place(SandboxRange.Stands[index].Spawn);
        }

        public string StandName => stand >= 0 && stand < SandboxRange.Stands.Count ? SandboxRange.Stands[stand].Name : "—";

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
            if (Scripted) { Aim(); return; }
#if ENABLE_INPUT_SYSTEM
            if (Down(Key.Escape)) { cursorFree = !cursorFree; Cursor.lockState = cursorFree ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = cursorFree; }
            if (Down(Key.F1)) SandboxHud.Panel = !SandboxHud.Panel;
            if (Down(Key.F2)) Spawn();
            if (Down(Key.F3) || Down(Key.F7)) firstPerson = !firstPerson;
            if (Down(Key.F4)) { TimeScale = TimeScale > .9f ? .25f : 1f; Time.timeScale = TimeScale; }
            if (Down(Key.F9)) { LeaveToGame(); return; }
            if (Down(Key.F8)) { HandsOn = !HandsOn; Body.HandsEnabled = HandsOn; SandboxHud.Say(HandsOn ? "руки включены (черновик)" : "руки выключены — работаем над ногами"); }
            if (Down(Key.F5)) { Tuning.Save("sandbox"); SandboxHud.Say("сохранено: " + PuppetTuning.PathFor("sandbox")); }
            if (Down(Key.F6)) { Tuning = PuppetTuning.Load("sandbox"); ApplySolver(); Spawn(); SandboxHud.Say("загружено"); }
            for (int i = 0; i < SandboxRange.Stands.Count && i < 9; i++)
                if (Down(Key.Digit1 + i)) GoTo(i);

            if (!cursorFree && Mouse.current != null)
            {
                var d = Mouse.current.delta.ReadValue();
                yaw += d.x * .12f;
                pitch = Mathf.Clamp(pitch - d.y * .12f, -80f, 80f);
                orbit = Mathf.Clamp(orbit - Mouse.current.scroll.ReadValue().y * .004f, 1.5f, 14f);
            }

            var move = new Vector2((Held(Key.D) ? 1f : 0f) - (Held(Key.A) ? 1f : 0f),
                                   (Held(Key.W) ? 1f : 0f) - (Held(Key.S) ? 1f : 0f));
            // letter keys do not reach the game under automation; the arrows are the spare pair
            if (move.sqrMagnitude < .01f)
                move = new Vector2((Held(Key.RightArrow) ? 1f : 0f) - (Held(Key.LeftArrow) ? 1f : 0f),
                                   (Held(Key.UpArrow) ? 1f : 0f) - (Held(Key.DownArrow) ? 1f : 0f));
            bool grabL = HandsOn && Mouse.current != null && Mouse.current.leftButton.isPressed && !cursorFree;
            bool grabR = HandsOn && Mouse.current != null && Mouse.current.rightButton.isPressed && !cursorFree;
            var look = Quaternion.Euler(pitch, yaw, 0f);
            Body.Drive(new PuppetInput
            {
                Move = Vector2.ClampMagnitude(move, 1f),
                Look = look,
                Run = Held(Key.LeftShift) || Held(Key.RightShift),
                Jump = Down(Key.Space),
                GrabLeft = grabL,
                GrabRight = grabR,
                PullUp = (grabL || grabR) && move.y > .3f,
            });
#endif
            Aim();
        }

        void LateUpdate() => Aim();

        void Aim()
        {
            if (Cam == null || Body == null) return;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var head = Body.Torso.position + Body.Torso.transform.up * (Tuning.TorsoHeight * .5f + .1f);
            if (firstPerson) { Cam.transform.SetPositionAndRotation(head, rot); return; }
            var pivot = Body.Torso.position + Vector3.up * .3f;
            var want = pivot - rot * Vector3.forward * orbit;
            if (Physics.SphereCast(pivot, .2f, (want - pivot).normalized, out var hit, orbit, ~0, QueryTriggerInteraction.Ignore))
                want = pivot + (want - pivot).normalized * Mathf.Max(.8f, hit.distance - .1f);
            Cam.transform.position = Vector3.Lerp(Cam.transform.position, want, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
            Cam.transform.LookAt(pivot);
        }
    }
}
