using System.Collections.Generic;
using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>One arm, drawn as one thing: a hose that leaves the shoulder, bends round the elbow, narrows to the
    /// wrist, widens and flattens into the palm and ends in a low dome that three fingers and a thumb come out of.
    /// No upper arm, no forearm, no hand — one surface from the torso to the fingertips.
    ///
    /// The first arm was three lathed pieces overlapping at the joints, each capped with a ball so the next could
    /// start inside it. That is the classic trick for a doll and it reads as one: a chain of sausages, with the
    /// palm a fourth sausage glued on the end. A game does it with a skinned mesh — one tube, its vertices weighted
    /// to two or three bones, bending as a whole — and this is the same surface arrived at without the skeleton.
    /// The figure already solves the shoulder, the elbow and the hand in world space every frame, so instead of
    /// weighting a tube to bones it is simply rebuilt along the line through those three points: a Bézier rounds the
    /// elbow (a bend of radius about eight centimetres, roughly the radius of the arm, so the inside of the bend
    /// never folds), the profile tapers from shoulder to wrist and swells into the palm, and
    /// <see cref="PuppetMesh.Sweep"/> sews it up. Five hundred vertices twice a frame, which is nothing.
    ///
    /// The fingers are the one part not rebuilt: a relaxed hand curls the same way whatever the arm is doing, so
    /// they are turned once (<see cref="PuppetSkin.Digits"/>) and copied in at the end of the palm each frame. Their
    /// roots start a centimetre inside the palm's dome, so what shows is a finger coming out of a hand rather than a
    /// stub stuck to a ball — the same reason a shin starts inside the knee.
    ///
    /// The mesh is built in the shoulder's frame (world axes, origin at the shoulder) and the node is put there,
    /// which keeps the vertex coordinates small: a body twelve kilometres from the world origin would otherwise lose
    /// its fingertips to float precision.</summary>
    public sealed class PuppetArm
    {
        /// <summary>Radii along the arm, m, at the built stature. The shoulder matches the old upper arm and the
        /// elbow the old joint, so the sleeve still stands clear of the arm inside it.</summary>
        public const float ShoulderR = .094f, ElbowR = .074f, WristR = .056f, PalmR = .080f;
        /// <summary>The palm is centred on the hand point the figure solves for, and runs this far either side of it
        /// along the forearm's line; beyond that a dome this long closes it, and the fingers come out of the dome.</summary>
        public const float PalmHalf = .055f, PalmEnd = .025f;
        /// <summary>How thin the palm is across against how wide: a hand is a flat thing.</summary>
        public const float PalmThin = .55f;

        readonly PuppetMesh buf = new PuppetMesh();
        readonly List<PuppetMesh.Section> rings = new List<PuppetMesh.Section>(40);
        public readonly Mesh Mesh;
        public readonly Transform Node;
        /// <summary>The arm's own renderer, so the first person can keep the arms on screen while the rest of the
        /// figure is switched off (<see cref="PuppetFigure.ShowEyeArms"/>).</summary>
        public readonly MeshRenderer Renderer;
        /// <summary>−1 for the left arm, +1 for the right: the fingers are mirrored for the left, so the thumb stays
        /// on the inside and the curl goes toward the body.</summary>
        readonly float side;

        public PuppetArm(Transform parent, string name, float side, Material skin)
        {
            this.side = side;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            Mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = Mesh;
            Renderer = go.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial = skin;
            Node = go.transform;
        }

        /// <summary>The mesh is HideAndDontSave and belongs to no scene, so nothing frees it when the figure goes:
        /// the figure calls this from its own OnDestroy. The node dies with the figure by itself.</summary>
        public void Release() => PuppetRig.Kill(Mesh);

        /// <summary>Rebuilds the arm through three world points. <paramref name="grip"/> is the hand's rotation —
        /// its up points back along the forearm, its right is the thin axis of the palm — and <paramref name="k"/>
        /// the figure's scale against the stature these radii were drawn for.</summary>
        public void Pose(Vector3 shoulder, Vector3 elbow, Vector3 hand, Quaternion grip, float k)
        {
            Vector3 e = elbow - shoulder, p = hand - shoulder;
            var u1 = e.sqrMagnitude > 1e-8f ? e.normalized : Vector3.down;
            var f = p - e;
            var u2 = f.sqrMagnitude > 1e-8f ? f.normalized : u1;
            float lenU = Mathf.Max(e.magnitude, .02f), half = PalmHalf * k;
            float fore = Mathf.Max(f.magnitude - half, .02f);                  // elbow to wrist
            float rS = ShoulderR * k, rE = ElbowR * k, rW = WristR * k, rP = PalmR * k;
            // how far either side of the elbow the corner is rounded over: a quadratic Bézier with legs d bends with
            // a radius of about 0.7·d at 90°, so 11 cm of leg keeps the bend wider than the arm is thick
            float d = Mathf.Min(.11f * k, lenU * .45f, fore * .45f);
            rings.Clear();

            // the shoulder: a dome buried in the torso, so the arm has no edge where it leaves the shirt
            for (int i = 0; i < 4; i++)
            {
                float a = -Mathf.PI * .5f + Mathf.PI * .5f * i / 4;
                rings.Add(new PuppetMesh.Section(u1 * (rS * Mathf.Sin(a)), u1, rS * Mathf.Cos(a)));
            }
            // upper arm, straight, to where the bend begins
            const int up = 5;
            for (int i = 0; i <= up; i++)
            {
                float s = (lenU - d) * i / up;
                rings.Add(new PuppetMesh.Section(u1 * s, u1, Upper(s / lenU, rS, rE)));
            }
            // the elbow, rounded
            Vector3 A = e - u1 * d, B = e + u2 * d;
            float rA = Upper((lenU - d) / lenU, rS, rE), rB = Lower(d / fore, rE, rW);
            const int bend = 7;
            for (int i = 1; i <= bend; i++)
            {
                float t = i / (float)bend;
                rings.Add(new PuppetMesh.Section(PuppetMesh.Bezier(A, e, B, t), PuppetMesh.BezierTangent(A, e, B, t), Mathf.Lerp(rA, rB, t)));
            }
            // forearm, straight, narrowing to the wrist
            const int low = 5;
            for (int i = 1; i <= low; i++)
            {
                float s = d + (fore - d) * i / low;
                rings.Add(new PuppetMesh.Section(e + u2 * s, u2, Lower(s / fore, rE, rW)));
            }
            // the palm: the same tube, wider and flattened across
            var w = e + u2 * fore;
            const int palm = 7;
            for (int i = 1; i <= palm; i++)
            {
                float t = i / (float)palm;
                float r = Mathf.Lerp(rW, rP, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 1.35f)));
                float thin = Mathf.Lerp(1f, PalmThin, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 1.5f)));
                rings.Add(new PuppetMesh.Section(w + u2 * (2f * half * t), u2, r, new Vector2(thin, 1f)));
            }
            // and its end: a low dome, the fingers come out of it
            var end = p + u2 * half;
            float cap = PalmEnd * k;
            for (int i = 1; i <= 4; i++)
            {
                float a = Mathf.PI * .5f * i / 4;
                rings.Add(new PuppetMesh.Section(end + u2 * (cap * Mathf.Sin(a)), u2, rP * Mathf.Cos(a), new Vector2(PalmThin, 1f)));
            }

            buf.Clear();
            buf.Sweep(rings, grip, PuppetSkin.Slim, PuppetSkinTexture.Forearm);
            buf.Append(PuppetSkin.Digits, Matrix4x4.TRS(p, grip, new Vector3(side < 0f ? -k : k, k, k)));
            buf.Fill(Mesh);
            Node.SetPositionAndRotation(shoulder, Quaternion.identity);
        }

        /// <summary>Upper arm profile, 0 at the shoulder and 1 at the elbow: a taper with a little swell in the
        /// middle, so the arm looks soft rather than machined.</summary>
        static float Upper(float x, float rS, float rE) => Mathf.Lerp(rS, rE, Mathf.SmoothStep(0f, 1f, x)) * (1f + .04f * Mathf.Sin(x * Mathf.PI));
        static float Lower(float x, float rE, float rW) => Mathf.Lerp(rE, rW, Mathf.SmoothStep(0f, 1f, x)) * (1f + .02f * Mathf.Sin(x * Mathf.PI));
    }
}
