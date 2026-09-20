using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Core;
using Height1079.Runtime;

namespace Height1079.EditorTools.World
{
    /// <summary>Writes the index the game uses to reach the generated world outside Resources (see <see cref="WorldKit"/>).
    ///
    /// Only assets the game loads BY NAME get an entry. Meshes are deliberately skipped — every one of them hangs off a
    /// prefab that is indexed, and Unity ships a prefab's dependencies with it, so indexing 1 600 track chunks would
    /// bloat the asset for nothing. The sky is not here at all: it is shared code now (Assets/Scripts/Night) that the
    /// small map draws too, so <c>SkyFactory</c> writes it into Resources, where a player with no kit still finds it.</summary>
    public static class WorldKitBuilder
    {
        public const string AssetPath = WorldPaths.Kit + "/WorldKit.asset";

        /// <summary>Folders under the generated tree whose contents are loaded by name. A folder listed here is walked
        /// recursively; anything else in the tree is reached through a prefab and needs no entry.</summary>
        static readonly string[] Indexed = { "Prefabs", "Materials", "TerrainLayers" };

        /// <summary>Loose files at the root of the generated tree that the game asks for by name: every registered
        /// map's terrain and its height raster, and nothing else. Read off <see cref="Locations.Available"/> rather
        /// than written down, so a map that is not in this build is never indexed and therefore never shipped.</summary>
        static IEnumerable<string> Roots()
        {
            foreach (var place in Locations.Available)
            {
                yield return place.TerrainAsset;
                yield return place.HeightAsset;
            }
        }

        /// <summary>Extensions the pipeline writes. ".meta" and anything else is ignored.</summary>
        static readonly HashSet<string> Known = new HashSet<string> { ".prefab", ".asset", ".mat", ".bytes", ".png", ".jpg", ".ttf", ".otf" };

        /// <summary>Where these assets used to be written, under Resources. A checkout generated before the split still
        /// has them there, and Unity packs all of Resources into every player — so leaving them behind would keep the
        /// sandbox exactly as heavy as before while the game quietly loaded the copies in the kit.</summary>
        static readonly string[] Moved =
        {
            "Kholat.asset", "Elbrus.asset", "height_2049.bytes", "elbrus_height_2049.bytes",
            "Prefabs/Tracks", "Prefabs/Elbrus", "Prefabs/Gear",
            "Meshes/Tracks", "Meshes/Elbrus", "Meshes/Valley", "Meshes/Ascent", "Meshes/Lodge", "Meshes/Gear",
        };

        /// <summary>Drops the old copies once the kit has the asset. Everything here is generated and git-ignored:
        /// the worst a wrong delete can do is cost one rebuild from the 1079 menu.</summary>
        static void PruneMovedOutput()
        {
            int gone = 0;
            foreach (var rel in Moved)
            {
                string stale = WorldPaths.Generated + "/" + rel;
                if (!File.Exists(stale) && !Directory.Exists(stale)) continue;
                // only when the replacement is really there, so a half-run pipeline does not leave the game with neither
                string fresh = WorldPaths.Kit + "/" + rel;
                if (!File.Exists(fresh) && !Directory.Exists(fresh)) { Debug.LogWarning("1079: " + rel + " не перенесён в " + WorldPaths.Kit + ", старая копия оставлена"); continue; }
                if (AssetDatabase.DeleteAsset(stale)) gone++;
                else Debug.LogWarning("1079: не удалось удалить " + stale);
            }
            if (gone > 0) Debug.Log($"1079: из Resources убрано {gone} перенесённых в {WorldPaths.Kit} — песочница больше их не несёт.");
        }

#if HEIGHT1079_NO_ELBRUS
        /// <summary>Output of a map this build was compiled without. It is the one place in <c>Height1079.Editor</c>
        /// that still spells the name, and it has to be: the map's own assembly is gone, so there is nobody left to
        /// say what belongs to it, and what is left on disk from an earlier build with the location on would
        /// otherwise ship — a hundred and thirty-six megabytes of mountain nothing in the player can reach.
        ///
        /// Two trees, two reasons. Under <see cref="WorldPaths.Kit"/> it is enough not to index it, because the
        /// player carries only what the index names. Under <see cref="WorldPaths.Generated"/> (Assets/Resources) it
        /// has to go from disk, because Unity packs all of Resources into every player whatever references it
        /// (CLAUDE.md §4).</summary>
        static readonly string[] DisabledInKit =
        {
            "Elbrus.asset", "elbrus_height_2049.bytes",
            "Prefabs/Elbrus", "Meshes/Elbrus", "Meshes/Valley", "Meshes/Ascent", "Meshes/Lodge",
        };

