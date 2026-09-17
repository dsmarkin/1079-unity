// Stars: one quad per star (Yale Bright Star Catalogue to V 6), billboarded on the celestial sphere; additive, twinkling,
// dimmed toward the horizon. The mesh object carries the equatorial→horizontal rotation (Runtime/SkyDome.cs).
Shader "Height1079/SkyStars"
{
    SubShader
    {
        Tags { "Queue"="Background+1" "RenderType"="Background" }
        Cull Off ZWrite Off Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "SkyCommon.cginc"
            struct appdata { float4 vertex : POSITION; float2 corner : TEXCOORD0; float3 star : TEXCOORD1; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 corner : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                const float R = 2600;
                float3 dir = normalize(mul((float3x3)unity_ObjectToWorld, v.vertex.xyz));
                float3 centre = _WorldSpaceCameraPos + dir * R;
                float4 vc = mul(UNITY_MATRIX_V, float4(centre, 1));
                float size = v.star.x * R;
                vc.xy += v.corner * size;
                o.pos = mul(UNITY_MATRIX_P, vc);
                o.corner = v.corner;
                // extinction near the horizon, twinkle (stronger low in the sky)
                float ext = saturate(dir.y * 6) * lerp(.35, 1, saturate(dir.y * 2));
                float tw = 1 - (.35 * (1 - saturate(dir.y * 1.5)) + .1) * (.5 + .5 * sin(_Time.y * (7 + v.star.z * 9) + v.star.z * 60));
                o.color = v.color * v.star.y * ext * tw * _StarVis * (1 - _SkyStorm);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float r2 = dot(i.corner, i.corner);
                float a = exp(-r2 * 9) + .15 * exp(-r2 * 2);
                return fixed4(i.color.rgb * a, 1);
            }
            ENDCG
        }
    }
}
