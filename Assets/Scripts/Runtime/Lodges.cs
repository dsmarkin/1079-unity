#if !HEIGHT1079_NO_ELBRUS
// Compiled only when the southern slope of Elbrus is in the build (docs/ELBRUS.md). It has to live in
// Height1079.Runtime and not in the location's own assembly because a partial class cannot be split across
// assemblies, and because the rest of Height1079.Runtime's Elbrus partials name what is in here.
using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The bunks of the southern slope, as objects on the mountain. Every shelter prefab carries one empty
    /// child called <c>Elb_Bunk</c> (docs/ELBRUS.md, <c>ElbrusLodge</c>); this finds them and says which line of
    /// <see cref="Lodging"/>'s price board each one belongs to.
    ///
    /// The prefab's own name decides it where it can — a barrel is a barrel, a capsule is LeapRus, the long hut on the
    /// foundation of Приют 11 is Приют 11 — and the height decides the rest, because the board is written by height
    /// and the mountain is the same height on both machines. Both the host and the owner ask the same question of the
    /// same scene and get the same answer, so nothing about which bunk is which ever needs sending.</summary>
    public static class Lodges
    {
        static readonly List<(Transform at, Bunk bunk)> beds = new List<(Transform, Bunk)>();
        static bool scanned;

        /// <summary>Prefab name → the line of the board it is. Anything not here is matched by height.</summary>
        static readonly Dictionary<string, string> byPrefab = new Dictionary<string, string>
        {
            ["Elb_Barrel"] = "barrels",
            ["Elb_Hut_Natspark"] = "natspark",
            ["Elb_Hut_Capsule"] = "leaprus",
            ["Elb_Hut_Diesel"] = "priut11",
        };

        public static int Count { get { Ensure(); return beds.Count; } }

        public static void Forget() { beds.Clear(); scanned = false; }

        static void Ensure() { if (!scanned) Scan(null); }

        /// <summary>Walks the scene (or one root) for bunk markers. Cheap enough to run once and idempotent.</summary>
        public static void Scan(Transform root)
        {
            beds.Clear();
            scanned = true;
            if (!Climb.On) return;
            var roots = new List<Transform>();
            if (root != null) roots.Add(root);
            else
                foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    roots.Add(go.transform);
            foreach (var r in roots)
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "Elb_Bunk") continue;
                    var bunk = Match(t);
                    if (!bunk.IsEmpty) beds.Add((t, bunk));
                }
        }

        /// <summary>Which line of the board this marker is.</summary>
        static Bunk Match(Transform marker)
        {
            for (var p = marker.parent; p != null; p = p.parent)
            {
                string name = p.name.EndsWith("(Clone)") ? p.name.Substring(0, p.name.Length - 7) : p.name;
                if (byPrefab.TryGetValue(name, out var id)) return Lodging.Get(id);
            }
            // no name we know: take the line of the board closest to the height this bunk actually stands at
            float ele = marker.position.y;
            var best = default(Bunk);
            float bd = float.MaxValue;
            foreach (var b in Lodging.Board())
            {
                float d = Mathf.Abs(b.Ele - ele);
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        /// <summary>The nearest bunk, how far it is, and where it stands — the host wants the place as well as the
        /// price, because that is where the save is written and where the party wakes up.</summary>
        public static Bunk Nearest(Vector3 pos, out float metres, out Vector3 at, out float yaw)
        {
            Ensure();
            metres = float.MaxValue;
            var best = default(Bunk);
            at = pos; yaw = 0f;
            for (int i = beds.Count - 1; i >= 0; i--)
            {
                if (beds[i].at == null) { beds.RemoveAt(i); continue; }
                float d = Vector3.Distance(pos, beds[i].at.position);
                if (d >= metres) continue;
                metres = d; best = beds[i].bunk;
                at = beds[i].at.position; yaw = beds[i].at.eulerAngles.y;
            }
            return best;
        }

        public static Bunk Nearest(Vector3 pos, out float metres) => Nearest(pos, out metres, out _, out _);

        /// <summary>The bunk the player is standing at, or an empty one.</summary>
        public static Bunk Here(Vector3 pos)
        {
            var b = Nearest(pos, out float m);
            return m <= Lodging.DoorReach ? b : default;
        }
    }
}
#endif
