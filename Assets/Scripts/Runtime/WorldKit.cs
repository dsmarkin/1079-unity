using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Height1079.Runtime
{
    /// <summary>Index of the generated world that lives OUTSIDE Resources.
    ///
    /// Unity puts every asset under <c>Assets/Resources</c> into every player, whichever scene is built, so the
    /// physics sandbox used to carry the whole mountain — terrain, Elbrus, animal tracks — for a scene that shows
    /// none of it. The heavy, game-only half of the pipeline now writes to <see cref="GeneratedDir"/> instead, and
    /// this asset names what the game loads by name. The Main scene holds a <see cref="WorldKitHolder"/> pointing
    /// here, so Unity ships those assets with the game scene and with nothing else.
    ///
    /// Only assets loaded BY NAME need an entry: meshes and materials reached through a prefab ride along with it.
    /// The editor fills this in <c>WorldKitBuilder</c>; do not edit it by hand, it is regenerated.</summary>
    public class WorldKit : ScriptableObject
    {
        /// <summary>Where the game-only half of the pipeline writes. Git-ignored, like Assets/Resources.
        /// The editor's <c>WorldPaths.Kit</c> reads this, so the path is written down once.</summary>
        public const string GeneratedDir = "Assets/Generated/World";

        [System.Serializable]
        public struct Entry
        {
            /// <summary>Path under <see cref="GeneratedDir"/> with no extension, e.g. "Prefabs/Elbrus/Tower".
            /// Same string the code used to pass to Resources.Load minus the leading "World/".</summary>
            public string Path;
            public Object Asset;
        }

        [SerializeField] Entry[] entries = new Entry[0];

        public Entry[] Entries
        {
            get => entries;
            set { entries = value ?? new Entry[0]; map = null; }
        }

        Dictionary<string, Object> map;

        /// <summary>The asset filed under this path, or null. Case-sensitive, like Resources.Load.</summary>
        public Object Find(string path)
        {
            if (map == null)
            {
                map = new Dictionary<string, Object>(entries.Length);
                foreach (var e in entries)
                    if (!string.IsNullOrEmpty(e.Path) && e.Asset != null) map[e.Path] = e.Asset;
            }
            return map.TryGetValue(path, out var asset) ? asset : null;
        }
    }

    /// <summary>The one scene object the generated Main scene carries: it is what makes Unity ship the world with
    /// the game scene and not with the sandbox. Created by ProjectSetup — nothing reads it but <see cref="WorldAssets"/>.</summary>
    public class WorldKitHolder : MonoBehaviour
    {
        public WorldKit Kit;
    }

    /// <summary>How the game reaches the generated world. Drop-in for <c>Resources.Load&lt;T&gt;("World/" + path)</c>:
    /// pass the same string without the "World/" prefix.
    ///
    /// Two places are tried in order, so a half-migrated or stale checkout still runs:
    /// 1. the kit the current scene points at — this is the path a built player takes;
    /// 2. <c>Resources/World/…</c> — what the code did before, for anything still generated there.
    /// A miss returns null, exactly as Resources.Load did, and every caller already warns and falls back.
    /// The kit is rebuilt by every path that rebuilds the world, so it cannot go stale behind the assets.</summary>
    public static class WorldAssets
    {
        static WorldKit kit;
        static bool searched;

        /// <summary>The kit the loaded scene carries, or null in a scene that has none (the sandbox).</summary>
        public static WorldKit Kit
        {
            get
            {
                if (kit != null) return kit;
                if (searched) return null;
                searched = true;
                var holder = Object.FindFirstObjectByType<WorldKitHolder>(FindObjectsInactive.Include);
                kit = holder != null ? holder.Kit : null;
                if (kit == null) Debug.LogWarning("1079: в сцене нет WorldKit — мир ищется в Resources. Меню 1079 → Generate prefabs and scene.");
                return kit;
            }
        }

        public static T Load<T>(string path) where T : Object
        {
            var current = Kit;
            // the null check happens while the value is still typed Object: C# does not apply an operator overload to a
            // type parameter, so `T != null` would be a plain reference compare and would miss Unity's destroyed objects
            Object found = current != null ? current.Find(path) : null;
            if (found != null && found is T fromKit) return fromKit;
            return Resources.Load<T>("World/" + path);
        }

        /// <summary>A new scene brings its own kit — coming back from the sandbox, or switching place — so look again.</summary>
        static void Forget(Scene scene, LoadSceneMode mode) { kit = null; searched = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { kit = null; searched = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= Forget;
            SceneManager.sceneLoaded += Forget;
        }
    }
}
