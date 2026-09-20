using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Art;

namespace Height1079.EditorTools.World
{
    /// <summary>Ready-made low-poly models (Assets/Art/ThirdParty: Quaternius, Kenney — CC0) → prefabs in the game's
    /// own colours, Resources/World/Prefabs/Imported/&lt;Name&gt;. Each model is fitted to its real size in metres with the
    /// pivot at its base, and every face is painted with a colour of the palette (docs/art/palette.json): the nearest
    /// colour to what the model came with, or the colour the table below names for that material or part. The colour
    /// is baked into the vertices, so all of them share one material (<see cref="PaletteMaterials.Shared"/>). Faces
    /// that look up take snow where the table says so, which is how a summer pine becomes a winter one.
    ///
    /// The table (<see cref="Table"/>) is the whole recipe: name, file, size, what gets which colour, what collider.
    /// A missing file is a warning and no prefab; whoever uses the prefab falls back to the game's own model, so a
    /// checkout without the packs still runs.</summary>
    public static class ImportedFactory
    {
        public const string PrefabDir = WorldPaths.Generated + "/Prefabs/Imported";
        const string MeshDir = WorldPaths.Generated + "/Meshes/Imported";
        public const string Quaternius = "Assets/Art/ThirdParty/Quaternius";
        public const string Kenney = "Assets/Art/ThirdParty/Kenney/SurvivalKit/Models/GLB format";
        const string Survival = Quaternius + "/SurvivalPack/FBX/", Nature = Quaternius + "/UltimateNature/FBX/", KenneyModels = Kenney + "/";
        const string KenneyColormap = Kenney + "/Textures/colormap.png";

        /// <summary>Which extent the size in metres applies to.</summary>
        public enum Fit { Height, Length }
        public enum Shape { None, Box, Trunk, Convex, Mesh }

        public sealed class Entry
        {
            public string Name, Path;
            public float Size; public Fit Fit;
            public Shape Shape = Shape.Box;
            /// <summary>A model that stands upright in its file (a torch, an axe) is laid down: its long axis goes along +Z.</summary>
            public bool Lay;
            /// <summary>A lying model is turned about Y so its long axis runs along Z.</summary>
            public bool AlignZ;
            /// <summary>Turned about Y at the end, degrees — a doorway that has to face +Z.</summary>
            public float Yaw;
            /// <summary>Stretched after the fit, per axis: a pup tent's proportions made into a long low one.</summary>
            public Vector3 Stretch = Vector3.one;
            /// <summary>Faces whose normal points up more than this take snow; 0 = no snow.</summary>
            public float SnowCap;
            /// <summary>Only faces already in one of these colours take snow (null = any).</summary>
            public string[] SnowOn;
            /// <summary>Faces of the <see cref="SnowOn"/> colours that look down take this colour instead: the underside of a bough is in shadow.</summary>
            public string ShadeUnder;
            /// <summary>Material or part name prefix → palette colour; "*" is everything not named. Missing = nearest colour.</summary>
            public (string match, string colour)[] Colours = new (string, string)[0];
            /// <summary>Only faces of these materials/parts make the collider (null = all of them).</summary>
            public string[] CollideOn;

            public Entry(string name, string path, float size, Fit fit) { Name = name; Path = path; Size = size; Fit = fit; }
        }

        /// <summary>Names the table paints as fire: their faces glow (vertex alpha 1, see the shader).</summary>
        static readonly HashSet<string> Glowing = new HashSet<string> { Palette.FireCore, Palette.FireEdge };

