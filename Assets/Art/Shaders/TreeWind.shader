// Trees in the wind (built-in RP). Standard lighting, cut-out cards, and a vertex bend driven by global wind values
// set from Runtime/Weather.cs: _WindParams = (strength 0..1, gust 0..1, time, unused), _WindDir = (x, z) in world space.
// The crown bends with height; branch cards away from the trunk flutter faster; each tree gets its own phase.
Shader "Height1079/TreeWind"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal scale", Float) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.1
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.45
        _Bend ("Bend (m at 18 m in full storm)", Float) = 1.3
        _Flutter ("Branch flutter", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "DisableBatching"="True" }
        LOD 300
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow alphatest:_Cutoff
        #pragma multi_compile_instancing
        #pragma target 3.0

        sampler2D _MainTex, _BumpMap;
        fixed4 _Color;
        half _Glossiness, _BumpScale, _Bend, _Flutter;
        float4 _WindParams;
        float4 _WindDir;

        struct Input { float2 uv_MainTex; };

        void vert(inout appdata_full v)
        {
            float strength = _WindParams.x, gust = _WindParams.y, t = _WindParams.z;
            float3 root = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            float phase = dot(root.xz, float2(.137, .171));
            float3 dirOS = normalize(mul((float3x3)unity_WorldToObject, float3(_WindDir.x, 0, _WindDir.y)) + 1e-5);

            float h = max(v.vertex.y, 0);
            float k = pow(saturate(h / 18.0), 1.7);
            // the crown leans with the wind and rocks around the lean
            float lean = strength * (.35 + .65 * gust);
            float rock = sin(t * .85 + phase) * .55 + sin(t * 2.05 + phase * 1.9) * .25 + sin(t * 3.7 + phase * .7) * .1 * gust;
            float bend = k * _Bend * (lean + strength * rock * (.6 + .6 * gust));
            v.vertex.xyz += dirOS * bend;
            v.vertex.y -= abs(bend) * k * .12; // keep the length roughly

            // branches and needle cards away from the trunk whip about
            float r = length(v.vertex.xz);
            float f = saturate(r / 2.5) * saturate(h / 3.0) * strength * (.35 + .9 * gust) * _Flutter;
            float w = t * (5.5 + 2.5 * gust) + phase * 3.1 + v.vertex.x * 1.7 + v.vertex.z * 1.3 + h * .6;
            v.vertex.y += sin(w) * f * .22;
            v.vertex.xz += dirOS.xz * (sin(w * 1.3 + 1.7) * .5 + .5) * f * .3;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
