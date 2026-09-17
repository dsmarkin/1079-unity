// The Moon: a quad facing the camera, the disc lit from the sun's direction (phase), faint earthshine, a soft halo.
Shader "Height1079/SkyMoon"
{
    SubShader
    {
        Tags { "Queue"="Background+3" "RenderType"="Background" }
        Cull Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "SkyCommon.cginc"
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 l : TEXCOORD1; float up : TEXCOORD2; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy * 2 - 1;
                float3 right = normalize(unity_ObjectToWorld._m00_m10_m20);
                float3 up = normalize(unity_ObjectToWorld._m01_m11_m21);
                float3 fwd = normalize(unity_ObjectToWorld._m02_m12_m22);
                o.l = float3(dot(_SunDirW, right), dot(_SunDirW, up), dot(_SunDirW, -fwd));
                o.up = _MoonDirW.y;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // the quad is 4 moon radii wide: the disc is r < .25
                float2 p = i.uv * 4;
                float r2 = dot(p, p);
                float3 n = float3(p, sqrt(saturate(1 - r2)));
                float lit = smoothstep(-.02, .12, dot(n, normalize(i.l)));
                float maria = .78 + .22 * h1079_fbm(p * 2.2 + 3.7);
                float disc = smoothstep(1.02, .98, sqrt(r2));
                float ext = saturate(i.up * 5 + .15);   // reddened and dimmed low over the horizon
                float3 moonCol = lerp(float3(1, .72, .5), float3(.98, .96, .9), saturate(i.up * 4));
                float3 col = moonCol * (lit * maria * 1.25 + .025) * disc;
                float halo = exp(-max(r2 - 1, 0) * .35) * (1 - disc) * .08 * _MoonLit;
                float vis = (1 - _SkyStorm) * ext;
                return fixed4((col + moonCol * halo) * vis, disc * vis);
            }
            ENDCG
        }
    }
}
