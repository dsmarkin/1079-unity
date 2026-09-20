using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>One working ropeway in the scene: towers on the ground, the sagging rope and every car moving along it.
    /// All the kinematics live in <see cref="Ropeway"/> (Core, unit-tested); this class only draws them.</summary>
    public sealed class RopewayRig : MonoBehaviour
    {
        public Ropeway Line { get; private set; }
        public RopewaySpec Spec => Line.Spec;
        /// <summary>Shared clock of every ropeway on the map. <see cref="ElbrusRides"/> advances it, faster while a passenger asks for it.</summary>
        public static double Clock;

        Transform[] cars;
        Transform[] seats;

        public Transform Car(int i) => cars[i];
        public Transform Seat(int i) => seats[i];

        public static RopewayRig Create(RopewaySpec spec, Ropeway line, Transform parent)
        {
            var go = new GameObject("Ropeway_" + spec.Id);
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<RopewayRig>();
            rig.Line = line;
            rig.BuildTowers();
            rig.BuildRope();
            rig.BuildCars();
            return rig;
        }

        static GameObject Load(string name) => WorldAssets.Load<GameObject>("Prefabs/Elbrus/" + name);

        void BuildTowers()
        {
            var towers = new GameObject("Towers").transform; towers.SetParent(transform, false);
            for (int i = 1; i < Spec.Towers.Length - 1; i++)
            {
                var t = Spec.Towers[i];
                var prev = Spec.Towers[i - 1]; var next = Spec.Towers[i + 1];
                float yaw = Mathf.Atan2(next.x - prev.x, next.z - prev.z) * Mathf.Rad2Deg;
                float h = Line.TowerHeight[i];
                var pos = new Vector3(t.x, Line.Ground[i], t.z);

                if (Spec.Kind == RopewayKind.Pendulum)
                {
                    int[] sizes = { 16, 22, 30, 40 };
                    int best = sizes[0];
                    foreach (var s in sizes) if (Mathf.Abs(s - h) < Mathf.Abs(best - h)) best = s;
                    var prefab = Load("Elb_Tower_Lattice_" + best);
                    if (prefab == null) continue;
                    var go = Instantiate(prefab, pos, Quaternion.Euler(0, yaw, 0), towers);
                    go.transform.localScale = new Vector3(1, h / best, 1);
                }
                else
                {
                    var prefab = Load(Spec.Kind == RopewayKind.Chair ? "Elb_Tower_Chair" : "Elb_Tower_Gondola");
                    if (prefab == null) continue;
                    var go = Instantiate(prefab, pos, Quaternion.Euler(0, yaw, 0), towers);
                    var mast = go.transform.Find("Mast");
                    var head = go.transform.Find("Head");
                    if (mast != null) mast.localScale = new Vector3(1, h, 1);
                    if (head != null) head.localPosition = new Vector3(0, h, 0);
                    var trunk = go.GetComponentInChildren<CapsuleCollider>();
                    if (trunk != null) { trunk.height = h; trunk.transform.localPosition = new Vector3(0, h / 2, 0); }
                }
            }
        }

        /// <summary>The haul rope (or, on a jig-back, the two track ropes and the haul rope between them) as a thin tube.</summary>
        void BuildRope()
        {
            var mesh = new Mesh { name = "Rope_" + Spec.Id };
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
            float gauge = Spec.Kind == RopewayKind.Chair ? 2.2f : Ropeway.Gauge;
            AddStrand(verts, norms, tris, gauge / 2, Spec.Kind == RopewayKind.Pendulum ? .035f : .028f);
            AddStrand(verts, norms, tris, -gauge / 2, Spec.Kind == RopewayKind.Pendulum ? .035f : .028f);
            if (Spec.Kind == RopewayKind.Pendulum) AddStrand(verts, norms, tris, 0f, .018f);
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var go = new GameObject("Rope", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mat = Resources.Load<Material>("World/Materials/ElbSteel");
            var r = go.GetComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void AddStrand(List<Vector3> verts, List<Vector3> norms, List<int> tris, float offset, float radius)
        {
            const int sides = 5;
            int start = verts.Count;
            var pts = Line.Rope;
            for (int i = 0; i < pts.Length; i++)
            {
                var p = new Vector3(pts[i].x, pts[i].y, pts[i].z);
                var d = Direction(i);
                var right = Vector3.Cross(Vector3.up, d).normalized;
                var up = Vector3.Cross(d, right).normalized;
                var c = p + right * offset;
                for (int k = 0; k < sides; k++)
                {
                    float a = Mathf.PI * 2 * k / sides;
                    var n = right * Mathf.Cos(a) + up * Mathf.Sin(a);
                    verts.Add(c + n * radius); norms.Add(n);
                }
            }
            for (int i = 0; i < pts.Length - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = start + i * sides + k, b = start + i * sides + (k + 1) % sides;
                    int c = a + sides, d = b + sides;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
        }

        Vector3 Direction(int i)
        {
            var pts = Line.Rope;
            int a = Mathf.Max(0, i - 1), b = Mathf.Min(pts.Length - 1, i + 1);
            var v = new Vector3(pts[b].x - pts[a].x, pts[b].y - pts[a].y, pts[b].z - pts[a].z);
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }

        void BuildCars()
        {
            string prefabName = Spec.Kind == RopewayKind.Pendulum ? "Elb_Car_Pendulum"
                : Spec.Kind == RopewayKind.Chair ? "Elb_Chair" : "Elb_Cabin_Gondola";
            var prefab = Load(prefabName);
            cars = new Transform[Line.Cars];
            seats = new Transform[Line.Cars];
            if (prefab == null) return;
            var holder = new GameObject("Cars").transform; holder.SetParent(transform, false);
            for (int i = 0; i < Line.Cars; i++)
            {
                var go = Instantiate(prefab, holder);
                go.name = prefabName + "_" + i;
                // cars are moved by hand every frame: colliders would only shove the passengers about
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                cars[i] = go.transform;
                var seat = go.transform.Find("Seat");
                seats[i] = seat != null ? seat : go.transform;
            }
            Move();
        }

        /// <summary>Beyond this the whole line stops being moved and drawn: a cabin at three kilometres is a speck,
        /// and six lines of thirty to sixty-five cars each were writing a transform apiece every frame from the
        /// saddle, ten kilometres away from any of them.</summary>
        const float LiveM = 900f, LiveHysteresisM = 60f;
        /// <summary>A line further than that is still moved, but rarely — so that a cabin is where it should be by
        /// the time anybody comes back into sight of it, and never jumps.</summary>
        const float FarStep = .2f;
        bool live = true;
        float farNext;

        void Update()
        {
            if (cars == null) return;
            var me = Bootstrap.LocalHiker;
            // the line somebody is riding is always live, whatever the distance says
            bool riding = me != null && me.Ride != null && me.Ride.IsChildOf(transform);
            bool near = riding || Near(me);
            if (near != live)
            {
                live = near;
                foreach (var t in cars) if (t != null) t.gameObject.SetActive(live);
            }
            if (live) { Move(); return; }
            if (Time.time < farNext) return;
            farNext = Time.time + FarStep;
            Move();
        }

        /// <summary>Is anybody close enough to this line for its cars to be worth a frame? Measured to the two ends
        /// and the middle, because a line is kilometres long and its nearest point is rarely a terminal.</summary>
        bool Near(HikerController me)
        {
            if (me == null) return true;   // no hiker yet: the menu camera may be looking at anything
            var p = me.transform.position;
            float limit = live ? LiveM + LiveHysteresisM : LiveM;
            float limitSq = limit * limit;
            for (int i = 0; i <= 2; i++)
            {
                var q = Line.PointAt(Line.Length * .5f * i);
                float dx = q.x - p.x, dz = q.z - p.z;
                if (dx * dx + dz * dz < limitSq) return true;
            }
            return false;
        }

        void Move()
        {
            if (cars == null) return;
            float gauge = Spec.Kind == RopewayKind.Chair ? 2.2f : Ropeway.Gauge;
            for (int i = 0; i < cars.Length; i++)
            {
                if (cars[i] == null) continue;
                var c = Line.CarAt(i, Clock);
                var p = Line.PointAt(c.S);
                var d = Line.DirectionAt(c.S);
                var dir = new Vector3(d.x, d.y, d.z);
                var right = Vector3.Cross(Vector3.up, dir).normalized;
                float side = c.Up ? 1f : -1f;
                cars[i].position = new Vector3(p.x, p.y - Ropeway.Hang(Spec.Kind), p.z) + right * (gauge / 2 * side);
                // A car hangs from a grip and is held by gravity, not by the rope's angle: on a 30 degree span the
                // body still stands plumb and its floor stays level. Taking the yaw from the rope and nothing else is
                // what makes it look like a cabin rather than a crate glued to the cable.
                var look = new Vector3(dir.x, 0f, dir.z) * side;
                if (look.sqrMagnitude > 1e-6f) cars[i].rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }
        }

        /// <summary>Index of a car a hiker standing at this terminal can step into now, or -1.</summary>
        public int Boardable(bool atBottom) => Line.BoardableCar(Clock, atBottom);

        public Vector3 TerminalPoint(bool bottom)
        {
            var t = bottom ? Spec.Towers[0] : Spec.Towers[Spec.Towers.Length - 1];
            float g = bottom ? Line.Ground[0] : Line.Ground[Line.Ground.Length - 1];
            return new Vector3(t.x, g, t.z);
        }

        /// <summary>Which car (if any) the hiker is riding is decided by ElbrusRides; this is where it should step out.</summary>
        public Vector3 ExitPoint(int car)
        {
            var c = Line.CarAt(car, Clock);
            bool bottom = c.S < Line.Length / 2f;
            var t = TerminalPoint(bottom);
            var other = TerminalPoint(!bottom);
            var away = (t - other); away.y = 0; away = away.normalized;
            return t + away * 6f + Vector3.up * 1.2f;
        }
    }
}
