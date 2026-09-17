using System.IO;
using UnityEditor;
using UnityEngine;

namespace Height1079.EditorTools.World
{
    /// <summary>Hand items of the period: an Adrianov-type compass (the case file lists three compasses, model not named — assumption),
    /// a Chinese tube flashlight (Dyatlov's, found on the tent roof; exact model unknown — stylised), and the tracing-paper route map.
    /// Prefabs go to Resources/World/Prefabs/Items, the map and fonts are copied to Resources/World.</summary>
    public static class ItemsFactory
    {
        const string PrefabDir = WorldPaths.Generated + "/Prefabs/Items";
        const string MeshDir = WorldPaths.Generated + "/Meshes/Items";

        public static void Build()
        {
            Directory.CreateDirectory(PrefabDir); Directory.CreateDirectory(MeshDir);
            CopyArt("Assets/Art/Maps/kalka_1959.png", WorldPaths.Generated + "/Textures/kalka_1959.png", imp => { imp.maxTextureSize = 2048; imp.mipmapEnabled = true; imp.anisoLevel = 4; imp.wrapMode = TextureWrapMode.Clamp; });
            Directory.CreateDirectory(WorldPaths.Generated + "/Fonts");
            foreach (var f in new[] { "Caveat-400", "Caveat-700" }) CopyAsset($"Assets/Art/Fonts/{f}.ttf", $"{WorldPaths.Generated}/Fonts/{f}.ttf");
            Compass();
            Flashlight();
        }