        public static readonly Entry[] Table =
        {
            // ── the things by the tent ──
            new Entry("Flashlight", Survival + "Torch.fbx", .25f, Fit.Length) { Lay = true,
                Colours = new[] { ("LightBlue", Palette.TorchLight), ("Black", Palette.Black), ("*", Palette.Metal) } },
            new Entry("Axe", Survival + "Axe.fbx", .6f, Fit.Length) { Lay = true,
                Colours = new[] { ("DarkWood", Palette.Wood), ("LightGrey", Palette.Metal), ("Red", Palette.Red) } },
            new Entry("Backpack", Survival + "Backpack.fbx", .6f, Fit.Height)
                { Colours = new[] { ("Brown", Palette.Felt), ("Gold", Palette.Felt), ("Green", Palette.Anorak), ("*", Palette.Canvas) } },
            // the pack's pup tent (1.8 × 1.6 m of canvas, guy lines out to twice that), drawn out to 3.6 × 2 m — near
            // the camp tent's 4.3 × 2; its open end comes out at +Z, like the site's. Only the canvas collides, so a
            // body crawls in through the mouth and the guy lines do not trip it
            new Entry("Tent", Survival + "Tent.fbx", 1.5f, Fit.Height) { Shape = Shape.Mesh, CollideOn = new[] { "LightGreen", "Green" }, Stretch = new Vector3(1.25f, 1f, 2f),
                Colours = new[] { ("DarkWood", Palette.Wood), ("Black", Palette.Quilt), ("LightGreen", Palette.Canvas), ("Green", Palette.Anorak) } },
            new Entry("Bedroll", KenneyModels + "bedroll.glb", 1.7f, Fit.Length) { AlignZ = true,
                Colours = new[] { ("blanket", Palette.Quilt), ("*", Palette.Canvas) } },
            new Entry("BedrollPacked", KenneyModels + "bedroll-packed.glb", .65f, Fit.Length) { AlignZ = true, Colours = new[] { ("*", Palette.Canvas) } },
            // the four-man tent rolled: a bundle a metre long, the same roll drawn bigger
            new Entry("TentRoll", KenneyModels + "bedroll-packed.glb", 1.1f, Fit.Length) { AlignZ = true, Colours = new[] { ("*", Palette.Canvas) } },
            new Entry("Can", Survival + "Can_Closed.fbx", .11f, Fit.Height) { Colours = new[] { ("LightGrey", Palette.Enamel), ("Grey", Palette.Metal) } },
            new Entry("Pot", Survival + "Pot.fbx", .22f, Fit.Height) { Colours = new[] { ("Black", Palette.Black), ("Grey", Palette.Metal) } },
            // ── the fire and the wood ──
            new Entry("Bonfire", Survival + "Bonfire.fbx", 1.1f, Fit.Length) { Shape = Shape.None,
                Colours = new[] { ("LightWood", Palette.Wood), ("*", Palette.Bark) } },
            new Entry("BonfireLit", Survival + "Bonfire_Fire.fbx", 1.1f, Fit.Length) { Shape = Shape.None,
                Colours = new[] { ("LightWood", Palette.Wood), ("Fire", Palette.FireEdge), ("*", Palette.Bark) } },
            new Entry("WoodLog", Survival + "WoodLog.fbx", .5f, Fit.Length) { AlignZ = true,
                Colours = new[] { ("LightWood", Palette.Wood), ("*", Palette.Bark) } },
            // the pack's stubby log drawn out to three metres and squeezed to half a metre through: a trunk, not a barrel
            new Entry("Windfall", KenneyModels + "tree-log.glb", 3f, Fit.Length) { AlignZ = true, Shape = Shape.Convex, SnowCap = .55f, Stretch = new Vector3(.75f, .75f, 1f),
                Colours = new[] { ("*", Palette.Bark) } },
            // ── trees: summer pines take snow on the boughs (their one green splits into lit tops and shaded
            // undersides), winter birches and a dead tree come with theirs ──
            new Entry("Pine_Snow_1", Nature + "PineTree_1.fbx", 9f, Fit.Height) { Shape = Shape.Trunk, SnowCap = .35f, SnowOn = new[] { Palette.NeedlesLit, Palette.NeedlesShade }, ShadeUnder = Palette.NeedlesShade,
                Colours = new[] { ("Wood", Palette.Bark), ("DarkGreen", Palette.NeedlesShade), ("Green", Palette.NeedlesLit) } },
            new Entry("Pine_Snow_2", Nature + "PineTree_2.fbx", 12f, Fit.Height) { Shape = Shape.Trunk, SnowCap = .35f, SnowOn = new[] { Palette.NeedlesLit, Palette.NeedlesShade }, ShadeUnder = Palette.NeedlesShade,
                Colours = new[] { ("Wood", Palette.Bark), ("DarkGreen", Palette.NeedlesShade), ("Green", Palette.NeedlesLit) } },
            new Entry("Pine_Snow_3", Nature + "PineTree_3.fbx", 11f, Fit.Height) { Shape = Shape.Trunk, SnowCap = .35f, SnowOn = new[] { Palette.NeedlesLit, Palette.NeedlesShade }, ShadeUnder = Palette.NeedlesShade,
                Colours = new[] { ("Wood", Palette.Bark), ("DarkGreen", Palette.NeedlesShade), ("Green", Palette.NeedlesLit) } },
            new Entry("Birch_Snow_1", Nature + "BirchTree_Dead_Snow_1.fbx", 7f, Fit.Height) { Shape = Shape.Trunk,
                Colours = new[] { ("White", Palette.Birch), ("Black", Palette.Black), ("Snow", Palette.SnowLit) } },
            new Entry("Birch_Snow_2", Nature + "BirchTree_Dead_Snow_2.fbx", 8.5f, Fit.Height) { Shape = Shape.Trunk,
                Colours = new[] { ("White", Palette.Birch), ("Black", Palette.Black), ("Snow", Palette.SnowLit) } },
            new Entry("DeadTree_Snow_1", Nature + "CommonTree_Dead_Snow_1.fbx", 7f, Fit.Height) { Shape = Shape.Trunk,
                Colours = new[] { ("Wood", Palette.Bark), ("Snow", Palette.SnowLit) } },
            // ── rocks: stone, snow on the upward faces ──
            new Entry("Rock_Snow_1", KenneyModels + "rock-a.glb", 1.0f, Fit.Height) { Shape = Shape.Convex, SnowCap = .6f, Colours = new[] { ("*", Palette.Stone) } },
            new Entry("Rock_Snow_2", KenneyModels + "rock-b.glb", 1.4f, Fit.Height) { Shape = Shape.Convex, SnowCap = .6f, Colours = new[] { ("*", Palette.Stone) } },
            new Entry("Rock_Snow_3", KenneyModels + "rock-c.glb", 1.8f, Fit.Height) { Shape = Shape.Convex, SnowCap = .6f, Colours = new[] { ("*", Palette.Stone) } },
        };

