using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Art;

namespace Height1079.EditorTools.World
{
    /// <summary>The palette on the editor side: the copy of the sheet the player reads, and the one shared material
    /// every imported prop is drawn with (Resources/World/Materials/Palette.mat on Height1079/PaletteVertex — the
    /// colour is in the vertices, so one material serves a tent, a torch and a pine, and the shader ships in the
    /// build because this asset references it).</summary>
    public static class PaletteMaterials
    {
        public const string MaterialPath = WorldPaths.Generated + "/Materials/Palette.mat";
        const string ResourceCopy = "Assets/Resources/" + Palette.ResourcePath + ".json";

        /// <summary>docs/art/palette.json → Resources/Art/palette.json, so the build carries the sheet the code reads.</summary>
        public static void CopySheet()
        {
            if (!File.Exists(Palette.SourcePath)) { Debug.LogWarning("1079 palette: нет " + Palette.SourcePath + " — в сборке будет встроенная копия"); return; }
            string text = File.ReadAllText(Palette.SourcePath);
            Directory.CreateDirectory(Path.GetDirectoryName(ResourceCopy));
            if (!File.Exists(ResourceCopy) || File.ReadAllText(ResourceCopy) != text)
            {
                File.WriteAllText(ResourceCopy, text);
                AssetDatabase.ImportAsset(ResourceCopy);
            }
            Palette.Reload();
        }

        /// <summary>The shared material, made if missing, brought back to its settings if not.</summary>
        public static Material Shared()
        {
            var shader = Shader.Find("Height1079/PaletteVertex");
            if (shader == null) { Debug.LogError("1079 palette: нет шейдера Height1079/PaletteVertex (Assets/Art/Shaders)"); shader = Shader.Find("Standard"); }
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool fresh = m == null;
            if (fresh) { Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath)); m = new Material(shader); }
            m.shader = shader;
            m.name = "Palette";
            m.color = Color.white;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", .05f);
            if (m.HasProperty("_Glow")) m.SetFloat("_Glow", 1.2f);
            m.enableInstancing = true;
            if (fresh) AssetDatabase.CreateAsset(m, MaterialPath); else EditorUtility.SetDirty(m);
            return m;
        }
    }
}
