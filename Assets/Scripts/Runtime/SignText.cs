using UnityEngine;

namespace Height1079.Runtime
{
    /// <summary>Makes the text of a board in the world behave like paint on wood: hidden behind whatever stands in front
    /// of it, and readable from the front face only. Unity's own text material draws with the depth test off, so without
    /// this a station sign reads backwards through its own post and the chalk menu of a cafe shows through the wall.
    /// The font atlas of a dynamic font is rebuilt whenever a new glyph appears, so the texture is re-read then too.</summary>
    [DisallowMultipleComponent]
    public sealed class SignText : MonoBehaviour
    {
        static Shader shader;
        Font font;
        Material mat;

        void Awake() => Apply();

        void OnEnable() => Font.textureRebuilt += OnRebuilt;
        void OnDisable() => Font.textureRebuilt -= OnRebuilt;

        void OnRebuilt(Font f) { if (f == font) Apply(); }

        void Apply()
        {
            var text = GetComponent<TextMesh>();
            var renderer = GetComponent<MeshRenderer>();
            if (text == null || renderer == null) return;
            font = text.font != null ? text.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            var atlas = font != null && font.material != null ? font.material.mainTexture : null;
            if (atlas == null) return;
            if (shader == null) shader = Shader.Find("1079/Text3D");
            if (shader == null) return;                       // stripped from the build: keep Unity's own material
            if (mat == null) mat = new Material(shader) { name = "Text3D (runtime)" };
            mat.mainTexture = atlas;
            renderer.sharedMaterial = mat;
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }
    }
}
