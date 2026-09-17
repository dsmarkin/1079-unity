// Cloud deck: procedural fbm on a flat layer projected onto the dome, drifting with the wind; broken or overcast
// (_CloudCover), dark against the night sky, warm on the sun's side at dusk. Hides stars, moon and aurora behind it.
Shader "Height1079/SkyClouds"
{
    SubShader
    {
        Tags { "Queue"="Background+4" "RenderType"="Background" }
        Cull Front ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "SkyCommon.cginc"
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float2 p = dir.xz / (max(dir.y, 0) + .09) * .9 + _CloudWind * _CloudTime;
                float n = h1079_fbm(p) * .7 + h1079_fbm(p * 3.1 + 9.2) * .3;
                float cover = _CloudCover;
                float dens = smoothstep(1 - cover - .12, 1 - cover + .18, n);
                dens = max(dens, saturate(cover * 1.1 - .05));            // overcast fills the gaps
                dens = max(dens, smoothstep(.16, 0, dir.y) * .9);        // haze on the horizon
                float3 sky = h1079_skyColor(dir);
                // underside of the deck: a bit darker than the sky at night, lit by the glow at dusk, thin edges catch light
                float2 sd = normalize(_SunDirW.xz + 1e-5), vd = normalize(dir.xz + 1e-5);
                float toward = saturate(dot(sd, vd) * .5 + .5);
                float3 col = sky * lerp(.75, 1.05, 1 - dens) + _SkyGlow * pow(toward, 3) * .35 * (1 - dens * .5);
                col = lerp(col, float3(.07, .08, .095) * (1 + length(_SkyGlow)), _SkyStorm);
                float alpha = saturate(dens * 1.05) * (dir.y < -.02 ? 1 : 1);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