        /// <summary>Prefixes inside the indexed folders: Materials/Elb*, TerrainLayers/Elb*.</summary>
        static readonly string[] DisabledPrefixes = { "Materials/Elb", "TerrainLayers/Elb" };

        /// <summary>And in Resources, where being on disk is enough to be shipped: the map sheet and the layers.</summary>
        static readonly string[] DisabledInResources =
        {
            "Elbrus.asset", "elbrus_height_2049.bytes", "Prefabs/Elbrus",
            "Meshes/Elbrus", "Meshes/Valley", "Meshes/Ascent", "Meshes/Lodge",
            "Textures/elbrus_map.png", "Textures/elb_rock.png", "Textures/elb_scree.png",
        };

        static bool Disabled(string path)
        {
            string key = path.Substring(WorldPaths.Kit.Length + 1);
            foreach (var p in DisabledPrefixes) if (key.StartsWith(p, System.StringComparison.Ordinal)) return true;
            foreach (var p in DisabledInKit) if (key.StartsWith(p, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Takes the disabled map's output off disk. Everything here is generated and git-ignored: the worst
        /// a wrong delete can do is cost one rebuild from the 1079 menu.</summary>
        static void PruneDisabledMaps()
        {
            int gone = 0;
            foreach (var rel in DisabledInKit) gone += Drop(WorldPaths.Kit + "/" + rel);
            foreach (var rel in DisabledInResources) gone += Drop(WorldPaths.Generated + "/" + rel);
            foreach (var folder in new[] { "Materials", "TerrainLayers" })
                foreach (var root in new[] { WorldPaths.Kit, WorldPaths.Generated })
                {
                    string dir = root + "/" + folder;
                    if (!Directory.Exists(dir)) continue;
                    foreach (var file in Directory.GetFiles(dir, "Elb*"))
                        if (!file.EndsWith(".meta", System.StringComparison.Ordinal)) gone += Drop(file.Replace('\\', '/'));
                }
            if (gone > 0) Debug.Log($"1079: сборка без Эльбруса — убрано {gone} файлов и папок локации, в плеер они не поедут.");
        }

        static int Drop(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return 0;
            if (AssetDatabase.DeleteAsset(path)) return 1;
            Debug.LogWarning("1079: не удалось удалить " + path);
            return 0;
        }
#else
        /// <summary>Every map this build knows about is in it, so nothing is skipped and nothing is pruned.</summary>
        static bool Disabled(string path) => false;
        static void PruneDisabledMaps() { }
#endif

        public static WorldKit Build()
        {
            Directory.CreateDirectory(WorldPaths.Kit);
            // CreateAsset throws into a folder the AssetDatabase has never seen, and on a fresh clone Assets/Generated
            // is exactly that — it is born on disk a moment earlier, in WorldImporter.
            if (!AssetDatabase.IsValidFolder(WorldPaths.Kit)) AssetDatabase.Refresh();
            PruneMovedOutput();
            var kit = AssetDatabase.LoadAssetAtPath<WorldKit>(AssetPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<WorldKit>();
                AssetDatabase.CreateAsset(kit, AssetPath);
            }

            PruneDisabledMaps();
            var entries = new List<WorldKit.Entry>();
            var seen = new HashSet<string>();
            foreach (var name in Roots()) Add(entries, seen, WorldPaths.Kit + "/" + name);
            foreach (var folder in Indexed)
            {
                string dir = WorldPaths.Kit + "/" + folder;
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    string path = file.Replace('\\', '/');
                    if (!Known.Contains(Path.GetExtension(path))) continue;   // .meta and strays
                    if (Disabled(path)) continue;
                    Add(entries, seen, Path.ChangeExtension(path, null));
                }
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            kit.Entries = entries.ToArray();
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssetIfDirty(kit);
            Debug.Log($"1079: WorldKit — {entries.Count} записей из {WorldPaths.Kit}");
            return kit;
        }

        /// <summary>Files an asset under its path relative to the generated tree, without extension. <paramref name="stem"/>
        /// is that full path minus the extension; the real file is whichever known extension exists next to it.</summary>
        static void Add(List<WorldKit.Entry> entries, HashSet<string> seen, string stem)
        {
            string dir = Path.GetDirectoryName(stem), name = Path.GetFileName(stem);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            foreach (var file in Directory.GetFiles(dir, name + ".*"))
            {
                string path = file.Replace('\\', '/');
                if (!Known.Contains(Path.GetExtension(path))) continue;
                string key = stem.Substring(WorldPaths.Kit.Length + 1);
                if (!seen.Add(key)) return;
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset == null) { Debug.LogWarning("1079 WorldKit: не читается " + path); return; }
                entries.Add(new WorldKit.Entry { Path = key, Asset = asset });
                return;
            }
        }
    }
}