        /// <summary>True once the set has been baked (the sandbox setup asks before building its player).</summary>
        public static bool IsBuilt => File.Exists($"{PrefabDir}/Tent.prefab") && File.Exists(PaletteMaterials.MaterialPath);

        public static void Build()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Directory.CreateDirectory(PrefabDir); Directory.CreateDirectory(MeshDir);
            PaletteMaterials.CopySheet();
            var material = PaletteMaterials.Shared();
            var colormap = LoadColormap();
            int made = 0, missing = 0;
            foreach (var e in Table)
            {
                try
                {
                    if (Bake(e, material, colormap)) made++; else missing++;
                }
                catch (Exception ex) { Debug.LogError($"1079 imported: {e.Name} — {ex.Message}\n{ex.StackTrace}"); }
            }
            if (colormap != null) UnityEngine.Object.DestroyImmediate(colormap);
            AssetDatabase.SaveAssets();
            Debug.Log($"1079 imported: {made} prefabs in {clock.Elapsed.TotalSeconds:0.0} s" + (missing > 0 ? $", {missing} without a source model" : ""));
        }

        static Texture2D LoadColormap()
        {
            if (!File.Exists(KenneyColormap)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            if (!t.LoadImage(File.ReadAllBytes(KenneyColormap))) { UnityEngine.Object.DestroyImmediate(t); return null; }
            return t;
        }

        /// <summary>The import settings these files want, written by <see cref="ThirdPartyModelPostprocessor"/> on
        /// import; checked here too, so a file imported before the rule existed is re-imported on the spot.</summary>
        static void EnsureImportSettings(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter imp)) return;
            if (imp.isReadable && imp.useFileScale && !imp.importAnimation && imp.animationType == ModelImporterAnimationType.None) return;
            ThirdPartyModelPostprocessor.Apply(imp);
            imp.SaveAndReimport();
        }

        struct Face { public Vector3 A, B, C; public string Colour; public string Material, Part; }

        static bool Bake(Entry e, Material material, Texture2D colormap)
        {
            if (!File.Exists(e.Path)) { Debug.LogWarning($"1079 imported: нет модели {e.Path} — {e.Name} не собран"); return false; }
            EnsureImportSettings(e.Path);
            var src = AssetDatabase.LoadMainAssetAtPath(e.Path) as GameObject;
            if (src == null) { Debug.LogWarning($"1079 imported: {e.Path} не импортирован как модель — {e.Name} не собран"); return false; }
            // the root keeps the rotation and scale the importer gave it: a Blender export carries its Z-up → Y-up
            // turn on that node, and resetting it lays every model on its side
            var inst = UnityEngine.Object.Instantiate(src);
            inst.transform.position = Vector3.zero;
            var faces = new List<Face>();
            var used = new Dictionary<string, int>();
            try
            {
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = mf.sharedMesh; if (mesh == null) continue;
                    var mr = mf.GetComponent<MeshRenderer>();
                    var mats = mr != null ? mr.sharedMaterials : new Material[0];
                    var m = mf.transform.localToWorldMatrix;
                    bool flip = m.determinant < 0f;
                    var verts = mesh.vertices; var uvs = mesh.uv;
                    bool isGlb = e.Path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || e.Path.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase);
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        var mat = mats.Length > 0 ? mats[Mathf.Min(s, mats.Length - 1)] : null;
                        string matName = mat != null ? mat.name : "";
                        string part = mf.name;
                        // the colour this material/part came with: the FBX diffuse (linear in the file, so brought to sRGB
                        // before it is compared) or, per face, the Kenney colour sheet at the face's UVs
                        string fixedColour = Override(e, matName, part);
                        Color srcColour = mat != null && mat.HasProperty("_Color") ? Gamma(mat.color) : Color.gray;
                        var tris = mesh.GetTriangles(s);
                        for (int t = 0; t + 2 < tris.Length; t += 3)
                        {
                            int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                            if (flip) { int tmp = i1; i1 = i2; i2 = tmp; }
                            var f = new Face { A = m.MultiplyPoint3x4(verts[i0]), B = m.MultiplyPoint3x4(verts[i1]), C = m.MultiplyPoint3x4(verts[i2]), Material = matName, Part = part };
                            string colour = fixedColour;
                            if (colour == null)
                            {
                                Color c = srcColour;
                                if (isGlb && colormap != null && uvs != null && uvs.Length == verts.Length)
                                {
                                    var uv = (uvs[i0] + uvs[i1] + uvs[i2]) / 3f;
                                    c = colormap.GetPixelBilinear(uv.x, uv.y);
                                    // the sheet's top half is empty; a sample that lands there was flipped on import
                                    if (c.r + c.g + c.b < .02f) c = colormap.GetPixelBilinear(uv.x, 1f - uv.y);
                                }
                                colour = Palette.NearestName(c);
                            }
                            f.Colour = colour;
                            faces.Add(f);
                        }
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(inst); }
            if (faces.Count == 0) { Debug.LogWarning($"1079 imported: в {e.Path} нет граней — {e.Name} не собран"); return false; }

            // ── orientation: lay the upright ones down, turn the lying ones along Z, then fit and put the pivot at the base ──
            var b = Bounds(faces);
            if (e.Lay)
            {
                var ext = b.size;
                if (ext.y >= ext.x && ext.y >= ext.z) Rotate(faces, Quaternion.Euler(90f, 0f, 0f));
                else if (ext.x >= ext.z) Rotate(faces, Quaternion.Euler(0f, 90f, 0f));
                b = Bounds(faces);
            }
            if (e.AlignZ)
            {
                Rotate(faces, Quaternion.Euler(0f, -PrincipalYaw(faces), 0f));
                b = Bounds(faces);
                if (b.size.x > b.size.z) { Rotate(faces, Quaternion.Euler(0f, 90f, 0f)); b = Bounds(faces); }
            }
            float extent = e.Fit == Fit.Height ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            if (extent < 1e-5f) { Debug.LogWarning($"1079 imported: {e.Name} плоский, не собран"); return false; }
            float k = e.Size / extent;
            var origin = new Vector3(b.center.x, b.min.y, b.center.z);
            var yaw = Quaternion.Euler(0f, e.Yaw, 0f);
            Vector3 Place(Vector3 p) => yaw * Vector3.Scale((p - origin) * k, e.Stretch);
            for (int i = 0; i < faces.Count; i++)
            {
                var f = faces[i];
                f.A = Place(f.A); f.B = Place(f.B); f.C = Place(f.C);
                faces[i] = f;
            }
            b = Bounds(faces);
            // where each material sits, for the log: which end a doorway or a lens is on is read off this
            var where = new Dictionary<string, (Vector3 sum, int n)>();
            foreach (var f in faces)
            {
                where.TryGetValue(f.Material, out var w);
                where[f.Material] = (w.sum + (f.A + f.B + f.C) / 3f, w.n + 1);
            }

            // ── snow on what looks up, shadow on what looks down ──
            if (e.SnowCap > 0f || e.ShadeUnder != null)
            {
                for (int i = 0; i < faces.Count; i++)
                {
                    var f = faces[i];
                    if (e.SnowOn != null && Array.IndexOf(e.SnowOn, f.Colour) < 0) continue;
                    float up = Normal(f).y;
                    if (e.SnowCap > 0f && up > e.SnowCap) { f.Colour = Palette.SnowLit; faces[i] = f; }
                    else if (e.ShadeUnder != null && up < -.15f) { f.Colour = e.ShadeUnder; faces[i] = f; }
                }
            }
            foreach (var f in faces) { used.TryGetValue(f.Colour, out int n); used[f.Colour] = n + 1; }

            // ── the mesh: three vertices a face, flat normals, the colour in the vertices ──
            var render = ToMesh(faces, e.Name, null);
            SaveMesh(render, e.Name);
            Mesh collision = null;
            if ((e.Shape == Shape.Convex || e.Shape == Shape.Mesh) && e.CollideOn != null)
            {
                collision = ToMesh(faces, e.Name + "_Col", f => Matches(e.CollideOn, f.Material, f.Part));
                if (collision.vertexCount == 0) { Debug.LogWarning($"1079 imported: {e.Name}: под коллайдер не попала ни одна грань, взят весь меш"); collision = null; }
                else SaveMesh(collision, e.Name + "_Col");
            }

            var root = new GameObject(e.Name, typeof(MeshFilter), typeof(MeshRenderer));
            root.GetComponent<MeshFilter>().sharedMesh = render;
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            switch (e.Shape)
            {
                case Shape.Box: { var c = root.AddComponent<BoxCollider>(); c.center = b.center; c.size = b.size; break; }
                case Shape.Trunk:
                {
                    var c = root.AddComponent<CapsuleCollider>();
                    c.radius = TrunkRadius(faces, b.size.y) * 1.1f;
                    c.height = b.size.y * .6f; c.center = new Vector3(0f, b.size.y * .3f, 0f);
                    break;
                }
                case Shape.Convex: { var c = root.AddComponent<MeshCollider>(); c.sharedMesh = collision ?? render; c.convex = true; break; }
                case Shape.Mesh: { var c = root.AddComponent<MeshCollider>(); c.sharedMesh = collision ?? render; c.convex = false; break; }
            }
            string prefabPath = $"{PrefabDir}/{e.Name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            var names = new List<string>();
            foreach (var kv in used) names.Add($"{kv.Key} ×{kv.Value}");
            var places = new List<string>();
            foreach (var kv in where) { var c = kv.Value.sum / kv.Value.n; places.Add($"{kv.Key} ({c.x:0.00}, {c.y:0.00}, {c.z:0.00})"); }
            Debug.Log($"1079 imported: {e.Name} ← {Path.GetFileName(e.Path)}: {faces.Count} граней, {b.size.x:0.00}×{b.size.y:0.00}×{b.size.z:0.00} м; {string.Join(", ", names)}; центры материалов: {string.Join(", ", places)}");
            return true;
        }

        static string Override(Entry e, string material, string part)
        {
            string any = null;
            foreach (var (match, colour) in e.Colours)
            {
                if (match == "*") { any ??= colour; continue; }
                if (material.StartsWith(match, StringComparison.OrdinalIgnoreCase) || part.StartsWith(match, StringComparison.OrdinalIgnoreCase)) return colour;
            }
            return any;
        }

        static bool Matches(string[] prefixes, string material, string part)
        {
            foreach (var p in prefixes)
                if (material.StartsWith(p, StringComparison.OrdinalIgnoreCase) || part.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static Color Gamma(Color c) => new Color(Mathf.LinearToGammaSpace(c.r), Mathf.LinearToGammaSpace(c.g), Mathf.LinearToGammaSpace(c.b), 1f);

        static Vector3 Normal(Face f) => Vector3.Cross(f.B - f.A, f.C - f.A).normalized;

        static Bounds Bounds(List<Face> faces)
        {
            var b = new Bounds(faces[0].A, Vector3.zero);
            foreach (var f in faces) { b.Encapsulate(f.A); b.Encapsulate(f.B); b.Encapsulate(f.C); }
            return b;
        }

        static void Rotate(List<Face> faces, Quaternion q)
        {
            for (int i = 0; i < faces.Count; i++) { var f = faces[i]; f.A = q * f.A; f.B = q * f.B; f.C = q * f.C; faces[i] = f; }
        }

        /// <summary>Degrees of the long axis of the footprint, clockwise from +Z, off the covariance of the vertices.</summary>
        static float PrincipalYaw(List<Face> faces)
        {
            double sx = 0, sz = 0; int n = 0;
            foreach (var f in faces) { sx += f.A.x + f.B.x + f.C.x; sz += f.A.z + f.B.z + f.C.z; n += 3; }
            double mx = sx / n, mz = sz / n, cxx = 0, czz = 0, cxz = 0;
            foreach (var f in faces)
                foreach (var p in new[] { f.A, f.B, f.C }) { double dx = p.x - mx, dz = p.z - mz; cxx += dx * dx; czz += dz * dz; cxz += dx * dz; }
            // the eigenvector of the larger eigenvalue, as a heading
            double angle = .5 * Math.Atan2(2 * cxz, cxx - czz);      // from +X toward +Z
            return (float)(90.0 - angle * 180.0 / Math.PI);          // Unity yaw: from +Z toward +X
        }

        /// <summary>The trunk: how far from the axis the bark reaches at chest height. Only faces in a bark colour
        /// count, and only those close to the axis — a low bough or a root that runs out sideways is not the
        /// trunk, and a capsule as wide as the boughs would keep a body two metres from every tree.</summary>
        static float TrunkRadius(List<Face> faces, float height)
        {
            float lo = Mathf.Clamp(height * .1f, .3f, 1.2f), hi = lo + Mathf.Clamp(height * .1f, .4f, 1.5f);
            float r = 0f; bool any = false;
            foreach (var f in faces)
            {
                if (f.Colour != Palette.Bark && f.Colour != Palette.Birch) continue;
                foreach (var p in new[] { f.A, f.B, f.C })
                {
                    if (p.y < lo || p.y > hi) continue;
                    float d = new Vector2(p.x, p.z).magnitude;
                    if (d > .9f) continue;
                    r = Mathf.Max(r, d); any = true;
                }
            }
            return any ? Mathf.Clamp(r, .1f, .9f) : .25f;
        }

        static Mesh ToMesh(List<Face> faces, string name, Func<Face, bool> keep)
        {
            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var colours = new List<Color32>(); var tris = new List<int>();
            foreach (var f in faces)
            {
                if (keep != null && !keep(f)) continue;
                var n = Normal(f);
                Color32 c = Palette.Get(f.Colour);
                c.a = (byte)(Glowing.Contains(f.Colour) ? 255 : 0);
                int i = verts.Count;
                verts.Add(f.A); verts.Add(f.B); verts.Add(f.C);
                normals.Add(n); normals.Add(n); normals.Add(n);
                colours.Add(c); colours.Add(c); colours.Add(c);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }
            var mesh = new Mesh { name = name, indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetColors(colours); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void SaveMesh(Mesh mesh, string name)
        {
            string path = $"{MeshDir}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
        }
    }

}
