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
        _Color      ("Tint", Color)             = (0.44, 0.55, 0.72, 1)
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
        // Depth first, colour second. Without this every transparent face of a ghost blends over every other
        // one behind it, and a camp of them at close range comes out as white fog with no objects in it — it
        // did, and it is what the first shots of the camp showed. The prepass writes depth without colour, so
        // only the nearest surface of each object blends and the thing keeps its shape from any distance.
        Pass
        {
            ColorMask 0
            ZWrite On
            Cull Back
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        CGPROGRAM
        #pragma surface surf Lambert alpha:fade noshadow nometa
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Ghost, _Alpha, _RimPower, _RimBoost, _Emission;
        // Set globally by Bootstrap from the world's own darkness: 0 in daylight, 1 in the deep of the night.
        // Deliberately not a Property, so a per-material value cannot shadow it.
        half _GhostNight;

        struct Input { float2 uv_MainTex; float3 viewDir; };

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            // the texture only tints here: a ghost keeps the shape of the thing, not its material
            half3 base = lerp(_Color.rgb, _Color.rgb * (0.55 + 0.45 * tex.rgb), 0.5);

            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            half edge = pow(rim, _RimPower) * _RimBoost;

            // A ghost has opposite problems at the two ends of the day, and one setting cannot serve both. On
            // sunlit snow anything pale and half-transparent vanishes into the white, so by day it has to be
            // DARKER than the thing it stands on — a drawing in blue-grey. At night there is nothing bright to be
            // darker than, so it goes back to a faint glow. The world says which way it is.
            half night = saturate(_GhostNight);

            o.Albedo = base * lerp(0.55, 1.0, night);
            o.Emission = base * lerp(0.05, _Emission, night) * _Ghost * (0.35 + edge);
            // certainty drives opacity, and the rim is what keeps the outline visible on snow
            o.Alpha = saturate((lerp(0.52, _Alpha, night) + edge * 0.85) * (0.35 + 0.65 * _Ghost));
        }
        ENDCG
    }

    Fallback "Transparent/Diffuse"
}
