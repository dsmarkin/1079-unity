// What the investigator draws in his head, standing on the spot.
//
// The world is one state: the day after. Everything the record puts on the ground that morning is solid
// and has colliders. Everything that had already stopped existing — the camp of 31 January, packed up and
// carried away the afternoon before — is drawn with this, and has no colliders at all. You walk through it.
// That is the whole explanation, and it needs no interface.
//
// _Ghost carries how much is known: 1 for a reconstruction whose place is proved, lower for an assumption.
//
// Weighted hard toward the rim rather than the surface, and that is not a style choice. Transparent faces with
// ZWrite off sort against each other badly, and a tent rendered as translucent solid comes out a glassy blob
// with no tent in it. Drawn mostly as edges it reads as a tent again — and edges are what someone picturing a
// thing actually sees.
//
// Lit rather than unlit on purpose: an unlit material burns pure white at night (see CLAUDE.md), and this
// has to sit in the same darkness as everything else. The emission is small and scales with _Ghost so the
// faint version does not become the brightest thing on the slope.
Shader "Height1079/Ghost"
{
    Properties
    {
        _Color      ("Tint", Color)             = (0.72, 0.80, 0.88, 1)
        _MainTex    ("Albedo", 2D)              = "white" {}
        _Ghost      ("Certainty", Range(0,1))   = 1
        _Alpha      ("Base alpha", Range(0,1))  = 0.11
        _RimPower   ("Rim power", Range(0.5,8)) = 1.9
        _RimBoost   ("Rim boost", Range(0,3))   = 2.35
        _Emission   ("Emission", Range(0,1))    = 0.22
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        CGPROGRAM
        #pragma surface surf Lambert alpha:fade noshadow nometa
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Ghost, _Alpha, _RimPower, _RimBoost, _Emission;

        struct Input { float2 uv_MainTex; float3 viewDir; };

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            // the texture only tints here: a ghost keeps the shape of the thing, not its material
            half3 base = lerp(_Color.rgb, _Color.rgb * (0.55 + 0.45 * tex.rgb), 0.5);

            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            half edge = pow(rim, _RimPower) * _RimBoost;

            o.Albedo = base;
            o.Emission = base * _Emission * _Ghost * (0.35 + edge);
            // certainty drives opacity, and the rim is what keeps the outline visible on snow
            o.Alpha = saturate((_Alpha + edge * 0.85) * (0.35 + 0.65 * _Ghost));
        }
        ENDCG
    }

    Fallback "Transparent/Diffuse"
}