        static void CopyAsset(string src, string dst)
        {
            if (!File.Exists(src)) { Debug.LogWarning("1079 items: missing " + src); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
        }

        static void CopyArt(string src, string dst, System.Action<TextureImporter> setup)
        {
            CopyAsset(src, dst);
            if (AssetImporter.GetAtPath(dst) is TextureImporter imp) { setup(imp); imp.SaveAndReimport(); }
        }

        static GameObject Part(Transform parent, string name, MeshBuilder mb, params Material[] mats)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var mesh = mb.ToMesh(parent.root.name + "_" + name);
            string path = $"{MeshDir}/{parent.root.name}_{name}.asset";
            AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Material Emissive(string name, Color baseColor, Color emission)
        {
            var m = Materials.Get(name, baseColor, smoothness: .6f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Dial: white card, degree ring with ticks every 3° (Adrianov inner scale), long ticks every 15°, luminous dots at 0/90/180/270.</summary>
        static Texture2D Dial()
        {
            const int S = 512;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var ink = new Color(.1f, .1f, .12f, 1);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + .5f) / S * 2 - 1, v = (y + .5f) / S * 2 - 1;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float ang = (Mathf.Atan2(u, v) * Mathf.Rad2Deg + 360f) % 360f; // 0 at +v, clockwise
                    Color c = r > .97f ? new Color(.12f, .1f, .09f) : new Color(.93f, .91f, .84f);
                    float tickW = .35f / Mathf.Max(r, .1f);
                    float d3 = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 3f) * 3f));
                    float d15 = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 15f) * 15f));
                    if (r > .84f && r < .95f && d3 < tickW) c = ink;
                    if (r > .74f && r < .95f && d15 < tickW * 1.3f) c = ink;
                    if (r > .70f && r < .72f) c = ink;
                    float d90 = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 90f) * 90f));
                    if (r > .56f && r < .66f && d90 * r * .0175f < .03f) c = new Color(.72f, .95f, .62f); // luminous marks
                    if (r < .05f) c = ink;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return TextureFactory.Save("compass_dial", t, false, TextureWrapMode.Clamp);
        }

        static void Label(Transform parent, string text, Vector3 pos, float size, Color color)
        {
            var go = new GameObject("Label_" + text, typeof(TextMesh));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var tm = go.GetComponent<TextMesh>();
            tm.text = text; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.fontSize = 64; tm.characterSize = size; tm.color = color;
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
        }

        /// <summary>Compass, pivot at the case centre, +Y = face, +Z = sight direction (the "0" of the ring).</summary>
        static void Compass()
        {
            var root = new GameObject("Compass");
            var t = root.transform;
            var bakelite = Materials.Get("Bakelite", new Color(.09f, .07f, .06f), smoothness: .55f);
            var dialMat = Materials.Get("CompassDial", Color.white, Dial(), null, .35f);
            var lume = Emissive("Luminous", new Color(.75f, .92f, .66f), new Color(.25f, .45f, .2f));
            var steel = Materials.Metal;

            const float R = .024f, H = .012f;
            var body = new MeshBuilder(1);
            body.Tube(0, new Vector3(0, -H / 2, 0), new Vector3(0, H / 2 - .002f, 0), R, R * .98f, 32, 20f);
            body.Tube(0, new Vector3(0, H / 2 - .002f, 0), new Vector3(0, -H / 2, 0), R * .97f, R * .99f, 32, 20f, 0, true); // bottom cap
            // rotating ring with sight (rear notch + front sight)
            body.Tube(0, new Vector3(0, H / 2 - .003f, 0), new Vector3(0, H / 2 + .0015f, 0), R + .002f, R + .002f, 32, 20f);
            body.Box(0, new Vector3(0, H / 2 + .003f, R - .001f), new Vector3(.002f, .006f, .004f), Quaternion.identity);
            body.Box(0, new Vector3(-.0025f, H / 2 + .003f, -R + .001f), new Vector3(.0015f, .006f, .004f), Quaternion.identity);
            body.Box(0, new Vector3(.0025f, H / 2 + .003f, -R + .001f), new Vector3(.0015f, .006f, .004f), Quaternion.identity);
            // arretir lever and strap lug
            body.Box(0, new Vector3(R + .002f, 0, -.006f), new Vector3(.004f, .003f, .01f), Quaternion.identity);
            body.Box(0, new Vector3(0, -.002f, -R - .004f), new Vector3(.014f, .004f, .008f), Quaternion.identity);
            Part(t, "Case", body, bakelite);

            var card = new MeshBuilder(1);
            card.Tube(0, new Vector3(0, H / 2 - .0035f, 0), new Vector3(0, H / 2 - .003f, 0), R * .92f, R * .92f, 48, 1f, 0, true);
            var cardGo = Part(t, "Card", card, dialMat);
            float ly = H / 2 - .0026f;
            Label(cardGo.transform, "С", new Vector3(0, ly, R * .5f), .0011f, new Color(.55f, .1f, .08f));
            Label(cardGo.transform, "В", new Vector3(R * .5f, ly, 0), .0009f, new Color(.1f, .1f, .12f));
            Label(cardGo.transform, "Ю", new Vector3(0, ly, -R * .5f), .0009f, new Color(.1f, .1f, .12f));
            Label(cardGo.transform, "З", new Vector3(-R * .5f, ly, 0), .0009f, new Color(.1f, .1f, .12f));

            // needle: luminous north half, dark south half, on a pivot
            var needle = new GameObject("Needle").transform;
            needle.SetParent(t, false); needle.localPosition = new Vector3(0, H / 2 - .0015f, 0);
            var north = new MeshBuilder(1); var south = new MeshBuilder(1);
            float L = R * .8f, W = .0024f;
            north.Quad(0, new Vector3(-W, 0, 0), new Vector3(0, 0, L), new Vector3(W, 0, 0), new Vector3(0, 0, 0), Vector2.zero, Vector2.up, Vector2.one, Vector2.right, true, Vector3.up);
            south.Quad(0, new Vector3(W, 0, 0), new Vector3(0, 0, -L), new Vector3(-W, 0, 0), new Vector3(0, 0, 0), Vector2.zero, Vector2.up, Vector2.one, Vector2.right, true, Vector3.up);
            Part(needle, "North", north, lume);
            Part(needle, "South", south, Materials.Get("NeedleDark", new Color(.15f, .16f, .2f), smoothness: .6f));
            var pin = new MeshBuilder(1);
            pin.Tube(0, new Vector3(0, -.001f, 0), new Vector3(0, .0012f, 0), .0014f, .0012f, 10, 1f, 0, true);
            Part(needle, "Pivot", pin, steel);

            var glass = new MeshBuilder(1);
            glass.Tube(0, new Vector3(0, H / 2 + .0004f, 0), new Vector3(0, H / 2 + .0006f, 0), R * .96f, R * .96f, 32, 1f, 0, true);
            var glassMat = Materials.Get("CompassGlass", new Color(.85f, .9f, .95f, .12f), smoothness: .95f);
            glassMat.SetFloat("_Mode", 3); glassMat.SetOverrideTag("RenderType", "Transparent");
            glassMat.SetInt("_SrcBlend", 1); glassMat.SetInt("_DstBlend", 10); glassMat.SetInt("_ZWrite", 0);
            glassMat.EnableKeyword("_ALPHAPREMULTIPLY_ON"); glassMat.renderQueue = 3000; EditorUtility.SetDirty(glassMat);
            Part(t, "Glass", glass, glassMat);

            // leather strap loop
            var strap = new MeshBuilder(1);
            strap.Box(0, new Vector3(0, -.004f, -R - .03f), new Vector3(.01f, .0015f, .05f), Quaternion.Euler(8, 0, 0));
            Part(t, "Strap", strap, Materials.Cloth("strap", new Color(.3f, .2f, .12f)));

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Compass.prefab");
            Object.DestroyImmediate(root);
        }

        /// <summary>Tube flashlight for two round cells, nickel body with a green enamel band; pivot at the grip, +Z = beam.
        /// Child "Lens" marks where the spot light goes.</summary>
        static void Flashlight()
        {
            var root = new GameObject("Flashlight");
            var t = root.transform;
            var nickel = Materials.Get("Nickel", new Color(.72f, .72f, .7f), smoothness: .78f);
            nickel.SetFloat("_Metallic", .85f); EditorUtility.SetDirty(nickel);
            var enamel = Materials.Get("GreenEnamel", new Color(.16f, .3f, .2f), smoothness: .55f);
            var lensMat = Emissive("FlashlightLens", new Color(.95f, .93f, .85f), Color.black);

            var body = new MeshBuilder(1);
            body.Tube(0, new Vector3(0, 0, -.10f), new Vector3(0, 0, .06f), .0165f, .0165f, 20, 30f);
            body.Tube(0, new Vector3(0, 0, .06f), new Vector3(0, 0, .095f), .0165f, .026f, 20, 30f);
            body.Tube(0, new Vector3(0, 0, .095f), new Vector3(0, 0, .105f), .027f, .027f, 20, 30f);
            body.Tube(0, new Vector3(0, 0, -.10f), new Vector3(0, 0, -.108f), .0165f, .014f, 20, 30f, 0, true);
            // lanyard ring on the tail
            body.Tube(0, new Vector3(0, .012f, -.108f), new Vector3(0, .012f, -.112f), .006f, .006f, 10, 30f);
            Part(t, "Body", body, nickel);
            var band = new MeshBuilder(1);
            band.Tube(0, new Vector3(0, 0, -.06f), new Vector3(0, 0, .02f), .0168f, .0168f, 20, 30f);
            Part(t, "Band", band, enamel);
            var sw = new MeshBuilder(1);
            sw.Box(0, new Vector3(0, .0175f, .035f), new Vector3(.007f, .004f, .018f), Quaternion.identity);
            Part(t, "Switch", sw, nickel);
            var lens = new MeshBuilder(1);
            lens.Tube(0, new Vector3(0, 0, .100f), new Vector3(0, 0, .104f), .024f, .024f, 24, 1f, 0, true);
            var lensGo = Part(t, "Lens", lens, lensMat);
            var beam = new GameObject("BeamOrigin").transform;
            beam.SetParent(lensGo.transform, false); beam.localPosition = new Vector3(0, 0, .11f);

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Flashlight.prefab");
            Object.DestroyImmediate(root);
        }
    }
}
