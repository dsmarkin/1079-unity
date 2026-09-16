using GLTFast;
using UnityEngine;

namespace Height1079.Runtime
{
    /// <summary>Loads the exported hiker (public/models/hiker-v1.glb → Resources/hiker.bytes) under the "Visual" child and drives its five legacy clips.
    /// Falls back to the primitive placeholder when the model is missing.</summary>
    public sealed class HikerAnimator : MonoBehaviour
    {
        const string Idle = "Idle", Walk = "Walk", Run = "Run", Cold = "Cold", Kindle = "Kindle";

        Animation anim;
        Transform visual, placeholder, model;
        Vector3 lastPosition;
        float speed;
        string current = "";
        HikerController hiker;

        async void Start()
        {
            hiker = GetComponent<HikerController>();
            visual = transform.Find("Visual");
            if (visual == null) return;
            placeholder = visual.Find("Body");
            lastPosition = transform.position;
            var asset = Resources.Load<TextAsset>("hiker");
            if (asset == null) { Debug.LogWarning("Resources/hiker.bytes missing — using the placeholder hiker."); return; }
            var gltf = new GltfImport();
            bool ok = await gltf.Load(asset.bytes, null, new ImportSettings { AnimationMethod = AnimationMethod.Legacy });
            if (!ok || this == null) { Debug.LogWarning("Hiker GLB failed to load — using the placeholder hiker."); return; }
            var root = new GameObject("Model").transform;
            root.SetParent(visual, false);
            root.localRotation = Quaternion.Euler(0, 180f, 0); // the export faces −Z; Unity forward is +Z
            ok = await gltf.InstantiateMainSceneAsync(root);
            if (!ok || this == null) return;
            model = root;
            anim = root.GetComponentInChildren<Animation>();
            if (anim != null)
            {
                foreach (AnimationState state in anim) state.wrapMode = WrapMode.Loop;
                anim.playAutomatically = false;
                Play(Idle);
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            // Hide the primitives once the real model is in.
            for (int i = visual.childCount - 1; i >= 0; i--) { var c = visual.GetChild(i); if (c != root) c.gameObject.SetActive(false); }
        }

        void Update()
        {
            if (anim == null) return;
            var delta = transform.position - lastPosition; lastPosition = transform.position;
            float v = Time.deltaTime > 0 ? new Vector2(delta.x, delta.z).magnitude / Time.deltaTime : 0f;
            speed = Mathf.Lerp(speed, v, 1f - Mathf.Exp(-9f * Time.deltaTime));
            byte action = hiker != null ? hiker.Action.Value : (byte)0;
            string wanted = action == 2 ? Kindle : speed > 4f ? Run : speed > .35f ? Walk : action == 1 ? Cold : Idle;
            Play(wanted);
        }

        void Play(string clip)
        {
            if (clip == current || anim == null || anim.GetClip(clip) == null) return;
            anim.CrossFade(clip, .2f);
            current = clip;
        }
    }
}
