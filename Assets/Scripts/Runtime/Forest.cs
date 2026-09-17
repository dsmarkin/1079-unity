using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Where the trunks are. Terrain trees have colliders, but a ray does not identify the tree behind them, so the trunks are read
    /// straight out of the terrain data once and kept in a 24 m grid: cheap to ask "which tree am I facing" on both the client and the host.
    /// Only prototypes with a trunk collider count as trees; the understory (bushes, deadwood, hummocks) has none.</summary>
    public static class Forest
    {
        const float Cell = 24f;
        static Dictionary<(int, int), List<Vector3>> grid;
        static readonly List<Vector3> empty = new List<Vector3>();

        static (int, int) Key(float x, float z) => (Mathf.FloorToInt(x / Cell), Mathf.FloorToInt(z / Cell));

        static void Build()
        {
            grid = new Dictionary<(int, int), List<Vector3>>();
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null) return;
            var data = terrain.terrainData;
            var protos = data.treePrototypes;
            var isTree = new bool[protos.Length];
            for (int i = 0; i < protos.Length; i++)
                isTree[i] = protos[i].prefab != null && protos[i].prefab.GetComponentInChildren<CapsuleCollider>() != null;
            var origin = terrain.transform.position;
            var size = data.size;
            foreach (var t in data.treeInstances)
            {
                if (t.prototypeIndex >= isTree.Length || !isTree[t.prototypeIndex]) continue;
                var p = new Vector3(origin.x + t.position.x * size.x, 0, origin.z + t.position.z * size.z);
                var key = Key(p.x, p.z);
                if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<Vector3>();
                list.Add(p);
            }
            Debug.Log($"1079: {grid.Count} forest cells with trunks");
        }

        static List<Vector3> At(int cx, int cz) => grid.TryGetValue((cx, cz), out var l) ? l : empty;

        /// <summary>The trunk the hiker is facing: within <paramref name="reach"/> metres of the eye and no more than
        /// <paramref name="halfAngle"/> degrees off the line of sight. Returns the trunk's ground position.</summary>
        public static bool InFront(Vector3 pos, float yaw, out Vector3 trunk, float reach = 3f, float halfAngle = 40f)
        {
            trunk = pos;
            if (grid == null) Build();
            if (grid == null || grid.Count == 0) return false;
            var look = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var flat = new Vector2(pos.x, pos.z);
            float best = reach;
            bool found = false;
            var (cx, cz) = Key(pos.x, pos.z);
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    foreach (var t in At(cx + dx, cz + dz))
                    {
                        var to = new Vector2(t.x, t.z) - flat;
                        float d = to.magnitude;
                        if (d > best || d < 1e-3f) continue;
                        if (Vector2.Angle(to / d, new Vector2(look.x, look.z)) > halfAngle) continue;
                        best = d;
                        trunk = new Vector3(t.x, pos.y, t.z);
                        found = true;
                    }
            return found;
        }

        /// <summary>Forget the grid (a new night rebuilds the terrain).</summary>
        public static void Forget() => grid = null;
    }
}
