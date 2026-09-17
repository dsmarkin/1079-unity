// Sky dome: gradient, twilight glow, Milky Way band. Drawn first, no fog, no depth write.
Shader "Height1079/Sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Front ZWrite Off
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
                float3 c = h1079_skyColor(dir);
                // Milky Way: a soft band along the galactic plane, brighter toward the centre, patchy
                float b = dot(dir, _GalPoleW);
                float band = exp(-b * b / .018) * (.55 + .45 * saturate(dot(dir, _GalCentreW) * .5 + .5));
                float patch = 0;
                if (_MilkyWay > .001 && band > .01)
                {
                    float3 q = dir * 9;
                    patch = h1079_fbm(q.xy + q.z * 1.7);
                }
                band *= .3 + 1.4 * patch;
                c += float3(.55, .6, .75) * band * _MilkyWay * .045 * saturate(dir.y * 4);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
