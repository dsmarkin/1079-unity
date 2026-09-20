using System.Collections.Generic;
using UnityEngine;

namespace Height1079.Runtime
{
    /// <summary>Draws what <see cref="NightSession.CampList"/> says: a двойка standing on the snow wherever somebody
    /// pitched one, and gone again when it is struck.
    ///
    /// The mesh is built here rather than in a factory on purpose. A pitched tent is the only object in the game that
    /// appears and disappears while the run is going, and building it in the runtime keeps it out of the generated
    /// world altogether: no prefab, no pipeline version, nothing to regenerate. It is a ridge tent of the shape every
    /// такая двойка has — a fly over two poles, a groundsheet under it, a dark doorway at the downhill end — and it is
    /// deliberately built without a collider, because the party spawns beside it when a save is loaded and a collider
    /// there would push somebody through the slope.</summary>
    public sealed class CampView : MonoBehaviour
    {
        /// <summary>A двойка: 2.3 m along the ridge, 1.4 m across, 1.05 m at the ridge.</summary>
        public const float Length = 2.3f, Width = 1.4f, Ridge = 1.05f;

        readonly Dictionary<int, Transform> tents = new Dictionary<int, Transform>();
        readonly List<int> gone = new List<int>();
        static Mesh flyMesh, sheetMesh;
        static Material flyMat, sheetMat;

        public static CampView Create()
        {
            var go = new GameObject("CampView", typeof(CampView));
            DontDestroyOnLoad(go);
            return go.GetComponent<CampView>();
        }

        void LateUpdate()
        {
            var s = NightSession.Instance;
            if (s == null)
            {
                if (tents.Count > 0) Clear();
                return;
            }
            gone.Clear();
            gone.AddRange(tents.Keys);
            for (int i = 0; i < s.CampList.Count; i++)
            {
                var c = s.CampList[i];
                gone.Remove(c.Id);
                if (!tents.TryGetValue(c.Id, out var t) || t == null)
                {
                    t = Build(c.Id);
                    tents[c.Id] = t;
                }
                t.SetPositionAndRotation(c.Pos, Quaternion.Euler(0, c.Yaw, 0));
            }
            foreach (var id in gone)
            {
                if (tents.TryGetValue(id, out var t) && t != null) Destroy(t.gameObject);
                tents.Remove(id);
            }
        }

        void Clear()
        {
            foreach (var t in tents.Values) if (t != null) Destroy(t.gameObject);
            tents.Clear();
        }

        Transform Build(int id)
        {
            var root = new GameObject("Camp" + id).transform;
            root.SetParent(transform, false);
            Part(root, "Fly", Fly(), FlyMaterial);
            Part(root, "Groundsheet", Sheet(), SheetMaterial);
            return root;
        }

        static void Part(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
        }

        // ── the two meshes ────────────────────────────────────────────────────────────────────────────────

        /// <summary>The fly: two sloping panels over the ridge and a triangular end at each end, the downhill one left
        /// open as a doorway. Double-sided, because you can walk round a tent and look into it.</summary>
        static Mesh Fly()
        {
            if (flyMesh != null) return flyMesh;
            float hw = Width / 2f, hl = Length / 2f, h = Ridge;
            // a fly sags a little between the poles: the ridge dips in the middle and the walls belly out
            var verts = new List<Vector3>();
            var tris = new List<int>();

            int Rows = 6, Cols = 5;
            // one sheet from the left ground edge over the ridge to the right ground edge
            var section = new[]
            {
                new Vector2(-hw, 0f),
                new Vector2(-hw * .78f, h * .52f),
                new Vector2(0f, h),
                new Vector2(hw * .78f, h * .52f),
                new Vector2(hw, 0f),
            };
            for (int r = 0; r <= Rows; r++)
            {
                float v = (float)r / Rows;
                float z = -hl + v * Length;
                float sag = Mathf.Sin(Mathf.PI * v);                // nothing at the poles, most in the middle
                for (int c = 0; c < Cols; c++)
                {
                    var p = section[c];
                    float dip = c == 2 ? .05f : c == 1 || c == 3 ? .02f : 0f;
                    verts.Add(new Vector3(p.x, p.y - sag * dip, z));
                }
            }
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols - 1; c++)
                {
                    int a = r * Cols + c, b = a + 1, d = a + Cols, e = d + 1;
                    tris.Add(a); tris.Add(d); tris.Add(b);
                    tris.Add(b); tris.Add(d); tris.Add(e);
                    tris.Add(b); tris.Add(d); tris.Add(a);          // and the same from inside
                    tris.Add(e); tris.Add(d); tris.Add(b);
                }

            // the closed end (-Z): the section filled in as a fan from the ridge
            int back = 0;
            int apex = verts.Count;
            verts.Add(new Vector3(0f, h, -hl));
            for (int c = 0; c < Cols - 1; c++)
            {
                int a = back + c, b = back + c + 1;
                tris.Add(a); tris.Add(b); tris.Add(apex);
                tris.Add(b); tris.Add(a); tris.Add(apex);
            }

            var mesh = new Mesh { name = "CampTentFly" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            flyMesh = mesh;
            return mesh;
        }

        /// <summary>The groundsheet, a hand's width proud of the fly so the tent reads as sitting on the snow.</summary>
        static Mesh Sheet()
        {
            if (sheetMesh != null) return sheetMesh;
            float hw = Width / 2f + .06f, hl = Length / 2f + .06f;
            var mesh = new Mesh { name = "CampTentSheet" };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-hw, .02f, -hl), new Vector3(-hw, .02f, hl),
                new Vector3(hw, .02f, hl), new Vector3(hw, .02f, -hl),
            });
            mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            sheetMesh = mesh;
            return mesh;
        }

        // ── two materials, lit like everything else ───────────────────────────────────────────────────────
        // Unlit would glow in the dark and on a white slope would read as a hole; the Flat template ProjectSetup
        // writes into Resources is the Standard shader, which is what the rest of the world uses.

        static Material FlyMaterial => flyMat != null ? flyMat : flyMat = Tinted(new Color(.86f, .36f, .12f), .12f);
        static Material SheetMaterial => sheetMat != null ? sheetMat : sheetMat = Tinted(new Color(.14f, .15f, .17f), .05f);

        static Material Tinted(Color c, float smoothness)
        {
            var template = Resources.Load<Material>("Flat");
            var m = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
            m.color = c;
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        void OnDestroy() => Clear();
    }
}
