using UnityEditor;
using UnityEngine;

namespace Height1079.EditorTools.World
{
    public static partial class SiteFactory
    {
        /// <summary>The wearable rucksack: the same canvas "колобок" as at the 31 Jan camp, full, without snow.
        /// Pivot = bottom centre, +Z = the straps (toward the wearer's back). Saved as Resources/World/Prefabs/Items/Rucksack.</summary>
        public static void BuildRucksack(string prefabDir)
        {
            var root = new GameObject("Rucksack");
            var k = new Kit();
            Rucksack(k, Matrix4x4.identity, .9f, 0f, 500, RuckKhaki);
            k.Flush(root.transform);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            System.IO.Directory.CreateDirectory(prefabDir);
            PrefabUtility.SaveAsPrefabAsset(root, $"{prefabDir}/Rucksack.prefab");
            Object.DestroyImmediate(root);
        }
    }
}
