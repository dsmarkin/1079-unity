using System.Collections.Generic;
using UnityEngine;

namespace Height1079.Night
{
    /// <summary>The Menk's body: the jointed model the editor builds (<c>CreatureFactory</c>, a hierarchy of named
    /// bones with no skinning) and the one way of posing it. The night's <c>MenkView</c> works out what the creature
    /// is doing from the session; the sandbox works it out from its own hunter; both hand the answer here as a
    /// <see cref="Pose"/> and get the same giant back — a dead spruce with its arms up, a hunched stride, a swing.</summary>
    public sealed class MenkPuppet
    {
        public const string PrefabPath = "World/Prefabs/Creatures/Menk";
        public const float Height = 4.3f;

        /// <summary>Everything the bones need to know this frame. All 0 … 1 unless said otherwise.</summary>
        public struct Pose
        {
            /// <summary>1 = pretending to be a tree (arms raised, still), 0 = alive.</summary>
            public float Tree;
            /// <summary>How much of a stride it is taking, and where in it (radians, grows with the ground covered).</summary>
            public float Gait, Phase;
            /// <summary>Degrees the back is bent forward: about twenty walking, thirty hunting.</summary>
            public float Hunch;
            /// <summary>The swing: the arm going up, then coming down. Both hands when <see cref="TwoHands"/>.</summary>
            public float Raise, Slam;
            public bool TwoHands;
            /// <summary>Wind in the branches while it pretends (degrees), and the shudder of waking.</summary>
            public float Sway, Shudder;
            /// <summary>Degrees the head is turned aside, looking about.</summary>
            public float HeadTurn;
        }

        public Transform Model { get; }
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        readonly Transform hips; readonly Vector3 hipsRest;

        /// <summary>The editor's model under <paramref name="parent"/>, or null when the world has not been built —
        /// the caller then draws something else, the way every factory here has a fallback.</summary>
        public static MenkPuppet Load(Transform parent)
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null) return null;
            return new MenkPuppet(Object.Instantiate(prefab, parent).transform);
        }

        public MenkPuppet(Transform model)
        {
            Model = model;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) { bones[t.name] = t; rest[t] = t.localRotation; }
            hips = Bone("Hips"); if (hips != null) hipsRest = hips.localPosition;
        }

        public Transform Bone(string name) => bones.TryGetValue(name, out var t) ? t : null;

        static Quaternion E(float x, float y, float z) => Quaternion.Euler(x, y, z);

        void Set(string bone, Quaternion tree, Quaternion alive, float treeW)
        {
            var t = Bone(bone);
            if (t == null) return;
            t.localRotation = rest[t] * Quaternion.Slerp(alive, tree, treeW);
        }

        public void Apply(Pose p)
        {
            float treeW = Mathf.Clamp01(p.Tree), gait = Mathf.Clamp01(p.Gait);
            float sp = Mathf.Sin(p.Phase), cp = Mathf.Cos(p.Phase);
            float raise = Mathf.Clamp01(p.Raise), slam = Mathf.Clamp01(p.Slam);
            float sway = p.Sway, shudder = p.Shudder, hunch = p.Hunch;
            float bob = gait * Mathf.Abs(sp);

            Set("Spine", E(-2f + sway * .4f, 0, 2f + sway), E(hunch + bob * 3f - raise * 18f + slam * 25f + shudder, sp * 4f * gait, sp * 3f * gait), treeW);
            Set("Chest", E(0, 0, -3f + sway * .6f), E(6f - raise * 8f + slam * 12f, -sp * 6f * gait, 0), treeW);
            Set("Neck", E(38f, 0, sway * .3f), E(-hunch - 4f + shudder * .5f, 0, 0), treeW);
            Set("Head", E(22f, 0, 0), E(-10f + raise * 10f, p.HeadTurn, 0), treeW);

            float swingL = -sp * 26f * gait, swingR = sp * 26f * gait;
            Quaternion armRAlive = E(swingR + 6f, 0, 8f);
            armRAlive = Quaternion.Slerp(armRAlive, E(-168f, 0, 18f), raise);
            armRAlive = Quaternion.Slerp(armRAlive, E(-25f, 0, 6f), slam);
            Quaternion armLAlive = E(swingL + 6f, 0, -8f);
            if (p.TwoHands)
            {
                armLAlive = Quaternion.Slerp(armLAlive, E(-168f, 0, -18f), raise);
                armLAlive = Quaternion.Slerp(armLAlive, E(-25f, 0, -6f), slam);
            }
            Set("ArmL", E(-12f, sway * .5f, -146f + sway * 2f), armLAlive, treeW);
            Set("ArmR", E(-8f, -sway * .5f, 142f - sway * 2f), armRAlive, treeW);
            Set("ForearmL", E(0, 0, 34f + sway * 3f), E(-22f - 10f * gait - raise * 25f, 0, 0), treeW);
            Set("ForearmR", E(0, 0, -40f - sway * 3f), E(-22f - 10f * gait - raise * 25f, 0, 0), treeW);
            Set("HandL", E(0, 0, 22f), E(-10f, 0, 0), treeW);
            Set("HandR", E(0, 0, -18f), E(-10f, 0, 0), treeW);

            float kneeL = Mathf.Max(0f, cp) * 50f * gait + 8f, kneeR = Mathf.Max(0f, -cp) * 50f * gait + 8f;
            Set("ThighL", E(0, 0, 2f), E(-sp * 30f * gait - 10f - slam * 8f, 0, 3f), treeW);
            Set("ThighR", E(0, 0, -2f), E(sp * 30f * gait - 10f - slam * 8f, 0, -3f), treeW);
            Set("ShinL", E(0, 0, 0), E(kneeL + slam * 12f, 0, 0), treeW);
            Set("ShinR", E(0, 0, 0), E(kneeR + slam * 12f, 0, 0), treeW);
            Set("FootL", E(0, 0, 0), E(-kneeL * .3f, 0, 0), treeW);
            Set("FootR", E(0, 0, 0), E(-kneeR * .3f, 0, 0), treeW);

            if (hips != null) hips.localPosition = hipsRest + Vector3.up * ((1f - treeW) * (-.18f - bob * .1f - slam * .25f));
        }

        /// <summary>The swing, 0 … 1.4 s in: how far the arm is up and how far it has come down. Shared so a blow
        /// looks the same whoever times it.</summary>
        public static void Swing(float t, out float raise, out float slam)
        {
            raise = t < 0f ? 0f : t < .45f ? Mathf.SmoothStep(0, 1, t / .45f) : t < .75f ? 1f - Mathf.SmoothStep(0, 1, (t - .45f) / .3f) : 0f;
            slam = t < 0f ? 0f : t < .45f ? 0f : t < .75f ? Mathf.SmoothStep(0, 1, (t - .45f) / .3f) : 1f - Mathf.SmoothStep(0, 1, (t - .75f) / .65f);
        }
    }
}
