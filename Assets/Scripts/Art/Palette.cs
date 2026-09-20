using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Height1079.Art
{
    /// <summary>The one box of colours of the game (docs/ART.md §3, docs/art/palette.json): every flat colour on a
    /// model, a stand-in or a piece of the yard comes from here by name, so that a tent, a rock and a tin can read as
    /// one drawing. The file is the source of truth for the artist and for the code alike; the editor copies it into
    /// Resources/Art so the player has it too, and a checkout without either still has the table below.
    ///
    /// Nearest colour is judged in Oklab, a colour space where the distance between two colours is roughly the
    /// distance the eye sees, so a dark olive snaps to the quilted jacket and not to the bark.</summary>
    public static class Palette
    {
        public struct Swatch
        {
            public string Group, Name, Label, Hex;
            public Color Colour;
            public override string ToString() => Name + " " + Hex;
        }

        // ── the names used by the code, so a typo is a compile error and not a magenta prop ──
        public const string SnowLit = "Снег на свету", SnowShade = "Снег в тени", SnowFar = "Снег вдали, ночь", SkyNight = "Небо, ночь";
        public const string NeedlesShade = "Хвоя в тени", NeedlesLit = "Хвоя на свету", Bark = "Кора", Birch = "Берёза", Stone = "Камень", Lichen = "Лишайник";
        public const string Skin = "Кожа", Quilt = "Ватник", Anorak = "Штормовка", Canvas = "Брезент: палатка, рюкзак", Felt = "Валенки, кожа обуви";
        public const string Wood = "Дерево: лыжи, дрова", Metal = "Металл", Enamel = "Эмаль, жесть";
        public const string Red = "Красный", Blue = "Синий", Yellow = "Жёлтый";
        public const string FireCore = "Огонь, ядро", FireEdge = "Огонь, край", TorchLight = "Свет фонаря", Black = "Чёрный: уголь, Менк", MenkEyes = "Глаза Менка";

        /// <summary>Where the editor puts the copy the player reads (Resources.Load path, no extension).</summary>
        public const string ResourcePath = "Art/palette";
        /// <summary>The source of truth, relative to the project root.</summary>
        public const string SourcePath = "docs/art/palette.json";
        /// <summary>The override a built player can be given without rebuilding it: a sheet in the loose files beside
        /// the app (<c>StreamingAssets/sandbox/palette.json</c>; on macOS inside
        /// <c>1079-sandbox.app/Contents/Resources/Data/StreamingAssets/sandbox/</c>). Put one there, restart, and every
        /// colour the game <em>applies at run time</em> comes from it. What it cannot touch is colour baked into mesh
        /// vertices by the editor's import (docs/ART.md §9): those need a re-bake in Unity.</summary>
        public const string OverrideFolder = "sandbox", OverrideFile = "palette.json";
        public static string OverridePath => Path.Combine(Application.streamingAssetsPath, OverrideFolder, OverrideFile);

        static List<Swatch> all;
        static Dictionary<string, int> byName;
        static readonly Dictionary<string, Material> flats = new Dictionary<string, Material>();

        public static IReadOnlyList<Swatch> All { get { Load(); return all; } }

        public static bool TryGet(string name, out Color colour)
        {
            Load();
            if (name != null && byName.TryGetValue(name, out int i)) { colour = all[i].Colour; return true; }
            colour = Color.magenta;
            return false;
        }

        /// <summary>The colour by its name in the sheet; magenta, and a warning, for a name the sheet does not have.</summary>
        public static Color Get(string name)
        {
            if (TryGet(name, out var c)) return c;
            Debug.LogWarning("1079 palette: нет цвета «" + name + "»");
            return Color.magenta;
        }

        public static bool Has(string name) { Load(); return name != null && byName.ContainsKey(name); }

        /// <summary>The name of the sheet's colour nearest to this one, by eye (Oklab).</summary>
        public static string NearestName(Color c)
        {
            Load();
            var lab = Oklab(c);
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                var d = lab - Oklab(all[i].Colour);
                float dd = d.sqrMagnitude;
                if (dd < bestD) { bestD = dd; best = i; }
            }
            return all[best].Name;
        }

        public static Color Nearest(Color c) => Get(NearestName(c));

        /// <summary>A flat lit material in this colour (Standard, no texture, no shine), one per colour, for the
        /// pieces made of primitives at run time — the yard's ground, a stand-in, a part of a site repainted.</summary>
        public static Material Flat(string name)
        {
            if (flats.TryGetValue(name, out var m) && m != null) return m;
            var shader = Shader.Find("Standard");
            m = new Material(shader) { name = "Palette " + name, color = Get(name) };
            m.SetFloat("_Glossiness", .05f);
            m.SetFloat("_Metallic", 0f);
            flats[name] = m;
            return m;
        }

        /// <summary>The name every material of the palette carries: the one shared material the imported set is drawn
        /// with (<c>Palette</c>, the colour in the vertices, baked by the editor) and the flat ones made at run time by
        /// <see cref="Flat"/> (<c>Palette «имя цвета»</c>). The small map's check at start-up reads this and nothing
        /// else: a renderer painted with anything else is paint from somewhere else, and on that map there is nowhere
        /// else (docs/SANDBOX.md §12).</summary>
        public const string MaterialName = "Palette";

        /// <summary>True when this material is the palette's — the shared vertex-colour one or a flat colour of the
        /// sheet. Unity adds " (Instance)" to a material it had to instance, so that suffix is ignored.</summary>
        public static bool IsPalette(Material m)
        {
            if (m == null) return false;
            string n = m.name;
            int copy = n.IndexOf(" (Instance)", StringComparison.Ordinal);
            if (copy >= 0) n = n.Substring(0, copy);
            return n == MaterialName || n.StartsWith(MaterialName + " ", StringComparison.Ordinal);
        }

        /// <summary>Repaints every renderer under <paramref name="root"/> in palette colours: a part whose name starts
        /// with a key of <paramref name="map"/> takes that colour (null keeps what it has); any other untextured
        /// material snaps to the nearest colour of the sheet; a textured material — a scan, a decal — is left alone,
        /// because its look is in the picture and not in the tint.</summary>
        public static void Recolour(GameObject root, params (string prefix, string colour)[] map)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                string forced = null; bool keep = false;
                foreach (var (prefix, colour) in map)
                {
                    if (!r.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    if (colour == null) keep = true; else forced = colour;
                    break;
                }
                if (keep) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    if (forced != null) { mats[i] = Flat(forced); changed = true; continue; }
                    if (m.HasProperty("_MainTex") && m.mainTexture != null) continue;
                    if (!m.HasProperty("_Color")) continue;
                    mats[i] = Flat(NearestName(m.color)); changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        // ── loading ──

        [Serializable] class SheetFile { public List<Group> groups; }
        [Serializable] class Group { public string group; public List<Entry> colours; }
        [Serializable] class Entry { public string name, label, hex; }

        static void Load()
        {
            if (all != null) return;
            all = new List<Swatch>();
            // A sheet dropped beside a built player wins over everything: this is the one colour knob that needs no
            // rebuild. Desktop only, which is every platform this game is built for. A sheet that will not parse is a
            // warning and the next source down, so a typo in it cannot leave the game without colours.
            Sheet(ReadOverride(), OverridePath);
            if (all.Count == 0)
            {
                var asset = Resources.Load<TextAsset>(ResourcePath);
                if (asset != null && !string.IsNullOrEmpty(asset.text)) Sheet(asset.text, "Resources/" + ResourcePath);
            }
            if (all.Count == 0)
            {
                try
                {
                    string file = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", SourcePath);
                    if (File.Exists(file)) Sheet(File.ReadAllText(file), SourcePath);
                }
                catch (Exception) { }
            }
            if (all.Count == 0)
            {
                Debug.LogWarning("1079 palette: docs/art/palette.json не найден, взята встроенная копия");
                foreach (var (group, name, hex) in Builtin)
                    if (ColorUtility.TryParseHtmlString(hex, out var c)) all.Add(new Swatch { Group = group, Name = name, Label = name, Hex = hex, Colour = c });
            }
            byName = new Dictionary<string, int>();
            for (int i = 0; i < all.Count; i++) byName[all[i].Name] = i;
        }

        /// <summary>The override sheet beside the player, or null when there is none.</summary>
        static string ReadOverride()
        {
            try { return File.Exists(OverridePath) ? File.ReadAllText(OverridePath) : null; }
            catch (Exception e) { Debug.LogWarning("1079 palette: " + OverridePath + " не прочитан — " + e.Message); return null; }
        }

        /// <summary>Parses one sheet into <see cref="all"/>; a sheet that will not parse leaves it empty and says so,
        /// and the caller goes on to the next source.</summary>
        static void Sheet(string json, string from)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                var sheet = JsonUtility.FromJson<SheetFile>("{\"groups\":" + json + "}");
                if (sheet?.groups != null)
                    foreach (var g in sheet.groups)
                        if (g.colours != null)
                            foreach (var e in g.colours)
                                if (ColorUtility.TryParseHtmlString(e.hex, out var c))
                                    all.Add(new Swatch { Group = g.group, Name = e.name, Label = e.label, Hex = e.hex.ToUpperInvariant(), Colour = c });
            }
            catch (Exception ex) { Debug.LogWarning("1079 palette: " + from + " не прочитан — " + ex.Message); all.Clear(); }
            if (all.Count > 0) Debug.Log("1079 palette: цвета из " + from + " (" + all.Count + ")");
        }

        /// <summary>Forget what was loaded (the editor calls it after rewriting the sheet).</summary>
        public static void Reload() { all = null; byName = null; flats.Clear(); }

        /// <summary>The sheet as of 20.09.2026, for a checkout with no docs folder (a player built without the copy).</summary>
        static readonly (string group, string name, string hex)[] Builtin =
        {
            ("Снег и небо", SnowLit, "#EEF3FA"), ("Снег и небо", SnowShade, "#9FB4D6"), ("Снег и небо", SnowFar, "#4F6390"),
            ("Снег и небо", SkyNight, "#0F1730"), ("Снег и небо", "Небо, день (Эльбрус)", "#79AEE6"),
            ("Лес и камень", NeedlesShade, "#1C3630"), ("Лес и камень", NeedlesLit, "#3A6A52"), ("Лес и камень", Bark, "#4A3A32"),
            ("Лес и камень", Birch, "#E8E4DA"), ("Лес и камень", Stone, "#6C737F"), ("Лес и камень", Lichen, "#97A06A"),
            ("Люди и снаряжение", Skin, "#E8B894"), ("Люди и снаряжение", Quilt, "#4E4A3F"), ("Люди и снаряжение", Anorak, "#6B774C"),
            ("Люди и снаряжение", Canvas, "#7A8460"), ("Люди и снаряжение", Felt, "#5A4635"), ("Люди и снаряжение", Wood, "#A9773F"),
            ("Люди и снаряжение", Metal, "#8A8F96"), ("Люди и снаряжение", Enamel, "#D9D4C5"),
            ("Акценты игроков: шапки, шарфы, варежки", Red, "#C43C2E"), ("Акценты игроков: шапки, шарфы, варежки", Blue, "#2F6FB5"), ("Акценты игроков: шапки, шарфы, варежки", Yellow, "#E3B23C"),
            ("Свет, огонь и тьма", FireCore, "#FFC64A"), ("Свет, огонь и тьма", FireEdge, "#FF7A1F"), ("Свет, огонь и тьма", TorchLight, "#FFE8B2"),
            ("Свет, огонь и тьма", Black, "#17140F"), ("Свет, огонь и тьма", MenkEyes, "#C9D37E"),
        };

        // ── Oklab (Björn Ottosson, 2020): sRGB → linear → LMS → cube root → Lab ──

        static float Lin(float c) => c <= .04045f ? c / 12.92f : Mathf.Pow((c + .055f) / 1.055f, 2.4f);

        public static Vector3 Oklab(Color c)
        {
            float r = Lin(Mathf.Clamp01(c.r)), g = Lin(Mathf.Clamp01(c.g)), b = Lin(Mathf.Clamp01(c.b));
            float l = Mathf.Pow(.4122214708f * r + .5363325363f * g + .0514459929f * b, 1f / 3f);
            float m = Mathf.Pow(.2119034982f * r + .6806995451f * g + .1073969566f * b, 1f / 3f);
            float s = Mathf.Pow(.0883024619f * r + .2817188376f * g + .6299787005f * b, 1f / 3f);
            return new Vector3(
                .2104542553f * l + .7936177850f * m - .0040720468f * s,
                1.9779984951f * l - 2.4285922050f * m + .4505937099f * s,
                .0259040371f * l + .7827717662f * m - .8086757660f * s);
        }
    }
}
