using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Places the reconstructed event sites, landmark trees and the archive layer (alternative versions, KAN landmarks, route flags).
    /// The archive layer is hidden by default and toggled with F2.</summary>
    public static class WorldDressing
    {
        public static GameObject Archive { get; private set; }

        static GameObject Spawn(string prefab, Vector3 pos, Quaternion rot, Transform parent)
        {
            var p = Resources.Load<GameObject>("World/Prefabs/" + prefab);
            if (p == null) { Debug.LogWarning("World prefab missing: " + prefab); return null; }
            return Object.Instantiate(p, pos, rot, parent);
        }

        static Vector3 Ground(HeightField dem, float x, float z, float dy = 0) => new Vector3(x, TerrainBuilder.Height(dem, x, z) + dy, z);

        public static void Build(HeightField dem)
        {
            var sites = new GameObject("EventSites").transform;

            // tent: ridge along the contour, entrance toward the pass, cuts on the downslope side; floor levelled at the uphill edge
            var (ex, ez, dx, dz, _) = Sites.Tent.Orientation(dem);
            var t = WorldData.Tent;
            float uphill = TerrainBuilder.Height(dem, t.X - dx * 1.1f, t.Z - dz * 1.1f);
            var rot = Quaternion.LookRotation(new Vector3(ex, 0, ez), Vector3.up);
            var tent = Spawn("Sites/Site_Tent_1959", new Vector3(t.X, uphill - .05f, t.Z), rot, sites);
            if (tent != null && Vector3.Dot(rot * Vector3.right, new Vector3(dx, 0, dz)) < 0) tent.transform.localScale = new Vector3(-1, 1, 1);

            // night camp of 31 Jan as on the morning of 1 Feb: tent with its entrance toward the fire, kitchen, labaz being dug
            var l = WorldData.Labaz;
            var (ldx, ldz, _) = dem.Fall(l.X, l.Z);
            var labazRot = Quaternion.LookRotation(new Vector3(-ldz, 0, ldx));
            Spawn("Sites/Site_Labaz_1959_Morning", Ground(dem, l.X, l.Z), labazRot, sites);
            var (px, pz) = WorldData.CampTentPad;
            var toFire = new Vector3(WorldData.Camp.x - px, 0, WorldData.Camp.z - pz);
            campTent = Spawn("Sites/Site_Camp_31Jan_Tent", Ground(dem, px, pz, .02f), Quaternion.LookRotation(toFire), sites)?.transform;
            campFire = Spawn("Sites/Site_Camp_31Jan_Fire", Ground(dem, WorldData.Camp.x, WorldData.Camp.z, -.05f), Quaternion.LookRotation(-toFire), sites)?.transform;

            var c = WorldData.Cedar;
            Spawn("Sites/Site_Cedar_1959", Ground(dem, c.X, c.Z, -.05f), Quaternion.identity, sites);

            var down = new Vector3(WorldData.Creek[2].x - WorldData.Creek[1].x, 0, WorldData.Creek[2].z - WorldData.Creek[1].z);
            Spawn("Sites/Site_Den_1959", Ground(dem, WorldData.Den.x, WorldData.Den.z), Quaternion.LookRotation(down.sqrMagnitude > 1e-4f ? down : Vector3.forward), sites);
            Spawn("Sites/Site_P4_Stone", Ground(dem, WorldData.P4.X, WorldData.P4.Z), Quaternion.Euler(0, 40, 0), sites);

            var d = WorldData.Get("dyatlov");
            Spawn("Trees/DyatlovBirch", Ground(dem, d.X + .3f, d.Z + .3f, -.1f), Quaternion.Euler(0, 70, 0), sites);
            var tr = WorldData.Get("triple");
            Spawn("Trees/TripleSpruce", Ground(dem, tr.X, tr.Z, -.1f), Quaternion.Euler(0, 15, 0), sites);

            // markers: documented event points are visible, everything else is in the archive layer
            Archive = new GameObject("ArchiveLayer");
            // the labaz as the searchers found it on 2 Mar (same spot as the morning pit)
            Spawn("Sites/Site_Labaz_1959", Ground(dem, l.X, l.Z), labazRot, Archive.transform);
            foreach (var p in WorldData.Pois)
            {
                string kind = p.Kind.ToString();
                Transform parent = p.Kind == WorldData.Kind.Event ? sites : Archive.transform;
                float off = p.Kind == WorldData.Kind.Event ? 3.5f : 0f;
                var toTent = new Vector3(t.X - p.X, 0, t.Z - p.Z);
                var dir = toTent.sqrMagnitude > 1 ? toTent.normalized : Vector3.forward;
                var pos = Ground(dem, p.X - dir.z * off, p.Z + dir.x * off, -.05f);
                var m = Spawn("Sites/Marker_" + kind, pos, Quaternion.LookRotation(dir), parent);
                if (m == null) continue;
                m.name = "Marker " + p.Id;
                var label = m.GetComponentInChildren<TextMesh>();
                if (label != null) label.text = Wrap(p.Label, 26);
            }
            Flags(dem, WorldData.FootprintLine, 25f, "Sites/Flag_Footprints");
            Flags(dem, WorldData.AscentRoute, 40f, "Sites/Flag_Ascent");
            Archive.SetActive(false);
        }

        static void Flags(HeightField dem, (float x, float z)[] line, float every, string prefab)
        {
            float acc = 0, next = 0;
            for (int i = 1; i < line.Length; i++)
            {
                var a = line[i - 1]; var b = line[i];
                float len = WorldData.Distance(a.x, a.z, b.x, b.z);
                if (len < 1e-3f) continue;
                var yaw = Quaternion.Euler(0, Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg, 0);
                while (next <= acc + len)
                {
                    float k = (next - acc) / len;
                    Spawn(prefab, Ground(dem, Mathf.Lerp(a.x, b.x, k), Mathf.Lerp(a.z, b.z, k)), yaw, Archive.transform);
                    next += every;
                }
                acc += len;
            }
        }

        static string Wrap(string s, int width)
        {
            var sb = new System.Text.StringBuilder(); int col = 0;
            foreach (var word in s.Split(' '))
            {
                if (col > 0 && col + word.Length > width) { sb.Append('\n'); col = 0; }
                else if (col > 0) { sb.Append(' '); col++; }
                sb.Append(word); col += word.Length;
            }
            return sb.ToString();
        }

        /// <summary>Site viewer (F3): orbit camera around the event sites in turn; the hiker keeps standing where it was. Index -1 = off.</summary>
        public static int ViewIndex { get; private set; } = -1;
        static Transform campTent, campFire;
        static readonly string[] ViewIds = { "tent", "cedar", "p4", "labaz", "camp-gear", "camp-inside", "camp-things", "camp-kitchen", "dyatlov" };
        static readonly float[] ViewRadius = { 9f, 14f, 8f, 13f, 2.4f, .7f, .42f, 2.6f, 10f };
        public static string ViewName => ViewIndex < 0 ? "" : ViewIds[ViewIndex] switch
        {
            "labaz" => "Стоянка 31 января и лабаз",
            "camp-gear" => "Стоянка 31 января · рюкзаки, лыжи, палки",
            "camp-inside" => "Стоянка 31 января · в палатке",
            "camp-things" => "Стоянка 31 января · фотоаппарат «Зоркий», дневник, ботинки",
            "camp-kitchen" => "Стоянка 31 января · костёр на брёвнах",
            _ => WorldData.Get(ViewIds[ViewIndex]).Label,
        };

        public static void NextView() { ViewIndex = ViewIndex + 1 >= ViewIds.Length ? -1 : ViewIndex + 1; }

        /// <summary>Places the camera for the current site view; returns false when the viewer is off.</summary>
        public static bool UpdateView(Camera cam, HeightField dem)
        {
            if (ViewIndex < 0 || cam == null || dem == null) return false;
            string vid = ViewIds[ViewIndex];
            float a = Time.unscaledTime * .12f, r = ViewRadius[ViewIndex];
            if (vid.StartsWith("camp-"))
            {
                var anchor = vid == "camp-kitchen" ? campFire : campTent;
                if (anchor == null) return false;
                Vector3 local = vid == "camp-gear" ? new Vector3(.9f, .35f, 3.4f) : vid == "camp-inside" ? new Vector3(0, .35f, .4f) : vid == "camp-things" ? new Vector3(.3f, .07f, 1.4f) : new Vector3(0, .5f, .3f);
                float h = vid == "camp-gear" ? 1.1f : vid == "camp-inside" ? .22f : vid == "camp-things" ? .22f : 1.3f;
                var c = anchor.TransformPoint(local);
                var eye = c + new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r);
                if (vid == "camp-things") eye = anchor.TransformPoint(local + new Vector3(Mathf.Cos(a * 2f) * r, h, Mathf.Sin(a * 2f) * r * .8f));
                if (vid == "camp-inside") eye = anchor.TransformPoint(local + new Vector3(Mathf.Cos(a * 1.5f) * .55f, h, Mathf.Sin(a * 1.5f) * 1.2f));
                cam.transform.position = eye;
                cam.transform.LookAt(c);
                return true;
            }
            var p = WorldData.Get(vid);
            float x = p.X, z = p.Z;
            if (vid == "p4") { x = WorldData.Den.x; z = WorldData.Den.z; }
            if (vid == "labaz") { x = (p.X + WorldData.CampTentPad.x + WorldData.Camp.x) / 3; z = (p.Z + WorldData.CampTentPad.z + WorldData.Camp.z) / 3; } // the whole 31 Jan camp
            var target = new Vector3(x, TerrainBuilder.Height(dem, x, z) + 1f, z);
            var pos = target + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            pos.y = Mathf.Max(TerrainBuilder.Height(dem, pos.x, pos.z) + 1.6f, target.y + r * .35f);
            cam.transform.position = pos;
            cam.transform.LookAt(target);
            return true;
        }

        public static bool ToggleArchive()
        {
            if (Archive == null) return false;
            Archive.SetActive(!Archive.activeSelf);
            return Archive.activeSelf;
        }
    }
}
