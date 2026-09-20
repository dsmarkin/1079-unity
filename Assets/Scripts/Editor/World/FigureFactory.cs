using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Height1079.EditorTools.World
{
    /// <summary>The team's palette, docs/art/palette.json: twenty-seven named colours in five groups (snow and
    /// sky, forest and stone, people and kit, the players' accents, fire and dark). Read here so that a model from
    /// a file can be dressed in it (<see cref="FigureFactory"/>) and nothing on a body is a colour the palette does
    /// not have. The file is the source: this class never carries a colour of its own.</summary>
    public static class Palette
    {
        public const string JsonPath = "docs/art/palette.json";

        [System.Serializable] class Sheet { public Group[] groups; }
        [System.Serializable] class Group { public string group; public Swatch[] colours; }

        [System.Serializable]
        public class Swatch
        {
            public string name, label, hex;
            public Color Color => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        static Swatch[] all;

        public static Swatch[] All
        {
            get { if (all == null || all.Length == 0) Load(); return all; }
        }

        static void Load()
        {
            all = System.Array.Empty<Swatch>();
            if (!File.Exists(JsonPath)) { Debug.LogWarning("1079: нет палитры " + JsonPath); return; }
            // the file is a bare array; JsonUtility wants an object round it
            var sheet = JsonUtility.FromJson<Sheet>("{\"groups\":" + File.ReadAllText(JsonPath) + "}");
            var list = new List<Swatch>();
            if (sheet?.groups != null)
                foreach (var g in sheet.groups)
                    if (g.colours != null) foreach (var s in g.colours) if (!string.IsNullOrEmpty(s.hex)) list.Add(s);
            all = list.ToArray();
        }

        public static Swatch ByName(string name)
        {
            foreach (var s in All) if (s.name == name) return s;
            return null;
        }

        /// <summary>The palette colour closest to <paramref name="srgb"/>, by distance in Lab — the space where a
        /// step looks the same size wherever it is taken, so a dark olive lands on the quilted jacket and not on
        /// black.</summary>
        public static Swatch Nearest(Color srgb)
        {
            Swatch best = null; float bestD = float.MaxValue;
            var want = Lab(srgb);
            foreach (var s in All)
            {
                float d = (Lab(s.Color) - want).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        /// <summary>A Standard material in this colour, flat and matte — the look the style asks for — saved under
        /// <paramref name="dir"/> by its hex so that every part painted in one colour shares one asset, and the
        /// shader variant it needs is in the build.</summary>
        public static Material Material(Swatch s, string dir)
        {
            Directory.CreateDirectory(dir);
            string path = $"{dir}/{s.hex.TrimStart('#')}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool fresh = m == null;
            if (fresh) m = new Material(Shader.Find("Standard"));
            m.color = s.Color;
            m.SetFloat("_Glossiness", .05f);
            m.SetFloat("_Metallic", 0f);
            if (fresh) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            return m;
        }

        static Vector3 Lab(Color c)
        {
            float r = Lin(c.r), g = Lin(c.g), b = Lin(c.b);
            float x = (r * .4124f + g * .3576f + b * .1805f) / .95047f;
            float y = r * .2126f + g * .7152f + b * .0722f;
            float z = (r * .0193f + g * .1192f + b * .9505f) / 1.08883f;
            float fx = F(x), fy = F(y), fz = F(z);
            return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
        }

        static float F(float t) => t > .008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
        static float Lin(float v) => v <= .04045f ? v / 12.92f : Mathf.Pow((v + .055f) / 1.055f, 2.4f);
    }

    /// <summary>A character from a model file, dressed in the palette and saved as a prefab the sandbox can load
    /// by name. The team decided the visible climber comes from a model with a skeleton over the puppet's physics
    /// (docs/ART.md §7, docs/PHYSICS.md §3); this is the asset side of it. The model's own materials are replaced
    /// one for one by flat palette materials: by a table of which part is which colour where the file's material
    /// or mesh names say (jacket, trousers, pack, boots, skin, hair, straps), and by the nearest palette colour to
    /// the file's own where they do not. The rig is untouched — the puppet wears it by bone name
    /// (<c>PuppetSkeleton</c>). The prefab lives in Resources (generated, not in git), so a fresh clone gets it
    /// on the first open, and it is rebuilt on every generation because the table above is code.</summary>
    public static class FigureFactory
    {
        public const string AdventurerFbx = "Assets/Art/ThirdParty/Quaternius/ModularMen/Adventurer.fbx";
        public const string AdventurerPrefab = "Assets/Resources/Sandbox/Adventurer.prefab";
        public const string PaletteDir = "Assets/Resources/Sandbox/Palette";

        /// <summary>A part of the model and the palette colour it is painted: the file's material name, matched
        /// whole and case-blind, and optionally a piece of the mesh's name, because one material can serve two
        /// parts ("Green" is the Adventurer's jacket and his pack).</summary>
        public struct Rule
        {
            public string Mesh, Material, Colour;
            public Rule(string mesh, string material, string colour) { Mesh = mesh; Material = material; Colour = colour; }
        }

        /// <summary>The Adventurer, as a winter hiker: quilted jacket, storm trousers with dark patches, canvas pack
        /// and bedroll with wooden toggles, felt boots with dark soles; skin, hair and beard. The model's shirt has
        /// rolled sleeves — the "Skin" of its body mesh is the bare forearms and the hands — and they are painted as
        /// jacket, which reads as long sleeves and gloves. Anything not listed falls to the nearest colour.</summary>
        static readonly Rule[] Adventurer =
        {
            new Rule("Body", "Skin", "Ватник"),
            new Rule(null, "Skin", "Кожа"),
            new Rule(null, "Hair", "Кора"),
            new Rule(null, "Eyebrows", "Кора"),
            new Rule(null, "Eye", "Чёрный: уголь, Менк"),
            new Rule("Body", "Green", "Ватник"),
            new Rule("Body", "LightGreen", "Ватник"),
            new Rule("Legs", "Brown", "Штормовка"),
            new Rule("Legs", "Brown2", "Ватник"),
            new Rule("Feet", "Grey", "Валенки, кожа обуви"),
            new Rule("Feet", "Black", "Чёрный: уголь, Менк"),
            new Rule("Backpack", "Green", "Брезент: палатка, рюкзак"),
            new Rule("Backpack", "LightGreen", "Брезент: палатка, рюкзак"),
            new Rule("Backpack", "Brown", "Брезент: палатка, рюкзак"),
            new Rule("Backpack", "Gold", "Дерево: лыжи, дрова"),
        };

        public static bool BuildAdventurer() => Build(AdventurerFbx, AdventurerPrefab, Adventurer);

        /// <summary>Instantiates the model, repaints every renderer slot from the table, strips whatever could
        /// animate the bones, and saves the result as a plain prefab. The material → colour table goes to the log.</summary>
        public static bool Build(string fbx, string prefab, Rule[] rules)
        {
            var model = AssetDatabase.LoadMainAssetAtPath(fbx) as GameObject;
            if (model == null) { Debug.LogWarning($"1079 figure: нет модели {fbx}"); return false; }
            // the import rules are a postprocessor (below), but a file imported before it existed keeps its old
            // settings until it is imported again
            if (AssetImporter.GetAtPath(fbx) is ModelImporter imp && (imp.animationType != ModelImporterAnimationType.Generic || imp.importAnimation))
            {
                ThirdPartyModelPostprocessor.Apply(imp);
                imp.SaveAndReimport();
                model = AssetDatabase.LoadMainAssetAtPath(fbx) as GameObject;
                if (model == null) return false;
            }
            var root = Object.Instantiate(model);
            root.name = Path.GetFileNameWithoutExtension(prefab);
            var log = new StringBuilder($"1079 figure: {Path.GetFileName(fbx)} → {prefab}");
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string name = mats[i] != null ? mats[i].name : "(none)";
                    var sw = Pick(rules, r.name, name, mats[i], out string how);
                    log.Append($"\n  {r.name} / {name} → {sw.name} {sw.hex} ({how})");
                    mats[i] = Palette.Material(sw, PaletteDir);
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var a in root.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (var a in root.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(a);
            Directory.CreateDirectory(Path.GetDirectoryName(prefab));
            PrefabUtility.SaveAsPrefabAsset(root, prefab);
            Object.DestroyImmediate(root);
            Debug.Log(log.ToString());
            return true;
        }

        static Palette.Swatch Pick(Rule[] rules, string mesh, string material, Material src, out string how)
        {
            foreach (var rule in rules)
            {
                if (!string.Equals(rule.Material, material, System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(rule.Mesh) && mesh.IndexOf(rule.Mesh, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var s = Palette.ByName(rule.Colour);
                if (s != null) { how = "по таблице"; return s; }
                Debug.LogWarning("1079 figure: в палитре нет цвета «" + rule.Colour + "»");
            }
            // the file's own colour. Blender writes it linear and the importer copies it as it is, so it is
            // brought back to the sRGB it was painted as before the palette is searched
            var c = src != null && src.HasProperty("_Color") ? src.color : Color.gray;
            var srgb = new Color(Mathf.LinearToGammaSpace(c.r), Mathf.LinearToGammaSpace(c.g), Mathf.LinearToGammaSpace(c.b));
            how = "ближайший к #" + ColorUtility.ToHtmlStringRGB(srgb);
            return Palette.Nearest(srgb);
        }
    }

    /// <summary>Import rules for third-party character models: a rig to be posed by the puppet, not by clips —
    /// no animation, no blend shapes, nothing read back on the CPU. The rig type has to be Generic: with "None"
    /// Unity imports the meshes rigid, in the rest pose, and no bone moves them (the Animator that Generic puts on
    /// the root is stripped by FigureFactory). The studio convention: settings live in a postprocessor, never in
    /// a .meta file (CLAUDE.md §5).</summary>
    public sealed class ThirdPartyModelPostprocessor : AssetPostprocessor
    {
        const string Root = "Assets/Art/ThirdParty/Quaternius/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            Apply((ModelImporter)assetImporter);
        }

        internal static void Apply(ModelImporter imp)
        {
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.optimizeGameObjects = false;            // the bones must stay transforms: the puppet turns them
            imp.importAnimation = false;
            imp.importBlendShapes = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.isReadable = false;
        }
    }
}
