// Aurora borealis: green curtains low in the northern sky with red-violet tops and moving rays. Additive, faint.
// An artistic assumption for this night (1957–59 was a solar maximum); driven by _Aurora (0 = off).
Shader "Height1079/SkyAurora"
{
    SubShader
    {
        Tags { "Queue"="Background+2" "RenderType"="Background" }
        Cull Front ZWrite Off Blend One One
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
                if (_Aurora <= 0.001) return 0;
                float3 dir = normalize(i.dir);
                float az = atan2(dir.x, dir.z);           // 0 = north
                float alt = asin(saturate(dir.y));
                float t = _AuroraTime;
                float spread = exp(-az * az / 1.1);        // centred on the north
                float fold = sin(az * 2.3 + t * .05) * .07 + sin(az * 5.1 - t * .09) * .03 + h1079_noise(float2(az * 3, t * .04)) * .05;
                float base = .2 + fold;                    // lower edge ≈ 11° with folds
                float above = alt - base;
                float curtain = above > 0 ? exp(-above / .32) : exp(above * 40);
                float rays = .45 + .55 * pow(h1079_noise(float2(az * 70 + fold * 30, t * .6)), 2);
                float bands = .6 + .4 * h1079_noise(float2(az * 9 - t * .15, 3.3));
                float3 green = float3(.25, 1, .45), top = float3(.75, .25, .55);
                float3 col = lerp(green, top, saturate(above / .45)) * curtain * rays * bands * spread;
                return fixed4(col * _Aurora * .22 * _StarVis, 0);
            }
            ENDCG
        }
    }
}
